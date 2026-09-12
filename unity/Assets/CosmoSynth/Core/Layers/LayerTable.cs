using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>Which panel group a layer's row sits in. CONTRACT §4.2.</summary>
    public enum LayerGroup { SolarSystem, Stars, DustAndNebulae, Galaxies, Beyond, Guides }

    /// <summary>SOLO isolates one group; <c>None</c> means the layer is never hidden by a SOLO.</summary>
    public enum SoloGroup { None, Solar, Stars, Dust, Galaxies, Beyond }

    /// <summary>The category a layer's objects are labelled under. CONTRACT §5.5.1.</summary>
    public enum LabelCategory
    {
        Bodies, Spacecraft, Sites, SolarLandmarks, StarNames, Constellations,
        Clouds, MilkyWay, LocalGroup, Structures, Guides, StageFictional, StageExhibit
    }

    /// <summary>A layer's user-facing parameter. CONTRACT §5.5.2.</summary>
    public sealed record LayerParam(string Id, string Caption, double Min, double Max, double Default,
        string Unit, bool IsPresentationGain);

    /// <summary>
    /// One row of the layer list. CONTRACT §4.2.
    ///
    /// <see cref="MinLog10U"/>/<see cref="MaxLog10U"/> are the visibility window in
    /// <c>log10(units per km)</c>, with a 0.25-decade linear fade inside each end so a layer
    /// arrives and leaves as a fade rather than a pop.
    /// </summary>
    public sealed record LayerDef(
        string Id, string Caption, LayerGroup Group, SoloGroup Solo, bool DefaultOn,
        double MinLog10U, double MaxLog10U,
        LabelCategory? Labels,
        string[] DataKinds,
        string RequiresFeature,
        ProvenanceClass Weakest, string ProvenanceNote,
        LayerParam[] Params);

    /// <summary>
    /// STUB (integrator, CONTRACT P0.2/P0.7) — the real table has 46 rows plus 19 rungs, and is
    /// assembled from every lane's slot. This copy carries ONLY the <c>deep-sky</c> lane's rows,
    /// so the layers in this branch have windows, defaults, provenance and params to read.
    ///
    /// Replace wholesale at merge: the rows below are the lane's contribution to §5.4 and are
    /// meant to be pasted into the integrator's table, not to survive as a file.
    ///
    /// <c>Rung</c> and <c>LayerTable.Rungs</c> are deliberately absent: they need
    /// <c>PresentationPlane</c> (P0.3, not written), and no layer in this lane reads a rung.
    /// </summary>
    public static class LayerTable
    {
        private const double Floor = -24.70;   // ScaleLimits.MinUnitsPerKm
        private const double Ceil = 3.00;      // ScaleLimits.MaxUnitsPerKm

        private static readonly LayerParam[] None = new LayerParam[0];

        public static readonly LayerDef[] Layers =
        {
            // ---- STARS -----------------------------------------------------------------------
            new LayerDef("stars", "Stars (3D)", LayerGroup.Stars, SoloGroup.Stars, true,
                -19.6, Ceil, LabelCategory.StarNames, new[] { "stars" }, null,
                ProvenanceClass.Inferred, "position measured (AT-HYG parallax); luminosity and colour inferred",
                new[]
                {
                    new LayerParam("exposure", "Brightness", 0.1, 3.0, 1.0, "x", true),
                    new LayerParam("observed", "Observed sky", 0, 1, 0, "", false),
                    new LayerParam("limitMag", "Limit magnitude", 3.0, 14.0, 6.5, "mag", false),
                }),

            new LayerDef("constellations", "Constellation figures (3D)", LayerGroup.Stars, SoloGroup.Stars, false,
                -17.0, Ceil, LabelCategory.Constellations, new[] { "constellations", "stars" }, null,
                ProvenanceClass.Fictional, "endpoints measured; the figure is a cultural convention",
                new[]
                {
                    new LayerParam("boundaries", "Boundaries", 0, 1, 0, "", false),
                }),

            // ---- DUST ------------------------------------------------------------------------
            new LayerDef("dust", "Milky Way dust (3D)", LayerGroup.DustAndNebulae, SoloGroup.Dust, true,
                -19.0, -14.6, LabelCategory.Clouds, new[] { "dust" }, null,
                ProvenanceClass.Procedural, "position measured; density inferred (Vergely+2022); colour ramp procedural",
                new[]
                {
                    new LayerParam("density", "Dust", 0.1, 6.0, 1.0, "x", true),
                    new LayerParam("threshold", "Threshold", 0.0, 0.7, 0.22, "", false),
                }),

            // ---- GALAXIES: the Milky Way itself -----------------------------------------------
            new LayerDef("cepheids", "Classical Cepheids", LayerGroup.Stars, SoloGroup.Stars, true,
                -20.2, -15.5, null, new[] { "cepheids" }, null,
                ProvenanceClass.Inferred, "period-luminosity distances, distErr kept as the uncertainty", None),

            new LayerDef("globulars", "Globular clusters", LayerGroup.Stars, SoloGroup.Stars, true,
                -20.8, -16.0, LabelCategory.MilkyWay, new[] { "globulars" }, null,
                ProvenanceClass.Inferred, "LVDB positions; distance inferred", None),

            new LayerDef("masers", "Maser parallaxes", LayerGroup.Galaxies, SoloGroup.Galaxies, true,
                -19.6, -16.6, LabelCategory.MilkyWay, new[] { "masers" }, null,
                ProvenanceClass.Measured, "VLBI parallaxes (Reid+2019)", None),

            new LayerDef("arms", "Spiral arms", LayerGroup.Galaxies, SoloGroup.Galaxies, true,
                -19.6, -16.6, LabelCategory.MilkyWay, new string[0], null,
                ProvenanceClass.Inferred, "log-periodic fit to maser parallaxes (Reid+2019)", None),

            new LayerDef("sunOrbit", "Sun's galactic orbit", LayerGroup.Galaxies, SoloGroup.Galaxies, true,
                -19.6, -17.2, LabelCategory.Guides, new string[0], null,
                ProvenanceClass.Measured, "R0 = 8.15 kpc (Reid+2019)", None),

            new LayerDef("mwDisc", "Milky Way disc (impression)", LayerGroup.Galaxies, SoloGroup.Galaxies, true,
                -21.8, -17.4, LabelCategory.MilkyWay, new string[0], null,
                ProvenanceClass.Procedural, "an impression constrained by the inferred arm fit", None),

            // ---- GALAXIES: everything else ----------------------------------------------------
            new LayerDef("galaxiesLocal", "Local Group and Local Volume galaxies", LayerGroup.Galaxies,
                SoloGroup.Galaxies, true, -22.3, -17.8, LabelCategory.LocalGroup,
                new[] { "localvolume" }, null,
                ProvenanceClass.Procedural, "distances measured or inferred; unknown ellipticity drawn round and faint",
                new[] { new LayerParam("glow", "Galaxy glow", 0.5, 30.0, 1.0, "x", true) }),

            new LayerDef("galaxies2mrs", "2MRS galaxies", LayerGroup.Beyond, SoloGroup.Beyond, true,
                -24.0, -20.4, LabelCategory.Structures, new[] { "2mrs" }, null,
                ProvenanceClass.Inferred, "direction measured; distance inferred from redshift at the stated H0",
                new[] { new LayerParam("glow", "Galaxy glow", 0.5, 30.0, 1.0, "x", true) }),

            new LayerDef("groupsCf4", "Cosmicflows-4 groups", LayerGroup.Beyond, SoloGroup.Beyond, false,
                -24.0, -20.4, LabelCategory.Structures, new[] { "cf4_groups" }, null,
                ProvenanceClass.Measured, "distance moduli, about 20 % scatter", None),

            new LayerDef("sdss", "SDSS cosmic web", LayerGroup.Beyond, SoloGroup.Beyond, true,
                -24.7, -21.6, LabelCategory.Structures, new[] { "sdss" }, null,
                ProvenanceClass.Inferred, "direction measured; distance inferred from redshift",
                new[] { new LayerParam("glow", "Galaxy glow", 0.5, 30.0, 1.0, "x", true) }),

            new LayerDef("basins", "Laniakea and neighbours", LayerGroup.Beyond, SoloGroup.Beyond, true,
                -23.4, -20.8, LabelCategory.Structures, new[] { "structures" }, null,
                ProvenanceClass.Inferred, "named structures at published positions; the watershed outline needs cf4_basins",
                None),

            new LayerDef("cmb", "Cosmic microwave background", LayerGroup.Beyond, SoloGroup.Beyond, true,
                -24.7, -21.6, LabelCategory.Structures, new[] { "cmb" }, null,
                ProvenanceClass.Inferred, "measured over 97.9 % of the sky, inferred over the inpainted 2.1 %; false colour",
                new[]
                {
                    new LayerParam("mode", "Mode", 0, 2, 0, "", false),
                    new LayerParam("contrast", "Contrast", 100, 500, 250, "uK", false),
                }),

            // ---- GUIDES ------------------------------------------------------------------------
            new LayerDef("cylGrids", "Cylindrical distance grids", LayerGroup.Guides, SoloGroup.None, false,
                -21.6, -15.0, LabelCategory.Guides, new string[0], null,
                ProvenanceClass.Procedural, "drawing aid: exact radii, no measurement claim", None),
        };

        public static LayerDef Find(string id)
        {
            for (int i = 0; i < Layers.Length; i++) if (Layers[i].Id == id) return Layers[i];
            return null;
        }

        /// <summary>Every id in this table, for the tests and for the host that creates the layers.</summary>
        public static IEnumerable<string> Ids()
        {
            for (int i = 0; i < Layers.Length; i++) yield return Layers[i].Id;
        }
    }
}
