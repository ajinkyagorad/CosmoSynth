using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    public enum BodyType
    {
        Unknown = 0,
        Star,
        Planet,
        DwarfPlanet,
        Moon,
        Asteroid,
        Comet,
        Barycenter,
        Spacecraft,
        Ring,
        Nebula,
        Galaxy,
        StarCluster
    }

    /// <summary>
    /// Atmospheric properties, at the level of detail a scattering shader and an entry model
    /// actually consume. Deliberately not a chemistry model — the goal is a sky that is the
    /// right colour for the right reason, not a radiative transfer solver.
    ///
    /// Not <c>[Serializable]</c>: the composition dictionary and the nullable fields on
    /// <see cref="CelestialBody"/> are outside what Unity's serialiser supports, so marking
    /// these types only produces a stream of "field is skipped by serialization" warnings for
    /// a facility nothing uses. Persistence goes through the JSON layer instead, which handles
    /// all of it and is shared with the non-Unity build.
    /// </summary>
    public sealed class AtmosphereDefinition
    {
        /// <summary>Surface pressure, bar. Zero means no atmosphere.</summary>
        public double SurfacePressureBar;

        /// <summary>
        /// Scale height, km: the altitude over which density falls by a factor of e. Governs
        /// how sharp the limb looks and how quickly drag builds during entry.
        /// </summary>
        public double ScaleHeightKm;

        /// <summary>Altitude at which the atmosphere is treated as ending, km.</summary>
        public double TopAltitudeKm;

        /// <summary>Surface density, kg/m^3. Used by the drag model.</summary>
        public double SurfaceDensityKgPerM3;

        /// <summary>Mean molecular mass, kg/mol — sets the scale height for a given temperature.</summary>
        public double MeanMolecularMassKgPerMol;

        /// <summary>Mean surface temperature, K.</summary>
        public double SurfaceTemperatureK;

        /// <summary>Volume fractions by species, e.g. {"N2": 0.78, "O2": 0.21}.</summary>
        public Dictionary<string, double> Composition = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>
        /// Rayleigh scattering coefficients at RGB wavelengths, per km. These are what give a
        /// sky its colour: Earth's blue comes from the strong wavelength dependence, Mars's
        /// butterscotch from dust that overwhelms it.
        /// </summary>
        public Double3 RayleighScatteringPerKm = new Double3(0.0058, 0.0135, 0.0331);

        /// <summary>Mie scattering coefficient per km — haze and dust, largely wavelength-neutral.</summary>
        public double MieScatteringPerKm = 0.0021;

        /// <summary>Mie asymmetry parameter in (-1,1); positive means forward-scattering.</summary>
        public double MieAsymmetry = 0.758;

        public bool IsPresent => SurfacePressureBar > 0.0 && ScaleHeightKm > 0.0;

        /// <summary>Pressure at an altitude, bar, from the barometric law with a constant scale height.</summary>
        public double PressureAtAltitude(double altitudeKm)
        {
            if (!IsPresent || altitudeKm >= TopAltitudeKm) return 0.0;
            if (altitudeKm <= 0.0) return SurfacePressureBar;
            return SurfacePressureBar * System.Math.Exp(-altitudeKm / ScaleHeightKm);
        }

        /// <summary>Density at an altitude, kg/m^3, on the same isothermal assumption.</summary>
        public double DensityAtAltitude(double altitudeKm)
        {
            if (!IsPresent || altitudeKm >= TopAltitudeKm) return 0.0;
            if (altitudeKm <= 0.0) return SurfaceDensityKgPerM3;
            return SurfaceDensityKgPerM3 * System.Math.Exp(-altitudeKm / ScaleHeightKm);
        }

        /// <summary>
        /// Scale height implied by temperature, molecular mass and gravity: H = RT/(Mg).
        /// Used by the generator to keep an invented atmosphere self-consistent rather than
        /// letting pressure, temperature and thickness be dialled independently into
        /// something that could not exist.
        /// </summary>
        public static double ScaleHeightFor(double temperatureK, double meanMolecularMassKgPerMol,
            double surfaceGravityMPerS2)
        {
            const double universalGasConstant = 8.314462618;  // J/(mol K)
            if (meanMolecularMassKgPerMol <= 0.0 || surfaceGravityMPerS2 <= 0.0) return 0.0;
            return universalGasConstant * temperatureK / (meanMolecularMassKgPerMol * surfaceGravityMPerS2) / 1000.0;
        }

        public JsonValue ToJson()
        {
            var json = JsonValue.Object();
            json["surfacePressureBar"] = JsonValue.Number(SurfacePressureBar);
            json["scaleHeightKm"] = JsonValue.Number(ScaleHeightKm);
            json["topAltitudeKm"] = JsonValue.Number(TopAltitudeKm);
            json["surfaceDensityKgPerM3"] = JsonValue.Number(SurfaceDensityKgPerM3);
            json["meanMolecularMassKgPerMol"] = JsonValue.Number(MeanMolecularMassKgPerMol);
            json["surfaceTemperatureK"] = JsonValue.Number(SurfaceTemperatureK);
            json["rayleighScatteringPerKm"] = JsonValue.Vector(RayleighScatteringPerKm);
            json["mieScatteringPerKm"] = JsonValue.Number(MieScatteringPerKm);
            json["mieAsymmetry"] = JsonValue.Number(MieAsymmetry);

            if (Composition.Count > 0)
            {
                var composition = JsonValue.Object();
                foreach (var pair in Composition) composition[pair.Key] = JsonValue.Number(pair.Value);
                json["composition"] = composition;
            }
            return json;
        }

        public static AtmosphereDefinition FromJson(JsonValue json)
        {
            if (json == null || !json.IsObject) return null;

            var atmosphere = new AtmosphereDefinition
            {
                SurfacePressureBar = json["surfacePressureBar"].AsDouble(),
                ScaleHeightKm = json["scaleHeightKm"].AsDouble(),
                TopAltitudeKm = json["topAltitudeKm"].AsDouble(),
                SurfaceDensityKgPerM3 = json["surfaceDensityKgPerM3"].AsDouble(),
                MeanMolecularMassKgPerMol = json["meanMolecularMassKgPerMol"].AsDouble(),
                SurfaceTemperatureK = json["surfaceTemperatureK"].AsDouble(),
                MieScatteringPerKm = json["mieScatteringPerKm"].AsDouble(0.0021),
                MieAsymmetry = json["mieAsymmetry"].AsDouble(0.758)
            };

            if (json.ContainsKey("rayleighScatteringPerKm"))
                atmosphere.RayleighScatteringPerKm = json["rayleighScatteringPerKm"].AsVector(atmosphere.RayleighScatteringPerKm);

            var composition = json["composition"];
            if (composition.IsObject)
            {
                foreach (string species in composition.Keys)
                    atmosphere.Composition[species] = composition[species].AsDouble();
            }

            return atmosphere;
        }
    }

    /// <summary>
    /// One object in the catalogue: a star, planet, moon, small body or barycentre.
    ///
    /// Values are in the core's km/s/radian convention. Quantities that may genuinely be
    /// unknown are nullable rather than sentinel-valued, because "we do not know this
    /// exoplanet's radius" and "this exoplanet has zero radius" must not be the same state.
    /// </summary>
    public sealed class CelestialBody
    {
        /// <summary>Stable machine identifier, lower-case, e.g. "earth", "jupiter-barycenter".</summary>
        public string Id;

        /// <summary>Display name.</summary>
        public string Name;

        public BodyType Type = BodyType.Unknown;

        /// <summary>Id of the body this one orbits, or null for a root.</summary>
        public string ParentId;

        /// <summary>Cross-identifications: NAIF codes, HIP/Gaia ids, MPC designations.</summary>
        public List<string> Designations = new List<string>();

        // ---- Physical -------------------------------------------------------

        /// <summary>Gravitational parameter, km^3/s^2. Better constrained than mass, and what the dynamics need.</summary>
        public double GravitationalParameter;

        public double? EquatorialRadiusKm;
        public double? PolarRadiusKm;

        /// <summary>Bond albedo (fraction of all incident energy reflected), used for temperature.</summary>
        public double? BondAlbedo;

        /// <summary>Geometric albedo (reflectivity at zero phase angle), used for brightness.</summary>
        public double? GeometricAlbedo;

        // ---- Orbit and rotation ---------------------------------------------

        /// <summary>Orbit about <see cref="ParentId"/>, or null for a root body.</summary>
        public OrbitalElements? Orbit;

        /// <summary>
        /// Fixed position in the root frame, km — for bodies that do not orbit anything in this
        /// catalogue.
        ///
        /// This is what lets an exoplanet host star sit at its real place. Its position comes
        /// from measured right ascension, declination and distance, and it does not orbit the
        /// Sun, so it needs somewhere to say "I am simply here". Stars do of course move, but
        /// their proper motion over any timescale this application animates is far below what
        /// the distance uncertainty already permits.
        /// </summary>
        public Double3? PositionKm;

        public BodyOrientation? Orientation;

        // ---- Star-specific --------------------------------------------------

        public double? LuminosityWatts;
        public double? EffectiveTemperatureK;

        /// <summary>Morgan-Keenan spectral type, e.g. "G2V".</summary>
        public string SpectralType;

        // ---- Environment ----------------------------------------------------

        public AtmosphereDefinition Atmosphere;

        // ---- Presentation ---------------------------------------------------

        /// <summary>Relative path of the albedo map inside StreamingAssets, if one is shipped.</summary>
        public string AlbedoTexture;

        /// <summary>Relative path of the elevation map, if one is shipped.</summary>
        public string HeightmapTexture;

        /// <summary>Fallback display colour, linear RGB in [0,1], used before textures stream in.</summary>
        public Double3 DisplayColor = new Double3(0.6, 0.6, 0.6);

        /// <summary>Seed for procedural detail, so an invented surface is at least reproducible.</summary>
        public long ProceduralSeed;

        // ---- Provenance -----------------------------------------------------

        public ProvenanceRecord Provenance = new ProvenanceRecord();

        // ---- Derived --------------------------------------------------------

        public double MassKg => GravitationalParameter / Constants.G;

        /// <summary>Volumetric mean radius, km, or null if no radius is known.</summary>
        public double? MeanRadiusKm
        {
            get
            {
                if (!EquatorialRadiusKm.HasValue) return null;
                if (!PolarRadiusKm.HasValue) return EquatorialRadiusKm;
                // Geometric mean of the ellipsoid axes.
                double a = EquatorialRadiusKm.Value, c = PolarRadiusKm.Value;
                return System.Math.Cbrt(a * a * c);
            }
        }

        /// <summary>Flattening (a-c)/a, or 0 when the body is spherical or its shape is unknown.</summary>
        public double Flattening
        {
            get
            {
                if (!EquatorialRadiusKm.HasValue || !PolarRadiusKm.HasValue) return 0.0;
                double a = EquatorialRadiusKm.Value;
                return a > 0.0 ? (a - PolarRadiusKm.Value) / a : 0.0;
            }
        }

        /// <summary>Surface gravity in m/s^2 at the equator, or null if the radius is unknown.</summary>
        public double? SurfaceGravity =>
            EquatorialRadiusKm.HasValue
                ? Constants.SurfaceGravity(GravitationalParameter, EquatorialRadiusKm.Value)
                : (double?)null;

        /// <summary>Escape speed in km/s at the equator, or null if the radius is unknown.</summary>
        public double? EscapeSpeed =>
            EquatorialRadiusKm.HasValue
                ? Constants.EscapeSpeed(GravitationalParameter, EquatorialRadiusKm.Value)
                : (double?)null;

        /// <summary>Mean density in kg/m^3, or null if the radius is unknown.</summary>
        public double? DensityKgPerM3
        {
            get
            {
                double? radius = MeanRadiusKm;
                if (!radius.HasValue || radius.Value <= 0.0) return null;
                double volumeM3 = 4.0 / 3.0 * MathD.Pi * System.Math.Pow(radius.Value * 1000.0, 3);
                return MassKg / volumeM3;
            }
        }

        public bool IsStar => Type == BodyType.Star;

        public override string ToString() => $"{Name} ({Type}, id={Id})";

        // ---- Validation -----------------------------------------------------

        /// <summary>
        /// Check the record for physical impossibilities.
        ///
        /// Run before anything is rendered, and in particular on every definition an AI
        /// produces. A language model asked for "a habitable moon" will happily return one
        /// denser than a neutron star or orbiting inside its primary; catching that here keeps
        /// the failure legible instead of letting it surface as a silently broken scene.
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(Id)) problems.Add("Id is required.");
            if (GravitationalParameter < 0.0) problems.Add("GravitationalParameter cannot be negative.");

            if (EquatorialRadiusKm.HasValue && EquatorialRadiusKm.Value <= 0.0)
                problems.Add("EquatorialRadiusKm must be positive when present.");
            if (PolarRadiusKm.HasValue && PolarRadiusKm.Value <= 0.0)
                problems.Add("PolarRadiusKm must be positive when present.");
            if (EquatorialRadiusKm.HasValue && PolarRadiusKm.HasValue && PolarRadiusKm.Value > EquatorialRadiusKm.Value)
                problems.Add("PolarRadiusKm exceeds EquatorialRadiusKm; a rotating body bulges at its equator.");

            if (BondAlbedo.HasValue && (BondAlbedo.Value < 0.0 || BondAlbedo.Value > 1.0))
                problems.Add($"BondAlbedo {BondAlbedo.Value} is outside [0,1].");
            if (GeometricAlbedo.HasValue && GeometricAlbedo.Value < 0.0)
                problems.Add($"GeometricAlbedo {GeometricAlbedo.Value} is negative.");

            double? density = DensityKgPerM3;
            if (density.HasValue)
            {
                // The upper bound has to depend on what the object is. Osmium, the densest
                // element at standard conditions, is 22590 kg/m^3, so a planet past ~30000 is
                // impossible. Stars are a different matter entirely: a late M dwarf is held up
                // partly by electron degeneracy and genuinely reaches 50000 kg/m^3 — Proxima
                // Centauri is about 47000 — and a white dwarf reaches 1e9. Applying the
                // planetary limit to stars rejects most of the real main sequence.
                double densityLimit = Type == BodyType.Star ? 1e11 : 30000.0;
                if (density.Value > densityLimit)
                {
                    problems.Add(Type == BodyType.Star
                        ? $"Mean density {density.Value:G4} kg/m^3 is past white-dwarf territory for a main-sequence star."
                        : $"Mean density {density.Value:G4} kg/m^3 exceeds that of any solid material.");
                }

                if (density.Value < 10.0 && Type != BodyType.Star && Type != BodyType.Nebula)
                    problems.Add($"Mean density {density.Value:G4} kg/m^3 is below that of a gas at room pressure.");
            }

            if (Orbit.HasValue)
            {
                var orbit = Orbit.Value;
                if (orbit.Eccentricity < 0.0) problems.Add("Orbital eccentricity cannot be negative.");
                if (orbit.SemiLatusRectumKm <= 0.0) problems.Add("Orbital semi-latus rectum must be positive.");
                if (orbit.GravitationalParameter <= 0.0) problems.Add("Orbit needs a positive central GM.");
                if (orbit.InclinationRad < 0.0 || orbit.InclinationRad > MathD.Pi)
                    problems.Add($"Inclination {orbit.InclinationRad * MathD.Rad2Deg:F3} deg is outside [0,180].");
                if (string.IsNullOrEmpty(ParentId))
                    problems.Add("A body with an orbit must name the parent it orbits.");
            }
            else if (Type == BodyType.Planet || Type == BodyType.Moon)
            {
                problems.Add($"{Type} '{Id}' has no orbit.");
            }

            if (Atmosphere != null && Atmosphere.IsPresent)
            {
                if (Atmosphere.ScaleHeightKm <= 0.0)
                    problems.Add("Atmosphere has a pressure but no scale height.");
                if (EquatorialRadiusKm.HasValue && Atmosphere.ScaleHeightKm > EquatorialRadiusKm.Value)
                    problems.Add("Atmospheric scale height exceeds the body's radius; it would not be bound.");

                double total = 0.0;
                foreach (var fraction in Atmosphere.Composition.Values) total += fraction;
                if (Atmosphere.Composition.Count > 0 && System.Math.Abs(total - 1.0) > 0.05)
                    problems.Add($"Atmospheric composition fractions sum to {total:F3}, not 1.");
            }

            if (IsStar)
            {
                if (LuminosityWatts.HasValue && LuminosityWatts.Value <= 0.0)
                    problems.Add("A star must have positive luminosity.");
                if (EffectiveTemperatureK.HasValue &&
                    (EffectiveTemperatureK.Value < 500.0 || EffectiveTemperatureK.Value > 200000.0))
                    problems.Add($"Effective temperature {EffectiveTemperatureK.Value:F0} K is outside the stellar range.");
            }

            return problems;
        }

        public bool IsValid => Validate().Count == 0;

        // ---- Serialisation --------------------------------------------------

        public JsonValue ToJson()
        {
            var json = JsonValue.Object();
            json["id"] = JsonValue.String(Id);
            json["name"] = JsonValue.String(Name);
            json["type"] = JsonValue.String(Type.ToString());
            if (!string.IsNullOrEmpty(ParentId)) json["parent"] = JsonValue.String(ParentId);

            if (Designations.Count > 0)
            {
                var designations = JsonValue.Array();
                foreach (string designation in Designations) designations.Add(JsonValue.String(designation));
                json["designations"] = designations;
            }

            json["gravitationalParameter"] = JsonValue.Number(GravitationalParameter);
            if (EquatorialRadiusKm.HasValue) json["equatorialRadiusKm"] = JsonValue.Number(EquatorialRadiusKm.Value);
            if (PolarRadiusKm.HasValue) json["polarRadiusKm"] = JsonValue.Number(PolarRadiusKm.Value);
            if (BondAlbedo.HasValue) json["bondAlbedo"] = JsonValue.Number(BondAlbedo.Value);
            if (GeometricAlbedo.HasValue) json["geometricAlbedo"] = JsonValue.Number(GeometricAlbedo.Value);
            if (LuminosityWatts.HasValue) json["luminosityWatts"] = JsonValue.Number(LuminosityWatts.Value);
            if (EffectiveTemperatureK.HasValue) json["effectiveTemperatureK"] = JsonValue.Number(EffectiveTemperatureK.Value);
            if (!string.IsNullOrEmpty(SpectralType)) json["spectralType"] = JsonValue.String(SpectralType);

            if (PositionKm.HasValue) json["positionKm"] = JsonValue.Vector(PositionKm.Value);

            if (Orbit.HasValue)
            {
                var orbit = Orbit.Value;
                var orbitJson = JsonValue.Object();
                orbitJson["semiLatusRectumKm"] = JsonValue.Number(orbit.SemiLatusRectumKm);
                orbitJson["eccentricity"] = JsonValue.Number(orbit.Eccentricity);
                orbitJson["inclinationDeg"] = JsonValue.Number(orbit.InclinationRad * MathD.Rad2Deg);
                orbitJson["longitudeOfAscendingNodeDeg"] = JsonValue.Number(orbit.LongitudeOfAscendingNodeRad * MathD.Rad2Deg);
                orbitJson["argumentOfPeriapsisDeg"] = JsonValue.Number(orbit.ArgumentOfPeriapsisRad * MathD.Rad2Deg);
                orbitJson["meanAnomalyAtEpochDeg"] = JsonValue.Number(orbit.MeanAnomalyAtEpochRad * MathD.Rad2Deg);
                orbitJson["epochSecondsPastJ2000"] = JsonValue.Number(orbit.Epoch.SecondsPastJ2000);
                orbitJson["gravitationalParameter"] = JsonValue.Number(orbit.GravitationalParameter);
                json["orbit"] = orbitJson;
            }

            if (Orientation.HasValue)
            {
                var orientation = Orientation.Value;
                var orientationJson = JsonValue.Object();
                orientationJson["poleRaDeg"] = JsonValue.Number(orientation.PoleRaDeg);
                orientationJson["poleRaRateDegPerCentury"] = JsonValue.Number(orientation.PoleRaRateDegPerCentury);
                orientationJson["poleDecDeg"] = JsonValue.Number(orientation.PoleDecDeg);
                orientationJson["poleDecRateDegPerCentury"] = JsonValue.Number(orientation.PoleDecRateDegPerCentury);
                orientationJson["primeMeridianDeg"] = JsonValue.Number(orientation.PrimeMeridianDeg);
                orientationJson["rotationRateDegPerDay"] = JsonValue.Number(orientation.RotationRateDegPerDay);
                json["orientation"] = orientationJson;
            }

            if (Atmosphere != null) json["atmosphere"] = Atmosphere.ToJson();
            if (!string.IsNullOrEmpty(AlbedoTexture)) json["albedoTexture"] = JsonValue.String(AlbedoTexture);
            if (!string.IsNullOrEmpty(HeightmapTexture)) json["heightmapTexture"] = JsonValue.String(HeightmapTexture);
            json["displayColor"] = JsonValue.Vector(DisplayColor);
            if (ProceduralSeed != 0) json["proceduralSeed"] = JsonValue.Number(ProceduralSeed);

            if (Provenance != null && Provenance.Count > 0) json["provenance"] = Provenance.ToJson();

            return json;
        }

        public static CelestialBody FromJson(JsonValue json)
        {
            if (json == null || !json.IsObject)
                throw new FormatException("A celestial body must be a JSON object.");

            var body = new CelestialBody
            {
                Id = json.RequireString("id"),
                Name = json["name"].AsString(json["id"].AsString()),
                ParentId = json["parent"].AsString(),
                GravitationalParameter = json["gravitationalParameter"].AsDouble(),
                SpectralType = json["spectralType"].AsString(),
                AlbedoTexture = json["albedoTexture"].AsString(),
                HeightmapTexture = json["heightmapTexture"].AsString(),
                ProceduralSeed = json["proceduralSeed"].AsLong()
            };

            body.Type = Enum.TryParse(json["type"].AsString("Unknown"), ignoreCase: true, out BodyType type)
                ? type
                : BodyType.Unknown;

            foreach (var designation in json["designations"]) body.Designations.Add(designation.AsString());

            if (json.ContainsKey("equatorialRadiusKm")) body.EquatorialRadiusKm = json["equatorialRadiusKm"].AsDouble();
            if (json.ContainsKey("polarRadiusKm")) body.PolarRadiusKm = json["polarRadiusKm"].AsDouble();
            if (json.ContainsKey("bondAlbedo")) body.BondAlbedo = json["bondAlbedo"].AsDouble();
            if (json.ContainsKey("geometricAlbedo")) body.GeometricAlbedo = json["geometricAlbedo"].AsDouble();
            if (json.ContainsKey("luminosityWatts")) body.LuminosityWatts = json["luminosityWatts"].AsDouble();
            if (json.ContainsKey("effectiveTemperatureK")) body.EffectiveTemperatureK = json["effectiveTemperatureK"].AsDouble();
            if (json.ContainsKey("displayColor")) body.DisplayColor = json["displayColor"].AsVector(body.DisplayColor);

            if (json.ContainsKey("positionKm")) body.PositionKm = json["positionKm"].AsVector();

            var orbit = json["orbit"];
            if (orbit.IsObject)
            {
                body.Orbit = new OrbitalElements
                {
                    SemiLatusRectumKm = orbit.RequireDouble("semiLatusRectumKm"),
                    Eccentricity = orbit.RequireDouble("eccentricity"),
                    InclinationRad = orbit["inclinationDeg"].AsDouble() * MathD.Deg2Rad,
                    LongitudeOfAscendingNodeRad = orbit["longitudeOfAscendingNodeDeg"].AsDouble() * MathD.Deg2Rad,
                    ArgumentOfPeriapsisRad = orbit["argumentOfPeriapsisDeg"].AsDouble() * MathD.Deg2Rad,
                    MeanAnomalyAtEpochRad = orbit["meanAnomalyAtEpochDeg"].AsDouble() * MathD.Deg2Rad,
                    Epoch = new CosmicTime(orbit["epochSecondsPastJ2000"].AsDouble()),
                    GravitationalParameter = orbit.RequireDouble("gravitationalParameter")
                };
            }

            // Importers may name a published orientation instead of restating its six numbers,
            // keeping the IAU values in one place (BodyOrientation) rather than copied into
            // every generated catalogue.
            string preset = json["_orientationPreset"].AsString();
            if (!string.IsNullOrEmpty(preset))
            {
                if (BodyOrientation.TryGetPreset(preset, out var presetOrientation))
                {
                    body.Orientation = presetOrientation;
                }
                else
                {
                    throw new FormatException(
                        $"Body '{body.Id}' names orientation preset '{preset}', which is not a known " +
                        "published body. Add it to BodyOrientation or give explicit elements.");
                }
            }

            var orientation = json["orientation"];
            if (orientation.IsObject)
            {
                body.Orientation = new BodyOrientation(
                    orientation["poleRaDeg"].AsDouble(),
                    orientation["poleDecDeg"].AsDouble(),
                    orientation["primeMeridianDeg"].AsDouble(),
                    orientation["rotationRateDegPerDay"].AsDouble(),
                    orientation["poleRaRateDegPerCentury"].AsDouble(),
                    orientation["poleDecRateDegPerCentury"].AsDouble());
            }

            body.Atmosphere = AtmosphereDefinition.FromJson(json["atmosphere"]);
            body.Provenance = ProvenanceRecord.FromJson(json["provenance"]);

            return body;
        }
    }
}
