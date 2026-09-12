# What the pre-migration tree actually renders (2026-09-12)

A play-mode capture of main at `19e6923` — the real app, real bootstrap, flat fallback with no XR
device. Image and report: `/tmp/cs-baseline/app-running.{png,report.txt}` on the box; the PNG was
sent to the owner. This is the before-picture for the Phase 0 migration, and the defects below are
work items, not observations for their own sake.

**What works.** 235 bodies through three catalogue merges, measured Blue Marble Earth and LRO Moon
surfaces, 9,000 real stars to magnitude 6.5 in 39,136 vertices, 24 spacecraft models (107,583
triangles) with 16 inside their coverage windows, orbit lines culled to 4 of 42, and a legible
status card. The frame is not broken: Earth reads as Earth.

## Defects visible in this one frame

1. **The atmosphere is a hard teal shell, not air.** Earth's shell is only 1.6 % larger than the
   planet (`ratio=1.0157`) yet renders as an opaque band around the whole limb, darker than the
   planet it surrounds. Air should be a limb-weighted gradient that vanishes face-on. Owner: the
   atmosphere shader and `AtmosphereShell`.
2. **Labels are clipped and collide with the UI.** "James Webb Space Telescope" is drawn across the
   status card and off the left edge as two overlapping ghosts. `BodyLabelLayer` needs the
   declutter and the screen-edge rule the contract's label policy specifies.
3. **The render diagnostic is malformed**: `render: flat (no XR device)cams 2 q5`. A missing
   separator in the one line that is supposed to answer "what is this device actually doing".
   It also confirms `cams 2` — the two-camera stack that Phase 0 deletes.
4. **Pluto's atmosphere carries `worldScale=1`** while every other body's is its real scale
   (Mars 71, Saturn 338, Neptune 1166). Either the shell is unscaled or the report lies about it.
5. **The capture is not aimed.** Earth sits bottom-right, half out of frame. The capture must frame
   its subject, which is also what the seven fixed views in G1c are for.
6. **Two texture bakes are aborted at shutdown** (`texture bake failed for 'sun' / 'earth': Thread
   was being aborted`). Benign at exit, but the capture should wait for bakes to finish, or a shot
   can show an unbaked body and nothing says so.

## Two things the report proves that the picture cannot

- `clip planes: near 0.05, far 10` with `depth bands: split 10, far 2000` — the near camera's far
  plane is 10 m. Every distant body in this frame is drawn by the *second* camera, which is exactly
  the stack that Optimize Buffer Discards is documented to break, and the suspected cause of the
  owner's "100 plus instances of earth … repeated on a grid".
- `clear: Depth, colour alpha 1.00`. In the flat fallback that is correct, but on device the XR
  camera must clear SolidColor with alpha 0 or passthrough is a black wall. The play-mode harness
  cannot tell those apart, which is why G1c asserts the clear mode and alpha directly.
