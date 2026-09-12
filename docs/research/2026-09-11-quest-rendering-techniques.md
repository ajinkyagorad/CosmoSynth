# Quest 3 rendering techniques for a 1 m to 1e27 m universe

Research report, 2026-09-11, slug `quest-rendering-techniques`. Read-only phase: nothing under
`~/projects` was changed, no editor was launched, nothing was built. Target: CosmoSynth on Unity
6000.5.5f1, OpenXR 1.17.1 + Unity OpenXR Meta 2.5.0, Quest 3 (XR2 Gen 2 / Adreno 740),
passthrough, single-pass instanced / multiview stereo, 72-90 Hz.

**Confidence labels.** **VERIFIED** = I read the file or page, or ran the computation, myself in
this session (file:line or URL given). **INFERRED** = derived from verified facts by reasoning or
arithmetic, or taken from a secondary source (search snippet, another project's research file);
treat as a hypothesis until measured. In tables: **V** = verified, **I** = inferred.

Paths are on triton unless they start with `D:\`. `CS/` = `~/projects/CosmoSynth/unity/Assets/CosmoSynth/`.

---

## 0. Answers in one page

| # | Question | Recommendation | Budget |
|---|---|---|---|
| 1 | Camera and depth | **One camera.** Delete `DepthBands`. Three layers by render queue, not by camera: a depth-tested *near* layer (hands, UI, the focused system, terrain; near 0.05 m, far <= 1-2 km), a *sky* layer at infinity (stars, galaxies, CMB, pinned bodies; ZWrite Off, drawn first, anchored to each eye), and an optional *compressed* layer (galaxy disc, dust cube, Local Group) placed by a per-group log mapping that preserves angular size. Set OpenXR Depth Submission to **None**. Do not rely on reversed-Z: the Quest eye buffer is **D24_UNORM_S8** (or **D16_UNORM** with CosmoSynth's current Depth16Bit), not float. | 1 render pass; depth-tested content within ~840 m of the eye for 0.1% relative depth resolution |
| 2 | 100k-500k stars, ~50k galaxies | **Static mesh, 4 vertices per point, billboard expanded in the vertex shader** (the GeoramaXR / Chemispace / BoltGraphXR pattern, all three worn on a headset). Magnitude -> size/brightness computed in the shader. Not GPU instancing (1023 per call), not procedural/ComputeBuffer draws (unreliable reports on Quest Vulkan, and the SPI instance-doubling rule is wrong under multiview). | 119,613 stars = 478,452 vertices, ~12 MB; <= ~0.6 M point vertices per frame; sprite overdraw <= 0.5 eye-screens |
| 3 | Dust 201x201x81 | Keep the bounded-proxy raymarch from the web page but **spend a step budget, not a step count** (Fractarium's governor: steps x coverage = constant). Voxel sprites (2x-downsampled, 73,756 cells above cut 0.30) as the tabletop LOD. No half-resolution offscreen pass on Built-in; possible later in URP. | 3.27 MB R8 3D texture; <= ~2.5 ms; 16-40 steps when the camera is inside the cube |
| 4 | CMB | Planck SMICA (Nside 2048, 5 arcmin) resampled offline to a **cubemap 6 x 2048^2, single channel EAC_R11 (or ASTC 6x6)**, colour map applied in the shader from a 256x1 LUT. Drawn as the first thing in the sky layer. | 16.8 MB with mips (EAC_R11); 1 texture sample + 1 LUT sample per pixel |
| 5 | Galaxy impostors | One quad per galaxy, oriented as a **3D disc** from measured axis ratio b/a and position angle: inclination from Hubble's formula with q0 = 0.2, disc normal from PA (E of N). Point below 3 px, template sprite by morphological type above. | 43,533 galaxies (2MRS) = 174,132 vertices, ~6.6 MB |
| 6 | Orbits and grids | Orbits as **screen-space-width ribbons** (4 verts per segment) expanded in the vertex shader with analytic edge AA; not `LineRenderer`, not 1-px `MeshTopology.Lines`. Grids procedural in the fragment shader (Golus "pristine grid"). | <= 150k orbit triangles, 1-3 draws; line width 1.5-2.5 px |
| 7 | Text | **TextMeshPro SDF** with the font taken from `TMP_Settings.defaultFontAsset` (ships because it lives in a `Resources` folder); import TMP Essentials headlessly; gate on glyph count and on the font name inside the APK. | 1 material; atlas ~1-4 MB |
| 8 | URP vs Built-in | **Migrate to URP.** Built-in is deprecated in Unity 6.5, cannot use SRP foveation (build fails today), cannot use render-graph Quest features, and has a won't-fix MSAA bug with depth submission. Every sibling project that shipped to a headset on this box is URP. | MSAA 4x (profile 2x), HDR off, FFR on |
| 9 | Stereo skeleton | Given verbatim in §11 for both pipelines, with the include-file lines that explain what each macro does under Quest multiview. | - |

Two findings that are not questions but matter more than any of the above:

- **CosmoSynth's render configuration differs from every headset-proven project on this box in six
  ways** (pipeline, colour space, depth submission, camera count, symmetric projection, foveation) —
  §2.1.
- **A concrete mechanism for the unconfirmed 1.7.0 "100 plus instances of earth ... on a grid"
  report**: camera stacking (`DepthBands`, since 1.5.0) + Optimize Buffer Discards (memoryless MSAA)
  + MSAA switched on in 1.7.0. INFERRED, with the evidence for and against in §2.2.

---

## 1. What was read and run

**Project authority (VERIFIED, read this session):** `~/projects/CosmoSynth/CLAUDE.md`,
`docs/architecture.md`, `docs/roadmap-universe.md`, `docs/feedback.md`; memory notes
`~/.claude/projects/-home-triton-projects-CosmoSynth/memory/{MEMORY,cosmosynth-status,cosmosynth-xr-empty-scene,cosmosynth-runtime-built-scenes-strip-shaders,cosmosynth-small-things-need-two-bounds,cosmosynth-headless-verification}.md`.

**CosmoSynth code and settings (VERIFIED):** `CS/Runtime/Rendering/{DepthBands,CosmoShaders,RenderDiagnostics,StarPointBillboard,SkyAndRings,OrbitLineRenderer}.cs`,
`CS/Runtime/UI/CosmoText.cs`, `CS/Runtime/Shaders/{CosmoRim,CosmoAtmosphere}.shader`,
`CS/Editor/{XrSetup,ServerBuild}.cs`, `CS/Core/Scale/ScaleSpace.cs`,
`unity/ProjectSettings/{QualitySettings,GraphicsSettings,ProjectSettings}.asset`,
`unity/Assets/XR/Settings/OpenXR Package Settings.asset`, `unity/Packages/manifest.json`.

**Sibling projects (VERIFIED):** `~/projects/Worldfall/docs/CONTRACT.md` (§4, §5),
`docs/RESEARCH.md`, `docs/research/quest-render.md`; `~/projects/GeoramaXR/CONTRACT-XR.md` (§0, §3),
`Assets/Georama/Shaders/{GeoStar,GeoLine}.shader`, `Assets/Georama/Runtime/{GeoPanel,SkyDome}.cs`;
`~/projects/Fractarium/CLAUDE.md`, `Assets/Fractarium/Scripts/Core/QualityGovernor.cs`;
`~/projects/Chemispace/CLAUDE.md`, `Assets/Chemispace/Resources/ChemPoints.shader`;
`~/projects/BoltGraphXR/Assets/BoltGraph/Shaders/BoltPoints.shader`;
`~/projects/NeuroWeaveXR/{FEEDBACK-NOW.md,Assets/NeuroWeave/Shaders/{NwSlice,NwTract}.shader}`;
`~/projects/Mechanarium/CLAUDE.md`. (`NeuroWeaveXR/CLAUDE.md` and `BoltGraphXR/CLAUDE.md` are
14-line skill lists with no rendering content; the rendering facts are in their shaders.)

**Engine and package sources (VERIFIED, grep over the installed files, not docs):**
`~/Unity/Hub/Editor/6000.5.5f1/Editor/Data/Resources/CGIncludes/{HLSLSupport,UnityInstancing,UnityShaderVariables}.cginc`;
in `unity/Library/PackageCache/`: URP 17.5 `ShaderLibrary/UnityInput.hlsl`,
`Runtime/UniversalRendererData.cs`, `Runtime/UniversalRendererRenderGraph.cs`; OpenXR 1.17.1
`Runtime/OpenXRUnityAPI.cs`, `Runtime/Settings/OpenXRRenderSettings.cs`; com.unity.ugui 2.5.0
`Package Resources/`.

**Web (VERIFIED where fetched; URLs in §14).** Meta: OpenXR settings, Meta Quest support settings,
advanced GPU pipelines, performance targets, FFR. Unity: OpenXR project configuration, depth
submission enum, foveated rendering feature, Meta Quest feature, Oculus XR plugin settings,
single-pass instanced shaders, platform differences, 2026 render-pipeline strategy, two issue
tracker entries (via search snippets — the tracker is a JS app that WebFetch cannot render).
NVIDIA "Depth precision visualized"; Outerra log depth; Planck Legacy Archive; Huchra et al. 2012
(2MRS, PDF read); IPAC 2MASS XSC column definitions; Ben Golus grid shader.

**Run (VERIFIED):** occupancy statistics of the real dust cube
(`D:\PhD_Backup\OneDrive - Aalto University\Projects\Nebulytic\webxr-examples\Fable5\nebula-gallery\data\dust.bin`,
3,272,481 bytes) copied to `/tmp/qrt/` on triton and measured with two stdlib Python scripts (§5.1).

---

## 2. CosmoSynth today, against what is proven on this box

### 2.1 Configuration audit

| Setting | CosmoSynth (V) | Headset-proven siblings (V) | Guidance |
|---|---|---|---|
| Render pipeline | **Built-in** — `unity/ProjectSettings/GraphicsSettings.asset:55` `m_CustomRenderPipeline: {fileID: 0}` (URP 17.5.0 installed but unassigned, `Packages/manifest.json`) | URP in GeoramaXR (`GraphicsSettings.asset:105`), BoltGraphXR (:108), NeuroWeaveXR (:109), Fractarium (:121), Mechanarium (:124), Microverse (:97); Built-in only in Chemispace (:54) | Built-in deprecated in Unity 6.5 (§10) |
| Colour space | **Gamma** — `ProjectSettings.asset:49` `m_ActiveColorSpace: 0`; set nowhere in code | Linear (`GeoramaXR/ProjectSettings/ProjectSettings.asset:49` = 1, Chemispace = 1) | OpenXR composes in linear; the Oculus plugin docs and the Unity forum say Gamma is unsupported with OpenXR (V, forum thread §14) |
| Cameras | **2** — `DepthBands.cs:17-19,59-85` (far camera child of the near one, near camera `CameraClearFlags.Depth` at :78) | 1 (GeoramaXR far plane 1200 m, per memory note `georamaxr-project`) | each extra pass costs a colour store + load (§3.3) |
| Depth submission | **Depth16Bit** — `OpenXR Package Settings.asset:172` (`m_depthSubmissionMode: 1` in the `m_Name: Android` block at :126), written by `XrSetup.cs:281` | None (`Worldfall/docs/research/quest-render.md:18`) | Meta: "None" for most Quest apps (V) |
| MSAA | 0 — Android default quality level 2 "Medium" (`QualitySettings.asset:329`), `antiAliasing: 0` (:131). 1.7.0 had MSAA on; 1.7.2 reverted it (`docs/roadmap-universe.md`, Phase 0) | 4x (GeoramaXR memory note; Worldfall contract §2.6 "MSAA 4") | Meta 4x, Unity XR page 2x — profile both (Worldfall RESEARCH:51) |
| Optimize Buffer Discards | **on** — `:174` (and MetaQuestFeature block ~:1409) | on (Worldfall) | "can potentially break ... camera stacking" (Oculus XR plugin docs, V) |
| Symmetric Projection | **off** — `:175`, `~:1406` | on (Worldfall) | 5-15% GPU in GPU-bound scenes (Meta, V) |
| Foveation | feature force-disabled — `XrSetup.cs:85-93` (it fails the Built-in build) | SRP foveation (Worldfall, Fractarium) | Low/Med/High = 6.5/11.5/21% (Meta, V) |
| Latency optimisation | `m_latencyOptimization: 0` (PrioritizeRendering) — `:168` | PrioritizeInputPolling (Worldfall RESEARCH:33) | Meta recommends input polling |
| Stereo | `m_renderMode: 1` SinglePassInstanced — `:167`, `XrSetup.cs:276` | same | same; on Quest Vulkan this runs as **multiview** (§11.3) |
| Graphics API | Vulkan only — `ServerBuild.cs:131-132` | same | same |

Also noticed, not investigated further: empty directories `unity/Assets/XR 1`, `XR 2`, `XR 3` next
to `unity/Assets/XR` (VERIFIED they contain no non-`.meta` files at depth <= 3). Harmless as far as I can see,
but the project already has a documented history of duplicated XR settings assets causing silent
build faults (CLAUDE.md "Do not copy another project's XR settings YAML").

### 2.2 The 1.7.0 grid report: one concrete mechanism (INFERRED)

User, verbatim, on 1.7.0: *"current apk renders like 100 plus isntances of earth , issue with
something aI see everyghin so manh times repeated ona grid flat"*.

**Mechanism.** On a tiled GPU, a render pass whose colour attachment is not loaded starts each tile
with whatever the previous tile left in tile memory (Vulkan `LOAD_OP_DONT_CARE`: "contents are
initially garbage", Android developers / Vulkan guide, §14). If the pass then draws only a little,
content from one tile is carried into every following tile at the same tile-local position: the
same fragment of the image, repeated on the tile grid, identical in both eyes (so it looks flat).

**Why CosmoSynth 1.7.0 could produce exactly that:**

1. Camera stacking since 1.5.0: `DepthBands` renders the far band, then the near camera starts a
   second pass that clears depth only and is expected to keep the colour (`DepthBands.cs:76-78`).
   (VERIFIED, and `docs/feedback.md` dates DepthBands to 1.5.0.)
2. Optimize Buffer Discards is on (`OpenXR Package Settings.asset:174`, VERIFIED). Unity's Oculus
   plugin documents it as: "the depth buffer contents will be discarded rather than resolved and the
   **MSAA color buffer will be resolved rather than stored** after rendering. This optimization can
   potentially break rendering effects that sample from the depth buffer, such as camera stacking"
   (VERIFIED, §14). The OpenXR Meta Quest feature describes the same switch as making "4x MSAA
   textures memoryless on Vulkan" (VERIFIED).
3. 1.7.0 turned MSAA on; 1.7.2 turned it off again (`docs/roadmap-universe.md`, VERIFIED).

With MSAA off (1.5.0, 1.6.0, 1.7.2+), the colour target is the ordinary eye texture, stored after
pass 1 and loadable by pass 2. With MSAA on and memoryless, the multisampled colour of pass 1 no
longer exists when pass 2 begins — so pass 2 would start on garbage. Tile count fits the words
"100 plus": Meta states 4x MSAA + 32-bit colour + D24S8 gives **96 x 176 px tiles** on XR2 Gen 2
(VERIFIED), and a 1680 x 1760 eye is 2,956,800 px / 16,896 px per tile = **175 tiles per eye**.

**Evidence against:** Unity closed as won't-fix an issue where, on Built-in + Vulkan with Depth
Submission 16/24-bit, "the underlying render target texture has a sample count of 1" — MSAA
requested but silently not applied (search snippet of the tracker entry, §14; Quest 2). If the
same happened on Quest 3 in 1.7.0, step 3 never took effect and the mechanism fails.

**What decides it:** one headset look at 1.9.0 (MSAA 0). Its status card prints
`render: <mode> <W>x<H>:<volumeDepth> msaa <requested>/<eyeSamples> cams <n>`
(`RenderDiagnostics.cs:39-56`, VERIFIED). No grid at `msaa 0/1 cams 2` supports the mechanism.
**Regardless of the answer, the recommended configuration (one camera, URP, depth submission None)
removes every ingredient.**

---

## 3. Q1 — camera and depth strategy

### 3.1 What depth format the Quest eye buffer actually has (VERIFIED)

- Unity's OpenXR provider enumerates the XR depth texture formats in
  `com.unity.xr.openxr@*/Runtime/OpenXRUnityAPI.cs:105-136`:
  `kUnityXRDepthTextureFormat24bitOrGreater` — "Unity prefers 32 bit floating point Z buffer if
  available ... DX11: D32_FLOAT_S8X24 ... **Vulkan: VK_FORMAT_D24_UNORM_S8_UINT**";
  `kUnityXRDepthTextureFormat16bit` — "**Vulkan: VK_FORMAT_D16_UNORM**".
- Unity's OpenXR manual: Depth 16 bit = "shared depth buffer using **16 bits per pixel**"; Depth 24
  bit = "shared depth buffer using 24 bits per pixel" (project-configuration page). The tooltip in
  `OpenXRRenderSettings.cs:221-225`: "resolves or submits depth to OpenXR runtime".
- URP, when rendering straight to the XR eye texture, imports the XR target's own depth:
  `UniversalRendererRenderGraph.cs:467-473` (`targetDepthId = cameraData.xr.renderTarget`) and
  `:1699-1708` (`importInfoDepth.format = cameraData.xr.renderTargetDesc.depthStencilFormat`). URP's
  own "Depth Attachment Format" (default on Android `D24_UNorm_S8_UInt`,
  `UniversalRendererData.cs:62`) only applies to an intermediate texture.
- Unity **does** use reversed-Z on Vulkan: `HLSLSupport.cginc:670-672` defines `UNITY_REVERSED_Z 1`
  for `SHADER_API_VULKAN`. (The manual page "Write HLSL for different graphics APIs" lists only
  DX11/DX12/Metal as reversed — the shipped include file is the authority.)

**So:** CosmoSynth today renders into **D16_UNORM** (Depth16Bit). With submission None or 24-bit it
would be **D24_UNORM_S8_UINT**. There is no float depth on the direct-to-eye path (INFERRED from the
enum mapping plus the URP import; confirm at runtime by logging
`XRSettings.eyeTextureDesc.depthStencilFormat`).

**What that implies.** Reversed-Z only helps a *float* depth buffer; with a UNORM buffer "reversed-Z
with an integer depth buffer is as good as any of the other integer options" (NVIDIA, VERIFIED).
The comment in `CS/Core/Scale/ScaleSpace.cs:155-156` — "a reversed floating-point depth buffer
handles comfortably" — describes a buffer the Quest does not have.

### 3.2 Precision arithmetic (INFERRED, exact for a UNORM buffer)

For a perspective depth d = n/z (reversed, far -> infinity) or d = 1 - n/z, |dd/dz| = n/z^2, so
one quantisation step 2^-N corresponds to

    dz = z^2 / (n * 2^N)        relative:  dz/z = z / (n * 2^N)

With n = 0.05 m (`DepthBands.cs:41`; hands and wrist panels need it):

| z | D24: dz | D24: dz/z | D16: dz | D16: dz/z |
|---|---|---|---|---|
| 1 m | 1.2 um | 1.2e-6 | 0.31 mm | 3.1e-4 |
| 10 m | 0.12 mm | 1.2e-5 | 3.1 cm | 3.1e-3 |
| 100 m | 1.2 cm | 1.2e-4 | 3.1 m | 3.1e-2 |
| 1 km | 1.19 m | 1.2e-3 | 305 m | 0.31 |
| 10 km | 119 m | 1.2e-2 | 30 km | 3.1 |

Usable range for 0.1% relative resolution: D24 to **839 m**, D16 to **3.3 m**; for 1%: D24 to
8.4 km, D16 to 33 m. `DepthBands` assumes "a 24-bit buffer holds about 1e5 usefully"
(`DepthBands.cs:34-38`) — right for D24 at ~1%, but the app runs on D16, where each band holds
~650:1 at 1%. Worked example with D16 and `FitTo(5e5)`: split = sqrt(0.05 x 5e5) = 158 m; the near
band's resolution at 158 m is 4.8%, the far band's (near plane 142 m) at 5e5 m is 5.4%.

### 3.3 Options compared

| Option | Verdict | Why (numbers) |
|---|---|---|
| **Multiple cameras / depth bands** (current) | Reject | Each extra camera is an extra render pass: Meta's ovrgpuprofiler example shows StoreColor 1.525 ms, LoadColor 0.71 ms, Store/LoadDepthStencil 0.87/0.83 ms in a 10.62 ms frame (V, advanced GPU pipelines page). Doubles culling. Suspected cause of the 1.7.0 grid (§2.2). A URP overlay camera on Quest 2 dropped a 90 fps app to 68-75 fps (Unity forum search snippet, I). |
| **Reversed-Z float depth** | Not available | Eye buffer is UNORM (§3.1). An intermediate D32F target in URP needs an intermediate colour texture and a final blit — a full-screen store + load per frame. |
| **Logarithmic depth written in the shader** (Outerra) | Reject | Needs `SV_Depth` in the fragment shader for correctness on large near triangles; "writing to gl_FragDepth disables early-z" (Outerra, V) — on Adreno that also disables LRZ. The vertex-only form is wrong on long polygons near the camera (V). |
| **Scaled space with log distance compression** | Use, per group | Exact angular size and centre depth order; a similarity transform about the eye keeps everything inside a group exact. Needs group-level (not per-body) scaling — §3.4 shows the failure otherwise. |
| **Painter's algorithm for the sky** | Use | Additive layers are order-independent; opaque discs over stars only need a CPU sort of a few hundred bodies. Proven: GeoramaXR draws its whole far world with `ZTest Always ZWrite Off` and render-queue order (`CONTRACT-XR.md` §3, V) and was worn on a headset. |

### 3.4 Recommended design: one camera, three layers

**Near layer** — depth-tested, true or user scale: hands, panels, the focused body or system at
tabletop/room scale, landing terrain. Camera near 0.05 m, far 1000-2000 m, D24. Resolution 1.2 cm
at 100 m, 1.2 m at 1 km (table above).

**Sky layer** — everything that is effectively at infinity: CMB, galaxies, stars as seen from the
viewer, bodies pinned to the sky (CosmoSynth already pins bodies past the horizon to a sphere at
900 units, `SkyAndRings.cs:29`). Rules:

- Drawn first (queue <= Geometry-500), `ZWrite Off`; either `ZTest Always` or `LEqual` against the
  cleared buffer — both pass. Opaque near-layer geometry drawn later overwrites it; no depth range
  is involved at all.
- Placed **relative to each eye**: `posWS = eyeWS + dirWS * R_sky` with `eyeWS =
  UNITY_MATRIX_I_V._m03_m13_m23` in the vertex shader (per-eye under multiview, §11.3), R_sky
  inside the far plane (e.g. 800 m). Each eye then sees the object at its own infinity: zero
  binocular disparity and zero head-motion parallax, which is physically right for anything beyond
  ~100 m. Why ~100 m: one eye-buffer pixel at ~21 px/deg is 2.9 arcmin = 8.3e-4 rad, and a 63 mm
  IPD falls below one pixel of disparity beyond 0.063 / 8.3e-4 = **76 m** (INFERRED; ~21 px/deg is
  Fractarium's figure, `QualityGovernor.cs:53-55`; Quest 3 panels are 25 PPD, search snippet).
- Order inside the layer: CMB -> large-scale structure -> Local Group -> Milky Way dust -> stars ->
  pinned bodies (opaque/alpha discs sorted back to front on the CPU by true distance).

**Compressed layer** (optional, for big structures you fly *through*: the Milky Way disc, the dust
cube, the Local Group). Map true eye distance d (metres at the current user scale) to rendered
distance

    f(d) = d                       for d <= d0
    f(d) = d0 * (1 + ln(d / d0))   for d >  d0      (continuous in value and slope at d0)
    s(d) = f(d) / d                uniform scale applied to the object about its own centre

Angular radius is preserved exactly: atan(R s / (d s)) = atan(R/d). Centre depth order is preserved
because f is monotonic. Each decade of true distance costs d0 ln 10 of rendered distance; with
d0 = 10 m: 1e3 m -> 56 m, 1e6 -> 125 m, 1e9 -> 194 m, 1e12 -> 263 m, 1e18 -> 401 m, 1e24 -> 540 m,
**1e27 m -> 609 m** — the whole ladder fits inside a 1 km far plane (INFERRED, arithmetic).

**The trap — scale per group, never per body (INFERRED, worked example, d0 = 10):** a planet R =
100 at d = 1000 and a moon R = 10 at d = 850 (moon truly in front: its back at 860 < the planet's
front at 900). Compressed: planet centre 56.05, s = 0.0561, front surface at 50.45; moon centre
f(850) = 54.43. The moon's centre is now *behind* the planet's front surface, so the depth test hides
a moon that is transiting the planet. Fix: give a whole system one scale s_g = f(D_g)/D_g about the
eye, i.e. `p' = eye + (p - eye) * s_g` — a similarity transform centred on the eye, which preserves
every angle and every depth order inside the group exactly. Between groups, draw far to near.
Depth resolution in the compressed layer at 600 m (D24) is 0.43 m = 4.3% of a d0 = 10 m log-decade
unit, so distinct groups are separable when their true distances differ by more than ~0.5-4%.

**Precision of positions** (unchanged principle from `docs/architecture.md`): everything is
camera-relative (floating origin); for big static point meshes store positions relative to a chunk
origin in float and pass the eye position relative to that origin, computed in double on the CPU,
as a uniform (relative-to-eye rendering).

### 3.5 Settings to change with this design

Depth Submission **None**; delete `DepthBands` (and the `IFloatingOriginListener` still applies);
single camera near 0.05, far ~1500; MSAA 4x (profile 2x) only once there is one camera; Symmetric
Projection on; Optimize Buffer Discards on (safe with one camera); Latency Optimization
PrioritizeInputPolling. Correct `ScaleSpace.cs:155` and `DepthBands.cs:34-38` comments when the code
changes, since both state a buffer the device does not have.

---

## 4. Q2 — 100k-500k stars and ~50k galaxies in one draw

### 4.1 Options

| Option | Verdict | Evidence |
|---|---|---|
| **Static mesh, 4 verts/point, billboard in the vertex shader** | **Use** | GeoramaXR `GeoStar.shader` (view-space metric corner offset, URP), BoltGraphXR `BoltPoints.shader` (clip-space pixel offset: `ofs = corner * px * 2 / _ScreenParams.xy * positionCS.w`), Chemispace `ChemPoints.shader` (Built-in, 36,654 molecules in one mesh, filters as uniforms). All three shipped APKs that were worn (V for the shaders; headset status from memory notes, I). |
| GPU instancing (`Graphics.RenderMeshInstanced`) | Reject | <= 1023 instances per call, 511 with worldToObject (Worldfall RESEARCH:236, verified there) -> 117 calls for 119,613 stars. |
| Procedural / indirect draw from a `GraphicsBuffer` (`Graphics.RenderPrimitives[Indirect]`) | Reserve | Unresolved report of `StructuredBuffer` vertex fetch rendering nothing on Quest 2 Vulkan SPI (Unity discussions, V); vertex-shader buffer/texture fetch pushes Adreno out of binning mode (Worldfall RESEARCH:97, "likely"). Instance-count rule: the manual says "for indirect draw calls, you must manually double the instance count ... to support single-pass instancing" (V) — but on Quest the same setting runs **multiview**, where `UnitySetupInstanceID` does **not** halve the instance ID (`UnityInstancing.cginc:171-190`, only under `UNITY_STEREO_INSTANCING_ENABLED`), so a doubled count draws every instance twice — double brightness for additive stars (INFERRED from the include code). Only worth it if GPU-side culling becomes necessary; check `SystemInfo.maxComputeBufferInputsVertex > 0` at runtime. |
| `MeshTopology.Points` / point size | Reject | Fixed 1 px; the GeoramaXR shader header records "point sprites are unreliable on Vulkan/Android" (V, comment). A one-pixel feature has no shape (`cosmosynth-small-things-need-two-bounds`). |

### 4.2 Recommended vertex format and shader work

Per vertex (all four corners of a point carry the same data):

| Attribute | Format | Bytes | Content |
|---|---|---|---|
| POSITION | Float32x3 | 12 | position in parsecs relative to the chunk origin (sky-only mode: unit direction) |
| COLOR | UNorm8x4 | 4 | colour (from B-V / T_eff), **already linear** if the project is Linear (§4.5) |
| TEXCOORD0 | Float16x2 | 4 | x = absolute magnitude, y = corner index 0..3 (or derive the corner from `SV_VertexID & 3` and drop it) |

20 B/vertex (Meta advises dropping unused channels and using half precision for non-position data,
performance-targets page, V). Vertex shader: apparent magnitude `m = M + 5 log10(d / 10 pc)` from the
eye-relative distance (so brightness is right while flying), flux `10^(-0.4 (m - m_lim))`, colour
scaled by `min(flux, 1)`, size in pixels grown only for `flux > 1`, clamped to [min_px, max_px].
This is CosmoSynth's own hard-won rule: brightness in RGB, a size floor so the profile has pixels to
resolve in, extra geometry (halo, spikes) only for the bright end, and a maximum as well as a
minimum (`cosmosynth-small-things-need-two-bounds.md`, V). Index buffer `UInt32` (already used,
`SkyAndRings.cs:419`). Mesh bounds set large so Unity never frustum-culls the sky mesh.

A proposed URP star shader (PROPOSED, **not compiled**) is in §11.4.

### 4.3 Budgets (INFERRED arithmetic from VERIFIED counts)

| Population | Count | Vertices | Indices (UInt32) | GPU memory |
|---|---|---|---|---|
| HYG stars (shipped 1.8.0+) | 119,613 | 478,452 | 717,678 | 9.6 MB + 2.9 MB |
| Gaia-scale subset | 500,000 | 2,000,000 | 3,000,000 | 40 MB + 12 MB |
| 2MRS galaxies (§7) | 43,533 | 174,132 | 261,198 | ~6.6 MB (38 B/vert) |
| Dust voxel sprites (§5) | 73,756 | 295,024 | 442,536 | ~7.7 MB |

Meta's Quest 3 targets are 200-300 draw calls for a busy app and **1.3-1.8 M triangles per frame**
(V). Two triangles per point: 120k stars = 240k triangles (13-18% of the triangle target); 500k =
1 M triangles — too much to draw every frame, so split into magnitude bands (submeshes) and draw a
faint band only when the view can show it, and/or split the sky into ~96 tiles (6 cube faces x 4x4)
so Unity's CPU frustum culling drops the ~79% of the sphere outside the view. Budget: **<= ~0.6 M
point vertices processed per frame** across stars + galaxies + dust sprites. Multiview renders both
eyes from one draw; Meta says it "primarily reduces CPU usage, and the GPU performance is unchanged
largely" (Worldfall quest-render.md, verified there), so vertex work is paid per eye.

**Overdraw.** Quest 3 FOV ~110 x 96 deg is a solid angle of 4 asin(sin 55 sin 48) = 2.62 sr = 21% of
the sphere, so ~25,000 of the 119,613 stars are in view (uniform-sky approximation). At a 4 px quad
(16 px^2) that is 0.40 M fragments = 14% of one 1680x1760 eye; at 6 px, 30%; plus halos for the
brightest ~2% (500 x 32^2 px = 17%). Budget: **point sprites <= 0.5 eye-screens of fragments**,
everything transparent together <= ~3.

### 4.4 Stereo and MR rules for sprites

- View-space (GeoStar) and clip-space (BoltPoints) expansion are both per-eye correct under
  multiview, because `UNITY_MATRIX_V`/`VP` resolve per `gl_ViewID` in the vertex stage
  (`UnityInput.hlsl:210`, V).
- MR: additive colour that must not touch destination alpha — `Blend One One, Zero One`
  (CosmoSynth `CosmoRim.shader:32`, NeuroWeave `NwTract`; V). Quest's compositor reads alpha as
  coverage (CLAUDE.md, measured 2026-07-31).
- `clip()` in a `ZWrite Off` additive pass buys nothing and costs a discard; drop it (INFERRED).

### 4.5 The sRGB vertex colour trap (V)

In Linear colour space Unity does not convert vertex colours; BoltGraphXR decodes in the fragment
(`pow(abs(col.rgb), 2.2)`, `BoltPoints.shader`) and the web twin needed the same fix. CosmoSynth is in
**Gamma** today (§2.1), so the trap is latent: the day the project goes Linear (which OpenXR
compositing and every sibling use), every vertex colour and every hand-picked `CosmoTheme` colour
will wash out unless converted (`Color.linear` on the CPU, or decode in the shader).

---

## 5. Q3 — volumetric dust (201 x 201 x 81)

### 5.1 The data, measured (VERIFIED, run on triton)

`dust.bin` is 3,272,481 bytes = 201 x 201 x 81 uint8, log-encoded (`density = vmin *
(vmax/vmin)^(v/255)`, vmin 0.02, vmax 79.81), Sun-centred Galactic Cartesian, 2000 x 2000 x 800 pc
(10 pc voxels); source "Leike & Ensslin / Lallement" (`dust_meta.json`, V). The web page marches it
on a back-face box with `uSteps = 110`, `uCut = 0.22`, alpha early-out at 0.985 (`index.html:196-255`,
V).

| Measure | Result |
|---|---|
| non-zero voxels | 2,574,895 (78.7%) |
| v/255 > 0.10 / 0.22 / 0.30 / 0.40 / 0.50 | 52.0% / **28.6%** / 17.9% / 8.6% / 3.4% |
| 8^3 bricks occupied at the web cut 0.22 | 3,969 of 7,436 (**53%**) — empty-space skipping gains < 2x |
| 4^3 / 16^3 bricks occupied | 41% / 67% |
| 2x downsample (101x101x41, 20 pc cells), mean above 0.22 / 0.30 / 0.40 / 0.50 | 119,207 / **73,756** / 35,557 / 13,480 cells |
| 4x downsample (51x51x21, 40 pc cells), same cuts | 15,232 / 9,411 / 4,421 / 1,485 cells |

As a texture: R8 3D = **3.27 MB** uncompressed. No compression needed.

### 5.2 Cost model (measured anchor + inference)

The only device-measured raymarch number on this box: Fractarium's governor spends a budget of
**20 step-coverages per frame = 6.85 ms / 0.338 ms per step-coverage** on Quest 3
(`QualityGovernor.cs:253-260`, V: "derived from a single 12%-coverage device measurement taken while
foveation was off, so it is an upper bound on cost"). One step-coverage = one march step over the
full area of both eyes. That step is a Mandelbulb distance estimate; a dust step (one trilinear 3D
fetch + a few ALU) is cheaper by an unmeasured factor — assume 3-10x (INFERRED), i.e. kappa_dust ~
0.03-0.1 ms per step-coverage until measured.

    cost_ms ~= N_steps * coverage * kappa        (coverage = fraction of both eyes the proxy covers)

| Situation | Coverage | 110 steps | Budgeted |
|---|---|---|---|
| Tabletop, 2 kpc cube 0.5 m wide at 1 m (~590 x 240 px) | ~0.048 | 5.3 step-cov -> 0.16-0.53 ms (<= 1.8 ms at the Mandelbulb upper bound) | full 110 steps affordable |
| Camera inside the cube (the Sun is at its centre) | 1.0 | 110 step-cov -> 3.3-11 ms | **N = clamp(B / coverage, 16, 64)** with B ~ 30 -> 30 steps ~ 0.9-3 ms |

Other published anchors: no shipped Quest volumetric numbers exist; one mobile-VR figure is 16-32
steps at 50% resolution = 3-5 ms (Worldfall RESEARCH:231, "verified thread / likely blog").

### 5.3 Options

| Option | Cost | Quality | Verdict |
|---|---|---|---|
| Full-res march on the box proxy, fixed 110 steps | 3-11 ms inside (I) | Web-identical | Only from outside |
| **Full-res march with a step-coverage budget** (Fractarium pattern) + blue-noise jitter + alpha early-out + 8^3 brick skip grid (26x26x11 R8, 7.4 KB) | bounded by B; ~1-3 ms inside (I) | jitter noise inside at 25-50 pc steps | **Use** |
| Half/quarter-res offscreen march + upsample | 1/4 or 1/16 of the fragments + one extra pass (store ~1/4 of a full store) | soft edges | URP only, later, if measured necessary. On Built-in it means a stereo RT array plus a CommandBuffer — fragile. |
| **Voxel point cloud** (sprites per cell) | vertices, not steps: 2x-down cut 0.30 = 295k verts | good from outside; overdraw explodes when inside | **Tabletop LOD.** At tabletop scale (20 pc = 5 mm at 1 m, ~6 px) 73,756 sprites x 36 px^2 = 2.65 M fragments per eye ~0.9 screens, concentrated on the cube (I). Clamp max sprite px and fade sprites within ~50 pc of the eye. |
| Axis-aligned slice stack (81 textured quads) | 81 x coverage layers of blending | view-angle artefacts edge-on | Reject (fill-rate ~ N steps with worse quality) |
| Baked 2D: face-on column-density map (201x201) and an all-sky panorama from the Sun (cubemap 6x256^2) | 1-2 samples/pixel | no parallax; panorama valid only within ~tens of pc of the Sun (I) | Far LOD (cube < ~200 px) and a cheap "view from Earth" |

NeuroWeaveXR proves the plumbing: URP `Texture3D` sampling on Quest with a per-frame world->volume
matrix (`NwSlice.shader`, V; app accepted at ~30 fps on device for other reasons, `FEEDBACK-NOW.md`).

**Recommendation.** Outside the cube: march with up to 110 steps (coverage is small). Inside:
step-coverage budget B ~ 30 with jitter, N_min 16. Tabletop/galaxy scale: voxel sprites. Measure
kappa_dust on device first with a Fractarium-style `cost_probe` before committing to B.

---

## 6. Q4 — the CMB as an inside-out sphere

**Data (VERIFIED):** Planck 2018 CMB maps (SMICA among them) are HEALPix Nside = 2048 (12 x 2048^2 =
50,331,648 pixels of 1.72 arcmin) at ~5 arcmin FWHM (Planck Legacy Archive wiki). The field is a
scalar temperature; store it as one channel and apply the colour map in the shader.

**Resolution (INFERRED arithmetic).** A cube face of N texels spans 90 deg; at the face centre one
texel is 2/N rad. N = 2048 -> 3.36 arcmin (17.9 texels/deg) at face centre, 1.68 arcmin at edge
midpoints — about 1.5 texels per 5 arcmin beam at worst, matching the ~2.9 arcmin eye-buffer pixel.
N = 1024 (6.7 arcmin) is one texel per beam — slightly softer than the data; N = 4096 adds nothing
the beam contains.

| Layout | Texels | EAC_R11 (4 bpp) | ASTC 6x6 (3.56 bpp) | ASTC 4x4 (8 bpp) | RGBA8 |
|---|---|---|---|---|---|
| Cube 6 x 1024^2 | 6.29 M | 3.1 MB (4.2 with mips) | 2.8 (3.7) | 6.3 (8.4) | 25 (34) |
| **Cube 6 x 2048^2** | 25.2 M | **12.6 MB (16.8)** | 11.2 (14.9) | 25.2 (33.6) | 101 (134) |
| Cube 6 x 4096^2 | 101 M | 50 (67) | 45 (60) | 101 (134) | 403 (537) |
| Equirect 8192 x 4096 | 33.6 M | 16.8 (22.4) | 14.9 (19.9) | 33.6 (44.7) | 134 (179) |

**Choices:**
- **Cubemap, not equirect**: hardware-seamless, no pole oversampling, no `atan2` seam in the mip
  selection (an equirect needs `tex2Dgrad` or a manual LOD at the seam).
- **Single channel**: EAC_R11 (11-bit, ~0.5 uK steps over a +-500 uK window) or ASTC 6x6; both are
  Quest-native (INFERRED; ETC2/EAC and ASTC LDR are baseline on Adreno Vulkan). Colour through a
  256x1 LUT (Planck-style map) — also lets the UI switch maps honestly.
- **Draw** in the sky layer first: a low-poly inverted sphere (~1-4k triangles) or a full-screen
  pass, sampling `texCUBE(normalize(posWS - eyeWS))` — 1 texture + 1 LUT sample per pixel. In
  passthrough use premultiplied alpha as an "immersion" dial; at alpha 1 it replaces the room, which
  is the point of the CMB stage.
- Offline conversion: healpy on triton (`get_interp_val` per cube texel direction), same pipeline
  pattern as `tools/cosmodata` (stdlib-only rule would need an exception for healpy, like the
  existing `npx` one). Licensing/attribution is the data agent's topic.

---

## 7. Q5 — galaxy impostors with measured ellipticity and position angle

### 7.1 Fields (VERIFIED)

- **2MRS** (Huchra et al. 2012, ApJS 199, 26; arXiv:1108.0669, PDF read): 44,599 galaxies selected
  at Ks <= 11.75, redshifts for 43,533. Table 3 has 29 columns including `r_iso` (col 19, log10 of
  the Ks = 20 mag/arcsec^2 isophotal radius in arcsec), `r_ext` (col 20), **`b/a` (col 21, "axial
  ratio from co-added JHKs images [sup_ba]")**, type (col 23, modified Hubble T: ellipticals -7..-5,
  S0 -4..0, spirals 1..9, irregular >= 10), v (col 25, km/s). **Position angle is not in 2MRS
  Table 3** — join the 2MASS XSC by designation.
- **2MASS XSC** (IPAC column definitions): `sup_ba` "minor/major axis ratio fit to 3-sig.
  super-coadd isophote"; **`sup_phi` "super-coadd angle to major axis (E of N), quantized to 5 deg"**
  (2 deg for Large Galaxy Atlas galaxies). So orientation is known to +-2.5 deg.
- Where a catalogue gives ellipticity instead (e.g. Local Group dwarfs), q = 1 - epsilon.

### 7.2 From (q, PA) to a 3D disc (INFERRED geometry; formula VERIFIED as Hubble 1926)

Inclination (Hubble 1926, q0 = intrinsic thickness; Holmberg's q0 = 0.20; 0.30 for Sa down to 0.16
for Sd, 0.42 for Sdm-Im — A&A 2001 comparison page, search result):

    cos^2 i = (q^2 - q0^2) / (1 - q0^2),   i = 90 deg if q <= q0

On the sky at (alpha, delta), line of sight r (unit, from observer to galaxy):

    north n = (-sin d cos a, -sin d sin a, cos d)      east e = (-sin a, cos a, 0)      (ICRF)
    major axis on the sky   m = cos(PA) n + sin(PA) e
    minor axis on the sky   k = -sin(PA) n + cos(PA) e
    disc normal             c = cos(i) r + s sin(i) k,   s = +-1 (unmeasurable; pick by hash of
                                                          the designation, label "inferred")
    in-disc axes            u = m,  v = normalize(cross(c, m))
    quad corners            centre + R (+-u +- v),  R = theta_ext * D  (r_ext in arcsec -> rad x distance)

From Earth this reproduces the catalogue ellipse exactly; from anywhere else it is a plausible disc,
flagged as inferred. Convert ICRF -> Unity with `CelestialBodyView.ToUnityDirection` (handedness
trap, CLAUDE.md).

### 7.3 Rendering

- One quad per galaxy in a static mesh like §4 (centre float3, `u` and `c` as SNorm8x4, R half,
  type/colour UNorm8x4; ~38 B/vertex). Below ~3 px: the point-sprite path. Above: sample a
  template by T-type (E / S0 / Sa-b / Sc-d / Irr) from one atlas (e.g. 5 x 128^2 in a 1024 x 256
  ASTC 6x6 texture, ~0.1 MB).
- Edge-on guard: when `|dot(c, viewDir)| < ~0.2` the quad degenerates to a line; cross-fade to a
  camera-facing ellipse of axis ratio q0 so edge-on spirals stay visible (INFERRED).
- Ellipticals: camera-facing ellipse with the catalogue q and PA (they are triaxial; no disc).
- Budget: 43,533 galaxies = 174,132 vertices, 261,198 indices, ~6.6 MB; ~9,000 in view; at 3-4 px
  ~5% of an eye in fragments.
- A handful of hero galaxies (M31, M33, Magellanic Clouds) can use real imagery on the same oriented
  quad; licensing is the data agent's topic.

---

## 8. Q6 — orbit lines and grids

**What exists.** CosmoSynth draws each orbit with a `LineRenderer` in `LineAlignment.View`
(`OrbitLineRenderer.cs:42-88`, V): one draw call per orbit and CPU-generated geometry. GeoramaXR,
NeuroWeaveXR, BoltGraphXR and Chemispace use `MeshTopology.Lines` (grep, V) — 1-pixel hardware
lines, accepted on device but aliased; NeuroWeaveXR's dense additive tracts ran ~30 fps (cause not
isolated, `FEEDBACK-NOW.md`).

| Option | AA without MSAA | Width control | Cost | Verdict |
|---|---|---|---|---|
| `MeshTopology.Lines/LineStrip` | none (MSAA only) | fixed 1 px | cheapest | Reject for orbits: after the compositor's lens resampling a 1-px line is thinner than a display pixel and crawls with head motion (INFERRED) |
| `LineRenderer` (View) | none | yes | CPU rebuild per camera move, 1 draw per line | Replace |
| **Ribbon expanded in the vertex shader** (4 verts/segment: both endpoints + side +-1) | analytic in the fragment | constant pixels | static mesh, 1-3 draws total | **Use** |

Ribbon recipe (INFERRED, standard): project both endpoints; screen-space direction
`dir = normalize((p1.xy/p1.w - p0.xy/p0.w) * _ScreenParams.xy)`; normal `nrm = (-dir.y, dir.x)`;
offset each vertex by `nrm * side * (w_px/2 + 1) * 2 / _ScreenParams.xy * clip.w`; pass
`v = side * (w_px/2 + 1)` in pixel units; fragment `alpha = saturate(w_px/2 + 0.5 - abs(v))`. For
w_px < 1 draw at 1 px and multiply alpha by w_px (lines fade instead of breaking up). Cull segments
with an endpoint behind the eye (orbits are only drawn from outside anyway — CLAUDE.md orbit rule).
Blend `One One, Zero One` in MR.

Numbers: segments per orbit `N = clamp(2 pi r_px / 10 px, 32, 512)`; 300 orbits x 256 segments = 76,800
segments = 153,600 triangles, 307,200 vertices. Moons' orbits follow their parent by indexing a
small uniform array of parent positions with a per-vertex orbit id, so all orbits are one draw
(INFERRED). Width 1.5-2.5 px.

**Grids** (RA/Dec, galactic l/b, ecliptic, distance rings): procedural in the fragment shader on the
sky sphere or a plane with `fwidth`-based line width and the "pristine grid" fade to average grey
at distance, which removes moire (Ben Golus, V). One draw, analytic AA, no MSAA needed. On a sphere
compute (lon, lat) from the view direction and take `fwidth` of the wrapped longitude to avoid the
seam; thin lines out near the poles.

---

## 9. Q7 — text: legacy TextMesh vs TextMeshPro SDF

**CosmoSynth today (V):** legacy `TextMesh` with the built-in font
`Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` (fallback `Arial.ttf`,
`CosmoText.cs:50-52`), shader `GUI/Text Shader` listed in Always Included (`CosmoShaders.cs:43,49-59`).
Built-in resources always ship, so this path is immune to the font-not-in-build trap. Its cost is
quality: a bitmap atlas rasterised at `FontSize = 64` (`CosmoText.cs:37`) blurs when magnified,
shimmers when minified, and has no outline for legibility over passthrough. The app's angular-size
discipline (dmm, measure-then-lay-out) is independent of the text backend and should be kept.

**The trap (V, memory `unity-headless-text-fonts`):** a `TMP_Text` whose font reference is not
serialised renders in the editor (fallback to an on-disk default) and renders **nothing** on
device; Verdara shipped blank panels this way; legacy TextMesh has the same failure with no font
assigned.

**The safe TMP path (V, GeoramaXR):** TMP Essentials put `TMP Settings.asset` and
`Fonts & Materials/LiberationSans SDF.asset` under `Assets/TextMesh Pro/Resources/` — a `Resources`
folder, so they always ship. Code takes the font from `TMP_Settings.defaultFontAsset`
(`GeoramaXR/Assets/Georama/Runtime/GeoPanel.cs:105-107`, `SkyDome.cs:112-115`), logging an error if
null. TMP's `TMP_SDF-Mobile.shader` carries the stereo macros (`UNITY_VERTEX_INPUT_INSTANCE_ID`,
`UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`,
lines 98-132 in GeoramaXR's copy). CosmoSynth has **no** `Assets/TextMesh Pro` today; com.unity.ugui
2.5.0 ships `Package Resources/TMP Essential Resources.unitypackage` (V), importable headlessly with
`AssetDatabase.ImportPackage(path, false)`; the menu method is `TMP_PackageUtilities.ImportProjectResourcesMenu`
(`TMP_PackageUtilities.cs:1028`).

**Recommendation:** TMP SDF, world-space `TextMeshPro` for labels (no Canvas/EventSystem needed).
Gates: (1) `unzip -p <apk> 'assets/bin/Data/*' | strings | grep -c "LiberationSans SDF"` > 0;
(2) a runtime assert that a label produced glyphs (`textInfo.characterCount > 0`); (3) on a null
font draw a legacy TextMesh sign naming the cause. **Glyph coverage:** star names need Greek
(Bayer letters) and symbols (deg, arcmin, arcsec, sun/earth symbols); Chemispace found Unicode
subscripts missing from the Android built-in font (memory note). Build a static SDF atlas with the
needed character set, or ship the source TTF for dynamic fallback, and render a test string in the
play-mode capture. Keep central, high-contrast text in the fovea when FFR is on (Meta FFR page, V).

---

## 10. Q8 — URP vs Built-in for custom unlit additive content on Quest

| Aspect | Built-in (CosmoSynth today) | URP 17.5 (siblings) |
|---|---|---|
| Future | **Deprecated in Unity 6.5**; available at least through 6.7 LTS; supported until end of 2028 (Unity 2026 strategy page, V) | the pipeline Unity develops |
| Per-draw cost of an unlit additive shader | same shader work; no SRP Batcher | same shader work; SRP Batcher for CPU |
| Foveated rendering | SRP Foveation "does not support the Built-in Rendering Pipeline"; Legacy API needs the Meta Core XR SDK (Unity OpenXR 1.17 docs, V). CosmoSynth's build fails with the feature on (CLAUDE.md, `XrSetup.cs:85-93`) | SRP Foveation (FDM on Vulkan); Low/Med/High = 6.5/11.5/21% GPU in fill-bound apps (Meta, V); SRP mode 20-30% faster than Legacy (Worldfall RESEARCH:90) |
| MSAA 4x | works in principle; **won't-fix**: MSAA silently not applied on Built-in + Vulkan with depth submission 16/24 (issue tracker, search snippet, Quest 2) | 4x with memoryless buffers via Optimize Buffer Discards: saves ~90 MB per eye at 1680x1760 (Meta, V) |
| Render graph / on-tile | no | native render passes; Multiview Render Regions (Unity 6.1+, needs Symmetric Projection); on-tile post; AppSW — all URP/render-graph features (Worldfall RESEARCH §1.1) |
| Multi-camera | camera stacking = extra passes | overlay stacking also expensive; use one camera either way |
| Runtime-built scenes | shaders must be Always Included (CosmoSynth does this) | same, **plus** URP Unlit/Particles-Unlit transparent variants get stripped when configured at runtime -> write tiny single-variant custom shaders (memory `unity-quest3-passthrough-headless`, V) |
| Legacy shaders still in use | `Standard`, `Unlit/Color`, `Unlit/Transparent`, `Sprites/Default`, `Particles/Standard Unlit`, `GUI/Text Shader` (`CosmoShaders.cs:22-43`) | untagged built-in unlit passes can still draw via SRPDefaultUnlit (INFERRED) but are not SRP-Batcher compatible; replace with `CosmoSynth/*` URP shaders |

**Verdict: migrate to URP** in the same change that removes `DepthBands`. The migration surface is
small because the scene is built in code: `CosmoShaders.All` (8 names), the two custom shaders, and
every `Shader.Find` (CLAUDE.md names `SceneShot.cs` and `CelestialBodyView.cs`). Take the settings
from Worldfall's contract §2.6 / `WorldfallUrpConfig` (MSAA 4, HDR off, depth/opaque textures off,
post off, SRP batcher on) and Worldfall RESEARCH §1.1. Switch to **Linear** at the same time (§4.5).

