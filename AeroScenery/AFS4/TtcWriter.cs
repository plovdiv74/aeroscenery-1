using System;
using System.IO;
using System.IO.Compression;

namespace AeroScenery.AFS4
{
    /// <summary>
    /// Writes Aerofly FS 4 aerial image tiles (map_LL_XXXX_YYYY.ttc).
    ///
    /// A ttc is a 0x100-byte header of u32 fields (the rest zero), then the payload: the BC1 mip chain of a 2048x2048
    /// image, 12 levels, largest first (2796216 bytes), rows from the south. The header is the stock US tiles' (magic
    /// 0x303a, payload offset 0x100, 0, payload size, 2796216, 2048, 2048, 12, 0x12345672). The stock tiles' payload is
    /// LZHAM; the sim also reads zlib (78 01, raw deflate, big-endian Adler-32), as in its own tth elevation tiles, so
    /// that is what is written here (checked in the sim, 2026-11-17). Format worked out in afsd (terrain_tile.h).
    /// </summary>
    public static class TtcWriter
    {
        public const int Side = 2048;
        public const int Levels = 12;
        public const int HeaderSize = 0x100;
        public const uint Magic = 0x303a;
        public const uint VariantBc1 = 0x12345672;

        public static readonly int ChainSize = ComputeChainSize();

        private static int ComputeChainSize()
        {
            int size = 0;
            for (int level = 0; level < Levels; level++)
            {
                int side = Math.Max(1, Side >> level);
                size += Bc1Encoder.EncodedSize(side, side);
            }

            return size;
        }

        /// <summary>
        /// The BC1 mip chain of a 2048x2048 RGB image (3 bytes per pixel, rows from the south); each level is the 2x2
        /// box average of the one before.
        /// </summary>
        public static byte[] Bc1Chain(byte[] rgb)
        {
            return Bc1Chain(rgb, null);
        }

        /// <summary>
        /// The BC1 mip chain of a 2048x2048 RGB image with its coverage (one byte per pixel: 255 opaque, 0 transparent
        /// and without imagery, between: imagery shown with that probability; null if all opaque). Each level's
        /// coverage is dithered into BC1's one-bit alpha (Dither); transparent pixels use BC1's transparent index
        /// (blocks in 3-colour mode), which Aerofly shows its own imagery through. Mips average the coverage, and the
        /// colour of the pixels with imagery (AverageCovered).
        /// </summary>
        public static byte[] Bc1Chain(byte[] rgb, byte[] coverage)
        {
            if (rgb.Length != Side * Side * 3)
            {
                throw new ArgumentException(String.Format("a ttc takes a {0}x{0} RGB image, not {1} bytes", Side, rgb.Length));
            }

            if (coverage != null && coverage.Length != Side * Side)
            {
                throw new ArgumentException(String.Format("a ttc's coverage has {0}x{0} bytes, not {1}", Side, coverage.Length));
            }

            var chain = new byte[ChainSize];
            int offset = 0;
            var image = rgb;
            var mask = coverage;
            int side = Side;
            for (int level = 0; level < Levels; level++)
            {
                Bc1Encoder.Encode(image, mask == null ? null : Dither(mask, side), side, side, chain, offset);
                offset += Bc1Encoder.EncodedSize(side, side);
                if (level + 1 < Levels)
                {
                    if (mask == null)
                    {
                        image = HalveBox(image, side);
                    }
                    else
                    {
                        HalveBox(image, mask, side, out image, out mask);
                    }

                    side /= 2;
                }
            }

            return chain;
        }

        /// <summary>
        /// Writes a ttc from the 2048x2048 RGB image (3 bytes per pixel, rows from the south).
        /// </summary>
        public static void Write(string path, byte[] rgb)
        {
            WriteChain(path, Bc1Chain(rgb));
        }

        /// <summary>
        /// Writes a ttc from the 2048x2048 RGB image and its coverage (null if all opaque; see Bc1Chain).
        /// </summary>
        public static void Write(string path, byte[] rgb, byte[] coverage)
        {
            WriteChain(path, Bc1Chain(rgb, coverage));
        }

        /// <summary>
        /// One-bit alpha (0 or 255) from coverage: a pixel is opaque if its coverage exceeds a threshold between 1 and
        /// 253 from the R2 low-discrepancy sequence (frac(0.7549 x + 0.5698 y), a blue-noise-like pattern without
        /// the grid look of a Bayer matrix). 255 is always opaque, 0 always transparent, and a share of c / 255 of an
        /// area of coverage c is opaque.
        /// </summary>
        public static byte[] Dither(byte[] coverage, int side)
        {
            var alpha = new byte[side * side];
            for (int y = 0; y < side; y++)
            {
                int row = y * side;
                for (int x = 0; x < side; x++)
                {
                    int c = coverage[row + x];
                    if (c == 255)
                    {
                        alpha[row + x] = 255;
                    }
                    else if (c != 0)
                    {
                        double t = x * 0.75487766624669276 + y * 0.56984029099805327;
                        int threshold = 1 + (int)(253 * (t - Math.Floor(t)));
                        alpha[row + x] = c > threshold ? (byte)255 : (byte)0;
                    }
                }
            }

            return alpha;
        }

