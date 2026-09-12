using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// How a layer's fragments reach the framebuffer. Read by the play-mode `alpha` assertion
    /// and by the passthrough-coverage probe; it is the layer's promise about its own blend.
    public enum LayerBlend : byte { Opaque, Additive, Premultiplied, Sky }

    /// <summary>
    /// A population or effect that is not a catalogue body. CONTRACT §4.1, verbatim.
    ///
    /// One subclass per file, created by the bootstrap, backed by an engine-free Core parser.
    /// Layers never talk to each other and never look each other up.
    ///
    /// The important asymmetry: a layer decides nothing about whether it is visible.
    /// <see cref="Weight"/> already folds in on/off, SOLO, the scale window and whether the data
    /// arrived, so a layer that is off, out of window or missing its file contributes no group,
    /// no draw and no pick — which is also what stops the group solver pushing a real group away
    /// from geometry that is not being drawn.
    /// </summary>
    public abstract class CosmoLayer : MonoBehaviour
    {
        public abstract string LayerId { get; }                  // exactly a LayerTable id
        public abstract IReadOnlyList<string> DataKinds { get; } // manifest kinds it consumes
        public abstract LayerBudget Budget { get; }              // ceiling at Weight = 1
        public abstract LayerBlend Blend { get; }                // one kind per layer

        /// <summary>This frame's weight in [0, 1]. 0 means not drawn and not pickable.</summary>
        public float Weight { get; private set; }

        protected UniverseSimulation Simulation { get; private set; }
        protected LayerState State { get; private set; }

        /// <summary>
        /// True once the layer's data has been delivered. False is a normal, reportable state:
        /// the file is not in this build. A layer must never fabricate content to fill the gap.
        /// </summary>
        public bool HasData { get; protected set; }

        /// <summary>One line for the play-mode report and the panel when there is no data.</summary>
        public string AbsenceReason { get; protected set; }

        public void Bind(UniverseSimulation simulation, LayerState state)
        {
            Simulation = simulation;
            State = state;
        }

        /// <summary>
        /// Called by the data loader for every kind in <see cref="DataKinds"/> that arrives, and
        /// again on re-registration if the kind already arrived. Layers never subscribe to the
        /// loader themselves (R33). Build meshes here; the heavy parsing already happened in Core.
        /// </summary>
        public abstract void OnData(string kind, object parsed);

        protected virtual void OnEnable() => LayerRegistry.Register(this);
        protected virtual void OnDisable() => LayerRegistry.Unregister(this);

        /// <summary>
        /// Called by <see cref="ScaleRenderer"/> BEFORE DeclareGroups, so a layer that is off,
        /// out of window or missing its data declares no group at all (R28).
        /// </summary>
        public void UpdateWeight(in FrameContext ctx)
        {
            Weight = State == null ? 0f : (float)State.Weight(LayerId, ctx.Log10U, ctx.LayerContext);
            if (!HasData) Weight = 0f;
            SetRenderersEnabled(Weight > 0f);
        }

        /// <summary>Called after AcceptGroup, once per frame, only when Weight > 0.</summary>
        public void Apply(in FrameContext ctx) => ApplyCore(ctx);

        protected abstract void SetRenderersEnabled(bool on);

        /// <summary>
        /// Uniforms, chunk culling, rigid parts. Must write <see cref="Weight"/> into every owned
        /// material's <c>_Fade</c>, or the 0.25-decade window fade is a pop.
        /// </summary>
        protected abstract void ApplyCore(in FrameContext ctx);

        /// <summary>Rigid or enclosing parts. Append nodes; return how many.</summary>
        public virtual int DeclareGroups(List<GroupNode> into) => 0;

        public virtual void AcceptGroup(int localIndex, in GroupPlacement placement, in FrameContext ctx) { }

        public virtual void CollectDestinations(List<Destination> into) { }
        public virtual void CollectLabels(List<LabelCandidate> into) { }

        /// <summary>Pick in RENDERED space (the hand ray meets what is drawn); report TRUE positions.</summary>
        public virtual bool TryPick(Ray worldRay, in FrameContext ctx, out PickHit hit)
        {
            hit = default;
            return false;
        }

        /// <summary>Feeds NearestBody, so flight speed follows the nearest thing of any kind.</summary>
        public virtual double NearestSurfaceKm(Double3 absoluteKm) => double.PositiveInfinity;

        /// <summary>
        /// Every internal invariant this layer can check without a headset. Append one short
        /// sentence per problem; the play-mode `layers` assertion fails if any layer returns one.
        /// </summary>
        public virtual void SelfCheck(List<string> problems) { }

        /// <summary>
        /// Writing <c>_Fade</c> is not optional and is easy to forget, so it is one call.
        /// A material whose fade is never written stays at the shader default of 1 and pops.
        /// </summary>
        protected void WriteFade(Material material, float fade)
        {
            if (material != null) material.SetFloat(FadeId, fade);
        }

        protected static readonly int FadeId = Shader.PropertyToID("_Fade");
    }

    /// <summary>What a layer hit. Positions are TRUE, absolute, double — never render positions.</summary>
    public struct PickHit
    {
        public string LayerId, Id, Label;
        public Double3 PositionKm;
        public double TrueDistanceKm;
        public float AngleRad;
    }

    /// <summary>
    /// Non-MonoBehaviour helper; sharing the file is legal under the one-MonoBehaviour-per-file
    /// rule, and keeping it here means the registry cannot be registered with twice.
    /// </summary>
    public static class LayerRegistry
    {
        private static readonly List<CosmoLayer> Layers = new List<CosmoLayer>();

        public static IReadOnlyList<CosmoLayer> All => Layers;

        public static void Register(CosmoLayer layer) { if (!Layers.Contains(layer)) Layers.Add(layer); }

        public static void Unregister(CosmoLayer layer) => Layers.Remove(layer);

        public static CosmoLayer Find(string id)
        {
            foreach (CosmoLayer layer in Layers) if (layer.LayerId == id) return layer;
            return null;
        }
    }
}
