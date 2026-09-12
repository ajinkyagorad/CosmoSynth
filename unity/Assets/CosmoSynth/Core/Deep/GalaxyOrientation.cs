using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Turning a galaxy's two measured shape numbers — a projected axis ratio and a position
    /// angle — into a disc in three dimensions.
    ///
    /// What is actually measured is an ellipse on the sky. Everything past that is a model:
    ///
    ///   * the **inclination** assumes the galaxy is a circular disc of some intrinsic
    ///     thickness q₀ seen at an angle (Hubble 1926). q₀ = 0.2 is the conventional choice
    ///     and it is a choice, so <see cref="InclinationDeg"/> takes it as an argument and the
    ///     shipped files record it;
    ///   * the **near side** is not determined at all. Nothing in a photometric axis ratio
    ///     says which edge of the disc is toward you, so <see cref="DiscNormal"/> takes a sign
    ///     and that sign is Procedural. It must be a *stable* function of the galaxy (a hash
    ///     of its name), not a random draw, or the field flickers as the catalogue reloads.
    ///
    /// And for an elliptical or an S0 the inclination is meaningless whatever the arithmetic
    /// says: Centaurus A's axis ratio 0.957 comes out at 17°, which describes a projected
    /// isophote and not a disc tilt. <see cref="IsDiscLike"/> is the gate.
    /// </summary>
    public static class GalaxyOrientation
    {
        /// <summary>The conventional intrinsic axis ratio of a thin disc seen edge on.</summary>
        public const double DefaultIntrinsicAxisRatio = 0.2;

        /// <summary>Unit vector toward (RA, Dec) in ICRF.</summary>
        public static Double3 Direction(double raDeg, double decDeg)
        {
            double ra = raDeg * Math.PI / 180.0;
            double dec = decDeg * Math.PI / 180.0;
            double cd = Math.Cos(dec);
            return new Double3(cd * Math.Cos(ra), cd * Math.Sin(ra), Math.Sin(dec));
        }

        /// <summary>
        /// The local sky frame at (RA, Dec): north toward increasing declination, east toward
        /// increasing right ascension. Both are unit vectors perpendicular to the line of
        /// sight, and (east × north) points back at the observer.
        /// </summary>
        public static void SkyBasis(double raDeg, double decDeg, out Double3 north, out Double3 east)
        {
            double ra = raDeg * Math.PI / 180.0;
            double dec = decDeg * Math.PI / 180.0;
            double sd = Math.Sin(dec), cd = Math.Cos(dec);
            double sa = Math.Sin(ra), ca = Math.Cos(ra);
            north = new Double3(-sd * ca, -sd * sa, cd);
            east = new Double3(-sa, ca, 0.0);
        }

        /// <summary>
        /// The major axis as a direction on the sky. A position angle is degrees East of
        /// North, so PA = 0 is north and PA = 90 is east.
        /// </summary>
        public static Double3 MajorAxis(Double3 north, Double3 east, double positionAngleDeg)
        {
            double pa = positionAngleDeg * Math.PI / 180.0;
            return north * Math.Cos(pa) + east * Math.Sin(pa);
        }

        /// <summary>Convenience: the major axis at a sky position, in ICRF.</summary>
        public static Double3 MajorAxis(double raDeg, double decDeg, double positionAngleDeg)
        {
            SkyBasis(raDeg, decDeg, out Double3 north, out Double3 east);
            return MajorAxis(north, east, positionAngleDeg);
        }

        /// <summary>
        /// Inclination from a projected axis ratio q = b/a, degrees. 0 is face on, 90 edge on.
        ///
        ///     cos²i = (q² − q₀²) / (1 − q₀²)          (Hubble 1926)
        ///
        /// and 90° wherever q ≤ q₀, because a disc cannot look flatter than it is: a smaller
        /// ratio is not a steeper tilt, it is a thinner galaxy than the model assumed.
        /// </summary>
        public static double InclinationDeg(double axisRatio,
            double intrinsicAxisRatio = DefaultIntrinsicAxisRatio)
        {
            if (double.IsNaN(axisRatio)) return double.NaN;
            if (axisRatio <= intrinsicAxisRatio) return 90.0;
            if (axisRatio >= 1.0) return 0.0;

            double q0 = intrinsicAxisRatio;
            double cos2 = (axisRatio * axisRatio - q0 * q0) / (1.0 - q0 * q0);
            if (cos2 < 0.0) cos2 = 0.0;
            if (cos2 > 1.0) cos2 = 1.0;
            return Math.Acos(Math.Sqrt(cos2)) * 180.0 / Math.PI;
        }

        /// <summary>The inverse: the axis ratio a disc at this inclination would project to.</summary>
        public static double AxisRatioFromInclinationDeg(double inclinationDeg,
            double intrinsicAxisRatio = DefaultIntrinsicAxisRatio)
        {
            if (double.IsNaN(inclinationDeg)) return double.NaN;
            double c = Math.Cos(inclinationDeg * Math.PI / 180.0);
            double q0 = intrinsicAxisRatio;
            return Math.Sqrt(c * c * (1.0 - q0 * q0) + q0 * q0);
        }

        /// <summary>
        /// The disc's normal: tilt the line of sight by the inclination about the major axis.
        ///
        /// At i = 0 the normal points straight back at the observer (face on); at i = 90 it
        /// lies in the sky plane, perpendicular to the major axis (edge on). <paramref
        /// name="nearSideSign"/> is +1 or −1 and chooses which way the disc leans: that is
        /// **not measured**, and <see cref="NearSideSign"/> supplies a stable one.
        /// </summary>
        public static Double3 DiscNormal(Double3 lineOfSight, Double3 majorAxis,
            double inclinationDeg, double nearSideSign)
        {
            Double3 toObserver = -lineOfSight.Normalized;
            Double3 axis = (majorAxis - Double3.Project(majorAxis, toObserver)).Normalized;
            double i = inclinationDeg * Math.PI / 180.0 * (nearSideSign < 0 ? -1.0 : 1.0);
            return Double3.Rotate(toObserver, axis, i).Normalized;
        }

        /// <summary>
        /// A stable, arbitrary near-side choice. Procedural, and the same every run: FNV-1a
        /// over the galaxy's name, low bit. A random draw would make the whole field flip as
        /// the catalogue reloads, which looks like a bug and is one.
        /// </summary>
        public static double NearSideSign(string name)
        {
            if (string.IsNullOrEmpty(name)) return 1.0;
            ulong h = 0xCBF29CE484222325UL;
            foreach (char c in name)
            {
                h ^= (byte)c;
                h *= 0x100000001B3UL;
            }
            return (h & 1UL) == 0 ? 1.0 : -1.0;
        }

        /// <summary>
        /// Whether an inclination means anything for this morphology. An E or S0 has no disc,
        /// so its axis ratio is an intrinsic shape seen in projection and the Hubble formula
        /// returns a number about nothing. T-type ≥ 0 (S0/a and later) is the gate; the
        /// Local Volume file ships 99 for "unknown", which is not disc-like either — a value
        /// you do not have cannot license a claim.
        /// </summary>
        public static bool IsDiscLike(int tType) => tType >= 0 && tType <= 10;
    }
}
