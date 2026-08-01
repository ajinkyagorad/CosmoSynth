using System;
using System.Collections.Generic;
using System.IO;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// Checks on the shipped exoplanet catalogue: that it parses, that the stars are where
    /// astronomy says they are, and — most importantly — that nothing claims to be measured
    /// when it was invented.
    /// </summary>
    public static class ExoplanetCatalogTests
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
                            "unity", "Assets", "StreamingAssets", "CosmoSynth", "catalog", "exoplanets.json");
                        if (File.Exists(candidate)) found = candidate;
                        directory = directory.Parent;
                    }

                    if (found == null)
                        throw new FileNotFoundException(
                            "exoplanets.json not found. Build it with " +
                            "'python3 tools/cosmodata/build_exoplanets.py'.");

                    _catalog = CelestialCatalog.Parse(File.ReadAllText(found));
                }
                return _catalog;
            }
        }

        [Test("the exoplanet catalogue parses and validates")]
        public static void ExoplanetCatalogIsValid()
        {
            var problems = Catalog.Validate();
            Assert.True(problems.Count == 0,
                $"{problems.Count} problems:\n  " + string.Join("\n  ", problems));

            // Sixty systems is the shipped budget — see build_exoplanets.py --max-systems, and the
            // rendering limit that forces it. Asserting a floor still catches a broken import.
            Assert.True(Catalog.Count > 120, $"expected the nearest-systems catalogue, found {Catalog.Count} bodies");

            int stars = 0, planets = 0;
            foreach (var body in Catalog.Bodies)
            {
                if (body.IsStar) stars++;
                else if (body.Type == BodyType.Planet) planets++;
            }

            Assert.True(stars >= 50, $"expected the nearest ~60 host stars, found {stars}");
            Assert.True(planets >= 100, $"expected 100+ planets, found {planets}");
            Console.WriteLine($"      {stars} host stars, {planets} planets");
        }

        [Test("host stars sit at their real measured distances")]
        public static void HostStarDistancesAreRight()
        {
            // Distances from the archive's own sy_dist, cross-checked against values any
            // reference will confirm. If the RA/Dec/distance to Cartesian conversion were wrong
            // — a degree/radian slip, a parsec/light-year mixup — these would be off by a
            // factor, not a percent.
            var expectedLightYears = new Dictionary<string, double>
            {
                { "Proxima Cen", 4.25 },
                { "Barnard's star", 5.96 },
                { "eps Eri", 10.45 },
                { "Ross 128", 11.01 },
                { "tau Cet", 11.75 },
            };

            foreach (var pair in expectedLightYears)
            {
                CelestialBody star = Catalog.FindByName(pair.Key);
                Assert.True(star != null, $"'{pair.Key}' is missing from the catalogue");
                Assert.True(star.PositionKm.HasValue, $"'{pair.Key}' has no position");

                double lightYears = star.PositionKm.Value.Magnitude / Constants.LightYear;
                Assert.Relative(pair.Value, lightYears, 0.02, $"[{pair.Key}] distance in light years");
            }
        }

        [Test("a known system reproduces its published orbits")]
        public static void KnownSystemMatchesPublishedValues()
        {
            // Proxima b: about 1.07 Earth masses on an 11.2-day orbit at 0.0485 AU. Those are
            // the numbers in the discovery paper, and the whole import chain — mass, GM,
            // semi-major axis, Kepler's third law — has to agree with them.
            CelestialBody proximaB = Catalog.FindByName("Proxima Cen b");
            Assert.True(proximaB != null, "Proxima Cen b is missing");
            Assert.True(proximaB.Orbit.HasValue, "Proxima Cen b has no orbit");

            OrbitalElements orbit = proximaB.Orbit.Value;
            double au = orbit.SemiMajorAxisKm / Constants.AstronomicalUnit;
            double days = orbit.PeriodSeconds / Constants.SecondsPerDay;

            Assert.Relative(0.0485, au, 0.10, "Proxima b semi-major axis (AU)");
            Assert.Relative(11.19, days, 0.10, "Proxima b orbital period (days)");

            // Its host is a red dwarf: about 12% of a solar mass and far cooler than the Sun.
            CelestialBody proxima = Catalog.Get(proximaB.ParentId);
            Assert.True(proxima != null && proxima.IsStar, "Proxima b's parent should be a star");
            Assert.Relative(0.122 * Constants.GM_Sun, proxima.GravitationalParameter, 0.15, "Proxima mass");
            Assert.True(proxima.EffectiveTemperatureK.HasValue && proxima.EffectiveTemperatureK.Value < 3500,
                "Proxima is an M dwarf and must be cool");
        }

        [Test("every orbit is self-consistent through Kepler's third law")]
        public static void OrbitsAreSelfConsistent()
        {
            // Whether the semi-major axis was published or derived from the period, the stored
            // elements and the stored GM must agree with each other. A mismatch means the two
            // came from different places and one is wrong.
            int checked_ = 0;
            foreach (CelestialBody body in Catalog.Bodies)
            {
                if (!body.Orbit.HasValue) continue;
                OrbitalElements orbit = body.Orbit.Value;

                double a = orbit.SemiMajorAxisKm;
                Assert.True(a > 0.0 && !double.IsInfinity(a), $"[{body.Id}] bad semi-major axis {a}");

                double expectedPeriod = MathD.TwoPi * System.Math.Sqrt(a * a * a / orbit.GravitationalParameter);
                Assert.Relative(expectedPeriod, orbit.PeriodSeconds, 1e-9, $"[{body.Id}] period vs a and GM");

                // And the planet must orbit outside its star, not inside it.
                CelestialBody star = Catalog.Get(body.ParentId);
                if (star?.EquatorialRadiusKm != null)
                {
                    Assert.True(orbit.PeriapsisKm > star.EquatorialRadiusKm.Value,
                        $"[{body.Id}] periapsis {orbit.PeriapsisKm:G4} km is inside {star.Id}");
                }
                checked_++;
            }

            Assert.True(checked_ >= 100, $"only {checked_} orbits checked");
        }

        [Test("nothing invented is labelled as measured")]
        public static void InventedFieldsAreNotClaimedAsMeasured()
        {
            // This is the test the whole project exists for. An exoplanet's surface, its orbital
            // orientation on the sky and its atmosphere are not measured for any planet in this
            // catalogue — if any of them ever comes back Measured, the provenance system has
            // stopped meaning anything.
            int surfaces = 0, orientations = 0, measuredOrbits = 0, inferredOrbits = 0;

            foreach (CelestialBody body in Catalog.Bodies)
            {
                if (body.Type != BodyType.Planet) continue;

                FieldProvenance surface = body.Provenance.Get("surface");
                Assert.True(surface.Class == ProvenanceClass.Procedural,
                    $"[{body.Id}] surface is '{surface.Class}' — no exoplanet has a resolved surface");
                surfaces++;

                FieldProvenance orientation = body.Provenance.Get("orbit.orientation");
                Assert.True(orientation.Class == ProvenanceClass.Procedural,
                    $"[{body.Id}] orbit orientation is '{orientation.Class}' but is not measured");
                orientations++;

                FieldProvenance orbit = body.Provenance.Get("orbit");
                Assert.True(orbit.Class == ProvenanceClass.Measured || orbit.Class == ProvenanceClass.Inferred,
                    $"[{body.Id}] orbit provenance is '{orbit.Class}'");
                if (orbit.Class == ProvenanceClass.Measured) measuredOrbits++; else inferredOrbits++;

                // Anything claiming to be measured must name where it came from.
                foreach (string field in body.Provenance.Fields)
                {
                    FieldProvenance entry = body.Provenance.Get(field);
                    if (entry.Class == ProvenanceClass.Measured)
                    {
                        Assert.True(!string.IsNullOrWhiteSpace(entry.Source),
                            $"[{body.Id}.{field}] claims Measured with no source");
                    }
                }

                // And the object's overall badge must be the weakest claim, which for a planet
                // with an invented surface can never be Measured.
                Assert.True(body.Provenance.WeakestClass() != ProvenanceClass.Measured,
                    $"[{body.Id}] is badged Measured despite having procedural fields");
            }

            Assert.True(surfaces >= 100, $"only {surfaces} planets checked");
            Console.WriteLine($"      {surfaces} planets: {measuredOrbits} measured orbits, " +
                              $"{inferredOrbits} inferred from period, all surfaces procedural");
        }

        [Test("host stars are positioned and planets are not")]
        public static void PositionsAreOnStarsOnly()
        {
            foreach (CelestialBody body in Catalog.Bodies)
            {
                if (body.IsStar)
                {
                    Assert.True(body.PositionKm.HasValue, $"[{body.Id}] host star has no position");
                    Assert.True(body.PositionKm.Value.Magnitude > Constants.LightYear,
                        $"[{body.Id}] is closer than a light year, which cannot be right");
                    Assert.True(body.Provenance.Get("positionKm").Class == ProvenanceClass.Measured,
                        $"[{body.Id}] position should trace to ra/dec/sy_dist");
                }
                else
                {
                    // A planet's place comes from its orbit, not from a fixed position.
                    Assert.False(body.PositionKm.HasValue,
                        $"[{body.Id}] is a planet and must not carry a fixed position");
                }
            }
        }

        [Test("the frame tree resolves interstellar distances at full precision")]
        public static void FrameTreeHandlesInterstellarDistances()
        {
            ReferenceFrame root = Catalog.BuildFrameTree();
            var epoch = CosmicTime.FromUtc(2026, 1, 1);

            CelestialBody proximaB = Catalog.FindByName("Proxima Cen b");
            ReferenceFrame planetFrame = root.Find(proximaB.Id);
            ReferenceFrame starFrame = root.Find(proximaB.ParentId);

            // The planet relative to its own star is an 0.05 AU quantity resolved from a
            // position 4.2 light years out. That is the entire reason for the frame tree: the
            // subtraction never happens in absolute coordinates.
            double localKm = planetFrame.StateRelativeTo(starFrame, epoch).Position.Magnitude;
            double expectedKm = proximaB.Orbit.Value.SemiMajorAxisKm;
            Assert.Relative(expectedKm, localKm, 1e-6, "Proxima b's distance from its star");

            // And the absolute position is still 4.2 light years from the Sun.
            double absoluteLightYears = planetFrame.AbsoluteState(epoch).Position.Magnitude / Constants.LightYear;
            Assert.Relative(4.25, absoluteLightYears, 0.02, "Proxima b's distance from the Sun");

            // A double holds ~16 digits; 4.2 ly in km is 4e13, so metre precision survives with
            // room to spare. Confirm the local orbit is not being quantised by the huge offset.
            Assert.True(localKm > 1e6, "the local orbit should be millions of km, not rounded away");
        }
    }
}
