# Solar System — commercial licence clearance

**Family**: SOLAR SYSTEM
**Audited**: 2026-09-12
**Constraint being applied**: every shipped byte must be usable in a commercial product.
Earlier design documents assumed non-commercial sideload; where they are wrong, this document
overrides them and says so explicitly.

Every claim below is marked **VERIFIED** (I read the page or ran the command, in this audit,
today) or **INFERRED** (reasoning from something verified). No claim rests on memory, on a
third-party summary, or on what another document in this project says. Where an existing
project document is wrong, that is called out rather than quietly corrected.

---

## 1. Headline findings

Four things changed as a result of this audit. Two of them affect code that already ships.

1. **A non-commercial dataset is reachable through a public-domain host, under an innocuous
   column name, and it is the column a developer would naturally use.** NOAA's
   `observed-solar-cycle-indices.json` carries an `ssn` column that is a verbatim copy of
   SILSO's CC BY-NC series. Proven by value comparison, not assumed. See §4. **This is the
   most important finding in this family.**
2. **`docs/data-sources.md` claims a per-model licence check for the 24 NASA spacecraft
   models that the shipped catalogue does not evidence.** All 24 records carry one identical
   blanket string and one identical repository-directory URL. See §5.
3. **The NASA 3D Resources repository contradicts itself about its own licence** — the README
   says "free and without copyright", `meta.json` says NASA Open Source Agreement 1.3. See §5.
   This is the one open question in the family and is cheap to close with an email.
4. **Solar System Scope publishes four *invented* planetary textures whose filenames differ
   from the real ones only by a `_fictional` suffix** — for Ceres, Eris, Haumea and Makemake.
   In a project built on a real/authored distinction, and with an importer that promotes a
   body to **Measured** purely because a file decodes as an image, that is a live hazard. See
   §6.

Nothing in this family had to be dropped for licence reasons. Every Solar System need is met
by a source cleared below.

---

## 2. Decision table

| # | Source | Verdict | Decisive evidence (verbatim), URL, date read | Substitute |
|---|---|---|---|---|
| 1 | **JPL Horizons** ephemerides | **OK** | "Unless otherwise noted, images and video on JPL public web sites (public sites ending with a jpl.nasa.gov address) may be used for any purpose without prior permission, subject to the special cases noted below." — <https://www.jpl.nasa.gov/jpl-image-use-policy/>, read 2026-09-12. **VERIFIED** | — |
| 2 | **JPL Small-Body Database Query API** | **OK** | Same JPL policy as #1 (`ssd-api.jpl.nasa.gov` is a jpl.nasa.gov address). Response self-identifies: `{"signature":{"version":"1.0","source":"NASA/JPL SBDB (Small-Body DataBase) Query API"}}` — **VERIFIED** by running the query, 2026-09-12. | — |
| 3 | **IAU WGCCRE 2015 rotation elements**, as published by Springer (CMDA 130:22, 2018) | **CONDITIONAL — avoid the file** | The *values* are physical constants and not copyrightable (Feist, 499 U.S. 340) — **INFERRED**. The *article and its tables* are Springer's. No licence permitting redistribution of the tables was found. | **YES → NAIF SPICE `pck00011.tpc`** (#4) |
| 4 | **NAIF SPICE generic PCK** (`pck00011.tpc`, `gm_de440.tpc`) | **OK**, one condition | "Use of SPICE components in commercial products is allowed, subject to the substantial restrictions on user support noted under 'Getting Help from NAIF' above. No fees or licensing are required." and "Redistribution of SPICE kernels distributed by NAIF is permitted as long as they have not been modified." — <https://naif.jpl.nasa.gov/naif/rules.html>, read 2026-09-12. **VERIFIED** | is itself the substitute for #3 |
| 5 | **NASA NSSDCA planetary fact sheets** (incl. the four **ring** fact sheets) | **OK** | "NASA content – images, audio, video, and media files used in the rendition of 3-dimensional models, such as texture maps and polygon data in any format – generally are not subject to copyright in the United States." — <https://www.nasa.gov/nasa-brand-center/images-and-media/>, read 2026-09-12. **VERIFIED** | — |
| 6 | **NASA 3D Resources** — the 24 shipped spacecraft models | **OK to ship**, with one open question | README: "These assets are free and without copyright." — <https://github.com/nasa/NASA-3D-Resources>, read 2026-09-12. **VERIFIED**. Conflicting `meta.json`: `"license": "NASA Open Source Agreement Version 1.3"` — **VERIFIED**. See §5. | — (question to close by email) |
| 7 | **NASA SVS "CGI Moon Kit"** (SVS 4720) — lunar colour + elevation | **OK** | Same NASA clause as #5, which names "texture maps" explicitly. SVS record: "These color and elevation maps are designed for use in 3D rendering software." — <https://svs.gsfc.nasa.gov/api/4720>, read 2026-09-12. **VERIFIED** | — |
| 8 | **NASA Visible Earth / Earth Observatory** — Blue Marble | **OK** | Same NASA clause as #5. <https://www.nasa.gov/nasa-brand-center/images-and-media/>, read 2026-09-12. **VERIFIED** | — |
| 9 | **USGS Astrogeology (Astropedia)** — Mars & lunar basemaps | **OK per product** | Mars Viking Global Color Mosaic 925m: `Access Constraints: public domain` / `Use Constraints: None`. Moon LRO LOLA DEM 118m: `Access Constraints: public domain` / `Use Constraints: Please cite authors`. Read from the product pages 2026-09-12. **VERIFIED** | — |
| 10 | **Solar System Scope textures** | **OK — CC BY 4.0** | "Distributed under Attribution 4.0 International license:" / "You may use, adapt, and share these textures for any purpose, even commercially." — <https://www.solarsystemscope.com/textures/>, read 2026-09-12. **VERIFIED** | — |
| 11 | **NOAA SWPC** solar activity | **OK for NOAA-authored columns only** | "The information on National Weather Service (NWS) Web pages are in the public domain, unless specifically noted otherwise, and may be used without charge for any lawful purpose so long as you do not: 1) claim it is your own … 2) use it in a manner that implies an endorsement or affiliation with NOAA/NWS, or 3) modify its content and then present it as official government material." — <https://www.weather.gov/disclaimer>, read 2026-09-12. **VERIFIED**. **But see §4 — two columns in one file are BLOCKED.** | is itself the substitute for #12 |
| 12 | **SILSO** International Sunspot Number (Royal Observatory of Belgium) | **BLOCKED** | "The World Data Center SILSO has adopted an open data policy (https://www.sidc.be/SILSO/legal) and our sunspot data can be freely downloaded (Licence: CC BY-NC : https://creativecommons.org/licenses/by-nc/4.0)." — <https://www.sidc.be/SILSO/datafiles>, read 2026-09-12. **VERIFIED** | **YES → NOAA SWPC** (#11) |
| 13 | **ESA Space Science Archives** | **BLOCKED** | "Data hosted in the ESA Space Science Archives are distributed under the CC BY-NC 3.0 IGO licence." and "Prior to any commercial use by the User of any Data or Data Product, including any use or application that directly or indirectly generates a financial gain, a detailed request for authorisation/licence shall be made by the User by sending email to data.licences@esa.int." — <https://www.cosmos.esa.int/web/esdc/terms-and-conditions>, read 2026-09-12. **VERIFIED** | not needed by this family |
| 14 | **VizieR / CDS** copies of AAS tables | **UNVERIFIED → treat as blocked** | Three candidate CDS rights URLs returned no readable licence text to the fetcher on 2026-09-12. The prior CC BY-NC claim is **neither confirmed nor refuted**. **VERIFIED** that the check failed; the licence itself is **not** verified. | not needed by this family |

