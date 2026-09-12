# Cosmic microwave background — commercial licence clearance and bake

Family: **CMB**. Written 2026-09-12. Every licence page below was read in full on that date on
triton (fetched with `curl`/`urllib`, stripped to text, quoted from the text — not from memory and
not from any other document in this project).

Claims are marked **VERIFIED** (I read the page or ran the command and the output is quoted here)
or **INFERRED** (reasoning from verified facts).

**Bottom line:** Planck is blocked and stays blocked whichever archive it is fetched from. WMAP
9-year ILC from NASA LAMBDA is CC0, is baked, and is good enough that the difference is invisible
on a Quest 3. Staged footprint 47.7 MB of a 600 MB budget.

---

## 1. Decision table

| Source | Verdict | Evidence | Substitute |
|---|---|---|---|
| **ESA Planck Legacy Archive** (`pla.esac.esa.int`) — PR3 SMICA-noSZ and all Planck masks | **BLOCKED** | *"Data hosted in the ESA Space Science Archives are distributed under the CC BY-NC 3.0 IGO licence."* and *"Prior to any commercial use by the User of any Data or Data Product, including any use or application that directly or indirectly generates a financial gain, a detailed request for authorisation/licence shall be made by the User by sending email to data.licences@esa.int ."* — <https://www.cosmos.esa.int/web/esdc/terms-and-conditions>, read 2026-09-12. The same page's credit table lists *"Planck / All / Credit: ESA, Planck Collaboration"*, confirming Planck is in scope. **VERIFIED** | WMAP 9-year ILC |
| **NASA/IPAC IRSA Planck copy** (`irsa.ipac.caltech.edu/data/Planck/release_3/`) | **BLOCKED** | IRSA's own README: *"The IRSA Planck Public Data Release 3 (PR3) is the U.S. release of the mid-2018 ESA PR3 Planck data products."* and *"Planck is an ESA mission with significant NASA involvement."* — read 2026-09-12. The README states no licence at all. NASA's CC0 default is scoped to *"a NASA-led mission"*, which Planck is not. **VERIFIED** | WMAP 9-year ILC |
| **LAMBDA Planck pages** (`lambda.gsfc.nasa.gov/product/planck/`) | **BLOCKED — and there is nothing there to take** | *"Products are released by ESA through the archive at ESA Planck Legacy Archive ( https://pla.esac.esa.int ) and by NASA through the NASA/IPAC Infrared Science Archive"* … *"The links in the tables below lead directly to Planck resources at ESA."* — read 2026-09-12. LAMBDA hosts **no** Planck map data; its Planck section is a link list. **VERIFIED** | — |
| **NASA LAMBDA — WMAP 9-year (DR5) ILC map** | **OK — CC0** | *"Unless the data file is marked with a restrictive notice or license, data that is provided from a NASA-led mission including observations, engineering, calibration, and auxiliary data are licensed as Creative Commons Zero . There are no restrictions on the usage of these data."* — <https://science.data.nasa.gov/about/license>, read 2026-09-12. WMAP is NASA-led; both FITS headers were dumped in full and carry no COPYRIGHT/LICENSE/restriction card. **VERIFIED** | — (this is the chosen primary) |
| **NASA LAMBDA — WMAP KQ85 temperature analysis mask** | **OK — CC0** | Same page, same clause, same archive, same mission. Header dumped, no restriction card. **VERIFIED** | — |
| **LAMBDA site acknowledgement** | **OK — request, not a condition** | *"There is no cost involved in the use of LAMBDA; however, if your research benefits from its use then we request that you include the following acknowledgement in your publications"* — <https://lambda.gsfc.nasa.gov/contact/>, read 2026-09-12. A request tied to publications, no fee, no use restriction. **VERIFIED** | — |
| **CMB constants** (T₀, dipole) | **OK — not copyrightable** | Physical measurements read out of running prose and retyped; no table layout or selection copied. Source PDFs fetched to `/tmp`, read, and deleted — not redistributed. **VERIFIED** | — |

### The share-alike question, answered

