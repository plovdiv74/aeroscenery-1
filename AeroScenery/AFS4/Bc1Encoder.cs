using System;
using System.Threading.Tasks;

namespace AeroScenery.AFS4
{
    /// <summary>
    /// A BC1 (DXT1) encoder for opaque RGB images, always in 4-colour mode. Per block: endpoints from the principal axis
    /// of the block's colours, then refined twice by least squares on the chosen indices (as stb_dxt and squish's range
    /// fit do); single-colour blocks use the best endpoint pair from a table. The interpolated colours are taken as
    /// (2 a + b) / 3 rounded, as GPUs and DirectXTex decode them (truncating left the tiles 0.2 darker after decoding).
    /// </summary>
    public static class Bc1Encoder
    {
        public const int BlockBytes = 8;

        // For each 8-bit value, the 5- and 6-bit endpoint pair (a, b) whose 2/3 a + 1/3 b colour (index 2) is closest.
        private static readonly byte[,] Solid5 = SolidTable(5);
        private static readonly byte[,] Solid6 = SolidTable(6);

        public static int EncodedSize(int width, int height)
        {
            return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * BlockBytes;
        }

        /// <summary>
        /// Encodes an RGB image (3 bytes per pixel, rows packed, the first row at the top of the texture) into
        /// output[offset..]. Images smaller than a block (2x2, 1x1) repeat their edge pixels.
        /// </summary>
        public static void Encode(byte[] rgb, int width, int height, byte[] output, int offset)
        {
            Encode(rgb, null, width, height, output, offset);
        }

        /// <summary>
        /// As above, with a transparency mask (one byte per pixel, 0 transparent; null if all opaque). A block with
        /// transparent pixels is encoded in BC1's 3-colour mode (c0 &lt;= c1: the two endpoint colours, their
        /// midpoint, and index 3 = transparent); fully opaque blocks are encoded exactly as without a mask.
        /// </summary>
        public static void Encode(byte[] rgb, byte[] alpha, int width, int height, byte[] output, int offset)
        {
            int blocksWide = Math.Max(1, (width + 3) / 4);
            int blocksHigh = Math.Max(1, (height + 3) / 4);

            Parallel.For(0, blocksHigh, by =>
            {
                var block = new int[48];
                var opaque = new bool[16];
                var indexScratch = new int[16];
                for (int bx = 0; bx < blocksWide; bx++)
                {
                    int opaqueCount = 0;
                    for (int py = 0; py < 4; py++)
                    {
                        int y = Math.Min(by * 4 + py, height - 1);
                        for (int px = 0; px < 4; px++)
                        {
                            int x = Math.Min(bx * 4 + px, width - 1);
                            int s = (y * width + x) * 3;
                            int d = (py * 4 + px) * 3;
                            block[d] = rgb[s];
                            block[d + 1] = rgb[s + 1];
                            block[d + 2] = rgb[s + 2];
                            opaque[py * 4 + px] = alpha == null || alpha[y * width + x] != 0;
                            opaqueCount += opaque[py * 4 + px] ? 1 : 0;
                        }
                    }

                    int at = offset + (by * blocksWide + bx) * BlockBytes;
                    if (opaqueCount == 16)
                    {
                        EncodeBlock(block, indexScratch, output, at);
                    }
                    else if (opaqueCount == 0)
                    {
                        // c0 = c1 = 0 (3-colour mode), every index 3: all transparent.
                        for (int k = 0; k < 4; k++)
                        {
                            output[at + k] = 0;
                            output[at + 4 + k] = 0xFF;
                        }
                    }
                    else
                    {
                        EncodeTransparentBlock(block, opaque, indexScratch, output, at);
                    }
                }
            });
        }

        // Index i's position between endpoint 0 and endpoint 1 in 3-colour mode (index 3 is transparent).
        private static readonly double[] IndexWeight3 = { 0, 1, 0.5 };

