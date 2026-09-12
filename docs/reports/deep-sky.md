# Lane deep-sky — report

Status: ready for review
Branch: `lane/deep-sky` (from `lane/foundation-code` 6f19d5d, merged with `lane/foundation-shaders` 6e0e3be)
Worktree: `~/projects/CosmoSynth-wt/deep-sky`
Authority: `docs/CONTRACT.md` §3.4, §4, §5.4, §7.3, Appendix A, Appendix B.1, Appendix C.1–C.12;
`docs/research/2026-09-11-milkyway-atlas-port.md`; `CLAUDE.md`

## Summary

Today the app stops at 130 light years, and it stops there for one reason: the star field was
9,000 quads on a 900-unit shell whose own comment read "distance is deliberately discarded here".
With no distance there is nothing to fly through, and beyond the Solar System there was nothing
else at all — no Galaxy, no Local Group, no structure, no background. That is the "empty dark
space".

This lane replaces the shell with a real volume and builds the seven layers above it: 120,000
stars at their measured parsec positions, the constellation figures between them, the dust cube
raymarched inside its own survey box, the Milky Way as a disc you can leave and still see, the
spiral arms drawn only where they were fitted, the Local Volume and 2MRS galaxies as oriented
impostors with a point fallback, the named large-scale structures, the microwave background as an
inside-out sky, and distance grids at three regimes.

```
./run-tests.sh          212 passed, 0 failed   (was 174; floor 142)
tools/gate-all.sh       G0a PASS  G0b PASS  G0d PASS  G0e PASS
                        G0c FAIL — 14 Project Settings values only XrSetup.Run can write; the
                                   same gate fails identically (15) on lane/foundation-code
                        G0f/G0g/G0h ABSENT — those gate scripts do not exist in the tree yet
```

**All shaders are uncompiled.** No editor was launched (one is reserved for the integrator, the
box is at 98 % disk). Every shader this lane uses is one the shader lane already wrote and that
`stereo_gate` passes statically; this lane wrote no shader and changed none.

---

## 1. Stars in three dimensions

`Runtime/Layers/Stars/StarFieldLayer.cs` + `Runtime/Layers/{PointMesh,ChunkedPoints}.cs` +
`Core/Scale/PointChunks.cs` + `Core/Sky/StarQuery.cs`.

**Chunked positions.** `PointChunks.Build` splits the population octree-wise into boxes small
enough that a float offset from a double chunk origin is exact to 6e-8 of the chunk radius — an
angular error under half a pixel for anything farther than 3.4e-4 of the box radius. One
`MeshRenderer` and one material per chunk, `_EyeInChunk` recomputed per frame with the
subtraction done in double before the cast.

**One mesh of vertex-shader billboards, never instancing.** Four vertices per sprite; all four
corners of a quad carry the same `data.w`, because the shader's "cull" writes an off-screen clip
position, which clips a primitive rather than discarding it (R14).

**Size and brightness from magnitude, with both bounds.** `_PxBounds = (3, 64)` under
`StarLook`'s measured `_SizeDmm = (3.8, 9.0)` dmm. The floor exists because a one-pixel feature
has no shape — the Gaussian's visible core is about half the quad, so a two-pixel quad renders as
a square patch whatever texture it is cut from, which is what shipped in 1.7.0. The ceiling
exists because a floor with no ceiling is unbounded in the worst direction; that one put the
camera inside an inflated lander and produced a black screen with passthrough working. Faintness
rides in brightness, not size: a dimmer star is not a smaller star.

**Colour from temperature, in the right space.** `StarCatalog.ColorFor` returns **linear** RGB and
`CosmoSynth/Point` decodes the vertex stream with `CS_DisplayToLinear`, so the layer encodes once
with `.gamma` on the way in. Never `.linear` as well (§7.4).

**The sky-shell fallback.** The 146 rows with no distance keep a direction-only mesh at infinity,
per eye (`_Space = 2`), and switch off past half a parsec from the Sun. Their direction is
measured and their distance is not; putting them in the volume would mean inventing one.

**Observed sky is a Core query plus a layer mode.** `StarQuery` holds the photometry:
`AbsoluteMagnitude`, `ApparentMagnitude`, `ApparentMagnitudeFrom(star, vantage)`,
`VisualResponse(m, limit)` with the 0.23 eye exponent, and `CountVisibleFrom`. The rendered mode
sets the brightness window's faint end to the limit magnitude and the faint floor to zero, so a
star under the limit is genuinely absent rather than sitting at the floor. Extinction is
subtracted **once**, from the catalogue's baked `av` (R41); the dust layer never re-dims a star.

