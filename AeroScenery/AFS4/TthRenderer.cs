using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AeroScenery.AFS4
{
    public class TthRendererProgress
    {
        public int TilesDone { get; set; }
        public int TilesTotal { get; set; }
        public string Message { get; set; }
    }

    // An IProgress that calls its handler at once, on the reporting thread.
    internal class ForwardingProgress<T> : IProgress<T>
    {
        private readonly Action<T> handler;

        public ForwardingProgress(Action<T> handler)
        {
            this.handler = handler;
        }

        public void Report(T value)
        {
            this.handler(value);
        }
    }

    public class TthRendererResult
    {
        public List<string> Written { get; } = new List<string>();

        /// <summary>Tiles not written because no source had data for some of their samples.</summary>
        public List<string> Incomplete { get; } = new List<string>();
    }

    /// <summary>
    /// Writes the elevation tiles (tth) for an AFS grid square from an elevation source. A level coarser than the
    /// square gets the one tile containing it (a tth tile may reach beyond the square: elevation is cheap); a finer
    /// level gets every tile inside it. Each sample is the bilinear height at GeoConvert's sample position.
    /// </summary>
    public class TthRenderer
    {
        private readonly DemSet dem;
        private readonly string outputDirectory;

        public TthRenderer(DemSet dem, string outputDirectory)
        {
            this.dem = dem;
            this.outputDirectory = outputDirectory;
        }

        /// <summary>The tth tiles of the given levels for a square.</summary>
        public static List<AfsTile> TilesFor(AfsTile square, IEnumerable<int> levels)
        {
            var tiles = new List<AfsTile>();
            foreach (int level in levels.Distinct().OrderBy(l => l))
            {
                int span = 0x10000 >> level;
                if (level <= square.Level)
                {
                    tiles.Add(new AfsTile(level, square.X - square.X % span, square.Y - square.Y % span));
                }
                else
                {
                    for (int y = square.Y; y < square.Y + square.Span; y += span)
                    {
                        for (int x = square.X; x < square.X + square.Span; x += span)
                        {
                            tiles.Add(new AfsTile(level, x, y));
                        }
                    }
                }
            }

            return tiles;
        }

        /// <summary>The 1-degree cells the tiles' samples (and their bilinear neighbours) fall in.</summary>
        public static List<Tuple<int, int>> CellsFor(IEnumerable<AfsTile> tiles)
        {
            var cells = new HashSet<Tuple<int, int>>();
            foreach (var tile in tiles)
            {
                double reach = (double)Border * tile.Span / TthWriter.Intervals;
                double west = AfsGrid.Lon(tile.X - reach), east = AfsGrid.Lon(tile.X + tile.Span + reach);
                double south = AfsGrid.Lat(tile.Y - reach), north = AfsGrid.Lat(tile.Y + tile.Span + reach);
                foreach (var cell in DemSet.CellsCovering(west, south, east, north, 0.002))
                {
                    cells.Add(cell);
                }
            }

            return cells.OrderBy(c => c.Item1).ThenBy(c => c.Item2).ToList();
        }

        private const int Border = TthWriter.Border;

        public async Task<TthRendererResult> RenderAsync(AfsTile square, IEnumerable<int> levels, IProgress<TthRendererProgress> progress, CancellationToken cancellation)
        {
            var result = new TthRendererResult();
            var tiles = TilesFor(square, levels);
            if (tiles.Count == 0)
            {
                return result;
            }

            // Passed straight through: wrapping it in a second Progress<T> would post each message to the UI thread
            // twice, so a cell message could arrive after the final "Writing TTH Files".
            var report = new ForwardingProgress<string>(message =>
            {
                if (progress != null)
                {
                    progress.Report(new TthRendererProgress { TilesDone = 0, TilesTotal = tiles.Count, Message = message });
                }
            });
            await this.dem.PrepareAsync(CellsFor(tiles), report, cancellation);

            Directory.CreateDirectory(this.outputDirectory);
            int done = 0;
            foreach (var tile in tiles)
            {
                cancellation.ThrowIfCancellationRequested();
                var samples = await Task.Run(() => RenderTile(tile), cancellation);
                if (samples == null)
                {
                    result.Incomplete.Add(tile.Name("maph"));
                }
                else
                {
                    var path = Path.Combine(this.outputDirectory, tile.Name("maph") + ".tth");
                    TthWriter.Write(path, tile.Level, samples);
                    result.Written.Add(path);
                }

                done++;
                if (progress != null)
                {
                    progress.Report(new TthRendererProgress { TilesDone = done, TilesTotal = tiles.Count, Message = "Writing TTH Files" });
                }
            }

            return result;
        }

        // The tile's samples (rows from the south), or null if some sample has no height.
        public ushort[] RenderTile(AfsTile tile)
        {
            int side = TthWriter.Side;
            double step = (double)tile.Span / TthWriter.Intervals;
            var lon = new double[side];
            var lat = new double[side];
            for (int k = 0; k < side; k++)
            {
                lon[k] = AfsGrid.Lon(tile.X + (k - Border) * step);
                lat[k] = AfsGrid.Lat(tile.Y + (k - Border) * step);
            }

            var samples = new ushort[side * side];
            int missing = 0;
            Parallel.For(0, side, r =>
            {
                for (int u = 0; u < side; u++)
                {
                    var h = this.dem.Height(lon[u], lat[r]);
                    if (h == null)
                    {
                        Interlocked.Increment(ref missing);
                        continue;
                    }

                    samples[r * side + u] = TthWriter.Sample(h.Value);
                }
            });

            return missing > 0 ? null : samples;
        }
    }
}
