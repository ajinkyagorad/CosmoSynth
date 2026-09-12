using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The Sun's path round the Galaxy: one ring at R0 = 8.15 kpc about Sgr A*.
    /// CONTRACT §5.4 <c>sunOrbit</c>; Atlas MWA-25.
    ///
    /// It is a circle, not an orbit: the real path precesses and oscillates through the plane, and
    /// the one number here that is measured is the radius. Drawing it as an ellipse would be
    /// inventing an eccentricity. What it is for is scale — at galactic rungs there is nothing in
    /// view whose size anyone knows, and a ring the Sun sits on gives the disc a unit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SunOrbitLayer : CosmoLayer
    {
        public const int Segments = 192;

        private GameObject _object;
        private MeshRenderer _renderer;
        private Material _material;
        private Mesh _mesh;

        public override string LayerId => "sunOrbit";

        public override IReadOnlyList<string> DataKinds => new string[0];

        public override LayerBudget Budget => new LayerBudget(1, 800, 0.01f, 0f, 0f);

        public override LayerBlend Blend => LayerBlend.Additive;

        public override void OnData(string kind, object parsed) { }

        private void Awake()
        {
            Build();
            HasData = true;
        }

        private void Build()
        {
            DestroyAll();

            // In galactocentric kpc, in the plane, radius R0. The Sun's 20.8 pc offset above the
            // mid-plane is kept: it is measured, it is 0.25 % of the radius, and dropping it would
            // put the Sun visibly off its own ring at close scales.
            RibbonMesh.Polyline ring = new RibbonMesh.Polyline
            {
                Colour = CosmoTheme.Scaled(CosmoTheme.Accent, 0.5f),
                Closed = true,
            };
            for (int i = 0; i < Segments; i++)
            {
                double a = i * MathD.TwoPi / Segments;
                ring.Points.Add(new Vector3(
                    (float)(GalaxyModel.R0Kpc * System.Math.Cos(a)),
                    (float)(GalaxyModel.R0Kpc * System.Math.Sin(a)),
                    (float)(GalaxyModel.SunHeightPc / 1000.0)));
            }

            _mesh = RibbonMesh.Build(new[] { ring }, "Sun's galactic orbit");
            _object = new GameObject("Sun's galactic orbit");
            _object.transform.SetParent(transform, false);
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _object.AddComponent<MeshRenderer>();
            _material = CosmoShaders.Material(CosmoShaders.Ribbon);
            _material.renderQueue = 3060;
            _material.SetFloat(Shader.PropertyToID("_WidthDmm"), 1.2f);
            _material.SetFloat(Shader.PropertyToID("_Placement"), 1f);
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
        }

        protected override void SetRenderersEnabled(bool on)
        {
            if (_renderer != null) _renderer.enabled = on;
        }

        protected override void ApplyCore(in FrameContext ctx)
        {
            if (_object == null || Simulation == null || Simulation.Origin == null) return;
            Matrix3d icrfFromMesh = StandardFrames.GalacticToEquatorial * Matrix3d.RotationZ(MathD.Pi);
            _object.transform.localPosition = Simulation.Origin.ToRender(GalaxyModel.GalacticCentreIcrfKm);
            OrientedPlacement.SetOrientation(_object.transform, icrfFromMesh,
                (float)(GalaxyModel.KmPerKiloparsec * ctx.UnitsPerKm));
            WriteFade(_material, Weight);
        }

        public override void CollectLabels(List<LabelCandidate> into)
        {
            if (Weight <= 0f) return;
            into.Add(LabelCandidate.At("sunOrbit", "Sun's orbit, 8.15 kpc", LabelCategory.Guides,
                Double3.Zero, 0.3f, ProvenanceClass.Measured));
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