### On the brief's two "verify, do not assume" items

- **ESA Space Science Archive → non-commercial: CONFIRMED.** The clause is real, explicit, and
  reaches "any use or application that directly or indirectly generates a financial gain".
- **VizieR copies of AAS-journal tables → NOT CONFIRMED.** I could not reach a readable CDS
  rights page. I am recording this as unverified rather than passing the prior claim through
  as a finding. It does not block this family because nothing here needs VizieR. The
  star/galaxy families must do this properly, and must read the **per-catalogue ReadMe**, not
  only the site-level policy.

---

## 3. What is already shipping, and whether it passes

Read from `unity/Assets/StreamingAssets/CosmoSynth/` on 2026-09-12. **VERIFIED**.

| Shipped asset | Source | Verdict |
|---|---|---|
| `catalog/solar_system.json` (88 kB, 31 bodies) | JPL Horizons | **PASS** |
| `catalog/spacecraft.json` (979 kB, 24 craft) | JPL Horizons trajectories | **PASS** |
| `models/*.csmesh` (24 files, ~2.0 MB) | NASA 3D Resources | **PASS**, but see §5 — the *documentation* of this asset is wrong, not the asset |
| `surfaces/earth.jpg` (2048×1024, 239 kB) | NASA Visible Earth, `land_shallow_topo_2048.jpg` | **PASS** — re-fetched and hash-recorded this audit |
| `surfaces/moon.jpg` (1024×512, 139 kB) | NASA SVS 4720, `lroc_color_poles_1k.jpg` | **PASS** |
| `surfaces/surfaces.json` | provenance record | **PASS** |
| `sky/stars.csstars` | HYG database | **OUT OF FAMILY — CONDITIONAL.** CC BY-SA 4.0 per `docs/data-sources.md`. Commercial use is allowed; the derived file inherits share-alike. Flagged in §8, owned by the star family. |
| `catalog/exoplanets.json` (330 kB) | NASA Exoplanet Archive | **OUT OF FAMILY** — belongs to the exoplanet family. Not audited here. |

**No shipped Solar System asset fails the commercial bar.** One shipped asset's
*documentation* fails (§5), and one shipped asset outside this family is conditional (§8).

### The NASA insignia question, and why it cannot bite — **VERIFIED**

NASA carves its logos out of the public domain explicitly: *"The NASA Insignia, Logotype,
identifiers, and imagery are not in the public domain. The use of the Insignia, Logotype and
NASA identifiers is protected by law"* (<https://www.nasa.gov/nasa-brand-center/images-and-media/>,
read 2026-09-12). Spacecraft models routinely carry the meatball as a texture.

The `.csmesh` format cannot carry it. From `tools/cosmodata/meshpack.py` and confirmed against
the header bytes of the shipped `cassini.csmesh`:

```
magic "CSM1" | flags | vertexCount | indexCount | submeshCount | boundsMin | boundsMax | lengthM
vertices  : position 3×f32, normal 3×f32
indices   : u16 or u32
submeshes : start u32, count u32, rgba 4×f32, metallic f32, smoothness f32
```

**There are no UVs and no texture maps in the format at all** — each submesh carries one flat
RGBA colour. No image from any source model is shipped, so no insignia can be present.

**This conclusion expires the moment spacecraft become textured.** If that happens, each
model's textures must be inspected for the insignia before shipping.

---

