# VISUAL-PLAN — the five visual technique documents, reconciled

Editor's pass, 2026-09-12. Inputs: the five documents in `docs/design/visual/`, read in full:
`image-pipeline.md` (1,052 lines), `starfield.md` (1,193), `galaxy-and-sky.md` (1,203),
`planets-and-air.md` (1,262), `staging-and-bridge.md` (1,232). Nothing on the box was modified
except this file and the appendix it points back to.

**Tags.** **V** = verified (read in one of the five documents at its own V tag, or computed by me
on triton in this session — the script is `/tmp/recon.py`, output quoted inline). **I** = inferred.
Every millisecond in this document is **I**; §6 names the one probe that fixes them all.

---

## 0. The verdict, on one page

| Axis | Ceiling | Worst reconciled frame | Verdict |
|---|---|---|---|
| Draw calls | 150 | **116** (rungs 0–2, body-rich) | **PASS**, 23 % spare |
| Vertices in view | 1.2 M | **1.092 M** (rungs 0–2) | **PASS**, 9.0 % spare — *if* "vertices" means mesh vertices, §1.4 |
| Transparent fragments | 3.0 eye-screens | **1.95** (rungs 9–11) | **PASS** once the sky backdrop is reclassified opaque |
| Point sprites | 0.5 eye-screens | **0.329** at the governor setpoint | **PASS**, and 100 k stars is where it breaks (0.549) |
| Raymarch | 33 step-coverages | **30.04** (rungs 9–11) or **8.04** (rung 0, inside the air) | **PASS**; the two never co-occur |
| Texture | 100 MB | **61 MB** | **PASS**, and **26 MB below** the contract's own rows |
| APK | 150 MB | ~**55 MB** | **PASS** |
| **GPU, per eye** | **11.0 ms** | **4.35 ms at κ = 0.06** / **12.70 ms at κ = 0.338** | **THE ONE FAILURE, and it is one number** |

**It busts on exactly one axis, and only for one value of one unmeasured constant.**

κ is the milliseconds per step-coverage for a 3D-texture march step. The only device-measured
anchor this project owns is Fractarium's **0.338 ms/step-coverage**, and that figure is explicitly
an *upper bound* — a Mandelbulb distance estimate with foveation off, not a trilinear fetch. The
Galaxy volume's 30 steps at coverage 1.0 is 90 % of the raymarch budget and, at that upper bound,
77 % of the whole GPU frame.

Computed on triton (**V**):

```
Regime A (rungs 9-11), everything except the volume  = 2.55 ms
Galaxy volume + sky bake                             = 30.043 step-coverages
break-even  kappa = (11.00 - 2.55) / 30.043          = 0.2813 ms/step-coverage

  kappa = 0.060 (inferred)  ->  volume  1.80 ms  ->  frame  4.35 ms   PASS
  kappa = 0.338 (measured upper bound) -> 10.15 ms -> frame 12.70 ms  FAIL by 15.5 %
```

So the honest statement is not "it might not fit". It is: **the design fits 11 ms for any κ below
0.281, which is 83 % of the only measured anchor, and the first cut on the list buys unconditional
safety.** At B = 20 steps the break-even rises to **κ = 0.4216**, above the measured upper bound,
so a 20-step volume fits *whatever the probe says* (**V**, computed).

Everything else in the five documents is not merely inside budget — three of them actively return
budget. Against the contract's own §7.3 rows the reconciled design is **−18 draw-call ceilings,
−0.291 M vertex ceilings, −26 MB of texture** (**V**, computed), from four structural decisions:
the four-layer volume collapse (galaxy-and-sky A1), the nine-atmospheres-become-one draw rule
(planets-and-air §5.5), the CMB face 1024 → 512 (A3), and packing + compressing the planet maps
(planets-and-air §4.2).

---

## 1. The reconciled frame budget

### 1.0 Why there are two worst frames and not one

The five documents each cost "the frame" and each means a different one. There are exactly two
binding frames and they bind different axes:

| | **Body-rich frame**, rungs 0–2 (`sky`, `leo`, `planet-hand`, `system`) | **Galaxy frame**, rungs 9–11 (`nearest` … `clouds`) |
|---|---|---|
| Binds | **draws** and **vertices** | **step-coverages** and **GPU ms** |
| Why | every solar body, ring, spacecraft, orbit ribbon and UI panel is up | the enclosing volume is at coverage 1.0 and nothing else marches |
| Backdrop | not drawn (`RoomDefault = 1.00` ⇒ `_Immersion = 0`) | drawn (`RoomDefault` 0.70 → 0.35) |
| Volume | closed (window opens at log10 U −13.7) | enclosing, 30 steps |
| Atmospheres | 1 draw (Earth), or 8 step-cov in Envelope | 0 — no body clears the 127 px rule |

The contract's §7.3 row ceilings sum to **203 draws and 1.789 M vertices** against limits of 150
and 1.2 M (**V**, computed). That is not a defect; the rows are windowed and the totals row says
"worst simultaneous". Any reconciliation that sums ceilings will appear to bust and will be wrong.
Both tables below are **worst simultaneous**, per frame.

### 1.1 Draw calls — ceiling 150

| Row | Contract | Reconciled | Body-rich frame | Galaxy frame | Source of change |
|---|---|---|---|---|---|
| `cmb` sky form → **sky backdrop** | 1 | 1 | 0 | 1 | now carries CMB + band + zodiacal + airglow in one fragment (galaxy-and-sky A4, staging §3.3) |
| **sky bake** (offscreen, one face/frame) | — | 1 | 0 | 1 | new; 0 at rest (galaxy-and-sky §2.4) |
| sky stars, 146 rows | 1 | 1 | 1 | 1 | |
| `stars` chunks | 64 | 64 | ~20 | ~20 | cone cull; starfield §11 |
| `galaxiesLocal/2mrs/cf4` | 16 | 16 | 0 | 0 | |
| `globulars`, `cepheids`, `masers`, `belts` | 6 | 6 | 0 | 6 | |
| `dust` + `nebulae` + `dust` voxel-LOD | 1 + 3 + 8 | **1** | 0 | 1 | **A1**: four rows collapse to one volume |
| Bodies: spheres, rings, atmospheres | 60 | **52** | 52 | 2 | **−8 atmospheres** (planets-and-air §5.5) |
| Body markers | 1 | 1 | 1 | 1 | |
| Spacecraft | 20 | 20 | 20 | 0 | |
| Ribbons (incl. `dustExtent`, nebula bounds) | 16 | 16 | 16 | 8 | |
| UI | 6 | 6 | 6 | 6 | |
| **Row-ceiling sum** | **203** | **185** | | | **Δ −18** |
| **Worst simultaneous** | ≤150 | | **116** | **47** | **PASS** |

The 8 atmosphere draws removed by the `D ≥ 2/(k−1)` rule are exactly what pays for the 3 new rows
(bake, backdrop, volume). galaxy-and-sky §8.2 reported 158 draws and waved it away as "a worst case
that never happens"; with planets-and-air's rule applied the worst case never reaches 150 in the
first place, and no hand-waving is needed.

### 1.2 Vertices — ceiling 1.2 M

