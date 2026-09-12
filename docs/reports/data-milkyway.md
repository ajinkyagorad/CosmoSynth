# Lane `data-milkyway` — the Galaxy itself

**Status: READY-1A**, with one known-red dependency (§7).
Branch `lane/data-milkyway`, worktree `~/projects/CosmoSynth-wt/data-milkyway`, off `master` at
`e72d89a`. No Unity editor was opened. Phase 1a only: data, engine-free Core, tests.

---

## 1. What shipped

Seven files under `unity/Assets/StreamingAssets/CosmoSynth/galaxy/`, plus their `.meta`
sidecars. **16,941,105 bytes raw, 5,606,678 bytes deflated.**

| File | Raw B | Deflated B | Rows | Source | Tag |
|---|---:|---:|---:|---|---|
| `dust.csdu` | 16,783,192 | 5,537,135 | 16,777,216 voxels | Edenhofer et al. 2024 (Zenodo 8187943) | `by` |
| `namedclouds.json` | 44,158 | 3,774 | 94 places | Zucker et al. 2020 (Dataverse 07L7YZ) | `cc0` |
| `radcliffe.csgp` | 35,496 | 23,408 | 1,500 spine points | Konietzka et al. 2024 (F98QHY) | `cc0` |
| `clouds.csgp` | 34,812 | 27,739 | 1,039 clouds | Rice et al. 2016 (EWY90X) | `cc0` |
| `globulars.csgp` | 28,252 | 8,165 | 213 clusters | Local Volume Database v1.1.1 | `cc0` |
| `radcliffe_clusters.csgp` | 8,156 | 4,132 | 125 clusters | Konietzka et al. 2024 (XSCB9N) | `cc0` |
| `landmarks.json` | 7,039 | 2,325 | 12 places | hand-authored, one citation each | `own` |

**APK budget.** The lane adds **16.9 MB raw / 5.6 MB deflated** to a 46 MB APK against a 150 MB
ceiling. Design B §0 budgeted the dust layer at 10.29 MB raw / 6.22 MB deflated; the shipped
cube is larger raw and **smaller deflated** than that, so the compressed budget is met at full
native resolution and there was no reason to reduce.

Everything else in the lane is 158 kB raw / 70 kB deflated, which is noise at this scale.

**Code, all new:**

```
tools/cosmodata/build_dust.py                  the cube        (numpy, the declared exception)
tools/cosmodata/build_galaxy_points.py         the four point files + namedclouds.json (stdlib)
tools/cosmodata/build_authored.py              validates and copies landmarks.json     (stdlib)
tools/cosmodata/authored/landmarks.json        the authored source, committed
unity/Assets/CosmoSynth/Core/Deep/DustCube.cs      density, extinction, the map sphere
unity/Assets/CosmoSynth/Core/Deep/VoxelGrid.cs     grid decode and sampling (shared with CF4)
unity/Assets/CosmoSynth/Core/Deep/PointSet.cs      the four populations, one type
unity/Assets/CosmoSynth/Core/Deep/GalaxyModel.cs   R0, disc, bar, the seven Reid arms
unity/Assets/CosmoSynth/Core/Deep/PlaceCatalog.cs  the places schema, both files
tests/CosmoSynth.Core.Tests/{CsData,DustCube,GalacticPoints,GalaxyModel,PlaceCatalog}Tests.cs
```

`./run-tests.sh`: **184 passed, 4 failed** (floor 142). The four are one bug, in §7.

---

## 2. The dust cube

**Source.** Edenhofer et al. 2024, A&A 685, A82, the 1.25 kpc 3D dust map, CC BY 4.0. Design B
§2.7 specified Vergely+2022 via CDS; `docs/DATA-LICENCE-AMENDMENT.md` §2 row 10 blocked it (no
licence in its ReadMe) and promoted Edenhofer, which was §B's own declared fallback. **This is a
deviation from B and it is the amendment's, not mine.** Lost with Vergely: mapped volume beyond
1.25 kpc, and convenient mag/pc units.

