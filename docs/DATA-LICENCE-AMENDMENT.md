# CosmoSynth data-licence amendment — the commercial bar

**Status: binding.** This amendment supersedes the data plan in
`docs/design/2026-09-11-B-data.md` wherever the two disagree, and supersedes every
licence statement in the pre-amendment `docs/data-sources.md`. It is the output of eight
family clearance passes run on 2026-09-12 and recorded in `docs/licence/*.md`, cross-checked
and reconciled here.

**Why it exists.** The owner's constraint of 2026-09-12, verbatim: *"make sure stuff sources
used are can be used in commercial capacity to make visualizaiton of real, and also this is
not just real but bridges to fictional worlds"*. Section B was written for non-commercial
sideload. Under a commercial bar, **eight of its fifteen shipped layers change source, and
one currently-shipped file must not ship at all.**

**Date of record:** 2026-09-12. Every licence page named below was read on that date.

---

## 0. Evidence convention, read this first

Three different strengths of claim appear in this document, and they are not
interchangeable:

| Tag | Means |
|---|---|
| **VERIFIED (ledger)** | I ran the command or read the file myself during this ledger pass, on triton, 2026-09-12. Reproducible from the command given. |
| **VERIFIED (family)** | The named family report states it read the primary page itself on 2026-09-12 and reproduces the decisive sentence verbatim. **I did not re-fetch the page.** The chain is: primary page → family agent → this ledger. |
| **INFERRED** | Reasoning on top of a verified fact. Not itself observed. Every legal reading in this document is INFERRED — no lawyer has seen any of it. |

Verbatim licence text and verbatim credit text are **not** truncated to fit a table cell.
The layer table (§2) therefore carries a verdict plus an evidence key `E#`; the full
quotation, its URL, its date and the family that read it live in the evidence register
(§4). Credit strings live whole in §5, keyed `C#`. A truncated licence quote is not
evidence.

---

## 1. The headline: what changed

1. **`sky/stars.csstars` (2,870,728 B, shipped today) cannot ship in a commercial build.**
   It is built from HYG, which is compiled from ESA Hipparcos, which is CC BY-NC 3.0 IGO.
   astronexus's genuine CC BY-SA 4.0 grant cannot convey commercial rights it does not
   hold. **The whole star layer is rebuilt** from Yale BSC5p (NASA HEASARC) + UCAC4 (USNO)
   + IAU-CSN. VERIFIED (family: `stars.md`), E2/E58/E54.
2. **The Planck CMB is dead and WMAP replaces it.** ESA Space Science Archive data is
   CC BY-NC 3.0 IGO with a written-authorisation clause, and mirroring through NASA/IPAC
   does not launder it. The WMAP 9-year ILC is already baked, verified and staged.
   VERIFIED (family: `cmb.md`), E1/E44/E42.
3. **Every large-scale-structure catalogue in Section B is blocked** — 2MRS, Cosmicflows-4
   (all three routes), 6dFGS, 2M++. DESI DR1 (CC BY 4.0, *"including commercially"*) plus
   SDSS (public domain) plus the VAST void catalogue (CC BY 4.0) replace them and go
   deeper, at the cost of all-sky coverage and of every measured redshift-independent
   distance. VERIFIED (family: `large-scale.md`), E34/E35/E36/E37.
4. **The dust source flips.** Vergely+2022 via CDS has no licence; Edenhofer 2024 and
   Leike 2020 are CC BY 4.0 on Zenodo and are staged as ready cubes. VERIFIED (family:
   `milkyway.md`), E5/E21/E22.
5. **The Local Volume giants lose their measured distances.** UNGC, HyperLEDA, NED-D and
   EDD/Cosmicflows are all blocked. Distances are now derived from LVDB satellite medians
   and are **Inferred**. VERIFIED (family: `local-volume.md`), E5/E31/E32/E33/E39.
6. **Pulsars drop from 4,393 to 294.** ATNF `psrcat.db` carries no data licence. Fermi LAT
   3PC substitutes. VERIFIED (family: `exo-and-transients.md`), E47/E46.
7. **Sunspot history drops from 1749 to 1947** and becomes F10.7 radio flux. SILSO is
   CC BY-NC, and NOAA republishing the identical series under the column name `ssn` does
   not launder it. VERIFIED (family: `solar-system.md`), E18/E17.
8. **Two blocking defects that are not licence questions** were found in passing and are
   carried here because they gate the same release: the app cannot render its own units
   (no font in the build covers U+2609 ☉ or U+2295 ⊕), and **the app has no credits
   surface at all**, so the CC BY obligations below currently have nowhere to land.
   VERIFIED (ledger), §10.

**Licence profile.** `licenceProfile: "sideload-nc"` in the Section B meta block (§1.2) and
in `tools/cosmodata/licence.py` is retired. There is one profile, `commercial`, and the
`nc` tag becomes a build-time **refusal**, not a warning.

---

## 2. The layer table — every layer the app ships

`§B` = the row in `docs/design/2026-09-11-B-data.md` §0 this replaces. Staged paths are
relative to `~/projects/CosmoSynth/data-staging/`. Every path, byte count and sha256 in
this table was re-verified during this pass — 146 of 146 matched (§8).

