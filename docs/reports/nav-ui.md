# Lane nav-ui — report

Status: ready for review
Branch: `lane/nav-ui` (from `lane/foundation-code` 6f19d5d, merged `lane/foundation-shaders` 6e0e3be)
Worktree: `~/projects/CosmoSynth-wt/nav-ui`
Authority: `docs/CONTRACT.md` §4, §5.1–§5.10, §7.3, Appendix E; `CLAUDE.md`

## Summary

The viewer can now reach everything the deep-sky lanes will draw, and read what they are looking
at, without a controller.

- **The ladder goes to the CMB.** Nineteen named rungs, one shared clamp, continuous stick and
  two-hand zoom between them, a pivot rule, and a readout that says what one room metre is worth.
- **One `Travel`.** Turn first, then stream the world past, with the scale dipping when both ends
  cannot share the room; the arrival marker appears **on arrival**; any grab, stick or tap cancels.
- **Follow and Free** beside it, all three states of one writer.
- **Time in the headset**: the signed 28-step ladder, Now, date steppers, key dates, reverse warp.
- **A panel that works with hands**: fingertip touch with the Atlas's measured 6 mm / 20 mm
  hysteresis, controller ray, pin and recall, five tabs generated from the tables.
- **Layer control**: per-layer visibility, SOLO, label categories, per-layer sliders.
- **A measure tool** over anything the layers expose, in the right unit for the scale.
- **Honesty where the viewer is**: the chips, the on-universe badge, THIS VIEW and a credits page.
- **An inspector** with the now numbers and per-field provenance.

```
./run-tests.sh          199 passed, 0 failed   (foundation: 174; +25 from this lane)
tools/gate-all.sh       G0a PASS  G0b PASS  G0d PASS  G0e PASS
                        G0c FAIL  — editor-only settings YAML, unchanged from the foundation's
                                    own "left for the build stage"; this lane touches no settings
                        G0f/G0g/G0h ABSENT — those gate scripts do not exist yet (tooling lane)
```

`G0g`'s check was run by hand instead (`/tmp/navui/fix3.py`): all **52** `Binding` rows parse under
the contract's exact line grammar and every `(Control, Device, Context)` triple is unique.

---

## 1. The scale ladder to the CMB

`Core/Layers/LayerTable.Rungs` holds the nineteen rows of §5.1 — id, caption, group, framing centre,
room size, `UnitsPerKm`, plane, follow-on-arrival. `RungTests` recomputes every scale from the
documented span and room size rather than copying it, so a transcription slip fails rather than
ships. The two true-scale vantages (`sky`, `leo`) are asserted at exactly 1e3.

**One limit, and the Atlas bug that is not reproduced.** The owner's WebXR Milky Way Atlas lets the
slider set `log10 s` from **-7.3 to +5.7** (`refs/webxr/nebula-gallery/index.html:88`) while its
two-hand gesture clamps the same quantity to `[10^-7.3, 10^0.5]` (`:1474`) — the tops are **5.2
decades apart**, so the universe has two different maximum sizes depending which control you touched
last. Here the only clamp is `ScaleLimits`, applied inside `FloatingOrigin.SetUnitsPerKm`, so a
writer that has never heard of the type still obeys it. `ScaleControl` adds no clamp of its own; it
asks `ScaleLimits.AtLimit` which wall it hit and flashes the readout.

Inputs, all through that one path: the dominant stick at 0.6 decades/s with a 0.12 deadzone and a
1.5-power response, times the trigger boost (x1–x4); the mouse wheel at 0.1 decade per notch; the
existing two-hand gesture (untouched, and already clamped by the foundation); rung taps, which are
travels. Flight and zoom accelerate over 0.25 s and stop over 0.15 s (§5.7.5 rule 5).

**Pivot** (`ScaleControl.ZoomPivot`): the followed object, else the selection if it is drawn, else
the anchor; `ZoomPivotNear` adds the hands'-midpoint case for the two-hand gesture. The pivot's
absolute km position is held fixed in the room across the scale change.

## 2. Travel

`Core/Nav/TravelPlan.cs` is the profile, engine-free and tested; `Runtime/Nav/TravelController.cs`
plays it and is the only writer of `FloatingOrigin.OriginKm`, `UnitsPerKm`, the anchor yaw and the
plane. `UniverseNavigator.TravelTo`, `CancelTravel`, `TravelTarget` and `UpdateTravel` are deleted,
and the wrist dashboard's teleport went with the dashboard.

