using System;

namespace CosmoSynth.Core
{
    /// <summary>Which rest frame the dipole is quoted in. The legend always states the frame.</summary>
    public enum CmbFrame
    {
        /// <summary>No dipole at all: the anisotropy as the shipped map holds it.</summary>
        None = 0,

        /// <summary>The Solar System barycentre, moving at 369 km/s. This is the measured one.</summary>
        Sun = 1,

        /// <summary>The Local Group, 627 km/s. What the sky would look like without our own orbit.</summary>
        LocalGroup = 2,

        /// <summary>The galactic centre. Derived, not measured — see <see cref="CmbModel"/>.</summary>
        GalacticCentre = 3,
    }

    /// <summary>
    /// The parts of the cosmic microwave background that are numbers rather than a texture:
    /// the monopole, the dipole in three rest frames, and the two distances that put the top
    /// rung on the scale ladder.
    ///
    /// The shipped map has the monopole AND the dipole removed — that is why its rms is 71 µK
    /// and not 3355. Everything needed to put either one back is here, so the app adds them in
    /// a shader and says which mode it is in, instead of baking one interpretation into an
    /// asset that can then never be questioned.
    ///
    /// Source discipline: the map is WMAP (NASA, CC0) because ESA's Planck archive is CC BY-NC
    /// and this project has a commercial bar, so the dipole and the frame velocities are taken
    /// from WMAP and COBE papers rather than from Planck's. The ΛCDM parameters below are a
    /// different kind of thing — published numbers in a paper, not archive data — so they are
    /// the Planck 2018 values the rest of the contract's scale ladder is built on.
    /// </summary>
    public static class CmbModel
    {
        // ---- monopole ------------------------------------------------------

        /// <summary>
        /// The CMB monopole, kelvin. Fixsen 2009 (ApJ 707, 916) from COBE/FIRAS:
        /// 2.72548 ± 0.00057 K. Measured.
        /// </summary>
        public const double MonopoleKelvin = 2.72548;

        /// <summary>One standard deviation on <see cref="MonopoleKelvin"/>, kelvin.</summary>
        public const double MonopoleUncertaintyKelvin = 0.00057;

        /// <summary>Speed of light, km/s. Exact by definition of the metre.</summary>
        public const double LightSpeedKmPerSecond = Constants.SpeedOfLight;

        // ---- dipole --------------------------------------------------------

        /// <summary>
        /// The dipole seen from the Solar System barycentre, microkelvin: 3355 ± 8 µK toward
        /// galactic (263.99°, 48.26°). Hinshaw et al. 2009 (ApJS 180, 225), WMAP five-year.
        /// Measured.
        ///
        /// Deliberately NOT the Planck 2018 value the contract quotes (3362.08 µK toward
        /// 264.021°, 48.253°): the licence amendment removed every Planck data product from
        /// this build, and quoting Planck's dipole beside a WMAP map would invite the app to
        /// say "Planck" about a map that is not. The two differ by 0.2 % and 0.02°, which is
        /// far below anything a headset can show.
        /// </summary>
        public const double DipoleAmplitudeMicrokelvin = 3355.0;

        /// <summary>One standard deviation on <see cref="DipoleAmplitudeMicrokelvin"/>, µK.</summary>
        public const double DipoleUncertaintyMicrokelvin = 8.0;

        /// <summary>Galactic longitude of the dipole apex, degrees.</summary>
        public const double DipoleGalacticLongitudeDeg = 263.99;

        /// <summary>Galactic latitude of the dipole apex, degrees.</summary>
        public const double DipoleGalacticLatitudeDeg = 48.26;

        /// <summary>
        /// Local Group motion with respect to the CMB: 627 ± 22 km/s toward galactic
        /// (276° ± 3, 30° ± 3). Kogut et al. 1993 (ApJ 419, 1), COBE/DMR. Measured.
        /// </summary>
        public const double LocalGroupSpeedKmPerSecond = 627.0;

        public const double LocalGroupGalacticLongitudeDeg = 276.0;
        public const double LocalGroupGalacticLatitudeDeg = 30.0;

        /// <summary>
        /// Circular speed of the local standard of rest about the galactic centre, km/s.
        /// Eilers et al. 2019 (ApJ 871, 120): 229 ± 1 km/s at R0. Used only to derive the
        /// galactic-centre frame below.
        /// </summary>
        public const double LsrCircularSpeedKmPerSecond = 229.0;

        /// <summary>
        /// The Sun's peculiar velocity with respect to the local standard of rest, km/s, in
        /// galactic components (toward the centre, toward l = 90°, toward the north galactic
        /// pole). Schönrich, Binney and Dehnen 2010 (MNRAS 403, 1829): (11.1, 12.24, 7.25).
        /// </summary>
        public static readonly Double3 SolarPeculiarVelocityGalactic = new Double3(11.1, 12.24, 7.25);

        /// <summary>Unit vector toward the dipole apex in galactic coordinates.</summary>
        public static Double3 DipoleDirectionGalactic =>
            StandardFrames.FromSpherical(DipoleGalacticLongitudeDeg * MathD.Deg2Rad,
                                         DipoleGalacticLatitudeDeg * MathD.Deg2Rad);

