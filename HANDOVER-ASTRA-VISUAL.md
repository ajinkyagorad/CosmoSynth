# Handover to Astra — make it look like something

Written 2026-09-12, after the owner saw the first render of the revived app and said, verbatim:

> "it just looks lame , empty dark space, I will have it run through astra to improve its overall
> engineering"

That is the brief. Read it as two jobs that share a root cause: **the picture is dead**, and **the
engineering underneath it was never built to make a picture**. Both are yours. This note is the
context you need so you do not spend your first day rediscovering what is already known.

Read `HANDOVER-ASTRA.md` first for the box rules, ownership and the lane protocol. This note is
about quality, not logistics.

---

## 1. The frame in question, and what it actually is

`/tmp/cs-baseline/app-running.png` on the box (report beside it). Earth with the real Blue Marble
texture, the ISS labelled on its orbit, a status card, a scatter of small stars, black everywhere
else. The write-up of its defects is `docs/reports/baseline-observations.md`.

Before you chase anything, know what that image is and is not:

- It is a **flat, mono, play-mode capture on a server with no GPU** (llvmpipe), at 1920×1080 with
  **MSAA off**. It is not the headset. Stereo, passthrough, foveation and real frame rate are all
  absent from it.
- It is the **pre-migration tree**: Built-in render pipeline, **Gamma** colour space, **no HDR, no
  tone mapping, no bloom, no post-processing of any kind**, two stacked cameras.
- The migration to URP + Linear + one camera is **in flight right now** on branches
  `lane/foundation-code` and `lane/foundation-shaders`, not yet merged. Do not start by porting the
  pipeline; it is being done. Start from the merged result.
- In passthrough on the headset, the black is **not black** — the camera clears to alpha 0 and the
  room shows through. "Empty dark space" is partly an artefact of capturing MR flat. But the app
  also has a Space mode, and there the void is real and has to earn its place.

So the owner's judgement is correct and the explanation is not "it is only a test render". The
frame is technically right and visually dead, and the reasons are structural.

---

## 2. Why it looks dead — the honest list

Every item here is evidenced by the capture, its report, or the gates. None of it is taste.

1. **There is no image pipeline at all.** No HDR, no exposure, no tone mapping, no bloom. A star is
   therefore a one- or two-pixel quad at whatever brightness the shader wrote, on pure black. Real
   night-sky photography and every convincing space renderer get their look from the *response
   curve*, not from the geometry. This is the single biggest gap.
2. **The colour space is Gamma**, so every additive blend and every hand-tuned colour is wrong in a
   way that cannot be fixed by tuning. Linear lands with the migration; expect every constant that
   was tuned by eye to need re-measuring afterwards.
3. **The sky is empty because the layers that fill it do not exist yet.** What ships today is 9,000
   stars to magnitude 6.5 drawn as small quads on a 900-unit shell — direction only, no depth. There
   is **no Milky Way band, no dust, no nebulae, no galaxies, no CMB**, and no zodiacal light. The
   contract plans all of them; none is built. Of these, **the Milky Way band alone removes most of
   the emptiness**, because from inside the galaxy it *is* the sky.
4. **Earth is flat-lit `Standard` shading.** No clouds, no city lights on the night side, no
   specular sea, no soft terminator, no aerial perspective at the limb. The texture is good; the
   material is doing nothing with it.
5. **The atmosphere is a hard shell.** Earth's is 1.6 % larger than the planet (`ratio=1.0157`) and
   renders as an opaque teal band all the way around the limb, darker than the planet. Air should
   be limb-weighted and vanish face-on. A single-scattering integral with a precomputed table is the
   right answer, and it is affordable if the table is small.
6. **Nothing frames anything.** The subject sits half out of frame, the labels clip off the left
   edge and overprint the UI card, and the card itself is a grey rectangle with no typographic
   hierarchy. The contract defines seven fixed capture views; use them, and compose them.
7. **There is no sound at all.** The app has an `AudioListener` and nothing else.
8. **Scale is invisible.** Nothing in the frame says whether Earth is a hologram on a table or a
   planet you are orbiting. Rings, grids, a horizon cue, labelled distances and the honesty line
   ("sizes not to scale") all exist in the design and none is on screen.

---

## 3. The bar to clear

The owner's own WebXR work is the reference, and it is better looking than this. Copies are on the
box at `refs/webxr/`:

- `refs/webxr/nebula-gallery` — the **Milky Way Atlas**. Read its README. It renders stars
  **flux-true** (brightness ∝ luminosity / distance², with an eye-like L^0.28 compression), galaxies
  with their *measured* ellipticity and position angle, a real 3D dust cube, and an honest-limits
  section. That is the standard to match and then exceed in a headset.
- `refs/webxr/pages/solar-xr2.html` — the Heliosphere page: light-time rings, live inspector
  numbers, category-coloured tablet UI.
- `refs/webxr/pages/holographic_star_map(2).html` — the fictional half, and the look the owner
  associates with it.

