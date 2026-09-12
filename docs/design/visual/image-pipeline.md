# The image pipeline — the response curve that turns dots on black into a sky

Design pass, 2026-09-12, slug `image-pipeline`. Read-only: no code, shader, setting or datum was
changed, no editor launched, no build run. The only file written is this one.

**Confidence.** **VERIFIED** = I read the file (path:line given) or ran the command on triton in
this session. **INFERRED** = arithmetic or reasoning from verified facts. Where I am guessing at a
cost I say so and name the experiment that settles it.

Target: CosmoSynth 2.0 on Unity 6000.5.5f1, URP **17.5.0** (VERIFIED, `package.json`), Quest 3,
one camera, Linear, MSAA 4× from the pipeline asset, SRP foveation, depth submission None,
symmetric projection, passthrough-first. Budget 11.0 ms GPU per eye at 72 Hz, 150 draws, 1.2 M
vertices, 33 step-coverages, 150 MB textures (CONTRACT §7.3).

---

## 0. The answer in one page

| # | Question | Decision | Cost |
|---|---|---|---|
| a | HDR eye buffer | **No.** The Quest eye swapchain is 8-bit; "HDR" can only mean an intermediate RT that is tone-mapped back down, and with passthrough alpha it is forced to `R16G16B16A16_SFloat` (8 B/px). Store+load of that intermediate is the whole cost of the feature. | **+4.5 ms** (INFERRED, §3.3). Rejected. |
| b | Tone mapping | **In-shader, per source, at the point of emission.** Extended Reinhard on luminance, `f(c) = c(1+c/W²)/(1+c)`, W = 4. Not URP's post stack: `UberPost` returns `half4(color, 1)` unless `_ENABLE_ALPHA_OUTPUT`, which **kills passthrough everywhere** (VERIFIED, §4.4). ACES never — it takes `log10` of the input (VERIFIED `ACES.hlsl:494`) and is graded for a 100-nit dim cinema surround, not a lit room. | 4 ALU/fragment. **+0.03 ms** |
| c | Bloom | **Geometric, not post.** The halo and spike quads already in `Point.shader` carry the highlights; the gate is the existing `saturate(2b−1)`, which with the new response lands on the brightest **6.3 %** of the naked-eye sky (679 of 8,920, **measured on this box**). URP bloom is 11 render passes plus the HDR intermediate. | **+0.02 ms**, 0 new draws, ≤ 43,828 star vertices. URP bloom: **+1.5–3 ms** on top of the 4.5. Rejected. |
| d | Exposure | **A generated per-rung table, plus a bounded ±1.5-mag trim that ships off.** `m0` (the apparent magnitude that maps to display 1.0) for point sources; `μ0` (mag/arcsec²) for extended ones. The 20 orders of magnitude are in the *luminance*; only about **8** are in the *surface brightness*, which is what a pixel shows (§6.1). | 1 `Shader.SetGlobalVector` per frame. **0 ms** |
| e | Grading LUT | **No LUT.** Grading cannot be applied per-source correctly (`grade(a)+grade(b) ≠ grade(a+b)`), and the post pass that could apply it is rejected in (a). Grade the *authored constants* on the CPU instead: zero runtime cost. | 0. Costed anyway in §7. |
| f | Banding / dither | **Yes, and hand-written.** Triangular-PDF IGN dither on luminance, in linear, amplitude = the local slope of the sRGB curve, **clamped so it is exactly 0 at black**. URP's own `ApplyDithering` would lift half the black pixels to code 1 and **haze the room** (§8.2). Applied in 5 of 11 shaders. | **+0.10 ms** worst case |
| g | MR | The compositor is **premultiplied over**: `out = layer.rgb + room.rgb·(1 − layer.a)`. A non-zero rgb at alpha 0 is *added to the room*. Every operator in this pipeline therefore must satisfy `f(0) = 0` exactly and must never touch alpha. | — |
| — | **Total added** | | **+0.15 ms of 11.0** |

The single structural rule that makes all of this work: **accumulate inside a shader, never in the
framebuffer.** A response curve applied per source is only wrong where two sources overlap. This
design keeps overlaps sub-unity by construction (§5.4), which is why it needs no HDR buffer.

---

## 1. What was read and run

| Source | Gave | Status |
|---|---|---|
| `Library/PackageCache/com.unity.render-pipelines.universal@4cbcec34e716` (17.5.0) | `MakeRenderTextureGraphicsFormat`, `UberPost.shader`, `Bloom.shader`, `BloomPostProcessPass.cs`, `ColorGradingLutPass.cs`, `Bloom.cs` | VERIFIED |
| `com.unity.render-pipelines.core@bf8191d734cd` | `Color.hlsl` (`NeutralTonemap`), `ACES.hlsl`, `FoveatedRendering.hlsl` | VERIFIED |
| `lane/foundation-shaders` @ `6e0e3be` | `CosmoCore.hlsl` (57 lines) and all eleven shaders; every blend mode and every `frag` return | VERIFIED |
| `lane/foundation-code` @ `6f19d5d` | `Placement.PublishGlobals`, `StarLook.cs` | VERIFIED |
| `SA/sky/stars.csstars` | **119,613 records**, magnitudes counted with `/tmp/magcount.py` (§5.3) | VERIFIED, run on triton |
| `refs/webxr/nebula-gallery/{README.md,index.html}` | the L^0.28 response, `1.15·b^0.23·uExpo`, `skyBoost`/`farAssist` | VERIFIED |
| `docs/CONTRACT.md` §2.1, §2.2, §7.3, §7.4, §5.4 rungs, §8.1 | budget, queues, settings, the rung table | VERIFIED |
| `docs/research/2026-09-11-{quest-rendering-techniques,render-xr-audit}.md` | Meta's store/load figures, GMEM, the 1.7.0 mechanism, the sRGB vertex-colour trap | VERIFIED (they quote Meta docs; those quotes are secondary) |
| `~/projects/Worldfall/.../WorldfallUrpConfig.cs` | `m_SupportsHDR 0`, `postProcessData = null` — headset-proven | VERIFIED |

No web fetches. Nothing on the box was modified.

---

## 2. The two facts that decide everything

### 2.1 The eye buffer is 8-bit, and that is not negotiable from inside the app

Meta documents that Optimize Buffer Discards "saves ~90 MB per eye at 4× MSAA 1680×1760"
(quoted in `quest-rendering-techniques` §10, secondary). Check the arithmetic:

```
1680 × 1760 = 2,956,800 px
× 4 samples × (4 B colour + 4 B D24_UNORM_S8) = 94,617,600 B = 90.2 MiB     ✓
```

It matches to three figures **only if the colour attachment is 4 bytes per pixel** — i.e.
`R8G8B8A8_SRGB`. (VERIFIED arithmetic against a secondary quote; INFERRED conclusion.) The
compositor swapchain the app submits to is 8-bit. There is no HDR display path here.

So "HDR eye buffer" in this project can only ever mean: render to a floating-point *intermediate*,
then tone-map it back down to the 8-bit swapchain. That is a feature with a price, not a free
precision upgrade, and §3 prices it.

**Settle it on device:** log `XRSettings.eyeTextureDesc.graphicsFormat` and `.depthStencilFormat`.
`RenderDiagnostics` already prints `WxH` and `msaa <urp>/<eyeSamples>`; add the two formats to the
`render:` line (CONTRACT §11.6 format string).

### 2.2 The compositor is premultiplied over, so colour at alpha 0 is added to the room

The queue table (CONTRACT §2.2) has additive passes at `Blend One One, Zero One` writing alpha 0,
and premultiplied passes where "coverage = opacity". That is the premultiplied-over composite:

```
displayed = layer.rgb + room.rgb · (1 − layer.a)
```

An additive star at alpha 0 therefore *adds* its light on top of the room — which is the feature.
The corollary is the trap: **any operator that writes a non-zero colour where nothing was drawn
hazes the entire passthrough room with a flat grey.** Three candidate operators in this brief do
exactly that, and §4.4, §7 and §8.2 catch each one.

Every function in this pipeline must satisfy **`f(0) = 0` exactly, in fp16**, and must leave alpha
alone. That is the acceptance test, and it is checkable headlessly.

---

## 3. (a) HDR eye buffer — no

### 3.1 What format is available on this path (VERIFIED)

`UniversalRenderPipelineCore.cs:1539-1552`:

```csharp
internal static GraphicsFormat MakeRenderTextureGraphicsFormat(bool isHdrEnabled,
        HDRColorBufferPrecision requestHDRColorBufferPrecision, bool needsAlpha)
{
    if (isHdrEnabled)
    {
        if (!needsAlpha && requestHDRColorBufferPrecision != HDRColorBufferPrecision._64Bits
            && SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, ...))
            return GraphicsFormat.B10G11R11_UFloatPack32;
        if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, ...))
            return GraphicsFormat.R16G16B16A16_SFloat;
        ...
```

and `UniversalRenderPipeline.cs:1518-1526`:

```csharp
bool needsAlphaChannel = Graphics.preserveFramebufferAlpha;
...
cameraData.isAlphaOutputEnabled = GraphicsFormatUtility.HasAlphaChannel(...graphicsFormat);
```

The cheap mobile HDR format, `B10G11R11_UFloatPack32`, **has no alpha channel**. This app's alpha
channel *is* passthrough coverage. So enabling HDR here forces either
`Graphics.preserveFramebufferAlpha = true` or `HDRColorBufferPrecision._64Bits`; both land on
`R16G16B16A16_SFloat`, **8 bytes per pixel**. There is no 4-byte HDR option for a passthrough app.

If someone enables HDR and forgets `preserveFramebufferAlpha`, URP silently picks the alpha-less
format, `isAlphaOutputEnabled` goes false, and **passthrough dies for the whole app**. That is a
one-checkbox, no-error-message failure, and it belongs in the settings gate (§13).

### 3.2 What it costs (INFERRED arithmetic from VERIFIED figures)

Per-eye resolution 2064 × 2208 = 4,557,312 px; both eyes 9,114,624 px (INFERRED, contract's Quest 3
figures; settle with `eyeTextureDesc`).

| Buffer | B/px | Both eyes | Note |
|---|---|---|---|
| Today: direct to swapchain, 4× MSAA memoryless | 4 | 36.5 MB stored once | MSAA samples never leave GMEM |
| HDR intermediate `R16G16B16A16_SFloat`, resolved | 8 | **72.9 MB stored, 72.9 MB loaded** | then a further 36.5 MB to the swapchain |

Meta's own pipeline example (`po-advanced-gpu-pipelines`, quoted in `quest-rendering-techniques`
§14) measures **StoreColor 1.525 ms, LoadColor 0.71 ms** in a 10.62 ms frame, at 8-bit. Scaling by
bytes moved (these are bandwidth-bound):

```
extra store+load = (1.525 + 0.71) ms × (8 B / 4 B) = 4.47 ms
```

**4.47 ms of an 11.0 ms budget, for the buffer alone, before a single tone-mapping instruction.**
INFERRED — the scaling is linear-in-bytes and Meta's example is at an unstated resolution. The
experiment that settles it: build two APKs differing only in `m_SupportsHDR`, read
`adb logcat -d | grep VrApi` for `App=<ms>` on the same fixed view.

Second cost, not in that number: **tile shrinkage**. Meta states XR2 Gen 2 gives 96×176 px tiles at
4× MSAA with 32-bit colour + D24S8 — 32 B per pixel of tile memory. At 64-bit colour that becomes
48 B/px, so the tile must shrink by a third and the binning pass runs over ~1.5× as many tiles,
re-processing geometry each time (INFERRED; `ovrgpuprofiler -t` reports bin counts).

### 3.3 What is lost without it

Only one thing, and it is repairable in the shader: **the sum of two overlapping emitters is
clipped at 1.0 before the response curve can compress it.** `tonemap(a) + tonemap(b)` is not
`tonemap(a + b)`.

Where that would bite, and why it does not:

| Overlap | Why it is not a problem |
|---|---|
| Two different stars in the same pixel | At m ≤ 6.5 there are 8,920 stars over 4π sr; the chance two land in one 10-px sprite is negligible, and where it happens the pixel *should* be white. |
| Core + halo + spikes of the **same** star | Made sub-unity by construction: core ≤ 1, halo gain 0.101 linear, spikes 0.047 (§14). Sum ≤ 1.19 — the very centre of Sirius clips, which is correct. |
| The Milky Way band | Built as **one mesh whose fragment integrates the density in float** — the sum happens inside the shader, so the curve applies to the sum. This is the architectural rule, not a coincidence. |
| Dust / nebula volumes | Same: a raymarch integrates in float and emits one already-compressed value. `MayEnclose` arbitration (CONTRACT §4.3 rule 4) guarantees at most one draws. |
| A lit planet plus the sky behind it | Different queues, opaque first; no additive overlap at all. |

**Rule to put in the contract:** *an additive pass may emit at most 1.0 of display white from a
single source, and a layer that would exceed it accumulates in its own fragment shader instead of
in the framebuffer.*

---

## 4. (b) Tone mapping

### 4.1 The three candidates, costed

| Curve | ALU/px | `f(0)` | Where | Verdict |
|---|---|---|---|---|
| **Extended Reinhard** `c(1+c/W²)/(1+c)` | ~4 (on luminance: 4 + 3 mul) | **0 exactly**, `f'(0) = 1`, `f(W) = 1` | in-shader, per source | **Chosen** |
| URP **Neutral** (`Color.hlsl:525-560`) | ~14/channel (two rational polys + `whiteScale`) | **0 exactly** — `NeutralCurve(0) = d·e/(d·f) − e/f = 0` (VERIFIED algebra) | post only, or 42 ALU/px in-shader | Acceptable fallback, 3.5× the cost, no better on this content |
| **ACES** (`ACES.hlsl`) | 100+ (two 3×3 matrices, a `log10`, a branchy B-spline, the ODT, another matrix) | **Not verified, and suspect**: `ACES.hlsl:494` branches on `logx <= log10(minPoint.x)` with `minPoint = (0.18·2⁻¹⁵, 1e-4)` and the ODT is normalised against `CINEMA_BLACK = 48/2400` (VERIFIED `:812-813`) | post only | **Never.** `log10(0)` on the fp16 mobile path is a NaN hazard, and a non-zero black floor at alpha 0 hazes the room (§2.2) |

ACES is also the wrong *look* here on grounds that have nothing to do with cost. Its RRT+ODT is
graded for a 100-nit reference display in a dim cinema surround. The Quest 3 is an LCD worn over a
room lit at 100–300 lux with passthrough compositing half the frame. ACES's shadow toe would crush
the faint half of the star field — the exact content this app exists to show — to nothing, and its
highlight desaturation would be doing a job the response curve in §5 already does better because it
does it in magnitudes.

### 4.2 The chosen curve, written out

```hlsl
// Highlight shoulder for BROAD sources: surfaces, volumes, the CMB, atmospheres.
// Extended Reinhard. W is the "white point": the scene value that maps to display 1.0.
//   f(0) = 0 exactly        (required: §2.2, or the room hazes)
//   f'(0) = 1               (dark detail is untouched — a night side stays a night side)
//   f(W) = W(1+1/W)/(1+W) = 1 exactly
// Applied to LUMINANCE, not per channel, so a bright blue star desaturates toward white the way
// an overexposed emitter does, instead of turning cyan.
half3 CS_Shoulder(half3 c)
{
    half W = (half)_CS_Image.z;                   // default 4.0
    half L = max(dot(c, half3(0.2126h, 0.7152h, 0.0722h)), 1e-5h);
    half Lt = L * (1.0h + L / (W * W)) / (1.0h + L);
    return c * (Lt / L);
}
```

Cost: 1 dot, 2 mad, 1 rcp, 4 mul ≈ **8 ALU**. At the worst case — one enclosing volume at full
coverage, 9.1 Mpx both eyes — that is 7.3e7 ops, **≈ 0.03 ms** (INFERRED; the volume's own 2.5 ms
budget dwarfs it).

`W = 4.0`: a source four times over the exposure white point maps exactly to display 1.0, and
anything below `W/4 = 1.0` is within 6 % of the identity. Justification: the widest *surface
brightness* ratio that must coexist in one frame after exposure is the sub-solar point of the focus
body against its own limb darkening and its atmosphere, which is under 4:1 (§6.4). Range 2–8; retune
against a headset capture of a full-phase Earth.

### 4.3 Which shaders get it

| Shader | Shoulder? | Why |
|---|---|---|
| `Body` | **yes** | the only broad lit surface; the terminator is where a curve shows |
| `Volume` | **yes** | dust/nebula integrate to arbitrary values |
| `Sky` (CMB) | **yes** | full-screen fill |
| `CosmoAtmosphere` | **yes** | limb can integrate far past 1 |
| `Ring` | **yes** | broad, premultiplied |
| `Point` | **no** — the γ response in §5 *is* its curve, and it must keep rising past 1 so the excess can become area |
| `Ribbon`, `Text`, `Panel`, `Unlit`, `CosmoRim` | **no** — authored display-referred colours, already ≤ 1 by construction |

