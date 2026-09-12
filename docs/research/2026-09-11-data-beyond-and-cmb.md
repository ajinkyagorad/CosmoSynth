# Beyond the Solar System: datasets from the nearby stars to the CMB

Research report, slug `data-beyond-and-cmb`, 2026-09-11. Read-only research: the only change
under `~/projects` is this file. Every fetch ran **from triton** (curl / Python urllib). The
laptop downloaded nothing. Scratch outputs are in `/tmp/cs-research/` on triton (ephemeral; see §14).

**Tags.** **VERIFIED** means I read the file, page or header, or ran the code, on 2026-09-11, and
the number came out of that run. **INFERRED** means it comes from the literature or my own
knowledge, or was derived by reasoning. Check INFERRED items before relying on them.

---

## 0. The answer in one screen

**Recommended minimal set, ranked by what it adds for what it costs.** Sizes are the shipped
payload, measured unless marked ~. Section 11 has the full budget.

| # | Layer | Source (exact product) | Ships as | Size | Licence gate |
|---|---|---|---|---|---|
| 1 | CMB sky | Planck PR3 `COM_CMB_IQU-smica-nosz_2048_R3.00_full.fits`, column `I_STOKES_INP`, streamed from the PLA | 4096×2048 R8 ICRS equirect, plus a 2048×1024 inpainting mask | 4.49 MB PNG-8 (1.32 MB at 2048×1024) | ESA **CC BY-NC 3.0 IGO**: WMAP ILC is the commercial-safe fallback |
| 1b | CMB dipole | Planck 2018 I, Table 2 (HFI) | computed analytically in the shader | 0 | cite |
| 2 | Better star distances | AT-HYG v4.0 `hyglike_from_athyg_v40.csv.gz`, a drop-in for HYG v4.4 | the existing `.csstars` format | ±0 (2.87 MB, replaces the current file) | CC BY-SA 4.0 (Gaia upstream: see §2) |
| 3 | Local Universe to ~300 Mpc | 2MRS, VizieR `J/ApJS/199/26/table3` | 16 B/galaxy | 0.70 MB | AAS/VizieR NC (see §2) |
| 4 | Laniakea and neighbouring basins | Cosmicflows-4 watershed labels `CF4_new_128-z008_BoA.fits` (and BoR), plus the CF4 64³ density | 128³ uint8 labels; 64³ float16 δ | 2.1 MB raw (37 KB zlib); ~0.5 MB | cite (no licence stated) |
| 5 | Distance-true galaxies | Cosmicflows-4 groups `J/ApJ/944/94/table3` | 16 B/group | 0.61 MB | AAS/VizieR NC |
| 6 | Local Group and Local Volume | LVDB (`dwarf_all.csv` + `gc_*.csv`) + UNGC giants | ~2k objects | ~0.2 MB ~ | **LVDB is CC0**; UNGC is AAS/VizieR NC |
| 7 | Milky Way dust, 10×10 kpc | Vergely+2022 `explore_cube_density_values_050pc_v2.fits` | uint8 log cube, 20 pc step (or 40 pc) | 10.3 MB raw / 6.2 MB zlib (1.25 / 0.93 MB at 40 pc) | A&A/VizieR "free for scientific usage" |
| 8 | Spiral arms, disc, halo | Reid+2019 Table 2 (analytic) · Skowron+2019 Cepheids · Baumgardt GC database v4 | analytic curves plus ~2.4k points | <0.1 MB | cite; Skowron has **no licence** |
| 9 | All exoplanets | NASA Exoplanet Archive `pscomppars`, all 6,360 rows | compact binary | ~0.4 MB ~ | NASA, verbatim acknowledgement |
| 10 | The cosmic web to z = 0.2 | SDSS DR18 SpecObj galaxies (783,530; subsample to ~200k) | 16 B/galaxy | 3.2 MB (12.5 MB for all) | **public domain** |

