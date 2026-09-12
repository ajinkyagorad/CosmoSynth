# Planets that are lit, and air that is air

Design pass for `HANDOVER-ASTRA-VISUAL.md` items 4 and 5. Written 2026-09-12 against `master`
at `e72d89a`, `lane/foundation-code` at `6f19d5d`, `lane/foundation-shaders` at `6e0e3be`.

**Nothing here was compiled or run.** It is a specification: maths, constants with their
derivations, shader source, costs with the arithmetic shown, and an ordered plan. Every claim
is tagged **VERIFIED** (I read the file, ran the arithmetic, or measured the pixels) or
**INFERRED** (reasoned, needs a check — the check is named).

The one sentence: **the atmosphere is additive-only, so it can add light to a planet but never
take any away, and every other defect in this topic follows from that or from a constant that
was tuned by eye instead of derived.** Fixing it costs zero new draw calls and zero new
vertices.

---

## 0. What the numbers say about the current frame

Measured by me on the box from `/tmp/cs-baseline/app-running.png` with Pillow
(`/tmp/cs_m3.py`, read-only), plus `/tmp/cs-baseline/app-running.report.txt`.

| Quantity | Value | How |
|---|---|---|
| Earth shell screen radius | **431-446 px** at 1920x1080 | VERIFIED: circle fits through the chords at y=600 (half-width 306), y=700 (380), y=800 (421), y=1000 (439); centre x = 957 exactly. The 3% spread is the edge threshold finding a dimmer limb on the night side |
| Implied focal length | **1060-1100 px**, vertical FOV **53-54 deg** | VERIFIED: from `rWorld=0.2793`, `d=0.7414` and `r_px = f R / sqrt(d^2-R^2)` = `0.40668 f` |
| Planet silhouette radius | **423-438 px** | VERIFIED: same projection with `rWorld=0.275`, `= 0.39941 f` |
| Geometric gap, planet to shell | **7.7-8.0 px** | VERIFIED: `R_shell * (1 - 0.39941/0.40668)` = `0.01787 R_shell`, insensitive to which fit you take |
| Width of the visible teal band | **36-50 px** | VERIFIED: y=600 left edge 651 -> surface at ~701; y=800 right edge 1376 -> surface at 1340 |
| Ratio | **4.5-6.5x wider than the shell** | VERIFIED |
| Outermost band pixel | rgb(33, 80, 103) | VERIFIED at y=600, x=653 |
| Shader output there (Gamma space, so framebuffer = shader) | (0.129, 0.314, 0.404) | VERIFIED: /255 |
| Ratio R:G:B of that output | 1 : 2.43 : 3.13 | VERIFIED |
| Ratio R:G:B of the optical depth driving it | 1 : 2.33 : **5.71** | VERIFIED: tau (0.049, 0.115, 0.281) from the report |

Read those last two rows together. Blue is carrying only 3.1x red where the physics says 5.7x.
**The blue channel has saturated and the hue has collapsed toward grey-teal.** That is the
"opaque teal band" in one number, and it is not a tuning problem.

Two more things the frame proves:

- The band is *inside* the disc. Only 7.7-8.0 px of it can be outside the silhouette; the other
  30-42 px is atmosphere drawn over the planet's own near-limb surface, where the surface is
  grazing-lit and dim, so an additive glow wins. The band has a hard inner edge because the
  view path through the shell halves discontinuously across the silhouette (full chord
  `2 sqrt(Rs^2 - b^2)` outside, near half `sqrt(Rs^2-b^2) - sqrt(Rp^2-b^2)` inside). That step
  is physically real; it only reads as a seam because the surface behind it is not being
  attenuated to match.
- **`worldScale=1` on Pluto is not a scaling bug.** `AtmosphereShell.LateUpdate` returns at
  `if (!visible) return;` *before* it writes `transform.localScale`, while `Describe()` prints
  `transform.lossyScale.x` with no visibility guard. Pluto's shell has never run a sizing pass
  since it was created, so it still carries the primitive's scale of 1. The other eight report
  stale values from the last frame they were drawn — which is why all eight print the *same*
  `planet r=3.131`, the minimum-visible clamp at sky distance, and why Earth (the one body
  actually drawn) is the only one that is self-consistent (`0.2793 * 2 = 0.5586` exactly).
  VERIFIED by reading `AtmosphereShell.cs:106-160` against the report. The fix is to the
  diagnostic, not the shell: print `IsGeometryDrawn` and the words `not drawn` instead of a
  transform nobody updated.

---

## 1. The catalogue's scattering coefficients are not what they claim to be

`tools/cosmodata/bodies.py` marks every `rayleighScatteringPerKm` **Measured** with a NASA fact
sheet as the source. The fact sheets do not publish Rayleigh coefficients. I checked the nine
rows against the physics using only numbers that *are* in the same table.

Rayleigh scattering scales as `N * (n-1)^2 * F_King`, where `N` is number density and `n` the
refractive index of the mixture. Every input is already in `ATMOSPHERES`:
`N = surfaceDensityKgPerM3 / meanMolecularMassKgPerMol`, and `(n-1)` mixes by the volume
fractions in `composition`.

`N` in mol/m^3 (VERIFIED arithmetic from the shipped table):

| Body | rho | M | N | N / N_Earth |
|---|---|---|---|---|
| Earth | 1.225 | 0.028964 | 42.29 | 1.000 |
| Venus | 65.0 | 0.0434 | 1497.7 | 35.42 |
| Mars | 0.020 | 0.04334 | 0.4615 | 0.01091 |
| Jupiter | 0.16 | 0.00223 | 71.75 | 1.697 |
| Saturn | 0.19 | 0.0023 | 82.61 | 1.953 |
| Uranus | 0.42 | 0.00264 | 159.1 | 3.762 |
| Neptune | 0.45 | 0.00269 | 167.3 | 3.956 |
| Titan | 5.4 | 0.02801 | 192.8 | 4.559 |
| Pluto | 1.0e-5 | 0.02801 | 3.570e-4 | **8.44e-6** |

Refractivities `(n-1) x 1e4` at STP, 550 nm (standard values; INFERRED from memory, the lane
should pin them from a handbook): air 2.779, N2 2.98, O2 2.72, CO2 4.49, H2 1.36, He 0.35,
CH4 4.41, Ar 2.81, CO 3.34. King factors: air 1.048, N2 1.034, CO2 1.15, H2 1.00.

Then `beta_R(body) / beta_R(Earth) = (N/N_E) * ((n-1)/(n-1)_air)^2 * (F/F_air)`:

| Body | mixture (n-1) x1e4 | ((n-1)/air)^2 | ratio to Earth | **derived** beta_R green (/km) | **shipped** | shipped / derived |
|---|---|---|---|---|---|---|
| Venus | 4.437 | 2.549 | **99.0** | 1.337 | 0.0155 | **1/86** |
| Mars | 4.409 | 2.517 | **0.0301** | 4.07e-4 | 0.0045 | **11x** |
| Jupiter | 1.257 | 0.2046 | 0.331 | 4.47e-3 | 0.0105 | 2.3x |
| Saturn | 1.321 | 0.2260 | 0.421 | 5.69e-3 | 0.0100 | 1.8x |
| Uranus | 1.277 | 0.2110 | 0.757 | 1.02e-2 | 0.0110 | 1.1x |
| Neptune | 1.271 | 0.2091 | 0.789 | 1.07e-2 | 0.0105 | 1.0x |
| Titan | 3.047 | 1.2023 | 5.39 | 7.27e-2 | 0.0130 | **1/5.6** |
| Pluto | 2.989 | 1.1569 | **9.60e-6** | 1.30e-7 | 0.0090 | **69,000x** |

Earth's own triple `(0.0058, 0.0135, 0.0331)` is the textbook (680, 550, 440) nm set and is
correct: my own closed-form evaluation gives `(4.66e-3, 1.089e-2, 2.659e-2)`, a uniform 1.245x
below the shipped values with **exactly** the `lambda^-4` ratio structure — a different
normalisation constant, not a different physics (VERIFIED arithmetic).

What this changes visually, and why each is right:

- **Venus** becomes genuinely opaque (vertical `tau = beta*H = 1.337*15.9 = 21.3` in green,
  52 in blue) — a featureless yellow-white ball with no surface. That is what Venus looks like.
  Today `AtmosphereShell.Scatter` clamps `tau` at 4.0 "so Venus stays yellow-grey", which is a
  workaround for the wrong number.
- **Mars** loses its Rayleigh almost entirely (`tau = 4.5e-3`) and is left with its Mie term
  (`0.030 * 11.1 = 0.333`), which is squarely in the published clear-season dust opacity range
  of 0.3-0.9. **Mars's sky is dust, not air**, and the derivation says so by itself.
- **Titan** gains a strong Rayleigh blue (`tau = (0.656, 1.528, 3.745)`) under its orange Mie
  haze — which is exactly the Cassini picture: an orange body with a famously *blue* limb. The
  shipped row inverts it by putting the orange in the *scattering* coefficient.
- **Pluto** all but vanishes in Rayleigh (`tau ~ 1e-5`). Its real appearance — a thin blue ring
  in backlit New Horizons frames — is forward-scattered haze, i.e. the Mie term, which the
  table already carries.
- **Uranus and Neptune** are within 10% for green and blue but the shipped *red* is deliberately
  suppressed (Uranus 0.0020 against a derived 4.4e-3) to fake methane absorption. That is
  physically wrong in a way that matters: absorption removes light without scattering any in,
  so it must be a separate extinction term. Add `absorptionPerKm` (RGB) to
  `AtmosphereDefinition`, apply it in the transmittance and **not** in the source function.