It does not arise. CC BY-NC 3.0 IGO is **non-commercial, not share-alike** — there is no
"publish your derivative and you may ship" escape hatch. The only route to Planck is a written
authorisation from `data.licences@esa.int`. Until such a licence exists in writing, **no Planck
map, mask, or texture derived from one may ship**, and the existing validated SMICA bake
(recorded in `docs/research/scripts/smica.json`) must be treated as unshippable.

### Correction to earlier design docs

Earlier CosmoSynth research baked and validated the Planck PR3 SMICA-noSZ map. That work is sound
engineering and is now **licence-dead for a commercial release**. Any design document that names
Planck as the CMB source is wrong and should say WMAP. The app's narration must not say "this is
the Planck map", because it is not.

### Mirroring does not launder a licence

This generalises past the CMB family: **an archive that mirrors somebody else's data cannot grant
more than the originator did.** Check the originating mission's lead agency, not the hostname the
file came from. IRSA, NERSC and third-party repackagings of Planck are all equally blocked.
**INFERRED**, from the three verified quotes above.

---

## 2. What the substitution costs

| | Planck PR3 SMICA-noSZ (blocked) | WMAP 9-year ILC (shipped) |
|---|---|---|
| native HEALPix | NSIDE 2048, 1.72′ pixels | NSIDE 512, 6.87′ pixels |
| effective resolution | ~5′ beam | ~1°, reliable above ~10° |
| sky rms, as baked | 107.73 µK (at NSIDE 1024) | **71.06 µK** |
| range, as baked | −1034.01 … +653.94 µK | **−452.46 … +442.23 µK** |
| frequencies combined | 9 (30–857 GHz) | 5 (23–94 GHz) |
| epoch | 2009–2013, PR3 (2018 reprocessing) | 2001–2010, DR5 (2012) |

The honest version: **Planck resolves about twelve times finer angular detail**, and essentially
all of the rms and range difference above is that extra small-scale power, not a different sky.

**Why it does not matter here (INFERRED):** the CMB is an inside-out sphere on a Quest 3, whose
display gives roughly 20 pixels per degree at lens centre. A 1° map is already at the headset's
resolving limit on that sphere. The 4096×2048 bake is 5.27′ per texel, so WMAP is *oversampled* by
the texture; Planck would be oversampled by ~144× in area for no visible gain.

**Where it would matter, so the app must not do it:** any "zoom into a patch of the CMB"
interaction, any acoustic-peak or power-spectrum overlay claiming Planck precision, or narration
naming Planck.

---

## 3. Staged inventory

`~/projects/CosmoSynth/data-staging/` — **47,685,015 bytes total for this family** (600 MB budget).
Raw FITS deleted after baking, per the disk rule; `df -h /` showed 16 G free before the fetch
(above the 8 G floor) and 15 G after.

### `wmap9-ilc-lambda/` — 46 MB, the cleared primary

| file | bytes | sha256 |
|---|---|---|
| `cmb_equirect_4096x2048_uint16.png` | 8,677,677 | `e25740aec25b6c732281d5ce564cd6b5326658fb62e7a3e3a5b160a1563ee5dc` |
| `cmb_equirect_4096x2048.f16` | 16,777,216 | `d1c0db5cbd444601323d97171e071a300fbb5d7ee5b996380393f25dad0cd4bd` |
| `cmb_cube_1024.f16` | 12,582,912 | `30b7a6e9c6ab1392be76d4ba9527dc4a7eaf89c3e2212247bb587fb50c2dbbe8` |
| `cmb_cube_posx_1024_uint16.png` | 1,375,182 | `4239280bf58ff04352d730c4f727072617344bf60be04411400ce943f3e7f4e7` |
| `cmb_cube_negx_1024_uint16.png` | 1,374,831 | `ede77c68ffc661c6355275647114d1054ec4d313bba3b993ab736dd457697208` |
| `cmb_cube_posy_1024_uint16.png` | 1,369,229 | `3382aabc66b2416b3da3d4033ebc840adda62e9a7ab9177e69d66b2165337681` |
| `cmb_cube_negy_1024_uint16.png` | 1,376,652 | `99904b0283f03791163b27e5d1cb9b431c85e15c5a69797ae8d326efe95e8b5d` |
| `cmb_cube_posz_1024_uint16.png` | 1,361,447 | `a8efad93654190a88ff0a7ba4837c43bfb56fda265b5bd3861b209bc56e8c309` |
| `cmb_cube_negz_1024_uint16.png` | 1,362,180 | `0494597b348aa475d9be8977341515c835d9a456659ac504fc1f226651161d5c` |
| `mask_kq85_equirect_2048x1024.png` | 41,581 | `ad306a5fb6294bec394c310f100fc2c6959ea88dc06467fbe729622f3430b4bb` |
| `preview_equirect_1400x700.png` | 1,342,017 | `463cc1d18caf3fae9f6af18f14223c280f21517f237272cd9f75950bd4c78886` |
| `bake_checks.json` | 1,793 | `3a1680683c1f1c464a2ba65cf0f9d67fdb544af1c29f0463158a8595cdf1266d` |
| `cmb_equirect_bake_stats.json` | 516 | `560c4c820191fb91d61d711f110ac6a7ae77e2c56d6637522fd5df6fc362b91f` |
| `cmb_cube.py` | 10,666 | `9167f4e203ce61d158a0bc52f09474c8363f3d4fd277a1e1ffb6735a5bb29076` |

