# CosmoSynth render / XR audit — why 1.7.0 drew "100 plus Earths on a grid", and what the next APK needs

2026-09-11 · read-only research (no builds, no editor, nothing under `~/projects` modified except
this file) · slug `render-xr-audit`

Every claim is tagged **VERIFIED** (I read the file or ran the command; file:line given) or
**INFERRED** (reasoning from verified facts, or general Unity/GPU knowledge that I could not check
on this box). Paths are relative to `~/projects/CosmoSynth/unity/` unless they start with `~` or
name another project.

---

## 0. Verdict (read this if nothing else)

1. **Root cause of the 1.7.0 grid, high confidence:** *camera stacking* (the two-camera
   `DepthBands` rig) **plus** 4×MSAA **plus** OpenXR's Meta-Quest *Optimize Buffer Discards*
   (default ON), on Vulkan. That flag makes the 4×MSAA eye-buffer attachment **memoryless** and
   "always resolves MSAA colour / discards depth"; Unity's own package source says in so many words
   that this breaks *camera stacking*. The near-band camera clears depth only, so it starts its
   render pass by *loading* an MSAA colour attachment that has no memory behind it → undefined
   tile memory, which on a tiled GPU shows up as bin-sized copies of the frame repeated across
   the view. The mechanism is INFERRED; every ingredient is VERIFIED (§2.2), and the timeline fits
   exactly: the one device-affecting change between the working 1.6.0 and the broken 1.7.0 was
   Android quality level 2 going `antiAliasing 0 → 4` (§2.1).
2. **1.7.1/1.7.2 reverted MSAA to 0, and the recovered 1.9.0 APK really does ship MSAA 0.**
   I checked the baked `QualitySettings` inside the APK binary, not just the YAML (§5.4). With no
   MSAA the load has real memory behind it, so 1.9.0 *should* render correctly. **Nobody has
   checked that on a headset**: the user has not tested since 1.7.0.
3. **The grid is not caused by** duplicate XR folders (they are empty; exactly one
   `MetaQuestFeature`), a flat 2D launch (the VR intent category is present in 1.7.0 and 1.9.0),
   shaders missing stereo macros (both custom shaders have all five), `Graphics.DrawMesh`/`GL`/
   RenderTextures (none in the runtime), or texture tiling. The APK packaging (boot.config,
   manifest, native libs) is **identical** to the device-verified Microverse and Mechanarium
   APKs (§5).
4. **Recommendation:** (a) move to **URP**, the recipe all four device-verified projects on this
   box use, and Unity 6.5's own OpenXR docs call Built-in *deprecated* (§6.1). (b) **Delete
   `DepthBands`**. Use **one XR camera plus angular-size-preserving distance compression**
   ("scaled space"), as Worldfall and GeoramaXR already do (§6.2). (c) Set depth submission to
   **None** to match every reference project. (d) Add the static, artefact and play-mode gates in
   §7, so that this class of failure, which cannot be reproduced on llvmpipe, cannot ship again.

---

## 1. Evidence base and method

| Source | What it gave | Status |
|---|---|---|
| `git log` in `~/projects/CosmoSynth` | **History is squashed into 4 snapshot commits**, all titled "Track the source tree in git": `c450708` 2026-08-01 11:31 (= the **1.7.2** tree, bundleVersion 1.7.2 / code 14, 399 files), `bcb2886` 2026-08-02 01:22 (= **1.9.0**: code 14→16, HYG sky, NearbyStarSystems), `87ee996` 2026-08-22 (Core `TerrainMesh` + tests), `cf93a25` 2026-09-06 (`.gitignore`). **1.6 and 1.7.0 are not in git.** | VERIFIED |
| Session transcript `~/.claude/projects/-home-triton-projects-CosmoSynth/30b1156c-754f-4364-bb1f-0ce34a65b85b.jsonl` (11.5 MB) | The 1.6→1.7.0→1.7.2 history, reconstructed from its tool calls and results (line numbers `L…` below refer to jsonl lines) | VERIFIED |
| All Runtime/Rendering, Runtime/XR/XrRig.cs, Editor/XrSetup.cs, Editor/ServerBuild.cs, both `.shader` files, ProjectSettings, `Assets/XR*` | Current (1.9.0 + Phase-3 Core) source | VERIFIED |
| Microverse, Mechanarium, Worldfall, GeoramaXR: ProjectSettings, QualitySettings, GraphicsSettings, `Packages/manifest.json`, `Assets/XR/Settings/OpenXRPackageSettings.asset`, `tools/verify-apk.sh`, CONTRACT/CLAUDE stereo rules | Proven configuration to diff against | VERIFIED |
| APKs: `~/builds/cosmosynth/recovered/CosmoSynth-release.apk` vs `~/builds/microverse/Microverse-0.1.6.apk`, `~/builds/mechanarium/Mechanarium-1.0.2.apk`, `~/builds/georamaxr/GeoramaXR-0.1.3.apk` | aapt2 badging/xmltree, boot.config, lib list, apksigner, baked QualitySettings scan | VERIFIED |
| Installed package sources `Library/PackageCache/com.unity.xr.openxr@58837b2992e0` (1.17.1), `com.unity.xr.meta-openxr@dae986a05b5c` (2.5.0), `com.unity.render-pipelines.core@bf8191d734cd` | What the flags actually do, validation rules, docs (`Documentation~`) | VERIFIED |
| Web | **No URLs fetched.** Unity docs were read from the `Documentation~` folders shipped inside the packages. | — |

---

## 2. The "100 plus instances of Earth … repeated on a grid, flat" report

### 2.1 Timeline, reconstructed from the transcript (all VERIFIED)

Times are CEST (UTC+2). The transcript stores UTC; file mtimes are CEST.

| When | Build / event | Android quality L2 (`antiAliasing` / `pixelLightCount`) | Cameras |
|---|---|---|---|
| 30 Jul 22:26 | **1.5.0**: `DepthBands` introduced to fix the 26 km near plane black screen (`DepthBands.cs.meta` 22:06; `docs/feedback.md` §1.5.0) | 0 / 1 (Unity default "Medium") | 2 |
| 31 Jul 07:39 | **1.6.0 (original)** | 0 / 1 | 2 |
| 31 Jul 16:21 | User on the device: *"make stars as stars, not some square looking patches at distance"* (jsonl L833). **The scene rendered; the stars were visible.** | 0 / 1 | 2 |
| 31 Jul 16:48 | Critique agent: "Android quality level 2 has **`antiAliasing: 0`**" (L1149) | 0 / 1 | 2 |
| 31 Jul ~17:06 | Aesthetics agent: `QualitySettings.asset` Medium → `antiAliasing: 4`, `pixelLightCount: 2`, plus `ForcePixel` star light, `CosmoSphere` icosphere, spacecraft tweaks (L1165, L1203) | **4 / 2** | 2 |
| 31 Jul 17:59 | **1.6.0 rebuilt without a version bump**, which overwrote the original (L1469; CLAUDE.md "losing the earlier 1.6.0") | 4 / 2 | 2 |
| 31 Jul 18:01 | **1.7.0**, code 12. Build gate: VR intent OK, 15 `xr-*` keys (L1517) | 4 / 2 | 2 |
| 31 Jul 18:47 | User: *"current apk renders like 100 plus isntances of earth , issue with something aI see everyghin so manh times repeated ona grid flat"* (L1658, verbatim) | — | — |
| 31 Jul 18:50 | **1.7.1**, code 13: Medium reverted to `antiAliasing 0`, `pixelLightCount 1`. "Nothing else" (L1681, L1708) | 0 / 1 | 2 |
| 31 Jul ~18:57 | **1.7.2**, code 14: adds `RenderDiagnostics` (the `render:` status line) (L1729, L1748) | 0 / 1 | 2 |
| 1 Aug 22:5x / 23:23 | **1.8.0** (HYG sky), **1.9.0**, code 16 (worlds around stars). 1.9.0 survives as `recovered/CosmoSynth-release.apk` | 0 / 1 | 2 |
| 1 Aug 22:56 | User: *"cant test in XR right now"* (L1947). **No headset report since 1.7.0.** | | |

