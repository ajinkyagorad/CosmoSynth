using System;
using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    public static class OrbitTests
    {
        private static readonly CosmicTime Epoch = CosmicTime.FromUtc(2026, 1, 1);

        /// <summary>
        /// A deliberately awkward spread of orbits. The degenerate cases are the point: circular
        /// orbits have no periapsis, equatorial orbits have no ascending node, and a
        /// circular-equatorial orbit has neither. Element sets that quietly produce NaN there
        /// are the classic way a solar-system renderer ends up with planets at the origin.
        /// </summary>
        private static IEnumerable<(string name, StateVector state, double gm)> TestOrbits()
        {
            // Low Earth orbit, inclined and eccentric — the well-conditioned baseline.
            yield return ("LEO inclined eccentric",
                new StateVector(new Double3(6524.834, 6862.875, 6448.296),
                                new Double3(4.901327, 5.533756, -1.976341)),
                Constants.GM_Earth);

            // Vallado's canonical worked example, so the elements can be checked against print.
            yield return ("Vallado example 2-6",
                new StateVector(new Double3(6524.834, 6862.875, 6448.296),
                                new Double3(4.901327, 5.533756, -1.976341)),
                398600.4418);

            yield return ("circular equatorial prograde",
                new StateVector(new Double3(7000.0, 0.0, 0.0),
                                new Double3(0.0, Constants.CircularSpeed(Constants.GM_Earth, 7000.0), 0.0)),
                Constants.GM_Earth);

            yield return ("circular equatorial retrograde",
                new StateVector(new Double3(7000.0, 0.0, 0.0),
                                new Double3(0.0, -Constants.CircularSpeed(Constants.GM_Earth, 7000.0), 0.0)),
                Constants.GM_Earth);

            yield return ("circular polar",
                new StateVector(new Double3(7000.0, 0.0, 0.0),
                                new Double3(0.0, 0.0, Constants.CircularSpeed(Constants.GM_Earth, 7000.0))),
                Constants.GM_Earth);

            yield return ("eccentric equatorial",
                new StateVector(new Double3(10000.0, 0.0, 0.0),
                                new Double3(0.0, 5.0, 0.0)),
                Constants.GM_Earth);

            yield return ("Molniya-like high eccentricity",
                new StateVector(new Double3(6700.0, 0.0, 0.0),
                                new Double3(0.0, 5.3, 8.0)),
                Constants.GM_Earth);

            yield return ("hyperbolic escape",
                new StateVector(new Double3(7000.0, 1000.0, 500.0),
                                new Double3(1.0, 12.0, 3.0)),
                Constants.GM_Earth);

            // Heliocentric, to exercise the code at a completely different magnitude: the
            // radius here is five orders larger than LEO, which is where a formulation that
            // silently loses precision would show it.
            yield return ("heliocentric Earth-like",
                new StateVector(new Double3(-2.607213844816194e7, 1.447746738210197e8, -8.892861905746162e3),
                                new Double3(-2.978893116564825e1, -5.396270536820538, 4.106639513727917e-4)),
                Constants.GM_Sun);

            yield return ("heliocentric comet-like",
                new StateVector(new Double3(1.2e8, 0.0, 3.0e7),
                                new Double3(0.0, 41.0, 2.0)),
                Constants.GM_Sun);
        }

        [Test("state -> elements -> state reproduces the original state exactly")]
        public static void StateElementsRoundTrip()
        {
            foreach (var (name, state, gm) in TestOrbits())
            {
                var elements = OrbitalElements.FromStateVector(state, gm, Epoch);
                var back = elements.Propagate(Epoch);

                // Scale tolerance to the orbit's own size: 1e-9 relative is ~7 microns in LEO
                // and ~15 cm at 1 AU.
                double positionTolerance = state.Position.Magnitude * 1e-9;
                double velocityTolerance = state.Velocity.Magnitude * 1e-9;

                Assert.VectorClose(state.Position, back.Position, positionTolerance, $"[{name}] position round trip");
                Assert.VectorClose(state.Velocity, back.Velocity, velocityTolerance, $"[{name}] velocity round trip");
            }
        }

        [Test("recovered elements match Vallado's published worked example")]
        public static void MatchesValladoWorkedExample()
        {
            // Vallado, Fundamentals of Astrodynamics and Applications, example 2-6.
            var state = new StateVector(new Double3(6524.834, 6862.875, 6448.296),
                                        new Double3(4.901327, 5.533756, -1.976341));
            var elements = OrbitalElements.FromStateVector(state, 398600.4418, Epoch);

            Assert.Relative(11067.790, elements.SemiLatusRectumKm, 1e-6, "semi-latus rectum p");
            Assert.Relative(36127.343, elements.SemiMajorAxisKm, 1e-6, "semi-major axis a");
            Assert.Relative(0.832853, elements.Eccentricity, 1e-6, "eccentricity");
            Assert.Equal(87.870, elements.InclinationRad * MathD.Rad2Deg, 1e-3, "inclination (deg)");
            Assert.Equal(227.898, elements.LongitudeOfAscendingNodeRad * MathD.Rad2Deg, 1e-3, "RAAN (deg)");
            Assert.Equal(53.38, elements.ArgumentOfPeriapsisRad * MathD.Rad2Deg, 1e-2, "argument of periapsis (deg)");
            Assert.Equal(92.335, elements.TrueAnomalyAt(Epoch) * MathD.Rad2Deg, 1e-2, "true anomaly (deg)");
        }

        [Test("classical and universal-variable propagation agree")]
        public static void ClassicalAgreesWithUniversalVariables()
        {
            // Two algorithmically unrelated derivations of the same physics. Agreement between
            // them is far stronger evidence than either one round-tripping against itself,
            // which only proves a function is invertible, not that it is right.
            double worstRelative = 0.0;
            string worstCase = "none";

            foreach (var (name, state, gm) in TestOrbits())
            {
                var elements = OrbitalElements.FromStateVector(state, gm, Epoch);

                double span = elements.IsClosed
                    ? elements.PeriodSeconds
                    : 4.0 * 3600.0;

                for (int i = 1; i <= 12; i++)
                {
                    double dt = span * i / 12.0;

                    var viaElements = elements.Propagate(Epoch + dt);
                    var viaUniversal = UniversalKepler.Propagate(state, gm, dt);

                    double relative = (viaElements.Position - viaUniversal.Position).Magnitude
                                      / viaElements.Position.Magnitude;
                    if (relative > worstRelative) { worstRelative = relative; worstCase = $"{name} at dt={dt:G6} s"; }

                    Assert.True(relative < 1e-9,
                        $"[{name}] position disagreement {relative:G6} (relative) at dt={dt:G6} s: " +
                        $"elements {viaElements.Position.ToString("F6")} vs universal {viaUniversal.Position.ToString("F6")}");
                }
            }

            Assert.True(worstRelative < 1e-9, $"worst disagreement {worstRelative:G6} at {worstCase}");
        }

        [Test("universal-variable propagation is reversible")]
        public static void UniversalPropagationIsReversible()
        {
            foreach (var (name, state, gm) in TestOrbits())
            {
                var forward = UniversalKepler.Propagate(state, gm, 3600.0);
                var back = UniversalKepler.Propagate(forward, gm, -3600.0);
                Assert.VectorClose(state.Position, back.Position, state.Position.Magnitude * 1e-9,
                    $"[{name}] forward-then-backward position");
                Assert.VectorClose(state.Velocity, back.Velocity, state.Velocity.Magnitude * 1e-9,
                    $"[{name}] forward-then-backward velocity");
            }
        }

        [Test("propagating a full period returns to the starting state")]
        public static void FullPeriodReturnsToStart()
        {
            foreach (var (name, state, gm) in TestOrbits())
            {
                var elements = OrbitalElements.FromStateVector(state, gm, Epoch);
                if (!elements.IsClosed) continue;

                var after = elements.Propagate(Epoch + elements.PeriodSeconds);
                Assert.VectorClose(state.Position, after.Position, state.Position.Magnitude * 1e-9,
                    $"[{name}] position after one period");
            }
        }

        [Test("energy and angular momentum are conserved under propagation")]
        public static void ConservedQuantitiesHold()
        {
            foreach (var (name, state, gm) in TestOrbits())
            {
                double energy0 = state.SpecificEnergy(gm);
                Double3 momentum0 = state.AngularMomentum;

                for (int i = 1; i <= 10; i++)
                {
                    var propagated = UniversalKepler.Propagate(state, gm, 1000.0 * i);
                    Assert.Relative(energy0, propagated.SpecificEnergy(gm), 1e-9, $"[{name}] specific energy at step {i}");
                    Assert.VectorClose(momentum0, propagated.AngularMomentum, momentum0.Magnitude * 1e-9,
                        $"[{name}] angular momentum at step {i}");
                }
            }
        }

        [Test("vis-viva relation holds along the orbit")]
        public static void VisVivaHolds()
        {
            foreach (var (name, state, gm) in TestOrbits())
            {
                var elements = OrbitalElements.FromStateVector(state, gm, Epoch);
                if (elements.Conic == ConicType.Parabolic) continue;
                double a = elements.SemiMajorAxisKm;

                for (int i = 0; i < 8; i++)
                {
                    double nu = MathD.TwoPi * i / 8.0;
                    // Skip true anomalies past the asymptote of an open orbit.
                    if (1.0 + elements.Eccentricity * System.Math.Cos(nu) <= 1e-6) continue;

                    var s = elements.ToStateVector(nu);
                    double expected = gm * (2.0 / s.Radius - 1.0 / a);
                    Assert.Relative(expected, s.Velocity.SqrMagnitude, 1e-10, $"[{name}] v^2 at nu={nu:F3}");
                }
            }
        }

        [Test("sampled orbit paths lie on the conic section")]
        public static void SampledPathsLieOnTheConic()
        {
            foreach (var (name, state, gm) in TestOrbits())
            {
                var elements = OrbitalElements.FromStateVector(state, gm, Epoch);
                var points = elements.SampleOrbitPath(64);

                Assert.True(points.Count == 64, $"[{name}] expected 64 samples, got {points.Count}");

                Double3 normal = elements.OrbitNormal;
                foreach (var point in points)
                {
                    // Every sample must satisfy the focal conic equation r = p / (1 + e cos(nu)).
                    double radius = point.Magnitude;
                    double cosNu = Double3.Dot(point.Normalized, elements.PeriapsisDirection);
                    double expected = elements.SemiLatusRectumKm / (1.0 + elements.Eccentricity * cosNu);
                    Assert.Relative(expected, radius, 1e-7, $"[{name}] sampled point on conic");

                    // ...and lie in the orbital plane.
                    double outOfPlane = System.Math.Abs(Double3.Dot(point, normal));
                    Assert.True(outOfPlane < radius * 1e-9,
                        $"[{name}] sample is {outOfPlane:G6} km out of the orbital plane");
                }
            }
        }

        [Test("derived orbit geometry is self-consistent")]
        public static void DerivedGeometryIsConsistent()
        {
            var elements = OrbitalElements.FromSemiMajorAxis(
                semiMajorAxisKm: 26600.0, eccentricity: 0.74, inclinationRad: 63.4 * MathD.Deg2Rad,
                longitudeOfAscendingNodeRad: 0.5, argumentOfPeriapsisRad: 270.0 * MathD.Deg2Rad,
                meanAnomalyAtEpochRad: 0.0, epoch: Epoch, gravitationalParameter: Constants.GM_Earth);

            Assert.Relative(26600.0 * (1.0 - 0.74), elements.PeriapsisKm, 1e-12, "periapsis distance");
            Assert.Relative(26600.0 * (1.0 + 0.74), elements.ApoapsisKm, 1e-12, "apoapsis distance");
            Assert.Relative(26600.0, elements.SemiMajorAxisKm, 1e-12, "semi-major axis survives the p round trip");

            // Mean anomaly 0 means we start exactly at periapsis.
            var atEpoch = elements.Propagate(Epoch);
            Assert.Relative(elements.PeriapsisKm, atEpoch.Radius, 1e-12, "radius at M=0 equals periapsis");

            // Half a period later we must be at apoapsis.
            var halfway = elements.Propagate(Epoch + 0.5 * elements.PeriodSeconds);
            Assert.Relative(elements.ApoapsisKm, halfway.Radius, 1e-9, "radius at half period equals apoapsis");

            // Kepler's third law, stated independently of the mean-motion code path.
            double expectedPeriod = MathD.TwoPi * System.Math.Sqrt(
                System.Math.Pow(26600.0, 3) / Constants.GM_Earth);
            Assert.Relative(expectedPeriod, elements.PeriodSeconds, 1e-12, "orbital period");
        }

        [Test("open orbits report infinite apoapsis and period rather than nonsense")]
        public static void OpenOrbitsReportInfinity()
        {
            var hyperbolic = OrbitalElements.FromStateVector(
                new StateVector(new Double3(7000.0, 1000.0, 500.0), new Double3(1.0, 12.0, 3.0)),
                Constants.GM_Earth, Epoch);

            Assert.True(hyperbolic.Conic == ConicType.Hyperbolic, "orbit should classify as hyperbolic");
            Assert.False(hyperbolic.IsClosed, "hyperbolic orbit is not closed");
            Assert.True(double.IsPositiveInfinity(hyperbolic.ApoapsisKm), "hyperbolic apoapsis must be +infinity");
            Assert.True(double.IsPositiveInfinity(hyperbolic.PeriodSeconds), "hyperbolic period must be +infinity");
            Assert.True(hyperbolic.SemiMajorAxisKm < 0.0, "hyperbolic semi-major axis must be negative");
        }

        [Test("degenerate inputs are rejected with a clear message rather than producing NaN")]
        public static void DegenerateInputsAreRejected()
        {
            Assert.Throws<ArgumentException>(
                () => OrbitalElements.FromStateVector(
                    new StateVector(Double3.Zero, new Double3(1, 0, 0)), Constants.GM_Earth, Epoch),
                "a state at the centre of the primary has no orbit");

            // Purely radial motion: r and v parallel, so angular momentum vanishes and there is
            // no conic section at all. Straight-line fall is a real trajectory but not a Kepler
            // orbit, and silently returning NaN elements would be much worse than refusing.
            Assert.Throws<ArgumentException>(
                () => OrbitalElements.FromStateVector(
                    new StateVector(new Double3(7000, 0, 0), new Double3(1, 0, 0)), Constants.GM_Earth, Epoch),
                "radial trajectory has no conic section");
        }

        [Test("a burn changes the orbit the way the rocket equation says it should")]
        public static void BurnsChangeTheOrbitCorrectly()
        {
            // Circular 400 km orbit, then a prograde burn: this is the manoeuvre the spatial
            // trajectory editor performs when the user pulls on the orbit ribbon.
            double radius = Constants.EarthEquatorialRadius + 400.0;
            double circularSpeed = Constants.CircularSpeed(Constants.GM_Earth, radius);
            var circular = new StateVector(new Double3(radius, 0, 0), new Double3(0, circularSpeed, 0));

            var before = OrbitalElements.FromStateVector(circular, Constants.GM_Earth, Epoch);
            Assert.True(before.Eccentricity < 1e-12, $"starting orbit should be circular, e={before.Eccentricity:G6}");

            // Hohmann-style first burn targeting geostationary radius.
            const double targetRadius = 42164.0;
            double transferSemiMajorAxis = 0.5 * (radius + targetRadius);
            double burnSpeed = System.Math.Sqrt(Constants.GM_Earth * (2.0 / radius - 1.0 / transferSemiMajorAxis));
            double deltaV = burnSpeed - circularSpeed;

            var after = OrbitalElements.FromStateVector(
                circular.WithDeltaV(new Double3(0, deltaV, 0)), Constants.GM_Earth, Epoch);

            Assert.Relative(transferSemiMajorAxis, after.SemiMajorAxisKm, 1e-10, "transfer orbit semi-major axis");
            Assert.Relative(radius, after.PeriapsisKm, 1e-9, "burn point stays the periapsis");
            Assert.Relative(targetRadius, after.ApoapsisKm, 1e-9, "apoapsis reaches the target radius");

            // And the transfer really does arrive there after half the transfer period.
            var atApoapsis = after.Propagate(Epoch + 0.5 * after.PeriodSeconds);
            Assert.Relative(targetRadius, atApoapsis.Radius, 1e-9, "radius on arrival");
        }
    }
}