## 4. The laundered non-commercial dataset — read this before touching solar data

**VERIFIED by running both sources on 2026-09-12.**

NOAA's `services.swpc.noaa.gov/json/solar-cycle/observed-solar-cycle-indices.json` is served
from a public-domain US Government host. It has six columns. Two of them are not NOAA's work:

```
NOAA  observed-solar-cycle-indices.json, first row:
  {"time-tag":"1749-01","ssn":96.7,"smoothed_ssn":-1.0,"observed_swpc_ssn":-1.0, ...}

SILSO SN_m_tot_V2.0.txt, first row:
  1749 01 1749.042   96.7  -1.0    -1
```

Identical value. Identical start month. NOAA did not observe sunspots in 1749. And in the same
NOAA file, NOAA's own column `observed_swpc_ssn` is `-1.0` (absent) for **every row before
1997-01**, which is when SWPC's own record begins.

The `ssn` and `smoothed_ssn` columns are the SILSO International Sunspot Number V2, which is
**CC BY-NC 4.0**. A public-domain host does not launder a non-commercial licence.

**Rule for the codebase:** ask where each **column** came from, not where the **file** came
from. Checking the host's licence is not the same as checking the data's licence.

**Safe to ship:** `observed_swpc_ssn`, `smoothed_swpc_ssn`, `f10.7`, `smoothed_f10.7`, and the
whole of `swpc_observed_ssn.json`, `f10-7cm-flux.json`, `f107_cm_flux.json`.

**What is lost:** 1749–1996 of sunspot history — 248 years. The honest replacement is the
**10.7 cm radio flux**, measured at Ottawa from 1947-02-14 and Penticton from 1991-06-01 by
the National Research Council Canada and archived by NOAA (<https://www.ngdc.noaa.gov/stp/solar/flux.html>,
read 2026-09-12, **VERIFIED**). It is a genuine measurement with a 79-year baseline and no
licence problem — a better answer than a shorter sunspot series. Drive the Sun's visual
activity from F10.7.

If the 1749 record is genuinely wanted, SILSO invite contact at `silso.info@oma.be`. That is a
business decision, not an engineering one, and until a licence exists in writing the series is
blocked.

---

## 5. NASA 3D Resources — two corrections to `docs/data-sources.md`

### Correction 1 — the repository contradicts itself. **VERIFIED.**

| File | Statement |
|---|---|
| `README.md` | "These assets are free and without copyright." |
| `meta.json` | `"license": "NASA Open Source Agreement Version 1.3"` |

NOSA 1.3 is a software licence with notice, modification-marking and reciprocity terms. If it
governed the models, shipping them would carry obligations the README says do not exist.

**Assessment (INFERRED):** `meta.json` is the code.nasa.gov catalogue stub — its sibling fields
are `languages`, `operating_systems`, `software_types`, and the repository contains no
software. The README is the asset-specific statement, it is the document the repository
directs users to, and it points onward to NASA's Media Usage Guidelines, which cover *"polygon
data in any format"* explicitly. The README and the Guidelines govern.

**This is a genuine ambiguity in NASA's own metadata, not a resolved question.** It is cheap to
close — the README gives `arc-special-proj@lists.nasa.gov`. **Recommend asking before
commercial release.** It is the one open licence item in this family.

### Correction 2 — the per-model check is claimed but not evidenced. **VERIFIED.**

`docs/data-sources.md` says: *"Some models in that repository are contributed rather than
NASA-authored; the ones shipped here were checked individually, and each catalogue record
carries the source URL and licence string."*

All 24 records in `catalog/spacecraft.json` were dumped. Every one carries the **identical**
pair:

```
"attribution": "NASA 3D Resources (public domain; see NASA media usage guidelines)"
"sourceUrl":   "https://github.com/nasa/NASA-3D-Resources/tree/master/3D%20Models"
```

One blanket sentence, one repository-directory URL. Not a per-model licence, not a per-model
URL. Whatever check was performed, its result is not recorded, so it cannot be re-checked
without redoing it.

**And the repository publishes nothing to check against. VERIFIED:**

- The contributor list is repository-level only, and it does include non-NASA entities:
  **Ball Aerospace**, **DigitalSpace Corporation**, and **Carlo Conti** (listed with no
  organisation).
- The repository does **not** map models to contributors.
- Per-model directories were listed via the GitHub contents API for *Voyager Probe (A)*,
  *Cassini-Huygens (A)*, *Apollo Lunar Module*, *ISS (A)* and *Juno (A)*. Each contains **only**
  a `.glb` and a `.png` preview. **No README, no LICENSE, no per-model attribution file exists
  anywhere in the repository.**

So NASA's blanket "free and without copyright" is the only licence statement in existence, and
NASA makes it over the contributed models too — which is NASA asserting it holds the rights to
give.

**Action:** soften the claim in `data-sources.md` to what is true. The collection carries one
repository-level public-domain statement covering contributed models as well; there is no
per-model licence data to check. Claiming a check that left no record is worse than claiming
none, because it stops the next person from doing it.

---

## 6. The fictional-texture hazard

**VERIFIED** from the Solar System Scope textures page, 2026-09-12.

The site publishes, alongside its real maps:

```
2k_ceres_fictional.jpg     4k_ceres_fictional.jpg
2k_eris_fictional.jpg      4k_eris_fictional.jpg
2k_haumea_fictional.jpg    4k_haumea_fictional.jpg
2k_makemake_fictional.jpg  4k_makemake_fictional.jpg
```

These are artistic inventions. **None is staged, deliberately.**