Everything else in this report is optional (§10). The **minimal set (#1–6)** adds about 9 MB and
takes the APK from 46.5 MB to about 55 MB. Adding #7–10 brings it to about 70 MB, well under the
150 MB target.

**Two findings that change the plan:**

1. **ESA archive data is CC BY-NC 3.0 IGO, not CC BY-SA 3.0 IGO.** This is VERIFIED against the
   ESDC terms page and the Gaia licence page. It covers Planck, Gaia and Hipparcos, and any
   commercial use requires writing to `data.licences@esa.int` first. `docs/data-sources.md` line 177,
   `DATA-GAPS.md` and the sibling `2026-09-11-architecture-map.md` (line 403) all say CC BY-SA
   3.0 IGO for Gaia, and all are out of date. VizieR adds another non-commercial layer on the
   AAS-journal catalogues (2MRS, CF4, Reid, UNGC). **Whether CosmoSynth will ever make money
   decides which of these can ship** (§2).
2. **The Planck file lies about its units.** In `smica-nosz R3.00`, the `I_STOKES_INP` column is in
   **µK**, although its `TUNIT` says `K_CMB`. I verified this: its rms is 102.5 against 103.6 µK for
   `I_STOKES`×10⁶, with correlation 0.989. Scaling it by the header produced a "CMB" with an
   rms of 100 K and no error from anywhere. The bake script in Appendix B now refuses any map
   whose rms is not between 30 and 300 µK.

---

## 1. What already exists: do not refetch

**CosmoSynth shipped data (VERIFIED, `unity/Assets/StreamingAssets/CosmoSynth/`):**

| File | Bytes | Content |
|---|---|---|
| `sky/stars.csstars` | 2,870,728 | 119,613 stars, header `<4sIII`, record `<3f f f I` (pc xyz, mag, K, flags), 24 B/star |
| `sky/stars_named.json` | 57,079 | proper names |
| `catalog/exoplanets.json` | 329,896 | `pscomppars`, queried 2026-07-30: **60 hosts + 120 planets**, "within 40.0 pc" |
| `catalog/solar_system.json` | 88,177 | 31 bodies |
| `catalog/spacecraft.json` | 978,886 | 24 craft |
| `models/*.csmesh` | ~2 MB | spacecraft meshes |
| `surfaces/earth.jpg`, `moon.jpg` | 238,676 / 139,068 | |

- **What the shipped star catalogue covers** (VERIFIED by reading `stars.csstars`): 61 stars within
  5 pc, **321 within 10 pc**, 1,890 within 20 pc, 3,055 within 25 pc, 24,687 within 100 pc and
  109,389 within 1 kpc. **10,224 carry `FLAG_DISTANCE_UNKNOWN`.** The median known distance is
  194 pc.
- **Only one APK is left** (VERIFIED): `~/builds/cosmosynth/recovered/CosmoSynth-release.apk`,
  46,520,034 B, dated 2026-08-01 23:23.
- **The exoplanet cap is binding, not the distance limit.** `build_exoplanets.py` defaults to
  `--max-distance-pc 60` and `--max-systems 60` (lines 383–386), and the shipped file used 40 pc.
  The archive has **471 hosts within 40 pc**, so the file carries 60/471 of what it claims to
  cover (§7.5).
- **Nothing on disk covers §3–§8.** `~/DATA-INVENTORY.md` (audited 2026-08-04) and `~/DATA-GAPS.md`
  list no CMB, galaxy, dust or Galactic-structure data anywhere on the box (VERIFIED).
- **Python on triton** (VERIFIED):
  - System: python 3.12.3, **numpy 2.5.1** (in `~/.local`), Pillow 10.2.0, `pdftotext`.
  - **Not installed anywhere:** healpy and astropy are absent from the system and from all 32 venvs
    found (`find ~ -maxdepth 4 -name pyvenv.cfg`).
  - h5py is in `~/venvs/{tsflow,neuro,splatmesh}`, `~/projects/PlanetaryVitals/.venv` and
    `~/projects/AtomicScopeXR/.venv`. scipy is in 12 venvs.
- **The Milky Way Atlas** (on the laptop, at
  `D:\PhD_Backup\OneDrive - Aalto University\Projects\Nebulytic\webxr-examples\Fable5\nebula-gallery`,
  VERIFIED by reading `README.md` and `tools/extract_data.py`):

  | File | Bytes | Content |
  |---|---|---|
  | `data/dust.bin` | 3,272,481 | 201×201×81 uint8 log cube over 2×2×0.8 kpc, **scraped from the HTML of GitHub `sb2580/k3d_dust`** |
  | `stars.bin` | 1,200,000 | 60k HYG v4.1 stars ≤1400 pc, from the archived GitHub HYG repo |
  | `cepheids.bin` | 35,424 | 2,214 Skowron Cepheids |
  | `globulars.bin` | 3,408 | 213 = LVDB `gc_harris` 155 + `gc_mw_new` 58 |
  | `galaxies.json` | 138,208 | 911 LVDB dwarfs, with M31/M33 hand-added |
  | `constellations.json` | 38,331 | Stellarium modern sky culture |
  | `*_landmarks.json` | | landmarks at hand-typed (l, b, d) |

  The sibling report `2026-09-11-milkyway-atlas-port.md` covers porting these files.

---

## 2. Licence findings (read before choosing sources)

| Source | Licence / terms (what I read) | Obligation | Commercial? | Tag |
|---|---|---|---|---|
| **ESA Space Science Archives** (Planck, Gaia, Hipparcos, …) | "Data hosted in the ESA Space Science Archives are distributed under the **CC BY-NC 3.0 IGO** licence … Prior to any commercial use … including any use or application that directly or indirectly generates a financial gain, a detailed request for authorisation/licence shall be made … data.licences@esa.int" | Credit lines: Planck "ESA, Planck Collaboration"; Gaia "ESA, Gaia DPAC"; Hipparcos "ESA" | **Only with ESA permission** | VERIFIED <https://www.cosmos.esa.int/web/esdc/terms-and-conditions>, <https://www.cosmos.esa.int/web/gaia-users/license> ("Gaia data are distributed under the CC BY-NC 3.0 IGO license") |
| Planck, extra acknowledgement text | "Based on observations obtained with Planck (http://www.esa.int/Planck), an ESA science mission with instruments and contributions directly funded by ESA Member States, NASA, and Canada." | use verbatim | — | INFERRED (standard text, from a web search, not read on an ESA page) |
| **VizieR / CDS** | "free of usage in a scientific context … The commercial usage of the data is subject to rules depending of the origin." Tables from **AAS journals (J/ApJ, J/ApJS, J/AJ) "are under CC-BY-NC licence"**. A&A tables are "free for a scientific usage". MNRAS follows the OUP policy | cite authors, catalogue and publisher; VizieR acknowledgement appreciated | per origin; **AAS = NC** | VERIFIED <https://cds.unistra.fr/vizier-org/licences_vizier.html>. INFERRED: AAS articles went CC BY 4.0 open access in 2022, so the CDS line may be stale for CF4 (2023). Ask CDS or the authors |
| NASA LAMBDA / WMAP | US Government work. Acknowledgement: "We acknowledge the use of the Legacy Archive for Microwave Background Data Analysis (LAMBDA), part of the High Energy Astrophysics Science Archive Center (HEASARC). HEASARC/LAMBDA is a service of the Astrophysics Science Division at the NASA Goddard Space Flight Center." | acknowledge | yes | INFERRED (search result; the LAMBDA policy page did not render) |
| NASA Exoplanet Archive | "This research has made use of the NASA Exoplanet Archive, which is operated by the California Institute of Technology, under contract with the National Aeronautics and Space Administration under the Exoplanet Exploration Program." | verbatim, plus per-reference credit | yes | VERIFIED <https://exoplanetarchive.ipac.caltech.edu/docs/acknowledge.html> |
| **SDSS** | "All SDSS data released in our public data releases are considered in the public domain." Images are CC BY, credit "Sloan Digital Sky Survey" | credit | yes | VERIFIED <https://www.sdss.org/collaboration/image-use-policy/> |
| **LVDB** (Pace) | **CC0-1.0** (GitHub API `license.spdx_id`). The README asks you to cite Pace 2025, OJAp 8, 142, doi:10.33232/001c.144859, and link the repo | cite (courtesy) | yes | VERIFIED |
| HYG v4.4 and AT-HYG v4.0 | CC BY-SA 4.0 (both LICENSE files) | attribution plus **share-alike** | yes, but upstream Hipparcos/Gaia values fall under the ESA terms above | VERIFIED licence files; the upstream interaction is a **lawyer question**, not a conclusion |
| Leike+2020 dust (Zenodo 3993082) · Edenhofer+2024 dust (Zenodo 8187943 / 10658339) | **CC BY 4.0** | attribution | yes | VERIFIED (Zenodo API) |
| Cosmicflows grids (IP2I Lyon) | no licence; the page says "If you use this data cite the article" | cite | unclear | VERIFIED page text |
| Skowron+2019 Cepheids (GitHub `jskowron/galactic_cepheids/data`) | "This data is **NOT licenced under the MIT or CC licenses**." Cite Skowron et al. | cite | unclear | VERIFIED `data/LICENSE` |
| `sb2580/k3d_dust`, the Atlas's dust source | **GPL-3.0** | copyleft | — | VERIFIED. **Do not reuse the Atlas's `dust.bin`**: fetch the primary cube instead (§6.4) |
| Stellarium `skycultures/modern` | "Text and data: **CC BY-SA 4.0**; Illustrations: Free Art License" | attribution plus SA | yes | VERIFIED `description.md`. `DATA-GAPS.md`'s "Stellarium is GPL" warning does not apply to this file |
| Planck and ESA *images* (for example "Gaia's sky in colour") | CC BY-SA 3.0 IGO | — | — | VERIFIED as quoted in the Skowron README. This is the image licence, which the project docs appear to have conflated with the data licence |

**Decision the user has to make, and early:** is CosmoSynth ever distributed commercially
(paid, in-app purchases, ads, or a monetised store listing)?

- **Free and non-monetised:** every source above ships with attribution, plus share-alike for the
  HYG-derived file.
- **Monetised:** Planck, Gaia/GCNS and Hipparcos-derived values need ESA's written permission or a
  substitute. The substitutes are **WMAP for the CMB** (NASA, clean) and **SDSS + LVDB** for
  galaxies. 2MRS, CF4, Reid and UNGC need the NC question resolved as well.

---

## 3. (a) The cosmic microwave background

### 3.1 Products, verified from triton

All HEADs and FITS headers below were read with byte-range requests (Appendix D lists every URL).
Every file is HEALPix, `ORDERING = NESTED`, `COORDSYS = GALACTIC`, `BAD_DATA = -1.6375e30`, big-endian.

| Product | URL (all return 200 from triton) | Bytes | Row layout | Nside |
|---|---|---|---|---|
| **SMICA-noSZ PR3 (recommended)** | `https://pla.esac.esa.int/pla/aio/product-action?MAP.MAP_ID=COM_CMB_IQU-smica-nosz_2048_R3.00_full.fits` (mirror: `https://irsa.ipac.caltech.edu/data/Planck/release_3/all-sky-maps/maps/component-maps/cmb/COM_CMB_IQU-smica-nosz_2048_R3.00_full.fits`) | 402,661,440 | 8 B: `I_STOKES` E (K_CMB), `I_STOKES_INP` E (**µK**, header says K_CMB) | 2048 |
| SMICA PR3 full IQU | same dirs, `COM_CMB_IQU-smica_2048_R3.00_full.fits` | 2,013,312,960 | 40 B: I, Q, U, TMASK, PMASK, I_INP, Q_INP, U_INP, TMASKINP, PMASKINP | 2048 |
| Commander PR3 | `…/COM_CMB_IQU-commander_2048_R3.00_full.fits` | 1,610,660,160 | (not inspected) | 2048 |
| SMICA PR2 (2015) | `https://irsa.ipac.caltech.edu/data/Planck/release_2/all-sky-maps/maps/component-maps/cmb/COM_CMB_IQU-smica_1024_R2.02_full.fits` | 176,209,920 | 14 B: I, Q, U (E) + TMASK, PMASK (B) | 1024 |
| Commander PR2 | `…release_2/…/COM_CMB_IQU-commander_1024_R2.02_full.fits` | 176,209,920 | | 1024 |
| Common T mask PR3 | `…release_3/ancillary-data/masks/COM_Mask_CMB-common-Mask-Int_2048_R3.00.fits` (PLA `MAP_ID` works too) | 201,335,040 | `TMASK` E | 2048 |
| Inpainting T mask PR3 | PLA `MAP_ID=COM_Mask_CMB-Inpainting-Mask-Int_2048_R3.00.fits` | (streamed) | `TMASK` E | 2048 |
| **WMAP 9-yr ILC** (fallback) | `https://lambda.gsfc.nasa.gov/data/map/dr5/dfp/ilc/wmap_ilc_9yr_v5.fits` | 25,174,080 | 8 B: `TEMPERATURE` E (mK), `N_OBS` E | 512, **1° resolution** |

- **PR4 (NPIPE):**
  - The NERSC portal `https://portal.nersc.gov/cfs/cmb/planck2020/` has frequency maps, misc
    masks, likelihoods and chains. **It has no component-separated CMB maps.**
  - IRSA has no `release_4/` (404).
  - The PLA answers "No results found" for `COM_CMB_IQU-{smica,sevem,commander}_2048_R4.00_full.fits`.
  - PR4 CMB maps may exist on the PLA under other IDs that I did not find. **Use PR3**, the
    2018 "Legacy" release. Its maps are the standard ones, and PR4 changes nothing visible at
    texture resolution (INFERRED).
- **Measured throughput from triton:**
  - **PLA about 33 MB/s.** It ignores `Range` and sends the whole file: a 20 MB range request
    received all 402 MB in 12 s.
  - **IRSA 0.67 MB/s**, but it honours `Range`.
  - **LAMBDA 0.16 MB/s** (25 MB in 153 s).
- **Measured mask fractions** (streamed from the PLA, 22 s):
  - The **inpainting mask flags 2.12 %** of the sky, which `I_STOKES_INP` fills with a constrained
    realisation.
  - The **common mask excludes 22.06 %**, so f_sky = 0.779.

### 3.2 Pure-numpy HEALPix (no healpy): written and tested

`healpix_nest.py` (Appendix A, 110 lines) ports `ang2pix` and `pix2ang` from HEALPix C++ for
NESTED ordering. Its self-check (VERIFIED, 5.4 s on triton) asserts four things:

- the 12 base-pixel centres are where Górski+2005 puts them (z = ±2/3, 0; φ = π/4 + kπ/2, kπ/2);
- the round trip `ang2pix(pix2ang(p)) == p` holds for **every pixel at Nside 1–64** and for
  200,000 random pixels at Nside 1024 and 2048;
- equal area holds: 3×10⁶ uniform random directions fill the 768 Nside-8 pixels to within 6σ;
- nesting holds: the children of parent p at order n+1 are 4p … 4p+3.

RING ordering is not needed, because every Planck and WMAP product checked is NESTED. Nested
ordering is what makes the bake cheap: **degrading Nside by 2ᵏ is `reshape(-1, 4ᵏ).mean(1)`**.

Pixel sizes follow θ_pix = √(4π / (12·Nside²)): 1.72′ at 2048, 3.44′ at 1024, 6.87′ at 512 and
13.7′ at 256 (formula).

### 3.3 Streaming bake: run on triton, outputs checked

`cmb_bake.py` (Appendix B) reads the FITS `BINTABLE` from the socket in about 1 M-row chunks,
picks one column, masks `BAD_DATA`, degrades by nested block-mean and projects to an **ICRS
equirectangular** texture. Row 0 is Dec +90° and column 0 is RA 0, with RA increasing along the row.
It writes nothing to disk except the output (VERIFIED runs):

| Run | Input | Out | Wall | Peak RAM | rms | p1 / p99 | min / max |
|---|---|---|---|---|---|---|---|
| WMAP ILC | Nside 512, 25 MB from LAMBDA | Nside 512 → 2048×1024 | 158 s (153 of them network) | 0.64 GB | 71.1 µK | −171 / +165 µK | −452 / +442 µK |
| **Planck SMICA-noSZ `I_STOKES_INP`** | Nside 2048, 402 MB from the PLA | Nside 1024 → 4096×2048 | **55.6 s** (24 s stream, 28 s projection) | 2.4 GB (chunk the projection to cut it) | 107.7 µK | −252 / +250 µK | −1034 / +654 µK |

**The orientation is proven, not assumed** (`check_frame.py`, VERIFIED):

- **The Galactic plane is where it should be.** Residual foreground peaks at |b| < 2°. For
  SMICA the texel rms is 116.0 µK at |b| < 2°, 113.6 at 2–10°, 108.2 at 10–30° and 105.1 at
  > 30°. For WMAP it is 88.7 / 80.9 / 71.4 / 66.3 µK.
- **The Cold Spot is where it should be.** The mean within 5° of (l, b) = (209°, −57°) is
  **−112 µK** in SMICA and −107 µK in WMAP. A mis-rotated texture would give about 0 ± 20.
- **WMAP and Planck agree.** Their correlation at |b| > 30° is 0.787 (WMAP resampled to 4096). It
  is limited by WMAP's 1° beam against Planck's 5′.

Preview PNGs (downscaled to 1024×512) are at `/tmp/cs-research/{smica,wmap}_small.png`. They show
the same large-scale pattern, with Planck finer.

### 3.4 Texture budget (measured from the SMICA bake)

The texture is quantised to 8 bits over ±500 µK (3.9 µK per step). Only 0.0014 % of texels clip.

| Size | PNG-8 (L) | raw R8 | zlib-9 raw | GPU as ETC2 EAC R11 (0.5 B/px, +33 % mips) |
|---|---|---|---|---|
| 4096×2048 | **4.49 MB** | 8.39 MB | 6.12 MB | ~5.6 MB ~ |
| 2048×1024 | 1.32 MB | 2.10 MB | 1.69 MB | ~1.4 MB ~ |
| 1024×512 | 0.36 MB | 0.52 MB | 0.43 MB | — |

Why 4096: 4096 px across 360° is 5.3′ per texel, which matches SMICA's ~5′ beam. Quest 3 resolves
roughly 25 px/deg (INFERRED), so a sky sphere would need 8192 to be pixel-limited, and the data
has no more detail than that anyway.

**Recommendation:**

- Ship the **anisotropy only**, as a single channel, and map it through a LUT in the shader. That
  gives false-colour switching, a contrast slider, and µK read-back for a "tap to measure" tool.
- **Sample by direction, not by mesh UV:** u = atan2(y, x)/2π and v = asin(z)/π + ½ in ICRF, then
  the ICRF→Unity conversion in `CelestialBodyView.ToUnityDirection`. This avoids the
  inside-the-sphere mirroring trap that `CLAUDE.md` warns about.
- Ship the **inpainting mask** (1 bit of information; 2048×1024 is ample) so the UI can say that
  2.1 % of the sky is Inferred.

### 3.5 The dipole, the monopole and the velocities (VERIFIED: Planck 2018 I, arXiv:1807.06205, Tables 2 and 3)

**Solar dipole, HFI 2018** (the paper recommends HFI as the best Planck determination):

| Quantity | Value |
|---|---|
| Amplitude D | **3362.08 ± 0.99 µK** |
| Direction | (l, b) = (**264.021 ± 0.011°, 48.253 ± 0.005°**) = RA 167.942°, Dec −6.944° (J2000), in Crater near Leo |
| Speed | **v = 369.82 ± 0.11 km/s**, β = 1.23357×10⁻³ |

The PR3 maps have the **2015 "nominal" dipole** (3364.5 µK, 264.00°, 48.24°) and its induced
quadrupole subtracted. A tiny residual dipole therefore remains in all released maps.

**Relative velocities from the same paper, Table 3:**

| Frame | Speed | (l, b) |
|---|---|---|
| Sun–CMB | 369.82 ± 0.11 km/s | (264.021°, 48.253°) |
| Sun–LSR | 17.9 ± 2.0 km/s | (48 ± 7°, 23 ± 4°) |
| LSR–Galactic centre | 239 ± 5 km/s | (90°, 0°) |
| GC–CMB | 565 ± 5 km/s | (265.76°, 28.38°) |
| Sun–Local Group | 299 ± 15 km/s | (98.4°, −5.9°) |
| **Local Group–CMB** | **620 ± 15 km/s** | (271.9 ± 2.0°, 29.6 ± 1.4°) |

**Other numbers:**

- The **monopole** is T₀ = 2.72548 ± 0.00057 K (Fixsen 2009, ApJ 707, 916). INFERRED: the paper
  cites Fixsen but I did not grep this exact value.
- The **Wien peak** is 2.8978 mm·K / T₀ = **1.063 mm**, so any rendering of the CMB is false colour
  (derived). The provenance label should say so.
- The **orbital (annual) dipole** is T₀·(29.8 km/s)/c ≈ **±271 µK**, modulated over the year
  (derived).
- The **distance to last scattering** is r∗/θ∗ = 144.43 Mpc / 0.0104110 = **13.87 Gpc comoving**,
  about 45.2 Gly (derived from VERIFIED Planck-alone r∗ and 100θ∗). The age of the Universe is
  **13.797 Gyr** and **H₀ = 67.36 ± 0.54**, both Planck alone (VERIFIED).

**Shader recipe** (exact to first order in β; the β² terms are about 3 µK and below any display):

```
T(n)  = T0 + D·(n·d̂) + ΔT_map(n)        d̂ = unit vector of (l,b)=(264.021°,48.253°) in ICRF
toggles: show monopole (uniform 2.725 K), show dipole (±3.36 mK), show anisotropy (±~250 µK)
```

- With the monopole removed, the dipole is ten times the anisotropies. A contrast slider (or
  separate "dipole" and "anisotropy" modes) is required, or the dipole saturates the LUT.
- **Seen from the Local Group** frame the dipole is 620 km/s toward (271.9°, 29.6°): about 5.64 mK.
  Seen from the Galactic centre it is 565 km/s. Picking a frame is one 3-vector.

### 3.6 WMAP against Planck

| | Planck PR3 SMICA-noSZ | WMAP 9-yr ILC |
|---|---|---|
| Resolution | ~5′ (Nside 2048) | 1° (Nside 512) |
| Download | 402 MB, PLA at 33 MB/s | 25 MB, LAMBDA at 0.16 MB/s |
| Masked or inpainted | inpainted column plus masks shipped | full sky; foreground residual on the plane |
| **Licence** | ESA **CC BY-NC 3.0 IGO**; commercial use needs ESA permission | **NASA, US Government work**; acknowledgement requested |
| Verdict | the best picture for a free app | the **clean fallback for a paid app**; visibly softer |

---

## 4. (b) Large-scale structure

### 4.1 Galaxy catalogues (VERIFIED from the CDS ReadMe files; sizes from directory listings; parse stats from `lss_sizes.py`)

| Catalogue | Fetch | Size | Rows | Needed columns (bytes) | Measured | 16 B/obj |
|---|---|---|---|---|---|---|
| **2MRS** (Huchra+2012) | `https://cdsarc.cds.unistra.fr/ftp/J/ApJS/199/26/table3.dat.gz` | 3.3 MB gz | 44,599 | RAdeg 18–26, DEdeg 28–36, GLON 38–46, GLAT 48–56, Ktmag 79–84, Rext 148–152 (log arcsec), b/a 154–158, type 165–169, **cz 174–178** (km/s, barycentric), e_cz 180–182 | 43,507 with cz > 0; cz/70: median 120 Mpc, p95 255, max 741 | **0.70 MB** (0.63 zlib) |
| 2MRS (Harvard original) | `http://tdc-www.harvard.edu/2mrs/2mrs_v240.tgz` | 39,501,956 | — | — | reachable; VizieR is enough | — |
| **Cosmicflows-4 groups** (Tully+2023) | `…/J/ApJ/944/94/table3.dat.gz` | 1.6 MB gz | 38,053 | 1PGC 1–7, **DMzp 9–14**, DMav 16–21, e_DMav 23–27, Vcmb 29–33, RAdeg 35–42, DEdeg 44–51, SGL 71–78, SGB 80–87 | d = 10^(DM/5+1): median 210 Mpc, p95 459, max 773 | **0.61 MB** |
| Cosmicflows-4 galaxies | `…/J/ApJ/944/94/table2.dat.gz` | 2.4 MB gz | 55,877 | PGC 1–7, Vcmb 23–27, **DM 29–34**, e_DM 36–40, RAdeg 138–145, DEdeg 147–154, GLON/GLAT 156–172, SGL/SGB 174–190 | median 204 Mpc, p95 450, max 804 | 0.89 MB |
| CF4 group velocities | `…/J/ApJ/944/94/table4.dat.gz` | 2.5 MB gz | 38,053 | (peculiar velocities) | not parsed | — |
| **6dFGS DR3** (Jones+2009) | `…/VII/259/6dfgs.dat.gz` | 5.0 MB gz | 124,647 | RA h/m/s 18–28, Dec 31–41, bJ 50–54, **cz 72–78**, q_cz 89 | q = 4: 106,777; q = 3: 8,265; q = 9 (SDSS/ZCAT): 9,605; 124,363 with cz > 300 | 1.99 MB |
| 2M++ (Lavaux & Hudson 2011) | `…/J/MNRAS/416/2840/catalog.dat.gz` | 2.3 MB gz | 72,973 | … | **3,813 are fake Zone-of-Avoidance galaxies (`ZoA = 1`) and some redshifts are cloned (`Cln = 1`)** | ~1.1 MB |
| **SDSS DR18 SpecObj** | SkyServer SqlSearch (below) | query | 783,530 galaxies, `zWarning = 0`, 0.005 < z < 0.2 · 1,693,382 at 0.2–0.8 · 2,639,535 in total · **753,228 QSOs** | ra, dec, z, petroMag_r | a 50k-row query took 8.1 s and returned 1.98 MB of CSV | 12.5 MB for all; 3.2 MB for 200k |

- **SDSS query (VERIFIED working):**
  ```
  https://skyserver.sdss.org/dr18/SkyServerWS/SearchTools/SqlSearch?format=csv&cmd=
    select s.ra,s.dec,s.z,p.petroMag_r from SpecObj s join PhotoObj p on s.bestObjID=p.objID
    where s.class='GALAXY' and s.zWarning=0 and s.z between 0.005 and 0.2
  ```
  Chunk it by RA range. INFERRED: SqlSearch caps each response at about 500k rows.
- **2M++ must not ship unfiltered.** Its fake ZoA galaxies are fabrications, so keep only
  `ZoA = 0`. Better still, skip 2M++: 2MRS plus 6dFGS plus SDSS cover what it merges.
- **Redshift space is not real space.** 2MRS, 6dFGS and SDSS distances are cz/H₀. They carry
  "fingers of God" (radial smearing of clusters by ~1000 km/s, ~10–15 Mpc) and belong in
  provenance as **Inferred (Hubble-law distance, H₀ stated)**. CF4 distances are measured, but at
  ~20 % scatter for Tully-Fisher (INFERRED from CF4 e_DM of ~0.4 mag). The layers will disagree
  in detail, and the UI should say which one is showing.

### 4.2 Cosmicflows grids: Laniakea as data, not artwork

From `https://projets.ip2i.in2p3.fr//cosmicflows/` (VERIFIED page and HEADs):

| File | Bytes | What |
|---|---|---|
| `CF4_new_128-z008_watersheds-fits.zip` | 67,127,665 | 4 × 16.78 MB FITS: `CF4_new_128-z008_BoA.fits` (basins of attraction, from galaxies), `…_BoR.fits` (repulsion), and `CF4gp_…` (from groups). Members are stored uncompressed, so one member can be range-fetched without the rest |
| `CF4_new_64-z008_delta.fits` | 1,054,080 | 64³ float32 overdensity δ |
| `CF4_new_64-z008_velocity.fits` | 3,150,720 | 3 × 64³ float32 velocity |
| `CF4gp_new_64-z008_delta.fits` | 1,054,080 | group-based δ |
| `CF4pp_mean_std_grids.npz` + `retrieve_CF4pp_grid_values.py` | 167,773,716 / 5,468 | CF4++ (Courtois+2025) mean and std of velocity and density |

**The geometry was verified against the paper, and the two file families use opposite axis
orders.** This is a trap.

- **Watershed grids** (`laniakea.py`, which range-fetches only the 16.78 MB BoA member):
  - 128³ **float64** (BITPIX −64), 9 labels. The box is 1000 Mpc/h centred on the observer
    (Dupuy & Courtois 2023, A&A; arXiv:2305.02339).
  - **Index `lab[iz, iy, ix]`** with i = ⌊(SG + 500) / 7.8125⌋, so FITS NAXIS1 is SGX.
  - With that indexing the Laniakea attractor SG[−62, −8, 39] reads **label 1** and the Apus
    attractor SG[−125, −23, −31] reads **label 2**, as the paper numbers them. The observer at the
    box centre reads label 1.
  - Laniakea is 4,079 voxels = **1.95×10⁶ (Mpc/h)³** (paper: 1.9×10⁶).
  - Both label grids compress hard: the Laniakea mask is 2,785 B zlib and all 9 labels as uint8
    are 36,984 B.
- **The 64³ δ grid** (`CF4_new_64-z008_delta.fits`): I searched 6 axis permutations × 2 flips × 5
  box sizes against 6 attractors and 2 voids. The unique best fit is a **1000 Mpc/h box indexed
  `g[ix, iy, iz]` (NAXIS1 = SGZ, the reverse of the watershed files)**. With it:
  - δ at the attractors: Laniakea 1.24, Apus 1.51, Perseus-Pisces 1.26, Shapley 1.94,
    Hercules 1.61, Lepus 1.39;
  - δ at the voids: Sculptor Void −1.12, "near Boötes" −2.32;
  - the score is 3.21 against 1.09 for the next-best geometry.

  The FITS carries no WCS, and Courtois+2023 (arXiv:2211.16390) says "64³ … about 8 h⁻¹ Mpc per
  voxel", which would imply a ~500 Mpc/h box. **The data disagree with that sentence, so treat the
  geometry as verified by fit.** Confirm it with a rendered SGX–SGY slice before shipping.
- **The h-scaled units need a stated h.** Mpc/h has to become Mpc: use h = 0.746 to match CF4's
  H₀ = 74.6 (VERIFIED in the ReadMe: "H_0_=74.6km/s/Mpc"), or 0.674 to match Planck. Record the
  choice (§9.4).

### 4.3 Named structures with published positions

**Gravitational basins from Dupuy & Courtois 2023, Table A.1** (VERIFIED from `pdftotext -layout`,
CF4 galaxy reconstruction). Coordinates are the attractor or repeller; SG is in Mpc/h; volume is
in 10⁶ (Mpc/h)³.

| Name | RA° | Dec° | V_CMB km/s | SGX | SGY | SGZ | Volume |
|---|---|---|---|---|---|---|---|
| **Laniakea** | 263 | −39 | 7,412 | −62 | −8 | 39 | 1.9 |
| Apus | 199 | −75 | 13,096 | −125 | −23 | −31 | 9.5 |
| Hercules | 237 | 9 | 11,213 | −39 | 70 | 78 | 3.1 |
| Lepus | 99 | −49 | 13,189 | −62 | −39 | −109 | 8.1 |
| Perseus-Pisces | 56 | 21 | 6,102 | 47 | −23 | −31 | 4.8 |
| Shapley | 199 | −40 | 15,468 | −141 | 62 | −16 | 7.9 |
| SDSS-1a | 133 | 24 | 34,117 | 117 | 227 | −227 | 184.8 |
| SDSS-2a | 209 | 12 | 32,979 | −102 | 297 | 102 | 169.8 |
| SDSS-2b | 218 | 36 | 33,007 | 31 | 289 | 156 | 130.2 |
| Sculptor Void (repeller) | 18 | −16 | 11,294 | 23 | −109 | −16 | 1.3 |
| near Boötes Void (repeller) | 194 | 13 | 12,908 | −31 | 125 | 8 | 7.8 |
| Dipole Repeller + CSR | 360 | 22 | 19,795 | 133 | −133 | 62 | 163.0 |

The same text gives Laniakea's central attractor as sgl = 187°, sgb = 32°, cz = 7370 km/s, "in
full accordance with the direction of the Great Attractor" (VERIFIED).

**Galaxy clusters, resolved through CDS Sesame/SIMBAD** (VERIFIED). J2000 degrees; the redshift
is SIMBAD's. d ≈ cz/H₀ is INFERRED; use CF4 group distances where they match.

| Name | SIMBAD id | RA | Dec | z |
|---|---|---|---|---|
| Virgo Cluster | NAME Virgo Cluster | 187.699 | +12.385 | 0.0038 |
| Coma | ACO 1656 | 194.935 | +27.912 | 0.0234 |
| Norma (Great Attractor core) | ACO 3627 | 243.594 | −60.869 | 0.01628 |
| Perseus | ACO 426 | 49.948 | +41.515 | 0.01836 |
| Leo | ACO 1367 | 176.186 | +19.700 | 0.0225 |
| Hercules | ACO 2151 | 241.313 | +17.749 | 0.0352 |
| Centaurus | ACO 3526 | 192.205 | −41.305 | 0.0114 |
| Fornax | ACO S 373 | 54.623 | −35.452 | 0.0050 |
| Hydra I | ACO 1060 | 159.150 | −27.518 | 0.0129 |
| **Shapley core** | ACO 3558 | 202.014 | −31.527 | 0.0474 |
| ACO 2199 | ACO 2199 | 247.144 | +39.541 | 0.0309 |
| Boötes Void | NAME Bootes Void | 222.946 | +45.796 | — (z ≈ 0.05, INFERRED) |
| Local Void | NAME Local Void | 279.5 | +18 | — (a coarse centre) |

- **Sesame's `<v>` field is not usable for galaxies.** For individual galaxies (M87, NGC 1275) the
  regex picked up a magnitude, so only the cluster redshifts above are meaningful.
- **INFERRED, and not verified here:** CfA2 Great Wall (z ≈ 0.03), Sloan Great Wall (z ≈ 0.07,
  ~400 Mpc long), and the Dipole Repeller (Hoffman+2017; it appears as "DR + CSR" in the table
  above).
- **Abell/ACO clusters** (VizieR `VII/110A`, VERIFIED listing): tables 3–6 hold 2,712 + 1,364 +
  1,174 + 274 clusters in 84 + 58 + 51 + 13 KB gz. Only sky positions and distance classes are
  there, with no redshifts, so it is useful only with a redshift cross-match.

---

## 5. (c) The Local Group and the Local Volume

| Source | Fetch | Size / rows | Notes | Licence |
|---|---|---|---|---|
| **LVDB** (Pace) | `https://raw.githubusercontent.com/apace7/local_volume_database/main/data/<file>.csv`; releases v1.1.1 (2026-08-12); Zenodo doi:10.5281/zenodo.14076714 | `dwarf_all.csv` 433 KB = **911** (`dwarf_mw` 68, `dwarf_m31` 43, `dwarf_local_field` 64, `dwarf_local_field_distant` 736); `gc_harris.csv` 155, `gc_mw_new.csv` 58, plus `gc_dwarf_hosted`, `gc_ambiguous`, `gc_other`, `pm_overview`, `j_factor` | Columns include ra, dec, **distance_modulus**, rhalf (arcmin), **ellipticity**, **position_angle**, apparent_magnitude_v, vlos_systemic, pmra/pmdec, metallicity, age, flux_HI. **Every row has a DM** (VERIFIED). **Max dwarf distance is 12.76 Mpc** (the Atlas README's "~17 Mpc" is wrong for the current LVDB). **Hosts (MW, M31, M33, M81 …) are not rows**; LMC is in `dwarf_mw`, SMC is hosted by the LMC | **CC0** |
| McConnachie 2012, updated Jan 2021 | `https://www.cadc-ccda.hia-iha.nrc-cnrc.gc.ca/files/vault/PANDAS/NearbyGals/NearbyGalaxies_Jan2021_PUBLIC.fits` (the old `NearbyGalaxies.dat` path is 404) | 60,480 B, 144 rows, 49 columns (GalaxyName, RA, Dec, EB-V, dmod ±, vh ±, Vmag …, systemic PMs) | Dwarfs only (no M31/MW rows); largely superseded by LVDB | none stated; cite |
| **UNGC** (Karachentsev+2013) | `https://cdsarc.cds.unistra.fr/ftp/J/AJ/145/101/table1.dat` | 106 KB, **869 galaxies**, D up to 26.2 Mpc | Name 1–18, RA 20–29, Dec 31–39, **a26 41–46 (arcmin)**, b/a 48–51, Kmag 79–83, TT 99–100, HRV 110–113, **Dist 115–119 (Mpc)**, method 121–124. **Includes the giants**: MESSIER031 0.77 Mpc (Cep, a26 190.55′, b/a 0.33), MESSIER033 0.85, MESSIER081 3.63, LMC 0.05, Milky Way | AAS/VizieR (NC) |

**Recommendation:** use LVDB for every dwarf and globular cluster (CC0, current, with morphology
columns), and take the ~20 giant hosts from UNGC distances and sizes, or hand-enter them with
citations. That is exactly the gap the Atlas papered over with a 2-row hand list.

---

## 6. (d) Milky Way structure

### 6.1 Spiral arms: Reid+2019 (ApJ 885, 131; arXiv:1910.03357), VERIFIED

- **Masers:** VizieR `J/ApJ/885/131/table1.dat` (24 KB, **199 rows**). Columns: Name 1–13,
  RA 32–44, Dec 47–59, **plx 63–67 (mas)**, e_plx 69–73, pmE, pmN, VLSR, **Arm 114–116**.
- **The arm model is not in VizieR.** It is Table 2 of the paper (read from the arXiv PDF):
  `ln(R/R_kink) = −(β − β_kink) · tan ψ`. β is the Galactocentric azimuth (0 toward the Sun,
  increasing with rotation), and ψ< / ψ> apply for β ≤ / > β_kink.

| Arm | N | β range° | β_kink° | R_kink kpc | ψ<° | ψ>° | width kpc |
|---|---|---|---|---|---|---|---|
| 3-kpc(N) | 3 | 15→18 | 15 | 3.52 | −4.2 | −4.2 | 0.18 |
| Norma | 11 | 5→54 | 18 | 4.46 | −1.0 | 19.5 | 0.14 |
| Sct-Cen | 36 | 0→104 | 23 | 4.91 | 14.1 | 12.1 | 0.23 |
| Sgr-Car | 35 | 2→97 | 24 | 6.04 | 17.1 | 1.0 | 0.27 |
| Local | 28 | −8→34 | 9 | 8.26 | 11.4 | 11.4 | 0.31 |
| Perseus | 41 | −23→115 | 40 | 8.87 | 10.3 | 8.7 | 0.35 |
| Outer | 11 | −16→71 | 18 | 12.24 | 3.0 | 9.4 | 0.65 |

- The model assumes **R₀ = 8.15 kpc**. The paper's fit gives Θ₀ = 236 ± 7 km/s and
  Θ₀ + V⊙ = 247 ± 4 km/s, and an arm width law of w(R) = 336 + 36 (R − 8.15) pc (VERIFIED).
- **Honesty:** outside the β ranges in the table these curves are **extrapolations**, and the
  Atlas deliberately refused to draw them. Draw them only over the fitted β range, tagged
  "Inferred (log-periodic fit)", and fade them outside it.

### 6.2 The disc and its warp: Skowron+2019 classical Cepheids (Science 365, 478)

- **Source:** `https://raw.githubusercontent.com/jskowron/galactic_cepheids/master/data/Data_Table_1.dat`,
  575,072 B, **2,431 rows, of which 2,214 have DIST > 0** (VERIFIED). Columns (whitespace
  separated): SOURCE, ID, **L, B (deg), DIST, dDIST (pc), AGE (Myr)**, PERIOD, MODE, photometry,
  A_Ks.
- **Licence:** the data is **not** MIT or CC licensed; cite Skowron+2019 (§2).
- **Shipped size:** 2,214 × 16 B = 35 KB.

### 6.3 The halo: globular clusters

| Source | Fetch | Rows | Use |
|---|---|---|---|
| **Baumgardt GC database v4** (Mar 2023) | `https://people.smp.uq.edu.au/HolgerBaumgardt/globular/orbits_table.txt` (43,346 B, updated 2024-10-16) | ~165 (the file is 168 lines with 3 header lines) | Name, RA, Dec, l, b, **Rsun (kpc)** ± error, R_GC, RV, PMs, **X, Y, Z (kpc)**, U, V, W: the best single source for 3D positions |
| Vasiliev & Baumgardt 2021 | VizieR `J/MNRAS/505/5978/tablea1.dat` (19 KB) | 170 | PMs and parallaxes; no distances |
| Harris 2010 | `https://physics.mcmaster.ca/~harris/mwgc.dat` (49,364 B, dated 2018-09-18) | 157 | classic; VizieR `VII/202` is the 1997 version (147) |
| LVDB | `gc_harris.csv` (155) + `gc_mw_new.csv` (58) | 213 | what the Atlas uses; CC0 |

### 6.4 3D dust: sizes and fetchability, VERIFIED

| Map | Where | Extent and grid | Files | Fetchable? |
|---|---|---|---|---|
| **Vergely, Lallement & Cox 2022** (A&A 664, A174) | `https://cdsarc.cds.unistra.fr/ftp/J/A+A/664/A174/fits/` | `…density_values_010pc_v2.fits`: 601×601×161, 3×3×0.8 kpc (5 pc step); `…_025pc_v2`: 601×601×81, 6×6×0.8 kpc (10 pc); **`…_050pc_v2`: 501×501×41, 10×10×0.8 kpc, `STEP = 20`, `RESOL = 50`, `UNIT = 'A0(550nm)/parsec'`, `SUN_POS = (250.5, 250.5, 20.5)`**; plus 2 error cubes | 222 / 112 / **40** / 40 / 40 MB; ASCII `cube_ext.dat` is 7.0 GB (ignore it) | **Yes, CDS at ~1.3 MB/s.** Downloaded, measured and deleted: max 0.01375 mag/pc, median 1.36e-4; A₀ from the Sun +5 kpc toward the GC 6.3 mag, toward the anticentre 3.0, to the NGP 0.058. uint8 log cube: 10.29 MB raw, **6.22 MB zlib**; at 40 pc step (250×250×20) 1.25 MB, **0.93 MB zlib**. Axis order (z, y, x), +x toward the GC (INFERRED from the extinction asymmetry and the ReadMe's "X axis is directed to the Galactic Centre") |
| Lallement+2019 (A&A 625, A135) | `…/J/A+A/625/A135/` | 6×6×0.8 kpc, 25 pc | `map3D_GAIAdr2_feb2019.h5.gz` 772 MB, `STILISM_cube.fits.gz` 1.5 GB | yes, but **superseded by Vergely 2022** |
| **Edenhofer+2024** (A&A 685, A82; data 2023) | Zenodo `10658339` (v1.0.2, which adds `interp2box.py`); `8187943` is v1.0 | Sun-centred HEALPix **Nside 256 NESTED × 516 log-spaced shells, 68.81 → 1244.6 pc** (ratio 1.0056376/shell). Unit "E of Zhang, Green and Rix (2023)" per pc | `mean_and_std_healpix.fits` **3,252,715,200 B**; `mean_and_std_xyz.fits` 15.7 GB (2 pc grid); samples 19.5 GB; a 2 kpc validation map as well | **Yes, by range.** The MEAN HDU is an IMAGE of 516 × 786,432 float32 (big-endian) starting at **byte 5760**. Shell i is `[5760 + i·3,145,728, +3,145,728)`. The STD header is at 1,623,202,560 and the RADIAL PIXEL CENTERS table at 3,246,402,240 (+2880 bytes of header, 516 float32). A 5 pc Cartesian resample of the inner 1.25 kpc sphere can be streamed without storing the file, which removes the "too big for the disk" objection in the sibling Atlas report. CC BY 4.0 |
| Leike, Glatzle & Enßlin 2020 (A&A 639, A138) | Zenodo `3993082` | 740×740×540 at **1 pc**, x, y ±370, z ±270 pc; natural-log G-band extinction per pc | `mean_std.h5` 2.37 GB, `samples.h5` 14.2 GB | Yes, but HDF5 needs h5py (use `~/venvs/neuro`) and a 2.4 GB temporary file. CC BY 4.0. Highest resolution; best inside 370 pc |
| Green+2019 Bayestar19 | Harvard Dataverse doi:10.7910/DVN/2EJ9TX (answered 202) | 3/4 sky, dec > −30°, per-sightline reddening | ~GB, INFERRED | not a cube; **skip** |
| `sb2580/k3d_dust` (the Atlas's source) | GitHub | 201×201×81 resample of unknown provenance | an HTML blob | **GPL-3.0: do not use** |

**Physics limit, carried over from the Atlas and correct:** no whole-Galaxy dust cube exists.
Extinction tomography needs stars *behind* the dust, so the far side of the disc is unmappable.
The largest real cube is Vergely's 10×10×0.8 kpc one. Beyond it, use the Reid arms (Inferred),
the Cepheids (Measured) and a procedural disc (Procedural, labelled as such).

**Galactic constants to use** (one table in Core):

| Constant | Value | Tag |
|---|---|---|
| R₀ | 8.15 kpc | Reid 2019, VERIFIED (GRAVITY 2019 8.178 and 2022 8.277 are INFERRED alternatives) |
| Θ₀ | 236 km/s | VERIFIED |
| Sun–LSR | 17.9 km/s toward (48°, 23°) | Planck Table 3, VERIFIED |
| z⊙ | ≈ 20.8 pc | Bennett & Bovy 2019, INFERRED |

---

## 7. (e) Stars

### 7.1 HYG v4.4, currently shipped (VERIFIED)

`https://codeberg.org/astronexus/hyg/media/branch/main/data/hyg/CURRENT/hyg_v44.csv.gz` is
13,636,362 B (Last-Modified 2026-07-12) and is the **only** file in `CURRENT/`; the importer
already uses it.

- The GitHub `astronexus/HYG-Database` repo that the Atlas fetched from is **archived**, although
  `hygdata_v41.csv` still downloads (33.9 MB).
- Licence: CC BY-SA 4.0.

### 7.2 AT-HYG v4.0: the cheap upgrade (VERIFIED)

`https://codeberg.org/astronexus/athyg/media/branch/main/data/athyg_40.csv.gz` is 199,688,001 B:
"over 2.5 million stars", Tycho-2 augmented with Gaia DR3, and "Nearly 99% … have distance data
from Gaia DR3". Its subsets are in `…/data/subsets/`:

| Subset | Stars | Size |
|---|---|---|
| `athyg_40_reduced_m10.csv.gz` | 332,178 | 27.6 MB |
| `athyg_40_reduced_m11.csv.gz` | 875,292 | 70.1 MB |
| `athyg_40_classic_ids.csv.gz` | 317,175 | — |
| **`hyglike_from_athyg_v40.csv.gz`** | 119,670 | 14,096,722 B |

The HYGLike subset is in the **HYG column schema** plus `*_src` columns. I downloaded it,
measured it and deleted it:

- **only 146 rows lack a distance, against 10,224 in today's build**;
- `dist_src` is G_R3 (Gaia DR3) for 117,154 rows, H (Hipparcos) 1,741, G_R2 586, GJ 175, other 14;
- 305 stars within 10 pc and 23,530 within 100 pc. That is slightly fewer than HYG, because
  AT-HYG drops some Gliese secondaries, as its README says.

**`build_starfield.py` needs little more than a new `SOURCE_URL`**, plus a check of its column
parsing: the fields `comp`, `lum`, `var` are placeholders in HYGLike. The v4.4 attribution string
becomes "AT-HYG v4.0 (astronexus), CC BY-SA 4.0". The upstream Gaia terms question in §2 applies.

### 7.3 Gaia DR3 subsets

- **TAP works from triton** (VERIFIED):
  ```
  https://gea.esac.esa.int/tap-server/tap/sync?REQUEST=doQuery&LANG=ADQL&FORMAT=csv&QUERY=SELECT TOP 2 source_id,ra,dec,parallax FROM gaiadr3.gaia_source WHERE parallax>500
  ```
  It returned Barnard's star (546.98 mas) and Proxima (768.07 mas).
- **Gaia Catalogue of Nearby Stars** (GCNS, EDR3; VizieR `J/A+A/649/A6`, VERIFIED):
  - `table1c.dat.gz` is 72 MB with **331,312 objects within 100 pc**, 13× HYG's 24,687 at
    100 pc. There are also 880,428 rejected objects and 1,211,740 distance PDFs.
  - Fetch slim columns only:
    `https://vizier.cds.unistra.fr/viz-bin/asu-tsv?-source=J/A%2BA/649/A6/table1c&-out=RA_ICRS,DE_ICRS,Plx,Gmag&-out.max=unlimited`.
    It works; BP/RP are under their own names in the ReadMe.
  - 331k × 16 B = **5.3 MB**.
  - Licence: ESA CC BY-NC.
- **Hipparcos new reduction** (van Leeuwen 2007; VizieR `I/311`): `hip2.dat.gz` 8.6 MB, 117,955
  stars (VERIFIED). HYG already contains it.

### 7.4 Completeness within 10 pc

- **Reylé+2021**, updated 2023-08-25 (VizieR `J/A+A/650/A201/tablea1.dat.gz`, 77 KB, 562 rows,
  VERIFIED). Its abstract gives **"541 stars, brown dwarfs, and exoplanets in 339 systems"**.
  Columns: Seq, Sys, Name, **ObjType 41–46**, RAdeg 79–91, DEdeg 94–106, Epoch, **plx 115–122**,
  pm, RV, **SpType 302–309**, Gmag 353–361, V/J/H/K, Nexopl, GaiaEDR3 id.
- **HYG gives 321 within 10 pc and AT-HYG 305.** The gap is mostly brown dwarfs, faint M dwarfs
  and exoplanets.
- **Ship Reylé as the "nearest stars" layer** (a few KB), tagged per object type. It is the
  authoritative census.
- RECONS `http://www.recons.org/TOP100.posted.htm` answers 200 but was last modified 2012-01-02:
  stale. Use Reylé.

### 7.5 NASA Exoplanet Archive `pscomppars` (VERIFIED, queried 2026-09-11)

| Count | Planets | Hosts |
|---|---|---|
| All | **6,360** | **4,769** |
| Within 40 pc | 758 | 471 |
| Within 100 pc | 1,569 | — |

- 6,332 planets have `sy_dist`. The latest `disc_year` is 2026.
- The shipped file holds 120 planets and 60 hosts, dated 2026-07-30.
- The 20 columns `build_exoplanets.py` uses total **1,203,219 B** of CSV (396,860 B gzipped) for
  all 6,360 rows.
- **The JSON-with-provenance format does not scale:** 330 KB for 180 bodies is ~1.8 KB per body,
  so all 11,129 bodies would be ~20 MB. Ship a compact table (~64 B/planet ≈ 0.4 MB, INFERRED)
  and build the provenance-rich records only for the system being visited, as `NearbyStarSystems`
  already does for generated worlds.

---

## 8. (f) Heliosphere, local interstellar medium and Oort cloud

| Quantity | Value | Source | Tag |
|---|---|---|---|
| Voyager 1 heliopause | 2012-08-25 at **121.6 au** | Stone+2013 / Nature Astronomy 2019 series | VERIFIED (search summary of the Nature Astronomy 2019 papers) |
| Voyager 2 heliopause | 2018-11-05 at **119 au** | Richardson+2019, Nat Astron 3, 1019; Stone+2019 | VERIFIED (same) |
| Voyager 1 termination shock | 2004-12-16 at ~94 au | Stone+2005, Science 309, 2017 | INFERRED |
| Voyager 2 termination shock | 2007-08-30 at ~84 au | Stone+2008, Nature 454, 71 | INFERRED |
| Interstellar He flow (IBEX, 6 yr) | **25.4 ± 1.1 km/s**; downwind ecliptic λ = 75.6 ± 1.4°, β = −5.12 ± 0.27° (so the upwind "nose" is λ ≈ 255.6°, β ≈ +5.1°); T = 8000 ± 1300 K | McComas / Schwadron+2015, ApJS 220 | VERIFIED (search summary of the ApJS paper III abstract) |
| Bow shock | none; a "bow wave" | McComas+2012, Science 336, 1291 | INFERRED |
| Heliotail | several hundred to thousands of au downwind | Voyager-era modelling (the arXiv:1912.02476 text says "thousand au in the downwind direction") | INFERRED |
| Kuiper belt, classical | 30–50 au; scattered disc beyond | — | INFERRED |
| Sedna | q ≈ 76 au, Q ≈ 900+ au | — | INFERRED |
| Inner Oort (Hills) cloud | ~2,000–20,000 au | Dones+2015, Space Sci Rev 197, 191 | INFERRED |
| Outer Oort cloud | ~20,000 to ~100,000–200,000 au (0.5–1 pc), bounded by the Galactic tide | Dones+2004/2015 | INFERRED |
| Oort cloud population | ~10¹¹–10¹² comets > 1 km; mass of a few M⊕ (very uncertain) | — | INFERRED |

- **Honesty:** the Oort cloud has **never been observed**. It is inferred from the orbits of
  long-period comets and must carry the **Inferred** tag, drawn as a diffuse statistical shell,
  never as a crisp sphere.
- **The heliopause crossings come free.** CosmoSynth already has Voyager 1 and 2 trajectories from
  JPL Horizons, so the crossing *points* are computable from data already on the box
  (`~/.cache/planetaryvitals` holds the Horizons responses, per `DATA-INVENTORY.md`).
- **The sibling report `2026-09-11-solar-heliosphere-port.md`** covers the heliosphere rendering.
  The numbers in this table are the ones to reconcile with it.
- **Local interstellar clouds:** Redfield & Linsky 2008 (ApJ 673, 283) give 15 clouds within
  15 pc as velocity vectors plus sky outlines (INFERRED; I did not fetch them). The Local Bubble
  shell (Pelgrims+2020) was not found at the CDS path I tried (`J/A+A/636/A17` is 404). Leave both
  for later.

---

## 9. Frames and formulas, verified against catalogue columns

### 9.1 ICRS to Galactic

This is the same matrix as in the Atlas and in Appendix B; its rows are the Galactic x, y, z axes
expressed in ICRS:

```
[[-0.0548755604, -0.8734370902, -0.4838350155],
 [ 0.4941094279, -0.4448296300,  0.7469822445],
 [-0.8676661490, -0.1980763734,  0.4559837762]]
```

Checked against all 44,599 2MRS RA/Dec → GLON/GLAT pairs: median error 1.08″, max 27.8″
(VERIFIED). That is far below any visual need; the residual is 2MRS's own rounding and
transformation.

### 9.2 Galactic to supergalactic

The pole is at (l, b) = (47.37°, +6.32°) and SGL = 0 lies at (137.37°, 0) (de Vaucouleurs). The
rows are SGX, SGY, SGZ in Galactic coordinates:

```
[[-0.7357425748,  0.6772612964,  0.          ],
 [-0.0745537784, -0.0809914713,  0.9939225904],
 [ 0.6731453021,  0.7312711658,  0.1100812622]]
```

Checked against all 55,877 CF4 GLON/GLAT → SGL/SGB pairs: **median 0.17″** (max 6′, a few rows
near the poles) (VERIFIED).

### 9.3 Distances

- From a distance modulus: d_pc = 10^(DM/5 + 1).
- From a redshift: d = cz/H₀ for z ≲ 0.05. Beyond that use the ΛCDM comoving distance, and state
  (H₀, Ωm) as Planck 67.36 / 0.3153 (VERIFIED, Planck alone).

### 9.4 H₀ is a visible choice

- **The Hubble tension shows up as a 10 % radial scale mismatch.** CF4 distance moduli are
  calibrated to **H₀ = 74.6** (VERIFIED, ReadMe). Redshift distances (2MRS, SDSS) scale as 1/H₀.
  If one layer uses 67.4 and the other 74.6, the same cluster sits at two radii.
- Pick **one** H₀ for every redshift-derived distance, and write it into each file's `source`
  field. Recommended: 74.6 inside ~300 Mpc, so the redshift layers agree with CF4, the watershed
  grid and the named basins.

### 9.5 Grid conventions (both VERIFIED, §4.2)

- **Watershed grids:** `lab[iz, iy, ix]`, i = ⌊(SG_Mpc/h + 500) / 7.8125⌋.
- **CF4 64³ δ:** `g[ix, iy, iz]`, 15.625 Mpc/h per voxel, observer at the centre.
- **Vergely 2022:** array (z, y, x), step 20 pc (in the 50 pc-resolution file), Sun at voxel
  (250, 250, 20).
- **Edenhofer 2024:** Sun-centred, the radius from the RADIAL PIXEL CENTERS table, the direction
  from `pix2ang_nest(256, p)` in Galactic coordinates.

---

## 10. Ranked shopping list with fetch-and-derive sketches

**Rank = value / (bytes + licence risk + engineering).**

- **MUST:** the minimal set.
- **SHOULD:** high value, affordable.
- **COULD:** later.
- **SKIP:** superseded or dishonest.

All fetches run on triton. All outputs go to `unity/Assets/StreamingAssets/CosmoSynth/<layer>/`
with a `source` field, per project convention.

- **Stdlib versus numpy.** `tools/cosmodata/` is standard-library only. Two routes follow from that:
  - The HEALPix bake and the cube resamples are practical with **numpy** (installed system-wide on
    triton, 2.5.1), and it could be recorded as a second deliberate exception, like the one for
    `npx`.
  - Alternatively, port `healpix_nest` into engine-free **Core C#**, where `run-tests.sh` already
    runs. The Python self-check in Appendix A translates 1:1 into a test.
- **Every derived file** should be written to a temporary name and `os.replace`d, and validated by
  magic bytes, as `CLAUDE.md` requires.

### MUST

1. **CMB anisotropy, Planck PR3 SMICA-noSZ, plus the inpainting mask.**
   ```sh
   python3 cmb_bake.py "https://pla.esac.esa.int/pla/aio/product-action?MAP.MAP_ID=COM_CMB_IQU-smica-nosz_2048_R3.00_full.fits" \
       I_STOKES_INP 1 1024 4096 out/cmb_smica          # scale 1: that column is already µK
   # mask: same streaming read of COM_Mask_CMB-Inpainting-Mask-Int_2048_R3.00.fits (TMASK, 0 = inpainted),
   # degrade with max-of-children, project at 2048x1024, 1 byte per texel
   # then quantise ±500 µK to R8 (or keep float16) and write a sidecar with scale/offset + provenance
   ```
   **Fallback (commercial):** `cmb_bake.py https://lambda.gsfc.nasa.gov/data/map/dr5/dfp/ilc/wmap_ilc_9yr_v5.fits TEMPERATURE 1000 512 2048 out/cmb_wmap`.

   **Provenance:** Measured over 97.9 % of the sky, Inferred (inpainted) over 2.1 %, false colour.
2. **CMB dipole.** No data: constants D = 3362.08 µK and d̂ from (l, b) = (264.021°, 48.253°),
   plus the Local Group and GC frames from §3.5 Table 3, as constants in Core.
3. **Stars: AT-HYG HYGLike drop-in.** Run `build_starfield.py` with `SOURCE_URL` set to
   `https://codeberg.org/astronexus/athyg/media/branch/main/data/subsets/hyglike_from_athyg_v40.csv.gz`,
   update the attribution, and re-run `StarCatalogTests`: Sirius, Vega and Arcturus will move
   slightly. Expect distance-unknown to fall from 10,224 to about 146.
4. **2MRS.**
   ```sh
   curl -s https://cdsarc.cds.unistra.fr/ftp/J/ApJS/199/26/table3.dat.gz | gunzip > 2mrs.dat
   # python: RA=[17:26] Dec=[27:36] cz=[173:178] Kt=[78:84] Rext=[147:152] b/a=[153:158] type=[164:169]
   #   keep cz>0; d = cz/H0 (state H0); xyz ICRF Mpc float32 + Kt f16 + size f16 -> .csgal (16 B)
   ```
5. **Cosmicflows-4 groups plus watershed basins plus δ.**
   ```sh
   curl -s https://cdsarc.cds.unistra.fr/ftp/J/ApJ/944/94/table3.dat.gz | gunzip > cf4g.dat   # DMzp [8:14], RA [34:42], Dec [43:51]
   python3 laniakea.py      # Appendix C: range-fetches CF4_new_128-z008_BoA.fits only; add BoR the same way
   curl -sO https://projets.ip2i.in2p3.fr//cosmicflows/CF4_new_64-z008_delta.fits      # 1 MB; g[ix,iy,iz], 1000 Mpc/h
   # write labels as 128^3 uint8 (37 KB zlib) + δ as 64^3 float16 (0.5 MB); SG->Galactic->ICRF via §9
   ```
   The basin names come from Table A.1 (§4.3), in label order: 1 Laniakea, 2 Apus, 3 Hercules,
   4 Lepus, 5 Perseus-Pisces, 6 Shapley, … (the labels follow the table's row order, blank rows
   skipped; VERIFIED for labels 1 and 2 only).
6. **LVDB plus the giant hosts.**
   ```sh
   for f in dwarf_all gc_harris gc_mw_new; do curl -sO https://raw.githubusercontent.com/apace7/local_volume_database/main/data/$f.csv; done
   curl -s https://cdsarc.cds.unistra.fr/ftp/J/AJ/145/101/table1.dat > ungc.dat   # take the ~20 hosts: MESSIER031/033/081/082/101, Milky Way, NGC 5128, NGC 253, Maffei1/2, IC 342 …
   # d = 10^(distance_modulus/5+1); keep ra,dec,d,rhalf,ellipticity,position_angle,M_V,host; pin LVDB release tag v1.1.1
   ```

### SHOULD

7. **Vergely 2022 dust.**
   ```sh
   curl -sO https://cdsarc.cds.unistra.fr/ftp/J/A+A/664/A174/fits/explore_cube_density_values_050pc_v2.fits   # 40 MB, then delete
   # numpy >f4 (41,501,501) (z,y,x), step 20 pc, Sun at (20,250,250); log-encode to uint8 (floor = p1 of positive values);
   # optionally 2x downsample to 40 pc (0.93 MB zlib). Sidecar: extent ±5000/±5000/±400 pc, unit A0(550nm)/pc, vmin/vmax.
   ```
   For more local detail, add the 025pc cube (112 MB → 6 kpc at 10 pc), or Edenhofer (item 13).
8. **Arms, Cepheids and globular clusters.**
   - Reid Table 2 goes into Core as constants: 7 arms × (β range, β_kink, R_kink, ψ<, ψ>, width).
   - Skowron:
     `curl -s https://raw.githubusercontent.com/jskowron/galactic_cepheids/master/data/Data_Table_1.dat`
     → keep DIST > 0 (2,214) → (l, b, d, age).
   - Baumgardt: `curl -s https://people.smp.uq.edu.au/HolgerBaumgardt/globular/orbits_table.txt`
     → X, Y, Z already in kpc (Galactocentric; check the sign convention in its header).
   - Reid masers: `curl -s https://cdsarc.cds.unistra.fr/ftp/J/ApJ/885/131/table1.dat` → d = 1/plx
     (plx 63–67), arm code 114–116.
9. **All exoplanets.**
   ```
   https://exoplanetarchive.ipac.caltech.edu/TAP/sync?format=csv&query=select <20 cols> from pscomppars
   ```
   That is 1.2 MB of CSV. Write a compact binary table; expand to the provenance JSON only for the
   visited system. Raise or remove `--max-systems`.
10. **SDSS galaxies.** Run the SqlSearch in §4.1 in about 16 RA chunks (8 s per 50k rows,
    measured), stratify-subsample to about 200k by redshift (3.2 MB), and state H₀.
11. **Nearest-star census (Reylé 10 pc).**
    `curl -s https://cdsarc.cds.unistra.fr/ftp/J/A+A/650/A201/tablea1.dat.gz`. Keep ObjType,
    so brown dwarfs and exoplanets are labelled. A few KB.
12. **GCNS 100 pc.** Use the VizieR ASU slim-column URL in §7.3 → 331k × 16 B = 5.3 MB. It gives
    true volume completeness to 100 pc. ESA NC.

### COULD

13. **Edenhofer 2024, parsec-scale local dust.** Stream shells from Zenodo 10658339 by `Range`
    (layout in §6.4) and resample to about 5 pc Cartesian over the 1.25 kpc sphere with
    `healpix_nest`. At 500×500×200 that is 50 MB, so ship a subregion or 10 pc (6 MB).
    CC BY 4.0.
14. **SDSS DR18 quasars** (753,228) for the "beyond" shell out to z ≈ 7; subsample.
15. **6dFGS DR3** (124k, 2 MB): southern-sky redshifts complementing SDSS.
16. **AT-HYG m10** (332k stars, 8 MB in the current 24 B format): a deeper sky.
17. **CF4 velocity field** (3×64³, 3 MB): streamlines into Laniakea. The CF4++ `npz` (168 MB) is
    for a later refinement.

### SKIP

- **2M++**: it contains 3,813 fabricated ZoA galaxies plus cloned redshifts.
- **Lallement 2019**: superseded by Vergely 2022.
- **Bayestar19**: not a cube.
- **`k3d_dust`**: GPL, unknown provenance.
- **RECONS TOP100**: stale since 2012.
- **Harvard `2mrs_v240.tgz`**: VizieR suffices.
- **Planck full-IQU 2 GB file**: noSZ carries the temperature at a fifth of the size.
- **Planck PR4**: no CMB maps located.

---

## 11. APK budget

The APK today is **46.5 MB** (VERIFIED). Payloads below are measured, raw or zlib, unless marked ~.
INFERRED: Unity's Android packaging deflates most `StreamingAssets`, so zlib is closer to the APK
cost.

| Layer | Raw | zlib / PNG | Tier |
|---|---|---|---|
| CMB 4096×2048 R8 (or 2048×1024) | 8.39 (2.10) | **4.49** PNG (1.32) | MUST |
| CMB inpainting mask 2048×1024 | 2.10 | ~0.1 ~ | MUST |
| Stars, AT-HYG HYGLike | 2.87 (replaces 2.87) | — | MUST, net 0 |
| 2MRS | 0.70 | 0.63 | MUST |
| CF4 groups | 0.61 | 0.56 | MUST |
| CF4 watershed labels (BoA + BoR) | 4.2 | ~0.08 | MUST |
| CF4 δ 64³ f16 | 0.52 | ~0.4 ~ | MUST |
| LVDB + GCs + UNGC hosts + names | ~0.2 ~ | ~0.1 | MUST |
| **MUST subtotal** | **~17 raw** | **~6.5** | APK ≈ **53–60 MB** |
| Vergely dust (20 pc / 40 pc step) | 10.29 / 1.25 | 6.22 / 0.93 | SHOULD |
| Reid, Skowron, Baumgardt, masers | ~0.05 | — | SHOULD |
| pscomppars compact (6,360) | ~0.4 ~ | ~0.25 ~ | SHOULD |
| SDSS 200k | 3.2 | ~2.9 ~ | SHOULD |
| Reylé 10 pc | ~0.02 | — | SHOULD |
| GCNS 331k | 5.3 | ~4.8 ~ | SHOULD |
| **MUST + SHOULD** | **~36** | **~21** | APK ≈ **68–82 MB** |
| COULD (Edenhofer 10 pc, QSOs, 6dFGS, AT-HYG m10) | ~6 + 12 + 2 + 8 | | APK ≈ 100–110 MB, still under 150 |

**Render budget is the real limit, not bytes.** For example, 783k SDSS points plus 331k GCNS
stars is roughly 1.1M sprites. Roadmap Phase 1 found that 9,000 stars cost 37k vertices. The
data design needs LOD by magnitude and density before the full sets are worth shipping (INFERRED).
The sibling `2026-09-11-architecture-map.md` §5.6 makes the same point: new populations must not
be `CelestialBodyView`s.

---

## 12. Corrections to the Milky Way Atlas and to CosmoSynth's own docs

1. **The Atlas's dust came from a GPL-3.0 repo's HTML blob** (`sb2580/k3d_dust`), and the Atlas
   records it as "Leike & Enßlin / Lallement". Replace it with a primary cube: Vergely 2022 (CDS)
   or Leike/Edenhofer (Zenodo, CC BY 4.0). The sibling Atlas report agrees.
2. **"2MASS Redshift Survey … isn't mirror-accessible"** (Atlas README, Honest limits) is false
   from triton: VizieR `J/ApJS/199/26/table3.dat.gz` (3.3 MB) and the Harvard tgz (39.5 MB) both
   answer 200.
3. **"911 galaxies … out to ~17 Mpc":** the current LVDB `dwarf_all` reaches 12.76 Mpc (911 rows,
   all with a DM). The M31/M33 rows the Atlas hand-adds are missing from LVDB by design (hosts),
   and UNGC has them with citable distances.
4. **The Atlas pulls HYG v4.1 from an archived GitHub repo.** CosmoSynth uses v4.4 from Codeberg,
   which is correct. Both should consider AT-HYG HYGLike (§7.2).
5. **`docs/data-sources.md` line 177, `DATA-GAPS.md` and `docs/research/2026-09-11-architecture-map.md`
   line 403 give Gaia as CC BY-SA 3.0 IGO.** The current ESA terms say **CC BY-NC 3.0 IGO**, with
   commercial use by request (VERIFIED §2). The same applies to Planck and Hipparcos. Fix these
   before any store listing.
6. **`DATA-GAPS.md` says "Stellarium's data is GPL".** The modern sky culture's
   `description.md` says "Text and data: CC BY-SA 4.0" (VERIFIED).
7. **`exoplanets.json` claims "confirmed planets within 40.0 pc"** but holds 60 of 471 systems,
   because of `--max-systems 60`. The claim is misleading; either lift the cap or state it in the
   `source` string.
8. **The sibling Atlas report marks the Vergely 2022 licence as "not checked".** Per the CDS page
   it falls under the A&A rule, "free for a scientific usage" (§2). Its advice not to download the
   15.7 GB Edenhofer Cartesian file stands, but the HEALPix product can be range-streamed shell by
   shell (§6.4).

---

## 13. Open questions and risks

- **Commercial or not?** This decides the Planck and Gaia (ESA NC) and AAS/VizieR (NC) sources.
  WMAP, SDSS, LVDB, the Zenodo dust maps and NASA are safe either way.
- **HYG and AT-HYG share-alike versus ESA non-commercial terms upstream:** a lawyer question,
  already flagged in `data-sources.md`, and sharper now.
- **The CF4 64³ grid geometry** was verified by fit, and the paper's text disagrees with it.
  Confirm with a rendered SGX–SGY slice before trusting voxel positions below ~30 Mpc.
- **The watershed label order past label 2** (Hercules = 3, Lepus = 4 …) follows the paper's
  statement that labels follow Table A.1's row order. Only labels 1 and 2 were checked against
  attractor coordinates. Check the others the same way (`laniakea.py`'s `at()`).
- **PR4 CMB maps:** not located. PR3 is the default.
- **The Planck units trap generalises: do not trust `TUNIT`.** Every bake should assert a physical
  range (Appendix B does), in the spirit of `CLAUDE.md`'s "an artefact on disk is not evidence".
- **Oort cloud and termination-shock numbers** are INFERRED here. Verify them against Dones+2015
  and Stone+2005/2008 before they go into Core.
- **The dataset source hosts vary in speed and reliability:** IRSA 0.67 MB/s, LAMBDA 0.16 MB/s,
  EDD's (`edd.ifa.hawaii.edu`) TLS chain fails verification from triton (curl exit 60; works with
  `-k`). The PLA and CDS are fast and reliable.

---

## 14. Reproducibility and scratch

- **Scratch on triton** (ephemeral; delete at will): `/tmp/cs-research/`
  - `healpix_nest.py`, `cmb_bake.py`, `check_frame.py`, `laniakea.py`, `lss_sizes.py`,
    `frames_check.py`, `dust50.py`, `maskfrac.py`, `ziplist.py`;
  - `smica.f16` / `smica.json` (the verified 4096×2048 µK bake), `wmap.f16` / `wmap.json`, and
    their `_small.png` previews.

  Large intermediates (downloaded cubes, the HYGLike CSV) were deleted after measuring. The two
  essential scripts are reproduced in full below, so this report stands alone.

---

## Appendix A: `healpix_nest.py` (VERIFIED by its own self-check on triton)

```python
"""Pure-numpy HEALPix NESTED ang2pix / pix2ang (Gorski et al. 2005), no healpy.

Ported from the HEALPix C++ healpix_base.cc loc2pix / pix2loc (NEST branch).
Only what a sky-texture bake needs. Run this file to self-check.
"""
import numpy as np

_JRLL = np.array([2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4])
_JPLL = np.array([1, 3, 5, 7, 0, 2, 4, 6, 1, 3, 5, 7])


def _spread(v):                      # interleave: bit i of v -> bit 2i
    v = v.astype(np.int64)
    out = np.zeros_like(v)
    for i in range(30):
        out |= ((v >> i) & 1) << (2 * i)
    return out


def _compress(v):                    # inverse of _spread on the even bits
    out = np.zeros_like(v)
    for i in range(30):
        out |= ((v >> (2 * i)) & 1) << i
    return out


def ang2pix_nest(nside, z, phi):
    """z = cos(colatitude), phi = longitude (rad). Arrays in, int64 pixel indices out."""
    z = np.asarray(z, float)
    za = np.abs(z)
    tt = np.mod(np.asarray(phi, float) / (np.pi / 2), 4.0)
    eq = za <= 2.0 / 3.0
    # equatorial belt
    t1 = nside * (0.5 + tt)
    t2 = nside * z * 0.75
    jp = np.floor(t1 - t2).astype(np.int64)
    jm = np.floor(t1 + t2).astype(np.int64)
    ifp, ifm = jp // nside, jm // nside
    face_e = np.where(ifp == ifm, ifp | 4, np.where(ifp < ifm, ifp, ifm + 8))
    ix_e = jm & (nside - 1)
    iy_e = nside - (jp & (nside - 1)) - 1
    # polar caps
    ntt = np.minimum(np.floor(tt).astype(np.int64), 3)
    tp = tt - ntt
    tmp = nside * np.sqrt(3.0 * (1.0 - za))
    jp2 = np.minimum(np.floor(tp * tmp).astype(np.int64), nside - 1)
    jm2 = np.minimum(np.floor((1.0 - tp) * tmp).astype(np.int64), nside - 1)
    north = z > 0
    face_p = np.where(north, ntt, ntt + 8)
    ix_p = np.where(north, nside - jm2 - 1, jp2)
    iy_p = np.where(north, nside - jp2 - 1, jm2)
    face = np.where(eq, face_e, face_p)
    ix = np.where(eq, ix_e, ix_p)
    iy = np.where(eq, iy_e, iy_p)
    return face * nside * nside + _spread(ix) + (_spread(iy) << 1)


def pix2ang_nest(nside, pix):
    """Pixel centres: returns (z, phi)."""
    pix = np.asarray(pix, np.int64)
    npface = nside * nside
    face = pix // npface
    ipf = pix % npface
    ix, iy = _compress(ipf), _compress(ipf >> 1)
    jr = _JRLL[face] * nside - ix - iy - 1
    fact2 = 4.0 / (12 * npface)
    nr = np.where(jr < nside, jr, np.where(jr > 3 * nside, 4 * nside - jr, nside))
    z = np.where(jr < nside, 1 - nr * nr * fact2,
        np.where(jr > 3 * nside, nr * nr * fact2 - 1, (2 * nside - jr) * 2 * nside * fact2))
    kshift = np.where((jr >= nside) & (jr <= 3 * nside), (jr - nside) & 1, 0)
    jp = (_JPLL[face] * nr + ix - iy + 1 + kshift) // 2
    jp = np.where(jp > 4 * nside, jp - 4 * nside, jp)
    jp = np.where(jp < 1, jp + 4 * nside, jp)
    phi = (jp - (kshift + 1) * 0.5) * (np.pi / 2 / nr)
    return z, phi


if __name__ == "__main__":
    # 1. the 12 base pixels sit where the HEALPix paper puts them
    z, phi = pix2ang_nest(1, np.arange(12))
    assert np.allclose(z, [2/3] * 4 + [0] * 4 + [-2/3] * 4), z
    assert np.allclose(phi[:4], np.pi / 4 + np.arange(4) * np.pi / 2)
    assert np.allclose(phi[4:8], np.arange(4) * np.pi / 2)
    # 2. round trip through every pixel centre at small nside, random ones at Planck's
    for ns in (1, 2, 4, 64, 1024, 2048):
        p = np.arange(12 * ns * ns) if ns <= 64 else np.random.default_rng(1).integers(0, 12 * ns * ns, 200000)
        assert np.array_equal(ang2pix_nest(ns, *pix2ang_nest(ns, p)), p), ns
    # 3. equal area: uniform random directions fill nside=8 pixels evenly (768 px)
    rng = np.random.default_rng(2)
    n = 3_000_000
    cnt = np.bincount(ang2pix_nest(8, rng.uniform(-1, 1, n), rng.uniform(0, 2 * np.pi, n)), minlength=768)
    exp = n / 768
    assert np.abs(cnt - exp).max() < 6 * np.sqrt(exp), (cnt.min(), cnt.max(), exp)
    # 4. nested hierarchy: a parent's 4 children are 4p..4p+3
    zc, pc = pix2ang_nest(64, np.arange(4 * 1000, 4 * 1000 + 4))
    assert np.all(ang2pix_nest(32, zc, pc) == 1000)
    print("healpix_nest self-check OK")
```

## Appendix B: `cmb_bake.py` (VERIFIED runs in §3.3)

```python
"""Stream a HEALPix-NESTED FITS map over HTTP, degrade it, and bake an equirectangular sky texture.

No healpy, no astropy, no temp file: the FITS BINTABLE is read row-chunk by row-chunk from the
socket, and NESTED ordering makes degrading a reshape + mean (a parent's children are 4^k
consecutive pixels).

  python3 cmb_bake.py URL COLUMN SCALE_TO_uK NSIDE_OUT WIDTH OUTPREFIX
  e.g. ... wmap_ilc_9yr_v5.fits TEMPERATURE 1000 512 2048 /tmp/cs-research/wmap
       ... COM_CMB_IQU-smica-nosz_2048_R3.00_full.fits I_STOKES_INP 1 1024 4096 /tmp/cs-research/smica
       (SCALE 1, not 1e6: that column is in uK although its TUNIT says K_CMB)

Writes OUTPREFIX.f16 (float16 uK, H x W, row 0 = dec +90, column 0 = RA 0, RA increasing with
column), OUTPREFIX.json (sidecar) and OUTPREFIX.png (preview, +-400 uK diverging ramp).
"""
import json, sys, time, urllib.request
import numpy as np
from healpix_nest import ang2pix_nest

# ICRS (J2000) -> Galactic; rows are the Galactic x, y, z axes expressed in ICRS (same matrix
# as the Milky Way Atlas extract_data.py).
EQ2GAL = np.array([[-0.0548755604, -0.8734370902, -0.4838350155],
                   [+0.4941094279, -0.4448296300, +0.7469822445],
                   [-0.8676661490, -0.1980763734, +0.4559837762]])
TFORM_BYTES = {"E": 4, "D": 8, "J": 4, "I": 2, "B": 1, "K": 8}


def read_header(f):
    cards = {}
    while True:
        block = f.read(2880)
        if len(block) < 2880:
            raise SystemExit("stream ended inside a FITS header")
        for i in range(0, 2880, 80):
            c = block[i:i + 80].decode("ascii", "replace")
            key = c[:8].strip()
            if key == "END":
                return cards
            if c[8:10] == "= ":
                v = c[10:].split("/")[0].strip().strip("'").strip()
                cards[key] = v


def main(url, column, scale, nside_out, width, out):
    t0 = time.time()
    f = urllib.request.urlopen(url, timeout=120)
    prim = read_header(f)
    if int(prim.get("NAXIS", "0")) != 0:
        raise SystemExit("primary HDU has data; not a HEALPix BINTABLE file")
    h = read_header(f)
    assert h["XTENSION"] == "BINTABLE" and h["ORDERING"].startswith("NEST"), h
    nside_in, rowlen, nrows = int(h["NSIDE"]), int(h["NAXIS1"]), int(h["NAXIS2"])
    assert nrows == 12 * nside_in ** 2, "cut-sky / multi-element-per-row maps not handled"
    off = 0
    for k in range(1, int(h["TFIELDS"]) + 1):
        form = h[f"TFORM{k}"].strip().lstrip("1")
        if h[f"TTYPE{k}"] == column:
            assert form == "E", form
            break
        off += TFORM_BYTES[form]
    else:
        raise SystemExit(f"no column {column}")
    kids = (nside_in // nside_out) ** 2
    npix_out = 12 * nside_out ** 2
    out_map = np.empty(npix_out, np.float32)
    per = max(1, (1 << 20) // kids) * kids            # rows per chunk, multiple of kids
    done = 0
    rowdt = np.dtype({"names": ["v"], "formats": [">f4"], "offsets": [off], "itemsize": rowlen})
    while done < nrows:
        n = min(per, nrows - done)
        buf = f.read(n * rowlen)
        while len(buf) < n * rowlen:                  # urllib may return short reads
            more = f.read(n * rowlen - len(buf))
            if not more:
                raise SystemExit(f"stream ended at row {done}")
            buf += more
        v = np.frombuffer(buf, rowdt)["v"].astype(np.float32)
        v[v < -1e30] = np.nan                         # HEALPix BAD_DATA sentinel
        out_map[done // kids:(done + n) // kids] = np.nanmean(v.reshape(-1, kids), axis=1) * scale
        done += n
    t1 = time.time()
    # CMB anisotropy rms is ~100 uK at these resolutions. Refuse anything else: TUNIT is not to be
    # trusted (smica-nosz R3.00 I_STOKES_INP says K_CMB and is in uK).
    rms = float(np.nanstd(out_map))
    if not 30 < rms < 300:
        raise SystemExit(f"rms {rms:.3g} uK is not a CMB map: wrong column, or SCALE_TO_uK is wrong")
    # equirectangular in ICRS: texel centres, RA = 0 at column 0 increasing with column
    H = width // 2
    ra = (np.arange(width) + 0.5) / width * 2 * np.pi
    dec = np.pi / 2 - (np.arange(H) + 0.5) / H * np.pi
    RA, DEC = np.meshgrid(ra, dec)
    eq = np.stack([np.cos(DEC) * np.cos(RA), np.cos(DEC) * np.sin(RA), np.sin(DEC)], -1)
    g = eq @ EQ2GAL.T
    tex = out_map[ang2pix_nest(nside_out, g[..., 2], np.arctan2(g[..., 1], g[..., 0]))]
    t2 = time.time()
    tex.astype(np.float16).tofile(out + ".f16")
    finite = np.isfinite(out_map)
    stats = {"source": url, "column": column, "unit": "uK_CMB (dipole and monopole removed)",
             "nside_in": nside_in, "nside_out": nside_out, "width": width, "height": H,
             "frame": "ICRS equirectangular, row 0 = dec +90, column 0 = RA 0, RA increases with column",
             "bad_pixels_after_degrade": int((~finite).sum()),
             "min_uK": float(np.nanmin(out_map)), "max_uK": float(np.nanmax(out_map)),
             "rms_uK": float(np.nanstd(out_map)), "p01_uK": float(np.nanpercentile(out_map, 1)),
             "p99_uK": float(np.nanpercentile(out_map, 99)),
             "seconds_stream": round(t1 - t0, 1), "seconds_project": round(t2 - t1, 1)}
    json.dump(stats, open(out + ".json", "w"), indent=1)
    try:
        from PIL import Image
        x = np.clip(np.nan_to_num(tex) / 400.0, -1, 1)
        rgb = np.stack([np.clip(1 + x, 0, 1) * np.where(x > 0, 1, 1 + x),
                        1 - np.abs(x), np.clip(1 - x, 0, 1) * np.where(x < 0, 1, 1 - x)], -1)
        Image.fromarray((rgb * 255).astype(np.uint8)).save(out + ".png")
    except ImportError:
        pass
    print(json.dumps(stats))


if __name__ == "__main__":
    a = sys.argv[1:]
    main(a[0], a[1], float(a[2]), int(a[3]), int(a[4]), a[5])
```

**Note on the projection.** It samples the nearest Nside-1024 pixel (3.4′) per 5.3′ texel, which
is adequate. For the 2048×1024 texture, degrade to Nside 512 first; otherwise nearest-pixel
sampling aliases. The projection peaks at 2.4 GB of RAM at 4096×2048 because it builds full
float64 direction arrays. Loop over row blocks to drop it to under 0.5 GB.

## Appendix C: extracting one member of the Laniakea basin zip (`laniakea.py`, VERIFIED)

The script:

1. HEADs the zip and range-reads the last 64 KB to find the end-of-central-directory record.
2. Range-reads the central directory and finds `CF4_new_128-z008_BoA.fits`: method 0 (stored),
   16.78 MB.
3. Range-reads its local header, then exactly its bytes.
4. Parses the FITS header (BITPIX −64, 128³) and reshapes the data to `(128, 128, 128)` big-endian
   float64.
5. Rounds to labels and indexes `lab[iz, iy, ix]` with i = ⌊(SG + 500) / 7.8125⌋.

Its assertions to keep:

| Probe | Expected |
|---|---|
| Label at SG[−62, −8, 39] | 1 |
| Label at SG[−125, −23, −31] | 2 |
| Label at the box centre | 1 |
| Voxel count of label 1 × 7.8125³ | ≈ 1.9×10⁶ |

Runtime: 1.5 s.

## Appendix D: URLs checked from triton on 2026-09-11 (final status | bytes | Last-Modified)

```
PLA  product-action?MAP.MAP_ID=COM_CMB_IQU-smica-nosz_2048_R3.00_full.fits      200 | 402,661,440 (streamed) | ~33 MB/s, ignores Range
PLA  product-action?MAP.MAP_ID=COM_CMB_IQU-smica_2048_R3.00_full.fits           200
PLA  MAP_ID=COM_CMB_IQU-{smica,sevem,commander}_2048_R4.00_full.fits            "No results found"
IRSA …/release_3/…/cmb/COM_CMB_IQU-smica_2048_R3.00_full.fits                   200 | 2,013,312,960 | 2018-06-04
IRSA …/release_3/…/cmb/COM_CMB_IQU-commander_2048_R3.00_full.fits               200 | 1,610,660,160 | 2018-06-04
IRSA …/release_3/…/cmb/COM_CMB_IQU-smica-nosz_2048_R3.00_full.fits              200 | 402,661,440   | 2018-06-14 | 0.67 MB/s
IRSA …/release_2/…/cmb/COM_CMB_IQU-smica_1024_R2.02_full.fits                   200 | 176,209,920   | 2015-07-17
IRSA …/release_2/…/cmb/COM_CMB_IQU-commander_1024_R2.02_full.fits               200 | 176,209,920   | 2015-07-17
IRSA …/release_2/…/cmb/COM_CMB_IQU-smica-field-Int_2048_R2.01_full.fits         200 | 251,691,840   | 2015-05-06
IRSA …/release_3/ancillary-data/masks/COM_Mask_CMB-common-Mask-Int_2048_R3.00.fits 200 | 201,335,040
IRSA …/data/Planck/release_4/                                                   404
NERSC portal.nersc.gov/project/cmb/planck2020/                                  200 (frequency maps, misc masks, likelihoods, chains)
LAMBDA data/map/dr5/dfp/ilc/wmap_ilc_9yr_v5.fits                                200 | 25,174,080    | 2012-12-11 | 0.16 MB/s
CDS  ftp/J/ApJS/199/26/table3.dat.gz (2MRS)                                     3.3M listing
CDS  ftp/J/ApJ/944/94/table{2,3,4}.dat.gz (CF4)                                 2.4M / 1.6M / 2.5M
CDS  ftp/VII/259/6dfgs.dat.gz                                                   5.0M
CDS  ftp/J/MNRAS/416/2840/catalog.dat.gz (2M++)                                 2.3M
CDS  ftp/VII/110A/table{3,4,5,6}.dat.gz (Abell)                                 84K/58K/51K/13K
CDS  ftp/J/ApJ/885/131/table1.dat (Reid masers)                                 24K
CDS  ftp/J/AJ/145/101/table1.dat (UNGC)                                         106K
CDS  ftp/J/A+A/649/A6/table1c.dat.gz (GCNS)                                     72M
CDS  ftp/J/A+A/650/A201/tablea1.dat.gz (10 pc)                                  77K
CDS  ftp/I/311/hip2.dat.gz (Hipparcos 2007)                                     8.6M
CDS  ftp/J/MNRAS/505/5978/tablea1.dat (Vasiliev GCs)                            19K
CDS  ftp/VII/202/catalog (Harris 1997)                                          35K
CDS  ftp/J/A+A/664/A174/fits/explore_cube_density_values_{010,025,050}pc_v2.fits 222M/112M/40M | ~1.3 MB/s
CDS  ftp/J/A+A/625/A135/ (Lallement 2019)                                       772M h5.gz / 1.5G fits.gz
CDS  ftp/J/A+A/636/A17/                                                         404
Zenodo api/records/3993082 (Leike 2020)                                         mean_std.h5 2.37 GB, samples.h5 14.2 GB, CC BY 4.0
Zenodo api/records/10658339 (Edenhofer v1.0.2)                                  mean_and_std_healpix.fits 3,252,715,200 (Range OK), CC BY 4.0
IP2I projets.ip2i.in2p3.fr//cosmicflows/CF4_new_128-z008_watersheds-fits.zip    200 | 67,127,665 | 2023-06-28 (Range OK)
IP2I …/CF4_new_64-z008_delta.fits | _velocity.fits | CF4gp_new_64-z008_delta.fits 1,054,080 | 3,150,720 | 1,054,080
IP2I …/CF4pp_mean_std_grids.npz                                                 167,773,716 | 2025-05-05
EDD  edd.ifa.hawaii.edu                                                         TLS verify fails (curl 60); 200 with -k
Harvard tdc-www.harvard.edu/2mrs/2mrs_v240.tgz                                  200 | 39,501,956 | 2012-01-03
6dFGS www-wfau.roe.ac.uk/6dFGS/                                                 200
CADC …/PANDAS/NearbyGals/NearbyGalaxies_Jan2021_PUBLIC.fits                     200 | 60,480   (old NearbyGalaxies.dat paths 404)
SAO  www.sao.ru/lv/lvgdb/                                                       200
GitHub apace7/local_volume_database (CC0, pushed 2026-08-30, release v1.1.1)    data/*.csv listed
GitHub jskowron/galactic_cepheids data/Data_Table_1.dat                         575,072
GitHub sb2580/k3d_dust                                                          GPL-3.0
raw.githubusercontent astronexus/HYG-Database …/hygdata_v41.csv                 200 | 33,932,548 (repo archived)
Codeberg astronexus/hyg …/CURRENT/hyg_v44.csv.gz                                200 | 13,636,362 | 2026-07-12
Codeberg astronexus/athyg …/data/athyg_40.csv.gz                                200 | 199,688,001
Codeberg astronexus/athyg …/subsets/hyglike_from_athyg_v40.csv.gz               14,096,722
McMaster physics.mcmaster.ca/~harris/mwgc.dat                                   200 | 49,364 | 2018-09-18
UQ   people.smp.uq.edu.au/HolgerBaumgardt/globular/orbits_table.txt             200 | 43,346 | 2024-10-16
RECONS www.recons.org/TOP100.posted.htm                                         200 | 44,002 | 2012-01-02
Gaia gea.esac.esa.int/tap-server/tap/sync                                       works (Barnard, Proxima returned)
NEA  exoplanetarchive.ipac.caltech.edu/TAP/sync                                 works (counts §7.5)
SDSS skyserver.sdss.org/dr18/SkyServerWS/SearchTools/SqlSearch                  works (counts §4.1)
Sesame cds.unistra.fr/cgi-bin/nph-sesame                                        works (clusters §4.3)
Stellarium skycultures/modern/description.md                                    "Text and data: CC BY-SA 4.0"
arXiv 1807.06205 (Planck 2018 I), 1910.03357 (Reid 2019), 2211.16390 (Courtois 2023), 2305.02339 (Dupuy 2023) — read via pdftotext
```