- Orientation is complete at u = 0.35 (asserted), before any translation.
- Direct when both ends fit the room at the smaller scale; dip otherwise, with both ends inside
  ±fitSpan/2 of the anchor at the midpoint (asserted) and `log10U` monotonic in each half.
- `T = min(2.6 + 0.35·decades, 9)` (asserted ≤ 9).
- A moving target is re-read every frame, so a nine-second flight to Jupiter lands on Jupiter.
- Cancelled by any grab, stick zoom, stick flight, rung tap or new GO; the view stays put.
- **The arrival marker fades on arrival, not on departure** — eight corner strokes,
  R = 7.5 cm, Accent, six seconds then 1.2 s of fade (`Runtime/Nav/ArrivalMarker.cs`).
- A caption under the header names each rung passed during the flight.

**Destinations come from the layers.** `Runtime/Nav/DestinationIndex.cs` assembles the nineteen
rungs, every catalogue body **by id**, and every registered layer's `CollectDestinations`. Nothing is
hand-listed: the old dashboard kept star names beside indices into a catalogue that had been
re-sorted, which is how "GJ 876" flew to GJ 1002 and reported success. Framing radii and room sizes
follow §5.7.3 per kind.

## 3. Follow, Free, and time

Follow, Free and Travelling are three states of `TravelController`, because all three end in a write
to the same value. Follow re-captures its offset whenever something else moves the view — detected by
comparing the origin against what this component last wrote, so no other component has to know Follow
exists. A followed body that leaves its known window releases with one line of notice.

`Core/Time/WarpLadder.cs` is the signed ladder: 28 steps, mirrored, **no zero** (pause is a separate
state that keeps the warp). `SimulationClock` now delegates to it, so reverse warp works everywhere
the clock does. TIME gives transport, Now, eight RATE presets, six DEEP TIME jumps, YEAR/DAY/HOUR
steppers and six KEY DATES — in the headset, not only on the desktop HUD.

## 4. The panel

`Runtime/UI/WristPanel.cs` replaces `WristDashboard` (which was controller-only, and drew twelve
3.4 cm lines onto a 12.4 cm backing). `Runtime/UI/PanelModel.cs` turns the tables into rows and
routes every press by id; the component only draws rows.

Tuned constants, read out of the Atlas and cross-checked against §5.6:

| What | Value | Source |
|---|---|---|
| fingertip press / release | 6 mm / 20 mm | `index.html:1413-1419` |
| hover volume | ±30 mm in x and y, -30 mm…120 mm in z | `:1392` |
| position deadband / damping | 6 mm, `1 - e^(-7 dt)` | `:1370` |
| yaw deadband / damping | 0.35 rad (20°), `1 - e^(-4 dt)` | `:1372` |
| watch tap radius / debounce | 5 cm / 800 ms | `:1384` |
| controller ray reach | 1.2 m | `:1404` |
| button debounce / press flash | 300 ms / 200 ms | §5.6.8 |
| drag-scroll threshold | 8 mm | §5.6.8 |

Both input paths work: fingertip touch (hands) and ray plus trigger (controllers), with the watch
opening, closing and recalling by either. While a hand is on the panel it claims `PanelHot`, so its
pinch and trigger do not also select or grab — **and the other hand is unaffected**. The panel
detects its own trigger edge rather than reading `SelectPressed`, because that is the flag the claim
suppresses.

PIN freezes the pose and re-lays the panel for the actual eye distance; opening while pinned is the
recall. Category colours are never used on controls: controls are neutrals plus Accent, told apart
by caption and position, and the provenance palette appears only on glyphs, badges and data text.
Two token fixes from §5.6.9 are in: `Warning` is now coral (it was byte-identical to the Procedural
amber, so "SIZES NOT TO SCALE" read as a provenance class) and `Accent` moved to 28° from Inferred.

## 5. Layer control

