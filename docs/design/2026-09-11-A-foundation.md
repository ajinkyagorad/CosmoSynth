# CosmoSynth 2.0, Section A: Foundation (render, frames, placement, layers)

2026-09-11 · design document · slug `A-foundation` · authority for every lane that renders, places or
orients anything. Built on the eight reports in `docs/research/2026-09-11-*.md`, the project's
`CLAUDE.md`, and the source at git `cf93a25`. Nothing was built, launched or changed while writing
it. The only commands run were read-only greps on the box, plus two stdlib Python scripts on the
laptop that produced every number below (`calc.py`, `frames.py`, `walk.py`; the formulas are
restated in the text, so no one has to re-run them).

**Tags.** **VERIFIED**: read in code or a report, or computed here, with the location or formula
given. **INFERRED**: engine or GPU behaviour I could not observe on this box. Line numbers refer to
`cf93a25`. `CS/` means `unity/Assets/CosmoSynth/`.

**Binding decisions.** Nine were given, and this section designs within them: one app; URP 17.5 +
Linear; one XR camera; depth submission None; symmetric projection on; input-polling latency; MSAA 4
from the URP asset; SRP foveation; buffer discards on; per-group similarity about the eye; one
ICRF→Unity mapping inside `FloatingOrigin`; a single `SetCatalog`; populations as layer renderers;
no runtime network. None of them turned out to be unworkable. §1.6 does extend decision 3: rigid
body groups use the similarity, and three cases the similarity cannot express use **per-vertex
radial compression with the same function**. Those cases are point populations, ribbons whose
owner contains the eye, and bodies the eye is close to. I say so explicitly here because the
decision text names only the similarity.

---

## 0. The foundation on one screen

| # | Decision | Value | Why (details in) |
|---|---|---|---|
| 1 | Cameras | exactly **one** XR camera; `DepthBands` deleted | 1.7.0 grid mechanism (§1.1) |
| 2 | Clip planes | near **0.05 m**, far **2000 m**, fixed forever | nothing placed beyond 1500 m (§1.3) |
| 3 | Compression | `f(d) = d` for d ≤ d0; `f(d) = d0·(1 + ln(d/d0))` beyond; **d0 = 10 m** | C¹, monotonic, 23.0 m of depth per decade, 1e27 m → 608.7 m (§1.3) |
| 4 | Depth budget (D24) | inside any group, surfaces separated by more than **8.0e-4 of their distance** are ordered correctly, anywhere up to 4.4e29 m | Δz(z) = z²/(n·2²⁴) (§1.4) |
| 5 | Groups | rigid similarity `p' = E + s·(p − E)`, with `s = f(D)/D` **per group, never per body**; a group stays rigid only while its **rendered radius ≤ 10 m** | caps the rendered depth extent, so that groups at different scales cannot interleave (§1.6) |
| 6 | Inter-group order | "push toward the eye": process groups farthest first; shrink a nearer group's `s` until its rendered far point clears the farther group's near point by 2Δz | depth order between any two groups that overlap on screen is guaranteed (§1.6) |
| 7 | Sky layer | direction-only content (the CMB, the 10,224 distance-unknown stars), placed **per eye** at 1500 m, drawn first, no depth write | zero disparity, i.e. truly at infinity (§1.2) |
| 8 | Axis mapping | fixed `universeLocal = P·(abs − O)·S` inside `FloatingOrigin`; P swaps y↔z (a right-handed to left-handed change of basis, det −1). The presentation plane is a separate rotation on the Universe host (Section C §2.3) | host at identity: Unity +y = ICRF +z; after levelling, **world +y = the active plane's north pole** (§2.1) |
| 9 | Level policy | *when*: Section C §2.3 (a rung tap, a GO or the PLANE control; a hint chip; never on zoom). *How*: the host rotation, animated or blinked, pivoting at the origin; grabs stop it | §3 |
| 10 | Layers | a ~40-line `CosmoLayer` base class plus a static `LayerRegistry`. Visibility is `LayerState.Weight` (Section C §4.1), data comes from `DataLoader.LayerReady` (Section B §3), and the base adds placement, budgets, groups, destinations and picks | §4 |
| 11 | Shaders | 11 single-variant `CosmoSynth/*` URP HLSL shaders plus `CosmoCore.hlsl`; **no** Resources material templates. Section D's provisional names are mapped in §5.8 | §5 |
| 12 | Frame budget | ≤ 11 ms app GPU at 72 Hz, ≤ 150 draws, ≤ 1.2 M vertices, ≤ 3 transparent eye-screens, raymarch ≤ 30 step-coverages | §6 |

**New defect found while designing (VERIFIED by computation).**
`Core/Frames/StandardFrames.cs:105-108` builds `EquatorialToGalactic` with all three signs flipped.
It maps the north galactic pole (RA 192.85948°, Dec 27.12825°) to (−0.346, 0.176, 0.922) instead
of (0, 0, 1), and differs from the Hipparcos matrix by up to 1.75 per element. It has zero test
references, which is why it survived. The correct product in Core's own vector-rotation
convention is
`RotationZ(+32.93192°) · RotationX(−(90° − 27.12825°)) · RotationZ(−(90° + 192.85948°))`, and it
matches the Hipparcos matrix to 4.9e-11. Every galactic layer depends on it, so the fix is in §2.2
and a Foundation deliverable.

### Alignment with Sections B, C and D

Sections B, C and D were written in parallel, and I read them after drafting this one. Section D
§0 states the order of authority: Foundation for rendering, distance, axes, catalogue and XR
settings; Data for formats, importers and licences; Runtime/UI for the layer table and panel;
Process for how work is split.

**Adopted from them** (this document defers):

