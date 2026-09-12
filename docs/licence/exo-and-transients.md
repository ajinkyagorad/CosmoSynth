# Exoplanets, pulsars, gravitational waves and heliophysics — commercial licence clearance

Audit run 2026-09-12 (UTC) against the owner's constraint of the same date: every shipped
byte must be usable commercially. Earlier CosmoSynth design documents assumed non-commercial
sideload; where they did, this report corrects them with evidence.

Every claim below is marked **VERIFIED** (a page I read in full, or a command I ran and read
the output of) or **INFERRED** (a reasoned conclusion that no single sentence states).

**Method, applied without exception:** the primary licence page was fetched with `curl` on
triton, stripped to text, and read as published. Third-party summaries were not trusted —
and that mattered: a summariser reported a GPL grant on the ATNF download page that **does
not exist on it**. That one false positive would have put a non-free catalogue into a
commercial APK.

Staged footprint: **16,435,325 bytes (15.67 MiB)** across 9 new directories plus 8 files
added to a directory shared with the Solar System audit. Budget was 300 MB. Disk on triton
was 15 GB free before and after (`df -h /`, VERIFIED); nothing fetched exceeded 3 MB.

---

## 1. Decision table

| # | Source | Verdict | Decisive evidence (read 2026-09-12) | Substitute |
|---|---|---|---|---|
| 1 | **NASA Exoplanet Archive** `pscomppars` | **OK** | No restriction stated anywhere on the archive's own acknowledgement page; the words *commercial, licence, redistribute, permission* do not occur on it. `/docs/data_use_policy.html` and `/docs/imageuse.html` both **404**. NASA content "generally are not subject to copyright in the United States" and commercial use is permitted subject only to no-endorsement — <https://www.nasa.gov/nasa-brand-center/images-and-media/> | — |
| 2 | **ATNF Pulsar Catalogue** (`psrcat.db`, 4,393 pulsars) | **BLOCKED** | The download page states **no licence at all**. The tarball contains **no COPYING/LICENSE file** (VERIFIED by extraction). The only GPL sentence, in `psrcat_help.html` §9, grants rights over "**PSRCAT**" — which the package README defines as the *program*, listing `psrcat.db` separately as "the public database". With no grant, CSIRO's site notice governs: *"Except as permitted by statute, any other use of copyright material on this website is subject to first receiving consent."* — <https://www.csiro.au/en/about/Policies/Legal/Copyright> | **Fermi LAT 3PC** (row 3) |
| 3 | **Fermi LAT 3PC** gamma-ray pulsars (294) | **OK**, with a recorded provenance caveat | NASA GSFC product, no proprietary period: *"all LAT science data has been released as early as possible, typically within a day or two of acquisition"* — <https://fermi.gsfc.nasa.gov/ssc/data/policy/>; plus the NASA copyright statement in row 1 | — (this *is* the substitute) |
| 4 | **GWOSC / LIGO–Virgo–KAGRA** event catalogues + strain | **OK** | *"Data released under a CC BY 4.0 License"* — <https://gwosc.org/> and <https://gwosc.org/acknowledgement/>. CC BY 4.0 permits commercial use and has **no** share-alike term | — |
| 5 | **NOAA SWPC** solar wind, Kp, solar indices | **OK** | *"The information on National Weather Service (NWS) Web pages are in the public domain, unless specifically noted otherwise, and may be used without charge for any lawful purpose…"* — <https://www.weather.gov/disclaimer> | — |
| 6 | **NOAA NCEI** Penticton F10.7 daily, 1947–2018 | **OK** (INFERRED on one point — §5) | Same NWS statement as row 5; provenance stated at <https://www.ngdc.noaa.gov/stp/solar/flux.html> | — |
| 7 | **NRCan / spaceweather.gc.ca** F10.7 `fluxtable.txt` | **BLOCKED** | *"Commercial reproduction — Unless otherwise specified, you may not reproduce materials on this site, in whole or in part, for the purposes of commercial redistribution without prior written permission from the copyright administrator."* — <https://www.canada.ca/en/transparency/terms.html>, the terms page spaceweather.gc.ca itself links to | **NOAA NCEI + SWPC** (rows 5, 6) |
| 8 | **SILSO** International Sunspot Number | **BLOCKED** | *"Licence: CC BY-NC"* — <https://www.sidc.be/SILSO/datafiles>. Independently verified by the Solar System audit; re-read and confirmed here | **NOAA F10.7** (rows 5, 6) |
| 9 | **NASA SDO** (AIA, HMI) imagery | **OK**, one condition | *"SDO images and movies are not copyrighted unless explicitly noted."* — <https://sdo.gsfc.nasa.gov/data/rules.php>. Condition: rule 4, browse products are not for science analysis | — |
| 10 | **SOHO / LASCO** (ESA + NASA) | **BLOCKED** | *"The use of SOHO images or data for public education efforts and non-commercial purposes is strongly encouraged and requires no expressed authorization."* — <https://soho.nascom.nasa.gov/data/summary/copyright.html>. Commercial use is unaddressed, i.e. unauthorised | **NOAA GOES-19 CCOR-1** + **GOES SUVI** (row 11) |
| 11 | **NOAA GOES SUVI + CCOR-1** | **OK** | Same NWS statement as row 5; NOAA instruments, NOAA spacecraft, NOAA host — no international partner and no consortium notice | — (this *is* the substitute) |

