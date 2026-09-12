# CosmoSynth 2.0 — CONTRACT

`docs/CONTRACT.md` · assembled 2026-09-12 from the four design sections of 2026-09-11
(`docs/design/2026-09-11-{A-foundation,B-data,C-product,D-process}.md`) and the eight research
reports (`docs/research/2026-09-11-*.md`). **This document supersedes all four sections.** Where a
section and this document differ, this document wins; the sections remain readable as the
reasoning behind it, and every ruling below cites the section it came from.

Line numbers and API names refer to git **`fc07a52`** on `master` (the baseline was `cf93a25`
when the design sections were written; the tooling lane merged at `49741aa` in between, so §8
and §10 are stated against `fc07a52` and every line number in them is re-verified there before a
lane card is handed out — R21). `U/` means
`unity/Assets/CosmoSynth/`, `SA/` means `unity/Assets/StreamingAssets/CosmoSynth/`,
`T/` means `tests/CosmoSynth.Core.Tests/`, `TC/` means `tools/cosmodata/`.

**Tags.** **VERIFIED** = read in the code/data on triton, measured by a 2026-09-11 research
report, or computed here from stated inputs. **INFERRED** = engine behaviour, a literature value
or an assumption that nothing on this box could check. Every INFERRED item of consequence is
listed in §13 with the check that settles it. Design *decisions* are not tagged; they are
decisions.

**What was verified for this contract (2026-09-12, read-only on triton).** Every existing API
name cited below was greped in the tree at `cf93a25`: `StandardFrames` (members and the matrix
build at `:105`), `ScaleSpace` (`RenderTransform`, `NavigationSpeed`, `DefaultBands`,
`SelectBand`), `FloatingOrigin` (`ToRender`/`ToAbsolute`/`SetUnitsPerKm`/`Focus`/`Rebase`/
`OriginShifted`/`IFloatingOriginListener`), `CosmoShaders.All` (8 entries, 6 of them built-in
names), `CosmoTheme` (`Warning` is byte-identical to the Procedural colour — CONFIRMED),
`XrInput.Bindings` (`(string Control, string Action)[]`, 9 rows), `SimulationClock.WarpRates`
(15 unsigned steps starting at 0.0), `CelestialBodyView` (`DrawnAsStarPoint`, `IsGeometryDrawn`,
`IsMarkerDrawn`, `IsPointable`, `RenderedRadius`, `SizeExaggeration`, `ToUnityRotation`,
`ToUnityDirection`, `ToIcrfDirection`, `PlaceForRendering`), `UniverseSimulation` (`SetCatalog`,
`AddBodies`, `RemoveBodies`, `MergeCatalogFromJson`, `LoadCatalogFrom*`, `ReadStreamingAsset*`,
`NearestBody`, `StarPointHorizonUnits`), `CelestialCatalog` (`Validate`, `RootIds`, `FromJson`,
`Traverse`, `MergeFrom`), `StarCatalog` (`CSS1`, `HeaderBytes 16`, `RecordBytes 24`, `Parse`,
`CountBrighterThan`, `ColorFor`, `StarFlags`), `Provenance` (`ProvenanceClass`,
`FieldProvenance`, `ProvenanceRecord.WeakestClass`), `CosmicTime`/`CalendarUtil`
(`MinimumSupportedYear = -4700`), `Hash.HashString`, `Constants` (`Parsec`, `KiloParsec`,
`MegaParsec`, `GM_Sun`, `LightYear`), `XrRig` (`nearClipPlane 0.05`, `farClipPlane 5000`,
`AddComponent<DepthBands>().Attach` at `:78`), `StarLook` (3.8/9.0 dmm, 0.40),
`SkyAndRings.BuildStarLight` reading `RootIds[0]` at `:585`, `TabletopManipulator`
(`_minimumUnitsPerKm 1e-20`, `_maximumUnitsPerKm 1e6`, `_targetUnitsPerKm`), the editor entry
points (`CosmoSynth.EditorTools.{ServerBuild.BuildAndroid, XrSetup.Run, PlayModeShot.Run}`;
`Version = "1.9.0"`, `VersionCode = 16`), `RenderDiagnostics.OneLine`, `run-tests.sh`, the three
asmdefs, `Packages/manifest.json` (URP 17.5.0, OpenXR 1.17.1, Meta OpenXR 2.5.0, **no explicit
ugui/TMP entry**; `com.unity.ugui 2.5.0` is a builtin transitive dependency), the absence of
`Assets/TextMesh Pro/`, the presence of `Assets/XR 1`, `XR 2`, `XR 3`, and `.gitignore` (no
`unity/.utmp/`). Nothing was built, launched or changed.

---

## 0. The contract on one page

### 0.1 What the app is

CosmoSynth 2.0 is **one Unity 6 (6000.5.5f1) Quest 3 mixed-reality app**, passthrough-first,
with an opt-in full-space (Space) mode and a desktop/editor path that exists for the headless
harness. It opens in your room with the real Earth in front of you as it is right now, and one
continuous scale control carries you up **nineteen rungs** — the sky over your city, low orbit,
the Earth and Moon, the planets, the heliosphere, the Oort cloud, the nearest stars, the local
clouds, the Milky Way, its halo, the Local Group, the Local Volume, Laniakea, the cosmic web,
and the observable universe wrapped in the cosmic microwave background. Every rung shows real,
cited data at real positions. Every object can be picked, inspected, measured and flown to.
Beside the real universe sit two clearly different kinds of content: **nine phenomenon exhibits**
on plinths, each reachable from the real object it explains, and **fictional sectors** (the
user's holographic star map, plus generated systems) shown only on a neon projector skin that is
never used for anything real.

What makes it CosmoSynth is the rule it already had: **every value says how it is known**
(Measured, Inferred, Procedural, Fictional) and **every presentation lie is named on screen while
it is active**.

### 0.2 The scale ladder

One variable, `FloatingOrigin.UnitsPerKm` (`U`, room metres per real kilometre), clamped once by
`ScaleLimits` to **[2e-25, 1e3]** — 27.7 decades. 1e3 is true scale (1 m of space = 1 m of room);
nothing is ever drawn larger than life. 2e-25 shrinks the observable universe — the **particle
horizon**, radius 14.26 Gpc = 4.4e23 km — to a 17.6 cm ball. The **CMB last-scattering surface**
is a different, smaller radius: chi* = 13.87 Gpc = 4.28e23 km. Both are named, and never
interchanged (R44).
Nineteen rungs in five groups (§5.1):

```
EARTH          sky(+3.00)  leo(+3.00)  planet-room(-3.71)  planet-hand(-4.63)  planet-moons(-6.15)
SOLAR SYSTEM   inner(-8.90)  system(-10.12)  heliosphere(-11.11)  oort(-13.73)
STARS          nearest(-14.51)  neighbourhood(-15.55)  clouds(-16.59)
GALAXIES       milkyway(-18.25)  halo(-19.42)  localgroup(-20.01)  localvolume(-20.84)
BEYOND         laniakea(-21.89)  cosmicweb(-22.71)  cmb(-23.87)        (values are log10 U)
```

Thirty-plus orders of magnitude reach one 24-bit depth buffer through **one distance map**
`f(d) = d` up to d0 = 10 m, `d0 * (1 + ln(d/d0))` beyond — 23.03 m of rendered depth per decade,
1e27 m at 608.7 m — applied as **one similarity per group about the centre eye**, never per body
(§2.3–§2.6).

### 0.3 The architecture in ten lines

1. `Core/` is engine-free C#, fully unit-tested headlessly: maths, frames, orbits, parsers,
   the layer table, travel, formatting. It never references `UnityEngine`.
2. `Runtime/` is Unity: one camera, one `FloatingOrigin`, one `UniverseSimulation`, one
   `ScaleRenderer`, and a set of `CosmoLayer` components.
3. **Bodies** (Sun, planets, moons, spacecraft, expanded exoplanet systems, fictional nodes) are
   `CelestialBodyView`s under one `CelestialCatalog`, adopted by a **single `SetCatalog`**;
   everything later uses `AddBodies`/`RemoveBodies`.
4. **Populations** (stars, galaxies, clusters, dust, CMB, grids, belts) are **not** bodies: each
   is a `CosmoLayer` — one mesh or a few — over an engine-free Core parser with tests (§4).
5. Every position is **double km, ICRF, SSB origin**. The ICRF-to-Unity axis mapping exists in
   exactly one place, inside `FloatingOrigin` (§3.1). The room-level plane is a separate rotation
   on the Universe host (§3.6).
6. Rendering is **URP 17.5 + Linear**, one XR camera, MSAA 4x from the URP asset, depth
   submission None, symmetric projection, SRP foveation, buffer discards on (§2.1, §8.1).
7. Every shader is a custom single-variant `CosmoSynth/*` URP HLSL shader with the full
   stereo-instancing macro skeleton; additive passes never write alpha into passthrough
   (§7, Appendix A).
8. Data ships as **one column-store container** per dataset under `SA/`, baked on triton by
   `TC/` importers, listed by `manifest.json` — the only list of files. The runtime never
   touches the network (§6).
9. Everything the user sees is generated from tables: `LayerTable.Layers` (46 rows),
   `LayerTable.Rungs` (19), `XrInput.Bindings`, `WarpLadder`, `Destination`s (§5).
10. Work is split into **git worktree lanes** with mechanical file ownership and seeded
    registration slots; **exactly one Unity editor** (the integrator's, behind a `flock`) builds,
    captures and publishes; six gates (G0–G5) plus a headset gate stand between a commit and a
    published APK (§9–§12).

### 0.4 The phase plan

| Phase | Where | Output | Rule |
|---|---|---|---|
| **0** (approx. 4–6 d) | main + 2 optional helper worktrees | **2.0.0-alpha.1**, versionCode 17 | the foundation only: no Phase-1a data merges before the alpha is published, so the user's headset A/B against 1.9.0 tests URP + one camera + scaled space alone |
| **1a** (parallel with 0) | 8 lane worktrees, **never an editor** | data files + Core parsers + tests | lanes touch only `TC/`, `SA/`, `Core/`, `T/`, their report and their `data-sources.md` slot |
| **1a merge** | main | master carries all data | after P0.7; FIFO by `READY-1A`; batches of at most 3 |
| **1b** (3 waves, at most 4 lanes) | the same lanes plus `ui`, `lines` | runtime layers, UI, navigation | one editor serves the capture queue; about 20–30 editor runs a day |
| **2** | main | **2.0.0-beta.N**, then **2.0.0** | full G0–G5, headset each beta |
| **3** | main + small lanes | audio, tuning, docs, store work *only if asked* | |

Roughly **36 agent-days**, about 3 weeks wall clock (INFERRED).

### 0.5 OPEN DECISIONS for the user

Each has a default the design proceeds on. Only **D1, D2 and D3** can block anything, and only
at the gate named.

| # | Decision | Default taken | Needed before |
|---|---|---|---|
| **D1** | **Licence profile.** `sideload-nc` ships every source, including the non-commercial ones (ESA/Planck, A&A via CDS, AAS via VizieR, SILSO) and two with no redistribution grant at all (Skowron Cepheids, CF4 grids). A commercial release needs a lawyer on the HYG/AT-HYG question (CC BY-SA files carrying ESA-derived Gaia and Hipparcos values) and would drop or substitute five layers. | `COSMODATA_LICENCE=sideload-nc`; private sideload only; no commercial claim made anywhere. **And the process now keeps it private** (R46): while the profile is `sideload-nc` the APK stays behind the build page's login and is never attached to a public page, a store listing, a release asset or a public repository; `SA/` files whose ledger row is tagged `unclear` are listed in `.gitignore-export` and excluded from any mirror push; the notes file beside every build carries the licence line (§12.2); and G3 **fails** with `STORE=1` on any `unclear` or `nc` tag (§11.4) | any public store listing |
| **D2** | **Signing key.** Debug-signed (`~/.android/debug.keystore`, cert SHA-256 `e39a7703...4a17`) installs over 1.9.0 with no uninstall. A release keystore must be created **before any Meta upload**, because the first upload locks the app to that certificate forever (memory `meta-cert-locked-by-first-upload`). | debug-signed for all of 2.0 | the first Meta upload on any channel |
| **D3** | **Store at all?** Sideload only, or a Meta listing in Phase 3? | sideload only; Phase 3 store work happens only if asked | Phase 3 |
| **D4** | **Test 1.9.0 on the headset now** (gate P, §11.7). Three questions; it is the A/B reference for the alpha and the only way to learn whether the 1.7.1 MSAA revert already killed the grid. | please do it now; it does **not** block Phase 0 | nothing, but the sooner the better |
| **D5** | **Sun disk stills plus one SUVI 304 loop** (about 2.2 MB + about 3 MB): in 2.0.0, or after? Until they ship, the Sun's VITALS imagery row reads "not in this build". | after 2.0.0 | Phase 2 content freeze |
| **D6** | **Is 1:1 the right top of the ladder** (nothing ever drawn larger than life)? | yes, `MaxUnitsPerKm = 1e3` | Phase 1b `nav` |
| **D7** | **Default site for the "Sky from Earth" rung.** | Helsinki (60.17 N, 24.94 E) | Phase 1b `nav` |
| **D8** | **Keep the holo-map node names** (Alpha Core Prime, Chrono Frontier, Searing Fragment ...) now that the `MARVEL_COSMOS` header is dropped? | kept — they are generic English phrases carrying no third-party mark | Phase 1a `fiction` |
| **D9** | **May a fictional system ever be placed at a real star?** | not in 2.0 | — |
| **D10** | **Ambient sound on by default**, about -24 dBFS, mute persisted? | on | Phase 3 `audio` |
| **D11** | **Resume the previous session's view on launch?** | no — always Earth, at now | Phase 1b `ui` |
| **D12** | **The Earth and Mars globe pages** (EARTH-XR, ARES ATLAS, TERRA DECK): fold them into the planet rungs later? | **decide after the B.14 inventory** — nobody has opened these six files, so "out of scope" was a guess. The `solar` lane reads them in its 1a half and appends B.14 (R49); the decision is taken against that table | Phase 2 content freeze |

**Resolved by the orchestrator, not the user** (recorded so nobody re-opens them): the Phase-0
helper lanes `p0-core` and `p0-shaders` **are used** (saves about 1–1.5 d); gate P does **not**
block P0.4; Multiview Render Regions (MVPVV) stays **off** for 2.0 (§11.4); text uses **legacy
`TextMesh` with `CosmoSynth/Text`**, not TextMeshPro (§1.2 C3); star positions stay **f32**
(§1.2 C11).

---

## 1. Rulings

### 1.1 The nine binding decisions and where each lives

| # | Binding decision | Implemented in |
|---|---|---|
| 1 | One app, name kept, MR passthrough-first with opt-in full space; desktop path for the harness; next release 2.0.0, versionCode at least 17, never overwrite a published APK | §0.4, §5.9.5, §12.1, §10.4 step 2 |
| 2 | URP 17.5 + Linear; exactly one XR camera (`DepthBands` deleted); depth submission None; symmetric projection on; latency `PrioritizeInputPolling`; MSAA 4 from the URP asset; SRP foveation on; Optimize Buffer Discards on; single-variant `CosmoSynth/*` URP HLSL with the full stereo skeleton; additive passes never write alpha; `ZTest LEqual` unless justified | §2.1, §7, §8.1, Appendix A, gates G0b/G0c/G0d |
| 3 | Near content linear and depth-tested; far content via a **per-group** similarity about the eye; a sky layer at infinity drawn first, eye-anchored; the formula and constants chosen with justification | §2.2–§2.7 |
| 4 | One ICRF-to-Unity mapping, applied only inside `FloatingOrigin`; every ad-hoc axis swap deleted | §3.1, §8.2 item 13, gate G0d |
| 5 | Catalogue loads merged in Core; `SetCatalog` runs once; later additions use `AddBodies`; populations are layer renderers over engine-free Core parsers with tests | §4, §6.6, gate G0d, assertion `catalog` |
| 6 | The runtime never touches the network; data baked on triton by `TC/` importers (stdlib Python; numpy a declared exception for FITS and HEALPix), temp-then-`os.replace`, per-field provenance, physical range assertions, never trusting FITS `TUNIT` | §6.1–§6.3, §6.5 |
| 7 | Non-commercial sideload assumed; every source's licence and attribution recorded in `docs/data-sources.md` (ESA archive data is **CC BY-NC 3.0 IGO**, not CC BY-SA); licence-clean fallbacks behind one switch; any source without a redistribution grant droppable with no code change | §6.2.2, Appendix D, gate G0f (`drop_test.sh`) |
| 8 | Core never references `UnityEngine`; one MonoBehaviour per file named after the class; colour only from `CosmoTheme`; UI sizes angular (dmm); every binding in `XrInput.Bindings`; anything decorating a body checks `IsGeometryDrawn`; numeric tolerances justified | Appendix F (lane preamble), gates G0d/G0e/G0g |
| 9 | Lanes are Library-less git worktrees at `~/projects/CosmoSynth-wt/<lane>` on `lane/<name>`, compiling via `run-tests.sh` against main's `ScriptAssemblies`; exactly one Unity editor behind a `flock`; Xvfb band 461–490; tools ported from Worldfall | §9, §10 |

### 1.2 Conflict register

Every place two drafts disagreed, the ruling, and the one-line reason.

| # | Conflict | Ruling | Why |
|---|---|---|---|
| **C1** | Shader names: A §5.8 (`Body`, `Point`, `Ribbon`, `Ring`, `Panel`, `CosmoCore.hlsl`) versus D P0.4b (`Planet`, `Points`, `Line`, `UnlitTransparent`, `CosmoCommon.hlsl`) | **A's names stand**, everywhere, including `CosmoShaders.All` and every lane card | D §0 gives the Foundation authority over rendering; one set of names must exist before P0.7 seeds the slots |
| **C2** | `Destination`: A's `Core/Catalog/LayerRecords.cs` record versus C's `Core/Nav/Destination.cs` record (with Kind, BodyId, RoomMetres, Plane, Source) | **C's record at `Core/Nav/Destination.cs`**, with A's `LayerId` added as a field; `Core/Catalog/LayerRecords.cs` is not created and `LayerBudget` moves to `Core/Layers/LayerBudget.cs` | travel framing is a product decision and C's record carries everything A's did |
| **C3** | Text stack: A §5.8 and §9.10 (legacy `TextMesh` + `CosmoSynth/Text`) versus D P0.4a/b/d (TextMeshPro, `TMP_SDF-Mobile` in `CosmoShaders.All`, TMP Essentials imported headlessly) | **Legacy `TextMesh` + `CosmoSynth/Text`.** TMP is not used in 2.0. D's TMP import step, its TMP `<Reference>`, its `FONT_ASSET="LiberationSans SDF"` and its TMP entry in the stereo gate are all dropped | binding 2 forbids a multi-variant third-party shader; `Assets/TextMesh Pro/` does **not** exist (VERIFIED), so TMP costs a flaky headless `ImportPackage`, new committed assets and an unverifiable `ShaderUtilities` property contract, while `CosmoText` already fetches `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` (VERIFIED `CosmoText.cs:50`) and already handles `Font.textureRebuilt`. Escalation if the headset says text is unreadable: port `TMP_SDF-Mobile` as `CosmoSynth/TextSDF` in Phase 3 — never TMP's own shader name |
| **C4** | Glyph set: C's panel copy uses transport and tick symbols outside ASCII | **ASCII transport glyphs by default** (`<<`, `\|\|`, `>`, `>>`, `[]`, `<`, `>`, `^`, `v`, `x`, `+`); the richer set is opt-in **only after** the play-mode `text` assertion reports `Font.HasCharacter` true for it (§5.6.9) | a dynamic legacy font silently drops a missing glyph; the gate, not a guess, decides what the panel may say |
| **C5** | `EquatorialToGalactic` is wrong in the shipped code (`StandardFrames.cs:105-108`, all three signs flipped; NGP maps to (-0.346, 0.176, 0.922); up to 1.75 per element off Hipparcos) | **Fixed at P0.3, in the same commit as the Hipparcos test** (A §2.2 and B §1.6 ask for the same test; it fails on today's code) | three lanes depend on it and it has zero test references today |
| **C6** | Lane data paths: D's `SA/milkyway/**`, `SA/galaxies/**`, `SA/cmb/**`, `SA/fiction/**`, `SA/smallbodies/**`, `SA/helio/**`, `SA/exoplanets/**` and importers `build_milkyway.py`, `build_galaxies.py`, `build_fiction.py` versus B's actual layout | **B's layout and importer names** (`sky/`, `sun/`, `galaxy/`, `lv/`, `lss/`, `cmb/`, `catalog/`, `phenomena/`; `build_starfield`, `build_exoplanets`, `build_small_bodies`, `build_heliophysics`, `build_dust`, `build_galaxy_points`, `build_localvolume`, `build_lss`, `build_cmb`, `build_authored`); `tools/lanes.txt` is rewritten in §9.3 | B owns formats and importers; a lane cannot own a path that does not exist |
| **C7** | Shared importer helper: D's single `TC/cosmoio.py` versus B's five modules | **B's five**: `csdata.py`, `fetch.py`, `fits.py`, `licence.py`, `ledger.py`, all integrator-owned, all delivered at P0.2. `cosmoio.py` is never created | one atomic-write and readback implementation, one licence switch, one manifest generator |
| **C8** | Star file: D's `stars` card says "keep CSS1 unless Data/Core defines a v2" | **CSS2** (`sky/stars.csstars`), with `StarCatalog` rewritten on `CsData` in the same lane half; a `CSS1` file is refused by name | B defines v2 and the rewrite is Core-only, so it stays inside the 1a envelope |
| **C9** | Stars source: a naive AT-HYG drop-in fails the shipped Sirius -1.44 test (AT-HYG gives -1.088; 118,237 of 119,670 magnitudes are Tycho-derived) | **Accepted as designed**: astrometry, distances and velocities from AT-HYG v4.0 `hyglike`, magnitudes, colours and spectra from HYG v4.4, joined 1:1 on the HYG id (119,614 matched rows, every `hip` agreeing) | it is the only join that keeps both the modern astrometry and the shipped naked-eye counts (5,070 at V at most 6.0; 8,920 at 6.5) |
| **C10** | Exoplanets: D's additive rule says keep `catalog/exoplanets.json` until the 1b loader ships; B deletes it and makes `RootIds[0] == "sun"` | **P0.4d deletes the exoplanet merge and `catalog/exoplanets.json` together**, and updates `ShippedCatalogTests` in the same commit. 2.0.0-alpha.1 has no exoplanets, and its handoff says so | A §7.2 item 9 removes `MergeCatalogFromJson` at P0.4d anyway; keeping a 330 KB file nothing reads would break the "manifest is the only list" rule |
| **C11** | Star position precision: f32 pc (at most 6e-8 of \|r\|, about 17 AU at 1.4 kpc) versus an f64 column (+1.44 MB) | **f32 stays.** The bound goes into `docs/accuracy.md` and the `stars` lane report | the only failure it fixes is a few pixels for pairs closer than 0.1 pc seen from more than 1 kpc away, and the promoted `NearbyStarSystems` path already covers the case anyone can actually visit |
| **C12** | Distance-unknown star count: A's budget row says 10,224 (today's CSS1) | **146** (AT-HYG, VERIFIED probe), so the sky-star layer is 584 vertices, not 41 k. A's ceiling stays as a ceiling | the number changed with the catalogue |
| **C13** | `ProvenanceHud`: A §7.3 item 22 gates it behind `!XrActive`; C §5.1 deletes it | **Deleted at P0.4d**, with a minimal status card (`AnchorBeacon` plus `CosmoText`) taking its place until `ui` lands the real one in 1b | it runs IMGUI every frame on the headset and duplicates the provenance UI; the alpha still needs the `render:` line on screen, which the status card carries |
| **C14** | Gate scripts: A's `tools/gates/foundation.sh` (six gates, self-test VERIFIED on the laptop) versus D's `stereo_gate.sh` / `code_gate.sh` / `settings_gate.py` | **D's file names are canonical; A's rules and regexes are folded into them** (§11.1 and Appendix H). `foundation.sh` is not created | two scripts checking the same invariant is how one of them rots |
| **C15** | Rung ids: D's capture views use `rung:local-stars`, `rung:milky-way`, `rung:solar-system`, `rung:local-group`, `rung:2mrs`, `rung:observable-universe` | **C's 19 ids** (§5.1). Mapping: `local-stars` to `nearest`, `milky-way` to `milkyway`, `solar-system` to `system`, `local-group` to `localgroup`, `2mrs` to `laniakea`, `observable-universe` to `cmb` | one id namespace drives the panel, the harness and `release` view expansion |
| **C16** | `LayerState.Weight(id, log10U, ctx)` — A asked C to name the context type; C never did | **`Core/Layers/LayerContext.cs`**, an engine-free readonly struct built once per frame by `ScaleRenderer` and carried in `FrameContext.LayerContext` (§4.2) | there must be exactly one context type and it must be testable without an engine |
| **C17** | Layer interface: A's `CosmoLayer` lacks D's required `SelfCheck()`, `LabelCandidates()` and a per-layer blend kind | **Added to `CosmoLayer`**: `SelfCheck(List<string>)`, `CollectLabels(List<LabelCandidate>)`, `abstract LayerBlend Blend` (§4.1) | the `layers`, `alpha` and `text` play-mode assertions are built on them |
| **C18** | MVPVV (Multiview Render Regions): D needs it decided to pick the expected xr-block MD5; A predicted the block would change | **MVPVV off.** Expected xr-block MD5 `2e1fc6ccd7f09d6cbe013eb1c74e9e02` (1.9.0 = Mechanarium 1.0.1 = Microverse 0.1.6, VERIFIED). Worldfall's `9dd8284e0dd84f1eb53cd40c80cd145d` differs only in `xr-mvpvv-enabled=2` and is **not** accepted for 2.0 | the two known blocks differ only in that one key, so the block does not encode depth submission or symmetric projection; keeping it off removes one unverifiable variable from the first alpha |
| **C19** | `Core` folder ownership collided: D gives `Core/Sky/**` to `stars`, but B puts `CmbSky`, `CmbModel` and `ExoplanetTable` there and A puts `PointChunks` there | **Relocations**: `Core/Cmb/{CmbSky,CmbModel}.cs` (cmb), `Core/Exo/ExoplanetTable.cs` (exoplanets), `Core/Scale/PointChunks.cs` (integrator). `Core/Sky/**` belongs to `stars` | one folder, one owner, checked mechanically by `own_gate.py` |
| **C20** | C's data gaps G1–G10 are absent from B's plan | **Assigned** (§6.9.2): G2, G3, G4, G5, G9 to `solar` 1a; G7 to `stars` 1a; G10 to `nav` 1a; **G1 deferred** (user decision D5); **G6 and G8 deferred** to Phase 3 with their layers reading "not in this build" | each is small and belongs to a lane that is already in that data |
| **C21** | `docs/data-sources.md`: B rewrites the whole file; D gives the integrator the file with per-lane slots | **Both**: the integrator rewrites it at P0.2 into B §6's structure with the ledger table pre-filled, and seeds one `lane:<name>` slot per lane inside the per-source notes | the rewrite is a Phase-0 act; the slots are how lanes add rows without conflicting |
| **C22** | Data budget: D's `data-budget.txt` initial 80 MB | **50 MB.** B's projected `SA/` total is about 36 MB (6.7 today plus 29.3 added); 50 leaves room for `--face 2048` only as a deliberate integrator commit | a budget that can never be hit is not a budget |
| **C23** | C §4.3 says 548 star names; C §4.2 gives `globulars` the provenance "LVDB, Baumgardt" | **640 names** (AT-HYG, VERIFIED probe); globular provenance cites **LVDB only** (which itself cites Baumgardt 2021). Baumgardt's table is an importer-side cross-check and is **never redistributed** | its licence is unstated |
| **C24** | `Core/Presentation/**` ownership (C lists it under `ui`, but `ScaleLimits` is needed at P0.3) | `ScaleLimits.cs` and `PresentationPlane.cs` are **integrator-owned**, created at P0.3; `Format.cs` and `SizePresets.cs` are **`ui`-owned** from 1b | `FloatingOrigin.SetUnitsPerKm` clamps through `ScaleLimits` in Phase 0 |
| **C25** | B's `data-*` build order (10 steps) versus D's 8 vertical lanes | **D's lanes**, with B's steps mapped onto them (§6.9.1); B step 0 becomes **P0.2** and B step 3 splits across `solar`, `milkyway`, `cmb`, `galaxies` and P0.3 | one process model; the mapping loses none of B's ordering constraints |

### 1.3 Corrections to the drafts, carried into this contract

1. A §6 budget: the distance-unknown sky-star row is 146 stars and 584 vertices, not 10,224 and
   41 k (C12).
2. A §8.3: the xr-block MD5 does **not** change for 2.0 (C18).
3. A §7.3 item 22 and §10 P0.4d: `ProvenanceHud` is deleted, not gated (C13).
4. A §2.4: `PointChunks` lives at `Core/Scale/PointChunks.cs` (C19).
5. B §2.6 and §2.13: `CmbModel` and `CmbSky` live under `Core/Cmb/`; `ExoplanetTable` under
   `Core/Exo/` (C19).
6. C §4.3: 640 star names, not 548 (C23).
7. C §4.2 `globulars`: provenance cites LVDB only (C23).
8. D P0.4a/P0.4b/P0.4d: no TextMeshPro (C3); D §D3.1 `FONT_ASSET` becomes `LegacyRuntime.ttf`;
   D §D3.4's font check becomes the §11.4 form; D §D3.7 G0b no longer scans
   `Assets/TextMesh Pro/Shaders/TMP_SDF-Mobile.shader`.
9. D §D2.15 `tools/lanes.txt` is replaced wholesale by §9.3.
10. D §D1.3's additive rule keeps its force everywhere except exoplanets, where P0.4d has
    already removed the reader (C10).
---

## 2. Foundation: camera, distance, depth, groups

Authority: Foundation section A. Every lane that renders, places or orients anything obeys this
part.

### 2.0 The constants, in one place

All of these live in `Core/Scale/ScaledSpace.cs` (§2.7). Shaders receive the ones they need
through `_CS_Compress`. **No other file may restate a number from this table.**

| Name | Value | Meaning |
|---|---|---|
| `IdentityRadiusMetres` (d0) | 10.0 | inside this rendered distance placement is exact (s = 1) |
| `RigidEnterMetres` / `RigidLeaveMetres` (H, with hysteresis) | 9.0 / 11.0 | a rigid group's rendered-radius cap |
| `NearPlaneMetres` | 0.05 | hands and wrist panels come closer than the usual 0.3 (`XrRig.cs:70`, VERIFIED) |
| `FarPlaneMetres` | 2000 | at least 1.33 times the largest placed distance |
| `SkyRadiusMetres` | 1500 | radius of the per-eye sky sphere; also the cap for enclosing volumes |
| `DepthBits` | 24 | expected eye depth format D24_UNORM_S8 (INFERRED; logged on device, §11.6) |
| `PushMarginQuanta` | 2 | inter-group clearance, in depth quanta |
| `NearPointFloorMetres` | 5.0 (d0/2) | a push never brings a group's near point closer than this |
| `IpdMetres` | 0.063 | nominal interpupillary distance. Half-IPD 0.0315 m is the synthetic eye offset the `stereo` assertion uses. **No other section restates 63 mm; they name this constant** (R15) |
| `PixelsPerRadianCentre` | **about 1140** (INFERRED) | `0.5 * eyeHeightPx * cot(fovY/2)` — the value `CS_PixelsPerRadian()` (A.2) and `FrameContext.PixelsPerRadian` actually return. **1432 px/rad (25 px/deg) is the panel *average* over the FOV and must not be used for any on-screen size.** From the report's Quest 3 figures (2064 x 2208 per eye, about 110 x 96 deg; quest-rendering-techniques line 982, INFERRED) the centre value is `0.5 * 2208 * cot(48 deg)` = **994** at 96 deg vertical, or **1143** at 88 deg; Symmetric Projection (§8.1) widens the per-eye frustum and lowers it again. Every pixel figure in §2.4, §2.5, §5.6.10 and Appendix A is restated against 1140 and marked INFERRED; the `stereo` assertion reports the measured value (§11.3) (R8) |

`ScaleLimits.MinUnitsPerKm = 2e-25`, `ScaleLimits.MaxUnitsPerKm = 1e3` live in
`Core/Presentation/ScaleLimits.cs` (§5.1) and are the only scale clamp in the codebase.

### 2.1 One camera

`XrRig` creates exactly one camera and nothing else may add another (gate G0d counts
`AddComponent<Camera>` and requires exactly 1).

| Setting | Value | Note |
|---|---|---|
| near / far | 0.05 / **2000** m | `XrRig.cs:70-71` today says 0.05 / 5000 (VERIFIED); far becomes `ScaledSpace.FarPlaneMetres`. Set once in `BuildRig`, never re-fitted per frame |
| clear | `SolidColor`; (0, 0, 0, 0) in passthrough; (0.004, 0.005, 0.011, 1) in full-space VR | Meta's passthrough rule (`ARCameraFeature.cs:194`, render-xr-audit §3) |
| `allowMSAA` | true | the URP asset supplies 4x; with a single camera, Optimize Buffer Discards makes it memoryless and therefore safe |
| `allowHDR` | false | |
| URP camera data | `renderPostProcessing=false`, `antialiasing=None`, `requiresDepthTexture=false`, `requiresColorTexture=false`, `renderShadows=false` | through `camera.GetUniversalAdditionalCameraData()` |
| `TrackedPoseDriver` | unchanged (`UpdateAndBeforeRender`) | |

Why one camera, in one line: the 1.7.0 "100 plus Earths on a grid" had a verified set of
ingredients, and camera stacking made the second pass **load** a memoryless MSAA attachment
(render-xr-audit §2.2). With one camera that attachment is never loaded.
`Runtime/Rendering/DepthBands.cs` (157 lines) and every reference to it are deleted (§8.1 item 5).

### 2.2 The three placement modes and the render queues

Every renderer is in exactly one placement mode.

| Mode | Used by | How positions reach the screen | Depth |
|---|---|---|---|
| **Rigid** (similarity about the centre eye E) | body groups (planetary and star systems, fictional holograms), bounded volumes, the CMB object form, ribbons owned by a rigid group | the CPU sets the transform: `localPosition = E + s*(p - E)`, `localScale *= s`; the shader is unaware | exact inside the group; §2.6 orders groups against each other |
| **Per-vertex** (radial compression about E) | point populations, ribbons whose owner contains the eye, "envelope" bodies the eye is close to | the vertex shader computes `CS_CompressWS(posWS)`: direction exact, distance `f(d)` | order along every ray is exact, because f is monotonic |
| **Sky** (direction only, per eye) | the CMB inside form, the 146 distance-unknown stars, the horizon grid | the vertex shader computes `CS_EyePositionWS() + dir * 1500` (R1: **never** `GetCameraPositionWS()`) | none: drawn first, no depth write |

Near content is not a separate mode. It is content with `s = 1`, which is automatic whenever
`D + R <= d0`.

**Render-queue table.** This is the single ordering; every lane uses these numbers.

| Queue | Content | Blend (colour, alpha) | ZWrite | ZTest | Writes passthrough alpha? |
|---|---|---|---|---|---|
| 1000 | Sky: CMB | `One Zero` (the first draw of the frame) | Off | LEqual | equal to `_Immersion` (0 in MR, so the layer skips the draw; Space mode sets 1) |
| 1010 | Sky: direction-only stars, horizon grid | `One One, Zero One` | Off | LEqual | no |
| 2000 | Opaque: bodies, spacecraft, beacon bars, pointer ray | opaque | On | LEqual | yes, 1 — an object occludes the room, correctly |
| 3000 | Rings | `One OneMinusSrcAlpha, One OneMinusSrcAlpha` (premultiplied over) | Off | LEqual | coverage = opacity |
| 3010 | Bounded and enclosing volumes (`dust`, `nebulae`) | premultiplied over, `Cull Front` | Off | LEqual | coverage = opacity |
| 3050 | Point populations, body billboards and markers | `One One, Zero One` | Off | LEqual | no |
| 3060 | Ribbons: orbits, constellations, grids | `One One, Zero One` | Off | LEqual | no |
| 3100 | Atmospheres, selection rim, heliosphere shells | `One One, Zero One` | Off | LEqual | no |
| 3200 / 3201 | Panel backing / accent | premultiplied over | Off | LEqual | coverage |
| 3210 | Text | premultiplied over | Off | LEqual | coverage |

`ZTest Always` is forbidden unless the pass carries a `// ztest-justified: <reason>` comment; the
gate enforces it, and no pass in this design needs it.

### 2.3 The compression function: choice and proof

**Definition** (identical in Core double and shader float):

```
f(d)    = d                          for d <= d0            d0 = 10 m
f(d)    = d0 * (1 + ln(d / d0))      for d > d0
f^-1(y) = y  (y <= d0),  d0 * exp(y/d0 - 1)  (y > d0)
s(d)    = f(d)/d                     (= 1 for d <= d0)
```

**Properties** (VERIFIED algebraically and numerically):

1. **Identity inside d0.** The room, the hologram and the wrist are exact.
2. **C-1 at d0.** f(d0) = d0 and f'(d0+) = 1. Numeric slopes either side are 0.9999999993 and
   0.9999999477 at h = 1e-6.
3. **Strictly increasing**, f' = d0/d > 0, so depth order along any ray is preserved.
4. **Each decade costs exactly d0 * ln 10 = 23.03 m.** Every scale of structure gets the same
   share of depth, which a 30-decade universe needs.
5. **Bounded in practice.** f(1e27 m) = 608.67 m. The largest reachable render distance is the
   CMB at `MaxUnitsPerKm = 1e3`: 4.28e26 m, f = 600.2 m. A rigid group adds at most H = 10 m, so
   every placed opaque surface lies within 680 m; only enclosing volumes reach 1500 m.
6. **Angular size is exact** under the group similarity, `asin(s*R / (s*D)) = asin(R/D)`, and
   under per-vertex compression, because every vertex keeps its direction from E.

**Known-answer vectors** (these are the test rows of §2.7):

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

**Why not exp-saturation** (the render-xr-audit §6.2 proposal, D0 = 50 m, Dmax = 900 m): it
saturates. f(6 km) = 899.22 m is already within one D24 quantum (0.97 m at 900 m) of Dmax, and
f(20 km) is the same float value. Everything beyond about 6 km of render distance would land on
one depth value — at the Earth-hologram scale, everything beyond about 1 AU. Group order across
scales becomes impossible, which is the one thing the far layer must provide. Rejected.

**Why d0 = 10 m and not smaller.** The relative separability thresholds scale linearly with d0
(§2.4), so d0 = 5 halves them. The cost is that content 5–10 m away would be gently compressed,
so walking toward it would feel rubbery; angular size and direction stay exact either way, only
vergence changes. d0 = 10 already gives at least 1.8 depth quanta for the tightest real case, so
it stays at 10 **as a calibration knob**: it is the first thing to halve if the headset shows
z-fighting, and it is one constant.

### 2.4 Depth separability on D24 across 1e-3 to 1e27 m

With a UNORM depth buffer, near plane n and N bits, one quantum at view distance z is
`dz(z) = z^2 / (n * 2^N)` when far is much greater than near. Reversed-Z does not change this for
UNORM (quest-rendering-techniques §3.1). Below 0.05 m nothing is drawn. From 0.05 to 10 m content
is exact: dz(1 m) = 1.2 um, dz(10 m) = 0.12 mm.

| true d (m) | f (m) | dz(f) (m) | **inside a group**: dz/f | **per-vertex, between rays**: dz/(f'*d) = dz/d0 |
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

- **Inside a rigid group** (the column that matters for occlusion): two surfaces whose true
  separation along a ray exceeds **8.0e-4 of their distance** are ordered correctly at any scale.
  A group of apparent radius a pixels therefore resolves features separated by more than
  `8.0e-4 * PixelsPerRadianCentre / a` of its own radius: at **1140 px/rad** (§2.0, INFERRED)
  that is **0.9 % at 100 px and 9 % at 10 px**. (At the panel-average 1432 the same expression
  reads 1.1 % and 11 %; those were the old figures and are superseded — R8.) Real cases:
  - Io transiting Jupiter seen from Earth at true scale (1000 m/km): separation 5.6e-4 against a
    threshold of 3.1e-4 — 1.8 quanta, correct.
  - Saturn's back ring against its disc: correct once Saturn is wider than about 3 px.
  - The ISS 400 km above Earth (6 % of R_Earth): correct.
  - Landers on a surface (gap about 0): never correct by depth, which is why a lander is drawn at
    true scale or as a marker that writes no depth (`CelestialBodyView.cs:622-641`).
- **Between groups:** not a precision question, because §2.6 enforces at least 2 quanta of
  clearance by construction.
- **Per-vertex or per-point content** never writes depth, except envelope bodies, whose near and
  far sides are separated by a whole diameter.
- **With D16** (today's `Depth16Bit` submission) the inside-group threshold becomes 3.3e-3 at
  11 m and 0.19 at 1e27 m — unusable. Nothing here is designed to work on D16; the depth format
  is logged on device and a 16-bit format turns the `render:` line red (§8.3 item 23).

**Float precision of f in shaders.** `log` in float has a relative error of about 1e-7, so the
error in f at 600 m is about 6e-5 m, roughly 7,000 times smaller than dz(600 m) = 0.43 m.

### 2.5 Stereo-disparity correctness

Disparity at rendered distance y is `IpdMetres / y` (§2.0). Pixel figures below use
`PixelsPerRadianCentre` = 1140 (§2.0, INFERRED), so 1 px = 0.88 mrad. They were previously stated
at 1432 px/rad and are about 20 % smaller now (R8, R15).

1. **Geometric consistency.** All compression happens about **one** point, the centre eye
   `_CS_CentreEyeWS`, set once per frame from the camera transform. Both eyes therefore view one
   rigid 3D scene: no binocular rivalry, no vertical disparity, consistent vergence.
   Compressing about each eye separately would give the two eyes different geometry and **is
   forbidden**.
2. **Exact near.** For d at most d0 the scene is true, so disparity is true (at least
   **7.2 px** at 10 m: `0.063 * 1140 / 10`).
3. **Monotonic everywhere.** Disparity `IpdMetres / f(d)` falls strictly as true distance grows:
   **7.2 px** at f = 10 m, **1.4 px** at f = 50 m, **0.12 px** at f = 600 m. The sky gets exactly 0, because it is
   placed per eye. So near < compressed < sky in perceived depth, always.
4. **Head-motion parallax is exact, not compressed.** Placement is recomputed every frame about
   the *current* eye, so the rendered direction to every object is its true direction from the
   eye. Walking one metre moves the Sun by exactly its true parallax. Only binocular disparity
   reflects f.
5. **Bounded distortion.** Compressed content shows more disparity than the truth (at most
   **7.2 px** instead of about 0), and always appears **behind the room**: rendered near points
   stay at least 5 m, and passthrough walls at 2–5 m carry **14–36 px** of disparity.
6. **Per-eye angular error of rigid groups** (compressed about the centre, viewed from plus or
   minus 32 mm): relative at most 0.032/f(D), which is at most 0.3 % at 10 m.

Only a headset can say whether **1–7 px** of far disparity is comfortable (§11.6 H4).

7. **Shading noise must be eye-consistent too.** Geometric consistency is not enough: a per-pixel
   jitter decorrelates between the eyes, because the two eyes rasterize one world point at
   different pixel coordinates. Any stochastic term in a stereo pass is therefore a function of
   the **world or object-space ray**, never of `positionCS.xy` (A.2 `CS_JitterDir`, R7).

### 2.6 Groups

**Definition.** A group is a set of renderers that share one similarity per frame. There are two
kinds:

- **Body group**: a subtree of the catalogue tree. Its **anchor** is the subtree root body's
  position, not a bounding-sphere centre — that choice is what makes a per-vertex ribbon pass
  exactly through a rigid group's rendered centre, because at the anchor `f(D)*u = s*D*u`. The
  group's radius R is the maximum over drawn descendants of
  `|p_i - p_anchor| + drawnRadius_i`, recomputed every frame (O(n), n about 250), plus 10 %.
  Examples: a planetary system; a star system (the 4,769 exoplanet hosts are roots once
  expanded); a fictional hologram system.
- **Layer group**: declared by a layer through `DeclareGroups` (§4.1). Examples: the dust volume,
  the CMB object form, a hero galaxy impostor, a phenomenon exhibit, a nebula close-up box, the
  heliosphere shell.

**Not groups.** Point populations (stars, galaxies, clusters, belts, Cepheids) are placed per
point in the vertex shader. A point has no extent, so the per-body trap — a moon vanishing behind
its planet — cannot arise, and per-point f is exact for them. Sky content and per-vertex ribbons
are not groups either.

**Modes per node** (the output of the solver):

| Mode | When | Scale |
|---|---|---|
| `Collapsed` | an ancestor is Rigid | the ancestor's s |
| `Rigid`, owning the subtree | `s*R <= H` for the whole subtree (rendered radius at most 10 m; hysteresis 9/11) and the eye outside (D > R); all descendants Collapsed | s = f(D)/D |
| "dissolved" (reported as `OwnsSubtree = false`) | a node with children failing that test: its own body is then tested as a leaf, and each child is tested separately | the leaf's |
| `Envelope` | a **leaf** (one body and its decorations) failing the Rigid test: the eye is near a body that is big on screen | 1, and the body's materials set `_PerVertexCompress = 1`. **Subject to the tessellation rule below** |
| `Enclosing` | layer groups only, flagged `MayEnclose` (volumes, the CMB object form): may contain the eye | `min(1, 0.75 * Far / (D + R))`, capped at 1500 m |

**Why the cap is a rendered radius of 10 m** (VERIFIED arithmetic). A similarity scales a group's
depth extent by the same s as its distance, whereas f compresses the *gaps between* groups by far
more (23 m per decade). A rigid group rendered 300 m deep would interleave with groups several
decades away. Capping the rendered radius at H = d0 does four things: it bounds every rigid
group's rendered depth extent to at most 20 m, less than one decade; it keeps rendered near
points at `f(D) - 10` m, which is at least 6.9 m whenever D is at least 2*d0, so outside the
room; it puts every rigid surface within 680 m; and it turns "the eye is close to a big body"
into Envelope mode instead of pulling the surface into your face. The last point matters:
a similarity would put the ground 1.6 m below you at `f(D)*1.6/D` = about 36 um, inside the near
plane.

**Tessellation rule for Envelope mode** (R3). `CS_CompressWS` is applied per vertex and the
rasterizer then interpolates linearly across the triangle, while f is logarithmic. What that does
and does not break, exactly:

- **It does not warp the silhouette.** Both endpoints keep their exact direction from E, so the
  rendered edge is a straight segment in a plane through E sweeping the same angular interval as
  the true edge. Its projection is the same screen segment a flat triangle would give. (The
  review's claim that "the interior direction is nothing like the true one" is wrong for screen
  position; the errors are radial and parametric.)
- **It does bend the surface in depth.** For an edge whose endpoint distances are `d1, d2` (both
  beyond d0) the rendered midpoint sits at `mean(f)` while the true surface point is at `f(mean)`;
  since f is concave the radial error is exactly `d0 * ln(AM/GM)`, with `AM/GM = (1+k)/(2 sqrt k)`
  and `k = d2/d1`.
- **It skews attributes.** UVs and normals are carried by the same interpolation, so the texture
  slides along the edge by at most `(AM - GM)/GM` of the edge length.

**The rule:** every drawn edge of a body mesh in Envelope mode satisfies **`d_max/d_min <= 1.1`**,
where d is the true distance of the endpoint from the centre eye. At k = 1.1 the radial error is
`10 * ln(2.1 / (2*sqrt(1.1)))` = **0.0114 m** and the parametric skew is **0.114 %**, which is
under 1 px for any edge shorter than 875 px. The radial error is under one depth quantum
(§2.4) from f = 98 m outwards (dz(98 m) = 0.0114 m, d = 6.4 km of true distance); nearer than that
it can exceed a quantum, which is the same regime in which §2.6 already refuses to order
surface-placed content by depth ("Landers on a surface ... never correct by depth").

`CosmoSphere` builds a **distance-graded** mesh in Envelope mode: subdivide any triangle whose
endpoint distance ratio exceeds 1.1, capped at **16 k vertices** for the one body in Envelope mode
(the §7.3 Bodies vertex row is raised to 180 k to hold it). The graded mesh is rebuilt on entering
Envelope mode and whenever `|log(D / D_built)| > 0.04` (about 10 %), at most once per 0.25 s. From
10 m to a true-scale Earth's far limb (1.27e7 m) that is 147 radial rings at 64 segments, about
9.4 k vertices (INFERRED arithmetic). The residual after the cap goes into `docs/accuracy.md`.
Gate: the G1c `envelope` assertion (§11.3).

**Push toward the eye** (inter-group order; runs after mode assignment, O(k^2), k at most 64
groups drawing opaque geometry this frame):

```
sort rigid and envelope groups with HasOpaque by true D, descending
for each group g in that order:
    for each already-placed group h (farther) that overlaps g on screen:
        # angular test: angle(u_g, u_h) < asin(rho_g) + asin(rho_h) + 1e-3
        zNearH = h.RenderedNear            # s_h (D_h - R_h), or f(D_h - R_h) for envelope
        if s_g (D_g + R_g) > zNearH - 2 dz(zNearH):
            s_g = (zNearH - 2 dz(zNearH)) / (D_g + R_g)
    if s_g (D_g - R_g) < NearPointFloorMetres:      # never pull a group into the room
        s_g = NearPointFloorMetres / (D_g - R_g); result.Conflicts++   # reported in the render line
    g.RenderedNear = s_g (D_g - R_g)
```

Pushing changes only depth, never screen position or angular size. The one visible side effect is
a disparity step: a per-vertex parent orbit then passes the pushed group's centre at a slightly
different depth.

**Points against rigid groups: the bounded artefact.** A point at true distance x in front of a
rigid group is rendered at f(x), while the group's near face is at `f(D)*(1 - rho)`. Because
f(x)/x decreases, a point with `x` in `[D*e^(-r/d0), D - R]` (r = rendered radius) that lies on
the group's disc on screen can be hidden wrongly. With r at most 1 m, typical, the band is the
10 % in front of the group's near surface; at the r = 10 m cap it is `[0.37*D, D - R]`. Real
case: asteroid-belt points in front of Jupiter seen from Earth give a band of 0.3 % of Jupiter's
distance, which is empty. **Accepted, and written into the reports as a known bound.**

**Handover across 30 orders of magnitude.** The cut is recomputed every frame from the rendered
radius, with hysteresis. At a switch between Rigid and dissolved, screen positions and angular
sizes are identical in both modes, because both are about E. Only depth steps, and the step is at
most about 1.5 px of disparity at f about 20 m and at most 0.1 px at f about 100 m. The
walkthrough below is VERIFIED arithmetic and supplies the expected values for the seven fixed
capture scales (§11.3):

| Scale (UnitsPerKm, m/km) | What is where |
|---|---|
| Earth hologram 0.55 m (4.31e-5) | Earth: identity at 0.7 m. The Earth system's R (the Moon's orbit, 16.6 m) exceeds D, so it **dissolves**, and the Moon is its own rigid leaf at f = 15.05 m, s = 0.908. Sun: rigid, f = 74.7 m, r = 0.35 m. Jupiter system: f = 89.1 m, r = 0.27 m. Alpha Cen: f = 200 m (billboard). Galactic-centre stars: per-point, f = 287 m. CMB: sky |
| Solar System in the room, Neptune orbit r = 1.5 m (3.34e-10) | Sun subtree D + R at most 10 m: all identity; Voyager 1 at 8.3 m, still identity. Alpha Cen at f = 82 m. M31 per-point at 215 m. CMB: sky |
| Nearby stars, 10 pc per metre (3.24e-15) | Alpha Cen at 0.13 m, identity; stars in the room are true 3D. Galactic centre per-point at 54 m. Dust box is `Enclosing` with s = 1 (eye inside). CMB: sky |
| Milky Way tabletop, 30 kpc per metre (1.08e-18) | Dust box 6.7 cm, identity. GC 0.27 m. M31 at f = 19.6 m (per-point). Virgo 50 m. CMB (4.6e5 m): sky |
| Local Group, 3 Mpc per metre (1.08e-20) | M31 0.26 m; Virgo 5.5 m, identity. CMB sphere 4.6 km around the eye: sky |
| Observable universe, CMB r = 0.6 m (1.40e-24) | Everything identity. The **CMB object form** is rigid. The handover to the sky form is a **hard switch with hysteresis** — object form while the eye is inside 0.95 R, sky form once it passes 1.05 R — and **no frame has both forms with `_Fade > 0`** (R10). To avoid a pop the outgoing form fades to `_Fade = 0` over 0.3 s (§5.7.5 rule 6) while the incoming form is already at full; the two are never both non-zero. A true cross-fade is impossible here: the sky form's disparity is exactly 0 by construction (§2.5 point 3) while the object form's surface is 0–6 cm from the eye at the switch, so superimposing them would fill the view with two incompatible depth cues. Gated by the G1c `cmb-fade` assertion (§11.3) |
| True scale, 1 m per metre (1000) | Earth: **Envelope**. Moon rigid at f = 184.7 m, r = 0.84 m. Sun f = 244 m, r = 1.14 m. Jupiter system f = 259 m. Alpha Cen f = 370 m. M31 per-point f = 502 m. CMB: sky |

**When the viewer is inside a group:**

- Body subtree: it dissolves, its anchor body is tested as a leaf, and its children are tested
  independently.
- Inside a leaf's sphere (inside the body, or inside its atmosphere at a big scale): Envelope.
  The body renders per-vertex, and the atmosphere integrates analytically in true eye-relative
  space in units of the planet radius (a port rule for the atmosphere shader, §7.2).
- Inside a volume or the CMB: Enclosing. The raymarch runs in object space from the per-eye ray
  origin. A similarity about E keeps the eye at the same object-space point, so the march is
  unaffected.
- Envelope spheres are exact only to `ulp(|T|)` about 6e-8*|T|. Standing on a true-scale planet
  shows about 0.5 m of quantisation until Phase-3 terrain with tile-local origins exists. This is
  a known limit, not a bug, and it goes into `docs/accuracy.md`.

**Size floors never create false occlusion.** A body whose true apparent diameter is below
**2.5 px** (`CelestialBodyView.cs:300`) is **not** drawn as an inflated sphere. It is drawn by the
body-point path (additive, no depth write), `DrawnAsPoint` is true and `IsGeometryDrawn` is
false. An inflated sphere would break inside-group order, since a floored moon would intersect
its planet.

### 2.7 `Core/Scale/ScaledSpace.cs` and its tests

A new file next to `ScaleSpace.cs`. The dead band stack in `ScaleSpace.cs:28-78` and `:150-179`
(`ScaleBand`, `DefaultBands`, `SelectBand`, and the "multi-pass compositor" and "reversed float
depth" comments at `:29-35` and `:152-158`, all VERIFIED present) is **deleted in the same
change**, so there is exactly one account of distance in the codebase.

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
        public bool OwnsSubtree;      // Rigid with the whole subtree collapsed into it; false = dissolved
        public int RigidOwner;        // node whose similarity applies (self or rigid ancestor); -1 if Envelope
        public double Scale;          // s; 1 for Envelope
        public double RenderedNearMetres, RenderedFarMetres;
        public bool Pushed;
    }

    // Visibility windows are NOT here: Core/Layers/LayerState.Weight(id, log10U, ctx) owns them.

    public static class ScaledSpace
    {
        public const double IdentityRadiusMetres = 10.0;
        public const double RigidEnterMetres = 9.0, RigidLeaveMetres = 11.0;   // H with hysteresis
        public const double NearPlaneMetres = 0.05, FarPlaneMetres = 2000.0, SkyRadiusMetres = 1500.0;
        public const int DepthBits = 24;
        public const double PushMarginQuanta = 2.0, NearPointFloorMetres = 5.0;

        public static double Compress(double metres);           // f
        public static double Decompress(double renderedMetres); // f^-1
        public static double GroupScale(double metres);         // f(d)/d, 1 for d <= d0 or d <= 0
        public static double DepthQuantum(double viewMetres, int bits = DepthBits,
                                          double nearMetres = NearPlaneMetres); // z^2/(n 2^bits)
        /// CPU replica of CS_CompressWS for picking and gates: eye-relative metres in and out.
        public static Double3 CompressRelative(Double3 eyeRelativeMetres);

        /// The cut, the modes and the push. `previous` may be null (first frame). Returns the
        /// number of unresolved conflicts (a push clamped by NearPointFloorMetres); ScaleRenderer
        /// prints it in the render: line.
        public static int Solve(IReadOnlyList<GroupNode> nodes, Double3 eyeKm, double unitsPerKm,
                                GroupPlacement[] previous, GroupPlacement[] result);
    }
}
```

**`Solve`, exactly** (all lengths are rendered metres: `D = |AnchorKm - eyeKm| * upk`,
`R = RadiusKm * upk`, `R_leaf = LeafRadiusKm * upk`):

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
push pass over owners with HasOpaque; Rigid ones move, Envelope ones are fixed obstacles;
a group whose un-pushed near point is already < NearPointFloorMetres is room content: never pushed.
```

**Tests: `T/ScaledSpaceTests.cs`** (harness style of `CoreSystemsTests.cs`; every tolerance
carries its reason in a comment).

| Test | Assertion | Tolerance and why |
|---|---|---|
| identity | `Compress(d) == d` for d in {0.05, 1, 9.999, 10} | exact (same branch) |
| known answers | the §2.3 table, 13 rows | relative 1e-12: `Math.Log` is within 1 ulp (about 1e-16); the margin lets an algebraically equal form such as `log(d) - log(d0)` pass |
| C-1 at d0 | one-sided difference quotients with h = 1e-6*d0 agree with 1 within 1e-6 | truncation error of a one-sided difference is O(h/d0) = 1e-6 |
| monotonic and bounded | f strictly increasing on 2000 log-spaced points over 1e-3 to 1e30, and f(1e30) + RigidLeave < 0.5*Far | exact comparisons |
| inverse | `Decompress(Compress(d)) == d` on the same grid | relative 1e-12 |
| angular-size invariance | for 1000 seeded (D, R) with R < D: `asin(s*R/(s*D)) == asin(R/D)` | 1e-12 rad: exact algebra, double rounding about 1e-16 |
| depth quantum | `DepthQuantum(600.462318658) = 0.429823...` (= 600.46^2 / 838860.8) | relative 1e-12 |
| T1 near leaf | D = 1, R_leaf = 0.3 gives Rigid, s = 1, interval [0.7, 1.3] | exact |
| T2 far leaf | D = 1e9, R = 1e6 gives Rigid, s = 1.9420680744e-7, rendered radius 0.194207 | relative 1e-12 |
| T3 envelope | D = 6.771e6, R_leaf = 6.471e6 (true-scale Earth plus atmosphere from LEO) gives rendered radius 137.86 m > 9, so Envelope, s = 1 | exact mode |
| T4 system cut | planet D = 1e9, R = 4e7, R_leaf = 6.4e6 gives Rigid and owning (7.768 m), child Collapsed at the same s. At D = 1e8: 68.47 m, so dissolved; leaf 10.956 m > 9, so Envelope | exact mode, s to 1e-12 |
| **T5 the transit trap** | planet (anchor) R = 100 at D = 1000, moon R = 10 at 850 on the same ray, one group (rendered radius 8.968, at most 9): the moon's back at s*860 = 48.2045 is **less than** the planet's front at s*900 = 50.4465. The comment records the per-body counterexample: f(850) = 54.4265 > 50.4465 | 1e-9 |
| T6 push | leaf A (D = 1000, R = 100) gives [50.4465, 61.6569]; leaf B (D = 1500, R = 100) gives [56.0993, 64.1134]; same ray. After the push s_A = 0.050992509 and A = [45.8933, 56.0918], so A's far point is below B's near point minus 2dz. Angular radii unchanged | 1e-9 |
| T7 floor | A (D = 11, R = 5), B (D = 13, R = 1), same ray: the push would put A's near point at 4.3696 < 5, so it clamps to s_A = 5/6 and the returned conflicts = 1 | exact count, s to 1e-12 |
| T8 enclosing | volume D = 0.7, R = 173, `MayEnclose` gives Enclosing, s = 1 | exact |
| T9 hysteresis | rendered radius 10: a previous owning Rigid stays Rigid; previous null gives dissolved | exact |

Every one of these passes or fails on arithmetic alone. That is CLAUDE.md's "print the numbers,
do not eyeball" applied to placement.

### 2.8 The per-frame order, `FrameContext`, `Placement`

**Order** (`[DefaultExecutionOrder]`):

| Phase | Order | Who | Does |
|---|---|---|---|
| Update | -100 | `XrInput` | reads devices once |
| Update | 0 | movers (`TabletopManipulator`, `XrLocomotion`, `TravelController`, `ScaleControl`) | write `FloatingOrigin.OriginKm` and `UnitsPerKm` (through `SetUnitsPerKm`, clamped by `ScaleLimits`) and the anchor yaw |
| LateUpdate | -100 | the presentation-plane driver (§3.6) | writes the Universe host's `localRotation`. `FloatingOrigin` loses its `LateUpdate` with the dead rebase code |
| LateUpdate | -50 | `UniverseSimulation` | clock to **double state only**: `view.UpdateState(now)` sets `AbsolutePositionKm` and `IsKnown`; **no transform writes** |
| LateUpdate | 50 | `ScaleRenderer` | builds `FrameContext`; LOD per view from true apparent size; **`layer.UpdateWeight(ctx)` for every registered layer**; builds `GroupNode`s (views plus `CosmoLayer.DeclareGroups`, **called only on layers with `Weight > 0`**); `ScaledSpace.Solve`; `view.Place(ctx, placement)` and `layer.AcceptGroup(...)`; light uniforms per body; shader globals; **`layer.Apply(ctx)`**; `BodyMarkers.Rebuild` (R28) |
| LateUpdate | 60 | decorations: `AtmosphereShell`, `RingSystem`, per-orbit ribbons, `InspectorCard`, the selection rim | read the **placed** transforms; this also fixes today's one-frame `IsGeometryDrawn` lag |
| LateUpdate | 100 | `LabelLayer` | labels |

Placement runs in `LateUpdate`, not `onBeforeRender`. Worldfall ships that way. The residual
head-pose delta between `LateUpdate` and render is about 2 mm, which is at most 0.3 px of
direction error at 10 m. Moving placement to `Application.onBeforeRender` is an optimisation to
measure, not a requirement (ordering against the pose driver is INFERRED).

**`Runtime/Universe/FrameContext.cs`** — the struct every placement consumer receives:

```csharp
public readonly struct FrameContext
{
    public readonly Double3 EyeKm;            // centre eye, absolute ICRF km (double)
    public readonly Vector3 EyeWorld;         // centre eye, Unity world (camera transform position)
    public readonly Vector3 EyeUniverse;      // centre eye in the Universe host's local space
    public readonly double UnitsPerKm;        // render metres per km ("U")
    public double Log10U => System.Math.Log10(UnitsPerKm);   // the argument of LayerState.Weight
    public readonly Matrix4x4 IcrfToWorld;    // (anchor * Universe host) rotation * P, upper 3x3, det = -1
    public readonly float PixelsPerRadian;    // 0.5 * eye height * P11, centre-eye estimate
    public readonly CosmicTime Now;
    public readonly LayerContext LayerContext; // Core/Layers/LayerContext.cs, built here (§4.2)
}
```

**`Runtime/Rendering/Placement.cs`** — a static helper, the only code that turns a scale into
transforms:

```csharp
public static class Placement
{
    /// Rigid similarity about the centre eye, in Universe-local space.
    public static void PlaceRigid(Transform t, Vector3 trueUniverse, double s, in FrameContext ctx)
        => t.localPosition = ctx.EyeUniverse + (float)s * (trueUniverse - ctx.EyeUniverse);
        //   trueUniverse must come from FloatingOrigin.ToRender (double subtraction first).
        //   t MUST be a direct child of the Universe host: EyeUniverse is in host-local space.
    /// Eye position expressed in a layer chunk's own units, computed in double then cast.
    public static Vector3 EyeInChunk(in FrameContext ctx, Double3 layerOriginKm, Matrix3d icrfToLayer,
                                     double kmPerUnit, Double3 chunkOriginUnits);
    /// Globals for CosmoCore.hlsl; called once per frame by ScaleRenderer only.
    public static void PublishGlobals(in FrameContext ctx);
}
```

**`PlaceRigid` places host children only** (R34). `ctx.EyeUniverse` is the centre eye in the
Universe host's **local** space, so `t.localPosition` is only the right space when `t` is a direct
child of that host. A layer whose renderers are nested (a dust volume with a child LOD sprite set,
an exhibit plinth with children) places the host-child transform only and keeps every descendant
at local identity. `CosmoLayer.SelfCheck()` must append
`"<LayerId>: placed transform is not a child of the Universe host"` when it is not; the G1c
`layers` assertion then fails.

**Shader globals**, set only by `Placement.PublishGlobals` and read by `CosmoCore.hlsl`:
`_CS_CentreEyeWS` (float4), `_CS_Compress` = (d0, 1/d0, SkyRadius, 0), `_CS_IcrfToWorld`
(float4x4, rotation in the upper 3x3, improper). They are globals, so they do not break SRP
Batcher compatibility.

**`CelestialBodyView` split** (replacing the placement inside `UpdateState`, `:140-294`):

- `UpdateState(time)`: state only — sets `AbsolutePositionKm`, velocity and `IsKnown`, and
  collapses on unknown.
- `Place(in FrameContext ctx, in GroupPlacement g, Double3? hostStarKm)`:
  - Rigid or Collapsed: `Placement.PlaceRigid(transform, origin.ToRender(abs), g.Scale, ctx)`;
    the visual diameter is `g.Scale * S * 2 * R_display`.
  - Envelope: `localPosition = origin.ToRender(abs)`, s = 1, and the material sets
    `_PerVertexCompress = 1`.
  - `localRotation = origin.MapRotation(BodyFixedToIcrf)`.
  - Floor rule: true apparent diameter below 2.5 px sets `DrawnAsPoint`, switches the sphere
    renderer off and makes `IsGeometryDrawn` false. `IsMarkerDrawn` and `IsPointable` keep their
    present meanings (VERIFIED `CelestialBodyView.cs:703,713`).
  - Light: `_LightDirWS` from the host star (§8.2 item 12).

---

## 3. Frames, units, axes

### 3.1 The single ICRF-to-Unity mapping (inside `FloatingOrigin` only)

```
universeLocal = P * (absKm - OriginKm) * UnitsPerKm      (metres, Universe host space; FIXED)
absKm         = P * universeLocal / UnitsPerKm + OriginKm   (P is its own inverse)
P = [[1,0,0],[0,0,1],[0,1,0]]    swaps y and z: right-handed ICRF -> left-handed Unity
world = UniverseAnchor (yaw + translation) * UniverseHost (presentation plane, §3.6) * universeLocal
```

- **Up.** With the host at identity, Unity +y is ICRF +z (the celestial pole), +x is the vernal
  equinox, +z is ICRF +y. The presentation-plane driver rotates the host so the active plane's
  north pole points to world +y. With the Ecliptic plane that makes the bootstrap comment "the
  ecliptic stays level" (`CosmoSynthBootstrap.cs:348`) true for the first time.
- **Handedness.** `det P = -1` is exactly the right-to-left-handed change of basis, so the solar
  system is no longer mirrored. Today positions go through the identity
  (`FloatingOrigin.cs:78-86`, VERIFIED), which *is* the mirror.
- **Why the plane is not in the mapping.** Putting the plane rotation Q inside the mapping
  (`P*Q*(abs - O)*S`) and putting `P*Q*P` on the host give identical world positions, because P
  is symmetric and its own inverse. The host form keeps the mapping fixed, as binding 4 reads it,
  and every universe-local computation (the eye via `InverseTransformPoint`, the hands'
  `ToAbsolute`) then includes the plane with no extra code.
- **Body rotations**: `R_unity = P * M_bodyToIcrf * P`. Its determinant is (-1)(+1)(-1) = +1, a
  proper rotation, so it converts to a Quaternion as `LookRotation(forward = column 2,
  up = column 1)`. Unity local +y is the body pole, matching today's `ToUnityRotation` convention
  (`CelestialBodyView.cs:898-909`) and the sphere's UV pole.
- **Meshes authored in ICRF (or layer-frame) components** (ribbons, volumes) get the improper P
  as a proper rotation times a mirrored scale: `P = (P*D)*D` with `D = diag(1, 1, -1)`, so
  `localRotation = quat(P*D) = Quaternion.Euler(90, 0, 0)` and `localScale = s*k*(1, 1, -1)`.
  Unity flips the cull winding for negative-determinant transforms by itself (INFERRED, standard
  behaviour). Population and sky shaders instead take the **global** `_CS_IcrfToWorld` directly.
  There is **no per-material `_LayerToWorld`** (R4): a layer's frame rotation is applied at bake
  time, so what the shader receives is already ICRF components.

**Core: extend the existing `ScaleSpace.RenderTransform`** (`ScaleSpace.cs:197-225`, VERIFIED)
rather than adding a type. Section D P0.3 calls this "`UnityAxes`"; it is this struct:

```csharp
public readonly struct RenderTransform
{
    public Double3 OriginKm { get; }  public double UnitsPerKm { get; }
    public static readonly Matrix3d AxisSwap = new Matrix3d(1,0,0, 0,0,1, 0,1,0);   // P
    public Double3 ToRender(Double3 absoluteKm);        // P*(abs - O)*S
    public Double3 ToAbsolute(Double3 render);          // P*render/S + O
    public Double3 MapDirection(Double3 icrf);          // P*dir
    public Double3 UnmapDirection(Double3 render);      // P*dir
    public Matrix3d MapRotation(Matrix3d bodyToIcrf);   // P*M*P  (proper)
    public double ScaleLength(double km);               // unchanged
}
```

`Rebased(...)` is deleted with the rebase machinery.

**`Runtime/Universe/FloatingOrigin.cs` becomes a thin Unity wrapper**, and the **only** place
Unity vectors meet ICRF:

- `ToRender(Double3) -> Vector3`, `ToAbsolute(Vector3 universeLocal) -> Double3`;
- `MapDirection`, `UnmapDirection`, `MapRotation(Matrix3d) -> Quaternion`;
- `IcrfToUniverse -> Matrix4x4`, `IcrfMeshRotation -> Quaternion` (for the D trick above);
- `SetOrigin`, `SetUnitsPerKm` (clamped by `ScaleLimits.Clamp`).

The rebase machinery (`:9-16` `IFloatingOriginListener`, `:37-47`, `:59-75` `Focus`, `:109-160`
`LateUpdate`/`Rebase`, plus `OriginShifted`, `Register`, `Unregister`, `RebaseCount`) is
**deleted**: it is dead, because `Focus` is always null (`CosmoSynthBootstrap.cs:118`).
`CelestialBodyView.ToUnityRotation`, `ToUnityDirection` and `ToIcrfDirection` (`:898-917`) are
deleted and their three callers switch to `FloatingOrigin` (§8.2 item 13).

**Tests (`T/FrameTests.cs`, new).**

- `ToAbsolute(ToRender(x)) == x`, relative 1e-15 of |x| (two double rotations);
- `MapRotation(M)` has determinant +1 and `IsRotation(1e-12)`;
- consistency: `MapRotation(M) * (0,1,0) == MapDirection(M * (0,0,1))` — the body pole lands
  where positions say the pole points. This is the test that makes the old split impossible;
- the Unity-axis known answers of §3.2.

### 3.2 `StandardFrames`: the galactic fix, the matrices, the test vectors

| Rotation (ICRF to frame; rows are the frame's axes in ICRF) | Matrix (VERIFIED) |
|---|---|
| Ecliptic, IAU 1976, eps = 84381.448" (existing `EquatorialToEcliptic()` = RotationX(-eps)) | `[1, 0, 0; 0, 0.9174820621, 0.3977771559; 0, -0.3977771559, 0.9174820621]` |
| **Galactic, fixed** | `[-0.0548755604, -0.8734370902, -0.4838350155; 0.4941094279, -0.4448296300, 0.7469822445; -0.8676661490, -0.1980763734, 0.4559837762]` (the Hipparcos matrix; checked against all 44,599 2MRS pairs, median 1.08") |
| Galactic to Supergalactic (new `GalacticToSupergalactic`; pole (l, b) = (47.37, 6.32), SGL = 0 at (137.37, 0)) | `[-0.7357425748, 0.6772612964, 0; -0.0745537784, -0.0809914713, 0.9939225904; 0.6731453021, 0.7312711658, 0.1100812622]` (checked against 55,877 CF4 pairs, median 0.17") |
| Supergalactic (new `EquatorialToSupergalactic` = G2S * I2G) | `[0.3750154778, 0.3413589618, 0.8618801834; -0.8983204556, -0.0957271407, 0.4287851135; 0.2288749668, -0.9350456533, 0.2707505787]` |

**The fix to `StandardFrames.cs:105-108`** (C5):

```csharp
_equatorialToGalactic =
    Matrix3d.RotationZ(+GalacticAscendingNodeLongitudeDeg * MathD.Deg2Rad) *
    Matrix3d.RotationX(-(90.0 - GalacticPoleDecDeg) * MathD.Deg2Rad) *
    Matrix3d.RotationZ(-(90.0 + GalacticPoleRaDeg) * MathD.Deg2Rad);
```

It matches Hipparcos to 4.9e-11; the current signs are off by up to 1.75 per element.
`GalacticPoleRaDeg = 192.85948`, `GalacticPoleDecDeg = 27.12825` and
`GalacticAscendingNodeLongitudeDeg = 32.93192` already exist (VERIFIED `:82,85,88`).
`GalacticToSupergalactic` is built from the two defining directions (z = pole, x = SGL 0,
y = z cross x) and agrees with the matrix above to 3.5e-11. **`Planes.Up(plane, ctx)`** (§5.2, the
one declaration) reads these three rotations; no second plane enum exists anywhere. (R31: the
earlier drafts wrote `PlaneUp(X)`, `PlaneUp(plane, context)` and `PresentationPlane.PlaneUp`, and
`PresentationPlane` is an `enum` and cannot carry a method. There is one name and it is
`Planes.Up`.)

**Test vectors (`T/FrameTests.cs`):**

| Input | Expected | Tolerance and why |
|---|---|---|
| `EquatorialToGalactic` against the Hipparcos matrix | every element | 1e-9: 20 times the measured 4.9e-11, which comes from the pole angles being published to 5 decimals |
| NGP (192.85948, 27.12825) | galactic (0, 0, 1) | 1e-12, exact by construction |
| galactic (1, 0, 0), inverted | RA 266.404995, Dec -28.936174 | 1e-6 deg, implied by the matrix |
| Sirius (101.287155, -16.716116) | l = 227.2303, b = -8.8903 | 1e-4 deg (4-decimal rounding of the expectation) |
| M31 (10.684708, 41.268750) | l = 121.1743, b = -21.5733; SGL = 336.1930, SGB = 12.5521 | 1e-4 deg |
| M87 (187.70593, 12.39112) | SGL = 102.8806, SGB = -2.3479 | 1e-4 deg |
| galactic (264.021, 48.253) (CMB dipole) | RA 167.9419, Dec -6.9443 | 1e-4 deg |
| SG pole (l, b) = (47.37, 6.32) | SG (0, 0, 1); ICRS RA 283.7541, Dec 15.7089 | 1e-12 / 1e-4 deg |
| ecliptic pole | ICRF (0, -0.3977771559, 0.9174820621), RA 270, Dec 66.5607 | 1e-10 |
| **Universe-local axes** (host at identity), `MapDirection` | ICRF +x to (1,0,0); ICRF +z to (0,1,0); ICRF +y to (0,0,1) | exact |
| Levelled to Ecliptic (host rotation = the minimal rotation taking `P*pole` to +y) | world direction of the ecliptic pole = (0,1,0); the vernal equinox stays (1,0,0) when levelling from identity | 1e-6 (float quaternion) |
| Levelled to Galactic, and to Supergalactic | the NGP, respectively the SG pole, to (0,1,0) | 1e-6 |

Angles between the plane poles, which set the size of a levelling animation: ecliptic to galactic
60.19 deg, galactic to supergalactic 83.68, ecliptic to supergalactic 51.66, equatorial to
ecliptic 23.44.

### 3.3 Units and the double-precision budget

Absolute positions stay **double km, ICRF, SSB origin**. The spacing of a double at |x| km is
`ulp = 2^(floor(log2 x) - 52)` km:

| Where | \|x\| (km) | ulp | What it means |
|---|---|---|---|
| Neptune | 4.5e9 | 1e-6 km (1 mm) | every scale exact |
| Voyager 1 | 2.5e10 | 4 mm | exact |
| Alpha Cen | 4.1e13 | 8 m | exact at every hologram scale |
| 1 kpc | 3.1e16 | 4 km | a 0.5 m planet hologram there jitters by 1.6e-4 m, still under 0.5 px |
| Galactic centre | 2.5e17 | 32 km | galaxy-scale content only |
| M31 | 2.4e19 | 4096 km | galaxy-scale only: no planetary content may be placed there |
| CMB | 4.3e23 | 6.7e7 km (0.45 AU) | direction-only, or the object form at Mpc scales |

**Rule for 2.0:** a catalogue body with a surface (Planet, Moon, DwarfPlanet, Asteroid, Comet or
Spacecraft) beyond **3e16 km** of the SSB is a validation warning in `CelestialCatalog.Validate`.
No shipped or planned catalogue does this; fictional systems are tabletop holograms with no ICRF
placement. If it is ever needed, add a frame-relative origin using the existing
`ReferenceFrame.StateRelativeTo`. Not before.

Render units are metres in the Universe host space; the anchor is unscaled
(`CosmoSynthBootstrap.cs:96-111`).

### 3.4 How population layers store positions

- **At bake time** each point is stored in the layer's natural unit, in **ICRF components**:
  pc for stars, kpc for the Local Volume, Mpc for large-scale structure, Mpc/h for the CF4 group
  catalogue. **The frame rotation is applied by the importer, never at runtime** (R4): §6.3
  already fixes the frames and H0, and the rotations of §3.2 are constants. The meta block
  (§6.1.2) still records the *source* frame for provenance, and the unit and the layer origin.
  **Grids are the one exception** and keep their native sampling frame (`galactic-helio`,
  `supergalactic`), because resampling would blur them (§6.3); a grid layer rotates with the
  global `_CS_IcrfToWorld` composed with the fixed frame matrix **on the CPU, into its transform**,
  not with a per-material matrix.
- **Core `PointChunks.Build(ReadOnlySpan<Double3> positionsUnits, int maxPerChunk = 4096,
  int maxChunks = 64)`** at `Core/Scale/PointChunks.cs` splits the points octree-wise. For each
  chunk it produces a double `OriginUnits` (the box centre), a `RadiusUnits` and float offsets.
  Test: `max |(double)offset + origin - original| <= 6e-8 * RadiusUnits` for every point, and
  every point in exactly one chunk.
- **Runtime:** one `MeshRenderer` per chunk with a per-chunk material instance (SRP
  Batcher-friendly) holding `_EyeInChunk`. Each frame
  `eyeInChunk = (float3)(icrfToLayer*(ctx.EyeKm - layerOriginKm)/kmPerUnit - chunk.OriginUnits)`,
  with the subtraction done in double before the cast (`Placement.EyeInChunk`).
- **Shader:** `rel = offset - _EyeInChunk`, then `CS_Length`, per-point f, and rotation by the
  **global** `_CS_IcrfToWorld` through `CS_IcrfDirToWorld` (Appendix A.4). Rotation is
  norm-preserving, so the 6e-8 chunk-offset guarantee below is unchanged. **No matrix is ever
  declared in `UnityPerMaterial`** — ShaderLab has no matrix property type, so it could never
  appear in a `Properties` block, which is the documented condition for SRP Batcher
  compatibility; if a layer ever genuinely needs a per-material rotation, it declares three
  `Vector` properties `_L2W0/_L2W1/_L2W2` (R4).
- **Precision guarantee.** The angular error is at most `1.2e-7 * chunkRadius / pointDistance`
  (two float roundings), which is under 0.5 px (3.5e-4 rad) when the point is farther than
  3.4e-4 of the chunk radius from the eye — beyond about 1,800 AU for a 25 pc chunk. Nearer
  points are faded out with the near-fade `_Look.zw`, and the promoted star system
  (`NearbyStarSystems`, double precision) represents them. This is how nearby-star parallax stays
  exact.
- **Culling.** Chunk renderers get huge bounds, because vertices move in the shader. `Apply`
  enables a chunk only if its angular disc `asin(R/D)` intersects the view cone (O(64) per
  frame).
- **De-duplication.** Points a `CelestialBodyView` also draws (the Sun, an expanded exoplanet
  host, the active `gen-hyg<row>` star) carry `data.w = 1` (hidden). The layer rewrites that
  chunk's `TEXCOORD0` stream on `UniverseSimulation.BodiesChanged`, which is rare.
  **Invariant (R14): all four vertices of a point quad carry the same `data.w`.** The shader's
  "cull" writes an off-screen clip position, which *clips* a primitive rather than discarding it;
  a quad whose four vertices disagreed would rasterize as a clipped wedge, not as nothing. The
  same holds for the Ribbon's behind-the-eye test: both endpoints of a segment evaluate it
  together. `PointChunksTests` asserts it: every group of four consecutive vertices shares one
  `data.w`.

### 3.5 Floating-origin behaviour

- There is **no rebase**. The camera stays in the room; movers change `OriginKm`, `UnitsPerKm`
  and the plane. The render mapping subtracts in double before any float cast, so float error
  cannot accumulate.
- Rigid placement stays exact because `Placement.PlaceRigid` receives `origin.ToRender(abs)`,
  computed in double.
- The viewer's absolute position is
  `ctx.EyeKm = origin.ToAbsolute(universe.InverseTransformPoint(camera.position))`, computed once
  per frame in `ScaleRenderer`. Nothing else recomputes it.
- `UniverseSimulation.NearestBody` also asks every `CosmoLayer.NearestSurfaceKm`, so flight speed
  at Mpc scales is set by the nearest galaxy, not by the Sun.
- **Every writer of `UnitsPerKm` uses `SetUnitsPerKm`**, which clamps to `ScaleLimits`
  [2e-25, 1e3]. `TabletopManipulator._minimumUnitsPerKm` (1e-20) and `_maximumUnitsPerKm` (1e6),
  both VERIFIED at `:45,49`, are deleted in favour of `ScaleLimits` — the atlas's own bug B1 was
  two clamps disagreeing. Every number in §2 was checked up to 1e6 m/km, so there is margin.
- `ScaleSpace.NavigationSpeed`'s ceiling rises to **1e24 km/s** (owned by `nav`): at the `cmb`
  rung, crossing the observable universe's 8.8e23 km in about 4 s needs 2.2e23 km/s, so 1e24
  gives margin. Today's 1e14 is five decades short.

### 3.6 The presentation plane: mechanism

*What* is level and *when* it changes is §5.2. This section fixes *how*.

- The plane is the Universe host's `localRotation`. The anchor above it keeps the yaw and the
  room position. **Nothing else writes the host rotation.**
- **Levelling to plane X** computes the target up in universe-local coordinates,
  `u = P * Planes.Up(X, ctx)` (§5.2). The new host rotation is `R1 = FromToRotation(R0*u, +y) * R0` — the
  minimal rotation. `TravelController` then adds a yaw on the anchor that keeps the travel target
  in front of the user.
- **Animation.** Over T seconds (the travel duration, or 1.2 s for a rung tap),
  `R(t) = Slerp(R0, R1, e(t/T))` with smootherstep `e(u) = u^3(6u^2 - 15u + 10)`, giving zero
  angular velocity at both ends. In Space mode a tilt larger than 10 deg **blinks** instead
  (0.15 s fade out, snap, 0.15 s fade in), because a rolling starfield with no room reference
  induces vection. The largest tilts are the pole-to-pole angles of §3.2.
- **Pivot = the Universe host's origin**, which is where `OriginKm` maps. After a GO, `OriginKm`
  is the target, so the thing being looked at stays put while the sky turns around it.
  `FloatingOrigin` needs no change, because the rotation is a transform, not a mapping.
- **Time-dependent planes** (Horizon, LocalVertical, FocusEquator) are re-evaluated every frame
  by the same driver: one `FromToRotation` per frame.
- **A grab during a levelling animation stops the animation where it is.** The host keeps a valid
  rotation that is level to no named plane, and the panel's "Level to ..." chip then offers to
  finish. Travel is cancelled by a grab anyway.
- **The manipulator never touches the host rotation and never requests levelling.** It touches
  `OriginKm` (drag), `UnitsPerKm` (two-hand scale about a pivot) and the anchor's yaw (twist and
  snap turn) only.
- **The local-space fix (architecture-map B4).** Hand poses arrive in world space from
  `XrHandInput.cs:282-288`. Convert them **once**, at the top of `TabletopManipulator.UpdateHands`
  (`:100`): `pose.Position = _simulation.transform.InverseTransformPoint(pose.Position)`, and the
  rotation likewise. `_simulation.transform` *is* the Universe host, so this crosses both the
  anchor and the plane rotation, and every `origin.ToAbsolute` call at `:131`, `:138`, `:174` and
  `:191` then receives universe-local metres. Grabs stay correct in any plane.
  `XrHandInput` stays in world space; the manipulator owns the conversion.
- **The sticky-target fix (B5).** `_targetUnitsPerKm` (`:79`, VERIFIED) is cleared to 0 when
  `|log10(current) - log10(target)| < 1e-3` (reached), and also 0.5 s after the last hand
  releases. `ApplySmoothedScale` (`:206-229`) then leaves the scale alone. Rungs and travel set
  the scale only through `FloatingOrigin.SetUnitsPerKm`.
- **Tests.** Core: the `PresentationPlane` pole vectors of §3.2. Play mode: assertion `plane`
  (§11.3). Neither needs a headset.
---

## 4. The layer framework

The minimum that population layers share: one abstract MonoBehaviour and one static registry.
There is no interface hierarchy, no factory and no per-layer config asset. **Bodies are not a
layer**: `UniverseSimulation` and `CelestialBodyView` stay what they are and only gain
`Place(...)`. The layer ids `bodies`, `orbits`, `spacecraft`, `trails`, `sites`, `atmospheres`,
`rings`, `clouds`, `winds`, `comets` and `sunActivity` gate those existing components through the
same `Weight`.

### 4.1 `Runtime/Universe/CosmoLayer.cs` (the verbatim contract)

```csharp
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// How a layer's fragments reach the framebuffer. Read by the play-mode `alpha` assertion
    /// and by the passthrough-coverage probe; it is the layer's promise about its own blend.
    public enum LayerBlend : byte { Opaque, Additive, Premultiplied, Sky }

    /// A population or effect that is not a catalogue body. One subclass per file, added by the
    /// bootstrap's BuildLayers() slot, backed by an engine-free Core parser with tests.
    public abstract class CosmoLayer : MonoBehaviour
    {
        public abstract string LayerId { get; }                  // exactly a LayerTable id
        public abstract IReadOnlyList<string> DataKinds { get; } // manifest kinds it consumes (= LayerDef.DataKinds)
        public abstract LayerBudget Budget { get; }              // ceiling at Weight = 1; asserted by the play-mode gate
        public abstract LayerBlend Blend { get; }                // one kind per layer

        public float Weight { get; private set; }                // LayerState.Weight this frame; 0 = not drawn, not pickable
        protected UniverseSimulation Simulation { get; private set; }
        protected LayerState State { get; private set; }

        public void Bind(UniverseSimulation simulation, LayerState state)
        {
            Simulation = simulation;
            State = state;
        }

        /// `DataLoader` raises `LayerReady(string kind, object parsed)` (§6.5.3). The layer kit is
        /// the ONLY subscriber: it calls OnData on every registered layer whose DataKinds contains
        /// the kind, and calls it again on re-registration if the kind already arrived. Layers
        /// implement OnData and never subscribe to LayerReady themselves (R33).
        /// Build meshes here; heavy parsing already happened in Core.
        public abstract void OnData(string kind, object parsed);

        protected virtual void OnEnable() => LayerRegistry.Register(this);
        protected virtual void OnDisable() => LayerRegistry.Unregister(this);

        /// Called by ScaleRenderer (LateUpdate order 50) BEFORE DeclareGroups, so a layer that is
        /// off, out of window or missing its data declares no group at all (R28).
        public void UpdateWeight(in FrameContext ctx)
        {
            Weight = (float)State.Weight(LayerId, ctx.Log10U, ctx.LayerContext);
            SetRenderersEnabled(Weight > 0f);
        }

        /// Called after AcceptGroup, once per frame, only when Weight > 0.
        public void Apply(in FrameContext ctx) => ApplyCore(ctx);

        protected abstract void SetRenderersEnabled(bool on);
        protected abstract void ApplyCore(in FrameContext ctx);   // uniforms, chunk culling, rigid parts;
                                                                  // writes Weight into every owned material's _Fade

        /// Rigid or enclosing parts (a volume, the CMB object form, an exhibit). Append nodes;
        /// return how many. ScaleRenderer hands each back through AcceptGroup(localIndex, ...).
        public virtual int DeclareGroups(List<GroupNode> into) => 0;
        public virtual void AcceptGroup(int localIndex, in GroupPlacement placement, in FrameContext ctx) { }

        public virtual void CollectDestinations(List<Destination> into) { }
        public virtual void CollectLabels(List<LabelCandidate> into) { }

        /// Pick in RENDERED space (the hand ray meets what is drawn); report TRUE positions.
        public virtual bool TryPick(Ray worldRay, in FrameContext ctx, out PickHit hit) { hit = default; return false; }

        /// Feeds UniverseSimulation.NearestBody so flight speed follows the nearest thing of any kind.
        public virtual double NearestSurfaceKm(Double3 absoluteKm) => double.PositiveInfinity;

        /// Every internal invariant this layer can check without a headset. Append one short
        /// sentence per problem. The play-mode `layers` assertion fails if any layer returns one.
        public virtual void SelfCheck(List<string> problems) { }
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

### 4.2 The Core layer types

All engine-free, all in `Core/Layers/` unless stated.

**C# level (R18, VERIFIED on triton at `fc07a52`).** The five shared types below are declared as
C# 9 `record`s. Positional records generate `init`-only setters, which need
`System.Runtime.CompilerServices.IsExternalInit`; it is **absent** from this project's compilation
context (`strings netstandard.dll | grep -c IsExternalInit` = 0; the only hits in the tree are
`internal` to two packages), and both test projects pin `<LangVersion>9.0</LangVersion>`. So
**P0.2(e) creates `unity/Assets/CosmoSynth/Core/Compat/IsExternalInit.cs`**, integrator-owned,
containing exactly:

```csharp
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
```

G0d asserts that file exists, so nobody deletes it as dead code. **`record struct` is C# 10 and
cannot be polyfilled**, so `ViewState` (§5.7.1) is a `public readonly struct` with an explicit
constructor, not a `readonly record struct`.

```csharp
// Core/Layers/LayerTable.cs  — the only list of layers and rungs.
public enum LayerGroup { SolarSystem, Stars, DustAndNebulae, Galaxies, Beyond, Guides }
public enum SoloGroup  { None, Solar, Stars, Dust, Galaxies, Beyond }   // None = unaffected by SOLO
public enum LabelCategory { Bodies, Spacecraft, Sites, SolarLandmarks, StarNames, Constellations,
                            Clouds, MilkyWay, LocalGroup, Structures, Guides, StageFictional, StageExhibit }

public sealed record LayerDef(
    string Id, string Caption, LayerGroup Group, SoloGroup Solo, bool DefaultOn,
    double MinLog10U, double MaxLog10U,        // visibility window; 0.25-decade linear fade inside each end
    LabelCategory? Labels,                      // category its objects label under, or null
    string[] DataKinds,                         // manifest "kind"s it needs (§6.5.2); empty = Core-only
    string RequiresFeature,                     // a manifest features[] key, or null (R47): the layer's
                                                // content is a SUBSET of another layer's kind, so the kind
                                                // loading does not prove the content is there
    ProvenanceClass Weakest, string ProvenanceNote,
    LayerParam[] Params);

public sealed record LayerParam(string Id, string Caption, double Min, double Max, double Default,
    string Unit, bool IsPresentationGain);      // a gain away from Default raises a header chip

public sealed record Rung(int Index, string Id, string Caption, string Group,
    string CentreKind, string CentreId,         // how the framing centre is found (§5.1)
    double SpanMetres, double UnitsPerKm, PresentationPlane Plane, bool FollowOnArrival);

public static class LayerTable
{
    public static readonly LayerDef[] Layers;   // 46 rows, §5.4
    public static readonly Rung[] Rungs;        // 19 rows, §5.1
    public static LayerDef Find(string id);
    public static Rung FindRung(string id);
}
```

```csharp
// Core/Layers/LayerContext.cs — engine-free, built once per frame by ScaleRenderer,
// carried in FrameContext.LayerContext. This is the "ctx" of LayerState.Weight (C16).
public readonly struct LayerContext
{
    public readonly LoadedKinds Kinds;         // data availability, from DataLoader + manifest
    public readonly Stage Stage;               // Real | Fictional | Phenomenon
    public readonly float RoomVisibility;      // 1 = full passthrough, 0 = Space
    public readonly bool CmbBackdrop;          // the "CMB backdrop in passthrough" switch
    public readonly bool ViewerOnSite;         // rung `sky`: the viewer stands on a surface site
    public readonly double ViewerFromSunPc;    // |eye - Sun| in pc
    public readonly double NearestStarPc;      // nearest catalogue/layer star to the eye, pc
    public readonly float FocusPx;             // apparent diameter of the focus body, px
    public readonly float SunPx, EarthPx;      // same for the Sun and Earth (0 if not drawn)
    public readonly bool ViewerInsideHeliosphere;
    public readonly float HeliosphereDeg;      // angular extent of the heliopause shell, deg
    public readonly double NearestNebulaBoxRadii;  // distance to the nearest nebula close-up, in its box radii
    public readonly bool ObservedSkyMode;

    // R29: built in exactly one place (ScaleRenderer), so one constructor taking all 13 fields
    // in declaration order. A test can build one because the fields are all it needs.
    public LayerContext(LoadedKinds kinds, Stage stage, float roomVisibility, bool cmbBackdrop,
                        bool viewerOnSite, double viewerFromSunPc, double nearestStarPc,
                        float focusPx, float sunPx, float earthPx, bool viewerInsideHeliosphere,
                        float heliosphereDeg, double nearestNebulaBoxRadii, bool observedSkyMode);

    public bool Has(string kind) => Kinds.Has(kind);
    public bool HasFeature(string feature);   // manifest features[]; false when the key is absent (R47)
}

public readonly struct LoadedKinds          // a sorted string array plus a binary search; no allocation per frame
{
    public bool Has(string kind);
    public static LoadedKinds From(IReadOnlyList<string> kinds);
    public static readonly LoadedKinds Empty;   // R29: lets a test build a LayerContext before any load
}
public enum Stage : byte { Real, Fictional, Phenomenon }
```

```csharp
// Core/Layers/LayerState.cs — on/off, SOLO and param values; one pure question per layer.
public sealed class LayerState
{
    public bool IsOn(string id);  public void SetOn(string id, bool on);
    public SoloGroup Solo { get; set; }
    public double Param(string layerId, string paramId);
    public void SetParam(string layerId, string paramId, double value);

    /// Weight(id, log10U, ctx)
    ///   = on(id) * soloAllows(id) * window(id, log10U) * available(id, ctx)
    ///   window = clamp01((log10U - Min)/0.25) * clamp01((Max - log10U)/0.25)
    ///   available = every DataKind in LayerDef loaded (LayerReady) AND the layer's condition (§5.4)
    public double Weight(string id, double log10U, in LayerContext ctx);

    /// Why a row reads what it reads, for the panel: On | Off | OutOfWindow | Loading |
    /// Missing(file) | Invalid(reason) | NotInThisBuild(reason) | FeatureMissing(reason) |
    /// ConditionUnmet(text). FeatureMissing fires when LayerDef.RequiresFeature is set and the
    /// manifest's features[] says the importer did not produce it (R47); it renders as
    /// "not in this build (<reason from the manifest>)".
    public LayerStatus Status(string id, double log10U, in LayerContext ctx);
}
```

```csharp
// Core/Layers/LayerBudget.cs  (was A's Core/Catalog/LayerRecords.cs; C2)
public readonly struct LayerBudget
{
    public readonly int Draws, Vertices;
    public readonly float FragmentScreens, StepCoverage, TextureMB;
    public LayerBudget(int draws, int vertices, float fragmentScreens, float stepCoverage, float textureMB);
}

// Core/Layers/LabelCandidate.cs  (C17)
public readonly struct LabelCandidate
{
    public readonly string Id, Text;
    public readonly LabelCategory Category;
    public readonly Double3 PositionKm;     // absolute ICRF; used when IsDirectionOnly is false
    public readonly Double3 DirectionIcrf;  // used when IsDirectionOnly is true (sky content)
    public readonly bool IsDirectionOnly;
    public readonly float Priority;         // higher wins the greedy declutter (§5.6.10)
    public readonly ProvenanceClass Provenance;

    // R29: a readonly struct with no constructor cannot be built, and every runtime lane must
    // build these in CollectLabels. One constructor, two factories that are what lanes call.
    public LabelCandidate(string id, string text, LabelCategory category, Double3 positionKm,
                          Double3 directionIcrf, bool isDirectionOnly, float priority,
                          ProvenanceClass provenance);
    public static LabelCandidate At(string id, string text, LabelCategory c, Double3 positionKm,
                                    float priority, ProvenanceClass p);
    public static LabelCandidate Toward(string id, string text, LabelCategory c, Double3 directionIcrf,
                                        float priority, ProvenanceClass p);
}
```

```csharp
// Core/Nav/Destination.cs  (C's record with A's LayerId folded in; C2)
public enum DestinationKind { Rung, Body, Spacecraft, Site, Star, Cluster, Nebula, Galaxy,
                              Structure, Cmb, Phenomenon, FictionalSector }
public sealed record Destination(
    string Id, string Label, DestinationKind Kind,
    string BodyId,               // catalogue id for moving targets, else null
    string LayerId,              // the layer that produced it, else null for catalogue bodies
    Double3 PositionIcrfKm,      // for static targets
    double FramingRadiusKm, double RoomMetres, PresentationPlane? Plane,
    ProvenanceClass Provenance, string Source);
```

**Tests (headless, `T/LayerTableTests.cs`):** ids unique (**and every id in §5.4 appears exactly
once across the §9.5 slot claims plus the integrator's own rows** — R20, R32); every `Min < Max` and both inside
`ScaleLimits`; every rung in §5.1 has at least one default-on layer with `Weight > 0` at its U;
every `SoloGroup` except `None` has at least one layer; every `DataKind` string is a kind in the
§6.5.2 table; `Weight` fades continuously (no step larger than 0.1 between `log10U` samples
0.01 apart); `Status` returns `NotInThisBuild` for a layer whose kind is absent; rung `UnitsPerKm`
values match §5.1 to 4 significant figures; rung indices contiguous and `log10 U` non-increasing.
Added by this revision: **every layer whose data is a subset of another layer's kind declares a
`RequiresFeature`** (R47); **for every layer the declared `Weakest` glyph is at least as weak as
the weakest per-field provenance its Core type reports for what it draws** (R42); **at every
rung's U the LAYERS tab's "This rung" list is between 6 and 16 rows** (R48).

### 4.3 Lifecycle, visibility, provenance, budgets, LOD

1. **Create.** The bootstrap's `BuildLayers()` slot, one line per layer:
   `AddLayer<StarLayer>()` does `AddComponent` under the Universe host and then `Bind`. The
   bootstrap hands references; **no `FindAnyObjectByType`**. Layers never talk to each other.
2. **Data.** `DataLoader` walks the manifest in order and calls `OnData` for every layer whose
   `DataKinds` contains the kind. A kind absent from the build is never delivered, `Weight` stays
   0, and the panel row says "not in this build". Nothing throws, and dropping a source changes
   no code (binding 7).
3. **Every frame**, in this order (R28): `UpdateWeight(ctx)` on **every** registered layer;
   `DeclareGroups` on **only** the layers with `Weight > 0`; `ScaledSpace.Solve`; `AcceptGroup`;
   `Apply(ctx)` on the layers with `Weight > 0`. A layer that is off, out of window or missing its
   data therefore contributes **no** `GroupNode` and no `HasOpaque`, so the §2.6 push pass can
   never move a real group away from geometry that is not drawn.
4. **At most one `MayEnclose` volume renders per frame** (R2). `ScaleRenderer` keeps the enclosing
   group with the largest `Weight`, sets every other enclosing volume's `_Fade` to 0 and disables
   its renderer; ties break by layer id, ascending. Without this rule, `dust` (window -19.0 to
   -14.6) and `nebulae` (-17.5 to -13.5) overlap over 2.9 decades and two full-screen marches
   would run at once — 60 step-coverages against a global of 33 (§7.3). Enforced by the G1c
   `volumes` assertion.
5. **Catalogue change.** `UniverseSimulation.BodiesChanged` is a new event, raised by `AddBodies`
   and `RemoveBodies` and **not** by `SetCatalog`. It lets layers re-flag duplicated points
   (§3.4).
6. **Teardown.** `OnDisable` unregisters; `OnDestroy` frees meshes and materials (the
   `DestroyMaterials` pattern of `CelestialBodyView.cs:930-943`).

- **Visibility.** **Every `CosmoSynth/*` shader carries a `_Fade` float property, defaulting to
  1, in both its `Properties` block and its `UnityPerMaterial` buffer** (R30). A layer's `Apply`
  writes `Weight` into `_Fade` on every material it owns; the shader multiplies the returned
  colour by it, and the alpha too in premultiplied and opaque passes, so a 0.25-decade window fade
  is a fade and not a pop. Only three of the eleven shaders declared `_Fade` before this revision,
  which left `cmb`, `mwBand`, `worlds`, `exohosts` and every decoration layer with no uniform to
  write. `Sky`'s `_Immersion` is the passthrough/Space switch, **not** the fade: the CMB layer
  writes both. Gate G0b: every `CosmoSynth/*` shader with a `#pragma vertex` must declare `_Fade`.
  Layers never test scale, SOLO or data availability themselves: `Weight` already does all three.
- **Provenance and licence.** `LayerDef.Weakest` and `ProvenanceNote`, plus the manifest's
  attribution strings, feed the panel and the credits page. Per-object provenance stays in the
  Core types (the `provenance` / `provenanceBits` meta blocks, §6.1.2).
- **Budgets.** Each layer declares its ceiling; the play-mode `budget` assertion sums the rows
  with `Weight > 0`, plus bodies, and requires the §7.3 totals for every fixed capture view.
- **LOD** is internal to each layer (magnitude bands, chunk cone culling, sprite versus volume).
  The framework's only rule is never to exceed `Budget` at `Weight = 1`.

### 4.4 Groups, destinations, picks, labels

- **Groups.** A layer declares its rigid parts through `DeclareGroups` and places them in
  `AcceptGroup` with `Placement.PlaceRigid`. Examples: the dust volume (`MayEnclose = true`), the
  CMB object form (`MayEnclose = true`), the heliosphere shell on the Sun, a nebula close-up box,
  a hero galaxy impostor, an exhibit plinth. Point content is never declared: it is per-point.
- **Destinations.** The travel list has two sources. `UniverseSimulation` contributes bodies
  (Star, Planet, DwarfPlanet, Spacecraft, Site, and the active fictional systems) with the
  framing of §5.7.3. Every layer then adds its own through `CollectDestinations`: named galaxies,
  the galactic centre, Laniakea, the CMB, clouds, structures. Arrival is framed by
  `FramingRadiusKm` and never raises `CatalogLoaded`.
- **Picks.**
  - `BodySelector` keeps its ray–sphere test against the *placed* transforms (`RenderedRadius`
    and the drawn position; §2.6 makes that exact for rigid groups). It also asks every layer
    with `Weight > 0` for `TryPick` and takes the hit with the smallest angle, with 1.6 deg of
    forgiveness (today's value).
  - Layers compute candidates' rendered positions with the CPU replica
    `ScaledSpace.CompressRelative` in double. The scan is O(N) per click; hover scans at most
    5,000 of the brightest points at 10 Hz, over a coarse spatial hash for the 120 k stars.
  - **Measure, readouts and travel use `PickHit.PositionKm` (true, double), never render
    positions.**
- **Labels.** Every layer offers `CollectLabels`; `LabelLayer` (order 100) applies the budget,
  the priority order and the greedy screen-space rejection of §5.6.10. Only drawn things are
  labelled (`IsGeometryDrawn`, or a layer point with `Weight > 0` and on screen).

### 4.5 Placement mode and render queue for every layer id

Section §5.4 is the list of layers with ids, windows and defaults. This table adds what rendering
decides. Every one of the 46 ids belongs to exactly one row. If a new id is added, the owning
lane adds a row here in its report and the integrator merges it.

| Layer ids | Placement (§2.2) | Queue | Shader |
|---|---|---|---|
| `bodies`, `spacecraft`, `sites`, `exohosts` (hosts once expanded into bodies), `worlds` | bodies: rigid, or Envelope when the eye is near; floored bodies go to `BodyMarkers` | 2000 / 3050 | Body / Point (`_Space = 1`) |
| `atmospheres`, `clouds`, `winds`, `sunActivity`, `comets` | decorations of a body: they follow its placement and check `IsGeometryDrawn` | 3100 | Atmosphere, or small ports of it |
| `rings` | decoration: rigid with the body | 3000 | Ring |
| `orbits`, `trails` | ribbons owned by the parent's group; per-vertex when the owner contains the eye | 3060 | Ribbon |
| `stars`, `census`, `cepheids`, `globulars`, `masers`, `belts`, `density`, `quasars`, `galaxies2mrs`, `groupsCf4`, `sdss`, `cloudMarkers` | per-point chunks | 3050 | Point (`_Space = 0`) |
| `galaxiesLocal` | per-point, plus oriented discs above 3 px (a rigid quad per galaxy, placed per-point at its centre) | 3050 | Point plus a galaxy-disc port of Point |
| `stars`, the distance-unknown subset (146 rows); `mwBand` | sky (per eye, at infinity) | 1010 | Point (`_Space = 2`) or Sky |
| `cmb` | sky form: sky; object form: rigid layer group (`MayEnclose`) | 1000 / 3010 | Sky |
| `dust`, `nebulae` | Enclosing volume groups, plus a voxel-sprite LOD for `dust` | 3010 | Volume |
| `dustExtent`, `arms`, `sunOrbit`, `scaleRings`, `eclipticGrid`, `planeGrid`, `cylGrids`, `sgGrid`, `constellations`, `basins`, `parker` | ribbons: rigid when their centre group is rigid, per-vertex otherwise | 3060 | Ribbon |
| `heliosphere`, `oort`, `mwDisc` | a rigid layer group anchored on its centre (the Sun or the GC); per-vertex shell while the eye is inside | 3100 / 3010 | Atmosphere-like shell / Volume |
| `horizonGrid` | sky | 1010 | Ribbon, `_Placement = 2` (vertices are ICRF directions, placed per eye at SkyRadius) |
| `flare` | modulates `stars` (a per-point flag), draws nothing of its own | — | Point |

### 4.6 The canonical data-kind table

`LayerDef.DataKinds` strings, `manifest.json` `kind` strings and the `DataLoader.LayerReady` key
are **the same namespace**. This is the whole of it. A kind not in this table is a bug in a lane.

| kind | File | Core type | Layers that consume it | Lane |
|---|---|---|---|---|
| `solar_system` | `catalog/solar_system.json` | `CelestialCatalog` (via `CatalogAssembler`) | bodies, orbits, sites, atmospheres, rings, clouds, comets | solar |
| `spacecraft` | `catalog/spacecraft.json` | `CelestialCatalog` (via `CatalogAssembler`) | spacecraft, trails | solar |
| `models` | `models/*.csmesh` | `CsMesh.Model` | spacecraft | solar |
| `surfaces` | `surfaces/*` | `Texture2D` | bodies, clouds | solar |
| `stars` | `sky/stars.csstars` | `StarCatalog` | stars, flare, worlds | stars |
| `census` | `sky/census.cspt` | `PointSet` | census | stars |
| `stars_named` | `sky/stars_named.json` | `StarNames` | stars (labels), flare | stars |
| `constellations` | `sky/constellations.json` | `Constellations` | constellations | stars |
| `exoplanets` | `catalog/exoplanets.csexo` | `ExoplanetTable` | exohosts | exoplanets |
| `smallbodies` | `sun/smallbodies.cssb` | `SmallBodyTable` | belts | solar |
| `activity` | `sun/activity.cssa` | `SolarActivity` | sunActivity | solar |
| `snapshot` | `sun/snapshot.json` | `SpaceWeatherSnapshot` | parker, sunActivity (VITALS card) | solar |
| `dust` | `galaxy/dust.csdu` | `DustCube` (over `VoxelGrid`) | dust, dustExtent | milkyway |
| `cepheids` | `galaxy/cepheids.csgp` | `PointSet` | cepheids | milkyway |
| `globulars` | `galaxy/globulars.csgp` | `PointSet` | globulars | milkyway |
| `masers` | `galaxy/masers.csgp` | `PointSet` | masers | milkyway |
| `landmarks` | `galaxy/landmarks.json` | `PlaceCatalog` | cloudMarkers, nebulae, arms (labels), mwDisc | milkyway |
| `localvolume` | `lv/localvolume.cslv` | `GalaxySet` | galaxiesLocal | galaxies |
| `2mrs` | `lss/2mrs.csrs` | `GalaxySet` | galaxies2mrs | galaxies |
| `cf4_groups` | `lss/cf4_groups.cspt` | `PointSet` | groupsCf4 | galaxies |
| `cf4_basins` | `lss/cf4_basins.csgr` | `VoxelGrid` | basins | galaxies |
| `cf4_delta` | `lss/cf4_delta.csgr` | `VoxelGrid` | density | galaxies |
| `sdss` | `lss/sdss.cspt` | `PointSet` | sdss | galaxies |
| `structures` | `lss/structures.json` | `PlaceCatalog` | basins (labels), galaxies2mrs (labels), sdss (labels) | galaxies |
| `cmb` | `cmb/cmb_sky.cscb` | `CmbSky` | cmb | cmb |
| `fictional` | `catalog/fictional_holosystem.json` | `CelestialCatalog` | the Fictional stage | fiction |
| `encounter` | `phenomena/encounter.bin` + `.json` | `EncounterCapture` | the `galaxies` exhibit | fiction |
| `exhibits` | `phenomena/exhibits.json` | `ExhibitCatalog` | the Phenomenon stage | fiction |
| `quasars` | *not shipped in 2.0* | `PointSet` | quasars | — |

Core-only layers, with `DataKinds` empty: `arms`, `sunOrbit`, `mwDisc`, `mwBand`, `heliosphere`,
`oort`, `scaleRings`, `horizonGrid`, `eclipticGrid`, `planeGrid`, `cylGrids`, `sgGrid`.
`quasars` is deliberately left in the table with a kind that is never in the manifest: it is the
worked example of "not in this build" and a test row for `LayerState.Status`. **Its `LayerDef` row
is written by the integrator at P0.7, outside every slot**, because it belongs to no lane (R32).
---

## 5. Product: the ladder, the stages, the layers, the panel

Authority: Product section C, inside the Foundation's constants.

### 5.1 The one scale limit and the nineteen rungs

```csharp
// Core/Presentation/ScaleLimits.cs  (engine-free; integrator-owned, created at P0.3)
public static class ScaleLimits
{
    public const double MinUnitsPerKm = 2e-25; // observable universe (8.8e23 km) = 0.176 m
    public const double MaxUnitsPerKm = 1e3;   // true scale: 1 m of space = 1 m of room
    public static double Clamp(double unitsPerKm) =>
        Math.Min(MaxUnitsPerKm, Math.Max(MinUnitsPerKm, unitsPerKm));
    public static bool AtLimit(double unitsPerKm, out bool atMax);   // for the haptic bump and the readout
}
```

Why these two numbers. **Max 1e3**: CosmoSynth never needs to magnify space; the smallest things
anyone frames are landers (about 3 m), comfortable at hand size (0.4 m / 0.003 km = 133). Today's
ceiling of 1e6 (VERIFIED `TabletopManipulator.cs:49`) allows 1 m of space to fill 1 km of room
and serves nothing. Capping at 1:1 makes "true scale" a fact the user can feel. **Min 2e-25**:
the CMB rung puts the observable universe (radius about 4.4e23 km) at 1.2 m across, and the limit
lets the user pull it to a palm-sized 17.6 cm without allowing a scale at which it vanishes.
Today's floor of 1e-20 (`FloatingOrigin`/manipulator) is five decades short. Range: **27.7
decades**.

At a limit the scale stops, the controller pulses once (0.5 amplitude, 30 ms) and the header
readout flashes "limit: true scale (1:1)" or "limit: observable universe" for 1.5 s. **No other
clamp may exist anywhere in the input path.**

A rung is a triple (scale, framing centre, plane). Scale is either a fixed U or a span-to-room
size relative to the focus. Spans are diameters. U computed with AU = 1.495978707e8 km,
ly = 9.4607e12 km, pc = 3.0857e13 km (INFERRED arithmetic).

| N | Id | Group | Name on the panel | Framing centre | Span to room | U | log10 U | 1 m in room | Plane | Follow on arrival |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | `sky` | EARTH | Sky from Earth | the observer on the chosen site, on the rotating Earth | true scale | 1e3 | +3.000 | 1 m | Horizon | the site (co-rotating) |
| 1 | `leo` | EARTH | Low Earth orbit | a point 420 km above the day-side surface | true scale | 1e3 | +3.000 | 1 m | LocalVertical | Earth centre (translation only) |
| 2 | `planet-room` | EARTH | Planet, room-size | focus planet (Earth if the focus is not a planet or moon) | 2R to 2.5 m | Earth 1.962e-4 | -3.707 | 5,100 km | FocusEquator | focus |
| 3 | `planet-hand` | EARTH | Planet in hand | focus planet | 2R to 0.30 m | Earth 2.354e-5 | -4.628 | 42,500 km | FocusEquator | focus |
| 4 | `planet-moons` | EARTH | Planet and moons | focus planet | 2a(outermost major moon) to 0.55 m | Earth-Moon 7.154e-7 | -6.145 | 1.4e6 km | FocusEquator | focus |
| 5 | `inner` | SOLAR SYSTEM | Inner planets | the focus's host star (Sun by default) | 2 x 1.6 AU to 0.6 m | 1.253e-9 | -8.902 | 5.33 AU | Ecliptic | host star |
| 6 | `system` | SOLAR SYSTEM | Whole Solar System | host star | 2 x 31 AU to 0.7 m | 7.547e-11 | -10.122 | 88.6 AU | Ecliptic | host star |
| 7 | `heliosphere` | SOLAR SYSTEM | Heliosphere | Sun | 2 x 300 AU to 0.7 m | 7.799e-12 | -11.108 | 857 AU | Ecliptic | Sun |
| 8 | `oort` | SOLAR SYSTEM | Oort cloud | Sun | 2 x 2 ly to 0.7 m | 1.850e-14 | -13.733 | 5.71 ly | Ecliptic | Sun |
| 9 | `nearest` | STARS | Nearest stars | Sun | 2 x 12 ly to 0.7 m | 3.083e-15 | -14.511 | 34.3 ly | Ecliptic | Sun |
| 10 | `neighbourhood` | STARS | Solar neighbourhood | Sun | 2 x 130 ly to 0.7 m | 2.846e-16 | -15.546 | 371 ly | Galactic | Sun |
| 11 | `clouds` | STARS | Local clouds | Sun | 2 kpc to 1.6 m | 2.593e-17 | -16.586 | 1.25 kpc | Galactic | none |
| 12 | `milkyway` | GALAXIES | Milky Way | Galactic Centre (Sgr A*) | 40 kpc to 0.7 m | 5.671e-19 | -18.246 | 57.1 kpc | Galactic | none |
| 13 | `halo` | GALAXIES | Halo and satellites | Galactic Centre | 600 kpc to 0.7 m | 3.781e-20 | -19.422 | 857 kpc | Galactic | none |
| 14 | `localgroup` | GALAXIES | Local Group | midpoint Milky Way to M31 | 3 Mpc to 0.9 m | 9.722e-21 | -20.012 | 3.33 Mpc | Supergalactic | none |
| 15 | `localvolume` | GALAXIES | Local Volume | Milky Way | 20 Mpc to 0.9 m | 1.458e-21 | -20.836 | 22.2 Mpc | Supergalactic | none |
| 16 | `laniakea` | BEYOND | Laniakea | Milky Way | 250 Mpc to 1.0 m | 1.296e-22 | -21.887 | 250 Mpc | Supergalactic | none |
| 17 | `cosmicweb` | BEYOND | Cosmic web | Milky Way | 2 Gpc to 1.2 m | 1.944e-23 | -22.711 | 1.67 Gpc | Supergalactic | none |
| 18 | `cmb` | BEYOND | Observable universe and CMB | Milky Way (the observer; the CMB is observer-centred) | 8.8e23 km to 1.2 m | 1.364e-24 | -23.865 | 23.8 Gpc | Supergalactic | none |

Rungs 2–4 are **relative**: the U shown is for Earth; for another focus `U = room / span(focus)`.
Rungs 5–9 use the focus's host star when the focus belongs to another system, so "Whole system"
around Proxima frames Proxima's planets.

Room sizes: 0.55–0.7 m (tabletop, the size the user has lived with since 1.4.0 without
complaint), 0.9–1.2 m for the extragalactic rungs whose content is a sparse cloud, 1.6 m for
`clouds` (it spreads the dust cube across the room), 2.5 m for the room-size planet, 1:1 for the
two Earth vantages.

The **current rung** shown in the panel is the one nearest in `log10 U`, and exact when within
0.05 decades of it and framed on its centre. Continuous zoom updates the highlight; it never
snaps.

**Moving up and down.** Three inputs change scale, all through `ScaleLimits.Clamp`, all with the
same pivot rule.

1. **Two-hand gesture** (both grips, or both pinches): today's `TabletopManipulator` law, factor
   `(span / span0)^2`, log-space smoothing `1 - 0.5^(60 dt)` (VERIFIED), plus a **yaw-only twist**
   from the horizontal inter-hand vector. The sticky target is cleared when reached (§3.6).
   Hand points are converted with `Universe.InverseTransformPoint` first (§3.6).
2. **Right stick Y** (mirrored for left-handed): **0.6 decades per second** at full deflection,
   deadzone 0.12, rate `0.6 * sign(y) * ((|y| - 0.12)/0.88)^1.5`, times the left-trigger boost
   (x1 to x4). 27.7 decades take 46 s unboosted, 11.5 s boosted. **Mouse wheel** on desktop:
   0.1 decade per notch.
3. **Rung tap** in GO (or `rung:<id>` in the harness): a `Travel` to the rung's framing.

**Pivot rule** (one function, `ScaleControl.ZoomPivot`): (a) the followed object if Follow is on;
else (b) the selected object if it is drawn (`IsGeometryDrawn`, or a visible layer point); else,
for the two-hand gesture only, (c) the hands' midpoint if any drawn object lies within 0.6 m of
it (today's rule); else (d) the anchor, the tabletop centre. The pivot's absolute km position is
held fixed in the room while U changes.

**Flight** (left stick) moves the viewer at
`ScaleSpace.NavigationSpeed(distance to the nearest object of any layer)`, so the same deflection
crosses the visible scene in a few seconds at every rung. Two changes from today: the direction
is the full head-forward vector (looking up and pushing forward rises; the separate rise axis is
gone), and the nearest-object query includes layer points, not only `CelestialBodyView`s (§3.5).

**Cancelling:** any grab, stick zoom, stick flight, rung tap or GO cancels a travel in progress.
A zoom never changes the plane.

**Readout** (panel header and watch face): the rung name, then `1 m = <distance>` on the unit
ladder m, km, AU, ly, kpc, Mpc, Gpc to 3 significant figures, e.g. `Whole Solar System · 1 m =
88.6 AU`. The watch face shows the `log10 U` position as a tick on a 27.7-decade arc with the
five group names.

**How honest the distances are** (INFO -> THIS VIEW text, not a badge): angular sizes and
directions are exact, and so is front-to-back order inside a group; what a far group loses is
stereo depth beyond the compression knee, and a real observer cannot perceive stereo depth beyond
about 76 m either (INFERRED), so no badge is shown for it. **Every number is true**: the
inspector, the measure tool, the readout and the travel planner all use absolute double km. The
only departures that get a badge are the ones that change what the eye reads as size or
brightness (§5.9.6, §5.5.2).

### 5.2 The room-level plane: policy

```csharp
// Core/Presentation/PresentationPlane.cs  (integrator-owned, created at P0.3)
public enum PresentationPlane { Horizon, LocalVertical, FocusEquator, Equatorial, Ecliptic,
                                Galactic, Supergalactic }

// R31: ONE declaration of the up-vector function and its context. §3.2, §3.6 and §11.3 all name
// `Planes.Up(plane, ctx)`; nothing is called `PlaneUp`, and `PresentationPlane` is an enum and
// carries no method.
public readonly struct PlaneContext
{
    public readonly double SiteLatDeg, SiteLonDeg;   // rung `sky`; ignored by the fixed planes
    public readonly Double3 ViewerKm;                // absolute ICRF, for LocalVertical
    public readonly string FocusBodyId;              // for FocusEquator; null otherwise
    public readonly CosmicTime Now;
    public PlaneContext(double siteLatDeg, double siteLonDeg, Double3 viewerKm,
                        string focusBodyId, CosmicTime now);
}
public static class Planes
{
    public static Double3 Up(PresentationPlane plane, in PlaneContext ctx);   // ICRF unit vector
}

// Planes.Up(plane, ctx) -> ICRF unit vector placed on room-up:
//   Horizon       local zenith at the site at time t (rotates with Earth)
//   LocalVertical radial from Earth's centre through the viewer
//   FocusEquator  the focus body's IAU north pole at t (BodyOrientation.PoleDirectionAt)
//   Equatorial    ICRF +z
//   Ecliptic      ecliptic north pole, J2000, obliquity 84381.448" (StandardFrames.EquatorialToEcliptic)
//   Galactic      NGP, RA 192.85948, Dec 27.12825 (StandardFrames.EquatorialToGalactic, §3.2 fix)
//   Supergalactic SGP at (l, b) = (47.37, +6.32)  (StandardFrames.GalacticToSupergalactic)
```

`PlaneContext` is built by the presentation-plane driver (§3.6) and by `ScaleRenderer` for the
`plane` assertion; nothing else constructs one.

1. **The plane changes only on a rung tap, a GO, or the PLANE control.** Continuous zoom, flight
   and grabs never tilt the universe.
2. The levelling rotation is the minimal one taking the old up-vector to the new, followed by the
   yaw that keeps the travel target in front of the user; it is animated as part of the travel
   (§3.6), and blinks in Space mode for tilts over 10 deg.
3. `PLANE: Auto` (the default) uses the rung's plane on a rung tap, and on a GO the plane of the
   rung nearest in `log10 U` to the arrival scale; stars keep the current plane. Choosing a fixed
   plane in TOOLS -> PLANE pins it until Auto is chosen again.
4. When zooming has left the user nearer to a rung whose plane differs from the active one, a
   small chip appears in the panel header and on the watch face: "Level to Galactic". Tapping it
   levels; ignoring it is fine. **It never levels by itself.**
5. **Horizon** puts *north behind the user* at the moment of arrival, so a northern-hemisphere
   user faces south, where the ecliptic and the planets are. The real room's compass heading is
   unknown (Quest has no compass, INFERRED), so the compass ring at the horizon says N/E/S/W and
   the user turns with snap turn or a two-hand twist.

### 5.3 Stages: one universe, three kinds of truth

The room holds **one active stage** at a time. Switching fades the old one out and the new one in
over 0.4 s at the same anchor; inactive stages keep their state and resume where they were.

| | **Real universe** (default) | **Fictional sector** | **Phenomenon exhibit** |
|---|---|---|---|
| What it is | the catalogued universe at real positions and real time | authored or generated places that make no claim about reality | small models of real physics, schematic by design |
| Provenance rule | Measured / Inferred; Procedural only as "Procedural over a Measured position" | every field Fictional (authored) or Procedural (generated, with the seed shown) | laws and exact ratios Inferred (the formula is the source); simulated dynamics Procedural (generator and seed); illustrative visuals Fictional |
| Visual signature | no frame; content floats in the room or fills it | **neon projector skin**: octagonal plate, emitter, upward beam, flow-filament orbits, bracket labels, all chrome in the Fictional hue. Reserved: never used on real content | **plinth**: a dark disc 0.8 m across with a 3 mm rim in the exhibit family's content colour, and the model badge on its edge |
| Header chip | `REAL` | `FICTIONAL SECTOR ?` in the Fictional colour | `PHENOMENON · <badge>` |
| Clock | global `SimulationClock` (TDB) | sector clock, local, authored rate (play/pause/speed x0.25–x4) | exhibit timeline, local, 30–40 s (play, pause, restart, scrub, x0.25/0.5/1/2) |
| Scale | U within `ScaleLimits` | hologram scale x0.15–x2.5 | diorama scale x0.25–x1.6 |
| Entry | app start; "Back to real universe" | GO -> FICTIONAL SECTORS | GO -> PHENOMENA, or the host object's inspector |
| Exit | — | header chip "Back to real universe" | header chip "Back to <host>" or "Back to real universe" |

The global clock keeps running while another stage is active. Real and invented content are never
in the room at the same time: two scales, two clocks and two grab targets side by side would make
every gesture ambiguous and would invite the reading that an exhibit is to scale with the real
Sun beside it.

**One exception: room furniture** (R45). The specimen tray (§5.9.7) is a room-anchored *object*,
not a stage. It lives **outside the Universe host**; the two-hand manipulator, `ScaleControl`,
`ScaleRenderer` and the group solver ignore it entirely; it is never a `Destination`; it carries
the persistent `MODEL COPY — not the simulation` plate on its rim; and grabbing a specimen claims
that hand the way the panel does, through a new claim `TrayHot(hand)` alongside `PanelHot(hand)`
(§5.6.8). **Nothing else may use this exception**: a new room object needs a ruling written here
first. The tray is the only thing in the app that is neither a stage nor part of the universe.

**Fictional content has exactly one door.** The arcade slingshot is reached only through
**GO -> FICTIONAL SECTORS -> Arcade slingshot**, beside VG-429 and "Generate a system"; TOOLS ->
SANDBOX offers the **real** sandbox only, with a one-line pointer to the arcade (R45). Otherwise
the GO list stops being the answer to "where is the made-up stuff".

**A Core test enforces the boundary** (`T/StageBoundaryTests.cs`): no field reachable from the
real catalogue is `Fictional`; no field of a Fictional catalogue is anything but `Fictional` or
`Procedural`; no Fictional body carries an ICRF `PositionKm`.

**`StageDirector`** (one Runtime component) owns the active stage (`Real`,
`Fictional(sectorId)`, `Phenomenon(exhibitId, hostId?)`), the 0.4 s cross-fade, the header chip,
the per-stage panel content and which root the manipulator, selector and labels act on. It stores
the real universe's state (origin km, U, plane, focus, follow, layer state) on leaving and
restores it on return. Harness actions: `stage:real`, `stage:fictional:<id>`,
`stage:phenomenon:<id>`.

**Real universe.** Everything in §5.1 and §5.4. Two kinds of invented content are allowed inside
it, both badged: generated worlds around real stars (`NearbyStarSystems`, Procedural with the
seed shown, ids `gen-hyg<row>-...`) and Procedural appearance over Measured positions (galaxy
sprites, the Milky Way disc impostor, nebula close-ups, belt colours). The projector skin is
never used here.

**Fictional sector: VG-429 Holosystem.** The user's holographic star map, merged from the three
variants: node names and rings from the base file, inclined orbits and the twin-helix filaments
from variant (2).

- **Data:** `catalog/fictional_holosystem.json` (§6.4.14), loaded lazily when the stage opens,
  parsed into its own Core `CelestialCatalog`, validated by `CelestialCatalog.Validate()` and
  **never merged into the real one**, so the single `SetCatalog` is untouched. The sector spans
  2.6 m and needs no floating origin or scale space: one Runtime `SectorView` evaluates the
  sector catalogue's frame tree at sector time and draws its eight bodies, rings, orbit filaments
  and labels under the sector root. **No second `UniverseSimulation` is created** (today's
  components find each other by global search and would bind to the wrong one).
- **Orbits are real Kepler conics** with the authored values of §6.4.14 (a in AU from the
  source, authored e = 0.02–0.05 because the source's 0.46–0.58 made neighbouring orbits cross,
  inclinations from the source, a central star of 1.2 solar masses). `SectorView` draws 1 AU as
  1 m at sector scale x1. The sector clock's x1 rate makes the innermost node (DD-210,
  a = 0.22 AU, period about 34.4 d) lap in 12 s, close to the source's 11.6 s at 72 Hz: about
  2.5e5 sector seconds per real second (INFERRED).
- **Skin inputs** (colours, radii and ring flags VERIFIED from the source; `a` and inclination
  superseded by §6.4.14 where they differ):

  | Id | Name | Colour | Radius | a | Incl. (rad) | Rings |
  |---|---|---|---|---|---|---|
  | VG-429 | Alpha Core Prime (the star) | #ff7a1a | 0.14 | 0 | 0 | yes |
  | DD-210 | Searing Fragment | #ff3b1a | 0.04 | 0.22 | 0.05 | no |
  | PF-919 | Chrono Frontier | #ff4f1a | 0.08 | 0.35 | 0.15 | yes |
  | DA-125 | Desert Sentinel | #ee9a44 | 0.06 | 0.52 | -0.22 | no |
  | AG-514 | Sub-Zero Basin | #4dd2ff | 0.09 | 0.75 | 0.35 | yes |
  | CH-024 | Gaseous Nebula Orbit | #2b8eff | 0.05 | 0.90 | -0.10 | no |
  | AZ-704 | Outer Core Ring | #ffa200 | 0.085 | 1.10 | 0.45 | yes |
  | AR-503 | Deep Boundary | #2b77ff | 0.10 | 1.30 | -0.30 | yes |

  The sector is 2.6 m across at x1 and opens at x0.3 = 0.8 m.
- **Look:** projector skin (plate, emitter, beam `fade = (1 - v)^4`, stripes); flow-filament
  orbit ribbons with `f = 1/2 + 1/2 sin(uR - ts)`, `b = f^8` travelling in the direction of
  motion; rim-bright star glow with the plus/minus 5 % pulse; sector dust (250 points, part of
  the fiction); slow turntable (0.03 rad/s, toggle); bracket labels with pointer lines, in the
  Fictional hue with the `?` glyph; **upright billboards** (the source's fixed double-sided planes
  read mirrored from behind). Orbit style selector: Twin helix (default), Bundle, Pulse. Body
  colours stay the authored node colours: they are content, not UI.
- **Inspector:** authored values only (a, e, period, radius), computed from the Kepler orbit and
  labelled Fictional. The source's "PLANETARY METRICS" panel with `Math.random()` velocity is not
  ported.
- **Dropped:** the header "SYS_MAP // MARVEL_COSMOS" and the reference-image comment (IP).
- **Generated sectors:** GO -> FICTIONAL SECTORS also offers "Generate a system" with a seed
  (default the last one, plus a "New seed" button), using `PlanetGenerator.GenerateSystem`
  exactly as today's `_proceduralSeed` path, ids namespaced `gen-<seed>-...`, provenance
  Procedural with the seed shown, and the projector skin.

**Phenomenon exhibits.** The nine Cosmic Atlas XR exhibits: physics into `Core/Phenomena/*.cs`
with the Node tests translated, the N-body capture as a StreamingAsset checked against its
SHA-256, and the texts copied verbatim into `phenomena/exhibits.json`.

| # | Id | Title (verbatim) | Family | Badge | Parameter (range, default) | Layers (verbatim) | Real host |
|---|---|---|---|---|---|---|---|
| 1 | `black-hole` | At the event horizon | Relativity | ANALYTIC MODEL | Black hole mass 5–50 M_sun, 20 | Curvature mesh · Photon sphere · Spatial labels | **Sgr A\*** ("the model is scale-free: horizon : photon sphere : ISCO = 1 : 1.5 : 3 holds for Sgr A* too; its mass is far outside the slider") |
| 2 | `merger` | When black holes meet | Relativity | SCHEMATIC MODEL | Mass ratio q 0.25–1, 0.7 | Wave surface · Strain trace · Spatial labels | none in the shipped data; list entry only |
| 3 | `lensing` | Gravity bends the view | Relativity | THIN-LENS MODEL | Source offset beta/theta_E 0–1.5, 0.35 | Light paths · Einstein ring · Spatial labels | **any named galaxy cluster** ("a point-mass lens; real cluster lenses are extended") |
| 4 | `galaxies` | A galactic encounter | Cosmology | HEADLESS CAPTURE | Trail persistence 0–1, 0.6 | Particle trails · Galaxy centres · Spatial labels | **M31** ("a generic two-disc encounter, not a forecast of the Milky Way–M31 merger") |
| 5 | `cosmic-web` | From ripples to structure | Cosmology | DISPLACEMENT MODEL | Perturbation amplitude 0.3–1.3, 1 | Filament guides · Void markers · Spatial labels | **the `cosmicweb` rung** and the `sdss` layer row |
| 6 | `dark-matter` | The invisible gravitational halo | Cosmology | ORBITAL MODEL | Halo strength 0–2, 1 | Invisible-mass halo · Rotation curves · Spatial labels | **the Milky Way** |
| 7 | `stellar-interior` | Inside a living star | Stellar | STRUCTURAL MODEL | Cutaway opening 0.35–1.8 rad, 1.25 | Energy transport · Convection cells · Spatial labels | **the Sun** |
| 8 | `reconnection` | Magnetic fields let go | Stellar | SCHEMATIC MODEL | Reconnection rate 0.3–1.5, 0.8 | Magnetic field lines · Current sheet · Spatial labels | **the Sun** |
| 9 | `alfven` | A wave carried by magnetism | Stellar | LINEAR MHD SOLUTION | Background field B0 0.5–2, 1 | Field lines · Reference positions · Spatial labels | **the Sun** |

Behaviour: **entry from a host** places the plinth where the host was rendered, clamped to
0.5–1.2 m from the eyes, so the transition reads as "into the Sun"; entry from the GO list places
it 0.9 m ahead and 0.25 m below eye height; the diorama fits a 0.8 m box at x1. In the exhibit
stage GO lists the nine by family with Previous/Next; LAYERS holds the three layer toggles and
the parameter slider; TIME holds the timeline (scrubbing pauses; play stops at the end, as the
source does), Play/Pause, Restart and speed; INFO holds **Observe** (explanation, equation,
question, observation, the active note) and **Model & limits** (method title, method, research
reference, and for the galaxy exhibit the capture information). Labels are tappable and open
their note in INFO. The status chip reads `PAUSED`, `CAPTURE PLAYBACK` or `LIVE MODEL`, verbatim.
Geometry uses ribbons of angular width and CosmoSynth star sprites instead of 1-px lines and GL
points, with analytic motion in vertex shaders. **If the capture fails its hash**, the
`galaxies` exhibit row reads "capture failed verification" and cannot be opened; the other eight
are unaffected.
### 5.4 The complete layer list (46 rows of `LayerTable.Layers`)

**The Prov. column is the weakest class of what the layer DRAWS at the current time, not of the
stored value** (R42). It is exactly `LayerDef.Weakest`, and `LayerTableTests` asserts that the
declared glyph is at least as weak as the weakest per-field provenance the layer's Core type
reports for the fields it draws. This is the galaxy defect (B4, "542/911 default ellipticities
drawn as if measured") one level up: stored elements can be Measured while the position drawn at
the simulation time is a two-body propagation, and an orbit whose eccentricity was *assumed* is
not a measured orbit. **Matching inspector rule:** a field whose importer flag says the value was
assumed prints "assumed (not published)" in the Inferred colour, never a bare number.

Windows are `log10 U`. Provenance glyphs: `=` Measured, `~` Inferred, `*` Procedural, `?`
Fictional. "Extra condition" is the `ctx` term of `LayerState.Weight` (§4.2); everything it names
is a field of `LayerContext`. "Data / licence" names the kinds of §4.6.

**SOLAR SYSTEM** (SOLO: Solar) — 15 layers

| Id | Caption | Default | Window | Extra condition | Labels | Prov. | Data / licence |
|---|---|---|---|---|---|---|---|
| `bodies` | Sun, planets, moons, small bodies | on | -13.5 ... +3.0 | the Sun body hands over to the `stars` point below -13.5, so the Sun is drawn once | Bodies | = radii and elements, ~ position away from the Horizons fit (chip `EPHEMERIS EXTRAPOLATED`) | `solar_system` |
| `orbits` | Orbits | on | -13.0 ... +3.0 | per orbit: drawn only from outside and above 8 px | — | = elements, ~ drift | (bodies) |
| `spacecraft` | Spacecraft | on | -12.6 ... +3.0 | each craft only inside its `KnownFrom`–`KnownUntil` window | Spacecraft | = | `spacecraft`, `models` |
| `trails` | Mission trails | off (on while a mission plays) | -12.6 ... +3.0 | trajectory history up to *now* in sim time | Spacecraft | = (Horizons); the Apollo 11 CSM path ? illustrative | `spacecraft` |
| `sites` | Surface sites | on | -5.5 ... +3.0 | host drawn at 170 px or more | Sites | = | `solar_system` |
| `atmospheres` | Atmospheres | on | with bodies | `IsGeometryDrawn` | — | ~ colour | (bodies) |
| `rings` | Planetary rings | on | with bodies | `IsGeometryDrawn` | — | = radii, * texture | (bodies) |
| `clouds` | Earth clouds | on | with bodies | `EarthPx >= 60` | — | = NASA Visible Earth, static | `surfaces` |
| `winds` | Zonal winds | off | with bodies | `FocusPx >= 60`; giants, Venus, Earth, Mars | — | * over ~ profiles | wind profiles (deferred, §6.9.2 G6) |
| `belts` | Asteroid and Kuiper belts | on | -13.2 ... -4.7 | — | SolarLandmarks | ~ position (two-body propagation from the SBDB epoch; the error grows with \|t - epoch\|), = elements at epoch | `smallbodies` |
| `comets` | Comet tails | on | with bodies | only within 5 AU of the Sun, length growing as r falls | — | * | (bodies) |
| `heliosphere` | Heliosphere | on | -12.6 ... -9.6 | `!ViewerInsideHeliosphere && HeliosphereDeg < 90` | SolarLandmarks | = crossing points, ~ shape | Core model |
| `parker` | Parker spiral | off | -11.2 ... -7.5 | — | — | * geometry over = L1 wind speed | `snapshot` |
| `oort` | Oort cloud | on | -14.8 ... -11.6 | a diffuse statistical shell, never a crisp sphere | SolarLandmarks | ~ (never observed) | Core model |
| `sunActivity` | Sun activity | on | with bodies | `SunPx >= 60` | — | * spots and corona over = monthly sunspot number | `activity` (SSN tag `nc`) |

**STARS** (SOLO: Stars) — 8 layers

| Id | Caption | Default | Window | Extra condition | Labels | Prov. | Data / licence |
|---|---|---|---|---|---|---|---|
| `stars` | Stars (3D) | on | -19.6 ... +3.0 | model (flux-true) or observed-sky mode; the 146 distance-unknown stars go on the sky layer only while `ViewerFromSunPc < 0.5` | StarNames | = position, ~ luminosity and colour | `stars` (CSS2; `by-sa`) |
| `census` | Nearest-star census | on | -16.0 ... -12.0 | brown dwarfs, white dwarfs and M dwarfs within 10 pc; `RequiresFeature = "census"` | StarNames | = (Reyle 2021) | `census` = **its own file** `sky/census.cspt`, tag `nc` (R40) |
| `exohosts` | Exoplanet systems | on | -17.0 ... +3.0 | a ring on each host; the focus host's planets and orbits drawn | StarNames, Bodies | ~ orbit: a and period = where published, e and i **assumed** (0, 90 deg) on most rows and flagged per planet (C.3 flags 1 and 2), * orientation | `exoplanets` |
| `worlds` | Generated worlds | on | with stars | `NearestStarPc < 1` | Bodies | * (seed shown) | Core generator |
| `flare` | Flare stars (illustrative) | off | with stars | the named flare stars flicker; the label gets a flare glyph; `RequiresFeature = "flare"` (R47) | — | * timing; class ~ | `stars_named` flag (§6.9.2 G7) |
| `constellations` | Constellation figures (3D) | off | -17.0 ... +3.0 | 3D segments between real stars, so they distort away from the Sun, which is the point | Constellations | = endpoints, ? figures (cultural convention) | `constellations`, CC BY-SA 4.0 |
| `cepheids` | Classical Cepheids | on | -20.2 ... -15.5 | colour by age; size by the Leavitt-law M from the period | — (pickable) | ~ distances (P–L), `distErr` as the uncertainty | `cepheids` (tag `unclear`; dropped by `public-nc` and `commercial`) |
| `globulars` | Globular clusters | on | -20.8 ... -16.0 | a point below about 3 px, a resolved sprite above | MilkyWay | = / ~ (LVDB) | `globulars` |

**DUST AND NEBULAE** (SOLO: Dust) — 4 layers

| Id | Caption | Default | Window | Extra condition | Labels | Prov. | Data / licence |
|---|---|---|---|---|---|---|---|
| `dust` | Milky Way dust (3D) | on | -19.0 ... -14.6 | step budget from the quality governor; extinction applied to the **de-reddened luminosity** of stars behind it, **never on top of the catalogue V** (R41) | Clouds | = position, ~ density (Vergely 2022, mag/pc), * colour ramp | `dust` (tag `nc`; fallback Edenhofer) |
| `dustExtent` | Dust map extent | off | with dust | a box outline: where the measurement ends | — | = survey geometry | (dust) |
| `nebulae` | Nebula close-ups | on | -17.5 ... -13.5 | Orion, Crab, Local Bubble; `NearestNebulaBoxRadii < 3` | Clouds | * shape and colour over = position | Core fields; positions from `landmarks` |
| `cloudMarkers` | Molecular clouds | on | -18.2 ... -14.5 | 9 markers, a reference each | Clouds | = (l, b, d) with citation | `landmarks` |

**GALAXIES** (SOLO: Galaxies) — 6 layers

| Id | Caption | Default | Window | Extra condition | Labels | Prov. | Data / licence |
|---|---|---|---|---|---|---|---|
| `arms` | Spiral arms | on | -19.6 ... -16.6 | Reid 2019, drawn only over each arm's fitted azimuth range and faded outside it | MilkyWay | ~ (log-periodic fit) | Core constants |
| `mwDisc` | Milky Way disc (impression) | on | -21.8 ... -17.4 | fades out as the dust, arms and stars take over from inside | MilkyWay | * constrained by ~ arms | Core plus texture |
| `mwBand` | Milky Way band from inside | on | -16.6 ... +3.0 | Space mode only (`RoomVisibility <= 0.35`) | — | * oriented to the NGP | Core |
| `sunOrbit` | Sun's galactic orbit | on | -19.6 ... -17.2 | a ring at R0 = 8.15 kpc about Sgr A* | Guides | = R0 (Reid 2019) | Core constant |
| `masers` | Maser parallaxes | on | -19.6 ... -16.6 | the 199 high-mass star-forming regions that define the arms; drawn with `arms` so the fit and its data are seen together | MilkyWay | = (VLBI parallaxes) | `masers` |
| `galaxiesLocal` | Local Group and Local Volume galaxies | on | -22.3 ... -17.8 | oriented discs from PA and axis ratio in the sky frame; unknown fields drawn round and faint | LocalGroup | = / ~ distances, = morphology where present, * where unknown | `localvolume` (LVDB `cc0`; UNGC giants `nc`, fallback hand-entered giants) |

**BEYOND** (SOLO: Beyond) — 7 layers

| Id | Caption | Default | Window | Extra condition | Labels | Prov. | Data / licence |
|---|---|---|---|---|---|---|---|
| `galaxies2mrs` | 2MRS galaxies | on | -24.0 ... -20.4 | redshift distances with the stated H0 | Structures | = direction, ~ distance | `2mrs` (tag `nc`; fallback SDSS only) |
| `groupsCf4` | Cosmicflows-4 groups | off | -24.0 ... -20.4 | measured distances; turning it on dims 2MRS to 30 % so the two can be compared | Structures | = distance moduli (about 20 % scatter) | `cf4_groups` (tag `nc`) |
| `basins` | Laniakea and neighbours | on | -23.4 ... -20.8 | watershed outlines, one hue step per basin (content colours, not UI) | Structures | ~ (CF4 reconstruction) | `cf4_basins` (tag `unclear`) |
| `density` | Density field | off | -23.4 ... -20.8 | the CF4 64^3 overdensity as points above delta = 1 | — | ~ | `cf4_delta` (tag `unclear`) |
| `sdss` | SDSS cosmic web | on | -24.7 ... -21.6 | a subsample to z = 0.2 | Structures | = direction, ~ distance | `sdss` (`pd`) |
| `cmb` | Cosmic microwave background | on | object form -24.7 ... -23.3; sky form -23.3 ... -21.6 | the sky form (at infinity, behind everything) shows at the BEYOND rungs in Space mode, or at any scale with `CmbBackdrop`; the object form is the 1.2 m ball of rung 18. **The two forms never draw together** | Structures | = over 97.9 % of the sky, ~ over the inpainted 2.1 %, false colour | `cmb` (Planck `nc`; fallback WMAP at the same path) |
| `quasars` | Quasars | off | -24.7 ... -22.0 | only if a quasar file ever ships | — | = direction, ~ distance | `quasars` (**not in the 2.0 plan**: the worked "not in this build" case) |

**GUIDES** (SOLO: None; a SOLO never hides guides or labels) — 6 layers

| Id | Caption | Default | Window | Extra condition | Labels |
|---|---|---|---|---|---|
| `scaleRings` | Scale and light-time rings | on | -24.7 ... +3.0 | the 2–4 rings whose room radius is 0.08–3 m, viewer outside (§5.9.4) | Guides |
| `horizonGrid` | Horizon compass | on | rung `sky` only (`ViewerOnSite`) | N/E/S/W and 30 deg altitude ticks | Guides |
| `eclipticGrid` | Ecliptic grid | off | -12.0 ... -7.5 | circles at 1–10 AU every 1, then every 5 AU to 40; 12 spokes | Guides |
| `planeGrid` | Galactic plane grid | off | -19.6 ... -15.0 | 2 kpc / 100 pc cells near the Sun; 60 kpc / 5 kpc cells about the GC | Guides |
| `cylGrids` | Cylindrical distance grids | off | stellar -17.2 ... -15.0; galactic -19.6 ... -17.2; Local Group -21.6 ... -19.6 | one ribbon mesh per regime, independent of `planeGrid` | Guides |
| `sgGrid` | Supergalactic grid | off | -24.0 ... -20.0 | the SGX–SGY plane, 100 Mpc cells | Guides |

Sandbox-only visuals (the potential diagram, probe trails) and the stage-specific layers of a
Fictional sector or an exhibit are **not** in this table: each stage brings its own small list,
generated the same way.

### 5.5 Label categories and presentation gains

**5.5.1 Label categories** (`LabelCategory`, §4.2):

| Category | Window (log10 U) | Contents | Default |
|---|---|---|---|
| Bodies | -13.5 ... +3.0 | Sun, planets, dwarfs, moons, comets, generated planets, the focus host's exoplanets | on |
| Spacecraft | -12.6 ... +3.0 | craft inside their window | on |
| Sites | host at 170 px or more | landers, rovers, pads, splashdowns | on |
| SolarLandmarks | -14.8 ... -4.7 | "Main belt", "Kuiper belt", "Termination shock", "Heliopause", the Voyager crossing pins, "Oort cloud (inferred)" | on |
| StarNames | -17.0 ... +3.0 | the 640 index-linked proper names, ranked by apparent brightness from the viewer | on |
| Constellations | -16.6 ... +3.0 | 88 names at the direction of each figure's brightest star, no dot | on (when the layer is on) |
| Clouds | -18.2 ... -14.5 | 9 molecular clouds, the Orion Nebula, the Crab Nebula, the Local Bubble | on |
| MilkyWay | -20.2 ... -16.4 | Sgr A*, the Sun, spiral-arm names, famous globulars | on |
| LocalGroup | -22.0 ... -18.6 | the 13 Local Group landmarks plus the Local Volume giants | on |
| Structures | -24.2 ... -20.6 | clusters, basins, voids, walls; CMB pins | on |
| Guides | with their layer | ring and grid labels | on |
| StageFictional | the sector stage | node brackets | on |
| StageExhibit | the exhibit stage | the exhibit's tappable labels | on |

**5.5.2 Presentation gains.** Every `LayerParam` with `IsPresentationGain` raises a header chip
while it differs from its default, naming the factor:

| Param | Range, default | Chip |
|---|---|---|
| `stars.exposure` | 0.1–3, 1 | `BRIGHTNESS xN` |
| the automatic brightness assist inside the star renderer | automatic | `BRIGHTNESS xN` whenever the effective gain is not 1 |
| `galaxiesLocal.glow`, `galaxies2mrs.glow`, `sdss.glow` | 0.5–30, 1 | `GALAXY GLOW xN` |
| `dust.density` | 0.1–6, 1 (1 = physical opacity) | `DUST xN` |
| `nebulae.density` | 0.1–4, 1 | `NEBULA xN` |
| `dust.threshold` | 0–0.7, the shader default | none (a visibility cut; listed in INFO -> THIS VIEW) |
| `cmb.mode` | Anisotropy / +Dipole / Monopole + dipole + anisotropy | none (the CMB legend always states the mode) |
| `cmb.contrast` | plus/minus 100 ... plus/minus 500 uK, default plus/minus 250 | none (legend) |
| `cmb.palette` | Diverging / Grey / Inverted | none |
| `cmb.frame` | Sun / Local Group / Galactic Centre (dipole only) | legend |

### 5.6 The wrist panel, the inspector and labels

**5.6.1 Surfaces.**

| Surface | Where | Replaces |
|---|---|---|
| **Watch face** | left wrist (hand joint `Wrist` when tracked, else the left controller grip pose); a 19 mm disc with a 21–27 mm ring and the ladder arc | the glance behaviour of `WristDashboard` |
| **Wrist panel** | above the wrist, pinned in space, or the hands-free position | `WristDashboard` (controller-only today) |
| **Inspector card** | beside the selected object, facing the viewer | `SpatialPanel` |
| **Status card** | 0.8 m ahead at first run and whenever unhealthy (§5.10) | the `AnchorBeacon` card, kept and extended |
| **On-universe badges** | under the focus label: `SIZES NOT TO SCALE xN`, `FICTIONAL SECTOR`, the exhibit badge | the desktop-only "SIZES NOT TO SCALE" |
| **Edge arrow** | at the edge of the view, pointing to the focus or the stage when it is out of view for 2 s | the anchor beacon's marker bars |

`ProvenanceHud` (IMGUI) is removed from every platform (C13). On desktop the same wrist panel is
shown pinned in front of the camera and operated by the mouse ray, so the harness captures the
panel the headset shows.

**5.6.2 Opening, placement, pin, recall.**

- **Open / close:** the right index tip within **5 cm** of the watch centre, debounced **800 ms**
  (hands); **X**, or ray plus trigger on the watch (controllers). The watch brightens from 40 %
  to 100 % opacity while the back of the wrist faces the eyes within 55 deg. Opening while
  pinned is the **recall**: the panel returns to the wrist.
- **Anchor when not pinned:** with `hz = normalize((wrist - head).xz)`, target =
  `(wrist.x + 0.10 hz.x, wrist.y + 0.10 + H/2, wrist.z + 0.10 hz.z)` — the bottom edge 10 cm
  above the wrist and 10 cm beyond it. Position follows only when the target is more than
  **6 mm** away, damping `1 - e^(-7 dt)`; yaw follows only when it is off by more than
  **20 deg**, damping `1 - e^(-4 dt)`; pitch and roll are always zero.
- **Hands-free fallback** (no wrist pose, tracking lost, or first run): 0.55 m forward and 0.26 m
  left of the head at eye height minus 0.15 m, with the same deadbands.
- **PIN** (top right of the header) freezes the current pose in the room. Sizes are angular, so a
  pinned panel keeps the metric size it had when pinned; the dmm-to-metres conversion uses the
  wrist viewing distance **0.42 m** when worn and the actual eye distance at the moment of
  pinning otherwise.
- **The panel never closes by itself.**

**5.6.3 Layout.** Built with `CosmoPanel`/`CosmoText`, sized from its contents, and generated
from tables, never hand-listed: `LayerTable.Layers` (LAYERS), `LayerTable.Rungs` and
`Destination`s (GO), `WarpLadder` and missions (TIME), `XrInput.Bindings` (HELP). One
`PanelModel` (Runtime, plain class) turns the tables into rows; the panel component only draws
rows and routes presses.

```
+------------------------------------------------------------------+
| [REAL]  2026-09-11 18:04:22 UTC · real time               [PIN] |  header row 1
| Whole Solar System · 1 m = 88.6 AU   [SIZES x2400] [Level to ...]|  header row 2 (chips)
| [ GO ]  [ LAYERS ]  [ TIME ]  [ TOOLS ]  [ INFO ]               |  tab row
|------------------------------------------------------------------|
|  CAPTION                                                         |
|  [button] [button] [button] [button]                             |  content: 7 rows visible,
|  ...                                                   (scroll)  |  scroll beyond
|------------------------------------------------------------------|
| = Jupiter        [GO]  [FOLLOW]  [INSPECT]  [MEASURE]            |  focus footer
+------------------------------------------------------------------+
```

Metrics in dmm (at 0.42 m, 1 dmm = 0.42 mm): button row height **60** (25 mm), gap **16**
(6.7 mm), minimum button width **150** (63 mm), at most 4 buttons per row, button text
`TypeBody` 24, captions `TypeCaption` 19 in `TextSecondary`, padding `SpacePanelPadding` 22 (all
tokens VERIFIED in `CosmoTheme.cs:98-113`). The widest row sets the width (about 692 dmm, 0.29 m
worn). Content scrolls by the right stick (ray on the panel), by up/down buttons at the right
edge, or by touch-drag.

**5.6.4 Header.** Mode chip (`REAL` / `FICTIONAL SECTOR ?` / `PHENOMENON · <badge>`; in a
non-real stage it also carries "Back to ..."). Clock: UTC date and time to the second, then the
warp label ("real time", "1 week per second", "10,000 years per second backwards", "paused").
PIN. Scale readout. Chips: `SIZES NOT TO SCALE xN`, `BRIGHTNESS xN`, `GALAXY GLOW xN`,
`DUST xN`, `NEBULA xN`, `RELIEF xN`, `SOLO: STARS x` (tap clears), `EPHEMERIS EXTRAPOLATED`,
`STAR MOTION EXTRAPOLATED`, `QUALITY REDUCED`, `Level to <plane>`, `N moons hidden`.

**At most four chips are shown** (R53), in this priority:

1. **honesty** — `SIZES NOT TO SCALE`, `BRIGHTNESS`, `GALAXY GLOW`, `DUST`, `NEBULA`, `RELIEF`;
2. **validity** — `EPHEMERIS EXTRAPOLATED`, `STAR MOTION EXTRAPOLATED`;
3. **state** — `SOLO: <group>`, `N moons hidden`, `QUALITY REDUCED`;
4. **hints** — `Level to <plane>`.

A fifth and further chips collapse into one `+N` chip that opens INFO -> THIS VIEW, which always
lists **every** active chip in full. Chips never shrink below `TypeCaption` 19 dmm; the row wraps
to a second line rather than clipping. The honesty chips are first precisely because a plausible
view (Readable sizes, brightness assist, galaxy glow raised, dust raised, SOLO on, deep time,
quality reduced, a plane hint, hidden moons) produces nine at once, and clipping would drop the
ones the product is named for. The `honesty` assertion (§11.3) compares against the **full** set,
not the four displayed.

**Footer (focus bar).** Glyph and name of the selection, or "Nothing selected: point and pull the
trigger" / "... tap-pinch" by input type; **GO** (travel), **FOLLOW / FREE**, **INSPECT** (opens
INFO -> INSPECTOR), **MEASURE** (starts a measurement from the selection).

**5.6.5 GO tab.**

| Control | Behaviour |
|---|---|
| LADDER: 19 rung buttons in 5 captioned groups (EARTH 5, SOLAR SYSTEM 4, STARS 3, GALAXIES 4, BEYOND 3) | `Travel` to the rung's framing; the current rung highlighted |
| RECENT | the last 8 destinations this session |
| DESTINATIONS: Solar System · Moons · Spacecraft · Surface sites · Nearest stars · Named stars A–Z · Exoplanet systems · Clouds and nebulae · Milky Way · Local Group · Local Volume · Large-scale structure · Cosmic microwave background · Phenomena · Fictional sectors | each opens a sub-list with a "< Back" row; each entry is label, provenance glyph and distance from the viewer ("4.24 ly"); tap is a `Travel`, or a stage switch for the last two |
| Phenomena sub-list | grouped by family (Relativity, Cosmology, Stellar physics); a host chip per exhibit; Previous/Next while an exhibit is open |
| Fictional sectors sub-list | VG-429 Holosystem · Generate a system (seed) · New seed |

**5.6.6 LAYERS tab.**

| Control | Behaviour |
|---|---|
| **SHOW: This rung (default) · All layers** | R48. In *This rung* the list holds only layers whose window contains the current `log10 U` (typically 8–14 of the 46 rows), in §5.4 section order, plus a final row `N layers apply at other scales — [All layers]`. *All layers* shows the full 46 and is **not** persisted: the next open is back to This rung. A layer the user switched **off** stays listed in This rung while its window contains the current scale, so nothing disappears from where it was switched off. Without this the tab is 46 toggles in a 7-row window — about seven screens of scrolling, most rows reading `on (other scales)` — which is the one place the design turns into a pile of features |
| SOLO: SOLAR · STARS · DUST · GALAXIES · BEYOND · ALL | `LayerState.Solo`; guides and labels unaffected |
| One toggle per layer, in the six §5.4 sections | caption, provenance glyph and the state suffix from `LayerState.Status`: `on`, `off`, `on (other scales)`, `loading`, `missing: <file>`, `invalid: <reason>`, `not in this build (licence)`, **`not in this build (<reason from the manifest>)` for `FeatureMissing`** (R47) |
| Layer params | tapping an "on" layer that has params opens its sliders and choices beneath it (accordion): `stars.exposure`; the galaxy glows; `dust.density`, `dust.threshold`; `nebulae.gas`, `nebulae.dust`, `nebulae.density`; `cmb.mode`, `cmb.contrast`, `cmb.palette`, `cmb.frame`, `cmb.backdrop` |
| LABELS: master on/off · one toggle per category (§5.5.1) · FACE TEXT (billboard) / FROZEN · SIZE: Caption / Body | label layer state |
| Fictional stage | Orbit flow · Turntable · Sector dust · Labels · Orbit style: Twin helix / Bundle / Pulse |
| Exhibit stage | the exhibit's 3 layer toggles and its parameter slider (range and unit from `exhibits.json`) |

**5.6.7 TIME tab** — §5.8. **TOOLS tab** — §5.9. **INFO tab**:

| Page | Content |
|---|---|
| INSPECTOR | the full inspector for the selection (§5.6.9) |
| THIS VIEW | the rung's authored text; each visible layer with glyph, count and source; the active presentation departures; "What we do not know here" (per rung, e.g. "no dust map covers the far side of the Galaxy", "the Oort cloud has never been observed") |
| STAGE | the exhibit's Observe / Model & limits, or the sector About |
| SUN VITALS | when the Sun is selected (§5.8.5) |
| CREDITS & LICENCES | every source and required acknowledgement, generated from `manifest.json` `credits[]` |
| STATUS | the `render:` line, data files with status and counts, catalogue validation warnings, version and commit, frame time and governor state |
| HELP | the bindings legend for the active input type, generated from `XrInput.Bindings` |

**5.6.8 Input on the panel.**

**Controllers.** The dominant controller's ray hits the panel within **1.2 m**; hover
highlights; the trigger's rising edge above 0.6 presses; the dominant stick's Y scrolls while the
ray is on the panel. Haptics through `XrInput.Pulse`: hover 0.22 for 10 ms, press 0.7 for 40 ms,
watch toggle 0.8 for 50 ms.

**Hands.** The dominant index tip, in panel-local coordinates:

- *Hover* inside `|x| < W/2 + 30 mm`, `|y| < H/2 + 30 mm`, `-30 mm < z < 120 mm`; a 6 mm Accent
  cursor dot is drawn at the projection.
- *Press* when `z <= 6 mm`, **but only if the tip has been at `z > 20 mm` since the last
  release** (this is the entry-from-behind false press).
- *Release* when `z > 20 mm`. Sliders drag while `z <= 20 mm`. Buttons debounce **300 ms**; press
  flash 200 ms; hit boxes padded 3 mm.
- *Drag-scroll:* a press on a non-button region followed by more than 8 mm of vertical travel
  scrolls the content 1:1.
- No haptics; the press click sound confirms.
- A pinned panel out of reach is operated by the hand's aim ray and a **tap-pinch** (close then
  open within 0.35 s, travel under 30 mm).

**Arbitration.** A hand whose fingertip is in the hover volume, or whose ray hits the panel,
holds the claim `PanelHot(hand)`: its pinch, trigger and grip do not select or grab while the
claim holds. **The other hand is unaffected.** The stick claim generalises
`ClaimNavigationStick` (VERIFIED `XrInput.cs:188`) to `ClaimStick(hand)`.

**5.6.9 Colours, the inspector, and the glyph rule.**

- **Provenance colours** (Measured green `(0.435, 0.851, 0.541)`, Inferred blue
  `(0.498, 0.706, 0.961)`, Procedural amber `(0.949, 0.694, 0.361)`, Fictional pink-violet
  `(0.894, 0.541, 0.847)`, Unknown grey; all VERIFIED `CosmoTheme.cs:71-74`) appear **only** on
  provenance glyphs, badges and data text. The Fictional hue also colours the projector skin's
  chrome, because that skin *is* a provenance signal.
- **Controls** use only neutrals (`Surface`, `SurfaceRaised`, `Stroke`, `Text*`) and `Accent`:
  hover = `SurfaceRaised` + `StrokeActive`; on = Accent fill at 28 % plus an Accent stroke; press
  flash = Accent at 60 % for 200 ms; disabled = `TextMuted`, no stroke. Categories are told apart
  by captions and position, **never by hue**.
- **Two token fixes.** `Warning` is today `(0.949, 0.694, 0.361)`, byte-identical to the
  Procedural colour (VERIFIED `CosmoTheme.cs:59` against `:73`), so a "SIZES NOT TO SCALE"
  warning reads as "Procedural". New `Warning` = **coral (0.973, 0.451, 0.388)**, always with a
  `!` glyph. `Accent` `(0.310, 0.765, 0.969)` sits 14 deg of hue from Inferred; new `Accent` =
  **(0.200, 0.867, 0.929)**, 28 deg from Inferred and 50 deg from Measured, still cyan (no body
  is cyan).
- Honesty chips use `Warning`; actionable hints ("Level to Galactic") use an Accent outline; the
  `FICTIONAL SECTOR` chip uses the Fictional colour; `REAL` and `PHENOMENON` are neutral.
- Guides (rings, grids) are drawn in a muted neutral derived from `TextMuted`, never a uniform
  saturated blue (user, 1.4.0: "blue orbits is just not good").
- **Glyph rule (C4).** The panel, labels and chips may use only characters in the **gated glyph
  set**: printable ASCII, plus `° · × – — … ≤ ≥ ± µ ′ ″` and the Greek letters
  `α β γ δ ε θ λ μ ν π ρ σ φ χ ω Ω Δ Σ Θ Λ Ξ Π Φ Ψ`. Transport and tick controls use ASCII:
  `<<` `||` `>` `>>` `[]` `<` `>` `^` `v` `x` `+`. The play-mode `text` assertion calls
  `Font.HasCharacter` for **every** character in the set and fails if one is missing; `ui` must
  then replace it before the lane can be READY. A richer glyph is added to the set only after the
  assertion has been seen passing with it.

**The inspector.** One `InspectorModel` (Runtime, plain class, rebuilt only when the selection or
a displayed value changes, refreshed at most at 2 Hz) feeds two views: a **card** beside the
object (0.12 rad to its right, at its rendered distance clamped to 0.5–2 m, `TypeTitle` name with
its weakest-class glyph, three NOW lines, and **GO**, **FOLLOW**, **MORE**; plus one line for an
exhibit or mission entry when there is one), and **INFO -> INSPECTOR** with everything.

| Kind | NOW | Facts |
|---|---|---|
| any | distance from the viewer; angular diameter from the viewer; size exaggeration xN when drawn larger than true | name, type, parent/host, the per-field provenance list with sources |
| Solar System body | distance from the Sun (AU, light-minutes); from Earth (AU or km) and one-way light time; speed (heliocentric, or relative to the parent, from the state vector); phase angle Sun-body-Earth | mass, radii, gravity, density, orbit a/e/i/period, day length, atmosphere, surface provenance |
| star | distance (pc, ly); V from Earth and from the viewer; A_V along the line of sight when dust is on | M_V, T_eff, spectral type if known, distance source, worlds: "N exoplanets" (=) or "generated" (*) or "fly within 1 pc to generate" |
| spacecraft | as body, plus mission elapsed time | the known window, placement (table / elements / site), model attribution, attitude rule (~), summary and epitaph, play |
| galaxy | distance and its method (a distance modulus with its reference, or cz/H0 with H0 named) | M_V, r_half, axis ratio and PA (unknown fields flagged), type, host |
| cluster, basin, void | distance, redshift | source, members or volume |
| CMB (tap a direction) | anisotropy dT in uK at that direction; the dipole in uK in the chosen frame; "inpainted" if masked | T0 = 2.72548 K, dipole 3362.08 uK toward (l, b) = (264.021, 48.253), Planck PR3 SMICA-noSZ |
| surface point (ray on a body) | planetocentric latitude and longitude, elevation if a DEM is shipped | host, surface map source |

**5.6.10 Labels.**

- **Size:** `TypeCaption` 19 dmm; `TypeBody` 24 dmm for the selected, followed and travel-target
  objects; never smaller. **In pixels** (R8): at `PixelsPerRadianCentre` = 1140 (§2.0, INFERRED)
  19 dmm is **21.7 px** and 24 dmm is **27.4 px** — not the 27 px and 34 px the panel-average
  1432 implied. 21.7 px is above a legibility floor but not comfortably, which is why `TypeBody`
  is used for the selected, followed and travel-target labels. If the `stereo` assertion reports
  a measured `PixelsPerRadian` **below 1000**, `ui` re-checks both sizes against the floor before
  the lane can be READY. Metres = dmm times the label's **rendered** distance, so labels on
  compressed far groups are sized for where they are drawn and read the same as near ones.
- **Orientation:** upright, facing the eye (room up); FACE TEXT off freezes them.
- **How the text is drawn** (R9). Legacy `TextMesh` is one `MeshRenderer` per object, and
  `m_SupportsDynamicBatching` is 0 (§8.1), so 24 labels plus a multi-row panel would be 50–80
  draws — a third of the whole 150-draw budget. Sharing a material does not merge draws; the SRP
  Batcher removes state-change cost, not draws. So **`CosmoText` builds one mesh per *panel*, not
  one per row**: it lays out all of a panel's lines into a single `Mesh` through
  `Font.GetCharacterInfo` against the shared dynamic-font atlas (it already calls that for
  measurement, VERIFIED `CosmoText.cs:131`), rebuilt on `Font.textureRebuilt` as today.
  `CosmoText.Create` (the `TextMesh` factory at `:222`) survives only for one-off world text and
  is not used by the panel or by `LabelLayer`. **`LabelLayer` (order 100) builds a single mesh for
  all 24 world labels the same way**, exactly as `BodyMarkers` already does for markers (§2.8).
  One shared `CosmoSynth/Text` material for all of it. §7.3's row is therefore **at most 6 draws**
  for all UI text and panels together, gated by the G1c `draws` assertion.
- **Budget and priority:** at most **24** world labels (today 14). Priority: selected > followed > travel target > measure endpoints > landmarks of the
  categories whose window contains the current U > guides > names by apparent brightness from the
  viewer. Greedy screen-space rejection as `BodyLabelLayer` does today; **no crowd shrink**,
  because shrinking pushes text below the legibility floor.
- **Only drawn things are labelled.**
- **Hover names:** while a ray points into a region, up to 5 names within 0.05 rad of the ray
  appear; an unnamed star shows as "star, 312 ly".
- Constellation names sit at the direction of the figure's brightest star, with no dot.
### 5.7 Navigation

**5.7.1 `Travel(target, framing)`.** Today there are three unrelated travel mechanisms (dashboard
teleport plus `FrameBodyAtSize`, desktop `UniverseNavigator.TravelTo`, harness `FlyToStar`). 2.0
has one: the Runtime `TravelController` plays a Core `TravelPlan` and writes
`FloatingOrigin.OriginKm`, `UnitsPerKm` (through `ScaleLimits`), the anchor yaw and the
presentation plane. The panel, the selector, the footer's GO, the A button, double-click, the
harness and mission playback all call it. `UniverseNavigator.TravelTo` is deleted.

```csharp
// Core/Nav/TravelPlan.cs  (engine-free)
// R18: `readonly record struct` is C# 10 and cannot be polyfilled (LangVersion is pinned to 9.0).
// A plain readonly struct with an explicit constructor, in this field order.
public readonly struct ViewState
{
    public readonly Double3 FocusKm;
    public readonly double Log10U, YawRad;
    public readonly Double3 PlaneUpIcrf;
    public ViewState(Double3 focusKm, double log10U, double yawRad, Double3 planeUpIcrf);
}
public sealed class TravelPlan
{
    public static TravelPlan Create(ViewState from, ViewState to, double fitSpanMetres = 0.6);
    public double DurationSeconds { get; }
    public ViewState At(double seconds, Double3 currentTargetKm);   // the target is re-read each frame
    public bool UsesDip { get; }
}
```

Profile, with `u = t / T` and `s(x) = x^3 (x (6x - 15) + 10)` (smootherstep):

1. **Orientation first.** `er = s(min(1, u / 0.35))`; yaw = angle-lerp(yaw0, yaw1, er), where
   yaw1 turns the target straight ahead of the user; plane up = slerp(up0, up1, er). Both are
   complete at u = 0.35.
2. **Direct profile** when start and target already fit the room together at the smaller scale,
   i.e. when `Lfit = log10(fitSpan / D) >= min(l0, l1)` with `D = |f1 - f0|` km:
   `e = s(u)`, `log10U = lerp(l0, l1, e)`, `focus = lerp(f0, f1, e)`. Interpolating the focus in
   **absolute km** (not the group position) keeps the target on the line from the anchor for the
   whole flight.
3. **Dip profile** otherwise (Earth to M31, say): the scale first falls to `Lfit`, where both ends
   are within plus or minus 0.3 m of the anchor, and the focus moves only while zoomed out:
   `log10U = lerp(l0, Lfit, s(2u))` for `u <= 0.5` and `lerp(Lfit, l1, s(2u - 1))` after;
   `focus = lerp(f0, f1, s(clamp((u - 0.25) / 0.5, 0, 1)))`. Without it, a straight log-lerp
   leaves both ends millions of room metres away mid-flight and the user sees nothing (Earth to
   M31 at the midpoint scale puts M31 about 1.2e7 m away, INFERRED arithmetic). This is a cheap
   version of van Wijk and Nuij's optimal zoom-pan; the full method is not needed.
4. **Duration** `T = min(2.6 + 0.35 * decades, 9.0)` s, with decades `= |l1 - l0|` (direct) or
   `|l0 - Lfit| + |l1 - Lfit|` (dip). Examples (INFERRED arithmetic): `clouds` to `system` 4.9 s
   direct; `planet-hand` to M31 8.3 s dip; `sky` to `cmb` 9 s (the cap; 12 s uncapped).
5. **Cancel** on any grab, stick zoom, stick flight, rung tap or new GO; the view stays where it
   is, and the manipulator's stale target is cleared.
6. **Arrival:** the crosshair (8 strokes along the cube diagonals from 0.55R to R, R = 7.5 cm at
   the target, Accent) appears **at arrival** and fades after 6 s over 1.2 s. Follow switches on
   for moving targets. During the flight a caption under the panel header names each rung passed
   ("Inner planets · Whole Solar System · Heliosphere ..."): the powers-of-ten moment, for the
   cost of one text line.

**Tests (`T/NavTests.cs`):** endpoints exact; zero velocity of `log10U` and focus at u = 0 and 1
(finite difference); yaw and plane complete at u = 0.35; direct profile keeps the target
direction from the anchor constant to 1e-9 rad; dip profile puts both ends within plus or minus
`fitSpan/2` of the anchor at u = 0.5, with `log10U` monotonic in each half; `T <= 9` s; a plan
evaluated past T returns `to`.

**5.7.2 Follow and Free.**

- **Follow** (co-moving, translation only): every frame
  `OriginKm = target.AbsolutePositionKm(now) + offsetKm`. The offset is re-captured when a grab
  or a flight ends, so the user can move around a followed planet. Without it, at 1 day per
  second Earth leaves a tabletop hologram in about a second (30 km/s x 86,400 s = 2.6e6 km per
  real second, VERIFIED arithmetic).
- On by default after a GO to a body, spacecraft or star system, and after rungs 1–9; off after
  rungs 10–18 and after a GO to fixed targets (clusters, galaxies, structures).
- The footer shows `FOLLOW` or `FOLLOWING Jupiter` with `FREE`; L stick click and `F` toggle it.
- If the followed object leaves its known window (a spacecraft) or is released (a generated
  system), Follow ends with a one-line notice ("Voyager 1 is outside its known window: now
  free").
- **Free:** the viewer stays at a fixed ICRF point (today's behaviour).
- Rung `sky` is not Follow: the observer stands on the rotating Earth (a `SurfaceFixedEphemeris`
  point), so the sky turns.

**5.7.3 Destinations and their framing.** `Destination` is §4.2. Framing on arrival is
`U = RoomMetres / (2 * FramingRadiusKm)`, clamped by `ScaleLimits`:

| Kind | Framing radius | Room |
|---|---|---|
| Sun, planet, moon, dwarf | display radius (the sizes preset applied) | 0.55 m |
| small body, comet nucleus | true radius | 0.55 m |
| spacecraft | half the model length | 0.4 m |
| surface site | the host at `planet-room` framing, turned so the site faces the user, with its pin | 2.5 m |
| star | `max(2.2 x outermost known or generated orbit, 40 AU)` | 0.7 m |
| globular cluster | 5 x r_half | 0.7 m |
| cloud or nebula | 50 pc (Orion complex), 300 pc (Local Bubble), 2 pc (Crab) | 0.7 m |
| galaxy | 3 x r_half (or a26/2 from UNGC) | 0.7 m |
| cluster, basin, void | 2.2 x its radius | 1.0 m |
| Sgr A* | the `milkyway` rung (its inspector offers the black-hole exhibit) | rung 12 |
| CMB | rung `cmb` | rung 18 |

Sources of destinations: the 19 rungs; every catalogue body **by id** (fixing the hard-coded star
index that sent "GJ 876" to GJ 1002); the six atlas targets (Solar System, Orion, Sgr A*,
omega Cen, LMC, M31); the nine nearest stars (Proxima, alpha Cen A, Barnard's Star, Wolf 359,
Sirius, epsilon Eridani, tau Ceti, Teegarden's Star, GJ 876); the landmark and structure files;
and **any picked object** — a ray query over a layer's point array returns the nearest point to
the ray, which becomes a `Destination` on the fly.

**5.7.4 Bindings.** The complete table is **Appendix E**. `XrInput.Bindings` changes from today's
`(string Control, string Action)[]` (VERIFIED `XrInput.cs:58`, 9 rows) to the array below, and
takes every row of Appendix E, so the legend (INFO -> HELP, the status card, the first-run hint)
is generated for whichever input is active.

**The row type, verbatim** (R26; `nav` creates it in 1b wave A, but P0.1's `binding_gate.py` and
the P0.7 slot both need the syntax now, so it is fixed here and not left to a lane). It is a
`struct`, not a `record`: see the C# level note in §4.2.

```csharp
// Runtime/XR/XrInput.cs — owned by `nav` from 1b; slots for `ui`, `fiction`, `audio` (§9.4).
public enum BindDevice  : byte { Controller, Hand, Keyboard }
public enum BindContext : byte { Any, Default, PanelRay, NotPanelRay, PanelTouch, Measure,
                                 Sandbox, HasSelection, ExhibitStage, RealOrFictionalStage,
                                 TrayHot }
public readonly struct Binding
{
    public readonly string Control, Action;
    public readonly BindDevice Device;
    public readonly BindContext Context;
    public Binding(string control, BindDevice device, BindContext context, string action)
        { Control = control; Device = device; Context = context; Action = action; }
}

public static readonly Binding[] Bindings =
{
    new Binding("L stick", BindDevice.Controller, BindContext.RealOrFictionalStage, "fly"),
    // lane:ui BEGIN
    // lane:ui END
    // lane:fiction BEGIN
    // lane:fiction END
    // lane:audio BEGIN
    // lane:audio END
};
```

**`Context` is an enum, not prose** (R37). `binding_gate.py` (G0g) keys uniqueness on the
(Control, Device, Context) triple; with free text, "any" and "default" and "ray on the panel" and
"panel" are four different strings for two states and the gate would pass a real collision. It
parses **exactly one**
`new Binding("<control>", BindDevice.<device>, BindContext.<context>, "<action>"),` per line, and
fails on a line it cannot parse. Appendix E's Context column uses these enum names.

Until P0.7 replaces the 2-tuple array, **G0g is listed in `tools/gates/known-red.txt` with a
removal date of P0.7**: it cannot pass on today's rows, and a gate that cannot pass is as broken
as one that cannot fail (§11.4). Right-handed by default; **Handedness: Left** mirrors every L/R pair and puts the watch
on the right wrist.

**5.7.5 Comfort rules.**

1. **In passthrough the room never moves**; only the stage moves. The room is the user's balance
   reference, which is why passthrough is the default.
2. **In Space mode**: travel yaw at most 60 deg/s; plane changes over 10 deg blink rather than
   rotate; the **comfort floor** (a faint 1.5 m ring at the user's feet) is on by default.
3. No roll or pitch of the stage except a plane change and the opt-in **Free tilt** (a one-hand
   grab carrying full 6-DoF rotation; default off).
4. Snap turn by default; smooth turn opt-in.
5. Flight and zoom accelerate over 0.25 s and stop over 0.15 s; nothing moves the view without a
   user action except Follow, mission playback and a travel the user started.
6. No full-view flashes: stage switches and passthrough changes fade over at least 0.3 s;
   luminance never changes by more than 30 % of the view within 100 ms.
7. Content nearer than 0.25 m to the eyes fades out; rungs frame their centre at least 0.45 m
   away.
8. No head-locked content: the panel, cards and badges are world- or wrist-locked.
9. The frame rate never drops below 72 Hz by design: the quality governor sheds dust steps, then
   star vertices, then galaxy sprites, and says so with the `QUALITY REDUCED` chip.

**5.7.6 Harness actions.** The view grammar (§10.6) already covers `default`, `wide`, `mode:vr`,
`rung:<id>`, `dest:<id>`, `layer:<id>` (solo), `star:<n>`, `ui:<panel>`. This section adds, in the
same grammar: `stage:real`, `stage:fictional:<id>`, `stage:phenomenon:<id>`, `time:<ISO-8601>`,
`warp:<signed index>`, `sizes:true|readable|x10|x100|x1000`, `room:<0..1>`, `solo:<group>`,
`plane:<name>`, `measure:<idA>,<idB>`, `observed:<vantage>:<limit>`, `select:<id>`. Each writes
its result into the report (the measured distance, the active chips, the layers with
`Weight > 0`), so headless gates can assert on them.

### 5.8 Time

**5.8.1 The signed warp ladder.** `WarpLadder` moves out of `SimulationClock` into
`Core/Time/WarpLadder.cs` (engine-free, testable). Today's forward ladder (VERIFIED
`SimulationClock.cs`, `WarpRates` = {0, 1, 10, 60, 600, 3600, 21600, 86400, 604800, 2592000,
1 yr, 10 yr, 100 yr, 1e4 yr, 1e6 yr}) is kept and mirrored. **There is no zero step**, because
pause is a separate state that keeps the warp.

| Index | Rate (s/s) | Label |
|---|---|---|
| -14 ... -1 | -3.15576e13 ... -1 | "1,000,000 years per second backwards" ... "real time backwards" |
| +1 | 1 | "real time" (default) |
| +2 | 10 | "10 seconds per second" |
| +3 | 60 | "1 minute per second" |
| +4 | 600 | "10 minutes per second" |
| +5 | 3600 | "1 hour per second" |
| +6 | 21600 | "6 hours per second" |
| +7 | 86400 | "1 day per second" |
| +8 | 604800 | "1 week per second" |
| +9 | 2592000 | "30 days per second" |
| +10 | 31557600 | "1 year per second" |
| +11 | 3.15576e8 | "10 years per second" |
| +12 | 3.15576e9 | "100 years per second" |
| +13 | 3.15576e11 | "10,000 years per second" |
| +14 | 3.15576e13 | "1,000,000 years per second" |

28 steps. `Y` / `<<` / `,` step towards -14; `B` / `>>` / `.` towards +14; from +1 the next step
down is -1. **Now** sets the epoch to the device's UTC and the index to +1. Tests: strictly
increasing, symmetric, contains +1 and -1, has no zero, every label round-trips.

**5.8.2 Dates and the limits of time.**

- **Date jump:** YEAR, MONTH, DAY, HOUR steppers apply immediately; KEY DATES jump to a listed
  instant. The clock is TDB seconds past J2000 in double; the display is UTC with the leap-second
  table.
- **Calendar range:** dates display exactly from year -4700 to +9999 (`CalendarUtil.MinimumSupportedYear`
  = -4700, VERIFIED). Outside, the header shows the offset from J2000 in years with k/M/G
  prefixes ("-1.23 Myr"), which also removes the `int` year overflow.
- **What each clock-dependent thing does outside its data** — each raises a header chip; nothing
  is silently extrapolated:

| Content | Valid span | Outside it |
|---|---|---|
| Planet positions | the Horizons-fitted conics near their epoch, then the Standish secular elements (Table 1 1800–2050; Tables 2a/2b 3000 BC – AD 3000) | chip `EPHEMERIS EXTRAPOLATED`; beyond plus or minus 10 kyr from J2000 the chip reads "orbits not valid this far from today" |
| The Moon | the Meeus ch. 47 lunar theory's span | the same chip |
| Spacecraft | each craft's `KnownFrom`–`KnownUntil` | not drawn (today's rule) |
| Stars | linear motion from J2000 using `StarCatalog.PositionAt`; census stars and velocity-suspect rows stay fixed | chip `STAR MOTION EXTRAPOLATED` beyond plus or minus 100,000 years, where real Galactic orbits curve away from straight lines (INFERRED threshold) |
| Sun activity | the monthly record 1749-01 to the last observed month, then the SWPC prediction | before 1749 or after the prediction: "no sunspot record for this date", a neutral Sun, no invented activity |
| Galaxies, CMB | static; no cosmological clock | nothing to show; INFO says so |

**5.8.3 Mission playback.** MISSIONS (TIME tab) lists every spacecraft with a trajectory: the 24
already shipped, plus three new Horizons trajectories — New Horizons (-98), BepiColombo (-121),
Artemis I (-1023), all reachable from triton (VERIFIED) — and one authored table.

- **Apollo 11** is authored as a `TrajectoryTable` labelled "Illustrative: Fictional geometry
  anchored to Measured events", because Horizons has only the S-IVB (-399110): launch from LC-39A
  (28.608 N, 80.604 W) on the rotating Earth at the real lift-off time 13:32 UTC on 1969-07-16, a
  190 km parking orbit, a translunar path to the Moon's position from the lunar theory, three
  points on a 1,849 km lunar orbit, landing at the existing Apollo 11 site (0.674 N, 23.473 E) on
  1969-07-20, splashdown at 13.32 N, 169.15 W on 1969-07-24.
- **Venera 7** has no trajectory source ("No matches" in Horizons, VERIFIED): its playback is
  **dropped** and replaced by a dated surface site on Venus (1970-12-15; coordinates to be cited,
  INFERRED about 5 S, 351 E).
- **Cassini's cruise** (Venus, Venus, Earth, Jupiter, 1997–2004) plays only if a heliocentric
  Horizons table is added; otherwise Cassini plays its Saturn tour and the row says so.

Play sets the epoch to one day (heliocentric) or one hour (geocentric) before the first key
event, the warp to the mission's default, the rung, Follow on the craft, and its trail on. For a
geocentric mission it also switches SIZES to True and shows an "undo" chip. Previous/next event
jump with the same lead. Stop returns the warp to real time and leaves the epoch where it is;
**Now** returns to today.

| Mission | Key events | Default warp | Rung |
|---|---|---|---|
| Voyager 1 | 1977-09-05 launch; 1979-03-05 Jupiter; 1980-11-12 Saturn; 2004-12-16 termination shock (INFERRED); 2012-08-25 heliopause | 30 days/s | `system`, then `heliosphere` |
| Voyager 2 | 1977-08-20; 1979-07-09 J; 1981-08-25 S; 1986-01-24 U; 1989-08-25 N; 2007-08-30 TS (INFERRED); 2018-11-05 HP | 30 days/s | `system`, then `heliosphere` |
| Pioneer 10 | 1972-03-03 launch (VERIFIED Horizons); 1973-12-03 Jupiter | 30 days/s | `system` |
| New Horizons | 2006-01-19; 2007-02-28 Jupiter; 2015-07-14 Pluto; 2019-01-01 Arrokoth (INFERRED) | 30 days/s | `system` |
| Cassini | 1997-10-15; V 1998-04-26; V 1999-06-24; E 1999-08-18; J 2000-12-30; S 2004-07-01; end 2017-09-15 | 1 week/s | `inner`, then `planet-moons` at Saturn |
| BepiColombo | 2018-10-20; E 2020-04-10; V 2020-10-15; V 2021-08-10; Me 2021-10-01, 2022-06-23, 2023-06-19, 2024-09-04, 2024-12-01, 2025-01-08; MOI 2026-11-15 | 1 week/s | `inner` |
| Artemis I | 2022-11-16 06:47:44 launch (VERIFIED Horizons); lunar flyby and return | 6 hours/s | `planet-moons` (Earth) |
| Apollo 11 | as above | 6 hours/s | `planet-moons` (Earth) |
| the other shipped craft (JWST, Hubble, ISS, Juno, MRO, MAVEN, LRO, Parker, SOHO, Dawn ...) | their window start and end | tables 1 week/s, orbits 1 hour/s | `planet-moons` or `inner` |

Voyager and Pioneer need no radial extrapolation: the shipped tables already run to 2034–2035
(VERIFIED).

**KEY DATES** in the TIME tab: J2000.0 · 1859-09-01 Carrington flare (INFERRED date) ·
1969-07-20 Apollo 11 landing · 1977-09-05 Voyager 1 launch · 1989-08-25 Voyager 2 at Neptune ·
2012-08-25 Voyager 1 heliopause · 2015-07-14 New Horizons at Pluto · 2018-11-05 Voyager 2
heliopause · 2026-11-15 BepiColombo Mercury orbit insertion; then every mission key event above.

**Other TIME-tab controls.** `<<` slower · pause/play · `>>` faster · **Now**. RATE:
-1 yr · -30 d · -1 d · Real · 1 h · 1 d · 30 d · 1 yr · 10 yr. DEEP TIME: -1 Myr · -10 kyr ·
-100 yr · +100 yr · +10 kyr · +1 Myr. DATE: YEAR/MONTH/DAY/HOUR steppers (long press repeats at
5 Hz; YEAR switches to x10 steps after 2 s). Fictional stage: sector pause/play and speed
x0.25/x0.5/x1/x2/x4. Exhibit stage: the timeline slider (scrubbing pauses), Play/Pause, Restart,
speed x0.25/x0.5/x1/x2.

**5.8.4 Spacecraft attitude.** Each mission row carries an attitude rule: `earth` (high-gain
antenna towards Earth: the Voyagers, Pioneer 10, New Horizons, Cassini in cruise), `sun` (JWST,
Parker, SOHO), `lvlh` (ISS, Hubble) or `inertial`, with the model axis it drives. Provenance
Inferred ("typical attitude").

**5.8.5 Sun vitals at the simulation date.** The Sun's inspector gains a **VITALS** page:

- **Activity at the simulation date:** the monthly sunspot number and F10.7 from 1749 (Measured),
  the predicted cycle after the last observation (Inferred), shown as the month's value and a
  30-year sparkline with a cursor. It also drives the Sun's spots, faculae and corona extent
  (`sunActivity`, Procedural over Measured). It works for 1859 and 1989 as well as for today.
- **"Space weather: snapshot as of &lt;UTC&gt; (not live)"**, baked at build time: the GOES X-ray
  class and flux with the 6-hour curve, the latest flare, Kp (24 three-hour bars) and the
  G-scale, the L1 solar wind speed, density, temperature, Bt and Bz with the 24-hour speed curve,
  protons at or above 10 MeV and the S-scale, F10.7, numbered regions, summed spots, and the
  latest alert headline. Thresholds verbatim from the source page. The Bz alert levels (at or
  below -8 nT, at or below -4 nT) show as `!!` and `!` in the Warning colour, because amber is
  the Procedural colour.
- **"The Sun as seen from Earth" images** are **deferred** (user decision D5); until they ship
  the VITALS row reads "Solar images: not in this build".
- **Parker spiral** (`parker`): streamlines from the Sun's sidereal rate and the snapshot's
  measured L1 speed (Procedural geometry over a Measured speed).
- The WSA-Enlil panel is **dropped**: it is an annotated forecast plot, and drawn in 3D it would
  be fiction. The Parker spiral is the honest equivalent.
- Credit line on the card: "Recorded &lt;UTC&gt; from NOAA SWPC, NASA SDO and ESA/NASA SOHO. Not
  live."

### 5.9 Tools

**5.9.1 Measure.** TOOLS -> MEASURE on (or the footer's MEASURE, or `M`). While on, the dominant
trigger or a tap-pinch picks points instead of selecting; the other hand still grabs. **Any two
picks**: bodies, spacecraft, sites, stars from the 3D layer, clusters, galaxies, landmarks,
structures, or **From me** (the viewer). Distances come from absolute double km only. A ribbon of
screen-constant width in `TextPrimary` with Accent end dots, labelled `4.24 ly · 1.30 pc` with a
sub-line `Sun -> Proxima Centauri · light 4.24 years`. Units: Auto (km below 1e6 km; AU with
light-minutes or light-hours below 0.1 ly; ly and pc below 1 kpc; kpc or Mpc with Mly above), or
forced km / AU / ly / pc. A third pick starts a new measurement; the last three stay until
**Clear**. Measurements between moving objects update live. `WristDashboard.FormatDistance` and
`ProvenanceHud`'s formatter become one `Core/Presentation/Format.cs`, with tests at every unit
boundary.

**5.9.2 Observed sky.** TOOLS -> OBSERVED SKY switches `stars` (and `cepheids`, `globulars`) from
**model** mode (flux-true point sizes from luminosity and distance) to **observed** mode: each
star is drawn by its apparent magnitude from a **vantage**,
`m = M_V + 5 log10(D / 10 pc) + A_V(vantage -> star)`, where **`M_V` is de-reddened**
(`M_V = vmag - 5 log10(d_Sun/10 pc) - av`, with `av` the shipped Sun-to-star extinction column,
C.1). With vantage = Sun and extinction **on** this returns the catalogue `vmag` **exactly**, so
extinction is applied once and not twice (R41). With extinction **off** from the Sun it returns
the de-reddened magnitude, which is brighter — honestly labelled "the sky without dust" in the
readout, not "the sky". Visible when brighter than **LIMIT MAG** (3–14, default 6.5),
with size and brightness compressed by the eye response `b^0.23`. The maths lives in
`Core/Sky/Photometry.cs` with tests: Sirius -1.44 from the Sun; **for 200 rows spanning
`av` 0–3, `|m(Sun, extinction on) - vmag| < 0.02 mag`** (the known-answer test that catches the
double count — Sirius alone cannot, because its `av` is about 0 and the correct and the
double-counted formula agree there, R41); and the count brighter than 6.5 from the Sun equals the
catalogue's 8,920 **with extinction on**, since that is the observed sky. **Vantage:** Sun (default) · Me (the viewer's absolute
position) · Selected (any star or body) · Site (a place on Earth; rung `sky` uses it).
**Extinction** on/off (needs `dust`): adds `A_V` along vantage-to-star from the dust cube.
**SITE:** a list of 12 cities (Helsinki default 60.17 N, 24.94 E; plus Sydney, Santiago, Cape
Town, Mumbai, Tokyo, Honolulu, Los Angeles, New York, Sao Paulo, Nairobi, the South Pole) and
LAT/LON steppers (1 deg, long press 10 deg), as Measured constants with a source in
`Core/Sky/ObservingSites.cs` (§6.9.2 G10). The readout says what the data can show: "8,920 stars
brighter than 6.5 from the Sun" and, when the limit exceeds the catalogue's completeness,
"fainter than about 9 the catalogue is incomplete".

**5.9.3 Trajectory sandbox.** **Owner: lane `fiction`, 1b second half** (R38). Both sandboxes and
`StageDirector` were promised in this document and claimed by no lane in `tools/lanes.txt`, which
under §9.3 rule 1 made them integrator-owned — and the integrator's explicit list named neither.
Twenty-three Appendix B rows (NB-01..14, SLG-01..09) and the stage machinery that `ui`,
`fiction` and `nav` all depend on hung on that gap. §9.3, §9.5 and §9.2 are amended accordingly.

**Real sandbox** (TOOLS -> SANDBOX -> Launch from selected; default Earth). `Core/Orbits/RestrictedNBody.cs`:
a massless probe in the field of the catalogue bodies whose positions come from the real
ephemerides, adaptive Dormand-Prince 5(4) in double, with events for surface impact (the body's
ellipsoid) and sphere-of-influence changes. **The predicted path uses the same integrator and the
same steps as the flight**, so what is drawn is what is flown. Aiming: pinch (or trigger) near
the launch body, pull back, release — the slingshot gesture with its 22 mm / 45 mm hysteresis and
release buffer (re-use the last non-zero impulse if the release frame reads zero). The pull
length maps logarithmically to a delta-v over 0.1–45 km/s. The panel repeats the controls: a
delta-v slider with x0.1 and x10 buttons, direction sliders in the prograde / normal / radial
basis ("0 deg = prograde, 180 deg = retrograde"), and LAUNCH. **Live readout** of the osculating
orbit from `OrbitalElements.FromState`: a, e, i, periapsis, apoapsis, period, and ESCAPES /
BOUND, plus the launch body's escape speed and the solar escape speed at the launch distance.
Time is the global clock: warp speeds the probe up; reverse warp pauses the sandbox with a notice
("the sandbox integrates forward only"). Up to 3 probes; trails as ring buffers with "10 s fade"
or "Infinite"; Clear probes. **Potential diagram** (off by default) draws the depression from the
real potential `Phi = -sum GM_i / |r - r_i|`, labelled "DIAGRAM". Tests: energy conservation in a
two-body case; a radial launch from Earth at 11.19 km/s escapes and at 11.0 does not; a Hohmann
Earth-Mars transfer time within the vis-viva bound; forward-then-back integration returns within
tolerance.

**Arcade slingshot** (TOOLS -> SANDBOX -> Arcade; a **Fictional** stage): the Orbital Slingshot
game on a tabletop in the projector skin — five abstract planets with GM proportional to radius
(so every planet has the same surface escape speed), the pinch-and-pull impulse
`min(|pull| * 4.8, 1.8)` m/s, surface lock and relaunch (push out 0.12 m, 0.5 s collision grace),
a 400-point trail, reset beyond 10 m. Fixes: a deterministic seed shown on the plinth with "New
layout", and one fixed-step integrator for both prediction and flight, frame-rate independent. No
score or goals were in the source; none are added.

**5.9.4 Scale and light-time rings.** Sun-centred circles in the active presentation plane,
labelled at 45 deg. The renderer shows the 2–4 rings whose room radius is 0.08–3 m; a ring that
surrounds the viewer is drawn only if its plane is at least 0.2 m below the eyes.

**Two different radii, each named correctly** (R44). 13.87 Gpc is the comoving distance to
**last scattering**, which is where the CMB shell is drawn (both forms). The **particle horizon**
at 14.26 Gpc — radius 4.40e23 km, diameter 8.8e23 km — is "the edge of the observable universe",
and it is what rung 18 frames and what `ScaleLimits.MinUnitsPerKm = 2e-25` shrinks to 17.6 cm. The
earlier single row labelled 13.87 Gpc "edge of the observable universe" drew a ring *inside* the
CMB shell under that name, which teaches the wrong thing.

| Ring | Radius | Source |
|---|---|---|
| 1 light-second | 299,792 km | definition |
| 1 light-minute | 1.799e7 km | definition |
| 1 AU · 8.3 light-minutes | 1.496e8 km | IAU |
| 1 light-hour | 1.079e9 km (7.2 AU) | definition |
| 30 AU · Neptune | 4.49e9 km | catalogue |
| about 120 AU · heliopause (Voyager 1: 121.6 AU; Voyager 2: 119 AU) | 1.8e10 km | VERIFIED summaries |
| 1 light-day | 2.59e10 km (173 AU) | definition |
| about 2,000 AU · inner Oort cloud (inferred) | 3.0e11 km | INFERRED |
| 1 light-month | 7.88e11 km (5,270 AU) | definition |
| 1 light-year | 9.46e12 km | definition |
| 1 parsec · 3.26 ly | 3.09e13 km | definition |
| 4.24 ly · Proxima Centauri | 4.01e13 km | catalogue |
| 10 ly, 100 ly, 1,000 ly | | definition |
| 8.15 kpc · distance to the Galactic Centre | | Reid 2019 |
| about 50 kly · Milky Way disc radius | | INFERRED; cite before shipping |
| 2.5 Mly · M31 | | UNGC 0.77 Mpc |
| 10 Mly, 100 Mly, 1 Gly | | definition |
| 45.2 Gly · CMB last-scattering surface (comoving) | 13.87 Gpc | Planck 2018, `r*/theta*` |
| 46.5 Gly · particle horizon, the edge of the observable universe | 14.26 Gpc (4.40e23 km) | Planck 2018 LambdaCDM, integrated (INFERRED until the integration is recorded in `CmbModel`) |

**5.9.5 Room: passthrough and space.** One XR session with passthrough. TOOLS -> ROOM has a
**room visibility** slider (100 % = passthrough, the default; 0 % = Space) and the two presets
**Passthrough** and **Space**; changes ease over 0.3 s. Space draws an opaque backdrop that writes
coverage (§2.2 queue 1000 with `_Immersion = 1`), shows `mwBand`, the CMB backdrop at the BEYOND
rungs, the comfort floor ring, and a neutral translucent hand mesh so the user still sees their
hands. **CMB backdrop in passthrough** (off by default) draws the CMB sky behind everything even
with the room visible. Intermediate values dim the room behind the content. If passthrough is
unavailable the app starts in Space and says so.

**5.9.6 Sizes and the other honesty presets.**

| Preset | Radii | Moon orbits | Sun |
|---|---|---|---|
| **True** | true | true | true |
| **Readable** (default, today's) | `R_ref (R / R_ref)^0.55`, `R_ref` = Earth | true | per the law |
| **x10 / x100 / x1000** | planets, moons and dwarfs times N | co-scaled about the primary by one factor per system, `min(N, 0.5 r_H / a_outermost)`, with the Hill radius `r_H = a (m / 3 M_sun)^(1/3)`, so the moons keep their order | capped at x25 |

A moon whose drawn orbit falls inside its drawn primary is hidden and counted: chip
`N moons hidden`. Worked example (INFERRED arithmetic): Mars's Hill radius is about 1.08e6 km, so
its system factor caps at x23 (set by Deimos, a = 23,460 km); at x100 Phobos is drawn at
216,600 km, inside a 339,000 km Mars, and is hidden, while Deimos at 542,000 km is not. x1000 is
kept because the planets themselves do not overlap there (Earth becomes 0.043 AU across against
0.28 AU to Venus at closest); only moons suffer, and they are counted.

**RELIEF x1 / x10 / x50**: elevation exaggeration on bodies with a shipped DEM; disabled with
"no elevation data for &lt;body&gt;" elsewhere (DEMs deferred, §6.9.2 G8); chip `RELIEF xN`.

**The badge and the chip are two different rules** (R43). The old single rule fired on the
**focus** only, so the supported state "Readable preset, nothing selected" (the footer reads
"Nothing selected") showed Phobos at x15 and every small moon inflated **with no honesty marker at
all** — and it covered neither the 2.5–3 px star floor nor the automatic far-assist, which inflate
size and brightness across the whole view.

- **Header chip:** on whenever the active SIZES preset is not True **or any drawn body's
  `SizeExaggeration` exceeds 1.05**, reading
  `SIZES NOT TO SCALE <preset> · up to x<max over drawn bodies>` (two significant figures).
- **On-universe badge:** `SIZES NOT TO SCALE x<N>` in `Warning` under the **focus's** label in the
  headset, naming the focus's own factor, whenever that factor exceeds 1.05 (including in True
  mode, when the 2.5 px floor inflates a distant body). Today this exists only in the desktop
  IMGUI HUD.
- **`BRIGHTNESS xN` follows the same shape:** the chip is on whenever the *effective* star gain —
  `stars.exposure` x the automatic assist x the mean inflation the pixel floor applies — differs
  from 1 by more than 5 %, not only when the user moved a slider.

Gated by the G1c `honesty` assertion (§11.3): the set of active chips must equal the set computed
from state. Until this revision it was the one promise the product is named for with no mechanical
check, in a document that elsewhere insists "Never trust a gate that has not failed once".

Core: `Core/Presentation/SizePresets.cs`, tests — Mars at x100 hides Phobos and not Deimos; Earth
at x1000 hides the Moon; the Sun never exceeds x25.

**5.9.7 Specimen tray.** A room-anchored shelf, not part of the universe: **MODEL COPY: not the
simulation**. It is the **one** exception to §5.3's "real and invented content are never in the
room at the same time", and §5.3 states its terms: outside the Universe host, ignored by the
manipulator and `ScaleControl`, never a `Destination`, a persistent plate on the rim, and a
`TrayHot(hand)` claim while a specimen is held (R45). Twelve specimens (Sun, Mercury, Venus, Earth, Moon, Mars, Jupiter, Saturn, Uranus,
Neptune, Pluto, Ceres), each with its real or procedural surface (with glyph) and its current IAU
rotation. Size modes: **Equal** (3.5 cm radius each), **Readable** (the 0.55 law with Earth at
3.5 cm), **True** (Earth at 3.5 cm; the Sun would be 3.8 m in radius, so its slot says "Sun:
3.8 m radius at this scale, not shown"). Pinch or grip a specimen and it grows to 0.22 m radius
over 0.25 s and can be turned in the hand; released within 0.2 m of its slot it docks and
shrinks; released elsewhere it floats for 10 s, then returns. Grabbing the shelf's handle carries
the whole tray.

**5.9.8 Sound. Not in 2.0.0** (R52). The `audio` lane is Phase 3, after 2.0.0 ships (§9.2), so in
2.0.0 **TOOLS -> SOUND is a single row reading "Sound: not in this build"**, the UI click sounds
are absent, and D10 ("ambient on by default") applies from the first build that has audio. The
rest of this section is the design that lane builds; every "default on" below is a default in that
build, not a 2.0.0 promise.

DSP in `Core/Audio` (Kellet pink noise, oscillators, LFO, soft limiter; tests:
pink slope -3 dB/octave within 0.5 dB over a 10 s sample, the LFO never takes the cutoff below
40 Hz, a chirp rises monotonically), one Runtime `CosmoAudio` filling `OnAudioFilterRead`,
followed by Unity's low-pass, echo and reverb filter components (their Android behaviour is
INFERRED; check on device). At most 8 voices.

- **Ambient bed** (default on, about -24 dBFS): the synthesizer's "edge of the universe" preset —
  65 Hz sine plus a 130 Hz triangle detuned +6 cents (the source never applied the detune), plus
  pink noise 0.4, low-pass 220 Hz Q 6 swept by a 0.04 Hz LFO **kept within 60–600 Hz** (the
  source's sweep sat at 0 Hz for about 30 % of each cycle), delay 0.65 s feedback 0.75 mix 0.45,
  reverb about 6.5 s mix 0.65. The filter centre rises one octave per six rungs outwards. Legend:
  "ambient, not data".
- **Data voices** (default on), each with the provenance of what drives it: the selected
  **pulsar** as a click train at its spin frequency (Crab 29.6 Hz, Measured); the **merger
  exhibit's** strain sent to audio (illustrative); **star voices** (off by default: the five
  brightest stars in view, temperature to timbre and flux to loudness, an authored convention);
  tapping the **CMB** plays a tone whose pitch follows dT (authored, labelled).
- **UI sounds:** hover 1500 Hz 30 ms at 0.045; press 880 to 620 Hz 70 ms at 0.11; generated, not
  shipped.
- **Sound lab** (TOOLS -> SOUND -> Sound lab): the bed's whole chain with every module parameter
  as a slider (VCO 1 and 2 frequency 40–800 Hz, fine plus or minus 50 cents, waveform; FM depth;
  noise level; VCF cutoff 60–5000 Hz, Q 0.5–20, modulation depth; LFO rate 0.02–15 Hz, depth;
  delay time 0.05–2 s, feedback 0–0.95, mix; reverb decay 0.5–8 s, mix; master volume), a scope
  strip, and Reset to preset. Mute persists between sessions.

**5.9.9 View tools.** **Clean view** hides labels, guides, cards and the panel, keeping a small
`!` badge if sizes are not true; any input brings the panel back. **Surface probe**: while the
ray is on a drawn body the card shows planetocentric latitude and longitude of the hit, and
elevation where a DEM is shipped. **Free tilt**, **Smooth turn**, **Handedness**. **Quality:**
Auto (the governor), Battery (72 Hz budgets), Max (90 Hz if it holds); the governor state is in
INFO -> STATUS. **Recentre:** re-places the active stage 0.7 m ahead of the eyes horizontally and
0.25 m below eye height, facing the user — the same placement every rung uses for its centre.

### 5.10 First run and failure-visible behaviour

| t | What the user sees | Why |
|---|---|---|
| frame 1 | the room, and a status card 0.8 m ahead: "CosmoSynth 2.0.0 · loading the universe", one line per data file with a progress mark | the 1.5.0 lesson: "passthrough works but there is nothing in it" must never be a possible state |
| 0.5–3 s | **Earth, 0.30 m across (rung `planet-hand`), 0.6 m ahead and 0.15 m below eye level**, lit from the real Sun direction for *now*, Blue Marble surface, clouds, terminator; label "Earth · 2026-09-11 18:04 UTC · real time"; the Moon and Sun as labelled points on the sky layer in their real directions | the ladder's first handhold; "real" and "now" are the headline promises |
| 3 s | the watch face on the left wrist pulses once; one hint line under the globe, generated from `XrInput.Bindings` for the active input: hands "Pinch with both hands and pull apart to zoom out · tap your wrist for the map"; controllers "Right stick to zoom · X for the map" | one sentence, not a tutorial |
| 5 s | if all data loaded, the status card collapses to one line ("all data loaded · real time") beside the globe | healthy runs get out of the way |
| 20 s | the status card and the hint disappear; the hint also disappears at the first zoom or pinch | |

Later launches skip the hint (a `PlayerPrefs` flag) and start the same way. The app does not
resume the previous session's view (D11).

**Nothing fails silently.** Every row below also writes one line to the play-mode report, so the
headless gates can see it.

| Condition | What the user sees |
|---|---|
| a data file missing or failing validation | its layer row reads `missing: <file>` or `invalid: <reason>`; the status card shows a line in `Warning` and stays until dismissed; the rest of the app runs |
| a layer whose data the licence profile left out | row `not in this build`, with the manifest's reason when it records one; a fallback (WMAP for Planck) is named in the layer's legend |
| catalogue validation warnings | a count on the status card; INFO -> STATUS lists them |
| the `galaxies` exhibit's capture fails its SHA-256 | exhibit row disabled: "capture failed verification" |
| no hands and no controllers tracked | the panel goes to the hands-free position; card "Show your hands or pick up a controller" |
| tracking lost | panel to the hands-free position; the watch face greys |
| the stage is out of view for 2 s | an edge arrow points to it |
| nothing drawn at the current scale in view (no object with `Weight > 0` larger than 3 px) | chip "Nothing here at this scale. Nearest: Proxima Centauri, 4.24 ly [GO]" |
| a scale limit reached | a controller pulse; the readout flashes "limit: true scale (1:1)" or "limit: observable universe" |
| time outside a valid span | `EPHEMERIS EXTRAPOLATED`, `STAR MOTION EXTRAPOLATED`, "no sunspot record" (§5.8.2) |
| a spacecraft selected outside its window | inspector: "outside its known window (1977–2035)"; Follow releases |
| the quality governor steps down | `QUALITY REDUCED` chip; STATUS says what was shed |
| the render configuration is not as designed (camera count other than 1, pipeline not URP, colour space not Linear, MSAA not 4, a 16-bit depth format) | a line in `Warning` on the status card, which stays; the `render:` line is in STATUS |
| passthrough unavailable | Space mode, plus a card line "Passthrough unavailable: showing space" |
| audio not built (2.0.0), or unavailable | the SOUND row reads "not in this build" or "unavailable" |
---

## 6. Data

Authority: Data section B. Full per-dataset specifications — byte layouts, importers, Core types,
tests, provenance and range assertions — are **Appendix C**. This part gives the conventions, the
loading contract, the budget and the lane mapping.

### 6.1 One container for every new binary

**6.1.1 The layout.** Today there are two binary formats (`CSS1`, a 16-byte header plus records,
and `CSM1` meshes). Ten new datasets would mean ten more hand-offset parsers, so every new
binary, the rebuilt stars included, uses **one column store**. Each dataset keeps its own 4-byte
magic, so a file loaded under the wrong name is refused. `.csmesh` is out of scope and unchanged.

```
CosmoSynth data container (all integers little-endian)
offset size  field
0      4     magic        4 ASCII bytes naming the kind, e.g. "CSS2"
4      4     version u32  layout version of that kind; the parser refuses any value it does not know
8      4     rows u32     rows of the primary table (for a grid: number of voxels)
12     4     flags u32    reserved, must be 0
16     4     metaBytes u32  length of the UTF-8 JSON meta block; a multiple of 8, padded with 0x20
20     4     reserved u32 must be 0
24     8     payloadBytes u64  bytes of column data after the meta block
32     metaBytes            meta JSON (§6.1.2)
32+metaBytes payloadBytes   columns, each starting at an 8-byte-aligned offset from the payload start
```

- **File length must equal `32 + metaBytes + payloadBytes` exactly.** A truncated download or a
  file written by another version is refused, exactly as `StarCatalog.Parse` refuses today.
- **Column types:** `u8 i8 u16 i16 u32 i32 f32 f64`. There is no `f16`: `System.Half` is not in
  netstandard 2.1 and the byte saving does not justify a hand-written converter.
- **Strings never live in columns.** Names are arrays in the meta JSON (at most 4,769 of them,
  for the exoplanet hosts).
- **Multi-table files:** a column may declare its own `"rows"` and `"table"` (for example `hosts`
  and `planets` in one exoplanet file). The header `rows` is the primary table.

| Magic | File | Kind |
|---|---|---|
| `CSS2` | `sky/stars.csstars` | stars |
| `CSEX` | `catalog/exoplanets.csexo` | exoplanet hosts and planets |
| `CSSB` | `sun/smallbodies.cssb` | osculating elements |
| `CSSA` | `sun/activity.cssa` | monthly solar indices |
| `CSDU` | `galaxy/dust.csdu` | voxel grid |
| `CSGP` | `galaxy/{cepheids,globulars,masers}.csgp` | point set (meta `kind` distinguishes) |
| `CSLV` | `lv/localvolume.cslv` | galaxy set |
| `CSRS` | `lss/2mrs.csrs` | galaxy set |
| `CSPT` | `lss/{cf4_groups,sdss}.cspt` | point set |
| `CSGR` | `lss/{cf4_basins,cf4_delta}.csgr` | voxel grid |
| `CSCB` | `cmb/cmb_sky.cscb` | cube-map sky |

`version` is 1 for every kind. The stars file's predecessor was the separate magic `CSS1`, which
the new parser refuses by name with "rebuild with `tools/cosmodata/build_starfield.py` (CSS1 is
the 1.x format)".

**6.1.2 The meta block.** JSON, parsed in Core by the existing `Json.cs`. These keys are
**required**; the parser refuses a file lacking one.

```json
{
  "kind": "stars", "schema": 1,
  "generator": "tools/cosmodata/build_starfield.py", "generatorCommit": "cf93a25+dirty",
  "generatedUtc": "2026-09-12T10:00:00Z", "licenceProfile": "sideload-nc",
  "frame": "ICRF", "epoch": "J2000.0",
  "sources": [ {"id": "athyg", "name": "AT-HYG v4.0 hyglike subset",
                "url": "https://codeberg.org/astronexus/athyg/media/branch/main/data/subsets/hyglike_from_athyg_v40.csv.gz",
                "retrievedUtc": "2026-09-12T09:58:11Z", "licence": "CC BY-SA 4.0", "licenceTag": "by-sa",
                "attribution": "...exact text from Appendix D..."} ],
  "columns": [ {"name": "pos", "type": "f32", "components": 3, "unit": "pc", "offset": 0},
               {"name": "vmag", "type": "f32", "unit": "mag", "offset": 1436032} ],
  "provenance": { "pos":  {"class": "Measured", "source": "athyg", "reference": "..."},
                  "vmag": {"class": "Measured", "source": "hyg44"} },
  "provenanceBits": [ {"bit": 1, "field": "teff", "class": "Inferred",
                       "note": "spectral class only; no B-V published"} ],
  "provenanceCodes": [ {"bits": [8, 10], "field": "pos",
                        "values": {"1": {"class": "Measured", "note": "Gaia DR3 parallax"},
                                   "3": {"class": "Measured", "note": "Hipparcos 2007 parallax"}}} ],
  "uncertainty": {"pos": "dist_err_pc"},
  "counts": {"rows": 119669, "distanceUnknown": 146},
  "assertions": [ {"name": "sirius_vmag", "expected": -1.44, "tolerance": 0.05, "got": -1.44, "ok": true} ]
}
```

- **Grids** add a `"grid"` object: `dims [nx,ny,nz]` with **x fastest in memory**;
  `sampling` = `"nodes"` (values at `origin + i*step`) or `"cells"` (the value covers
  `[origin + i*step, origin + (i+1)*step)`); `origin`, `step`, `unit`; `frame` =
  `galactic-helio` | `supergalactic` | `ICRF`.
- **Quantised columns** carry an `"encoding"`: `{"kind":"linear","scale":s,"offset":o}` decodes
  as `value = raw*s + o`; `{"kind":"log8","lo":lo,"hi":hi}` decodes as `0 -> 0` and
  `v >= 1 -> lo*(hi/lo)^((v-1)/254)`.
- **`provenanceBits` / `provenanceCodes`** turn a row's `flags` column into per-row, per-field
  provenance without shipping strings per row. **`uncertainty`** names the column that fills
  `FieldProvenance.Uncertainty`.
- **`assertions`** records every range and known-answer check the importer ran, with the value it
  got. An importer that fails an assertion writes nothing; the record exists so a reader of the
  shipped file can see what was checked.

**6.1.3 The code that reads and writes it.**

- **Python writer `TC/csdata.py`** (new, stdlib, integrator-owned):
  `write(path, magic, meta, columns)` where `columns` is a list of
  `(name, type, components, bytes, extra)`. It aligns each column and fills `offset`, `columns`
  and `payloadBytes`. It writes `path + ".tmp"`, flushes and `fsync`s, reads it back **through
  its own reader** (magic, exact length, first and last element of every column), and only then
  `os.replace`s. On any failure it deletes both the temporary file **and the stale target** and
  exits non-zero. `csdata.py --selftest` writes `T/fixtures/csdata_fixture.csd`: three rows, one
  column of each type, an encoding, a grid block and a bit override. **That small committed file
  is the cross-language contract.**
- **Core reader `Core/Data/CsData.cs`** (new, engine-free):
  `static CsData Parse(byte[] data, string expectedMagic, uint maxVersion)`, `JsonValue Meta`,
  `int Rows`, `bool Has(string column)`, `float[] F32(string)`, `double[] F64(string)`,
  `byte[] U8(string)`, `ushort[] U16(string)`, `uint[] U32(string)`, `int[] I32(string)`,
  `FieldProvenance ProvenanceFor(string field, uint rowFlags, int row)`, `string Attribution()`.
  It copies with `Buffer.BlockCopy` and asserts `BitConverter.IsLittleEndian`. Every refusal is a
  `FormatException` naming the kind and the importer to re-run.
- **Core test `T/CsDataTests.cs`**: parse the committed Python-written fixture and check every
  value; refusals for a wrong magic, a version too new, a header/length disagreement, a column
  running past the end, a misaligned offset, an unknown type, `metaBytes` not a multiple of 8,
  invalid JSON, a missing required meta key; and the round trip of `log8` and `linear` decoding
  at `raw = 0, 1, 255`.

### 6.2 Importer rules and the licence switch

**6.2.1 Rules for every `TC/*.py`.**

1. **Standard library only.** The existing exception is `npx` for Draco in `build_spacecraft.py`.
   This plan adds exactly one more, and `CLAUDE.md` must list it: **numpy** (system 2.5.1,
   VERIFIED present) in **`build_dust.py`, `build_cmb.py` and the grid part of `build_lss.py`**,
   only for FITS cubes and HEALPix. Nothing else may import numpy. The phenomena texts are
   converted from the user's `catalog.js` **once, by hand**, into an authored JSON; the pipeline
   never runs node.
2. **Shared helpers, so no importer re-implements them** (all integrator-owned, delivered P0.2):
   - `fetch.py`: `get(url, expect=...)` sniffing magic bytes (gzip `1f 8b`; FITS `SIMPLE  =`;
     JSON `{` or `[`; a CSV whose header starts with an expected column name; zip `PK`),
     `get_range(url, a, b)`, three retries with backoff, a `CosmoSynth/2.0 (importer)`
     User-Agent, and timeouts. **A 200 response is not evidence of data.**
   - `fits.py` (stdlib): `read_header(stream)` (2880-byte cards), `image_layout(cards)`,
     `bintable_layout(cards)`; it returns offsets and dtypes, and the numpy scripts wrap it.
   - `licence.py` (§6.2.2), `csdata.py` (§6.1.3), `ledger.py` (§6.5.2). **`ledger.py`'s CLI**
     (R35): `--write` regenerates `SA/manifest.json` **and** the `docs/data-sources.md` ledger
     table; `--check` verifies both against the tree and fails if they disagree; `--candidate
     <paths>` checks files that are not yet in the manifest, which is what a lane runs, because
     the manifest itself is regenerated at merge time and no lane may write it (§9.6 step 3b).
3. **Write to a temporary name, then `os.replace`, or write nothing.** On refusal, delete the
   stale target, so "an artefact on disk is not evidence of the run" holds by construction.
4. **Physical range assertions on values, never on metadata.** Never trust FITS `TUNIT` or
   `BUNIT`, and never trust a column name. Each dataset's assertion list is in Appendix C. A row
   failing a per-row bound is dropped and **counted in `counts`**; a dataset whose drop count
   exceeds its stated budget is refused whole.
5. **Every run records** its sources (URL, retrieval time, licence tag), the generator commit
   (`git rev-parse --short HEAD` plus `+dirty`), and its assertions.
6. **Pin what can be pinned.** Release tags or commit SHAs in raw URLs where the host supports
   them: LVDB `v1.1.1`, and the Stellarium commit resolved through the GitHub API. Record the
   resolved SHA.
7. **Run on triton only, niced.** Stream large inputs and never keep them: the Planck FITS,
   Vergely's 40 MB cube (to `/tmp`, deleted on exit) and the CF4 zip member (a range fetch). Peak
   disk use stays under 50 MB. Raw downloads go to `~/.cache/cosmosynth/raw/<source>/` and are
   deleted after the bake; numpy bakes run under `flock /tmp/cosmosynth-bake.lock`, one at a
   time.

**6.2.2 The licence switch.** `TC/licence.py` defines `PROFILE`, read from the environment
variable `COSMODATA_LICENCE`, default `sideload-nc`. Each source in each importer declares one
licence tag.

| Tag | Meaning | `sideload-nc` | `public-nc` | `commercial` |
|---|---|---|---|---|
| `pd` | US government / public domain | ship | ship | ship |
| `pd-ack` | public, acknowledgement required | ship | ship | ship |
| `cc0` | CC0 | ship | ship | ship |
| `by` | CC BY | ship | ship | ship |
| `by-sa` | CC BY-SA; the derived file is BY-SA | ship | ship | ship |
| `nc` | non-commercial (ESA CC BY-NC IGO, AAS via VizieR, A&A "scientific use", SILSO) | ship | ship | **fallback or drop** |
| `unclear` | no redistribution grant (Skowron, CF4 grids) | ship | **drop** | **drop** |
| `own` | the user's own work | ship | ship | ship |

- **No shipped file may carry two incompatible tags** (R40). **A `by-sa` file may contain only
  `by-sa`, `by`, `cc0`, `pd`, `pd-ack` and `own` sources; `nc` and `unclear` sources go in their
  own file.** CC BY-SA 4.0 requires the adapted material to be offered under BY-SA, which permits
  commercial use, so a BY-SA file containing non-commercial material is unlicensable and its
  `licenceTags` would state something untrue. `ledger.py --check` fails on a mixed file. Audited
  under this rule: `sky/stars.csstars` is `by-sa`, so the Reylé 10 pc census (`nc`) moves out into
  `sky/census.cspt` (C.1, §4.6); `lv/localvolume.cslv` is `cc0 + nc + own` and is fine, because
  CC0 imposes nothing on the combination; `sun/activity.cssa` is `pd + nc` and is fine for the
  same reason.
- **Drop means the importer writes nothing and deletes the shipped file.** The ledger then omits
  it from `manifest.json`. The runtime loads only what the manifest lists, so **dropping a source
  changes no code** (binding 7).
- **Where a fallback exists, the importer writes the fallback to the same path and filename**:
  WMAP for Planck, Edenhofer for Vergely, SDSS-only for 2MRS, the hand-entered giants for UNGC.
- **Stars are the exception.** The `by-sa` files carry ESA-derived astrometry, and whether that
  makes them `nc` is a lawyer question (user decision D1). Under `commercial`,
  `build_starfield.py` **refuses to run** until `TC/licence_decisions.json` records a decision. It
  must not silently ship, and it must not silently gut the sky.
- `ledger.py --check` fails gate G0f when any file under `SA/` is absent from the manifest, a
  manifest file is missing, or a file's `licenceTag` is not allowed by the recorded profile.

### 6.3 Frames, units and the one H0

- **Every position column is ICRF** (J2000 equator and equinox, epoch stated in meta), in the
  unit named by the column: pc for stars and Milky Way points, kpc for the Local Volume, Mpc for
  large-scale structure.
- **Grids are the one exception.** They stay in their native frame (`galactic-helio` or
  `supergalactic`), because resampling would blur them. The grid meta declares the frame and Core
  supplies the fixed rotations of §3.2.
- **No file contains a Unity axis.** The CMB cube-map faces are defined in ICRF components for
  the same reason.
- **Units:** km and s inside Core bodies (unchanged), and the stated unit per column in the
  containers. Core converts at the boundary with `Constants.Parsec`, `KiloParsec` and
  `MegaParsec` (VERIFIED present).
- **One H0.** All redshift-derived distances use **H0 = 74.6 km/s/Mpc with Omega_m = 0.3153** in
  a flat LambdaCDM comoving distance. This is Cosmicflows-4's own calibration (VERIFIED, CF4
  ReadMe "H_0_=74.6"), so 2MRS, SDSS and the named clusters land on the same radial scale as the
  CF4 groups, the CF4 grids and the Dupuy & Courtois basins. Using Planck's 67.4 for the redshift
  layers would put every cluster **10.7 % farther out in the redshift layers than in the CF4
  layers** — the Hubble tension, visible as doubled structure. The shape of the expansion history
  (Omega_m) comes from Planck 2018 (VERIFIED 0.3153); only the distance scale comes from CF4.
  - Redshifts convert to the CMB frame first:
    `cz_cmb = cz_helio + 369.82 km/s * (n . d_dipole)` with d from (l, b) = (264.021, 48.253).
    The conversion is first order in beta (beta = 1.2e-3), which is exact enough.
  - Then `D_C(z) = (c/H0) * integral_0^z dz' / sqrt(Omega_m (1+z')^3 + 1 - Omega_m)`. The
    importers integrate it in Python; `Core/Scale/Cosmology.cs` has the same function and the
    tests compare the two. Known answers (VERIFIED by probe integration):
    **D_C(0.05) = 198.54 Mpc, D_C(0.1) = 392.20 Mpc, D_C(0.2) = 764.64 Mpc** (cz/H0 would give
    200.93, 401.87 and 803.73).
  - **CF4 h-scaled grids convert with h = 0.746.**
  - **Planck's own numbers are quoted as Planck numbers** and are not recomputed with 74.6:
    chi* = 13.87 Gpc (4.28e26 m), the age 13.797 Gyr, H0 = 67.36 for statements about the early
    universe only. At the CMB's radius a 10 % scale choice is invisible against 300 Mpc of
    galaxies; the CMB panel says "Planck 2018 LambdaCDM".
  - **Provenance of every redshift distance:** `Inferred`, source "Hubble-law comoving distance,
    CMB frame, H0 = 74.6, Omega_m = 0.3153; peculiar velocities of about plus or minus 1000 km/s
    (plus or minus 13 Mpc) not removed".

### 6.4 The fifteen datasets: the index

Full specifications are Appendix C. Sizes are raw / deflated MB.

| # | Layer group | Primary source | Shipped file | Size | Tag | Fallback | Appendix |
|---|---|---|---|---|---|---|---|
| 1 | Stars | **AT-HYG v4.0 hyglike** astrometry + **HYG v4.4** photometry, joined on the HYG id | `sky/stars.csstars` (`CSS2`) | 4.79 / ~4.3 | by-sa (ESA upstream: D1) | none | C.1 |
| 2 | Names, constellations | AT-HYG `proper`; Stellarium *modern* `index.json` | `sky/stars_named.json`, `sky/constellations.json` | 0.11 | by-sa | — | C.2 |
| 3 | Exoplanets | NASA Exoplanet Archive `pscomppars`, **all 6,360 planets / 4,769 hosts** | `catalog/exoplanets.csexo` (`CSEX`) | 0.64 / ~0.5 | pd-ack | — | C.3 |
| 4 | Belts | JPL SBDB: asteroids H < 14, union TNO, union CEN (about 34 k) | `sun/smallbodies.cssb` (`CSSB`) | 1.26 / ~1.1 | pd | — | C.4 |
| 5 | Sun activity | SWPC monthly SSN and F10.7, 1749-01 to 2026-08, plus the predicted cycle; a dated snapshot | `sun/activity.cssa` (`CSSA`), `sun/snapshot.json` | 0.07 | pd / nc (SSN) | F10.7 only | C.5 |
| 6 | Moon, deep-time planets, reference tables | **Core code**: Meeus ch. 47, Standish tables, published constants | — | 0 | cite | — | C.6 |
| 7 | Milky Way dust | **Vergely+2022** 050 pc cube at native 20 pc, 10 x 10 x 0.8 kpc | `galaxy/dust.csdu` (`CSDU`) | 10.29 / **6.22** | nc | Edenhofer+2024, CC BY | C.7 |
| 8 | Cepheids, globulars, masers | Skowron+2019; LVDB GCs; Reid+2019 table 1 | `galaxy/{cepheids,globulars,masers}.csgp` (`CSGP`) | 0.08 | unclear / cc0 / nc | each droppable | C.8 |
| 9 | Spiral arms | Reid+2019 Table 2 | **Core constants** | 0 | cite | — | C.6 |
| 10 | Landmarks, nebulae | hand-authored, one reference per entry | `galaxy/landmarks.json` | 0.02 | own | — | C.9 |
| 11 | Local Group / Volume | **LVDB v1.1.1** dwarfs (911, CC0) + UNGC non-LVDB giants + 2MASS PA + hero overrides | `lv/localvolume.cslv` (`CSLV`) | 0.07 | cc0 + nc | `authored/giants_fallback.json` | C.10 |
| 12 | Large-scale structure | 2MRS (+2MASS XSC PA); CF4 groups; CF4 BoA basins 128^3; CF4 delta 64^3; named structures; SDSS DR18 100 k subsample | `lss/*.csrs`, `.cspt`, `.csgr`, `structures.json` | 7.0 / ~4.3 | nc / unclear / pd | SDSS (pd) | C.11 |
| 13 | CMB | **Planck PR3 SMICA-noSZ** `I_STOKES_INP` to a **cube map 6 x 1024^2 R8**, plus an inpainting mask at 6 x 256^2 | `cmb/cmb_sky.cscb` (`CSCB`) | 6.68 / ~4.6 | nc (ESA) | WMAP 9-yr ILC | C.12 |
| 14 | Fictional | the holographic star map (2) plus base names, renamed | `catalog/fictional_holosystem.json` | 0.02 | own | — | C.13 |
| 15 | Phenomena | the Cosmic Atlas XR capture plus the exhibit texts | `phenomena/{encounter.bin,encounter.json,exhibits.json}` | 1.51 / ~1.4 | own | — | C.14 |
| — | Manifest | generated by `ledger.py` | `manifest.json` | 0.01 | — | — | §6.5.2 |

**Six findings the implementers must not rediscover** (all VERIFIED by read-only probes on
2026-09-11):

1. **AT-HYG's magnitudes are not Johnson V for bright stars.** 118,237 of 119,670 rows have
   `mag_src = T` (Tycho). **Sirius is -1.088 there against -1.44 in HYG v4.4**; Arcturus +0.16
   against -0.05; Betelgeuse 0.77 against 0.45; B-V is blank for Sirius, Arcturus and Vega. All
   119,614 rows carrying a `hyg` id match HYG v4.4 `id` one-to-one with agreeing `hip`, so the
   join of C9 is exact.
2. **AT-HYG positions are epoch J2000.0.** Barnard's Star has Dec 4.69328, against the J2000
   value 4.6934 and the J2016 value 4.7395. AT-HYG also carries velocities (`vx, vy, vz` in
   pc/yr; Barnard's comes to 142 km/s, matching the 142.5 computed from its proper motion,
   parallax and RV), so stars can move under the deep-time warp.
3. **The Planck column lies about its units.** `I_STOKES_INP` is in uK although its `TUNIT` says
   K_CMB. Every FITS bake asserts a physical range and never trusts `TUNIT`.
4. **The two CF4 grid families use opposite axis orders.** The watershed labels are x-fastest;
   the 64^3 delta grid is z-fastest. Its box geometry was verified by fit, and the paper's text
   disagrees with it.
5. **ESA archive data is CC BY-NC 3.0 IGO, not CC BY-SA.** This covers Planck, Gaia and
   Hipparcos. `docs/data-sources.md`'s "Stars (deeper than HYG)" row says "CC BY-SA 3.0 IGO"
   (VERIFIED wrong); Appendix D rewrites it.
6. **SBDB has exactly the belt populations we want**: 32,294 asteroids with H < 14, 7,287 TNOs,
   1,047 centaurs and 16,391 Jupiter trojans. The query API as probed returns about four
   significant figures (Ceres `a = 2.766`), so the importer must request full precision and
   assert that it got it.

### 6.5 The StreamingAssets loading contract

**6.5.1 One reader.** `Runtime/Data/StreamingAssetReader.cs`, a static class, not a MonoBehaviour:

```csharp
public static IEnumerator Read(string relativePath, Action<byte[], string> done)
// url = Application.streamingAssetsPath + "/" + relativePath;
// if (!url.Contains("://")) url = new Uri(url).AbsoluteUri;       // file:///... on desktop and editor
// UnityWebRequest.Get(url), timeout 30 s; done(bytes, null) or done(null, "<result>: <error>")
public static string Utf8(byte[] b)   // UTF-8, BOM stripped
```

- **UnityWebRequest on every platform**, the editor and the flat harness included, so the desktop
  path exercises the same branch as the headset. UnityWebRequest accepting `file://` URIs is
  INFERRED; the first harness run proves it.
- **Delete** `UniverseSimulation.ReadStreamingAsset`, `ReadStreamingAssetBytes` and
  `LoadCatalogFromStreamingAssets` (all VERIFIED present at `:104`, `:161`, `:211`), and
  `PlanetTextureBaker`'s private reader, which swallows errors. Every caller uses `Read`.
- **Failure contract.** One log line, `[CosmoSynth] data: <path>: <error>`, then `null`. The
  reader never throws. `manifest.json` and `catalog/solar_system.json` are **required**: without
  them the app shows "data missing — rebuild with tools/cosmodata" and stops. Every other file is
  optional, and its layer reports "not in this build" with the reason from the manifest.

**6.5.2 The manifest is the only list of files.** `TC/ledger.py` generates
`SA/manifest.json`. **Runtime code never hard-codes a data path**; it iterates the manifest.

```json
{ "schema": "cosmosynth.manifest/1", "generatedUtc": "...", "licenceProfile": "sideload-nc",
  "generatorCommit": "...",
  "files": [ { "path": "galaxy/cepheids.csgp", "kind": "cepheids", "magic": "CSGP", "bytes": 69123,
               "sha256": "...", "required": false, "droppable": true, "load": "startup", "order": 60,
               "features": { "cepheids": true },
               "licenceTags": ["unclear"],
               "attribution": ["Classical Cepheids: Skowron D. M. et al. 2019, Science 365, 478"] } ],
  "credits": [ { "text": "Classical Cepheids: Skowron D. M. et al. 2019, Science 365, 478",
                 "licence": "no redistribution grant stated",
                 "licenceUrl": "https://github.com/jskowron/galactic_cepheids",
                 "changed": "re-encoded as CSGP; positions unchanged",
                 "files": ["galaxy/cepheids.csgp"] } ] }
```

- At runtime a file's length must equal `bytes`; that check is cheap. SHA-256 is checked at the
  build gate, not at runtime. The one exception is `encounter.bin`, whose SHA **is** its
  contract.
- **`droppable` is the single machine-readable source** (R35). `drop_test.sh` reads
  `SA/manifest.json`, never `docs/data-sources.md`; the doc row is prose for humans and
  `ledger.py --check` fails if the two disagree.
- **`features`** (R47) is written by the importer from what it actually produced, keyed by the
  `LayerDef.RequiresFeature` strings. Three pieces of content are per-field or per-flag **inside**
  a kind that is still present, so the kind loading does not prove the content is there: the 10 pc
  census (its own file now, so `features.census` is simply its presence), the sunspot-number
  columns of `activity.cssa` (which the licence profile may blank to NaN, C.5 flag 1), and the
  flare-star boolean in `stars_named.json` (gap G7). Without `features` the kind loads, `Weight`
  is non-zero, the row reads `on` and nothing is drawn — the exact failure §5.10 exists to
  prevent. The Sun's VITALS card prints "sunspot number: not in this build (licence) — F10.7
  only" when `features.ssn` is false.
- **`credits` is structured, not free text** (R50). A tab named "CREDITS & LICENCES" that can show
  no licence is a bug: CC BY, BY-SA, BY-NC and CC0 attribution all require naming the licence and
  giving its URI wherever the material is distributed, and BY-SA additionally requires indicating
  that changes were made — and **every file here is a re-encoding**. The CREDITS page prints, per
  entry, the attribution verbatim, then `<licence> · <licenceUrl>`, then the `changed` line where
  present, and closes with a footer generated from the profile: *"This build ships data under
  non-commercial terms and is for personal use only."* `ledger.py --check` **fails** when any
  shipped file's source lacks `licence` or `licenceUrl`, or — for a `by-sa` or `by` source —
  `changed`.
- `kind` values are exactly the §4.6 namespace.

**6.5.3 Load order.** `Runtime/Data/DataLoader.cs` is one MonoBehaviour. It walks the manifest
`order`, waits one frame between files, parses with the Core type, and raises
`LayerReady(kind, object)`. Nearest-scale data comes first, because the default view is Earth at
0.30 m; the heavy textures come last.

| Order | Files | On load |
|---|---|---|
| 0 | `manifest.json` | required |
| 10 | `catalog/solar_system.json`, `catalog/spacecraft.json` | Core `CatalogAssembler` merges them, then **`SetCatalog` exactly once** |
| 20 | `sky/stars.csstars`, `sky/census.cspt`, `sky/stars_named.json`, `sky/constellations.json` | the star layer; the census layer; `NearbyStarSystems` |
| 30 | `catalog/exoplanets.csexo` | the host layer; star cross-links |
| 40 | `sun/smallbodies.cssb`, `sun/activity.cssa`, `sun/snapshot.json` | |
| 50–60 | `galaxy/landmarks.json`, `galaxy/*.csgp`, `galaxy/dust.csdu` | |
| 70 | `lv/localvolume.cslv` | |
| 80 | `lss/structures.json`, `lss/2mrs.csrs`, `lss/cf4_groups.cspt`, `lss/cf4_basins.csgr`, `lss/cf4_delta.csgr`, `lss/sdss.cspt` | |
| 90 | `cmb/cmb_sky.cscb` | |
| lazy | `catalog/fictional_holosystem.json` (when the Fictional stage opens); `phenomena/*` (when an exhibit opens) | `load: "lazy"` in the manifest |

The startup read is about 25 MB raw; INFERRED under 2 s on Quest 3 behind a rendered first frame,
with layers appearing as they arrive.

**6.5.4 Memory** (data side; vertex formats belong to rendering):

| | Estimate |
|---|---|
| Transient peak | the largest file three times over (the `byte[]`, the Core typed copy, the upload `NativeArray`): about 31 MB, during the dust load. Each is released once used |
| Resident Core copies kept for picking, measuring and tap-to-read | stars (pos, vmag, flags, hip: 2.9 MB), the exoplanet table (0.6), small bodies (1.3), dust (10.3, for `ExtinctionAlong`), CMB (6.7), LSS (about 5), Local Volume and points (about 0.3). **About 27 MB** |
| GPU textures from data | dust R8 3D 10.3 MB (11.8 with mips); the CMB R8 cube 8.4 MB with mips plus the 0.5 MB mask; CF4 basins R8 3D 2.1 MB; delta 0.26 MB. **About 23 MB** |
| GPU vertex data | owned by rendering: stars about 12.5 MB, 2MRS about 6.6 MB |

### 6.6 Catalogue assembly and what stays a body

- **`Core/Catalog/CatalogAssembler.cs`** (new, integrator-owned):
  `Assemble(IReadOnlyList<(string name, string json)>) -> (CelestialCatalog, IReadOnlyList<string> problems)`.
  It parses each text, merges in the fixed order solar system then spacecraft, runs `Validate()`
  once, and returns. The runtime calls `SetCatalog` **once** with the result.
  **Exoplanets are no longer in this merge**, so the first root is the Sun again — which removes
  the "Sun's light attached to 61 Vir" defect at its data cause (`SkyAndRings.cs:585` reads
  `RootIds[0]`, VERIFIED). Test `T/CatalogAssemblerTests.cs`: the shipped files give 31 + 24 = 55
  bodies, 0 errors, `RootIds[0] == "sun"`.
- **Real exoplanet systems:** `ExoplanetTable.Expand(host, epoch)` then `AddBodies`, when a host
  is visited, selected or becomes the nearest system. When the nearest CSS2 star has
  `HostForStarRow >= 0`, `NearbyStarSystems` expands the real system instead of generating one.
- **Fictional:** a separate `CelestialCatalog` in its own simulation root, never passed to
  `SetCatalog` or `AddBodies` of the real universe.
- **Never bodies:** stars, exoplanet hosts (until expanded), belts, Milky Way points, dust,
  galaxies, clusters, grids, the CMB, landmarks.
- **Id conventions** (fixed here): real exoplanets `exo-<slug>`; generated systems
  `gen-hyg<row>-...`; fictional `fic-holo-...`.

### 6.7 APK size budget

Today's APK is **46,520,034 B = 46.5 MB** (VERIFIED, the recovered 1.9.0) and its
StreamingAssets is 6.7 MB (VERIFIED). Deflate ratios marked `~` are INFERRED; the rest are
measured. Whether Unity deflates StreamingAssets entries in the APK is INFERRED either way, so
both columns are given.

| File | Raw MB | Deflated MB | Delta against 1.9.0 (raw) |
|---|---|---|---|
| `sky/stars.csstars` (CSS2, **44 B/star** with `av`, R41) | 5.27 | ~4.7 | +2.40 (replaces 2.87) |
| `sky/census.cspt` (split out of the star file, R40) | 0.02 | ~0.02 | +0.02 |
| `sky/stars_named.json` + `constellations.json` | 0.11 | ~0.02 | +0.06 |
| `catalog/exoplanets.csexo` | 0.64 | ~0.5 | +0.31 (`exoplanets.json` 0.33 deleted) |
| `sun/smallbodies.cssb` | 1.26 | ~1.1 | +1.26 |
| `sun/activity.cssa` + `snapshot.json` | 0.07 | ~0.04 | +0.07 |
| `galaxy/dust.csdu` | 10.29 | **6.22** | +10.29 |
| `galaxy/*.csgp` + `landmarks.json` | 0.11 | ~0.09 | +0.11 |
| `lv/localvolume.cslv` | 0.07 | ~0.05 | +0.07 |
| `lss/2mrs.csrs` | 1.56 | ~1.35 | +1.56 |
| `lss/cf4_groups.cspt` | 1.07 | ~0.95 | +1.07 |
| `lss/cf4_basins.csgr` | 2.10 | **0.04** | +2.10 |
| `lss/cf4_delta.csgr` | 0.26 | ~0.2 | +0.26 |
| `lss/sdss.cspt` (100 k) | 2.00 | ~1.8 | +2.00 |
| `cmb/cmb_sky.cscb` (1024^2 faces + mask) | 6.68 | ~4.6 | +6.68 |
| `catalog/fictional_holosystem.json`, `lss/structures.json`, `manifest.json` | 0.04 | ~0.01 | +0.04 |
| `phenomena/*` | 1.51 | ~1.38 | +1.51 |
| **Total added** | **about 29.8** | **about 20.4** | |
| **Expected APK** (plus the code and shader delta) | **about 76.3** stored | **about 66.9** deflated | |

**Gates.** `tools/gates/data-budget.txt` = **50** (MB of `SA/`, C22), checked by G0f. The G3
artefact gate records the real APK size and fails above **100 MB**. Knobs, all without code
changes: `build_dust.py --step 40` saves 7.7 MB raw; `build_lss.py --keep 50000` saves 1 MB;
`build_cmb.py --face 512` saves 5 MB. The knob that costs is `--face 2048` (+18.9 MB raw), and
taking it requires an integrator commit raising `data-budget.txt`.

### 6.8 `docs/data-sources.md`

Replaced wholesale at P0.2 (C21) by the structure of Appendix D: the licence profile in force,
the ledger table, the four recorded corrections, the per-source notes with one `lane:<name>` slot
each, and "Reproducing the data" with the importer order of §6.9. Its good prose is kept: the
Horizons notes, the NASA 3D Resources lessons, the HYG honesty notes and "Hosted by NASA is not
public domain".

### 6.9 The importer build order, mapped onto the lanes

**6.9.1 B's ten steps to D's lanes** (C25):

| B step | Deliverable | Becomes |
|---|---|---|
| 0 `data-core` | `TC/{csdata,fetch,fits,licence,ledger}.py` + the `--selftest` fixture; `Core/Data/{CsData,DataManifest}.cs` + tests; `Runtime/Data/{StreamingAssetReader,DataLoader}.cs`; the `CLAUDE.md` Data section | **P0.2** (contracts) and **P0.4d** (the Runtime half), integrator |
| 1 `data-stars` | `build_starfield.py` (CSS2 + names + constellations + census); `StarCatalog` rewrite, `Constellations`, tests | lane **`stars`** 1a |
| 2 `data-exo` | `build_exoplanets.py`; `ExoplanetTable`, tests | lane **`exoplanets`** 1a |
| 3 `data-core-physics` | `LunarTheory`, `StandishElements`, `Heliosphere` | lane **`solar`** 1a |
| | `GalaxyModel` | lane **`milkyway`** 1a |
| | `CmbModel` | lane **`cmb`** 1a |
| | `Cosmology` | lane **`galaxies`** 1a |
| | the `StandardFrames` galactic fix + `GalacticToSupergalactic` + the Hipparcos test | **P0.3** (C5), integrator / `p0-core` |
| 4 `data-sun` | `build_small_bodies.py`, `build_heliophysics.py`; `SmallBodyTable`, `SolarActivity` | lane **`solar`** 1a |
| 5 `data-galaxy` | `authored/landmarks.json`; `build_galaxy_points.py`; `build_dust.py`; `DustCube`, `PlaceCatalog` | lane **`milkyway`** 1a |
| 6 `data-lv` | `build_localvolume.py`, `authored/{overrides,giants_fallback}.json`; `GalaxySet`, `GalaxyOrientation` | lane **`galaxies`** 1a |
| 7 `data-lss` | `build_lss.py` (2MRS + the XSC join, CF4 groups, basins, delta + the slice PNG, SDSS) and `structures.json` | lane **`galaxies`** 1a |
| 8 `data-cmb` | `build_cmb.py`, `healpix_nest.py`; `CmbSky`. **The GPU face-orientation check is the `cmb` lane's 1b assertion** | lane **`cmb`** 1a and 1b |
| 9 `data-extras` | `authored/fictional_holosystem.json`; `authored/exhibits.json` (the one-off conversion); the phenomena copy; `EncounterCapture` | lane **`fiction`** 1a |
| 10 integrator | run every importer on triton, then `ledger.py`, then `manifest.json`, then Appendix D, then record the APK size at G3 | integrator, Phase 2 |

**Extinction ordering** (R41). `build_starfield.py` takes an optional `--dust <path>` and, when
given `galaxy/dust.csdu`, writes the `av` column from the cube along each Sun-to-star line.
**Without it** — which is the normal case inside the `stars` lane, since `milkyway` bakes the cube
in a different worktree at the same time and lanes never talk — it writes `av = 0` and sets flag
12 `ExtinctionUnknown` on every row, and the meta records that. **Step 10 below (the integrator,
Phase 2) re-runs `build_starfield.py --dust` after both lanes have merged**, which is already in
the plan, so the shipped file always carries a real `av`. No cross-lane dependency is created.

`build_authored.py` is **integrator-owned** and schema-driven: it validates and copies every
`TC/authored/*.json` named in a table that lanes extend inside their own slot. **The table's row
schema, fixed here so the three lanes can write a row without asking** (R25):

```python
# tools/cosmodata/build_authored.py
# (source path under TC/authored/, schema name, destination under SA/, sha256-checked?)
AUTHORED = [
    ("authored/landmarks.json", "cosmosynth.places/1", "galaxy/landmarks.json", False),
    # lane:milkyway BEGIN
    # lane:milkyway END
    # lane:galaxies BEGIN
    # lane:galaxies END
    # lane:fiction BEGIN
    # lane:fiction END
]
``` Authored inputs
themselves are lane-owned: `landmarks.json` (milkyway), `structures.json`, `overrides.json`,
`giants_fallback.json` (galaxies), `fictional_holosystem.json`, `exhibits.json` (fiction).
`overrides.json` and `giants_fallback.json` are importer inputs and are **not** shipped.

**Network time on triton** (INFERRED from measured throughputs): Planck about 1 min; SDSS
16 chunks of about 10 s; the 2MASS XSC query minutes; Vergely 30 s; everything else seconds;
Edenhofer, only if chosen, tens of minutes.

**6.9.2 Section C's data gaps, assigned** (C20):

| Gap | Item | Ruling |
|---|---|---|
| G1 | Solar disk stills (22 channels) and one SUVI 304 loop | **deferred** — user decision D5; the VITALS row reads "not in this build" |
| G2 | Horizons tables for New Horizons (-98), BepiColombo (-121), Artemis I (-1023); optionally Cassini's 1997–2004 cruise; the authored Apollo 11 table | lane **`solar`** 1a (its card already names the three) |
| G3 | Seven moons (Mimas, Tethys, Dione, Miranda, Ariel, Umbriel, Proteus) and eight small bodies (Hygiea, Eris, Makemake, Haumea, Sedna, Quaoar, Gonggong, Orcus) | lane **`solar`** 1a |
| G4 | Surface sites: Curiosity, Viking 2, Sojourner, Opportunity, Venera 7; Mars landmarks | lane **`solar`** 1a, each coordinate cited |
| G5 | An Earth cloud map (NASA Visible Earth `cloud_combined_2048.jpg`, VERIFIED reachable) | lane **`solar`** 1a, into `surfaces/` |
| G6 | Zonal wind profiles with a citation per planet | **deferred to Phase 3**; `winds` is off by default and reads "not in this build" |
| G7 | A flare-star flag for the 12 named flare stars | lane **`stars`** 1a, a boolean in `stars_named.json`, **and `features.flare` in the manifest** (R47), so the `flare` layer says "not in this build" rather than reading `on` and drawing nothing |
| G8 | Elevation maps (Moon LOLA, Mars MOLA) for RELIEF | **deferred to Phase 3** (APK size); RELIEF says "no elevation data for &lt;body&gt;" |
| G9 | A per-spacecraft attitude rule | lane **`solar`** 1a, a column in `spacecraft.json` |
| G10 | Twelve observing-site coordinates | lane **`nav`** 1a, `Core/Sky/ObservingSites.cs` as Measured constants with a source |

### 6.10 `SA/` after 2.0.0

```
manifest.json
catalog/solar_system.json      catalog/spacecraft.json      catalog/exoplanets.csexo
catalog/fictional_holosystem.json
sky/stars.csstars              sky/stars_named.json         sky/constellations.json
sky/census.cspt
sun/smallbodies.cssb           sun/activity.cssa            sun/snapshot.json
galaxy/dust.csdu               galaxy/cepheids.csgp         galaxy/globulars.csgp
galaxy/masers.csgp             galaxy/landmarks.json
lv/localvolume.cslv
lss/2mrs.csrs  lss/cf4_groups.cspt  lss/cf4_basins.csgr  lss/cf4_delta.csgr  lss/sdss.cspt  lss/structures.json
cmb/cmb_sky.cscb
phenomena/encounter.bin  phenomena/encounter.json  phenomena/exhibits.json
models/*.csmesh                surfaces/*                   (unchanged)
```

`catalog/exoplanets.json` is **deleted at P0.4d** (C10). **Everything shipped is committed**, as
today: the tests read the shipped files and lanes need them without running importers. Rebake at
most once per release.
---

## 7. Rendering: shaders, budget, colour

### 7.1 Rules for every CosmoSynth shader

1. `Shader "CosmoSynth/<Name>"`, `"RenderPipeline"="UniversalPipeline"`, one `Pass` with
   `"LightMode"="UniversalForward"`, `HLSLPROGRAM` only (no CG). **Single variant**: the only
   pragma is `#pragma multi_compile_instancing`. No `shader_feature`, no custom keywords;
   switches are uniform branches. XR stereo variants are added by Unity itself.
   **Any shader whose vertex or fragment function reads object space declares
   `"DisableBatching"="True"` in its SubShader Tags** (R12) — `CosmoSynth/Volume` (the march's
   object-space entry point), `CosmoSynth/Sky` (object-space vertices *are* ICRF directions) and
   `CosmoSynth/Ribbon` (`otherOS`). Static batching bakes vertices into world space and would
   silently destroy all three; it is a Player Setting, on by default for Android, and it is now in
   the G0c table as `m_StaticBatching: 0`. The tag costs nothing and does not depend on that
   setting staying right.
2. **Never call `GetCameraPositionWS()`** (R1). In URP it resolves to `_WorldSpaceCameraPos`
   (URP's camera-relative branch is commented out), and whether the XR path redefines that as
   `unity_StereoWorldSpaceCameraPos[unity_StereoEyeIndex]` is a package-version detail this
   project does not control — and **four** render paths (the sky anchor, the point sky mode, the
   ribbon sky placement, the body's view vector, the volume's ray origin) are wrong in both eyes
   at once if it is the centre eye. Use **`CS_EyePositionWS()`** (A.2), which reads
   `UNITY_MATRIX_I_V` — `unity_StereoMatrixInvV[unity_StereoEyeIndex]` under single-pass
   instanced, per eye by construction. G0b: the string `GetCameraPositionWS` must not appear in
   any file under `Runtime/Shaders/`. **`_CS_CentreEyeWS` is a different thing and stays the
   centre eye**: compression happens about one point for both eyes (§2.5 rule 1), and making it
   per-eye is forbidden.
3. **The stereo skeleton, every macro, every time:**
   - `UNITY_VERTEX_INPUT_INSTANCE_ID` in `Attributes`;
   - `UNITY_VERTEX_OUTPUT_STEREO` in `Varyings`;
   - `UNITY_SETUP_INSTANCE_ID(v)` and `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)` first in the
     vertex function;
   - `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)` first in the fragment function. Without it the
     fragment sees the left eye's camera position in both eyes under multiview.
4. **Alpha.** Additive passes are `Blend One One, Zero One` and return alpha 0. Premultiplied
   "over" passes are `Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha` and write coverage equal
   to opacity; they are for physical absorbers and UI only. Opaque passes return alpha 1.
   `Blend SrcAlpha One` is **forbidden**: it adds alpha into passthrough. `ColorMask RGB` in the
   same pass is an accepted alternative to `, Zero One`.
5. `ZTest LEqual` everywhere. `ZTest Always` needs a `// ztest-justified: <reason>` comment
   (gated; the marker string is exactly `// ztest-justified:`, which is what the shipped
   `tools/gates/stereo_gate.sh:69` greps).
   **The depth-test consequence for enclosing volumes, stated** (R6): with `ZTest LEqual` on the
   volume's **back** face, an opaque surface nearer than that back face rejects the whole
   primitive, so the entire ray integral is lost — a body inside or in front of the dust box
   punches a body-shaped hole through the *whole* cloud, near half included. That is accepted and
   bounded in §13's "Known accepted limits"; what matters here is that **fixing it is not free**.
   It would cost `m_RequireDepthTexture 1` in the URP asset plus a depth prepass (a full-screen
   store and load per frame, render-xr-audit line 210 — the same class of surprise extra pass that
   produced the 1.7.0 failure), **and** every screen-UV lookup would have to go through
   `RemapFoveatedRenderingNonUniformToLinear`, because SRP foveation rasterizes non-uniformly
   (§8.1 sets `foveatedRenderingApi = SRPFoveation`). **No lane adds a depth-texture read
   casually**: it is an integrator decision with a G0c settings change.
6. All material properties go in `CBUFFER_START(UnityPerMaterial)` **and in the `Properties`
   block**, so the SRP Batcher works. Per-renderer values use **material instances**, not
   `MaterialPropertyBlock`s. **No matrix may be declared in `UnityPerMaterial`** (R4): ShaderLab
   has no matrix property type, so a matrix can never appear in `Properties`, which is the
   documented condition for batcher compatibility. Per-layer frames are baked (§3.4); a genuine
   per-material rotation uses three `Vector` properties.
7. **Colour.** The project is Linear. Vertex colours and hand-tuned constants are
   **display-referred (sRGB)** and are decoded exactly once with `CS_DisplayToLinear`. Material
   colours set with `SetColor` are linearised by Unity itself (INFERRED).
8. Sizes on screen are **angular (dmm = mrad)**, converted with the eye's own projection
   (`CS_AngularToClip`) and bounded in pixels where a floor matters. Pixel figures in this
   document come from `PixelsPerRadianCentre` = 1140 (§2.0), not from the panel average.
9. **No fragment function may read `unity_ObjectToWorld` or `unity_WorldToObject`** (R5), directly
   or through `TransformWorldToObject` / `TransformObjectToWorld`. Under the `INSTANCING_ON`
   variant those are arrays indexed by `unity_InstanceID`, which no fragment sets up, so the
   fragment reads matrix 0 — the wrong one the moment a layer is ever GPU-instanced. Object-space
   quantities are computed in the **vertex** function, where `UNITY_SETUP_INSTANCE_ID` has already
   run, and passed as varyings. Gated in G0b by grep.
10. **Normal maps are plain linear RGB** (R13). No asset in this project may be imported with
   `TextureImporterType.NormalMap`, because `CosmoSynth/Body` unpacks with
   `UnpackNormalRGBNoScale`; Unity swizzles and compresses NormalMap-typed textures (DXT5nm/ASTC
   with X in alpha) and the shader would read garbage — flat or inverted normals, no error, on the
   terminator, which is exactly what §7.4 asks a human to eyeball at G1d. Runtime-generated maps
   use `new Texture2D(w, h, format, mipChain, linear: true)`. G0 checks it with a grep for
   `textureType: 1` over `.meta` files.
11. **Every `CosmoSynth/*` shader declares `_Fade`** (R30, §4.3), defaulting to 1, in both
   `Properties` and `UnityPerMaterial`, multiplying the returned colour — and the alpha in
   premultiplied and opaque passes. It is the one visibility mechanism the layer framework uses.
12. **Stochastic terms are functions of the ray, never of the pixel** (R7, §2.5 point 7).

### 7.2 The eleven shaders

**Status: PROPOSED; none has been compiled.** They follow the URP pattern of the headset-proven
`GeoramaXR/.../GeoStar.shader` and `Worldfall/docs/CONTRACT.md` §4. The first job of the
`p0-shaders` helper lane is to write them; the integrator compiles each one in the one editor,
checks the stereo macros with G0b, and captures a frame.

`CosmoShaders.All` becomes exactly these eleven, and **removes the six built-in names it lists
today** (`Standard`, `Unlit/Color`, `Unlit/Transparent`, `Sprites/Default`,
`Particles/Standard Unlit`, `GUI/Text Shader`; VERIFIED at `CosmoShaders.cs:22-43`). The two
existing CG shaders are ported in place under their existing names. `CosmoShaders.Material()`'s
fallback at `:88` changes from `Standard` to `CosmoSynth/Unlit`.

| Name | File | Queue | Source | Verbatim HLSL |
|---|---|---|---|---|
| `CosmoSynth/Body` | `Body.shader` | 2000 | new; replaces `Standard` in `CelestialBodyView.cs:114-121, 442-453, 753-792` | Appendix A.3 |
| `CosmoSynth/Point` | `Point.shader` | 3050 (1010 in sky mode, set on the material) | new; replaces `Particles/Standard Unlit` and `StarSprite.cs:141-151` | Appendix A.4 |
| `CosmoSynth/Ribbon` | `Ribbon.shader` | 3060 | new; replaces `LineRenderer` + `Sprites/Default` (`OrbitLineRenderer.cs:42-88`) | Appendix A.5 |
| `CosmoSynth/Sky` | `Sky.shader` | 1000 | new | Appendix A.6 |
| `CosmoSynth/Volume` | `Volume.shader` | 3010 | new | Appendix A.7 |
| `CosmoSynth/Atmosphere` | `CosmoAtmosphere.shader` | 3100 | **port in place** of today's CG shader to this skeleton: same maths (`:111-237`), `CS_EyePositionWS()` (R1) read after the eye macro. Rigid mode passes the drawn centre and radii as today; Envelope mode (a uniform) integrates in true eye-relative space in units of the planet radius (§2.6) | port |
| `CosmoSynth/Rim` | `CosmoRim.shader` | 3100 | **port in place** of `CosmoRim.shader:30-89`, maths unchanged | port |
| `CosmoSynth/Ring` | `Ring.shader` | 3000 | new: `_BaseMap` (RGBA ring texture) times `_LightColor` times a planet-shadow term (an analytic ray-sphere test toward `_LightDirWS` against the parent's drawn sphere), premultiplied over | Appendix A.8 |
| `CosmoSynth/Unlit` | `Unlit.shader` | 2000 | new: opaque `_BaseColor` (beacon bars `AnchorBeacon.cs:65`, pointer ray `BodySelector.cs:70`) | Appendix A.8 |
| `CosmoSynth/Panel` | `Panel.shader` | 3200 / 3201 | new: `_BaseMap` alpha times vertex colour, premultiplied over (`CosmoTheme.cs:229,240`) | Appendix A.8 |
| `CosmoSynth/Text` | `Text.shader` | 3210 | new: font-atlas alpha times vertex colour (display-decoded), premultiplied over; used by `CosmoText.cs:78` instead of `GUI/Text Shader`. Legacy `TextMesh` stays, because its built-in font always ships (C3) | Appendix A.8 |

plus the shared include `Runtime/Shaders/CosmoCore.hlsl` (Appendix A.2), whose helpers are
`CS_EyePositionWS`, `CS_Length`, `CS_CompressDistance`, `CS_CompressWS`, `CS_IcrfDirToWorld`,
`CS_DisplayToLinear`, `CS_PixelsPerRadian`, `CS_AngularToClip`, `CS_JitterDir`. (`CS_IGN` is
removed: nothing in Appendix A used it after R7 moved the volume's jitter into ray space.)

**No `Resources` material templates.** Templates exist only to keep alive the keyword variants a
runtime-built scene would otherwise strip (the URP Lit trap, memory
`unity-quest3-passthrough-headless`). Every shader here is single-variant, so Always Included
plus `Shader.Find` through `CosmoShaders.Load` is sufficient and there is nothing to strip. **No
`Universal Render Pipeline/*` shader may be referenced at runtime** (gate G0d).

### 7.3 The Quest 3 per-frame budget at 72 Hz

The frame is 13.9 ms. **App GPU at most 11.0 ms** with SRP foveation at Medium and MSAA 4x.
Passthrough costs about 17 % GPU in the compositor. The global limits follow Meta's Quest 3
figures (200–300 draw calls for a busy app, 1.3–1.8 M triangles), taken conservatively:

| Global limit | Value |
|---|---|
| Draw calls | at most 150 |
| Vertices processed | at most 1.2 M (triangles at most 0.8 M) |
| Transparent fragments | at most 3.0 eye-screens in total; point sprites at most 0.5 |
| Raymarch | **at most 33 step-coverages** (one step over the area of both eyes): the shared volume budget **B = 30** plus **3** for body atmospheres and rings. The number is stated here and nowhere else; every `LayerBudget.StepCoverage` row below is drawn from it (R2) |
| Texture memory | at most 150 MB |

Allocation per layer, **INFERRED until measured**. Every row is a `LayerBudget` constant, and the
play-mode `budget` assertion sums the rows with `Weight > 0`. Windows are §5.4's.

| Layer | Draws | Vertices | Fragments (eye-screens) | Step-cov. | Texture MB | GPU ms | When active |
|---|---|---|---|---|---|---|---|
| `cmb` sky form (immersion > 0 only) | 1 | 2 k | 1.0 (opaque fill; 1 cube sample + 1 LUT) | 0 | 8.4 (6 x 1024^2 R8 + mips) + 0.5 mask | 0.5 | Space mode / BEYOND rungs |
| sky stars, distance unknown (**146 rows**, C12) | 1 | 584 | 0.002 | 0 | 0 | 0.02 | always |
| `stars` (about 120 k, chunked, cone-culled) | at most 64 (about 20 in view) | at most 480 k (about 100 k in view) | at most 0.5 | 0 | 0 | 1.2 | log10 U -19.6 ... +3.0 |
| `galaxiesLocal`, `galaxies2mrs`, `groupsCf4` (about 84 k) | at most 16 | at most 340 k | at most 0.3 | 0 | 0.1 (type atlas) | 0.8 | log10 U at most -17.8 |
| `globulars`, `cepheids`, `masers`, `belts`, `census` | at most 6 | at most 60 k | at most 0.1 | 0 | 0 | 0.2 | their windows |
| `dust` (raymarch) | 1 | 24 | at most 1.0 | **B = 30**, shared | 3.3–10.3 (R8 3D) | at most 2.5 | log10 U -19.0 ... -14.6 |
| `nebulae` (raymarch, R2) | at most 3 | 72 | at most 1.0 | **the same B = 30**, never simultaneously with `dust` | 0 (procedural fields) | at most 2.5 | log10 U -17.5 ... -13.5 |
| `dust` (voxel-sprite LOD) | at most 8 | at most 295 k | at most 0.9 | 0 | 0 | 1.0 | when the box is under 10 % of the screen, instead of the raymarch |
| Bodies: spheres, rings, atmospheres | at most 60 | **at most 180 k** (150 k plus the 16 k cap for the one Envelope-mode graded sphere, §2.6, R3) | atmospheres at most 0.5 | about 3 | planet maps at most 64 | 3.0 | always |
| Body markers (one batched mesh) | 1 | at most 1 k | 0.01 | 0 | 0 | 0.05 | always |
| Spacecraft models | at most 20 visible | at most 110 k (whole set) | — | 0 | 0 | 0.5 | above the 3 dmm model threshold |
| Ribbons (orbits, constellations, grids) | at most 16 | at most 300 k | at most 0.2 | 0 | 0 | 0.8 | |
| UI panels and text | **at most 6** (R9: one mesh per panel and one for all 24 world labels, §5.6.10) | at most 20 k | at most 0.3 | 0 | at most 4 (font atlas) | 0.8 | |
| **Totals (worst simultaneous)** | **at most 150** | **at most 1.2 M** | **at most 3.0** | **at most 33** | **at most 100** | **at most 9.8** (1.2 headroom) | |

Measure frame time with `adb logcat -d | grep VrApi` (the compositor's `FPS`, `App`, `GPU%` and
`Fov` fields). **`Fov=` is the only proof that foveation is really on.** The dust cost per
step-coverage on Quest 3 is unmeasured, so the step budget B = 30 needs a Fractarium-style cost
probe on device before it is trusted (§13). **Why one shared B and an arbitration rule** (R2): the
only device-measured anchor is Fractarium's **0.338 ms per step-coverage** on Quest 3
(quest-rendering-techniques §5.2, stated as an upper bound). Two enclosing volumes at coverage 1.0
would be 60 step-coverages = **20 ms** against an 11.0 ms app GPU budget; even at the optimistic
inferred `kappa_dust` = 0.03 it is 1.8 ms of pure margin lost. `dust` and `nebulae` windows
overlap over 2.9 decades and are owned by two lanes that cannot talk to each other (§9), so the
arbitration is a framework rule, not a lane's judgement: §4.3 lifecycle rule 4 keeps exactly one
`MayEnclose` volume, and the G1c `volumes` assertion fails the build if two ever draw.
**`basins` is not a volume**: §4.5 routes it through `CosmoSynth/Ribbon` as a watershed outline,
and `density` is per-point, so the CF4 layers need no step-coverage row (the earlier
"Bounded volumes (dust, CF4 basins)" in §2.2's queue table was stale and is corrected).

### 7.4 Colour space

- All authored constants stay **display-referred**: every `CosmoTheme` colour
  (`CosmoTheme.cs:34-75`) and every `StarLook` value (`FaintestBrightness 0.40`,
  `FaintestQuadDmm 3.8`, `BrightestQuadDmm 9.0`, `BrightnessFromMagnitude`; all VERIFIED). That
  is what they were measured as.
- Conversion happens at exactly two boundaries: `Material.SetColor` (Unity linearises; INFERRED)
  and vertex colours (decoded by `CS_DisplayToLinear` in every CosmoSynth shader). **Never call
  `.linear` on a vertex colour and also decode it in the shader.**
- `PlanetTextureBaker` creates normal and surface maps with `linear: true`; albedo maps stay
  sRGB.
- After the switch, the `stars` lane re-measures five reference stars with the pixel-neighbourhood
  method. Single-quad peaks should match the gamma-era numbers; overlapping quads (core, core,
  halo) now sum in linear and will read brighter. **Tune the one exposure knob `_Look.y` first.**
  Then re-capture the planet terminator and the atmosphere limb and adjust `_LightColor`
  intensity and the atmosphere's `_Intensity`.

---

## 8. Migration from today's Built-in code: every file

Line numbers are at `cf93a25`. Each row says what to change and which Phase-0 step owns it.

### 8.1 Rendering and XR configuration (through `XrSetup`, never by editing YAML)

1. **`Editor/XrSetup.cs`** (P0.4a)
   - **`EnsureLoader` (`:143`):** call `CreateDefaultManagerSettingsForBuildTarget` only when
     `perBuildTarget.SettingsForBuildTarget(Android)?.Manager == null`. Then remove the orphans:
     every `XRManagerSettings` sub-asset of `XRGeneralSettingsPerBuildTarget.asset` that no
     `XRGeneralSettings.Manager` references (4 today), using `AssetDatabase.RemoveObjectFromAsset`.
   - **`RequiredAndroidFeatures` (`:43-75`):** add `com.unity.openxr.feature.foveatedrendering`.
   - **`DisabledAndroidFeatures` (`:90-93`):** becomes
     `{ "com.unity.openxr.feature.input.handtracking" }` — the colliding Microsoft profile,
     enabled today at `OpenXR Package Settings.asset:1164-1166`.
   - **`EnsureRenderSettings` (`:268-285`)**, through the public `OpenXRSettings` API (verified
     to exist in `com.unity.xr.openxr@58837b2992e0/Runtime/Settings/OpenXRRenderSettings.cs` at
     `:38`, `:85`, `:230`, `:282`, `:405`, `:471`): `renderMode = SinglePassInstanced`,
     `depthSubmissionMode = DepthSubmissionMode.None`,
     `latencyOptimization = LatencyOptimization.PrioritizeInputPolling`,
     `symmetricProjection = true`, `optimizeBufferDiscards = true`,
     `foveatedRenderingApi = BackendFovationApi.SRPFoveation`. **MVPVV / Multiview Render Regions
     stays off** (C18).
   - **`MetaQuestFeature`:** its copies `m_symmetricProjection` and `m_optimizeBufferDiscards`
     are `internal` (`MetaQuestFeature.cs:109-141`). Set them to 1 through `SerializedObject` on
     the feature asset, the way `EnsureBothInputHandlers` writes `activeInputHandler`, and log
     both values.
   - **`EnsureUrp`** (new): port `~/projects/Worldfall/Assets/Worldfall/Editor/WorldfallUrpConfig.cs`
     `Create()` (288 lines, headset-proven).
     - Assets: `Assets/CosmoSynth/Settings/CS_URP.asset` and `CS_Renderer.asset`.
     - Renderer: `renderingMode = Forward`, `depthPrimingMode = Disabled`,
       `intermediateTextureMode = Auto`, `postProcessData = null`, **`rendererFeatures` empty**.
       A renderer feature that requires an intermediate colour texture forces one even under
       `Auto`, which on Quest is a full-screen store plus load per frame (render-xr-audit line
       210) — the same class of surprise extra pass that produced the 1.7.0 failure. Nothing
       re-checked `CS_Renderer.asset` before this revision; §11.2 now has a row for it and §11.3's
       `pipeline` assertion reads it back through the API (R11).
     - Asset expectations, set through `SerializedObject` exactly as Worldfall does: `m_MSAA 4`,
       `m_SupportsHDR 0`, `m_RequireDepthTexture 0`, `m_RequireOpaqueTexture 0`,
       `m_MainLightRenderingMode 0` (no Unity lights are used), `m_MainLightShadowsSupported 0`,
       `m_AdditionalLightsRenderingMode 0`, `m_AdditionalLightShadowsSupported 0`,
       `m_SoftShadowsSupported 0`, `m_UseSRPBatcher 1`, `m_SupportsDynamicBatching 0`,
       `m_EnableLODCrossFade 0`, `m_StoreActionsOptimization 0`, `m_RenderScale 1.0`,
       `m_ShadowDistance 0`.
     - Assign the asset to every quality level and to `GraphicsSettings.defaultRenderPipeline`.
       On the Android default level (index 2 today) set `pixelLightCount 0`, `shadows Disable`,
       `vSyncCount 0`, `antiAliasing 0`; the URP asset owns MSAA.
   - **`EnsureColorSpace`** (new): `PlayerSettings.colorSpace = ColorSpace.Linear`
     (`ProjectSettings.asset:49` becomes 1).
   - **`EnsureStaticBatchingOff`** (new, R12): `PlayerSettings.SetBatchingForPlatform(Android,
     staticBatching: 0, dynamicBatching: 0)` — `m_StaticBatching: 0` in
     `ProjectSettings.asset`, now a G0c row. Static batching bakes vertices into world space and
     would silently destroy the object-space raymarch, the ICRF-direction sky mesh and the
     Ribbon's `otherOS` endpoint. The `"DisableBatching"="True"` tags of §7.1 rule 1 are the
     belt; this is the braces.
   - **`EnsureFramePacing`** (new): `PlayerSettings.Android.optimizedFramePacing = false`, which
     removes Swappy and `libswappywrapper.so` (API name INFERRED; verify with the compile).
   - **`EnsureShadersIncluded` (`:343-394`):** as today, from `CosmoShaders.All`, **plus** remove
     the six former built-in entries.
   - **`DeleteOrphanXrAssets`** (new): `AssetDatabase.DeleteAsset` on `Assets/XR 1`, `XR 2`,
     `XR 3` (all VERIFIED present) and `Assets/XR/Settings 1..3` if empty, and on
     `Assets/XR/Resources/XRSimulationRuntimeSettings.asset` unless it is the registered one.
   - Log a one-line `[XrSetup] SUMMARY ...` holding every value above, which G0c greps, and print
     `XRSETUP_OK` on success.
2. **`Editor/ServerBuild.cs:36-37`** (P0.4e): `Version = "2.0.0-alpha.1"`, `VersionCode = 17`
   (today `"1.9.0"` / 16, VERIFIED). Pin
   `EditorUserBuildSettings.selectedBuildTargetGroup = Android`. Full spec in §10.5.
3. **`Runtime/CosmoSynth.Runtime.asmdef`** (P0.4a): drop the unused `Unity.XR.Interaction.Toolkit`
   reference (VERIFIED present) and keep `Unity.RenderPipelines.Universal.Runtime`, which
   `XrRig` now uses for camera data. **`tests/CosmoSynth.Runtime.Compile.csproj`**: add
   `Unity.RenderPipelines.Universal.Runtime.dll` and `Unity.RenderPipelines.Core.Runtime.dll`
   from `$(UnityScriptAssemblies)`; both are present (VERIFIED listing). **No TextMeshPro
   reference** (C3).
4. **`Runtime/XR/XrRig.cs`** (P0.4c)
   - `:71`: far becomes `ScaledSpace.FarPlaneMetres` (2000; today 5000, VERIFIED).
   - `:77-78`: delete the `DepthBands` attach.
   - after `:73`: the URP camera data of §2.1.
   - `:90-93`: delete the dead hand anchors.
   - `:96-97`: delete `RenderSettings.ambientMode`/`ambientLight` (no built-in lighting is used).
   - add a public `PassthroughClearColour`, used by the gates.
5. **Delete `Runtime/Rendering/DepthBands.cs`** (157 lines) and every reference:
   `ScaleRenderer.cs:28-30,51,106-109`; `PlayModeShot.cs:391-394,552-553,764-765,811-812`;
   `RenderDiagnostics.cs:32-34` (the "cams" comment).
6. **Delete `Editor/SceneShot.cs`** (417 lines). It builds its own Built-in scene, swaps axes
   itself (`:142-143`) and "proves nothing about the shipped app". Put a first-line pointer to
   `tools/shot.sh` in `~/server-setup/shot-cosmosynth.sh`, which is protected from deletion.
7. **Shaders** (P0.4b): add the eleven of §7.2 under `Runtime/Shaders/`, plus `CosmoCore.hlsl`.
   Port `CosmoAtmosphere.shader` and `CosmoRim.shader` in place. Rewrite `CosmoShaders.cs:22-59`
   to list the eleven, and change the `Material()` fallback at `:88`.

### 8.2 Placement, frames and the single mapping

8. **`Core/Frames/StandardFrames.cs`** (P0.3): the galactic fix at `:105-108`; add
   `GalacticToSupergalactic` and `EquatorialToSupergalactic` (§3.2).
   **`Core/Scale/ScaleSpace.cs`**: delete the band stack (`:28-78`, `:150-179`); extend
   `RenderTransform` (`:197-225`) with `AxisSwap` and the four mapping members (§3.1); raise
   `NavigationSpeed`'s ceiling to 1e24 km/s.
   **New**: `Core/Scale/ScaledSpace.cs` (§2.7), `Core/Scale/PointChunks.cs` (§3.4),
   `Core/Layers/{LayerTable,LayerState,LayerContext,LayerBudget,LabelCandidate}.cs`,
   `Core/Presentation/{ScaleLimits,PresentationPlane}.cs`, `Core/Nav/{TravelPlan,Destination}.cs`
   (stubs at P0.2, filled by `nav`), `Core/Data/{CsData,DataManifest}.cs`,
   `Core/Deep/{VoxelGrid,PointSet}.cs`, `Core/Catalog/CatalogAssembler.cs`.
9. **The single `SetCatalog`** (P0.4d):
   - The bootstrap no longer reads files itself. `DataLoader` reads through
     `StreamingAssetReader`, and `CatalogAssembler` merges `solar_system.json` and
     `spacecraft.json`. The runtime calls **one** `Simulation.SetCatalog(assembled)`.
   - Exoplanets are **not** in that merge (C10), so `RootIds[0]` is `sun` again.
   - Delete `UniverseSimulation.MergeCatalogFromJson` (`:267-293`), `LoadCatalogFromJson`
     (`:243-257`), `LoadCatalogFromStreamingAssets` (`:211-240`) and the two readers
     (`:104-202`), and `CosmoSynthBootstrap.LoadCatalogRoutine` (`:193-231`). All VERIFIED
     present.
   - `OnCatalogLoaded` (`:233-275`) now runs exactly once. Keep its clear-before-create code and
     add a counter the play-mode `catalog` assertion requires to equal 1.
   - Later additions keep using `AddBodies`/`RemoveBodies` (`:334-379`), which also raise the new
     `BodiesChanged` event.
10. **`Runtime/Universe/FloatingOrigin.cs`** (P0.4c): rewrite as §3.1; delete the rebase
    machinery and the identity mapping (`:78-86`); `LateUpdate` is deleted (the presentation
    plane is the host's rotation, owned by its driver); `SetUnitsPerKm` clamps with
    `ScaleLimits`.
11. **`Runtime/Universe/CelestialBodyView.cs`** (P0.4c)
    - Split `UpdateState` and `Place` (§2.8).
    - Delete `PlaceForRendering` and the horizon pinning (`:200-254`);
      `UniverseSimulation.StarPointHorizonUnits` (`:436`) goes with it.
    - `UpdateVisualScale` (`:256-294`) multiplies by `g.Scale` and applies the 2.5 px floor as
      "draw as point".
    - `UpdateStarPointScale` (`:311-358`) becomes `PointState` (visible, placed position,
      apparent magnitude, colour, dmm override, kind), consumed by the new `BodyMarkers`.
    - Rotation (`:194`) becomes `origin.MapRotation(...)`.
    - Delete the statics (`:898-917`).
    - Materials (`:114-121`, `:442-453`, `:753-792`) become `CosmoSynth/Body`: `_Glossiness` to
      `_Smoothness`; `_METALLICGLOSSMAP` + `_MetallicGlossMap` to `_SurfaceMap`; `_NORMALMAP` +
      `_BumpMap` to `_NormalMap`, always bound (the default is `bump`); `_EMISSION` +
      `_EmissionColor` to `_Emission`. **There are no keywords any more.** The metallic clamps
      (`:478`, `:488`) become irrelevant; delete them with their comments.
    - `RefreshRendererEnabled` (`:850-854`) computes
      `IsGeometryDrawn = IsKnown && !DrawnAsPoint && !_modelSuppressed`. `DrawnAsStarPoint` is
      renamed `DrawnAsPoint` everywhere (grep); `IsMarkerDrawn` and `IsPointable` keep their
      meanings.
12. **Host-star lighting through the parent chain** (P0.4d)
    - Core `CelestialCatalog.HostStarOf(string id)`: walk `ParentId` to the first `IsStar`, at
      most 32 hops; null for rogue or unknown bodies; a star returns itself. Tests: `moon` to
      `sun`; any `exo-*` planet to its host; a spacecraft on Mars to `sun`; a synthetic parent
      cycle terminates. This one function replaces the four duplicated "walk up to the star"
      loops.
    - Delete `SkyAndRings.BuildStarLight` (`:581-637`), which contains the `RootIds[0]` defect at
      `:585` (VERIFIED), and the ambient directional light.
    - `ScaleRenderer` sets, per drawn body per frame:
      `_LightDirWS = universe.TransformDirection(origin.MapDirection((star - body).Normalized))`;
      `_LightColor = BlackBodyColor(T_star) * StarLook.StarLightIntensity`, a new constant
      starting at 0.85 (today's `:607`), to re-measure; `_Ambient = CosmoTheme.AmbientFill`, a new
      token starting at (0.05, 0.055, 0.07) (today's `XrRig.cs:97`). The atmosphere's
      `_SunDirection` reads the same vector.
13. **Every ad-hoc axis swap is deleted** (P0.4c)
    - `SkyAndRings.cs:220`: the shell. **Interim path until `stars` lands `StarLayer`**: rebuild
      the existing shell as a `CosmoSynth/Point` mesh in sky mode (`_Space = 2`) from
      `star.Direction` in ICRF with **no** swap; `_Space = 2` rotates with the **global**
      `_CS_IcrfToWorld` and there is no per-material matrix (R4). That also
      removes the anchor-parented 900-unit sphere (`:29`, `:407-447`).
    - `XrLocomotion.cs:150-151`:
      `origin.UnmapDirection(universe.InverseTransformDirection(worldDir))`.
    - `UniverseNavigator.cs:144,186`: the same, through `FloatingOrigin`.
    - `UniverseNavigator.cs:202`: place the camera with `universe.TransformPoint(local)`, not a
      raw universe-local value.
    - `SceneShot.cs:142-143`: deleted with the file.
14. **`Runtime/XR/TabletopManipulator.cs`** (P0.4c): the universe-local hand conversion, the
    sticky-target clear, `_minimumUnitsPerKm`/`_maximumUnitsPerKm` deleted in favour of
    `ScaleLimits`, and never touching the presentation plane — all §3.6.
15. **`Runtime/Rendering/RingSystem.cs:26-37`**: if `!_body.IsGeometryDrawn`, disable the
    renderer and return. `[DefaultExecutionOrder(60)]`. The ring material becomes
    `CosmoSynth/Ring` (`SkyAndRings.cs:514-516`).
16. **`AtmosphereShell`, the per-orbit ribbons, the inspector card, the `BodySelector`
    highlight**: `[DefaultExecutionOrder(60)]`.
17. **`Runtime/Rendering/OrbitLineRenderer.cs`**: keep the sampling and the legibility cull;
    replace the `LineRenderer` (`:42-88`, material `:82`, width `:235`) with a
    `CosmoSynth/Ribbon` mesh; the transform comes from the owner group (`_Placement = 0` rigid,
    `= 1` when the owner contains the eye); delete `OnOriginShifted` (`:327-335`).
18. **`StarSprite.cs`**: delete (the Gaussian is analytic in `CosmoSynth/Point`).
    **`StarPointBillboard.cs`**: replaced by one new `Runtime/Rendering/BodyMarkers.cs`
    (MonoBehaviour), a single dynamic mesh rebuilt each frame from every view's `PointState`
    (`_Space = 1`). That turns up to 240 draws into 1.
19. **`Runtime/Rendering/ScaleRenderer.cs`**: rewrite `LateUpdate` (`:55-110`) as the order-50
    step of §2.8. Delete `BodiesByApparentSize` and `_sorted` (`:36`, `:112-117`, dead).
20. **`UniverseSimulation.cs`**: `LateUpdate` (`:438-451`) computes state only; `NearestBody`
    (`:504-525`) also asks `LayerRegistry.All[i].NearestSurfaceKm`; add the `BodiesChanged`
    event.

### 8.3 Colour space, UI, diagnostics

21. **Linear re-tune**: §7.4.
22. **UI** (P0.4d): `CosmoTheme.cs:229,240` to `CosmoSynth/Panel`; `CosmoText.cs:78` to a
    `CosmoSynth/Text` material sampling the built-in font's atlas (C3);
    `AnchorBeacon.cs:65` and `BodySelector.cs:70` to `CosmoSynth/Unlit`; `BodySelector.cs:108` to
    `CosmoSynth/Rim`. **`ProvenanceHud` is deleted** and `CosmoSynthBootstrap.cs:171` with it
    (C13); a minimal status card (`AnchorBeacon` plus `CosmoText`) shows the `render:` line and
    the per-file data status from frame 1, and `ui` replaces it with §5.10's card in 1b. The two
    `CosmoTheme` token fixes of §5.6.9 land here.
23. **`RenderDiagnostics.OneLine` (`:39-57`)** becomes
    `render: URP|BiRP <mode> WxH:<layers> msaa <urp>/<eyeSamples> cams <n> clr SC/a=<a> dep <depthStencilFormat> sub <submission> cs <Linear|Gamma> fov <level> ppr <pixelsPerRadian> grp <rigid>/<envelope>/<enclosing> push <conflicts> vol <volumeOcclusion> far <max rendered m>`
(`ppr` is R8's measured `PixelsPerRadian`; `vol` is R6's count of drawn bodies inside or in front
of an active enclosing volume).
    It is logged once to logcat, as today, and shown in INFO -> STATUS. **Red conditions:**
    `cams != 1`, `a != 0` in passthrough, a 16-bit depth format, Gamma, BiRP, `push > 0`,
    `vol > 0`.
24. **`Editor/PlayModeShot.cs`**: §10.6.
25. **Docs** (the same change as the code): `CLAUDE.md` "XR / Quest configuration" (Built-in to
    URP, the mapping, the test count); `docs/architecture.md:137-139,233` (bands to groups);
    `docs/data-sources.md:177` (ESA is CC BY-NC 3.0 IGO); `docs/feedback.md` (the 1.7.0 grid
    report recorded verbatim); and the `ScaleSpace`/`DepthBands` comments that describe a float
    depth buffer the device does not have.
---

## 9. Process: phases, lanes, ownership

Authority: Process section D. On process, this part wins over everything else.

### 9.1 Phase 0, step by step (integrator unless stated)

| Step | What | Editor | Done when | Effort (INFERRED) |
|---|---|---|---|---|
| **P0.0** | Housekeeping: (a) commit the pending `docs/feedback.md` (it already holds the 1.7.0 grid report verbatim, VERIFIED by `git diff`) together with `docs/research/` and `docs/design/` and this `docs/CONTRACT.md`; (b) confirm `~/builds/cosmosynth/recovered/CosmoSynth-release.apk.KEEP` exists (it does, VERIFIED, 176 B); (c) move `v1.7.2__01-msaa-reverted-plus-render-readout.png` (a byte-copy of v1.7.0's shot), the 9 untagged PNGs and the 5 `.report.txt` from `~/builds/shots/cosmosynth/` into `~/builds/cosmosynth/diagnostics/pre-2.0/` with a `README.txt` line saying why; (d) add `unity/.utmp/` to `.gitignore` (VERIFIED absent) then `git rm -r --cached unity/.utmp`; (e) write the **P** handoff (§12.6) asking the user to test the recovered 1.9.0; (f) retire `~/server-setup/*-cosmosynth.sh` (§10.8) | no | one commit; the gallery holds only the tagged shots; the handoff is written | 1 h |
| **P0.1** | **Tooling *reconciliation*, not a port** (R21). Most of §10 already exists on master: `git log --oneline cf93a25..HEAD` shows `49741aa Merge lane/tooling: build, capture and gate scripts with self-testing gates`, and `tools/` holds `build.sh common.sh editor-method.sh gate-all.sh lane.sh shot.sh verify-apk.sh xr-baseline.txt cosmodata/ gates/`, with `gates/` holding `class_gate.sh code_gate.sh g0.sh known-red.txt qsettings_scan.py settings_gate.py stereo_gate.sh test-floor.txt` (all VERIFIED on triton at `fc07a52`). **Do not recreate any script that already carries a passing `--self-test`.** The work is: **(a)** `common.sh:21` `FONT_ASSET="LiberationSans SDF"` becomes `FONT_BUILTIN="LegacyRuntime.ttf"`, and its consumers in `verify-apk.sh` (the `font` check at about `:349` and the **`font-tmp` / `TMP_SDF-Mobile` check right below it**, both VERIFIED present) become the §11.4 form — with C3 dropping TMP, neither can ever pass on a 2.0 build as written. **(b)** Add the four missing gates `data_gate.py`, `drop_test.sh`, `binding_gate.py`, `own_gate.py` and the control files `data-budget.txt` (50) and `log-allowlist.txt`. **(c)** Add `tools/shotname.py`, `tools/lanes.txt`, `tools/phase.txt`. **(d)** Implement `lane.sh merge` (today a stub that prints "Not implemented here" and exits 2, `lane.sh:92-95`). **(e)** Extend `code_gate.sh` to scan `U/Core/**` as well as `U/Runtime/**` (it scans Runtime only today, `code_gate.sh:18`), and add the new G0b/G0d rules of §11.1. **(f)** `run-tests.sh` already has all three §10.7 additions (ScriptAssemblies fallback to main, `mktemp` log, per-tree `flock`, `nice -n 10`, the Editor.Compile hook) at `:60-100`, VERIFIED — **nothing to do there**. **(g)** Keep `gate-all.sh` as the implementation and `g0.sh` as its one-line name shim. Then every gate's `--self-test`, then the probe: `tools/lane.sh add probe`, `./run-tests.sh` inside it (must print 142 passed, 0 failed), `tools/lane.sh rm probe` | no | self-tests pass; the worktree probe passes; `known-red.txt` lists `G0c G0d G0g` | 3 h |
| **P0.2** | Contracts: (a) `tools/lanes.txt` (§9.3) and `tools/phase.txt` = `1a`; (b) the `docs/data-sources.md` rewrite with one `lane:<name>` slot each (C21, Appendix D); (c) the shared importer modules `TC/{csdata,fetch,fits,licence,ledger,build_authored}.py` with `csdata.py --selftest` and its committed fixture (C7), **and one `# lane:<name> BEGIN/END` pair per lane inside `build_authored.py`'s authored-file table** (§6.9.1, §9.4) — `build_authored` was in §9.3's integrator list but missing from this step, and `milkyway`, `galaxies` and `fiction` must register authored JSON on day one of 1a (R25); (d) `Core/Data/{CsData,DataManifest}.cs` + tests; (e) the shared Core stubs with tests: `Core/Deep/{VoxelGrid,PointSet}.cs`, `Core/Layers/{LayerTable,LayerState,LayerContext,LayerBudget,LabelCandidate}.cs`, `Core/Nav/{TravelPlan,Destination}.cs`, `Core/Presentation/{ScaleLimits,PresentationPlane}.cs`, **and `Core/Compat/IsExternalInit.cs` containing exactly `namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }`** — without it not one of the five shared `record` types compiles, and P0.2 is the commit every lane branches from (R18); (e2) `own_gate.py --audit` over `tools/lanes.txt` must print no equal-specificity collision (R23); (f) commit, tag `phase0-contracts`, then `tools/lane.sh add` for the 8 Phase-1a lanes and the helpers `p0-core` and `p0-shaders` | no | the tag exists; the worktrees are listed; the preamble (Appendix F) is handed to each lane agent | 4 h |
| **P0.3** | Core foundation, in the `p0-core` worktree: `Core/Scale/ScaledSpace.cs` + `ScaledSpaceTests` (§2.7); `RenderTransform` extended with P (§3.1) and the band stack deleted; the `StandardFrames` galactic fix + `GalacticToSupergalactic` + `FrameTests` (§3.2, C5); `CelestialCatalog.HostStarOf`; `Core/Scale/PointChunks.cs`; `ScaleLimits` and `PresentationPlane` filled in; `NavigationSpeed`'s ceiling | no | `./run-tests.sh` green; merged into main | 0.5–1 d |
| **P0.4a** | `XrSetup` extensions in full: §8.1 item 1, plus `Runtime.Compile.csproj`'s URP and RP-Core references and the asmdef change (§8.1 item 3). **No TMP** (C3) | yes | G1a green; the settings YAML shows every value | 0.5 d |
| **P0.4b** | Shaders, in the `p0-shaders` worktree: the eleven of §7.2 and `CosmoCore.hlsl` under the Appendix A names; the integrator rewrites `CosmoShaders.All` and replaces every built-in `Shader.Find` | yes, for the compile | G0b green; G1b has no shader errors | 0.5–1 d |
| **P0.4c** | One camera and scaled space: §8.1 items 4–6, §8.2 items 10, 11, 13, 14, 15, 16, 17, 18, 19, 20, plus `Placement`, `FrameContext`, `BodyMarkers`, decoration order 60, the orbit ribbons and the interim sky-mode star shell. `PlayModeShot`'s compositing and assertions change in the same commit | yes | G0d green; G1c green with the new assertions | 1–1.5 d |
| **P0.4d** | The single `SetCatalog` (§8.2 item 9, C10), host-star light (§8.2 item 12), the text stack and the two theme tokens (§8.3 item 22, C3, C13), the `render:` line (§8.3 item 23), the Runtime data half `Runtime/Data/{StreamingAssetReader,DataLoader}.cs` and the first generated `manifest.json` over today's files. **In one commit** it also deletes the exoplanet merge, `unity/Assets/StreamingAssets/CosmoSynth/catalog/exoplanets.json` **and `tests/CosmoSynth.Core.Tests/ExoplanetCatalogTests.cs`** (7 test methods that read the deleted file directly at `:28` and error at `:35`, VERIFIED on triton — `ShippedCatalogTests.cs` has no exoplanet reference at all, so the earlier C10 text named the wrong file, R27), **lowers `tools/gates/test-floor.txt` to the new passing count** and records the reason in `docs/reports/integrator.md` | yes | G1c `text` and `glyph` assertions green; `catalog` asserts 1; G0a green against the lowered floor | 0.75 d |
| **P0.4e** | `ServerBuild`: §10.5 | yes | G1b `DIAGNOSTICS_OK` | 0.25 d |
| **P0.5** | Empty `known-red.txt`; full G0 and G1a–G1e green on master at the seven fixed scales of §2.6 | yes | all green | 0.5 d |
| **P0.6** | `tools/build.sh "foundation"` builds 2.0.0-alpha.1 (G2), verifies it (G3), publishes it (G4); then write the G5 handoff (§12.6) | yes | `RESULT=OK`; the handoff is written | 0.5 d |
| **P0.7** | Hook kit for 1b: seed the code slots (§9.4); `CosmoLayer` + `LayerRegistry` (§4.1) and the `LayerTable`/`LayerState` skeleton with the `SelfCheck()` hook; the generic `XrInput` claim API (`ClaimStick(hand)`, `PanelHot(hand)`); set `tools/phase.txt` to `1b`; tag `phase0-done` and move `green` there | yes (compile) | G0 + G1b green; the tags are set | 0.5 d |

**Phase 0 total: about 4–6 days of wall clock** (INFERRED). Each Unity launch on this box takes
minutes under a load average of about 70 on 12 threads, and P0.4 needs 10–20 launches. Commit
after every sub-step so the history can be bisected. Commits inside P0.1–P0.4 may be red; `green`
first appears at P0.5.

**If gate P or the alpha's G5 reports a render failure** (grid, black, flat, one-eyed): 1b
runtime merges freeze. Lanes keep working, compile-only. The integrator and the Foundation owner
diagnose from the `render:` line.

**Rule: the alpha is the foundation only.** No Phase-1a data merges into master before
2.0.0-alpha.1 is published. The user's first headset look is an A/B between 1.9.0 (Built-in, two
cameras) and the alpha (URP, one camera); changing the star catalogue at the same time would
confound it.

### 9.2 Phases 1a, 1b, 2, 3

**Phase 1a** (data and Core, never an editor, starts at `phase0-contracts`). Eight lanes, each
touching only `TC/`, `SA/`, `Core/`, `T/`, its own report and its `data-sources.md` slot. When
`tools/phase.txt` says `0` or `1a`, `lane.sh check` rejects anything else.

| Priority | Lane | Why in this order |
|---|---|---|
| 1 | `stars` | the user's first rung past the Solar System; AT-HYG and the constellations also feed `lines` |
| 1 | `cmb` | the end of the ladder; the numpy bake wants the bake lock early |
| 1 | `galaxies` | the Local Group and "beyond", which the user named explicitly |
| 1 | `milkyway` | the dust cube is the riskiest bake |
| 1 | `solar` | the biggest data lane (Moon, secular elements, SBDB, heliophysics, spacecraft, five of the ten gaps) |
| 1 | `nav` | Core only: `TravelPlan`, destinations, the calendar range, observing sites |
| 2 | `exoplanets` | small |
| 2 | `fiction` | the content is on the laptop and on triton; copying it takes time, not CPU |

**Concurrency cap: 6 lane agents plus the integrator** (INFERRED from box load, not disk: eight
worktrees cost under 0.5 GB). Priority-2 lanes start when a priority-1 lane reaches `READY-1A`.
**1a merges wait for P0.7**, then go FIFO by `READY-1A` time, in batches of at most 3.

**Additive rule for 1a merges.** A 1a merge must not remove, or change the format of, any file
the current runtime reads. Replacing a format happens in the lane's 1b half, together with its
reader. `stars` may swap `sky/stars.csstars` from CSS1 to CSS2 because it rewrites `StarCatalog`
in the same half. **Exoplanets are the one exception** (C10): P0.4d has already removed the
reader and the file.

**Phase 1b** (runtime), in three waves of at most 4 lanes:

| Wave | Lanes | Opens when | Why |
|---|---|---|---|
| A | `nav`, `ui`, `stars`, `lines` | P0.7 done and the 1a halves of `stars` and `nav` merged | they touch the most shared input and UI files and set the look of the base experience; `lines` needs the constellation data from `stars`' 1a half |
| B | `cmb`, `galaxies`, `milkyway`, `solar` | two wave-A lanes merged | the ladder: CMB, then the Local Group and beyond, then the Galaxy, then the Solar System decorations |
| C | `exoplanets`, `fiction` | two wave-B lanes merged | lowest coupling |
| Phase 3 | `audio` | after Phase 2 | sonification |

**Why the cap is 4** (INFERRED): every 1b lane needs captures through the single editor queue
(one Unity launch per commit, 5–10 min each on this box), and every 1b merge costs one G1b and
one G1c editor run. The integrator serves about 20–30 editor runs a day.

**Phase 2:** merge the last 1b lane; full G0 and G1; bump to `2.0.0-beta.1` (next code); run
`tools/build.sh`; G5 handoff with the complete checklist; fix blockers on master or in a
short-lived lane; build `2.0.0-beta.N`. When G5 reports no blocker: bump to `2.0.0`, build,
publish, `.KEEP` it, and move the xr baseline only if it changed **and** the headset confirmed
it.

**Phase 3** (each item its own small lane or an integrator task): the frame budget, which needs a
device measurement (`adb logcat | grep VrApi`, or OVR Metrics); re-tuning StarLook, the galaxy
look and the dust look under Linear; the `audio` lane; docs (CLAUDE.md, README,
`docs/architecture.md`); slot collapse once no lanes remain; deferred data G6 and G8; and store
readiness **only if the user asks** (D2, D3): a release keystore, targetSdk 34, `STORE=1
verify-apk.sh`.

**Capacity plan** (snapshot VERIFIED 2026-09-11 about 19:10; the rest INFERRED):

| Resource | Today | CosmoSynth's use | Guard |
|---|---|---|---|
| Disk `/` | **9.3 GB free (98 %)** | worktrees 15–50 MB each; raw downloads peak at about 0.3 GB and are deleted after baking; `SA/` grows by 20–35 MB per tree that holds it; one APK is 55–80 MB, staged then moved; Library growth after URP 0.5–2 GB; IL2CPP transient 1–3 GB; **G0f's drop test about 100 MB under `$TMPDIR` per run** (R36) | editor runs need at least 5 GB free; builds at least 8 GB; downloads over 50 MB at least 6 GB. **If a guard refuses, stop and report. Never delete other projects' files.** |
| RAM | 62 GB, 40 GB available, **swap 7/7 GB** | editor 0.7–4 GB; IL2CPP peak unmeasured (INFERRED 4–8 GB); dotnet test builds up to 1.5 GB each; numpy bakes up to 1 GB | editor runs need `MemAvailable` at least 6 GB; builds at least 10 GB; **one numpy bake at a time** (`flock /tmp/cosmosynth-bake.lock`). The first build logs its peak RSS and the thresholds are then tuned |
| Unity editors | about 10 others on the box | **exactly 1 for CosmoSynth** | `editor_lock` plus a `pgrep` foreign-editor check |
| CPU | load about 70 on 12 threads | dotnet and python under `nice -n 10` | none; slow is acceptable |

**Effort, INFERRED agent-days per lane half (1a / 1b):** `stars` 1.0 / 2.0; `milkyway` 1.5 / 2.5;
`galaxies` 2.0 / 2.0 (it absorbed B's `data-lv` and `data-lss`); `cmb` 1.0 / 1.0; `solar` 2.5 /
2.5 (it absorbed five gaps); `exoplanets` 0.5 / 1.0; **`fiction` 1.5 / 5.0** (raised from 3.0: it
now also owns `StageDirector` and both sandboxes, R38); `nav` 0.75 / 2.5; `ui` — / 3.0;
`lines` — / 1.5; `audio` 1.5 in Phase 3. About **38 agent-days**, about 3 weeks of wall clock.

### 9.3 Ownership rules and `tools/lanes.txt`

1. **Every tracked path has exactly one owner**: the integrator (the default for anything not
   claimed) or one lane. **Globs may overlap; the most specific match wins** (R23) — an explicit
   file path beats a `**` glob, and a longer literal prefix beats a shorter one.
   `own_gate.py --audit` prints every path matched by two lanes **at equal specificity** and exits
   1; the integrator runs it at P0.2 and it is a P0.2 completion condition. Without the rule,
   `lane.sh check` passes for both claimants of a file and the collision surfaces only as a merge
   conflict.
2. **Non-owners may edit a file only inside their own seeded slot.** A slot is a
   `lane:<name> BEGIN` / `lane:<name> END` marker pair on its own lines: `// lane:stars BEGIN`
   (C#), `<!-- lane:stars BEGIN -->` (Markdown), `# lane:stars BEGIN` (Python). The lane inserts
   or edits lines strictly between its own two markers. It never edits, moves or deletes a marker
   line, and never touches another lane's slot.
3. **Only the integrator creates slots**, one pair per lane per registration point, in
   `lanes.txt` order, with each END and the next BEGIN on consecutive lines. A lane that needs a
   new registration point asks in its report; the integrator seeds it on master and moves
   `green`.
4. **Slot position carries no meaning.** Render order, menu order and binding priority come from
   explicit fields (a `LayerTable` column, a queue number), never from where a line sits. One
   statement or row per line, with trailing commas in C# initialisers.
5. **Never create `.meta` files.** The integrator's editor generates them at merge time and they
   are committed in the merge commit.
6. **Integrator-only files:** `unity/ProjectSettings/**`, `unity/Packages/**`,
   `unity/Assets/XR/**`; every `.meta`, every `*.csproj`, `tests/**/TestHarness.cs`,
   `run-tests.sh`; `tools/*.sh`, `tools/gates/**`, `tools/lanes.txt`, `tools/phase.txt`,
   `tools/xr-baseline.txt`, `TC/{csdata,fetch,fits,licence,ledger,build_authored}.py`;
   `CLAUDE.md`, `README.md`, `docs/*.md` except `accuracy.md` (owned by `solar`, with one
   `lane:<name>` slot per lane inside it from P0.2, R24) and the `data-sources.md` slots; `docs/design/**`, `docs/CONTRACT.md`, `docs/feedback.md`;
   `unity/Assets/CosmoSynth/Editor/**`; and the foundation runtime files named below. Lanes put
   requested changes to these in their report.

**The slot protocol, VERIFIED on triton's git 2.43.0** (a throwaway probe, since deleted): three
branches inserting lines into three adjacent `lane:*` slots of one C# array all merged CLEAN and
the result held every insertion in its own slot; inserts separated by one unchanged line merged
CLEAN; the control case, two branches inserting after the same line, CONFLICTED as expected.

```
# tools/lanes.txt — the machine-readable claims; own_gate.py reads it.
# Everything unclaimed belongs to the integrator. Globs are repo-relative; ** is recursive.
# A "1b:" glob is owned only while tools/phase.txt is 1b or later.
# Globs may overlap. The MOST SPECIFIC match wins: an explicit file path beats a ** glob, and a
# longer literal prefix beats a shorter one. own_gate.py --audit prints every path claimed by two
# lanes at equal specificity and exits 1; the integrator runs it at P0.2 (R23).
#
# While phase.txt is 0 or 1a, own_gate additionally restricts every lane whose phases include 1a
# to tools/cosmodata/**, unity/Assets/StreamingAssets/**, unity/Assets/CosmoSynth/Core/**,
# tests/**, docs/accuracy.md (inside the lane's own slot), and its own report and data-sources
# slot. docs/accuracy.md is in the list because two lanes are told to write it during 1a and it
# was absent, so lane.sh check would have rejected both commits with no way to comply (R24);
# it gets one `<!-- lane:<name> BEGIN/END -->` pair per lane, seeded at P0.2 beside
# data-sources.md, and `solar` owns the file outside every slot. Phase-0 helpers (phases "0") are
# held to their explicit globs only, and own_gate refuses them once phase.txt is past 1a.
#
# lane        phases  owned globs
p0-core       0       unity/Assets/CosmoSynth/Core/Scale/ScaledSpace.cs unity/Assets/CosmoSynth/Core/Scale/ScaleSpace.cs unity/Assets/CosmoSynth/Core/Scale/PointChunks.cs unity/Assets/CosmoSynth/Core/Frames/StandardFrames.cs unity/Assets/CosmoSynth/Core/Catalog/CelestialCatalog.cs unity/Assets/CosmoSynth/Core/Presentation/ScaleLimits.cs unity/Assets/CosmoSynth/Core/Presentation/PresentationPlane.cs tests/CosmoSynth.Core.Tests/ScaledSpaceTests.cs tests/CosmoSynth.Core.Tests/FrameTests.cs tests/CosmoSynth.Core.Tests/PointChunksTests.cs
p0-shaders    0       unity/Assets/CosmoSynth/Runtime/Shaders/**
stars         1a,1b   tools/cosmodata/build_starfield.py unity/Assets/StreamingAssets/CosmoSynth/sky/** unity/Assets/CosmoSynth/Core/Sky/StarCatalog.cs unity/Assets/CosmoSynth/Core/Sky/StarSystemFactory.cs unity/Assets/CosmoSynth/Core/Sky/Constellations.cs unity/Assets/CosmoSynth/Core/Sky/Photometry.cs unity/Assets/CosmoSynth/Core/Sky/StarQuery.cs tests/CosmoSynth.Core.Tests/StarCatalogTests.cs tests/CosmoSynth.Core.Tests/StarSystemFactoryTests.cs tests/CosmoSynth.Core.Tests/Star*Tests.cs tests/CosmoSynth.Core.Tests/ConstellationTests.cs tests/CosmoSynth.Core.Tests/PhotometryTests.cs unity/Assets/CosmoSynth/Runtime/Layers/Stars/** unity/Assets/CosmoSynth/Runtime/Rendering/StarLook.cs 1b:unity/Assets/CosmoSynth/Runtime/Rendering/SkyAndRings.cs 1b:unity/Assets/CosmoSynth/Runtime/Universe/NearbyStarSystems.cs
milkyway      1a,1b   tools/cosmodata/build_dust.py tools/cosmodata/build_galaxy_points.py tools/cosmodata/authored/landmarks.json unity/Assets/StreamingAssets/CosmoSynth/galaxy/** unity/Assets/CosmoSynth/Core/Deep/DustCube.cs unity/Assets/CosmoSynth/Core/Deep/GalaxyModel.cs unity/Assets/CosmoSynth/Core/Deep/PlaceCatalog.cs tests/CosmoSynth.Core.Tests/DustCubeTests.cs tests/CosmoSynth.Core.Tests/GalaxyModelTests.cs tests/CosmoSynth.Core.Tests/GalacticPointsTests.cs tests/CosmoSynth.Core.Tests/PlaceCatalogTests.cs unity/Assets/CosmoSynth/Runtime/Layers/MilkyWay/**
galaxies      1a,1b   tools/cosmodata/build_localvolume.py tools/cosmodata/build_lss.py tools/cosmodata/authored/structures.json tools/cosmodata/authored/overrides.json tools/cosmodata/authored/giants_fallback.json unity/Assets/StreamingAssets/CosmoSynth/lv/** unity/Assets/StreamingAssets/CosmoSynth/lss/** unity/Assets/CosmoSynth/Core/Deep/GalaxySet.cs unity/Assets/CosmoSynth/Core/Deep/GalaxyOrientation.cs unity/Assets/CosmoSynth/Core/Scale/Cosmology.cs tests/CosmoSynth.Core.Tests/LocalVolumeTests.cs tests/CosmoSynth.Core.Tests/GalaxyOrientationTests.cs tests/CosmoSynth.Core.Tests/RedshiftSurveyTests.cs tests/CosmoSynth.Core.Tests/Cf4GridTests.cs tests/CosmoSynth.Core.Tests/CosmologyTests.cs tests/CosmoSynth.Core.Tests/StructuresTests.cs unity/Assets/CosmoSynth/Runtime/Layers/Galaxies/**
cmb           1a,1b   tools/cosmodata/build_cmb.py tools/cosmodata/healpix_nest.py unity/Assets/StreamingAssets/CosmoSynth/cmb/** unity/Assets/CosmoSynth/Core/Cmb/** tests/CosmoSynth.Core.Tests/CmbSkyTests.cs tests/CosmoSynth.Core.Tests/CmbModelTests.cs unity/Assets/CosmoSynth/Runtime/Layers/Cmb/**
solar         1a,1b   tools/cosmodata/build_solar_system.py tools/cosmodata/horizons.py tools/cosmodata/bodies.py tools/cosmodata/fetch_validation_fixture.py tools/cosmodata/build_spacecraft.py tools/cosmodata/spacecraft.py tools/cosmodata/gltf.py tools/cosmodata/meshpack.py tools/cosmodata/fetch_surface_maps.py tools/cosmodata/build_small_bodies.py tools/cosmodata/build_heliophysics.py unity/Assets/StreamingAssets/CosmoSynth/catalog/solar_system.json unity/Assets/StreamingAssets/CosmoSynth/catalog/spacecraft.json unity/Assets/StreamingAssets/CosmoSynth/models/** unity/Assets/StreamingAssets/CosmoSynth/surfaces/** unity/Assets/StreamingAssets/CosmoSynth/sun/** unity/Assets/CosmoSynth/Core/Orbits/** unity/Assets/CosmoSynth/Core/Sun/** unity/Assets/CosmoSynth/Core/Frames/BodyOrientation.cs tests/CosmoSynth.Core.Tests/ShippedCatalogTests.cs tests/CosmoSynth.Core.Tests/SpacecraftTests.cs tests/CosmoSynth.Core.Tests/HorizonsValidationTests.cs tests/CosmoSynth.Core.Tests/OrbitTests.cs tests/CosmoSynth.Core.Tests/KeplerTests.cs tests/CosmoSynth.Core.Tests/LunarTheoryTests.cs tests/CosmoSynth.Core.Tests/StandishTests.cs tests/CosmoSynth.Core.Tests/SmallBodyTests.cs tests/CosmoSynth.Core.Tests/SolarActivityTests.cs tests/CosmoSynth.Core.Tests/HeliosphereTests.cs tests/CosmoSynth.Core.Tests/fixtures/** docs/accuracy.md unity/Assets/CosmoSynth/Runtime/Layers/Solar/** 1b:unity/Assets/CosmoSynth/Runtime/Rendering/RingSystem.cs 1b:unity/Assets/CosmoSynth/Runtime/Rendering/AtmosphereShell.cs 1b:unity/Assets/CosmoSynth/Runtime/Rendering/PlanetTextureBaker.cs 1b:unity/Assets/CosmoSynth/Runtime/Rendering/SpacecraftModelLoader.cs 1b:unity/Assets/CosmoSynth/Runtime/Rendering/CsMesh.cs
exoplanets    1a,1b   tools/cosmodata/build_exoplanets.py unity/Assets/StreamingAssets/CosmoSynth/catalog/exoplanets.csexo unity/Assets/CosmoSynth/Core/Exo/** tests/CosmoSynth.Core.Tests/ExoplanetTableTests.cs unity/Assets/CosmoSynth/Runtime/Layers/Exoplanets/**
fiction       1a,1b   unity/Assets/CosmoSynth/Runtime/Stage/** unity/Assets/CosmoSynth/Runtime/Sandbox/** unity/Assets/CosmoSynth/Core/Orbits/RestrictedNBody.cs tests/CosmoSynth.Core.Tests/SandboxTests.cs tests/CosmoSynth.Core.Tests/StageTests.cs tools/cosmodata/authored/fictional_holosystem.json tools/cosmodata/authored/exhibits.json unity/Assets/StreamingAssets/CosmoSynth/phenomena/** unity/Assets/StreamingAssets/CosmoSynth/catalog/fictional_holosystem.json unity/Assets/CosmoSynth/Core/Phenomena/** tests/CosmoSynth.Core.Tests/FictionalCatalogTests.cs tests/CosmoSynth.Core.Tests/EncounterTests.cs tests/CosmoSynth.Core.Tests/PhenomenaTests.cs tests/CosmoSynth.Core.Tests/StageBoundaryTests.cs unity/Assets/CosmoSynth/Runtime/Fiction/** unity/Assets/CosmoSynth/Runtime/Phenomena/**
nav           1a,1b   unity/Assets/CosmoSynth/Core/Nav/** unity/Assets/CosmoSynth/Core/Time/WarpLadder.cs unity/Assets/CosmoSynth/Core/Sky/ObservingSites.cs tests/CosmoSynth.Core.Tests/NavTests.cs tests/CosmoSynth.Core.Tests/TimeTests.cs unity/Assets/CosmoSynth/Runtime/Nav/** 1b:unity/Assets/CosmoSynth/Runtime/XR/XrInput.cs 1b:unity/Assets/CosmoSynth/Runtime/XR/XrHandInput.cs 1b:unity/Assets/CosmoSynth/Runtime/XR/XrLocomotion.cs 1b:unity/Assets/CosmoSynth/Runtime/XR/TabletopManipulator.cs 1b:unity/Assets/CosmoSynth/Runtime/XR/UniverseNavigator.cs 1b:unity/Assets/CosmoSynth/Runtime/XR/DesktopInputSource.cs 1b:unity/Assets/CosmoSynth/Runtime/Simulation/SimulationClock.cs
ui            1b      unity/Assets/CosmoSynth/Runtime/UI/** unity/Assets/CosmoSynth/Core/Presentation/Format.cs unity/Assets/CosmoSynth/Core/Presentation/SizePresets.cs tests/CosmoSynth.Core.Tests/FormatTests.cs tests/CosmoSynth.Core.Tests/SizePresetTests.cs unity/Assets/CosmoSynth/Runtime/XR/BodySelector.cs unity/Assets/CosmoSynth/Runtime/XR/AnchorBeacon.cs
lines         1b      unity/Assets/CosmoSynth/Runtime/Layers/Lines/** unity/Assets/CosmoSynth/Runtime/Rendering/OrbitLineRenderer.cs unity/Assets/CosmoSynth/Core/Grids/** tests/CosmoSynth.Core.Tests/LinesTests.cs
audio         3       unity/Assets/CosmoSynth/Core/Audio/** tests/CosmoSynth.Core.Tests/AudioTests.cs unity/Assets/CosmoSynth/Runtime/Audio/**
# `Core/Orbits/RestrictedNBody.cs` is listed explicitly under `fiction` and therefore beats
# `solar`'s `Core/Orbits/**` by the specificity rule above (R23, R38).
# every lane also owns docs/reports/<lane>.md implicitly.
```

The **integrator** owns everything else, and in particular: `CosmoSynthBootstrap.cs`;
`Universe/{UniverseSimulation,CelestialBodyView,FloatingOrigin,CosmoLayer,FrameContext}.cs`;
`Rendering/{CosmoShaders,CosmoSphere,ViewportInfo,RenderDiagnostics,ScaleRenderer,Placement,BodyMarkers}.cs`;
`Rendering/SkyAndRings.cs` until P0.7; `Runtime/Shaders/**` after P0.4b; `Runtime/Layers/*.cs`
(the top level only: the shared layer kit); `Runtime/Data/**`; `XR/XrRig.cs`; `Editor/**`; Core
`Math`, `Physics`, `Serialization`, `Procedural`, `Scale`, `Layers`, `Data`,
`Deep/{VoxelGrid,PointSet}.cs`, `Catalog/**`, `Frames/**` except `BodyOrientation.cs`,
`Presentation/{ScaleLimits,PresentationPlane}.cs`; and the tests `CoreSystemsTests`,
`MathTests`, `ProceduralTests`, `SurfaceTests`, `TerrainMeshTests`, `CsDataTests`,
`LayerTableTests`, `CatalogAssemblerTests`, **`ExoplanetCatalogTests`** (which P0.4d deletes,
R27). `Runtime/Stage/**`, `Runtime/Sandbox/**` and `Core/Orbits/RestrictedNBody.cs` are **not**
integrator files: they belong to `fiction` (R38).

### 9.4 Registration points (slots seeded by the integrator)

| File | Owner | Slot content | Lanes with a slot | Seeded at |
|---|---|---|---|---|
| `docs/data-sources.md` (one slot per lane, after the ledger table) | integrator | source rows: URL, licence, `licenceUrl`, the `changed` line, attribution text, `droppable: yes/no`, output files, bytes (R50) | stars, milkyway, galaxies, cmb, solar, exoplanets, fiction, audio | P0.2 |
| `docs/accuracy.md` (one slot per lane) | `solar` outside the slots | measured bounds a lane must record during 1a — C11's f32 star-position bound, the Envelope tessellation residual, the Moon and Standish errors (R24) | stars, milkyway, galaxies, cmb, solar, exoplanets, nav | P0.2 |
| `TC/build_authored.py`, the authored-file table | integrator | one row per authored JSON: `(source path under TC/authored/, schema name, destination under SA/, sha256-checked?)`; the schema is written out verbatim in §6.9.1 so a lane can add a row without asking (R25) | milkyway, galaxies, fiction | **P0.2** (the file itself is created there too) |
| `Runtime/Rendering/CosmoShaders.cs`, the `All` array | integrator | one shader name per line | stars, milkyway, galaxies, cmb, solar, exoplanets, fiction, lines, ui | P0.7 |
| `Runtime/CosmoSynthBootstrap.cs`, `BuildLayers()` | integrator | one `AddLayer<T>()` line per component; the bootstrap hands references, with no `FindAnyObjectByType` | all runtime lanes | P0.7 |
| `Core/Layers/LayerTable.cs`, `Layers` and `Rungs` | integrator | `LayerDef` rows; `Rung` rows. **A `LayerDef` row is written by exactly one lane — the lane that owns the *renderer* named in §4.5, not the lane that owns the data** (R20). The `quasars` row is written by the **integrator**, outside every slot, because it is the `NotInThisBuild` fixture for `LayerTableTests` and belongs to no lane (R32) | stars, milkyway, galaxies, cmb, solar, exoplanets, fiction, lines | P0.7 |
| `Runtime/XR/XrInput.cs`, `Bindings` | `nav` from 1b | binding rows, one per line, exactly `new Binding("<control>", BindDevice.<device>, BindContext.<context>, "<action>"),` — the type is declared in §5.7.4 and `binding_gate.py` parses that one form (R26, R37) | ui, fiction, audio (`nav` edits its own file freely) | P0.7 |
| `Runtime/UI/CosmoTheme.cs` | `ui` from 1b | colour tokens for that lane's layer | stars, milkyway, galaxies, cmb, solar, exoplanets, fiction, lines, nav, audio | P0.7 |
| `Runtime/Rendering/SkyAndRings.cs`, the ring table | `stars` from 1b | ring rows | solar | P0.7 |
| `Editor/SceneBuilder.cs` | integrator | **none**: the bootstrap builds everything in code, and textures and volumes are created at runtime from StreamingAssets bytes, so no lane needs an editor asset. A lane that thinks otherwise files a request | — | — |
| `PlayModeShot` views | integrator | **none needed**: views are data-driven (`rung:<id>`, `dest:<id>`, `layer:<id>`) | — | — |
| test projects | integrator | **none needed**: `CosmoSynth.Core.Tests.csproj` compiles `Core/**/*.cs` and `tests/.../*.cs` by glob (VERIFIED), so new files join automatically | — | — |

**Destinations and label candidates are not shared tables**: each layer publishes them through
`CollectDestinations` and `CollectLabels` (§4.1).

### 9.5 The lane cards

Every card gives: branch and worktree, phases, what it **owns** beyond `docs/reports/<lane>.md`
and its `data-sources.md` slot, its **slots**, its **1a** and **1b** scope, its **minimum tests**,
its **captures**, its **G5 items** and the **reading list**. Exact formats and constants come from
§2–§8 and Appendix C; the cards fix scope and ownership, not algorithms. All worktrees are
`~/projects/CosmoSynth-wt/<lane>` on branch `lane/<lane>`.

**`stars`** (1a, then 1b wave A). Slots: data-sources, accuracy, CosmoShaders, bootstrap,
LayerTable (layers `stars`, `census`, `flare`; rungs `nearest`, `neighbourhood`), CosmoTheme.
**`constellations` is struck from this list** (R20): `lines` draws it per §4.5, so `lines` writes
the row. The constellation *data* is still yours.
*1a:* `build_starfield.py` on the shared helpers, producing `sky/stars.csstars` (CSS2, Appendix
C.1 — the AT-HYG plus HYG v4.4 join of C9, **with the `av` de-reddening column and the optional
`--dust`**, R41), `stars_named.json` (with the flare flag, G7, **and `features.flare`**) and
`constellations.json` **in the same run**, so the three can never disagree about row indices;
the Reylé census **as its own file `sky/census.cspt`**, never merged into the BY-SA star file
(R40); `StarCatalog` rewritten on `CsData`; `Core/Sky/Constellations.cs`,
`Photometry.cs`, `StarQuery.cs` (ray pick and nearest over the whole catalogue);
`cache/stars.index.json` (`hip` to row; **not shipped**, for `build_exoplanets.py`).
*1b:* `StarFieldLayer` (4 vertices per star, model-flux and observed-sky modes, the magnitude
budget, per-chunk `_EyeInChunk`); the sky-mode subset for the 146 distance-unknown rows; removal
of the old star-shell code from `SkyAndRings`; `NearbyStarSystems` over `StarQuery` rather than
the 9,000 loaded; de-duplication of stars that are also catalogue bodies; label candidates from
`stars_named`.
*Tests:* Sirius -1.44 at 2.637 pc; Barnard's Dec +4.6933 (the J2016 4.7395 must fail); counts at
V at most 6.0 = 5,070 and 6.5 = 8,920; CSS1 refusal; 88 figures, 695 segments, every index valid,
no figure touching a `DistanceUnknown` star; a `StarQuery` hit and miss; photometry round trips;
**`PhotometryTests`: for 200 rows spanning `av` 0–3, `|m(Sun, extinction on) - vmag| < 0.02 mag`**
(R41), and the naked-eye count is the extinction-on count.
*accuracy.md slot:* C11's f32 bound (about 6e-8 of |r|, 17 AU at 1.4 kpc).
*Captures:* `default`, `rung:nearest`, `dest:sirius`, `layer:stars`.
*G5:* stars are round points, not squares; the sky does not shift when walking 2 m; named stars
are where the user expects them.

**`milkyway`** (1a, then 1b wave B). **numpy: yes** (`build_dust.py`). Slots: data-sources,
build_authored, CosmoShaders, bootstrap, LayerTable (`dust`, `dustExtent`, `nebulae`,
`cloudMarkers`, `arms`, `mwDisc`, `mwBand`, `masers`, `cepheids`, `globulars`; rungs
`clouds`, `milkyway`, `halo`), CosmoTheme, accuracy. **`dustExtent` and `sunOrbit` are struck**
(R20): both are ribbons drawn by `lines` per §4.5, so `lines` writes their rows; their data and
constants are still yours.
*1a:* `build_dust.py` (Vergely at native 20 pc, Appendix C.7, with the three axis integrals and
the 8-flip sign fit); `build_galaxy_points.py` (Cepheids, globulars, masers, C.8);
`authored/landmarks.json` with **a reference on every entry** (C.9); Core `DustCube`,
`GalaxyModel` (the Reid arm table), `PlaceCatalog`. **Never reuse the Atlas's GPL `dust.bin`.**
*1b:* the dust volume layer (step-budget raymarch, premultiplied, plus the voxel-sprite LOD); the
arms and landmark markers; Cepheid and globular points through the shared point kit; the
procedural nebula close-ups at measured positions.
*Tests:* the three dust integrals; `ExtinctionAlong` symmetry; 0 outside the box; the 8-flip
cloud score recomputed in Core without numpy; a header/size mismatch refused; the Reid arm radii
(Local 8.526, Perseus 10.070, Sgr-Car 6.871, Outer 12.443 kpc at beta = 0); the globular count
213; 47 Tuc at 4.52 kpc; the Cepheid count 2,214 and the warp sign; the drop test passes without
the Cepheid file.
*Captures:* `rung:milkyway`, `dest:galactic-centre`, `layer:dust`.
*G5:* dust does not darken the room; the frame rate holds inside the cube.

**`galaxies`** (1a, then 1b wave B). **numpy: yes**, for the CF4 grids only. Slots: data-sources,
build_authored, CosmoShaders, bootstrap, LayerTable (`galaxiesLocal`, `galaxies2mrs`,
`groupsCf4`, `basins`, `density`, `sdss`; rungs `localgroup`, `localvolume`,
`laniakea`, `cosmicweb`), CosmoTheme, accuracy. **`sgGrid` is struck** (R20): it is a `lines`
ribbon.
*1a:* `build_localvolume.py` (C.10) and `build_lss.py` (C.11: 2MRS with the 2MASS XSC PA join,
CF4 groups, the 128^3 basins, the 64^3 delta plus its slice PNG for a human check, the SDSS
subsample) and `authored/{structures,overrides,giants_fallback}.json`; Core `GalaxySet`,
`GalaxyOrientation`, `Cosmology`. **H0 = 74.6 named in every provenance string.**
*1b:* the galaxy layer (oriented disc impostors, a point below 3 px); the basin shells and
labels; destinations for M31, M33, the Magellanic Clouds, the Local Group barycentre, the named
clusters and voids.
*Tests:* 911 LVDB dwarfs; Antlia II at 124.17 kpc; M31 at 0.77 Mpc with the override i = 77 and
the derived value inside 72–80; the inclination formula at q = 0.28, 0.33, 0.59; PA 0 points
north; no LVDB blank became a number; Laniakea is exactly 4,079 cells and the label at
SG(-62, -8, 39) is 1; the CF4 median H0 = 74.6 within 2; the Zone of Avoidance; D_C(0.05) =
198.54 Mpc through the whole path; the drop test passes without the CF4 files.
**`StructuresTests`** (R51 — `lss/structures.json` was the only shipped dataset with no test, and
several of its rows are marked "INFERRED: each needs a citation before shipping"): Virgo resolves
to a direction within 0.1 deg of (187.699, +12.385) and a distance of 16.5 plus/minus 1.5 Mpc
through the cluster distance rule; Coma to 99 plus/minus 8 Mpc; the Laniakea entry's SG position
converts to the same cell the `cf4_basins` test reads as label 1; the Dipole Repeller lies in the
hemisphere opposite Shapley (dot product under -0.7); every entry has a non-empty `reference` and
a category in the allowed set; no two entries within 0.5 deg **and** 5 % in distance.
*Captures:* `rung:localgroup`, `rung:laniakea`, `dest:m31`.
*G5:* galaxies read as tilted discs, not squares; the Local Group scale feels navigable.

**`cmb`** (1a, then 1b wave B). **numpy: yes**. Slots: data-sources, CosmoShaders, bootstrap,
LayerTable (`cmb`; rung `cmb`), CosmoTheme.
*1a:* `build_cmb.py` plus `healpix_nest.py` (C.12: Planck PR3 SMICA-noSZ `I_STOKES_INP`,
**already in uK although `TUNIT` says K_CMB**, rms asserted in 30–300 uK; the inpainting mask;
the 6 x 1024^2 R8 cube in ICRF face components; the WMAP fallback behind the licence profile);
Core `CmbSky` and `CmbModel`.
*1b:* the CMB sky layer (drawn first, LUT colour map, dipole and monopole modes as uniforms) and
the object-form sphere at the top rung with the 0.9–1.1 R crossfade. **The sky form and the
object form never draw together.** Plus the **GPU face-orientation check** (§11.3 assertion
`cmb-faces`), which is this lane's deliverable, not B's.
*Tests:* the Cold Spot at -112 uK (Planck) or -107 (WMAP) within 5; the Galactic-plane rms ratio;
the mask fraction 2.1 % within 0.3; direction-texel-direction round trip on all six faces; decode
raw 0 to -500.0 uK and 255 to +500.0; the dipole apex at RA 167.9419, Dec -6.9443; a sidecar
refusal on a bad header; the HEALPix self-check; the drop test passes with the WMAP profile.
*Captures:* `rung:cmb`, `layer:cmb`, `mode:vr`.
*G5:* the CMB appears at the end of the ladder and never hides the room in MR unless Space mode
is on.

**`solar`** (1a, then 1b wave B). Slots: data-sources, CosmoShaders, bootstrap, LayerTable
(`bodies`, `orbits`, `spacecraft`, `trails`, `sites`, `atmospheres`, `rings`, `clouds`, `winds`,
`belts`, `comets`, `heliosphere`, `parker`, `oort`, `sunActivity`; rungs `leo`, `planet-room`,
`planet-hand`, `planet-moons`, `inner`, `system`, `heliosphere`, `oort`), CosmoTheme, the
SkyAndRings ring table. Owns `docs/accuracy.md` (and seeds nothing in it: the other lanes write
inside their slots, R24). **`orbits` and `trails` are struck from the LayerTable list** (R20):
`lines` draws both per §4.5 and writes their rows; the orbit and trail *data* is yours.
*1a:* Core `LunarTheory` (Meeus ch. 47 **precessed to J2000**, C.6 — this fixes the 2,762 km
error of the same series used without precession, and the roughly 85,000 km drift of today's
Kepler Moon); `StandishElements` Tables 1 and 2a/2b; `Heliosphere`; `SolarActivity` and
`SpaceWeatherScales`; `build_small_bodies.py` (C.4) and `build_heliophysics.py` (C.5); the seven
moons and eight small bodies (G3); the three new spacecraft tables and the authored Apollo 11
table (G2); the surface sites (G4) and the Earth cloud map (G5); the per-craft attitude rule
(G9); `docs/accuracy.md` updated.
*1b:* the belt layer (a GPU Kepler point cloud from the shipped elements); heliosphere shells,
single-sided when the viewer is inside, one shell at a time; comet tails; `RingSystem` checking
`IsGeometryDrawn`; textures re-applied from `view.HasSurfaceTexture`.
*Tests:* the Moon against the Horizons fixture, median at most 20 km and maximum at most 50 km,
with the bound justified; the Earth-EMB offset exactly `|Moon| * 0.0121505`; Standish
heliocentric direction error at most 0.08 deg over 2026–2031 plus the new Jupiter 1979-03-05
fixture vector; every new body passes `CelestialCatalog.Validate`; the Kirkwood 3:1 and 2:1 gaps,
the Hildas, the Trojan clouds, the Plutinos; Ceres absent and Hygiea present; solar-cycle 19 and
25 extrema; `XrayClass(1.2e-4) = "X1.2"`, `GScale(7) = G3`, `SScale(100) = S2`; the heliosphere
Rankine limits.
*Captures:* `rung:system`, `dest:saturn`, `layer:belts`.
*G5:* rings and atmospheres never float in empty sky; belts read as belts.

**`exoplanets`** (1a, then 1b wave C). Slots: data-sources, CosmoShaders, bootstrap, LayerTable
(`exohosts`, `worlds`), CosmoTheme.
*1a:* `build_exoplanets.py` producing `catalog/exoplanets.csexo` with **all 6,360 planets and
4,769 hosts** (C.3), keeping every derivation rule it has today and storing the results plus the
bits that say which rule fired; the `hygRow` join to CSS2; Core `Core/Exo/ExoplanetTable.cs`.
`--max-systems` and the misleading "within 40 pc" claim are gone.
*1b:* the host-star marker layer; the system expander (`AddBodies` on approach or select,
`RemoveBodies` on leaving — **never `SetCatalog`**); orbit lines on the focus host through
`lines`' API.
*Tests:* at least 6,000 planets and 4,500 hosts; at least 450 hosts within 40 pc; 51 Peg b period
4.2308 d; TRAPPIST-1 exactly 7 planets at 12.4 pc; Proxima b; no expanded planet denser than
30,000 kg/m^3; every expanded body validates; the FNV seed parity between Python and C#; the NASA
acknowledgement string present.
*Captures:* `dest:trappist-1`, `layer:exohosts`.

**`fiction`** (1a, then 1b wave C). Slots: data-sources, build_authored, CosmoShaders, bootstrap,
LayerTable, CosmoTheme, XrInput.Bindings (the exhibit timeline scrub).
*1a:* `authored/fictional_holosystem.json` (C.13, the re-authored near-circular orbits, every
field Fictional, renamed "VG-429 Holosystem"); `authored/exhibits.json` (the one-off hand
conversion of `catalog.js`, texts verbatim, plus a per-exhibit provenance block and
`sourceCommit`); the phenomena copy through `build_authored.py` with the SHA-256 check; Core
`EncounterCapture` and the nine exhibit formulas.
*1b (first half):* the Fictional tabletop hologram in its own root with no ICRF placement,
`SectorView` and the projector skin shaders; the nine exhibits as a panel list with local time;
the context entries on the Sun, Sgr A*, M31, the clusters and the cosmic-web rung.
*1b (second half) — added by this revision (R38), because these had no owner at all:*
`Runtime/Stage/StageDirector.cs` (§5.3) with the three stages, the 0.4 s cross-fade, state
save/restore and the harness actions `stage:real`, `stage:fictional:<id>`,
`stage:phenomenon:<id>`; `Runtime/Sandbox/SandboxController.cs` with the **real** sandbox of
§5.9.3 over Core `Core/Orbits/RestrictedNBody.cs`, and the **arcade slingshot** as a Fictional
stage entered from GO -> FICTIONAL SECTORS (R45), not from TOOLS.
*Tests:* the stage-boundary test of §5.3; Searing Fragment's period 34.4 d within 0.1; every id
starts `fic-holo-` with no collision; the encounter SHA and 241 x 514 shape; the mass-weighted
centre of mass drifts under 1e-5 between frames 0 and 240; 9 exhibit ids including `black-hole`
and `merger`; a known value for each formula.
*Sandbox and stage tests:* the four §5.9.3 checks (two-body energy conservation; a radial launch
from Earth escaping at 11.19 km/s and not at 11.0; a Hohmann Earth-Mars transfer time inside the
vis-viva bound; forward-then-back integration returning within tolerance); one fixed-step arcade
determinism test from a recorded seed; `StageTests` for save/restore round-tripping origin, U,
plane, focus, follow and layer state.
*Captures:* `stage:fictional:vg429`, one per exhibit, **`stage:phenomenon:black-hole`,
`tool:sandbox`, `tool:sandbox:arcade`**.
*G5:* fictional content is unmistakable (the projector skin and the `?` glyph).

**`nav`** (1a Core-only, then 1b wave A). Slots: bootstrap, CosmoTheme. It owns `XrInput.cs` from
1b and so edits `Bindings` directly.
*1a:* `TravelPlan` (§5.7.1), `Destination` (§4.2), `WarpLadder` (§5.8.1), the calendar range and
the `int` year overflow, `Core/Sky/ObservingSites.cs` (G10).
*1b:* `TravelController`, `ScaleControl` (stick and wheel zoom, the pivot rule), Follow and Free,
date jump and reverse warp and Now, mission playback over the real trajectories, the proximity
service ("nearest of any layer" drives flight speed), the three manipulator fixes of §3.6, the
desktop camera world/local fix, the `ClaimStick`/`PanelHot` claim API, and the whole of Appendix
E in `Bindings`.
*Tests:* §5.7.1's list; the calendar round trip at the limits and refusal beyond them; the
binding table has no duplicate (control, device, context) triple.
*Captures:* `dest:<a few>`; the harness's flight goes through `TravelController`.
*G5:* grab and scale do not drift under snap turn; GO reaches M31 and returns to Earth.

**`ui`** (1b wave A). Owns `Runtime/UI/**`, `Core/Presentation/{Format,SizePresets}.cs`,
`BodySelector.cs`, `AnchorBeacon.cs`. Slots: CosmoShaders, bootstrap, XrInput.Bindings.
*Scope:* `WristPanel` (successor to `WristDashboard`) with the five tabs of §5.6 generated from
tables; the watch face; `InspectorCard` and `InspectorModel` (replacing `SpatialPanel`); the
status card and `FirstRunGuide`; `LabelLayer` (successor to `BodyLabelLayer`) with the budget and
priority of §5.6.10; `EdgeArrow`; `MeasureTool`; `RoomFade`; `SpecimenTray`;
`ObservedSkyControl`; the two `CosmoTheme` token fixes; **hands-only reachability**; trigger
arbitration through the claim API; rebuild only on change; "SIZES NOT TO SCALE" in the headset.
`WristDashboard`, `SpatialPanel` and `ProvenanceHud` are deleted.
*Tests:* Core-side only, for any layout maths it moves to Core (`Format`, `SizePresets`).
*Captures:* `ui:go`, `ui:layers`, `ui:time`, `ui:tools`, `ui:info`.
*G5:* the panel is readable at wrist distance, reachable with hands only, and never blank.

**`lines`** (1b wave A). Owns `Runtime/Layers/Lines/**`, `OrbitLineRenderer.cs`,
`Core/Grids/**`. Slots: CosmoShaders, bootstrap, LayerTable (`orbits`, `trails`,
`constellations`, `scaleRings`, `horizonGrid`, `eclipticGrid`, `planeGrid`, `cylGrids`,
`sgGrid`, `dustExtent`, `sunOrbit`), CosmoTheme. **All eleven rows are `lines`'** under the
one-lane-per-row rule (R20): the six that four other cards also claimed — `orbits`, `trails`
(solar), `constellations` (stars), `sgGrid` (galaxies), `dustExtent`, `sunOrbit` (milkyway) —
are struck from those cards, because §4.5 draws every one of them through `CosmoSynth/Ribbon`.
Summing the seven slot lists before this revision gave 51 ids for 46 rows: six duplicates and one
unclaimed (`quasars`, now the integrator's, R32). Git would have merged both copies cleanly and
`LayerTableTests`' "ids unique" would have failed at integration with neither lane having broken
a rule it was given.
*Scope:* orbits as `CosmoSynth/Ribbon` meshes under the group transform; the distance grids; the
light-time rings up the whole ladder (§5.9.4); the constellation lines from `stars`' Core data;
the galactic and supergalactic grids; the Sun's orbit ring at R0 = 8.15 kpc.
*Captures:* `rung:system`, `layer:scaleRings`.
*G5:* no hairlines, no stair-stepping, lines readable at every rung.

**`audio`** (Phase 3). Owns `Core/Audio/**`, `Runtime/Audio/**`. Slots: bootstrap,
XrInput.Bindings, CosmoTheme, data-sources (a pulsar table if one ships).
*Scope:* §5.9.8. **No `RECORD_AUDIO` permission** (G3 fails it).

### 9.6 Integration protocol

**Who runs the editor:** only the integrator, in main, always through `editor_lock`. No MCP
editor is used for CosmoSynth. **Lanes never launch Unity in any mode.**

`tools/lane.sh merge <lane>`, one lane at a time:

1. `require_main_clean`. No `/tmp/cosmosynth-main-detached` sentinel. The lane report says
   `READY-1A` or `READY-1B` with a head commit equal to `lane/<lane>`. `lane.sh check <lane>`
   passes.
2. `git merge --no-ff --no-commit lane/<lane>`.
   - A conflict **only inside slots**: the integrator resolves it by keeping every lane's slot
     content, then continues. This is not expected to happen (§9.3 probe).
   - **Any other conflict:** `git merge --abort`. The lane must `git merge green`, resolve in its
     own tree, rerun G0 and re-signal READY.
3. `tools/gates/g0.sh --full` on the merge result. Red means `git merge --abort`.
3b. **If the merge added, removed or changed any file under `unity/Assets/StreamingAssets/`, run
   `python3 tools/cosmodata/ledger.py --write` on the merge result** to regenerate
   `SA/manifest.json` and the `docs/data-sources.md` ledger table, and include both in the merge
   commit (step 5) (R19). The manifest is claimed by no lane, so it is integrator-owned; without
   this step G0f (`ledger.py --check`, "every `SA/` file in the manifest") goes red on master the
   moment the first data lane merges, and §9.6's "when main is red, merging stops" halts every
   remaining 1a merge. Re-run `g0.sh --full` after the regeneration.
4. If the lane changed anything under `unity/Assets`: **G1b** (`Diagnostics`). This also imports
   the new files, so the editor writes their `.meta`. Red means `git merge --abort`, then delete
   only the untracked `.meta` files the import created:
   `git ls-files -z --others --exclude-standard -- 'unity/Assets/*.meta' | xargs -0r rm`
   (a plain git pathspec `*` also matches `/`, VERIFIED by probe, so this one pattern is
   recursive).
5. `git add -A unity/Assets` (for the `.meta` files), then
   `git commit -m "Merge lane/<lane> (<phase>): <one line>"`.
6. For 1b merges, or any runtime-visible change: **G1c** with `--views release` plus the lane's
   views. Red means `git revert -m 1 HEAD --no-edit`, and the log excerpt goes to the
   orchestrator for the lane.
7. **G1d:** look at the images.
8. Green: `git tag -f green`; raise `tools/gates/test-floor.txt` to the new pass count; append a
   `docs/reports/integrator.md` entry (lane, merge sha, gates, editor minutes) and commit it.
9. Batch rule: Phase-1a merges may be taken in a batch of up to 3, with steps 3–4 once for the
   batch. If the batch is red, bisect by reverting the batch merges one at a time.

**Merge order.** Phase 0: `p0-core`, then `p0-shaders`, then the integrator's own P0.4 commits.
1a halves (after P0.7), FIFO by READY time, suggested `stars`, `nav`, `cmb`, `galaxies`,
`milkyway`, `solar`, `exoplanets`, `fiction`. 1b: wave A in the order `nav`, `ui` (it uses nav's
claim API and `TravelController`), `stars`, `lines`; wave B `cmb`, `galaxies`, `milkyway`,
`solar`; wave C `exoplanets`, `fiction`. Rationale: input and UI foundations first, then the sky
base other layers sit on, then the independent layers.

**Semantic collisions git cannot see** are caught by gates: two lanes binding one control by
`binding_gate`; two layers claiming an ordering the contract forbids by the `LayerTable` unit
test. A lane needing a slot, a csproj reference, a package, a setting or a doc change files it
under "Requests to the integrator"; the integrator does it on master and moves `green`; the lane
merges `green`.

**When main is red** (any `g0.sh --full` failure, or G1b or G1c failing on master HEAD):
merging stops; `green` does not move, so lanes keep merging the last `green` and keep working;
the integrator either fixes it within **30 minutes** with a commit `Fix integration: <what>`, or
reverts the offending merge with `git revert -m 1 <merge-sha>` and logs why. **Never
`reset --hard` published history. Never force anything. Never edit a lane's branch.** Red during
P0.1–P0.4 is expected and is not an incident.

**Lanes staying current:** `git merge green` only — never `master` directly, never a rebase.
Merge `green` before declaring READY, so the integrator's merge is trivial.
---

## 10. Tooling

Everything lives in `~/projects/CosmoSynth/tools/`, versioned in git. **Most of this already
exists on master** (R21): `49741aa Merge lane/tooling: build, capture and gate scripts with
self-testing gates` landed after `cf93a25`, and at `fc07a52` the tree holds `build.sh common.sh
editor-method.sh gate-all.sh lane.sh shot.sh verify-apk.sh xr-baseline.txt cosmodata/ gates/`,
with `gates/` holding `class_gate.sh code_gate.sh g0.sh known-red.txt qsettings_scan.py
settings_gate.py stereo_gate.sh test-floor.txt` (VERIFIED). The earlier statement that "there is
no `tools/` directory today" was true of `cf93a25` and is false now.

**Read this section as a specification of the end state, and P0.1 as a reconciliation against what
is there** — the shipped scripts are self-tested, and where they differ from the prose below the
shipped script wins unless P0.1 names the change. In particular `shot.sh` already implements
`--views/--out/--hide/--profile/--rev/--queue` (`:7-9,32-37`), `run-tests.sh` already has all
three §10.7 additions (`:60-100`), `lane.sh merge` is a stub that exits 2 (`:92-95`), and
`common.sh:21` still says `FONT_ASSET="LiberationSans SDF"` with a `TMP_SDF-Mobile` check in
`verify-apk.sh` — the one live defect C3 creates. **Every line number in §8 and §10 is re-verified
against `fc07a52` before the lane cards go out.**

**Sources.** The Worldfall originals, read in full: `common.sh` 27 lines, `build.sh` 97,
`editor-method.sh` 21, `verify-apk.sh` 171, `shot.sh` 23, `integrate.sh` 76; Mechanarium's
`verify-store-apk.sh` 79. **Do not copy** Worldfall's rsync `lane.sh`, `integrate.sh`'s copy
step, Mechanarium's band-blind `reap-xvfb.py`, or the stray literal `\n` at Worldfall
`shot.sh:18`.

**Script conventions.** `set -uo pipefail`. Count matches with `grep -c`, **never `grep -q`** (the
SIGPIPE trap documented in Worldfall's `verify-apk.sh:7-12`). Every Unity call gets
`-buildTarget Android` and **never `-nographics`**.

### 10.1 Exit codes (shared by every tool)

| Code | Meaning |
|---|---|
| 0 | OK |
| 1 | generic failure: no free display, or Unity exited 1 |
| 3 | expected artefact missing (APK or PNG) |
| 4 | editor lock busy longer than `LOCK_WAIT`, or a foreign editor holds the project |
| 6 | resource guard refused (disk or RAM) |
| 7 | version gate refused (code at or below the maximum published, or a filename collision) |
| 8 | not main, not on `master`, or a dirty tree |
| 9 | timeout (from `timeout` 124 or 137) |
| 10 | G0 red |
| 12 | G1a/G1b red (XrSetup, Diagnostics, editor compile) |
| 13 | G3 red |
| 14 | G1c red (play-mode assertions) |
| 15 | publish refused (the shot's commit is not HEAD, or the name is rejected by `shot_version`) |

`PlayModeShot` itself exits 0 OK, 1 no scene, 2 exception, 3 capture failed, **4 assertion failed
(new)**.

### 10.2 `tools/common.sh` (sourced)

```bash
REPO=/home/triton/projects/CosmoSynth            # main: the only tree with a Library
PROJECT=$REPO/unity
UNITY=/home/triton/Unity/Hub/Editor/6000.5.5f1/Editor/Unity
OUTDIR=/home/triton/builds/cosmosynth            # published; the build page reads it on every load
STAGE=/home/triton/.cache/cosmosynth/staging     # OUTSIDE ~/builds: serve.py find_apks() walks every dir there, hidden ones included (VERIFIED)
DIAG=/home/triton/builds/cosmosynth/diagnostics  # working captures worth keeping (not scanned as shots)
RAW=/home/triton/.cache/cosmosynth/raw           # importer downloads, deleted after the bake
LOCK=/tmp/cosmosynth-editor.lock
BAND_LO=461; BAND_HI=490                         # claimed by no other script on the box (VERIFIED)
FONT_BUILTIN="LegacyRuntime.ttf"                 # what G3 looks for; legacy TextMesh, not TMP (C3)
SDK=$HOME/Unity/Hub/Editor/6000.5.5f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK
BT=$(ls -d "$SDK"/build-tools/* | sort -V | tail -1)
```

Functions:

- **`start_xvfb`**: Worldfall's loop over `seq $BAND_LO $BAND_HI` — take the first display whose
  lock file is absent, keep it only if `kill -0` succeeds after 3 s, and kill it on EXIT through
  a trap.
- **`editor_lock`**:
  - If `COSMOSYNTH_LOCK_HELD` is set, return at once. A child such as `shot.sh` run by `build.sh`
    must not reopen the lock file: `flock` locks belong to the open file description, so a fresh
    `exec 9>` in the child would deadlock against its own parent (**VERIFIED by probe**).
  - Otherwise `exec 9>"$LOCK"; flock -w "${LOCK_WAIT:-3600}" 9 || exit 4;
    export COSMOSYNTH_LOCK_HELD=$$`.
  - Then refuse (exit 4) if `pgrep -f "Editor/Unit[y].*projectPath $PROJECT( |$)"` finds an
    editor these tools did not start.
  - Then **reap the band**: while holding the only CosmoSynth editor lock, every Xvfb on 461–490
    is by definition stale, so kill those PIDs (read from `/tmp/.X<n>-lock`) and remove their
    lock files. Safe because no other script uses the band; it is **not** a pattern `pkill`.
  - The lock is held until the script exits, so one `build.sh` holds it across all its Unity
    launches.
- **`guard_resources DISK_GB MEM_GB`**: reads `df --output=avail -BG /` and `MemAvailable`,
  prints both, exits 6 below either threshold. Editor methods and shots use
  `guard_resources 5 6`; builds use `guard_resources 8 10`. Mark in the file:
  `# ponytail: thresholds are guesses; tune from the first measured IL2CPP peak (build.sh logs it)`.
- **`require_main_clean`**: exit 8 unless `git -C $REPO symbolic-ref --short -q HEAD` is `master`
  and `git -C $REPO status --porcelain` is empty.
- **`unity_run TAG TIMEOUT METHOD [args...]`**: runs
  `timeout -k 60 "$TIMEOUT" "$UNITY" -batchmode $([ "${NOQUIT:-0}" = 1 ] || echo -quit) -buildTarget Android -projectPath "$PROJECT" -executeMethod "$METHOD" -logFile "$PROJECT/Logs/$TAG-$(date +%Y%m%d-%H%M%S).log" "$@"`.
  `shot.sh` sets `NOQUIT=1`, because `PlayModeShot` enters play mode and exits by itself. Maps
  124 and 137 to 9. Greps the log for
  `^\[[A-Za-z][A-Za-z0-9]*\]|_OK|_FAIL|ASSERT_FAIL|error CS|Exception` and prints the last 60
  lines. `unity/Logs/` is already gitignored (VERIFIED).

### 10.3 `tools/editor-method.sh METHOD [unity args...]`

Sources `common.sh`, then `editor_lock`, `guard_resources 5 6`, `start_xvfb`, and
`unity_run method ${METHOD_TIMEOUT:-1200} "$METHOD" "$@"`. It exits with the Unity code. **This
is the only way lane code reaches the editor, and only the integrator runs it.**

### 10.4 `tools/build.sh "<caption>"` (G0 to G1 to G2 to G3 to G4)

Each step stops on failure with the listed exit code.

1. **Where and what** (exit 8): `require_main_clean`; refuse unless
   `git rev-parse --show-toplevel` equals `$REPO` (no worktrees); read `VER` and `CODE` from
   `ServerBuild.cs`.
2. **Version gate** (exit 7): `CODE` must exceed the maximum `versionCode` read by
   `aapt2 dump badging` from **every APK under `$OUTDIR` and `$STAGE`** (today 16, from the
   recovered file). Neither `$OUTDIR/CosmoSynth-$VER.apk` nor `$STAGE/CosmoSynth-$VER.apk` may
   exist. **The gate reads artefacts, not names.**
3. **G0** (exit 10): refuse if `tools/gates/known-red.txt` is non-empty — a build never runs with
   excused gates. Then `tools/gates/g0.sh --full`, output saved for the notes.
4. `editor_lock`, `guard_resources 8 10`, `start_xvfb`. Log `MemAvailable` and `df`.
5. **G1a** (exit 12): `unity_run xrsetup 1200 CosmoSynth.EditorTools.XrSetup.Run`. It must print
   `XRSETUP_OK` and "exactly 1 MetaQuestFeature".
6. **G1b** (exit 12): `unity_run diag 1200 CosmoSynth.EditorTools.ServerBuild.Diagnostics`. It
   must print `DIAGNOSTICS_OK`, with zero `DIAGNOSTICS_FAIL` and zero `error CS`.
7. **G1c** (exit 14): `tools/shot.sh --views release --out "$STAGE/shots-$VER"`, with the lock
   already held. It must print `PLAYSHOT_OK`, and the report's `commit` must equal `HEAD` with
   `dirty 0`.
8. **G2** (exit 9 on timeout, 3 on no APK):
   `COSMOSYNTH_OUTDIR="$STAGE" /usr/bin/time -v timeout -k 60 3600 "$UNITY" ... -executeMethod CosmoSynth.EditorTools.ServerBuild.BuildAndroid -logFile ...`
   It must print `BUILD_OK`. Record `Maximum resident set size` and the BuildPlayer time. The APK
   must be exactly `$STAGE/CosmoSynth-$VER.apk` — **no `ls -t` guessing**.
9. **G3** (exit 13): `tools/verify-apk.sh "$STAGE/CosmoSynth-$VER.apk"`, output saved.
10. **G4 publish** (exit 15): `python3 tools/shotname.py "v$VER__01-<caption-slug>"` must return
    `(VER, caption)`; `mv -n` the APK into `$OUTDIR`, then check that it arrived and the stage
    copy is gone; copy the default-view PNG to `$OUTDIR/CosmoSynth-$VER.apk.png` and
    `~/builds/shots/cosmosynth/v$VER__01-<caption-slug>.png` (other release views become
    `__02...`, capped at 4); write `CosmoSynth-$VER.apk.notes.txt` (§12.2);
    `git tag -a v$VER -m "CosmoSynth $VER (code $CODE)"`; write
    `docs/reports/handoff-$VER.md` and commit it with the tag; print
    `RESULT=OK CosmoSynth-$VER.apk`.

**No skip flags.** A gate that can be skipped is how bad builds ship. The signing environment
follows Worldfall `build.sh:13-22`: if `~/projects/CosmoSynth_secrets/{cosmosynth.keystore,
keystore.pass}` exist, export `COSMOSYNTH_KEYSTORE`, `..._PASS`, `..._KEY_ALIAS` and
`..._KEY_ALIAS_PASS`; otherwise print `SIGNING=debug (sideload only)`. **Today no CosmoSynth
keystore exists** (VERIFIED); creating one is user decision D2.

### 10.5 `ServerBuild.cs` (edit, integrator, P0.4e)

- **Pin the target.** `EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android`
  in `BuildAndroid` and `Diagnostics`; call `SwitchActiveBuildTarget` if the active target is not
  Android.
- **Output.** `OutputDirectory` is `COSMOSYNTH_OUTDIR`, defaulting to
  `/home/triton/.cache/cosmosynth/staging`. **It never writes `~/builds`.**
- **Scene.** Remove the duplicate `EnsureScene`; if the committed scene is missing, fail with a
  message naming `SceneBuilder`.
- **Signing.** `ApplySigning()` copied from Worldfall `WorldfallBuild.cs:176-197` with
  `COSMOSYNTH_*` names, falling back to `~/.android/debug.keystore` (cert SHA-256
  `e39a7703...4a17`, the same key as 1.9.0, so 2.0 installs over it without an uninstall).
- **Version.** `Version` and `VersionCode` stay hand-set constants that `build.sh` reads and
  gates. The first 2.0 values are `"2.0.0-alpha.1"` and `17` (today `"1.9.0"` / 16, VERIFIED at
  `:36-37`).
- **`Diagnostics()` asserts, it does not just dump.** It prints one `[ServerBuild]` line each
  for: the Unity version, product, bundle id, version and code, min and target SDK, architecture,
  backend, APIs and colour space; the quality level, the RP asset name, and the URP MSAA, HDR,
  depth texture, opaque texture, SRP batcher and render scale; the OpenXR Android `renderMode`,
  `depthSubmissionMode`, `latencyOptimization`, `symmetricProjection`, `optimizeBufferDiscards`,
  foveation API and enabled feature ids; the `MetaQuestFeature` count, the Always Included shader
  names, the scenes, and the `LayerTable` rows. It then prints `DIAGNOSTICS_FAIL <what>` for
  every deviation from binding decisions 1 and 2, and exits 1 if there are any, otherwise
  `DIAGNOSTICS_OK` and exit 0. **G0c reads the YAML and G1b reads the API: two independent
  sources, which agree only when both are right.**
- **Data profile.** When the active data profile excludes files, write
  `$OUTDIR/CosmoSynth-<ver>.excluded.txt` for G3.

### 10.6 `PlayModeShot.cs` (edit, integrator, P0.4c)

**Arguments.** `-shotViews` is a comma list, all captured in one play session after one settle
(this amortises editor start on this loaded box). The **view grammar** is `default`, `wide`,
`mode:vr`, `rung:<id>` (the §5.1 ids, C15), `dest:<id>` (flight through `TravelController` once
`nav` lands; `FlyToStar` until then), `layer:<id>` (solo), `star:<n>` (legacy), `ui:<panel>`,
plus §5.7.6's `stage:`, `time:`, `warp:`, `sizes:`, `room:`, `solo:`, `plane:`, `measure:`,
`observed:` and `select:`. `release` expands to `default,wide,mode:vr` **plus every rung in
`LayerTable.Rungs`** — derived, not a hand-kept list. Also `-shotCommit`, `-shotDirty`,
`-shotHide` (as `COSMOSYNTH_HIDE`), `-shotOut`, `-shotProfile`.

**Report.** First line: `commit <sha> dirty <0|1> unity <ver> profile <p> views <n>`. Each view
then prints the group table, the `render:` line, and one line per §5.7.6 action it executed.

**Fatal assertions** are §11.3. Each prints `ASSERT_FAIL <id>: <detail>`; any failure exits 4.
Success prints `PLAYSHOT_OK`. Informational, never fatal (the editor on llvmpipe is not the
device): draw calls, triangles, vertices per layer.

Delete the `DepthBands` compositing (`:391-394,552-553,764-765,811-812`), the far-band
compositing (`:778-883`), the MSAA-off override (`:743-747`) and the `Unused()` stub
(`:324-327`). Keep `ProbeDestinationAlpha` and `COSMOSYNTH_HIDE`. Capturing through
`camera.Render()` into a `RenderTexture` must be re-validated under URP (INFERRED; Worldfall
captures under URP on this box, which is the precedent).

### 10.7 `verify-apk.sh`, `shot.sh`, `lane.sh`, `run-tests.sh`, `shotname.py`

**`tools/verify-apk.sh <apk>`** — the full check table is §11.4. Worldfall's structure is kept:
`has()` counts matches, `pass`/`bad`/`warn`, the xmltree VR category, the orientation parse and
the self-test.

**`tools/shot.sh`** (G1c and lane captures):

```
tools/shot.sh [--views v1,v2|release] [--out DIR] [--hide PATTERNS] [--profile clean]   # current main HEAD
tools/shot.sh --rev <commit> [same options]    # detached checkout of <commit>, restored by a trap
tools/shot.sh --queue                          # serve /tmp/cosmosynth-shotq/*.req, oldest first
```

- **Default output:** `/tmp/cosmosynth-shots/<sha7>/<view>.png` plus `<view>.report.txt`.
  `--out` refuses any path under `~/builds/shots` (exit 1). **Never the gallery**: only
  `build.sh`'s G4 step writes there.
- **Unity call:** `editor_lock` (skipped if already held), `guard_resources 5 6`, `start_xvfb`,
  then `unity_run shot 1800 CosmoSynth.EditorTools.PlayModeShot.Run -shotViews <list> -shotOut <dir> -shotCommit <sha> -shotDirty <0|1> -shotHide "<patterns>" -shotProfile <profile>`.
  No `-quit`: the harness exits itself.
- **`--rev`:** `require_main_clean`; `git -C $REPO switch --detach <commit>` with
  `trap 'git -C $REPO switch master && git -C $REPO clean -fdq -- unity/Assets' EXIT`. The
  `clean` is safe because `require_main_clean` guaranteed no untracked files before the switch;
  what it removes are the `.meta` files and assets the import of a lane commit left behind. Write
  the sentinel `/tmp/cosmosynth-main-detached` (commit, pid), removed by the trap; every tool
  that edits or builds main refuses while it exists. The next editor run on master recompiles:
  that is the cost of not having a second Library.
- **Queue:** a request file `/tmp/cosmosynth-shotq/<lane>-<epoch>.req` holds `lane=`, `commit=`
  (must be on `lane/<lane>` or master), `views=`, `hide=`, `profile=`. The integrator runs
  `--queue` at merge boundaries, one Unity launch per request. Results go to
  `/tmp/cosmosynth-shots/<lane>/<sha7>/`, with `<epoch>.done` or `<epoch>.failed` holding the
  exit code and the last 40 log lines. **Lanes never run `shot.sh`.**

**`tools/lane.sh`**:

```
tools/lane.sh add <name>     # integrator: refuses unless <name> is in lanes.txt
                             # git worktree add -b lane/<name> ~/projects/CosmoSynth-wt/<name> green
                             #   (phase0-contracts before green exists)
tools/lane.sh check <name>   # anyone: the G0h ownership gate (own_gate.py) on master...lane/<name>
tools/lane.sh merge <name>   # integrator: §9.6 steps 1-9
tools/lane.sh rm <name>      # integrator: refuses unless lane/<name> is merged into master
tools/lane.sh list           # git worktree list, plus each report's Status line
```

`own_gate.py <lane>`: compute `git merge-base master lane/<lane>` and diff the branch against it;
every changed path must match one of the lane's globs, respecting `1b:` and the Phase-1a
restriction from `tools/phase.txt`; otherwise the file must contain the lane's markers at the
base **and** every hunk of `git diff -U0 <base> lane/<lane> -- <file>` must lie strictly between
that lane's BEGIN and END lines. **Any `*.meta` change fails. Any change to a marker line
fails.** `--self-test` builds a throwaway repo in `mktemp -d` and asserts that an in-slot edit
passes and that an out-of-slot edit, a marker edit, a `.meta` addition and an unowned file each
fail.

**`run-tests.sh`** — **already done at `fc07a52`** (R21; the claim that it "passes no
`-p:UnityScriptAssemblies`" was true at `cf93a25` and is false now: `:60-100` has the fallback,
the `mktemp` log, the per-tree `flock`, `nice -n 10` and the Editor.Compile hook, VERIFIED). The
three additions, recorded because they are the contract for anyone who touches it again:

1. **ScriptAssemblies fallback.** `SA=$REPO_ROOT/unity/Library/ScriptAssemblies`, falling back to
   `/home/triton/projects/CosmoSynth/unity/Library/ScriptAssemblies`. Print the date of
   `Unity.XR.Hands.dll` and pass `-p:UnityScriptAssemblies="$SA"`. This fixes the 16 CS errors of
   a Library-less tree (VERIFIED by probe).
2. **A private log.** `LOG=$(mktemp /tmp/cosmosynth-runtime-build.XXXXXX.log)`; the shared fixed
   path `/tmp/cosmosynth-runtime-build.log` is clobbered by concurrent trees (VERIFIED).
3. **A per-tree lock.**
   `exec 8>/tmp/cosmosynth-tests-$(printf %s "$REPO_ROOT" | md5sum | cut -c1-8).lock; flock 8`.

Also: `nice -n 10` for the dotnet calls; build `tests/CosmoSynth.Editor.Compile` when it exists;
and correct the false "clean checkout builds" comment in `Runtime.Compile.csproj:32-36`.

**`tests/CosmoSynth.Editor.Compile`** (feasible, **time-boxed to 2 h**, else dropped with a note
in the P0 report): a netstandard2.1 project compiling `Core/**`, `Runtime/**` and `Editor/**`
against `$(UnityManaged)` plus `UnityEditor.CoreModule.dll`, and the package Editor DLLs from
ScriptAssemblies (`Unity.XR.OpenXR.Editor`, `Unity.XR.Management.Editor`,
`Unity.XR.OpenXR.Features.MetaQuestSupport(.Editor)`,
`Unity.RenderPipelines.Universal.Editor/Runtime`, all VERIFIED present). Worth it because
`ServerBuild`, `XrSetup` and `PlayModeShot` are compiled by nothing outside Unity today.

**`tools/shotname.py NAME`** — screenshot names come from the real parser, read-only. It never
edits and never imports `serve.py` (importing would execute its module-level code); it extracts
only `SHOT_VERSION` and `shot_version` with `ast` and evaluates them:

```python
import ast, re, sys
src = open('/home/triton/control-panel/serve.py').read()
keep = [n for n in ast.parse(src).body
        if (isinstance(n, ast.Assign) and any(getattr(t, 'id', '') == 'SHOT_VERSION' for t in n.targets))
        or (isinstance(n, ast.FunctionDef) and n.name == 'shot_version')]
ns = {'re': re}; exec(compile(ast.Module(keep, []), 'serve.py', 'exec'), ns)
if sys.argv[1] == '--self-test':
    assert ns['shot_version']('v1.3__01-attach-camera') == ('1.3', 'attach camera'); print('ok'); sys.exit()
print(*ns['shot_version'](sys.argv[1]), sep='\t')
```

`build.sh` requires the printed version to equal `VER` and the caption to be non-empty. The regex
(`serve.py:2252`) accepts `2.0.0-alpha.1` (VERIFIED).

### 10.8 Retire `~/server-setup/*-cosmosynth.sh`

Insert as line 2 of each of the four scripts:
`echo "retired 2026-09: use ~/projects/CosmoSynth/tools/ (see CLAUDE.md)" >&2; exit 2`. Keep the
rest for history; `never-delete.txt` protects the directory. Reason: `build-cosmosynth.sh` would
build ungated with the 141–180 Xvfb band, and `playshot-`/`shot-cosmosynth.sh` write into the
published gallery by default (VERIFIED). `SceneShot.cs` is deleted in P0.4c, so
`shot-cosmosynth.sh` is dead anyway.

---

## 11. Gates

### 11.1 G0: static checks, any tree, seconds

`tools/gates/g0.sh [--full]` runs the self-tests, then G0a–G0g (and G0h on a `lane/*` branch),
printing `G0x PASS|FAIL|KNOWN-RED`. A gate listed in `tools/gates/known-red.txt` is printed but
does not set the exit code. Exit 0 or 10. **Every gate script supports `--self-test` and is run
with it before it is trusted.** A's `tools/gates/foundation.sh` is **not** created; its six gates
are folded in as marked (C14), and its verbatim shell — self-tested on the laptop and known to
flag exactly today's defects — is **Appendix H**, the implementation source for those rules.

| Script | Id | Scope and rule | Self-test |
|---|---|---|---|
| `../run-tests.sh` | **G0a** | §10.7. Passes when the suite's `N passed, 0 failed` has N at least `tools/gates/test-floor.txt` (**142** today, VERIFIED). The integrator raises the floor after each merge, so a deleted test cannot pass silently | — |
| `stereo_gate.sh` | **G0b** | Every `*.shader` and `*.hlsl` under `U/**`. A file with `#pragma vertex` must contain `multi_compile_instancing`, `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`, and the tag `"RenderPipeline"="UniversalPipeline"`, and must not contain `CGPROGRAM`. CosmoSynth shaders must also have a `CosmoSynth/...` name; no `multi_compile`/`shader_feature` other than instancing unless a `// variant-justified:` comment; no `ZTest Always` unless `// ztest-justified:`; any additive `Blend One One` must be exactly `Blend One One, Zero One` (**the regex is anchored at line end**, so `Zero OneMinusSrcAlpha` cannot satisfy it by prefix) or carry `ColorMask RGB` in the same pass; `Blend SrcAlpha One` is forbidden outright. **No TMP shader is scanned** (C3). **Four rules added by this revision:** the string `GetCameraPositionWS` must not appear anywhere under `Runtime/Shaders/` (R1, use `CS_EyePositionWS`); no **fragment** function may contain `unity_ObjectToWorld`, `unity_WorldToObject`, `TransformWorldToObject` or `TransformObjectToWorld` (R5); every `CosmoSynth/*` shader with a `#pragma vertex` must declare `_Fade` in both `Properties` and `UnityPerMaterial` (R30); and `Volume`, `Sky` and `Ribbon` must carry `"DisableBatching"="True"` (R12). The ZTest marker is exactly `// ztest-justified:`, which is what the shipped script greps at `stereo_gate.sh:69` | `fixtures/shader/good.shader` plus one mutant per rule: the good one passes, each mutant fails. Appendix H's `gate_stereo`/`gate_alpha` plants are the starting set |
| `settings_gate.py` | **G0c** | Stdlib regex over YAML; the full expected table is §11.2. **Cross-check:** FAIL if Optimize Buffer Discards is on while G0d counts more than one camera | negative control: `git show c450708:` (the 1.7.2 tree) must FAIL on pipeline, colour space, depth submission, latency and symmetric projection, **each by name**. The positive control is the committed settings after P0.5 |
| `code_gate.sh` | **G0d** | Over `U/Runtime/**/*.cs` and `U/Core/**/*.cs`: exactly **1** `AddComponent<Camera>` / `AddComponent(typeof(Camera))`; **0** of `CopyFrom(`, `CameraClearFlags.Depth`, `CameraClearFlags.Nothing`, `targetTexture\s*=[^=]`, `Graphics.DrawMesh`, `Graphics.RenderMesh`, `Graphics.Blit`, `CommandBuffer`, `OnRenderImage`, `\bGL\.`, `Camera.current`, `DepthBands`; `Shader.Find(` only in `CosmoShaders.cs`; no built-in or URP shader name string in Runtime (`Standard`, `Unlit/*`, `Sprites/Default`, `Particles/*`, `GUI/Text Shader`, `TextMeshPro/*`, `Universal Render Pipeline/*`); the axis-swap shape `\.x\s*,[^,;]*\.z\s*,[^,;]*\.y` only in `FloatingOrigin.cs` and `Core/Scale/ScaleSpace.cs`; the removed helpers `ToUnityDirection\|ToIcrfDirection\|ToUnityRotation` referenced nowhere; `MergeCatalogFromJson` and `SetCatalog(Catalog)` absent; `SetCatalog(` at most once outside its own definition; `new Color(` / `Color\.[a-z]` outside `CosmoTheme.cs` (except `Color.clear` for the passthrough clear); any file reading `RenderedRadius` must also reference `IsGeometryDrawn`; `RingSystem`, `AtmosphereShell` and `InspectorCard` must carry `DefaultExecutionOrder` at least 60; **`unity/Assets/CosmoSynth/Core/Compat/IsExternalInit.cs` exists and contains `internal static class IsExternalInit`** (R18 — five shared `record` types do not compile without it, and it looks like dead code to anyone tidying up); **no `.meta` file under `unity/Assets/CosmoSynth/` contains `textureType: 1`** (R13: a NormalMap-typed import makes `CosmoSynth/Body`'s `UnpackNormalRGBNoScale` read garbage, silently, on the terminator). A hit is allowed only with a same-line `// gate-justified: <reason>`, and is still counted. Writes the camera count to `$TMPDIR/cosmosynth-g0d.cameras` for G0c and G3. **The shipped `code_gate.sh` scans `U/Runtime/**` only (`:18`, VERIFIED); P0.1 extends it to `U/Core/**`** (R21, R22) | fixture `.cs` files, one per rule, each flagged; a justified fixture passes. Appendix H's `gate_camera`/`gate_axis`/`gate_catalog`/`gate_shaders`/`gate_order` plants are the starting set |
| `class_gate.sh` | **G0e** | Over `U/**/*.cs`: each file declares a type named after the file; at most one `MonoBehaviour` per file; a MonoBehaviour's class name equals the file name. **Warnings are failures** (memory `unity-monoscript-filename-trap`) | fixtures: two MonoBehaviours in one file; a wrong name |
| `data_gate.py` + `drop_test.sh` | **G0f** | `ledger.py --check` (every `SA/` file in the manifest, every manifest file present, every `licenceTag` allowed by the recorded profile); every `SA/` file also named in `docs/data-sources.md` with a licence; the `SA/` total at most `tools/gates/data-budget.txt` MB (**50**, C22). The **drop test** runs `git archive HEAD` into `mktemp -d`, deletes every file whose **`SA/manifest.json`** entry says `"droppable": true` (the manifest is the machine-readable source; `docs/data-sources.md` is prose and `ledger.py --check` fails if the two disagree, R35), and runs the Core suite there with `-p:UnityScriptAssemblies=<main>`; it must pass, which proves "drop without code changes" for Core. **It is the one slow G0 gate** (R36): about a minute and about 100 MB under `$TMPDIR` after 1a, on a box at 97 % disk with up to six lane agents running `g0.sh`. So it runs **only under `--full`**, under `guard_resources 5 6`, cleans its `mktemp -d` on EXIT through a trap, and passes `--no-restore` after the first run in a tree. A plain `tools/gates/g0.sh` prints `G0f SKIPPED (use --full)`. Its temp-disk cost is in the §9.2 capacity table | a scratch doc missing one file must fail; a fake droppable file whose test does not skip must fail |
| `binding_gate.py` | **G0g** | Parses the rows of `XrInput.Bindings`, one `new Binding("<control>", BindDevice.<device>, BindContext.<context>, "<action>"),` per line; a line it cannot parse is a failure. Each **(Control, Device, Context)** triple appears once, comparing **enum members**, not sentences (R26, R37). The row type and the `BindContext` enum are declared verbatim in §5.7.4; Appendix E's Context column uses those names. **Listed in `known-red.txt` until P0.7**, because today's array is the 9-row 2-tuple form (VERIFIED `XrInput.cs:58`) and the gate cannot pass on it | a fixture with a duplicate row; a fixture with an unparsable row |
| `own_gate.py` | **G0h** | §10.7 | §10.7 |
| `qsettings_scan.py` | (used by G3) | the render-xr-audit Appendix B scan, verbatim | its own YAML self-check |
| `known-red.txt`, `test-floor.txt`, `data-budget.txt`, `log-allowlist.txt` | — | integrator-owned control files | — |

**Verified behaviour of the folded rules** (Appendix H, run on the laptop in Git Bash against a
copy of the `cf93a25` tree): `--self-test` passes, each gate failing on its plant; and on today's
tree the script reports exactly the expected defects — the two CG shaders (no URP tag), the six
built-in shader names in `CosmoShaders.cs:22-43`, two `AddComponent<Camera>` plus `DepthBands`
(`:29`, `:68`, `:78`, `:106-107`), the `.x, .z, .y` swaps at `SceneShot.cs:143`,
`SkyAndRings.cs:220` and `CelestialBodyView.cs:904,905,913,917`, and the removed helpers. After
the migration it must print zero failures.

### 11.2 G0c: the settings table

| File | Key to expected |
|---|---|
| `ProjectSettings/ProjectSettings.asset` | `m_ActiveColorSpace: 1`; `androidUseSwappy: 0`; `activeInputHandler: 2`; **`m_StaticBatching: 0`** (R12: static batching is on by default for Android and bakes vertices into world space, which silently destroys the object-space raymarch, the ICRF-direction sky mesh and the Ribbon's `otherOS`) |
| `ProjectSettings/GraphicsSettings.asset` | `m_CustomRenderPipeline` points at `CS_URP.asset`'s guid (read from its `.meta`); `m_AlwaysIncludedShaders` holds the guids of all 11 `CosmoSynth/*` shaders and **none** of the 6 former built-ins |
| `Assets/CosmoSynth/Settings/CS_URP.asset` | `m_MSAA: 4`, `m_SupportsHDR: 0`, `m_RequireDepthTexture: 0`, `m_RequireOpaqueTexture: 0`, `m_UseSRPBatcher: 1`, `m_MainLightRenderingMode: 0`, `m_AdditionalLightsRenderingMode: 0`, `m_RenderScale: 1` (**derive the field names from Worldfall's or Microverse's URP asset, never from memory**) |
| **`Assets/CosmoSynth/Settings/CS_Renderer.asset`** (R11: `CS_URP.asset` was checked and its renderer was not) | `m_RendererFeatures: []` (**empty** — a feature that needs an intermediate colour texture forces one even under `Auto`, a full-screen store plus load per frame), `m_RendererType` Universal, `m_RenderingMode: 0` (Forward), `m_DepthPrimingMode: 0`, `m_IntermediateTextureMode: 0` (Auto), `m_PostProcessData: {fileID: 0}`, `m_OpaqueLayerMask`/`m_TransparentLayerMask` unchanged (**derive the field names from Worldfall's or Microverse's renderer asset, never from memory**) |
| `ProjectSettings/QualitySettings.asset` | every level's `customRenderPipeline` is the CS_URP guid; the Android default level has `pixelLightCount: 0`, `shadows: 0`, `vSyncCount: 0`, `antiAliasing: 0` |
| `Assets/XR/Settings/OpenXR Package Settings.asset`, the Android block | `m_renderMode: 1`, `m_depthSubmissionMode: 0`, `m_latencyOptimization: 1`, `m_symmetricProjection: 1`, `m_optimizeBufferDiscards: 1`, `m_foveatedRenderingApi: 1` |
| the same file, features | exactly **one** `MetaQuestFeature` document (script guid `f647cc05...`) carrying `m_symmetricProjection: 1` and `m_optimizeBufferDiscards: 1`; `...input.handtracking` `m_enabled: 0`; `...foveatedrendering` `m_enabled: 1`; one "Android Providers" manager |
| filesystem | no `Assets/XR [0-9]*`, no `Assets/XR/Settings [0-9]*`, no `Assets/XR/Resources/XRSimulationRuntimeSettings.asset` |

### 11.3 G1: the editor gates, in main

| Gate | Command | Pass criterion |
|---|---|---|
| **G1a** | `tools/editor-method.sh CosmoSynth.EditorTools.XrSetup.Run` | exit 0, `XRSETUP_OK`, exactly 1 `MetaQuestFeature` |
| **G1b** | `tools/editor-method.sh CosmoSynth.EditorTools.ServerBuild.Diagnostics` | `DIAGNOSTICS_OK`; 0 `DIAGNOSTICS_FAIL`; 0 `error CS` |
| **G1c** | `tools/shot.sh --views release` (lanes through `--queue`) | `PLAYSHOT_OK`; 0 `ASSERT_FAIL`; report commit = HEAD, dirty 0 |
| **G1d** | the integrator scp's each G1c PNG and **reads it**, writing one line per view into the notes and the log | every view described; anything unexpected blocks G2 |
| **G1e** (conditional: any `droppable` data changed) | `tools/shot.sh --profile clean --views default` | `PLAYSHOT_OK` |

**The play-mode assertions.** Each prints `ASSERT_FAIL <id>: <detail>`; any failure exits 4.
These merge A's P1–P13 with D's ten ids; the id is what appears in the report.

| Id | Assertion | Tolerance and why |
|---|---|---|
| `catalog` | `CatalogLoaded` fired exactly once and `SetCatalog` ran exactly once; the view count equals the merged catalogue's body count (derived, not a literal) | exact |
| `camera` | before the harness touches anything: `Camera.allCamerasCount == 1`, its `targetTexture == null`, clear `SolidColor` with `XrRig.PassthroughClearColour.a == 0` while passthrough is requested and opaque in `mode:vr` | exact |
| `pipeline` | `GraphicsSettings.currentRenderPipeline` is the URP asset with `msaaSampleCount == 4`; `QualitySettings.activeColorSpace == Linear`; **its default renderer reports `rendererFeatures.Count == 0`, and `CameraData.requiresIntermediateTexture` is false for the XR camera in every fixed view** (R11) | exact |
| `far` | every enabled CPU-placed renderer's farthest bounds corner is under 0.9 x far = 1800 m; shader-placed renderers assert `_CS_Compress` equals the Core constants; the sky layer sits at its declared radius, centred on the eye within 1e-4 m, and renders first | exact |
| `push` | no rigid owner with s < 1 and an un-pushed near point of at least 5 m ends nearer than 5 m - 1e-3; the returned conflict count is 0 for every fixed view | 1 mm is float rounding at 5 m (ulp 4.8e-7) with a large margin |
| `group` | for every group: every member's rendered position is `E + s_G*(p - E)` and its radius `s_G*r`; `\|r_rendered/d_rendered - r_true/d_true\| <= 1e-5 * r_true/d_true`; intra-group depth order equals true order; near-zone groups have `s_G = 1`; for every pair of opaque owners overlapping on screen the rendered intervals are ordered with at least 2 dz | 1e-5 relative: float32 transforms (24-bit mantissa, 6e-8) over at most 16 roundings is about 1e-6, so 10x margin, while any real scaling bug is at least 1e-3 |
| `stereo` | placement reads only the centre-eye pose (a synthetic plus/minus `IpdMetres/2` pair of view matrices changes no transform); two renders offset by plus/minus `IpdMetres/2` shift **Earth** in `default` (f = 0.7 m) by `IpdMetres * ppr / 0.7` px — **103 px at ppr 1140**, the discriminating case — while the **Sun** (f = 74.69 m) shifts by `IpdMetres * ppr / 74.69` = **0.96 px** as the monotonicity check, and a sky star by **0**. It also **reports the measured `FrameContext.PixelsPerRadian`** — the value every px figure in §2.4, §2.5, §5.6.10 and Appendix A is restated against (R8) — **and the axis ratio `(W*P00)/(H*P11)`, which must be within 15 % of 1** (R17). A reported `PixelsPerRadian` below 1000 requires `ui` to re-check the label sizes against the legibility floor. **What this assertion cannot do:** the harness is monoscopic and fakes the eyes by moving the camera transform, which moves `_WorldSpaceCameraPos` too, so a shader that reads the centre eye where it should read its own passes here either way. That is why §7.1 rule 2 bans `GetCameraPositionWS()` outright and §13 settles it on the headset instead (R1) | plus/minus 0.5 px, the centroid precision of a disc wider than 5 px; the Sun row is informational at that tolerance and is not the discriminator |
| `mapping` | in `default`, the Sun's rendered direction from Earth's rendered centre, in Universe-host space, equals `MapDirection(normalize(sun - earth))` | 1e-5 rad (float) |
| `plane` | in every view, the world y of `universe.TransformDirection(MapDirection(Planes.Up(active plane, ctx)))` is 1 (R31: one name for the function, declared in §5.2) | 1e-6 (float quaternion) |
| `alpha` | the clear alpha is 0; the existing `ProbeDestinationAlpha`; **for each layer whose `Blend` is `Additive` or `Sky`, destination-alpha coverage with the layer hidden by `COSMOSYNTH_HIDE` equals coverage with it shown** | delta = 0 of 255 |
| `text` | every visible label has a non-null font and `characterCount > 0`; `Font.HasCharacter` is true for **every character of the §5.6.9 gated glyph set** | exact; a missing glyph names the character |
| `shaders` | no renderer uses `Hidden/InternalErrorShader`; every material's shader name is in `CosmoShaders.All` | exact |
| `layers` | every registered layer's `SelfCheck()` appends no strings | exact |
| `budget` | the sum of `LayerBudget` over layers with `Weight > 0`, plus bodies, is inside §7.3 | exact |
| `log` | zero Error or Exception log entries after settle, apart from `tools/gates/log-allowlist.txt` entries, each with a justification | exact |
| `volumes` | at most **one** enabled renderer using `CosmoSynth/Volume` has `_Fade > 0`, in every fixed view (R2, §4.3 lifecycle rule 4) | exact |
| `envelope` | in the true-scale view, for every enabled renderer with `_PerVertexCompress > 0.5`, the maximum endpoint-distance ratio over its mesh edges is at most **1.1** (R3, §2.6) | exact ratio; it is a mesh property, not a float race |
| `draws` | the number of enabled renderers using `CosmoSynth/Text` or `CosmoSynth/Panel` is at most **6** in every fixed view (R9, §7.3) | exact |
| `cmb-fade` (conditional: the `cmb` layer is present) | at the `rung:cmb` view and at U values on both sides of the 0.95 R / 1.05 R switch, **at most one** CMB renderer has `_Fade > 0` (R10) | exact |
| `honesty` | the set of active chips equals the set computed from state: `SIZES NOT TO SCALE` per §5.9.6's rule (preset not True **or** any drawn body above 1.05); one chip per `IsPresentationGain` param away from its default, plus `BRIGHTNESS` when the effective star gain differs from 1 by more than 5 %; `EPHEMERIS EXTRAPOLATED` / `STAR MOTION EXTRAPOLATED` when the clock is outside the spans of §5.8.2; and **no drawn object in the Real stage carries a Fictional provenance field**. Compared against the **full** chip set, not the four displayed (§5.6.4) | exact; it lands with a deliberately broken run — `COSMOSYNTH_FORCE_SIZES=x100` with the chip suppressed must trip it (R43) |
| `cmb-faces` (conditional: the `cmb` layer is present) | the GPU samples the uploaded cube map along the six ICRF face-centre directions and the Cold Spot, and the values match `CmbSky.Sample` | within 2 quantisation steps (7.8 uK); a flipped face reads hundreds of uK out |

**Fixed capture views.** The Foundation requires the **seven scales of §2.6**: the Earth hologram
(`default`), the Solar System in the room, 10 pc/m, 30 kpc/m, 3 Mpc/m, the observable universe,
and true scale. Use the nearest rung ids with U overridden to the §2.6 walkthrough values, **so
the walkthrough numbers are the expected values**.

**Never trust a gate that has not failed once.** A new G1c assertion lands together with a
harness run on a deliberately broken input — `COSMOSYNTH_FORCE_SECOND_CAMERA=1` must trip
`camera`; a debug additive shader writing alpha must trip `alpha` — and the integrator records
that run in its log before relying on the assertion.

### 11.4 G3: the artefact gate

| Check | Verdict |
|---|---|
| package `com.triton.cosmosynth`; `versionName` equals the filename's `<ver>`; not debuggable | FAIL |
| boot.config: `xrsdk-pre-init-library=UnityOpenXR`, at least 10 `xr-*` entries, `xr-use-vulkan-offscreen-swapchain` | FAIL |
| **xr-block MD5** (`grep -i 'xr-' \| md5sum`) must be listed in `tools/xr-baseline.txt`. For 2.0 that is **`2e1fc6ccd7f09d6cbe013eb1c74e9e02`** (1.9.0 = Mechanarium 1.0.1 = Microverse 0.1.6, VERIFIED), because MVPVV is off (C18). `9dd8284e0dd84f1eb53cd40c80cd145d` (Worldfall, `xr-mvpvv-enabled=2`) is listed in the file **as a comment only** and is not accepted. On a mismatch, print the line diff against the baseline block | FAIL |
| libs `libUnityARFoundationMeta.so`, `libUnityOpenXR.so`, `libopenxr_loader.so`, `libUnityOpenXRHands.so`; arm64 only, 0 armeabi-v7a | FAIL |
| manifest: `com.oculus.intent.category.VR`, `com.oculus.feature.PASSTHROUGH`, headtracking, `HAND_TRACKING`, `OPENXR` | FAIL |
| manifest has **none** of EYE_TRACKING, USE_SCENE, USE_ANCHOR_API, RECORD_AUDIO, `android.permission.CAMERA`, IOT_MAP_DATA | FAIL |
| INTERNET, BOUNDARY_VISIBILITY, a missing passthrough pre-splash, targetSdk above 34, a debug signature, `libswappywrapper.so` present | warn (**FAIL with `STORE=1`**) |
| **data**: every file under `SA/**` in the tree (excluding `.meta`, minus any list `ServerBuild` wrote as excluded by the active data profile) is present at `assets/CosmoSynth/<rel>` with the same size. **The list is derived from the tree, never hand-kept** | FAIL |
| **shaders**: every name parsed from `CosmoShaders.All` in the source occurs in `strings assets/bin/Data/*` (this catches the magenta-after-stripping trap) | FAIL (promotion rule below) |
| **font**: `LegacyRuntime.ttf` **or** `Arial.ttf` occurs in `strings assets/bin/Data/*`, and `CosmoSynth/Text` occurs (C3; memory `unity-headless-text-fonts`). **This replaces the shipped `FONT_ASSET="LiberationSans SDF"` check and deletes the `font-tmp` / `TMP_SDF-Mobile` check beside it** — with TMP dropped, neither can ever pass on a 2.0 build (R21, P0.1a) | FAIL |
| **URP**: `CS_URP` or `UniversalRenderPipelineAsset` occurs in the data | FAIL (promotion rule below) |
| **baked MSAA scan**: `tools/gates/qsettings_scan.py <apk> <QualitySettings.asset>`. FAIL if its SELF-CHECK fails; FAIL if G0d counted more than one camera while any Android level has AA > 1; otherwise print the values | FAIL / info |
| **data licence**: print the manifest's `licenceProfile` and every distinct `licenceTag` in the build, and the count of files with no redistribution grant. **FAIL with `STORE=1` if any tag is `unclear` or `nc`** (R46) | info / FAIL with `STORE=1` |
| APK size | FAIL above **100 MB** (§6.7); the real size is always recorded |
| the signing DN | info |

**Promotion rule for new checks.** A check whose pattern is INFERRED (the shader and URP strings)
lands as `warn` on its first build and is promoted to FAIL — a one-line change noted in the build
notes — once it has been seen passing on a build whose G1b dump confirms the property. **A guard
that cannot pass is as broken as one that cannot fail** (memory `verification-gap-pattern`).

**`--self-test`** runs Worldfall's `has()` test, then the whole verifier on the **recovered
1.9.0** (`.KEEP`, stable) and compares each named verdict with a table in the script:

| Check on 1.9.0 | Expected | What it proves |
|---|---|---|
| xr MD5 | pass | the baseline comparison can pass |
| VR category, libs | pass | — |
| `versionName` against the filename `CosmoSynth-release` | **FAIL** | the name check can fail |
| font: `CosmoSynth/Text` | **FAIL** (1.9.0 uses `GUI/Text Shader`) | the font check can fail |
| URP string | **FAIL** (Built-in) | the URP check can fail |
| `CosmoSynth/Body` shader string | **FAIL** | the shader check can fail |
| Swappy | warn | — |
| qsettings scan with `git show bcb2886:unity/ProjectSettings/QualitySettings.asset` | SELF-CHECK PASS, L2 AA 0 | the scan decodes |

Every expected FAIL proves a check *can* fail. Any deviation prints `SELF-TEST FAIL` and exits 1.

### 11.5 Gates at a glance, and what each cannot prove

| Gate | Proves | Cannot prove |
|---|---|---|
| **G0** | source invariants: Core maths and data tests; shaders carry the stereo macros and never write alpha additively; the settings YAML holds the decided values; one camera and none of the banned render calls; no axis swaps outside `FloatingOrigin`; one `SetCatalog` site; MonoScript names; data documented and droppable; unique bindings; ownership | that Unity imports or compiles anything; that the settings are applied at build time; any rendering |
| **G0-main** (inside `build.sh`) | no overwrite; the APK traces to a clean commit on `master` | — |
| **G1a** | the XR, URP and colour configuration is (re)applied through the official APIs | that the device honours it |
| **G1b** | the whole project compiles in Unity, Editor code included; the API-level state matches bindings 1 and 2, independently of G0c's YAML read | player-only compile paths (IL2CPP stripping), runtime behaviour |
| **G1c** | the real bootstrap runs; one camera; the passthrough clear; angular size and group order preserved; placement uses the centre eye; additive layers leak no alpha; text has glyphs; no magenta; no error logs | **multiview, Vulkan, MSAA, foveation and tile-memory behaviour, the compositor, hand tracking, performance, and the 1.7.0 bug class.** The harness runs flat on llvmpipe |
| **G1d** | a human-level check that the numbers correspond to a sane picture | the same limits as G1c |
| **G2** | IL2CPP and Gradle succeed with a graphics device, never `-nographics` | anything at runtime |
| **G3** | the artefact carries OpenXR boot wiring identical to a known-good build, immersive launch, passthrough, libs, ABI, version, every data file, every shader, the font and the URP asset; no forbidden permissions | behaviour; that the right *values* are inside the files |
| **G4** | traceability: APK to commit to shots to notes | quality |
| **G5** | the real thing: stereo, passthrough, MSAA, comfort, hands | a second device, a long session |

### 11.6 G5: what only the headset can prove

| # | Check | How |
|---|---|---|
| H1 | the 1.7.0 grid is gone, in MR and in Space. **Also settles the per-eye question** (R1): in `mode:vr`, standing inside the dust box, the two eyes must show **different** marched noise; identical noise in both eyes means `UNITY_MATRIX_I_V` is not per-eye under SPI and every eye-anchored path is drawing the centre eye | look; first also on the recovered 1.9.0 APK (gate P) |
| H2 | the depth format really is D24 (or D32) with submission None | the `render:` line's `dep` field; red if 16-bit |
| H3 | foveation really on; frame time within budget | `adb logcat -d \| grep VrApi`: `Fov=` above 0; `App=` at most 11 ms at 72 Hz in `default`, at true scale and inside the dust |
| H4 | **1–7 px** of far disparity is comfortable; the sky does not swim; the CMB reads as infinitely far. **Also: stand inside the dust box and close one eye, then the other — the marched noise pattern must not change** (R7: a per-pixel jitter decorrelates between the eyes and is binocular rivalry on a high-frequency pattern, in the one layer the design expects you to stand inside) | wear it; if uncomfortable, halve d0 — the single knob of §2.3 |
| H5 | passthrough composition: additive layers never darken the room; rings, dust and bodies occlude correctly; MSAA edges on limbs | look against a bright wall |
| H6 | grabs stay under the hand while the level frame slerps | grab during a travel slerp |
| H7 | symmetric projection is active | the eye size in the `render:` line changes with the IPD setting |
| H8 | text is readable at wrist distance and at world distances | read the panel and a far label |

### 11.7 Gate P: test 1.9.0 now

The user installs `/dl/cosmosynth/recovered/CosmoSynth-release.apk` (1.9.0, code 16) and answers
items 1–3 of the handoff checklist (§12.6). The expected `render:` line on 1.9.0 is
`SinglePassInstanced WxH:2 msaa 0/1 cams 2 q2`. It proves whether the 1.7.1 MSAA revert already
removed the grid, and gives the A/B baseline for the alpha. **It does not block P0.4.**

---

## 12. Versioning, notes, feedback, publishing, handoff

### 12.1 Versions

| Build | versionName | versionCode | Tag |
|---|---|---|---|
| recovered 1.9.0 (kept, `.KEEP`) | 1.9.0 | 16 | — (tree `bcb2886`) |
| Phase-0 alpha | `2.0.0-alpha.1` | 17 | `v2.0.0-alpha.1` |
| later alphas | `2.0.0-alpha.N` | 16 + N | `v2.0.0-alpha.N` |
| Phase-2 betas | `2.0.0-beta.N` | next | `v2.0.0-beta.N` |
| release | `2.0.0` | next | `v2.0.0` |

The integrator bumps `ServerBuild.Version` and `VersionCode` in a commit titled
`Release <ver> (code <n>)` before `build.sh`. The version gate refuses any code at or below the
published maximum, and any existing filename. **Never overwrite, never rename a published APK,
never publish `latest.apk`** (memory `triton-build-page-conventions`). One file per version:
`CosmoSynth-<ver>.apk`.

### 12.2 `<apk>.notes.txt` (written by `build.sh`, rendered verbatim in the build's row)

```
CosmoSynth <ver> (code <n>) — built <date CEST> on triton WITH a graphics device (Xvfb :<n>)
Unity 6000.5.5f1 · URP 17.5 · OpenXR 1.17.1 + Meta OpenXR 2.5.0 · signing: <debug (sideload only) | release DN>
data licence profile: <profile> · contains non-commercial sources (ESA/Planck, A&A via CDS,
AAS via VizieR, SILSO) and <n> source(s) with no redistribution grant (<ids>) ·
**personal sideload only: do not forward this APK, do not link it outside the login**
Commit <sha> on master (clean) · tag v<ver>
Changes since v<previous tag>:
  <git log --oneline v<prev>..HEAD | head -15>

-- gates, read back from their own output (not promises) --
G0  <final line; one line per sub-gate>
G1a <XRSETUP line> · G1b <DIAGNOSTICS_OK + rp / msaa / colour space / depth submission / symmetric / latency / foveation>
G1c PLAYSHOT_OK · <n> views · <n> assertions · render: <line>
G1d <one line per view: what the image shows>
G2  BUILD_OK · <MB> · BuildPlayer <s> · peak RSS <GB> · MemAvailable <before>/<after> GB
G3  VERIFY OK · xr-block <md5> (<baseline label>) · <n> data files · <n> shaders · font <name> x<count> · APK <MB>

-- what is in THIS apk --
layers: <LayerTable rows from the G1b dump>
data:   <total MB; one line per StreamingAssets directory>
shots:  v<ver>__01-... (default view), ...

Nothing above is a promise about behaviour on the headset. Awaiting field test.

--- USER FEEDBACK ---
(none yet)
```

### 12.3 Feedback records (integrator only)

**Every statement the user makes about a build** goes, verbatim in a blockquote with date, time
and time zone, build (version and file), device, and a line "led to: ...", into all three of:
`docs/feedback.md`; `~/builds/cosmosynth/CosmoSynth-<ver>.apk.feedback.md`, which the page renders
under "Feedback from testing"; and that APK's `notes.txt` under `--- USER FEEDBACK ---`.

**Classify each item.** A *blocker* must be fixed before the next build and named in its notes. A
*major* goes to `docs/feedback.md` and the next wave. A *minor* is fixed opportunistically.

**The 1.7.0 grid report.** As of 2026-09-11 it **is** in `docs/feedback.md`, uncommitted
(VERIFIED by `git diff`), quoted verbatim from 2026-07-31 16:47 UTC about 1.7.0:

> "current apk renders like 100 plus isntances of earth , issue with something aI see everyghin so manh times repeated ona grid flat"

The same diff records the 1.9.0 remarks and the 2026-09-11 revival request verbatim. **P0.0
commits it.** If the working copy has lost it, restore exactly this text under the heading
`## 1.7.0 — the grid report (recorded 2026-09-11; missing from this log until then)`.
**Lanes never write feedback.** They receive it through the integrator.

### 12.4 `.KEEP`, screenshots, publishing

- `~/builds/cosmosynth/recovered/CosmoSynth-release.apk.KEEP` **exists** (VERIFIED, 176 B). Its
  text: *"Only surviving 1.9.0 binary (versionCode 16, debug-signed). Kept as the A/B reference
  for the 1.7.0 grid-of-Earths regression until 2.0.0 is confirmed on a headset."* It is also
  `verify-apk.sh`'s negative control, so **releasing it is the user's call**, even after 2.0.0 is
  confirmed; if released, the self-test needs a new negative control first.
- Add `<apk>.KEEP` to every build the user rates good at G5, with a one-line reason and date, and
  to `CosmoSynth-2.0.0.apk`.
- Prune superseded, non-KEEP alphas and betas with `python3 ~/tools/prune-apks.py` (a dry run by
  default; `--delete` applies it, VERIFIED). It moves each build's `<apk>.png` into the shots
  gallery under its version tag before deleting. **Keep at most 3 non-KEEP CosmoSynth APKs on the
  page.**
- **Gallery:** `~/builds/shots/cosmosynth/v<ver>__NN-<caption>.png`, **only** from G4, **only**
  after `VERIFY OK`, **only** from the G1c capture of the same commit; `build.sh` checks that the
  report's commit equals the notes' commit. The name is validated by `tools/shotname.py`.
  `<apk>.png` beside the APK is the same `__01` image.
- **Working captures never go into `~/builds/shots/`**: anything there is published or lands in
  "Unmatched screenshots". Use `/tmp/cosmosynth-shots/...` or `~/builds/cosmosynth/diagnostics/`.
  **Never fabricate a version-tagged shot by copying** — the `v1.7.2__...` copy, md5-identical to
  v1.7.0's, is the precedent P0.0 moves out.
- Only `tools/build.sh` publishes, by `mv -n` from `$STAGE` after G3. Nothing is copied into
  `~/builds` by hand. Worktrees never publish. Every publish ends with a tag and a handoff file
  committed on master. The `mtime` of published files is the build time, which is load-bearing
  for the page's timeline; `mv` preserves it.

### 12.5 The build page

The base URL `https://85-10-235-242.sslip.io` and its authentication come from memory and are
**INFERRED**: confirm them before the first handoff (§13). Downloads are at
`/dl/cosmosynth/CosmoSynth-<ver>.apk`.

### 12.6 The headset handoff (`docs/reports/handoff-<ver>.md`)

```
CosmoSynth <ver> is on the build page:
/dl/cosmosynth/CosmoSynth-<ver>.apk — <MB> MB, debug-signed, installs over 1.9.0 without uninstalling.
Please check, in this order, and answer yes/no plus anything odd (one sentence per problem is enough):
1. It opens with your room visible (passthrough), not as a flat window and not black.
2. Nothing repeats: one Earth, no grid of copies. Close each eye in turn: the same scene in both.
3. The status card's "render:" line reads roughly: <expected string from G1c> — please read it
   out or photograph it.
4. Planet edges are clean and orbit lines smooth (not stair-stepped).
5. Stars are round points, not squares; walking two steps does not drag the sky.
6. Every panel and label has readable text (none blank).
7. Grab and two-hand scale work with hands and with controllers.
8..n. <items from the lane cards merged since the last handoff>
```

The **P handoff (1.9.0, now)** asks items 1–3 only, against
`/dl/cosmosynth/recovered/CosmoSynth-release.apk`, with the expected line of §11.7.

When the answer arrives: record it per §12.3; if it is good, `.KEEP` it, and if the xr MD5 moved
deliberately add it to `tools/xr-baseline.txt` with the APK name and feedback date as evidence —
**a baseline moves only after a headset confirmation, never to turn a red check green**; if it is
bad, blockers freeze 1b runtime merges.
---

## 13. What is INFERRED, and the check that settles each item

Nothing below blocks Phase 0. Each row names the single observation that turns it into VERIFIED,
and who makes it.

**Engine and GPU (settled by the first editor compile and capture, integrator, P0.4–P0.5)**

| # | Item | Settled by |
|---|---|---|
| 1 | **Every shader in Appendix A is uncompiled.** They are PROPOSED HLSL | the P0.4b compile plus G0b plus the G1c assertions |
| 2 | `_ScreenParams` is the eye-texture size under URP XR | the `stereo` assertion's px numbers |
| 3 | `Material.SetColor` linearises in Linear mode | the terminator capture at G1d |
| 4 | negative-scale transforms flip culling by themselves | a ribbon or volume capture at G1d |
| 5 | `Camera.Render()` into a `RenderTexture` works for the harness under URP | the first `PLAYSHOT_OK` |
| 6 | `PlayerSettings.Android.optimizedFramePacing` is the Swappy switch | G1b plus `libswappywrapper.so` absent at G3 |
| 7 | **`UNITY_MATRIX_I_V` is per-eye under single-pass instanced**, so `CS_EyePositionWS()` (A.2) returns this eye's position in both the vertex and the fragment stage (R1). This replaces the deleted item about `_LayerToWorld` in `UnityPerMaterial`, which R4 removed from the design: ShaderLab has no matrix property type, so that question can no longer arise | the **H1 headset alpha**, view `mode:vr`, standing inside the dust box: the two eyes must show different marched noise. The G1c `stereo` assertion **cannot** settle it — the harness is monoscopic and moves the camera transform, so the correct and the broken shader both pass |
| 8 | the eye depth format with submission None is `D24_UNorm_S8_UInt` | the headset `render:` line, field `dep` (H2). **A 16-bit format breaks §2.4 and turns the line red** |
| 9 | Unity's cube-map row order under `SetPixelData` on Vulkan | the `cmb-faces` assertion (§11.3), owned by the `cmb` lane |
| 10 | `UnityWebRequest` accepts `file://` in the editor and the flat harness | the first harness run |
| 11 | placement in `LateUpdate` versus `Application.onBeforeRender` ordering against the pose driver | a headset look; it is an optimisation, not a requirement |
| 12 | the left controller menu button is available to apps for "hold to recentre"; the tracking-origin event name | a headset test; the fallback is TOOLS -> Recentre |
| 13 | Unity's `AudioLowPass`/`Echo`/`Reverb` filters on Android | a device check in Phase 3 |

**Performance and comfort (settled only on the headset)**

| # | Item | Settled by |
|---|---|---|
| 14 | the dust raymarch cost per step-coverage, so whether B = 30 is right | a Fractarium-style cost probe on device, before the dust budget is trusted |
| 15 | whether **1–7 px** of far binocular disparity is comfortable, and whether d0 should fall from 10 m to 5 m | H4; it is one constant |
| 16 | the whole §7.3 per-layer budget | `adb logcat -d \| grep VrApi` (H3) |
| 17 | **the 1.7.0 grid mechanism itself.** The design removes every candidate ingredient either way | gate P on the recovered 1.9.0 (D4), and H1 on the alpha |

**Data (settled by the first bake; the importer assertions catch most of them automatically)**

| # | Item | Settled by |
|---|---|---|
| 18 | the SBDB `full-prec=true` parameter name | the importer's "Ceres `a` has at least 7 significant digits" assertion |
| 19 | the VizieR 2MASS XSC query: the `K.ext` label and constraint syntax, and its coverage of 2MRS ids | the importer's row-count and "at least 95 % matched" assertions |
| 20 | the LVDB `v1.1.1` raw-URL tag path | HTTP 200 plus the header check; otherwise `main` with the SHA recorded |
| 21 | the Vergely dust y and z axis signs | the 8-flip cloud score, which must win by at least 1.5x; otherwise a rendered slice inspected by a human |
| 22 | Dupuy & Courtois basin labels 3–9 | the attractor-voxel check for every label |
| 23 | the CF4 delta box geometry (the fit is VERIFIED; the paper's text disagrees) | the rendered SGX–SGY slice PNG, before the first ship |
| 24 | whether Unity deflates StreamingAssets in the APK (about 66.5 or about 75.8 MB) | G3 records the real size |
| 25 | Meeus ch. 47 accuracy against the Horizons fixture (the bound is at most 20 km median) | `LunarTheoryTests` on the first run |
| 26 | the JPL Standish Table 2a/2b values and error tables | transcription from the JPL page |
| 27 | the Reylé `ObjType` codes | the importer prints the distinct values |
| 28 | the Edenhofer `E(ZGR23)` to `A0` factor, and Zenodo throughput from triton | the paper; a timed range probe |
| 29 | the ZCAT morphological code to T-type mapping for 2MRS | the 2MRS paper; the field is flagged meanwhile |
| 30 | literature known answers to be confirmed then pinned: Cycle 19 maximum (285 in 1958-03), Cycle 25 minimum (Dec 2019), omega Cen M_V (-10.26), 51 Peg b period (4.2308 d), TRAPPIST-1 distance (12.47 pc), Proxima b period (11.19 d), the LMC's LVDB distance modulus, Hygiea a (3.142 AU), M31 and M33 kinematic i and PA, the Leavitt law coefficients, every landmark distance, the Carrington flare date, the Arrokoth and termination-shock dates, the Venera 7 site | the first bake, then pinned into the tests |
| 31 | the exact acknowledgement sentences for Planck, Gaia, LAMBDA, SIMBAD and SDSS; the SILSO licence (believed CC BY-NC 4.0); the 2MASS-via-IRSA commercial route; AAS-via-VizieR NC status for 2022+ open-access papers | read each provider's page; update Appendix D |
| 32 | whether the 1024 CMB cube face is visibly soft on the headset | a headset look; `--face 2048` costs +18.9 MB raw and an integrator commit raising the budget |

**Process and product**

| # | Item | Settled by |
|---|---|---|
| 33 | the build page's base URL and authentication (from memory) | confirm before the first handoff |
| 34 | the IL2CPP peak RSS, and therefore the `guard_resources` thresholds and the 3600 s build timeout | the first build, which logs `Maximum resident set size` |
| 35 | every schedule and effort number in §9 | observation |
| 36 | the §5.1 rung `U` values and every timing example (arithmetic from stated constants) | recomputation; they are tested against `LayerTable.Rungs` to 4 significant figures |
| 37 | the CosmoTheme hue angles behind the two token fixes (arithmetic from VERIFIED RGB) | a headset look at the chips |
| 38 | the Hill-cap worked examples, the sector clock rate, the ambient level, the proper-motion extrapolation threshold, the claim that stereo depth beyond about 76 m is imperceptible, and whether today's Cassini entry is Saturn-centred | their own measurements, when each feature is built |
| 39 | **`PixelsPerRadianCentre` = 1140** (§2.0). Derived from 2064 x 2208 per eye and about 110 x 96 deg (quest-rendering-techniques line 982, itself INFERRED from search snippets), and Symmetric Projection changes it. Every px figure in §2.4, §2.5, §5.6.10, A.4 and A.5 rests on it (R8) | the G1c `stereo` assertion reports the measured value, and the headset confirms legibility at H8 |
| 40 | **the six Earth and Mars globe pages are unassessed** — nobody has opened `earth-xr-vitals.html`, `earth-xr-vitals-weathernext.html`, `earthwindXR.html`, `earthWindXR2.html`, `terra_deck.html`, `ares-atlas-mars-mr.html`, over 350 KB of source classified from filenames alone (R49) | the `solar` lane reads each one in its 1a half and appends the B.14 sub-table; D12 is then decided against that table, before the Phase-2 content freeze |

**Known accepted limits** (not INFERRED — measured and accepted; each goes into
`docs/accuracy.md`):

- Envelope-mode spheres quantise at about `6e-8 * |T|`, roughly 0.5 m standing on a true-scale
  planet, until Phase-3 terrain with tile-local origins (§2.6).
- **Envelope-mode interpolation** (R3): with the `d_max/d_min <= 1.1` tessellation rule the
  rendered surface is radially off by at most 0.0114 m and its attributes are skewed by 0.114 %.
  The silhouette is exact — both endpoints keep their true direction — so this is a depth and
  texture error, not a shape error. It is under one depth quantum from f = 98 m outwards and can
  exceed one nearer than that, which is the same regime in which surface-placed content is already
  refused depth ordering.
- **An opaque body nearer than an enclosing volume's back face removes the whole ray through that
  pixel** (R6), because the volume's `ZTest LEqual` rejects the back face and the near half of the
  cloud goes with it. This is invisible only while no body with `IsGeometryDrawn` lies inside or
  in front of an active enclosing volume. It holds today: the `dust` window (log10 U -19.0 to
  -14.6, §5.4) puts every catalogue body below the 2.5 px floor, so none is geometry. To keep it
  true, `ScaleRenderer` checks it every frame and reports `volumeOcclusion <n>` on the `render:`
  line; **the fixed views must show 0**. Fixing it if it ever fails is not free — see §7.1 rule 5.
- Points in the band `[D*e^(-r/d0), D - R]` in front of a rigid group can be hidden wrongly; no
  real catalogue case falls in it (§2.6).
- The push rule's 5 m floor can leave a conflict unresolved, reported as `push > 0`. The fixed
  views must show 0. If a real scene shows more, the answer is to give that pair's owner a common
  rigid ancestor, **not** to lower the floor.
- Star positions are f32 pc, so close pairs beyond 1 kpc can be misplaced by a few pixels when
  viewed from one of them (C11).
- The galactic-rotation fix changes nothing visible today, because no galactic layer exists yet;
  it must land before any lane draws in galactic coordinates, and it ships with its test (C5).

---

## Appendix A — Shader skeletons (verbatim)

**Status: PROPOSED. None of these has been compiled.** They follow the URP pattern of the
headset-proven `GeoramaXR/.../GeoStar.shader` and `Worldfall/docs/CONTRACT.md` §4.

### A.1 The eleven names

`CosmoShaders.All` is exactly: `CosmoSynth/Body`, `/Point`, `/Ribbon`, `/Sky`, `/Volume`,
`/Atmosphere`, `/Rim`, `/Ring`, `/Unlit`, `/Panel`, `/Text`. Plus the include
`Runtime/Shaders/CosmoCore.hlsl`, which every shader includes by that one name.

### A.2 `Runtime/Shaders/CosmoCore.hlsl`

```hlsl
#ifndef COSMOSYNTH_CORE_INCLUDED
#define COSMOSYNTH_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

// Per-frame globals. Written ONLY by Placement.PublishGlobals (ScaleRenderer, LateUpdate order 50).
float4   _CS_CentreEyeWS;   // xyz: centre eye = the one compression centre for both eyes
float4   _CS_Compress;      // x: d0 identity radius (m), y: 1/d0, z: sky radius (m), w: unused
float4x4 _CS_IcrfToWorld;   // upper 3x3: ICRF components -> world (anchor * P * Q); det = -1

// THIS eye's world position. UNITY_MATRIX_I_V is unity_StereoMatrixInvV[unity_StereoEyeIndex]
// under single-pass instanced, so this is per eye by construction, in the vertex stage after
// UNITY_SETUP_INSTANCE_ID and in the fragment stage after UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX.
// Do NOT use GetCameraPositionWS(): in URP it resolves to _WorldSpaceCameraPos, whose per-eye
// definition under XR is a package detail we do not control (CONTRACT R1, §7.1 rule 2).
// This is NOT the compression centre: that is _CS_CentreEyeWS, which is the CENTRE eye for both
// eyes on purpose (§2.5 rule 1) and must never be made per-eye.
float3 CS_EyePositionWS()
{
    return float3(UNITY_MATRIX_I_V._m03, UNITY_MATRIX_I_V._m13, UNITY_MATRIX_I_V._m23);
}

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

// Pixels per radian for THIS eye near the view CENTRE (P11 = cot(fovY/2)). This is
// ScaledSpace's PixelsPerRadianCentre, about 1140 on Quest 3 (INFERRED, §2.0) -- NOT the panel
// average of 1432 px/rad (25 px/deg), which must never be used for an on-screen size.
// It is the VERTICAL value; the horizontal one differs by (W*P00)/(H*P11), because the eye buffer
// is 2064 x 2208 and Symmetric Projection widens the horizontal term specifically. Point sprites
// and ribbons use the vertical value for both axes; the `stereo` assertion measures the ratio once
// and requires it to be within 15 % of 1 (CONTRACT R8, R17).
float CS_PixelsPerRadian() { return 0.5 * _ScreenParams.y * UNITY_MATRIX_P._m11; }

// Angular half-extent (radians, x and y) to a clip-space offset at clip w, for this eye.
float2 CS_AngularToClip(float2 radians, float w)
{
    return radians * float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11) * w;
}

// Ray-space jitter for ray marches. A function of the RAY, never of the pixel: both eyes
// rasterize the same world point at different pixel coordinates, so a per-pixel jitter
// decorrelates between the eyes and becomes binocular rivalry on a high-frequency pattern -- in
// the one layer the design expects you to stand inside (CONTRACT R7, §2.5 point 7).
float CS_JitterDir(float3 dirOS)
{
    return frac(52.9829189 * frac(dot(dirOS.xy + dirOS.z, float2(0.06711056, 0.00583715))));
}

#endif
```

### A.3 `CosmoSynth/Body` — opaque lit or unlit body

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
        _PerVertexCompress ("Envelope mode (contract 2.6)", Float) = 0
        _Fade ("Layer fade", Float) = 1
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
                float  _Fade;
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
                // The rasterizer then interpolates linearly while f is logarithmic, so the mesh
                // must satisfy the §2.6 tessellation rule d_max/d_min <= 1.1 per edge; CosmoSphere
                // builds a distance-graded mesh in this mode and the G1c `envelope` assertion
                // checks it. The silhouette is exact either way (both endpoints keep their true
                // direction from E); what the rule bounds is depth and attribute skew.
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

                half3 V = normalize(CS_EyePositionWS() - i.positionWS);   // THIS eye (R1)
                half3 H = normalize(L + V);
                half  smooth = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, i.uv).a * _Smoothness;
                half  spec = pow(saturate(dot(N, H)), exp2(1.0h + 10.0h * smooth))
                           * smooth * _SpecGain * step(0.0h, ndl);

                half3 colour = albedo * (_LightColor.rgb * diffuse + _Ambient.rgb)
                             + _LightColor.rgb * spec
                             + albedo * _Emission.rgb;
                return half4(colour * (half)_Fade, (half)_Fade);   // _Fade 1: an opaque body covers
                                                                  // passthrough, correctly (§4.3)
            }
            ENDHLSL
        }
    }
    Fallback Off
}
```

### A.4 `CosmoSynth/Point` — additive point-sprite billboard

Vertex layout, the same for all four corners of a point: `POSITION` float3; `COLOR` UNorm8x4
(display colour); `TEXCOORD0` float4 (x magnitude, y corner 0..3, z shape, w hidden flag). That
is 32 B per vertex; a half-precision `TEXCOORD0` would cut it to 24 B if the budget ever needs
it. Shapes are 0 core, 1 halo, 2 spike-x, 3 spike-y; the extra quads exist only for the roughly
50 brightest stars and are baked by the layer. The shape extents and gains come from today's
`HaloScale`, `HaloBrightness`, `SpikeLength`, `SpikeWidth` and `SpikeBrightness` constants; the
`stars` lane carries them over and re-measures under Linear.
**All four vertices of a quad carry the same `data.w`** (R14, §3.4): the "cull" below writes an
off-screen clip position, which *clips* a primitive rather than discarding it, so a quad whose four
vertices disagreed would rasterize as a clipped wedge instead of nothing. `PointChunksTests`
asserts it.

```hlsl
Shader "CosmoSynth/Point"
{
    Properties
    {
        _EyeInChunk ("Layer mode: eye in this chunk's units", Vector) = (0,0,0,0)
        _Units      ("x: metres per unit, y: parsecs per unit", Vector) = (1,1,0,0)
        _MagWindow  ("x: magnitude at brightness 0, y: at brightness 1 (StarLook: 7, 1.5)", Vector) = (7,1.5,0,0)
        // 3.8 and 9.0 dmm are 4.3 px and 10.3 px at PixelsPerRadianCentre 1140 (§2.0), not the
        // 5.4 and 12.9 px the old panel-average 1432 implied; the `stars` lane re-measures (R8).
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
                // NO matrix here (R4): ShaderLab has no matrix property type, so a matrix can
                // never appear in Properties, which is the documented condition for SRP Batcher
                // compatibility. Point positions arrive in ICRF components (baked, §3.4) and the
                // rotation to world is the GLOBAL _CS_IcrfToWorld.
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
                    // Chunk-relative, eye-relative, precise near the chunk (contract 3.4), then f(d).
                    float3 rel = v.offset - _EyeInChunk.xyz;
                    float  du  = max(CS_Length(rel), 1e-30);
                    float3 dirWS = normalize(CS_IcrfDirToWorld(rel / du));
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
                    float3 dirWS = normalize(CS_IcrfDirToWorld(v.offset));
                    centreWS = CS_EyePositionWS() + dirWS * _CS_Compress.z;   // THIS eye: infinity (R1)
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

### A.5 `CosmoSynth/Ribbon` — screen-constant-width line with analytic anti-aliasing

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
        // 1.4 dmm is ~1.6 px at PixelsPerRadianCentre 1140 (§2.0), not the ~2 px the old
        // panel-average 1432 implied; the analytic AA below fades below one pixel anyway (R8).
        _WidthDmm ("Line width, dmm (mrad); 1.4 dmm ~ 1.6 px on Quest 3", Float) = 1.4
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Float) = 1
        _Placement ("0 rigid (CPU transform), 1 per-vertex (owner contains the eye), 2 sky (ICRF directions, per eye)", Float) = 0
    }
    SubShader
    {
        // DisableBatching: the vertex function reads object space (otherOS), and static batching
        // bakes vertices into world space (R12).
        Tags { "RenderType"="Transparent" "Queue"="Transparent+60" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "DisableBatching"="True" }
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
                if (_Placement > 1.5)   // sky: positionOS is an ICRF direction; THIS eye, at infinity (R1)
                    return CS_EyePositionWS() + normalize(CS_IcrfDirToWorld(positionOS)) * _CS_Compress.z;
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
                // Both endpoints of a segment evaluate the same test, so the whole primitive goes
                // together: an off-screen clip position CLIPS, it does not discard (R14).
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

### A.6 `CosmoSynth/Sky` — inside-out sky sphere at infinity

The mesh is a unit sphere of about 2 k vertices whose object-space vertices **are** ICRF
directions. It uses the identity transform and huge bounds. The data is `cmb/cmb_sky.cscb`
(Appendix C.12): a cube map, 6 x 1024^2 R8, face selection in **ICRF components**; linear
encoding, 3.92157 uK per step, offset -500 uK; plus an inpainting-mask cube of 6 x 256^2. It is
uploaded as a linear (non-sRGB) `Cubemap` and sampled with the **ICRF** direction, never a Unity
axis, so the mapping stays in `FloatingOrigin` for placement and the texture lookup needs no
mapping at all. The `cmb-faces` assertion proves the face and row order. The LUT is an sRGB 256x1
texture.

```hlsl
Shader "CosmoSynth/Sky"
{
    Properties
    {
        _SkyCube ("CMB cube map, faces in ICRF components (linear R8)", Cube) = "grey" {}
        _Lut ("Colour LUT 256x1 (sRGB)", 2D) = "white" {}
        _Contrast ("Contrast about 0 uK (1 = +-500 uK full LUT)", Float) = 2
        _Immersion ("0 = passthrough untouched, 1 = replaces the room (VR / full-space)", Range(0,1)) = 0
        _Fade ("Layer fade (LayerState.Weight; NOT the same thing as _Immersion)", Float) = 1
    }
    SubShader
    {
        // DisableBatching: the object-space vertices ARE ICRF directions (R12).
        Tags { "RenderType"="Background" "Queue"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" "DisableBatching"="True" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero   // first draw of the frame; replaces the (0,0,0,0) clear with a premultiplied colour
            ZWrite Off
            ZTest LEqual
            Cull Off         // The eye sits exactly at the sphere's centre, so every triangle is
                             // seen from inside and they all share one facing sense: culling either
                             // removes all of them or none, and cannot remove the hemisphere behind
                             // the eye. That hemisphere is removed by NEAR-PLANE CLIPPING (its
                             // vertices have w < 0). Cull Off therefore costs nothing and works
                             // whichever way the `cmb` lane winds the mesh. Correctness does not
                             // depend on culling here because `Blend One Zero` makes a double draw
                             // idempotent -- but a pass that ACCUMULATES must not copy this (R16).

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
                float _Fade;
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
                // Anchored to THIS eye -- CS_EyePositionWS reads UNITY_MATRIX_I_V, which is
                // per eye by construction under SPI (R1): zero disparity and zero head-motion
                // parallax, which is what infinity looks like.
                o.positionCS = TransformWorldToHClip(CS_EyePositionWS() + dirWS * _CS_Compress.z);
                o.dirIcrf = dirIcrf;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Sample by ICRF direction: hardware-seamless, no pole oversampling, no inside-out
                // mirroring, and no Unity axis in the lookup.
                half raw = SAMPLE_TEXTURECUBE(_SkyCube, sampler_SkyCube, normalize(i.dirIcrf)).r;
                half t   = saturate(0.5h + (raw - 0.5h) * (half)_Contrast);   // raw 0.5 = 0 uK
                half3 c  = SAMPLE_TEXTURE2D(_Lut, sampler_Lut, float2(t, 0.5)).rgb;
                // Premultiplied. This is the ONE pass that writes coverage on purpose: immersion is
                // the opt-in full-space mode. In MR, _Immersion = 0 and the layer skips the draw.
                half k = (half)(_Immersion * _Fade);   // _Immersion is the room switch, _Fade the
                return half4(c * k, k);                //  layer weight; the CMB layer writes both
            }
            ENDHLSL
        }
    }
    Fallback Off
}
```

The dipole and monopole modes (`cmb.mode`) add `D * dot(n, dhat)` in the fragment, using a
per-material ICRF dipole vector. That is the `cmb` lane's addition; it is a uniform, so the shader
stays single-variant.

### A.7 `CosmoSynth/Volume` — bounded raymarch with a per-eye ray origin

The mesh is a unit cube `[-0.5, 0.5]^3`. The transform is the group placement (Rigid or
Enclosing), with the layer frame's rotation. The march runs in **object space** from **this eye**;
a similarity about the centre eye leaves every eye at the same object-space point, so compression
never touches the integral. The step count comes from the CPU through the step-coverage governor:
`N = clamp(B / coverage, 16, 64)`, B = 30 (§7.3).

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
        // DisableBatching: positionOS is the march's object-space entry point, and static
        // batching bakes vertices into world space (R12).
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            Name "Volume"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha   // premultiplied over: dust absorbs, so coverage = opacity
            ZWrite Off
            ZTest LEqual     // An opaque body inside the box rejects the back face, so the body
                             // shows and no depth texture is needed. ACCEPTED COST (R6, §13): the
                             // rejection kills the WHOLE ray, near half included, so such a body
                             // punches a body-shaped hole through the entire cloud. Sound only
                             // while no IsGeometryDrawn body lies inside or in front of an active
                             // enclosing volume; ScaleRenderer asserts that every frame and reports
                             // `vol` on the render: line. Fixing it is NOT free (§7.1 rule 5).
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
                float3 eyeOS      : TEXCOORD1;   // constant across the primitive for a given eye,
                                                 // so perspective-correct interpolation returns it
                                                 // exactly. Computed in vert, where the instance
                                                 // and eye ids are set up (R5).
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.positionOS = v.positionOS;
                o.eyeOS = TransformWorldToObject(CS_EyePositionWS());   // R1 + R5
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // NOT TransformWorldToObject here: under INSTANCING_ON unity_WorldToObject is an
                // array indexed by unity_InstanceID, which no fragment sets up, so a fragment reads
                // matrix 0 -- the wrong one the moment this layer is ever GPU-instanced. It is also
                // a per-pixel 4x4 multiply for a value constant across the primitive (R5).
                float3 eyeOS = i.eyeOS;
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
                float t  = tNear + dt * CS_JitterDir(dir);   // ray-space jitter: banding becomes fine
                                                            // noise, and the SAME noise in both
                                                            // eyes -- a pixel-space jitter is
                                                            // binocular rivalry here (R7)
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

### A.8 The four small shaders and the two ports

All four follow the same skeleton: the six stereo macros, `CBUFFER_START(UnityPerMaterial)`, a
single pass, `ZTest LEqual`, and `#include "CosmoCore.hlsl"`. **Every one of them — and the two
ports — declares `_Fade ("Layer fade", Float) = 1` in both `Properties` and `UnityPerMaterial`,
multiplying the returned colour and, in these premultiplied and opaque passes, the alpha** (R30,
§4.3, G0b). **None of them calls `GetCameraPositionWS()`; the two that need an eye position use
`CS_EyePositionWS()`** (R1).

- **`CosmoSynth/Unlit`** — queue 2000, opaque, `Cull Back`, `ZWrite On`. `half4 frag(...)` returns
  `half4(CS_DisplayToLinear(_BaseColor.rgb), 1.0h)`. Used by `AnchorBeacon.cs:65` and the pointer
  ray at `BodySelector.cs:70`.
- **`CosmoSynth/Ring`** — queue 3000, `Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha`,
  `ZWrite Off`, `Cull Off`. Samples `_BaseMap` (RGBA, the ring texture) by radius,
  multiplies by `_LightColor` and by a planet-shadow term: an analytic ray-sphere test from the
  ring point along `_LightDirWS` against the parent's drawn sphere, given as
  `_ParentCentreWS` (float4: xyz centre, w radius). Returns premultiplied colour with coverage
  equal to the texture's alpha.
- **`CosmoSynth/Panel`** — queues 3200 (backing) and 3201 (accent),
  `Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha`, `ZWrite Off`, `Cull Off`.
  `alpha = SAMPLE(_BaseMap, uv).a * v.color.a`; colour is
  `CS_DisplayToLinear(v.color.rgb) * alpha`. Replaces the two material creations at
  `CosmoTheme.cs:229,240`.
- **`CosmoSynth/Text`** — queue 3210, the same blend and states as Panel.
  `alpha = SAMPLE(_MainTex, uv).a * v.color.a`, colour `CS_DisplayToLinear(v.color.rgb) * alpha`.
  `_MainTex` is the legacy dynamic font's atlas; `CosmoText.cs:78` clones the font material onto
  this shader and keeps its existing `Font.textureRebuilt` handling (VERIFIED present at
  `CosmoText.cs:82-83`). **TMP is not used** (C3).
- **`CosmoSynth/Atmosphere`** — the existing `CosmoAtmosphere.shader` ported in place from CG to
  this skeleton, maths unchanged (`:111-237`), with **`CS_EyePositionWS()`** read **after** the
  eye macro (R1), and one added uniform for Envelope mode: when set, the analytic integral runs in
  true eye-relative space in units of the planet radius (§2.6).
- **`CosmoSynth/Rim`** — the existing `CosmoRim.shader` ported in place (`:30-89`), maths
  unchanged, queue 3100, `Blend One One, Zero One`.
---

## Appendix B — Feature traceability matrix

Every feature of every source page, mapped to where it lives in CosmoSynth 2.0 or to an explicit
drop. **PORT** = carried over, adapted to CosmoSynth's rules; **FIX** = carried over with a
correction; **REAL** = replaced by a real-data equivalent that does the same job better;
**EXISTS** = CosmoSynth already does it, equal or better; **DROP** = not carried over, with the
reason and the honest equivalent the user gets instead. Drops are allowed only for fabricated
data presented as real, IP, runtime network use, or a 2D-only editor that is not a function of
the universe.

### B.1 Milky Way Atlas (`Fable5/nebula-gallery/index.html`)

| Id | Source feature | CosmoSynth 2.0 home | Status |
|---|---|---|---|
| MWA-01 | Galactic Cartesian parsec frame, Sun at origin, galactic plane horizontal | data stay ICRF; `StandardFrames.EquatorialToGalactic` gets its first test (against the Hipparcos matrix) and its sign fix; galactic-up is `PresentationPlane.Galactic` on rungs 10–13 | PORT |
| MWA-02 | One scaled group, metres per parsec, log range -7.3 to +5.7 | `FloatingOrigin.UnitsPerKm` with `ScaleLimits` -24.70 to +3.00, a superset | FIX (range) |
| MWA-03 | Easing toward targets, `k = 1 - e^(-6.5 dt)` | the manipulator's log smoothing (existing) and `TravelPlan` | EXISTS |
| MWA-04 | 11 parallel loads with a progress bar; explanatory failure on `file://` | the status card with per-file progress; failure-visible layer rows (§5.10) | PORT |
| MWA-05 | Separate VR and MR session buttons | one session; the ROOM slider and the Passthrough / Space presets (§5.9.5) | FIX |
| MWA-06 | Foveation 0.7, XR start pose, dust steps capped at 72 in XR | URP SRP foveation; first-run placement; the dust step budget via the quality governor | PORT |
| MWA-07 | `window.__dbg` debug hook | play-mode harness actions and report fields (§5.7.6) | PORT |
| MWA-08 | Hosting site stylesheet (`lumen-experience.css`) | — | DROP: web page styling, no function. Equivalent: `CosmoTheme` |
| MWA-09 | Dust volume raymarch, Density / Threshold / Quality controls | the `dust` layer from Vergely 2022 (re-derived, not the GPL snapshot); physical per-pc opacity, premultiplied, per-star extinction; `dust.density`, `dust.threshold`, TOOLS -> Quality | FIX |
| MWA-10 | Dust cube frame (box edges) | the `dustExtent` guide: where the measurement ends | PORT |
| MWA-11 | 60 k HYG stars, flux-true sizes | the `stars` layer, about 120 k AT-HYG stars, CosmoSynth star rules (the 2.5–3 px floor, brightness in RGB) | FIX |
| MWA-12 | B-V to T to RGB (Ballesteros plus a Helland fit) | `PlanetGenerator.BlackBodyColor(T)` (the same T formula, the existing colour curve) | EXISTS |
| MWA-13 | The Sun as a G star in the star field | the Sun drawn once: a body above log10 U = -13.5, a star point below | PORT |
| MWA-14 | OBSERVED sky mode, LIMIT MAG 3–14, eye exponent 0.23 | TOOLS -> OBSERVED SKY with an explicit vantage and extinction (§5.9.2) | FIX |
| MWA-15 | Cepheids coloured by age, all at M = -4.2 | the `cepheids` layer, M from a Leavitt law, `distErr` kept as the uncertainty; tag `unclear`, droppable with no code change | FIX |
| MWA-16 | Globular clusters | the `globulars` layer, names kept, LVDB M_V and r_half, a resolved sprite LOD | FIX |
| MWA-17 | 911 galaxies with ellipticity and PA sprites | `galaxiesLocal`: oriented discs in the sky frame, unknown fields drawn round and faint, UNGC giants added | FIX |
| MWA-18 | Marker categories (clouds, MW, LG, solar) with toggles | label categories Clouds, MilkyWay, LocalGroup, Bodies with scale windows; the `cloudMarkers` layer with a reference per entry | PORT |
| MWA-19 | 88 constellations as 3D HIP segments, names at centroids | the `constellations` layer from CSS2 index pairs; names at the brightest star's direction, no dot | FIX |
| MWA-20 | Hover names: the 5 nearest named stars to the ray | hover names (§5.6.10), index-linked to rendered stars | FIX |
| MWA-21 | Standish Solar System, comets, belts | bodies from Horizons; `belts` from SBDB; comet tails gated by distance from the Sun | REAL |
| MWA-22 | Heliosphere: three glow shells (TS 90 AU, HP 120 AU x1.9 tail, bow wave 230 AU) | the `heliosphere` layer: Voyager-measured crossings, a Rankine half-body heliopause, and "no bow shock (IBEX)" | FIX |
| MWA-23 | Saturn ring from the ecliptic frame | real ring radii and the IAU pole (existing) | EXISTS |
| MWA-24 | Plane grids: cube frame, 2 kpc grid, 60 kpc grid | the `planeGrid` guide | PORT |
| MWA-25 | Cylindrical grids (stellar, galactic, Local Group) with ring labels | the `cylGrids` guide, one ribbon mesh per regime, independent of the plane toggle; `sunOrbit` at R0 = 8.15 kpc | FIX |
| MWA-26 | Canvas labels 8.5 cm tall, crowd shrink, look-at, FACE TEXT | labels in dmm, declutter by dropping, upright billboard, FACE TEXT / FROZEN | FIX |
| MWA-27 | Exposure, galaxy glow, skyBoost / farAssist brightness assists | `stars.exposure`, the galaxy glows, the automatic assist, each with a BRIGHTNESS chip | FIX |
| MWA-28 | Measure tool, pc / ly, 489 pickables | TOOLS -> MEASURE between any two picks including bodies, stars and galaxies; km / AU / ly / pc | FIX |
| MWA-29 | GO TO destinations: Solar, Orion, Sgr A*, omega Cen, LMC, M31 | GO destinations (all six, plus every catalogue object) | PORT |
| MWA-30 | "Spaceship" travel: yaw-first, smootherstep, duration 2.6 + 0.5 x decades | `TravelPlan` with the dip profile; cancelled by any scale input | FIX |
| MWA-31 | 3D arrival crosshair | the arrival crosshair, timer started at arrival | FIX |
| MWA-32 | Scale presets Clouds / Galaxy / Local Group | rungs `clouds`, `milkyway`, `localgroup` | PORT |
| MWA-33 | Layer toggles and SOLO (dust, stars, galaxies, solar) | `LayerTable` plus SOLO (Solar, Stars, Dust, Galaxies, Beyond) | PORT |
| MWA-34 | Watch on the left wrist; open by a 5 cm / 800 ms tap or a controller ray | the watch face and its rules (§5.6.2) | PORT |
| MWA-35 | Panel anchor above the wrist, 6 mm / 20 deg deadbands, 7 and 4 per-second damping | the same constants (§5.6.2) | PORT |
| MWA-36 | PIN, and opening while pinned recalls | PIN and recall (§5.6.2) | PORT |
| MWA-37 | Fingertip pointer, 6 mm press / 20 mm release, 300 ms debounce; controller ray within 1.2 m | §5.6.8, plus the approach rule | FIX |
| MWA-38 | Tabs VIEW / LAYERS / TOOLS; buttons, sliders, rows | tabs GO / LAYERS / TIME / TOOLS / INFO, generated from tables | PORT |
| MWA-39 | Category colours (nav violet, preset cyan, layer green, marker amber, tool pink ...) | — | DROP: they collide with the provenance palette (green Measured, amber Procedural, pink Fictional). Equivalent: captions and position, Accent for controls |
| MWA-40 | Hands drawn as a flat blue tint | real hands in passthrough; a neutral translucent hand mesh in Space | FIX |
| MWA-41 | One-hand 6-DoF grab (can tilt the galaxy) | a one-hand grab translates; TOOLS -> Free tilt restores full 6-DoF | PORT (opt-in) |
| MWA-42 | Two-hand scale and twist, clamp at 10^0.5 m/pc | two-hand log scale plus yaw twist, clamped only via `ScaleLimits` | FIX |
| MWA-43 | Sticks: left translate; right X yaw 1.4 rad/s; right Y 0.55 decades/s | L stick 3D fly; R X snap turn (smooth 60 deg/s optional); R Y zoom 0.6 decades/s | PORT |
| MWA-44 | Desktop OrbitControls and HUD: 3 presets, 6 destinations, 7 sliders, 14 toggles | desktop: the same panel pinned and mouse-driven; every slider and toggle is a `LayerParam`, a layer toggle, a label toggle or a tool; Scale is the wheel zoom | PORT |
| MWA-45 | Hover and press beeps; right-controller haptics | UI sounds (§5.9.8); `XrInput.Pulse` on either controller | PORT |
| MWA-46 | Scale readout (cm per AU, or the span of the 2 kpc cube) | the header readout `1 m = X` (§5.1) | PORT |
| MWA-47 | README "Honest limits" section | INFO -> THIS VIEW, "What we do not know here" | PORT |
| MWA-48 | Shipped data files (`stars.bin`, `dust.bin`, `galaxies.json` ...) | re-derived by the §6 importers (the atlas's files are not reproducible, and its dust is GPL) | REAL |

### B.2 Heliosphere V2 (`solar-xr2.html`)

| Id | Source feature | CosmoSynth 2.0 home | Status |
|---|---|---|---|
| HV2-01 | Device-clock time, signed rate, pause, Now | the signed `WarpLadder`, pause, Now (§5.8.1) | PORT |
| HV2-02 | Rate labels ("hr/s", "d/s", "yr/s") | human labels ("1 week per second") | PORT |
| HV2-03 | Standish Table 1 planets (valid 1800–2050) | the Standish fallback outside the Horizons fit; the `EPHEMERIS EXTRAPOLATED` chip | PORT |
| HV2-04 | Schlyter Moon series | the Core lunar theory (the series as used is 2,762 km off; CosmoSynth's Kepler Moon about 85,000 km at 5 years) | FIX |
| HV2-05 | 21 moons on circular orbits, placeholder phases | Horizons moons, plus Mimas, Tethys, Dione, Miranda, Ariel, Umbriel, Proteus | REAL |
| HV2-06 | 12 small bodies | Horizons and SBDB: adds Hygiea, Eris, Makemake, Haumea, Sedna, Quaoar, Gonggong, Orcus | REAL |
| HV2-07 | IAU pole and prime-meridian orientation; Earth by ERA | `BodyOrientation` (WGCCRE, 20 presets) | EXISTS |
| HV2-08 | Surface points: KSC pad, splashdown, rover pins | `SurfaceSite` plus the `sites` layer; adds Curiosity, Viking 2, Sojourner, Opportunity | PORT |
| HV2-09 | Facts on the info card (mass, gravity, day, temperature, winds, missions) | inspector FACTS from catalogue fields; free text as authored summaries with provenance | PORT |
| HV2-10 | Planet magnification x1 / x10 / x100 / x1000 | SIZES True / Readable / x10 / x100 / x1000 | PORT |
| HV2-11 | Moon orbits co-scale with magnification | one factor per system, Hill-capped, swallowed moons hidden and counted | FIX |
| HV2-12 | Sun capped at x25 | the same cap | PORT |
| HV2-13 | Elevation exaggeration x1 / x10 / x50 | RELIEF x1 / x10 / x50 on shipped DEMs | PORT |
| HV2-14 | LOD by pixel radius; the mesh hidden inside a body | `ScaleSpace` representation levels | EXISTS |
| HV2-15 | Labels by gaze cone, no overlap avoidance | the label budget and greedy declutter | EXISTS |
| HV2-16 | One AR session; a space dome fades room to space | the ROOM slider and presets | PORT |
| HV2-17 | Milky Way band cylinder, shown when the dome is over 35 % | the `mwBand` layer in Space mode | PORT |
| HV2-18 | Atmosphere fresnel shell | `AtmosphereShell` | EXISTS |
| HV2-19 | Zonal wind flow shader from per-planet profiles | the `winds` layer; profiles Procedural until cited | PORT |
| HV2-20 | Earth clouds (a remote map, drifting) | the `clouds` layer, NASA Visible Earth, baked offline, static | FIX (network) |
| HV2-21 | Saturn rings with the Cassini division | the existing rings (Cassini and Encke) | EXISTS |
| HV2-22 | Procedural body textures | `SurfacePainter` | EXISTS |
| HV2-23 | GPU-Keplerian belts from a statistical generator | `belts` from real SBDB elements, the same GPU Kepler approach | REAL |
| HV2-24 | 56 hand-entered nearest stars | `stars` (AT-HYG) plus `census` (Reylé 10 pc) | REAL |
| HV2-25 | Temperature colours and diffraction spikes | the existing `StarLook` (spikes for the brightest) | EXISTS |
| HV2-26 | Flare flicker on 12 named M dwarfs | the `flare` layer, off by default, labelled illustrative | PORT (labelled) |
| HV2-27 | Exoplanets: circular coplanar orbits near a star | Archive systems with real e and i, Procedural orientation, orbits drawn for the focus host | FIX |
| HV2-28 | Go-to-star framing at 1 AU = 0.35 m | star framing by the outermost orbit (§5.7.3) | FIX |
| HV2-29 | Light-time scale rings | `scaleRings`, extended up the whole ladder (§5.9.4) | PORT |
| HV2-30 | Ecliptic grid | `eclipticGrid` | PORT |
| HV2-31 | Low-poly probe models | NASA models (existing `.csmesh`) and an abstract glyph where no model exists | REAL |
| HV2-32 | Probes aim their dish at Earth | attitude rules (§5.8.4) | PORT |
| HV2-33 | Info card live values: r from the Sun, distance from Earth, speed, inclination, phase angle, radius | the inspector NOW block (§5.6.9) | PORT |
| HV2-34 | 8 missions as Catmull-Rom curves through dated waypoints, with radial extrapolation | Horizons trajectories (24 plus New Horizons, BepiColombo, Artemis I); no extrapolation needed | REAL |
| HV2-35 | Venera 7 waypoints | — | DROP: no trajectory source exists (Horizons "No matches"); waypoints would be invented geometry. Equivalent: a dated landing site on Venus |
| HV2-36 | Apollo 11 from the real KSC pad on the rotating Earth to the Pacific splashdown | an authored Illustrative table anchored to Measured events; the real lift-off 13:32 UTC | FIX |
| HV2-37 | Play: jump to launch, set the rate, force x1 for Apollo | play, event stepping, stop (§5.8.3) | PORT |
| HV2-38 | Live probes: JWST, Juno, Parker (made-up orbits) | Horizons JWST, Juno and Parker with their windows | REAL |
| HV2-39 | Go to (1.4 m across), Follow, Free | GO / FOLLOW / FREE (§5.7.1–§5.7.2) | PORT |
| HV2-40 | Presets Overview / Inner system / Earth-Moon / Star map | rungs `system` / `inner` / `planet-moons` / `nearest` | PORT |
| HV2-41 | Desktop: click focus, double-click go, F / G / X, wheel x1.2, WASD / QE with proximity speed, Shift x4 | the desktop bindings (Appendix E) | PORT |
| HV2-42 | Quest: right stick fly and tablet scroll, left stick rise and turn, both grips grab and pinch-scale, X tablet, trigger select | CosmoSynth's table (Appendix E); the raw-midpoint pivot is not ported | FIX |
| HV2-43 | Wrist tablet: 4 tabs Time / View / Go to / Missions | TIME, LAYERS plus TOOLS, GO, TIME -> MISSIONS | PORT |
| HV2-44 | Scrollable content, up/down buttons, a scroll bar, stick scroll | panel scrolling (§5.6.3, §5.6.8) | PORT |
| HV2-45 | Fixed footer: focus name, Go to, Follow, Free | the focus footer | PORT |
| HV2-46 | Collapse to a "MENU" pill | the watch face | PORT |
| HV2-47 | Smooth wrist follow and a hands-free fallback | §5.6.2 | PORT |
| HV2-48 | Tablet category colours | — | DROP: they collide with the provenance palette. Equivalent: captions and position |
| HV2-49 | View toggles: Orbits, Labels, Info cards, Wind flow, Clouds, Scale rings, Ecliptic grid | `orbits`, the LABELS master, the inspector card, `winds`, `clouds`, `scaleRings`, `eclipticGrid` | PORT |
| HV2-50 | Population toggles: Asteroid belt, Kuiper belt, Stars, Exoplanets, Missions, Live probes | `belts` (one layer, both belts), `stars`, `exohosts`, `trails` and `spacecraft` | PORT |
| HV2-51 | Go-to lists: 18 bodies, all moons, 9 nearest stars (the "GJ 876" button opened GJ 1002) | GO destinations by catalogue id | FIX |
| HV2-52 | Desktop DOM: AR / VR buttons, magnification, elevation, go-to and mission selects, rewind / play / forward / Now | the panel on desktop; keys | PORT |
| HV2-53 | `logarithmicDepthBuffer` | one camera and per-group compression (binding 3) | REAL |
| HV2-54 | Floating origin `rigKm` and render scale `scaleMK` | `FloatingOrigin` | EXISTS |
| HV2-55 | Stars beyond STAR_FAR clamped to a sky shell | the sky layer at infinity (binding 3) | EXISTS |
| HV2-56 | Remote textures (threex.planets, three.js examples) | — | DROP: runtime network and unclear licences (Planet Pixel Emporium is not public domain). Equivalent: NASA and USGS maps baked offline |

### B.3 Heliosphere V1 (`solar-xr.html`), where it differs from V2

| Id | Source feature | CosmoSynth 2.0 home | Status |
|---|---|---|---|
| HV1-01 | Separate `immersive-ar` / `immersive-vr` sessions | one session with the ROOM fade | FIX |
| HV1-02 | Tablet bolted rigidly to the left grip at a fixed tilt | the smooth-follow wrist panel | FIX |
| HV1-03 | Provenance line "Positions from JPL Keplerian elements, valid 1800-2050. Earth spin uses true ERA." | INFO -> THIS VIEW ephemeris text and the EPHEMERIS chip | PORT |
| HV1-04 | Moons kept in the non-rotating pole frame (correct, unlike V2) | Horizons moons | REAL |
| HV1-05 | Flat spectral star colours, marker-only probes, Apollo from a fixed ecliptic point | superseded by HV2-25, HV2-31, HV2-36 | FIX |
| HV1-06 | Scale presets and a magnification select | as HV2-10 and HV2-40 | PORT |

### B.4 SOL-XR Vitals (`sol-xr-vitals.html`)

| Id | Source feature | CosmoSynth 2.0 home | Status |
|---|---|---|---|
| VIT-01 | Boot screen: ENTER OBSERVATORY / AMBIENT SUN | the Sun inspector's VITALS page; TOOLS -> Clean view | PORT |
| VIT-02 | Five imagery feeds with channel buttons (SUVI 6, AIA 8 plus HMI 2, LASCO C2/C3, EIT 4) | all 22 channels as baked stills with timestamps, channel buttons, an "as seen from Earth" billboard; **deferred** (D5) | PORT (offline, deferred) |
| VIT-03 | Animations (SUVI, HMI, LASCO, Enlil) with play/pause, a scrubber, 4/8/12/20 fps, arrow keys | one 24-frame SUVI 304 loop with the same controls; **deferred** (D5) | PORT (one loop, deferred) |
| VIT-04 | WSA-Enlil solar wind model animation | — | DROP: an annotated forecast plot; in 3D it would be fiction. Equivalent: the Parker spiral driven by the measured L1 speed |
| VIT-05 | Preload progress; 60 s still refresh; 5-minute cache-bust | — | DROP: runtime network. Equivalent: baked at build time, "as of &lt;UTC&gt;" on every card |
| VIT-06 | GOES X-ray class, flux, a 6-hour log plot, the latest flare | the snapshot card with the class, flux, curve and flare | PORT (snapshot) |
| VIT-07 | Solar wind at L1: speed, density, temperature, Bt, Bz with alert colours; a 24-hour speed plot | the snapshot card from the RTSW endpoints (two of the page's endpoints are 404); alerts as `!` / `!!` in Warning | FIX |
| VIT-08 | Kp, 24 bars, the G-scale | the snapshot card | PORT |
| VIT-09 | Protons at or above 10 MeV and the S-scale | the snapshot card | PORT |
| VIT-10 | F10.7, active regions, sunspots, the latest alert | the snapshot card, plus the monthly record since 1749 driving the Sun at any date | PORT (extended) |
| VIT-11 | "All feeds fetched live ... nothing is simulated" | "Recorded &lt;UTC&gt; ... Not live." | FIX (offline) |
| VIT-12 | Each poller fails soft to a status line | failure-visible rows (§5.10) | PORT |
| VIT-13 | Amber-on-black monospace dashboard style | — | DROP: styling; amber is the Procedural colour. Equivalent: `CosmoTheme` |

### B.5 Celestial Tray (`planetsview.html`)

| Id | Source feature | CosmoSynth 2.0 home | Status |
|---|---|---|---|
| TRAY-01 | A glass specimen tray with ten bodies at 3.5 cm radius | TOOLS -> SPECIMEN TRAY, twelve specimens, "MODEL COPY" (§5.9.7) | PORT |
| TRAY-02 | Grab a body: it grows to 0.22 m; release near the tray: it re-docks | the same, plus a 10 s return when released elsewhere | PORT |
| TRAY-03 | Grab the handle to carry the tray | the same | PORT |
| TRAY-04 | Everything spins at 0.004 rad per frame | the real IAU rotation at the simulation time; turn a held specimen by hand | FIX (frame-rate dependent) |
| TRAY-05 | A placeholder Moon texture on the Sun and giants; Mars and Venus URLs 404 | real or procedural surfaces with provenance glyphs | FIX |
| TRAY-06 | Displacement 0.06 (heavy exaggeration) | RELIEF x1 / x10 / x50 | PORT |
| TRAY-07 | "Start Solar Tray" button | TOOLS -> SPECIMEN TRAY -> Show | PORT |
| TRAY-08 | (new) true relative size | Equal / Readable / True size modes | added |

### B.6 Astro-Volume N-body (`orbital_simulator.html`)

| Id | Source feature | CosmoSynth 2.0 home | Status |
|---|---|---|---|
| NB-01 | Launch sandbox, "true time-synced integration" | the real sandbox: a restricted N-body in the real ephemeris field (§5.9.3) | FIX |
| NB-02 | Planets Mercury to Saturn on circular coplanar orbits with random phases | the real ephemerides | REAL |
| NB-03 | Launch from Earth's surface with 0–45 km/s injection, x0.1 / x10 | delta-v 0.1–45 km/s (log pull), x0.1 / x10 buttons | PORT |
| NB-04 | Launch latitude and longitude in a prograde / north / zenith basis | direction sliders in prograde / normal / radial | PORT |
| NB-05 | Semi-implicit Euler, fixed 0.016 s frame step, up to 8,192 substeps | adaptive Dormand-Prince 5(4) in double | FIX |
| NB-06 | Probe dropped beyond 40 AU or within 0.003 AU of the Sun; passes through planets | impact and SOI events; no arbitrary drop; an edge arrow when out of view | FIX |
| NB-07 | Trails "10 s fade" / "Infinite" | the same two modes, a ring buffer | FIX (unbounded array) |
| NB-08 | "Focus Visuals" exaggerated versus true sizes | the SIZES presets | PORT |
| NB-09 | "Gravity well" funnel grid | the potential diagram from the real potential, labelled DIAGRAM | FIX |
| NB-10 | Time buttons 0x–1000x | the global warp ladder | PORT |
| NB-11 | Status "Escape V: Earth = 11.2 km/s, Solar = ~42 km/s" | computed escape speeds in the sandbox readout | PORT |
| NB-12 | AR: both triggers pinch-scale and move | two-hand scale and grab | PORT |
| NB-13 | "Inject into orbit" button | LAUNCH, and the pull gesture | PORT |
| NB-14 | "Start Passthrough" button | the app is passthrough-first | EXISTS |

### B.7 Orbital Slingshot v33 (`spacecraft.html`)

| Id | Source feature | CosmoSynth 2.0 home | Status |
|---|---|---|---|
| SLG-01 | Five random textured planets, GM proportional to radius | the arcade slingshot (a Fictional stage, projector skin), the seed shown, "New layout" | FIX |
| SLG-02 | Pinch-and-pull impulse `min(\|pull\| * 4.8, 1.8)` | arcade: the same; the real sandbox: a log delta-v mapping | PORT |
| SLG-03 | Pinch hysteresis 22 / 45 mm and the release buffer | both sandboxes use it | PORT |
| SLG-04 | A live predicted trajectory (orange, 4.8 s ahead), re-aim in flight | a prediction ribbon from the flight integrator | FIX |
| SLG-05 | 64 substeps of symplectic Euler with softening | one fixed-step integrator for prediction and flight, frame-rate independent | FIX |
| SLG-06 | "Surface lock" landing; relaunch pushes out 0.12 m with 0.5 s grace | the same | PORT |
| SLG-07 | A neon trail of 400 points | the same, a ring buffer | PORT |
| SLG-08 | Reset beyond 10 m | the same (arcade); an edge arrow (real sandbox) | PORT |
| SLG-09 | An overlay with status text and instructions | the generated hint line and the sandbox readout | PORT |

### B.8 Holographic Star Map (three variants)

| Id | Source feature | CosmoSynth 2.0 home | Status |
|---|---|---|---|
| HSM-01 | The eight-node system (ids, names, colours, radii, a, b, inclination, speed) | the VG-429 Holosystem Fictional catalogue (§5.3, Appendix C.13) | PORT |
| HSM-02 | Centred ellipses at uniform angular speed, advanced per frame | Kepler conics with authored e = 0.02–0.05, periods from Kepler III, a sector clock in seconds | FIX |
| HSM-03 | Header "SYS_MAP // MARVEL_COSMOS" and the reference-image comment | — | DROP: IP. Equivalent: the system's own name, "VG-429 Holosystem" |
| HSM-04 | Star glow: centre-bright fresnel (base), rim-bright (1)(2), a glow sprite, a plus/minus 5 % pulse | a rim-bright glow shader with the pulse, in the projector skin | PORT |
| HSM-05 | Wireframe overlays and wire rings on five nodes | ring flags from the base file; the wireframe as a skin option on the star | PORT |
| HSM-06 | Labels: boxed sprites / bracket sprites / fixed 3D planes with pointer lines | bracket labels with pointer lines, upright billboards, the Fictional hue and the `?` glyph | FIX (mirrored planes) |
| HSM-07 | Paths: dashed pulse trails (base), helical thread bundles (1), twin helical threads (2) | a flow-filament ribbon on each Kepler orbit; Orbit style Pulse / Bundle / Twin helix | PORT |
| HSM-08 | Projector pad: rings, grid, an octagonal plate, a border, an emitter, an upward beam | the projector skin, reserved for non-real content | PORT |
| HSM-09 | Cosmic dust particles (200–350 points) | sector dust (part of the fiction) | PORT |
| HSM-10 | "PLANETARY METRICS" panel with random velocity and a meaningless radius | — | DROP: random numbers presented as metrics. Equivalent: the Fictional inspector with authored values consistent with the Kepler orbit |
| HSM-11 | A button toggling the trajectory flow | sector LAYERS -> Orbit flow | PORT |
| HSM-12 | Slow auto-rotation of the hologram | the Turntable toggle (0.03 rad/s) | PORT |
| HSM-13 | `immersive-ar`, the hologram 1.2 m ahead at scale 0.8 | the sector opens on the anchor at x0.3 (0.8 m) | PORT |
| HSM-14 | Variant (2): trigger grab near the base, one-hand move and yaw, two-hand scale 0.15–2.5 | stage grab, twist and scale with the sector range 0.15–2.5 | FIX (Euler yaw) |
| HSM-15 | Desktop OrbitControls | desktop fly and mouse | PORT |
| HSM-16 | 1-px lines and points; additive blending that writes alpha | ribbons and sprites; additive without alpha (binding 2) | FIX |

### B.9 3D Astrophysical Nebula Gallery

| Id | Source feature | CosmoSynth 2.0 home | Status |
|---|---|---|---|
| NEB-01 | Three targets: Orion M42 plus the Trapezium, the Local Bubble, Crab M1 | GO destinations and the `nebulae` close-ups at real positions (Procedural over Measured) | PORT |
| NEB-02 | 64^3 volumes from analytic fields and fBm | the same three field functions in Core, uploaded as a 3D texture | PORT |
| NEB-03 | Front-to-back raymarch with early exit | the close-up shader with a step budget and per-eye ray origins | FIX |
| NEB-04 | Layer toggles Gas / Dust | `nebulae.gas`, `nebulae.dust` | PORT |
| NEB-05 | Layer toggle Stars ("Gaia Stars", up to 8,000 random points) | — | DROP: random points labelled "Gaia DR3". Equivalent: the real `stars` layer (about 120 k measured stars) |
| NEB-06 | Layer toggle B-Fields (80 random helices) | — | DROP: invented field lines presented as data; no measured 3D field exists offline. Equivalent: none; listed under "What we do not know here" |
| NEB-07 | Sliders: ray steps, density, star count | the Quality preset; `nebulae.density` (with a chip); star count becomes the OBSERVED SKY limit magnitude | PORT |
| NEB-08 | "Spectrometric probe" (atoms/cm^3 from camera distance) | — | DROP: fabricated numbers. Equivalent: the inspector's true distance and per-field provenance |
| NEB-09 | Static spectral bars (H-alpha, [O III], H-beta, extinction) | — | DROP: never updated, not data. Equivalent: the close-up's colour legend in INFO (what each channel means, Procedural) |
| NEB-10 | "Spectroscopic Doppler velocity mapping" mode (unreachable) | — | DROP: no velocity field exists and the mode was unreachable |
| NEB-11 | "AI Astro-Analyst" Gemini chat with quick prompts | — | DROP: runtime network. Equivalent: authored text per object in INFO |
| NEB-12 | Parsec grid | `cylGrids` / `planeGrid` | PORT |
| NEB-13 | "MR Enter" | the app is MR | EXISTS |
| NEB-14 | Numbers: Crab pulsar 30.2 Hz, Local Bubble 150 pc | Crab 29.6 Hz (2017 value); the Local Bubble about 300 pc and peanut-shaped (Zucker 2022) | FIX |
| NEB-15 | Crab pulsar spin | the pulsar data voice (§5.9.8) | PORT |

### B.10 Cosmic Modular Synthesizer

| Id | Source feature | CosmoSynth 2.0 home | Status |
|---|---|---|---|
| SYN-01 | VCO: 40–800 Hz, fine plus/minus 50 cents, FM depth, four waveforms | the `Core/Audio` oscillator; Sound lab sliders | PORT |
| SYN-02 | LFO 0.02–15 Hz, depth | the Core LFO; Sound lab | PORT |
| SYN-03 | Pink noise (Kellet filter) | Core pink noise, tested | PORT |
| SYN-04 | VCF low-pass: cutoff, Q, modulation depth | the low-pass filter, sweep kept above 60 Hz | FIX |
| SYN-05 | VCA | the gain stage | PORT |
| SYN-06 | Delay: time, feedback, mix | the echo filter | PORT |
| SYN-07 | Convolution reverb: decay, mix | the reverb filter (no IR regeneration on every knob move) | FIX |
| SYN-08 | Scope | the Sound lab scope strip | PORT |
| SYN-09 | Master volume and limiter | master volume and a soft limiter | PORT |
| SYN-10 | Preset "Soundscape for sleeping at the edge of the universe" | the ambient bed (§5.9.8), detune applied | FIX |
| SYN-11 | Drag-and-drop patching with bezier cables, delete module, clear grid | — | DROP: a 2D DOM patch editor, not a function of the universe. Equivalent: the fixed chain with every module parameter exposed (Sound lab) |
| SYN-12 | Audio on/off; "Explore visuals only" (silent start) | SOUND -> Volume and mute (persisted) | FIX |
| SYN-13 | A reference panel and welcome modal explaining the patch | INFO -> SOUND "What you hear" (ambient versus data voices, with provenance) | PORT |
| SYN-14 | Credits to Bitwig Poly Grid and Polarity Music | — | DROP: third-party names for a homage (IP caution). Equivalent: the preset's own name, "Edge of the universe" |

### B.11 Cosmic Atlas XR

| Id | Source feature | CosmoSynth 2.0 home | Status |
|---|---|---|---|
| CAX-01 | At the event horizon: Keplerian flow, 1 : 1.5 : 3 radii, mass 5–50 M_sun | exhibit 1; host Sgr A* | PORT |
| CAX-02 | When black holes meet: prescribed inspiral, strain, ringdown, q 0.25–1 | exhibit 2; the strain sent to audio | PORT |
| CAX-03 | Gravity bends the view (thin lens), beta/theta_E 0–1.5 | exhibit 3; host: galaxy clusters | PORT |
| CAX-04 | A galactic encounter: a 514-particle N-body capture, trail persistence | exhibit 4; the capture shipped with its SHA-256; host M31 | PORT |
| CAX-05 | From ripples to structure: Zel'dovich displacement on 19^3 points, amplitude 0.3–1.3 | exhibit 5; host: the `cosmicweb` rung | PORT |
| CAX-06 | The invisible gravitational halo: rotation curves, halo 0–2 | exhibit 6; host: the Milky Way | PORT |
| CAX-07 | Inside a living star: shells at 0.23 / 0.72 R, granulation, cutaway 0.35–1.8 rad | exhibit 7; host: the Sun | PORT |
| CAX-08 | Magnetic fields let go (reconnection), rate 0.3–1.5 | exhibit 8; host: the Sun | PORT |
| CAX-09 | A wave carried by magnetism (Alfven), B0 0.5–2 | exhibit 9; host: the Sun | PORT |
| CAX-10 | Three named layers per exhibit | the exhibit LAYERS toggles | PORT |
| CAX-11 | Texts: explanation, equation, question, observation, method title, method, notes, source | `exhibits.json`, verbatim; the INFO pages | PORT |
| CAX-12 | A collection of nine in three families | GO -> PHENOMENA by family | PORT |
| CAX-13 | Detail pages "Observe" and "Model & limits" | INFO -> STAGE, the same two pages | PORT |
| CAX-14 | Timeline slider (scrub pauses), Play/Pause, Restart, speed 0.25/0.5/1/2, play stops at the end | the TIME tab in the exhibit stage | PORT |
| CAX-15 | Smaller / Larger (0.25–1.6) | R stick zoom and two-hand scale within the same range | PORT |
| CAX-16 | Recenter | TOOLS -> Recentre; left menu hold | PORT |
| CAX-17 | Previous / Next | the GO list and the header chip's prev/next | PORT |
| CAX-18 | Exit XR | "Back to real universe" (the app itself is XR) | PORT |
| CAX-19 | Squeeze to move the workspace; right stick scales; left stick rotates | grip grab; R stick Y scale; L stick X yaw, declared with the exhibit context | PORT |
| CAX-20 | Tappable labels open notes | the same | PORT |
| CAX-21 | Status "PAUSED / CAPTURE PLAYBACK / LIVE MODEL" | the exhibit status chip, verbatim | PORT |
| CAX-22 | The capture's SHA-256 verified in the browser | verified at load; the exhibit disabled on a mismatch | PORT |
| CAX-23 | Desktop: orbit drag, scroll zoom, tap labels, Space pauses, arrow keys change exhibit, R resets | the desktop bindings in the exhibit context | PORT |
| CAX-24 | A white studio VR mode and white panels | — | DROP: a styling choice that conflicts with the passthrough-and-space look. Equivalent: passthrough (the user's own room) or Space |
| CAX-25 | Placement 1.9 m ahead, 0.2 m down, scale 0.67 | the plinth at the host's position or 0.9 m ahead | FIX (desktop-derived numbers) |
| CAX-26 | 1-px lines, GL points, per-frame CPU vertex rewrites | ribbons, star sprites, vertex-shader motion | FIX |
| CAX-27 | Family content colours | exhibit content colours (not UI colours) | PORT |

### B.12 Adjacent pages

| Page | Disposition |
|---|---|
| `dummyartemis.html` (0.1 m Moon, fake craters, a mis-tilted Orion loop) | REAL: Artemis I from Horizons (-1023) and the LROC Moon; the page itself is a placeholder |
| `interactive_mars_globe_moons.html` | relief becomes the RELIEF presets; the laser "GPS" becomes the surface probe (§5.9.9); landmarks and landers become `sites` with cross-checked coordinates; the decorative moons become Horizons Phobos and Deimos as triaxial, tidally locked ellipsoids; the ILLUM toggle is unnecessary (real lighting); the wrist EXAG/MOONS/ILLUM/RESET become RELIEF, layers and Recentre |
| `cosmic-portal-test.html` plus `js/cosmic-portal-original.js` | DROP: fictional eye candy with no information, desktop only, and its test page loads a missing file. Kept idea: the expanding pulse ring as the rung-change transition cue |
| `arcane_astral*.html` | not star or cosmos content; not ported |
| `~/projects/StarHolo`, `StarHoloXR` | not ported as scenes (a starship HUD, URP, IP-flavoured terms); the mesh helpers and noise maths are donor code for the projector skin and the stellar-interior granulation |
| `earth-xr-vitals.html` (73 KB), `earth-xr-vitals-weathernext.html` (76 KB), `earthwindXR.html` (52 KB), `earthWindXR2.html` (54 KB), `terra_deck.html` (45 KB), `ares-atlas-mars-mr.html` (57 KB) | **Unassessed (INFERRED — nobody has opened these files.)** Over 350 KB of source classified from filenames alone, at the Earth end of a ladder the user's request begins with ("from earth to solar system to ..."). **The `solar` lane opens each one in its 1a half and appends a B.14 sub-table of feature rows with statuses**, before the Phase-2 content freeze; any feature that is a function of the universe rather than a live 2D weather read is scheduled or explicitly deferred with a reason. **Until then this contract makes no coverage claim about them, and B.13's totals exclude them.** D12 is decided against that table (R49) |

### B.13 Totals

| Source | Features | PORT | FIX | REAL | EXISTS | DROP |
|---|---|---|---|---|---|---|
| Milky Way Atlas | 48 | 21 | 20 | 2 | 3 | 2 |
| Heliosphere V2 | 56 | 28 | 8 | 8 | 9 | 3 |
| Heliosphere V1 | 6 | 2 | 3 | 1 | 0 | 0 |
| SOL-XR Vitals | 13 | 8 | 2 | 0 | 0 | 3 |
| Celestial Tray | 7 | 5 | 2 | 0 | 0 | 0 |
| Astro-Volume N-body | 14 | 7 | 5 | 1 | 1 | 0 |
| Orbital Slingshot | 9 | 6 | 3 | 0 | 0 | 0 |
| Holographic Star Map | 16 | 10 | 4 | 0 | 0 | 2 |
| Nebula Gallery | 15 | 6 | 2 | 0 | 1 | 6 |
| Cosmic Modular Synthesizer | 14 | 8 | 4 | 0 | 0 | 2 |
| Cosmic Atlas XR | 27 | 24 | 2 | 0 | 0 | 1 |
| **All eleven** | **225** | **125** | **55** | **12** | **14** | **19** |

The six Earth and Mars pages of B.12 are **not** in these totals, because nobody has read them
(R49).

### B.13a Designed here, not in 2.0.0

The status column above says where a feature is **designed**. It does not say which release ships
it, and reading it as a coverage answer for 2.0.0 was wrong for twenty rows (R39). Every one of
them is listed here with the release it lands in; each shows "not in this build" with its reason
in the app, and **none is silently missing**.

| Rows | Feature | Not in 2.0.0 because | Lands in |
|---|---|---|---|
| SYN-01..14 (14) | the whole Cosmic Modular Synthesizer: ambient bed, sound lab, the UI click sounds | the `audio` lane is Phase 3, after 2.0.0 ships (§9.2). TOOLS -> SOUND is one row reading "Sound: not in this build" (§5.9.8) | the first build after 2.0.0 with audio |
| NEB-15 (1) | the Nebula Gallery's ambient audio | the same lane | the same |
| — | exhibit 2's strain sent to audio (part of CAX-02's design) | the same lane | the same |
| VIT-02, VIT-03 (2) | the Sun disk stills and the SUVI 304 loop | **user decision D5**, deferred; the VITALS row reads "Solar images: not in this build" | after 2.0.0, on the user's word |
| HV2-13, TRAY-06 (2) | RELIEF elevation exaggeration | DEMs deferred to Phase 3 for APK size (§6.9.2 G8); RELIEF says "no elevation data for &lt;body&gt;" | Phase 3 |
| HV2-19 (1) | zonal wind profiles | deferred to Phase 3 (§6.9.2 G6); `winds` is off by default and reads "not in this build" | Phase 3 |

**NB-01..14 and SLG-01..09 are *in* 2.0.0** and were the reason this list is not longer: they had
no owner at all until R38 gave both sandboxes and `StageDirector` to lane `fiction`'s 1b second
half and raised its effort from 3.0 to 5.0 agent-days (§9.2, §9.5). Had they stayed unowned, 43
rows would be missing rather than 20.

"EXISTS / FIX" counts as FIX; "PORT (labelled)", "PORT (opt-in)", "PORT (offline)", "PORT
(snapshot)" count as PORT. The Tray's "added" row is not a source feature and is not counted.

**Every drop, in one list:** MWA-08 page stylesheet; MWA-39 category colours; HV2-35 Venera 7
waypoints; HV2-48 tablet category colours; HV2-56 remote textures; VIT-04 Enlil; VIT-05 live
polling; VIT-13 dashboard style; HSM-03 the Marvel header; HSM-10 the random metrics panel;
NEB-05 fake Gaia stars; NEB-06 random B-fields; NEB-08 the fabricated probe; NEB-09 static
spectral bars; NEB-10 the unreachable Doppler mode; NEB-11 the LLM chat; SYN-11 the patch editor;
SYN-14 third-party credits; CAX-24 the white studio mode. Five are styling, eight are fabricated
or data-less content presented as real, three need the runtime network, two are IP, one is a 2D
editor. Each names its honest equivalent.

**The honest closing statement** (R39). Nineteen features are **dropped**, each with its reason and
its honest equivalent. A further **twenty are designed here but deferred past 2.0.0** (audio, the
solar imagery, DEM relief, wind profiles — B.13a); each shows "not in this build" with the reason,
and none is silently missing. **No working astronomy function of any page is dropped outright**;
three are deliberately converted from live network reads to dated offline snapshots (VIT-05's
polling, VIT-02/03's imagery, HV2-20), which is an honest FIX and is not "nothing lost". And six
adjacent Earth and Mars pages have never been read, so no claim is made about them at all (B.12,
R49).
---

## Appendix C — Dataset specifications

Each subsection gives, in order: the source (exact URL), its licence tag and fallback; the
shipped layout; the importer; the Core type and its tests; the provenance of each field; the
range assertions. Conventions are §6.1–§6.3.

### C.1 Stars — `sky/stars.csstars` (`CSS2`), lane `stars`

**Sources.** Both by astronexus (D. Nash) under CC BY-SA 4.0.

| id | URL | What we take |
|---|---|---|
| `athyg` | `https://codeberg.org/astronexus/athyg/media/branch/main/data/subsets/hyglike_from_athyg_v40.csv.gz` (14,096,722 B, 119,670 rows, VERIFIED) | `x,y,z` (pc, ICRF, epoch J2000.0), `dist`, `dist_src`, `vx,vy,vz` (pc/yr), `rv`, `hip`, `hyg`, `proper`, `con`, `bf` |
| `hyg44` | `https://codeberg.org/astronexus/hyg/media/branch/main/data/hyg/CURRENT/hyg_v44.csv.gz` (13,636,362 B, 119,614 rows, VERIFIED) | `mag` (V), `ci` (B-V), `spect`, joined on `hyg44.id == athyg.hyg` |

**Join rule** (C9, VERIFIED by probe): the 119,614 matched rows (1:1, every `hip` agreeing) take
`mag`, `ci` and `spect` from HYG v4.4; the 56 AT-HYG-only rows keep AT-HYG's `mag` and set flag
3. AT-HYG's magnitudes have median |delta| 0.076 mag, p99 0.64, maximum 4.6, and 3,830 rows
differ by more than 0.3 mag, all of them `mag_src = T`. Sol (row 0) is excluded, as today.

**Records.** 119,669 rows (plus up to about 100 from the optional census), **sorted by `vmag`
ascending**, NaN last. The prefix property is kept: the runtime still takes "the brightest N" as
a length.

| Column | Type | Unit | Content |
|---|---|---|---|
| `pos` | f32 x3 | pc | ICRF at epoch J2000.0. Unknown-distance stars keep AT-HYG's sentinel position; the direction is exact |
| `vel` | f32 x3 | km/s | ICRF space velocity = AT-HYG `v*` x 977,792.2 (1 pc/yr in km/s). Zero if `vx` is blank |
| `vmag` | f32 | mag | V (HYG v4.4, or AT-HYG when flag 3) |
| `teff` | f32 | K | Ballesteros 2012 from B-V, else the spectral-class table; clamped 1,000–60,000 |
| `av` | f32 | mag | **A_V along Sun -> star, from the dust cube at bake time** (R41). 0 outside the cube, with flag 12 `ExtinctionUnknown` set. `M_V = vmag - 5 log10(d/10 pc) - av`, computed in Core, provenance Inferred ("de-reddened with Vergely+2022; A_V unknown outside the cube, flagged") |
| `hip` | u32 | — | Hipparcos number, 0 if none. Used for picking, labels and joins |
| `flags` | u32 | — | below |

**Flag bits:** 0 `DistanceUnknown` (`dist >= 100000` or blank: **146 rows**, VERIFIED);
1 `TemperatureFromSpectrum`; 2 `HasProperName`; 3 `PhotometryFromTycho`;
4 `RadialVelocityMissing` (20 rows); 5 `FromNearbyCensus`; 6 `NoVisualMagnitude` (`vmag` NaN);
7 `VelocitySuspect` (computed |vel| above 1,000 km/s, almost always a bad parallax; `vel` is
zeroed and the count must stay at or below 0.5 % of rows); 8–10 a `DistanceSource` code
(0 unknown, 1 Gaia DR3, 2 Gaia DR2, 3 Hipparcos, 4 Gliese, 5 other, 6 census); 11 `IsFlareStar`
(G7); **12 `ExtinctionUnknown`** (R41: no dust cube covered this star, `av` is 0 and the
de-reddening is a no-op, so the inspector says so rather than implying A_V = 0 was measured). `StarFlags` in Core keeps bits 0–2 at their current values (VERIFIED) and adds the rest.
`StarCatalog` gains `Velocity`, `Hip` and `PositionAt(epochYears)` (linear motion from J2000.0,
exact enough for plus or minus 1e5 yr, INFERRED).

**Size.** **44 B x 119,669 = 5.27 MB** plus about 6 KB of meta; deflated about 4.7 MB (INFERRED).
The `av` column adds 0.48 MB and is what makes extinction correct rather than double-counted.

**The 10 pc census is a SEPARATE FILE** (R40). `sky/census.cspt` (`CSPT`: `pos` f32 x3 pc ICRF,
`teff` f32, `vmag` f32 NaN, `objType` u8, `flags` u32), kind `census`, tag `nc`, **never merged
into `stars.csstars`** — that file is a CC BY-SA 4.0 adaptation, and BY-SA requires the adaptation
to be offered under BY-SA, which permits commercial use, so an NC source cannot live in it and the
ledger's `licenceTags` for it would have stated something untrue (§6.2.2's no-mixed-tags rule).
Two further consequences the merged form broke: dropping the census is now **one file removed**,
which `drop_test.sh` can actually prove (binding 7), instead of a re-bake it could not see; and
the `census` layer now has **its own kind**, so with the census absent §4.3's "a kind absent from
the build is never delivered" fires and the panel row reads **"not in this build (licence)"**
instead of reading `on` and drawing nothing. The 0.02 pc duplicate rule becomes a de-duplication
**flag in the census file** against CSS2 rows, computed at bake time.

**Census step** (`--census`, default on; tag `nc`, dropped under `commercial`): Reylé+2021,
`https://cdsarc.cds.unistra.fr/ftp/J/A+A/650/A201/tablea1.dat.gz` (77 KB, 562 rows, VERIFIED).
Byte columns ObjType 41–46, RAdeg 79–91, DEdeg 94–106, plx 115–122, SpType 302–309, Gmag 353–361.
Selection: `ObjType != planet`; the importer prints the distinct values and maps them (codes
INFERRED). Append only if no CSS2 star lies within **0.02 pc** in 3D (Reylé's epoch is J2016; the
radius absorbs 16 years of proper motion, INFERRED). `teff` from SpType with an L/T/Y table
(L0 2,200 K ... T8 700 K ... Y0 450 K, INFERRED; provenance "spectral type to Teff, weak");
`vmag` NaN plus flag 6, so they sort last and never enter a magnitude prefix; `vel` 0 plus flag 4.

**Importer** `build_starfield.py`, rewritten on `csdata`/`fetch`. It downloads both CSVs and
asserts the gzip magic and each header's first column (`id`); joins, computes, sorts, and writes
`stars.csstars`, `sky/census.cspt`, `stars_named.json` and `constellations.json` **in the same
run**; takes an optional **`--dust <path>`** and, with it, fills `av` from the cube along each
Sun-to-star line — without it, `av = 0` and flag 12 on every row, which is the normal case inside
the `stars` lane and is re-baked by the integrator at §6.9.1 step 10 (R41); prints the
Stellarium commit SHA it used; and writes `TC/cache/stars.index.json` (`hip` to row) — **not
shipped**, and the ledger ignores `cache/`.

**Core** `Core/Sky/StarCatalog.cs`, rewritten on `CsData`, keeping `Parse(bytes, maximumStars)`,
`CountBrighterThan`, `ColorFor`, the prefix read and the magnitude-sort check (all VERIFIED
present). A `CSS1` file is refused by name.

**Tests (`StarCatalogTests`, rewritten):**

| Check | Expected | Tolerance and why |
|---|---|---|
| Row 0 is Sirius (HIP 32349) | V -1.44 | plus/minus 0.05: HYG prints to 0.01 mag and the literature V is -1.46 |
| Sirius distance | 2.637 pc | plus/minus 0.011 pc: 3 sigma of Hipparcos 2007's 1.58 mas error at 379 mas |
| **Barnard's Star** (HIP 87937) distance | 1.8282 pc | plus/minus 0.002: Gaia DR3 546.98 mas, so this proves `dist_src` G_R3 flows through |
| **Barnard declination** | +4.6933 deg | **plus/minus 0.001 deg. The J2016 value 4.7395 must fail.** This pins the epoch |
| Barnard space speed | 142.5 km/s | plus/minus 0.5: tangential 4.74047 x 10.3933"/yr x 1.8282 pc = 90.07 km/s; radial -110.47 |
| Proxima (HIP 70890) | 1.302 pc, proper name "Proxima Centauri" | plus/minus 0.001 |
| Stars at V at most 6.0 | 5,070 | plus/minus 2 %: allows photometric revisions between HYG releases and nothing more |
| `DistanceUnknown` | at most 200 (146 today) | each has `DistanceParsecs == null` and a unit direction |
| Every row finite | `pos`; `teff` in (500, 100000); \|vel\| under 1,000 km/s; bit-7 rows at most 600 | disc and halo stars move under 600 km/s relative to the Sun, so anything faster is an astrometric error (INFERRED bound) |
| Prefix read equals full read; malformed files refused | — | the existing tests, kept |

**Importer assertions:** rows = 119,669 (the census is its own file now); the HYG join is at least
99.9 %; sorted order; with `--dust`, `0 <= av <= 10` on every row and the fraction with flag 12
recorded in `counts`;
no V brighter than -1.5 and none fainter than 21.5; `|pos|` under 350,000 pc.

**Provenance:** `pos` direction Measured ("AT-HYG v4.0 (Tycho-2 / Hipparcos / Gliese positions),
epoch J2000"); `pos` distance Measured by code ("Gaia DR3 parallax", "Hipparcos 2007 parallax",
"Gliese") or Unknown when flag 0, `Uncertainty` NaN (AT-HYG ships no parallax error); `vmag`
Measured ("HYG v4.4 V (Hipparcos/Yale/Gliese)"; flag 3: "Tycho-2 VT to V via AT-HYG"); `teff`
Inferred ("Ballesteros 2012 from B-V"; flag 1: "spectral class only, weaker"); `vel` Inferred
("proper motion + parallax + radial velocity (AT-HYG)"; flag 4: "tangential only, RV unknown").

### C.2 Names and constellations — `sky/stars_named.json`, `sky/constellations.json`, lane `stars`

**`stars_named.json`.** The same schema as today (`source`, `sourceUrl`, `licence`, `generated`,
`note`, `stars[]`), with `hip` added to each entry, a new top-level `starsGenerated` equal to the
stars meta `generatedUtc`, and a boolean `flare` per entry (G7). Names come from AT-HYG `proper`:
**640** (VERIFIED, against 548 in HYG v4.4, because AT-HYG follows the current IAU WGSN list,
INFERRED). `constellation` comes from `con`, `designation` from `bf`. About 75 KB.

**`constellations.json`.** Source: Stellarium `skycultures/modern/index.json`, pinned. The
importer resolves the latest commit SHA through
`https://api.github.com/repos/Stellarium/stellarium/commits?path=skycultures/modern/index.json&per_page=1`
and fetches
`https://raw.githubusercontent.com/Stellarium/stellarium/<sha>/skycultures/modern/index.json`
(today's master is 205,767 B). Licence: "Text and data: CC BY-SA 4.0" (VERIFIED), tag `by-sa`.
Probe findings (VERIFIED): 88 constellations, each with `id` (`"CON modern Aql"`), `lines` (HIP
polylines) and `common_name`; **695 segments**; **710 unique HIP numbers, all 710 present in
AT-HYG**; 659 `common_names` entries keyed `"HIP n"`.

```json
{ "source": "Stellarium modern sky culture", "sourceCommit": "<sha>", "licence": "CC BY-SA 4.0",
  "attribution": "...", "starsGenerated": "<stars meta generatedUtc>", "starsRows": 119669,
  "figures": [ { "abbr": "Aql", "iau": "Aquila", "english": "Eagle",
                 "pairs": [[1203, 877]],          // CSS2 row indices, one pair per segment
                 "labelStar": 877 } ] }           // the brightest star in the figure (lowest vmag)
```

The label goes on the direction of `labelStar`, not the figure's 3D centroid. The figure lines
are drawn Sol-centred; rendering owns what happens away from Sol.

**Core** `Core/Sky/Constellations.cs`: `Parse(json, StarCatalog)`, `Figures`.
**Tests (`ConstellationTests`):** exactly 88 figures and 695 segments at the pinned commit; every
index below `starsRows`; `starsGenerated` equals the stars meta; **Orion contains a segment
touching HIP 27989 (Betelgeuse) and one touching HIP 24436 (Rigel)**; `Cru`'s label star is
HIP 60718 (Acrux, V 0.77); **no segment longer than 60 deg on the sky** — a shifted index joins
random stars (INFERRED bound; the longest real segment is well under 60 deg).

**Provenance.** Segment endpoints are Measured (they are star positions). **The figure is a
cultural convention**: `Fictional`, source "Stellarium modern sky culture (authored convention)".

### C.3 Exoplanets — `catalog/exoplanets.csexo` (`CSEX`), lane `exoplanets`

**Source.** The NASA Exoplanet Archive TAP:

```
https://exoplanetarchive.ipac.caltech.edu/TAP/sync?format=csv&query=select <columns> from pscomppars
<columns> = pl_name,hostname,ra,dec,sy_dist,sy_vmag,sy_snum,sy_pnum,pl_orbsmax,pl_orbper,pl_orbeccen,
            pl_orbincl,pl_rade,pl_bmasse,pl_eqt,st_teff,st_rad,st_mass,st_lum,st_spectype,
            discoverymethod,disc_year,pl_controv_flag
```

**No `top` and no distance cut**: 6,360 planets and 4,769 hosts on 2026-09-11 (VERIFIED). Tag
`pd-ack` with the acknowledgement verbatim (Appendix D). `--max-systems 60` and
`exoplanets.json` are deleted, which also removes the misleading "within 40 pc" claim.

**Host table** (`rows` = hosts, sorted by `sy_dist`):

| Column | Type | Unit | Content |
|---|---|---|---|
| `pos` | f32 x3 | pc | ICRF from ra, dec, sy_dist |
| `vmag` | f32 | mag | `sy_vmag`, NaN if blank |
| `teff` | f32 | K | measured or NaN |
| `radius` | f32 | R_sun | measured or derived |
| `mass` | f32 | M_sun | measured or derived |
| `loglum` | f32 | log L_sun | measured or derived |
| `firstPlanet`, `planetCount` | u32, u32 | — | the range into the planet table |
| `hygRow` | i32 | — | the CSS2 row, or -1 |
| `flags` | u32 | — | 0 mass inferred (M-T); 1 radius inferred (M-R); 2 luminosity from Stefan-Boltzmann; 3 luminosity from M-L; 4 no Teff; 5 has a spectral type |

Meta holds `hostNames[]` and `spectralTypes{row: "G2 V"}`.

**Planet table** (`rows` = planets, grouped by host):

| Column | Type | Unit | Content |
|---|---|---|---|
| `host` | u32 | — | the host row |
| `a` | f32 | AU | measured or Kepler III |
| `period` | f64 | d | the published digits kept |
| `ecc`, `incl` | f32, f32 | —, deg | 0 or 90 when unpublished |
| `radius`, `mass` | f32, f32 | R_earth, M_earth | measured or a relation |
| `teq` | f32 | K | NaN if blank |
| `discYear` | u16 | — | |
| `method` | u8 | — | a code table in meta |
| `letter` | u8 | ASCII | e.g. `'b'` |
| `flags` | u32 | — | 0 a from Kepler III; 1 e assumed 0; 2 i assumed 90; 3 mass rejected as impossible (the density is in meta `rejections[]`); 4 mass from M-R; 5 radius from M-R; 6 controversial |

`pl_name` is `hostname + " " + letter` for every row the importer checks; any exception goes into
meta `planetNameOverrides{row: name}`. Size: hosts about 60 B x 4,769 = 0.29 MB; planets about
46 B x 6,360 = 0.29 MB; names about 0.07 MB.

**Importer** `build_exoplanets.py`, restructured and **keeping every derivation rule it has
today**: mass from temperature; radius from mass; luminosity by Stefan-Boltzmann or from mass;
the 30,000 kg/m^3 impossible-density rejection; `a` from Kepler III; e = 0 and i = 90 when
unpublished; the FNV-1a `stable_seed` and `slug` ids. **It stores the results plus bits saying
which rule fired**; Core never re-derives physics, it maps bits to provenance. It also joins
hosts to CSS2 rows by reading `stars.csstars` and `cache/stars.index.json`: a match is a star
within 0.05 pc of the host position with `|dV| < 1` mag where both are known.

**Core** `Core/Exo/ExoplanetTable.cs` (C19): `Parse`; `HostCount`, `HostPosition(i)`,
`HostName(i)`; `HostForStarRow(int)`, used by `NearbyStarSystems` to show real planets instead of
generating them; `Expand(i, CosmicTime epoch)` returning `List<CelestialBody>` with ids
`exo-<slug>`, orbit orientation Procedural from the same seed, and the provenance strings moved
into meta `provenanceText` so the wording lives in one place.

**Tests (`ExoplanetTableTests`, replacing `ExoplanetCatalogTests`):** at least 6,000 planets and
4,500 hosts; at least 450 hosts within 40 pc (471 on 2026-09-11) — the number the old file
misrepresented; **51 Peg b** period 4.2308 d plus/minus 0.0005; **TRAPPIST-1** exactly 7 planets,
host distance 12.4 plus/minus 0.2 pc; **Proxima Cen b** host 1.30 plus/minus 0.01 pc, period
11.19 plus/minus 0.02 d; no expanded planet denser than 30,000 kg/m^3; `Expand` of every host
passes `CelestialCatalog.Validate()` with 0 errors; seed parity — Core
`Hash.HashString("exo-51-peg-b") & 0x7FFFFFFF` equals the importer's value for 3 ids (a fixture
in the test; Python and C# FNV must agree).

**Importer range assertions:** `sy_dist` in [1.2, 30,000] pc with ra/dec present, else the host
is skipped; `st_teff` in [500, 60,000] K; `pl_orbper` in [0.09, 1e8] d; `pl_orbeccen` in [0, 1);
`pl_rade` in [0.2, 30] R_earth; `pl_bmasse` in [0.01, 30,000] M_earth. **A value out of range is
treated as "not published", which fires the relation and sets its bit. It is never clamped.**
Drop budget: at most 2 % of rows.

### C.4 Small bodies — `sun/smallbodies.cssb` (`CSSB`), lane `solar`

**Source.** The JPL SBDB Query API (tag `pd`). Counts VERIFIED by probe:

```
Q1  https://ssd-api.jpl.nasa.gov/sbdb_query.api?fields=spkid,full_name,a,e,i,om,w,ma,epoch,H,class,diameter
       &sb-kind=a&sb-cdata={"AND":["H|LT|14"]}&full-prec=true            -> 32,294 rows
Q2  ...same fields...&sb-class=TNO&full-prec=true                        ->  7,287 rows
Q3  ...same fields...&sb-class=CEN&full-prec=true                        ->  1,047 rows
```

The union is deduplicated by `spkid`, about 34 k objects (INFERRED: most TNOs and centaurs
already have H < 14). Jupiter trojans arrive through Q1 (the H < 14 part of the 16,391 TJN).
**Precision:** without `full-prec` the API returns display precision (Ceres `a = 2.766`);
`full-prec=true` is INFERRED, so the importer **asserts that Ceres (spkid 2000001) has at least 7
significant digits in `a`** and refuses otherwise. **Excluded:** any `spkid` already a catalogue
body in `solar_system.json` (Ceres, Vesta, Pallas, Eros, Bennu); the importer reads that file's
designations, so no body is drawn twice.

| Column | Type | Unit | Content |
|---|---|---|---|
| `elem` | f32 x6 | AU, —, rad, rad, rad, rad | a, e, i, Omega, omega, M0 (ecliptic J2000, as SBDB publishes them) |
| `epoch` | f32 | d past J2000 TDB | per row (most are 2461200.5 = 9655.5 d) |
| `H` | f32 | mag | absolute magnitude |
| `diam` | f32 | km | NaN if blank |
| `cls` | u8 | — | a code table in meta: MBA IMB OMB HUN TJN HYA CEN TNO APO ATE AMO IEO MCA PAA AST |

Mean motion is derived from Kepler III with `Constants.GM_Sun`, not stored; at f32, `a` is good
to 6e-8 relative, which is 1e-5 rad of phase after 20 orbits. Size 37 B x 34 k = about 1.26 MB.
The elements are ecliptic J2000; Core rotates them to ICRF with
`StandardFrames.EclipticToEquatorial` (the IAU 1976 obliquity, as Horizons uses), and the GPU
path uses the same matrix.

**Core** `Core/Orbits/SmallBodyTable.cs`: `Parse`, `Count`,
`PositionAu(row, CosmicTime) -> Double3` in ICRF using the existing `KeplerSolver`. The runtime
renders the belt from the same columns on the GPU; the Core function is the reference the render
test compares against.

**Tests (`SmallBodyTests`)** — physics known answers, not file echoes:

| Check | Expected | Why |
|---|---|---|
| **3:1 Kirkwood gap** | the MBA count density in 2.495–2.510 AU is under 30 % of the mean density over 2.40–2.47 and 2.55–2.62 AU | the resonance sits at 5.2029 x (1/3)^(2/3) = 2.502 AU (VERIFIED arithmetic) |
| **2:1 gap** | the same test at 3.270–3.290 against 3.15–3.25 and 3.31–3.40 | 3.279 AU |
| **Hildas** | the density at 3.92–4.02 AU is over 3x the density at 3.55–3.75 AU | the 3:2 resonance piles up at 3.97 AU |
| **Trojan clouds** | of objects with 5.1 < a < 5.3 AU, at least 80 % have mean longitude within 60 plus/minus 20 deg of Jupiter's at the table epoch, ahead or behind | L4/L5; Jupiter's longitude comes from `StandishElements` |
| **Plutinos** | the TNO count in 39.2–39.6 AU is over 4x the count in 40.6–41.0 AU | the 3:2 with Neptune: 30.07 x 1.5^(2/3) = 39.40 AU |
| Ceres absent; Hygiea (10) present with a = 3.14 plus/minus 0.02 AU | — | the exclusion rule works (Hygiea's a INFERRED 3.142) |
| Counts | at least 30,000 total, at least 5,000 TNO | the probe gave 32,294 and 7,287 |

**Range assertions:** 0 at most e < 1 (hyperbolic rows dropped and counted); 0.1 at most a at
most 5,000 AU; 0 at most i at most pi; -1 at most H at most 30. Drop budget 0.5 %.
**Provenance:** elements at epoch Measured ("JPL SBDB osculating elements, epoch JD ..."); a
position at another time Inferred ("two-body propagation from the epoch; planetary perturbations
ignored, the error grows with |t - epoch|"); the point appearance Procedural.

### C.5 Sun activity — `sun/activity.cssa` (`CSSA`) and `sun/snapshot.json`, lane `solar`

**Sources** (all VERIFIED 200 on 2026-09-11):

| id | URL | Tag |
|---|---|---|
| `swpc-cycle` | `https://services.swpc.noaa.gov/json/solar-cycle/observed-solar-cycle-indices.json` (512 KB, **3,332 monthly rows, 1749-01 to 2026-08**; keys `time-tag`, `ssn`, `smoothed_ssn`, `observed_swpc_ssn`, `f10.7`, and `smoothed_f10.7`, the last INFERRED and asserted) | F10.7 `pd`; **SSN `nc`**, because WDC-SILSO data is CC BY-NC 4.0 (INFERRED, verify) |
| `swpc-pred` | `https://services.swpc.noaa.gov/json/solar-cycle/predicted-solar-cycle.json` (18 KB) | `pd` |
| `swpc-now` | under `https://services.swpc.noaa.gov/`: `json/goes/primary/xrays-6-hour.json`, `json/goes/primary/xray-flares-latest.json`, `products/noaa-planetary-k-index.json`, `json/goes/primary/integral-protons-1-day.json`, `products/summary/10cm-flux.json`, `json/solar_regions.json`, `products/alerts.json`, `json/rtsw/rtsw_wind_1m.json`, `json/rtsw/rtsw_mag_1m.json`. **The old `plasma-1-day` and `mag-1-day` endpoints are 404 and must not be used** | `pd` |

**`activity.cssa`** (rows = months from 1749-01; observed first, then predicted): `ssn` f32
(International Sunspot Number v2 monthly mean; NaN if absent or dropped by profile),
`ssnSmoothed` f32, `f107` f32 (sfu, adjusted monthly mean; NaN before 1947-02), `f107Smoothed`
f32, `flags` u8 (0 predicted/Inferred, 1 SSN removed by the licence profile). Meta:
`firstMonth` "1749-01", `lastObservedMonth`, `predictedThrough`. **The manifest entry declares
`"features": {"ssn": <true|false>}`** (R47), so the layer and the VITALS card can say "sunspot
number: not in this build (licence) — F10.7 only" instead of drawing a flat NaN line. About 3,450 rows x 17 B = 59 KB.

**`snapshot.json`** (about 5 KB): `asOfUtc`, `"notLive": true`; `xray {fluxWm2, class}`,
`lastFlare {class, peakUtc}`, `kp`, `gScale`;
`solarWind {speedKms, densityCc, temperatureK, btNt, bzNt, spacecraft}` (`source` and `active`
from RTSW); `protons10MeV {pfu, sScale}`, `f107Sfu`; `regions {count, spots}`, `alertHeadline`;
`thresholds` (the Vitals scales, verbatim); `sources[]`. The UI text is fixed:
"Space weather — snapshot as of &lt;UTC&gt; (not live)".

**Importer** `build_heliophysics.py`, stdlib, run once per release just before
`build_spacecraft.py`. **On any endpoint failure it keeps the previous artefact rather than
shipping none** — the one sanctioned exception to "delete on failure", because a snapshot is
explicitly dated.

**Core** `Core/Sun/SolarActivity.cs`: `Parse`, then `At(CosmicTime)` returning
`(ssn, f107, FieldProvenance)` with linear interpolation between month midpoints — Measured for
observed months, Inferred for predicted, `Unknown` outside the range. `SpaceWeatherScales`:
`XrayClass(flux)`, `GScale(kp)`, `SScale(pfu)`.

**Tests (`SolarActivityTests`):** first month 1749-01 and at least 3,332 rows (VERIFIED);
**Cycle 19 maximum** — the smoothed SSN peak in 1955–1962 falls in 1958-02 to 1958-04 at 285
plus/minus 10 (INFERRED, confirm on the first bake); **Cycle 25 minimum** in 2019-11 to 2020-01
(INFERRED); F10.7 NaN before 1947-02 and at least 60 sfu wherever present (the quiet-Sun floor is
about 64); `XrayClass(1.2e-4) = "X1.2"`, `XrayClass(5e-6) = "C5.0"`, `GScale(7) = G3`,
`SScale(100) = S2` (VERIFIED formulas). **Range assertions:** 0 at most ssn at most 500; 50 at
most F10.7 at most 500; months contiguous; no duplicate `time-tag`.

**Deferred (not in 2.0.0):** the solar disk stills and the Carrington synoptic map (D5).

### C.6 Core code instead of data — the Moon, deep-time planets, reference tables

Engine-free C# files with published coefficients. There is no importer; **the tests are the
gate.**

**`Core/Orbits/LunarTheory.cs`** (lane `solar`): Meeus, *Astronomical Algorithms* 2nd ed.
chapter 47 — the truncated ELP-2000/82 of Chapront-Touzé & Chapront (1983). Inputs TDB; the mean
elements L', D, M, M' and F are cubic and quartic polynomials in T; the series are the
eccentricity factor E, the additive terms A1–A3, Table 47.A (60 terms: Sigma-l, Sigma-r) and
Table 47.B (60 terms: Sigma-b), giving geocentric lambda, beta and Delta referred to the **mean
ecliptic and equinox of date**. **Then precess to J2000** (IAU 1976 Lieske angles, Meeus ch. 21
rigorous method) and convert ecliptic to ICRF with
`StandardFrames.EclipticToEquatorial(ObliquityJ2000Iau1976Rad)` (VERIFIED present).
**Do not add nutation**: the validation fixture is geometric J2000. This fixes the finding that
the same series used without precession is off by 2,762 km (median). **It replaces the two-body
Moon everywhere**: Earth = EMB - Moon_geo x GM_Moon/(GM_Earth + GM_Moon).
**Tests (`LunarTheoryTests`)** use the 26 geocentric Moon vectors 2026–2031 in
`T/fixtures/horizons_validation.json`: **median error at most 20 km and maximum at most 50 km**
(Meeus states about 10" in lambda and 4" in beta for this truncation, and 10" at 384,400 km is
18.6 km; the maximum allows 2.5x for the distance series) — **if the measured error is larger,
find the cause before touching the bound**; and the Earth-EMB offset is `|Moon| x 0.0121505`
exactly, 4,670.6 km at 384,400 km. The 120 coefficient rows are transcribed from the book
(INFERRED): a typo shows up as a periodic residual in this test.

**`Core/Orbits/StandishElements.cs`** (lane `solar`): JPL "Approximate Positions of the Planets"
(`https://ssd.jpl.nasa.gov/planets/approx_pos.html`, VERIFIED reachable). Table 1 (1800–2050) is
already transcribed and its Mercury–Neptune values match JPL's page (VERIFIED). Tables 2a and 2b
(3000 BC – AD 3000) must be transcribed from the same page; they include the b, c, s and f terms
for Jupiter to Pluto: `M = L - varpi + bT^2 + c cos(fT) + s sin(fT)`. Evaluation uses
`eps = ObliquityJ2000Iau1976Rad`. **Pluto always comes from Table 2a/2b** (JPL's current Table 1
has no Pluto row, INFERRED); the eight planets use it whenever `|t - 2026.0|` exceeds the fitted
conic's validated window (proposed: outside 2016–2036, with a one-year linear blend at the
edges). **Tests (`StandishTests`):** heliocentric direction error against the fixture at most
0.08 deg for all eight planets over 2026–2031 (the measured worst case was 0.074 deg for Saturn,
so this is the measurement plus 8 %); Table 2 against Table 1 at J2000, within JPL's published
Table 2 error; and **a new fixture vector, Jupiter at 1979-03-05 (Voyager 1)**, added by
`fetch_validation_fixture.py`, within JPL's published Table 1 error for Jupiter (the value to be
read off the same page, INFERRED).

**Reference tables** — constants with citations, each in exactly one place:

| File | Lane | Contents | Tests |
|---|---|---|---|
| `Core/Deep/GalaxyModel.cs` | milkyway | R0 = **8.15 kpc** and Theta0 = 236 km/s (Reid+2019, VERIFIED); z_sun = 20.8 pc (INFERRED); Sun-LSR 17.9 km/s toward (l, b) = (48, 23) (Planck 2018 I Table 3, VERIFIED); Sgr A* at RA 266.41684, Dec -29.00781 (INFERRED) at d = R0; **the Reid arm table** (7 rows, VERIFIED); `ArmRadiusKpc(arm, beta)`; `FittedBetaRange(arm)` | `GalaxyModelTests`: **R(beta=0): Local 8.526, Perseus 10.070, Sgr-Car 6.871, Outer 12.443 kpc**, plus/minus 0.001 (probe arithmetic); each arm's curve continuous at beta_kink to 1e-9; (l, b) = (0, 0) to RA 266.4050, Dec -28.9362 and the NGP to RA 192.8595, Dec 27.1283, plus/minus 0.0005 deg |
| `Core/Cmb/CmbModel.cs` | cmb | T0 = 2.72548 K (Fixsen 2009); dipole D = 3362.08 uK toward (264.021, 48.253) and v_sun = 369.82 km/s (Planck 2018 I Table 2 HFI, VERIFIED); the frame table Sun-LSR, LSR-GC, GC-CMB 565 km/s toward (265.76, 28.38), Sun-LG, **LG-CMB 620 plus/minus 15 km/s toward (271.9, 29.6)** (VERIFIED); **chi\* = 13.87 Gpc** (the comoving distance to last scattering, `r*/theta*` = 144.43 Mpc / 0.0104110, VERIFIED from Planck-alone values — **this is where the CMB shell is drawn, in both forms**); **`ParticleHorizonGpc` = 14.26** (the comoving particle horizon, `integral_0^inf c dz / H(z)` over the same LambdaCDM parameters — **this is "the edge of the observable universe", radius 4.40e23 km, and it is what rung 18 frames and what `ScaleLimits.MinUnitsPerKm = 2e-25` shrinks to 17.6 cm**; INFERRED until the integration is recorded here with its Planck citation, R44); age 13.797 Gyr; H0 = 67.36 (Planck-alone, VERIFIED) | `CmbModelTests`: **the dipole apex to RA 167.9419, Dec -6.9443**, plus/minus 0.001 deg; `T(apex) - T0 = +3362.08 uK`; the LG apex to RA 162.954, Dec -25.967; **`ParticleHorizonGpc > ChiStarGpc` and the two differ by 0.39 plus/minus 0.05 Gpc** (R44: the one test that stops the two radii being swapped again) |
| `Core/Scale/Cosmology.cs` | galaxies | H0_distance = 74.6; Omega_m = 0.3153; `ComovingDistanceMpc(z)` (Simpson, 512 steps); `CmbFrameCz(czHelio, dirIcrf)`; `DistanceFromModulusPc(DM) = 10^(DM/5 + 1)` | `CosmologyTests`: D_C(0.05) = 198.54, D_C(0.1) = 392.20, D_C(0.2) = 764.64 Mpc, plus/minus 0.02 (probe, 20,000-step midpoint); DM 18.477 to 49.59 kpc |
| `Core/Sun/Heliosphere.cs` | solar | the nose at lambda = 255.6, beta = +5.1 (ecliptic); inflow 25.4 km/s (IBEX, VERIFIED summary); the termination shock V1 94 AU (2004-12-16) and V2 84 AU (2007-08-30) (INFERRED); the heliopause V1 **121.6 AU (2012-08-25)** and V2 **119 AU (2018-11-05)** (VERIFIED summaries); the Rankine half-body `r(theta) = r_n sqrt(2(1 + cos theta)) / sin theta`; the Oort cloud inner 2,000–20,000 AU and outer to about 100,000–200,000 AU (INFERRED), provenance Inferred ("never observed") | `HeliosphereTests`: r = r_n at the nose (the theta to pi limit); r = sqrt(2) r_n on the flanks (theta = 90 deg); down the tail (theta to 0) the half-width `r sin theta` tends to 2 r_n |
### C.7 Milky Way dust — `galaxy/dust.csdu` (`CSDU`), lane `milkyway`, **numpy**

**Choice: Vergely, Lallement & Cox 2022 at native resolution. The Atlas cube is rejected.**

| | Atlas `dust.bin` | **Vergely+2022 `..._050pc_v2`** | Edenhofer+2024 (the fallback) |
|---|---|---|---|
| Origin | scraped from the HTML of a **GPL-3.0** repo, unknown map and version, "relative" units | CDS `J/A+A/664/A174`, a primary product | Zenodo 10658339, a primary product |
| Extent | 2 x 2 x 0.8 kpc | **10 x 10 x 0.8 kpc** | a sphere of radius 1.25 kpc about the Sun |
| Grid | 10 pc | 501 x 501 x 41 nodes, **20 pc step, 50 pc resolution** | HEALPix Nside 256 x 516 log-spaced shells, 68.8–1244.6 pc |
| Unit | unknown | `A0(550nm)/parsec` (header; asserted by value) | E(ZGR23)/pc |
| Licence | GPL-3.0: **unusable** | A&A via CDS, "free for scientific usage": `nc` | **CC BY 4.0: `by`** |

**Why native 20 pc, not resampled.** Resampling to 25 pc (the Nyquist step for 50 pc features)
would save 5 MB of GPU memory and 3 MB of APK, but would add a resampling step that must be
tested and would change the data. The §6.7 budget affords the native grid. Two knobs exist:
`--step 40` (box-averages x and y, keeps z: 250 x 250 x 41 = 2.56 MB) and `--crop`. **The 025 pc
cube (6 x 6 kpc at 10 pc, 112 MB download) is not shipped in 2.0.0.**

**Source.**
`https://cdsarc.cds.unistra.fr/ftp/J/A+A/664/A174/fits/explore_cube_density_values_050pc_v2.fits`
(40 MB; the header ends inside the first 2,880-byte block). `SUN_POS = (250.5, 250.5, 20.5)` is
FITS 1-based, so the Sun is at 0-based node (250, 250, 20). The array is (z, y, x) in numpy, so
**x is fastest**. **+x points toward the Galactic Centre** (ReadMe; INFERRED for the exact
wording — the importer records the ReadMe sentence in meta).

**Layout** (`rows` = 501 x 501 x 41 = 10,291,041): one column `rho` u8, `log8` encoding with
`lo` = the 1st percentile of positive voxels and `hi` = the maximum (0.01375 mag/pc, VERIFIED);
0 means at or below `lo`. Meta `grid`: `dims [501,501,41]`, `sampling "nodes"`,
`origin [-5000,-5000,-400]` pc, `step [20,20,20]`, `frame "galactic-helio"` (+x toward l = 0,
+y toward l = 90, +z toward the NGP), `sunNode [250,250,20]`, `unit "mag/pc (A0, 550 nm)"`.
Size: **10.29 MB raw, 6.22 MB deflated (VERIFIED)**.

**Importer `build_dust.py`:**

1. Download to `/tmp/cs_vergely.fits` and assert the FITS magic, `NAXIS1..3 = 501, 501, 41` and
   `BITPIX = -32`.
2. Read the big-endian floats into memory, then **delete the file**.
3. **Assert on the values, not the header:** the maximum in (0.005, 0.05) mag/pc; no NaN;
   negatives below 1 % (set to 0 and counted); and **the integrated A0 from the Sun along the
   grid axes** (VERIFIED numbers):

   | Path | A0 | Tolerance |
   |---|---|---|
   | Sun toward +x, 5 kpc to the GC | 6.3 mag | plus/minus 5 % |
   | Sun toward -x, the anticentre | 3.0 mag | plus/minus 0.3 |
   | Sun toward +z | 0.058 mag | plus/minus 0.02 |

   This pins the x axis and the unit.
4. **Pin the y and z signs by fit.** For each of the 8 sign flips, compute the mean of `log rho`
   at the 8 cloud landmarks of C.9 (Orion, Taurus, Ophiuchus, Perseus, California, Cepheus,
   Chamaeleon, the Aquila Rift), minus the median `log rho` over nodes at the same `|r|` and
   `|z|`. **The identity must score highest by a margin of at least 1.5x over the next flip**, or
   the importer refuses and a human inspects a rendered x-y slice. The per-flip scores go into
   meta `assertions`. INFERRED that these round-number cloud positions discriminate y and z; if
   they do not, the fallback is the rendered-slice inspection, **never a guess**.
5. Log-encode, then write through `csdata`.

**Fallback** (`commercial`, or `--source edenhofer`): stream the MEAN HDU of
`mean_and_std_healpix.fits` from Zenodo record 10658339 with HTTP Range requests. The layout is
VERIFIED: an IMAGE of 516 x 786,432 float32 big-endian starting at byte 5760, shell *i* at
`5760 + i*3,145,728`, and the radial pixel-centre table at `3,246,402,240 + 2,880`. Resample onto
a 250 x 250 x 80 node grid (plus/minus 1,250 pc in x and y, plus/minus 400 pc in z, 10 pc step)
with `healpix_nest.ang2pix_nest(256, ...)` (VERIFIED by its self-check) and the shell radii: 5.0
MB. The unit is E(ZGR23)/pc, and `ExtinctionAlong` returns it with meta `toA0` = the Zhang, Green
& Rix 2023 conversion factor (INFERRED; take it from the paper). Transfer is 1.62 GB streamed,
with no disk use.

**Core** `Core/Deep/VoxelGrid.cs` (integrator-owned, shared with the CF4 grids): `Parse`, `Dims`,
`Decode(raw)`, `SampleNearest(p)`, `SampleTrilinear(p)` — trilinear decodes the corners first and
interpolates in linear density. `Core/Deep/DustCube.cs` (milkyway): `DensityAt(galacticPc)`,
`ExtinctionAlong(aPc, bPc, stepPc = 10)` in mag, and `GalacticPcFromIcrfPc` via `StandardFrames`.
Outside the box the density is 0.

**Tests (`DustCubeTests`):** the three integrals above (plus/minus 5 %: the log8 step is 3.7 %
relative, rounding is random along a path, and below-floor values are lost);
`ExtinctionAlong(a, b) == ExtinctionAlong(b, a)` to 1e-6; 0 outside the box; **the 8-flip cloud
score recomputed in Core from `landmarks.json`**, so the importer's numpy check is re-proved
without numpy; CSS1 or random bytes refused.

**Provenance:** the grid geometry and extent Measured ("survey footprint"); the density Inferred
("Bayesian inversion of Gaia/2MASS extinctions (Vergely+2022); 50 pc resolution; the far side of
the disc is unmappable"); the rendering ramp Procedural.

### C.8 Galactic point sets — `galaxy/{cepheids,globulars,masers}.csgp` (`CSGP`), lane `milkyway`

Three files with one magic, so each source can be dropped on its own; meta `kind` distinguishes
them. Every file has `pos` f32 x3 (ICRF pc), `distErr` f32 (pc) and `flags` u16.

| File | Source (tag) | Rows | Extra columns | Names |
|---|---|---|---|---|
| `cepheids.csgp` | Skowron+2019, **`unclear`**: "This data is NOT licenced under the MIT or CC licenses" (VERIFIED). Pin the commit of `https://raw.githubusercontent.com/jskowron/galactic_cepheids/master/data/Data_Table_1.dat` (575,072 B, 2,431 rows, 2,214 with DIST > 0, VERIFIED) | 2,214 | `period` f32 (d), `ageMyr` f32, `mode` u8, `absV` f32 (Leavitt law; INFERRED relation, e.g. Benedict+2007 `M_V = -2.43(log P - 1) - 4.05`, stated in provenance) | `ID` in meta |
| `globulars.csgp` | LVDB v1.1.1 `gc_harris.csv` (155) plus `gc_mw_new.csv` (58), **`cc0`**. Header VERIFIED: `key, name, ra, dec, distance` (kpc), `distance_em/ep`, `M_V`, `M_V_em/ep`, `rhalf` (arcmin), `rhalf_physical` (pc), `ll`, `bb`, `distance_gc` | 213 | `absV` f32 (LVDB `M_V`, NaN and flagged if blank), `rhalfPc` f32 | `name` |
| `masers.csgp` | Reid+2019 `https://cdsarc.cds.unistra.fr/ftp/J/ApJ/885/131/table1.dat` (24 KB, 199 rows, VERIFIED): Name 1–13, RA 32–44, Dec 47–59, plx 63–67 (mas), e_plx 69–73, Arm 114–116. **`nc`** (AAS via VizieR) | 199 | `plx` f32 (mas), `arm` u8 (a code table from the Arm column) | `Name` |

**Flags:** Cepheids bit 0 `Outlier` (d above 30 kpc or |z| above 5 kpc; 16 rows are beyond 30
kpc — kept, drawn dim, provenance "suspect"); globulars bit 1 `NoMv`; masers bit 2
`LowSnParallax` (plx/e_plx under 5).

**Importer** `build_galaxy_points.py` (stdlib). **Core** `Core/Deep/PointSet.cs`
(integrator-owned): positions plus named optional columns, flags and names; the same type reads
`lss/cf4_groups.cspt` and `lss/sdss.cspt`.

**Tests (`GalacticPointsTests`):** Cepheid rows 2,214 (VERIFIED); the Cepheid thin disc (median
|z| under 300 pc for d under 10 kpc, INFERRED physics bound); **the Cepheid warp** — the mean z
of Cepheids with R_GC above 12 kpc and 60 < l < 120 is positive, and with 240 < l < 300 is
negative (INFERRED sign; confirm on the bake); globular rows 213 (VERIFIED); **NGC 104 (47 Tuc)**
at 4.52 plus/minus 0.05 kpc (VERIFIED probe); the nearest globular is NGC 6121 (M4) under 2.0
kpc; **omega Cen (NGC 5139)** M_V = -10.26 plus/minus 0.3 (INFERRED); maser rows 199 with every
`arm` code in the meta table (VERIFIED).

**Cross-check (importer only; the source is never redistributed):** Baumgardt's
`https://people.smp.uq.edu.au/HolgerBaumgardt/globular/orbits_table.txt` (168 lines, columns
VERIFIED) must agree with LVDB within 3 sigma for at least 95 % of the clusters in both. Its
licence is unstated, so it is a check only (C23).

**Range assertions:** Cepheids 0.2 at most DIST at most 150 kpc, 1 at most period at most 100 d;
globulars 1 at most d at most 200 kpc, -11 at most M_V at most +3; masers plx above 0.
**Provenance:** Cepheid position Inferred ("mid-IR period-luminosity distance") with `distErr` as
the uncertainty; Cepheid age Inferred (period-age relation); Cepheid absV Inferred (Leavitt law);
globular position Measured direction plus Inferred distance, LVDB `ref_distance` per row in meta
`refs[]`; maser distance Inferred (1/parallax).

**Spiral arms** are Core constants only (`GalaxyModel`, C.6): no file. The curves are drawn
**only over each arm's fitted beta range**, tagged `Inferred ("log-periodic fit, Reid+2019")` and
faded outside it. The masers are the Measured anchors drawn on them.

### C.9 Landmarks and nebulae — `galaxy/landmarks.json`, lane `milkyway`

Hand-authored at `TC/authored/landmarks.json` and committed; `TC/build_authored.py` validates it
and copies it into `SA/`. The same schema serves `lss/structures.json`.

```json
{ "schema": "cosmosynth.places/1",
  "places": [ { "id": "orion-complex", "name": "Orion complex", "category": "cloud",
                "l": 209.0, "b": -19.0, "distancePc": 414, "distanceErrPc": 7, "radiusPc": 60,
                "description": "M42, Horsehead, Flame",
                "reference": "Menten et al. 2007, A&A 474, 515 (VLBA parallax 414+-7 pc)",
                "provenance": {"position": "Measured", "distance": "Measured", "radius": "Inferred"},
                "closeup": "orion" } ] }
```

**`build_authored.py` fails when any entry lacks `reference`**, has an unknown `category`, or has
non-finite coordinates. Positions are either (`l`, `b`, `distancePc`) or
(`ra`, `dec`, `distancePc`); Core computes ICRF pc, so no Cartesian values are stored.
Categories: `cloud`, `nebula`, `cluster`, `snr`, `pulsar`, `blackhole`, `mw`, `lg`. Optional
keys: `closeup` (`orion`, `crab` or `localbubble`) and `pulsarHz`.

**The starter set.** The cloud (l, b, d) values are the Atlas's, VERIFIED by converting its
`markers.json` Cartesian values back. **Every reference below is INFERRED and must be confirmed
to a DOI or bibcode before shipping** (§13 item 30).

| id | l / b (deg) or RA / Dec | d (pc) | Reference to confirm |
|---|---|---|---|
| sun | 0 / 0 | 0 | — |
| sgr-a-star | RA 266.41684 / Dec -29.00781 | `GalaxyModel.R0` (8,150) | Reid & Brunthaler 2004 (position); R0 from Reid+2019 |
| orion-complex | 209.0 / -19.0 | 414 | Menten+2007 |
| taurus | 172.0 / -15.0 | 140 | Zucker+2020 A&A 633, A51 |
| ophiuchus | 353.0 / +17.0 | 140 | Zucker+2020 |
| perseus-clouds | 159.0 / -20.0 | 300 | Zucker+2020 |
| california | 160.0 / -12.0 | 470 | Zucker+2020 |
| cepheus-flare | 110.0 / +15.0 | 350 | Zucker+2020 |
| chamaeleon | 300.0 / -16.0 | 180 | Zucker+2020 |
| aquila-rift | 28.0 / +3.0 | 250 | Zucker+2020 |
| local-bubble | centred near the Sun | radius about 150 | Zucker+2022 Nature 601, 334 (about 1,000 ly across, peanut-shaped: VERIFIED) |
| crab | RA 83.633 / Dec +22.0145 | 2,000 | Trimble 1973; **`pulsarHz` 29.6** (MJD 58058, VERIFIED) |
| m42 | RA 83.822 / Dec -5.391 | 414 | Menten+2007 |
| m8-lagoon, m16-eagle, m17-omega, carina-nebula, rosette, helix, m57-ring, n7000, pleiades, hyades, praesepe | catalogue positions | literature | one reference each (all INFERRED) |

Globular landmarks (47 Tuc, omega Cen, M4, M13, M15) carry `"source": "globulars"` and a `name`
looked up in `globulars.csgp`, so their positions are never duplicated; Local Group landmarks do
the same with `localvolume.cslv`. **The Atlas's Sgr A* at 8,178 pc is replaced by R0 = 8.15 kpc**
so the arm model and the Galactic Centre agree; GRAVITY 2019 (8.178) and 2022 (8.277) are cited
in the entry's `description` as alternatives.

**Core** `Core/Deep/PlaceCatalog.cs`: `Parse(json)`, `Places`, `IcrfPc(place)`.
**`PlaceCatalogTests`:** every entry has a reference; the Orion complex resolves to ICRF pc with
|r| = 414 plus/minus 0.5; the Crab's `pulsarHz` is 29.6.

### C.10 Local Group and Local Volume — `lv/localvolume.cslv` (`CSLV`), lane `galaxies`

**Sources:**

| id | URL | Rows | Tag |
|---|---|---|---|
| `lvdb` | `https://raw.githubusercontent.com/apace7/local_volume_database/v1.1.1/data/dwarf_all.csv` (a release tag; INFERRED that the tag path resolves — the importer falls back to `main` and records the SHA). Header VERIFIED: `key, ra, dec, name, host, rhalf, position_angle(_em/_ep), ellipticity(_em/_ep/_ul), distance_modulus(_em/_ep), apparent_magnitude_v, M_V(_em/_ep), distance` (kpc), `rhalf_physical`, `ll, bb, sg_xx, sg_yy, sg_zz, distance_measurement_method` | **911** (VERIFIED); the largest dwarf distance is 12.76 Mpc | `cc0` |
| `ungc` | `https://cdsarc.cds.unistra.fr/ftp/J/AJ/145/101/table1.dat` (106 KB). Bytes VERIFIED: Name 1–18, RAh/m/s 20–29, DE sign 31, DEd/m/s 32–39, **a26 41–46** (arcmin, Holmberg isophote), **b/a 48–51**, Kmag 79–83, **TT 99–100**, HRV 110–113, **Dist 115–119** (Mpc), f_Dist 121–124. **It has no position angle** | 869 | `nc` |
| `xsc` | VizieR `VII/233/xsc` (2MASS XSC). Labels VERIFIED from the ReadMe: `2MASX` (A17), **`Sb/a`**, **`Spa`** (deg E of N), `Kb/a`, `Kpa`, `r.ext`. Query `https://vizier.cds.unistra.fr/viz-bin/asu-tsv?-source=VII/233/xsc&-out=2MASX,RAJ2000,DEJ2000,Sb/a,Spa,r.ext&K.ext=<12.5&-out.max=unlimited` (the `K.ext` label and constraint syntax INFERRED; the importer asserts the row count above 50,000) | about 1e5 (INFERRED) | `nc` via VizieR |
| `overrides` | `authored/overrides.json`, hand-authored and cited: hero kinematic inclinations and PAs | about 6 | `own` |
| `giants-fallback` | `authored/giants_fallback.json`, hand-authored and cited, about 20 giants | about 20 | `own` |

**Build rules.**

1. **Every LVDB dwarf.** `distance` (kpc) comes from the file; the importer asserts it equals
   `10^(distance_modulus/5 + 1)/1000` to 0.5 %. **`ell`, `pa`, `rhalf_physical` and `M_V` are
   taken as given, and a blank stays blank**, with its bit set — this is the Atlas's worst defect
   reversed (its 542 of 911 default ellipticities were drawn as if measured). `M_V` blank but
   `apparent_magnitude_v` present gives `M_V = m - DM`, provenance Inferred, bit set.
2. **UNGC rows not already in LVDB.** Matching: angular separation under 2 arcmin and
   `|dd|/d < 0.3`, **or** a normalised-name match ("MESSIER031" to "M31", "NGC0253" to
   "NGC 253"). Of the unmatched, keep those with **M_K = Kmag - 5 log10(Dist x 1e5) at most
   -19.5**, plus any row named in `overrides.json`. This yields M31, M33, M81, M82, NGC 253,
   NGC 5128, M83, IC 342, M101, NGC 4945, Maffei 1/2 and so on; the expected count is 40–80
   (INFERRED; the importer prints the list). Size = the Holmberg radius (`a26/2 x d`), size kind
   "Holmberg"; the axis ratio from UNGC `b/a`; the T-type from `TT`. **The Milky Way row is kept
   but flagged `IsHome` and is never drawn as a point**; its disc comes from `GalaxyModel`.
3. **PA for non-LVDB galaxies** from `xsc` `Spa`, the nearest 2MASX within 30 arcsec of the UNGC
   position; otherwise the PA is unknown (bit set).
4. **Overrides win** and are Measured and cited: M31 (i = 77, PA = 38; Corbelli+2010, INFERRED),
   M33 (i about 55, PA about 22.5; Corbelli+2014, INFERRED), the LMC and SMC disc parameters
   (van der Marel & Kallivayalil 2014, INFERRED), plus M81 and NGC 253 if the author finds
   kinematic values. `build_authored.py` validates this file's references too.
5. **Fallback** (profile `commercial`, or UNGC unreachable): the giants come from
   `giants_fallback.json` — about 20 rows (MW, M31, M33, M81, M82, NGC 253, NGC 5128, M83,
   IC 342, M101, NGC 4945, Maffei 1, Maffei 2, NGC 55, NGC 300, M94, NGC 4258, M51, NGC 6946,
   M104), each with RA, Dec, distance, D25 or a26, b/a, PA and T-type, and **one reference per
   value**. These are facts transcribed from papers, released CC0 by the project. When UNGC is
   available the importer cross-checks the fallback against it (`dd` under 15 %, position under
   1 arcmin), so the hand file cannot rot unnoticed.

**Columns** (rows about 911 plus the giants): `pos` f32 x3 (kpc, ICRF); `distErr` f32 (kpc, half
of em + ep); `absMag` f32 (M_V from LVDB, or M_K from UNGC, per `band`); `band` u8 (0 = V,
1 = K); `size` f32 (kpc: the half-light radius from LVDB or the Holmberg radius from UNGC, per
`sizeKind`); `sizeKind` u8 (0 = half-light, 1 = Holmberg); `ell` f32 (1 - b/a; NaN if unknown);
`pa` f32 (deg E of N, 0 at most pa < 180; NaN if unknown); `incl` f32 (the override if present,
else NaN, and Core derives it); `ttype` i8 (UNGC TT, or 99 for an LVDB dwarf); `host` u8 (a code
table: mw, m31, lmc, lf, lfd, none); `source` u8 (0 lvdb, 1 ungc, 2 fallback, 3 override);
`flags` u32 (0 EllUnknown, 1 PaUnknown, 2 SizeUnknown, 3 MvDerived, 4 IsHome, 5 LandmarkLG).
Meta: `names[]`, `refs[]` (per row: the LVDB `ref_distance` or the UNGC `f_Dist`). About 45
B/row, so about 0.05 MB plus names.

**Core** `Core/Deep/GalaxySet.cs` reads both `CSLV` and `CSRS` through the column names the two
share. `Core/Deep/GalaxyOrientation.cs`: `SkyBasis(ra, dec)` with
`n = (-sin d cos a, -sin d sin a, cos d)` and `e = (-sin a, cos a, 0)`;
`MajorAxis(pa) = cos(pa) n + sin(pa) e`; **`InclinationDeg(q, q0 = 0.2)`:
`cos^2 i = (q^2 - q0^2)/(1 - q0^2)`, and 90 deg if q at most q0** (Hubble 1926);
`DiscNormal(rhat, i, khat, sign)` where the sign is Procedural, a hash of the name. These are
shared with 2MRS.

**Tests (`LocalVolumeTests`, `GalaxyOrientationTests`):**

| Check | Expected | Why the bound |
|---|---|---|
| LVDB dwarf rows | **911** at v1.1.1 | VERIFIED |
| **Antlia II** | d = 124.17 plus/minus 0.1 kpc; ell 0.6; PA 156 | VERIFIED probe row |
| **LMC** | d = 49.6 plus/minus 1.0 kpc | DM 18.477 (INFERRED as LVDB's adopted value) |
| **M31** | d = 0.77 plus/minus 0.04 Mpc (UNGC 0.77, VERIFIED); `incl` = 77 from the override | the literature spread is 0.76–0.78 Mpc |
| **Inclination formula** | q = 0.28 gives 78.46; q = 0.33 gives 74.46; q = 0.59 gives 55.49 (plus/minus 0.01 deg) | probe arithmetic. "M31 about 78 deg" is the q = 0.28 case; photometric b/a (UNGC 0.33) gives 74.5 and kinematics about 77, which is why M31 is an override |
| M31 derived without the override | 72 at most i at most 80 | photometric q 0.30–0.34 with q0 in 0.13–0.25 |
| PA 0 gives a major axis parallel to north; PA 90 parallel to east | exact to 1e-12 | pure geometry |
| No LVDB blank became a number | for every row with `EllUnknown`, `ell` is NaN | the Atlas-default regression |

**Range assertions:** 0 at most ell < 1; PA normalised to [0, 180); 0.005 at most d at most 30
Mpc (the Milky Way row excepted); -24 at most absMag at most +2; 1 pc at most size at most 60
kpc. **Provenance:** position Measured direction plus Inferred distance (TRGB, Cepheid or other,
per the method code); ell and PA Measured where given, "Unknown — drawn round, faint" where
flagged; inclination Measured (override) or Inferred (Hubble, q0 = 0.2); the disc front/back sign
Procedural.

### C.11 Large-scale structure — `lss/`, lane `galaxies`

**C.11.1 2MRS — `lss/2mrs.csrs` (`CSRS`).** Source
`https://cdsarc.cds.unistra.fr/ftp/J/ApJS/199/26/table3.dat.gz` (3.3 MB gz, 44,599 rows). Bytes
VERIFIED: **ID 1–16** (the 2MASS XSC or LGA designation), RAdeg 18–26, DEdeg 28–36, Ktmag 79–84,
**Rext 148–152** (log arcsec), **b/a 154–158**, flags 160–163 (a leading "Z" means LGA
photometry), type 165–169 (a ZCAT code), **cz 174–178** (barycentric km/s), e_cz 180–182. Tag
`nc`, plus the 2MASS acknowledgement.

Rules: compute `cz_cmb` (§6.3); **drop rows with `cz_cmb` under 500 km/s** (the Local Group and
Local Volume, where peculiar velocity dominates and C.10 already has them); rows at 500–1,500
km/s get `NearbyPv`; `d = D_C(cz_cmb/c)`. **PA: join `Spa` from the `xsc` query of C.10 on the
designation** ("J" + ID = `2MASX`); for unmatched LGA rows (flag "Z") fall back to a positional
join within 30 arcsec; otherwise `PaUnknown`.

Columns: `pos` f32 x3 (Mpc, ICRF), `kt` f32 (mag), `rext` f32 (arcsec, `10^Rext`), `ba` f32,
`pa` f32 (deg, NaN if unknown), `type` u8 (an index into meta `zcatTypes[]`; the T-type mapping
is INFERRED and flagged), `czCmb` f32 (km/s), `flags` u16 (0 PaUnknown, 1 LgaPhotometry,
2 NearbyPv, 3 TypeUnknown). 36 B x about 43.4 k = 1.56 MB.

**Tests (`RedshiftSurveyTests`):** at least 43,000 rows (43,507 with cz > 0, VERIFIED, minus the
cut); **the Zone of Avoidance** — galaxies per square degree at |b| under 5 deg is under 50 % of
those at 20 < |b| < 30; **Coma** — at least 50 galaxies within 1.5 deg of (194.935, +27.912) with
cz_cmb 5,500–9,500 (INFERRED count; position VERIFIED); **Virgo** — at least 100 within 6 deg of
(187.70, +12.39) with cz_cmb 700–2,700 (INFERRED); PA coverage at least 95 % (INFERRED; if lower,
report and keep); **a row with cz_cmb = 14,989.6 km/s (z = 0.05) sits at 198.54 plus/minus 0.05
Mpc** — the §6.3 known answer through the whole path.

**C.11.2 Cosmicflows-4 groups — `lss/cf4_groups.cspt` (`CSPT`).** Source
`https://cdsarc.cds.unistra.fr/ftp/J/ApJ/944/94/table3.dat.gz` (1.6 MB gz, 38,053 rows). Bytes
VERIFIED: 1PGC 1–7, **DMzp 9–14**, DMav 16–21, e_DMav 23–27, **Vcmb 29–33**, RAdeg 35–42,
DEdeg 44–51. Tag `nc`. `d = 10^(DMzp/5 + 1)` pc, the CF4 zero point (H0 = 74.6). Columns: `pos`
f32 x3 (Mpc, ICRF), `dm` f32, `dmErr` f32, `vcmb` f32, `pgc` u32. 28 B x 38,053 = 1.07 MB.
**Tests:** rows = 38,053; **median(Vcmb/d) over 60 < d < 200 Mpc = 74.6 plus/minus 2 km/s/Mpc** —
the calibration known answer, the catalogue's own H0 coming back out; median d = 210 plus/minus 5
Mpc (VERIFIED 210). **Provenance:** distance Inferred ("TF/FP/SN Ia distance moduli calibrated on
TRGB, Cepheids and masers; e_DMav as the uncertainty").

**C.11.3 CF4 basins of attraction — `lss/cf4_basins.csgr` (`CSGR`).** Source: the member
`CF4_new_128-z008_BoA.fits` (16.78 MB, stored uncompressed) inside
`https://projets.ip2i.in2p3.fr//cosmicflows/CF4_new_128-z008_watersheds-fits.zip` (67,127,665 B);
range-fetch only that member by following the zip's central directory (VERIFIED method). Tag
**`unclear`** ("if you use this data cite the article"). Geometry VERIFIED: 128^3 float64 labels
0–9, numpy `lab[iz,iy,ix]` so **x = SGX is fastest**; a 1000 Mpc/h box centred on the observer;
cells of 7.8125 Mpc/h; `i = floor((SG + 500)/7.8125)`. Layout: `label` u8, 2,097,152 rows
(2.10 MB raw, **about 37 KB deflated**, VERIFIED); meta
`grid {dims [128,128,128], sampling "cells", origin [-500,-500,-500], step [7.8125]x3, unit "Mpc/h", h 0.746, frame "supergalactic"}`
plus meta `labels[]`.

**Label names** from Dupuy & Courtois 2023 Table A.1, in row order. **The importer verifies every
named basin**: the voxel at the basin's attractor SG coordinate must read that label. Labels 1
and 2 are VERIFIED; the rest are INFERRED until then. A basin that fails is named "basin N
(unidentified)".

| Label | Name | Attractor SG (Mpc/h) | Volume (1e6 (Mpc/h)^3) |
|---|---|---|---|
| 1 | **Laniakea** | (-62, -8, 39) | 1.9 |
| 2 | Apus | (-125, -23, -31) | 9.5 |
| 3 | Hercules | (-39, 70, 78) | 3.1 |
| 4 | Lepus | (-62, -39, -109) | 8.1 |
| 5 | Perseus-Pisces | (47, -23, -31) | 4.8 |
| 6 | Shapley | (-141, 62, -16) | 7.9 |
| 7–9 | SDSS-1a / 2a / 2b | (117, 227, -227) / (-102, 297, 102) / (31, 289, 156) | 184.8 / 169.8 / 130.2 |

**Tests (`Cf4GridTests`):** the label at SG(-62, -8, 39) is 1 and at SG(-125, -23, -31) is 2; the
observer cell is label 1; **Laniakea is exactly 4,079 cells = 1.945e6 (Mpc/h)^3** (VERIFIED; the
paper says 1.9e6); labels are in 0–9 only. **Provenance:** the basin partition Inferred
("watershed of the CF4 reconstructed velocity field").

**C.11.4 CF4 density contrast — `lss/cf4_delta.csgr` (`CSGR`).** Source
`https://projets.ip2i.in2p3.fr//cosmicflows/CF4_new_64-z008_delta.fits` (1,054,080 B), tag
`unclear`. Geometry **VERIFIED by fit** (6 permutations x 2 flips x 5 box sizes against 6
attractors and 2 voids; score 3.21 against 1.09 for the next best): numpy `g[ix,iy,iz]`, so
**NAXIS1 = SGZ, the reverse of the basins**; a 1000 Mpc/h box, 15.625 Mpc/h cells, the observer
at the centre. **The paper's text implies about 500 Mpc/h. The data win** (§13 item 23).
Importer: transpose to x-fastest (`np.ascontiguousarray(g.transpose(2,1,0))`), then u8 `linear`
over [min delta, max delta]. Delta reaches -2.32, so this is a smoothed linear field, not a
physical density contrast bounded at -1. **Render an SGX-SGY slice PNG into `TC/cache/` for a
human check** before the first ship. Layout 262,144 B with the same grid meta at step 15.625.
**Tests** (delta at the nearest cell, plus/minus 0.05 — half a u8 step, 0.018, plus the
cell-centre offset): Laniakea attractor 1.24; Apus 1.51; Perseus-Pisces 1.26; Shapley 1.94;
Hercules 1.61; Lepus 1.39; Sculptor Void -1.12; near-Boötes -2.32. **Provenance:** Inferred
("CF4 Wiener-filter / HMC reconstruction, z < 0.08").

**C.11.5 Named structures — `lss/structures.json`.** The same `cosmosynth.places/1` schema as
C.9. Categories `basin`, `cluster`, `void`, `wall`, `repeller`. Positions are either
`sg: [x,y,z]` in Mpc/h (converted with h = 0.746) or `ra`/`dec` plus `z` or `distanceMpc`.

| Entry | Position | Status |
|---|---|---|
| The 12 Dupuy & Courtois 2023 Table A.1 rows | Laniakea, Apus, Hercules, Lepus, Perseus-Pisces, Shapley, SDSS-1a, SDSS-2a, SDSS-2b, the Sculptor Void, the near-Boötes Void, the Dipole Repeller plus CSR, with RA/Dec, V_CMB, SG and volume | VERIFIED from the PDF |
| Clusters (SIMBAD) | Virgo (187.699, +12.385, z 0.0038), Coma (194.935, +27.912, 0.0234), Norma/ACO 3627 (243.594, -60.869, 0.01628), Perseus/ACO 426 (49.948, +41.515, 0.01836), Leo/ACO 1367 (176.186, +19.700, 0.0225), Hercules/ACO 2151 (241.313, +17.749, 0.0352), Centaurus/ACO 3526 (192.205, -41.305, 0.0114), Fornax/ACO S373 (54.623, -35.452, 0.0050), Hydra I/ACO 1060 (159.150, -27.518, 0.0129), **Shapley core ACO 3558 (202.014, -31.527, 0.0474)**, ACO 2199 (247.144, +39.541, 0.0309) | VERIFIED |
| Cluster distance rule | the CF4 group within 1 deg and dV under 500 km/s if one exists (Measured-calibrated), else `D_C(z_cmb)` (Inferred) | rule |
| Voids | the Boötes Void (222.946, +45.796, z about 0.05); the Local Void (279.5, +18, coarse) | position VERIFIED; z INFERRED |
| Walls | the CfA2 Great Wall (z about 0.03); the Sloan Great Wall (z about 0.07, about 400 Mpc) | **INFERRED: each needs a citation before shipping**, which `build_authored.py` enforces. The Dipole Repeller is already a Table A.1 row; add Hoffman+2017 as its second reference |

**Tests (`StructuresTests`, lane `galaxies`)** — R51: this was the **only** shipped dataset with no
test, while it supplies the labels the user reads at every BEYOND rung and several of its rows are
marked "needs a citation before shipping". A non-empty `reference` string checked by
`build_authored.py` is not a test.

- Virgo resolves to a direction within **0.1 deg** of (187.699, +12.385) and a distance of
  **16.5 plus/minus 1.5 Mpc** through the cluster distance rule above.
- Coma resolves to **99 plus/minus 8 Mpc**.
- The Laniakea entry's SG position converts to the same cell the `cf4_basins` test reads as
  **label 1**.
- The Dipole Repeller lies in the hemisphere **opposite** Shapley: `dot(n_DR, n_Shapley) < -0.7`.
- Every entry has a non-empty `reference` and a `category` in {`basin`, `cluster`, `void`, `wall`,
  `repeller`}.
- **No two entries within 0.5 deg *and* 5 % in distance** (the duplicate-label check).

**C.11.6 SDSS DR18 subsample — `lss/sdss.cspt` (`CSPT`), recommended, optional.** Source: the
SkyServer SqlSearch (VERIFIED working), run in 16 RA chunks:

```
https://skyserver.sdss.org/dr18/SkyServerWS/SearchTools/SqlSearch?format=csv&cmd=
  select s.specObjID, s.ra, s.dec, s.z, p.petroMag_r from SpecObj s join PhotoObj p on s.bestObjID=p.objID
  where s.class='GALAXY' and s.zWarning=0 and s.z between 0.005 and 0.2 and s.ra >= {a} and s.ra < {b}
```

783,530 rows in total (VERIFIED). Tag **`pd`**; this is also the commercial fallback for 2MRS
(northern sky only). **Subsample, deterministic:** keep a row when
`FNV-1a-64(str(specObjID)) / 2^64 < keep / N_total`, with `keep` defaulting to 100,000. The draw
is uniform, so the z-distribution is preserved and a rerun reproduces it exactly; the knob is
`--keep`. Columns: `pos` f32 x3 (Mpc, `d = D_C(z_cmb)`), `rmag` f32, `z` f32. 20 B x 100,000 =
2.0 MB. **Tests:** rows 100,000 plus/minus 1 % (the hash is uniform); all z in [0.005, 0.2];
under 2 % of rows at Dec below -15 deg (the footprint; INFERRED); the distance known answer as in
2MRS. **Provenance:** direction Measured; distance Inferred (§6.3).

### C.12 The CMB — `cmb/cmb_sky.cscb` (`CSCB`), lane `cmb`, **numpy**

**Choice: a cube map of 6 x 1024^2 texels, R8, sampled by the ICRF direction, with an inpainting
mask at 6 x 256^2.** The two research reports disagreed; this is the resolution:

| Option | Texels | Raw R8 | GPU with mips | Deflated | Problems |
|---|---|---|---|---|---|
| Equirect 4096 x 2048 | 8.4 M | 8.39 MB | 11.2 MB | 6.12 MB (VERIFIED) | an `atan2` seam in mip selection; minified along RA near the poles |
| Cube 6 x 2048^2, EAC_R11 | 25.2 M | — | 16.8 MB | about 12.6 MB | EAC needs an offline encoder the stdlib cannot provide, or the Unity asset pipeline, which departs from the StreamingAssets contract |
| Cube 6 x 2048^2, R8 | 25.2 M | 25.2 MB | 33.6 MB | about 18 MB | the size |
| **Cube 6 x 1024^2, R8 (chosen)** | 6.29 M | **6.29 MB** | **8.4 MB** | **about 4.6 MB** (INFERRED from the 0.73 ratio measured on the equirect) | the face-centre texel is 6.7 arcmin against SMICA's about 5 arcmin beam, so slightly softer at face centres; 3.4 arcmin at edge midpoints |

The cube map is seamless in hardware, has no pole oversampling, and needs one texture fetch. Its
texel density (3.4–6.7 arcmin) matches the Nside-1024 map it samples (3.44 arcmin pixels), and
the data hold no detail below the 5 arcmin beam anyway. **Knob:** `--face 2048` makes a 25 MB
file with no code change, because the face size is in the header; use it only if the headset
shows the CMB visibly soft (§13 item 32).

**Sources:**

| id | URL | Column | Tag |
|---|---|---|---|
| `planck` | `https://pla.esac.esa.int/pla/aio/product-action?MAP.MAP_ID=COM_CMB_IQU-smica-nosz_2048_R3.00_full.fits` (402,661,440 B; PLA gives 33 MB/s and ignores Range; the mirror is `https://irsa.ipac.caltech.edu/data/Planck/release_3/all-sky-maps/maps/component-maps/cmb/...`) | `I_STOKES_INP`: **uK although TUNIT says K_CMB**; scale 1 (VERIFIED) | `nc` (ESA CC BY-NC 3.0 IGO) |
| `planck-mask` | `https://pla.esac.esa.int/pla/aio/product-action?MAP.MAP_ID=COM_Mask_CMB-Inpainting-Mask-Int_2048_R3.00.fits` | `TMASK` (0 = inpainted; 2.12 % of the sky, VERIFIED) | `nc` |
| `wmap` (fallback) | `https://lambda.gsfc.nasa.gov/data/map/dr5/dfp/ilc/wmap_ilc_9yr_v5.fits` (25 MB; LAMBDA gives 0.16 MB/s) | `TEMPERATURE` (mK, scale 1000); Nside 512; a 1 deg beam | `pd-ack` |

All three are HEALPix NESTED, Galactic, big-endian, with `BAD_DATA = -1.6375e30` (VERIFIED).

**Cube-face convention** — the standard cube-map face selection shared by OpenGL, Vulkan and
D3D. **Every term is in ICRF components, so the file never contains a Unity axis.**

| Face | Major axis | sc | tc | Texel direction from (u, v) = (2s - 1, 2t - 1) |
|---|---|---|---|---|
| 0 | +x | -z | -y | ( 1, -v, -u) |
| 1 | -x | +z | -y | (-1, -v,  u) |
| 2 | +y | +x | +z | ( u,  1,  v) |
| 3 | -y | +x | -z | ( u, -1, -v) |
| 4 | +z | +x | -y | ( u, -v,  1) |
| 5 | -z | -x | -y | (-u, -v, -1) |

`s = (sc/|ma| + 1)/2`, `t = (tc/|ma| + 1)/2`. Texel (i, j) has its centre at `s = (i + 0.5)/F`,
`t = (j + 0.5)/F`. **Row j = 0 is t = 0.** Storage is face-major, then row-major (j), then i.

INFERRED: that Unity's `Cubemap.SetPixelData(bytes, 0, (CubemapFace)f)` puts row 0 at t = 0 on
Vulkan. **The runtime must prove this, not assume it**: the `cmb-faces` play-mode assertion
(§11.3) samples the uploaded cube map **through the GPU** along the six face-centre directions
and the Cold Spot and compares with `CmbSky.Sample`. If a face comes out flipped, the fix is a
flip at upload. **The data never change.**

**Layout:** `t` u8, 6 F^2 rows (F = 1024), the ΔT anisotropy, `linear` encoding with scale
1000/255 = 3.92157 uK and offset -500 uK, so plus/minus 500 uK; the clipped fraction is reported
(0.0014 % in the VERIFIED equirect bake). `mask` u8, 6 x 256^2, 1 = inpainted (a constrained
realisation, not a measurement), 0 = measured; absent under WMAP. Meta: `faceSize`,
`maskFaceSize`, `source` (`planck` or `wmap`), `beamArcmin` (5 or 60), `units` "uK_CMB, monopole
and dipole removed", `stats {rmsUk, p01, p99, min, max, clippedFraction}`. Size 6.29 + 0.39 MB.

**Importer `build_cmb.py`** (it generalises the research `cmb_bake.py`, VERIFIED on both maps,
and uses `healpix_nest.py`, VERIFIED by its self-check; both move into `TC/`):

1. Stream the BINTABLE from the socket in chunks of about 1 M rows. Pick the column. Treat
   `BAD_DATA` as NaN. Degrade NESTED 2048 to 1024 by `reshape(-1, 4).mean(1)` — nested ordering
   makes a degrade a reshape.
2. **Refuse unless** the rms is in (30, 300) uK (`TUNIT` is not trusted), |mean| under 10 uK, p1
   and p99 within plus/minus 400 uK, and there are 0 NaN after the degrade.
3. For every cube texel, take the ICRF direction from the table above, rotate it to Galactic with
   the Hipparcos matrix, and look up `ang2pix_nest(1024, z, phi)` (nearest).
4. Quantise; write the mask (degrade 2048 to 256 with the maximum over children of "is
   inpainted", then sample per texel); write through `csdata`.
5. **Frame self-check before writing** (VERIFIED on the equirect): the rms by |b| band decreases
   monotonically from |b| under 2 deg to |b| above 30 deg, and **the Cold Spot mean within 5 deg
   of (l, b) = (209, -57) is -112 plus/minus 5 uK** (WMAP: -107 plus/minus 6).

**Core** `Core/Cmb/CmbSky.cs` (C19): `Parse`, `FaceSize`; `Sample(Double3 dirIcrf)` in uK (the
forward table, bilinear within a face, clamped at face edges — good to one texel, which is all
tap-to-measure needs); `IsInpainted(dir)`;
`TemperatureK(dir, CmbFrame frame) = T0 + D_frame*(n . d) + dT(n)/1e6` with the constants from
`CmbModel`.

**Tests (`CmbSkyTests`):** the **Cold Spot** 5 deg disc mean (equal-area weights) at -112 uK
(Planck) or -107 (WMAP) plus/minus 5 — the cube resample and 8-bit rounding move a 5 deg mean by
under 2 uK, while a mis-rotated map reads 0 plus/minus 20; the Galactic plane rms at |b| under
2 deg is over 1.05x the rms at |b| above 30 deg (the equirect measured 116.0 against 105.1 uK);
the mask fraction 2.1 plus/minus 0.3 % (Planck only); direction to texel to direction round trip
within half a texel on all six faces; decode raw 0 to -500.0 uK and raw 255 to +500.0 uK,
exactly.

**Provenance:** ΔT Measured ("Planck PR3 SMICA-noSZ, 5 arcmin") over 97.9 % of the sky and
**Inferred over the 2.1 % inpainted**, per texel from `mask` (WMAP: Measured, "foreground
residual along the plane not masked"); the dipole Measured (constants); the monopole Measured
(Fixsen 2009); the colour Procedural ("false colour: the Wien peak is at 1.06 mm").

### C.13 The fictional catalogue — `catalog/fictional_holosystem.json`, lane `fiction`

**Source:** the user's own `holographic_star_map(2).html` lines 117–126 (`starSystemData`: id,
radius, colour, a, b, inclination, speed, angle) and `holographic_star_map.html` lines 143–150
(names, `rings`, `secondary`), both VERIFIED by reading the laptop files. **Renamed**: the header
`SYS_MAP // MARVEL_COSMOS` is dropped and the system is named after its own central node,
**"VG-429 Holosystem"** (D8). **Schema:** exactly `solar_system.json`'s
(`CelestialCatalog.FromJson`), plus a top-level `"fictional": true`, `"rootName"` and
`"source"`. **Every provenance entry** is
`{"class": "Fictional", "source": "Nebulytic holographic star map (2026), authored"}`. Each body
gets an extra `hologram` block (colour hex, node radius, `ringed`, label offset), the skin's
input; INFERRED that `FromJson` ignores unknown keys, and the test proves it.

**Authored values**, physically consistent so `Validate()` passes, all Fictional. The centre star
has GM = 1.2 GM_sun, R = 1.10 R_sun, Teff 6,000 K.

| id | name | a (AU) | e | i (deg) | node Omega | M0 | R (R_earth) | M (M_earth) | density (kg/m^3) |
|---|---|---|---|---|---|---|---|---|---|
| fic-holo-dd-210 | Searing Fragment | 0.22 | 0.02 | 2.9 | 0 | 309.4 | 0.60 | 0.25 | 6,380 |
| fic-holo-pf-919 | Chrono Frontier | 0.35 | 0.03 | 8.6 | 0 | 28.6 | 7.5 | 60 | 784 |
| fic-holo-da-125 | Desert Sentinel | 0.52 | 0.04 | 12.6 | 180 | 103.1 | 0.9 | 0.7 | 5,300 |
| fic-holo-ag-514 | Sub-Zero Basin | 0.75 | 0.03 | 20.1 | 0 | 183.3 | 3.9 | 15 | 1,395 |
| fic-holo-ch-024 | Gaseous Nebula Orbit | 0.90 | 0.02 | 5.7 | 180 | 257.8 | 11.0 | 300 | 1,243 |
| fic-holo-az-704 | Outer Core Ring | 1.10 | 0.05 | 25.8 | 0 | 11.5 | 8.8 | 90 | 729 |
| fic-holo-ar-503 | Deep Boundary | 1.30 | 0.04 | 17.2 | 180 | 120.3 | 3.8 | 17 | 1,707 |

- **Why e at most 0.05.** The source's b/a implies e about 0.46–0.58, which would make the orbits
  cross. The paths are re-authored as nearly circular conics about the focus.
- **Negative source inclinations** become node 180 deg.
- **Not claimed stable.** The spacings are below about 10 mutual Hill radii (the 300 M_earth
  planet at 0.90 AU beside a 15 M_earth one at 0.75 AU), so this system would not survive
  millions of years. That is acceptable for authored content and is stated in the file's `source`
  note.
- **`displayColor`** is the hologram hex, which is sRGB; the runtime converts it to linear.
- **Ring flags** come from the base variant (VG-429, PF-919, AG-514, AZ-704, AR-503).

**Presentation contract:** its own simulation root, never merged into the real catalogue, no ICRF
placement. **Tests (`FictionalCatalogTests`):** every provenance field on every body is
`Fictional`; `Validate()` returns 0 errors; every id starts `fic-holo-` and none collides with a
real id; **Searing Fragment's period is 34.4 plus/minus 0.1 d** (Kepler III with 1.2 M_sun:
`365.25 sqrt(0.22^3 / 1.2)`); and the §5.3 stage-boundary test.

### C.14 Phenomena — `phenomena/`, lane `fiction`

| File | Content | Origin (VERIFIED on triton) |
|---|---|---|
| `encounter.bin` | 241 frames x 514 particles x xyz float32 LE = **1,486,488 B**, **SHA-256 `d5faf6d5e436b7fba63cc2e66e4cf21d97bcfafecfe4d077c71181ba4745ca78`** | `~/projects/CosmicAtlasXR/public/data/encounter.bin`, copied **byte for byte**; it is not re-containerised, because the SHA is the contract |
| `encounter.json` | 808 B, verbatim: format, particles 514, frames 241, duration 36, dt 0.002, captureStride 48, softening 0.09, seed 1701, centres [0, 257], massTotal 2.06, maxRelativeEnergyDrift 0.00575, sha256, limitations | the same directory |
| `exhibits.json` | `{families[3], exhibits[9]}`. Each exhibit has `id, family, title, short, subtitle, glyph, color, badge, duration, explanation, equation, parameter{name,min,max,step,value,unit}, layers[3], question, observation, methodTitle, method, source{name,url}, notes[3]`, the texts verbatim, **plus** a `provenance` block per exhibit (laws Inferred with the formula; simulated dynamics Procedural with the seed; illustrative visuals Fictional) and `sourceCommit` | `~/projects/CosmicAtlasXR/src/catalog.js` (14,495 B) |

**Authoring, once.** The implementer converts `catalog.js` into `TC/authored/exhibits.json` by
hand, keeping the texts verbatim. Running node interactively on triton is fine as a one-off
developer action, for example:

```
node --input-type=module -e "import('<abs>/src/catalog.js').then(m => process.stdout.write(JSON.stringify({families: m.families, exhibits: m.exhibits})))"
```

The implementer then adds the per-exhibit `provenance` block and records `sourceCommit`
(`git -C ~/projects/CosmicAtlasXR rev-parse HEAD`) and `sourceSha256` of `catalog.js`. **Node is
not a pipeline dependency** (binding 6).

**Importer** `build_authored.py`, the same stdlib script that handles the places files and the
fictional catalogue: (1) verify the SHA-256 of `encounter.bin` and that its length is
241 x 514 x 12, then copy it and `encounter.json` byte for byte; (2) validate `exhibits.json` —
3 families, 9 exhibits, every required key, a provenance block on each; (3) **warn** (do not
fail) when the current `catalog.js` SHA differs from `sourceSha256`, which means the user has
edited the source since the conversion; (4) write temp then `os.replace`.

**Core** `Core/Phenomena/EncounterCapture.cs`: `Parse(bytes, metaJson)` checks the length and the
SHA-256 (`System.Security.Cryptography.SHA256`, engine-free), then provides `Frame(i)` and
`Positions(t)` (a linear lerp between frames). **The N-body is never integrated on the headset.**

**Tests (`EncounterTests`):** the SHA matches; the shape is 241 x 514; **the mass-weighted centre
of mass drifts under 1e-5 between frames 0 and 240** — the masses are the two centres (particles
0 and 257) at 1.0 and 0.8 and 0.13/256 for each disc particle, cross-checked against the meta's
`massTotal` (1.0 + 0.8 + 512 x 0.13/256 = 2.06); the capture reports 4e-14 in doubles, and float32
storage of 514 positions of order 2 contributes about 5e-6; `exhibits.json` has 9 ids including
`black-hole` and `merger`.
---

## Appendix D — The licence ledger (`docs/data-sources.md`)

The file is **replaced wholesale** at P0.2 with this structure, keeping its good prose (the
Horizons notes, the NASA 3D Resources lessons, the HYG honesty notes and "Hosted by NASA is not
public domain"):

1. **The licence profile in force.** Name `TC/licence.py`, give the current value
   (`sideload-nc`) and the decision record `TC/licence_decisions.json`.
2. **The ledger table** (below). It is the source of `manifest.json` `attribution`, and
   `ledger.py --check` verifies that every shipped file's sources appear in it.
3. **Corrections, recorded as corrections:**
   - (a) **ESA Space Science Archive data (Planck, Gaia, Hipparcos) is CC BY-NC 3.0 IGO**, with
     commercial use only by written request to `data.licences@esa.int` (VERIFIED against the ESDC
     terms and the Gaia licence page). The present "Stars (deeper than HYG)" row saying
     "CC BY-SA 3.0 IGO" (VERIFIED wrong) conflated the ESA *image* licence with the data licence.
   - (b) Stellarium's *modern* sky-culture data is CC BY-SA 4.0, not GPL.
   - (c) VizieR tables from AAS journals are CC BY-NC per CDS.
   - (d) The HYG/AT-HYG ShareAlike-versus-ESA-upstream question stays open, as a lawyer question
     (D1).
   - (e) **ShareAlike, now** (R54). The ledger recorded the ShareAlike obligation only as an open
     *commercial* question and never stated the obligation that already applies to **this** build.
     `sky/stars.csstars`, `sky/stars_named.json` and `sky/constellations.json` are adaptations of
     CC BY-SA 4.0 material (joined, de-reddened, re-encoded, re-indexed to row numbers) and are
     themselves **offered under CC BY-SA 4.0**. Their manifest credit entries carry `licence`,
     `licenceUrl` (`https://creativecommons.org/licenses/by-sa/4.0/`) and a `changed` line —
     "joined AT-HYG v4.0 astrometry with HYG v4.4 photometry, de-reddened with Vergely+2022,
     re-encoded as CSS2, sorted by V" for the star file, "HIP numbers resolved to CSS2 row
     indices" for the names and the figures — the CREDITS page prints all three, and this file
     repeats them. **This holds for every profile and is independent of D1**, which is only about
     whether a *commercial* release may ship ESA-derived values at all.
   - (f) **No shipped file mixes incompatible tags** (R40, §6.2.2). The Reylé 10 pc census is
     `nc` and therefore ships as its own file `sky/census.cspt`, never inside the BY-SA star file.
4. The per-source notes, one short section each, **with one `lane:<name>` slot per lane** (§9.4).
5. "Reproducing the data", with the importer order of §6.9.

| id | Dataset to file | Source | Terms (as read) | Tag | Commercial | Attribution (ships in-app, verbatim) | Fallback |
|---|---|---|---|---|---|---|---|
| jpl-horizons | planets, moons, spacecraft to `catalog/*.json` | ssd.jpl.nasa.gov/horizons | US Government work; acknowledgement requested | pd-ack | yes | "Ephemerides: JPL Horizons, NASA/JPL-Caltech Solar System Dynamics (DE440/DE441)" | — |
| nasa-3d | `models/*.csmesh` | github.com/nasa/NASA-3D-Resources | NASA media guidelines; checked per model | pd | yes | per model, as today | — |
| nasa-maps | `surfaces/*.jpg` | NASA Visible Earth; LRO LROC | NASA; the LROC credit goes to ASU | pd-ack | yes | "Earth: NASA Blue Marble. Moon: NASA LRO LROC (Arizona State University)" | — |
| athyg | star astrometry to `sky/stars.csstars` | codeberg.org/astronexus/athyg | CC BY-SA 4.0; values upstream from Gaia DR3 and Hipparcos (ESA, CC BY-NC 3.0 IGO) | by-sa | **open question (D1)** | "AT-HYG v4.0 and HYG v4.4 star databases (astronexus / D. Nash), CC BY-SA 4.0 — compiled from Gaia DR3 (ESA/Gaia/DPAC), Hipparcos and Tycho-2 (ESA), the Yale Bright Star and Gliese catalogues" | none; the commercial profile refuses until decided |
| hyg44 | star photometry to the same file | codeberg.org/astronexus/hyg | CC BY-SA 4.0 | by-sa | open question | (combined with the line above) | — |
| gaia-ack | upstream of athyg | — | ESA Gaia licence | — | — | "This work has made use of data from the European Space Agency (ESA) mission Gaia (https://www.cosmos.esa.int/gaia), processed by the Gaia Data Processing and Analysis Consortium (DPAC)." (INFERRED standard wording; confirm) | — |
| reyle | the 10 pc census merged into the stars | CDS J/A+A/650/A201 | A&A: free for scientific use | nc | no | "Reylé C. et al. 2021, A&A 650, A201 (CDS)" | omit the census |
| stellarium | constellations to `sky/constellations.json` | github.com/Stellarium/stellarium skycultures/modern | "Text and data: CC BY-SA 4.0" | by-sa | yes | "Constellation figures: Stellarium 'modern' sky culture, Stellarium contributors, CC BY-SA 4.0" | — |
| nea | exoplanets to `catalog/exoplanets.csexo` | exoplanetarchive.ipac.caltech.edu | acknowledgement specified (VERIFIED) | pd-ack | yes | "This research has made use of the NASA Exoplanet Archive, which is operated by the California Institute of Technology, under contract with the National Aeronautics and Space Administration under the Exoplanet Exploration Program." | — |
| sbdb | belts to `sun/smallbodies.cssb` | ssd-api.jpl.nasa.gov | as Horizons | pd-ack | yes | "Small-body orbits: JPL Small-Body Database, NASA/JPL-Caltech SSD" | — |
| swpc | F10.7 and the snapshot to `sun/*` | services.swpc.noaa.gov | US Government | pd | yes | "Space weather: NOAA Space Weather Prediction Center" | — |
| silso | the sunspot number to `sun/activity.cssa` | via SWPC; origin WDC-SILSO | CC BY-NC 4.0 (INFERRED; verify) | nc | no | "Sunspot data: WDC-SILSO, Royal Observatory of Belgium, Brussels" | drop the SSN columns |
| vergely | dust to `galaxy/dust.csdu` | CDS J/A+A/664/A174 | A&A: free for scientific use | nc | no | "3D dust: Vergely J.-L., Lallement R., Cox N. L. J. 2022, A&A 664, A174" | edenhofer |
| edenhofer | dust (commercial) to the same file | zenodo.org/records/10658339 | CC BY 4.0 (VERIFIED) | by | yes | "3D dust: Edenhofer G. et al. 2024, A&A 685, A82, CC BY 4.0" | — |
| skowron | Cepheids to `galaxy/cepheids.csgp` | github jskowron/galactic_cepheids | "NOT licenced under the MIT or CC licenses" | unclear | no | "Classical Cepheids: Skowron D. M. et al. 2019, Science 365, 478" | none (the layer is absent) |
| lvdb | dwarfs and globulars to `lv/`, `galaxy/globulars.csgp` | github apace7/local_volume_database v1.1.1 | CC0-1.0 (VERIFIED); citation requested | cc0 | yes | "Local Volume Database: Pace A. B. 2025, OJAp 8, 142 (doi:10.33232/001c.144859)" | — |
| ungc | Local Volume giants to `lv/localvolume.cslv` | CDS J/AJ/145/101 | AAS via VizieR: CC BY-NC | nc | no | "Karachentsev I. D., Makarov D. I., Kaisina E. I. 2013, AJ 145, 101 (UNGC)" | `authored/giants_fallback.json` |
| 2masx | PAs and axis ratios to `lss/2mrs.csrs`, `lv/` | CDS VII/233 (or IRSA) | 2MASS public; the VizieR rights page (AJ) | nc | via IRSA (INFERRED) | "This publication makes use of data products from the Two Micron All Sky Survey, which is a joint project of the University of Massachusetts and the Infrared Processing and Analysis Center/California Institute of Technology, funded by the National Aeronautics and Space Administration and the National Science Foundation." (tail VERIFIED in the ReadMe) | fetch from IRSA |
| reid19 | arms (Core constants); masers to `galaxy/masers.csgp` | ApJ 885, 131; CDS J/ApJ/885/131 | AAS NC for the table; the constants are cited facts | nc | constants yes, masers no | "Spiral arms and masers: Reid M. J. et al. 2019, ApJ 885, 131" | drop the masers |
| 2mrs | `lss/2mrs.csrs` | CDS J/ApJS/199/26 | AAS NC | nc | no | "2MASS Redshift Survey: Huchra J. P. et al. 2012, ApJS 199, 26" plus the 2MASS line | sdss |
| cf4 | `lss/cf4_groups.cspt` | CDS J/ApJ/944/94 | AAS NC | nc | no | "Cosmicflows-4: Tully R. B. et al. 2023, ApJ 944, 94" | none |
| cf4grids | `lss/cf4_basins.csgr`, `lss/cf4_delta.csgr` | projets.ip2i.in2p3.fr/cosmicflows | none stated: "cite the article" | unclear | no | "Basins of attraction: Dupuy A., Courtois H. M. 2023, A&A (arXiv:2305.02339). Density: Courtois H. M. et al. 2023 (arXiv:2211.16390)" | none (the Laniakea outline is absent) |
| sdss | `lss/sdss.cspt` | skyserver.sdss.org DR18 | "considered in the public domain" (VERIFIED) | pd | yes | "Sloan Digital Sky Survey DR18 (sdss.org)" | — |
| simbad | cluster positions to `lss/structures.json` | simbad.cds.unistra.fr | CDS acknowledgement | pd-ack | yes | "This research has made use of the SIMBAD database, operated at CDS, Strasbourg, France" (INFERRED standard wording) | — |
| planck | the CMB to `cmb/cmb_sky.cscb` | pla.esac.esa.int | **ESA CC BY-NC 3.0 IGO** (VERIFIED) | nc | only with ESA permission | "Based on observations obtained with Planck (http://www.esa.int/Planck), an ESA science mission with instruments and contributions directly funded by ESA Member States, NASA, and Canada." (INFERRED standard wording) plus "CMB map: ESA and the Planck Collaboration (PR3 SMICA-noSZ)" | wmap |
| wmap | the CMB (commercial) to the same file | lambda.gsfc.nasa.gov | US Government | pd-ack | yes | "We acknowledge the use of the Legacy Archive for Microwave Background Data Analysis (LAMBDA), part of the High Energy Astrophysics Science Archive Center (HEASARC). HEASARC/LAMBDA is a service of the Astrophysics Science Division at the NASA Goddard Space Flight Center." (INFERRED wording) | — |
| papers | Core constants (Planck 2018 I, Fixsen 2009, Reid 2019, IBEX, Voyager, Meeus/ELP, the JPL approximate elements, IAU WGCCRE) | — | published facts | cite | yes | listed in the credits under "Constants" | — |
| own-landmarks | `galaxy/landmarks.json`, `lss/structures.json`; the LV overrides and the giants fallback | this project | CC0; facts with a reference per entry | own | yes | per-entry references shown in the inspector | — |
| cosmicatlas | `phenomena/*` | ~/projects/CosmicAtlasXR (the user's) | own work | own | yes | "Phenomena: Cosmic Atlas XR (Nebulytic)" | — |
| holomap | `catalog/fictional_holosystem.json` | the user's holographic star map | own work | own | yes | "Fictional system: Nebulytic holographic star map" | — |

---

## Appendix E — The complete bindings table

Every row lives in `XrInput.Bindings`, whose element type — `Binding`, with `BindDevice` and
**`BindContext` enums** — is declared verbatim in §5.7.4. The legend (INFO -> HELP, the status
card, the first-run hint) is generated from it for whichever input is active. Right-handed by
default; **Handedness: Left** mirrors every L/R pair and puts the watch on the right wrist.
`binding_gate.py` (G0g) requires each **(Control, Device, Context)** triple to appear once.
Today's array is `(string Control, string Action)[]` with 9 rows (VERIFIED `XrInput.cs:58`), so
**G0g is known-red until P0.7**.

**The Context column below is an enum member, not prose** (R37). It was free text with ten
spellings for about six states — "any", "default", "panel", "ray on the panel", "the hand is not
panel-hot" — and a gate comparing sentences would have passed a real collision, which is exactly
the "semantic collision git cannot see" §9.6 says the gates exist to catch. The values are
`Any`, `Default`, `PanelRay`, `NotPanelRay`, `PanelTouch`, `Measure`, `Sandbox`, `HasSelection`,
`ExhibitStage`, `RealOrFictionalStage`, `TrayHot`.

### E.1 Controllers

| Control | Action | Context | Change from 1.9.0 |
|---|---|---|---|
| L stick | fly along the full head direction (3D), speed from the nearest object of any layer | `RealOrFictionalStage` | was planar; the rise axis is removed |
| L stick X | yaw the diorama | `ExhibitStage` | new |
| L stick click | FOLLOW / FREE on the selection | `Any` | new |
| L trigger (analogue) | boost: flight x1–x6, zoom x1–x4 | `Any` | zoom boost new |
| L grip | grab the stage; with R grip, scale plus a yaw twist | `Any` | twist new |
| X | wrist panel open / close / recall | `Any` | the same key, a new panel |
| Y | time: step slower (towards reverse) | `Any` | same |
| L menu, hold 0.8 s | recentre | `Any` | was A; menu availability INFERRED, fallback TOOLS -> Recentre |
| R stick X | snap turn 30 deg (smooth turn 60 deg/s if enabled) | `Any` | same |
| R stick Y | zoom, 0.6 decades/s | `NotPanelRay` | was rise |
| R stick Y | scroll the panel | `PanelRay` | new |
| R stick click | pause / play | `Any` | same |
| R trigger | select | `Default` | same |
| R trigger | press | `PanelRay` | same, now arbitrated by `PanelHot` |
| R trigger | pick a measure point | `Measure` | new |
| R trigger, hold, pull, release | aim and launch a probe | `Sandbox` | new |
| R grip | grab the stage; with L grip, scale plus twist | `Any` | same |
| A | GO to the selection | `HasSelection` | was recentre |
| B | time: step faster | `Any` | same |

### E.2 Hands (XR Hands; the pinch closes at 22 mm and opens at 35 mm, VERIFIED)

| Control (gesture) | Action | Context | Note |
|---|---|---|---|
| pinch, hold and move (one hand) | grab and drag the stage | `Default` | i.e. the hand is neither `PanelHot` nor `TrayHot` |
| pinch with both hands | scale by (span ratio)^2, log-smoothed, plus a yaw twist | `Any` | |
| tap-pinch | select | `Default` | opens within 0.35 s, travel under 30 mm, aim ray on an object |
| tap-pinch | press | `PanelRay` | a pinned panel out of reach |
| tap-pinch | pick a measure point | `Measure` | |
| double tap-pinch | GO to the object | `Default` | the second within 0.45 s; hands have no A button |
| index fingertip on the panel | touch press / slider / drag-scroll (§5.6.8) | `PanelTouch` | |
| the dominant index tip within 5 cm of the watch, 800 ms debounce | panel open / close / recall | `Any` | |
| pinch near the launch body, pull back, release | launch (22 mm / 45 mm hysteresis and the release buffer) | `Sandbox` | |
| pinch on a tray specimen | grab it, claiming `TrayHot(hand)` | `TrayHot` | the room-furniture exception, §5.3 (R45) |
| (no flight gesture) | cover distance by drag, two-hand zoom, rungs and GO | `Any` | |

### E.3 Desktop (the flat editor and the harness)

Device `Keyboard` for every row. (R37: the gate keys on the triple, so the Context column exists
here too; most desktop keys are unconditional.)

| Key / mouse | Action | Context |
|---|---|---|
| W A S D / Q E | fly / down, up | `RealOrFictionalStage` |
| right-drag | look | `Any` |
| wheel | zoom 0.1 decade per notch (was "trim speed") | `NotPanelRay` |
| Shift / Ctrl | boost / slow | `Any` |
| left click | select | `Default` |
| left click | press | `PanelRay` |
| left click | pick a measure point | `Measure` |
| double click | GO | `Default` |
| G / F / X | GO / FOLLOW toggle / FREE | `HasSelection` |
| `,` and `.` | time slower / faster | `Any` |
| Space / N | pause / Now | `Any` |
| Tab | show / hide the panel (pinned in front of the camera) | `Any` |
| 1–5 | panel tabs GO, LAYERS, TIME, TOOLS, INFO | `Any` |
| Page Up / Page Down | the next rung up / down the ladder | `Any` |
| M | measure mode | `Any` |
| R | recentre | `Any` |

The OS recentre (holding the Meta button) moves the tracking origin; the app re-places the active
stage in front of the user when it sees the tracking-origin-updated event (API name INFERRED,
§13 item 12).

---

## Appendix F — The standard lane preamble

Paste verbatim; fill `<lane>`, `<phase>`, `<card>`.

```
You are the CosmoSynth lane agent for lane `<lane>`, phase <1a|1b|3>. Your lane card is §9.5 of
~/projects/CosmoSynth/docs/CONTRACT.md, under the heading `<lane>`. That card and tools/lanes.txt
list the ONLY files you may change. docs/CONTRACT.md supersedes the four design sections in
docs/design/; read it, not them, when they differ.

ENVIRONMENT (hard rules)
- Server: triton, Ubuntu 24.04, user `triton`, reached with `ssh triton@100.127.10.14 '<cmd>'`. It is
  shared with about 10 other projects' Unity editors; disk is about 98% full and swap is often
  exhausted. Treat it as fragile. If you run on the Windows laptop: no local servers, browsers,
  rendering or heavy processes; any ssh payload containing backticks, $, ! or apostrophes must be
  written to a local file (LF line endings), scp'd, and run with bash.
- Your tree: ~/projects/CosmoSynth-wt/<lane>, branch lane/<lane>. It has no Unity Library and must
  never get one. Never touch ~/projects/CosmoSynth (main) or any other worktree.
- Never: launch Unity in any mode; run tools/build.sh, tools/editor-method.sh or tools/shot.sh; write
  to ~/builds; `du` any Library; delete anything outside your tree; pkill by pattern; rebase,
  force-push or reset any branch; edit ~/control-panel, ~/server-setup, ~/README.md or
  ~/DATA-INVENTORY.md.
- Resources: run dotnet and python under `nice -n 10`. Before any download over 50 MB, check that
  `df -BG /` shows at least 6 GB free; otherwise stop and report BLOCKED. Raw downloads go to
  ~/.cache/cosmosynth/raw/<source>/ and are deleted after the bake unless your card says keep. Numpy
  bakes run under `flock /tmp/cosmosynth-bake.lock`. One download per remote host at a time.
- Tests: `./run-tests.sh` in your tree (it reads main's ScriptAssemblies read-only). Gates:
  `tools/gates/g0.sh`. Ownership: `tools/lane.sh check <lane>`.
- Captures (1b): you cannot render. Write /tmp/cosmosynth-shotq/<lane>-<epoch>.req with lines
  commit=<sha on your branch>, views=<list from your card>, hide=, profile=. Results appear in
  /tmp/cosmosynth-shots/<lane>/<sha7>/ after the integrator serves the queue. Copy the PNGs to your
  scratchpad (scp), Read them, and write what each one shows. At most 2 requests per day unless the
  integrator says otherwise.
- Staying current: `git merge green` (the integrator's last green master). Never merge master
  directly, never rebase. Commit small and often on lane/<lane>.

CODE RULES (binding)
 1. Core never references UnityEngine. Maths, parsing and anything testable live in Core, with tests.
 2. One MonoBehaviour per file, named after the file; every .cs declares a type named after its file.
 3. Colour only from CosmoTheme (your slot). UI sizes in dmm (angular). Containers sized from contents.
    Text may use only the gated glyph set of CONTRACT §5.6.9.
 4. Every controller or hand binding is a row in XrInput.Bindings (CONTRACT Appendix E). Nothing else
    reads a button.
 5. Anything that decorates, picks or measures a body checks IsGeometryDrawn every frame.
 6. Shaders are custom single-variant `CosmoSynth/*` URP HLSL with the six stereo macros (the
    skeleton is CONTRACT Appendix A). Additive passes never write alpha into passthrough: exactly
    `Blend One One, Zero One`, or `ColorMask RGB`. ZTest LEqual unless a `// ztest-justified:`
    comment. No multi_compile or shader_feature except multi_compile_instancing. Register the name in
    CosmoShaders.All inside your slot. Never TextMeshPro. **Never `GetCameraPositionWS()`: use
    `CS_EyePositionWS()`.** No fragment function reads object space — compute it in the vertex
    function and pass a varying. Every shader declares `_Fade`. Any shader reading object space
    carries `"DisableBatching"="True"`. Jitter from the ray (`CS_JitterDir`), never from the pixel.
    No matrix in `UnityPerMaterial`.
 7. Exactly one camera. Never add a Camera, CopyFrom, Depth/Nothing clears, targetTexture,
    Graphics.DrawMesh/RenderMesh/Blit, GL, CommandBuffer, OnRenderImage or Camera.current.
 8. Distances exactly as CONTRACT §2 says: near content linear and depth-tested; far content through
    the per-GROUP similarity about the centre eye (never per-body scaling); the sky layer at infinity,
    eye-anchored and drawn first. The ICRF-to-Unity mapping exists only inside FloatingOrigin: never
    swap axes yourself.
 9. Populations are CosmoLayer renderers (one mesh or a few) backed by an engine-free Core parser with
    tests (CONTRACT §4). They are never CelestialBodyViews. Bodies added after startup use
    AddBodies/RemoveBodies. Never call SetCatalog.
10. The runtime never touches the network. Read StreamingAssets only through
    Runtime/Data/StreamingAssetReader. Degrade gracefully when an optional (droppable) file is absent;
    the manifest is the only list of files.
11. Importers: stdlib Python plus tools/cosmodata/{csdata,fetch,fits,licence,ledger}.py (numpy only if
    your card says so). Write to a temp name, then os.replace. Per-field provenance. Assert physical
    ranges; never trust FITS TUNIT. Refuse zero-row parses and HTML-instead-of-data downloads. Record
    the source URL, licence, attribution and `droppable: yes/no` in your docs/data-sources.md slot.
    ESA archive data is CC BY-NC 3.0 IGO.
12. A numeric tolerance carries a comment saying what the measurement is worth. Every new test has
    been seen failing once on a deliberately wrong input.
13. Shared files: only between your `lane:<lane> BEGIN/END` markers. Never create .meta files. Never
    edit ProjectSettings, Packages, Assets/XR, any csproj, CLAUDE.md, README.md or docs/feedback.md;
    put what you need under "Requests to the integrator" in your report.
14. In every report and comment you write, each claim is VERIFIED (you ran or read it; say where) or
    INFERRED.

READ FIRST
- CLAUDE.md and README.md in your tree.
- docs/CONTRACT.md: §0, §1.2 (the conflict register), your lane card in §9.5, §9.3 and §9.6, plus
  every section your card cites — typically §2 (foundation), §4 (layers), §6 and Appendix C (data),
  Appendix A (shaders), §11 (gates).
- The research reports your card names (docs/research/2026-09-11-*.md).

DONE (for your phase)
- `tools/gates/g0.sh` exits 0 in your tree, **except G0f, which only the integrator can make
  green** — the `SA/manifest.json` it checks is regenerated at merge time (§9.6 step 3b) and is
  owned by no lane, so a data lane would otherwise fail a gate it has no way to fix. Instead show
  `python3 tools/cosmodata/ledger.py --check --candidate <your new SA files>` clean in your report.
  Also except any gate listed in `tools/gates/known-red.txt` (G0g until P0.7).
  `tools/lane.sh check <lane>` exits 0.
- Every new Core file has tests; the Core suite count went up; nothing that passed before fails.
- 1b: every view your card names has been captured, read and described; your layer's SelfCheck()
  returns nothing in the capture reports.
- docs/reports/<lane>.md is complete (Appendix G) with Status READY-1A or READY-1B and the head sha.
- Everything is committed on lane/<lane>; `git status` is clean; you have merged the latest `green`.
Then stop and say READY. Do not merge into master yourself.
```

---

## Appendix G — The lane report format

`docs/reports/<lane>.md`, owned by the lane.

```
# Lane <lane> — report
Status: WORKING | READY-1A | READY-1B | BLOCKED (<one line why>)
Branch: lane/<lane> · Head: <sha> · Last merged green: <sha> · Updated: <ISO datetime>

## Summary            (at most 10 lines: what exists now that did not before)
## Files              (paste `git diff --stat $(git merge-base green HEAD) HEAD`)
## Gates              (tail of tools/gates/g0.sh; output of tools/lane.sh check <lane>)
## Tests added        (name -> what it proves -> the wrong input that made it fail once)
## Data               (per output file: source URL, licence, attribution, droppable, bytes,
                       provenance classes, range assertions, raw download deleted? y/n)
## Hook lines used    (file -> slot -> lines added)
## Captures           (request file, sha, view -> one line: what the image shows)
## Requests to the integrator (csproj references, packages, settings, new slots, doc text for
                       CLAUDE.md/README/accuracy, capture requests, anything blocked)
## VERIFIED / INFERRED (every claim of consequence, tagged)
## Headset checks this lane needs (items for the next handoff checklist)
## Open questions
```

The integrator keeps `docs/reports/integrator.md`, an append-only log of each merge, gate result,
revert, editor-run duration and peak RSS, and writes `docs/reports/handoff-<ver>.md` per publish.
---

## Appendix H — The verified gate rules (implementation source)

**These rules already ship** (R22). `tools/gates/stereo_gate.sh` and `tools/gates/code_gate.sh`
were merged at `49741aa` and are self-tested; **this appendix is kept only as the derivation, and
where it and the shipped script differ, the shipped script wins.** Do **not** copy the shell below
into a gate — it would regress them. The differences, all VERIFIED on triton at `fc07a52`:

- **The ZTest marker is `// ztest-justified:`**, which §2.2, §7.1, §11.1 G0b and Appendix F all
  mandate and `stereo_gate.sh:69` greps. Appendix H's `grep -v 'ztest-always:'` matches nothing
  anyone writes, so justified passes would be flagged and unjustified ones would slip through.
- **The macro list has six entries, including `UNITY_SETUP_INSTANCE_ID`**, which Appendix H's
  `gate_stereo` omits and `stereo_gate.sh:27` checks.
- **No gate uses `grep -q`.** §10's own script conventions forbid it (the SIGPIPE trap);
  Appendix H uses it throughout, and the shipped gates count with `grep -c` through their `cnt`
  helper and say why at `stereo_gate.sh:19` and `code_gate.sh:23`.
- **Scope.** §11.1 G0d requires `U/Runtime/**` **and** `U/Core/**`; Appendix H's `gate_camera`
  scans `$1/Runtime` only, and so does the shipped `code_gate.sh:18` today — **P0.1 extends the
  shipped script to Core** (R21), rather than anyone copying Appendix H's narrower scan.
- The new G0b/G0d rules of this revision (no `GetCameraPositionWS`, no object space in a
  fragment, `_Fade` everywhere, `DisableBatching`, `IsExternalInit.cs` present, no
  `textureType: 1`) exist only in §11.1 and are added to the shipped scripts at P0.1.

What follows is the original laptop script, unchanged, as the record of what was actually run and
what it flagged on `cf93a25`.

```bash
#!/usr/bin/env bash
# Static render-foundation gates. Rules folded into stereo_gate.sh and code_gate.sh (CONTRACT C14).
# Usage: <script> [ASSETS_DIR]   exit 0 = every gate passes
#        <script> --self-test    every gate must FAIL on a planted violation
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
  h=$(grep -rnE --include='*.cs' '"(Standard|Unlit/[A-Za-z]+|Sprites/Default|Particles/[^"]+|GUI/Text Shader|TextMeshPro/[^"]+|Universal Render Pipeline/[^"]+)"' "$1/Runtime") && bad "shaders: built-in shader name in Runtime: $h"
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
  for c in RingSystem AtmosphereShell InspectorCard; do
    f=$(find "$1/Runtime" -name "$c.cs" | head -1)
    [ -n "$f" ] || continue
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

**What it reports on today's tree** (VERIFIED, and therefore the P0.4/P0.5 checklist): the two CG
shaders (no URP tag); the six built-in shader names in `CosmoShaders.cs:22-43`; two
`AddComponent<Camera>` plus `DepthBands` at `XrRig.cs:68,78` and `ScaleRenderer.cs:29,106-107`;
the `.x, .z, .y` swaps at `SceneShot.cs:143`, `SkyAndRings.cs:220` and
`CelestialBodyView.cs:904,905,913,917`; and the removed helpers. After §8's migration each of
these must be clear.

**Extra rules the two shipped gates add on top of this script** (§11.1): the `CosmoSynth/...`
name check, the variant justification, the `ColorMask RGB` alternative, the `Blend One One,
Zero One` end-anchored regex, the `Camera.current` / `Graphics.RenderMesh` additions, the
`SetCatalog(` count, the `new Color(` rule, the `RenderedRadius` implies `IsGeometryDrawn` rule,
and the camera count written to `$TMPDIR/cosmosynth-g0d.cameras` for G0c and G3.

---

## Appendix I — Revision log (review of 2026-09-12)

Fifty-four review issues, every one dispositioned. **FIXED** = applied, and the section says so.
**FIXED (amended)** = applied, with the reviewer's reasoning or one sub-part corrected against the
code or the reports — those are called out, because an implementer who read only the review would
otherwise build the wrong thing. **REJECTED** = one line of evidence saying why. Nothing is
deferred to the user; D5 and D12 remain the user's calls and are unchanged in kind, D12's *default*
having moved from a guess to "decide against the B.14 inventory".

In-document, each change is tagged `R<n>` with the issue number, so any edit can be traced back.

| # | Class | Disposition | Where | Note |
|---|---|---|---|---|
| 1 | blocker | **FIXED** | A.2 (`CS_EyePositionWS`), A.3–A.7, §2.2, §7.1 rule 2, §11.1 G0b, §11.3 `stereo`, §13 item 7, §11.6 H1 | The four eye-anchored paths now read `UNITY_MATRIX_I_V`. The `stereo` assertion is explicitly documented as **unable** to catch the failure (monoscopic harness), so it is settled on the headset instead |
| 2 | blocker | **FIXED (amended)** | §7.3 (global 33, `nebulae` row), §4.3 lifecycle rule 4, §11.3 `volumes`, §2.2 queue 3010 | Global raised to 33 = B 30 + 3 for body atmospheres, stated once. **The reviewer's CF4-basin volume row is rejected**: §4.5 routes `basins` through `CosmoSynth/Ribbon` and `density` per-point, so neither is a volume; §2.2's stale "(dust, CF4 basins)" parenthetical is corrected instead |
| 3 | blocker | **FIXED (amended)** | §2.6 (tessellation rule), A.3, §7.3 Bodies row (150 k to 180 k), §11.3 `envelope`, §13 limits | Rule `d_max/d_min <= 1.1` and the `d0 ln(AM/GM)` = 0.0114 m bound adopted. **The reviewer's stated mechanism is corrected**: the silhouette does **not** warp — both endpoints keep their exact direction, so the rendered edge sweeps the same angular interval and projects to the same screen segment. The real errors are radial (depth) and parametric (attribute skew), and the section now says so |
| 4 | major | **FIXED** | §3.4, A.4, §7.1 rule 6, §3.1, §13 item 7 | `_LayerToWorld` deleted; frames baked; the "no matrix in `UnityPerMaterial`" rule generalised with the ShaderLab reason |
| 5 | major | **FIXED** | A.7 (`eyeOS` varying), §7.1 rule 9, §11.1 G0b | |
| 6 | major | **FIXED** | option (a): §13 "Known accepted limits", A.7 comment, §7.1 rule 5, §8.3 `render:` line (`vol`, a red condition) | Accepted and bounded, with the `volumeOcclusion` counter reported every frame, and the cost of the alternative written into §7.1 so no lane adds a depth-texture read casually |
| 7 | major | **FIXED** | A.2 (`CS_JitterDir`, `CS_IGN` removed), A.7, §2.5 point 7, §7.2 helper list, §11.6 H4 | |
| 8 | major | **FIXED** | §2.0 (`PixelsPerRadianCentre` about 1140), §2.4, §2.5, §5.6.10, A.2, A.4, A.5, §11.3 `stereo`, §11.6 H4, §13 item 39 | Every px figure restated and marked INFERRED; the assertion now reports the measured value |
| 9 | major | **FIXED** | §5.6.10 (one mesh per panel, one for all labels), §7.3 row 30 to 6, §11.3 `draws` | `CosmoText` already calls `Font.GetCharacterInfo` (VERIFIED `:131`) and `Create` returns a `TextMesh` (`:222`), so the mechanism is real and the old row was not reachable |
| 10 | major | **FIXED** | §2.6 walkthrough row (hard switch, 0.95 R / 1.05 R hysteresis), §11.3 `cmb-fade` | Resolved in favour of §9.5, as asked |
| 11 | major | **FIXED** | §11.2 (new `CS_Renderer.asset` row), §11.3 `pipeline`, §8.1 `EnsureUrp` | |
| 12 | minor | **FIXED** | A.5, A.6, A.7 tags; §7.1 rule 1; §11.2 `m_StaticBatching: 0`; §8.1 `EnsureStaticBatchingOff` | |
| 13 | minor | **FIXED** | §7.1 rule 10, §11.1 G0d (`.meta` grep for `textureType: 1`) | |
| 14 | minor | **FIXED** | A.4 intro, A.5 comment, §3.4 de-duplication bullet, `PointChunksTests` | |
| 15 | minor | **FIXED** | §2.0 (`IpdMetres`), §2.5, §11.3 `stereo` | |
| 16 | minor | **FIXED (amended)** | A.6 `Cull Off` comment | The old reason was wrong. **So is the replacement offered**: from the sphere's centre every triangle is seen from inside and they all share one facing sense, so culling removes all of them or none and **cannot** remove the rear hemisphere — near-plane clipping does (`w < 0`). "Halve the vertex work" is doubly wrong, since culling happens after vertex shading anyway. `Cull Off` is kept, with the correct explanation and the warning for accumulating passes |
| 17 | minor | **FIXED** | A.2 comment (option 1), §11.3 `stereo` (axis ratio within 15 %) | |
| 18 | blocker | **FIXED** | §4.2 (C# level note), P0.2(e), §5.7.1 `ViewState`, §11.1 G0d | Verified on triton: no record exists today, `IsExternalInit` is absent from netstandard.dll and from both `mscorlib` candidates, and both test projects pin `<LangVersion>9.0</LangVersion>`. Polyfill file added; `record struct` replaced by a plain readonly struct |
| 19 | blocker | **FIXED** | §9.6 step 3b, Appendix F DONE, `ledger.py --candidate` in §6.2.1 | |
| 20 | blocker | **FIXED (amended)** | §9.5 five lane cards and `lines`, §9.4, §4.2 tests | The rule and the six ids to strike are adopted. **The count is six duplicates, not five**: 51 claimed ids minus 6 duplicates = 45 distinct, plus the unclaimed `quasars` = 46 |
| 21 | blocker | **FIXED** | header re-pinned to `fc07a52`, §10 opening, P0.1 rewritten as reconciliation, §10.7 `run-tests.sh`, §11.4 font row | Every claim re-verified on triton: `tools/` and `tools/gates/` contents, `common.sh:21`, the `font`/`font-tmp` pair in `verify-apk.sh`, `lane.sh:92-95`, `run-tests.sh:60-100`, `code_gate.sh:18`. P0.1 also now removes the `TMP_SDF-Mobile` check, which C3 makes unpassable |
| 22 | major | **FIXED (amended)** | Appendix H preamble | The shipped script wins, as asked. **One correction:** the reviewer says `code_gate.sh` scans `U/Runtime/**` and `U/Core/**`; it scans Runtime **only** (`:18`, VERIFIED), and §11.1 requires both — so P0.1 extends the shipped script rather than anyone copying Appendix H |
| 23 | major | **FIXED** | §9.3 rule 1, `lanes.txt` header, `p0-core` and `stars` globs narrowed, `own_gate.py --audit` at P0.2 | |
| 24 | major | **FIXED** | `lanes.txt` 1a narrowing list, §9.4 (a new `docs/accuracy.md` slot row), §9.3 integrator list, the `stars` card | Slots chosen over moving the work to 1b: C11's bound is a 1a measurement |
| 25 | major | **FIXED** | P0.2(c), §6.9.1 (the `AUTHORED` row schema verbatim), §9.4 | |
| 26 | major | **FIXED** | §5.7.4 (the `Binding` struct and both enums, verbatim), §9.4, §11.1 G0g known-red until P0.7 | |
| 27 | major | **FIXED** | C10 is superseded by the amended P0.4d; §9.3 integrator test list | Verified: `ShippedCatalogTests.cs` has zero exoplanet references; `ExoplanetCatalogTests.cs` reads the deleted file and declares 7 tests; `test-floor.txt` = 142 |
| 28 | major | **FIXED** | §4.1 (`UpdateWeight` / `Apply` split), §4.3 item 3, §2.8 frame order | |
| 29 | major | **FIXED** | §4.2 (`LabelCandidate` constructor plus `At`/`Toward`, `LayerContext` 13-field constructor, `LoadedKinds.Empty`) | |
| 30 | major | **FIXED** | §4.3, §7.1 rule 11, §11.1 G0b, A.3, A.6, A.8 | `_Fade` on all eleven; `Sky` keeps `_Immersion` as a separate switch and the CMB layer writes both |
| 31 | minor | **FIXED** | §5.2 (`PlaneContext` and `Planes.Up`, one declaration), §3.2, §3.6, §11.3 `plane` | |
| 32 | minor | **FIXED** | §9.4 LayerTable row, §4.6 note | |
| 33 | minor | **FIXED** | §4.1 `OnData` doc comment | |
| 34 | minor | **FIXED** | §2.8 (`PlaceRigid` places host children only; `SelfCheck` message) | |
| 35 | minor | **FIXED** | §6.5.2 (`droppable` in the schema), §11.1 G0f, §6.2.1 (`ledger.py` CLI) | |
| 36 | minor | **FIXED** | §11.1 G0f (`--full` only, `guard_resources`, EXIT trap, `--no-restore`, `SKIPPED` line), §9.2 capacity table | |
| 37 | minor | **FIXED** | §5.7.4 (`BindContext` enum), Appendix E.1, E.2 and E.3 rewritten against it | E.2's "double tap-pinch" row had a *note* in its Context column, which the rewrite also fixes |
| 38 | blocker | **FIXED** | `lanes.txt` (`fiction` gains `Runtime/Stage/**`, `Runtime/Sandbox/**`, `Core/Orbits/RestrictedNBody.cs`, two test files), §9.5 `fiction` card, §9.3 integrator list, §9.2 effort 3.0 to 5.0, §5.9.3 | Funded rather than deferred: the user asked for "all functionalities". `RestrictedNBody.cs` is claimed explicitly, which beats `solar`'s `Core/Orbits/**` under the new specificity rule (R23) |
| 39 | blocker | **FIXED (amended)** | new **B.13a** table, B.13 closing paragraph rewritten | The information the reviewer asked for, in one auditable place rather than a `2.0.0?` column on 225 rows — one table to keep correct instead of 225 cells. **The list is twenty, not forty-two**, because R38 funds NB-01..14 and SLG-01..09; B.13a says so explicitly |
| 40 | blocker | **FIXED** | C.1 (census split to `sky/census.cspt`), §4.6 (a `census` kind), §5.4 `census` row, §6.2.2 (the no-mixed-tags rule and the audit), §6.7, §6.10, Appendix D item 3(f) | |
| 41 | blocker | **FIXED** | C.1 (`av` column, flag 12, size 44 B), §5.9.2 (the formula and the 200-row test), §5.4 `dust` and `bodies` rows, §6.9.1 (the `--dust` ordering), §6.7, the `stars` card | **The cross-lane dependency the reviewer's ordering would create is avoided**: `--dust` is optional, the lane bakes `av = 0` with flag 12, and §6.9.1 step 10 (already in the plan) re-bakes with the cube after both lanes merge |
| 42 | major | **FIXED** | §5.4 preamble, the `bodies`, `belts` and `exohosts` rows, `LayerTableTests`, the inspector "assumed (not published)" rule | |
| 43 | major | **FIXED** | §5.9.6 (chip and badge split; `BRIGHTNESS` on effective gain), §11.3 `honesty` with its deliberately-broken run | |
| 44 | major | **FIXED (amended)** | §5.9.4 (two rings), C.6 `CmbModel` (`ParticleHorizonGpc` and a test), §0.2, §13 | The mislabelled ring is corrected. **Rung 18 is NOT re-spanned to 2 chi\***: its 8.8e23 km is exactly the particle-horizon **diameter** (14.26 Gpc x 2 x 3.0857e22 km/Gpc = 8.80e23), so the ladder and `ScaleLimits` were already right and only the ring's label was wrong. Framing the particle horizon also shows the CMB shell inside it, which the rung's own name asks for |
| 45 | major | **FIXED** | §5.3 (the room-furniture exception and the single fictional door), §5.9.7, §5.9.3, Appendix E.2 (`TrayHot`) | |
| 46 | major | **FIXED** | §12.2 notes template, §11.4 G3 licence row, §0.5 D1 | |
| 47 | major | **FIXED** | §6.5.2 (`features`), §4.2 (`LayerDef.RequiresFeature`, `LayerState.Status.FeatureMissing`, `LayerContext.HasFeature`), §5.4, §5.6.6, §6.9.2 G7, C.5, `LayerTableTests` | |
| 48 | major | **FIXED** | §5.6.6 (SHOW: This rung / All layers), `LayerTableTests` (6–16 rows at every rung) | |
| 49 | major | **FIXED** | B.12 row rewritten, B.13 totals exclude them, §0.5 D12 default changed, §13 item 40, the `solar` 1a scope | The claim is narrowed **and** the reading is scheduled, since the user's request starts at Earth |
| 50 | major | **FIXED** | §6.5.2 (`credits[]` structured; `ledger.py --check` fails without `licence`/`licenceUrl`/`changed`), §5.6.7, §9.4 | |
| 51 | minor | **FIXED** | C.11.5 (`StructuresTests`, six checks), `lanes.txt` galaxies globs, the `galaxies` card | |
| 52 | minor | **FIXED** | §5.9.8 first line, §5.10 row | |
| 53 | minor | **FIXED** | §5.6.4 (four chips, four priority bands, `+N` overflow into INFO -> THIS VIEW) | |
| 54 | minor | **FIXED** | Appendix D item 3(e) | |

**Rejected outright: none.** Three issues had a sub-part rejected with evidence (2's CF4-basin
volume row, 44's rung-18 re-span, 16's culling remedy) and three had their stated mechanism
corrected while the fix stood (3, 16, 22); all six are marked **FIXED (amended)** above and the
correction is written into the section itself, not only here.

**What changed in the numbers.** Global raymarch budget 30 to 33 step-coverages; Bodies vertices
150 k to 180 k; UI draws 30 to 6; star record 40 B to 44 B and `SA/` about 29.3 MB to about
29.8 MB; `fiction` 1b effort 3.0 to 5.0 agent-days and the project total 36 to 38; every pixel
figure restated from 1432 px/rad to 1140; far disparity 1–9 px to 1–7 px.

**Three new gate ids** (`volumes`, `envelope`, `draws`) and two more play-mode assertions
(`cmb-fade`, `honesty`) join §11.3; six new static rules join G0b and G0d; `G0g` starts known-red
with a removal date of P0.7.

---

*End of `docs/CONTRACT.md`. Every lane prompt cites this document by section: "CONTRACT §x.y".*
