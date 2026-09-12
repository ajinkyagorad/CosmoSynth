# Local Group and Local Volume galaxies — commercial licence clearance

**Family:** Local Group / Local Volume galaxies (the ~1000 nearby galaxies drawn at measured
position, shape and orientation, plus the hero objects).
**Date of work:** 2026-09-12 (all licence pages read that day, all fetches made that day, UTC).
**Constraint applied:** commercial usability is a hard filter on every shipped byte.
**Staging root:** `~/projects/CosmoSynth/data-staging/`
**Family footprint:** 43.9 MB against a 400 MB budget. VERIFIED (`du -sb`).

Every claim below is marked **VERIFIED** (I read the page or ran the command myself) or
**INFERRED** (reasoning, memory, or a third-party reading).

---

## 1. The headline

The design in `docs/design/2026-09-11-B-data.md` §2.11 built the Local Volume from five sources.
**Two of the five are blocked** — the Updated Nearby Galaxy Catalog via CDS, and the 2MASS XSC via
VizieR. Both have clean substitutes from the survey's own NASA archive, and the replacement is in
one respect *better* than the original: the UNGC had no position angle at all, which is why the
design had to bolt a second catalogue onto it; the 2MASS Large Galaxy Atlas carries the axis ratio
and the position angle in the same row, measured on the same mosaic, with a second independent fit
beside it for cross-checking.

The one real loss is **measured distances for the giants**. Every all-sky nearby-galaxy distance
compilation is blocked (UNGC, HyperLEDA, NED-D, and EDD/Cosmicflows — the last independently
rejected by a sibling family the same day). The replacement derives each giant's distance from the
median distance of its LVDB satellites, which is CC0 all the way down and lands within ~1–9% of the
literature for the nine named heroes — but it is **Inferred**, and the app must say so.

---

## 2. Decision table

