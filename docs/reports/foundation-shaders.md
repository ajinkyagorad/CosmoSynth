# Lane report: `foundation-shaders`

Branch `lane/foundation-shaders`, worktree `~/projects/CosmoSynth-wt/foundation-shaders`.
Authority followed: `docs/CONTRACT.md` §7 and Appendix A (which supersedes the A-foundation
design where they differ), §2.2 queues, §2.6 groups, §7.4 colour.

**The one thing to read first:** not one of these twelve files has ever been compiled. There is
no editor in this lane, by design. Everything below is verified *statically*; §7 of this report
says exactly what remains unproven and what the build stage must check before any of it can be
believed.

---

## 1. What is in the tree

Eleven shaders plus one include, all under
`unity/Assets/CosmoSynth/Runtime/Shaders/`, all single-variant URP HLSL, all carrying the six
stereo macros, all `Fallback Off`, all with a `UnityPerMaterial` CBUFFER.

| Shader | File | Queue | Blend (colour, alpha) | ZWrite | ZTest | Cull | Writes passthrough alpha |
|---|---|---|---|---|---|---|---|
| `CosmoSynth/Sky` | `Sky.shader` | 1000 `Background` | `One Zero` | Off | LEqual | Off | yes, `= _Immersion` (0 in MR) |
| `CosmoSynth/Body` | `Body.shader` | 2000 `Geometry` | opaque | **On** | LEqual | Back | yes, 1 (correct: a body occludes) |
| `CosmoSynth/Unlit` | `Unlit.shader` | 2000 `Geometry` | opaque | **On** | LEqual | Back | yes, 1 |
| `CosmoSynth/Ring` | `Ring.shader` | 3000 | `One OneMinusSrcAlpha, One OneMinusSrcAlpha` | Off | LEqual | Off | coverage = opacity |
| `CosmoSynth/Volume` | `Volume.shader` | 3010 | `One OneMinusSrcAlpha, One OneMinusSrcAlpha` | Off | LEqual | **Front** | coverage = opacity |
| `CosmoSynth/Point` | `Point.shader` | 3050 | `One One, Zero One` | Off | LEqual | Off | **no** |
| `CosmoSynth/Ribbon` | `Ribbon.shader` | 3060 | `One One, Zero One` | Off | LEqual | Off | **no** |
| `CosmoSynth/Atmosphere` | `CosmoAtmosphere.shader` | 3100 | `One One, Zero One` | Off | LEqual | Off | **no** |
| `CosmoSynth/Rim` | `CosmoRim.shader` | 3100 | `One One, Zero One` | Off | LEqual | **Front** | **no** |
| `CosmoSynth/Panel` | `Panel.shader` | 3200 (accent material sets 3201) | `One OneMinusSrcAlpha, One OneMinusSrcAlpha` | Off | LEqual | Off | coverage = opacity |
| `CosmoSynth/Text` | `Text.shader` | 3210 | `One OneMinusSrcAlpha, One OneMinusSrcAlpha` | Off | LEqual | Off | coverage = opacity |

`Runtime/Shaders/CosmoCore.hlsl` is the shared include (contract A.2, verbatim): `CS_Length`,
`CS_CompressDistance`, `CS_CompressWS`, `CS_IcrfDirToWorld`, `CS_DisplayToLinear`,
`CS_PixelsPerRadian`, `CS_AngularToClip`, `CS_IGN`, and the three per-frame globals
`_CS_CentreEyeWS`, `_CS_Compress`, `_CS_IcrfToWorld`.

No pass uses `ZTest Always`. No pass uses `Blend SrcAlpha One`. Every additive pass returns
alpha 0 explicitly, and the only pass that writes coverage on purpose in MR is `Sky`, gated on
`_Immersion` which the layer sets to 0 and then skips the draw.

---

## 2. Per shader: inputs, cost, and what it needs from placement

Costs are **per covered pixel**, as written. None is measured.

### `CosmoSynth/Body` — opaque lit body
- **Inputs.** `_BaseMap` (sRGB albedo), `_NormalMap` (linear, `linear:true` at import),
  `_SurfaceMap` (linear, `a` = smoothness), `_BaseColor`, `_LightDirWS`, `_LightColor`,
  `_Ambient`, `_Emission`, `_Smoothness`, `_SpecGain`, `_Terminator`, `_PerVertexCompress`.
  Vertex: POSITION, NORMAL, TANGENT, TEXCOORD0.