---

## 11. Q9 — the stereo shader skeleton, verbatim

### 11.1 Built-in (CG) — Unity manual, verbatim (V)

From https://docs.unity3d.com/Manual/SinglePassInstancing.html ("Full sample shader code"):

```glsl
struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float2 uv : TEXCOORD0;
    float4 vertex : SV_POSITION;

    UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert (appdata v)
{
    v2f o;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;
    return o;
}

UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);

half4 frag (v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    half4 col = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.uv);

    col = 1 - col;

    return col;
}
```

The same page: "For indirect draw calls, you must manually double the instance count contained in
your compute buffer to support single-pass instancing" — see §4.1 for why that is wrong under
Quest multiview.

In-repo Built-in shader that ships today, verbatim (`CS/Runtime/Shaders/CosmoRim.shader:30-89`, V):

```glsl
        Pass
        {
            Blend One One, Zero One
            ZWrite Off
            Cull Front
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float4 _Color;
            float  _Power;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNrm : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNrm = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 view = normalize(_WorldSpaceCameraPos - i.worldPos);
                // abs, because these are back faces: the normal points away from the camera.
                float facing = saturate(abs(dot(normalize(i.worldNrm), view)));

                // 1 at the silhouette, falling to 0 as the surface turns to face the viewer.
                float rim = pow(1.0 - facing, _Power);

                // Alpha is the brightness dial, and it must not reach the framebuffer.
                return float4(_Color.rgb * (_Color.a * rim), 0.0);
            }
            ENDCG
        }
```

