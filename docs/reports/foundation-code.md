# Foundation code — report

Status: ready for review
Branch: `lane/foundation-code`
Worktree: `~/projects/CosmoSynth-wt/foundation-code`
Authority: `docs/CONTRACT.md` §2–§4, §8, §10.5–§10.6; `docs/design/2026-09-11-A-foundation.md`; `CLAUDE.md`

## Summary

The app has not rendered correctly on a headset since 1.7.0 and has never been tested since. This
lane removes the two structural causes of that class of failure and replaces the machinery that
was covering for them.

**Gone:** the second camera; the depth-band compositor; the sky-horizon pinning that stood in for
a working depth story; the rebase machinery; the identity "conversion" between a right-handed
frame and a left-handed one; three catalogue adoptions at startup; two hand-written axis helpers
that disagreed with a third; two disagreeing scale clamps; one deliberately wrong galactic
rotation.

**In their place:** one camera with two constant clip planes, one distance map with proofs, one
group solver, one axis mapping, one catalogue adoption, one scale clamp, and 32 new headless tests
that each fail on a planted defect.

```
./run-tests.sh          174 passed, 0 failed   (was 142)
tools/gate-all.sh       G0a PASS  G0d PASS  G0e PASS
                        G0b FAIL  — shader lane's; Runtime/Shaders/** is not this lane's
                        G0c FAIL  — needs the editor run (G1a), see "left for the build stage"
```

---

## 1. Scaled space in Core

**`Core/Scale/ScaledSpace.cs`** (new, 400 lines) is now the only account of distance in the
codebase. `Compress`, `Decompress`, `GroupScale`, `DepthQuantum`, `CompressRelative`, and `Solve`,
which assigns a mode and a scale to every group and then pushes overlapping groups apart.

`Core/Scale/ScaleSpace.cs` lost the band stack in the same change — `ScaleBand`, `DefaultBands`,
`SelectBand`, `ShouldRebase`, `Rebased` and the comments describing a multi-pass compositor over a
reversed floating-point depth buffer the device does not have. Two accounts of distance is how one
of them rots.

`Core/Presentation/ScaleLimits.cs` (new) holds the one clamp, `[2e-25, 1e3]`, applied inside
`FloatingOrigin.SetUnitsPerKm` so every writer goes through it whether it knows about the type or
not.

### What `tests/CosmoSynth.Core.Tests/ScaledSpaceTests.cs` proves (19 tests)

| Test | Proves |
|---|---|
| identity inside d0 | `f(d) == d` **bit-exactly** for d ≤ 10 m: the room, the hologram and the wrist are untouched, not "approximately untouched" |
| known answers | the 13-row §2.3 table to 1e-12 relative |
| C-1 at the join | one-sided difference quotients either side of d0 are 1 to 1e-6 (which is the truncation error of the method, not a chosen bound) — so walking across 10 m shows no crease |
| monotonic and bounded | f strictly increases on 2000 log-spaced points over 1e-3…1e30, by exact comparison. This is what guarantees depth order along any ray. And f(1e30)+H < half the far plane |
| inverse | `f⁻¹(f(d)) == d` to 1e-12 on the same grid |
| **angular size invariance** | for 1000 seeded (D, R) pairs, `asin(sR/sD) == asin(R/D)` to 1e-12 rad — the similarity cannot change what the viewer sees |
| CompressRelative | direction exact (angle 0), magnitude `f(\|v\|)`, and vectors inside d0 returned unchanged |
| depth quantum | the UNORM form, and that D16 is exactly 256× coarser than D24 |
| **depth separability** | across 1 m…1e27 m, `dz(f)/f < 8.0e-4` everywhere — two surfaces in one rigid group separated by more than 0.08 % of their distance are ordered correctly at every scale. Paired with a control showing the same bound fails by 3 orders on an uncompressed scene, so the test cannot pass vacuously |
| T1–T4, T8, T9 | the mode cut: near leaf exact, far leaf rigid, a close big body becomes Envelope (not a 138 m sphere in your face), a system owns its subtree far away and dissolves as you approach, an enclosing volume, and the 9/11 m hysteresis in both directions |
| **T5 the transit trap** | a moon at 850 m in front of a planet at 1000 m renders entirely in front of it — *and* the counterexample is asserted: per-body placement puts the moon's far side 4 m **behind** the planet's near face, which is the bug the whole per-group design exists to prevent |
| T6 push | two overlapping groups end up ordered with at least 2 depth quanta between them, at the exact scale the design predicts (0.050992508553), **and** the pushed group's angular radius is unchanged to 1e-12 |
| T7 floor | a push that would drag a group inside 5 m is refused, clamped to exactly 5 m, and **counted** — the conflict count is the honest report, not a silent clamp |
| room content | a hologram at 0.7 m and room content at 4 m are never pushed, whatever is behind them |

