# Roadmap: from a solar system to a universe

Requested 2026-08-01 as "convert current game into Space Engine". This document is the honest
version of what that means, in ordered phases, with effort and risk stated per phase.

---

## What is actually being asked for

Space Engine is a 1:1 model of the observable universe: every catalogued star in its real place,
every uncatalogued one generated, planets you descend to without a loading screen, galaxies and
nebulae, and continuous navigation from a mountainside to intergalactic space. It is roughly a
decade of one specialist's work and it targets a desktop GPU.

**Full parity is not on the table**, and any plan that implies otherwise is lying. Two hard
constraints make CosmoSynth's version a different object:

| Constraint | Consequence |
|---|---|
| Quest 3 mobile GPU, 72 Hz **per eye** | A budget of roughly one desktop tenth. Star counts, triangle counts and overdraw all get an order of magnitude less room. |
| Everything ships inside a 43 MB APK, **no runtime network** | Gaia is 1.8 billion stars and ~1 TB. We ship a magnitude-limited subset measured in megabytes and generate the rest. |

What *is* on the table is the same idea at a scale a headset can hold: the real sky, worlds
everywhere, and a continuous ladder from the ground to the galaxy.

---

## Why this is not starting from zero

The foundations were built for this and are already tested:

| Already built | Where | State |
|---|---|---|
| Scale ladder to **10²² km** | `ScaleSpace.DefaultBands` | Five bands, per-band depth compression, tested |
| Two-band depth compositing | `DepthBands` | Shipping; near plane stays at 5 cm |
| Floating origin, double precision | `FloatingOrigin`, `Double3` | Shipping |
| **Whole-system procedural generation** | `PlanetGenerator.GenerateSystem` | Works, physically validated — wired only to a fallback |
| **Cube-sphere quadtree + screen-space-error LOD** | `CubeSphere`, `TileId` | Complete and tested. **Nothing in `Runtime/` references it** — no geometry has ever been built from it |
| **Surface field**: elevation, sea level, normals, slope, temperature, moisture | `TerrainField` | Complete and tested; drives the baked textures today |
| Sampled ephemerides, coverage windows | `TrajectoryTable` | Shipping (spacecraft) |
| Per-field provenance | `Provenance` | Shipping — the thing that keeps "generated" honest |

So the gap is **population and geometry**, not architecture. That is why this is a roadmap rather
than a rewrite.

---

## Phase 0 — the render regression, first

**Nothing below matters if the headset draws the scene a hundred times.** 1.7.0 came back as
"100 plus instances of earth … repeated on a grid". 1.7.2 reverts the suspected cause (MSAA under
multiview) and adds a `render:` line to the status card reporting stereo mode, eye-texture volume
depth, MSAA and camera count.

**Blocked on one reading from the headset.** Effort: minutes once that arrives.

---

## Phase 1 — the real sky  ✅ DONE 2026-08-01

Replace the 3200 invented background stars with a real catalogue at measured positions, so
flying outward reaches actual stars.

- Import a magnitude-limited catalogue (~10⁵ stars) with position, parallax, magnitude and colour
  index; derive temperature from colour and place each star in ICRF like the exoplanet hosts
  already are.
- Ship it as a compact binary, the same way `.csmesh` works — roughly **20 bytes a star, ~2 MB**.
- Render it within budget.

**Effort:** 1–2 sessions. The importer pattern is well established (this is the third).

**Risks, honestly:**
- *Licensing.* `docs/data-sources.md` already flags it: Gaia is **ESA, CC BY-SA 3.0 IGO with a
  required acknowledgement** — not NASA-style public domain. The catalogue chosen and its exact
  attribution have to be settled before anything ships.
- *Render budget.* The field is already 3200 stars × 5 quads. 10⁵ stars the same way is 2 million
  vertices and will not hold 72 Hz. Needs magnitude-banded LOD, and probably a point-sprite path
  rather than per-star quads. **This is the real work of the phase**, not the import.

