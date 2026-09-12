# Lane `data-cmb` — the cosmic microwave background

**Status: delivered, one known-red test.** Branch `lane/data-cmb`, worktree
`~/projects/CosmoSynth-wt/data-cmb`, based on `master` at `e72d89a`.

The last rung of the ladder now exists as data: a cube map the viewer can stand inside, the
mask that says where the galaxy is still in the way, and the constants — monopole, dipole in
three rest frames, and the two distances that put the CMB at the top of the scale ladder.

---

## 1. What ships

| Path | Bytes | Rows | What |
|---|---|---|---|
| `unity/Assets/StreamingAssets/CosmoSynth/cmb/cmb_sky.cscb` | **6,688,616** | 6,291,456 | `CSCB` v1: header 32 + meta 3,912 + payload 6,684,672 |

Inside it:

| Column | Type | Rows | Offset | Content |
|---|---|---|---|---|
| `t` | u8 | 6 × 1024² = 6,291,456 | 0 | ΔT anisotropy, `linear` encoding scale 1000/255 = 3.9215686 µK, offset −500 µK |
| `mask` | u8 | 6 × 256² = 393,216 | 6,291,456 | 1 = outside KQ85 (galactic foreground residual), 0 = kept |

`sha256 fe4ea628a526ed2536636364e77ccdd26e9e0570db2402055566746c86584132`

**APK cost.** 6,688,616 bytes raw; **3,160,848 bytes deflated** (ratio 0.473, measured, not
inferred — §C.12 guessed ~4.6 MB from the equirect's 0.73 ratio, and an 8-bit map of a smooth
field compresses much better than that). `SA/CosmoSynth/` goes from 6.70 MB to **13.39 MB**.
Against the ~46 MB APK today and the 150 MB ceiling, the layer costs about **3.2 MB** in the
package and 8.4 MB of GPU texture with mips once uploaded as a 6 × 1024² R8 cube.

Knob, unchanged from §C.12: `--face 2048` makes a 25 MB file with no code change (the face
size is read from the header). `--face 512` saves 5 MB. Neither was needed.

### Code

| Path | Lines | What |
|---|---|---|
| `tools/cosmodata/build_cmb.py` | 706 | the importer, numpy + stdlib, `--selftest` |
| `tools/cosmodata/healpix_nest.py` | 97 | pure-numpy HEALPix NESTED, copied verbatim from `docs/research/scripts/` per §C.12 ("both move into `TC/`") |
| `unity/Assets/CosmoSynth/Core/Cmb/CmbSky.cs` | 358 | container parse, face table, `Sample`, `IsMasked`, `ProvenanceAt`, `TemperatureK`, `FalseColour` |
| `unity/Assets/CosmoSynth/Core/Cmb/CmbModel.cs` | 308 | monopole, dipole in 3 frames, ΛCDM distances |
| `tests/CosmoSynth.Core.Tests/CmbSkyTests.cs` | 378 | 11 tests against the shipped file |
| `tests/CosmoSynth.Core.Tests/CmbModelTests.cs` | 180 | 9 tests on the constants and the integrals |

Suite: **161 passed, 1 failed** (was 142 passed; the floor is 142 and the one failure
is the known red of section 9). `tools/gate-all.sh a` — the test-suite gate alone —
exits **0**, the failure excused by `known-red.txt`. Whole-run `tools/gate-all.sh`
exits **10** both here and on `master`, from the same four pre-existing failures
(G0b–G0e) in `Runtime/` and in two files `lane/foundation-code` renames. No gate log
in this worktree mentions a file this lane touched.

---

## 2. Source, and why it is not Planck

NASA LAMBDA, **WMAP nine-year (DR5) Internal Linear Combination** map plus the **KQ85
temperature analysis mask**. Both CC0 — a NASA-led mission with no restrictive notice in
either FITS header (evidence E42, E43 in the licence amendment).

