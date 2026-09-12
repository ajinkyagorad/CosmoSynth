# Lane `data-stars` — the true 3-D star layer

**Branch** `lane/data-stars`. **Worktree** `~/projects/CosmoSynth-wt/data-stars`.
**Status:** delivered, with two rows explicitly deferred (§7) and one known-red dependency (§8).

The lane's job: give the Unity app the star layer the WebXR Milky Way Atlas has and the
current build does not — stars at **measured three-dimensional positions** instead of
directions painted on a shell, plus names and the 88 constellation figures.

Everything below that says "measured" was measured on triton on 2026-09-12 and the command
that measured it is named. Everything else says INFERRED.

---

## 1. What ships

Two source paths, one importer, selected by `COSMODATA_LICENCE` (design B §1.5).

### 1.1 Development default — `COSMODATA_LICENCE=sideload-nc` (shipped in the tree)

| File | Bytes | Rows / count |
|---|---|---|
| `unity/Assets/StreamingAssets/CosmoSynth/sky/stars.csstars` | **5,747,716** | **119,613 stars**, 10 columns, 48.1 B/row |
| `unity/Assets/StreamingAssets/CosmoSynth/sky/stars_named.json` | **79,965** | 548 proper names |
| `unity/Assets/StreamingAssets/CosmoSynth/sky/constellations.json` | **11,018** | 44 figures, 138 segments |
| `tests/CosmoSynth.Core.Tests/fixtures/csdata_fixture.csd` | **1,316** | 3 rows, 9 columns (cross-language contract, committed) |

**APK impact: +2,970,971 bytes** over the CSS1 file it replaces (2,870,728 → 5,747,716), plus
91 KB of JSON. About 46 MB today → **about 49 MB**, against the 150 MB ceiling. The growth is
the columns CSS1 never had: velocity (12 B), extinction (2 B), B−V (1 B), spectral class
(1 B), HIP (4 B), HD (4 B).

- **109,389 stars carry a measured distance**, from 1.296 pc to 990.1 pc, median 194.2 pc.
  10,224 carry an exact direction and an honest `DistanceUnknown`.
- 5,070 stars at V ≤ 6.0; 8,920 at V ≤ 6.5. Faintest row V 21.00, recorded in the file so the
  observed-sky limit slider can state its own ceiling.

### 1.2 Commercial-clean — `COSMODATA_LICENCE=commercial`

Built and verified this run to `/tmp/cs-clean`; **not committed**, because the development
build is what the tree ships today.

| File | Bytes | Rows / count |
|---|---|---|
| `sky/stars.csstars` | **443,088** | **9,096 stars**, 48.7 B/row |
| `sky/stars_named.json` | **52,332** | 338 IAU-approved names |
| `sky/constellations.json` | **10,979** | 44 figures, 137 segments |

Sources: Yale BSC5p via NASA HEASARC (`pd`) + IAU-CSN (`by`) + Stellarium modern (`by-sa`).
All staged, all cleared in `docs/licence/stars.md`.

### 1.3 What the clean path loses — the number that matters

**204 of 9,096 stars have a usable distance.** Not 9,096, not "most of the bright ones" — 204,
and none of them beyond 16.7 pc.

That is not a design choice, it is a measurement. `probe_plx.py` compared BSC5's ground-based
parallaxes against modern parallaxes for the 2,670 stars where both exist:

| BSC5 parallax | n | median error | p90 error | distance |
|---|---|---|---|---|
| ≥ 0.200″ | 10 | 2.1% | 4.0% | < 5 pc |
| 0.100–0.200″ | 49 | 7.2% | 19.3% | 5–10 pc |
| 0.060–0.100″ | 143 | 10.5% | 35.3% | 10–17 pc |
| 0.040–0.060″ | 350 | **22.1%** | 68.2% | 17–25 pc |
| 0.020–0.030″ | 609 | **38.0%** | 74.1% | 33–50 pc |
| < 0.010″ | 457 | **62.2%** | 374.0% | > 100 pc |

