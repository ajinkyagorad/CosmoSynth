# Composition, MR staging, and the bridge to fiction

Design pass, 2026-09-12. Topic owner: Astra. Status: **design only** — no code, shader, setting or
data file was changed to write this. Everything below is written to be implemented by the `ui`,
`fiction`, `nav` and `lines` lanes and by the integrator, inside `docs/CONTRACT.md` as it stands.
Where I believe the contract itself is wrong, I say so in §6 with arithmetic and do not act on it.

Every claim is tagged **VERIFIED** (I read the file, ran the measurement, or did the arithmetic
here and show it) or **INFERRED** (a judgement, a guess, or a number nobody has measured yet).
Where a cost is a guess, the experiment that settles it is written beside it.

---

## 0. What this document is for, in one paragraph

Three things in the product have no design: the captures are not aimed, nothing says what a space
scene should look like *inside a real room*, and the half of the product that carries the viewer
from the measured sky into authored worlds is one JSON file and a paragraph. This document turns
those three into numbers, geometry and shader/material setups. It does not touch the image
pipeline, the star point-spread, the Milky Way band or the atmosphere — those are other lanes'
topics and this one assumes they land.

**Contents**

| § | Topic |
|---|---|
| 1 | The measurement: what the baseline frame actually is, in pixels |
| 2 | Composition: the capture geometry, the house rules, and seven shot cards |
| 3 | MR staging: the room as a luminance floor, edges, coverage, the room dial |
| 4 | The fiction bridge: the Threshold, the projector, the one rule |
| 5 | First run: the first ten seconds, second by second |
| 6 | Conflicts found in the contract while doing this, with arithmetic |
| 7 | Costs, in the budget's own units |
| 8 | What is verifiable headlessly, what needs a headset |
| 9 | Implementation plan, ordered |

---

## 1. The measurement: what the baseline frame actually is

Everything in §2 rests on knowing the harness's optics exactly, and nobody had measured them.
I measured them from the shipped frame.

### 1.1 Measured from the PNG (VERIFIED)

`/tmp/cs-baseline/app-running.png`, 1920 × 1080, measured with a 25-line PIL script on triton
(longest contiguous run of luminance > 30 per row, which isolates the Earth disc from the star
field and the status card):

| Quantity | Measured |
|---|---|
| Disc rows present | 469 → 1079 (**clipped by the frame bottom**) |
| Widest row | y = 928, x ∈ [514, 1398] → width **885 px**, centre x = 956, half-width **442.5 px** |
| Disc pixel area | 436,895 px = **21.1 % of the frame** |
| Run at the last row (y = 1079) | 846 px wide — the disc is still 846 px across where the frame ends |

### 1.2 Derived optics (VERIFIED arithmetic)

From the report beside it: the anchor is at `(0.00, -0.25, 0.70)` and is `0.74 m` from the camera,
so the camera sits at the rig origin. The atmosphere shell is `r = 0.2793` at `d = 0.7414`.

Let β be the anchor's depression below the gaze axis and α the subject's angular radius:

```
β = atan(0.25 / 0.70) = 19.65°            (the anchor is 19.65° below where the camera looks)
```

For a sphere of silhouette half-angle α whose centre is β below the axis, the horizontal extremes
project at `x = ± f·tanα / cosβ` and at `y = -f·tan(α-β)` (top edge), with `f` the focal length in
pixels. Two measurements, two unknowns:

```
f·tanα / cos(19.65°) = 442.5     →  f·tanα = 416.7
540 - f·tan(α - 19.65°) = 469    →  f·tan(α - 19.65°) = 71
⇒ 0.06083 t² - 0.82961 t + 0.357 = 0 with t = tanα
⇒ t = 0.445  →  α = 24.0°,  R_eff = 0.7414·sin(24.0°) = 0.302 m
⇒ f = 416.7 / 0.445 = 936.4 px
⇒ fovY = 2·atan(540 / 936.4) = 59.95°
```

**The capture camera is at Unity's default 60° vertical field of view**, and `R_eff = 0.302 m`
against a shell radius of 0.2793 m says the visible teal halo extends 8 % beyond the shell — which
is the atmosphere defect §2 of `baseline-observations.md`, now with a number.

### 1.3 The consequence nobody has written down (VERIFIED arithmetic, high consequence)

The harness's pixels-per-radian is

```
ppr_capture = 0.5 · 1080 / tan(30°) = 936 px/rad
ppr_device  = 1140 px/rad   (PixelsPerRadianCentre, §2.0, INFERRED there)
ratio       = 1140 / 936 = 1.218
```

Every apparent-size rule in this app is denominated in **pixels of the render target** — the 2.5 px
body floor (`CelestialBodyView.cs:300`), the star size floor, the ribbon width `1.4 dmm ≈ 1.6 px`,
the 3 px "nothing here at this scale" test, the 170 px site-label threshold. `PlayModeShot` already
knows this and says so at `:109-120`. But it sizes the target and leaves the **field of view alone**,
so the harness evaluates all of those rules at 936 px/rad and the device evaluates them at 1140.

**Every pixel-denominated rule is therefore validated at an angular size 21.8 % larger than the one
the headset will produce.** A star the code floors at 2.5 px subtends 2.67 mrad in the capture and
2.19 mrad on the headset. A body at the 2.5 px visibility cut in the capture is at 3.0 px on device.
This is the project's own `verification-gap` pattern: the check passes for a reason unrelated to its
name.

**Fix (one line in `PlayModeShot.PrepareCaptureViewport`)**: set the capture camera's field of view
from the *measured* pixels-per-radian rather than leaving it at Unity's default:

```
captureHeightPx = the device per-eye height, 2208            (§2.0, INFERRED)
fovY_capture    = 2 · atan(0.5 · captureHeightPx / ppr)      with ppr from FrameContext.PixelsPerRadian
                = 2 · atan(1104 / 1140) = 88.2°
captureWidthPx  = 2064                                        → half-angle atan(1032/1140) = 42.2°
```