| Row | Contract | Reconciled | Body-rich | Galaxy | Note |
|---|---|---|---|---|---|
| `stars` | 480 k | 480 k (mesh **478,452**, **V**) | 480 k | 480 k | 119,613 × 4; **1,548 spare** |
| `galaxies` | 340 k | 340 k | 0 | 0 | 43,500 quads after the cz cut |
| `globulars` etc. | 60 k | 60 k | 0 | 60 k | |
| `dust` + `nebulae` + voxel-LOD | 24 + 72 + 295 k | **24** | 0 | 24 | **A1** deletes the 295 k voxel-sprite row |
| sky backdrop | 2 k | 2,048 | 0 | 2,048 | |
| sky bake quad | — | 4 | 0 | 4 | not stereo-instanced |
| Bodies | 180 k | 180 k | 180 k | 0 | unchanged; planets-and-air adds **zero** geometry |
| Markers / spacecraft / ribbons / UI | 1 k / 110 k / 300 k / 20 k | same | 431 k | 170 k | |
| **Row-ceiling sum** | **1.789 M** | **1.498 M** | | | **Δ −0.291 M** |
| **Worst simultaneous** | ≤1.2 M | | **1.092 M** | **0.716 M** | **PASS, 9.0 % spare** |

Vertex *buffer* bytes fall too: starfield's 24 B layout against Appendix A.4's 32 B saves **3.8 MB**
(15.3 → 11.5 MB) by deleting the `shape` field the halo/spike quads needed.

**Superseded:** image-pipeline §5.3's "43,828 star vertices, a saving of 136,000" is arithmetic on
a 9,000-star sky shell that starfield §7 deletes. It is not wrong; it describes an architecture that
no longer exists. Use 478,452.

### 1.3 Fragments — ceiling 3.0 eye-screens transparent, 0.5 of it point sprites

Eye = 2064 × 2208 = 4,557,312 px (**V**, contract §2.0, itself **I**).

| Row | Galaxy frame | Body-rich frame | Note |
|---|---|---|---|
| Sky backdrop | **opaque 1.0** | 0 | **reclassified** — `Blend One Zero` with `_Immersion = 1` is a write-only fill, no read-modify-write |
| Galaxy volume | ≤1.0 | 0 | |
| `stars` | **0.329** | **0.078** | starfield §11, computed over the real catalogue; the contract's row said 0.5 |
| Bodies (opaque) | 0 | 0.28 | Earth at 784 px |
| Atmospheres | 0 | ≤0.5 | eight shells removed from the typical frame |
| Rings | 0 | 0.3 | |
| Point sets, ribbons, boundaries, UI, markers | 0.62 | 0.51 | |
| **Transparent total** | **1.95** | **1.39** | **PASS** against 3.0 |

galaxy-and-sky §8.2 reported 3.5 and busting. The overrun was entirely the backdrop counted as
transparent. It is opaque, the document says so itself as fix (i), and this reconciliation takes it.
**Amendment A4 must therefore add the `sky backdrop` row to §7.3 as opaque, not transparent** —
otherwise the `budget` assertion will sum it into the transparent column and fail a frame that is
correct.

Point sprites (**V**, computed): 8,920 stars from Earth = **0.078**; the governor setpoint of
60,000 = **0.329**; 100,000 = **0.549 and FAILS**. `N_target = 60,000` is the guard and it has
52 % of headroom to the failure point.

### 1.4 The one ambiguity that could double the vertex row

The contract says "**Vertices processed** at most 1.2 M". Under single-pass instancing the vertex
shader runs once per eye, so *invocations* are 2× mesh vertices. If 1.2 M means invocations, the
body-rich frame is **2.183 M and busts by 1.82×** (**V**, computed) — and the `stars` row alone
(480 k) would already be 80 % of the budget.

It almost certainly means mesh vertices in view: the 1.2 M is stated against Meta's 1.3–1.8 M
*triangle* guidance, and the per-layer rows only make sense that way. **But it is not written down,
and it is a factor of two on the second-tightest axis.** Settle it headlessly: capture a known view,
read `UnityStats.vertices` from the play-mode harness, and compare against the sum of the enabled
meshes. One line in the `render:` diagnostic; one paragraph in §7.3. Do it in Phase V0.

### 1.5 Raymarch — ceiling 33 step-coverages (B = 30 shared + 3 bodies)

| Frame | Volume | Bake | Atmospheres | Total | |
|---|---|---|---|---|---|
| Galaxy (rungs 9–11) | 30.0 | 0.043 | **0** — no body clears 127 px at 10 pc/m | **30.04** | PASS |
| Body-rich, outside the air | 0 | 0.043 | **2.9** (Earth at coverage 0.29) | **2.94** | PASS |
| Body-rich, **inside** the air (`sky`, `leo`) | 0 (window closed) | 0 | **8.0** (N = 8 floor at coverage 1.0) | **8.04** | PASS, borrowing 5 from an unspent B |

**A finding neither document had:** galaxy-and-sky's Regime A totalled 33.0 of 33 — zero margin —
because it carried the contract's 3 step-coverages of body atmospheres alongside the volume. Under
planets-and-air's `D ≥ 2/(k−1)` draw rule no atmosphere draws at 10 pc/m at all, so the real figure
is 30.04 and **the atmosphere rule buys back 3 step-coverages of margin at exactly the frame that
had none.**

One consequence for the gate: the Envelope floor borrows 5 of B = 30 while nothing else marches, so
the G1c **`volumes` assertion must check the summed step budget, not the count of enclosing
volumes**. One-line change; state it in the amendment.

### 1.6 GPU milliseconds — ceiling 11.0 per eye

Every figure **I**. Fixed (non-march) cost of the galaxy frame, itemised (**V**, computed):

| | ms |
|---|---|
| Sky backdrop, 1.0 opaque eye-screen, 4 fetches | 0.45 |
| `stars` (fill ≤0.11 + vertex ~0.34) | 0.45 |
| `globulars`, `cepheids`, `masers` | 0.20 |
| `dustExtent` + nebula boundaries | 0.05 |
| Ribbons in view | 0.40 |
| UI | 0.80 |
| Body markers | 0.05 |
| **Image pipeline** (shoulder 0.03 + dither 0.10 + bloom 0.02) | **0.15** |
| **Fixed total** | **2.55** |

| B | step-cov | κ = 0.060 | κ = 0.338 | break-even κ |
|---|---|---|---|---|
| **30** (contract) | 30.043 | **4.35 PASS** | **12.70 FAIL** | **0.2813** |
| **20** (cut 1) | 20.043 | 3.75 PASS | **9.32 PASS** | **0.4216** |
| 16 | 16.043 | 3.51 PASS | 7.97 PASS | 0.5267 |

Body-rich frame, inside the air, at κ = 0.338: bodies 3.0 + atmospheres 2.70 + stars 0.45 +
spacecraft 0.5 + ribbons 0.8 + UI 0.8 + markers 0.05 + sky stars 0.02 + image pipeline 0.15 =
**8.47 ms. PASS at both κ.** The body-rich frame is not at risk; only the galaxy frame is, and only
through the volume.