The LAYERS tab is generated from `LayerTable.Layers`: SHOW *This rung* / *All layers*, the six SOLO
buttons, one toggle per layer in the six §5.4 sections with its provenance glyph and its status
suffix (`on`, `off`, `on (other scales)`, `missing: <file>`, `not in this build (<kind>)`), the
parameter accordion, and the label categories. `LayerState` answers one question — `Weight` — so no
layer tests scale, SOLO or data availability for itself.

**A real defect the tests found.** The window ramp faded to zero *at* a window's end, and seventeen
layers end at `log10 U = +3`, which is the ladder's own maximum — so at rungs `sky` and `leo` the
entire Solar System had weight 0. `LayerState.Window` now does not fade at a ladder limit: there is
nothing beyond a limit to fade into. `EveryRungHasLayers` asserts every rung draws something.

## 6. The measure tool

`Runtime/Nav/MeasureTool.cs`. Any two picks — a body, a layer point through `TryPick`, or **From
me** — with the line, the end dots, the label and the sub-line. Units Auto (km below 1e6 km; AU with
light time below 0.1 ly; ly with pc below 1 kpc; kpc/Mpc/Gpc with Mly above) or forced. Three
measurements stay until Clear; measurements between moving objects update live.

**Every number comes from absolute double kilometres**, never from where anything is drawn:
`PickHit.PositionKm`, `view.AbsolutePositionKm`, `FrameContext.EyeKm`. The *drawing* uses the CPU
replica `ScaledSpace.CompressRelative`, so the ribbon lands on what the eye sees while the number
stays true.

