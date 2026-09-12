using System;
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The microwave background: an inside-out sphere at infinity, and a wire shell marking the
    /// edge of the observable universe. CONTRACT §5.4 <c>cmb</c>, §2.6, C.12.
    ///
    /// <b>It is at infinity, per eye, so it cannot be a wall you fly into.</b> The vertex stage
    /// anchors every vertex to the eye it is being drawn for, which under multiview runs once per
    /// eye: zero stereo disparity, zero head-motion parallax, and no surface to collide with. That
    /// is not a trick to avoid a bug — it is what infinity looks like, and the last scattering
    /// surface genuinely is the edge of what can be seen rather than an object in front of you.
    ///
    /// <b>It fades in only at the outermost scales.</b> Below the BEYOND rungs it is off: a
    /// microwave sky behind the Solar System would be a screensaver. The window is the layer
    /// table's, and the fade rides the same weight as everything else.
    ///
    /// <b>The colour is a function, not a picture.</b> The Wien peak of a 2.725 K black body is at
    /// 1.06 mm; nothing here is a colour anyone could see. <see cref="CmbModel.Palette"/> is the
    /// one place the mapping lives, and the legend, the LUT and the tap-to-measure readout are all
    /// generated from it, so they cannot tell three different stories about what red means.
    ///
    /// <b>The dipole can be switched off, and is by default.</b> The 3.4 mK dipole is not a
    /// feature of the early universe at all — it is the Solar System moving at 369 km/s — and a
    /// viewer who cannot separate the two has no way to learn that. Adding it re-bakes the cube on
    /// the CPU rather than adding a shader uniform, because the <c>cmb-faces</c> assertion
    /// compares GPU texels against <c>CmbSky.Sample</c> and a shader-side dipole would make that
    /// comparison meaningless. The assertion must therefore run in Anisotropy mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CmbLayer : CosmoLayer
    {
        /// <summary>Room visibility at or below which the sky form is allowed to draw.</summary>
        public const float SpaceThreshold = 0.35f;

        private CmbSky _sky;
        private GameObject _skyObject;
        private MeshRenderer _skyRenderer;
        private Material _skyMaterial;
        private Mesh _skyMesh;
        private Cubemap _cube;
        private Texture2D _lut;

        private GameObject _shellObject;
        private MeshRenderer _shellRenderer;
        private Material _shellMaterial;
        private Mesh _shellMesh;

        private CmbMode _bakedMode = CmbMode.Anisotropy;
        private CmbFrame _frame = CmbFrame.Sun;
        private float _skyFade;
        private float _shellFade;

        private static readonly int SkyCubeId = Shader.PropertyToID("_SkyCube");
        private static readonly int LutId = Shader.PropertyToID("_Lut");
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int ImmersionId = Shader.PropertyToID("_Immersion");

        public override string LayerId => "cmb";

        public override IReadOnlyList<string> DataKinds => new[] { "cmb" };

        /// <summary>
        /// §7.3: one draw, about 2 k vertices, one opaque-fill screen, 8.4 MB of cube plus mask.
        /// The wire shell adds a second draw and 600 vertices.
        /// </summary>
        public override LayerBudget Budget => new LayerBudget(2, 3_000, 1.0f, 0f, 8.9f);

        /// <summary>
        /// Sky: the ONE pass that writes coverage on purpose, and only in full-space mode. In
        /// passthrough the layer does not draw at all, because alpha is what hides the room.
        /// </summary>
        public override LayerBlend Blend => LayerBlend.Sky;

        public override void OnData(string kind, object parsed)
        {
            if (kind != "cmb") return;

            _sky = parsed as CmbSky;
            if (_sky == null || !_sky.IsPresent)
            {
                // No file, no layer. The wire shell belongs to this layer too, so it goes with
                // it: a shell drawn while the panel says "not in this build" would be claiming
                // the CMB is here.
                HasData = false;
                AbsenceReason = "cmb/cmb_sky.cscb is not in this build; no microwave background drawn";
                return;
            }

            BuildSky();
            BuildShell();
            HasData = true;
            AbsenceReason = null;
        }

        // ---- building --------------------------------------------------------------------------

        private void BuildSky()
        {
            DestroySky();

            _cube = new Cubemap(_sky.FaceSize, TextureFormat.R8, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "CMB",
            };
            UploadCube(CmbMode.Anisotropy);

            _lut = BuildLut();

            _skyMesh = SphereMesh.Unit("CMB sky");
            _skyObject = new GameObject("CMB sky");
            _skyObject.transform.SetParent(transform, false);
            _skyObject.AddComponent<MeshFilter>().sharedMesh = _skyMesh;
            _skyRenderer = _skyObject.AddComponent<MeshRenderer>();
            _skyMaterial = CosmoShaders.Material(CosmoShaders.Sky);
            _skyMaterial.renderQueue = 1000;
            _skyMaterial.SetTexture(SkyCubeId, _cube);
            _skyMaterial.SetTexture(LutId, _lut);
            _skyRenderer.sharedMaterial = _skyMaterial;
            _skyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _skyRenderer.receiveShadows = false;
            _skyRenderer.enabled = false;
        }

        /// <summary>
        /// Write the six faces. In Anisotropy mode the file's own bytes go up untouched, which is
        /// what makes the <c>cmb-faces</c> assertion a real comparison; the other modes add the
        /// dipole texel by texel through <see cref="CmbModel"/>.
        /// </summary>
        private void UploadCube(CmbMode mode)
        {
            int size = _sky.FaceSize;
            byte[] face = new byte[size * size];

            for (int f = 0; f < CmbSky.Faces; f++)
            {
                byte[] source = _sky.Face(f);
                if (source == null) continue;

                if (mode == CmbMode.Anisotropy)
                {
                    Array.Copy(source, face, Math.Min(source.Length, face.Length));
                }
                else
                {
                    double amplitude = CmbModel.DipoleAmplitude(_frame);
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            Double3 dir = CmbSky.DirectionOf(f, (x + 0.5) / size, (y + 0.5) / size);
                            double dipole = amplitude * Double3.Dot(dir, CmbModel.DipoleDirectionIcrf);
                            double total = CmbModel.DecodeMicroK(source[y * size + x]) + dipole;
                            face[y * size + x] = Encode(total);
                        }
                    }
                }

                _cube.SetPixelData(face, 0, (CubemapFace)f);
            }
            _cube.Apply(false, false);
            _bakedMode = mode;
        }

        private static byte Encode(double microK)
        {
            double t = MathD.Clamp01(
                (microK + CmbModel.EncodedRangeMicroK) / (2.0 * CmbModel.EncodedRangeMicroK));
            return (byte)Math.Round(t * 255.0);
        }

        private Texture2D BuildLut()
        {
            Texture2D lut = new Texture2D(256, 1, TextureFormat.RGBA32, false, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "CMB LUT",
            };
            Color[] pixels = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                // The shader's t is already the contrast-mapped position, so the LUT is sampled
                // over the full +/- range: the palette function is asked for the same thing here
                // as the legend asks for.
                double microK = (i / 255.0) * 2.0 * CmbModel.EncodedRangeMicroK
                                - CmbModel.EncodedRangeMicroK;
                pixels[i] = CosmoTheme.FromLinear(
                    CmbModel.Palette(microK, CmbModel.EncodedRangeMicroK)).gamma;
            }
            lut.SetPixels(pixels);
            lut.Apply(false, false);
            return lut;
        }

        /// <summary>
        /// The edge of the observable universe as three great circles at 13.87 Gpc.
        ///
        /// This is the object form's honest stand-in. CONTRACT §4.5 asks for a 1.2 m textured ball
        /// of the microwave sky at the outermost rung, and no shader in <c>CosmoShaders.All</c>
        /// can draw a cube-mapped sphere that is NOT anchored to the eye — <c>CosmoSynth/Sky</c>
        /// is eye-anchored by construction. Asking for a shader that does not exist ships magenta,
        /// so the shell is drawn as a wire boundary with its radius labelled, which claims exactly
        /// what it can support. The report asks the shader lane for an object mode on Sky.
        /// </summary>
        private void BuildShell()
        {
            DestroyShell();

            RibbonMesh.Polyline[] rings =
                SphereMesh.WireFrame(CosmoTheme.Scaled(CosmoTheme.Accent, 0.35f));
            _shellMesh = RibbonMesh.Build(rings, "Observable universe");
            _shellObject = new GameObject("Observable universe");
            _shellObject.transform.SetParent(transform, false);
            _shellObject.AddComponent<MeshFilter>().sharedMesh = _shellMesh;
            _shellRenderer = _shellObject.AddComponent<MeshRenderer>();
            _shellMaterial = CosmoShaders.Material(CosmoShaders.Ribbon);
            _shellMaterial.renderQueue = 3060;
            _shellMaterial.SetFloat(Shader.PropertyToID("_WidthDmm"), 1.2f);
            _shellMaterial.SetFloat(Shader.PropertyToID("_Placement"), 1f);
            _shellRenderer.sharedMaterial = _shellMaterial;
            _shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shellRenderer.receiveShadows = false;
            _shellRenderer.enabled = false;
        }

        // ---- per frame --------------------------------------------------------------------------

        protected override void SetRenderersEnabled(bool on)
        {
            if (!on)
            {
                if (_skyRenderer != null) _skyRenderer.enabled = false;
                if (_shellRenderer != null) _shellRenderer.enabled = false;
                _skyFade = 0f;
                _shellFade = 0f;
            }
        }

        protected override void ApplyCore(in FrameContext ctx)
        {
            CmbMode mode = (CmbMode)(int)State.Param(LayerId, "mode");
            float contrast = (float)State.Param(LayerId, "contrast");

            if (_sky != null && _sky.IsPresent && mode != _bakedMode) UploadCube(mode);

            // In passthrough the sky form does not draw. Sky writes coverage into alpha on
            // purpose, and alpha is what hides the room: a CMB behind the furniture would be an
            // opaque wall the compositor honours and no desktop capture shows.
            float immersion = 1f - Mathf.Clamp01(ctx.LayerContext.RoomVisibility);
            bool allowed = immersion > 1f - SpaceThreshold || ctx.LayerContext.CmbBackdrop;

            _skyFade = _sky != null && _sky.IsPresent && allowed ? Weight * immersion : 0f;

            if (_skyRenderer != null)
            {
                _skyRenderer.enabled = _skyFade > 0.004f;
                if (_skyRenderer.enabled)
                {
                    // Sky has no _Fade of its own (it is the one shader in the set without one);
                    // _Immersion is both the full-space switch and, here, the window fade. The
                    // report asks the shader lane to add _Fade so the two stop sharing a channel.
                    _skyMaterial.SetFloat(ImmersionId, _skyFade);
                    _skyMaterial.SetFloat(ContrastId, (float)(CmbModel.EncodedRangeMicroK / Mathf.Max(1f, contrast)));
                }
            }

            ApplyShell(ctx);
        }

        /// <summary>
        /// The wire shell, drawn only from OUTSIDE it — from inside it is a band across the whole
        /// view, which is the same rule the orbit lines follow and for the same reason.
        /// </summary>
        private void ApplyShell(in FrameContext ctx)
        {
            if (_shellObject == null || Simulation == null || Simulation.Origin == null) return;

            double radiusKm = CmbModel.ObservableRadiusKm;
            double roomRadius = radiusKm * ctx.UnitsPerKm;
            bool outside = ctx.EyeKm.Magnitude < radiusKm;      // the observer is at its centre

            // Legible: at least 8 cm and at most 3 m in the room, the same band the grids use.
            _shellFade = outside && roomRadius >= DistanceGrids.MinRoomMetres &&
                         roomRadius <= DistanceGrids.MaxRoomMetres
                ? Weight
                : 0f;

            _shellRenderer.enabled = _shellFade > 0.004f;
            if (!_shellRenderer.enabled) return;

            _shellObject.transform.localPosition = Simulation.Origin.ToRender(Double3.Zero);
            _shellObject.transform.localRotation = FloatingOrigin.IcrfMeshRotation;
            _shellObject.transform.localScale = FloatingOrigin.IcrfMeshScale((float)roomRadius);
            WriteFade(_shellMaterial, _shellFade);
        }

        // ---- the rest ----------------------------------------------------------------------------

        public override void CollectDestinations(List<Destination> into)
        {
            into.Add(new Destination(
                "cmb", "Observable universe and CMB", DestinationKind.Cmb, null, LayerId,
                Double3.Zero, CmbModel.ObservableRadiusKm, 1.2,
                PresentationPlane.Supergalactic, ProvenanceClass.Measured,
                _sky != null ? _sky.Source : "Planck PR3 SMICA-noSZ"));
        }

        public override void CollectLabels(List<LabelCandidate> into)
        {
            if (_shellFade <= 0f) return;
            into.Add(LabelCandidate.Toward("cmb:dipole", "CMB dipole", LabelCategory.Structures,
                CmbModel.DipoleDirectionIcrf, 0.4f, ProvenanceClass.Measured));
        }

        /// <summary>Temperature in a direction, for tap-to-measure. Everything the legend quotes.</summary>
        public double TemperatureK(Double3 dirIcrf) =>
            _sky != null ? _sky.TemperatureK(dirIcrf, _frame)
                         : CmbModel.TemperatureK(dirIcrf, 0.0, _frame);

        public bool IsInpainted(Double3 dirIcrf) => _sky != null && _sky.IsInpainted(dirIcrf);

        public override void SelfCheck(List<string> problems)
        {
            // R10: the two forms never draw together. Here that is the sky against the shell.
            if (_skyFade > 0f && _shellFade > 0f)
                problems.Add("cmb: both the sky form and the shell have a non-zero fade in one frame");
            if (_cube != null && _cube.width != (_sky?.FaceSize ?? 0))
                problems.Add("cmb: the uploaded cube face size does not match the file's");
        }

        private void DestroySky()
        {
            if (_skyObject != null) Destroy(_skyObject);
            if (_skyMaterial != null) Destroy(_skyMaterial);
            if (_skyMesh != null) Destroy(_skyMesh);
            if (_cube != null) Destroy(_cube);
            if (_lut != null) Destroy(_lut);
            _skyObject = null; _skyRenderer = null; _skyMaterial = null; _skyMesh = null;
            _cube = null; _lut = null;
        }

        private void DestroyShell()
        {
            if (_shellObject != null) Destroy(_shellObject);
            if (_shellMaterial != null) Destroy(_shellMaterial);
            if (_shellMesh != null) Destroy(_shellMesh);
            _shellObject = null; _shellRenderer = null; _shellMaterial = null; _shellMesh = null;
        }

        private void OnDestroy()
        {
            DestroySky();
            DestroyShell();
        }
    }
}