        // A block with some transparent pixels, in 3-colour mode: endpoints from the principal axis of the opaque
        // pixels, refined twice by least squares; opaque pixels take the nearest of the three colours, the others 3.
        private static void EncodeTransparentBlock(int[] px, bool[] opaque, int[] indices, byte[] output, int at)
        {
            double r0, g0, b0, r1, g1, b1;
            AxisEndpoints(px, opaque, out r0, out g0, out b0, out r1, out g1, out b1);
            int c0 = Pack565(r0, g0, b0), c1 = Pack565(r1, g1, b1);
            long error = ChooseIndices3(px, opaque, c0, c1, indices);

            var trial = new int[16];
            for (int iteration = 0; iteration < 2; iteration++)
            {
                double aa = 0, ab = 0, bb = 0, axr = 0, axg = 0, axb = 0, bxr = 0, bxg = 0, bxb = 0;
                for (int k = 0; k < 16; k++)
                {
                    if (!opaque[k])
                    {
                        continue;
                    }

                    double t = IndexWeight3[indices[k]], a = 1 - t;
                    aa += a * a; ab += a * t; bb += t * t;
                    axr += a * px[k * 3]; axg += a * px[k * 3 + 1]; axb += a * px[k * 3 + 2];
                    bxr += t * px[k * 3]; bxg += t * px[k * 3 + 1]; bxb += t * px[k * 3 + 2];
                }

                double det = aa * bb - ab * ab;
                if (Math.Abs(det) < 1e-9)
                {
                    break;
                }

                int n0 = Pack565((bb * axr - ab * bxr) / det, (bb * axg - ab * bxg) / det, (bb * axb - ab * bxb) / det);
                int n1 = Pack565((aa * bxr - ab * axr) / det, (aa * bxg - ab * axg) / det, (aa * bxb - ab * axb) / det);
                if (n0 == c0 && n1 == c1)
                {
                    break;
                }

                long trialError = ChooseIndices3(px, opaque, n0, n1, trial);
                if (trialError >= error)
                {
                    break;
                }

                c0 = n0; c1 = n1; error = trialError;
                Array.Copy(trial, indices, 16);
            }

            // 3-colour mode needs c0 <= c1; swapping the endpoints swaps indices 0 <-> 1 (the midpoint stays).
            bool swap = c0 > c1;
            if (swap)
            {
                int t = c0; c0 = c1; c1 = t;
            }

            uint bits = 0;
            for (int i = 15; i >= 0; i--)
            {
                int index = !opaque[i] ? 3 : (swap && indices[i] < 2 ? indices[i] ^ 1 : indices[i]);
                bits = (bits << 2) | (uint)index;
            }

            output[at] = (byte)c0;
            output[at + 1] = (byte)(c0 >> 8);
            output[at + 2] = (byte)c1;
            output[at + 3] = (byte)(c1 >> 8);
            output[at + 4] = (byte)bits;
            output[at + 5] = (byte)(bits >> 8);
            output[at + 6] = (byte)(bits >> 16);
            output[at + 7] = (byte)(bits >> 24);
        }