### 4.4 How tone mapping interacts with passthrough (the part that is not obvious)

Tone mapping runs on the *app layer*, before the compositor blends the room in. It therefore cannot
crush the room by itself. Two second-order effects can, and both are real:

1. **A post pass writes every pixel, including the ones nothing drew.** `UberPost.shader:344-348`
   (VERIFIED):

   ```hlsl
   #if _ENABLE_ALPHA_OUTPUT
       return half4(color, saturate(inputColor.a));
   #else
       return half4(color, 1);
   #endif
   ```

   Without `_ENABLE_ALPHA_OUTPUT` — which requires an alpha-carrying intermediate, which requires
   §3.1's 64-bit format — **every pixel of the frame is submitted at alpha 1 and the passthrough
   room disappears behind an opaque black rectangle.** This is a single-keyword, silent,
   everything-is-broken failure, and it is the reason the post stack is not merely expensive here
   but hostile.

2. **A curve with a non-zero black point adds a flat haze.** With `f(0) = ε` and alpha 0, §2.2 gives
   `displayed = ε + room`. A uniform ε = 1/255 over the whole field of view is a visible grey veil
   over the user's living room. The ACES path is the candidate that cannot be shown to have ε = 0.

Both are avoided by the same decision: no full-screen pass at all, and every in-shader operator
proven to return exactly 0 for 0.

---

## 5. (c) Bloom

### 5.1 URP's bloom, priced (VERIFIED structure, INFERRED ms)

`Bloom.cs:220,226` (VERIFIED): `downscale = Half`, `maxIterations = 6` (clamped 2–8).
`BloomPostProcessPass.cs:187-190` (VERIFIED):

```csharp
int maxSize   = Mathf.Max(bloomResolution.x, bloomResolution.y);
int iterations = Mathf.FloorToInt(Mathf.Log(maxSize, 2f) - 1);
int mipCount   = Mathf.Clamp(iterations, 1, bloom.maxIterations.value);
```

At half of 2064×2208 → 1032×1104, `iterations = floor(log2 1104) − 1 = 9`, clamped to **6**.
The chain is 1 prefilter + 5 downsample (each a 9-tap `FragBlurH` then `FragBlurV`, VERIFIED 9
`SAMPLE_TEXTURE2D_X` in `FragBlurH`) + 5 upsample = **11 render passes**, each on an XR texture
array, before `UberPost` composites.

```
mip0 both eyes              = 1032 × 1104 × 2 = 2.279 Mpx
pyramid sum (1 + ¼ + …)     = 2.279 × 1.333   = 3.04 Mpx per sweep
down (prefilter + H + V)    ≈ 2 sweeps        = 6.1 Mpx × 9 taps = 55 M fetches
up                          ≈ 1 sweep         = 3.0 Mpx × 2 taps =  6 M fetches
pyramid memory (RGBA16F)    = 3.04 Mpx × 8 B  = 24.3 MB
```

11 render-target switches on a tiled GPU is 11 bin/store/load cycles. INFERRED **1.5–3.0 ms**,
*on top of* the 4.47 ms HDR intermediate it cannot run without. Total post stack **6.5–8.0 ms of
11.0**. It also requires reversing three contract decisions at once (`m_SupportsHDR 0`,
`postProcessData = null`, `rendererFeatures` empty — CONTRACT §8.1), each of which is there because
Worldfall proved the alternative on a headset.

Rejected.

### 5.2 The cheaper alternative is not a cheaper blur — it is geometry

The project already measured the governing fact. `StarLook.cs` (VERIFIED, verbatim):

> "a star's rendered peak turns out to be governed by its *width* far more than by the brightness it
> is given … the median 2-pixel star peaked at 11/255 and the single 5-pixel one at 72, with the
> correlation between width and peak at 0.80"

and

> "Size is the only channel that reliably survives to the screen here; brightness rides along with
> it."

That is the whole argument for geometric bloom, written by this codebase from its own measurements.
The owner's Milky Way Atlas reached the same conclusion independently: it puts the dynamic range
into `gl_PointSize` and caps alpha at 0.8 (VERIFIED, `nebula-gallery/index.html:294-305`).

`Point.shader` **already has the geometry**: `v.data.z` selects core / halo / spike-x / spike-y,
`_Shape = (2.8, 7, 0.35)` gives the extents and `_ShapeGain = (0.35, 0.25)` the gains. Nothing new
is drawn. The only change is *which stars get the extra quads* and *what drives the gain*.

Three quads more than the core, on the bright end only:

```
core    1.0 × 1.0 of the base width
halo    2.8 × 2.8
spike-x 7.0 × 0.35      spike-y 0.35 × 7.0
```

### 5.3 Where the threshold goes — measured, not chosen

Counted on triton from `SA/sky/stars.csstars` (119,613 records, VERIFIED run):

| m ≤ | stars | % of the m ≤ 6.5 sky |
|---|---|---|
| −1.00 | 1 | 0.011 |
| 0.00 | 4 | 0.045 |
| 1.50 | 24 | 0.269 |
| 2.00 | 50 | 0.561 |
| 2.50 | 94 | 1.054 |
| 3.00 | 179 | 2.007 |
| 3.50 | 291 | 3.262 |
| 4.00 | 523 | 5.863 |
| 4.25 | **679** | **7.612** |
| 5.00 | 1,637 | 18.35 |
| 6.50 | 8,920 | 100 |

Brightest record m = −1.44 (Sirius). With the §6 response at `m0 = 1.5` and γ = 0.28,
`b = 2^(−0.372056·(m − m0))`:

| m | b | what happens |
|---|---|---|
| −1.44 | 2.145 | core clips over a disc of radius 0.525 quad; full halo, full spikes |
| 0.00 | 1.473 | full halo, full spikes |
| 1.50 | **1.000** | halo at full gain; `saturate(3b−2)` puts spikes at full |
| 2.59 | 0.667 | spikes begin |
| 4.05 | **0.500** | **halo begins** |
| 6.50 | 0.275 | core only, peak sRGB code 142 |

So the **existing** `saturate(2·b − 1)` expression, with `b` redefined, ramps the halo on across the
brightest **6.3 %** (b 0.5→1.0, m 4.05→1.5) and reaches full strength on the brightest **0.27 %**.
A second, tighter gate `saturate(3·b − 2)` puts diffraction spikes on the brightest **1.2 %** only
(m ≤ 2.59, 110 stars) — spikes are a *camera* artefact and an eye does not have them, so they are
authored decoration and deserve the tighter gate and a line in the provenance panel.

**Mesh rule for the `stars` lane:** emit the halo and both spike quads only for `m ≤ 4.25`
(679 stars, measured). Vertex count for the naked-eye sky:

```
679 × 4 quads × 4 verts  =  10,864
8,241 × 1 quad × 4 verts =  32,964
                            -------
                             43,828 vertices
```

against the `stars` layer's 480,000-vertex row (CONTRACT §7.3). The current builder emits "1–5
quads" for up to 9,000 stars (`render-xr-audit`, VERIFIED) — up to 180,000 — so the measured cut is
a **saving of up to 136,000 vertices**, not a cost.

### 5.4 The fragment cost of geometric bloom

At `ppr = 1140` px/rad and `_SizeDmm.y = 9.0` dmm, a bright core is `9.0e-3 × 1140 = 10.26 px`
(inside `_PxBounds = (3, 64)`).

| Quad | px | px² | ×679 |
|---|---|---|---|
| core | 10.26 | 105.3 | 71,499 |
| halo (×2.8) | 28.73 | 825.4 | 560,447 |
| spike ×2 (7.0 × 0.35) | 71.8 × 3.59 | 515.6 | 350,092 |
| | | **1,446** | **982,038 px²** |

Faint stars: 8,241 × (4.33 px)² ≈ 154,500 px².
Total ≈ 1.137 M px² per eye against an eye of 4.557 Mpx = **0.249 eye-screens** if the entire
celestial sphere were in view at once. The frustum covers about 23 % of 4π, so in practice
**≈ 0.06 eye-screens**, against the budget's 0.5 for point sprites. Worst case still passes.

GPU ms, anchored on the only device-measured number this project has — Fractarium's **0.338 ms per
step-coverage** on Quest 3 (a step-coverage = one raymarch step, including a 3D texture fetch and
~20 ALU, over the area of both eyes). A star fragment is an `exp2`, three multiplies and a blend,
call it a third of a march step:

```
0.249 coverage × (0.338 / 3) ms = 0.028 ms   worst case
0.06  coverage × (0.338 / 3) ms = 0.007 ms   typical
```

**≈ 0.02 ms.** INFERRED — the "third of a march step" factor is a guess. Experiment: build the
Fractarium-style `cost_probe.sh` for point sprites — draw N identical full-coverage quads with the
star fragment and read `App=<ms>` off `VrApi` for N = 1, 2, 4, 8.

### 5.5 Keeping the bright end from becoming a white square

The fragment profile is `a = exp2(−4·u·u)`, so the pixel where `b·a = 1` sits at
`u = sqrt(log2 b)/2`. The saturated white disc therefore grows on its own as a star gets brighter —
that is the bloom core, and it is free. But past `log2 b = 1.44` (`b > 2.71`) it reaches `u = 0.6`
and keeps going, and at `b = 16` the whole quad is white: **the square-star bug again, at the other
end of the scale.** `_PxBounds.x = 3` guards the faint end; nothing guards this one.

```hlsl
// Keep the saturated core inside the quad. Same failure as the 2 px star, opposite cause.
float grow = sqrt(max(1.0, log2(max(b, 1.0)) / 1.44));
```

At the shipped sky (`b ≤ 2.15`) this is exactly 1 and costs nothing. It only fires when the user
pushes `stars.exposure` to 3 or a far rung applies an assist — which is precisely when nobody is
watching for it.

---

## 6. (d) Exposure

### 6.1 The span that matters is 8 orders of magnitude, not 20

The brief says twenty orders of magnitude of luminance, and that is true of *emitted power*. It is
not true of what a pixel displays. A pixel displays **surface brightness**, and surface brightness
is distance-invariant: as an object recedes, its flux falls as 1/d² and its solid angle falls as
1/d², and the ratio is constant. That is why a photograph of the Andromeda galaxy has the same
exposure whether it is taken from Earth or from 10× further away.

| Source | Surface brightness | mag/arcsec² |
|---|---|---|
| Sunlit Earth at 1 AU, albedo 0.3 | `ρE/π = 0.3 × 1.28e5 / π` ≈ 1.2e4 cd/m² | ≈ 3.5 |
| Full Moon | ≈ 2.5e3 cd/m² | ≈ 5.2 |
| Milky Way disc, naked eye | ≈ 2.7e-4 cd/m² | ≈ 22 |
| The darkest sky background | ≈ 1.7e-4 cd/m² | ≈ 22.5 |
| The CMB | **zero visible light** | undefined |

(INFERRED arithmetic from standard photometry; the magnitudes are the conventional values.)

That is 1.2e4 / 2.7e-4 ≈ **4.6e7, about 7.6 orders, 19 magnitudes** — for *extended* sources, across
the whole product, and **it barely changes with the rung.** The genuine per-rung span is in **point
sources**, whose flux falls as 1/d² with no compensating solid angle once they are smaller than a
pixel: about 30 magnitudes from Sirius at rung 0 to a galaxy at rung 18.

Three consequences that shape the design:

1. **Two exposure anchors, not one.** `m0` for point sources (magnitudes) and `μ0` for extended
   ones (mag/arcsec²). They are different quantities and a single knob cannot serve both.
2. **`μ0` is nearly constant across all nineteen rungs.** One number, 20.0 mag/arcsec², works from
   the tabletop to the cosmic web. This is why the owner's atlas needs no per-scale exposure for its
   dust cube and its galaxy discs.
3. **The CMB's brightness is authored, not measured.** It emits nothing visible. Its display value
   is a palette choice and must carry the `*` provenance glyph and a legend saying so. The contract
   already requires the legend (`cmb.mode`, `cmb.contrast`); this is the reason.

### 6.2 The point-source response

```
ℓ(m)  =  10^(−0.4 (m − m0))        normalised flux; ℓ = 1 at the exposure white point
b(m)  =  ℓ^γ  =  2^(−1.3287712 · γ · (m − m0))
```

with **γ = 0.28**. Justification, in order of weight:

- The owner's Milky Way Atlas stores `L^0.28` per star and calls it "eye-like compression"
  (VERIFIED, `nebula-gallery/index.html:345` and README). This is the visual bar, and it was worn.
- Its OBSERVED mode uses a companion exponent 0.23 on the apparent-flux path (`:298`). 0.28 and 0.23
  bracket the same choice.
- It brackets the real Stevens exponent for brightness of a point in the dark (≈ 0.33) and the
  ≈ 0.4 of the classic Weber–Fechner fit; a lower exponent is the conventional astrophotographic
  "stretch".
- Range to retune in: **0.22–0.40**. Lower is flatter and shows more faint stars; higher gives more
  bright-end separation and empties the sky.

Note `−1.3287712 · γ = −0.372056` for γ = 0.28: one `exp2` of a bounded argument, no `pow`, no
overflow. The existing code already uses the constant `1.3287712` for `10^(−0.4x)`.

**This replaces a defect, not a working design.** `Point.shader` today computes
`b = saturate((_MagWindow.x − m) / (_MagWindow.x − _MagWindow.y))` — linear in magnitude, and
`saturate`d. Sirius (m = −1.44), Vega (0.03) and Deneb (1.25) all clamp to `b = 1` and render
**identically**: the brightest 24 stars in the sky have no hierarchy at all. That is one visible
cause of "empty dark space": the sky has no bright end.

### 6.3 `m0` per rung — a generated table, because the physics is not one slope

Both halves of the brief's question are the same mechanism. Compute, offline from the catalogues,
the apparent magnitude that the active layers actually present from each rung's vantage; that is a
*measured log-percentile* of the scene, and putting it in a table makes it reproducible. There is no
framebuffer readback anywhere.

```
m0(rung) = P99.5{ apparent magnitude of every row the rung's active layers will draw,
                  evaluated from the rung's vantage } + 3.0
```

The `+3.0` reproduces the measured anchor: at rung 0 the brightest star is Sirius at m = −1.46, and
the shipped, hand-tuned white point is `_MagWindow.y = 1.5` — a difference of 2.96. **The rule
regenerates the one constant that was tuned by eye on a device.** That is the calibration check.

Hand-computed expected output (INFERRED arithmetic; the generator is the authority):

| Rung | id | log10 U | vantage | m0 | note |
|---|---|---|---|---|---|
| 0–8 | `sky` … `oort` | +3.00 … −13.73 | within ~300 AU of the Sun | **1.5** | the real sky is the real sky; gain 1; **no `BRIGHTNESS` chip** |
| 9 | `nearest` | −14.51 | ~6 ly out | **1.5** | Sirius at 1.84 pc reads m = −2.26 |
| 10 | `neighbourhood` | −15.55 | ~40 pc out | **5.4** | brightest inside 40 pc ≈ M_V −0.6 → m 2.4 |
| 11 | `clouds` | −16.59 | ~1 kpc out | **7.0** | O/B associations, M_V ≈ −6 at 1 kpc → m 4 |
| 12 | `milkyway` | −18.25 | ~40 kpc from the GC | **13.0** | brightest supergiant M_V −8 → m 10.0 |
| 13 | `halo` | −19.42 | ~600 kpc | **13.3** | |
| 14 | `localgroup` | −20.01 | ~3 Mpc | **13.4** | M31 as a point, M_V −21.5 → m 6.9, but it is extended (§6.4) |
| 15 | `localvolume` | −20.84 | ~20 Mpc | **13.5** | L* galaxy M_V −21 → m 10.5 |
| 16 | `laniakea` | −21.89 | ~250 Mpc | **15.0** | |
| 17 | `cosmicweb` | −22.71 | ~2 Gpc | **15.8** | |
| 18 | `cmb` | −23.87 | observer-centred | **16.0** | point content is clusters; the CMB itself is §6.1 note 3 |

The slope is **3.1 mag per decade between rungs 9 and 12** and **0.54 between 12 and 18** — because
between 9 and 12 you leave the stars behind and they all vanish at once, while beyond 12 the content
is galaxies whose visible population is Malmquist-selected and tracks distance. **That is why it is
a table and not a formula**, and why a slope fitted from the two endpoints would be wrong at every
point in between.

Interpolate **linearly in `log10 U`** between adjacent entries, so continuous zoom gives continuous
exposure and never snaps — matching CONTRACT §5.4's "Continuous zoom updates the highlight; it never
snaps."

### 6.4 The extended-source anchor and the scene-unit mapping