- **Cost.** 3 texture samples, one `pow`, one `exp2`, two `normalize`. Opaque and depth-writing,
  so it pays once per pixel and shadows every transparent pass behind it.
- **Lighting is by uniform, not by a Unity light.** One camera sees several star systems at
  once; per-pixel light slots cannot express that and a single directional light gets the
  terminator wrong on every body but one. The CPU walks the parent chain to the host star.
- **Needs from placement.** `_PerVertexCompress = 1` for an `Envelope`-mode leaf and `0` for
  everything else, per frame, per material. `_LightDirWS` in world space and `_LightColor`
  linear. In Rigid mode the transform carries the group similarity and the shader is unaware.

### `CosmoSynth/Point` — additive point sprites
- **Inputs.** Per vertex: POSITION (chunk offset / object position / ICRF direction, by
  `_Space`), COLOR (display sRGB), TEXCOORD0 `(magnitude, corner 0..3, shape 0..3, hidden)`.
  Uniforms: `_EyeInChunk`, `_Units`, `_MagWindow`, `_SizeDmm`, `_PxBounds`, `_Shape`,
  `_ShapeGain`, `_Look`, `_Fade`, `_Space`, `_LayerToWorld`.
- **Cost.** Fragment is one `dot` and one `exp2` — trivially cheap; the whole cost is
  **overdraw**, budgeted at 0.5 eye-screens (§7.3). The vertex stage does the real work:
  one `CS_Length`, one `log10`, one `normalize`, one matrix multiply per vertex, 4 vertices
  per point.
- **The two bounds in `_PxBounds` are bug fixes, not knobs.** The minimum exists because a
  one-pixel feature has no shape: with a Gaussian profile the visible core is about half the
  quad, so a two-pixel quad renders square whatever texture it is cut from. The maximum exists
  because a floor with no ceiling is unbounded in the worst direction — the same rule applied
  to a 3 m lander inflated it past the size of Mars and gave a black screen with passthrough
  working and nothing in it. Both are in the shader, so no call site can omit either.
- **Needs from placement.** `_CS_CentreEyeWS` and `_CS_Compress` published before the layer
  draws. Per-chunk `_EyeInChunk` in that chunk's units, and `_Units` = (metres per unit,
  parsecs per unit). `_Space` = 0 layer / 1 object / 2 sky.

### `CosmoSynth/Ribbon` — screen-constant-width line
- **Inputs.** Per vertex: POSITION (this endpoint), TEXCOORD0 (the other endpoint), TEXCOORD1
  `(side -1/+1, order +1/-1)`, COLOR (display, `a` = tail gradient). Uniforms: `_WidthDmm`,
  `_Intensity`, `_Fade`, `_Placement`.
- **Cost.** Fragment is two `saturate` and an `abs` — the anti-aliasing is analytic, computed
  from the signed pixel distance to the centre line. That is deliberate: **MSAA cannot be
  relied on for hairlines**; a line thinner than the sample grid breaks into dashes, and a 4x
  pattern cannot represent a third of a pixel of coverage at all. Sub-pixel lines fade rather
  than dropping out (`coverage *= saturate(halfPx * 2.0)`).
- **Near-plane clipping is in the vertex shader**, against a plane at 0.06 m: a segment with one
  endpoint behind the eye otherwise swings across the whole screen.