**The image pipeline's +0.15 ms is 1.4 % of the budget and it buys the response curve, the
shoulder and the dither.** Against the alternative it priced and rejected — an
`R16G16B16A16_SFloat` intermediate at +4.47 ms plus URP bloom at +1.5–3.0 ms, i.e. **6.5–8.0 ms of
11.0** — that is the single best cost/benefit decision in the five documents, and it is the reason
there is any margin to argue about at all.

### 1.7 Texture memory — ceiling 100 MB (§7.3 totals row), 150 MB overall

| | MB | |
|---|---|---|
| Planet maps, packed RGBA + `Compress(true)` | 45.00 | vs 64 MB row; **−29 MB** against today's RGBA32 decode path |
| Dust cubes, mipped (Edenhofer 128³ + Leike 185×185×135) | 7.68 | measured on triton (**V**) |
| Font atlas | 4.00 | |
| CMB, 6 × 512² R8 + 6 × 64² mask, mipped | 2.12 | **−6.3 MB** against face 1024 |
| Band + extinction cubes, runtime-generated, ping-pong | 1.57 | |
| Hero galaxy atlas, 14 tiles ASTC 6×6 | 0.46 | replaces the 0.1 MB type atlas (A5) |
| Optical-depth LUTs, 9 bodies × 16 KB, built at load | 0.14 | **0 APK bytes** |
| Ring profile 2048×1 RGBAHalf | 0.02 | |
| Contact shadow, CMB LUT, dust ramp, zodiacal table | 0.01 | |
| **Total** | **61.00** | **PASS**, 39 MB spare |
| Contract rows for the same content | 87.30 | **saving 26.3 MB** (**V**, computed) |

### 1.8 APK — ceiling 150 MB, today ~46 MB

`+4.10` dust, `+1.17` CMB (against `+4.6` at face 1024), `+0.46` hero atlas, `+0.25` night map,
`+0.97` cloud map, `+2.3` optional 5400×2700 Blue Marble, `0` for every lookup table (all built at
load from catalogue numbers). **~55 MB worst.** Not a constraint on anything in these five
documents.

---

## 2. The cut list, ordered

Trigger: the κ probe returns above **0.281 ms/step-coverage**. Nothing below is needed otherwise,
and nothing below should be pre-emptively applied — three of these cost real picture.

| # | Cut | Recovers at κ = 0.338 | Visual cost |
|---|---|---|---|
| **1** | **Volume B: 30 → 20 steps** | **3.38 ms** → frame 9.32, **PASS at any κ ≤ 0.42** | First step 5.8 → **9.25 pc**; step at the near-box edge 94 → **132 pc** (**V**). Because the march is log-spaced, the *fraction* of steps inside the near box (39 %) and inside the measured 1248 pc (55 %) is **identical at N = 30, 20 and 16** (**V**) — the cut coarsens resolution uniformly in log distance rather than sacrificing the near field. The Local Bubble wall softens; the Great Rift does not move. **This is the cut to take.** |
| **2** | Hand the volume to the bake above coverage 0.6 | up to 10 ms in frames where the viewer is not inside measured dust | The near dust loses its parallax when you are not near it. Only correct because the bake is computed from the same `J` and `ρ` — it is an LOD, not a substitution. Needs the §3.5 continuity capture to prove the handover. |
| **3** | Volume B: 20 → 16 | a further 1.35 ms | First step 12.2 pc — now coarser than the Leike cube's own 4 pc voxels, so the near cube stops earning its 3.37 MB. Take cut 4 first. |
| **4** | Nebula field loop off outside the three close-ups (`_NebCount = 0`) | ~24 ALU × 20 steps, unmeasured | Nothing: the AABB test already skips them; this removes the test itself. Free, do it regardless. |
| **5** | Sky backdrop sphere → screen-space triangle | ~0.05 ms + 2 k verts | None. Book-keeping. |
| **6** | `CS_Dither` off in `Volume` and `Sky` (`_CS_Image.w = 0`) | ≤0.10 ms | Banding returns in precisely the two shaders where it is worst — the dust volume and the CMB fill. **Last resort**, and if the foveation problem (§6, Q4) forces it anyway, the B10G11R11 band cube is what keeps the Great Rift's edge smooth. |
| **—** | **`N_target` 60,000 → 40,000 stars** | **0.036 ms** | **Do not.** 0.11 eye-screens of a 3.0 budget, for the one row the owner actually complained about. Listed only so nobody proposes it. |

**Read the list as one sentence: there is exactly one expensive thing in this frame and it is the
Galaxy volume's step count.** Cuts 1–3 are all the same knob. Everything else is noise, and the
noise includes the entire image pipeline.

---

## 3. Dependency order

### 3.1 Tier 0 — nothing visual may land before these

| | Blocks | Owner |
|---|---|---|
| **0a** `StandardFrames` ICRF→galactic signs fixed, with its test vectors | every field in galaxy-and-sky (all evaluated in galactic pc), shot V4, the `plane` assertion. The pole is at (−0.346, 0.176, 0.922) instead of (0,0,1) — **22.8° of tilt that would read as a compositional choice** | `p0-core` |
| **0b** `lane/foundation-shaders` to R1/R7/R30: `CS_EyePositionWS`, `CS_JitterDir`, `_Fade` on `Body.shader`, all seven `GetCameraPositionWS()` sites, and `stereo_gate.sh` extended to grep for all three **with its own self-test** | every shader change in all five documents. planets-and-air §4.3 verified that the branch's "stereo gate green" report checks none of these | `p0-shaders` |
| **0c** The Sun's point light re-parented off `RootIds[0]` (= 61 Vir after the exoplanet merge) | every lit-body claim in planets-and-air, every terminator capture, H5's 78° ± 8° lighting rule. **Not mentioned in any of the five documents** — it is in HANDOVER §6 and it invalidates any judgement of §4 | integrator |
| **0d** Capture fov derived from `PixelsPerRadian`; capture at the device eye resolution | *validating* every pixel-denominated constant: α = 2.2 px, the 5 px quad floor, the 127 px atmosphere threshold, the 2.5 px body floor, the 3 px galaxy fallback, the 170 px label threshold. The harness evaluates all of them at 936 px/rad against the device's 1140 — **21.8 % too generous** (**V**, staging §1.3). Does **not** block writing the constants | integrator |
| **0e** The κ probe on device | trusting any millisecond, and the choice of B | integrator + `milkyway` |

### 3.2 Tier 1 — Linear before any brightness constant

`lane/foundation-code` + `lane/foundation-shaders` merged (URP 17.5, Linear, one camera) precedes
**every** re-tune, because each of these was chosen by eye against a Gamma framebuffer where the
shader's output *was* the sRGB code:

- image-pipeline §14's whole table (`_ShapeGain` 0.35 → 0.101, `_Ambient` 0.05 → 0.0018, every `^2.2`)
- starfield's `A_min`, ε, `_Spike.z`, the desaturation window
- planets-and-air's `_NightGain`, `_CloudTint`, the 0.55 cloud-shadow strength, `_StarIrradiance`
- galaxy-and-sky's `j0`, `_BandTint`, the CMB LUT anchors
- staging-and-bridge's deck and chrome hexes

