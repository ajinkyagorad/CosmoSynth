using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The spiral arms, drawn only where they were fitted. CONTRACT §5.4 <c>arms</c>; Atlas
    /// MWA-09 (corrected).
    ///
    /// Each arm is a log-periodic curve fitted to maser parallaxes, and the parallaxes only cover
    /// part of the disc. Drawing a complete spiral would assert structure on the far side of the
    /// Galaxy that nobody has measured — and it would look magnificent, which is the problem. So
    /// each curve is drawn at full strength over its own fitted azimuth range and fades to a
    /// fifteen-percent ghost over 30 degrees of extrapolation either side, then stops.
    ///
    /// The masers themselves are a separate layer drawn in the same window, on purpose: the fit
    /// and the data it was fitted to are visible together, and the viewer can see for themselves
    /// where the evidence runs out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpiralArmsLayer : CosmoLayer
    {
        /// <summary>How far past the fitted range the curve is ghosted before it stops, degrees.</summary>
        public const double ExtrapolationDeg = 30.0;

        /// <summary>Strength at the far end of the extrapolation. Not zero: the fade is the message.</summary>
        public const float ExtrapolatedStrength = 0.15f;

        /// <summary>Degrees of azimuth per drawn segment.</summary>
        public const double StepDeg = 2.0;

        private GameObject _object;
        private MeshRenderer _renderer;
        private Material _material;
        private Mesh _mesh;

        public override string LayerId => "arms";

        /// <summary>Core constants only — no file, no licence, nothing to be absent.</summary>
        public override IReadOnlyList<string> DataKinds => new string[0];

        /// <summary>Seven arms, about 90 segments each: one draw, under 2,600 vertices.</summary>
        public override LayerBudget Budget => new LayerBudget(1, 3_000, 0.02f, 0f, 0f);

        public override LayerBlend Blend => LayerBlend.Additive;

        public override void OnData(string kind, object parsed) { }

        private void Awake()
        {
            Build();
            HasData = true;   // Core constants; there is nothing to wait for
        }

        private void Build()
        {
            DestroyAll();

            List<RibbonMesh.Polyline> lines = new List<RibbonMesh.Polyline>();
            Color colour = CosmoTheme.FromLinear(GalaxyModel.ArmColourLinear);

            for (int a = 0; a < GalaxyModel.Arms.Length; a++)
            {
                SpiralArm arm = GalaxyModel.Arms[a];
                RibbonMesh.Polyline line = new RibbonMesh.Polyline { Colour = colour };

                double from = arm.BetaMinDeg - ExtrapolationDeg;
                double to = arm.BetaMaxDeg + ExtrapolationDeg;
                for (double beta = from; beta <= to + 1e-9; beta += StepDeg)
                {
                    Double3 p = GalaxyModel.ArmPointKpc(arm, beta);
                    line.Points.Add(new Vector3((float)p.x, (float)p.y, (float)p.z));
                    line.Gradient.Add(Strength(arm, beta));
                }
                lines.Add(line);
            }

            _mesh = RibbonMesh.Build(lines, "Spiral arms");
            _object = new GameObject("Spiral arms");
            _object.transform.SetParent(transform, false);
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _object.AddComponent<MeshRenderer>();
            _material = CosmoShaders.Material(CosmoShaders.Ribbon);
            _material.renderQueue = 3060;
            _material.SetFloat(Shader.PropertyToID("_WidthDmm"), 2.2f);
            // Per-vertex compression: an arm spans 30 kpc and the eye is inside it, so no single
            // similarity about the eye describes the whole curve.
            _material.SetFloat(Shader.PropertyToID("_Placement"), 1f);
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
        }

        /// <summary>
        /// 1 inside the fitted range, fading to a ghost over the extrapolation. The curve is
        /// <see cref="SpiralArm.Support"/> in Core, where it has a test; this is the float cast.
        /// </summary>
        public static float Strength(in SpiralArm arm, double betaDeg) =>
            (float)arm.Support(betaDeg, ExtrapolationDeg, ExtrapolatedStrength);

        protected override void SetRenderersEnabled(bool on)
        {
            if (_renderer != null) _renderer.enabled = on;
        }

        protected override void ApplyCore(in FrameContext ctx)
        {
            if (_object == null || Simulation == null || Simulation.Origin == null) return;

            // The mesh is in galactocentric kpc. Getting to ICRF is a half turn about the pole
            // (the heliocentric frame looks the other way along x) followed by the galactic
            // matrix; the handedness swap after that is OrientedPlacement's problem, not this
            // layer's, which is the entire reason that helper exists.
            Matrix3d icrfFromMesh = StandardFrames.GalacticToEquatorial * Matrix3d.RotationZ(MathD.Pi);
            _object.transform.localPosition = Simulation.Origin.ToRender(GalaxyModel.GalacticCentreIcrfKm);
            OrientedPlacement.SetOrientation(_object.transform, icrfFromMesh,
                (float)(GalaxyModel.KmPerKiloparsec * ctx.UnitsPerKm));

            WriteFade(_material, Weight);
        }

        public override void CollectLabels(List<LabelCandidate> into)
        {
            if (Weight <= 0f) return;
            for (int a = 0; a < GalaxyModel.Arms.Length; a++)
            {
                SpiralArm arm = GalaxyModel.Arms[a];
                double middle = 0.5 * (arm.BetaMinDeg + arm.BetaMaxDeg);
                Double3 km = GalaxyModel.IcrfPcFrom(GalaxyModel.ArmPointKpc(arm, middle))
                             * GalaxyModel.KmPerParsec;
                into.Add(LabelCandidate.At("arm:" + arm.Name, arm.Name, LabelCategory.MilkyWay,
                    km, 0.6f, ProvenanceClass.Inferred));
            }
        }

        public override void SelfCheck(List<string> problems)
        {
            if (_mesh != null && _mesh.vertexCount > Budget.Vertices)
                problems.Add("arms: " + _mesh.vertexCount + " vertices, over budget");
        }

        private void DestroyAll()
        {
            if (_object != null) Destroy(_object);
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
            _object = null; _renderer = null; _material = null; _mesh = null;
        }

        private void OnDestroy() => DestroyAll();
    }
}
