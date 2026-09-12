using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>How a layer's fragments reach the framebuffer, and its promise about alpha.</summary>
    public enum LayerBlend : byte { Opaque, Additive, Premultiplied, Sky }

    /// <summary>
    /// A population or effect that is not a catalogue body (§4.1).
    ///
    /// STUB, lane `nav-ui`: the layer framework is integrator-owned and was not in the foundation
    /// branch, but three of this lane's deliverables are defined in terms of it — destinations come
    /// from layers rather than a hand-kept list, the LAYERS tab is generated from the same table,
    /// and the measure tool picks anything a layer exposes. Written to §4.1 verbatim so the
    /// integrator's own copy replaces it with no caller changing. Delete at merge.
    /// </summary>
    public abstract class CosmoLayer : MonoBehaviour
    {
        public abstract string LayerId { get; }
        public abstract IReadOnlyList<string> DataKinds { get; }
        public abstract LayerBudget Budget { get; }
        public abstract LayerBlend Blend { get; }

        /// <summary>This frame's weight. 0 means not drawn and not pickable.</summary>
        public float Weight { get; private set; }

        protected UniverseSimulation Simulation { get; private set; }
        protected LayerState State { get; private set; }

        public void Bind(UniverseSimulation simulation, LayerState state)
        {
            Simulation = simulation;
            State = state;
        }

        /// <summary>Called by the layer kit when a kind this layer consumes has finished parsing.</summary>
        public abstract void OnData(string kind, object parsed);

        protected virtual void OnEnable() => LayerRegistry.Register(this);
        protected virtual void OnDisable() => LayerRegistry.Unregister(this);

        /// <summary>
        /// Called before <see cref="DeclareGroups"/>, so a layer that is off, out of window or
        /// missing its data declares no group at all (R28).
        /// </summary>
        public void UpdateWeight(in FrameContext ctx)
        {
            Weight = State == null ? 0f : (float)State.Weight(LayerId, ctx.Log10U, ctx.LayerContext);
            SetRenderersEnabled(Weight > 0f);
        }

        /// <summary>Called after AcceptGroup, once per frame, only when Weight &gt; 0.</summary>
        public void Apply(in FrameContext ctx) => ApplyCore(ctx);

        protected abstract void SetRenderersEnabled(bool on);

        /// <summary>Uniforms, chunk culling, rigid parts; writes Weight into every owned _Fade.</summary>
        protected abstract void ApplyCore(in FrameContext ctx);

        public virtual int DeclareGroups(List<GroupNode> into) => 0;

        public virtual void AcceptGroup(int localIndex, in GroupPlacement placement, in FrameContext ctx) { }

        public virtual void CollectDestinations(List<Destination> into) { }

        public virtual void CollectLabels(List<LabelCandidate> into) { }

        /// <summary>Pick in RENDERED space; report TRUE positions.</summary>
        public virtual bool TryPick(Ray worldRay, in FrameContext ctx, out PickHit hit)
        {
            hit = default;
            return false;
        }

        public virtual double NearestSurfaceKm(Double3 absoluteKm) => double.PositiveInfinity;

        /// <summary>Every invariant this layer can check without a headset, one sentence each.</summary>
        public virtual void SelfCheck(List<string> problems) { }
    }

    /// <summary>What a ray hit, in true coordinates. Measure and travel read these, never render positions.</summary>
    public struct PickHit
    {
        public string LayerId, Id, Label;

        /// <summary>True absolute ICRF km. The measure tool uses this and nothing else.</summary>
        public Double3 PositionKm;

        public double TrueDistanceKm;

        /// <summary>Off-ray angle, for choosing among layers and bodies.</summary>
        public float AngleRad;
    }

    /// <summary>Every live layer. A non-MonoBehaviour helper, so it may share this file.</summary>
    public static class LayerRegistry
    {
        private static readonly List<CosmoLayer> Layers = new List<CosmoLayer>();

        public static IReadOnlyList<CosmoLayer> All => Layers;

        public static void Register(CosmoLayer layer)
        {
            if (!Layers.Contains(layer)) Layers.Add(layer);
        }

        public static void Unregister(CosmoLayer layer) => Layers.Remove(layer);

        public static CosmoLayer Find(string id)
        {
            foreach (CosmoLayer layer in Layers)
                if (layer.LayerId == id) return layer;
            return null;
        }
    }
}