All eleven verdicts: **VERIFIED**, except the single INFERRED point in row 6, isolated in §5.

### The two corrections to earlier design assumptions

1. **The pulsar layer cannot use the ATNF catalogue.** Any design document that says
   "spin frequencies from psrcat" is wrong for a commercial build. 4,393 pulsars become 294.
2. **The solar layer cannot use SOHO/LASCO.** Any document specifying LASCO C2/C3 coronagraph
   frames is wrong. CCOR-1 on GOES-19 replaces them, with a smaller field of view and no
   historical archive.

### Two things this audit did *not* find blocked, against expectation

- **The NASA Exoplanet Archive is clean.** All 6,366 planets ship. VERIFIED: the whole
  acknowledgement page was read and contains a credit request and nothing else.
- **NOAA's coronagraph exists and is alive.** CCOR-1 was not in the brief. It is the single
  most valuable find here, because it means the app can show a coronal mass ejection leaving
  the Sun without asking anyone's permission. VERIFIED: HTTP 200 with 15-minute cadence at
  `https://services.swpc.noaa.gov/images/animations/ccor1/`.

---

## 2. The ATNF rejection, in full, because it is the one that will be argued with

The ATNF catalogue is the best pulsar table in existence, and rejecting it costs 93% of the
rows. Four findings, each VERIFIED by reading a primary artefact:

1. **The download page states nothing.** Its complete text is one sentence — *"Click on the
   link below to download a gzipped tar file of the latest psrcat source code and public
   catalogue:"* — plus ~60 tarball links. `licence`, `license`, `copyright`, `terms`,
   `redistribute`, `commercial`: none of them appear.
2. **The package states nothing.** `psrcat_pkg.tar.gz` (sha256 `2ea143fc…`, 1,224,627 bytes)
   holds 23 files. No COPYING, no LICENSE, no NOTICE. Grepping every source for licence text
   returns two vendored third-party notices — Pete Wilson's `wc_strncmp.c` (LGPL 2.1) and
   Parsifal Software's expression evaluator — and no CSIRO grant.
3. **The one GPL sentence names the program.** From `psrcat_help.html` §9 *Downloading the
   Catalogue*: the preceding sentence distinguishes *"the catalogue database"* from *"the
   PSRCAT source code"*, and the grant that follows names only **PSRCAT**. The package's own
   README defines that as the program: *"This package contains the source code of "psrcat"
   and the public database (psrcat.db)."*
4. **With no grant, the host's default applies**, and CSIRO's default requires consent for
   anything beyond a single personal copy.

**INFERRED, and it is the load-bearing inference:** finding 3 is ambiguous, not hostile. But
both readings block the file. If the GPL does *not* cover `psrcat.db`, there is no licence
and CSIRO's consent requirement applies. If it *does*, then baking a GPLv3 file into a Unity
APK raises a copyleft question over the shipped work that no commercial release should carry
unresolved. Ambiguity is itself disqualifying under a hard commercial filter.

**Mirrors do not help.** psrcat is VizieR `B/psr`; CDS sends you back to the producer's
ReadMe, which grants nothing (this project already recorded CDS's rules in
`data-staging/REJECTED-vizier-aas-aa-mnras-tables/LICENCE.txt`). The 2005 catalogue paper is
in the *Astronomical Journal*, whose tables CDS classes CC BY-NC — blocked outright.

### And the honest wrinkle in the substitute

The Fermi 3PC README says, verbatim: *"The sequence of input sources is: 1) The ATNF pulsar
catalog, "psrcat" … We used ATNF psrcat v1.68."* The substitute is partly derived from the
rejected source. That is recorded in full in
`data-staging/fermi-lat-3pc-pulsars/LICENCE.txt` and is **not** waved away. Summary
(**INFERRED**):

- What is taken from psrcat are *measured values* — a right ascension, a dispersion measure.
  CosmoSynth's own bar draws the line here: facts are not copyrightable; a specific compiled
  file is. The blocked artefact is `psrcat.db` and its selection of 4,393 objects.