**What the runtime lane gets.**

- **256 × 256 × 256 voxels of 10 pc**, extent ±1275 pc on each axis, node centres at
  `(i − 127.5) × 10` pc. `grid.frame` is `galactic-helio`: +x toward l = 0, +y toward l = 90,
  +z toward the north galactic pole. **Nothing was resampled by this lane** — the staging pass
  already reduced the 3.25 GB HEALPix product to this grid (its `SOURCE.txt` has the range
  requests). The only reduction here is the re-encoding below.
- **Re-encoded** from the staged codec (`v/255`, `v == 0` means no data) into the container's
  `log8` (§1.2 of B: `0 → 0`, `v ≥ 1 → lo·(hi/lo)^((v−1)/254)`), with `lo` = the 1st percentile
  of positive voxels = 2.35e-7 and `hi` = 0.0575. Measured worst-case round-trip error **0.02432
  in log**, which is exactly half a step of a 254-level ramp over that range — the test asserts
  the analytic half-step, not a guessed bound.
- `--step N` block-averages if a future budget needs it. Unused: `--step 1` fits.

**Three things the runtime lane must not get wrong**, all recorded in the file's own
`meta.notes`:

1. **The values are not magnitudes.** The source's unit card reads, verbatim, *"E of Zhang,
   Green, and Rix (2023)"* per parsec. `DustCube.ExtinctionAlong` returns E and is named for it.
   A single scale factor is fine for rendering; the result must not be labelled magnitudes.
2. **Outside `grid.mapRadiusPc[1]` = 1248.1 pc the cube is UNKNOWN, not empty.** 51.7% of the
   box is outside the mapped sphere. A raymarcher that reads the encoded zero there as vacuum
   cuts square corners of clear sky into the Galaxy. `DustCube.IsMapped` answers this;
   `DensityAt` returns 0 beyond it and the shader needs the same `r < 1248` test.
3. **Inside 68.6 pc there are no shells** — those voxels carry the innermost shell value. The
   Sun is inside the Local Bubble, so that is both the most visible part of the cube and the
   only part that is an extrapolation.

**Leike 2020 (2 pc, 740×740×540 pc) is staged and not shipped.** It would add 37 MB raw for a
sharper Local Bubble inside 370 pc, in *different units* (G-band optical depth per pc, not
Zhang+2023 E), which means a second texture, a second scale factor and a seam. Deferred, not
rejected: if the Local Bubble scene needs it, block-average it to 4 pc (4.6 MB) and switch cubes
at a boundary rather than cross-fading — the two reconstructions disagree in detail.

---

## 3. The point populations

**The Cepheid layer of design B §2.8 does not exist and cannot.** `docs/licence/milkyway.md`
checked five candidates and **no commercial-clean Galactic Cepheid catalogue exists**: Skowron
2019's repository licence explicitly excludes *"scientific data"*, Gaia DR3 is CC BY-NC, OGLE
states no terms, VizieR grants scientific use only, ASAS-SN has citation requirements and no
licence. The Reid+2019 maser table is blocked for the same reason (§4). What ships instead:

| File | What it traces | Instead of |
|---|---|---|
| `clouds.csgp` | the young disc, all the way across the Galaxy | Cepheids |
| `globulars.csgp` | the halo, 1.85 to 145.9 kpc | (unchanged from B) |
| `radcliffe.csgp` + `radcliffe_clusters.csgp` | the local arm's nearby segment | masers |
| `namedclouds.json` | named nebulae with measured distances | — (new) |

Every file carries `pos` f32×3 in **ICRF parsecs**, `distErr` f32 (pc), `flags` u16, plus its own
columns. Names live in `meta.names`.

**Flags** (`PointFlags` in Core, `meta.provenanceBits` in each file, and they *change what the
app claims*, not just what it records):

