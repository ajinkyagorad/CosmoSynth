using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The CMB's numbers: the monopole, the dipole in three rest frames, and the two distances
    /// the scale ladder's top rung is built on.
    ///
    /// Two of these are known-answer tests in the strong sense — the answer was published by
    /// someone else and the code was not fitted to it. The dipole's implied speed is
    /// c·ΔT/T₀ = 369.04 km/s against a published 369.0 ± 0.9, and the comoving distance to
    /// last scattering comes out 13867 Mpc against Planck 2018's own r*/θ* = 13873 Mpc. Either
    /// could have failed; a unit slip or a missing radiation term would have moved both.
    /// </summary>
    public static class CmbModelTests
    {
        [Test("the monopole is the FIRAS value")]
        public static void Monopole()
        {
            Assert.Equal(2.72548, CmbModel.MonopoleKelvin, 0.0, "T0, kelvin (Fixsen 2009)");
            Assert.Equal(0.00057, CmbModel.MonopoleUncertaintyKelvin, 0.0, "sigma(T0)");
            // 2.7 K is the whole story of the layer; getting it in millikelvin or in celsius
            // would still look like a number, so pin the magnitude too.
            Assert.True(CmbModel.MonopoleKelvin > 2.7 && CmbModel.MonopoleKelvin < 2.8,
                "T0 is not in kelvin");
        }

        [Test("the dipole amplitude implies the published solar speed, 369 km/s")]
        public static void DipoleImpliesTheSolarSpeed()
        {
            Assert.Equal(3355.0, CmbModel.DipoleAmplitudeMicrokelvin, 0.0,
                "dipole amplitude, uK (Hinshaw 2009, WMAP five-year)");
            // The relation is v = c dT / T0 and nothing in this class was tuned to make it
            // land: 3355 uK over 2.72548 K gives 369.04 km/s. Hinshaw publishes 369.0 +- 0.9
            // independently of the amplitude, so a factor of 1e3 or 1e6 anywhere, or the
            // monopole in the wrong unit, moves this off by orders of magnitude. The bound is
            // the published uncertainty, not something chosen to pass.
            Assert.Equal(369.0, CmbModel.ImpliedSpeedKmPerSecond, 0.9,
                "implied solar speed from c dT / T0, km/s");
            // and the relation inverts
            Assert.Equal(CmbModel.DipoleAmplitudeMicrokelvin,
                CmbModel.AmplitudeForSpeed(CmbModel.ImpliedSpeedKmPerSecond), 1e-9,
                "amplitude -> speed -> amplitude");
        }

        [Test("the dipole apex is at its published galactic position")]
        public static void DipoleApexInGalacticCoordinates()
        {
            Double3 d = CmbModel.DipoleDirectionGalactic;
            Assert.Equal(1.0, d.Magnitude, 1e-12, "the apex direction is a unit vector");
            StandardFrames.ToSpherical(d, out double lon, out double lat, out _);
            Assert.Equal(263.99, lon * MathD.Rad2Deg, 1e-6, "dipole apex galactic longitude");
            Assert.Equal(48.26, lat * MathD.Rad2Deg, 1e-6, "dipole apex galactic latitude");
        }

        [Test("the dipole apex lands at RA 167.93, Dec -6.93 in ICRF")]
        public static void DipoleApexInIcrf()
        {
            // KNOWN RED ON master. This is the only test in the lane that depends on
            // StandardFrames.EquatorialToGalactic, whose three rotation signs are all wrong on
            // master (it sends the north galactic pole to (-0.346, 0.176, 0.922) instead of
            // (0, 0, 1)). The fix is on lane/foundation-code, which this test then passes.
            // Recorded in tools/gates/known-red.txt. It is deliberately NOT worked around with
            // a private copy of the rotation: the CMB must turn with the same matrix as the
            // stars, and a private copy would hide exactly the failure this catches.
            Double3 pole = StandardFrames.FromSpherical(192.85948 * MathD.Deg2Rad,
                                                        27.12825 * MathD.Deg2Rad);
            Double3 mapped = StandardFrames.EquatorialToGalactic * pole;
            Assert.Equal(1.0, mapped.z, 1e-9,
                "StandardFrames.EquatorialToGalactic does not send the north galactic pole to " +
                "+z, so every galactic conversion in the app is wrong. Fix: lane/foundation-code");

            Double3 apex = CmbModel.DipoleDirectionIcrf;
            StandardFrames.ToSpherical(apex, out double ra, out double dec, out _);
            Assert.Equal(167.9287, ra * MathD.Rad2Deg, 0.001, "dipole apex RA, degrees");
            Assert.Equal(-6.9269, dec * MathD.Rad2Deg, 0.001, "dipole apex Dec, degrees");
        }

        [Test("the dipole is a cosine about its apex, zero on the equator between the poles")]
        public static void DipoleIsACosine()
        {
            Double3 apex = CmbModel.DipoleDirectionIcrf;
            Assert.Equal(3355.0, CmbModel.DipoleMicrokelvin(apex, CmbFrame.Sun), 1e-9, "at the apex");
            Assert.Equal(-3355.0, CmbModel.DipoleMicrokelvin(-apex, CmbFrame.Sun), 1e-9, "at the antapex");
            Double3 perpendicular = Double3.Cross(apex, Double3.UnitZ).Normalized;
            Assert.Equal(0.0, CmbModel.DipoleMicrokelvin(perpendicular, CmbFrame.Sun), 1e-9,
                "90 degrees from the apex");
            Assert.Equal(0.0, CmbModel.DipoleMicrokelvin(apex, CmbFrame.None), 0.0,
                "CmbFrame.None is the map as it ships: no dipole at all");
            // magnitude independence: the caller may pass an unnormalised direction
            Assert.Equal(3355.0, CmbModel.DipoleMicrokelvin(apex * 42.0, CmbFrame.Sun), 1e-9,
                "an unnormalised direction");
        }

        [Test("the Local Group frame is the COBE measurement, 627 km/s and 5700 uK")]
        public static void LocalGroupFrame()
        {
            CmbModel.Dipole(CmbFrame.LocalGroup, out double amplitude, out Double3 apex);
            Assert.Equal(5700.2, amplitude, 0.5, "Local Group dipole amplitude, uK");
            Assert.Equal(627.0, CmbModel.LightSpeedKmPerSecond * amplitude * 1e-6 /
                                CmbModel.MonopoleKelvin, 0.1, "implied Local Group speed, km/s");
            // it must be a different direction from the solar one, or the frame switch does
            // nothing: 627 km/s toward (276, 30) against 369 toward (264, 48.3)
            double separation = Double3.Angle(apex, CmbModel.DipoleDirectionIcrf) * MathD.Rad2Deg;
            Assert.True(separation > 15.0 && separation < 30.0,
                $"the Local Group apex is {separation:F1} deg from the solar one; " +
                "expected about 21");
        }

        [Test("the galactic-centre frame is derived, and lands where the literature puts it")]
        public static void GalacticCentreFrameIsDerivedAndPlausible()
        {
            Double3 v = CmbModel.GalacticCentreVelocityGalactic;
            // Subtracting the Sun's 241 km/s orbit from its 369 km/s CMB velocity leaves the
            // galaxy itself moving at about 556 km/s. This is INFERRED, not measured: the
            // circular speed is 220-240 km/s depending on whose paper, which moves the answer
            // by +-8 km/s, and the bound below is that spread and no wider.
            Assert.Equal(556.0, v.Magnitude, 10.0, "galactic-centre frame speed, km/s");
            StandardFrames.ToSpherical(v, out double lon, out double lat, out _);
            Assert.Equal(265.7, lon * MathD.Rad2Deg, 1.0, "apex galactic longitude");
            Assert.Equal(28.8, lat * MathD.Rad2Deg, 1.5, "apex galactic latitude");
            // and it is genuinely between the solar and Local Group frames, as it must be
            CmbModel.Dipole(CmbFrame.GalacticCentre, out double amplitude, out _);
            Assert.True(amplitude > CmbModel.DipoleAmplitudeMicrokelvin &&
                        amplitude < CmbModel.AmplitudeForSpeed(CmbModel.LocalGroupSpeedKmPerSecond),
                $"the galactic-centre dipole {amplitude:F0} uK is not between the solar and " +
                "Local Group ones");
        }

        [Test("the cosmology reproduces Planck's own distance to last scattering")]
        public static void DistanceToLastScattering()
        {
            Assert.Equal(1.0, CmbModel.ExpansionRate(0.0), 1e-12, "E(0) must be exactly 1");
            Assert.Equal(0.0, CmbModel.ComovingDistanceMpc(0.0), 0.0, "no distance to here");
            Assert.Equal(4450.6, CmbModel.HubbleDistanceMpc, 0.1, "Hubble distance c/H0, Mpc");
            // radiation is small today but not optional: leaving it out changes the horizon
            Assert.Equal(9.209e-5, CmbModel.OmegaRadiation, 5e-8, "Omega_r today");
            Assert.Equal(1.0, CmbModel.OmegaMatter + CmbModel.OmegaRadiation + CmbModel.OmegaLambda,
                1e-12, "the universe is flat by construction");

            // THE known-answer test. Planck 2018 VI Table 2 publishes r* = 144.43 Mpc and
            // 100 theta* = 1.04109, whose ratio is the comoving distance to last scattering:
            // 144.43 / 0.0104109 = 13873.4 Mpc. Nothing in ComovingDistanceMpc was fitted to
            // that; it integrates c/H(z) from the density parameters. Agreement is to 0.05 %,
            // so the bound is 0.2 % — wide enough for the quadrature and the parameters'
            // own uncertainties, far too narrow for a missing (1+z) or a dropped radiation
            // term, either of which moves this by whole percent.
            double chi = CmbModel.ComovingDistanceMpc(CmbModel.RedshiftOfLastScattering);
            Assert.Relative(13873.4, chi, 0.002, "comoving distance to last scattering, Mpc");
            Assert.Equal(13.867, CmbModel.LastScatteringDistanceGpc, 0.005, "chi*, Gpc");

            // the quadrature has converged: doubling the intervals must not move it
            Assert.Relative(chi, CmbModel.ComovingDistanceMpc(CmbModel.RedshiftOfLastScattering, 1600),
                1e-6, "Simpson convergence in ln(1+z)");

            // and in kilometres, which is what the scale ladder consumes
            Assert.Relative(4.28e23, CmbModel.LastScatteringDistanceKm, 0.01, "chi*, km");
        }

        [Test("the particle horizon is beyond last scattering, and it is the integrated value")]
        public static void ParticleHorizon()
        {
            double horizon = CmbModel.ParticleHorizonGpc;
            double chi = CmbModel.LastScatteringDistanceGpc;
            Assert.True(horizon > chi,
                $"the horizon {horizon:F3} Gpc must be beyond last scattering {chi:F3} Gpc");
            // Light emitted at last scattering has been travelling for all but 380000 of the
            // universe's 13.8 Gyr, so the horizon is only a couple of percent further. If this
            // ratio came out large, the integration would be reaching into the wrong epoch.
            Assert.Equal(1.0202, horizon / chi, 0.002, "horizon over last-scattering distance");
            // CONTRACT section 2482 carries 14.26 Gpc and marks it INFERRED "until the
            // integration is recorded in CmbModel". This is that integration: 14.147 Gpc with
            // the Planck 2018 parameters above. 14.26 belongs to an older parameter set.
            Assert.Equal(14.147, horizon, 0.01, "particle horizon, Gpc");
            // converged
            Assert.Relative(CmbModel.ParticleHorizonMpc(), CmbModel.ParticleHorizonMpc(400000),
                1e-5, "Simpson convergence in the scale factor");
        }
    }
}