§B row 13 and §C.12 name **Planck PR3 SMICA-noSZ**. The licence amendment kills it: ESA Space
Science Archive data is CC BY-NC 3.0 IGO and the IRSA mirror states no terms of its own, so it
does not launder it. WMAP was §C.12's own declared fallback and is now primary. The cost is
angular resolution — a 1° beam against SMICA's 5′ — which is invisible on a Quest 3, and
§C.12's own note that "the data hold no detail below the 5 arcmin beam anyway" applies twelve
times over here.

`CmbSkyTests.ShippedFileHeader` asserts the attribution line names WMAP and **does not contain
the string "Planck"**, so the amendment's "the app must never say Planck" about the map is
enforced by a test rather than by memory.

The attribution string the app must display, from the file's own meta and matching amendment
credit C29 verbatim:

> Cosmic microwave background: WMAP 9-year Internal Linear Combination map and KQ85
> temperature analysis mask. Credit: NASA / WMAP Science Team, via the Legacy Archive for
> Microwave Background Data Analysis (LAMBDA).

---

## 3. Equirectangular versus cube map — the argument, written out

§C.12 already chose the cube. The measurement this run confirms it:

| Option | Texels | Raw | GPU with mips | Deflated | Problem |
|---|---|---|---|---|---|
| Equirect 4096 × 2048 R8 | 8.4 M | 8.39 MB | 11.2 MB | 6.12 MB (measured earlier) | `atan2` seam in mip selection; 4× oversampled in RA at the poles and starved at the equator |
| Cube 6 × 2048² R8 | 25.2 M | 25.2 MB | 33.6 MB | ~18 MB | four times the size for detail the 1° beam does not contain |
| Cube 6 × 2048² EAC_R11 | 25.2 M | — | 16.8 MB | ~12.6 MB | needs an offline EAC encoder the stdlib cannot provide, or Unity's asset pipeline, which leaves the StreamingAssets contract |
| **Cube 6 × 1024² R8 (shipped)** | 6.29 M | **6.69 MB** | **8.4 MB** | **3.16 MB (measured)** | face-centre texel is 6.7′ against a 60′ beam — irrelevant here |

**Memory.** The cube is the smaller of the two at equal worst-case texel density, because the
equirect spends its budget on polar rows that subtend almost nothing. Texel density on the
shipped cube runs 3.4′ (edge midpoints) to 6.7′ (face centres); the equirect at 4096 wide is
5.3′ at the equator and 0.09′ at Dec 89°, which is 60× oversampled for the same file size.

**Block compression.** Neither ASTC nor EAC is used, and that is deliberate: this is a
*measurement*, sampled by tap-to-read as well as drawn. ASTC's per-block endpoint fitting
would put errors of tens of microkelvin — a third of the sky's whole rms — into a field whose
entire signal is ±450 µK, and it would do it worst exactly at the Cold Spot and the
plane/pole contrast the layer exists to show. R8 uncompressed costs 8.4 MB with mips and
keeps the quantisation at a flat, known **±1.96 µK** (half of one 3.92 µK step, measured:
`quantisationMaxErrorUk = 1.9608`). If the layer ever has to shrink, `--face 512` is the
honest knob, not a block format.

**Import settings for the runtime lane: R8, linear, no sRGB, no compression, mips on.**

---

## 4. The microkelvin range

Measured on the shipped cube, solid-angle weighted:

| | µK |
|---|---|
| min | −452.50 |
| max | +442.25 |
| rms | 71.088 |
| mean | +2.038 |
| p1 / p99 | −169.12 / +165.25 |
| rms at \|b\| < 2° | 89.11 |
| rms at \|b\| > 30° | 66.42 |
| clipped by the ±500 µK encoding | **0.0 %** |

The header said `TUNIT1 = 'mK, thermodynamic'` and for this file it happens to be true — but
it was *proved* by the rms, not read: 0.0711 in native units is 71.1 µK, which is the CMB
anisotropy at a 1° beam. 0.0711 K would be 71,100 µK and 0.0711 µK would be nothing. The
importer refuses anything whose rms falls outside (30, 300) µK, and `--selftest` feeds it the
same map scaled 1000× wrong to prove the gate fires (it reports 41,023 µK and refuses).