- **3PC is a different compilation, not a mirror.** Its selection criterion is external to
  psrcat ("pulsars for which we see gamma-ray pulsations"). It keeps 294 of 4,393 (6.7%),
  adds objects psrcat lacks, *overwrites* psrcat's proper motions and distances with its own,
  and adds ~60 gamma-ray columns psrcat never had.
- **This is not the SILSO loophole in disguise.** That trap — NOAA republishing SILSO's
  series verbatim — is blocked because a *named licence* travels with the data. Here no
  licence exists, and the NASA product is a new work rather than a copy.
- **Mitigation, applied:** the credit string names ATNF anyway. It costs one line.

---

## 3. Staged inventory

Root: `~/projects/CosmoSynth/data-staging/`. All byte counts and sha256 sums VERIFIED by
running `inv.py` on triton after staging completed.

### `nasa-exoplanet-archive/` — 2.0 MB, the primary exoplanet table

| file | bytes | sha256 | rows |
|---|---:|---|---:|
| `pscomppars.csv` | 2,033,259 | `50ab0ff785042754bbb3da8dc70ce75a3568e5464bc5d3bdf20bf2d719b118a1` | **6,366** |
| `SOURCE.txt` | 1,721 | `d3adac9669fad104d3b1f3580d95434380a024a5c3e136812e0844092be20294` | — |
| `LICENCE.txt` | 3,870 | `7e9e9cf370cb11a84d7dcb972e3369f5346e00e2027fd4ae4727d047d5815038` | — |
| `NOTES.md` | 5,815 | `6f39e8db655c5e54fa3bc6edd6749761bde88f313e169ed2c979b335b94e6291` | — |

35 columns, TAP query recorded verbatim in `SOURCE.txt`. Cross-check `select count(*) from
pscomppars` → **6366**, matching the staged rows exactly (VERIFIED). Coverage: `ra`/`dec`
6,366 · `sy_dist` 6,338 · `pl_rade` 6,316 · `pl_eqt` 5,832 · `st_teff` 6,062. Discovery
years 1992–2026; distances 1.301 pc to 8,500 pc.

### `fermi-lat-3pc-pulsars/` — 1.4 MB, the pulsar substitute

| file | bytes | sha256 | rows |
|---|---:|---|---:|
| `3PC_Catalog+SEDs_20230803.fits` | 944,640 | `df6e610774d3de90213eff933662b30046709c85d37aa3486bc8f352bb5bab10` | 294 (HDU 1) |
| `3pc_pulsars.csv` | 426,052 | `c120bf6d597b69414d5a44c30a5b5036c9ba98408e67f1d1f39874e9a92d92d9` | **294 × 97 cols** |
| `README_3PC_Catalog+SEDs.txt` | 7,029 | `ee6d24bb69132b92bd94ef591f6dff20f2d3c1fe0c1a9cbe0d8d2c4ccd9b2ee6` | — |
| `SOURCE.txt` | 2,851 | `d975a461ec2043defa71a6ef8ae0c14a740b439eaf29bef2f168934c00e2487d` | — |
| `LICENCE.txt` | 6,994 | `89bbf75b58fbb2024278ad155c4f99d17dad6c673ceb40017930b72219899f81` | — |
| `NOTES.md` | 6,205 | `9c85ec12ebe78fc5136b35a5891a9a11635a9fced8836409d7754fcdd963506c` | — |

The CSV was extracted from the FITS on triton with a pure-numpy bintable reader (astropy is
not installed there) that asserts `sum(column widths) == NAXIS1` (1755) before reading, so a
mis-parsed dtype fails loudly rather than yielding plausible garbage.

**Physical sanity check, VERIFIED by reading values back**: Crab J0534+2200 F0 = 29.716906 Hz,
Vela J0835−4510 = 11.189301 Hz, Geminga J0633+1746 = 4.217554 Hz, MSP J0437−4715 =
173.687946 Hz. Full F0 range 1.2050 – 707.314 Hz. `RAJD`, `DECJD`, `F0`, `D3PC` populated for
all 294.

### `gwosc-gwtc/` — 4.4 MB, gravitational waves