Counts: source 3,145,728 HEALPix NESTED pixels (NSIDE 512, no degrade — NSIDE_out = NSIDE_in);
equirect 8,388,608 texels at 5.27′; cube 6 × 1,048,576 texels at 5.27′ at face centre; mask
2,097,152 texels, kept fraction 0.7480.

Source files, fetched 2026-09-12T16:11Z and then deleted:
`wmap_ilc_9yr_v5.fits`, 25,174,080 B, sha256 `4501090f824f9140e916339f9e8e490333f6ffe4257a6ee6a02526c8640099a9`;
`wmap_temperature_kq85_analysis_mask_r9_9yr_v5.fits`, 25,174,080 B, sha256 `30608930b5a3a2154ff0e4d95b32dd3dcb8fda2151133153a1d59feb97ab8d63`.

### `cmb-constants/` — 20 KB
`cmb_constants.json`, 1,186 B, sha256 `36d2e1005d57883a91101baab91107807496464b2c119320ca747c0beb427d38`.

### `planck-pr3-smica-esa-REJECTED/`, `planck-pr3-irsa-REJECTED/` — 16 KB each
`SOURCE.txt` + `LICENCE.txt` + `NOTES.md` only, no data. They exist so nobody re-checks Planck.

---

## 4. The bake, and the checks that prove it

Baked with the project's existing validated pure-numpy code, unmodified:
`docs/research/scripts/healpix_nest.py` + `cmb_bake.py`. The cube map and the verification are in
`cmb_cube.py`, staged beside the data and rerunnable.

```
cd ~/projects/CosmoSynth/docs/research/scripts
python3 cmb_bake.py file:///tmp/cmb-work/ilc.fits TEMPERATURE 1000 512 4096 /tmp/cmb-work/wmap_ilc_9yr
python3 cmb_cube.py /tmp/cmb-work/ilc.fits /tmp/cmb-work/wmap_ilc_9yr.f16 /tmp/cmb-work/out
```

### Pixel scale
Source NSIDE 512 → 6.87′ nominal pixel, smoothed to a 1° beam. Equirect 4096×2048 → **5.27′ per
texel**; cube 6×1024² → **5.27′ per texel at face centre**. Both oversample the 1° map, which is
the point: no aliasing, and the texture is not the limiting factor.

### The unit trap — resolved, and in the *opposite* direction to Planck
`TUNIT1 = 'mK, thermodynamic'`. **Here the header is telling the truth**, and that was established
by measurement, not trust: the raw column's standard deviation is **0.0711**. CMB anisotropy rms at
1° is order 70 µK, so only `mK` is physical (`K` would be 71,100 µK; `µK` would be nothing).
**SCALE_TO_µK = 1000. VERIFIED.**

The Planck file has the opposite property — `COM_CMB_IQU-smica-nosz` column `I_STOKES_INP` declares
`K_CMB` and is really µK, scale 1. Neither was taken on trust. **The rms test is the thing that
transfers, not the answer.**