Side finding (VERIFIED): `~/builds/shots/cosmosynth/v1.7.2__01-msaa-reverted-plus-render-readout.png`
is **byte-identical** to the v1.7.0 shot (md5 `8f347ff30036caa90c27cc28ddd97b5c` for both). It was
copied, not captured from the 1.7.2 tree, so it does not show the render readout its name
advertises. The v1.8.0 and v1.9.0 shots are copies of `realsky2.png` / `sirius.png`, which is
the documented practice.

### 2.2 The mechanism (ingredients VERIFIED, composition INFERRED)

1. **Camera stacking exists.** `Runtime/XR/XrRig.cs:63-78` creates the tracked "Main Camera",
   then `DepthBands.Attach` (`Runtime/Rendering/DepthBands.cs:58-85`) adds a second camera
   parented under it: `:64` new GameObject, `:67` `AddComponent<Camera>`, `:68` `CopyFrom`,
   `:72` `depth = near.depth - 1` (renders first). `:78` sets the **near (XR-origin) camera to
   `CameraClearFlags.Depth`**, re-enforced every frame at `:106`. The far camera clears to the
   passthrough colour (0,0,0,0).
2. **Vulkan-only, single-pass instanced (multiview):** `ProjectSettings.asset:400`
   `m_APIs: 15000000` (0x15 = Vulkan), automatic off; OpenXR Android `m_renderMode: 1` (SPI)
   at `Assets/XR/Settings/OpenXR Package Settings.asset:167`, forced by `XrSetup.cs:276`.
3. **Optimize Buffer Discards is ON**: `OpenXR Package Settings.asset:174` (OpenXRSettings
   Android) and `:1409` (MetaQuestFeature Android), both `m_optimizeBufferDiscards: 1`. It is
   the package default (`MetaQuestFeature.cs:133-134`, `bool m_optimizeBufferDiscards = true`)
   and `XrSetup` never touches it.
4. **What that flag does, in Unity's words** (`com.unity.xr.openxr@58837b2992e0/Runtime/OpenXRUnityAPI.cs:191-197`):
   > "Opt-in to always discarding depth and resolving MSAA color to improve performance on
   > tile-based architectures at the expense of rarely-used effects which require depth resolve
   > or MSAA color store, **such as camera stacking**. This only affects Vulkan. Note that this may
   > break user content …"

   and `Documentation~/features/metaquest.md:65`: *"allows 4x Multi Sample Anti Aliasing (MSAA)
   textures to be memoryless on Vulkan."*
5. **1.7.0 turned on 4×MSAA** (§2.1). With the MSAA colour attachment memoryless, the far
   camera's pass resolves and discards it. The near camera's pass (clear = depth only) then
   begins by *loading* a colour attachment that has no backing store. The contents are undefined.
   On a tile-based GPU that means whatever the on-chip bin memory held, and then that garbage is
   resolved over the far band's correct image. (INFERRED: standard tiled-GPU behaviour for a
   load from a transient attachment.)
6. **Why "a grid of copies":** undefined bin memory typically repeats the last-rendered bin's
   content in every bin. At 4×MSAA with two multiview layers, a bin holds tens of thousands of
   pixels, so an approximately 2k×2k eye buffer is on the order of 100 bins, matching "100 plus
   instances". INFERRED order-of-magnitude plausibility only: Adreno 740 bin sizes are
   driver-chosen, and I did not measure them.
7. **Why desktop captures never showed it:** the play-mode harness runs flat, on llvmpipe, at
   quality level 5, with no memoryless attachments (`render: flat (no XR device) cams 2 q5`,
   L1911/L1941). This failure class **cannot be reproduced on this box at all** (§7.6).

### 2.3 Candidate causes, ranked

| # | Candidate | Evidence | Verdict |
|---|---|---|---|
| 1 | **DepthBands camera stacking + 4×MSAA + memoryless MSAA (Optimize Buffer Discards) on Vulkan** | §2.2. The only device-render delta between the working 1.6.0 and 1.7.0 was L2 `antiAliasing 0→4` / `pixelLightCount 1→2` (L1667-1682). DepthBands was present in both. Unity's own comment names camera stacking as what the flag breaks. | **Primary cause.** High confidence; not device-confirmed. |
| 2 | Built-in RP + SPI + Vulkan + MSAA *by itself* | Common, working Quest configuration when there is **one** camera (INFERRED). Nothing in the evidence isolates MSAA without stacking. | Contributory only through #1 |
| 3 | Far camera parented under the tracked camera and `CopyFrom`'d | `DepthBands.cs:64-68`. `CopyFrom` also copies the transform (Unity API behaviour, INFERRED). At attach time both are at local identity, so no offset results. Under XR-SDK the per-eye view is derived from the camera's own transform plus the eye offset, which is why a `TrackedPoseDriver` is mandatory. A child at local identity therefore gets the same matrices (INFERRED). A mismatch would show as the far layer *swimming* with head motion, not a grid, and the 1.6.0 user report mentions no swimming. | Not the cause. Still fragile, and removed by §6.2 |
| 4 | Shaders without stereo macros | `Runtime/Shaders/CosmoAtmosphere.shader:72,90,97,103-105,140` and `CosmoRim.shader:41,51,59,65-67,76` have all five macros (`multi_compile_instancing`, `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`). These are the only shader sources in `Assets` (`find` for `*.shader/*.cginc/*.hlsl/*.shadergraph/*.compute`), and there is no shader source embedded in C#. All other materials use Unity built-ins (`CosmoShaders.cs:49-59`: Standard, Unlit/Color, Unlit/Transparent, Sprites/Default, Particles/Standard Unlit, GUI/Text Shader), which are stereo-aware (INFERRED). A missing macro gives one-eyed or wrong-eye rendering, never a tiled frame. | Excluded |
| 5 | `Graphics.DrawMesh` / `GL.*` / `CommandBuffer` / `OnRenderImage` / RenderTextures / `camera.targetTexture` | `grep` over `Assets/CosmoSynth/Runtime` finds none. `ViewportInfo.cs:32` only *reads* `targetTexture`. Only the Editor harness (`PlayModeShot.cs:552-565, 764-765, 811`) sets targets, and it is not in the player. | Excluded |
| 6 | App launching as a flat 2D panel (missing `com.oculus.intent.category.VR`) | 1.7.0's build gate printed "OK immersive launch" (L1517), and the category is present in the 1.9.0 APK (§5.2). A 2D panel shows a window, not a tiled frame. | Excluded |
| 7 | Duplicate `Assets/XR 1..3` / `XR/Settings 1..3` breaking Meta's build hook | All six directories are **empty** (created in the same second, 30 Jul 17:13:38). Exactly **one** `MetaQuestFeature` exists (`grep -rl` of its script guid `f647cc05…` finds only `OpenXR Package Settings.asset`, one document). The build succeeded, so the hook did not bail. | Excluded (hygiene only, §6.3 P2) |
| 8 | Texture tiling ("100 Earths" read literally) | Planet maps are `wrapModeU=Repeat, V=Clamp` with no `mainTextureScale`/`SetTextureScale` anywhere (`grep`). `CosmoSphere.cs:76-82,127-129` only interpolates Unity's own UVs. `StarSprite` uses Clamp (`StarSprite.cs:45`). | Excluded |
| 9 | Camera accumulation on each catalogue merge | `DepthBands` is attached once, in `XrRig.BuildRig` (checked at L1687-1695). The desktop report says `cams 2`. | Excluded |
| 10 | `pixelLightCount 1→2` (the other 1.7.0 delta) | Adds Standard ForwardAdd passes. Cannot tile a frame. Reverted together with MSAA, so not separable by evidence. | Very unlikely |