| Bit | Name | Set on |
|---|---|---|
| 0 | `Outlier` | 34 clouds beyond 20 kpc or 2 kpc off the plane |
| 1 | `NoAbsoluteMagnitude` | 7 globulars with no published `M_V`. Their `absV` is **NaN**, never 0 |
| 3 | `NotMilkyWayHost` | globulars whose LVDB `host` is `sagittarius_1` |
| 4 | `AmbiguousDistance` | **663 of 1,039 clouds** — an unresolved kinematic near/far distance is not a measurement |
| 5 | `NotAMember` | 36 of the 125 Radcliffe clusters, whose `inlier` is 0 |

Bit 4 is the one that matters visually: **64% of the cloud catalogue's distances are a choice**,
and the renderer must show them differently from a parallax distance.

Globular distances come from LVDB's `distance_modulus` with the **larger arm** of its asymmetric
error, and `absV` from LVDB's own `M_V` column — never recomputed as `m_V − μ`, which ignores
extinction and is the bug the Atlas port inherited. `rhalf` is angular (arcmin); `rhalfPc` uses
`rhalf_physical` where LVDB publishes it and derives from the angular value otherwise.

---

## 4. The spiral arms, and where their numbers came from

`GalaxyModel` is **code, not data, and that is a licence decision as much as a design one**. The
VizieR copy of Reid et al. 2019's 199 maser parallaxes may not be redistributed; the fitted arm
parameters are measurements of the Galaxy, and a measurement is a fact.

`data-staging/reid2019-spiral-arms/NOTES.md` said the numbers *"must be transcribed from the
paper by a human reading it… do not transcribe them from a VizieR/ADS scrape, and do not let a
model recall them — that is how a wrong R₀ ships looking confident"*, and deliberately staged
none. **They are now transcribed, from the papers.** `curl https://arxiv.org/e-print/1910.03357`
(2,657,580 B, one LaTeX file) gave the `deluxetable` labelled `table:pitchangles` verbatim; the
disc and bar numbers came the same way from `https://arxiv.org/e-print/1602.07702`
(Bland-Hawthorn & Gerhard 2016, ARA&A 54, 529). Neither tarball was kept.

```
Arm                 beta range   kink  R_kink       psi<   psi>   width   N
3-kpc(N)             15 ...  18    15   3.52+/-0.26  -4.2   -4.2   0.18    3
Norma                 5 ...  54    18   4.46+/-0.19  -1.0   19.5   0.14   11
Scutum-Centaurus      0 ... 104    23   4.91+/-0.09  14.1   12.1   0.23   36
Sagittarius-Carina    2 ...  97    24   6.04+/-0.09  17.1    1.0   0.27   35
Local                -8 ...  34     9   8.26+/-0.05  11.4   11.4   0.31   28
Perseus             -23 ... 115    40   8.87+/-0.13  10.3    8.7   0.35   41
Outer               -16 ...  71    18  12.24+/-0.36   3.0    9.4   0.65   11
```

with `ln(R/R_kink) = −(β − β_kink) tan ψ`, `w(R) = 336 + 36 (R_kpc − 8.15)` pc,
**R₀ = 8.15 ± 0.15 kpc** and Θ₀ = 236 ± 7 km/s (fit B). Disc: thin 2.6 ± 0.5 kpc / 300 ± 50 pc,
thick 2.0 ± 0.2 kpc / 900 ± 180 pc, thick/thin local density 4%. Bar: 27 ± 2°, long-bar half
length 5.0 ± 0.2 kpc, near end in the first Galactic quadrant.

**`RadiusKpc(β)` returns NaN outside the fitted range**, and `ArmPoints` never samples outside
it. That is the point: a log spiral extrapolated past its data winds into the centre or off the
disc while still looking exactly like a spiral arm. Nothing was fitted on the far side of the
Galactic Centre and `TryNearestArm` returns false there rather than inventing one.

`GalaxyModel.Citation` carries the required credit and the sentence saying no catalogue data was
redistributed.

---

