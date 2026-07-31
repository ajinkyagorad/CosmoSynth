using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Solvers for Kepler's equation and conversions between the three anomalies.
    ///
    /// Kepler's equation M = E - e sin(E) has no closed-form inverse, so E must be found
    /// iteratively. The elliptic solver uses Danby's cubically-convergent iteration, which
    /// reaches machine precision in about three passes for typical planetary eccentricities.
    /// A bisection fallback guarantees convergence for the pathological corner (e -> 1 with
    /// M near 0), where Newton-family methods can stall or overshoot: there the derivative
    /// 1 - e cos(E) approaches zero exactly where the solution lives.
    /// </summary>
    public static class KeplerSolver
    {
        /// <summary>Convergence threshold on the anomaly, radians. Near the double-precision floor.</summary>
        public const double DefaultTolerance = 1e-13;

        public const int DefaultMaxIterations = 60;

        /// <summary>Eccentricities within this distance of 1 are treated as parabolic.</summary>
        public const double ParabolicTolerance = 1e-10;

        /// <summary>Orbits below this eccentricity are treated as circular (periapsis undefined).</summary>
        public const double CircularTolerance = 1e-10;

        /// <summary>Inclinations within this of 0 or pi are treated as equatorial (node undefined).</summary>
        public const double EquatorialTolerance = 1e-10;

        // ---- Elliptic ------------------------------------------------------

        /// <summary>
        /// Solve M = E - e sin(E) for the eccentric anomaly E, given mean anomaly M and 0 &lt;= e &lt; 1.
        /// The returned E is in the same revolution as the supplied M.
        /// </summary>
        public static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity,
            double tolerance = DefaultTolerance, int maxIterations = DefaultMaxIterations)
        {
            if (eccentricity < 0.0 || eccentricity >= 1.0)
                throw new ArgumentOutOfRangeException(nameof(eccentricity),
                    $"Elliptic solver requires 0 <= e < 1, got {eccentricity}.");

            if (eccentricity < CircularTolerance) return meanAnomaly;

            // Work in (-pi, pi] where the solver is best conditioned, then add the revolutions back.
            double revolutions = System.Math.Floor((meanAnomaly + MathD.Pi) / MathD.TwoPi);
            double m = meanAnomaly - revolutions * MathD.TwoPi;

            // Danby's starter: displacing the guess toward the periapsis side of M by 0.85e
            // keeps the first Newton step from jumping past the root at high eccentricity.
            double e = eccentricity;
            double eccentric = m + 0.85 * e * MathD.Sign(System.Math.Sin(m));
            if (System.Math.Sin(m) == 0.0) eccentric = m;

            bool converged = false;
            for (int i = 0; i < maxIterations; i++)
            {
                double s = e * System.Math.Sin(eccentric);
                double c = e * System.Math.Cos(eccentric);
                double f = eccentric - s - m;
                double f1 = 1.0 - c;      // f'
                double f2 = s;            // f''
                double f3 = c;            // f'''

                if (System.Math.Abs(f) < tolerance) { converged = true; break; }
                if (f1 == 0.0) break;     // stalled: hand over to bisection

                double d1 = -f / f1;
                double d2 = -f / (f1 + 0.5 * d1 * f2);
                double d3 = -f / (f1 + 0.5 * d2 * f2 + d2 * d2 * f3 / 6.0);

                eccentric += d3;

                if (System.Math.Abs(d3) < tolerance) { converged = true; break; }
            }

            if (!converged)
            {
                // Guaranteed bracket: E - M = e sin(E), so E always lies within e of M.
                eccentric = BisectEccentricAnomaly(m, e, tolerance, maxIterations * 4);
            }

            return eccentric + revolutions * MathD.TwoPi;
        }

        private static double BisectEccentricAnomaly(double m, double e, double tolerance, int maxIterations)
        {
            double lo = m - e;
            double hi = m + e;
            double flo = lo - e * System.Math.Sin(lo) - m;

            for (int i = 0; i < maxIterations; i++)
            {
                double mid = 0.5 * (lo + hi);
                double fmid = mid - e * System.Math.Sin(mid) - m;
                if (System.Math.Abs(fmid) < tolerance || (hi - lo) < tolerance) return mid;

                if (MathD.Sign(fmid) == MathD.Sign(flo)) { lo = mid; flo = fmid; }
                else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        /// <summary>Mean anomaly from eccentric anomaly (the forward, closed-form direction).</summary>
        public static double MeanFromEccentric(double eccentricAnomaly, double eccentricity) =>
            eccentricAnomaly - eccentricity * System.Math.Sin(eccentricAnomaly);

        /// <summary>True anomaly from eccentric anomaly. The atan2 form is quadrant-safe.</summary>
        public static double TrueFromEccentric(double eccentricAnomaly, double eccentricity)
        {
            double beta = System.Math.Sqrt(1.0 - eccentricity * eccentricity);
            return System.Math.Atan2(beta * System.Math.Sin(eccentricAnomaly),
                                     System.Math.Cos(eccentricAnomaly) - eccentricity);
        }

        /// <summary>Eccentric anomaly from true anomaly.</summary>
        public static double EccentricFromTrue(double trueAnomaly, double eccentricity)
        {
            double beta = System.Math.Sqrt(1.0 - eccentricity * eccentricity);
            return System.Math.Atan2(beta * System.Math.Sin(trueAnomaly),
                                     eccentricity + System.Math.Cos(trueAnomaly));
        }

        // ---- Hyperbolic ----------------------------------------------------

        /// <summary>
        /// Solve M = e sinh(H) - H for the hyperbolic anomaly H, given e &gt; 1.
        /// Newton's method converges globally here because the function is strictly monotonic
        /// and convex away from the origin; the starter just has to avoid overflowing sinh.
        /// </summary>
        public static double SolveHyperbolicAnomaly(double meanAnomaly, double eccentricity,
            double tolerance = DefaultTolerance, int maxIterations = DefaultMaxIterations)
        {
            if (eccentricity <= 1.0)
                throw new ArgumentOutOfRangeException(nameof(eccentricity),
                    $"Hyperbolic solver requires e > 1, got {eccentricity}.");

            double m = meanAnomaly;
            double e = eccentricity;

            // Two regimes: near the origin M ~ (e-1)H, far out M ~ (e/2)exp(H).
            double h;
            double absM = System.Math.Abs(m);
            if (absM < 6.0 * (e - 1.0))
            {
                h = m / (e - 1.0);
                // The linear starter overestimates badly once the cubic term bites; damp it.
                double cube = System.Math.Pow(6.0 * absM / e, 1.0 / 3.0);
                if (System.Math.Abs(h) > cube && cube > 0.0) h = MathD.Sign(m) * cube;
            }
            else
            {
                h = MathD.Sign(m) * System.Math.Log(2.0 * absM / e + 1.8);
            }

            for (int i = 0; i < maxIterations; i++)
            {
                double sinh = System.Math.Sinh(h);
                double cosh = System.Math.Cosh(h);
                double f = e * sinh - h - m;
                double f1 = e * cosh - 1.0;
                if (System.Math.Abs(f) < tolerance * System.Math.Max(1.0, absM)) break;
                if (f1 == 0.0) break;

                double step = f / f1;
                // Cap the step: an unconstrained Newton step from a poor starter can leap into
                // the exponential tail, where sinh overflows and the iteration never returns.
                if (System.Math.Abs(step) > 1.0) step = MathD.Sign(step);
                h -= step;
                if (System.Math.Abs(step) < tolerance) break;
            }

            return h;
        }

        /// <summary>Mean anomaly from hyperbolic anomaly.</summary>
        public static double MeanFromHyperbolic(double hyperbolicAnomaly, double eccentricity) =>
            eccentricity * System.Math.Sinh(hyperbolicAnomaly) - hyperbolicAnomaly;

        /// <summary>True anomaly from hyperbolic anomaly.</summary>
        public static double TrueFromHyperbolic(double hyperbolicAnomaly, double eccentricity)
        {
            double beta = System.Math.Sqrt(eccentricity * eccentricity - 1.0);
            return System.Math.Atan2(beta * System.Math.Sinh(hyperbolicAnomaly),
                                     eccentricity - System.Math.Cosh(hyperbolicAnomaly));
        }

        /// <summary>
        /// Hyperbolic anomaly from true anomaly. Returns NaN if the true anomaly lies beyond
        /// the asymptote, i.e. on the unreachable branch of the hyperbola.
        /// </summary>
        public static double HyperbolicFromTrue(double trueAnomaly, double eccentricity)
        {
            double denom = 1.0 + eccentricity * System.Math.Cos(trueAnomaly);
            if (denom <= 0.0) return double.NaN;
            double beta = System.Math.Sqrt(eccentricity * eccentricity - 1.0);
            return Asinh(beta * System.Math.Sin(trueAnomaly) / denom);
        }

        // ---- Parabolic (Barker's equation) ----------------------------------

        /// <summary>
        /// Barker's equation solved in closed form: given the parabolic mean anomaly
        /// M_p = D + D^3/3 with D = tan(nu/2), recover the true anomaly. Unlike the elliptic
        /// case this inversion is exact — it is a depressed cubic with one real root.
        /// </summary>
        public static double TrueFromParabolicMean(double parabolicMeanAnomaly)
        {
            double m = parabolicMeanAnomaly;
            double b = 1.5 * m;
            double discriminant = System.Math.Sqrt(1.0 + b * b);
            double cubeRoot = System.Math.Cbrt(b + discriminant);
            double d = cubeRoot - 1.0 / cubeRoot;
            return 2.0 * System.Math.Atan(d);
        }

        /// <summary>Parabolic mean anomaly from true anomaly.</summary>
        public static double ParabolicMeanFromTrue(double trueAnomaly)
        {
            double d = System.Math.Tan(0.5 * trueAnomaly);
            return d + d * d * d / 3.0;
        }

        // ---- Small helpers --------------------------------------------------

        /// <summary>Inverse hyperbolic sine. Present for parity with older runtimes that lack Math.Asinh.</summary>
        public static double Asinh(double x) => System.Math.Log(x + System.Math.Sqrt(x * x + 1.0));

        /// <summary>Inverse hyperbolic tangent.</summary>
        public static double Atanh(double x) => 0.5 * System.Math.Log((1.0 + x) / (1.0 - x));
    }
}
