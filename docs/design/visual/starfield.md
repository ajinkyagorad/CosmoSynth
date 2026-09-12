# Stars that look like stars

Design pass for the `stars` layer's appearance, 2026-09-12. Topic 2 of the Astra visual brief
(`HANDOVER-ASTRA-VISUAL.md` §5). This document turns that section into numbers and code; it
changes nothing on disk but itself.

Every claim is marked **V** (verified: read on this box, or computed on this box from a shipped
file) or **I** (inferred). Costs that are guesses say so and name the experiment that settles them.

Everything below sits inside `docs/CONTRACT.md`: one camera, URP 17.5, Linear, one distance map
applied per group about the centre eye, `CosmoSynth/Point` as the one point shader, additive
`Blend One One, Zero One`, and §7.3's budget. Where this design differs from the contract's
Appendix A.4 it says so explicitly and gives the arithmetic; those are amendments for the
integrator to accept or reject, not a second render path.

Working numbers were computed by two throwaway scripts run on triton against the shipped
catalogue; they are reproduced inline so nothing here has to be taken on trust. The catalogue
parse self-checks against the contract: it recovers N(V ≤ 6.5) = 8,920 and N(V ≤ 6.0) = 5,070,
which are §1.2 C9's VERIFIED figures (**V**).

---

## 0. The verdict on one page

| Question | Answer | Where |
|---|---|---|
| Flux law | true inverse-square, **no luminosity compression**; one magnitude = 10<sup>0.4</sup> of linear radiance, exactly | §2 |
| The atlas's `L^0.28` | **adapted, not adopted**: keep the physics, move the compression out of `psize` and into the PSF wing, where it is a consequence rather than a constant | §2.2, §3.5 |
| Where brightness lives | linear radiance in `COLOR.rgb`, never in alpha, never in size | §2.3 |
| Point spread | **analytic Moffat β = 2**, `1/(1+u²)²`, evaluated in **core radii**, not quad-relative. No texture at all | §3 |
| Core radius α | **2.2 px** (FWHM 2.83 px) at `PixelsPerRadianCentre` 1140 | §3.2 |
| Minimum quad | **5.0 px** (half 2.5 px), a *sampling* floor, not a brightness floor | §3.4 |
| Maximum quad | **48 px** (half 24 px), enforced by clamping **amplitude**, never the quad | §3.4 |
| Diffraction spikes | **analytic, in the fragment**, gated by amplitude. Zero extra geometry, zero extra draws, zero CPU bookkeeping | §4 |
| Colour | `Teff` → CIE 1931 → **linear sRGB**, max-normalised into `COLOR`, decoded once, then divided by its own luminance so radiance is colour-independent | §5 |
| Mesh | the contract's `PointChunks`: one `MeshRenderer` per chunk, 4 verts per star, `IndexFormat.UInt32`, **24 B/vertex** | §6 |
| Magnitude-banded LOD | **deleted**. The in-shader cut already sheds every fragment; banding geometry buys nothing the arithmetic can find | §6.3 |
| Sky shell vs 3D volume | **one layer**. Their binocular disparity differs by 0.19 px, below anything a headset resolves; only the 146 distance-unknown rows are placed by direction | §7 |
| Bloom | stars **do their own glare**; post-process bloom is neither used nor needed by this layer, which is what makes it survive URP's `_ENABLE_ALPHA_OUTPUT` behaviour | §8 |
| Exposure | one limiting magnitude, `m_cut`, computed from baked per-chunk histograms; it *is* the LOD, the exposure and the `BRIGHTNESS ×N` chip | §2.4, §8.2 |
| Cost | 1 + ≈20 draws, 478 k verts in the mesh, **0.078 eye-screens** of fill from Earth, 0.36 worst case, **0 texture bytes**, **0 APK bytes** | §11 |

