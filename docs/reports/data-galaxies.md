# Lane `data-galaxies` — everything beyond the Milky Way

**Status: complete, green, not merged.** Branch `lane/data-galaxies`, worktree
`~/projects/CosmoSynth-wt/data-galaxies`. `./run-tests.sh` → **165 passed, 0 failed**
(floor 142, was 142 before this lane). `tools/gate-all.sh` → G0a PASS; G0b/c/d/e FAIL
**identically on master** and are owned by other lanes (§7).

The app stopped at 130 light years. It now reaches **1.6 gigaparsecs**, in four shipped
files and four engine-free Core types, with every unmeasured field carrying a flag that says
so.

---

## 1. What shipped

`unity/Assets/StreamingAssets/CosmoSynth/`

| File | Magic | Rows | Bytes | Covers |
|---|---|---|---|---|
| `lv/localvolume.cslv` | `CSLV` | **930** | **68,600** | 0.008 – 12,764 kpc |
| `lss/sdss.csrs` | `CSRS` | **49,680** | **1,692,488** | 13 – 420 Mpc, oriented |
| `lss/desi.cspt` | `CSPT` | **98,846** | **2,375,880** | 430 – 1,600 Mpc |
| `lss/voids.cspt` | `CSPT` | **805** | **18,452** | interior voids to 483 Mpc |
| `lss/structures.json` | — | **27 places** | **16,468** | named clusters, basins, voids |
| 7 × `.meta` | — | — | 1,119 | |
| | | | **4,172,663 raw** | |

