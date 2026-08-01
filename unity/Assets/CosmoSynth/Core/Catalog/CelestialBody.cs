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

        /// <summary>
        /// A sampled trajectory about <see cref="ParentId"/>, for motion no conic describes.
        ///
        /// Takes precedence over <see cref="Orbit"/> when both are present. Spacecraft use this;
        /// see <see cref="TrajectoryTable"/> for why elements are the wrong tool for them.
        /// </summary>
        public TrajectoryTable Trajectory;

        /// <summary>
        /// A fixed place on the parent's rotating surface, for anything that has landed.
        /// Mutually exclusive with <see cref="Orbit"/> and <see cref="Trajectory"/>.
        /// </summary>
        public SurfaceSite? Site;

        /// <summary>
        /// First and last instants this body's position is claimed to be known, or null for
        /// "always" — which is the right answer for a planet and the wrong one for a spacecraft.
        ///
        /// Elements and surface sites are closed forms: they will happily evaluate a century
        /// after the spacecraft burned up, drawing a Cassini that is still orbiting Saturn. The
        /// window is how a catalogue says when its own data stopped meaning anything. See
        /// <see cref="KnownAt"/>.
        /// </summary>
        public CosmicTime? KnownFrom;

        public CosmicTime? KnownUntil;

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

        /// <summary>A shipped mesh to draw instead of a sphere, for spacecraft.</summary>
        public ModelReference? Model;

        /// <summary>One line of description for the selection panel, where the source supplies one.</summary>
        public string Summary;

        /// <summary>
        /// When and how the mission ended, for something no longer operating. Empty for
        /// anything still working, so the UI can distinguish "silent since 2003" from "live".
        /// </summary>
        public string Epitaph;

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

        /// <summary>
        /// Whether this body's position is actually known at an instant.
        ///
        /// A sampled trajectory answers this from its own ends: beyond them the interpolator
        /// clamps, which would leave a spacecraft parked at its final position looking exactly
        /// like one still flying. Elements and surface sites have no ends of their own, so they
        /// rely on <see cref="KnownFrom"/> and <see cref="KnownUntil"/> — without which Cassini
        /// would still be circling Saturn nine years after it entered the atmosphere.
        ///
        /// Callers that draw things are expected to check this and draw nothing. Absence of
        /// data is a fact about the data, and showing a plausible position instead is the one
        /// outcome this catalogue exists to prevent.
        /// </summary>
        public bool KnownAt(CosmicTime time)
        {
            if (Trajectory != null && !Trajectory.Covers(time)) return false;
            if (KnownFrom.HasValue && time.SecondsPastJ2000 < KnownFrom.Value.SecondsPastJ2000) return false;
            if (KnownUntil.HasValue && time.SecondsPastJ2000 > KnownUntil.Value.SecondsPastJ2000) return false;
            return true;
        }

        /// <summary>How a body's position is arrived at. See <see cref="Placement"/>.</summary>
        public enum PlacementKind
        {
            /// <summary>Nothing says where it is; it sits at its parent's centre.</summary>
            None = 0,
            /// <summary>A fixed offset in its parent's frame — an exoplanet host star.</summary>
            Fixed,
            /// <summary>A two-body conic osculating at one epoch.</summary>
            Elements,
            /// <summary>Measured state vectors, interpolated between samples.</summary>
            Sampled,
            /// <summary>A latitude and longitude carried by the host's rotation.</summary>
            Surface,
        }

        /// <summary>
        /// Which mechanism actually places this body, in the order the frame builder picks.
        ///
        /// Derived from the fields rather than stored beside them, so it cannot disagree with
        /// them — the same reason the input legend is generated from the binding table. Anything
        /// presenting a body has to answer this question, and three callers each re-deriving it
        /// from four nullable fields is three chances to answer it differently from
        /// <see cref="CelestialCatalog.BuildFrameTree"/>, which is the only answer that matters.
        /// </summary>
        public PlacementKind Placement
        {
            get
            {
                if (Trajectory != null) return PlacementKind.Sampled;
                if (Site.HasValue) return PlacementKind.Surface;
                if (Orbit.HasValue) return PlacementKind.Elements;
                if (PositionKm.HasValue) return PlacementKind.Fixed;
                return PlacementKind.None;
            }
        }

        /// <summary>
        /// One line saying over what span this body's position is claimed, for display.
        ///
        /// Lives here rather than in the interface because it is a statement about the data, and
        /// because the window is the whole reason a spacecraft is ever hidden: a user who sees
        /// nothing where a spacecraft should be has no other way to find out whether it is
        /// missing or simply not known at this instant. Returns null for a body with no window,
        /// which is every planet, so a caller can leave the row out entirely.
        /// </summary>
        public string DescribeCoverage()
        {
            if (!KnownFrom.HasValue && !KnownUntil.HasValue) return null;
            if (!KnownUntil.HasValue) return $"position known from {KnownFrom.Value.ToIso8601()}";
            if (!KnownFrom.HasValue) return $"position known until {KnownUntil.Value.ToIso8601()}";
            return $"position known {KnownFrom.Value.ToIso8601()} to {KnownUntil.Value.ToIso8601()}";
        }

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

            if (Trajectory != null && string.IsNullOrEmpty(ParentId))
                problems.Add("A body with a sampled trajectory must name the parent it is measured against.");

            // ---- Coverage window -------------------------------------------
            //
            // The window is what stops a dead spacecraft being drawn, so it is worth as much
            // as its own consistency. All three of these shipped wrong at least once.

            if (KnownFrom.HasValue && KnownUntil.HasValue &&
                KnownUntil.Value.SecondsPastJ2000 <= KnownFrom.Value.SecondsPastJ2000)
            {
                problems.Add(
                    $"Coverage window ends at {KnownUntil.Value.SecondsPastJ2000:G9} s, which is not " +
                    $"after it starts at {KnownFrom.Value.SecondsPastJ2000:G9} s. Nothing is ever drawn.");
            }

            // A window wider than the table behind it is a claim with no data under it. It does
            // no visible harm — KnownAt takes the intersection — which is exactly why it went
            // unnoticed on all eight sampled missions, running past the samples by between one
            // and 148 days. A catalogue that overstates its own extent will eventually be
            // believed by something that does not cross-check.
            if (Trajectory != null)
            {
                if (KnownFrom.HasValue && !Trajectory.Covers(KnownFrom.Value))
                {
                    problems.Add(
                        $"KnownFrom {KnownFrom.Value.SecondsPastJ2000:G9} s is outside the sampled " +
                        $"trajectory, which runs {Trajectory.Start.SecondsPastJ2000:G9} to " +
                        $"{Trajectory.End.SecondsPastJ2000:G9} s.");
                }
                if (KnownUntil.HasValue && !Trajectory.Covers(KnownUntil.Value))
                {
                    problems.Add(
                        $"KnownUntil {KnownUntil.Value.SecondsPastJ2000:G9} s is outside the sampled " +
                        $"trajectory, which runs {Trajectory.Start.SecondsPastJ2000:G9} to " +
                        $"{Trajectory.End.SecondsPastJ2000:G9} s.");
                }
            }

            // A spacecraft with nothing saying where it is is a name and a mesh. It would build
            // a frame anyway — FixedEphemeris at the parent's centre — and be drawn inside the
            // planet it was supposed to be orbiting.
            if (Type == BodyType.Spacecraft &&
                !Orbit.HasValue && Trajectory == null && !Site.HasValue && !PositionKm.HasValue)
            {
                problems.Add(
                    "Spacecraft has no orbit, trajectory, site or fixed position, so nothing says " +
                    "where it is; it would be placed at its parent's centre.");
            }

            if (Site.HasValue)
            {
                if (string.IsNullOrEmpty(ParentId))
                    problems.Add("A body on a surface must name the body it is standing on.");
                if (System.Math.Abs(Site.Value.LatitudeDeg) > 90.0)
                    problems.Add($"Site latitude {Site.Value.LatitudeDeg} is outside [-90, 90].");

                // Two placements would silently let one win. Which one is not something the
                // reader should have to know.
                if (Orbit.HasValue || Trajectory != null)
                    problems.Add("A body cannot both be landed and be in flight; it has a site and an orbit.");
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

            if (KnownFrom.HasValue) json["knownFromSecondsPastJ2000"] = JsonValue.Number(KnownFrom.Value.SecondsPastJ2000);
            if (KnownUntil.HasValue) json["knownUntilSecondsPastJ2000"] = JsonValue.Number(KnownUntil.Value.SecondsPastJ2000);
            if (Trajectory != null) json["trajectory"] = Trajectory.ToJson();
            if (Site.HasValue) json["site"] = Site.Value.ToJson();
            if (Model.HasValue) json["model"] = Model.Value.ToJson();
            if (!string.IsNullOrEmpty(Summary)) json["summary"] = JsonValue.String(Summary);
            if (!string.IsNullOrEmpty(Epitaph)) json["epitaph"] = JsonValue.String(Epitaph);

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
                ProceduralSeed = json["proceduralSeed"].AsLong(),
                Summary = json["summary"].AsString(),
                Epitaph = json["epitaph"].AsString()
            };

            if (json.ContainsKey("knownFromSecondsPastJ2000"))
                body.KnownFrom = new CosmicTime(json["knownFromSecondsPastJ2000"].AsDouble());
            if (json.ContainsKey("knownUntilSecondsPastJ2000"))
                body.KnownUntil = new CosmicTime(json["knownUntilSecondsPastJ2000"].AsDouble());
            if (json.ContainsKey("trajectory")) body.Trajectory = TrajectoryTable.FromJson(json["trajectory"]);
            if (json.ContainsKey("site")) body.Site = SurfaceSite.FromJson(json["site"]);
            if (json.ContainsKey("model")) body.Model = ModelReference.FromJson(json["model"]);

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
