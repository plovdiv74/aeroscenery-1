using BitMiracle.LibTiff.Classic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AeroScenery.AFS4
{
    /// <summary>
    /// One elevation raster: sample (i, j) is at lon = Lon0 + i DLon, lat = Lat0 - j DLat (rows from the north);
    /// heights in metres, NaN where the source has no data.
    /// </summary>
    public class DemCell
    {
        static DemCell()
        {
            // LibTiff.Net warns (on the console) about every GeoTIFF tag it has no definition for; it reads them anyway.
            Tiff.SetErrorHandler(new QuietTiffErrorHandler());
        }

        private class QuietTiffErrorHandler : TiffErrorHandler
        {
            public override void WarningHandler(Tiff tif, string method, string format, params object[] args)
            {
            }

            public override void WarningHandlerExt(Tiff tif, object clientData, string method, string format, params object[] args)
            {
            }
        }

        public double Lon0, Lat0, DLon, DLat;
        public int Width, Height;
        public float[] Heights;

        /// <summary>The fractional sample position of a point (may be outside the raster).</summary>
        public void Position(double lon, double lat, out double i, out double j)
        {
            i = (lon - this.Lon0) / this.DLon;
            j = (this.Lat0 - lat) / this.DLat;
        }

        public bool Contains(long i, long j)
        {
            return i >= 0 && j >= 0 && i < this.Width && j < this.Height;
        }

        public float At(long i, long j)
        {
            return this.Heights[j * this.Width + i];
        }

        /// <summary>
        /// Reads a GeoTIFF (one band; 16/32-bit integer or 32/64-bit float) and places it from its own tags: the model
        /// tie point and pixel scale, with PixelIsArea (sample centres half a sample in) or PixelIsPoint.
        /// </summary>
        public static DemCell ReadGeoTiff(string path)
        {
            using (var tif = Tiff.Open(path, "r"))
            {
                if (tif == null)
                {
                    throw new InvalidDataException(String.Format("'{0}' isn't a TIFF file", path));
                }

                int width = tif.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
                int height = tif.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
                int bits = tif.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();
                var sampleFormatField = tif.GetField(TiffTag.SAMPLEFORMAT);
                var sampleFormat = sampleFormatField == null ? SampleFormat.UINT : (SampleFormat)sampleFormatField[0].ToInt();
                var samplesField = tif.GetField(TiffTag.SAMPLESPERPIXEL);
                if (samplesField != null && samplesField[0].ToInt() != 1)
                {
                    throw new InvalidDataException(String.Format("'{0}': {1} bands, an elevation GeoTIFF has one", path, samplesField[0].ToInt()));
                }

                var tieField = tif.GetField(TiffTag.GEOTIFF_MODELTIEPOINTTAG);
                var scaleField = tif.GetField(TiffTag.GEOTIFF_MODELPIXELSCALETAG);
                if (tieField == null || scaleField == null)
                {
                    throw new InvalidDataException(String.Format("'{0}' has no GeoTIFF tie point and pixel scale", path));
                }

                double[] tie = tieField[1].ToDoubleArray();
                double[] scale = scaleField[1].ToDoubleArray();

                // GTRasterTypeGeoKey (1025): 1 = PixelIsArea (the tie point is a pixel's outer corner), 2 = PixelIsPoint.
                bool pixelIsPoint = false;
                var keysField = tif.GetField(TiffTag.GEOTIFF_GEOKEYDIRECTORYTAG);
                if (keysField != null)
                {
                    short[] keys = keysField[1].ToShortArray();
                    for (int k = 4; k + 3 < keys.Length; k += 4)
                    {
                        if (keys[k] == 1025 && keys[k + 1] == 0)
                        {
                            pixelIsPoint = keys[k + 3] == 2;
                        }
                    }
                }

                double noData = Double.NaN;
                var noDataField = tif.GetField((TiffTag)42113); // GDAL_NODATA, ASCII
                if (noDataField != null && noDataField.Length > 1)
                {
                    var text = noDataField[1].ToString().Trim('\0', ' ');
                    Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out noData);
                }

                var cell = new DemCell
                {
                    Width = width,
                    Height = height,
                    DLon = scale[0],
                    DLat = scale[1],
                    Heights = new float[(long)width * height],
                };
                double offset = pixelIsPoint ? 0 : 0.5;
                cell.Lon0 = tie[3] + (offset - tie[0]) * scale[0];
                cell.Lat0 = tie[4] - (offset - tie[1]) * scale[1];

                Func<byte[], int, float> convert = Converter(bits, sampleFormat, path);
                int bytes = bits / 8;
                if (tif.IsTiled())
                {
                    int tileWidth = tif.GetField(TiffTag.TILEWIDTH)[0].ToInt();
                    int tileHeight = tif.GetField(TiffTag.TILELENGTH)[0].ToInt();
                    var buffer = new byte[tif.TileSize()];
                    for (int ty = 0; ty < height; ty += tileHeight)
                    {
                        for (int tx = 0; tx < width; tx += tileWidth)
                        {
                            if (tif.ReadTile(buffer, 0, tx, ty, 0, 0) < 0)
                            {
                                throw new InvalidDataException(String.Format("'{0}': tile at ({1}, {2}) didn't read", path, tx, ty));
                            }

                            for (int y = 0; y < tileHeight && ty + y < height; y++)
                            {
                                long row = (long)(ty + y) * width;
                                for (int x = 0; x < tileWidth && tx + x < width; x++)
                                {
                                    cell.Heights[row + tx + x] = convert(buffer, (y * tileWidth + x) * bytes);
                                }
                            }
                        }
                    }
                }
                else
                {
                    var buffer = new byte[tif.ScanlineSize()];
                    for (int y = 0; y < height; y++)
                    {
                        if (!tif.ReadScanline(buffer, y))
                        {
                            throw new InvalidDataException(String.Format("'{0}': row {1} didn't read", path, y));
                        }

                        long row = (long)y * width;
                        for (int x = 0; x < width; x++)
                        {
                            cell.Heights[row + x] = convert(buffer, x * bytes);
                        }
                    }
                }

                // No data: the GDAL tag's value, NaN, and the SRTM/int16 void.
                for (long n = 0; n < cell.Heights.LongLength; n++)
                {
                    float h = cell.Heights[n];
                    if (h == noData || Single.IsNaN(h) || h <= -32767 || h > 10000)
                    {
                        cell.Heights[n] = Single.NaN;
                    }
                }

                return cell;
            }
        }

        private static Func<byte[], int, float> Converter(int bits, SampleFormat format, string path)
        {
            if (format == SampleFormat.IEEEFP && bits == 32) return (b, o) => BitConverter.ToSingle(b, o);
            if (format == SampleFormat.IEEEFP && bits == 64) return (b, o) => (float)BitConverter.ToDouble(b, o);
            if (format == SampleFormat.INT && bits == 16) return (b, o) => BitConverter.ToInt16(b, o);
            if (format == SampleFormat.UINT && bits == 16) return (b, o) => BitConverter.ToUInt16(b, o);
            if (format == SampleFormat.INT && bits == 32) return (b, o) => BitConverter.ToInt32(b, o);
            throw new InvalidDataException(String.Format("'{0}': {1}-bit {2} samples aren't supported", path, bits, format));
        }

        /// <summary>
        /// Reads an SRTM .hgt file: N x N big-endian int16 metres (N = 1201 or 3601), row 0 at the north edge, the
        /// corners on whole degrees; -32768 is a void.
        /// </summary>
        public static DemCell ReadHgt(string path, int lat, int lon)
        {
            var data = File.ReadAllBytes(path);
            int n = (int)Math.Round(Math.Sqrt(data.Length / 2.0));
            if ((long)n * n * 2 != data.Length || n < 2)
            {
                throw new InvalidDataException(String.Format("'{0}': {1} bytes isn't an N x N .hgt", path, data.Length));
            }

            var cell = new DemCell
            {
                Width = n,
                Height = n,
                Lon0 = lon,
                Lat0 = lat + 1,
                DLon = 1.0 / (n - 1),
                DLat = 1.0 / (n - 1),
                Heights = new float[(long)n * n],
            };
            for (long k = 0; k < cell.Heights.LongLength; k++)
            {
                short v = (short)((data[2 * k] << 8) | data[2 * k + 1]);
                cell.Heights[k] = v == -32768 ? Single.NaN : v;
            }

            return cell;
        }
    }

    /// <summary>
    /// Heights from an elevation source (and its fallbacks), cell by cell: downloads cells into a cache folder, loads
    /// them on demand, and samples bilinearly across cell edges.
    /// </summary>
    public class DemSet
    {
        private readonly List<ElevationSource> chain = new List<ElevationSource>();
        private readonly string cacheDirectory;
        private readonly string userAgent;
        private readonly ConcurrentDictionary<string, Lazy<DemCell>> cells = new ConcurrentDictionary<string, Lazy<DemCell>>();
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

        /// <param name="sources">All sources (for fallbacks).</param>
        /// <param name="source">The one to use.</param>
        /// <param name="cacheDirectory">Downloads go to cacheDirectory\&lt;source name&gt;\.</param>
        public DemSet(IList<ElevationSource> sources, ElevationSource source, string cacheDirectory, string userAgent)
        {
            this.cacheDirectory = cacheDirectory;
            this.userAgent = userAgent;
            for (var s = source; s != null && !this.chain.Contains(s); s = sources.FirstOrDefault(x => x.Name == s.Fallback))
            {
                this.chain.Add(s);
            }
        }

        public ElevationSource Source { get { return this.chain[0]; } }

        public IEnumerable<ElevationSource> Chain { get { return this.chain; } }

        /// <summary>
        /// The cells (south-west corners) covering a lon/lat box, with one cell of margin where the box comes within
        /// marginDegrees of a cell edge (bilinear needs the neighbour's first samples).
        /// </summary>
        public static IEnumerable<Tuple<int, int>> CellsCovering(double west, double south, double east, double north, double marginDegrees)
        {
            for (int lat = (int)Math.Floor(south - marginDegrees); lat <= (int)Math.Floor(north + marginDegrees); lat++)
            {
                for (int lon = (int)Math.Floor(west - marginDegrees); lon <= (int)Math.Floor(east + marginDegrees); lon++)
                {
                    yield return Tuple.Create(lat, lon);
                }
            }
        }

        /// <summary>
        /// Downloads (if needed) and loads the given cells of the main source, reporting each step. Fallback cells are
        /// fetched later, only where needed.
        /// </summary>
        public async Task PrepareAsync(IEnumerable<Tuple<int, int>> cellCorners, IProgress<string> progress, CancellationToken cancellation)
        {
            foreach (var corner in cellCorners)
            {
                cancellation.ThrowIfCancellationRequested();
                var source = this.chain[0];
                if (source.IsDownload)
                {
                    await DownloadAsync(source, corner.Item1, corner.Item2, progress, cancellation);
                }

                if (progress != null)
                {
                    progress.Report(String.Format("Loading elevation cell {0}", CellName(corner.Item1, corner.Item2)));
                }

                await Task.Run(() => GetCell(source, corner.Item1, corner.Item2), cancellation);
            }
        }

        public static string CellName(int lat, int lon)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}{1:00}{2}{3:000}", lat >= 0 ? "N" : "S", Math.Abs(lat), lon >= 0 ? "E" : "W", Math.Abs(lon));
        }

        private string CachePath(ElevationSource source, int lat, int lon)
        {
            var url = new Uri(source.CellLocation(lat, lon));
            var safeName = String.Concat(source.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            return Path.Combine(this.cacheDirectory, safeName, Path.GetFileName(url.LocalPath));
        }

        private async Task DownloadAsync(ElevationSource source, int lat, int lon, IProgress<string> progress, CancellationToken cancellation)
        {
            var path = CachePath(source, lat, lon);
            if (File.Exists(path) || File.Exists(path + ".missing"))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (progress != null)
            {
                progress.Report(String.Format("Downloading elevation cell {0} ({1})", CellName(lat, lon), source.Name));
            }

            using (var request = new HttpRequestMessage(HttpMethod.Get, source.CellLocation(lat, lon)))
            {
                if (!String.IsNullOrEmpty(this.userAgent))
                {
                    request.Headers.TryAddWithoutValidation("User-Agent", this.userAgent);
                }

                using (var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        // S3 answers 403 or 404 for a key that doesn't exist: the source has no such cell.
                        File.WriteAllText(path + ".missing", source.CellLocation(lat, lon));
                        return;
                    }

                    response.EnsureSuccessStatusCode();
                    var temporary = path + ".download";
                    using (var file = File.Create(temporary))
                    {
                        await response.Content.CopyToAsync(file);
                    }

                    File.Move(temporary, path);
                }
            }
        }

        // The cell of a source with the given south-west corner, or null if the source has none there.
        private DemCell GetCell(ElevationSource source, int lat, int lon)
        {
            var key = source.Name + "|" + lat + "|" + lon;
            return this.cells.GetOrAdd(key, _ => new Lazy<DemCell>(() => LoadCell(source, lat, lon))).Value;
        }

        private DemCell LoadCell(ElevationSource source, int lat, int lon)
        {
            string path;
            if (source.IsDownload)
            {
                path = CachePath(source, lat, lon);
                if (!File.Exists(path) && !File.Exists(path + ".missing"))
                {
                    // A fallback cell, fetched on first use.
                    DownloadAsync(source, lat, lon, null, CancellationToken.None).GetAwaiter().GetResult();
                }
            }
            else
            {
                path = source.CellLocation(lat, lon);
            }

            if (!File.Exists(path))
            {
                return null;
            }

            return source.Format == "hgt" ? DemCell.ReadHgt(path, lat, lon) : DemCell.ReadGeoTiff(path);
        }

        /// <summary>
        /// The height at a point in metres, or null if no source in the chain has data there.
        /// </summary>
        public double? Height(double lon, double lat)
        {
            foreach (var source in this.chain)
            {
                double h = SourceHeight(source, lon, lat);
                if (!Double.IsNaN(h))
                {
                    return h;
                }

                if (source.MissingIsSea)
                {
                    return 0;
                }
            }

            return null;
        }

        // Bilinear in one source; NaN where it has no data. A neighbour that is missing or void is replaced by the
        // nearest valid one of the four.
        private double SourceHeight(ElevationSource source, double lon, double lat)
        {
            var cell = GetCell(source, (int)Math.Floor(lat), (int)Math.Floor(lon));
            if (cell == null)
            {
                return Double.NaN;
            }

            double fi, fj;
            cell.Position(lon, lat, out fi, out fj);
            long i0 = (long)Math.Floor(fi), j0 = (long)Math.Floor(fj);
            double wx = fi - i0, wy = fj - j0;

            double h00 = Sample(source, cell, i0, j0), h10 = Sample(source, cell, i0 + 1, j0);
            double h01 = Sample(source, cell, i0, j0 + 1), h11 = Sample(source, cell, i0 + 1, j0 + 1);
            if (!Double.IsNaN(h00) && !Double.IsNaN(h10) && !Double.IsNaN(h01) && !Double.IsNaN(h11))
            {
                return (h00 * (1 - wx) + h10 * wx) * (1 - wy) + (h01 * (1 - wx) + h11 * wx) * wy;
            }

            var candidates = new[]
            {
                Tuple.Create(h00, (1 - wx) * (1 - wy)), Tuple.Create(h10, wx * (1 - wy)),
                Tuple.Create(h01, (1 - wx) * wy), Tuple.Create(h11, wx * wy),
            };
            var valid = candidates.Where(c => !Double.IsNaN(c.Item1)).OrderByDescending(c => c.Item2).FirstOrDefault();
            return valid == null ? Double.NaN : valid.Item1;
        }

        // Sample (i, j) of a cell's grid; outside the raster, the sample at the same place in the neighbouring cell
        // (whose first row or column is this cell's missing edge).
        private double Sample(ElevationSource source, DemCell cell, long i, long j)
        {
            if (cell.Contains(i, j))
            {
                return cell.At(i, j);
            }

            double lon = cell.Lon0 + i * cell.DLon, lat = cell.Lat0 - j * cell.DLat;
            int baseLat = (int)Math.Floor(lat), baseLon = (int)Math.Floor(lon);
            for (int dy = 0; dy >= -1; dy--)
            {
                for (int dx = 0; dx >= -1; dx--)
                {
                    var other = GetCell(source, baseLat + dy, baseLon + dx);
                    if (other == null || other == cell)
                    {
                        continue;
                    }

                    double oi, oj;
                    other.Position(lon, lat, out oi, out oj);
                    long ni = (long)Math.Round(oi), nj = (long)Math.Round(oj);
                    if (other.Contains(ni, nj))
                    {
                        return other.At(ni, nj);
                    }
                }
            }

            return Double.NaN;
        }
    }
}