Re-tuning before the merge is work thrown away twice: once for the colour space and once for the
response curve that replaces the knob.

**Caution carried forward:** planets-and-air §12 flags that its own retune values use "illustrative
Gamma-era numbers" and that the lane must read the shipped constants before applying `^2.2`. That
warning applies to image-pipeline §14's table as well — `_Ambient = 0.05` is an example, not a read.

### 3.3 Tier 2 — data before the shaders that read it

```
build_dust.py  ──►  j0 calibration (pole anchor needs measured extinction)  ──►  Milky Way band
      │
      └──────────►  the bake's tau  ──►  _ExtCube  ──►  the star layer's final amplitude
                                              │
                                              └──►  the ExtCube == 10^(-0.4 av) identity test
Core/AirTable.cs  ──►  CosmoSynth/Atmosphere  AND  CosmoSynth/Body's Teye   (one table, two readers)
build_cmb.py --face 512   ──►  independent of everything above
```

Three hard orderings inside that:

1. **The dust cube before the Milky Way band.** `j0` is calibrated on the galactic pole, where the
   published ISL must be compared against the catalogue's own contribution *and* where extinction
   must be known to be negligible — which is a measurement from the cube (E = 0.021, A_V ≈ 0.10,
   **V**). Calibrate on the pole, **validate** on the plane at l = 90°, never fit both.
2. **`_ExtCube` before the star layer's amplitude is judged.** See §4, conflict C5 — the ordering
   *inside the vertex shader* matters too.
3. **`AirTable` before both the atmosphere and the body shader.** They read the same table with the
   same parameterisation; that is what makes Rigid and Envelope one code path.

### 3.4 Tier 3 — one exposure knob, published before anything is judged

`m_cut` is published as a single global before any star, band or sky constant is tuned, because
three systems target it: the star layer's amplitude, the band's gain, and the `BRIGHTNESS ×N` chip.
The per-rung table is generated before the seven views are composed, or each view is exposed by
whatever the governor happened to pick that frame and the captures are not comparable.

### 3.5 Tier 4 — composition last, harness first

The shot cards and the `compose` gate come after the content exists. The *harness* fix (0d) comes
before everything, because it is the only thing that makes any pixel claim in the other four
documents checkable.

### 3.6 One dependency that inverts

starfield §15 states: *"Nothing depends on the image-pipeline topic, by construction (§8)."* After
conflict C1 is resolved that is no longer true, and it inverts: **the star layer owns the point
response outright and the image pipeline's γ is deleted from that path.** The image pipeline
depends on the star layer for `m_cut`, not the other way round. The `stars` lane must be told, or it
will ship `_Look.x` expecting an `EV_pipeline` that no longer exists.

---

## 4. Conflicts between the five documents, resolved

Seventeen. Each resolution is one line of reasoning and an instruction a lane can act on.

### C1 · The star response curve — γ compression vs exact flux · **starfield wins**

image-pipeline §6.2 compresses amplitude as `b = ℓ^0.28`; starfield §2.3 keeps flux exact and lets
the compression emerge from the point-spread wing (radius ∝ A^0.25) plus an in-shader Reinhard.

**They cannot both apply.** starfield derives the quad from the amplitude
(`u_max = sqrt(sqrt(A/ε) − 1)`), so pre-compressing A makes the apparent radius go as
`(A^0.28)^0.25 = A^0.07` — essentially constant size at every magnitude, which is the *exact defect*
both documents were written to fix. Exact flux, then the wing, then the tone curve.
**Delete `CS_RESPONSE_GAMMA` from the point path.** It survives nowhere else.

### C2 · Geometric bloom — extra quads vs analytic wing · **starfield wins**

image-pipeline §5.2–5.5 keeps `Point.shader`'s halo and two spike quads for the 679 stars at
m ≤ 4.25 (+8,148 vertices, 1,446 px² per bloomed star). starfield §3–§4 deletes all three and makes
the halo the Moffat wing and the spikes an analytic separable rational in the fragment.

Analytic wins on four counts, none of them taste: **0 extra vertices** against 8,148; correct in
layer mode, where *which stars are bright* depends on where the viewer is, so a baked bright-set
goes stale the moment anyone travels; the wing is at the right size for each star's own flux by
construction rather than by a gate; and it deletes `_Shape`, `_ShapeGain`, `_MagWindow` and
`_SizeDmm` rather than re-tuning them.

The `grow` guard of image-pipeline §5.5 dies with the Gaussian it guarded: a Moffat's peak is
exactly 1, so a saturated core cannot grow past its crop, and `A_max` caps the quad *algebraically*
(`A_max = ε(1 + (halfMax/α)²)²` ⇒ `halfPx(A_max) = 24.00 px` exactly, **V**).

**What image-pipeline §5.3 keeps:** the measured magnitude census (679 stars at m ≤ 4.25 = 7.6 % of
the naked-eye sky, counted on triton) is still the right sanity check on starfield's spike onset,
which admits ~180 partial and ~50 full. The two agree on "the brightest few per cent".

### C3 · Which tone operator goes in `Point` · **unify, do not choose**

image-pipeline §4.3 gives `Point` no shoulder; starfield §8.3 puts `c/(1+c)` in it behind `_Look.y`.
These are the same family: extended Reinhard `c(1 + c/W²)/(1 + c)` becomes `c/(1+c)` as `W → ∞`.
Write **one** function and let `W ≤ 0` select the pure form:

```hlsl
// CosmoCore.hlsl -- one shoulder, two callers. f(0) = 0 exactly in both branches,
// which is the whole passthrough rule (image-pipeline 2.2).
half3 CS_Shoulder(half3 c, half W)          // W <= 0  =>  pure Reinhard, no white point
{
    half L = max(dot(c, half3(0.2126h, 0.7152h, 0.0722h)), 1e-5h);
    half n = (W > 0.0h) ? (1.0h + L / (W * W)) : 1.0h;
    return c * ((L * n / (1.0h + L)) / L);
}
```

`Body`, `Volume`, `Sky`, `CosmoAtmosphere`, `Ring` pass `_CS_Image.z` (default 4.0). `Point` passes
0. Eleven shaders, one operator, one `f(0) = 0` proof, one G1c `image` assertion covering all six.

### C4 · Two exposure knobs, `m0` and `m_cut` · **one knob, and they already agree**

They are algebraically the same number: `m0 = m_cut + 2.5·log10(A_min)`. Computed on triton (**V**):

```
m_cut = 6.5, A_min = 0.009  ->  m0 = 1.3856
shipped hand-tuned _MagWindow.y = 1.5
disagreement = 0.114 magnitudes = a flux ratio of 1.111
```

**Two exposure anchors derived independently, from different reasoning, by two documents that did
not read each other, agree to 11 %.** Neither is arbitrary, and that is the strongest evidence in
the whole reconciliation that the photometry is right.