**APK cost: 3,541,737 bytes** (measured, `zip -9` over both directories — that is what the
APK's own deflate will get). 46 MB today → about **49.4 MB** against a 150 MB ceiling.

### Row provenance

**`localvolume.cslv` = 911 LVDB dwarfs + 18 giants + 1 Milky Way row.**

- 911 dwarfs from LVDB v1.1.1 `dwarf_all.csv` (CC0), every one of them, nothing dropped.
- 31 heroes in `heroes-derived/heroes.json`, minus **9 already in LVDB with a *measured*
  distance** (NGC 55, NGC 300, NGC 3109, NGC 4449, NGC 7793, NGC 1313, NGC 6503, LMC, SMC —
  the LVDB row wins, because its distance is measured and the hero row's is derived), minus
  **4 with no distance at all** (NGC 4945, Maffei 1, Maffei 2, Circinus: no LVDB host group,
  so the satellite-median trick has nothing to work with). Those four are **not shipped**
  rather than placed at a guessed distance — see §5.
- 1 Milky Way row at the **galactic centre** (RA 266.41684, Dec −29.00781, R₀ = 8.15 kpc),
  flagged `IsHome` and never to be drawn as a point.

**Shapes, which is the whole point of the file:**

| | measured | unknown and flagged |
|---|---|---|
| ellipticity | **394** | **536** |
| position angle | **252** | **678** |

Of the measured shapes, 8 position angles and 5 ellipticities came from a 2MASS XSC
counterpart rather than from LVDB (§4).

---

## 2. Core

| File | What it is |
|---|---|
| `Core/Data/CsData.cs` | The container reader for design B §1.1. **Shared spine — see §8.** |
| `Core/Scale/Cosmology.cs` | The one cosmology. H₀ = 67.66, Ωm = 0.3150, comoving distance and its inverse. |
| `Core/Deep/GalaxyOrientation.cs` | Sky basis, major axis, Hubble inclination, disc normal. |
| `Core/Deep/GalaxySet.cs` | Reads `CSLV`, `CSRS` and `CSPT` behind one API. |

`tools/cosmodata/csdata.py` is the writing half, with `--selftest` writing the committed
cross-language fixture `tests/CosmoSynth.Core.Tests/fixtures/csdata_fixture.csd` (1,316 B).

Importers: `tools/cosmodata/build_localvolume.py`, `tools/cosmodata/build_lss.py`,
`tools/cosmodata/authored/overrides.json`. Both importers are **stdlib only** — including a
17-line `.npy` reader, so the numpy exception the contract allows for `build_lss.py` was not
needed and is not taken.

---

## 3. The cosmology, stated once

```
flat ΛCDM, H0 = 67.66 km/s/Mpc (h = 0.6766), Omega_m = 0.3150
Planck 2018 TT,TE,EE+lowE+lensing+BAO
```

It is in `Cosmology.cs`, in the `cosmology` block of every `lss/*` file's header, and in
`structures.json`.

**This is a deviation from design B §1.7, which specifies H₀ = 74.6.** That figure was
Cosmicflows-4's zero point, and CF4 is blocked outright by
`docs/DATA-LICENCE-AMENDMENT.md` §3, so its calibration went with it. Planck 2018 is what the
staged VAST void catalogue already assumes (its own coordinates invert to it), what the
staged DESI and SDSS notes prescribe, and what the CMB layer is based on. The two differ by
about 10%: mixing them puts one cluster in two places.

Comoving distance is Simpson over [0, z] with **512 intervals, in both languages**. That is
a contract, not a taste: `build_lss.py` places every shipped galaxy with it, so changing the
count on one side alone stops the positions round-tripping. It agrees with a 400,000-step
midpoint rule to 2 × 10⁻¹¹ Mpc at z = 0.5.

---

## 4. Subsampling, and the hash that had to be fixed

| Layer | From | Kept | Rule |
|---|---|---|---|
| SDSS | 492,189 → 413k in shell | 49,680 | uniform deterministic draw |
| DESI | 300,043 | 98,846 | draw **weighted by the LSS `WEIGHT`** |

Design B specifies the draw as `FNV-1a-64(key) / 2^64`. **Measured on this data it is not
uniform enough**: over sequential row keys it kept 51,021 rows where 50,000 were asked for —
a 2% excess, about 5σ on a binomial whose σ is 210. FNV's avalanche is weak in the high
bits, which is the end of the word this draw reads. The fix is murmur3's `fmix64` finalizer
on the FNV output: three lines, still deterministic, still reproducible, and the count comes
back inside noise. Recorded as a deviation from B.

DESI's weighted draw clamps p at 1 for high-weight rows. `min` is concave, so the count
lands **at or just below** the target and never above it — which is why that assertion is
`0.97·keep ≤ n ≤ keep` and not `±2%`.

**The two survey layers tile the volume; they do not overlap.** SDSS is cut at z < 0.10 and
the DESI volume-limited sample starts at z ≥ 0.10, so **no object can be in both files**.
That is the de-duplication the staged notes warn about, done by construction rather than by
a crossmatch. The price: the boundary at 430 Mpc is a change of catalogue, and the app should
say so rather than implying one continuous survey.

**The 2MASS XSC shape fill for dwarfs**: 92 counterparts within 30″ (the count
`data-staging/lvdb/NOTES.md` predicts, asserted exactly). Of those, **43 rejected by the
two-band fit guard** and **6 by the angular-size guard**, leaving 8 position angles and 5
ellipticities actually filled. The notes say 74 PAs and 64 ellipticities *could* be filled;
that is the count before any guard. Keeping the strict guard is deliberate — an unguarded
2MASS shape is how NGC 3109, an edge-on irregular, becomes perfectly face on. Relaxing
`r_k20fe ≥ 30″` (a threshold set for giant galaxies, not dwarfs) is the knob that would fill
more, and it is the one thing in this lane I would revisit with a way to validate the result.

---

## 5. Laniakea: what we can and cannot ship

**The basin boundary is not shipped, and nothing stands in for it.**

The watershed that *defines* Laniakea is the Cosmicflows-4 128³ `BoA` grid. CF4 is blocked at
every route (`DATA-LICENCE-AMENDMENT.md` §3). So:

- the 12 Dupuy & Courtois 2023 Table A.1 **attractor and repeller positions** ship as places
  in `structures.json`, placed in ICRF from their published RA/Dec and V_CMB — no
  supergalactic conversion, therefore no dependence on the galactic rotation matrix;
- Laniakea's entry carries the **published volume 1.9 × 10⁶ (Mpc/h)³**, provenance
  **Inferred**, with the paper cited and the transcription chain named;
- the entry's own `description` says, in the shipped file, that no boundary is shipped and
  why.

**The task's "Laniakea about 1.95e6 (Mpc/h)³" cannot be a test here.** That number is a
*cell count* in the CF4 grid (4,079 cells) — it is a measurement *of the blocked data*. The
test that ships instead asserts the published 1.9 is present, is labelled Inferred, and that
no `boundary`/`watershed` key exists on the entry. That is the honest version of the check.

A basin drawn without its grid would be artwork presented as a measurement, which is the one
thing this project's contract forbids everywhere.

---

## 6. Tests: what each proves, and what it does not

`CosmologyTests` (7) and `GalaxyTests` (16). 23 new tests; suite 142 → 165.

| Test | Proves | Does **not** prove |
|---|---|---|
| `ComovingDistanceKnownAnswers` | Simpson-512 reproduces an **independent 400,000-step midpoint rule** at 9 redshifts to 1e-6 Mpc | that Planck 2018 is the right cosmology — only that this one is implemented correctly |
| `LowRedshiftLimitIsTheHubbleLaw` | D_C → cz/H₀ as z → 0, *and* the fractional shortfall is linear in z at the rate Ωm sets. A wrong Ωm can hide in H₀ in the first check and cannot survive the second | anything about z > 0.01 |
| `RedshiftRoundTrips` | the inverse is exact to 1e-12 relative over 0.003 ≤ z ≤ 3.5 | — |
| `DesiRoundTrips` | **every 7th shipped DESI position, run back through the inverse, returns the catalogue's own redshift to < 1e-6.** The bound is set by float32 storage (≈5e-8 in z at z = 0.4), not by taste. This is the end-to-end check that the importer and Core agree about the cosmology | that the positions are *right* — a redshift-space position is not a location |
| `InclinationFormula` | the Hubble 1926 formula at 5 probe values to 1e-5°, computed independently in Python; the q₀ knob changes the answer; q ≤ q₀ is edge on; NaN in gives NaN out | that q₀ = 0.2 is true of any real galaxy |
| `PositionAngleGeometry` | PA 0 ∥ north and PA 90 ∥ east to 1e-12 at four sky positions, and the basis is orthonormal and on-sky | the sign convention matches any particular renderer's handedness |
| `M31` | shipped distance 751.6 kpc is consistent with the literature 0.77 Mpc **within the catalogue's own published MAD**; the MAD is itself bounded (< 40 kpc) so it cannot silently widen; the kinematic override (i = 77°, PA = 38°) is present and flagged; the photometric value 64.06° is *not* what ships | that M31 is at 0.77 Mpc. **We cannot claim that** — see §9 |
| `MagellanicClouds` | LMC PA is folded 227.24 → 47.24; the SMC's shape is NaN both ways, `ShapeIsMeasured` is false, and `DiscNormal` returns null | — |
| `LocalGroupCount` | **127 galaxies within 1 Mpc, 126 of them external to the Milky Way**; the MW row is flagged and sits at R₀ | that 1 Mpc is the Local Group's boundary; it is a convention |
| `NoPlaceholderIsEverMeasured` | for all 930 rows: flagged ⇒ NaN ⇒ `ShapeIsMeasured` false ⇒ provenance Inferred ⇒ no disc normal offered. And the counts are exact (536 / 394 / 678), so nothing can quietly fill them | that the 394 measured values are *correct*, only that they are measured |
| `RejectedShapeFits` | a 2MASS fit rejected by the two-band guard ships no ellipticity | — |
| `Voids` | 805 of 1,163, the 358 boundary voids excluded; median rEff and maximum radius both land where the Mpc/h → Mpc conversion puts them and **not** where a missed conversion would (a factor 1.48 apart) | that VoidFinder's voids are the voids |
| `SdssShell` | > 90% have a usable shape; max z < DESI's floor, so the layers cannot double-draw; round-trip; z < 0.01 rows are flagged | — |
| `NamedStructures` | every place carries a reference; 9 basins; Laniakea's volume is 1.9 and Inferred; no boundary key; Coma carries the independent SDSS peak | that the labels are right — each carries its own separation evidence instead |
| `ContainerFixtureRoundTrips` / `ContainerRefusals` | the Python writer and the C# reader agree on every type, and the reader refuses wrong magic, too-new version, short file, length mismatch, dirty reserved field, missing column, wrong-type read | — |

**What none of them prove:** nothing here has been rendered. No Unity editor was opened, by
instruction. Every claim is about bytes and arithmetic.

---

## 7. Gates

`tools/gate-all.sh` on this branch: **G0a PASS** (165, floor 142). G0b, G0c, G0d, G0e FAIL —
and fail **identically on master** (verified by running the same four gates in
`~/projects/CosmoSynth`; G0c is in fact one failure *better* here, 15 vs 16). They are:
shader `RenderPipeline` tags, the URP settings migration, `DepthBands.cs`'s second camera,
and two pre-existing filename/class mismatches (`Provenance.cs`, `Json.cs`). All belong to
`lane/foundation-code` and `lane/foundation-shaders`.

**`tools/gates/known-red.txt` was not touched.** Nothing in this lane failed because of the
`StandardFrames` ICRF→galactic sign bug, because **nothing in this lane uses that rotation**:
every input is ICRF-native (LVDB RA/Dec, LGA RA/Dec, DESI/SDSS RA/Dec, the void catalogue's
equatorial cartesian, the attractors' published RA/Dec), and the one place a supergalactic
conversion was tempting — the Dupuy & Courtois SG coordinates — was avoided by using the same
table's RA/Dec and V_CMB instead. That is a deliberate design choice, not luck.

---

## 8. `CsData` is a shared spine, and this lane wrote it first

`Core/Data/CsData.cs` and `tools/cosmodata/csdata.py` are design B §1.1/§1.3 and belong to
**every** data lane, not to this one. No branch had them when this lane started (checked
across `master`, `foundation-code`, `tooling`, `deep-sky`, `parity`). If the stars, dust or
CMB lanes wrote their own, **keep one and delete the others** — the committed fixture
`csdata_fixture.csd` decides which, because whichever reader parses it is correct.

It reuses the existing `ProvenanceClass` / `FieldProvenance` from `Core/Catalog/Provenance.cs`
rather than defining its own; the first version of this lane's file duplicated them and the
compile caught it. **No existing file was modified by this lane** — `git status` on the branch
is 18 additions and zero changes — so it should merge without a conflict.

One consequence of that reuse: `FieldProvenance` has no `Note` field, so the human-readable
`note` on each `provenanceBits` entry ("not measured; the source leaves it blank. Draw the
galaxy round and faint") does not come back through `ProvenanceFor`. The text is in the
shipped meta block and reachable as `set.Data.Meta["provenanceBits"]`; if a provenance panel
wants it, adding `public string Note` to `FieldProvenance` and one line to its `FromJson` is
the whole job.

---

## 9. Every deviation from the contract, with its reason

1. **H₀ = 67.66, not design B's 74.6.** B's figure was CF4's zero point; CF4 is blocked. §3.
2. **The subsample hash gains a `fmix64` finalizer** over B's plain FNV-1a-64. B's draw is
   measurably 5σ biased on this data. §4.
3. **No `sizeKind` column.** B declares one (0 half-light, 1 Holmberg), but the Holmberg
   radius came from UNGC, which is blocked; every size in the file is now a half-light radius.
   A column with one value forever is a constant, so it lives in the meta block as
   `"sizeKind": "half-light radius"` instead. The 2MASS K20 isophotal radius is in
   `heroes.json` if the runtime wants an outer extent — one column, say so and it ships.
4. **`ttype` is 99 on every row.** The LGA publishes a Hubble *type string*
   (`SAB(s)c;Sbrst`), not a de Vaucouleurs number, and mapping one to the other is an
   inference this lane did not want to make silently. **This matters to the renderer:**
   `GalaxyOrientation.IsDiscLike(tType)` is therefore false everywhere, so a caller that gates
   inclination on morphology will currently gate *everything* off. The string is in
   `heroes.json`; mapping it is a small, separate, citable job.
5. **Four heroes are not shipped** (NGC 4945, Maffei 1, Maffei 2, Circinus). No distance
   exists for them in any unblocked source. A position needs a distance; guessing one is
   exactly the class of error `CLAUDE.md` records under fabricated masses.
6. **The M31 distance is 751.6 kpc, Inferred, not 0.77 Mpc Measured.** Every commercially
   clean catalogue of measured distances to nearby giants is blocked, so the shipped value is
   the median of the 39 LVDB satellites that name M31 as host (MAD 35.4 kpc). The task's
   0.77 Mpc is the value the derived one is tested to be *consistent with*; it is not a number
   this lane may claim. Same for every other giant.
7. **The two hero overrides are class Inferred, not Measured.** `authored/overrides.json`
   gives M31 i = 77°/PA = 38° and M33 i = 55°/PA = 22.5°, citing Corbelli+2010 and
   Corbelli+2014 — but the chain is *paper → design B §2.11 rule 4 → this file*, and this lane
   did not read the papers. A number transcribed from a document is Inferred however good the
   underlying measurement. Anyone who reads the papers may promote them and say so in the file.
8. **B's test "M31 derived without the override gives 72°–80°" is dropped.** It assumed UNGC's
   photometric q = 0.30–0.34. The unblocked source (2MASS LGA) gives q = 0.473, hence 64.06°.
   The test that ships asserts the 64.06° value and that it is *not* what ships.
9. **`refIndex`/`refs` string table** added to the `CSLV` column set (B's meta carries a
   per-row `refs[]`); same information, 2 bytes per row instead of a string per row.
10. **No CMB-frame redshift correction.** The dipole is 0.1% at z = 0.1 but 40% of the
    redshift at the z = 0.003 floor. The apex direction is normally published in galactic
    coordinates, and this lane will not route through the known-bad rotation to get it. Rows
    below z = 0.01 carry a `NearbyPeculiarVelocity` bit instead. §10.
11. **A discrepancy in the staged data, reported not propagated:**
    `data-staging/named-structures/structures.csv`'s header comment says `Om=0.3111` while its
    sibling `sdss_density_peaks.csv` and the family NOTES both say `Om=0.3150`. This lane
    recomputes every distance from RA/Dec/z itself, so nothing inherits the discrepancy, but
    somebody should fix that comment.

---

## 10. What the runtime lane must know

1. **Units differ between files.** `CSLV` positions are **kiloparsecs**; `CSRS`/`CSPT` are
   **megaparsecs**. `GalaxySet.PositionUnit` and `PositionScaleToMpc` say which. Getting this
   wrong is a factor of 1000 that looks like a working scene.
2. **NaN is the signal.** `ell`, `pa` and `incl` are NaN wherever nothing was measured, and
   536 of 930 Local Volume rows have no ellipticity. Do not substitute a default — draw the
   galaxy round and faint, which is what the flags are for. `GalaxySet.DiscNormal(i)` returns
   **null** rather than a made-up orientation, and `ShapeIsMeasured(i)` is the question to ask.
3. **The Milky Way row is not drawable.** `IsHome` is set, its shape and magnitude are NaN,
   and its position is the *galactic centre* at 8.15 kpc, not the Sun. Its disc belongs to
   `GalaxyModel` (lane `milkyway`).
4. **Redshift space is not real space.** Both survey layers are redshift-space positions from
   heliocentric redshifts. Clusters are radially smeared into fingers of God up to ~10 Mpc.
   The `space` and `redshiftFrame` keys are in the meta; label the layer.
5. **The footprints are not the sky.** DESI DR1 is two disconnected caps (~14,000 deg²); SDSS
   is the north cap plus three southern stripes (~9,300 deg²) with a real empty band across
   RA 60–120 where the galactic plane crosses it. If the app fades the sample at the edge
   without a boundary cue, viewers read the edge as a wall. The `footprint` key in each meta
   says this in the file.
6. **SDSS ends where DESI begins, at z = 0.10 / 430 Mpc.** Draw both; they cannot double up.
7. **CC BY obligations are live.** DESI and VAST are CC BY 4.0 and both were modified
   (subsampled, unit-converted), so the attribution *and* a statement of change must reach a
   surface the user can read. `CsData.Attribution()` returns the exact strings, one per line.
   `DATA-LICENCE-AMENDMENT.md` §10 records that **the app has no credits surface at all** —
   that is a blocker for shipping this data commercially, and it is not this lane's to build.
8. **`IsDiscLike` is false everywhere** until someone maps the Hubble type strings. §9.4.
9. Voids are `rEff` spheres — the radius of an equal-volume sphere, not an outline. The
   triaxial axes exist in the staged ZOBOV files if a close-up ever needs a real shape.

---

## 11. Parity rows assigned to this lane

| Id | State after this lane |
|---|---|
| **MWA-17** — 911 Local Volume galaxies at measured size, ellipticity, PA | **Data delivered** (`lv/localvolume.cslv`, 930 rows, 394 measured ellipticities, 252 PAs, half-light sizes in kpc) + `GalaxySet`/`GalaxyOrientation`. The oriented-sprite *renderer* is a Unity-side job and is deferred to the runtime phase by the lane protocol (Core never references UnityEngine). |
| **MWA-17a** — GAL GLOW slider 0.5–30 | **Deferred**: a LayerParam and a UI chip. No LayerParam system exists yet (lane `ui`). Nothing in the data blocks it. |
| **MWA-17b** — fragment profile `0.62e^-9rr + 0.38e^-1.7rr` | **Deferred**: a shader, lane `p0-shaders`. |
| **MWA-17c** — colour by luminosity `t = (−M_V − 8)/12`; defaults drawn round and faint | **Data half delivered**: `absMag` + `band` ship per row, and the "defaults" half is done properly — 536 rows have no ellipticity and say so, where the atlas had 542 defaults drawn as data. The colour ramp itself is a renderer. |
| **MWA-18b** — 13 Local Group landmarks | **Delivered** as the `LandmarkLocalGroup` flag; the importer asserts all 13 names resolved, and `counts.landmarksLg == 13` is tested. |
| **PLUS-06** — large-scale structure | **Delivered, with substitutions forced by the licence bar**: DESI DR1 + SDSS DR18 + VAST voids + our own named structures, in place of B's 2MRS / CF4 groups / CF4 basins / CF4 δ, all four of which are blocked. Gained: reach to 1.6 Gpc. **Lost and explicitly deferred**: all-sky coverage (2MRS was 91%; ours is ~23,000 deg² in two footprints), every measured redshift-independent distance, and the CF4 density grid. A density field *can* be rebuilt as our own derived product by cloud-in-cell'ing the DESI positions — `data-staging/desi-dr1-lss/NOTES.md` §3 describes it — and that is the natural next lane, not a blocked one. |
| **PLUS-07** — Reid spiral arms drawn over their fitted range with the masers beside them | **Deferred, and one half of it is blocked.** The 199 maser parallaxes are the Reid+2019 VizieR table, which `DATA-LICENCE-AMENDMENT.md` §2 row 11 blocks; without them the row's whole point ("you see the model and the measurements that made it, together") is gone, and arms alone are the artwork the atlas correctly refused to draw. The arm *model* is facts and is implementable from published parameters, but it lives in `Core/Deep/GalaxyModel.cs` inside the Milky Way, which is lane `milkyway`'s — this lane starts at the Local Group. Recommend the parity row be reassigned to `milkyway` and its maser half marked `dropped` with the licence reason. |

---

## 12. Reproducing

```sh
cd ~/projects/CosmoSynth-wt/data-galaxies
python3 tools/cosmodata/csdata.py --selftest        # the cross-language fixture
python3 tools/cosmodata/build_localvolume.py        # ~6 s, 17 assertions
python3 tools/cosmodata/build_lss.py                # ~26 s, 20 assertions
./run-tests.sh                                      # 165 passed
```

Both importers read only `data-staging/` (resolved through git's common dir, since it is
gitignored and lives in the main checkout, not in a worktree), touch no network, write to a
temporary name, verify by reading back through the shipping reader, and `os.replace` only on
success — deleting both the temporary file and any stale target on any failure. Every
assertion they ran, with the value it got, is recorded in the shipped file's own meta block
under `assertions`, so a reader can see what was checked without rerunning anything.