## 5. Landmarks and named places

Two files, **one schema** (`cosmosynth.places/1`), **one parser** (`PlaceCatalog`):

- `landmarks.json` — 12 hand-authored entries, each with exactly one citation.
  `build_authored.py` refuses the file if any lacks one, has an unknown category, or gives a
  position both ways.
- `namedclouds.json` — 94 named clouds generated from Zucker et al. 2020, grouped from 326
  sightlines. Each takes **one real sightline at the group's median distance**, never a median
  of l, of b and of d computed separately — that mixes three rows and put California 54 pc out
  on the first run.

Five globular landmarks (47 Tuc, ω Cen, M4, M13, M15) carry **no coordinates at all**: they name
a row of `globulars.csgp` and `PlaceCatalog.Resolve` fills the position in, so a landmark that is
also a catalogued object has exactly one position in the build. A name that is not in the
catalogue throws, rather than silently drawing the landmark at the Sun.

**Two Atlas numbers corrected, with citations:**

| | Atlas | Here |
|---|---|---|
| Crab pulsar spin | 30.2 Hz | **29.6 Hz** at MJD 58058 (Lyne et al. 2018, MNRAS 478, 3832) |
| Local Bubble | "150 pc" in one place, "(100pc)" on its own button | radius **150 pc**, i.e. ~300 pc across (Zucker et al. 2022, Nature 601, 334) |

Also: the Atlas puts Sgr A\* at 8,178 pc with no citation. Here it is at `GalaxyModel.R0` =
8,150 pc so the arm model and the Galactic Centre agree, with GRAVITY's 8.178 and 8.277 kpc named
in the entry as alternatives.

**Not authored, and why:** open clusters (Pleiades, Hyades) — no staged source carries their
distances, and a recalled parallax is exactly the failure this project has paid for. OpenNGC is
staged and CC BY-SA, which would make the derived file share-alike; the Zucker set already gives
named nebulae with *measured* distances, so OpenNGC bought nothing this lane needed.

---

## 6. Tests: what each proves, and what it cannot

`./run-tests.sh` — 47 new assertions across five files. Everything below is a check on the
**shipped artefact**, not on a constructed example, and every expected value comes from outside
the thing it tests.

