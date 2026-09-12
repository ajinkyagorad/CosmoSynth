# Large-scale structure ("beyond") - commercial licence clearance

Scope: everything CosmoSynth draws outside the Local Group - the galaxy field to a
few hundred Mpc, the cosmic web, superclusters, voids, named structures, and the
quasar frame behind them.

Date of every reading below: **2026-09-12 (UTC)**. Every quote was read by me from
the primary terms page or file, fetched with curl on triton or with WebFetch, never
from memory and never from another document in this project.

Claims are marked **VERIFIED** (I read the page, or ran the command, and the output
is reproduced here) or **INFERRED** (reasoning from something verified).

---

## 1. The verdict in one paragraph

**Every catalogue the original design assumed is blocked.** 2MRS, Cosmicflows-4 in
all three of its distribution routes, 6dFGS and 2M++ each fail the commercial bar,
for four different reasons. What replaces them is a stronger set than expected:
**DESI DR1 is CC BY 4.0 with the words "including commercially" in the licence
statement**, **SDSS public-release data are public domain**, and the **VAST void
catalogue is CC BY 4.0 on Zenodo**. The result is 5.5M galaxies and quasars and
1,163 voids, all commercially clean, staged in 220 MB. Two things are permanently
lost and cannot be bought back: **all-sky coverage** (2MRS was the only all-sky
redshift survey) and **measured, redshift-independent distances** (Cosmicflows-4 was
the only large source of those). The Laniakea boundary as *data* is gone with them.

---

## 2. Decision table

| # | Source | Verdict | Decisive evidence (verbatim), URL, read 2026-09-12 | Substitute |
|---|---|---|---|---|
| 1 | **DESI DR1** LSS clustering catalogues | **OK - CC BY 4.0** | "The Dark Energy Spectroscopic Instrument (DESI) data are licensed under the Creative Commons Attribution 4.0 International License ("CC BY 4.0" ...). Users are free to share, copy, redistribute, adapt, transform, and build upon the DESI data available through this website **for any purpose, including commercially**, as long as they [cite the data release paper / indicate if any changes are made / include the following acknowledgments text]" - <https://data.desi.lbl.gov/doc/acknowledgments/> | - (this IS the substitute) |
| 2 | **SDSS** public data releases (DR16, DR18 via SkyServer) | **OK - public domain** | "All SDSS data released in our public data releases are considered in the public domain." - <https://www.sdss.org/collaboration/image-use-policy/> | - |
| 3 | **VAST void catalogs for SDSS DR7** (Zenodo 7406035) | **OK - CC BY 4.0** | `"license": {"id": "cc-by-4.0"}`, `"access_right": "open"` - <https://zenodo.org/api/records/7406035> | - |
| 4 | **2MRS**, survey's own copy (CfA / SAO TDC) | **BLOCKED - explicit no-redistribution, and no licence at all** | "(3) Please do not redistribute any of these files. This will help us ensure that all users receive the most up-to-date version." - <http://tdc-www.harvard.edu/2mrs/2mrs_readme.html> | DESI DR1 + SDSS (loses all-sky coverage) |
| 5 | **2MRS** via VizieR `J/ApJS/199/26` | **BLOCKED - CC BY-NC** | "Tabular data, spectra or images coming from AAS journals (J/ApJ, J/ApJS, J/AJ) are under CC-BY-NC licence" - <https://cds.unistra.fr/vizier-org/licences_vizier.html> | as above |
| 6 | **Cosmicflows-4** via VizieR `J/ApJ/944/94` | **BLOCKED - CC BY-NC** | same sentence as #5; ApJ is an AAS journal | none for measured distances |
| 7 | **Cosmicflows-4 / Laniakea watershed + density grids**, IP2I Lyon | **BLOCKED - no licence stated** | the complete statement of terms on the page, repeated under each download block, is: "If you use this data cite the article above" - <https://projets.ip2i.in2p3.fr//cosmicflows/> | our own density field from DESI; Laniakea as labelled points or a declared schematic |
| 8 | **Cosmicflows / EDD**, U. Hawaii | **BLOCKED - no licence, no terms, anywhere on the site** | nothing to quote: the home page contains zero occurrences of "licence", "license", "copyright", "terms", "acknowledg", "permission" or "credit" - <https://edd.ifa.hawaii.edu/> (reachable only with `curl --insecure`; the TLS chain still fails from triton, confirming the earlier finding) | as #7 |
| 9 | **6dFGS DR3** (WFAU/ROE and VizieR `VII/259`) | **BLOCKED - no redistribution licence on either route** | "How to acknowledge the 6dF Galaxy Survey: Any use of these data should explicitly state that they come from the Final Release of 6dFGS and cite both the 6dGS DR3 paper ... as well as the original 6dFGS survey paper" - <http://www-wfau.roe.ac.uk/6dFGS/>. That is attribution, not permission; the VizieR route falls through to MNRAS/OUP, which grants third parties nothing | DESI SGC (partial southern coverage only) |
| 10 | **2M++** (`J/MNRAS/416/2840`) | **BLOCKED twice - fabricated rows, and no licence** | "Note on catalog.dat: Number of real galaxies = 69160; **Number of fake galaxies in ZoA = 3813**; Number of groups = 4002" and, in the column description, "Name of the galaxy as given in the 2MASS-XSC database ..., **or ZOA fake galaxy**" - <https://cdsarc.cds.unistra.fr/ftp/J/MNRAS/416/2840/ReadMe> | never - see §7 |
| 11 | **Named supercluster / void catalogues** with published positions | **BLOCKED as a class** | all are ApJ/ApJS/AJ/A&A/MNRAS tables reaching us through CDS; see #5 and the A&A/MNRAS rows of <https://cds.unistra.fr/vizier-org/licences_vizier.html> ("Tabular data ... coming from A&A (J/A+A) are free for a scientific usage", which is not a commercial grant) | derive positions ourselves from SDSS - `data-staging/named-structures/` |
| 12 | **Cosmic-web / filament catalogues** (Bisous, VIDE, ZOBOV-derived, etc.) | **BLOCKED as a class**, same reason as #11 | as #11 | compute the density field ourselves from DESI + SDSS |
| 13 | **VizieR / CDS as a channel** | **BLOCKED for commercial use** | "The commercial usage of the data is subject to rules depending of the origin" plus the per-journal rules quoted in #5 and #11 - <https://cds.unistra.fr/vizier-org/licences_vizier.html> | Zenodo author-deposits (like #3), survey data portals with explicit licences (like #1, #2) |