The mean is +2.04 µK, not 0: the ILC has its own small offset. It is well inside the
importer's |mean| < 10 µK bar.

---

## 5. Proving the orientation — three independent ways, plus a control for each

A CMB layer fails by looking perfect while sitting rotated. A randomly rotated map has the
same rms, the same histogram and the same beautiful mottling. So every check below is one a
rotated map fails, and each one was run **with a deliberately broken version** to show it can.

**None of the three uses the rotation the bake used.** The bake applies the Hipparcos ICRS→
galactic matrix; the checks take their sky positions from published RA/Dec.

### 5.1 The galactic plane residual peaks at |b| < 2°

|ΔT| binned in 2° bands of galactic latitude, with b computed from the **published** north
galactic pole (RA 192.85948, Dec +27.12825), never from the matrix.

Peak bin **[0°, 2°) at 70.78 µK**, falling to 49–56 µK above |b| = 30. rms 89.11 µK inside
|b| < 2 against 66.42 outside |b| > 30, **ratio 1.342** — §C.12's bar is 1.05 and a map whose
plane is elsewhere gives about 1.00.

*What it cannot prove:* it constrains the plane, not the longitude along it. A map rotated
about the galactic pole would pass this and fail 5.2.

### 5.2 A known feature lands where it belongs

The **CMB Cold Spot**, published independently of any of this (Cruz et al. 2005; Planck 2013
XXIII) at galactic (209°, −57°) = RA 03h15m20s, Dec −19°35′.

| measured from | 5° disc mean |
|---|---|
| the published equatorial position | **−105.62 µK** |
| the published (l, b), rotated in | **−107.25 µK** |

§C.12 expects **−107 ± 6 µK for WMAP**, and that is what it reads. The two positions differ
by ~1° because the (l, b) pair is quoted to whole degrees; 5° is far wider than the gap.

*Note on the brief's "about −112 µK":* −112 is the **Planck** number in §C.12's own table.
WMAP's 1° beam smooths the spot, and §C.12 gives −107 ± 6 for it. −105.6 is the honest value
for the product that can actually ship.

*What it cannot prove:* one feature is one point. A map reflected through the Cold Spot's axis
would pass. That is what 5.1 and 5.3 are for.

### 5.3 Agreement with an independent product

Two, since neither alone is decisive:

**(a) The KQ85 analysis mask** is a different FITS file with its own sha256, built by the WMAP
team from K-band, Hα and dust foreground *templates* — not from the ILC map. Where it rejects
the sky, the ILC residual must be large.

| | rms(rejected) / rms(kept) |
|---|---|
| the mask as it is | **1.1723** |
| rotated 90° about the ICRS pole | 0.9232 |
| rotated 90° about ICRS x | 0.9864 |
| rotated 90° about ICRS y | 1.0007 |

So the null is 0.92–1.00 and the signal is 1.17. It is only a 17 % effect because the ILC is
cleaned hard; that is why it is a second vote and not the decisive one. (A point reflection is
**not** a usable control — the galactic plane maps to itself and duly scores 1.155.) The
shipped bar is > 1.10 and > 1.15× the z90 control. Rejected fraction **0.2520**, against the
published KQ85 f_sky of about 0.75.

**(b) An independently written projection of the same map.** The staged equirectangular bake
came out of `cmb_bake.py`; this cube came out of `cmb_cube.py` — separate scripts, separate
rasters, separate application of the rotation. 200,000 random directions sampled through both:

| | correlation |
|---|---|
| as baked | **0.99649** |
| face order scrambled | −0.00991 |
| +x and −x swapped | 0.72533 |
| rotated by **1.0°** | 0.75382 |
| rotated by 2.0° | 0.53106 |

**This check resolves a one-degree error.** The bar is > 0.98 with the scrambled control under
0.05. That is the sharpest instrument in the set.