| file | bytes | sha256 | count |
|---|---:|---|---:|
| `gwtc-events.json` | 738,435 | `9802865cc8b41508f4073769758f7dbaf5498b22a907849d567083635e7e862a` | **391 events** |
| `allevents.json` | 1,267,691 | `731f167b5545db5b3b4345994fde52730e260bb1112a739051be7f884fca7969` | 671 events (17 catalogues, incl. marginal) |
| `GW150914_H1_32s_4096Hz.txt.gz` | 1,285,956 | `f8357860261bb39859e8c92308e065ff984ccb23cc3a81f73e4b2671f1737df2` | **131,072 samples** |
| `GW150914_L1_32s_4096Hz.txt.gz` | 1,219,487 | `3da578c84a05393c2bd1d130c0e680b4c0f86dc9777588ac7c898ed317f8ec9b` | **131,072 samples** |
| `SOURCE.txt` | 2,950 | `dacf187f756d7a3d4a2478d706dee18208d83517e3061cff6b4ebb4d2963079e` | — |
| `LICENCE.txt` | 4,231 | `c6c6dcb5abdf61045ec36ea36b567b53df4d311fdcea80d5f7de9cd31a575347` | — |
| `NOTES.md` | 5,799 | `1347c1337186e0907c9ef529bfac306e7f63c75302bcabb8b6f6120bb6ea0b8d` | — |

Sample count checked against 32 s × 4096 Hz = 131,072 for both detectors (VERIFIED).
Catalogue mix in `gwtc-events.json`: GWTC-5.0 161 · GWTC-4.1 140 · GWTC-2.1-confident 54 ·
GWTC-3-confident 35 · GWTC-1-confident 1.

**Reduction applied:** GWOSC also serves this window as HDF5 (~1.04 MB per detector). Those
were fetched, found unverifiable on triton (no `h5py`), and **deleted** in favour of the
`.txt.gz` form, which carries the same series plus its sample-rate/GPS header in plain text
and is readable with the standard library. An unverifiable file is not a staged file.

### `noaa-swpc-solar/` — shared with the Solar System audit; 4.68 MB added here

| file | bytes | sha256 | records |
|---|---:|---|---:|
| `rtsw_wind_1m.json` | 2,945,343 | `c11177990036c9d9271d863d14006ff6b5c7ec7f9bd3cf02d147358c3d93ede1` | 3,979 |
| `rtsw_mag_1m.json` | 1,669,610 | `67e1436f5314590c16f27535418f90a46812925ae1541b3a5f3f32f8a917066c` | 3,958 |
| `planetary_k_index_1m.json` | 27,925 | `31821aa7a69ed838eff6824dd528331257d32324e52f957e45c01e3ba465d23c` | 358 |
| `predicted-solar-cycle.json` | 18,173 | `683a6d575cbb764d28f16994baafb3e9ba6cc342dce806d486fbebb34b69a873` | 58 |
| `propagated-solar-wind-1-hour.json` | 6,792 | `1f9e158423cd4a31837990fc76d66a1d4ef0127c768749fab5fccceae2b73e84` | 57 |
| `noaa-planetary-k-index.json` | 4,648 | `a9fa8cf338928a9d993a4f16f0b1e2379118473e74fcbd9cb7546df8dd1ad216` | 60 |
| `daily-solar-indices.txt` | 2,919 | `543fe9b4d95989153c70cf2a4ed03b31acfb3e66ffaa25b867aaf4a869ad86ed` | 30 days |
| `NOTES-heliophysics.md` | 6,107 | `4020ff8f049feeaa0d964685e0ea20c95c3c7d95bbb75c5f331ecb269f6afb80` | — |

The directory's existing `SOURCE.txt`, `LICENCE.txt` and `NOTES.md` were **not overwritten**;
an addendum was appended to `SOURCE.txt` and the new content documented in a separate
`NOTES-heliophysics.md`. That directory's SILSO warning applies here too and is repeated
there.

**Two dead endpoints confirmed (VERIFIED, both HTTP 404):**
`products/solar-wind/plasma-7-day.json` and `products/solar-wind/mag-7-day.json` — these are
the two an older CosmoSynth page used. The whole `products/solar-wind/` family is gone
(5-minute, 2-hour and 1-day variants all 404). Live replacements: `json/rtsw/rtsw_wind_1m.json`
and `json/rtsw/rtsw_mag_1m.json`, staged above.

### `noaa-ncei-penticton-f107/` — 928 KB, the long solar-activity record

| file | bytes | sha256 | rows |
|---|---:|---|---:|
| `listing_drao_noontime-flux-observed_daily.txt` | 462,938 | `7240df111ea45f26057b89d3e9880a58856d7b6d040ad16247db41f5546dac96` | 26,052 lines, **25,388 with a value** |
| `listing_drao_noontime-flux-adjusted_daily.txt` | 462,772 | `58c8409e126c081d1063127f6b77bd7f0807d075622d94f04b0cbd25bde7f7de` | same shape |

VERIFIED span: first value 1947-02-14 (260.4 sfu), last dated line 2018-04-30 (70.2 sfu).

### `nasa-sdo-imagery/` — 804 KB · `noaa-goes-suvi-ccor1/` — 2.0 MB