| # | Layer → shipped file | Chosen source | Verdict, licence | Ev. | Staged path | Change vs §B | Fallback if dropped | Credit |
|---|---|---|---|---|---|---|---|---|
| 1 | **Stars** → `sky/stars.csstars` (rebuild, magic `CSS2`) | Yale BSC5p via NASA HEASARC (9,110 rows, V≤6.5, with parallax) + UCAC4, USNO (~271k to V=9.5, no parallax) | **OK** — free use / US Government work | E6, E7 | `yale-bsc5-heasarc/`, `usno-ucac4/` | §B row 1 named AT-HYG v4.0 + HYG v4.4. **Both blocked.** All-sky parallax and 3-D space velocity are lost | Sky sphere with the bright layer only; directions are measured either way | C1, C2 |
| 2 | **Star names** → `sky/stars_named.json` | IAU Catalog of Star Names (IAU-CSN), IAU WGSN — 451 named stars with HIP, HD, J2000, V | **OK** — CC BY | E8 | `iau-wgsn-star-names/IAU-CSN.txt` | §B row 2 took names from AT-HYG `proper` (548 names). **Blocked.** 451 IAU-approved names replace them | Unnamed stars; the layer is labels only | C3 |
| 3 | **Constellations** → `sky/constellations.json` | Stellarium *modern* sky culture: 88 figures, 219 strokes, 695 segments, IAU B1875 boundary edges (upstream: Pierre Barbier) | **CONDITIONAL** — CC BY-SA 4.0. Share-alike attaches to the derived figure file only (INFERRED) | E9, E10 | `stellarium-modern-skyculture/` | §B row 2 kept, but the **figures are HIP-keyed and HIP→position is ESA's table**. Needs the Wikidata CC0 HIP↔HD bridge (coverage unverified) or re-authoring over BSC5 HR numbers | Re-author 88 figures over HR numbers — owned outright, zero licence surface | C4 |
| 4 | **Solar System bodies** → `catalog/solar_system.json` | JPL Horizons (DE440/DE441); NAIF SPICE `pck00011.tpc` + `gm_de440.tpc` for rotation/radii/GM; NASA NSSDCA fact sheets incl. the four ring sheets | **OK** — US Gov work; SPICE commercial use explicitly allowed, kernels redistributable **unmodified** | E11, E12, E13 | `jpl-horizons/`, `naif-spice-pck/`, `nasa-factsheets/` | §B row 6 unchanged. **New:** the hand-transcribed IAU WGCCRE table in `Core/Frames/BodyOrientation.cs` is replaced by the NAIF PCK — same values, NASA's public-domain restatement, 4,318 lines instead of 20 hand-typed bodies | none needed; this is the spine | C5, C6, C7 |
| 5 | **Spacecraft** → `catalog/spacecraft.json`, `models/*.csmesh` | JPL Horizons trajectories; NASA 3D Resources models (24) | **OK**, one open question — README says *"free and without copyright"*, `meta.json` says NOSA 1.3 | E11, E13, E14 | `nasa-3d-resources/` (evidence only; models already committed) | §B unchanged. **Correction:** `data-sources.md` claimed a per-model licence check; all 24 records carry one identical blanket string and one repository-directory URL, and the repository publishes no model→contributor map. VERIFIED (ledger) | drop individual craft; each is independent | C8 |
| 6 | **Surface textures** → `surfaces/*` | NASA Visible Earth Blue Marble; NASA SVS CGI Moon Kit (4720); USGS Astrogeology Mars Viking + lunar LOLA; Solar System Scope 2k set (15 files) | **OK** — US Gov work; Solar System Scope is **CC BY 4.0, attribution mandatory, changes must be indicated** | E13, E15, E16 | `nasa-visibleearth-earth/`, `nasa-svs-moon/`, `usgs-astrogeology/`, `solarsystemscope-textures/` | §B did not enumerate textures. **New hazard:** Solar System Scope also publishes `*_fictional` maps for Ceres, Eris, Haumea, Makemake, one filename suffix from the real ones, and `fetch_surface_maps.py` promotes a body to **Measured** on image validation alone. Never ingest those through that path | procedural surfaces; the renderer already has them | C9, C10, C11 |
| 7 | **Small bodies** → `sun/smallbodies.cssb` | JPL SBDB Query API: 32,294 asteroids H<14, 7,287 TNOs, 1,047 centaurs, 16,391 Jupiter Trojans | **OK** — US Gov work, same JPL policy | E11 | `jpl-sbdb/` | §B row 4 unchanged and confirmed | omit the belts | C6 |
| 8 | **Solar activity** → `sun/activity.cssa`, `sun/snapshot.json` | NOAA SWPC (`observed_swpc_ssn`, `smoothed_swpc_ssn`, `f10.7`, `smoothed_f10.7`, predicted cycle) + NOAA NCEI Penticton/Ottawa F10.7 daily 1947–2018 | **OK** — NWS public domain. **Column-level, not file-level** | E17 | `noaa-swpc-solar/`, `noaa-ncei-penticton-f107/` | §B row 5 shipped SSN from 1749-01. Those are the SILSO CC BY-NC series arriving under NOAA's `ssn`/`smoothed_ssn` columns. **Never ship those two columns.** 248 years of sunspot history lost; F10.7 has a real 79-year baseline | F10.7 monthly only; the cycle envelope survives | C12, C13 |
| 9 | **Solar imagery** → `sun/` stills | NASA SDO (AIA 171/193/304/4500, HMI intensitygram); NOAA GOES SUVI 195/304 + GOES-19 CCOR-1 coronagraph | **OK** — SDO not copyrighted; GOES under NWS public domain | E19, E17 | `nasa-sdo-imagery/`, `noaa-goes-suvi-ccor1/` | §B had no imagery row. **SOHO/LASCO is blocked** (non-commercial notice); CCOR-1 is the coronagraph substitute, ~30 years of CME archive lost | no solar disc imagery; procedural Sun | C13, C14 |
| 10 | **Milky Way dust** → `galaxy/dust.csdu` | Edenhofer et al. 2024, 1.25 kpc @ 10 pc (Zenodo 8187943) + Leike, Glatzle & Ensslin 2020, 740×740×540 pc @ 2 pc (Zenodo 3993082) | **OK** — CC BY 4.0 both; attribution + "indicate changes" (both were resampled and quantised) | E21, E22 | `edenhofer2024-3d-dust/`, `leike2020-3d-dust/` | §B row 7 named Vergely+2022 via CDS. **Blocked** (no licence in the ReadMe). Edenhofer was §B's own fallback and is now primary. Lost: mapped volume beyond 1.25 kpc | far-field column map only (786 KB, same source) | C15, C16 |
| 11 | **Galactic point sets** → `galaxy/{globulars,clouds,radcliffe}.csgp` | LVDB globulars (155 Harris-set + 58 new = 213 MW); Rice+2016 1,064 CO clouds; Zucker+2020 94 named cloud distances; Radcliffe Wave spine + clusters; Reid+2019 arm model from published parameters | **OK** — CC0 1.0 for all four catalogues; the arm model is facts, implemented not redistributed | E23, E24 | `lvdb-globular-clusters/`, `rice2016-molecular-clouds/`, `zucker2020-cloud-distances/`, `radcliffe-wave-konietzka2024/`, `reid2019-spiral-arms/` (doc only) | §B row 8 named Skowron+2019 Cepheids (**blocked** — the repo licence excludes *"scientific data"*), Harris/Baumgardt globulars (**both blocked**) and the Reid VizieR table (**blocked**). **No commercial-clean Galactic Cepheid catalogue exists.** Lost: per-star ages and the star-traced warp to 121 kpc | each point set drops independently | C17, C18, C19, C20 |
| 12 | **Deep sky and landmarks** → `galaxy/landmarks.json` | Hand-authored landmarks (one reference per entry) + OpenNGC 13,970 NGC/IC rows for names and angular sizes | Landmarks **OK** (ours). OpenNGC **CONDITIONAL** — CC BY-SA 4.0 | E25 | `openngc-deepsky/` | §B row 10 was hand-authored only. OpenNGC adds names and sizes but inherits share-alike on the derived table, and its upstream chain (NED, HyperLEDA, SIMBAD) is a recorded, unresolved risk | drop OpenNGC; label from Zucker (CC0) + IAU designations | C21 |
| 13 | **Local Volume** → `lv/localvolume.cslv` | LVDB v1.1.1 `dwarf_all.csv` (911 dwarfs, CC0) + 2MASS Large Galaxy Atlas v2 via IRSA (656 rows) + 2MASS XSC via IRSA (198,647 rows, `r_ext>20″`) + derived `heroes.json` (31 giants) + 14 2MASS LGA colour mosaics | **OK** — CC0 and US Government Works; imagery public domain | E23, E28, E29, E30 | `lvdb/`, `2mass-lga-irsa/`, `2mass-xsc-irsa/`, `heroes-derived/`, `imagery-2mass-lga/` | §B row 11 used UNGC (**blocked** via CDS *and* via the authors' own host, which states no terms) and the XSC **via VizieR** (**blocked**). IRSA is the clean route for the same measurements, and the LGA carries the position angle UNGC never had. **Every giant distance is now Inferred** | `heroes.json` is itself the fallback for the whole layer | C22, C23, C24, C25 |
| 14 | **Large-scale structure** → `lss/*` | DESI DR1 LSS clustering (5,481,299 rows) + SDSS DR18 SkyServer (492,189 galaxies with shapes, 747,982 quasars) + VAST void catalogue (1,163 VoidFinder voids) + our own `named-structures/` | **OK** — CC BY 4.0 (DESI, VAST); public domain (SDSS) | E34, E35, E36 | `desi-dr1-lss/`, `sdss-dr18/`, `vast-voids-sdss-dr7/`, `named-structures/` | §B row 12 named 2MRS, CF4 groups, CF4 basins, CF4 δ. **All blocked.** Gained: 4.3M galaxies to z=0.5 and 1.2M quasars to z=3.5, plus 1,163 voids. Lost: all-sky coverage (2MRS was 91%), every measured redshift-independent distance, and Laniakea as data | SDSS alone (public domain, no licence obligations at all) | C26, C27, C28 |
| 15 | **CMB** → `cmb/cmb_sky.cscb` | NASA LAMBDA WMAP 9-year (DR5) ILC map + KQ85 analysis mask, baked to 4096×2048 equirect and 6×1024² cube | **OK** — CC0 (NASA-led mission, no restrictive notice in either FITS header) | E42, E43 | `wmap9-ilc-lambda/`, `cmb-constants/` | §B row 13 named Planck PR3 SMICA-noSZ. **Blocked at every archive.** WMAP was §B's own fallback and is now primary, baked larger than §B specified (6×1024² not 6×512²). Costs ~12× angular resolution; invisible on a Quest 3 at 1°. **The app must never say "Planck"** | equirect only, no cube | C29, C30 |
| 16 | **Exoplanets** → `catalog/exoplanets.csexo` | NASA Exoplanet Archive `pscomppars`, 6,366 planets × 35 columns | **OK** — no restriction stated; NASA content generally not subject to US copyright | E45, E13 | `nasa-exoplanet-archive/` | §B row 3 unchanged and confirmed clean. Row count is 6,366, not §B's 6,360 | omit; hosts remain as stars | C31 |
| 17 | **Pulsars** → `catalog/pulsars.csgp` | Fermi LAT Third Pulsar Catalog (3PC), 294 gamma-ray pulsars × 97 columns | **OK** — NASA GSFC product, no proprietary period | E46, E13 | `fermi-lat-3pc-pulsars/` | Not in §B §0 at all. ATNF psrcat (4,393 rows) is **blocked** — no data licence, and CSIRO's site notice requires consent. **93% of rows lost.** The app must say *gamma-ray pulsars*, not *pulsars* | omit the layer | C32 |
| 18 | **Phenomena** → `phenomena/*` | Cosmic Atlas XR capture + exhibit texts (the owner's own work) + GWOSC: 391 confident GW events and 32 s of GW150914 H1/L1 strain | **OK** — ours; GWOSC is CC BY 4.0, modification must be indicated | E48 | `gwosc-gwtc/` | §B row 15 was own work only. GWOSC is new and clean. GW events have **no point sky position** — do not invent an RA/Dec | drop GW; phenomena capture stands alone | C33, C34 |
| 19 | **Fictional** → `catalog/fictional_holosystem.json` | The Ninth Field naming scheme (ours, generated from real galactic coordinates); ambientCG CC0 materials; Poly Haven CC0 HDRI; three NASA backdrop plates | **OK** — ours + CC0 + US Gov work | E52, E53, E13 | `fiction-naming-scheme/`, `textures-ambientcg-cc0/`, `textures-polyhaven-cc0/`, `imagery-nebula-nasa/` | §B row 14 shipped the holographic star map as-is. **Three third-party IP strings must be renamed before ship**: `MARVEL_COSMOS` (star map line 44), `WARP CORE` ×6 and `dilithium` ×5 (StarHolo). Freesound and OpenGameArt are blocked as *sources* | authored worlds need no external asset at all | C35, C36, C37, C38 |
| 20 | **Fonts** → `Assets/CosmoSynth/Fonts/DejaVuSans.ttf` | DejaVu Sans / Bold / Mono 2.37 | **OK** — Bitstream Vera Fonts Copyright; sale as part of a larger package explicitly permitted | E50 | `fonts-dejavu/` | Not in §B. The build ships Unity's `LegacyRuntime.ttf` today, whose **licence is fine and whose coverage is not**: 15 required codepoints absent including U+2609 ☉ and U+2295 ⊕ | none — this is a defect fix, not an optional layer | C39 |
| 21 | **Audio** → `audio/*` | Kenney Interface Sounds (100 `.ogg`, CC0) + an original 32 s ambient bed generated by `make_ambient.py` | **OK** — CC0 and ours | E51 | `audio-kenney-cc0/`, `audio-original-ambient/` | Not in §B. Freesound is **blocked as a source** (one of its three licences is CC BY-NC, and download needs an account) | silence; no layer depends on audio | C40, C41 |

---

## 3. Blocked register — source, reason, substitute, cost

Nothing in this list may be fetched, baked or shipped without a written grant. Each has a
rejection dossier on disk so nobody re-checks it.

### 3.1 Blocked by an explicit non-commercial licence

| Source | Licence | Ev. | Substitute | What is lost | Dossier |
|---|---|---|---|---|---|
| **ESA Gaia DR3** | CC BY-NC 3.0 IGO | E2, E1 | UCAC4 (astrometry, no parallax) | All-sky parallaxes, 3-D space velocities | `REJECTED-esa-gaia-dr3/` |
| **ESA Hipparcos / Tycho-1 / Tycho-2** (VizieR I/239, I/259) | `License: CC-BY-NC-3.0 IGO` in the ReadMe | E58 | BSC5p (bright, has parallax) + UCAC4 (deep) | ~109k measured distances | `REJECTED-esa-hipparcos-tycho/` |
| **Hipparcos New Reduction** (VizieR I/311) | No licence line at all; ESA credits it | E58 | as above | as above | `REJECTED-esa-hipparcos-tycho/` |
| **HYG v4.4** (astronexus) | Grant is genuine CC BY-SA 4.0; the astrometry underneath is ESA's CC BY-NC 3.0 IGO, which astronexus cannot sublicense | E54, E58 | The four-layer rebuild (rows 1–3 above) | **THE SHIPPED `stars.csstars` IS THIS FILE** | `REJECTED-astronexus-hyg/` |
| **AT-HYG v4.0** | Same, worse — its own README: *"Nearly 99% … from Gaia DR3"* | E55 | none for 3-D velocities | Deep-time star motion | `REJECTED-astronexus-athyg/` |
| **ESA Planck PR3** (PLA, and the IRSA mirror) | CC BY-NC 3.0 IGO; the mirror states no terms of its own and NASA's CC0 is scoped to *"a NASA-led mission"* | E1, E44, E42 | WMAP 9-year ILC | ~12× angular resolution | `planck-pr3-smica-esa-REJECTED/`, `planck-pr3-irsa-REJECTED/` |
| **SILSO International Sunspot Number** | `Licence: CC BY-NC` | E18 | NOAA `observed_swpc_ssn` (1997–) + F10.7 (1947–) | 248 years of sunspot history | `silso-sunspots/` |
| **SILSO laundered through NOAA** — the `ssn` and `smoothed_ssn` columns of `observed-solar-cycle-indices.json` | Same CC BY-NC series, byte-identical values, arriving under a public-domain hostname | E18, E17 | the `*_swpc_ssn` and `f10.7` columns of the same file | nothing beyond the row above | `noaa-swpc-solar/LICENCE.txt` |
| **VizieR / CDS copies of AAS-journal tables** (`J/ApJ`, `J/ApJS`, `J/AJ`) | *"…are under CC-BY-NC licence"* | E5 | the producer's own deposit (Zenodo, Dataverse, the survey portal) | nothing, when a deposit exists | `REJECTED-cds-vizier-simbad/`, `REJECTED-vizier-aas-aa-mnras-tables/` |
| **SOHO / LASCO** | *"…for public education efforts and non-commercial purposes … requires no expressed authorization."* Commercial use unaddressed = unauthorised. ESA/NASA joint mission, so NASA's default does not apply | E20 | GOES-19 CCOR-1 + GOES SUVI + SDO/AIA | ~30 years of CME archive, the wide C3 field | `REJECTED-soho-lasco/` |
| **Freesound as a source** | One of its three per-upload licences is CC BY-NC, and download needs an account | E56 | Kenney CC0 + the original ambient bed | nothing | `REJECTED-freesound/` |

### 3.2 Blocked by a scientific-use-only or by-permission grant

| Source | The whole of its terms | Ev. | Substitute | What is lost | Dossier |
|---|---|---|---|---|---|
| **VizieR / CDS A&A tables** (`J/A+A`) | *"free for a scientific usage"* | E5 | as 3.1 | — | `REJECTED-vizier-aas-aa-mnras-tables/` |
| **VizieR / CDS generally** | *"The commercial usage of the data is subject to rules depending of the origin."* | E5 | fetch from the origin and verify there | — | as above |
| **SIMBAD** | A citation request; and CDS: *"The Information system including the metadata is private and reserved to the CDS."* | E5 | build-time research aid only; rebuild resolved tables from clean primaries | Section B's `simbad` ledger row for `lss/structures.json` — replaced by our own `named-structures/` | `REJECTED-cds-vizier-simbad/` |
| **Reylé+2021 10 pc census** (CDS `J/A+A/650/A201`, §B ledger row `reyle`) | A&A: *"free for a scientific usage"* | E5 | none; drop the census | The complete nearby-star sample inside 10 pc | covered by `REJECTED-vizier-aas-aa-mnras-tables/` |
| **Harris MW globular catalogue** | *"…MUST DO SO WITHOUT CHARGE OF ANY FEE."* | E26 | LVDB (CC0) — 155 of 157 objects plus 58 newer | nothing of consequence | `harris-globulars-REJECTED/` |
| **HyperLEDA** | *"…an acknowledgment would be appreciated"* — no licence anywhere | E32 | 2MASS LGA + XSC via IRSA | homogenised all-sky galaxy photometry | `REJECTED-hyperleda/` |
| **6dFGS DR3** (WFAU and VizieR VII/259) | An acknowledgement instruction; MNRAS/OUP grants third parties nothing | E40 | DESI SGC — a narrow equatorial strip | the deep southern sky | `REJECTED-6dfgs/` |
| **ATNF Pulsar Catalogue** | Download page states nothing; package has no LICENSE; CSIRO: *"…any other use of copyright material on this website is subject to first receiving consent."* | E47 | Fermi LAT 3PC | 4,099 of 4,393 pulsars | `REJECTED-atnf-psrcat/` |
| **NRCan / spaceweather.gc.ca F10.7** | canada.ca: *"you may not reproduce materials on this site … for the purposes of commercial redistribution without prior written permission"* | E49 | NOAA NCEI + SWPC. **F10.7 the quantity is not blocked, only this host's files** | daily resolution 2018-05 → present | `REJECTED-nrcan-spaceweather-f107/` |

### 3.3 Blocked by silence — no redistribution licence stated anywhere

| Source | Ev. | Substitute | What is lost | Dossier |
|---|---|---|---|---|
| **Vergely, Lallement & Cox 2022 dust** (CDS `J/A+A/664/A174`) | E5 | Edenhofer 2024 + Leike 2020 | mapped volume beyond 1.25 kpc; mag/pc units | `vergely2022-dust-REJECTED/` |
| **Skowron+2019 Galactic Cepheids** — repo licence explicitly excludes *"scientific data"* | E27 | Rice 2016 clouds + Zucker 2020 + Radcliffe Wave + analytic Reid arms | per-star ages; the star-traced warp; few-per-cent distances | `skowron2019-cepheids-REJECTED/` |
| **Baumgardt globular cluster database** — a copyright line and nothing else | E62 | LVDB | N-body masses, mass functions, dispersion profiles, orbits | `baumgardt-globulars-REJECTED/` |
| **ASAS-SN Variable Stars** (a Cepheid candidate) | — | none found | confirms no clean Galactic Cepheid catalogue exists | in `skowron2019-cepheids-REJECTED/NOTES.md` |
| **Updated Nearby Galaxy Catalog (UNGC)** — blocked twice: via CDS, *and* the authors' own host states no terms at all | E5, E33 | 2MASS LGA v2 + LVDB satellite-median distances | 869 rows with a measured distance → 29 giants with an **Inferred** one | `REJECTED-ungc-vizier/` |
| **NED / NED-D** — *"Copyright (c) 2026, California Institute of Technology"* and no grant. **Do not generalise IRSA's clearance to NED: same institution, opposite rights posture** | E31 | LVDB satellite medians | measured galaxy distances | `REJECTED-ned/` |
| **Cosmicflows-4 / Laniakea watershed + density grids** (IP2I Lyon) — *"If you use this data cite the article above"* is the complete terms text | E38 | our own density field from DESI+SDSS; Laniakea as labelled points or a **declared** schematic, never as measured | the Laniakea boundary as data | `REJECTED-cosmicflows4-ip2i-grids/` |
| **Cosmicflows / EDD** (U. Hawaii) — zero occurrences of licence/copyright/terms/permission/credit on the page | E39 | as above | measured redshift-independent distances | `REJECTED-cosmicflows4-edd/` |
| **2MRS** (CfA/SAO) — *"(3) Please do not redistribute any of these files."* | E37 | DESI + SDSS | 91% all-sky coverage; the Zone of Avoidance | `REJECTED-2mrs-cfa/`, `REJECTED-2mrs-vizier/` |
| **2M++** — no licence, **and** its own ReadMe: *"Number of fake galaxies in ZoA = 3813"* | E41 | never use it | nothing; 5.2% of its rows are inventions a viewer cannot distinguish | `REJECTED-2mpp/` |
| **Siena Galaxy Atlas 2020** — the CC BY 4.0 sentence covers *images*; the catalogue's terms are stated nowhere | E61 | 2MASS LGA v2 | nothing needed | `REJECTED-sga2020/` |
| **Constellation boundaries via VizieR** (VI/49, VI/42) | E5 | the same Delporte 1930 edges arrive inside Stellarium's `index.json` | nothing | `REJECTED-constellation-boundaries-vizier/` |
| **Yale BSC5 via VizieR V/50** | E5 | the NASA HEASARC copy of the same catalogue | nothing | in `REJECTED-cds-vizier-simbad/NOTES.md` |
| **`sb2580/k3d_dust` HTML snapshot** (the cube the old Milky Way Atlas page ships) — GPL-3.0 repo, and the data's provenance is unstated | — | Edenhofer + Leike | nothing; the replacement is larger, finer and physically labelled | `k3d-dust-snapshot-REJECTED/` |
| **IAU WGCCRE 2015 tables as published by Springer** — values are facts, the compiled table is the publisher's | E12 | NAIF `pck00011.tpc` | nothing; the PCK is newer and larger | `iau-wgccre-springer/` |
| **OpenGameArt as a source** — per-file licences include CC BY-SA and GPL | E57 | Kenney CC0 | nothing | `REJECTED-opengameart/` |

### 3.4 ESA has three different rights postures — do not generalise either way

This bit trips people in both directions, so it is stated once, here:

| ESA property | Posture | Ev. |
|---|---|---|
| ESA Space Science Archives (Gaia, Planck, Hipparcos, XMM…) | **CC BY-NC 3.0 IGO**; commercial use by written authorisation from `data.licences@esa.int` | E1 |
| `esa.int` website and ESA publications | *"intended for the personal and non-commercial use of its users"* — **stricter** than the archive terms | E3 |
| `esahubble.org` (and ESO, and NOIRLab) | **CC BY 4.0** — commercial use permitted, credit must stay attached to the image, no implied endorsement | E4, E59, E60 |

The pre-amendment `docs/data-sources.md` recorded Gaia as "CC BY-SA 3.0 IGO". That was the
ESA *media* licence conflated with the *data* licence, and it was wrong in the direction
that matters.

---

## 4. Evidence register — the verbatim quotes

Every entry: the decisive sentence as published, its URL, the date it was read, and the
family report that read it. **I did not re-fetch these pages during this ledger pass**; the
tag is VERIFIED (family) throughout this section.

- **E1 — ESA Space Science Archives.** *"Data hosted in the ESA Space Science Archives are distributed under the CC BY-NC 3.0 IGO licence."* and *"Prior to any commercial use by the User of any Data or Data Product, including any use or application that directly or indirectly generates a financial gain, a detailed request for authorisation/licence shall be made by the User by sending email to data.licences@esa.int."* — <https://www.cosmos.esa.int/web/esdc/terms-and-conditions>, 2026-09-12. Read independently by four families (`stars`, `solar-system`, `milkyway`, `cmb`). Its credit table lists *"Planck / All / Credit: ESA, Planck Collaboration"*.
- **E2 — Gaia.** *"Gaia data are distributed under the CC BY-NC 3.0 IGO license."* — <https://www.cosmos.esa.int/web/gaia-users/license>, 2026-09-12 (`stars`, `milkyway`).
- **E3 — esa.int website terms.** *"The contents of the ESA website are intended for the personal and non-commercial use of its users."* — <https://www.esa.int/Services/Terms_and_conditions>, 2026-09-12 (`stars`).
- **E4 — ESA/Hubble.** *"Creative Commons Attribution 4.0 International license"*; *"The full image or footage credit must be presented in a clear and readable manner to all users, with the wording unaltered"*; *"The credit should not be hidden or disassociated from the image footage."* — <https://esahubble.org/copyright/>, 2026-09-12 (`local-volume`, `fiction-assets`). Exceptions: logos need prior written consent; music, scientific papers and code are **not** covered.
- **E5 — CDS / VizieR.** *"Tabular data, spectra or images coming from AAS journals (J/ApJ, J/ApJS, J/AJ) are under CC-BY-NC licence"*; *"Tabular data, spectra or images coming from A&A (J/A+A) are free for a scientific usage."*; *"The commercial usage of the data is subject to rules depending of the origin."*; *"The Information system including the metadata is private and reserved to the CDS."* — <https://cds.unistra.fr/vizier-org/licences_vizier.html>, 2026-09-12 (`stars`, `milkyway`, `local-volume`, `large-scale`). Also VERIFIED (family: `stars`): VizieR ReadMe files carry a greppable `License:` line **only when a licence exists** — 2 of 21 star-relevant catalogues had one.
- **E6 — NASA HEASARC.** *"HEASARC materials are all available freely for your use."* — <https://heasarc.gsfc.nasa.gov/docs/heasarc/data_policy.html>, 2026-09-12 (`stars`). The same page carves out ESA/JAXA non-commercial cases explicitly; BSC5p is not among them.
- **E7 — UCAC4.** Federal record metadata: `"license": "https://www.usa.gov/government-works"` — <https://catalog.data.gov/dataset/the-fourth-u-s-naval-observatory-ccd-astrograph-catalog>, 2026-09-12. Catalogue readme: *"The UCAC4 comes free of charge."* — <https://irsa.ipac.caltech.edu/data/USNO/UCAC4/readme_u4.txt>, 2026-09-12 (`stars`).
- **E8 — IAU-CSN.** *"All IAU-produced products (Images, Videos, Texts) are released under Creative Commons Attribution (i.e. free to use in all perpetuity, world-wide, as long as the source is mentioned)."* — file header, <https://www.pas.rochester.edu/~emamajek/WGSN/IAU-CSN.txt>, 2026-09-12 (`stars`).
- **E9 — Stellarium modern sky culture.** *"## License / Text and data: CC BY-SA 4.0 / Illustrations: Free Art License"* — <https://raw.githubusercontent.com/Stellarium/stellarium/master/skycultures/modern/description.md>, 2026-09-12 (`stars`). The repo root `COPYING` is GPL-2 and governs the program, not this data. The Free Art License illustrations are **not** taken.
- **E10 — Pierre Barbier (boundary upstream).** *"All the original content on this site is licensed under a Creative Commons Attribution-ShareAlike 4.0 International License"* — <https://pbarbier.com/terms.html>, 2026-09-12 (`stars`).
- **E11 — JPL.** *"Unless otherwise noted, images and video on JPL public web sites (public sites ending with a jpl.nasa.gov address) may be used for any purpose without prior permission, subject to the special cases noted below."* — <https://www.jpl.nasa.gov/jpl-image-use-policy/>, 2026-09-12 (`solar-system`). `ssd-api.jpl.nasa.gov` is a jpl.nasa.gov address.
- **E12 — NAIF SPICE.** *"Use of SPICE components in commercial products is allowed, subject to the substantial restrictions on user support noted under 'Getting Help from NAIF' above. No fees or licensing are required."* and *"Redistribution of SPICE kernels distributed by NAIF is permitted as long as they have not been modified."* — <https://naif.jpl.nasa.gov/naif/rules.html>, 2026-09-12 (`solar-system`).
- **E13 — NASA Brand Center.** *"NASA content – images, audio, video, and media files used in the rendition of 3-dimensional models, such as texture maps and polygon data in any format – generally are not subject to copyright in the United States."*; *"If the NASA material is to be used for commercial purposes, including advertisements, it must not explicitly or implicitly convey NASA's endorsement of commercial goods or services."*; *"The NASA Insignia, Logotype, identifiers, and imagery are not in the public domain. The use of the Insignia, Logotype and NASA identifiers is protected by law"* — <https://www.nasa.gov/nasa-brand-center/images-and-media/>, 2026-09-12 (`solar-system`, `fiction-assets`).
- **E14 — NASA 3D Resources, the conflict.** README: *"These assets are free and without copyright."* — <https://github.com/nasa/NASA-3D-Resources>, 2026-09-12. `meta.json`: `"license": "NASA Open Source Agreement Version 1.3"` (`solar-system`). Both VERIFIED to exist; which governs is INFERRED.
- **E15 — USGS Astrogeology, per product.** Mars Viking Global Color Mosaic 925 m: `Access Constraints: public domain` / `Use Constraints: None`. Moon LRO LOLA DEM 118 m: `Access Constraints: public domain` / `Use Constraints: Please cite authors`. Product pages read 2026-09-12 (`solar-system`).
- **E16 — Solar System Scope.** *"Distributed under Attribution 4.0 International license:"* / *"You may use, adapt, and share these textures for any purpose, even commercially."* — <https://www.solarsystemscope.com/textures/>, 2026-09-12 (`solar-system`).
- **E17 — NOAA / NWS.** *"The information on National Weather Service (NWS) Web pages are in the public domain, unless specifically noted otherwise, and may be used without charge for any lawful purpose so long as you do not: 1) claim it is your own … 2) use it in a manner that implies an endorsement or affiliation with NOAA/NWS, or 3) modify its content and then present it as official government material."* — <https://www.weather.gov/disclaimer>, 2026-09-12 (`solar-system`, `exo-and-transients`). The same page invokes 17 U.S.C. § 403, which is why credit C13 is not optional.
- **E18 — SILSO.** *"The World Data Center SILSO has adopted an open data policy (https://www.sidc.be/SILSO/legal) and our sunspot data can be freely downloaded (Licence: CC BY-NC : https://creativecommons.org/licenses/by-nc/4.0)."* — <https://www.sidc.be/SILSO/datafiles>, 2026-09-12 (`solar-system`, re-confirmed by `exo-and-transients`).
- **E19 — NASA SDO.** *"SDO images and movies are not copyrighted unless explicitly noted."* — <https://sdo.gsfc.nasa.gov/data/rules.php>, 2026-09-12 (`exo-and-transients`). Condition, rule 4: browse products are not for science analysis.
- **E20 — SOHO.** *"The use of SOHO images or data for public education efforts and non-commercial purposes is strongly encouraged and requires no expressed authorization."* — <https://soho.nascom.nasa.gov/data/summary/copyright.html>, 2026-09-12 (`exo-and-transients`). That is the complete notice; commercial use is unaddressed.
- **E21 — Edenhofer 2024.** Zenodo record rights field `"license": "cc-by-4.0"` — <https://zenodo.org/api/records/8187943>, 2026-09-12 (`milkyway`).
- **E22 — Leike 2020.** `"license": "CC-BY-4.0"` on the record; *"Provided under the terms of the Creative Commons Attribution 4.0 International License (CC BY 4.0)."* in the data's own `readme.txt` and embedded in `mean_std.h5`'s root `information` attribute — <https://zenodo.org/records/3993082>, 2026-09-12 (`milkyway`). Three independent statements.
- **E23 — Local Volume Database.** *"Creative Commons Legal Code / CC0 1.0 Universal"* — repo `LICENSE` at tag `v1.1.1`, <https://raw.githubusercontent.com/apace7/local_volume_database/v1.1.1/LICENSE>, 2026-09-12; corroborated by the Zenodo licence field *"Creative Commons Zero v1.0 Universal"*, DOI 10.5281/zenodo.21910097 (`local-volume`, `milkyway`).
- **E24 — Harvard Dataverse deposits.** `"license": {"name": "CC0 1.0", "rightsIdentifier": "CC0-1.0"}`, `"termsOfUse": null` — for Rice+2016 (doi:10.7910/DVN/EWY90X), Zucker+2020 (07L7YZ), Radcliffe Wave (F98QHY and XSCB9N) and Bayestar19 (2EJ9TX), all read 2026-09-12 (`milkyway`).
- **E25 — OpenNGC.** *"Unlike other similar databases which are released with license limitations, OpenNGC is released under CC-BY-SA-4.0 license, which allows the use for a wider range of cases."* — repo README, 2026-09-12; full licence text staged from `LICENSES/CC-BY-SA-4.0.txt` (`milkyway`). Note `…/master/LICENSE` is a 404.
- **E26 — Harris globulars.** *"THE DATA IN THIS CATALOG ARE SUPPLIED FREE OF CHARGE TO ALL USERS. ANY THIRD PARTY WHO SUPPLIES THIS CATALOG IN TURN TO OTHER USERS MUST REFER TO THE ORIGINAL SOURCE, WHICH IS THIS WEBSITE, AND MUST DO SO WITHOUT CHARGE OF ANY FEE."* — file header, <https://physics.mcmaster.ca/~harris/mwgc.dat>, 2026-09-12 (`milkyway`).
- **E27 — Skowron+2019.** *"For figures (e.g. PNG files) and other non-code files (except scientific data and unless stated differently) the license is: Creative Commons Attribution-ShareAlike 3.0"* — repo `LICENSE`, 2026-09-12 (`milkyway`). MIT covers `*.py` only. OGLE's own site states no terms.
- **E28 — 2MASS Large Galaxy Atlas.** License field `"https://www.usa.gov/government-works"`, publisher *"NASA/IPAC Infrared Science Archive"* — <https://catalog.data.gov/dataset/2mass-large-galaxy-atlas-photometry>, 2026-09-12 (`local-volume`).
- **E29 — 2MASS All-Sky Extended Source Catalog.** Same licence field — <https://catalog.data.gov/dataset/2mass-all-sky-extended-source-catalog>, 2026-09-12 (`local-volume`).
- **E30 — IPAC / 2MASS imagery.** *"The images in this gallery are released into the public domain."* — <https://irsa.ipac.caltech.edu/data/2MASS/docs/gallery/messiercat.html>; *"Unless otherwise noted, images and video on the IPAC public web sites may be used for any purpose without prior permission."* — <https://www.ipac.caltech.edu/page/image-use-policy>, both 2026-09-12 (`local-volume`).
- **E31 — NED.** *"Copyright (c) 2026, California Institute of Technology"* — the entirety of the rights content at <https://ned.ipac.caltech.edu/Documents/Guides/Copyright>, 2026-09-12 (`local-volume`). No grant follows it.
- **E32 — HyperLEDA.** *"If HyperLeda was helpful to your research, for example to prepare observations or astronomical papers, an acknowledgment would be appreciated"* — <http://atlas.obs-hp.fr/hyperleda/acknowledge.html>, 2026-09-12, OHP mirror (the Lyon host refuses HTTPS from triton) (`local-volume`, `milkyway`).
- **E33 — UNGC authors' own host.** <https://www.sao.ru/lv/lvgdb/> carries no terms, copyright, licence or redistribution statement of any kind — only a description, a contact address and a visitor counter. Read 2026-09-12 (`local-volume`). Verified by absence.
- **E34 — DESI DR1.** *"Users are free to share, copy, redistribute, adapt, transform, and build upon the DESI data available through this website for any purpose, including commercially, as long as they [cite the data release paper / indicate if any changes are made / include the following acknowledgments text]"* — <https://data.desi.lbl.gov/doc/acknowledgments/>, 2026-09-12 (`large-scale`).
- **E35 — SDSS.** *"All SDSS data released in our public data releases are considered in the public domain."* — <https://www.sdss.org/collaboration/image-use-policy/>, 2026-09-12 (`large-scale`). `sdss.org/science/data-use/` returns 404; this is the only public-domain sentence found.
- **E36 — VAST voids.** `"license": {"id": "cc-by-4.0"}`, `"access_right": "open"` — <https://zenodo.org/api/records/7406035>, 2026-09-12 (`large-scale`).
- **E37 — 2MRS (CfA).** *"(3) Please do not redistribute any of these files. This will help us ensure that all users receive the most up-to-date version."* — <http://tdc-www.harvard.edu/2mrs/2mrs_readme.html>, 2026-09-12 (`large-scale`).
- **E38 — Cosmicflows-4 grids (IP2I Lyon).** The complete statement of terms on the page, repeated under each download block: *"If you use this data cite the article above"* — <https://projets.ip2i.in2p3.fr//cosmicflows/>, 2026-09-12 (`large-scale`).
- **E39 — EDD (U. Hawaii).** The home page contains zero occurrences of *licence*, *license*, *copyright*, *terms*, *acknowledg*, *permission* or *credit* — <https://edd.ifa.hawaii.edu/>, 2026-09-12, reachable only with `curl --insecure` (`large-scale`). Verified by absence.
- **E40 — 6dFGS.** *"How to acknowledge the 6dF Galaxy Survey: Any use of these data should explicitly state that they come from the Final Release of 6dFGS and cite both the 6dGS DR3 paper … as well as the original 6dFGS survey paper"* — <http://www-wfau.roe.ac.uk/6dFGS/>, 2026-09-12 (`large-scale`). Attribution, not permission.
- **E41 — 2M++.** *"Number of real galaxies = 69160; Number of fake galaxies in ZoA = 3813; Number of groups = 4002"* and the `Name` column description *"…or ZOA fake galaxy"* — <https://cdsarc.cds.unistra.fr/ftp/J/MNRAS/416/2840/ReadMe>, 2026-09-12 (`large-scale`).
- **E42 — NASA science data licence.** *"Unless the data file is marked with a restrictive notice or license, data that is provided from a NASA-led mission including observations, engineering, calibration, and auxiliary data are licensed as Creative Commons Zero . There are no restrictions on the usage of these data."* — <https://science.data.nasa.gov/about/license>, 2026-09-12 (`cmb`). Both WMAP FITS headers were dumped in full and carry no COPYRIGHT/LICENSE card.
- **E43 — LAMBDA.** *"There is no cost involved in the use of LAMBDA; however, if your research benefits from its use then we request that you include the following acknowledgement in your publications"* — <https://lambda.gsfc.nasa.gov/contact/>, 2026-09-12 (`cmb`). A request, not a condition.
- **E44 — IRSA's Planck copy.** *"The IRSA Planck Public Data Release 3 (PR3) is the U.S. release of the mid-2018 ESA PR3 Planck data products."* and *"Planck is an ESA mission with significant NASA involvement."* — IRSA README, 2026-09-12 (`cmb`). The README states no licence. NASA's fallback clause: *"NASA may not be the original source of data and thus users are encouraged to validate the source and associated usage rights of the data."*
- **E45 — NASA Exoplanet Archive.** No restriction stated anywhere on the archive's own acknowledgement page; the words *commercial*, *licence*, *redistribute*, *permission* do not occur on it, and `/docs/data_use_policy.html` and `/docs/imageuse.html` both return 404. Read 2026-09-12 (`exo-and-transients`). Clearance rests on E13.
- **E46 — Fermi LAT.** *"all LAT science data has been released as early as possible, typically within a day or two of acquisition"* — <https://fermi.gsfc.nasa.gov/ssc/data/policy/>, 2026-09-12 (`exo-and-transients`). No proprietary period.
- **E47 — ATNF / CSIRO.** The download page's complete text is one sentence plus tarball links; *licence*, *license*, *copyright*, *terms*, *redistribute*, *commercial* do not appear. The package (`psrcat_pkg.tar.gz`, sha256 `2ea143fc…`) contains no COPYING/LICENSE/NOTICE. CSIRO: *"Except as permitted by statute, any other use of copyright material on this website is subject to first receiving consent."* — <https://www.csiro.au/en/about/Policies/Legal/Copyright>, all 2026-09-12 (`exo-and-transients`).
- **E48 — GWOSC.** *"Data released under a CC BY 4.0 License"* — <https://gwosc.org/> and <https://gwosc.org/acknowledgement/>, 2026-09-12 (`exo-and-transients`).
- **E49 — canada.ca.** *"Commercial reproduction — Unless otherwise specified, you may not reproduce materials on this site, in whole or in part, for the purposes of commercial redistribution without prior written permission from the copyright administrator."* — <https://www.canada.ca/en/transparency/terms.html>, the terms page `spaceweather.gc.ca` itself links to, 2026-09-12 (`exo-and-transients`).
- **E50 — DejaVu / Bitstream Vera.** *"Permission is hereby granted, free of charge … to use, copy, merge, publish, distribute, and/or sell copies of the Font Software"* and *"The Font Software may be sold as part of a larger software package but no copy of one or more of the Font Software typefaces may be sold by itself."* — the `LICENSE` **inside the release tarball** (staged as `fonts-dejavu/LICENCE-upstream.txt`), cross-read at <https://dejavu-fonts.github.io/License.html>, 2026-09-12 (`fiction-assets`).
- **E51 — Kenney.** *"License: (Creative Commons Zero, CC0) … This content is free to use in personal, educational and commercial projects."* — `License.txt` **inside the pack**; cross-read at <https://kenney.nl/support>, 2026-09-12 (`fiction-assets`).
- **E52 — ambientCG.** *"All ambientCG assets are provided under the Creative Commons CC0 1.0 Universal License."* / *"You can copy, modify, distribute and perform the assets, even for commercial purposes, all without asking permission."* — <https://docs.ambientcg.com/license/>, 2026-09-12 (`fiction-assets`).
- **E53 — Poly Haven.** *"All assets (HDRIs, textures and 3D models) on this site are … licensed as CC0."* / *"You can use our assets for any purpose, including commercial work."* — <https://polyhaven.com/license>, 2026-09-12 (`fiction-assets`).
- **E54 — HYG v4.4.** *"This work is licensed under a Creative Commons Attribution-ShareAlike 4.0 International License."* — <https://codeberg.org/astronexus/hyg/raw/branch/main/LICENSE>; README: *"HYG combines every identifiable star in the HIPPARCOS, Yale Bright Star, and Gliese (nearby star) catalogs"*, both 2026-09-12 (`stars`).
- **E55 — AT-HYG v4.0.** *"Nearly 99% of stars in AT-HYG have distance data from Gaia DR3 and 92% have full 3D velocity information from Gaia DR3."* — repo README, 2026-09-12 (`stars`).
- **E56 — Freesound.** *"freesound lets the user select one of three licenses for their sounds"* and *"To download a sound, first make sure you are logged into your registered account."* — <https://freesound.org/help/faq/>, 2026-09-12 (`fiction-assets`). One of the three is CC BY-NC.
- **E57 — OpenGameArt.** *"Yes, you can use this art. Even in commercial projects."* — but the same FAQ lists CC BY-SA 3.0/4.0 and GPL 2.0/3.0 among accepted licences — <https://opengameart.org/content/faq>, 2026-09-12 (`fiction-assets`).
- **E58 — VizieR I/239 and I/259 ReadMes.** `License: CC-BY-NC-3.0 IGO` — <https://cdsarc.cds.unistra.fr/ftp/I/239/ReadMe> and `…/I/259/ReadMe`, grepped 2026-09-12 (`stars`). I/311 (Hipparcos New Reduction) has no `License:` line at all.
- **E59 — ESO.** CC BY 4.0; *"the full image or footage credit must be presented in a clear and readable manner to all users, with the wording unaltered"*; *"ESO materials, images and videos may not be used to state or imply the endorsement by ESO or any ESO employee of a commercial product or service"* — <https://www.eso.org/public/copyright/>, 2026-09-12 (`local-volume`).
- **E60 — NOIRLab / NSF.** *"Unless specifically noted, the images, and videos distributed on the public NOIRLab website … are licensed under a Creative Commons Attribution 4.0 International License, and hence may on a non-exclusive basis be reproduced without fee provided the credit is clear and visible."* — <https://noirlab.edu/public/copyright/>, 2026-09-12 (`local-volume`).
- **E61 — DESI Legacy Imaging Surveys / SGA-2020.** *"Images are licensed under a Creative Commons Attribution 4.0 International License, and hence may on a non-exclusive basis be reproduced without fee provided the credit is clear and visible."* — <https://www.legacysurvey.org/acknowledgment/>, 2026-09-12 (`local-volume`). Covers **images**; the catalogue's terms are stated nowhere.
- **E62 — Baumgardt.** The only rights statement on the page is the copyright line *"(C) Holger Baumgardt, Antonio Sollima, Michael Hilker, Andrea Bellini, Eugene Vasiliev, Vincent Henault-Brunet & Nolan Dickson, March 2023"* — <https://people.smp.uq.edu.au/HolgerBaumgardt/globular/>, 2026-09-12 (`milkyway`).
- **E63 — Wikidata.** *"All data in Wikidata has a CC0 license."* — <https://www.wikidata.org/wiki/Wikidata:Licensing>, 2026-09-12 (`stars`). Coverage for the 710 Stellarium figure stars is **UNVERIFIED** — the WDQS count query timed out.

---

## 5. Credit block — exact strings for the app's about panel

These are the strings, verbatim, keyed to the layer table. Ship only the ones whose layer
actually ships. **C15, C16, C21, C26, C28, C33 and C9 are licence conditions with teeth
(CC BY / CC BY-SA); the rest are requested acknowledgements that cost one line each.**
This same block is reproduced in `docs/data-sources.md` so the app can paste from one
place.

```
C1  Bright-star data: Yale Bright Star Catalogue, 5th Revised Edition
    (Hoffleit & Warren 1991), via NASA HEASARC.
    This research has made use of data and/or software provided by the High Energy
    Astrophysics Science Archive Research Center (HEASARC), which is a service of the
    Astrophysics Science Division at NASA/GSFC.

C2  Star positions and proper motions: UCAC4, U.S. Naval Observatory
    (Zacharias et al. 2013, AJ 145, 44). Public domain (U.S. Government work).

C3  Star names: IAU Catalog of Star Names (IAU-CSN), IAU Division C Working Group on
    Star Names (WGSN). CC BY.

C4  Constellation figures and boundaries: Stellarium sky culture "modern"
    (Stellarium's team), CC BY-SA 4.0 - https://creativecommons.org/licenses/by-sa/4.0/
    Constellation boundary data (c) 2012-2022 Pierre Barbier, CC BY-SA 4.0.

C5  Ephemerides: JPL Horizons / DE440, NASA/JPL-Caltech.

C6  Small-body orbits and physical parameters: JPL Small-Body Database,
    NASA/JPL-Caltech.

C7  Body orientation and shape: NAIF SPICE PCK pck00011.tpc (NASA/JPL-Caltech),
    after Archinal et al. (2018), IAU WGCCRE 2015 report.
    Planetary and ring parameters: NASA NSSDCA Planetary Fact Sheets
    (D. R. Williams, NASA/GSFC).

C8  Spacecraft models: NASA 3D Resources (nasa.gov), with contributions from NASA ARC,
    GSFC, JPL, JSC, Ball Aerospace and DigitalSpace Corporation. Geometry simplified and
    recoloured for real-time use. NASA does not endorse this application.

C9  Planetary surface textures (c) Solar System Scope (solarsystemscope.com), licensed
    under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). Modified: resampled
    and recompressed for real-time use.

C10 Earth imagery: NASA Visible Earth / NASA Earth Observatory, Blue Marble Next
    Generation (Terra/MODIS), NASA Goddard Space Flight Center.
    Lunar colour and elevation maps: NASA's Scientific Visualization Studio, CGI Moon Kit
    (E. Wright, USRA; N. Petro, NASA/GSFC), from LRO LROC and LOLA data.

C11 Mars and lunar basemaps: USGS Astrogeology Science Center / NASA Planetary Data
    System. Mars Viking Global Color Mosaic (Viking Orbiter); Lunar LOLA elevation (LRO).

C12 Solar activity: NOAA/NWS Space Weather Prediction Center. 10.7 cm solar radio flux
    measured at the Dominion Radio Astrophysical Observatory, Penticton, by the National
    Research Council Canada; archived and redistributed by NOAA/NCEI and NOAA/SWPC.

C13 NOAA/NWS material incorporated in this application is not subject to copyright
    protection.

C14 Solar imagery courtesy of NASA/SDO and the AIA, EVE, and HMI science teams.
    Solar EUV and coronagraph imagery: NOAA/NWS Space Weather Prediction Center,
    GOES SUVI and GOES-19 CCOR-1.

C15 3D dust map: Edenhofer, Zucker, Frank, Saydjari, Speagle, Finkbeiner and Ensslin
    (2024), A&A 685, A82. Data via Zenodo doi:10.5281/zenodo.8187943, CC BY 4.0.
    Resampled and quantised for this application.

C16 Local 3D dust: Leike, Glatzle & Ensslin (2020), A&A 639, A138. Data via Zenodo
    doi:10.5281/zenodo.3993082, CC BY 4.0. Downsampled and quantised for this
    application.

C17 Globular clusters and dwarf galaxies: Local Volume Database (Pace 2025, OJAp 8,
    E142), https://github.com/apace7/local_volume_database, CC0 1.0. Individual distances
    and magnitudes carry their own per-object references in the catalogue.

C18 Molecular clouds: Rice, Goodman, Bergin, Beaumont & Dame (2016), ApJ 822, 52, from
    the Dame, Hartmann & Thaddeus (2001) CO survey. Harvard Dataverse
    doi:10.7910/DVN/EWY90X, CC0 1.0.
    Molecular cloud distances: Zucker, Speagle, Schlafly, Green, Alves and Goodman et al.
    (2020), A&A 633, A51. Harvard Dataverse doi:10.7910/DVN/07L7YZ, CC0 1.0.

C19 Radcliffe Wave: Konietzka, Goodman, Zucker, Burkert, Alves et al. (2024), Nature 628,
    62; structure discovered by Alves et al. (2020), Nature 578, 237. Harvard Dataverse
    doi:10.7910/DVN/F98QHY and doi:10.7910/DVN/XSCB9N, CC0 1.0.

C20 Spiral-arm geometry after Reid et al. (2019), ApJ 885, 131 (BeSSeL Survey maser
    parallaxes). Model implemented from the published fit parameters; no catalogue data
    redistributed.

C21 Deep-sky objects: OpenNGC by Mattia Verga, https://github.com/mattiaverga/OpenNGC,
    CC BY-SA 4.0. Modified: subset, reprojected and converted. The derived table is
    distributed under CC BY-SA 4.0.

C22 This work has made use of the Local Volume Database (Pace 2025, The Open Journal of
    Astrophysics, 8, 142). https://github.com/apace7/local_volume_database

C23 This publication makes use of data products from the Two Micron All Sky Survey, which
    is a joint project of the University of Massachusetts and the Infrared Processing and
    Analysis Center/California Institute of Technology, funded by the National
    Aeronautics and Space Administration and the National Science Foundation.
    2MASS Large Galaxy Atlas: Jarrett et al. (2003), AJ 125, 525.
    Dataset DOI 10.26131/IRSA122.

C24 This research has made use of the NASA/IPAC Infrared Science Archive, which is funded
    by the National Aeronautics and Space Administration and operated by the California
    Institute of Technology.

C25 [REQUIRED whenever a 2MASS image is displayed, shown with the image]
    Atlas Image mosaic obtained as part of the Two Micron All Sky Survey (2MASS), a joint
    project of the University of Massachusetts and the Infrared Processing and Analysis
    Center/California Institute of Technology, funded by the National Aeronautics and
    Space Administration and the National Science Foundation.

C26 This research used data obtained with the Dark Energy Spectroscopic Instrument
    (DESI). DESI construction and operations is managed by the Lawrence Berkeley National
    Laboratory. This material is based upon work supported by the U.S. Department of
    Energy, Office of Science, Office of High-Energy Physics, under Contract No.
    DE-AC02-05CH11231, and by the National Energy Research Scientific Computing Center, a
    DOE Office of Science User Facility under the same contract. Additional support for
    DESI was provided by the U.S. National Science Foundation (NSF), Division of
    Astronomical Sciences under Contract No. AST-0950945 to the NSF's National
    Optical-Infrared Astronomy Research Laboratory; the Science and Technology Facilities
    Council of the United Kingdom; the Gordon and Betty Moore Foundation; the
    Heising-Simons Foundation; the French Alternative Energies and Atomic Energy
    Commission (CEA); the National Council of Humanities, Science and Technology of
    Mexico (CONAHCYT); the Ministry of Science and Innovation of Spain (MICINN), and by
    the DESI Member Institutions: www.desi.lbl.gov/collaborating-institutions. The DESI
    collaboration is honored to be permitted to conduct scientific research on I'oligam
    Du'ag (Kitt Peak), a mountain with particular significance to the Tohono O'odham
    Nation. Any opinions, findings, and conclusions or recommendations expressed in this
    material are those of the author(s) and do not necessarily reflect the views of the
    U.S. National Science Foundation, the U.S. Department of Energy, or any of the listed
    funding agencies.
    DESI Collaboration et al. (2026), "Data Release 1 of the Dark Energy Spectroscopic
    Instrument". DESI DR1 data, CC BY 4.0.
    Modified from the DESI DR1 clustering catalogues: columns subset, converted to
    comoving positions, and downsampled for display.

C27 Funding for the Sloan Digital Sky Survey IV has been provided by the Alfred P. Sloan
    Foundation, the U.S. Department of Energy Office of Science, and the Participating
    Institutions.
    SDSS public data-release data are in the public domain. This work uses SDSS
    spectroscopic redshifts and SDSS DR18 SkyServer photometry.

C28 Void catalogue: Douglass, Kelly; Veyrat, Dahlia; BenZvi, Segev (University of
    Rochester), "VAST void catalogs for SDSS DR7", version 1.3.0, Zenodo,
    doi:10.5281/zenodo.7406035, CC BY 4.0. Described in Douglass, Veyrat & BenZvi, ApJS
    (2023), arXiv:2202.01226. Modified: converted from comoving Mpc/h to Mpc and subset
    to interior voids.

C29 Cosmic microwave background: WMAP 9-year Internal Linear Combination map and KQ85
    temperature analysis mask. Credit: NASA / WMAP Science Team, via the Legacy Archive
    for Microwave Background Data Analysis (LAMBDA), a service of the Astrophysics
    Science Division at NASA Goddard Space Flight Center.

C30 CMB temperature 2.72548 K (Fixsen 2009, COBE/FIRAS). CMB dipole 3.355 mK toward
    Galactic (263.99, 48.26) (Hinshaw et al. 2009, WMAP five-year).

C31 This research has made use of the NASA Exoplanet Archive, which is operated by the
    California Institute of Technology, under contract with the National Aeronautics and
    Space Administration under the Exoplanet Exploration Program.

C32 Pulsar data: Fermi LAT Third Catalog of Gamma-ray Pulsars (3PC), NASA/Fermi Science
    Support Center. Smith et al. 2023. Includes parameters collated from the ATNF Pulsar
    Catalogue (Manchester, Hobbs, Teoh & Hobbs, AJ 129, 1993, 2005),
    https://www.atnf.csiro.au/research/pulsar/psrcat

C33 Gravitational-wave event data: Gravitational Wave Open Science Center (gwosc.org), a
    service of the LIGO Scientific Collaboration, the Virgo Collaboration and KAGRA, used
    under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). Strain data for
    GW150914 from the LIGO Open Science Center.
    Gravitational-wave strain has been filtered and frequency-shifted for presentation.

C34 Phenomena: Cosmic Atlas XR (Nebulytic).

C35 Surface materials from ambientCG.com, licensed under CC0 1.0 Universal.

C36 HDRI environment from Poly Haven (polyhaven.com), licensed under CC0 1.0 Universal.

C37 Perseus Molecular Cloud - NASA/JPL-Caltech (Spitzer Space Telescope).
    Cat's Paw Nebula - NASA/JPL-Caltech (Spitzer Space Telescope).
    Solstice coronal mass ejection, 20 June 2013 - NASA/GSFC (SDO).

C38 Fictional systems, names and worlds authored for CosmoSynth (Nebulytic).

C39 DejaVu Sans, DejaVu Sans Bold and DejaVu Sans Mono.
    Fonts (c) 2003 Bitstream, Inc. Bitstream Vera is a trademark of Bitstream, Inc.
    Arev glyphs (c) Tavmjong Bah. DejaVu changes are in the public domain.
    Used under the Bitstream Vera Fonts Copyright.

C40 UI sounds by Kenney (kenney.nl), released under CC0 1.0 Universal.

C41 Ambient score generated procedurally for CosmoSynth.
```

**Prohibitions that travel with every credit above, and are not negotiable:**

- Never use the NASA insignia ("meatball"), logotype ("worm") or seal, or the JPL logo, in
  the app, its icon, its splash screen or any store asset. These are protected by law and
  are carved out of the public domain explicitly (E13).
- Never state or imply endorsement by NASA, JPL, Caltech, NOAA, USGS, ESA, ESO or NOIRLab.
- Never claim copyright in NASA-, NOAA- or USGS-derived material, and never present
  modified NOAA content as official government material (E17).
- NASA explicitly does not want its material used for NFTs.
- Every CC BY 4.0 source requires that **changes be indicated** — C9, C15, C16, C26, C28
  and C33 each carry that sentence; do not trim it.
- SDO images are *browse* products: never derive a measured number from their pixels.
- If ESA/Hubble, ESO or NOIRLab imagery is ever added, its per-image credit must be shown
  **attached to the image**, wording unaltered, not only in a credits scene.

---

## 6. Residual risks

Ordered by how much a bad answer would cost.

### 6.1 Share-alike obligations we are accepting

Two sources are CC BY-SA 4.0. Both are used, and the obligation is bounded:

| Source | The obligation, precisely | If we refuse it |
|---|---|---|
| **Stellarium modern sky culture** (+ Pierre Barbier boundaries) | The baked constellation-figure/boundary file is Adapted Material under §3(a)/(b). We must (a) credit Stellarium's team and Pierre Barbier, (b) name and link the licence, (c) offer **that one file** under CC BY-SA 4.0 — in practice, publish it at a stable URL named in the store listing, and ship a `LICENSES/` folder in the APK, and (d) apply no DRM to that file that restricts the licensed rights | Re-author the 88 figures over BSC5 HR numbers. Owned outright, no bridge needed, and that authoring pipeline has to exist anyway for the fictional constellations |
| **OpenNGC** | Same shape: the derived deep-sky table inherits CC BY-SA 4.0 and must be offered under it | Drop NGC/IC names and angular sizes; label from Zucker (CC0) plus IAU designations |

**INFERRED, and it is the one sentence worth a lawyer's eye before launch:** share-alike
attaches to those two derived data files and does **not** copyleft the Unity application,
the shaders, or any other data file. The app is a *Collection* that contains the
adaptations, not an adaptation of a constellation atlas. This is the standard reading of
CC BY-SA 4.0 §3(b) and it has not been reviewed by counsel.

### 6.2 Upstream-of-upstream — permissive compilations built on restricted inputs

This is the failure mode that cost the star layer, and it is not unique to HYG. Each row
is a permissive grant sitting on top of inputs the grantor may not control:

| Compilation | Its grant | What is underneath | Assessment |
|---|---|---|---|
| **HYG v4.4 / AT-HYG v4.0** | CC BY-SA 4.0, genuine | ESA Hipparcos (CC BY-NC 3.0 IGO), Gaia DR3 (CC BY-NC 3.0 IGO) | **BLOCKED.** The grantor cannot convey commercial rights it does not hold. This is the settled case |
| **OpenNGC** | CC BY-SA 4.0 | Its README names NED, HyperLEDA, SIMBAD, HEASARC, Corwin and *"internet sources like Wikipedia"* — **and NED, HyperLEDA and SIMBAD each block redistribution on their own pages** | **CONDITIONAL, unresolved.** Verga asserts rights over his own compilation, which is the strongest available position, and positions and angular sizes are facts (Feist). Selection and arrangement is what a compiler owns. Not verified that every upstream holder agreed. **Live risk** |
| **Fermi LAT 3PC** | NASA GSFC, no restriction | Its own README: *"The sequence of input sources is: 1) The ATNF pulsar catalog, 'psrcat' … We used ATNF psrcat v1.68."* — and psrcat is blocked | **ACCEPTED, with reasoning.** 3PC keeps 294 of 4,393 rows on a criterion external to psrcat, overwrites psrcat's distances and proper motions, and adds ~60 columns psrcat never had. The blocked artefact is the *file* `psrcat.db`; the values are facts. Credit C32 names ATNF anyway. **This is the closest call in the whole audit** |
| **UCAC4** | US Government work | Bundles **APASS** B/V photometry (AAVSO). APASS was never separately cleared | **OPEN.** UCAC4's astrometry is USNO's. Before shipping the APASS magnitude columns, read <https://www.aavso.org/apass>. Note that APASS is the only magnitude source for 54% of UCAC4 rows, so this is not a corner case |
| **NOAA's Penticton F10.7** | NWS public domain | Measured by the National Research Council Canada; `spaceweather.gc.ca`'s own terms bar commercial redistribution | **ACCEPTED, weakest link in the solar family.** The distinction from the SILSO trap: SILSO attaches a *named licence* that travels with the data; Canada attaches none, and canada.ca's clause is a website terms-of-use governing reproduction *from that site*. One email to NRC settles it |
| **NASA 3D Resources** | *"free and without copyright"* | Contributed models from Ball Aerospace, DigitalSpace Corporation and an individual (Carlo Conti); no model→contributor map exists | **ACCEPTED.** NASA makes the blanket statement over contributed models too, which is NASA asserting it holds the rights to give. Unverifiable, not merely unverified — the mapping does not exist to check |
| **`heroes.json`** (ours) | CC0 | LVDB (CC0) + 2MASS LGA (US Gov) only | **CLEAN.** Verified: no blocked input |

### 6.3 The SOHO seam — two artefacts, two verdicts, one mission

`models/soho.csmesh` (124,446 B) ships today, cleared under NASA 3D Resources (E14).
SOHO/LASCO *imagery* is blocked because SOHO is an ESA/NASA joint mission and NASA's
public-domain default does not reach it (E20). These are different artefacts with different
publishers, so it is not a contradiction — but it is exactly the kind of seam that gets
mis-generalised. **The model stays; no SOHO image ships.** If a textured SOHO model is ever
wanted, the texture is a new clearance question, not an inherited one. INFERRED.

### 6.4 Documented, unverified, and cheap to close

| # | Item | How to close it | Cost of being wrong |
|---|---|---|---|
| 1 | Whether ESA would grant a commercial licence for Gaia/Hipparcos/Planck, on what terms, how fast | One email to `data.licences@esa.int`. **This single action would unblock the star layer, the Planck CMB and the deep-time star motion at once** | None — it is the upside case, not the risk |
| 2 | NASA 3D Resources README vs `meta.json` (NOSA 1.3) | Email `arc-special-proj@lists.nasa.gov`. The only open licence item touching a currently-shipped asset | NOSA 1.3 carries notice, modification-marking and reciprocity terms the README says do not exist |
| 3 | Wikidata HIP↔HD coverage for the 710 Stellarium figure stars | Run the recorded SPARQL paginated with `VALUES` blocks of 50; the 120 s WDQS timeout is the only obstacle. Licence is VERIFIED CC0 (E63) | 88 broken constellation figures, or a re-authoring job |
| 4 | Yale University's compilation-copyright position on BSC5 | No statement found either reserving or granting. NASA HEASARC's free-use statement is the grant relied on | Thin — 9,110 rows of 1991 positional facts, redistributed by a US federal agency for three decades |
| 5 | APASS terms inside UCAC4 (see 6.2) | Read <https://www.aavso.org/apass> before taking the B/V columns | Loss of magnitudes for 54% of the telescopic star layer |
| 6 | Whether a newer IAU-CSN edition exists | Re-fetch and diff. VERIFIED that the `iau.org` URL in the file header 404s; the WGSN's live page is now `exopla.net/star-names/` | Missing recent star names |
| 7 | A&A's own copyright page | `aanda.org/about-aa/copyright` returns 403 to a fetcher and a JavaScript wall to curl. A&A's position is recorded only via CDS's summary | None — no commercial grant under either reading |
| 8 | Whether CDS's blanket CC-BY-NC line is current for post-2022 AAS catalogues (AAS went CC BY 4.0 open access in 2022) | Written answer from `cds-question@unistra.fr` | Could unblock Cosmicflows-4 (2023, ApJ). Nothing currently planned depends on it |
| 9 | CDS's own internal inconsistency — the sentence names **CC-BY-NC** and links the **CC BY-NC-ND** deed | VERIFIED that the page is inconsistent; not resolved | None for us; both forbid commercial use |
| 10 | Reid+2019 Table 2 arm parameters | **Not transcribed anywhere in this project.** A human must open the primary paper and copy each with its uncertainty — not scraped from VizieR, not recalled by a model | A confident, wrong Galaxy |
| 11 | SVS lunar DEM scale and offset | Stated on the SVS page, not in the TIFF header; confirm before baking | A plausible-looking Moon of systematically wrong radius |
| 12 | Bayestar19 `DM_bin_edges` distance grid | Absent from the FITS and from the first 32 MB of the 728 MB HDF5; `dustmaps` reads it from `/pixel_info.attrs` | Every distance in a derived cube would be a guess. Layer is cleared (CC0) but deliberately not staged |
| 13 | On-device CMB orientation | The bake is numerically verified (Galactic-plane residual, Cold Spot, cube↔equirect correlation 0.99649 against a scrambled control at −0.00991) but has never been rendered in the Unity sphere. **The app owns the ICRS→Unity basis change and it must be the same one the star layer uses** | A CMB rotated 60° off every star |
| 14 | Visual IP in the fictional layer | Nobody has looked at the pictures. A grep finds `MARVEL_COSMOS`; it cannot find a station silhouette that reads as a Death Star | A rename that fixes nothing |
| 15 | Legal review | **None has occurred.** Every reading here — Feist on measurement tables, 17 U.S.C. §105 on government works, CC BY-SA Collection-vs-Adaptation, "without charge of any fee" barring a paid app — is an engineering reading with the primary text quoted | Unknown |

---

## 7. Cross-family contradictions, and how each was resolved

Seven disagreements between the eight reports. Each is resolved with evidence, not by
majority.

1. **CDS/VizieR AAS tables: VERIFIED-blocked, or UNVERIFIED?**
   `solar-system.md` row 14 records the CDS rights page as unreadable and says "treat as
   blocked". Four other families (`stars`, `milkyway`, `local-volume`, `large-scale`) each
   fetched it successfully on the same day and reproduce the same sentence verbatim.
   **Resolved: VERIFIED blocked** (E5). The solar-system entry is a fetch failure, not a
   contrary reading, and the outcome is identical either way.

2. **`sky/stars.csstars`: CONDITIONAL or BLOCKED?**
   `solar-system.md` §3 calls it CONDITIONAL (CC BY-SA, commercial allowed, derived file
   inherits share-alike), citing the pre-amendment `data-sources.md` and saying explicitly
   that it did not re-verify because it is out of family. `stars.md` read the primary
   evidence — VizieR I/239's `License: CC-BY-NC-3.0 IGO` (E58) and HYG's own README naming
   HIPPARCOS as an input (E54). **Resolved: BLOCKED.** The owning family read the primaries;
   the other quoted a document this amendment is replacing.

3. **LVDB staged twice under two directory names.**
   `milkyway` staged `lvdb-globular-clusters/` (four globular tables); `local-volume`
   staged `lvdb/` (`dwarf_all.csv`). VERIFIED (ledger): both upstream licence copies have
   sha256 `a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499` — the same
   file, so the same source at the same version. **Resolved: one source, two tables, one
   ledger row (C17/C22).** Not a conflict; the duplication is deliberate and both
   directories stay.

4. **`noaa-swpc-solar/` written by two families.**
   `solar-system` staged 4 files; `exo-and-transients` appended 8 more and an addendum
   rather than overwriting. Both quote the same NWS statement (E17) and both carry the same
   SILSO warning. **Resolved: no conflict**; one directory, one ledger row, and the
   column-level SILSO rule applies to every file in it.

5. **`fiction-assets.md` §0.2: "the complete list of non-code, non-Unity-package binary
   assets in the build is `earth.jpg` and `moon.jpg`".**
   VERIFIED (ledger) that this is **incomplete as written**: `StreamingAssets/CosmoSynth/`
   also ships 24 `.csmesh` models, `stars.csstars` (2,870,728 B), and four catalogue JSONs.
   In context the family meant *art* assets, and it says elsewhere that the meshes are NASA
   3D Resources. **Resolved: read it as "art assets"; the data assets are covered by rows
   1, 5 and 16 of §2.**

6. **`fiction-assets.md` §7.5 recommends adding an "Authored" provenance category.**
   VERIFIED (ledger): `Core/Catalog/Provenance.cs` already has
   `ProvenanceClass.Fictional = 4`, documented as *"Authored for narrative or teaching
   purposes. Makes no claim about reality."* **Resolved: use the existing class. Do not add
   a fifth.** The recommendation's substance — that authored worlds must never wear a
   `Measured` badge — stands and is unaffected.

7. **The font fix names one call site; there are two.**
   VERIFIED (ledger), `unity/Assets/CosmoSynth/Runtime/UI/CosmoText.cs`:

   ```
   50:  if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
   52:  if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
   ```

   `fiction-assets.md` §0.1 quotes only line 50. **Resolved: the fix must replace both**,
   or the fallback silently restores the coverage gap on any Unity version where
   `LegacyRuntime.ttf` resolves to null.

---

## 8. Staged-inventory verification

**VERIFIED (ledger), 2026-09-12.** Every file path, byte count and sha256 claimed in the
eight family reports' staged-inventory tables was re-checked on triton:

```
ssh triton@100.127.10.14 'cd ~/projects/CosmoSynth/data-staging/_verify && python3 verify.py claims.txt'
verified OK: 146
problems   : 0
```

**146 of 146 files exist with exactly the claimed size and sha256. Zero mismatches.** The
claims file and checker are kept at `data-staging/_verify/` so the check is rerunnable.

Headline row counts were also re-counted rather than repeated (VERIFIED, ledger): LVDB
dwarfs **911**; 2MASS LGA **656**; 2MASS XSC subset **198,647**; `pscomppars` **6,366**;
Fermi 3PC **294**; OpenNGC **13,970**; VoidFinder voids **1,163**; SDSS galaxies
**492,189** and quasars **747,982**; BSC5p **9,110**; LVDB globulars **155 + 58 = 213**;
Zucker sightlines **326**; GWTC events **391**; IAU-CSN file **593 lines / 451 named
stars**. All match.

**Footprint and disk.** `du -sb data-staging` = **493,252,292 B (470 MiB)** across 554
files and 84 directories. `df -h /` = **15 GB free of 460 GB, 97% used** — above the 8 GB
floor, unchanged by this pass, which fetched nothing.

**Also verified by this pass** (each is a claim another document makes, re-run rather than
repeated):

- All 24 records in `catalog/spacecraft.json` carry the identical pair
  `"attribution": "NASA 3D Resources (public domain; see NASA media usage guidelines)"` and
  `"sourceUrl": "https://github.com/nasa/NASA-3D-Resources/tree/master/3D%20Models"`. The
  claim in the old `data-sources.md` that models "were checked individually" is not
  evidenced by the catalogue.
- `find unity/Assets -iname "*.ttf" -o -iname "*.otf"` returns nothing: there is no font
  file in the project.
- `grep -rl -i "credit" unity/Assets/CosmoSynth/Runtime/` returns nothing, and
  `grep -rl -i "credits" unity/Assets/` returns nothing. **There is no credits or about
  surface anywhere in the app.** Every CC BY and CC BY-SA obligation in §5 currently has
  nowhere to be discharged.

---

## 9. Procedure: what to do if a source is later found unusable

**The design requirement this creates: every layer must be droppable without a code
change.** A licence can be withdrawn, a page can be re-read and found to say something
else, or a lawyer can disagree with §6.1. When that happens the fix must be a rebake and a
manifest edit, never a code edit under time pressure.

**The mechanism** (extends Section B §3, the StreamingAssets loading contract):

1. `manifest.json` is the only index. `DataLoader` opens what the manifest lists and
   nothing else. A layer is dropped by removing its manifest entry and its file.
2. **Every layer's loader must treat "absent" as a normal state**, not an error — no
   exception, no `Debug.LogError`, no null dereference three frames later. A missing layer
   renders as that layer being off.
3. `manifest.json` carries a `licence` block per file: `{sourceId, licenceTag, commercial,
   attribution}`. `ledger.py --check` fails the build if any shipped file names a source
   absent from `docs/data-sources.md`, or names one whose tag is `nc` or `unclear`.
4. The credits panel is **generated from the manifest**, not hand-written. A dropped layer
   drops its credit line automatically; a new layer cannot ship without one. This is also
   the fix for §8's finding that no credits surface exists.
5. Each layer's fallback is named in §2 column "Fallback if dropped", and the fallback must
   be a *different source or a degraded mode*, never a fabricated value. 2M++ is the
   counter-example kept on file: 3,813 of its 72,973 rows are invented Zone-of-Avoidance
   galaxies, and a viewer cannot tell which.

**The procedure, when a source goes bad:**

1. **Stop the bake.** Do not ship a build made from it while the question is open.
2. **Write the rejection dossier first** — `data-staging/REJECTED-<slug>/` with
   `SOURCE.txt`, `LICENCE.txt` (the verbatim text that changed the verdict, its URL, the
   date read) and `NOTES.md` (what was lost, what replaces it). This is what stops the next
   person re-deriving the same answer.
3. **Remove the manifest entry and the file**, rebake, run `ledger.py --check`, run the
   test suite. The layer's tests must be written so they skip cleanly on an absent file,
   not fail.
4. **Look for a substitute at the producer, not the mirror.** The reusable lesson from this
   audit: a journal's data mirror and the authors' own deposit are different channels with
   different terms. The VAST void catalogue is CC BY 4.0 on Zenodo while its ApJS tables at
   CDS are CC BY-NC. Check Zenodo, Harvard Dataverse and the survey's own data portal
   before concluding a layer is dead.
5. **Update three documents in the same commit:** this amendment's §2 row, the blocked
   register §3, and `docs/data-sources.md`. A verdict recorded in one place and not the
   others is how the ESA error survived for months.
6. **If the layer has no substitute, say so in the app.** An empty Zone of Avoidance is the
   truth; a filled one is 2M++.

---

## 10. What this amendment requires, as edits

| File | Change |
|---|---|
| `tools/cosmodata/licence.py` | Delete the `sideload-nc` profile. One profile, `commercial`. Tags `nc` and `unclear` become build **failures**, not warnings |
| `tools/cosmodata/licence_decisions.json` | Record the seven §7 resolutions and the §6.4 open items, each with its evidence key |
| `tools/cosmodata/ledger.py` | `--check` must fail on any shipped file whose source is absent from `docs/data-sources.md` or carries a blocked tag; and must emit the credits block from the manifest |
| `docs/data-sources.md` | Fully rewritten by this pass. The old Gaia row ("CC BY-SA 3.0 IGO") was wrong; the old HYG row was incomplete; the old NASA-3D per-model claim was unevidenced |
| `docs/design/2026-09-11-B-data.md` | §0 rows 1, 2, 5, 7, 8, 11, 12, 13 and 14 are superseded by §2 here. §6's ledger is replaced. §1.2's `"licenceProfile": "sideload-nc"` example must read `"commercial"` |
| `Core/Frames/BodyOrientation.cs` | Replace the hand-transcribed IAU WGCCRE table with values loaded from the NAIF PCK (E12). Same numbers, public-domain restatement, and it stops a Springer table living inside a commercial product |
| `tools/cosmodata/build_starfield.py` | Rewritten on BSC5p + UCAC4 + IAU-CSN. The known-answer test (Sirius V = −1.44) still holds — BSC5p carries Johnson V |
| `tools/cosmodata/fetch_surface_maps.py` | Must refuse any filename matching `*_fictional*`, and must not be the ingest path for authored surfaces. Image validation is not provenance |
| `unity/Assets/CosmoSynth/Runtime/UI/CosmoText.cs` | **Both** lines 50 and 52 → `Assets/CosmoSynth/Fonts/DejaVuSans.ttf`. Add a play-mode test asserting `font.HasCharacter(c)` over the required codepoint list in `fonts-dejavu/fontcheck.py` |
| **New:** a credits panel | Generated from `manifest.json`, showing §5. This does not exist today and no CC BY obligation can be discharged without it |
| `holographic_star_map.html:44`, `~/projects/StarHolo` | `MARVEL_COSMOS` → `NINTH FIELD`; `WARP CORE` ×6 → `DRIVE BAY`; `warp core` → `pair cell`; `dilithium` ×5 → stored antimatter / `pair-cell lattice`; `warp starfield` → `transit blur` |

---

## 11. The fictional half of the constraint

The owner asked for two things and the second is easy to lose behind the first. Three rules
fall out of this audit, and all three are about the seam:

1. **A real object's identity and position must come from a cleared source, even when a
   blocked source has better data.** If an authored world hangs off a real star, take the
   star from the cleared layer and state its precision honestly. Reaching into the ESA
   archive because it is more precise is how a non-commercial dependency enters through the
   fiction rather than through the science.
2. **Authored content must never inherit a Measured label by accident.** The concrete
   hazard is `*_fictional` textures entering through `fetch_surface_maps.py`, which
   promotes a body to `Measured` because a file decoded as a 2:1 image. Authored assets
   need an ingest path that *cannot* set `Measured`. `ProvenanceClass.Fictional` already
   exists; use it.
3. **Authored content needs provenance rows too.** An authored world with a blank source
   field reads as an unlabelled real one. "Authored for CosmoSynth" is a provenance value,
   and a good one — it just has to be present.

The bridge itself is now better supported than before, because the commercial filter
happened to leave exactly the right seams: the ~10k stars with a measured direction and no
usable distance (the Ninth Field's home), the exoplanet hosts with real temperature and
luminosity, PSR B1257+12 appearing in two independently-cleared catalogues as both a spin
frequency and a planetary system, and gravitational-wave events — the one class of real
astronomical object with a genuinely unknown position, where an authored world contradicts
nothing that is known.

---

*Sources: `docs/licence/{stars,solar-system,milkyway,local-volume,large-scale,cmb,exo-and-transients,fiction-assets}.md`, all dated 2026-09-12. Staged evidence and rejection dossiers: `data-staging/`. Verification of this pass: `data-staging/_verify/`.*
