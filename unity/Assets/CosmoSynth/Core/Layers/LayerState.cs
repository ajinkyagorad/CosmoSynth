using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>Why a LAYERS row reads what it reads.</summary>
    public enum LayerStatusKind
    {
        On,
        Off,
        OutOfWindow,
        Loading,
        Missing,
        Invalid,
        NotInThisBuild,
        FeatureMissing,
        ConditionUnmet
    }

    /// <summary>A row's state and the reason, which the panel prints verbatim.</summary>
    public readonly struct LayerStatus
    {
        public readonly LayerStatusKind Kind;
        public readonly string Detail;

        public LayerStatus(LayerStatusKind kind, string detail = null)
        {
            Kind = kind;
            Detail = detail;
        }

        /// <summary>The suffix §5.6.6 puts after a layer's caption.</summary>
        public override string ToString() => Kind switch
        {
            LayerStatusKind.On => "on",
            LayerStatusKind.Off => "off",
            LayerStatusKind.OutOfWindow => "on (other scales)",
            LayerStatusKind.Loading => "loading",
            LayerStatusKind.Missing => "missing: " + (Detail ?? "?"),
            LayerStatusKind.Invalid => "invalid: " + (Detail ?? "?"),
            LayerStatusKind.NotInThisBuild => "not in this build (licence)",
            LayerStatusKind.FeatureMissing => "not in this build (" + (Detail ?? "?") + ")",
            LayerStatusKind.ConditionUnmet => Detail ?? "not here",
            _ => "?"
        };
    }

    /// <summary>
    /// On, off, SOLO and parameter values for every layer, and the one question a layer asks
    /// about itself: <see cref="Weight"/>.
    ///
    /// STUB, lane `nav-ui`: integrator-owned framework type (§4.2), written to the contract's
    /// signatures because the LAYERS tab, SOLO and the destination list are this lane's
    /// deliverables and all three are functions of it. Delete at merge.
    ///
    /// The point of the design is that a layer never tests scale, SOLO or data availability for
    /// itself. Four layers doing that four ways is how <c>dust</c> and <c>nebulae</c> came to
    /// overlap by 2.9 decades with nothing arbitrating.
    /// </summary>
    public sealed class LayerState
    {
        private readonly HashSet<string> _off = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, double> _params =
            new Dictionary<string, double>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, LayerStatus> _dataProblems =
            new Dictionary<string, LayerStatus>(System.StringComparer.Ordinal);

        /// <summary>The 0.25-decade ramp at each end of a window, so a layer fades rather than pops.</summary>
        public const double FadeDecades = 0.25;

        public SoloGroup Solo { get; set; } = SoloGroup.None;

        public bool IsOn(string id) => !_off.Contains(id);

        public void SetOn(string id, bool on)
        {
            if (on) _off.Remove(id);
            else _off.Add(id);
        }

        /// <summary>Reset to the table's defaults. Called once at startup and by the panel's Reset.</summary>
        public void ApplyDefaults()
        {
            _off.Clear();
            for (int i = 0; i < LayerTable.Layers.Length; i++)
            {
                LayerDef def = LayerTable.Layers[i];
                if (!def.DefaultOn) _off.Add(def.Id);
            }
        }

        public double Param(string layerId, string paramId)
        {
            if (_params.TryGetValue(layerId + "." + paramId, out double value)) return value;
            LayerDef def = LayerTable.Find(layerId);
            if (def?.Params == null) return 0.0;
            for (int i = 0; i < def.Params.Length; i++)
                if (def.Params[i].Id == paramId) return def.Params[i].Default;
            return 0.0;
        }

        public void SetParam(string layerId, string paramId, double value)
        {
            LayerDef def = LayerTable.Find(layerId);
            if (def?.Params != null)
            {
                for (int i = 0; i < def.Params.Length; i++)
                {
                    if (def.Params[i].Id != paramId) continue;
                    value = MathD.Clamp(value, def.Params[i].Min, def.Params[i].Max);
                    break;
                }
            }
            _params[layerId + "." + paramId] = value;
        }

        /// <summary>True when any param of this layer is away from its default by more than 5 %.</summary>
        public bool ParamIsRaised(string layerId, string paramId)
        {
            LayerDef def = LayerTable.Find(layerId);
            if (def?.Params == null) return false;
            for (int i = 0; i < def.Params.Length; i++)
            {
                if (def.Params[i].Id != paramId) continue;
                double def0 = def.Params[i].Default;
                return System.Math.Abs(Param(layerId, paramId) - def0) > 0.05 * System.Math.Abs(def0 == 0.0 ? 1.0 : def0);
            }
            return false;
        }

        /// <summary>
        /// Record that a kind failed to load, so the row can say which file and why rather than
        /// silently reading "off".
        /// </summary>
        public void SetDataProblem(string kind, LayerStatus status) => _dataProblems[kind] = status;

        public void ClearDataProblem(string kind) => _dataProblems.Remove(kind);

        // ---- the one question ------------------------------------------------

        /// <summary>
        /// <c>on * soloAllows * window * available</c>, in [0, 1]. Zero means not drawn and not
        /// pickable; anything else is the layer's fade and goes straight into <c>_Fade</c>.
        /// </summary>
        public double Weight(string id, double log10U, in LayerContext ctx)
        {
            LayerDef def = LayerTable.Find(id);
            if (def == null) return 0.0;
            if (!IsOn(id)) return 0.0;
            if (!SoloAllows(def)) return 0.0;
            if (!Available(def, ctx)) return 0.0;
            if (!ConditionMet(def, ctx)) return 0.0;
            return Window(def, log10U);
        }

        /// <summary>
        /// The window ramp alone, for the "This rung" filter and for tests.
        ///
        /// <b>A window end that coincides with the ladder's own limit does not fade.</b> Seventeen
        /// layers — the bodies, the orbits, the stars, the sky guides — end at log10 U = +3, and
        /// +3 is <see cref="ScaleLimits.MaxUnitsPerKm"/> itself: with a fade there, the whole Solar
        /// System is invisible at true scale, which is where rungs `sky` and `leo` put the viewer.
        /// There is nothing beyond a limit to fade into.
        /// </summary>
        public static double Window(LayerDef def, double log10U)
        {
            double top = MathD.SafeLog10(ScaleLimits.MaxUnitsPerKm);
            double bottom = MathD.SafeLog10(ScaleLimits.MinUnitsPerKm);

            double rising = def.MinLog10U <= bottom + 1e-9
                ? 1.0
                : MathD.Clamp01((log10U - def.MinLog10U) / FadeDecades);
            double falling = def.MaxLog10U >= top - 1e-9
                ? 1.0
                : MathD.Clamp01((def.MaxLog10U - log10U) / FadeDecades);

            return rising * falling;
        }

        /// <summary>A SOLO never hides guides or labels: their group is <c>None</c>.</summary>
        public bool SoloAllows(LayerDef def) =>
            Solo == SoloGroup.None || def.Solo == SoloGroup.None || def.Solo == Solo;

        private bool Available(LayerDef def, in LayerContext ctx)
        {
            for (int i = 0; i < def.DataKinds.Length; i++)
                if (!ctx.Has(def.DataKinds[i])) return false;
            if (!string.IsNullOrEmpty(def.RequiresFeature) && !ctx.HasFeature(def.RequiresFeature)) return false;
            return true;
        }

        /// <summary>
        /// The §5.4 "extra condition" column, for the conditions that are functions of
        /// <see cref="LayerContext"/>. Anything else is internal to the layer's own renderer.
        /// </summary>
        private static bool ConditionMet(LayerDef def, in LayerContext ctx) => def.Id switch
        {
            "clouds" => ctx.EarthPx >= 60f,
            "winds" => ctx.FocusPx >= 60f,
            "sunActivity" => ctx.SunPx >= 60f,
            "heliosphere" => !ctx.ViewerInsideHeliosphere && ctx.HeliosphereDeg < 90f,
            "worlds" => ctx.NearestStarPc < 1.0,
            "nebulae" => ctx.NearestNebulaBoxRadii < 3.0,
            "mwBand" => ctx.RoomVisibility <= 0.35f,
            "horizonGrid" => ctx.ViewerOnSite,
            _ => true
        };

        /// <summary>The row text's reason, which is the same decision taken one branch at a time.</summary>
        public LayerStatus Status(string id, double log10U, in LayerContext ctx)
        {
            LayerDef def = LayerTable.Find(id);
            if (def == null) return new LayerStatus(LayerStatusKind.Invalid, "no such layer");

            for (int i = 0; i < def.DataKinds.Length; i++)
            {
                if (_dataProblems.TryGetValue(def.DataKinds[i], out LayerStatus problem)) return problem;
                if (!ctx.Has(def.DataKinds[i]))
                    return new LayerStatus(LayerStatusKind.NotInThisBuild, def.DataKinds[i]);
            }

            if (!string.IsNullOrEmpty(def.RequiresFeature) && !ctx.HasFeature(def.RequiresFeature))
                return new LayerStatus(LayerStatusKind.FeatureMissing, def.RequiresFeature);

            if (!IsOn(id)) return new LayerStatus(LayerStatusKind.Off);
            if (!SoloAllows(def)) return new LayerStatus(LayerStatusKind.ConditionUnmet, "hidden by SOLO");
            if (Window(def, log10U) <= 0.0) return new LayerStatus(LayerStatusKind.OutOfWindow);
            if (!ConditionMet(def, ctx)) return new LayerStatus(LayerStatusKind.ConditionUnmet, "not at this view");
            return new LayerStatus(LayerStatusKind.On);
        }
    }
}
