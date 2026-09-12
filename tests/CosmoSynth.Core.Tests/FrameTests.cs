using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The standard rotations and the one ICRF-to-Unity axis mapping.
    ///
    /// Both halves existed before and neither had a test. The galactic matrix had every sign
    /// wrong and still produced a plausible band of stars across a plausible sky; the axis
    /// mapping was the identity, which mirrors the solar system so that it orbits correctly and
    /// rotates backwards. Neither is visible in a screenshot. Both are one assertion away.
    /// </summary>
    public static class FrameTests
    {
        private static void AssertMatrix(Matrix3d expected, Matrix3d actual, double tolerance, string what)
        {
            Assert.Equal(expected.M00, actual.M00, tolerance, what + " [0,0]");
            Assert.Equal(expected.M01, actual.M01, tolerance, what + " [0,1]");
            Assert.Equal(expected.M02, actual.M02, tolerance, what + " [0,2]");
            Assert.Equal(expected.M10, actual.M10, tolerance, what + " [1,0]");
            Assert.Equal(expected.M11, actual.M11, tolerance, what + " [1,1]");
            Assert.Equal(expected.M12, actual.M12, tolerance, what + " [1,2]");
            Assert.Equal(expected.M20, actual.M20, tolerance, what + " [2,0]");
            Assert.Equal(expected.M21, actual.M21, tolerance, what + " [2,1]");
            Assert.Equal(expected.M22, actual.M22, tolerance, what + " [2,2]");
        }

        private static void Spherical(Double3 v, out double lonDeg, out double latDeg)
        {
            StandardFrames.ToSpherical(v, out double lon, out double lat, out _);
            lonDeg = lon * MathD.Rad2Deg;
            latDeg = lat * MathD.Rad2Deg;
        }

        private static Double3 Icrs(double raDeg, double decDeg) =>
            StandardFrames.FromSpherical(raDeg * MathD.Deg2Rad, decDeg * MathD.Deg2Rad);

        // ---- Galactic -------------------------------------------------------

        [Test("the galactic rotation matches the Hipparcos matrix element by element")]
        public static void GalacticMatchesHipparcos()
        {
            // The published ICRS-to-galactic matrix (Hipparcos, ESA SP-1200 vol. 1 §1.5.3).
            // Tolerance 1e-9: twenty times the 4.9e-11 this construction actually achieves, which
            // is set by the pole angles being published to five decimals. Anything looser would
            // have passed the shipped code, which was out by 1.75 per element.
            var hipparcos = new Matrix3d(
                -0.0548755604, -0.8734370902, -0.4838350155,
                 0.4941094279, -0.4448296300,  0.7469822445,
                -0.8676661490, -0.1980763734,  0.4559837762);

            AssertMatrix(hipparcos, StandardFrames.EquatorialToGalactic, 1e-9, "EquatorialToGalactic");
            Assert.True(StandardFrames.EquatorialToGalactic.IsRotation(1e-12), "it is a proper rotation");
        }

        [Test("the north galactic pole lands on the galactic z axis")]
        public static void NorthGalacticPoleIsExact()
        {
            Double3 ngp = StandardFrames.EquatorialToGalactic *
                          Icrs(StandardFrames.GalacticPoleRaDeg, StandardFrames.GalacticPoleDecDeg);

            // Exact by construction, so 1e-12. The shipped code put it at (-0.346, 0.176, 0.922),
            // 45 degrees away, and nothing downstream complained.
            Assert.VectorClose(Double3.UnitZ, ngp, 1e-12, "NGP in galactic coordinates");
        }

        [Test("galactic longitude zero points at the galactic centre")]
        public static void GalacticOriginIsTheGalacticCentre()
        {
            Spherical(StandardFrames.GalacticToEquatorial * Double3.UnitX, out double ra, out double dec);

            // Sgr A*: RA 266.405, Dec -28.936. 1e-6 deg is what the matrix implies from five-decimal
            // pole angles; the value itself is a physical fact, not a round trip.
            Assert.Equal(266.404995, ra, 1e-6, "RA of the galactic centre");
            Assert.Equal(-28.936174, dec, 1e-6, "Dec of the galactic centre");
        }

        [Test("Sirius and M31 land at their catalogued galactic coordinates")]
        public static void GalacticTestVectors()
        {
            // 1e-4 deg is the rounding of the published expectations, not a chosen bound.
            Spherical(StandardFrames.EquatorialToGalactic * Icrs(101.287155, -16.716116),
                out double sl, out double sb);
            Assert.Equal(227.2303, sl, 1e-4, "Sirius galactic longitude");
            Assert.Equal(-8.8903, sb, 1e-4, "Sirius galactic latitude");

            Spherical(StandardFrames.EquatorialToGalactic * Icrs(10.684708, 41.268750),
                out double ml, out double mb);
            Assert.Equal(121.1743, ml, 1e-4, "M31 galactic longitude");
            Assert.Equal(-21.5733, mb, 1e-4, "M31 galactic latitude");

            Spherical(StandardFrames.GalacticToEquatorial * Icrs(264.021, 48.253),
                out double dra, out double ddec);
            Assert.Equal(167.9419, dra, 1e-4, "CMB dipole RA");
            Assert.Equal(-6.9443, ddec, 1e-4, "CMB dipole Dec");
        }

        // ---- Supergalactic --------------------------------------------------

        [Test("the supergalactic rotation matches its published matrix and its own defining pole")]
        public static void SupergalacticIsCorrect()
        {
            var published = new Matrix3d(
                -0.7357425748,  0.6772612964,  0.0,
                -0.0745537784, -0.0809914713,  0.9939225904,
                 0.6731453021,  0.7312711658,  0.1100812622);
            AssertMatrix(published, StandardFrames.GalacticToSupergalactic, 1e-9, "GalacticToSupergalactic");

            var fromIcrs = new Matrix3d(
                 0.3750154778,  0.3413589618,  0.8618801834,
                -0.8983204556, -0.0957271407,  0.4287851135,
                 0.2288749668, -0.9350456533,  0.2707505787);
            AssertMatrix(fromIcrs, StandardFrames.EquatorialToSupergalactic, 1e-9, "EquatorialToSupergalactic");

            Double3 pole = StandardFrames.GalacticToSupergalactic *
                StandardFrames.FromSpherical(
                    StandardFrames.SupergalacticPoleGalacticLonDeg * MathD.Deg2Rad,
                    StandardFrames.SupergalacticPoleGalacticLatDeg * MathD.Deg2Rad);
            Assert.VectorClose(Double3.UnitZ, pole, 1e-12, "the SG pole is the SG z axis, by construction");

            Spherical(StandardFrames.SupergalacticToEquatorial * Double3.UnitZ, out double ra, out double dec);
            Assert.Equal(283.7541, ra, 1e-4, "SG pole RA");
            Assert.Equal(15.7089, dec, 1e-4, "SG pole Dec");
        }

        [Test("M31 and M87 land at their catalogued supergalactic coordinates")]
        public static void SupergalacticTestVectors()
        {
            Spherical(StandardFrames.EquatorialToSupergalactic * Icrs(10.684708, 41.268750),
                out double ml, out double mb);
            Assert.Equal(336.1930, ml, 1e-4, "M31 SGL");
            Assert.Equal(12.5521, mb, 1e-4, "M31 SGB");

            Spherical(StandardFrames.EquatorialToSupergalactic * Icrs(187.70593, 12.39112),
                out double vl, out double vb);
            Assert.Equal(102.8806, vl, 1e-4, "M87 SGL");
            Assert.Equal(-2.3479, vb, 1e-4, "M87 SGB");
        }

        [Test("the angles between the plane poles are the ones the levelling animation must cover")]
        public static void PlanePoleAngles()
        {
            Double3 eclipticPole = StandardFrames.EclipticToEquatorial() * Double3.UnitZ;
            Double3 galacticPole = StandardFrames.GalacticToEquatorial * Double3.UnitZ;
            Double3 superPole = StandardFrames.SupergalacticToEquatorial * Double3.UnitZ;

            // 0.01 deg: the published figures are quoted to two decimals.
            Assert.Equal(60.19, Double3.Angle(eclipticPole, galacticPole) * MathD.Rad2Deg, 0.01,
                "ecliptic to galactic");
            Assert.Equal(83.68, Double3.Angle(galacticPole, superPole) * MathD.Rad2Deg, 0.01,
                "galactic to supergalactic");
            Assert.Equal(51.66, Double3.Angle(eclipticPole, superPole) * MathD.Rad2Deg, 0.01,
                "ecliptic to supergalactic");
            Assert.Equal(23.44, Double3.Angle(Double3.UnitZ, eclipticPole) * MathD.Rad2Deg, 0.01,
                "equatorial to ecliptic");
        }

        [Test("the ecliptic pole sits where the obliquity says it does")]
        public static void EclipticPole()
        {
            Double3 pole = StandardFrames.EclipticToEquatorial() * Double3.UnitZ;
            Assert.VectorClose(new Double3(0.0, -0.3977771559, 0.9174820621), pole, 1e-10,
                "ecliptic pole in ICRF");

            Spherical(pole, out double ra, out double dec);
            Assert.Equal(270.0, ra, 1e-9, "ecliptic pole RA");
            Assert.Equal(66.5607, dec, 1e-4, "ecliptic pole Dec");
        }

        // ---- The one axis mapping -------------------------------------------

        [Test("the axis mapping is the right-to-left-handed change of basis, and its own inverse")]
        public static void AxisSwapHandedness()
        {
            Matrix3d p = ScaleSpace.RenderTransform.AxisSwap;

            // det = -1 is the whole point: a determinant of +1 would be a rotation, and no
            // rotation converts a right-handed frame to a left-handed one. The identity that used
            // to stand here has det +1, which IS the mirror.
            Assert.Equal(-1.0, p.Determinant, 1e-15, "the mapping must flip handedness");
            AssertMatrix(Matrix3d.Identity, p * p, 1e-15, "P is its own inverse");
        }

        [Test("celestial north maps to Unity up, and the vernal equinox to Unity +x")]
        public static void AxisMappingKnownAnswers()
        {
            var t = new ScaleSpace.RenderTransform(Double3.Zero, 1.0);

            // Exact: these are permutations of 1 and 0.
            Assert.VectorClose(new Double3(1, 0, 0), t.MapDirection(Double3.UnitX), 0.0,
                "ICRF +x (vernal equinox) is universe-local +x");
            Assert.VectorClose(new Double3(0, 1, 0), t.MapDirection(Double3.UnitZ), 0.0,
                "ICRF +z (celestial north) is universe-local +y — this is which axis is up");
            Assert.VectorClose(new Double3(0, 0, 1), t.MapDirection(Double3.UnitY), 0.0,
                "ICRF +y is universe-local +z");

            Assert.VectorClose(Double3.UnitZ, t.UnmapDirection(new Double3(0, 1, 0)), 0.0,
                "and back again");
        }

        [Test("a known body direction survives the mapping: the ecliptic pole is 23.44 deg off Unity up")]
        public static void KnownBodyDirectionThroughTheMapping()
        {
            var t = new ScaleSpace.RenderTransform(Double3.Zero, 1.0);

            // A physical fact rather than a round trip: with the universe host at identity, Unity
            // +y is the celestial pole, so the ecliptic pole must sit one obliquity away from it.
            // A mapping that mirrored or transposed an axis would still give a unit vector, and
            // would still give 23.44 degrees from SOME axis — so the test names which one.
            Double3 eclipticPole = t.MapDirection(StandardFrames.EclipticToEquatorial() * Double3.UnitZ);
            Assert.Equal(23.4392911, Double3.Angle(new Double3(0, 1, 0), eclipticPole) * MathD.Rad2Deg,
                1e-6, "ecliptic pole against universe-local up");

            // And it leans toward -z, not +z: ICRF y is negative for the ecliptic pole.
            Assert.True(eclipticPole.z < 0.0, "the lean is toward universe-local -z");
            Assert.True(MathD.Approximately(eclipticPole.x, 0.0, 1e-12), "and has no x component");
        }

        [Test("a body rotation maps to a proper rotation whose pole agrees with the position mapping")]
        public static void MapRotationAgreesWithMapDirection()
        {
            var t = new ScaleSpace.RenderTransform(Double3.Zero, 1.0);
            Matrix3d bodyToIcrf =
                Matrix3d.RotationZ(0.7) * Matrix3d.RotationX(0.3) * Matrix3d.RotationY(-1.1);

            Matrix3d mapped = t.MapRotation(bodyToIcrf);
            Assert.Equal(1.0, mapped.Determinant, 1e-12, "P M P must be proper, or it cannot be a quaternion");
            Assert.True(mapped.IsRotation(1e-12), "and orthonormal");

            // The test that makes the old split impossible: the body's pole (its own +z in ICRF)
            // must land exactly where the position mapping says a direction lands, which is
            // universe-local +y. When rotations and positions were converted by two separate
            // hand-written helpers, this is the invariant that silently broke.
            Assert.VectorClose(t.MapDirection(bodyToIcrf * Double3.UnitZ), mapped * new Double3(0, 1, 0),
                1e-15, "the mapped pole");
        }

        [Test("the absolute-to-universe mapping round-trips to a part in 1e15")]
        public static void RenderTransformRoundTrips()
        {
            var t = new ScaleSpace.RenderTransform(new Double3(1.4959787e8, -3.2e7, 8.1e6), 4.31e-5);
            var absolute = new Double3(1.4959787e8 + 384400.0, -3.2e7 + 1234.5, 8.1e6 - 77.25);

            Double3 local = t.ToRender(absolute);
            Double3 back = t.ToAbsolute(local);

            // Two double rotations and a multiply-divide: 1e-15 of |x| is the arithmetic's own
            // precision, which at 1.5e8 km is about 150 metres of an AU-sized number.
            Assert.VectorClose(absolute, back, 1e-15 * absolute.Magnitude, "absolute round trip");

            // The Moon, 384400 km out along ICRF +x, must be 384400 * upk metres along local +x.
            Assert.Relative(384400.0 * 4.31e-5, local.x, 1e-12, "scaled offset along x");
            Assert.Relative(-77.25 * 4.31e-5, local.y, 1e-12, "ICRF z becomes local y");
            Assert.Relative(1234.5 * 4.31e-5, local.z, 1e-12, "ICRF y becomes local z");
        }

        [Test("the scale ladder has exactly one clamp and it is the published pair")]
        public static void ScaleLimitsClamp()
        {
            Assert.True(ScaleLimits.Clamp(1.0) == 1.0, "a scale inside the ladder passes through");
            Assert.True(ScaleLimits.Clamp(1e9) == ScaleLimits.MaxUnitsPerKm, "1:1 is the top, nothing is larger than life");
            Assert.True(ScaleLimits.Clamp(1e-40) == ScaleLimits.MinUnitsPerKm, "and the observable universe is the bottom");
            Assert.True(ScaleLimits.Clamp(0.0) == ScaleLimits.MinUnitsPerKm, "a zero request cannot divide by zero downstream");
            Assert.True(ScaleLimits.Clamp(double.NaN) == ScaleLimits.MinUnitsPerKm, "nor can a NaN one");
            Assert.Equal(27.699, ScaleLimits.Decades, 0.001, "the ladder spans 27.7 decades");
        }
    }
}
