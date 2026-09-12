# Filling the sky — the Milky Way band, dust, nebulae, galaxies and the CMB

*Design pass, 2026-09-12. Slug `galaxy-and-sky`. Topic (a)–(f) of the Astra visual brief.
Nothing under `~/projects` was modified by this pass except this file.*

**Tags.** **V** = VERIFIED: I read the file, ran the command, or the arithmetic is exact from
values tagged V in this document. **I** = INFERRED: reasoning on top of a V fact, or a number
recalled from the literature. Every **I** that costs money or could be wrong on the headset gets
an experiment beside it. Two probes were run on triton for this document and their output is
quoted verbatim in §3.2 and §3.1.

**The one-line answer.** The frame is empty because the sky has no diffuse component at all. The
fix is *one* scalar field of the Galaxy — an emissivity `J(x)` and an extinction density
`ρ(x)` — rendered three ways by distance: **one enclosing raymarch volume** for everything you
can fly through, **one baked direction cube** for the unresolved light beyond it, and **one
oriented-quad population** for other galaxies. That collapses five planned layers
(`dust`, `nebulae`, `mwDisc`, `mwBand`, `dustExtent`) onto **one draw call and one step budget**,
removes the `dust`/`nebulae` arbitration problem entirely, and is the only formulation that stays
correct when the viewer leaves the disc.

---

## 0. What this document decides, and what it does not

| Decides | Section |
|---|---|
| The representation of the Milky Way band from inside and outside, and its LOD chain | §2, §3.5 |
| Which dust cubes ship, at what resolution, packed how, and the extinction model | §3 |
| How extinction is applied exactly once across the dust, star and band layers | §3.4 |
| Nebulae: measured position, procedural appearance, and the mechanism that keeps them honest | §4 |
| Galaxy impostors: orientation, profile, the sub-pixel fallback and the cross-fade | §5 |
| The CMB: shipped resolution (changed), colour map, fade-in, and why it is never a wall | §6 |
| Zodiacal light and airglow: yes, both, at ~0 draw cost | §7 |
| Draw / vertex / step-coverage / byte arithmetic for every layer and the compose order | §8 |
| Amendments this design needs to CONTRACT §4.5, §5.4, §7.3, A.6, A.7, C.7, C.12 | §11 |

**Does not decide:** the image pipeline (HDR, tone map, bloom, exposure) — that is a sibling
topic and every radiance constant here is expressed as a ratio so it survives whatever that
decides; star point-spread shape; labels; audio; the fiction bridge.

**Hard dependency, read this first.** `Core/Frames/StandardFrames.cs` builds ICRF→galactic with
all three signs flipped (HANDOVER-ASTRA-VISUAL §6, V as a reported defect). **Every field in this
document is evaluated in galactic coordinates.** Nothing here may be built or measured until that
matrix is fixed and has its test. It is listed as `p0-core`'s file in CONTRACT §9.3.

---

## 1. The one idea

CONTRACT §5.4 plans five separate layers for the same physical object:

| Layer | Window (log10 U) | Placement (§4.5) | What it actually is |
|---|---|---|---|
| `dust` | −19.0 … −14.6 | Enclosing volume | ρ(x), measured, |x| < 1.28 kpc |
| `nebulae` | −17.5 … −13.5 | Enclosing volume | ρ(x), procedural, three boxes |
| `mwDisc` | −21.8 … −17.4 | Rigid layer group / Volume | J(x) + ρ(x), seen from outside |
| `mwBand` | −16.6 … +3.0 | Sky, per eye | J(x) + ρ(x), seen from inside |
| `dustExtent` | with `dust` | Ribbon | the boundary of the measured part of ρ |

Five layers, two of them flagged `MayEnclose`, whose windows overlap over 2.9 decades, owned by
one lane, with a framework rule (§4.3 lifecycle 4) and a gate (`volumes`) whose entire job is to
stop two of them drawing at once. There is a 0.8-decade dead band between `mwDisc` ending at
−17.4 and `mwBand` starting at −16.6 where neither draws (V, from the §5.4 table).

They are the same two functions. Write them once:

```
J(x)  : galactic pc -> radiance per unit length   (unresolved starlight; Procedural over Inferred)
rho(x): galactic pc -> A_V magnitudes per pc      (Measured inside 1.28 kpc, Inferred outside)
```

and render them by **where the viewer is relative to the structure**, not by layer identity:

| Viewer | Renderer | Draws | Cost |
|---|---|---|---|
| Inside the box (`sky` … `milkyway`, +3.0 … −18.2) | one **Enclosing** `CosmoSynth/Volume` march over the ±20 kpc galaxy box, log-spaced in `f(d)` | 1 | ≤ 30 step-cov (§8) |
| Beyond the march's useful reach, all directions | one **baked direction cube**, re-integrated when the viewer moves >25 pc, sampled by `CosmoSynth/Sky` | 1 | 0.043 step-cov/frame while moving, **0 at rest** |
| Outside the box (`halo` … `cmb`, −19.4 … −23.9) | the **same box**, now Rigid, marched the same way | 1 | ≤ 10 step-cov (§8.2) |

The band, the dust, the nebulae and the disc-from-outside become **one draw call**. The dead band
closes because there is nothing to hand over between. The arbitration rule becomes structurally
true instead of enforced: there is only ever one `MayEnclose` volume because there is only one
volume.

`dustExtent` survives unchanged — it is the ribbon that shows where **Measured** ρ stops and
**Inferred** ρ begins, and it is the single most important honesty object in this whole document.

---

## 2. (a) The Milky Way band

### 2.1 Why not a skybox, and why not a per-pixel integral

| Option | Cost | Verdict |
|---|---|---|
| Painted all-sky panorama (ESO/Gaia style plate) | 6×1024² RGB = 18.9 MB, 1 draw | **Reject.** Licence aside, it is a photograph from *one* point. The app's entire premise is leaving that point. It is also the thing the owner's own Atlas README refuses ("No artist impressions, no procedural filler") |
| Per-pixel integral in the `Sky` fragment | full-screen × N steps = **N step-coverages**; at N=16 that is 16 of the 33 budgeted | **Reject.** Half the frame's raymarch budget for a field that has no detail finer than a degree |
| Point cloud of ~10⁶ procedural filler stars | 4 M verts | **Reject.** Over budget by 3×, and 60 k stars is 1.45 per deg² — a dusting, not a glow. Density is the whole point |
| **Baked direction cube, re-integrated on viewer motion** | 0.39 MB × 2 buffers, 1 draw, 0.043 step-cov/frame while moving | **Use** |

The bake is not a skybox, and the distinction is the whole design: **a skybox is authored once at
one place; this is recomputed from `J` and `ρ` whenever the viewer moves 25 pc.** From inside the
disc it produces a band; from 5 kpc above the plane it produces a disc seen at an angle; from
Andromeda's distance it produces a faint smudge at the right position angle. It is correct
everywhere because it is never stored, only cached.

There is also a free stereo win. CONTRACT R7 (§2.5 point 7) forbids per-pixel stochastic terms in
a march because the two eyes rasterise the same world point at different pixel coordinates and the
noise decorrelates into binocular rivalry. **A bake has no per-pixel term at all** — both eyes
sample the same cube with the same direction. The band is stereo-consistent by construction.

### 2.2 The emissivity `J(x)`

Galactocentric from heliocentric: the Galactic Centre is at heliocentric `(R0, 0, 0)` pc with
`R0 = 8150` (Reid+2019, already `GalaxyModel.R0` in CONTRACT C.9, **V** as a contract constant),
so `pg = p − (R0,0,0)`, `R = |pg.xy|`, `z = pg.z`.

```
J(p) = j0 * [ ( exp(-(R-R0)/hR  - |z|/hz )                      thin disc, the band
            +   fT * exp(-(R-R0)/hRT - |z|/hzT) )               thick disc, the wings
            * trunc(R) * (1 + aA * arms(R, phi, z))
            +   fB * exp(-m/rB) ],   m = |(pg.x, pg.y, 2*pg.z)| flattened bulge
trunc(R) = smoothstep(Rt + 2000, Rt - 2000, R)
```

| Constant | Value | Justification | Tag |
|---|---|---|---|
| `R0` | 8150 pc | Reid+2019; the one value the contract already fixes (C.9) | **V** |
| `hR` | 2600 pc | Bland-Hawthorn & Gerhard 2016 (ARA&A 54, 529) thin-disc scale length 2.6 ± 0.5 kpc | **I**, from memory — read the review and pin |
| `hz` | **200 pc** | the *light* is not the *mass*. BHG's 300 pc is the old thin disc; the band's surface brightness is dominated by young luminous stars with hz ≈ 100–150 pc. 200 pc is the compromise and it is **the single most visible constant here** — it sets the band's apparent width | **I**, Procedural; tune against a published ISL latitude profile, §10 |
| `hRT`, `hzT`, `fT` | 2000 pc, 900 pc, 0.04 | BHG thick disc, 4 % local normalisation | **I** |
| `rB`, `fB` | 700 pc, set so the bulge integrates to ≈20 % of the disc | boxy/peanut bulge scale; the flattening `q = 0.5` is the `2*pg.z` in `m` | **I**, Procedural |
| `Rt` | 14000 pc | the stellar disc's sharp outer break | **I** |
| `aA`, arm width | 0.30, 300 pc | arms modulate, they do not create. `arms()` uses `GalaxyModel`'s Reid+2019 log-periodic fits, which CONTRACT C.8 already makes Core constants, **drawn only over each arm's fitted beta range** and faded outside it — the same rule the `arms` ribbon layer obeys | **I**, Procedural over Inferred |

**Calibrating `j0` — and the double-count that would otherwise ship.** The app already draws
119,669 real stars (C.1, **V**). If `J` is calibrated to the *total* integrated starlight, every
star brighter than the catalogue's completeness limit is drawn twice. The calibration is therefore
of the **remainder**:

```
j0 = ( ISL_pole_published  -  ISL_pole_catalogue ) / I_model_pole(j0 = 1)
```

- `ISL_pole_published` ≈ 30 S10(V) at the galactic poles, where 1 S10(V) = one V = 10 star per
  square degree = `10 + 2.5 log10(1.296e7)` = **27.78 mag/arcsec²** (**V**, exact arithmetic), so
  30 S10 = 24.09 mag/arcsec². *The 30 is* **I** *from memory of Leinert et al. 1998 (A&AS 127, 1),
  "The 1997 reference of diffuse night sky brightness", Table 17. Read the table and pin it.*
