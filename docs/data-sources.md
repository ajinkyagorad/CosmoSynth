# Data sources and licensing

Every number in CosmoSynth traces to a source. This document lists them, quotes their terms
as published, and records what each one obliges us to do.

**This edition supersedes the previous one, which was written for non-commercial sideload
and is wrong in three places.** The corrections are in §9. The binding analysis, with the
verbatim evidence for every verdict, is `docs/DATA-LICENCE-AMENDMENT.md`; the eight family
clearance reports behind it are in `docs/licence/`. Where this file and
`docs/design/2026-09-11-B-data.md` disagree, this file and the amendment win.

**The bar in force:** commercial distribution of a paid Quest application, worldwide, EU
included. Every shipped byte must pass it.

**Date of record:** 2026-09-12. Every licence page quoted below was read on that date.

---

## 1. The four rules this document exists to enforce

**1. "Hosted by NASA" does not mean "public domain."**
NASA and ESA archives host contributed datasets, joint-mission products with partner terms,
and images with individual credits. NASA/IPAC IRSA mirrors Planck, and ESA's licence still
governs it. NASA's CC0 default is scoped to *"a NASA-led mission"*, and its own fallback
clause says so: *"NASA may not be the original source of data and thus users are encouraged
to validate the source and associated usage rights of the data."*

**2. Ask where the *column* came from, not where the *file* came from.**
NOAA's `observed-solar-cycle-indices.json` is served from a public-domain US Government
host and carries six columns. Two of them — `ssn` and `smoothed_ssn` — are the SILSO
International Sunspot Number, CC BY-NC, byte-identical from 1749-01 onward, in a file whose
NOAA-authored column is blank before 1997. A public-domain host does not launder a
non-commercial licence.

**3. A permissive compilation cannot convey rights its inputs withheld.**
astronexus grants HYG under CC BY-SA 4.0, and that grant is genuine. It sits on ESA
Hipparcos astrometry, which is CC BY-NC 3.0 IGO. astronexus cannot license what it does not
hold, so HYG is blocked for a commercial build and the star layer is rebuilt. Check the
compilation's own input list before trusting its licence.

**4. A licence audit is not a fitness audit.**
Liberation Sans passes every licensing question this document asks, and still cannot print
`M☉`. Ask both questions. The same shape appears in `fetch_surface_maps.py`, which promotes
a body to **Measured** because a file decoded as a 2:1 image — which is not the same as the
file being a photograph of that body.

**Corollary, for art:** *"CC0 site" does not mean "CC0 file."* ambientCG, Poly Haven and
Kenney licence their whole catalogue at once and are cheap to clear for that reason.
Freesound and OpenGameArt licence *per upload*, and a `.wav` on disk carries no record of
which licence it arrived under. Clear those one file at a time or not at all.

---

## 2. The licence profile

`tools/cosmodata/licence.py` holds one profile: **`commercial`**. The `sideload-nc` profile
is retired. Tags:

| Tag | Meaning | Build behaviour |
|---|---|---|
| `pd` | Public domain / US Government work / CC0 | ships |
| `pd-ack` | As above, acknowledgement requested | ships; credit line required |
| `by` | CC BY — attribution and "indicate changes" are licence conditions | ships; credit line **and** change statement required |
| `by-sa` | CC BY-SA — as above, and the derived file inherits share-alike | ships; the derived file must also be published under CC BY-SA |
| `cite` | Published physical facts, cited not redistributed | ships |
| `own` | This project's own work | ships |
| `nc` | Any non-commercial clause | **build failure** |
| `unclear` | No stated redistribution licence | **build failure** |

`ledger.py --check` fails the build if any shipped file names a source that is absent from
this document or carries `nc`/`unclear`. Decisions and their evidence keys are recorded in
`tools/cosmodata/licence_decisions.json`.

---

## 3. What ships today, and whether it passes

Read from `unity/Assets/StreamingAssets/CosmoSynth/` on 2026-09-12.