- **Needs from placement.** `_Placement` = 0 rigid / 1 per-vertex / 2 sky, per owner per frame.
  Mode 2 reads `_CS_IcrfToWorld` and `_CS_Compress.z`. The legibility cull ("only from outside,
  only if bigger than a dot") stays on the CPU and is **not** in this shader.

### `CosmoSynth/Sky` — inside-out sky sphere
- **Inputs.** `_SkyCube` (6 x 1024² R8, linear, faces in **ICRF components**), `_Lut` (sRGB
  256x1), `_Contrast`, `_Immersion`. Mesh: unit sphere whose object-space vertices **are** ICRF
  directions, identity transform, huge bounds.
- **Cost.** 1 cube sample + 1 LUT sample, full-screen when drawn; budgeted 1.0 eye-screens.
  Drawn **first**, `ZWrite Off`, so it costs nothing in depth and everything overwrites it.
- **Zero disparity by construction.** The eye anchor is applied in the **vertex** stage, which
  under multiview runs per eye, so the sphere sits at the same angular position in both eyes:
  no stereo disparity and no head-motion parallax, which is what infinity looks like.
- **The ICRF lookup keeps the axis mapping in one place.** Sampling by the ICRF direction rather
  than a Unity axis means the cube lookup needs no mapping at all, so the single ICRF→Unity
  mapping stays inside `FloatingOrigin` as the contract requires.
- **Needs from placement.** `_CS_IcrfToWorld` and `_CS_Compress.z`. `_Immersion` = 0 in MR, and
  the layer should **skip the draw entirely** at 0 rather than draw a transparent sphere.

### `CosmoSynth/Volume` — bounded raymarch
- **Inputs.** `_Density` (R8 3D, log-encoded), `_Ramp` (sRGB 256x1), `_Decode`
  `(vmin, ln(vmax/vmin), extinction, ramp scale)`, `_Steps` `(N, empty cut)`, `_Fade`.
  Mesh: unit cube `[-0.5, 0.5]³`.
- **Cost.** N 3D samples + up to N ramp samples per pixel, N from the CPU governor
  `N = clamp(B / coverage, 16, 64)`, B = 30. Three early-outs keep the average far below N:
  the slab test rejects missed pixels for ~10 instructions, `_Steps.y` skips empty voxels
  before the exp, and `alpha > 0.985` breaks out of dense ones.
- **The per-eye ray origin is the whole point.** `GetCameraPositionWS()` is read **after**
  `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`. Read before it, both eyes march from the left
  eye's origin and the volume has no stereo depth at all — and it would look plausible in every
  mono desktop capture.
- **Blue-noise jitter.** `CS_IGN(i.positionCS.xy)` offsets the first step per pixel, turning
  banding into fine noise, which is far cheaper than raising N.
- **Needs from placement.** The transform is the group placement (`Rigid` or `Enclosing`) with
  the layer frame's rotation. A similarity about the centre eye leaves every eye at the same
  object-space point, so compression never touches the integral — nothing in this shader reads
  `_CS_*` at all. `_Steps.x` per frame from the step-coverage governor.

### `CosmoSynth/Ring`
- **Inputs.** `_BaseMap` (sRGB RGBA, sampled by radius), `_Radii` (inner, outer, object units),
  `_LightDirWS`, `_LightColor`, `_Ambient`, `_ParentCentreWS` (xyz centre, **w = the parent's
  DRAWN radius**), `_Fade`, `_PerVertexCompress`.
- **Cost.** 1 texture sample plus an analytic ray-sphere test (3 dots). Cheap; it is a large
  transparent area, so it costs coverage, not arithmetic.
- **The shadow radius must be the drawn radius.** Every rigid group renders at its own
  similarity; the physical radius would cast a shadow the wrong size at every scale but one.
- **Needs from placement.** `_ParentCentreWS` in the same space the ring geometry is in, updated
  every frame, with `w` taken from the parent view's **drawn** radius; `_PerVertexCompress`
  mirroring the parent body's Envelope flag (rings are a body's decorations, §2.6).
- **Ring geometry is assumed to lie in the object XZ plane** (`length(positionOS.xz)`). If the
  ring mesh is built in XY instead, this samples the texture along the wrong axis — see §6.

### `CosmoSynth/Unlit`, `CosmoSynth/Panel`, `CosmoSynth/Text`
- **Unlit.** `_BaseColor` only; opaque, alpha 1. Beacon bars and the pointer ray.
- **Panel.** `_BaseMap.a` × vertex-colour alpha, premultiplied, `_BaseMap_ST` in the CBUFFER.
  One shader for both queues: the accent **material** sets `renderQueue = 3201`, so there is no
  second shader and no second variant.
- **Text.** Same as Panel but `_MainTex`, deliberately named `_MainTex` and not `_BaseMap`,
  because Unity's own `Font.material` assigns the atlas to `_MainTex` and `CosmoText` clones
  that material onto this shader. The dynamic font atlas is **rebuilt at runtime** when a new
  glyph appears; `CosmoText`'s existing `Font.textureRebuilt` handling must be kept or the
  labels turn to garbage the first time an unseen character is drawn.