*What it cannot prove:* (b) is evidence about the **code**, not the data — both projections
read the same FITS, so a fault in the source map would pass. (a) is evidence about the
**data**, but weakly, because the ILC's foreground residual is small by design. Nothing here
is a second measurement of the CMB sky. A genuinely independent CMB map (COBE-DMR, or Planck)
was not fetched: Planck is licence-barred, and triton was at **4.9 GB free**, below the 6 GB
floor the brief sets for any fetch at all.

### 5.4 And the matrix itself

`build_cmb.py` refuses to run unless its own `EQ2GAL` sends the published north galactic pole
to (0, 0, 1) within 1e-9. It comes out at 1.2e-12. Separately, the published NGP and galactic
centre directions are orthogonal to 7.7e-5 degrees, so the two published positions agree with
each other before either is used.

---

## 6. The dipole is constants, not texture

The shipped map has the monopole **and** the dipole removed — that is why its rms is 71 µK and
not 3355. Everything needed to put either back is in `CmbModel` and in the file's meta block,
so the app computes them in a shader and states which mode it is in.

| Quantity | Value | Class | Source |
|---|---|---|---|
| Monopole T₀ | 2.72548 ± 0.00057 K | Measured | Fixsen 2009 (COBE/FIRAS) |
| Dipole amplitude | 3355 ± 8 µK | Measured | Hinshaw et al. 2009 (WMAP five-year) |
| Dipole direction | galactic (263.99°, 48.26°) = **RA 167.9287°, Dec −6.9269°** | Measured | ditto |
| Implied solar speed | **369.04 km/s** | Derived | c·ΔT/T₀; published 369.0 ± 0.9 |
| Local Group | 627 ± 22 km/s toward (276°, 30°) → 5700.2 µK | Measured | Kogut et al. 1993 (COBE/DMR) |
| Galactic centre | 555.9 km/s toward (265.66°, 28.83°) → 5053 µK | **Inferred** | derived: solar CMB velocity minus the Sun's galactic orbit |
| Mask rejected fraction | 0.2520 | Measured | KQ85, and the 25.2 % it covers is **Inferred**, not Measured |

**Deviation, deliberate:** §B and §C.12 quote the *Planck 2018* dipole (3362.08 µK toward
264.021°, 48.253°, v = 369.82 km/s). The amendment removed every Planck data product, and
quoting Planck's dipole beside a WMAP map invites the app to say "Planck" about a map that is
not one. The WMAP/COBE numbers are used instead. They differ by 0.2 % in amplitude and 0.02°
in direction — four orders of magnitude below anything a headset shows. The contract's own
probe target (`galactic (264.021, 48.253) → RA 167.9419, Dec −6.9443`, §CONTRACT line 892)
still holds for whoever wants it; it was reproduced to 1e-4 during this lane and is simply not
what ships.

**The galactic-centre frame is Inferred and says so.** §B took 565 km/s → (265.76°, 28.38°)
from Planck 2018 I Table 3. Deriving it instead needs the LSR circular speed, which the
literature puts between 220 and 240 km/s; that spread moves the answer by ±8 km/s. With
Eilers et al. 2019's 229 ± 1 km/s and Schönrich et al. 2010's solar peculiar motion it comes
out 555.9 km/s toward (265.66°, 28.83°) — within 2 % of §B's Planck-derived value, which is a
useful cross-check on the derivation, and the test bound is that literature spread and no
wider.

**The foreground mask, and why it is not called inpainting.** §C.12's Core API is
`IsInpainted(dir)`. That is right for Planck, whose masked pixels hold a *constrained
realisation* — a plausible invention. WMAP inpaints nothing: inside KQ85 the map is still the
measurement, merely contaminated. Shipping a method called `IsInpainted` would claim a fiction
the file does not contain, so it is **`IsMasked(Double3)`**, with `ProvenanceAt(Double3)`
returning `Measured` outside and `Inferred` inside. The meta block's `maskMeaning` says the
same in prose. **Runtime lane: grep for `IsMasked`, not `IsInpainted`.**