| Shipped file | Source | Verdict |
|---|---|---|
| `catalog/solar_system.json` (88,177 B, 31 bodies) | JPL Horizons | **PASS** |
| `catalog/spacecraft.json` (978,886 B, 24 craft) | JPL Horizons trajectories | **PASS** |
| `models/*.csmesh` (24 files, ~2.0 MB) | NASA 3D Resources | **PASS** (the asset; see §4.3 for a correction to this document's old claim about it) |
| `surfaces/earth.jpg` (238,676 B) | NASA Visible Earth | **PASS** |
| `surfaces/moon.jpg` (139,068 B) | NASA SVS 4720 / LRO LROC | **PASS** |
| `catalog/exoplanets.json` (329,896 B) | NASA Exoplanet Archive | **PASS** |
| `sky/stars_named.json` (57,079 B) | HYG `proper` names | **FAILS** — rebuild from IAU-CSN |
| **`sky/stars.csstars` (2,870,728 B, 119,613 stars)** | **HYG v4.4** | **FAILS. Must not ship.** See §4.1 |
| `galaxy/dust.csdu` (16,783,144 B, 256³ voxels at 10 pc) | Edenhofer et al. 2024 (Zenodo 8187943) | **PASS** — CC BY 4.0, credit C15 with the change statement; §4.7 |
| `galaxy/globulars.csgp` (28,252 B, 213 clusters) | Local Volume Database v1.1.1 | **PASS** — CC0; §4.8 |
| `galaxy/clouds.csgp` (34,812 B, 1,039 clouds) | Rice et al. 2016 | **PASS** — CC0; §4.8 |
| `galaxy/radcliffe.csgp` (35,496 B, 1,500 spine points) | Konietzka et al. 2024 | **PASS** — CC0; §4.8 |
| `galaxy/radcliffe_clusters.csgp` (8,156 B, 125 clusters) | Konietzka et al. 2024 | **PASS** — CC0; §4.8 |
| `galaxy/namedclouds.json` (44,158 B, 94 named clouds) | Zucker et al. 2020 | **PASS** — CC0; §4.8 |
| `galaxy/landmarks.json` (7,039 B, 12 places) | hand-authored, one citation each | **PASS** — `own`; §4.8 |
| *(no font file exists in the project)* | Unity built-in `LegacyRuntime.ttf` | **FAILS on fitness** — 15 required codepoints absent, including U+2609 ☉ and U+2295 ⊕ |
| *(no credits panel exists in the app)* | — | **BLOCKS RELEASE** — the CC BY and CC BY-SA obligations below have nowhere to be discharged |

---

## 4. Sources, one section each

Each section: what it supplies, how it is accessed, its terms as published, what it obliges
us to do, where the staged evidence lives, and the pitfalls that cost time.

Staged paths are under `~/projects/CosmoSynth/data-staging/`. Every one was re-verified on
2026-09-12 — 146 files, all matching their recorded size and sha256, zero mismatches
(`data-staging/_verify/`).

---

### 4.1 Stars — Yale BSC5p (NASA HEASARC) + UCAC4 (USNO) + IAU-CSN

**Replaces HYG, which cannot ship.** HYG v4.4 and AT-HYG v4.0 carry a genuine CC BY-SA 4.0
grant from astronexus over a compilation of ESA Hipparcos, Yale Bright Star and Gliese —
and the Hipparcos half is CC BY-NC 3.0 IGO (`License: CC-BY-NC-3.0 IGO`, VizieR I/239
ReadMe). AT-HYG is worse: its own README says *"Nearly 99% of stars in AT-HYG have distance
data from Gaia DR3."* Rule 3 applies. `sky/stars.csstars` as it exists today is
HYG-derived and must be rebuilt before any commercial build.

| Layer | Source | Terms | Staged |
|---|---|---|---|
| V ≤ 6.5, 9,110 stars **with parallax**, B−V, pmRA/pmDec, HD/HR/SAO/FK5/DM, Bayer + Flamsteed | Yale Bright Star Catalogue 5th rev. via **NASA HEASARC** | *"HEASARC materials are all available freely for your use."* — heasarc.gsfc.nasa.gov/docs/heasarc/data_policy.html. The same page carves ESA/JAXA non-commercial cases out explicitly; BSC5p is not among them | `yale-bsc5-heasarc/heasarc_bsc5p.tdat.gz`, 913,895 B, 9,110 rows × 53 fields |
| 6.5 < V < 9.5, ≈271k stars, position + proper motion + APASS B,V, **no parallax** | **UCAC4**, U.S. Naval Observatory (Zacharias+ 2013) | US Government work — the federal record's own metadata field reads `"license": "https://www.usa.gov/government-works"` (catalog.data.gov). The catalogue readme: *"The UCAC4 comes free of charge."* | `usno-ucac4/z500`, 11,671,452 B, 149,634 records (one 0.2° declination zone, format probe) |
| 451 IAU-approved proper names with HIP, HD, J2000, V | **IAU-CSN**, IAU WGSN | *"All IAU-produced products (Images, Videos, Texts) are released under Creative Commons Attribution (i.e. free to use in all perpetuity, world-wide, as long as the source is mentioned)."* — the file's own header | `iau-wgsn-star-names/IAU-CSN.txt`, 71,800 B, 451 names |

**Obligations:** credits C1, C2, C3 (§7). The UCAC4 `license` field is machine-readable US
Government Works; no attribution is legally required, but it costs a line.

**What the rebuild costs, honestly.** All-sky parallaxes are gone — HYG gave a measured
distance for ~109k stars; BSC5p gives ground-based parallaxes for the bright few thousand
and UCAC4 gives none. The 3-D star field becomes a sky sphere with a shallow near-star
volume, and 3-D space velocities (which came from Gaia radial velocities) go with it. For
an app whose premise is Earth → Solar System → local stars, that is the expensive loss.

**Two honesty notes carried forward from the HYG era, because they still apply:**

- **Temperature is inferred, never measured.** It comes from the B−V colour index through
  Ballesteros' formula (EPL 97 34008, 2012), and where no colour index is published it
  comes from the spectral class alone — a weaker claim still, flagged separately in the
  record so the runtime can tell the two apart. BSC5p carries B−V for the bright layer;
  UCAC4 carries APASS B and V for 54% of its rows.
- **Stars with no usable parallax are kept and flagged, never placed.** Their *direction*
  is measured and their distance is not known at all, which is precisely the case the
  sky-sphere treatment exists for. Dropping them deletes a tenth of the sky; placing them
  invents a distance. Under the rebuild this class grows, not shrinks — which is exactly
  where the authored Ninth Field worlds live (§4.14).

**Pitfall — APASS is not cleared.** UCAC4 bundles AAVSO APASS B/V photometry and was
cleared as a USNO product. Before shipping those columns, read
<https://www.aavso.org/apass>. This matters: APASS is the only magnitude source for 54% of
UCAC4 rows.

---

### 4.2 Constellations — Stellarium "modern" sky culture (CC BY-SA 4.0, conditional)

88 figures, 219 strokes, 695 segments over 710 distinct HIP ids, plus the IAU boundary
edges at epoch B1875 (upstream: Pierre Barbier's `edges_18.txt`).

- **Terms:** *"## License / Text and data: CC BY-SA 4.0 / Illustrations: Free Art
  License"* — `skycultures/modern/description.md`. The repository root `COPYING` is GPL-2
  and governs the **program**, not this data. The Free Art License illustrations are **not
  taken**. Upstream boundaries: *"All the original content on this site is licensed under a
  Creative Commons Attribution-ShareAlike 4.0 International License"* — pbarbier.com/terms.html.
- **Staged:** `stellarium-modern-skyculture/index.json` (205,767 B) + `description.md`.
- **Obligations:** credit C4, **and** the derived figure/boundary file must itself be
  offered under CC BY-SA 4.0 — in practice, publish that one file at a stable URL named in
  the store listing, ship a `LICENSES/` folder in the APK, and apply no DRM to it that
  restricts the licensed rights. It does **not** copyleft the application; the app is a
  Collection containing the adaptation, not an adaptation of a constellation atlas.
  INFERRED, standard reading, worth a lawyer's eye before launch.
- **Pitfall — the HIP problem.** Every figure vertex is a HIP number. HIP identity as an
  integer key is a fact and fine to keep; HIP → (RA, Dec) is ESA's table and is not. Of the
  710 distinct HIP ids, only 244 (34.4%) resolve to an HD number through IAU-CSN alone —
  measured, not assumed. Two ways out: a CC0 HIP↔HD bridge from Wikidata (*"All data in
  Wikidata has a CC0 license"* — licence verified, coverage **unverified**, the WDQS count
  query timed out), or re-author the 88 figures over BSC5p HR numbers. The second is owned
  outright, has zero licence surface, and that authoring pipeline has to exist anyway for
  the fictional constellations.

---

### 4.3 Solar System — JPL Horizons, NAIF SPICE, NASA NSSDCA

#### JPL Horizons — <https://ssd.jpl.nasa.gov/horizons/>

The primary source. Gravitational parameters, radii, densities, geometric albedos and
osculating orbital elements for all 31 bodies in the shipped catalogue, plus the 260 state
vectors in the validation fixture, plus every spacecraft trajectory.

- **Accessed via** the public API, offline, by `tools/cosmodata/`. Never at runtime.
- **Ephemeris:** DE440/DE441 planetary and lunar ephemerides.
- **Terms:** US Government work. JPL's image use policy: *"Unless otherwise noted, images
  and video on JPL public web sites (public sites ending with a jpl.nasa.gov address) may
  be used for any purpose without prior permission, subject to the special cases noted
  below."* NASA requests acknowledgement. No key, no registration.
- **Etiquette:** JPL asks automated clients to keep their request rate modest. The
  importers sleep between requests by default (`--delay`).
- **Obligation:** credit C5.
- **Coverage is per-object and finite, and was verified rather than assumed** — `-25` is
  Lunar Prospector, not Magellan; JPL's Webb ephemeris ends in July 2031; Dawn's begins at
  12:40 on launch day, so a request starting at midnight that morning returns nothing at
  all. Every spacecraft ships with the window over which its position is claimed to be
  known, and is **not drawn outside it**. See `docs/accuracy.md`.

#### JPL Small-Body Database Query API — <https://ssd-api.jpl.nasa.gov/>

32,294 asteroids with H < 14, 7,287 TNOs, 1,047 centaurs, 16,391 Jupiter Trojans.

- **Terms:** same JPL policy — `ssd-api.jpl.nasa.gov` is a jpl.nasa.gov address. The
  response self-identifies as `"source":"NASA/JPL SBDB (Small-Body DataBase) Query API"`.
- **Staged:** `jpl-sbdb/` — 4 JSON files, 9,025,553 B total.
- **Obligation:** credit C6.
- **Pitfall:** the query API returns about four significant figures by default (Ceres
  `a = 2.766`). Request full precision and assert that you got it.

#### NAIF SPICE generic kernels — <https://naif.jpl.nasa.gov/pub/naif/generic_kernels/pck/>

Pole right ascension and declination, prime meridian angle, rotation rate, radii and GM.
**This replaces the hand-transcribed IAU WGCCRE table currently in
`Core/Frames/BodyOrientation.cs`.**

- **Terms:** *"Use of SPICE components in commercial products is allowed… No fees or
  licensing are required."* and *"Redistribution of SPICE kernels distributed by NAIF is
  permitted as long as they have not been modified."* — naif.jpl.nasa.gov/naif/rules.html.
- **Staged:** `naif-spice-pck/pck00011.tpc` (131,226 B, 4,318 lines) and `gm_de440.tpc`
  (12,406 B).
- **Why the change:** the WGCCRE *values* are physical facts and free to use, but the
  compiled table as published in CMDA 130:22 (2018) is Springer's, and `BodyOrientation.cs`
  currently hand-transcribes it into a commercial product. The PCK is NASA's public-domain
  restatement of the same report — newer (2026-12-27), 4,318 lines against 20 hand-typed
  bodies, and it carries the periodic terms too. Keep citing Archinal et al. (2018).
- **Obligation:** credit C7. Kernels must be redistributed **unmodified** if redistributed
  at all; we bake from them rather than ship them.

#### NASA NSSDCA planetary fact sheets — <https://nssdc.gsfc.nasa.gov/planetary/factsheet/>

Bond albedos, atmospheric pressures, scale heights, temperatures, compositions, and the
four ring-system fact sheets (Saturn, Jupiter, Uranus, Neptune).

- **Terms:** US Government work, not subject to domestic copyright.
- **Staged:** `nasa-factsheets/` — 10 HTML files, 79,402 B.
- **Location in code:** `tools/cosmodata/bodies.py`, each value with its source noted inline.
- **Obligation:** credit C7.

---

### 4.4 Spacecraft models — NASA 3D Resources

The 24 spacecraft models. Fetched by `tools/cosmodata/build_spacecraft.py`, converted to
the compact `.csmesh` format, and committed under `StreamingAssets/CosmoSynth/models/`.

- **Terms:** README: *"These assets are free and without copyright."* NASA's Media Usage
  Guidelines cover *"polygon data in any format"* explicitly.
- **Open question, recorded rather than glossed:** the repository contradicts itself. Its
  `meta.json` says `"license": "NASA Open Source Agreement Version 1.3"`, a software licence
  with notice, modification-marking and reciprocity terms the README says do not exist. The
  assessment — that `meta.json` is a code.nasa.gov catalogue stub (its sibling fields are
  `languages`, `operating_systems`, `software_types`, and the repository contains no
  software) and the README governs — is **INFERRED**. Close it by emailing
  `arc-special-proj@lists.nasa.gov`. **This is the only open licence item touching a
  currently-shipped asset.**
- **Correction to the previous edition of this file.** It said *"Some models in that
  repository are contributed rather than NASA-authored; the ones shipped here were checked
  individually, and each catalogue record carries the source URL and licence string."*
  That is not supported by the catalogue: all 24 records in `catalog/spacecraft.json` carry
  the **identical** pair `"attribution": "NASA 3D Resources (public domain; see NASA media
  usage guidelines)"` and `"sourceUrl": ".../NASA-3D-Resources/tree/master/3D%20Models"`.
  The repository publishes no model→contributor mapping and the model directories contain
  only a `.glb` and a `.png`. The true statement is: **the repository carries a single
  collection-level public-domain statement covering contributed models as well as
  NASA-authored ones; there is no per-model licence data to check against, and each
  catalogue record carries that collection-level statement.** The contributor list is
  repository-level and does include non-NASA entities — Ball Aerospace, DigitalSpace
  Corporation and Carlo Conti. Claiming a check that left no record is worse than claiming
  none, because it stops the next person doing it.
- **The NASA insignia cannot be present, and here is why.** `.csmesh` has no UVs and no
  texture maps at all — each submesh carries one flat RGBA colour. No image from any source
  model is shipped. **This conclusion expires the moment spacecraft become textured**, and
  then each model's textures must be inspected for the insignia before shipping.
- **Obligation:** credit C8, including *"NASA does not endorse this application."*

**Five things about this source are worth recording, because each cost time:**

- **Almost every model is Draco-compressed** (`KHR_draco_mesh_compression`, and *required*,
  not optional). Draco is an entropy-coded connectivity format; decoding it is not a job
  for a hand-written parser. The importer shells out to `npx @gltf-transform/cli` for that
  one step. This is the only tool in `tools/cosmodata/` that is not standard-library-only,
  and the dependency is confined to it: the `.csmesh` output is committed, so building the
  app, running the tests and using the data need nothing but Python.
- **A silent decode failure looks exactly like a success.** A well-known Python mesh
  library, handed one of these models without a Draco decoder present, returns a scene with
  the right number of meshes and *exactly* the right triangle count — with every vertex at
  the origin, and no error raised. Triangle counts therefore prove nothing.
  `gltf.validate_decoded` checks that the geometry occupies space, which is the only cheap
  check that distinguishes the two.
- **Some models are simply authored inside out.** glTF fixes winding and normals together,
  so a model's two must agree; SOHO's disagree on 100% of faces, and no amount of correct
  importing changes that. Which half is wrong is settled by the enclosed signed volume,
  which reads the geometry and no normals at all: SOHO encloses +0.0149 of its bounding box
  cubed, squarely among the 23 models needing no repair, so its winding is right and its
  normals are inverted. `gltf.orient_outward` negates them, says so in the build log, and
  refuses to guess where the volume is near zero because the model is open.
- **A failed import must not leave the previous answer behind.** Meshes are written to a
  temporary name and moved into place only when complete; any failure deletes the target
  outright and says so; an adopted mesh is re-validated against its own header before being
  trusted; `--only` merges into the existing catalogue instead of replacing it; and the run
  reports any mesh no mission claims. That is how the inside-out SOHO nearly shipped after
  the check had already caught it.
- **Model scale is not recorded in the files.** The exports carry no consistent unit, so
  each model's longest axis is scaled to a published dimension from the mission table. The
  dimension is a measurement; that the longest axis *is* that dimension is an assumption
  about the export. The pair is recorded as **Inferred**, naming the dimension used.

Two missions were dropped rather than approximated. New Horizons has a JPL ephemeris but no
model in the repository, and substituting another probe's body would have put something
that is not New Horizons at New Horizons' position under its name.

**One seam worth naming.** `models/soho.csmesh` ships under NASA 3D Resources' blanket
statement. SOHO/LASCO **imagery** is blocked (§6) because SOHO is an ESA/NASA joint mission
and NASA's public-domain default does not reach it. Different artefacts, different
publishers, different verdicts. The model stays; no SOHO image ships.

---

### 4.5 Surface imagery and textures

| Product | Source | Terms | Staged |
|---|---|---|---|
| Earth, Blue Marble Next Generation 5400×2700 and the shipped 2048×1024 | NASA Visible Earth / Earth Observatory (Terra/MODIS) | US Gov work: *"NASA content — images … used in the rendition of 3-dimensional models, such as texture maps and polygon data in any format — generally are not subject to copyright in the United States."* | `nasa-visibleearth-earth/`, 2,805,446 B |
| Lunar colour 4096×2048 and LOLA elevation 4096×2048 16-bit | NASA SVS CGI Moon Kit (SVS 4720), from LRO LROC + LOLA | same NASA clause; the SVS record says *"These color and elevation maps are designed for use in 3D rendering software."* | `nasa-svs-moon/`, 14,398,203 B |
| Mars Viking global colour; lunar LOLA browse derivatives | USGS Astrogeology (Astropedia) | **Per product.** Mars Viking 925 m: `Access Constraints: public domain` / `Use Constraints: None`. Moon LOLA 118 m: `public domain` / `Please cite authors` | `usgs-astrogeology/`, 157,096 B |
| 15 planetary and ring textures at 2k (Mercury, Venus surface + atmosphere, Mars, Jupiter, Saturn, Uranus, Neptune, Sun, Earth day/night/clouds, Moon, Milky Way, Saturn ring alpha) | **Solar System Scope** | **CC BY 4.0.** *"You may use, adapt, and share these textures for any purpose, even commercially."* | `solarsystemscope-textures/`, 7,579,995 B |

- **Obligations:** credits C10, C11, and **C9 which is mandatory** — CC BY 4.0 requires both
  attribution and an indication that changes were made. Resampling and recompression are
  changes; C9 says so.
- **Pitfall, and it is a live hazard in this project.** Solar System Scope publishes
  *invented* maps alongside its real ones — `2k_ceres_fictional.jpg`,
  `2k_eris_fictional.jpg`, `2k_haumea_fictional.jpg`, `2k_makemake_fictional.jpg` — whose
  filenames differ from the real maps by one suffix. Their licence is fine; their **content
  is artistic invention**. `tools/cosmodata/fetch_surface_maps.py` promotes a body from
  Procedural to **Measured** on the strength of the file decoding as an image with roughly a
  2:1 aspect. Hand it `2k_ceres_fictional.jpg` and it will validate, pass, and label an
  invented surface as measured. **None is staged, deliberately.** Ceres has real Dawn
  Framing Camera imagery via USGS Astrogeology; use that. The importer must refuse
  `*_fictional*` outright, and authored surfaces need an ingest path that cannot set
  `Measured`.
- **Not yet confirmed:** the exact scale and offset of the SVS lunar DEM (`ldem_16_uint.tif`)
  is stated on the SVS page, not in the TIFF header. A wrong offset yields a
  plausible-looking Moon of systematically wrong radius. Confirm before baking.

---

### 4.6 Solar activity and solar imagery — NOAA SWPC, NOAA NCEI, NASA SDO, NOAA GOES

- **Terms (all NOAA products):** *"The information on National Weather Service (NWS) Web
  pages are in the public domain, unless specifically noted otherwise, and may be used
  without charge for any lawful purpose so long as you do not: 1) claim it is your own …
  2) use it in a manner that implies an endorsement or affiliation with NOAA/NWS, or
  3) modify its content and then present it as official government material."* —
  weather.gov/disclaimer.