`μ0 = 20.0 mag/arcsec²` maps to display 1.0, constant across rungs (§6.1). For surfaces, express it
as a linear gain on **normalised irradiance** — `E_norm = 1` is the solar constant at 1 AU:

```
displayed = albedo · cos θ · E_norm · G,        G = _CS_Image.y
G is set so the focus body's sub-solar point reads 0.75 at a reference albedo of 0.3:
    G = 0.75 / (0.30 · E_norm(focus))
```

Worked: Earth at 1 AU, `E_norm = 1` → `G = 2.5`, sub-solar display `0.3 × 2.5 = 0.75`. Pluto at
39.5 AU, `E_norm = 1/1560` → `G = 3900`, and Pluto's sub-solar point reads 0.75 again while Earth,
if both are in frame, reads 1950 and clips to white. That is a camera's behaviour and it is honest,
but it must be *stated*: the gain follows the focus, so anything nearer the star than the focus is
blown out on purpose.

Contract hook: `G ≠ G(rung-0 focus)` is a presentation gain and raises `BRIGHTNESS x<N>` under
CONTRACT §5.5.2, alongside `stars.exposure`.

### 6.5 Adaptation — bounded, model-based, and off by default

The table already covers the reproducible case. Adaptation exists for the residual: turning toward
versus away from the galactic centre, flying into a nebula, a planet filling the view.

**Do not read the framebuffer.** A log-average luminance meter needs a resolve plus an async readback
— which defeats memoryless MSAA, adds 1–2 frames of latency, and cannot be tested on this box.
Compute the meter on the CPU from the layers' own data instead: each active layer already knows its
catalogue and the view, so it can report the flux it is about to put on screen. That is engine-free,
deterministic, unit-testable in `Core/` with no editor, and has zero GPU cost.

```
Core/Image/Exposure.cs                      (Core: no UnityEngine reference)
  double Meter(IReadOnlyList<LayerFlux> active)   // Σ predicted screen-integrated flux
  double Trim(double meter, double target)        // in magnitudes, clamped to ±1.5
  double Step(double current, double wanted, double dt)
      τ = 0.30 s when the scene brightens (pull down fast: no white flash)
      τ = 1.50 s when it darkens         (raise slowly: human dark adaptation)
```

Clamped to **±1.5 magnitudes** so the seven fixed capture views stay reproducible and the G1c
brightness assertions stay meaningful. **Ships with `Trim` disabled** (`_CS_Image` gets the table
value alone); turn it on in Phase 3 when there is a headset to judge it on. Skipped: image-based
metering — add it only if a Phase-3 headset session shows the model meter mispredicting by more than
half a magnitude on a real view.

---

## 7. (e) Colour grading LUT — no

Costed for completeness. URP's LUT (`ColorGradingLutPass.cs:41-55, 90-93`, VERIFIED):

| | Size | Format | Bytes |
|---|---|---|---|
| LDR | `lutSize² × lutSize` = 1024 × 32 | `R8G8B8A8_UNorm` | **131,072** |
| HDR | 1024 × 32 | `R16G16B16A16_SFloat` | **262,144** |

Trivial memory. The cost is elsewhere:

- **It cannot be applied per source.** Grading is non-linear, so `grade(a) + grade(b) ≠ grade(a+b)`.
  Applying it inside each additive shader grades each contribution separately and the result is not
  the graded image of anything.
- **The pass that could apply it correctly is rejected in §3–§4.**
- **In-shader it is a texture fetch per fragment**, in passes whose whole design goal is to be ALU
  only. A full-coverage volume would pay 9.1 M extra bilinear fetches for a colour tweak.

**Do it on the CPU, on the constants.** The theme is a fixed set of authored colours
(`CosmoTheme.cs:34-75`) and per-layer tints. A grade applied once at load, to ~50 `Color` values,
costs nothing at runtime and is exactly as expressive for content that has no photographic
provenance to preserve. Escape hatch if a future look genuinely needs a curve: bake it into
`CS_Respond`'s γ and the shoulder's W, which are already per-frame globals.

---

## 8. (f) Banding and dithering

### 8.1 How much banding there actually is

The eye buffer is `R8G8B8A8_SRGB` (§2.1) and Unity writes sRGB-encoded (`desc.sRGB = (activeColorSpace
== Linear)`, VERIFIED `UniversalRenderPipelineCore.cs:1581`). Quantisation is therefore **1/255 of
sRGB code**, which near black is a linear step of 0.000303 and near white 0.0057.

A smooth gradient across a 400-px-wide feature that spans, say, 20 code values puts a visible Mach
band every 20 px. The features that do this in this app: the atmosphere limb, the dust volume, the
CMB fill, the terminator, and panel backings. On a Quest 3 LCD in a lit room, with the compositor's
lens resampling smearing each band's edge, this reads as concentric rings — the classic "cheap space
render" tell.

### 8.2 URP's dither would haze the room

`Common.hlsl:188-205` (VERIFIED):

```hlsl
float noise = SAMPLE_TEXTURE2D(BlueNoiseTexture, ..., uv * scale + offset).a * 2.0 - 1.0;
noise = FastSign(noise) * (1.0 - sqrt(1.0 - abs(noise)));
...
input = GetSRGBToLinear(GetLinearToSRGB(input) + noise / 255.0);
```

At `input = 0` this is `GetSRGBToLinear(noise/255)`. For the half of pixels where the noise is
positive it returns a small positive linear value that encodes back to **code 1**. With alpha 0 and
premultiplied-over compositing (§2.2) that is `+1/255` of light added to the room on half of every
pixel where nothing was drawn — a faint but genuinely visible grain over the user's living room.
URP's form is *additive in sRGB space* and is correct for an opaque frame. It is wrong here.

### 8.3 The dither this project needs

Multiplicative in the local slope of the sRGB curve, so it is **exactly zero at zero**:

```hlsl
// Triangular-PDF dither at the 8-bit sRGB quantisation, applied in LINEAR.
//
// The amplitude is the local slope d(linear)/d(sRGB code), so one LSB of noise in linear equals
// one LSB after encoding. Two properties earn their instructions:
//   * slope(0) = 0, so a pixel with nothing drawn stays EXACTLY zero and cannot haze passthrough.
//     URP's own ApplyDithering does not have this property (Common.hlsl:188).
//   * the clamp at 0.0774 = 1/12.92 is the slope of the sRGB curve's LINEAR toe; without it the
//     sqrt over-dithers the darkest pixels by 58% and puts grain on near-black over the room.
// Luminance only: chroma noise is not needed for banding and costs 3x.
half3 CS_Dither(half3 c, float2 positionSS)
{
    half n = (half)(2.0 * CS_IGN(positionSS) - 1.0);                  // [-1, 1] uniform
    n = (n < 0.0h ? -1.0h : 1.0h) * (1.0h - sqrt(1.0h - abs(n)));     // [-1, 1] triangular
    half L = max(dot(c, half3(0.2126h, 0.7152h, 0.0722h)), 0.0h);
    half slope = min(2.2h * sqrt(L), 0.0774h);                        // d(linear)/d(sRGB)
    return max(c + n * slope * (half)_CS_Image.w * (1.0h / 255.0h), 0.0h);
}
```

`CS_IGN` already exists in `CosmoCore.hlsl` (VERIFIED) and is used for the volume march jitter, so
this needs **no texture** — unlike URP, which samples a blue-noise atlas.

`_CS_Image.w` is the amplitude in LSB; **default 1.0**, range 0–2, 0 disables.

Cost: IGN 5 + sign/sqrt 4 + dot 1 + sqrt 1 + min 1 + mad 3 ≈ **~15 fp16 ALU per fragment**. At full
eye coverage both eyes, 9.1 Mpx × 15 = 1.4e8 ops ≈ **0.10 ms** worst case (INFERRED, using ~1.4
Tops/s fp16 for Adreno 740). Real cost is lower because it goes in only 5 shaders.

### 8.4 Where it belongs, and where it must not go

| Shader | Dither? | |
|---|---|---|
| `Volume`, `Sky`, `CosmoAtmosphere`, `Body`, `Panel` | **yes** | broad smooth gradients — the whole reason banding is visible |
| `Point` | **no** | a 10-px sprite cannot band, and per-sprite noise on 8,920 stars reads as twinkle |
| `Ribbon`, `Text`, `Ring`, `Unlit`, `CosmoRim` | **no** | hard-edged or ≤ 1 code of gradient |

**Position in the chain: last, after the response curve and the shoulder, before the return.**
Dithering before the curve would be re-quantised by the curve and do nothing.

### 8.5 The foveation interaction nobody will look for