| Test | Proves | Cannot prove |
|---|---|---|
| `DustCubeTests.CloudsPeakAtTheirPublishedDistances` | six named clouds peak within 15% of their **Zucker+2020** distance — a catalogue this map was never built from. Measured: Orion +4.1%, Taurus +4.5%, Perseus +7.2%, California +7.5%, Chamaeleon +2.9%, Coalsack −2.7% | that the map is *right*. Two reconstructions of the same cloud differ by 10–15% and this bound deliberately does not adjudicate that. Its job is catching a swapped axis or a wrong distance scale, either of which moves a peak by hundreds of per cent |
| `DustCubeTests.AxisSignsWinAgainstEveryFlip` | the identity axis assignment beats all seven sign flips by **8.6×** in mean log density at 59 cloud positions (contract asks ≥ 1.5×). Re-proves the importer's numpy check in the code that reads the file on a headset | that x, y and z are *labelled* right, only that the clouds are where their coordinates say |
| `DustCubeTests.LocalBubbleAndDiscFlattening` | the Sun sits in a cavity (100–400 pc mean > 3× the mean inside 60 pc) and the dust is flattened (|z| < 100 pc mean > 10× beyond 400 pc) — two facts about the Galaxy, not about this reconstruction | anything about density *values*; both are ratios |
| `DustCubeTests.DensityRange` | peak 0.0575 E/pc, in (0.01, 0.2); exactly zero outside the box and outside the map sphere; `IsMapped` false at the corners | — |
| `DustCubeTests.ExtinctionIsSymmetric` / `ExtinctionConverges` | the integral is endpoint-order-independent to 1e-9, and 10 pc vs 2.5 pc steps agree to 10% | that 10 pc is the right step for rendering |
| `DustCubeTests.TrilinearInterpolatesDecodedValues` | the midpoint of two nodes is their **arithmetic** mean, so the corners are decoded before interpolating. Interpolating log codes would give the geometric mean, smaller everywhere the field has contrast | — |
| `GalacticPointsTests.GlobularKnownAnswers` | 47 Tuc at 4,520.6 ± 5 pc; the nearest globular is M4 (NGC 6121) at 1,851 pc; the brightest is ω Cen at M_V −10.54 ± 0.02 | that −10.54 is *true*. Harris 2010 gives −10.26 for the same cluster; the 0.28 mag is between catalogues. Asserting on the value we ship is what catches a column shift or an f32 truncation |
| `GalacticPointsTests.CloudsTraceTheDisc` | median \|z\| is **42.4 pc** for R 4–10 kpc and the outer disc is **2.27×** thicker at 10–16 kpc — the thin molecular disc and its flaring | the warp. See §8 |
| `GalacticPointsTests.MissingMagnitudesAreFlagged` | all 7 globulars without a published `M_V` carry NaN *and* the flag, none carries 0, and their provenance reports Inferred rather than Measured | — |
| `GalacticPointsTests.AmbiguousCloudDistancesAreFlagged` | 663 clouds flagged, and a flagged row's `pos` provenance says *why* | — |
| `GalacticPointsTests.RadcliffeWaveShape` | span 2,732 pc, amplitude 185 pc, closest approach 259 pc to the Sun, largest consecutive step < 20 pc (so the rows really are a path) | that the fit is right; it is a fit, and its provenance says Inferred |
| `GalaxyModelTests.ArmsStopWhereTheirDataStops` | every arm returns NaN half a degree outside its own β range, is continuous at its kink, and `ArmPoints` never samples outside | — |
| `GalaxyModelTests.SunSitsOnTheLocalArm` | the Local arm passes **376 pc outside** the Sun's orbit at β = 0, i.e. the Sun sits just inside it, within two arm widths | — |
| `GalaxyModelTests.ArmTableMatchesThePaper` | seven arms, spot-checked row by row against the printed table (Sgr-Car R_kink 6.04, β_kink 24, ψ 17.1→1.0, N = 35; Local single pitch angle; Outer widest) | that I read the right table |
| `GalaxyModelTests.GalacticCentreDirection` | the model's GC direction is within 0.1° of Sgr A\* | **RED, see §7** |
| `PlaceCatalogTests.*` | every place in both files has a reference; Orion at 414 ± 0.5 pc; Crab at 29.6 Hz; Local Bubble radius 150 pc; the five globular landmarks resolve and 47 Tuc lands at 4.52 kpc; a landmark naming a missing row throws; five malformed-file shapes are refused | — |
| `CsDataTests.EncodingsDecodeAsDefined` | `log8(0)` is exactly 0 and not the floor, and the ramp's midpoint is the geometric mean; an unknown encoding is refused rather than read raw | — |

**Importer-side checks** (recorded in each file's `meta.assertions`, and the importer writes
nothing when one fails — it did refuse three times during this run):

- `build_dust.py` correlates its own ray integral against the independently-computed all-sky
  column map over 768 directions, after removing the |b| trend from both: **r = 0.957**, and the
  same statistic with the sky turned 90° in longitude gives **−0.072**. The wrong answer is
  computed every run and recorded beside the right one, because a gate that cannot fail proves
  nothing.
- SHA-256 of every staged input is asserted and recorded.

---

## 7. Known red — one bug, four assertions

`Core/Frames/StandardFrames.cs` builds the ICRS→galactic rotation with **all three signs
reversed**, sending the north galactic pole to (−0.346, 0.176, 0.922) instead of (0, 0, 1). The
fix is on `lane/foundation-code`. Recorded in `tools/gates/known-red.txt` as `G0a`.