**Recommendation for the `solar` lane** (it owns `bodies.py`): compute `rayleighScatteringPerKm`
from `surfaceDensityKgPerM3`, `meanMolecularMassKgPerMol` and `composition` at import time,
anchored on Earth's shipped triple, and re-label the field **Inferred** —
"computed from measured surface density and composition; Rayleigh cross-sections from
refractivity". Keep `mieScatteringPerKm` as it is but re-label it **Inferred** too: those
numbers read as authored-for-look and no fact sheet contains them. Add `absorptionPerKm` for
the two ice giants and Titan.

**The test that settles it** (Core, engine-free, runs in `run-tests.sh`): recompute the ratio
table above from the shipped `ATMOSPHERES` dict and assert every body is within 2x of its
derived value. It fails today on five of nine rows, which is what makes it a gate worth having.

---

## 2. The one structural change: the atmosphere must be able to darken the planet

The shader header says it plainly, and `SkyAndRings.StarIntensityFor` says it again in a
comment I read at `:766-774`:

> "This pass only ever ADDS light — it has no way to attenuate the surface underneath it, which
> a real atmosphere does. So the honest ceiling is well below the physical scattered radiance,
> or the two brightnesses stack and a blue marble comes out as a flat cyan disc. At 8 ..."

So the reference intensity is 8 instead of the physical value, and everything downstream is
under-driven except the limb, which saturates anyway. That is the whole defect.

**Arithmetic for why it cannot be tuned out.** Earth, `tau = (0.049, 0.115, 0.281)` vertical
(VERIFIED, from the report). Limb airmass at the surface tangent is
`sqrt(pi R_p / (2 H)) = sqrt(pi * 6378.137 / 17) = 34.33` (VERIFIED). So:

| Geometry | tau (R,G,B) | transmittance exp(-tau) |
|---|---|---|
| face-on (sub-observer point) | 0.049, 0.115, 0.281 | 0.952, 0.891, **0.755** |
| limb (tangent to the surface) | 1.69, 3.94, **9.66** | 0.184, 0.0195, **6.4e-5** |

At the limb the surface must **disappear** — 99.99% of its blue is gone before it reaches the
eye. Today it cannot: additive blending leaves the Blue Marble texture at full strength and
piles a saturated glow on top. That is the hard inner edge I measured.

Face-on it matters just as much, in the other direction. The ocean's own albedo is about 0.06
and nearly grey; the single-scattered blue at full phase is
`tau_B * P_R(180 deg) = 0.281 * 0.1194 = 0.0336` of the solar irradiance against
`0.06 * 0.755 = 0.045` of transmitted ocean — comparable. **Most of the blue in "blue marble"
is the air, not the water.** The current model throws that away and leans on the texture.

### 2.1 How to get absorption without breaking the passthrough rule

Two candidates. Take the second.

**Rejected: change the blend.** `Blend One OneMinusSrcAlpha, Zero One` would attenuate the
destination while leaving framebuffer alpha untouched, and outside the silhouette (dst = 0) it
degenerates to exactly the additive result — safe in passthrough. But `OneMinusSrcAlpha` is one
scalar, and the whole point is that blue is absorbed 5.7x harder than red. Per-channel
absorption needs `OneMinusSrcColor`, and then the same three numbers would have to be both the
added light and the removed light. It does not work, and it would amend the contract's queue
table (§2.2 row 3100) for nothing.

**Chosen: the body applies its own transmittance.** `CosmoSynth/Body` computes the air column
between the surface fragment and the eye, and between the surface fragment and the star, and
multiplies. The atmosphere pass stays exactly `Blend One One, Zero One` and returns alpha 0, so
§7.1 rule 4, the §2.2 queue table and the G1c `alpha` assertion are all untouched. The cost is
one texture fetch per body fragment, shared with the same lookup table the atmosphere pass
already needs.

This is also the only version that is correct in Envelope mode, where the body is
per-vertex-compressed and the atmosphere is integrated in true eye-relative space: both read
the same table with the same parameterisation.

---

## 3. The lookup table

One 2D table per body with an atmosphere. It replaces the inner 4-sample light march, which is
the expensive and wrong part of today's shader (it steps over the planet at grazing angles and
lights the night side).

### 3.1 What it stores

For a spherically symmetric exponential atmosphere, the **column integral** of density along a
ray, from a point at radius `r` in a direction with cosine-zenith `mu`, out to the top of the
atmosphere:

```
D_R(r, mu) = integral_0^L exp( -( |x(s)| - Rp ) / H_R ) ds
D_M(r, mu) = the same with H_M
```

Two channels, one `RGHalf` texture. The optical depth for any segment is then a difference of
two lookups, and the transmittance is `exp(-(beta_R*D_R + beta_M*D_M + beta_A*D_M))` with
`beta_A` the new absorption term riding the Mie profile.

**Everything is in units of the planet radius.** `Rp = 1`, `Ra = 1 + h_top/Rp`,
`H_R' = H_R/Rp`, `beta' = beta * Rp`. All four are dimensionless constants per body, set once at
construction and never touched again. This is the structural cleanup: today `_ScaleHeight` is
"fraction of shell thickness", so the shell's geometry and the physics are entangled and the C#
divides the entanglement back out every frame (`AtmosphereShell.cs:148-158`). In planet radii
there is nothing to divide. The only per-frame uniforms left are the drawn centre and the drawn
radius — and that is exactly the pair the §2.6 group rule already gives you, in both Rigid and
Envelope mode, from one code path.

### 3.2 Parameterisation

Naive `u = mu` wastes almost all the table: everything interesting happens within a few degrees
of the horizon. Use the distance-to-boundary mapping, which I derive here rather than cite so
the lane can check it:

```
rho   = sqrt(r^2 - Rp^2)          distance from r to its own horizon tangent point
H_geo = sqrt(Ra^2 - Rp^2)         the same quantity at the top of the atmosphere
x_r   = rho / H_geo                                        in [0, 1]

d      = -r*mu + sqrt(r*r*mu*mu - r*r + Ra*Ra)             distance along mu to the top
d_min  = Ra - r                    straight up: the shortest ray to the boundary
d_max  = rho + H_geo               grazing the planet: the longest ray to the boundary
x_mu   = (d - d_min) / (d_max - d_min)                     in [0, 1]
```

`d_min` and `d_max` are self-evident from the geometry and the inverse is closed-form, so this
needs no external authority. The `x_r` mapping concentrates samples where the density is: for
Earth, `H_geo = sqrt(6478^2 - 6378^2) = 1133.9 km`, and the first of 32 rows spans altitude
`(1133.9/32)^2 / (2*6378) = 0.098 km` — 98 metres. The top row is 100 km up where density is
`exp(-100/8.5) = 7.4e-6`. VERIFIED arithmetic.

### 3.3 Size, format, and where it comes from

| | |
|---|---|
| Resolution | **128 (x_mu) x 32 (x_r)** |
| Format | `TextureFormat.RGHalf`, `linear: true`, no mips |
| Bytes per body | 128 * 32 * 4 = **16,384 B = 16 KB** |
| Nine bodies | **144 KB** of runtime texture |
| Bytes in the APK | **0** |

It is a pure function of six catalogue numbers, so **build it on the CPU at load and ship
nothing**. Build cost: 128*32 texels x 32 integration steps = 131,072 evaluations per body,
1.18 M for all nine, each a `sqrt` and an `exp`. That is milliseconds, and it belongs in
`Core/` (engine-free — it takes six doubles and returns a `float[]`), which means it is covered
by the 142-test suite with a known answer: the vertical column must equal
`H * (1 - exp(-h_top/H))` to 0.5%, and the horizontal column at the surface must equal
`H * sqrt(2*pi*Rp/H)` to 2%. Both are closed forms. That is the test that makes the table a
gate rather than an artefact.

Interpolation error: linear interpolation of an exponential sampled at 0.37 e-folds per step is
about 0.6% (VERIFIED: `(cosh(0.185)-1) ~ 0.0171` relative curvature error over a half-interval,
which is the standard bound). Under the 1/255 a pixel can show.

**Do not precompute the in-scattering as well, yet.** It is tempting: because the phase
functions depend only on the view-sun angle `nu` and nothing else in the single-scattering
integral does, they factor out exactly —

```
I_single(r, mu, mu_s, nu) = P_R(nu) * S_R(r, mu, mu_s) + P_M(nu) * S_M(r, mu, mu_s)
```

— so a 3D table in `(r, mu, mu_s)` gives **exact** single scattering with one fetch and zero
marching. At 32 x 64 x 32 `RGBAHalf` that is 512 KB per body, 4.6 MB for nine, and 18.9 M CPU
evaluations to build. It is the upgrade path if the cost probe in §8 says the 16-step march is
too expensive. It is not the first thing to build, because the 2D table already removes the
expensive inner loop and the march is what makes the code readable.

---

## 4. (a) The body material

### 4.1 What changes, and why each constant is what it is

**Terminator softness.** `_Terminator = 0.05` is a wrap-lighting fudge. The physical penumbra
half-width in `cos(theta)` is the star's angular radius seen from the body, `R_star / d_star`:

| Body | d (km) | `R_sun/d` | vs the shipped 0.05 |
|---|---|---|---|
| Earth, Moon | 1.496e8 | **4.651e-3** | 10.7x too soft |
| Mars | 2.279e8 | 3.053e-3 | 16x |
| Jupiter | 7.785e8 | 8.936e-4 | 56x |
| Saturn | 1.434e9 | 4.852e-4 | 103x |
| Neptune | 4.495e9 | **1.548e-4** | **323x** |