With SRP foveation on, peripheral pixels are rendered at ¼ or 1/16 rate and upscaled by the
compositor. A per-pixel IGN dither computed in the *rendered* grid becomes 2×2 or 4×4 **blocks**
after upscale — blocky noise in the periphery, which is worse than the banding it replaced.

Mitigations, in order of laziness:
1. Amplitude 1.0 LSB is already at the edge of visibility; blocking it by 4× makes it a 4 px blob of
   ±1 code. **Probably invisible. Test before spending anything.**
2. If it is visible: compute the noise in linear (non-foveated) screen space with
   `RemapFoveatedRenderingNonUniformToLinear(uv)` from
   `com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl:69` (VERIFIED present),
   behind `#include_with_pragmas "FoveatedRenderingKeywords.hlsl"`. Costs one remap and adds a
   keyword — which every CosmoSynth shader currently avoids (single-variant, CONTRACT §7).
3. If that is unacceptable: taper `_CS_Image.w` to 0 outside the foveal region using the radius the
   vertex shader already has.

**Only a headset can decide this.** Put it in the first headset session's checklist.

---

## 9. (g) What each decision does to the MR case

| Decision | Effect where the room is behind the image |
|---|---|
| No HDR intermediate | The alpha channel never passes through a format that lacks one. The `B10G11R11` trap (§3.1) cannot fire. |
| No post pass | `UberPost`'s `return half4(color, 1)` (§4.4) cannot fire. Nothing writes alpha except the shader that meant to. |
| `f(0) = 0` on every operator | Pixels with nothing drawn stay `(0,0,0,0)`; the room composites through unchanged. |
| Shoulder on luminance, not per channel | A bright limb desaturates toward white instead of shifting hue against a room whose colour is uncontrolled. |
| Geometric bloom | Bloom lives in geometry the app already draws, so it is depth-sorted and passthrough-safe. A screen-space bloom would smear a star's light **over the room**, which is a coverage claim the app never made. |
| Dither with a zero-slope at zero | No grain on the room (§8.2). |
| Exposure follows the focus, and says so | `BRIGHTNESS xN` fires. The room is the only thing in frame whose brightness the app does *not* control, so an unlabelled gain change reads as the room dimming. |

One thing the app cannot control and must design around: **passthrough is never black.** Meta's own
passthrough clear rule requires `SolidColor` with alpha 0 (`ARCameraFeature.cs:194`, VERIFIED via
`render-xr-audit`). The "empty dark space" the owner saw is a flat mono capture artefact; in the
headset the same frame shows the room. The Space mode (`_Immersion = 1`, CONTRACT §2.2 queue 1000)
is where the void is real, and that is the mode this exposure model must be judged in.

---

## 10. The code

### 10.1 `CosmoCore.hlsl` addendum (Appendix A.2)

Append to the existing 57-line file rather than adding a twelfth include: eleven shaders already
include it, and a new file is one more thing to forget.

```hlsl
// ---------------------------------------------------------------------------------------------
// Image pipeline. One exposure, one response curve, one shoulder, one dither.
//
// There is no HDR buffer and no post-processing pass in this application. The Quest eye swapchain
// is 8-bit sRGB and the compositor blends premultiplied-over onto the room, so a full-screen pass
// would (a) cost 4.5 ms for the intermediate it needs, and (b) submit alpha 1 everywhere and hide
// the room. The response curve therefore lives at the point of emission, and every function here
// returns EXACTLY 0 for 0 so that a pixel with nothing drawn cannot tint the user's room.
// ---------------------------------------------------------------------------------------------

// Published by Placement.PublishGlobals, LateUpdate order 50. No material owns these.
float4 _CS_Image;   // x: m0   apparent magnitude that maps to display 1.0 (point sources)
                    // y: G    linear gain for irradiance-referred sources (surfaces)
                    // z: W    shoulder white point, default 4.0
                    // w: D    dither amplitude in LSB, default 1.0, 0 disables

#define CS_RESPONSE_GAMMA 0.28    // eye-like compression; refs/webxr/nebula-gallery stores L^0.28

// Apparent magnitude -> display-referred brightness. One exp2 of a bounded argument: no pow, and
// no overflow even at m - m0 = 40. Keeps rising past 1 on purpose, so the excess above display
// white can be spent as AREA (the halo and spike quads) instead of being thrown away.
//   -1.3287712 = -0.4 * log2(10)   (the constant already used for 10^(-0.4 x))
float CS_RespondMagnitude(float apparentMagnitude)
{
    return exp2(-1.3287712 * CS_RESPONSE_GAMMA * (apparentMagnitude - _CS_Image.x));
}

// Extended Reinhard shoulder for BROAD sources. f(0)=0, f'(0)=1, f(W)=1. On luminance, so an
// overexposed emitter desaturates toward white instead of shifting hue.
half3 CS_Shoulder(half3 c)
{
    half W = (half)_CS_Image.z;
    half L = max(dot(c, half3(0.2126h, 0.7152h, 0.0722h)), 1e-5h);
    half T = L * (1.0h + L / (W * W)) / (1.0h + L);
    return c * (T / L);
}

// Triangular-PDF dither at the 8-bit sRGB quantisation, applied in linear. See design doc 8.3:
// the slope form is what keeps black exactly black, which URP's ApplyDithering does not.
half3 CS_Dither(half3 c, float2 positionSS)
{
    half n = (half)(2.0 * CS_IGN(positionSS) - 1.0);
    n = (n < 0.0h ? -1.0h : 1.0h) * (1.0h - sqrt(1.0h - abs(n)));
    half L = max(dot(c, half3(0.2126h, 0.7152h, 0.0722h)), 0.0h);
    half slope = min(2.2h * sqrt(L), 0.0774h);          // 1/12.92 is the sRGB toe slope
    return max(c + n * slope * (half)_CS_Image.w * (1.0h / 255.0h), 0.0h);
}
```

### 10.2 `Placement.cs` — one more global

```csharp
/// <summary>Shader global: (m0, surface gain, shoulder white point, dither LSB).</summary>
public static readonly int ImageId = Shader.PropertyToID("_CS_Image");

// inside PublishGlobals(in FrameContext ctx):
Shader.SetGlobalVector(ImageId, new Vector4(
    (float)ctx.ExposureM0,          // Core/Image/ExposureTable, interpolated in log10 U
    (float)ctx.SurfaceGain,         // 0.75 / (0.30 * E_norm(focus))
    ImageLook.ShoulderWhite,        // 4.0
    ImageLook.DitherLsb));          // 1.0
```

`ImageLook` is the one place these three constants live, next to `StarLook`, for the same reason
`StarLook` exists.

### 10.3 The eleven shaders — one line each

| Shader | Change |
|---|---|
| `Point` | `b` from `CS_RespondMagnitude(magnitude)` instead of the `saturate` ramp; `level = min(b, 1)`; halo gain `saturate(2b−1)` (**unchanged expression**); spike gain `saturate(3b−2)`; add the `grow` guard (§5.5) |
| `Body` | `colour = CS_Dither(CS_Shoulder(colour), i.positionCS.xy);` before the return |
| `Volume` | shoulder + dither on the premultiplied colour **before** multiplying by alpha |
| `Sky` | shoulder + dither before `* _Immersion` |
| `CosmoAtmosphere` | shoulder + dither on `colour` before `max(colour, 0)` |
| `Ring` | shoulder before `* alpha` |
| `Panel` | dither only |
| `Ribbon`, `Text`, `Unlit`, `CosmoRim` | **no change** |

Every one of the eleven has exactly one `return half4(...)` (VERIFIED by reading all eleven on
`lane/foundation-shaders`), so this is an eleven-line diff with no restructuring.

### 10.4 The chain, in order

| # | Operation | Where | Space |
|---|---|---|---|
| 1 | physical quantity (m, μ, irradiance) | the layer's data | — |
| 2 | exposure: `− m0`, or `× G` | vertex or fragment | — |
| 3 | response: `CS_RespondMagnitude` (points) | vertex | display-referred |
| 4 | authored colour decode `CS_DisplayToLinear` | vertex/fragment | → linear |
| 5 | layer gain, `_Fade`, near-fade, shape gain | vertex/fragment | linear |
| 6 | shoulder `CS_Shoulder` (broad sources) | fragment | linear |
| 7 | dither `CS_Dither` (5 shaders) | fragment | linear |
| 8 | hardware sRGB encode + 8-bit quantise | ROP | → sRGB |
| 9 | hardware blend per the queue table | ROP | linear (sRGB format ⇒ linear blend) |
| 10 | compositor: premultiplied over the room | Meta compositor | — |