- **Terms (SDO):** *"SDO images and movies are not copyrighted unless explicitly noted."* —
  sdo.gsfc.nasa.gov/data/rules.php.
- **Staged:** `noaa-swpc-solar/` (12 files, 5.19 MB, shared between two clearance families),
  `noaa-ncei-penticton-f107/` (925,710 B, daily F10.7 1947-02-14 → 2018-04-30),
  `nasa-sdo-imagery/` (796,133 B, five 1024×1024 stills), `noaa-goes-suvi-ccor1/`
  (1,998,283 B).
- **Obligations:** credits C12, C13, C14. **C13 is not decoration** — the NWS disclaimer
  invokes 17 U.S.C. § 403: third parties producing copyrighted works consisting
  predominantly of NWS material must identify it and state that it is not subject to
  copyright protection.

**The column trap, in full, because it is the most important finding in the solar family.**
`services.swpc.noaa.gov/json/solar-cycle/observed-solar-cycle-indices.json` is served from a
public-domain host. Its first row reads
`{"time-tag":"1749-01","ssn":96.7,"smoothed_ssn":-1.0,"observed_swpc_ssn":-1.0, …}`. SILSO's
`SN_m_tot_V2.0.txt` first row reads `1749 01 1749.042 96.7 -1.0 -1`. Identical value,
identical start month; NOAA did not observe sunspots in 1749; and NOAA's own
`observed_swpc_ssn` column is `-1.0` for every row before 1997-01, when SWPC's record
begins. The `ssn` and `smoothed_ssn` columns are the SILSO International Sunspot Number V2,
**CC BY-NC 4.0**.

- **Safe to ship:** `observed_swpc_ssn`, `smoothed_swpc_ssn`, `f10.7`, `smoothed_f10.7`,
  and the whole of `swpc_observed_ssn.json`, `f10-7cm-flux.json`, `f107_cm_flux.json`.
- **What is lost:** 1749–1996 of sunspot history, 248 years. The honest replacement is the
  **10.7 cm radio flux**, measured at Ottawa from 1947-02-14 and Penticton from 1991-06-01
  by the National Research Council Canada and archived by NOAA. A real measurement with a
  79-year baseline and no licence problem, and a better answer than a shorter sunspot
  series. Drive the Sun's visual activity from F10.7.

**Pitfalls:**

- The `rtsw_*` spacecraft changes per record — ACE and IMAP appeared in the same staged
  minute. Three missing-value sentinels coexist in one directory: `null`, `-1.0`, `-999`.
- The `products/solar-wind/*-7-day.json` endpoints an older CosmoSynth page used are all
  **dead (404)**. Live replacements: `json/rtsw/rtsw_wind_1m.json`, `json/rtsw/rtsw_mag_1m.json`.
- **No daily-resolution F10.7 exists in the cleared set for 2018-05 → ~2026-05.** The NCEI
  daily archive ends 2018-04-30 and the SWPC `warehouse/YYYY/YYYY_DSD.txt` and
  `ftpdir/indices/old_indices/` paths cited in older documentation all 404 today. The
  *monthly* series covers the period continuously, so the cycle envelope has no gap; only
  the daily texture does.
- The SDO and GOES images are `latest_*` snapshots, not dated products — a re-fetch returns
  a different Sun. **Ship the bytes, not the URL.** Reproducibility rests on the staged
  sha256.
- SDO images are *browse* products: never derive a measured number from their pixels
  (rule 4 of the SDO rules page).
- **NOAA's authority to redistribute the Canadian Penticton series is INFERRED, and it is
  the weakest link in this family.** No page states, in NOAA's or NRC's words, the terms of
  that redistribution. What is verified: NOAA states no restriction, NCEI has served it
  publicly for decades, and NOAA's own material is public domain. The distinction from the
  SILSO trap is that SILSO attaches a *named licence* that travels with the data, whereas
  Canada attaches none and canada.ca's clause is a website terms-of-use. One email to NRC
  settles it.

---

### 4.7 Milky Way dust — Edenhofer 2024 and Leike 2020 (both CC BY 4.0)

| Map | Terms | Staged |
|---|---|---|
| **Edenhofer et al. 2024**, 1.25 kpc sphere at 10 pc, Zenodo 8187943 | Record rights field `"license": "cc-by-4.0"` | `edenhofer2024-3d-dust/` — 256³ log-u8 cube (16,777,216 B), nside-128 column map (786,432 B), shell bounds, meta |
| **Leike, Glatzle & Ensslin 2020**, 740×740×540 pc at 2 pc, Zenodo 3993082 | Stated three times: the Zenodo record, the data's own `readme.txt` (*"Provided under the terms of the Creative Commons Attribution 4.0 International License (CC BY 4.0)."*), and inside `mean_std.h5`'s root `information` attribute | `leike2020-3d-dust/` — 370×370×270 log-u8 cube (36,963,000 B) |
| **Bayestar19** (Green+ 2019), Harvard Dataverse | CC0 1.0, `"termsOfUse": null` — cleared, **deliberately not staged** | `bayestar19-3d-dust/` — licence and reduction recipe only |

- **Obligations:** credits C15 and C16, each including the "resampled and quantised"
  sentence — CC BY 4.0 requires that changes be indicated.
- **Units differ and mixing them silently is invisible.** Leike is **G-band optical depth
  per pc**, not magnitudes. Radcliffe Wave positions are in **pc**; Rice clouds are in
  **kpc**. Mixing them renders a structure 1000× too small.
- **`v == 0` means *unknown*, not *empty*.** Mask `r < 1248 pc` on the Edenhofer cube.
- **Both cubes were checked against the sky, not just parsed.** Ten named molecular clouds
  sampled along their own sightlines peak within 1–2 voxels of their published Zucker+2020
  distances (Orion A 432 → 430 pc, Perseus 294 → 290, Taurus 141 → 150, Ophiuchus 139 →
  150); Cygnus X at 1300 pc correctly falls outside the map sphere; the Local Bubble cavity
  is present at 5.6× contrast and disk flattening at 32×. Scripts
  `data-staging/scripts/{eden,leike}_verify.py` assert and exit non-zero, so they are
  regression tests rather than printouts.
