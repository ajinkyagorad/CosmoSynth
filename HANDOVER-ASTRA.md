# Handover to Astra (GPT-6) — CosmoSynth 2.0, the data half

Written 2026-09-12 by the Claude session that is rebuilding CosmoSynth. You are picking up a live
project on a shared server, alongside another agent. Read this file to the end before you touch
anything: the parts about ownership and the box are what stop us destroying each other's work.

---

## 1. What CosmoSynth is, and what 2.0 has to become

`~/projects/CosmoSynth` on this box is a Quest 3 mixed-reality universe engine in Unity 6
(6000.5.5f1). The Unity project is one level down in `unity/`. Its physics layer,
`unity/Assets/CosmoSynth/Core`, never references `UnityEngine`, so 142 tests run in about two
seconds in a terminal with no editor — that rule is the reason the orbital mechanics could be
validated against 260 real JPL state vectors at all. Keep it.

It last shipped 1.9.0 on 2026-08-01 and then stopped, with an unfixed headset report against
1.7.0: "100 plus instances of earth … repeated on a grid flat".

The owner's request on 2026-09-11, which defines 2.0.0:

> based on CosmosSynth, in for unity, make optimal … both fictional and current universe stars
> catalog from earth to solar system to local stars to milky way, to local group, to beyond, to
> microwave cosmic background, make all into coherent unity app … make major corrections, all
> functionalities from nebulytic star thingies should be available there.

So: one app, one continuous scale ladder from a room to the cosmic microwave background, real
catalogues and clearly-labelled fiction side by side, and every feature of the owner's WebXR star
pages absorbed. The app never touches the network at runtime; everything ships as baked data
inside the APK.

---

## 2. Access and the box

```sh
ssh triton@100.127.10.14        # key auth, user triton, passwordless sudo
```

The box is shared by dozens of agent sessions and it is fragile:

- **Disk is the binding constraint.** It has been between 9 and 16 GB free out of 460 GB for two
  days, at 97-98%. Check `df -h /` before any download, never fetch a file over ~1 GB, and clean
  your scratch in `/tmp` when a bake finishes. If you fill the disk, every other session's build
  fails too.
- **RAM and load are shared.** About ten Unity editors from other projects run at once, swap is
  often full, load average sits near 25 on 12 threads. Your work needs no Unity at all (see §4).
- **Never run `du` over a Unity `Library/` tree.** An IO storm from exactly that took the whole
  box down once, killing 103 agent sessions.
- **Never kill a process you did not start**, and never touch `~/control-panel/` (another session
  owns the web panel), other projects under `~/projects/`, or
  `~/builds/cosmosynth/recovered/CosmoSynth-release.apk` (the last surviving APK, kept as the A/B
  reference for the render bug, protected with a `.KEEP` marker).

---

## 3. The documents that are authority, in order

All on the box, all written in the last day, all marked VERIFIED or INFERRED claim by claim:

1. `~/projects/CosmoSynth/docs/CONTRACT.md` — the binding plan for 2.0.0. It is being assembled
   right now from the four section drafts below; if it exists when you read this, it wins over
   everything else. Cite it as "CONTRACT §x.y" in commits.
2. `~/projects/CosmoSynth/docs/design/2026-09-11-B-data.md` — **your section.** 1,850 lines: every
   dataset, its source URL, licence, binary layout, importer name, Core parser, tests and
   known-answer checks, in the order to build them. Read it in full before starting.
3. `~/projects/CosmoSynth/docs/design/2026-09-11-{A-foundation,C-product,D-process}.md` — the
   render/frames/layer architecture, the product and UX design, and the lane/gate process. Read
   A §2 (frames and units) and D §3-4 (tooling and gates); skim the rest for context.
4. `~/projects/CosmoSynth/docs/research/2026-09-11-*.md` — eight read-only research reports the
   design is built on. `data-beyond-and-cmb.md` is the one behind your section; its validated
   scratch scripts are in `docs/research/scripts/` (`healpix_nest.py` is a pure-numpy HEALPix
   NESTED implementation that passes its own round-trip, equal-area and hierarchy checks, and
   `cmb_bake.py` already produced a correct Planck texture — reuse them, do not rewrite them).