| file | bytes | sha256 | format (VERIFIED with `file`) |
|---|---:|---|---|
| `sdo_latest_1024_0171.jpg` | 182,726 | `6ed60ae5edbf16408b7aace44d645d4de6536a03487f35452234ef4ef407980c` | JPEG 1024×1024 |
| `sdo_latest_1024_0193.jpg` | 140,850 | `e8cbf4c53da6874864783064a5c9826fbdd8e6b5becaadf14a8dff4963c91c60` | JPEG 1024×1024 |
| `sdo_latest_1024_0304.jpg` | 206,829 | `3e79120c034e6dbb229e3868a905bc3a3a5c1178edc3e9ceced3017d9461617b` | JPEG 1024×1024 |
| `sdo_latest_1024_4500.jpg` | 69,536 | `8a7f278557828b721eba0d5f214c4d75db139da27e3eab6221b1c12d258ab119` | JPEG 1024×1024 |
| `sdo_latest_1024_HMIIC.jpg` | 196,192 | `349e988b0f9ab948eeaad93d0542fb00856ee0842de57430fc530c9868fe5a82` | JPEG 1024×1024 |
| `ccor1_latest.jpg` | 138,708 | `1f54440f5a979688e3ad84624ee62d9e9b8500d2561e6a7e790b77212ee0e2c2` | JPEG **1204×1205** |
| `suvi_primary_195_latest.png` | 708,500 | `30be9dec9a5b9c86fbe8b44154b4d7c8b67b8ced6112d867e57413d8f404c076` | PNG 1280×1280 RGBA |
| `suvi_primary_304_latest.png` | 1,151,075 | `6901a8f01598f7ac29fa7222c01c4b5a2cc610eb337a72cbbb727c0e362ede79` | PNG 1280×1280 RGBA |

All eight are `latest_*` URLs — the staged bytes are the Sun at ~14:36 UTC on 2026-09-12 and
a re-fetch returns a different Sun. **Ship the bytes, not the URL.**

### Rejection directories (evidence only; nothing shippable)

| directory | contents | why |
|---|---|---|
| `REJECTED-atnf-psrcat/` | `LICENCE.txt` 7,724 B · `NOTES.md` 3,429 B · `SOURCE.txt` 2,430 B · `psrcat_README.txt` 2,789 B · `psrcat_db_sample_head60.txt` 2,731 B | no stated data licence; CSIRO requires consent |
| `REJECTED-soho-lasco/` | `LICENCE.txt` 5,384 B · `NOTES.md` 2,216 B · `SOURCE.txt` 1,583 B | notice covers non-commercial only |
| `REJECTED-nrcan-spaceweather-f107/` | `LICENCE.txt` 7,051 B · `NOTES.md` 2,756 B · `SOURCE.txt` 2,241 B | canada.ca bars commercial redistribution |

`silso-sunspots/` already exists as a rejection directory, staged by the Solar System audit;
its CC BY-NC finding was re-read and confirmed here.

The two files under `REJECTED-atnf-psrcat/` are the README that *would* have carried a data
licence and does not, and the head of `psrcat.db` showing it carries no licence header of
its own. They exist so nobody re-downloads 1.2 MB to reach the same conclusion.

---

## 4. Attribution strings that must appear in the app's credits

Verbatim, in this form. Each is required by the source named.

**Exoplanets — NASA Exoplanet Archive**

> This research has made use of the NASA Exoplanet Archive, which is operated by the
> California Institute of Technology, under contract with the National Aeronautics and Space
> Administration under the Exoplanet Exploration Program.

**Pulsars — Fermi LAT 3PC** (the second sentence is a deliberate courtesy to ATNF, see §2)

> Pulsar data: Fermi LAT Third Catalog of Gamma-ray Pulsars (3PC), NASA/Fermi Science Support
> Center. Smith et al. 2023. Includes parameters collated from the ATNF Pulsar Catalogue
> (Manchester, Hobbs, Teoh & Hobbs, AJ 129, 1993, 2005),
> https://www.atnf.csiro.au/research/pulsar/psrcat

**Gravitational waves — GWOSC** (satisfies CC BY 4.0 §3(a) for an app credits screen)