### What it cannot prove

Nothing about stereo, the compositor, tile memory, MSAA resolve or foveation — the arithmetic says
depth *values* differ; only a headset says the *pixels* do. Nothing about whether 1–9 px of far
disparity is comfortable (G5 H4), and nothing about the device's actual depth format: `DepthBits`
is 24 by assumption and the `render:` line prints what the device reports so H2 can settle it.
Nothing about performance.

**Two arithmetic corrections to the design documents**, both found by computing rather than
copying:

1. §2.7's depth-quantum row prints `DepthQuantum(600.462318658) = 0.429823`. The correct value is
   **0.429815049324204** (= 600.462318658² / 838860.8), 3.4e-5 relative away. The formula in the
   document is right; the printed value is a typo. The test pins the computed value and says so.
2. §2.4 prints `dz(1 m) = 1.2 um`. It is 20/2²⁴ = **1.1920928955078125e-6**, the same number to two
   significant figures and 0.7 % from the printed one.

---

## 2. One camera

`Runtime/Rendering/DepthBands.cs` (157 lines) and every reference are **deleted**: `XrRig`,
`ScaleRenderer`, `PlayModeShot` (four sites) and `RenderDiagnostics`.

`XrRig` builds one camera, clearing `SolidColor`, near `ScaledSpace.NearPlaneMetres` (0.05) and far
`ScaledSpace.FarPlaneMetres` (2000) — two constants, set once, never re-fitted per frame. URP camera
data is configured explicitly: base render type, no post-processing, no camera AA (MSAA comes from
the URP asset), no depth texture, no opaque texture, no shadows. The dead hand anchors and the
built-in ambient light are gone. `PassthroughClearColour` / `SpaceClearColour` are public statics
the gates read.

`Runtime/Rendering/ScaleRenderer.cs` is rewritten as the order-50 step: build `FrameContext`, publish
the shader globals, build one `GroupNode` per body in catalogue order, `ScaledSpace.Solve`, then
`view.Place(ctx, placement)`. It exposes `RigidGroups` / `EnvelopeGroups` / `EnclosingGroups` /
`PushConflicts` / `FarthestRenderedMetres` for the render line and the play-mode gate.

Two new files carry the API nothing may now bypass:

- **`Runtime/Universe/FrameContext.cs`** — the centre eye in three spaces, computed once per frame.
  Components used to work it out for themselves, and some crossed the universe host's transform
  while others did not; at tabletop scale that is thousands of kilometres of apparent distance,
  which drops every body to a point and blanks the scene while passthrough carries on working.
- **`Runtime/Rendering/Placement.cs`** — `PlaceRigid` (the only code that turns a scale into a
  transform) and `PublishGlobals` (`_CS_CentreEyeWS`, `_CS_Compress`, `_CS_IcrfToWorld`, set by
  `ScaleRenderer` and nobody else, so CPU and GPU cannot disagree about where the eye is).

`CelestialBodyView` splits into `UpdateState(time)` (state only, no transform writes) and
`Place(ctx, placement)`. `PlaceForRendering`, the sky horizon and
`UniverseSimulation.StarPointHorizonUnits` are deleted.

**The 2.5 px floor no longer inflates.** `DrawnAsStarPoint` is renamed `DrawnAsPoint` and now means
what the contract says: below 2.5 px of *true* apparent diameter the sphere renderer is switched
off and the additive point path draws instead. An inflated sphere writes depth at a size the body
does not have, so a floored moon intersects its own planet and the intra-group ordering everything
else rests on stops being true. The point path now covers **every** body, not only stars — a planet
below the floor gets a dot in its own catalogue colour at exactly the angular size the floor asked
for, so "correctly far away" is still distinguishable from "missing".