**Proven by:** the catalogue against the published sky, in `StarCatalogTests`. Sirius comes out
brightest at magnitude −1.44 and 2.64 pc; Vega 0.03 at 7.68 pc; Arcturus −0.05 at 11.26 pc — all
matching published values. About 5000 stars reach magnitude 6, which is the naked-eye sky.

**Outcome.** 9000 stars drawn to magnitude 6.5 in **37 328 vertices — fewer than the 3200
invented stars it replaced**, because the invented sky spent up to five quads on every star while
the real one spends them only on the bright end. Measured against the tuned invented sky: 553
detectable stars against 191, at the same median brightness.

The one thing that needed retuning was the magnitude mapping. The two populations have completely
different distributions — the procedural sky drew `rank = u^2.2` with a median of 0.22, while a
real sky to magnitude 6.5 has a median star at magnitude 5.8, because a real sky is overwhelmingly
faint stars. The first render was correct and dim: 460 stars, none above 100/255. Narrowing the
window put the median back where it belonged.

---

## Phase 2 — every star has worlds  ✅ DONE 2026-08-01

Wire `PlanetGenerator` to the star catalogue so approaching *any* star generates its system
deterministically from a seed derived from its catalogue id.

- Generate on approach, discard on departure; never persist.
- Same seed ⇒ same system, forever, on every machine (`Hash` is SplitMix64 for exactly this).
- Every generated field is **Procedural** in the provenance and the UI must say so. A real star
  with invented planets is the single easiest way for this project to start lying.

**Effort:** 1–2 sessions. The generator and its validation exist; this is lifecycle and wiring.

**Risks:** memory churn from generating and discarding systems during flight; and the honesty
problem above, which is a UI requirement, not a nice-to-have.

**Proven by:** flying to a star, finding worlds, leaving, returning, and finding *the same*
worlds — plus a test asserting no generated field ever reports itself as Measured.

**Done so far — `Core/Sky/StarSystemFactory.cs`, six tests.** Given any catalogue star it builds
a validated system around it, deterministically.

- The **seed is the star's position**, quantised to a milliparsec, not its index in the file.
  Index is an artefact of the sort order, so a rebuilt catalogue would silently replace every
  planetary system in the sky; position is a measured fact and does not move. A revised parallax
  in the seventh decimal must not reseed a star, and there is a test for it.
- The star is **not** regenerated. Its position, temperature and luminosity come from the
  catalogue, and the generator is handed the mass those imply, so the planets orbit the star that
  is actually there. Mass comes from luminosity where a parallax exists — by numerically
  inverting the generator's own mass-luminosity relation, so the two cannot disagree — and from
  temperature where it does not.
- A test asserts no generated field is ever Measured, and that the star's position always is.

**Bug found and fixed by writing the test rather than by reading the code:** `CelestialCatalog`
keys bodies in a dictionary by the id they had when added, and `Rebuild()` only refreshes the
parent/child index. Renaming the generated star in place left it answering to its old id and
invisible under its real one — which is exactly what the runtime looks it up by.

**Known limitation, documented rather than hidden:** the mass relation is main-sequence, and it
is wrong for giants. Arcturus derives 113 L☉ and Betelgeuse 1.3 × 10⁴ from real magnitudes, and
both are luminous because they are large and old rather than massive, so each gets a system
spaced for a heavier star than the real one.

**Runtime — `NearbyStarSystems`, verified in the harness rather than on a headset.** The nearest
star with a known parallax gets its worlds once the viewer is within a parsec, and loses them
past 1.35 of that; only one system exists at a time, because 119 613 of them is not a design.

`PlayModeShot` gained a `star<N>` action that flies the viewer to a catalogued star, so this is
checkable here. Flying to Sirius: **8 bodies generated**, planets b–h with radii 2 956–6 474 km,
and standing there **the Sun reads 2.62 pc away against Sirius's catalogued 2.64** — the geometry
is self-consistent from the far end.

Three bugs the capture found that reading the code did not:

- `UniverseNavigator` rewrites the origin from its own stored position at the end of every
  Update, so moving the origin alone was undone within a frame. The harness reported "no star
  within range" from a viewpoint that had never moved — a false negative indistinguishable from
  the feature not working.