Bright stars get a halo quad under magnitude 3 and two spike quads under magnitude 1 — about 800
extra vertices in the whole catalogue. Past magnitude 1.5 the brightness channel is saturated and
a bigger disc just reads as a nearer object, so the bright end is told apart by what is around
the dot.

## 2. Constellation figures

`Runtime/Layers/Stars/ConstellationLayer.cs` + `Core/Sky/Constellations.cs`.

Ribbon segments from catalogue index pairs, so each line runs between two real three-dimensional
stars and the figures come apart as the viewer leaves the Solar System. That is the point: a
constellation is a line-of-sight coincidence. `_Placement = 1` (per-vertex compression) because a
figure spans the whole neighbourhood and no single similarity describes it. Provenance
**Fictional** — the endpoints are measured, the figure is a cultural convention with an author.

A file whose indices fall outside the catalogue is **refused whole**, because a shifted index does
not throw: it joins two random stars and still looks like a constellation.

**Boundaries: nothing is drawn.** The schema carries no boundary polygons, and the IAU rectangles
are defined on the B1875 sphere. Deriving them from the figures would be a fabricated boundary.
The option is wired and empty, and says so.

## 3. The Milky Way

- **`DustVolumeLayer`** — the survey box as a bounded proxy, `CosmoSynth/Volume`, `MayEnclose`.
  Step budget from screen coverage: `N = clamp(30 / coverage, 16, 64)`, so steps × pixels stays
  roughly constant. The box is in galactic axes and is the survey's own extent, which is why
  `dustExtent` draws its outline: "why does it stop there" has a visible answer. The non-cube
  aspect (10 × 10 × 0.8 kpc) rides on the transform so the shader's object space stays a cube.
  A header/payload size mismatch is refused rather than thrown.
- **`PointSetLayer`** — one class, five instances: `cepheids`, `globulars`, `masers`, `groupsCf4`,
  `sdss`. They differ by a colour rule and a unit, not by a rendering strategy. Cepheid colour is
  driven by age and its **magnitude by the period-derived `absV`**, correcting the Atlas, which
  held the period and gave every Cepheid M = −4.2 anyway.
- **`SpiralArmsLayer`** — the seven Reid+2019 arms, each drawn at full strength only over its own
  fitted azimuth range, ghosted to 15 % over 30° of extrapolation, then stopped.
  `SpiralArm.Support` is in Core with a test. The Atlas drew complete spirals, which asserts
  structure on the far side of the Galaxy that nobody has measured — and looks magnificent, which
  is why it needs a rule rather than a judgement. The masers are drawn in the same window so the
  fit and its data are visible together.
- **`SunOrbitLayer`** — one ring at R0 = 8.15 kpc about Sgr A*.
- **`MilkyWayDiscLayer`** — 40,000 deterministic sprites: an exponential disc, a flattened bulge,
  and arm overdensities laid only over the fitted ranges. **This is the layer that answers "why
  is it empty out here":** past a few kiloparsecs per metre the catalogue stars are all within
  130 pc of the Sun and collapse to one speck, and without this the viewer flies out of a galaxy
  they cannot see into a black room. Provenance **Procedural** — nobody has photographed the
  Milky Way from outside — constrained by measured numbers. It also carries the galactic-centre
  landmark (destination, label) and `NearestSurfaceKm`, so flight speed at extragalactic scales
  is set by the Galaxy rather than by the Sun.

## 4. Galaxies

`Runtime/Layers/Galaxies/GalaxyImpostorLayer.cs` + `Core/Deep/{GalaxySet,GalaxyOrientation}.cs`.

Two representations in one mesh each. **The point** — every galaxy, one additive sprite from its
absolute magnitude; correct at 20 Mpc per metre, where there is no shape to show. **The resolved
cloud** — the 28 largest, each a scatter of 220 sprites laid out in its own disc plane from the
published axis ratio and position angle, cross-fading in between 3 and 10 pixels of true apparent
size. M31 becomes a tilted ellipse at the angle it is really tilted at.