> Gravitational-wave event data: Gravitational Wave Open Science Center (gwosc.org), a service
> of the LIGO Scientific Collaboration, the Virgo Collaboration and KAGRA, used under CC BY 4.0
> (https://creativecommons.org/licenses/by/4.0/). Strain data for GW150914 from the LIGO Open
> Science Center.

CC BY 4.0 §3(a)(1)(B) also requires that modification be indicated. If the strain is
band-passed, whitened, resampled or pitch-shifted — and it must be, to be audible — add:

> Gravitational-wave strain has been filtered and frequency-shifted for presentation.

**Solar activity — NOAA + DRAO**

> Solar activity: NOAA/NWS Space Weather Prediction Center. 10.7 cm solar radio flux measured
> at the Dominion Radio Astrophysical Observatory, Penticton, by the National Research Council
> Canada; archived and redistributed by NOAA/NCEI and NOAA/SWPC.

**Solar imagery — NASA SDO**

> Solar imagery courtesy of NASA/SDO and the AIA, EVE, and HMI science teams.

**Solar imagery — NOAA GOES**

> Solar EUV and coronagraph imagery: NOAA/NWS Space Weather Prediction Center, GOES SUVI and
> GOES-19 CCOR-1.

**Required once, covering all NOAA material** — this is not optional politeness. The NWS
disclaimer invokes 17 U.S.C. § 403: third parties producing copyrighted works consisting
predominantly of NWS material must identify it and state that it is not subject to copyright.

> NOAA/NWS material incorporated in this application is not subject to copyright protection.

**Conditions attached to the above, all mandatory**

- No NASA or NOAA logo/insignia in the app icon, splash screen or store art. Crediting a data
  source is not endorsement; a meatball on the icon is.
- Do not claim copyright in NASA or NOAA material.
- Do not present modified NOAA content as official government material.
- SDO images are *browse* products: do not derive a measured number from their pixels
  (rule 4, `sdo.gsfc.nasa.gov/data/rules.php`).

---

## 5. What I could not verify

1. **NOAA's authority to redistribute the Penticton F10.7 series — INFERRED.** No page states,
   in NOAA's or NRC's words, the terms of that redistribution. What is VERIFIED: NOAA states
   no restriction on it, NCEI has served it publicly for decades, and NOAA's own material is
   public domain. The reasoning for treating this differently from the SILSO trap is in
   `REJECTED-nrcan-spaceweather-f107/LICENCE.txt`: SILSO attaches a *named licence* that
   travels with the data, whereas Canada attaches no licence to F10.7 and canada.ca's clause
   is a website terms-of-use governing reproduction from that site. **This is the weakest
   link in the family.** One email to NRC settles it, and would probably also unlock the
   richer Canadian file.
2. **Whether an OGL-licensed F10.7 dataset exists on open.canada.ca — INFERRED.** A portal
   search on 2026-09-12 found none. Portal search is not enumeration. If one exists, the Open
   Government Licence – Canada expressly permits commercial use and would be the better
   source, flipping decision #7 to OK. Worth two minutes before any re-derivation.
3. **The exact copyleft reach of GPLv3 over a data file inside an APK — not analysed.** Not
   needed: the ATNF rejection holds under both readings (§2). Flagged so nobody thinks it was
   settled.
4. **Third-party inputs inside the Fermi 3PC compilation — partly INFERRED.** The 3PC README
   names Lorimer's Galactic MSP list (`astro.phys.wvu.edu`) as input 7. That page's own terms
   were not separately read, because no staged CSV column is sourced from it alone. If a bake
   ever isolates MSP-only columns, check it.
5. **Whether GWOSC's CC BY 4.0 covers the *catalogue metadata* as well as the strain —
   INFERRED as yes.** The statement on gwosc.org is unqualified ("Data released under a CC BY
   4.0 License") and appears site-wide, including on the acknowledgement page that governs the
   event API. No page distinguishes the two. The risk is negligible; the alternative reading
   would be that the metadata is unlicensed, which contradicts the whole purpose of the site.
6. **No daily-resolution F10.7 exists in the cleared set for 2018-05 → ~2026-05.** VERIFIED
   that the NCEI daily archive ends 2018-04-30 and that the SWPC `warehouse/YYYY/YYYY_DSD.txt`
   and `ftpdir/indices/old_indices/` paths cited in older documentation are all 404 today. The
   *monthly* F10.7 series covers the period continuously, so the solar-cycle envelope has no
   gap; only the daily texture does. Named fix: locate SWPC's current archived Daily Solar
   Data location, or resolve item 1 and use `fluxtable.txt` (which starts 2004, 3 samples/day).
7. **SDO and GOES images are `latest_*` snapshots, not dated products.** VERIFIED that a
   re-fetch returns different pixels. Reproducibility rests on the staged bytes and their
   sha256, not on the URL.
8. **The Fermi 3PC `TUNIT` keywords are empty for every column** (VERIFIED). Units come from
   the README, not the file. A FITS reader's unit attribute is not evidence here.

---

## 6. Ledger rows for `docs/data-sources.md`

### Add under "Currently used"

#### NASA Exoplanet Archive — <https://exoplanetarchive.ipac.caltech.edu/>

All 6,366 confirmed exoplanets from the `pscomppars` composite-parameters table: positions
(`ra`, `dec`, `sy_dist`), orbits, radii, masses, equilibrium temperatures, and host-star
parameters. The `ps` table (40,155 rows, one per publication per planet) is not used.

- **Accessed via**: the TAP synchronous endpoint, offline, at bake time. Never at runtime.
  Exact query in `data-staging/nasa-exoplanet-archive/SOURCE.txt`.
- **Terms**: operated by Caltech/IPAC under NASA contract. The archive's acknowledgement page
  states no restriction on use or redistribution and no non-commercial clause; NASA content is
  generally not subject to US copyright and may be used commercially subject to no-endorsement.
  **Commercial use: cleared.**
- **Required credit**: see §4. Cite Christiansen et al. (2025), PSJ.
- **Pitfall**: composite rows are assembled across publications and are not guaranteed
  self-consistent. `pl_bmasse` is often M·sin i, a lower bound. A TAP query naming a
  non-existent column returns **HTTP 200** with an Oracle error inside a VOTABLE — check the
  row count, never the status code.

#### Fermi LAT Third Pulsar Catalog (3PC) — <https://fermi.gsfc.nasa.gov/ssc/data/access/lat/3rd_PSR_catalog/>

294 gamma-ray pulsars with spin frequency, spin-down rate, sky position, distance and class —
the source for the pulsar sonification layer. Chosen because the ATNF catalogue is blocked.

- **Accessed via**: direct file download, offline. HDU 1 `PULSARS_BIGFILE` extracted to CSV
  with a pure-numpy FITS reader on triton (astropy is not installed there).
- **Terms**: NASA GSFC product, no proprietary period, no stated restriction. US Government
  work. **Commercial use: cleared**, with the provenance caveat recorded in
  `data-staging/fermi-lat-3pc-pulsars/LICENCE.txt` — 3PC collates some values from ATNF psrcat
  v1.68, which is itself blocked as a *file*; the values are facts and the 3PC table is a
  different compilation.
- **Required credit**: see §4 — names both NASA/FSSC and ATNF.
- **Pitfall**: `Type` is a stringified Python list of flags, and `'ATNF'` in it means "also in
  the ATNF catalogue", not a licence or provenance marker for that row. `TUNIT` keywords are
  empty throughout; units come from the README. `F0` is epoch-dependent — the Crab reads
  29.7169 Hz, not the textbook 30.

#### GWOSC — <https://gwosc.org/>

391 confident gravitational-wave events with source-frame masses, effective spin, luminosity
distance and network SNR, plus 32 s of H1 and L1 strain around GW150914.

- **Accessed via**: the event API and the event strain file service, offline.
- **Terms**: **CC BY 4.0.** Commercial use permitted, no share-alike. **Commercial use:
  cleared.** Modification must be indicated.
- **Required credit**: see §4, including the modification notice once the strain is filtered.
- **Pitfall**: GW events have **no single sky position** — they are localised to areas of tens
  to thousands of square degrees. Do not invent an RA/Dec. `allevents.json` mixes confident
  detections with marginal triggers, third-party reanalyses and a 2010 blind injection; filter
  on `catalog.shortName`. The raw strain is dominated by seismic noise three orders of
  magnitude above the chirp — VERIFIED, L1's 32-second window never crosses zero.

#### NOAA SWPC / NCEI — space weather and solar activity

Real-time solar wind and IMF at L1, planetary K index, the 10.7 cm radio flux record
(1947–present across NCEI and SWPC), and GOES SUVI / CCOR-1 solar imagery.

- **Accessed via**: `services.swpc.noaa.gov` and `ngdc.noaa.gov`, offline at bake time.
- **Terms**: US Government work, public domain under the NWS disclaimer. **Commercial use:
  cleared.** 17 U.S.C. § 403 notice required — see §4.
- **Required credit**: see §4, including the DRAO/NRC line for F10.7.
- **Pitfalls**: the `ssn` and `smoothed_ssn` columns of `observed-solar-cycle-indices.json`
  are the SILSO CC BY-NC series and **must not ship** (see the shared
  `data-staging/noaa-swpc-solar/LICENCE.txt`). The spacecraft behind `rtsw_*` changes per
  record — ACE and IMAP appeared in the same staged minute. Three missing-value sentinels
  coexist in one directory: `null`, `-1.0`, `-999`. The `products/solar-wind/*-7-day.json`
  endpoints are dead.

#### NASA SDO — <https://sdo.gsfc.nasa.gov/>

Five 1024×1024 stills — AIA 171/193/304/4500 Å and an HMI intensitygram — for the
"seen from Earth" solar view.

- **Terms**: *"SDO images and movies are not copyrighted unless explicitly noted."*
  **Commercial use: cleared**, with rule 4: browse products are not for science analysis.
- **Required credit**: see §4.
- **Pitfall**: `latest_*` URLs are not reproducible. AIA 304 Å is the chromosphere, not the
  surface — do not label it so.

### Add a "Rejected" section (or extend the existing one)

#### ATNF Pulsar Catalogue — <https://www.atnf.csiro.au/research/pulsar/psrcat/> — **REJECTED, commercially blocked**

No stated redistribution licence for `psrcat.db`; the download page and the package carry
none, the one GPL sentence names the *program*, and CSIRO's site notice requires consent for
any use beyond a single personal copy. Substituted by Fermi LAT 3PC at a cost of 4,393 → 294
rows. Full evidence: `data-staging/REJECTED-atnf-psrcat/LICENCE.txt`. **Do not re-check.**

#### SOHO / LASCO — <https://soho.nascom.nasa.gov/> — **REJECTED, commercially blocked**

The project's own copyright notice grants "public education efforts and non-commercial
purposes" with "no expressed authorization" and is silent on commercial use. SOHO is an
ESA/NASA joint mission, so NASA's public-domain default does not cover it, and NASA's media
guidelines expressly disclaim third-party material hosted on NASA servers. Substituted by
NOAA GOES-19 CCOR-1 (coronagraph) and GOES SUVI + NASA SDO (EUV disc), at the cost of ~30
years of CME archive and the wide C3 field. Full evidence:
`data-staging/REJECTED-soho-lasco/LICENCE.txt`. **Do not re-check.**

#### NRCan / spaceweather.gc.ca F10.7 — **REJECTED as a source, not as a quantity**

canada.ca's terms — the terms page spaceweather.gc.ca itself links to — state you may not
reproduce materials for commercial redistribution without prior written permission. **F10.7
itself is fine and is shipped**, taken from NOAA NCEI and SWPC. The distinction from the SILSO
trap, and the open.canada.ca OGL escape route that was checked and found closed, are in
`data-staging/REJECTED-nrcan-spaceweather-f107/LICENCE.txt`. **Do not re-check without
reading that distinction first.**

#### SILSO International Sunspot Number — **REJECTED, CC BY-NC**

Confirmed independently here; already documented by the Solar System audit at
`data-staging/silso-sunspots/`. NOAA's republication of the series inside
`observed-solar-cycle-indices.json` does not launder it. **Do not re-check.**

---

## 7. What the licence bar cost, stated plainly

| layer | wanted | shipped | loss |
|---|---|---|---|
| exoplanets | 6,366 planets | **6,366** | none |
| gravitational waves | GWTC catalogues + strain | **391 events + GW150914** | none |
| solar wind / geomagnetic | SWPC real-time products | **all of them** | none |
| solar imagery (disc) | SDO/AIA + SOHO/EIT | **SDO/AIA + GOES SUVI** | none of substance |
| solar imagery (corona) | SOHO/LASCO, 1996– | **GOES-19 CCOR-1, 2025–** | ~30 years of CME archive; smaller field of view |
| solar activity index | SILSO sunspots, 1749– | **F10.7, 1947–** | two centuries of history; a real measurement instead of a proxy count |
| **pulsars** | **4,393** | **294** | **93% of rows — the one real casualty** |

Six of seven layers ship essentially intact. The pulsar layer is the casualty, and the honest
mitigation is labelling: the app says *gamma-ray pulsars*, not *pulsars*, and the 294 it has
are the loudest, best-timed and most famous ones, spanning 1.2 Hz to 707 Hz — nine and a half
octaves for the sonification layer to work with.

## 8. Where the real-to-fictional bridges are

The owner's second requirement was that this be a bridge to fictional worlds, not only a
catalogue. Three seams fall out of the cleared data, and each one is a place where fiction can
sit *inside* something real rather than beside it:

1. **Exoplanets.** Every authored world should hang off a real host star from `pscomppars`, at
   that star's real position, temperature and luminosity. The transition to fiction becomes a
   zoom rather than a cut.
2. **Pulsars.** A pulsar is a lighthouse with a published, exact period. An authored world
   orbiting one inherits a real clock. **PSR B1257+12 b, c and d are in the staged exoplanet
   table** (VERIFIED by grep) — the first planets ever found outside the solar system were
   found around a pulsar, which makes this the strongest seam in the app: the same object
   appears in two cleared catalogues, one as a spin frequency and one as a planetary system.
3. **Gravitational waves.** A GW event is the one real astronomical object with a genuinely
   *unknown* location — a sky area, not a point. An authored world placed inside a localisation
   region contradicts nothing that is known.