        // The extreme colours along the principal axis of the selected pixels of a block.
        private static void AxisEndpoints(int[] px, bool[] use, out double r0, out double g0, out double b0,
            out double r1, out double g1, out double b1)
        {
            double n = 0, mr = 0, mg = 0, mb = 0;
            for (int k = 0; k < 16; k++)
            {
                if (use[k]) { n++; mr += px[k * 3]; mg += px[k * 3 + 1]; mb += px[k * 3 + 2]; }
            }

            mr /= n; mg /= n; mb /= n;
            double crr = 0, crg = 0, crb = 0, cgg = 0, cgb = 0, cbb = 0;
            for (int k = 0; k < 16; k++)
            {
                if (!use[k]) continue;
                double r = px[k * 3] - mr, g = px[k * 3 + 1] - mg, b = px[k * 3 + 2] - mb;
                crr += r * r; crg += r * g; crb += r * b; cgg += g * g; cgb += g * b; cbb += b * b;
            }

            double ar, ag, ab;
            if (crr >= cgg && crr >= cbb) { ar = crr; ag = crg; ab = crb; }
            else if (cgg >= cbb) { ar = crg; ag = cgg; ab = cgb; }
            else { ar = crb; ag = cgb; ab = cbb; }
            for (int iteration = 0; iteration < 8; iteration++)
            {
                double nr = crr * ar + crg * ag + crb * ab, ng = crg * ar + cgg * ag + cgb * ab, nb = crb * ar + cgb * ag + cbb * ab;
                double norm = Math.Max(Math.Abs(nr), Math.Max(Math.Abs(ng), Math.Abs(nb)));
                if (norm < 1e-12) break;
                ar = nr / norm; ag = ng / norm; ab = nb / norm;
            }

            if (Math.Abs(ar) + Math.Abs(ag) + Math.Abs(ab) < 1e-12)
            {
                ar = ag = ab = 1;
            }

            double lo = Double.MaxValue, hi = Double.MinValue;
            int iLo = 0, iHi = 0;
            for (int k = 0; k < 16; k++)
            {
                if (!use[k]) continue;
                double p = px[k * 3] * ar + px[k * 3 + 1] * ag + px[k * 3 + 2] * ab;
                if (p < lo) { lo = p; iLo = k * 3; }
                if (p > hi) { hi = p; iHi = k * 3; }
            }

            r0 = px[iLo]; g0 = px[iLo + 1]; b0 = px[iLo + 2];
            r1 = px[iHi]; g1 = px[iHi + 1]; b1 = px[iHi + 2];
        }

        // Nearest of the three 3-colour-mode colours for each opaque pixel (transparent ones get 0 here, 3 when written);
        // returns the squared error of the opaque pixels.
        private static long ChooseIndices3(int[] px, bool[] opaque, int c0, int c1, int[] indices)
        {
            var palette = new int[9];
            Unpack565(c0, palette, 0);
            Unpack565(c1, palette, 3);
            for (int c = 0; c < 3; c++)
            {
                palette[6 + c] = (palette[c] + palette[3 + c] + 1) / 2;
            }

            long total = 0;
            for (int k = 0; k < 16; k++)
            {
                if (!opaque[k])
                {
                    indices[k] = 0;
                    continue;
                }

                int best = 0, bestError = Int32.MaxValue;
                for (int i = 0; i < 3; i++)
                {
                    int dr = px[k * 3] - palette[i * 3], dg = px[k * 3 + 1] - palette[i * 3 + 1], db = px[k * 3 + 2] - palette[i * 3 + 2];
                    int e = dr * dr + dg * dg + db * db;
                    if (e < bestError) { bestError = e; best = i; }
                }

                indices[k] = best;
                total += bestError;
            }

            return total;
        }

        private static void EncodeBlock(int[] px, int[] indices, byte[] output, int at)
        {
            int c0, c1;
            uint bits;

            bool solid = true;
            for (int i = 3; i < 48 && solid; i += 3)
            {
                solid = px[i] == px[0] && px[i + 1] == px[1] && px[i + 2] == px[2];
            }

            if (solid)
            {
                c0 = (Solid5[px[0], 0] << 11) | (Solid6[px[1], 0] << 5) | Solid5[px[2], 0];
                c1 = (Solid5[px[0], 1] << 11) | (Solid6[px[1], 1] << 5) | Solid5[px[2], 1];
                for (int i = 0; i < 16; i++)
                {
                    indices[i] = 2;
                }
            }
            else
            {
                double r0, g0, b0, r1, g1, b1;
                PrincipalAxisEndpoints(px, out r0, out g0, out b0, out r1, out g1, out b1);
                c0 = Pack565(r0, g0, b0);
                c1 = Pack565(r1, g1, b1);
                long error = ChooseIndices(px, c0, c1, indices);

                var trial = new int[16];
                for (int iteration = 0; iteration < 2; iteration++)
                {
                    if (!LeastSquaresEndpoints(px, indices, out r0, out g0, out b0, out r1, out g1, out b1))
                    {
                        break;
                    }

                    int n0 = Pack565(r0, g0, b0), n1 = Pack565(r1, g1, b1);
                    if (n0 == c0 && n1 == c1)
                    {
                        break;
                    }

                    long trialError = ChooseIndices(px, n0, n1, trial);
                    if (trialError >= error)
                    {
                        break;
                    }

                    c0 = n0;
                    c1 = n1;
                    error = trialError;
                    Array.Copy(trial, indices, 16);
                }
            }

            // 4-colour mode needs c0 > c1; swapping the endpoints swaps indices 0 <-> 1 and 2 <-> 3.
            bool swap = c0 < c1;
            if (swap)
            {
                int t = c0;
                c0 = c1;
                c1 = t;
            }

            bits = 0;
            for (int i = 15; i >= 0; i--)
            {
                int index = c0 == c1 ? 0 : (swap ? indices[i] ^ 1 : indices[i]);
                bits = (bits << 2) | (uint)index;
            }

            output[at] = (byte)c0;
            output[at + 1] = (byte)(c0 >> 8);
            output[at + 2] = (byte)c1;
            output[at + 3] = (byte)(c1 >> 8);
            output[at + 4] = (byte)bits;
            output[at + 5] = (byte)(bits >> 8);
            output[at + 6] = (byte)(bits >> 16);
            output[at + 7] = (byte)(bits >> 24);
        }