Deletions this design pays for: `StarSprite.cs` in full, the halo quad and the two spike quads
(shapes 1–3 of A.4's vertex layout), the 900-unit sky shell's 9,000 star quads, and the
`_SizeDmm` / `_MagWindow` / `_ShapeGain` constant set.

---

## 1. What is wrong today, precisely

The baseline (`docs/reports/baseline-observations.md`, **V**) draws 9,000 stars to magnitude 6.5 as
quads on a 900-unit shell, 39,136 vertices, direction only. `StarLook.cs` (**V**, read in full)
states the law:

```
brightness b = clamp01((7.0 - m) / 5.5)             // linear in MAGNITUDE
quad width  = lerp(3.8, 9.0 dmm, b)                 // size carries brightness
level       = 0.40 + 0.60 b                         // 2.5 : 1 of radiance, total
```

Three things are wrong with that, and only the third has ever been written down.

1. **The radiance range is 2.5 : 1 for a flux range of 158 : 1.** Between magnitude 7 and
   magnitude 1.5 the true flux ratio is 10<sup>0.4×5.5</sup> = 158. The shader delivers
   1.0/0.40 = 2.5. A sky's whole character is its dynamic range; this throws away 98 % of it and
   then tries to buy it back with quad width.
2. **Size is being used as the brightness channel.** `StarLook`'s own remarks say so — *"Size is
   the only channel that reliably survives to the screen here; brightness rides along with it"* —
   and explain why: below about four pixels a profile rarely has a sample near its peak, so the
   measured peak correlated 0.80 with *width* rather than with the colour given. That measurement
   is right (§3.3 reproduces it numerically). The conclusion drawn from it was not: the fix for a
   profile that is too narrow to sample is to make the profile wider **for every star**, not to
   make bright stars wider than faint ones.
3. **A one-pixel feature has no shape** (`CLAUDE.md`, **V**) — already paid for, already fixed by
   the 3.8 dmm floor.

And two defects that are live in the **contract's own** Appendix A.4 shader, found by reading it
against the arithmetic:

4. **Every star renders as a soft square.** A.4's fragment is
   `a = exp2(-4 * dot(uv, uv))` with `uv = corner`, i.e. **quad-relative**. At the middle of a
   quad edge `dot(uv,uv) = 1`, so the profile is still at `2⁻⁴ = 0.0625` of its peak where the
   quad cuts it; at a corner `dot = 2`, so `2⁻⁸ = 0.0039`. With A.4's `level` floor of 0.40, the
   dimmest possible star's quad edge sits at 0.40 × 0.0625 = 0.025 linear = **sRGB code 44/255**,
   and a magnitude-1.5 star's at code 71 (**V**, computed). The iso-contour at any level between
   0.0039 and 0.0625 of peak is therefore *flat where the quad clips it and curved at the
   corners*: a square with bulging corners. This is the 1.7.0 complaint, reintroduced, in the
   binding document. §3.1 fixes it by one line.
5. **The brightness is gamma-encoded along with the hue.** A.4 does
   `CS_DisplayToLinear(tinted * level)`. `SRGBToLinear` is not linear, so
   `decode(c·level) ≈ c^2.2 · level^2.2`: a level of 0.40 arrives as 0.133, and any flux-true law
   put through that line comes out with its exponent multiplied by 2.2. §5.2 fixes it by moving
   one parenthesis.

Neither (4) nor (5) is visible in the pre-migration capture, because the pre-migration tree is
Gamma and Built-in. Both land the moment `lane/foundation-shaders` merges.

---

## 2. Magnitude → flux → radiance

### 2.1 The owner's atlas, read exactly

`refs/webxr/nebula-gallery/index.html` (**V**, lines 279–331 and 335–351 read directly):

```js
absM    = mag - 5*log10(max(d,1)/10)
psize   = pow(pow(10, -0.4*absM), 0.28)      // = L^0.28, stored per star
// model mode, vertex:
s       = psize * uBright * uK / d_view      // uK = 2.1
alpha   = clamp(s, 0, 1) * 0.75
gl_PointSize = clamp(s, 1.2, 20)
// fragment:
a = exp(-4.5*r*r) * vA;  gl_FragColor = vec4(vC*1.5, a)   // THREE.AdditiveBlending
```

The single scalar `s` drives **both** the point size and the alpha. Below `s = 1` the size is
pinned at 1.2 px and alpha carries the flux; above `s = 1` alpha is pinned at 0.75 and size
carries it. `docs/research/2026-09-11-milkyway-atlas-port.md` already records the consequence
(**V**): *"the pixel diameter goes as L^0.28/d, so the integrated brightness goes as L^0.56/d².
That is honest inverse-square in distance but compressed in luminosity."*

It is also the exact recipe for this project's own shipped bug: a 1.2 px floor and brightness in
alpha. The port note says so in one line (**V**): *"Its 1.2 px star floor is exactly the 'square
patches' failure the user already reported."*

### 2.2 Adopt, adapt or replace

**Adopt** the physics: brightness ∝ luminosity / distance², the Sun as an ordinary G star at
M<sub>V</sub> 4.83, colour from the measured colour index, and the explicit distinction between a
model view and an observed-sky view. All four are already in the contract.

**Replace** the mapping. `L^0.28` driving `gl_PointSize` is an empirical compression bolted onto
the wrong quantity. The reason it looks good is not that 0.28 is a special number; it is that a
point source's *apparent* radius at a fixed detection threshold is a power of its flux, with the
exponent set by the slope of the point-spread wing (§3.5). Making that a consequence of the
profile instead of a constant in a CPU bake means:

* flux ratios stay exact everywhere, including between the core and the wing of the same star;
* there is one fewer hand-tuned number, and the one that remains (the wing slope) is checkable;
* the size floor stops being a photometric lie, because size stops being photometry.

### 2.3 The law

In layer mode `TEXCOORD0.x` holds **absolute** magnitude M<sub>V</sub> (de-reddened at bake with
the CSS2 `av` column, contract C.1, **V**). The vertex shader forms the apparent magnitude from
the true distance, exactly as A.4 already does:

```
m = M_V + 5·log10(D_pc) - 5
```

Radiance amplitude, relative to a reference magnitude `m_cut`:

```
A(m) = A_min · 10^(-0.4 (m - m_cut))
     = A_min · exp2(-1.3287712 (m - m_cut))        // 0.4·log2(10) = 1.3287712
A    = min(A · exposure · fade · nearFade, A_max)
```

`exp2` rather than `pow(10, x)` is one instruction on every mobile GPU and is exact here; the
constant is written to seven figures because it is a definition, not a taste.

| Constant | Value | Justification |
|---|---|---|
| `m_cut` | 6.5 at the Solar System, otherwise set by the governor (§2.4) | 6.5 is the naked-eye limit and the catalogue's own shipped anchor: 8,920 rows at V ≤ 6.5 (**V**, matches contract C9) |
| `A_min` | **0.009** linear = 3 ε | three cut-steps above nothing: a magnitude-6.5 star must read as a pinprick, not as nothing and not as a dot. Peaks at **sRGB 23/255** over black (**V**, §14) |
| ε (`_Photometry.w`) | **0.003** linear | the level below which a fragment is not worth rasterising. sRGB code ≈ 6/255 over black. **This is the one constant that can only be set on a headset** (§13) |
| `A_max` | **43.2** | derived, never typed: `A_max = ε·(1 + (halfMax/α)²)²` = 0.003 × (1 + (24/2.2)²)² = 0.003 × 14 400 (**V**). See §3.4 |

Resulting amplitudes (**V**, computed):

| m | 6.5 | 6.0 | 5.0 | 4.0 | 3.0 | 2.0 | 1.0 | 0.0 | −1.44 (Sirius) |
|---|---|---|---|---|---|---|---|---|---|
| A | 0.0090 | 0.0143 | 0.0358 | 0.0900 | 0.2261 | 0.5679 | 1.4264 | 3.5830 | 13.497 |

The range from the faintest drawn star to Sirius is **1 500 : 1**, delivered in full. Compare
2.5 : 1 today.

### 2.4 Exposure is a limiting magnitude

There is exactly one gain in this layer and it is expressed as `m_cut`. Raising `m_cut` by one
magnitude multiplies every star's radiance by 2.512 and admits the next shell of fainter stars.
That single number therefore serves three jobs that were previously three mechanisms:

* **exposure** — the whole sky brightens or dims together, and flux *ratios* between stars stay
  exact, which is the honest definition of an exposure change;
* **LOD** — the count of stars past the cut is a monotone function of `m_cut`;
* **honesty** — `BRIGHTNESS ×N` reports `10^(0.4(m_cut − 6.5))`, so the chip states how far from
  the real night sky the view has been pushed, in the unit the product is about.

`m_cut` is chosen on the CPU to hold a target drawn-star count `N_target` (§11 sets it at 60 000).
Evaluating N(m < x) over 120 k stars every frame is not affordable; evaluating it over **chunks**
is free. `PointChunks.Build` bakes, per chunk, a 16-bin histogram of absolute magnitude
(M<sub>V</sub> −8 … +8 in 1-mag bins, `ushort` counts = 32 B/chunk, ≤ 2 KB for 64 chunks). Then

```
N(m < x) = Σ_chunks N_chunk( M < x − 5·log10(d_chunk/10 pc) )
```

is ≤ 64 × 16 = 1 024 lookups per frame, with an error bounded by the chunk's own radius spread.
One bisection over 8 steps on that histogram gives `m_cut` to 0.1 mag. Cost: negligible; it
replaces the atlas's `skyBoost`/`farAssist` pair of scale-keyed fudges with a measured quantity.

**This is a design decision, not a contract change**: §5.4 already gives the layer
`stars.exposure` and an "automatic brightness assist", and §5.6 already requires the chip. This
says what the assist *is*.

---

## 3. The point-spread function

### 3.1 Analytic, and in the star's own units

**No texture.** Two of the four recorded 1.7.0 root causes were properties of a texture and its
sampler — a profile whose two lobes summed to 1.40 and clamped flat, and a 64 px image sampled on a
4 px quad landing in a 4×4 mip (**V**, `CLAUDE.md`, `docs/feedback.md`). Neither failure mode has
an analytic counterpart. `StarSprite.cs` is deleted; its 32×32 RGBA + mip chain and its
`mipMapBias = -0.75` go with it.

The structural change versus A.4 is one line. A.4 passes `o.uv = corner`, so `uv` runs −1 … +1
over the quad *whatever size the quad is*, and the profile is therefore a decoration stretched
over whatever rectangle the vertex shader happened to produce. Instead:

```hlsl
o.uv = corner * (halfPx / alphaPx);      // uv is in CORE RADII
```

Now `uv` is an absolute angular coordinate in units of the star's own core, the quad is nothing
but a crop, and the crop is placed (§3.4) exactly where the profile has already fallen below the
visibility cut. Three defects die together: the square edge (§1.4), the halo that was only a
scaled copy of the core, and the "spike" that was an isotropic Gaussian squashed into an
anisotropic quad and therefore a lozenge.

### 3.2 The profile

```
P(u) = 1 / (1 + u²)²,      u = r / α          (Moffat, β = 2)
```

* **Peak is exactly 1**, by construction, for every star. Nothing sums; nothing clamps. The
  1.7.0 plateau cannot occur.
* **Two multiplies and a reciprocal.** No `pow`, no `exp`. `exp2(-4u²)` (A.4's Gaussian) is one
  transcendental; this is cheaper *and* has wings.
* **Finite, analytic total flux**: ∫P dA = πα². So the relation between a star's peak and its
  integral is closed-form, which is what lets the headless gate predict both (§14).
* **Power-law wing**, `P → u⁻⁴`. A Gaussian has no wing at all, which is why A.4 needed the quad
  edge to do the wing's job and got a square for it.

β = 2 is chosen because it is the free case: `float t = 1.0 + u2; P = rcp(t*t);`. It gives an
apparent-radius exponent of 1/(2β) = **0.25** (§3.5). The atlas's tuned 0.28 corresponds to
β = 1.786 and costs one `pow`; the visible difference over the full 1 500 : 1 range is
1500<sup>0.03</sup> = **1.24×** on Sirius's radius and nothing on anything else. β stays a
compile-time constant, and 0.25 vs 0.28 is recorded here as a knob rather than an open question.

### 3.3 Behaviour at 1, 2, 4 and 16 px

The failure `StarLook` measured — *"the median 2-pixel star peaked at 11/255 and the single
5-pixel one at 72, correlation between width and peak 0.80"* — is sub-pixel sampling: a profile
narrower than the pixel pitch rarely has a sample near its peak, so its rendered brightness is a
lottery. Reproduced numerically over 400 random sub-pixel offsets (**V**, computed on triton):

| profile | α (px) | FWHM (px) | peak recovered: p05 | p50 | p95 |
|---|---|---|---|---|---|
| Moffat β=2 | 0.60 | 0.77 | **0.258** | 0.496 | 0.906 |
| Moffat β=2 | 1.00 | 1.29 | 0.550 | 0.754 | 0.965 |
| Moffat β=2 | 1.50 | 1.93 | 0.750 | 0.878 | 0.984 |
| **Moffat β=2** | **2.20** | **2.83** | **0.870** | **0.940** | 0.993 |
| Moffat β=2 | 3.00 | 3.86 | 0.927 | 0.967 | 0.996 |
| A.4 Gaussian, quad 4.33 px | — | 2.17 | 0.814 | 0.914 | 0.989 |
| A.4 Gaussian, quad 10.26 px | — | 5.13 | 0.964 | 0.984 | 0.998 |

Read as: at α = 0.6 px a star's rendered peak varies by **3.5×** star to star for no physical
reason — that is the shipped failure. At α = 2.2 px the spread is 0.870 … 0.993, a **14 %** worst
case, and the p50 is within 6 % of the true peak. Going to α = 3.0 buys another 6 % for 86 % more
fill (area goes as the square), which the budget does not need to spend.

**α = 2.2 px is the choice**, i.e. 1.93 dmm at `PixelsPerRadianCentre` 1140. FWHM 2.83 px. Note
this is not the same number as A.4's `_SizeDmm` — that was a *quad* width whose profile happened
to be half of it; 2.2 px is the core radius itself, and the quad is derived from it.

Behaviour across the sizes the brief asks about:

| the star is… | what happens |
|---|---|
| 1 px of core (α ≈ 0.5) | **never produced.** α is a uniform, not a per-star quantity; the only way to reach it is the foveated periphery (§9) |
| 2 px of core | α = 1.0: peak recovery p05 0.55. Below the design floor; recorded here as the boundary |
| 4 px quad | the old floor. At α = 2.2 the *minimum* quad is 5.0 px, so 4 px is below the floor by design |
| 16 px quad | a magnitude-2 star (A = 0.568, half 7.86 px, quad 15.7 px, **V**). Core FWHM still 2.83 px; the extra 13 px is wing, and it is *where the brightness went* |

### 3.4 The floor, the ceiling, and what enforces each

**The quad is derived, never authored.** The half-extent is the radius at which the star's own
profile crosses the cut:

```
A · P(u_max) = ε      ⟹      u_max = sqrt( sqrt(A/ε) − 1 )
halfPx = clamp( u_max · α , 2.5 , 24.0 )
```

| m | A | u_max (core radii) | half (px) | quad (px) | quad area (px²) |
|---|---|---|---|---|---|
| 6.5 | 0.0090 | 0.856 | **2.50** (floor) | 5.00 | 25.0 |
| 6.0 | 0.0143 | 1.087 | **2.50** (floor) | 5.00 | 25.0 |
| 5.0 | 0.0358 | 1.567 | 3.45 | 6.90 | 47.5 |
| 4.0 | 0.0900 | 2.116 | 4.66 | 9.31 | 86.7 |
| 3.0 | 0.2261 | 2.771 | 6.10 | 12.19 | 148.7 |
| 2.0 | 0.5679 | 3.572 | 7.86 | 15.72 | 247.0 |
| 1.0 | 1.4264 | 4.561 | 10.03 | 20.07 | 402.8 |
| 0.0 | 3.5830 | 5.793 | 12.74 | 25.49 | 649.7 |
| −1.44 | 13.497 | 8.129 | 17.88 | 35.77 | 1279.2 |

(**V**, computed.) The formula and the floor cross at m ≈ 5.9, so the floor only ever *enlarges* a
quad; the crop is at or below ε for every star, and there is no hard edge anywhere.

**The minimum, 2.5 px half (5.0 px quad), is a sampling floor, not a brightness floor.** It exists
because of the table in §3.3, and it is set by α: it is the smallest crop that still contains the
core. It costs nothing photometrically — a star clipped at the floor has already fallen below ε,
so the pixels the floor adds are black.

Crucially, the faint majority all sit *exactly* on this floor and are therefore all the same size.
That is correct and it is the point: **the size of a star image is a property of the instrument,
not of the star.** The 1.7.0 distribution problem — `pow(u, 3.2)` putting almost every star at the
size floor — was only a problem because size was carrying brightness. It is not a problem now; it
is the expected result.

**The maximum is enforced on amplitude, not on the quad.** Clamping `halfPx` would crop the profile
somewhere above ε and put the square edge straight back. Instead the CPU derives
`A_max = ε(1 + (halfMax/α)²)²` from `halfMax`, and the shader clamps `A`. The quad formula then
cannot exceed `halfMax` — algebraically, not by a `clamp`:

```
A = A_max  ⟹  u_max = sqrt(sqrt(14400) − 1) = sqrt(119) = 10.909
             halfPx = 10.909 × 2.2 = 24.00 px          ✓ exactly the cap   (V)
```

`halfMax = 24 px` = 21 mrad = 1.2° of half-angle, about 2.4° across, roughly five full Moons. It
binds only while flying at a star: a star of M<sub>V</sub> = −7.2 (the catalogue's most luminous,
**V**) seen from 1 pc reaches A ≈ 2.7 × 10⁵ and would want a 214 px half-extent. It is capped,
the `BRIGHTNESS ×N` chip fires, and within 1 pc `NearbyStarSystems` promotes it to a
`CelestialBodyView` with a real angular diameter and the point is de-duplicated (`data.w = 1`),
which is the contract's existing mechanism.

This is the same rule `CelestialBodyView` already learned the hard way — *"A minimum apparent size
needs a maximum to go with it"* (**V**, `CLAUDE.md`; the 3 m Viking lander inflated to ten
thousand kilometres and blacked out the screen). Here the minimum and the maximum are derived from
one another, so they cannot drift apart.

### 3.5 Where `L^0.28` comes from

For any profile with a power-law wing `P(r) ∝ (r/α)^(−n)`, the radius at which a star of amplitude
A crosses a fixed threshold ε is

```
r_ε = α (A/ε)^(1/n)
```

so apparent radius is a **power of flux with exponent 1/n**, and nothing had to be tuned to make it
so. β = 2 gives n = 4 and exponent **0.25**. The human eye's own glare point-spread — the CIE
general glare equation, whose veiling luminance falls as θ⁻³ in the near field and θ⁻² further out
(**I**, a recollection of the published form; nothing below depends on it) — gives exponents
between 0.33 and 0.5. The atlas's empirically-tuned **0.28** sits just inside that range, one notch
shallower than β = 2.

So the owner's constant is not replaced so much as explained: the design keeps the behaviour and
drops the constant. If a headset session says Sirius wants to be bigger, the knob is β, and
turning it moves brightness and size together in a way that stays physical.

---

## 4. Diffraction spikes

Spikes are drawn **analytically in the fragment shader**, not as geometry. A.4 budgets two crossed
quads plus a halo quad for "roughly 50 stars", baked by the layer. In layer mode that is wrong as
well as expensive: which stars are bright depends on where the viewer is, so a baked set of 50 is
correct only at the Sun and goes stale the moment anyone travels. Fixing it with a CPU rebuild
means a top-K sort over 120 k stars on a trigger, a second mesh, and a de-duplication invariant to
maintain — for an effect that is four ALU wide.

The term is separable and rational, in the same core-radii coordinates:

```hlsl
float2 aL = u / _Spike.x;          // along the arm,  in arm half-lengths
float2 aW = u / _Spike.y;          // across the arm, in arm half-widths
float  wy = 1.0 + aW.y*aW.y,  wx = 1.0 + aW.x*aW.x;
float  sx = rcp(1.0 + aL.x*aL.x) * rcp(wy*wy);     // horizontal arm
float  sy = rcp(1.0 + aL.y*aL.y) * rcp(wx*wx);     // vertical arm
p += _Spike.z * i.colour.a * (sx + sy);            // .a carries the onset ramp
```

| Constant | Value | Justification |
|---|---|---|
| arm half-length `_Spike.x` | 9.0 core radii = 19.8 px | reaches the edge of a magnitude-1 star's quad (half 10.0 px) and dies inside a magnitude-0 star's (12.7 px), so an arm is never clipped by its own quad |
| arm half-width `_Spike.y` | 0.55 core radii = 1.2 px | narrower than the core: a spike must read as a line, not a lobe. The `rcp(w*w)` makes the across-arm falloff quartic, so the arm has crisp sides at no extra cost |
| gain `_Spike.z` | 0.06 | adds 0.12 to a peak of 1.0 (**V**: the two arms each contribute at the centre). Invisible photometrically after tone compression — Sirius moves from 0.9310 to 0.9380 — and clearly visible as arms |
| onset `_Spike.w` | A = 0.25 | `ramp = saturate(A/0.25 − 1)`: nothing below A 0.25 (m ≈ 3.1 at `m_cut` 6.5), full at A 0.5 (m ≈ 2.3). From Earth that is **≈ 180 stars partially and ≈ 50 fully** out of 8,920 drawn (**V**, from the catalogue's cumulative counts) — "the brightest few per cent", measured |

Because the onset is in *amplitude*, it tracks exposure automatically: dim the sky and the spikes
retreat to the brightest stars, exactly as glare does.

Spikes are **screen-aligned**, which is free (the quad already is). That is also the physically
right choice here: these model the observer's glare, not a telescope's secondary-mirror vanes, and
the eye's glare pattern does not rotate with the world.

**Cost.** The whole term is behind `UNITY_BRANCH if (i.colour.a > 0)`. For the 99.4 % of drawn
stars with no spikes it is one scalar compare. For the ~180 that have them it is ≈ 14 ALU over
≈ 180 × 250 px = 45 k fragments per eye — 0.010 eye-screens (**V**, arithmetic in §11). Extra
draws: **0**. Extra vertices: **0**. Extra CPU: **0**.

---

## 5. Colour

### 5.1 `Teff` → linear sRGB

The catalogue ships `teff` in kelvin, not B−V: `build_starfield.py` converts B−V to Teff with
Ballesteros 2012 at bake time and CSS1/CSS2 store the temperature (**V**, `StarCatalog.cs`
`CatalogStar.TemperatureK`, and contract C.1). So "colour from B−V" is settled upstream; the
question here is B−V → Teff → **linear** RGB.

Do **not** port the atlas's Tanner Helland fit. It is a fit to *display* bytes, published for
sRGB output, and using it in a Linear project is the BoltGraphXR trap in its subtler form — that
project's shaders carry `pow(abs(i.color.rgb), 2.2)` in the fragment with the comment *"vertex
colors arrive as raw sRGB bytes; the project renders linear, so decode here or everything reads
washed out"* (**V**, `BoltPoints.shader:84`, `BoltVertexColor.shader:59`). The trap is not that the
decode is hard; it is that a display-referred fit has no defined linear meaning, so you cannot tell
a missing decode from a double one by looking.

Compute the colour properly instead, once, at bake: Planck spectrum × CIE 1931 colour-matching
functions → XYZ → linear sRGB (D65 matrix) → clamp negatives → normalise so the largest component
is 1. Computed on triton with the Wyman–Sloan–Shirley 2013 multi-lobe Gaussian fit to the CIE
1931 CMFs; the fit is self-checked against the published black-body locus — 6500 K comes out at
x = 0.3135, y = 0.3237 and 5778 K at x = 0.3263, y = 0.3359, both within 0.002 of the published
values (**V**).

| Teff (K) | x | y | linear R, G, B (max = 1) | sRGB bytes for `COLOR` | Y of that chromaticity |
|---|---|---|---|---|---|
| 2 000 | 0.5242 | 0.4157 | 1.0000 0.2663 0.0077 | 255, 141, 21 | 0.4036 |
| 2 500 | 0.4755 | 0.4151 | 1.0000 0.3803 0.0677 | 255, 166, 74 | 0.4895 |
| 3 000 | 0.4359 | 0.4050 | 1.0000 0.4840 0.1545 | 255, 185, 110 | 0.5699 |
| 3 500 | 0.4046 | 0.3914 | 1.0000 0.5768 0.2606 | 255, 200, 140 | 0.6439 |
| 4 000 | 0.3799 | 0.3772 | 1.0000 0.6590 0.3788 | 255, 212, 166 | 0.7113 |
| 4 500 | 0.3604 | 0.3639 | 1.0000 0.7316 0.5034 | 255, 222, 188 | 0.7722 |
| 5 000 | 0.3449 | 0.3519 | 1.0000 0.7955 0.6299 | 255, 231, 208 | 0.8270 |
| **5 778** (Sun) | 0.3263 | 0.3359 | 1.0000 0.8799 0.8230 | 255, 241, 234 | 0.9013 |
| 6 000 | 0.3220 | 0.3319 | 1.0000 0.9010 0.8764 | 255, 244, 241 | 0.9203 |
| 6 500 | 0.3135 | 0.3237 | 1.0000 0.9445 0.9929 | 255, 249, 254 | 0.9598 |
| 7 000 | 0.3064 | 0.3166 | 0.9062 0.8908 1.0000 | 244, 242, 255 | 0.9019 |
| 8 000 | 0.2952 | 0.3048 | 0.7657 0.8018 1.0000 | 227, 231, 255 | 0.8085 |
| 9 000 | 0.2870 | 0.2956 | 0.6740 0.7401 1.0000 | 214, 223, 255 | 0.7448 |
| 10 000 | 0.2807 | 0.2883 | 0.6103 0.6951 1.0000 | 205, 217, 255 | 0.6991 |
| 12 000 | 0.2719 | 0.2775 | 0.5287 0.6345 1.0000 | 192, 209, 255 | 0.6384 |
| 15 000 | 0.2638 | 0.2672 | 0.4616 0.5816 1.0000 | 181, 201, 255 | 0.5863 |
| 20 000 | 0.2566 | 0.2576 | 0.4070 0.5359 1.0000 | 171, 193, 255 | 0.5420 |
| 30 000 | 0.2502 | 0.2487 | 0.3628 0.4970 1.0000 | 162, 187, 255 | 0.5048 |
| 40 000 | 0.2474 | 0.2447 | 0.3440 0.4799 1.0000 | 158, 184, 255 | 0.4886 |

The catalogue's shipped range is 1 499 – 50 000 K, median 5 875 K (**V**), so this table covers it
with 500 K steps where the colour moves fastest. Bake a 64-entry lookup over log Teff in Core;
`Core/` may compute it (no `UnityEngine`), and it is the *only* place a colour is decided.

**The `sRGB bytes` column is what goes into the vertex `COLOR`**, so the convention stays exactly
what every other CosmoSynth shader uses: vertex colours are display-referred, decoded once with
`CS_DisplayToLinear`. Nothing becomes special; §7.1 rule 7 is obeyed.

### 5.2 Decode the hue, never the brightness

```hlsl
// WRONG — this is contract A.4 as written:
o.color = CS_DisplayToLinear(tinted * level) * gain;      // level^2.2, not level

// RIGHT:
half3 chroma = CS_DisplayToLinear(v.color.rgb);           // the hue, exactly once
...
o.colour.rgb = chroma * (half)(A / y);                    // radiance, already linear
```

`SRGBToLinear` is applied to a value that has never been multiplied by anything. That is the
invariant a gate can check by grep: `CS_DisplayToLinear` must take a bare `v.color.rgb`.

### 5.3 Luminance-preserving colour

A max-normalised chromaticity has a different luminance for every temperature — 0.40 at 2 000 K,
0.96 at 6 500 K (the `Y` column above). Multiplying it by A would make a red star 2.4× dimmer than
a white star of the same **V** magnitude, which contradicts the definition of magnitude. One `dot`
and one divide fix it:

```hlsl
half y = max(dot(chroma, half3(0.2126h, 0.7152h, 0.0722h)), 1e-3h);
o.colour.rgb = chroma * (half)(A / y);        // Rec.709 luminance of the result == A, exactly
```

No extra storage, and it also makes the desaturation free of side-effects.

**Desaturation.** `StarLook`'s existing rule — saturation rises with brightness, because colour
vision is a cone function and faint stars do not excite cones — is right and is kept, with its
reasoning re-anchored on magnitude rather than on a normalised ramp:

```hlsl
half sat = (half)saturate((_Colour.x - magnitude) * _Colour.y);   // 3.5, 1/3
chroma = lerp(half3(1,1,1), chroma, 0.30h + 0.70h * sat);
```

`_Colour.x = 3.5`, `_Colour.y = 1/3`: fully colourless at m ≥ 3.5, fully coloured at m ≤ 0.5.
Both numbers are **I** and are the visual-astronomy rule of thumb that star colour becomes
apparent around second magnitude; they are cheap to re-tune and the luminance divide above means
re-tuning them cannot change any star's brightness.

---

## 6. Geometry, vertex layout and LOD

### 6.1 Mesh

Exactly the contract's `PointChunks` (§3.4), which is exactly the pattern Chemispace ships
(**V**, `UniverseView.cs:100-143`): four vertices per point sharing one position, corner index in
a per-vertex channel, `IndexFormat.UInt32`, one `MeshRenderer` per chunk with a per-chunk material
instance holding `_EyeInChunk`, huge bounds because the vertices move in the shader, and a CPU
cone cull over the chunks' angular discs.

No `RenderMeshInstanced`, no indirect draws — §7 of the handover forbids them and the reason is
real (instance-count doubling under multiview).

### 6.2 Vertex layout

| Channel | Type | Bytes | Content |
|---|---|---|---|
| `POSITION` | float3 | 12 | offset from the chunk origin, ICRF components, layer units (pc) |
| `COLOR` | UNorm8×4 | 4 | display-referred chromaticity; `a` unused |
| `TEXCOORD0` | **half4** | 8 | x: M<sub>V</sub> (absolute in layer mode, apparent otherwise) · y: corner 0…3 · z: unused · w: hidden flag |
| | | **24 B** | |

A.4 specifies 32 B with a float4 `TEXCOORD0` carrying a `shape` field. Deleting the halo and spike
quads deletes `shape`; moving to `half4` costs nothing real — half resolves M<sub>V</sub> to 0.01
mag over the catalogue's −7.2 … +19.6 range (**V**), against a magnitude column whose own
uncertainty is ≥ 0.05 mag. That is 8 B × 478 452 = **3.8 MB of vertex buffer saved**, 15.3 → 11.5 MB.

R14 still holds and gets stronger: **all four vertices of a quad carry the same `data.w`** *and*
the same `data.x`, so every early-out in the vertex shader is bit-identical across the four
corners. There are now two early-outs (hidden, and below-cut); both depend only on per-star data,
so neither can produce the clipped wedge R14 warns about. `PointChunksTests` asserts the fourfold
equality of `data.w` today; it must be extended to `data.x` and to `COLOR`.

### 6.3 Magnitude-banded LOD is deleted

The brief asks for magnitude-banded LOD. The arithmetic says not to build it.

Geometry banding can only save **vertex** cost. Fragment cost is already shed, completely, by the
`ratio <= 1` early-out: a star below the cut writes an off-screen clip position and rasterises
nothing. So the question is whether vertex cost is ever the binding constraint.

* Mesh total: 119 613 × 4 = **478 452 vertices** — at the contract's 480 k cap (**V**).
* Processed per frame: only the chunks the cone cull enables, "about 20 of 64" (§7.3) ⇒ ≈ 150 k,
  doubled by single-pass instancing ⇒ ≈ 300 k vertex invocations per frame.
* Meta's own Quest 3 guidance, taken conservatively in §7.3, is 1.3–1.8 M **triangles**. 300 k
  vertex invocations is 150 k triangles.

There is no regime in the ladder where that is the limit and the fill is not. And if it ever
becomes the limit, the cheap lever already exists and costs nothing: **skip a whole chunk whose
brightest star is below the cut**, one compare per chunk inside the `Apply` loop that already runs.
Bake `minAbsoluteMagnitude` per chunk (4 B) next to the histogram.

So: the governor's documented "shed star vertices" lever (§5.7.5 rule 9) is re-defined as **raise
`m_cut`**. It sheds fragments, which is what costs, and it is the same single number that already
means exposure and LOD, and it announces itself through `BRIGHTNESS ×N`.

---

## 7. The sky shell and the 3D volume

Today's 9 000 quads on a 900-unit shell exist because `DepthBands` had a far plane and distance had
to be thrown away. Under §2.3's distance map they are unnecessary, and the reason is a number.

`f(d) = d₀(1 + ln(d/d₀))`, d₀ = 10 m. Render depth and the resulting binocular disparity at an IPD
of 63 mm and 1 140 px/rad (**V**, computed):

| star | true distance | f(d) | disparity |
|---|---|---|---|
| 206 AU (0.001 pc) | 3.09e13 m | 297.6 m | 0.241 px |
| 0.1 pc | 3.09e15 m | 343.6 m | 0.209 px |
| 1 pc | 3.09e16 m | 366.7 m | 0.196 px |
| Proxima, 1.30 pc | 4.01e16 m | 369.3 m | 0.194 px |
| Sirius, 2.64 pc | 8.15e16 m | 376.4 m | 0.191 px |
| 194 pc (catalogue median) | 5.99e18 m | 419.3 m | 0.171 px |
| 990 pc (catalogue max) | 3.06e19 m | 435.6 m | 0.165 px |
| Sgr A*, 8.18 kpc | 2.52e20 m | 456.8 m | 0.157 px |
| **the sky shell** | ∞ | 1 500 m | **0.048 px** |

The entire three-dimensional stellar volume, spanning five decades of true distance, occupies
298 – 457 m of render depth, and the difference in disparity between the nearest star in the
catalogue and infinity is **0.19 px** — below the 0.5 px threshold the contract uses for every
other precision claim (§3.4, §2.7), and far below anything a headset resolves.

**Therefore there is no shell, and no handover.** One layer, one shader, one placement mode
(`_Space = 0`, per-point compression). The only rows that are not in the volume are the **146**
with no usable parallax (**V**, contract C12; the shipped CSS1 still has 10 224, which CSS2's
AT-HYG astrometry reduces to 146), and those go on `_Space = 2` at 1 500 m with an apparent
magnitude straight in `data.x` — 584 vertices, one draw, faded out once `ViewerFromSunPc ≥ 0.5`
exactly as §5.4 already specifies.

What *does* change as the viewer leaves the Solar System is not the placement but the population:
stars pass in and out of the cut as their apparent magnitudes change, constellations shear apart
because their endpoints are real 3D positions, and Sol appears as an ordinary G star behind you.
None of that needs a mechanism; it falls out of `m = M + 5 log₁₀(D) − 5` being evaluated per
vertex per frame.

The near field is guarded by two things that already exist: the `_Look.zw` near-fade over the
chunk's precision limit (§3.4's 1 200 AU bound for a 25 pc chunk), and `NearbyStarSystems`
promoting anything within 1 pc to real geometry.

---

## 8. Exposure, bloom, and the image pipeline

### 8.1 Stars do not use post-process bloom, and that is a feature

Reading URP 17.5's `Bloom.shader` on this box (**V**,
`Library/PackageCache/com.unity.render-pipelines.universal@4cbcec34e716/Shaders/PostProcessing/Bloom.shader:56-63`):

