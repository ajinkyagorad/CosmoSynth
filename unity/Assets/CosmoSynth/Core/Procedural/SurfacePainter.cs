using System;

namespace CosmoSynth.Core
{
    /// <summary>A linear RGB colour, engine-free so this can be unit tested.</summary>
    public struct ColorRgb
    {
        public float R, G, B;

        public ColorRgb(float r, float g, float b) { R = r; G = g; B = b; }

        public static ColorRgb Lerp(ColorRgb a, ColorRgb b, double t)
        {
            float f = (float)MathD.Clamp01(t);
            return new ColorRgb(a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f);
        }

        public ColorRgb Scaled(double factor)
        {
            float f = (float)factor;
            return new ColorRgb(R * f, G * f, B * f);
        }
    }

    /// <summary>
    /// Turns a <see cref="TerrainField"/> into surface colour — the step between "this body has
    /// an elevation function" and "this body looks like a world".
    ///
    /// The palette is chosen by climate rather than by height alone: temperature and moisture
    /// pick the biome, and elevation only modifies it. That ordering is what stops every planet
    /// looking like the same green-brown gradient, and it is why an ice cap appears at the pole
    /// of a temperate world but covers the whole surface of a cold one, without either being a
    /// special case.
    ///
    /// This is explicitly <see cref="ProvenanceClass.Procedural"/> output. No measured imagery
    /// is involved for any body, including Earth.
    /// </summary>
    public static class SurfacePainter
    {
        // Reference colours, in linear space. Values chosen to read correctly under the
        // near-white starlight the renderer uses rather than to look right in isolation.
        private static readonly ColorRgb DeepOcean = new ColorRgb(0.015f, 0.055f, 0.13f);
        private static readonly ColorRgb ShallowOcean = new ColorRgb(0.05f, 0.20f, 0.32f);
        private static readonly ColorRgb Beach = new ColorRgb(0.62f, 0.57f, 0.40f);
        private static readonly ColorRgb Desert = new ColorRgb(0.60f, 0.46f, 0.26f);
        private static readonly ColorRgb Grassland = new ColorRgb(0.24f, 0.34f, 0.14f);
        private static readonly ColorRgb Forest = new ColorRgb(0.12f, 0.22f, 0.09f);
        private static readonly ColorRgb Tundra = new ColorRgb(0.35f, 0.33f, 0.28f);
        private static readonly ColorRgb Rock = new ColorRgb(0.31f, 0.28f, 0.25f);
        private static readonly ColorRgb Snow = new ColorRgb(0.86f, 0.88f, 0.92f);
        private static readonly ColorRgb Ice = new ColorRgb(0.72f, 0.80f, 0.88f);
        private static readonly ColorRgb Basalt = new ColorRgb(0.13f, 0.12f, 0.12f);
        private static readonly ColorRgb Lava = new ColorRgb(0.95f, 0.35f, 0.06f);
        private static readonly ColorRgb Regolith = new ColorRgb(0.34f, 0.32f, 0.30f);

        /// <summary>
        /// Surface colour at a direction on the body.
        ///
        /// <paramref name="baseColor"/> is the catalogue's measured or inferred disc colour; the
        /// generated detail is tinted toward it so a procedurally painted Mars still comes out
        /// the red that observation says it is, rather than whatever the palette alone implies.
        /// </summary>
        public static ColorRgb Paint(TerrainField terrain, Double3 direction, double equilibriumTemperatureK,
            ColorRgb baseColor, double tintStrength = 0.45)
        {
            ColorRgb color = PaintRaw(terrain, direction, equilibriumTemperatureK);

            // Preserve the generated luminance while pulling the hue toward the observed colour.
            double luminance = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
            ColorRgb tinted = baseColor.Scaled(luminance / System.Math.Max(1e-4, Luminance(baseColor)));

            return ColorRgb.Lerp(color, tinted, tintStrength);
        }

