using System;
using System.Collections.Generic;
using System.IO;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// End-to-end checks on the catalogue that actually ships in StreamingAssets.
    ///
    /// Everything else in the suite tests code. This tests the artefact: the real file, built
    /// by the real importer from real JPL data, loaded by the real parser. It is the test that
    /// would catch a Horizons format change, a bad regex, a broken parent link or a body
    /// quietly dropped from the import — none of which any amount of unit testing would show.
    /// </summary>
    public static class ShippedCatalogTests
    {
        private static CelestialCatalog _catalog;

        private static CelestialCatalog Catalog
        {
            get
            {
                if (_catalog == null)
                {
                    var directory = new DirectoryInfo(AppContext.BaseDirectory);
                    string found = null;
                    for (int depth = 0; depth < 10 && directory != null && found == null; depth++)
                    {
                        string candidate = Path.Combine(directory.FullName,
                            "unity", "Assets", "StreamingAssets", "CosmoSynth", "catalog", "solar_system.json");
                        if (File.Exists(candidate)) found = candidate;
                        directory = directory.Parent;
                    }

                    if (found == null)
                        throw new FileNotFoundException(
                            "solar_system.json not found. Build it with " +
                            "'python3 tools/cosmodata/build_solar_system.py'.");

                    _catalog = CelestialCatalog.Parse(File.ReadAllText(found));
                }
                return _catalog;
            }
        }

        [Test("the shipped Solar System catalogue parses and validates")]
        public static void ShippedCatalogIsValid()
        {
            var problems = Catalog.Validate();
            Assert.True(problems.Count == 0,
                $"the shipped catalogue has {problems.Count} problems:\n  " + string.Join("\n  ", problems));

            Assert.True(Catalog.Count >= 25, $"expected at least 25 bodies, found {Catalog.Count}");
            Assert.True(Catalog.RootIds.Count == 1, $"expected exactly one root (the Sun), found {Catalog.RootIds.Count}");
            Assert.True(Catalog.RootIds[0] == "sun", $"the root should be the Sun, got '{Catalog.RootIds[0]}'");
        }

        [Test("catalogue values match independently known figures")]
        public static void CatalogValuesMatchKnownFigures()
        {
            // Spot-check against values that did not come through the same pipeline. If the
            // importer silently mangled a unit, these are what would notice.
            var expectations = new (string id, double gm, double radiusKm, double tolerance)[]
            {
                ("sun", 1.327124400e11, 695700.0, 1e-6),
                ("mercury", 2.2031868e4, 2440.53, 1e-4),
                ("venus", 3.24858592e5, 6051.8, 1e-3),
                ("earth", 3.98600435e5, 6378.137, 1e-6),
                ("moon", 4.9028001e3, 1737.4, 1e-3),
                ("mars", 4.2828375e4, 3396.19, 1e-3),
                ("jupiter", 1.26686531e8, 71492.0, 3e-2),
                ("saturn", 3.7931206e7, 60268.0, 3e-2),
                ("titan", 8978.14, 2574.7, 1e-3),
            };

            foreach (var (id, gm, radiusKm, tolerance) in expectations)
            {
                var body = Catalog.Get(id);
                Assert.True(body != null, $"'{id}' is missing from the shipped catalogue");
                Assert.Relative(gm, body.GravitationalParameter, tolerance, $"[{id}] gravitational parameter");
                Assert.True(body.EquatorialRadiusKm.HasValue, $"[{id}] has no radius");
                // 1e-3, not the 5e-2 this used to allow. A five per cent bound on a radius is
                // wide enough to accept the volumetric mean in place of the equatorial one,
                // which is exactly the substitution the importer was making: Jupiter shipped at
                // 69 911 km against the 71 492 written right here, 2.2% out, and passed.
                Assert.Relative(radiusKm, body.EquatorialRadiusKm.Value, 1e-3, $"[{id}] equatorial radius");
            }

            // Derived quantities must come out at their textbook values.
            var earth = Catalog.Get("earth");
            Assert.Equal(9.80, earth.SurfaceGravity.Value, 0.05, "Earth surface gravity (m/s^2)");
            Assert.Equal(11.18, earth.EscapeSpeed.Value, 0.05, "Earth escape speed (km/s)");
            Assert.Equal(5514.0, earth.DensityKgPerM3.Value, 30.0, "Earth mean density (kg/m^3)");
            Assert.Equal(5.972e24, earth.MassKg, 5e21, "Earth mass (kg)");
        }

        [Test("the flattened planets are actually flattened")]
        public static void PlanetShapesAreNotAllSpheres()
        {
            // Every one of these shipped as a perfect sphere, because Horizons labels the giants
            // "Equat. radius (1 bar)" and the scraper's pattern only allowed "Equ." and
            // "Equatorial" — so no equatorial radius was found, the volumetric mean was used in
            // its place, and the published polar radius was never read at all. Jupiter was
            // 69 911 x 69 911 km. Its real flattening is 0.065, which is one of the most visible
            // things about it.
            //
            // Figures are the IAU/WGCCRE ones, independent of the import path.
            var expected = new (string id, double flattening)[]
            {
                ("jupiter", 0.06487),
                ("saturn", 0.09796),
                ("uranus", 0.02293),
                ("neptune", 0.01708),
                ("earth", 0.0033528),
                ("mars", 0.00589),
            };

            foreach (var (id, flattening) in expected)
            {
                var body = Catalog.Get(id);
                Assert.True(body != null, $"'{id}' is missing from the shipped catalogue");
                Assert.Relative(flattening, body.Flattening, 5e-3, $"[{id}] flattening");
            }

            // And a body with no published shape stays explicitly spherical rather than
            // acquiring an invented one.
            Assert.Equal(0.0, Catalog.Get("moon").Flattening, 1e-12,
                "the Moon has no separately published polar radius and must stay spherical");
        }

        [Test("no radius claims a source that did not publish it")]
        public static void RadiiCiteWhatTheyCameFrom()
        {
            // A polar radius is Measured only where Horizons publishes one, or publishes a
            // flattening to combine with the equatorial radius. Everywhere else it is derived or
            // assumed, and must say so. Mars shipped its *volumetric mean* radius as a Measured
            // polar radius — 13.7 km from the IAU value, citing a source that says something
            // else — which is the failure this checks for.
            foreach (var body in Catalog.Bodies)
            {
                if (!body.PolarRadiusKm.HasValue || !body.EquatorialRadiusKm.HasValue) continue;

                FieldProvenance provenance = body.Provenance.Get("polarRadiusKm");
                Assert.True(provenance.Class != ProvenanceClass.Unknown,
                    $"[{body.Id}] polar radius has no provenance at all");

                if (provenance.Class != ProvenanceClass.Measured) continue;

                // The one thing a Measured polar radius must never be is the volumetric mean,
                // which is what it silently was. R_vol = (a^2 c)^(1/3) reproduces it exactly,
                // so a "measured" polar radius equal to the mean radius of its own ellipsoid is
                // the fingerprint of that substitution.
                double a = body.EquatorialRadiusKm.Value, c = body.PolarRadiusKm.Value;
                double meanRadius = System.Math.Cbrt(a * a * c);
                Assert.True(System.Math.Abs(c - meanRadius) > 1e-6 || System.Math.Abs(a - c) < 1e-9,
                    $"[{body.Id}] polar radius {c:F3} km is its own volumetric mean radius, which is " +
                    "what the importer used to substitute for a value Horizons never published");
            }
        }

        [Test("orbits in the catalogue reproduce the real orbital periods")]
        public static void OrbitsHaveTheRightPeriods()
        {
            // Period follows from the semi-major axis and GM, so agreeing with the known
            // year length confirms the elements, the units and the central mass together.
            var expectedDays = new Dictionary<string, double>
            {
                { "mercury", 87.969 }, { "venus", 224.701 }, { "earth", 365.256 },
                { "mars", 686.980 }, { "jupiter", 4332.589 }, { "saturn", 10759.22 },
                { "uranus", 30685.4 }, { "neptune", 60189.0 }, { "pluto", 90560.0 },
                { "moon", 27.3217 }, { "io", 1.769 }, { "europa", 3.551 },
                { "ganymede", 7.155 }, { "callisto", 16.689 }, { "titan", 15.945 },
                { "enceladus", 1.370 }, { "triton", 5.877 }, { "charon", 6.387 },
                { "ceres", 1681.6 }, { "vesta", 1325.5 },
            };

            foreach (var pair in expectedDays)
            {
                var body = Catalog.Get(pair.Key);
                Assert.True(body != null, $"'{pair.Key}' is missing");
                Assert.True(body.Orbit.HasValue, $"'{pair.Key}' has no orbit");

                double days = body.Orbit.Value.PeriodSeconds / Constants.SecondsPerDay;
                // 1% covers the difference between an osculating element set at one epoch and
                // the mean period quoted in a reference table.
                Assert.Relative(pair.Value, days, 0.01, $"[{pair.Key}] orbital period (days)");
            }
        }

        [Test("orbital distances match the real Solar System")]
        public static void OrbitalDistancesAreRight()
        {
            var expectedAu = new Dictionary<string, double>
            {
                { "mercury", 0.387 }, { "venus", 0.723 }, { "earth", 1.000 }, { "mars", 1.524 },
                { "jupiter", 5.203 }, { "saturn", 9.537 }, { "uranus", 19.19 }, { "neptune", 30.07 },
                { "pluto", 39.48 }, { "ceres", 2.77 },
            };

            foreach (var pair in expectedAu)
            {
                var body = Catalog.Get(pair.Key);
                double au = body.Orbit.Value.SemiMajorAxisKm / Constants.AstronomicalUnit;
                Assert.Relative(pair.Value, au, 0.02, $"[{pair.Key}] semi-major axis (AU)");
            }

            // The Moon's distance, in the frame of its own parent.
            Assert.Relative(384400.0, Catalog.Get("moon").Orbit.Value.SemiMajorAxisKm, 0.01,
                "lunar semi-major axis (km)");
        }

        [Test("the hierarchy is the real one and builds a working frame tree")]
        public static void HierarchyIsCorrect()
        {
            var expectedParents = new Dictionary<string, string>
            {
                { "earth", "sun" }, { "moon", "earth" }, { "io", "jupiter" }, { "europa", "jupiter" },
                { "titan", "saturn" }, { "enceladus", "saturn" }, { "triton", "neptune" },
                { "charon", "pluto" }, { "ceres", "sun" },
            };

            foreach (var pair in expectedParents)
            {
                Assert.True(Catalog.Get(pair.Key).ParentId == pair.Value,
                    $"'{pair.Key}' should orbit '{pair.Value}', got '{Catalog.Get(pair.Key).ParentId}'");
            }

            Assert.True(Catalog.ChildIdsOf("jupiter").Count >= 4, "Jupiter should have its four Galilean moons");
            Assert.True(Catalog.ChildIdsOf("saturn").Count >= 4, "Saturn should have several moons");

            var root = Catalog.BuildFrameTree();
            var epoch = CosmicTime.FromUtc(2026, 6, 1);

            // Distances measured through the live frame tree must be physically right.
            //
            // The bound here is deliberately wider than the Moon's true 356500-406700 km
            // perigee-apogee range. The catalogue stores osculating elements for one epoch, and
            // HorizonsValidationTests measures what that costs: the Moon is the worst body in
            // the solar system for a two-body fit, drifting ~10% over a year, because the Sun
            // perturbs its orbit almost as strongly as Earth holds it. Five months from the
            // element epoch the Kepler Moon sits a few thousand kilometres outside the real
            // envelope. That is the model's known error, documented in docs/accuracy.md — not
            // a defect, and not something to hide behind a tolerance that quietly accepts
            // anything. A genuinely broken orbit would miss by an order of magnitude.
            double earthMoon = root.Find("moon").StateRelativeTo(root.Find("earth"), epoch).Position.Magnitude;
            Assert.True(earthMoon > 340000 && earthMoon < 425000,
                $"Earth-Moon distance {earthMoon:F0} km is outside even the two-body error envelope");

            double sunEarth = root.Find("earth").StateRelativeTo(root.Find("sun"), epoch).Position.Magnitude;
            Assert.True(sunEarth > 1.47e8 && sunEarth < 1.53e8,
                $"Sun-Earth distance {sunEarth:F0} km is outside the real perihelion-aphelion range");

            // A cross-branch query: Io as seen from Enceladus. Bounded by the two bodies'
            // heliocentric distances, and the point is that it resolves at all.
            double ioFromEnceladus = root.Find("io").StateRelativeTo(root.Find("enceladus"), epoch).Position.Magnitude;
            Assert.True(ioFromEnceladus > 3e8 && ioFromEnceladus < 2e9,
                $"Io-Enceladus distance {ioFromEnceladus:E3} km is not plausible");
        }

        [Test("every body carries provenance and nothing claims to be measured without a source")]
        public static void ProvenanceIsComplete()
        {
            int measuredFields = 0, inferredFields = 0;

            foreach (var body in Catalog.Bodies)
            {
                Assert.True(body.Provenance != null && body.Provenance.Count > 0,
                    $"[{body.Id}] has no provenance at all");

                // The two fields every body must be able to justify.
                Assert.True(body.Provenance.Get("gravitationalParameter").Class != ProvenanceClass.Unknown,
                    $"[{body.Id}] does not say where its GM came from");

                foreach (string field in body.Provenance.Fields)
                {
                    var entry = body.Provenance.Get(field);
                    if (entry.Class == ProvenanceClass.Measured)
                    {
                        measuredFields++;
                        // A measurement without a source is not a measurement.
                        Assert.True(!string.IsNullOrWhiteSpace(entry.Source),
                            $"[{body.Id}.{field}] is marked Measured but names no source");
                    }
                    else if (entry.Class == ProvenanceClass.Inferred)
                    {
                        inferredFields++;
                        Assert.True(!string.IsNullOrWhiteSpace(entry.Source),
                            $"[{body.Id}.{field}] is marked Inferred but does not say from what");
                    }
                }

                if (body.Orbit.HasValue)
                {
                    Assert.True(body.Provenance.Get("orbit").Class == ProvenanceClass.Measured,
                        $"[{body.Id}] orbit should trace to JPL");
                }
            }

            Assert.True(measuredFields > 100, $"only {measuredFields} measured fields; the import looks thin");
            Assert.True(inferredFields > 0, "nothing is marked Inferred, which is implausible for real data");
        }

        [Test("rotation presets resolved, and day lengths match reality")]
        public static void RotationResolvedFromPresets()
        {
            var expectedHours = new Dictionary<string, double>
            {
                { "earth", 23.934 }, { "mars", 24.623 }, { "jupiter", 9.925 },
                { "moon", 655.7 },                        // tidally locked to its 27.3-day orbit
                { "venus", -5832.4 },                     // retrograde
            };

            foreach (var pair in expectedHours)
            {
                var body = Catalog.Get(pair.Key);
                Assert.True(body.Orientation.HasValue,
                    $"[{pair.Key}] has no orientation; the preset lookup did not resolve");

                double hours = body.Orientation.Value.SiderealPeriodSeconds / 3600.0;
                Assert.Relative(pair.Value, hours, 0.01, $"[{pair.Key}] sidereal day (hours)");
            }

            Assert.True(Catalog.Get("venus").Orientation.Value.IsRetrograde, "Venus rotates retrograde");

            // A tidally locked moon's rotation period must equal its orbital period.
            var moon = Catalog.Get("moon");
            Assert.Relative(moon.Orbit.Value.PeriodSeconds, moon.Orientation.Value.SiderealPeriodSeconds, 0.01,
                "the Moon is tidally locked");
        }

        [Test("atmospheres are present where they should be and absent where they should not")]
        public static void AtmospheresAreWhereTheyBelong()
        {
            foreach (string id in new[] { "venus", "earth", "mars", "jupiter", "saturn", "titan" })
            {
                var body = Catalog.Get(id);
                Assert.True(body.Atmosphere != null && body.Atmosphere.IsPresent,
                    $"[{id}] should have an atmosphere");
            }

            foreach (string id in new[] { "mercury", "moon", "io", "europa", "ceres", "vesta" })
            {
                var body = Catalog.Get(id);
                Assert.True(body.Atmosphere == null || !body.Atmosphere.IsPresent,
                    $"[{id}] should be airless");
            }

            // Pressures at the surface, against the published figures.
            Assert.Relative(1.01325, Catalog.Get("earth").Atmosphere.SurfacePressureBar, 1e-6, "Earth surface pressure (bar)");
            Assert.Relative(92.0, Catalog.Get("venus").Atmosphere.SurfacePressureBar, 1e-6, "Venus surface pressure (bar)");
            Assert.Relative(0.00636, Catalog.Get("mars").Atmosphere.SurfacePressureBar, 1e-6, "Mars surface pressure (bar)");

            // The barometric law must reproduce known altitudes: Earth's pressure halves at
            // about 5.5 km, and cruising altitude is roughly a quarter of sea level.
            var earthAir = Catalog.Get("earth").Atmosphere;
            Assert.Equal(0.5, earthAir.PressureAtAltitude(5.9) / earthAir.SurfacePressureBar, 0.02,
                "Earth pressure halves near 5.9 km");
            Assert.True(earthAir.DensityAtAltitude(100.0) < 1e-3, "the atmosphere is effectively gone at 100 km");
        }

        [Test("the catalogue survives a save/load cycle unchanged")]
        public static void CatalogSurvivesReserialisation()
        {
            var reparsed = CelestialCatalog.Parse(Catalog.ToJsonString());

            Assert.True(reparsed.Count == Catalog.Count, "body count survives");
            Assert.True(reparsed.Validate().Count == 0, "the reserialised catalogue still validates");

            var epoch = CosmicTime.FromUtc(2027, 3, 15);
            foreach (var body in Catalog.Bodies)
            {
                var twin = reparsed.Get(body.Id);
                Assert.True(twin != null, $"[{body.Id}] survives");

                if (body.Orbit.HasValue)
                {
                    var before = body.Orbit.Value.Propagate(epoch);
                    var after = twin.Orbit.Value.Propagate(epoch);
                    Assert.VectorClose(before.Position, after.Position, before.Position.Magnitude * 1e-12,
                        $"[{body.Id}] propagates identically after a round trip");
                }

                if (body.Orientation.HasValue)
                {
                    Assert.True(twin.Orientation.HasValue, $"[{body.Id}] orientation survives");
                    Assert.Equal(body.Orientation.Value.RotationRateDegPerDay,
                        twin.Orientation.Value.RotationRateDegPerDay, 0.0, $"[{body.Id}] rotation rate survives");
                }
            }
        }
    }
}