VERIFIED arithmetic, `R_sun = 6.957e5 km`. A terminator smeared 300x wider than physics is
exactly what "flat-lit, like painted clay" looks like. Replace the wrap with the closed-form
irradiance from a uniform disc source, which is `C1`-continuous and exact at both ends:

```
half t = _StarAngularRadius;                 // R_star / d_star, per body, per frame
half lit = (ndl >= t)  ? ndl
         : (ndl <= -t) ? 0.0h
         : (ndl + t) * (ndl + t) / (4.0h * t);
```

Check: at `ndl = t` it returns `(2t)^2/(4t) = t` and its derivative is `2(2t)/(4t) = 1`, both
matching `ndl` exactly; at `ndl = -t` both value and derivative are 0. VERIFIED by hand.

The real softener on a body with air is the air, and it arrives for free once §2 lands. For an
airless body (Moon, Mercury) the terminator genuinely is knife-sharp, and MSAA 4x from the URP
asset is what holds it. If G1d shows it crawling, the next step is Oren-Nayar with the lunar
photometric roughness — not a wider wrap.

**Night-side city lights.** The transition must track civil twilight, not a guess. The sun is
at the geometric horizon at `ndl = 0` and at the end of civil twilight (6 deg below) at
`ndl = cos(96 deg) = -0.1045`. So:

```
half night = smoothstep(0.02h, -0.1045h, ndl);   // 0 in daylight, 1 past civil twilight
```

VERIFIED arithmetic. The upper end at `+0.02` (sun 1.1 deg up) keeps the lights from appearing
while the sky is still bright.

Lights must not be multiplied by the star colour or attenuated by the terminator — they are
their own emitters — but they **must** be attenuated by the view-ray transmittance, which is
what makes a city at the limb disappear into the haze.

**What is actually staged for this.** The brief asks whether NASA Black Marble is available.
**It is not staged.** VERIFIED: the only night map on the box is
`data-staging/solarsystemscope-textures/2k_earth_nightmap.jpg` (2048x1024, sha
`c16fd1bc...`, listed in `docs/licence/solar-system.md:302`), which is **CC BY 4.0 from Solar
System Scope, a derived work after NASA imagery** — attribution is mandatory and its own
`NOTES.md` says the provenance label must read "Solar System Scope, after NASA imagery", never
"NASA". Under D1 (`sideload-nc`) it is usable today; for any store listing the credit line in
`data-staging/solarsystemscope-textures/LICENCE.txt` must appear verbatim.

Recommendation: stage NASA Black Marble (VIIRS DNB, public domain under the same NASA terms
already quoted in `data-staging/nasa-visibleearth-earth/LICENCE.txt`) so the field can be
labelled **Measured**, and use the Solar System Scope map as the interim with an **Inferred**
label. Either way, do not tint from the map's own colour — it is invented. Store **luminance
only** and tint with one constant.

**Clouds.** Staged and clean: `2k_earth_clouds.jpg`, 2048x1024, same CC BY 4.0 (VERIFIED).

Do **not** add a second sphere for it. A cloud shell is a second draw, a second silhouette that
pokes through the planet's at the limb, and a second `IsGeometryDrawn` consumer to get wrong.
Sample it in the same fragment with a parallax offset. At a 12 km cloud deck on a 6378 km
planet the true parallax is `12/6378 = 0.19%` of the radius — **1.5 px on a 780 px disc**
(VERIFIED arithmetic), visible only on close approach, and a two-line tangent-space offset
covers it:

```
half2 uvCloud = i.uv + _CloudParallax * half2(dot(V, t), dot(V, b)) / max(dot(V, n), 0.25h);
```

Cloud shadow on the ground is the same trick along `L` instead of `V`, one more fetch.

**Rotation.** `LayerTable` row `clouds` reads "= NASA Visible Earth, **static**"
(VERIFIED, CONTRACT §5.4). A drift rate is not in any source, and animating measured imagery at
an invented rate is precisely what the provenance system exists to stop. So: **zero relative
rotation by default**, and expose `cloudDrift` as a `LayerParam` with `IsPresentationGain =
true`, default 0, so any non-zero value raises a header chip through the existing G1c `honesty`
assertion. That is the project's own rule applied without inventing a new mechanism.

**Specular ocean.** The water mask already exists and is already in the right channel:
`PlanetTextureBaker.DeriveWaterMap` classifies open water from the measured Blue Marble pixels
by blue-dominance and a brightness cut, writes it to the **alpha** of a `RGBA32` map, and
`CelestialBodyView.ApplySurfaceTexture` binds it as `_MetallicGlossMap` today (VERIFIED,
`PlanetTextureBaker.cs:217-268`). `CosmoSynth/Body` already samples `_SurfaceMap.a` for
smoothness (VERIFIED, Appendix A.3). So nothing new has to be computed — only used properly.

Replace the `pow(NdotH, exp2(1 + 10*s))` Blinn lobe with GGX and a real Fresnel:

- `F0` for water: `n = 1.333`, so `F0 = ((1.333-1)/(1.333+1))^2 = (0.333/2.333)^2 = **0.0204**`.
  VERIFIED arithmetic. Land is dielectric too; use 0.04.
- Roughness from wind, via Cox and Munk 1954: mean-square sea-surface slope
  `sigma^2 = 0.003 + 5.12e-3 * W`, `W` in m/s. At the global mean ocean wind of ~7 m/s,
  `sigma^2 = 0.003 + 0.0358 = 0.0388`, `sigma = 0.197`. Taking GGX `alpha` equal to the RMS
  total slope gives **`alpha_ocean = 0.20`**. VERIFIED arithmetic; the alpha-equals-sigma
  convention is INFERRED and the check is a G1d capture of the glint at 30 deg phase — if it is
  a hard dot, alpha is too low; if it is a smear across a whole ocean basin, too high.
- Land: `alpha = 0.9`, effectively matte. Sea ice sits between, which the existing mask already
  encodes (`Lerp(0.03, 0.88, water)`).

The whole reason this matters is in the mask's own docstring: a real ocean throws a sun glint
and a painted one does not.

### 4.2 Texture memory — a real problem in the current path, fixed in passing

`Texture2D.LoadImage` decodes to **RGBA32**. Earth's 2048x1024 albedo is therefore
`2048*1024*4 = 8.0 MB`, `+33%` for mips = **10.7 MB**, and the derived water mask is another
**10.7 MB** for a map in which only the alpha channel is ever read. That is 21.4 MB for one
planet against a `planet maps at most 64` row in §7.3 (VERIFIED: the format is chosen at
`PlanetTextureBaker.cs:149` and `:260`).

Adding a night map and a cloud map naively would take Earth to 42.8 MB. Instead:

**Pack them.** One `_SurfaceMap`, `RGBA32` before compression:

| channel | content | replaces |
|---|---|---|
| `.r` | cloud opacity | a separate cloud texture |
| `.g` | spare (reserved for cloud height / shadow bias) | |
| `.b` | night-light **luminance** | a separate night texture |
| `.a` | water mask / smoothness | as today |

Then call `Texture2D.Compress(true)` after `SetPixels32`. On Android that is ETC2_RGBA8 at
8 bits/texel (INFERRED — `Compress()` does not produce ASTC; the check is one `Debug.Log` of
`texture.format` on device): `2048*1024*1 = 2.0 MB`, `+33%` mips = **2.8 MB**, against 32 MB for
three uncompressed maps. **A 29 MB saving and two fewer samplers**, and the `_NightTint`
constant is more honest than the invented colour in the source map.

Compress the albedo the same way: `2048*1024` ETC2_RGB at 4 bits/texel = 1.0 MB, +mips
= 1.33 MB. Earth's upgrade to the staged 5400x2700 Blue Marble then costs
`5400*2700*0.5 = 7.29 MB`, +mips = 9.7 MB — still affordable, and the staged file is already on
the box with its licence cleared.

Per-body texture total after this change: Earth ~12.5 MB with the 5400 map (or ~4.1 MB at
2048), every other atmosphered body ~4.1 MB. Nine bodies: **about 45 MB at 2048 across the
board**, inside the 64 MB row. VERIFIED arithmetic, INFERRED compressed format.

### 4.3 `CosmoSynth/Body` — the fragment shader

This replaces the fragment function in `lane/foundation-shaders`'s `Body.shader`. The vertex
function is unchanged from Appendix A.3 apart from one added varying.

Three contract compliance notes about the branch as it stands, all VERIFIED by
`git grep` on `lane/foundation-shaders`:

1. **`Body.shader:114` calls `GetCameraPositionWS()`**, which §7.1 rule 2 and R1 forbid outright
   — as do `CosmoRim`, `CosmoAtmosphere`, `Point`, `Ribbon`, `Sky` and `Volume` on that branch.
2. **`CosmoCore.hlsl` on that branch has no `CS_EyePositionWS` and no `CS_JitterDir`**; it still
   carries `CS_IGN`, which R7 removed. The branch predates R1/R7/R30.
3. **`Body.shader` declares no `_Fade`**, which R30 requires of every `CosmoSynth/*` shader.
4. `tools/gates/stereo_gate.sh` on that branch greps for neither string, so "stereo gate green"
   in `docs/reports/foundation-shaders.md` is not evidence on these three points.

The code below assumes those are fixed first — it uses `CS_EyePositionWS()` and declares
`_Fade`. **Do not merge this on top of the branch as it stands.**

