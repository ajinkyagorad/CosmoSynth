using System;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// Kepler's equation has no closed-form inverse, so the solvers are the single most
    /// error-prone piece of the whole engine. These tests sweep the parameter space rather
    /// than checking a handful of hand-picked values, because the failure modes (stalling at
    /// high eccentricity, quadrant errors near the apsides, overflow in the hyperbolic tail)
    /// all hide in narrow bands that a spot check walks straight past.
    /// </summary>
    public static class KeplerTests
    {
        [Test("elliptic solver inverts Kepler's equation across the full e/M grid")]
        public static void EllipticSolverInvertsAcrossGrid()
        {
            double[] eccentricities = { 0.0, 1e-8, 0.001, 0.0167, 0.1, 0.2056, 0.5, 0.8, 0.9, 0.95, 0.99, 0.999, 0.9999 };
            const int meanAnomalySamples = 720;
            double worst = 0.0;
            double worstE = 0.0, worstM = 0.0;

            foreach (double e in eccentricities)
            {
                for (int i = 0; i <= meanAnomalySamples; i++)
                {
                    double m = -MathD.TwoPi + 2.0 * MathD.TwoPi * i / meanAnomalySamples;
                    double eccentric = KeplerSolver.SolveEccentricAnomaly(m, e);
                    double reconstructed = KeplerSolver.MeanFromEccentric(eccentric, e);
                    double error = System.Math.Abs(MathD.WrapPi(reconstructed - m));
                    if (error > worst) { worst = error; worstE = e; worstM = m; }
                }
            }

            Assert.True(worst < 1e-11,
                $"worst residual {worst:G6} rad at e={worstE}, M={worstM:F6} rad exceeds 1e-11");
        }

        [Test("elliptic solver keeps the eccentric anomaly in the same revolution as the mean anomaly")]
        public static void EllipticSolverPreservesRevolution()
        {
            // E and M must stay in the same 2pi window: an orbit renderer that walks the
            // anomaly forward relies on it advancing monotonically, not wrapping to zero.
            const double e = 0.7;
            for (int revolution = -3; revolution <= 3; revolution++)
            {
                double m = revolution * MathD.TwoPi + 1.234;
                double eccentric = KeplerSolver.SolveEccentricAnomaly(m, e);
                Assert.True(System.Math.Abs(eccentric - m) <= e + 1e-9,
                    $"|E-M| must be <= e; got {System.Math.Abs(eccentric - m):G6} at revolution {revolution}");
            }
        }

        [Test("hyperbolic solver inverts Kepler's equation across the e/M grid")]
        public static void HyperbolicSolverInvertsAcrossGrid()
        {
            double[] eccentricities = { 1.0000001, 1.001, 1.1, 1.5, 2.0, 3.0, 5.0, 10.0, 50.0 };
            double[] meanAnomalies = { -500, -100, -20, -5, -1, -0.1, -1e-4, 0, 1e-4, 0.1, 1, 5, 20, 100, 500 };
            double worst = 0.0;
            double worstE = 0.0, worstM = 0.0;

            foreach (double e in eccentricities)
            {
                foreach (double m in meanAnomalies)
                {
                    double h = KeplerSolver.SolveHyperbolicAnomaly(m, e);
                    double reconstructed = KeplerSolver.MeanFromHyperbolic(h, e);
                    // Relative, because M spans four orders of magnitude here.
                    double error = System.Math.Abs(reconstructed - m) / System.Math.Max(1.0, System.Math.Abs(m));
                    if (error > worst) { worst = error; worstE = e; worstM = m; }
                }
            }

            Assert.True(worst < 1e-10,
                $"worst relative residual {worst:G6} at e={worstE}, M={worstM} exceeds 1e-10");
        }

        [Test("Barker's equation round-trips for parabolic orbits")]
        public static void ParabolicRoundTrips()
        {
            for (int i = -200; i <= 200; i++)
            {
                double nu = 0.9 * MathD.Pi * i / 200.0;   // stay inside the asymptote at +/-pi
                double m = KeplerSolver.ParabolicMeanFromTrue(nu);
                double recovered = KeplerSolver.TrueFromParabolicMean(m);
                Assert.Angle(nu, recovered, 1e-9, $"parabolic true anomaly round trip at nu={nu:F4}");
            }
        }

        [Test("anomaly conversions round-trip in both directions")]
        public static void AnomalyConversionsRoundTrip()
        {
            double[] eccentricities = { 0.0, 0.05, 0.3, 0.7, 0.95, 0.999 };
            foreach (double e in eccentricities)
            {
                for (int i = 0; i < 360; i++)
                {
                    double nu = MathD.WrapPi(i * MathD.Deg2Rad);
                    double eccentric = KeplerSolver.EccentricFromTrue(nu, e);
                    double back = KeplerSolver.TrueFromEccentric(eccentric, e);
                    Assert.Angle(nu, back, 1e-11, $"true<->eccentric at e={e}, nu={i} deg");
                }
            }

            foreach (double e in new[] { 1.01, 1.5, 3.0 })
            {
                double asymptote = MathD.SafeAcos(-1.0 / e);
                for (int i = -95; i <= 95; i++)
                {
                    double nu = asymptote * 0.99 * i / 100.0;
                    double hyperbolic = KeplerSolver.HyperbolicFromTrue(nu, e);
                    Assert.True(!double.IsNaN(hyperbolic), $"hyperbolic anomaly should exist inside the asymptote (e={e})");
                    double back = KeplerSolver.TrueFromHyperbolic(hyperbolic, e);
                    Assert.Angle(nu, back, 1e-10, $"true<->hyperbolic at e={e}, nu={nu:F4}");
                }
            }
        }

        [Test("true anomaly beyond the hyperbolic asymptote is reported as unreachable")]
        public static void HyperbolicAsymptoteIsRejected()
        {
            const double e = 2.0;
            double asymptote = MathD.SafeAcos(-1.0 / e);
            double beyond = asymptote + 0.05;
            double result = KeplerSolver.HyperbolicFromTrue(beyond, e);
            Assert.True(double.IsNaN(result),
                "true anomalies past the asymptote lie on the unreachable branch and must return NaN, " +
                $"got {result:G6}");
        }

        [Test("solvers reject eccentricities outside their domain")]
        public static void SolversValidateDomain()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => KeplerSolver.SolveEccentricAnomaly(1.0, 1.5),
                "elliptic solver must reject e > 1");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => KeplerSolver.SolveHyperbolicAnomaly(1.0, 0.5),
                "hyperbolic solver must reject e < 1");
        }

        [Test("the pathological corner (e -> 1, M -> 0) still converges")]
        public static void NearParabolicCornerConverges()
        {
            // Here f'(E) = 1 - e cos(E) approaches zero at the root, which is where naive
            // Newton iterations stall. The bisection fallback exists for exactly this case.
            foreach (double e in new[] { 0.99, 0.999, 0.9999, 0.99999 })
            {
                foreach (double m in new[] { 0.0, 1e-12, 1e-9, 1e-6, 1e-3 })
                {
                    double eccentric = KeplerSolver.SolveEccentricAnomaly(m, e);
                    double residual = System.Math.Abs(KeplerSolver.MeanFromEccentric(eccentric, e) - m);
                    Assert.True(residual < 1e-11,
                        $"residual {residual:G6} at e={e}, M={m:G6} — solver stalled near the singular point");
                }
            }
        }
    }
}
