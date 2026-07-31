using System;
using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    public static class ProceduralTests
    {
        [Test("hashing is deterministic, well distributed and order dependent")]
        public static void HashingIsSound()
        {
            // Determinism is the whole contract: the same coordinate must always give the same
            // world, on every machine and in every session.
            Assert.True(Hash.Hash3(42, 1, 2, 3) == Hash.Hash3(42, 1, 2, 3), "the same input gives the same output");
            Assert.True(Hash.Hash3(42, 1, 2, 3) != Hash.Hash3(43, 1, 2, 3), "the seed changes the output");
            Assert.True(Hash.Hash3(42, 1, 2, 3) != Hash.Hash3(42, 3, 2, 1), "argument order matters");
            Assert.True(Hash.HashString("Kepler-442") == Hash.HashString("Kepler-442"), "string hashing is stable");

            // Uniformity: bucket a large sample and check no bucket is far off the mean. A
            // biased hash would cluster procedural systems in some regions of space.
            const int buckets = 64;
            const int samples = 200000;
            var counts = new int[buckets];
            for (int i = 0; i < samples; i++) counts[(int)(Hash.ToUnit(Hash.Hash1(7, i)) * buckets)]++;

            double expected = (double)samples / buckets;
            foreach (int count in counts)
            {
                Assert.True(System.Math.Abs(count - expected) < expected * 0.06,
                    $"bucket count {count} deviates too far from the expected {expected:F0}");
            }

            // Every value must stay inside the advertised range.
            for (int i = 0; i < 10000; i++)
            {
                double unit = Hash.ToUnit(Hash.Hash1(3, i));
                Assert.True(unit >= 0.0 && unit < 1.0, $"ToUnit produced {unit}");
                double signed = Hash.ToSigned(Hash.Hash1(3, i));
                Assert.True(signed >= -1.0 && signed < 1.0, $"ToSigned produced {signed}");
            }
        }

        [Test("distributions have the shape they claim")]
        public static void DistributionsHaveTheRightShape()
        {
            var random = new DeterministicRandom(99);

            // Normal: check the mean and standard deviation of a large sample.
            double sum = 0.0, sumSquares = 0.0;
            const int count = 100000;
            for (int i = 0; i < count; i++)
            {
                double value = random.Normal(5.0, 2.0);
                sum += value;
                sumSquares += value * value;
            }
            double mean = sum / count;
            double standardDeviation = System.Math.Sqrt(sumSquares / count - mean * mean);
            Assert.Equal(5.0, mean, 0.05, "normal distribution mean");
            Assert.Equal(2.0, standardDeviation, 0.05, "normal distribution standard deviation");

            // Power law with a steep negative exponent must produce far more small values than
            // large ones — the property that keeps generated systems from being all giants.
            int small = 0, large = 0;
            for (int i = 0; i < 100000; i++)
            {
                double value = random.PowerLaw(1.0, 100.0, -2.3);
                if (value < 2.0) small++;
                if (value > 50.0) large++;
            }
            Assert.True(small > large * 20,
                $"a -2.3 power law should heavily favour small values, got {small} small vs {large} large");

            // Directions must cover the sphere evenly, not bunch at the poles the way two
            // uniform angles would.
            int northern = 0, equatorial = 0;
            for (int i = 0; i < 100000; i++)
            {
                Double3 direction = random.OnUnitSphere();
                Assert.Equal(1.0, direction.Magnitude, 1e-12, "sphere samples are unit length");
                if (direction.z > 0.5) northern++;
                if (System.Math.Abs(direction.z) < 0.25) equatorial++;
            }
            // By Archimedes' hat-box theorem the area of a sphere between two parallel planes
            // is proportional to their separation, so the cap z>0.5 is a quarter of the
            // surface and the band |z|<0.25 is likewise a quarter.
            Assert.Equal(0.25, northern / 100000.0, 0.01, "polar cap fraction matches its area");
            Assert.Equal(0.25, equatorial / 100000.0, 0.01, "equatorial band fraction matches its area");
        }

        [Test("derived seeds stay independent across channels")]
        public static void DerivedSeedsAreIndependent()
        {
            const long parent = 1234567;
            var seen = new HashSet<long>();

            foreach (string channel in new[] { "planet", "moon", "ring", "atmosphere" })
            {
                for (int i = 0; i < 100; i++)
                {
                    long derived = Hash.Derive(parent, channel, i);
                    Assert.True(seen.Add(derived), $"seed collision at {channel}[{i}]");
                }
            }

            // And derivation is reproducible.
            Assert.True(Hash.Derive(parent, "planet", 3) == Hash.Derive(parent, "planet", 3),
                "derivation is deterministic");

            // Position seeding is stable inside a cell and changes between cells.
            long a = Hash.SeedForPosition(new Double3(1000, 2000, 3000), 500.0, 42);
            long b = Hash.SeedForPosition(new Double3(1100, 2100, 3100), 500.0, 42);
            long c = Hash.SeedForPosition(new Double3(9000, 2000, 3000), 500.0, 42);
            Assert.True(a == b, "positions inside one cell share a seed");
            Assert.True(a != c, "positions in different cells get different seeds");
        }

        [Test("noise is continuous, bounded and repeatable")]
        public static void NoiseIsWellBehaved()
        {
            const long seed = 555;
            var settings = NoiseD.FractalSettings.Default;

            double smallest = double.MaxValue, largest = double.MinValue;
            var random = new DeterministicRandom(1);

            for (int i = 0; i < 20000; i++)
            {
                Double3 point = random.OnUnitSphere() * random.Range(0.5, 8.0);
                double value = NoiseD.Fbm(seed, point, settings);
                smallest = System.Math.Min(smallest, value);
                largest = System.Math.Max(largest, value);
                Assert.True(!double.IsNaN(value), "noise must never produce NaN");
            }

            Assert.True(smallest >= -1.05 && largest <= 1.05,
                $"fBm escaped its nominal range: [{smallest:F4}, {largest:F4}]");
            Assert.True(largest - smallest > 0.6, "fBm should actually vary, not sit near zero");

            // Repeatability.
            var probe = new Double3(1.234, -5.678, 9.012);
            Assert.Equal(NoiseD.Fbm(seed, probe, settings), NoiseD.Fbm(seed, probe, settings), 0.0,
                "noise is a pure function of its arguments");
            Assert.True(NoiseD.Fbm(seed, probe, settings) != NoiseD.Fbm(seed + 1, probe, settings),
                "the seed changes the field");

            // Continuity: a small step must produce a small change, or terrain will show
            // visible cracks at tile boundaries.
            double worstJump = 0.0;
            for (int i = 0; i < 2000; i++)
            {
                Double3 point = random.OnUnitSphere() * 3.0;
                Double3 nudge = random.OnUnitSphere() * 1e-4;
                worstJump = System.Math.Max(worstJump,
                    System.Math.Abs(NoiseD.Fbm(seed, point, settings) - NoiseD.Fbm(seed, point + nudge, settings)));
            }
            Assert.True(worstJump < 0.02, $"noise jumped by {worstJump:F5} over a 1e-4 step; it is not continuous");

            // The other variants must stay bounded too.
            for (int i = 0; i < 5000; i++)
            {
                Double3 point = random.OnUnitSphere() * random.Range(0.5, 6.0);
                double ridged = NoiseD.Ridged(seed, point, settings);
                double billow = NoiseD.Billow(seed, point, settings);
                double worley = NoiseD.Worley(seed, point);
                Assert.True(ridged >= -1.05 && ridged <= 1.05, $"ridged noise escaped range: {ridged}");
                Assert.True(billow >= -1.05 && billow <= 1.05, $"billow noise escaped range: {billow}");
                Assert.True(worley >= 0.0 && worley < 2.0, $"Worley distance out of range: {worley}");
            }
        }

        [Test("terrain produces a coherent surface with the requested character")]
        public static void TerrainIsCoherent()
        {
            var terrain = new TerrainField
            {
                Seed = 314159,
                Class = SurfaceClass.Continental,
                BodyRadiusKm = 6371.0,
                ReliefKm = 20.0,
                OceanFraction = 0.7,
                ContinentFraction = 0.3
            };

            var random = new DeterministicRandom(2718);
            double lowest = double.MaxValue, highest = double.MinValue;
            int submerged = 0;
            const int samples = 20000;

            for (int i = 0; i < samples; i++)
            {
                Double3 direction = random.OnUnitSphere();
                double elevation = terrain.ElevationKm(direction);

                Assert.True(!double.IsNaN(elevation), "elevation must never be NaN");
                lowest = System.Math.Min(lowest, elevation);
                highest = System.Math.Max(highest, elevation);
                if (terrain.IsSubmerged(direction)) submerged++;

                // The visible radius must never fall below the ocean surface.
                if (terrain.HasOcean)
                {
                    Assert.True(terrain.VisibleRadiusKm(direction) >= terrain.BodyRadiusKm + terrain.SeaLevelKm - 1e-9,
                        "the visible surface dipped below sea level");
                }
            }

            Assert.True(highest - lowest > terrain.ReliefKm * 0.2,
                $"terrain relief {highest - lowest:F2} km is far below the requested {terrain.ReliefKm} km");
            Assert.True(highest - lowest <= terrain.ReliefKm * 1.05, "terrain exceeded its requested relief");

            // The ocean should cover a decent share of the surface without swallowing it.
            double oceanFraction = (double)submerged / samples;
            Assert.True(oceanFraction > 0.2 && oceanFraction < 0.95,
                $"ocean covers {oceanFraction:P0} of the surface, which does not look like a water world");

            // Normals must be unit length and point generally outward.
            for (int i = 0; i < 500; i++)
            {
                Double3 direction = random.OnUnitSphere();
                Double3 normal = terrain.SurfaceNormal(direction);
                Assert.Equal(1.0, normal.Magnitude, 1e-9, "surface normals are unit length");
                Assert.True(Double3.Dot(normal, direction) > 0.5,
                    "surface normals should point broadly outward, not sideways");
            }

            // Determinism, again at the level that matters to a player.
            var repeat = new TerrainField
            {
                Seed = 314159, Class = SurfaceClass.Continental, BodyRadiusKm = 6371.0,
                ReliefKm = 20.0, OceanFraction = 0.7, ContinentFraction = 0.3
            };
            var probe = new Double3(0.3, -0.5, 0.81).Normalized;
            Assert.Equal(terrain.ElevationKm(probe), repeat.ElevationKm(probe), 0.0,
                "the same seed and settings give the same terrain");
        }

        [Test("cratered terrain actually produces craters")]
        public static void CrateredTerrainHasCraters()
        {
            var airless = new TerrainField
            {
                Seed = 8080, Class = SurfaceClass.Cratered,
                BodyRadiusKm = 1737.4, ReliefKm = 16.0, CraterDensity = 1.0
            };
            var smooth = new TerrainField
            {
                Seed = 8080, Class = SurfaceClass.Cratered,
                BodyRadiusKm = 1737.4, ReliefKm = 16.0, CraterDensity = 0.0
            };

            // A cratered surface must be measurably rougher than the same surface without them.
            double Roughness(TerrainField field)
            {
                var random = new DeterministicRandom(11);
                double sum = 0.0;
                const int count = 4000;
                for (int i = 0; i < count; i++)
                {
                    Double3 direction = random.OnUnitSphere();
                    sum += System.Math.Abs(field.SlopeRad(direction, 2.0));
                }
                return sum / count;
            }

            double cratered = Roughness(airless);
            double flat = Roughness(smooth);
            Assert.True(cratered > flat * 1.2,
                $"crater density made little difference to roughness ({cratered:F5} vs {flat:F5} rad)");
        }

        [Test("stellar relations reproduce the Sun")]
        public static void StellarRelationsReproduceTheSun()
        {
            // The relations are calibrated on the Sun, so one solar mass must come back out.
            Assert.Equal(1.0, PlanetGenerator.MainSequenceLuminosity(1.0), 1e-9, "solar luminosity");
            Assert.Equal(1.0, PlanetGenerator.MainSequenceRadius(1.0), 1e-9, "solar radius");
            Assert.Equal(Constants.SolarEffectiveTemperature, PlanetGenerator.EffectiveTemperature(1.0, 1.0), 1.0,
                "solar effective temperature");
            Assert.True(PlanetGenerator.SpectralType(5772).StartsWith("G", StringComparison.Ordinal),
                $"the Sun should be a G star, got {PlanetGenerator.SpectralType(5772)}");

            // And the ordering across the main sequence is right.
            Assert.True(PlanetGenerator.MainSequenceLuminosity(0.2) < 0.02, "an M dwarf is very faint");
            Assert.True(PlanetGenerator.MainSequenceLuminosity(10.0) > 1000.0, "a B star is very bright");
            Assert.True(PlanetGenerator.SpectralType(3200).StartsWith("M", StringComparison.Ordinal), "3200 K is class M");
            Assert.True(PlanetGenerator.SpectralType(25000).StartsWith("B", StringComparison.Ordinal), "25000 K is class B");

            // Earth sits inside the Sun's habitable zone, and the snow line falls in the belt.
            PlanetGenerator.HabitableZone(1.0, out double inner, out double outer);
            Assert.True(inner < 1.0 && 1.0 < outer, $"Earth should be inside the habitable zone [{inner:F2}, {outer:F2}] AU");
            Assert.Equal(2.7, PlanetGenerator.SnowLineAu(1.0), 0.01, "solar snow line (AU)");
        }

        [Test("mass-radius and atmospheric retention match the solar system")]
        public static void MassRadiusAndRetentionMatchReality()
        {
            // One Earth mass gives one Earth radius by construction.
            Assert.Equal(1.0, PlanetGenerator.RadiusFromMass(1.0), 1e-9, "Earth's radius from Earth's mass");

            // Jupiter: 318 Earth masses, about 11.2 Earth radii.
            Assert.Equal(11.2, PlanetGenerator.RadiusFromMass(317.8, gaseous: true), 1.0, "Jupiter's radius");

            // A super-Earth is bigger than Earth but not proportionally so.
            double superEarth = PlanetGenerator.RadiusFromMass(5.0);
            Assert.True(superEarth > 1.0 && superEarth < 2.0,
                $"a five-Earth-mass planet should be 1-2 Earth radii, got {superEarth:F2}");

            // Retention: Earth keeps nitrogen but loses hydrogen — the reason it has the
            // atmosphere it does rather than a hydrogen envelope.
            double earthEscape = Constants.EscapeSpeed(Constants.GM_Earth, Constants.EarthEquatorialRadius);
            Assert.True(PlanetGenerator.RetainsGas(earthEscape, 288.0, 28.0), "Earth retains nitrogen");
            Assert.True(!PlanetGenerator.RetainsGas(earthEscape, 288.0, 2.0), "Earth loses hydrogen");

            // The Moon retains nothing.
            double lunarEscape = Constants.EscapeSpeed(Constants.GM_Moon, Constants.LunarRadius);
            Assert.True(!PlanetGenerator.RetainsGas(lunarEscape, 250.0, 28.0), "the Moon cannot hold nitrogen");

            // Titan is smaller than Mars but far colder, and keeps a thick atmosphere anyway.
            double titanEscape = Constants.EscapeSpeed(8978.14, 2574.7);
            Assert.True(PlanetGenerator.RetainsGas(titanEscape, 94.0, 28.0), "Titan retains nitrogen because it is cold");
        }

        [Test("black-body colour runs from red through white to blue")]
        public static void BlackBodyColourIsPhysical()
        {
            Double3 coolStar = PlanetGenerator.BlackBodyColor(3000);
            Double3 sunLike = PlanetGenerator.BlackBodyColor(5772);
            Double3 hotStar = PlanetGenerator.BlackBodyColor(20000);

            Assert.True(coolStar.x > coolStar.z, "a 3000 K star is red");
            Assert.True(hotStar.z > hotStar.x, "a 20000 K star is blue");
            Assert.True(System.Math.Abs(sunLike.x - sunLike.z) < 0.35, "the Sun is close to white");

            foreach (var (name, colour) in new[] { ("cool", coolStar), ("sun", sunLike), ("hot", hotStar) })
            {
                Assert.True(colour.x >= 0 && colour.x <= 1.0001, $"{name} red channel out of range");
                Assert.True(colour.y >= 0 && colour.y <= 1.0001, $"{name} green channel out of range");
                Assert.True(colour.z >= 0 && colour.z <= 1.0001, $"{name} blue channel out of range");
            }
        }

        [Test("generated systems are deterministic, valid and physically ordered")]
        public static void GeneratedSystemsAreValid()
        {
            for (long seed = 1; seed <= 25; seed++)
            {
                var settings = new PlanetGenerator.SystemSettings { Seed = seed, Name = $"Test-{seed}" };
                var catalog = PlanetGenerator.GenerateSystem(settings);

                // Every generated system must survive the same validation real data does.
                var problems = catalog.Validate();
                Assert.True(problems.Count == 0,
                    $"seed {seed} produced an invalid system:\n  " + string.Join("\n  ", problems));

                var star = catalog.Get("star");
                Assert.True(star != null && star.IsStar, $"seed {seed} has no star");
                Assert.True(star.LuminosityWatts.Value > 0.0, $"seed {seed} star has no luminosity");

                // Planets must be ordered outward and never overlap their star.
                double previousAxis = 0.0;
                int planets = 0;
                foreach (var planet in catalog.ChildrenOf("star"))
                {
                    planets++;
                    double axis = planet.Orbit.Value.SemiMajorAxisKm;
                    Assert.True(axis > previousAxis, $"seed {seed}: planet orbits are not ordered outward");
                    Assert.True(planet.Orbit.Value.PeriapsisKm > star.EquatorialRadiusKm.Value,
                        $"seed {seed}: a planet's periapsis is inside the star");
                    previousAxis = axis;

                    Assert.True(planet.DensityKgPerM3.Value > 10.0 && planet.DensityKgPerM3.Value < 30000.0,
                        $"seed {seed}: implausible planet density {planet.DensityKgPerM3.Value:F0} kg/m^3");

                    // Everything invented must be labelled as invented.
                    Assert.True(planet.Provenance.WeakestClass() == ProvenanceClass.Procedural,
                        $"seed {seed}: generated planet is not marked procedural");
                }

                Assert.True(planets >= settings.MinPlanets && planets <= settings.MaxPlanets,
                    $"seed {seed} produced {planets} planets, outside the requested range");
            }

            // Determinism across separate invocations.
            var first = PlanetGenerator.GenerateSystem(new PlanetGenerator.SystemSettings { Seed = 777 });
            var second = PlanetGenerator.GenerateSystem(new PlanetGenerator.SystemSettings { Seed = 777 });
            Assert.True(first.Count == second.Count, "the same seed gives the same planet count");

            foreach (var body in first.Bodies)
            {
                var twin = second.Get(body.Id);
                Assert.True(twin != null, $"body {body.Id} missing from the regenerated system");
                Assert.Equal(body.GravitationalParameter, twin.GravitationalParameter, 0.0,
                    $"{body.Id} GM differs between runs");
                if (body.Orbit.HasValue)
                {
                    Assert.Equal(body.Orbit.Value.SemiLatusRectumKm, twin.Orbit.Value.SemiLatusRectumKm, 0.0,
                        $"{body.Id} orbit differs between runs");
                }
            }

            // Different seeds must give genuinely different systems.
            var other = PlanetGenerator.GenerateSystem(new PlanetGenerator.SystemSettings { Seed = 778 });
            Assert.True(other.Get("star").GravitationalParameter != first.Get("star").GravitationalParameter,
                "different seeds should give different stars");
        }

        [Test("generated systems survive a JSON round trip")]
        public static void GeneratedSystemsRoundTrip()
        {
            var original = PlanetGenerator.GenerateSystem(
                new PlanetGenerator.SystemSettings { Seed = 20260730, Name = "Round Trip" });

            var reparsed = CelestialCatalog.Parse(original.ToJsonString());

            Assert.True(reparsed.Count == original.Count, "body count survives");
            Assert.True(reparsed.Validate().Count == 0, "the reparsed system is still valid");

            foreach (var body in original.Bodies)
            {
                var twin = reparsed.Get(body.Id);
                Assert.True(twin != null, $"{body.Id} survives the round trip");
                Assert.Relative(body.GravitationalParameter, twin.GravitationalParameter, 1e-12, $"{body.Id} GM");

                if (body.Orbit.HasValue)
                {
                    var before = body.Orbit.Value.Propagate(CosmicTime.J2000 + 1e7);
                    var after = twin.Orbit.Value.Propagate(CosmicTime.J2000 + 1e7);
                    Assert.VectorClose(before.Position, after.Position, before.Position.Magnitude * 1e-9,
                        $"{body.Id} propagates identically after reparsing");
                }
            }
        }
    }
}