```hlsl
// ---- additions to the Properties block ----
//   _SurfaceMap  ("Packed (linear): r cloud, g spare, b night, a water", 2D) = "black" {}
//   _NightTint   ("City-light colour (display-referred)", Color) = (1.0, 0.78, 0.50, 1)
//   _NightGain   ("City-light gain", Range(0,2)) = 0.30
//   _CloudTint   ("Cloud colour (display-referred)", Color) = (1,1,1,1)
//   _CloudParallax ("Cloud deck altitude / planet radius", Float) = 0.0019
//   _StarAngularRadius ("R_star / d_star", Float) = 0.00465
//   _OceanAlpha  ("GGX alpha over water (Cox-Munk)", Range(0.02,1)) = 0.20
//   _LandAlpha   ("GGX alpha over land", Range(0.02,1)) = 0.90
//   _AirGeom     ("x: Ra/Rp, y: H_R/Rp, z: H_M/Rp, w: eye radius / Rp", Vector) = (1,0,0,0)
//   _BetaR       ("Rayleigh * Rp (RGB)", Vector) = (0,0,0,0)
//   _BetaM       ("Mie * Rp", Float) = 0
//   _BetaA       ("Absorption * Rp (RGB), rides the Mie profile", Vector) = (0,0,0,0)
//   _OpticalDepth ("Column LUT: r Rayleigh, g Mie, in planet radii", 2D) = "black" {}
//   _RingAxisWS  ("Body north pole, world, unit", Vector) = (0,1,0,0)
//   _RingRadii   ("x: inner/Rp, y: outer/Rp, z: 1 if this body has rings", Vector) = (0,0,0,0)
//   _RingMap     ("Ring profile (r: optical depth, gba: albedo)", 2D) = "black" {}
//   _Fade        ("Layer fade", Float) = 1
//
// One new varying: float3 positionPR — the fragment in PLANET RADII about the drawn centre,
// computed in vert as (positionWS - _PlanetCentreWS.xyz) / _PlanetCentreWS.w. It is computed
// in the vertex stage because the fragment may not read unity_ObjectToWorld (R5), and the
// division is constant work the rasterizer can interpolate exactly (it is affine in WS).

// ---- Column optical depth from the LUT. dirPR is unit; rPR is |x| in planet radii. ----
float2 AirColumn(float rPR, float muPR)
{
    float Ra    = _AirGeom.x;
    float rho   = sqrt(max(rPR * rPR - 1.0, 0.0));
    float Hgeo  = sqrt(Ra * Ra - 1.0);
    float disc  = rPR * rPR * (muPR * muPR - 1.0) + Ra * Ra;
    float d     = -rPR * muPR + sqrt(max(disc, 0.0));
    float dMin  = Ra - rPR;
    float dMax  = rho + Hgeo;
    float xMu   = saturate((d - dMin) / max(dMax - dMin, 1e-6));
    float xR    = saturate(rho / Hgeo);
    return SAMPLE_TEXTURE2D_LOD(_OpticalDepth, sampler_OpticalDepth, float2(xMu, xR), 0).rg;
}

// Transmittance of a column, per channel. Absorption rides the Mie (aerosol) profile.
half3 AirTransmittance(float2 column)
{
    float3 tau = _BetaR.rgb * column.x + (_BetaM + 0.0) * column.y + _BetaA.rgb * column.y;
    return (half3)exp(-tau);
}

half4 frag(Varyings i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    half4 packed = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, i.uv);
    half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;

    half3 nTS = UnpackNormalRGBNoScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
    half3 n = normalize(i.normalWS);
    half3 t = normalize(i.tangentWS.xyz);
    half3 b = cross(n, t) * i.tangentWS.w;
    half3 N = normalize(nTS.x * t + nTS.y * b + nTS.z * n);

    half3 L = normalize(_LightDirWS.xyz);
    half3 V = normalize(CS_EyePositionWS() - i.positionWS);      // THIS eye (R1)

    // ---- Clouds, parallaxed to their deck altitude and shadowing the ground ----
    half cloud = 0.0h, cloudShadow = 0.0h;
    UNITY_BRANCH if (_CloudParallax > 0.0)
    {
        half nv = max(dot(N, V), 0.25h);
        half nl = max(dot(N, L), 0.25h);
        cloud       = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap,
                        i.uv + _CloudParallax * half2(dot(V, t), dot(V, b)) / nv).r;
        cloudShadow = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap,
                        i.uv + _CloudParallax * half2(dot(L, t), dot(L, b)) / nl).r;
    }

    // ---- Terminator: the star is a disc, not a point. C1, exact at both ends. ----
    half ndl = dot(N, L);
    half tA  = max((half)_StarAngularRadius, 1e-4h);
    half lit = (ndl >= tA)  ? ndl
             : (ndl <= -tA) ? 0.0h
             : (ndl + tA) * (ndl + tA) / (4.0h * tA);

    // ---- Ring shadow on the planet. Axis-only, so no matrix (R4) and no object space (R5). ----
    UNITY_BRANCH if (_RingRadii.z > 0.5)
    {
        float3 axis = _RingAxisWS.xyz;
        float  h    = dot(i.positionPR, axis);
        float  la   = dot(L, axis);
        float  s    = -h / (abs(la) < 1e-5 ? 1e-5 : la);
        UNITY_BRANCH if (s > 0.0)
        {
            float3 q  = i.positionPR + s * L;
            float  rr = length(q - dot(q, axis) * axis);
            float  u  = (rr - _RingRadii.x) / max(_RingRadii.y - _RingRadii.x, 1e-6);
            UNITY_BRANCH if (u >= 0.0 && u <= 1.0)
            {
                float tauRing = SAMPLE_TEXTURE2D_LOD(_RingMap, sampler_RingMap,
                                                     float2(u, 0.5), 0).r;
                // The shadow edge is softened by the same disc source as the terminator.
                lit *= (half)exp(-tauRing / max(abs(la), 0.02));
            }
        }
    }

    // ---- Air: the column to the star and the column to the eye, both from ONE table ----
    float  rPR  = length(i.positionPR);
    half3  Tsun = 1.0h, Teye = 1.0h;
    UNITY_BRANCH if (_AirGeom.x > 1.0)
    {
        float3 up = i.positionPR / max(rPR, 1e-6);
        // Sun column from the surface. Below the local horizon there is no path at all.
        float  muS = dot(up, L);
        float  muH = -sqrt(max(1.0 - 1.0 / (rPR * rPR), 0.0));   // horizon cosine at rPR
        Tsun = (muS <= muH) ? 0.0h : AirTransmittance(AirColumn(rPR, muS));
        // Eye column: the air in FRONT of this fragment, = full column minus the part behind.
        float  muV = dot(up, V);
        float2 cSurf = AirColumn(rPR, muV);
        float2 cEye  = (_AirGeom.w > _AirGeom.x)
                     ? 0.0                                         // eye outside: nothing behind
                     : AirColumn(max(_AirGeom.w, 1.0), muV);       // eye inside: subtract its tail
        Teye = AirTransmittance(max(cSurf - cEye, 0.0));
    }

    // ---- Surface response ----
    half3 ground = albedo * (1.0h - cloud) * (1.0h - 0.55h * cloudShadow)
                 + _CloudTint.rgb * cloud;

    half  water  = packed.a;
    half  alphaG = lerp((half)_LandAlpha, (half)_OceanAlpha, water);
    half3 F0     = lerp(0.040h, 0.0204h, water);
    half3 H      = normalize(L + V);
    half  noh    = saturate(dot(N, H)), voh = saturate(dot(V, H)), nov = saturate(dot(N, V));
    half  a2     = alphaG * alphaG;
    half  dden   = noh * noh * (a2 - 1.0h) + 1.0h;
    half  Dggx   = a2 / max(3.14159h * dden * dden, 1e-5h);
    half  k      = alphaG * 0.5h;
    half  Gv     = 1.0h / max((nov * (1.0h - k) + k) * (lit * (1.0h - k) + k), 1e-5h);
    half3 Fr     = F0 + (1.0h - F0) * pow(1.0h - voh, 5.0h);
    half3 spec   = Dggx * Gv * Fr * 0.25h * (1.0h - cloud) * step(0.0h, ndl);

    half3 starE  = _LightColor.rgb * Tsun;
    half3 colour = (ground * starE * lit + spec * starE * lit
                   + ground * _Ambient.rgb) * Teye;

    // City lights: their own emitters, so no star colour and no terminator, but they DO go
    // through the air in front of them, which is what fades a coastal city into the limb haze.
    half night = smoothstep(0.02h, -0.1045h, ndl) * (1.0h - cloud);
    colour += CS_DisplayToLinear(_NightTint.rgb) * (packed.b * (half)_NightGain * night) * Teye;

    colour += albedo * _Emission.rgb;
    return half4(colour * (half)_Fade, (half)_Fade);
}
```

Notes on the shader:

- Every `UNITY_BRANCH` is on a **uniform**, so it is a real branch on the GPU and bodies without
  air, clouds or rings pay nothing. 226 of the 235 bodies take neither air nor ring path.
- No `shader_feature`, no keywords: single variant, as §7.1 rule 1 requires.
- `i.positionPR` is computed in the vertex stage, never in the fragment, so R5 holds.
- `_RingAxisWS` is a `Vector` property, not a matrix, so R4 holds.
- `_AirGeom.w` carries the eye's radius in planet radii and is the **only** thing that changes
  between "outside the atmosphere" and "standing in it" (§6, aerial perspective).

### 4.4 Cost

Texture fetches per body fragment, today vs proposed:

| | today | proposed | when |
|---|---|---|---|
| albedo | 1 | 1 | always |
| normal | 1 | 1 | always |
| surface/packed | 1 | 1 | always |
| cloud, cloud shadow | 0 | 2 | Earth only (1 of 235) |
| optical depth | 0 | 2 | the 9 bodies with air |
| ring profile | 0 | 1 | the 4 bodies with rings |
| **worst case, one body** | **3** | **7** | Saturn (air + rings, no clouds): 6 |

Draw calls added: **0**. Vertices added: **0**. Render passes added: **0**.

The extra fetches land on opaque fragments only. In the `default` view Earth covers
`pi*430^2 / (1920*1080) = 0.28` of the frame (VERIFIED from my measurement); every other body
is at or near the 2.5 px floor. So the added work is 4 extra bilinear fetches over about 0.27
eye-screens, once. I will not invent a texel rate for the Adreno 740: **the cost is INFERRED,
and §8 names the probe.** The order of magnitude is small — these are 2D fetches with high
cache coherence on a smooth surface, and they replace nothing, so the honest statement is that
the Bodies row's 3.0 ms has to absorb them and the probe says by how much.

---

## 5. (b) Atmospheric scattering

### 5.1 The model

Single scattering, Rayleigh plus Mie plus a separate absorption term, marched along the view ray
with the sun column read from the table instead of marched. That is the same model the shipped
shader states in its header; what changes is that the inner loop becomes a lookup, the units
become planet radii, and the result is used to *attenuate* the body as well as add to it.

Phase functions, unchanged and correct in the shipped code:

```
P_R(c) = 3/(16 pi) * (1 + c^2)                                     = 0.0596831 * (1 + c^2)
P_M(c) = 3/(8 pi) * (1-g^2)(1+c^2) / ((2+g^2)(1+g^2-2gc)^1.5)      = 0.1193662 * ...
```

with `c = dot(viewDir, sunDir)` and `g` the catalogue's `MieAsymmetry` (Earth 0.758, VERIFIED).

### 5.2 The step-count governor

Today: 10 view samples, each running a 4-sample inner light march with its own ray-sphere test,
so about **50 density evaluations and 11 sphere intersections per pixel**. Earth's shell covers
0.29 of the baseline frame (VERIFIED: `pi*438^2/(1920*1080) = 0.291`). Counting one density
evaluation as one step, that is `10 * 0.29 = 2.9` view step-coverages, but each step costs
roughly 5x a plain step because of the inner loop: **about 14 effective step-coverages from one
planet**,
against a §7.3 allocation of **3 for all body atmospheres and rings together**. VERIFIED
arithmetic against a VERIFIED coverage measurement. It is 4.7x over budget today, on a frame
with one visible planet.

Proposed, exactly the volume governor from Appendix A.7 with its own smaller budget:

```
N = clamp( round(B_atm / coverage), 8, 24 )      B_atm = 3.0
```

`coverage` is the shell's projected area over both eyes, which the CPU already computes for the
minimum-apparent-size rule. Resulting cost:

| Situation | coverage | N | step-coverages |
|---|---|---|---|
| Earth in `default` (the measured case) | 0.29 | 10 | **2.9** |
| A planet 200 px across on a 2208 px eye | 0.021 | 24 | 0.50 |
| Envelope mode, eye inside the shell | 1.0 | 8 (floor) | **8.0** |

The floor case exceeds the 3 step-coverage row. That is acceptable and provable: you can only
be inside one atmosphere at a time, and the only rungs where that happens are `sky` and `leo`
(`log10 U = +3.000`, §5.1), where the `dust` layer's window (`-19.0 ... -14.6`) and `nebulae`
(`-17.5 ... -13.5`) are both closed, so the shared volume budget `B = 30` is entirely unspent.
Borrowing 5 of 30 while nothing else marches keeps the frame inside 33. State it in the
`LayerBudget` row for `atmospheres` and let the G1c `budget` assertion sum it.

Per step the work is: 1 `exp`, 1 `RGHalf` 2D fetch, one horizon compare, and about 12 ALU. That
is the same class as a dust step (1 trilinear 3D fetch + a few ALU), for which the contract
brackets `kappa` at **0.03-0.10 ms per step-coverage** (INFERRED, §7.3, from Fractarium's
VERIFIED 0.338 ms/step-coverage Mandelbulb-DE upper bound). So:

- normal views: `2.9 * (0.03 .. 0.10)` = **0.09 to 0.29 ms per eye** (INFERRED)
- inside the air: `8.0 * (0.03 .. 0.10)` = **0.24 to 0.80 ms per eye** (INFERRED)

against the 3.0 ms Bodies row. The number that settles it is in §8.

### 5.3 The shader

`CosmoSynth/Atmosphere`, replacing the body of the fragment function. Blend, cull, ztest, queue
and the alpha rule are all **unchanged** — `Blend One One, Zero One`, alpha 0, queue 3100.

```hlsl
// Everything is in PLANET RADII about the drawn centre. Rigid and Envelope are the same code:
//   _Planet.xyz = drawn centre (world), _Planet.w = drawn radius (world)
// and the shader divides once. Nothing else changes between the two modes, which is what makes
// the §2.6 Envelope rule ("integrates in true eye-relative space in units of the planet
// radius") a consequence of the units rather than a second code path.

half4 frag(Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    float3 eyePR = (CS_EyePositionWS() - _Planet.xyz) / _Planet.w;      // R1
    float3 dir   = normalize(i.positionWS - CS_EyePositionWS());
    float3 sunD  = normalize(_SunDirection.xyz);
    float  Ra    = _AirGeom.x;

    float aNear, aFar;
    if (!RaySphere(eyePR, dir, Ra, aNear, aFar)) return half4(0, 0, 0, 0);
    // Reject the duplicate fragment from the far face; unchanged from the shipped shader.
    if (IS_FRONT_VFACE(facing, 0, 1) && aNear > 0.0) return half4(0, 0, 0, 0);

    float start = max(aNear, 0.0);
    float end   = aFar;
    float pNear, pFar;
    if (RaySphere(eyePR, dir, 1.0, pNear, pFar) && pNear > 0.0) end = min(end, pNear);
    if (end <= start) return half4(0, 0, 0, 0);

    int   steps = clamp((int)_Steps.x, 8, 24);        // CPU governor, §5.2
    float dt    = (end - start) / steps;
    // Ray-space jitter, never pixel-space: both eyes rasterise the same world point at
    // different pixels, so a pixel jitter is binocular rivalry (R7).
    float t     = start + dt * CS_JitterDir(dir);

    float3 sumR = 0, sumM = 0;
    float2 depth = 0;                                  // view-ray column, accumulated exactly
    half   tA    = max((half)_StarAngularRadius, 1e-4h);

    [loop] for (int s = 0; s < steps; s++, t += dt)
    {
        float3 p  = eyePR + dir * t;
        float  r  = length(p);
        float  h  = r - 1.0;
        float2 rho = float2(exp(-h / _AirGeom.y), exp(-h / _AirGeom.z)) * dt;
        depth += rho;

        // Sun column from this sample, straight out of the table. One fetch replaces the
        // 4-sample inner march that used to step OVER the planet at grazing angles.
        float3 up  = p / max(r, 1e-6);
        float  muS = dot(up, sunD);
        float  muH = -sqrt(max(1.0 - 1.0 / (r * r), 0.0));
        // Soften the shadow edge over the star's angular radius: the same penumbra the body
        // uses, so the terminator matches across the seam instead of stepping.
        float  vis = smoothstep(muH - tA, muH + tA, muS);
        if (vis <= 0.0) continue;

        float2 cs  = AirColumn(r, max(muS, muH + 1e-4));
        float3 tau = _BetaR.rgb * (cs.x + depth.x)
                   + (_BetaM + _BetaA.rgb) * (cs.y + depth.y);
        float3 att = exp(-tau) * vis;

        sumR += att * rho.x;
        sumM += att * rho.y;
    }

    float c  = dot(dir, sunD);
    float pR = 0.0596831 * (1.0 + c * c);
    float g  = _MieG, g2 = g * g;
    float pM = 0.1193662 * ((1.0 - g2) * (1.0 + c * c))
             / ((2.0 + g2) * pow(abs(1.0 + g2 - 2.0 * g * c), 1.5));

    float3 colour = (sumR * _BetaR.rgb * pR + sumM * _BetaM * pM) * _StarIrradiance;
    // Alpha stays 0: this pass only ADDS. Coverage here is what blacks out passthrough.
    return half4(max(colour, 0.0) * _Fade, 0.0);
}
```

`_StarIrradiance` replaces `_Intensity`. It is `(1 AU / d)^2 * (L_star / L_sun)`, **unclamped** —
the `clamp(8/au^2, 1.2, 26)` in `StarIntensityFor` exists only to stop the additive model
blowing out the disc, and with §2 landed there is nothing to blow out. Exposure and tone mapping
are the image-pipeline lane's job and are where the range belongs. **This is a dependency: do
not tune `_StarIrradiance` before the HDR/tone-map decision lands, or it will be re-tuned.**

### 5.4 Behaviour in the four cases the brief asks about

With Earth's derived numbers (`tau_vert = (0.049, 0.115, 0.281)`, limb airmass 34.33):

- **Face-on.** View column = 1 airmass. `T = (0.952, 0.891, 0.755)` so the disc dims slightly and
  differentially; in-scatter at full phase adds `tau * P_R(180) = 0.281 * 0.1194 = 0.034` of the
  stellar irradiance in blue. Net: the grey-albedo ocean turns blue because of the *air*, which
  is the correct mechanism and the one the current build cannot express.