- **Why not Vergely+2022**, which earlier research recommended: its CDS ReadMe states no
  licence at all, and VizieR's own terms grant *"free of usage in a scientific context"*
  only. Blocked. Lost: mapped volume beyond 1.25 kpc, and convenient mag/pc units.

---

### 4.8 Galactic structure — LVDB, Rice 2016, Zucker 2020, Radcliffe Wave, Reid 2019

All CC0 1.0 except the arm model, which is facts.

| Data | Source | Terms | Staged |
|---|---|---|---|
| 213 Milky Way globular clusters (155 Harris-set + 58 new) and 313 dwarf-hosted + 22 ambiguous | **Local Volume Database** (Pace 2025), pinned at tag `v1.1.1` | `LICENSE`: *"Creative Commons Legal Code / CC0 1.0 Universal"*; Zenodo licence field *"Creative Commons Zero v1.0 Universal"*, DOI 10.5281/zenodo.21910097 | `lvdb-globular-clusters/`, 287,991 B |
| 1,064 Galactic molecular clouds with positions, kinematic distances, sizes, masses | **Rice et al. 2016**, Harvard Dataverse doi:10.7910/DVN/EWY90X | `"license": {"name": "CC0 1.0"}`, `"termsOfUse": null` | `rice2016-molecular-clouds/cloud_catalog.fits`, 486,720 B, 1,064 × 56 |
| 94 named clouds, 326 sightlines, d50 108–2558 pc | **Zucker et al. 2020**, doi:10.7910/DVN/07L7YZ | same CC0 record field | `zucker2020-cloud-distances/`, 55,870 B |
| Radcliffe Wave best-fit spine + member clusters | **Konietzka et al. 2024**, doi:10.7910/DVN/F98QHY and /XSCB9N | same CC0 record field | `radcliffe-wave-konietzka2024/`, 58,141 B |
| Spiral-arm geometry | **Reid et al. 2019**, ApJ 885, 131 | **Facts, cited.** Implement the log-spiral model from the published fit parameters; redistribute nothing. The VizieR table copy is blocked | `reid2019-spiral-arms/` — documentation only |

- **Obligations:** credits C17, C18, C19, C20. CC0 imposes none; ship them anyway, because
  they are what the accuracy claim rests on.
- **Use LVDB's `M_V` column, never `m_V − μ`.**
- **Honour Rice's `KDA_resolution`** — kinematic distances carry the near/far ambiguity, and
  `'U'` means unresolved.
- **Reid's Table 2 is now transcribed, and here is where from.** Lane `data-milkyway`
  fetched the arXiv e-print of ApJ 885, 131 (`https://arxiv.org/e-print/1910.03357`,
  2,657,580 B, one LaTeX source file) and copied the `deluxetable` labelled
  `table:pitchangles` — the paper itself, not a VizieR or ADS scrape and not model recall.
  The seven arm segments, their azimuth ranges, kink azimuths, kink radii with uncertainties,
  both pitch angles and the fitted widths are in
  `unity/Assets/CosmoSynth/Core/Deep/GalaxyModel.cs`, with R₀ = 8.15 ± 0.15 kpc and
  Θ₀ = 236 ± 7 km/s from the same paper's fit B, and the arm-width relation
  w(R) = 336 + 36 (Rₕₚₜ − 8.15) pc from its section 4. Disc and bar numbers
  come the same way from the e-print of Bland-Hawthorn & Gerhard 2016, ARA&A 54, 529
  (`https://arxiv.org/e-print/1602.07702`): thin disc 2.6 ± 0.5 kpc / 300 ± 50 pc,
  thick disc 2.0 ± 0.2 kpc / 900 ± 180 pc, bar angle 27 ± 2°, long-bar
  half-length 5.0 ± 0.2 kpc. Nothing is redistributed but that file, which is the whole
  point of the distinction. `GalaxyModelTests` spot-checks the transcription row by row.

