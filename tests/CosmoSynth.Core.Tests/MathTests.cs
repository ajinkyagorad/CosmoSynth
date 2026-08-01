using System;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    public static class MathTests
    {
        [Test("Double3 algebra follows the usual identities")]
        public static void Double3AlgebraHolds()
        {
            var a = new Double3(1, 2, 3);
            var b = new Double3(-4, 5, 6);

            Assert.Equal(24.0, Double3.Dot(a, b), 1e-12, "dot product");   // -4 + 10 + 18
            Assert.VectorClose(new Double3(-3, -18, 13), Double3.Cross(a, b), 1e-12, "cross product");
            Assert.Equal(0.0, Double3.Dot(Double3.Cross(a, b), a), 1e-12, "cross product is perpendicular to a");
            Assert.Equal(0.0, Double3.Dot(Double3.Cross(a, b), b), 1e-12, "cross product is perpendicular to b");
            Assert.VectorClose(-Double3.Cross(b, a), Double3.Cross(a, b), 1e-12, "cross product is antisymmetric");
            Assert.Equal(System.Math.Sqrt(14.0), a.Magnitude, 1e-12, "magnitude");
            Assert.Equal(1.0, a.Normalized.Magnitude, 1e-12, "normalised magnitude");
        }

        [Test("normalising a zero vector yields zero instead of NaN")]
        public static void NormalisingZeroIsSafe()
        {
            // Reached routinely: the direction to a body you are sitting exactly on top of.
            var result = Double3.Zero.Normalized;
            Assert.True(result.IsFinite, "normalising zero must not produce NaN");
            Assert.VectorClose(Double3.Zero, result, 0.0, "normalised zero");
        }

        [Test("angle between vectors stays accurate when they are nearly parallel")]
        public static void AngleIsAccurateForNearlyParallelVectors()
        {
            // acos(dot) loses roughly half its significant digits here; atan2 does not. The
            // case matters because comparing the orbital planes of two planets means taking
            // the angle between two nearly identical normals.
            var reference = new Double3(0, 0, 1);
            foreach (double expected in new[] { 1e-8, 1e-6, 1e-4, 1e-2 })
            {
                var tilted = Double3.Rotate(reference, Double3.UnitX, expected);
                double measured = Double3.Angle(reference, tilted);
                Assert.Relative(expected, measured, 1e-9, $"angle of {expected:G3} rad");
            }
        }

        [Test("Rodrigues rotation preserves length and reverses cleanly")]
        public static void RotationPreservesLengthAndReverses()
        {
            var v = new Double3(3, -1, 2);
            var axis = new Double3(1, 1, 0);

            for (int i = 0; i < 16; i++)
            {
                double angle = MathD.TwoPi * i / 16.0;
                var rotated = Double3.Rotate(v, axis, angle);
                Assert.Relative(v.Magnitude, rotated.Magnitude, 1e-12, $"length preserved at {angle:F3} rad");

                var back = Double3.Rotate(rotated, axis, -angle);
                Assert.VectorClose(v, back, 1e-12, $"rotation reverses at {angle:F3} rad");
            }

            // Rotating about an axis leaves the component along that axis untouched.
            var rotatedQuarter = Double3.Rotate(v, axis, MathD.HalfPi);
            Assert.Equal(Double3.Dot(v, axis.Normalized), Double3.Dot(rotatedQuarter, axis.Normalized), 1e-12,
                "axial component is invariant");
        }

        [Test("projection splits a vector into parallel and perpendicular parts")]
        public static void ProjectionSplitsCleanly()
        {
            var v = new Double3(3, 4, 5);
            var onto = new Double3(0, 2, 0);

            var parallel = Double3.Project(v, onto);
            var perpendicular = Double3.ProjectOnPlane(v, onto);

            Assert.VectorClose(v, parallel + perpendicular, 1e-12, "parts recombine to the original");
            Assert.Equal(0.0, Double3.Dot(perpendicular, onto), 1e-12, "perpendicular part really is perpendicular");
            Assert.VectorClose(new Double3(0, 4, 0), parallel, 1e-12, "parallel part");

            // Degenerate axis must not divide by zero.
            Assert.VectorClose(Double3.Zero, Double3.Project(v, Double3.Zero), 0.0, "projection onto a zero axis");
        }

        [Test("angle wrapping lands in the documented ranges")]
        public static void AngleWrappingIsCorrect()
        {
            Assert.Equal(0.5, MathD.WrapTwoPi(0.5), 1e-12, "WrapTwoPi leaves in-range values alone");
            Assert.Equal(0.5, MathD.WrapTwoPi(0.5 + 4.0 * MathD.TwoPi), 1e-12, "WrapTwoPi removes revolutions");
            Assert.Equal(MathD.TwoPi - 0.5, MathD.WrapTwoPi(-0.5), 1e-12, "WrapTwoPi maps negatives into [0,2pi)");

            Assert.Equal(-0.5, MathD.WrapPi(-0.5), 1e-12, "WrapPi leaves in-range values alone");
            Assert.Equal(-0.5, MathD.WrapPi(-0.5 - 6.0 * MathD.TwoPi), 1e-12, "WrapPi removes revolutions");

            for (int i = -20; i <= 20; i++)
            {
                double a = i * 0.7;
                double wrapped = MathD.WrapTwoPi(a);
                Assert.True(wrapped >= 0.0 && wrapped < MathD.TwoPi, $"WrapTwoPi({a}) = {wrapped} escaped [0,2pi)");
                double wrappedPi = MathD.WrapPi(a);
                Assert.True(wrappedPi > -MathD.Pi - 1e-12 && wrappedPi <= MathD.Pi + 1e-12,
                    $"WrapPi({a}) = {wrappedPi} escaped (-pi,pi]");
            }

            // The shortest way from 359 degrees to 1 degree is +2 degrees, not -358.
            Assert.Equal(2.0 * MathD.Deg2Rad, MathD.DeltaAngle(361.0 * MathD.Deg2Rad, 359.0 * MathD.Deg2Rad), 1e-12,
                "delta angle takes the short way round");
        }

        [Test("safe inverse trig tolerates arguments nudged out of range by rounding")]
        public static void SafeInverseTrigClamps()
        {
            Assert.Equal(0.0, MathD.SafeAcos(1.0 + 1e-16), 1e-12, "acos just past 1");
            Assert.Equal(MathD.Pi, MathD.SafeAcos(-1.0 - 1e-16), 1e-12, "acos just past -1");
            Assert.Equal(MathD.HalfPi, MathD.SafeAsin(1.0 + 1e-16), 1e-12, "asin just past 1");
            Assert.True(!double.IsNaN(MathD.SafeAcos(2.0)), "SafeAcos must never return NaN");
        }

        [Test("derived physical helpers agree with hand calculations")]
        public static void PhysicalHelpersAgree()
        {
            // Earth's surface gravity from GM and radius.
            Assert.Equal(9.798, Constants.SurfaceGravity(Constants.GM_Earth, Constants.EarthEquatorialRadius), 0.01,
                "Earth surface gravity (m/s^2)");

            // Low Earth orbit speed and period.
            double leoRadius = Constants.EarthEquatorialRadius + 400.0;
            Assert.Equal(7.669, Constants.CircularSpeed(Constants.GM_Earth, leoRadius), 0.01, "400 km orbital speed (km/s)");
            Assert.Equal(11.18, Constants.EscapeSpeed(Constants.GM_Earth, Constants.EarthEquatorialRadius), 0.01,
                "Earth escape speed (km/s)");

            // Geostationary radius derived from the sidereal day.
            const double siderealDay = 86164.0905;
            double geoRadius = System.Math.Cbrt(Constants.GM_Earth * siderealDay * siderealDay / (4.0 * MathD.Pi * MathD.Pi));
            Assert.Relative(42164.0, geoRadius, 1e-4, "geostationary radius (km)");

            // Earth's equilibrium temperature: the textbook 255 K, which is 33 K below the
            // real mean surface temperature because this ignores the greenhouse effect.
            double equilibrium = Constants.EquilibriumTemperature(
                Constants.SolarLuminosity, Constants.AstronomicalUnit, 0.306);
            Assert.Equal(254.0, equilibrium, 2.0, "Earth equilibrium temperature (K)");

            // Earth's Hill sphere is about 1.5 million km; the Moon at 384000 km sits well inside it.
            double hill = Constants.HillRadius(Constants.GM_Earth, Constants.GM_Sun, Constants.AstronomicalUnit);
            Assert.Equal(1.496e6, hill, 5e4, "Earth Hill radius (km)");
            Assert.True(hill > 384400.0, "the Moon must lie inside Earth's Hill sphere");

            // Earth's sphere of influence is the smaller ~925000 km figure.
            double soi = Constants.SphereOfInfluence(Constants.GM_Earth, Constants.GM_Sun, Constants.AstronomicalUnit);
            Assert.Equal(9.25e5, soi, 2e4, "Earth sphere of influence (km)");
        }

        [Test("astronomical unit conversions are self-consistent")]
        public static void AstronomicalUnitsAreConsistent()
        {
            // A parsec is 648000/pi AU by the IAU 2015 definition.
            Assert.Relative(648000.0 / MathD.Pi * Constants.AstronomicalUnit, Constants.Parsec, 1e-12,
                "parsec derived from the AU");

            // A light year is c times a Julian year.
            Assert.Relative(Constants.SpeedOfLight * Constants.SecondsPerJulianYear, Constants.LightYear, 1e-12,
                "light year derived from c");

            // A parsec is about 3.26 light years.
            Assert.Equal(3.2616, Constants.Parsec / Constants.LightYear, 1e-3, "parsec in light years");

            // Solar mass derived from GM/G lands on the accepted ~1.989e30 kg.
            Assert.Relative(1.98841e30, Constants.SolarMass, 1e-4, "solar mass (kg)");
            Assert.Relative(5.9722e24, Constants.EarthMass, 1e-3, "Earth mass (kg)");

            // The Sun-to-Earth mass ratio is the well-known 332946.
            Assert.Relative(332946.0, Constants.GM_Sun / Constants.GM_Earth, 1e-4, "Sun/Earth mass ratio");
        }
    }
}
