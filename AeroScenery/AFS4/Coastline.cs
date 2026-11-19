using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AeroScenery.AFS4
{
    /// <summary>
    /// Which pixels of a base-level ttc tile are sea, from OpenStreetMap's land polygons (the OSM coastline assembled
    /// into polygons and split into pieces of at most 1 degree, osmdata.openstreetmap.de "land-polygons-split-4326",
    /// lon/lat). Everything outside the land polygons is sea; lakes and rivers are inside them, so they keep their
    /// imagery. The approach follows Ortho4XP's water masks (O4_Mask_Utils.py), which draw OSM water polygons rather
    /// than thresholding elevation: a DEM is too coarse at the shore and puts low land (salt marsh) below sea level.
    ///
    /// A pixel is sea if its centre is outside every land polygon (even-odd rule per polygon, so holes work). BC1
    /// alpha is one bit, so the cut is sharp; a strip offshore (StripMetres) gets a falling opacity, which the renderer
    /// turns into a colour fade toward Aerofly's stock imagery (TtcRenderer), where Ortho4XP blurs its masks for
    /// X-Plane's 8-bit alpha.
    /// </summary>
    public class Coastline
    {
        public const string DownloadUrl = "https://osmdata.openstreetmap.de/download/land-polygons-split-4326.zip";
        public const string ShapefileRelativePath = @"land-polygons-split-4326\land_polygons.shp";

        private const int Side = TtcWriter.Side;

        // A polygon in level-16 AFS grid units: rings of (x, y) pairs, and its bounding box.
        private class Polygon
        {
            public double[][] Rings;
            public double MinX, MinY, MaxX, MaxY;
        }

        private readonly List<Polygon> polygons;

        private Coastline(List<Polygon> polygons)
        {
            this.polygons = polygons;
        }

        /// <summary>
        /// The land polygons whose bounding boxes touch the given area (degrees), from a land_polygons.shp.
        /// </summary>
        public static Coastline Load(string shapefile, double west, double south, double east, double north)
        {
            var shx = Path.ChangeExtension(shapefile, ".shx");
            var offsets = ReadIndex(shx);
            var polygons = new List<Polygon>();
            using (var stream = new FileStream(shapefile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess))
            {
                var header = new byte[44];
                foreach (var offset in offsets)
                {
                    // Record header (8 bytes, big-endian number and length), then shape type and bounding box.
                    stream.Position = offset + 8;
                    ReadFully(stream, header, 36);
                    int type = BitConverter.ToInt32(header, 0);
                    if (type != 5)
                    {
                        continue; // null shape, or not a polygon
                    }

                    double xmin = BitConverter.ToDouble(header, 4), ymin = BitConverter.ToDouble(header, 12);
                    double xmax = BitConverter.ToDouble(header, 20), ymax = BitConverter.ToDouble(header, 28);
                    if (xmax < west || xmin > east || ymax < south || ymin > north)
                    {
                        continue;
                    }

                    ReadFully(stream, header, 8);
                    int numParts = BitConverter.ToInt32(header, 0), numPoints = BitConverter.ToInt32(header, 4);
                    var buffer = new byte[4 * numParts + 16 * numPoints];
                    ReadFully(stream, buffer, buffer.Length);
                    polygons.Add(ToGrid(buffer, numParts, numPoints));
                }
            }

            return new Coastline(polygons);
        }

        // The .shx: a 100-byte header, then 8-byte records of big-endian (offset, length) in 16-bit words.
        private static List<long> ReadIndex(string shx)
        {
            var bytes = File.ReadAllBytes(shx);
            var offsets = new List<long>((bytes.Length - 100) / 8);
            for (int p = 100; p + 8 <= bytes.Length; p += 8)
            {
                offsets.Add(2L * ((bytes[p] << 24) | (bytes[p + 1] << 16) | (bytes[p + 2] << 8) | bytes[p + 3]));
            }

            return offsets;
        }

        private static void ReadFully(Stream stream, byte[] buffer, int count)
        {
            int done = 0;
            while (done < count)
            {
                int n = stream.Read(buffer, done, count - done);
                if (n <= 0)
                {
                    throw new InvalidDataException("The land polygons shapefile ends early");
                }

                done += n;
            }
        }

        private static Polygon ToGrid(byte[] buffer, int numParts, int numPoints)
        {
            var polygon = new Polygon
            {
                Rings = new double[numParts][],
                MinX = double.MaxValue, MinY = double.MaxValue, MaxX = double.MinValue, MaxY = double.MinValue,
            };
            int pointsAt = 4 * numParts;
            for (int part = 0; part < numParts; part++)
            {
                int first = BitConverter.ToInt32(buffer, 4 * part);
                int end = part + 1 < numParts ? BitConverter.ToInt32(buffer, 4 * (part + 1)) : numPoints;
                var ring = new double[2 * (end - first)];
                for (int k = first; k < end; k++)
                {
                    double x = AfsGrid.X(BitConverter.ToDouble(buffer, pointsAt + 16 * k));
                    double y = AfsGrid.Y(BitConverter.ToDouble(buffer, pointsAt + 16 * k + 8));
                    ring[2 * (k - first)] = x;
                    ring[2 * (k - first) + 1] = y;
                    polygon.MinX = Math.Min(polygon.MinX, x);
                    polygon.MaxX = Math.Max(polygon.MaxX, x);
                    polygon.MinY = Math.Min(polygon.MinY, y);
                    polygon.MaxY = Math.Max(polygon.MaxY, y);
                }

                polygon.Rings[part] = ring;
            }

            return polygon;
        }

        /// <summary>
        /// Width in metres of the strip of sea along the coastline where the imagery blends into Aerofly's stock
        /// imagery instead of ending at the coastline: its opacity falls from 255 at the coast to 0 at this distance
        /// (smoothstep), and the renderer blends the colour by it (TtcRenderer). 0: a sharp cut at the coastline.
        /// </summary>
        public double StripMetres { get; set; }

        /// <summary>
        /// The opacity of a tile's pixels (Side x Side, rows from the south, as the tile's image): 255 on land (a pixel
        /// centre inside a land polygon), falling across the strip (StripMetres) to 0 at sea; null if all land.
        /// Lines of constant longitude and latitude are straight on the AFS grid, and coastline edges are short, so the
        /// polygon edges are drawn as straight lines between their vertices on the grid.
        /// </summary>
        public byte[] Opacity(AfsTile tile)
        {
            double pixelSpan = tile.Span / (double)Side;
            double metresPerPixel = MetresPerPixel(tile);
            double stripPixels = this.StripMetres / metresPerPixel;

            // The land mask with a margin as wide as the strip, so the distance to land sees across the tile's edges.
            int margin = stripPixels > 0 ? (int)Math.Ceiling(stripPixels) + 1 : 0;
            int size = Side + 2 * margin;
            double originX = tile.X - margin * pixelSpan, originY = tile.Y - margin * pixelSpan;
            var land = new bool[size * size];
            bool anyLand = false;
            double extent = size * pixelSpan;
            foreach (var polygon in this.polygons)
            {
                if (polygon.MaxX < originX || polygon.MinX > originX + extent || polygon.MaxY < originY || polygon.MinY > originY + extent)
                {
                    continue;
                }

                anyLand |= Fill(polygon, originX, originY, 1 / pixelSpan, size, land);
            }

            var opacity = new byte[Side * Side];
            if (!anyLand)
            {
                return opacity; // all sea, with no coast within the strip
            }

            bool anySea = false;
            if (margin == 0)
            {
                for (int p = 0; p < opacity.Length; p++)
                {
                    opacity[p] = land[p] ? (byte)255 : (byte)0;
                    anySea |= !land[p];
                }

                return anySea ? opacity : null;
            }

            var distance = DistanceToLand(land, size);
            for (int j = 0; j < Side; j++)
            {
                int row = (j + margin) * size + margin;
                for (int i = 0; i < Side; i++)
                {
                    double d = distance[row + i];
                    if (d <= 0)
                    {
                        opacity[j * Side + i] = 255;
                        continue;
                    }

                    anySea = true;
                    double t = d / stripPixels;
                    if (t < 1)
                    {
                        double fade = 1 - t * t * (3 - 2 * t);
                        opacity[j * Side + i] = (byte)Math.Round(255 * fade);
                    }
                }
            }

            return anySea ? opacity : null;
        }

        // Ground metres per pixel of a tile (east-west, at its centre; the AFS grid isn't conformal, but near enough).
        private static double MetresPerPixel(AfsTile tile)
        {
            double lat = AfsGrid.Lat(tile.Y + tile.Span / 2.0);
            double degrees = tile.EastLongitude - tile.WestLongitude;
            return degrees * 111319.49 * Math.Cos(lat * Math.PI / 180) / Side;
        }

        // The Euclidean distance in pixels from each pixel centre to the nearest land pixel centre (0 on land): the
        // separable squared distance transform of Felzenszwalb and Huttenlocher, by columns and then by rows. Without
        // land in reach the distance is about 1e6 (FarSquared).
        private static float[] DistanceToLand(bool[] land, int size)
        {
            var squared = new double[size * size];
            Parallel.For(0, size, i =>
            {
                var f = new double[size];
                var d = new double[size];
                var v = new int[size];
                var z = new double[size + 1];
                for (int j = 0; j < size; j++)
                {
                    f[j] = land[j * size + i] ? 0 : FarSquared;
                }

                Transform1D(f, size, d, v, z);
                for (int j = 0; j < size; j++)
                {
                    squared[j * size + i] = d[j];
                }
            });

            var distance = new float[size * size];
            Parallel.For(0, size, j =>
            {
                var f = new double[size];
                var d = new double[size];
                var v = new int[size];
                var z = new double[size + 1];
                Array.Copy(squared, j * size, f, 0, size);
                Transform1D(f, size, d, v, z);
                for (int i = 0; i < size; i++)
                {
                    distance[j * size + i] = (float)Math.Sqrt(d[i]);
                }
            });

            return distance;
        }

        // Large against any squared distance on the grid (sizes are a few thousand pixels), small enough for doubles to
        // keep q * q next to it exactly.
        private const double FarSquared = 1e12;

        // The 1D squared distance transform of f into d: d[q] = min over p of (q - p)^2 + f[p], the lower envelope of
        // the parabolas rooted at each p (v: their positions, z: the boundaries between them).
        private static void Transform1D(double[] f, int n, double[] d, int[] v, double[] z)
        {
            int k = 0;
            v[0] = 0;
            z[0] = double.NegativeInfinity;
            z[1] = double.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                double s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                }

                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = double.PositiveInfinity;
            }

            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q)
                {
                    k++;
                }

                double dq = q - v[k];
                d[q] = dq * dq + f[v[k]];
            }
        }

        // Sets the pixels whose centres are inside the polygon (even-odd over all its rings) on a size x size grid with
        // its south-west corner at (originX, originY) and scale pixels per grid unit: a scanline fill with the edges
        // bucketed by their first row. Returns whether any pixel was set.
        private static bool Fill(Polygon polygon, double originX, double originY, double scale, int size, bool[] land)
        {
            // Edges in pixel units, where pixel centre (i, j) is at (i + 0.5, j + 0.5); an edge covers the rows whose
            // centres lie in [min y, max y) (half-open, so a vertex on a row isn't counted twice).
            var buckets = new List<double[]>[size];
            foreach (var ring in polygon.Rings)
            {
                int n = ring.Length / 2;
                for (int k = 0; k < n; k++)
                {
                    int k1 = k + 1 < n ? k + 1 : 0;
                    double xa = (ring[2 * k] - originX) * scale, ya = (ring[2 * k + 1] - originY) * scale;
                    double xb = (ring[2 * k1] - originX) * scale, yb = (ring[2 * k1 + 1] - originY) * scale;
                    if (ya == yb)
                    {
                        continue;
                    }

                    double ylo = Math.Min(ya, yb), yhi = Math.Max(ya, yb);
                    int j0 = Math.Max(0, (int)Math.Ceiling(ylo - 0.5));
                    int j1 = Math.Min(size - 1, (int)Math.Ceiling(yhi - 0.5) - 1);
                    if (j0 > j1)
                    {
                        continue;
                    }

                    // x at row centre j: x0 + (j + 0.5 - y0) * dxdy; stored as (x at row j0, dx per row, last row)
                    double dxdy = (xb - xa) / (yb - ya);
                    var edge = new[] { xa + (j0 + 0.5 - ya) * dxdy, dxdy, j1 };
                    if (buckets[j0] == null)
                    {
                        buckets[j0] = new List<double[]>();
                    }

                    buckets[j0].Add(edge);
                }
            }

            bool any = false;
            var active = new List<double[]>();
            var crossings = new List<double>();
            for (int j = 0; j < size; j++)
            {
                if (buckets[j] != null)
                {
                    active.AddRange(buckets[j]);
                }

                if (active.Count == 0)
                {
                    continue;
                }

                crossings.Clear();
                foreach (var edge in active)
                {
                    crossings.Add(edge[0]);
                }

                crossings.Sort();
                int row = j * size;
                for (int c = 0; c + 1 < crossings.Count; c += 2)
                {
                    // pixel i is inside if crossings[c] <= i + 0.5 < crossings[c + 1]
                    int i0 = Math.Max(0, (int)Math.Ceiling(crossings[c] - 0.5));
                    int i1 = Math.Min(size - 1, (int)Math.Ceiling(crossings[c + 1] - 0.5) - 1);
                    for (int i = i0; i <= i1; i++)
                    {
                        land[row + i] = true;
                        any = true;
                    }
                }

                // advance: drop edges ending at this row, step the rest
                for (int e = active.Count - 1; e >= 0; e--)
                {
                    var edge = active[e];
                    if (edge[2] <= j)
                    {
                        active[e] = active[active.Count - 1];
                        active.RemoveAt(active.Count - 1);
                    }
                    else
                    {
                        edge[0] += edge[1];
                    }
                }
            }

            return any;
        }

        /// <summary>
        /// The land polygons shapefile under the given folder, downloaded (about 930 MB) and unpacked on first use.
        /// </summary>
        public static async Task<string> EnsureDownloadedAsync(string directory, string userAgent, IProgress<string> progress, CancellationToken cancellation)
        {
            var shapefile = Path.Combine(directory, ShapefileRelativePath);
            if (File.Exists(shapefile) && File.Exists(Path.ChangeExtension(shapefile, ".shx")))
            {
                return shapefile;
            }

            Directory.CreateDirectory(directory);
            var zip = Path.Combine(directory, "land-polygons-split-4326.zip");
            if (!File.Exists(zip))
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromHours(2) })
                using (var request = new HttpRequestMessage(HttpMethod.Get, DownloadUrl))
                {
                    if (!String.IsNullOrEmpty(userAgent))
                    {
                        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    }

                    using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation))
                    {
                        response.EnsureSuccessStatusCode();
                        long? total = response.Content.Headers.ContentLength;
                        var temporary = zip + ".download";
                        using (var input = await response.Content.ReadAsStreamAsync())
                        using (var output = File.Create(temporary))
                        {
                            var buffer = new byte[1 << 20];
                            long done = 0, reported = 0;
                            int n;
                            while ((n = await input.ReadAsync(buffer, 0, buffer.Length, cancellation)) > 0)
                            {
                                await output.WriteAsync(buffer, 0, n, cancellation);
                                done += n;
                                if (progress != null && done - reported >= 20 << 20)
                                {
                                    reported = done;
                                    progress.Report(total.HasValue
                                        ? String.Format("Downloading the OSM coastline: {0} of {1} MB", done >> 20, total.Value >> 20)
                                        : String.Format("Downloading the OSM coastline: {0} MB", done >> 20));
                                }
                            }
                        }

                        File.Move(temporary, zip);
                    }
                }
            }

            if (progress != null)
            {
                progress.Report("Unpacking the OSM coastline");
            }

            await Task.Run(() =>
            {
                using (var archive = ZipFile.OpenRead(zip))
                {
                    foreach (var entry in archive.Entries.Where(e => e.Name.Length > 0))
                    {
                        var target = Path.GetFullPath(Path.Combine(directory, entry.FullName));
                        if (!target.StartsWith(Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // an entry outside the folder
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        entry.ExtractToFile(target, true);
                    }
                }
            }, cancellation);

            File.Delete(zip);
            return shapefile;
        }
    }
}