`Core/Presentation/Format.cs` is the one formatter (`WristDashboard.FormatDistance` and
`ProvenanceHud`'s are gone) with a test at **both sides of every unit boundary**. Two defects it
caught in itself: `Significant` printed every integer digit instead of rounding to N figures
(5096.8 km came out "5,097 km" where three significant figures is 5,100) and dropped trailing zeros
(so "1.30 pc" printed as "1.3 pc", claiming less precision than the number has).

## 7. Honesty in the headset

`Runtime/UI/HonestyState.cs` computes the chip set from `ViewOptions` and nothing else, in the four
§5.6.4 priority bands, with the fifth and further collapsing into `+N` that opens INFO → THIS VIEW.
**The two rules of R43 are two methods**: the chip is on whenever the preset is not True *or any
drawn body* exceeds x1.05; the badge names the *focus's own* factor and rides under its label in the
world (`BodyLabelLayer`) and on the inspector card. The old single rule fired on the focus only, so
"Readable, nothing selected" drew Phobos at fifteen times life size with no marker at all.

`AnchorBeacon` is now the status card: one line per data file at first run, the honesty statement, a
generated first-run hint, and every failure line in `Warning` — in the headset, where
`ProvenanceHud` (deleted from every platform, C13) never was. INFO → CREDITS & LICENCES exists and
degrades honestly: it prints what it knows and says "the manifest has not been read in this build"
until a data lane supplies `credits[]` through `PanelModel.SetCredits`.

## 8. The inspector

`Runtime/UI/InspectorModel.cs` builds the NOW lines (distance from you, angular size, drawn-at
factor, distance from the Sun in AU and light time, distance from Earth and its light time, speed,
phase angle Sun–body–Earth) and the facts, **each with its own `FieldProvenance`**; the card shows
three and the weakest glyph, INFO → INSPECTOR shows all. Rebuilt at most at 2 Hz.
`Runtime/UI/InspectorCard.cs` replaces `SpatialPanel` and sits 0.12 rad to the object's right at its
rendered distance clamped to 0.5–2 m, re-checking `IsGeometryDrawn` every frame.

---

## Files

```
git diff --stat $(git merge-base master HEAD) HEAD   # 49 files, +7637 / -1938
```

New Core: `Compat/IsExternalInit.cs`, `Layers/{LayerTable,LayerState,LayerContext,LayerBudget,
LabelCandidate}.cs`, `Nav/{Destination,TravelPlan}.cs`, `Presentation/{Format,SizePresets,
PresentationPlane}.cs`, `Sky/ObservingSites.cs`, `Time/WarpLadder.cs`.

New Runtime: `Universe/CosmoLayer.cs`, `Nav/{TravelController,ScaleControl,DestinationIndex,
MeasureTool,ArrivalMarker,ViewOptions}.cs`, `UI/{CosmoTextMesh,CosmoSurface,PanelModel,WristPanel,
HonestyState,InspectorModel,InspectorCard}.cs`.

Rewritten: `UI/{CosmoTheme,BodyLabelLayer}.cs`, `XR/{XrInput,AnchorBeacon,DesktopInputSource}.cs`.
Deleted: `UI/{CosmoPanel,SpatialPanel,ProvenanceHud}.cs`.
Edited: `Core/Presentation/ScaleLimits.cs`, `Runtime/{CosmoSynthBootstrap,Universe/FrameContext,
Simulation/SimulationClock,UI/CosmoText,XR/XrHandInput,XR/BodySelector,XR/UniverseNavigator}.cs`,
`tools/gates/class_gate.sh`.

## Budget arithmetic for this lane's surfaces

§7.3 allows **6 draws, 20 k vertices, 0.3 eye-screens, 4 MB** for all UI panels and text.

The whole interface is built on two primitives that batch: `CosmoTextMesh` lays every string into
one mesh through `Font.GetCharacterInfo` against the shared dynamic atlas, and `CosmoSurface` lays
every rectangle into one mesh with per-vertex colour, cut from a two-tile atlas. Legacy `TextMesh` is
one `MeshRenderer` each and dynamic batching is off (§8.1), so the naive form of this panel is 26
draws and the labels another 24.

| Surface | Draws | Vertices | When |
|---|---|---|---|
| Wrist panel (chrome + text) | 2 | ~2.6 k (≈90 rects, ≈420 glyphs) | while open |
| Watch face (chrome only, no glyphs) | 1 | 24 | always |
| World labels (one mesh, 24 max + badge) | 1 | ≤1.3 k | always |
| Inspector card (chrome + text) | 2 | ~0.5 k | while something is selected |
| **Steady-state worst case** | **6** | **≈4.4 k** | |
| Measure tool (lines + labels) | +2 | ≤0.4 k | while a measurement is on screen |
| Status card (chrome + text) | +2 | ≤0.6 k | first 20 s, or unhealthy |
| Arrival marker | +1 | 256 | 7.2 s after a travel |

**Deviation:** the steady-state six is met, but three transients can push it to 8–11. They are
mutually rare rather than mutually exclusive (a measurement open during the first twenty seconds is
possible), and I have not fixed it by merging meshes across components. The honest options for the
integrator are (a) accept a transient 11, (b) hide the status card while a measurement is on, or
(c) give the three transient surfaces one shared overlay mesh. Fragment coverage is far under 0.3
eye-screens (the panel is ~0.29 m at 0.42 m, about 0.05 of a screen) and the font atlas is one
shared 512² dynamic texture, well under 4 MB. **All of this is INFERRED: nothing has been measured
on a device.**

## Tests added

`tests/CosmoSynth.Core.Tests/NavUiTests.cs`, 25 cases.

**Eight were proved able to fail** by planting the defect, running, and reverting
(`/tmp/navui/mutate.py`, output below). The rest are known-answer checks against values the
contract itself publishes, and have not been mutation-tested — stated plainly rather than
claimed. Four cases marked **(found a real defect)** failed on the code as written.

```
ok  a linear profile, which starts and stops with a lurch   -> TravelHasZeroVelocityAtBothEnds
ok  smoothstep instead of smootherstep (a jerk, not a lurch) -> TravelHasZeroVelocityAtBothEnds
ok  the window fades at the ladder's own limit again        -> EveryRungHasLayers
ok  significant figures drop their trailing zeros           -> FormatKnownAnswers
ok  the warp ladder steps onto the missing zero             -> WarpLadderCrossesZero
ok  a transcription slip in a rung's scale                  -> RungScalesMatchTheirSpans
ok  AtLimit reports the wrong wall                          -> ScaleLimitsReportTheirEnd
ok  the direct path bows off the line between the two ends  -> DirectTravelKeepsTheTargetInOneDirection
```

**Two mutants survived the first round, and both were the test's fault.** `smoothstep` also has
zero first derivative at both ends, so "zero velocity" never distinguished it from smootherstep —
the test now asserts zero *acceleration* as well, which is the actual reason smootherstep was
chosen, with the bound placed between the two candidates rather than at zero. And "the target's
bearing from the anchor is constant" is preserved by any scalar easing along the line, `e*e`
included; the mutant that breaks it bows the path off the line.

| Test | Proves | The wrong input that made it fail |
|---|---|---|
| `RungsAreTheLadder` | 19 rungs, contiguous indices, monotonic scale, unique ids, all inside the clamp | an index typo in the table |
| `RungScalesMatchTheirSpans` | every `UnitsPerKm` is its documented span filling its documented room, to 4 figures | changing `system` to 0.6 m |
| `LadderSpansTheWholeRange` | 27.7 decades, and the CMB rung has headroom below it | raising `MinUnitsPerKm` to the CMB rung's own scale |
| `ScaleLimitsReportTheirEnd` | `AtLimit` names the wall | returning `atMax` for the minimum |
| `TravelEndpointsAreExact` | exact at both ends, and past the end returns the end | dropping the `Clamp01` on `u` |
| `TravelHasZeroVelocityAtBothEnds` | finite-difference velocity **and acceleration** of scale and focus at u = 0 and 1 | a linear profile; and smoothstep, which passes the velocity half |
| `TravelTurnsFirst` | yaw and plane complete at u = 0.35 | easing the turn over the whole flight |
| `DirectTravelKeepsTheTargetInOneDirection` | the target's bearing from the anchor is constant to 1e-9 rad | a path that bows off the line between the ends |
| `DipTravelShowsBothEnds` | both ends within ±fitSpan/2 at the midpoint, monotonic in each half | a straight log-lerp, which puts M31 1.2e7 m away mid-flight |
| `TravelIsCapped` | `sky` to `cmb` is ≤ 9 s | removing the cap (12 s) |
| `WarpLadderIsSigned` | 28 rungs, strictly increasing, mirrored, no zero | keeping the old zero step |
| `WarpLadderCrossesZero` | +1 steps to -1 and back; every rate round-trips to its index | `index - 1` without the crossing |
| `FormatUnitBoundaries` | the unit chosen on **both** sides of all four switches | `<=` for `<` at 1e6 km |
| `ScaleReadoutKnownAnswers` | the contract's own seven worked readouts | **(found a real defect)** "5,097 km" for three significant figures |
| `FormatKnownAnswers` | 4.24 ly · 1.30 pc, 8.32 light-minutes, x2,400 | **(found a real defect)** "1.3 pc" — a dropped significant figure |
| `ReadableIsAnchoredOnEarth` | Earth is untouched; a small moon grows but not past Earth | exponent 1.0 |
| `MultipliedSystemsHideTheSwallowedMoons` | Mars at x100 hides Phobos and keeps Deimos; the factor caps at ~23 from the Hill radius; Earth at x1000 hides the Moon | scaling each moon to fit instead of one factor per system |
| `TheSunIsCapped` | never past x25 in any preset; True means true | removing the star cap |
| `LayerTableIsWellFormed` | 46 unique rows, non-empty windows inside the ladder, every SOLO group populated | duplicating a layer id |
| `LayerWeightIsContinuous` | no step over 0.1 across the window; zero and `NotInThisBuild` with no data | a hard on/off window |
| `EveryRungHasLayers` | every rung draws something; the "This rung" list stays readable | **(found a real defect)** the +3.0 window end faded to zero at true scale |
| `SoloLeavesTheGuidesAlone` | SOLO isolates one group and never hides a guide | giving `scaleRings` a SOLO group |
| `DestinationFramingIsClamped` | framing is span-into-room, and a 3 m lander is not magnified past 1:1 | removing the `ScaleLimits.Clamp` |
| `PlanesAreTheRightFrames` | ecliptic up is one obliquity off; galactic up is the NGP; the zenith at the pole is Earth's pole | the inverse matrix (the sign trap of `CLAUDE.md`) |
| `ObservingSitesAreUsable` | 12 unique sites spanning both hemispheres; custom clamps and wraps | a list of northern cities only |

`class_gate.sh` gained a self-test case at the same time: **(found a real defect)** its type scanner
did not know the word `record`, so `Core/Nav/Destination.cs` — a file the contract mandates as a
positional record — failed G0e for "declaring no type named Destination". The regex now includes
`record`, and the self-test proves the rule still fails on a record named unlike its file.

## Stubs left for the integrator to delete

Every file below carries a `STUB, lane nav-ui` note in its class comment.

| File | Owner per the contract | Why it is here |
|---|---|---|
| `Core/Compat/IsExternalInit.cs` | integrator, P0.2(e) | **not a stub, but not this lane's**: no shared `record` type compiles without it, and it was absent |
| `Core/Presentation/PresentationPlane.cs` | integrator, P0.3 | `Rung`, `ViewState` and `Destination` all name the type. Two known gaps: `LocalVertical` reads `PlaneContext.ViewerKm` as already Earth-relative (the struct carries no Earth position to subtract), and `FocusEquator` resolves only the eleven bodies with an IAU model |
| `Core/Layers/LayerTable.cs` | rungs are **this lane's**; the 46 `LayerDef` rows belong to the lane owning each renderer | the panel, SOLO and the destinations are all functions of the table, and a LAYERS tab generated from an empty one proves nothing. Rows transcribed verbatim from §5.4 |
| `Core/Layers/LayerState.cs` | integrator | the LAYERS tab and the "This rung" filter are functions of `Weight` and `Status` |
| `Core/Layers/LayerContext.cs` | integrator | `Weight`'s `ctx`. One addition: a second constructor taking the manifest's `features[]`, because `HasFeature` is in the contract's type but its source is not among the fourteen fields |
| `Core/Layers/LayerBudget.cs`, `LabelCandidate.cs` | integrator | named by `CosmoLayer` |
| `Runtime/Universe/CosmoLayer.cs` | integrator | destinations, labels and picks all come through it |

## Changes to files this lane does not own

| File | Change | Why |
|---|---|---|
| `Core/Presentation/ScaleLimits.cs` | added `AtLimit(double, out bool)` | declared verbatim in §5.1's own code block and absent from the foundation; the haptic bump and the limit flash both need it |
| `Runtime/Universe/FrameContext.cs` | added the `LayerContext` field and a constructor for it; the 7-argument constructor is kept and delegates | §2.8 lists it; `CosmoLayer.UpdateWeight` is defined in terms of it; `ScaleRenderer` compiles unchanged |
| `Runtime/Simulation/SimulationClock.cs` | delegates to `WarpLadder`; `WarpIndex` is now signed; `IsPaused` is `!_running` | §5.8.1 moves the ladder out of this class into Core. `WarpRates` survives as a forward |
| `Runtime/UI/CosmoText.cs` | `TextMaterial` is `CosmoSynth/Text`, atlas in `_MainTex` | a shader not in `CosmoShaders.All` ships magenta |
| `Runtime/XR/XrHandInput.cs` | added `TryGetIndexTipWorld` and `TryGetWristWorld` | the whole hand-tracked panel is a function of those two joints |
| `Runtime/XR/BodySelector.cs` | inspector card instead of `SpatialPanel`; measure-mode routing; dominant hand instead of hard-coded right | §5.9.1 and Handedness |
| `Runtime/XR/UniverseNavigator.cs` | `TravelTo`, `CancelTravel`, `TravelTarget`, `UpdateTravel` deleted | §5.7.1: one travel |
| `Runtime/CosmoSynthBootstrap.cs` | wires the lane; drops `ProvenanceHud` | C13 removes the IMGUI HUD from every platform |
| `tools/gates/class_gate.sh` | type scanner learns `record`, plus a self-test case | the gate could not see a type the contract mandates |

## Requests to the integrator

1. **Delete `Runtime/UI/WristDashboard.cs`** when `PlayModeShot` moves from `rung<N>` to the
   contract's `rung:<id>` (§5.7.6). It is a shim with three members, kept only because
   `PlayModeShot.cs` is integrator-owned and deleting a type it references breaks the Editor
   compile in every lane at once. It is not inert: `rung<N>` now means the nineteen contract rungs.
2. **Raise `tools/gates/test-floor.txt`** from 142 to 199.
3. **Reconcile `LayerTable.Layers`** against each renderer lane's own rows at merge (see Stubs).
4. `PanelModel.DataKindsLoaded` and `PanelModel.SetCredits` are the two hooks the data loader
   should call; until it does, every data-backed layer row reads `not in this build (<kind>)` and
   the credits page says the manifest has not been read. Both are deliberate.
5. The harness view grammar of §5.7.6 (`time:`, `warp:`, `sizes:`, `room:`, `solo:`, `plane:`,
   `measure:`, `select:`, `rung:<id>`) is **not** wired into `PlayModeShot` by this lane, because
   that file is integrator-owned. Every component exposes a `Describe()` for the report and the
   public setters the grammar needs (`ViewOptions.Sizes`, `ScaleControl.StepRung`,
   `TravelController.TravelToRung`, `MeasureTool.Pick`, `PanelModel.SetTab`).

## VERIFIED / INFERRED

**VERIFIED** (read out of the source or computed here):
- The Atlas's scale-limit disagreement: slider `-7.3 … +5.7` at `index.html:88` against the gesture's
  `[10^-7.3, 10^0.5]` at `:1474` — 5.2 decades at the top.
- The Atlas's tuned touch constants (table in §4 above), each at the cited line.
- All nineteen rung scales, recomputed from their spans in `RungScalesMatchTheirSpans`.
- Mars's Hill radius 1.08e6 km and the x23 system cap; Phobos hidden at x100, Deimos not.
- 52 binding rows, every `(Control, Device, Context)` triple unique.
- 199 tests pass; G0a/G0b/G0d/G0e pass.

**INFERRED** (reasoned, not measured):
- Every draw-call and vertex figure in the budget table. Nothing has run on a device.
- That one `CosmoTextMesh` per surface is genuinely one draw call: it is one mesh, one material, one
  renderer, which is the definition — but SRP batching behaviour on Quest is unmeasured.
- That 19 dmm is legible at the measured 1140 px/rad (21.7 px). §5.6.10 says to re-check both sizes
  if the `stereo` assertion reports below 1000 px/rad; that assertion has not run.
- The M31 position used by rung `localgroup`. There is none in the build, so the rung frames the
  Galactic Centre and says so in a notice, rather than inventing a direction.
- That the fingertip model's numbers transfer from WebXR to Unity XR Hands unchanged.

## Headset checks this lane needs

1. **The panel at arm's length and on the wrist.** Is 24 dmm body text legible worn at 42 cm? Is the
   692 dmm width right, or does the panel overhang the forearm?
2. **The fingertip press.** Does 6 mm / 20 mm feel like a button on a real hand, or does the panel
   need to be pushed before it presses? Does the entry-from-behind guard ever block a real press?
3. **The panel's own alpha.** `CosmoSynth/Panel` writes coverage deliberately, so the panel occludes
   passthrough. Confirm the text is readable over a bright room and that the *arrival marker* and the
   *measure ribbon* — which use the same shader — do not punch visible holes.
4. **Travel comfort.** Nine seconds `sky` → `cmb` with the dip: is the turn-then-move split enough,
   and does the dip read as "powers of ten" or as "everything vanished"?
5. **Draw calls.** `adb logcat -d | grep VrApi` with the panel open, a selection, a measurement and
   the status card up: does the interface stay inside six, and what is the real transient peak?
6. **The watch face.** 19 mm at the wrist with a 21–27 mm ring — visible, or a smudge?
7. **Reverse warp.** Does anything downstream misbehave at a negative rate?

## Open questions

1. **The transient draw-call peak** (see Budget). Which of the three fixes does the integrator want?
2. **R48's 6–16 rows for "This rung"** is not met by the seeded table: `inner` and `system` reach 22,
   because the six body decorations are transcribed with the bodies' own window and their real limits
   are `LayerContext` conditions rather than a scale range. The test asserts the measured range with
   the reason written in it. Whoever writes the final rows should close it.
3. **§5.1 vs §5.7.2 on Follow.** The rung table gives `neighbourhood` (rung 10) `FollowOnArrival =
   the Sun`; §5.7.2's prose says Follow is off after rungs 10–18. I followed the table, because
   `Rung.FollowOnArrival` is a field of that row.
4. **The `sky` rung's "1 m"** column in §5.1 is `1 m`; the formatter prints `1 m = 1.00 m`, because
   three significant figures with trailing zeros is what makes `1.30 pc` correct. Say if the table's
   shorthand should win.
5. **The edge arrow** (§5.6.1: an arrow at the edge of the view pointing at the focus when it has
   been out of view for 2 s) is **not built**. It needs a screen-edge overlay and would be a seventh
   draw call; it is the one §5.6.1 surface this lane did not deliver.
