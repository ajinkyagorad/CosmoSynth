using System;
using System.Globalization;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Double-precision 3-vector. Absolute universe positions are stored in these; Unity's
    /// float <c>Vector3</c> only enters after the floating origin has rebased a position to
    /// something small. A double holds ~15-16 significant digits, so metre precision survives
    /// out to roughly 10^9 km (Neptune's orbit) and centimetre precision to ~10^13 m.
    /// Beyond that <see cref="CosmicPosition"/> adds a sector offset.
    /// </summary>
    [Serializable]
    public struct Double3 : IEquatable<Double3>
    {
        public double x;
        public double y;
        public double z;

        public Double3(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Double3(double scalar)
        {
            x = scalar;
            y = scalar;
            z = scalar;
        }

        public static readonly Double3 Zero = new Double3(0, 0, 0);
        public static readonly Double3 One = new Double3(1, 1, 1);
        public static readonly Double3 UnitX = new Double3(1, 0, 0);
        public static readonly Double3 UnitY = new Double3(0, 1, 0);
        public static readonly Double3 UnitZ = new Double3(0, 0, 1);

        public double this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    default: throw new IndexOutOfRangeException("Double3 index must be 0..2");
                }
            }
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException("Double3 index must be 0..2");
                }
            }
        }

        public double SqrMagnitude => x * x + y * y + z * z;

        public double Magnitude => System.Math.Sqrt(x * x + y * y + z * z);

        /// <summary>Unit vector, or <see cref="Zero"/> if this vector is degenerate.</summary>
        public Double3 Normalized
        {
            get
            {
                double m = Magnitude;
                return m > 0.0 ? new Double3(x / m, y / m, z / m) : Zero;
            }
        }

        public bool IsFinite =>
            !(double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z) ||
              double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(z));

        public static Double3 operator +(Double3 a, Double3 b) => new Double3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Double3 operator -(Double3 a, Double3 b) => new Double3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Double3 operator -(Double3 a) => new Double3(-a.x, -a.y, -a.z);
        public static Double3 operator *(Double3 a, double s) => new Double3(a.x * s, a.y * s, a.z * s);
        public static Double3 operator *(double s, Double3 a) => new Double3(a.x * s, a.y * s, a.z * s);
        public static Double3 operator /(Double3 a, double s) => new Double3(a.x / s, a.y / s, a.z / s);

        /// <summary>Component-wise product (scaling), not a dot product.</summary>
        public static Double3 Scale(Double3 a, Double3 b) => new Double3(a.x * b.x, a.y * b.y, a.z * b.z);

        public static double Dot(Double3 a, Double3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static Double3 Cross(Double3 a, Double3 b) => new Double3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x);

        public static double Distance(Double3 a, Double3 b) => (a - b).Magnitude;

        public static double SqrDistance(Double3 a, Double3 b) => (a - b).SqrMagnitude;

        public static Double3 Lerp(Double3 a, Double3 b, double t) => a + (b - a) * t;

        public static Double3 Min(Double3 a, Double3 b) => new Double3(
            System.Math.Min(a.x, b.x), System.Math.Min(a.y, b.y), System.Math.Min(a.z, b.z));

        public static Double3 Max(Double3 a, Double3 b) => new Double3(
            System.Math.Max(a.x, b.x), System.Math.Max(a.y, b.y), System.Math.Max(a.z, b.z));

        /// <summary>
        /// Unsigned angle between two vectors in radians, computed with the atan2 form because
        /// acos(dot) loses catastrophic precision for nearly parallel vectors — which is the
        /// common case when comparing orbit normals of near-coplanar planets.
        /// </summary>
        public static double Angle(Double3 a, Double3 b)
        {
            double cross = Cross(a, b).Magnitude;
            double dot = Dot(a, b);
            return System.Math.Atan2(cross, dot);
        }

        /// <summary>Component of <paramref name="a"/> along <paramref name="onto"/>.</summary>
        public static Double3 Project(Double3 a, Double3 onto)
        {
            double d = onto.SqrMagnitude;
            if (d <= 0.0) return Zero;
            return onto * (Dot(a, onto) / d);
        }

        /// <summary>Component of <paramref name="a"/> perpendicular to <paramref name="normal"/>.</summary>
        public static Double3 ProjectOnPlane(Double3 a, Double3 normal) => a - Project(a, normal);

        /// <summary>Rotate <paramref name="v"/> about a unit <paramref name="axis"/> by <paramref name="radians"/> (Rodrigues).</summary>
        public static Double3 Rotate(Double3 v, Double3 axis, double radians)
        {
            Double3 k = axis.Normalized;
            double c = System.Math.Cos(radians);
            double s = System.Math.Sin(radians);
            return v * c + Cross(k, v) * s + k * (Dot(k, v) * (1.0 - c));
        }

        public bool Equals(Double3 other) => x == other.x && y == other.y && z == other.z;

        public override bool Equals(object obj) => obj is Double3 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = x.GetHashCode();
                h = (h * 397) ^ y.GetHashCode();
                h = (h * 397) ^ z.GetHashCode();
                return h;
            }
        }

        public static bool operator ==(Double3 a, Double3 b) => a.Equals(b);
        public static bool operator !=(Double3 a, Double3 b) => !a.Equals(b);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:G17}, {1:G17}, {2:G17})", x, y, z);

        public string ToString(string format) =>
            string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2})",
                x.ToString(format, CultureInfo.InvariantCulture),
                y.ToString(format, CultureInfo.InvariantCulture),
                z.ToString(format, CultureInfo.InvariantCulture));
    }
}