- **At the limb.** `tau = (1.69, 3.94, 9.66)`, `T = (0.184, 0.0195, 6.4e-5)`. The surface is
  extinguished to nothing in blue and the in-scatter saturates at the source function, so the
  limb becomes a bright ring whose *hue* runs red-through-white-to-blue with altitude. The
  measured teal band becomes a gradient because the thing it is drawn over is now being
  removed at the correct rate.
- **From inside.** `eyePR` has `r < Ra`, so `aNear < 0`, `start = 0`, and the march runs from the
  eye. `_AirGeom.w < Ra` switches the Body shader to subtracting the eye's own tail column. No
  special case, no second shader.
- **Thin cases.** Mars with the derived `beta` has `tau_R = 4.5e-3` (invisible) and
  `tau_M = 0.333` (the dust) — a butterscotch haze that vanishes face-on and reads only at the
  limb, which is what Mars is. Pluto's derived `tau_R ~ 1e-5` removes its shell entirely, which
  also removes the `worldScale=1` confusion at its root. Venus's derived `tau_G = 21.3`
  saturates every channel and hides the surface, which is what Venus is. All VERIFIED arithmetic
  from §1.

### 5.5 When not to draw it at all

A shell `k = Ra/Rp` seen as a disc `D` pixels wide puts `(k-1) * D/2` pixels between the
silhouette and the top of the air. Below one pixel there is no limb to render and the pass is a
uniformly tinted disc. So the draw rule, in the same shape as CLAUDE.md's
"bigger than a dot **and** smaller than its host":

```
draw the shell only while  D >= 2 / (k - 1)  pixels
```

| Body | k (from the report) | threshold D |
|---|---|---|
| Earth | 1.0157 | **127 px** |
| Jupiter | 1.0140 | 143 px |
| Saturn | 1.0249 | 80 px |
| Mars | 1.0353 | 57 px |
| Venus | 1.0413 | 48 px |
| Uranus | 1.0391 | 51 px |
| Neptune | 1.0404 | 50 px |
| Titan | 1.2330 | 8.6 px |
| Pluto | 1.2800 | 7.1 px |

VERIFIED arithmetic against the `ratio=` values in the baseline report.

Below its threshold, fold the air into the body instead of drawing it: integrate the
atmosphere's disc-average transmittance and in-scatter **once**, on the CPU, from the same
table, and apply them to `_BaseColor` and `_Emission`. Zero per-frame cost, and the planet's
integrated colour stays right.

Budget effect, in the measured `default` view: Earth is 784 px wide and passes; the other eight
are all at `planet r=3.131`, the 2.5 px minimum-visible clamp, and all fail. **Nine atmosphere
draws become one.** That is 8 draw calls and eight shells' worth of fragments removed from the
typical frame, and it also closes the bug class CLAUDE.md already paid for — an atmosphere sized
from a stale `RenderedRadius` around a body that is not drawn — by adding a second, independent
gate on top of `IsGeometryDrawn`.

---

## 6. (c) Aerial perspective near a surface

There is no separate feature here, and that is the finding. Aerial perspective is the same
`Teye` term in §4.3 evaluated with the eye inside the shell — `_AirGeom.w < _AirGeom.x` — so it
is already written. What it needs is plumbing, not code:

1. `_AirGeom.w` must be set per frame to `|eye - centre| / drawnRadius`. One float.
2. The Body shader must subtract the eye's own tail column (the `cEye` branch in §4.3), or
   everything within the atmosphere is attenuated as if seen from space.

**What it does not yet cover, stated as an accepted limit.** A *different* body seen through
this body's air — the Moon low over the horizon from a ground site — is drawn by its own
`CosmoSynth/Body` material, which knows nothing about the air you are standing in. It receives
the in-scatter, because the atmosphere pass is queue 3100 and draws over it, but not the
extinction, so it does not redden at the horizon.

Fixing it is cheap and I recommend it in Phase 3, not now: two shader globals published by
`Placement.PublishGlobals` beside `_CS_CentreEyeWS` —

```
float4 _CS_EyeAir;       // xyz: the centre of the atmosphere the eye is inside (world)
                         // w:   its drawn radius; w <= 0 means vacuum
float4 _CS_EyeAirBeta;   // rgb: beta_R * Rp, a: beta_M * Rp
```

— plus the same `AirColumn` lookup in every Body fragment, behind `UNITY_BRANCH if
(_CS_EyeAir.w > 0)`. In vacuum the branch is never taken, so the cost outside rungs 0 and 1 is
one uniform compare. There are 19 rungs and two of them are inside an atmosphere.

Note also that **CosmoSynth has no terrain in 2.0** — DEMs are Phase 3 behind gate G8 (VERIFIED,
CONTRACT-SUMMARY phase table). So the classic aerial-perspective subject, a mountain range
fading with distance, has nothing to fade. Build the term now because it costs nothing extra,
and revisit when there is ground.

---

## 7. (d) Rings

### 7.1 The data is already on the box and is better than what is being used

`data-staging/nasa-factsheets/satringfact.html` is staged, licence-cleared, and carries
measured radii, **optical depths and albedos** for every ring and gap (VERIFIED — I read it).
Extract, with Saturn's equatorial radius 60,268 km:

| Feature | radius (km) | optical depth | albedo |
|---|---|---|---|
| D inner / outer | 66,900 / 74,510 | 1e-5 | — |
| C inner / outer | 74,658 / 91,975 | 0.05 - 0.35 | 0.12 - 0.30 |
| B inner / outer | 91,975 / **117,507** | 0.4 - 2.5 | 0.4 - 0.6 |
| Cassini division | 117,507 - **122,340** | **0 - 0.1** | 0.2 - 0.4 |
| A inner / outer | 122,340 / 136,780 | 0.4 - 1.0 | 0.4 - 0.6 |
| Encke gap | 133,410 | — | — |
| Keeler gap | 136,487 | — | — |
| F ring | 139,826 | 0.1 | 0.6 |

The shipped `SkyAndRings.BuildRingTexture` builds a 256x1 texture from fbm noise and places the
Cassini division at `t = 0.63 +/- 0.035` on a span of 74,500 to 140,220 km. Converting the real
edges to that same `t` (span 65,720 km):

- Cassini division: `t` from `(117507-74500)/65720 = 0.654` to `(122340-74500)/65720 = 0.728`,
  so **centre 0.691, half-width 0.037**. The shipped centre 0.63 is **0.061 low = 4,010 km
  inward**, which puts the division inside the B ring instead of between B and A. The
  half-width is right. VERIFIED arithmetic.
- Encke gap: `(133410-74500)/65720 = 0.8964`; shipped 0.90, within 236 km — fine. But its real
  width is 325 km = `0.00495` in `t`, and the shipped code darkens `+/- 0.012` (1,577 km, **4.9x
  too wide**) by a factor of 0.25 instead of clearing it. VERIFIED.
- Keeler gap (42 km) and the 3,046 km A-to-F gap are not modelled at all.
- At 256 texels across 65,720 km, one texel is **257 km** — the Encke gap is 1.3 texels and the
  Keeler gap is 0.16. Raise the profile to **2048x1** (32 km/texel) and the real features
  resolve. Cost: `2048 * 4 B = 8 KB`. VERIFIED arithmetic.

### 7.2 Store optical depth, not opacity

This is the change that makes rings behave like rings. A baked alpha is a single number per
radius and cannot express the two most recognisable facts about Saturn: the rings **thicken
toward edge-on** and **reverse contrast when backlit**. Both fall out of storing `tau`:

```
mu    = |dot(viewDirWS, ringAxisWS)|              // 0 edge-on, 1 face-on
alpha = 1 - exp(-tau / max(mu, 0.02))             // optical depth along the ACTUAL path
```

- Face-on, B ring (`tau = 1.5` mid-range): `alpha = 0.78`.
- At 10 degrees open (`mu = 0.174`): `alpha = 1 - exp(-8.6) = 0.9998`, effectively solid.
- Cassini division face-on (`tau = 0.05`): `alpha = 0.049` — a genuine gap you see through.
  At 10 degrees: `alpha = 0.25` — the division closing up as the rings tilt, exactly as it does
  from Earth. VERIFIED arithmetic.

Back-lit transmission, one more line and one of the most striking real effects in the solar
system — from the shadowed side the dense B ring goes dark and the Cassini division goes
*bright*:

```
half muS   = max(abs(dot(L, axis)), 0.02h);
half trans = exp(-tau / muS);                     // starlight through the ring
half back  = step(dot(V, L), 0.0h);               // eye and star on opposite sides
half3 lightThrough = starE * trans * _ForwardPhase;
```

Texture layout: `_RingMap` 2048x1 `RGBAHalf` (`r` = tau, `gba` = albedo colour), 2048 * 8 B =
**16 KB**. Built on the CPU from the fact-sheet table: piecewise-constant tau and albedo per
named band, with the existing fbm only as a small ripple *inside* each band so no two systems
look identical without inventing structure that is not there. Provenance becomes **Measured**
for the band edges, tau and albedo, and **Procedural** for the ripple — which the per-field
provenance system already supports and which the fbm-only version cannot claim at all.

### 7.3 Planet shadow on the rings

Already present in `lane/foundation-shaders`'s `Ring.shader` as an analytic ray-sphere test
against `_ParentCentreWS` (VERIFIED, I read it). Three corrections:

1. **Soften the edge** over the star's angular radius, the same `tA` as the terminator — the
   current test is a binary `lit = (b < 0 && b*b - c > 0) ? 0 : 1`, which aliases into a jagged
   shadow boundary across a low-frequency surface.