**No render features. The renderer feature list stays empty.** A renderer feature that requires an
intermediate colour texture forces one even under `intermediateTextureMode = Auto` — a full-screen
store plus load per frame (CONTRACT §8.1, quoting `render-xr-audit`), which is the same class of
surprise extra pass that produced the 1.7.0 grid.

---

## 11. Settings asset values

**Nothing in CONTRACT §8.1 changes.** That is the deliverable of this section: the image pipeline
described here is implementable without reversing a single settings decision. Restated so the
integrator can assert them:

| Setting | Value | Why this design needs it |
|---|---|---|
| `m_SupportsHDR` | **0** | §3 |
| `camera.allowHDR` | **false** | §3 |
| `renderPostProcessing` | **false** | §4.4 |
| `antialiasing` (camera) | **None** | MSAA comes from the asset |
| `m_MSAA` | **4** | the only AA in the frame; the star profile is a Gaussian and relies on it |
| `postProcessData` | **null** | §5.1 |
| `rendererFeatures` | **empty** | §10.4 |
| `intermediateTextureMode` | **Auto** | with the above, resolves to "no intermediate" |
| `colorSpace` | **Linear** | §14 |
| `m_StoreActionsOptimization` | **0** | leave URP's default; buffer discards are the OpenXR feature's job |
| `Graphics.preserveFramebufferAlpha` | **not set** (false) | only matters if HDR is ever enabled; §3.1 says why it must then be true |

Three **new** gate rows for `settings_gate.py` / G0c, each of which would have caught a real failure:

1. `m_SupportsHDR == 0` **or** `Graphics.preserveFramebufferAlpha == true` — the §3.1 trap.
2. `rendererFeatures.Count == 0` and `postProcessData == null` — the §10.4 extra pass.
3. Every `CosmoSynth/*` shader that samples `_CS_Image` also declares it — a missing global reads 0,
   which makes `m0 = 0` and the sky four magnitudes too dark with no error.

And one new G1c play-mode assertion, **`image`**: capture a region of the frame that no layer draws
and assert the pixel is **exactly** `(0, 0, 0, 0)` with the dither, the shoulder and the response all
live. Per the project's rule, land it with a deliberately broken run recorded: set
`ImageLook.DitherLsb` to URP's additive form and watch it fail.

Add to the `render:` diagnostic line: `img <m0>/<G>/<W>/<D> fmt <colourFormat>`.

---

## 12. Per-frame cost table

| Item | GPU ms/eye | Draws | Vertices | Texture bytes | Step-cov |
|---|---|---|---|---|---|
| Exposure + response, `Point` vertex stage | ~0 (2 ALU × 43,828 verts) | 0 | 0 | 0 | 0 |
| Geometric bloom fragments (679 stars, halo + 2 spikes) | **0.02** (worst 0.03) | 0 | +8,148 vs core-only | 0 | 0 |
| `CS_Shoulder` in 5 shaders | **0.03** | 0 | 0 | 0 | 0 |
| `CS_Dither` in 5 shaders | **0.10** (worst, full coverage) | 0 | 0 | 0 | 0 |
| `_CS_Image` publish | 0 (1 `SetGlobalVector`) | 0 | 0 | 0 | 0 |
| **Total added by this design** | **+0.15** | **0** | **+8,148** | **0** | **0** |

Against the rejected alternative:

| Rejected item | GPU ms/eye | Memory |
|---|---|---|
| `R16G16B16A16_SFloat` intermediate, store + load | **+4.47** | 72.9 MB |
| URP Bloom, 11 passes at Half downscale, 6 mips | **+1.5 to +3.0** | 24.3 MB |
| `UberPost` (LUT sample + dither + composite) | **+0.5** | 0.13 MB (LDR LUT) |
| **Total** | **+6.5 to +8.0 of 11.0** | 97 MB |

All ms figures are INFERRED. The store/load figure is scaled from Meta's measured 1.525/0.71 ms; the
bloom and shader figures are scaled from Fractarium's measured 0.338 ms per step-coverage. §16 lists
the three experiments that replace them with measurements.

---

## 13. Failure modes this project has already hit, and how this design avoids them

| Shipped failure | Recurrence risk here | Guard |
|---|---|---|
| **1.7.0's "100 plus Earths on a grid"** — an extra render pass loading a memoryless MSAA attachment | Every post-processing pass is an extra pass. | No post pass, `rendererFeatures` empty, and a gate row asserting it (§11). |
| **Square stars** (a 2 px feature has no shape) | The same bug at the bright end: an over-bright Gaussian saturates the whole quad. Nothing guarded that direction. | The `grow` guard, §5.5. |
| **The lander inflated past Mars** (a floor with no ceiling) | `_PxBounds.y = 64` already exists; `grow` is bounded by it. | Both bounds kept, §5.5. |
| **"Brightness on the alpha channel is a dial connected to nothing"** (`StarLook.cs`) | The exposure model must live in RGB. | `CS_RespondMagnitude` feeds `o.color`, never alpha. Additive passes return alpha 0 (§2.2). |
| **The sRGB vertex-colour trap** (BoltGraphXR; `quest-rendering-techniques` §4.5) | Every authored colour is display-referred. | `CS_DisplayToLinear` exactly once, already in `Point.shader` and `Panel`/`Text`. §14 lists what is still wrong. |
| **An additive pass writing alpha punches an opaque hole in passthrough** | Any new full-screen operator. | §2.2's `f(0) = 0` rule, the `image` G1c assertion, and refusing the post stack whose default is `alpha = 1`. |
| **"Foveation was never on"** (Fractarium) — a setting that reports itself enabled and is not | §8.5's dither/foveation interaction is invisible headlessly. | It goes on the headset checklist as an explicit look, not as a settings read. |
| **A gate that has never failed is not a gate** | Three new gate rows. | Each lands with a recorded deliberately-broken run (§11). |

---

## 14. What must be re-tuned when Gamma becomes Linear

Every constant in the list below was chosen by eye on a **Gamma** build, where the shader's output
value *was* the sRGB code. In Linear, the shader's output is luminance and the hardware encodes it.
For a value that was tuned as a display ratio `d`, the linear equivalent is `d^2.2`.

| Where | Constant | Gamma value | Linear equivalent | Note |
|---|---|---|---|---|
| `Point.shader` | `_ShapeGain.x` halo | 0.35 | **0.101** | multiplies *after* `CS_DisplayToLinear`, so it is a linear gain |
| `Point.shader` | `_ShapeGain.y` spike | 0.25 | **0.047** | same |
| `Point.shader` | `_Look.x` faintest brightness | 0.40 | **0.40, unchanged** | it is *inside* `CS_DisplayToLinear(tinted * level)` — correct as written (VERIFIED) |
| `Point.shader` | `_Look.y` exposure | 1.0 | **1.0** | dimensionless; §6 replaces its job anyway |
| `CosmoRim.shader` | `_Color.a` used as a brightness dial | as authored | `a^2.2` | the comment at `:103-105` correctly says not to decode `_Color.rgb`; it does **not** cover `.a`, which is a linear multiplier now |
| `Body.shader` | `_Ambient` | e.g. 0.05 | **0.0018** | night sides will go far darker than intended |
| `Body.shader` | `_Emission` (city lights) | as authored | `^2.2` | |
| `Body.shader` | `_SpecGain` | as authored | `^2.2` | |
| `CosmoAtmosphere` | `_Intensity` | as authored | `^2.2` | CONTRACT §7.4 already flags this |
| `StarLook` | `FaintestQuadDmm 3.8`, `BrightestQuadDmm 9.0` | — | **unchanged** | angles, not colours |
| `CosmoTheme` colours | — | **unchanged** | set via `Material.SetColor`; Unity linearises | never also call `.linear` |

**One correction to CONTRACT §7.4, with the arithmetic.** It says overlapping quads "now sum in
linear and will read brighter". They read **dimmer**:

```
Gamma:  two quads at display 0.5   → 0.5 + 0.5 = 1.00 stored → display 1.00 (clipped)
Linear: same single-quad look needs 0.5^2.2 = 0.2176 linear each
        blend on an sRGB format is performed in linear: 0.2176 + 0.2176 = 0.4353
        encoded back: 0.4353^(1/2.2) = 0.687 → display 0.687
```

