# Feedback log

Everything the user has said about a shipped APK, against the build they said it about, and
what was done. This file is the **source of truth**; the `YOUR FEEDBACK` section in each
`~/builds/cosmosynth/<apk>.notes.txt` is a rendered copy of it, and the build page shows that
copy inside the APK's expanded row.

Keep the copy in step by hand. The build script regenerates a notes file every time it builds
that version, so anything appended there is lost on the next rebuild of the same version — the
repo is the thing that survives.

**Quote exactly, or mark it as a paraphrase.** Where the wording below is reconstructed rather
than verbatim it says so. Putting invented words in the user's mouth would make the whole log
worthless, and the difference is invisible once written down.

---

## Before 1.4.0 — reported, no APK on disk

**"everything is magenta"** *(paraphrase)* — the Quest build rendered every object with the
error shader while the editor looked perfect. The app builds its whole scene at runtime, so no
asset referenced any shader and Unity's build step stripped them all; `Shader.Find` returned
null in the player only. `CosmoShaders.All` is now the single list, and `XrSetup` writes the
same array into Always Included so the two cannot drift.

**"passthrough works but there is nothing in it"** *(paraphrase)* — several distinct causes over
several builds, collected in the memory note `cosmosynth-xr-empty-scene`. The anchor was placed
relative to the play-area origin rather than the user, so the hologram could be behind them or
inside a wall.

These predate the oldest APK still on the server, so they are recorded here rather than against
a build.

---

## 1.4.0 — versionCode 9, 30 Jul 2026

Four pieces of feedback, all reproduced and all fixed. The APK's notes carry the full text.

- *"rings are bigger than the solar system when I zoom out"* — the ring mesh baked the render
  scale into its vertices, so it was correct only at the zoom it was built at.
- *"I should be able to zoom in/out as I want, it's restrictive to solar system distance"* — the
  scale clamp stopped at ~40 light years, and the two-hand gesture was linear in hand
  separation, giving only ~16× per full arm movement.
- *"I can't go close to the solar system when I zoom out"* — scaling pivoted on the raw midpoint
  between the hands, so with hands over empty space the system rushed away from nothing and
  never came back.
- *"blue orbits is just not good"* — every orbit was the same flat blue and told you nothing
  about which path belonged to which world.

---

## 1.5.0 — versionCode 10, 30 Jul 2026

**A black screen on the headset** *(paraphrase — the exact wording was not recorded)*.

Importing the exoplanets put bodies 4×10¹³ km away. The far plane chased them out, the near
plane was pushed to **26 kilometres** to keep the depth ratio survivable, and every object in
the room was clipped away. Passthrough kept compositing normally, so it looked exactly like an
app that had failed to load.

Fixed structurally: `DepthBands` renders two passes sharing one pose, splitting at the geometric
mean; and bodies past the horizon are pinned to a sky sphere — direction exact, distance given
up, which is what a planetarium does and what nobody can perceive at four light years.

---

## 1.6.0 — versionCode 11, 31 Jul 2026

**"ugly, nonsense controls"** *(recorded in the memory note `cosmosynth-ui-was-never-designed`;
close to verbatim but not certain)*.

There was no locomotion on the headset at all. The only way to move was to grip and haul the
entire universe past yourself, and the left thumbstick was bound to **time warp** — so pushing
forward did not fly anywhere, it made the planets orbit faster.

Two structural causes behind the whole class of problem:

1. **Containers were sized alongside their contents rather than from them.** Every panel that
   declared a width *and* a character size had the two disagree — the wrist dashboard drew
   twelve 3.4 cm lines onto a 12.4 cm backing and showed three and a half of them.
2. **Four separate components each polled the input devices**, so the trigger meant select *and*
   grab *and* boost, and none of it was visible in any one file. All bindings now live in
   `XrInput.Bindings`, and the on-screen legend is generated from that same table.

> The original 1.6.0 notes file was **destroyed on 31 Jul 2026** by a rebuild that ran without
> bumping `ServerBuild.Version`, which overwrites the APK and its notes in place. The summary
> above is reconstructed from what had been read into context plus the memory notes; the
> remainder is gone. The APK binary from the first 1.6.0 build is gone the same way.

---

## 1.7.0 — versionCode 12, 31 Jul 2026

Two requests, both verbatim.

**"use assets from > this link to populate the system as fit"** — with a link to
`github.com/nasa/NASA-3D-Resources`.

24 real spacecraft, geometry from that repository and trajectories from JPL Horizons, each
placed by whichever of three mechanisms is least wrong for its path and each carrying the window
over which its position is actually known. See `docs/accuracy.md`.

**"make stars as stars, not some square looking patches at distance"**

Four independent causes, only the first of which had previously been found: the quads once
carried no texture; the replacement point-spread summed to 1.40 and was clamped, giving it a
flat top (a plateau is a disc, and a small disc is a square); a 64 px texture on a 4 px quad
samples a 4×4 mip, and a magnified 4×4 *is* a grid of blocks; and brightness was routed through
an alpha channel that this shader attenuates, so the quads had been enlarged to compensate for a
signal that was not arriving — which is what turned the squares into cotton wool.

Brightness now lives in the RGB. A single additive quad saturates at ~0.27 of full scale however
it is tuned, so the brightest few per cent get more *geometry* rather than more gain: a second
core quad, a wide faint halo, and two crossed elongated quads that read as diffraction spikes.

**"launch 2 claude agents to study and criticise the logic, and design aesthetics of the
implementations, then after one round they will solve their own critiques, you manage them till
finish"**

Done. What the round found is listed in the 1.7.0 notes; the durable lessons are in `CLAUDE.md`.