`GalaxyOrientation` is pure geometry with a test: `SkyBasis`, `MajorAxis` (PA 0 north, PA 90 east,
exact), `InclinationDeg` (Hubble, q0 = 0.2, pinned at three published points), `DiscNormal`, and
`FaceSign` — a name hash, because front-versus-back is genuinely not measurable and must be
stable across runs.

**Unknown shape is drawn as unknown:** no cloud and a dimmer point, counted for the panel. The
Atlas drew 542 of its 911 galaxies with a *default* ellipticity as if it were measured, and from
the picture there is no way to tell those from the 369 real ones. `SelfCheck` also fails a
catalogue in which not one row has an unknown ellipticity, because no real catalogue manages that.

The Milky Way's own row (`IsHome`) is never drawn as a point: its disc comes from `GalaxyModel`.

## 5. Large-scale structure

`Runtime/Layers/Galaxies/StructureLayer.cs` (`basins`) plus the `sdss` and `groupsCf4` point sets
and `galaxies2mrs`. Named superclusters, walls, voids and basins as three orthogonal marker rings
at their published positions, each labelled and each carrying a citation — a file with an
unreferenced entry is refused whole.

**The watershed outlines are deliberately not drawn.** A basin boundary is a surface computed from
the CF4 velocity field; without `cf4_basins` the only way to produce one is to invent it, and a
ring at a published centre and radius is a claim the citation supports where a boundary is not.

## 6. The CMB

`Runtime/Layers/Cmb/CmbLayer.cs` + `Core/Cmb/{CmbModel,CmbSky}.cs`.

An inside-out sphere whose object-space vertices *are* ICRF directions, anchored to each eye in
the vertex stage — zero disparity, zero head-motion parallax, and **no surface to fly into**. It
fades in only at the BEYOND rungs and only in Space mode or with the backdrop switch, because
`CosmoSynth/Sky` writes coverage into alpha on purpose and alpha is what hides the room.

**The colour mapping is a function.** `CmbModel.Palette(microK, contrast, palette)` is the single
place it lives; the 256 × 1 LUT, the legend and the tap-to-measure readout are all generated from
it. The Wien peak of a 2.725 K black body is at 1.06 mm, so every CMB image ever published is a
palette choice, and writing it as code with its range in the signature is what stops three
surfaces telling three stories about what red means.

**The dipole toggles, and is off by default.** It is not a feature of the early universe — it is
the Solar System moving at 369 km/s — and a viewer who cannot switch it off has no way to learn
that. Adding it re-bakes the cube on the CPU through `CmbModel` rather than adding a shader
uniform, so the `cmb-faces` assertion stays a real comparison against `CmbSky.Sample`. **That
assertion must run in Anisotropy mode.**

`CmbSky.DirectionOf` is the tested inverse of `FaceOf`; a flipped face reads as hundreds of
microkelvin in a place the eye cannot check, on a map that still looks exactly like the CMB.

## 7. Distance and light-time grids

`Runtime/Layers/Lines/DistanceGridLayer.cs` + `Core/Grids/DistanceGrids.cs`.

Three regimes — 10–500 pc about the Sun, 2–20 kpc about Sgr A*, 0.5–10 Mpc about the Local Group —
each a ring set with twelve spokes, each ring labelled with **both** a distance and a
light-travel time. At these scales the second number is the interesting one.

Drawn only when legible: a ring is skipped below 8 cm and above 3 m of room radius, the same
both-ended rule the orbit lines learned when Earth's own orbit seen from Earth put a hairline
across two shipped screenshots. One regime at a time, chosen by which has the most legible rings —
which grid suits a scale is a fact about the scale, not a preference.

Grids keep their native sampling frame (§3.4): the plane comes from `StandardFrames`' own matrix
rather than from a second spelling of "the galactic plane".

---

## Two defects the new tests caught, both fixed at the root