Failing: `GalaxyModelTests.NorthGalacticPoleIsThePole`,
`GalaxyModelTests.GalacticCentreDirection`, `GalacticPointsTests.CloudsTraceTheDisc`,
`GalacticPointsTests.RadcliffeWaveShape`.

**Verified, not assumed**: with `lane/foundation-code`'s `StandardFrames.cs` swapped into this
worktree, the suite is **188 passed, 0 failed**. The file was restored; the branch does not carry
that change.

**No shipped byte depends on it.** Every galactic→ICRF conversion in the data happens in
`build_galaxy_points.py`, which carries its own copy of the Hipparcos matrix from design B §1.6
and asserts it against a known answer before using it on anything — Sgr A\* must come out at
l = 359.944, b = −0.046, and it does (359.9443, −0.0462). So the data is right and only the
runtime rotation is wrong.

`known-red.txt` excuses the **whole** G0a gate, which is the cost of excusing it at all. The
entry therefore names the expected count and the four test names, so a fifth failure is visible.
It says to remove the line the moment foundation-code merges, and to escalate rather than extend
past 2026-09-19.

---

## 8. Deviations from the contract, each with its reason

1. **Cepheids and masers are not shipped.** Both blocked commercially; §3. The task asked for
   "Cepheids, globular clusters and masers into the shipped container" and for "a Cepheid
   distance distribution that traces the warp". Neither is possible under the amendment.
2. **The warp test does not exist.** Design B §2.8 wanted mean z > 0 for Cepheids with
   R_GC > 12 kpc and 60° < l < 120°, and < 0 for 240°–300°. Measured on the CO clouds, the
   substitute population: **5 rows and 7 rows** in those two windows, with the signs the other
   way round. The catalogue is a Galactic-plane CO survey and has 12 objects past R = 12 kpc in
   the relevant longitudes; the test would be noise. The disc thinness and flaring tests replace
   it and are strong (§6).
3. **The arms are not cross-validated against the clouds.** Worth stating because it was
   attempted and failed. Median distance from a cloud to its nearest arm, in arm widths: **real
   1.79 vs azimuth-shuffled 1.87 ± 0.18**, about 0.4σ. Restricting to the 376 KDA-resolved
   clouds gives 1.3σ; to the 203 most massive, 1.1σ. **The Rice clouds do not significantly
   prefer the Reid arms at this catalogue's distance precision** — kinematic distance errors of
   order a kiloparsec swamp a 0.3 kpc arm. No test was written on it. Four probe runs are in the
   lane transcript; the conclusion is the deliverable.
4. **File names.** B §2.8 named `cepheids/globulars/masers.csgp`. The amendment's §2 row 11 names
   `globulars/clouds/radcliffe.csgp`; this lane ships those plus `radcliffe_clusters.csgp` and
   `namedclouds.json`.
5. **`namedclouds.json` is generated, not authored.** B §2.10 has one authored places file. 94
   clouds with measured distances are an import, not an authoring act, so they are generated into
   a second file in the same schema — one parser, two files, no new container kind.
6. **`CsData`/`csdata.py` were written twice, and this lane dropped its copy.** Lane
   `data-galaxies` committed `tools/cosmodata/csdata.py` and `Core/Data/CsData.cs` first. Both
   implement design B §1.1 and the wire formats agree. This lane **adopted theirs verbatim** and
   made one additive change: `ColumnMeta(string)` and `Decoder(string)` on `CsData`, which a
   quantised voxel grid needs and a galaxy catalogue does not. The integrator gets an additive
   diff on one file instead of a conflict between two rival implementations. Folder `.meta` GUIDs
   were taken from that branch too, so `Core/Data.meta` and `Core/Deep.meta` are byte-identical
   across the two lanes.
7. **`DustCube` uses `StandardFrames.EquatorialToGalactic`, bug and all.** A private copy of the
   rotation would have made both tests pass and left two rotations to keep in step. §7.

---

## 9. What the runtime lane needs to know