Ship **`m_cut` as the one published global**. image-pipeline §6.3's per-rung P99.5 + 3.0 generator
produces its *default* per rung; starfield §2.4's per-chunk histogram bisection is allowed to move
it to hold `N_target`, but only within **±1.5 magnitudes** of the table — the same clamp
image-pipeline already put on adaptation, and for the same reason: the seven fixed capture views
must stay reproducible. `BRIGHTNESS ×N` reports `10^(0.4(m_cut − 6.5))`. One definition, one chip,
one number in the report line.

### C5 · Extinction applied exactly once — **and the order inside the vertex shader**

galaxy-and-sky §3.4 requires `CosmoSynth/Point` to sample `_ExtCube` and multiply; starfield §10's
shader has no such fetch. galaxy-and-sky wins (it owns contract R41). But the naive merge is wrong
twice, and the lane will get it wrong unless it is written out:

1. **`Tstar` must be applied before `ratio = A/ε`**, or a star behind the Coalsack gets a quad sized
   for its *unextinguished* flux and the profile is cropped above ε — the soft-square defect,
   reintroduced through the back door on precisely the reddest stars.
2. **Extinction reddens as well as dims**, so it cannot simply multiply `o.colour.rgb` after the
   luminance divide (that would change the star's V magnitude too). Split it:

```hlsl
half4 T     = SAMPLE_TEXTURECUBE_LOD(_ExtCube, sampler_ExtCube, dirIcrfFromEye, 0);
half  Tstar = lerp(T[min(s,3)], T[min(s+1,3)], (half)frac(g));      // scalar, per galaxy-and-sky 3.4
A *= (float)Tstar;                       // (1) dim FIRST -- the quad, the cut and A_max all follow A
float ratio = A / max(_Photometry.w, 1e-8);
if (ratio <= 1.0) { o.positionCS = CS_CULLED; return o; }           // LOD sees the DIMMED star
...
chroma *= _ExtTint.rgb;                  // (2) then redden, luminance-neutral by construction
```

Cost: **+1 cube fetch over ~300 k vertex invocations per frame**, and `_ExtCube` is already paid for
(1.57 MB shared with the band cube). The identity test — `ExtCube(dir, d) == 10^(−0.4·av)` from the
Sun, for every star — is pure Core, needs no GPU, and is jointly owned by the `stars` and
`milkyway` lanes. galaxy-and-sky calls it "the single most valuable test in this document" and it
is: it fails loudly on a sign flip, a `toAv` mismatch, a re-dimmed `vmag` or a cube swap.

### C6 · Three documents all edit `CosmoSynth/Sky` · **one file, one owner, one commit**

galaxy-and-sky A4 adds `_Mode`, `_BandCube`, MRT and the zodiacal/airglow terms; staging §3.3 makes
Sky the single room-dial renderer with `_Contrast = 0` and a 1×1 flat LUT. Not a conflict — both are
uniforms on one renderer, and staging's flat-LUT case is simply `_Gains.w` with the band, zodiacal
and airglow terms at their own gains. But `Runtime/Shaders/**` belongs to `p0-shaders`, and two
lanes editing one fragment function is how a file gets whole-file-synced. **Both change sets land in
one `p0-shaders` commit**, with the merged fragment written out once.

### C7 · Sky backdrop: transparent or opaque? · **opaque, and §7.3 must say so**

Resolved in §1.3. `Blend One Zero` with `_Immersion = 1` is a write-only fill; at `_Immersion = 0`
the layer does not draw at all; at intermediate values it still writes without reading. Costs an
opaque fill in all cases. Reclassifying it is what takes the frame from 3.5 to 1.95 eye-screens.

### C8 · The room dial switches off the two things that stop rung 0 being black · **real, and unseen by both**

galaxy-and-sky §7 adds zodiacal light and airglow so that *"a black sky at 'Sky from Earth' is simply
wrong"*. Both ride `_Gains` on the Sky backdrop, which is multiplied by `_Immersion`. staging §3.5
sets `RoomDefault = 1.00` at rungs 0–9, so `_Immersion = 0` and **the backdrop is never drawn at the
rung the feature exists for.**

The resolution is that this is *correct in MR* — in passthrough the room **is** what replaces the
black sky, and adding a grey wash over a lit living room is the haze failure image-pipeline §8.2
spends a page avoiding. But it has two consequences that must be written down:

1. **Zodiacal light and airglow are Space-mode content at rungs 0–9.** The seven fixed views must
   include a `rung:sky` capture with `room:0`, or the feature ships unseen and unjudged.
2. galaxy-and-sky §2.6 defers a passthrough band for the same reason. **These are one Phase-3 item,
   not three**: a single additive `CosmoSynth/Point` draw in `_Space = 2` over a subdivided sphere,
   vertex-coloured from the backdrop's own radiance on the CPU, carries the band, the zodiacal light
   and the airglow into passthrough at once. ~5 k verts, 1 draw, already gate-clean.

### C9 · ε = 0.003 over a lit room · **one experiment, not two**

starfield H-S1 (is the faint-end cut right over passthrough?) and staging §3.1's `L_room` sweep are
the same measurement taken for two reasons. Merge into one headset item; see §6 Q2.

### C10 · `_StarIrradiance` vs the surface gain `G` · **remove a knob rather than reconcile two**

planets-and-air §5.3 unclamps `_StarIrradiance = (1 AU/d)²·(L/L_sun)` and warns "do not tune it
before the HDR decision lands". image-pipeline §6.4 defines `G = 0.75/(0.30·E_norm(focus))`.
Resolution: **`_StarIrradiance` is never tuned at all — it is computed from the ephemeris. `G` is
the only knob, and it raises `BRIGHTNESS ×N` when it departs from the rung-0 focus value.** The old
`clamp(8/au², 1.2, 26)` in `StarIntensityFor` exists only because the additive-only atmosphere had
nothing to attenuate; with planets-and-air §2 landed it has no job and is deleted, not re-tuned.

### C11 · Two things soften the terminator · **order them; they are not the same softness**

The star's angular radius (4.651e-3 for Earth at 1 AU, **V**) is geometry and is not negotiable. The
shoulder `W` is a look. Tune in image-pipeline §14's order — the terminator first because it is
physics, `W` fourth because it is taste. A `W` tuned against a knife-edge terminator will be wrong
once the penumbra is correct.

### C12 · Every pixel figure is validated at the wrong angular size · **staging wins, and it is a prerequisite**

`ppr_capture = 936` against `ppr_device = 1140` — a 21.8 % error on every pixel-denominated rule
(**V**, staging §1.3). This is not staging's local problem: it invalidates starfield's α = 2.2 px
sampling claim, planets-and-air's 127 px atmosphere threshold, the 2.5 px body floor and the 3 px
galaxy fallback *simultaneously*. It is Tier 0 (§3.1, 0d), it is one derived field of view, and
staging is right to call it the highest-value single line in its document.

### C13 · Four documents edit a 57-line `CosmoCore.hlsl` · **sequence it**

image-pipeline adds `_CS_Image`, `CS_Shoulder`, `CS_Dither`; galaxy-and-sky adds `_CS_IcrfToGal`;
planets-and-air adds `CS_EyePositionWS`, `CS_JitterDir` and (Phase 3) `_CS_EyeAir`. Land 0b first,
then all additions in **one** `p0-shaders` commit. Four lanes appending to one 57-line file in
parallel is a guaranteed conflict on the single most-included file in the project.

### C14 · Two anti-banding mechanisms for the same gradient · **both, and they are independent**

The B10G11R11 band cube removes banding in the *stored* radiance; `CS_Dither` removes it in the
*8-bit output*. Different stages, no overlap. Keep both — and note that if the foveation problem
(§6 Q4) forces the dither off, the float cube is the only thing left keeping the Great Rift's edge
smooth, which is an argument against ever downgrading the cube to RGBA8 to save 0.2 MB.

### C15 · Two star vertex counts · **not a conflict, one is superseded**

image-pipeline's 43,828 describes a 9,000-star sky shell. starfield deletes the shell (§7: the whole
3D stellar volume spans 298–457 m of render depth and differs from a 1,500 m shell by **0.19 px** of
disparity, below the contract's own 0.5 px threshold). Use 478,452 mesh vertices, 24 B each.

### C16 · The `volumes` assertion counts volumes; the budget is now a sum · **one-line gate change**

With A1 there is structurally only ever one `MayEnclose` volume, so the count check becomes trivially
true and stops being a gate. But the Envelope atmosphere floor borrows 5 step-coverages from B = 30
(§1.5). **Change `volumes` to assert the summed `LayerBudget.StepCoverage` of everything with
`Weight > 0` is ≤ 33**, which is what the budget actually is, and keep the count check as a second
clause so a future second volume still fails.

### C17 · `CS_IGN`: removed by R7, required by the dither · **scope it, do not delete it**

planets-and-air §4.3 reports `CosmoCore.hlsl` "still carries `CS_IGN` (removed by R7)".
image-pipeline §8.3 builds its dither on `CS_IGN` precisely so it needs no blue-noise texture.

R7 forbids a **per-pixel stochastic term inside a raymarch**, because two eyes rasterise the same
world point at different pixel coordinates and the sampling noise decorrelates into binocular
rivalry. A screen-space *output* dither decorrelates too — but it perturbs the final colour by
**≤ 1 LSB of an 8-bit value**, not a sampling position, so it cannot produce structured differences
between the eyes.

Resolution: **keep `CS_IGN` and scope its use.** `CS_JitterDir(dirOS)` is the ray-space jitter and
is the only thing a march may call; `CS_DitherNoise(positionSS)` wraps `CS_IGN` for output dithering
only, with the ≤ 1 LSB amplitude bound stated in the rule. Add one `stereo_gate.sh` row: `CS_IGN`
may appear only inside `CS_DitherNoise`. That turns a deletion into the scoping rule R7 actually
meant, and it keeps the gate honest — the rule as currently written would be silently violated by
the dither and nobody would notice.

*This is also the item most likely to be wrong on a headset* (§6 Q4). Ship it behind
`_CS_Image.w`, which disables it exactly at 0.

---

## 5. The phased plan, on the contract's lanes

Five visual phases mapped onto the contract's Phase 0 / 1a / 1b / 2 structure. Effort is the sum of
the five documents' own estimates (2.7 + 8.5 + 5.1 agent-days stated; galaxy-and-sky and starfield
give step lists without totals — estimated at 6 and 4).