- `ISL_pole_catalogue` = `sum 10^(-0.4 * vmag)` over `stars.csstars` rows within 20° of the NGP,
  divided by the solid angle, converted to S10. **Computable on this box with no GPU**, and it
  must be run: if it is a large fraction of 30 S10 the model's faint-end normalisation is wrong,
  not `j0`.
- The pole is used because extinction there is negligible (**V**: the Edenhofer column toward
  |b| > 70° is E = 0.021, A_V ≈ 0.06 mag — §3.2).

Then **validate, do not fit**, against a second anchor: the plane at `l = 90°, b = 0` (ISL ≈ 200 S10
= 22.0 mag/arcsec², **I**, same table). If the model misses that by more than 2×, `hR`, `hz` or
`fB` is wrong — `j0` must not be bent to hide it. This split is the difference between a model and
a curve fit, and it is cheap: both anchors are one CPU integral each.

### 2.3 The extinction `ρ(x)`, and where measurement stops

```
rho(p) =  measured_near(p)              |p| inside the Leike box      (2 pc data at 4 pc)
       |  measured_far(p)               |p| inside the 1248 pc sphere (10 pc data at 20 pc)
       |  analytic(p)                   everywhere else
analytic(p) = n0 * exp(-(R - R0)/hRd - |z|/hzd)
```

| Constant | Value | Justification | Tag |
|---|---|---|---|
| `hRd` | 3000 pc | the dust disc is longer than the stellar disc | **I** |
| `hzd` | 125 pc | the cold-dust scale height; it is what makes the dark lane thinner than the bright band, which is the single most recognisable feature of the real band | **I** |
| `n0` | set so that ∫ from the Sun to the GC through the plane = **30 mag A_V** | the extinction to Sgr A* is ~30 mag in V (A_K ≈ 2.5); it is why the Galactic Centre is invisible optically and it *must* be in the model or the band will glow brightest exactly where the real one goes black | **I**, well known; confirm against a cited value |

**The handover must not double-count.** The analytic disc is a model of *all* the dust including
the near 1.28 kpc. Inside the measured region the analytic term is **replaced**, not added. The
shader takes the measured value where it exists and the analytic value only outside — one
`if`, no blending except across a 40 pc feather at the box faces to kill the seam.

**Edenhofer's `v == 0` means "no data", not "no dust"** (**V**, staged `NOTES.md`: 8,632,456 of
16,777,216 voxels are outside the 1248 pc map sphere). A marcher that reads 0 as vacuum punches
square corners of clear sky into the Galaxy. The shader tests `|p| < 1248 pc` **and** `v > 0`, and
falls through to the analytic term — which is also why the analytic term is not optional.

### 2.4 The bake: what, where, when, how much

One pass, two render targets (MRT), six cube faces, driven by a **motion trigger**.

| | Value | Why |
|---|---|---|
| `_BandCube` | cube, 6 × 128², **B10G11R11_UFloat** (4 B/texel) | 0.39 MB. **Not RGBA8**: a dark smooth gradient in 8 bits bands visibly, and this is the darkest gradient in the app. 11-bit float mantissa, same bytes |
| `_ExtCube` | cube, 6 × 128², RGBA8 | 0.39 MB. Transmission `exp(-0.921 τ)` at **100 / 250 / 600 / 1250 pc**, one per channel. 8 bits is right here: it multiplies a brightness, and 1/255 of a transmission is invisible |
| Double buffer | ×2 | the roll writes the back buffer; the Sky pass reads the front and they swap on the last face. Avoids a half-updated cube during flight |
| **Total texture** | **1.57 MB** | against the 100 MB §7.3 texture ceiling |
| Face size knob | `--face 256` | 4× the texels and 4× the bake cost; face-centre texel goes 0.89° → 0.45°. Use only if the headset shows the far band as visible facets (§10) |
| Steps per texel | **24**, log-spaced (§3.3) | the field has no structure finer than the arm width (300 pc) beyond the measured region; 24 log steps put the first at 3 pc and the last at 4 kpc |
| Trigger | rebake all six faces, one per frame, when `|x_viewer − x_baked| > 25 pc` **or** a model/rho uniform changes | 25 pc is 1/8 of `hz` and 1/50 of the nearest cloud distance — a 25 pc parallax moves the band's structure by under a texel at every distance that contributes. **I**; §10 gives the check |

Resolution sanity: a 128 face spans 90°, so a face-centre texel is `2/128` rad = **0.89°**, and an
edge-midpoint texel is 0.45°. The band's diffuse structure is degrees; the *sharp* structure —
the Great Rift, the Coalsack, the Aquila Rift — is all inside 1.28 kpc and is drawn at full
resolution by the volume march **over** the bake. The bake is deliberately the blurry half.

**Cost arithmetic.** One eye buffer is 2064 × 2208 = 4,557,312 px (§2.0, **V**); both eyes =
9,114,624 px. One step-coverage = one march step over both eyes (Fractarium's definition,
quest-rendering-techniques §5.2, **V**).

```
full rebake  = 6 faces x 128^2 x 24 steps = 2,359,296 samples
             = 2,359,296 / 9,114,624      = 0.2589 step-coverages
one face/frame                            = 0.0431 step-coverages per frame
  at kappa = 0.338 ms/step-cov (Fractarium's Mandelbulb UPPER bound, V) = 0.0146 ms
  at kappa = 0.06 (analytic + 1 3D fetch, I)                            = 0.0026 ms
at rest (no motion beyond 25 pc)                                        = 0 ms
```

Against a 33 step-coverage frame budget, a moving viewer spends **0.13 %** of it on the band.
At `--face 256` it is 0.52 %. This is the cheapest thing in this document and it is the one that
removes most of the emptiness.

**The mechanism.** `Graphics.SetRenderTarget(rt, 0, (CubemapFace)f)` then a 4-vertex quad whose
`POSITION` attributes are that face's four corner **ICRF directions** and whose `TEXCOORD0` is the
clip-space xy. `CosmoSynth/Sky` gets a `_Mode` uniform: `0` = sample the cube (today's behaviour,
unchanged), `1` = integrate and write. One shader, one pass, a uniform branch — CONTRACT §7.1
rule 1 explicitly allows "switches are uniform branches", so this stays single-variant and adds
no twelfth shader. The bake target is 128×128 = one tile; the render-target switch costs a tile
flush of 64 KB, once per frame while moving.

