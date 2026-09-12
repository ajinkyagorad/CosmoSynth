# Licence clearance: Milky Way dust and structure

*Audit and staging pass, 2026-09-12. Family slug `milkyway`. Written against the
owner's constraint of 2026-09-12, verbatim: "make sure stuff sources used are can
be used in commercial capacity to make visualizaiton of real, and also this is
not just real but bridges to fictional worlds".*

Tags: **VERIFIED** = I read the page, ran the command or parsed the file myself
in this pass. **INFERRED** = reasoning from a verified fact, not itself observed.

Everything staged lives under `~/projects/CosmoSynth/data-staging/<slug>/`, with
`SOURCE.txt`, `LICENCE.txt` and `NOTES.md` beside the data. Rejected sources get
the same three files and no data, so nobody re-checks them.

---

## 0. Verdict in one paragraph

Every dust and structure layer the Milky Way scene needs can be built from
commercially-clean data, but **not from the sources the existing design notes
name**. The two best 3D dust maps (Edenhofer 2024, Leike 2020) are CC BY 4.0 and
are now staged as ready-to-raymarch cubes, reduced from 3.25 GB and 2.37 GB of
upstream product without ever storing the raw files. The dust source the earlier
research note recommended (Vergely 2022 via CDS) is **blocked**. Gaia is
**CC BY-NC** — not CC BY-SA as `docs/data-sources.md` currently claims — which
blocks it outright and removes the obvious Cepheid substitute. **No
commercial-clean Galactic Cepheid catalogue exists that I could find**; the layer
is replaced by three CC0 sources plus an analytic arm model, and exactly what is
lost is stated. Globular clusters move from Harris/Baumgardt (both fail) to the
CC0 Local Volume Database at no scientific cost. Deep-sky names and sizes come
from OpenNGC, CC BY-SA 4.0 — conditional, and the share-alike obligation is
spelled out below.

Staged footprint: **59.4 MB** against a 900 MB budget. Nothing over 1 GB was
stored at any point; 2.8 GB was streamed and discarded. Disk on triton was
checked before and after: 15–16 GB free throughout, never below the 8 GB abort
line. **VERIFIED**

---

## 1. Decision table

