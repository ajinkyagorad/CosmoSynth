using System;
using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// Generating a system around a real star.
    ///
    /// Two properties matter more than anything about the planets themselves. The system must be
    /// the *same* system every time, or a universe you can fly back into does not exist. And the
    /// invented parts must never claim to be measured, because a real star lends its planets a
    /// credibility they have not earned — which is the single easiest way for this project to
    /// start lying.
    /// </summary>
    public static class StarSystemFactoryTests
    {
        private static CatalogStar SunLike(double x = 4.0, double y = 3.0, double z = 2.0) =>
            new CatalogStar(new Double3(x, y, z), 5.2, 5772.0, StarFlags.None);

        [Test("the same star always generates the same system")]
        public static void GenerationIsDeterministic()
        {
            CatalogStar star = SunLike();

            CelestialCatalog first = StarSystemFactory.Create(star, "hyg-1", "Test", CosmicTime.J2000);
            CelestialCatalog second = StarSystemFactory.Create(star, "hyg-1", "Test", CosmicTime.J2000);

            Assert.True(first.Count == second.Count,
                $"same star gave {first.Count} bodies then {second.Count}");

            var firstIds = new List<string>();
            foreach (CelestialBody body in first.Traverse()) firstIds.Add(body.Id);

            int index = 0;
            foreach (CelestialBody body in second.Traverse())
            {
                Assert.True(body.Id == firstIds[index],
                    $"body {index} was '{firstIds[index]}' and is now '{body.Id}'");

                CelestialBody original = first.Get(body.Id);
                if (original.Orbit.HasValue && body.Orbit.HasValue)
                {
                    Assert.Equal(original.Orbit.Value.SemiLatusRectumKm,
                        body.Orbit.Value.SemiLatusRectumKm, 1e-9, $"{body.Id}'s orbit");
                }
                index++;
            }
        }

        [Test("the seed follows the star's position, not its place in the file")]
        public static void SeedIsPositionalAndStable()
        {
            // Two different stars must not share a system.
            Assert.True(StarSystemFactory.SeedFor(SunLike(4, 3, 2)) !=
                        StarSystemFactory.SeedFor(SunLike(9, 1, 7)),
                "two stars in different places produced the same seed");

            // A revised parallax that moves a star by a hundred-thousandth of a parsec must NOT
            // reseed it. Quantisation exists so that rebuilding the catalogue does not silently
            // replace every planetary system in the sky.
            Assert.True(StarSystemFactory.SeedFor(SunLike(4.0, 3.0, 2.0)) ==
                        StarSystemFactory.SeedFor(SunLike(4.000004, 3.000002, 1.999997)),
                "a sub-milliparsec revision changed the seed");

            // Seeds are printed into provenance strings; a negative one reads as a defect.
            Assert.True(StarSystemFactory.SeedFor(SunLike(-11, -7, -3)) >= 0, "seed must be positive");
        }

        [Test("the star stays the real star and is findable under its real id")]
        public static void TheStarIsNotReplacedByAnInventedOne()
        {
            var star = new CatalogStar(new Double3(1.5, -2.5, 0.75), 4.1, 4200.0, StarFlags.None);
            CelestialCatalog catalog = StarSystemFactory.Create(star, "hyg-42", "Test Star",
                CosmicTime.J2000);

            // Re-keying: setting Id on the body is not enough, because the catalogue is a
            // dictionary keyed by the id a body had when it was added. This is the assertion
            // that caught that.
            CelestialBody found = catalog.Get("hyg-42");
            Assert.True(found != null, "the star is not findable under the id it was given");
            Assert.True(found.IsStar, "'hyg-42' is not a star");

            // Its position is the measured one, in km.
            Double3 expected = star.PositionParsecs * Constants.Parsec;
            Assert.True(found.PositionKm.HasValue, "the star has no position");
            Assert.VectorClose(expected, found.PositionKm.Value, 1.0, "the star's position");

            Assert.Equal(4200.0, found.EffectiveTemperatureK ?? 0.0, 1e-6,
                "the star's temperature came from the catalogue");

            // Every planet hangs off the real id, not the generator's.
            foreach (CelestialBody body in catalog.Bodies)
            {
                if (body.IsStar) continue;
                Assert.True(catalog.Contains(body.ParentId),
                    $"{body.Id} names parent '{body.ParentId}', which is not in the catalogue");
            }

            Assert.True(catalog.RootIds.Count == 1 && catalog.RootIds[0] == "hyg-42",
                $"expected one root, the star; got [{string.Join(", ", catalog.RootIds)}]");
        }

        [Test("nothing generated ever claims to be measured")]
        public static void GeneratedFieldsAreNeverMeasured()
        {
            CelestialCatalog catalog = StarSystemFactory.Create(SunLike(), "hyg-7", "Test",
                CosmicTime.J2000);

            foreach (CelestialBody body in catalog.Bodies)
            {
                if (body.IsStar)
                {
                    // The star's position is the one thing here that IS measured — that is the
                    // whole point of hanging invented worlds off a real catalogue.
                    Assert.True(body.Provenance.Get("positionKm").Class == ProvenanceClass.Measured,
                        "the star's position should be Measured, from the catalogue");
                    continue;
                }

                foreach (string field in body.Provenance.Fields)
                {
                    ProvenanceClass claim = body.Provenance.Get(field).Class;
                    Assert.True(claim != ProvenanceClass.Measured,
                        $"{body.Id}.{field} claims to be Measured, but it was invented");
                }
            }
        }

        [Test("the generated system is physically valid")]
        public static void GeneratedSystemsValidate()
        {
            // Across a spread of real stellar parameters, not one favourable case.
            var stars = new[]
            {
                new CatalogStar(new Double3(2, 1, 0.5), 5.0, 5772.0, StarFlags.None),
                new CatalogStar(new Double3(30, -12, 4), 1.2, 9600.0, StarFlags.None),
                new CatalogStar(new Double3(1.3, 0.2, -0.4), 11.0, 3040.0, StarFlags.None),
                // No parallax: mass has to come from temperature instead of luminosity.
                new CatalogStar(new Double3(500, 200, -300), 8.0, 6500.0, StarFlags.DistanceUnknown),
            };

            for (int i = 0; i < stars.Length; i++)
            {
                CelestialCatalog catalog = StarSystemFactory.Create(stars[i], $"hyg-{i}", $"S{i}",
                    CosmicTime.J2000);

                IReadOnlyList<string> problems = catalog.Validate();
                Assert.True(problems.Count == 0,
                    $"star {i} generated an invalid system:\n  " + string.Join("\n  ", problems));

                Assert.True(catalog.Count >= 2, $"star {i} generated no planets at all");
            }
        }

        [Test("mass follows luminosity where there is a parallax, and temperature where there is not")]
        public static void MassUsesTheStrongestAvailableEvidence()
        {
            // A Sun at 10 parsecs has apparent magnitude 4.83 by construction, so its derived
            // luminosity is 1 solar and its mass should come out near 1.
            var sun = new CatalogStar(new Double3(10.0, 0.0, 0.0), 4.83, 5772.0, StarFlags.None);
            Assert.Equal(1.0, StarSystemFactory.LuminositySolar(sun) ?? 0.0, 0.02,
                "a solar-magnitude star at 10 pc should be one solar luminosity");
            Assert.Equal(1.0, StarSystemFactory.MassSolar(sun), 0.15, "its implied mass");

            // With no parallax there is no luminosity at all — apparent magnitude alone cannot
            // distinguish a nearby dim star from a distant bright one.
            var unknown = new CatalogStar(new Double3(1e5, 0.0, 0.0), 4.83, 5772.0,
                StarFlags.DistanceUnknown);
            Assert.False(StarSystemFactory.LuminositySolar(unknown).HasValue,
                "luminosity was derived without a distance");
            Assert.Equal(1.0, StarSystemFactory.MassSolar(unknown), 0.2,
                "a solar-temperature star should still imply about a solar mass");

            // A cool red dwarf is much less massive than a hot blue star.
            var dwarf = new CatalogStar(new Double3(2, 0, 0), 12.0, 3000.0, StarFlags.None);
            var hot = new CatalogStar(new Double3(50, 0, 0), 2.0, 15000.0, StarFlags.None);
            Assert.True(StarSystemFactory.MassSolar(dwarf) < StarSystemFactory.MassSolar(hot),
                "a red dwarf came out heavier than a blue star");
        }
    }
}