        // The block's extreme colours along the principal axis of its colour distribution.
        private static void PrincipalAxisEndpoints(int[] px, out double r0, out double g0, out double b0,
            out double r1, out double g1, out double b1)
        {
            double mr = 0, mg = 0, mb = 0;
            for (int i = 0; i < 48; i += 3)
            {
                mr += px[i];
                mg += px[i + 1];
                mb += px[i + 2];
            }

            mr /= 16;
            mg /= 16;
            mb /= 16;

            double crr = 0, crg = 0, crb = 0, cgg = 0, cgb = 0, cbb = 0;
            for (int i = 0; i < 48; i += 3)
            {
                double r = px[i] - mr, g = px[i + 1] - mg, b = px[i + 2] - mb;
                crr += r * r;
                crg += r * g;
                crb += r * b;
                cgg += g * g;
                cgb += g * b;
                cbb += b * b;
            }

            // Power iteration, starting from the axis of largest spread.
            double ar = 1, ag = 1, ab = 1;
            if (crr >= cgg && crr >= cbb) { ar = crr; ag = crg; ab = crb; }
            else if (cgg >= cbb) { ar = crg; ag = cgg; ab = cgb; }
            else { ar = crb; ag = cgb; ab = cbb; }

            for (int iteration = 0; iteration < 8; iteration++)
            {
                double nr = crr * ar + crg * ag + crb * ab;
                double ng = crg * ar + cgg * ag + cgb * ab;
                double nb = crb * ar + cgb * ag + cbb * ab;
                double norm = Math.Max(Math.Abs(nr), Math.Max(Math.Abs(ng), Math.Abs(nb)));
                if (norm < 1e-12)
                {
                    break;
                }

                ar = nr / norm;
                ag = ng / norm;
                ab = nb / norm;
            }

            if (Math.Abs(ar) + Math.Abs(ag) + Math.Abs(ab) < 1e-12)
            {
                ar = ag = ab = 1;
            }

            double lo = Double.MaxValue, hi = Double.MinValue;
            int iLo = 0, iHi = 0;
            for (int i = 0; i < 48; i += 3)
            {
                double p = px[i] * ar + px[i + 1] * ag + px[i + 2] * ab;
                if (p < lo) { lo = p; iLo = i; }
                if (p > hi) { hi = p; iHi = i; }
            }

            r0 = px[iHi]; g0 = px[iHi + 1]; b0 = px[iHi + 2];
            r1 = px[iLo]; g1 = px[iLo + 1]; b1 = px[iLo + 2];
        }

        // Index i's position between endpoint 0 and endpoint 1 (4-colour mode).
        private static readonly double[] IndexWeight = { 0, 1, 1.0 / 3, 2.0 / 3 };