### V0 · "the picture cannot be judged yet" — Phase 0, integrator + `p0-core` + `p0-shaders`

| Step | Lane | From |
|---|---|---|
| `StandardFrames` galactic signs + test vectors | `p0-core` | galaxy-and-sky step 0 |
| R1/R7/R30 on `lane/foundation-shaders`; `stereo_gate.sh` greps all three with a self-test | `p0-shaders` | planets-and-air step 4 |
| Sun light off `RootIds[0]` | integrator | HANDOVER §6 |
| Capture fov derived from `PixelsPerRadian`; capture at 2064 × 2208; `<view>.png` + `<view>.key.png` | integrator | staging steps 2–3 |
| The llvmpipe resolution timing run; pin the profile | integrator | staging §7.4 |
| The κ probe on device | integrator + `milkyway` | galaxy-and-sky step 1 |
| `render:` line gains `fmt`, `ppr`, `fovY`, and the vertex-counting convention (§1.4) | integrator | new |

**Done when:** `stereo_gate.sh` fails on a planted `GetCameraPositionWS()`, a planted missing
`_Fade` and a planted `CS_IGN` outside `CS_DitherNoise`; the galactic-pole test vector passes;
`abs(0.5·H/tan(fovY/2) − ppr) < 1`; κ is a number in `docs/accuracy.md`; `UnityStats.vertices` for a
known view is reconciled against the enabled meshes.

**Captures:** re-capture all seven views. **The before/after pair is the same scene at
1920×1080/60° and at 2064×2208/derived-fov** — that pair alone is the evidence that every pixel
rule in the app had been validated 21.8 % too generously.

*~1.5 agent-days.*

### V1 · "the sky has a bright end" — 1a (`stars` Core) + 1b wave 1 (`stars`, `p0-shaders`)

starfield steps 1–9, plus image-pipeline's `CosmoCore` addendum and the `_CS_Image` global, with C1,
C2, C3, C4 applied. Highest visible payoff per agent-day in the whole plan and it depends on nothing
but V0.

