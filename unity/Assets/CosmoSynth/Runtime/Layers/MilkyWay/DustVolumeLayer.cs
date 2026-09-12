using System;
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The Milky Way dust, raymarched inside its own bounded proxy.
    /// CONTRACT §5.4 <c>dust</c>, §7.3, C.7; Atlas MWA-09/10.
    ///
    /// The proxy is the survey's own box, in galactic heliocentric parsecs, and that is a
    /// statement rather than an optimisation: the Vergely map covers 10 x 10 x 0.8 kpc about the
    /// Sun, the far side of the disc is unmapped, and a cloud drawn outside the box would be
    /// invented. Turning on <c>dustExtent</c> shows the box, so "why does it stop" has a visible
    /// answer.
    ///
    /// <b>The step budget comes from screen coverage, not from a slider.</b> A raymarch costs
    /// steps times pixels; when the box is small on screen it can afford many steps and when it
    /// fills the view it cannot. <c>N = clamp(B / coverage, 16, 64)</c> with B = 30 holds the
    /// product roughly constant, which is the only way one number can be a budget.
    ///
    /// <b>At most one enclosing volume draws per frame</b> (R2). <c>dust</c> and <c>nebulae</c>
    /// overlap over 2.9 decades of scale, and two full-screen marches at coverage 1.0 would be
    /// 60 step-coverages against a global of 33 — about 20 ms on an 11 ms budget. The arbitration
    /// is the framework's, not this layer's; the layer only declares <c>MayEnclose</c>.
    ///
    /// <b>Extinction is not applied here.</b> The star catalogue's magnitudes are already
    /// de-reddened with this same cube at bake time (R41). Dimming a star behind a cloud again
    /// would redden it twice.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DustVolumeLayer : CosmoLayer
    {
        /// <summary>The shared raymarch budget, in step-coverages. CONTRACT §7.3, stated once there.</summary>
        public const float StepBudget = 30f;

        public const int MinimumSteps = 16;
        public const int MaximumSteps = 64;

        private DustCube _cube;
        private GameObject _boxObject;
        private MeshRenderer _boxRenderer;
        private Material _boxMaterial;
        private Mesh _boxMesh;
        private Texture3D _density;
        private Texture2D _ramp;

        private GameObject _extentObject;
        private MeshRenderer _extentRenderer;
        private Material _extentMaterial;
        private Mesh _extentMesh;

        private Double3 _centreIcrfKm;
        private Double3 _halfExtentPc;
        private double _radiusKm;
        private int _lastSteps;

        private static readonly int DecodeId = Shader.PropertyToID("_Decode");
        private static readonly int StepsId = Shader.PropertyToID("_Steps");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int RampId = Shader.PropertyToID("_Ramp");

        public override string LayerId => "dust";

        public override IReadOnlyList<string> DataKinds => new[] { "dust" };

        /// <summary>
        /// §7.3: one draw, 24 vertices, up to one eye screen of premultiplied fragments, the whole
        /// shared step budget, and the 3D texture (10.3 MB at Vergely's native 501 x 501 x 41 R8).
        /// </summary>
        public override LayerBudget Budget => new LayerBudget(1, 24, 1.0f, StepBudget, 10.3f);

        /// <summary>Premultiplied: dust absorbs, so its coverage IS its opacity and it may write alpha.</summary>
        public override LayerBlend Blend => LayerBlend.Premultiplied;

        public override void OnData(string kind, object parsed)
        {
            if (kind != "dust") return;

            _cube = parsed as DustCube;
            if (_cube == null || !_cube.IsPresent)
            {
                HasData = false;
                AbsenceReason = "galaxy/dust.csdu is not in this build; no dust volume drawn";
                return;
            }

            Build();
            HasData = _boxRenderer != null;
            AbsenceReason = HasData ? null : "dust cube present but the 3D texture could not be created";
        }

        private void Build()
        {
            DestroyAll();

            VoxelGrid grid = _cube.Grid;

            // A header that says 501 x 501 x 41 and a payload that is not exactly that many bytes
            // is a parse that half worked. SetPixelData would throw on it, which on a headset is
            // a black screen with no console; refusing it here is a panel row that says why.
            long expected = (long)grid.Nx * grid.Ny * grid.Nz;
            if (grid.Raw == null || grid.Raw.LongLength != expected)
            {
                HasData = false;
                AbsenceReason = "galaxy/dust.csdu header says " + expected + " voxels but carries " +
                                (grid.Raw == null ? 0 : grid.Raw.LongLength) + " bytes; refused";
                return;
            }

            _halfExtentPc = grid.HalfExtentUnits;

            Double3 centreGalacticPc = grid.CentreUnits;
            _centreIcrfKm = DustCube.IcrfPcFromGalacticPc(centreGalacticPc) * GalaxyModel.KmPerParsec;
            _radiusKm = _halfExtentPc.Magnitude * GalaxyModel.KmPerParsec;

            _density = new Texture3D(grid.Nx, grid.Ny, grid.Nz, TextureFormat.R8, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Dust density",
            };
            _density.SetPixelData(grid.Raw, 0);
            _density.Apply(false, true);

            _ramp = BuildRamp();

            _boxMesh = BoxMesh.Unit("Dust proxy");
            _boxObject = new GameObject("Dust volume");
            _boxObject.transform.SetParent(transform, false);
            _boxObject.AddComponent<MeshFilter>().sharedMesh = _boxMesh;
            _boxRenderer = _boxObject.AddComponent<MeshRenderer>();

            _boxMaterial = CosmoShaders.Material(CosmoShaders.Volume);
            _boxMaterial.renderQueue = 3010;
            _boxMaterial.SetTexture(DensityId, _density);
            _boxMaterial.SetTexture(RampId, _ramp);
            _boxRenderer.sharedMaterial = _boxMaterial;
            _boxRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _boxRenderer.receiveShadows = false;
            _boxRenderer.enabled = false;

            BuildExtent();
        }

        /// <summary>256 x 1, linear, from the Core ramp so the legend and the shader cannot disagree.</summary>
        private Texture2D BuildRamp()
        {
            Texture2D ramp = new Texture2D(256, 1, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Dust ramp",
            };
            Color[] pixels = new Color[256];
            for (int i = 0; i < 256; i++)
                pixels[i] = CosmoTheme.FromLinear(GalaxyModel.DustRampLinear(i / 255.0));
            ramp.SetPixels(pixels);
            ramp.Apply(false, true);
            return ramp;
        }

        /// <summary>The box outline: where the measurement ends, drawn on request.</summary>
        private void BuildExtent()
        {
            RibbonMesh.Polyline[] edges =
                BoxMesh.UnitEdges(CosmoTheme.Scaled(CosmoTheme.TextMuted, 0.6f));
            _extentMesh = RibbonMesh.Build(edges, "Dust extent");
            _extentObject = new GameObject("Dust extent");
            _extentObject.transform.SetParent(_boxObject.transform, false);
            _extentObject.AddComponent<MeshFilter>().sharedMesh = _extentMesh;
            _extentRenderer = _extentObject.AddComponent<MeshRenderer>();
            _extentMaterial = CosmoShaders.Material(CosmoShaders.Ribbon);
            _extentMaterial.renderQueue = 3060;
            _extentMaterial.SetFloat(Shader.PropertyToID("_WidthDmm"), 1.0f);
            _extentMaterial.SetFloat(Shader.PropertyToID("_Placement"), 0f);   // rigid with the box
            _extentRenderer.sharedMaterial = _extentMaterial;
            _extentRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _extentRenderer.receiveShadows = false;
            _extentRenderer.enabled = false;
        }

        // ---- groups -----------------------------------------------------------------------------

        public override int DeclareGroups(List<GroupNode> into)
        {
            if (!HasData) return 0;
            into.Add(new GroupNode
            {
                Parent = -1,
                AnchorKm = _centreIcrfKm,
                RadiusKm = _radiusKm,
                LeafRadiusKm = _radiusKm,
                HasChildren = false,
                HasOpaque = false,        // ZWrite Off: it absorbs, it does not occlude
                MayEnclose = true,        // the Sun is inside this box, so the eye usually is too
            });
            return 1;
        }

        public override void AcceptGroup(int localIndex, in GroupPlacement placement, in FrameContext ctx)
        {
            if (_boxObject == null || Simulation == null || Simulation.Origin == null) return;

            Vector3 trueUniverse = Simulation.Origin.ToRender(_centreIcrfKm);
            Placement.PlaceRigid(_boxObject.transform, trueUniverse, placement.Scale, ctx);

            // The mesh is a unit cube in GALACTIC axes; one mesh unit is the full box side.
            double metresPerMeshUnit = 2.0 * _halfExtentPc.x * GalaxyModel.KmPerParsec * ctx.UnitsPerKm;
            OrientedPlacement.SetOrientation(_boxObject.transform,
                StandardFrames.GalacticToEquatorial, (float)(metresPerMeshUnit * placement.Scale));

            // The box is not a cube: the survey is 10 x 10 x 0.8 kpc. Non-uniform scale on a
            // child rather than on the marched cube, so the shader's object space stays a cube.
            Vector3 s = _boxObject.transform.localScale;
            _boxObject.transform.localScale = new Vector3(
                s.x, s.y * (float)(_halfExtentPc.y / _halfExtentPc.x),
                s.z * (float)(_halfExtentPc.z / _halfExtentPc.x));
        }

        // ---- per frame ---------------------------------------------------------------------------

        protected override void SetRenderersEnabled(bool on)
        {
            if (_boxRenderer != null) _boxRenderer.enabled = on;
            if (_extentRenderer != null) _extentRenderer.enabled = false;
        }

        protected override void ApplyCore(in FrameContext ctx)
        {
            if (_boxMaterial == null) return;

            VoxelGrid grid = _cube.Grid;
            float density = (float)State.Param(LayerId, "density");
            float threshold = (float)State.Param(LayerId, "threshold");

            // _Decode.z: extinction per (density value x one object unit). The density is A_V per
            // parsec and one object unit is the box side in parsecs, so the product is magnitudes.
            float parsecsPerObjectUnit = (float)(2.0 * _halfExtentPc.x);
            _boxMaterial.SetVector(DecodeId, new Vector4(
                (float)grid.ValueMin, (float)grid.LogRange, parsecsPerObjectUnit * density, 1f));

            _lastSteps = StepsFor(Coverage(ctx));
            _boxMaterial.SetVector(StepsId, new Vector4(_lastSteps, threshold, 0f, 0f));
            WriteFade(_boxMaterial, Weight);

            if (_extentRenderer != null)
            {
                // The extent belongs to `dustExtent`, which is off by default; it rides this
                // layer's weight because it is meaningless without the volume it bounds.
                _extentRenderer.enabled = Weight > 0f && State.IsOn("dustExtent");
                WriteFade(_extentMaterial, Weight);
            }
        }

        /// <summary>
        /// What fraction of one eye screen the proxy covers, from its angular radius. Inside the
        /// box it is 1 by definition, which is also when the march is most expensive.
        /// </summary>
        private float Coverage(in FrameContext ctx)
        {
            double distance = Double3.Distance(ctx.EyeKm, _centreIcrfKm);
            if (distance <= _radiusKm) return 1f;

            double angular = Math.Asin(Math.Min(1.0, _radiusKm / distance));
            Camera camera = Camera.main;
            double halfFov = camera != null ? camera.fieldOfView * Mathf.Deg2Rad * 0.5 : 0.8;
            double fraction = angular / Math.Max(1e-6, halfFov);
            return (float)MathD.Clamp01(fraction * fraction);
        }

        /// <summary>N = clamp(B / coverage, 16, 64). The whole governor.</summary>
        public static int StepsFor(float coverage) =>
            Mathf.Clamp(Mathf.RoundToInt(StepBudget / Mathf.Max(0.01f, coverage)),
                MinimumSteps, MaximumSteps);

        // ---- the rest -----------------------------------------------------------------------------

        public override void CollectDestinations(List<Destination> into)
        {
            if (!HasData) return;
            into.Add(new Destination(
                "dust:centre", "Local dust", DestinationKind.Nebula, null, LayerId,
                _centreIcrfKm, _radiusKm, 1.6, PresentationPlane.Galactic,
                ProvenanceClass.Inferred, "Vergely, Lallement & Cox 2022"));
        }

        public override void SelfCheck(List<string> problems)
        {
            if (!HasData) return;
            if (_lastSteps > MaximumSteps)
                problems.Add("dust: " + _lastSteps + " raymarch steps, over the governor's ceiling");
            if (_density != null && _density.depth < 2)
                problems.Add("dust: the density texture is not three-dimensional");
            if (_halfExtentPc.x <= 0.0 || _halfExtentPc.z <= 0.0)
                problems.Add("dust: the cube has no extent; the header did not parse");
        }

        private void DestroyAll()
        {
            if (_extentObject != null) Destroy(_extentObject);
            if (_extentMaterial != null) Destroy(_extentMaterial);
            if (_extentMesh != null) Destroy(_extentMesh);
            if (_boxObject != null) Destroy(_boxObject);
            if (_boxMaterial != null) Destroy(_boxMaterial);
            if (_boxMesh != null) Destroy(_boxMesh);
            if (_density != null) Destroy(_density);
            if (_ramp != null) Destroy(_ramp);
            _extentObject = null; _extentRenderer = null; _extentMaterial = null; _extentMesh = null;
            _boxObject = null; _boxRenderer = null; _boxMaterial = null; _boxMesh = null;
            _density = null; _ramp = null;
        }

        private void OnDestroy() => DestroyAll();
    }
}
