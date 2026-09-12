using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Turning a catalogue's two numbers — axis ratio and position angle — into a disc in space.
    /// CONTRACT C.10. Pure geometry, no data, no engine: every claim below is a test.
    ///
    /// This is what separates a galaxy field that reads as a galaxy field from one that reads as
    /// a cloud of identical blobs. Andromeda is an ellipse at a known angle; drawing it as a
    /// circle throws away a measurement that was published.
    ///
    /// <b>The honesty rule that comes with it:</b> where <c>ell</c> or <c>pa</c> is unknown the
    /// caller must draw the galaxy round and faint and tag it Inferred, never fill in a default.
    /// The Atlas shipped 542 of 911 galaxies with a default ellipticity drawn as if it had been
    /// measured, and there is no way to tell from the picture which ones those were.
    /// </summary>
    public static class GalaxyOrientation
    {
        /// <summary>
        /// The local sky frame at (RA, Dec): <c>n</c> points north, <c>e</c> points east, and
        /// <c>r</c> (their cross product) points at the object. All ICRF, all unit.
        /// </summary>
        public static void SkyBasis(double raRad, double decRad, out Double3 north, out Double3 east,
            out Double3 outward)
        {
            double sa = Math.Sin(raRad), ca = Math.Cos(raRad);
            double sd = Math.Sin(decRad), cd = Math.Cos(decRad);
            north = new Double3(-sd * ca, -sd * sa, cd);
            east = new Double3(-sa, ca, 0.0);
            outward = new Double3(cd * ca, cd * sa, sd);
        }

        /// <summary>
        /// The major axis direction for a position angle measured east of north.
        /// PA 0 is parallel to north; PA 90 is parallel to east. Exact, and tested as such.
        /// </summary>
        public static Double3 MajorAxis(double raRad, double decRad, double paDeg)
        {
            SkyBasis(raRad, decRad, out Double3 n, out Double3 e, out _);
            double pa = paDeg * MathD.Deg2Rad;
            return n * Math.Cos(pa) + e * Math.Sin(pa);
        }

        /// <summary>
        /// Inclination from the observed axis ratio, Hubble 1926:
        /// <c>cos^2 i = (q^2 - q0^2) / (1 - q0^2)</c>, and 90 deg for a disc thinner than q0.
        ///
        /// <paramref name="q0"/> is the intrinsic thickness of the disc, 0.2 by convention. It is
        /// the reason an edge-on spiral does not come out at exactly 90 deg: a real disc has
        /// thickness, and pretending otherwise inflates every inclination near the edge-on end.
        /// </summary>
        public static double InclinationDeg(double q, double q0 = 0.2)
        {
            if (double.IsNaN(q)) return double.NaN;
            if (q <= q0) return 90.0;
            if (q >= 1.0) return 0.0;
            double cos2 = (q * q - q0 * q0) / (1.0 - q0 * q0);
            if (cos2 < 0.0) cos2 = 0.0;
            return Math.Acos(Math.Sqrt(cos2)) * MathD.Rad2Deg;
        }

        /// <summary>
        /// The disc normal for a galaxy seen along <paramref name="outward"/>, inclined by
        /// <paramref name="inclinationDeg"/> about its major axis.
        ///
        /// <paramref name="sign"/> chooses which face is toward us. It is NOT measured — a
        /// position angle and an axis ratio cannot distinguish the two — so callers pass a hash
        /// of the object's name and the field is tagged Procedural.
        /// </summary>
        public static Double3 DiscNormal(Double3 outward, double inclinationDeg, Double3 majorAxis,
            int sign)
        {
            double i = inclinationDeg * MathD.Deg2Rad;
            Double3 axis = majorAxis.Normalized;
            Double3 los = outward.Normalized;
            // Face-on (i = 0) means the normal points back along the line of sight. Tilting by i
            // about the major axis is what an inclination is.
            Double3 faceOn = -los;
            return Double3.Rotate(faceOn, axis, (sign >= 0 ? 1.0 : -1.0) * i).Normalized;
        }

        /// <summary>
        /// A deterministic front/back choice from a name. Procedural by construction: it must be
        /// stable across runs (a galaxy that flips every frame is a bug the eye sees instantly)
        /// and it must never be mistaken for data.
        /// </summary>
        public static int FaceSign(string name)
        {
            if (string.IsNullOrEmpty(name)) return 1;
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < name.Length; i++) { h ^= name[i]; h *= 16777619u; }
                return (h & 1u) == 0u ? 1 : -1;
            }
        }
    }
}
