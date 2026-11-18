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
            if (rgb.Length != Side * Side * 3)
            {
                throw new ArgumentException(String.Format("a ttc takes a {0}x{0} RGB image, not {1} bytes", Side, rgb.Length));
            }

            var chain = new byte[ChainSize];
            int offset = 0;
            var image = rgb;
            int side = Side;
            for (int level = 0; level < Levels; level++)
            {
                Bc1Encoder.Encode(image, side, side, chain, offset);
                offset += Bc1Encoder.EncodedSize(side, side);
                if (level + 1 < Levels)
                {
                    image = HalveBox(image, side);
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

        private static void PutUInt32LittleEndian(byte[] buffer, int at, uint value)
        {
            buffer[at] = (byte)value;
            buffer[at + 1] = (byte)(value >> 8);
            buffer[at + 2] = (byte)(value >> 16);
            buffer[at + 3] = (byte)(value >> 24);
        }
    }
}