---

## 3. Audit of every render path in the current tree (1.9.0 + Phase-3 Core)

| Area | File:line | Finding | Stereo / Quest risk |
|---|---|---|---|
| XR camera | `XrRig.cs:67-73` | near 0.05, far 5000, `allowHDR false`, `allowMSAA true`, clear SolidColor (0,0,0,0) when passthrough. `:173` re-applies alpha 0 once XR is up. `:178-189` adds `ARSession` + `ARCameraManager` at runtime | OK. However `DepthBands.cs:78/106` then forces this camera to `Depth` clear, which violates Meta's passthrough rule (next row) |
| Meta passthrough rule | `com.unity.xr.meta-openxr@dae986a05b5c/Runtime/Subsystems/Camera/ARCameraFeature.cs:194` | *"Passthrough requires Camera clear flags set to solid color with alpha value zero"* for the XR-Origin camera carrying `ARCameraManager` | **Violated** by DepthBands. It works only because the far camera clears. The rule never fires because the rig is built at runtime and edit-time validation cannot see it |
| Depth bands | `DepthBands.cs:38` `MaximumBandRatio 1e5` "24-bit buffer"; `:117-133` `FitTo` (floor 2000, split = √(0.05·far)); `:151` far near-plane = split×0.9 | Two render passes per frame. The second must LOAD the full colour of both eye layers. Blocks MSAA permanently (§2.2). The "24-bit" assumption conflicts with `Depth16Bit` submission (next row) | **Remove** (§6.2) |
| Depth submission | `XrSetup.cs:281` forces `Depth16Bit`; asset `:172` = 1 | All four reference projects use **0 (None)**. With Optimize Buffer Discards "always discarding depth", the submitted depth is at best undefined (INFERRED). If the eye depth attachment *is* the 16-bit swapchain image, the band ratios DepthBands assumes (1e5) are about 256× beyond 16-bit precision (INFERRED) | Set **None** |
| Clip-plane driver | `ScaleRenderer.cs:100-109` measures drawn distance in universe-local units and feeds `FitTo`; `CelestialBodyView.cs:229-253` pins bodies beyond the horizon onto a sphere (direction kept, distance dropped) | The existing pinning is already a degenerate distance compression (a step function). §6.2 generalises it | Replace with continuous f(d) |
| Star field | `SkyAndRings.cs:407-448`: one UInt32-index mesh on a **900-unit sphere parented to the anchor** (`:29`, `:428`), up to 9000 catalogue stars × 1–5 quads (`:26`, `:342-376`), `Particles/Standard Unlit` additive at queue 1000, ZWrite 0 (`StarSprite.cs:141-151`) | Anchor-centred rather than eye-centred, so walking 2 m shifts the "infinite" sky by 2/900 rad ≈ 2.2 mrad. `StarSprite` docstring: alpha-write "not verified" for this built-in shader. The previous session measured no alpha writes in the editor (CLAUDE.md "Needs a headset…") | Replace with an explicit `CosmoSynth/Star` shader with `Blend One One, Zero One`, eye-centred |
| Sky billboards | `StarPointBillboard.cs:83-89` face `Camera.main.transform.rotation` | Centre-eye facing: correct for stereo | OK |
| Atmosphere | `CosmoAtmosphere.shader:45-63` Transparent+100, `Blend One One, Zero One`, Cull Off, ZTest LEqual; `:140` eye index; `:237` alpha 0 | Stereo-correct, passthrough-safe. Uses analytic ray–sphere against its own planet (`:166-168`), so it does not need the depth test against that planet (useful under compression, §6.2) | OK. Port to URP HLSL |
| Rim | `CosmoRim.shader:28-35`, `:76`, `:86` | Stereo-correct, passthrough-safe | OK. Port to URP HLSL |
| Orbit lines | `OrbitLineRenderer.cs:85` `LineAlignment.View`, `:235` width from nearest distance | View alignment is computed per camera (centre eye), fine. Under compression, widths must scale per vertex (§6.2) | Adapt |
| Planet meshes | `CelestialBodyView.cs:84-121` `CreatePrimitive` + per-body instanced Standard material; `CosmoSphere.cs:45-107` subdivided copy (reads `primitive.isReadable`, falls back safely) | None stereo-specific. In URP the default-material and property names change (§6.3) | Port |
| Spacecraft | `CelestialBodyView.cs:436-460` Standard with `_EMISSION`, metallic/gloss clamps | Same as above | Port |
| Text / panels | `CosmoText`/`CosmoPanel`/`CosmoTheme.cs:229,240` use GUI/Text Shader and Sprites/Default | Built-in unlit shaders with no LightMode tag still render under URP as `SRPDefaultUnlit` (INFERRED); keep or move to URP/Unlit | Low |
| IMGUI HUD | `ProvenanceHud.cs:49` `OnGUI`; added unconditionally (`CosmoSynthBootstrap.cs:57` `_showHud = true`, `:171`) | Runs GUILayout over approximately 240 bodies on the headset every frame. Screen-space IMGUI is not shown in XR (INFERRED), so this is CPU and GC for nothing | Skip when `XRSettings.isDeviceActive` |
| Diagnostics | `RenderDiagnostics.cs:39-57`, shown by `AnchorBeacon.cs:176-177` | Good idea; extend (§7.5) | Keep |
| Always-included shaders | `XrSetup.cs:343-394` writes `CosmoShaders.All` into `GraphicsSettings.asset:29-42` (13 entries) | Fine for Built-in. Under URP, always-including `Universal Render Pipeline/Lit` compiles every variant (build time and size); use material templates instead (§6.3) | Rework with URP |