So `BSC5_MINIMUM_PARALLAX = 0.060″`. Everything fainter gets a measured direction and
`DistanceUnknown`.

**The importer found this by failing.** The first commercial run refused to write because
Canopus came out at 35.7 pc against a true 94.8 pc — BSC5's 0.028″ parallax. The fix was not
to widen the tolerance; it was to stop claiming a distance that the source cannot support.

**In one sentence: the commercial-clean sky is a direction shell with a 17-parsec bubble of
real depth in the middle of it.** The development sky is a 990-parsec volume with 109,389
measured distances. That is the whole cost of not having an ESA licence, and
`docs/licence/stars.md` §4 Option A (one email to `data.licences@esa.int`) removes it.

---

## 2. The container (design B §1.1–1.3)

`tools/cosmodata/csdata.py` + `unity/Assets/CosmoSynth/Core/Data/CsData.cs`. Column store, one
magic per kind, 8-byte-aligned columns, JSON meta.

The writer writes to `.tmp`, `fsync`s, **reads it back through its own reader**, checks the
magic, the exact length and the first and last element of every column, and only then
`os.replace`s. On any failure it deletes both the temporary file and the stale target. That is
the `build_spacecraft.py` lesson applied once rather than per script.

`python3 tools/cosmodata/csdata.py --selftest` writes the committed fixture. `CsDataTests`
parses that fixture field by field — including every integer type at both ends of its range —
so the two implementations of one byte layout cannot drift apart silently.

### Star columns (`CSS2`, 48 B/row)

| Column | Type | Unit | Content |
|---|---|---|---|
| `pos` | f32×3 | pc | ICRF, epoch J2000.0 |
| `vel` | f32×3 | km/s | ICRF space velocity |
| `vmag` | f32 | mag | Johnson V |
| `teff` | f32 | K | Ballesteros 2012 from B−V, else spectral class |
| `av` | u16 | mag | **extinction, 1/4096 mag** — see §4 |
| `bv` | u8 | mag | B−V, linear-encoded over −0.5…3.5; 0 means missing |
| `spec` | u8 | — | class×10 + subclass over `OBAFGKMLTYCSW`; 255 unknown |
| `hip` | u32 | — | Hipparcos number, 0 if none |
| `hd` | u32 | — | Henry Draper number, 0 if none |
| `flags` | u32 | — | bits below |

Flags keep B §2.1's bits 0–10 and add: **11 `ExtinctionUnknown`**, **12 `VelocityUnknown`**,
**13 `FromCss1Migration`**. Bits 8–10 remain the distance-source code, with **7 = ground-based
trigonometric** added for BSC5.

---

## 3. The constellation bridge — the hard part, measured

Every Stellarium figure vertex is a **HIP number**. The Hipparcos catalogue is ESA
CC BY-NC 3.0 IGO and is not redistributable, so HIP → position cannot be shipped and the
figures have to reach the catalogue through somebody else's identifier table.

**The importer treats bridge routes exactly like data sources: each declares a licence tag, and
the profile filters them.** So switching to `commercial` narrows the bridge instead of
silently shipping a broken figure set.

### 3.1 The resolved fraction, both paths

| | development | commercial |
|---|---|---|
| distinct HIP in the 88 figures | 710 | 710 |
| **HIP resolved** | **245 (34.5%)** | **244 (34.4%)** |
| **segments resolved** | **138 / 695 (19.9%)** | **137 / 695 (19.7%)** |
| figures with at least one segment | 44 / 88 | 44 / 88 |

This independently reproduces the 34.4% the licence audit measured, by a different route.

### 3.2 Two routes were tried and rejected, with the evidence

Both are recorded here so nobody re-tries them:

1. **Replaying the owner's own Atlas.** `refs/webxr/nebula-gallery/data/constellations.json`
   holds 679 segments with HIP already resolved to 3-D positions. A greedy alignment against
   the staged Stellarium file recovered 665 HIP positions — and then **disagreed with the
   independent IAU-CSN bridge on 103 stars against 94 agreements**, i.e. it was noise.
   An exact subsequence alignment (695 pairs, 679 segs, 16 skips, DFS with memoisation)
   then proved **no valid alignment exists at all**: the Atlas was built against a different
   Stellarium revision than the one staged. Dead, twice over, and the cross-check is what
   caught it.
2. **Wikidata SPARQL (CC0).** Implemented as `tools/cosmodata/fetch_hip_bridge.py`. Run this
   session: WDQS is in an active outage and answers
   `429 Aggressively rate-limiting to 1 req / min — this rule was created during active wdqs
   outage`. Zero pairs returned. The script now honours `Retry-After`; it is the
   one action that would take the figures from 20% to (probably) near-complete.

### 3.3 What did work, and the cross-check that makes it trustworthy

| Stage | Tag | dev | commercial |
|---|---|---|---|
| `catalog-hip` (the file's own HIP column) | `by-sa` | +0 | +338 |
| `iau-csn-hd` (HIP → HD → row) | `by` | +0 | +0 |
| **`iau-csn-position`** (HIP → IAU's own J2000 position → nearest row) | `by` | **+404** | +0 |
| **`stellarium-name`** (HIP → traditional name → row) | `by-sa` | **+18** | +0 |
| `wikidata-hip-hd` (cached) | `cc0` | +0 | +0 |

The position route accepts a match only within 30″ **and** five times closer than the
runner-up, so a close pair refuses rather than guesses. Residual median **0.01″**.

**The two routes are independent — a name table and a position table with different
publishers — so the importer compares them and refuses to write on a disagreement.** On the
development build: **381 compared, 380 agree, 1 same-system, 0 conflicts.**

That cross-check earned its place twice on its first two runs:

- **HIP 83608 (μ Draconis)** — the two routes landed on the two components of one double star,
  2″ apart. Not a contradiction about *which star*, so the check now separates
  `sameSystem` (< 10″, allowed and counted) from `conflict` (fatal).
- **HIP 78820 (β¹ Scorpii, Acrab)** — Stellarium lists "Graffias" as an alias of Acrab while
  the catalogue attaches that name to **ξ Scorpii, a different star**. A whole-sky error, from
  a name. The name route now requires a name to be unique in **both** directions — one HIP per
  name *and* one row per HIP — and refuses 46 shared names (Algenib names two stars, Marfak
  four) plus every HIP whose aliases disagree.

Without the cross-check, both of these would have shipped as a line drawn to the wrong star,
which has **no visual symptom** — it just looks like a slightly odd constellation.

### 3.4 One more signal that the bridge is real

The highest catalogue row any figure references is **1,272 of 119,613** — every figure star
landed in the brightest 1% of the catalogue. Constellation figures are naked-eye patterns, so
that is what a correct bridge looks like; a bridge resolving to random rows would scatter
across 119,613. `ConstellationTests.FigureStarsAreNakedEye` asserts this (≤ 5% may be fainter
than V 6.5).

---

## 4. Extinction — the column that exists so dust never dims a star twice

`av` ships as **119,613 zeros, every row flagged `ExtinctionUnknown`**, provenance class
`Unknown`, with this in the file's own meta:

> "no 3-D dust map is shipped yet; every row is zero and flagged ExtinctionUnknown. A zero
> here is the absence of a measurement, not a measurement of zero extinction."

The column is here now so that when the `milkyway` lane lands the dust cube, A_V is baked into
the star record and `Photometry.ApparentMagnitude(M, d, av)` applies it **exactly once**.
`StarCatalogTests.ProvenanceIsHonest` fails if any row is flagged unknown while carrying a
non-zero A_V.

---

## 5. Core

| File | What it is |
|---|---|
| `Core/Data/CsData.cs` | the container reader; prefix reads, quantised-column decode, per-row provenance from flag bits and bit-field codes, `Attribution()` |
| `Core/Sky/StarCatalog.cs` | rewritten on `CsData`. `CatalogStar` gains velocity, B−V, A_V, HIP, HD, spectral class, `AbsoluteMagnitude`, `PositionAt(years)`. Adds `ObservedMagnitude(star, vantage)` and `CountVisibleFrom(vantage, limit)` |
| `Core/Sky/Photometry.cs` | new. The Atlas's own formulae, cited to line numbers in its `index.html` |
| `Core/Sky/Constellations.cs` | new. Figures as index pairs, `Validate(starCount)`, `ResolvedFraction`, `ReferencedStars()` |

**The Atlas formulae were read out of `index.html`, not invented** (lines 293–300, 344–350,
859–861): `psize = L^0.28`, `b = 10^(0.4(lim − m))`, `s = 1.15 b^0.23 · expo`,
`skyBoost = max(1, (s/1.5e-3)^0.9)`, `farAssist = min(6, (1.5e-3/s)^0.24)`.

`CatalogStar` keeps a 4-argument constructor so the ten existing call sites in
`StarSystemFactoryTests` compile unchanged — it sets `VelocityUnknown | ExtinctionUnknown`,
because that is what is true of a star with no catalogue row behind it.

---

## 6. Tests — what each proves, and what it cannot

`./run-tests.sh`. New: `CsDataTests` (6), `StarCatalogTests` (7), `PhotometryTests` (6),
`ConstellationTests` (5).

| Test | Proves | Measured value |
|---|---|---|
| `sirius/vega/arcturus` V and distance | the brightest sky is the real sky | **−1.44 @ 2.637 pc**, **0.03 @ 7.679 pc**, **−0.05 @ 11.257 pc** (dev); −1.46 @ 2.667, 0.03 @ 8.13, −0.04 @ 11.111 (clean) |
| `CountBrighterThan(6.0)` | the naked-eye sky is the right size | **5,070** dev / **5,080** clean, bounds 4,500–5,500 |
| `DistancesAreRealAndVaried` | it is a volume, not a dome | 109,389 distances, 1.296–990.1 pc, far/near ratio 764 |
| `EveryFigureIndexIsInTheCatalogue` | **the test the brief asked for** | max referenced row 1,272 of 119,613 |
| `FigureStarsAreNakedEye` | the HIP bridge resolved to the right rows | figure stars are all in the brightest 1% |
| `AttributionTravelsWithTheFile` | CC BY-SA is satisfied by the shipped file | "Stellarium" and "CC BY-SA" present |
| `PrefixReadMatchesTheFullRead` | the brightest-N contract holds | 1,000 rows byte-identical to the full read |
| `ProvenanceIsHonest` | teff is `Inferred`, A_V is `Unknown` | 119,613 flagged, 119,613 zero, 0 contradictions |
| `MalformedFilesAreRefused` / `OverrunningColumnIsRefused` | the container refuses, never reinterprets | 8 refusal cases including a self-consistent header with a lying column table |
| `CompressionCurvesMatchTheAtlas` | the port matches what the owner looked at | Sun psize 0.2763, eye response 1.15 at the limit, farAssist 1.17 at the Atlas's default |
| `DistanceModulusIsRight` | the photometry is the sky's | the Sun from Earth = **−26.75** |

**What they cannot prove.** Nothing here is rendered — no test says a star is *drawn* in the
right place, only that the number in the file is right. The vertex placement, the ≥ 2.5 px
floor and the `min(len(rel), R_sky)` clamp are the runtime lane's, and they are where a true
3-D field can still come out looking like a shell. No test compares against an external
ephemeris; the known answers are literature values written into the test. And the figure tests
prove the 138 shipped segments are sound — they say nothing about the 557 that are missing.

---

## 7. Deviations from the contract, and deferrals

| # | What | Why |
|---|---|---|
| 1 | **The development path is a CSS1 → CSS2 migration, not a rebuild from HYG.** | The HYG and AT-HYG CSVs are **not on the box** — `data-staging/REJECTED-astronexus-hyg/` is a rejection record with **0 bytes of data**. Disk was 5.2 GB against the brief's 6 GB abort floor when the lane started. So the dev build migrates the CSS1 file already in the tree: byte-exact for position, V, temperature and sort order; **velocity, B−V, spectral class, HIP and HD come out unknown and flagged** (`FromCss1Migration`), and `counts` says so. `--hyg PATH [--athyg PATH]` runs the full B §2.1 build and is implemented; it has never been executed. |
| 2 | **No AT-HYG join, no `--census` step.** | Same cause: no CSVs. Both code paths exist in `load_from_hyg`; neither has run. |
| 3 | **UCAC4 is not used.** | Only **one 0.2° declination zone** is staged (`z500`, 11.7 MB, a format probe). The full catalogue is 900 zones, about 10 GB — impossible under the disk floor. So the clean path stops at BSC5's V ≈ 7, not the ~260k stars to V 9.5 the licence report sketched. |
| 4 | **B−V is quantised to u8** (B §2.1 implies f32). | 0.0157 mag of resolution moves Ballesteros' temperature by well under 1%, `teff` ships at full f32 anyway, and it saves 359 KB. Encoding declared in the column meta. |
| 5 | **Figures ship at 19.9% of segments.** | §3. Everything resolvable from staged CC-clean data is resolved; the remainder needs the Wikidata fetch. The file records `resolvedFraction`, `Constellations.ResolvedFraction` exposes it, and `--require-figure-fraction` exists so nobody later ships a broken set believing it is complete. |
| 6 | On the clean path the `catalog-hip` stage is tagged `by-sa` although its HIP values come from IAU-CSN (`by`). | Conservative, not permissive — `by` ships wherever `by-sa` does. Not worth a dynamic tag. |

---

## 8. Known-red dependency

**None hit.** `StandardFrames.EquatorialToGalactic` has all three signs flipped (fix in flight
on `lane/foundation-code`), and this lane deliberately never touches it: every position is
kept in ICRF end to end, and the one place a galactic frame was needed — matching Atlas
positions to BSC5 in §3.2 — used BSC5's own published `lii`/`bii` columns instead of rotating
anything. No entry was added to `tools/gates/known-red.txt`.

---

## 9. What the runtime lane must know

1. **`stars.csstars`, `stars_named.json` and `constellations.json` are written by one run and
   must be loaded as a set.** The name and figure files hold **row indices**, so a stale pair
   points into the wrong rows silently. `Constellations.Validate(catalog.Count)` is the guard —
   call it at load, not only in tests.
2. **A prefix read breaks the figures.** Figure stars reach row 1,272, so
   `StarCatalog.Parse(data, N)` with N < 1,273 makes `Validate` fail. Either load enough rows
   or use `Constellations.ReferencedStars()` to force those rows to stay drawn.
3. **10,224 stars have no distance.** `DistanceParsecs` is `null`, `AbsoluteMagnitude` is NaN,
   and `ObservedMagnitude` from any vantage is NaN. They must be pinned to the sky shell by
   direction. Do not treat their position magnitude (the 100,000 pc sentinel) as a distance.
4. **`FaintestMagnitude` is 21.00 (dev) / 7.96 (clean).** The observed-sky limit slider must
   clamp to it and say so, or it repeats the Atlas's defect of a 3–14 slider over data that
   stops at 8.54.
5. **Extinction is zero and flagged.** Do not read `ExtinctionAv == 0` as a clear sightline.
   When the dust lane lands, it fills this column — the renderer must not also dim.
6. **`Constellations.ResolvedFraction` is 0.199, not 1.0.** The UI must not say "88
   constellations".
7. **`Attribution()` must be drawn.** Both paths ship CC BY-SA data; a comment in a source file
   does not discharge it.
8. **The Sun is not in the catalogue** (row 0 excluded, as before). MWA-13/PLUS-03 — the
   handover between the Sun-as-body and the Sun-as-star-point at log₁₀ U = −13.5 — is the
   runtime lane's, and it needs a synthetic row at the origin with M = 4.83, B−V 0.65.

---

## 10. ATLAS-PARITY rows

| Row | State after this lane |
|---|---|
| **MWA-11** stars at measured 3-D positions | **delivered (data)** — 109,389 measured distances in `sky/stars.csstars`. Rendering is the runtime lane's |
| **MWA-11b** real parallax, figures shear | **delivered (data)** — same file; the geometry is now in the data |
| **MWA-12** B−V → T → RGB | **delivered** — `Photometry.TemperatureFromColourIndex`, B−V now shipped per star |
| **MWA-14 / 14b / 14c** observed sky, limit mag, eye compression | **delivered (Core)** — `Photometry.EyeResponse`, `RelativeBrightness`, `StarCatalog.CountVisibleFrom`; `FaintestMagnitude` gives 14b its honest ceiling. UI is the `ui` lane's |
| **MWA-14a** named vantage | **delivered (Core)** — `ObservedMagnitude(star, vantage)` takes an explicit vantage |
| **MWA-19** 88 figures as index pairs | **partial, 44 figures / 138 of 695 segments (19.9%)** — §3, with the fraction in the file |
| **MWA-19a** figure names | **delivered, better than the Atlas** — label on the figure's **brightest star**, not the 3-D centroid of its endpoints, which is not a star |
| **MWA-27a / 27b** skyBoost, farAssist | **delivered (Core)** — `Photometry.SkyBoost` / `FarAssist`, with the Atlas's 1.17× at its own default pinned by a test |
| **PLUS-01** stars at real distances from any vantage | **delivered (data + Core)** |
| **PLUS-02** dust dims stars once | **column shipped, values pending the dust lane** — §4 |
| **PLUS-27** observed sky with named vantage + real extinction | **Core delivered**; extinction values pending |
| **MWA-13 / PLUS-03** one Sun, drawn once | **deferred to runtime** — §9.8 |
| **MWA-14d** one photometry path for stars, Cepheids, globulars | **delivered (Core)** — `Photometry` is engine-free and layer-agnostic; the `milkyway` lane calls the same functions |
| **HV2-26** flare flicker | **deferred** — timing is invented; out of a data lane's scope |

---

## 11. Reproducing this

```bash
cd ~/projects/CosmoSynth-wt/data-stars
python3 tools/cosmodata/csdata.py --selftest          # the committed fixture
python3 tools/cosmodata/build_starfield.py            # development (needs the CSS1 file)
COSMODATA_LICENCE=commercial python3 tools/cosmodata/build_starfield.py --out /tmp/cs-clean
./run-tests.sh
```

The one action that improves this lane most, in order:

1. **`python3 tools/cosmodata/fetch_hip_bridge.py`** once WDQS is out of its outage — CC0,
   about 40 KB, takes the figures from 19.9% toward complete. Re-run `build_starfield.py`
   after it; nothing else changes.
2. **Fetch `hyg_v44.csv.gz` and `hyglike_from_athyg_v40.csv.gz`** (28 MB, disk is now 11 GB
   free) and run `build_starfield.py --hyg ... --athyg ...`. That fills velocity, B−V,
   spectral class and HIP on the development path — and HIP alone would take the figures to
   100% there without Wikidata.
3. **Email `data.licences@esa.int`** (`docs/licence/stars.md` §4 Option A). One grant makes
   the development build shippable and §1.3 becomes history.
