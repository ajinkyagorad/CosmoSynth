# CosmoSynth 2.0 — CONTRACT summary for the orchestrator

`docs/CONTRACT.md` (7,356 lines) is the authority; this page is the map. Revised 2026-09-12 after
review: all 54 issues dispositioned in **Appendix I**, none rejected outright, six amended with
evidence. Baseline re-pinned from `cf93a25` to **`fc07a52`**.

## Architecture

- **One app.** Unity 6 (6000.5.5f1), Quest 3 MR passthrough-first, opt-in Space mode, desktop
  path for the headless harness. 2.0.0, versionCode ≥ 17.
- **One camera**, URP 17.5 + Linear, depth submission None, symmetric projection, MSAA 4 from the
  URP asset, SRP foveation, buffer discards on. `DepthBands` deleted. (§2.1, §8.1)
- **One distance map** `f(d) = d0(1 + ln(d/d0))`, d0 = 10 m, applied as **one similarity per
  group about the centre eye** — never per body. Sky layer at infinity, per eye. (§2.3–§2.6)
- **One ICRF→Unity mapping**, inside `FloatingOrigin` only. (§3.1)
- **One `SetCatalog`.** Populations are `CosmoLayer` renderers over engine-free Core parsers, not
  `CelestialBodyView`s. (§4)
- **Eleven custom single-variant `CosmoSynth/*` URP shaders** with the six stereo macros; every
  one now declares `_Fade`; none calls `GetCameraPositionWS()`. (§7, Appendix A)
- **One column-store container** per dataset under `SA/`, listed by `manifest.json`; the runtime
  never touches the network. (§6)
- **Everything generated from tables**: `LayerTable.Layers` (46), `Rungs` (19),
  `XrInput.Bindings`, `Destination`s. (§4.2, §5)

## Phases

| Phase | Where | Output | Gate to leave |
|---|---|---|---|
| **0** (~4–6 d) | main + `p0-core`, `p0-shaders` | **2.0.0-alpha.1** (code 17) | G0–G4 green, handoff written |
| **1a** (parallel) | 8 lane worktrees, never an editor | data + Core parsers + tests | `READY-1A`, merged FIFO in batches ≤ 3 after P0.7 |
| **1b** (3 waves ≤ 4 lanes) | the same lanes + `ui`, `lines` | runtime layers, UI, navigation | per-lane captures read at G1d |
| **2** | main | **2.0.0-beta.N** → **2.0.0** | full G0–G5, headset each beta |
| **3** | main + small lanes | `audio`, tuning, DEMs (G8), winds (G6), store work if asked | — |

~38 agent-days (was 36; `fiction` 1b rose 3.0 → 5.0). Phase-0 rule: **no 1a data merges before
the alpha is published**, so the user's headset A/B tests URP + one camera alone.

## Lanes and what each owns

Worktrees `~/projects/CosmoSynth-wt/<lane>` on `lane/<name>`, no Library, machine-claimed in
`tools/lanes.txt`. **Globs may overlap; the most specific match wins** — `own_gate.py --audit`
must print no equal-specificity collision at P0.2.

| Lane | Phases | Owns (beyond its report + data-sources/accuracy slots) |
|---|---|---|
| `p0-core` | 0 | `Core/Scale/{ScaledSpace,ScaleSpace,PointChunks}.cs`, `StandardFrames.cs`, `CelestialCatalog.cs`, `Core/Presentation/{ScaleLimits,PresentationPlane}.cs` + 3 test files |
| `p0-shaders` | 0 | `Runtime/Shaders/**` |
| `stars` | 1a,1b | `build_starfield.py`, `SA/sky/**`, `Core/Sky/{StarCatalog,StarSystemFactory,Constellations,Photometry,StarQuery}.cs`, `Runtime/Layers/Stars/**`, `StarLook.cs`, 1b `SkyAndRings.cs`, `NearbyStarSystems.cs` |
| `milkyway` | 1a,1b | `build_dust.py`, `build_galaxy_points.py`, `SA/galaxy/**`, `Core/Deep/{DustCube,GalaxyModel,PlaceCatalog}.cs`, `Runtime/Layers/MilkyWay/**` — **numpy** |
| `galaxies` | 1a,1b | `build_localvolume.py`, `build_lss.py`, `SA/{lv,lss}/**`, `Core/Deep/{GalaxySet,GalaxyOrientation}.cs`, `Core/Scale/Cosmology.cs`, `Runtime/Layers/Galaxies/**` — **numpy** |
| `cmb` | 1a,1b | `build_cmb.py`, `healpix_nest.py`, `SA/cmb/**`, `Core/Cmb/**`, `Runtime/Layers/Cmb/**` — **numpy** |
| `solar` | 1a,1b | the 11 solar importers, `SA/{catalog,models,surfaces,sun}/**`, `Core/{Orbits,Sun}/**` (except `RestrictedNBody.cs`), `BodyOrientation.cs`, `docs/accuracy.md`, `Runtime/Layers/Solar/**`, 1b the four decoration renderers |
| `exoplanets` | 1a,1b | `build_exoplanets.py`, `exoplanets.csexo`, `Core/Exo/**`, `Runtime/Layers/Exoplanets/**` |
| `fiction` | 1a,1b | the two authored JSONs, `SA/phenomena/**`, `Core/Phenomena/**`, `Runtime/{Fiction,Phenomena}/**`, **and new: `Runtime/Stage/**`, `Runtime/Sandbox/**`, `Core/Orbits/RestrictedNBody.cs`** |
| `nav` | 1a,1b | `Core/Nav/**`, `WarpLadder.cs`, `ObservingSites.cs`, `Runtime/Nav/**`, 1b the seven XR/input files |
| `ui` | 1b | `Runtime/UI/**`, `Core/Presentation/{Format,SizePresets}.cs`, `BodySelector.cs`, `AnchorBeacon.cs` |
| `lines` | 1b | `Runtime/Layers/Lines/**`, `OrbitLineRenderer.cs`, `Core/Grids/**` — **and all eleven ribbon `LayerDef` rows** |
| `audio` | 3 | `Core/Audio/**`, `Runtime/Audio/**` |