5. `~/projects/CosmoSynth/CLAUDE.md` — the project's standing rules and the traps already paid
   for. `~/README.md` is the machine's handoff doc.

---

## 4. What you own, and what you must not touch

You own **the data half**: everything that turns public archives into files the app ships, plus
the engine-free code that reads them.

**Yours:**

- `tools/cosmodata/**` — the importers (Python).
- `tools/cosmodata/authored/**` — hand-authored inputs (landmarks, structures, overrides,
  fictional catalogue, exhibit texts).
- `unity/Assets/StreamingAssets/CosmoSynth/**` — the shipped data files.
- `unity/Assets/CosmoSynth/Core/**` — the engine-free parsers, models and physics that go with
  the data (`CsData`, `StarCatalog`, `DustCube`, `GalaxySet`, `CmbSky`, `LunarTheory`,
  `StandishElements`, `Cosmology`, `GalaxyModel`, …).
- `tests/CosmoSynth.Core.Tests/**` — their tests.
- `docs/data-sources.md` — the licence ledger.

**Not yours, and changing them will collide with the other agent's work in flight:**

- `unity/Assets/CosmoSynth/Runtime/**` and `Editor/**` (the render, XR, UI and build code — it is
  mid-migration from Built-in to URP).
- `unity/ProjectSettings/**`, `unity/Packages/**`, `unity/Assets/XR*/**`.
- `tools/*.sh` and `tools/gates/**` (the build/verify/gate scripts, being ported now).
- `~/server-setup/*cosmosynth*.sh`, `~/builds/**`, `CLAUDE.md` (only lane `data-core` edits
  CLAUDE.md, and only its Data section).
- **Never run a Unity editor for this project.** Exactly one editor is allowed and the other agent
  holds it behind a lock. Nothing in your half needs it.

---

## 5. How to work here

Each lane is a git worktree with no Unity `Library`, which costs about 13 MB:

```sh
cd ~/projects/CosmoSynth
git worktree add ~/projects/CosmoSynth-wt/data-core -b lane/data-core
cd ~/projects/CosmoSynth-wt/data-core
./run-tests.sh                     # 142/142 today; add yours to it
```

`run-tests.sh` needs main's compiled package assemblies when the worktree has none. The tooling
lane is adding that fallback; until it lands, pass it yourself:

```sh
./run-tests.sh -p:UnityScriptAssemblies=/home/triton/projects/CosmoSynth/unity/Library/ScriptAssemblies
```

Commit on your lane branch. **Do not merge into main** — one integrator does that, in order, with
`git merge --no-ff`, so a broken merge can be found and undone. Write a report per lane at
`docs/reports/<lane>.md` in your worktree: what you built, what each test proves, what it cannot
prove, every deviation from section B with its reason, and every number you measured.

Two mechanical traps that have each cost a whole build cycle on this box:

- **Anything you send inline through `ssh` is chewed by the local shell first.** If it contains
  backticks, `$`, `!` or apostrophes — which means all real code — write the file and `scp` it.
- **Shell scripts must be LF-only.** `head -1 f | cat -A` must end `bash$`, never `bash^M$`.

---

## 6. Your queue

Section B §7 has the full table with dependencies. In order:

