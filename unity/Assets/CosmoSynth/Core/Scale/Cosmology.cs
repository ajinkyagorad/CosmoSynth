using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// The one cosmology. Every redshift in the app becomes a distance through this class and
    /// through nothing else.
    ///
    ///     flat ΛCDM, H₀ = 67.66 km/s/Mpc (h = 0.6766), Ωm = 0.3150
    ///     Planck 2018 TT,TE,EE+lowE+lensing+BAO
    ///
    /// **Why this one and not design B's H₀ = 74.6.** That figure was Cosmicflows-4's zero
    /// point, and CF4 is blocked by docs/DATA-LICENCE-AMENDMENT.md §3, so its calibration goes
    /// with it. The staged void catalogue is the Planck2018 variant — its own coordinates
    /// invert to this cosmology — and the CMB layer is Planck-based. The two choices differ by
    /// about 10%, which is enough to put one cluster in two places, so the constant lives here
    /// once and is named in every shipped file's header.
    ///
    /// **A comoving distance from a redshift is Inferred, never Measured.** The redshift is
    /// measured; turning it into a length assumes a model, and it places the object in
    /// *redshift space*, where a cluster's own motion smears it radially into a finger of God
    /// ten megaparsecs long. Nothing here corrects for that, because the correction is not
    /// knowable per object.
    /// </summary>
    public static class Cosmology
    {
        /// <summary>Hubble constant, km/s/Mpc.</summary>
        public const double H0 = 67.66;

        /// <summary>H₀ / 100, the "little h" the void catalogue's units carry.</summary>
        public const double LittleH = 0.6766;

        /// <summary>Matter density parameter.</summary>
        public const double OmegaM = 0.3150;

        /// <summary>Dark energy density parameter; the model is flat, so this is 1 − Ωm.</summary>
        public const double OmegaLambda = 1.0 - OmegaM;

        /// <summary>Speed of light, km/s (exact, SI definition).</summary>
        public const double SpeedOfLightKmS = 299792.458;

        /// <summary>c/H₀ = 4430.87 Mpc, the scale every comoving distance is measured in.</summary>
        public const double HubbleDistanceMpc = SpeedOfLightKmS / H0;

        /// <summary>
        /// Simpson intervals. **This must stay 512**: tools/cosmodata/build_lss.py uses the
        /// same rule with the same count to place every shipped galaxy, so changing it here
        /// alone stops the shipped positions round-tripping. 512 agrees with a 400,000-step
        /// midpoint rule to 2e-11 Mpc at z = 0.5, so it is not a compromise, it is a contract.
        /// </summary>
        public const int SimpsonIntervals = 512;

        public const string Reference = "Planck 2018 TT,TE,EE+lowE+lensing+BAO";

        /// <summary>E(z) = H(z)/H₀ for a flat ΛCDM model.</summary>
        public static double E(double z)
        {
            double a = 1.0 + z;
            return Math.Sqrt(OmegaM * a * a * a + OmegaLambda);
        }

        /// <summary>
        /// Line-of-sight comoving distance, megaparsecs:
        /// D_C(z) = (c/H₀) ∫₀^z dz′/E(z′), by Simpson's rule.
        /// </summary>
        public static double ComovingDistanceMpc(double z)
        {
            if (double.IsNaN(z)) return double.NaN;
            if (z <= 0.0) return 0.0;

            int n = SimpsonIntervals;
            double h = z / n;
            double total = 1.0;                       // 1/E(0) = 1
            for (int i = 1; i < n; i++)
            {
                double weight = (i & 1) != 0 ? 4.0 : 2.0;
                total += weight / E(i * h);
            }
            total += 1.0 / E(z);
            return HubbleDistanceMpc * total * h / 3.0;
        }

        /// <summary>
        /// The inverse: the redshift whose comoving distance is <paramref name="distanceMpc"/>.
        ///
        /// D_C is strictly increasing, so this is a bisection with a Newton polish — 60
        /// iterations of halving takes the bracket below double precision, and the derivative
        /// (c/H₀)/E(z) is exact, so the polish costs one step and removes the last ulps.
        /// This is what makes "the shipped positions round-trip" a test rather than a hope.
        /// </summary>
        public static double RedshiftAtComovingDistanceMpc(double distanceMpc)
        {
            if (double.IsNaN(distanceMpc)) return double.NaN;
            if (distanceMpc <= 0.0) return 0.0;

            double lo = 0.0, hi = 1.0;
            while (ComovingDistanceMpc(hi) < distanceMpc)
            {
                lo = hi;
                hi *= 2.0;
                if (hi > 1e6)
                {
                    throw new ArgumentOutOfRangeException(nameof(distanceMpc),
                        distanceMpc, "beyond any redshift this model describes");
                }
            }

            for (int i = 0; i < 60 && hi - lo > 1e-14 * (1.0 + hi); i++)
            {
                double mid = 0.5 * (lo + hi);
                if (ComovingDistanceMpc(mid) < distanceMpc) lo = mid; else hi = mid;
            }

            double z = 0.5 * (lo + hi);
            double residual = ComovingDistanceMpc(z) - distanceMpc;
            return z - residual * E(z) / HubbleDistanceMpc;
        }

        /// <summary>
        /// Distance from a distance modulus, in parsecs: d = 10^(DM/5 + 1).
        /// No cosmology in it at all — this is the definition of the magnitude scale, and it
        /// is how every LVDB dwarf's distance is stored.
        /// </summary>
        public static double DistanceFromModulusPc(double distanceModulus) =>
            Math.Pow(10.0, distanceModulus / 5.0 + 1.0);

        /// <summary>Distance modulus from a distance in parsecs.</summary>
        public static double ModulusFromDistancePc(double parsecs) =>
            5.0 * Math.Log10(parsecs) - 5.0;

        /// <summary>Redshift from a recession velocity in km/s, cz = v. Small-z only.</summary>
        public static double RedshiftFromCz(double czKmS) => czKmS / SpeedOfLightKmS;

        /// <summary>Comoving Mpc/h to comoving Mpc. The void catalogue ships the former.</summary>
        public static double MpcPerHToMpc(double value) => value / LittleH;

        /// <summary>Absolute magnitude from an apparent one and a distance in parsecs.</summary>
        public static double AbsoluteMagnitude(double apparent, double parsecs) =>
            apparent - 5.0 * Math.Log10(parsecs) + 5.0;
    }
}