| # | Source | Verdict | Decisive evidence (read 2026-09-12 unless noted) | Substitute |
|---|---|---|---|---|
| 1 | **Edenhofer et al. 2024** 1.25 kpc 3D dust map, Zenodo 8187943 | **OK — ship** | `https://zenodo.org/api/records/8187943` → `"license": "cc-by-4.0"`. Record readme: *"All data products are made publicly available."* CC BY 4.0 permits commercial use, no share-alike. | — (this *is* the substitute) |
| 2 | **Leike, Glatzle & Ensslin 2020** 400 pc map, Zenodo 3993082 | **OK — ship** | Two independent statements. Record: `"license": "CC-BY-4.0"`. The data's own `readme.txt`: *"Provided under the terms of the Creative Commons Attribution 4.0 International License (CC BY 4.0)."* The same sentence is embedded in `mean_std.h5` itself, in its root `information` attribute. | — |
| 3 | **Bayestar19** (Green et al. 2019), Harvard Dataverse `doi:10.7910/DVN/2EJ9TX` | **OK — cleared, not staged** | Dataverse record: `"license": {"name": "CC0 1.0", "rightsIdentifier": "CC0-1.0"}`, `"termsOfUse": null`. | n/a — see §5 for why it was not staged and the exact recipe if it is wanted |
| 4 | **Vergely, Lallement & Cox 2022** dust cubes, CDS `J/A+A/664/A174` | **BLOCKED** | The catalogue's own ReadMe (`https://cdsarc.cds.unistra.fr/ftp/J/A+A/664/A174/ReadMe`, 6 707 B) contains **no** licence statement — grepping the whole file for licen/copyright/commercial/permission matches only the heading "Acknowledgements:". Distributor's terms (`https://cds.unistra.fr/vizier-org/licences_vizier.html`): *"The data retrieved with VizieR are free of usage in a scientific context"* and *"The commercial usage of the data is subject to rules depending of the origin."* | Edenhofer (#1) + Leike (#2) |
| 5 | **Skowron et al. 2019** Galactic Cepheids (OGLE), GitHub `jskowron/galactic_cepheids` | **BLOCKED** | Repo `LICENSE`, verbatim: *"For figures (e.g. PNG files) and other non-code files (**except scientific data** and unless stated differently) the license is: Creative Commons Attribution-ShareAlike 3.0"*; the rest grants MIT to `*.py` only. The data is excluded from both grants and nothing else licenses it. OGLE's own site (`https://ogle.astrouw.edu.pl/`) is a frameset with no terms at all. | Rice 2016 + Zucker 2020 + Radcliffe Wave + analytic arms (#8–11). Losses quantified in §6 |
| 6 | **Gaia DR3** (and every ESA space science archive product) | **BLOCKED** | `https://www.cosmos.esa.int/web/gaia-users/license`, verbatim and complete: *"Gaia data are distributed under the CC BY-NC 3.0 IGO  license. For details and guidelines concerning commercial use of the Gaia data, please see the Terms and Conditions for the use of data in the ESA space science archives."* NonCommercial, with commercial use available only by asking ESA — i.e. "by permission". | none for astrometry; see §2 correction |
| 7 | **Harris (1996, 2010 ed.)** MW globular catalogue | **BLOCKED for a paid build** | The file's own header (`https://physics.mcmaster.ca/~harris/mwgc.dat`), verbatim: *"THE DATA IN THIS CATALOG ARE SUPPLIED FREE OF CHARGE TO ALL USERS.  ANY THIRD PARTY WHO SUPPLIES THIS CATALOG IN TURN TO OTHER USERS MUST REFER TO THE ORIGINAL SOURCE, WHICH IS THIS WEBSITE, AND MUST DO SO WITHOUT CHARGE OF ANY FEE."* Shipping it inside a paid app is supplying it for a fee. | LVDB (#12) |
| 8 | **Baumgardt** GC database | **BLOCKED** | `https://people.smp.uq.edu.au/HolgerBaumgardt/globular/` states no licence, no terms, no redistribution permission. The only rights statement is the copyright line *"(C) Holger Baumgardt, Antonio Sollima, Michael Hilker, Andrea Bellini, Eugene Vasiliev, Vincent Henault-Brunet & Nolan Dickson, March 2023"*. No stated licence = bar failure. | LVDB (#12) |
| 9 | **Reid et al. 2019** maser parallaxes / spiral-arm fit — **the numbers** | **OK — facts** | Measured pitch angles, reference radii and R₀ are facts about the Galaxy, not expressive works. Implement the model, cite the paper, redistribute nothing. | — |
| 10 | **Reid et al. 2019** — **the VizieR table** `J/ApJ/885/131` | **BLOCKED** | Same VizieR terms as #4. The author-side alternative is unavailable: `http://bessel.vlbi-astrometry.org/` returns 275 bytes reading, verbatim, *"The BeSSeL Homepage is currently offline. We are working on it."* | implement from published parameters (#9) |
| 11 | **OpenNGC** NGC/IC deep-sky database | **CONDITIONAL — ship with share-alike** | README, verbatim: *"Unlike other similar databases which are released with license limitations, OpenNGC is released under CC-BY-SA-4.0 license, which allows the use for a wider range of cases."* Full licence text staged from `LICENSES/CC-BY-SA-4.0.txt`. (Note `…/master/LICENSE` is a 404 — the licence is in `LICENSES/`.) | — (nothing cleaner found) |
| 12 | **Local Volume Database** globular clusters | **OK — ship** | `LICENSE` file, verbatim opening: *"Creative Commons Legal Code / CC0 1.0 Universal"*. Confirmed via the GitHub licence API. Citation request in the README is a courtesy, honoured anyway. | — (this *is* the substitute for #7 and #8) |
| 13 | **Rice et al. 2016** 1064 Galactic molecular clouds, Dataverse `doi:10.7910/DVN/EWY90X` | **OK — ship** | Dataverse record: `"license": {"name": "CC0 1.0", …}`, `"termsOfUse": null`. | — (this *is* the Cepheid substitute) |
| 14 | **Zucker et al. 2020** cloud distance compendium, Dataverse `doi:10.7910/DVN/07L7YZ` | **OK — ship** | Same CC0 1.0 record field, `"termsOfUse": null`. | — (this *is* the "named nebulae with real positions and sizes" answer) |
| 15 | **Radcliffe Wave** (Konietzka et al. 2024), Dataverse `doi:10.7910/DVN/F98QHY` and `…/XSCB9N` | **OK — ship** | Both records: `"license": {"name": "CC0 1.0", …}`, `"termsOfUse": null`. | — |
| 16 | **CDS / VizieR / SIMBAD** as a shipping source, generally | **BLOCKED** | `licences_vizier.html` as quoted in #4. The `https://cds.unistra.fr/legals/` page does say *"Datasets containing only Public Information are distributed under an Open Licence or ODbL … or CC-BY"* and that a dataset-specific licence *"is provided on the Dataset page"* — but neither catalogue checked here has one on its page, so the general scientific-use-only terms govern. | fetch from the origin (Zenodo / Dataverse / the author) and verify there |
| 17 | **HyperLEDA** | **UNVERIFIED** | `http://leda.univ-lyon1.fr/` refused HTTPS from triton (ECONNREFUSED :443); the Lyon mirror `http://atlas.obs-hp.fr/hyperleda/` serves a front page with no terms. Nothing staged from it, so nothing is blocked today. | not used |
| 18 | **ASAS-SN Variable Stars Database** (considered as a Cepheid substitute) | **BLOCKED** | `https://asas-sn.osu.edu/variables` states citation requirements (Shappee et al. 2014 plus one catalog paper) and **no licence, no terms of use, no redistribution statement**. | — |
| 19 | **sb2580/k3d_dust** HTML snapshot — the dust cube the existing Milky Way Atlas page ships | **BLOCKED, twice** | Repo licence is **GPL-3.0** (GitHub API, recorded 2026-09-11): a copyleft software licence, and the blob is extracted from the licensed work. Separately, the data's provenance is unstated — README says only *"based on Leike & Lallement 3d dust maps"*, no version, no citation, units recorded as "relative, unknown". | Edenhofer (#1) + Leike (#2) |

One misquote worth recording so it does not propagate: a web-search summary
reported CDS as saying *"Any other use, in particular for commercial purposes,
without the prior written consent of the CDS is prohibited"* about its **data**.
Reading the surrounding paragraph on `https://cds.unistra.fr/legals/` shows that
sentence governs the **CDS, SIMBAD, VizieR, Aladin, Xmatch and CDSportal logos**
— it is a trademark clause. The data position (#4/#16) is restrictive enough
without overstating it. **VERIFIED**

---

## 2. Corrections to existing project documents

These are the places where earlier docs, written for non-commercial sideload,
are now wrong. Each was checked against a primary page in this pass.

1. **`docs/data-sources.md`, "Planned, not yet integrated" table, Gaia row.**
   Currently reads: *"ESA, not NASA. CC BY-SA 3.0 IGO with a specific required
   acknowledgement."* **Wrong.** It is **CC BY-NC 3.0 IGO**. Share-alike would
   have been inconvenient; NonCommercial is disqualifying. Replace with the
   ledger row in §8.

2. **`docs/research/2026-09-11-milkyway-atlas-port.md`, §0 and the `dust.bin`
   row of the data table.** Recommends Vergely+2022 at CDS as "its better
   primary source" and records its licence as *"licence not checked"*. Now
   checked: **blocked**. The replacement is Edenhofer 2024, which is better in
   every respect except mapped volume.

3. **Same document, §9 table, "Leike+2020" and "Edenhofer+2023" rows.** These
   are marked *"Do not download"* on disk-space grounds. That constraint is
   correct and has now been solved rather than accepted: both maps are staged,
   reduced by HTTP range streaming, without either full product ever touching
   disk. See §7.

4. **Same document, `cepheids.bin` row**, which says the Cepheid data licence
   *"needs a decision"*. The decision is **blocked**, and the obvious substitute
   (Gaia) is blocked too. The layer is replaced, not re-sourced — §6.

5. **`docs/data-sources.md`, closing section.** Its rule — *"Hosted by NASA does
   not mean public domain"* — is right and is now evidenced twice over: ESA's
   archives are CC BY-NC, and CDS grants scientific use only. Worth strengthening
   to name the agency-level split explicitly: **NASA products are US Government
   works; ESA science-archive products are CC BY-NC**. A pipeline that treats
   them alike ships a licence violation no test can catch.

---

## 3. Staged inventory

Root: `~/projects/CosmoSynth/data-staging/`. All sizes and hashes measured on
triton 2026-09-12. **VERIFIED**

### Cleared and staged

| Path | Bytes | sha256 | Rows / voxels |
|---|---:|---|---|
| `edenhofer2024-3d-dust/edenhofer2024_dust_256x256x256_10pc_u8.bin` | 16 777 216 | `d06dbe9b090c09b7bb6459d760e734c0ce44bbf27a02a55230d6c200c4401cde` | 256³ voxels @ 10 pc; 8 144 760 inside the 1248 pc map sphere |
| `edenhofer2024-3d-dust/dust_column_nside128_nest.f32` | 786 432 | `b09111938a97c6abe944b696ec8e033f114cceaf648bd5cbf4d173ddfefdfd30` | 196 608 HEALPix NESTED pixels |
| `edenhofer2024-3d-dust/radial_shell_bounds_pc.txt` | 5 729 | `adf52ffefcbe3e63b4ff1edb31bb81f6e116c7e15522ad8744365b00dadd7d01` | 517 shell boundaries |
| `edenhofer2024-3d-dust/cube_meta.json` | 1 822 | `652f6ea8028ed21b69b26367cc4f5feacb979c241da222113a1e4b35327986eb` | — |
| `leike2020-3d-dust/leike2020_dust_370x370x270_2pc_u8.bin` | 36 963 000 | `36245c61db24058bb62d8cef187217260005e2699059fb930d333e7a0b916c6c` | 370×370×270 voxels @ 2 pc |
| `leike2020-3d-dust/cube_meta.json` | 1 683 | `4b8952433df9f7a1836c6dbe097564f2a4a35a96295d73271bb50d6d723d9d0a` | — |
| `lvdb-globular-clusters/gc_harris.csv` | 91 840 | `1d9944892a21cece96d1d0528b741aa62536738a7eeeba7824eb75f949ec15de` | 155 rows × 120 cols |
| `lvdb-globular-clusters/gc_mw_new.csv` | 32 736 | `e13e19219bf4ec4701d49653aa1fced11a77543ae37228cfb9e06aaa3f4f8cc1` | 58 rows |
| `lvdb-globular-clusters/gc_dwarf_hosted.csv` | 136 446 | `a1dab859e6e61e9f2932da30229f1f91b56890574c392ab2001197bbed87df3b` | 313 rows |
| `lvdb-globular-clusters/gc_ambiguous.csv` | 13 130 | `84557e9e99e5cbcd9b5ecace7156582e95bee0a2efaf16be8aa6b817155fc3a7` | 22 rows |
| `lvdb-globular-clusters/lvdb_LICENSE.txt` | 7 048 | `a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499` | CC0 text |
| `rice2016-molecular-clouds/cloud_catalog.fits` | 486 720 | `6d43e30e872db56eabb279415030d6d1231359cb71d1ecd7bb823b2ccfd48a4e` | 1064 rows × 56 cols |
| `zucker2020-cloud-distances/Handbook_Distances_Zucker2020.dat` | 55 870 | `aea23bd136ae635172f78ca77b569dc6517d1ca02a67119fdf0a2d1713a060ca` | 326 sightlines, 94 named clouds |
| `radcliffe-wave-konietzka2024/Radcliffe_Wave_best_fit_model_Konietzka2024.tab` | 47 253 | `18e9cf9746654229717fc4e40b93f951028d100ef08d3431728b58bff168e212` | spine polyline, cols `x y z vz` |
| `radcliffe-wave-konietzka2024/Radcliffe_Wave_stellar_clusters_Konietzka2024.tab` | 10 888 | `6f980cc9ea64b3e4ed7f77514f3ef1940d2660941db940ff8a939a7d0c2bf522` | member clusters, 16 cols |
| `openngc-deepsky/NGC.csv` | 3 876 622 | `be150bdaa1997dacbcb39f303074403edec7a953b589b36d5f1c4522c0cc6fae` | 13 970 rows × 32 cols |
| `openngc-deepsky/addendum.csv` | 17 484 | `1d8f0914e643ada325a5a94d88d8fefad6a4937a2f77cc34f21483af22b11983` | non-NGC/IC additions |
| `openngc-deepsky/CC-BY-SA-4.0.txt` | 18 375 | `cde7883b9050a1104f4ac19a1572aafd6e5d7323b68351aaf51fbf4beba54966` | licence text |
| `openngc-deepsky/README.md` | 4 285 | `382a717d05a2b75fcc22e9769340b5ec0a6d6beec210e43b5993b1c2b9efd7d2` | licence statement + source list |

Family total including all metadata and rejection records: **59 421 127 bytes**.

### Documented, no data (cleared-but-deferred and rejected)

`bayestar19-3d-dust` (CC0, deferred), `reid2019-spiral-arms` (facts OK, table
blocked), `vergely2022-dust-REJECTED`, `skowron2019-cepheids-REJECTED`,
`gaia-esa-archives-REJECTED`, `harris-globulars-REJECTED`,
`baumgardt-globulars-REJECTED`, `simbad-vizier-cds-REJECTED`,
`k3d-dust-snapshot-REJECTED` — each with `SOURCE.txt`, `LICENCE.txt`, `NOTES.md`.

Reduction and verification scripts, kept because they are the reproduction
recipe: `data-staging/scripts/{eden_reduce,eden_verify,leike_reduce,leike_verify,inspect_cats,write_staging}.py`.

---

## 4. Attribution strings for the app credits

Ship these verbatim. The first two are contractual (CC BY), the OpenNGC one is
contractual (CC BY-SA), the rest are CC0 and are courtesy — ship them anyway,
because they are what the accuracy claim rests on.

```
3D dust map: Edenhofer, Zucker, Frank, Saydjari, Speagle, Finkbeiner and
Ensslin (2024), A&A 685, A82. Data via Zenodo doi:10.5281/zenodo.8187943,
CC BY 4.0. Resampled and quantised for this application.

Local 3D dust: Leike, Glatzle & Ensslin (2020), A&A 639, A138. Data via
Zenodo doi:10.5281/zenodo.3993082, CC BY 4.0. Downsampled and quantised for
this application.

Deep-sky objects: OpenNGC by Mattia Verga,
https://github.com/mattiaverga/OpenNGC, CC BY-SA 4.0. Modified: subset,
reprojected and converted. The derived table is distributed under CC BY-SA 4.0.

Globular clusters and dwarf galaxies: Local Volume Database (Pace 2025,
OJAp 8, E142), https://github.com/apace7/local_volume_database, CC0 1.0.
Individual distances and magnitudes carry their own per-object references in
the catalogue.

Molecular clouds: Rice, Goodman, Bergin, Beaumont & Dame (2016), ApJ 822, 52,
from the Dame, Hartmann & Thaddeus (2001) CO survey. Harvard Dataverse
doi:10.7910/DVN/EWY90X, CC0 1.0.

Molecular cloud distances: Zucker, Speagle, Schlafly, Green, Alves and
Goodman et al. (2020), A&A 633, A51. Harvard Dataverse
doi:10.7910/DVN/07L7YZ, CC0 1.0.

Radcliffe Wave: Konietzka, Goodman, Zucker, Burkert, Alves et al. (2024),
Nature 628, 62; structure discovered by Alves et al. (2020), Nature 578, 237.
Harvard Dataverse doi:10.7910/DVN/F98QHY and doi:10.7910/DVN/XSCB9N, CC0 1.0.

Spiral-arm geometry after Reid et al. (2019), ApJ 885, 131 (BeSSeL Survey
maser parallaxes). Model implemented from the published fit parameters; no
catalogue data redistributed.
```

**The one share-alike obligation, precisely.** OpenNGC is CC BY-SA 4.0. If the
build ships a derived deep-sky table (the `.csdeep` / `deepsky.json` carrying
OpenNGC-derived names, positions, types and angular sizes), that **table** is
Adapted Material and must be offered to recipients under CC BY-SA 4.0. It does
not reach the application binary, the shaders, the dust cubes or any other data
file — CosmoSynth is not an adaptation of a deep-sky catalogue. This is the same
shape of obligation the project already accepted for HYG v4.4, so it adds no new
class of duty. If the project decides it wants zero share-alike anywhere, the
only alternative found is to drop NGC/IC names and sizes entirely and label
objects from Zucker (CC0) plus IAU designations.

---

## 5. What I could not verify, and what I deliberately did not do

| Item | Status | Why |
|---|---|---|
| **HyperLEDA terms** | **UNVERIFIED** | `leda.univ-lyon1.fr` refuses HTTPS from triton (ECONNREFUSED :443); the Lyon mirror's front page carries no terms. Nothing from HyperLEDA is staged, so nothing is blocked — but it is one of OpenNGC's listed upstream sources, so it is a chain risk, not a non-issue. |
| **OpenNGC's upstream chain** | **Risk recorded, not resolved** | OpenNGC's README names NED, HyperLEDA, SIMBAD, HEASARC, Corwin and *"internet sources like Wikipedia"* as inputs. Verga asserts CC BY-SA over his compilation and that is the strongest available position, but I could not verify that every upstream holder agreed. Positions and sizes are facts; selection and arrangement is what a compiler owns. Flagged here rather than buried. |
| **Reid et al. 2019 arm parameters** | **UNVERIFIED, deliberately** | The licence position is settled (facts are usable), but I did not read the paper's Table 2 in this pass, so no numbers are recorded anywhere in this family. They must be transcribed by a human from the primary paper, each with its uncertainty — not scraped, and not recalled by a model. That is how a wrong R₀ ships looking confident. |
| **Bayestar19 reduction** | **Cleared (CC0) but not staged** | Three reasons: Edenhofer already covers the raymarched volume all-sky in true density; Bayestar covers only ~¾ of the sky (dec > −30°), and a dust volume with a southern hole is a worse artefact in a headset than no far-field volume; and the reduction is currently blocked on a missing grid — the FITS is 543 MB gzipped but **12.3 GB decompressed** and cannot be range-requested, and the distance grid is not in the FITS at all (`dustmaps` reads `/pixel_info.attrs['DM_bin_edges']` from the 728 MB HDF5, and that string is absent from the first 32 MB of it). Full recipe in `bayestar19-3d-dust/NOTES.md`. |
| **Whether the Harris "without charge of any fee" clause really bars a paid app** | **My reading, not a lawyer's** | It is the plain reading, and it costs us nothing because LVDB is strictly better here. If a lawyer reads it otherwise, nothing needs to change — we are not using it either way. |
| **AAS / A&A journal reuse policy for 2016–2019 supplementary tables** | **Not pursued** | Would only matter if we wanted a VizieR table anyway. Every table we wanted turned out to have a CC0 or CC BY alternative at the author's own deposit, which is a better answer than a publisher-policy argument. |

---

## 6. The Cepheid hole, stated plainly

This is the one place where the commercial constraint costs real science.

**No commercial-clean Galactic Cepheid catalogue was found.** Five candidates,
all checked against primary pages on 2026-09-12: Skowron 2019 (data excluded
from the repo licence), Gaia DR3 (CC BY-NC), OGLE's own site (no terms at all),
any VizieR copy (scientific use only), ASAS-SN (citation requirements, no
licence). Zenodo and Harvard Dataverse searches for an openly-licensed Galactic
Cepheid catalogue returned nothing relevant.

**What replaces the layer** — all CC0, all staged:

- `rice2016-molecular-clouds` — 1064 CO clouds across the whole Galaxy with
  positions, kinematic distances, physical sizes and masses. Traces the spiral
  arms directly, which is the main thing Cepheids were drawing.
- `zucker2020-cloud-distances` — 94 named clouds with precise distances inside
  2.5 kpc.
- `radcliffe-wave-konietzka2024` — the Local arm's actual measured 3D shape.
- the analytic Reid et al. 2019 arm model, from published parameters.

**What is lost, precisely:**

- **Ages.** Skowron gives a period-derived age per Cepheid (3–1169 Myr). Nothing
  in the replacement set carries a stellar age. A "young disk coloured by age"
  visualisation is not reproducible.
- **The warp traced by individual stars.** Skowron's Cepheids reach 121 kpc with
  |z| up to 11.8 kpc, and the warped disk was the paper's headline result. CO
  clouds sit near the plane and are kinematic-distance-limited: they show arms,
  not the warp.
- **Distance precision.** Period–luminosity distances are good to a few per cent;
  kinematic distances carry the near/far ambiguity (flagged per row in Rice's
  `KDA_resolution`, with `'U'` meaning unresolved).
- **A visual register.** 2214 point sources read as "the Galaxy is made of
  stars"; 1064 extended clouds read as "the Galaxy is made of gas". The fiction
  layer should know which one it is bridging from.

---

## 7. How the two big maps were reduced without storing them

Worth recording because it turns "too large for triton" into a solved problem,
and because the same trick applies to the other families.

**Edenhofer.** The 3.25 GB `mean_and_std_healpix.fits` was never written to
disk. Its structure was mapped with three small range reads — primary header,
`MEAN` header, and the last 6.3 MB where the radial BINTABLEs live — giving
exact byte offsets. Then **one** range request streamed the `MEAN` extension
only (bytes 5760–1623201407, 1.62 GB) straight into a reducer that averaged
nside 256 → 128 per distance shell in flight and resampled to a 256³ 10 pc
Cartesian cube. 85 s wall, 16.8 MB out. The `STD` half, the 15.7 GB XYZ
interpolation and the 19.5 GB sample cube were never fetched.

**Leike.** The 2.37 GB `mean_std.h5` has no chunking or compression, so its two
float32 datasets are contiguous. Offsets were derived from the file size and
confirmed by probing four byte positions (`2048` decodes as `1.26e-4`, a
plausible extinction density; the literal string `information` appears at
`1182818064`, exactly where the second dataset's object header must be). One
range request streamed the `mean` dataset (1.18 GB), block-averaged 2×2×2 to
2 pc and transposed to match the Edenhofer axis order. 70 s wall, 37 MB out.

**Both were then checked against the sky, not just parsed.** This is the step
that distinguishes "bytes arrived" from "a dust map arrived":

- Edenhofer: ten named molecular clouds sampled along their own sightlines,
  every one peaking within 1–2 voxels of its published Zucker+2020 distance
  (Orion A 432 → 430 pc, Perseus 294 → 290, Taurus 141 → 150, Ophiuchus 139 →
  150, Chamaeleon 190 → 190 …); Cygnus X at 1300 pc correctly falls outside the
  1248 pc map sphere. Local Bubble cavity present (5.6× density contrast), disk
  flattening present (32×).
- Leike: all eight sampled clouds peak within 2–13 pc of their published
  distances; cavity contrast 4.0×, flattening 4.4×.

Scripts: `data-staging/scripts/eden_verify.py`, `leike_verify.py`. Both assert
and exit non-zero on failure, so they are regression tests, not printouts.

**One environment trap, recorded because it cost time and will recur:** running
any Python script from `/tmp` on triton fails at `import numpy`, because another
agent has left `/tmp/inspect.py` there and it shadows the stdlib `inspect`
module. The traceback names `bpy`, which has nothing to do with anything. Keep
scripts in the staging directory. (Same class of failure as the
`assets-src shadows the stdlib` note.)

---

## 8. Ledger rows for `docs/data-sources.md`

Add these to the "Currently used" section (or a new "Milky Way structure"
subsection), and **replace** the existing Gaia row in the planned table.

| Data | Source | Licence | Terms note |
|---|---|---|---|
| 3D dust, 1.25 kpc sphere @ 10 pc | [Edenhofer et al. 2024, Zenodo 8187943](https://zenodo.org/records/8187943) | **CC BY 4.0** | Commercial use permitted, attribution + "indicate changes" required (it was resampled and quantised). No share-alike. Staged reduction: `data-staging/edenhofer2024-3d-dust/` |
| 3D dust, local 740×740×540 pc @ 2 pc | [Leike, Glatzle & Ensslin 2020, Zenodo 3993082](https://zenodo.org/records/3993082) | **CC BY 4.0** | Licence is stated three times: Zenodo record, `readme.txt`, and inside the HDF5's own `information` attribute. Units are **G-band optical depth per pc**, not magnitudes |
| Galactic molecular clouds (1064) | [Rice et al. 2016, Dataverse 10.7910/DVN/EWY90X](https://doi.org/10.7910/DVN/EWY90X) | **CC0 1.0** | No conditions. Replaces the blocked Cepheid layer for spiral structure. Kinematic distances: honour `KDA_resolution` |
| Named cloud distances (94 clouds, 326 sightlines) | [Zucker et al. 2020, Dataverse 10.7910/DVN/07L7YZ](https://doi.org/10.7910/DVN/07L7YZ) | **CC0 1.0** | The distance source that turns OpenNGC angular sizes into physical sizes |
| Radcliffe Wave spine + member clusters | [Konietzka et al. 2024, Dataverse 10.7910/DVN/F98QHY, /XSCB9N](https://doi.org/10.7910/DVN/F98QHY) | **CC0 1.0** | Positions in **pc**; Rice is in **kpc**. Mixing them silently renders 1000× too small |
| Globular clusters + dwarfs (548) | [Local Volume Database (Pace 2025)](https://github.com/apace7/local_volume_database) | **CC0 1.0** | Replaces Harris and Baumgardt, both of which fail the bar. Use the catalogue's `M_V`, never `m_V − μ` |
| NGC/IC deep sky (13 970) | [OpenNGC (Mattia Verga)](https://github.com/mattiaverga/OpenNGC) | **CC BY-SA 4.0** | **Conditional.** The derived deep-sky table inherits share-alike; the app does not. Same obligation shape as HYG. Upstream chain (NED/HyperLEDA/SIMBAD) unverified — recorded risk |
| Spiral-arm geometry | Reid et al. 2019, ApJ 885, 131 | **Facts, cited** | Implement the log-spiral model from published parameters. The VizieR table copy is **not** redistributable |
| Deep 3D dust (deferred) | [Bayestar19, Dataverse 10.7910/DVN/2EJ9TX](https://doi.org/10.7910/DVN/2EJ9TX) | **CC0 1.0** | Cleared, not staged: ¾-sky footprint and a 12.3 GB decompressed stream. Recipe in `data-staging/bayestar19-3d-dust/NOTES.md` |
| ~~Stars (deeper than HYG) — Gaia~~ | [ESA Gaia Archive](https://gea.esac.esa.int/archive/) | **CC BY-NC 3.0 IGO — BLOCKED** | **Corrects the existing row, which says CC BY-SA 3.0 IGO.** NonCommercial. Commercial use only by contacting ESA, i.e. by permission. This blocks all ESA space-science-archive products, not just Gaia |
| ~~3D dust — Vergely 2022 via CDS~~ | CDS `J/A+A/664/A174` | **BLOCKED** | Catalogue ReadMe states no licence; VizieR grants *"free of usage in a scientific context"* only |
| ~~Cepheids — Skowron 2019~~ | `jskowron/galactic_cepheids` | **BLOCKED** | Repo licence excludes *"scientific data"* from every grant it makes |
| ~~Globulars — Harris / Baumgardt~~ | McMaster / UQ | **BLOCKED** | Harris: redistribution permitted only *"WITHOUT CHARGE OF ANY FEE"*. Baumgardt: no licence stated |
| ~~SIMBAD / VizieR as a source~~ | CDS | **BLOCKED** | A mirror does not launder a licence. Fetch from the origin and verify there |

---

## 9. What the app can now build, per layer

| Layer | Data | Licence | Notes |
|---|---|---|---|
| Local Bubble, 0–370 pc | Leike cube, 2 pc, 37 MB `Texture3D` R8 | CC BY 4.0 | The cavity around the Sun is the visual point |
| Solar neighbourhood, 0–1.25 kpc | Edenhofer cube, 10 pc, 16.8 MB `Texture3D` R8 | CC BY 4.0 | Mask `r < 1248 pc`: `v == 0` means *unknown*, not *empty* |
| Far-field dust sky | Edenhofer column map, nside 128, 786 KB | CC BY 4.0 | For when the camera leaves the cube |
| Named nearby nebulae | Zucker names + distances × OpenNGC angular sizes | CC0 × CC BY-SA | Labels *inside* the dust volume, not painted on the sky |
| Galactic spiral arms | Rice clouds + analytic Reid model | CC0 + facts | Validate the model against the clouds |
| Local arm 3D shape | Radcliffe Wave spine + clusters | CC0 | Animate z(t) from `vz` and label it as extrapolation |
| Globular cluster halo | LVDB, 213 MW globulars | CC0 | Sized by `rhalf` × distance, coloured by `M_V`/metallicity |
| Deep-sky labels | OpenNGC, 131 common names + Messier | CC BY-SA 4.0 | Filter the 651 `Dup` rows and the star types |

Every byte in that table is cleared for commercial distribution. The one
conditional entry is OpenNGC, and its condition is bounded and already
precedented in this project.

---

## 10. Reproducing this

```bash
# on triton, from ~/projects/CosmoSynth/data-staging/scripts/
python3 eden_reduce.py  --selfcheck           # HEALPix + codec, no network
python3 leike_reduce.py --selfcheck
python3 eden_reduce.py  ../edenhofer2024-3d-dust <tail.bin>   # 1.62 GB streamed
python3 leike_reduce.py ../leike2020-3d-dust                  # 1.18 GB streamed
python3 eden_verify.py  ../edenhofer2024-3d-dust   # asserts; non-zero on failure
python3 leike_verify.py ../leike2020-3d-dust
python3 inspect_cats.py                            # prints real headers and rows
python3 write_staging.py                           # regenerates all metadata
```

`eden_reduce.py` needs `<tail.bin>`, the last 6 320 000 bytes of
`mean_and_std_healpix.fits`, for the radial shell tables:

```bash
curl -sL -r 3246395200-3252715199 \
  "https://zenodo.org/records/8187943/files/mean_and_std_healpix.fits?download=1" \
  -o tail.bin
```

Nothing else is needed. Python 3.12 + numpy 2.5.1 + curl; no astropy, no healpy,
no h5py. The HEALPix NESTED implementation is the project's existing
`docs/research/scripts/healpix_nest.py`.