```hlsl
half3 SamplePrefilter(float2 uv, float2 offset)
{
    half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * offset);
    #if _ENABLE_ALPHA_OUTPUT
        // regions with zero alpha should not generate any bloom / glow
        color.xyz *= color.w;
    #endif
    return color.xyz;
}
```

`_ENABLE_ALPHA_OUTPUT` is set from `cameraData.isAlphaOutputEnabled`, which with post-processing on
requires the URP asset's `allowPostProcessAlphaOutput`
(`UniversalRenderPipeline.cs:1828`, **V**). In MR the camera clears alpha 0 and every additive pass
leaves destination alpha untouched by contract (`Blend One One, Zero One`). **So in passthrough,
with alpha output on — which passthrough requires — every star, every nebula and every additive
layer contributes exactly zero to bloom.** Only the opaque bodies, which write alpha 1, bloom at
all.

That is a landmine for the image-pipeline topic and it is stated here so it is not discovered on a
headset. This layer is immune to it, because the glare is in the profile:

* the Moffat wing **is** the halo, at the right size for the star's own flux, per star, in one
  pass, with no render target, no downsample and no threshold;
* nothing scintillates. URP 17.5's bloom prefilter has **no Karis average** (**V**: the 13-tap
  `_BLOOM_HQ` filter at `Bloom.shader:78-100` is a fixed-weight tent; the only firefly control is
  `ClampMax`). A 5 px star fed through a half- or quarter-resolution pyramid is a sub-texel
  feature whose contribution flickers with sub-pixel head motion. A star field is the worst
  possible content for a downsampled bloom;