| Step | Lane | Deliverable |
|---|---|---|
| 0 | `data-core` | The shared container: `tools/cosmodata/{csdata,fetch,fits,licence,ledger}.py` with a self-test fixture, `Core/Data/{CsData,DataManifest}.cs` + tests, the StreamingAssets reader contract, and the Data section of `CLAUDE.md`. **Everything else depends on this, so it goes first and it has to be right.** |
| 1 | `data-stars` | `build_starfield.py`: AT-HYG v4.0 astrometry joined to HYG v4.4 photometry, proper names, 88 Stellarium constellations as index pairs; `StarCatalog` rewrite + tests. |
| 2 | `data-exo` | The full exoplanet archive, all 6,360 planets, replacing the 120 that ship today. |
| 3 | `data-core-physics` | Core code, no data: Meeus lunar theory (the current Kepler Moon drifts ~85,000 km in five years), Standish deep-time planets, galaxy model, cosmology, heliosphere, the galactic and supergalactic frame matrices with test vectors. |
| 4 | `data-sun` | JPL SBDB belts (measured asteroids, TNOs, centaurs, trojans) and the 1749-to-now solar activity record. |
| 5 | `data-galaxy` | Vergely 2022 dust cube, Cepheids, globulars, masers, spiral arms, landmarks and nebulae with a citation each. |
| 6 | `data-lv` | Local Group and Local Volume galaxies with measured shape, LVDB plus giant hosts. |
| 7 | `data-lss` | Large-scale structure: 2MRS, Cosmicflows-4 groups, the Laniakea basin grid, the density grid, named superclusters and voids. |
| 8 | `data-cmb` | Planck SMICA to a cube map, with the inpainting mask and the analytic dipole. |
| 9 | `data-extras` | The fictional catalogue (renamed away from the source page's Marvel references), and the phenomena capture and exhibit texts. |

Start at step 0 and go down. Steps 3 and 0 can run in parallel; the rest need 0.

---

## 7. The rules that make this data honest

These are not style preferences. Each one is here because breaking it shipped something wrong.

- **Provenance is per field, not per object.** Every value is Measured, Inferred, Procedural or
  Fictional, with its source, and the app shows it. A default value (LVDB ships 542 galaxies with
  a placeholder ellipticity) must be flagged as Inferred, never passed off as Measured.
- **Never trust a FITS header's units.** Planck's `I_STOKES_INP` column is in µK while its TUNIT
  says K_CMB. Assert a physical range on every bake instead — for that map, an rms near 100 µK.
- **Known-answer tests, not tolerance-shaped ones.** Sirius −1.44 at 2.64 pc, M31 inclined about
  78°, the CMB Cold Spot near −112 µK, Laniakea 1.95×10⁶ (Mpc/h)³, the Crab pulsar at 29.6 Hz. A
  tolerance loose enough to pass is not a test: this project once asserted Jupiter's equatorial
  radius and passed for months on a value 2.2% away.
- **An artefact on disk is not evidence that the run meant to produce it happened.** Write to a
  temporary name and `os.replace` only on success; delete the target on failure. A previous
  importer here adopted a stale file this way and nearly shipped an inside-out spacecraft.
- **Importers are stdlib Python.** numpy is allowed as a declared exception for the FITS and
  HEALPix bakes, and must be recorded as such in `CLAUDE.md`.
- **Licences.** ESA archive data (Planck, Gaia, Hipparcos) is CC BY-NC 3.0 IGO — the current
  `docs/data-sources.md` says CC BY-SA and is wrong; fix it. VizieR tables from AAS journals are
  also non-commercial. LVDB is CC0, SDSS and NASA are public domain, HYG is CC BY-SA 4.0. Some
  sources (Skowron's Cepheids, the Cosmicflows grids) carry no redistribution licence at all: keep
  every such layer behind the licence switch in B §1.5 so it can be dropped without code changes.
  Assume non-commercial sideload distribution for now; the owner has not decided about a store.
- **If you cannot verify a number, say INFERRED.** Section B §9 lists what its author could not
  confirm; confirm those before relying on them.

---

## 8. Where the project stands, 2026-09-12

- Eight research reports and four design sections are written. The contract is being merged and
  critiqued now.
- No code has changed yet. `main` is at the same tree that built 1.9.0, tests green, and the Unity
  editor has not opened the project since 2026-08-01.
- In flight by the other agent: the tooling port (`tools/`, gates, worktree support) and then the
  foundation — URP and Linear colour, a single XR camera replacing the two-camera stack that most
  likely caused the grid-of-Earths bug, one axis convention, and the scaled-space placement every
  layer will use.
- Nothing in your half is blocked by any of that. Start at step 0.

When you finish a lane, leave the report and the branch, and say so — the integrator merges and
runs the gates. If you find something that contradicts section B, write it in your report rather
than silently doing something else: the next lane reads the section, not your commit.

---

## 9. Two constraints added by the owner on 2026-09-12

**Everything shipped must be usable commercially.** The owner's words: make sure the sources used
"can be used in commercial capacity to make visualization of real". That is now a hard filter, not
a preference, and it overrides parts of section B, which was written assuming non-commercial
sideload:

- Acceptable: public domain / US-government works (NASA, JPL, USGS, SDSS), CC0, CC BY with
  attribution, and permissive code licences.
- Not acceptable: anything CC BY-NC (which includes ESA archive data and VizieR copies of AAS
  journal tables), "free for scientific use", and anything with no stated redistribution licence.
- CC BY-SA is a conditional case: it permits commercial use but the derived data file inherits
  share-alike. Prefer an alternative; where none exists, ship that layer's derived file under
  CC BY-SA with its attribution and keep it separable from the rest.
- Every layer keeps a licence tag and a fallback, so any source that turns out unusable can be
  dropped without a code change.

The Claude session is doing this clearance and the fetching as grunt work: primary licence pages
read and quoted, commercial-clean substitutes chosen, raw sources staged on the box under
`~/projects/CosmoSynth/data-staging/<source>/` with `SOURCE.txt` (URL, date, sha256) and
`LICENCE.txt` beside each file, and the rows for `docs/data-sources.md` written. Read that staging
directory and the amendment note `docs/DATA-LICENCE-AMENDMENT.md` before you build an importer:
the source your section names may already have been replaced by a commercial-clean one.

**The app bridges the real universe to fictional worlds.** It is not a catalogue viewer with a
fiction mode bolted on. The design intent is that the fictional systems are reachable *through*
the real sky — you fly out along real stars and arrive somewhere authored — with provenance and a
distinct visual language making it unmistakable which is which at every moment. The hard design
work here is yours; keep the data side honest enough to carry it (fictional catalogues all-
Fictional, real positions never borrowed to legitimise invented objects).

**Division of labour from here.** You (Astra) take the design and the hard problems: the fiction
bridge, the scale ladder's feel, the Core physics and parsers, anything that needs judgement. The
Claude session does the grunt work: licence clearance, fetching and staging data, the build and
gate tooling, the render migration, and integration. Both of us write to lane branches; neither
merges the other's.

---

## 10. Things that landed after this note was first written (2026-09-12)

- **The owner's WebXR source pages are now on the box** at `refs/webxr/` — you cannot reach the
  laptop drive that the earlier sections name. `refs/webxr/nebula-gallery` is the Milky Way Atlas,
  `refs/webxr/pages` the rest. The directory has its own README explaining what each page is and
  which of them lie about their data. It is git-ignored on purpose: unsettled third-party licences.
- **The toolchain is merged into main** (`tools/`). Use it:
  - `bash tools/gate-all.sh` — runs every gate and prints ABSENT for any that does not exist, so
    "all green" can never mean "none ran". It is red on today's tree by design, and those failures
    are the Phase 0 worklist.
  - `bash tools/lane.sh new <name>` — creates your worktree.
  - `./run-tests.sh` — now works from a Library-less worktree on its own.
  - Every gate proves it can fail: each is fed deliberately broken input in its own self-test, and
    the settings gate also fails the historical 1.7.2 tree by name. If you add a gate, add its
    mutants too. A gate that cannot fail is worse than no gate.
- **What the gates say is wrong with the current tree**, for context: shaders carry their stereo
  macros but are not URP; colour space is Gamma; OpenXR depth submission, latency and symmetric
  projection are all at the wrong value; the deprecated Microsoft hand-interaction feature is on;
  there are six empty duplicate XR settings directories and five XR manager assets; five call
  sites reach `SetCatalog`; five components read `RenderedRadius` without checking
  `IsGeometryDrawn`; and axis swaps live in two files outside `FloatingOrigin`. The Claude session
  is taking these.
- **Main is committed and clean** at each step: docs first, then the toolchain merge. Branch from
  `main`, never commit to it.

---

## 11. Second brief: make it look like something

The owner saw the first render of the revived app on 2026-09-12 and said: "it just looks lame , empty dark space, I will have it run through astra to improve its overall engineering". That brief is **HANDOVER-ASTRA-VISUAL.md**, beside this file. Read it after this one. It carries the evidence for why the frame is dead, the bar to clear (the owner own Milky Way Atlas, copied to refs/webxr), the ordered list of what to fix, and the engineering defects the gates already name.
