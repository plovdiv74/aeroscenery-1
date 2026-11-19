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

        /// <summary>Selected tiles not written because the server has no imagery anywhere in them (open water).</summary>
        public List<string> NoImagery { get; } = new List<string>();

        public SortedSet<string> MissingSourceTiles { get; } = new SortedSet<string>();

        /// <summary>The level rendered straight from the source tiles; coarser levels are box averages of it.</summary>
        public int BaseLevel { get; set; }

        /// <summary>Selected levels coarser than the square, which it can't fill.</summary>
        public List<int> SkippedLevels { get; } = new List<int>();
    }

    /// <summary>
    /// A tile's image: RGB, rows from the south, and its coverage (per pixel: 255 opaque, 0 transparent, between:
    /// shown with that probability, TtcWriter.Dither), or null if every pixel is opaque. An Empty image has no
    /// imagery at all.
    /// </summary>
    public class TileImage
    {
        public static readonly TileImage Empty = new TileImage(null, null);

        public readonly byte[] Rgb;
        public readonly byte[] Coverage;

        public TileImage(byte[] rgb, byte[] coverage)
        {
            this.Rgb = rgb;
            this.Coverage = coverage;
        }

        public bool IsEmpty { get { return this.Rgb == null; } }
    }

    /// <summary>
    /// Renders the ttc tiles of an AFS grid square straight from downloaded Web Mercator tiles.
    ///
    /// The base level is the finest level the source resolves (zoom - 3: one tile pixel per source pixel in longitude)
    /// or the finest selected level, if that is finer. Each base-level pixel centre, corner + (i + 0.5, j + 0.5) span /
    /// 2048, goes to longitude/latitude, to a Web Mercator pixel position, and is sampled bilinearly. A coarser tile is
    /// the 2x2 box average of its four children, built depth-first, so only one branch of the quadtree is in memory.
    ///
    /// Where the server has no imagery (a source tile with a no-imagery marker, e.g. open water) the pixels are
    /// transparent, and Aerofly shows its own imagery through them (BC1 transparency, seen in the sim 2026-11-18). A
    /// source tile that is simply missing (a failed download) makes the tile incomplete: it and its parents are not
    /// written, so the gap is noticed and filled by downloading again. A tile without any imagery isn't written.
    ///
    /// With a sea mask (seaFade, from the OSM coastline: Coastline.SeaFade) the imagery near the coast stays as it is,
    /// further out its colour fades into a solid sea colour, and the open sea beyond is that colour: Bing's water is
    /// much lighter than Aerofly's (RGB about 42,60,76 against 8,23,47 at Provincetown), and the stock tiles end in
    /// that solid colour too. A tile that is all open sea isn't written, so Aerofly's own sea shows there. Inside a
    /// written tile the open sea isn't left transparent, as the stock tile can still show its own near-shore photo
    /// there (Provincetown's harbour).
    /// </summary>
    public class TtcRenderer
    {
        private const int Side = TtcWriter.Side;
        private const int Half = Side / 2;

        private readonly SourceTiles source;
        private readonly string outputDirectory;
        private readonly Func<AfsTile, byte[]> seaFade;
        private readonly byte[] seaColour;

        /// <param name="seaFade">Optional: per pixel of a base-level tile (rows from the south), 0 for the imagery as
        /// it is, 1..254 for blending it toward seaColour by that / 254, 255 for open sea (seaColour; a tile that is all
        /// open sea isn't written); null if all 0 (Coastline.SeaFade). Null: only areas without imagery are
        /// transparent.</param>
        /// <param name="seaColour">The colour the sea fades into (RGB), e.g. Aerofly's 8,23,47.</param>
        public TtcRenderer(SourceTiles source, string outputDirectory, Func<AfsTile, byte[]> seaFade = null, byte[] seaColour = null)
        {
            this.source = source;
            this.outputDirectory = outputDirectory;
            this.seaFade = seaFade;
            this.seaColour = seaColour ?? new byte[] { 8, 23, 47 };
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

        // The tile's image, or null if it is incomplete; writes it if its level is selected.
        private TileImage Build(AfsTile tile, RenderState state)
        {
            state.Cancellation.ThrowIfCancellationRequested();

            TileImage image;
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
                // Children are built even after one is incomplete, so their own selected tiles get written.
                var children = new TileImage[2, 2];
                bool complete = true;
                for (int cy = 0; cy < 2; cy++)
                {
                    for (int cx = 0; cx < 2; cx++)
                    {
                        children[cx, cy] = Build(tile.Child(cx, cy), state);
                        complete &= children[cx, cy] != null;
                    }
                }

                image = complete ? Combine(children) : null;
            }

            if (state.Selected.Contains(tile.Level))
            {
                if (image == null)
                {
                    state.Result.Incomplete.Add(tile.Name());
                }
                else if (image.IsEmpty)
                {
                    state.Result.NoImagery.Add(tile.Name());

                    // A tile written by an earlier run (before the sea mask, say) would otherwise be installed again.
                    var stale = Path.Combine(this.outputDirectory, tile.Name() + ".ttc");
                    if (File.Exists(stale))
                    {
                        File.Delete(stale);
                    }
                }
                else
                {
                    var path = Path.Combine(this.outputDirectory, tile.Name() + ".ttc");
                    TtcWriter.Write(path, image.Rgb, image.Coverage);
                    state.Result.Written.Add(path);
                }
            }

            return image;
        }

        // The parent of four children (indexed [cx, cy]: cx 0 west, cy 0 south), each 2x2 box averaged into its quadrant.
        private static TileImage Combine(TileImage[,] children)
        {
            bool anyImagery = false, anyTransparent = false;
            foreach (var child in children)
            {
                anyImagery |= !child.IsEmpty;
                anyTransparent |= child.IsEmpty || child.Coverage != null;
            }

            if (!anyImagery)
            {
                return TileImage.Empty;
            }

            var rgb = new byte[Side * Side * 3];
            var coverage = anyTransparent ? new byte[Side * Side] : null;
            for (int cy = 0; cy < 2; cy++)
            {
                for (int cx = 0; cx < 2; cx++)
                {
                    HalveInto(rgb, coverage, children[cx, cy], cx, cy);
                }
            }

            return new TileImage(rgb, coverage);
        }

        // Writes the 2x2 box average of a child tile into its quadrant of the parent (cx: 0 west; cy: 0 south): the mean
        // coverage, and the mean colour of the pixels with imagery (TtcWriter.AverageCovered).
        private static void HalveInto(byte[] parentRgb, byte[] parentCoverage, TileImage child, int cx, int cy)
        {
            if (child.IsEmpty)
            {
                return; // parentCoverage is 0 there already
            }

            var rgb = child.Rgb;
            var coverage = child.Coverage;
            Parallel.For(0, Half, y =>
            {
                int row0 = 2 * y * Side, row1 = row0 + Side;
                int p = (cy * Half + y) * Side + cx * Half;
                for (int x = 0; x < Half; x++, p++)
                {
                    int s0 = row0 + 2 * x, s1 = row1 + 2 * x;
                    TtcWriter.AverageCovered(rgb, coverage, s0, s0 + 1, s1, s1 + 1, parentRgb, parentCoverage, p);
                }
            });
        }

        private TileImage RenderFromSource(AfsTile tile, TtcRendererResult result)
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
            // A tile that is all open sea isn't written (Aerofly's own sea shows), and needs no imagery.
            var sea = this.seaFade == null ? null : this.seaFade(tile);
            if (sea != null && Array.TrueForAll(sea, f => f == 255))
            {
                return TileImage.Empty;
            }

            byte seaR = this.seaColour[0], seaG = this.seaColour[1], seaB = this.seaColour[2];
            var window = this.source.Load((int)(x0 >> 8), (int)(y0 >> 8), (int)(x1 >> 8), (int)(y1 >> 8));

            var image = new byte[Side * Side * 3];
            var alpha = new byte[Side * Side];
            int incomplete = 0, transparent = 0;
            Parallel.For(0, Side, j =>
            {
                double fy = v[j] - Math.Floor(v[j]);
                long sy0 = (long)Math.Floor(v[j]), sy1 = sy0 + 1;
                int o = j * Side * 3;
                int rowTransparent = 0;
                for (int i = 0; i < Side; i++, o += 3)
                {
                    int p = j * Side + i;
                    int fade = sea == null ? 0 : sea[p];
                    if (fade == 255)
                    {
                        // open sea in a tile with imagery: the solid sea colour, not transparent, as the stock tile
                        // may still show its own near-shore photo there (inside Provincetown's harbour)
                        image[o] = seaR;
                        image[o + 1] = seaG;
                        image[o + 2] = seaB;
                        alpha[p] = 255;
                        continue;
                    }

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

                        alpha[p] = 255;
                        if (fade != 0)
                        {
                            Blend(image, o, fade, seaR, seaG, seaB);
                        }
                    }
                    else
                    {
                        // At the edge of the download or of the imagery, a neighbour for the interpolation may be
                        // missing: take the nearest pixel. If that tile has no imagery the pixel is transparent; if it
                        // is missing the tile is incomplete.
                        long nx = (long)Math.Floor(u[i] + 0.5), ny = (long)Math.Floor(v[j] + 0.5);
                        var t = window.TileAt(nx, ny);
                        if (t != null)
                        {
                            int a = Offset(nx, ny);
                            image[o] = t[a];
                            image[o + 1] = t[a + 1];
                            image[o + 2] = t[a + 2];
                            alpha[p] = 255;
                            if (fade != 0)
                            {
                                Blend(image, o, fade, seaR, seaG, seaB);
                            }
                        }
                        else if (window.IsNoImagery(nx, ny))
                        {
                            if (fade != 0)
                            {
                                // no imagery out in the fade: the sea colour
                                image[o] = seaR;
                                image[o + 1] = seaG;
                                image[o + 2] = seaB;
                                alpha[p] = 255;
                            }
                            else
                            {
                                rowTransparent++;
                            }
                        }
                        else
                        {
                            Interlocked.Increment(ref incomplete);
                        }
                    }
                }

                Interlocked.Add(ref transparent, rowTransparent);
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

            if (transparent == Side * Side)
            {
                return TileImage.Empty;
            }

            // alpha is the coverage: 255 opaque, 0 transparent
            return new TileImage(image, transparent > 0 ? alpha : null);
        }

        // Blends a pixel toward the sea colour by fade / 254.
        private static void Blend(byte[] image, int o, int fade, byte r, byte g, byte b)
        {
            double w = fade / 254.0;
            image[o] = (byte)Math.Round(image[o] + (r - image[o]) * w);
            image[o + 1] = (byte)Math.Round(image[o + 1] + (g - image[o + 1]) * w);
            image[o + 2] = (byte)Math.Round(image[o + 2] + (b - image[o + 2]) * w);
        }

        // The byte offset of global pixel (x, y) within its source tile.
        private static int Offset(long x, long y)
        {
            return (int)(((y & 255) * WebMercator.TileSize + (x & 255)) * 3);
        }
    }
}