* and it works identically in Space mode and MR mode, where a bloom that depends on alpha does not.

**Handshake with the image pipeline.** Post bloom stays for the Sun's disc, planet limbs, and UI.
Its threshold must sit above the star layer's clamped peak so the two never compound. With
`_Look.y = 1` (in-shader tone, §8.3) star output is bounded by 1.0, so a Bloom Threshold ≥ 1.0 in
linear is sufficient and checkable in G0c.

### 8.2 Exposure handshake

One rule, so whichever lane lands first the other composes:

> The star layer targets a **displayed** faint-end amplitude of `A_min` *after* the pipeline's
> exposure. It therefore sets `_Look.x = 10^(−0.4 (m_cut − 6.5)) / exp2(EV_pipeline)` and reads
> `EV_pipeline` from the same global the pipeline publishes. If the pipeline has no exposure,
> `EV_pipeline = 0` and nothing changes.

Two auto-adapting systems multiplying each other is the classic way to get a sky that breathes.
This makes the composition explicit, keeps `m_cut` as the honest number the chip reports, and
leaves the star layer shippable before the image pipeline exists.

### 8.3 Tone compression, with or without an HDR eye buffer

A star's amplitude reaches 13.5 for Sirius. If the eye buffer is LDR, an additive blend clips at
1.0 and re-creates the flat top the project has already shipped once.