- All three: 0–1 texture samples, negligible arithmetic; budgeted 0.3 eye-screens together.

### The two ports
Maths unchanged, line for line. What changed in both: `CGPROGRAM`→`HLSLPROGRAM`,
`UnityCG.cginc`→`CosmoCore.hlsl`, `UnityObjectToClipPos`→`TransformObjectToHClip` /
`TransformWorldToHClip`, `_WorldSpaceCameraPos`→`GetCameraPositionWS()` (still read after the
eye macro), properties moved into `CBUFFER_START(UnityPerMaterial)`, and the two URP tags
`"RenderPipeline"="UniversalPipeline"` + `"LightMode"="UniversalForward"` — **without which URP
does not draw the pass at all**, so the shader is silently absent rather than visibly wrong.

- **`CosmoAtmosphere`** additionally: `VFACE` → URP's `FRONT_FACE_TYPE`/`FRONT_FACE_SEMANTIC`/
  `IS_FRONT_VFACE` macros (the raw `VFACE` semantic is not correct on every backend, and the
  duplicate-rejection it feeds is what stops the limb being integrated twice); and
  `_PerVertexCompress`, the Envelope switch. **Envelope contract for the CPU:** when
  `_PerVertexCompress = 1`, supply `_PlanetCenter`, `_PlanetRadius`, `_AtmosphereRadius` and the
  rescaled coefficients in **true eye-relative space in units of the planet radius**; the shader
  then starts the ray at the origin. The view *direction* is identical either way, because
  compression is radial about the centre eye and preserves direction exactly.
- **`CosmoRim`** additionally: `_PerVertexCompress`, so the rim still hugs the silhouette of an
  Envelope-mode body. **Queue changed 3150 → 3100** to match the contract's §2.2 table.

---

## 3. The registry is now the source of truth

`CosmoShaders.All` is exactly the eleven names. The six built-in names are **out of `All`**, so
they are not in the player and `Shader.Find` returns null for each of them in a build.

They still exist as constants, each marked `[Obsolete]` with the replacement named. That is
deliberate: seven call sites in five other lanes still ask for them, and deleting the constants
would simply fail to compile. The attribute turns the migration list into compiler output
instead of prose in a report that goes stale. **Delete each constant as its last caller moves.**

`CosmoShaders.Material()`'s fallback changed from `Standard` to `CosmoSynth/Unlit`. The old
fallback had the same failure as the thing it was catching: under URP, `Standard` is missing too.

Verified statically on this tree: **11 registry names ↔ 11 `Shader "..."` declarations on disk,
exact agreement in both directions** — no name in `All` without a file, no file without a name
in `All`. Also verified: every non-texture property of every shader is inside
`CBUFFER_START(UnityPerMaterial)` (an SRP-Batcher break is silent), every shader includes
`CosmoCore.hlsl`, has a `LightMode` tag, and has `Fallback Off`.

---

## 4. Gate change — please review, it is outside this lane's ownership

`tools/gates/stereo_gate.sh` had **two false positives that rejected the contract's own verbatim
shaders**. G0b could not pass on a correct tree. Both are gate bugs; the rules they enforce are
right, the regexes were not.

1. The additive detector matched `One` by **prefix**, so `Blend One OneMinusSrcAlpha` —
   premultiplied over, the correct and *required* blend for queues 3000/3010/3200/3210 — was
   reported as an unguarded additive. It failed `Ring`, `Volume`, `Panel`, `Text`.
2. The "guarded" pattern was anchored at end of line, so a trailing `// ...` comment on the
   `Blend` line failed it. Contract Appendix A.4's verbatim `Point.shader` carries exactly such
   a comment. It failed `Point`.

Fix: require `One` to be a whole token, and let the guarded pattern end at `(space|end)` rather
than `(optional space, end)`. `Zero OneMinusSrcAlpha` still cannot satisfy it by prefix, because
the character after `One` is then `M`.

Three mutants added to the self-test, because a loosened rule is a hole until proven otherwise:
`additive writes alpha, commented` (proves fix 2 did not create the hole),
`premultiplied turned additive` (proves a premultiplied pass is still judged, not waved through),
and the fixture itself now contains both a commented additive pass and a premultiplied pass so
the good case covers both shapes. **All ten mutants fail as they must; `SELF-TEST PASS`.**