| # | Source | Verdict | Decisive evidence (verbatim), URL, read date | Substitute |
|---|---|---|---|---|
| 1 | **Local Volume Database (LVDB) v1.1.1** — `data/dwarf_all.csv`, 911 dwarfs | **OK** | *"Creative Commons Legal Code / CC0 1.0 Universal"* — the repo's own LICENSE file at the pinned tag, <https://raw.githubusercontent.com/apace7/local_volume_database/v1.1.1/LICENSE>, read 2026-09-12. **VERIFIED.** Corroborated by the Zenodo record's licence field *"Creative Commons Zero v1.0 Universal"*, <https://zenodo.org/doi/10.5281/zenodo.14076714> → v1.1.1, DOI 10.5281/zenodo.21910097, read 2026-09-12. **VERIFIED** | — (kept) |
| 2 | **2MASS Large Galaxy Atlas v2** (IRSA table `lga_v2`, 656 rows) | **OK** | License field *"https://www.usa.gov/government-works"*, publisher *"NASA/IPAC Infrared Science Archive"*, access *"public"* — <https://catalog.data.gov/dataset/2mass-large-galaxy-atlas-photometry>, read 2026-09-12. **VERIFIED** | — (new; replaces UNGC) |
| 3 | **2MASS All-Sky Extended Source Catalog** (IRSA table `fp_xsc`) | **OK** | License field *"https://www.usa.gov/government-works"*, publisher *"NASA/IPAC Infrared Science Archive"* — <https://catalog.data.gov/dataset/2mass-all-sky-extended-source-catalog>, read 2026-09-12. **VERIFIED** | replaces VizieR `VII/233/xsc` |
| 4 | **2MASS imagery** (LGA mosaics, and 2MASS images generally) | **OK** | *"The images in this gallery are released into the public domain."* — <https://irsa.ipac.caltech.edu/data/2MASS/docs/gallery/messiercat.html>, read 2026-09-12. **VERIFIED.** Plus *"Unless otherwise noted, images and video on the IPAC public web sites may be used for any purpose without prior permission."* — <https://www.ipac.caltech.edu/page/image-use-policy>, read 2026-09-12. **VERIFIED** | — (14 hero mosaics staged) |
| 5 | **Updated Nearby Galaxy Catalog** (Karachentsev+2013, AJ 145, 101) via CDS `J/AJ/145/101` | **BLOCKED** | *"The data retrieved with VizieR are free of usage in a scientific context…"* and *"The commercial usage of the data is subject to rules depending of the origin."* — <https://cds.unistra.fr/vizier-org/licences_vizier.html>, read 2026-09-12. **VERIFIED** | 2MASS LGA v2 + LVDB-derived distances |
| 5b | UNGC via the authors' own host, <https://www.sao.ru/lv/lvgdb/> | **BLOCKED** | The introduction page carries **no** terms, copyright, licence or redistribution statement of any kind — only a description, `lvg@sao.ru`, and a visitor counter. Read 2026-09-12. **VERIFIED** (verified by absence) | same |
| 6 | **HyperLEDA** | **BLOCKED** | The whole of its rights posture: *"If HyperLeda was helpful to your research, for example to prepare observations or astronomical papers, an acknowledgment would be appreciated"* — <http://atlas.obs-hp.fr/hyperleda/acknowledge.html>, read 2026-09-12 (Lyon host unreachable; OHP mirror served it). No licence, no redistribution grant anywhere. **VERIFIED** | 2MASS LGA + XSC |
| 7 | **NED / NED-D** | **BLOCKED** | *"Copyright (c) 2026, California Institute of Technology"* — the entirety of <https://ned.ipac.caltech.edu/Documents/Guides/Copyright>, read 2026-09-12; and the acknowledgements page grants nothing: *"This research has made use of the NASA/IPAC Extragalactic Database, which is funded by the National Aeronautics and Space Administration and operated by the California Institute of Technology."* — <https://ned.ipac.caltech.edu/Documents/Overview/Acknowledgments>, read 2026-09-12. **VERIFIED** | LVDB satellite-median distances (Inferred) |
| 8 | **Siena Galaxy Atlas 2020** / DESI Legacy Imaging Surveys | **CONDITIONAL — not taken** | *"Images are licensed under a Creative Commons Attribution 4.0 International License, and hence may on a non-exclusive basis be reproduced without fee provided the credit is clear and visible."* — <https://www.legacysurvey.org/acknowledgment/>, read 2026-09-12. **VERIFIED.** That sentence covers *images*; the catalogue's own redistribution terms are stated nowhere I could read. The SGA page only requires acknowledgement: *"Use of the SGA-2020 data products must acknowledge the Scientific Publication Acknowledgment for the DESI Legacy Imaging Surveys, as well as the specific SGA acknowledgment."* — <https://www.legacysurvey.org/sga/sga2020/>, read 2026-09-12. **VERIFIED** | not needed; 2MASS covers it |
| 9 | **ESA/Hubble imagery** (the task's open question) | **OK** | Creative Commons Attribution 4.0 International, commercial use permitted; *"The full image or footage credit must be presented in a clear and readable manner to all users, with the wording unaltered"* and *"The credit should not be hidden or disassociated from the image footage."* — <https://esahubble.org/copyright/>, read 2026-09-12. **VERIFIED** | usable, with the per-image credit |
| 10 | **ESO imagery** | **OK** | CC BY 4.0, commercial use permitted; *"the full image or footage credit must be presented in a clear and readable manner to all users, with the wording unaltered"*; *"ESO materials, images and videos may not be used to state or imply the endorsement by ESO or any ESO employee of a commercial product or service"* — <https://www.eso.org/public/copyright/>, read 2026-09-12. **VERIFIED** | best for southern heroes |
| 11 | **NOIRLab / NSF imagery** | **OK** | *"Unless specifically noted, the images, and videos distributed on the public NOIRLab website … are licensed under a Creative Commons Attribution 4.0 International License, and hence may on a non-exclusive basis be reproduced without fee provided the credit is clear and visible."* — <https://noirlab.edu/public/copyright/>, read 2026-09-12 (fetched from triton; the laptop fetcher got an empty body). **VERIFIED** | best for northern heroes |

### Answers to the task's specific questions

- **LVDB believed CC0 — verify and pin a release.** VERIFIED CC0, pinned at **git tag `v1.1.1`**,
  which is also the current Zenodo version (DOI 10.5281/zenodo.21910097). The design's note that
  the tag path is INFERRED to resolve is now **VERIFIED to resolve**. `release_version.txt` does
  **not** exist at that tag (GitHub returns the 14-byte string `404: Not Found`) — use the tag.
- **Does IPAC state public-domain-style terms for the 2MASS XSC?** Effectively yes, and the
  decisive statement is not on an IPAC page: it is the US Government's own machine-readable rights
  field for that dataset on data.gov (`https://www.usa.gov/government-works`). IPAC's site policy
  (*"may be used for any purpose without prior permission"*) and 2MASS's gallery statement
  (*"released into the public domain"*) corroborate it for imagery.
- **Are NASA/ESA Hubble images usable commercially?** Yes. They are ESA-co-owned and ESA's answer
  is CC BY 4.0, which permits commercial use. They are **not** public domain: the credit line is a
  licence condition, and ESA requires it to stay attached to the image, not buried in a credits
  scene.

---

## 3. Staged inventory

All sizes, row counts and hashes VERIFIED by running `sha256sum`/`wc` on triton, 2026-09-12.
Paths are relative to `~/projects/CosmoSynth/data-staging/`.

| Path | Bytes | Rows | sha256 |
|---|---:|---:|---|
| `lvdb/dwarf_all.csv` | 433 436 | 911 (+header) | `c5c029dce9970e7b70fc329cc46dbf2ed02b308901579a3f2aa756887d542b26` |
| `lvdb/LICENSE.upstream` | 7 048 | — | `a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499` |
| `2mass-lga-irsa/lga_v2.csv` | 76 788 | 656 (+header) | `04a54f16682a3974c054df4337f1b99fce82268eb639f6ce548e58d4389bfc92` |
| `2mass-lga-irsa/column_descriptions.csv` | 3 053 | 83 (+header) | `7d5e76a0f6c5fed518b04f629c4ff847a954737ae00a075e3275bcbc788b0dba` |
| `2mass-xsc-irsa/xsc_rext20.csv` | 18 411 909 | 198 647 (+header) | `6b030c422987ebbf974589395ef05c2da913f5544616fc82138700ff6cbde95d` |
| `2mass-xsc-irsa/column_descriptions.csv` | 22 364 | 393 (+header) | `5667360cdac8c3262fa9424729fc5a1238c86229f89f6c6801f7c148701c2278` |
| `heroes-derived/heroes.json` | 39 066 | 31 galaxies | `552b097994e894508abb80b8c16cca715b712d782be0b420f71a88548d05d8a5` |
| `imagery-2mass-lga/*.jpg` (14 files) | 24 831 431 | — | per-file hashes in that directory's `SOURCE.txt` |

Plus `SOURCE.txt` / `LICENCE.txt` / `NOTES.md` in each directory, and
`heroes-derived/derive_heroes.py` (the generator, with a `--check` self-test).

Rejected sources, each with the same three documents and no data:
`REJECTED-ungc-vizier/`, `REJECTED-hyperleda/`, `REJECTED-ned/`, `REJECTED-sga2020/`.

### How the XSC was reduced

The full `fp_xsc` is **1 647 599 rows** (VERIFIED by `COUNT(*)`) across 393 columns — far past the
budget on a box at 97%. Reduction was done **server-side**, in the ADQL, two ways:

- row cut `r_ext > 20` arcsec → **198 647 rows** (12.1%). Counts at the alternatives, all VERIFIED:
  `r_ext > 30` → 49 788; `k_m_k20fe < 12.5` → 131 706.
- column cut: 15 of 393 columns.

Result: 18.4 MB downloaded instead of gigabytes. `df -h /` before the fetch showed 15–16 GB free,
above the 8 GB abort threshold. **VERIFIED.**

---

## 4. Attribution strings the app must show

Exact text, to be reproduced without alteration.

**Local Volume Database** (requested, not legally required — CC0 imposes no conditions):

> This work has made use of the Local Volume Database
> (Pace 2025, The Open Journal of Astrophysics, 8, 142).
> https://github.com/apace7/local_volume_database

**2MASS catalogue data** (LGA and XSC) — verbatim from
<https://irsa.ipac.caltech.edu/Missions/2mass.html>:

> This publication makes use of data products from the Two Micron All Sky Survey, which is a joint
> project of the University of Massachusetts and the Infrared Processing and Analysis
> Center/California Institute of Technology, funded by the National Aeronautics and Space
> Administration and the National Science Foundation.

**2MASS Large Galaxy Atlas specifically** — verbatim from
<https://irsa.ipac.caltech.edu/applications/2MASS/LGA/>:

> If you use LGA data, please cite both the journal article Jarrett et al. (2003) and the dataset
> Digital Object Identifier (DOI): 10.26131/IRSA122.

i.e. ship: `2MASS Large Galaxy Atlas: Jarrett et al. (2003), AJ 125, 525. DOI 10.26131/IRSA122.`

**IRSA** (the archive both 2MASS tables came from) — verbatim from
<https://irsa.ipac.caltech.edu/ack.html>:

> This research has made use of the NASA/IPAC Infrared Science Archive, which is funded by the
> National Aeronautics and Space Administration and operated by the California Institute of
> Technology.

**Any 2MASS image shown in the app** — verbatim from the 2MASS gallery policy, which requires it
whenever an image "is redisplayed or reproduced":

> Atlas Image mosaic obtained as part of the Two Micron All Sky Survey (2MASS), a joint project of
> the University of Massachusetts and the Infrared Processing and Analysis Center/California
> Institute of Technology, funded by the National Aeronautics and Space Administration and the
> National Science Foundation.

**If ESA/Hubble, ESO or NOIRLab imagery is ever used**: the per-image credit line exactly as printed
on that image's page (e.g. `ESA/Hubble & NASA`, `ESO/IDA/Danish 1.5 m/R. Gendler`,
`KPNO/NOIRLab/NSF/AURA`), shown **attached to the image**, not only in a credits scene — all three
licences require the credit to be visible and not disassociated from the image. None of the three
permit any statement implying their endorsement of a commercial product.

