using System;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    public static class SurfaceTests
    {
        private static ColorRgb White => new ColorRgb(1f, 1f, 1f);

        [Test("surface colours stay in range and never produce NaN")]
        public static void SurfaceColoursAreWellFormed()
        {
            var random = new DeterministicRandom(31337);

            foreach (SurfaceClass surfaceClass in Enum.GetValues(typeof(SurfaceClass)))
            {
                var terrain = new TerrainField
                {
                    Seed = 4242, Class = surfaceClass, BodyRadiusKm = 6371.0, ReliefKm = 18.0,
                    OceanFraction = surfaceClass == SurfaceClass.Continental ? 0.65 : 0.0,
                    CraterDensity = surfaceClass == SurfaceClass.Cratered ? 1.0 : 0.0
                };

                for (int i = 0; i < 1500; i++)
                {
                    Double3 direction = random.OnUnitSphere();
                    ColorRgb colour = SurfacePainter.Paint(terrain, direction, 255.0, White);

                    Assert.True(!float.IsNaN(colour.R) && !float.IsNaN(colour.G) && !float.IsNaN(colour.B),
                        $"[{surfaceClass}] produced NaN");
                    // A little headroom above 1 is fine — the tint can brighten — but a colour
                    // far outside the range means a multiply has gone wrong.
                    Assert.True(colour.R >= 0f && colour.R <= 2f, $"[{surfaceClass}] red {colour.R} out of range");
                    Assert.True(colour.G >= 0f && colour.G <= 2f, $"[{surfaceClass}] green {colour.G} out of range");
                    Assert.True(colour.B >= 0f && colour.B <= 2f, $"[{surfaceClass}] blue {colour.B} out of range");
                }
            }
        }

        [Test("painting is deterministic")]
        public static void PaintingIsDeterministic()
        {
            TerrainField Build() => new TerrainField
            {
                Seed = 90210, Class = SurfaceClass.Continental, BodyRadiusKm = 6371.0,
                ReliefKm = 20.0, OceanFraction = 0.7
            };

            var probe = new Double3(0.31, -0.52, 0.79).Normalized;
            ColorRgb first = SurfacePainter.Paint(Build(), probe, 255.0, White);
            ColorRgb second = SurfacePainter.Paint(Build(), probe, 255.0, White);

            Assert.Equal(first.R, second.R, 0.0, "red is reproducible");
            Assert.Equal(first.G, second.G, 0.0, "green is reproducible");
            Assert.Equal(first.B, second.B, 0.0, "blue is reproducible");
        }

        [Test("a cold world freezes over and a warm one does not")]
        public static void TemperatureDrivesIce()
        {
            var terrain = new TerrainField
            {
                Seed = 777, Class = SurfaceClass.Continental, BodyRadiusKm = 6371.0,
                ReliefKm = 18.0, OceanFraction = 0.6
            };

            double BrightFraction(double equilibriumK)
            {
                var random = new DeterministicRandom(5);
                int bright = 0;
                const int samples = 2000;
                for (int i = 0; i < samples; i++)
                {
                    ColorRgb colour = SurfacePainter.Paint(terrain, random.OnUnitSphere(), equilibriumK, White);
                    // Snow and ice are the only near-white entries in the palette.
                    if (colour.R > 0.6f && colour.G > 0.6f && colour.B > 0.6f) bright++;
                }
                return bright / (double)samples;
            }

            double frozen = BrightFraction(120.0);
            double temperate = BrightFraction(288.0);

            Assert.True(frozen > 0.8, $"a 120 K world should be almost entirely frozen, got {frozen:P0}");
            Assert.True(temperate < 0.35, $"a 288 K world should not be, got {temperate:P0}");
        }

        [Test("the observed disc colour survives the procedural detail")]
        public static void BaseColourIsPreserved()
        {
            // Mars is red because it is measured to be red. Whatever the generator invents for
            // its surface must not turn it green — the measured value has to dominate the hue.
            var terrain = new TerrainField
            {
                Seed = 4, Class = SurfaceClass.Continental, BodyRadiusKm = 3396.0,
                ReliefKm = 25.0, OceanFraction = 0.0
            };
            var marsColour = new ColorRgb(0.72f, 0.42f, 0.28f);

            var random = new DeterministicRandom(11);
            int reddish = 0;
            const int samples = 1200;

            for (int i = 0; i < samples; i++)
            {
                ColorRgb colour = SurfacePainter.Paint(terrain, random.OnUnitSphere(), 210.0, marsColour);
                if (colour.R > colour.G && colour.G >= colour.B * 0.9f) reddish++;
            }

            double fraction = reddish / (double)samples;
            Assert.True(fraction > 0.75,
                $"only {fraction:P0} of a Mars-tinted surface came out red-dominant");
        }

        [Test("surface class is chosen from what is known about the body")]
        public static void SurfaceClassFollowsTheData()
        {
            CelestialBody Body(string id, double radiusKm, double pressureBar) => new CelestialBody
            {
                Id = id,
                Type = BodyType.Planet,
                GravitationalParameter = Constants.GM_Earth,
                EquatorialRadiusKm = radiusKm,
                PolarRadiusKm = radiusKm,
                Atmosphere = pressureBar > 0.0
                    ? new AtmosphereDefinition { SurfacePressureBar = pressureBar, ScaleHeightKm = 8.5 }
                    : null
            };

            // Airless bodies saturate with craters, because nothing erases them.
            TerrainField airless = SurfacePainter.TerrainFor(Body("moon", 1737.0, 0.0), 270.0);
            Assert.True(airless.Class == SurfaceClass.Cratered, $"an airless warm body should be cratered, got {airless.Class}");
            Assert.True(airless.CraterDensity > 0.5, "and heavily so");

            // Cold, airless and large: an ice shell, as at Europa.
            TerrainField icy = SurfacePainter.TerrainFor(Body("europa", 1560.0, 0.0), 102.0);
            Assert.True(icy.Class == SurfaceClass.IceShell, $"a cold airless body should be an ice shell, got {icy.Class}");

            // A gas giant has no surface at all.
            TerrainField giant = SurfacePainter.TerrainFor(Body("jupiter", 71492.0, 1000.0), 165.0);
            Assert.True(giant.Class == SurfaceClass.GasGiant, $"a large body should be a gas giant, got {giant.Class}");
            Assert.Equal(0.0, giant.ElevationKm(new Double3(1, 0, 0)), 0.0, "a gas giant has no elevation");

            // Temperate and thick-aired: continental, with an ocean.
            TerrainField earthLike = SurfacePainter.TerrainFor(Body("earth", 6378.0, 1.0), 255.0);
            Assert.True(earthLike.Class == SurfaceClass.Continental, $"expected continental, got {earthLike.Class}");
            Assert.True(earthLike.OceanFraction > 0.0, "a temperate world with air should have an ocean");

            // Too hot for liquid water: no ocean, even with an atmosphere.
            TerrainField venusLike = SurfacePainter.TerrainFor(Body("venus", 6051.0, 92.0), 420.0);
            Assert.True(venusLike.OceanFraction == 0.0, "a 420 K world cannot have a water ocean");
        }

        [Test("relief scales sub-linearly with radius, as gravity requires")]
        public static void ReliefScalesWithGravity()
        {
            CelestialBody Body(double radiusKm) => new CelestialBody
            {
                Id = $"b{radiusKm}", Type = BodyType.Planet,
                GravitationalParameter = Constants.GM_Earth,
                EquatorialRadiusKm = radiusKm, PolarRadiusKm = radiusKm,
                Atmosphere = new AtmosphereDefinition { SurfacePressureBar = 1.0, ScaleHeightKm = 8.5 }
            };

            double smallRelief = SurfacePainter.TerrainFor(Body(3396.0), 210.0).ReliefKm;
            double largeRelief = SurfacePainter.TerrainFor(Body(6378.0), 255.0).ReliefKm;

            Assert.True(largeRelief > smallRelief, "a larger body still has more absolute relief");

            // But proportionally less: a smaller body has weaker surface gravity and supports
            // proportionally taller mountains, which is why Olympus Mons is on Mars.
            double smallRatio = smallRelief / 3396.0;
            double largeRatio = largeRelief / 6378.0;
            Assert.True(smallRatio > largeRatio,
                $"relief should be a larger fraction of a small body ({smallRatio:E2} vs {largeRatio:E2})");
        }

        [Test("height map agrees with the elevation field and flattens water")]
        public static void HeightMapMatchesTerrain()
        {
            var terrain = new TerrainField
            {
                Seed = 8, Class = SurfaceClass.Continental, BodyRadiusKm = 6371.0,
                ReliefKm = 20.0, OceanFraction = 0.6
            };

            var random = new DeterministicRandom(3);
            int flatWater = 0, water = 0;

            for (int i = 0; i < 3000; i++)
            {
                Double3 direction = random.OnUnitSphere();
                double height = SurfacePainter.HeightAt(terrain, direction);

                Assert.True(height >= 0.0 && height <= 1.0, $"height {height} out of range");

                if (terrain.IsSubmerged(direction))
                {
                    water++;
                    // Shading the sea floor through the surface looks like a fault line, so
                    // water must be flat in the height map.
                    if (System.Math.Abs(height - 0.5) < 1e-9) flatWater++;
                }
            }

            Assert.True(water > 100, "the test world should have some ocean to check");
            Assert.True(flatWater == water, $"{water - flatWater} of {water} submerged samples were not flat");
        }

        [Test("the water mask marks sea as smooth and land as rough")]
        public static void WaterMaskSeparatesSeaFromLand()
        {
            // This mask becomes the alpha of the metallic/smoothness map, and alpha is where
            // Unity's Standard shader reads smoothness from. If it leaked onto land, continents
            // would throw a mirror glint; if it missed the sea, oceans would be as matte as rock
            // and the single most recognisable feature of a habitable world would be gone.
            var terrain = new TerrainField
            {
                Seed = 12, Class = SurfaceClass.Continental, BodyRadiusKm = 6371.0,
                ReliefKm = 20.0, OceanFraction = 0.7
            };

            var random = new DeterministicRandom(19);
            int wet = 0, dry = 0, openWater = 0;

            for (int i = 0; i < 4000; i++)
            {
                Double3 direction = random.OnUnitSphere();
                double mask = SurfacePainter.WaterMask(terrain, direction, 288.0);
                Assert.True(mask >= 0.0 && mask <= 1.0, $"mask {mask} out of range");

                if (terrain.IsSubmerged(direction))
                {
                    // Every submerged point is smoother than every land point, but not equally:
                    // the poles of a 288 K world are below freezing, and sea ice is matte. The
                    // floor is what matters here — water is never as rough as rock.
                    Assert.True(mask >= 0.3, $"sea returned {mask}");
                    if (mask > 0.9) openWater++;
                    wet++;
                }
                else
                {
                    Assert.True(mask == 0.0, $"land returned {mask}");
                    dry++;
                }
            }

            Assert.True(wet > 500 && dry > 500, $"expected a mixed world, got {wet} wet and {dry} dry");
            Assert.True(openWater > wet / 2, $"most of a temperate world's sea should be liquid, got {openWater}/{wet}");

            // Cool the same point below freezing and it roughens: ice is not a mirror.
            Double3 sea = EquatorialSea(terrain);
            double liquid = SurfacePainter.WaterMask(terrain, sea, 288.0);
            double frozen = SurfacePainter.WaterMask(terrain, sea, 220.0);
            Assert.True(frozen < liquid, $"ice ({frozen}) should be rougher than water ({liquid})");

            // And a world with no sea at all has nothing smooth anywhere.
            var desert = new TerrainField
            {
                Seed = 12, Class = SurfaceClass.Cratered, BodyRadiusKm = 3390.0,
                ReliefKm = 25.0, OceanFraction = 0.0
            };
            for (int i = 0; i < 500; i++)
            {
                Assert.True(SurfacePainter.WaterMask(desert, random.OnUnitSphere(), 210.0) == 0.0,
                    "a world with no ocean must have no water");
            }
        }

        /// <summary>A submerged point near the equator, where the sea is warmest and so liquid.</summary>
        private static Double3 EquatorialSea(TerrainField terrain)
        {
            var random = new DeterministicRandom(77);
            for (int i = 0; i < 20000; i++)
            {
                Double3 direction = random.OnUnitSphere();
                if (System.Math.Abs(direction.z) < 0.15 && terrain.IsSubmerged(direction)) return direction;
            }
            throw new System.InvalidOperationException("no equatorial sea found");
        }
    }
}