---

## 7. The top rung of the scale ladder

`CmbModel` integrates rather than copies, which is what CONTRACT §2482 asked for.

| Quantity | Value | How |
|---|---|---|
| Comoving distance to last scattering χ* | **13.867 Gpc** (4.28e23 km, 45.2 Gly) | Simpson in ln(1+z) over c/H(z) |
| Particle horizon | **14.147 Gpc** (46.1 Gly) | Simpson in the scale factor, ∫da/(a²E) |
| z* | 1089.92 | Planck 2018 VI Table 2 |
| H₀, Ω_m, Ω_r, Ω_Λ | 67.36, 0.3153, 9.209e-5 (derived), 0.68461 | Planck 2018 VI |

**χ* is a real known-answer test.** Planck 2018 VI publishes r* = 144.43 Mpc and
100θ* = 1.04109, whose ratio is 13,873.4 Mpc. `ComovingDistanceMpc` integrates from the
density parameters and was not fitted to it, and lands on 13,867.2 Mpc — **0.045 % away**. A
missing (1+z) in the substitution, or a dropped radiation term, moves this by whole percent.
The test bound is 0.2 %.

**⚠ DEVIATION THE SCALE-LADDER LANE MUST KNOW.** CONTRACT §2482/§2507 carries the particle
horizon as **14.26 Gpc (4.40e23 km, 46.5 Gly)** and marks it *"INFERRED until the integration
is recorded in `CmbModel`"*. This is that integration, and it gives **14.147 Gpc**, 0.8 %
smaller. 14.26 belongs to an older parameter set. Rung 18's "8.8e23 km to 1.2 m" and §2.4's
"radius 14.26 Gpc = 4.4e23 km" should be taken from `CmbModel.ParticleHorizonGpc` rather than
from the table. At the 1.2 m ball this is 5 mm; it changes nothing visually and it is the
difference between a copied number and a derived one.

χ* is unchanged at 13.87 Gpc, and **that** is where both forms of the shell are drawn — the
particle horizon is a different surface and the app must not label the CMB shell with it.

---

## 8. Tests: what each proves and what it cannot

### `CmbSkyTests` (11, all green)

| Test | Proves | Cannot prove |
|---|---|---|
| `ShippedFileHeader` | the artefact is CSCB v1, 1024² faces, 256² mask, ICRF, WMAP-attributed, and the word "Planck" is absent | nothing about the pixels |
| `EncodingEndpointsAreExact` | raw 0 → −500.0 µK and raw 255 → +500.0 µK exactly, one step = 1000/255 | nothing about what values are in the file |
| `FaceTableRoundTrips` | the cube-face table is self-consistent: all six faces, 36 texels each, select themselves; and 60,000 arbitrary directions land within the texel half-diagonal (0.0791°, worst 0.0782°) | that the table matches the **GPU's** face layout — that is the `cmb-faces` play-mode assertion, still owed |
| `ColdSpotIsWhereItWasMeasured` | orientation, against a published position, to −107 ± 6 µK; and the C# bilinear reading agrees with the Python texel-weighted one within 4 µK **across the language boundary** | it is one 5° disc; a reflection about its axis survives |
| `GalacticResidualIsOnThePlane` | the plane is the plane, using the published pole; ratio 1.34 against a 1.05 bar and a ~1.00 null | longitude along the plane |
| `MaskAgreesWithTheMap` | a separately produced file agrees about where the galaxy is (1.17 against a ~1.0 null), and the rejected fraction is 25.2 % | it is a weak agreement by construction — the ILC is cleaned hard |
| `SkyStatisticsAreCmb` | rms 71 µK, mean ~0, nothing escapes the encoding — the monopole and dipole really are absent | that the structure is *the* CMB rather than *a* CMB-like field |
| `TemperatureAddsTheConstantsBack` | `TemperatureK` is T₀ + D(n̂·d̂) + ΔT, exactly at apex and antapex | nothing about the shader that will do the same in GLSL |
| `ProvenanceFollowsTheMask` | the galactic centre reads Inferred, the north galactic pole reads Measured | nothing about the 25 % boundary's shape |
| `FalseColourIsAFunction` | the ramp is a function of ΔT: white at zero, monotone, saturating, contrast-controlled, invertible, grey has no hue | that it looks good |
| `RefusesWhatItShould` | five ways to corrupt the file are refused by `FormatException`, not read as if fine | the list is not exhaustive |