If the integrator would rather own this change, revert the file and re-apply it there — but G0b
cannot be green on any contract-conformant tree until it lands somewhere.

---

## 5. Gate results on this worktree

```
G0a   test suite (floor 142)             PASS   142 passed, 0 failed
G0b   shader stereo macros               PASS   16 files ok
G0c   settings YAML                      FAIL   pre-existing (15) — not this lane
G0d   runtime code invariants            FAIL   pre-existing — not this lane
G0e   MonoScript filename/class          FAIL   pre-existing (Provenance.cs, Json.cs)
G0f/g/h                                  ABSENT — gates do not exist yet
```

`code_gate` and `class_gate` failure counts are **identical to `main`** (8 and 3): this lane adds
no new failures. G0d explicitly reports `ok  Shader.Find( only in CosmoShaders.cs`.

G0a passing is the only evidence that `CosmoShaders.cs` compiles — including the `[Obsolete]`
constants against their seven existing callers.

---

## 6. Every shader here has never been compiled. What the build stage must check

All twelve files. There is no editor in this lane and none may be started. Ordered by how
quietly each one fails.

1. **It compiles at all, for Android, under URP 17.5, in the one editor.** Twelve files,
   including `CosmoCore.hlsl`. Every URP function used is spelled from the package's own
   headers, but that is a reading, not a compile: `TransformWViewToHClip`, `GetOddNegativeScale`,
   `UnpackNormalRGBNoScale`, `IS_FRONT_VFACE`, `SAMPLE_TEXTURE3D_LOD`, `SRGBToLinear`,
   `UNITY_MATRIX_P._m11`, `_ScreenParams`.
2. **`Shader.Find` returns non-null for all eleven names in a PLAYER build, not the editor.**
   This is the failure that shipped the all-magenta build. The editor always has every shader
   loaded, so an editor check proves nothing. `XrSetup` must write `CosmoShaders.All` into
   Always Included Shaders, and the check must run in the player.
3. **`aapt`/`unzip` the APK and confirm the shader blobs are actually in it.** Item 2 checked in
   the editor is the verification gap in its purest form.
4. **The destination-alpha probe, with attribution.** `PlayModeShot.ProbeDestinationAlpha` with
   alpha cleared to 0. Coverage from `Body`, `Unlit`, `Ring`, `Volume`, `Panel`, `Text` is
   correct; **any** coverage from `Point`, `Ribbon`, `Atmosphere` or `Rim` is a bug. Attribute
   with `COSMOSYNTH_HIDE`, never by reading the total.
5. **Stereo, per eye, from the device.** G0b proves the macros are *present*, not that they
   *work*. The three that would look perfect in any mono capture and wrong only in a headset:
   `Volume`'s per-eye ray origin, `Sky`'s per-eye anchor, `Point`'s sky mode. A mono screenshot
   cannot distinguish correct from broken for any of them.
6. **Pixel bounds on `Point`, measured by printing a star's pixel neighbourhood** — not by
   looking at a thumbnail, at which the failure and the fix are indistinguishable. Confirm no
   star is under `_PxBounds.x` px and none over `_PxBounds.y`.
7. **The two colour-space questions**, both flagged in comments in the files:
   - `Unlit` decodes `_BaseColor` with `CS_DisplayToLinear` because contract A.8 says so, while
     §7.4 says Unity linearises `SetColor` itself — and marks that **INFERRED**. If §7.4 is
     right, beacons and the pointer ray come out too dark by one sRGB curve. **One capture
     settles it.** I left A.8's explicit instruction in place and did *not* propagate it: `Rim`
     ("maths unchanged") and `Ring` (sRGB texture, decoded by the sampler) do not decode.
   - Overlapping `Point` quads (core, core, halo) now sum in **linear** and will read brighter
     than the gamma-era numbers. Tune `_Look.y` first, before touching anything else.
8. **SRP Batcher actually engages** (Frame Debugger). Verified statically that every non-texture
   property is in `UnityPerMaterial`; that is necessary, not sufficient.