---

## 4. CosmoSynth vs the four proven Quest projects

### 4.1 Player / quality / graphics (VERIFIED by script over each project's YAML)

| Setting | CosmoSynth | Microverse | Mechanarium | Worldfall | GeoramaXR |
|---|---|---|---|---|---|
| Render pipeline (`GraphicsSettings.m_CustomRenderPipeline`) | **none (Built-in)** `GraphicsSettings.asset:55` | URP asset `d0e2fc18…` | URP `d0e2fc18…` | URP `WF_URP` `35e6e3f0…` | URP `d0e2fc18…` |
| URP asset MSAA / HDR / depth tex / opaque tex / SRP batcher | n/a | 4 / off / off / off / on | same | 4 / off / off / off / on | same |
| Android default quality level | **2 (Medium)** | 1 (Low) | 1 | 1 | 1 |
| …its `antiAliasing` | **0** | 4 | 4 | 4 | 4 |
| …`pixelLightCount` / `shadows` / `vSyncCount` | 1 / **1** / **1** | 0 / 0 / 0 | 0/0/0 | 0/0/0 | 0/0/0 |
| Colour space `m_ActiveColorSpace` | **0 Gamma** (`ProjectSettings.asset:49`) | 1 Linear | 1 | 1 | 1 |
| Graphics APIs Android | Vulkan only | Vulkan only | Vulkan only | Vulkan only | Vulkan only |
| `m_StereoRenderingPath` (legacy field) | 0 | 0 | 0 | 0 | 0 |
| `androidUseSwappy` | **1** (`:74`; `libswappywrapper.so` ships in the APK) | 0 | 0 | 0 | 0 |
| `activeInputHandler` | **2 Both** (`:784`, needed for legacy `Input` in HUD/desktop code) | 1 | 1 | 1 | 1 |
| minSdk / targetSdk | 32 / 0 (Auto → **36** in the APK) | 32 / 0 | 32 / 34 | 32 / 34 | 32 / 0 |
| Scripting / ABI / entry point | IL2CPP / ARM64 / GameActivity | same | same | same | same |
| Packages (URP 17.5.0, OpenXR 1.17.1, meta-openxr 2.5.0, ARF 6.5.0, XRI 3.5.1, Hands 1.8.0, InputSystem 1.19.0, XR Mgmt 4.5.4, CompositionLayers 2.5.0) | identical (URP **installed but unused**) | identical | identical | meta-openxr 2.5.1, hands 1.8.1 | identical |

### 4.2 OpenXR Android settings and features (VERIFIED)

| Setting | CosmoSynth | Microverse | Mechanarium | Worldfall | GeoramaXR |
|---|---|---|---|---|---|
| `m_renderMode` | 1 SPI | 1 | 1 | 1 | 1 |
| `m_depthSubmissionMode` | **1 (16-bit)** | 0 None | 0 | 0 | 0 |
| `m_optimizeBufferDiscards` (settings / MetaQuestFeature) | 1 / 1 | 1 / 1 | 1 / 1 | 1 / 1 | 1 / 1 |
| `m_latencyOptimization` | **0 PrioritizeRendering** | 1 PrioritizeInputPolling | 1 | 1 | 1 |
| `m_symmetricProjection` / MVPVV mode | 0 / 0 | 0 / 0 | 0 / 0 | **1 / 2** | 0 / 0 |
| `m_foveatedRenderingApi` / FoveatedRendering feature | 1 / off | 0 / off | 0 / off | 1 / **on** | 0 / off |
| MetaQuestFeature count | 1 | 1 | 1 | 1 | 1 |
| ARCamera (passthrough) / ARSession | on / on | on / on | on / on | on / on (pre-splash passthrough) | on / on |
| `…input.handtracking` (**Microsoft** Hand Interaction, the deprecated colliding id) | **ENABLED** (`OpenXR Package Settings.asset:1164-1166`) | off | off | off | off |
| `…input.handinteraction` / `handinteractionposes` | on / on | off | off | off | off |
| Settings asset name | `OpenXR Package Settings.asset` (XR-Mgmt default) | `OpenXRPackageSettings.asset` (MR template) | same | same | same |

OpenXR's own Meta validator recommends input polling: `MetaQuestFeature.cs:697` *"[Optional]
Latency Optimization is recommended to be set to Prioritize Input Polling when Meta Quest
support is enabled."* (VERIFIED)

The Microsoft profile is the exact trap CLAUDE.md warns about (XR Hands' old id collides with
it). `XrSetup` enables the correct `handtrackingsubsystem` but never *disables* the stale one.
Quest's runtime does not offer `XR_MSFT_hand_interaction`, so the feature is most likely just
skipped at runtime (INFERRED). It has no bearing on the grid.

### 4.3 `Assets/XR*` inventory (VERIFIED)

- `Assets/XR 1`, `XR 2`, `XR 3`, `Assets/XR/Settings 1`, `Settings 2`, `Settings 3`: **all empty**
  folders plus `.meta`, mtime 2026-07-30 17:13:38. They are a folder-creation race from the first
  XR-Management run (INFERRED). They break nothing.
- `Assets/XR/Settings/OpenXR Package Settings.asset`: the single OpenXR settings asset
  (`m_configObjects com.unity.xr.openxr.settings4 → 4606f6b0…`, `EditorBuildSettings.asset:8-11`).
- `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`: **5** `XRManagerSettings` docs named
  "Android Providers". Only `&1609618090827629129` is referenced by `Android Settings`; **4 are
  orphans**, left by `XrSetup.EnsureLoader` calling `CreateDefaultManagerSettingsForBuildTarget`
  on every run (`XrSetup.cs:143`, INFERRED). `m_InitManagerOnStart: 1`, loader = OpenXRLoader
  only.
- Two `XRSimulationRuntimeSettings.asset`: `XR/Settings/…` (registered, guid `5b948615…`) and
  `XR/Resources/…` (guid `9ab5c5ab…`, **orphan in a Resources folder, so it ships in the APK**).
- `Assets/CompositionLayers/UserSettings/Resources/CompositionLayersRuntimeSettings.asset`,
  `Assets/XRI/`, `UniversalRenderPipelineGlobalSettings.asset`, `DefaultVolumeProfile.asset`:
  package-generated.

### 4.4 Reference stereo-shader contract (VERIFIED)