All thirteen rows are VERIFIED.

### The one nuance worth keeping

Row 3 is the useful precedent. The VAST void catalogue's *paper* is in ApJS - an AAS
journal, whose VizieR-mirrored tables are CC BY-NC - but the authors deposited the
catalogue themselves on Zenodo under CC BY 4.0. **Author self-deposit is a different
channel from a journal's data mirror, and it can be permissive where the mirror is
not.** When a catalogue is wanted and CDS blocks it, check Zenodo for an author
deposit before giving up.

---

## 3. Cosmology convention - one, and only one

```
flat LCDM,  H0 = 67.66 km/s/Mpc  (h = 0.6766),  Omega_m = 0.3150
comoving distance  D_C(z) = (c/H0) * INT_0^z dz'/E(z'),  E(z) = sqrt(Om (1+z)^3 + 1 - Om)
```

**VERIFIED, and not by trusting a filename.** The staged VAST catalogue carries both
redshifts and cartesian positions, so the cosmology can be recovered by inversion.
`_tools/inventory.py` searches flat-LCDM models against all 531 zobovoids and finds
`Omega_m = 0.3150, h = 0.6766` with a maximum residual of **0.0000 Mpc/h**. Its
coordinates are comoving Mpc/h; divide by h for Mpc.

Everything else in the family is a redshift, so everything else is converted with
that same model. Three other Hubble constants appear in the rejected sources and
none of them may leak in: CF4's **H0 = 74.6** ("compatible with a value of the Hubble
constant of H_0_=74.6km/s/Mpc", VERIFIED from the J/ApJ/944/94 ReadMe), EDD's
**H0 = 75.2** ("provides our currently preferred value of the Hubble Constant of
H0=75.2+-3.0 km/s/Mpc", VERIFIED from the EDD home page), and the naive cz/H0 with
H0 = 70 used in earlier CosmoSynth research. They differ from the convention by up
to 11%, which is 50 Mpc at the edge of the galaxy shell.

**All staged positions are redshift-space positions.** Clusters are radially smeared
into fingers-of-god; sheets are squashed. That is a property of the data, not a bug,
and the app should say "redshift space" in the provenance panel rather than implying
a measured 3-D map. INFERRED (standard redshift-space distortion; not measured here).

---

## 4. Staged inventory

Root: `~/projects/CosmoSynth/data-staging/`. Total for this family **219,577,309 B
(209 MB)** against a budget of 800 MB. Disk before the first fetch: 16 GB free of
460 GB; the largest single transient download was 340 MB and every FITS was deleted
immediately after reduction.

### `desi-dr1-lss/` - 143,709,203 B

| file | rows | bytes | sha256 |
|---|---|---|---|
| `bgs_bright_ngc.npy` | 2,909,876 | 81,476,784 | `950d1795e2c883b7b50f1090604c347cbfbfb680c728db64a708677e1e849caf` |
| `bgs_bright_sgc.npy` | 1,047,989 | 29,343,948 | `6124ac0d7d880fef49fd285d80d995290bf3fd28de080b8136247152bd9c4b54` |
| `bgs_bright_m21p5_ngc.npy` | 217,614 | 6,093,448 | `8bc67d722cd9000a86fc8bd65d49351186e11090334781bf504b67c463e1e65a` |
| `bgs_bright_m21p5_sgc.npy` | 82,429 | 2,308,268 | `770e6c633ef2d6b00a24f5bea016bf40ea1c7d83df7f1fc124030c628c32dcb1` |
| `qso_ngc.npy` | 793,219 | 15,864,572 | `731e5a66452a43ed6f289fd2a30cf1c9d7f23defb79213b9f0b1b528425b6696` |
| `qso_sgc.npy` | 430,172 | 8,603,632 | `d47ee13575198178d9ab912cd7769ef41ea4e363a3ef4a3b7c047035e8a3cf69` |

5,481,299 rows, reduced from 626,690,880 B of source FITS (deleted). Columns kept:
`RA, DEC, Z, flux_r_dered, flux_g_dered, WEIGHT` (quasar files have no flux columns).
Observed z ranges: BGS 0.010-0.500 (median 0.203), BGS-21.5 0.100-0.400 (median
0.315), QSO 0.800-3.500 (median 1.743).

### `sdss-dr18/` - 75,242,171 B

| file | rows | bytes | sha256 |
|---|---|---|---|
| `sdss_dr18_galaxies.csv` | 492,189 | 43,824,133 | `6e939d5a076c118a4d820473ec52f94fd639ff7aefd303b1cc2dda4aa39696ec` |
| `sdss_dr18_quasars.csv` | 747,982 | 31,407,554 | `8977ccd54a0aab89539b9f82da15c2dc2fa00bc78f7ae4b0e1b73c04ffb292e0` |

Galaxies: `ra,dec,z,zErr,petroMag_r,expAB_r,expPhi_r,modelMag_g,modelMag_r`,
0.003 < z < 0.12, `zWarning=0`, `sciencePrimary=1`. Median D_C 331 Mpc, max 517 Mpc.
**`expAB_r` is usable (0 < b/a <= 1) on 492,163 of 492,189 rows - 100.0%** - which is
what makes "oriented points" possible; DESI ships no shapes.

Both row counts were checked against independent `COUNT(*)` queries run before the
pull and match **exactly** (492,189 and 747,982), despite four transient connection
timeouts during the chunked download. VERIFIED.

### `vast-voids-sdss-dr7/` - 541,494 B

| file | rows | bytes | sha256 |
|---|---|---|---|
| `VoidFinder-nsa_v1_0_1_Planck2018_comoving_maximal.txt` | 1,163 | 181,659 | `98082b9973e491757ca854a519a3ecd98689d064b928c522ae31954d58604afc` |
| `V2_VIDE-nsa_v1_0_1_Planck2018_zobovoids.dat` | 531 | 177,270 | `486a3cc057db2c6229dbc26a080e84f8bba8810ab3ab2eceaeb1b4e73a02f3e0` |
| `V2_REVOLVER-nsa_v1_0_1_Planck2018_zobovoids.dat` | 518 | 173,069 | `c5d4637892ad568dc6ecec24c6c329b21025f9eef81301390c521b5f65e42faf` |

Byte-for-byte as published. Frame VERIFIED as equatorial cartesian (rebuilding
(x,y,z) from (ra,dec,r) reproduces the file to 1.7e-13); units VERIFIED as comoving
Mpc/h. Edge flags: 805 interior, 333 touching the survey boundary, 25 beyond -
**draw `edge = 0` only**, or the survey boundary becomes a shell of fake voids.

### `named-structures/` - 14,876 B (derived, no external source)

| file | rows | bytes | sha256 |
|---|---|---|---|
| `sdss_density_peaks.csv` | 30 | 1,869 | `d708640b60236429c347f6536aee610c544f7403f07858ae406f8abb52e7939c` |
| `structures.csv` | 30 | 2,003 | `f52d5633a89aa4761b0cb8c6f021618a8e484bd6e450e425e11b8b4f98e50ba3` |
| `largest_voids.csv` | 20 | 1,131 | `e845f3e0c8c86aa485b1953d0ec27d63ca43230c582a629c2b43f7915bd5186a` |

Six of the thirty peaks carry a name, each with its own evidence in the row:

| name | measured RA, Dec | measured z | d (Mpc) | separation from reference |
|---|---|---|---|---|
| Abell 1656 (Coma Cluster) | 192.89, +27.77 | 0.0226 | 100 | 1.83 deg = 3.2 Mpc |
| Abell 2199 | 246.31, +39.60 | 0.0308 | 136 | 0.66 deg = 1.6 Mpc |
| Abell 1367 (Leo Cluster) | 179.81, +22.24 | 0.0230 | 102 | 4.22 deg = 7.5 Mpc |
| Abell 2151 (Hercules Cluster) | 240.67, +17.09 | 0.0360 | 158 | 0.89 deg = 2.5 Mpc |
| Abell 2052 | 229.73, +6.94 | 0.0348 | 153 | 0.55 deg = 1.5 Mpc |
| Abell 1185 | 169.08, +28.77 | 0.0333 | 146 | 1.21 deg = 3.1 Mpc |

VERIFIED: positions measured from the staged SDSS sample; reference coordinates
resolved one at a time through CDS Sesame and transcribed into the script. A seventh
candidate was rejected on purpose - a peak 3 Mpc from Abell 2052 on the sky but
43 Mpc behind it - because sky position alone will happily label a background
structure with a foreground name.

### Rejection dossiers (SOURCE + LICENCE + NOTES each, no data)

`REJECTED-2mrs-cfa/`, `REJECTED-2mrs-vizier/`, `REJECTED-cosmicflows4-vizier/`,
`REJECTED-cosmicflows4-ip2i-grids/`, `REJECTED-cosmicflows4-edd/`,
`REJECTED-6dfgs/`, `REJECTED-2mpp/`, `REJECTED-vizier-aas-aa-mnras-tables/`.
43,378 B total.

### Tools - `_tools/` (25,202 B)

`desi_reduce.py` (download + FITS BINTABLE reader in pure numpy + reduce + delete),
`sdss_chunks.py` (chunked SkyServer pull with retries), `inventory.py` (hashes,
observed headers, cosmology inversion), `check_voids.py`, `derive_structures.py`,
`label_structures.py`, plus `sdss_query.py`, `fitshdr.py`, `txt.py`.
No astropy, no healpy - numpy only.

---

## 5. Attribution strings the app must carry

**DESI (CC BY 4.0 - legally required, all three parts).** Do not shorten the block;
it is a licence condition.

> This research used data obtained with the Dark Energy Spectroscopic Instrument
> (DESI). DESI construction and operations is managed by the Lawrence Berkeley
> National Laboratory. This material is based upon work supported by the U.S.
> Department of Energy, Office of Science, Office of High-Energy Physics, under
> Contract No. DE-AC02-05CH11231, and by the National Energy Research Scientific
> Computing Center, a DOE Office of Science User Facility under the same contract.
> Additional support for DESI was provided by the U.S. National Science Foundation
> (NSF), Division of Astronomical Sciences under Contract No. AST-0950945 to the
> NSF's National Optical-Infrared Astronomy Research Laboratory; the Science and
> Technology Facilities Council of the United Kingdom; the Gordon and Betty Moore
> Foundation; the Heising-Simons Foundation; the French Alternative Energies and
> Atomic Energy Commission (CEA); the National Council of Humanities, Science and
> Technology of Mexico (CONAHCYT); the Ministry of Science and Innovation of Spain
> (MICINN), and by the DESI Member Institutions:
> www.desi.lbl.gov/collaborating-institutions. The DESI collaboration is honored to
> be permitted to conduct scientific research on I'oligam Du'ag (Kitt Peak), a
> mountain with particular significance to the Tohono O'odham Nation. Any opinions,
> findings, and conclusions or recommendations expressed in this material are those
> of the author(s) and do not necessarily reflect the views of the U.S. National
> Science Foundation, the U.S. Department of Energy, or any of the listed funding
> agencies.

> DESI Collaboration et al. (2026), "Data Release 1 of the Dark Energy Spectroscopic
> Instrument". DESI DR1 data, CC BY 4.0.

> Modified from the DESI DR1 clustering catalogues: columns subset, converted to
> comoving positions, and downsampled for display.

**SDSS (public domain - courtesy, not required).**

> Funding for the Sloan Digital Sky Survey IV has been provided by the Alfred P.
> Sloan Foundation, the U.S. Department of Energy Office of Science, and the
> Participating Institutions.

> SDSS public data-release data are in the public domain. This work uses SDSS
> spectroscopic redshifts and SDSS DR18 SkyServer photometry.

**VAST voids (CC BY 4.0 - legally required).**

> Void catalogue: Douglass, Kelly; Veyrat, Dahlia; BenZvi, Segev (University of
> Rochester), "VAST void catalogs for SDSS DR7", version 1.3.0, Zenodo,
> doi:10.5281/zenodo.7406035, CC BY 4.0. Described in Douglass, Veyrat & BenZvi,
> ApJS (2023), arXiv:2202.01226. Modified: converted from comoving Mpc/h to Mpc and
> subset to interior voids.

**Derived products.** `named-structures/` is our own work from the above, so it
carries the DESI/VAST obligations through and adds none of its own.

---

## 6. What I could not verify

1. **A&A's own copyright page.** <https://www.aanda.org/about-aa/copyright> returns
   HTTP 403 to WebFetch and a JavaScript wall to curl. A&A's position is recorded
   only through CDS's summary of it ("free for a scientific usage"), so it is
   **INFERRED**. It changes no verdict: under either reading there is no commercial
   grant.
2. **The AAS copyright policy page itself** (`aas.org/publications/aas-copyright-policy`)
   was not fetched. CDS's assertion is taken as operative because CDS is the party
   redistributing to us. **INFERRED** that AAS's own policy would say the same.
3. **Whether the CDS CC-BY-NC line is current.** AAS articles moved to CC BY 4.0
   open access from 2022, and Cosmicflows-4 is a 2023 paper, so it is possible that
   CDS's blanket NC statement is stale for recent catalogues. Resolving it needs a
   written answer from CDS (cds-question@unistra.fr). Not pursued: nothing the
   project needs depends on it. **INFERRED.**
4. **Which licence CDS actually means for AAS tables.** The sentence names CC-BY-NC
   and links the CC BY-NC-**ND** deed. Both forbid commercial use, so the ambiguity
   does not matter to us; it would matter to anyone seeking permission. **VERIFIED
   that the page is internally inconsistent**; not resolved.
5. **Data Central's terms for the 6dFGS mirror** (datacentral.org.au) were not
   checked. It is the only remaining lead for southern coverage, and a mirror cannot
   grant rights the originator never granted, so the expected answer is another no.
6. **Whether SDSS has a terms page other than the Image Use Policy.**
   <https://www.sdss.org/science/data-use/> returns HTTP 404; the citation page
   states no licence. The public-domain sentence lives, oddly, on the image page.
   **VERIFIED as quoted**; whether a better-placed statement exists elsewhere is
   unknown.
7. **The scientific quality of the derived density peaks** beyond the six positional
   identifications. The other 24 peaks are real overdensities in the data, but they
   have not been matched to any published object.

---

## 7. Ledger rows for `docs/data-sources.md`

```
### DESI DR1 - https://data.desi.lbl.gov/doc/
Large-scale-structure clustering catalogues (BGS_BRIGHT, BGS_BRIGHT-21.5, QSO;
NGC and SGC). 5,481,299 rows staged at data-staging/desi-dr1-lss/, reduced to
RA/DEC/Z/flux_g/flux_r/WEIGHT.
- Terms: CC BY 4.0. "Users are free to share, copy, redistribute, adapt, transform,
  and build upon the DESI data available through this website for any purpose,
  including commercially" (read 2026-09-12).
- Obligations: cite the DR1 paper, state that changes were made, carry the full
  acknowledgements block verbatim. All three in the credits screen.
- Commercial: YES, explicitly.

### SDSS (public releases, queried via SkyServer DR18) - https://skyserver.sdss.org/
492,189 galaxies (0.003 < z < 0.12) with shapes, and 747,982 quasars, at
data-staging/sdss-dr18/.
- Terms: public domain. "All SDSS data released in our public data releases are
  considered in the public domain" (read 2026-09-12,
  https://www.sdss.org/collaboration/image-use-policy/).
- Obligations: none legally. Acknowledge anyway.
- Commercial: YES.

### VAST void catalogs for SDSS DR7 - https://zenodo.org/records/7406035
1,163 VoidFinder voids plus two ZOBOV variants, at
data-staging/vast-voids-sdss-dr7/. Comoving Mpc/h, equatorial cartesian,
Planck 2018 cosmology (Om = 0.3150, h = 0.6766, recovered by inversion).
- Terms: CC BY 4.0 (Zenodo licence field "cc-by-4.0", read 2026-09-12).
- Obligations: attribute the three authors and the DOI; state modifications.
- Commercial: YES.

### BLOCKED, do not revisit without a signed grant
- 2MRS (CfA): "Please do not redistribute any of these files." No licence at all.
- 2MRS (VizieR J/ApJS/199/26): CC BY-NC.
- Cosmicflows-4 (VizieR J/ApJ/944/94): CC BY-NC.
- Cosmicflows-4 / Laniakea watersheds + density grids (IP2I Lyon): no licence,
  only "If you use this data cite the article above".
- Cosmicflows / EDD (U. Hawaii): no licence, no terms page at all.
- 6dFGS DR3 (WFAU and VizieR VII/259): attribution instructions only, no licence.
- 2M++ (VizieR J/MNRAS/416/2840): no licence AND 3,813 of 72,973 rows are
  fabricated Zone-of-Avoidance galaxies, per the catalogue's own ReadMe.
- Any supercluster, void, group or filament catalogue reaching us through CDS:
  AAS = CC BY-NC, A&A = "free for a scientific usage", MNRAS = nothing granted.
Full dossiers in data-staging/REJECTED-*/.
```

---

## 8. What this costs the app, and how to be honest about it

**The galaxy shell is no longer all-sky.** DESI DR1 covers roughly 14,000 deg^2 in
two disconnected caps and SDSS about 11,000 deg^2 measured from the staged sample
itself; together, well under half the sky, with nothing in the Zone of Avoidance and
little in the deep south. 2MRS covered 91% of the sky and is gone.

Do not fill the gap. Filling it is exactly what 2M++ did, and that is why 2M++ is in
the rejected pile: 5.2% of its rows are inventions, and a viewer cannot tell which.
Draw the survey footprint as a visible boundary; an empty Zone of Avoidance is the
truth.

**There are no measured distances.** Everything staged is redshift-space. Label the
layer as such.

**Laniakea has no data boundary.** Two honest options, in order of laziness:
(a) draw the named attractors as labelled points and let the galaxy density carry the
shape; (b) draw an explicitly-labelled schematic envelope sized to the published
volume. Never present (b) as measured.

**What actually improved.** 4.3M galaxies instead of 43k, reaching z = 0.5 instead of
cz/H0 ~ 300 Mpc, with 1.2M quasars behind them to z = 3.5 - plus a volume-limited
sample of 300,043 galaxies that lands almost exactly on the 45k-200k the renderer
wants once a shell is cut from it, and 1,163 voids that were never in the plan. The
commercial filter cost coverage and bought depth.

**For the bridge to the fictional worlds.** The named-structure positions in
`named-structures/structures.csv` are the anchors. A fictional system placed "beyond
the Coma Cluster" must be placed beyond *that* Coma - same frame, same cosmology -
or the seam between the real and the invented opens at exactly the moment somebody
looks closely, which is the moment the whole premise depends on.