Why this matters more here than in most projects: `tools/cosmodata/fetch_surface_maps.py`
promotes a body from **Procedural** to **Measured** on the strength of the file decoding as an
image with roughly a 2:1 aspect. Its own docstring says *"Bodies that get one are re-labelled
from Procedural to Measured"*. Hand it `2k_ceres_fictional.jpg` and it will validate, pass, and
label an invented surface as measured — under a filename one suffix away from the real ones,
in an app whose entire premise is that real and authored content are both present and always
distinguishable.

Ceres has real Dawn Framing Camera imagery via USGS Astrogeology. Use that. If any `_fictional`
texture is ever wanted for an authored world, it must be labelled **Authored**, and it must
never enter through `fetch_surface_maps.py`.

This is the same shape as the verification gap already documented in this project: *the check
passes for a reason unrelated to its name.* "It decoded as a 2:1 image" is not "it is a
photograph of Ceres".

---

## 7. Staged inventory

Root: `~/projects/CosmoSynth/data-staging/`
**44 files, 35,221,533 bytes (35.2 MB)** against an 800 MB budget. **VERIFIED.**

Disk discipline: `df -h /` showed **16.0 GB free before, 15.7 GB after** — the box is at 97%
and shared, so nothing over 150 MB was written in original form, and every large raster was
reduced and its original deleted in the same script run. See §7.3 for what was refused.

### 7.1 Cleared and staged