`~/projects/GeoramaXR/CONTRACT-XR.md:93-135` (URP HLSL skeleton; every custom shader must be
URP-HLSL with the macros), `~/projects/Mechanarium/CONTRACT.md:103-105`, `~/projects/Worldfall/CLAUDE.md:34-35`
(additive = `Blend One One, Zero One`, camera clears (0,0,0,0), eye position from
`UNITY_MATRIX_I_V`). CosmoSynth's two shaders already follow the macro rule, in Built-in CG.

### 4.5 How the references cover huge distances with ONE camera (VERIFIED)

- **Worldfall** `Assets/Worldfall/Runtime/Atmosphere/CelestialBodies.cs:285-310`: bodies are
  placed at `eye + dir * min(distance, cam.farClipPlane*0.9)` with `radius = d * tan(angularDiameter/2)`,
  i.e. angular size is preserved and distance clamped. Camera near 0.05 / far 6000
  (`Editor/WorldfallSceneBuilder.cs:54-58`).
- **GeoramaXR** `CONTRACT-XR.md:54`: every Earth-surface feature is drawn on an egocentric
  shell `shellR(t) = 25 + 35·t/(t + 3e6)` m at its true bearing and dip. This is continuous
  monotonic compression of true distance *t* into 25–60 m.

---

## 5. The last CosmoSynth APK vs known-good APKs

`~/builds/cosmosynth/recovered/CosmoSynth-release.apk`: 46 520 034 bytes, mtime 1 Aug 23:23,
**versionName 1.9.0 / versionCode 16**, `com.triton.cosmosynth`. aapt2 used:
`~/Unity/Hub/Editor/6000.5.5f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/aapt2`.

### 5.1 boot.config (VERIFIED)

```
gfx-threading-mode=4            wait-for-native-debugger=0       hdr-display-enabled=0
gc-max-time-slice=3             xrsdk-pre-init-library=UnityOpenXR
xr-meta-enabled=0               xr-request-additional-vulkan-graphics-queue=0
xr-use-vulkan-offscreen-swapchain-no-main-display-buffer=1       xr-latelatching-enabled=0
xr-latelatchingdebug-enabled=0  xr-vulkan-extension-fragment-density-map-enabled=1
xr-low-latency-audio-enabled=1  xr-require-backbuffer-textures=0 xr-keyboard-overlay-enabled=1
xr-pipeline-cache-enabled=1     xr-skip-B10G11R11-special-casing=1
xr-hide-memoryless-render-texture=1  xr-skip-audio-buffer-size-check=1
xr-usable-core-mask-enabled=1   xr-mvpvv-enabled=0   android-preferred-data-location=1
build-guid=610ea5ce…
```
15 `xr-*` keys. `diff` of the sorted file against Microverse-0.1.6 and Mechanarium-1.0.2: the
**only** differences are `build-guid` and CosmoSynth's extra `gc-max-time-slice=3`
(incremental GC). So boot.config does not explain any rendering difference.

### 5.2 Manifest (aapt2 xmltree/badging, VERIFIED)

- Launcher activity `com.unity3d.player.UnityPlayerGameActivity` with `MAIN`, `LAUNCHER` and
  **`com.oculus.intent.category.VR`**, so it launches immersive. `screenOrientation=0`
  (landscape).
- `uses-feature` `android.hardware.vr.headtracking` (required), `com.oculus.feature.PASSTHROUGH`
  (required), `android.hardware.vulkan.version` (required), `oculus.software.handtracking` (not
  required).
- Permissions: `INTERNET`, `com.oculus.permission.HAND_TRACKING`,
  `org.khronos.openxr.permission.OPENXR(_SYSTEM)`, `com.oculus.permission.BOUNDARY_VISIBILITY`.
- `com.oculus.supportedDevices = quest|quest2|cambria|eureka|quest3s`, `com.oculus.vr.focusaware=true`.
- minSdk 32, **targetSdk 36** (Worldfall's gate warns the store validator wants ≤ 34; sideload
  is fine).
- Flattened name/value diff vs Microverse: CosmoSynth lacks only the scene, anchor, colocation and
  IOT permissions that Microverse's extra AR features add. vs Mechanarium: no difference beyond
  the package-specific receiver permission.

### 5.3 Native libraries (VERIFIED)

CosmoSynth `lib/arm64-v8a/`: `libUnityARFoundationMeta.so, libUnityOpenXR.so,
libUnityOpenXRHands.so, lib_burst_generated.so, libc++_shared.so, libgame.so, libil2cpp.so,
libmain.so, libopenxr_loader.so, libswappywrapper.so, libunity.so` (11). Microverse and
Mechanarium have the same set, **minus `libswappywrapper.so`** (Swappy off) **plus
`libUnityARFoundationAndroidXR.so`**. arm64 only, no armeabi-v7a. Signed with the **Android
Debug** key (apksigner).

### 5.4 Baked MSAA inside the APK (VERIFIED, new artefact check)

There is no UnityPy on the box, so I scanned `assets/bin/Data/globalgamemanagers` for each
quality level's length-prefixed name and decoded the following fields in the YAML's serialized
order (script in Appendix B). **Self-check:** all 6 levels decode to exactly the YAML's
`antiAliasing`/`pixelLightCount`, for CosmoSynth and for Microverse (whose levels differ, AA 4/4/0/0/2/4).