So a two-quad overlap that used to clip to white now reads 0.69 and keeps its structure. Amendment
requested (CONTRACT §7 invites it: "argue it in `docs/reports/` with numbers"): §7.4's sentence
should read *"overlaps will stop clipping and read dimmer; tune `_Look.y` first, then the shape
gains in the table above."* The supporting line: `desc.sRGB = (activeColorSpace == Linear)`,
`UniversalRenderPipelineCore.cs:1581` (VERIFIED) — the target is an sRGB format, so the hardware
blend linearises, adds, and re-encodes.

**Re-tuning order** (do it once, in this sequence, or the knobs fight):
1. `_CS_Image.x` (`m0`) from the generated table — verify rung 0 comes out at 1.5.
2. γ, against a headset capture of Orion (Rigel 0.13, Betelgeuse 0.50, Bellatrix 1.64, Mintaka 2.25,
   and the belt's fainter members) — the constellation with the widest bright-end spread.
3. `_ShapeGain` halo, then spike.
4. `_CS_Image.z` (W), against a full-phase Earth.
5. `Body` ambient / emission / spec.
6. `CosmoAtmosphere` `_Intensity`, at the limb.
7. `_CS_Image.w` (dither), last, because it is the only one judged at 1 code value.

---

## 15. What can be verified headlessly on this box, and what cannot

**Verifiable on triton, with no headset and no GPU:**

| Claim | How |
|---|---|
| `CS_RespondMagnitude` is correct | `Core` unit test on the known-answer rows of §5.3 (m = −1.44, 0, 1.5, 2.59, 4.05, 6.5 → b to 1e-6). Core never touches `UnityEngine`, so it runs in the 2-second test pass. |
| The exposure table regenerates `m0 = 1.5` at rung 0 | Generator test against `stars.csstars`; the calibration check of §6.3. |
| Star counts above each bloom threshold | Counted from the catalogue, as §5.3 already was. |
| `f(0) = 0` for the shoulder and the dither | Play-mode capture: assert an undrawn region is exactly `(0,0,0,0)`. This is the new G1c `image` assertion, and it is a *maths* property, so llvmpipe proves it. |
| Vertex and draw counts | The existing `budget` assertion; the §5.3 mesh rule is deterministic. |
| Shader compiles, single variant, six stereo macros present | `stereo_gate.sh`, unchanged. |
| Banding is reduced | Render a synthetic linear ramp, count distinct 8-bit codes along a row with `_CS_Image.w` at 0 and at 1. The dither's job is to raise that count; it is arithmetic, not appearance. Confirm first that the harness's target is sRGB — llvmpipe will happily give a non-sRGB RT and make the test meaningless. |
| No shader writes alpha in an additive pass | Static gate on the eleven `frag` returns; already the rule. |

**Only a headset can prove:**

- Every millisecond in §12. A flat mono llvmpipe capture times nothing.
- That the eye buffer is really 8-bit sRGB with D24S8 (§2.1 is arithmetic on a doc quote).
- That the room does not haze — passthrough compositing does not exist in the harness.
- Whether the dither survives the compositor's lens resampling, or is smoothed away.
- **Whether foveation turns the dither into blocks in the periphery (§8.5).** This one is invisible
  in every headless test and is the most likely thing on this page to be wrong.
- Whether γ = 0.28 and W = 4.0 look right on an LCD worn in a lit room.
- Whether the bright end reads: Sirius against Vega against Deneb, which the current build cannot
  distinguish at all.

Say all of this in the capture notes, every time. A flat mono capture on llvmpipe cannot prove
stereo, passthrough, frame rate or foveation.

---

## 16. Implementation plan for a lane agent

Ordered so that each step is verifiable before the next, and so the visible payoff arrives first.

| # | Step | Files | Verify | Effort |
|---|---|---|---|---|
| 1 | `Core/Image/ImageLook.cs` + `Exposure.cs` (table, interpolation, **`Trim` disabled**) and their tests | `Core/Image/**`, `tests/CosmoSynth.Core.Tests/ImageTests.cs` | `./run-tests.sh` — known-answer rows of §5.3 and §6.3 | 0.3 d |
| 2 | Exposure-table generator, run offline over the catalogues; commit the table as data | `tools/cosmodata/build_exposure.py`, `SA/image/exposure.csv` | rung 0 regenerates `m0 = 1.5` (§6.3 calibration check) | 0.4 d |
| 3 | `CosmoCore.hlsl` addendum + `Placement.PublishGlobals` + `FrameContext` fields | §10.1, §10.2 | `stereo_gate.sh`; shaders still compile single-variant | 0.2 d |
| 4 | `Point.shader`: the response, `min(b,1)`, the spike gate, the `grow` guard | §10.3 | play-mode capture of the seven fixed views; compare the bright end against `/tmp/cs-baseline/app-running.png` | 0.3 d |
| 5 | `stars` mesh rule: halo + spikes only for `m ≤ 4.25` | `SkyAndRings.cs` / the `stars` layer builder | `budget` assertion: vertices ≤ 43,828 for the naked-eye set | 0.2 d |
| 6 | `CS_Shoulder` into `Body`, `Volume`, `Sky`, `CosmoAtmosphere`, `Ring` | §10.3 | capture the terminator and the limb | 0.2 d |
| 7 | `CS_Dither` into `Volume`, `Sky`, `CosmoAtmosphere`, `Body`, `Panel` | §10.3 | the distinct-code-count test of §15 | 0.2 d |
| 8 | The three gate rows + the `image` G1c assertion, each with a recorded failing run | `tools/gates/settings_gate.py`, the play-mode harness | the gate fails on the broken input, then passes | 0.3 d |
| 9 | `render:` line gains `img <m0>/<G>/<W>/<D> fmt <colourFormat>` | `RenderDiagnostics.cs` | G1a `DIAGNOSTICS_OK` | 0.1 d |
| 10 | Gamma→Linear re-tune, in the order of §14 | the constants table | seven fixed views, before/after pairs | 0.5 d |
| 11 | **Headset**, with the §15 list as the checklist | — | `adb logcat -d \| grep VrApi` for `App=` and `Fov=` | — |

**≈ 2.7 agent-days** before the headset. Steps 1–4 alone change how the sky looks.

Deliberately **not** in this plan, with the trigger for adding it:

- **Image-based metering** — add only if step 11 shows the model meter off by more than half a
  magnitude on a real view.
- **`Trim` adaptation enabled** — Phase 3, after a headset has judged the table.
- **A grading LUT** — only if a look is wanted that γ, W and the authored constants cannot reach.
- **`RemapFoveatedRenderingNonUniformToLinear` in the dither** — only if §8.5's blocking is actually
  visible. It costs a shader keyword, which this project spends carefully.

---

## 17. Open numbers, and the experiment that settles each

| # | Number | Current basis | Experiment |
|---|---|---|---|
| 1 | Eye buffer format and resolution | INFERRED from Meta's 90 MB figure and the contract | log `XRSettings.eyeTextureDesc.{graphicsFormat, depthStencilFormat, width, height}` in the `render:` line |
| 2 | 4.47 ms for an HDR intermediate | INFERRED, linear-in-bytes scaling of Meta's 1.525/0.71 ms | two APKs differing only in `m_SupportsHDR`; `VrApi` `App=` on a fixed view |
| 3 | 1.5–3.0 ms for URP bloom | INFERRED from pass count and pixel count | only worth running if (2) ever comes back cheap |
| 4 | 0.02 ms for the star fragments | INFERRED from Fractarium's 0.338 ms/step-coverage, ÷3 | a `cost_probe.sh` for point sprites: N full-coverage quads with the star fragment, N = 1,2,4,8 |
| 5 | 0.10 ms for the dither | INFERRED from an assumed ~1.4 Tops/s fp16 | same probe with the dither on and off |
| 6 | γ = 0.28 | VERIFIED as the owner's atlas constant; not verified on this content in a headset | Orion capture, γ ∈ {0.22, 0.28, 0.34, 0.40} |
| 7 | W = 4.0 | INFERRED from the surface-brightness ratios of §6.4 | full-phase Earth capture, W ∈ {2, 4, 8} |
| 8 | Dither 1.0 LSB and the foveation blocking | INFERRED | a headset look at the atmosphere limb and the dust volume, foveation on and off |
| 9 | The `m0` table beyond rung 9 | INFERRED hand arithmetic | the generator of step 2 replaces it; the hand values are its regression test |
| 10 | `AcesTonemap(0)` in fp16 | not verified | moot — ACES is rejected on three independent grounds |

---

*The one sentence to hand the next agent: there is no HDR buffer and no post-processing pass in this
app, the response curve lives at the point of emission, and every operator returns exactly zero for
zero because the room is behind the picture.*