### 11.2 URP (HLSL) — Worldfall CONTRACT §4, verbatim (V, `Worldfall/docs/CONTRACT.md:334-382`)

```hlsl
Shader "Worldfall/Example" {
  Properties { _BaseMap ("Base", 2D) = "white" {} }
  SubShader {
    Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
    Pass {
      Name "ForwardLit" Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing
      #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
      #pragma multi_compile_fragment _ _SHADOWS_SOFT
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
      #include "WF_Atmosphere.hlsl"
      TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
      CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST; CBUFFER_END
      struct A { float3 pos:POSITION; float3 nrm:NORMAL; float4 col:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
      struct V { float4 pos:SV_POSITION; float3 wpos:TEXCOORD0; half3 nrm:TEXCOORD1; half4 col:COLOR;
                 half3 trans:TEXCOORD2; half3 insc:TEXCOORD3;
                 UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
      V vert (A v) {
        V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.wpos = TransformObjectToWorld(v.pos); o.pos = TransformWorldToHClip(o.wpos);
        o.nrm = TransformObjectToWorldNormal(v.nrm); o.col = v.col;
        WF_AerialPerspective(o.wpos, WF_EyePosWS(), o.trans, o.insc);
        return o;
      }
      half4 frag (V i) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        half3 albedo = ...; half3 lit = ...;                       // main light + WF_AmbientLight
        return half4(lit * i.trans + i.insc, 1);
      }
      ENDHLSL
    }
    Pass { Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" } /* URP shadow caster with the same macros */ }
  }
}
```