| APK | L0 | L1 | **L2 Medium (Android default)** | L3 | L4 | L5 |
|---|---|---|---|---|---|---|
| CosmoSynth 1.9.0 | AA 0 | AA 0 | **AA 0, pixel lights 1** | AA 0 | AA 2 | AA 2 |
| Microverse 0.1.6 (URP; the URP asset's MSAA 4 governs) | AA 4 | **AA 4 (Android L1)** | AA 0 | AA 0 | AA 2 | AA 4 |

So the shipped 1.9.0 has the 1.7.1 revert. The 1.7.0 trigger is absent from the last APK.

---

## 6. Recommendations

### 6.1 Keep Built-in or migrate to URP → **migrate to URP**

Evidence:

- **Proven on this box:** all four device-verified Quest projects run URP 17.5 with MSAA 4 from
  the pipeline asset, HDR off, depth/opaque textures off, and one XR camera (§4.1, §4.5). Their
  APKs have the same boot.config and manifest as CosmoSynth's (§5), so the pipeline and camera
  setup are the remaining differences.
- **Built-in is on its way out in exactly this Unity version:**
  `com.unity.xr.openxr@…/Documentation~/features/foveatedrendering.md:50`: *"In Unity 6.5 and
  newer, the Built-In Render Pipeline is deprecated and will be made obsolete in a future
  release."* The project runs 6000.5.5f1.
- **Quest features Built-in cannot use:** SRP foveation (`foveatedrendering.md:57`; `XrSetup.cs:84-93`
  must *disable* foveation to build at all), SpaceWarp and URP dynamic resolution (docs list URP
  as a prerequisite), Multiview Render Regions (validator: needs Render Graph, `MetaQuestFeature.cs:448`).
  Worldfall uses symmetric projection, MVPVV and foveation together.
- **The migration surface is small:** 11 runtime material sites (`grep`: `CelestialBodyView.cs:442`,
  `CosmoTheme.cs:229,240`, `OrbitLineRenderer.cs:82`, `SkyAndRings.cs:514,721`, `StarSprite.cs:143`,
  `AnchorBeacon.cs:65`, `BodySelector.cs:70,108`, plus `CreatePrimitive` defaults) and 2 custom
  shaders (244 + 93 lines). URP 17.5.0 and its global settings asset are already installed.

Costs and risks to plan for (INFERRED): Standard → URP Lit renames `_Glossiness→_Smoothness`
and `_METALLICGLOSSMAP→_METALLICSPECGLOSSMAP`. `material.color`/`mainTexture` keep working via
`[MainColor]/[MainTexture]`. Lighting will look different, so the star and planet tuning
measured in gamma/Built-in captures must be re-measured. `PlayModeShot`'s `camera.Render()`
into a RenderTexture must be re-validated under URP.

Fallback if URP must wait one APK: stay on Built-in, but only with **one camera** (§6.2). Then
MSAA 4 becomes safe (INFERRED). Never ship Built-in + DepthBands + MSAA > 1.

### 6.2 DepthBands vs single camera + scaled space → **single camera + scaled space**

Why DepthBands has to go (VERIFIED unless noted):

1. It is camera stacking. Combined with MSAA and the default Quest buffer-discard optimisation,
   it produced the 1.7.0 grid (§2.2). Keeping it means **no MSAA, ever**, on a scene of
   hairline orbits, text and planet limbs that the user already called "thin lines everywhere".
2. It makes the XR-origin camera violate Meta's passthrough rule (`ARCameraFeature.cs:194`).
3. Meta's foveation API does not support intermediate targets or camera stacking
   (`foveatedrendering.md:59`).
4. It costs a second cull and a second render pass per frame, and the second pass must load the
   full colour of both eye layers from memory (≈ 2 × W × H × 4 bytes per frame; at 2064×2208
   that is 36 MB/frame, 2.6 GB/s at 72 Hz; INFERRED, eye size not measured on device).
5. Its own premise, a 24-bit depth buffer, is contradicted by `Depth16Bit` submission (§3).
6. `docs/architecture.md:137-139,233` and `Core/Scale/ScaleSpace.cs:24-35,159-169` still plan a
   "multi-pass band compositor". **On Quest, bands should be a placement concept inside one pass,
   not separate camera passes.**

Why one camera is enough (numbers):

- Quest 3 is about 25 px/degree (INFERRED spec), so 1 px ≈ 0.70 mrad. Stereo disparity for
  IPD 63 mm is `0.063 / d` rad, which is **1 px at d ≈ 90 m** and 0.1 px at 900 m. Beyond about
  100 m stereo depth is sub-pixel, so compressing distance there is invisible to stereo. Motion
  parallax from head movement scales the same way.

**The compression (angle-preserving, monotonic, C¹).** Let `E` be the centre-eye world position.
Use ONE shared point for both eyes: compressing about each eye separately would give the two
eyes different geometry and wrong vergence. For a body at true render position `P`
(after `UnitsPerKm`) with true radius `R`:

```
d  = |P − E|,  û = (P − E)/d
f(d) = d                                   for d ≤ D0
f(d) = D0 + W·(1 − exp(−(d − D0)/W))       for d > D0,   W = Dmax − D0
P' = E + û·f(d)        R' = R·f(d)/d       (so R'/f(d) = R/d: angular size exact)
```

- `f` is continuous with `f'(D0) = 1`, strictly increasing (depth order along every ray, and
  between bodies, is preserved), and bounded by `Dmax`.
- Suggested values: `D0 = 50 m` (the room and the hologram at true scale; 1.3 px disparity),
  `Dmax = 900 m`, camera far = 1000 m, near = 0.05 m. Worked values: `f(100 m) ≈ 98.5 m`,
  `f(500 m) ≈ 399 m`, `f(10 km) ≈ 900 m`.
- Apply compression only when the body's near surface is beyond the identity zone
  (`d − R > D0`). Otherwise draw at true scale. Standing on or near a huge body is Phase 3
  terrain; handle it **per vertex** (below).
- **Per-vertex form**, for orbit lines and terrain: `v' = E + (v − E)·f(|v−E|)/|v−E|`. Every
  vertex keeps its exact screen position from `E`, so each triangle's screen footprint is
  unchanged. Only depth is remapped, monotonically. LineRenderer widths must scale by
  `f(d)/d` per vertex (width curve), or move to a shader.
- The existing sky pinning (`CelestialBodyView.cs:229-253`, horizon = `FitTo`'s `far*0.6`) is
  the limit case of `f`. Fold it in so there is one rule, not two.
- Centre the star sphere on `E`, not on the anchor, and keep it at `≈ Dmax`, drawn first with
  ZWrite Off (it already is, at queue 1000).
- Update placement after the head pose each frame (`Application.onBeforeRender`, or LateUpdate
  as Worldfall does; for distant bodies the 1-frame translation error is negligible).
- Everything decorating a body (atmosphere shell radius and centre, rings, selection rim,
  labels, spacecraft) must use the same `P'` and scale `f(d)/d`. That is one more reason to keep
  honouring CLAUDE.md's rule of consulting `IsGeometryDrawn` and the drawn transform, never the
  raw radius.

**Depth precision with one camera, near 0.05 / far 1000 (ratio 2e4). Measure it, do not
assume.** Standard hyperbolic depth resolution is `Δz ≈ z²/(n·2^bits)`. At `z = 900 m`:
**247 m at 16 bits** (unusable: another reason to drop Depth16 submission), **0.97 m at 24 bits**,
and negligible with reversed-Z 32-bit float. An atmosphere shell sits about 1.6 % of R outside
its planet (0.13 m for a 1°-wide planet at 900 m). At 24 bits that is below the resolution,
but the atmosphere shader already clips analytically against its own planet
(`CosmoAtmosphere.shader:166-168`), so only occlusion by *other* bodies depends on the depth
buffer. Action: log `XRSettings.eyeTextureDesc.depthStencilFormat` in the `render:` line on
device. If it is not 32-bit float, choose `Dmax` so that bodies which must occlude each other
stay more than `Δz(Dmax)` apart. (All INFERRED; the eye depth format was not observable here.)

Put `f`, its inverse and the placement rule in **Core** (engine-free), with tests in
`tests/`: identity below `D0`, monotonicity, C¹ at `D0`, `R'/f = R/d` to 1e-12, and the bound
`< Dmax`. That keeps the project's rule that numbers are tested headlessly.

### 6.3 Every change required for the next APK

**P0: correct stereo rendering with passthrough**

1. Remove `DepthBands` (`Runtime/Rendering/DepthBands.cs`; `XrRig.cs:77-78`; `ScaleRenderer.cs:30,51,106-109`;
   `PlayModeShot.cs:391-394,552-553,764-765,811-812`; the `cams` expectation in
   `RenderDiagnostics.cs:32-34`). One XR camera: near 0.05, far ≈ 1000, clear SolidColor
   (0,0,0,0) in passthrough (`XrRig.cs:68-69,173`), never `Depth`.
2. Implement §6.2 scaled space (Core function + tests; `CelestialBodyView` placement and scale;
   `AtmosphereShell`, `SkyAndRings` rings and star sphere, `OrbitLineRenderer`, `BodySelector`
   rim, labels, spacecraft).
3. Render pipeline: URP asset created **in code** by an `XrSetup`-style editor step, following
   the project rule of no hand-copied settings YAML (CLAUDE.md). Settings: MSAA 4, HDR off,
   render scale 1, depth and opaque texture off, SRP batcher on, main light per-pixel, additional
   lights off. Assign it to `GraphicsSettings` and to the Android quality level. Shader ports:
   Standard → URP Lit/Simple Lit (`CelestialBodyView.cs:115-121,442-453,754-790`),
   `CosmoAtmosphere`/`CosmoRim` → URP HLSL per `GeoramaXR/CONTRACT-XR.md:93-135`, a new
   `CosmoSynth/Star` additive shader (`Blend One One, Zero One`, ZWrite Off) replacing
   `Particles/Standard Unlit` (`StarSprite.cs:141-151`). Keep or port the unlit, sprite and text
   built-ins.
   Replace always-included URP shaders with material templates in `Resources/` (one per keyword
   combination the runtime toggles: `_EMISSION`, `_NORMALMAP`, `_METALLICSPECGLOSSMAP`), cloned at
   runtime. `CosmoShaders.All` and `XrSetup.EnsureShadersIncluded` change accordingly.
4. OpenXR: `depthSubmissionMode = None` (`XrSetup.cs:281`; asset `:172`). Keep
   `optimizeBufferDiscards = true`, which is correct with one camera. `XrSetup` must **assert**
   `optimizeBufferDiscards == false` whenever more than one camera renders to the eye buffer.

**P1: parity with the proven configuration**

5. `latencyOptimization = PrioritizeInputPolling` (validator hint, `MetaQuestFeature.cs:697`).
6. Add `com.unity.openxr.feature.input.handtracking` to `XrSetup.DisabledAndroidFeatures`
   (it is enabled at `OpenXR Package Settings.asset:1166`). Consider whether
   `handinteraction`/`handinteractionposes` are used at all; no reference enables them.
7. Colour space Gamma → **Linear** (all references; standard for URP). Re-measure the
   `StarLook` constants (`StarLook.cs:52,69,83`), which were tuned on gamma captures.
8. `androidUseSwappy = 0` (`ProjectSettings.asset:74`).
9. Do not add `ProvenanceHud` when XR is active (`CosmoSynthBootstrap.cs:171`).
10. With URP the pipeline asset governs MSAA. Still set the Android quality level to
    pixelLightCount 0–1, shadows 0, vSync 0, like the references.
11. Optional after measuring frame time: `eyeTextureResolutionScale` and a 90 Hz request, as
    GeoramaXR does (`GeoramaXR/Assets/Georama/Runtime/XrRigBoot.cs:31,54-58`); `targetSdk 34`
    before any store upload.

**P2: hygiene (no render effect)**

12. Delete the empty `Assets/XR 1..3` and `Assets/XR/Settings 1..3` (plus `.meta`), the orphan
    `Assets/XR/Resources/XRSimulationRuntimeSettings.asset`, and the 4 orphan `XRManagerSettings`
    sub-assets. Make `XrSetup.EnsureLoader` stop creating a new manager on every run. Keep the
    "exactly one MetaQuestFeature" check and never blank `EditorBuildSettings.m_configObjects`.
13. Git tracks 56 generated files under `unity/.utmp/RelWithDebInfo` (`git ls-files`). Ignore
    and untrack them.
14. Doc drift: CLAUDE.md "XR / Quest configuration" (Built-in, `Shader.Find` in
    `SceneShot.cs`/`CelestialBodyView.cs`), `docs/architecture.md:137-139` ("renders one band",
    which is wrong: two), `docs/roadmap-universe.md:48-55` (Phase 0), the `DepthBands` docstring,
    `ScaleSpace.cs:28-35` (multi-pass bands). Rename or annotate the mislabeled
    `v1.7.2__…png`.

---

## 7. Verification that runs on this headless box

What cannot be done here (VERIFIED): the OpenXR **Mock Runtime** ships Windows, macOS and Android
binaries only (`Runtime/MockRuntime/{windows,osx,android}`), and uses the `XR_UNITY_null_gfx`
extension, so it renders nothing. `com.unity.xr.mock-hmd` is in no package cache on the box, and
its Linux support is unverified. The Android emulator path (`apk-shot`) cannot run OpenXR apps
(GeoramaXR CONTRACT §0). llvmpipe has no tile memory, so **the 1.7.0 failure itself is not
reproducible here**. What can stop it is structural gates, V1–V5 below.

### V1. Shader stereo gate (static; seconds)
```bash
# fails if any shader with a vertex program lacks a stereo macro
cd ~/projects/CosmoSynth/unity/Assets && fail=0
while IFS= read -r -d '' f; do
  grep -q '#pragma vertex' "$f" || continue
  for m in 'multi_compile_instancing' UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO \
           UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX; do
    n=$(grep -c "$m" "$f"); [ "$n" -gt 0 ] || { echo "MISSING $m in $f"; fail=1; }
  done
done < <(find . \( -name '*.shader' -o -name '*.hlsl' \) -print0)
[ $fail -eq 0 ] && echo "stereo gate OK"; exit $fail
```
It passes today on both shaders. Add a self-test: run it on a scratch copy with one macro
removed, and it must fail.

### V2. Settings gate (static python over YAML; seconds)
Assert: `GraphicsSettings.m_CustomRenderPipeline` is a URP asset whose `m_MSAA` is 4; the Android
quality level's `customRenderPipeline` matches; OpenXR Android `m_renderMode 1`,
`m_depthSubmissionMode 0`, `m_latencyOptimization 1`; exactly one `MetaQuestFeature`; the
feature ids that must be off are off (`input.handtracking`, `foveatedrendering` unless the
pipeline supports it); `m_ActiveColorSpace 1`; no `Assets/XR [0-9]*` or `Assets/XR/Settings [0-9]*`
directories. The comparison script used for §4 (`/tmp/rxa_cmpxr.py`, `/tmp/rxa_cmpps.py` on
triton, ephemeral) shows the parsing approach. Diff against Microverse's values, not a
hand-kept list.

### V3. Code gate (static grep over `Assets/CosmoSynth/Runtime`)
Exactly one `AddComponent<Camera>` (XrRig). No `CopyFrom(`, no `CameraClearFlags.Depth`/`.Nothing`,
no `targetTexture =`, no `Graphics.DrawMesh|Graphics.Blit|GL\.|CommandBuffer|OnRenderImage`. Each
hit must be justified in a comment or the gate fails. Today it would flag `DepthBands.cs:67,68,78,106`.

### V4. Play-mode assertions (existing `~/server-setup/playshot-cosmosynth.sh` harness)
Extend the `PlayModeShot` report to FAIL, not just print, when:
- enabled cameras with `targetTexture == null` ≠ 1;
- the XR-origin camera's clear is not SolidColor with α 0 while passthrough is requested;
- any drawn renderer's distance from the eye exceeds `0.9·far`;
- for every compressed body, `|R'/f(d) − R/d| > 1e-6` (angular size not preserved).

Also add a **synthetic stereo pair**: two renders with the camera offset ±32 mm along its right
axis, checking that compressed geometry is identical for both eyes (compression is about the
centre eye). This proves scene-level stereo consistency, not the driver path.

### V5. APK artefact gate (seconds; adopt `~/projects/Worldfall/tools/verify-apk.sh`)
Add to the existing checks (VR category via xmltree, boot.config `xrsdk-pre-init-library`,
`xr-use-vulkan-offscreen-swapchain`, `libUnityARFoundationMeta.so`, arm64 only, self-test of
the match helper):
- **boot.config exact diff** against a known-good APK, where only `build-guid` and
  `gc-max-time-slice` may differ (§5.1 shows today's diff);
- **baked QualitySettings scan** (Appendix B), with its YAML self-check. For Built-in builds:
  FAIL if the Android level has `antiAliasing > 1` while the runtime code gate (V3) finds more
  than one camera;
- `targetSdkVersion ≤ 34` warning, and no `libswappywrapper.so` once Swappy is off.

### V6. On device (the only real proof)
Before any code change, the recovered 1.9.0 APK is installable today: its `render:` line on the
status card (`RenderDiagnostics.OneLine`) answers whether the MSAA revert fixed the grid. It
should read `SinglePassInstanced W×H:2 msaa 0/1 cams 2 q2`. For the next APK, extend the line
with: pipeline name, cameras rendering to the eye buffer, XR camera clear flags and α,
`eyeTextureDesc.depthStencilFormat`, depth-submission mode, colour space, and URP MSAA. Log it
once to logcat (already done by `LogOnce`).

---

## 8. Residual risks and open questions

- The mechanism in §2.2 is INFERRED. The ingredients and the timeline are VERIFIED. **One
  headset test of 1.9.0** (V6) turns it into a fact.
- Eye buffer size and depth format on Quest 3 were not observable here. The bandwidth numbers
  in §6.2 use 2064×2208, which was only an illustrative string in the 1.7.2 notes.
- Whether Unity's `CopyFrom` plus child parenting would ever mis-pose under XR-SDK is INFERRED
  safe. It becomes moot when `DepthBands` is deleted.
- Colour-space and URP changes will shift every visual tuning constant. Plan a re-measurement
  pass with the play-mode harness (the project's "print the pixels" practice).

---

## Appendix A: commands run (all read-only)

- `git log --format='%H %ad %s' --date=iso; git show --stat <each>`; `git show c450708:unity/ProjectSettings/QualitySettings.asset`
- Transcript search: `/tmp/rxa_tx.py`, `/tmp/rxa_txr.py` (json line scan for "100 plus", "antiAliasing|MSAA", `BUILD_OK …1.7.0`)
- `grep -rnE "Graphics\.(DrawMesh|Blit…)|GL\.(…)|CommandBuffer|targetTexture|AddComponent<Camera>|CopyFrom|stereoTargetEye|…" Runtime`
- `find Assets Packages -name '*.shader' -o -name '*.cginc' -o -name '*.hlsl' -o -name '*.shadergraph' -o -name '*.compute'`
- `/tmp/rxa_cmpxr.py` (OpenXR settings/features across 5 projects), `/tmp/rxa_cmpps.py` (Player/Quality/Graphics/manifest table)
- `/tmp/rxa_apkinspect.sh` (aapt2 badging + xmltree, `unzip -p assets/bin/Data/boot.config`, lib listing for 4 APKs), `apksigner verify --print-certs`
- `/tmp/rxa_qmsaa.py` (Appendix B) on CosmoSynth 1.9.0 and Microverse 0.1.6
- `md5sum` of the release shots

## Appendix B: baked QualitySettings scan (self-checking)

```python
"""Per-level QualitySettings antiAliasing from a built APK's globalgamemanagers (no UnityPy).
Finds each level's length-prefixed name, aligns to 4, reads fields in QualitySettings.asset
order. Self-check: every level must reproduce the project YAML or the scan does not count."""
import struct, sys, zipfile, re
apk, yaml = sys.argv[1], (sys.argv[2] if len(sys.argv) > 2 else None)
data = zipfile.ZipFile(apk).read('assets/bin/Data/globalgamemanagers')
names = ['Very Low', 'Low', 'Medium', 'High', 'Very High', 'Ultra']; found = {}
for n in names:
    key = struct.pack('<I', len(n)) + n.encode(); i = data.find(key)
    while i >= 0:
        p = (i + len(key) + 3) & ~3
        ints = struct.unpack_from('<5i', data, p); floats = struct.unpack_from('<6f', data, p + 20)
        rest = struct.unpack_from('<6i', data, p + 44)
        # pixelLights, shadows, shadowRes, shadowProj, cascades | dist, nearOff, 2split, 4split xyz
        # | shadowmaskMode, skinWeights, mipLimit, mipSettingsCount, aniso, antiAliasing
        if (0 <= ints[0] <= 8 and ints[1] in (0, 1, 2) and 0 <= ints[2] <= 3 and ints[3] in (0, 1)
                and ints[4] in (1, 2, 4) and 0.0 <= floats[0] <= 1e4 and rest[1] in (0, 1, 2, 4, 255)
                and rest[3] == 0 and 0 <= rest[4] <= 2 and rest[5] in (0, 1, 2, 4, 8)):
            found[n] = dict(pixelLightCount=ints[0], antiAliasing=rest[5]); break
        i = data.find(key, i + 1)
for n in names: print(n, found.get(n))
if yaml:
    t = open(yaml).read(); ok = True
    for n, body in re.findall(r'    name: (.*?)\n(.*?)(?=\n    name: |\n  m_TextureMipmapLimitGroupNames|\n  m_PerPlatformDefaultQuality)', t, flags=re.S):
        aa = int(re.search(r'antiAliasing: (\d+)', body).group(1)); pl = int(re.search(r'pixelLightCount: (\d+)', body).group(1))
        g = found.get(n); ok &= bool(g and g['antiAliasing'] == aa and g['pixelLightCount'] == pl)
    print('SELF-CHECK', 'PASS' if ok else 'FAIL')
```
Result 2026-09-11: CosmoSynth 1.9.0 → L2 Medium `antiAliasing 0, pixelLightCount 1`, SELF-CHECK
PASS. Microverse 0.1.6 → L1 `antiAliasing 4`, SELF-CHECK PASS.