Two more header lies found in passing: `N_OBS` is identically 1.0 in both files (it looks like a
hit count and is not one), and the KQ85 mask's column is also called `TEMPERATURE` with
`TUNIT='mK'` while actually holding exactly two values, 0.0 and 1.0.

### Temperature range
Whole sky **−452.46 µK to +442.23 µK**, rms **71.06 µK**, mean **+2.04 µK** (the ILC's own residual
offset; monopole and dipole are removed). Cube min/max identical to the equirect's. **VERIFIED.**

### Orientation check 1 — Galactic plane residual ✅
|T| of the finished equirect, solid-angle weighted, binned by Galactic latitude in 2° bins. The
latitude was computed **from the published NGP position (RA 192.85948, Dec +27.12825), not from the
`EQ2GAL` matrix the bake used** — so this audits the rotation rather than assuming it.

Peak bin **[0°, 2°] at 70.79 µK**, falling to 50–55 µK above |b| = 30°. Requirement was a peak at
|b| < 2°. **PASS, VERIFIED.**

As a check on the two published positions themselves, the NGP and the Galactic centre
(RA 266.40510, Dec −28.93617) come out orthogonal to **7.7 × 10⁻⁵ degrees**.

### Orientation check 2 — a known feature ✅
The **CMB Cold Spot**, Galactic (l, b) = (209, −57) = ICRS (RA 48.83, Dec −19.58). Solid-angle
weighted mean inside a 5° disc: **−105.58 µK** over 10,801 texels, against a 71 µK sky rms. It is in
the right place and it is cold. **PASS, VERIFIED.**

### Orientation check 3 — cube ↔ equirect ✅
200,000 random directions sampled through both rasters: correlation **0.99649**. A deliberately
scrambled face order gives **−0.00991** — the control proves the test can fail. **PASS, VERIFIED.**

Only 60% of samples agree within 1 µK and the p99 difference is 21 µK. That is two
nearest-neighbour rasterings of a 6.87′ map on 5.27′ grids disagreeing at pixel boundaries, not a
misorientation; correlation is the orientation test, equality is not. **INFERRED**, supported by
the scrambled control.

### Not a check — informational
A 5° disc on the Galactic centre has rms 75.22 µK against 69.63 µK at the North Galactic Pole, a
ratio of 1.08. The ILC is cleaned hard enough that this is weak. It is recorded, it is **not** a
second vote; check 1 is the decisive plane test. (I originally wrote this as a pass/fail gate at
1.5× and it failed — the gate was wrong, not the bake, and demoting it rather than loosening it is
the honest fix.)

### Encoding
Both equirect and cube ship as **lossless single-channel 16-bit greyscale PNG**:

```
µK = px / 65535 × 1000 − 500        # range −500…+500 µK, step 0.01526 µK
```

The quantisation was round-tripped through PIL and asserted at bake time: **max encode error
0.0077 µK**, four orders of magnitude below the 71 µK signal. **VERIFIED.** The exact float16 µK
arrays (`.f16`) are staged alongside as the authoritative output.

**Unity import: R16, linear, no sRGB, no compression.** A DXT/ASTC block format destroys the point
of the map.

### Cube face convention
Right-handed **ICRS** basis: `X → (RA 0, Dec 0)`, `Y → (RA 90, Dec 0)`, `Z → (Dec +90)`.
`u, v ∈ [−1, 1]`, u left→right across the face, v top→bottom down it:

```
+X ( 1, -v, -u)   -X (-1, -v,  u)
+Y ( u,  1,  v)   -Y ( u, -1, -v)
+Z ( u, -v,  1)   -Z (-u, -v, -1)
```

Standard OpenGL/DDS/KTX layout. The source map is **Galactic** on disk and was rotated to **ICRS**
during projection so it lines up with CosmoSynth's star and Milky Way layers. Unity is left-handed
Y-up, so the app still owns the ICRS→Unity basis change — **it must be the same one the star layer
uses.** Mixing Galactic and ICRS is the easiest way to ship a CMB rotated 60° off every star.