### `CmbModelTests` (9, one red)

| Test | Proves | Cannot prove |
|---|---|---|
| `Monopole` | T₀ is the FIRAS value and is in kelvin | — |
| `DipoleImpliesTheSolarSpeed` | **known answer:** 3355 µK / 2.72548 K × c = 369.04 km/s against a separately published 369.0 ± 0.9. A unit slip anywhere moves this by orders of magnitude | that 3355 is the right amplitude for *this* map — it is a published constant, not fitted here |
| `DipoleApexInGalacticCoordinates` | the apex is a unit vector at the published (l, b) | — |
| **`DipoleApexInIcrf`** | **KNOWN RED** — see §9 | — |
| `DipoleIsACosine` | A·(n̂·d̂) at apex, antapex and 90°, zero for `CmbFrame.None`, insensitive to input magnitude | — |
| `LocalGroupFrame` | 627 km/s → 5700.2 µK and an apex 21° from the solar one, so switching frames visibly does something | that 627 is current — it is COBE's 1993 number, chosen because it is clean |
| `GalacticCentreFrameIsDerivedAndPlausible` | the derivation lands at 556 ± 10 km/s toward (265.7°, 28.8°), between the solar and Local Group frames | that it is measured — it is Inferred and the bound is the Θ₀ literature spread |
| `DistanceToLastScattering` | **known answer:** the integrator reproduces Planck's own r*/θ* to 0.045 %; E(0) = 1; flatness; quadrature converged | that the parameters are right — they are published |
| `ParticleHorizon` | the horizon is beyond χ* by 2.02 %, at 14.147 Gpc, quadrature converged | that 14.26 Gpc is wrong — only that it is not what these parameters give |

### `build_cmb.py --selftest`

Runs the **FITS branch** end to end on a synthetic NSIDE-32 HEALPix map, because the raw WMAP
FITS is no longer on disk (see §10). It asserts: a hot pixel placed at galactic (120°, +30°)
comes back out at the right ICRF direction (0.974° away, against a 1.406° texel); a whole face
reproduces the map exactly; the encoding endpoints; that the rms gate fires on the same map in
the wrong units; and that the container round trips through its own reader with the mask
column landing 8-byte aligned. **What it cannot prove:** that the *real* WMAP FITS parses —
that was done on 2026-09-12 by the staged research script, whose output this lane consumes.

---

## 9. KNOWN RED — `CmbModelTests.DipoleApexInIcrf`

Recorded in `tools/gates/known-red.txt` as `G0a`.

`Core/Frames/StandardFrames.cs` on `master` builds `EquatorialToGalactic` with all three
rotation signs flipped, sending the north galactic pole to (−0.346, 0.176, 0.922) instead of
(0, 0, 1). `CmbModel.DipoleDirectionIcrf` goes through that shared matrix **on purpose** — a
private copy would make this class immune to the bug, which is exactly the wrong robustness:
the CMB must turn with the same matrix as the stars or it sits 45° away from them with nothing
saying so.

**Fixed by `lane/foundation-code`, and that was verified rather than assumed:** dropping that
branch's `StandardFrames.cs` into this worktree gives **20 passed, 0 failed** against 19/1 on
master. Everything else in the lane is green either way.

The `known-red.txt` line excuses the whole G0a gate, which is more than it should — the file
has no per-test granularity and `TestHarness.cs` is not this lane's to extend. **Delete that
line the moment `lane/foundation-code` merges.**

---

## 10. Deviations from the contract, each with its reason

