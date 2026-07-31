using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// A double-precision 3x3 matrix, used for frame rotations.
    ///
    /// Rotations are kept as matrices rather than quaternions here because the frame
    /// definitions we ingest (IAU pole/prime-meridian elements, the ecliptic obliquity,
    /// SPICE frame kernels) are all published as sequences of axis rotations, and composing
    /// them as matrices keeps the code a transcription of the published formula rather than a
    /// translation of it. Unity's float quaternions take over once a value reaches the
    /// renderer, where the precision is no longer needed.
    ///
    /// Storage is row-major: <c>M{row}{column}</c>.
    /// </summary>
    [Serializable]
    public struct Matrix3d
    {
        public double M00, M01, M02;
        public double M10, M11, M12;
        public double M20, M21, M22;

        public Matrix3d(
            double m00, double m01, double m02,
            double m10, double m11, double m12,
            double m20, double m21, double m22)
        {
            M00 = m00; M01 = m01; M02 = m02;
            M10 = m10; M11 = m11; M12 = m12;
            M20 = m20; M21 = m21; M22 = m22;
        }

        public static readonly Matrix3d Identity = new Matrix3d(1, 0, 0, 0, 1, 0, 0, 0, 1);

        /// <summary>Build a matrix whose columns are the given basis vectors.</summary>
        public static Matrix3d FromColumns(Double3 x, Double3 y, Double3 z) => new Matrix3d(
            x.x, y.x, z.x,
            x.y, y.y, z.y,
            x.z, y.z, z.z);

        /// <summary>Build a matrix whose rows are the given basis vectors.</summary>
        public static Matrix3d FromRows(Double3 x, Double3 y, Double3 z) => new Matrix3d(
            x.x, x.y, x.z,
            y.x, y.y, y.z,
            z.x, z.y, z.z);

        public Double3 Row0 => new Double3(M00, M01, M02);
        public Double3 Row1 => new Double3(M10, M11, M12);
        public Double3 Row2 => new Double3(M20, M21, M22);

        public Double3 Column0 => new Double3(M00, M10, M20);
        public Double3 Column1 => new Double3(M01, M11, M21);
        public Double3 Column2 => new Double3(M02, M12, M22);

        /// <summary>Right-handed rotation about the x axis.</summary>
        public static Matrix3d RotationX(double radians)
        {
            double c = System.Math.Cos(radians), s = System.Math.Sin(radians);
            return new Matrix3d(
                1, 0, 0,
                0, c, -s,
                0, s, c);
        }

        /// <summary>Right-handed rotation about the y axis.</summary>
        public static Matrix3d RotationY(double radians)
        {
            double c = System.Math.Cos(radians), s = System.Math.Sin(radians);
            return new Matrix3d(
                c, 0, s,
                0, 1, 0,
                -s, 0, c);
        }

        /// <summary>Right-handed rotation about the z axis.</summary>
        public static Matrix3d RotationZ(double radians)
        {
            double c = System.Math.Cos(radians), s = System.Math.Sin(radians);
            return new Matrix3d(
                c, -s, 0,
                s, c, 0,
                0, 0, 1);
        }

        /// <summary>Rotation of <paramref name="radians"/> about an arbitrary axis (Rodrigues).</summary>
        public static Matrix3d RotationAxis(Double3 axis, double radians)
        {
            Double3 k = axis.Normalized;
            double c = System.Math.Cos(radians), s = System.Math.Sin(radians), t = 1.0 - c;
            return new Matrix3d(
                t * k.x * k.x + c, t * k.x * k.y - s * k.z, t * k.x * k.z + s * k.y,
                t * k.x * k.y + s * k.z, t * k.y * k.y + c, t * k.y * k.z - s * k.x,
                t * k.x * k.z - s * k.y, t * k.y * k.z + s * k.x, t * k.z * k.z + c);
        }

        public static Double3 operator *(Matrix3d m, Double3 v) => new Double3(
            m.M00 * v.x + m.M01 * v.y + m.M02 * v.z,
            m.M10 * v.x + m.M11 * v.y + m.M12 * v.z,
            m.M20 * v.x + m.M21 * v.y + m.M22 * v.z);

        public static Matrix3d operator *(Matrix3d a, Matrix3d b) => new Matrix3d(
            a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20,
            a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21,
            a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22,

            a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20,
            a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21,
            a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22,

            a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20,
            a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21,
            a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22);

        public Matrix3d Transposed => new Matrix3d(
            M00, M10, M20,
            M01, M11, M21,
            M02, M12, M22);

        /// <summary>
        /// Inverse of a rotation. This is the transpose, which is exact and free — but only
        /// for an orthonormal matrix. Every matrix produced by this type's factories is
        /// orthonormal; if one is ever built from arbitrary values, use <see cref="Inverse"/>.
        /// </summary>
        public Matrix3d InverseRotation => Transposed;

        public double Determinant =>
            M00 * (M11 * M22 - M12 * M21)
            - M01 * (M10 * M22 - M12 * M20)
            + M02 * (M10 * M21 - M11 * M20);

        /// <summary>General inverse, for matrices that are not known to be orthonormal.</summary>
        public Matrix3d Inverse
        {
            get
            {
                double det = Determinant;
                if (det == 0.0) throw new InvalidOperationException("Matrix3d is singular and cannot be inverted.");
                double inv = 1.0 / det;
                return new Matrix3d(
                    (M11 * M22 - M12 * M21) * inv, (M02 * M21 - M01 * M22) * inv, (M01 * M12 - M02 * M11) * inv,
                    (M12 * M20 - M10 * M22) * inv, (M00 * M22 - M02 * M20) * inv, (M02 * M10 - M00 * M12) * inv,
                    (M10 * M21 - M11 * M20) * inv, (M01 * M20 - M00 * M21) * inv, (M00 * M11 - M01 * M10) * inv);
            }
        }

        /// <summary>True if this is a proper rotation: orthonormal with determinant +1.</summary>
        public bool IsRotation(double tolerance = 1e-12)
        {
            if (System.Math.Abs(Determinant - 1.0) > tolerance) return false;
            Matrix3d shouldBeIdentity = this * Transposed;
            return System.Math.Abs(shouldBeIdentity.M00 - 1.0) < tolerance
                && System.Math.Abs(shouldBeIdentity.M11 - 1.0) < tolerance
                && System.Math.Abs(shouldBeIdentity.M22 - 1.0) < tolerance
                && System.Math.Abs(shouldBeIdentity.M01) < tolerance
                && System.Math.Abs(shouldBeIdentity.M02) < tolerance
                && System.Math.Abs(shouldBeIdentity.M12) < tolerance;
        }

        public override string ToString() =>
            $"[{M00:G6} {M01:G6} {M02:G6}; {M10:G6} {M11:G6} {M12:G6}; {M20:G6} {M21:G6} {M22:G6}]";
    }
}