9. **`Ring` geometry is in the object XZ plane.** `Ring.shader` samples `_BaseMap` by
   `length(positionOS.xz)`. If `RingSystem` builds the mesh in XY, the texture is sampled along
   the wrong axis and the rings come out as a flat wash. One line either way; it needs checking
   against the mesh builder, not assuming.
10. **`Volume` step budget B = 30 is unmeasured on Quest 3** (§7.3, §13). It needs a
    Fractarium-style cost probe on device before it is trusted.

---

## 7. Requests to the `foundation-code` lane and others

**Blocking — must land in the same build as this lane, or labels ship pink.**

1. **`CosmoText.cs:78` must clone the font material onto `CosmoSynth/Text`** (owner: `ui`).
   `GUI/Text Shader` is now out of `CosmoShaders.All`, so it is stripped from the player. Today
   `CosmoText.TextMaterial` clones `Font.material`, which uses `GUI/Text Shader`. Ship this lane
   without that change and **every label and panel renders magenta on the device** while the
   editor shows it working. This is the single highest-risk item in the report.

**Required for any of these shaders to be correct on screen.**

2. **`Placement.PublishGlobals` must set `_CS_CentreEyeWS`, `_CS_Compress`, `_CS_IcrfToWorld`
   as global shader values, before any layer draws** (LateUpdate order 50, per §2.8).
   `Point`, `Ribbon` (modes 1 and 2), `Sky`, `Body`/`Ring`/`Rim`/`Atmosphere` in Envelope mode
   all read them. Unset, they are zero: `_CS_Compress.x = 0` makes `CS_CompressDistance`
   take the logarithmic branch for every distance and `log(0)` for a point at the eye. Nothing
   about this failure names the cause.
3. **A per-material `_PerVertexCompress` write, every frame, from the group solver**, on the
   body's material *and its decorations'* (`Ring`, `Rim`, `Atmosphere`). §2.6 calls an Envelope
   group "a leaf (one body and its decorations)"; if the decorations keep `0` while the body
   gets `1`, the atmosphere and rim detach from the surface they belong to.
4. **`_LightDirWS` / `_LightColor` per body, resolved through the parent chain to the host
   star.** `Body` and `Ring` are not lit by any Unity light and will be flat black without it.
5. **`Ring._ParentCentreWS.w` must be the parent's DRAWN radius**, and the caller must consult
   `CelestialBodyView.IsGeometryDrawn` first — the stale-`RenderedRadius` trap in CLAUDE.md is
   exactly this shape, and here it would cast a planet shadow the size of the sky.
6. **`Volume._Steps.x` from the step-coverage governor** `N = clamp(30 / coverage, 16, 64)`.
   Left at the default 32 it ignores screen coverage entirely and blows the raymarch budget the
   moment the dust cube fills the view.
7. **`Sky` layer must skip the draw when `_Immersion == 0`**, not draw a transparent sphere: at
   queue 1000 with `Blend One Zero` it would overwrite the passthrough clear with zeros.
8. **Meshes this lane assumes and does not build.** `Point`: 4 vertices per point, `TEXCOORD0 =
   (magnitude, corner 0..3, shape, hidden)`, corner bit 0 = x sign and bit 1 = y sign.
   `Ribbon`: 4 vertices per segment, `TEXCOORD0` = the other endpoint, `TEXCOORD1 = (side, order)`.
   `Sky`: unit sphere, ~2 k vertices, object-space positions **are** ICRF directions, identity
   transform, huge bounds. `Volume`: unit cube `[-0.5, 0.5]³`. `Ring`: ring plane in object **XZ**.
9. **Delete the six `[Obsolete]` constants in `CosmoShaders.cs` as their callers migrate**
   (`CelestialBodyView.cs:442`, `CosmoTheme.cs:229,240`, `OrbitLineRenderer.cs:82`,
   `SkyAndRings.cs:514`, `StarSprite.cs:143`, `AnchorBeacon.cs:65`, `BodySelector.cs:70`).
   The compiler prints the remaining list on every build.
10. **A G0-class check that `CosmoShaders.All` and the `Shader "..."` declarations on disk agree
    in both directions.** This lane verified it with a throwaway script; it deserves to be a
    gate, because a name in `All` with no file is a guaranteed null in the player and a file
    with no name in `All` is guaranteed stripped. Not built here — `tools/gates/**` is not this
    lane's to own.