---

## 5. What this family actually produced, beyond clearance

### 5.1 The LVDB blank-value question, settled with real numbers

Earlier research recorded "542 default ellipticities and 689 default position angles" in a derived
Atlas file. Measured against LVDB v1.1.1 itself (**VERIFIED**, by reading the staged file):

| Column | blank | filled |
|---|---:|---:|
| `position_angle` | **682** | 229 |
| `ellipticity` | **536** | 375 |
| `rhalf` | 167 | 744 |
| `host` | 222 | 689 |
| `M_V` | 5 | 906 |
| both `ellipticity` and `position_angle` | — | **227** |

**These are not defaults. LVDB leaves the cell empty; the Atlas substituted a value downstream and
drew it as measured.** The fix is to keep blanks blank and set `EllUnknown` / `PaUnknown`.

Can they be filled from a clean source? Barely. Crossmatching all 911 LVDB dwarfs against the
staged XSC subset at 30 arcsec finds **92 counterparts**, of which **74** would fill a blank
position angle and **64** a blank ellipticity (**VERIFIED**, run on triton). That is 11% and 12%.
The remaining ~600 are below 2MASS's reach and must stay unknown. False-match rate at that
tolerance is ~2.6 × 10⁻⁴ per trial, i.e. ~0.24 expected false matches across 911 (**INFERRED**,
from the subset's source density of ~4.8 deg⁻²).

### 5.2 The hero galaxies — `heroes-derived/heroes.json`, 31 objects

Shape, size, magnitude and Hubble type from the LGA (Measured); distance from the median of that
host's LVDB satellites (Inferred, with N and MAD carried); LMC and SMC straight out of LVDB
(Measured, they are rows in `dwarf_all.csv`).

| Hero | N sats | derived d (kpc) | MAD | PA (E of N) | b/a | incl | M_K | shape OK? |
|---|---:|---:|---:|---:|---:|---:|---:|:--:|
| M31 | 39 | 751.6 | 35.4 | 45.0 | 0.473 | 64.1 | −23.34 | yes |
| M33 | 3 | 912.0 | 21.2 | 13.0 | 0.792 | 38.5 | −20.31 | **no** |
| M81 | 54 | 3698.3 | 147.6 | 141.5 | 0.517 | 60.9 | −23.91 | yes |
| M82 | 2 | 3614.1 | 0.0 | 65.0 | 0.396 | 69.6 | −23.10 | yes |
| NGC 5128 (Cen A) | 113 | 3681.3 | 0.0 | 47.5 | 0.957 | 17.2 | −23.84 | yes |
| M83 | 15 | 4943.1 | 178.8 | 47.5 | 0.825 | 35.2 | −23.73 | yes |
| NGC 253 | 22 | 3548.2 | 97.6 | 51.0 | 0.264 | 79.9 | −23.93 | yes |
| IC 342 | 7 | 3191.5 | 199.3 | 10.0 | 0.858 | 31.6 | −22.64 | **no** |
| M101 | 10 | 6826.5 | 107.7 | 28.5 | 0.979 | 12.0 | −23.23 | yes |
| LMC | — | **49.59 measured** | — | 47.24 | — | — | M_V −18.08 | LVDB |
| SMC | — | **62.81 measured** | — | *blank* | *blank* | — | M_V −16.79 | LVDB |

Plus 20 more Local Volume giants on the same footing (NGC 55, NGC 300, NGC 4945, Maffei 1/2,
Circinus, NGC 6946, M94, NGC 2403, NGC 4258, M51, NGC 3109, NGC 4449, NGC 7793, NGC 4826,
NGC 5055, NGC 4631, NGC 3621, NGC 1313, NGC 6503).

**Three traps this file defuses.** All **VERIFIED** by running the build:

1. **Position angles are not in [0,180).** 2MASS's `sup_pa` runs **−89 … +117.5**; LVDB's
   `position_angle` runs **−81 … +335** with 7 rows at or above 180 (the LMC is 227.24). Anything
   that assumes a convention draws those major axes at the wrong angle. Fold with `% 180`.
2. **`sup_ba` = 1.000 is a failed fit, not a round galaxy.** NGC 3109, an edge-on Magellanic
   irregular, gets `sup_ba` 1.000 and therefore **inclination 0° — face on** through the Hubble
   formula. 2MASS's ellipse fits latch onto bright knots in clumpy, low-surface-brightness discs.
   `heroes.json` carries a `shapeReliable` flag (`sup_ba` vs `k_ba` within 0.15, the two PAs within
   20°, `r_k20fe` ≥ 30″, `sup_ba` < 0.995) that rejects six of 29: **M33, IC 342, NGC 2403, M51,
   NGC 3109, NGC 1313** — precisely the big, face-on, clumpy ones. Where it is false, ship the row
   but flag the shape unknown.
3. **Inclination is meaningless for ellipticals and S0s.** Cen A (`S0;pec;Sy2`) comes out at 17°
   from its axis ratio; that is a projected isophote, not a disc tilt. Gate the inclination on the
   Hubble type.

**Where a kinematic override is still wanted.** M31's photometric b/a gives i ≈ 64°; H I kinematics
give ~77°, which is why the design made M31 an override. M33 is worse, because its shape fails the
reliability test entirely. Those overrides were **deliberately not written here**: a number copied
from memory with a citation attached is manufactured provenance. Whoever adds them must open the
papers. (**INFERRED** that the kinematic values differ in that direction — stated as a known open
item, not used anywhere.)

---

## 6. What I could not verify

1. **The AAS copyright cutover date.** <https://aas.org/policies/copyright-permissions> returned
   HTTP 403 to the fetcher; the statement that the AAS held copyright in pre-11-Oct-2021 AJ/ApJ
   articles and that authors retain CC BY 4.0 after it comes from search-result text, not the page.
   **INFERRED.** It does not change the UNGC verdict, which rests on VizieR's own terms alone.
2. **No IRSA page states a catalogue licence in words.** The decisive statement for both 2MASS
   tables is the data.gov licence field. IPAC's own pages say "may be used for any purpose without
   prior permission" for *images* and "released into the public domain" for *gallery images*; no
   IPAC page says the equivalent for catalogue rows. The legal reasoning that a machine-generated
   US-produced measurement table carries no copyright and no EU-style database right is **INFERRED**,
   not a quote.
3. **EDD / Cosmicflows** was not re-verified by me: `https://edd.ifa.hawaii.edu/` fails TLS
   verification from triton and from the laptop fetcher. A sibling family verified and blocked it
   the same day (`data-staging/REJECTED-cosmicflows4-edd/`, zero matches for "licen*", "copyright",
   "terms", "permission" on the page). I relied on that. **INFERRED at second hand.**
4. **Whether `lga_v2`'s 656 rows are exactly the "649 objects" the overview page advertises.** The
   count difference is 7 and the duplicates I can see are alias rows (M51 appears as `M51`, `M51A`
   and `M51B`). That explanation is **INFERRED**; the 656 is **VERIFIED**.
5. **SGA-2020's catalogue licence** — not stated on either page I read; classed CONDITIONAL rather
   than OK or BLOCKED.
6. **Whether HyperLEDA would grant permission on request.** Not attempted; "by permission" is
   blocked by the bar regardless.
7. The nine "commonly quoted" literature distances in `heroes-derived/NOTES.md` are recalled values
   used only as a sanity read of the derived medians. **INFERRED, and not used in any file.**

---

## 7. Ledger rows for `docs/data-sources.md`

```
### Local Volume Database (LVDB) v1.1.1 — https://github.com/apace7/local_volume_database
911 nearby dwarf galaxies: position, distance modulus, half-light radius, ellipticity, position
angle, M_V, host assignment, and a literature reference per value. Pinned at git tag v1.1.1
(= Zenodo DOI 10.5281/zenodo.21910097).
- Licence: CC0 1.0 Universal. VERIFIED from the repo's LICENSE file at the tag and the Zenodo
  licence field, both read 2026-09-12. No conditions; commercial redistribution permitted.
- Credit (requested, not required): "This work has made use of the Local Volume Database
  (Pace 2025, OJAp 8, 142). https://github.com/apace7/local_volume_database"
- Staged: data-staging/lvdb/ (433 436 B, sha256 c5c029dc…)
- Caveat: 682 of 911 position angles and 536 of 911 ellipticities are BLANK — unmeasured, not
  default. Keep them blank and flag them.

### 2MASS Large Galaxy Atlas v2 — IRSA table lga_v2, DOI 10.26131/IRSA122
656 large galaxies: Hubble type, J+H+Ks and Ks-only axis ratio and position angle, isophotal and
half-light radii, JHKs isophotal magnitudes. Fetched through the IRSA TAP service.
- Licence: US Government Works. VERIFIED from the data.gov licence field for this dataset
  (https://catalog.data.gov/dataset/2mass-large-galaxy-atlas-photometry), read 2026-09-12.
- Credit: the 2MASS survey acknowledgement + "Jarrett et al. (2003), DOI 10.26131/IRSA122" +
  the IRSA acknowledgement (exact strings in docs/licence/local-volume.md §4).
- Staged: data-staging/2mass-lga-irsa/ (76 788 B, sha256 04a54f16…)
- Replaces the Updated Nearby Galaxy Catalog, which is BLOCKED.

### 2MASS All-Sky Extended Source Catalog — IRSA table fp_xsc
Axis ratio and position angle for extended sources; the staged subset is the 198 647 rows with
r_ext > 20 arcsec, 15 of 393 columns, reduced server-side in ADQL from 1 647 599 rows.
- Licence: US Government Works. VERIFIED from the data.gov licence field
  (https://catalog.data.gov/dataset/2mass-all-sky-extended-source-catalog), read 2026-09-12.
- Credit: as for the LGA (2MASS survey + IRSA).
- Staged: data-staging/2mass-xsc-irsa/ (18 411 909 B, sha256 6b030c42…)
- Replaces VizieR VII/233/xsc. Same measurements, clean distributor.

### CosmoSynth hero-galaxy table — derived, data-staging/heroes-derived/heroes.json
31 Local Volume giants. Shape/size/magnitude/type from the LGA (Measured); distance from the median
distance of that host's LVDB satellites (INFERRED, with N and MAD); LMC and SMC from LVDB
(Measured). Carries a shapeReliable flag that rejects six 2MASS fits.
- Licence: CC0 (derivative of CC0 + US Government Works only).
- Staged: 39 066 B, sha256 552b0979…
- Every distance is Inferred. Do not present it as a measurement.

### Hero imagery — 2MASS LGA J+H+Ks mosaics
14 colour mosaics (M31, M33, M81, M82, Cen A, M83, NGC 253, IC 342, M101, NGC 55, NGC 300,
NGC 4945, Maffei 1, Maffei 2), 24.8 MB.
- Licence: public domain. VERIFIED: "The images in this gallery are released into the public
  domain." (2MASS gallery) and "images and video on the IPAC public web sites may be used for any
  purpose without prior permission" (IPAC image use policy), both read 2026-09-12.
- Credit REQUIRED whenever an image is shown: the "Atlas Image mosaic obtained as part of the Two
  Micron All Sky Survey…" string, in full.
- Also cleared for later use, all CC BY 4.0 with commercial use permitted and a visible
  per-image credit: ESA/Hubble, ESO, NOIRLab (pages read 2026-09-12).

### BLOCKED for the Local Volume — do not re-check
- Updated Nearby Galaxy Catalog (Karachentsev+2013) via CDS/VizieR: "The commercial usage of the
  data is subject to rules depending of the origin." (VizieR licence page, 2026-09-12). The
  authors' own SAO host states no terms at all. → data-staging/REJECTED-ungc-vizier/
- HyperLEDA: acknowledgement requested, no licence, no redistribution statement anywhere.
  → data-staging/REJECTED-hyperleda/
- NED / NED-D: "Copyright (c) 2026, California Institute of Technology" and no grant.
  → data-staging/REJECTED-ned/
- Siena Galaxy Atlas 2020: CONDITIONAL — imagery is CC BY 4.0, the catalogue's terms are unstated,
  and its parent sample derives from HyperLeda and NED. → data-staging/REJECTED-sga2020/
```

---

## 8. Consequences for `docs/design/2026-09-11-B-data.md` §2.11

The section needs rewriting; the specific edits:

| Design element | Change |
|---|---|
| Source `ungc` (`cdsarc…J/AJ/145/101/table1.dat`, tag `nc`) | **delete** — blocked |
| Source `xsc` (VizieR `VII/233/xsc`, tag `nc`) | **replace** with IRSA `fp_xsc` via TAP, tag `pd` |
| Source `lvdb` (`v1.1.1`, tag `cc0`) | unchanged; the tag is now VERIFIED to resolve, and `release_version.txt` does not exist at it |
| Build rule 2 (UNGC giants, M_K ≤ −19.5, Holmberg radius from a26) | **replace** with the LGA join in `heroes-derived/derive_heroes.py` |
| Build rule 3 (PA from the nearest 2MASX within 30″) | keep, but the LGA already has the PA for giants; the crossmatch is now only for LVDB dwarfs, where it fills 74 of 682 blanks |
| Build rule 5 (`giants_fallback.json`, ~20 hand rows, only in the `commercial` profile) | **promote**: there is no non-commercial profile any more, and `heroes.json` supersedes the hand file with 31 rows and real provenance |
| `sizeKind` 1 = "Holmberg" | now unused; the LGA's isophote is Ks 20 mag/arcsec², a different definition. Add kind 2 or retire kind 1 |
| `ttype` i8 from UNGC `TT` | the LGA gives a Hubble-type **string**; either widen the meta or write a documented, Inferred string→T mapping |
| Test "LVDB dwarf rows = 911" | VERIFIED, keep |
| Test "M31 d = 0.77 ± 0.04 Mpc (UNGC)" | **rebase** on the derived 751.6 ± 35.4 kpc, and relabel Inferred |
| Test "no LVDB blank became a number" | keep — this is the regression that matters, and §5.1 gives it exact numbers (682 PA / 536 ell blanks) |