`ApparentMagnitude` now measures from the eye rather than from the solar system barycentre, which
is what its own doc comment has always claimed.

---

## 3. Axis unification

`ScaleSpace.RenderTransform` gains `AxisSwap` (P, det −1, its own inverse), `MapDirection`,
`UnmapDirection` and `MapRotation` (P·M·P, det +1). `FloatingOrigin` becomes a thin Unity wrapper
over it and is the only file that may reorder an axis — gate G0d enforces it and now passes.

Deleted: `CelestialBodyView.ToUnityRotation` / `ToUnityDirection` / `ToIcrfDirection` (a second
coordinate convention living in a renderer), the swap in `SkyAndRings.cs:220`, and the ad-hoc calls
in `XrLocomotion` and `UniverseNavigator`. `UniverseNavigator.ApplyToTransform` also now goes
through `universe.TransformPoint`, because `ToRender` answers in the host's local space and
assigning that to a world position ignores the anchor entirely.

`FloatingOrigin`'s rebase machinery is deleted with it: it was dead (the bootstrap set `Focus` to
null), and what it left behind was an **identity** mapping between a right-handed frame and a
left-handed one, which is a mirror. `OrbitLineRenderer` loses `IFloatingOriginListener`.

### What `tests/CosmoSynth.Core.Tests/FrameTests.cs` proves (13 tests)

- **the galactic matrix**, element by element against the Hipparcos matrix at 1e-9 (the construction
  achieves 4.9e-11; the bound is 20× that, set by the pole angles being published to five decimals).
  The shipped code was out by up to **1.75 per element** and put the north galactic pole at
  (−0.346, 0.176, 0.922) — 45° from where it belongs — while still producing a plausible band of
  stars across a plausible sky. It had no test at all.
- NGP → (0,0,1) exactly; galactic longitude zero → RA 266.404995, Dec −28.936174 (Sgr A*);
  Sirius, M31 and the CMB dipole at their catalogued coordinates to 1e-4 deg.
- **supergalactic**, built from its two defining directions rather than copied, matching the
  published matrix to 1e-9 and its own pole exactly; M31 and M87 at their catalogued SGL/SGB.
- the four plane-pole angles the levelling animation has to cover (60.19 / 83.68 / 51.66 / 23.44 deg).
- **the axis mapping**: det = −1 (a determinant of +1 would be a rotation, and no rotation converts
  handedness — the identity that used to stand there has det +1); P·P = I; ICRF +z → universe-local
  +y, which is *which axis is celestial north*; ICRF +x → +x.
- **a known body direction**: the ecliptic pole sits 23.4392911° from universe-local up, leans
  toward −z and has no x component. A mapping that mirrored or transposed an axis would still give
  a unit vector and still give 23.44° from *some* axis, so the test names which one.
- rotations and positions agree: the mapped body pole lands exactly where `MapDirection` says a
  direction lands. This is the invariant that silently broke when two hand-written helpers did the
  two jobs.
- the absolute↔universe round trip to 1e-15 of |x|, with the y/z swap asserted component by
  component.

**What it cannot prove:** that the universe host's presentation-plane rotation is applied correctly
(not built in this lane), and that the scene looks right — a consistent mapping can still be
consistently pointed the wrong way, which is what the `mapping` and `plane` play-mode assertions and
G5 are for.

**Note for whoever wrote CONTRACT §3.2 / C5:** the proposed replacement is correct and the shipped
code was wrong, but only under this codebase's `Matrix3d` convention, where `RotationZ(a)` is an
*active* rotation of a vector. Verified numerically both ways before changing anything, because
under the transposed (frame-rotation) convention the two constructions swap places exactly.

---

## 4. Catalogue once, and the host star

**`Core/Catalog/CatalogAssembler.cs`** (new) merges the catalogue JSON documents in Core, in order,
skipping any that are missing or malformed and logging what it did. The bootstrap reads the files,
hands them to the assembler, and calls `Adopt(...)` — **one** `SetCatalog` call site in the whole
runtime, which is what gate G0d now measures (1, down from 5).