        public static void WriteChain(string path, byte[] bc1Chain)
        {
            if (bc1Chain.Length != ChainSize)
            {
                throw new ArgumentException(String.Format("a ttc takes a BC1 chain of {0} bytes, not {1}", ChainSize, bc1Chain.Length));
            }

            var payload = Zlib(bc1Chain);
            var file = new byte[HeaderSize + payload.Length];
            uint[] header = { Magic, HeaderSize, 0, (uint)payload.Length, (uint)bc1Chain.Length, Side, Side, Levels, VariantBc1 };
            for (int i = 0; i < header.Length; i++)
            {
                PutUInt32LittleEndian(file, i * 4, header[i]);
            }

            Buffer.BlockCopy(payload, 0, file, HeaderSize, payload.Length);

            // Write to a temporary file first, so a cancelled run never leaves a truncated tile behind.
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, file);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temporary, path);
        }

        /// <summary>
        /// A zlib stream (RFC 1950): 78 01, the raw deflate data, the Adler-32 of the input (big-endian).
        /// </summary>
        public static byte[] Zlib(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                output.WriteByte(0x78);
                output.WriteByte(0x01);
                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
                {
                    deflate.Write(data, 0, data.Length);
                }

                uint adler = Adler32(data);
                output.WriteByte((byte)(adler >> 24));
                output.WriteByte((byte)(adler >> 16));
                output.WriteByte((byte)(adler >> 8));
                output.WriteByte((byte)adler);
                return output.ToArray();
            }
        }

        public static uint Adler32(byte[] data)
        {
            const uint Mod = 65521;
            uint a = 1, b = 0;
            int i = 0;
            while (i < data.Length)
            {
                // 5552 is the most bytes that can be summed before b could overflow 32 bits.
                int end = Math.Min(data.Length, i + 5552);
                for (; i < end; i++)
                {
                    a += data[i];
                    b += a;
                }

                a %= Mod;
                b %= Mod;
            }

            return (b << 16) | a;
        }

        /// <summary>
        /// The 2x2 box average of a square RGB image of the given side.
        /// </summary>
        public static byte[] HalveBox(byte[] rgb, int side)
        {
            int half = side / 2;
            var output = new byte[half * half * 3];
            for (int y = 0; y < half; y++)
            {
                int row0 = 2 * y * side * 3, row1 = row0 + side * 3;
                int o = y * half * 3;
                for (int x = 0; x < half * 3; x += 3)
                {
                    int s = 2 * x;
                    for (int c = 0; c < 3; c++, o++)
                    {
                        output[o] = (byte)((rgb[row0 + s + c] + rgb[row0 + s + 3 + c] + rgb[row1 + s + c] + rgb[row1 + s + 3 + c] + 2) >> 2);
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// The 2x2 box average of a square RGB image with its coverage (AverageCovered per output pixel).
        /// </summary>
        public static void HalveBox(byte[] rgb, byte[] coverage, int side, out byte[] rgbOut, out byte[] coverageOut)
        {
            int half = side / 2;
            var outRgb = new byte[half * half * 3];
            var outCoverage = new byte[half * half];
            for (int y = 0; y < half; y++)
            {
                int row0 = 2 * y * side, row1 = row0 + side;
                for (int x = 0; x < half; x++)
                {
                    int s0 = row0 + 2 * x, s1 = row1 + 2 * x;
                    AverageCovered(rgb, coverage, s0, s0 + 1, s1, s1 + 1, outRgb, outCoverage, y * half + x);
                }
            }

            rgbOut = outRgb;
            coverageOut = outCoverage;
        }

        /// <summary>
        /// Sets pixel p of dst from four source pixels (pixel indices; coverage null = all 255): its coverage is their
        /// rounded mean, its colour the rounded mean of those with imagery (coverage above 0). With four fully covered
        /// pixels this is the plain box average.
        /// </summary>
        public static void AverageCovered(byte[] rgb, byte[] coverage, int i0, int i1, int i2, int i3, byte[] dstRgb, byte[] dstCoverage, int p)
        {
            int n = 0, r = 0, g = 0, b = 0, c = 0;
            AddCovered(rgb, coverage, i0, ref n, ref r, ref g, ref b, ref c);
            AddCovered(rgb, coverage, i1, ref n, ref r, ref g, ref b, ref c);
            AddCovered(rgb, coverage, i2, ref n, ref r, ref g, ref b, ref c);
            AddCovered(rgb, coverage, i3, ref n, ref r, ref g, ref b, ref c);

            int o = 3 * p;
            if (dstCoverage != null)
            {
                dstCoverage[p] = (byte)((c + 2) >> 2);
            }

            if (n == 0)
            {
                dstRgb[o] = dstRgb[o + 1] = dstRgb[o + 2] = 0;
                return;
            }

            dstRgb[o] = (byte)((r + n / 2) / n);
            dstRgb[o + 1] = (byte)((g + n / 2) / n);
            dstRgb[o + 2] = (byte)((b + n / 2) / n);
        }

        private static void AddCovered(byte[] rgb, byte[] coverage, int i, ref int n, ref int r, ref int g, ref int b, ref int c)
        {
            int ci = coverage == null ? 255 : coverage[i];
            if (ci != 0)
            {
                n++;
                c += ci;
                r += rgb[3 * i];
                g += rgb[3 * i + 1];
                b += rgb[3 * i + 2];
            }
        }

        private static void PutUInt32LittleEndian(byte[] buffer, int at, uint value)
        {
            buffer[at] = (byte)value;
            buffer[at + 1] = (byte)(value >> 8);
            buffer[at + 2] = (byte)(value >> 16);
            buffer[at + 3] = (byte)(value >> 24);
        }
    }
}