| Path (under `~/projects/CosmoSynth/`) | Bytes | Rows / pixels | sha256 |
|---|---:|---|---|
| `data-staging/jpl-horizons/mars_vectors_sample.txt` | 7,908 | 126 lines | `9e5eeb3d9ec1507e593de6c1fb03ce9cb29dbea35b1222961f65b5147a478373` |
| `data-staging/jpl-sbdb/asteroids_H_lt_14.json` | 5,177,083 | 32,294 rows | `ef68a4691af9c27bdb708f9780b217ef0fa7ae4fafe5f836ed47d7957f7934f5` |
| `data-staging/jpl-sbdb/tno.json` | 1,132,450 | 7,287 rows | `5865391f7813d2cb3a41a83645a608438a7023982f242a8d7535dc6e0543f173` |
| `data-staging/jpl-sbdb/centaurs.json` | 163,853 | 1,047 rows | `d69bb176da3e4b731ae2f665081f7384b8f478b15d3c7521904d45225d0495d4` |
| `data-staging/jpl-sbdb/trojans_jupiter.json` | 2,552,167 | 16,391 rows | `e7c41e021b9b20591f144b4f7bd5ecdd92df87227dd292d5197c532e5e104f8f` |
| `data-staging/naif-spice-pck/pck00011.tpc` | 131,226 | 4,318 lines | `3dff7b1dbeceaa01f25467767d3fa25816051c85d162d1edf04acb310ee28bb1` |
| `data-staging/naif-spice-pck/gm_de440.tpc` | 12,406 | 302 lines | `924ddf4fb9ead9fe8a1aa55780bcabde40b09d00065d58226e24b68d8092f140` |
| `data-staging/nasa-factsheets/planet_table_ratio.html` | 13,210 | — | `9b0fac568006c536da996bebb81d4eeecf3f073cfccb7eabb9dad13af7ef3eb6` |
| `data-staging/nasa-factsheets/sunfact.html` | 7,232 | — | `bcc186a518298636340e7975bf5a32da660cd656b90081f2e7c8aa7ec2b6561a` |
| `data-staging/nasa-factsheets/earthfact.html` | 5,490 | — | `1b06e3ec31249d063d40d1b38837fac89ddcd2f4b25268b38e8a6d5f413e70ef` |
| `data-staging/nasa-factsheets/marsfact.html` | 13,261 | — | `366b0ce9b69cbee370be904956748af954468a41b93dc71a796a986519aa8d74` |
| `data-staging/nasa-factsheets/jupiterfact.html` | 11,195 | — | `9ac7835ae5387261bc02019a641bbfc8450122ffebebffea5678c3de6c54d0cb` |
| `data-staging/nasa-factsheets/saturnfact.html` | 11,257 | — | `6d09ba010cacef61c8ae1a1ff137b06e5464e99461faf8f32c107419c474d06d` |
| `data-staging/nasa-factsheets/satringfact.html` | 5,566 | Saturn rings | `95d44c887d4fa6f235ec0ba0cce2352c6c542243c05cf66e20cdf86ceeddd957` |
| `data-staging/nasa-factsheets/jupringfact.html` | 3,524 | Jupiter rings | `e362dc46d9ec74e25ce9b40f41e5593945569ae3af8c9d10577999948c4a0e8e` |
| `data-staging/nasa-factsheets/uranringfact.html` | 4,338 | Uranus rings | `dbbe304c6cd6927fbb8ac94b8451ec67f2be56e6e3762bfb5c4c213e2488093e` |
| `data-staging/nasa-factsheets/nepringfact.html` | 4,329 | Neptune rings | `b79b93010089e65eb63aa8437bceca48da71afc9b47de1192040031f30a10b08` |
| `data-staging/noaa-swpc-solar/observed-solar-cycle-indices.json` | 512,287 | 3,332 rows | `18c6f28b246e18967bc367ebe42dd58ab8073e2920c9b6b0acf67fa1c8627695` |
| `data-staging/noaa-swpc-solar/swpc_observed_ssn.json` | 475,254 | 9,846 rows | `0e90d21d8e843ef62c86cc77471145fbd5a53cff2a37b5e1b4866249bf6a3559` |
| `data-staging/noaa-swpc-solar/f10-7cm-flux.json` | 10,876 | 263 rows | `3a379a0b431a71b8409072adaf041fd9b2c9cf77ae1ddf2ed478dce4de35e25b` |
| `data-staging/noaa-swpc-solar/f107_cm_flux.json` | 22,838 | 123 rows | `d3275fb032ff0ac47ef90ff6062b42ae1b46c74ee1438c86433508a458a2e698` |
| `data-staging/nasa-svs-moon/moon_lroc_color_4k.jpg` | 2,312,011 | 4096×2048 | `e0404e7587e71dc2b8cca64692967196bbfa88d431e558488520edb6d08b2f3b` |
| `data-staging/nasa-svs-moon/moon_ldem_4k_uint16.png` | 12,086,192 | 4096×2048 16-bit | `833eadcc1772d37c544c9e659be9fb2f33f00efff762692b5384bb3e73b86e6a` |
| `data-staging/nasa-visibleearth-earth/world.topo.bathy.200412.3x5400x2700.jpg` | 2,566,770 | 5400×2700 | `a9f0088972dee0254610af851c4d6838ca3f2cf79176987e0a5713e2c15ec042` |
| `data-staging/nasa-visibleearth-earth/land_shallow_topo_2048.jpg` | 238,676 | 2048×1024 | `5b54cc586c6cbf2b28762ef4d4011f6cf4227a8b93a637b818a0c54090ce6c2c` |
| `data-staging/usgs-astrogeology/mars_viking_clrmosaic_global_1024.jpg` | 60,229 | 1024×512 | `1368021e0416da0f626a337bd79d8b26e6e67b2b8b49cccb6103dad25b333a9b` |
| `data-staging/usgs-astrogeology/moon_lro_lola_global_ldem_1024.jpg` | 96,867 | 1024×512 | `c168ada16bb6249d9d09474d27e5c7894ef1ef7a686aec5668c56ce935827a83` |
| `data-staging/solarsystemscope-textures/2k_mercury.jpg` | 872,555 | 2048×1024 | `5a5c80607f643496bac9a631e71957def35ed788895f18b678ac849c2b38e48a` |
| `data-staging/solarsystemscope-textures/2k_venus_surface.jpg` | 885,075 | 2048×1024 | `dbe5db1c794a8ab4cbf7dd6bf193540c400fc833ce1e6cc399318aa68026278b` |
| `data-staging/solarsystemscope-textures/2k_venus_atmosphere.jpg` | 229,696 | 2048×1024 | `225012ad4911730605c4e189ca2a3bf674fce50cc48aab4102b936b47d6991ac` |
| `data-staging/solarsystemscope-textures/2k_mars.jpg` | 750,547 | 2048×1024 | `2d187f3e77a98eaa8cea5f4cc722f633c122ef170b9e94ace6b5fb6cbc3f8e01` |
| `data-staging/solarsystemscope-textures/2k_jupiter.jpg` | 498,976 | 2048×1024 | `b0f04d005350252636b0e3396fc592548cbd9e9126b269d32d5c6abd4b0e4f2b` |
| `data-staging/solarsystemscope-textures/2k_saturn.jpg` | 199,916 | 2048×1024 | `54a900ca9bf7ab62e70f862852759abdf342e6d6436a95a2fe9ebdb6bcd3bbac` |
| `data-staging/solarsystemscope-textures/2k_uranus.jpg` | 77,751 | 2048×1024 | `d15239d46f82d3ea13d2b260b5b29b2a382f42f2916dae0694d0387b1204a09d` |
| `data-staging/solarsystemscope-textures/2k_neptune.jpg` | 241,580 | 2048×1024 | `cb42ea82709741d28b0af44d8b283cbc6dbd0c521a7f0e1e1e010ade00977df6` |
| `data-staging/solarsystemscope-textures/2k_sun.jpg` | 822,427 | 2048×1024 | `ff0f076ba65e03b5ab518451bc96699325be38e3ccbdd5869ee1c00f3a0c8816` |
| `data-staging/solarsystemscope-textures/2k_earth_daymap.jpg` | 463,087 | 2048×1024 | `767ee1dc6eb3802699bfccf6f264880f8acd0b80de3191cd24984fe279b07b7c` |
| `data-staging/solarsystemscope-textures/2k_earth_nightmap.jpg` | 255,287 | 2048×1024 | `c16fd1bc096ab91a5c5265c6ff9847c43f489f2e2ee790ccdbcbd03251cf3a5a` |
| `data-staging/solarsystemscope-textures/2k_earth_clouds.jpg` | 965,676 | 2048×1024 | `fffd7f68d41b37274822150e54a6ef605af1d3ec35624d9f628c3b896bfa42ed` |
| `data-staging/solarsystemscope-textures/2k_moon.jpg` | 1,053,869 | 2048×1024 | `2764ba6535ea0481a062846ee033cc7a909dae05b31a8fd13f3e98f3a7fd92bd` |
| `data-staging/solarsystemscope-textures/2k_stars_milky_way.jpg` | 251,454 | 2048×1024 | `ec2240e6f3784d962ab81609b228bfd470e87c86d1fed5e99134d4cd3de24239` |
| `data-staging/solarsystemscope-textures/2k_saturn_ring_alpha.png` | 12,119 | 2048×125 RGBA | `4b0644b2f3ef259fd0bfaedd9dc52f9e7d9738f65f50363e8c4dd19f650d7334` |
| `data-staging/nasa-3d-resources/README.md` | 2,396 | evidence | `2e86b56d48a27803fa32f6fe66d252746c92a875865af162b3a0941435199a12` |
| `data-staging/nasa-3d-resources/meta.json` | 627 | evidence | `ac78885acd9375435ac51889eca5540d3d4e6a63980098abfe0d7626aaa53c03` |