2. **The shadowing body is oblate.** Saturn's flattening is 0.098, so a spherical shadow is up
   to 9.8% too wide near the poles. The fix is one line — scale the `y` component of `oc` and of
   `L` (in the body frame) by `Rp_eq/Rp_pol` before the sphere test, turning it into an exact
   ellipsoid test. Given the shadow's edge is already the most-looked-at feature in every Saturn
   image, this is worth the line.
3. **Add the atmosphere's transmittance to the shadow edge.** A ring point just inside the
   umbra is lit by light that grazed the top of Saturn's air, so the edge is not just soft but
   *red*. Reuse `AirColumn` with `r` = the grazing radius. One more fetch, only on the shadow
   boundary, and it is the same table again.

### 7.4 The bug the rings still have

`RingSystem.cs` reads `_body.RenderedRadius` every `LateUpdate` and **never checks
`IsGeometryDrawn`** (VERIFIED — I read the whole file; it is 40 lines and the string does not
appear). That is precisely the failure CLAUDE.md documents for atmospheres: a body pinned to the
sky sphere keeps a stale, enormous `RenderedRadius`, and a decoration that only reads the radius
carries on drawing at that size. It is one of the "five components reading `RenderedRadius`
without checking whether the body is drawn" the gates already flag. Fix it in the same change:

```csharp
bool visible = _body.IsGeometryDrawn;
if (_renderer.enabled != visible) _renderer.enabled = visible;
if (!visible) return;
```

and add the same apparent-size gate the atmospheres get: a ring system whose outer edge is under
3 px across is a smear, not a ring. Saturn's rings reach 2.27 Rp, so the threshold is a planet
disc of about **1.3 px** — i.e. the rings are worth drawing slightly *before* the planet clears
the 2.5 px floor, which is the honest answer for Saturn and should be stated rather than
accidentally inherited.

---

## 8. (e) Behaviour under the group transform, and at two pixels

### 8.1 Everything is in planet radii, so the similarity cancels

§2.6 applies **one similarity per group about the centre eye**. For a body in a Rigid group at
scale `s`, the drawn centre is `E + s*(C - E)` and the drawn radius is `s*R`. Divide any world
point by that pair and `s` disappears:

```
p_PR = (p_WS - centre_WS) / radius_WS
```

So `_AirGeom`, `_BetaR`, `_BetaM`, `_BetaA` and `_RingRadii` are **constants of the body**, set
once at construction, never per frame. The only per-frame uniforms in this whole design are
`_Planet` (drawn centre, drawn radius), `_LightDirWS`, `_StarIrradiance`, `_StarAngularRadius`,
`_RingAxisWS` and `_AirGeom.w`. Today `AtmosphereShell.LateUpdate` writes six vectors per body
per frame and re-derives the coefficients from the shell thickness every time
(`AtmosphereShell.cs:148-158`); after this it writes two.

In **Envelope** mode the body is per-vertex compressed and `s = 1`, so the drawn radius *is* the
true radius and the same division gives the same answer. The contract's port rule — "the
atmosphere integrates analytically in true eye-relative space in units of the planet radius"
(§2.6, §7.2) — stops being a special mode and becomes the only mode. `_PerVertexCompress` stays
on the **Body** and **Ring** shaders, where it drives `CS_CompressWS` on the vertices; the
atmosphere shader does not need it at all, because its shell is rasterised only to get the right
pixels and its integral is in eye-relative planet radii either way.

One thing to check on the headset, not here: in Envelope mode the body's *vertices* are
compressed but the atmosphere's shell is not, so the two silhouettes can disagree. They do not,
to first order — §2.6 proves the per-vertex compression leaves the silhouette exact because both
endpoints keep their true direction from E — but the shell should be marked
`_PerVertexCompress = 1` too so its own rasterised silhouette matches the body's at the same
mesh density. Cheap insurance, and the G1c `envelope` assertion will hold it to the same
`d_max/d_min <= 1.1` rule.

### 8.2 Two pixels

`CelestialBodyView.MinimumApparentPixels = 2.5f` (VERIFIED at `:300`). Below it a body is not
drawn as an inflated sphere at all — it goes to the body-point path, `DrawnAsPoint` is true and
`IsGeometryDrawn` is false (§2.6, "Size floors never create false occlusion"), so decorations
switch off with it. Above it, the clamp inflates the *drawn radius* while the physics stays
true, and that is where the trap is for air:

**A 2.5 px planet with the current shader still gets a full-strength atmosphere pass.** The
shell is 1.6% of 2.5 px = 0.04 px of limb, so every fragment in that disc takes the saturated
in-scatter and the planet becomes a bright dot brighter than itself. §5.5's
`D >= 2/(k-1)` rule kills it — for Earth the shell does not draw until the disc is 127 px — and
the disc-average fold-in keeps the colour right below that.

The matching ceiling is CLAUDE.md's other lesson — "a minimum apparent size needs a maximum to
go with it", the rule that stopped a 3 m Viking lander being drawn 10,000 km across
(`MaximumFractionOfParent = 0.10f`, VERIFIED at `:519`). For air there is no runaway of that
kind, because the shell is a fixed multiple of the body. What there *is* is the Envelope case,
where coverage reaches 1.0 and the march would run at full step count over the whole eye — and
the `clamp(B/coverage, 8, 24)` governor is exactly the ceiling. Both ends are bounded, and both
bounds are the project's existing pattern rather than a new one.

---

## 9. (f) What the box can prove, and what it cannot

### 9.1 Headless, in the play-mode harness (`PlayModeShot`, G1c)

All of these are numbers the harness can compute from a `RenderTexture` readback of a fixed view
without a headset. I recommend three new assertions, each landing with the deliberately broken
run the contract requires.

**`limb` — the radial profile is monotone and the right width.**
Render the `default` view with `COSMOSYNTH_HIDE` set to everything but Earth. Find the disc
centre and radius by centroid and area (`R = sqrt(area/pi)`), then sample radiance along 64 rays
at 1 px steps from `0.90 R` to `1.10 R`. Assert:

- the profile is **monotone decreasing** outward beyond `R`, to within 1/255 (a saturated band
  is not);
- the radiance at `1.0 R + 1 px` divided by that at `0.90 R` is between 0.5 and 3.0 (today it is
  a step; measure it, then pin it);
- the outer edge — the last radius above 2/255 — sits within 2 px of `R * k`, i.e. `0.0179 R`
  beyond the silhouette, the 7.7-8.0 px I measured. That single number catches a shell sized
  from a stale radius.

This is the assertion that would have caught the current defect: my 36-50 px band against a
7.7-8.0 px geometry fails the third clause by 4.5-6.5x.

**`terminator` — the dark limb is where the sun says it is.**
Sample along the equator of the rendered disc and find the 50% radiance crossing. Its angular
position must equal `acos(0)` from the sub-solar point to within the penumbra half-width
`R_star/d` plus 2 px. The current 0.05 wrap displaces the 50% point by
`asin(0.05) = 2.87 deg` — 20 px on a 784 px disc, which is 10x the tolerance. Catchable,
headless, today.

**`airalpha` — the air contributes no coverage.**
Already available as a special case of the existing G1c `alpha` assertion: the atmosphere layer's
`Blend` is `Additive`, so destination-alpha coverage with `COSMOSYNTH_HIDE=Atmosphere_` must
equal coverage without it, exactly, 0 of 255. This one exists — make sure the atmosphere layer
is registered with `LayerBlend.Additive` so it is actually covered, and record a broken run (a
debug build returning alpha 1 from the atmosphere) as the contract requires.

Also verifiable headless, without new assertions:

- **The LUT itself**, in `run-tests.sh` with no engine at all: the vertical column must equal
  `H*(1-exp(-h_top/H))` to 0.5% and the horizontal column at the surface `H*sqrt(2 pi Rp/H)` to
  2%. Both are closed forms, both are in `Core/`, and both fail loudly on a sign error in the
  `x_mu` mapping.
- **The coefficient derivation** of §1: recompute the nine `beta_R` rows from density,
  molar mass and composition and assert each is within 2x of the shipped value. Fails on five of
  nine today, which is the point.
- **The ring profile**: assert that the Cassini division's minimum lies between `t = 0.654` and
  `t = 0.728` and that the Encke gap is under 400 km wide. Both from the staged fact sheet.
- **Texture bytes**: sum `Texture.currentTextureMemory` after all bakes and assert it is under
  the §7.3 row. Catches the RGBA32 decode path immediately.
- **Draw counts**: the number of enabled `CosmoSynth/Atmosphere` renderers in the `default` view
  must be 1, not 9, once §5.5 lands.

### 9.2 Only a headset can settle these

- **That `CS_EyePositionWS()` is genuinely per-eye.** The harness is monoscopic and fakes the
  eyes by moving the camera transform, which moves `_WorldSpaceCameraPos` too, so a shader that
  reads the centre eye where it should read its own passes either way. The contract already says
  this (§11.3 `stereo`, §13 item 7). For this topic it bites twice: the atmosphere's ray origin
  and the ocean's specular `V`. A wrong `V` on a mirror-like ocean puts the sun glint in a
  different place in each eye, which is the most uncomfortable possible version of this bug.
  **H-check: stand off Earth at the `planet-room` rung with the glint on the limb and close one
  eye, then the other. The glint must move by the parallax of a surface feature, not by half a
  disc.**
- **Whether the limb reads as air or as a rim light.** It is a gradient over 8 px at 1140 px/rad;
  no flat capture on llvmpipe with MSAA off can answer it.
- **Whether the atmosphere leaks coverage into passthrough in practice.** The `alpha` assertion
  proves the shader returns 0; it cannot prove the compositor agrees under SRP foveation, which
  rasterises non-uniformly.
