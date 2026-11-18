using System;
using System.IO;

namespace AeroScenery.AFS4
{
    /// <summary>
    /// Writes Aerofly FS 4 elevation tiles (maph_LL_XXXX_YYYY.tth).
    ///
    /// A tth is a 0x100-byte header of u32 fields (the rest zero): magic 0x9fbf1, payload offset 0x100, level, payload
    /// size, 2113568 (uncompressed), -500, 9000 (the height range, m), 1028 (samples per side), 2 (border); then the
    /// payload, zlib as in the stock tiles (78 01): 1028 x 1028 little-endian uint16, rows from the south, height =
    /// -500 + v * 9500 / 65535 m. Sample (u, r) is at the tile's corner + (u - 2, r - 2) * span / 1024, so a tile
    /// reaches two samples past each edge. Format worked out in afsd (terrain_tile.h, tasks/todo.md).
    /// </summary>
    public static class TthWriter
    {
        public const int Side = 1028;
        public const int Border = 2;
        public const int Intervals = 1024;
        public const int HeaderSize = 0x100;
        public const uint Magic = 0x9fbf1;
        public const int MinHeight = -500;
        public const int MaxHeight = 9000;

        /// <summary>GeoConvert's quantisation: (h + 500) * 65535 / 9500, rounded and clamped.</summary>
        public static ushort Sample(double metres)
        {
            double v = Math.Round((metres - MinHeight) * 65535 / (MaxHeight - MinHeight));
            return (ushort)(v < 0 ? 0 : (v > 65535 ? 65535 : v));
        }

        public static double Metres(ushort sample)
        {
            return MinHeight + sample * (double)(MaxHeight - MinHeight) / 65535;
        }

        /// <summary>
        /// Writes a tth of the given level from 1028 x 1028 samples (rows from the south).
        /// </summary>
        public static void Write(string path, int level, ushort[] samples)
        {
            if (samples.Length != Side * Side)
            {
                throw new ArgumentException(String.Format("a tth takes {0} x {0} samples, not {1}", Side, samples.Length));
            }

            var raw = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, raw, 0, raw.Length); // little-endian on Windows
            var payload = TtcWriter.Zlib(raw);

            var file = new byte[HeaderSize + payload.Length];
            uint[] header = { Magic, HeaderSize, (uint)level, (uint)payload.Length, (uint)raw.Length, unchecked((uint)MinHeight), MaxHeight, Side, Border };
            for (int i = 0; i < header.Length; i++)
            {
                uint value = header[i];
                file[i * 4] = (byte)value;
                file[i * 4 + 1] = (byte)(value >> 8);
                file[i * 4 + 2] = (byte)(value >> 16);
                file[i * 4 + 3] = (byte)(value >> 24);
            }

            Buffer.BlockCopy(payload, 0, file, HeaderSize, payload.Length);

            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, file);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temporary, path);
        }
    }
}
