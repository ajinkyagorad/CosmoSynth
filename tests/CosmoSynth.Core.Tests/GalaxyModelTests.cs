using System;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The Galaxy's own geometry: the Sun's place in it, the disc, the bar and the Reid et al.
    /// 2019 spiral arms.
    ///
    /// Two tests here — <see cref="GalacticCentreDirection"/> and
    /// <see cref="NorthGalacticPoleIsThePole"/> — fail on this branch, and only because
    /// Core/Frames/StandardFrames.cs builds the ICRS to galactic rotation with all three signs
    /// reversed. They are written against the correct Hipparcos matrix on purpose, and the fix
    /// is in flight on lane/foundation-code; tools/gates/known-red.txt records it. Nothing else
    /// in this lane depends on that rotation, because every shipped position column was
    /// converted in Python by the importer, which has its own correct copy of the matrix.
    /// </summary>
    public static class GalaxyModelTests
    {
        [Test("the Sun is 8.15 kpc from the Galactic Centre, as Reid et al. 2019 measured it")]
        public static void SolarDistance()
        {
            Assert.Equal(8.15, GalaxyModel.R0Kpc, 0.0, "R0, kpc");
            Assert.Equal(8150.0, GalaxyModel.R0Pc, 0.0, "R0, pc");
            Assert.Equal(0.15, GalaxyModel.R0ErrKpc, 0.0, "published uncertainty on R0");
            Assert.Equal(236.0, GalaxyModel.Theta0KmS, 0.0, "circular speed at the Sun, km/s");
            // The arm fits assume this same R0. If the two ever disagree, the arms will not
            // pass through the Galactic Centre's own catalogue position.
            Assert.Equal(8150.0, GalaxyModel.GalacticCentrePc.x, 0.0,
                "the Galactic Centre lies along +x in the heliocentric galactic frame");
        }

        [Test("the north galactic pole maps to the z axis")]
        public static void NorthGalacticPoleIsThePole()
        {
            // The pole is at RA 192.85948, Dec +27.12825. Rotated into galactic coordinates it
            // must be (0, 0, 1) -- that is what "galactic coordinates" means.
            Double3 pole = StandardFrames.FromSpherical(
                StandardFrames.GalacticPoleRaDeg * MathD.Deg2Rad,
                StandardFrames.GalacticPoleDecDeg * MathD.Deg2Rad);
            Double3 rotated = StandardFrames.EquatorialToGalactic * pole;
            Assert.True(rotated.z > 0.999999,
                $"the north galactic pole came out at ({rotated.x:F3}, {rotated.y:F3}, " +
                $"{rotated.z:F3}) instead of (0, 0, 1). A 45-degree error here still produces " +
                "a plausible band of stars across a plausible sky.");
        }

        [Test("the direction of the Galactic Centre from the Sun is the direction of Sgr A*")]
        public static void GalacticCentreDirection()
        {
            // Sgr A* at RA 266.41684, Dec -29.00781 (Reid & Brunthaler 2004). The model's
            // Galactic Centre direction is +x in galactic coordinates rotated back to ICRF, so
            // the two agreeing is a check on the rotation and on the frame convention at once.
            Double3 sgrA = StandardFrames.FromSpherical(266.41684 * MathD.Deg2Rad,
                -29.00781 * MathD.Deg2Rad);
            double angle = Double3.Angle(GalaxyModel.GalacticCentreDirectionIcrf, sgrA);
            Assert.True(angle * MathD.Rad2Deg < 0.1,
                $"the model points {angle * MathD.Rad2Deg:F3} degrees away from Sgr A*. The " +
                "galactic longitude origin is defined so that the Galactic Centre sits within " +
                "a twentieth of a degree of l = 0.");
        }

        [Test("galactocentric azimuth is zero toward the Sun and grows with Galactic rotation")]
        public static void AzimuthConvention()
        {
            // Reid's beta. Getting this backwards mirrors every spiral arm, and a mirrored
            // spiral still looks exactly like a spiral.
            Double3 sun = GalaxyModel.ToGalactocentricPc(Double3.Zero);
            Assert.Equal(-8150.0, sun.x, 1e-9, "the Sun is at -R0 in galactocentric x");
            Assert.Equal(0.0, GalaxyModel.AzimuthDeg(sun), 1e-9, "beta at the Sun");

            // Rotation at the Sun is toward l = 90, which is +y in this frame.
            var ahead = new Double3(-8150.0, 100.0, 0.0);
            Assert.True(GalaxyModel.AzimuthDeg(ahead) > 0.0,
                "moving in the direction of Galactic rotation must increase beta");
            var quarter = new Double3(0.0, 8150.0, 0.0);
            Assert.Equal(90.0, GalaxyModel.AzimuthDeg(quarter), 1e-9,
                "a quarter turn round from the Sun");
            Assert.Equal(8.15, GalaxyModel.RadiusKpc(sun), 1e-9, "galactocentric radius of the Sun");
        }

        [Test("every spiral arm is drawn only over the azimuth range it was fitted to")]
        public static void ArmsStopWhereTheirDataStops()
        {
            foreach (SpiralArm arm in GalaxyModel.Arms)
            {
                Assert.True(arm.BetaMaxDeg > arm.BetaMinDeg, $"{arm.Name}: empty azimuth range");

                double justBefore = arm.BetaMinDeg - 0.5;
                double justAfter = arm.BetaMaxDeg + 0.5;
                Assert.True(double.IsNaN(arm.RadiusKpc(justBefore)),
                    $"{arm.Name} returns a radius at beta = {justBefore}, half a degree before " +
                    "its data starts. A log spiral extrapolated past its fit winds into the " +
                    "centre or off the disc while still looking like an arm.");
                Assert.True(double.IsNaN(arm.RadiusKpc(justAfter)),
                    $"{arm.Name} returns a radius half a degree past its data");
                Assert.False(double.IsNaN(arm.RadiusKpc(arm.BetaMinDeg)),
                    $"{arm.Name} has no radius at the start of its own range");
                Assert.False(double.IsNaN(arm.RadiusKpc(arm.BetaMaxDeg)),
                    $"{arm.Name} has no radius at the end of its own range");

                // At the kink the model is continuous whichever pitch angle applies.
                Assert.Equal(arm.RadiusKinkKpc, arm.RadiusKpc(arm.BetaKinkDeg), 1e-9,
                    $"{arm.Name}: the radius at the kink azimuth is R_kink by definition");

                Double3[] points = GalaxyModel.ArmPoints(arm);
                Assert.True(points.Length >= 2, $"{arm.Name}: too few points");
                foreach (Double3 p in points)
                {
                    double r = GalaxyModel.RadiusKpc(p);
                    Assert.True(r > 1.5 && r < 22.0,
                        $"{arm.Name} reaches {r:F2} kpc from the centre, which is outside the " +
                        "Galaxy the arms were fitted in");
                    // 1e-6 degrees is the round trip through Cartesian and back, not slack:
                    // the endpoints are generated AT BetaMin and BetaMax exactly.
                    double beta = GalaxyModel.AzimuthDeg(p);
                    Assert.True(beta >= arm.BetaMinDeg - 1e-6 && beta <= arm.BetaMaxDeg + 1e-6,
                        $"{arm.Name} sampled a point at beta = {beta:F6}, outside its fitted " +
                        $"range {arm.BetaMinDeg} to {arm.BetaMaxDeg}");
                }
            }
        }

        [Test("the Sun sits just inside the Local Arm, where Reid et al. place it")]
        public static void SunSitsOnTheLocalArm()
        {
            SpiralArm local = GalaxyModel.Arm("Local");
            double radiusAtSun = local.RadiusKpc(0.0);       // beta = 0 is the Sun's azimuth
            Assert.False(double.IsNaN(radiusAtSun),
                "the Local arm's fitted range must cover the Sun's own azimuth");
            double offsetKpc = radiusAtSun - GalaxyModel.R0Kpc;
            Assert.True(offsetKpc > 0.0 && offsetKpc < 0.5,
                $"the Local arm passes {offsetKpc * 1000:F0} pc outside the Sun's orbit. Reid " +
                "et al. put the Sun a few hundred parsecs inside the arm; a sign error in the " +
                "pitch angle sends this the other way.");
            Assert.True(offsetKpc * 1000.0 < 2.0 * GalaxyModel.ArmWidthPc(radiusAtSun),
                "and within a couple of arm widths of its ridge line");
        }

        [Test("arm widths follow the fitted relation, 336 pc at the solar circle")]
        public static void ArmWidths()
        {
            Assert.Equal(336.0, GalaxyModel.ArmWidthPc(GalaxyModel.R0Kpc), 1e-9,
                "w(R0), pc: Reid et al. 2019, w(R) = 336 + 36 (R - 8.15) pc");
            Assert.Equal(336.0 + 36.0, GalaxyModel.ArmWidthPc(GalaxyModel.R0Kpc + 1.0), 1e-9,
                "the slope is 36 pc per kpc");
            Assert.True(GalaxyModel.ArmWidthPc(4.0) < GalaxyModel.ArmWidthPc(12.0),
                "arms widen with radius");
            foreach (SpiralArm arm in GalaxyModel.Arms)
            {
                Assert.True(arm.WidthKpc > 0.0 && arm.WidthKpc < 1.0,
                    $"{arm.Name}: fitted intrinsic width {arm.WidthKpc} kpc");
            }
        }

        [Test("the seven fitted arms are the seven Reid et al. 2019 name, with their kinks")]
        public static void ArmTableMatchesThePaper()
        {
            Assert.True(GalaxyModel.Arms.Length == 7,
                $"Table 2 lists seven arm segments, this has {GalaxyModel.Arms.Length}");
            // Spot checks against the printed table, so a transcription slip shows up.
            SpiralArm sgr = GalaxyModel.Arm("Sagittarius-Carina");
            Assert.Equal(6.04, sgr.RadiusKinkKpc, 0.0, "Sgr-Car R_kink, kpc");
            Assert.Equal(24.0, sgr.BetaKinkDeg, 0.0, "Sgr-Car beta_kink, deg");
            Assert.Equal(17.1, sgr.PitchLowDeg, 0.0, "Sgr-Car pitch below the kink");
            Assert.Equal(1.0, sgr.PitchHighDeg, 0.0, "Sgr-Car pitch above the kink");
            Assert.True(sgr.HasKink, "the Sagittarius-Carina kink is the paper's worked example");
            Assert.True(sgr.Parallaxes == 35, "35 maser parallaxes were assigned to Sgr-Car");

            SpiralArm local = GalaxyModel.Arm("Local");
            Assert.False(local.HasKink,
                "only one pitch angle was solved for the Local arm, so its two entries are equal");
            Assert.Equal(8.26, local.RadiusKinkKpc, 0.0, "Local R_kink, kpc");

            SpiralArm outer = GalaxyModel.Arm("Outer");
            Assert.Equal(12.24, outer.RadiusKinkKpc, 0.0, "Outer R_kink, kpc");
            Assert.True(outer.WidthKpc > GalaxyModel.Arm("Norma").WidthKpc,
                "the Outer arm is the widest and Norma the narrowest");

            Assert.True(GalaxyModel.Citation.Contains("Reid"),
                "the arm model owes Reid et al. 2019 a citation, and shipping the geometry " +
                "without one is the whole reason the table itself is not redistributed");
        }

        [Test("the bar leans the way the bulge does, at 27 degrees to the Sun-Centre line")]
        public static void BarOrientation()
        {
            Assert.Equal(27.0, GalaxyModel.BarAngleDeg, 0.0,
                "bar angle, degrees: Bland-Hawthorn & Gerhard 2016");
            Assert.Equal(5.0, GalaxyModel.BarHalfLengthKpc, 0.0, "long-bar half-length, kpc");

            Double3 near = GalaxyModel.BarAxis * (GalaxyModel.BarHalfLengthKpc * 1000.0);
            Double3 helio = GalaxyModel.ToHeliocentricPc(near);
            double l = Math.Atan2(helio.y, helio.x) * MathD.Rad2Deg;
            Assert.True(l > 0.0 && l < 90.0,
                $"the near end of the bar came out at l = {l:F1}. It is in the first Galactic " +
                "quadrant, and a sign slip on the bar angle puts it in the fourth.");
            Assert.Equal(1.0, GalaxyModel.BarAxis.Magnitude, 1e-12, "the bar axis is a unit vector");
        }

        [Test("the disc model falls off outward and away from the plane, from the Sun")]
        public static void DiscDensityFallsOff()
        {
            Double3 sun = GalaxyModel.ToGalactocentricPc(Double3.Zero);
            double atSun = GalaxyModel.DiscDensity(sun);
            Assert.Relative(1.04, atSun, 1e-9,
                "normalised to 1 at the Sun for the thin disc, plus the 4% thick disc");

            Double3 outward = GalaxyModel.ToGalactocentricPc(new Double3(-2600.0, 0, 0));
            Assert.True(GalaxyModel.DiscDensity(outward) < atSun / 2.0,
                "one thin-disc scale length farther out should more than halve the density");

            Double3 above = GalaxyModel.ToGalactocentricPc(new Double3(0, 0, 300.0));
            Assert.True(GalaxyModel.DiscDensity(above) < atSun,
                "300 pc above the plane is one thin-disc scale height");
            Assert.True(GalaxyModel.DiscDensity(above) > atSun / Math.E / 2.0,
                "but the thick disc keeps it from falling off as fast as the thin disc alone");

            Assert.True(GalaxyModel.Provenance["disc"].Class == ProvenanceClass.Inferred,
                "the disc is a model, never a map, and its provenance must say so");
            Assert.True(GalaxyModel.Provenance["R0"].Class == ProvenanceClass.Measured &&
                        GalaxyModel.Provenance["R0"].HasUncertainty,
                "R0 is measured, with a published uncertainty");
        }

        [Test("the nearest arm to the Sun is the Local arm, and the far side has none")]
        public static void NearestArm()
        {
            SpiralArm arm;
            double offset;
            bool found = GalaxyModel.TryNearestArm(GalaxyModel.ToGalactocentricPc(Double3.Zero),
                out arm, out offset);
            Assert.True(found, "some arm should be fitted at the Sun's own azimuth");
            Assert.True(arm.Name == "Local",
                $"the nearest arm to the Sun should be the Local arm, got {arm.Name}");
            Assert.True(offset < 2.0,
                $"and the Sun is {offset:F2} arm widths from its ridge line");

            // beta = 180 is directly behind the Galactic Centre, where nothing was fitted.
            Double3 behind = new Double3(8150.0, 0.0, 0.0);
            Assert.True(GalaxyModel.AzimuthDeg(behind) > 179.0 ||
                        GalaxyModel.AzimuthDeg(behind) < -179.0,
                "this point should be at beta = 180");
            Assert.False(GalaxyModel.TryNearestArm(behind, out arm, out offset),
                "no arm was fitted on the far side of the Galactic Centre, and saying so is " +
                "more honest than extrapolating one there");
        }
    }
}