**Done when:** `starshape.py` passes the golden tiles and **fails** a synthetic square and a
synthetic flat-top; `AmplitudeFor(m+1)/AmplitudeFor(m) == 10^−0.4` to 1e-6; the `A_max` round trip
holds to 0.01 px; the new G1c `image` assertion finds exactly `(0,0,0,0)` in an undrawn region with
the response, the shoulder and the dither all live; the generated `m_cut` table reproduces
**m0 = 1.386 ± 0.12** at rung 0 (C4's calibration check).

**Captures:** `rung:nearest` and `rung:planet-hand`, before/after against the baseline PNG.
**The measurable claim to put under the pair:** Sirius, Vega and Deneb render as three different
brightnesses spanning **1,500 : 1**. Today all three clamp to `b = 1` and are pixel-identical — the
brightest 24 stars in the sky have no hierarchy at all, which is one direct, nameable cause of
"empty dark space".

*~4 agent-days.*

### V2 · "planets are lit" — 1b wave 1 (`solar`, `p0-shaders`), parallel to V1

planets-and-air steps 1–7. Independent of V1; same wave.

**Done when:** the nine-row `β_R` derivation test fails on five of nine rows before and passes
after; the `limb` assertion is recorded failing on the current build (a 36–50 px band against a
7.7–8.0 px geometry — **4.5–6.5× over**, **V**) and passes after; the `terminator` assertion is
recorded failing at 20 px of displacement on a 784 px disc and passes after; the count of enabled
`CosmoSynth/Atmosphere` renderers in the `default` view is **1, not 9**; `Texture.currentTextureMemory`
after the bakes is under the §7.3 row.

**Captures:** V1 (`rung:planet-hand`) and V7 (`rung:leo`). The claim: Earth's limb is a gradient
whose hue runs red→white→blue with altitude, because the surface behind it is now being *removed* at
the correct per-channel rate, instead of a flat teal band saturating in blue.

*~8.5 agent-days (the largest phase, and 4 of it is shader work).*

### V3 · "the sky is not empty" — 1a (`milkyway` data) + 1b wave 2 (`milkyway`, `cmb`, `galaxies`)

galaxy-and-sky steps 2–11. Needs V0's 0a and the κ number to size B.

**Done when:** the `ExtCube == 10^(−0.4·av)` identity holds for every star with the viewer at the
Sun; the five integrated dust columns match (GC 4.13 mag, anticentre 1.13, NGP 0.100, l=90 2.34,
Orion 5.53 — **V**, measured on this box); `j0` calibrated on the pole **validates** at the plane
within 2× without being re-fitted; the 25 pc rebake ΔL is under 1 % of the local value; `cmb-faces`
passes and the extended `cmb-fade` frame-pair mean-absolute-difference is under its threshold; the
CPU 6×32² bake fallback exists behind the same interface.

**Captures:** V4 (`rung:milkyway`) and V3 (`rung:nearest`), before/after. **This is the phase that
answers the owner's actual sentence.** From inside the disc the band *is* the sky, and it is
currently absent.

*~6 agent-days.*

### V4 · "it is aimed, and it is in a room" — 1b wave 3 (integrator, `nav`, `ui`, `fiction`)

staging steps 4–17.

**Done when:** `compose_check.py` passes all seven views and **fails two of its five rules on the
existing baseline PNG** (32.5 % of the subject below the frame; the JWST label across the status
card) — a gate with a free failing fixture on day one; mean destination alpha ≤ 0.60 at rungs 0–9;
`fiction-bounds` passes and fails when one node's semi-major axis is moved to 4.0 AU; the deck-rim
glyphs are in the gated set and `verify-apk.sh` confirms the font is inside the APK.

**Captures:** all seven views plus the before/after pair. This is G1d.

*~5.1 agent-days.*

### V5 · headset — Phase 2, first beta

The six questions of §6, in one session. Then re-tune, in image-pipeline §14's order, and write the
measured numbers into §7.3 and `docs/accuracy.md`.

**Total ≈ 25 agent-days of visual work**, inside the 38-day programme, with V1 and V2 parallel and
V3 gated on one probe.

---

## 6. What only a device can settle

Six questions. The five documents raise about twenty between them; these are the distinct ones after
merging, each with the experiment and what it decides.

### Q1 · κ, the milliseconds per step-coverage for a 3D-texture march step
**Decides:** whether B = 30 ships or drops to 20 — and therefore whether the frame is 4.35 ms or
12.70 ms. Everything in §1.6 is sized off it. This subsumes planets-and-air's separate `κ_atm`,
because the per-step work is the same class (1 trilinear 3D fetch + ~12 ALU against 1 exp +
1 RGHalf 2D fetch + ~12 ALU).
**Experiment:** one full-screen quad running the `Volume` fragment over a 128³ noise cube.
(a) Fix coverage 1.0, sweep N ∈ {4, 8, 16, 24, 32}; the slope of `App=` against N **is** κ.
(b) Fix N, sweep coverage 0.1 → 1.0; linearity confirms the unit is well defined.
Read `adb logcat -d | grep VrApi` for `App=` and `GPU%=`, with `Fov=` confirming foveation is
actually on — Fractarium's own anchor was taken with it off, which is why it is an upper bound.
**Threshold: κ < 0.281 and the design ships as written.** Build this probe first; it is
galaxy-and-sky step 1 and contract §13 item 14, and it is the single highest-value hour on the
headset.

### Q2 · `L_room`, the passthrough luminance floor — and therefore ε
**Decides:** whether flux-true faint content exists at all in MR, and whether ε = 0.003 (a
magnitude-6.5 star peaking at sRGB 23/255) reads as a pinprick or as nothing over furniture.
starfield's own history is that 0.16 was below perception and 0.40 was not — **in Gamma**, so those
numbers do not transfer.
**Experiment:** stand facing a white wall, then a dark corner. Open `rung:nearest`, step
`stars.exposure` from 0.1 to 3.0, and record the lowest value at which magnitude-4 stars are visible
in each case. The ratio is `L_room`'s dynamic range; the wall answer is the passthrough gain.
3 minutes, and it settles starfield H-S1 and staging H5 together.

### Q3 · Foveation versus a 5 px star (`_Psf.w` = k)
**Decides:** whether the sky visibly thins and shimmers toward the frame edges. SRP foveation drops
the effective core radius from 2.2 px to 1.10 px at half rate and 0.55 px at quarter rate, taking
median peak recovery from 0.940 to ~0.75 and ~0.50 (**V**, starfield §3.3's sampling sweep). This is
starfield's own prediction of "the single most likely visual complaint after this design lands", and
llvmpipe has no FFR so no test on this box can see it.
**Experiment:** same view at FFR off / Low / Medium; count detected stars in the outer 30 % of the
frame against the inner 30 %; sweep k ∈ {0, 0.5, 1, 2}; take the smallest k that closes the gap.

### Q4 · Foveation versus the dither
**Decides:** whether `CS_Dither`'s per-pixel IGN noise becomes 2×2 or 4×4 *blocks* after the
compositor upscales the periphery — which would be worse than the banding it replaced — or whether
the compositor's lens resampling smooths it away entirely, making it free and useless. Three
mitigations are already ranked in image-pipeline §8.5.
**Experiment:** the same FFR sweep as Q3. Look at the atmosphere limb and the dust volume with
`_CS_Image.w` at 0 and at 1. **Q3 and Q4 are one ten-minute session.**

### Q5 · Is `CS_EyePositionWS()` genuinely per-eye under single-pass instanced?
**Decides:** three things at once, and it is the canonical verification gap for this whole
programme — the monoscopic harness fakes the eyes by moving the camera transform, so a shader that
reads the centre eye where it should read its own **passes either way**.
**Experiment, two looks:** (a) stand inside the dust cloud in `mode:vr`; the two eyes must show
*different* marched noise. (b) Stand off Earth at `planet-room` with the ocean glint on the limb and
close one eye, then the other; the glint must move by the parallax of a surface feature, not by half
a disc. A wrong `V` on a mirror-like ocean is the most uncomfortable possible form of this bug.

### Q6 · Does anything leak coverage into passthrough under foveation?
**Decides:** whether the `alpha` assertion means what it claims. It proves the shader returns 0; it
cannot prove the compositor agrees when rasterisation is non-uniform.
**Experiment:** open a view with heavy additive content over a bright wall and look for a grey veil.
The same session answers image-pipeline's `f(0) = 0` premise — that the compositor is
premultiplied-over and colour at alpha 0 is *added* to the room, which is inferred from the
contract's queue table and is the basis of every operator in the image pipeline.

### Also unsettled, but settleable **on this box, this week**

| | Experiment |
|---|---|
| Does 2064 × 2208 render in acceptable time on llvmpipe for a 22-view release run? | `tools/shot.sh --views default` at 1920×1080, 2064×2208 and 1032×1104, timed. Fallback is 1032×1104 with the fov derived the same way — `ppr = 570`, **exactly half**, which is a far better error than 21.8 % |
| Does "1.2 M vertices" mean mesh vertices or invocations? (§1.4, a factor of two) | `UnityStats.vertices` on a known view against the enabled-mesh sum |
| Is the eye buffer really `R8G8B8A8_SRGB` at 2064 × 2208? | log `XRSettings.eyeTextureDesc.{graphicsFormat, depthStencilFormat, width, height}`. Free, and it re-scales every pixel figure in all five documents |
| The `j0` pole calibration | one CPU integral over `J` plus a sum over `stars.csstars` within 20° of the NGP |
| The 25 pc rebake threshold | bake at x and at x + 25 pc; max per-texel ΔL over the cube |

---

## 7. Amendments this reconciliation asks the integrator for

Ten, of which five come straight from the source documents and five are new to this pass. None
reverses a ruling; seven of them reduce cost.

| # | Section | Change | From |
|---|---|---|---|
| A1 | §4.5, §5.4, §7.3 | `dust`, `nebulae`, `mwDisc`, `mwBand` collapse to one `MayEnclose` volume plus one sky bake; the voxel-sprite LOD row is deleted | galaxy-and-sky |
| A2 | C.7 | Dust source and layout: Edenhofer 128³ @ 20 pc + Leike 185×185×135 @ 4 pc, `toAv = 2.88`, mips required | galaxy-and-sky |
| A3 | C.12, §7.3 | CMB face 1024 → 512; mask 6×64²; the app's own LUT | galaxy-and-sky |
| A4 | A.6, A.2, §7.3 | `Sky` gains `_Mode`, `_BandCube`, MRT, zodiacal and airglow; `CosmoCore` gains `_CS_IcrfToGal`; new `sky backdrop` row, **classified opaque** | galaxy-and-sky + C7 |
| A5 | §7.3 | Drop the 0.1 MB galaxy type atlas; add 0.46 MB for the hero LGA atlas | galaxy-and-sky |
| A6 | A.4, §7.3 | Appendix A.4 replaced by starfield §10; vertex layout 32 B → 24 B; `_MagWindow`/`_SizeDmm`/`_PxBounds`/`_Shape`/`_ShapeGain` replaced by `_Photometry`/`_Psf`/`_Spike`/`_Colour`; §5.7.5 rule 9's "shed star vertices" becomes "raise `m_cut`" | starfield |
| A7 | §7.3 Bodies row | Bodies draws 60 → 52: an atmosphere shell draws only while its disc is ≥ 2/(k−1) px. **This is what returns the 3 step-coverages and the 8 draws that make the rest of the budget fit** | planets-and-air + §1.5 |
| **A8** | §7.4 | The sentence "overlaps … now sum in linear and will read brighter" is wrong: two quads at display 0.5 give 0.5 + 0.5 = 1.00 clipped in Gamma, and 0.2176 + 0.2176 = 0.4353 → **0.687** in Linear. They read **dimmer** and keep their structure | image-pipeline §14 |
| **A9** | §7.1 rule R7 | R7 is scoped to raymarch sampling. Output dithering of amplitude ≤ 1 LSB in screen space is exempt, via `CS_DitherNoise`; `CS_IGN` may appear only inside it, checked by `stereo_gate.sh` | **new**, C17 |
| **A10** | §7.3, G1c `volumes` | The raymarch ceiling is a **sum** over layers with `Weight > 0`, not a count of enclosing volumes; the Envelope atmosphere floor legitimately borrows 5 of B = 30 | **new**, C16 |

And one clarification rather than an amendment: **§7.3's "vertices processed" must state whether it
counts mesh vertices or single-pass invocations** (§1.4). It is a factor of two on an axis with 9 %
margin.

---

## 8. Claim ledger

**Computed by me on triton in this session** (`/tmp/recon.py`, output quoted in §0–§2): the
exposure-anchor agreement (m0 = 1.3856 against the shipped 1.5, 0.114 mag, flux ratio 1.111); the
Regime A fixed cost of 2.55 ms and the break-even κ of 0.2813, 0.4216 and 0.5267 at B = 30, 20, 16;
the frame totals 4.35 / 12.70 / 9.32 ms; the log-spaced march's first step and near-box fraction at
N = 30, 20, 16 and the invariance of the 39 % / 55 % fractions; the draw-row sums 203 → 185 and the
realistic frames 116 and 47; the vertex-row sums 1.789 M → 1.498 M and the worst frames 1.092 M and
0.716 M and the 2.183 M invocation case; the point-sprite eye-screens 0.078 / 0.329 / 0.549; the
texture total 61.00 MB against the contract's 87.30 MB for the same content.

**Inherited VERIFIED**, at the tag the source document gave it: the star catalogue's 119,613 records
and the magnitude census; the sampling sweep at α ∈ {0.6 … 3.0}; the dust cube sizes and deflate
ratios and the five integrated columns and `A_V = 2.88·E`; the WMAP bake checks; `UberPost.shader:344-348`,
`ACES.hlsl:494`, `Bloom.cs:220,226`, `BloomPostProcessPass.cs:187-190`, `UniversalRenderPipelineCore.cs:1539-1552,1581`;
`stereo_gate.sh:77`'s literal grep; the baseline frame's disc geometry and the 36–50 px band against
a 7.7–8.0 px shell; the nine-body `β_R` derivation and Earth's 1.245× normalisation offset; the
terminator penumbra half-widths; the Saturn ring radii and the Cassini division's true `t`;
`Texture2D.LoadImage` → RGBA32 and Earth's 21.4 MB; `lane/foundation-shaders`' seven
`GetCameraPositionWS()` sites; the harness's `f = 936.4 px` and the 21.8 % pixel-scale error;
Reverie's shipped shadow and immersion constants; `MinimumApparentPixels = 2.5`.

**INFERRED, and flagged everywhere it matters:** every millisecond, including all of §1.6 and the
verdict itself; κ and κ_atm; the eye buffer's format and resolution; the per-eye correctness of
`CS_EyePositionWS()` under instancing; the premultiplied-over compositor model that the whole
`f(0) = 0` rule rests on; `PixelsPerRadianCentre = 1140`, which is **I** in the contract and which
every absolute pixel figure in all five documents scales linearly with; the Galaxy emissivity
constants and the Leinert ISL anchors, recalled from memory and not yet read; the STP refractivities
for eight gas mixtures; ETC2 at 8 bits/texel from `Compress(true)`; the aesthetic constants
(`_Spike.z`, the desaturation window, `_NightGain`, `W = 4.0`, `N_target`, `L_room`, `k`, the
three-act timings).

**The sentence that goes in every capture note, verbatim, every time:** *a flat monoscopic capture
on llvmpipe cannot prove stereo, passthrough, frame rate or foveation.*

---

*Sources: `docs/design/visual/{image-pipeline,starfield,galaxy-and-sky,planets-and-air,staging-and-bridge}.md`
read in full; `docs/CONTRACT.md` §7.3, §7.4, §8.1; `docs/CONTRACT-SUMMARY.md`;
`HANDOVER-ASTRA.md`; `HANDOVER-ASTRA-VISUAL.md`. Arithmetic: `/tmp/recon.py` on triton.
Nothing else on the box was read or written.*