**The Cepheid hole, stated plainly.** This is the one place where the commercial constraint
costs real science. **No commercial-clean Galactic Cepheid catalogue exists** — five
candidates checked: Skowron 2019 (its repo licence explicitly excludes *"scientific
data"*), Gaia DR3 (CC BY-NC), OGLE's own site (no terms at all), any VizieR copy
(scientific use only), ASAS-SN (citation requirements, no licence). What replaces the layer
is the four CC0 rows above. What is lost precisely: per-star **ages** (Skowron gives
3–1169 Myr per Cepheid; nothing in the replacement carries a stellar age, so "young disk
coloured by age" is not reproducible); **the warp traced by individual stars** (Skowron's
Cepheids reach 121 kpc with |z| to 11.8 kpc; CO clouds sit near the plane and show arms,
not the warp); **distance precision** (period–luminosity is good to a few per cent,
kinematic distances are not); and **the visual register** — 2,214 point sources read as
"the Galaxy is made of stars", 1,064 extended clouds read as "the Galaxy is made of gas".
The fiction layer should know which one it is bridging from.

Globular clusters moved off Harris and Baumgardt at no scientific cost: Harris permits
redistribution only *"WITHOUT CHARGE OF ANY FEE"*, which a paid app cannot meet, and
Baumgardt states no licence at all. LVDB carries 155 of Harris's 157 objects plus 58 newer
clusters with `M_V`, `rhalf`, age and a per-value reference.

---

### 4.9 Deep sky — OpenNGC (CC BY-SA 4.0, conditional)

13,970 NGC/IC rows (12,011 with a major axis, 131 with a common name) for deep-sky names
and angular sizes.

- **Terms:** *"Unlike other similar databases which are released with license limitations,
  OpenNGC is released under CC-BY-SA-4.0 license, which allows the use for a wider range of
  cases."* — repo README. (Note `…/master/LICENSE` is a 404; the licence is in `LICENSES/`.)
- **Staged:** `openngc-deepsky/` — `NGC.csv` 3,876,622 B, `addendum.csv`, the licence text,
  the README.
- **Obligations:** credit C21, and the derived deep-sky table inherits share-alike exactly
  as the constellation file does (§4.2).
- **Recorded risk, unresolved.** OpenNGC's README names NED, HyperLEDA, SIMBAD, HEASARC,
  Corwin and *"internet sources like Wikipedia"* as inputs — and NED, HyperLEDA and SIMBAD
  each block redistribution on their own pages. Verga asserts CC BY-SA over his own
  compilation, which is the strongest available position, and positions and angular sizes
  are facts; selection and arrangement is what a compiler owns. Not verified that every
  upstream holder agreed. If the project decides it wants zero share-alike anywhere, the
  alternative is to drop NGC/IC names and sizes and label objects from Zucker (CC0) plus
  IAU designations.
- **Pitfall:** filter the 651 `Dup` rows and the star types.

---

### 4.10 Local Volume — LVDB dwarfs, 2MASS via IRSA, and a derived hero table

| Data | Source | Terms | Staged |
|---|---|---|---|
| 911 nearby dwarf galaxies: position, distance modulus, half-light radius, ellipticity, PA, `M_V`, host | **LVDB v1.1.1** `dwarf_all.csv` | CC0 1.0 (repo LICENSE at the tag + Zenodo field) | `lvdb/dwarf_all.csv`, 433,436 B |
| 656 large galaxies: Hubble type, axis ratio **and position angle**, isophotal and half-light radii, JHKs magnitudes | **2MASS Large Galaxy Atlas v2** via IRSA TAP (`lga_v2`), DOI 10.26131/IRSA122 | US Government Works — data.gov licence field `https://www.usa.gov/government-works` | `2mass-lga-irsa/lga_v2.csv`, 76,788 B |
| Axis ratio and PA for extended sources: 198,647 rows (`r_ext > 20″`) of 1,647,599, 15 of 393 columns, reduced server-side in ADQL | **2MASS XSC** via IRSA TAP (`fp_xsc`) | same data.gov licence field | `2mass-xsc-irsa/xsc_rext20.csv`, 18,411,909 B |
| 14 J+H+Ks colour mosaics of hero galaxies | 2MASS LGA imagery | *"The images in this gallery are released into the public domain."* + IPAC: *"images and video on the IPAC public web sites may be used for any purpose without prior permission"* | `imagery-2mass-lga/`, 24,831,431 B |
| 31 Local Volume giants: shape/size/magnitude/type **Measured** from the LGA, distance **Inferred** from the median of that host's LVDB satellites (with N and MAD carried); LMC and SMC straight from LVDB | **Derived — ours** | CC0 (a derivative of CC0 + US Government Works only) | `heroes-derived/heroes.json`, 39,066 B, plus `derive_heroes.py --check` |

- **Obligations:** credits C22, C23, C24, and **C25 whenever a 2MASS image is displayed**,
  in full, attached to the image.
- **This replaces the Updated Nearby Galaxy Catalog**, blocked twice: via CDS (*"The
  commercial usage of the data is subject to rules depending of the origin"*) and via the
  authors' own host, `sao.ru/lv/lvgdb/`, which carries no terms, copyright or licence of
  any kind. HyperLEDA and NED-D are blocked too, so **every giant distance in the app is now
  Inferred and must be presented as such.** In one respect the replacement is better: the
  UNGC had no position angle at all, which is why the old design bolted a second catalogue
  onto it; the LGA carries the axis ratio and the position angle in the same row, measured
  on the same mosaic, with a second independent fit beside it.
- **Do not generalise IRSA's clearance to NED.** Same institution, opposite rights posture:
  NED's copyright page's entire rights content is *"Copyright (c) 2026, California Institute
  of Technology"* with no grant.

**Three traps this layer defuses, all measured:**

1. **Position angles are not in [0,180).** 2MASS `sup_pa` runs −89 … +117.5; LVDB
   `position_angle` runs −81 … +335 with 7 rows at or above 180 (the LMC is 227.24). Fold
   with `% 180`.
2. **`sup_ba` = 1.000 is a failed fit, not a round galaxy.** NGC 3109, an edge-on Magellanic
   irregular, gets `sup_ba` 1.000 and therefore inclination 0° — face on. 2MASS ellipse fits
   latch onto bright knots in clumpy, low-surface-brightness discs. `heroes.json` carries a
   `shapeReliable` flag that rejects six of 29 — M33, IC 342, NGC 2403, M51, NGC 3109,
   NGC 1313 — precisely the big, face-on, clumpy ones. Where it is false, ship the row and
   flag the shape unknown.
3. **Inclination is meaningless for ellipticals and S0s.** Cen A comes out at 17° from its
   axis ratio; that is a projected isophote, not a disc tilt. Gate inclination on Hubble type.

**LVDB blanks are blanks, not defaults.** 682 of 911 position angles and 536 of 911
ellipticities are **empty** in LVDB v1.1.1. An earlier derived file substituted values
downstream and drew them as measured. Keep blanks blank and set `EllUnknown`/`PaUnknown`.
Crossmatching all 911 dwarfs against the staged XSC at 30″ finds 92 counterparts, filling
74 PA blanks and 64 ellipticity blanks — 11% and 12%. The remaining ~600 are below 2MASS's
reach and must stay unknown.

**Also cleared for future use, nothing staged:** ESA/Hubble, ESO and NOIRLab imagery, all
CC BY 4.0 with commercial use permitted, a **visible per-image credit with the wording
unaltered and attached to the image**, and no implication of endorsement.

---

### 4.11 Large-scale structure — DESI DR1, SDSS, VAST voids

| Data | Source | Terms | Staged |
|---|---|---|---|
| 5,481,299 rows: BGS_BRIGHT, BGS_BRIGHT-21.5 and QSO clustering catalogues, NGC + SGC, reduced to RA/DEC/Z/flux_g/flux_r/WEIGHT | **DESI DR1** | **CC BY 4.0** — *"Users are free to share, copy, redistribute, adapt, transform, and build upon the DESI data available through this website for any purpose, including commercially…"* | `desi-dr1-lss/`, 143,709,203 B (source FITS deleted after reduction) |
| 492,189 galaxies (0.003 < z < 0.12) **with shapes** and 747,982 quasars | **SDSS DR18** via SkyServer | **Public domain** — *"All SDSS data released in our public data releases are considered in the public domain."* | `sdss-dr18/`, 75,242,171 B |
| 1,163 VoidFinder voids plus two ZOBOV variants | **VAST void catalogs for SDSS DR7**, Zenodo 7406035 v1.3.0 | **CC BY 4.0** — record licence field `{"id": "cc-by-4.0"}`, `"access_right": "open"` | `vast-voids-sdss-dr7/`, 541,494 B, byte-for-byte as published |
| 30 density peaks (6 named with per-row positional evidence) and the 20 largest interior voids | **Derived — ours**, from the above | carries the DESI and VAST obligations through, adds none | `named-structures/`, 14,876 B |

- **Obligations:** credit **C26 in full and unshortened** (all three parts — acknowledgement
  block, citation, change statement — are licence conditions), C27 (courtesy), and C28.
- **One cosmology, and only one:** flat ΛCDM, H₀ = 67.66 km/s/Mpc (h = 0.6766), Ωm = 0.3150.
  Recovered by inversion from the VAST catalogue itself rather than trusted from a filename:
  searching flat-ΛCDM models against all 531 zobovoids reproduces those values with a
  maximum residual of 0.0000 Mpc/h. Three other Hubble constants appear in the rejected
  sources and none may leak in — CF4's 74.6, EDD's 75.2, and the naive cz/H₀ with 70 used in
  earlier CosmoSynth research. They differ by up to 11%, which is 50 Mpc at the edge of the
  galaxy shell. VAST coordinates are comoving Mpc/h; divide by h for Mpc.
- **All staged positions are redshift-space positions.** Clusters are radially smeared into
  fingers-of-god; sheets are squashed. That is a property of the data, not a bug, and the
  provenance panel should say "redshift space" rather than implying a measured 3-D map.
- **Draw `edge = 0` voids only** — 805 interior, 333 touching the survey boundary, 25
  beyond. Otherwise the survey boundary becomes a shell of fake voids.

**What the commercial filter cost here, and what it bought.** Lost: **all-sky coverage** —
2MRS covered 91% of the sky and is blocked; DESI DR1 covers ~14,000 deg² in two
disconnected caps and SDSS about 11,000 deg², together well under half the sky, with
nothing in the Zone of Avoidance. Lost: **every measured redshift-independent distance**,
because Cosmicflows-4 is blocked in all three of its distribution routes — which also means
**Laniakea has no data boundary**. Bought: 4.3M galaxies instead of 43k, reaching z = 0.5
instead of ~300 Mpc, with 1.2M quasars behind them to z = 3.5, plus 1,163 voids that were
never in the plan.

**Do not fill the gap.** Filling it is exactly what 2M++ did, and it is why 2M++ is
rejected: its own ReadMe records *"Number of fake galaxies in ZoA = 3813"* out of 72,973,
and a viewer cannot tell which. Draw the survey footprint as a visible boundary; an empty
Zone of Avoidance is the truth. For Laniakea, either draw the named attractors as labelled
points and let the galaxy density carry the shape, or draw an **explicitly labelled**
schematic envelope. Never present the second as measured.

**The reusable lesson from this family.** The VAST catalogue's paper is in ApJS, an AAS
journal whose VizieR-mirrored tables are CC BY-NC — but the authors deposited the catalogue
themselves on Zenodo under CC BY 4.0. **Author self-deposit is a different channel from a
journal's data mirror, and it can be permissive where the mirror is not.** When CDS blocks
a catalogue you want, check Zenodo and Harvard Dataverse before giving up.

---

### 4.12 The cosmic microwave background — NASA LAMBDA, WMAP 9-year

**Replaces Planck, which cannot ship from any archive.**

The WMAP 9-year (DR5) Internal Linear Combination temperature map and the KQ85 temperature
analysis mask, baked offline into an equirectangular texture and a cube map under
`StreamingAssets/CosmoSynth/cmb/`. Never fetched at runtime.

- **Source files:** `wmap_ilc_9yr_v5.fits` and
  `wmap_temperature_kq85_analysis_mask_r9_9yr_v5.fits`, both HEALPix NESTED NSIDE 512,
  25,174,080 B each, fetched 2026-09-12 and deleted after baking.
- **Terms:** **CC0.** *"Unless the data file is marked with a restrictive notice or license,
  data that is provided from a NASA-led mission including observations, engineering,
  calibration, and auxiliary data are licensed as Creative Commons Zero . There are no
  restrictions on the usage of these data."* — science.data.nasa.gov/about/license. WMAP is
  NASA-led; both FITS headers were dumped in full and carry no COPYRIGHT/LICENSE card.
  LAMBDA *requests* an acknowledgement (*"There is no cost involved in the use of LAMBDA;
  however, if your research benefits from its use then we request…"*) — a request, not a
  condition.
- **Staged:** `wmap9-ilc-lambda/` (47.7 MB: 4096×2048 equirect as lossless 16-bit PNG and
  as float16 µK, a 6×1024² cube in both forms, the mask, the bake stats, and the rerunnable
  `cmb_cube.py`) and `cmb-constants/cmb_constants.json`.
- **Obligations:** credits C29 and C30.
- **Resolution:** 1° effective (NSIDE 512, 6.87′ pixels), baked at 5.27′ per texel — the
  texture oversamples the map deliberately, so it is not the limiting factor. Degree scales
  only: LAMBDA itself cautions that small-scale structure in the bias correction is
  uncertain below ~10°.
- **Frame:** Galactic on disk, rotated to **ICRS** in the bake so it agrees with the star
  and Milky Way layers. Cube faces follow the OpenGL layout in an ICRS basis
  (`X → RA 0 Dec 0`, `Y → RA 90 Dec 0`, `Z → Dec +90`). Unity is left-handed Y-up, so **the
  app owns the ICRS→Unity basis change and it must be the same one the star layer uses.**
  Mixing Galactic and ICRS is the easiest way to ship a CMB rotated 60° off every star, and
  this is the one thing in the bake that has never been tested in-engine.
- **Encoding:** `µK = px / 65535 × 1000 − 500`. Import as **R16, linear, no sRGB, no
  compression** — a DXT/ASTC block format destroys the point of the map. Max encode error
  0.0077 µK against a 71 µK signal.
- **Verified orientation, three checks with a control:** the |T| profile binned by Galactic
  latitude peaks in the [0°, 2°] bin at 70.79 µK, falling to 50–55 µK above |b| = 30° —
  computed from the published NGP position, not from the rotation matrix the bake used, so
  it audits the rotation rather than assuming it. The CMB Cold Spot reads −105.58 µK inside
  a 5° disc against a 71 µK sky rms. Cube and equirect correlate at 0.99649 over 200,000
  random directions, while a deliberately scrambled face order gives −0.00991 — the control
  proves the test can fail.
- **Unit trap, resolved by measurement not by trust.** `TUNIT1` says `mK, thermodynamic` and
  here that is true (scale to µK = 1000), established by measuring the column rms at
  71.06 µK. The Planck file has the **opposite** property — `I_STOKES_INP` declares `K_CMB`
  and is really µK. Neither was taken on trust. **The rms test is what transfers, not the
  answer.** Two more header lies found in passing: `N_OBS` is identically 1.0 in both files
  and is not a hit count, and the mask's column is also called `TEMPERATURE` with
  `TUNIT='mK'` while holding only 0.0 and 1.0.
- **Use the mask.** 25.2% of the sky lies outside KQ85 (kept fraction 0.7480, matching the
  published f_sky ≈ 0.75). Fade, hatch or label the Galactic residual band whenever the app
  claims to be showing "the CMB".
- **Constants:** T_CMB = 2.72548 ± 0.00057 K (Fixsen 2009, COBE/FIRAS) and the CMB dipole
  3.355 ± 0.008 mK toward Galactic l = 263.99 ± 0.14, b = 48.26 ± 0.03 (Hinshaw et al.
  2009). Five measured numbers, not copyrightable, read from the primary papers' running
  text and retyped; the papers themselves are not redistributed. The derived solar speed
  comes out at 369.0 km/s against a published 369.0 ± 0.9 — an independent arithmetic check
  on the pair. The shipped texture has monopole **and** dipole removed, so the app has three
  honest modes and must label which it is in: anisotropy (as-is, ±450 µK), absolute
  (+2.72548 K), and as-seen-from-the-moving-Solar-System (add 3355 µK × cos θ about the
  dipole). Compute the second and third in the shader; do not re-bake.
- **What the substitution costs:** Planck resolves about twelve times finer angular detail.
  At 1° on a Quest 3 — roughly 20 pixels per degree at lens centre — that is invisible, and
  the 4096×2048 bake oversamples WMAP already. **Where it would matter, so the app must not
  do it:** any "zoom into a patch of the CMB", any acoustic-peak or power-spectrum overlay
  claiming Planck precision, and any narration naming Planck. **The app must never say
  "Planck".**

---

### 4.13 Exoplanets, pulsars and gravitational waves

| Data | Source | Terms | Staged |
|---|---|---|---|
| All 6,366 confirmed exoplanets from the `pscomppars` composite table, 35 columns | **NASA Exoplanet Archive**, Caltech/IPAC under NASA contract | No restriction stated on the archive's acknowledgement page (the words *commercial*, *licence*, *redistribute*, *permission* do not occur; `/docs/data_use_policy.html` and `/docs/imageuse.html` both 404); NASA content generally not subject to US copyright | `nasa-exoplanet-archive/pscomppars.csv`, 2,033,259 B |
| 294 gamma-ray pulsars: spin frequency, spin-down rate, position, distance, class, 97 columns | **Fermi LAT Third Pulsar Catalog (3PC)**, NASA/FSSC | NASA GSFC product, no proprietary period: *"all LAT science data has been released as early as possible, typically within a day or two of acquisition"* | `fermi-lat-3pc-pulsars/`, 1,377,721 B |
| 391 confident GW events with source-frame masses, effective spin, luminosity distance, SNR; 32 s of GW150914 H1 and L1 strain | **GWOSC** (LIGO / Virgo / KAGRA) | **CC BY 4.0** — *"Data released under a CC BY 4.0 License"* | `gwosc-gwtc/`, 4,511,569 B |

- **Obligations:** credits C31, C32, C33. C33 includes the modification notice, which
  becomes mandatory the moment the strain is band-passed, whitened, resampled or
  pitch-shifted — and it must be, to be audible.
- **Pulsars: 4,393 → 294.** The ATNF catalogue is blocked. Its download page states no
  licence, the tarball contains no COPYING/LICENSE/NOTICE, the one GPL sentence in
  `psrcat_help.html` §9 grants rights over "PSRCAT" which the package README defines as the
  *program* (*"This package contains the source code of 'psrcat' and the public database
  (psrcat.db)"*), and with no grant CSIRO's site notice governs: *"Except as permitted by
  statute, any other use of copyright material on this website is subject to first receiving
  consent."* Both readings block it — no licence, or GPLv3 copyleft over an APK. **The app
  must say *gamma-ray pulsars*, not *pulsars*.** The 294 it has are the loudest, best-timed
  and most famous, spanning 1.2 Hz to 707 Hz — nine and a half octaves for a sonification
  layer.
- **The honest wrinkle, recorded not waved away.** The 3PC README says *"The sequence of
  input sources is: 1) The ATNF pulsar catalog, 'psrcat' … We used ATNF psrcat v1.68."* The
  substitute is partly derived from the rejected source. What is taken from psrcat are
  measured values — a right ascension, a dispersion measure — and facts are not
  copyrightable; the blocked artefact is the file `psrcat.db` and its selection of 4,393
  objects. 3PC is a different compilation with a selection criterion external to psrcat, it
  overwrites psrcat's distances and proper motions with its own, and it adds ~60 gamma-ray
  columns psrcat never had. This is **not** the SILSO trap in disguise: there, a *named
  licence* travels with the data; here no licence exists and the NASA product is a new work.
  Credit C32 names ATNF anyway; it costs one line. **This is the closest call in the whole
  audit.**
- **Pitfalls.** A TAP query naming a non-existent column returns **HTTP 200** with an Oracle
  error inside a VOTABLE — check the row count, never the status code. `pl_bmasse` is often
  M·sin i, a lower bound, and composite rows are assembled across publications and are not
  guaranteed self-consistent. The 3PC FITS has **empty `TUNIT` keywords for every column** —
  units come from the README, not the file — and `'ATNF'` inside its `Type` field means
  "also in the ATNF catalogue", not a licence marker. `F0` is epoch-dependent: the Crab
  reads 29.7169 Hz, not the textbook 30. **GW events have no single sky position** — they
  are localised to tens or thousands of square degrees; do not invent an RA/Dec, and filter
  `allevents.json` on `catalog.shortName` because it mixes confident detections with
  marginal triggers, third-party reanalyses and a 2010 blind injection.

---

### 4.14 The authored layer, fonts and audio

| Asset | Source | Terms | Staged |
|---|---|---|---|
| UI and label font | **DejaVu fonts 2.37** | Bitstream Vera Fonts Copyright: *"Permission is hereby granted, free of charge … to use, copy, merge, publish, distribute, and/or sell copies of the Font Software"*; *"may be sold as part of a larger software package but no copy of one or more of the Font Software typefaces may be sold by itself."* Read from the LICENSE inside the release tarball | `fonts-dejavu/`, 1,812,285 B; 5,918 codepoints |
| 100 UI sounds | **Kenney Interface Sounds** | CC0 1.0: *"This content is free to use in personal, educational and commercial projects."* Read from `License.txt` inside the pack | `audio-kenney-cc0/`, 834,536 B |
| 32 s seamless ambient bed | **Ours** — `make_ambient.py` | Original work | `audio-original-ambient/`, 2,048,044 B |
| 6 PBR material sets for authored worlds | **ambientCG** | CC0 1.0: *"…even for commercial purposes, all without asking permission."* | `textures-ambientcg-cc0/`, 48,900,160 B |
| 1k HDRI light probe | **Poly Haven** | CC0 1.0: *"You can use our assets for any purpose, including commercial work."* | `textures-polyhaven-cc0/`, 1,745,132 B |
| 3 backdrop plates | **NASA images** | US Gov work; per-image credit read from the API record, not assumed, because images.nasa.gov also hosts ESA and STScI material | `imagery-nebula-nasa/`, 366,636 B |
| Fictional naming scheme | **Ours** — `ninthfield_names.py` | Original work | `fiction-naming-scheme/`, 6,776 B |

- **Obligations:** credit **C39 is required**; C35, C36, C40, C41 are CC0 and optional but
  cost a line each; **C37 is per-image** and NASA requests it.
- **The font is a defect fix, not a preference.** `CosmoText.cs` loads Unity's built-in
  `LegacyRuntime.ttf` (with an `Arial.ttf` fallback on the next line), and there is no font
  file anywhere in the project. That font's **licence is fine** (SIL OFL 1.1) and its
  **coverage is not**: 15 required codepoints are absent, including U+2609 ☉ (solar mass,
  radius, luminosity), U+2295 ⊕ (Earth mass and radius), U+260A/U+260B ☊ ☋ (the orbital
  nodes) and U+2070/U+2071 (exponent digits). **The app cannot render its own units.** On
  device this is a blank box at runtime with no error in any log. Noto Sans and Noto Sans
  Symbols 2 were checked and rejected on fitness — Noto Sans misses 31 required codepoints
  and Symbols 2 misses 153, including U+2640–U+2647, so a fallback chain still fails on
  Saturn. DejaVu Sans covers the whole set with one font and no fallback list. **Fix both
  call sites in `CosmoText.cs`, and add a `font.HasCharacter(c)` play-mode test over the
  required list so a future swap fails loudly.**
- **Freesound and OpenGameArt are blocked as *sources*.** Freesound lets each uploader pick
  one of three licences, one of which is CC BY-NC, and downloading needs an account.
  OpenGameArt accepts CC BY-SA and GPL submissions, and GPL is a real problem for a
  closed-source APK. An individual CC0 file from either remains shippable after per-file
  clearance by a human; what is rejected is treating either site as a CC0 source.
- **Third-party IP that must be renamed before ship**, found by grepping the star-map
  variants and the StarHolo tree against ~70 franchise proper nouns:
  `SYS_MAP // MARVEL_COSMOS` (holographic_star_map.html:44, Marvel), `WARP CORE` ×6 and
  `dilithium` ×5 (StarHolo, Paramount/Star Trek). Replacements: **the Ninth Field** for the
  authored layer, **`NF`** as the catalogue prefix, **drive bay / pair cell** for the warp
  core, **stored antimatter** instead of any invented element, **transit blur** for "warp
  starfield". The other star-map system names are generic descriptive English and borrow
  nothing.
- **World names are generated, not chosen**, and that is the part that actually protects the
  project. A human asked for "alien-sounding" names reaches for the ones they have already
  heard — which is how "warp core" got there in the first place, and nobody did it on
  purpose. `ninthfield_names.py` makes each name a fixed function of a real galactic
  coordinate (invented syllable tables, a BLAKE2b bit stream keyed on l and b, an explicit
  blocklist screen over every output). 7,066 distinct names over 7,080 sky directions, 0.20%
  collisions, and the name **encodes the direction**, so `NF 3203 Riadri` is reversible to a
  point on the real sky the user can fly to.
- **Where the authored worlds live.** The stars with a measured *direction* and no usable
  parallax — the ones the importer already refuses to place — are the Ninth Field's home.
  The fiction then never contradicts a measurement, and the bridge from real to fictional is
  a real feature of the sky rather than a portal.
- **Authored content must never inherit a Measured label.** `ProvenanceClass.Fictional`
  already exists in `Core/Catalog/Provenance.cs` (*"Authored for narrative or teaching
  purposes. Makes no claim about reality."*). Use it. Authored assets need an ingest path
  that *cannot* set `Measured` — a convention that nobody drops a file in the real folder is
  not enough, as §4.5 shows.
- **Not checked, and it matters:** visual IP. No grep finds a station silhouette that reads
  as a Death Star or a HUD that recreates a specific film's interface. Somebody has to look
  at the pictures. (The current star-map HUD is generic orange-on-black, which is a genre
  convention and fine.)

---

## 5. Constants and published facts

Physical constants and measured values are not copyrightable subject matter. They are cited,
not licensed.

| Values | Source | Location in code |
|---|---|---|
| Solar luminosity, effective temperature, solar radius, the astronomical unit | IAU 2015 Resolution B3 nominal values | `Core/Physics/Constants.cs` |
| Gravitational constant *G*, Stefan–Boltzmann constant | CODATA 2018 | `Core/Physics/Constants.cs` |
| Lunar theory; deep-time planetary elements | Meeus ch. 47; JPL approximate elements (Standish) | `Core/Orbits/` |
| Spiral-arm fit parameters | Reid et al. 2019, ApJ 885, 131 | `Core/Deep/GalaxyModel` — **not yet transcribed; see §4.8** |
| T_CMB, CMB dipole | Fixsen 2009; Hinshaw et al. 2009 | `Core/Sky/CmbModel` |
| Rotation elements, radii, GM | IAU WGCCRE 2015 (Archinal et al. 2018) — **via the NAIF PCK, not the Springer table** | `Core/Frames/BodyOrientation.cs` |

Four small bodies carry hand-entered values because Horizons prints small-body masses in a
prose format whose trailing multiplier would be a factor-of-10⁴ silent error to misparse:

| Body | Source |
|---|---|
| Phobos, Deimos | Jacobson (2010), Mars satellite ephemeris MAR097; radii from Willner et al. (2014) and Thomas et al. (2000) |
| Bennu | Scheeres et al. (2019), OSIRIS-REx radio science, *Nature Astronomy* 3:352 |
| 1P/Halley | Giotto/Vega encounter results. Nucleus mass uncertain to ~2×, so this is recorded as **Inferred**, not Measured |

---

## 6. Blocked — do not re-check without a written grant

Each has a rejection dossier under `data-staging/` with the verbatim terms, the URL, the
date read and the substitute. The full register with evidence is
`docs/DATA-LICENCE-AMENDMENT.md` §3.

| Source | Why | Substitute |
|---|---|---|
| **ESA Gaia DR3, Hipparcos, Tycho-1/2, Hipparcos-2, Planck, and every ESA Space Science Archive product** | CC BY-NC 3.0 IGO; commercial use only by written authorisation from `data.licences@esa.int` | UCAC4 + BSC5p (stars); WMAP 9-year ILC (CMB). **One email to ESA would unblock all of them at once** |
| **HYG v4.4 / AT-HYG v4.0** | CC BY-SA 4.0 grant over ESA CC BY-NC astrometry | the four-layer rebuild, §4.1 |
| **VizieR / CDS as a shipping channel** | AAS tables CC BY-NC; A&A tables *"free for a scientific usage"*; MNRAS falls through to OUP, which grants nothing; *"The commercial usage of the data is subject to rules depending of the origin"* | fetch from the producer's own deposit and verify there |
| **SIMBAD** | A citation request; CDS: *"The Information system including the metadata is private and reserved to the CDS."* | build-time research aid only |
| **Reylé+2021 10 pc census** (CDS J/A+A/650/A201) | A&A scientific-use-only | none; drop the census |
| **SILSO sunspot number**, and NOAA's `ssn` / `smoothed_ssn` columns | CC BY-NC 4.0, and republication does not launder it | NOAA F10.7, 1947– |
| **SOHO / LASCO** | *"…public education efforts and non-commercial purposes…"*; commercial use unaddressed | GOES-19 CCOR-1 + GOES SUVI + SDO/AIA |
| **NRCan / spaceweather.gc.ca F10.7 files** | canada.ca bars commercial redistribution without written permission. **F10.7 the quantity is not blocked, only this host's files** | NOAA NCEI + SWPC |
| **ATNF Pulsar Catalogue** | No data licence; CSIRO requires consent | Fermi LAT 3PC |
| **Vergely+2022 dust** (CDS) | No licence in the ReadMe | Edenhofer 2024 + Leike 2020 |
| **Skowron+2019 Cepheids** | Repo licence explicitly excludes *"scientific data"* | Rice 2016 + Zucker 2020 + Radcliffe Wave + analytic arms. **No clean Cepheid catalogue exists** |
| **Harris globulars** | Redistribution only *"WITHOUT CHARGE OF ANY FEE"* | LVDB |
| **Baumgardt globulars** | No licence stated | LVDB |
| **UNGC (Karachentsev+2013)** | Blocked via CDS **and** via the authors' own host, which states no terms at all | 2MASS LGA + LVDB satellite medians |
| **HyperLEDA** | Acknowledgement requested; no licence, no grant | 2MASS LGA + XSC via IRSA |
| **NED / NED-D** | *"Copyright (c) 2026, California Institute of Technology"* and no grant | LVDB satellite medians (Inferred) |
| **2MRS** (CfA and VizieR) | *"Please do not redistribute any of these files."* / CC BY-NC | DESI + SDSS |
| **Cosmicflows-4** (VizieR, IP2I grids, EDD) | CC BY-NC / *"cite the article above"* as the complete terms / no terms page at all | none for measured distances; compute our own density field |
| **6dFGS DR3** | Acknowledgement instructions only | DESI SGC |
| **2M++** | No licence **and** 3,813 of 72,973 rows are fabricated Zone-of-Avoidance galaxies | never use it |
| **Siena Galaxy Atlas 2020** | CC BY 4.0 covers the *images*; the catalogue's terms are stated nowhere | 2MASS LGA |
| **IAU WGCCRE tables as published by Springer** | Values are facts; the compiled table is the publisher's | NAIF `pck00011.tpc` |
| **Freesound, OpenGameArt** as sources | Per-upload licences include CC BY-NC and GPL | Kenney CC0 + our own ambient bed |
| **`sb2580/k3d_dust` snapshot** | GPL-3.0 repo, and the data's provenance is unstated | Edenhofer + Leike |

**ESA has three different rights postures and they must not be generalised in either
direction.** ESA Space Science Archives: CC BY-NC 3.0 IGO. The `esa.int` website and ESA
publications: *"intended for the personal and non-commercial use of its users"* — stricter
still. `esahubble.org` (and ESO, and NOIRLab): **CC BY 4.0**, commercial use permitted with
a visible unaltered per-image credit and no implied endorsement.

---

## 7. In-app credits block

Paste into the about panel. **Generate it from `manifest.json`** so that dropping a layer
drops its credit line and adding one without a credit fails the build. Ship only the entries
whose layer ships. Full text of every string, keyed C1–C41, is in
`docs/DATA-LICENCE-AMENDMENT.md` §5; the short form below is what the panel shows.

```
COSMOSYNTH — DATA SOURCES AND CREDITS

STARS AND CONSTELLATIONS
  Bright-star data: Yale Bright Star Catalogue, 5th Revised Edition (Hoffleit &
  Warren 1991), via NASA HEASARC. This research has made use of data and/or software
  provided by the High Energy Astrophysics Science Archive Research Center (HEASARC),
  which is a service of the Astrophysics Science Division at NASA/GSFC.
  Star positions and proper motions: UCAC4, U.S. Naval Observatory (Zacharias et al.
  2013, AJ 145, 44). Public domain (U.S. Government work).
  Star names: IAU Catalog of Star Names (IAU-CSN), IAU Division C Working Group on
  Star Names (WGSN). CC BY.
  Constellation figures and boundaries: Stellarium sky culture "modern" (Stellarium's
  team), CC BY-SA 4.0 — https://creativecommons.org/licenses/by-sa/4.0/
  Constellation boundary data (c) 2012-2022 Pierre Barbier, CC BY-SA 4.0.

SOLAR SYSTEM
  Ephemerides: JPL Horizons / DE440, NASA/JPL-Caltech.
  Small-body orbits and physical parameters: JPL Small-Body Database, NASA/JPL-Caltech.
  Body orientation and shape: NAIF SPICE PCK pck00011.tpc (NASA/JPL-Caltech), after
  Archinal et al. (2018), IAU WGCCRE 2015 report.
  Planetary and ring parameters: NASA NSSDCA Planetary Fact Sheets (D. R. Williams,
  NASA/GSFC).
  Spacecraft models: NASA 3D Resources (nasa.gov), with contributions from NASA ARC,
  GSFC, JPL, JSC, Ball Aerospace and DigitalSpace Corporation. Geometry simplified and
  recoloured for real-time use. NASA does not endorse this application.

SURFACES AND IMAGERY
  Earth imagery: NASA Visible Earth / NASA Earth Observatory, Blue Marble Next
  Generation (Terra/MODIS), NASA Goddard Space Flight Center.
  Lunar colour and elevation maps: NASA's Scientific Visualization Studio, CGI Moon
  Kit (E. Wright, USRA; N. Petro, NASA/GSFC), from LRO LROC and LOLA data.
  Mars and lunar basemaps: USGS Astrogeology Science Center / NASA Planetary Data
  System.
  Planetary surface textures (c) Solar System Scope (solarsystemscope.com), licensed
  under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). Modified: resampled
  and recompressed for real-time use.

THE SUN
  Solar activity: NOAA/NWS Space Weather Prediction Center. 10.7 cm solar radio flux
  measured at the Dominion Radio Astrophysical Observatory, Penticton, by the National
  Research Council Canada; archived and redistributed by NOAA/NCEI and NOAA/SWPC.
  Solar imagery courtesy of NASA/SDO and the AIA, EVE, and HMI science teams.
  Solar EUV and coronagraph imagery: NOAA/NWS Space Weather Prediction Center, GOES
  SUVI and GOES-19 CCOR-1.
  NOAA/NWS material incorporated in this application is not subject to copyright
  protection.

THE MILKY WAY
  3D dust map: Edenhofer, Zucker, Frank, Saydjari, Speagle, Finkbeiner and Ensslin
  (2024), A&A 685, A82. Data via Zenodo doi:10.5281/zenodo.8187943, CC BY 4.0.
  Resampled and quantised for this application.
  Local 3D dust: Leike, Glatzle & Ensslin (2020), A&A 639, A138. Data via Zenodo
  doi:10.5281/zenodo.3993082, CC BY 4.0. Downsampled and quantised for this
  application.
  Molecular clouds: Rice, Goodman, Bergin, Beaumont & Dame (2016), ApJ 822, 52, from
  the Dame, Hartmann & Thaddeus (2001) CO survey. Harvard Dataverse
  doi:10.7910/DVN/EWY90X, CC0 1.0.
  Molecular cloud distances: Zucker, Speagle, Schlafly, Green, Alves and Goodman et
  al. (2020), A&A 633, A51. Harvard Dataverse doi:10.7910/DVN/07L7YZ, CC0 1.0.
  Radcliffe Wave: Konietzka, Goodman, Zucker, Burkert, Alves et al. (2024), Nature
  628, 62; structure discovered by Alves et al. (2020), Nature 578, 237. Harvard
  Dataverse doi:10.7910/DVN/F98QHY and doi:10.7910/DVN/XSCB9N, CC0 1.0.
  Spiral-arm geometry after Reid et al. (2019), ApJ 885, 131 (BeSSeL Survey maser
  parallaxes). Model implemented from the published fit parameters; no catalogue data
  redistributed.
  Deep-sky objects: OpenNGC by Mattia Verga, https://github.com/mattiaverga/OpenNGC,
  CC BY-SA 4.0. Modified: subset, reprojected and converted. The derived table is
  distributed under CC BY-SA 4.0.

GALAXIES
  This work has made use of the Local Volume Database (Pace 2025, The Open Journal of
  Astrophysics, 8, 142). https://github.com/apace7/local_volume_database
  This publication makes use of data products from the Two Micron All Sky Survey,
  which is a joint project of the University of Massachusetts and the Infrared
  Processing and Analysis Center/California Institute of Technology, funded by the
  National Aeronautics and Space Administration and the National Science Foundation.
  2MASS Large Galaxy Atlas: Jarrett et al. (2003), AJ 125, 525. Dataset DOI
  10.26131/IRSA122.
  This research has made use of the NASA/IPAC Infrared Science Archive, which is
  funded by the National Aeronautics and Space Administration and operated by the
  California Institute of Technology.
  [shown with every 2MASS image] Atlas Image mosaic obtained as part of the Two Micron
  All Sky Survey (2MASS), a joint project of the University of Massachusetts and the
  Infrared Processing and Analysis Center/California Institute of Technology, funded
  by the National Aeronautics and Space Administration and the National Science
  Foundation.

THE COSMIC WEB
  [the DESI acknowledgement block, verbatim and unshortened — see C26]
  DESI Collaboration et al. (2026), "Data Release 1 of the Dark Energy Spectroscopic
  Instrument". DESI DR1 data, CC BY 4.0. Modified from the DESI DR1 clustering
  catalogues: columns subset, converted to comoving positions, and downsampled for
  display.
  Funding for the Sloan Digital Sky Survey IV has been provided by the Alfred P. Sloan
  Foundation, the U.S. Department of Energy Office of Science, and the Participating
  Institutions. SDSS public data-release data are in the public domain.
  Void catalogue: Douglass, Kelly; Veyrat, Dahlia; BenZvi, Segev (University of
  Rochester), "VAST void catalogs for SDSS DR7", version 1.3.0, Zenodo,
  doi:10.5281/zenodo.7406035, CC BY 4.0. Modified: converted from comoving Mpc/h to
  Mpc and subset to interior voids.

THE COSMIC MICROWAVE BACKGROUND
  Cosmic microwave background: WMAP 9-year Internal Linear Combination map and KQ85
  temperature analysis mask. Credit: NASA / WMAP Science Team, via the Legacy Archive
  for Microwave Background Data Analysis (LAMBDA), a service of the Astrophysics
  Science Division at NASA Goddard Space Flight Center.
  CMB temperature 2.72548 K (Fixsen 2009, COBE/FIRAS). CMB dipole 3.355 mK toward
  Galactic (263.99, 48.26) (Hinshaw et al. 2009, WMAP five-year).

PLANETS OF OTHER STARS, PULSARS AND GRAVITATIONAL WAVES
  This research has made use of the NASA Exoplanet Archive, which is operated by the
  California Institute of Technology, under contract with the National Aeronautics and
  Space Administration under the Exoplanet Exploration Program.
  Pulsar data: Fermi LAT Third Catalog of Gamma-ray Pulsars (3PC), NASA/Fermi Science
  Support Center. Smith et al. 2023. Includes parameters collated from the ATNF Pulsar
  Catalogue (Manchester, Hobbs, Teoh & Hobbs, AJ 129, 1993, 2005),
  https://www.atnf.csiro.au/research/pulsar/psrcat
  Gravitational-wave event data: Gravitational Wave Open Science Center (gwosc.org), a
  service of the LIGO Scientific Collaboration, the Virgo Collaboration and KAGRA, used
  under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). Strain data for
  GW150914 from the LIGO Open Science Center. Gravitational-wave strain has been
  filtered and frequency-shifted for presentation.

AUTHORED CONTENT, TYPE AND SOUND
  Fictional systems, names and worlds authored for CosmoSynth (Nebulytic).
  Phenomena: Cosmic Atlas XR (Nebulytic).
  DejaVu Sans, DejaVu Sans Bold and DejaVu Sans Mono. Fonts (c) 2003 Bitstream, Inc.
  Bitstream Vera is a trademark of Bitstream, Inc. Arev glyphs (c) Tavmjong Bah.
  DejaVu changes are in the public domain. Used under the Bitstream Vera Fonts
  Copyright.
  UI sounds by Kenney (kenney.nl), released under CC0 1.0 Universal.
  Surface materials from ambientCG.com, licensed under CC0 1.0 Universal.
  HDRI environment from Poly Haven (polyhaven.com), licensed under CC0 1.0 Universal.
  Ambient score generated procedurally for CosmoSynth.
  Perseus Molecular Cloud — NASA/JPL-Caltech (Spitzer Space Telescope).
  Cat's Paw Nebula — NASA/JPL-Caltech (Spitzer Space Telescope).
  Solstice coronal mass ejection, 20 June 2013 — NASA/GSFC (SDO).

CosmoSynth is not endorsed by, affiliated with, or produced in cooperation with NASA,
JPL, Caltech, NOAA, USGS, ESA, ESO, NOIRLab, or any other agency or institution whose
data it uses.
```

**Prohibitions that travel with the above:** never use the NASA insignia, logotype or seal,
or the JPL logo, in the app, its icon, its splash screen or any store asset; never state or
imply endorsement; never claim copyright in NASA-, NOAA- or USGS-derived material; never
present modified NOAA content as official government material; no NFT use; and every CC BY
source requires the change statement to stay in its credit line.

---

## 8. Reproducing the data

```bash
# The shipped Solar System catalogue (31 bodies, ~85 kB)
python3 tools/cosmodata/build_solar_system.py

# The validation fixture (10 bodies, 260 state vectors, ~104 kB)
python3 tools/cosmodata/fetch_validation_fixture.py

# The spacecraft: trajectories from JPL, models from NASA (24 craft)
python3 tools/cosmodata/build_spacecraft.py
python3 tools/cosmodata/build_spacecraft.py --skip-models   # trajectories only

# The real sky — rebuilt from BSC5p + UCAC4 + IAU-CSN. NOT from HYG; see §4.1.
python3 tools/cosmodata/build_starfield.py

# Everything else, in the order of docs/design/2026-09-11-B-data.md §7
python3 tools/cosmodata/ledger.py --check     # fails the build on any nc/unclear source
```

All are network-bound and take a few minutes. All use only the Python standard library
except `build_spacecraft.py`, which additionally needs `npx` on the path to decompress Draco
geometry, and the dust/CMB bakes, which need numpy.

Generated files carry a `source` field naming the query and the retrieval date, and every
body carries per-field provenance. **Nothing in the pipeline invents a value silently:**
where Horizons publishes no gravitational parameter, the importer **skips the body and says
so** rather than guessing, because a fabricated mass would propagate into every orbit that
depends on it and look entirely plausible while doing so.

The staged evidence for every source above — the raw or reduced data, `SOURCE.txt` with
exact URLs and sha256, `LICENCE.txt` with the verbatim terms, and `NOTES.md` with the
observed format — is under `~/projects/CosmoSynth/data-staging/`, 554 files in 84
directories, 493,252,292 B. Rejected sources have the same three documents and no data, so
nobody re-checks them. The whole inventory was re-verified on 2026-09-12:
`data-staging/_verify/`, 146 of 146 files matching their recorded size and sha256, zero
mismatches.

---

## 9. Corrections to the previous edition of this file

Recorded as corrections rather than quietly overwritten, because the first one survived for
months and cost a whole layer.

1. **The ESA statement was wrong.** The previous edition recorded the Gaia Archive as
   *"CC BY-SA 3.0 IGO with a specific required acknowledgement"*. That is ESA's **media**
   policy for images and videos. The **science archive data** is **CC BY-NC 3.0 IGO**, with
   commercial use only by written authorisation from `data.licences@esa.int`. Share-alike
   would have been inconvenient; NonCommercial is disqualifying, and it blocks Gaia,
   Hipparcos, Tycho-2 and Planck together.

2. **The HYG entry was incomplete.** It recorded HYG as CC BY-SA 4.0 and treated
   share-alike as the only question. astronexus's grant is real, but it sits on ESA
   Hipparcos astrometry that astronexus cannot sublicense. The correct verdict is that
   `sky/stars.csstars` **cannot ship in a commercial build**, and the note's own instinct —
   *"that is a question for a lawyer, and it is exactly the kind of thing this document
   exists to raise before it becomes expensive"* — was right; the answer just arrived from
   upstream rather than from a lawyer.

3. **The NASA 3D Resources per-model claim was not evidenced.** See §4.4. All 24 catalogue
   records carry one identical blanket string and one repository-directory URL, and the
   repository publishes nothing to check against.

4. **Two entries in the "Planned, not yet integrated" table were optimistic and are now
   settled the other way:** SIMBAD (*"Requires the CDS acknowledgement; bulk use has its own
   conditions"*) is **blocked for redistribution**, and NED (*"Caltech/IPAC under NASA
   contract; specified acknowledgement"*) is **blocked** — an asserted copyright with no
   grant. Both remain usable as build-time research aids.

5. **Stellarium's sky-culture data is CC BY-SA 4.0, not GPL.** The repository root `COPYING`
   is GPL-2 and governs the program; `skycultures/modern/description.md` licenses the data.

6. **VizieR is not a neutral mirror.** AAS-journal tables reaching us through CDS are
   CC BY-NC, A&A tables are *"free for a scientific usage"*, and CDS states that *"the
   commercial usage of the data is subject to rules depending of the origin"*. Fetch from
   the producer, and read the **per-catalogue ReadMe**, not only the site policy: VizieR
   ReadMe files carry a greppable `License:` line only when a licence exists, and of 21
   star-relevant catalogues checked, exactly two had one.

7. **The old closing paragraph said "before any commercial distribution, every dataset and
   every derived texture needs an individual licence and attribution audit."** That audit
   has now happened. It is `docs/licence/*.md` and `docs/DATA-LICENCE-AMENDMENT.md`, and
   this file is its output. What remains open is listed in the amendment's §6.4 — fifteen
   items, of which two are single emails (`data.licences@esa.int` and
   `arc-special-proj@lists.nasa.gov`) and one is a legal review that has not occurred.