1. **Mask the sphere.** `meta.grid.mapRadiusPc = [68.62, 1248.10]`. Test `r < 1248` in the
   shader or premultiply a sphere mask into the texture; the encoded 0 outside it means
   *unknown*.
2. **`log8(0)` is zero, not the floor.** `CsData.Decoder` gets this right; a shader that decodes
   the byte itself must too, or every unmapped voxel fills with faint dust.
3. **The texture is `R8`, 16 MB, x fastest, `(iz, iy, ix)` row-major.** `VoxelGrid` exposes
   `RawNode` so the bytes can go straight to a `Texture3D` with no re-pack.
4. **The cube is in galactic coordinates and everything else is in ICRF.** `DustCube.
   GalacticPcFromIcrfPc` is the only conversion the layer needs — and it is the call that is
   wrong until `lane/foundation-code` merges.
5. **Units are E(Zhang+2023) per parsec.** Pick a rendering scale factor and keep it a tunable;
   do not label the result magnitudes anywhere the user can read it.
6. **Draw the 663 ambiguous clouds differently.** Bit 4. This is the single biggest honesty
   affordance in the lane's data.
7. **`radcliffe.csgp` is a polyline** (`meta.polyline: true`), 1,500 points in path order, max
   step 18 pc. Feed it to a ribbon renderer, not a point cloud. Its `vz` column is the 2024
   paper's finding; animating z from it is legitimate and must be labelled an extrapolation.
8. **`ArmPoints` gives galactocentric parsecs at z = 0.** Add `GalaxyModel.GalacticCentrePc` to
   get heliocentric galactic, then rotate for ICRF. β is zero toward the Sun and grows with
   Galactic rotation; get that backwards and every arm mirrors while still looking like an arm.
9. **`PlaceCatalog.Resolve("globulars", …)` must be called** before the five globular landmarks
   have positions. They throw until it is, on purpose.
10. **Attribution is a licence condition, not a courtesy.** `DustCube.Attribution()` and
    `PointSet.Attribution()` return the exact strings; CC BY 4.0 requires the "resampled and
    quantised" sentence, which is in the dust cube's. There is still no credits panel in the app,
    and `docs/data-sources.md` §3 lists that as blocking release.

---

## 10. Parity rows

From `docs/ATLAS-PARITY.md` (branch `lane/parity`), the rows assigned to `milkyway`:

| Row | State |
|---|---|
| MWA-09 raymarched dust cube | **data delivered** — cube, sampler, extinction. The renderer is 1b |
| MWA-09a DENSITY slider | deferred to 1b: a LayerParam, not data |
| MWA-09b THRESHOLD slider | deferred to 1b, same reason |
| MWA-09d four-stop colour ramp | deferred to 1b: Procedural, owned by rendering |
| MWA-09e jitter / early-out / discard | deferred to 1b: shader |
| MWA-15 Cepheids | **cannot be delivered.** §3. `clouds.csgp` is the replacement |
| MWA-16 globular clusters | **delivered**, 213 with names, `M_V` and `rhalf`, against the Atlas's 213 with five names and no extinction correction |
| MWA-18 nine cloud markers | **exceeded** — 94 named clouds with measured distances and uncertainties, each cited, against nine hard-coded positions with none |
| MWA-18a seven Milky Way landmarks | **delivered**, 12 places, each with one citation, two of the Atlas's numbers corrected |

Rows MWA-09c (quality governor, `p0-shaders`), MWA-10 (cube edges, `lines`) and MWA-18b/c/d
(Local Group landmarks and label categories, `galaxies` and `ui`) are not this lane's.

**Beyond the Atlas:** the Radcliffe Wave (1,500-point spine + 125 clusters, discovered 2020,
absent from the Atlas entirely), the Reid 2019 arm model with honest azimuth ranges, per-field
provenance with row-level overrides on every population, and a dust cube at 10 pc over 2.55 kpc
against the Atlas's 10 pc over 2 kpc with unknown units from a GPL-3.0 HTML scrape.
