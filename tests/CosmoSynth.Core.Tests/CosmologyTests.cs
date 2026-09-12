using System;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The redshift-to-distance path, which every galaxy beyond the Local Volume depends on.
    ///
    /// The reference values are not remembered figures. They were produced on triton by a
    /// **400,000-step midpoint rule**, a different quadrature from the Simpson rule under
    /// test, and the two agree to 2e-11 Mpc at z = 0.5. That is what makes them known
    /// answers rather than a restatement of the code: an error in the integrand, the limits,
    /// c/H₀ or the constants moves Simpson and midpoint together only if the integrand itself
    /// is wrong, and the Hubble-law limit test below catches exactly that case.
    ///
    /// The tolerances are 1e-6 Mpc — about 2e-9 relative, a thousand times tighter than any
    /// change worth noticing and far looser than double-precision noise. A bound that wide
    /// enough to pass proves nothing; this one is set by what the arithmetic is worth.
    /// </summary>
    public static class CosmologyTests
    {
        [Test("the cosmology constants are the ones every shipped file names")]
        public static void ConstantsAreThePlanckOnes()
        {
            Assert.Equal(67.66, Cosmology.H0, 0.0, "H0");
            Assert.Equal(0.6766, Cosmology.LittleH, 0.0, "little h");
            Assert.Equal(0.3150, Cosmology.OmegaM, 0.0, "Omega_m");
            Assert.Equal(1.0, Cosmology.OmegaM + Cosmology.OmegaLambda, 1e-15, "flatness");
            Assert.Equal(4430.8669, Cosmology.HubbleDistanceMpc, 1e-4, "c/H0 in Mpc");
            Assert.True(Cosmology.SimpsonIntervals == 512,
                "the importer uses 512 intervals; changing this alone breaks every shipped position");
        }

        [Test("comoving distance matches an independent 400,000-step quadrature")]
        public static void ComovingDistanceKnownAnswers()
        {
            Assert.Equal(4.429820, Cosmology.ComovingDistanceMpc(0.001), 1e-6, "D_C(0.001)");
            Assert.Equal(13.283174, Cosmology.ComovingDistanceMpc(0.003), 1e-6, "D_C(0.003)");
            Assert.Equal(44.203790, Cosmology.ComovingDistanceMpc(0.01), 1e-6, "D_C(0.01)");
            Assert.Equal(103.106583, Cosmology.ComovingDistanceMpc(0.0234), 1e-6, "D_C(0.0234), Coma");
            Assert.Equal(218.902675, Cosmology.ComovingDistanceMpc(0.05), 1e-6, "D_C(0.05)");
            Assert.Equal(432.442818, Cosmology.ComovingDistanceMpc(0.1), 1e-6, "D_C(0.1)");
            Assert.Equal(843.105797, Cosmology.ComovingDistanceMpc(0.2), 1e-6, "D_C(0.2)");
            Assert.Equal(1598.417725, Cosmology.ComovingDistanceMpc(0.4), 1e-6, "D_C(0.4)");
            Assert.Equal(1943.888307, Cosmology.ComovingDistanceMpc(0.5), 1e-6, "D_C(0.5)");
        }

        [Test("the z to 0 limit is the Hubble law, which is the units check")]
        public static void LowRedshiftLimitIsTheHubbleLaw()
        {
            // D_C -> cz/H0 as z -> 0. It is not exact at finite z, and the size of the
            // shortfall is itself predicted: to first order D_C = (c/H0) z (1 - 3/4 Om z),
            // so at z = 0.001 the ratio should be 1 - 0.75*0.315*0.001 = 0.99976.
            double hubble = Cosmology.SpeedOfLightKmS * 0.001 / Cosmology.H0;
            double ratio = Cosmology.ComovingDistanceMpc(0.001) / hubble;
            Assert.Equal(0.99976, ratio, 2e-5, "D_C(0.001) / (cz/H0)");

            // ... and the FRACTIONAL shortfall is linear in z, so at z = 0.01 it is ten
            // times larger - not the same and not a hundred times larger. A wrong Omega_m
            // can be absorbed into H0 by the first check; it cannot survive this one.
            double shortfall001 = 1.0 - ratio;
            double shortfall01 = 1.0 - Cosmology.ComovingDistanceMpc(0.01)
                / (Cosmology.SpeedOfLightKmS * 0.01 / Cosmology.H0);
            Assert.Equal(10.0, shortfall01 / shortfall001, 0.1,
                "the first-order shortfall is linear in z");
        }

        [Test("redshift and comoving distance round-trip through each other")]
        public static void RedshiftRoundTrips()
        {
            foreach (double z in new[] { 0.003, 0.01, 0.05, 0.1, 0.12, 0.2, 0.3, 0.4, 0.5, 1.0, 3.5 })
            {
                double d = Cosmology.ComovingDistanceMpc(z);
                double back = Cosmology.RedshiftAtComovingDistanceMpc(d);
                Assert.Relative(z, back, 1e-12, $"round trip at z = {z}");
            }
            Assert.Equal(0.0, Cosmology.ComovingDistanceMpc(0.0), 0.0, "D_C(0) is zero");
            Assert.Equal(0.0, Cosmology.RedshiftAtComovingDistanceMpc(0.0), 0.0, "z(0) is zero");
        }

        [Test("comoving distance increases strictly, so the inverse is well defined")]
        public static void ComovingDistanceIsMonotonic()
        {
            double previous = -1.0;
            for (int i = 0; i <= 200; i++)
            {
                double z = i * 0.02;
                double d = Cosmology.ComovingDistanceMpc(z);
                Assert.True(d > previous, $"D_C is not increasing at z = {z}: {d} <= {previous}");
                previous = d;
            }
        }

        [Test("the distance modulus is the magnitude scale, not a cosmology")]
        public static void DistanceModulus()
        {
            // LVDB's own probe row: the LMC at DM 18.477 is 49.59 kpc (Pietrzynski et al.
            // 2019). This is the arithmetic every dwarf's distance in the shipped file rests
            // on, and the importer asserts the file agrees with it to 0.5%.
            Assert.Relative(49590.0, Cosmology.DistanceFromModulusPc(18.477), 1e-4,
                "LMC distance from DM 18.477, parsecs");
            Assert.Equal(10.0, Cosmology.DistanceFromModulusPc(0.0), 1e-12, "DM 0 is 10 pc");
            Assert.Equal(0.0, Cosmology.ModulusFromDistancePc(10.0), 1e-12, "10 pc is DM 0");
            Assert.Equal(18.477, Cosmology.ModulusFromDistancePc(
                Cosmology.DistanceFromModulusPc(18.477)), 1e-12, "modulus round trip");

            // Sirius: V = -1.44 at 2.64 pc gives M_V = -1.44 - 5log10(2.64) + 5 = +1.452.
            Assert.Equal(1.452, Cosmology.AbsoluteMagnitude(-1.44, 2.64), 1e-3,
                "Sirius absolute magnitude");
        }

        [Test("Mpc/h converts with the h this cosmology names, not another one")]
        public static void LittleHConversion()
        {
            // The staged void catalogue is in comoving Mpc/h. Dividing by the wrong h is a
            // 10% error that looks entirely plausible: CF4's h = 0.746 would put a 15.47 Mpc/h
            // void at 20.7 Mpc instead of 22.9.
            Assert.Equal(22.8643, Cosmology.MpcPerHToMpc(15.47), 1e-3,
                "the catalogue's median void radius, 15.47 Mpc/h");
            Assert.Equal(100.0 / 0.6766, Cosmology.MpcPerHToMpc(100.0), 1e-9, "100 Mpc/h");
        }
    }
}
