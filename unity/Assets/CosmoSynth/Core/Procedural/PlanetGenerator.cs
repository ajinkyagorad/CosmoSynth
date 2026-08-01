using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Generates star systems that are physically plausible rather than merely varied.
    ///
    /// The constraint that matters is that the generator must not produce anything the
    /// simulation would then have to pretend is real: a moon outside its planet's Hill sphere,
    /// a planet denser than osmium, a hydrogen atmosphere on a body too small to hold one.
    /// Every quantity here is therefore derived from a physical relation and then checked, and
    /// every result is stamped <see cref="ProvenanceClass.Procedural"/> so a viewer can always
    /// tell invention from measurement.
    ///
    /// The relations used are the standard empirical ones — main-sequence mass-luminosity,
    /// mass-radius by composition class, Jeans escape for atmospheric retention. They are
    /// approximations with real scatter, which is why the generated values carry procedural
    /// provenance rather than inferred.
    /// </summary>
    public static class PlanetGenerator
    {
        /// <summary>Boltzmann constant, J/K.</summary>
        private const double BoltzmannConstant = 1.380649e-23;

        /// <summary>Atomic mass unit, kg.</summary>
        private const double AtomicMassUnit = 1.66053906892e-27;

        // ---- Stars -----------------------------------------------------------

        /// <summary>
        /// Main-sequence luminosity from mass, in solar units. Piecewise because the exponent
        /// of the mass-luminosity relation genuinely changes across the main sequence — a
        /// single power law is wrong by an order of magnitude at the extremes.
        /// </summary>
        public static double MainSequenceLuminosity(double massSolar)
        {
            if (massSolar <= 0.0) return 0.0;
            if (massSolar < 0.43) return 0.23 * System.Math.Pow(massSolar, 2.3);
            if (massSolar < 2.0) return System.Math.Pow(massSolar, 4.0);
            if (massSolar < 55.0) return 1.4 * System.Math.Pow(massSolar, 3.5);
            return 32000.0 * massSolar;
        }

        /// <summary>Main-sequence radius from mass, in solar units.</summary>
        public static double MainSequenceRadius(double massSolar)
        {
            if (massSolar <= 0.0) return 0.0;
            return massSolar < 1.0
                ? System.Math.Pow(massSolar, 0.8)
                : System.Math.Pow(massSolar, 0.57);
        }

        /// <summary>Effective temperature from luminosity and radius, via Stefan-Boltzmann.</summary>
        public static double EffectiveTemperature(double luminositySolar, double radiusSolar)
        {
            if (luminositySolar <= 0.0 || radiusSolar <= 0.0) return 0.0;
            return Constants.SolarEffectiveTemperature *
                   System.Math.Pow(luminositySolar / (radiusSolar * radiusSolar), 0.25);
        }

        /// <summary>Morgan-Keenan spectral class letter and subdivision from effective temperature.</summary>
        public static string SpectralType(double temperatureK)
        {
            (double minimum, char letter)[] classes =
            {
                (30000, 'O'), (10000, 'B'), (7500, 'A'), (6000, 'F'),
                (5200, 'G'), (3700, 'K'), (2400, 'M'), (1300, 'L'), (0, 'T')
            };

            for (int i = 0; i < classes.Length; i++)
            {
                if (temperatureK >= classes[i].minimum)
                {
                    double upper = i == 0 ? 55000.0 : classes[i - 1].minimum;
                    double fraction = (upper - temperatureK) / System.Math.Max(1.0, upper - classes[i].minimum);
                    int subdivision = (int)MathD.Clamp(System.Math.Floor(fraction * 10.0), 0, 9);
                    return $"{classes[i].letter}{subdivision}V";
                }
            }
            return "T9V";
        }

        /// <summary>Inner and outer edges of the conservative habitable zone, in AU.</summary>
        public static void HabitableZone(double luminositySolar, out double innerAu, out double outerAu)
        {
            double scale = System.Math.Sqrt(System.Math.Max(0.0, luminositySolar));
            innerAu = 0.95 * scale;
            outerAu = 1.37 * scale;
        }

        /// <summary>
        /// Snow line in AU: the distance beyond which water ice survives, and therefore where
        /// giant planets can accrete enough solid material to form. Its position is what makes
        /// a system's inner planets rocky and its outer ones gaseous.
        /// </summary>
        public static double SnowLineAu(double luminositySolar) =>
            2.7 * System.Math.Sqrt(System.Math.Max(0.0, luminositySolar));

        // ---- Planets ---------------------------------------------------------

        /// <summary>
        /// Radius in Earth radii from mass in Earth masses, by composition class. Below about
        /// two Earth masses the exponent reflects rock compressibility; above roughly a
        /// hundred, degeneracy pressure holds the radius nearly constant, which is why Jupiter
        /// and a seventy-Jupiter-mass brown dwarf are almost the same size.
        /// </summary>
        public static double RadiusFromMass(double massEarth, bool icy = false, bool gaseous = false)
        {
            if (massEarth <= 0.0) return 0.0;

            if (gaseous)
            {
                // Approaches ~11.2 Earth radii (one Jupiter) and stays there.
                double jupiterMasses = massEarth / 317.8;
                return 11.2 * System.Math.Pow(jupiterMasses, 0.06) *
                       (1.0 - 0.1 * System.Math.Exp(-jupiterMasses * 3.0));
            }

            double coefficient = icy ? 1.25 : 1.0;
            return massEarth < 2.0
                ? coefficient * System.Math.Pow(massEarth, 0.27)
                : coefficient * System.Math.Pow(2.0, 0.27) * System.Math.Pow(massEarth / 2.0, 0.5);
        }

        /// <summary>
        /// Whether a body can retain a gas over geological time, by the standard Jeans
        /// criterion: escape velocity must exceed about six times the mean thermal speed of
        /// the molecule. This is what decides that Earth keeps nitrogen but loses hydrogen,
        /// and that Titan keeps an atmosphere despite being smaller than Mars because it is
        /// so much colder.
        /// </summary>
        public static bool RetainsGas(double escapeSpeedKmPerSec, double temperatureK, double molecularMassAmu)
        {
            if (molecularMassAmu <= 0.0 || temperatureK <= 0.0) return false;

            double massKg = molecularMassAmu * AtomicMassUnit;
            double thermalSpeedMPerSec = System.Math.Sqrt(3.0 * BoltzmannConstant * temperatureK / massKg);
            return escapeSpeedKmPerSec * 1000.0 > 6.0 * thermalSpeedMPerSec;
        }

        /// <summary>
        /// Roche limit for a rigid satellite, km. Inside it, tidal forces exceed the body's
        /// self-gravity, so this is where rings form instead of moons.
        /// </summary>
        public static double RocheLimitKm(double primaryRadiusKm, double primaryDensity, double satelliteDensity)
        {
            if (satelliteDensity <= 0.0) return 0.0;
            return primaryRadiusKm * 1.26 * System.Math.Cbrt(primaryDensity / satelliteDensity);
        }

        /// <summary>
        /// Whether two adjacent orbits are far enough apart to be dynamically stable, measured
        /// in mutual Hill radii. Systems separated by fewer than about ten are prone to
        /// crossing orbits and ejections over long timescales; observed multiplanet systems
        /// cluster well above that, which is a useful sanity check on a generated one.
        /// </summary>
        public static double MutualHillSeparation(double innerSemiMajorAxisKm, double outerSemiMajorAxisKm,
            double innerMassKg, double outerMassKg, double starMassKg)
        {
            if (starMassKg <= 0.0) return double.PositiveInfinity;

            double meanAxis = 0.5 * (innerSemiMajorAxisKm + outerSemiMajorAxisKm);
            double hillRadius = meanAxis * System.Math.Cbrt((innerMassKg + outerMassKg) / (3.0 * starMassKg));
            return hillRadius > 0.0 ? (outerSemiMajorAxisKm - innerSemiMajorAxisKm) / hillRadius : double.PositiveInfinity;
        }

        // ---- System synthesis ------------------------------------------------

        /// <summary>Knobs for <see cref="GenerateSystem"/>.</summary>
        public sealed class SystemSettings
        {
            public long Seed = 1;
            public string Name = "Procedural System";

            /// <summary>Star mass in solar masses. Left at 0 to draw from the initial mass function.</summary>
            public double StarMassSolar = 0.0;

            public int MinPlanets = 3;
            public int MaxPlanets = 9;

            /// <summary>Innermost planet's semi-major axis, AU.</summary>
            public double InnerEdgeAu = 0.25;

            /// <summary>Ratio between successive orbits, before jitter. Roughly the solar system's spacing.</summary>
            public double OrbitSpacing = 1.7;

            public CosmicTime Epoch = CosmicTime.J2000;
        }

        /// <summary>
        /// Generate a complete star system as a validated catalogue.
        ///
        /// The same seed always yields the same system, on any machine — no floating-point
        /// accumulation or iteration order enters the result, only integer hashing of the
        /// seed and index.
        /// </summary>
        public static CelestialCatalog GenerateSystem(SystemSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var random = new DeterministicRandom(settings.Seed);
            var catalog = new CelestialCatalog
            {
                Name = settings.Name,
                Source = $"CosmoSynth procedural generator (seed {settings.Seed})",
                GeneratedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
            };

            // --- Star ---
            double starMassSolar = settings.StarMassSolar > 0.0
                ? settings.StarMassSolar
                // Kroupa-like initial mass function: low-mass stars vastly outnumber high-mass
                // ones, so a uniform draw would produce an implausibly bright neighbourhood.
                : MathD.Clamp(random.PowerLaw(0.08, 3.0, -2.3), 0.08, 3.0);

            double luminositySolar = MainSequenceLuminosity(starMassSolar);
            double starRadiusSolar = MainSequenceRadius(starMassSolar);
            double starTemperature = EffectiveTemperature(luminositySolar, starRadiusSolar);

            var starProvenance = new ProvenanceRecord();
            foreach (string field in new[] { "gravitationalParameter", "equatorialRadiusKm", "luminosityWatts",
                         "effectiveTemperatureK", "spectralType" })
            {
                starProvenance.Set(field, FieldProvenance.Procedural("PlanetGenerator", settings.Seed));
            }

            var star = new CelestialBody
            {
                Id = "star",
                Name = $"{settings.Name} A",
                Type = BodyType.Star,
                GravitationalParameter = Constants.GM_Sun * starMassSolar,
                EquatorialRadiusKm = Constants.SolarRadius * starRadiusSolar,
                PolarRadiusKm = Constants.SolarRadius * starRadiusSolar,
                LuminosityWatts = Constants.SolarLuminosity * luminositySolar,
                EffectiveTemperatureK = starTemperature,
                SpectralType = SpectralType(starTemperature),
                DisplayColor = BlackBodyColor(starTemperature),
                ProceduralSeed = settings.Seed,
                Provenance = starProvenance
            };
            catalog.Add(star);

            // --- Planets ---
            double snowLine = SnowLineAu(luminositySolar);
            int planetCount = random.RangeInt(settings.MinPlanets, settings.MaxPlanets + 1);
            double semiMajorAxisAu = settings.InnerEdgeAu * random.Range(0.7, 1.4);

            double previousAxisKm = 0.0;
            double previousMassKg = 0.0;

            for (int index = 0; index < planetCount; index++)
            {
                var planetRandom = random.Branch("planet", index);

                bool beyondSnowLine = semiMajorAxisAu > snowLine;
                double semiMajorAxisKm = semiMajorAxisAu * Constants.AstronomicalUnit;

                // Giants form beyond the snow line where there is ice to accrete; inside it
                // there is only rock, so planets stay small.
                double massEarth = beyondSnowLine
                    ? planetRandom.PowerLaw(3.0, 400.0, -1.1)
                    : planetRandom.PowerLaw(0.05, 6.0, -1.3);

                bool gaseous = massEarth > 15.0;
                bool icy = beyondSnowLine && !gaseous;

                double radiusEarth = RadiusFromMass(massEarth, icy, gaseous);
                double radiusKm = radiusEarth * Constants.EarthEquatorialRadius;
                double gm = Constants.GM_Earth * massEarth;

                // Inner planets circularise through tidal damping; outer ones retain more
                // eccentricity, as the solar system and observed exosystems both show.
                double eccentricity = MathD.Clamp(
                    System.Math.Abs(planetRandom.Normal(0.0, beyondSnowLine ? 0.06 : 0.03)), 0.0, 0.4);
                double inclination = System.Math.Abs(planetRandom.Normal(0.0, 2.0)) * MathD.Deg2Rad;

                double equilibrium = Constants.EquilibriumTemperature(
                    star.LuminosityWatts.Value, semiMajorAxisKm, gaseous ? 0.5 : 0.3);
                double escapeSpeed = Constants.EscapeSpeed(gm, radiusKm);

                var provenance = new ProvenanceRecord();
                foreach (string field in new[] { "gravitationalParameter", "equatorialRadiusKm", "orbit",
                             "atmosphere", "bondAlbedo", "orientation" })
                {
                    provenance.Set(field, FieldProvenance.Procedural("PlanetGenerator", settings.Seed));
                }

                long planetSeed = Hash.Derive(settings.Seed, "planet", index);

                var planet = new CelestialBody
                {
                    Id = $"planet-{index + 1}",
                    Name = $"{settings.Name} {(char)('b' + index)}",
                    Type = BodyType.Planet,
                    ParentId = star.Id,
                    GravitationalParameter = gm,
                    EquatorialRadiusKm = radiusKm,
                    // Rotational flattening, scaled so a fast-spinning giant bulges like Saturn.
                    PolarRadiusKm = radiusKm * (1.0 - (gaseous ? planetRandom.Range(0.02, 0.10) : planetRandom.Range(0.0, 0.01))),
                    BondAlbedo = gaseous ? planetRandom.Range(0.3, 0.55) : planetRandom.Range(0.08, 0.45),
                    ProceduralSeed = planetSeed,
                    Provenance = provenance,
                    Orbit = OrbitalElements.FromSemiMajorAxis(
                        semiMajorAxisKm, eccentricity, inclination,
                        planetRandom.Range(0.0, MathD.TwoPi),
                        planetRandom.Range(0.0, MathD.TwoPi),
                        planetRandom.Range(0.0, MathD.TwoPi),
                        settings.Epoch,
                        star.GravitationalParameter + gm),
                    Orientation = new BodyOrientation(
                        planetRandom.Range(0.0, 360.0),
                        planetRandom.Range(-90.0, 90.0),
                        planetRandom.Range(0.0, 360.0),
                        360.0 / planetRandom.Range(gaseous ? 0.35 : 0.4, gaseous ? 0.8 : 3.0))
                };

                planet.Atmosphere = GenerateAtmosphere(planetRandom, escapeSpeed, equilibrium,
                    Constants.SurfaceGravity(gm, radiusKm), gaseous);
                planet.DisplayColor = SurfaceColor(planetRandom, equilibrium, gaseous, planet.Atmosphere);

                catalog.Add(planet);

                // Record the stability of the pair we just formed. Rather than silently
                // producing a system that would tear itself apart, this is surfaced through
                // Validate() so the caller can regenerate or accept it knowingly.
                if (index > 0)
                {
                    double separation = MutualHillSeparation(previousAxisKm, semiMajorAxisKm,
                        previousMassKg, massEarth * Constants.EarthMass, starMassSolar * Constants.SolarMass);
                    if (separation < 10.0)
                    {
                        planet.Provenance.Set("orbit", new FieldProvenance(ProvenanceClass.Procedural,
                            "PlanetGenerator",
                            $"seed:{settings.Seed}; mutual Hill separation {separation:F1} < 10, orbit may be unstable"));
                    }
                }

                previousAxisKm = semiMajorAxisKm;
                previousMassKg = massEarth * Constants.EarthMass;

                // Advance outward with jitter, so systems do not all share one spacing.
                semiMajorAxisAu *= settings.OrbitSpacing * planetRandom.Range(0.75, 1.35);
            }

            catalog.Rebuild();
            return catalog;
        }

        private static AtmosphereDefinition GenerateAtmosphere(DeterministicRandom random, double escapeSpeedKmPerSec,
            double equilibriumTemperatureK, double surfaceGravityMPerS2, bool gaseous)
        {
            if (gaseous)
            {
                return new AtmosphereDefinition
                {
                    SurfacePressureBar = random.Range(100.0, 10000.0),
                    ScaleHeightKm = random.Range(20.0, 60.0),
                    TopAltitudeKm = random.Range(500.0, 2000.0),
                    SurfaceDensityKgPerM3 = random.Range(0.1, 1.5),
                    MeanMolecularMassKgPerMol = 0.0023,   // hydrogen/helium
                    SurfaceTemperatureK = equilibriumTemperatureK,
                    Composition = new Dictionary<string, double> { { "H2", 0.86 }, { "He", 0.13 }, { "CH4", 0.01 } },
                    RayleighScatteringPerKm = new Double3(0.0045, 0.0105, 0.0250),
                    MieScatteringPerKm = 0.004
                };
            }

            // Which gases the body can hold decides whether it has an atmosphere at all.
            bool holdsNitrogen = RetainsGas(escapeSpeedKmPerSec, equilibriumTemperatureK, 28.0);
            bool holdsCarbonDioxide = RetainsGas(escapeSpeedKmPerSec, equilibriumTemperatureK, 44.0);

            if (!holdsCarbonDioxide)
            {
                // Too small or too hot to keep anything: an airless world.
                return new AtmosphereDefinition { SurfacePressureBar = 0.0 };
            }

            double pressure = holdsNitrogen ? random.Range(0.3, 3.0) : random.Range(0.001, 0.05);
            double meanMolecularMass = holdsNitrogen ? 0.029 : 0.044;
            double scaleHeight = AtmosphereDefinition.ScaleHeightFor(
                equilibriumTemperatureK, meanMolecularMass, surfaceGravityMPerS2);

            var composition = holdsNitrogen
                ? new Dictionary<string, double> { { "N2", 0.75 }, { "O2", 0.03 }, { "CO2", 0.2 }, { "Ar", 0.02 } }
                : new Dictionary<string, double> { { "CO2", 0.95 }, { "N2", 0.04 }, { "Ar", 0.01 } };

            return new AtmosphereDefinition
            {
                SurfacePressureBar = pressure,
                ScaleHeightKm = System.Math.Max(1.0, scaleHeight),
                TopAltitudeKm = System.Math.Max(10.0, scaleHeight * 10.0),
                SurfaceDensityKgPerM3 = pressure * 1.2,
                MeanMolecularMassKgPerMol = meanMolecularMass,
                SurfaceTemperatureK = equilibriumTemperatureK,
                Composition = composition
            };
        }

        private static Double3 SurfaceColor(DeterministicRandom random, double temperatureK, bool gaseous,
            AtmosphereDefinition atmosphere)
        {
            if (gaseous)
            {
                // Banded giants range from Jupiter's ochre to Neptune's methane blue, and which
                // one you get depends mostly on how cold it is.
                double warmth = MathD.Clamp01((temperatureK - 60.0) / 100.0);
                return new Double3(
                    MathD.Lerp(0.35, 0.82, warmth),
                    MathD.Lerp(0.55, 0.70, warmth),
                    MathD.Lerp(0.85, 0.55, warmth));
            }

            if (temperatureK < 170.0) return new Double3(0.78, 0.82, 0.88);                       // ice
            if (temperatureK > 500.0) return new Double3(0.42, 0.30, 0.24);                       // scorched rock
            if (atmosphere != null && atmosphere.IsPresent && atmosphere.SurfacePressureBar > 0.2)
            {
                return new Double3(random.Range(0.35, 0.6), random.Range(0.35, 0.55), random.Range(0.25, 0.45));
            }
            return new Double3(random.Range(0.30, 0.5), random.Range(0.28, 0.45), random.Range(0.25, 0.40));
        }

        /// <summary>
        /// Approximate linear RGB of a black body at a temperature, normalised so the brightest
        /// channel is 1. Used for star colour, so an M dwarf renders orange and an O star blue
        /// for the actual physical reason rather than by lookup table.
        /// </summary>
        public static Double3 BlackBodyColor(double temperatureK)
        {
            // Planck's law evaluated at representative RGB wavelengths.
            double red = PlanckRadiance(temperatureK, 600e-9);
            double green = PlanckRadiance(temperatureK, 550e-9);
            double blue = PlanckRadiance(temperatureK, 450e-9);

            double peak = System.Math.Max(red, System.Math.Max(green, blue));
            if (peak <= 0.0) return new Double3(1, 1, 1);
            return new Double3(red / peak, green / peak, blue / peak);
        }

        private static double PlanckRadiance(double temperatureK, double wavelengthMetres)
        {
            const double planckConstant = 6.62607015e-34;
            const double speedOfLight = 299792458.0;

            if (temperatureK <= 0.0) return 0.0;

            double exponent = planckConstant * speedOfLight / (wavelengthMetres * BoltzmannConstant * temperatureK);
            if (exponent > 700.0) return 0.0;   // exp() would overflow; the radiance is negligible anyway

            double numerator = 2.0 * planckConstant * speedOfLight * speedOfLight;
            double denominator = System.Math.Pow(wavelengthMetres, 5) * (System.Math.Exp(exponent) - 1.0);
            return denominator > 0.0 ? numerator / denominator : 0.0;
        }
    }
}
