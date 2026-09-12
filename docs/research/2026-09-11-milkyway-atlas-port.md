# Milky Way Atlas → CosmoSynth: feature inventory and Unity porting spec

*Research report, 2026-09-11. Slug `milkyway-atlas-port`. Read-only phase: nothing under
`~/projects` was modified except this file.*

Tags: **VERIFIED** = I read the code/file or ran the check myself. **INFERRED** = reasoning or
arithmetic from verified facts, not observed on a device or in a build.

---

## 0. Summary

The "Milky Way Atlas" (`nebula-gallery`) is a single 1,550-line three.js 0.164.1 WebXR page plus
4.72 MB of data. It puts real catalogues in one Sun-centred **galactic Cartesian parsec** frame
inside one scaled group (`model.scale` = metres per parsec, log10 range −7.3…+5.7). It has 11
data layers, a Standish-element Solar System, three cylindrical grid regimes, an observed-sky
magnitude mode, a pc/ly measure tool, "spaceship" GO-TO travel, and a tabbed wrist panel with
finger-touch hysteresis.

What this means for CosmoSynth:

- **Most of the atlas's data should be re-derived, not copied.** CosmoSynth's `stars.csstars`
  (119,613 HYG v4.4 stars) is a strict superset of the atlas's `stars.bin`. The atlas's
  brightest-60k cut throws away about 75 % of stars within 20 pc: 462 left against CosmoSynth's
  1,877 (VERIFIED). The shipped `galaxies.json` and `markers.json` **cannot be reproduced** from
  the committed `extract_data.py`, which is stale. The dust cube comes out of a GPL-3.0 repo's
  HTML snapshot, in unstated "relative" units. Its better primary source is Vergely+2022 at CDS:
  nmag/pc, 25 pc correlation length, 6×6×0.8 kpc, 114,294 KiB. That source is reachable from
  triton (VERIFIED).
- **Every upstream source is reachable from triton** (HTTP 200, 2026-09-11). That includes 2MRS,
  which the atlas README says was unreachable. The two parsec-resolution dust maps are too large
  for triton's ~19 GB free disk: Edenhofer 2023 is 15.7 GB and Leike 2020 is 2.4–14 GB.
- **Do not port the atlas's Solar System.** CosmoSynth's Horizons catalogue is validated to under
  a milliarcsecond. The Standish Table 1 values in the atlas do match JPL exactly (VERIFIED), but
  they are the weaker model. Port only the decorations: belts, heliosphere shells, comet tails
  (fixed), and the Sun as an ordinary G star in the stellar layer.
- **27 behaviours are wrong or need correcting** (§5). The most important ones:
  - A scale clamp of 10^0.5 m/pc on two-hand grab and on the right stick. It snaps the view out
    of the Solar System, which sits at 10^4.55.
  - Galaxy position angle is applied in *screen space*.
  - Roughly 60 % of galaxy "measured" ellipticities are the 0.25 default.
  - The "Sun orbit R≈8.2 kpc" label sits on a 26 kpc ring.
  - Dust is composited over stars that are in front of it.
  - Absolute magnitudes are not corrected for extinction.