**Risk, named.** An extra render pass per frame is the same *family* of surprise as the two-camera
stack and Optimize Buffer Discards that is the suspected cause of the 1.7.0 "100 Earths on a grid"
(HANDOVER §2, baseline report). It is one two-hundredth the size, but it must be measured on
device, not assumed. **Fallback if it misbehaves:** bake on the CPU at 6 × 32² (1,024 texels ×
24 steps = 24.5 k integrations per face per frame, ~0.2 ms of C#) and upload with
`Cubemap.SetPixelData`; the far band goes from 0.89° to 2.8° texels, which for a field with no
structure below a degree costs almost nothing visible. Both paths must exist behind one interface
before the first headset build, because discovering this on device with no fallback costs a day.

### 2.5 The Sky pass, written out

Additions to `CosmoSynth/Sky` (Appendix A.6). Everything not shown is unchanged; the six stereo
macros, `DisableBatching`, `Cull Off`, `Blend One Zero`, `ZWrite Off`, `ZTest LEqual` and
`CS_EyePositionWS()` all stay exactly as A.6 has them.

```hlsl
// --- Properties (added to A.6) ------------------------------------------------
//  _Mode      ("0 sample, 1 bake", Float) = 0
//  _BandCube  ("Baked band radiance, RGB float, faces in ICRF components", Cube) = "black" {}
//  _Gains     ("x band, y zodiacal, z airglow, w CMB", Vector) = (1,1,0,1)
//  _ViewerGal ("Viewer position, galactic heliocentric pc", Vector) = (0,0,0,0)
//  _Gal0      ("x j0, y 1/hR, z 1/hz, w R0 pc", Vector)
//  _Gal1      ("x fThick, y 1/hRT, z 1/hzT, w Rtrunc pc", Vector)
//  _Gal2      ("x fBulge, y rBulge pc, z armAmp, w armWidth pc", Vector)
//  _Dust0     ("x n0 A_V/pc, y 1/hRd, z 1/hzd, w measured radius pc", Vector)
//  _Bake      ("x steps, y t0 pc, z sMax pc, w unused", Vector) = (24, 20, 80000, 0)
//  _ExtDist   ("transmission slice distances, pc", Vector) = (100,250,600,1250)
// A matrix may NOT live in UnityPerMaterial (R4). The ICRF->galactic rotation is a
// GLOBAL, beside _CS_IcrfToWorld, written once per frame by Placement.PublishGlobals:
//   float4x4 _CS_IcrfToGal;    // added to CosmoCore.hlsl (A.2)

struct Attributes
{
    float3 positionOS : POSITION;    // ICRF direction, both in sky mode and in bake mode
    float2 bakeClip   : TEXCOORD0;   // bake mode only: clip-space xy of this corner
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings vert(Attributes v)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 dirIcrf = normalize(v.positionOS);
    if (_Mode > 0.5)
    {
        // Baking into one cube face: no eye, no stereo, no compression. That is CORRECT
        // and it is the point -- the band is at infinity and must be identical in both
        // eyes (R7 turns any per-eye difference here into binocular rivalry).
        o.positionCS = float4(v.bakeClip, 0.0, 1.0);
    }
    else
    {
        float3 dirWS = normalize(CS_IcrfDirToWorld(dirIcrf));
        o.positionCS = TransformWorldToHClip(CS_EyePositionWS() + dirWS * _CS_Compress.z);
    }
    o.dirIcrf = dirIcrf;
    return o;
}

// --- the field, shared by the bake and by CosmoSynth/Volume (one copy, in an .hlsl) ----
float CS_GalaxyEmissivity(float3 pGal)          // galactic heliocentric pc -> radiance/pc
{
    float3 pg = pGal - float3(_Gal0.w, 0.0, 0.0);       // galactocentric
    float  R  = length(pg.xy);
    float  az = abs(pg.z);
    float  dR = R - _Gal0.w;
    float  thin  =          exp(-dR * _Gal0.y - az * _Gal0.z);
    float  thick = _Gal1.x * exp(-dR * _Gal1.y - az * _Gal1.z);
    float  m     = length(float3(pg.xy, pg.z * 2.0));   // bulge flattening q = 0.5
    float  bulge = _Gal2.x * exp(-m / _Gal2.y);
    float  trunc = smoothstep(_Gal1.w + 2000.0, _Gal1.w - 2000.0, R);
    float  arms  = 1.0 + _Gal2.z * CS_ArmModulation(R, atan2(pg.y, pg.x), az);
    return _Gal0.x * ((thin + thick) * trunc * arms + bulge);
}

float CS_DustAnalytic(float3 pGal)              // A_V magnitudes per parsec
{
    float3 pg = pGal - float3(_Gal0.w, 0.0, 0.0);
    return _Dust0.x * exp(-(length(pg.xy) - _Gal0.w) * _Dust0.y - abs(pg.z) * _Dust0.z);
}

// --- fragment -----------------------------------------------------------------
struct FragOut { half4 band : SV_Target0; half4 trans : SV_Target1; };

FragOut frag(Varyings i)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    FragOut f;

    if (_Mode < 0.5)
    {
        // ---- sample: the normal sky draw, 2 fetches and a LUT ----
        half  raw = SAMPLE_TEXTURECUBE(_SkyCube,  sampler_SkyCube,  normalize(i.dirIcrf)).r;
        half  t   = saturate(0.5h + (raw - 0.5h) * (half)_Contrast);
        half3 cmb = SAMPLE_TEXTURE2D(_Lut, sampler_Lut, float2(t, 0.5)).rgb * (half)_Gains.w;
        half3 bnd = SAMPLE_TEXTURECUBE(_BandCube, sampler_BandCube, normalize(i.dirIcrf)).rgb
                    * (half)_Gains.x;
        half3 zod = CS_Zodiacal(i.dirIcrf) * (half)_Gains.y;    // section 7, ~14 ALU
        half3 air = CS_Airglow (i.dirIcrf) * (half)_Gains.z;    // section 7, ~6 ALU, rung 0 only
        half  k   = (half)(_Immersion * _Fade);
        f.band = half4((cmb + bnd + zod + air) * k, k);         // premultiplied, as A.6
        f.trans = 0;
        return f;
    }

    // ---- bake: integrate J and rho along this texel's direction ----
    float3 o   = _ViewerGal.xyz;
    float3 d   = normalize(mul((float3x3)_CS_IcrfToGal, normalize(i.dirIcrf)));
    int    N   = (int)_Bake.x;
    float  t0  = _Bake.y;
    float  kk  = log(1.0 + _Bake.z / t0) / N;      // uniform in ln(1 + s/t0)  ==  uniform in f(s)
    float3 L   = 0.0;
    float  tau = 0.0;                              // A_V magnitudes accumulated so far
    float4 tauSlice = 0.0;

    [loop] for (int n = 0; n < N; n++)
    {
        float sa = t0 * (exp(kk * n)       - 1.0);
        float sb = t0 * (exp(kk * (n + 1)) - 1.0);
        float ds = sb - sa;
        float3 p = o + d * (0.5 * (sa + sb));      // midpoint rule

        float dtau = CS_DustDensity(p) * ds;       // measured cube inside, analytic outside
        float j    = CS_GalaxyEmissivity(p);

        // emit then absorb: this slab's light crosses the tau already between it and the
        // eye, plus half its own. exp(-0.921034 * A_V): 0.921034 = ln(10)/2.5, exact.
        L   += j * ds * exp(-0.921034 * (tau + 0.5 * dtau));
        tau += dtau;

        // tau is monotonic, so max() picks the value at the first step past each distance
        tauSlice = max(tauSlice, tau.xxxx * step(_ExtDist, sb.xxxx));
    }

    f.band  = half4(L * _BandTint, 1.0h);          // _BandTint: display-referred, decoded ONCE
    f.trans = half4(exp(-0.921034 * tauSlice));    // transmission at 100/250/600/1250 pc
    return f;
}
```

Three details that are load-bearing:

1. **`_BandTint` is decoded with `CS_DisplayToLinear` on the CPU side or once here, never twice.**
   The project's own scar: BoltGraphXR shipped washed-out vertex colours because Linear Unity does
   not convert them (quest-rendering-techniques §4.5, **V**). The band's colour ramp is a
   hand-picked, display-referred constant like every `CosmoTheme` value (§7.4).
2. **The bake writes alpha 1 and the sample path writes `k = _Immersion * _Fade`.** In passthrough
   `_Immersion = 0`, the layer skips the draw entirely, and nothing writes coverage. The bake
   writes to an offscreen cube, not the eye buffer, so its alpha never reaches the compositor.
3. **`f.trans` is the entire per-star extinction system.** It is produced for free by a march that
   was going to compute `tau` anyway. §3.4.

### 2.6 The band in passthrough — deferred, with the reason

CONTRACT §5.4 makes `mwBand` Space-mode only (`RoomVisibility <= 0.35`). That is correct as the
shader stands: queue 1000 with `Blend One Zero` is the opaque immersion fill, and in passthrough
it must not draw.

A faint additive band *over* the room is physically the right MR behaviour — additive light adds
to a room, it does not occlude it — and it would be a large part of making passthrough mode not
look empty. It needs a second, additive draw (`Blend One One, Zero One`), which means either a
second pass (forbidden by §7.1 rule 1) or a property-driven blend.

**Do not use a property-driven blend.** `tools/gates/stereo_gate.sh:77` greps literally for
`^\s*Blend\s+One\s+One` and requires `, Zero One` after it (**V**, read the gate). A
`Blend [_SrcBlend] [_DstBlend]` form matches nothing, passes the gate silently, and can write
alpha at runtime — it would open a hole in exactly the gate that exists to stop passthrough
holes. That is a worse outcome than a dark passthrough sky.

**Deferred to Phase 3**, as one item: a `CosmoSynth/Point` draw in `_Space = 2` (sky) mode over a
subdivided sphere whose vertex colours are sampled from `_BandCube` on the CPU at bake time — the
Point shader is already additive and already gate-clean. ~5 k verts, 1 draw. Skipped now because
it is a second representation of the same thing and the Space-mode band is what the seven fixed
capture views show.

---

## 3. (b) The dust cube

### 3.1 What ships, and the sizes measured on this box

Both staged cubes are CC BY 4.0 (DATA-LICENCE-AMENDMENT row 10, E21/E22, **V**) and both are
already on the box. Neither ships as staged: Edenhofer is 16.78 MB and Leike 36.96 MB, against a
150 MB APK ceiling of which `SA/` is planned at ~29.8 MB.

Probe run on triton (`~/tmp-astra-sky/dust_probe.py`, numpy 2.5.1), output verbatim:

```
== raw / deflated, as staged
  edenhofer 256^3 @10pc        raw   16.777 MB  deflate   5.622 MB  ratio 0.335
  leike 370x370x270 @2pc       raw   36.963 MB  deflate  27.099 MB  ratio 0.733

== far cube  Edenhofer 128^3 @20 pc, +-1280 pc
  raw 2.097 MB   deflate 0.733 MB   nonzero 49.1 %

== near cube  Leike 185x185x135 @4 pc, +-370/+-270 pc
  raw 4.620 MB   deflate 3.369 MB
```

**Shipped layout** (all **V** except the mip factor, which is exact arithmetic):

| File | Grid | Step | Extent (galactic pc) | Raw | Deflated (APK) | GPU with mips |
|---|---|---|---|---|---|---|
| `galaxy/dust_far.csdu` | 128³ | 20 pc | ±1280 x, y, z | 2.10 MB | **0.73 MB** | 2.40 MB |
| `galaxy/dust_near.csdu` | 185 × 185 × 135 | 4 pc | ±370 x, y; ±270 z | 4.62 MB | **3.37 MB** | 5.28 MB |
| **total** | | | | 6.72 MB | **4.10 MB** | **7.68 MB** |

- Against CONTRACT §7.3's `dust` texture row, "3.3–10.3 MB (R8 3D)": **7.68 MB, inside it.**
- Against C.7's Vergely plan (10.29 MB raw / 6.22 MB deflated, **V**): smaller in the APK, finer
  where it matters (4 pc inside 370 pc against 20 pc everywhere), and **CC BY instead of
  unlicenced**. C.7 is superseded by the amendment already; this fills in the numbers.
- **Two files, one magic, meta `kind` distinguishes them** — the C.8 `cepheids/globulars/masers`
  pattern, so either can be dropped on its own by `drop_test.sh`.
- **Mips are not optional** (the ×1.143 factor above, `sum 8^-k`). §3.3 marches with steps that
  grow to 400 pc; sampling a 20 pc voxel with a 400 pc step without prefiltering both aliases
  and *biases* the optical depth. `lod = log2(dt / voxelPc)` is what 3D mips are for. This is
  +0.96 MB and it removes a whole class of shimmer on a moving viewer.
- **Two cubes, one volume.** The march picks the near texture inside the Leike box and the far
  one outside, with a 40 pc feather at the boundary. One draw, one `MayEnclose`, one step budget.
  Two `Texture3D` samplers, one extra compare per step.

**Why box-average in density, not in the u8 code.** The staged u8 is a log encoding; averaging
codes averages logarithms, which is a geometric mean and systematically *under*-states a column
through clumpy dust. The probe decodes, averages, re-encodes (`dust_probe.py`, and it also zeroes
`v == 0` first so "no data" never averages in as `vmin`).

**Knobs**, both free: `--far-step 40` gives 64³ = 0.26 MB if the APK ever gets tight; `--near-step 8`
gives 93 × 93 × 68 = 0.59 MB. Neither is needed at today's 46 MB.

### 3.2 The extinction unit, calibrated on this box

Edenhofer's unit is `E` of Zhang, Green & Rix 2023 per parsec — *not* magnitudes (staged
`NOTES.md`, **V**: "Converting E to A_V requires the Zhang+2023 extinction curve; do not silently
assume A_V = E"). CONTRACT §13 item 28 lists this factor as unsettled and says to take it from the
paper. **It can be measured here instead**, because Leike's unit *is* physical and stated in its
own readme (**V**, `cube_meta.json`: "a value of 1 corresponds to an intensity reduction of e^-1
for G-Band photons travelling one parsec"; `A_G [mag/pc] = 1.0857 * value`).

Second probe (`dust_probe2.py`, 4,000 random sightlines integrated to 260 pc through *both* cubes,
least squares through the origin), verbatim:

```
columns to 260 pc, 4000 of 4000 sightlines
  A_G = 2.2743 * E    Pearson r = 0.9358
  -> A_V/E = 2.883  (A_G/A_V = 0.789)
  -> A_V/E = 2.740  (A_G/A_V = 0.83)
  -> A_V/E = 2.645  (A_G/A_V = 0.86)
  per-sightline A_G/E: p05 1.784  p50 3.148  p95 8.571

polar cap |b|>70 deg, 254 sightlines, to 260 pc
  median E   = 0.0071
  median A_G = 0.0274 mag
  median E to 1248 pc (Edenhofer) = 0.0210
    if A_V(pole,total) = 0.05  ->  A_V/E = 2.38
    if A_V(pole,total) = 0.08  ->  A_V/E = 3.80
```

**Result: `A_V = 2.88 x E(ZGR23)`, adopt 2.8, uncertainty about ±15 %.** Two independent routes
agree — a 4,000-sightline column regression against a cube whose units are stated (r = 0.936), and
the polar column against the known polar A_V of 0.05–0.10 mag. The earlier voxel-by-voxel ratio
(median 3.33, p05–p95 spanning 0.83–14.4) is **not** the right statistic: the two reconstructions
disagree voxel by voxel far more than they disagree in column, which is exactly what you expect
from two different inversions at different resolutions, and it is the columns that render.

§13 item 28 is therefore settled to engineering accuracy without the paper; reading Zhang+2023
remains the confirmation, and if it disagrees by more than 15 % the probe is wrong and must be
re-run, not overridden. The factor goes in the `csdu` meta as `toAv`, never hard-coded in a
shader.

Integrated columns from the Sun through the shipped far cube, with the adopted factor (**V**,
first probe):

| Direction | E to 1248 pc | A_V |
|---|---|---|
| GC, l = 0 b = 0 | 0.902 | 4.13 mag |
| anticentre, l = 180 | 0.247 | 1.13 |
| NGP, b = +90 | 0.0217 | 0.100 |
| l = 90 | 0.512 | 2.34 |
| Orion, l = 209 b = −19 | 1.209 | 5.53 |
| staged column map, maximum over the sky | 2.027 | **9.28** |

These are the numbers the `dust` importer should assert on (the C.7 pattern of asserting on
values, not headers). They also pin the frame: A_V toward the anticentre must be roughly a third
of A_V toward the centre, and the pole must be ~0.1 — an axis flip destroys all three at once.

### 3.3 Marching in compressed distance

The problem: one box has to cover 4 pc structure at the Local Bubble wall *and* 20 kpc of disc.
30 uniform steps over ±20 kpc is 1.3 kpc per step, which resolves nothing; 30 uniform steps over
±370 pc reaches nothing.

The solution is already in the app. CONTRACT §2.3 fixes one distance map
`f(d) = d0 (1 + ln(d/d0))`, and §2.6 applies it as one similarity per group. **March uniformly in
`f`**, i.e. uniformly in `ln(1 + s/t0)`:

```
s(n) = t0 * (exp(k n) - 1),     k = ln(1 + sMax/t0) / N
```

With `t0 = 20 pc`, `sMax = 40 kpc` (box diagonal from a corner), `N = 30`:

| | value |
|---|---|
| `k` | `ln(1 + 2000)/30` = 0.2534 |
| first step `s(1) − s(0)` | **5.8 pc** |
| step at 370 pc (the near-box edge) | 114 pc |
| step at 1.28 kpc (the measured edge) | 350 pc |
| last step | 9.0 kpc |
| steps inside the near box | 12 of 30 |
| steps inside the measured region | 17 of 30 |

Half the budget lands in the 1.28 kpc where the data is measured, which is exactly the right
allocation, and it falls out of the app's own scale law rather than from a tuned table. The mip
level `log2(dt / voxelPc)` tracks the growing step so each sample is a prefiltered average over
the interval it stands for.

`t0` is a uniform, not a constant: `t0 = clamp(0.02 * distanceToNearestVolumeFeature, 0.05, 20)`
pc, computed on the CPU, so flying into a 5 pc nebula core tightens the near steps automatically
(§4).

Additions to `CosmoSynth/Volume` (Appendix A.7); everything else — `DisableBatching`, `Cull Front`,
`ZTest LEqual`, the premultiplied `Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha`, `eyeOS`
computed in **vert** (R5), `CS_JitterDir` in **ray space** (R7) — is unchanged:

```hlsl
TEXTURE3D(_DensityNear); SAMPLER(sampler_DensityNear);   // Leike, 4 pc
TEXTURE3D(_DensityFar);  SAMPLER(sampler_DensityFar);    // Edenhofer, 20 pc
// _NearBox ("xyz half-extent pc, w feather pc") ; _FarBox ("xyz half-extent pc, w sphere pc")
// _DecodeN / _DecodeF ("x vmin, y ln(vmax/vmin), z toAv, w unused")

float CS_DustDensity(float3 pGal)                 // A_V magnitudes per parsec
{
    float3 an = abs(pGal);
    float  wN = 1.0 - saturate((max(max(an.x/_NearBox.x, an.y/_NearBox.y), an.z/_NearBox.z)
                                - 1.0) * (_NearBox.x / _NearBox.w) + 1.0);
    float  rho = 0.0;
    if (wN > 0.0)
    {
        float v = SAMPLE_TEXTURE3D_LOD(_DensityNear, sampler_DensityNear,
                                       pGal / (2.0*_NearBox.xyz) + 0.5, _Lod).r;
        rho += wN * _DecodeN.x * exp(v * _DecodeN.y) * _DecodeN.z;
    }
    if (wN < 1.0)
    {
        float r = length(pGal);
        float v = SAMPLE_TEXTURE3D_LOD(_DensityFar, sampler_DensityFar,
                                       pGal / (2.0*_FarBox.xyz) + 0.5, _Lod).r;
        // v == 0 is NO DATA, not vacuum (staged NOTES pitfall 1): fall through to analytic,
        // or the map sphere's corners become square holes of clear sky in the Galaxy.
        float measured = (v > 0.0 && r < _FarBox.w)
                       ? _DecodeF.x * exp(v * _DecodeF.y) * _DecodeF.z : -1.0;
        rho += (1.0 - wN) * (measured >= 0.0 ? measured : CS_DustAnalytic(pGal));
    }
    return rho;
}
```

### 3.4 Extinction applied exactly once — the rule made mechanical

CONTRACT "things an implementer must not miss" #5: *extinction is applied once; `av` is baked,
`M_V` is de-reddened, `dust` never re-dims V* (R41). Read literally that is a prohibition; what it
needs is a mechanism, because the physics it encodes is:

1. `stars.csstars` stores observed `vmag` and a baked `av` = A_V along **Sun → star** (C.1, **V**).
2. Core computes `M_V = vmag − 5 log10(d/10) − av`. That is the **intrinsic** luminosity, with
   the Sun's line of sight removed.
3. The renderer must then re-apply extinction along **eye → star**, from wherever the eye is.
   From the Sun that gives `vmag` back, exactly. From 500 pc away it gives the sky as seen from
   there. **Nothing dims `vmag`, ever** — `vmag` is not used by the renderer at all.

Doing step 3 per star per frame is a march per star: 120 k stars × 20 steps = 2.4 M trilinear
fetches per eye. Not affordable.

`_ExtCube` does it for free. The bake already integrates `tau` along every direction; it records
the transmission at 100 / 250 / 600 / 1250 pc into RGBA. The star vertex shader then costs **one
cube fetch and six ALU**:

```hlsl
// in CosmoSynth/Point, _Space == 0 (layer mode), after the direction is known:
half4 T = SAMPLE_TEXTURECUBE_LOD(_ExtCube, sampler_ExtCube, dirIcrfFromEye, 0);
// du is the star's distance from the eye in parsecs (already computed for the M -> m step)
float g = saturate(log(du / 100.0) / log(1250.0 / 100.0)) * 3.0;   // 0..3 across the 4 slices
int   s = (int)g;
half  Tstar = lerp(T[min(s,3)], T[min(s+1,3)], (half)frac(g));
// beyond the measured cube, add the analytic slab leg (plane-parallel closed form, ~6 ALU)
magnitude += CS_FarSlabAv(dirGal, du);        // 0 inside 1250 pc
brightness *= Tstar;
```

Log interpolation between four slices is right because extinction accumulates roughly
exponentially with the path through a plane-parallel layer; the slices are placed at 100 / 250 /
600 / 1250 pc, a factor 2.5 apart, so the interpolation error is bounded by the curvature of
`tau(s)` over a factor 2.5 — small for a smooth medium, and worst where `tau` is smallest.

**The identity that proves there is no double count.** With the viewer at the Sun,

```
ExtCube(dir_to_star, d_star)  ==  10^(-0.4 * av_star)      for every star
```

That is a pure-Core assertion, no GPU, no headset. It fails loudly if `av` is baked from a
different cube than the runtime samples, if the `toAv` factor differs between the importer and the
material, if the galactic rotation differs by a sign, or if anybody re-dims `vmag`. **It is the
single most valuable test in this document** and it belongs in `DustCubeTests`, run by both the
`stars` and `milkyway` lanes.

Second consequence, and it resolves a question the compose order raises: the dust volume draws at
queue 3010 with `ZWrite Off`, and the stars draw after it at 3050. A star behind the Coalsack is
therefore drawn *on top of* the cloud that should hide it — **and that is correct**, because the
star has already been dimmed by `Tstar`. The volume never occludes point populations; the
extinction does. A depth-sorted answer would be both more expensive and wrong (it would dim by
coverage, not by column).

### 3.5 The LOD chain, end to end

| log10 U | Rungs | What carries the Galaxy | Draws |
|---|---|---|---|
| +3.0 … −13.7 | `sky` … `oort` | bake (band, at rest: free) + 120 k stars, dimmed by `_ExtCube` | 1 + ≤64 |
| −13.7 … −16.6 | `nearest` … `clouds` | bake + **volume enclosing**, 30 log steps, 17 inside measured dust | 2 + ≤64 |
| −16.6 … −18.2 | `clouds` … `milkyway` | volume still enclosing; the bake fades as the box stops being "far" | 2 + ≤64 |
| −18.2 … −19.4 | `milkyway` … `halo` | viewer leaves the box: **same volume, now Rigid**, coverage ~0.15, N = 64 | 1 |
| −19.4 … −21.8 | `halo` … `laniakea` | the volume shrinks below ~3 px of the room; hand to `galaxiesLocal`'s own Milky Way row — which C.10 flags `IsHome` and never draws as a point (**V**) — so the handover is to the **oriented disc impostor** of §5, built from `GalaxyModel` rather than a catalogue row | 0 (the Galaxy becomes one quad in the galaxy mesh) |
| below −21.8 | `cosmicweb`, `cmb` | one quad among 44 k | 0 |

Every handover in that chain is **the same `J` and the same `ρ`** evaluated by a different
renderer, so there is a headless check for each: render one frame on each side of the crossing
and compare. §10.

---

## 4. (c) Nebulae — measured position, procedural appearance

CONTRACT §5.4 gives `nebulae` three close-ups (Orion, Crab, Local Bubble), window −17.5 … −13.5,
gated on `NearestNebulaBoxRadii < 3`, positions from `landmarks.json` where every entry carries a
`reference` or `build_authored.py` refuses it (C.9, **V**).

### 4.1 They are a field, not a volume

Making each close-up its own `MayEnclose` volume is what creates the arbitration problem in the
first place (§1) — and it creates a worse one: when a nebula box takes over, the dust box stops
drawing and the rest of the sky loses its measured silhouette at the exact moment the viewer is
deepest inside the Galaxy.

Instead the nebula is **a term added to `CS_DustDensity` and `CS_GalaxyEmissivity`** inside the
one volume march, exactly as `flare` "modulates `stars` and draws nothing of its own" (§5.4, **V**).

```hlsl
// up to 4 active close-ups; _NebCentre[i].xyz galactic pc, .w radius pc;
// _NebParam[i]: x density amplitude (A_V/pc), y emission amplitude, z noise scale pc, w seed
for (int q = 0; q < _NebCount; q++)
{
    float3 r  = (pGal - _NebCentre[q].xyz) / _NebCentre[q].w;
    float  rr = dot(r, r);
    if (rr < 1.0)
    {
        float shell = (1.0 - rr) * (1.0 - rr);                 // soft-edged, zero at the boundary
        float n = CS_Fbm3(pGal / _NebParam[q].z + _NebParam[q].w);   // 3 octaves of value noise
        rho += _NebParam[q].x * shell * n;
        emi += _NebParam[q].y * shell * n * n;                 // emission is clumpier than absorption
    }
}
```

`CS_Fbm3` is analytic hash noise, 3 octaves, ~30 ALU — **no texture**. A tiling 32³ R8 noise
texture (32 KB) is the alternative if the ALU shows up in the frame time; measure before adding
it. Cost: the AABB test is 6 ALU per nebula per step when outside all of them, which is the normal
case; the whole block costs ~24 ALU per step at `_NebCount = 4` outside, ~120 inside one.

**Draws: 0. Vertices: 0. Step-coverages: 0 extra.** CONTRACT §7.3's `nebulae` row — "at most 3
draws, 72 verts, the same B = 30, never simultaneously with `dust`" — is deleted. So is the
overlap problem it describes.

### 4.2 Keeping them honest

The risk is precise and this project has already shipped it once: the Milky Way Atlas drew 542 of
911 *default* ellipticities as if measured (CONTRACT §5.4, B4, **V**). A procedural M42 next to a
measured dust cube will read as measurement unless the difference is **visible without reading a
card**.

Four mechanisms, all of which the framework already has:

1. **The provenance palette does the talking.** The nebula's colour ramp is keyed to the
   Procedural colour (amber) and the measured dust's ramp to the Measured colour (green-neutral).
   They are adjacent in the same frame, so the eye reads the difference directly. The `LayerDef`
   `Weakest` glyph is `*`.
2. **The boundary is drawn.** Each close-up gets the `dustExtent` treatment: a faint ribbon
   sphere at `_NebCentre.w`, in the Procedural colour, labelled `MODEL`. "This is a model in a
   box" is a shape, not a sentence.
3. **The measured thing is still there underneath.** Orion at 414 ± 7 pc (Menten+2007, cited in
   C.9, **V**) is *inside* the Edenhofer cube, which really does reconstruct it. The procedural
   term adds the sub-10-pc structure the reconstruction cannot resolve, on top of the measured
   large-scale cloud, and both are in the same integral. The honest caption is exact: *position,
   distance and extent measured; structure below 10 pc invented.*
4. **Imagery stays in the card, never in the scene.** `imagery-nebula-nasa/` has three cleared
   NASA plates (**V**, staged). A billboarded photograph breaks the moment you fly past it, and it
   is the strongest possible false claim of measurement. The plate belongs in the info panel under
   "what this looks like from Earth", with its credit string — which is a licence condition for
   the CC BY sources, not a courtesy (amendment §5, **V**).

**Local Bubble.** It is the one close-up that is *not* procedural: Zucker+2022 measured it
(≈1,000 ly across, peanut-shaped; C.9 records this as **V**), and the Leike cube resolves its wall
at 4 pc. Render it as the measured cube with the ramp pushed to show low densities — an exposure
choice, not a model — and say so. It is also the answer to "what does the viewer see at rung 11
(`clouds`, 2 kpc to 1.6 m)": they are standing inside a measured cavity.

---

## 5. (d) Galaxies as oriented impostors

The geometry is already settled and VERIFIED in CONTRACT C.10: `SkyBasis(ra, dec)`,
`MajorAxis(pa)`, `InclinationDeg(q, q0=0.2)` with `cos²i = (q²−q0²)/(1−q0²)` (Hubble 1926), and
`DiscNormal(rhat, i, khat, sign)` with the sign Procedural from a hash of the name — because the
near/far side of an inclined disc is **not measurable from an image**, and a hash is honest where
a fixed choice would look like knowledge. Probe values are pinned: q = 0.28 → 78.46°, q = 0.33 →
74.46°, q = 0.59 → 55.49° (±0.01°, **V**).

What remains is the rendering.

### 5.1 The quad, and the trap in building it

```hlsl
// A galaxy-disc port of CosmoSynth/Point (section 4.5 already calls for exactly this).
// Vertex: centre float3 (chunk-relative ICRF pc), u SNorm8x4, c SNorm8x4, R half,
//         absMag half, ttype/flags UNorm8x4, corner in the w of the SNorm -> 24 B/vertex.
float3 rel   = v.centre - _EyeInChunk.xyz;
float  du    = max(CS_Length(rel), 1e-30);
float3 dirWS = normalize(CS_IcrfDirToWorld(rel / du));

// THE TRAP: compress the CENTRE, then build the quad in ANGULAR units about it.
// Compressing the four corners independently warps the disc, because f is radial and
// logarithmic: a disc seen edge-on would bow, and one seen face-on would shrink at the
// rim. Direction is exact for every vertex either way; only the depth would differ, and
// the quad has no depth to spare.
float3 centreWS = _CS_CentreEyeWS.xyz + dirWS * CS_CompressDistance(du * _Units.x);
float  thetaR   = v.R / du;                                  // angular half-size, radians

float3 uWS = normalize(CS_IcrfDirToWorld(v.u.xyz));
float3 cWS = normalize(CS_IcrfDirToWorld(v.c.xyz));
float3 vWS = normalize(cross(cWS, uWS));
// Edge-on guard: a disc has thickness. Never let the minor axis collapse below q0.
float  mu  = max(abs(dot(cWS, dirWS)), 0.20);                // q0 = 0.20, the same q0 as Hubble 1926
float4 cs  = TransformWorldToHClip(centreWS);
float2 e   = float2(dot(uWS, right), dot(uWS, up));          // project the axes into eye space
cs.xy += CS_AngularToClip(thetaR * (corner.x * eU + corner.y * mu * eV), cs.w);
```

The edge-on guard is not cosmetic. Without it, `|dot(c, viewDir)| -> 0` degenerates the quad to a
line and every edge-on spiral in the catalogue vanishes; clamping at `q0 = 0.20` renders the same
intrinsic thickness the inclination formula already assumes, so the two agree by construction.

### 5.2 The profile, analytically

CONTRACT §7.3 budgets 0.1 MB for a "type atlas" (5 × 128² templates by T-type). **Do not ship it.**
Two closed forms cost fewer bytes, never blur, and are correct at every zoom:

```hlsl
float r = length(i.uv);                                   // elliptical radius, 1 at the rim
float I = (i.ttype <= 0.0)
        ? exp(-7.669 * (pow(max(r,1e-3), 0.25) - 1.0))    // de Vaucouleurs n=4: ellipticals
        : exp(-r * 3.0);                                  // exponential n=1: discs
```

`7.669` is the standard `b_4` of the Sérsic n = 4 profile (**I**, textbook; trivially confirmable).
The `3.0` puts ~3 scale lengths inside the catalogue radius, matching the isophotal definitions
(Holmberg for UNGC/LGA, half-light for LVDB — C.10 carries `sizeKind` per row, **V**, so the
constant is picked from the column, not assumed).

Colour from `absMag` and `ttype`: bluer with later type and lower luminosity. That is **Procedural
over Measured** and gets the Procedural glyph for the colour field alone, while position, size, PA
and ellipticity keep theirs.

### 5.3 Heroes with real imagery

`heroes-derived/` has 31 giants, and `imagery-2mass-lga/` has 14 J+H+Ks colour mosaics of them
(**V**, staged, public domain, 24.8 MB). Their value is stated in the staging notes and is
technical, not aesthetic: **`sup_ba` and `sup_pa` — the very columns that orient the billboard —
were measured on these mosaics**, so the image and the geometry are the same data and need no
registration.

- Atlas: 14 tiles at 256² in one 1024² sheet, ASTC 6×6 ≈ **0.46 MB**, plus the 0 MB the analytic
  profile costs for everything else.
- Cross-fade analytic → imagery over 30–60 px of major axis. Below 30 px the imagery is under its
  own texel size and the analytic profile is sharper; above 60 px the imagery has structure the
  profile cannot invent.
- **The credit string travels with the image into the object's info panel**, not only a credits
  scene — a licence condition for the CC BY sources and a stated request for the 2MASS ones
  (amendment §5, **V**).
- Caveat the staging already records: these are near-infrared, warm and low-contrast, showing the
  old stellar population. They will not look like the familiar optical M83. That is a *feature*
  for an app whose thesis is "measured, and labelled as such", and the info card should say which
  bands are mapped to which channels.

### 5.4 The sub-pixel fallback and the cross-fade that must not double the flux

Below ~3 px the disc is a point. CONTRACT §5.4 already says "oriented discs above 3 px". The
cross-fade is where the bug lives:

```
w = smoothstep(3.0, 6.0, px)
disc  brightness *= w
point brightness *= (1 - w)
```

Both representations carry the galaxy's **whole** flux. If each is merely faded in and out the
object is up to 2× too bright in the middle of the transition — a visible pulse as you zoom,
exactly the kind of artefact that reads as a bug and is dismissed as "the bloom". Complementary
weights conserve flux by construction: `w + (1−w) = 1`.

The same rule applies to the Milky Way's own handover at −21.8 (§3.5) and to every other pair of
representations in this document. It is worth stating once as a framework rule: **two
representations of one object have complementary weights, never independent fades.**

### 5.5 What must stay unknown

C.10's hardest-won rule, kept: `ell`, `pa`, `rhalf` and `M_V` are taken as given and **a blank
stays blank**, flagged, drawn round and faint. `LocalVolumeTests` already asserts it ("No LVDB
blank became a number", **V**). The impostor path must honour it in the *mesh builder*, not only
in the inspector: a row with `EllUnknown` gets `q = 1` and a camera-facing circle, and a row with
`PaUnknown` but a known `q` gets... also a circle, because an axis ratio without an angle cannot
be drawn as an ellipse without inventing the angle. That is one `if`, and it is the whole
difference between this app and the one whose worst defect it is reversing.

---

## 6. (e) The CMB

### 6.1 Ship 512, not 1024 — the data has no more

The source changed under the commercial bar: **Planck is dead, WMAP 9-year ILC replaces it**
(amendment row 15, E42/E43, **V**), and the bake is already done and checked on this box.
`bake_checks.json`, verbatim highlights (**V**):

```
nside 512 · source_rms 71.06 uK · min -452.46 · max +442.23
coldspot_5deg_mean -105.58 uK (10,801 texels)   check2 pass
gc_5deg_rms 75.22 / ngp_5deg_rms 69.63 -> ratio 1.08   check1 pass (plane peak below |b| 2 deg)
cube_vs_equirect_corr 0.99649   scrambled_face_control_corr -0.00991   check4 pass
kq85_kept_fraction 0.7481
```

The WMAP ILC is **smoothed to a 1° FWHM beam**. The contract chose a 6 × 1024² cube against
Planck's 5 arcmin beam (C.12, **V**), with the explicit reasoning "the data hold no detail below
the beam anyway". Applying that same reasoning to the new source:

| Face | Texel at face centre / edge midpoint | Samples across a 1° beam | Raw R8 | GPU + mips | Deflated (0.73 ratio, **V** from the equirect) |
|---|---|---|---|---|---|
| 1024 (staged, contract) | 6.7 / 3.4 arcmin | 9 – 18 | 6.29 MB | 8.4 MB | ~4.6 MB |
| **512 (ship)** | **13.4 / 6.7 arcmin** | **4.5 – 9** | **1.57 MB** | **2.10 MB** | **~1.15 MB** |
| 256 (knob) | 26.8 / 13.4 arcmin | 2.2 – 4.5 | 0.39 MB | 0.52 MB | ~0.29 MB |

**Ship 512.** It is at least 4.5 samples across the beam everywhere, which is 2× Nyquist at the
worst point on the cube. 1024 stores nothing the map contains and costs **+4.7 MB of APK and
+6.3 MB of GPU** for it. 256 is right at Nyquist at face centres and would be the knob if memory
ever got tight. The `--face` knob already exists in `build_cmb.py`'s design (C.12, **V**), so this
is a parameter change, not a code change, and the staged 1024 bake stays as the archive.

The mask goes to 6 × 64² R8 = **24 KB**. Note the mask is now **KQ85**, an analysis mask that
keeps 74.81 % of the sky (**V**) — not Planck's 2.1 % inpainting mask. A quarter of the sky is
foreground-suspect, which is an order of magnitude more than the contract's provenance design
assumed.

Quantisation: `±500 µK` in 8 bits is 3.92 µK per step against an rms of 71.06 µK — **18 steps per
sigma**, and `cube_min/max` of −452 / +442 µK mean **nothing clips** (**V**). The contract's
encoding is correct as written.

### 6.2 Colour, and showing a quarter of the sky as degraded

- **LUT**, 256 × 1 sRGB, generated by `build_cmb.py`, ours, no licence question (the Planck
  colour map is ESA's). Diverging, perceptually ordered cold → hot, anchored at
  `−1 (0.00,0.10,0.42)`, `−0.5 (0.10,0.45,0.75)`, `0 (0.93,0.91,0.86)`, `+0.5 (0.95,0.55,0.18)`,
  `+1 (0.55,0.05,0.02)`, smoothstep between anchors. It is an sRGB texture, so the hardware
  decodes it — **do not also call `CS_DisplayToLinear` on the result** (§7.4's exact trap).
- `_Contrast = 2` maps ±250 µK to the full LUT = **±3.5σ** at the WMAP rms. The contract's
  default is right for the new data; state it in those units in the info card so the picture is
  falsifiable.
- **The mask does not shout.** With 25.2 % of the sky affected, a hatch or a hard colour change
  would dominate the image and misrepresent the map (the ILC is still a CMB estimate inside
  KQ85 — it is the *power spectrum* that needs the mask). Design: multiply saturation by 0.65
  inside the mask, leave luminance alone, and put the numbers in the card:
  *"74.8 % of this sky is a measured CMB estimate; over the remaining 25.2 %, Galactic foreground
  residual is significant. WMAP 9-year ILC, 1° beam."* The mask cube is sampled in the same
  fragment, one extra fetch.
- Provenance per texel, per C.12's rule, driven by `mask` — and the app **must never say
  "Planck"** (amendment, **V**).

### 6.3 Fading in without ever being a wall

CONTRACT §2.6 already fixes the hard part (**V**): object form while the eye is inside 0.95 R,
sky form once it passes 1.05 R, a 0.3 s fade of the outgoing form, **no frame with both forms at
`_Fade > 0`**, and the G1c `cmb-fade` assertion. A true cross-fade is impossible because the sky
form has exactly zero disparity while the object form's surface is 0–6 cm from the eye.

What is left undesigned is the approach, and it is where a "wall" would come from: the last thing
you see before the switch is the inside of a 1.2 m ball at 6 cm. The fix is not to hide the switch
but to **make the two forms agree at the switch point**, so there is nothing to hide:

```
alpha_sky = smoothstep(30 deg, 75 deg, angularRadius(CMB object))
```

As the object's angular radius grows past 30°, ramp out everything that makes it read as a
surface — the rim/Fresnel term, any specular, the visible edge against the outside — and ramp
`_Immersion` in. By 75° the object form is already shading like the sky form, and at the 0.95/1.05
switch the two are pixel-for-pixel close. **That makes the switch measurable, which makes it
gateable**: extend the existing `cmb-fade` assertion to capture the frame before and the frame
after and require a mean absolute difference under a stated threshold. A discontinuity you can
measure headlessly is a discontinuity that will not ship.

Sky-form window and weight (§5.4, **V**: object −24.7…−23.3, sky −23.3…−21.6): ramp
`LayerState.Weight` linearly in `log10 U` over 0.3 decades at each edge, times `_Immersion` from
`RoomVisibility`. With `CmbBackdrop` on, the sky form draws at any scale — **and that is
physically honest**: the CMB is the sky from everywhere, including from Earth's surface, and the
only reason it is not the default is that it would compete with everything else for attention.

---

## 7. (f) Zodiacal light and airglow — both, for almost nothing

Neither appears anywhere in `docs/` (**V**, grepped). Both are real, measured, and they are the
reason the real night sky is not black. They ride in the Sky fragment as analytic terms: **0 extra
draws, 0 extra passes, ~20 ALU, 0.4 KB of table.**

### 7.1 Zodiacal light

Sunlight scattered by interplanetary dust. It is the second-brightest diffuse component of the
night sky, it is a band along the **ecliptic** rather than the galactic plane — so it crosses the
Milky Way band at a large angle, which is visually informative — and it brightens steeply toward
the Sun.

- **Model:** a 13 × 7 table of `I(elongation ε, ecliptic latitude β)` in S10(V), bilinear, from
  Leinert et al. 1998 Table 17. 91 floats = **364 B**. Transcribed with its citation; the entry
  is Measured, the interpolation Inferred.
- **Gegenschein:** a ~1.3× bump within 10° of the anti-solar point, already in the same table.
  It costs nothing and it is a thing almost no space app has.
- **Where it draws:** wherever the viewer is inside the zodiacal cloud, i.e. the inner Solar
  System. Fade out beyond ~5 AU by the same scale factor as the dust column. Rungs 0–7.
- **Honesty:** it depends on the Sun's direction, so it *moves with simulated time*, which the app
  already has. That makes it verifiable rather than decorative.

**Verdict: yes.** The cost is a table lookup; the payoff is that rung 0 ("Sky from Earth") and the
inner-system rungs stop being black.

### 7.2 Airglow

The 90–100 km OH and O(¹S) layer. It is what makes a dark site grey-green rather than black, and
its horizon brightening is one of the most recognisable features of a real dark sky.

- **Model:** van Rhijn, exact for a thin shell:
  `I(z) = I_zenith / sqrt(1 − (R/(R+h))² sin²z)` with `R = 6371 km`, `h = 90 km`.
  At the horizon that is `1/sqrt(1 − (6371/6461)²) = 1/sqrt(0.027686)` = **6.01×** the zenith
  value (**V**, exact arithmetic from the two radii).
- `I_zenith ≈ 100–150 S10(V)` at solar minimum (**I**, from memory of the same Leinert reference);
  expose it as one slider, because it genuinely varies by 2× with solar activity — and the app
  already ships the monthly sunspot number (`activity`, C.5), so the slider can have a *default
  driven by data*.
- **Where it draws:** rung 0 only, gated on `ViewerOnSite` — the same condition `horizonGrid`
  already uses (§5.4, **V**). `_Gains.z` is 0 everywhere else.
- **Not in scope here:** the green limb arc seen from orbit. That is a property of Earth's
  atmosphere shell and belongs to the atmosphere-as-scattering topic, not the sky.

**Verdict: yes, at rung 0.** Six ALU, one constant, gated off everywhere else. A black sky at
"Sky from Earth" is simply wrong, and this is the cheapest correction in the document.

---

## 8. The arithmetic and the compose order

### 8.1 Compose order

Queues are CONTRACT §4.5's, unchanged. What this design changes is that four of the rows collapse
into one.

| Queue | Pass | Blend | Writes alpha? |
|---|---|---|---|
| — (offscreen, pre-frame) | **Sky bake**, `_Mode = 1`, one cube face, MRT ×2 | `One Zero` into a 128² RT | into a cube, never the eye buffer |
| 1000 | **Sky backdrop**: CMB + band + zodiacal + airglow, one fetch each | `One Zero` | yes, `= _Immersion` — the *only* pass that writes coverage on purpose |
| 1010 | 146 distance-unknown stars (sky mode); `horizonGrid` | `One One, Zero One` | no |
| 2000 | bodies (opaque) | opaque | yes, 1 |
| 3000 | rings | premultiplied over | yes |
| **3010** | **the one Galaxy volume**: near dust + far dust + analytic dust + emissivity + nebula fields | `One OneMinusSrcAlpha, One OneMinusSrcAlpha` | yes, = opacity. Correct: dust absorbs |
| 3050 | stars, galaxies, all point populations — **each already dimmed by `_ExtCube`** | `One One, Zero One` | no |
| 3060 | ribbons: orbits, `dustExtent`, nebula boundaries, arms, grids | `One One, Zero One` | no |
| 3100 | atmospheres | premultiplied over | yes |
| 3200+ | UI, text | premultiplied over | yes |

The one thing that would break passthrough — an additive pass writing alpha — cannot happen in any
row above, and `stereo_gate.sh` greps for it statically (**V**).

### 8.2 Budget, per regime, against §7.3's ceilings

One eye = 2064 × 2208 = 4,557,312 px; both = 9,114,624 px (**V**, §2.0). κ = ms per step-coverage;
Fractarium's device-measured **0.338 ms** is an upper bound (it is a Mandelbulb distance estimate,
quest-rendering-techniques §5.2, **V**); κ for a 3D-texture step is inferred at **0.03–0.10**.

**Regime A — inside the Galaxy** (rungs 9–11, log10 U −14.5 … −16.6). The emptiest frame today.

| Layer | Draws | Verts | Frag (eye-screens) | Step-cov | Texture | ms at κ=0.06 | ms at κ=0.338 |
|---|---|---|---|---|---|---|---|
| Sky bake (moving; 0 at rest) | 1 | 4 | — | 0.043 | — | 0.003 | 0.015 |
| Sky backdrop | 1 | 2,048 | 1.0 | 0 | 1.57 MB (band+ext ×2) | 0.45 | 0.45 |
| **Galaxy volume** (dust + emission + nebulae) | **1** | **24** | ≤1.0 | **30** | 7.68 MB | 1.8 | 10.1 |
| stars 119,669, chunked | ≤64 (~20 in view) | ≤480 k | 0.5 | 0 | 0 | 1.2 | 1.2 |
| cloudMarkers, globulars, cepheids, masers | ≤6 | ≤60 k | 0.1 | 0 | 0 | 0.2 | 0.2 |
| `dustExtent` + nebula boundaries | ≤3 | ≤4 k | 0.02 | 0 | 0 | 0.05 | 0.05 |
| bodies + UI + ribbons (contract rows) | ≤82 | ≤500 k | 0.9 | 3 | ≤68 MB | 4.6 | 4.6 |
| **total** | **≤158** | **≤1.05 M** | **≤3.5** | **33.0** | **≤77 MB** | **8.3** | **16.6** |

Draws come out at 158 against the 150 ceiling because the star chunk count is a worst case that
never co-occurs with 60 bodies; cone culling takes the ~20-in-view figure and the real total is
~115. **Fragments at 3.5 exceed the 3.0 ceiling** and that is real: the backdrop is a full
eye-screen the contract's budget did not have. Two ways out, in order: (i) the Sky backdrop is one
texture fetch per pixel and is the cheapest full-screen pass possible — reclassify it as opaque,
since with `_Immersion = 1` it *is* opaque and `Blend One Zero` needs no read-modify-write; (ii)
if it still shows, the sphere mesh can be a screen-space triangle. Take (i); it is a book-keeping
correction, not a cost.

**The one number that decides this frame is κ.** At the inferred 0.06 the frame is 8.3 ms against
11.0; at Fractarium's 0.338 upper bound it is 16.6 ms and the app misses 72 Hz. CONTRACT §13 item
14 already names this as the thing a device probe must settle before B = 30 is trusted. **Build
the probe first** (§11 step 1) — everything else in this document is sized off it.

**Regime B — the Galaxy on the table** (rung 12, 30 kpc/m).

The box is ~0.7 m across at ~1 m, a half-angle of 0.35 rad, solid angle `2π(1−cos 0.35)` =
0.385 sr against the Quest's 2.62 sr both-eye FOV (**V** arithmetic, FOV from
quest-rendering-techniques) → **coverage 0.147**. The governor gives
`N = clamp(30/0.147, 16, 64) = 64`, so cost = `64 × 0.147` = **9.4 step-cov** — 3.2 ms at κ=0.338,
0.56 ms at 0.06. Plus `galaxiesLocal` at ≤16 draws / ≤340 k verts (contract row) and the arms,
masers and globulars at ≤6 / ≤60 k. Comfortably inside every ceiling.

**Regime C — BEYOND** (rungs 16–18). The Galaxy volume is off below −21.8; `galaxies2mrs` is
43,500 quads = **174,000 verts** and ~16 draws; the CMB is 1 draw, 2 k verts, 1.0 eye-screens,
**2.10 MB** (was 8.4). The lightest of the three.

### 8.3 Texture bytes, totalled

| Asset | APK (deflated) | GPU |
|---|---|---|
| `dust_far.csdu` 128³ | 0.73 MB | 2.40 MB (mipped) |
| `dust_near.csdu` 185×185×135 | 3.37 MB | 5.28 MB (mipped) |
| `cmb_sky.cscb` 6×512² R8 + 6×64² mask | 1.17 MB | 2.12 MB |
| hero galaxy atlas 1024² ASTC 6×6 | 0.46 MB | 0.46 MB |
| band + ext cubes (runtime-generated) | 0 | 1.57 MB |
| CMB LUT, dust ramp, zodiacal table | ~2 KB | ~2 KB |
| **total** | **5.73 MB** | **11.83 MB** |

Against a 46 MB APK today with a 150 MB ceiling, and a 100 MB texture budget. Shipping 512 instead
of 1024 for the CMB pays for the near dust cube outright.

---

## 9. Failure modes this project has already paid for, and where each bites here

| # | The scar | Where it bites in this design | The guard |
|---|---|---|---|
| 1 | **Additive pass writes alpha → opaque hole in passthrough** (measured 2026-07-31) | every point and ribbon row in §8.1 | `Blend One One, Zero One`, literal, so `stereo_gate.sh:77` can grep it. **No property-driven blend** (§2.6) |
| 2 | **sRGB vertex colours in Linear** (BoltGraphXR, **V**) | `_BandTint`, the dust ramp, the CMB LUT, the galaxy colour-by-type | decode exactly once: `CS_DisplayToLinear` for vertex colours and tuned constants; the LUT and ramp are *sRGB textures*, so the hardware decodes them and the shader must **not** decode again |
| 3 | **`GetCameraPositionWS()` is the wrong eye** (R1) | the volume's ray origin, the sky anchor | `CS_EyePositionWS()`; gated by grep |
| 4 | **Per-pixel jitter in a march = binocular rivalry** (R7) | the volume march | `CS_JitterDir(dirOS)`, ray space. The *bake* has no per-pixel term at all, so the band is rivalry-proof by construction |
| 5 | **Static batching bakes object space into world space** (R12) | `Volume` (march entry point), `Sky` (vertices *are* ICRF directions) | `"DisableBatching"="True"` in both, already in A.6/A.7 |
| 6 | **A fragment reading `unity_WorldToObject` under instancing reads matrix 0** (R5) | the volume's `eyeOS` | computed in **vert**, passed as a varying — already in A.7 |
| 7 | **`v == 0` is "no data", not vacuum** (staged `NOTES.md` pitfall 1, **V**) | the far dust cube's map sphere | `|p| < 1248` **and** `v > 0`, else fall through to analytic. §3.3 |
| 8 | **Blanks drawn as measurements** (542/911 default ellipticities, B4) | galaxy `ell`, `pa`, `size`; nebula shape; band emissivity | blank → round and faint (§5.5); Procedural palette + drawn boundary (§4.2); `LayerDef.Weakest` asserted by `LayerTableTests` |
| 9 | **An opaque body in front of an enclosing volume kills the whole ray** (R6) | the one Galaxy volume, now active from +3.0 down | the window still puts every catalogue body below the 2.5 px floor at those rungs, so none is geometry — but the volume is now active at **many more rungs than the old `dust` window**, so `ScaleRenderer`'s per-frame `volumeOcclusion` check matters more, not less. The fixed views must show 0 |
| 10 | **Two representations both carrying full flux** | disc ↔ point (§5.4), volume ↔ impostor (§3.5), object ↔ sky CMB (§6.3) | complementary weights, `w + (1−w) = 1`, stated as a framework rule |
| 11 | **An extra render pass is the family of the 1.7.0 grid bug** | the bake's render-target switch | 128² = one tile, 64 KB; measured on device before it is trusted; a CPU 32² fallback exists behind the same interface (§2.4) |
| 12 | **The verifying environment differs from the running one** (every gate line in the contract) | everything in §10 | say so in the notes, every time |
| 13 | **`StandardFrames` has all three galactic signs flipped** | every field here, evaluated in galactic pc | fix first, with its test; `_CS_IcrfToGal` is published from it |

---

## 10. What this box can prove, and what only a headset can

**Provable headlessly on triton** (no GPU, or llvmpipe, mono, flat):

| Check | Method | Status |
|---|---|---|
| Shipped cube sizes and deflate ratios | `dust_probe.py` | **done, §3.1** |
| `A_V = 2.88 × E(ZGR23)`, ±15 % | `dust_probe2.py`, 4,000 sightlines, two independent routes | **done, §3.2** |
| Dust axis and unit sanity | the five integrated columns of §3.2 as importer assertions | ready |
| CMB frame, Cold Spot, plane rms, cube↔equirect | already in `bake_checks.json` | **done, V** |
| `ExtCube(dir, d)` from the Sun == `10^(-0.4 av)` for every star | pure Core, `DustCubeTests` | **the double-count test; write it first** |
| `j0` calibration and the plane validation | two CPU integrals over `J` plus a sum over `stars.csstars` | ready once §2.2's table numbers are pinned |
| The 25 pc rebake threshold | bake at `x` and at `x + 25 pc·û`, max per-texel ΔL over the cube; require under the JND (~1 % of local value) | ready |
| mwBand ↔ mwDisc handover continuity | render the fixed view on both sides of −18.2 and compare | ready |
| disc ↔ point flux conservation | render a galaxy at 2, 4, 8 px and integrate total pixel flux; the 4 px value must lie between | ready |
| CMB object ↔ sky switch continuity | extend the G1c `cmb-fade` assertion to a frame-pair mean-abs-difference | needs the assertion extended |
| Draws, verts, step counts per fixed view | the play-mode `render:` line + the `budget`, `volumes`, `draws` assertions | exists |
| Inclination formula probes | `GalaxyOrientationTests`, already specified in C.10 | exists |

**Only a headset can settle:**

| # | Item | The measurement |
|---|---|---|
| 1 | **κ for a 3D-texture march step.** The whole budget hangs on it (§8.2) | a Fractarium-style `cost_probe`: fix N, vary coverage, read `adb logcat \| grep VrApi`. CONTRACT §13 item 14 |
| 2 | Whether the bake's render-target switch costs a measurable tile flush | frame time with the bake forced on every frame vs. off |
| 3 | Whether a B10G11R11 band cube still bands in a dark sky | look at the Great Rift's edge in Space mode |
| 4 | Whether 6 × 128² is enough, or 256 is needed | look for facets in the far band away from the measured region |
| 5 | Whether the CMB at face 512 reads soft (§13 item 32, restated for WMAP's 1° beam) | a headset look. The honest expectation is **yes, and correctly so** — the beam is 20× the eye pixel |
| 6 | Whether the volume march is per-eye correct (§13 item 7) | stand inside the cloud in `mode:vr`; the two eyes must show *different* marched noise. The flat mono harness passes either way — this is the canonical verification gap |
| 7 | Whether the band + zodiacal + airglow read as "sky" or as grey wash | the seven fixed views, worn |
| 8 | Whether `hz = 200 pc` gives the right band width | compare the rendered latitude profile against a published ISL profile, then look |

**The gap to state in every note**, verbatim from the contract's own rule: a flat mono capture on
llvmpipe cannot prove stereo, passthrough, frame rate or foveation.

---

## 11. Implementation plan

Ordered so that the number everything else depends on arrives first, and so that no lane waits on
another lane's runtime code.

| # | Step | Lane | Blocks | Output |
|---|---|---|---|---|
| 0 | Fix `StandardFrames` galactic signs, with its test vectors (C.5) | `p0-core` | **everything** | green test |
| 1 | **κ probe on device**: a minimal `CosmoSynth/Volume` over a 128³ noise cube at fixed N, coverage swept 0.1→1.0, `VrApi` GPU read | integrator + `milkyway` | the whole §8.2 budget | one number, into `docs/accuracy.md` |
| 2 | `build_dust.py`: decode → zero the no-data → box-average in density → re-encode log8 → write `dust_far.csdu` (128³) and `dust_near.csdu` (185×185×135), with `toAv = 2.88` in meta and the five column assertions of §3.2 | `milkyway` | 5, 6 | 4.10 MB in `SA/`, `DustCubeTests` green |
| 3 | `Core/Deep/GalaxyModel` gains `Emissivity(pGal)` and `DustAnalytic(pGal)`, engine-free, with the §2.2 constants in one table and the `j0` calibration computed from `stars.csstars` | `milkyway` | 4, 6 | the two anchors printed, the plane validation passing within 2× |
| 4 | `Core/Deep/DustCube` gains `ExtinctionAlong` over both cubes + analytic, and the **`ExtCube == av` identity test** | `milkyway` + `stars` | 7 | the double-count test, green |
| 5 | `p0-shaders` extends `CosmoSynth/Volume` (two 3D textures, `CS_DustDensity`, `CS_GalaxyEmissivity`, the nebula field loop, log-spaced steps, mip LOD) and `CosmoSynth/Sky` (`_Mode`, `_BandCube`, MRT, the zodiacal and airglow terms). `CosmoCore.hlsl` gains `_CS_IcrfToGal` | `p0-shaders` | 6, 7 | compiles, G0b green, `stereo_gate` green |
| 6 | `Runtime/Layers/MilkyWay/`: the one volume renderer + the bake driver (motion trigger, one face per frame, ping-pong) + the CPU 32² fallback behind the same interface | `milkyway` | 8 | a capture at rungs 9, 11, 12 |
| 7 | `CosmoSynth/Point` reads `_ExtCube`; `LayerDef` rows updated | `stars` | — | stars redden behind the Coalsack, and the Sun-position identity holds |
| 8 | Nebula fields: three `landmarks.json` entries wired to `_NebCentre`/`_NebParam`, plus the boundary ribbons and the Procedural ramp | `milkyway` | — | a capture flying into Orion |
| 9 | `build_cmb.py --face 512`, LUT generated, KQ85 mask at 6×64², the saturation treatment, the approach ramp of §6.3 and the extended `cmb-fade` assertion | `cmb` | — | 1.17 MB in `SA/`, `cmb-faces` and `cmb-fade` green |
| 10 | Galaxy impostors: the disc port of `Point`, the analytic profiles, the edge-on guard, the complementary cross-fade, the hero atlas and its credit strings | `galaxies` | — | M31 at i = 77°, PA = 38°, and the LVDB blanks still blank |
| 11 | Zodiacal table transcribed from Leinert+1998 with its citation; airglow gated on `ViewerOnSite` | `milkyway` (fields) + `nav` (the gate) | — | rung 0 stops being black |

Steps 2–4 are 1a work (Core and data only, no editor), which is where the `milkyway` lane already
lives. Steps 5–8 are 1b. Steps 9–11 are independent of everything above except step 0.

---

## 12. Open numbers, and the experiment that settles each

| # | Number | Tag | Settled by |
|---|---|---|---|
| 1 | **κ**, ms per step-coverage for a 3D-texture step | **I**, 0.03–0.10 against a 0.338 upper bound | step 1 above. **Everything in §8.2 is sized off it** |
| 2 | `hR`, `hz`, `hRT`, `hzT`, `fT`, `rB`, `fB`, `Rt` | **I**, from memory | read Bland-Hawthorn & Gerhard 2016 and pin each with its page. `hz` additionally tuned against a published ISL latitude profile |
| 3 | `ISL_pole` = 30 S10(V), `ISL_plane(l=90)` = 200 S10(V) | **I**, from memory | read Leinert et al. 1998 A&AS 127, 1, Table 17 |
| 4 | Zodiacal 13×7 table, gegenschein factor, airglow `I_zenith` | **I** | the same table, transcribed with its citation |
| 5 | `n0` for the far dust, set by A_V(Sun→GC) = 30 mag | **I**, well known | one cited value; the model then reproduces it by construction |
| 6 | `A_V/E(ZGR23)` = 2.88 | **measured here**, ±15 % | Zhang, Green & Rix 2023 as *confirmation*. If it disagrees by more than 15 %, re-run `dust_probe2.py` — do not override it |
| 7 | The 25 pc rebake threshold | **I** | the ΔL-over-the-cube check in §10 |
| 8 | Whether 6×128² / 24 steps is enough for the bake | **I** | headset item 4; the cost of 256 is known (0.17 step-cov/frame) |
| 9 | `A_G/A_V` = 0.789–0.86 (enters #6) | **I** | it moves the answer by 8 %, inside #6's band; the Gaia extinction-coefficient tables settle it |
| 10 | Sérsic `b_4 = 7.669`, disc `3.0 / R_iso` | **I**, textbook | one reference each; trivially checkable |

---

## 13. Amendments this design asks the contract for

Each is a change to a binding document, listed so the integrator can accept or refuse it as a unit.
None changes a ruling; all four reduce cost or fix a number that the source change invalidated.

| # | Section | Change | Why |
|---|---|---|---|
| **A1** | §4.5, §5.4, §7.3 | `dust`, `nebulae`, `mwDisc`, `mwBand` collapse to **one** `MayEnclose` volume plus one sky bake. The `nebulae` row's 3 draws / 72 verts / second B = 30 is deleted; `mwBand`'s window becomes the volume's | one draw instead of four, the arbitration becomes structural, and the −17.4 … −16.6 dead band closes. §1 |
| **A2** | C.7 | The dust source and layout: **Edenhofer 128³ @ 20 pc + Leike 185×185×135 @ 4 pc**, two `csdu` files, one magic, `toAv = 2.88` in meta, mips required | the amendment already killed Vergely; these are the measured sizes and the measured unit conversion. §3.1, §3.2 |
| **A3** | C.12, §7.3 | CMB cube face **1024 → 512**; mask to 6×64²; the app's own LUT | the WMAP beam is 1°, not 5′. Saves 4.7 MB APK and 6.3 MB GPU for zero information. §6.1 |
| **A4** | A.6, A.2, §7.3 | `CosmoSynth/Sky` gains `_Mode`, `_BandCube`, MRT and the zodiacal/airglow terms; `CosmoCore.hlsl` gains the `_CS_IcrfToGal` global; §7.3 gains a `sky backdrop` row of 1 draw / 2 k verts / 1.0 eye-screens / 1.57 MB and reclassifies it opaque | no twelfth shader, no second pass, and the fragment ceiling stays honest. §2.5, §8.2 |
| **A5** | §7.3 | Drop the 0.1 MB galaxy "type atlas"; add 0.46 MB for the 14-hero LGA atlas | two analytic profiles beat five blurry templates, and the heroes are the only place imagery earns its bytes. §5.2, §5.3 |

---

*Probes for this document: `~/tmp-astra-sky/dust_probe.py`, `~/tmp-astra-sky/dust_probe2.py` on
triton (read-only over `data-staging/`; they write nothing but stdout). Sources read: CONTRACT
§2.0, §2.3, §2.6, §4.5, §4.6, §5.1, §5.4, §7.1–§7.4, §13, A.2, A.4, A.6, A.7, B.1, C.1, C.7–C.12;
DATA-LICENCE-AMENDMENT rows 10, 12, 13, 15 and E21/E22/E42/E43; `data-staging/{edenhofer2024-3d-dust,
leike2020-3d-dust,wmap9-ilc-lambda,heroes-derived,imagery-2mass-lga,imagery-nebula-nasa}/`;
`docs/research/2026-09-11-{quest-rendering-techniques,milkyway-atlas-port,data-beyond-and-cmb}.md`;
`refs/webxr/nebula-gallery/{README.md,index.html}`; `Fractarium/.../QualityGovernor.cs:253-260`;
`tools/gates/stereo_gate.sh`.*
