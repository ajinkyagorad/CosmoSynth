using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>Why a layer row reads the way it does. CONTRACT §4.2.</summary>
    public enum LayerStatus
    {
        On, Off, OutOfWindow, Loading, Missing, Invalid, NotInThisBuild, FeatureMissing, ConditionUnmet
    }

    /// <summary>
    /// STUB (integrator, CONTRACT §4.2 / P0.2) — on/off, SOLO and parameter values, and the one
    /// pure question every layer asks: <see cref="Weight"/>.
    ///
    /// The point of putting the whole decision here is that no layer tests the scale, the SOLO
    /// or its own data availability for itself. A layer that is off, out of window or missing its
    /// file gets <c>Weight == 0</c> and therefore declares no group, draws nothing and is not
    /// pickable — which is also what stops the §2.6 push pass moving a real group away from
    /// geometry that is not being drawn.
    /// </summary>
    public sealed class LayerState
    {
        /// <summary>Width of the linear fade at each end of a window, in decades of U.</summary>
        public const double FadeDecades = 0.25;

        private readonly Dictionary<string, bool> _on = new Dictionary<string, bool>();
        private readonly Dictionary<string, double> _params = new Dictionary<string, double>();

        public SoloGroup Solo { get; set; } = SoloGroup.None;

        public bool IsOn(string id)
        {
            if (_on.TryGetValue(id, out bool on)) return on;
            LayerDef def = LayerTable.Find(id);
            return def != null && def.DefaultOn;
        }

        public void SetOn(string id, bool on) => _on[id] = on;

        public double Param(string layerId, string paramId)
        {
            if (_params.TryGetValue(Key(layerId, paramId), out double value)) return value;
            LayerParam p = FindParam(layerId, paramId);
            return p?.Default ?? 0.0;
        }

        public void SetParam(string layerId, string paramId, double value)
        {
            LayerParam p = FindParam(layerId, paramId);
            if (p != null) value = Math.Min(p.Max, Math.Max(p.Min, value));
            _params[Key(layerId, paramId)] = value;
        }

        /// <summary>True when a presentation gain is away from its default, which raises a chip.</summary>
        public bool IsGainActive(string layerId, string paramId)
        {
            LayerParam p = FindParam(layerId, paramId);
            if (p == null || !p.IsPresentationGain) return false;
            return Math.Abs(Param(layerId, paramId) - p.Default) > 1e-9;
        }

        /// <summary>
        /// <c>on * soloAllows * window * available</c>, all in [0, 1].
        ///
        /// The window term is the product of the two ends' 0.25-decade ramps, so a layer never
        /// pops into existence — which matters because several of these layers are full-screen.
        /// </summary>
        public double Weight(string id, double log10U, in LayerContext ctx)
        {
            LayerDef def = LayerTable.Find(id);
            if (def == null) return 0.0;
            if (!IsOn(id)) return 0.0;
            if (Solo != SoloGroup.None && def.Solo != SoloGroup.None && def.Solo != Solo) return 0.0;
            if (!Available(def, ctx)) return 0.0;
            return Window(def, log10U);
        }

        public static double Window(LayerDef def, double log10U)
        {
            double rise = Clamp01((log10U - def.MinLog10U) / FadeDecades);
            double fall = Clamp01((def.MaxLog10U - log10U) / FadeDecades);
            return rise * fall;
        }

        public LayerStatus Status(string id, double log10U, in LayerContext ctx)
        {
            LayerDef def = LayerTable.Find(id);
            if (def == null) return LayerStatus.Invalid;
            if (!Available(def, ctx)) return MissingKind(def, ctx) ? LayerStatus.NotInThisBuild : LayerStatus.FeatureMissing;
            if (!IsOn(id)) return LayerStatus.Off;
            if (Solo != SoloGroup.None && def.Solo != SoloGroup.None && def.Solo != Solo) return LayerStatus.Off;
            return Window(def, log10U) > 0.0 ? LayerStatus.On : LayerStatus.OutOfWindow;
        }

        private static bool Available(LayerDef def, in LayerContext ctx)
        {
            for (int i = 0; i < def.DataKinds.Length; i++)
                if (!ctx.Has(def.DataKinds[i])) return false;
            if (!string.IsNullOrEmpty(def.RequiresFeature) && !ctx.HasFeature(def.RequiresFeature)) return false;
            return true;
        }

        private static bool MissingKind(LayerDef def, in LayerContext ctx)
        {
            for (int i = 0; i < def.DataKinds.Length; i++)
                if (!ctx.Has(def.DataKinds[i])) return true;
            return false;
        }

        private static LayerParam FindParam(string layerId, string paramId)
        {
            LayerDef def = LayerTable.Find(layerId);
            if (def == null) return null;
            for (int i = 0; i < def.Params.Length; i++)
                if (def.Params[i].Id == paramId) return def.Params[i];
            return null;
        }

        private static string Key(string layerId, string paramId) => layerId + "." + paramId;

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
    }
}