Worldfall's accompanying rules (same file, :380-382): eye position `UNITY_MATRIX_I_V._m03_m13_m23`;
additive passes `Blend One One, Zero One`; transparent MR-visible passes keep destination alpha;
opaque world geometry writes alpha 1.

The unlit/additive URP skeleton in production for points, verbatim
(`GeoramaXR/Assets/Georama/Shaders/GeoStar.shader`, V):

```hlsl
Shader "Georama/Star" {
  Properties { }
  SubShader {
    Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
    Pass {
      Blend SrcAlpha One          // THREE.AdditiveBlending = (SrcAlpha, One)
      ZWrite Off  ZTest Always  Cull Off
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      struct A {
        float3 pos       : POSITION;   // quad center (object space)
        float2 corner    : TEXCOORD0;  // (-1..1, -1..1)
        float2 sizeAlpha : TEXCOORD1;  // x = half size in meters, y = alpha
        float4 color     : COLOR;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };
      struct V {
        float4 pos    : SV_POSITION;
        float2 corner : TEXCOORD0;
        float3 color  : TEXCOORD1;
        float  alpha  : TEXCOORD2;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
      };

      V vert (A v) {
        V o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_TRANSFER_INSTANCE_ID(v, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        float3 ws = TransformObjectToWorld(v.pos);
        float3 vs = TransformWorldToView(ws);
        vs.xy += v.corner * v.sizeAlpha.x;     // camera-facing quad, per-eye view space
        o.pos = TransformWViewToHClip(vs);
        o.corner = v.corner;
        o.color = v.color.rgb;
        o.alpha = v.sizeAlpha.y;
        return o;
      }

      half4 frag (V i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        // web: a = alpha * smoothstep(1.0, 0.5, dist*2) over gl_PointCoord;
        // corner is already -1..1 so length(corner) is that dist*2.
        half a = i.alpha * smoothstep(1.0, 0.5, length(i.corner));
        clip(a - 0.004);
        return half4(i.color, a);
      }
      ENDHLSL
    }
  }
}
```

