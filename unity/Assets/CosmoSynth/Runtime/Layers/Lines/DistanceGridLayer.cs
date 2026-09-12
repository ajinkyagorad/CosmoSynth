using System;
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Cylindrical distance and light-time grids, at the three regimes the Atlas uses.
    /// CONTRACT §5.4 <c>cylGrids</c>; Atlas MWA-25.
    ///
    /// In a scene with no horizon and no familiar objects, a grid is the only honest answer to
    /// "how far is that". Three regimes — 10 to 500 parsecs about the Sun, 2 to 20 kiloparsecs
    /// about the Galactic centre, half a megaparsec to ten about the Local Group — each a ring set
    /// with twelve spokes, in its own native frame.
    ///
    /// <b>Drawn only when legible.</b> A ring smaller than 8 cm in the room is a blob on the centre
    /// and one larger than 3 m is a band across the whole view; both are worse than nothing. That
    /// is the same rule the orbit lines learned the hard way — the hairline across both shipped
    /// screenshots was Earth's own orbit seen from Earth — and it is arithmetic in
    /// <see cref="DistanceGrids"/> rather than a number in a shader.
    ///
    /// <b>One regime at a time</b>, chosen by which has the most legible rings at the current
    /// scale. Which grid suits a scale is a fact about the scale, not a preference.
    ///
    /// <b>Each ring carries a light-travel time as well as a distance.</b> At these scales the
    /// second number is the interesting one: the outermost galactic ring is 65,000 years of light,
    /// which is a fact about looking rather than a fact about geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DistanceGridLayer : CosmoLayer
    {
        private readonly Dictionary<GridRegime, GameObject> _objects =
            new Dictionary<GridRegime, GameObject>();
        private readonly Dictionary<GridRegime, MeshRenderer> _renderers =
            new Dictionary<GridRegime, MeshRenderer>();
        private readonly Dictionary<GridRegime, Material> _materials =
            new Dictionary<GridRegime, Material>();
        private readonly Dictionary<GridRegime, Mesh> _meshes = new Dictionary<GridRegime, Mesh>();

        private GridRegime? _active;

        public override string LayerId => "cylGrids";

        public override IReadOnlyList<string> DataKinds => new string[0];

        /// <summary>
        /// One regime draws at a time: one draw, and six rings of 128 segments plus 12 spokes is
        /// about 3,200 vertices. Inside the §7.3 ribbon row with room to spare.
        /// </summary>
        public override LayerBudget Budget => new LayerBudget(1, 4_000, 0.02f, 0f, 0f);

        public override LayerBlend Blend => LayerBlend.Additive;

        public override void OnData(string kind, object parsed) { }

        private void Awake()
        {
            foreach (GridRegime regime in new[]
                     { GridRegime.Stellar, GridRegime.Galactic, GridRegime.LocalGroup })
            {
                Build(regime);
            }
            HasData = true;   // Core arithmetic; nothing to wait for
        }

        /// <summary>
        /// One regime's mesh, authored in kilometres in its own plane's frame, with z the plane
        /// normal. The frame rotation is applied by the transform, not by resampling the points:
        /// a galactic grid rotated into supergalactic coordinates would measure nothing anybody
        /// published (CONTRACT §3.4).
        /// </summary>
        private void Build(GridRegime regime)
        {
            IReadOnlyList<GridRing> rings = DistanceGrids.Rings(regime);
            List<RibbonMesh.Polyline> lines = new List<RibbonMesh.Polyline>();
            Color ringColour = CosmoTheme.Scaled(CosmoTheme.TextMuted, 0.8f);
            Color spokeColour = CosmoTheme.Scaled(CosmoTheme.TextMuted, 0.35f);

            double outer = rings[rings.Count - 1].RadiusKm;
            for (int i = 0; i < rings.Count; i++)
            {
                lines.Add(RibbonMesh.Circle(Vector3.zero, Vector3.forward,
                    (float)(rings[i].RadiusKm / outer), DistanceGrids.RingSegments, ringColour));
            }

            for (int s = 0; s < DistanceGrids.Spokes; s++)
            {
                double a = s * MathD.TwoPi / DistanceGrids.Spokes;
                RibbonMesh.Polyline spoke = new RibbonMesh.Polyline { Colour = spokeColour };
                spoke.Points.Add(Vector3.zero);
                spoke.Points.Add(new Vector3((float)Math.Cos(a), (float)Math.Sin(a), 0f));
                spoke.Gradient.Add(0f);
                spoke.Gradient.Add(1f);
                lines.Add(spoke);
            }

            Mesh mesh = RibbonMesh.Build(lines, regime + " grid");
            GameObject go = new GameObject(regime + " grid");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            Material material = CosmoShaders.Material(CosmoShaders.Ribbon);
            material.renderQueue = 3060;
            material.SetFloat(Shader.PropertyToID("_WidthDmm"), 1.0f);
            material.SetFloat(Shader.PropertyToID("_Placement"), 1f);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            _objects[regime] = go;
            _renderers[regime] = renderer;
            _materials[regime] = material;
            _meshes[regime] = mesh;
        }

        protected override void SetRenderersEnabled(bool on)
        {
            if (on) return;
            foreach (MeshRenderer renderer in _renderers.Values)
                if (renderer != null) renderer.enabled = false;
            _active = null;
        }

        protected override void ApplyCore(in FrameContext ctx)
        {
            if (Simulation == null || Simulation.Origin == null) return;

            _active = DistanceGrids.Best(ctx.UnitsPerKm);

            foreach (KeyValuePair<GridRegime, MeshRenderer> pair in _renderers)
            {
                bool on = _active.HasValue && pair.Key == _active.Value && Weight > 0f;
                if (pair.Value != null) pair.Value.enabled = on;
                if (!on) continue;

                GridRegime regime = pair.Key;
                IReadOnlyList<GridRing> rings = DistanceGrids.Rings(regime);
                double outer = rings[rings.Count - 1].RadiusKm;

                Transform t = _objects[regime].transform;
                t.localPosition = Simulation.Origin.ToRender(DistanceGrids.CentreKm(regime));

                // The mesh is authored with z as the plane normal, so the frame that takes mesh
                // axes to ICRF is the one whose third column is the plane's normal. Building it
                // from the normal alone keeps the two in step: the grid can only ever be in the
                // plane DistanceGrids says it is in.
                OrientedPlacement.SetOrientation(t, PlaneFrame(regime),
                    (float)(outer * ctx.UnitsPerKm));

                WriteFade(_materials[regime], Weight);
            }
        }

        /// <summary>
        /// The mesh-to-ICRF rotation for a regime: the grid's plane, taken from its native frame's
        /// matrix rather than reconstructed, so no third spelling of "the galactic plane" exists.
        /// </summary>
        private static Matrix3d PlaneFrame(GridRegime regime) => regime == GridRegime.LocalGroup
            ? StandardFrames.SupergalacticToEquatorial
            : StandardFrames.GalacticToEquatorial;

        public override void CollectLabels(List<LabelCandidate> into)
        {
            if (!_active.HasValue || Weight <= 0f || Simulation == null || Simulation.Origin == null)
                return;

            GridRegime regime = _active.Value;
            Double3 centre = DistanceGrids.CentreKm(regime);
            Double3 normal = DistanceGrids.PlaneNormalIcrf(regime);
            Double3 inPlane = Double3.Cross(normal,
                Math.Abs(normal.z) < 0.9 ? Double3.UnitZ : Double3.UnitX).Normalized;

            IReadOnlyList<GridRing> rings = DistanceGrids.Rings(regime);
            for (int i = 0; i < rings.Count; i++)
            {
                if (!DistanceGrids.IsLegible(rings[i].RadiusKm, Simulation.Origin.UnitsPerKm)) continue;
                string text = rings[i].DistanceLabel + "  ·  " + LightTime(rings[i].LightYears);
                into.Add(LabelCandidate.At("grid:" + regime + ":" + i, text, LabelCategory.Guides,
                    centre + inPlane * rings[i].RadiusKm, 0.2f, ProvenanceClass.Procedural));
            }
        }

        /// <summary>Light-travel time in the largest unit that still reads as a number.</summary>
        public static string LightTime(double years)
        {
            if (years >= 1e9) return (years / 1e9).ToString("0.##") + " Gyr of light";
            if (years >= 1e6) return (years / 1e6).ToString("0.##") + " Myr of light";
            if (years >= 1e3) return (years / 1e3).ToString("0.##") + " kyr of light";
            return years.ToString("0.##") + " yr of light";
        }

        public override void SelfCheck(List<string> problems)
        {
            foreach (KeyValuePair<GridRegime, Mesh> pair in _meshes)
                if (pair.Value != null && pair.Value.vertexCount > Budget.Vertices)
                    problems.Add("cylGrids: the " + pair.Key + " grid has " +
                                 pair.Value.vertexCount + " vertices, over budget");

            int enabled = 0;
            foreach (MeshRenderer renderer in _renderers.Values)
                if (renderer != null && renderer.enabled) enabled++;
            if (enabled > 1) problems.Add("cylGrids: " + enabled + " regimes drawing at once");
        }

        private void OnDestroy()
        {
            foreach (GameObject go in _objects.Values) if (go != null) Destroy(go);
            foreach (Material material in _materials.Values) if (material != null) Destroy(material);
            foreach (Mesh mesh in _meshes.Values) if (mesh != null) Destroy(mesh);
            _objects.Clear();
            _renderers.Clear();
            _materials.Clear();
            _meshes.Clear();
        }
    }
}