The product also has a second half that barely exists yet and is the most interesting thing in the
brief: **the app bridges the real universe to authored fictional worlds.** Not a mode switch — you
fly out along real stars and arrive somewhere invented, with provenance and a distinct visual
language making it unmistakable which is which. Staging that transition is a design problem worth
your best effort, and nothing in the current build even gestures at it.

---

## 4. What is already decided — build on it, do not re-litigate

`docs/CONTRACT.md` (7,356 lines) is binding; `docs/CONTRACT-SUMMARY.md` is its map. The parts that
constrain visual work:

- **One camera**, URP 17.5, Linear, MSAA 4× from the pipeline asset, SRP foveation, depth submission
  None, symmetric projection. `DepthBands` is deleted.
- **One distance map** `f(d) = d₀(1 + ln(d/d₀))`, d₀ = 10 m, applied as **one similarity per group
  about the centre eye**, never per body. 10²⁷ m lands at 609 m of render depth.
- **Eleven single-variant `CosmoSynth/*` URP shaders**, each carrying the six stereo macros. Additive
  passes are `Blend One One, Zero One` — **nothing may write alpha**, or it punches an opaque hole in
  passthrough.
- **Budget at 72 Hz**: ≤ 11 ms GPU, ≤ 150 draws, ≤ 1.2 M vertices, ≤ 33 raymarch step-coverages.
  Every visual idea has to be costed against that, on a mobile GPU, per eye.
- **Populations are layers**, one mesh each over an engine-free Core parser, never `CelestialBodyView`s.
- Provenance is per field and visible: generated beauty must never read as measured fact.

---

## 5. The brief, ordered by visible payoff per unit of risk

1. **The image pipeline.** Decide and implement the response curve: HDR eye buffer or not on Quest,
   tone mapping (URP Neutral vs ACES on a mobile GPU), a bloom that makes bright stars bloom and
   leaves the faint ones alone, an exposure model that adapts as the viewer changes scale rung by
   twenty orders of magnitude, and a grading LUT. This is what turns dots-on-black into a sky.
   Measure the cost; the budget above is the ceiling.
2. **Stars that look like stars.** Flux-true magnitudes, a point-spread with a real core and halo,
   diffraction spikes on the brightest few percent only, a size floor of at least 2.5 px (a
   one-pixel feature has no shape — this project already shipped square stars once for exactly that
   reason), colour from B−V, and brightness carried in RGB rather than alpha.
3. **The Milky Way band.** The largest single win against "empty". Build it from the dust cube and
   the star density, not from a painted skybox, so that it stays correct when the viewer leaves the
   galaxy.
4. **Planets that are lit.** PBR on the measured textures, night-side city lights, cloud layer,
   specular ocean, a terminator with real softness, ring shadows, and aerial perspective at the limb.
5. **Atmosphere as scattering, not a shell.** Single-scattering with a small precomputed table;
   limb-weighted, and gone face-on.
6. **Depth and scale cues.** Orbit ribbons with a gradient that reads direction, light-time and
   distance rings at the right rungs, labels with hierarchy and declutter, and the honesty line
   visible in the headset rather than only on the desktop HUD.
7. **Composition.** The seven fixed views become deliberate shots: subject framed, horizon placed,
   something in the foreground for parallax. A capture that is not aimed proves nothing and sells
   nothing.
8. **Sound.** An ambient bed and a few data voices (pulsar rates, a merger chirp) change how the
   space feels more cheaply than any shader.
9. **The fiction bridge.** Stage the arrival: how the real sky hands over to an authored system,
   what the viewer sees during the handover, and how provenance stays unmistakable.

---

## 6. The engineering underneath, which is the other half of the ask

The gates already name what is structurally wrong; run `bash tools/gate-all.sh` and read the logs.
On the pre-migration tree they flag: shaders without the URP tag, Gamma colour space, wrong OpenXR
depth/latency/symmetric-projection settings, a deprecated hand-interaction feature, six empty
duplicate XR directories, five XR manager assets, five `SetCatalog` call sites, five components
reading `RenderedRadius` without checking whether the body is drawn, and axis swaps outside
`FloatingOrigin`. The last one means **the solar system currently renders mirrored**.

Two more found by the design pass, both real:

- `Core/Frames/StandardFrames.cs` builds the ICRF→galactic rotation **with all three signs flipped**
  — the north galactic pole comes out at (−0.346, 0.176, 0.922) instead of (0, 0, 1). Every galactic
  layer you are about to build sits on that matrix.
- The Sun's point light is attached to `RootIds[0]`, which after the exoplanet merge is **61 Vir**.
  The solar system is lit from another star.

The rules that keep this from rotting, all of which have been paid for in shipped bugs:

- `Core/` never references `UnityEngine`; that is why 142 tests run in two seconds with no editor.
- One MonoBehaviour per file, named exactly after the class, or Unity silently strips it from
  headlessly-built prefabs.
- Every shader must be listed in `CosmoShaders.All`, or a runtime-built scene ships magenta.
- A gate that has never failed is not a gate: every gate here is fed a deliberately broken input in
  its own self-test, and any new one must be too.