**`PointChunks` claimed a chunk ceiling it did not enforce.** Depth-first recursion tested the
count on the way in and kept splitting on the way down, so a 16-chunk ceiling produced 29 chunks.
A chunk is a draw call, so a ceiling that does not hold is a budget that does not hold. Replaced
with a breadth-first split that stops *before* it can overshoot, and that closes a degenerate box
(the catalogue's 146 coincident sentinel positions) as one chunk rather than recursing forever.

**The Galactic centre was 0.14° from Sgr A*.** The first version placed it at galactic
(l, b) = (0, 0) and tilted by the Sun's 20.8 pc height. The IAU galactic frame was fixed in 1958
from 21 cm data and its origin is about 0.07° from the radio source; the Sun's offset is from a
*different* plane. The centre now comes from the measured position (Reid & Brunthaler 2004 — the
row CONTRACT C.9 already lists), and the galactocentric basis is built by orthonormalising that
direction against the north galactic pole, with +y a cross product so the frame cannot come out
mirrored. `SunGalactocentricKpc` is derived from it, and the test pins **both** the derived 6.5 pc
above the IAU plane and the adopted 20.8 pc above the true one, asserting they differ — so the
distinction cannot quietly collapse back into one number.

Note for whoever reads `FrameTests`: its comment "galactic longitude zero → RA 266.404995,
Dec −28.936174 (**Sgr A\***)" mislabels the value. That is galactic (0, 0). Sgr A* is
266.41684 / −29.00781. The assertion is right; the parenthesis is not.

---

## Budget arithmetic for this lane

Global (§7.3): 150 draws, 1.2 M vertices, 3.0 transparent eye-screens, 33 step-coverages.
Worst simultaneous case is the `neighbourhood`/`clouds` rungs, where stars, dust, constellations,
the point sets and the grids overlap.

| Layer | Draws | Vertices | Frag | Steps | Tex MB | §7.3 row |
|---|---|---|---|---|---|---|
| `stars` (64 chunks + 1 shell) | 65 | 480,584 | 0.502 | 0 | 0 | "stars" 64 + "sky stars" 1 |
| `constellations` | 2 | 12,000 | 0.02 | 0 | 0 | ribbons (16 shared) |
| `dust` | 1 | 24 | 1.0 | **30** | 10.3 | dust raymarch |
| `cepheids` / `globulars` / `masers` | 2 each | 20 k each | 0.04/0.04/0.02 | 0 | 0 | the 6-draw / 60 k row |
| `arms` | 1 | 3,000 | 0.02 | 0 | 0 | ribbons |
| `sunOrbit` | 1 | 800 | 0.01 | 0 | 0 | ribbons |
| `mwDisc` | 8 | 160,000 | 0.15 | 0 | 0 | **no row of its own — see below** |
| `galaxiesLocal` | 8 | 120,000 | 0.15 | 0 | 0.1 | the 16-draw / 340 k galaxy row |
| `galaxies2mrs` | 6 | 200,000 | 0.15 | 0 | 0.1 | same row |
| `groupsCf4` | 2 | 40,000 | 0.05 | 0 | 0 | same row |
| `sdss` | 4 | 120,000 | 0.10 | 0 | 0 | same row |
| `basins` | 1 | 10,000 | 0.03 | 0 | 0 | ribbons |
| `cmb` | 2 | 3,000 | 1.0 | 0 | 8.9 | cmb sky form |
| `cylGrids` | 1 | 4,000 | 0.02 | 0 | 0 | ribbons |

**Windows keep the totals apart.** The three worst rows never coexist: `dust` ends at −14.6 and
`cmb` starts at −21.6; `stars` ends at −19.6 where `galaxies2mrs` starts at −24.0 and
`galaxiesLocal` at −22.3. At the `neighbourhood` rung (−15.55) the live set is stars 65 + dust 1 +
constellations 2 + cepheids/globulars/masers 6 + arms/sunOrbit 2 + cylGrids 1 = **77 draws,
about 516 k vertices, 0.70 eye-screens, 30 step-coverages**. At `localvolume` (−20.84) it is
mwDisc 8 + galaxiesLocal 8 + 2MRS 6 + groupsCf4 2 + sdss 4 + basins 1 + cylGrids 1 = **30 draws,
654 k vertices, 0.65 screens, 0 steps**. Both are inside the global limits with the bodies, UI
and orbit ribbons still to come.

Ribbon draws used by this lane: constellations 2, arms 1, sunOrbit 1, basins 1, cylGrids 1,
dustExtent 1, cmb shell 1 = **8 of the 16-draw ribbon row**, leaving 8 for orbits and trails.

**`mwDisc` needs a §7.3 row.** It is not in the table. Its 8 draws and 160 k vertices are taken
from the slack in the galaxy row (16 / 340 k, of which this lane's galaxy layers use 20 draws and
480 k across four layers — over that row, see Deviations). The integrator should either add a
`mwDisc` row or fold it into the galaxy row and raise it; the arithmetic above assumes the former.

**Raymarch: 30 of the 33 step-coverages**, and `ScaleRenderer` now enforces R2 — at most one
`MayEnclose` volume survives a frame, largest weight wins, ties by layer id ascending.

---

## Stubs left for the data lanes to delete at merge

Every one carries a `STUB (<owner>)` banner naming who replaces it and what is deliberately
missing. **None of them fabricates data**: the parsers are absent on purpose, so on this branch
every file is missing, every layer reports "not in this build", draws nothing and logs once.

| File | Owner | What is missing on purpose |
|---|---|---|
| `Core/Layers/LayerTable.cs` | integrator P0.2/P0.7 | 46 rows and 19 rungs; this copy has only this lane's 16 rows and no `Rung` (it needs `PresentationPlane`, P0.3) |
| `Core/Layers/LayerContext.cs` | integrator P0.2 | nothing — the 13 fields are complete; `HasFeature` is hard-false until a manifest exists (R47) |
| `Core/Layers/LayerState.cs` | integrator P0.2 | nothing functional; replace with the real one |
| `Core/Layers/{LayerBudget,LabelCandidate}.cs` | integrator P0.2 | nothing |
| `Core/Nav/Destination.cs` | nav | nothing |
| `Core/Presentation/PresentationPlane.cs` | integrator P0.3 | `Planes.Up` and the levelling driver |
| `Core/Compat/IsExternalInit.cs` | integrator P0.2(e) | nothing — verbatim from the contract |
| `Core/Deep/PointSet.cs` | milkyway / galaxies | `Parse` (the `CSGP`/`CSPT` readers) |
| `Core/Deep/VoxelGrid.cs` | integrator | `Parse`; decode and sampling are here because the renderer must agree with them |
| `Core/Deep/DustCube.cs` | milkyway | `Parse`; the integrals are here for the same reason |
| `Core/Deep/GalaxySet.cs` | galaxies | `Parse` (`CSLV`, `CSRS`) |
| `Core/Deep/PlaceCatalog.cs` | milkyway / galaxies | `Parse(json)` |
| `Core/Cmb/CmbSky.cs` | cmb | `Parse` (`CSCB`); the face arithmetic is here and tested |
| `Core/Sky/{Constellations,StarNames}.cs` | stars | `Parse(json, …)` |
| `Runtime/Universe/DeepSkyLayers.cs` | integrator | the whole file: it stands in for the bootstrap's `BuildLayers()` slot and for `DataLoader` |

`Core/Deep/{GalaxyModel,GalaxyOrientation}.cs`, `Core/Cmb/CmbModel.cs`, `Core/Grids/DistanceGrids.cs`,
`Core/Sky/StarQuery.cs` and `Core/Scale/PointChunks.cs` are **not stubs** — they are complete and
tested, and are what the data lanes should build against.

**One number in them is unverified.** `GalaxyModel.Arms` — the seven Reid+2019 rows — was entered
from memory, not read off the paper: this lane has no network and the paper is not in the tree.
The file says so in a block comment. The `milkyway` lane must re-enter every row from ApJ 885, 131
Table 2 when it fetches the maser catalogue, and the masers are shipped, so the fit can be
re-proved against its own data rather than trusted. Until then the layer tags the arms Inferred
and the panel says "log-periodic fit", which is true whatever the coefficients turn out to be.

---

## Tests added — `tests/CosmoSynth.Core.Tests/DeepSkyTests.cs`, 38 cases

| Test | Proves | The wrong input it caught |
|---|---|---|
| chunked offsets are exact | every point rebuilds to 6e-8 of the chunk radius, and lands in exactly one chunk | — |
| the chunk ceiling is a ceiling | 16 means 16 | **caught the real bug: 29 chunks** |
| coincident points | a degenerate box is one chunk, not a stack overflow | — |
| the cone cull | ahead kept, behind dropped, eye-inside always kept | — |
| the Sun from Alpha Centauri | M_V 4.83 and V 0.40 at 1.3012 pc | — |
| magnitude round trip | absolute ↔ apparent inverse to 1e-12 with extinction | — |
| extinction applied once | one magnitude of dust moves M by exactly one (R41) | — |
| the visual response | 1 at the limit, 0 below, and 158× in flux becomes 3.2× on screen | — |
| no distance, no magnitude | the 146 sentinel rows return NaN rather than a plausible number | — |
| PA 0 north, PA 90 east | exact to 1e-12 | — |
| inclination known answers | q = 0.28 / 0.33 / 0.59 → 78.46 / 74.46 / 55.49 ± 0.01 | — |
| disc normal geometry | perpendicular to the major axis at every inclination; face-on looks down the line of sight | — |
| the face sign is stable | same name, same sign, every run | — |
| **the Galactic centre is where Sgr A\* is** | RA 266.41684, Dec −29.00781 to 0.02°, R0 8150 pc | **caught the real bug: 0.143° out** |
| the galactocentric frame is not mirrored | x × y = z exactly | a determinant −1 map |
| the Sun on its own origin | round trips both ways; derived 6.5 pc ≠ adopted 20.8 pc | — |
| the arms wind the way the Galaxy rotates | +y is anti-rotation; increasing β follows l = 90° | a mirrored spiral |
| an arm point is at its own fitted radius | through the reference point exactly; radius matches over the range | — |
| an arm is only full strength where fitted | 1 inside, 0.575 halfway out, 0.15 at the end, **0 beyond** | the Atlas's complete spirals |
| CMB byte encoding | raw 0 → −500 µK, raw 255 → +500 µK, exactly | — |
| the dipole | galactic (264.021, 48.253) to 0.01°; peak-to-peak = 2A; mean = T0; zero in the Local Group frame | — |
| the false-colour ramp | cold blue, hot red, neutral zero, clamps rather than wraps, stays in [0,1] | a wrap that invents structure |
| every cube face round-trips | all six faces × nine points; the six face centres are the six axes | a flipped face |
| an absent CMB file | flat sky, right monopole, no exception | — |
| grid legibility | false below 8 cm **and** above 3 m | a floor with no ceiling |
| the right grid for a scale | Stellar at rung 10, Galactic at rung 12, LocalGroup at rung 15; none at true scale or at the CMB | — |
| light-travel time | derived from the radius, not an independent number; R0 = 26,580 ly | — |
| grid planes | the galactic normal **is** the NGP; the two planes differ | a second spelling |
| extinction is symmetric | both directions agree to 1e-9 | — |
| a uniform cloud | 1 kpc at 0.001 mag/pc = 1.0 mag ± 0.02 | — |
| outside the box is empty | density and extinction both exactly 0 | inventing dust where no survey reached |
| an absent dust cube | no exception | — |
| the voxel decode | trilinear in **density**, and the arithmetic and geometric means really differ | interpolating log indices |
| absent data is honest | weight 0 and `NotInThisBuild`, whatever the scale | a row reading "on" and drawing nothing |
| a window fades | no step above 0.1 over 0.01 decades; 0 at both edges, 1 in the middle | a pop |
| SOLO keeps guides | a guide survives every SOLO; a Galaxies layer does not | — |
| the layer table is well formed | ids unique, windows inside the limits and wider than their own two fades, params inside range | a window that never reaches full weight |
| presentation gains | chip only away from the default; the range is enforced | — |

## Tooling change (outside this lane's globs, and why)

`tools/gates/class_gate.sh` — its type scanner knew `class|struct|interface|enum` but not
`record`, so `Core/Nav/Destination.cs` (an enum and a positional record) was reported as
declaring no type named `Destination`. CONTRACT §4.2 makes five shared types positional records,
so this would have blocked every lane that puts one in its own file. One word in one regex;
`mono_classes()` is untouched because a record cannot be a MonoBehaviour. The gate's own
`--self-test` still passes all four mutants.

---

## What the play-mode harness can check, and what needs a headset

**Checkable on the desktop, once the integrator runs G1c:**

- `layers` — every layer's `SelfCheck`. This lane added twelve: chunk counts against each layer's
  draw budget, the star population against the vertex budget, a distance-unknown count far from
  146 (which is what a parse that lost the distance column looks like), 88 figures and 695
  segments, raymarch steps over the governor's ceiling, a dust cube with no extent, two grid
  regimes drawing at once, both CMB forms fading at once, a cube face size that does not match
  the file, and a galaxy catalogue in which no row has an unknown ellipticity.
- `budget` — the table above, summed over the layers with weight > 0.
- `volumes` — at most one `CosmoSynth/Volume` renderer with `_Fade > 0`. Now enforced in
  `ScaleRenderer.DeclareLayerGroups`, so the assertion should never fire; it is the check on the
  enforcement, not the enforcement.
- `alpha` — every layer here except `cmb` declares `Additive` or `Premultiplied`. The additive
  ones must not move destination-alpha coverage when hidden with `COSMOSYNTH_HIDE`; `dust` is
  premultiplied and *should* contribute coverage (it absorbs); `cmb` writes coverage on purpose
  and only in Space mode.
- `shaders` — nothing on the error shader; every material is one of `CosmoShaders.All`.
- `far`, `push`, `group` — the layer groups now take part in the same solve as the bodies.
- `cmb-faces` — **must be run in Anisotropy mode** (`cmb.mode = 0`), or the dipole is baked into
  the texels and the comparison against `CmbSky.Sample` is meaningless.

**Only a headset settles:**

- Whether the star field reads as a sky rather than as cotton wool or as an empty field. Every
  size and brightness number here is inherited from a measurement made under **gamma**; §7.4 says
  overlapping quads now sum in linear and will read brighter. `_Look.y` (`stars.exposure`) is the
  one knob to tune first, and the pixel-neighbourhood method — not a thumbnail — is how to read it.
- Whether the dust volume's 30 step-coverages is affordable. The only device-measured anchor is
  Fractarium's 0.338 ms per step-coverage, which would make 30 steps 10 ms on an 11 ms budget.
  The number needs a cost probe on device before it is trusted (§13).
- Whether the resolved-galaxy cross-fade reads as a galaxy resolving or as a dot exploding.
- Whether the CMB sky at infinity is comfortable, and whether the Space/passthrough switch does
  what it should — the harness renders flat and monoscopic on llvmpipe, so multiview, the
  compositor and alpha coverage are exactly the things it cannot see.
- Whether 40,000 sprites read as a galaxy from outside, which is the whole point of `mwDisc`.

**Nothing here has been seen.** No capture was taken: `PlayModeShot` needs the editor.

---

## Deviations from the contract, each with its reason

| # | Deviation | Reason |
|---|---|---|
| D1 | **`galaxiesLocal` uses a sprite cloud, not "a galaxy-disc port of Point"** (§4.5) | That shader does not exist in `CosmoShaders.All`, and a layer asking for a shader that is not in the build renders magenta — the exact failure `CosmoShaders` exists to prevent. The cloud uses only shaders that exist and takes its orientation from the same published numbers. **Request to the shader lane: a Point port with an elliptical profile**, one quad per galaxy; the layer then drops to one draw and the cloud goes. |
| D2 | **The CMB object form is a wire shell, not a 1.2 m textured ball** (§4.5, §2.6) | Same reason. `CosmoSynth/Sky` is eye-anchored by construction; nothing in the set draws a cube-mapped sphere as an object. **Request: an object mode on Sky** (a `_Space`-like switch that uses `TransformObjectToWorld` instead of `GetCameraPositionWS`). The shell claims only what it can support: three great circles at 13.87 Gpc with the radius labelled. The 0.95 R / 1.05 R hysteresis of §2.6 is therefore not implemented; **its printed rule also reads inverted** against §5.4's windows (object form at the *smaller* U, where the eye is outside), which the integrator should settle before the real object form lands. |
| D3 | **`CosmoSynth/Sky` has no `_Fade`**, so the CMB layer writes its window fade into `_Immersion` | R30 says every shader declares `_Fade` and that "the CMB layer writes both". Measured on this tree: **seven of the eleven lack it** — Body, CosmoAtmosphere, CosmoRim, Panel, Sky, Text and Unlit; only Point, Ribbon, Ring and Volume have it, and all eleven have a vertex stage. **G0b passes anyway**, so §4.3's rule ("every `CosmoSynth/*` shader with a `#pragma vertex` must declare `_Fade`") is not implemented in `stereo_gate.sh` — a gate that cannot fail on the thing it is named for. Folding the fade into `_Immersion` is correct today because both are zero in passthrough. **Requests: add `_Fade` to Sky (and the other six), and add the R30 rule to G0b**; the CMB layer then writes both, one line. |
| D4 | **`StarFieldLayer.DataKinds` (`stars`, `stars_named`) ≠ its `LayerDef.DataKinds` (`stars`)** | §4.1 says they are equal. If they were, the star field would not draw at all until the names file shipped, because `LayerState.Available` requires every kind. Names are a decoration. The delivery list is the superset; the availability list is what the layer genuinely needs. `constellations` is the opposite case and its LayerDef row *was* widened to include `stars`, because its segments are indices into the catalogue. |
| D5 | **`cylGrids` has one window (−21.6 … −15.0), not three** | §5.4 gives a window per regime. The regime is chosen inside the layer from which has the most legible rings, so three windows would decide the same thing twice and could disagree. The union is the window; `DistanceGrids.Best` is the choice. |
| D6 | **`dustExtent` has no `LayerDef` row of its own**; it rides `dust`'s weight and is gated on `State.IsOn("dustExtent")` | The outline is meaningless without the volume it bounds, and the layer that owns the box is the one that can place it. A row can be added at integration with no code change. |
| D7 | **`mwBand`, `nebulae`, `cloudMarkers`, `density`, `scaleRings`, `planeGrid`, `sgGrid`, `eclipticGrid`, `horizonGrid`, `census`, `quasars` are not built** | Outside the seven deliverables. `nebulae` in particular is the *other* `MayEnclose` volume, and the arbitration that R2 needs is implemented and tested by `dust` alone; when `nebulae` lands it declares `MayEnclose` and the rule already holds. |
| D8 | **The dust voxel-sprite LOD (§7.3, 8 draws / 295 k vertices) is not built** | The raymarch covers every scale in the window at 16–64 steps. The LOD is a performance optimisation for when the box is under 10 % of the screen, and optimising against an unmeasured cost is how the 30-step budget became the open question it is. |
| D9 | **`ScaleRenderer`, `FrameContext` and `CosmoSynthBootstrap` were edited** — outside this lane | §4.3's per-frame order *is* `ScaleRenderer`; without it no layer is ever asked for its weight. `FrameContext.LayerContext` is the one field the foundation report's D10 says is missing. The bootstrap gained one line creating the layer host under the universe host, which is the `BuildLayers()` slot. All three are small, marked, and are what the integrator would have written. |
| D10 | **`DeepSkyLayers` stands in for `DataLoader` and reads files by hard-coded path, not from `manifest.json`** | The manifest and the loader are integrator-owned and not written. This reads through `UniverseSimulation.ReadStreamingAssetBytes` (the StreamingAssets-is-not-a-directory path) and delivers nothing when a file is absent, which is the behaviour the real loader must have. It logs 13 warnings once at startup, not per frame. |
| D11 | **`cmb.frame` and `cmb.palette` are not exposed as params** | The mode and contrast are; the frame and palette need the panel's enum control, which is the `ui` lane's. `CmbModel` takes both as arguments already, so exposing them is a `LayerParam` row. |
| D12 | **The galaxy layers use 20 draws / 480 k vertices against §7.3's 16 / 340 k row** | That row was sized for "about 84 k" galaxies; the `sdss` subsample and the resolved clouds are on top of it. The windows keep `galaxiesLocal` (ends −17.8) and `sdss` (starts −24.7) from peaking together, so the worst simultaneous case computed above is 30 draws and 654 k vertices — inside the global limits. The row still wants restating at integration. |

## Requests to the integrator

1. Raise `tools/gates/test-floor.txt` from 142 to **212** with this merge.
2. Generate `.meta` files for the 42 new `.cs` files (lanes never create them).
3. Three shader requests: D1 (a galaxy-disc Point port), D2 (an object mode on Sky), D3 (`_Fade` on the seven shaders that lack it) — and the R30 rule added to `stereo_gate.sh`, which cannot currently fail on it.
4. Add a §7.3 row for `mwDisc`, and restate the galaxy row (D12).
5. Settle the §2.6 CMB hysteresis direction (D2) before the object form is built.
6. `FrameTests`' Sgr A* comment mislabels galactic (0, 0) as the radio source; the number is fine.

## Open questions

- Is 30 step-coverages affordable for `dust` on a Quest 3? Nothing here can answer it; a
  Fractarium-style cost probe on device can.
- Should `mwDisc` and `galaxiesLocal`'s Milky Way row ever be visible at the same scale? Today the
  catalogue row is hidden outright, which is right at `localvolume` and arguable at `halo`.
- The observed-sky vantage is the viewer's eye (which is what the Atlas did). An explicit "from
  the Sun" or "from the selected star" vantage is a CPU rebake of the magnitude stream; worth it
  only if the user asks for it.