Integrator owns everything else, runs the **only** Unity editor (behind `flock`), Xvfb band
461–490, and regenerates `SA/manifest.json` at every merge that touches `SA/` (§9.6 step 3b).

## Gates

- **G0** (static, any tree, seconds): `run-tests.sh` floor 142 · `stereo_gate` · `settings_gate`
  · `code_gate` · `class_gate` · `data_gate` + `drop_test` (**`--full` only**, ~1 min, ~100 MB)
  · `binding_gate` (**known-red until P0.7**) · `own_gate`.
- **G1a/b** editor: `XrSetup.Run` → `XRSETUP_OK`; `ServerBuild.Diagnostics` → `DIAGNOSTICS_OK`.
- **G1c** play-mode, **21 assertions**: `catalog camera pipeline far push group stereo mapping
  plane alpha text shaders layers budget log` + new **`volumes` `envelope` `draws` `cmb-fade`
  `honesty`** + `cmb-faces`. Seven fixed capture views from §2.6's walkthrough.
- **G1d** a human reads every PNG. **G2** IL2CPP build. **G3** `verify-apk.sh` (adds a licence
  row: FAIL with `STORE=1` on any `unclear`/`nc` tag). **G4** publish. **G5** headset.
- Rule kept: *never trust a gate that has not failed once* — each new assertion lands with a
  deliberately broken run recorded in the integrator log.

## Open decisions (user)

| # | Decision | Default | Needed before |
|---|---|---|---|
| **D1** | licence profile | `sideload-nc`, **private sideload only, enforced**: behind the login, no public link, `unclear` files excluded from mirrors, licence line in every `notes.txt`, G3 fails with `STORE=1` | any store listing |
| **D2** | signing key | debug-signed for all of 2.0 | the first Meta upload |
| **D3** | store at all? | sideload only | Phase 3 |
| **D4** | test 1.9.0 on the headset now (gate P) | please do it now; blocks nothing | — |
| **D5** | Sun disk stills + SUVI loop | after 2.0.0; VITALS reads "not in this build" | Phase 2 freeze |
| **D6–D11** | 1:1 ladder top · default site · holo names · fictional at a real star · ambient sound · resume view | as in §0.5, unchanged | per row |
| **D12** | the six Earth/Mars globe pages | **changed**: "decide after the B.14 inventory" — the `solar` lane reads all six in 1a; until then the contract makes no coverage claim about them | Phase 2 freeze |

## What the revision changed in the numbers

Raymarch budget 30 → **33** step-coverages (B = 30 shared by one enclosing volume, + 3 bodies) ·
Bodies vertices 150 k → **180 k** · UI draws 30 → **6** · star record 40 B → **44 B** (`av`
de-reddening column) and `SA/` ~29.3 → **~29.8 MB** · every pixel figure restated from 1432 to
**1140 px/rad** · far disparity 1–9 → **1–7 px** · `fiction` 1b 3.0 → **5.0** agent-days.

## Things an implementer must not miss

1. `CS_EyePositionWS()` everywhere an eye position is needed; `GetCameraPositionWS` is gated out.
2. `Core/Compat/IsExternalInit.cs` must exist or **no** shared `record` type compiles.
3. A `LayerDef` row is written by the lane owning the **renderer**, not the data.
4. At most **one** `MayEnclose` volume draws per frame.
5. Extinction is applied **once**: `av` is baked, `M_V` is de-reddened, `dust` never re-dims V.
6. The census is its own file — a BY-SA file may not contain an NC source.