- Anything that decorates, picks or measures a body re-checks `IsGeometryDrawn` **every frame**.
- The verifying environment differs from the running one: a flat mono capture on llvmpipe cannot
  prove stereo, passthrough, frame rate or foveation. Say so in the notes, every time.

---

## 7. Hard limits

- **Do not fork the render stack.** One camera, one pipeline, one distance map. If you believe the
  compression maths is wrong, argue it in `docs/reports/` with numbers rather than adding a second
  path.
- **Never write alpha in an additive pass** and never let an effect contribute coverage in
  passthrough.
- **No per-body scaling** in the far field: one similarity per group, or a moon sinks into its
  planet.
- **No `RenderMeshInstanced` or indirect draws for point populations** — instance-count doubling is
  wrong under multiview. Static meshes with vertex-shader billboards, as the three sibling apps on
  this box already ship.
- **One Unity editor for this project, ever**, behind the `flock` in `tools/common.sh`. The box has
  about ten other editors from other projects and **9 GB of disk free**. Check `df -h /` before
  anything heavy.
- Do not touch `~/control-panel`, other projects, or `~/builds/cosmosynth/recovered` (the `.KEEP`
  1.9.0 reference APK).

---

## 8. State as of this note

| Thing | Where | State |
|---|---|---|
| Research | `docs/research/2026-09-11-*.md` | 8 reports, done |
| Design | `docs/design/2026-09-11-{A,B,C,D}*.md` | 4 sections, done |
| Contract | `docs/CONTRACT.md` | binding, revised after 3 reviews |
| Licence clearance | `docs/DATA-LICENCE-AMENDMENT.md`, `docs/licence/*` | done; ESA data is non-commercial, substitutes staged |
| Toolchain + gates | `tools/` on main | merged, gates self-testing |
| Foundation code | `lane/foundation-code` @ `6f19d5d` | committed, under review |
| Foundation shaders | `lane/foundation-shaders` @ `6e0e3be` | committed, stereo gate green |
| 2.0.0 APK | — | not built yet |
| Headset test | — | nothing since 1.7.0 |

Data lanes (the ten importers in `docs/design/2026-09-11-B-data.md` §7) are unstarted and are also
yours per the first handover. Several of them — dust, galaxies, the Milky Way point sets — are
exactly the layers that fix the emptiness, so the data work and the visual work are the same work.

---

## 9. Done looks like

A capture from the seven fixed views that a stranger would call beautiful, holding the 72 Hz budget
on Quest 3, with every gate green, and a before/after pair against `/tmp/cs-baseline/app-running.png`
for each view. Then a build on the owner's headset, because nothing above is proven until it is worn.

---

## Appendix — the five technique documents are written, and reconciled

§5's brief was answered as five documents in `docs/design/visual/`: `image-pipeline.md`,
`starfield.md`, `galaxy-and-sky.md`, `planets-and-air.md`, `staging-and-bridge.md` — ~5,900 lines
of maths, constants, shader source, costed arithmetic and V/I tags. **Read
`docs/design/VISUAL-PLAN.md` before acting on any of them**: it reconciles the five into one frame
budget, one dependency order and one phased plan, and resolves seventeen contradictions between them.

**Budget verdict.** Draws 116 of 150, vertices 1.092 M of 1.2 M, transparent fragments 1.95 of 3.0,
point sprites 0.329 of 0.5, raymarch 30.04 of 33, texture 61 MB of 100, APK ~55 MB of 150 — all pass,
and the design *returns* 18 draw ceilings, 0.29 M vertex ceilings and 26 MB of texture against §7.3's
own rows. **One axis busts, for one unmeasured constant.** κ (ms per step-coverage for a 3D-texture
march step) decides the frame: break-even **κ = 0.281**; at the inferred 0.06 the worst frame is
4.35 ms of 11.0, at Fractarium's measured *upper bound* 0.338 it is 12.70 and misses 72 Hz by 15 %.
Cut 1 — the Galaxy volume from 30 steps to 20 — moves break-even to 0.42, above that bound, so it
fits whatever the probe says. One thing in this frame is expensive and it is that step count.

**The decisions that matter most.** No HDR buffer and no post pass anywhere: the alternative costs
6.5–8.0 ms of 11.0 and `UberPost` returns alpha 1, killing passthrough app-wide. The response curve
lives at the point of emission and every operator returns exactly 0 for 0, because the room is behind
the picture. Stars are flux-true with no luminosity compression — it emerges from the point-spread
wing — giving 1,500 : 1 of range where the build ships 2.5 : 1 and renders Sirius, Vega and Deneb
identically. `dust`, `nebulae`, `mwDisc` and `mwBand` collapse to one volume plus one baked direction
cube; an atmosphere draws only while its disc is ≥ 2/(k−1) px, turning nine draws into one and
buying back the 3 step-coverages that had no margin.

**Do first, in order:** fix the galactic matrix; bring `lane/foundation-shaders` to R1/R7/R30; move
the Sun's light off 61 Vir; derive the capture fov from `PixelsPerRadian` (the harness validates every
pixel rule 21.8 % too generously); run the κ probe. Tune nothing before Linear lands — every constant
in all five documents was chosen against a Gamma framebuffer.