        /// <summary>
        /// Unit vector toward the dipole apex in ICRF, the frame everything else in the app is
        /// drawn in. RA 167.9287°, Dec −6.9269°.
        ///
        /// This goes through <see cref="StandardFrames.GalacticToEquatorial"/> on purpose. A
        /// private copy of the rotation would make this class immune to a bug in the shared
        /// one, which is precisely the wrong kind of robustness: the CMB must be rotated by
        /// the same matrix as the stars or it is 45° away from them and nothing says so.
        /// </summary>
        public static Double3 DipoleDirectionIcrf =>
            StandardFrames.GalacticToEquatorial * DipoleDirectionGalactic;

        /// <summary>
        /// The speed the dipole implies, km/s: v = c · ΔT / T₀. Derived, and it is a real
        /// check rather than a restatement — 3355 µK over 2.72548 K gives 369.04 km/s, and
        /// the published solar speed is 369.0 ± 0.9 km/s.
        /// </summary>
        public static double ImpliedSpeedKmPerSecond =>
            LightSpeedKmPerSecond * DipoleAmplitudeMicrokelvin * 1e-6 / MonopoleKelvin;

        /// <summary>Dipole amplitude in µK implied by a speed, from the same relation.</summary>
        public static double AmplitudeForSpeed(double kmPerSecond) =>
            MonopoleKelvin * kmPerSecond / LightSpeedKmPerSecond * 1e6;

        /// <summary>
        /// Velocity of the galactic centre with respect to the CMB, in galactic components,
        /// km/s. <b>Inferred</b>: it is the Sun's CMB velocity minus the Sun's velocity about
        /// the galactic centre, and the second term depends on a circular speed the literature
        /// puts anywhere between 220 and 240 km/s, which moves the answer by about ±8 km/s.
        /// Comes out near 556 km/s toward (265.7°, 28.8°).
        /// </summary>
        public static Double3 GalacticCentreVelocityGalactic
        {
            get
            {
                Double3 sun = DipoleDirectionGalactic * ImpliedSpeedKmPerSecond;
                Double3 aboutCentre = new Double3(SolarPeculiarVelocityGalactic.x,
                                                  LsrCircularSpeedKmPerSecond + SolarPeculiarVelocityGalactic.y,
                                                  SolarPeculiarVelocityGalactic.z);
                return sun - aboutCentre;
            }
        }

        /// <summary>Dipole amplitude (µK) and apex direction (ICRF) for a rest frame.</summary>
        public static void Dipole(CmbFrame frame, out double amplitudeMicrokelvin, out Double3 apexIcrf)
        {
            Double3 galactic;
            switch (frame)
            {
                case CmbFrame.Sun:
                    amplitudeMicrokelvin = DipoleAmplitudeMicrokelvin;
                    galactic = DipoleDirectionGalactic;
                    break;
                case CmbFrame.LocalGroup:
                    amplitudeMicrokelvin = AmplitudeForSpeed(LocalGroupSpeedKmPerSecond);
                    galactic = StandardFrames.FromSpherical(
                        LocalGroupGalacticLongitudeDeg * MathD.Deg2Rad,
                        LocalGroupGalacticLatitudeDeg * MathD.Deg2Rad);
                    break;
                case CmbFrame.GalacticCentre:
                    Double3 v = GalacticCentreVelocityGalactic;
                    amplitudeMicrokelvin = AmplitudeForSpeed(v.Magnitude);
                    galactic = v.Normalized;
                    break;
                default:
                    amplitudeMicrokelvin = 0.0;
                    apexIcrf = Double3.UnitZ;
                    return;
            }
            apexIcrf = StandardFrames.GalacticToEquatorial * galactic;
        }

        /// <summary>
        /// The dipole contribution in µK along an ICRF direction: A·(n̂·d̂). Zero for
        /// <see cref="CmbFrame.None"/>, which is the mode the shipped texture already is.
        /// </summary>
        public static double DipoleMicrokelvin(Double3 directionIcrf, CmbFrame frame)
        {
            if (frame == CmbFrame.None) return 0.0;
            Dipole(frame, out double amplitude, out Double3 apex);
            return amplitude * Double3.Dot(directionIcrf.Normalized, apex);
        }

        // ---- the top rung of the scale ladder ------------------------------

        /// <summary>Hubble constant, km/s/Mpc. Planck 2018 VI, TT,TE,EE+lowE+lensing.</summary>
        public const double HubbleConstant = 67.36;

        /// <summary>Matter density parameter today. Planck 2018 VI.</summary>
        public const double OmegaMatter = 0.3153;

        /// <summary>
        /// Radiation density parameter today: photons at Ω_γ h² = 2.47e-5 (which follows from
        /// <see cref="MonopoleKelvin"/>) plus 3.046 effective massless neutrino species.
        /// Derived, and it matters — leaving radiation out moves the particle horizon by more
        /// than it moves the distance to last scattering.
        /// </summary>
        public static double OmegaRadiation
        {
            get
            {
                double h = HubbleConstant / 100.0;
                double photons = 2.47e-5 / (h * h);
                return photons * (1.0 + 3.046 * (7.0 / 8.0) * System.Math.Pow(4.0 / 11.0, 4.0 / 3.0));
            }
        }

