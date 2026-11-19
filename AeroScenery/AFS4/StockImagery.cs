using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AeroScenery.AFS4
{
    /// <summary>
    /// Aerofly's stock imagery under a grid square, blurred: the colours the sim shows through transparent pixels. It
    /// comes from afsd ("afsd -ts &lt;square&gt; &lt;folder&gt;"), which writes &lt;square&gt;_stock.png: north up, covering the
    /// square on its own grid, each pixel from the finest stock tile there. The renderer blends the offshore strip
    /// toward it (TtcRenderer), so the imagery meets the stock water without a visible edge.
    /// </summary>
    public class StockImagery
    {
        public const string FileSuffix = "_stock.png";

        private readonly AfsTile square;
        private readonly int side;
        private readonly float[] sharp; // rows from the south
        private readonly float[] blurred;

        private StockImagery(AfsTile square, int side, float[] sharp, float[] blurred)
        {
            this.square = square;
            this.side = side;
            this.sharp = sharp;
            this.blurred = blurred;
        }

        /// <summary>
        /// The file afsd writes for a square into a folder.
        /// </summary>
        public static string PathFor(string folder, AfsTile square)
        {
            return Path.Combine(folder, square.Name() + FileSuffix);
        }

        /// <summary>
        /// Loads a square's stock image and blurs it over about blurMetres (two box blurs, close to a Gaussian), so the
        /// stock imagery's texture doesn't show in the blend, only its colour.
        /// </summary>
        public static StockImagery Load(string path, AfsTile square, double blurMetres)
        {
            int side;
            float[] rgb;
            using (var bitmap = new Bitmap(path))
            {
                if (bitmap.Width != bitmap.Height)
                {
                    throw new InvalidDataException(String.Format("{0} is {1}x{2}, not square", path, bitmap.Width, bitmap.Height));
                }

                side = bitmap.Width;
                rgb = new float[side * side * 3];
                var data = bitmap.LockBits(new Rectangle(0, 0, side, side), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    var row = new byte[side * 3];
                    for (int y = 0; y < side; y++)
                    {
                        Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                        int o = (side - 1 - y) * side * 3; // north up in the file, rows from the south here
                        for (int x = 0; x < side; x++)
                        {
                            rgb[o + 3 * x] = row[3 * x + 2]; // BGR
                            rgb[o + 3 * x + 1] = row[3 * x + 1];
                            rgb[o + 3 * x + 2] = row[3 * x];
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }

            double metresPerPixel = (square.EastLongitude - square.WestLongitude) * 111319.49
                * Math.Cos((square.SouthLatitude + square.NorthLatitude) / 2 * Math.PI / 180) / side;
            int radius = Math.Max(1, (int)Math.Round(blurMetres / metresPerPixel / 2));
            var blurred = rgb;
            for (int pass = 0; pass < 2; pass++)
            {
                blurred = BoxBlur(blurred, side, radius, true);
                blurred = BoxBlur(blurred, side, radius, false);
            }

            return new StockImagery(square, side, rgb, blurred);
        }

        // A box blur of the given radius along rows (horizontal) or columns, with the edges clamped.
        private static float[] BoxBlur(float[] input, int side, int radius, bool horizontal)
        {
            var output = new float[input.Length];
            Parallel.For(0, side, line =>
            {
                for (int c = 0; c < 3; c++)
                {
                    double sum = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        sum += input[Index(line, Clamp(k, side), horizontal, side) + c];
                    }

                    for (int k = 0; k < side; k++)
                    {
                        output[Index(line, k, horizontal, side) + c] = (float)(sum / (2 * radius + 1));
                        sum += input[Index(line, Clamp(k + radius + 1, side), horizontal, side) + c]
                            - input[Index(line, Clamp(k - radius, side), horizontal, side) + c];
                    }
                }
            });

            return output;
        }

        private static int Index(int line, int k, bool horizontal, int side)
        {
            return 3 * (horizontal ? line * side + k : k * side + line);
        }

        private static int Clamp(int k, int side)
        {
            return k < 0 ? 0 : k >= side ? side - 1 : k;
        }

        /// <summary>
        /// The stock colour at a level-16 grid position (bilinear, clamped to the square): the blurred image's mixed
        /// with the sharp image's by sharpness (0: blurred, 1: sharp, as the sim draws the stock tile).
        /// </summary>
        public void Sample(double x, double y, double sharpness, out double r, out double g, out double b)
        {
            double u = (x - this.square.X) / this.square.Span * this.side - 0.5;
            double v = (y - this.square.Y) / this.square.Span * this.side - 0.5;
            u = Math.Max(0, Math.Min(this.side - 1, u));
            v = Math.Max(0, Math.Min(this.side - 1, v));
            int i0 = Math.Min(this.side - 2, (int)u), j0 = Math.Min(this.side - 2, (int)v);
            double fu = u - i0, fv = v - j0;
            int a = 3 * (j0 * this.side + i0), c = a + 3 * this.side;
            r = Mix(a, c, 0, fu, fv, sharpness);
            g = Mix(a, c, 1, fu, fv, sharpness);
            b = Mix(a, c, 2, fu, fv, sharpness);
        }

        private double Mix(int a, int c, int k, double fu, double fv, double sharpness)
        {
            double soft = Lerp2(this.blurred, a, c, k, fu, fv);
            return sharpness <= 0 ? soft : soft + (Lerp2(this.sharp, a, c, k, fu, fv) - soft) * sharpness;
        }

        private static double Lerp2(float[] rgb, int a, int c, int k, double fu, double fv)
        {
            double bottom = rgb[a + k] + (rgb[a + 3 + k] - rgb[a + k]) * fu;
            double top = rgb[c + k] + (rgb[c + 3 + k] - rgb[c + k]) * fu;
            return bottom + (top - bottom) * fv;
        }
    }
}
