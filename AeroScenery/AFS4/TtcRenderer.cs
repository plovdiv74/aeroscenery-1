using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AeroScenery.AFS4
{
    public class TtcRendererProgress
    {
        public int BaseTilesDone { get; set; }
        public int BaseTilesTotal { get; set; }
        public int TilesWritten { get; set; }
    }

    public class TtcRendererResult
    {
        public List<string> Written { get; } = new List<string>();

        /// <summary>Selected tiles not written because source tiles were missing.</summary>
        public List<string> Incomplete { get; } = new List<string>();

        public SortedSet<string> MissingSourceTiles { get; } = new SortedSet<string>();

        /// <summary>The level rendered straight from the source tiles; coarser levels are box averages of it.</summary>
        public int BaseLevel { get; set; }

        /// <summary>Selected levels coarser than the square, which it can't fill.</summary>
        public List<int> SkippedLevels { get; } = new List<int>();
    }

    /// <summary>
    /// Renders the ttc tiles of an AFS grid square straight from downloaded Web Mercator tiles, without stitching or
    /// GeoConvert.
    ///
    /// The base level is the finest level the source resolves (zoom - 3: one tile pixel per source pixel in longitude)
    /// or the finest selected level, if that is finer. Each base-level pixel centre, corner + (i + 0.5, j + 0.5) span /
    /// 2048, goes to longitude/latitude, to a Web Mercator pixel position, and is sampled bilinearly. A coarser tile is
    /// the 2x2 box average of its four children, built depth-first, so only one branch of the quadtree is in memory.
    /// A tile is complete only if all its source pixels are; incomplete tiles (and their parents) are not written.
    /// </summary>
    public class TtcRenderer
    {
        private const int Side = TtcWriter.Side;
        private const int Half = Side / 2;

        private readonly SourceTiles source;
        private readonly string outputDirectory;

        public TtcRenderer(SourceTiles source, string outputDirectory)
        {
            this.source = source;
            this.outputDirectory = outputDirectory;
        }

        public static int FinestLevel(int zoom)
        {
            return zoom - 3;
        }

        public TtcRendererResult Render(AfsTile square, IEnumerable<int> levels, IProgress<TtcRendererProgress> progress, CancellationToken cancellation)
        {
            var result = new TtcRendererResult();
            var selected = new HashSet<int>(levels.Where(level => level >= square.Level && level <= 16));
            result.SkippedLevels.AddRange(levels.Where(level => level < square.Level).Distinct().OrderBy(level => level));
            if (selected.Count == 0)
            {
                return result;
            }

            int baseLevel = Math.Max(FinestLevel(this.source.Zoom), selected.Max());
            result.BaseLevel = baseLevel;
            var state = new RenderState
            {
                Selected = selected,
                BaseLevel = baseLevel,
                Result = result,
                Progress = progress,
                Cancellation = cancellation,
                Report = new TtcRendererProgress { BaseTilesTotal = 1 << (2 * (baseLevel - square.Level)) },
            };

            Directory.CreateDirectory(this.outputDirectory);
            Build(square, state);
            return result;
        }

        private class RenderState
        {
            public HashSet<int> Selected;
            public int BaseLevel;
            public TtcRendererResult Result;
            public IProgress<TtcRendererProgress> Progress;
            public CancellationToken Cancellation;
            public TtcRendererProgress Report;
        }

        // The tile's image (RGB, rows from the south), or null if it is incomplete; writes it if its level is selected.
        private byte[] Build(AfsTile tile, RenderState state)
        {
            state.Cancellation.ThrowIfCancellationRequested();

            byte[] image;
            if (tile.Level == state.BaseLevel)
            {
                image = RenderFromSource(tile, state.Result);
                state.Report.BaseTilesDone++;
                if (state.Progress != null)
                {
                    state.Progress.Report(new TtcRendererProgress
                    {
                        BaseTilesDone = state.Report.BaseTilesDone,
                        BaseTilesTotal = state.Report.BaseTilesTotal,
                        TilesWritten = state.Result.Written.Count,
                    });
                }
            }
            else
            {
                image = new byte[Side * Side * 3];
                bool complete = true;
                for (int cy = 0; cy < 2; cy++)
                {
                    for (int cx = 0; cx < 2; cx++)
                    {
                        // Children are built even after one is incomplete, so their own selected tiles get written.
                        var child = Build(tile.Child(cx, cy), state);
                        if (child == null)
                        {
                            complete = false;
                        }
                        else
                        {
                            HalveInto(image, child, cx, cy);
                        }
                    }
                }

                if (!complete)
                {
                    image = null;
                }
            }

            if (state.Selected.Contains(tile.Level))
            {
                if (image == null)
                {
                    state.Result.Incomplete.Add(tile.Name());
                }
                else
                {
                    var path = Path.Combine(this.outputDirectory, tile.Name() + ".ttc");
                    TtcWriter.Write(path, image);
                    state.Result.Written.Add(path);
                }
            }

            return image;
        }

        // Writes the 2x2 box average of a child tile into its quadrant of the parent (cx: 0 west; cy: 0 south).
        private static void HalveInto(byte[] parent, byte[] child, int cx, int cy)
        {
            Parallel.For(0, Half, y =>
            {
                int row0 = 2 * y * Side * 3, row1 = row0 + Side * 3;
                int o = ((cy * Half + y) * Side + cx * Half) * 3;
                for (int x = 0; x < Half * 3; x += 3)
                {
                    int s = 2 * x;
                    for (int c = 0; c < 3; c++, o++)
                    {
                        parent[o] = (byte)((child[row0 + s + c] + child[row0 + s + 3 + c] + child[row1 + s + c] + child[row1 + s + 3 + c] + 2) >> 2);
                    }
                }
            });
        }

        private byte[] RenderFromSource(AfsTile tile, TtcRendererResult result)
        {
            int zoom = this.source.Zoom;
            double pixelSpan = (double)tile.Span / Side;

            // Sample positions in source pixels, where an integer is a pixel centre: x per column, y per row.
            var u = new double[Side];
            var v = new double[Side];
            for (int i = 0; i < Side; i++)
            {
                u[i] = WebMercator.PixelX(AfsGrid.Lon(tile.X + (i + 0.5) * pixelSpan), zoom) - 0.5;
                v[i] = WebMercator.PixelY(AfsGrid.Lat(tile.Y + (i + 0.5) * pixelSpan), zoom) - 0.5;
            }

            // u rises with i; v falls with j (rows run south to north, Mercator y runs north to south).
            long x0 = (long)Math.Floor(u[0]), x1 = (long)Math.Floor(u[Side - 1]) + 1;
            long y0 = (long)Math.Floor(v[Side - 1]), y1 = (long)Math.Floor(v[0]) + 1;
            var window = this.source.Load((int)(x0 >> 8), (int)(y0 >> 8), (int)(x1 >> 8), (int)(y1 >> 8));

            var image = new byte[Side * Side * 3];
            int incomplete = 0;
            Parallel.For(0, Side, j =>
            {
                double fy = v[j] - Math.Floor(v[j]);
                long sy0 = (long)Math.Floor(v[j]), sy1 = sy0 + 1;
                int o = j * Side * 3;
                for (int i = 0; i < Side; i++, o += 3)
                {
                    double fx = u[i] - Math.Floor(u[i]);
                    long sx0 = (long)Math.Floor(u[i]), sx1 = sx0 + 1;
                    var t00 = window.TileAt(sx0, sy0);
                    var t10 = window.TileAt(sx1, sy0);
                    var t01 = window.TileAt(sx0, sy1);
                    var t11 = window.TileAt(sx1, sy1);
                    if (t00 != null && t10 != null && t01 != null && t11 != null)
                    {
                        int a = Offset(sx0, sy0), b = Offset(sx1, sy0), c = Offset(sx0, sy1), d = Offset(sx1, sy1);
                        for (int k = 0; k < 3; k++)
                        {
                            double top = t00[a + k] + (t10[b + k] - t00[a + k]) * fx;
                            double bottom = t01[c + k] + (t11[d + k] - t01[c + k]) * fx;
                            image[o + k] = (byte)(top + (bottom - top) * fy + 0.5);
                        }
                    }
                    else
                    {
                        // At the edge of the download, a neighbour for the interpolation may be missing: take the
                        // nearest pixel. Only if that is missing too is the tile incomplete.
                        long nx = (long)Math.Floor(u[i] + 0.5), ny = (long)Math.Floor(v[j] + 0.5);
                        var t = window.TileAt(nx, ny);
                        if (t == null)
                        {
                            Interlocked.Increment(ref incomplete);
                            continue;
                        }

                        int a = Offset(nx, ny);
                        image[o] = t[a];
                        image[o + 1] = t[a + 1];
                        image[o + 2] = t[a + 2];
                    }
                }
            });

            if (incomplete > 0)
            {
                lock (result)
                {
                    foreach (var name in window.MissingTiles(this.source))
                    {
                        result.MissingSourceTiles.Add(name);
                    }
                }

                return null;
            }

            return image;
        }

        // The byte offset of global pixel (x, y) within its source tile.
        private static int Offset(long x, long y)
        {
            return (int)(((y & 255) * WebMercator.TileSize + (x & 255)) * 3);
        }
    }
}
