using System;
using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>Reference frames, scale space, catalogue and JSON.</summary>
    public static class CoreSystemsTests
    {
        private static readonly CosmicTime Epoch = CosmicTime.FromUtc(2026, 1, 1);

        // ---- Frames ---------------------------------------------------------

        private static ReferenceFrame BuildToySystem(out ReferenceFrame planet, out ReferenceFrame moon,
            out ReferenceFrame otherPlanet)
        {
            var root = ReferenceFrame.CreateRoot("ssb");

            var earthOrbit = OrbitalElements.FromSemiMajorAxis(
                Constants.AstronomicalUnit, 0.0167, 0.0, 0.0, 0.0, 0.0, Epoch,
                Constants.GM_Sun + Constants.GM_EarthMoonSystem);
            planet = root.CreateChild("earth", new KeplerEphemeris(earthOrbit));

            var moonOrbit = OrbitalElements.FromSemiMajorAxis(
                384400.0, 0.0549, 5.145 * MathD.Deg2Rad, 0.0, 0.0, 0.0, Epoch,
                Constants.GM_EarthMoonSystem);
            moon = planet.CreateChild("moon", new KeplerEphemeris(moonOrbit));

            var marsOrbit = OrbitalElements.FromSemiMajorAxis(
                1.524 * Constants.AstronomicalUnit, 0.0934, 1.85 * MathD.Deg2Rad, 0.0, 0.0, 2.0, Epoch,
                Constants.GM_Sun + Constants.GM_MarsSystem);
            otherPlanet = root.CreateChild("mars", new KeplerEphemeris(marsOrbit));

            return root;
        }

        [Test("frame tree resolves relative states through the common ancestor")]
        public static void FrameTreeResolvesRelativeStates()
        {
            var root = BuildToySystem(out var earth, out var moon, out var mars);

            // The Moon relative to Earth must be its own orbit, exactly — no heliocentric
            // coordinate should be formed and subtracted back off.
            var moonFromEarth = moon.StateRelativeTo(earth, Epoch);
            var expected = moon.StateInParent(Epoch);
            Assert.VectorClose(expected.Position, moonFromEarth.Position, 1e-9, "Moon relative to Earth");

            // And the round trip is antisymmetric.
            var earthFromMoon = earth.StateRelativeTo(moon, Epoch);
            Assert.VectorClose(-moonFromEarth.Position, earthFromMoon.Position, 1e-9, "antisymmetry");

            // Cross-branch queries go through the root.
            var marsFromEarth = mars.StateRelativeTo(earth, Epoch);
            var viaAbsolute = mars.AbsoluteState(Epoch).Position - earth.AbsoluteState(Epoch).Position;
            Assert.VectorClose(viaAbsolute, marsFromEarth.Position, 1e-6, "Mars relative to Earth");

            Assert.True(ReferenceFrame.CommonAncestor(moon, mars) == root, "common ancestor of Moon and Mars is the root");
            Assert.True(ReferenceFrame.CommonAncestor(moon, earth) == earth, "common ancestor of Moon and Earth is Earth");
            Assert.True(moon.Depth == 2 && earth.Depth == 1 && root.Depth == 0, "frame depths");
        }

        [Test("the frame tree preserves precision that absolute coordinates would lose")]
        public static void FrameTreePreservesPrecision()
        {
            // This is the entire reason the tree exists. A 1 cm feature near a moon 1.4e9 km
            // from the barycentre survives when stored locally, and is at the edge of what a
            // double can express when stored absolutely.
            BuildToySystem(out _, out var moon, out _);

            var lander = moon.CreateChild("lander", new FixedEphemeris(new Double3(1737.4, 0.0, 0.0)));
            var beacon = moon.CreateChild("beacon", new FixedEphemeris(new Double3(1737.4 + 1e-5, 0.0, 0.0)));

            double localSeparation = (lander.StateRelativeTo(beacon, Epoch)).Position.Magnitude;
            Assert.Relative(1e-5, localSeparation, 1e-6, "1 cm separation measured within the moon's frame");

            // Measured through absolute coordinates the same separation is far noisier,
            // because both operands are ~1.5e8 km numbers being subtracted.
            double absoluteSeparation =
                (lander.AbsoluteState(Epoch).Position - beacon.AbsoluteState(Epoch).Position).Magnitude;
            double localError = System.Math.Abs(localSeparation - 1e-5);
            double absoluteError = System.Math.Abs(absoluteSeparation - 1e-5);
            Assert.True(localError <= absoluteError,
                $"local path should not be worse than the absolute one (local {localError:E3}, absolute {absoluteError:E3})");
        }

        [Test("sampled ephemeris interpolates at the fourth-order rate cubic Hermite guarantees")]
        public static void SampledEphemerisInterpolates()
        {
            // Sample a known Kepler orbit coarsely, then check the interpolant reproduces it
            // between the knots. This is exactly what the Horizons importer relies on.
            var elements = OrbitalElements.FromSemiMajorAxis(
                Constants.AstronomicalUnit, 0.0167, 0.0, 0.0, 0.0, 0.0, Epoch,
                Constants.GM_Sun + Constants.GM_EarthMoonSystem);

            double WorstErrorForStep(double stepSeconds, int knots)
            {
                var times = new List<double>();
                var samples = new List<StateVector>();
                for (int i = 0; i <= knots; i++)
                {
                    double t = Epoch.SecondsPastJ2000 + i * stepSeconds;
                    times.Add(t);
                    samples.Add(elements.Propagate(new CosmicTime(t)));
                }

                var interpolant = new SampledEphemeris(times, samples);
                double worst = 0.0;
                for (int i = 0; i < 400; i++)
                {
                    double t = Epoch.SecondsPastJ2000 + i * 0.037 * stepSeconds;
                    if (t > interpolant.EndSeconds) break;
                    var truth = elements.Propagate(new CosmicTime(t));
                    var guess = interpolant.GetStateInParent(new CosmicTime(t));
                    worst = System.Math.Max(worst, (truth.Position - guess.Position).Magnitude / truth.Position.Magnitude);
                }
                return worst;
            }

            const double tenDays = 10.0 * Constants.SecondsPerDay;

            // Cubic Hermite has truncation error h^4 |f''''| / 384. For circular motion of rate
            // omega that is (omega*h)^4 / 384 relative, which for ten-day sampling of a
            // one-year orbit predicts 2.3e-6. Asserting the predicted magnitude is worth more
            // than a round number picked to pass.
            double omega = MathD.TwoPi / elements.PeriodSeconds;
            double predicted = System.Math.Pow(omega * tenDays, 4) / 384.0;
            double measured = WorstErrorForStep(tenDays, 40);

            Assert.True(measured < predicted * 2.0,
                $"error {measured:E3} exceeds twice the analytic Hermite bound {predicted:E3}");

            // Far more decisive than any single magnitude: halving the step must cut the error
            // by roughly sixteen. Only a genuinely fourth-order scheme does that, so this
            // catches a mismatched basis or a wrong tangent scaling that a magnitude check
            // could easily absorb.
            double atHalfStep = WorstErrorForStep(tenDays * 0.5, 80);
            double convergenceRatio = measured / atHalfStep;
            Assert.True(convergenceRatio > 10.0 && convergenceRatio < 24.0,
                $"halving the step changed the error by {convergenceRatio:F1}x; cubic Hermite must give ~16x " +
                "(a ratio near 4 would mean the velocity tangents are being ignored)");

            var span = new SampledEphemeris(
                new List<double> { Epoch.SecondsPastJ2000, Epoch.SecondsPastJ2000 + tenDays },
                new List<StateVector> { elements.Propagate(Epoch), elements.Propagate(Epoch + tenDays) });
            Assert.True(span.Covers(Epoch + tenDays * 0.5), "span should cover an interior query");
            Assert.False(span.Covers(Epoch + tenDays * 2.0), "span should not cover past the end");

            // Exact at the knots by construction.
            var atKnot = span.GetStateInParent(Epoch + tenDays);
            Assert.VectorClose(elements.Propagate(Epoch + tenDays).Position, atKnot.Position, 1e-6,
                "interpolant passes through its knots");
        }

        [Test("sampled ephemeris clamps rather than extrapolating past its span")]
        public static void SampledEphemerisClamps()
        {
            var times = new List<double> { 0.0, 100.0, 200.0 };
            var samples = new List<StateVector>
            {
                new StateVector(new Double3(1000, 0, 0), new Double3(0, 1, 0)),
                new StateVector(new Double3(1000, 100, 0), new Double3(0, 1, 0)),
                new StateVector(new Double3(1000, 200, 0), new Double3(0, 1, 0))
            };
            var ephemeris = new SampledEphemeris(times, samples);

            // A cubic extrapolation would run away; clamping keeps the error bounded and visible.
            var before = ephemeris.GetStateInParent(new CosmicTime(-1e6));
            var after = ephemeris.GetStateInParent(new CosmicTime(1e6));
            Assert.VectorClose(samples[0].Position, before.Position, 1e-9, "clamped to the first sample");
            Assert.VectorClose(samples[2].Position, after.Position, 1e-9, "clamped to the last sample");

            Assert.Throws<ArgumentException>(
                () => new SampledEphemeris(new List<double> { 0.0, 0.0 }, new List<StateVector> { default, default }),
                "non-ascending sample times must be rejected");
        }

        // ---- Body orientation -----------------------------------------------

        [Test("body-fixed rotation is a proper rotation and reverses")]
        public static void BodyRotationIsWellFormed()
        {
            foreach (var (name, orientation) in new[]
                     {
                         ("Earth", BodyOrientation.Earth),
                         ("Venus", BodyOrientation.Venus),
                         ("Mars", BodyOrientation.Mars),
                         ("Moon", BodyOrientation.Moon),
                     })
            {
                var toIcrf = orientation.BodyFixedToIcrf(Epoch);
                Assert.True(toIcrf.IsRotation(1e-12), $"[{name}] body-fixed transform must be a proper rotation");

                var identity = toIcrf * orientation.IcrfToBodyFixed(Epoch);
                Assert.Equal(1.0, identity.M00, 1e-12, $"[{name}] round trip is the identity");
                Assert.Equal(1.0, identity.M11, 1e-12, $"[{name}] round trip is the identity");
                Assert.Equal(0.0, identity.M01, 1e-12, $"[{name}] round trip is the identity");

                // The body's z axis must map onto the published pole direction.
                var mappedPole = toIcrf * Double3.UnitZ;
                Assert.VectorClose(orientation.PoleDirectionAt(Epoch), mappedPole, 1e-12, $"[{name}] pole direction");
            }
        }

        [Test("rotation periods match the published day lengths")]
        public static void RotationPeriodsAreCorrect()
        {
            // Earth's sidereal day is 23h 56m 04s, not 24 hours.
            Assert.Equal(86164.1, BodyOrientation.Earth.SiderealPeriodSeconds, 1.0, "Earth sidereal day (s)");

            // Venus rotates retrograde with a 243-day period.
            Assert.True(BodyOrientation.Venus.IsRetrograde, "Venus rotates retrograde");
            Assert.Equal(-243.0, BodyOrientation.Venus.SiderealPeriodSeconds / Constants.SecondsPerDay, 0.1,
                "Venus sidereal day (days)");

            // Mars's day is about 40 minutes longer than Earth's.
            Assert.Equal(88642.0, BodyOrientation.Mars.SiderealPeriodSeconds, 5.0, "Mars sidereal day (s)");

            // The Moon is tidally locked: its rotation period equals its orbital period.
            Assert.Equal(27.32, BodyOrientation.Moon.SiderealPeriodSeconds / Constants.SecondsPerDay, 0.02,
                "lunar sidereal rotation (days)");
        }

        [Test("geodetic and planetocentric latitudes convert consistently")]
        public static void LatitudeConversionsAreConsistent()
        {
            double a = Constants.EarthEquatorialRadius, c = Constants.EarthPolarRadius;

            for (double geodetic = -85.0; geodetic <= 85.0; geodetic += 5.0)
            {
                double centric = BodyOrientation.GeodeticToCentricLatitude(geodetic, a, c);
                double back = BodyOrientation.CentricToGeodeticLatitude(centric, a, c);
                Assert.Equal(geodetic, back, 1e-9, $"latitude round trip at {geodetic} deg");

                // The centric latitude is always closer to the equator, except at the poles.
                if (System.Math.Abs(geodetic) > 1e-9)
                    Assert.True(System.Math.Abs(centric) < System.Math.Abs(geodetic) + 1e-12,
                        $"centric latitude should be nearer the equator at {geodetic} deg");
            }

            // The largest difference is about 0.19 degrees, near 45 degrees latitude.
            double maximumDifference = 0.0;
            for (double geodetic = 0.0; geodetic <= 90.0; geodetic += 0.5)
            {
                maximumDifference = System.Math.Max(maximumDifference,
                    System.Math.Abs(geodetic - BodyOrientation.GeodeticToCentricLatitude(geodetic, a, c)));
            }
            Assert.Equal(0.1924, maximumDifference, 0.005, "peak geodetic/centric divergence (deg)");
        }

        [Test("surface points land on the reference ellipsoid")]
        public static void SurfacePointsAreOnTheEllipsoid()
        {
            double a = Constants.EarthEquatorialRadius, c = Constants.EarthPolarRadius;

            var equator = BodyOrientation.SurfacePointCentric(0, 0, a, c);
            Assert.Relative(a, equator.Magnitude, 1e-12, "equatorial radius");

            var pole = BodyOrientation.SurfacePointCentric(90, 0, a, c);
            Assert.Relative(c, pole.Magnitude, 1e-9, "polar radius");

            // Round trip through surface coordinates.
            var point = BodyOrientation.SurfacePointCentric(51.5, 359.87, a, c, altitudeKm: 0.035);
            BodyOrientation.ToSurfaceCoordinates(point, out double latitude, out double longitude, out _);
            Assert.Equal(51.5, latitude, 1e-9, "latitude round trip");
            Assert.Equal(359.87, longitude, 1e-9, "longitude round trip");
        }

        // ---- Scale space -----------------------------------------------------

        [Test("angular diameter matches familiar sky sizes")]
        public static void AngularDiameterMatchesTheSky()
        {
            // The Sun and Moon both subtend about half a degree from Earth, which is why total
            // eclipses work at all.
            double sun = ScaleSpace.AngularDiameterRad(Constants.SolarRadius, Constants.AstronomicalUnit) * MathD.Rad2Deg;
            Assert.Equal(0.533, sun, 0.005, "angular diameter of the Sun (deg)");

            double moon = ScaleSpace.AngularDiameterRad(Constants.LunarRadius, 384400.0) * MathD.Rad2Deg;
            Assert.Equal(0.518, moon, 0.005, "angular diameter of the Moon (deg)");

            // From inside a body it fills the sky rather than returning nonsense.
            Assert.Equal(MathD.TwoPi, ScaleSpace.AngularDiameterRad(6371.0, 100.0), 1e-12, "inside the body");
        }

        [Test("representation level rises monotonically as a body is approached")]
        public static void RepresentationLevelIsMonotonic()
        {
            const double radius = 6371.0;
            const double fov = 60.0 * MathD.Deg2Rad;
            const int pixels = 1080;

            var previous = RepresentationLevel.Point;
            var distances = new[] { 1e12, 1e10, 1e8, 1e7, 1e6, 1e5, 3e4, 1e4, 7e3, 6.4e3 };

            foreach (double distance in distances)
            {
                var level = ScaleSpace.SelectRepresentation(radius, distance, fov, pixels);
                Assert.True(level >= previous,
                    $"level went backwards from {previous} to {level} at {distance:G3} km");
                previous = level;
            }

            Assert.True(ScaleSpace.SelectRepresentation(radius, 1e12, fov, pixels) == RepresentationLevel.Point,
                "a planet a light-hour away is a point");
            Assert.True(ScaleSpace.SelectRepresentation(radius, 6.0e3, fov, pixels) == RepresentationLevel.LocalDetail,
                "inside the body we are in local detail");
        }

        [Test("scale bands tile the range without gaps and keep depth ratios workable")]
        public static void ScaleBandsAreWellFormed()
        {
            var bands = ScaleSpace.DefaultBands;
            for (int i = 1; i < bands.Count; i++)
            {
                Assert.True(bands[i].NearKm <= bands[i - 1].FarKm,
                    $"gap between bands '{bands[i - 1].Name}' and '{bands[i].Name}'");
            }

            foreach (var band in bands)
            {
                // Beyond roughly 1e6 a floating-point depth buffer starts to break down, which
                // is the whole reason for splitting into bands.
                Assert.True(band.DepthRange <= 1e7,
                    $"band '{band.Name}' has a depth range of {band.DepthRange:E2}, too wide for one pass");
            }

            Assert.True(ScaleSpace.SelectBand(1.0).Name == "Local", "a metre-scale object is local");
            Assert.True(ScaleSpace.SelectBand(1e8).Name == "Interplanetary", "1e8 km is interplanetary");
            Assert.True(ScaleSpace.SelectBand(1e20).Name == "Galactic", "1e20 km is galactic");
        }

        [Test("floating origin round-trips and triggers a rebase at the right distance")]
        public static void FloatingOriginBehaves()
        {
            var origin = new Double3(1.4959787e8, 0, 0);   // 1 AU out
            var transform = new ScaleSpace.RenderTransform(origin, 1.0);

            var absolute = origin + new Double3(100.0, -50.0, 25.0);
            var render = transform.ToRender(absolute);
            Assert.VectorClose(new Double3(100.0, -50.0, 25.0), render, 1e-9, "render position is small and local");
            Assert.VectorClose(absolute, transform.ToAbsolute(render), 1e-6, "round trip back to absolute");

            Assert.False(ScaleSpace.ShouldRebase(new Double3(1000, 0, 0)), "no rebase while close to the origin");
            Assert.True(ScaleSpace.ShouldRebase(new Double3(5000, 0, 0)), "rebase once past the threshold");

            // After rebasing, the same absolute point maps near zero again.
            var rebased = transform.Rebased(absolute);
            Assert.VectorClose(Double3.Zero, rebased.ToRender(absolute), 1e-9, "rebased origin sits at zero");
        }

        [Test("navigation speed scales with the scene and the log slider spans its range")]
        public static void NavigationSpeedScales()
        {
            // Crossing a room and crossing a solar system should take a comparable time.
            Assert.Relative(0.0025, ScaleSpace.NavigationSpeed(0.01), 1e-9, "speed near a 10 m object");
            Assert.Relative(2.5e7, ScaleSpace.NavigationSpeed(1e8), 1e-9, "speed at interplanetary range");

            Assert.Relative(1e-3, ScaleSpace.LogarithmicSpeed(0.0, 1e-3, 1e9), 1e-9, "slider at minimum");
            Assert.Relative(1e9, ScaleSpace.LogarithmicSpeed(1.0, 1e-3, 1e9), 1e-9, "slider at maximum");
            Assert.Relative(1e3, ScaleSpace.LogarithmicSpeed(0.5, 1e-3, 1e9), 1e-9, "slider midpoint is geometric");
        }

        [Test("presentation scaling preserves ordering while compressing extremes")]
        public static void PresentationScalingPreservesOrdering()
        {
            double reference = Constants.EarthEquatorialRadius;
            double moon = Constants.LunarRadius;
            double jupiter = Constants.JupiterEquatorialRadius;

            // True scale leaves everything alone.
            Assert.Relative(jupiter, ScaleSpace.PresentationRadius(jupiter, reference, 1.0), 1e-12, "true scale");

            // Full compression makes everything the reference size.
            Assert.Relative(reference, ScaleSpace.PresentationRadius(jupiter, reference, 0.0), 1e-12, "full compression");

            // Partway, ordering survives but the ratio shrinks.
            double compressedMoon = ScaleSpace.PresentationRadius(moon, reference, 0.4);
            double compressedJupiter = ScaleSpace.PresentationRadius(jupiter, reference, 0.4);
            Assert.True(compressedMoon < reference && reference < compressedJupiter, "ordering is preserved");
            Assert.True(compressedJupiter / compressedMoon < jupiter / moon, "the extremes are pulled in");

            // Holding Earth as a one-metre hologram.
            double unitsPerKm = ScaleSpace.HologramUnitsPerKm(Constants.EarthEquatorialRadius, 1.0);
            Assert.Relative(1.0, 2.0 * Constants.EarthEquatorialRadius * unitsPerKm, 1e-12, "one-metre Earth");
        }

        // ---- Cube-sphere ------------------------------------------------------

        [Test("cube-sphere mapping round-trips over the whole sphere")]
        public static void CubeSphereRoundTrips()
        {
            var random = new DeterministicRandom(4242);
            for (int i = 0; i < 2000; i++)
            {
                Double3 direction = random.OnUnitSphere();
                CubeSphere.DirectionToFace(direction, out int face, out double u, out double v);

                Assert.True(u >= -1.0000001 && u <= 1.0000001, $"u {u} escaped the face");
                Assert.True(v >= -1.0000001 && v <= 1.0000001, $"v {v} escaped the face");

                Double3 back = CubeSphere.FaceToDirection(face, u, v);
                Assert.VectorClose(direction, back, 1e-12, $"direction round trip through face {face}");
            }
        }

        [Test("the tangent warp evens out cell areas across a face")]
        public static void TangentWarpEvensOutCellAreas()
        {
            // Without the warp, a face's centre cells are far larger than its corner cells, so
            // a uniform tile grid would carry uneven detail. Measure both and compare.
            double UnwarpedRatio(bool warp)
            {
                const int steps = 16;
                double smallest = double.MaxValue, largest = 0.0;

                for (int i = 0; i < steps; i++)
                for (int j = 0; j < steps; j++)
                {
                    double u0 = -1.0 + 2.0 * i / steps, u1 = -1.0 + 2.0 * (i + 1) / steps;
                    double v0 = -1.0 + 2.0 * j / steps, v1 = -1.0 + 2.0 * (j + 1) / steps;

                    Double3 a = CubeSphere.FaceToDirection(4, u0, v0, warp);
                    Double3 b = CubeSphere.FaceToDirection(4, u1, v0, warp);
                    Double3 c = CubeSphere.FaceToDirection(4, u0, v1, warp);

                    double area = Double3.Angle(a, b) * Double3.Angle(a, c);
                    smallest = System.Math.Min(smallest, area);
                    largest = System.Math.Max(largest, area);
                }
                return largest / smallest;
            }

            double withoutWarp = UnwarpedRatio(false);
            double withWarp = UnwarpedRatio(true);

            Assert.True(withWarp < withoutWarp,
                $"the warp should reduce area variation, but it went from {withoutWarp:F2} to {withWarp:F2}");
            Assert.True(withWarp < 1.6, $"warped area ratio {withWarp:F2} is still too uneven");
        }

        [Test("tile quadtree navigation is consistent")]
        public static void TileQuadtreeIsConsistent()
        {
            var root = new TileId(3, 0, 0, 0);
            Assert.True(root.IsRoot, "level 0 is a root");
            Assert.True(root.Parent == root, "a root is its own parent");

            foreach (var child in root.Children())
            {
                Assert.True(child.Level == 1, "children are one level deeper");
                Assert.True(child.Parent == root, "children point back at their parent");
                Assert.True(child.Face == root.Face, "children stay on the same face");
            }

            // A direction must land in the tile that claims it, at every level.
            var random = new DeterministicRandom(77);
            for (int i = 0; i < 300; i++)
            {
                Double3 direction = random.OnUnitSphere();
                for (int level = 0; level <= 6; level++)
                {
                    var tile = TileId.FromDirection(direction, level);
                    CubeSphere.DirectionToFace(direction, out int face, out double u, out double v);
                    Assert.True(tile.Face == face, "tile face matches the direction's face");

                    tile.Bounds(out double minU, out double minV, out double maxU, out double maxV);
                    Assert.True(u >= minU - 1e-9 && u <= maxU + 1e-9, $"u {u} outside tile bounds at level {level}");
                    Assert.True(v >= minV - 1e-9 && v <= maxV + 1e-9, $"v {v} outside tile bounds at level {level}");

                    if (level > 0)
                        Assert.True(tile.Parent == TileId.FromDirection(direction, level - 1),
                            "the tile's parent is the containing tile one level up");
                }
            }
        }

        [Test("tile ground size and geometric error halve with each level")]
        public static void TileMetricsHalveWithLevel()
        {
            const double radius = 6371.0;

            var tile = new TileId(4, 0, 0, 0);
            double previousSize = tile.GroundSizeKm(radius);
            double previousError = tile.GeometricErrorKm(radius, 20.0);

            for (int level = 1; level <= 8; level++)
            {
                tile = tile.Children()[0];
                double size = tile.GroundSizeKm(radius);
                double error = tile.GeometricErrorKm(radius, 20.0);

                Assert.True(size < previousSize, $"ground size should shrink at level {level}");
                Assert.True(error < previousError, $"geometric error should shrink at level {level}");
                Assert.Relative(0.5, size / previousSize, 0.15, $"ground size roughly halves at level {level}");

                previousSize = size;
                previousError = error;
            }

            // A level-0 tile on Earth spans a good fraction of the globe.
            Assert.True(new TileId(4, 0, 0, 0).GroundSizeKm(radius) > 5000.0, "a root tile is continent-scale");
        }

        [Test("LOD selection produces more, finer tiles the closer the viewer gets")]
        public static void LodSelectionRespondsToDistance()
        {
            const double radius = 6371.0;
            const double fov = 60.0 * MathD.Deg2Rad;
            const int pixels = 1080;
            var direction = new Double3(0.3, 0.5, 0.8).Normalized;

            int previousDeepestLevel = -1;
            foreach (double altitude in new[] { 1e6, 1e5, 1e4, 1e3, 1e2 })
            {
                var tiles = TileId.SelectVisible(direction, radius + altitude, radius, 20.0, fov, pixels,
                    pixelErrorBudget: 4.0, maxLevel: 10);

                Assert.True(tiles.Count > 0, $"no tiles selected at {altitude:G3} km altitude");

                // Tile *count* is not monotonic — descending toward the surface pulls more of
                // the globe below the horizon even as the remaining part is refined. The
                // invariant that actually matters is that the terrain under the viewer keeps
                // getting finer.
                int deepestLevel = 0;
                foreach (var tile in tiles) deepestLevel = System.Math.Max(deepestLevel, tile.Level);

                Assert.True(deepestLevel >= previousDeepestLevel,
                    $"deepest level fell from {previousDeepestLevel} to {deepestLevel} on approaching to {altitude:G3} km");
                previousDeepestLevel = deepestLevel;

                // Nothing wholly behind the horizon should survive the cull.
                Double3 viewer = direction * (radius + altitude);
                foreach (var tile in tiles)
                {
                    Assert.True(tile.IsPotentiallyVisible(viewer, radius),
                        $"tile {tile} is entirely below the horizon but was selected");
                }
            }

            // Close in, the terrain directly beneath the viewer must reach fine subdivision.
            var closeUp = TileId.SelectVisible(direction, radius + 1.0, radius, 20.0, fov, pixels,
                pixelErrorBudget: 4.0, maxLevel: 14);
            int finest = 0;
            foreach (var tile in closeUp) finest = System.Math.Max(finest, tile.Level);
            Assert.True(finest >= 10, $"one kilometre up, the finest tile reached only level {finest}");

            // And a coarse root tile must not be discarded merely because its centre faces
            // away: from low altitude every root is partly visible or wholly behind us, and
            // dropping a partly-visible one leaves a hole in the ground.
            var nearSurfaceViewer = direction * (radius + 100.0);
            int visibleRoots = 0;
            foreach (var root in TileId.Roots())
            {
                if (root.IsPotentiallyVisible(nearSurfaceViewer, radius)) visibleRoots++;
            }
            Assert.True(visibleRoots >= 1, "at least one root tile must survive culling from low altitude");
        }

        // ---- Catalogue and JSON ----------------------------------------------

        [Test("JSON round-trips values, structures and escapes")]
        public static void JsonRoundTrips()
        {
            var original = JsonValue.Object();
            original["name"] = JsonValue.String("Kepler-442 b \"outer\"\n\ttabbed");
            original["mass"] = JsonValue.Number(1.3452678901234567e24);
            original["confirmed"] = JsonValue.Bool(true);
            original["missing"] = JsonValue.Null();
            original["vector"] = JsonValue.Vector(new Double3(-1.5, 2.25, 1e-30));
            original["empty"] = JsonValue.Array();

            var list = JsonValue.Array();
            list.Add(JsonValue.Number(1));
            list.Add(JsonValue.String("two"));
            list.Add(JsonValue.Object());
            original["list"] = list;

            var reparsed = JsonValue.Parse(original.ToString(indented: true));

            Assert.True(reparsed["name"].AsString() == "Kepler-442 b \"outer\"\n\ttabbed", "string escapes survive");
            // "R" formatting must reproduce the double exactly, or orbital elements shift.
            Assert.Equal(1.3452678901234567e24, reparsed["mass"].AsDouble(), 0.0, "doubles round-trip exactly");
            Assert.True(reparsed["confirmed"].AsBool(), "booleans survive");
            Assert.True(reparsed["missing"].IsNull, "nulls survive");
            Assert.VectorClose(new Double3(-1.5, 2.25, 1e-30), reparsed["vector"].AsVector(), 0.0, "vectors survive");
            Assert.True(reparsed["list"].Count == 3, "arrays keep their length");
            Assert.True(reparsed["empty"].Count == 0, "empty arrays survive");

            // Missing keys read as null instead of throwing.
            Assert.True(reparsed["nope"].IsNull, "a missing key yields null");
            Assert.Equal(42.0, reparsed["nope"].AsDouble(42.0), 0.0, "a missing key falls back");
        }

        [Test("JSON rejects malformed input with a useful message")]
        public static void JsonRejectsMalformedInput()
        {
            foreach (string bad in new[] { "{", "[1,2", "{\"a\":}", "{'a':1}", "tru", "{\"a\":1}garbage", "" })
            {
                Assert.True(!JsonValue.TryParse(bad, out _), $"should have rejected: {bad}");
            }

            Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\": }"), "malformed input throws FormatException");

            // Required fields must fail loudly rather than defaulting a physical quantity to zero.
            var json = JsonValue.Parse("{\"a\": 1}");
            Assert.Throws<FormatException>(() => json.RequireDouble("b"), "a missing required field throws");
        }

        [Test("catalogue round-trips through JSON with orbits and provenance intact")]
        public static void CatalogRoundTrips()
        {
            var catalog = new CelestialCatalog { Name = "Test", Source = "unit test" };

            var star = new CelestialBody
            {
                Id = "sun", Name = "Sun", Type = BodyType.Star,
                GravitationalParameter = Constants.GM_Sun,
                EquatorialRadiusKm = Constants.SolarRadius,
                PolarRadiusKm = Constants.SolarRadius,
                LuminosityWatts = Constants.SolarLuminosity,
                EffectiveTemperatureK = Constants.SolarEffectiveTemperature,
                SpectralType = "G2V"
            };
            star.Provenance.Set("gravitationalParameter",
                FieldProvenance.Measured("JPL DE440", "https://ssd.jpl.nasa.gov/", 1e-3, "2026-07-30"));
            catalog.Add(star);

            var earth = new CelestialBody
            {
                Id = "earth", Name = "Earth", Type = BodyType.Planet, ParentId = "sun",
                GravitationalParameter = Constants.GM_EarthMoonSystem,
                EquatorialRadiusKm = Constants.EarthEquatorialRadius,
                PolarRadiusKm = Constants.EarthPolarRadius,
                BondAlbedo = 0.306,
                Orientation = BodyOrientation.Earth,
                Orbit = OrbitalElements.FromSemiMajorAxis(
                    Constants.AstronomicalUnit, 0.0167, 0.0, 0.0, 1.796, 6.24, Epoch,
                    Constants.GM_Sun + Constants.GM_EarthMoonSystem),
                Atmosphere = new AtmosphereDefinition
                {
                    SurfacePressureBar = 1.01325, ScaleHeightKm = 8.5, TopAltitudeKm = 100.0,
                    SurfaceDensityKgPerM3 = 1.225, MeanMolecularMassKgPerMol = 0.02896,
                    SurfaceTemperatureK = 288.0,
                    Composition = new Dictionary<string, double> { { "N2", 0.78 }, { "O2", 0.21 }, { "Ar", 0.01 } }
                }
            };
            earth.Provenance.Set("equatorialRadiusKm", FieldProvenance.Measured("IAU 2015"));
            earth.Provenance.Set("heightmapTexture", FieldProvenance.Procedural("TerrainField", 12345));
            catalog.Add(earth);
            catalog.Rebuild();

            var reparsed = CelestialCatalog.Parse(catalog.ToJsonString());

            Assert.True(reparsed.Count == 2, "both bodies survive");
            var reparsedEarth = reparsed.Get("earth");
            Assert.True(reparsedEarth != null, "Earth is present");
            Assert.True(reparsedEarth.ParentId == "sun", "the parent link survives");
            Assert.True(reparsedEarth.Orbit.HasValue, "the orbit survives");

            // The orbit must reproduce the same position, not merely the same numbers.
            var before = earth.Orbit.Value.Propagate(Epoch + 1e7);
            var after = reparsedEarth.Orbit.Value.Propagate(Epoch + 1e7);
            Assert.VectorClose(before.Position, after.Position, before.Position.Magnitude * 1e-9,
                "the reparsed orbit propagates identically");

            Assert.True(reparsedEarth.Orientation.HasValue, "orientation survives");
            Assert.Equal(360.9856235, reparsedEarth.Orientation.Value.RotationRateDegPerDay, 1e-9, "rotation rate");

            Assert.True(reparsedEarth.Atmosphere != null && reparsedEarth.Atmosphere.IsPresent, "atmosphere survives");
            Assert.Equal(0.78, reparsedEarth.Atmosphere.Composition["N2"], 1e-12, "composition survives");

            Assert.True(reparsedEarth.Provenance.Get("equatorialRadiusKm").Class == ProvenanceClass.Measured,
                "measured provenance survives");
            Assert.True(reparsedEarth.Provenance.Get("heightmapTexture").Class == ProvenanceClass.Procedural,
                "procedural provenance survives");
            Assert.True(reparsedEarth.Provenance.WeakestClass() == ProvenanceClass.Procedural,
                "the weakest claim wins for the object badge");

            // An unrecorded field is unknown, not assumed measured.
            Assert.True(reparsedEarth.Provenance.Get("bondAlbedo").Class == ProvenanceClass.Unknown,
                "unrecorded fields are Unknown");
        }

        [Test("catalogue validation catches physically impossible configurations")]
        public static void CatalogValidationCatchesImpossibilities()
        {
            var catalog = new CelestialCatalog();
            catalog.Add(new CelestialBody
            {
                Id = "star", Name = "Star", Type = BodyType.Star,
                GravitationalParameter = Constants.GM_Sun,
                EquatorialRadiusKm = Constants.SolarRadius, PolarRadiusKm = Constants.SolarRadius,
                LuminosityWatts = Constants.SolarLuminosity, EffectiveTemperatureK = 5772
            });

            // A planet whose periapsis is inside the star.
            catalog.Add(new CelestialBody
            {
                Id = "grazer", Name = "Grazer", Type = BodyType.Planet, ParentId = "star",
                GravitationalParameter = Constants.GM_Earth,
                EquatorialRadiusKm = 6371, PolarRadiusKm = 6350,
                Orbit = OrbitalElements.FromSemiMajorAxis(
                    1e6, 0.5, 0.0, 0.0, 0.0, 0.0, Epoch, Constants.GM_Sun + Constants.GM_Earth)
            });

            // A planet denser than any solid material.
            catalog.Add(new CelestialBody
            {
                Id = "toodense", Name = "Too Dense", Type = BodyType.Planet, ParentId = "star",
                GravitationalParameter = Constants.GM_Earth * 500.0,
                EquatorialRadiusKm = 6371, PolarRadiusKm = 6371,
                Orbit = OrbitalElements.FromSemiMajorAxis(
                    Constants.AstronomicalUnit, 0.0, 0.0, 0.0, 0.0, 0.0, Epoch,
                    Constants.GM_Sun + Constants.GM_Earth * 500.0)
            });

            // A body whose parent does not exist.
            catalog.Add(new CelestialBody
            {
                Id = "orphan", Name = "Orphan", Type = BodyType.Moon, ParentId = "nowhere",
                GravitationalParameter = 100.0, EquatorialRadiusKm = 500, PolarRadiusKm = 500,
                Orbit = OrbitalElements.FromSemiMajorAxis(10000, 0.0, 0.0, 0.0, 0.0, 0.0, Epoch, 1000.0)
            });

            catalog.Rebuild();
            var problems = catalog.Validate();

            Assert.True(problems.Count >= 3, $"expected several problems, found {problems.Count}");

            string all = string.Join("\n", problems);
            Assert.True(all.Contains("grazer") && all.Contains("inside"), "the grazing orbit is reported");
            Assert.True(all.Contains("toodense") && all.Contains("density"), "the impossible density is reported");
            Assert.True(all.Contains("orphan"), "the dangling parent is reported");

            // Polar radius larger than equatorial is caught on the body itself.
            var oblate = new CelestialBody
            {
                Id = "prolate", Type = BodyType.Asteroid,
                GravitationalParameter = 1.0, EquatorialRadiusKm = 100, PolarRadiusKm = 200
            };
            Assert.True(!oblate.IsValid, "a body taller than it is wide should not validate");
        }

        [Test("catalogue builds a frame tree matching its hierarchy")]
        public static void CatalogBuildsFrameTree()
        {
            var catalog = new CelestialCatalog();
            catalog.Add(new CelestialBody
            {
                Id = "sun", Type = BodyType.Star, GravitationalParameter = Constants.GM_Sun,
                EquatorialRadiusKm = Constants.SolarRadius, PolarRadiusKm = Constants.SolarRadius,
                LuminosityWatts = Constants.SolarLuminosity, EffectiveTemperatureK = 5772
            });
            catalog.Add(new CelestialBody
            {
                Id = "earth", Type = BodyType.Planet, ParentId = "sun",
                GravitationalParameter = Constants.GM_Earth,
                EquatorialRadiusKm = 6378, PolarRadiusKm = 6357,
                Orbit = OrbitalElements.FromSemiMajorAxis(
                    Constants.AstronomicalUnit, 0.0167, 0, 0, 0, 0, Epoch, Constants.GM_Sun + Constants.GM_Earth)
            });
            catalog.Add(new CelestialBody
            {
                Id = "moon", Type = BodyType.Moon, ParentId = "earth",
                GravitationalParameter = Constants.GM_Moon,
                EquatorialRadiusKm = 1737, PolarRadiusKm = 1737,
                Orbit = OrbitalElements.FromSemiMajorAxis(
                    384400, 0.055, 0, 0, 0, 0, Epoch, Constants.GM_Earth + Constants.GM_Moon)
            });
            catalog.Rebuild();

            var root = catalog.BuildFrameTree();
            var moonFrame = root.Find("moon");
            var earthFrame = root.Find("earth");

            Assert.True(moonFrame != null && earthFrame != null, "frames exist for every body");
            Assert.True(moonFrame.Parent == earthFrame, "the Moon's frame hangs off Earth's");
            Assert.True(moonFrame.Depth == 3, "sun/earth/moon under the root gives depth 3");

            double distance = moonFrame.StateRelativeTo(earthFrame, Epoch).Position.Magnitude;
            Assert.Equal(384400.0 * (1.0 - 0.055), distance, 1.0, "the Moon starts at periapsis");
        }
    }
}
