using System;

namespace AeroScenery.AFS4
{
    /// <summary>
    /// Tone curves for the ttc writer, as 256-entry lookup tables applied to each channel of the source pixels.
    /// </summary>
    public static class ToneCurves
    {
        /// <summary>
        /// The brightening GeoConvert (aerofly_fs_2_geoconvert 1.5.17) applies to aerial images. Measured 2026-11-18 from
        /// its raw output for 256 constant grey blocks: the same table for R, G and B, each channel independent of the
        /// others, the same with or without the PNG's sRGB/gAMA chunks, and the same as on Bing imagery (no auto levels).
        /// No simple formula fits it (a power or 1-(1-x)^p is off by 17 or more), so it is kept as measured.
        /// </summary>
        public static readonly byte[] GeoConvert =
        {
            0, 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 22, 24, 26, 28,
            30, 31, 32, 35, 36, 38, 40, 42, 43, 44, 47, 48, 50, 52, 52, 55,
            56, 58, 60, 60, 63, 64, 65, 67, 69, 70, 72, 73, 75, 76, 77, 79,
            80, 81, 83, 85, 86, 87, 89, 89, 91, 93, 94, 95, 97, 97, 99, 101,
            102, 103, 104, 105, 107, 108, 109, 111, 112, 113, 113, 115, 117, 118, 119, 120,
            121, 121, 124, 125, 126, 127, 128, 129, 130, 131, 133, 134, 135, 136, 137, 138,
            139, 140, 141, 142, 143, 144, 145, 146, 147, 147, 149, 150, 151, 152, 153, 154,
            155, 156, 157, 158, 159, 160, 161, 162, 163, 163, 165, 165, 166, 167, 168, 169,
            170, 171, 172, 173, 173, 174, 175, 176, 177, 178, 179, 179, 179, 181, 182, 183,
            184, 184, 185, 186, 187, 188, 188, 189, 190, 191, 192, 192, 193, 194, 195, 195,
            195, 197, 198, 198, 199, 200, 201, 201, 202, 203, 204, 204, 205, 206, 206, 207,
            208, 208, 209, 210, 211, 211, 211, 213, 213, 214, 215, 215, 216, 217, 217, 218,
            219, 219, 220, 221, 221, 222, 222, 223, 224, 224, 225, 226, 226, 227, 227, 227,
            229, 229, 230, 230, 231, 232, 232, 233, 233, 234, 235, 235, 236, 236, 237, 237,
            238, 239, 239, 240, 240, 241, 241, 242, 242, 243, 243, 243, 245, 245, 246, 246,
            247, 247, 248, 248, 249, 249, 250, 250, 251, 251, 252, 252, 253, 253, 254, 255
        };

        /// <summary>
        /// Applies a tone curve in place to RGB (or any 8-bit channel) data.
        /// </summary>
        public static void Apply(byte[] curve, byte[] pixels)
        {
            if (curve.Length != 256)
            {
                throw new ArgumentException("a tone curve has 256 entries");
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = curve[pixels[i]];
            }
        }
    }
}