Every directory also carries `SOURCE.txt` (exact URLs, UTC fetch timestamps, sha256, byte
sizes, row/pixel counts, and the query used), `LICENCE.txt` (the verbatim decisive terms, the
page URL, the date read, and the required credit string) and `NOTES.md` (observed format with a
printed header and sample row, units, epoch/frame, pitfalls, and how CosmoSynth should use it).

### 7.2 Rejected — directories exist so nobody re-checks

No data files are staged in these; each carries `SOURCE.txt`, `LICENCE.txt` and `NOTES.md`
recording the verdict, the verbatim quote, the URL, the date, and the substitute.

| Directory | Verdict |
|---|---|
| `data-staging/silso-sunspots/` | BLOCKED — CC BY-NC 4.0. Substitute: `noaa-swpc-solar` |
| `data-staging/esa-space-science-archives/` | BLOCKED — CC BY-NC 3.0 IGO + prior-authorisation clause |
| `data-staging/iau-wgccre-springer/` | Avoid the file. Substitute: `naif-spice-pck` |
| `data-staging/vizier-cds-aas-tables/` | UNVERIFIED — treat as blocked until read properly |

### 7.3 Large products deliberately refused — **VERIFIED by HEAD request, 2026-09-12**

| Product | Size | Decision |
|---|---:|---|
| `Lunar_LRO_LOLA_Global_LDEM_118m_Mar2014.tif` | **8,100.7 MB** | not fetched |
| `Mars_MGS_MOLA_DEM_mosaic_global_463m.tif` | **2,027.3 MB** | not fetched |
| `Mars_Viking_ClrMosaic_global_925m.tif` | **760.9 MB** | not fetched |
| `Mars_Viking_MDIM21_ClrMosaic_global_232m.tif` | ~12 GB (per product page) | not fetched |
| SVS `lroc_color_poles.tif` (27360×13680) | very large | not fetched; took the 4k variant |

The box had 16.0 GB free and is shared with dozens of other agents' builds. These are the
upgrade path, not the staging: fetch one at a time on a box with room, reduce immediately,
delete the original in the same run.

**Reductions actually performed** (recorded in each `SOURCE.txt`):
- `lroc_color_poles_4k.tif` → `moon_lroc_color_4k.jpg`: **no resampling** (source is already
  4096×2048); 16-bit sRGB TIFF re-encoded to 8-bit JPEG q92, original deleted.
- `ldem_16_uint.tif` (5760×2880, 33.2 MB) → `moon_ldem_4k_uint16.png`: decoded,
  Lanczos-resampled to 4096×2048, written as 16-bit PNG, original deleted.

---

## 8. Attribution strings that must appear in the app's credits

Use these verbatim. Only one of them is a legal *requirement* with teeth (CC BY 4.0); the rest
are requested acknowledgements that cost nothing and that NASA, NOAA and USGS all ask for.

```
EPHEMERIDES AND SMALL BODIES
  Ephemerides: JPL Horizons / DE440, NASA/JPL-Caltech.
  Small-body orbits and physical parameters: JPL Small-Body Database, NASA/JPL-Caltech.

BODY ORIENTATION AND SHAPE
  Body orientation and shape: NAIF SPICE PCK pck00011.tpc (NASA/JPL-Caltech),
  after Archinal et al. (2018), IAU WGCCRE 2015 report.

BULK AND RING PARAMETERS
  Planetary and ring parameters: NASA NSSDCA Planetary Fact Sheets
  (D. R. Williams, NASA/GSFC).

SPACECRAFT MODELS
  Spacecraft models: NASA 3D Resources (nasa.gov), with contributions from NASA ARC, GSFC,
  JPL, JSC, Ball Aerospace and DigitalSpace Corporation. Geometry simplified and recoloured
  for real-time use. NASA does not endorse this application.

SURFACE IMAGERY — NASA
  Earth imagery: NASA Visible Earth / NASA Earth Observatory, Blue Marble Next Generation
  (Terra/MODIS), NASA Goddard Space Flight Center.
  Lunar colour and elevation maps: NASA's Scientific Visualization Studio, CGI Moon Kit
  (E. Wright, USRA; N. Petro, NASA/GSFC), from LRO LROC and LOLA data.

SURFACE IMAGERY — USGS
  Mars and lunar basemaps: USGS Astrogeology Science Center / NASA Planetary Data System.
  Mars Viking Global Color Mosaic (Viking Orbiter); Lunar LOLA elevation (LRO).

SURFACE TEXTURES — CC BY 4.0 (THIS ONE IS MANDATORY)
  Planetary surface textures (c) Solar System Scope (solarsystemscope.com), licensed under
  CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). Modified: resampled and
  recompressed for real-time use.

SOLAR ACTIVITY
  Solar activity: NOAA/NWS Space Weather Prediction Center. 10.7 cm radio flux measured at
  the Dominion Radio Astrophysical Observatory, Penticton (National Research Council Canada).
  NOAA/NWS material incorporated in this application is not subject to copyright protection.
```

That last NOAA line is not decoration — the NWS disclaimer asks for it specifically, citing
17 U.S.C. § 403: *"third parties producing copyrighted works consisting predominantly of the
material appearing in NWS Web pages must provide notice with such work(s) identifying the NWS
material incorporated and stating that such material is not subject to copyright protection."*

### Prohibitions that travel with these credits

