using System;
using System.Globalization;

namespace AeroScenery.AFS4
{
    /// <summary>
    /// The Aerofly FS 4 world grid. Positions are in level-16 grid units: x and y run 0..65536 from the
    /// south-west, x linear in longitude, y a tangent stretch of latitude:
    ///   x = 65536 (0.5 + lon / 360)
    ///   y = 65536 (0.5 + 0.5 tan(k lat / 180) / k),  k = 2.3311223704144
    /// A tile of level L spans 0x10000 >> L grid units each way.
    /// </summary>
    public static class AfsGrid
    {
        public const double K = 2.3311223704144;

        public static double X(double lon)
        {
            return 65536 * (0.5 + lon / 360);
        }

        public static double Y(double lat)
        {
            return 65536 * (0.5 + 0.5 * Math.Tan(K * lat / 180) / K);
        }

        public static double Lon(double x)
        {
            return (x / 65536 - 0.5) * 360;
        }

        public static double Lat(double y)
        {
            return 180 * Math.Atan(K * (2 * y / 65536 - 1)) / K;
        }
    }

    /// <summary>
    /// A terrain tile, named map_LL_XXXX_YYYY: LL the level, XXXX_YYYY its south-west corner in level-16 grid units (hex).
    /// </summary>
    public struct AfsTile : IEquatable<AfsTile>
    {
        public readonly int Level;
        public readonly int X; // south-west corner, level-16 grid units
        public readonly int Y;

        public AfsTile(int level, int x, int y)
        {
            if (level < 0 || level > 16)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "AFS tile levels are 0..16");
            }

            int span = 0x10000 >> level;
            if (x < 0 || y < 0 || x % span != 0 || y % span != 0 || x >= 0x10000 || y >= 0x10000)
            {
                throw new ArgumentException(String.Format("({0:x}, {1:x}) isn't the corner of a level {2} tile", x, y, level));
            }

            this.Level = level;
            this.X = x;
            this.Y = y;
        }

        public int Span
        {
            get { return 0x10000 >> this.Level; }
        }

        public double WestLongitude { get { return AfsGrid.Lon(this.X); } }
        public double EastLongitude { get { return AfsGrid.Lon(this.X + this.Span); } }
        public double SouthLatitude { get { return AfsGrid.Lat(this.Y); } }
        public double NorthLatitude { get { return AfsGrid.Lat(this.Y + this.Span); } }

        /// <summary>
        /// The child tile one level finer: cx = 0 west, 1 east; cy = 0 south, 1 north.
        /// </summary>
        public AfsTile Child(int cx, int cy)
        {
            int half = this.Span / 2;
            return new AfsTile(this.Level + 1, this.X + cx * half, this.Y + cy * half);
        }

        /// <summary>
        /// Parses a name such as map_12_44b0_a1d0 (a file name or path works too).
        /// </summary>
        public static AfsTile Parse(string name)
        {
            var stem = System.IO.Path.GetFileNameWithoutExtension(name);
            var parts = stem.Split('_');
            if (parts.Length != 4)
            {
                throw new FormatException(String.Format("'{0}' isn't named <prefix>_LL_XXXX_YYYY", stem));
            }

            return new AfsTile(
                Int32.Parse(parts[1], CultureInfo.InvariantCulture),
                Int32.Parse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                Int32.Parse(parts[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        public string Name(string prefix = "map")
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}_{1:00}_{2:x4}_{3:x4}", prefix, this.Level, this.X, this.Y);
        }

        public override string ToString()
        {
            return Name();
        }

        public bool Equals(AfsTile other)
        {
            return this.Level == other.Level && this.X == other.X && this.Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is AfsTile && Equals((AfsTile)obj);
        }

        public override int GetHashCode()
        {
            return (this.Level << 28) ^ (this.X << 14) ^ this.Y;
        }
    }
}