- **`kappa_atm`, the ms per step-coverage.** Build a Fractarium-style probe: draw one full-screen
  quad running the atmosphere fragment with the step count forced to `N` and sweep
  `N = 4, 8, 16, 24, 32` at fixed coverage 1.0, reading `adb logcat -d | grep VrApi` for
  `GPU%`. The slope is `kappa_atm` directly and it settles both this topic's budget and, by
  comparison, the INFERRED 0.03-0.10 bracket the dust layer is relying on.
- **Whether the GGX ocean shimmers.** A specular lobe on a 2048-texel water mask at 1140 px/rad
  will alias; whether MSAA 4x plus specular anti-aliasing (normal-variance to roughness) is
  enough is a device question.
- **The night lights' gain.** `_NightGain = 0.30` is a starting value. On a passthrough headset
  with a real room behind it, the right number is whatever makes a city read without looking
  like a light leak.

---

## 10. Cost, collected

Per eye, `default` view (Earth at 784 px, the measured case). All figures INFERRED where marked;
the arithmetic is shown so the lane can redo it with a measured `kappa`.

| Item | Draws | Verts | Fragments | Step-cov | Texture | ms/eye |
|---|---|---|---|---|---|---|
| Body, Earth (4 extra fetches over 0.28 eye-screens) | 0 new | 0 new | 0 new | 0 | +2.8 MB packed, -7.9 MB from compression | **INFERRED, probe** |
| Atmosphere, Earth (10 steps x 0.29 coverage) | 0 new | 0 new | 0 new | **2.9** | +16 KB LUT | **0.09-0.29 (I)** |
| Atmosphere, the other eight | **-8** | -8 shells | **-8 shells** | 0 | 8 x 16 KB | **negative** |
| Rings, Saturn (profile 256 -> 2048, RGBAHalf) | 0 new | 0 new | 0 new | 0 | +8 KB (16 KB total) | ~0 |
| Ring shadow on the planet (1 fetch, 4 bodies) | 0 | 0 | 0 | 0 | 0 | INFERRED, small |
| Envelope / inside the air (8 steps x 1.0) | 0 | 0 | 0 | **8.0**, borrowed from B=30 | 0 | 0.24-0.80 (I) |
| **Net against §7.3** | **-8 draws** | **0** | **fewer** | **2.9 of 3** | **about -5 MB** | fits, pending the probe |

APK delta: **0 bytes for the lookup tables** (built at load), **+249 KB** if the staged night map
ships, **+966 KB** if the staged cloud map ships, **+2.3 MB** if Earth is upgraded to the staged
5400x2700 Blue Marble. Against 46.5 MB today and a 150 MB ceiling. VERIFIED file sizes from
`docs/licence/solar-system.md`.

---

## 11. Ordered plan for a lane agent

Each step is independently testable and each leaves the tree green. Steps 1-3 are `solar` lane
plus Core; 4-7 are `p0-shaders` plus the integrator; 8-9 are `solar` 1b.

1. **Fix the diagnostic first, so you can see what you are changing.** `AtmosphereShell.Describe`
   prints `IsGeometryDrawn` and the words `not drawn` instead of a stale transform. Re-capture
   the baseline. This alone turns the "Pluto `worldScale=1`" defect into a non-issue and stops
   eight stale rows lying in every report. *Half a day.*
2. **Core: `AirTable.cs`** — engine-free. Input: six doubles (`Rp`, `h_top`, `H_R`, `H_M`,
   and the two beta triples are not needed here). Output: `float[2 * 128 * 32]` of column
   integrals in planet radii, with the `x_r` / `x_mu` mapping of §3.2. Land it with the two
   closed-form tests of §9.1 and one deliberately wrong sign recorded as failing. *One day.*
3. **Core: the coefficient derivation and its test** (§1). Add `absorptionPerKm` to
   `AtmosphereDefinition`. Regenerate `bodies.py` with derived Rayleigh values and re-label the
   provenance **Inferred**. The nine-row test fails before, passes after. *One day.*
4. **Bring `lane/foundation-shaders` up to R1/R7/R30** before anything else touches it: add
   `CS_EyePositionWS` and `CS_JitterDir` to `CosmoCore.hlsl`, replace all seven
   `GetCameraPositionWS()` call sites, add `_Fade` to `Body.shader`, and extend
   `tools/gates/stereo_gate.sh` to grep for both strings with its own self-test. **This is a
   prerequisite, not part of this topic, and it is currently green in a report that does not
   check it.** *Half a day.*
5. **`CosmoSynth/Atmosphere`**: units to planet radii, inner light march replaced by the LUT,
   governor uniform, soft shadow edge, `_StarIrradiance` unclamped. Delete the `tau` clamp at 4.0
   and the `normalise` arithmetic in `AtmosphereShell`. Capture the `default` view and read the
   limb profile. *One and a half days.*
6. **`CosmoSynth/Body`**: the §4.3 fragment. Terminator from the star's angular radius,
   `Tsun`/`Teye`, GGX ocean on the existing water mask. Do the transmittance and the terminator
   **before** the cosmetic layers, because they are what change the picture. *One and a half
   days.*
7. **The `limb` and `terminator` assertions** (§9.1), each with its recorded broken run. *Half a
   day.*
8. **Clouds, night lights, and the packed `_SurfaceMap`** including `Compress(true)` and the
   texture-memory assertion. Stage NASA Black Marble if the licence work is quick; otherwise ship
   the Solar System Scope map labelled Inferred with its CC BY line. *One day.*
9. **Rings**: profile from `satringfact.html` at 2048 with tau and albedo, the `1-exp(-tau/mu)`
   opening-angle law, back-lit transmission, ring shadow on the planet in the Body shader, the
   oblate shadow, and `IsGeometryDrawn` in `RingSystem`. *One day.*
10. **The `kappa_atm` probe on the headset** (§9.2), then re-tune the governor's `B_atm` from the
    measured number and write the real ms into the §7.3 row. *Comes with the first beta.*

Total: about **eight and a half agent-days**, of which four are shader work and none adds a draw
call.

---

## 12. Claim ledger

**VERIFIED** — read, run, or measured by me on the box:

- The 431-446 px / 423-438 px / 7.7-8.0 px geometry and the 36-50 px measured band, from the
  baseline PNG.
- The saturated hue at the band edge: shader output (0.129, 0.314, 0.404) against a `tau` ratio
  of 1 : 2.33 : 5.71.
- `AtmosphereShell.LateUpdate` returns before writing `localScale` when `!IsGeometryDrawn`, and
  `Describe()` has no visibility guard: the `worldScale=1` explanation.
- Earth's `tau = (0.049, 0.115, 0.281)`, `ratio = 1.0157`, `densityFalloff = 0.085` — all
  reproduced from `bodies.py` and `SkyAndRings.BuildAtmospheres`.
- The limb airmass 34.33 and the resulting `tau` and transmittance at the limb and face-on.
- The `N`, `(n-1)` and beta-ratio tables of §1, and that Earth's shipped triple is the textbook
  `lambda^-4` set to within a uniform 1.245x of my own closed-form evaluation.
- Terminator penumbra half-widths per body, and the closed-form disc-source formula's continuity.
- The Saturn ring radii, optical depths and albedos, from the staged NASA fact sheet; the
  Cassini division's true `t` centre 0.691 against the shipped 0.63; the Encke gap 4.9x too wide;
  257 km per texel at 256.
- `MinimumApparentPixels = 2.5`, `MaximumFractionOfParent = 0.10`, and the §2.6 rule that a body
  below the floor is not geometry.
- `DeriveWaterMap` exists, writes smoothness to alpha, and is bound as `_MetallicGlossMap`.
- `Texture2D.LoadImage` produces RGBA32 in this codebase; Earth's two maps cost 21.4 MB.
- NASA Black Marble is **not** staged; the only night map is Solar System Scope's, CC BY 4.0.
- `lane/foundation-shaders` calls `GetCameraPositionWS()` in seven places, has no
  `CS_EyePositionWS`, still has `CS_IGN`, and `Body.shader` has no `_Fade`.
- `RingSystem.cs` never checks `IsGeometryDrawn`.
- Fractarium's 0.338 ms per step-coverage, quoted from the research report as an upper bound.

**INFERRED** — reasoned, with the check named:

- `kappa_atm` of 0.03-0.10 ms per step-coverage, hence every ms figure here. → the device probe
  in §9.2. This is the single number that decides whether the design fits.
- STP refractivities and King factors for the eight non-air mixtures. → pin them from a handbook
  in the importer, with the test of §1 as the guard.
- That `Texture2D.Compress(true)` yields ETC2 at 8 bits/texel on Quest. → one `Debug.Log` of
  `texture.format` on device.
- `alpha_ocean = 0.20` from Cox-Munk at 7 m/s, and the GGX-alpha-equals-RMS-slope convention. →
  a G1d capture of the glint at 30 degrees phase.
- `_NightGain = 0.30`, `_CloudTint`, and the 0.55 cloud-shadow strength. → G1d, then the headset.
- That 128 x 32 is enough table resolution (0.6% interpolation error). → compare the marched
  result against a 512 x 128 table offline in the Core test.
- That the Envelope-mode shell and body silhouettes agree. → the G1c `envelope` assertion plus a
  true-scale capture.
- The Saturn A-to-F gap and Keeler widths quoted from the fact sheet's inner-edge-only listing.
  → the PDS Rings Node table the fact sheet links to, if the gaps matter at 2048.

**Not addressed here, and whose lane owns it:** exposure, tone mapping and bloom (the image
pipeline is item 1 of the visual brief and every brightness constant above is provisional until
it lands); star rendering; the Milky Way band; sound; composition of the seven fixed views.
