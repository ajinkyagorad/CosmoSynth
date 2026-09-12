# Stars, names and constellations — commercial licence audit

**Family:** STARS, NAMES AND CONSTELLATIONS
**Audited:** 2026-09-12 (all pages read that day, on triton, from the primary URL)
**Staging root:** `~/projects/CosmoSynth/data-staging/`
**Staged this pass:** 13.3 MB across 4 cleared sources + 6 rejection records
**Bar applied:** commercial distribution of a paid Quest app, worldwide, EU included.

Every claim below is marked **VERIFIED** (I fetched the page or ran the command myself on
2026-09-12) or **INFERRED** (reasoning on top of verified facts).

---

## 1. The headline

**The entire Hipparcos estate — Hipparcos, Tycho‑1, Tycho‑2, Hipparcos‑2, Gaia — is ESA
CC BY‑NC 3.0 IGO, and it is the root of every convenient star catalogue in amateur
astronomy, including the HYG database CosmoSynth ships today.** VERIFIED.

That one fact decides this whole family:

- `StreamingAssets/CosmoSynth/sky/stars.csstars` (119,613 stars) **cannot ship in a
  commercial build as it stands.** It is built from HYG, which is Hipparcos-derived.
- `docs/data-sources.md` is **wrong** where it records the Gaia Archive as "CC BY‑SA 3.0
  IGO". The CC BY‑SA 3.0 IGO licence is ESA's *media* policy for images and videos on
  esa.int. The *science archive data* is CC BY‑NC 3.0 IGO. VERIFIED, quotes in §2.
- `docs/data-sources.md` is **incomplete** where it records HYG as "CC BY‑SA 4.0 —
  attribution AND share-alike". That is astronexus's grant, and it is real; but astronexus
  cannot grant commercial rights in ESA's astrometry, which they do not hold.

**The single highest-value action in this audit is one email.** ESA's own terms name the
mechanism, verbatim:

> "Prior to any commercial use by the User of any Data or Data Product, including any use
> or application that directly or indirectly generates a financial gain, a detailed request
> for authorisation/licence shall be made by the User by sending email to
> data.licences@esa.int."
> — <https://www.cosmos.esa.int/web/esdc/terms-and-conditions>, read 2026-09-12. VERIFIED.

One licence grant from ESA unblocks Gaia DR3, Hipparcos, Tycho‑2 **and** HYG at once, and
this entire rebuild becomes unnecessary. Everything below §3 is what to do if that email is
not sent, or is refused.

---

## 2. Decision table

