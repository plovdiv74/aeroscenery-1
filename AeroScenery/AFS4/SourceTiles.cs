using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AeroScenery.AFS4
{
    /// <summary>
    /// The downloaded Web Mercator tiles of one zoom level, as AeroScenery names them:
    /// &lt;directory&gt;\&lt;prefix&gt;_&lt;zoom&gt;_&lt;tileX&gt;_&lt;tileY&gt;.jpg (or .png).
    /// </summary>
    public class SourceTiles
    {
        private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png" };

        public string Directory { get; private set; }
        public string Prefix { get; private set; }
        public int Zoom { get; private set; }

        public SourceTiles(string directory, string prefix, int zoom)
        {
            this.Directory = directory;
            this.Prefix = prefix;
            this.Zoom = zoom;
        }

        public string PathOf(int tileX, int tileY)
        {
            var stem = Path.Combine(this.Directory, String.Format("{0}_{1}_{2}_{3}", this.Prefix, this.Zoom, tileX, tileY));
            foreach (var extension in Extensions)
            {
                if (File.Exists(stem + extension))
                {
                    return stem + extension;
                }
            }

            return null;
        }

        /// <summary>
        /// Loads the tiles tileX0..tileX1, tileY0..tileY1 (inclusive) in parallel; a missing or unreadable tile is null (and counts as missing).
        /// </summary>
        public Window Load(int tileX0, int tileY0, int tileX1, int tileY1)
        {
            var window = new Window(tileX0, tileY0, tileX1 - tileX0 + 1, tileY1 - tileY0 + 1);
            Parallel.For(0, window.Columns * window.Rows, i =>
            {
                int tileX = tileX0 + i % window.Columns, tileY = tileY0 + i / window.Columns;
                var path = PathOf(tileX, tileY);
                window.Pixels[i] = path == null ? null : Decode(path);
            });

            return window;
        }

        // A 256x256 tile as RGB bytes, rows from the north; null if it isn't a 256x256 image.
        private static byte[] Decode(string path)
        {
            try
            {
                using (var bitmap = new Bitmap(path))
                {
                    if (bitmap.Width != WebMercator.TileSize || bitmap.Height != WebMercator.TileSize)
                    {
                        return null;
                    }

                    var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        const int RowBytes = WebMercator.TileSize * 3;
                        var rgb = new byte[RowBytes * WebMercator.TileSize];
                        var row = new byte[data.Stride];
                        for (int y = 0; y < WebMercator.TileSize; y++)
                        {
                            Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, data.Stride);
                            for (int x = 0; x < RowBytes; x += 3)
                            {
                                // GDI+ stores BGR.
                                rgb[y * RowBytes + x] = row[x + 2];
                                rgb[y * RowBytes + x + 1] = row[x + 1];
                                rgb[y * RowBytes + x + 2] = row[x];
                            }
                        }

                        return rgb;
                    }
                    finally
                    {
                        bitmap.UnlockBits(data);
                    }
                }
            }
            catch (ArgumentException)
            {
                // GDI+ reports a corrupt image file as an ArgumentException.
                return null;
            }
            catch (OutOfMemoryException)
            {
                // ... or, for some formats, as an OutOfMemoryException.
                return null;
            }
        }

        /// <summary>
        /// A rectangle of loaded tiles, addressed by global pixel coordinates at the tiles' zoom.
        /// </summary>
        public class Window
        {
            public readonly int TileX0, TileY0, Columns, Rows;
            public readonly byte[][] Pixels; // per tile, row-major from (TileX0, TileY0); null where missing

            public Window(int tileX0, int tileY0, int columns, int rows)
            {
                this.TileX0 = tileX0;
                this.TileY0 = tileY0;
                this.Columns = columns;
                this.Rows = rows;
                this.Pixels = new byte[columns * rows][];
            }

            /// <summary>
            /// The tile holding global pixel (x, y), or null if it is outside the window or missing.
            /// </summary>
            public byte[] TileAt(long x, long y)
            {
                long column = (x >> 8) - this.TileX0, row = (y >> 8) - this.TileY0;
                if (column < 0 || row < 0 || column >= this.Columns || row >= this.Rows)
                {
                    return null;
                }

                return this.Pixels[row * this.Columns + column];
            }

            public IEnumerable<string> MissingTiles(SourceTiles tiles)
            {
                for (int i = 0; i < this.Pixels.Length; i++)
                {
                    if (this.Pixels[i] == null)
                    {
                        yield return String.Format("{0}_{1}_{2}_{3}", tiles.Prefix, tiles.Zoom, this.TileX0 + i % this.Columns, this.TileY0 + i / this.Columns);
                    }
                }
            }
        }
    }
}