`_Look.y` is the switch:

```hlsl
UNITY_BRANCH if (_Look.y > 0.5) c = c / (1.0h + c);    // Reinhard, in the star shader
```

| | `_Look.y` | why |
|---|---|---|
| URP asset HDR **off** (today's Worldfall-derived baseline, **V**: research §8 recommends "MSAA 4x, HDR off") | **1** | the star layer compresses its own range; `c/(1+c)` is smooth with a derivative that never reaches zero, so the iso-contours stay circular and there is no plateau at any amplitude |
| URP asset HDR **on**, tone mapping in the pipeline | **0** | one tone curve, in one place |

Values under the in-shader curve (**V**): m 3.0 → 0.184, m 1.0 → 0.588, m 0.0 → 0.782,
Sirius → 0.931. A 1 500 : 1 flux range lands inside [0.0085, 0.931] of display range with the rest
of the information carried by radius, which is precisely how a photograph of a sky works.

The G1c `honesty` assertion gains one row: `_Look.y == 1` **iff** the URP asset's HDR is off. Two
places that must agree, checked mechanically, because agreeing by inspection is how this project
got a shader that attenuated the channel the brightness was travelling in.

---

## 9. Foveation: the risk this design creates

SRP foveation is on (§8.1). It reduces the **shading rate** toward the periphery; the rasterizer
still covers the same pixels but one shaded value serves a block. A star's core is therefore
sampled on a coarser grid off-axis, and §3.3's table says exactly what that costs (**V**):

| region | effective α | median peak recovered | p05 |
|---|---|---|---|
| fovea, 1 : 1 | 2.20 px | 0.940 | 0.870 |
| FFR ½ linear | 1.10 px | ≈ 0.75 | ≈ 0.55 |
| FFR ¼ linear | 0.55 px | ≈ 0.50 | ≈ 0.26 |

So the sky can be expected to **thin out and shimmer toward the edges of the display**, and the
faintest stars there will drop below perception star-by-star as the head moves. This is the
single most likely visual complaint after this design lands, and it is invisible to every test
this box can run.

It is also the place where foveation's premise is weakest. FFR assumes the periphery cannot use
resolution; for *point sources* the relevant task is detection, not resolution, and peripheral rod
vision detects points well.

**Mitigation, costed:** widen the core with eccentricity, three ALU in the vertex shader.

```hlsl
float ecc      = min(length(cs.xy / max(cs.w, 1e-6)), 1.5);   // 0 at centre, ~1 at the edge
float alphaPx  = _Psf.x * (1.0 + _Psf.w * ecc * ecc);         // _Psf.w = k, default 1.0
```

At k = 1 the core doubles at the frame edge, restoring ≈ 2.2 shaded px where FFR halves the rate.
Fill grows only where FFR has already made it cheap — which is the trade FFR is offering.
`_Psf.w = 0` disables it exactly. **k must be tuned on a headset**, and §13 gives the experiment.

The core widening also has to feed the quad: because the quad is derived from `u_max · α`, a
widened core automatically widens the crop, and `A_max` is derived from `halfMax/α` on the CPU
using the **unwidened** α, so the cap stays conservative. That is deliberate; note it in the code.

---

## 10. `CosmoSynth/Point`, verbatim

Replaces Appendix A.4. Single variant, six stereo macros, additive without alpha, no matrix in
`UnityPerMaterial`, no `GetCameraPositionWS`, no object space in the fragment, `_Fade` present.

```hlsl
Shader "CosmoSynth/Point"
{
    Properties
    {
        _EyeInChunk ("Layer mode: eye in this chunk's units", Vector) = (0,0,0,0)
        _Units      ("x: metres per unit, y: parsecs per unit", Vector) = (1,1,0,0)
        // x m_cut, y A_min (linear radiance of a star AT m_cut), z A_max (DERIVED from _Psf on
        // the CPU: eps*(1+(maxHalfPx/corePx)^2)^2 -- never typed by hand), w eps (the cut)
        _Photometry ("x: m_cut, y: A_min, z: A_max, w: cut", Vector) = (6.5, 0.009, 43.2, 0.003)
        // x core radius px (alpha), y min half px, z max half px, w fovea compensation k
        _Psf        ("x: core px, y: min half px, z: max half px, w: fovea k", Vector) = (2.2, 2.5, 24.0, 1.0)
        // x arm half-length, y arm half-width (both in CORE RADII), z gain, w onset amplitude
        _Spike      ("x: arm len, y: arm width, z: gain, w: onset A", Vector) = (9.0, 0.55, 0.06, 0.25)
        _Colour     ("x: magnitude at zero saturation, y: 1/width", Vector) = (3.5, 0.33333, 0, 0)
        // x exposure, y tone-in-shader (1 when the URP asset has HDR off), z/w near-fade (layer units)
        _Look       ("x: exposure, y: tone here, z/w: near fade", Vector) = (1, 1, 0, 0)
        _Fade       ("Layer fade", Float) = 1
        _Space      ("0 layer (per-point f), 1 object (CPU-placed), 2 sky (direction, per eye)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50"
               "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Points"
            Tags { "LightMode"="UniversalForward" }
            Blend One One, Zero One     // additive colour; destination alpha (passthrough) untouched
            ZWrite Off
            ZTest LEqual                // behind opaque bodies, which draw first
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing          // [stereo 1/6]
            #include "CosmoCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EyeInChunk;
                float4 _Units;
                float4 _Photometry;
                float4 _Psf;
                float4 _Spike;
                float4 _Colour;
                float4 _Look;
                float  _Fade;
                float  _Space;
                // No matrix here (R4). ICRF -> world is the GLOBAL _CS_IcrfToWorld.
            CBUFFER_END

            struct Attributes
            {
                float3 offset : POSITION;    // layer: offset from the chunk origin (pc, ICRF)
                half4  color  : COLOR;       // display-referred chromaticity, max component 1; a unused
                half4  data   : TEXCOORD0;   // x M_V (absolute in layer mode, apparent otherwise),
                                             // y corner 0..3, z unused, w hidden
                UNITY_VERTEX_INPUT_INSTANCE_ID                    // [stereo 2/6]
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  colour     : COLOR;       // rgb linear radiance, a spike onset ramp
                float2 uv         : TEXCOORD0;   // position in CORE RADII (NOT quad-relative)
                UNITY_VERTEX_OUTPUT_STEREO                        // [stereo 3/6]
            };

            // Off-screen clip position. Clips the primitive rather than discarding a fragment, so
            // it is only safe when every vertex of the quad computes it (R14): both early-outs
            // below depend solely on per-STAR data, which is identical across the four corners.
            #define CS_CULLED float4(2, 2, 2, 1)

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);                       // [stereo 4/6]
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);         // [stereo 5/6]

                if (v.data.w > 0.5h) { o.positionCS = CS_CULLED; return o; }   // de-duplicated

                float3 centreWS;
                float  magnitude = (float)v.data.x;
                float  nearFade  = 1.0;

                if (_Space < 0.5)
                {
                    // Chunk-relative, eye-relative, precise near the chunk (contract 3.4), then f(d).
                    float3 rel   = v.offset - _EyeInChunk.xyz;
                    float  du    = max(CS_Length(rel), 1e-30);
                    float3 dirWS = normalize(CS_IcrfDirToWorld(rel / du));
                    centreWS     = _CS_CentreEyeWS.xyz + dirWS * CS_CompressDistance(du * _Units.x);
                    // M -> m. 1.5051500 = 5 / log2(10), so one log2 replaces log10.
                    magnitude   += 1.5051500 * log2(max(du * _Units.y, 1e-12)) - 5.0;
                    nearFade     = smoothstep(_Look.z, _Look.w, du);
                }
                else if (_Space < 1.5)
                {
                    centreWS = TransformObjectToWorld(v.offset);          // placed by the CPU
                }
                else
                {
                    float3 dirWS = normalize(CS_IcrfDirToWorld(v.offset));
                    centreWS = CS_EyePositionWS() + dirWS * _CS_Compress.z;   // THIS eye (R1)
                }

                // Flux, exactly. 1.3287712 = 0.4*log2(10); one magnitude is 10^0.4 of radiance.
                float A = _Photometry.y * exp2(-1.3287712 * (magnitude - _Photometry.x));
                A = min(A * _Look.x * _Fade * nearFade, _Photometry.z);

                // Below the cut: no fragments at all. This IS the LOD.
                float ratio = A / max(_Photometry.w, 1e-8);
                if (ratio <= 1.0) { o.positionCS = CS_CULLED; return o; }

                float4 cs = TransformWorldToHClip(centreWS);

                // Core radius, widened toward the periphery where SRP foveation shades coarsely.
                // _Psf.w = 0 disables it exactly; _Psf.z (and hence _Photometry.z) is derived on
                // the CPU from the UNWIDENED core, so the amplitude cap stays conservative.
                float ecc     = min(length(cs.xy / max(cs.w, 1e-6)), 1.5);
                float alphaPx = _Psf.x * (1.0 + _Psf.w * ecc * ecc);

                // The quad is a CROP placed exactly where the profile falls to the cut:
                //     A / (1 + u^2)^2 = eps   =>   u = sqrt(sqrt(A/eps) - 1)
                // so no quad edge is ever visible, at any magnitude. (Contract A.4 cropped at
                // 1/16 of peak on every star, which is a soft square.)
                float uMax   = sqrt(sqrt(ratio) - 1.0);
                float halfPx = clamp(uMax * alphaPx, _Psf.y, _Psf.z);

                uint   ci     = (uint)v.data.y;
                float2 corner = float2((ci & 1u) ? 1.0 : -1.0, (ci & 2u) ? 1.0 : -1.0);
                float  ppr    = CS_PixelsPerRadian();
                cs.xy += corner * CS_AngularToClip((halfPx / ppr).xx, cs.w);
                o.positionCS = cs;

                // uv in CORE RADII: the profile is an angular function of the star, not a
                // decoration stretched over whatever rectangle the quad turned out to be.
                o.uv = corner * (halfPx / alphaPx);

                // Colour. Decode the HUE exactly once, and never a value that has been multiplied
                // by a brightness (contract A.4's CS_DisplayToLinear(tinted * level) raised the
                // brightness to the power 2.2).
                half3 chroma = CS_DisplayToLinear(v.color.rgb);
                half  sat    = (half)saturate((_Colour.x - magnitude) * _Colour.y);
                chroma = lerp(half3(1, 1, 1), chroma, 0.30h + 0.70h * sat);
                // Divide out the chromaticity's own luminance so radiance is colour-independent:
                // two stars of equal V magnitude are equally bright whatever their temperature.
                half  y = max(dot(chroma, half3(0.2126h, 0.7152h, 0.0722h)), 1e-3h);
                o.colour = half4(chroma * (half)(A / y),
                                 (half)saturate(A / max(_Spike.w, 1e-6) - 1.0));   // spike ramp
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);      // [stereo 6/6]

                // Moffat beta = 2. Peak exactly 1, wing ~ u^-4, two muls and a reciprocal, no
                // transcendental. Computed in float: (1+u^2)^2 reaches 14400 at the amplitude cap,
                // which half survives but only just, and the fovea term can push u further.
                float2 u  = i.uv;
                float  t  = 1.0 + dot(u, u);
                float  p  = rcp(t * t);

                // Diffraction spikes for the brightest few per cent. Uniform-coherent branch: the
                // ramp is constant over a quad, so every fragment of one star agrees.
                UNITY_BRANCH if (i.colour.a > 0.0h)
                {
                    float2 aL = u / _Spike.x;      // along the arm,  in arm half-lengths
                    float2 aW = u / _Spike.y;      // across the arm, in arm half-widths
                    float  wy = 1.0 + aW.y * aW.y;
                    float  wx = 1.0 + aW.x * aW.x;
                    float  sx = rcp(1.0 + aL.x * aL.x) * rcp(wy * wy);
                    float  sy = rcp(1.0 + aL.y * aL.y) * rcp(wx * wx);
                    p += _Spike.z * (float)i.colour.a * (sx + sy);
                }

                half3 c = i.colour.rgb * (half)p;

                // The star layer compresses its own range when the pipeline cannot (HDR off).
                // Smooth everywhere, derivative never zero, so no iso-contour is ever flat.
                UNITY_BRANCH if (_Look.y > 0.5) c = c / (1.0h + c);

                return half4(c, 0.0h);      // additive; destination alpha untouched
            }
            ENDHLSL
        }
    }
    Fallback Off
}
```

Three notes for the integrator.

* **`CS_CompressDistance` is called unchanged**, so §2.7's "must equal Core `ScaledSpace.Compress`"
  still holds bit-for-bit. There is a free optimisation available — `f` and the magnitude both need
  a log of `du`, and with two baked constants one `log2` serves both — but it changes the result in
  the last ulp, so it is only worth taking if the G1c `placement` assertion is stated with a
  tolerance. Left out by default.
* **`CS_AngularToClip` takes a `float2`**; the `.xx` on a scalar is deliberate, and points and
  ribbons both use the vertical `PixelsPerRadian` for both axes per A.2's note.
* `_Photometry.z` and `_Psf.y/.z` are **not independent**. `StarLook` computes
  `A_max = cut * pow(1 + sqr(maxHalfPx / corePx), 2)` and writes both into the material; a test
  asserts the round trip (`halfPx(A_max) == maxHalfPx` to 0.01 px).

---

## 11. Cost, in the budget's own units

§7.3 allocates the `stars` layer: ≤ 64 draws (≈ 20 in view), ≤ 480 k vertices (≈ 100 k in view),
≤ 0.5 eye-screens of fragments, 0 step-coverages, 0 texture MB, 1.2 ms GPU.

**Draws.** 1 (sky, 146 rows) + up to 64 chunk renderers, ≈ 20 enabled by the cone cull. Spikes,
halos and LOD bands add **0**. ✅ within 64.

**Vertices.** 119 613 × 4 = **478 452** in the mesh (**V**), ≈ 150 k in enabled chunks, ×2 for
single-pass instancing = ≈ 300 k invocations. ✅ within 480 k. Vertex buffer 24 B × 478 452 =
**11.5 MB** (A.4's 32 B layout would be 15.3 MB).

**Fragments.** Computed over the real catalogue with the §3.4 quad formula (**V**):

| case | stars drawn | Σ quad area | eye-screens (eye = 2064 × 2208 = 4.557 Mpx) |
|---|---|---|---|
| naked-eye sky from Earth, `m_cut` 6.5 | 8 920 | 355 549 px | **0.078** |
| governor target, all at the 5 px floor | 60 000 | 1 500 000 px | **0.329** |
| the cap the governor must not cross | 100 000 | 2 500 000 px | 0.549 ✗ |

So **`N_target` = 60 000** is the governor's setpoint, and 100 000 is where the budget breaks; the
gap is the margin for the bright tail (from Earth the 179 stars brighter than magnitude 3 cost
≈ 52 k px, 0.011 eye-screens). ✅ within 0.5.

Spikes add ≈ 180 stars × ≈ 250 px = 45 k px = **0.010 eye-screens** of *extra ALU*, not extra
coverage — those fragments are drawn anyway.

**Step-coverages: 0.** No raymarch.

**Texture bytes: 0.** `StarSprite`'s 32² RGBA + mips (≈ 5.5 KB) is deleted and nothing replaces it.

**APK bytes: 0.** No new asset. The catalogue itself belongs to the data lane (CSS2 at 44 B ×
119 669 = 5.27 MB, contract C.1).

**GPU milliseconds — INFERRED, with the experiment.** The only device-measured anchor on this box
is Fractarium's **0.338 ms per step-coverage** on Quest 3 (**V**, quoted in §7.3 and in
`docs/research/2026-09-11-quest-rendering-techniques.md:376`, itself flagged there as an upper
bound taken with foveation off). One step-coverage is one march step over the area of both eyes; a
star fragment is perhaps a fifth of a Mandelbulb distance-estimate step. Using the anchor as a
ceiling:

```
worst-case fill  = 0.36 eye-screens ≈ 0.36 step-coverages
                 ≤ 0.36 × 0.338 ms = 0.12 ms          (ceiling; likely 0.02-0.05 ms)
Earth fill       = 0.078            ≤ 0.026 ms
vertex cost      = UNMEASURED
```

Vertex cost is the unknown. The vertex function carries one `log2`, one `log`
(`CS_CompressDistance`), one `exp2`, two `normalize`, a 3×3 rotate and a 4×4 transform — call it
80 ALU plus 3 transcendentals, over 300 k invocations = 24 MFLOP/frame, 1.7 GFLOP/s at 72 Hz.
**I** would put the total at 0.3–0.6 ms against a 1.2 ms allocation, but that is a guess.

> **Experiment E1 (settles the GPU row).** Build one APK with a `-starCount` switch, capture
> `adb logcat -d | grep VrApi` `GPU%`/`App` at N = 0, 30 k, 60 k, 120 k drawn stars with the same
> view and `Fov=` confirming foveation is on. Two regressions fall out: ms per 1 000 drawn stars
> (fill, since quad area scales with N at the floor) and ms per 100 k vertices (from the N = 0 case
> with the mesh still resident versus absent). Same shape as Fractarium's own governor probe.

---

## 12. The failure modes this project has already paid for

| # | Failure | Where recorded | How this design cannot repeat it |
|---|---|---|---|
| 1 | Point spread summed to **1.40** and clamped: a flat top, and a small disc is a square | `feedback.md` 1.7.0 (**V**) | one term, peak exactly 1.0, nothing sums. The only compression is `c/(1+c)`, whose derivative is never zero, so no iso-contour is ever flat. Gate: plateau ≤ 2 px (§14) |
| 2 | 64 px texture on a 4 px quad landed in a 4×4 mip; a magnified 4×4 *is* a grid of blocks | `feedback.md` 1.7.0, `StarSprite.cs` (**V**) | **no texture**. Gate: G0 grep — nothing under `Runtime/Layers/Stars/**` may construct a `Texture2D` |
| 3 | Brightness routed through an alpha channel the shader attenuated; quads enlarged to compensate for a signal that never arrived | `feedback.md` 1.7.0, `StarLook.cs` (**V**) | radiance is `o.colour.rgb`; the fragment returns alpha **0**; the blend is `Zero One`. Gate: `ProbeDestinationAlpha` with `COSMOSYNTH_HIDE=stars` must move coverage by 0 — already measured 0 once, 2026-07-31 (**V**) |
| 4 | `pow(u, 3.2)` put almost every star at the size floor; a one-pixel feature has no shape | `CLAUDE.md` (**V**) | size is no longer a brightness channel. Every faint star is the same size *on purpose*, and that size is 5 px because 5 px is where sub-pixel sampling stops being a lottery (§3.3) |
| 5 | A minimum apparent size with no maximum: a 3 m lander inflated past Mars, black screen | `CLAUDE.md` (**V**) | `A_max` is *derived* from `halfMax`, so the pair cannot disagree; a round-trip test asserts it |
| 6 | Contract A.4 crops its Gaussian at 1/16 of peak on every quad edge — a soft square, at every magnitude | found in this pass (**V**, §1.4) | `uv` in core radii; the crop is placed where the profile has already reached ε |
| 7 | Contract A.4 gamma-encodes the brightness: `CS_DisplayToLinear(tinted * level)` | found in this pass (**V**, §1.5) | decode the bare `v.color.rgb`; radiance multiplies afterwards. Grep-checkable |
| 8 | Vertex colours read as raw sRGB bytes in a Linear project — washed out, and indistinguishable from a double decode | BoltGraphXR `BoltVertexColor.shader:59` (**V**) | exactly one `CS_DisplayToLinear` per shader, on a value with no brightness in it, and the *linear* table is the authority (§5.1) so a missing or doubled decode is a measurable colour error, not a taste |
| 9 | Bloom silently does nothing to additive layers when `_ENABLE_ALPHA_OUTPUT` is on | found in this pass (**V**, §8.1) | the layer does not use post bloom. Survives either resolution |
| 10 | A "verification gap": the check passes for a reason unrelated to its name | `docs/` and the memory note of the same name | §13 states what the box can and cannot prove, per assertion, and §14's gate prints numbers rather than showing a picture |
| 11 | Whole-file drift between two populations answering "how bright is this dot" in different units | `StarLook.cs` class remarks (**V**) | there is one population and one law. The 146 sky rows run the same shader with `_Space = 2` |

---

## 13. What this box can prove, and what only a headset can

**Provable headlessly**, on llvmpipe, flat, mono, through `PlayModeShot`:

| claim | how |
|---|---|
| a star is round, has no plateau, and is radially monotone | §14's neighbourhood gate on a capture |
| flux ratios are exact | §14's pair test: the peak ratio of two stars of known magnitude must equal 10^(−0.4Δm) to 2 % |
| quad size matches the formula | measure the extent of the above-cut mask; predict from the capture's own reported `PixelsPerRadian` (the harness prints it, §11.3 `stereo`), never from 1140 |
| nothing writes alpha | `ProbeDestinationAlpha` + `COSMOSYNTH_HIDE=stars` (existing) |
| the colour of a named star matches the §5.1 table | decode the capture's sRGB, normalise, compare chromaticity |
| star counts at a given `m_cut` | the report's per-layer line vs the catalogue's cumulative counts |
| the amplitude/quad round trip, the histogram governor, the Teff table | Core tests, no editor, in the 2-second suite |
| every stereo macro is present, the blend is `One One, Zero One`, no `GetCameraPositionWS`, no fragment object space | G0b greps, which already exist |

**Only a headset can settle these.** Each is one number and each gets one line in
`docs/reports/handoff-<ver>.md`:

| # | Question | Knob | Experiment |
|---|---|---|---|
| H-S1 | Is ε = 0.003 the right cut **over a lit room**? A magnitude-6.5 star peaks at sRGB 23/255; passthrough composites that over furniture, not over black | `_Photometry.w` | wear it in a normally-lit room and in the dark; count whether the faint majority reads as pinpricks or as nothing. `StarLook`'s own history says 0.16 (gamma) was below perception and 0.40 was not — those numbers do not transfer to Linear and must be re-taken |
| H-S2 | Does the periphery thin out and shimmer under SRP foveation (§9)? | `_Psf.w` (k) | capture the same view at FFR off / Low / Medium; count detected stars in the outer 30 % of the frame against the inner 30 %. Sweep k ∈ {0, 0.5, 1, 2} and pick the smallest that closes the gap |
| H-S3 | Do 5 px stars scintillate with normal head motion? | α, `_Psf.x` | stand still, then rotate slowly; watch the faint field. If it crawls, α goes to 2.6 px and the fill budget absorbs 40 % more |
| H-S4 | Is Sirius at 36 px convincing, or does it want β = 1.786? | β | look at Sirius, Canopus and Vega against memory of the real sky |
| H-S5 | Do the spikes read as glare or as a decoration? | `_Spike.z`, `_Spike.w` | the onset is in amplitude, so check it at three exposures |
| H-S6 | Does 0.19 px of disparity between the volume and the sky layer read as one sky? (§7) | d₀ | already H4 in §11.6; this adds the star-specific case |

The rule from §6 of the handover applies verbatim and goes in the notes every time: a flat mono
capture on llvmpipe cannot prove stereo, passthrough, frame rate or foveation.

---

## 14. The headless test

Named `tools/gates/starshape.py`, self-tested with a deliberately broken input like every other
gate here. It reads a PNG from `PlayModeShot` and a JSON sidecar naming the expected stars.
`numpy 2.5.1` and `PIL 12.3.0` are installed on triton (**V**).

**It prints the pixel neighbourhood. It never shows a thumbnail** — the failure and the fix are
indistinguishable at 1:1, which is how the square stars survived the fix that was meant to cure
them (**V**, `CLAUDE.md`).

Four assertions per star, on the sRGB-decoded capture:

1. **Plateau** — pixels within 2 % of the peak must number ≤ 2. A clipped flat top gives 3 or more
   (**V**: the 1.7.0 profile, re-simulated, gives 3).
2. **Radial monotonicity** — within the inscribed disc `r ≤ halfPx`, no pixel farther from the
   centroid may exceed a nearer one by more than 2 % of the peak. (Restricting to the inscribed
   disc matters: at the 5 px floor the square crop puts corner pixels outside the disc and they
   legitimately break monotonicity — 6 violations, **V**.)
3. **Axis/diagonal symmetry** — the mean value in an annulus at the half-peak radius, along the
   four axes versus the four diagonals, must agree within 15 %. A square patch fails this by
   construction; a clipped quad edge fails it the other way.
4. **Photometry** — for a *pair* of faint stars with no spikes, the ratio of peaks must equal
   `10^(−0.4Δm)` within 2 %. This works because the core width is a constant, so the peak is
   linear in flux: predicted 3.9811 for Δm = 1.5, measured 3.9811 (**V**, §14's golden data).
   Run this view with `_Look.y = 0` and post-processing off; the integrated ratio is *not* invariant
   (the quad grows), and the analytic prediction for it is in §3.2 if it is ever wanted.

Assertions 1–3 hold **through a tone curve**, because any monotone per-channel transform preserves
iso-contours; assertion 4 does not, which is why it gets its own view.

**Golden output** for `α = 2.2, ε = 0.003, A_min = 0.009, m_cut = 6.5`, star centred at
(+0.17, +0.31) px, in-shader tone on, printed as sRGB bytes (**V**, computed on triton):

```
m = 6.5   A = 0.0090   half = 2.50 px  quad =  5.00 px        m = 2.0   A = 0.5679   half = 7.86 px  quad = 15.72 px
   0   0   0   0   0   0   0   0   0                            22  28  35  40  42  41  37  30  24
   0   0   0   0   0   0   0   0   0                            28  38  49  59  63  61  53  42  32
   0   0   3   5   7   6   4   0   0                            35  50  67  85  95  90  74  55  40
   0   0   5  11  15  13   7   0   0                            42  61  87 116 133 124  97  69  47
   0   0   7  16  23  19  10   0   0                            45  67  99 137 160 148 112  77  51
   0   0   7  15  21  17   9   0   0                            44  65  96 132 153 142 108  75  50
   0   0   5   8  12  10   6   0   0                            39  57  80 104 118 111  88  64  45
   0   0   0   0   0   0   0   0   0                            33  45  60  74  81  78  65  50  37
   0   0   0   0   0   0   0   0   0                            26  34  43  51  54  53  46  37  29
   peak linear 0.00848   plateau 1                              peak linear 0.35049   plateau 1

m = 4.0   A = 0.0900   half = 4.66 px  quad =  9.31 px        m = -1.44 A = 13.4972  half = 17.88 px quad = 35.77 px
   4   6   9  11  12  12  10   7   5                           111 129 146 158 163 160 150 135 117
   6  10  16  20  23  21  17  12   7                           130 154 175 190 197 194 181 161 138
   9  16  25  33  39  36  28  19  11                           147 176 202 218 225 222 208 186 157
  12  21  34  50  61  55  40  25  15                           161 193 220 235 241 238 226 203 172
  14  24  41  63  79  71  48  29  17                           167 201 228 242 247 245 234 211 179
  13  24  39  60  74  66  46  28  16                           166 199 226 241 245 243 232 209 177
  11  19  31  44  52  48  35  23  13                           156 188 214 230 236 233 221 198 167
   8  14  21  28  32  30  23  16  10                           141 168 192 209 216 212 199 177 150
   5   9  13  16  18  17  14  10   6                           123 144 164 178 184 181 170 151 130
   peak linear 0.07879   plateau 1                              peak linear 0.92767   plateau 2
```

(Sirius's block is the inner 9×9 of a 36 px quad, which is why it does not fall to zero at the
edge; its own crop is at 17.9 px.)

This is what a star looks like when it is one. Compare it with a square, whose rows would read
`23 23 23 23 23` and whose corners would equal its edges.

---

## 15. Implementation plan for the `stars` lane

Ordered so each step is separately testable and nothing waits on the image pipeline.

1. **Core, no engine.** `Core/Sky/Photometry.cs`: `AmplitudeFor(m, mCut, aMin)`,
   `HalfPixels(A, eps, alphaPx, minHalf, maxHalf)`, `MaxAmplitude(eps, alphaPx, maxHalf)`,
   `LinearChromaticity(teffK)` (the 64-entry log-Teff table of §5.1) and
   `DisplayChromaticity(teffK)` (its sRGB encode). Tests: the `A_max` round trip to 0.01 px; the
   5 778 K row against the table; the Sun's M<sub>V</sub> 4.83 reproducing the catalogue value;
   `AmplitudeFor(m+1)/AmplitudeFor(m) == 10^-0.4` to 1e-6. Runs in the existing 2-second suite.
2. **`StarLook.cs` rewritten** as the one place the constants live and the one place that writes
   them into a material. Delete `FaintestQuadDmm`, `BrightestQuadDmm`, `FaintestBrightness`,
   `QuadDmm`, `BrightnessFromMagnitude`. Keep the class remarks — they are the evidence trail —
   and re-point them at this document.
3. **Delete `StarSprite.cs`.** Add the G0 grep that keeps it deleted.
4. **`Point.shader`** per §10, into `lane/foundation-shaders`. Compile in the one editor, run G0b
   for the six macros and the blend, capture one frame of a synthetic five-star card.
5. **`PointChunks` extensions**: the per-chunk 16-bin absolute-magnitude histogram (32 B) and
   `minAbsoluteMagnitude` (4 B). Test: the histogram sums to the chunk's point count, and a
   bisection on it recovers a known `m_cut` to 0.1 mag.
6. **`Runtime/Layers/Stars/StarLayer.cs`**: build the chunk meshes at 24 B/vertex (§6.2), run the
   cone cull and the `m_cut` governor, publish `BRIGHTNESS ×N`, honour SOLO and `_Fade`, and
   rewrite `data.w` on `BodiesChanged` for de-duplication. Extend `PointChunksTests` to assert the
   fourfold equality of `data.w`, `data.x` and `COLOR`.
7. **The 146 sky rows** as a second renderer with the same material family and `_Space = 2`, faded
   by `ViewerFromSunPc`.
8. **`tools/gates/starshape.py`** (§14) plus its self-test: feed it a synthetic square patch and a
   synthetic flat-top and require both to fail, then the golden tiles above and require them to
   pass. A gate that has never failed is not a gate.
9. **Wire the capture**: a `layer:stars` solo view and a `star:<n>` view in `PlayModeShot` that
   forces `_Look.y = 0` and post off, so assertion 4 has somewhere to run.
10. **Hand off** with `docs/reports/handoff-<ver>.md` carrying §13's six headset questions, each
    with the knob, the file and the line.

Steps 1–3 and 5–6 have no dependency on `lane/foundation-shaders`; step 4 does. Nothing depends on
the image-pipeline topic, by construction (§8).

---

## 16. Open numbers

| | Item | Status | What settles it |
|---|---|---|---|
| O1 | GPU ms for the layer | **I**, 0.3–0.6 ms guessed against a 1.2 ms allocation | E1 (§11) |
| O2 | ε = 0.003 over passthrough | **I** | H-S1 |
| O3 | Foveation compensation k | **I**, default 1.0 | H-S2 |
| O4 | α = 2.2 px vs scintillation | **V** for sampling, **I** for temporal stability | H-S3 |
| O5 | β = 2 (exponent 0.25) vs the atlas's 0.28 | **V** that they differ by 1.24× on Sirius alone | H-S4 |
| O6 | `N_target` = 60 000 | **V** arithmetic, **I** that it is the right aesthetic density | a headset, and the `QUALITY REDUCED` chip firing or not |
| O7 | Desaturation window (3.5, 1/3) | **I** | H-S1's session; it cannot change any brightness (§5.3) |
| O8 | `PixelsPerRadianCentre` 1 140 | **I** (contract §2.0, R8) | the G1c `stereo` assertion reports the measured value; every px figure in this document scales with it linearly, and the pixel *ratios* in §3.3 and §14 do not move at all |

---

### Amendments this document asks the integrator to accept

1. Appendix A.4 is replaced by §10. The behavioural changes are: `uv` in core radii; a Moffat
   profile; flux-true amplitude; the decode-order fix; the derived quad; the amplitude cap; the
   analytic spikes; the fovea term; the in-shader tone switch.
2. A.4's vertex layout loses `shape` and becomes 24 B (§6.2). `_MagWindow`, `_SizeDmm`,
   `_PxBounds`, `_Shape` and `_ShapeGain` are replaced by `_Photometry`, `_Psf`, `_Spike` and
   `_Colour`.
3. §7.3's `stars` row keeps every ceiling; the fragment figure is now computed rather than
   inferred (0.078 from Earth, 0.329 at the governor's setpoint).
4. §5.7.5 rule 9's "shed star vertices" becomes "raise `m_cut`" (§6.3).
5. G1c gains two rows: `_Look.y == 1` iff the URP asset's HDR is off (§8.3), and `starshape`
   (§14).
6. §13's INFERRED list gains O1–O7 above.