### Mask
`mask_kq85_equirect_2048x1024.png`, 8-bit, 255 = trust the CMB here, 0 = Galactic residual, same
ICRS frame. Kept fraction **0.7480** of pixels (0.7480 solid-angle weighted in the texture), which
matches the published KQ85 f_sky ≈ 0.75. **VERIFIED.** Use it to fade, hatch or label the residual
band whenever the app claims to be showing "the CMB" — 25.2% of the sky is outside it.

### Constants
```
T_cmb            2.72548 ± 0.00057 K
dipole           3.355 ± 0.008 mK toward Galactic l = 263.99 ± 0.14, b = 48.26 ± 0.03
dipole, ICRS     RA 167.9287, Dec −6.9269                      DERIVED
solar speed      369.0 km/s                                    DERIVED
```

T₀ **VERIFIED** by reading the WMAP nine-year paper's own text (`pdftotext` line 6397):
*"CMB = 2.72548 ± 0.00057 K, from Fixsen (2009)."* The dipole is **VERIFIED** from Hinshaw et al.
2009 (WMAP 5-year), line 427: *"(d, l, b) = (3.355 ± 0.008 mK, 263.99° ± 0.14°, 48.26° ± 0.03°),"* —
still the WMAP solution of record, because the nine-year paper uses the dipole for calibration and
does not restate a solution.

The derived solar speed comes out at **369.0 km/s against a published 369.0 ± 0.9** — an
independent arithmetic check on the dipole/T₀ pair that I did not tune for.

The shipped texture has monopole **and** dipole removed, so the app has three honest modes and
should label which it is in: anisotropy (as-is, ±450 µK), absolute (+2.72548 K, a uniform useless
glow but the right picture for "the whole sky is 2.7 K"), and as-seen-from-the-moving-Solar-System
(add `3355 µK × cos θ` about the dipole — ~47× the anisotropy, a smooth hot/cold gradient, and the
natural bridge into *why we are moving*). Compute 2 and 3 in the shader from decoded µK; do not
re-bake, and do not stack them at the ±500 µK PNG range.

---

## 5. Attribution strings that must appear in the app's credits

**Required (data):**

> Cosmic microwave background: WMAP 9-year Internal Linear Combination map and KQ85 temperature
> analysis mask. Credit: NASA / WMAP Science Team, via the Legacy Archive for Microwave Background
> Data Analysis (LAMBDA), a service of the Astrophysics Science Division at NASA Goddard Space
> Flight Center.

**Required (constants):**

> CMB temperature 2.72548 K (Fixsen 2009, COBE/FIRAS). CMB dipole 3.355 mK toward Galactic
> (263.99, 48.26) (Hinshaw et al. 2009, WMAP five-year).

Strictly, CC0 compels neither — the first is LAMBDA's stated request and the second is scientific
courtesy. Ship both.