- **Never** use the NASA insignia ("meatball"), logotype ("worm") or seal, or the JPL logo, in
  the app, its icon, or its store listing. NASA and JPL both require prior written approval,
  and both state the logos are not in the public domain.
- **Never** state or imply that NASA, JPL, Caltech, NOAA, USGS or ESA endorse CosmoSynth.
- **Never** claim copyright in the NASA-, NOAA- or USGS-derived material.
- NASA explicitly does not want its material used for NFTs. Do not.
- If the app credits CC BY 4.0 material, it must also **indicate that changes were made** —
  resampling and recompression are changes. The string above does this.

---

## 9. What I could not verify

Recorded honestly rather than glossed. Each of these is a real gap, not a formality.

1. **The NASA 3D Resources README-vs-`meta.json` conflict is unresolved.** Both statements are
   VERIFIED to exist; which governs is **INFERRED**. Close it by emailing
   `arc-special-proj@lists.nasa.gov`. This is the only open licence item that touches shipped
   assets.
2. **VizieR / CDS terms could not be read.** Three candidate rights URLs returned no licence
   text. The prior "CC BY-NC" claim is neither confirmed nor refuted. Does not block this
   family; **must** be done properly by the star/galaxy families, reading the per-catalogue
   ReadMe as well as the site policy.
3. **The per-model provenance of the 24 NASA spacecraft models cannot be established from the
   repository**, because the repository does not publish a model→contributor mapping and the
   model directories contain no licence files. VERIFIED that the data does not exist; therefore
   *unverifiable*, not merely unverified. The blanket NASA statement is what we rely on.
4. **Whether a model's original textures contained the NASA insignia was not checked** — it did
   not need to be, because `.csmesh` ships no textures (§3, VERIFIED). If spacecraft ever
   become textured this becomes an unverified question that must be answered per model.
5. **The exact scale and offset of the SVS lunar DEM** is stated on the SVS page and is not in
   the TIFF header. Not yet confirmed against the page. A wrong offset yields a plausible-looking
   Moon of systematically wrong radius. Confirm before baking.
6. **`catalog/exoplanets.json` (NASA Exoplanet Archive) was not audited** — out of family. IPAC
   specifies acknowledgement text that must be used verbatim; the exoplanet family owns it.
7. **`sky/stars.csstars` (HYG, CC BY-SA 4.0) was not re-verified** — out of family. Its
   share-alike inheritance is a live commercial question: CC BY-SA permits commercial use, but
   the derived file is an adaptation and would have to be offered under CC BY-SA 4.0. The app
   is not an adaptation of a star catalogue, but the *data file* is. Star family owns it; it
   is the strongest claim on anything currently shipping.
8. **No legal review has happened.** This is an engineering audit with primary sources quoted.
   The Feist and 17 U.S.C. 105 reasoning in §2 rows 1–3 is **INFERRED** and is the kind of
   thing a lawyer should confirm before commercial release.

---

## 10. Ledger rows for `docs/data-sources.md`

Paste-ready. These replace the corresponding rows in the "Currently used" and "Planned" tables,
and the "Planned" entries for USGS, SBDB and Copernicus/solar data can be deleted once these
land.