- **Conflicts with CosmoSynth rules:**
  - The atlas's panel colours (green = datasets, amber = markers, pink = tools) collide with
    CosmoTheme's provenance palette (green = Measured, amber = Procedural, pink = Fictional).
  - Labels are a fixed 8.5 cm tall, which breaks the angular-size rule.
  - Its 1.2 px star floor is exactly the "square patches" failure the user already reported.
  - Its thin, uniformly blue lines are the style the user rejected ("blue orbits is just not
    good", "thin lines everywhere").
  - It has no controller-binding table.
- **Quest 3 risks:**
  1. The dust raymarch. The atlas leaned on WebXR foveation 0.7, and CosmoSynth's Built-in RP
     build **cannot enable foveation** (CLAUDE.md).
  2. Single-pass-instanced compatibility of any procedural or instanced star shader, which is
     untested in this project.
  3. Label and line draw-call counts. The atlas creates ~151 label planes and 137 dot spheres,
     each with its own texture or material.

---

## 1. What was read and checked

| Item | Where | Size | Status |
|---|---|---|---|
| `index.html` (the whole app) | laptop `D:\PhD_Backup\OneDrive - Aalto University\Projects\Nebulytic\webxr-examples\Fable5\nebula-gallery\index.html` | 76,124 B, 1,550 lines, mtime 2026-09-10 | VERIFIED, read in full |
| `README.md` | same dir | 17,360 B | VERIFIED, read in full |
| `tools/extract_data.py` | same dir | 10,355 B, 157 lines | VERIFIED, read in full |
| `preview.png`, `preview_cyl_grid.png`, `preview_solar_system.png` | same dir | 514,584 / 221,147 / 1,247,049 B | VERIFIED, viewed. **All three show older UI versions** (boxed labels, "LOCAL ISM" title, a Spin toggle, the v2 HUD). None shows the v3 wrist panel or the unboxed v3 labels. |
| All 11 data files | `data/` | 4,721,004 B total | VERIFIED, parsed with a stdlib Python script (§3) |
| CosmoSynth `CLAUDE.md`, `README.md`, `docs/roadmap-universe.md`, `docs/feedback.md`, `docs/architecture.md` §Frames–§Floating origin, `docs/data-sources.md` (grep) | triton `~/projects/CosmoSynth` | — | VERIFIED |
| CosmoSynth memory notes (MEMORY.md, status, ui-was-never-designed, xr-empty-scene, small-things-need-two-bounds, runtime-built-scenes-strip-shaders, build-and-ship) | triton `~/.claude/projects/-home-triton-projects-CosmoSynth/memory/` | — | VERIFIED |
| CosmoSynth source: `StarCatalog.cs`, `build_starfield.py`, `SkyAndRings.cs` (star part), `XrInput.cs`, `WristDashboard.cs`, `CosmoTheme.cs` (palette/type), `XrHandInput.cs`, `TabletopManipulator.cs`, `ScaleSpace.cs` bands, `DepthBands.cs`, `FloatingOrigin.cs`, `BodyLabelLayer.cs` header, `Provenance.cs`, `CelestialBody.cs` BodyType, `CosmoSynthBootstrap.cs` component list | triton | — | VERIFIED (read or grepped) |
| Upstream reachability and licences | `curl -sI` and GitHub/Zenodo/CDS API reads from triton | — | VERIFIED, §9 |

Line numbers below are `index.html` lines unless another file is named.

---

## 2. Architecture of the atlas

- **Frame** (VERIFIED, `extract_data.py` L22-31, `index.html` L171-175). Galactic Cartesian,
  **parsecs**, Sun at origin, +X toward the Galactic Centre, +Y toward rotation (l = 90°), +Z
  toward the North Galactic Pole. Equatorial → galactic uses the Hipparcos matrix:
  ```
  R = [[-0.0548755604,-0.8734370902,-0.4838350155],
       [ 0.4941094279,-0.4448296300, 0.7469822445],
       [-0.8676661490,-0.1980763734, 0.4559837762]]
  lb_xyz(l,b,d) = d·(cos b cos l, cos b sin l, sin b)
  ```
  `model.rotation.x = −π/2` maps +Z_gal to room up, so **the galactic plane is horizontal. The
  Solar System is also shown in galactic orientation**, with the ecliptic tilted about 60° to the
  floor (INFERRED from the rotation chain `eclToGal`, L653-661).
- **One scaled group** (L171-175, L834-836). Children live in pc and
  `model.scale = 10^logScale` metres/pc. The slider range is [−7.3, 5.7] (L88). The camera is
  near 0.02 m, far 5e8 m (L164). There is no floating origin and no depth banding; everything
  except the planets is additive with depthWrite off, so z-fighting never shows.
- **Motion model** (L1528-1538). Everything eases toward targets:
  `k = 1 − exp(−6.5·dt)`, `model.position.lerp(tgtPos,k)`, `quaternion.slerp(tgtQuat,k)`, and
  `logScale += (targetLog − logScale)·k`, with `dt ≤ 0.05`.
- **Loading** (L133-152). 11 fetches in parallel, with a progress bar. Opening from `file://`
  fails with an explanatory message.
- **Rendering setup** (L155-161, L934-955). `antialias:true, alpha:true`, pixel ratio ≤2,
  `xr.setFoveation(0.7)`, reference space `local-floor`. Two session buttons: **VR**
  (`immersive-vr`, black clear) and **MR** (`immersive-ar`, clear alpha 0). On XR start the pose
  becomes (0, 1.25, −1.0) and dust steps are capped at 72.
- **Debug hook** (L1547). `window.__dbg` exposes `{measure, nameCache, model, solarGroup,
  solarLbl, goTo, DESTS, obs, lim, cyl, namesVisible}`, which headless checks can use.
- **External stylesheet** (L2, L56). The page links `/css/lumen-experience.css` and sets
  `class="lumen-experience"` on `<html>`. That stylesheet belongs to the hosting site and is
  **not in the folder** (VERIFIED absent from the listing).

---

## 3. Data files: layout, statistics, provenance, verdict

All statistics come from my own parse of the laptop copy (VERIFIED) unless marked otherwise.
Little-endian float32 throughout.

| File | Bytes | Layout | Rows / measured stats | Provenance and licence | Verdict |
|---|---|---|---|---|---|
| `dust.bin` + `dust_meta.json` (580 B) | 3,272,481 = 201×201×81 | uint8, **z-major (z,y,x), x fastest**. Extent x,y ∈ [−1000,1000] pc, z ∈ [−400,400] pc, voxel ≈ 10 pc. Decode: `density = vmin·(vmax/vmin)^(v/255)`, vmin = 0.02, vmax = 79.8145 | 21.3 % voxels = 0; **28.6 % above the default cut 0.22** (byte > 56); byte p50 = 28, p90 = 98, p99 = 150, max = 255. Units: "relative extinction density" (unknown physical units) | Extracted from a msgpack+zlib+base64 blob in `sb2580/k3d_dust` `index.html` (18.86 MB). **Repo licence GPL-3.0** (GitHub API). README of that repo: "based on Leike & Lallement 3d dust maps"; the exact map and version are not stated | **Re-derive.** Use **Vergely, Lallement & Cox 2022**, CDS `J/A+A/664/A174`, file `explore_cube_density_values_025pc_v2.fits`: 601×601×81, 6×6×0.8 kpc, 25 pc correlation length, **extinction density in nmag/pc at 550 nm**, same galactic axes, 114,294 KiB. Or the 10 pc 3×3×0.8 kpc cube (601×601×161, 227,168 KiB). Grid spacing is 10 pc and 5 pc respectively (dims ÷ extent, INFERRED). Keep the atlas cube only as a stopgap, labelled "Inferred, units unknown". |
| `stars.bin` | 1,200,000 = 60,000×20 | `[x,y,z (pc, galactic), mag V, B−V]` | 60,000 rows, **sorted by apparent mag** (−1.44 … **8.54**). Distance 1.32 … **990.1 pc** (the script filter is ≤1400; the magnitude cut binds first). B−V −0.40 … 5.46 (p50 0.581); 213 rows exactly 0.6 (the script's missing-ci fallback, value count VERIFIED, attribution INFERRED). Stars ≤5/10/20 pc: **16/79/462** | HYG v4.1 (`astronexus/HYG-Database`, superseded: its README says HYG moved to Codeberg; repo LICENSE = **CC BY-SA 4.0**) | **Do not copy.** CosmoSynth's `stars.csstars` (HYG v4.4, 119,613 stars; 109,389 with known distance ≤1400 pc; ≤5/10/20 pc: **61/319/1,877**; VERIFIED) supersedes it. Derive galactic coordinates at runtime with `StandardFrames.EquatorialToGalactic`. |
| `cepheids.bin` | 35,424 = 2,214×16 | `[x,y,z (pc, galactic), age (Myr)]` | 2,214 rows: the 2,431 table rows minus 217 with DIST = −1 (VERIFIED against upstream). Distance 266 pc … **121.7 kpc**; 68 >20 kpc, 16 >30 kpc, 2 >60 kpc (outliers to review). \|z\| up to 11.76 kpc. Age 3 … 1,169 Myr (p10/50/90 = 59/122/248) | Skowron et al. 2019, *Science* 365, 478 (OGLE). `jskowron/galactic_cepheids/data/Data_Table_1.dat` (575,072 B). Columns: L, B (deg), DIST, dDIST (pc), AGE (Myr), PERIOD (d), MODE, photometry. Repo LICENSE: figures CC BY-SA 3.0, code MIT, **"except scientific data" (data licence unstated)** | **Re-derive** to keep ID, dDIST (uncertainty), period and mode; the atlas drops them. Needs a licence/citation decision. |
| `globulars.bin` | 3,408 = 213×16 | `[x,y,z (pc, galactic), M_V]` | 213 rows. M_V −10.54 … +2.20; 8 rows are the **fallback −6**. Distance 1.85 … 145.9 kpc | LVDB `gc_harris.csv` + `gc_mw_new.csv` (**CC0-1.0**; please cite Pace 2025, OJAp 8, E142). M_V computed as `apparent_magnitude_v − distance_modulus`, **no extinction** | **Re-derive.** Keep names (the atlas keeps only five), use LVDB's own `M_V` column (#85, with `_em/_ep` errors) and `rhalf`. |
| `galaxies.json` | 138,208 | JSON array `{name,pos[pc,int],MV,rh(pc),host,ell,pa,ra,dec}` | 911 galaxies (LV 736, MW 67, LF 64, M31 42, LG 2). Distance 21.6 kpc (Draco II) … 17.78 Mpc (NGC 1042). rh 20 … 40,177 pc; **167 at the 200 pc fallback**, 8 at the 20 pc floor. MV −21.7 … 0.0; 7 at the −8 fallback. **ell = 0.25 default for 542** (369 non-default; 8 capped at 0.85). **PA = 0 for 689** (222 non-zero). M31 ell 0.72 / PA 38; M33 0.45 / 23. **Missing major Local Volume galaxies:** M81, M82, NGC 253, Cen A, M83, IC 342, M101 (only their dwarfs appear) | LVDB dwarf files (CC0) plus M31/M33 hard-coded in the script. **The on-disk JSON has `ra`,`dec` keys and M31/M33 `ell`/`pa` that `extract_data.py` does not produce** (L113-117) | **Re-derive.** Fix the pipeline, flag defaults per field, use LVDB `M_V`, and add a complete Local Volume census: Karachentsev+2013 UNGC, CDS `J/AJ/145/101`, 869 galaxies, reachable. |
| `starnames.json` | 26,697 | `[{n, p:[x,y,z pc galactic], m}]` sorted by m | 460 names; m up to 14.67. **43 are fainter than 8.54, so they are not drawn in `stars.bin`** (e.g. Proxima 11.01, Barnard's 9.54, Wolf 359 13.45) | HYG v4.1 `proper` | **Do not copy.** CosmoSynth's `stars_named.json` (548 names per the importer docstring, index-linked into csstars) supersedes it. |
| `constellations.json` | 38,331 | `{segs:[[[x,y,z],[x,y,z]]…], names:[{name,pos}]}` (pc, galactic, 2 dp) | **679 segments, 88 names**, 703 unique vertices, segment length 1.0 … 844.9 pc, farthest endpoint 847 pc. Name position = 3D mean of the figure's segment endpoints | Stellarium `skycultures/modern/index.json` (HIP polylines). Stellarium repo GPL-2.0; the skyculture's `description.md` says **"Text and data: CC BY-SA 4.0"**. Endpoints come from HYG v4.1 HIP positions | **Re-derive** as HIP pairs → csstars indices. The atlas stores positions, not ids, so they cannot be joined to csstars without a nearest-neighbour match. |
| `markers.json` | 4,320 | `{markers:[{name,pos,desc}], stars:[…24…]}` | 9 cloud markers (Sun, Orion 414 pc, Taurus 140, Ophiuchus 140, Perseus 300, California 470, Cepheus 350, Chamaeleon 180, Aquila Rift 250; each from `lb_xyz(l,b,d)`). **24 named-star entries that the app never reads and the script never writes** (`"stars":[]` at L133) | "Published (l,b,d) values", **no per-entry citation** | Copy the values, **add a reference per entry**; drop the unused `stars`. |
| `mw_landmarks.json` | 699 | `[{name,pos,desc}]` | 7: Sgr A* [8178, 0, −14] ("26,700 ly": 8178 pc × 3.2616 = 26,673 ly), Sun, and 5 famous globulars (47 Tuc, ω Cen, M4, M13, M15) | Script L81-96; R0 = 8178 pc matches GRAVITY 2019 (INFERRED attribution) | Copy, and add citations. |
| `lg_landmarks.json` | 856 | `[{name,pos,desc}]` | 13 (LMC, SMC, Sgr dSph, Fornax, Sculptor, Leo I, NGC 6822, M31, M33, IC 10, WLM, Draco, Sextans) | Subset of galaxies.json | Derive from the galaxy catalogue as a "landmark" flag. |

**Conclusion on reproducibility (VERIFIED).** `extract_data.py` needs `numpy` + `msgpack`. It
downloads HYG twice (once for stars, once for constellations), and its output does not match
two of the shipped files. A CosmoSynth importer must be the source of truth. CLAUDE.md requires
**stdlib-only** importers. That is feasible for all of this, FITS included: a primary HDU is
2880-byte header blocks followed by big-endian float32. The msgpack/zlib extraction goes away if
the cube comes from CDS instead.

---

## 4. Feature inventory: exact behaviour, formulas, and the CosmoSynth mapping

Each subsection gives what the atlas does (VERIFIED from code unless marked), then **→ Port**:
how it maps onto CosmoSynth.

### 4.1 Scale model, presets, and zoom-gated visibility

- Model scale `s = 10^logScale` m/pc (L834-836). Readout: if `s > 40` it shows cm/AU
  (`4.8481368e-6·s·100`), otherwise the size of the 2000 pc cube in the room (`fmtSpan`, L831-837).
- Presets (L70-72, L1223-1225): **Clouds −3.1**, **Galaxy −4.75**, **Local Group −6.05**.
- Two-hand and right-stick clamp: **[−7.3, 0.5]** (L1474, L1511). This is a bug; see §5 B1.
- `applyScale()` visibility table (L838-869), with `cloudZone = 3e-5 < s < 40`:

| Element | Visible when |
|---|---|
| frame (cube edges), grid2k | cloudZone ∧ gridOn (grid2k also ¬cylOn) |
| grid60k (60 kpc, 5 kpc cells, centred on the GC) | ¬cloudZone ∧ s > 3e-7 ∧ gridOn ∧ ¬cylOn |
| cylStellar / cylGalactic / cylLG | cloudZone / (¬cloudZone ∧ s > 2.5e-6) / (s ≤ 2.5e-6); **all also need gridOn ∧ cylOn** |
| dust, stars, galaxies, solar | layer toggle ∧ SOLO allows |
| Cepheids, globulars, constellation lines | toggle ∧ SOLO ∈ {null, 'stars'} |
| constellation labels | constOn ∧ labelsOn ∧ solo-stars ∧ 2e-5 < s < 40 |
| cloud / MW / LG markers | labelsOn ∧ mCat ∧ (2e-5 < s < 40) / (2e-7 < s < 4e-4) / (s < 5e-6) |
| solar labels | labelsOn ∧ mSolar ∧ solarOn ∧ solo-solar ∧ s > 250 |

**→ Port.** 1 m/pc = 1/3.0856776e13 m/km, so `UnitsPerKm = 10^L / 3.0857e13` and
`L = log10(UnitsPerKm) + 13.489` (INFERRED arithmetic).

| Preset | UnitsPerKm | What fits |
|---|---|---|
| Clouds | 2.57e-17 | 2 kpc cube = 1.59 m |
| Galaxy | 5.76e-19 | 40 kpc = 0.71 m |
| Local Group | 2.89e-20 | 2 Mpc = 1.78 m |
| Solar (4.55) | 1.15e-9 | 1 AU = 0.172 m |

CosmoSynth's `TabletopManipulator` clamps UnitsPerKm to [1e-20, 1e6] (VERIFIED, i.e.
L ∈ [−6.51, 19.49]). **The lower bound must drop to about 1e-22** to show the 17.8 Mpc Local
Volume at tabletop size; the atlas minimum −7.3 is 1.6e-21. `ScaleSpace` "Galactic" band
(1e15–1e22 km, i.e. 32 pc – 324 Mpc) already covers it. Add the three presets as
`WristDashboard` rungs; the existing rungs stop at "Solar neighbourhood".

### 4.2 Dust volume (raymarched 3D texture)

- Texture: `Data3DTexture(uint8, 201,201,81)`, RedFormat, Linear filtering, `unpackAlignment 1`
  (L189-194).
- Unit box `BoxGeometry(1,1,1)` scaled to 2000×2000×800 pc, **BackSide**, transparent,
  depthWrite off, **renderOrder 10** (L200-260).
- Fragment shader (L212-255, key lines):
  ```glsl
  float dt = 1.74 / n;                         // n = uSteps; √3 ≈ 1.732 = unit-cube diagonal
  t += dt * hash(gl_FragCoord.xy);             // static per-pixel jitter (no temporal accum.)
  for(i<256){ if(t>tEnd || alpha>0.985 || i>=n) break;
    float s = texture(uMap, p).r;              // LOG-encoded byte /255
    float d = smoothstep(uCut, 1.0, s);
    if(d>0.001){ float a = 1.0 - exp(-d*d*uDensity*dt*60.0);
      acc += (1.0-alpha)*a*ramp(d)*(0.35+1.9*d); alpha += (1.0-alpha)*a; } t += dt; }
  if(alpha<0.003) discard; fragColor = vec4(acc, alpha);
  ramp: t<0.45 mix((.055,.10,.28),(.10,.42,.55)); t<0.78 → (.98,.52,.16); else → (1.0,.93,.80)
  ```
- Controls: Density 0.1–6 (default 1.6), Threshold 0–0.7 (0.22), Quality/steps 24–220 (110
  desktop; **≤72 in XR**, L951).
- A frame (`EdgesGeometry` of the box, 0x3d5f8a, additive, opacity 0.6) plus the plane grids.

**Issues** (VERIFIED from code; visual consequences INFERRED):
- The transfer function acts on **log density**, not density.
- Opacity per step is in unit-cube units, so vertical sight lines get 2.5× the optical depth per
  pc of horizontal ones.
- The volume is drawn after all stars, so it tints stars that lie in front of clouds.
- Colour + alpha output goes through normal blending even though `acc` is already weighted by
  alpha: effectively premultiplied twice.

**→ Port.** `BodyType.Nebula` exists but nothing renders volumes today.
- **Core:** a `DustCube` parser for a `.csdust` binary (magic, version, dims, extents in pc, frame
  id, encoding, units string, then a u8/u16 grid), `Sample(pc)`, and
  `ExtinctionAlong(a, b) → A_V (mag)`. Tests: decode round-trip, trilinear sampling, and additive
  segment integration.
- **Runtime:** a Built-in-RP raymarch shader.
  - Step in **pc** with `a = 1 − exp(−κ·ρ·ds_pc)`, where ρ = nmag/pc × 1e-9 = mag/pc and
    κ = ln(10)/2.5 converts magnitudes to optical depth.
  - Premultiplied blend `Blend One OneMinusSrcAlpha`.
  - Decide the passthrough alpha policy deliberately; CLAUDE.md warns alpha is read as coverage.
- **Couple stars to dust:** per-star extinction and reddening along eye→star, sampled from the
  same texture (§4.3). That replaces the render-order hack with physics.
- Provenance: position/extent Measured (survey geometry); density **Inferred** (Bayesian
  inversion); colour ramp Procedural.

### 4.3 Stars ("flux-true" HYG)

- Per-star attribute `psize = L^0.28`. Built from `absM = mag − 5·log10(max(d,1)/10)`,
  `L = 10^(−0.4·absM)` (L340-348).
- The **Sun** is appended at (0,0,0) with M = 4.83 (psize 0.2878) and B−V 0.65 (L349-351).
- Colour: B−V → T (Ballesteros 2012), then T → RGB (Tanner Helland fit) (L326-333):
  ```js
  T = 4600*(1/(0.92*bv+1.7) + 1/(0.92*bv+0.62)); u = T/100;
  r = u<=66 ? 255 : 329.7*(u-60)^-0.1332;
  g = u<=66 ? 99.47*ln(u)-161.12 : 288.12*(u-60)^-0.0755;
  b = u>=66 ? 255 : (u<=19 ? 0 : 138.52*ln(u-10)-305.04)
  ```
- Vertex shader, model mode (L302-306):
  `s = psize·uBright·uK / d_view` (d_view = −mv.z, in **metres**),
  `alpha = clamp(s,0,1)·0.75`, `gl_PointSize = clamp(s, 1.2, uMaxPx)`.
  Stars use uK = 2.1 and uMaxPx = 20; Cepheids 2.1/6; globulars 1.6/8 (L357, L379, L397).
  `uProjH` is declared but unused here, so point size ignores display resolution.
- Fragment shader (L309-317): Gaussian `a = exp(−4.5 r²)·vA`, discard a < 0.02, colour `vC·1.5`.
  Additive, depthWrite off, renderOrder 1.

Why "flux-true" is only approximate (INFERRED): the pixel diameter goes as L^0.28/d, so the
integrated brightness goes as L^0.56/d². That is honest inverse-square in distance but
compressed in luminosity.

**→ Port.**
- CosmoSynth draws **9,000 stars as fixed quads on a 900-unit sky sphere, directions only**. The
  comment in `SkyAndRings.EmitCatalogSky` says "Distance is deliberately discarded here".
  Separately, `NearbyStarSystems` generates worlds within 1 pc. **No 3D star volume exists.** It
  is the key new renderer.
- Build it from csstars: the 109,389 stars with known distance, keeping the DistanceUnknown
  10,224 on the sky sphere.
- Use a GPU billboard: 4 verts per star or instanced quads, positions relative to the Sun in pc.
  The vertex shader takes `rel = p − eye_pc`, computes flux from `|rel|`, and **places the vertex
  at `min(|rel|, R_sky)` along `rel`**. That sidesteps `DepthBands`' 5e5-unit far plane and float
  precision.
- **Follow CosmoSynth's learned star rules, not the atlas's:**
  - size floor ≥ ~2.5–3 px, never 1.2 px ("a one-pixel feature has no shape"; user report
    1.7.0 "square looking patches");
  - brightness in RGB, not alpha;
  - extra geometry (halo, spikes) for the bright end;
  - `StarSprite`'s additive blend **without writing destination alpha** (passthrough).
- Colour: CosmoSynth stores T (Ballesteros, the same formula), not B−V. Use
  `PlanetGenerator.BlackBodyColor(T)`, as the catalogue already does; do not port the Helland fit.
- Provenance: position = Measured (HYG parallax); luminosity = **Inferred** (from m, d and, once
  dust is in, A_V); colour = Inferred.

### 4.4 Observed-sky mode (limit magnitude, "from here")

- Vertex shader, observed mode (L291-301):
  ```glsl
  float L = pow(psize, 3.5714286);                // undo L^0.28
  float D = max(distance(position, uEye), 0.02);  // pc, eye in MODEL space
  float m = -2.5*log10(L) + 5*log10(D) - 5;       // = M + 5 log10(D/10pc)
  float b = pow(10.0, 0.4*(uLim - m));            // 1 at the limit
  s = 1.15 * pow(b, 0.23) * uExpo;  vA = clamp(s*0.45,0,1)*0.8;
  gl_PointSize = clamp(s, 0, uMaxPx*1.1); if(b<1.0) gl_PointSize = 0.0;
  ```
- `uEye` = camera world position → `model.worldToLocal`, every frame (L877-887). Toggle
  OBSERVED; LIMIT MAG slider 3–14 (default 6.5). Applies to stars, Cepheids and globulars.
- Checked on the data (VERIFIED, same formula offline): from the Sun at limit 6.5, **8,653 stars
  pass**. The README claims "≈5,000 (verified)"; the dataset has 4,965 at ≤ 6.0 and 8,714 at
  ≤ 6.5. CosmoSynth's catalogue has 8,920 at ≤ 6.5.
- **The data stops at mag 8.54**, so limits above ~8.5 reveal nothing new from the Sun, even
  though the README advertises 12–14 as telescope settings.
- At tabletop scales the camera sits about 1 m / s pc from the model origin (≈1.26 kpc at the
  Clouds preset). The "observed" sky is then computed from far outside the data (INFERRED). The
  earlier From-Sun / From-Here pair (README L99) was clearer.

**→ Port.** In the 3D star shader, expose **vantage = Sun / viewer / selected object** explicitly,
with limit magnitude and an eye-response exponent (0.23).
- Add extinction: `m = M + 5·log10(D/10) + A_V(vantage→star)`.
- De-redden M once, offline: `M = m_obs − 5·log10(d/10) − A_V(Sun→star)`.
- Keep CosmoSynth's sky sphere as the "from the Solar System" limit case; parallax there is
  sub-pixel, as `SkyAndRings` already argues.
- The pure maths (m from M and D, the visibility test, the compression) belongs in Core with
  tests. Known answers: the Sun seen from α Cen should be about V ≈ 0.5 (INFERRED literature
  value, to be pinned by the test author); Sirius −1.44 from the Sun (already in
  `StarCatalogTests`).

### 4.5 Cepheids

- Points; colour lerps from young `#9fc8ff` to old `#ffb36b` with
  `t = clamp(log10(age/20)/log10(20), 0, 1)`, so 20 Myr is fully young and 400 Myr fully old
  (L366-372).
- Every Cepheid gets the same `M = −4.2` (L373), even though the period is in the source table.

**→ Port.** Use the importer to keep the period and derive M from a Leavitt-law relation
(Inferred; state the relation in provenance). Keep dDIST as `Uncertainty`. Review the >30 kpc
outliers (16 rows) before shipping. New `BodyType.StarCluster`-like records aren't needed; a
lightweight `DeepSkyPoint` record list with provenance is enough.

### 4.6 Globular clusters

- Points, colour `#ffe3b0`, `psize = (10^(−0.4·max(MV,−11)))^0.28`, uK 1.6, max 8 px (L384-399).

**→ Port.** Use LVDB `M_V` (#85) and `rhalf`, and keep the names. Globulars can switch from point
to a resolved sprite when their half-light radius exceeds a few pixels (CosmoSynth's
apparent-size LOD).

### 4.7 Galaxies (morphology from ellipticity and PA)

- Point sprites; `psize = 2·rh` pc;
  `alpha0 = clamp(0.16 + (−MV−7)·0.05, 0.10, 0.9)`;
  colour `t = clamp((−MV−8)/12, 0, 1)`, `rgb = (1, 0.93−0.05(1−t), 0.82+0.14t)` (L403-418).
- Vertex shader (L434-441): `px = psize·mscale/d·uProjH`, with mscale taken from the
  modelView column length. `uProjH = h/2/tan(fov/2)`, where h = 1400 in XR or the canvas height
  (L182-185). `vA = clamp(alp·uBright·clamp(px/2,0.3,1.4))`,
  `gl_PointSize = clamp(px·1.6, uMinPx, 500)`.
- Fragment shader (L444-456):
  ```glsl
  r = rotate(gl_PointCoord-0.5, PA);  r.y /= max(1-ell, 0.18);  rr = 4|r|²;
  core = exp(-9rr); env = exp(-1.7rr); a = (0.62core+0.38env)·vA; discard a<0.012;
  color = col·(1.1+1.6core)
  ```

**Issues** (VERIFIED from code; the meaning INFERRED):
- PA rotates in **screen space**, so orientation spins with head roll and view direction.
- 60 % of ell and 76 % of PA values are defaults drawn as if measured.
- Sprites cap at 500 px.

**→ Port.** Draw each galaxy as an **oriented 3D quad (or thin disc) fixed in the sky frame**,
built from (ra, dec, PA, ell), which are all in the shipped JSON.
- The major axis on the sky is `north·cos PA + east·sin PA`, with north/east the tangent basis at
  (ra, dec).
- For discs, inclination follows from `cos² i = (q² − q0²)/(1 − q0²)` with `q = 1 − ell` and
  intrinsic thickness `q0 ≈ 0.2`. Check for M31: q = 0.28 gives i ≈ 78.5° against ~77° in the
  literature (INFERRED arithmetic, a good unit test).
- The front/back sign is ambiguous. Label that choice **Procedural**.
- Where ell/PA are defaults, draw round and faint, and say so in provenance.
- Apparent-size floor **and** ceiling ("small things need two bounds").
- `BodyType.Galaxy` exists in Core.

### 4.8 Landmarks and marker categories

- Three label groups with dots:
  - clouds: orange `#ff9a3c`, cap 380 pc;
  - MW: cyan `#6fd6ff`, cap 9000;
  - LG: violet `#c9a7ff`, cap 250,000.
- Plus solar (`#8fd8b8`, cap 1.2e-4 pc, min 1e-9) and constellation names (`#7fb8e8`, cap 90)
  (L511-513, L542, L823).
- Toggles: MK·CLOUD / MK·MW / MK·LGRP / MK·SOLAR.

**→ Port.** One `Landmark` record type (name, position, category, description, **reference**).
Categories drive a label layer with scale windows (§4.13).

### 4.9 Constellations (88 IAU figures as HIP–HIP 3D segments)

- One `LineSegments`, colour 0x6fb8e8, opacity 0.55, additive, renderOrder 2, off by default
  (L535-543).
- Names sit at the 3D mean of the figure's endpoints, with a dot there too; that point is not a
  star.
- Pipeline (script L135-156): Stellarium modern `index.json` → consecutive pairs of each HIP
  polyline → HYG HIP positions.

**→ Port.**
- Extend `build_starfield.py`, which already holds every HYG row, including `hip`, to also emit
  `constellations.json` as **pairs of csstars indices** plus the IAU abbreviation and full name.
  That is the least new code and keeps one HYG version.
- Render as one ribbon mesh with screen-constant width (~1.5–2 px, feathered), additive, colour
  from CosmoTheme.
- Place names at the **direction** of the figure's brightest star (or the angular mean), not the
  3D mean.
- Provenance: segment endpoints Measured; the figure itself is a cultural convention →
  `Fictional` (authored), source "Stellarium modern skyculture, CC BY-SA 4.0".

### 4.10 Hover star names

- A ray from the mouse (desktop) or the right controller (XR) goes into model space. Each frame,
  rank the 460 named stars by `angle = perp/t < 0.05 rad`, then show the **5 nearest**.
- Labels are created lazily and cached, never disposed. Height `clamp(0.05/s, 1e-9, 300)` pc
  (5 cm) (L598-647).

**→ Port.** Use `stars_named.json` (index-linked). The ray comes from `XrInput` pose or the
`BodySelector` ray. For picking beyond named stars (109k), add a coarse spatial hash, built once.

### 4.11 Solar System (LOD, Standish elements, comets, belts, heliosphere)

- **Epoch:** `JD = Date.now()/86400000 + 2440587.5`, `T = (JD−2451545)/36525`, **computed once
  at load**, so there is no time evolution (L669-670).
- **Kepler:** normalise M, `E0 = e<0.8 ? M : π`, 20 Newton iterations (L671-676). `orbPos`: the
  standard perifocal → ecliptic rotation (L677-684).
- **Planets:** JPL "approximate positions" **Table 1 (1800–2050)**. Mercury–Neptune values
  **match JPL's current page exactly** (VERIFIED against `ssd.jpl.nasa.gov/planets/approx_pos.html`).
  Pluto is included, but the current Table 1 has no Pluto row (an older version, INFERRED).
  `w = ϖ − Ω`, `M = L − ϖ`.
- **Minor bodies:** Ceres, Vesta, Eris, Makemake, Haumea, with `M = M0 + 360·(JD−ep)/P`.
- **Comets:** Halley (Tp 2446467.4, P 27,510 d), Hale-Bopp (a 186 AU, e 0.99511), 67P (Tp
  2457247.6, P 2,353 d), with `M = 360·frac((JD−Tp)/P)` (L697-709, L764-780).
- **Frames:** ecliptic → equatorial (ε = 23.439281°) → galactic (L653-661).
  `AU = 4.8481368e-6 pc`.
- **Bodies:**
  - Lambert spheres with exaggerated radii (in AU): Jupiter 0.42, Saturn 0.36, Earth 0.16 …
    (L713-714).
  - Sun: an emissive sphere of 0.10 AU plus `PointLight(0xfff2d0, 2.2, decay 0)` and
    `AmbientLight(0x404860, 0.7)` (L742-747).
  - Saturn ring from 1.35 to 2.3 r, placed with `ring.quaternion = eclQ; rotateX(26.7°)` (L725-731).
  - Comet tail: a line from the nucleus to **0.8 AU anti-sunward**, at any heliocentric distance
    (L732-737).
- **Orbits:** 181-point ellipses, additive; opacity 0.45 (planets), 0.22 (minor), 0.3 (comets).
- **Belts** (ecliptic annuli): asteroid 2.1–3.3 AU (opacity 0.07), Kuiper 30–50 AU (0.05), each
  labelled (L782-791).
- **Heliosphere** (L793-822). Nose direction l = 5°, b = 19°. Fresnel rim shader
  `rim = (1−|N·V|)^2.6`, colour `uC·(0.35+1.8·rim)`, alpha `(0.06+rim)·uA`, additive, DoubleSide.
  Three shells:
  - termination shock: sphere of 90 AU;
  - heliopause: 120 AU, scaled (1, 1, 1.9) and offset −55 AU along the nose;
  - "bow wave (no shock, IBEX)": partial sphere of 230 AU (θ ≤ 0.75 rad).
- **Visibility:** the group always renders; labels only when s > 250 m/pc (L855-856).

**→ Port.** Do **not** port Standish or the hand elements: CosmoSynth's Horizons catalogue (31
bodies incl. 1P/Halley, Pluto, Ceres, Vesta, Pallas, Eros, Bennu; VERIFIED in
`solar_system.json`) is validated and time-animated. To add Eris, Makemake, Haumea, Hale-Bopp
and 67P, extend `build_solar_system.py`. CLAUDE.md's rule applies: no GM from Horizons → skip or
hand-cite, as `bodies.py` does. Port as new decorations on the existing Sun and bodies:
- Belts: an annulus plus a label, Inferred extents, drawn Procedural.
- Heliosphere shells: radii Measured (Voyager 1 at 94 AU and V2 at 84 AU for the termination
  shock are the natural citations, INFERRED); shape and offsets Procedural. Cull back faces or
  draw single-sided when inside, because of fill rate.
- Comet tails: only within about 3–5 AU of the Sun, with length growing as r_h falls. Ion tail
  anti-sunward; dust tail curved behind along the orbit. All Procedural.
- Saturn's ring: CosmoSynth already has rings at real radii plus IAU pole elements. Keep those.
- The "Sun is just a G star" point belongs in the 3D star layer (§4.3), switched off inside
  about 0.1 pc, where the real Sun body takes over.

### 4.12 Grids

- `frame` = cube edges; `grid2k` = `GridHelper(2000 pc, 20)` (100 pc cells);
  `grid60k` = `GridHelper(60000, 12)` at [8178, 0, 0]. All in the galactic plane, additive,
  opacity 0.4–0.5 (L262-276).
- **Cylindrical volume grids** (`makeCylGrid`, L547-576). Each ring is drawn at z ∈
  {−halfH, 0, +halfH}: mid ring opacity 0.55, outer levels 0.22, 129 points. There are 12
  mid-plane spokes and 12 rim verticals. Each ring label sits at angle 0.42 rad.

| Regime | Centre (pc) | Rings | halfH | Label cap/min |
|---|---|---|---|---|
| Stellar | [0,0,0] | 250 ("250 pc · 815 ly"), 500, 750, 1000 ("1 kpc · 3260 ly") | 300 pc | 120 / 2 |
| Galactic | [8178,0,−14] | 5, 10, 15, 20 kpc ("20 kpc · 65 kly"), **26,000 labelled "Sun orbit R≈8.2 kpc"** | 1200 | 3500 / 60 |
| Local Group | [0,0,0] | 300 kpc, 1 Mpc ("1 Mpc · 3.3 Mly"), 3, 8, 16 Mpc ("16 Mpc · 52 Mly") | 2.5 Mpc | 2.5e6 / 4e4 |

- Ring label height `clamp(0.028/s, min, cap)` → 2.8 cm (L586-595). There are 114 separate
  `Line` objects (VERIFIED count: 4×3+24, 5×3+24, 5×3+24).

**→ Port.** One `CylinderGrid` mesh per regime (ribbons, one draw call each), gated by scale
band. Fix the Sun-orbit ring: add R0 = 8178 pc and keep 26 kpc as its own ring. Decouple from the
plane toggle. Colour must not be "uniform blue"; per feedback 1.4.0, use a muted CosmoTheme
neutral with the accent reserved for controls.

### 4.13 Labels

- `makeLabel` (L472-491) draws on a **canvas**:
  - IBM Plex Mono 600, 46 px (small 36 px); sub-line 26 px;
  - 7 px dark outline `rgba(3,6,12,.88)`;
  - 12 px padding; canvas height 58 / 48 / 96 (with sub-line).
  - Then a `PlaneGeometry(aspect,1)` with its own `CanvasTexture` (anisotropy 4), depthTest and
    depthWrite off, DoubleSide, renderOrder 20.
- Size (L515-530):
  `h0 = clamp(0.085/s, min, cap)` pc, i.e. **8.5 cm in the room, a linear size**. Crowding:
  `n = #labels within 2.4·h0`, **`h = h0/(1 + 0.42·√n)`**; the README's "1/√n" is a
  simplification. Label lifted `0.9·h` in z. Dot radius `clamp(0.004/s, 0.25·min, 0.02·cap)`
  (4 mm).
- Orientation: `orientLabels` runs `L.lookAt(cameraWorldPos)` on every label, every frame. The
  default `up` gives "upright in the room". FACE TEXT off freezes them (L467-471, L913).
- Totals (VERIFIED by code): **~151 persistent label planes** (9 clouds + 7 MW + 13 LG + 88
  constellations + 20 solar + 14 grid rings), plus up to 5 names, measure and crosshair labels,
  **plus 137 dot spheres**. Each is a separate draw call; each label has its own texture.

**→ Port.** Extend `BodyLabelLayer`, which ranks, caps at 14 and does greedy screen-space
declutter with pooled `TextMesh`, into category layers with the scale windows of §4.1.
- **Sizes in dmm via `CosmoTheme`** (Caption 19 / Body 24 dmm × viewing distance), not 8.5 cm.
- Keep the look-at-upright default and the freeze toggle.
- Declutter by dropping, as CosmoSynth does. Optionally apply the crowd shrink first, as a softer
  stage.
- One font material, and batch.

### 4.14 Brightness assists ("honestly artificial")

Formulas (L859-865):
```js
skyBoost  = max(1, (s/1.5e-3)^0.9);                                  // stars only
farAssist = s < 1.5e-3 ? min(6, (1.5e-3/s)^0.24) : 1;                // stars, Cepheids, GCs
starMat.uBright = expo * max(skyBoost, farAssist);
ceph/gc.uBright = expo * farAssist;
galMat.uBright  = expo * galBoost;   galMat.uMinPx = 1.5 + 0.14*galBoost;
```
- Exposure 0–3 (HUD) or 0.1–3 (panel), default 1. Galaxy glow 0.5–30, default 6.
- farAssist is **already 1.17× at the default Clouds preset** (VERIFIED arithmetic) and reaches
  its 6× cap near s ≈ 8.6e-7, the LG preset.

**→ Port.** Keep, but surface as presentation state. Like CosmoSynth's readable-scale mode, the
provenance/status card should say "brightness assist ×N" whenever one is active.

### 4.15 Measure tool

- `pickables` = 460 star names + 9 clouds + 7 MW + 13 LG = **489** objects. Galaxies (other than
  LG landmarks), Cepheids, globulars and solar bodies are **not** pickable.
- Pick: the smallest angular offset `< 0.05 rad` along the model-space ray.
- Desktop: click with < 5 px drag. XR: controller `selectstart`, or pinch, which WebXR delivers
  as select (L959-1045).
- Two points give a line (depthTest off, cyan) and a label `fmtDist(pc)` = "… pc · … ly"
  (ly = pc × 3.26156, with k/M prefixes) plus the sub-line "A → B". A third pick starts over.
  Dots 6 mm, label 7.5 cm.
- **With Measure on, hand pinch never grabs** (L1465).

**→ Port.** Make it a mode of `BodySelector` plus star picking, in double km. Format pc / ly / AU
by magnitude; `WristDashboard.FormatDistance` and `ProvenanceHud` already format ly/AU. Also let
it measure between any two selectable things, bodies and stars included.

### 4.16 Destinations, spaceship travel, 3D crosshair

- `DESTS` (L1055-1062), log m/pc:

| Key | Target (pc) | log |
|---|---|---|
| solar | [0,0,0] | 4.55 |
| orion | [−342,−190,−135] | −2.35 |
| sgra | [8178,0,−14] | −4.3 |
| omcen | ω Cen landmark | −3.9 |
| lmc | LMC | −5.0 |
| m31 | M31 | −6.1 |

- `goTo` (L1099-1123):
  ```js
  anchor = XR ? head + fwd_h·0.95 (y = max(head.y−0.05, 0.75)) : orbitTarget
  dy = atan2(fwd.x,fwd.z) − atan2(v1.x,v1.z)      // v1 = target − head, horizontal
  q1 = Ry(dy) · q0;  sc = 10^log;  p1 = anchor − q1·(d.p)·sc
  dur = 2.6 + 0.5·|Δlog|   // e.g. Clouds→Solar: 6.4 s; Clouds→M31: 4.1 s
  ```
- `updateTravel` (L1124-1137):
  ```js
  e  = smootherstep(u) = u³(u(6u−15)+10)        // position & scale: 0 → max → 0 speed
  er = smootherstep(min(1, u/0.35))              // yaw completes in the first 35 %
  q = slerp(q0,q1,er); p = lerp(p0,p1,e); log = lerp(l0,l1,e)
  ```
  The per-frame damping `k = 1 − e^(−6.5dt)` is applied on top, so the curve is eased twice.
  Grabbing cancels travel (L1460).
- **Crosshair** (L1065-1095): 8 lines along the normalised cube diagonals, from 0.55 R to R.
  `R = 0.075/s` (7.5 cm). Colour `#9fe8ff`, additive, depthTest off, renderOrder 25. The label
  (8 cm) sits at local z = 1.6. Fade: during travel 1; otherwise 1 until `crossT` = 6 s, then
  linear over 1.2 s. **`crossT` starts at goTo, not at arrival** (§5).

**→ Port.** A Core `TravelPlan`: a pure function of t returning (focus pc, log scale, yaw), with
tests for exact endpoints, zero speed at both ends, and yaw complete at u = 0.35.
- Drive `FloatingOrigin.OriginKm` (double) and `UnitsPerKm` from it.
- **Interpolate the model-space focus point, not the group position.** With
  `f(t) = lerp(f0, target, e)` and `p = anchor − q·f·s`, the target stays on the view line while
  scale changes exponentially. Linear world-position lerp against log-scale lerp swings the
  target off-axis mid-flight (INFERRED).
- For very large Δlog, consider van Wijk & Nuij (2003) optimal zoom-pan.
- Replaces `WristDashboard.Activate`'s instant jump plus `FrameBodyAtSize`.

### 4.17 Layer toggles, SOLO, marker categories

- State (L871-874): `gridOn, labelsOn, expo, galBoost, dustOn, starsOn, solarOn, mCloud, mMW,
  mLG, mSolar, cephOn, gcOn, galOn, constOn, solo, obsOn, limMag`, plus `namesOn, cylOn,
  billboardOn, measureOn`.
- SOLO ∈ {null, 'dust', 'stars', 'gal', 'solar'}, with `so(k) = solo===null || solo===k`.
  'stars' also covers Cepheids, globulars and constellation lines. **SOLO does not hide markers,
  labels or grids** (L866-868).

**→ Port.** One `LayerState` table, data like `XrInput.Bindings`, that the panel **and** any
legend are generated from. SOLO becomes a view over it.

### 4.18 Wrist panel v3

- **Watch** (L1181-1187, L1351-1356): at the left `wrist` joint (hand tracking) or the left grip.
  `CircleGeometry(0.019)` in cyan plus `RingGeometry(0.021–0.027)` in orange, depthTest off,
  renderOrder 30; `lookAt(camera)`.
- **Open / close** (L1384-1389, L1423-1433):
  - hands: right index tip within **5 cm** of the wrist, debounced **800 ms**;
  - controllers: right ray + trigger on the watch face.
  - Opening while pinned unpins, which is the "recall".
- **Panel:** canvas 512×640 → plane **0.28 × 0.35 m**. Material depthTest off, renderOrder 31.
  Cursor dot r = 6 mm, `#ffd7ae` (L1189-1197).
- **Anchor when not pinned** (L1363-1376):
  ```js
  hz = normalize((wrist − head).xz)
  target = (wrist.x + hz.x·0.10, wrist.y + 0.10 + PMH/2, wrist.z + hz.z·0.10)
      // bottom edge 10 cm above the wrist, 10 cm beyond it
  if |target − smoothP| > 0.006: smoothP = lerp(smoothP, target, 1 − e^(−7dt))   // 6 mm deadband
  wantYaw = atan2(cam.x − p.x, cam.z − p.z)
  if |Δyaw| > 0.35 rad (20°): yaw += Δyaw·(1 − e^(−4dt))                         // lazy yaw
  rotation = Euler(0, yaw, 0)                                                     // zero pitch/roll
  ```
- **PIN** copies the current pose; the panel stays in space until the watch is tapped (L1334-1338).
- **Pointer** (L1379-1406):
  - Right index tip in panel-local space inside `|x| < PMW/2 + 3 cm`, `|y| < PMH/2 + 3 cm`,
    `−3 cm < z < 12 cm` → canvas px `((x/PMW + .5)·512, (.5 − y/PMH)·640)`.
  - Else the right controller ray, if it hits within **1.2 m**.
  - Either way the pointer sets `panelHot`, which **suppresses right-hand pinch-grab** (L1465).
- **Touch hysteresis** (L1411-1420):
  - press when not down and `z ≤ 6 mm`;
  - while down and `z ≤ 20 mm`, sliders drag;
  - release when `z > 20 mm`.
  - Buttons are debounced **300 ms**; the press flash lasts 200 ms. Hit boxes pad ±3 px
    vertically.
- **Layout** (L1264-1273): X0 12, button W 116, gap 8. Caption h 24 (advance 26), slider h 42
  (48), row h 44 (50). A row of 1/2/3/4 buttons is 488/240/157/116 px wide. Slider track
  `x+126 … w−138`.
- **Tabs** (L1211-1263). Every tab has the top row `VIEW · LAYERS · TOOLS · PIN/UNPIN` (sys).
  - **VIEW:** "GO TO — orient & travel there (spaceship)": SOLAR ORION SGR A* ω CEN / LMC M31
    (nav). "SCALE PRESETS": CLOUDS GALAXY LOCAL GR (preset). "SKY — light as it reaches your
    eye": OBSERVED CONSTELL. LABELS FACE TEXT (sky), plus a LIMIT MAG slider 3–14.
  - **LAYERS:** DATASETS: DUST STARS CEPHEIDS GLOBULAR / GALAXIES SOLAR SYS. SOLO: S·DUST
    S·STARS S·GALAX S·SOLAR / ALL. MARKERS: MK·CLOUD MK·MW MK·LGRP MK·SOLAR / STAR NAMES,
    PLANE GRID, CYL GRID.
  - **TOOLS:** MEASURE pc/ly. RENDER: DENSITY 0.1–6, EXPOSURE 0.1–3, GAL GLOW 0.5–30.
- **Category colours** (L1205-1206): nav `#b48cff`, preset `#6fd6ff`, layer `#7fe0a0`, marker
  `#ffb35c`, tool `#ff8cb0`, sky `#8fd8ff`, sys `#dbe4f0`. Hovered = category colour at
  alpha 0x44 with a white 3 px stroke; "on" = alpha 0x2e with a coloured stroke.
- **Hands:** a flat additive tint `#9fc8ff` at opacity 0.5. The README calls it "starry"; it has
  no texture (L1178-1179, L1347-1350).

**→ Port.** CosmoSynth's `WristDashboard` is a **controller-only** list: X engages, the left stick
navigates, the right trigger acts. It follows `XrInput.TryGetPose(Left)`, and `XrInput.Read`
queries only `HeldInHand|Controller` devices (VERIFIED from code); with no pose, `FollowWrist`
calls `Dismiss()`. Unless an OpenXR hand-interaction profile registers the tracked hand as such a
device, the dashboard therefore disappears under pure hand tracking (INFERRED; check on the
headset). Port v3 as its successor, keeping CosmoSynth's invariants:
- **Content-sized layout** through `CosmoPanel`/`CosmoText`, with angular sizes, not a 512×640
  canvas.
- **All inputs through `XrInput`.** Add a fingertip-touch pointer: hand joints from the existing
  XR Hands subsystem in `XrHandInput`. Treat panelHot as an explicit claim, the same pattern as
  `ClaimNavigationStick`, so grab arbitration is declared.
- Keep glance/engage, and **also** allow touch.
- Constants to carry over: 6 mm press, 20 mm release, 6 mm position deadband, 20° yaw deadband,
  damping rates 7 and 4 s⁻¹, 5 cm/800 ms watch tap, 300 ms debounce, 1.2 m ray.
- **Do not reuse the category colours.** Green, amber and pink collide with
  `CosmoTheme.Provenance` (Measured `(0.435,0.851,0.541)`, Procedural `(0.949,0.694,0.361)`,
  Fictional `(0.894,0.541,0.847)`; VERIFIED). Distinguish categories by captions and position,
  with CosmoTheme `Accent` for controls.

### 4.19 XR manipulation and locomotion

- **One-hand grab** (L1447-1489): `m = pose(t) · pose(0)⁻¹ · model(0)`, decomposed with scale
  kept on `targetLog`. Full 6-DoF, so it can tilt the galaxy. Hand pose = the index-tip joint.
- **Two-hand:** `k = clamp(d/d0, 0.02, 50)`, `s = clamp(s0·k, 10^−7.3, 10^0.5)`. Yaw comes from
  the horizontal inter-hand vector; the position keeps the midpoint fixed.
- **Sticks** (L1492-1516): deadzone 0.12.
  - Left: translate along camera forward/right at `1.6·max(300·s, 0.25)` m/s.
  - Right x: yaw at 1.4 rad/s.
  - Right y (> 0.15): `targetLog −= sign(y)·0.55·dt`, i.e. 0.55 log10 units per second,
    clamped to [−7.3, 0.5].

**→ Port.** `TabletopManipulator` (log-space scale smoothing `1 − 0.5^(60dt)`) and `XrHandInput`
(pinch closes at 22 mm, opens at 35 mm, tap = travel < 30 mm) already exist and are better
engineered. Carry over only the **yaw-only two-hand twist** and, if wanted, the damping constant.
CosmoSynth's binding table (VERIFIED):

| Control | CosmoSynth | Atlas | Conflict? |
|---|---|---|---|
| L stick | fly | fly | no |
| R stick | rise · turn | turn / log-scale | differs |
| grip | hold · scale | grab / both = scale+twist | same |
| R trigger | select | measure pick / panel press | compatible |
| L trigger | boost | — | — |
| X | dashboard | — (the atlas uses the watch tap) | — |
| A | recentre | — | — |
| Y / B | time warp | — | — |
| R click | pause | — | — |

### 4.20 Desktop controls and HUD

- OrbitControls with damping; target (0, 1.25, −0.6); camera (0, 1.55, 1.9); fov 60 (L164-169).
- HUD (L66-106): 3 presets, 6 destinations, 7 sliders (Density, Threshold, Quality, Exposure,
  Limit mag, Gal glow, Scale) and 14 toggles (Labels, Star names, Measure, Plane, Cyl grid,
  Cepheids, Globulars, Galaxies, Dust, Stars, Solar sys, Face view, Observed sky, Constellations).
- Hover names; click-to-measure.

**→ Port.** CosmoSynth desktop is WASD fly (`DesktopInputSource` / `UniverseNavigator`), which is
fine. The HUD toggles map onto the same `LayerState` table.

### 4.21 Audio and haptics

- Audio (L1159-1171), WebAudio oscillator plus an exponential gain decay:
  hover `snd(1500→1500 Hz, 30 ms, vol 0.045)`; press `snd(880→620 Hz, 70 ms, vol 0.11)`.
- Haptics (L1172-1175), **right controller only**: hover `pulse(0.22, 10 ms)`; press
  `(0.7, 40 ms)`; watch toggle `(0.8, 50 ms)`.

**→ Port.** Use `XrInput.Pulse(hand, amp, s)`, which already exists. Audio is new to CosmoSynth
(no `AudioSource` anywhere, VERIFIED by grep): two procedurally generated `AudioClip`s of about
100 lines, played on one 2D `AudioSource`.

---

## 5. Bugs and inaccuracies found ("major corrections")

All VERIFIED by reading the code, unless marked INFERRED.

| # | Issue | Where | Port fix |
|---|---|---|---|
| B1 | **Two-hand scale and right-stick zoom clamp to [−7.3, 0.5]** while the slider max is 5.7 and the Solar destination is 4.55. Any two-hand grab or right-stick push inside the Solar System snaps the scale out by ≥10⁴. This is the same class of bug as the user's 1.4.0 complaint "restrictive to solar system distance". | L1474, L1511 | One scale-limit constant shared by every input. |
| B2 | "Sun orbit R≈8.2 kpc" is the label of the **26,000 pc** ring; there is no 8.2 kpc ring. | L581 | Add an R0 = 8178 pc ring. |
| B3 | Galaxy PA is rotated in **screen space** on point sprites. | L446-449 | Oriented quads in the sky frame (§4.7). |
| B4 | "Measured" morphology is mostly defaults: ell = 0.25 for 542/911, PA = 0 for 689/911, rh = 200 for 167, MV = −8 for 7. | data | Per-field provenance; draw defaults differently. |
| B5 | `extract_data.py` does not reproduce the shipped `galaxies.json` (ra/dec, M31/M33 ell/pa) or `markers.json` (24 star entries). | script L113-117, L133 | A CosmoSynth importer becomes the source of truth. |
| B6 | Dust renders after all stars (renderOrder 10 vs 1), so clouds tint stars in front of them. Per-step opacity is in unit-cube space, so vertical sight lines are 2.5× too opaque per pc (INFERRED consequence). Colour is effectively premultiplied twice. | L237-259 | Physical per-pc opacity, premultiplied blend, per-star extinction. |
| B7 | Absolute magnitudes ignore extinction: HYG observed V, and `m_V − μ` for globulars and dwarfs although LVDB provides `M_V`. | script L57-59, L90-91, L105 | De-redden with the dust cube; use LVDB `M_V`. |
| B8 | Brightest-60k-from-Sun sample: 462 stars ≤20 pc against 1,877 in csstars, and a data floor of mag 8.54. "Observed from here" skies are therefore missing their faint neighbours, and limits above 8.5 add nothing. | data | Use csstars. |
| B9 | Observed mode uses the camera's model-space position. At tabletop scale that is kiloparsecs outside the data (INFERRED). | L877-887 | Explicit vantage choice. |
| B10 | README says "≈5,000 stars at limit 6.5"; the formula gives 8,653 (4,965 ≤ 6.0). | README L99 | Correct the docs; test the count. |
| B11 | The crosshair fade timer starts at GO TO, not at arrival; flights over 6 s start fading on arrival. | L1080, L1091 | Start the timer at arrival. |
| B12 | Scale-preset buttons (HUD and panel) do not cancel an active travel, which overwrites them on the next frame. | L918-922, L1223-1225, L1132 | Cancel travel on any scale input. |
| B13 | Comet tails are a fixed 0.8 AU at any distance (Halley at ~35 AU has one), and the Solar System is frozen at load time. | L669, L732-737 | Activity-gated tails; CosmoSynth clock. |
| B14 | Saturn's ring uses the ecliptic frame plus `rotateX(26.7°)`, which gives the wrong node. | L728-729 | IAU pole (already in CosmoSynth). |
| B15 | Every Cepheid is drawn at M = −4.2, ignoring its period. | L373 | Leavitt law from the period. |
| B16 | The crowd rule is `1/(1+0.42√n)`, not "1/√n" as the README says. | L523 | Document it. |
| B17 | farAssist is already 1.17× at the default preset, so "flux-true at natural scales untouched" is slightly false. | L860 | Show the assist state. |
| B18 | The cylindrical grids also need the PLANE toggle on (`gridOn ∧ cylOn`). | L843-845 | Independent toggles. |
| B19 | 43 of 460 name labels (and measure targets) are stars that are not rendered. | data | Index-linked names. |
| B20 | Constellation name dots sit at 3D centroids that are not stars. | L502-506, L542 | No dot; place the name by direction. |
| B21 | A touch can fire on entry if the fingertip enters the panel volume already behind the surface (z ≤ 6 mm), e.g. from below (INFERRED edge case). | L1392, L1413 | Require an approach from z > 20 mm. |
| B22 | Measure mode disables pinch-grab on both hands. | L1465 | Arbitrate per hand. |
| B23 | Model-mode star px ignores display resolution (`uProjH` unused), so sizes differ between desktop and headset (INFERRED magnitude). | L302-306 | Angular sizing. |
| B24 | Labels are a fixed 8.5 cm linear height, which breaks CosmoSynth's angular rule. | L518 | CosmoTheme dmm. |
| B25 | The star-point floor of 1.2 px repeats the "square patches" failure (user report 1.7.0). | L305 | ≥2.5–3 px floor, brightness in RGB (CLAUDE.md). |
| B26 | Pluto's elements are not on JPL's current Table 1 (INFERRED to be from an older version). 67P uses 2015 perihelion elements with a fixed period (INFERRED drift). | L695, L708 | Horizons. |
| B27 | Hands are a flat tint, not "starry" as the README says. | L1178 | Cosmetic. |

---

## 6. Alignment with CosmoSynth: overlaps and conflicts

| Atlas feature | CosmoSynth today (VERIFIED) | Action |
|---|---|---|
| 60k HYG stars, 3D | `stars.csstars` 119,613 (ICRF pc, sorted by mag, flags), 9,000 drawn on a 900-unit sky sphere, direction only (`SkyAndRings`); `NearbyStarSystems` worlds within 1 pc | **New 3D star-volume renderer** over csstars; keep the sky sphere for distance-unknown stars and the "from the Solar System" limit. |
| Star names | `stars_named.json` (548, index-linked) | Reuse. |
| Constellations | none (grep "constellation" empty) | New by-product of `build_starfield.py`. |
| Dust, Cepheids, globulars, galaxies, landmarks | none; `BodyType.Nebula/Galaxy/StarCluster` enum values exist | New importers and renderers (§7). |
| Solar System (Standish) | 31 Horizons bodies, validated, animated clock | Keep CosmoSynth's; port decorations only. |
| Rings | real radii (`SkyAndRings.Rings`) | Keep. |
| Grids | none (grep) | New. |
| Measure | none | New mode of selection. |
| Travel | instant `SetOrigin` + `FrameBodyAtSize`; desktop `UniverseNavigator.TravelTo` | Core `TravelPlan`. |
| Wrist panel | `WristDashboard` (controller-only list; pose from controller devices only) | Successor panel via `CosmoPanel`/`XrInput`. |
| Labels | `BodyLabelLayer` (ranked, 14 max, greedy declutter) | Category layers with scale windows. |
| Haptics | `XrInput.Pulse` | Reuse. |
| Audio | none | New, tiny. |
| Provenance | per field, 5 classes, glyphs `= ~ * ?` | Every new field carries it (§7.4). |
| Frame | canonical ICRF km; ecliptic for presentation | Store the new data in ICRF pc (as csstars does); **galactic-up becomes a presentation rotation**. |

Conflicts the design phase has to settle:

1. **Room orientation.** The atlas is always galactic-up; CosmoSynth follows ICRF/ecliptic. Show
   the galaxy flat on the table (galactic-up) at galactic scales, and keep the ecliptic up in the
   Solar System. Tie the rotation to the destination or to a user toggle, never to continuous
   scale; rotating the world while zooming disorients (INFERRED).
2. **Colours.** Atlas category colours versus the provenance palette (§4.18). The user also
   rejected uniform blue lines (1.4.0) and thin lines (memory: "thin lines everywhere").
3. **Sizes.** The atlas uses linear cm; CosmoSynth uses dmm (§4.13).
4. **Scale limits.** `TabletopManipulator` minimum 1e-20 u/km must go to ~1e-22 (§4.1).
5. **Star rendering rules.** CosmoSynth's learned rules win (§4.3).
6. **Importer policy.** Stdlib-only Python, atomic writes (`os.replace` on success), magic-byte
   validation of downloads, "an artefact on disk is not evidence of the run" (CLAUDE.md).

---

## 7. Unity porting spec

### 7.1 Offline data (tools/cosmodata, stdlib-only)

| Script | Input (reachable from triton, VERIFIED) | Output | Notes |
|---|---|---|---|
| `build_starfield.py` (extend) | HYG v4.4 csv.gz (13.6 MB, Codeberg) + Stellarium modern `index.json` (206 KB) | adds `sky/constellations.json`: `[{abbr, name, pairs:[[i,j],…]}]` with csstars indices | HIP → sorted index while the rows are in memory; one HYG version; licence strings in the file. |
| `build_deepsky.py` (new) | Skowron `Data_Table_1.dat` (575 KB); LVDB `gc_harris.csv`, `gc_mw_new.csv`, `dwarf_mw.csv`, `dwarf_m31.csv`, `dwarf_local_field.csv`, `dwarf_local_field_distant.csv` (≈570 KB); UNGC `J/AJ/145/101` table1/table2 | `deep/deepsky.json` (or a `.csdeep` binary): per object id, name, kind (cepheid/globular/galaxy), **ICRF pc**, M_V ± err, rhalf pc, ell, PA, ra/dec, period, age, host, and a **provenance class per field** (default-flagged where the source is empty) | Magic-byte/HTML sniffing on every download; refuse zero-row parses; write atomically. |
| `build_dust.py` (new) | Vergely 2022 `explore_cube_density_values_025pc_v2.fits` (114 MB) or the `_010pc_` version (227 MB) | `deep/dust.csdust`: header (magic `CSD1`, version, nx ny nz, x0 y0 z0, dx dy dz pc, frame = galactic, units = "nmag/pc @550nm", encoding = log u8 or u16, vmin, vmax) + grid | Stdlib FITS reader (2880-byte header, BITPIX −32 big-endian). The 25 pc cube as R8 at its native 10 pc spacing is 601×601×81 = 29.3 MB. Resampled to 25 pc spacing, matching its correlation length, it is 240×240×32 ≈ 1.8 MB (INFERRED sizing). Alternatively crop to ±1.5 kpc at 10 pc. Record the resampling in provenance. |
| `build_landmarks.py` or a hand-authored JSON | markers, MW and LG landmarks (atlas values) | `deep/landmarks.json` **with a reference per entry** | Values copied from the atlas; citations added. |
| `build_largescale.py` (later tier, another agent) | 2MRS v2.40 tgz (39.5 MB; README L108 said unreachable, it **is** reachable) | — | Out of scope here; noted because the atlas README defers it. |

Do not download Edenhofer 2023 (`mean_and_std_xyz.fits` 15.7 GB) or Leike 2020
(`mean_std.h5` 2.37 GB, HDF5 needs h5py). Triton has ~19 GB free and CLAUDE.md requires stdlib
importers (VERIFIED sizes via the Zenodo API).

### 7.2 Core (engine-free, tested)

- `Core/Sky/Photometry.cs`: `AbsoluteMagnitude(m, dPc, Av)`, `ApparentMagnitude(M, dPc, Av)`,
  `Luminosity(M) = 10^(−0.4(M−4.83))`, the eye-compression functions (L^0.28 for model size,
  b^0.23 observed), and the visibility test. Tests: Sirius −1.44 at 2.64 pc; the count at ≤6.5
  from csstars; round trips.
- `Core/Deep/DustCube.cs`: parse `.csdust`, trilinear `Sample`, `ExtinctionAlong(a, b, steps)`.
  Tests: header/size mismatch refused, a known voxel decodes, line integral additivity, zero
  outside the box.
- `Core/Deep/DeepSkyCatalog.cs`: records plus provenance. Tests: counts, no NaN positions,
  M31 near 783 kpc, the >30 kpc Cepheids listed.
- `Core/Deep/GalaxyOrientation.cs`: sky-plane basis at (ra, dec), major axis from PA,
  inclination from ell with q0. Tests: M31 i ≈ 78° ± 2°; PA 0 → the axis points north.
- `Core/Sky/Constellations.cs`: pairs → catalogue indices. Tests: 88 figures, all indices
  valid, no pair with a DistanceUnknown star.
- `Core/Nav/TravelPlan.cs`: yaw solve plus smootherstep plus focus-space interpolation. Tests:
  endpoints exact, zero velocity at 0 and 1, yaw done at u = 0.35, and the target stays on the
  anchor line for all t.
- Frame test: `StandardFrames.EquatorialToGalactic` equals the atlas's Hipparcos matrix to ~1e-9
  (the IAU pole construction and the Hipparcos matrix should agree; INFERRED; the test proves it).

### 7.3 Runtime (Built-in RP, SPI)

1. `StarVolumeRenderer`: csstars → one static mesh (4 verts/star, 109k stars ≈ 437k verts) or
   an instanced quad. Custom shader in `CosmoShaders.All` (the stripping rule). Camera-relative
   pc with sky-radius clamping. Modes: model flux / observed (vantage, limit). Per-star A_V from
   the dust texture (tex3Dlod, 8–16 samples); **when the eye moves more than ~1 pc, compute it
   into a per-star buffer rather than per frame** (INFERRED cost control). Sun point hidden
   within ~0.1 pc.
2. `DeepSkyPointRenderer`: Cepheids and globulars, sharing the star shader with other constants.
3. `GalaxyRenderer`: oriented quads with the core+envelope profile
   `0.62·e^(−9rr) + 0.38·e^(−1.7rr)`; floor and ceiling on apparent size; LOD from point to quad.
4. `DustVolumeRenderer`: unit-cube mesh, raymarch in pc, premultiplied, steps ≤32–48, early exit
   at α > 0.985, per-pixel blue-noise jitter, and empty-space skipping from a coarse max-mip.
   Consider half-resolution.
5. `SkyLines`: ribbon meshes for constellations and grids (one draw per layer), screen-constant
   width, additive with `ColorMask RGB` (no alpha into passthrough).
6. `CatalogueLabelLayer`: categories × scale windows, CosmoTheme dmm sizes, look-at-upright or
   frozen, greedy declutter, batched text.
7. `SolarDecorations`: belts, heliosphere shells (single-sided when inside), comet tails, attached
   to existing views; each consults `IsGeometryDrawn` (CLAUDE.md).
8. `MeasureTool`: selection-based; the label formats pc/ly/AU.
9. `TravelController` plus `ArrivalCrosshair`: `TravelPlan` drives `FloatingOrigin` and
   `TabletopManipulator`.
10. `AtlasPanel`: successor to `WristDashboard`. Tabs VIEW / LAYERS / TOOLS, PIN, touch
    hysteresis; generated from the `LayerState` table; audio and haptics.

### 7.4 Provenance mapping

| Field | Class | Source string |
|---|---|---|
| Star position | Measured | HYG v4.4 (Hipparcos/Yale/Gliese), CC BY-SA 4.0 |
| Star luminosity / M | Inferred | from m, d (and A_V once dust is used) |
| Star colour / T | Inferred | Ballesteros 2012 from B−V (or from spectral class, flagged) |
| Dust density | Inferred | Vergely+2022 (CDS J/A+A/664/A174), nmag/pc |
| Dust colour ramp | Procedural | — |
| Cepheid position | Inferred | Skowron+2019 (mid-IR P–L distances), with dDIST as uncertainty |
| Cepheid age | Inferred | Skowron+2019 period–age |
| Globular / dwarf position | Measured/Inferred | LVDB (cite Pace 2025), distance-modulus references per object |
| Galaxy M_V, rhalf | Measured / Inferred | LVDB / UNGC |
| Galaxy ell, PA | Measured where present; **Unknown or Procedural where defaulted** | LVDB |
| Galaxy 3D orientation sign | Procedural | — |
| Constellation figures | Fictional (authored convention) | Stellarium modern skyculture, CC BY-SA 4.0 |
| Heliosphere radii | Measured | Voyager crossings (to cite) |
| Heliosphere shapes, belt rendering, comet tails | Procedural | — |
| Brightness assists, exaggerated sizes | presentation state (not data) | — |

### 7.5 Suggested order

1. Importers and Core tests: constellations, deep sky, dust.
2. `StarVolumeRenderer`, including observed mode.
3. Galaxies, Cepheids and globulars.
4. Labels, lines and grids.
5. `TravelPlan`, destinations and the panel.
6. Dust volume. Last, because it is the riskiest for performance.
7. Solar decorations.

Each step gets a headset check. CLAUDE.md: the harness renders flat and monoscopic.

---

## 8. Quest 3 performance risks

| Risk | Numbers | Mitigation |
|---|---|---|
| **Dust raymarch** | Up to 72 trilinear 3D fetches per pixel in the atlas's XR cap (110 on desktop). 28.6 % of voxels are non-empty past the cut (VERIFIED). If the cube covers ~half of both eye buffers (~1.5–3 Mpx, INFERRED), that is 100–200 M fetches per frame, 7–15 G/s at 72 Hz (INFERRED). The atlas relied on `setFoveation(0.7)`; **CosmoSynth disables foveation on Built-in RP** (CLAUDE.md, `XrSetup.DisabledAndroidFeatures`). | ≤32–48 steps, empty-space skip, early-out, lower-res pass, disable when outside the cube and far (bake an impostor), quality presets. Measure GPU time on the device. |
| **Single-pass-instanced custom shaders** | Every new renderer (stars, galaxies, dust, ribbons) needs the stereo instancing macros; procedural or instanced draws under SPI are untested here | Prototype one SPI procedural star draw on the headset first. The unresolved 1.7.0 report "100 plus instances … repeated on a grid" shows how badly an XR render path can fail; **no causal link is established** (INFERRED caution only). |
| 3D star field | 109,389 stars × 4 verts ≈ 437k verts / 219k tris per SPI pass. Fill ≈ 109k × (3 px)² ≈ 1 Mpx additive (INFERRED). Per-vertex A_V march adds 8–16 fetches × 4 verts × 109k per update | Per-star cached extinction, recomputed on vantage change; magnitude-banded budget as the roadmap already anticipates. |
| Labels | Atlas: ~151 label planes + 137 dot spheres, each its own draw and texture (VERIFIED count) | Batched text, 14-label cap plus declutter (`BodyLabelLayer`), no dots for constellation names. |
| Lines | 114 cyl-grid `Line` objects, 679 constellation segments, 17 orbits (atlas) | One ribbon mesh per layer. |
| Heliosphere shells | 3 additive double-sided spheres → up to 6 full-screen layers when inside | Single-sided, fade with distance, one shell at a time. |
| Galaxy sprites | 911 additive sprites up to 500 px with discard | Oriented quads with size caps; cull when below the floor with no impostor needed (the dust and stars fill the scene). |
| Two-camera `DepthBands` | Doubles culling and draw submission | Draw the additive, depth-less deep-sky layers once, in the far band or a dedicated sky pass. |
| Panel texture uploads | Atlas redraws a 512×640 RGBA canvas (1.3 MB) per hover or press change | Mesh text via `CosmoPanel`; no `Texture2D.Apply` per frame. |
| Picking | 460–489 candidates per frame is trivial; 109k is not | Spatial hash for star picking. |

---

## 9. Upstream reachability and licences (checked from triton, 2026-09-11)

| Source | URL | HEAD result | Licence (checked how) |
|---|---|---|---|
| k3d_dust snapshot | `raw.githubusercontent.com/sb2580/k3d_dust/main/index.html` | 200, 18,856,097 B | **GPL-3.0** (GitHub API); data origin "Leike & Lallement", unversioned |
| HYG v4.1 (atlas) | `…/astronexus/HYG-Database/main/hyg/CURRENT/hygdata_v41.csv` | 200, 33,932,548 B | CC BY-SA 4.0 (repo LICENSE text); repo says it moved to Codeberg |
| HYG v4.4 (CosmoSynth) | `codeberg.org/astronexus/hyg/media/branch/main/data/hyg/CURRENT/hyg_v44.csv.gz` | 200, 13,636,362 B | CC BY-SA 4.0 (`docs/data-sources.md`) |
| Skowron+2019 Cepheids | `…/jskowron/galactic_cepheids/master/data/Data_Table_1.dat` | 200, 575,072 B | LICENSE: figures CC BY-SA 3.0, code MIT, "except scientific data": **data licence unstated**, cite *Science* 365, 478 |
| LVDB (6 CSVs) | `…/apace7/local_volume_database/main/data/{gc_harris,gc_mw_new,dwarf_mw,dwarf_m31,dwarf_local_field,dwarf_local_field_distant}.csv` | all 200; 91,840 / 32,736 / 45,690 / 26,793 / 36,617 / 329,856 B | **CC0-1.0** (GitHub API); please cite Pace 2025 OJAp 8 E142 (README) |
| Stellarium modern skyculture | `…/Stellarium/stellarium/master/skycultures/modern/index.json` | 200, 205,767 B; 88 constellations | Repo GPL-2.0; skyculture `description.md` §License: **"Text and data: CC BY-SA 4.0"** |
| Leike+2020 (400 pc, 1 pc) | `zenodo.org/records/3993082` | 200 | **CC-BY-4.0** (Zenodo API); `mean_std.h5` 2.37 GB, `samples.h5` 14.2 GB |
| Edenhofer+2023 (1.25 kpc) | `zenodo.org/records/8187943` | 200 | **CC-BY-4.0** (Zenodo API); `mean_and_std_xyz.fits` 15.66 GB |
| Vergely+2022 | `cdsarc.cds.unistra.fr/ftp/J/A+A/664/A174/` | 200; cubes 40,208 / 114,294 / 227,168 KiB | CDS distribution; **licence not checked** |
| UNGC (Karachentsev+2013) | `cdsarc.cds.unistra.fr/ftp/J/AJ/145/101/` | 200; 869 galaxies | CDS; not checked |
| 2MRS v2.40 | `tdc-www.harvard.edu/2mrs/2mrs_v240.tgz` | 200, 39,501,956 B | not checked |
| JPL approx. elements | `ssd.jpl.nasa.gov/planets/approx_pos.html` | 200; Table 1 values match the atlas | NASA/JPL |

Nothing large was downloaded. I used HEAD requests, API metadata and small README/header reads.

---

## 10. Open questions for design and implementation

1. The Cepheid data licence (Skowron: "except scientific data") needs a decision: cite-only, or
   ask the authors. CC BY-SA spreads further: HYG, Stellarium figures, and the derived files.
2. Dust: one 25 pc 6 kpc cube (~1.8 MB after downsampling), or also a 10 pc inner cube?
3. Room orientation policy (§6 item 1).
4. How far "fictional content" goes in these tiers. The atlas has none; CosmoSynth's `Fictional`
   provenance class and `StarSystemFactory` already cover invented worlds.
5. Does the sky sphere stay, or does the 3D field in observed-from-Sun mode replace it? It is
   equivalent inside the Solar System (parallax is sub-pixel), except for distance-unknown stars.
6. Before building the dust layer, get a GPU-time budget for it from one headset measurement.