| # | Contract says | Shipped | Why |
|---|---|---|---|
| 1 | §B row 13 / §C.12: Planck PR3 SMICA-noSZ, 5′ beam | WMAP nine-year ILC, 60′ beam | `DATA-LICENCE-AMENDMENT` §2 row 15 — ESA is CC BY-NC and the IRSA mirror does not launder it. WMAP was §C.12's own declared fallback |
| 2 | §C.12: mask = inpainting mask, 2.1 % of the sky, `IsInpainted(dir)` | KQ85 analysis mask, 25.2 %, `IsMasked(dir)` | WMAP inpaints nothing. Calling a contamination mask "inpainted" would claim a constrained realisation the file does not contain. The masked 25.2 % is labelled **Inferred** |
| 3 | §C.12 dipole = Planck 2018 (3362.08 µK, 264.021°, 48.253°) | WMAP/COBE (3355 µK, 263.99°, 48.26°) | keeps the whole layer inside one licence-clean mission; 0.2 % and 0.02° apart |
| 4 | §C.12 step 1: stream the BINTABLE from the socket | consumed the staged `cmb_cube_1024.f16` (the same projection, run 2026-09-12) | the raw FITS was deleted after that bake, and triton was at **4.9 GB free**, below the brief's 6 GB fetch floor. The FITS branch is written, kept and exercised by `--selftest` |
| 5 | §6.1.3: the importer writes through `tools/cosmodata/csdata.py`; Core reads through `Core/Data/CsData.cs` | both inlined, marked `ponytail:` in both files | neither integrator-owned file exists on master yet (P0.2). The **byte layout is exactly §6.1.1**, so the swap is a delete-and-call in each place, not a reformat |
| 6 | §2482/§2507: particle horizon 14.26 Gpc | **14.147 Gpc, integrated** | §2508 itself marks 14.26 "INFERRED until the integration is recorded in CmbModel". This is that integration. See §7 |
| 7 | §C.12 Core path `Core/Cmb/{CmbSky,CmbModel}.cs` (C19), §B says `Core/Sky/` | `Core/Cmb/` | C19 relocated it; §B is superseded |
| 8 | §6.2.2: stdlib only, numpy the exception | stdlib + numpy, **and a 40-line PNG reader written here** | the staged mask is a PNG and Pillow is not a permitted dependency. Rather than add one, `read_png_gray8` decodes 8-bit non-interlaced greyscale with `zlib` + `struct` |

Not touched, deliberately: `docs/data-sources.md` and `SA/manifest.json` are regenerated by
`ledger.py --write` at merge time and §9.6 says no lane may write them. **Integrator: the row
for this layer is `cmb/cmb_sky.cscb`, 6,688,616 B, licenceTag `pd-ack`, licenceProfile
`commercial`, droppable `true`, credit C29** (the full attribution string is in §2 and inside
the file).

---

## 11. What the runtime lane must know

1. **Frame.** Everything in `CmbSky` is ICRF. The file contains no Unity axis. The ICRF→Unity
   basis change belongs to you and **must be the same one the star layer uses** — this is
   `DATA-LICENCE-AMENDMENT` §6.4 open item 13, and it is the one failure mode none of the
   tests above can catch.
2. **Import settings: R8, linear, no sRGB, no compression, mips on.** A block format destroys
   the measurement (§3).
3. **Face upload.** Storage is face-major (order +x, −x, +y, −y, +z, −z), then row j, then i,
   with **row j = 0 at t = 0**. That Unity's `Cubemap.SetPixelData(bytes, 0, (CubemapFace)f)`
   agrees on Vulkan is still **INFERRED**. The `cmb-faces` play-mode assertion (§11.3) must
   sample the uploaded cube *through the GPU* along the six face-centre directions and the
   Cold Spot and compare with `CmbSky.Sample`. If a face is flipped, **flip it at upload — the
   data never change.** `CmbSky.TexelDirection(face, i, j, faceSize)` gives you the expected
   direction for any texel, and `CmbSky.Sample` the expected value.