        // Endpoints that minimise the squared error for the given indices; false if the indices don't determine them.
        private static bool LeastSquaresEndpoints(int[] px, int[] indices, out double r0, out double g0, out double b0,
            out double r1, out double g1, out double b1)
        {
            double aa = 0, ab = 0, bb = 0;
            double axr = 0, axg = 0, axb = 0, bxr = 0, bxg = 0, bxb = 0;
            for (int k = 0; k < 16; k++)
            {
                double t = IndexWeight[indices[k]], a = 1 - t;
                aa += a * a;
                ab += a * t;
                bb += t * t;
                axr += a * px[k * 3];
                axg += a * px[k * 3 + 1];
                axb += a * px[k * 3 + 2];
                bxr += t * px[k * 3];
                bxg += t * px[k * 3 + 1];
                bxb += t * px[k * 3 + 2];
            }

            double det = aa * bb - ab * ab;
            if (Math.Abs(det) < 1e-9)
            {
                r0 = g0 = b0 = r1 = g1 = b1 = 0;
                return false;
            }

            r0 = (bb * axr - ab * bxr) / det;
            g0 = (bb * axg - ab * bxg) / det;
            b0 = (bb * axb - ab * bxb) / det;
            r1 = (aa * bxr - ab * axr) / det;
            g1 = (aa * bxg - ab * axg) / det;
            b1 = (aa * bxb - ab * axb) / det;
            return true;
        }

        // Picks the nearest of the four palette colours for each pixel; returns the block's squared error.
        private static long ChooseIndices(int[] px, int c0, int c1, int[] indices)
        {
            var palette = new int[12];
            Unpack565(c0, palette, 0);
            Unpack565(c1, palette, 3);
            for (int c = 0; c < 3; c++)
            {
                palette[6 + c] = (2 * palette[c] + palette[3 + c] + 1) / 3;
                palette[9 + c] = (palette[c] + 2 * palette[3 + c] + 1) / 3;
            }

            long total = 0;
            for (int k = 0; k < 16; k++)
            {
                int best = 0, bestError = Int32.MaxValue;
                for (int i = 0; i < 4; i++)
                {
                    int dr = px[k * 3] - palette[i * 3];
                    int dg = px[k * 3 + 1] - palette[i * 3 + 1];
                    int db = px[k * 3 + 2] - palette[i * 3 + 2];
                    int e = dr * dr + dg * dg + db * db;
                    if (e < bestError)
                    {
                        bestError = e;
                        best = i;
                    }
                }

                indices[k] = best;
                total += bestError;
            }

            return total;
        }

        private static int Quantize(double v, int bits)
        {
            int max = (1 << bits) - 1;
            int q = (int)Math.Round(v * max / 255);
            return q < 0 ? 0 : (q > max ? max : q);
        }

        private static int Pack565(double r, double g, double b)
        {
            return (Quantize(r, 5) << 11) | (Quantize(g, 6) << 5) | Quantize(b, 5);
        }

        private static int Expand(int v, int bits)
        {
            return bits == 5 ? (v << 3) | (v >> 2) : (v << 2) | (v >> 4);
        }

        private static void Unpack565(int c, int[] rgb, int at)
        {
            rgb[at] = Expand((c >> 11) & 31, 5);
            rgb[at + 1] = Expand((c >> 5) & 63, 6);
            rgb[at + 2] = Expand(c & 31, 5);
        }

        private static byte[,] SolidTable(int bits)
        {
            int count = 1 << bits;
            var table = new byte[256, 2];
            for (int v = 0; v < 256; v++)
            {
                int bestError = Int32.MaxValue;
                for (int a = 0; a < count; a++)
                {
                    for (int b = 0; b < count; b++)
                    {
                        int e = Math.Abs((2 * Expand(a, bits) + Expand(b, bits) + 1) / 3 - v);
                        if (e < bestError)
                        {
                            bestError = e;
                            table[v, 0] = (byte)a;
                            table[v, 1] = (byte)b;
                        }
                    }
                }
            }

            return table;
        }
    }
}
