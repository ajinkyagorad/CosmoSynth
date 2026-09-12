namespace CosmoSynth.Core
{
    /// <summary>The six sections the LAYERS tab prints, in order.</summary>
    public enum LayerGroup { SolarSystem, Stars, DustAndNebulae, Galaxies, Beyond, Guides }

    /// <summary>SOLO isolates one of these. <c>None</c> means a SOLO never hides it.</summary>
    public enum SoloGroup { None, Solar, Stars, Dust, Galaxies, Beyond }

    /// <summary>What a label is, so the viewer can switch off a kind rather than all of them.</summary>
    public enum LabelCategory
    {
        Bodies, Spacecraft, Sites, SolarLandmarks, StarNames, Constellations,
        Clouds, MilkyWay, LocalGroup, Structures, Guides, StageFictional, StageExhibit
    }

    /// <summary>One row of the layer list (§5.4).</summary>
    public sealed record LayerDef(
        string Id, string Caption, LayerGroup Group, SoloGroup Solo, bool DefaultOn,
        double MinLog10U, double MaxLog10U,
        LabelCategory? Labels,
        string[] DataKinds,
        string RequiresFeature,
        ProvenanceClass Weakest, string ProvenanceNote,
        LayerParam[] Params);

    /// <summary>A slider or choice under a layer. A gain away from its default raises a chip.</summary>
    public sealed record LayerParam(string Id, string Caption, double Min, double Max, double Default,
        string Unit, bool IsPresentationGain);

    /// <summary>One rung of the scale ladder (§5.1).</summary>
    public sealed record Rung(int Index, string Id, string Caption, string Group,
        string CentreKind, string CentreId,
        double SpanMetres, double UnitsPerKm, PresentationPlane Plane, bool FollowOnArrival)
    {
        /// <summary>The real span this rung frames, in km, recovered from the room size and scale.</summary>
        public double SpanKm => UnitsPerKm > 0.0 ? SpanMetres / UnitsPerKm : 0.0;

        public double Log10U => MathD.SafeLog10(UnitsPerKm);
    }

    /// <summary>
    /// The only list of layers and rungs.
    ///
    /// The nineteen <see cref="Rungs"/> are this lane's (`nav-ui`) deliverable and are authoritative:
    /// every scale control, the GO tab, the readout and the watch arc read them and nothing else.
    /// Each <c>UnitsPerKm</c> is §5.1's value and <c>RungTests</c> recomputes it from the documented
    /// span and room size, so a transcription slip fails rather than ships.
    ///
    /// The forty-six <see cref="Layers"/> rows are a STUB SEED. §9.5 gives each row to the lane
    /// that owns its <i>renderer</i>, through a slot in this file; none of those lanes had landed
    /// when this was written, and a LAYERS tab generated from an empty table proves nothing. The
    /// rows below are transcribed from §5.4 verbatim. At merge the integrator keeps whichever rows
    /// arrive in the lanes' own slots and deletes the duplicates here.
    /// </summary>
    public static class LayerTable
    {
        private static readonly LayerParam[] None = new LayerParam[0];
        private static readonly string[] NoKinds = new string[0];

        private static string[] K(params string[] kinds) => kinds;

        private static LayerParam Gain(string id, string caption, double min, double max, double def) =>
            new LayerParam(id, caption, min, max, def, "x", true);

        // ---- the nineteen rungs ---------------------------------------------
        //
        // U is room metres per kilometre. Rungs 2-4 are relative: the value shown is for Earth and
        // the travel recomputes U = room / span(focus) for any other focus.

        public static readonly Rung[] Rungs =
        {
            new Rung(0,  "sky",             "Sky from Earth",                "EARTH",         "site",        null,      1.0,  1e3,       PresentationPlane.Horizon,       true),
            new Rung(1,  "leo",             "Low Earth orbit",               "EARTH",         "orbit",       "earth",   1.0,  1e3,       PresentationPlane.LocalVertical, true),
            new Rung(2,  "planet-room",     "Planet, room-size",             "EARTH",         "focusPlanet", null,      2.5,  1.962e-4,  PresentationPlane.FocusEquator,  true),
            new Rung(3,  "planet-hand",     "Planet in hand",                "EARTH",         "focusPlanet", null,      0.30, 2.354e-5,  PresentationPlane.FocusEquator,  true),
            new Rung(4,  "planet-moons",    "Planet and moons",              "EARTH",         "focusMoons",  null,      0.55, 7.154e-7,  PresentationPlane.FocusEquator,  true),
            new Rung(5,  "inner",           "Inner planets",                 "SOLAR SYSTEM",  "hostStar",    null,      0.6,  1.253e-9,  PresentationPlane.Ecliptic,      true),
            new Rung(6,  "system",          "Whole Solar System",            "SOLAR SYSTEM",  "hostStar",    null,      0.7,  7.547e-11, PresentationPlane.Ecliptic,      true),
            new Rung(7,  "heliosphere",     "Heliosphere",                   "SOLAR SYSTEM",  "body",        "sun",     0.7,  7.799e-12, PresentationPlane.Ecliptic,      true),
            new Rung(8,  "oort",            "Oort cloud",                    "SOLAR SYSTEM",  "body",        "sun",     0.7,  1.850e-14, PresentationPlane.Ecliptic,      true),
            new Rung(9,  "nearest",         "Nearest stars",                 "STARS",         "body",        "sun",     0.7,  3.083e-15, PresentationPlane.Ecliptic,      true),
            new Rung(10, "neighbourhood",   "Solar neighbourhood",           "STARS",         "body",        "sun",     0.7,  2.846e-16, PresentationPlane.Galactic,      true),
            new Rung(11, "clouds",          "Local clouds",                  "STARS",         "body",        "sun",     1.6,  2.593e-17, PresentationPlane.Galactic,      false),
            new Rung(12, "milkyway",        "Milky Way",                     "GALAXIES",      "galacticCentre", null,   0.7,  5.671e-19, PresentationPlane.Galactic,      false),
            new Rung(13, "halo",            "Halo and satellites",           "GALAXIES",      "galacticCentre", null,   0.7,  3.781e-20, PresentationPlane.Galactic,      false),
            new Rung(14, "localgroup",      "Local Group",                   "GALAXIES",      "milkyWayM31Midpoint", null, 0.9, 9.722e-21, PresentationPlane.Supergalactic, false),
            new Rung(15, "localvolume",     "Local Volume",                  "GALAXIES",      "milkyWay",    null,      0.9,  1.458e-21, PresentationPlane.Supergalactic, false),
            new Rung(16, "laniakea",        "Laniakea",                      "BEYOND",        "milkyWay",    null,      1.0,  1.296e-22, PresentationPlane.Supergalactic, false),
            new Rung(17, "cosmicweb",       "Cosmic web",                    "BEYOND",        "milkyWay",    null,      1.2,  1.944e-23, PresentationPlane.Supergalactic, false),
            new Rung(18, "cmb",             "Observable universe and CMB",   "BEYOND",        "milkyWay",    null,      1.2,  1.364e-24, PresentationPlane.Supergalactic, false)
        };

        /// <summary>The five captioned groups the GO tab prints the ladder in, in order.</summary>
        public static readonly string[] RungGroups = { "EARTH", "SOLAR SYSTEM", "STARS", "GALAXIES", "BEYOND" };

        // ---- the forty-six layers (STUB SEED, see the class remarks) --------

        public static readonly LayerDef[] Layers =
        {
            // SOLAR SYSTEM - SOLO: Solar
            new LayerDef("bodies", "Sun, planets, moons, small bodies", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -13.5, 3.0, LabelCategory.Bodies, K("solar_system"), null,
                ProvenanceClass.Inferred, "measured radii and elements; position propagated away from the Horizons fit", None),
            new LayerDef("orbits", "Orbits", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -13.0, 3.0, null, NoKinds, null,
                ProvenanceClass.Inferred, "measured elements, inferred drift", None),
            new LayerDef("spacecraft", "Spacecraft", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -12.6, 3.0, LabelCategory.Spacecraft, K("spacecraft", "models"), null,
                ProvenanceClass.Measured, "JPL trajectories inside each craft's known window", None),
            new LayerDef("trails", "Mission trails", LayerGroup.SolarSystem, SoloGroup.Solar, false,
                -12.6, 3.0, LabelCategory.Spacecraft, K("spacecraft"), null,
                ProvenanceClass.Fictional, "measured, except the illustrative Apollo 11 path", None),
            new LayerDef("sites", "Surface sites", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -5.5, 3.0, LabelCategory.Sites, K("solar_system"), null,
                ProvenanceClass.Measured, "published landing coordinates", None),
            new LayerDef("atmospheres", "Atmospheres", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -13.5, 3.0, null, NoKinds, null,
                ProvenanceClass.Inferred, "inferred colour over measured scale heights", None),
            new LayerDef("rings", "Planetary rings", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -13.5, 3.0, null, NoKinds, null,
                ProvenanceClass.Procedural, "measured radii, procedural texture", None),
            new LayerDef("clouds", "Earth clouds", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -13.5, 3.0, null, K("surfaces"), null,
                ProvenanceClass.Measured, "NASA Visible Earth, one static day", None),
            new LayerDef("winds", "Zonal winds", LayerGroup.SolarSystem, SoloGroup.Solar, false,
                -13.5, 3.0, null, NoKinds, "winds",
                ProvenanceClass.Procedural, "procedural over inferred profiles", None),
            new LayerDef("belts", "Asteroid and Kuiper belts", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -13.2, -4.7, LabelCategory.SolarLandmarks, K("smallbodies"), null,
                ProvenanceClass.Inferred, "two-body propagation from the SBDB epoch", None),
            new LayerDef("comets", "Comet tails", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -13.5, 3.0, null, NoKinds, null,
                ProvenanceClass.Procedural, "procedural tail over a measured orbit", None),
            new LayerDef("heliosphere", "Heliosphere", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -12.6, -9.6, LabelCategory.SolarLandmarks, NoKinds, null,
                ProvenanceClass.Inferred, "measured crossing points, inferred shape", None),
            new LayerDef("parker", "Parker spiral", LayerGroup.SolarSystem, SoloGroup.Solar, false,
                -11.2, -7.5, null, K("snapshot"), null,
                ProvenanceClass.Procedural, "procedural geometry over a measured L1 wind speed", None),
            new LayerDef("oort", "Oort cloud", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -14.8, -11.6, LabelCategory.SolarLandmarks, NoKinds, null,
                ProvenanceClass.Inferred, "never observed; a statistical shell", None),
            new LayerDef("sunActivity", "Sun activity", LayerGroup.SolarSystem, SoloGroup.Solar, true,
                -13.5, 3.0, null, K("activity"), null,
                ProvenanceClass.Procedural, "procedural spots and corona over the measured monthly sunspot number", None),

            // STARS - SOLO: Stars
            new LayerDef("stars", "Stars (3D)", LayerGroup.Stars, SoloGroup.Stars, true,
                -19.6, 3.0, LabelCategory.StarNames, K("stars"), null,
                ProvenanceClass.Inferred, "measured positions, inferred luminosity and colour",
                new[] { Gain("exposure", "Brightness", 0.1, 3.0, 1.0) }),
            new LayerDef("census", "Nearest-star census", LayerGroup.Stars, SoloGroup.Stars, true,
                -16.0, -12.0, LabelCategory.StarNames, K("census"), "census",
                ProvenanceClass.Measured, "Reyle 2021 10 pc census", None),
            new LayerDef("exohosts", "Exoplanet systems", LayerGroup.Stars, SoloGroup.Stars, true,
                -17.0, 3.0, LabelCategory.StarNames, K("exoplanets"), null,
                ProvenanceClass.Procedural, "published a and period; e and i assumed on most rows; procedural orientation", None),
            new LayerDef("worlds", "Generated worlds", LayerGroup.Stars, SoloGroup.Stars, true,
                -19.6, 3.0, LabelCategory.Bodies, NoKinds, null,
                ProvenanceClass.Procedural, "generated from the star's own measured position; seed shown", None),
            new LayerDef("flare", "Flare stars (illustrative)", LayerGroup.Stars, SoloGroup.Stars, false,
                -19.6, 3.0, null, K("stars_named"), "flare",
                ProvenanceClass.Procedural, "procedural timing; class inferred", None),
            new LayerDef("constellations", "Constellation figures (3D)", LayerGroup.Stars, SoloGroup.Stars, false,
                -17.0, 3.0, LabelCategory.Constellations, K("constellations"), null,
                ProvenanceClass.Fictional, "measured endpoints; the figures are a cultural convention", None),
            new LayerDef("cepheids", "Classical Cepheids", LayerGroup.Stars, SoloGroup.Stars, true,
                -20.2, -15.5, null, K("cepheids"), null,
                ProvenanceClass.Inferred, "period-luminosity distances with published scatter", None),
            new LayerDef("globulars", "Globular clusters", LayerGroup.Stars, SoloGroup.Stars, true,
                -20.8, -16.0, LabelCategory.MilkyWay, K("globulars"), null,
                ProvenanceClass.Inferred, "LVDB distances, measured where published", None),

            // DUST AND NEBULAE - SOLO: Dust
            new LayerDef("dust", "Milky Way dust (3D)", LayerGroup.DustAndNebulae, SoloGroup.Dust, true,
                -19.0, -14.6, LabelCategory.Clouds, K("dust"), null,
                ProvenanceClass.Procedural, "measured positions, inferred density (Vergely 2022), procedural colour ramp",
                new[] { Gain("density", "Dust density", 0.1, 6.0, 1.0),
                        new LayerParam("threshold", "Threshold", 0.0, 1.0, 0.05, "", false) }),
            new LayerDef("dustExtent", "Dust map extent", LayerGroup.DustAndNebulae, SoloGroup.Dust, false,
                -19.0, -14.6, null, K("dust"), null,
                ProvenanceClass.Measured, "the survey geometry itself", None),
            new LayerDef("nebulae", "Nebula close-ups", LayerGroup.DustAndNebulae, SoloGroup.Dust, true,
                -17.5, -13.5, LabelCategory.Clouds, K("landmarks"), null,
                ProvenanceClass.Procedural, "procedural shape and colour over measured positions",
                new[] { Gain("density", "Nebula density", 0.1, 4.0, 1.0),
                        new LayerParam("gas", "Gas", 0.0, 2.0, 1.0, "", false),
                        new LayerParam("dust", "Dust", 0.0, 2.0, 1.0, "", false) }),
            new LayerDef("cloudMarkers", "Molecular clouds", LayerGroup.DustAndNebulae, SoloGroup.Dust, true,
                -18.2, -14.5, LabelCategory.Clouds, K("landmarks"), null,
                ProvenanceClass.Measured, "published (l, b, d) with a citation each", None),

            // GALAXIES - SOLO: Galaxies
            new LayerDef("arms", "Spiral arms", LayerGroup.Galaxies, SoloGroup.Galaxies, true,
                -19.6, -16.6, LabelCategory.MilkyWay, NoKinds, null,
                ProvenanceClass.Inferred, "Reid 2019 log-periodic fit, drawn only over its fitted azimuth range", None),
            new LayerDef("mwDisc", "Milky Way disc (impression)", LayerGroup.Galaxies, SoloGroup.Galaxies, true,
                -21.8, -17.4, LabelCategory.MilkyWay, NoKinds, null,
                ProvenanceClass.Procedural, "an impression constrained by the inferred arms", None),
            new LayerDef("mwBand", "Milky Way band from inside", LayerGroup.Galaxies, SoloGroup.Galaxies, true,
                -16.6, 3.0, null, NoKinds, null,
                ProvenanceClass.Procedural, "procedural, oriented to the north galactic pole", None),
            new LayerDef("sunOrbit", "Sun's galactic orbit", LayerGroup.Galaxies, SoloGroup.Galaxies, true,
                -19.6, -17.2, LabelCategory.Guides, NoKinds, null,
                ProvenanceClass.Measured, "R0 = 8.15 kpc (Reid 2019)", None),
            new LayerDef("masers", "Maser parallaxes", LayerGroup.Galaxies, SoloGroup.Galaxies, true,
                -19.6, -16.6, LabelCategory.MilkyWay, K("masers"), null,
                ProvenanceClass.Measured, "VLBI parallaxes", None),
            new LayerDef("galaxiesLocal", "Local Group and Local Volume galaxies", LayerGroup.Galaxies, SoloGroup.Galaxies, true,
                -22.3, -17.8, LabelCategory.LocalGroup, K("localvolume"), null,
                ProvenanceClass.Procedural, "measured or inferred distances; procedural where morphology is unknown",
                new[] { Gain("glow", "Galaxy glow", 0.5, 30.0, 1.0) }),

            // BEYOND - SOLO: Beyond
            new LayerDef("galaxies2mrs", "2MRS galaxies", LayerGroup.Beyond, SoloGroup.Beyond, true,
                -24.0, -20.4, LabelCategory.Structures, K("2mrs"), null,
                ProvenanceClass.Inferred, "measured directions, redshift distances with the stated H0",
                new[] { Gain("glow", "Galaxy glow", 0.5, 30.0, 1.0) }),
            new LayerDef("groupsCf4", "Cosmicflows-4 groups", LayerGroup.Beyond, SoloGroup.Beyond, false,
                -24.0, -20.4, LabelCategory.Structures, K("cf4_groups"), null,
                ProvenanceClass.Inferred, "distance moduli with about 20 % scatter", None),
            new LayerDef("basins", "Laniakea and neighbours", LayerGroup.Beyond, SoloGroup.Beyond, true,
                -23.4, -20.8, LabelCategory.Structures, K("cf4_basins"), null,
                ProvenanceClass.Inferred, "CF4 velocity-field reconstruction", None),
            new LayerDef("density", "Density field", LayerGroup.Beyond, SoloGroup.Beyond, false,
                -23.4, -20.8, null, K("cf4_delta"), null,
                ProvenanceClass.Inferred, "CF4 reconstructed overdensity", None),
            new LayerDef("sdss", "SDSS cosmic web", LayerGroup.Beyond, SoloGroup.Beyond, true,
                -24.7, -21.6, LabelCategory.Structures, K("sdss"), null,
                ProvenanceClass.Inferred, "measured directions, redshift distances",
                new[] { Gain("glow", "Galaxy glow", 0.5, 30.0, 1.0) }),
            new LayerDef("cmb", "Cosmic microwave background", LayerGroup.Beyond, SoloGroup.Beyond, true,
                -24.7, -21.6, LabelCategory.Structures, K("cmb"), null,
                ProvenanceClass.Inferred, "measured over 97.9 % of the sky, inpainted over 2.1 %, false colour",
                new[] { new LayerParam("contrast", "Contrast", 0.2, 5.0, 1.0, "x", false),
                        new LayerParam("mode", "Form", 0.0, 1.0, 0.0, "", false),
                        new LayerParam("palette", "Palette", 0.0, 2.0, 0.0, "", false),
                        new LayerParam("frame", "Frame", 0.0, 1.0, 0.0, "", false),
                        new LayerParam("backdrop", "Backdrop in passthrough", 0.0, 1.0, 0.0, "", false) }),
            new LayerDef("quasars", "Quasars", LayerGroup.Beyond, SoloGroup.Beyond, false,
                -24.7, -22.0, null, K("quasars"), null,
                ProvenanceClass.Inferred, "not in the 2.0 plan: the worked 'not in this build' case", None),

            // GUIDES - a SOLO never hides these
            new LayerDef("scaleRings", "Scale and light-time rings", LayerGroup.Guides, SoloGroup.None, true,
                -24.7, 3.0, LabelCategory.Guides, NoKinds, null,
                ProvenanceClass.Measured, "definitions and catalogue distances", None),
            new LayerDef("horizonGrid", "Horizon compass", LayerGroup.Guides, SoloGroup.None, true,
                -24.7, 3.0, LabelCategory.Guides, NoKinds, null,
                ProvenanceClass.Measured, "N/E/S/W and 30 deg altitude ticks", None),
            new LayerDef("eclipticGrid", "Ecliptic grid", LayerGroup.Guides, SoloGroup.None, false,
                -12.0, -7.5, LabelCategory.Guides, NoKinds, null,
                ProvenanceClass.Measured, "AU circles in the ecliptic", None),
            new LayerDef("planeGrid", "Galactic plane grid", LayerGroup.Guides, SoloGroup.None, false,
                -19.6, -15.0, LabelCategory.Guides, NoKinds, null,
                ProvenanceClass.Measured, "parsec cells in the galactic plane", None),
            new LayerDef("cylGrids", "Cylindrical distance grids", LayerGroup.Guides, SoloGroup.None, false,
                -21.6, -15.0, LabelCategory.Guides, NoKinds, null,
                ProvenanceClass.Measured, "one ribbon mesh per regime", None),
            new LayerDef("sgGrid", "Supergalactic grid", LayerGroup.Guides, SoloGroup.None, false,
                -24.0, -20.0, LabelCategory.Guides, NoKinds, null,
                ProvenanceClass.Measured, "the SGX-SGY plane in 100 Mpc cells", None)
        };

        public static LayerDef Find(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < Layers.Length; i++)
                if (string.Equals(Layers[i].Id, id, System.StringComparison.Ordinal)) return Layers[i];
            return null;
        }

        public static Rung FindRung(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < Rungs.Length; i++)
                if (string.Equals(Rungs[i].Id, id, System.StringComparison.Ordinal)) return Rungs[i];
            return null;
        }

        /// <summary>
        /// The rung nearest a scale, in log10 U. The panel highlights it; it never snaps.
        /// </summary>
        public static Rung NearestRung(double log10U)
        {
            Rung best = Rungs[0];
            double bestError = double.PositiveInfinity;
            for (int i = 0; i < Rungs.Length; i++)
            {
                double error = System.Math.Abs(Rungs[i].Log10U - log10U);
                if (error >= bestError) continue;
                bestError = error;
                best = Rungs[i];
            }
            return best;
        }

        /// <summary>Within 0.05 decades the panel calls the rung exact (§5.1).</summary>
        public static bool IsAtRung(Rung rung, double log10U) =>
            rung != null && System.Math.Abs(rung.Log10U - log10U) <= 0.05;
    }
}