(Note: `Blend SrcAlpha One` also adds `a` into destination alpha, which in passthrough darkens the
room behind bright stars; CosmoSynth's `One One, Zero One` is the correct MR form.)

### 11.3 What the macros do on Quest (VERIFIED from the shipped include files)

- Keywords: `STEREO_MULTIVIEW_ON` on Vulkan -> `UNITY_STEREO_MULTIVIEW_ENABLED`
  (`HLSLSupport.cginc:22-23`; URP `UnityInput.hlsl:12-14`); `STEREO_INSTANCING_ON` ->
  `UNITY_STEREO_INSTANCING_ENABLED` (`HLSLSupport.cginc:26-27`). Either one defines
  `USING_STEREO_MATRICES` (`UnityInput.hlsl:16-18`, `UnityShaderVariables.cginc:~10-12`).
- Under `USING_STEREO_MATRICES`, `UNITY_MATRIX_V`, `VP`, `I_V`, `P` and **`_WorldSpaceCameraPos`**
  are arrays indexed by `unity_StereoEyeIndex` (`UnityInput.hlsl:20-34`,
  `UnityShaderVariables.cginc:~14-25`, the `_WorldSpaceCameraPos` define at :24).
- **Multiview:** in the vertex stage `unity_StereoEyeIndex` is `gl_ViewID` (`UnityInput.hlsl:209-211`,
  `UnityShaderVariables.cginc:190`); in the fragment stage it is a `static uint` that stays 0
  unless `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)` copies it from the interpolated
  `stereoTargetEyeIndex` (`UnityInstancing.cginc:125-138`, URP `UnityInput.hlsl:212-213`).
  **Consequence:** a fragment shader that reads `_WorldSpaceCameraPos`, `UNITY_MATRIX_V` or
  `UNITY_MATRIX_I_V` without that macro uses the left eye's value for both eyes — subtly wrong
  stereo, invisible in a mono capture.
- **Instancing (PC Link):** eye = `instanceID & 1`, instance = `instanceID >> 1`
  (`UnityInstancing.cginc:171-190`) — why the manual asks for doubled indirect counts, and why that
  doubling is wrong under multiview.
- Chemispace's Built-in shader has no `multi_compile_instancing` and still renders in stereo on
  device — the stereo variants are compiled without the pragma (INFERRED from its shipped behaviour);
  keep the pragma anyway so GPU instancing variants exist.

### 11.4 Proposed CosmoSynth star-field shader (PROPOSED — not compiled)

```hlsl
Shader "CosmoSynth/StarField" {
  SubShader {
    Tags { "RenderType"="Transparent" "Queue"="Background+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
    Pass {
      Name "Stars" Tags { "LightMode"="UniversalForward" }
      Blend One One, Zero One      // additive colour, destination alpha (passthrough coverage) untouched
      ZWrite Off ZTest LEqual Cull Off
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      CBUFFER_START(UnityPerMaterial)
        float3 _ViewerPc;   // eye in the chunk frame, parsecs (double on CPU, subtracted per chunk)
        float  _SkyRadius;  // metres, inside the far plane, e.g. 800
        float  _MagLimit;   // apparent magnitude that maps to flux 1
        float  _MinPx, _MaxPx, _Exposure;
      CBUFFER_END
      struct A { float3 posPc:POSITION; half4 col:COLOR; float2 magCorner:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
      struct V { float4 pos:SV_POSITION; half3 col:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
      V vert (A v) {
        V o = (V)0;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        float3 rel  = v.posPc - _ViewerPc;
        float  dPc  = max(length(rel), 1e-6);
        float  m    = v.magCorner.x + 5.0 * log10(dPc / 10.0);      // apparent magnitude
        float  flux = exp2(-1.3287712 * (m - _MagLimit));          // 10^(-0.4 (m - mlim))
        float3 dirWS = normalize(mul((float3x3)UNITY_MATRIX_M, rel / dPc));
        float3 eyeWS = UNITY_MATRIX_I_V._m03_m13_m23;              // per eye: star at infinity
        float4 cs = TransformWorldToHClip(eyeWS + dirWS * _SkyRadius);
        uint   ci = (uint)v.magCorner.y;
        float2 corner = float2((ci & 1u) ? 1.0 : -1.0, (ci & 2u) ? 1.0 : -1.0);
        float  px = clamp(_MinPx * sqrt(max(flux, 1.0)), _MinPx, _MaxPx);   // grows only when bright
        cs.xy += corner * px * cs.w / _ScreenParams.xy;            // px is the full width
        o.pos = cs;
        o.uv  = corner;
        o.col = v.col.rgb * (min(flux, 1.0) * _Exposure);
        return o;
      }
      half4 frag (V i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        half a = exp2(-4.0 * dot(i.uv, i.uv));                     // Gaussian, ~0.06 at the edge
        return half4(i.col * a, 0);
      }
      ENDHLSL
    }
  }
}
```

It needs a compile check, a stereo capture, and the pixel-neighbourhood test from
`cosmosynth-small-things-need-two-bounds` before anyone trusts it.

---

## 12. Frame budget sheet (proposal — INFERRED allocation, replace with measurements)

Frame 13.9 ms at 72 Hz; aim <= 11 ms app GPU (Worldfall §5). Passthrough costs ~17% GPU / 14% CPU
(Meta, Worldfall RESEARCH:96).

| Item | GPU ms | Other limits |
|---|---|---|
| Sky layer: CMB + galaxies + stars | <= 1.5 | 1 CMB draw; point vertices <= 0.6 M total; sprite fragments <= 0.5 eye-screens |
| Dust | <= 2.5 | step-coverage budget B ~ 30; 3.3 MB texture |
| Orbits, grids | <= 1.0 | <= 150k triangles, 1-3 draws |
| Bodies (spheres, atmospheres, later terrain) | <= 4.0 | terrain per Worldfall's ring budget when Phase 3 lands |
| UI and text | <= 1.0 | 1 TMP material |
| Headroom (thermal, FFR off in the lab) | rest | |
| Totals | <= 11 | draws <= 150 (Meta busy band 200-300); triangles <= 1.0 M (Meta 1.3-1.8 M); textures <= 350 MB (Worldfall §5) |

Measure with `adb logcat -d | grep VrApi` (`FPS=72/72,...,App=...ms,...,GPU%=...,Fov=...`) —
frame rate is measured by the compositor, not by the app (Fractarium CLAUDE.md:27-48, V), and
`Fov=` is the only proof foveation is really on (`foveatedRenderingLevel` returns Unity's cached
value, Fractarium CLAUDE.md "Foveation was never on").

---

## 13. Open questions (need a headset or a measurement)

1. **The grid.** Read the `render:` line on 1.9.0 and look for the grid (§2.2).
2. **Eye-buffer depth format at runtime** after switching to None: log
   `XRSettings.eyeTextureDesc.depthStencilFormat` (expected D24_UNorm_S8_UInt).
3. **kappa_dust** (ms per step-coverage for a 3D-texture step) — build a probe like Fractarium's
   `tools/cost_probe.sh`; everything in §5.2 scales with it.
4. **Point-vertex cost** of 120k vs 500k stars in multiview on Quest 3 (OVR Metrics / VrApi).
5. **Symmetric Projection and Multiview Render Regions with URP in this project** — documented
   requirements (multiview, Vulkan, Unity 6.1+, render graph) are met on paper only.
6. **TMP glyph coverage** for Greek letters and astronomical symbols in the shipped atlas.
7. **Gamma -> Linear** colour audit (every `CosmoTheme` colour and vertex colour).
8. **Whether untagged built-in unlit shaders still draw under URP** in a runtime-built scene — moot
   if every runtime shader is replaced with a single-variant `CosmoSynth/*` shader, which is the
   recommendation.

---

## 14. Sources

Meta (VERIFIED, fetched 2026-09-11):
- https://developers.meta.com/horizon/documentation/unity/unity-openxr-settings/ — multiview recommended; depth submission "None" for most apps; depth resolve and composition costs
- https://developers.meta.com/horizon/documentation/unity/unity-openxr-settings-quest/ — Symmetric Projection 5-15% GPU; Optimize Buffer Discards saves ~90 MB per eye at 4x MSAA 1680x1760; Multiview Render Regions Unity 6.1+
- https://developers.meta.com/horizon/documentation/unity/po-advanced-gpu-pipelines/ — XR2 Gen 2 ~2 MB GMEM; 96x176 tiles at 4x MSAA + D24S8; load/store ms example
- https://developers.meta.com/horizon/documentation/unity/unity-perf/ — Quest 3 draw calls 200-300 / 400-600 / 700-1000; 1.3-1.8 M triangles
- https://developers.meta.com/horizon/documentation/unity/os-fixed-foveated-rendering/ — FFR 6.5 / 11.5 / 21%; keep text central

Unity (VERIFIED unless marked):
- https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.17/manual/project-configuration.html — render modes; depth submission "shared depth buffer using 16/24 bits per pixel"
- https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.15/api/UnityEngine.XR.OpenXR.OpenXRSettings.DepthSubmissionMode.html
- https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.17/manual/features/foveatedrendering.html — SRP Foveation does not support Built-in
- https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.17/manual/features/metaquest.html — buffer discards = memoryless 4x MSAA
- https://docs.unity3d.com/Packages/com.unity.xr.oculus@4.2/manual/index.html — Optimize Buffer Discards "can potentially break ... camera stacking"
- https://docs.unity3d.com/Manual/SinglePassInstancing.html — macros; indirect-count doubling
- https://docs.unity3d.com/6000.1/Documentation/Manual/SL-PlatformDifferences.html — reversed-Z list (omits Vulkan; include file says otherwise)
- https://unity.com/topics/render-pipelines-strategy-for-2026 — Built-in deprecated in 6.5
- https://issuetracker.unity3d.com/issues/msaa-is-not-applied-on-quest-2-using-built-in-render-pipeline-and-vulkan — search snippet only (INFERRED): won't fix; depth submission cause
- https://issuetracker.unity3d.com/issues/scene-becomes-black-in-meta-quest-when-using-built-in-render-pipeline-with-vulkan — search snippet only: not reproducible as of 6.3.0b1
- https://discussions.unity.com/t/how-to-renderprimitivesindirect-for-single-pass-stereo-with-instance-count-x2-gracefully/1636324
- https://discussions.unity.com/threads/structuredbuffer-on-quest-2-with-vulkan-and-single-pass-instanced-not-working.1365954/
- https://forum.unity.com/threads/camera-stack-causes-a-huge-drop-in-fps-quest-2-vulkan-urp.1330923/ — search snippet only (INFERRED)
- https://discussions.unity.com/t/gamma-color-mode-on-oculus-with-openxr/887009 — Gamma unsupported with OpenXR

Graphics references (VERIFIED):
- https://developer.nvidia.com/content/depth-precision-visualized
- https://outerra.blogspot.com/2012/11/maximizing-depth-buffer-range-and.html
- https://bgolus.medium.com/the-best-darn-grid-shader-yet-727f9278b9d8
- https://medium.com/androiddevelopers/efficient-render-passes-on-tile-based-rendering-hardware-621070158e40 — LOAD_OP_DONT_CARE contents are garbage (search snippet)

Data (VERIFIED unless marked):
- https://wiki.cosmos.esa.int/planck-legacy-archive/index.php/CMB_maps — Nside 2048, ~5 arcmin
- https://arxiv.org/abs/1108.0669 — 2MRS; Table 3 columns (PDF pages 5-6, 13-14 read)
- https://www.ipac.caltech.edu/2mass/releases/allsky/doc/sec2_3a.html — XSC `sup_ba`, `sup_phi` (E of N, 5 deg quantised)
- https://www.aanda.org/articles/aa/full/2001/18/aa10469/node5.html — Hubble inclination formula, q0 values (search snippet)
- Quest 3 panel 2064 x 2208 per eye, ~25 PPD, 110 x 96 deg FOV — search snippets (INFERRED)