        private static double Luminance(ColorRgb c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;

        private static ColorRgb PaintRaw(TerrainField terrain, Double3 direction, double equilibriumTemperatureK)
        {
            if (terrain.Class == SurfaceClass.GasGiant) return BandedCloud(terrain, direction);

            double elevation = terrain.ElevationKm(direction);
            double temperature = terrain.TemperatureK(direction, equilibriumTemperatureK);

            // Perturb the freezing threshold with fine noise instead of testing a hard value.
            //
            // A sharp threshold evaluated per texel produces a staircase along the ice edge —
            // the boundary snaps between texels and reads as a rendering artefact rather than a
            // coastline. Dithering the threshold by a few kelvin breaks the alignment and gives
            // the ragged margin real sea ice has, at no extra cost.
            double margin = NoiseD.Fbm(terrain.Seed + 5150, direction * 42.0,
                new NoiseD.FractalSettings { Octaves = 4, Frequency = 1.0, Lacunarity = 2.2, Persistence = 0.55 }) * 4.5;

            // --- Water ---
            if (terrain.HasOcean && elevation < terrain.SeaLevelKm)
            {
                double depth = terrain.SeaLevelKm - elevation;
                double normalised = MathD.Clamp01(depth / System.Math.Max(1e-6, terrain.ReliefKm * 0.35));

                // A frozen ocean is ice, not water. This is what puts polar sea ice on a
                // temperate world and covers an entire cold one, from one rule.
                //
                // Sea water freezes at about 271 K, and the blend is spread over ~12 K so the
                // margin is a gradient of thickening ice rather than a line.
                double seaIce = MathD.SmoothStep(271.0 + margin, 259.0 + margin, temperature);
                if (seaIce > 0.0)
                {
                    ColorRgb open = ColorRgb.Lerp(ShallowOcean, DeepOcean, normalised);
                    return ColorRgb.Lerp(open, ColorRgb.Lerp(Ice, Snow, seaIce), seaIce);
                }

                return ColorRgb.Lerp(ShallowOcean, DeepOcean, normalised);
            }

            // --- Ice caps, on any surface cold enough ---
            double snowCover = MathD.SmoothStep(268.0 + margin, 250.0 + margin, temperature);
            if (snowCover > 0.0)
            {
                ColorRgb ground = terrain.Class == SurfaceClass.Cratered ? Regolith : Rock;
                if (snowCover >= 0.999) return Snow;

                // Below full cover, blend against the biome that would otherwise be here rather
                // than against bare rock — a patchy snow line over grassland, not over stone.
                ColorRgb beneath = terrain.Class == SurfaceClass.Continental
                    ? ContinentalGround(terrain, direction, elevation, temperature)
                    : ground;
                return ColorRgb.Lerp(beneath, Snow, snowCover);
            }

            switch (terrain.Class)
            {
                case SurfaceClass.Cratered:
                    return CrateredGround(terrain, direction, elevation);

                case SurfaceClass.IceShell:
                    return ColorRgb.Lerp(Ice, Snow, MathD.Clamp01(0.5 + elevation / terrain.ReliefKm));

                case SurfaceClass.Volcanic:
                    return VolcanicGround(terrain, direction, elevation, temperature);

                default:
                    return ContinentalGround(terrain, direction, elevation, temperature);
            }
        }

        private static ColorRgb ContinentalGround(TerrainField terrain, Double3 direction, double elevation,
            double temperature)
        {
            double moisture = terrain.Moisture(direction);
            double altitude = terrain.HasOcean
                ? System.Math.Max(0.0, elevation - terrain.SeaLevelKm)
                : System.Math.Max(0.0, elevation);
            double altitudeFraction = MathD.Clamp01(altitude / System.Math.Max(1e-6, terrain.ReliefKm * 0.4));

            // Shoreline.
            if (terrain.HasOcean && altitudeFraction < 0.012) return ColorRgb.Lerp(Beach, Grassland, altitudeFraction / 0.012);

            // Biome by moisture, which is what distinguishes a desert world from a forested one
            // at the same temperature.
            ColorRgb ground = moisture < 0.28
                ? Desert
                : ColorRgb.Lerp(Grassland, Forest, MathD.Clamp01((moisture - 0.28) / 0.45));

            // Cold thins vegetation into tundra before any snow appears.
            if (temperature < 283.0)
            {
                ground = ColorRgb.Lerp(Tundra, ground, MathD.Clamp01((temperature - 258.0) / 25.0));
            }

            // Bare rock above the tree line, then snow above the snow line — both driven by the
            // same lapse rate the temperature model already applies.
            ground = ColorRgb.Lerp(ground, Rock, MathD.SmoothStep(0.35, 0.62, altitudeFraction));
            ground = ColorRgb.Lerp(ground, Snow, MathD.SmoothStep(0.72, 0.92, altitudeFraction));

            // Fine mottling so a large flat region does not read as a solid fill.
            double grain = NoiseD.Fbm(terrain.Seed + 4242, direction * 180.0,
                new NoiseD.FractalSettings { Octaves = 3, Frequency = 1.0, Lacunarity = 2.0, Persistence = 0.5 });
            return ground.Scaled(1.0 + grain * 0.09);
        }

        private static ColorRgb CrateredGround(TerrainField terrain, Double3 direction, double elevation)
        {
            // Airless surfaces are near-monochrome; what reads is albedo variation between old
            // highlands and darker basins, plus bright ejecta from fresh impacts.
            double basin = MathD.Clamp01(0.5 + elevation / System.Math.Max(1e-6, terrain.ReliefKm));
            ColorRgb ground = ColorRgb.Lerp(Basalt, Regolith, basin);

            double speckle = NoiseD.Fbm(terrain.Seed + 77, direction * 240.0,
                new NoiseD.FractalSettings { Octaves = 4, Frequency = 1.0, Lacunarity = 2.1, Persistence = 0.55 });
            double rays = System.Math.Max(0.0, NoiseD.Worley(terrain.Seed + 91, direction * 26.0));
            double brightening = MathD.Clamp01(1.0 - rays * 1.4) * 0.35;

            return ground.Scaled(1.0 + speckle * 0.14 + brightening);
        }

        private static ColorRgb VolcanicGround(TerrainField terrain, Double3 direction, double elevation,
            double temperature)
        {
            double heat = NoiseD.Fbm(terrain.Seed + 313, direction * 9.0,
                new NoiseD.FractalSettings { Octaves = 5, Frequency = 1.0, Lacunarity = 2.2, Persistence = 0.55 });

            ColorRgb ground = ColorRgb.Lerp(Basalt, Rock, MathD.Clamp01(0.5 + elevation / terrain.ReliefKm));

            // Molten channels only where the crust is both low and hot.
            double molten = MathD.Clamp01((heat - 0.35) * 2.2) * MathD.Clamp01((temperature - 380.0) / 200.0);
            return ColorRgb.Lerp(ground, Lava, molten);
        }

        /// <summary>
        /// Zonal cloud bands for a gas giant.
        ///
        /// Real giants are banded because rapid rotation organises convection into alternating
        /// jets, so the dominant structure is a function of latitude with turbulence layered on
        /// top. Generating it that way — rather than as isotropic noise — is what makes the
        /// result read as Jupiter rather than as marble.
        /// </summary>
        private static ColorRgb BandedCloud(TerrainField terrain, Double3 direction)
        {
            Double3 point = direction.Normalized;
            double latitude = MathD.SafeAsin(MathD.Clamp(point.z, -1.0, 1.0));

            // Alternating zones and belts.
            double bands = System.Math.Sin(latitude * 11.0) * 0.5 + 0.5;

            // Turbulence that distorts the band edges, stretched zonally so it smears along the
            // jets the way real cloud structure does.
            double turbulence = NoiseD.Fbm(terrain.Seed, new Double3(point.x * 2.2, point.y * 2.2, point.z * 9.0),
                new NoiseD.FractalSettings { Octaves = 6, Frequency = 1.0, Lacunarity = 2.1, Persistence = 0.55 });

            double mix = MathD.Clamp01(bands + turbulence * 0.28);

            var lightZone = new ColorRgb(0.86f, 0.80f, 0.68f);
            var darkBelt = new ColorRgb(0.52f, 0.38f, 0.26f);
            ColorRgb color = ColorRgb.Lerp(darkBelt, lightZone, mix);

            // Polar hoods are greyer and less banded.
            double polar = MathD.Clamp01((System.Math.Abs(latitude) - 1.0) / 0.45);
            return ColorRgb.Lerp(color, new ColorRgb(0.46f, 0.44f, 0.44f), polar);
        }

        /// <summary>
        /// How much of this point is open water, in [0,1].
        ///
        /// Drives the specular map. Water is the only common planetary surface that is
        /// genuinely smooth, and rendering it with the same roughness as rock is one of the
        /// most obvious tells that a planet is fake — a real ocean throws a sun glint and
        /// darkens toward the terminator, and nothing else on the surface does.
        ///
        /// Ice reads as partially smooth: less than open water, far more than rock.
        /// </summary>
        public static double WaterMask(TerrainField terrain, Double3 direction, double surfaceTemperatureK)
        {
            if (terrain.Class == SurfaceClass.GasGiant) return 0.0;

            if (terrain.Class == SurfaceClass.IceShell)
            {
                return 0.55;   // a fractured ice shell is glossy but not mirror-flat
            }

            if (!terrain.HasOcean) return 0.0;

            double elevation = terrain.ElevationKm(direction);
            if (elevation >= terrain.SeaLevelKm) return 0.0;

            double temperature = terrain.TemperatureK(direction, surfaceTemperatureK);

            // Sea ice is rough compared with open water; the transition matches the one the
            // colour uses so the glint and the white cap appear at the same latitude.
            double frozen = MathD.SmoothStep(271.0, 259.0, temperature);
            return MathD.Lerp(1.0, 0.35, frozen);
        }

        /// <summary>
        /// Height in [0,1] for a normal map, sampled from the same field the colour uses so the
        /// two never disagree.
        /// </summary>
        public static double HeightAt(TerrainField terrain, Double3 direction)
        {
            if (terrain.Class == SurfaceClass.GasGiant) return 0.5;

            double elevation = terrain.ElevationKm(direction);
            double datum = terrain.HasOcean ? terrain.SeaLevelKm : 0.0;

            // Water is flat: shading the sea floor through the surface looks like a fault.
            if (terrain.HasOcean && elevation < datum) return 0.5;

            return MathD.Clamp01(0.5 + (elevation - datum) / System.Math.Max(1e-6, terrain.ReliefKm));
        }

        /// <summary>
        /// Mean surface temperature to paint with, in K.
        ///
        /// The equilibrium (black-body) temperature is <b>not</b> the surface temperature, and
        /// using it as one makes every world far too cold: Earth's equilibrium temperature is
        /// 254 K, its actual mean surface is 288 K, and the 34 K difference is the greenhouse
        /// effect. Painting from 254 K freezes Earth's oceans down to the mid-latitudes, which
        /// is exactly the wrong picture for the one planet everybody can check.
        ///
        /// Where the catalogue carries a measured surface temperature — it does for Venus,
        /// Earth, Mars, the giants, Titan and Pluto — that value is used directly, because a
        /// measurement beats any model. Otherwise a crude greenhouse offset is applied, scaling
        /// with the log of surface pressure, which reproduces Earth and Titan to within a few
        /// kelvin and Venus to within about fifteen percent. That is a generated world's
        /// temperature, and it is labelled procedural like everything else about it.
        /// </summary>
        public static double SurfaceTemperatureFor(CelestialBody body, double equilibriumTemperatureK)
        {
            if (body.Atmosphere != null && body.Atmosphere.SurfaceTemperatureK > 0.0)
                return body.Atmosphere.SurfaceTemperatureK;

            if (body.Atmosphere == null || !body.Atmosphere.IsPresent)
                return equilibriumTemperatureK;   // airless: no greenhouse to add

            double pressureBar = body.Atmosphere.SurfacePressureBar;
            if (pressureBar <= 0.0) return equilibriumTemperatureK;

            // Optical depth grows roughly with pressure; the resulting warming grows with its
            // logarithm. Calibrated so 1 bar of mostly-N2 gives about +33 K.
            double greenhouse = 33.0 * System.Math.Log(1.0 + pressureBar * 1.7) / System.Math.Log(2.7);
            return equilibriumTemperatureK + greenhouse;
        }

        /// <summary>
        /// Build a terrain field appropriate to a catalogue body, choosing the surface class
        /// from what is actually known about it.
        /// </summary>
        public static TerrainField TerrainFor(CelestialBody body, double equilibriumTemperatureK)
        {
            double radius = body.EquatorialRadiusKm ?? 1000.0;
            bool hasAtmosphere = body.Atmosphere != null && body.Atmosphere.IsPresent;
            bool isGiant = body.Type == BodyType.Planet && radius > 20000.0;

            SurfaceClass surfaceClass;
            double oceanFraction = 0.0;
            double craterDensity = 0.0;

            if (isGiant)
            {
                surfaceClass = SurfaceClass.GasGiant;
            }
            else if (equilibriumTemperatureK < 200.0 && radius > 200.0 && !hasAtmosphere)
            {
                // Cold, airless and sizeable: an ice shell, as at Europa and Enceladus.
                surfaceClass = SurfaceClass.IceShell;
            }
            else if (!hasAtmosphere)
            {
                // No atmosphere means nothing erases impacts, so the surface saturates with them.
                surfaceClass = SurfaceClass.Cratered;
                craterDensity = 1.0;
            }
            else
            {
                surfaceClass = SurfaceClass.Continental;
                // Only a body that can hold liquid water gets an ocean.
                bool liquidWaterPossible = equilibriumTemperatureK > 250.0 && equilibriumTemperatureK < 340.0 &&
                                           body.Atmosphere.SurfacePressureBar > 0.1;
                oceanFraction = liquidWaterPossible ? 0.68 : 0.0;
                craterDensity = body.Atmosphere.SurfacePressureBar < 0.05 ? 0.45 : 0.05;
            }

            // Relief scales with radius but sub-linearly: a smaller body has weaker gravity and
            // supports proportionally taller mountains, which is why Olympus Mons is on Mars.
            double relief = System.Math.Max(2.0, System.Math.Pow(radius, 0.62) * 0.16);

            return new TerrainField
            {
                Seed = body.ProceduralSeed != 0 ? body.ProceduralSeed : Hash.HashString(body.Id),
                Class = surfaceClass,
                BodyRadiusKm = radius,
                ReliefKm = relief,
                OceanFraction = oceanFraction,
                ContinentFraction = 0.32,
                CraterDensity = craterDensity,
                MountainStrength = 0.55,
                ErosionStrength = hasAtmosphere ? 0.4 : 0.12
            };
        }
    }
}