This is deliberately derived, not hard-coded, because §2.0's three numbers are mutually
inconsistent: `0.5 · 2208 / tan(48°) = 994`, not 1140 — §2.0 admits it ("994 at 96 deg vertical, or
1143 at 88 deg"). Deriving the fov from whatever `PixelsPerRadian` actually reports makes the
capture correct for whichever value Symmetric Projection settles on, and the `stereo` assertion
already reports that value. **The frame becomes the eye.** (INFERRED that a 2064 × 2208 target
renders in acceptable time on llvmpipe — see §7.4 for the experiment.)

---

## 2. Composition

### 2.1 One render, two outputs

Do not build a separate "key art" path. Render once at the eye (§1.3) and write two files:

| File | Content |
|---|---|
| `<view>.png` | the full 2064 × 2208 eye frame — **the evidence**, what the assertions and G1d read |
| `<view>.key.png` | a crop of that frame, per-view rectangle, **the key art** |

The crop costs one `Texture2D.GetPixels`/`SetPixels` and no second render. The crop rectangle is
part of each shot's specification and is expressed in degrees from the gaze axis, converted to
pixels with the same `ppr` (`px = ppr · tan(deg)`), so a change to the device's optics moves both
outputs together.

Crop aspect is chosen per view, not globally: a 16:9 frame 80.2° wide is only 50.7° tall, and the
Earth hologram is a 46.2° object (§2.4 V1) — a round subject that large simply does not fit a 16:9
box with margin. Allowed crops: **16:9** (1920 × 1080, 80.2° × 50.7°) and **4:3** (1600 × 1200,
70.2° × 55.4°). Both at ppr = 1140.

### 2.2 Aiming: the two knobs, and why they are these two

The harness moves the **floating origin**, not the camera (`PlayModeShot.cs:222-224`, deliberately —
moving the camera alone would leave the universe where it was). Aiming must not break that, and it
must not break the `stereo` assertion, which requires that placement read only the centre-eye pose.

Two knobs, both safe:

- **`-shotPitch <deg>`** — rotates the capture camera about the eye point E. The eye position does
  not move, so `_CS_CentreEyeWS` is unchanged and every group placement, every compression and every
  per-vertex direction is identical. Only the view matrix changes. Safe by construction.
- **`-shotYaw <deg>`** — sets the app's own `ViewState.YawRad` (the anchor yaw that `Travel` writes).
  This is the app's own mechanism, not a harness cheat, and it is what chooses the lighting angle.

The report prints both, so a capture states its own framing. **Not permitted:** moving the camera
laterally (it moves E and re-places the universe), changing the near/far planes, or overriding any
layer's `Weight` for beauty.

### 2.3 The house rules (five)

**H1 — Aim.** The camera looks at the shot's subject point, not straight ahead. This is the whole of
defect 5 in `baseline-observations.md`, and it is one number per view.

**H2 — The house angle: 19.65°.** The anchor sits 0.70 m ahead and 0.25 m below eye height
(§5.9.9 Recentre, and the baseline report's `anchor at: (0.00, -0.25, 0.70)`). Therefore

```
atan(0.25 / 0.70) = 19.65°
```

and **every rung whose presentation plane is horizontal is already seen at a 19.65° grazing angle.**
That is a gift, not an accident: at 19.65° a circular orbit projects to an ellipse of axis ratio
`sin(19.65°) = 0.336`, three-to-one, which reads unambiguously as perspective. A top-down view
flattens the ladder into concentric rings; an edge-on view collapses it into a line. Do not fight
the house angle — compose to it, and pitch the camera so the *subject* lands where you want it, not
so the plane changes.

**H3 — Fill.** The subject's angular diameter is 45–85 % of the crop's height. Below 45 % it is a
dot on black (the baseline's whole problem, restated as a number); above 85 % it is a texture swatch.
Views where the subject inherently exceeds the frame (the CMB ball, true scale) declare
`fill: unbounded` and are exempt, with the reason written in the shot card.

**H4 — Foreground.** Every view except `default` carries one element at **≤ 0.55 m from the eye**,
touching a frame edge, whose silhouette clears the subject's silhouette by **≥ 2°**.

Why 0.55 m: stereo disparity in pixels is `ppr · IPD / d = 1140 · 0.063 / d = 71.8 / d`. The subject
at the usual 0.70 m carries 103 px of disparity — which is exactly the number the `stereo` assertion
asserts for Earth in `default` (§11.3), so this arithmetic is the contract's own. A foreground at
0.55 m carries 131 px, a **28 px difference**; at 0.44 m it is 163 px, a 60 px difference. Below
about 20 px of difference the two read as the same plane. 0.55 m is the loosest distance that still
clears that by 40 %.

The 2° clearance is the fix for defect 2 (labels drawn across the status card). In pixels at
ppr 1140 that is 40 px of clear air, which survives MSAA, the label's own outline and a JPEG.

**H5 — Light.** Where a lit body is the subject, the Sun's direction makes **78° ± 8°** with the view
direction. Illuminated fraction `k = (1 + cos α)/2`, so `k = 0.60 ± 0.07`: a fat gibbous with the
terminator crossing the visible disc. That is where the atmosphere limb, the cloud shadows, the
specular sea and (when they land) the night-side city lights all do their work at once. The baseline
frame is at `α ≈ 20°` — nearly full — which is why Earth reads as a flat texture swatch.

The knob is `-shotYaw` (the camera's azimuth about the subject), **not** the clock. The clock is
pinned once for the whole release set:

```
capture instant  T0 = 2026-03-20T14:46:00Z         (the March 2026 equinox, INFERRED to the minute)
sub-solar latitude ≈ 0°     → the terminator runs pole to pole, unmistakably a terminator
sub-solar longitude ≈ -15°/h × (14.767 - 12) = 41.5° W   (equation of time ≈ -8 min in March, ≈ 2°, ignored)
                            → the Americas are the lit face
```

Pinning one instant makes the seven shots a coherent set and makes them reproducible. The solar lane
confirms the equinox minute from the ephemeris; any minute in that hour is fine.

**One unresolved coupling, with its experiment.** At V1 the Moon should be in frame off Earth's limb
(§2.4), and its elongation at T0 is whatever it is. Run
`tools/shot.sh --views "rung:planet-hand" --time <sweep>` over 14 days at 6 h steps, print the Moon's
elongation from the Earth–camera line and Earth's phase angle from the shot camera, and pin the
instant where elongation ∈ [40°, 80°] and a yaw exists giving `α = 78° ± 8°`. One editor run,
56 samples, no build.

### 2.4 The seven shot cards

The seven G1c scales (§2.6 walkthrough, §11.3 "Fixed capture views"), each as a deliberate shot.
`pitch` is `-shotPitch`; positive is down. Subject elevation in the frame is `19.65° - pitch` below
the frame centre for content at the anchor.

---

#### V1 · `rung:planet-hand` @ U = 4.31e-5 — **"Earth, held"**

| | |
|---|---|
| Subject | Earth, 0.55 m across at f = 0.70 m → `2·asin(0.275/0.70) = 46.2°` |
| Fill | 46.2 / 55.4 = **83 %** of a 4:3 crop — **crop 4:3**, 1600 × 1200 |
| Pitch | **19.65°** (subject dead centre; at 83 % fill there is nowhere else to put it) |
| Yaw | set so `α = 78°`: the terminator crosses the disc a little right of centre |
| In frame | Earth with its terminator and limb; the ISS on its ribbon (drawn radius 0.293 m → the ribbon stands **1.7°** outside the limb and frames the globe); **the Moon**, a rigid leaf at f = 15.05 m, s = 0.908, rendered radius 0.068 m → angular radius **0.26°**, a 9-px disc |
| Foreground | **none** — this is the hero. `--hide` the wrist panel and the status card |
| Depth proof | Earth at 0.70 m = **103 px** of disparity; the Moon at 15.05 m = **4.8 px**; a sky star = **0**. Three depth planes across 98 px in one frame. This is the single best "it is a hologram, not a poster" image the app can make, and its numbers are already the `stereo` assertion's |
| Proves | one camera, the passthrough clear, `s = 1` inside d0, the Earth subtree dissolving with the Moon as its own rigid leaf, the atmosphere limb, the terminator |
| Margin | 4.6° top and bottom (92 px). H4 waived, reason written in the card |

---

#### V2 · `rung:system` @ U = 3.34e-10 — **"The room is the Solar System"**

| | |
|---|---|
| Subject | not a body — the **orbit structure**. Neptune's orbit at r = 1.5 m; the viewer stands inside it |
| Fill | `unbounded` (you are inside the subject). Rule instead: **≥ 3 complete ellipses in frame, and the outermost visible orbit crosses two frame edges** |
| Pitch | **19.65°** — the house angle. Plane is Ecliptic, so ecliptic north is room-up and the orbits lie horizontal; the house angle is a 19.65° grazing view, axis ratio 0.336 |
| Yaw | such that the Sun sits **20° left of the frame centre**; the frame centre is on the ecliptic between Mars and Jupiter. Aiming straight at the Sun would put every planet at phase angle ~180° — a set of thin crescents on a glare |
| Which orbits fit | frame half-width at the anchor's depth = `0.70·tan(40.1°) = 0.591 m` (16:9 crop) → orbits with `r ≤ 0.59 m` are complete: `r/1.5 = 0.394` of Neptune → **≤ 11.8 AU**. Mercury, Venus, Earth, Mars, Jupiter (0.26 m) and Saturn (0.48 m) close inside the frame; Uranus (0.96 m) and Neptune (1.5 m) cross the edges. **Six complete ellipses, two running off** |
| Foreground | **the near arc of Jupiter's orbit**, at `0.70 - 0.26 = 0.44 m` from the eye → 163 px of disparity against the Sun's 103 px, a 60 px separation. The same ribbon's far arc at 0.96 m gives 75 px. One ribbon supplies both the foreground and the background |
| Do not use | the near arc of **Saturn's** orbit: at `0.70 - 0.48 = 0.22 m` it is inside the 0.25 m comfort fade (§5.7.5 rule 7) and will be faded out. That is correct behaviour, not a bug — but a shot must not be composed around content the comfort rule deletes |
| Proves | identity placement for the whole Sun subtree (`D + R ≤ 10 m`), Voyager 1 still at s = 1 at 8.3 m, ribbon width and AA at three depths, the ecliptic plane levelling |

---

#### V3 · `rung:nearest` @ U = 3.24e-15 (10 pc per metre) — **"Stars have depth"**

| | |
|---|---|
| Subject | the local star cloud. The 10 pc sphere is 2 m across around the Sun at the anchor; the frame's half-width at 0.70 m is 0.591 m, so the frame is filled by the nearest ~6 pc |
| Fill | `unbounded`. Rule instead: **at least one star at ≤ 0.45 m from the eye and one at ≥ 1.2 m, both drawn, both labelled** |
| Pitch | **12°** — the plane here is Ecliptic and the content is a sphere, not a disc, so there is no plane to graze. Putting the anchor 7.65° above the frame centre leaves the lower third for the foreground |
| Yaw | such that α Centauri (0.134 m from the Sun → 0.57–0.83 m from the eye) is on the **near** side: 0.57 m, 126 px of disparity |
| Foreground | α Centauri at 0.57 m; a second named star in the lower-left corner |
| Depth proof | 126 px (α Cen) vs 60 px (a star at 1.2 m) vs 0 px (the 146 distance-unknown stars on the sky layer). **Three disparity classes in one frame, and the third is exactly zero by construction** (§2.5 point 3) |
| In frame | constellation ribbons, which from 4 ly off the Sun are visibly sheared — the honest demonstration that constellations are a viewpoint, not a thing |
| Proves | per-point compression, the sky layer at infinity per eye, the star point-spread at three brightnesses, the constellation shear |

---

#### V4 · `rung:milkyway` @ U = 1.08e-18 (30 kpc per metre) — **"The Galaxy on the table"**

| | |
|---|---|
| Subject | the Milky Way disc. Disc radius ≈ 15 kpc → **0.5 m**, so a 1.0 m disc at 0.70 m = `2·atan(0.5/0.70) = 71.1°` wide |
| Fill | 71.1 / 80.2 = **89 %** of the 16:9 crop width; vertically the disc is a 19.65°-grazed ellipse, `71.1 · 0.336 = 23.9°` tall = 47 % of the crop height. Within H3 on the binding axis |
| Pitch | **19.65°** — the house angle again. Plane is Galactic, so the disc is horizontal. A galaxy seen 20° above its own plane with the dust lane in silhouette against the bulge is the classic astronomical image, and here it is free |
| Yaw | such that the Sun's dust cube is on the **near** side of the Galactic Centre |
| In frame | the bulge; the dust lane; the Cepheid layer (the honest tracer of the warped disc, and the only measured answer to the far side, per the Milky Way Atlas's own honest-limits section); globulars above and below the plane |
| Foreground | **the dust cube**: 2 × 2 × 0.8 kpc at 30 kpc/m is a **6.7 cm box**, sitting 0.27 m from the Galactic Centre. Place it on the near side at ~0.45 m from the eye → **160 px** of disparity against the GC's 103 px. A 6.7 cm box you can put your face into, 0.27 m from a galaxy's core, is the scale ladder in one glance |
| Honesty | the `DUST xN` chip if `dust.density ≠ 1`; INFO → THIS VIEW must carry "no dust map covers the far side of the Galaxy" |
| Proves | the enclosing-volume arbitration (exactly one `MayEnclose` volume with `_Fade > 0`), the galactic plane levelling (which sits on the `StandardFrames` sign bug — see §6.1), the point-population budget |

---

#### V5 · `rung:localgroup` @ U = 1.08e-20 (3 Mpc per metre) — **"Labelled emptiness"**

| | |
|---|---|
| Subject | the Local Group. M31 is 0.77 Mpc from the Milky Way → **0.26 m**. M31's half-light diameter (≈ 22 kpc) at 3 Mpc/m is **7.3 µm** — invisible, which is the truth and the problem |
| Fill | `unbounded`; the honest content is two glow sprites and a sparse cloud |
| Pitch | **14°** (the plane is Supergalactic; the content is a flattened cloud, and 5.65° of lift leaves the bottom third for the rings) |
| Yaw | M31 on the **near** side: 0.44 m from the eye → 163 px against the Milky Way's 103 px |
| Foreground | M31's sprite at 0.44 m |
| **The composition problem, and its answer** | this is the view where "empty dark space" is *correct*. The answer is not to add content. It is **distance rings**: a cylindrical stack at 1, 2 and 3 Mpc centred on the Milky Way, with tags, from `lines`. Unlabelled emptiness reads as a bug; labelled emptiness reads as scale. The Milky Way Atlas already proved this — its "Cyl grid" is the single feature that makes its extragalactic views legible |
| Honesty | `GALAXY GLOW xN` will be on here almost always, because without it the layer is invisible. That is exactly the chip's purpose; the shot must show it lit, not hide it |
| Proves | the galaxy size floor and its chip, the impostor ellipticity and position angle, the supergalactic levelling, ring labels with declutter |

---

#### V6 · `rung:cmb` @ U = 1.364e-24 — **"Standing at the edge"**

| | |
|---|---|
| Subject | the CMB **object form**: a ball of radius 0.60 m centred at the anchor, 0.70 m from the eye. The eye is outside by 0.10 m (the sky-form switch is at 1.05 R = 0.63 m, so the object form is correct here) |
| Angular size | `asin(0.60 / 0.70) = 59.0°` → **118° across**, wider than the 84.4° eye frame. Fill: `unbounded` |
| Pitch | **19.65°** (anchor centred vertically) |
| Yaw | **aim 35° off the ball's centre.** The limb is 59° from the centre direction; with the crop's half-width 40.1°, aiming 35° off puts the near limb at `59 - 35 = 24°` from the frame centre, i.e. **60 % of the way to the frame edge**. The other limb falls outside. You see that it is a *ball*, not a sky — which is the entire difference between rung 18 and the sky form |
| Beyond the limb | the Local Volume points and the 2MRS shell, which is what makes the limb read as a surface with a universe behind it |
| Foreground | the ball's own near surface (see §6.2 — this is a comfort conflict, not a feature) |
| Proves | the object/sky hysteresis (`cmb-fade`: at most one form with `_Fade > 0`), the cube-map face order (`cmb-faces`), the LUT, the `_Immersion` coverage write |

---

#### V7 · `rung:leo` @ U = 1e3 (true scale) — **"Nine orders of magnitude in one frame"**

| | |
|---|---|
| Subject | Earth from 420 km, in **Envelope** mode, filling the lower field |
| Pitch | **6°** — the limb runs across the lower third of the frame; the upper two-thirds is sky |
| Yaw | Sun near the left frame edge, `α = 78°`, so the limb carries a terminator and an airglow band |
| In frame | Earth's surface and limb at metres; **the Moon**, rigid at f = 184.7 m, rendered radius 0.84 m → `asin(0.84/184.7) = 0.26°`, a half-degree disc — **the real angular size of the Moon**; **the Sun**, f = 244 m, r = 1.14 m → `asin(1.14/244) = 0.268°`, also half a degree, also real |
| Why this is the key art | three objects whose true distances span 4.2e2 km, 3.8e5 km and 1.5e8 km — **five and a half orders of magnitude** — sit in one frame at render distances of metres, 185 m and 244 m, and every one of them subtends its true angle. That is the distance map's entire thesis, provable in a single image, and it is the image to put in front of the owner |
| Foreground | the Earth's surface below, in Envelope mode, at a few metres |
| Proves | Envelope mode and its 1.1 edge-ratio tessellation rule, the graded mesh cap of 16 k vertices, `_PerVertexCompress`, the atmosphere integrated in true eye-relative space |

---

**The two non-rung views in `release`.** `wide` is the only view that repositions the camera today
(`PlayModeShot.cs:793-798`), and it should be re-specified as V2 with `-shotPitch 19.65` rather than
keeping its hand-tuned `(0.55, 0.42, -1.15)` offset, which is a camera move and therefore moves E.
`mode:vr` is the same subject as V1 with the room off and the clear opaque; it is the A/B for §3 and
its shot card is "V1, `room:0`".

### 2.5 The `compose` gate

Turn "aimed" into a number. `tools/gates/compose_check.py <png> <report.txt>`, stdlib + PIL, runs on
the box in under a second per view, and asserts:

1. **In frame.** The subject's bright-pixel bounding box lies inside the frame with ≥ 2° (40 px)
   margin on every side, unless the shot card declares `fill: unbounded`.
2. **Fill.** For bounded views, the subject's height is 45–85 % of the crop height.
3. **Placement.** The subject's centroid is within the declared box of the shot card, ± 2°.
4. **No collision.** No text bounding box intersects any panel or card bounding box, and no text
   bounding box touches a frame edge. (`PlayModeShot` already knows every text renderer; it writes
   their screen rects into the report, which is the input this check reads.)
5. **Foreground present.** For views declaring one, at least one enabled renderer's nearest bounds
   corner is ≤ 0.55 m from the eye and its screen rect touches a frame edge.

**The gate lands with a run that fails, and the failing input already exists.** Run it against
`/tmp/cs-baseline/app-running.png`: rule 1 fails (the disc's box extends 293 px below the frame —
`928 + 445 = 1373` against a height of 1080, so **32.5 % of the subject's height is outside the
frame**) and rule 4 fails (the "James Webb Space Telescope" label crosses the status card). That is
the project's "never trust a gate that has not failed once" satisfied on day one, with a fixture
nobody has to plant.

---

## 3. MR staging: making a space scene read inside a real room

### 3.1 The governing fact: in passthrough, black is free and grey is impossible

The camera clears to `(0, 0, 0, 0)` and the compositor puts the room there. So:

- **Black costs nothing and is not visible.** "Empty dark space" in a flat capture is your room on
  the headset. The owner's judgement is still right, because the app also has a Space mode and
  because emptiness with nothing *in* it is empty either way — but the fix in MR is not "make the
  black less black".
- **Dark grey is unreachable.** Anything additive is *added to the room*. An additive layer whose
  peak is below the room's own luminance is invisible. A magnitude-5 star rendered flux-true, with
  the eye-like `L^0.28` compression the Milky Way Atlas uses, lands at a few per cent of display
  white — over a lit wall it does not exist.

This is the single most consequential MR fact for this product and nothing in the build or the
contract states it. Its number is unmeasured: call the room's contribution `L_room`, the fraction of
display white the passthrough feed occupies in a normally lit room. **INFERRED: 0.05 to 0.30.**

**The experiment that settles it (H5, 3 minutes on the headset).** Stand facing a white wall, then a
dark corner. Open `rung:nearest`, set `stars.exposure` to 0.1, and step it 0.1 → 3.0. Record the
lowest value at which magnitude-4 stars are visible in each case. The ratio between the two answers
is `L_room`'s dynamic range, and the wall answer is `G_pt`, the passthrough gain in §3.2.

### 3.2 The rule: dim the room, do not inflate the data

Two ways to make faint content visible over a bright room. They are not equivalent:

- **Raise the content** — a gain on the star exposure. It changes what the eye reads as brightness,
  so §5.9.6 requires the `BRIGHTNESS xN` chip. It is a departure from the data.
- **Lower the room** — `RoomVisibility`. It changes nothing about the sky. It is not a claim.

**Lowering the room is always preferred, and the app never raises the content by itself.** Concretely:

```
roomGain is user-invoked, never automatic.
If the user forces RoomVisibility back to 1.0 at a rung whose default is below 1.0, a chip offers
    "Faint here — [DIM THE ROOM] or [BRIGHTEN xN]"
and choosing BRIGHTEN sets stars.exposure, which raises BRIGHTNESS xN through the existing
IsPresentationGain mechanism. There is no hidden multiplier anywhere in the path.
```

This falls straight out of machinery the contract already has, and it means an app named for
honesty never silently boosts.

### 3.3 The room dial: one number, one schedule, one draw

`LayerState.RoomVisibility` (1 = full passthrough, 0 = Space) already exists (§4.2). Three additions:

**(a) `_Immersion = 1 - RoomVisibility`.** The Sky pass's `Blend One Zero` writes premultiplied
colour with coverage `k = _Immersion · _Fade` (A.6). At `k = 0.65` the compositor shows the room at
35 % behind a deep-space veil. The intermediate values of the ROOM slider are not a special case;
they are this one uniform.

**(b) The Space backdrop and the CMB sky form are the same renderer.** Two draws at queue 1000 with
`Blend One Zero` would fight — the second overwrites the first, including its coverage. So one
`CosmoSynth/Sky` renderer serves both, and `_Contrast` selects:

```
CMB layer on   →  _Contrast = cmb.contrast          (the anisotropy map through the Planck LUT)
CMB layer off  →  _Contrast = 0                     (t = saturate(0.5 + (raw-0.5)*0) = 0.5 exactly,
                                                     every direction → the LUT's midpoint, flat)
                  _Lut      = a 1x1 linear texture of (0.004, 0.005, 0.011)
                                                     — the §2.1 full-space clear colour, 4 bytes
```

No new shader, no new queue, no second budget row, and the "two `One Zero` draws" bug cannot happen
because there is only ever one renderer. **This is why `_Immersion` lives on the Sky shader and not
on the camera** — worth writing into the layer's comment, because it looks like a CMB detail and it
is the room dial.

**(c) The clear stays at alpha 0 for every `RoomVisibility > 0`.** The `camera` assertion requires
`PassthroughClearColour.a == 0` while passthrough is requested; define "requested" as
`RoomVisibility > 0`. Only the hard Space preset (`RoomVisibility == 0`, and `mode:vr`) flips the
clear to `(0.004, 0.005, 0.011, 1)`. At that point the backdrop draw and the clear agree, and the
draw is redundant but harmless (`Blend One Zero`, idempotent).

### 3.4 The fade, with the arithmetic

§5.7.5 rule 6: "stage switches and passthrough changes fade over at least 0.3 s; luminance never
changes by more than 30 % of the view within 100 ms."

Smootherstep `s(x) = x³(6x² - 15x + 10)` has `s'(x) = 30x²(x-1)²`, maximum `1.875` at `x = 0.5`.
Over a duration T the peak rate is `1.875 / T` per second.

```
T = 0.30 s → 6.25 /s = 62.5 percentage points per 100 ms   ✗ (twice the limit)
T = 0.45 s → 4.17 /s = 41.7 pp per 100 ms                  ✗
T = 0.625 s → 3.00 /s = 30.0 pp per 100 ms                 ← exactly the limit
T = 0.70 s → 2.68 /s = 26.8 pp per 100 ms                  ✓ 11 % of margin
```

**The room fade is smootherstep over 0.70 s.** The contract's 0.3 s is a floor on the duration, not a
specification, and a 0.3 s smootherstep breaks the comfort rule in the same section.

**The 0.4 s stage cross-fade (§5.3) is fine and does not need changing.** Its peak rate is
`1.875/0.4 = 4.69 /s`, but it changes only the *content*, which covers at most 60 % of the frame
(§3.5), so the luminance change is `0.60 × 4.69 = 2.81 /s = 28.1 pp per 100 ms` ✓. Show this
arithmetic in the code comment or someone will "fix" one of the two numbers to match the other.

Sibling evidence that 0.70 s is not slow: Reverie's shipped `ImmersionController` uses
`LerpSpeed = 0.6` — a **1.67 s** full sweep (VERIFIED,
`~/projects/Reverie/Assets/Reverie/Runtime/ImmersionController.cs:22`).

**The binding direction is Space → passthrough**, not the reverse: going to Space *reduces*
luminance in a lit room, and the 30 % rule is about luminance. Use the same 0.70 s both ways; there
is no reason to make the comfortable direction faster.

### 3.5 What comes in behind the veil, and when

Reverie builds its world in layers as the dial rises (sphere, then bands at t > 0.12, then ground at
t > 0.25) rather than switching everything at once, and it works. The CosmoSynth schedule, with
`t = 1 - RoomVisibility`:

| t | What appears | Why that threshold |
|---|---|---|
| 0.00 → 1.00 | the backdrop veil (§3.3) | the dial itself |
| **0.35** | `mwBand` | the contract already sets this (`RoomVisibility ≤ 0.35`, §5.4) |
| **0.55** | the neutral translucent hand mesh, cross-fading in over t ∈ [0.55, 0.85] | your real hands are behind the veil by then; this is a safety cue, not decoration |
| **0.70** | the comfort floor ring (§5.7.5 rule 2) | it exists because you can no longer see the floor. Any earlier and it is clutter over a visible floor |
| **1.00** | the clear flips opaque | §3.3(c) |

**The room ramp is a rung property, not a slider default.** One new column in `LayerTable.Rungs`:

| Rungs | `RoomDefault` | Reason |
|---|---|---|
| 0–9 (`sky` … `nearest`) | **1.00** | the content is bright: planets, the Sun, spacecraft. The room is an asset |
| 10–11 (`neighbourhood`, `clouds`) | **0.70** | star points begin to lose the room |
| 12–15 (`milkyway` … `localvolume`) | **0.35** | the `mwBand` threshold the contract already chose |
| 16–18 (`laniakea` … `cmb`) | **0.00** | the CMB and the cosmic web are the sky; there is nothing left to see the room for |

**And the room changes only on a rung tap, a GO, or the ROOM control — never on continuous zoom.**
This is the same rule §5.2 already applies to the presentation plane, and for the same reason: a
room that flickers dark while you pinch-zoom is a worse experience than either end state.

### 3.6 Edges: where the hologram meets the room

The silhouette is the only place the virtual and the real touch, and there are four things that make
it read and one that would wreck it.

**(a) MSAA 4× is the edge treatment.** With coverage-writing opaque geometry and MSAA 4×, a partially
covered edge sample resolves to a fractional destination alpha, and the compositor blends the room
through at exactly that fraction. That is a correct, physically-shaped, sub-pixel soft silhouette,
for free, and it is one of the real reasons MSAA 4× is mandated. Three things would destroy it, all
already forbidden and now with the reason attached:

- **No alpha-test or `clip()` on a body silhouette.** Alpha-to-coverage aside, a clipped edge
  resolves to whole samples and the silhouette hardens into stair-steps against the room.
- **No intermediate render texture.** The `pipeline` assertion already requires
  `CameraData.requiresIntermediateTexture == false` (R11); the staging reason is that resolving to
  an intermediate and blitting re-quantises the alpha and hardens every edge in the frame.
- **No post-processing.** Already `renderPostProcessing = false`.

**(b) Do not fake a floor.** A contact shadow on the real floor is the strongest MR cue there is —
Reverie ships it (`Reverie/Shadow.shader`, a dark quad whose *alpha is the shadow*, `_Strength 0.42`,
`a = smoothstep(1.0, 0.15, d)²·_Strength`, VERIFIED) — but it requires knowing where the floor is.
**CosmoSynth has no scene understanding**: no MRUK, no `ARPlaneManager`, no `USE_SCENE` permission.
A shadow dropped at a guessed height lands in mid-air or inside the table, and a shadow that misses
is worse than no shadow.

So: **the Real stage has no contact shadow, on principle**, which also keeps §5.3's "no frame;
content floats in the room" intact. Contact shadows are used in exactly two places, both of which
own their own surface:

- under a **phenomenon exhibit's** model, on its plinth;
- under a **specimen** on the tray's shelf.

Both are virtual surfaces lit by the app's own light, so the shadow is true. Implementation costs no
new shader: `CosmoSynth/Panel` (`_BaseMap` alpha × vertex colour, premultiplied over, queue 3200),
vertex colour black, `_BaseMap` a 64 × 64 R8 radial falloff generated at load:

```csharp
// one 4 KB texture, shared by every contact shadow in the app
var t = new Texture2D(64, 64, TextureFormat.R8, mipChain: true, linear: true);
for (int y = 0; y < 64; y++)
for (int x = 0; x < 64; x++)
{
    float d = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 31.5f;
    float a = Mathf.SmoothStep(1f, 0.15f, d);          // Reverie's curve, VERIFIED shipped
    t.SetPixel(x, y, new Color(a * a * 0.42f, 0, 0));  // and its 0.42 strength
}
```

Because the plinth is already opaque (alpha 1), the premultiplied "over" only darkens its colour and
writes no new coverage. **The shadow never touches passthrough.** If MRUK is ever added, the real
floor shadow becomes legitimate and this texture is already there; Reverie's `RoomSense` is the
pattern to copy.

**(c) The rim is reserved.** `CosmoSynth/Rim` is the *selection* affordance. Do not put a decorative
rim on every body to help the silhouette — it would make "selected" meaningless. An airless body's
silhouette is carried by MSAA and by the body's own lit/unlit contrast; a body with air gets its
limb from the atmosphere shader, which is physical.

**(d) Your hand vanishes into the hologram. Accept it in 2.0 and say so.** With no depth occlusion,
reaching into a planet puts your real hand behind it. Environment-depth occlusion
(`XR_..._OCCLUSION` keywords, AR Foundation's `ARShaderOcclusion`) needs multi-variant shaders,
which §7.1 rule 1 forbids outright. The cheap alternative is a **depth-only hand mask** — write
depth where the hand capsules are, write no colour and no alpha, so the body behind is z-rejected and
the passthrough shows through. That needs a twelfth shader and a second pass, so it is a **contract
amendment**, not a lane decision. The amendment, written out so the decision can be taken on
evidence rather than on imagination:

```hlsl
// PROPOSED — CosmoSynth/Mask, a twelfth shader. Requires a §7.2 amendment.
// 24 hand-joint capsules, one batched mesh, ~600 vertices, 1 draw, no colour, no alpha.
Shader "CosmoSynth/Mask"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-1" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Mask"
            Tags { "LightMode"="UniversalForward" }
            ColorMask 0          // writes neither colour nor passthrough coverage
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // Hand capsules are in the room, always inside d0, so no compression applies:
                // CS_CompressWS is the identity for d <= 10 m and is called anyway, so that a
                // hand held up in front of a true-scale planet is masked by the same rule as
                // everything else.
                float3 posWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(CS_CompressWS(posWS));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return 0;        // ColorMask 0 discards this; depth is the whole output
            }
            ENDHLSL
        }
    }
    Fallback Off
}
```

Cost: 1 draw, ~600 vertices, zero fragments written, **≈ 0.05 ms** (INFERRED, same order as the body
markers row). The reason to hold it out of 2.0 is not cost, it is that `Queue Geometry-1` ahead of
the opaque pass is a new ordering rule and this design has already been burned once by an unexpected
extra pass (the 1.7.0 grid). Ship 2.0 without it; measure; amend.

### 3.7 How much of the frame may be opaque

Two guards, one number each.

**Guard 1 — the walking-safety guard.** In passthrough the user must retain enough of the room to
move. Assert on the *resting framing* of rungs whose `RoomDefault` is 1.00 (rungs 0–9):

```
mean destination alpha over the eye frame ≤ 0.60
```

Sanity check with V1, the biggest subject in that band: Earth's solid angle is
`2π(1 - cos 23.1°) = 0.503 sr`; the eye frame is `4·asin(sin(42.2°)·sin(44.1°)) = 1.95 sr`; so
Earth covers **25.8 %**. There is a factor of two of headroom, which is what a guard should have.

**Guard 2 — the honest-label guard.** Above 0.60 the app must stop calling itself passthrough: the
ROOM readout reads `ROOM 22 %` rather than `Passthrough`, computed from measured coverage rather than
from the slider. A user who cannot see their room should be told, whichever combination of content
did it.

**Rungs 10–18 are exempt** because their `RoomDefault` already dims the room, and `rung:cmb` is
exempt for the additional reason that a 118°-wide ball is the point of that rung.

Mechanism: extend the existing `alpha` assertion's `ProbeDestinationAlpha`, which already reads the
destination alpha buffer, to report **mean** and **95th-percentile** coverage per view. That is two
more numbers from a readback the harness already does — no new machinery.

### 3.8 The alpha rules, restated with their consequences

The contract's four blend forms (§7.1 rule 4) are correct. What is missing is the *order* and three
traps:

| Rule | Consequence if broken |
|---|---|
| Additive is `Blend One One, Zero One`, returns alpha 0 | `Blend SrcAlpha One` adds `a` into destination alpha and **darkens the room behind every bright star** (quest-rendering-techniques:821, VERIFIED). This is the single most-repeated MR bug on this box — Pastelia's CONTRACT calls it out at `:325`, Reverie's CLAUDE.md at `:87` |
| Premultiplied over is `Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha`; **physical absorbers and UI only** | a decorative "glow" done as premultiplied over is a hole punched in the room in the shape of the glow |
| Opaque returns alpha 1 | an opaque body that returns alpha 0 is invisible on device and perfect in the editor |
| **`Blend One Zero` REPLACES destination alpha, so the Sky must be the first draw of the frame, always** | the Space backdrop drawn after an opaque body resets that body's coverage to the backdrop's. Already covered by the `far` assertion ("the sky layer … renders first"); the *reason* belongs beside it |
| **No skybox.** `RenderSettings.skybox = null`; the camera clears `SolidColor`, never `Skybox` | a skybox material is drawn by URP with alpha 1 and turns passthrough into a wall. Pastelia hit exactly this (`CONTRACT.md:286`, "Skybox NONE (passthrough!)"). Worth one grep in `code_gate.sh` for `RenderSettings.skybox` |
| **The font atlas must be premultiplied**, or `CosmoSynth/Text` must output `(c·a, a)` | a straight-alpha atlas in a premultiplied blend gives every glyph a dark halo — invisible on black in the editor, obvious over a lit wall |

---

## 4. The fiction bridge

### 4.1 The problem, stated precisely

The product must carry a viewer from the measured sky into authored worlds, and at no instant may a
reasonable person be unsure which they are looking at. Two things make this hard:

- §5.3 forbids real and invented content in the room at the same time, so there is no spatial
  continuity to lean on;
- D9's default is **"a fictional system may not be placed at a real star, not in 2.0"**, so the
  obvious bridge — fly to a real star, find an invented system around it — is closed.

Both constraints are right, and this design does not ask to reopen them. It inverts the problem:

> **You never arrive at the fiction. You switch it on.**

The fiction is not a place at a distance. It is a machine standing in your room. That is not a
compromise — it is the strongest available statement of the product's thesis, it matches the owner's
own reference exactly (`holographic_star_map(2).html` is literally a projector), and it gives a rule
that is checkable by a machine and obvious to a person from any angle.

### 4.2 The one rule

> **Nothing fictional is ever drawn outside a projection volume. Nothing real is ever drawn inside
> one. Real content has no plate; fictional content always does.**

Properties that make this the right rule rather than a nice one:

- **Binary**, not a matter of degree. There is no "slightly fictional".
- **Visible from every angle**, including from behind, above and from inside the volume: the deck,
  the emitter and the beam surround the content.
- **Survives cropping.** A screenshot of one invented planet still has beam stripes in it.
- **Survives colour-blindness.** It is geometry, not hue. (The Fictional hue and the `?` glyph are
  still carried, per §5.5 — this is a fourth channel, not a replacement.)
- **Machine-checkable, both ways**, headlessly — see `fiction-bounds` in §8.
- **Already half-written**: §5.3's Fictional row says "octagonal plate, emitter, upward beam,
  flow-filament orbits, bracket labels, all chrome in the Fictional hue. Reserved: never used on
  real content." This document supplies the geometry and the numbers.

The negative half is already gated: the `honesty` assertion requires that no drawn object in the Real
stage carries a Fictional provenance field, and `T/StageBoundaryTests.cs` enforces it in Core.

### 4.3 The Threshold — the transition, in three acts

The user reaches it through the one door: **GO → FICTIONAL SECTORS → VG-429 Holosystem** (§5.3,
"fictional content has exactly one door"). Total 2.10 s, all of it inside `StageDirector`.

#### Act 1 — WITHDRAW (t = 0.00 → 0.70 s)

The real universe does not dissolve into the fiction. It **packs itself away**, which is a different
gesture and reads as one.

| Beat | t | What happens |
|---|---|---|
| Confirm | 0.00 | The GO row is tapped. A card appears at the anchor: **"VG-429 Holosystem"** / `? FICTIONAL — authored, not a real system` / "There is no distance to this place. It opens as a projection in your room." / `[OPEN]` `[CANCEL]`. In the Fictional hue, with the `?` glyph. **This card is the door, and it says in words what the skin will say in geometry.** |
| Withdraw | 0.00 → 0.70 | On OPEN: the real stage's `LayerState.Weight` for every layer falls to 0 on **smootherstep over 0.70 s** (§3.4's curve and duration, reused). The universe **contracts toward the anchor** as it fades: the stage's similarity scales to 0.15 over the same curve. The real scale, origin, plane, focus and follow are stored by `StageDirector` before the first frame of the fade |
| Room | 0.00 → 0.70 | `RoomVisibility` ramps to **1.00** on the same curve. The fictional stage is always shown at full passthrough, because a projector standing in your room is the whole claim |

Why contract rather than cross-fade: a cross-fade of two clouds of points is mush; a contraction is
legible as *removal*, and it leaves the anchor as the last thing occupied — which is where the deck
appears.

#### Act 2 — THE THRESHOLD (t = 0.70 → 1.10 s)

**0.40 s in which the room is empty.** Nothing is drawn except the wrist panel and the header chip,
which switches from `REAL` to `FICTIONAL SECTOR ?` at t = 0.70 exactly.

This is the most important 0.4 s in the product. Do not fill it. The gap *is* the boundary: the
viewer sees the real universe leave and the fiction arrive as two separate events, and nothing can
have crossed between them because for four hundred milliseconds there was nothing there. It is also
§5.3's own 0.4 s stage cross-fade duration, spent as a hard cut rather than a blend.

#### Act 3 — POWER ON (t = 1.10 → 2.10 s)

The projector builds bottom-up, the way a machine starts. Each element's `_Fade` follows
smootherstep over its own window; the whole sequence is 1.00 s.

| t (s) | Element | Window |
|---|---|---|
| 1.10 | **Deck** — the octagonal plate, opaque | 1.10 → 1.30 |
| 1.20 | **Emitter core** — the bright octagon and the grid | 1.20 → 1.45 |
| 1.30 | **Beam** — rises from the emitter, its top edge sweeping from the deck to full height | 1.30 → 1.70 |
| 1.45 | **Star** — VG-429 itself, at the volume centre, with its ±5 % pulse | 1.45 → 1.75 |
| 1.55 | **Nodes** — the seven bodies, innermost first, 0.05 s apart | 1.55 → 1.95 |
| 1.70 | **Orbit filaments** — drawn on, tracing in the direction of motion | 1.70 → 2.05 |
| 1.85 | **Bracket labels** and the deck-rim text | 1.85 → 2.10 |
| 2.10 | Sector clock starts at `00:00:00`, ×1 | — |

Luminance check: the largest single step is the beam, which covers ≈ 0.05 sr of a 1.95 sr frame at
peak alpha 0.17 → a 0.4 % luminance change over 0.40 s. The whole sequence is far inside
§5.7.5 rule 6; nothing here needs slowing.

**Sound: none in 2.0.0.** §5.9.8 (R52) puts the `audio` lane in Phase 3 and makes TOOLS → SOUND read
"Sound: not in this build". When it lands, the Threshold is the app's best sound cue and should be
three sounds and no more: a *descending* two-tone at t = 0.00 (the universe withdrawing), **silence
through the whole of Act 2**, and a capacitor-charge rise at t = 1.10 → 1.70 under the beam. The
silence is the sound design; do not fill it either.

#### The return

Exactly the reverse, same durations: the header chip's "Back to real universe" → the projector powers
down top-down over 1.00 s, 0.40 s of empty room, then the real universe expands back from the anchor
over 0.70 s to **the exact state `StageDirector` stored** — origin km, U, plane, focus, follow, layer
state (§5.3). Returning to a different view than you left from would be the one thing that makes the
boundary feel arbitrary.

### 4.4 The projector, in numbers

All geometry from `refs/webxr/pages/holographic_star_map(2).html:202-271` (VERIFIED by reading it),
in source units where **1 source unit = 1 m at sector scale ×1**, and the sector is 2.6 m across at
×1 and opens at **×0.3 = 0.78 m** (§5.3). `S` is the sector scale; default `S = 0.3`.

| Element | Source | At `S = 0.3` |
|---|---|---|
| Deck plate | 8-gon, `r_top 0.80`, `r_bot 0.85`, `h 0.06`, at `y = -0.55` | `r_top 0.240`, `r_bot 0.255`, `h 0.018`, `0.165 m` below the volume centre |
| Deck colour | `#071524` at 0.95 | `#071524` sRGB, **opaque** (`CosmoSynth/Unlit`) |
| Border ring | 8-gon outline, `r 0.84`, `h 0.07` | `r 0.252` — 8 ribbon segments at the plate's top edge |
| Emitter core | 8-gon, `r 0.74`, `h 0.01`, at `+0.031`, additive `#93c5fd` | `r 0.222`, at `+0.0093` above the deck |
| Grid | `1.3` across, 12 divisions, opacity 0.4 | 8 radial spokes + 3 concentric 8-gons = **32 ribbon segments** |
| Beam | open cone `r 0.72 → 0.75`, `h 1.2`, from `+0.031` | `r 0.216 → 0.225`, `h 0.36`, additive |
| Beam falloff | `pow(1 - v, 4)` | kept exactly |
| Beam stripes | `step(0.98, sin(u·64))` → 64 verticals | **24 verticals** (see below) |
| Beam alpha | `fade · (0.07 + stripe·0.10)` | peak **0.17** |

**Colour correction against the contract.** The source's chrome is cyan `#0ea5e9` and its beam runs
blue → orange. Both collide with reserved meanings: cyan is `CosmoTheme.Accent`, reserved because
"no body in the catalogue is cyan — so 'this is a control' is never confused with 'this is a planet'"
(`CosmoTheme.cs`, VERIFIED); orange is `CosmoTheme.Warning`. §5.3 requires "all chrome in the
Fictional hue". So:

```
chrome = CosmoTheme.Provenance(ProvenanceClass.Fictional) = (0.894, 0.541, 0.847)  sRGB  ≈ #E48AD8
beam   = lerp(chrome, chrome * 0.55, v)      // value ramp, single hue, no second hue introduced
```

The **node colours stay the authored hexes** (`#ff7a1a`, `#ff3b1a`, …, C.13) — the contract is
explicit that "body colours stay the authored node colours: they are content, not UI."

**The sRGB trap.** Every hex above is display-referred. Decode **once**, with `CS_DisplayToLinear` in
the shader for vertex colours, or by letting Unity linearise a `SetColor`. Never both. This has
already cost this box two projects (`BoltGraphXR`, `Pastelia`) and it presents as "everything is
slightly washed out and nobody can say why".

**The beam as ribbons, not a cone.** The eleven shaders contain no textured additive surface, and
inventing a twelfth for a beam would be absurd. The source's beam is *already* 64 thin verticals plus
a 0.07 haze, so build it from `CosmoSynth/Ribbon`, which is screen-constant-width with analytic AA
and therefore stays crisp at any sector scale:

```
N  = 24 verticals, evenly in azimuth (a multiple of the octagon's 8, so 3 per facet)
M  = 6 sub-segments per vertical (the (1-v)^4 falloff needs nodes, a ribbon segment is straight)

for j in 0..N-1:  phi = 2*pi*j/N
  for k in 0..M:  v = k/M
      r   = lerp(0.216, 0.225, v) * (S/0.3)
      y   = deckTop + 0.36 * v * (S/0.3)
      p_k = (r*cos(phi), y, r*sin(phi))
      a_k = pow(1 - v, 4) * 0.17          // 0.1700, 0.0820, 0.0336, 0.0106, 0.0021, 0.00013, 0
      COLOR.rgb = lerp(chrome, chrome*0.55, v)   // display-referred, decoded in the shader
      COLOR.a   = a_k                            // the Ribbon shader's tail gradient channel
```

144 segments, 576 vertices, one draw, `Blend One One, Zero One`, **no alpha written**. The 0.07 haze
term is dropped: it needs a surface, and without it the beam is a cage of light, which is if anything
a better read of "projection" than a cylinder of fog.

**Coverage.** In the whole fictional stage, **the deck and the emitter core are the only things that
write passthrough coverage.** Deck at `S = 0.3`, seen at the house angle, is an ellipse about
39.5° × 13.3° ≈ 0.13 sr = **6.7 % of the eye frame**. Everything else — beam, grid, border, orbits,
dust, the bodies' glows — is additive and leaves the room alone. The bodies themselves are opaque
spheres of 12–42 mm radius and cover ≈ 0.4 % between them.

**No shadows in the fictional stage.** The emitter is *below* the bodies and shines *up*; a shadow
cast down onto the deck would be a lie about the light, and a shadow cast up has nothing to land on.
The "it sits on something" cue is the deck and the beam, which is plenty. (Contrast §3.6(b): the
exhibit plinth is lit from above and does get one.)

### 4.5 Provenance, at every moment

Six channels, all simultaneously active, none of which is the only one:

1. **Header chip** `FICTIONAL SECTOR ?` in the Fictional hue, with "Back to real universe"
   (§5.3, §5.6.4). Honesty chips are priority 1 and cannot be pushed out by the four-chip cap.
2. **The skin** — deck, emitter, beam. Visible from every angle, unccroppable (§4.2).
3. **Bracket labels** with pointer lines, Fictional hue, `?` glyph (§5.3).
4. **On-universe badge** under the focus label (§5.6.1).
5. **The clock.** In the fictional stage the header shows the **sector clock** — `SECTOR 00:04:12 ·
   ×1` — and **never a UTC date**. A UTC date beside invented planets is the most dangerous single
   pixel in the product, because it is the one that says "this is happening now".
6. **The deck rim text** (new). The octagon's eight rim faces each carry
   `AUTHORED - NOT REAL` at `TypeCaption` 19 dmm, so **whichever of the eight sides you stand on,
   one face reads**. Precedent and wording pattern: the specimen tray's persistent
   `MODEL COPY - not the simulation` plate (§5.9.7, R45).

   Fit check: octagon side length `= 2·r·sin(22.5°) = 2·0.255·0.38268 = 0.195 m`. 19 dmm at a 0.7 m
   viewing distance is a 13.3 mm cap height, ≈ 8 mm of advance per character, so the 19-character
   string is **0.152 m** — it fits the facet with 21 mm of margin either side. The fuller
   `AUTHORED - NOT A REAL SYSTEM` is 28 characters ≈ 0.224 m and does **not** fit at 19 dmm; it
   would need 15 dmm, below the `TypeCaption` floor. Take the short form.

   **ASCII only** (C4, §5.6.9): a plain hyphen, not an em dash or a middle dot. The legacy
   `TextMesh` font silently drops a missing glyph, and the one string in the app that must never
   silently lose a character is this one.

   **Font risk, flagged loudly.** Eight strings baked at scene-build time is exactly the shape of
   the `unity-headless-text-fonts` failure — an unserialised font reference renders perfectly in the
   editor and nothing at all in the APK. These eight are the highest-risk text in the app because
   they are the honesty guarantee. Gate them: the `text` assertion already requires
   `characterCount > 0` and `Font.HasCharacter` for every gated glyph; add the rim strings' glyphs to
   the §5.6.9 gated set, and have `verify-apk.sh` confirm the font asset is inside the APK.

### 4.6 Generated sectors, and the second kind of invention

GO → FICTIONAL SECTORS also offers "Generate a system" with a seed (§5.3). Same skin, same deck, same
rule — but the provenance class is **Procedural**, not Fictional, and the chip reads
`FICTIONAL SECTOR * · seed 8841`. The glyph differs (`*` vs `?`) and the hue differs
(`Provenance(Procedural)` = `(0.949, 0.694, 0.361)`, the amber). **The seed is on the deck rim** in
place of one of the eight `AUTHORED` facets, because a generated system's honest claim is "this came
from this number", and that is a stronger statement than "authored".

The third kind of invention — **generated worlds around real stars** (`NearbyStarSystems`, ids
`gen-hyg<row>-…`) — lives in the **Real** stage and must not get a deck, because it is Procedural
appearance over a **Measured position**: the star is real, its planets are not. Its rule is the one
the contract already has: badged Procedural with the seed shown, no projector skin. Stating the
distinction here because it is the one place the "one rule" of §4.2 needs a sentence of its own:

> A deck means **the place is invented**. A `*` badge with a seed means **the place is real and the
> detail is invented**. There is no third case.

---

## 5. First run: the first ten seconds

§5.10 already specifies the beats. Three things are missing, and one of them is the baseline's exact
defect repeating in the specification.

### 5.1 The overprint, fixed with arithmetic (this is a real bug in §5.10)

§5.10 places the status card **0.8 m ahead** and Earth **0.6 m ahead, 0.15 m below eye level**. Both
on the gaze axis. They overlap:

```
Globe:  radius 0.15 m, centre 0.6 m ahead and 0.15 m down
        distance from eye = sqrt(0.36 + 0.0225) = 0.6185 m
        angular radius    = asin(0.15 / 0.6185) = 14.03°
        centre elevation  = -atan(0.15 / 0.60)  = -14.04°
        → the globe's TOP EDGE is at -14.04 + 14.03 = -0.01°, i.e. exactly at eye level

Card:   0.286 x 0.279 m at 0.8 m (measured, baseline report)
        angular size = 20.3° x 19.8°, so ±9.9° about its own centre
        centred at eye level → its bottom edge is at -9.9°
        → the card's bottom 9.9° sits INSIDE the globe's disc
```

And that is precisely what the baseline frame shows: the card at azimuth `atan(0.48/0.70) = 34.4°`
and the globe at angular radius 24.0° — `34.4 - 24.0 - 10.15 = +0.25°` of clearance, i.e. they touch,
which is why "James Webb Space Telescope" is drawn across the card.

**Fix: raise the card.** For the H4 clearance of 2°:

```
card centre elevation ≥ (globe top -0.01°) + 2° + (card half-height 9.9°) = +11.9°
→ card at (0, +0.170, 0.80)      since 0.80 · tan(12.0°) = 0.170 m
```

The card moves from eye level to **0.17 m above eye level**, 0.8 m ahead. Text above, subject below,
clear air between — which is also a better composition than either element alone, and it is one
constant.

(Putting the card *below* does not work: the globe's bottom edge is at −28.07°, so the card would
have to be centred at −40°, far outside comfortable reading.)

### 5.2 Nothing moves at real time — and nobody has noticed

At the first-run framing, with the clock at ×1:

| Motion source | Rate at `rung:planet-hand`, U = 2.354e-5 | Visible? |
|---|---|---|
| Earth's rotation | 1 revolution per 86,400 s → the terminator crosses 0.30 m in 12 h | no |
| The ISS | orbit radius 6,791 km → **0.160 m** drawn; period 5,562 s → **0.18 mm/s** | no |
| The Moon | 0.5°/hour of true motion | no |
| The clouds layer | tied to the clock | no |

**At real time, the first ten seconds of this app contain no motion whatsoever.** A still image is
the one thing that cannot sell a headset. Three answers, in order of preference:

1. **Reward head motion.** The only real-time motion available is the viewer's own, and it is free
   and entirely honest. The first ten seconds must therefore contain **view-dependent light**: a
   specular sea highlight that slides as you lean, an atmosphere limb whose brightness is a function
   of view angle, and stereo. Every one of those is in the image-pipeline lane's brief already; what
   this document adds is that **they are the first-run requirement, not a polish item**. If the sea
   does not glint when you move your head, the app's first impression is a poster.
2. **The status card's own motion** — the per-file progress marks, and the watch-face pulse at 3 s.
   Already specified; keep them, they are the only authored motion in the window.
3. **Do not auto-warp time.** The temptation is to start at ×60 so the ISS visibly orbits. Refuse:
   "real" and "now" are §5.10's two headline promises and a silent time warp breaks both. Instead,
   at **t = 8 s**, if the user has made no input, show one chip: `Real time · [1 day per second]`.
   One tap, user-invoked, and the warp label in the header says what happened.
   (This is a proposal to the `ui` lane, not a contract change: §5.10's table ends at 20 s and adding
   a beat at 8 s is within the "one hint line" spirit. If the `ui` lane declines, items 1 and 2 still
   stand.)

### 5.3 The first ten seconds, as specified

| t | What the person **sees** | What they **do** | Room |
|---|---|---|---|
| frame 1 | The room. A status card 0.8 m ahead, **0.17 m above eye level** (§5.1): "CosmoSynth 2.0.0 · loading the universe", one line per data file with a progress mark. Clear air below it | nothing | 1.00 |
| 0.5–3.0 s | **Earth**, 0.30 m across, 0.6 m ahead and 0.15 m below eye level, hanging in the clear air under the card. Lit from the real Sun direction for now. Label "Earth · <UTC> · real time". The Moon and Sun as labelled points on the sky layer, in their real directions | nothing | 1.00 |
| 1.0–3.0 s | As the data lands, layers appear **in the order the files finish**, and each one's row on the card ticks. This is the only progressive reveal the app gets for free; it is the motion of the first three seconds | nothing | 1.00 |
| 3.0 s | The watch face on the left wrist pulses once. One hint line under the globe, generated from `XrInput.Bindings`: hands — "Pinch with both hands and pull apart to zoom out · tap your wrist for the map" | looks at their wrist | 1.00 |
| 3–6 s | **Head motion is the reward**: the specular sea slides across the ocean, the limb brightens on the side away from the Sun, the globe has real stereo depth against a real room at 116 px of disparity (`71.8 / 0.6185`) | leans, turns their head | 1.00 |
| 5.0 s | If all data loaded, the card collapses to one line beside the globe: "all data loaded · real time" | — | 1.00 |
| 8.0 s | If there has still been no input: one chip, `Real time · [1 day per second]` (§5.2 item 3) | usually taps it | 1.00 |
| 10 s | Either they have warped time and the ISS is now visibly orbiting, or they have pinched and the Moon has come into the room. Either way the app has made its promise: **a real thing, at the real time, at a real size, in their actual room** | — | 1.00 |
| 20 s | Card and hint disappear (also at the first zoom or pinch) | — | 1.00 |

**The room is at full passthrough for the entire first run.** Do not open in Space, do not dim, do
not "establish the mood". The product's differentiator in the first ten seconds is that the planet is
in *their room*, and every pixel of room you take is the differentiator you spent.

---

## 6. Conflicts found in the contract while doing this

Three, each with arithmetic. None acted on — the integrator decides.

### 6.1 The galactic matrix is still wrong, and V4 sits on it

Already known (`HANDOVER-ASTRA-VISUAL.md` §6): `Core/Frames/StandardFrames.cs` builds ICRF→galactic
with all three signs flipped, putting the north galactic pole at `(-0.346, 0.176, 0.922)` instead of
`(0, 0, 1)`. **Shot V4 is composed entirely around the galactic plane being horizontal**, and so is
the `plane` assertion. If V4 is captured before that fix, the Milky Way will be tilted by
`acos(0.922) = 22.8°` and it will look like a compositional choice. Order the work: fix the matrix,
then compose V4.

### 6.2 Rung 18 puts a surface 0.10 m from the eye, and the comfort rule deletes it

```
Rung 18 frames 8.8e23 km to 1.2 m  →  CMB ball radius R = 0.60 m, centred at the anchor
Anchor is 0.70 m from the eye
→ the ball's NEAR SURFACE is 0.70 - 0.60 = 0.10 m from the eye
```

Three rules collide there:

- §5.7.5 rule 7: "content nearer than 0.25 m to the eyes fades out" → the near cap of the CMB ball
  fades, leaving a viewer-facing hole in the sphere;
- the near clip plane is 0.05 m, so it is drawn at all, but only just;
- stereo disparity at 0.10 m is `71.8 / 0.10 = 718 px` — seven times the 103 px the `stereo`
  assertion calls the comfortable reference case, and a vergence demand at 10 cm is where people
  report eye strain.

Two ways out, both one number:

- **(a) Reframe rung 18** to `8.8e23 km → 0.80 m` (`R = 0.40 m`), putting the near surface at
  **0.30 m** — outside the fade, `239 px` of disparity, and the ball still spans
  `2·asin(0.40/0.70) = 69.4°`, more than the crop's height. The hysteresis switch at `1.05 R =
  0.42 m` stays comfortably inside 0.70 m. Costs: §2.6's walkthrough number and the `cmb-fade`
  expected values change.
- **(b) Exempt `MayEnclose` volumes and the CMB object form from rule 7.** Costs: a viewer who
  pushes further in gets a 0.63 m-radius surface arbitrarily close before the sky-form switch, which
  is what rule 7 exists to prevent.

I recommend **(a)**: it is one constant, it preserves the comfort rule universally, and 0.80 m is
still a ball you have to turn your head to take in.

### 6.3 The harness validates every pixel rule at the wrong angular size

§1.3, in full: `ppr_capture = 936` against `ppr_device = 1140`, a **21.8 %** error, on every
px-denominated rule in the app. The fix is one derived field of view. I rate this the highest-value
single line in this document, because it silently invalidates the star size floor, the body
visibility cut, the ribbon width, the "nothing here at this scale" test and the label legibility
floor all at once — which is exactly the shape of this project's own `verification-gap` pattern.

---

## 7. Costs, in the budget's own units

Budget: ≤ 11.0 ms GPU per eye, ≤ 150 draws, ≤ 1.2 M vertices, ≤ 33 step-coverages, ≤ 150 MB textures.

### 7.1 MR staging

| Item | Draws | Verts | Fragments | Step-cov. | Texture | GPU ms | Note |
|---|---|---|---|---|---|---|---|
| Space backdrop / room veil | **0** | 0 | 0 | 0 | **4 B** | **0** | shares the `cmb` sky-form renderer and its existing budget row (1 draw, 2 k verts, 1.0 eye-screen, 0.5 ms) — it is the *same draw*, not an addition. The 4 B is the 1×1 flat LUT |
| Contact-shadow texture | 0 | 0 | 0 | 0 | **4 KB** | 0 | 64×64 R8 + mips, shared by every shadow in the app |
| Exhibit plinth shadow | +1 | 4 | 0.01 | 0 | 0 | **0.02** | INFERRED, scaled from the "Body markers" row (1 draw, 1 k verts, 0.01 eye-screens, 0.05 ms) |
| Hand depth mask (**not in 2.0**) | +1 | ~600 | 0 | 0 | 0 | **0.05** | INFERRED; `ColorMask 0` writes no colour, so this is vertex + depth-test only |
| Coverage statistics in `alpha` | 0 | 0 | 0 | 0 | 0 | 0 | harness only, never in a build |
| **Total added to a shipping frame** | **+1** | **4** | **0.01** | **0** | **4 KB** | **≈ 0.02 ms** | |

Nothing in §3 costs anything. That is the point: the room dial is a uniform on a draw that already
exists, the edge treatment is MSAA that is already on, and the honest answer to "where is the contact
shadow" is "not on a floor you have not measured", which is free.

### 7.2 The fictional stage

The fictional stage **replaces** the real one (§5.3: one active stage), so its cost is not added to
the real budget — it is measured against the whole of it.

| Element | Shader | Draws | Verts | Fragments |
|---|---|---|---|---|
| Deck plate (8-gon top + rim) | `Unlit` | 1 | 25 | opaque, 0.13 sr ≈ 6.7 % |
| Emitter core | `Unlit` | 1 | 9 | opaque, ≈ 5 % |
| Chrome ribbons: border 8 + grid 32 + beam 144 = 184 segs | `Ribbon` | 1 | 736 | additive, ≈ 0.05 |
| Orbit filaments, 7 × 128 segs | `Ribbon` | 1 | 3,584 | additive, ≈ 0.06 |
| Bodies, 8 spheres @ 320 verts | `Body` | 8 | 2,560 | opaque, ≈ 0.4 % |
| Rings on 5 bodies | `Ring` | 5 | 320 | premult, ≈ 0.3 % |
| Sector dust, 250 points | `Point` | 1 | 1,000 | additive, ≈ 0.02 |
| Bracket labels + 8 rim strings | `Text` | 1 | ~1,600 | premult, ≈ 0.03 |
| Panel + inspector | `Panel`/`Text` | 4 | ~8,000 | premult, ≈ 0.2 |
| **Total** | | **23** | **≈ 17.8 k** | **≈ 0.36 eye-screens** |

Against the global limits: **15 % of the draw budget, 1.5 % of the vertex budget, 12 % of the
transparent-fragment budget, zero step-coverages.** Draw count is inside the ≤ 6 UI rule
(4 panel/text draws) with room to spare.

**GPU time: ≈ 0.7 ms (INFERRED).** Derivation: the contract's "UI panels and text" row is 6 draws,
20 k vertices, 0.3 eye-screens → 0.8 ms. The fictional stage is 23 draws, 17.8 k vertices,
0.36 eye-screens — more draws of much smaller geometry, the same fragment order. **The experiment:**
open the sector on device and read one minute of `adb logcat -d | grep VrApi` for the `App=` and
`GPU%=` fields, with `Fov=` confirming foveation is actually on. If it exceeds 1.5 ms the beam's
sub-segment count `M` drops from 6 to 3 (halving the ribbon vertices) before anything else changes.

### 7.3 APK size

Zero measurable change. The fictional catalogue is one small JSON (already budgeted in C.13), the
contact-shadow and backdrop textures are generated at runtime, and no new shader ships unless the
hand mask amendment is taken (a single-variant shader is a few KB). Against 46 MB today and a 150 MB
ceiling, this section is noise.

### 7.4 The one cost I am guessing at, and its experiment

**Capture time at 2064 × 2208 on llvmpipe** (§1.3). The eye frame is 4.56 Mpx against the current
2.07 Mpx — **2.2×** the fragments, per view, on a software rasteriser, with `shot.sh`'s `unity_run`
timeout at 1800 s and `release` expanding to 3 + 19 = 22 views.

Experiment, one editor run, no build: `tools/shot.sh --views default` at each of 1920×1080,
2064×2208 and 1032×1104, timing each with the harness's own frame counter. If 2064×2208 pushes a
22-view release run near the timeout, the fallback is **1032 × 1104 with the fov derived the same
way** — half the linear resolution, so `ppr_capture = 570`, exactly half the device value, and every
pixel figure is then testable by doubling. An exact factor of two is a far better error than 21.8 %.
The raymarched views (`rung:milkyway`, `rung:clouds`) are the ones to watch; they may need the
half-resolution profile alone, recorded in the report.

---

## 8. What this box can prove, and what it cannot

### 8.1 Verifiable headlessly on triton

| Claim | How |
|---|---|
| The capture is aimed, filled and uncollided | `compose_check.py` (§2.5), five rules, seconds per view, **and it already fails on the existing baseline PNG** |
| Every pixel rule is evaluated at the device's angular scale | the report prints `ppr` and the derived fov; assert `abs(0.5·H/tan(fovY/2) - ppr) < 1` |
| No additive layer writes coverage | the existing `alpha` assertion (delta = 0 of 255 with `COSMOSYNTH_HIDE`) |
| Coverage stays inside the walking guard | extend `ProbeDestinationAlpha` to report mean and p95; assert mean ≤ 0.60 at rungs 0–9 (§3.7) |
| The room fade obeys the comfort rule | a Core test: sample the `_Immersion` schedule at 60 Hz over the transition and assert `max Δ per 100 ms ≤ 0.30`. Pure arithmetic, no engine |
| The Threshold's timings | `StageDirector` exposes `t`; a play-mode action `stage:fictional:vg429` captures at t = 0.35, 0.90, 1.50, 2.20 s and asserts what is enabled at each |
| **`fiction-bounds`** — nothing fictional outside the projection volume | for every enabled renderer whose layer provenance is Fictional or Procedural-in-a-sector, assert its world bounds lie inside the cone `(apex = emitter centre, half-angle = atan(r_beam_top / h_beam), height = 3·h_beam)`. The **inverse** is the existing `honesty` assertion (no Fictional field drawn in the Real stage) |
| The deck-rim strings render at all | the `text` assertion plus the §5.6.9 gated glyph set |
| No skybox | one grep in `code_gate.sh` for `RenderSettings.skybox` |
| The stage boundary in Core | `T/StageBoundaryTests.cs`, already specified |

Every one of the new checks above must land with a deliberately broken run recorded in the integrator
log, per §11.3. Two of them have their broken input for free: `compose` fails on
`/tmp/cs-baseline/app-running.png`, and `fiction-bounds` fails if you move one node's `a` to 4.0 AU.

### 8.2 Only a headset can settle these

| Question | Why the harness cannot answer it |
|---|---|
| **Does the room swamp the content?** (`L_room`, §3.1) | the harness composites nothing; there is no room. This is the single biggest unknown in this document |
| Is the 0.70 s room fade comfortable? | comfort is not a pixel |
| Does the deck read as a physical object in the room, or as a decal? | needs the compositor, MSAA on real edges, and real parallax |
| Does your hand vanishing into a planet disturb people? (§3.6(d)) | needs hands |
| Is 718 px of disparity at rung 18 painful? (§6.2) | needs two eyes |
| Does the Threshold's 0.4 s of empty room read as a boundary or as a bug? | needs a person |
| Whether the projector holds 72 Hz | `Fov=` in `VrApi` is the only proof foveation is on |
| Whether the 21.8 % pixel-scale error was hiding anything | the whole point is that the harness could not see it |

Add all eight to the H-series checklist in `docs/reports/handoff-<ver>.md`. And write the standard
sentence in every capture note: *a flat monoscopic capture on llvmpipe cannot prove stereo,
passthrough, frame rate or foveation.*

---

## 9. Implementation plan

Ordered so that each step's output is the next step's input, and so that nothing is composed against
geometry that is about to change.

| # | Step | Owner | Depends on | Size |
|---|---|---|---|---|
| 1 | Fix `StandardFrames` galactic signs (§6.1) | `p0-core` | — | already in Phase 0 |
| 2 | Derive the capture fov from `PixelsPerRadian`; capture at 2064 × 2208; write `<view>.png` + `<view>.key.png` (§1.3, §2.1) | integrator, in `PlayModeShot` | 1 | 0.25 d |
| 3 | Run the resolution experiment (§7.4) and pin the profile | integrator | 2 | 0.1 d |
| 4 | Add `-shotPitch` / `-shotYaw`; print both in the report (§2.2) | integrator | 2 | 0.1 d |
| 5 | Pin `T0` with the Moon/phase sweep (§2.3) | `solar` | 4 | 0.1 d |
| 6 | Write the seven shot cards into `tools/gates/views.txt` as data — pitch, yaw, crop, fill bounds, foreground id (§2.4) | integrator | 4, 5 | 0.25 d |
| 7 | `compose_check.py` + its failing run against the baseline PNG (§2.5) | integrator | 6 | 0.4 d |
| 8 | `_Immersion = 1 - RoomVisibility`; the shared Sky renderer with the 1×1 flat LUT; the 0.70 s smootherstep; the §3.5 thresholds (§3.3, §3.4) | `nav` + integrator (the Sky material is `p0-shaders`' file) | 2 | 0.5 d |
| 9 | `RoomDefault` column on `LayerTable.Rungs`; room changes only on rung/GO/ROOM (§3.5) | `nav` | 8 | 0.2 d |
| 10 | Coverage statistics in the `alpha` probe; the ≤ 0.60 guard and the `ROOM n %` readout (§3.7) | integrator + `ui` | 8 | 0.3 d |
| 11 | Contact-shadow texture + material; wire to the exhibit plinth and the tray only (§3.6(b)) | `fiction` (plinth), `ui` (tray) | — | 0.2 d |
| 12 | First-run card at `(0, +0.170, 0.80)`; the 8 s real-time chip (§5) | `ui` | — | 0.2 d |
| 13 | Projector deck, emitter, grid, border, beam-as-ribbons, with the Fictional-hue correction (§4.4) | `fiction` | 8 | 1.0 d |
| 14 | `StageDirector` Threshold: the three acts, their curves and windows (§4.3) | `fiction` | 13 | 0.75 d |
| 15 | Deck-rim strings + glyph gating; the sector clock in the header (§4.5) | `fiction` + `ui` | 13 | 0.3 d |
| 16 | `fiction-bounds` assertion + its broken run (§8.1) | `fiction` | 13 | 0.2 d |
| 17 | Capture the seven views, read every PNG at G1d, and write the before/after pair against `/tmp/cs-baseline/app-running.png` | integrator | 7, 9, 13 | 0.25 d |
| 18 | Take steps 2–10 and 13–16 to the headset; answer the eight questions in §8.2; fold `L_room` back into §3.2 | owner + integrator | 17 | — |

Total ≈ **5.1 agent-days**, of which 1.1 is the integrator's and 2.25 is `fiction` — which is inside
the 5.0 days §0.4 already allocates to `fiction` in 1b.

**The three things to do first if only three get done:** step 2 (the whole harness is measuring the
wrong angular size), step 7 (turns "aimed" into a gate with a free failing fixture), and step 8 (the
room dial is the difference between a black hole in your room and a window).

---

## 10. Claims ledger

**VERIFIED** — measured, read, or arithmetic shown here:
the baseline frame's disc geometry (885 px wide at row 928, clipped 293 px below the frame, 21.1 % of
the frame); `f = 936.4 px` and `fovY = 59.95°` for the current capture; `R_eff = 0.302 m` against a
0.2793 m shell; `ppr_capture / ppr_device = 936 / 1140 = 0.821`; the house angle `atan(0.25/0.70) =
19.65°` and its 0.336 axis ratio; disparity `71.8 / d` px and its agreement with the contract's own
103 px and 0.96 px; the smootherstep peak rate `1.875 / T` and therefore `T ≥ 0.625 s`; the 0.4 s
stage fade's `0.60 × 4.69 = 28.1 pp/100 ms`; the first-run card/globe overlap and the `+0.170 m` fix;
V2's six complete ellipses (`0.591 m` half-width → 11.8 AU) and Saturn's near arc at 0.22 m inside
the comfort fade; Earth at V1 covering 25.8 % of a 1.95 sr eye frame; rung 18's near surface at
0.10 m and 718 px; every projector constant, read from
`refs/webxr/pages/holographic_star_map(2).html:202-271`; the beam falloff nodes; the octagon facet
`0.195 m` against 19 dmm type; Reverie's shipped `_Strength 0.42`, `smoothstep(1.0, 0.15, d)²` and
`LerpSpeed 0.6`; `CosmoTheme`'s Fictional `(0.894, 0.541, 0.847)` and the cyan-is-reserved rule;
the contract's own rules cited throughout.

**INFERRED** — judgement, or a number nobody has measured:
`L_room` (the room's luminance floor) and therefore `G_pt`; the 0.60 coverage guard; the 78° ± 8°
lighting rule; the 0.55 m foreground distance and the 2° clearance; the §3.5 thresholds (0.55, 0.70);
the `RoomDefault` ladder; the three-act timings (0.70 / 0.40 / 1.00 s) and the Act-3 windows; the
projector's 0.7 ms and the hand mask's 0.05 ms; the plinth shadow's 0.02 ms; 24 beam verticals and 6
sub-segments; the T0 equinox minute; that 2064 × 2208 renders acceptably on llvmpipe; that a
`ColorMask 0` pass at `Queue Geometry-1` behaves as expected under URP 17.5 multiview.

**Guessed costs, each with its experiment, in one place:**
`L_room` → §3.1's exposure sweep against a white wall and a dark corner.
The projector's GPU ms → one minute of `VrApi` with the sector open.
Capture time at the eye resolution → §7.4's three-resolution timing run.
The hand mask → build it only after the first two are answered.