`UniverseSimulation` loses `LoadCatalogFromStreamingAssets`, `LoadCatalogFromJson` and
`MergeCatalogFromJson`; `SetCatalog` becomes `void` and counts its adoptions in
`CatalogAdoptions`, which the play-mode `catalog` assertion requires to be 1. A new `BodiesChanged`
event fires from `AddBodies` / `RemoveBodies` and deliberately **not** from `SetCatalog`.

**`CelestialCatalog.HostStarOf(id)`** walks the parent chain to the first star, at most 32 hops,
null for a rogue body. `SkyAndRings.BuildStarLight` now uses it instead of `RootIds[0]` — which is
insertion order, so the moment the exoplanet catalogue merged in, the single point light modelling
the entire Solar System was attached to 61 Vir, eleven parsecs away, and every planet went dark
while the build reported success.

---

## 5. The decoration rule

Every component that decorates, picks or measures a body now consults
`CelestialBodyView.IsGeometryDrawn` (or `IsPointable`) **every frame**, and gate G0d's
"every `RenderedRadius` reader also references `IsGeometryDrawn`" now passes:

| File | What it does now |
|---|---|
| `RingSystem` | disables its renderer and returns when the planet is not drawn; `[DefaultExecutionOrder(60)]` |
| `SpatialPanel` | hides itself when its target is not pointable; order 60 |
| `BodyLabelLayer` | skips a candidate that stopped being drawn between selection and placement |
| `AnchorBeacon` | only takes the focus body's radius into the clearance while its geometry is drawn |
| `ScaleRenderer` | measures the far plane against drawn extents only |
| `AtmosphereShell`, `OrbitLineRenderer` | order 60, so they read placed transforms rather than last frame's |

---

## 6. TabletopManipulator

- Hand poses are converted to universe-local space **once**, at the top of `UpdateHands`, rather
  than at each of the four `origin.ToAbsolute` call sites — which is how one of them gets missed,
  and a grab that is correct with the universe unrotated and drifts as soon as it is turned is
  exactly what that looks like.
- `_targetUnitsPerKm` is cleared when the scale is reached (within 1e-3 of a decade) and 0.5 s
  after the last hand releases. It was never cleared, and the exact-equality test that was supposed
  to clear it could never fire because log-space smoothing approaches asymptotically — so every
  gesture left a standing instruction dragging every later rung tap back toward it.
- `_minimumUnitsPerKm` / `_maximumUnitsPerKm` are deleted in favour of `ScaleLimits`.

---

## 7. XrSetup

Through the XR Management / OpenXR / URP APIs, never by editing YAML:

- depth submission **None** (was Depth16Bit — 256× coarser than the separability arithmetic
  assumes, and it forces the depth attachment to be stored rather than staying memoryless)
- symmetric projection **on**, latency **PrioritizeInputPolling**, optimize buffer discards **on**,
  foveation API **SRPFoveation**, render mode **SinglePassInstanced**
- `MetaQuestFeature`'s own internal copies of `m_symmetricProjection` and
  `m_optimizeBufferDiscards` mirrored through `SerializedObject`, because Meta's build hook wins
  if they disagree