4. **Decode in the shader** as `uK = raw * 3.9215686274509802 - 500.0` (the scale and offset
   are in the meta block's `t` column encoding; read them, do not hard-code them).
5. **Colour is not baked.** `CmbSky.FalseColour(uK, contrastUk, palette)` is the reference
   implementation for the shader ramp. The UI's `cmb.contrast` is ±100…±500 with a ±250
   default, and `cmb.palette` is Diverging / Grey / Inverted.
6. **The dipole is not in the texture.** `cmb.mode` = Anisotropy / +Dipole / Monopole+dipole+
   anisotropy maps to `CmbFrame.None` / `CmbFrame.Sun` etc. Adding the dipole is
   `A * dot(n, apex)` in µK with A and apex from `CmbModel.Dipole(frame, ...)`. Do **not**
   re-bake it into the texture: at ±500 µK encoding it would clip instantly (it is 47× the
   anisotropy).
7. **The legend must state the mode, the contrast, the palette and the frame** — PLUS-05's
   own requirement — and it must say **WMAP**, never Planck, about the map.
8. **25.2 % of the sky is Inferred.** `IsMasked` / `ProvenanceAt` tell you where. Fade, hatch
   or label it; do not present it as a clean measurement.
9. **Both forms draw at χ* = `CmbModel.LastScatteringDistanceKm` = 4.28e23 km**, not at the
   particle horizon, which is a different surface 2 % further out.
10. Angular resolution is **1°**. Anything the app says about small-scale CMB structure is not
    supported by this product, and LAMBDA says so itself.

---

## 12. `docs/ATLAS-PARITY.md` rows

| Row | Assigned | Status |
|---|---|---|
| **PLUS-05** — the CMB both ways: 1.2 m ball at the top rung, backdrop at any scale, Cold Spot where it is measured, legend states mode/contrast/palette/frame | `cmb` | **Data half delivered.** The map, the mask, the Cold Spot at −105.6 µK (measured, §5.2), and every constant the legend needs (mode, contrast range, palette, frame) exist and are tested. The **render half is deferred to the runtime lane** — the object form, the sky form, the `CmbBackdrop` toggle, the 0.95/1.05 R hysteresis switch and the legend itself are `Runtime/Layers/Cmb/**`, which needs the P0 shader and layer foundations that do not exist yet. Nothing in this lane blocks it; §11 is the handover |
| **MWA-48** — every dataset re-derived by a `tools/cosmodata/` importer into a documented container under `SA/`, listed by `manifest.json` | integrator | `cmb/` now exists (6.69 MB, one importer, reproducible). `manifest.json` still does not — integrator-owned, §10 gives the row |

No other parity row names `cmb`.

---

## 13. Reproducing this

```sh
cd ~/projects/CosmoSynth-wt/data-cmb
python3 tools/cosmodata/healpix_nest.py      # HEALPix self-check
python3 tools/cosmodata/build_cmb.py --selftest
python3 tools/cosmodata/build_cmb.py         # 5 s, writes cmb/cmb_sky.cscb
./run-tests.sh Cmb
```

`data-staging/` is untracked and so lives only in the main checkout; `build_cmb.py` finds it
there automatically when run from a worktree.

To rebuild from the raw FITS instead (needs ~30 MB of disk and a LAMBDA fetch at 0.16 MB/s):

```sh
curl -o /tmp/ilc.fits https://lambda.gsfc.nasa.gov/data/map/dr5/dfp/ilc/wmap_ilc_9yr_v5.fits
python3 tools/cosmodata/build_cmb.py --in /tmp/ilc.fits --scale-to-uk 1000
```

---

*Lane `data-cmb`, 2026-09-12. 161 passed / 1 known-red; `tools/gate-all.sh a` exits 0.
G0b–G0e fail identically on `master` (whole-run exit 10 there too) and none of their
findings is in this lane's files. G0f, G0g and G0h do not exist yet and were reported
ABSENT, so nothing here claims they passed.*