- `SetCatalog` raises `CatalogLoaded`, which the bootstrap answers by re-anchoring on the focus
  body. Generating a system therefore **teleported the viewer back to Earth**, which released the
  system, which was the state the report showed. `UniverseSimulation.AddBodies`/`RemoveBodies`
  now add and remove views surgically and do not raise the event — which also avoids destroying
  235 views to add 8.
- Arriving and being able to see are different requirements. At the hologram scale forty
  astronomical units is a quarter of a million render units, past the horizon where everything is
  a sky point: the first capture generated all eight bodies correctly and drew none of them.

---

## Phase 3 — seamless landing  ◐ GEOMETRY DONE 2026-08-19

Build terrain meshes from the cube-sphere quadtree so you can descend from orbit to the ground
with no transition.

- Generate tile meshes on worker threads from `TerrainField` — `Core` is engine-free and already
  used this way by `PlanetTextureBaker`.
- Split and merge by the screen-space error the quadtree already computes.
- Skirts at tile edges, or every seam shows a crack of space.

**Effort:** 3–5 sessions. The largest phase and the only one that is mostly new code.

**Risks:**
- *Precision.* At 1 m detail on a 6371 km body, float positions fail; meshes must be built in a
  tile-local frame. The double-precision layer exists for this but has never carried it.
- *Frame time.* Splitting a tile must never stall the main thread — the same constraint the
  texture baker solved, at higher frequency.
- *It is the phase most likely to look worse before better*: a partly-working terrain reads as a
  broken planet, where today's smooth sphere reads as a finished one.

**Proven by:** a continuous descent capture from orbit to ground with no popping and no seam.

**Done so far — `Core/Procedural/TerrainMesh.cs`, six tests.** A cube-sphere tile becomes
positions, normals and triangles, engine-free so it can run on a worker thread like the texture
baker already does. Vertices are **tile-local**, which is the point of the type: absolute
positions on Earth's surface quantise to about 60 cm in a 32-bit float, and a metre-scale
landscape would snap every vertex to the same handful of values. Skirts are sized from the tile's
own ground size rather than fixed.

Two failures the tests caught, and they were different in kind:

- **Every triangle was wound inside out** — 0 of 768 faced outward. A straight bug, invisible in
  any numeric check that does not ask which way a surface points, and on screen it would have
  looked like a planet that simply was not there.
- **A seam of 58 mm between neighbouring tiles**, which turned out to be the *test* being wrong.
  At level 3 a tile spans some twelve hundred kilometres, so its local coordinates still reach
  600 km and a float resolves those to about seven centimetres; two neighbours rounding
  independently differ by that much. The local frame buys precision **in proportion to how small
  the tile is** — which is fine, because that is exactly the regime landing happens in. Retested
  at level 10, where a tile is ten kilometres across, the two agree to under a micrometre.

**Still to do:** the runtime — a quadtree that splits and merges by the screen-space error the
tiles already compute, builds meshes on worker threads, and swaps them in without stalling a
frame. That is where the frame-time risk lives, and it is most of the remaining work.

---

## Phase 4 — deep sky

The Milky Way as a place you are inside, plus nearby galaxies and the brightest nebulae.

- Galactic structure from the star catalogue plus a published spiral model.
- Messier/NGC objects at real positions with real angular sizes.

**Effort:** 2–3 sessions.

**Risks:** this is the phase where invention is largest and provenance matters most — no galaxy
has a measured appearance at this detail. Everything here is **Procedural over a Measured
position**, and must be labelled that way.

---

## Order, and why

Phase 1 first because everything interstellar hangs off having real stars, and because it is the
one phase whose payoff ("that is actually Orion") is immediate. Phase 2 second because it needs
Phase 1's stars. Phase 3 is independent and could be pulled forward, but it is the heaviest and
the most likely to regress what already looks good. Phase 4 last because it is the most invented.

Each phase ships an APK and is judged on the headset, because
[the play-mode harness renders flat and monoscopic](../CLAUDE.md) and the last regression proved
what that misses.