        /// <summary>Dark energy density parameter, flat by construction.</summary>
        public static double OmegaLambda => 1.0 - OmegaMatter - OmegaRadiation;

        /// <summary>Hubble distance c/H₀ in megaparsecs: 4450.6 Mpc.</summary>
        public static double HubbleDistanceMpc => LightSpeedKmPerSecond / HubbleConstant;

        /// <summary>Redshift of last scattering. Planck 2018 VI Table 2: z* = 1089.92 ± 0.25.</summary>
        public const double RedshiftOfLastScattering = 1089.92;

        /// <summary>Age of the universe, gigayears. Planck 2018 VI: 13.797 ± 0.023 Gyr.</summary>
        public const double AgeGigayears = 13.797;

        /// <summary>E(z) = H(z)/H₀ for a flat ΛCDM universe with radiation.</summary>
        public static double ExpansionRate(double z)
        {
            double a1 = 1.0 + z;
            return System.Math.Sqrt(OmegaRadiation * a1 * a1 * a1 * a1
                                    + OmegaMatter * a1 * a1 * a1
                                    + OmegaLambda);
        }

        /// <summary>
        /// Comoving distance in megaparsecs out to a redshift, by Simpson's rule on
        /// u = ln(1 + z). The substitution is not decoration: on a linear grid in z this
        /// integral needs 20000 intervals for four significant figures, because almost all of
        /// the distance is accumulated below z = 10; in u, 200 intervals already agree with
        /// 5000 to 1e-5.
        /// </summary>
        public static double ComovingDistanceMpc(double redshift, int intervals = 400)
        {
            if (redshift <= 0.0) return 0.0;
            if (intervals % 2 != 0) intervals++;
            double uMax = System.Math.Log(1.0 + redshift);
            double h = uMax / intervals;
            double sum = 0.0;
            for (int i = 0; i <= intervals; i++)
            {
                double z = System.Math.Exp(i * h) - 1.0;        // du -> dz is (1 + z) du
                double f = (1.0 + z) / ExpansionRate(z);
                sum += f * (i == 0 || i == intervals ? 1.0 : (i % 2 == 1 ? 4.0 : 2.0));
            }
            return HubbleDistanceMpc * sum * h / 3.0;
        }

        /// <summary>
        /// Comoving distance to the surface of last scattering, gigaparsecs. The integration
        /// gives 13.867 Gpc; Planck 2018 VI publishes r* = 144.43 Mpc and 100θ* = 1.04109,
        /// whose ratio is 13.873 Gpc, so the integrator agrees with a published number it was
        /// never fitted to, to 0.05 %. That is the check, not the value.
        ///
        /// This is where the CMB shell is drawn, in both its forms. It is NOT the edge of the
        /// observable universe — see <see cref="ParticleHorizonGpc"/>, which is 2 % further.
        /// </summary>
        public static double LastScatteringDistanceGpc =>
            ComovingDistanceMpc(RedshiftOfLastScattering) / 1000.0;

        /// <summary>
        /// The comoving particle horizon, gigaparsecs: how far light has travelled since the
        /// beginning, and therefore the actual edge of the observable universe.
        ///
        /// Integrated in the scale factor, ∫ da / (a² E), from a = 1e-10 to 1. The lower limit
        /// costs nothing because the integrand tends to the constant 1/√Ω_r as a → 0, so the
        /// truncated piece is about 1e-8 Mpc.
        ///
        /// It comes out <b>14.147 Gpc</b> (46.1 Gly). CONTRACT §2482 carries 14.26 Gpc and
        /// marks it "INFERRED until the integration is recorded in CmbModel" — this is that
        /// integration, and 14.26 belongs to an older parameter set. Whoever owns the scale
        /// ladder should take the number from here, not from the table.
        /// </summary>
        public static double ParticleHorizonGpc => ParticleHorizonMpc() / 1000.0;

        public static double ParticleHorizonMpc(int intervals = 200000)
        {
            if (intervals % 2 != 0) intervals++;
            const double aMin = 1e-10;
            double h = (1.0 - aMin) / intervals;
            double sum = 0.0;
            for (int i = 0; i <= intervals; i++)
            {
                double a = aMin + i * h;
                double f = 1.0 / (a * a * ExpansionRate(1.0 / a - 1.0));
                sum += f * (i == 0 || i == intervals ? 1.0 : (i % 2 == 1 ? 4.0 : 2.0));
            }
            return HubbleDistanceMpc * sum * h / 3.0;
        }

        /// <summary>Megaparsecs to kilometres, from <see cref="Constants.Parsec"/>.</summary>
        public const double MegaparsecKm = Constants.Parsec * 1e6;

        /// <summary>
        /// Radius of the CMB shell in kilometres — the comoving distance to last scattering,
        /// which is where both forms of the layer are drawn. 4.28e23 km.
        /// </summary>
        public static double LastScatteringDistanceKm =>
            ComovingDistanceMpc(RedshiftOfLastScattering) * MegaparsecKm;
    }
}
