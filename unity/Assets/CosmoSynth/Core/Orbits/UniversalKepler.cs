using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Two-body propagation by universal variables (the Sundman transformation with Stumpff
    /// functions), following Vallado's formulation.
    ///
    /// This exists alongside <see cref="OrbitalElements.Propagate"/> for two reasons:
    ///
    /// 1. It propagates a state vector directly, with no conic classification, no anomaly
    ///    conversion and no branch on eccentricity. That makes it the right tool for
    ///    spacecraft, whose orbit may pass smoothly through e = 1 during a burn — exactly
    ///    where the classical elements are singular.
    /// 2. It is an algorithmically independent path to the same answer, so the two can be
    ///    cross-validated against each other in the test suite. Agreement between two
    ///    unrelated derivations is much stronger evidence of correctness than either one
    ///    round-tripping against itself.
    /// </summary>
    public static class UniversalKepler
    {
        public const double DefaultTolerance = 1e-11;
        public const int DefaultMaxIterations = 200;

        /// <summary>Below this |1/a| the orbit is treated as parabolic for starter selection.</summary>
        private const double NearParabolicAlpha = 1e-10;

        /// <summary>
        /// Stumpff function c2(psi) = (1 - cos(sqrt(psi))) / psi, continued analytically
        /// through psi = 0 and into psi &lt; 0 (where it becomes the cosh form).
        /// </summary>
        public static double StumpffC2(double psi)
        {
            if (psi > 1e-6)
            {
                double s = System.Math.Sqrt(psi);
                return (1.0 - System.Math.Cos(s)) / psi;
            }
            if (psi < -1e-6)
            {
                double s = System.Math.Sqrt(-psi);
                return (System.Math.Cosh(s) - 1.0) / (-psi);
            }
            // Series near zero: the closed forms are 0/0 there and lose all precision nearby.
            return 0.5 - psi / 24.0 + psi * psi / 720.0 - psi * psi * psi / 40320.0;
        }

        /// <summary>
        /// Stumpff function c3(psi) = (sqrt(psi) - sin(sqrt(psi))) / sqrt(psi)^3, continued
        /// through psi = 0 and into psi &lt; 0.
        /// </summary>
        public static double StumpffC3(double psi)
        {
            if (psi > 1e-6)
            {
                double s = System.Math.Sqrt(psi);
                return (s - System.Math.Sin(s)) / (s * s * s);
            }
            if (psi < -1e-6)
            {
                double s = System.Math.Sqrt(-psi);
                return (System.Math.Sinh(s) - s) / (s * s * s);
            }
            return 1.0 / 6.0 - psi / 120.0 + psi * psi / 5040.0 - psi * psi * psi / 362880.0;
        }

        /// <summary>
        /// Propagate a state vector forward (or backward) by <paramref name="deltaTimeSeconds"/>
        /// under the two-body attraction of a body with parameter <paramref name="gm"/>.
        /// </summary>
        public static StateVector Propagate(StateVector state, double gm, double deltaTimeSeconds,
            double tolerance = DefaultTolerance, int maxIterations = DefaultMaxIterations)
        {
            if (deltaTimeSeconds == 0.0) return state;
            if (gm <= 0.0) throw new ArgumentOutOfRangeException(nameof(gm), "GM must be positive.");

            Double3 r0 = state.Position;
            Double3 v0 = state.Velocity;
            double r0mag = r0.Magnitude;
            if (r0mag <= 0.0)
                throw new ArgumentException("Cannot propagate from the central body's centre.", nameof(state));

            double sqrtMu = System.Math.Sqrt(gm);
            double rdotv = Double3.Dot(r0, v0);

            // alpha = 1/a from the vis-viva relation. Sign classifies the conic for free.
            double alpha = 2.0 / r0mag - v0.SqrMagnitude / gm;

            double chi = InitialChi(r0, v0, r0mag, rdotv, alpha, gm, sqrtMu, deltaTimeSeconds);

            double psi = 0.0, c2 = 0.5, c3 = 1.0 / 6.0, r = r0mag;
            bool converged = false;

            for (int i = 0; i < maxIterations; i++)
            {
                psi = chi * chi * alpha;
                c2 = StumpffC2(psi);
                c3 = StumpffC3(psi);

                r = chi * chi * c2
                    + (rdotv / sqrtMu) * chi * (1.0 - psi * c3)
                    + r0mag * (1.0 - psi * c2);

                if (r == 0.0) break;

                double residual = sqrtMu * deltaTimeSeconds
                                  - chi * chi * chi * c3
                                  - (rdotv / sqrtMu) * chi * chi * c2
                                  - r0mag * chi * (1.0 - psi * c3);

                double step = residual / r;
                chi += step;

                if (System.Math.Abs(step) < tolerance * System.Math.Max(1.0, System.Math.Abs(chi)))
                {
                    converged = true;
                    // One final evaluation so f/g below use the converged chi, not the previous one.
                    psi = chi * chi * alpha;
                    c2 = StumpffC2(psi);
                    c3 = StumpffC3(psi);
                    r = chi * chi * c2
                        + (rdotv / sqrtMu) * chi * (1.0 - psi * c3)
                        + r0mag * (1.0 - psi * c2);
                    break;
                }
            }

            if (!converged)
            {
                throw new InvalidOperationException(
                    $"Universal Kepler propagation failed to converge (alpha={alpha:G6}, dt={deltaTimeSeconds:G6} s). " +
                    "This usually means the input state is degenerate or the time step spans many revolutions " +
                    "of an extremely eccentric orbit.");
            }

            // Lagrange coefficients relate the new state to the old one as a linear combination
            // of r0 and v0 — the orbit plane is preserved exactly by construction.
            double f = 1.0 - (chi * chi / r0mag) * c2;
            double g = deltaTimeSeconds - (chi * chi * chi / sqrtMu) * c3;
            double gDot = 1.0 - (chi * chi / r) * c2;
            double fDot = (sqrtMu / (r * r0mag)) * chi * (psi * c3 - 1.0);

            return new StateVector(r0 * f + v0 * g, r0 * fDot + v0 * gDot);
        }

        /// <summary>Propagate between two absolute instants.</summary>
        public static StateVector PropagateTo(StateVector state, double gm, CosmicTime from, CosmicTime to) =>
            Propagate(state, gm, to - from);

        private static double InitialChi(Double3 r0, Double3 v0, double r0mag, double rdotv,
            double alpha, double gm, double sqrtMu, double dt)
        {
            if (alpha > NearParabolicAlpha)
            {
                // Elliptic. chi advances roughly linearly in time; damp slightly for
                // multi-revolution steps so the first iteration does not overshoot a full orbit.
                double chi = sqrtMu * dt * alpha;
                double period = MathD.TwoPi / (sqrtMu * alpha * System.Math.Sqrt(alpha));
                if (System.Math.Abs(dt) > period) chi *= 0.99;
                return chi;
            }

            if (alpha < -NearParabolicAlpha)
            {
                // Hyperbolic. The logarithmic starter comes from the asymptotic form of the
                // hyperbolic Kepler equation; a linear guess diverges immediately here.
                double a = 1.0 / alpha;                       // negative
                double sign = MathD.Sign(dt);
                if (sign == 0.0) sign = 1.0;
                double numerator = -2.0 * gm * alpha * dt;
                double denominator = rdotv + sign * System.Math.Sqrt(-gm * a) * (1.0 - r0mag * alpha);
                if (denominator == 0.0 || numerator / denominator <= 0.0)
                    return sign * System.Math.Sqrt(-a);
                return sign * System.Math.Sqrt(-a) * System.Math.Log(numerator / denominator);
            }

            // Parabolic: Barker's equation solved through the trigonometric substitution.
            double h = Double3.Cross(r0, v0).Magnitude;
            double p = h * h / gm;
            double cot2s = 3.0 * System.Math.Sqrt(gm / (p * p * p)) * dt;
            double s = 0.5 * System.Math.Atan2(1.0, cot2s);
            double w = System.Math.Atan(System.Math.Cbrt(System.Math.Tan(s)));
            double tan2w = System.Math.Tan(2.0 * w);
            if (tan2w == 0.0) return 0.0;
            return System.Math.Sqrt(p) * 2.0 / tan2w;
        }
    }
}