- `com.unity.openxr.feature.input.handtracking` (the deprecated id that silently enables
  Microsoft's interaction profile) **disabled**; `...foveatedrendering` moved from the disabled list
  to the required one, since the error that put it there only exists on Built-in
- `EnsureColorSpace` → Linear; `EnsureFramePacing` → Swappy off
- `EnsureUrp` → **`Editor/CosmoUrpConfig.cs`** (new), ported from Worldfall's headset-proven
  `WorldfallUrpConfig.Create()`: creates `CS_URP.asset` + `CS_Renderer.asset`, sets every field
  through `SerializedObject` with names read off a real URP 17.5 asset, assigns to every quality
  level and to `GraphicsSettings.defaultRenderPipeline`, then **re-imports and reads back**,
  throwing on any value that did not stick
- `EnsureLoader` creates a manager only when there is not one (calling it unconditionally is what
  produced five "Android Providers" managers, four of them orphans), and `RemoveOrphanManagers`
  deletes every `XRManagerSettings` sub-asset nothing references
- `DeleteOrphanXrAssets` deletes `Assets/XR N` and `Assets/XR/Settings N`
- one `[XrSetup] SUMMARY ...` line carrying every value, and `XRSETUP_OK` on success

---

## 8. ServerBuild

Version `2.0.0-alpha.1`, versionCode 17. Android build target group pinned in both entry points
(every API below is per-target, and a batchmode editor that has not switched answers for whatever
platform was last active). Output is `COSMOSYNTH_OUTDIR`, defaulting to
`/home/triton/.cache/cosmosynth/staging` — never `~/builds`. Environment-variable signing
(`COSMOSYNTH_KEYSTORE` and friends) falling back to the debug keystore, which is what 1.9.0 was
signed with, so 2.0 installs over it and the A/B is possible. `EnsureScene` is replaced by
`RequireScene`, which fails naming `SceneBuilder` rather than generating a different scene from the
one that was reviewed.

**`Diagnostics()` asserts.** It prints the Unity version, product and identity, SDK/arch/backend/
APIs/colour space, quality level and pipeline, every URP field, every OpenXR render setting, the
enabled OpenXR feature list, the `MetaQuestFeature` count, `CosmoShaders.All`, the always-included
shader names, the scene, and the scaled-space constants — then emits `DIAGNOSTICS_FAIL <what>` for
each deviation from binding decisions 1 and 2 and exits 1, or `DIAGNOSTICS_OK` and exits 0. G0c
reads the YAML and this reads the API: two independent sources that agree only when both are right.

---

## 9. PlayModeShot

- `-shotViews` is a comma list captured in one play session after one settle. `release` expands to
  `default,wide,mode:vr` plus the **seven scales of §2.6**, held in the harness as a table with the
  design's own captions so the capture set cannot drift from what the foundation asks for.
- `-shotCommit`, `-shotDirty`, `-shotProfile`, `-shotHide` are read and the first report line is
  `commit <sha> dirty <0|1> unity <ver> profile <p> views <n>`.
- Output is a **directory**: `<dir>/<view>.png` + `<view>.report.txt`, which is what `shot.sh`
  already passes.
- New view verbs: `scale:<caption>` (set through `FloatingOrigin`, so a capture cannot photograph a
  scale the app would refuse) and `mode:vr`.
- **Assertions are fatal.** Each prints `ASSERT_FAIL <id>: <detail>` and any failure exits 4;
  success prints `PLAYSHOT_OK`. Implemented: `camera` (exactly one enabled camera, no
  targetTexture of its own, SolidColor, the passthrough clear's alpha is 0, the clear is one of the
  two the rig knows, near/far are the `ScaledSpace` constants — checked *before* the harness
  assigns its own target), `pipeline` (URP + Linear), `catalog` (adopted once, one view per body),
  `far` (nothing drawn past 0.9 × far, naming the offender), `push` (zero conflicts), `angular`
  (the largest drawn disc subtends the same angle drawn as in truth, to 1e-3 relative), `shaders`
  (nothing on `Hidden/InternalErrorShader`).
- The depth-band compositing, the far-band compositing and the `Unused()` stub are gone;
  `ProbeDestinationAlpha` and `COSMOSYNTH_HIDE` are kept.

**What the harness cannot prove** (unchanged, and worth restating because this is the gap 1.7.0
fell through): it renders flat and monoscopic on llvmpipe. Multiview, Vulkan, MSAA resolve,
foveation, tile memory, the compositor, hand tracking, performance — and therefore the 1.7.0 bug
class itself — are only settled by G5.

---

## 10. Provenance.cs / Json.cs

**The files are renamed, not the types:** `Provenance.cs` → `ProvenanceRecord.cs`,
`Json.cs` → `JsonValue.cs`, `.meta` moved with each so the asset guid is preserved.

The types are the contract — `ProvenanceClass`, `FieldProvenance`, `ProvenanceRecord` and
`JsonValue` are named in CONTRACT §0's verified-API list and used across the codebase and the
importers — so renaming one to match its filename would touch every call site to satisfy a rule
about filenames. The filename is the free variable, and `git mv` is the whole change. G0e passes.

---

## 11. Deviations from the design documents, and why

| # | Deviation | Reason |
|---|---|---|
| D1 | **Version is `2.0.0-alpha.1`, not `2.0.0`** as the lane brief said | CONTRACT §8.1 item 2 and §0.4 both say `2.0.0-alpha.1` / 17, and CONTRACT is authority 1. versionCode 17 is the same either way. One constant to change if the orchestrator meant it. |
| D2 | **Exoplanets still load** (CONTRACT C10 deletes the merge and the file at P0.4d) | Deleting `SA/catalog/exoplanets.json` is StreamingAssets, which this lane was told to leave alone, and `ShippedCatalogTests`/`ExoplanetCatalogTests` assert on it. The merge is now a `_loadExoplanets` flag feeding `CatalogAssembler`, so C10 is a one-line change in the bootstrap plus the file deletion, by whoever owns the data half. The `RootIds[0]` defect it was entangled with is fixed independently by `HostStarOf`. |
| D3 | **`m_MainLightRenderingMode` and `m_AdditionalLightsRenderingMode` are 1 (PerPixel), not 0** as CONTRACT §8.1 lists | Those zeros are correct *after* `CosmoSynth/Body` carries `_LightDirWS` — which is the shader lane's and has not landed. Setting them now ships an alpha of black spheres. Neither is checked by `settings_gate.py`. Marked in `CosmoUrpConfig.AssetExpect` with "FLIP BOTH TO 0 in the same commit that lands CosmoSynth/Body". |
| D4 | **`SkyAndRings.BuildStarLight` is kept** (CONTRACT §8.2 item 12 deletes it) | Same reason: it is the only thing lighting the scene until the body shader exists. The defect inside it — `RootIds[0]` — is fixed. Deleting it belongs to the commit that lands `_LightDirWS`. |
| D5 | **`BodyMarkers` not written**; the point path is still `StarPointBillboard` | CONTRACT §8.2 item 18 replaces up to 240 draws with 1. That is a performance change, not a correctness one, and it is coupled to `CosmoSynth/Point`. The floor behaviour it exists to serve is implemented; only the batching is deferred. |
| D6 | **Orbit ribbons, `CosmoLayer`, `LayerTable`, `LayerContext`, `DataLoader`, `PresentationPlane`, `PointChunks`** not written | Not in this lane's brief; §8.2 items 17 and 19–20 and §4 belong to P0.2/P0.7 and the 1b lanes. `OrbitLineRenderer` keeps its `LineRenderer` and loses only the dead listener. |
| D7 | **`ProvenanceHud` kept** (CONTRACT C13 deletes it at P0.4d) | It is UI, coupled to the status-card replacement that C13 pairs it with, and not in this lane's brief. It runs IMGUI every frame and should still go. |
| D8 | **Group radius accumulates bottom-up** rather than as a flat max over descendants | Identical for a two-level system (which is every test row and every shipped case); deeper than that it is an upper bound, so a group dissolves slightly *earlier* than it strictly must — the conservative direction. O(n) instead of O(n·depth). Documented at the call site. |
| D9 | **Rings and atmospheres are not in `LeafRadiusKm`** (CONTRACT §2.7 says "incl. rings/atmosphere") | Neither writes depth (queues 3000 and 3100, `ZWrite Off`), so neither can take part in the occlusion ordering the radius exists to drive. Including them would dissolve Saturn's system 2.3× sooner for no benefit. |
| D10 | **`FrameContext` has no `LayerContext`** | `Core/Layers/**` does not exist yet (P0.2). One field to add. |
| D11 | **Files outside the stated owned globs were touched**: `Core/Catalog/{CelestialCatalog,CatalogAssembler,ProvenanceRecord}.cs`, `Core/Serialization/JsonValue.cs`, `Core/Presentation/ScaleLimits.cs`, `tests/CosmoSynth.Core.Tests/CoreSystemsTests.cs`, `tests/CosmoSynth.Runtime.Compile/*.csproj` | Each is required by an explicit DO item: `HostStarOf` and the assembler by "catalogue once", the two renames by DO 10, `ScaleLimits` by the single clamp, `CoreSystemsTests` because it tested the deleted band stack and the deleted rebase (a test may only be deleted when the thing it tested no longer exists — it is replaced there with a mapping test), and the csproj because `XrRig` now needs URP. |
| D12 | **`tools/gates/test-floor.txt` is not raised** | Integrator-owned. It should go 142 → **174** with this merge. |

---

## 12. New tooling: `tests/CosmoSynth.Editor.Compile/`

The tooling report's own "cannot prove" column said G0a covers "nothing about ... the editor layer
(no `CosmoSynth.Editor.Compile` project exists yet)". `run-tests.sh` already looks for exactly this
path and builds it when present, so the gap was one csproj wide — and it is the most expensive gap
in the project, because the editor layer holds every XR, URP and build setting, has the most
version-sensitive API surface here, and the only way to find a misremembered name was a
five-minute batchmode launch behind a global lock.

It compiles `Core/**`, `Runtime/**` and `Editor/**` against the editor's own module assemblies.
`./run-tests.sh` now prints `Unity editor layer: compiles`. It proves no behaviour — what it
catches is a method that moved, a signature that changed, an enum member that does not exist.

**One implementation note for whoever maintains it:** it references the `UnityEditor.*Module.dll`
set under `Editor/Data/Managed/UnityEngine/`, **not** `Managed/UnityEditor.dll`. In this Unity
version the monolithic `UnityEditor.dll` and `UnityEngine.dll` are whole-assembly duplicates of the
module DLLs rather than type-forwarders, and mixing the two produces several hundred CS0433s.

The csproj is `tools/`-adjacent and formally integrator-owned; delete it if that matters more than
the check.

---

## 13. Proof that the new tests can fail

`/tmp/mutants.sh`, run against this tree. Each mutant is a defect this project has actually had, or
the one the design exists to prevent. Baseline 174 passed; restored 174 passed.

```
mutant: per-body compression instead of per-group (the transit trap)
  FAIL T4: a planetary system owns its subtree far away and dissolves as you approach
  FAIL T5: a transiting moon stays in front of its planet, which per-body placement breaks
mutant: the axis mapping back to the identity (the mirrored solar system)
  6 failures, incl. the handedness test and the known body direction
mutant: the shipped galactic signs (NGP 45 degrees out)
  7 failures, incl. the Hipparcos comparison and the supergalactic vectors
mutant: a scale clamp that does not clamp
  FAIL the scale ladder has exactly one clamp and it is the published pair
mutant: a push that ignores the near-point floor
  FAIL T7: a push is refused rather than dragging a group into the room, and says so
```

No gate script was changed, so no gate self-test needed re-planting.

---

## 14. Left for the build stage

1. **G0c needs the editor run.** Every remaining failure (pipeline unset, gamma, depth submission 1,
   latency 0, symmetric projection 0, hand tracking on, foveation feature off, five "Android
   Providers") is written by `XrSetup`, which this lane cannot run: exactly one editor, behind the
   `flock`, in main. Run `tools/editor-method.sh CosmoSynth.EditorTools.XrSetup.Run` after merge and
   G0c should go green in the same commit that captures the resulting YAML. The duplicate
   `Assets/XR N` directories exist only in main's working tree (git tracks their `.meta` files but
   not the empty folders), so the deletion also only lands from there.
2. **G0b is the shader lane's.** `CosmoAtmosphere.shader` and `CosmoRim.shader` still have no
   `"RenderPipeline"="UniversalPipeline"` tag. Until the eleven `CosmoSynth/*` shaders land, the app
   renders magenta under URP — expected, and the `shaders` play-mode assertion will say so out loud.
3. **`.meta` files.** Six new `.cs` files have none, per the ownership rule that lanes never create
   them. The integrator's editor generates them at merge time.
4. **Raise `test-floor.txt` to 174.**
5. **Never-run-here:** `G1a` (XrSetup), `G1b` (Diagnostics), `G1c` (the play-mode assertions). The
   editor code compiles and the assertions are written, but none of them has executed. Per
   CONTRACT §11.3, a new G1c assertion should land with a run against a deliberately broken input
   before it is relied on: `COSMOSYNTH_FORCE_SECOND_CAMERA=1` for `camera`, a debug additive shader
   writing alpha for `alpha`. Neither hook exists yet.
6. **The two deferred flips**, both one line: URP main/additional light rendering modes to 0, and
   `SkyAndRings.BuildStarLight` deleted — together with the commit that lands `CosmoSynth/Body`
   carrying `_LightDirWS`.