| Data | Source | Licence | Verdict | Required credit |
|---|---|---|---|---|
| Ephemerides, body constants | [JPL Horizons](https://ssd.jpl.nasa.gov/horizons/) | US Gov work; JPL Image Use Policy — "may be used for any purpose without prior permission" | **OK commercial** | "Ephemerides: JPL Horizons / DE440, NASA/JPL-Caltech." |
| Asteroids H<14, TNOs, centaurs, Trojans | [JPL SBDB Query API](https://ssd-api.jpl.nasa.gov/doc/sbdb_query.html) | US Gov work; same JPL policy | **OK commercial** | "Small-body orbits and physical parameters: JPL Small-Body Database, NASA/JPL-Caltech." |
| Rotation elements, radii, GM | [NAIF SPICE PCK](https://naif.jpl.nasa.gov/pub/naif/generic_kernels/pck/) | "Use of SPICE components in commercial products is allowed… No fees or licensing are required." Kernels redistributable **if unmodified** | **OK commercial** | "Body orientation and shape: NAIF SPICE PCK pck00011.tpc (NASA/JPL-Caltech), after Archinal et al. (2018), IAU WGCCRE 2015 report." |
| ~~IAU WGCCRE tables from the Springer article~~ | ~~CMDA 130:22 (2018)~~ | Springer copyright on the article and its tables | **Avoid the file — use NAIF PCK.** Values are facts; keep citing the paper | — |
| Bulk parameters; **ring systems** | [NASA NSSDCA fact sheets](https://nssdc.gsfc.nasa.gov/planetary/factsheet/) | US Gov work | **OK commercial** | "Planetary and ring parameters: NASA NSSDCA Planetary Fact Sheets (D. R. Williams, NASA/GSFC)." |
| 24 spacecraft models | [NASA 3D Resources](https://github.com/nasa/NASA-3D-Resources) | README: "free and without copyright"; Media Usage Guidelines cover "polygon data in any format". **`meta.json` conflicts (NOSA 1.3) — open question** | **OK commercial**, confirm by email | "Spacecraft models: NASA 3D Resources (nasa.gov)… NASA does not endorse this application." |
| Lunar colour + elevation, 4k | [NASA SVS CGI Moon Kit (4720)](https://svs.gsfc.nasa.gov/4720/) | US Gov work | **OK commercial** | "Lunar colour and elevation maps: NASA's Scientific Visualization Studio, CGI Moon Kit (E. Wright, USRA; N. Petro, NASA/GSFC), from LRO LROC and LOLA data." |
| Earth colour, 5400×2700 | [NASA Visible Earth](https://visibleearth.nasa.gov/) | US Gov work | **OK commercial** | "Earth imagery: NASA Visible Earth / NASA Earth Observatory, Blue Marble Next Generation (Terra/MODIS), NASA Goddard Space Flight Center." |
| Mars & lunar basemaps | [USGS Astrogeology](https://astrogeology.usgs.gov/search) | **Per product.** Mars Viking 925m: `public domain` / `Use Constraints: None`. Moon LOLA 118m: `public domain` / `Please cite authors` | **OK commercial per product** | "Mars and lunar basemaps: USGS Astrogeology Science Center / NASA Planetary Data System." |
| Planet & ring textures, 2k | [Solar System Scope](https://www.solarsystemscope.com/textures/) | **CC BY 4.0** — "even commercially". No share-alike | **OK commercial, attribution MANDATORY** | "Planetary surface textures (c) Solar System Scope (solarsystemscope.com), licensed under CC BY 4.0… Modified: resampled and recompressed for real-time use." |
| Solar activity (F10.7, SWPC SSN) | [NOAA SWPC](https://services.swpc.noaa.gov/json/solar-cycle/) | NWS: "in the public domain… may be used without charge for any lawful purpose" | **OK commercial — but see the column warning** | "Solar activity: NOAA/NWS SWPC. 10.7 cm radio flux measured at DRAO Penticton (NRC Canada). NOAA/NWS material… is not subject to copyright protection." |
| ~~Sunspot number (International, 1749–)~~ | ~~[SILSO](https://www.sidc.be/SILSO/datafiles)~~ | **CC BY-NC 4.0** | **BLOCKED.** Also blocked via NOAA's `ssn` column, which is a verbatim copy | — |
| ~~Any ESA Space Science Archive dataset~~ | ~~[ESA ESDC](https://www.cosmos.esa.int/web/esdc/)~~ | **CC BY-NC 3.0 IGO**, prior written authorisation required for commercial use | **BLOCKED** | — |
| VizieR / CDS copies of AAS tables | [CDS](https://cds.unistra.fr/) | **Unverified** — rights page unreadable 2026-09-12 | **Treat as blocked** until read | — |

### Two lines to change in the prose of `docs/data-sources.md`

1. Under **NASA 3D Resources**, the sentence *"the ones shipped here were checked individually,
   and each catalogue record carries the source URL and licence string"* is not supported by
   the catalogue. Replace with: *"The repository publishes a single collection-level
   public-domain statement covering contributed models as well as NASA-authored ones; there is
   no per-model licence data in the repository to check against, and each catalogue record
   carries that collection-level statement."*
2. The closing section *"Before any commercial distribution, every dataset and every derived
   texture needs an individual licence and attribution audit"* can now point here for the Solar
   System family: `docs/licence/solar-system.md`.

---

## 11. Bridging to fictional worlds — the constraint this audit puts on authored content

The owner's constraint has two halves, and the second half is easy to lose behind the first.
The app is *"not just real but bridges to fictional worlds"*, so the licence rules have to hold
at the seam as well as in the middle.

Three rules fall out of this audit:

1. **A real object's identity and position must come from a cleared source, even when a
   blocked source has better data.** If an authored world hangs off a real star or a real
   small body, take the object from Horizons or SBDB and state the precision honestly. Do not
   reach into the ESA archive because it is more precise — that is how a non-commercial
   dependency enters through the fiction rather than through the science.
2. **Authored content must never inherit a Measured label by accident.** The
   `_fictional` textures in §6 are the concrete case, and the promotion path in
   `fetch_surface_maps.py` is the concrete mechanism. Authored assets need their own ingest
   path that cannot set `Measured`, not merely a convention that nobody drops them in the real
   folder.
3. **Authored assets need provenance rows too.** The provenance panel is the thing that makes
   the real/fictional bridge honest rather than confusing. An authored world with a blank
   source field reads as an unlabelled real one. "Authored for CosmoSynth" is a provenance
   value, and a good one — it just has to be *present*.

---

## Appendix — method

Every source was cleared by reading its own primary terms page, not a summary, and quoting the
decisive sentence with its URL and today's date. Where a page was JavaScript-rendered and
returned nothing to a plain fetch, it was fetched with a browser user-agent and stripped to
text on triton; where that failed too, the failure is recorded as unverified (§9.2) rather than
filled in from memory.

Data was fetched with `curl`/`urllib` on triton, never to the laptop. `df -h /` was checked
before starting (16.0 GB free) and after (15.7 GB). Nothing over 150 MB was written in original
form; the fetcher refuses it both on the declared `Content-Length` and mid-stream. Every large
raster was reduced and its original deleted in the same script run. Four multi-gigabyte
products were identified by HEAD request and deliberately not fetched (§7.3).

Two claims in this report were established by *running* something rather than reading it, and
both turned out to matter: the SILSO/NOAA column identity (§4), and the absence of texture data
in the `.csmesh` format (§3). Both are the kind of claim that reads as plausible either way and
is cheap to settle with one command.

`data-staging/noaa-swpc-solar/` is **shared** with another CosmoSynth clearance family
(heliophysics), which staged additional NOAA files there at 14:36 UTC and recorded them in
`_fetchlog.txt` rather than overwriting this audit's `SOURCE.txt`. That directory's `NOTES.md`
says which four files this audit staged. The licence analysis there — including the SILSO
warning — applies to both families' files.