| From | What |
|---|---|
| C §2.3 | the `PresentationPlane` enum and *when* the plane changes |
| C §C1 | `ScaleLimits`: UnitsPerKm ∈ [2e-25, 1e3] |
| C §4.1–4.2 | the `LayerTable` ids and windows, and `LayerState.Weight` |
| B §3 | `StreamingAssetReader`, `DataLoader`, `LayerReady` and `manifest.json` |
| B §4 | `CatalogAssembler`: exoplanets become a layer, not part of the startup merge |
| B §2.13 | the CMB is a 6 × 1024² R8 **cube map** in ICRF components |
| D | lanes, phases and file ownership (§10 maps this section onto D's Phase 0) |

**Kept here, because rendering belongs to the Foundation:** the compression function, groups, the
mapping, the shader contract and names, the queues and the budgets.

**Conflicts still open:**
- D's P0.4b/P0.4d name TextMeshPro's shader, which binding 2 excludes (§9 item 10).
- D's provisional shader names differ from these (mapped in §5.8).

### Constants: the single source

All of these live in Core `ScaledSpace` (§1.7). Shaders receive the ones they need through
`_CS_Compress`. No other file may restate a number from this table.

| Name | Value | Meaning |
|---|---|---|
| `IdentityRadiusMetres` (d0) | 10.0 | inside this rendered distance, placement is exact (s = 1) |
| `MaxRigidRadiusMetres` (H) | 10.0 (enter 9.0 / leave 11.0) | a rigid group's rendered radius cap, with hysteresis |
| `NearPlaneMetres` | 0.05 | hands and wrist panels need it (`XrRig.cs:70`) |
| `FarPlaneMetres` | 2000 | ≥ 1.33 × the largest placed distance |
| `SkyRadiusMetres` | 1500 | radius of the per-eye sky sphere; also the cap for enclosing volumes |
| `DepthBits` | 24 | expected eye depth format D24_UNORM_S8 (INFERRED; logged on device, §8) |
| `PushMarginQuanta` | 2 | inter-group clearance, in depth quanta |
| `NearPointFloorMetres` | 5.0 (d0/2) | a push never brings a group's near point closer than this |

---

## 1. Camera, depth, layering and distance compression

### 1.1 One camera

`XrRig` creates exactly one camera and nothing else may add another. Settings:

| Setting | Value | Note |
|---|---|---|
| near / far | 0.05 / 2000 m | constants above; set once in `BuildRig`, never re-fitted per frame |
| clear | SolidColor; (0,0,0,0) in passthrough; (0.004, 0.005, 0.011, 1) in full-space VR | Meta's passthrough rule `ARCameraFeature.cs:194` (render-xr-audit §3) |
| `allowMSAA` | true | the URP asset supplies 4×; with a single camera, Optimize Buffer Discards makes it memoryless (safe) |
| `allowHDR` | false | |
| URP camera data | `renderPostProcessing=false`, `antialiasing=None`, `requiresDepthTexture=false`, `requiresColorTexture=false`, `renderShadows=false` | set through `camera.GetUniversalAdditionalCameraData()` |
| TrackedPoseDriver | unchanged (`UpdateAndBeforeRender`) | |

Why one camera, in one line: the 1.7.0 "100 plus Earths on a grid" had a verified set of
ingredients. Camera stacking made the second pass load a memoryless MSAA attachment
(render-xr-audit §2.2). With one camera, that attachment is never loaded.

### 1.2 Three placement modes and the render queues

Every renderer is in exactly one placement mode:

| Mode | Used by | How positions reach the screen | Depth |
|---|---|---|---|
| **Rigid** (similarity about the centre eye E) | body groups (planetary and star systems, fictional holograms), bounded volumes, the CMB object form, ribbons owned by a rigid group | the CPU sets the transform: `localPosition = E + s·(p − E)`, `localScale ×= s`; the shader is unaware | exact inside the group; §1.6 orders groups against each other |
| **Per-vertex** (radial compression about E) | point populations, ribbons whose owner contains the eye, "envelope" bodies the eye is close to | the vertex shader computes `CS_CompressWS(posWS)`: direction exact, distance `f(d)` | order along every ray is exact (f is monotonic) |
| **Sky** (direction only, per eye) | CMB (inside form), the 10,224 distance-unknown stars | the vertex shader computes `eyeWS_thisEye + dir·1500` | none: drawn first, no depth write |

Near content is not a separate mode. It is simply content with `s = 1`, which is automatic
whenever `D + R ≤ d0` (f is the identity there).

**Render-queue table.** This is the single ordering; every lane uses these numbers.

| Queue | Content | Blend (colour, alpha) | ZWrite | ZTest | Writes passthrough alpha? |
|---|---|---|---|---|---|
| 1000 | Sky: CMB | `One Zero` (it is the first draw) | Off | LEqual | = `_Immersion` (0 in MR, so the layer is skipped; opt-in VR/full-space sets 1) |
| 1010 | Sky: direction-only stars | `One One, Zero One` | Off | LEqual | no |
| 2000 | Opaque: bodies, spacecraft, beacon bars, pointer ray | opaque | On | LEqual | yes, 1 (an object occludes the room, correctly) |
| 3000 | Rings | `One OneMinusSrcAlpha, One OneMinusSrcAlpha` (premultiplied over) | Off | LEqual | coverage = opacity |
| 3010 | Bounded volumes (dust) | premultiplied over, `Cull Front` | Off | LEqual | coverage = opacity |
| 3050 | Point populations, body billboards and markers | `One One, Zero One` | Off | LEqual | no |
| 3060 | Ribbons: orbits, constellations, grids | `One One, Zero One` | Off | LEqual | no |
| 3100 | Atmospheres, selection rim | `One One, Zero One` | Off | LEqual | no |
| 3200 / 3201 | Panel backing / accent | premultiplied over | Off | LEqual | coverage |
| 3210 | Text | premultiplied over | Off | LEqual | coverage |

`ZTest Always` is forbidden unless the pass carries a `// ztest-always: <reason>` comment. The
gate in §8 enforces this. No pass in this design needs it.

### 1.3 The compression function: choice and proof

**Definition** (identical in Core double and shader float):

```
f(d) = d                          for d ≤ d0            d0 = 10 m
f(d) = d0 · (1 + ln(d / d0))      for d > d0
f⁻¹(y) = y  (y ≤ d0),  d0 · exp(y/d0 − 1)  (y > d0)
s(d) = f(d)/d                      (= 1 for d ≤ d0)
```

**Properties** (VERIFIED algebraically, and numerically with `calc.py`):

1. **Identity inside d0.** The room, the hologram and the wrist are exact.
2. **C¹ at d0.** f(d0) = d0, and f′(d0⁺) = d0/d0 = 1. Numeric slopes either side are
   0.9999999993 and 0.9999999477 (h = 1e-6).
3. **Strictly increasing**, f′ = d0/d > 0, so depth order along any ray is preserved.
4. **Each decade costs exactly d0·ln 10 = 23.03 m.** Every scale of structure gets the same
   share of depth, which a 30-decade universe needs.
5. **Bounded in practice.** f(1e27 m) = 608.67 m. The largest render distance anything can reach
   is the CMB at C's maximum scale, `ScaleLimits.MaxUnitsPerKm` = 1e3 m/km: 4.28e26 m, with
   f = 600.2 m. The tables here go on to 4.4e29 m (a hypothetical 1e6 m/km) as margin. A rigid
   group adds at most H = 10 m, so every placed opaque surface lies within 680 m, and only enclosing
   volumes reach 1500 m (§1.6).
6. **Angular size is exact under the group similarity.** `asin(s·R / (s·D)) = asin(R/D)`. It is
   also exact under per-vertex compression, because every vertex keeps its direction from E.

**Known-answer vectors** (these go into the tests, §1.7):

| d (m) | f(d) (m) | s = f/d |
|---|---|---|
| 0.05 | 0.05 | 1 |
| 10 | 10 | 1 |
| 11 | 10.953101798 | 9.957365e-01 |
| 20 | 16.9314718056 | 8.465736e-01 |
| 100 | 33.0258509299 | 3.302585e-01 |
| 1e3 | 56.0517018599 | 5.605170e-02 |
| 1e6 | 125.12925465 | 1.251293e-04 |
| 1e9 | 194.20680744 | 1.942068e-07 |
| 1e12 | 263.284360229 | 2.632844e-10 |
| 1e18 | 401.439465809 | 4.014395e-16 |
| 4.4e26 | 600.462318658 | 1.364687e-24 |
| 1e27 | 608.672124178 | 6.086721e-25 |
| 4.4e29 | 669.539871448 | 1.521682e-27 |

**Why not the exp-saturation proposal** (render-xr-audit §6.2: D0 = 50 m, Dmax = 900 m). It
saturates. f(6 km) = 899.22 m is already within one D24 quantum (0.97 m at 900 m) of Dmax, and
f(20 km) is 900 − 5e-8 m, the same float value. So **everything beyond ~6 km of render distance
lands on one depth value**, and at the Earth-hologram scale that means everything beyond ~1 AU.
Group order across scales becomes impossible, which is the one thing the far layer must provide.
Rejected.

**Why d0 = 10 m and not smaller.** The relative separability thresholds scale linearly with d0
(§1.4): d0 = 5 halves them. The cost of a smaller d0 is that content 5–10 m away (a large room, or
a solar system scaled to 12 m) would be gently compressed, so walking toward it would feel
"rubbery". Angular size and direction stay exact either way; only vergence changes. d0 = 10 already
gives ≥ 1.8 depth quanta for the tightest real case (§1.4), so the constant stays at 10 as a
**calibration knob**. It is the first thing to halve if the headset ever shows z-fighting.

### 1.4 Depth separability on D24 across 1e-3 … 1e27 m

With a UNORM depth buffer, near plane n and N bits, one quantum at view distance z is
`Δz(z) = z² / (n·2^N)` when far ≫ near. Reversed-Z does not change this for UNORM (NVIDIA
depth-precision article, quest-rendering-techniques §3.1). Below 0.05 m nothing is drawn (near
plane). From 0.05 to 10 m content is exact: Δz(1 m) = 1.2 µm and Δz(10 m) = 0.12 mm.

| true d (m) | f (m) | Δz(f) (m) | **inside a group**: Δz/f | **per-vertex or per-point, between rays**: Δz/(f′·d) = Δz/d0 |
|---|---|---|---|---|
| 11 | 10.95 | 1.43e-4 | 1.3e-5 | 1.4e-5 |
| 1e3 | 56.05 | 3.75e-3 | 6.7e-5 | 3.7e-4 |
| 1e6 | 125.13 | 1.87e-2 | 1.5e-4 | 1.9e-3 |
| 1e9 | 194.21 | 4.50e-2 | 2.3e-4 | 4.5e-3 |
| 1e12 | 263.28 | 8.26e-2 | 3.1e-4 | 8.3e-3 |
| 1e18 | 401.44 | 0.192 | 4.8e-4 | 1.9e-2 |
| 1e24 | 539.59 | 0.347 | 6.4e-4 | 3.5e-2 |
| **1e27** | 608.67 | 0.442 | **7.3e-4** | 4.4e-2 |
| 4.4e29 | 669.54 | 0.534 | **8.0e-4** | 5.3e-2 |

What each column guarantees:

- **Inside a rigid group** (the column that matters for occlusion). Two surfaces whose true
  separation along a ray exceeds 8.0e-4 of their distance are ordered correctly at any scale. A
  group of apparent radius α pixels therefore resolves features separated by more than
  `8.0e-4 · 1432/α` of its own radius (1432 px/rad at 25 px/deg): **1.1 % at 100 px, 11 % at
  10 px**. Real cases:
  - Io transiting Jupiter from Earth at true scale (1000 m/km): separation 5.6e-4 against a
    threshold of 3.1e-4, so 1.8 quanta. Correct.
  - Saturn's back ring against its disc (gap ≥ R_S): correct once Saturn is wider than ~3 px.
  - The ISS 400 km above Earth (6 % of R⊕): correct.
  - Landers on the surface (gap ≈ 0): never correct by depth. Landers are drawn at true scale or
    as a marker already (`CelestialBodyView.cs:622-641`), and the marker writes no depth.
- **Between groups:** not a precision question, because §1.6 enforces ≥ 2 quanta of clearance by
  construction.
- **Per-vertex or per-point content:** it never writes depth, except envelope bodies. Their near
  and far sides are separated by a whole diameter, so they are far above threshold.
- **With D16** (today's `Depth16Bit` submission), the inside-group threshold becomes 3.3e-3 at
  11 m and 0.19 at 1e27 m. That is unusable, which is one more reason for submission None. The
  depth format is **logged on device**, and a 16-bit format turns the `render:` line red (§8).
  Nothing here is designed to work on D16.

**Float precision of f in shaders.** `log` in float has a relative error of ~1e-7, so the error in
f at 600 m is ~6e-5 m, about 7,000 times smaller than Δz(600 m) = 0.43 m.

### 1.5 Stereo-disparity correctness

Disparity at rendered distance y is IPD/y (IPD 63 mm; 25 px/deg means 1 px = 0.70 mrad).

1. **Geometric consistency.** All compression happens about **one** point, the centre eye
   `_CS_CentreEyeWS`, set once per frame from the camera transform. Both eyes therefore view one
   rigid 3D scene, so there is no binocular rivalry, no vertical disparity, and consistent
   vergence. Compressing about each eye separately would give the two eyes different geometry. It
   is forbidden.
2. **Exact near.** For d ≤ d0 the scene is true, so disparity is true (≥ 9.0 px at 10 m).
3. **Monotonic everywhere.** Disparity IPD/f(d) falls strictly as true distance grows, from 9.0 px
   at 10 m to 1.8 px at f = 50 m and 0.15 px at f = 600 m. The sky gets exactly 0, because it is
   placed per eye (`GetCameraPositionWS()` + dir·1500). So near < compressed < sky in perceived
   depth, always.
4. **Head-motion parallax is exact, not compressed.** Placement is recomputed every frame about the
   *current* eye, so the rendered direction to every object is its true direction from the eye.
   Walking one metre moves the Sun by exactly its true parallax. Only binocular disparity reflects
   f.
5. **Bounded distortion.** Compressed content shows more disparity than the truth (at most 9 px
   instead of ≈ 0). It always appears **behind the room**: rendered near points stay ≥ 5 m (§1.6),
   and passthrough walls at 2–5 m have 13–30 px of disparity. That is the MR requirement, and
   "things beyond the walls look beyond the walls" holds.
6. **Per-eye angular error of rigid groups** (compressed about the centre, viewed from ±32 mm):
   relative ≤ 0.032/f(D), which is ≤ 0.3 % at 10 m.

A headset alone can say whether 1–9 px of far disparity is comfortable (§8, headset list).

### 1.6 Groups

**Definition.** A group is a set of renderers that share one similarity per frame. There are two
kinds:

- **Body group**: a subtree of the catalogue tree. Its **anchor** is the subtree root body's
  position, not a bounding-sphere centre. That choice is what makes a per-vertex ribbon pass
  exactly through a rigid group's rendered centre: at the anchor, `f(D)·û = s·D·û`. The group's
  radius R is the maximum over drawn descendants of `|p_i − p_anchor| + drawnRadius_i`,
  recomputed every frame (O(n), n ≈ 250), plus 10 %. Examples:
  - a planetary system (planet, moons, rings, atmospheres, spacecraft whose parent chain ends
    there);
  - a star system (a star and its planets; the 60 exoplanet hosts are roots);
  - a fictional hologram system.
- **Layer group**: declared by a layer through `DeclareGroups` (§4). Examples are the dust volume,
  the CMB object form, a hero galaxy impostor and a phenomenon exhibit.

**Not groups.** Point populations (stars, galaxies, clusters, belts, Cepheids) are placed per point
in the vertex shader. A point has no extent, so the per-body trap (a moon vanishing behind its
planet) cannot arise, and per-point f is exact for them. Sky content and per-vertex ribbons are not
groups either.

**Modes per node** (the output of the solver):

| Mode | When | Scale |
|---|---|---|
| `Collapsed` | an ancestor is Rigid | the ancestor's s |
| `Rigid`, owning the subtree | `s·R ≤ H` for the whole subtree (rendered radius ≤ 10 m; hysteresis 9/11 m) and eye outside (D > R); all descendants Collapsed | s = f(D)/D |
| "dissolved" (reported as `OwnsSubtree = false`) | a node with children failing that test: its own body is then tested as a leaf (Rigid or Envelope) and each child is tested separately | the leaf's |
| `Envelope` | a **leaf** (one body and its decorations) failing the Rigid test: the eye is near a body big on screen | 1, and the body's materials set `_PerVertexCompress = 1` |
| `Enclosing` | layer groups only, flagged `MayEnclose` (volumes, the CMB object form): may contain the eye | `min(1, 0.75·Far/(D + R))` (≤ 1500 m) |

Why the cap is the rendered radius ≤ 10 m (VERIFIED arithmetic). A similarity scales a group's
depth extent by the same s as its distance, whereas f compresses the *gaps between* groups by far
more (23 m per decade). A rigid group rendered 300 m deep would interleave with groups several
decades away. Capping the rendered radius at H = d0 does four things:

- it bounds every rigid group's rendered depth extent to ≤ 20 m, less than one decade;
- it keeps rendered near points ≥ `f(D) − 10` m, which is ≥ 6.9 m whenever D ≥ 2·d0, so outside
  the room;
- it puts every rigid surface within 680 m;
- it turns "the eye is close to a big body" (surface under you at true scale) into Envelope mode
  instead of pulling the surface into your face.

The last point matters: similarity would put the ground 1.6 m below you at
`f(D)·1.6/D ≈ 36 µm`, which is inside the near plane.

**Push toward the eye** (inter-group order; runs after mode assignment, O(k²), k ≤ 64 groups that
draw opaque geometry this frame):

```
sort rigid and envelope groups with HasOpaque by true D, descending
for each group g in that order:
    for each already-placed group h (farther) that overlaps g on screen:
        # angular test: angle(û_g, û_h) < asin(ρ_g) + asin(ρ_h) + 1e-3
        zNearH = h.RenderedNear                 # = s_h (D_h − R_h), or f(D_h − R_h) for envelope
        if s_g (D_g + R_g) > zNearH − 2 Δz(zNearH):
            s_g = (zNearH − 2 Δz(zNearH)) / (D_g + R_g)
    if s_g (D_g − R_g) < NearPointFloorMetres:      # never pull a group into the room
        s_g = NearPointFloorMetres / (D_g − R_g); result.Conflicts++   # reported in the render line
    g.RenderedNear = s_g (D_g − R_g)
```

Pushing changes only depth, never screen position or angular size. The one visible side effect is
a disparity step: a per-vertex parent orbit then passes the pushed group's centre at a slightly
different depth.

**Points against rigid groups: the bounded artefact.** A point at true distance x in front of a
rigid group is rendered at f(x), while the group's near face is rendered at `f(D)·(1 − ρ)`. Because
f(x)/x decreases, a point with `x ∈ [D·e^(−r/d0), D − R]` that lies on the group's disc on screen
(r = rendered radius) can be hidden wrongly. With r ≤ 1 m, typical, the band is the 10 % in front of
the group's near surface; at the r = 10 m cap it is [0.37·D, D − R]. Real cases: asteroid-belt
points in front of Jupiter from Earth give a band of 0.3 % of Jupiter's distance, which is empty.
This is accepted and written into the gates as a known bound.

**Handover across 30 orders of magnitude.** The cut is recomputed every frame from the rendered
radius, with hysteresis. At a switch between Rigid and Dissolved, screen positions and angular sizes
are identical in both modes, because both are about E. Only depth steps, and the step is ≤ ~1.5 px
of disparity at f ≈ 20 m and ≤ 0.1 px at f ≈ 100 m. A walkthrough (VERIFIED with `walk.py`; r is the
rendered radius):

| Scale (UnitsPerKm, m/km) | What is where |
|---|---|
| Earth hologram 0.55 m (4.31e-5) | Earth: identity at 0.7 m. The Earth system's R (the Moon's orbit, 16.6 m) exceeds D, so it **dissolves**, and the Moon is its own rigid leaf at f = 15.05 m, s = 0.908. Sun: rigid, f = 74.7 m, r = 0.35 m. Jupiter system: f = 89.1 m, r = 0.27 m. α Cen: f = 200 m (billboard). Galactic-centre stars: per-point, f = 287 m. CMB: sky. |
| Solar System in the room, Neptune orbit r = 1.5 m (3.34e-10) | Sun subtree D + R ≤ 10 m: all identity; Voyager 1 at 8.3 m, still identity. α Cen at f = 82 m. M31 per-point at 215 m. CMB: sky. |
| Nearby stars, 10 pc per metre (3.24e-15) | α Cen at 0.13 m, identity; stars in the room are true 3D. Galactic centre per-point at 54 m. Dust box is `Enclosing` with s = 1 (eye inside). CMB: sky. |
| Milky Way tabletop, 30 kpc per metre (1.08e-18) | Dust box 6.7 cm, identity. GC 0.27 m. M31 at f = 19.6 m (per-point). Virgo 50 m. CMB (4.6e5 m): sky. |
| Local Group, 3 Mpc per metre (1.08e-20) | M31 0.26 m; Virgo 5.5 m, identity. CMB sphere 4.6 km around the eye: sky. |
| Observable universe, CMB r = 0.6 m (1.40e-24) | Everything identity. The **CMB object form** is rigid, with a crossfade over 0.9–1.1 R against the inside (sky) form. |
| True scale, 1 m per metre (1000) | Earth: **Envelope**. Moon rigid at f = 184.7 m, r = 0.84 m. Sun f = 244 m, r = 1.14 m. Jupiter system f = 259 m. α Cen f = 370 m. M31 per-point f = 502 m. CMB: sky. |

**When the viewer is inside a group:**

- Body subtree: it dissolves, its anchor body is tested as a leaf, and its children are tested
  independently.
- Inside a leaf's sphere (inside the body, or inside its atmosphere at a big scale): Envelope. The
  body renders per-vertex. The atmosphere integrates analytically in true eye-relative space, in
  units of the planet radius (a port rule for the atmosphere lane, §5.8).
- Inside a volume or the CMB: Enclosing. The raymarch runs in object space from the per-eye ray
  origin. A similarity about E keeps the eye at the same object-space point, so the march is
  unaffected.
- Envelope spheres are exact only to ulp(|T|) ≈ 6e-8·|T|. Standing on a true-scale planet shows
  ~0.5 m quantisation until Phase-3 terrain arrives with tile-local origins. This is a known limit,
  not a bug.

**Size floors never create false occlusion.** A body whose true apparent diameter is below
2.5 px (`CelestialBodyView.cs:300`) is **not** drawn as an inflated sphere any more. It is drawn by
the body-point path (additive, no depth write), and `IsGeometryDrawn` is false. An inflated
sphere would break inside-group order, since a floored moon would intersect its planet. The rule
is also CLAUDE.md's own: a one-pixel feature has no shape.

### 1.7 Core API: `Core/Scale/ScaledSpace.cs` (engine-free) and its tests

A new file, next to `ScaleSpace.cs`. The dead band stack in `ScaleSpace.cs:28-78` and `:150-179`
(`ScaleBand`, `DefaultBands`, `SelectBand`, and the "multi-pass compositor" and "reversed float
depth" comments at `:29-35` and `:152-158`) is **deleted** in the same change, so there is
exactly one account of distance in the codebase.

```csharp
namespace CosmoSynth.Core
{
    public enum GroupMode : byte { Collapsed, Rigid, Envelope, Enclosing }

    /// One node per catalogue body (in Traverse order: parents before children) plus one per
    /// layer group. Filled by ScaleRenderer every frame.
    public struct GroupNode
    {
        public int Parent;            // index of the parent node, -1 for a root
        public Double3 AnchorKm;      // absolute ICRF km of the subtree root body / layer group centre
        public double RadiusKm;       // subtree bounding radius about the anchor (drawn extents, +10 %)
        public double LeafRadiusKm;   // the anchor body's own drawn radius incl. rings/atmosphere
        public bool HasChildren;
        public bool HasOpaque;        // this node's own renderers write depth this frame
        public bool MayEnclose;       // layer volumes and the CMB object form only
    }

    public struct GroupPlacement
    {
        public GroupMode Mode;        // how THIS node's own renderers are placed
        public bool OwnsSubtree;      // Rigid and the whole subtree collapsed into it; false = "dissolved"
        public int RigidOwner;        // node whose similarity applies (self or rigid ancestor); -1 if Envelope
        public double Scale;          // s; 1 for Envelope
        public double RenderedNearMetres, RenderedFarMetres;
        public bool Pushed;
    }

    // Visibility windows are NOT here: Section C's LayerState.Weight(id, log10U, ctx) owns them.

    public static class ScaledSpace
    {
        public const double IdentityRadiusMetres = 10.0;
        public const double RigidEnterMetres = 9.0, RigidLeaveMetres = 11.0;   // H with hysteresis
        public const double NearPlaneMetres = 0.05, FarPlaneMetres = 2000.0, SkyRadiusMetres = 1500.0;
        public const int DepthBits = 24;
        public const double PushMarginQuanta = 2.0, NearPointFloorMetres = 5.0;

        public static double Compress(double metres);          // f
        public static double Decompress(double renderedMetres); // f^-1
        public static double GroupScale(double metres);         // f(d)/d, 1 for d <= d0 or d <= 0
        public static double DepthQuantum(double viewMetres, int bits = DepthBits,
                                          double nearMetres = NearPlaneMetres); // z^2/(n 2^bits)
        /// CPU replica of CS_CompressWS for picking and gates: eye-relative metres in and out.
        public static Double3 CompressRelative(Double3 eyeRelativeMetres);

        /// The cut + modes + push. `previous` may be null (first frame). Returns the number of
        /// unresolved conflicts (push clamped by NearPointFloorMetres); ScaleRenderer prints it.
        public static int Solve(IReadOnlyList<GroupNode> nodes, Double3 eyeKm, double unitsPerKm,
                                GroupPlacement[] previous, GroupPlacement[] result);
    }
}
```

**`Solve`, exactly** (all lengths are rendered metres: `D = |AnchorKm − eyeKm|·upk`,
`R = RadiusKm·upk`, `R_leaf = LeafRadiusKm·upk`):

```
for i in 0..n-1:
    p = nodes[i].Parent
    if p >= 0 and result[p].Mode in {Rigid, Collapsed} and (result[p].Mode == Collapsed or result[p].OwnsSubtree):
        result[i] = {Collapsed, OwnsSubtree=true, RigidOwner=result[p].RigidOwner, Scale=result[p].Scale}; continue
    if MayEnclose:
        s = (D > R && GroupScale(D)*R <= RigidEnter) ? GroupScale(D) : min(1, 0.75*Far/(D+R))
        result[i] = {Enclosing, OwnsSubtree=true, RigidOwner=i, Scale=s}; continue
    wasOwner = previous?[i].Mode == Rigid && previous[i].OwnsSubtree
    wasRigid = previous?[i].Mode == Rigid
    if HasChildren and D > R and GroupScale(D)*R <= (wasOwner ? RigidLeave : RigidEnter):
        result[i] = {Rigid, OwnsSubtree=true, RigidOwner=i, Scale=GroupScale(D)}
    else if D > R_leaf and GroupScale(D)*R_leaf <= (wasRigid ? RigidLeave : RigidEnter):
        result[i] = {Rigid, OwnsSubtree=false, RigidOwner=i, Scale=GroupScale(D)}
    else:
        result[i] = {Envelope, OwnsSubtree=false, RigidOwner=-1, Scale=1}
    rendered interval: Rigid [s(D-Rx), s(D+Rx)] with Rx = R if OwnsSubtree else R_leaf;
                       Envelope [f(D-R_leaf), f(D+R_leaf)]
HasOpaque of an owner = OR over itself and its Collapsed descendants.
push pass (section 1.6) over owners with HasOpaque, Rigid ones only are moved, Envelope ones are fixed obstacles;
a group whose un-pushed near point is already < NearPointFloorMetres is room content: never pushed.
```

**Tests: `tests/CosmoSynth.Core.Tests/ScaledSpaceTests.cs`** (in the harness style of
`CoreSystemsTests.cs`; every tolerance carries its reason in a comment).

| Test | Assertion | Tolerance and why |
|---|---|---|
| identity | `Compress(d) == d` for d ∈ {0.05, 1, 9.999, 10} | exact (same branch) |
| known answers | the §1.3 table, 13 rows | relative 1e-12: `Math.Log` is within 1 ulp (≈1e-16); the margin lets an algebraically equal form such as `log(d) − log(d0)` pass |
| C¹ at d0 | one-sided difference quotients with h = 1e-6·d0 agree with 1 within 1e-6 | truncation error of a one-sided difference is O(h/d0) = 1e-6 |
| monotonic and bounded | f is strictly increasing on 2000 log-spaced points over 1e-3 … 1e30, and f(1e30) + RigidLeave < 0.5·Far | exact comparisons |
| inverse | `Decompress(Compress(d)) == d` on the same grid | relative 1e-12, as above |
| angular-size invariance | for 1000 seeded (D, R) with R < D: `asin(s·R/(s·D)) == asin(R/D)` | 1e-12 rad: exact algebra, with double rounding ≈1e-16 |
| depth quantum | `DepthQuantum(600.462318658) = 0.429823…` (= 600.46²/838860.8) | relative 1e-12 |
| T1 near leaf | D = 1, R_leaf = 0.3 → Rigid, s = 1, interval [0.7, 1.3] | exact |
| T2 far leaf | D = 1e9, R = 1e6 → Rigid, s = 1.9420680744e-7, rendered radius 0.194207 | relative 1e-12 |
| T3 envelope | D = 6.771e6, R_leaf = 6.471e6 (true-scale Earth + atmosphere seen from LEO) → rendered radius 137.86 m > 9, so Envelope, s = 1 | exact mode |
| T4 system cut | planet D = 1e9, R = 4e7, R_leaf = 6.4e6 → Rigid and owning (7.768 m), child Collapsed with the same s. At D = 1e8: 68.47 m → dissolved; leaf 10.956 m > 9 → Envelope | exact mode, s to 1e-12 |
| **T5 the transit trap** | planet (anchor) R = 100 at D = 1000, moon R = 10 at 850 on the same ray, one group (rendered radius 8.968 ≤ 9): the moon's back at s·860 = 48.2045 **<** the planet's front at s·900 = 50.4465. The comment records the per-body counterexample: f(850) = 54.4265 > 50.4465 | 1e-9 |
| T6 push | leaf A (D = 1000, R = 100) → [50.4465, 61.6569]; leaf B (D = 1500, R = 100) → [56.0993, 64.1134]; same ray. After the push, s_A = 0.050992509 and A = [45.8933, 56.0918], so A's far point < B's near point − 2Δz. Angular radii unchanged | 1e-9 |
| T7 floor | A (D = 11, R = 5), B (D = 13, R = 1), same ray: the push would put A's near point at 4.3696 < 5, so it clamps to s_A = 5/6 and the returned conflicts = 1 | exact count, s to 1e-12 |
| T8 enclosing | volume D = 0.7, R = 173, MayEnclose → Enclosing, s = 1 | exact |
| T9 hysteresis | rendered radius 10: previous owning Rigid stays Rigid; previous null → dissolved | exact |

Every one of these passes or fails on the arithmetic alone. That is the "print the numbers, do not
eyeball" rule applied to placement.

### 1.8 Runtime API: the per-frame order, and what every layer calls

**Order** (`[DefaultExecutionOrder]`; the §1.9 sequence of architecture-map changes here):

| Phase | Order | Who | Does |
|---|---|---|---|
| Update | −100 | `XrInput` | reads devices once |
| Update | 0 | movers (`TabletopManipulator`, `XrLocomotion`, travel, dashboard) | write `FloatingOrigin.OriginKm` and `UnitsPerKm` (clamped by C's `ScaleLimits`), and the anchor yaw |
| LateUpdate | −100 | the presentation-plane driver (Section C §2.3) | writes the Universe host's `localRotation` (animation tick, §3). `FloatingOrigin` loses its `LateUpdate` with the dead rebase code |
| LateUpdate | −50 | `UniverseSimulation` | clock → **double state only**: `view.UpdateState(now)` sets `AbsolutePositionKm` and `IsKnown`; **no transform writes** |
| LateUpdate | 50 | `ScaleRenderer` | builds `FrameContext`; LOD per view from true apparent size; builds `GroupNode`s (views plus `CosmoLayer.DeclareGroups`); `ScaledSpace.Solve`; `view.Place(ctx, placement)`; light uniforms per body; shader globals; `layer.Place(ctx)` for every registered layer; `BodyMarkers.Rebuild` |
| LateUpdate | 60 | decorations: `AtmosphereShell`, `RingSystem`, per-orbit ribbons, `SpatialPanel`, the selection rim | read the **placed** transforms. This also fixes today's one-frame `IsGeometryDrawn` lag (architecture-map §1.9) |
| LateUpdate | 100 | `BodyLabelLayer` | labels |

Placement runs in LateUpdate, not `onBeforeRender`. Worldfall ships that way. The residual head-pose
delta between LateUpdate and render is ~2 mm, which becomes ≤ 0.3 px of direction error at 10 m.
Moving placement to `Application.onBeforeRender` is an optimisation to measure, not a requirement
(ordering against the pose driver is INFERRED).

**`Runtime/Universe/FrameContext.cs`**. The struct every placement consumer receives:

```csharp
public readonly struct FrameContext
{
    public readonly Double3 EyeKm;            // centre eye, absolute ICRF km (double)
    public readonly Vector3 EyeWorld;         // centre eye, Unity world (camera transform position)
    public readonly Vector3 EyeUniverse;      // centre eye in the Universe host's local space
    public readonly double UnitsPerKm;        // render metres per km ("U" in Section C)
    public double Log10U => System.Math.Log10(UnitsPerKm);   // the argument of LayerState.Weight
    public readonly Matrix4x4 IcrfToWorld;    // (anchor · Universe host) rotation · P, upper 3x3, det = -1
    public readonly float PixelsPerRadian;    // 0.5 * eye height * P11, centre-eye estimate
    public readonly CosmicTime Now;
    public readonly LayerContext LayerContext; // Section C's engine-free LayerState.Weight context
}
```

**`Runtime/Rendering/Placement.cs`** (a static helper, the only code that turns a scale into
transforms):

```csharp
public static class Placement
{
    /// Rigid similarity about the centre eye, in Universe-local space.
    public static void PlaceRigid(Transform t, Vector3 trueUniverse, double s, in FrameContext ctx)
        => t.localPosition = ctx.EyeUniverse + (float)s * (trueUniverse - ctx.EyeUniverse);
        //   trueUniverse must come from FloatingOrigin.ToRender (double subtraction first).
    /// Eye position expressed in a layer chunk's own units, computed in double then cast.
    public static Vector3 EyeInChunk(in FrameContext ctx, Double3 layerOriginKm, Matrix3d icrfToLayer,
                                     double kmPerUnit, Double3 chunkOriginUnits);
    /// Globals for CosmoCore.hlsl; called once per frame by ScaleRenderer only.
    public static void PublishGlobals(in FrameContext ctx);
}
```

**Shader globals** (set only by `Placement.PublishGlobals`, read by `CosmoCore.hlsl`):
`_CS_CentreEyeWS` (float4), `_CS_Compress` = (d0, 1/d0, SkyRadius, 0), and `_CS_IcrfToWorld`
(float4x4, rotation in the upper 3×3, improper). They are globals, so they do not break SRP
Batcher compatibility.

**`CelestialBodyView` split** (what replaces `UpdateState` placement, `:140-294`):
- `UpdateState(time)`: state only. It sets `AbsolutePositionKm`, velocity and `IsKnown`, and
  collapses on unknown.
- `Place(in FrameContext ctx, in GroupPlacement g, Double3 hostStarKm?)`:
  - Rigid or Collapsed: `Placement.PlaceRigid(transform, origin.ToRender(abs), g.Scale, ctx)`;
    the visual diameter is `g.Scale · S · 2R_display`.
  - Envelope: `localPosition = origin.ToRender(abs)`, s = 1, and the material sets
    `_PerVertexCompress = 1`.
  - `localRotation = origin.MapRotation(BodyFixedToIcrf)`.
  - Floor rule: true apparent diameter < 2.5 px → `DrawnAsPoint`, sphere renderer off, and
    `IsGeometryDrawn` false.
  - Light: `_LightDirWS` from the host star (§7 item 12).

---

## 2. Coordinate frames and units

### 2.1 The single ICRF → Unity mapping (inside `FloatingOrigin` only)

```
universeLocal = P · (absKm − OriginKm) · UnitsPerKm      (metres, Universe host space; FIXED)
absKm         = P · universeLocal / UnitsPerKm + OriginKm   (P is its own inverse)
P = [[1,0,0],[0,0,1],[0,1,0]]    swaps y and z: right-handed ICRF components -> left-handed Unity
world = UniverseAnchor (yaw + translation) · UniverseHost (presentation-plane rotation, C §2.3) · universeLocal
```

- **Up.** With the host at identity, Unity +y is ICRF +z (the celestial pole), +x is the vernal
  equinox, and +z is ICRF +y. The presentation-plane driver rotates the host so that the active
  plane's north pole points to world +y (§3). With the Ecliptic plane that makes the bootstrap
  comment "the ecliptic stays level" (`CosmoSynthBootstrap.cs:348`) true for the first time.
- **Handedness.** det P = −1 is exactly the right-handed to left-handed change of basis, so the
  solar system is no longer mirrored. That is the defect in architecture-map §0 #1: positions today
  go through the identity (`FloatingOrigin.cs:78-86`), which *is* the mirror.
- **Why the plane is not in the mapping.** Putting the plane rotation Q inside the mapping
  (`P·Q·(abs − O)·S`) and putting `P·Q·P` on the host give identical world positions: P is
  symmetric and its own inverse. The host form keeps the mapping fixed, as binding 4 and Section C
  read it. Every universe-local computation (the eye via `InverseTransformPoint`, the hands'
  `ToAbsolute`) then includes the plane with no extra code.
- **Body rotations**: `R_unity = P · M_bodyToIcrf · P`. Its determinant is (−1)(+1)(−1) = +1, a
  proper rotation, so it converts to a Quaternion as `LookRotation(forward = column 2, up = column 1)`.
  Unity local +y is the body pole, matching today's `ToUnityRotation` convention
  (`CelestialBodyView.cs:898-909`) and the sphere's UV pole.
- **Meshes authored in ICRF (or layer-frame) components** (ribbons, volumes) get the improper P as
  a proper rotation times a mirrored scale: `P = (P·D)·D` with D = diag(1, 1, −1). So
  `localRotation = quat(P·D) = Quaternion.Euler(90, 0, 0)` (it maps +y to +z and +z to −y) and
  `localScale = s·k·(1, 1, −1)`. Unity
  flips the cull winding for negative-determinant transforms by itself (INFERRED, standard Unity
  behaviour). Population and sky shaders instead take `_CS_IcrfToWorld` or their per-material
  `_LayerToWorld` directly.

**Core: extend the existing `ScaleSpace.RenderTransform`** (`ScaleSpace.cs:197-225`) with P, rather
than adding a type. Section D P0.3 calls this "`UnityAxes`"; it is this struct:

```csharp
public readonly struct RenderTransform
{
    public Double3 OriginKm { get; }  public double UnitsPerKm { get; }
    public static readonly Matrix3d AxisSwap = new Matrix3d(1,0,0, 0,0,1, 0,1,0);   // P
    public Double3 ToRender(Double3 absoluteKm);        // P·(abs − O)·S
    public Double3 ToAbsolute(Double3 render);          // P·render/S + O
    public Double3 MapDirection(Double3 icrf);          // P·dir
    public Double3 UnmapDirection(Double3 render);      // P·dir
    public Matrix3d MapRotation(Matrix3d bodyToIcrf);   // P·M·P  (proper)
}
```

**`FloatingOrigin` (Runtime) becomes a thin Unity wrapper** and the **only** place Unity vectors
meet ICRF:
- `ToRender(Double3) → Vector3`, `ToAbsolute(Vector3 universeLocal) → Double3`;
- `MapDirection`, `UnmapDirection`, `MapRotation(Matrix3d) → Quaternion`;
- `IcrfToUniverse → Matrix4x4`, `IcrfMeshRotation → Quaternion` (for the D trick);
- `SetOrigin`, `SetUnitsPerKm` (clamped by `ScaleLimits.Clamp`).

The rebase machinery (`:9-16`, `:37-47`, `:59-75`, `:109-160`) is deleted: it is dead (`Focus` is
always null, `CosmoSynthBootstrap.cs:118`). `CelestialBodyView.ToUnityRotation`,
`ToUnityDirection` and `ToIcrfDirection` (`:898-917`) are deleted, and their three callers switch to
`FloatingOrigin` (§7).

**Tests (Core, `FrameTests.cs`, new).**
- `ToAbsolute(ToRender(x)) == x` (relative 1e-15 × |x|, since it is two double rotations);
- `MapRotation(M)` has determinant +1 and `IsRotation(1e-12)`;
- consistency: `MapRotation(M)·(0,1,0) == MapDirection(M·(0,0,1))`, i.e. the body pole lands
  where positions say the pole points. This is the test that makes the old split impossible;
- the Unity-axis known answers in §2.2.

### 2.2 `StandardFrames`: matrices and test vectors

| Rotation (ICRF → frame; rows are the frame's axes in ICRF) | Matrix (VERIFIED, `frames.py`) |
|---|---|
| Ecliptic, IAU 1976 ε = 84381.448″ (existing `EquatorialToEcliptic()` = RotationX(−ε)) | `[1, 0, 0; 0, 0.9174820621, 0.3977771559; 0, −0.3977771559, 0.9174820621]` |
| **Galactic, fixed** | `[−0.0548755604, −0.8734370902, −0.4838350155; 0.4941094279, −0.4448296300, 0.7469822445; −0.8676661490, −0.1980763734, 0.4559837762]` (the Hipparcos matrix; data-beyond-and-cmb §9.1) |
| Galactic → Supergalactic (new `GalacticToSupergalactic`; pole (l, b) = (47.37°, 6.32°), SGL = 0 at (137.37°, 0)) | `[−0.7357425748, 0.6772612964, 0; −0.0745537784, −0.0809914713, 0.9939225904; 0.6731453021, 0.7312711658, 0.1100812622]` |
| Supergalactic (new `EquatorialToSupergalactic` = G2S · I2G) | `[0.3750154778, 0.3413589618, 0.8618801834; −0.8983204556, −0.0957271407, 0.4287851135; 0.2288749668, −0.9350456533, 0.2707505787]` |

**The fix to `StandardFrames.cs:105-108`:**

```csharp
_equatorialToGalactic =
    Matrix3d.RotationZ(+GalacticAscendingNodeLongitudeDeg * MathD.Deg2Rad) *
    Matrix3d.RotationX(-(90.0 - GalacticPoleDecDeg) * MathD.Deg2Rad) *
    Matrix3d.RotationZ(-(90.0 + GalacticPoleRaDeg) * MathD.Deg2Rad);
```

It matches Hipparcos to 4.9e-11; the current signs are off by up to 1.75. **Section B §1.6 asks for
the same test** ("equals the Hipparcos matrix to 1e-9"). That test **fails** on today's code, and
this fix is what makes it pass: whichever lane writes the test lands the fix in the same commit.
`GalacticToSupergalactic` is built from the two defining directions (z = pole, x = SGL 0,
y = z × x). It agrees with Section B's matrix to 3.5e-11. Section C's `PresentationPlane.PlaneUp`
reads these three rotations; this section adds no plane enum of its own.

**Test vectors** (`FrameTests.cs`):

| Input (ICRS RA, Dec or as stated) | Expected | Tolerance and why |
|---|---|---|
| Matrix `EquatorialToGalactic` against the Hipparcos matrix above | every element | 1e-9: 20 × the measured 4.9e-11, which comes from the pole angles being published to 5 decimals |
| NGP (192.85948°, 27.12825°) | galactic (0, 0, 1) | 1e-12, exact by construction |
| galactic (1, 0, 0), inverted | RA 266.404995°, Dec −28.936174° | 1e-6°, implied by the matrix (Hipparcos GC definition) |
| Sirius (101.287155°, −16.716116°) | l = 227.2303°, b = −8.8903° | 1e-4°, 4-decimal rounding of the expectation |
| M31 (10.684708°, 41.268750°) | l = 121.1743°, b = −21.5733°; SGL = 336.1930°, SGB = 12.5521° | 1e-4° |
| M87 (187.70593°, 12.39112°) | SGL = 102.8806°, SGB = −2.3479° | 1e-4° |
| galactic (264.021°, 48.253°) (CMB dipole) | RA 167.9419°, Dec −6.9443° (matches data-beyond-and-cmb §3.5) | 1e-4° |
| SG pole (l, b) = (47.37°, 6.32°) | SG (0, 0, 1); ICRS RA 283.7541°, Dec 15.7089° | 1e-12 / 1e-4° |
| ecliptic pole | ICRF (0, −0.3977771559, 0.9174820621), RA 270°, Dec 66.5607° | 1e-10 |
| **Universe-local axes** (host at identity): `MapDirection` | ICRF +x → (1, 0, 0); ICRF +z → (0, 1, 0); ICRF +y → (0, 0, 1) | exact |
| Levelled to Ecliptic: host rotation = the minimal rotation taking `P·pole` to +y (C §2.3 rule 2) | world direction of the ecliptic pole = (0, 1, 0); the vernal equinox stays (1, 0, 0) when levelling from identity | 1e-6 (float quaternion) |
| Levelled to Galactic, and to Supergalactic | the NGP, respectively the SG pole, → (0, 1, 0) | 1e-6 |

Angles between the plane poles (this sets the size of a levelling animation): ecliptic to galactic
60.19°, galactic to supergalactic 83.68°, ecliptic to supergalactic 51.66°, and equatorial to
ecliptic 23.44°.

### 2.3 Units and the double-precision budget

Absolute positions stay **double km, ICRF, SSB origin** (Core rule unchanged). The spacing of a
double at |x| km is `ulp = 2^(floor(log2 x) − 52)` km:

| Where | |x| (km) | ulp | What it means |
|---|---|---|---|
| Neptune | 4.5e9 | 1e-6 km (1 mm) | every scale exact |
| Voyager 1 | 2.5e10 | 4 mm | exact |
| α Cen | 4.1e13 | 8 m | exact at every hologram scale |
| 1 kpc | 3.1e16 | 4 km | a 0.5 m planet hologram there jitters by 1.6e-4 m, still < 0.5 px |
| Galactic centre | 2.5e17 | 32 km | galaxy-scale content only |
| M31 | 2.4e19 | 4096 km | galaxy-scale only: no planetary content may be placed there |
| CMB | 4.3e23 | 6.7e7 km (0.45 AU) | direction-only or object form at ≥ Mpc scales |

The rule for 2.0: a catalogue body with a surface (Planet, Moon, DwarfPlanet, Asteroid, Comet or
Spacecraft) beyond **3e16 km** of the SSB is a validation warning in `CelestialCatalog.Validate`.
No shipped or planned catalogue does this; fictional systems are tabletop holograms (fictional
report). If it is ever needed, add a frame-relative origin (`OriginFrame` plus offset, using the
existing `ReferenceFrame.StateRelativeTo`). Not before.

Render units are metres in the Universe host space: the anchor is unscaled
(`CosmoSynthBootstrap.cs:96-111`).

### 2.4 How population layers store positions

- **At bake time** (`tools/cosmodata`), each point is stored in the layer's natural unit and frame:
  pc and ICRF for stars; Mpc and ICRF for galaxies; pc and Galactic for the dust volume. The
  file's meta block (Section B §1.2) declares the unit, the frame and the layer origin (SSB, or the
  Sun for Sun-centred cubes).
- **Core `PointChunks.Build(ReadOnlySpan<Double3> positionsUnits, int maxPerChunk = 4096, int maxChunks = 64)`**
  splits the points octree-wise. For each chunk it produces a double `OriginUnits` (the box
  centre), a `RadiusUnits`, and float offsets. Test: max |(double)offset + origin − original|
  ≤ 6e-8 · RadiusUnits for every point.
- **Runtime:** one `MeshRenderer` per chunk, with a per-chunk material instance (SRP Batcher-friendly)
  holding `_EyeInChunk`. Each frame:
  `eyeInChunk = (float3)(icrfToLayer·(ctx.EyeKm − layerOriginKm)/kmPerUnit − chunk.OriginUnits)`,
  with the subtraction done in double before the cast (`Placement.EyeInChunk`).
- **Shader:** `rel = offset − _EyeInChunk`, then `CS_Length`, per-point f, and rotation by
  `_LayerToWorld` (§5.4).
- **Precision guarantee.** The angular error is ≤ 1.2e-7 · chunkRadius / pointDistance (two float
  roundings). That is < 0.5 px (3.5e-4 rad) when the point is farther than 3.4e-4 × the chunk radius
  from the eye, i.e. beyond ~1,800 AU for a 25 pc chunk. Nearer points are faded out with a
  near-fade (`_Look.zw`, in layer units), and the promoted star system (`NearbyStarSystems`, which
  gives double precision) represents them. This is how nearby-star parallax stays exact.
- **Culling.** Chunk renderers get huge bounds, because vertices move in the shader. `Apply` enables
  a chunk only if its angular disc `asin(R/D)` intersects the view cone (O(64) per frame).
- **De-duplication.** Points that a `CelestialBodyView` also draws (the Sun, the exoplanet hosts,
  the active `hyg-N`) carry `data.w = 1` (hidden). The layer rewrites that chunk's `TEXCOORD0`
  stream on `AddBodies` and `RemoveBodies`, which is rare.

### 2.5 Floating-origin behaviour

- There is no rebase. The camera stays in the room, and movers change `OriginKm`, `UnitsPerKm`
  and `Level`. The render mapping subtracts in double before any float cast, so float error cannot
  accumulate (architecture-map §2.3; unchanged).
- Rigid placement stays exact because `Placement.PlaceRigid` receives
  `origin.ToRender(abs)`, computed in double.
- The viewer's absolute position:
  `ctx.EyeKm = origin.ToAbsolute(universe.InverseTransformPoint(camera.position))`, once per frame
  in `ScaleRenderer`. Nothing else recomputes it.
- `UniverseSimulation.NearestBody`, the navigation-speed reference, also asks every
  `CosmoLayer.NearestSurfaceKm` (§4), so speed at Mpc scales is set by the nearest galaxy, not by
  the Sun.
- **Every writer of `UnitsPerKm` must use `SetUnitsPerKm`**, which clamps to Section C's
  `ScaleLimits` [2e-25, 1e3] m/km. At 2e-25 the observable universe is 0.176 m; 1e3 is true
  scale. `TabletopManipulator._minimumUnitsPerKm` and `_maximumUnitsPerKm` (`:45`, `:49`) are
  deleted in favour of `ScaleLimits` (the atlas's own bug B1 was two clamps disagreeing). Every
  number in §1 was checked up to 1e6 m/km, so there is margin.

---

## 3. Room-orientation policy

**What is decided where.** Section C §2.3 decides *which* plane is level and *when* it changes.
The planes are Horizon, LocalVertical, FocusEquator, Equatorial, Ecliptic, Galactic and
Supergalactic. The plane changes only on a rung tap, a GO, or the PLANE control, and a "Level to …"
chip is offered after manual zoom, but the app never levels by itself. This section fixes *how*:
the mathematics, the pivot, the interaction with the manipulator, and the tests. The two agree on
the rule that matters most, which the atlas report found and the atlas itself observed
(milkyway-atlas-port §6 item 1): **the world never rotates with continuous zoom, flight or grabs.**

### 3.1 The mechanism

- The plane is the Universe host's `localRotation` (§2.1). The anchor above it keeps the yaw and
  the room position. Nothing else writes the host rotation.
- **Levelling to plane X** first computes the target up in universe-local coordinates,
  `u = P·PlaneUp(X)`, where `PlaneUp` comes from Section C and is built on §2.2's matrices. The new
  host rotation is `R1 = FromToRotation(R0·u, +y) · R0`: the minimal rotation, C's rule 2. C then
  adds a yaw on the anchor that keeps the travel target in front of the user.
- **Animation.** Over T seconds (the travel duration, or 1.2 s for a rung tap):
  `R(t) = Slerp(R0, R1, e(t/T))`, with smootherstep `e(u) = u³(6u² − 15u + 10)`, which gives zero
  angular velocity at both ends. In Space mode, a tilt larger than 10° blinks instead (C's rule 2).
  The largest tilts are the pole-to-pole angles in §2.2 (84° galactic to supergalactic).
- **Pivot = the Universe host's origin**, which is where `OriginKm` maps: the anchor point. After a
  GO, `OriginKm` is the target, so the thing being looked at stays put while the sky turns around
  it. `FloatingOrigin` needs no change for this, because the rotation is a transform, not a mapping.
- **Time-dependent planes** (Horizon, LocalVertical, FocusEquator) are re-evaluated every frame by
  the same driver. Their `PlaneUp` rotates with the body, so `R` follows. That is correct, and
  cheap: one `FromToRotation` per frame.
- **Tests.** Core: C's `PresentationPlane` tests plus §2.2's pole vectors. Play mode: P12 (§8.3).
  Neither needs a headset.

### 3.2 Interaction with the tabletop manipulator

- The manipulator touches O (drag), S (two-hand scale about a pivot) and the anchor's yaw (twist
  and snap-turn). It **never** touches the host rotation and **never** requests levelling.
  Levelling after a manual zoom is C's chip, a user tap.
- **A grab during a levelling animation stops the animation where it is.** The host keeps a valid
  rotation that is not level to any named plane. C's chip then offers "Level to …". Travel is
  cancelled by a grab anyway (atlas §4.16, adopted by C).
- **The local-space fix (B4, the architecture-map defect).** Hand poses arrive in world space from
  `XrHandInput.cs:282-288`. Convert them **once**, at the top of `TabletopManipulator.UpdateHands`:
  `pose.Position = _simulation.transform.InverseTransformPoint(pose.Position)` (rotation likewise).
  `_simulation.transform` *is* the Universe host, so this crosses both the anchor and the plane
  rotation. From then on every `origin.ToAbsolute` call at `:131`, `:138`, `:174` and `:191`
  receives universe-local metres, and grabs stay correct in any plane.
- **The sticky-target fix (B5).** `_targetUnitsPerKm` (`:79`) is cleared to 0 when
  |log10(current) − log10(target)| < 1e-3 (reached), and also 0.5 s after the last hand releases.
  `ApplySmoothedScale` (`:206-229`) then leaves the scale alone. Rungs and travel set the scale
  through `FloatingOrigin.SetUnitsPerKm` or `FrameBodyAtSize` only.

---

## 4. The layer framework

The minimum that the population layers share: one abstract MonoBehaviour and one static registry.
There is no interface hierarchy, no factory and no per-layer config asset. Two neighbouring
contracts are **reused, not duplicated**:

- **Which layers exist**, their ids, captions, windows, defaults, SOLO groups and params:
  Section C's `LayerTable`, and **how visible they are**: `LayerState.Weight(id, log10U, ctx)`
  (C §4.1).
- **Where their data comes from**: Section B's `DataLoader` walks `manifest.json` and raises
  `LayerReady(kind, parsed)` with the parsed Core object (B §3). Licence, attribution and
  "not in this build" come from the manifest.

The base class adds what only rendering knows: placement, budgets, groups, destinations and picks.
Bodies are **not** a layer. `UniverseSimulation` and `CelestialBodyView` stay what they are, and
only gain `Place(...)` (§1.8). C's `bodies`, `orbits`, `spacecraft`, `atmospheres` and `rings` rows
gate those components through the same `Weight`.

### 4.1 `Runtime/Universe/CosmoLayer.cs` (verbatim contract)

```csharp
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// A population or effect that is not a catalogue body. One subclass per file, added by the
    /// bootstrap's BuildLayers() slot (Section D §D2.2), backed by an engine-free Core parser.
    public abstract class CosmoLayer : MonoBehaviour
    {
        public abstract string LayerId { get; }                  // exactly a Section C LayerTable id
        public abstract IReadOnlyList<string> DataKinds { get; } // manifest kinds it consumes (= LayerDef.DataKinds)
        public abstract LayerBudget Budget { get; }              // ceiling at Weight = 1; asserted by the play-mode gate

        public float Weight { get; private set; }               // LayerState.Weight this frame; 0 = not drawn, not pickable
        protected UniverseSimulation Simulation { get; private set; }
        protected LayerState State { get; private set; }

        public void Bind(UniverseSimulation simulation, LayerState state)
        {
            Simulation = simulation;
            State = state;
        }

        /// Called by DataLoader.LayerReady for each kind in DataKinds (Section B section 3). Build
        /// meshes here; heavy parsing already happened in Core.
        public abstract void OnData(string kind, object parsed);

        protected virtual void OnEnable() => LayerRegistry.Register(this);
        protected virtual void OnDisable() => LayerRegistry.Unregister(this);

        /// Called once per frame by ScaleRenderer (LateUpdate order 50) after groups are solved.
        public void Place(in FrameContext ctx)
        {
            Weight = (float)State.Weight(LayerId, ctx.Log10U, ctx.LayerContext);   // C's engine-free context
            SetRenderersEnabled(Weight > 0f);
            if (Weight > 0f) Apply(ctx);                          // multiply material _Fade by Weight
        }

        protected abstract void SetRenderersEnabled(bool on);
        protected abstract void Apply(in FrameContext ctx);       // uniforms, chunk culling, rigid parts

        /// Rigid or enclosing parts (a volume, the CMB object form, an exhibit). Append nodes;
        /// return how many. ScaleRenderer hands each back through AcceptGroup(localIndex, ...).
        public virtual int DeclareGroups(List<GroupNode> into) => 0;
        public virtual void AcceptGroup(int localIndex, in GroupPlacement placement, in FrameContext ctx) { }

        public virtual void CollectDestinations(List<Destination> into) { }
        /// Pick in RENDERED space (the hand ray meets what is drawn); report TRUE positions.
        public virtual bool TryPick(Ray worldRay, in FrameContext ctx, out PickHit hit) { hit = default; return false; }
        /// Feeds UniverseSimulation.NearestBody so flight speed follows the nearest thing of any kind.
        public virtual double NearestSurfaceKm(Double3 absoluteKm) => double.PositiveInfinity;
    }

    public struct PickHit
    {
        public string LayerId, Id, Label;
        public Double3 PositionKm;      // true, absolute: measure tools use this, never render positions
        public double TrueDistanceKm;   // from the eye
        public float AngleRad;          // off-ray angle, for choosing among layers and bodies
    }

    /// Non-MonoBehaviour helper; sharing the file is legal under the one-MonoBehaviour-per-file rule.
    public static class LayerRegistry
    {
        private static readonly List<CosmoLayer> Layers = new List<CosmoLayer>();
        public static IReadOnlyList<CosmoLayer> All => Layers;
        public static void Register(CosmoLayer layer) { if (!Layers.Contains(layer)) Layers.Add(layer); }
        public static void Unregister(CosmoLayer layer) => Layers.Remove(layer);
        public static CosmoLayer Find(string id)
        {
            foreach (CosmoLayer layer in Layers) if (layer.LayerId == id) return layer;
            return null;
        }
    }
}
```

`LayerState.Weight` lives in Core, so its context must be engine-free. Section C defines that
type (called `LayerContext` here). `ScaleRenderer` builds it once per frame (data-ready kinds from
`DataLoader`, plus the viewer state C's extra conditions read) and carries it in
`FrameContext.LayerContext`. There is no second context type.

**Core records**: a new file, `Core/Catalog/LayerRecords.cs`, engine-free:

```csharp
public readonly struct Destination
{
    public readonly string Id, Label, LayerId;          // LayerId null for catalogue bodies
    public readonly Double3 PositionKm;                  // absolute ICRF
    public readonly double FramingRadiusKm;              // what should fill ~0.7 m of room on arrival
    public readonly ProvenanceClass PositionClass;
    public Destination(string id, string label, string layerId, Double3 positionKm,
                       double framingRadiusKm, ProvenanceClass positionClass);
}

public readonly struct LayerBudget
{
    public readonly int Draws, Vertices;
    public readonly float FragmentScreens, StepCoverage, TextureMB;
    public LayerBudget(int draws, int vertices, float fragmentScreens, float stepCoverage, float textureMB);
}
```

Tests: none are needed beyond construction. They are plain records: YAGNI.

### 4.2 Lifecycle, visibility, provenance, budgets, LOD

1. **Create.** The bootstrap's `BuildLayers()` slot, one line per layer (Section D §D2.2):
   `AddLayer<StarLayer>()`, which does `AddComponent` under the Universe host and then `Bind`.
   Layers never talk to each other.
2. **Data.** `DataLoader` walks the manifest in order and calls `OnData` for every layer whose
   `DataKinds` contains the kind. A kind absent from the build is never delivered, `Weight` stays 0,
   and the panel says "not in this build". Nothing throws, and dropping a source changes no code
   (binding 7).
3. **Every frame.** `ScaleRenderer` calls `DeclareGroups`, then `ScaledSpace.Solve`, then
   `AcceptGroup`, then `Place(ctx)`.
4. **Catalogue change.** `UniverseSimulation.BodiesChanged` is a new event, raised by `AddBodies`
   and `RemoveBodies` and **not** by `SetCatalog`. It lets layers re-flag duplicated points (§2.4).
5. **Teardown.** `OnDisable` unregisters; `OnDestroy` frees meshes and materials (the
   `DestroyMaterials` pattern of `CelestialBodyView.cs:930-943`).

- **Visibility.** `Weight` multiplies the layer's material `_Fade`. Layers never test scale, SOLO or
  data availability themselves: C's `Weight` already does all three.
- **Provenance and licence.** C's `LayerDef.Weakest` and `ProvenanceNote`, plus the manifest's
  attribution strings (B §3), feed the panel and the credits. Per-object provenance stays in the
  Core types (B's meta blocks).
- **Budgets.** Each layer declares its ceiling. The play-mode gate asserts that the sum over layers
  with `Weight > 0`, plus bodies, stays within the frame budget (§6) for every fixed capture view.
- **LOD** is internal to each layer: magnitude bands, chunk cone culling, sprite versus volume. The
  framework's only rule is to never exceed `Budget` at `Weight = 1`.

### 4.3 Groups, destinations, picks

- **Groups.** A layer declares its rigid parts through `DeclareGroups` and places them in
  `AcceptGroup` with `Placement.PlaceRigid`. Examples: the dust volume (`MayEnclose = true`), the
  CMB object form (`MayEnclose = true`), the heliosphere on the Sun, a nebula close-up box, an
  exhibit. Point content is **not** declared: it is per-point.
- **Destinations.** The travel list is built from two sources. `UniverseSimulation` contributes
  bodies: Star, Planet, DwarfPlanet and the active fictional systems, with `FramingRadiusKm` =
  equatorial radius × 1.3, or the system extent for a star. Every layer then adds its own through
  `CollectDestinations`: named galaxies, the galactic centre, Laniakea, the CMB and so on (C's GO
  targets). Arrival is framed by `FramingRadiusKm` and never raises `CatalogLoaded`. The target's
  plane is C's rule 3.
- **Picks.**
  - `BodySelector` keeps its ray–sphere test against the *placed* transforms (`RenderedRadius` and
    the drawn position: §1.6 makes that exact for rigid groups). It also asks every layer with
    `Weight > 0` for `TryPick`, and takes the hit with the smallest angle, with 1.6° of forgiveness
    (`BodySelector` today).
  - Layers compute candidates' rendered positions with the CPU replica
    `ScaledSpace.CompressRelative` in double. The scan is O(N) per click; hover scans at most 5,000
    of the brightest points at 10 Hz.
  - Measure and distance readouts use `PickHit.PositionKm` (true, double), never render positions
    (architecture-map §5.10).

### 4.4 Placement mode and queue for Section C's layer ids

Section C §4.2 is the list, with ids, windows and defaults. This table adds only what rendering
decides. The rows are grouped; every C id belongs to exactly one row.

| C ids | Placement (§1.2) | Queue | Shader |
|---|---|---|---|
| `bodies`, `spacecraft`, `sites`, `exohosts` (hosts once expanded into bodies), `worlds` | bodies: rigid, or Envelope when the eye is near; floored bodies go to `BodyMarkers` | 2000 / 3050 | Body / Point (`_Space = 1`) |
| `atmospheres`, `clouds`, `winds`, `sunActivity`, `comets` | decorations of a body: they follow its placement and check `IsGeometryDrawn` | 3100 | Atmosphere, or small ports of it |
| `rings` | decoration: rigid with the body | 3000 | Ring |
| `orbits`, `trails` | ribbons owned by the parent's group; per-vertex when the owner contains the eye | 3060 | Ribbon |
| `stars`, `census`, `cepheids`, `globulars`, `masers`, `belts`, `density`, `quasars`, `galaxies2mrs`, `groupsCf4`, `sdss`, `cloudMarkers` | per-point chunks | 3050 | Point (`_Space = 0`) |
| `galaxiesLocal` | per-point, plus oriented discs above 3 px (the discs are a rigid quad per galaxy, placed per-point at its centre) | 3050 | Point + a galaxy-disc port |
| `stars`, the distance-unknown subset; `mwBand` | sky (per eye, at infinity) | 1010 | Point (`_Space = 2`) or Sky |
| `cmb` | sky form: sky; object form: rigid layer group (`MayEnclose`) | 1000 / 3010 | Sky |
| `dust`, `nebulae` | Enclosing volume groups (+ a voxel-sprite LOD for `dust`) | 3010 | Volume |
| `dustExtent`, `arms`, `sunOrbit`, `scaleRings`, `eclipticGrid`, `planeGrid`, `cylGrids`, `sgGrid`, `constellations`, `basins`, `parker` | ribbons: rigid when their centre group is rigid, per-vertex otherwise | 3060 | Ribbon |
| `heliosphere`, `oort`, `mwDisc` | a rigid layer group anchored on its centre (the Sun or the GC); per-vertex shell while the eye is inside | 3100 / 3010 | Atmosphere-like shell / Volume |
| `horizonGrid` | sky | 1010 | Ribbon, `_Placement = 2` (vertices are ICRF directions, placed per eye at SkyRadius) |
| `flare` | modulates `stars` (a per-point flag) | — | Point |

If C adds an id, the owning lane adds a row here in its report, and the integrator merges it.

---

## 5. Shaders: verbatim URP HLSL skeletons

**Status: PROPOSED; none of them has been compiled.** They follow the URP pattern of the
headset-proven `GeoramaXR/.../GeoStar.shader` and `Worldfall/docs/CONTRACT.md` §4 (both quoted in
quest-rendering-techniques §11.2) and the macro semantics of §11.3 there. The first job of the
shader lane is to compile each one in the one editor, check the stereo macros with the gate, and
capture a frame.

### 5.1 Rules for every CosmoSynth shader

1. `Shader "CosmoSynth/<Name>"`, `"RenderPipeline"="UniversalPipeline"`, one `Pass` with
   `"LightMode"="UniversalForward"`, `HLSLPROGRAM` only (no CG). **Single variant**: the only pragma
   is `#pragma multi_compile_instancing`. No `shader_feature` and no custom keywords; switches are
   uniform branches. XR stereo variants are added by Unity itself, as in every sibling project.
2. The stereo skeleton, every macro, every time:
   - `UNITY_VERTEX_INPUT_INSTANCE_ID` in `Attributes`;
   - `UNITY_VERTEX_OUTPUT_STEREO` in `Varyings`;
   - `UNITY_SETUP_INSTANCE_ID(v)` and `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)` first in the vertex
     function;
   - `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)` first in the fragment function. Without it, the
     fragment sees the left eye's camera position in both eyes under multiview
     (quest-rendering-techniques §11.3).
3. **Alpha.** Additive passes are `Blend One One, Zero One` and return alpha 0. Premultiplied
   "over" passes are `Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha` and write coverage =
   opacity; they are for physical absorbers and UI only. Opaque passes return alpha 1.
   `Blend SrcAlpha One` (GeoStar's) is forbidden: it adds alpha into passthrough.
4. `ZTest LEqual` everywhere. `ZTest Always` needs a `// ztest-always: <reason>` comment (gated).
5. All material properties go in `CBUFFER_START(UnityPerMaterial)`, so the SRP Batcher works.
   Per-renderer values use **material instances**, not MaterialPropertyBlocks.
6. **Colour.** The project is Linear. Vertex colours and hand-tuned constants are
   **display-referred (sRGB)** and are decoded exactly once with `CS_DisplayToLinear`. Material
   colours set with `SetColor` are linearised by Unity itself (INFERRED, standard Linear-mode
   behaviour for `Color` properties).
7. Sizes on screen are **angular (dmm = mrad)**, converted with the eye's own projection
   (`CS_AngularToClip`), and bounded in pixels where a floor matters.

### 5.2 `Runtime/Shaders/CosmoCore.hlsl` (the shared include)

```hlsl
#ifndef COSMOSYNTH_CORE_INCLUDED
#define COSMOSYNTH_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

// Per-frame globals. Written ONLY by Placement.PublishGlobals (ScaleRenderer, LateUpdate order 50).
float4   _CS_CentreEyeWS;   // xyz: centre eye = the one compression centre for both eyes
float4   _CS_Compress;      // x: d0 identity radius (m), y: 1/d0, z: sky radius (m), w: unused
float4x4 _CS_IcrfToWorld;   // upper 3x3: ICRF components -> world (anchor * P * Q); det = -1

// |v| without overflowing float: render distances reach 4.4e29 m, and x*x overflows above ~1.8e19.
float CS_Length(float3 v)
{
    float m = max(max(abs(v.x), abs(v.y)), abs(v.z));
    return m > 0.0 ? m * length(v / m) : 0.0;
}

// f(d): identity inside d0, logarithmic beyond. Must equal Core ScaledSpace.Compress.
float CS_CompressDistance(float d)
{
    return d <= _CS_Compress.x ? d : _CS_Compress.x * (1.0 + log(d * _CS_Compress.y));
}

// Per-vertex radial compression about the centre eye: direction exact, distance f(d).
float3 CS_CompressWS(float3 positionWS)
{
    float3 rel = positionWS - _CS_CentreEyeWS.xyz;
    float d = CS_Length(rel);
    return d > 0.0 ? _CS_CentreEyeWS.xyz + rel * (CS_CompressDistance(d) / d) : positionWS;
}

float3 CS_IcrfDirToWorld(float3 dirIcrf) { return mul((float3x3)_CS_IcrfToWorld, dirIcrf); }

// Vertex colours and tuned constants are display-referred (sRGB); decode once, here.
half3 CS_DisplayToLinear(half3 c) { return SRGBToLinear(c); }

// Pixels per radian for THIS eye near the view centre (P11 = cot(fovY/2)).
float CS_PixelsPerRadian() { return 0.5 * _ScreenParams.y * UNITY_MATRIX_P._m11; }

// Angular half-extent (radians, x and y) to a clip-space offset at clip w, for this eye.
float2 CS_AngularToClip(float2 radians, float w)
{
    return radians * float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11) * w;
}

// Interleaved gradient noise (Jimenez 2014): per-pixel jitter for ray marches.
float CS_IGN(float2 pixel) { return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))); }

#endif
```

### 5.3 `CosmoSynth/Body`: opaque lit or unlit body (planets, moons, stars, spacecraft)

```hlsl
Shader "CosmoSynth/Body"
{
    Properties
    {
        _BaseColor ("Base colour", Color) = (1,1,1,1)
        _BaseMap ("Albedo (sRGB)", 2D) = "white" {}
        _NormalMap ("Normal (linear RGB, created with linear:true)", 2D) = "bump" {}
        _SurfaceMap ("Surface (linear; a = smoothness)", 2D) = "black" {}
        _LightDirWS ("Direction to host star, world", Vector) = (0,1,0,0)
        _LightColor ("Star light (linear)", Color) = (1,1,1,1)
        _Ambient ("Ambient fill (from CosmoTheme)", Color) = (0.02,0.02,0.03,1)
        _Emission ("Emission, x albedo (stars, spacecraft)", Color) = (0,0,0,1)
        _Smoothness ("Smoothness scale", Range(0,1)) = 0
        _SpecGain ("Specular gain", Range(0,2)) = 0.6
        _Terminator ("Terminator softness", Range(0,0.3)) = 0.05
        _PerVertexCompress ("Envelope mode (section 1.6)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Body"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
            TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                float4 _LightDirWS;
                half4  _LightColor;
                half4  _Ambient;
                half4  _Emission;
                half   _Smoothness;
                half   _SpecGain;
                half   _Terminator;
                float  _PerVertexCompress;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half4  tangentWS  : TEXCOORD2;   // w: bitangent sign
                float2 uv         : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(v.positionOS);
                // Rigid mode: the transform already holds the group similarity. Envelope mode:
                // the eye is near a large body, so compress each vertex about the centre eye.
                if (_PerVertexCompress > 0.5) positionWS = CS_CompressWS(positionWS);

                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS  = half4(TransformObjectToWorldDir(v.tangentOS.xyz),
                                     v.tangentOS.w * GetOddNegativeScale());
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);   // per-eye camera position below

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 nTS = UnpackNormalRGBNoScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                half3 n = normalize(i.normalWS);
                half3 t = normalize(i.tangentWS.xyz);
                half3 b = cross(n, t) * i.tangentWS.w;
                half3 N = normalize(nTS.x * t + nTS.y * b + nTS.z * n);

                // Lit by the HOST STAR (found through the parent chain on the CPU), not by a Unity
                // light: no per-pixel light slots, no RootIds[0], correct in every group.
                half3 L = normalize(_LightDirWS.xyz);
                half  ndl = dot(N, L);
                half  diffuse = saturate((ndl + _Terminator) / (1.0h + _Terminator));

                half3 V = normalize(GetCameraPositionWS() - i.positionWS);   // this eye
                half3 H = normalize(L + V);
                half  smooth = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, i.uv).a * _Smoothness;
                half  spec = pow(saturate(dot(N, H)), exp2(1.0h + 10.0h * smooth))
                           * smooth * _SpecGain * step(0.0h, ndl);

                half3 colour = albedo * (_LightColor.rgb * diffuse + _Ambient.rgb)
                             + _LightColor.rgb * spec
                             + albedo * _Emission.rgb;
                return half4(colour, 1.0h);   // alpha 1: an opaque body covers passthrough, correctly
            }
            ENDHLSL
        }
    }
    Fallback Off
}
```

### 5.4 `CosmoSynth/Point`: additive point-sprite billboard (stars, galaxies, voxels, body markers)

Vertex layout, the same for all four corners of a point: `POSITION` float3; `COLOR` UNorm8x4
(display colour); `TEXCOORD0` float4 (x magnitude, y corner 0..3, z shape, w hidden flag). That is
32 B per vertex; a half-precision TEXCOORD0 would cut it to 24 B if the budget ever needs it.

- Shapes are 0 core, 1 halo, 2 spike-x, 3 spike-y. The extra quads exist only for the ~50 brightest
  stars, baked by the layer. This is today's `SkyAndRings.EmitStar` design (`:198-279`) moved into
  one shader.
- The shape extents and gains come from today's `HaloScale`, `HaloBrightness`, `SpikeLength`,
  `SpikeWidth` and `SpikeBrightness` constants. The stars lane carries them over and re-measures.

```hlsl
Shader "CosmoSynth/Point"
{
    Properties
    {
        _EyeInChunk ("Layer mode: eye in this chunk's units", Vector) = (0,0,0,0)
        _Units      ("x: metres per unit, y: parsecs per unit", Vector) = (1,1,0,0)
        _MagWindow  ("x: magnitude at brightness 0, y: at brightness 1 (StarLook: 7, 1.5)", Vector) = (7,1.5,0,0)
        _SizeDmm    ("x: faint width, y: bright width (StarLook: 3.8, 9.0)", Vector) = (3.8,9,0,0)
        _PxBounds   ("x: min px, y: max px", Vector) = (3,64,0,0)
        _Shape      ("x: halo scale, y: spike length, z: spike width (x core width)", Vector) = (2.8,7,0.35,0)
        _ShapeGain  ("x: halo gain, y: spike gain (bright end only)", Vector) = (0.35,0.25,0,0)
        _Look       ("x: faintest brightness 0.40, y: exposure, z/w: near-fade start/end (layer units)", Vector) = (0.4,1,0,0)
        _Fade       ("Layer fade", Float) = 1
        _Space      ("0 layer (per-point compression), 1 object (CPU-placed), 2 sky (direction, per eye)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Points"
            Tags { "LightMode"="UniversalForward" }
            Blend One One, Zero One     // additive colour; destination alpha (passthrough coverage) untouched
            ZWrite Off
            ZTest LEqual                // hidden behind opaque bodies, which draw first
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EyeInChunk;
                float4 _Units;
                float4 _MagWindow;
                float4 _SizeDmm;
                float4 _PxBounds;
                float4 _Shape;
                float4 _ShapeGain;
                float4 _Look;
                float  _Fade;
                float  _Space;
                float4x4 _LayerToWorld;   // layer frame -> world rotation (may be improper); set by the layer
            CBUFFER_END

            struct Attributes
            {
                float3 offset : POSITION;   // layer: offset from chunk origin; object: object space; sky: direction
                half4  color  : COLOR;      // display (sRGB) colour; a unused
                float4 data   : TEXCOORD0;  // x magnitude (absolute in layer mode, apparent otherwise),
                                            // y corner 0..3, z shape 0 core/1 halo/2 spike-x/3 spike-y, w hidden
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                if (v.data.w > 0.5) { o.positionCS = float4(2, 2, 2, 1); return o; }   // de-duplicated: culled

                float3 centreWS;
                float  magnitude = v.data.x;
                float  nearFade = 1.0;
                if (_Space < 0.5)
                {
                    // Chunk-relative, eye-relative, precise near the chunk (section 2.4), then f(d).
                    float3 rel = v.offset - _EyeInChunk.xyz;
                    float  du  = max(CS_Length(rel), 1e-30);
                    float3 dirWS = normalize(mul((float3x3)_LayerToWorld, rel / du));
                    centreWS  = _CS_CentreEyeWS.xyz + dirWS * CS_CompressDistance(du * _Units.x);
                    magnitude = v.data.x + 5.0 * log10(max(du * _Units.y, 1e-12)) - 5.0;   // M -> m
                    nearFade  = smoothstep(_Look.z, _Look.w, du);
                }
                else if (_Space < 1.5)
                {
                    centreWS = TransformObjectToWorld(v.offset);          // placed by the CPU
                }
                else
                {
                    float3 dirWS = normalize(mul((float3x3)_LayerToWorld, v.offset));
                    centreWS = GetCameraPositionWS() + dirWS * _CS_Compress.z;   // this eye: infinity
                }

                // Magnitude -> brightness and size, in one law (StarLook): brighter is bigger AND
                // brighter, with a size floor so the profile has pixels to resolve in.
                float b = saturate((_MagWindow.x - magnitude) / (_MagWindow.x - _MagWindow.y));
                uint  shape = (uint)v.data.z;
                float2 extent = shape == 1u ? _Shape.xx
                              : shape == 2u ? float2(_Shape.y, _Shape.z)
                              : shape == 3u ? float2(_Shape.z, _Shape.y) : float2(1.0, 1.0);
                float gain = shape == 0u ? 1.0 : (shape == 1u ? _ShapeGain.x : _ShapeGain.y) * saturate(2.0 * b - 1.0);

                float ppr = CS_PixelsPerRadian();
                float widthRad = clamp(lerp(_SizeDmm.x, _SizeDmm.y, b) * 1e-3, _PxBounds.x / ppr, _PxBounds.y / ppr);

                float4 cs = TransformWorldToHClip(centreWS);
                uint ci = (uint)v.data.y;
                float2 corner = float2((ci & 1u) ? 1.0 : -1.0, (ci & 2u) ? 1.0 : -1.0);
                cs.xy += corner * CS_AngularToClip(0.5 * widthRad * extent, cs.w);
                o.positionCS = cs;
                o.uv = corner;

                half3 tinted = lerp(half3(1, 1, 1), v.color.rgb, 0.30h + 0.70h * (half)b);   // faint stars desaturate
                half  level  = (half)(_Look.x + b * (1.0 - _Look.x));
                o.color = CS_DisplayToLinear(tinted * level) * (half)(gain * _Look.y * _Fade * nearFade);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half a = exp2(-4.0h * (half)dot(i.uv, i.uv));   // Gaussian: 1 at the centre, 1/16 at the edge
                return half4(i.color * a, 0.0h);                // additive; alpha 0
            }
            ENDHLSL
        }
    }
    Fallback Off
}
```

### 5.5 `CosmoSynth/Ribbon`: screen-constant-width line with analytic anti-aliasing (orbits, constellations, grids)

The mesh has 4 vertices per segment. Each vertex carries its own endpoint (`POSITION`), the
segment's other endpoint (`TEXCOORD0`), and the side and order (`TEXCOORD1`). One draw covers all
the segments of one owner. The tail gradient rides in `COLOR.a`, which today's
`OrbitLineRenderer` puts in the gradient (`:216-276`). Its legibility cull ("only from outside,
only if bigger than a dot") stays on the CPU.

```hlsl
Shader "CosmoSynth/Ribbon"
{
    Properties
    {
        _WidthDmm ("Line width, dmm (mrad); 1.4 dmm ~ 2 px on Quest 3", Float) = 1.4
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Float) = 1
        _Placement ("0 rigid (CPU transform), 1 per-vertex (owner contains the eye), 2 sky (ICRF directions, per eye)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+60" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Ribbon"
            Tags { "LightMode"="UniversalForward" }
            Blend One One, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _WidthDmm;
                float _Intensity;
                float _Fade;
                float _Placement;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;    // this vertex's endpoint
                float3 otherOS    : TEXCOORD0;   // the segment's other endpoint
                float2 sideOrder  : TEXCOORD1;   // x: side -1/+1; y: +1 if other is the next point, -1 if previous
                half4  color      : COLOR;       // display colour; a = tail gradient
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3  color      : COLOR;
                noperspective float2 aa : TEXCOORD0;   // x: signed px from the centre line; y: half width px
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 PlaceWS(float3 positionOS)
            {
                if (_Placement > 1.5)   // sky: positionOS is an ICRF direction; this eye, at infinity
                    return GetCameraPositionWS() + normalize(CS_IcrfDirToWorld(positionOS)) * _CS_Compress.z;
                float3 p = TransformObjectToWorld(positionOS);
                return _Placement > 0.5 ? CS_CompressWS(p) : p;
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 selfVS  = TransformWorldToView(PlaceWS(v.positionOS));
                float3 otherVS = TransformWorldToView(PlaceWS(v.otherOS));

                // Clip the segment against a plane just beyond the near plane (view space looks down -z).
                const float nearVS = 0.06;
                bool selfBehind  = -selfVS.z  < nearVS;
                bool otherBehind = -otherVS.z < nearVS;
                if (selfBehind && otherBehind) { o.positionCS = float4(2, 2, 2, 1); return o; }
                if (selfBehind)  selfVS  = lerp(selfVS, otherVS, (-nearVS - selfVS.z) / (otherVS.z - selfVS.z));
                if (otherBehind) otherVS = lerp(otherVS, selfVS, (-nearVS - otherVS.z) / (selfVS.z - otherVS.z));

                float4 selfCS  = TransformWViewToHClip(selfVS);
                float4 otherCS = TransformWViewToHClip(otherVS);
                float2 halfScreen = 0.5 * _ScreenParams.xy;
                float2 d = (otherCS.xy / otherCS.w - selfCS.xy / selfCS.w) * halfScreen * v.sideOrder.y;
                float2 dir = dot(d, d) > 1e-8 ? normalize(d) : float2(1.0, 0.0);
                float2 nrm = float2(-dir.y, dir.x);

                float halfPx = 0.5 * _WidthDmm * 1e-3 * CS_PixelsPerRadian();   // angular width, this eye
                float drawnHalf = max(halfPx, 0.5) + 1.0;                        // +1 px for the analytic edge
                selfCS.xy += nrm * (v.sideOrder.x * drawnHalf) / halfScreen * selfCS.w;

                o.positionCS = selfCS;
                o.aa = float2(v.sideOrder.x * drawnHalf, halfPx);
                o.color = CS_DisplayToLinear(v.color.rgb) * (half)(v.color.a * _Intensity * _Fade);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float halfPx = i.aa.y;
                float coverage = saturate(max(halfPx, 0.5) + 0.5 - abs(i.aa.x));   // analytic AA, no MSAA needed
                coverage *= saturate(halfPx * 2.0);   // thinner than a pixel: fades instead of breaking up
                return half4(i.color * (half)coverage, 0.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
```

### 5.6 `CosmoSynth/Sky`: inside-out sky sphere at infinity (CMB, star shell)

The mesh is a unit sphere of about 2k vertices whose object-space vertices *are* ICRF directions.
It uses the identity transform and huge bounds. The data is Section B's CMB container
(`cmb/cmb_sky.cscb`, B §2.13):
- a **cube map, 6 × 1024², R8**, face selection in **ICRF components**;
- linear encoding: 3.92157 µK per step, offset −500 µK;
- an inpainting-mask cube of 6 × 256².

It is uploaded as a linear (non-sRGB) `Cubemap` and sampled with the **ICRF** direction as the
lookup vector, never a Unity axis. So the mapping stays in `FloatingOrigin` for placement, and the
texture lookup needs no mapping at all. B's harness check (GPU samples at the six face centres and
the Cold Spot, compared with `CmbSky.Sample`) proves the face and row order. The LUT is an sRGB
256×1 texture.

```hlsl
Shader "CosmoSynth/Sky"
{
    Properties
    {
        _SkyCube ("CMB cube map, faces in ICRF components (linear R8)", Cube) = "grey" {}
        _Lut ("Colour LUT 256x1 (sRGB)", 2D) = "white" {}
        _Contrast ("Contrast about 0 uK (1 = +-500 uK full LUT)", Float) = 2
        _Immersion ("0 = passthrough untouched, 1 = replaces the room (VR / full-space)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero   // first draw of the frame; replaces the (0,0,0,0) clear with a premultiplied colour
            ZWrite Off
            ZTest LEqual
            Cull Off         // inside a closed sphere every view ray meets the surface exactly once

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            TEXTURECUBE(_SkyCube); SAMPLER(sampler_SkyCube);
            TEXTURE2D(_Lut);       SAMPLER(sampler_Lut);

            CBUFFER_START(UnityPerMaterial)
                float _Contrast;
                float _Immersion;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;   // ICRF direction (unit sphere)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirIcrf    : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 dirIcrf = normalize(v.positionOS);
                float3 dirWS = normalize(CS_IcrfDirToWorld(dirIcrf));
                // Anchored to THIS eye (per eye in the vertex stage under multiview): zero disparity
                // and zero head-motion parallax, which is what infinity looks like.
                o.positionCS = TransformWorldToHClip(GetCameraPositionWS() + dirWS * _CS_Compress.z);
                o.dirIcrf = dirIcrf;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Sample by ICRF direction: hardware-seamless, no pole oversampling, no inside-out
                // mirroring (the trap CLAUDE.md warns about), and no Unity axis in the lookup.
                half raw = SAMPLE_TEXTURECUBE(_SkyCube, sampler_SkyCube, normalize(i.dirIcrf)).r;
                half t   = saturate(0.5h + (raw - 0.5h) * (half)_Contrast);   // raw 0.5 = 0 uK
                half3 c  = SAMPLE_TEXTURE2D(_Lut, sampler_Lut, float2(t, 0.5)).rgb;
                // Premultiplied. This is the ONE pass that writes coverage on purpose: immersion is
                // the opt-in full-space mode. In MR, _Immersion = 0 and the layer skips the draw.
                return half4(c * (half)_Immersion, (half)_Immersion);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
```

The dipole and monopole modes (C's `cmb.mode`) add `D·dot(n, d̂)` in the fragment, using a
per-material ICRF dipole vector (data-beyond-and-cmb §3.5). That is the CMB lane's addition. It is
a uniform, so the shader stays single-variant.

### 5.7 `CosmoSynth/Volume`: bounded raymarch with a per-eye ray origin (dust, CF4 basins)

The mesh is a unit cube [−0.5, 0.5]³. The transform is the group placement (Rigid or Enclosing,
§1.6), with the layer frame's rotation. The march runs in **object space** from **this eye**. A
similarity about the centre eye leaves every eye at the same object-space point, so compression
never touches the integral. The step count comes from the CPU through the step-coverage governor
(Fractarium pattern): `N = clamp(B / coverage, 16, 64)`, B = 30 (§6).

```hlsl
Shader "CosmoSynth/Volume"
{
    Properties
    {
        _Density ("Density: R8, log-encoded, x fastest", 3D) = "black" {}
        _Ramp ("Colour ramp 256x1 (sRGB)", 2D) = "white" {}
        _Decode ("x: vmin, y: ln(vmax/vmin), z: extinction per (density x object unit), w: ramp scale", Vector) = (0.02,8.3,1,1)
        _Steps ("x: steps (CPU governor), y: empty cut (encoded value)", Vector) = (32,0.22,0,0)
        _Fade ("Fade", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Volume"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha   // premultiplied over: dust absorbs, so coverage = opacity
            ZWrite Off
            ZTest LEqual     // an opaque body inside the box rejects the back face: the body shows, no depth texture needed
            Cull Front       // back faces: correct with the eye outside or inside the box

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            TEXTURE3D(_Density); SAMPLER(sampler_Density);
            TEXTURE2D(_Ramp);    SAMPLER(sampler_Ramp);

            CBUFFER_START(UnityPerMaterial)
                float4 _Decode;
                float4 _Steps;
                float  _Fade;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.positionOS = v.positionOS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);   // per-eye camera position
                float3 eyeOS = TransformWorldToObject(GetCameraPositionWS());
                float3 dir = normalize(i.positionOS - eyeOS);
                float3 inv = 1.0 / dir;                           // IEEE inf on an axis is handled by min/max
                float3 tA = (-0.5 - eyeOS) * inv;
                float3 tB = ( 0.5 - eyeOS) * inv;
                float3 t0 = min(tA, tB), t1 = max(tA, tB);
                float tNear = max(max(max(t0.x, t0.y), t0.z), 0.0);   // 0 when the eye is inside
                float tFar  = min(min(t1.x, t1.y), t1.z);
                if (tFar <= tNear) return half4(0, 0, 0, 0);

                int   steps = max((int)_Steps.x, 1);
                float dt = (tFar - tNear) / steps;
                float t  = tNear + dt * CS_IGN(i.positionCS.xy);    // jitter turns banding into fine noise
                half3 colour = 0;
                half  alpha  = 0;
                [loop] for (int s = 0; s < steps; s++, t += dt)
                {
                    half v = SAMPLE_TEXTURE3D_LOD(_Density, sampler_Density, eyeOS + dir * t + 0.5, 0).r;
                    if (v <= (half)_Steps.y) continue;
                    float density = _Decode.x * exp(v * _Decode.y);                       // log decode
                    half  a = (half)(1.0 - exp(-density * _Decode.z * dt));               // Beer-Lambert per step
                    half3 c = SAMPLE_TEXTURE2D_LOD(_Ramp, sampler_Ramp, float2(saturate(v * _Decode.w), 0.5), 0).rgb;
                    colour += (1.0h - alpha) * a * c;
                    alpha  += (1.0h - alpha) * a;
                    if (alpha > 0.985h) break;
                }
                return half4(colour, alpha) * (half)_Fade;           // premultiplied
            }
            ENDHLSL
        }
    }
    Fallback Off
}
```

### 5.8 `CosmoShaders.All`, the ports, and why there are no material templates

`CosmoShaders.All` (`CosmoShaders.cs:49-59`) becomes exactly these eleven. `XrSetup` writes them
into Always Included (Section D seeds this array with one slot per lane, §D2.2) and **removes** the eight former built-in names it added before (`Standard`,
`Unlit/Color`, `Unlit/Transparent`, `Sprites/Default`, `Particles/Standard Unlit`,
`GUI/Text Shader`, and the two old CG shaders under their names, which are replaced in place).

| Name | File | Queue | Source | Section D P0.4b provisional name |
|---|---|---|---|---|
| `CosmoSynth/Body` | `Body.shader` | 2000 | §5.3 (replaces Standard in `CelestialBodyView.cs:114-121, 442-453, 753-792`) | `CosmoSynth/Planet` |
| `CosmoSynth/Point` | `Point.shader` | 3050 (1010 in sky mode, set on the material) | §5.4 (replaces `Particles/Standard Unlit`, `StarSprite.cs:141-151`) | `CosmoSynth/Points` |
| `CosmoSynth/Ribbon` | `Ribbon.shader` | 3060 | §5.5 (replaces LineRenderer + `Sprites/Default`, `OrbitLineRenderer.cs:42-88`) | `CosmoSynth/Line` |
| `CosmoSynth/Sky` | `Sky.shader` | 1000 | §5.6 | (new) |
| `CosmoSynth/Volume` | `Volume.shader` | 3010 | §5.7 | (new) |
| `CosmoSynth/Atmosphere` | `CosmoAtmosphere.shader` | 3100 | **port** of today's CG shader to this skeleton: same maths (`:111-237`), `GetCameraPositionWS()` after the eye macro. Rigid mode passes the drawn centre and radii (as today); Envelope mode (uniform) integrates in true eye-relative space in units of the planet radius (§1.6) | same |
| `CosmoSynth/Rim` | `CosmoRim.shader` | 3100 | port of `CosmoRim.shader:30-89`, unchanged maths | same |
| `CosmoSynth/Ring` | `Ring.shader` | 3000 | new: `_BaseMap` (RGBA ring texture) × `_LightColor` × a planet-shadow term (an analytic ray–sphere test toward `_LightDirWS` against the parent's drawn sphere), premultiplied over | `CosmoSynth/UnlitTransparent` (rings) |
| `CosmoSynth/Unlit` | `Unlit.shader` | 2000 | new: opaque `_BaseColor` (beacon bars `AnchorBeacon.cs:65`, pointer ray `BodySelector.cs:70`) | same |
| `CosmoSynth/Panel` | `Panel.shader` | 3200 / 3201 | new: `_BaseMap` alpha × vertex colour, premultiplied over (`CosmoTheme.cs:229,240`) | same |
| `CosmoSynth/Text` | `Text.shader` | 3210 | new: font atlas alpha × vertex colour (display-decoded), premultiplied over; used by `CosmoText.cs:78` instead of `GUI/Text Shader`. Legacy `TextMesh` stays, because its built-in font always ships (quest-rendering-techniques §9) | TMP `TextMeshPro/Mobile/Distance Field` (conflicts with binding 2, §9 item 10) |

Section D's `CosmoCommon.hlsl` is this document's `CosmoCore.hlsl`: one include, whichever name the integrator seeds. Every lane includes it by that one name.

**No `Resources` material templates.** Templates exist only to keep the keyword variants alive that
a runtime-built scene would otherwise strip (the URP Lit trap, memory
`unity-quest3-passthrough-headless`). Every shader here is single-variant, so Always Included plus
`Shader.Find` through `CosmoShaders.Load` is sufficient, and there is nothing to strip. No
`Universal Render Pipeline/*` shader may be referenced at runtime (gated, §8).

---

## 6. Quest 3 per-frame budget at 72 Hz

The frame is 13.9 ms. **App GPU ≤ 11.0 ms** with SRP foveation on at Medium and MSAA 4×. Passthrough
costs about 17 % GPU in the compositor (Worldfall RESEARCH, quoted in quest-rendering-techniques
§12). The global limits follow Meta's Quest 3 figures (quest-rendering-techniques §4.3 and §12,
VERIFIED there): 200–300 draw calls for a busy app, 1.3–1.8 M triangles.

| Global limit | Value |
|---|---|
| Draw calls | ≤ 150 |
| Vertices processed | ≤ 1.2 M (triangles ≤ 0.8 M) |
| Transparent fragments | ≤ 3.0 eye-screens in total; point sprites ≤ 0.5 |
| Raymarch | ≤ 30 step-coverages (one step over the area of both eyes) |
| Texture memory | ≤ 150 MB |

Allocation per layer, **INFERRED until measured**. Every row is a `LayerBudget` constant, and the
play-mode gate sums the rows with `Weight > 0`. Windows are Section C's (log10 U).

| Layer | Draws | Vertices | Fragments (eye-screens) | Step-cov. | Texture MB | GPU ms | When active |
|---|---|---|---|---|---|---|---|
| `cmb` sky form (immersion > 0 only) | 1 | 2 k | 1.0 (opaque fill, 1 cube sample + 1 LUT) | 0 | 8.4 (6 × 1024² R8 + mips, B §2.13) + 0.5 mask | 0.5 | Space mode / BEYOND rungs (C) |
| sky stars, distance unknown | 1 | 41 k (10,224 × 4) | 0.02 | 0 | 0 | 0.1 | always |
| `stars` (≈ 120 k AT-HYG, chunked, cone-culled) | ≤ 64 (≈ 20 in view) | ≤ 480 k (≈ 100 k in view) | ≤ 0.5 | 0 | 0 | 1.2 | log10 U −19.6 … +3.0 |
| `galaxiesLocal`, `galaxies2mrs`, `groupsCf4` (≈ 84 k) | ≤ 16 | ≤ 340 k | ≤ 0.3 | 0 | 0.1 (type atlas) | 0.8 | log10 U ≤ −17.8 |
| `globulars`, `cepheids`, `masers`, `belts`, `census` | ≤ 6 | ≤ 60 k | ≤ 0.1 | 0 | 0 | 0.2 | C's windows |
| `dust` (raymarch) | 1 | 24 | ≤ 1.0 | ≤ 30 | 3.3–10.3 (R8 3D) | ≤ 2.5 | log10 U −19.0 … −14.6 |
| `dust` (voxel-sprite LOD) | ≤ 8 | ≤ 295 k | ≤ 0.9 | 0 | 0 | 1.0 | when the box is < 10 % of the screen (instead of the raymarch) |
| Bodies: spheres, rings, atmospheres | ≤ 60 | ≤ 150 k | atmospheres ≤ 0.5 | ≈ 3 (atmosphere samples, the existing 10 × 4 ≈ 0.3 ms per screen) | planet maps ≤ 64 | 3.0 | always |
| Body markers (one batched mesh) | 1 | ≤ 1 k | 0.01 | 0 | 0 | 0.05 | always |
| Spacecraft models | ≤ 20 visible | ≤ 110 k (whole set) | — | 0 | 0 | 0.5 | above the 3 dmm model threshold |
| Ribbons (orbits, constellations, grids) | ≤ 16 | ≤ 300 k (≤ 150 k triangles) | ≤ 0.2 | 0 | 0 | 0.8 | |
| UI panels and text | ≤ 30 | ≤ 20 k | ≤ 0.3 | 0 | ≤ 4 (font) | 0.8 | |
| **Totals (worst simultaneous)** | **≤ 150** | **≤ 1.2 M** | **≤ 3.0** | **≤ 33** | **≤ 100** | **≤ 9.8** (+1.2 headroom) | |

Measure frame time with `adb logcat -d | grep VrApi` (the compositor's `FPS`, `App`, `GPU%` and `Fov`
fields: Fractarium CLAUDE.md). `Fov=` is the only proof that foveation is really on. The dust κ
(ms per step-coverage) must be measured on device with a Fractarium-style cost probe before B = 30 is
trusted (quest-rendering-techniques §5.2).

---

## 7. Migration from today's Built-in code: every file

Line numbers are at `cf93a25`. Hand this list to the lanes; each row says what to change.

### 7.1 Rendering and XR configuration (through `XrSetup`, never by editing YAML)

1. **`Editor/XrSetup.cs`**
   - **`EnsureLoader` (`:143`):** call `CreateDefaultManagerSettingsForBuildTarget` only when
     `perBuildTarget.SettingsForBuildTarget(Android)?.Manager == null`. Then remove the orphans:
     every `XRManagerSettings` sub-asset of `XRGeneralSettingsPerBuildTarget.asset` that no
     `XRGeneralSettings.Manager` references (4 today, render-xr-audit §4.3), using
     `AssetDatabase.RemoveObjectFromAsset`.
   - **`RequiredAndroidFeatures` (`:43-75`):** add `com.unity.openxr.feature.foveatedrendering`.
   - **`DisabledAndroidFeatures` (`:90-93`):** becomes `{ "com.unity.openxr.feature.input.handtracking" }`,
     the colliding Microsoft profile, enabled today at `OpenXR Package Settings.asset:1164-1166`.
   - **`EnsureRenderSettings` (`:268-285`)**, through the public `OpenXRSettings` API (verified to
     exist in `com.unity.xr.openxr@58837b2992e0/Runtime/Settings/OpenXRRenderSettings.cs`: `:38`,
     `:85`, `:230`, `:282`, `:405`, `:471`):
     `renderMode = SinglePassInstanced`, `depthSubmissionMode = DepthSubmissionMode.None`,
     `latencyOptimization = LatencyOptimization.PrioritizeInputPolling`, `symmetricProjection = true`,
     `optimizeBufferDiscards = true`, `foveatedRenderingApi = BackendFovationApi.SRPFoveation`.
   - **`MetaQuestFeature`:** its copies `m_symmetricProjection` and `m_optimizeBufferDiscards` are
     `internal` (`MetaQuestFeature.cs:109-141`). Set them to 1 through `SerializedObject` on the
     feature asset, the way `EnsureBothInputHandlers` writes `activeInputHandler`, and log both values.
   - **`EnsureUrp`** (new): port `~/projects/Worldfall/Assets/Worldfall/Editor/WorldfallUrpConfig.cs`
     `Create()` (288 lines, headset-proven).
     - Assets: `Assets/CosmoSynth/Settings/CS_URP.asset` and `CS_Renderer.asset`.
     - Renderer: `renderingMode = Forward`, `depthPrimingMode = Disabled`,
       `intermediateTextureMode = Auto`, `postProcessData = null`.
     - Asset expectations, set through SerializedObject exactly as Worldfall does: `m_MSAA 4`,
       `m_SupportsHDR 0`, `m_RequireDepthTexture 0`, `m_RequireOpaqueTexture 0`,
       `m_MainLightRenderingMode 0` (no Unity lights are used, §5.3), `m_MainLightShadowsSupported 0`,
       `m_AdditionalLightsRenderingMode 0`, `m_AdditionalLightShadowsSupported 0`,
       `m_SoftShadowsSupported 0`, `m_UseSRPBatcher 1`, `m_SupportsDynamicBatching 0`,
       `m_EnableLODCrossFade 0`, `m_StoreActionsOptimization 0`, `m_RenderScale 1.0`,
       `m_ShadowDistance 0`.
     - Assign the asset to every quality level and to `GraphicsSettings.defaultRenderPipeline`. On
       the Android default level (index 2 today) set `pixelLightCount 0`, `shadows Disable`,
       `vSyncCount 0`, `antiAliasing 0`; the URP asset owns MSAA.
   - **`EnsureColorSpace`** (new): `PlayerSettings.colorSpace = ColorSpace.Linear`
     (`ProjectSettings.asset:49` becomes 1).
   - **`EnsureFramePacing`** (new): `PlayerSettings.Android.optimizedFramePacing = false`, which
     removes Swappy and `libswappywrapper.so` (API name INFERRED; verify with the compile).
   - **`EnsureShadersIncluded` (`:343-394`):** as today, from `CosmoShaders.All`, **plus** remove the
     eight former built-in entries.
   - **`DeleteOrphanXrAssets`** (new): `AssetDatabase.DeleteAsset` on `Assets/XR 1`, `XR 2`, `XR 3`
     and `Assets/XR/Settings 1..3` if they are empty, and on
     `Assets/XR/Resources/XRSimulationRuntimeSettings.asset` unless it is the registered one.
   - Log a one-line `[XrSetup] SUMMARY …` holding every value above, which the settings gate greps.
2. **`Editor/ServerBuild.cs:36-37`**: `Version = "2.0.0"`, `VersionCode = 17`. Pin
   `EditorUserBuildSettings.selectedBuildTargetGroup = Android` (build-pipeline §2.2). The version
   gate (build-pipeline §9.3) refuses code ≤ 16 and refuses to overwrite a file.
3. **`Runtime/CosmoSynth.Runtime.asmdef`**: drop the unused XR Interaction Toolkit reference and keep
   `RenderPipelines.Universal.Runtime`, which `XrRig` now uses for camera data.
   **`tests/CosmoSynth.Runtime.Compile.csproj`**: add
   `Unity.RenderPipelines.Universal.Runtime.dll` and `Unity.RenderPipelines.Core.Runtime.dll` from
   `$(UnityScriptAssemblies)`. Both already exist in main's ScriptAssemblies (build-pipeline §2.5).
4. **`Runtime/XR/XrRig.cs`**
   - `:71`: far becomes `ScaledSpace.FarPlaneMetres` (2000).
   - `:77-78`: delete the `DepthBands` attach.
   - After `:73`: URP camera data (§1.1).
   - `:90-93`: delete the dead hand anchors.
   - `:96-97`: delete `RenderSettings.ambient*` (no built-in lighting is used).
   - Add a public `PassthroughClearColour`, used by the gates.
5. **Delete** `Runtime/Rendering/DepthBands.cs` (157 lines) and every reference to it:
   `ScaleRenderer.cs:28-30,51,106-109`; `PlayModeShot.cs:391-394,552-553,764-765,811-812`;
   `RenderDiagnostics.cs:32-34` (the "cams" comment).
6. **Delete** `Editor/SceneShot.cs` (417 lines). It builds its own Built-in scene, swaps axes itself
   (`:142-143`) and "proves nothing about the shipped app" (CLAUDE.md). Put a first-line pointer to
   `tools/shot.sh` in `~/server-setup/shot-cosmosynth.sh`; that file is protected from deletion.
7. **Shaders**: add §5's eleven under `Runtime/Shaders/`, plus `CosmoCore.hlsl`. Port
   `CosmoAtmosphere.shader` and `CosmoRim.shader` in place. Rewrite `CosmoShaders.cs:22-59` to list
   the eleven, and change the `Material()` fallback at `:88` from `Standard` to `CosmoSynth/Unlit`.

### 7.2 Placement, frames and the single mapping

8. **`Core/Frames/StandardFrames.cs`**
   - `:105-108`: the galactic fix (§2.2).
   - Add `GalacticToSupergalactic` and `EquatorialToSupergalactic`. Section C's
     `PresentationPlane.PlaneUp` consumes them (§3.1).
   - **`Core/Scale/ScaleSpace.cs`**: delete the band stack (`:28-78`, `:150-179`); extend
     `RenderTransform` (`:197-225`) with `Level` and `P` (§2.1).
   - **New** `Core/Scale/ScaledSpace.cs` (§1.7) and `Core/Catalog/LayerRecords.cs` (§4.1).
   - **New** `Core/Sky/PointChunks.cs` (§2.4).
9. **The single `SetCatalog`**, as Section B §3 and §4 specify:
   - The bootstrap no longer reads files itself. B's `DataLoader` reads through
     `StreamingAssetReader`, and B's Core `CatalogAssembler` merges `solar_system.json` and
     `spacecraft.json`. The runtime calls **one** `Simulation.SetCatalog(assembled)`.
   - Exoplanets are **not** in that merge. Hosts are the `exohosts` layer and are expanded with
     `AddBodies` on visit (B §4). So `RootIds[0]` is `sun` again. `HostStarOf` (item 12) is still
     required for expanded and generated systems.
   - Delete `UniverseSimulation.MergeCatalogFromJson` (`:267-293`), `LoadCatalogFromJson`
     (`:243-257`), `LoadCatalogFromStreamingAssets` (`:211-240`) and the two readers
     (`:104-202`, replaced by B's reader). Also delete `CosmoSynthBootstrap.LoadCatalogRoutine`
     (`:193-231`).
   - `OnCatalogLoaded` (`:233-275`) now runs exactly once. Keep its clear-before-create code, and
     add a counter that the play-mode gate asserts equals 1.
   - Later additions keep using `AddBodies`/`RemoveBodies` (`:334-379`), which also raise the new
     `BodiesChanged` event.
10. **`Runtime/Universe/FloatingOrigin.cs`**
    - Rewrite as §2.1: delete the rebase machinery (`:9-16`, `:37-47`, `:59-75`, `:109-160`) and the
      identity mapping (`:78-86`).
    - `LateUpdate` is deleted: the presentation plane is the Universe host's rotation, owned by
      C's driver (§3).
    - `SetUnitsPerKm` clamps with C's `ScaleLimits` [2e-25, 1e3].
11. **`Runtime/Universe/CelestialBodyView.cs`**
    - Split `UpdateState` and `Place` (§1.8).
    - Delete `PlaceForRendering` and the horizon pinning (`:200-254`); `StarPointHorizonUnits` goes
      with it (`UniverseSimulation.cs:427-436`).
    - `UpdateVisualScale` (`:256-294`) multiplies by `g.Scale` and applies the 2.5 px floor as
      "draw as point" (§1.6).
    - `UpdateStarPointScale` (`:311-358`) becomes `PointState` (visible, placed position, apparent
      magnitude, colour, dmm override, kind), consumed by the new `BodyMarkers`.
    - Rotation (`:194`) becomes `origin.MapRotation(...)`.
    - Delete the statics (`:898-917`).
    - Materials (`:114-121`, `:442-453`, `:753-792`) become `CosmoSynth/Body`:
      - `_Glossiness` → `_Smoothness`;
      - `_METALLICGLOSSMAP` + `_MetallicGlossMap` → `_SurfaceMap`;
      - `_NORMALMAP` + `_BumpMap` → `_NormalMap`, always bound (the default is `bump`);
      - `_EMISSION` + `_EmissionColor` → `_Emission`.
      There are no keywords any more.
    - The metallic clamps (`:478`, `:488`) become irrelevant; delete them with their comments.
    - `RefreshRendererEnabled` (`:850-854`) computes
      `IsGeometryDrawn = IsKnown && !DrawnAsPoint && !_modelSuppressed`.
    - `BodyLabelLayer` and every other reference to `DrawnAsStarPoint` switch to `DrawnAsPoint`
      (grep).
12. **Host-star lighting through the parent chain**
    - Core `CelestialCatalog.HostStarOf(string id)`: walk `ParentId` to the first `IsStar`, at most
      32 hops; null for rogue or unknown bodies; a star returns itself. Tests: `moon` → `sun`; any
      `exo-*` planet → its host; a spacecraft on Mars → `sun`; a synthetic parent cycle terminates.
      This one function also replaces the four duplicated "walk up to the star" loops
      (architecture-map §6.3).
    - Delete `SkyAndRings.BuildStarLight` (`:581-637`), which includes the `RootIds[0]` =
      `exo-61-vir` defect at `:585`, and the ambient directional light.
    - `ScaleRenderer` sets, per drawn body per frame:
      - `_LightDirWS = universe.TransformDirection(origin.MapDirection((star − body).Normalized))`;
      - `_LightColor = BlackBodyColor(T_star) × StarLook.StarLightIntensity`, a new constant starting
        at 0.85 (today's `:607`), to re-measure;
      - `_Ambient = CosmoTheme.AmbientFill`, a new token starting at (0.05, 0.055, 0.07), today's
        `XrRig.cs:97`.
      The atmosphere's `_SunDirection` reads the same vector.
13. **Every ad-hoc axis swap is deleted**:
    - `SkyAndRings.cs:220`: the shell. The interim path until the stars lane lands `StarLayer`:
      rebuild the existing shell as a `CosmoSynth/Point` mesh in sky mode (`_Space = 2`) from
      `star.Direction` in ICRF with **no** swap, and `_LayerToWorld = _CS_IcrfToWorld`. That removes
      the anchor-parented 900-unit sphere (`:29`, `:407-447`) in the same step.
    - `XrLocomotion.cs:150-151`: `origin.UnmapDirection(universe.InverseTransformDirection(worldDir))`.
    - `UniverseNavigator.cs:144,186`: the same, through `FloatingOrigin`.
    - `UniverseNavigator.cs:202`: fix B12, placing the camera with
      `universe.TransformPoint(local)`, not a raw universe-local value.
    - `SceneShot.cs:142-143`: deleted with the file.
14. **`Runtime/XR/TabletopManipulator.cs`**
    - Convert hands to universe-local once, in `UpdateHands` (`:100-104`, §3.3); this fixes
      `:131,138,152-153,173-174,191`.
    - Sticky target: clear it on reach or 0.5 s after release (`:79,170,206-229,240`).
    - `_minimumUnitsPerKm` and `_maximumUnitsPerKm` (`:45`, `:49`) are deleted; use C's
      `ScaleLimits`.
    - Never touch the presentation plane (§3.2).
    - `XrHandInput.cs:282-288` stays in world space; the manipulator owns the conversion.
15. **`Runtime/Rendering/RingSystem.cs:26-37`**: if `!_body.IsGeometryDrawn`, disable the renderer
    and return. Set `[DefaultExecutionOrder(60)]`. The ring material becomes `CosmoSynth/Ring`
    (`SkyAndRings.cs:514-516`).
16. **`AtmosphereShell`, the per-orbit ribbons, `SpatialPanel`, the `BodySelector` highlight**:
    `[DefaultExecutionOrder(60)]` (§1.8).
17. **`Runtime/Rendering/OrbitLineRenderer.cs`**
    - Keep the sampling and the legibility cull.
    - Replace the `LineRenderer` (`:42-88`, material `:82`, width `:235`) with a `CosmoSynth/Ribbon`
      mesh.
    - The transform comes from the owner group: rigid (`_Placement = 0`), or `_Placement = 1` when
      the owner contains the eye (§1.2).
    - Delete `OnOriginShifted` (`:327-335`).
18. **`StarSprite.cs`**: delete (the Gaussian is analytic in `CosmoSynth/Point`).
    **`StarPointBillboard.cs`**: replaced by one new `Runtime/Rendering/BodyMarkers.cs`
    (MonoBehaviour), a single dynamic mesh rebuilt each frame from every view's `PointState`
    (`_Space = 1`). That turns up to 240 draws into 1.
19. **`Runtime/Rendering/ScaleRenderer.cs`**: rewrite `LateUpdate` (`:55-110`) as the §1.8 order-50
    step. Delete `BodiesByApparentSize` and `_sorted` (`:36`, `:112-117`, dead).
20. **`UniverseSimulation.cs`**
    - `LateUpdate` (`:438-451`) computes state only.
    - `NearestBody` (`:504-525`) also asks `LayerRegistry.All[i].NearestSurfaceKm`.

### 7.3 Colour space, UI, diagnostics

21. **Linear re-tune.**
    - All authored constants stay **display-referred**: every `CosmoTheme` colour
      (`CosmoTheme.cs:34-75`) and `StarLook` (`FaintestBrightness 0.40`, `3.8/9.0 dmm`,
      `BrightnessFromMagnitude`). That is what they were measured as.
    - Conversion happens at exactly two boundaries: `Material.SetColor` (Unity linearises; INFERRED)
      and vertex colours (decoded by `CS_DisplayToLinear` in every CosmoSynth shader). **Never** call
      `.linear` on a vertex colour and also decode it in the shader.
    - `PlanetTextureBaker` creates normal and surface maps with `linear: true`; albedo maps stay sRGB.
    - After the switch, the stars lane re-measures five reference stars with the pixel-neighbourhood
      method. Single-quad peaks should match the gamma-era numbers; overlapping quads (core, core,
      halo) now sum in linear and will read brighter. Tune the one exposure knob `_Look.y` first.
      Then re-capture the planet terminator and the atmosphere limb, and adjust `_LightColor`
      intensity and the atmosphere's `_Intensity`.
22. **UI.**
    - `CosmoTheme.cs:229,240` → `CosmoSynth/Panel`. `CosmoText.cs:78` → a `CosmoSynth/Text` material
      that samples the built-in font's atlas. If the UI decision (Section D P0.4d) is TMP SDF, the
      same binding requires a `CosmoSynth/TextSDF` port of `TMP_SDF-Mobile` with this skeleton, not
      TMP's own shader name (§9 item 10).
    - `AnchorBeacon.cs:65` and `BodySelector.cs:70` → `CosmoSynth/Unlit`; `BodySelector.cs:108` →
      `CosmoSynth/Rim`.
    - `CosmoSynthBootstrap.cs:171`: add `ProvenanceHud` only when `!Rig.XrActive` (B16).
23. **`RenderDiagnostics.OneLine` (`:39-57`)** becomes
    `render: URP|BiRP <mode> WxH:<layers> msaa <urp>/<eyeSamples> cams <n> clr SC/a=<a> dep <depthStencilFormat> sub <submission> cs <Linear|Gamma> fov <level> grp <rigid>/<envelope>/<enclosing> push <conflicts> far <max rendered m>`.
    It is logged once to logcat, as today. Red conditions: cams ≠ 1, a ≠ 0 in passthrough, a 16-bit
    depth format, Gamma, BiRP, push > 0.
24. **`Editor/PlayModeShot.cs`**
    - Single-camera capture: delete the far-band compositing (`:778-883`) and the MSAA-off override
      (`:743-747`).
    - Delete the `Unused()` stub (`:324-327`).
    - Add the §8 assertions as **fatal** (exit 4 on failure; the build-pipeline gate G1c).
    - Print `git rev-parse HEAD` into the report.
    - Capturing through `camera.Render()` into a RenderTexture must be re-validated under URP.
      Worldfall captures under URP on this box, which is the precedent (INFERRED).
25. **Docs** (the same change):
    - CLAUDE.md "XR / Quest configuration" (Built-in → URP, the mapping, "87 tests").
    - `docs/architecture.md:137-139,233` (bands → groups).
    - `docs/data-sources.md:177` (ESA is CC BY-NC 3.0 IGO).
    - `docs/feedback.md`: record the 1.7.0 grid report verbatim.
    - The `ScaleSpace` and `DepthBands` comments that describe a float depth buffer the device does
      not have (quest-rendering-techniques §3.1).

---

## 8. Gates that prove the foundation headlessly, and what only the headset can prove

### 8.1 Static gates (any worktree, seconds): `tools/gates/foundation.sh`, with a self-test

```bash
#!/usr/bin/env bash
# tools/gates/foundation.sh -- static render-foundation gates (Section A, section 8.1).
# Usage: tools/gates/foundation.sh [ASSETS_DIR]   exit 0 = every gate passes
#        tools/gates/foundation.sh --self-test    every gate must FAIL on a planted violation
set -u
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
FAILS=0
bad() { echo "FAIL $*"; FAILS=$((FAILS + 1)); }

gate_stereo() {   # every shader with a vertex program: full stereo skeleton, URP tag, no CG
  local f m
  while IFS= read -r -d '' f; do
    grep -q '#pragma vertex' "$f" || continue
    for m in 'multi_compile_instancing' 'UNITY_VERTEX_INPUT_INSTANCE_ID' 'UNITY_VERTEX_OUTPUT_STEREO' \
             'UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO' 'UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX' \
             '"RenderPipeline"="UniversalPipeline"'; do
      grep -qF "$m" "$f" || bad "stereo: $m missing in ${f#$1/}"
    done
    ! grep -q 'CGPROGRAM' "$f" || bad "stereo: CGPROGRAM in ${f#$1/}"
  done < <(find "$1" -name '*.shader' -print0)
}

gate_alpha() {    # additive keeps destination alpha; SrcAlpha One forbidden; ZTest Always needs a reason
  local h
  h=$(grep -rnE --include='*.shader' 'Blend[[:space:]]+One[[:space:]]+One[[:space:]]*($|//)' "$1") && bad "alpha: additive without ', Zero One': $h"
  h=$(grep -rnE --include='*.shader' 'Blend[[:space:]]+SrcAlpha[[:space:]]+One([[:space:]]|$)' "$1") && bad "alpha: SrcAlpha One writes alpha: $h"
  h=$(grep -rnE --include='*.shader' 'ZTest[[:space:]]+Always' "$1" | grep -v 'ztest-always:') && bad "ztest: Always without a reason: $h"
}

gate_shaders() {  # CosmoShaders.All is complete; no Shader.Find elsewhere; no built-in shader names
  local cs="$1/Runtime/Rendering/CosmoShaders.cs" n h
  for n in $(grep -rhoE --include='*.shader' 'Shader[[:space:]]+"CosmoSynth/[A-Za-z]+"' "$1" | sed -E 's/.*"(CosmoSynth\/[A-Za-z]+)"/\1/'); do
    grep -qF "\"$n\"" "$cs" || bad "shaders: $n is not in CosmoShaders.All"
  done
  h=$(grep -rn --include='*.cs' 'Shader\.Find(' "$1/Runtime" | grep -v 'CosmoShaders\.cs') && bad "shaders: Shader.Find outside CosmoShaders: $h"
  h=$(grep -rnE --include='*.cs' '"(Standard|Unlit/[A-Za-z]+|Sprites/Default|Particles/[^"]+|GUI/Text Shader|Universal Render Pipeline/[^"]+)"' "$1/Runtime") && bad "shaders: built-in shader name in Runtime: $h"
}

gate_camera() {   # exactly one camera; no stacking, blits, command buffers or DepthBands
  local n h
  n=$(grep -rho --include='*.cs' 'AddComponent<Camera>' "$1/Runtime" | wc -l)
  [ "$n" -eq 1 ] || bad "camera: $n AddComponent<Camera> in Runtime (want exactly 1)"
  h=$(grep -rnE --include='*.cs' 'CopyFrom\(|CameraClearFlags\.(Depth|Nothing)|targetTexture[[:space:]]*=[^=]|Graphics\.(Blit|DrawMesh)|[^A-Za-z]GL\.|CommandBuffer|OnRenderImage|DepthBands' "$1/Runtime") && bad "camera: forbidden render path: $h"
}

gate_axis() {     # the y/z swap lives only in FloatingOrigin and Core RenderTransform
  local h
  h=$(grep -rnE --include='*.cs' '\.x[[:space:]]*,[^,;]*\.z[[:space:]]*,[^,;]*\.y\b' "$1" | grep -vE 'Runtime/Universe/FloatingOrigin\.cs|Core/Scale/ScaleSpace\.cs') && bad "axis: y/z swap outside FloatingOrigin: $h"
  h=$(grep -rnE --include='*.cs' '\b(ToUnityDirection|ToIcrfDirection|ToUnityRotation)\b' "$1") && bad "axis: removed helper referenced: $h"
}

gate_catalog() {  # the catalogue is adopted once; later additions use AddBodies
  local h
  h=$(grep -rnE --include='*.cs' 'MergeCatalogFromJson|SetCatalog\(Catalog\)' "$1") && bad "catalog: re-SetCatalog path present: $h"
}

gate_order() {    # decorations run after placement (ScaleRenderer = LateUpdate order 50)
  local c f
  for c in RingSystem AtmosphereShell SpatialPanel; do
    f=$(find "$1/Runtime" -name "$c.cs" | head -1)
    grep -qE 'DefaultExecutionOrder\((6[0-9]|[7-9][0-9]|[1-9][0-9]{2,})\)' "$f" || bad "order: $c must be >= 60"
  done
}

self_test() {
  local t g before
  t=$(mktemp -d); mkdir -p "$t/Runtime/Rendering" "$t/Runtime/Universe" "$t/Runtime/Shaders"
  cat > "$t/Runtime/Shaders/Bad.shader" <<'EOF'
Shader "CosmoSynth/Bad" { SubShader { Tags { "RenderPipeline"="UniversalPipeline" } Pass {
  Blend One One
  ZTest Always
  HLSLPROGRAM
  #pragma vertex vert
  #pragma multi_compile_instancing
  // UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO
  ENDHLSL } } }
EOF
  printf 'class CosmoShaders { string[] All = { }; }\n' > "$t/Runtime/Rendering/CosmoShaders.cs"
  printf 'class A { void M() { go.AddComponent<Camera>(); go.AddComponent<Camera>(); var v = new Vector3(p.x, p.z, p.y); Shader.Find("Standard"); } }\n' > "$t/Runtime/Universe/Bad.cs"
  printf 'class B { void M() { Simulation.MergeCatalogFromJson(j); } }\n' >> "$t/Runtime/Universe/Bad.cs"
  for g in gate_stereo gate_alpha gate_shaders gate_camera gate_axis gate_catalog; do
    before=$FAILS; "$g" "$t" > /dev/null
    if [ "$FAILS" -gt "$before" ]; then echo "self-test ok: $g fails on its plant"
    else echo "SELF-TEST BROKEN: $g passed a planted violation"; rm -rf "$t"; exit 2; fi
  done
  rm -rf "$t"; echo "self-test PASS"; exit 0
}

[ "${1:-}" = "--self-test" ] && self_test
A="${1:-$ROOT/unity/Assets/CosmoSynth}"
gate_stereo "$A"; gate_alpha "$A"; gate_shaders "$A"; gate_camera "$A"; gate_axis "$A"; gate_catalog "$A"; gate_order "$A"
if [ "$FAILS" -eq 0 ]; then echo "foundation gates OK"; else echo "foundation gates: $FAILS failure(s)"; fi
[ "$FAILS" -eq 0 ]
```

VERIFIED (run on the laptop in Git Bash against a copy of the `cf93a25` tree):
- `--self-test` passes: each of the six gates fails on its plant.
- On today's tree the script reports exactly the expected defects:
  - the two CG shaders (no URP tag);
  - the six built-in shader names in `CosmoShaders.cs:22-43`;
  - 2 × `AddComponent<Camera>`, plus `DepthBands` (`:29`, `:68`, `:78`, `:106-107`);
  - the `.x, .z, .y` swaps at `SceneShot.cs:143`, `SkyAndRings.cs:220` and
    `CelestialBodyView.cs:904,905,913,917`;
  - the removed helpers.

Once migrated, it must print `foundation gates OK`. Also update the comment at
`RenderDiagnostics.cs:32` (it names DepthBands).

**Settings gate: `tools/gates/settings.py`** (stdlib, over the YAML; render-xr-audit V2 extended).
Each row is (file, key, expected). Its self-test flips one value in a temporary copy and must fail.

| File | Key → expected |
|---|---|
| `ProjectSettings/ProjectSettings.asset` | `m_ActiveColorSpace: 1`; `androidUseSwappy: 0`; `activeInputHandler: 2` |
| `ProjectSettings/GraphicsSettings.asset` | `m_CustomRenderPipeline` points at `CS_URP.asset`'s guid (from its `.meta`); `m_AlwaysIncludedShaders` holds the guids of all 11 `CosmoSynth/*` shaders and none of the 6 former built-ins |
| `Assets/CosmoSynth/Settings/CS_URP.asset` | `m_MSAA: 4`, `m_SupportsHDR: 0`, `m_RequireDepthTexture: 0`, `m_RequireOpaqueTexture: 0`, `m_UseSRPBatcher: 1`, `m_MainLightRenderingMode: 0`, `m_AdditionalLightsRenderingMode: 0` |
| `ProjectSettings/QualitySettings.asset` | every level's `customRenderPipeline` = the CS_URP guid; the Android default level has `pixelLightCount: 0`, `shadows: 0`, `vSyncCount: 0` |
| `Assets/XR/Settings/OpenXR Package Settings.asset`, Android block | `m_renderMode: 1`, `m_depthSubmissionMode: 0`, `m_latencyOptimization: 1`, `m_symmetricProjection: 1`, `m_optimizeBufferDiscards: 1`, `m_foveatedRenderingApi: 1` |
| the same file, features | exactly **one** MetaQuestFeature document (script guid `f647cc05…`, render-xr-audit §2.3 #7) carrying `m_symmetricProjection: 1` and `m_optimizeBufferDiscards: 1`; `…input.handtracking` `m_enabled: 0`; `…foveatedrendering` `m_enabled: 1` |
| filesystem | no `Assets/XR [0-9]*`, no `Assets/XR/Settings [0-9]*`, no `Assets/XR/Resources/XRSimulationRuntimeSettings.asset` |

### 8.2 Core tests (`./run-tests.sh`, any worktree)

`ScaledSpaceTests` (the 18 rows of §1.7) and `FrameTests` (§2.1 and §2.2: the matrices, 13 vectors,
round trips, determinants and the pole-consistency test). Also:
- `PointChunks` (the 6e-8·R bound, and every point in exactly one chunk);
- `HostStarOf` (4 cases);
- Section B's `EquatorialToGalactic` test, which fails until the §2.2 fix lands.

Pass criterion: ≥ 142 + 30 tests, 0 failures, "Unity runtime layer: compiles". The last check is
what proves the Runtime changes compile against URP without an editor (build-pipeline §8).

### 8.3 Play-mode assertions (`PlayModeShot`, in main behind the editor lock; any failure exits 4)

| # | Assertion | Measured as | Tolerance and why |
|---|---|---|---|
| P1 | one camera | `Camera.allCamerasCount == 1`, its `targetTexture == null` outside the capture call | exact |
| P2 | passthrough clear | `clearFlags == SolidColor` and `XrRig.PassthroughClearColour.a == 0` | exact |
| P3 | pipeline | `GraphicsSettings.currentRenderPipeline` is the URP asset with `msaaSampleCount == 4`; `QualitySettings.activeColorSpace == Linear` | exact |
| P4 | drawn inside far | for every enabled renderer that the CPU places (not shader-placed): bounds farthest corner < 0.9·far = 1800 m; shader-placed renderers assert `_CS_Compress` equals the Core constants and SkyRadius < 1800 | exact |
| P5 | nothing pulled into the room | every rigid owner with s < 1 and an un-pushed near point ≥ 5 m has rendered near ≥ 5 m − 1e-3 | 1 mm is float rounding at 5 m (ulp 4.8e-7) with a big margin |
| P6 | **angular-size invariance** | for every drawn body: \|r_rendered/d_rendered − r_true/d_true\| ≤ 1e-5 · r_true/d_true | float32 transforms (24-bit mantissa, 6e-8), ≤ 16 roundings ≈ 1e-6; 10 × margin, while any real scaling bug is ≥ 1e-3 |
| P7 | group order | for every pair of opaque owners overlapping on screen, the rendered intervals are ordered with ≥ 2Δz, or counted; the fixed views require count = 0 | exact |
| P8 | catalogue once | `CatalogLoaded` fired 1 time; `SetCatalog` ran 1 time | exact |
| P9 | passthrough alpha | the existing `ProbeDestinationAlpha`; hiding every additive layer with `COSMOSYNTH_HIDE` changes coverage by 0 | 0 of 255 |
| P10 | **synthetic stereo pair** | two renders with the camera offset ±32 mm along its right axis, placement **not** re-run. The Sun in `default` (f = 74.69 m) shifts by 0.064 m · ppr / 74.69 m px, where ppr = 0.5·H·P11 of the capture; a sky star shifts by 0 | ±0.5 px (centroid precision of a > 5 px disc) |
| P11 | the mapping applied once | in `default`: the Sun's rendered direction from Earth's rendered centre, in Universe-host space, equals `MapDirection(normalize(sun − earth))` | 1e-5 rad (float) |
| P12 | the level plane is level | in every view, world-y of `universe.TransformDirection(MapDirection(PlaneUp(active plane)))` = 1 | 1e-6 (float quaternion) |
| P13 | budget | sum of `LayerBudget` over layers with `Weight > 0`, plus bodies, ≤ §6 | exact |

**Fixed capture views.** Section D §D3.10 makes views data-driven (`rung:<id>`, `dest:<id>`,
`layer:<id>`). Captures go to `/tmp`, are scp'd and read, and each report prints the group table
and the `render:` line. The Foundation requires seven scales: the Earth hologram (`default`), the
Solar System in the room, 10 pc/m, 30 kpc/m, 3 Mpc/m, the observable universe, and true scale. Use
the nearest C rungs, with U overridden to the §1.6 walkthrough values, so that the walkthrough
numbers are the expected values.

The APK artefact gate is build-pipeline G3 / render-xr-audit V5, plus:
- `libswappywrapper.so` absent;
- the boot.config xr-block MD5 **will change**, because symmetric projection and foveation turn on
  (Worldfall's APK shows `xr-mvpvv-enabled` differing; INFERRED). Record the new baseline, with its
  reason, from the first 2.0.0 APK;
- no `Standard`, `Particles/Standard Unlit` or `GUI/Text Shader` strings in `globalgamemanagers`'s
  always-included list.

### 8.4 What only the headset can prove

| # | Check | How |
|---|---|---|
| H1 | the 1.7.0 grid is gone, in MR and in VR | look; first also on the recovered 1.9.0 APK (build-pipeline gate P) |
| H2 | the depth format really is D24 (or D32) with submission None | the `render:` line `dep` field; red if 16-bit |
| H3 | foveation really on; frame time within budget | `adb logcat -d \| grep VrApi`: `Fov=` > 0; `App=` ≤ 11 ms at 72 Hz in `default`, `truescale` and inside the dust |
| H4 | 1–9 px of far disparity is comfortable; the sky does not swim; the CMB reads as infinitely far | wear it; if uncomfortable, halve d0 (the single knob, §1.3) |
| H5 | passthrough composition: additive layers never darken the room; rings, dust and bodies occlude correctly; MSAA edges on limbs | look against a bright wall |
| H6 | grabs stay under the hand while the level frame slerps | grab during a travel slerp |
| H7 | symmetric projection is active | the eye size in the `render:` line changes with the IPD setting |

---

## 9. Risks and open questions

1. **Nothing here has been compiled.** §5 is PROPOSED HLSL. The first integration step is one
   editor compile of the eleven shaders, then the static gates, then the P-assertions.
2. INFERRED engine facts this design leans on; each is confirmed by the first capture:
   - `_ScreenParams` is the eye-texture size under URP XR (BoltPoints relies on it on device);
   - `Material.SetColor` linearises in Linear mode;
   - negative-scale transforms flip culling;
   - `Camera.Render()` works for the harness under URP;
   - `PlayerSettings.Android.optimizedFramePacing` is the Swappy switch;
   - variables in `UnityPerMaterial` that are absent from `Properties` (`_LayerToWorld`) keep the
     SRP Batcher on.
3. **The grid mechanism is still INFERRED** (render-xr-audit §8). The design removes every
   ingredient either way. One headset look at 1.9.0 settles it and costs no build.
4. **D16 would break the depth budget** (§1.4). The `render:` line catches it on the first launch.
5. **Envelope spheres quantise at ~6e-8·|T|**: about 0.5 m when standing on a true-scale planet,
   until Phase-3 terrain with tile-local origins exists. Accepted.
6. **Points just in front of a rigid group** can be hidden wrongly, within
   `[D·e^(−r/d0), D − R]` (§1.6). No real catalogue case falls in the band; the bound goes into the
   report.
7. The **push rule's floor** can leave a conflict unresolved (reported as `push > 0`). The fixed
   views must show 0. If a real scene shows > 0, the answer is to give that pair's owner a common
   rigid ancestor, not to lower the floor.
8. **The Hipparcos-convention galactic fix changes nothing visible today**, because no galactic
   layer exists yet. It must land before any lane draws in galactic coordinates. The fix and its
   test ship in the same commit.
9. Choosing Planck versus WMAP (and the other NC sources) is a user decision (data-beyond-and-cmb
   §2). Section B makes it a bake-time file swap with zero code.
10. **Text shader conflict.** Section D P0.4b/P0.4d put TextMeshPro's own
    `TextMeshPro/Mobile/Distance Field` into `CosmoShaders.All`. Binding 2 requires every shader to
    be a custom single-variant `CosmoSynth/*` URP shader with the full stereo skeleton. TMP's shader
    does carry the stereo macros (quest-rendering-techniques §9), but it is multi-variant and not
    ours. Either keep legacy TextMesh with `CosmoSynth/Text` (this document), or port
    `TMP_SDF-Mobile` as `CosmoSynth/TextSDF`. The integrator decides at P0.4d. The static gate
    (§8.1) fails TMP's name in Runtime, deliberately.
11. **Shader names.** D's provisional names (`Planet`, `Points`, `Line`, `UnlitTransparent`,
    `CosmoCommon.hlsl`) map one-to-one onto this section's names (§5.8). D §0 gives the Foundation
    authority on rendering, so these names stand. Lanes use the §5.8 names.
12. **Star-position precision comes from the file.** B stores star positions as f32 pc relative
    to the SSB (B §2.1), so the absolute error is ≤ 6e-8·|r|: 17 AU at 1.4 kpc. Chunking (§2.4)
    adds no error beyond that, but cannot remove it. Close pairs far from the Sun (< 0.1 pc apart,
    > 1 kpc out) can be misplaced by up to a few pixels when viewed from one of them. Promoting the
    nearest system through `NearbyStarSystems` hides that case. An f64 `pos` column would remove it
    at +1.4 MB. This is Section B's call; it is recorded here as a bound, not a blocker.

---

## 10. How Section A maps onto Section D's Phase 0 (binding 9)

Section D owns process: phases, lanes and file ownership. The foundation is its **Phase 0**, which
the integrator does in main, with the optional helper worktrees `p0-core` and `p0-shaders`. The
table maps this document onto D's steps, so that nothing is built twice and nothing is left out.

| D step | This document's content |
|---|---|
| P0.1 tooling | `tools/gates/foundation.sh` (§8.1, self-test verified) and `settings.py` join D §D3's gate set |
| **P0.3** core foundation (`p0-core`, no editor) | `Core/Scale/ScaledSpace.cs` + `ScaledSpaceTests`; `RenderTransform` with P (D's "`UnityAxes`"); the `StandardFrames` galactic fix + `GalacticToSupergalactic` + `FrameTests`; `CelestialCatalog.HostStarOf`; `Core/Sky/PointChunks.cs`; `Core/Catalog/LayerRecords.cs`. The band stack in `ScaleSpace.cs` is deleted |
| **P0.4a** `XrSetup` (editor) | §7.1 item 1 in full: URP in code, Linear, None / symmetric / input polling / discards / SRP foveation, the `MetaQuestFeature` flags, the Microsoft profile off, orphans removed, Swappy off, and the SUMMARY line |
| **P0.4b** shaders (`p0-shaders`) | the eleven §5 shaders and `CosmoCore.hlsl`, under the §5.8 names; `CosmoShaders.All` |
| **P0.4c** one camera + scaled space | §1 (placement, groups, `ScaleRenderer`, `Placement`, `FrameContext`, `BodyMarkers`), `DepthBands` deleted, the axis unification (§7.2 item 13), decoration order 60, orbit ribbons, the interim sky-mode star shell |
| **P0.4d** single `SetCatalog`, star light, HUD, text | §7.2 items 9 and 12, §7.3 item 22; the text decision (§9 item 10) |
| P0.4e `ServerBuild` | §7.1 item 2 |
| P0.5 green | §8.1–8.3 all pass on the seven fixed scales |
| P0.6 alpha APK | §8.3's APK additions; the new xr-block baseline recorded; then §8.4 on the headset |
| **P0.7** hook kit | `CosmoLayer` + `LayerRegistry` (§4.1) seeded as the layer interface that D §D2.2's `BuildLayers()` slot instantiates, next to C's `LayerTable`/`LayerState` |

After P0.7 the Phase-1 lanes (`stars`, `milkyway`, `galaxies`, `cmb`, `solar`, `exoplanets`,
`fiction`, `nav`, `ui`, `lines`) build on §4's contract and §5's shaders, and add their layers
through D's slots. They never touch the foundation runtime files that D §D2.1 reserves for the
integrator.