| # | Source | Verdict | Decisive evidence (verbatim), URL, date read |
|---|---|---|---|
| 1 | **ESA Gaia DR3** (Gaia Archive, ESAC) | **BLOCKED** | "Gaia data are distributed under the CC BY-NC 3.0 IGO license." — <https://www.cosmos.esa.int/web/gaia-users/license>, 2026-09-12. VERIFIED |
| 2 | **ESA Space Science Archives** (the umbrella that governs #1, #3) | **BLOCKED** | "Data hosted in the ESA Space Science Archives are distributed under the CC BY-NC 3.0 IGO licence." and "Prior to any commercial use … a detailed request for authorisation/licence shall be made … to data.licences@esa.int." — <https://www.cosmos.esa.int/web/esdc/terms-and-conditions>, 2026-09-12. VERIFIED |
| 3 | **Hipparcos & Tycho Catalogues** (VizieR I/239, ESA 1997) | **BLOCKED** | `License: CC-BY-NC-3.0 IGO` — <https://cdsarc.cds.unistra.fr/ftp/I/239/ReadMe>, 2026-09-12 (grepped the fetched file). VERIFIED |
| 4 | **Tycho‑2** (VizieR I/259, Høg+ 2000) | **BLOCKED** | `License: CC-BY-NC-3.0 IGO` — <https://cdsarc.cds.unistra.fr/ftp/I/259/ReadMe>, 2026-09-12. Its own abstract: positions "based on precisely the same observations as the original Tycho Catalogue … collected by the star mapper of the ESA Hipparcos satellite". VERIFIED |
| 5 | **Hipparcos, the New Reduction** (VizieR I/311, van Leeuwen 2007) | **BLOCKED** | No `License:` line anywhere in the ReadMe (grepped, 2026-09-12) — no stated redistribution grant, and ESA lists Hipparcos in its archive credit table ("Hipparcos  Credit: ESA"). VERIFIED |
| 6 | **HYG v4.4** (astronexus, Codeberg) | **REJECTED** (grant is CC BY‑SA 4.0; upstream is #3) | "This work is licensed under a Creative Commons Attribution-ShareAlike 4.0 International License." — <https://codeberg.org/astronexus/hyg/raw/branch/main/LICENSE>, 2026-09-12. README: "HYG combines every identifiable star in the HIPPARCOS, Yale Bright Star, and Gliese (nearby star) catalogs". VERIFIED |
| 7 | **AT‑HYG v4.0** (astronexus) | **BLOCKED** (worse than #6) | Same CC BY‑SA 4.0 grant; README: "Nearly 99% of stars in AT-HYG have distance data from Gaia DR3 and 92% have full 3D velocity information from Gaia DR3." — <https://codeberg.org/astronexus/athyg/raw/branch/main/README.md>, 2026-09-12. VERIFIED |
| 8 | **CDS VizieR — AAS journal tables** (J/ApJ, J/ApJS, J/AJ) | **BLOCKED** | "Tabular data, spectra or images coming from AAS journals (J/ApJ, J/ApJS, J/AJ) are under CC-BY-NC licence" — <https://cds.unistra.fr/vizier-org/licences_vizier.html>, 2026-09-12. **Earlier project research was right.** VERIFIED |
| 9 | **CDS VizieR — A&A tables** (J/A+A) | **BLOCKED** under our bar | "Tabular data, spectra or images coming from A&A (J/A+A) are free for a scientific usage." — same page, same date. "Free for scientific use" is exactly the wording the bar blocks. VERIFIED |
| 10 | **CDS VizieR — metadata & the service itself** | **BLOCKED** for redistribution | "The Information system including the metadata is private and reserved to the CDS." and "The commercial usage of the data is subject to rules depending of the origin." — same page, same date. VERIFIED |
| 11 | **SIMBAD** | **BLOCKED** for bulk redistribution | The only terms text on the guide is an acknowledgement request: "If the Simbad database was helpful for your research work the following acknowledgment would be appreciated…" — <https://simbad.cds.unistra.fr/guide/>, 2026-09-12. A citation request is not a redistribution licence. VERIFIED |
| 12 | **Yale Bright Star Catalogue 5th rev.** — VizieR copy V/50 | **BLOCKED** | No `License:` line in the ReadMe (grepped 2026-09-12). Use the NASA copy instead, row 13. VERIFIED |
| 13 | **Yale BSC5p — NASA HEASARC copy** | **OK — staged** | "HEASARC materials are all available freely for your use." — <https://heasarc.gsfc.nasa.gov/docs/heasarc/data_policy.html>, 2026-09-12. The same page carves ESA/JAXA cases out *explicitly* ("XMM: … for non-commercial purposes"); BSC5p is not in that carve-out. VERIFIED |
| 14 | **Stellarium "modern" sky culture** (88 figures + IAU boundaries) | **CONDITIONAL — staged** (CC BY‑SA 4.0) | "## License / Text and data: CC BY-SA 4.0 / Illustrations: Free Art License" — <https://raw.githubusercontent.com/Stellarium/stellarium/master/skycultures/modern/description.md>, 2026-09-12. Repo root `COPYING` is GPL‑2, which governs the *program*, not this data — verified by fetching it the same day. VERIFIED |
| 15 | **Constellation boundaries upstream** (pbarbier.com `edges_18.txt`, named in Stellarium's `edges_source`) | **CONDITIONAL** (CC BY‑SA 4.0) | "All the original content on this site is licensed under a Creative Commons Attribution-ShareAlike 4.0 International License" — <https://pbarbier.com/terms.html>, 2026-09-12. Copyright notice: "© 2012–2022 by Pierre Barbier". VERIFIED |
| 16 | **VizieR VI/49 Constellation Boundary Data** (Davenhall+ 1989) | **BLOCKED**, substituted by #14/#15 | No `License:` line in the ReadMe (grepped 2026-09-12). Nothing lost — same Delporte (1930) boundaries arrive licensed in #14. VERIFIED |
| 17 | **IAU‑CSN, IAU WGSN approved star names** | **OK — staged** | "All IAU-produced products (Images, Videos, Texts) are released under Creative Commons Attribution (i.e. free to use in all perpetuity, world-wide, as long as the source is mentioned)." — header of <https://www.pas.rochester.edu/~emamajek/WGSN/IAU-CSN.txt>, 2026-09-12. VERIFIED |
| 18 | **UCAC4** (U.S. Naval Observatory, Zacharias+ 2013) | **OK — staged (probe)** | Federal record's own metadata: `"license": "https://www.usa.gov/government-works"` — <https://catalog.data.gov/dataset/the-fourth-u-s-naval-observatory-ccd-astrograph-catalog>, 2026-09-12. Catalogue readme: "The UCAC4 comes free of charge." — <https://irsa.ipac.caltech.edu/data/USNO/UCAC4/readme_u4.txt>, 2026-09-12. VERIFIED |
| 19 | **Wikidata** (possible CC0 HIP↔HD identifier bridge) | **OK in principle, coverage UNVERIFIED** | "All data in Wikidata has a CC0 license." — <https://www.wikidata.org/wiki/Wikidata:Licensing>, 2026-09-12. Caveat on the same page: contributors apply CC0 "irrespective of whether the user has any claim of ownership to the data they contribute." VERIFIED (licence) / UNVERIFIED (coverage — the SPARQL count timed out) |
| 20 | **General esa.int website terms** (checked because the project cited them) | **BLOCKED** | "The contents of the ESA website are intended for the personal and non-commercial use of its users." — <https://www.esa.int/Services/Terms_and_conditions>, 2026-09-12. This is *stricter* than the archive terms, not the CC BY‑SA 3.0 IGO the project docs assumed. VERIFIED |

### The rows that were the point of this task

**Gaia's licence, settled.** Earlier research said CC BY‑NC 3.0 IGO; the project docs said
CC BY‑SA 3.0 IGO. **Earlier research was right.** The Gaia licence page and the ESDC terms
page say the same thing and do not differ from each other — I checked specifically for that,
and the Gaia page defers to the ESDC terms rather than stating its own. VERIFIED.

**VizieR AAS tables, settled.** Earlier research said CC BY‑NC. **Confirmed verbatim.** Note
the CDS page's link text says `by-nc-nd`, which if taken literally would also forbid
derivatives — one more reason not to touch that class. VERIFIED.

---

## 3. Staged inventory

Root: `/home/triton/projects/CosmoSynth/data-staging/`. Every directory has
`SOURCE.txt` + `LICENCE.txt` + `NOTES.md`.

| Path | Bytes | sha256 | Rows / count |
|---|---|---|---|
| `iau-wgsn-star-names/IAU-CSN.txt` | 71,800 | `84fac0c90f1b19abc491c2793469e0caa7b003ad1bc93a790ca41147010d0eb0` | 451 named stars (593 lines) |
| `stellarium-modern-skyculture/index.json` | 205,767 | `1f2f5ffd6c9e25a7d0dcfdbf1f756e2db03dd3b8ed4ec016a2839b09f6b0fe1e` | 88 constellations, 219 strokes, 695 segments, 710 distinct HIP ids, 92 asterisms, 660 common names, IAU `edges` at epoch B1875 |
| `stellarium-modern-skyculture/description.md` | 53,269 | `4b58344f63168d7c816fdfef405103b5770fda2e1f4a4bf5c8871041ce042586` | licence text lives here |
| `yale-bsc5-heasarc/heasarc_bsc5p.tdat.gz` | 913,895 | `122628cde2d8bedf7e16ddf5f888167ac58c04b5592d6155408ce297f3073931` | 9,110 rows, 53 declared fields |
| `usno-ucac4/z500` | 11,671,452 | `1e24505c66d7c78252277b5762b4a053e313d8375846713bf0a7872f59cfa849` | 149,634 records (one 0.2° dec zone, format probe only) |

Total staged: **13.3 MB** of a 500 MB budget. Rejection records: 6 directories, 16 KB each,
documentation only, **0 bytes of rejected data fetched**.

Disk on triton after this work: 15 GB free of 460 GB (97% used) — unchanged to within a
gigabyte. `df -h /` run before and after. VERIFIED.

### The UCAC4 probe — the one runnable check in this audit

Decoded `z500` with a hand-written 78-byte numpy dtype (no astropy on the box), on triton:

```
records in zone : 149634
record length   : 78 bytes (spec says 78)
RA range (deg)  : 0.0013 .. 359.9946
Dec range (deg) : 9.8000 .. 10.0000
rows with APASS V: 80891
rows with V<9.5 : 301
first row: ra=0.001330 deg dec=9.805404 deg B=20.000 V=20.000 pmRA=-4.9 mas/yr pmDec=-19.4 mas/yr
```

VERIFIED. 301 stars at V<9.5 in one of 900 zones extrapolates to roughly **271,000 stars
all-sky brighter than V=9.5** — more than the 120k the app wants, from a public-domain
source, with proper motions. INFERRED (the extrapolation; zone 500 is one declination band
and star density varies with galactic latitude). Only 54% of rows carry an APASS magnitude
at all, which is the real coverage limit, not the count.

---

## 4. What to ship, in order of laziness

### Option A — email ESA (recommended, one action)

`data.licences@esa.int`, describing CosmoSynth and asking for commercial authorisation for
Gaia DR3 and Hipparcos/Tycho‑2. If granted: keep HYG, keep `stars.csstars`, change nothing
in the build, add the credit lines in §5, and this document becomes history. ESA's terms
name this address as *the* mechanism, so it is a normal request and not a favour.

Until a written grant exists, the HYG-derived `stars.csstars` must not ship in a paid build.

### Option B — rebuild clean, layered (what the staged data supports)

| Layer | Source | Verdict | What it gives |
|---|---|---|---|
| V ≤ 6.5, ~9,110 stars | **Yale BSC5p** (HEASARC) | OK | RA/Dec J2000 + B1950, **parallax**, pmRA/pmDec, V, B−V, U−B, spectral type, HD / HR / SAO / FK5 / DM ids, Bayer+Flamsteed names |
| 6.5 < V < 9.5, ~260k stars | **UCAC4** (USNO) | OK | position, proper motion, APASS B and V. **No parallax.** |
| 451 proper names | **IAU‑CSN** | OK | name, HIP, HD, J2000 position, V |
| 88 figures + IAU boundaries | **Stellarium "modern"** | CONDITIONAL (BY‑SA) | HIP-keyed polylines, B1875 boundary edges |

**What Option B costs, honestly:**

- **All-sky parallaxes are gone.** HYG gives a measured distance for ~109k stars; BSC5 gives
  ground-based parallaxes for the bright few thousand and UCAC4 gives none. The 3‑D star
  field becomes a 2‑D sky sphere with a shallow near-star volume. For an app whose whole
  premise is Earth → Solar System → local stars → galaxy, this is the expensive loss.
- **3‑D space velocities are gone.** AT‑HYG's vx/vy/vz come from Gaia radial velocities.
  BSC5 has radial velocity only.
- **Gliese cross-ids and the nearby-star set are gone.** VizieR V/70A (Gliese CNS3) has no
  licence line either.
- **HIP↔position is gone**, which is the practical problem below.

### The HIP problem, measured

Every Stellarium figure vertex is a HIP number. HIP identity as an *integer key* is a fact
and fine to keep; HIP → (RA, Dec) is the ESA table and is not.

I measured the cheapest bridge: of the **710** distinct HIP ids used by the 88 figures,
**244 (34.4%)** can be resolved to an HD number through IAU‑CSN alone. VERIFIED (script
`~/scratch-cosmolic/bridge2.py`, run 2026-09-12). **So IAU‑CSN is not enough** — the
optimistic version of this claim would have shipped 88 broken figures.

Three ways forward, cheapest first:

1. **Wikidata SPARQL for a HIP→HD table**, CC0. Licence verified; coverage not — the count
   query timed out against WDQS on 2026-09-12. Query kept in
   `stellarium-modern-skyculture/NOTES.md`. If it covers all 710, this is a one-off offline
   build step and the figures work unchanged.
2. **Re-author the 88 figures over BSC5 HR numbers.** 695 segments of authored work, owned
   outright, no share-alike, no bridge needed. For a stylised MR gallery that also ships
   fictional constellations, this is arguably the right art-direction call anyway — and it
   is the only option with zero licence surface.
3. **Option A**, which dissolves the problem.

---

## 5. Attribution strings that must appear in the app's credits

Copy these verbatim. Only the ones for sources actually shipped.

```
Bright-star data: Yale Bright Star Catalogue, 5th Revised Edition
(Hoffleit & Warren 1991), via NASA HEASARC.

This research has made use of data and/or software provided by the High Energy
Astrophysics Science Archive Research Center (HEASARC), which is a service of the
Astrophysics Science Division at NASA/GSFC.

Star positions and proper motions: UCAC4, U.S. Naval Observatory
(Zacharias et al. 2013, AJ 145, 44). Public domain (U.S. Government work).

Star names: IAU Catalog of Star Names (IAU-CSN), IAU Division C Working Group on
Star Names (WGSN). CC BY.

Constellation figures and boundaries: Stellarium sky culture "modern"
(Stellarium's team), CC BY-SA 4.0 - https://creativecommons.org/licenses/by-sa/4.0/
Constellation boundary data (c) 2012-2022 Pierre Barbier, CC BY-SA 4.0.
```

If Option A is taken, add whichever apply — these are ESA's own credit lines, verbatim from
the credit table on the ESDC terms page (read 2026-09-12):

```
Gaia - Credit: ESA, Gaia DPAC
Hipparcos - Credit: ESA
```

And if HYG is kept under an ESA grant:

```
HYG star database v4.4 (astronexus), CC BY-SA 4.0 -
https://creativecommons.org/licenses/by-sa/4.0/
```

### What CC BY‑SA 4.0 obliges us to publish, exactly

For the Stellarium-derived figure/boundary file only:

The baked figure file is **Adapted Material** under CC BY‑SA 4.0 §3(a)/§3(b). We must
(a) credit Stellarium's team and Pierre Barbier, (b) name the licence and link it,
(c) offer **that one file** under CC BY‑SA 4.0 or a compatible licence — in practice, put
the file at a URL named in the store listing — and (d) apply no DRM to that file that
restricts the licensed rights.

It does **not** copyleft the Unity app, the shaders, the other data files, or the star
table. The app is a *Collection* that contains the adaptation, not an adaptation of the
constellation data. INFERRED from the licence text; this is the standard reading, but it is
the one sentence in this document worth a lawyer's eye before launch.

Practical shape: keep the figure file verbatim and identifiable in `StreamingAssets`, ship a
`LICENSES/` folder in the APK, publish the same file at a stable URL.

---

## 6. What I could not verify

1. **Whether ESA would grant a commercial licence, on what terms, or how fast.** Not
   knowable without sending the email. UNVERIFIED.
2. **Wikidata HIP↔HD coverage.** The WDQS SPARQL count timed out on 2026-09-12. The CC0
   licence is verified; the coverage number is not. UNVERIFIED.
3. **Yale University's position on BSC5's compilation copyright.** No statement found either
   reserving or granting rights; NASA's free-use statement is the grant relied on. I searched
   the HEASARC and Harvard TDC pages and found nothing from Yale. UNVERIFIED — this is the
   one cleared source with residual risk, and it is thin risk (9,110 rows of positional
   facts, 1991, redistributed by a US federal agency for three decades).
4. **Whether all 710 Stellarium figure stars are within BSC5's V≤6.5 limit.** Untested; it
   needs the HIP bridge from item 2 first. UNVERIFIED.
5. **Whether the APASS photometry bundled inside UCAC4 carries AAVSO terms of its own.** I
   cleared UCAC4 as a USNO product; I did not separately clear APASS. If we take the APASS
   B/V columns, clear <https://www.aavso.org/apass> first. UNVERIFIED.
6. **The exact current IAU‑CSN edition.** The staged file's header says "Last updated
   2022-04-04" and the IAU URL it cites (`iau.org/public/themes/naming_stars/`) 404s as of
   2026-09-12; the WGSN's live page is now `exopla.net/star-names/`. Newer approvals may
   exist. Re-fetch and diff before ship. VERIFIED that the URL 404s; UNVERIFIED whether a
   newer edition exists elsewhere.
7. **Star-name and figure sets for the *fictional* worlds.** Out of scope here; but note the
   design advantage — authored constellations and authored names have no licence surface at
   all, which makes Option B's item 2 (re-author the 88 figures) cheaper than it looks,
   because that pipeline has to exist anyway.

---

## 7. Ledger rows for `docs/data-sources.md`

Paste into the table under "Planned, not yet integrated", and **correct the two existing
rows** flagged in §1.

| Data | Source | Licence note |
|---|---|---|
| Stars — bright sky (9,110, V≤6.5) with parallax, B−V, HD/HR/SAO | [Yale BSC5p via NASA HEASARC](https://heasarc.gsfc.nasa.gov/W3Browse/star-catalog/bsc5p.html) | **OK.** "HEASARC materials are all available freely for your use" (data_policy.html, read 2026-09-12). Staged at `data-staging/yale-bsc5-heasarc/`. Residual: Yale's 1991 compilation rights never stated either way |
| Stars — telescopic sky (≈260k to V=9.5), position + proper motion + APASS B,V | [UCAC4, U.S. Naval Observatory](https://irsa.ipac.caltech.edu/data/USNO/UCAC4/) | **OK — US Government work.** data.gov record states `license: usa.gov/government-works` (read 2026-09-12). **No parallaxes.** Clear APASS separately if its columns are used. Probe staged at `data-staging/usno-ucac4/` |
| Star proper names (451, IAU-approved) | [IAU‑CSN, IAU WGSN](https://www.pas.rochester.edu/~emamajek/WGSN/IAU-CSN.txt) | **OK — CC BY** per the file's own header (read 2026-09-12). Carries HIP **and** HD **and** J2000 position. Staged at `data-staging/iau-wgsn-star-names/` |
| 88 constellation figures + IAU boundaries | [Stellarium "modern" sky culture](https://github.com/Stellarium/stellarium/tree/master/skycultures/modern) | **CONDITIONAL — CC BY‑SA 4.0** ("Text and data: CC BY-SA 4.0", description.md, read 2026-09-12). Share-alike attaches to the baked figure file only. Figures are HIP-keyed: needs a HIP→HD bridge or re-authoring. Illustrations (Free Art License) NOT taken. Staged at `data-staging/stellarium-modern-skyculture/` |
| ~~Stars (deeper than HYG) — ESA Gaia Archive~~ | ~~CC BY‑SA 3.0 IGO~~ | **CORRECTION: CC BY‑NC 3.0 IGO. BLOCKED for commercial use.** "Gaia data are distributed under the CC BY-NC 3.0 IGO license" (cosmos.esa.int/web/gaia-users/license, read 2026-09-12). Commercial use requires written authorisation from data.licences@esa.int |
| ~~HYG star database — CC BY‑SA 4.0~~ | ~~astronexus~~ | **CORRECTION: the CC BY‑SA 4.0 grant is real but cannot convey commercial rights in the Hipparcos astrometry underneath it (ESA CC BY‑NC 3.0 IGO, VizieR I/239 ReadMe, read 2026-09-12). `stars.csstars` must not ship commercially until ESA licenses Hipparcos to us or the catalogue is rebuilt.** See `data-staging/REJECTED-astronexus-hyg/` |
| Star identity — SIMBAD | [CDS](https://simbad.cds.unistra.fr/simbad/) | **BLOCKED for redistribution.** Only terms text is a citation request; CDS states "The Information system including the metadata is private and reserved to the CDS" (vizier-org/licences_vizier.html, read 2026-09-12). Use as a build-time research aid only |
| Any VizieR J/… journal table | [CDS VizieR](https://vizier.cds.unistra.fr/) | **BLOCKED.** AAS tables "are under CC-BY-NC licence"; A&A tables "are free for a scientific usage" (both verbatim, licences_vizier.html, read 2026-09-12) |
| Identifier cross-reference (HIP↔HD) | [Wikidata](https://www.wikidata.org/) | **OK — CC0** ("All data in Wikidata has a CC0 license", Wikidata:Licensing, read 2026-09-12). Coverage for our 710 figure stars UNVERIFIED (WDQS query timed out) |

---

## 8. Rejection records on disk

So nobody re-checks these:

- `data-staging/REJECTED-esa-gaia-dr3/`
- `data-staging/REJECTED-esa-hipparcos-tycho/` (covers I/239, I/259, I/311)
- `data-staging/REJECTED-astronexus-hyg/`
- `data-staging/REJECTED-astronexus-athyg/`
- `data-staging/REJECTED-cds-vizier-simbad/`
- `data-staging/REJECTED-constellation-boundaries-vizier/`

Each has the verbatim quote, the URL, the date, and — where one exists — the substitute.

One reusable fact recorded in `REJECTED-cds-vizier-simbad/NOTES.md`: VizieR ReadMe files
carry a machine-greppable `License:` line **only when a licence exists**. Of 21 star-relevant
catalogues grepped on 2026-09-12, exactly two had one (I/239 and I/259, both
CC‑BY‑NC‑3.0 IGO) and nineteen had none. "No licence line" means *go ask the producer*, not
*it is free*. VERIFIED.