**Trademark caveat, VERIFIED:** the NASA insignia, logotype and name must not be used so as to
imply NASA endorsement of CosmoSynth (NASA Brand & Usage Guidelines, linked from
<https://www.nasa.gov/nasa-web-privacy-policy-and-important-notices/>). Credit the data in text;
do not put a NASA logo on store art or imply a partnership.

---

## 6. What I could not verify

- **The PLA's own in-app terms text.** `pla.esac.esa.int` is a JavaScript application that returns
  only a loading shell to a non-browser fetch, and the environment rules forbid a browser here. I
  read the ESDC terms page that governs the ESA Space Science Archives instead, and confirmed from
  its own credit table that Planck is listed there ("Planck / All / Credit: ESA, Planck
  Collaboration"). If a Planck-specific term ever contradicted the ESDC page, I would not have seen
  it — but it would have to *grant* commercial rights to change the verdict, which is not how
  archive terms usually differ. **INFERRED.**
- **Whether ESA would in practice grant a commercial licence** for a Quest app, on what terms, or
  at what price. That requires emailing `data.licences@esa.int`, which is a decision for the owner,
  not a subagent. Unknown.
- **The HEASARC general data-rights page** (`heasarc.gsfc.nasa.gov/docs/heasarc/ofwg/docs/general/
  data_rights.html`) returned 404. Not needed — LAMBDA's own statement and NASA's science-data
  licence both cover the WMAP products directly.
- **Whether a CC-BY or CC0 copy of Planck exists anywhere.** I did not find one and did not
  exhaustively search third-party repackagings; given ESA's terms, any such copy would itself be
  unlicensed, so a "clean" mirror would be a red flag rather than a solution. **INFERRED.**
- **On-device appearance.** The bake is verified numerically and previewed as an image; it has not
  been rendered inside the Unity sphere on a Quest 3. The frame convention in §4 is the thing most
  likely to go wrong there, and it is untested in-engine.

---

## 7. Ledger rows for `docs/data-sources.md`

Written to match that file's existing prose style. Add under "Currently used":

---

### NASA LAMBDA — WMAP 9-year (DR5) <https://lambda.gsfc.nasa.gov/product/wmap/dr5/>

The cosmic microwave background. Supplies the Internal Linear Combination (ILC) temperature map
and the KQ85 temperature analysis mask, baked offline into an equirectangular texture and a cube
map under `StreamingAssets/CosmoSynth/cmb/`. Never fetched at runtime.

- **Files**: `wmap_ilc_9yr_v5.fits` and
  `wmap_temperature_kq85_analysis_mask_r9_9yr_v5.fits`, both HEALPix NESTED NSIDE 512, 25,174,080 B
  each, fetched 2026-09-12 and deleted after baking. Staging and hashes in
  `data-staging/wmap9-ilc-lambda/`.
- **Terms**: Creative Commons Zero. NASA's science-data licence page states that data from a
  NASA-led mission, absent a restrictive notice on the file, is CC0 with "no restrictions on the
  usage of these data"; both FITS headers were dumped and carry no such notice. Commercial use is
  unrestricted. LAMBDA *requests* an acknowledgement, which the credits carry.
- **Resolution**: 1° effective, NSIDE 512 (6.87′ pixels), baked at 5.27′ per texel. Degree scales
  only — LAMBDA itself cautions that small-scale structure in the bias correction is uncertain.
- **Frame**: Galactic on disk, rotated to ICRS in the bake so it agrees with the star and Milky Way
  layers. Cube faces follow the OpenGL layout in an ICRS basis.
- **Unit trap**: `TUNIT1` says `mK, thermodynamic` and here that is true (scale to µK = 1000),
  established by measuring the column rms at 71.06 µK, not by believing the header. `N_OBS` is
  identically 1 and is not a hit count. The mask's column is also called `TEMPERATURE` and holds
  only 0.0/1.0.

### CMB constants — Fixsen 2009; Hinshaw et al. 2009

T_CMB = 2.72548 ± 0.00057 K and the CMB dipole, 3.355 ± 0.008 mK toward Galactic (263.99, 48.26).
Five measured numbers, not copyrightable; read from the primary papers' running text and retyped.
The papers themselves are not redistributed. Staged as
`data-staging/cmb-constants/cmb_constants.json`. The shipped map has monopole and dipole removed;
the app adds them back in-shader for the labelled "absolute" and "as the Solar System sees it"
modes.

---

### Move to a "Rejected" section

### ESA Planck Legacy Archive — <https://pla.esac.esa.int/> — **REJECTED, commercially blocked**

Planck PR3 SMICA-noSZ was baked and validated during earlier research and **cannot ship**. ESA
Space Science Archive data is CC BY-NC 3.0 IGO, and the terms require a written authorisation from
`data.licences@esa.int` "prior to any commercial use … including any use or application that
directly or indirectly generates a financial gain". This is non-commercial, not share-alike, so
there is no publish-your-derivative workaround. The NASA-hosted copies do not help: LAMBDA hosts no
Planck maps at all (its Planck pages link to ESA), and IRSA describes its holding as "the U.S.
release of the mid-2018 ESA PR3 Planck data products" while stating no terms of its own — NASA's
CC0 default is scoped to "a NASA-led mission" and Planck is ESA-led. Evidence and verbatim quotes:
`docs/licence/cmb.md` and `data-staging/planck-pr3-*-REJECTED/`.

Any design document naming Planck as the CMB source is now wrong. The substitution costs about
12× angular resolution and is invisible on a Quest 3 at 1° — but the app must never say "Planck",
and must not offer CMB zoom or power-spectrum claims that the WMAP product cannot support.
