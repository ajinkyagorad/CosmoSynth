# ATLAS-PARITY.md — what "entire functionality plus more" means

*Lane `parity`, 2026-09-12. This file is the checklist the owner's instruction gets gated against,
so that "did we match the WebXR atlas?" is a query and not an argument.*

The owner's instruction, verbatim: **"well expand this current unity to milky way atlast etc and
beyond, if not already, follow that webxr properly, need entire functionality plus more"**.

Three documents already answer *where* each feature should live: `docs/research/2026-09-11-milkyway-atlas-port.md`
(the Atlas read in full, with line numbers), `docs/research/2026-09-11-solar-heliosphere-port.md`
(the Heliosphere family), and `docs/design/2026-09-11-C-product.md` §10 (the traceability matrix,
225 rows, every one with a design home). None of them answers *whether it is there yet*. That is
the hole this file fills, and the only new thing in it: **every row carries a state and an owning
lane, and a claim of `present` must name a file that does it.**

---

## 0. How to read this file

### 0.1 The six columns

| Column | Rule |
|---|---|
| **Id** | Stable. `MWA-nn` / `HV2-nn` / `HV1-nn` / `VIT-nn` / `HSM-nn` reuse the ids of `docs/design/2026-09-11-C-product.md` §10 so the two files join on them. A letter suffix (`MWA-09c`) is a row this file split out of a C-product row that lumped several affordances together — the task asked for the small ones individually. `PLUS-nn` is new: what the XR build must do that the web build cannot. |
| **What the web app does** | Behaviour, not intent, with the constant where there is one. Taken from the two research reports, which read the pages in full and cite line numbers. |
| **CosmoSynth home** | The module, layer id or file that will provide it, named from the contract (`docs/CONTRACT.md`) and C-product §4.2's 46-layer table. |
| **State** | `present` · `partial` · `absent` · `dropped`. Defined below. |
| **Lane** | The lane that owns the code, from the contract's lane table. `none` only on a `dropped` row. |
| **Evidence / note** | For `present` and `partial`, the **file that does it today** — this is what makes the claim checkable, and the gate rejects the row without it. For `absent`, what is missing, or `-` where the home column already says it. For `dropped`, a reason beginning `DROP:`. |

`U/` abbreviates `unity/Assets/CosmoSynth/`. `SA/` abbreviates
`unity/Assets/StreamingAssets/CosmoSynth/`.

### 0.2 The four states

- **`present`** — a named file in the tree does this today, to the Atlas's standard or better.
  The Evidence column names it. A row is not `present` because a design document says it will be.
- **`partial`** — something in the tree does part of it, and the Evidence column says which part
  and what is missing. Most of the "we already have that" instincts about this project land here:
  `WristDashboard` is a wrist panel but has no tabs, no touch and no PIN; `SkyAndRings` draws
  stars but with the distance deliberately discarded.
- **`absent`** — nothing in the tree does it. 235 bodies, 24 spacecraft, 180 exoplanet records and
  9,000 direction-only stars is what exists; everything past 130 light years is in this state.
- **`dropped`** — deliberately not carried over. Allowed only for: fabricated data presented as
  real, third-party IP, runtime network use, page styling, or a 2D editor that is not a function
  of the universe — the same five grounds C-product §10 allows. Every drop names the honest
  equivalent the owner gets instead.

### 0.3 Lanes

`p0-core` · `p0-shaders` · `stars` · `milkyway` · `galaxies` · `cmb` · `solar` · `exoplanets` ·
`fiction` · `nav` · `ui` · `lines` · `audio` · `integrator` · `parity` · `none`.

These are exactly the lanes of `docs/CONTRACT-SUMMARY.md` "Lanes and what each owns", plus
`integrator` (everything the lane table does not assign), `parity` (this file and its gate) and
`none` (dropped rows only). `tools/gates/parity_gate.py` holds the same list and rejects anything
else, so a row cannot be parked on a lane that does not exist.

### 0.4 What the gate enforces

`tools/gates/parity_gate.py`, wired into `tools/gate-all.sh` as **G0i**, reads every six-column
row in this file whose first cell is an id, and fails if a row is **unaccounted for**:

1. a duplicate or malformed id;
2. an empty "what it does" or "home" cell, or an empty evidence cell on a row that claims
   something (an `absent` row may leave it `-`);
3. a state outside the four above;
4. a lane outside the list above, or `none` on a row that is not `dropped`;
5. **a `present` or `partial` claim whose evidence names no file** — the rule that stops this
   document drifting into optimism;
6. `lane none` or a `DROP:` reason on a row that is not `dropped`, or a `dropped` row whose
   evidence does not begin `DROP:`.

`absent` is an accounted-for state, so the gate is green today with a large absent count printed.
`--require-parity` additionally fails while any row is `absent`; that is the flag `gate-all.sh`
switches to at the end of Phase 2, and it is what "entire functionality" finally means as a
command that exits non-zero.

### 0.5 The one thing this file is not

It is not a schedule and it does not grant anyone permission to mark a row `present`. A row moves
to `present` when the lane's own report names the file and the file is on master. §4 recommends an
order; the contract's phases decide it.

---

## 1. The parity table

### 1.1 Milky Way Atlas (`refs/webxr/nebula-gallery/index.html`, 1,550 lines + 4.72 MB of data)

**Frame, scale and session**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| MWA-01 | Galactic Cartesian parsecs, Sun at origin, `model.rotation.x = -pi/2` puts the galactic plane flat on the floor; equatorial to galactic by the Hipparcos matrix | data stay ICRF; `StandardFrames.EquatorialToGalactic`; galactic-up becomes `PresentationPlane.Galactic` on the galactic rungs | partial | p0-core | `U/Core/Frames/StandardFrames.cs` has the rotation but **all three signs are flipped** — it sends the north galactic pole to (-0.346, 0.176, 0.922) instead of (0,0,1). Fix in flight on `lane/foundation-code`. `Core/Presentation/PresentationPlane.cs` does not exist |
| MWA-02 | One scaled group, metres per parsec, `logScale` slider -7.3 ... +5.7 | `FloatingOrigin.UnitsPerKm` with `ScaleLimits` -24.70 ... +3.00 (a superset) | partial | p0-core | `U/Runtime/Universe/FloatingOrigin.cs` holds the double origin and units; `U/Runtime/XR/TabletopManipulator.cs` clamps UnitsPerKm to 1e-20 ... 1e6, which stops ~2 orders short of the 17.8 Mpc Local Volume. `Core/Presentation/ScaleLimits.cs` does not exist |
| MWA-03 | Every transform eases toward a target, `k = 1 - exp(-6.5 dt)`, `dt <= 0.05` | log-space scale smoothing and damped pose | present | nav | `U/Runtime/XR/TabletopManipulator.cs` smooths log scale at `1 - 0.5^(60 dt)`, the same shape with a different rate |
| MWA-04 | 11 parallel fetches behind a progress bar; a `file://` open fails with an explanatory message naming the URL and its status | status card with a row per dataset; failure-visible layer rows (C-product §9.2) | absent | ui | `U/Runtime/CosmoSynthBootstrap.cs` loads catalogues with no progress surface and no per-file failure row |
| MWA-05 | Two session buttons: VR (`immersive-vr`, black clear) and MR (`immersive-ar`, clear alpha 0) | one session, ROOM slider, Passthrough / Space presets | partial | nav | `U/Runtime/XR/XrRig.cs` has `SetPassthrough`, called once from `U/Runtime/CosmoSynthBootstrap.cs`. Instant, no fade, no user control |
| MWA-06 | `xr.setFoveation(0.7)`; XR start pose (0, 1.25, -1.0); dust steps capped at 72 in XR | SRP foveation on URP; first-run placement; a quality governor that owns every step budget | absent | p0-shaders | Built-in RP cannot enable foveation (`CLAUDE.md`); the contract's move to URP 17.5 is what unlocks it. No governor exists |
| MWA-07 | `window.__dbg` exposes measure, nameCache, model, solarGroup, goTo, DESTS, obs, lim, cyl, namesVisible for headless checks | play-mode harness actions and report fields | partial | integrator | `U/Editor/PlayModeShot.cs` takes captures and probes destination alpha, but exposes no layer or tool state to a headless check |
| MWA-08 | Links the hosting site's `/css/lumen-experience.css` and sets `class="lumen-experience"` | - | dropped | none | DROP: web page styling with no function in XR. Equivalent: `U/Runtime/UI/CosmoTheme.cs` |
| MWA-55 | Camera near 0.02 m, far 5e8 m, fov 60; no depth banding, because everything deep is additive with depthWrite off | one camera, symmetric projection, the `f(d) = d0(1 + ln(d/d0))` distance map applied per group | absent | p0-core | `U/Runtime/Rendering/DepthBands.cs` runs two cameras today; the contract deletes it. The distance map does not exist |

**Dust volume**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| MWA-09 | Raymarched 201x201x81 uint8 log-encoded density cube over 2000x2000x800 pc, BackSide unit box, front-to-back accumulation | `dust` layer from Vergely 2022 (nmag/pc), stepping in pc with `a = 1 - exp(-kappa rho ds)`, premultiplied blend | absent | milkyway | Nothing volumetric exists. `BodyType.Nebula` is an unused enum value in `U/Core/Catalog/CelestialBody.cs` |
| MWA-09a | DENSITY slider 0.1 ... 6, default 1.6 | `dust.density` LayerParam, 1 = physical opacity, raises a `DUST xN` chip | absent | milkyway | no LayerParam system exists |
| MWA-09b | THRESHOLD slider 0 ... 0.7, default 0.22, as `smoothstep(uCut, 1, s)` on the log byte | `dust.threshold` LayerParam; the cut is stated in INFO rather than chipped | absent | milkyway | - |
| MWA-09c | QUALITY slider 24 ... 220 steps, forced to <= 72 in XR | step budget from the quality governor, contract ceiling 33 step-coverages per frame | absent | p0-shaders | - |
| MWA-09d | Four-stop colour ramp on log density: blue-black, teal, orange, cream | the same ramp as an authored transfer function, provenance Procedural | absent | milkyway | - |
| MWA-09e | Static per-pixel hash jitter of the first sample; early-out at alpha > 0.985; `discard` below 0.003 | blue-noise jitter, the same early-out, plus empty-space skipping from a coarse max-mip | absent | milkyway | - |
| MWA-10 | Cube edges drawn as an additive `EdgesGeometry` at opacity 0.6 — where the measurement stops | `dustExtent` guide layer | absent | lines | - |

**Stars**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| MWA-11 | 60,000 HYG stars at measured 3D positions, `psize = L^0.28` from `absM = mag - 5 log10(d/10)` | `stars` layer over ~120k AT-HYG rows at their measured distances | partial | stars | `U/Runtime/Rendering/SkyAndRings.cs` draws 9,000 stars on a 900-unit sky shell and its own comment says "Distance is deliberately discarded here". `U/Core/Sky/StarCatalog.cs` already holds the distances it discards |
| MWA-11a | Gaussian point sprite `exp(-4.5 r^2)`, `gl_PointSize = clamp(s, 1.2, 20)`, brightness carried in alpha | quad billboards with a >= 2.5 px floor, brightness in RGB, halo and spikes at the bright end | partial | stars | `U/Runtime/Rendering/StarSprite.cs` and `U/Runtime/Rendering/StarLook.cs` already put brightness in RGB and add spikes — better than the Atlas. The 1.2 px floor is the "square patches" defect the owner reported in 1.7.0 and must not be copied |
| MWA-11b | Stars sit at their measured parsec positions inside the model, so moving through the model gives real parallax and the constellations shear apart | the same, with the vertex placed at `min(len(rel), R_sky)` along `rel` to stay inside the far plane | absent | stars | this is the single biggest absent row in the file: today there is no 3D star volume at all, only a direction shell |
| MWA-12 | B-V to temperature (Ballesteros 2012), temperature to RGB (Tanner Helland fit) | temperature already stored per star; `PlanetGenerator.BlackBodyColor(T)` | present | stars | `U/Core/Sky/StarCatalog.cs` stores T from the same Ballesteros formula; `U/Core/Procedural/PlanetGenerator.cs` converts it. The Helland fit is not needed |
| MWA-13 | The Sun appended to the star buffer at (0,0,0), M = 4.83, B-V 0.65 — an ordinary G star, no oversized ball | the Sun drawn once: body above log10 U = -13.5, star point below | absent | stars | `U/Runtime/Universe/CelestialBodyView.cs` draws the Sun as a body at every scale; no star-point handover exists |
| MWA-14 | OBSERVED SKY: `m = M + 5 log10(D/10 pc)` recomputed every frame from the eye, `b = 10^(0.4(lim - m))`, stars below the limit vanish | `TOOLS -> OBSERVED SKY` with the maths in `Core/Sky/Photometry.cs` | absent | stars | `Core/Sky/Photometry.cs` does not exist; there is no observed mode |
| MWA-14a | Vantage is the camera's model-space position ("from here"); an earlier build offered Sun / here as a pair | explicit vantage: Sun, viewer, or the selected object, named on screen | absent | stars | the Atlas's own defect: at tabletop scale the "observed sky" is computed from kiloparsecs outside the data |
| MWA-14b | LIMIT MAG slider 3 ... 14, default 6.5 (naked eye), presets for binoculars and telescope | the same slider; the honest ceiling is the catalogue's own magnitude limit, stated in INFO | absent | stars | the Atlas's data stops at mag 8.54, so its 12-14 settings reveal nothing; CosmoSynth's catalogue must state its own floor |
| MWA-14c | Eye compression `s = 1.15 b^0.23 * expo`; point size forced to 0 below the limit | the same exponent, as a tested Core function | absent | stars | - |
| MWA-14d | Observed mode applies to stars, Cepheids and globulars alike (from the Sun at 6.5, almost no Cepheid is visible, which is the point) | the same three layers share one photometry path | absent | stars | - |
| MWA-27a | `skyBoost = max(1, (s/1.5e-3)^0.9)` applied to stars only, so the sky stays readable when you zoom in | the same automatic assist inside the star renderer | absent | stars | - |
| MWA-27b | `farAssist = min(6, (1.5e-3/s)^0.24)` for stars, Cepheids and globulars when zoomed out | the same, capped | absent | stars | the Atlas's own README claim that "the flux-true look at natural scales is untouched" is false: farAssist is already 1.17x at the default preset |

**Deep-sky populations**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| MWA-15 | 2,214 Skowron Cepheids as points, colour lerped young blue to old amber by `log10(age/20)/log10(20)` | `cepheids` layer from the same catalogue, keeping period, mode and dDIST | absent | milkyway | nothing; `grep -ril cepheid U/` is empty. The Atlas draws every Cepheid at M = -4.2 regardless of period — the port derives M from a Leavitt law |
| MWA-16 | 213 globular clusters as points, colour `#ffe3b0`, `psize` from M_V | `globulars` layer with LVDB `M_V` and `rhalf`, names kept, point-to-resolved-sprite LOD | absent | milkyway | nothing; the Atlas keeps only five names, and computes M_V without extinction |
| MWA-17 | 911 Local Volume galaxies as oriented sprites drawn at measured half-light diameter, ellipticity and position angle | `galaxiesLocal`: oriented discs in the sky frame from PA and axis ratio | absent | galaxies | nothing. `BodyType.Galaxy` is an unused enum value in `U/Core/Catalog/CelestialBody.cs`; the word Galaxy appears nowhere else in the runtime |
| MWA-17a | GAL GLOW slider 0.5 ... 30, which also raises the minimum galaxy dot size | `galaxiesLocal.glow` LayerParam with a `GALAXY GLOW xN` chip | absent | galaxies | - |
| MWA-17b | Fragment profile `0.62 exp(-9 rr) + 0.38 exp(-1.7 rr)` — a concentrated core in a faint envelope | the same profile on an oriented quad | absent | galaxies | - |
| MWA-17c | Colour graded by luminosity, `t = (-MV - 8)/12` | the same, with defaulted fields drawn round and faint | absent | galaxies | 542 of 911 Atlas ellipticities and 689 of 911 position angles are defaults drawn as if measured — the port flags them |
| MWA-19 | All 88 IAU constellation figures as 679 real HIP-to-HIP segments in true 3D | `constellations` layer from csstars index pairs, one ribbon mesh | absent | stars | nothing; the two hits for "constellation" in the tree are both comments (`U/Runtime/Rendering/SkyAndRings.cs:128`, `U/Runtime/Universe/CelestialBodyView.cs:309`) |
| MWA-19a | Figure names at the 3D mean of the figure's endpoints, with a dot there | names at the direction of the figure's brightest star, no dot (the centroid is not a star) | absent | stars | - |
| MWA-19b | CONSTELL. toggle, off by default; labels gated to 2e-5 < s < 40 | `constellations` LayerDef with its scale window | absent | ui | - |
| MWA-18 | 9 molecular cloud markers from published (l, b, d): Orion 414 pc, Taurus 140, Ophiuchus 140, Perseus 300, California 470, Cepheus 350, Chamaeleon 180, Aquila Rift 250, Sun | `cloudMarkers` layer with a **reference per entry**, which the Atlas has not got | absent | milkyway | - |
| MWA-18a | 7 Milky Way landmarks: Sgr A* at [8178, 0, -14] pc, the Sun, and 47 Tuc / omega Cen / M4 / M13 / M15 | the same as `PlaceCatalog` rows, with citations | absent | milkyway | - |
| MWA-18b | 13 Local Group landmarks: LMC, SMC, Sgr dSph, Fornax, Sculptor, Leo I, NGC 6822, M31, M33, IC 10, WLM, Draco, Sextans | derived from the galaxy catalogue as a landmark flag | absent | galaxies | - |
| MWA-18c | Four marker-category toggles MK.CLOUD / MK.MW / MK.LGRP / MK.SOLAR | label categories Clouds, MilkyWay, LocalGroup, SolarLandmarks | absent | ui | - |
| MWA-18d | Each marker category has its own scale window (clouds 2e-5..40; MW 2e-7..4e-4; LG below 5e-6) | C-product §4.3's label-category windows in log10 U | absent | ui | `U/Runtime/UI/BodyLabelLayer.cs` has one category and no windows |

**Solar System and heliosphere**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| MWA-21 | 8 planets + Pluto from JPL Standish Table 1, Kepler solved by 20 Newton steps, epoch frozen at page load | bodies from Horizons, animated on the simulation clock | present | solar | `SA/catalog/solar_system.json` (31 bodies), `U/Core/Orbits/OrbitalElements.cs`, `U/Runtime/Simulation/SimulationClock.cs`. Validated under a milliarcsecond against 260 Horizons vectors — better than the Atlas, and it moves |
| MWA-21a | Ceres, Vesta, Eris, Makemake, Haumea on osculating elements | the same five in the catalogue | partial | solar | `SA/catalog/solar_system.json` has Ceres, Vesta, Pallas, Eros, Bennu; Eris, Makemake and Haumea are absent |
| MWA-21b | Comets 1P/Halley, Hale-Bopp and 67P with full orbit ellipses | the same three | partial | solar | `SA/catalog/solar_system.json` has 1P/Halley; Hale-Bopp and 67P are absent |
| MWA-21c | Lambert-shaded spheres at exaggerated radii (Jupiter 0.42 AU, Earth 0.16 AU), labelled as exaggerated | `ScaleSpace.PresentationRadius` power law plus SIZES presets, with the warning drawn **in the headset** | partial | solar | `U/Core/Scale/ScaleSpace.cs` compresses sizes at power 0.55; the "SIZES NOT TO SCALE" warning exists only in the desktop IMGUI at `U/Runtime/UI/ProvenanceHud.cs` |
| MWA-21d | Sun as an emissive 0.10 AU sphere plus a decay-free point light and a blue ambient | the real Sun body and its light | present | solar | `U/Runtime/Universe/CelestialBodyView.cs`, `U/Runtime/CosmoSynthBootstrap.cs` |
| MWA-21e | Asteroid belt drawn as an ecliptic annulus 2.1 ... 3.3 AU at opacity 0.07, with a label | `belts` layer from real SBDB elements on a GPU Kepler point cloud — the measured Kirkwood gaps instead of a flat ring | absent | solar | `grep -ril "kuiper\|asteroid belt" U/` is empty |
| MWA-21f | Kuiper belt as an annulus 30 ... 50 AU at opacity 0.05, with a label | the same `belts` layer, classical / plutino / scattered from real elements | absent | solar | - |
| MWA-21g | A comet dust tail drawn as a fixed 0.8 AU line anti-sunward, at any heliocentric distance | `comets` layer: tails only within ~5 AU of the Sun, length growing as r falls; ion tail anti-sunward, dust tail curved along the orbit | absent | solar | the Atlas gives Halley a tail at 35 AU |
| MWA-21h | Orbit ellipses of 181 points, additive, opacity 0.45 planets / 0.22 minor / 0.30 comets | `orbits` layer, drawn only from outside and above 8 px | present | lines | `U/Runtime/Rendering/OrbitLineRenderer.cs`, which already enforces both rules the Atlas lacks |
| MWA-21i | The Solar System group loads by LOD above ~40 m/pc, and (since v3) renders continuously at every zoom | `ScaleSpace` representation levels Point / Impostor / Sphere / Terrain / Tiles / Local | present | solar | `U/Core/Scale/ScaleSpace.cs` |
| MWA-22 | Termination shock as a 90 AU fresnel-glow sphere | `heliosphere` layer: an ellipsoid through the two measured Voyager crossings (V1 ~94 AU 2004, V2 ~84 AU 2007) | absent | solar | `grep -ril heliosphere U/` is empty. The web page family is *called* Heliosphere and only this one file draws one |
| MWA-22a | Heliopause as a 120 AU sphere scaled (1, 1, 1.9) and offset -55 AU downwind | a Rankine half-body `r(theta) = r_n sqrt(2(1+cos theta))/sin theta`, pinned at V1 121.6 AU and V2 ~119 AU, with "tail length unknown" in the provenance | absent | solar | - |
| MWA-22b | Bow wave as a partial 230 AU sphere (theta <= 0.75 rad), labelled "no shock", per IBEX 2012 | the same shell and the same honest label | absent | solar | - |
| MWA-22c | Fresnel rim shader `rim = (1 - abs(N.V))^2.6`, additive, DoubleSide | one `CosmoSynth/*` URP shell shader, single-sided when the viewer is inside, `ColorMask RGB` so it never writes passthrough coverage | absent | p0-shaders | `U/Runtime/Rendering/AtmosphereShell.cs` has a fresnel shell for atmospheres; the heliosphere needs its own, at 10 orders of magnitude more radius |
| MWA-22d | Nose direction taken as galactic l = 5 deg, b = +19 deg | the same axis, cited to the IBEX inflow direction | absent | solar | - |
| MWA-23 | Saturn's ring placed by `ring.quaternion = eclQ` then `rotateX(26.7 deg)`, which gives the wrong node | real ring radii on the IAU pole | present | solar | `U/Runtime/Rendering/RingSystem.cs` with poles from `U/Core/Frames/BodyOrientation.cs` (WGCCRE 2015, 20 bodies). Already better |

**Grids and guides**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| MWA-24 | Plane grids: the dust cube's edges, a 2 kpc grid in 100 pc cells, and a 60 kpc grid in 5 kpc cells centred on the Galactic Centre | `planeGrid` guide, one ribbon mesh | absent | lines | no grid renderer exists |
| MWA-24a | PLANE toggle, and a `cloudZone` test (3e-5 < s < 40) that chooses which grid is live | `planeGrid` LayerDef with its scale window | absent | ui | - |
| MWA-25 | Stellar cylindrical grid: rings at 250 / 500 / 750 / 1000 pc about the Sun, half-height 300 pc | `cylGrids` stellar regime, one ribbon mesh | absent | lines | - |
| MWA-25a | Galactic cylindrical grid: rings at 5 / 10 / 15 / 20 / 26 kpc about Sgr A*, half-height 1.2 kpc | the same, plus a **real** ring at R0 = 8.15 kpc | absent | lines | the Atlas labels its 26 kpc ring "Sun orbit R~8.2 kpc"; there is no 8.2 kpc ring in it at all |
| MWA-25b | Local Group cylindrical grid: rings at 300 kpc / 1 / 3 / 8 / 16 Mpc, half-height 2.5 Mpc | the same | absent | lines | - |
| MWA-25c | Each ring carries a small distance tag in both units — "250 pc . 815 ly", "1 Mpc . 3.3 Mly" | the same tags through the Guides label category, sizes in dmm | absent | lines | - |
| MWA-25d | Each regime is a true cylinder: rings at z = -halfH, 0, +halfH (mid opacity 0.55, outer 0.22), 12 mid-plane spokes and 12 rim verticals — 114 separate `Line` objects in all | one ribbon mesh per regime, one draw call each | absent | lines | the Atlas's 114 line objects are exactly the draw-call cost the port must not copy |
| MWA-25e | CYL GRID toggle — but it only works while PLANE is also on | independent toggles | absent | ui | the Atlas's `gridOn && cylOn` conjunction is a defect |
| MWA-46 | Scale readout: cm per AU above 40 m/pc, otherwise the room span of the 2 kpc cube | header readout "1 m = X" across the whole ladder | absent | ui | `U/Runtime/UI/WristDashboard.cs` prints a rung name and a distance, not a scale ratio |

**Labels**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| MWA-26 | Label height `clamp(0.085/s, min, cap)` — a fixed 8.5 cm in the room, per-category min and cap | sizes in dmm through `CosmoTheme` (Caption 19, Body 24 dmm), which is an angular rule | partial | ui | `U/Runtime/UI/BodyLabelLayer.cs` ranks, caps at 14 labels and declutters greedily with pooled text; `U/Runtime/UI/CosmoTheme.cs` holds the dmm scale. Missing: categories, scale windows, and the per-category caps |
| MWA-26a | Crowd shrink: `n` = labels within 2.4 h0, then `h = h0/(1 + 0.42 sqrt(n))`, so crowded regions shrink rather than overlap | the same shrink as a softer first stage before dropping | absent | ui | `BodyLabelLayer` drops crowded labels; it never shrinks them. The README's "1/sqrt(n)" is a simplification of the real rule above |
| MWA-26b | `orientLabels` runs `lookAt(cameraWorldPos)` on every label every frame; the default up keeps them upright in the room | the same per-label look-at | present | ui | `U/Runtime/UI/BodyLabelLayer.cs` |
| MWA-26c | FACE TEXT toggle freezes labels in 3D space like signage instead of billboarding | `FACE TEXT / FROZEN` in the panel | absent | ui | - |
| MWA-26d | LABELS master toggle | LABELS master, above the per-category toggles | partial | ui | `U/Runtime/UI/BodyLabelLayer.cs` can be disabled as a component; there is no user control |
| MWA-26e | Labels are plain outlined 3D type — no boxes — IBM Plex Mono 600 at 46 px with a 7 px dark outline | mesh text with an outline, one font material, batched | present | ui | `U/Runtime/UI/CosmoText.cs` |
| MWA-26f | A 4 mm dot marks each labelled object (137 dot spheres in all) | a dot only where the object is not itself drawn; never on a constellation centroid | absent | ui | - |
| MWA-20 | Hover names: a ray from the mouse or the right controller ranks the 460 named stars by angular offset (< 0.05 rad) and shows the nearest five, so labels never clog the view | hover names over the index-linked 548 names in `sky/stars_named.json`, with a coarse spatial hash for the other ~120k | absent | ui | `U/Runtime/XR/BodySelector.cs` picks bodies, not stars |
| MWA-20a | STAR NAMES toggle | `StarNames` label category toggle | absent | ui | - |
| MWA-51 | Credits line naming every dataset and its source | INFO -> SOURCES, per layer, with the licence tag | partial | ui | `U/Runtime/UI/ProvenanceHud.cs` shows per-field provenance glyphs for the selected body — better per object, absent per layer |
| MWA-52 | Title and subtitle: "Milky Way Atlas / All objects at **measured** 3D positions" | the app's own identity line plus INFO -> THIS VIEW | absent | ui | - |
| MWA-47 | The README's "Honest limits" section: no whole-Galaxy dust cube exists, no 3D dark matter, no measured spiral arms, no intergalactic dust | INFO -> THIS VIEW, "What we do not know here", per rung | absent | ui | the strongest idea in the Atlas and the one most worth beating: it belongs on screen, not in a README |

**Tools**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| MWA-28 | MEASURE mode: pick two objects, get a cyan line with the separation | `TOOLS -> MEASURE`, a mode of `BodySelector` | absent | ui | `U/Runtime/XR/BodySelector.cs` selects one body; there is no measure mode |
| MWA-28a | Label format "518 pc . 1690 ly" (ly = pc x 3.26156) with k and M prefixes | the same, extended to km / AU / ly / pc / Mpc by magnitude | absent | ui | `U/Runtime/UI/WristDashboard.cs` has a distance formatter; it does not do pc or Mpc |
| MWA-28b | A second line naming both ends, "Sun -> California Neb." | the same | absent | ui | - |
| MWA-28c | A third pick starts a new measurement | the same | absent | ui | - |
| MWA-28d | With MEASURE on, a pinch never grabs the map | per-hand arbitration: the picking hand does not grab, the other still can | absent | nav | the Atlas disables grab on **both** hands, which is heavier than it needs to be |
| MWA-28e | 489 pickables: 460 named stars + 9 cloud + 7 MW + 13 LG markers. Galaxies, Cepheids, globulars and planets are **not** pickable | anything selectable is measurable, bodies and galaxies included | absent | ui | - |

**Navigation**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| MWA-29 | Six GO TO destinations: Solar system, Orion clouds, Sgr A*, omega Cen, LMC, M31, each with its own target scale | `Destination` rows, all six plus every catalogue object | partial | nav | `U/Runtime/UI/WristDashboard.cs` lists catalogue bodies nearest-first; not one of the six deep-sky destinations exists, because none of the objects does |
| MWA-30 | Travel, not teleport: yaw the map so the target lies ahead, then stream space toward you on `smootherstep`, scale morphing en route, duration `2.6 + 0.5 x decades-of-scale` s | `Core/Nav/TravelPlan.cs`, a pure function of t returning focus, log scale and yaw | absent | nav | `U/Runtime/XR/UniverseNavigator.cs` has `TravelTo`, and `U/Runtime/UI/WristDashboard.cs` activates a rung instantly. No yaw-first, no eased profile |
| MWA-30a | Grabbing cancels the flight | any grab or scale input cancels travel | absent | nav | the Atlas's own defect is the opposite: a scale **preset** does not cancel travel, so the flight overwrites it on the next frame |
| MWA-30b | Yaw finishes in the first 35 % of the flight (`smootherstep(min(1, u/0.35))`), so you are looking the right way before you move | the same, as a tested endpoint condition | absent | nav | - |
| MWA-31 | Arrival crosshair: 8 cyan corner ticks along the cube diagonals from 0.55R to R, R = 7.5 cm in the room, additive, depth-test off | `ArrivalCrosshair`, the same construction, sized in dmm | absent | nav | - |
| MWA-31a | The destination's name sits at the crosshair and fades after ~6 s over 1.2 s | the same, with the timer started **at arrival** | absent | nav | the Atlas starts the timer at departure, so any flight longer than 6 s begins fading the moment you get there |
| MWA-32 | Three scale presets: Clouds (2 kpc), Galaxy (40 kpc), Local Group (2 Mpc) | rungs 11, 12 and 14 of the 19-rung ladder | partial | nav | `U/Runtime/UI/WristDashboard.cs` has 7 rungs, the largest "Solar neighbourhood" at 2 x 130 ly. The Atlas's smallest of the three is 2 kpc — 50x further out than anything CosmoSynth can show |
| MWA-41 | One-hand grab is full 6-DoF: `m = pose(t) pose(0)^-1 model(0)`, so you can tilt the galaxy off the floor | one-hand grab translates; free tilt is an opt-in tool | partial | nav | `U/Runtime/XR/TabletopManipulator.cs` has `HandPose` with a rotation field but applies no rotation to the model |
| MWA-42 | Two-hand stretch-scale `k = clamp(d/d0, 0.02, 50)` with a yaw twist from the inter-hand vector, midpoint held fixed | two-hand log scale and yaw twist, clamped only by `ScaleLimits` | partial | nav | `U/Runtime/XR/TabletopManipulator.cs` does the scale better (it pivots on the focus body, which fixed the 1.4.0 "can't go close" complaint). Missing: the twist. The Atlas's own scale clamp of 10^0.5 m/pc snaps you out of the Solar System, which sits at 10^4.55 |
| MWA-43 | Sticks: left translates at `1.6 max(300 s, 0.25)` m/s; right X yaws at 1.4 rad/s; right Y changes scale by 0.55 decades/s; deadzone 0.12 | L stick 3D fly, R stick X turn, R stick Y zoom | partial | nav | `U/Runtime/XR/XrInput.cs` binds "L stick: fly" and "R stick: rise . turn"; there is no stick zoom, and `U/Core/Scale/ScaleSpace.cs` already sets fly speed from distance |
| MWA-50 | An on-screen legend of every control, for desktop, controllers and hands | the binding legend, generated from the binding table so it cannot go stale | present | nav | `U/Runtime/XR/XrInput.cs` `Bindings` is the data the legend is drawn from; its own comment records that the previous hand-written legend was already wrong |
| MWA-44 | Desktop: OrbitControls with damping, plus a HUD of 3 presets, 6 destinations, 7 sliders and 14 toggles | the same panel, pinned and mouse-driven on desktop; every slider is a LayerParam and every toggle a layer, label or tool | partial | ui | `U/Runtime/XR/DesktopInputSource.cs` gives WASD fly and a few keys; `U/Runtime/UI/ProvenanceHud.cs` is an IMGUI read-out, not a control surface |

**The wrist panel**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| MWA-34 | A glowing watch face at the left wrist joint (or left grip), 19 mm disc in a 21-27 mm ring, always facing you | the same watch face | partial | ui | `U/Runtime/UI/WristDashboard.cs` follows the left controller pose and opens on X; there is no watch face, and with pure hand tracking `FollowWrist` calls `Dismiss()` because `XrInput.Read` only queries controller devices |
| MWA-34a | Open it by touching the watch with the right index tip within **5 cm**, debounced **800 ms**; or point the controller ray at it and pull the trigger | the same two ways | absent | ui | - |
| MWA-35 | Panel anchored 10 cm above and 10 cm beyond the wrist, always upright (zero roll, zero pitch), position damped at 7 s^-1 behind a **6 mm deadband**, yaw lazily at 4 s^-1 past a **20 deg** deadband | the same constants, carried over verbatim | partial | ui | `U/Runtime/UI/WristDashboard.cs` follows the wrist and damps; it has no deadbands, and no zero-roll rule |
| MWA-36 | PIN detaches the panel as a fixed panel in space; tapping the watch recalls it | PIN / recall | absent | ui | - |
| MWA-37 | Fingertip touch: press fires only when the tip crosses the surface by **6 mm**, releases past **20 mm** (hysteresis), buttons debounced **300 ms** | the same, plus an approach rule so a tip entering the volume from behind cannot fire on entry | absent | ui | `U/Runtime/XR/XrHandInput.cs` has pinch hysteresis (22/35 mm) and tap detection, but no panel-surface touch |
| MWA-37a | Failing a fingertip, the right controller ray drives the panel if it hits within **1.2 m** | the same fallback | absent | ui | - |
| MWA-37b | Either pointer sets `panelHot`, which suppresses right-hand pinch-grab so menu use cannot grab the map | the same, declared as an explicit claim like `ClaimNavigationStick` | absent | nav | - |
| MWA-37c | A 6 mm cursor dot marks exactly where the fingertip or ray touches the panel | the same cursor | absent | ui | - |
| MWA-37d | Hovered button highlights in the category colour with a white stroke; pressed flashes white for 200 ms | hover and press states in `CosmoTheme` accent, not category colours | absent | ui | `U/Runtime/UI/CosmoPanel.cs` draws panels but has no button states |
| MWA-38 | Three tabs VIEW / LAYERS / TOOLS plus PIN, on a persistent top row | tabs GO / LAYERS / TIME / TOOLS / INFO, generated from tables | absent | ui | `U/Runtime/UI/WristDashboard.cs` has two pages (SCALE, DESTINATIONS) toggled by a button, not tabs |
| MWA-38a | Section captions state what the group does — "GO TO - orient & travel there (spaceship)" against "DATASETS - show / hide" — so travel is visibly not the same thing as visibility | the same captions, because the distinction is the point | absent | ui | - |
| MWA-38b | Sliders live in the panel: tap anywhere on the track, or drag while pressed | the same, drag only while pressed | absent | ui | - |
| MWA-39 | Category colours: nav violet, preset cyan, layer green, marker amber, tool pink, sky blue, sys white | - | dropped | none | DROP: green, amber and pink are already `CosmoTheme.Provenance` Measured / Procedural / Fictional. Equivalent: captions and position carry the category, accent carries the control |
| MWA-40 | Hands drawn in a translucent additive blue tint so they are visible against space (the README calls it "starry"; it is a flat tint) | real hands in passthrough; a neutral translucent hand mesh only in Space mode | partial | nav | `U/Runtime/XR/XrHandInput.cs` reads joints; nothing renders a hand |
| MWA-33 | Six per-dataset toggles: DUST, STARS, CEPHEIDS, GLOBULAR, GALAXIES, SOLAR SYS | `LayerTable.Layers`, 46 rows, one source for the panel and the legend | absent | ui | - |
| MWA-33a | SOLO row: S.DUST / S.STARS / S.GALAX / S.SOLAR isolate one dataset, ALL restores. S.STARS also covers Cepheids, globulars and constellation lines | SOLO over the layer table: Solar, Stars, Dust, Galaxies, Beyond | absent | ui | - |
| MWA-33b | SOLO never hides markers, labels or grids — you can still read where you are | the same rule, stated in the layer table | absent | ui | - |
| MWA-33c | A zoom-gated visibility table decides what is drawn at each scale, so nothing saturates anything else | per-layer scale windows in log10 U, C-product §4.2 | absent | ui | - |
| MWA-45 | Soft tick on hover (1500 Hz, 30 ms), click on press (880 to 620 Hz, 70 ms), from a WebAudio oscillator | two procedurally generated clips on one 2D source | absent | audio | `grep -ril audiosource U/` is empty |
| MWA-45a | Right-controller haptics: hover pulse (0.22, 10 ms), press (0.7, 40 ms), watch toggle (0.8, 50 ms) | `XrInput.Pulse` on either hand | present | nav | `U/Runtime/XR/XrInput.cs` |
| MWA-27 | EXPOSURE slider 0 ... 3 (HUD) or 0.1 ... 3 (panel), default 1 | `stars.exposure` LayerParam | absent | ui | - |
| MWA-27c | Brightness assists are applied silently; the README calls them "honestly artificial" but nothing on screen says one is active | a `BRIGHTNESS xN` chip whenever the effective gain is not 1 | absent | ui | this is the row where CosmoSynth beats the Atlas at the Atlas's own stated ethic |
| MWA-53 | Every grid, orbit, frame and constellation line renders additively, for a glowing space-appropriate look | additive with `ColorMask RGB`, so nothing writes passthrough coverage | partial | p0-shaders | `U/Runtime/Rendering/StarSprite.cs` already blends additively without writing destination alpha — the rule exists, but only for stars |
| MWA-54 | Render order is explicit: stars 1, constellations 2, dust 10, labels 20, crosshair 25, watch 30, panel 31 | one declared draw order per layer in the layer table | absent | p0-shaders | the Atlas draws dust **after** stars, so clouds tint stars that are in front of them. The port replaces the ordering hack with real per-star extinction |
| MWA-48 | Ships 11 data files totalling 4.72 MB, of which the dust cube comes from a GPL-3.0 snapshot in unstated units and two files cannot be reproduced by the committed extractor | every dataset re-derived by a `tools/cosmodata/` importer into a documented container under `SA/`, listed by `manifest.json` | partial | integrator | `SA/catalog`, `SA/models`, `SA/sky` and `SA/surfaces` exist (6.8 MB); galaxy, lv, lss, cmb and sun do not. `tools/cosmodata/build_starfield.py` and the solar importers are all that is written |

### 1.2 Heliosphere V2 (`refs/webxr/pages/solar-xr2.html`, 2,103 lines), with V1 and Vitals

The owner's directory calls these three pages the "Heliosphere" family. The name is a promise none
of them keeps: **no page in the family draws a heliosphere** — the only heliosphere model in the
whole reference set is the Atlas's three shells (MWA-22 above). V2 is nonetheless the most complete
real-scale Solar System the owner has built, and CosmoSynth is already better than it on the hard
parts (double precision, a floating origin, Horizons ephemerides, honest spacecraft windows) and
worse than it on almost everything a person actually touches.

**Time and ephemerides**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| HV2-01 | Time starts from the device clock; the rate may be **negative**; pause; a "Now" button resets to real time | signed `WarpLadder`, pause, Now | partial | nav | `U/Runtime/Simulation/SimulationClock.cs` has 15 forward rates (0 to 1 Myr/s) and **no negative entry**; `SetEpochToNow` is reachable only from the desktop "N" key in `U/Runtime/XR/DesktopInputSource.cs` |
| HV2-02 | Eleven-rung rate ladder from -10 yr/s to +10 yr/s, labelled "real time", "hr/s", "d/s", "yr/s"; the date shown as ISO UTC | the same ladder with human labels ("a week per second") and the date on the panel header | partial | ui | `U/Runtime/UI/WristDashboard.cs` shows a rung name; no clock or rate is on the panel |
| HV2-03 | Planets from JPL Standish Table 1, valid **1800-2050**, Kepler by 12 Newton steps; Saturn worst at 0.074 deg | Horizons-fitted conics near 2026, with Standish as the fallback outside the fit window and an `EPHEMERIS EXTRAPOLATED` chip | absent | solar | `U/Core/Orbits/OrbitalElements.cs` has the conic; `Core/Orbits/StandishElements.cs` does not exist, so the app degrades away from 2026 with nothing to fall back to |
| HV2-04 | The Moon from Schlyter's truncated series (12 longitude, 5 latitude, 2 distance terms) | a real lunar theory in Core | partial | solar | `SA/catalog/solar_system.json` gives the Moon a Kepler conic that drifts ~85,000 km in five years (`docs/accuracy.md` §2). V2's series as shipped is 2,762 km off; corrected for precession it is 313 km — about 100x better than what CosmoSynth does today |
| HV2-05 | 21 moons on circular orbits in the planet's equatorial plane, with placeholder phases for the Saturn, Uranus and Neptune systems | Horizons elements for every moon | partial | solar | `SA/catalog/solar_system.json` has 15 moons with real elements — better data, 7 fewer bodies (Mimas, Tethys, Dione, Miranda, Ariel, Umbriel, Proteus). V2 places every non-Earth moon in its planet's **rotating** frame, so Titan laps Saturn every 0.46 d instead of 15.95 d: do not port |
| HV2-06 | 12 small bodies on osculating elements: Ceres, Vesta, Pallas, Hygiea, Eris, Makemake, Haumea, Sedna, Quaoar, Gonggong, Orcus, 1P/Halley | the same list from Horizons and SBDB | partial | solar | `SA/catalog/solar_system.json` has Ceres, Vesta, Pallas, Eros, Bennu, Halley, Pluto, Charon; eight of V2's are absent. V2's Ceres is 64 deg out of phase |
| HV2-07 | IAU pole RA/Dec plus prime meridian `W = W0 + Wd d`; Earth uses the Earth Rotation Angle | `BodyOrientation` | present | solar | `U/Core/Frames/BodyOrientation.cs`, WGCCRE 2015 with pole rates, 20 bodies |
| HV2-08 | Surface points: the KSC pad, the Pacific splashdown, rover pins, converted lat/east-lon to the body frame | `SurfaceSite` plus a `sites` layer | partial | solar | `U/Core/Catalog/SurfaceSite.cs` exists with Perseverance, InSight, Viking 1 and the Apollo 11 LM; Curiosity, Viking 2, Sojourner and Opportunity are absent, and nothing renders a pin |

**Presentation**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| HV2-09 | A free-text facts dictionary per body on the info card: mass, gravity, day, temperature, winds, missions | inspector FACTS from catalogue fields, authored text carrying provenance | partial | ui | `U/Runtime/UI/SpatialPanel.cs` shows catalogue fields with provenance glyphs; there is no authored fact text |
| HV2-10 | Planet magnification x1 / x10 / x100 (default) / x1000, with "x1 = true scale" stated first | SIZES presets True / Readable / x10 / x100 / x1000 | partial | ui | `U/Core/Scale/ScaleSpace.cs` `PresentationRadius` uses a fixed 0.55 power law set in `U/Runtime/CosmoSynthBootstrap.cs`; there is no user control and no true-scale option |
| HV2-11 | Moon orbits scale with their parent by the same factor, so a magnified planet does not swallow its satellites | one factor per system, capped by a fraction of the Hill radius; swallowed moons hidden and counted | absent | solar | V2's version leaves the Hill sphere: at x1000 the Moon's drawn orbit is 2.57 AU, outside Mars |
| HV2-12 | The Sun's magnification is capped at x25, "so it never swallows Mercury" | the same cap | absent | solar | - |
| HV2-13 | Elevation exaggeration x1 / x10 / x50 as `bumpScale = 0.004 elev` | RELIEF x1 / x10 / x50 on shipped DEMs | absent | solar | planets are smooth spheres; `U/Core/Procedural/TerrainMesh.cs` exists but no DEM ships |
| HV2-14 | LOD by projected pixel radius: sphere segments 96x64 / 40x28 / 16x12, a sprite below 1.2 px, mesh hidden when the camera is inside the body | `ScaleSpace` representation levels | present | p0-core | `U/Core/Scale/ScaleSpace.cs`; the minimum apparent size is 2.5 px in `U/Runtime/Universe/CelestialBodyView.cs`, not 1.2 |
| HV2-15 | Labels appear above 0.05 px or when focused, inside a gaze cone; **no overlap avoidance** | label budget plus greedy declutter | present | ui | `U/Runtime/UI/BodyLabelLayer.cs` ranks, caps at 14 and declutters — already better |
| HV2-16 | One `immersive-ar` session throughout; a space dome eases its opacity toward 1 (space) or 0 (passthrough) at `dt*4`, so the room and space cross-fade live with no session restart | ROOM slider and Passthrough / Space presets | partial | nav | `U/Runtime/XR/XrRig.cs` `SetPassthrough` is instant and called once from the bootstrap. V2's best UX discovery, and the least work to match |
| HV2-17 | A Milky Way band: an open cylinder with a canvas gradient oriented to the north galactic pole, shown only when the dome is more than 35 % opaque | `mwBand` layer, Space mode only | absent | milkyway | - |
| HV2-18 | Atmosphere as a back-face additive fresnel shell `pow(1 - abs(N.V), 2.5) * 0.7` at 1.045 R | `AtmosphereShell` | present | solar | `U/Runtime/Rendering/AtmosphereShell.cs`, Rayleigh-tinted |
| HV2-19 | Zonal wind flow: per-planet profiles baked to a 128x1 signed LUT; the shader advects streak noise at the real angular rate, blue to cream to red by speed; shown above 60 px | `winds` layer, Procedural until each profile is cited | absent | solar | `grep -ril zonal U/` hits only the surface painter's cloud bands |
| HV2-20 | Earth clouds: procedural alpha noise replaced by a remote cloud map, drifting at 0.003 rad/s of **real** time | `clouds` layer from NASA Visible Earth (public domain), baked offline, static | absent | solar | `SA/surfaces/` holds earth.jpg and moon.jpg only |
| HV2-21 | Saturn's rings with the Cassini division, radial UVs, per-band alpha | existing rings with Cassini **and** Encke | present | solar | `U/Runtime/Rendering/RingSystem.cs` |
| HV2-22 | Procedural fallback textures per body class: giant bands, the Great Red Spot at ~22 S, Neptune's dark spot, Venus chevrons, Mars caps | `SurfacePainter` | present | solar | `U/Core/Procedural/SurfacePainter.cs`, which works from temperature and moisture and has no pole pinch — better |
| HV2-23 | Asteroid and Kuiper belts as GPU-Keplerian point clouds: 6,500 + 7,000 points carrying (a, e, i, Omega, omega, M0, n, size), Kepler solved in the vertex shader, statistical Kirkwood gaps and plutino / scattered classes | `belts` from **real** SBDB elements on the same GPU Kepler approach, so the gaps are measured rather than generated | absent | solar | see MWA-21e / MWA-21f; nothing exists |
| HV2-24 | 56 hand-entered nearest stars, 19 of them with planets | the ~120k-row star catalogue plus a nearest-star census | present | stars | `SA/sky/stars.csstars` (2.9 MB) and `SA/sky/stars_named.json` supersede a hand list |
| HV2-25 | Temperature colours from spectral class, diffraction spikes in the point shader, flare flicker on 12 named M dwarfs | existing star look | present | stars | `U/Runtime/Rendering/StarLook.cs` adds spikes for the brightest; colours come from the catalogue's own temperature |
| HV2-26 | Flare flicker: a sine pulse plus a `pow(sin, 24)` burst on 12 named flare stars, labels marked with a bolt | `flare` layer, off by default, labelled illustrative | absent | stars | timing is invented, so it ships only if it says so |
| HV2-27 | Exoplanets within 0.5 ly of a star with planets, on circular coplanar orbits in the local XZ plane, phase zero at J2000 | Archive systems with real eccentricity and inclination, orientation Procedural and labelled | present | exoplanets | `SA/catalog/exoplanets.json` (180 records, 60 hosts), `U/Runtime/Universe/NearbyStarSystems.cs`, which refuses to invent over a real host |
| HV2-28 | `gotoStar` fixes the scale at 1 AU = 0.35 m, 6 m from the star, so an M-dwarf system ends up centimetres across | star framing from the outermost orbit | partial | nav | `U/Runtime/XR/UniverseNavigator.cs` frames a body at a fraction of the room; it has no system framing |
| HV2-29 | Light-time scale rings: Sun-centred ecliptic circles labelled 1 light-second, 1 light-minute, 1 light-hour, "1 AU . 8.3 light-min", 1 light-day, "30 AU . Neptune", 1 light-month, "1 LIGHT-YEAR", drawn while the ring is 0.4 m to 8 km in the room | `scaleRings`, extended the whole way up: heliopause, Oort inner and outer edges, 1 pc, 10 ly, 100 ly, the Galactic Centre, the disc, M31, the observable universe | absent | lines | the single best "how far is that?" device in the reference set, and nothing in CosmoSynth does it |
| HV2-30 | Ecliptic grid: circles at 1-10 AU every 1 AU, then every 5 AU to 40, plus 12 spokes | `eclipticGrid` guide | absent | lines | - |
| HV2-31 | Nine low-poly procedural probe stand-ins (Voyager, New Horizons, Cassini, Juno, JWST, Parker, BepiColombo, Apollo CSM, Venera) | real NASA models plus an abstract glyph where no model exists | present | solar | `SA/models/` (2.1 MB of `.csmesh`), `U/Runtime/Rendering/SpacecraftModelLoader.cs` |
| HV2-32 | Each probe is aimed so its high-gain dish faces Earth — the comment calls it a "real attitude habit" | a per-mission attitude rule: earth / sun / lvlh / inertial, provenance Inferred | absent | solar | `grep -ril attitude U/` is empty, so models sit at arbitrary orientations |
| HV2-33 | The info card carries live values, redrawn twice a second: distance from the Sun in AU and light-minutes, distance from Earth, orbital speed now by vis-viva, inclination, the Sun-body-Earth phase angle, radius | inspector NOW block | absent | ui | `U/Runtime/UI/SpatialPanel.cs` shows static catalogue fields only. One-way light time is the number that makes 168 AU mean something |

**Missions and navigation**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| HV2-34 | Eight missions drawn as Catmull-Rom curves through waypoints anchored to real bodies at real dates, extrapolated radially past the last anchor | 24 real spacecraft with Horizons trajectory tables and honest coverage windows | present | solar | `SA/catalog/spacecraft.json` (24 craft, 979 kB). No extrapolation is needed: the tables already run to 2034-2035 |
| HV2-35 | Venera 7 as two waypoints (launch 1970-08-17, Venus 1970-12-15) | - | dropped | none | DROP: Horizons has no Venera 7 trajectory, so any curve would be invented geometry. Equivalent: a dated landing site on Venus through `SurfaceSite` |
| HV2-36 | Apollo 11 geocentric: launch from LC-39A (28.608 N, 80.604 W) on the rotating Earth at the real instant, a 190 km parking orbit, a translunar figure-8 to the Moon's real position, three points on a 1,849 km lunar orbit, and the real Pacific splashdown | an authored Illustrative table anchored to Measured events; the real lift-off is 13:32 UTC, not the page's 13:00 | absent | solar | Horizons has only the Apollo 11 S-IVB (`-399110`), never the CSM, so this one genuinely has to be authored — and labelled |
| HV2-37 | "play": jump to the launch date, set a rate (2 weeks/s heliocentric, 6 h/s for Apollo), force x1 magnification for Apollo | play, event stepping, stop | absent | nav | `U/Runtime/Simulation/SimulationClock.cs` has no ladder entry at 2 weeks/s; 1 week/s and 1 month/s exist |
| HV2-38 | Three "live probes": JWST at 0.01 AU anti-sunward of Earth, Juno on a made-up 38-day loop, Parker on elements with Omega = omega = 0 (a fictional orientation) | the real Horizons trajectories for all three, each inside its window | partial | solar | `SA/catalog/spacecraft.json` has Juno (window closed 2025-09-30) and SOHO; JWST and Parker need checking against the shipped list before this row moves |
| HV2-39 | Go to (frames the body 1.4 m across, 2.3 m ahead, then follows it), **Follow** (rides along with the moving body every frame), **Free** (releases) | GO / FOLLOW / FREE | partial | nav | `U/Runtime/XR/UniverseNavigator.cs` travels to a body and then sits at a fixed ICRF point. **Follow is the missing half**: at 1 day per second Earth moves 2.6e6 km per real second and leaves the hologram behind |
| HV2-40 | Four presets: Overview 35 AU, Inner system 1.7 AU, Earth-Moon, Star map 5 ly | rungs 4, 5, 6 and 9 of the ladder | partial | nav | `U/Runtime/UI/WristDashboard.cs` has all four in spirit among its 7 rungs |
| HV2-41 | Desktop: click focuses, double-click goes to, F follows, G goes to, X frees, the wheel scales by x1.2 per notch, WASD/QE fly at a proximity-scaled speed, Shift x4 | the same desktop bindings | partial | nav | `U/Runtime/XR/DesktopInputSource.cs` has WASD fly and keys; no follow, no free, no double-click go-to |
| HV2-42 | Quest: right stick flies and scrolls the tablet while the ray hovers it; left stick rises and turns; both grips grab space and pinch-scale about the **raw hand midpoint**; left X toggles the tablet; the trigger selects tablet first, then bodies | CosmoSynth's binding table; the raw-midpoint pivot is deliberately not ported | partial | nav | `U/Runtime/XR/XrInput.cs` `Bindings`. `U/Runtime/XR/TabletopManipulator.cs` pivots on the focus body instead, which is the fix for the owner's 1.4.0 "I can't go close" report |
| HV2-53 | `logarithmicDepthBuffer: true` on one camera | one camera plus the per-group distance map | partial | p0-core | `U/Runtime/Rendering/DepthBands.cs` runs two cameras; the contract deletes it in favour of `f(d) = d0(1 + ln(d/d0))` |
| HV2-54 | Floating origin `rigKm` in double plus a render scale `scaleMK`, recomputed every frame | `FloatingOrigin` | present | p0-core | `U/Runtime/Universe/FloatingOrigin.cs` with `U/Core/Math/Double3.cs` |
| HV2-55 | Stars beyond `STAR_FAR` = 6e4 render-metres are clamped onto a sky shell | the sky layer at infinity, per eye | present | stars | `U/Runtime/Rendering/SkyAndRings.cs` pins the sky |

**The wrist tablet**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| HV2-43 | Four tabs: Time / View / Go to / Missions, each tab and control category-coloured | TIME, LAYERS + TOOLS, GO, and MISSIONS under TIME | absent | ui | `U/Runtime/UI/WristDashboard.cs` has two pages toggled by one button |
| HV2-44 | Scrollable content with up/down buttons, a scroll bar, and stick scrolling | panel scrolling | partial | ui | `U/Runtime/UI/WristDashboard.cs` already scrolls a window of rows with the stick (`WindowStart`, `_visibleRows`); no scroll bar, no buttons |
| HV2-45 | A fixed footer showing the focus name with "Go to", "Follow" and "Free" | the focus footer | absent | ui | - |
| HV2-46 | The whole tablet collapses to a "MENU" pill | the watch face | partial | ui | `U/Runtime/UI/WristDashboard.cs` dismisses entirely; there is nothing left to tap |
| HV2-47 | Smooth wrist follow: lerp at `dt*7` to 0.17 m above the left grip, pulled 0.06 m toward the eyes, slerped billboard; a hands-free fallback floats 0.55 m ahead and 0.26 m left of the head | the same follow with the Atlas's deadbands (MWA-35) | partial | ui | `U/Runtime/UI/WristDashboard.cs` follows the wrist; there is no hands-free fallback, so losing the left hand loses the panel |
| HV2-48 | Category colours: time amber, view cyan, go-to green, missions violet, system pink, stars blue | - | dropped | none | DROP: the same collision as MWA-39 with the provenance palette. Equivalent: captions and position |
| HV2-49 | Seven view toggles: Orbits, Labels, Info cards, Wind flow, Clouds, Scale rings, Ecliptic grid | the matching `LayerDef` rows | absent | ui | - |
| HV2-50 | Six population toggles: Asteroid belt, Kuiper belt, Stars, Exoplanets, Missions, Live probes | `belts`, `stars`, `exohosts`, `trails`, `spacecraft` | absent | ui | - |
| HV2-51 | Go-to lists: 4 presets, 18 bodies, all moons, 9 nearest stars — by hard-coded index, which is why the "GJ 876" button opens GJ 1002 | GO destinations by catalogue id, never by index | partial | ui | `U/Runtime/UI/WristDashboard.cs` builds the list from the live catalogue, so it cannot drift out of sync — already the fix |
| HV2-52 | Desktop DOM controls: AR / VR buttons, magnification and elevation selects, go-to and mission selects, rewind / play / forward / Now | the same panel on desktop, plus keys | partial | ui | `U/Runtime/XR/DesktopInputSource.cs` has keys; `U/Runtime/UI/ProvenanceHud.cs` is read-only |
| HV2-56 | Remote textures from threex.planets and three.js examples | - | dropped | none | DROP: runtime network, and the images trace to Planet Pixel Emporium, which is not public domain. Equivalent: NASA and USGS maps baked offline by `tools/cosmodata/fetch_surface_maps.py` |

**Heliosphere V1, where it differs from V2**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| HV1-01 | Starts either `immersive-ar` or `immersive-vr` and swaps `scene.background` — two sessions | one session with the ROOM fade, which is V2's own replacement | partial | nav | `U/Runtime/XR/XrRig.cs`; see HV2-16 |
| HV1-02 | The tablet is bolted rigidly to the left grip at a fixed tilt | the smooth-follow wrist panel | partial | ui | `U/Runtime/UI/WristDashboard.cs` already follows smoothly rather than rigidly |
| HV1-03 | A provenance line on the tablet: "Positions from JPL Keplerian elements, valid 1800-2050. Earth spin uses true ERA." — which V2 dropped | INFO -> THIS VIEW ephemeris text plus the EPHEMERIS chip | partial | ui | `U/Runtime/UI/ProvenanceHud.cs` states provenance per field on the desktop HUD; nothing states the ephemeris model or its validity window |
| HV1-04 | Moons placed in the planet's **non-rotating** pole frame — correct, and the thing V2 broke | Horizons elements in a proper reference-frame tree | present | solar | `U/Core/Frames/ReferenceFrame.cs`, `U/Core/Frames/BodyOrientation.cs` |
| HV1-05 | Flat spectral star colours, marker-only probes, Apollo starting from a fixed ecliptic point | superseded by HV2-25, HV2-31 and HV2-36 | partial | solar | `U/Runtime/Rendering/StarLook.cs` and `SA/models/` already beat the first two; the third is HV2-36 and is absent |
| HV1-06 | Scale presets and a magnification select | rungs plus SIZES presets | partial | nav | `U/Runtime/UI/WristDashboard.cs` rungs exist; SIZES does not (HV2-10) |

**SOL-XR Vitals**

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| VIT-01 | A boot screen offering "ENTER OBSERVATORY" or "AMBIENT SUN" | the Sun inspector's VITALS page, and a clean-view tool | absent | ui | - |
| VIT-02 | Five imagery feeds with 22 channel buttons: GOES-19 SUVI (6 wavelengths), SDO AIA (8) plus HMI magnetogram and continuum, SOHO LASCO C2/C3, SOHO EIT (4) | the same channels as baked stills with timestamps, on an "as seen from Earth" billboard — never wrapped on the sphere, because they are disk projections | absent | solar | `SA/sun/` does not exist. The contract defers the stills past 2.0.0 (decision D5); VITALS is to read "not in this build" until then |
| VIT-03 | Animations for SUVI, HMI, LASCO and Enlil with play/pause, a scrubber, 4/8/12/20 fps, arrow keys and space | one 24-frame SUVI 304 loop with the same controls, if the APK budget allows | absent | solar | a 24-frame 512-square loop is ~3 MB against a 150 MB ceiling currently at ~46 MB |
| VIT-04 | The WSA-Enlil solar-wind model animation | - | dropped | none | DROP: an annotated 2D forecast plot; rendering it in 3D would be fiction. Equivalent: a Parker spiral driven by the measured L1 wind speed |
| VIT-05 | Preload progress, a 60 s still refresh, a 5-minute cache-bust bucket | - | dropped | none | DROP: runtime network, which this project forbids. Equivalent: baked at build time with "as of <UTC>" on every card |
| VIT-06 | GOES X-ray flux with the class (X >= 1e-4, M >= 1e-5, C >= 1e-6, B >= 1e-7 W/m2), a 6-hour log plot with B/C/M/X gridlines, and the latest flare | a snapshot card with the class, flux, curve and flare, recorded at build time | absent | solar | - |
| VIT-07 | Solar wind at L1: speed, density, temperature, IMF Bt and Bz, red below -8 nT and amber below -4, plus a 24-hour speed plot | the same card from the RTSW endpoints | absent | solar | two of the page's own endpoints (`plasma-1-day.json`, `mag-1-day.json`) return 404 today, so the page's solar-wind block is blank: the working replacements are `rtsw/rtsw_wind_1m.json` and `rtsw/rtsw_mag_1m.json` |
| VIT-08 | Kp as 24 three-hour bars, and the G-scale (G0 below 5, else min(5, round(Kp) - 4)) | the same card | absent | solar | - |
| VIT-09 | Protons >= 10 MeV and the S-scale (S0 below 10 pfu, else min(5, floor(log10(p/10)) + 1)) | the same card | absent | solar | - |
| VIT-10 | F10.7 flux, the active-region count, the summed sunspot count, and the latest SWPC alert | the same card, **plus** the monthly sunspot and F10.7 record since 1749 driving the Sun's appearance at any simulated date | absent | solar | this is the CosmoSynth-shaped version of the idea: it works for 1859 and 1989 as well as today, which a live feed cannot do |
| VIT-11 | The credit line "All feeds fetched live in your browser; nothing is simulated except nothing" | "Recorded <UTC>. Not live." on every card, which keeps the same ethic while telling the truth about an offline build | absent | ui | - |
| VIT-12 | Every poller fails soft to a status line rather than blanking | failure-visible rows: a layer that did not load says so | absent | ui | see MWA-04 |
| VIT-13 | Amber-on-black monospace dashboard styling | - | dropped | none | DROP: styling, and amber is already the Procedural provenance colour. Equivalent: `U/Runtime/UI/CosmoTheme.cs` |

### 1.3 Holographic Star Map (`refs/webxr/pages/holographic_star_map(2).html`, 705 lines)

The fictional half of the reference set: an eight-node system on a projector pad, meant to look
like a hologram on a table. CosmoSynth's job is not to copy its physics — the orbits are centred
ellipses at uniform angular speed advanced per **frame**, so they run 25 % faster at 90 Hz — but to
keep its *look* as the visual signature of authored content, so a viewer can tell at a glance that
what is in front of them was invented.

| Id | What the web app does | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| HSM-01 | Eight nodes with ids, names, colours, radii, semi-axes, inclination and speed (VG-429 Alpha Core Prime at the centre, seven on inclined ellipses) | a Fictional catalogue JSON whose every field carries `Provenance.Fictional`, loaded through the existing catalogue path | absent | fiction | `U/Core/Catalog/Provenance.cs` already has the Fictional class and `U/Core/Sky/StarSystemFactory.cs` generates worlds — the bridge exists, the sector does not |
| HSM-02 | Orbits are ellipses **centred** on the star (not focused), advanced `theta += speed*0.015` per frame | real Kepler conics with authored eccentricity, periods from Kepler III, on the simulation clock | absent | fiction | frame-rate-dependent motion is a defect, not a feature; `U/Core/Orbits/KeplerSolver.cs` already does the right thing |
| HSM-03 | Header "SYS_MAP // MARVEL_COSMOS", and a comment citing an absent reference image | - | dropped | none | DROP: third-party IP. Equivalent: the system's own name, "VG-429 Holosystem" |
| HSM-04 | Star glow: a rim-bright fresnel `pow(1 - max(N.V, 0), 2.0)` plus a radial glow sprite at x3.8, pulsing +/-5 % | the same rim-glow shader with the pulse, in the projector skin | absent | fiction | - |
| HSM-05 | Wireframe overlays and wire rings on five of the eight nodes | ring flags from the data; wireframe as a skin option | absent | fiction | - |
| HSM-06 | Labels as fixed 3D planes tilted -0.15 rad with bracket art, "SYS_NODE // REC_OK", and a vertical pointer line down to the node | bracket labels with pointer lines, upright billboards, Fictional hue and the `?` provenance glyph | absent | fiction | the page's planes are `DoubleSide`, so they read mirrored from behind |
| HSM-07 | Two helical threads per orbit (tube radius 0.002) carrying a flow shader `f = 0.5 + 0.5 sin(u*30 - t*s)`, `b = f^8`, so the pulse travels along the path and shows the direction of motion | a flow-filament ribbon on each Kepler orbit, with Pulse / Bundle / Twin-helix styles | absent | fiction | the travelling pulse is the one idea here worth more than its cost |
| HSM-08 | The projector pad: an octagonal plate, a border, rings, a grid, an emitter and an upward beam with `fade = (1-v)^4` and stripe bands | the projector skin, **reserved for non-real content**, so the skin itself is the honesty signal | absent | fiction | - |
| HSM-09 | 250 coloured dust points in a ball around the hologram | sector dust, part of the fiction | absent | fiction | the real star field replaces it wherever the content is real |
| HSM-10 | A "PLANETARY METRICS" side panel showing a velocity drawn from `Math.random()` at load and a radius of "0.18M Km" that means nothing | - | dropped | none | DROP: random numbers presented as metrics. Equivalent: a Fictional inspector whose authored values are consistent with the orbit they are drawn from |
| HSM-11 | A button toggling the trajectory flow | an Orbit-flow toggle in the sector's own layer list | absent | fiction | - |
| HSM-12 | Slow auto-rotation of the whole hologram | a Turntable toggle at 0.03 rad/s | absent | fiction | - |
| HSM-13 | `immersive-ar`, hologram placed 1.2 m ahead at scale 0.8 | the sector opens on the room anchor at a tabletop scale | partial | nav | `U/Runtime/XR/AnchorBeacon.cs` places content relative to the room and checks passthrough alpha; no sector stage exists |
| HSM-14 | Trigger-grab within 0.8 m of the plate, one-hand move and yaw, two-hand scale clamped 0.15 ... 2.5 about the hand midpoint | stage grab, twist and scale with the same range | partial | nav | `U/Runtime/XR/TabletopManipulator.cs` does grab and two-hand scale better; the page reads `controller.rotation.y`, an Euler angle, so tilting the controller makes the yaw jump |
| HSM-15 | Desktop OrbitControls | desktop fly and mouse | partial | nav | `U/Runtime/XR/DesktopInputSource.cs` |
| HSM-16 | Everything is 1-px lines and GL points, and every additive material writes alpha | ribbons and sprites, additive with `ColorMask RGB` | partial | p0-shaders | `U/Runtime/Rendering/StarSprite.cs` already blends additively **without** writing destination alpha, which is the rule; nothing else follows it yet. On Quest passthrough, writing alpha punches holes in the room |

### 1.4 Totals

Counted by `tools/gates/parity_gate.py` over the rows above, 2026-09-12.

| Source | Rows | present | partial | absent | dropped |
|---|---|---|---|---|---|
| Milky Way Atlas (§1.1) | 121 | 11 | 23 | 85 | 2 |
| Heliosphere V2 (§1.2) | 56 | 13 | 21 | 19 | 3 |
| Heliosphere V1 (§1.2) | 6 | 1 | 5 | 0 | 0 |
| SOL-XR Vitals (§1.2) | 13 | 0 | 0 | 10 | 3 |
| Holographic Star Map (§1.3) | 16 | 0 | 4 | 10 | 2 |
| **Web-app parity, all four** | **212** | **25** | **53** | **124** | **10** |
| Plus more (§2) | 28 | 3 | 5 | 20 | 0 |
| **Everything the gate reads** | **240** | **28** | **58** | **144** | **10** |

Read the first block and not the second: of 212 features in the owner's own web pages, **25 are
present in the Unity app today and 124 are absent**. The 53 partials are mostly the ones that feel
done and are not — a wrist panel with no tabs, stars with the distance thrown away, travel that
teleports, a passthrough toggle nobody can reach.

The distribution is the interesting part. CosmoSynth is *ahead* of the pages wherever the work is
numerical — Horizons ephemerides, IAU orientation, the floating origin, honest spacecraft windows,
provenance per field — which is why 13 of the 25 present rows are in the Heliosphere table. It is
*behind* on everything the user's hands and eyes touch: 85 of the Atlas's 121 rows are absent, and
every one of the Atlas's actual data layers (dust, Cepheids, globulars, galaxies, constellations,
landmarks, grids) is one of them. That is a fair summary of the instruction as well: the physics
was the part that got built.

---

## 2. Plus more

"Entire functionality" is §1. This section is the "plus more", and it is not a wish list: these
rows are in the same table, carry the same states and lanes, and the same gate reads them. Each
says what it means **on screen**, because "better provenance" is not a feature and "a provenance
glyph beside every number on the inspector card, and a licence tag beside every layer in INFO" is.

Nine of these are the ones the task named. The rest follow from the same place: the web pages are
single files that reload from scratch, fetch at runtime, and stop where the author's patience did.
An app with a catalogue, a clock and a headset does not have those limits, and should stop
behaving as if it did.

**The things a shell of directions cannot do**

| Id | What the XR build must do, on screen | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| PLUS-01 | **Stars at their real distances, from any vantage.** Today the 9,000 drawn stars are directions painted on a 900-unit shell: fly a light year and the sky does not move. With ~110k measured distances in the catalogue, walking from the Sun toward Sirius must make Sirius separate from the field behind it, Orion's belt must lose its shape as you pass through it, and the 88 constellation figures must shear into their true depths — which is the whole argument for doing this in 3D rather than on a dome | `stars` 3D layer over `sky/stars.csstars`, camera-relative parsecs with sky-radius clamping | absent | stars | `U/Runtime/Rendering/SkyAndRings.cs` says in its own comment "Distance is deliberately discarded here", while `U/Core/Sky/StarCatalog.cs` holds the distances |
| PLUS-02 | **Dust that dims the stars behind it.** The Atlas draws the dust cube after the stars, so clouds tint stars in front of them, and its absolute magnitudes ignore extinction entirely. Here, a star behind Orion is dimmer and redder than the same star seen from the side, because `A_V` along the eye-to-star ray comes from the same volume the clouds are drawn from. The Coalsack should hide stars, not paint over them | per-star `A_V` sampled from `dust.csdu`, baked into the star record's `av` column, applied once | absent | stars | the contract's rule: extinction is applied exactly once, `M_V` is de-reddened and `dust` never re-dims V |
| PLUS-03 | **One Sun, drawn once.** Below the handover scale the Sun is a point in the star field at absolute magnitude 4.83, indistinguishable from its neighbours — which is the honest picture of what our star is. Above it, the body takes over and the point vanishes. No seam, no double Sun | handover at log10 U = -13.5 between `bodies` and `stars` | absent | stars | `U/Runtime/Universe/CelestialBodyView.cs` draws the Sun as a body at every scale |
| PLUS-27 | **Observed sky with a named vantage and real extinction.** "Limit magnitude 6.5, from the Sun" and "limit 6.5, from Deneb" are different skies, and the panel says which one you are looking at. Stars below the limit vanish; the count at 6.5 from the Sun is a number the test suite pins. Extinction is in the magnitude, so the Milky Way's dark lanes appear because dust is there, not because someone drew them | `TOOLS -> OBSERVED SKY` over `Core/Sky/Photometry.cs` | absent | stars | beats MWA-14, whose vantage is wherever the camera happens to be — kiloparsecs outside the data at tabletop scale |

**The ladder the web build stops short of**

| Id | What the XR build must do, on screen | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| PLUS-04 | **One continuous ladder, no mode switch.** From an Earth held in the hand to the observable universe, every rung is the same scene at a different scale: grab it, pull it, and the layers fade in and out around you. The Atlas covers 13 decades and stops at 17 Mpc; the heliosphere pages stop at 16 ly; CosmoSynth today stops at 130 ly. The ladder is 19 rungs over 27 decades, and nothing about it is a different app | `ScaleLimits`, `Rungs`, the `f(d) = d0(1 + ln(d/d0))` distance map | absent | p0-core | `U/Runtime/XR/TabletopManipulator.cs` clamps to 1e-20 u/km and `U/Runtime/UI/WristDashboard.cs` tops out at 2 x 130 ly |
| PLUS-05 | **The CMB, both ways.** At the top rung it is a 1.2 m ball you can walk around, the last scattering surface as an object with a diameter. Turn on the backdrop and the same map becomes the sky at infinity behind everything, at any scale, so the oldest light in the universe is the wallpaper behind the Solar System. The Cold Spot is where it is measured to be (about -112 microkelvin), and the legend states the mode, the contrast in microkelvin, the palette and the frame | `cmb` layer, object form and sky form | absent | cmb | `grep -ril cmb U/` is empty. No reference page has anything like this |
| PLUS-06 | **Large-scale structure.** Beyond the Local Volume: 2MRS galaxies out to ~300 Mpc, the SDSS cosmic web, Cosmicflows-4 groups with measured distances, and the Laniakea basin outlines — the supercluster we are in, drawn as a watershed with a volume of about 1.95e6 (Mpc/h)^3. The Atlas's README explicitly defers this ("the genuine next step is 2MRS"); this is that step | `galaxies2mrs`, `sdss`, `groupsCf4`, `basins`, `density` | absent | galaxies | `SA/lss/` does not exist; the data is staged at `data-staging/desi-dr1-lss/` and `data-staging/sdss-dr18/` |
| PLUS-07 | **Spiral arms with their evidence beside them.** The Atlas refuses to draw arms at all, on the correct ground that the ribbons other apps draw are artwork. The answer is not to omit them but to draw the Reid 2019 log-periodic fits **over their fitted azimuth range only**, fading outside it, with the 199 maser parallaxes that constrain the fit drawn at the same time. You see the model and the measurements that made it, together | `arms` + `masers` layers | absent | galaxies | - |
| PLUS-28 | **Deep time, stated.** Scrub a century and the app says which ephemeris it is using and how far it trusts it: an `EPHEMERIS EXTRAPOLATED` chip when the clock leaves the Horizons fit window, and Standish elements as the fallback (valid 1800-2050) rather than a conic quietly degrading | `WarpLadder` plus `Core/Orbits/StandishElements.cs` and a validity chip | absent | solar | `docs/accuracy.md` already records that the conics degrade away from their 2026 epoch; nothing on screen says so |

**Honesty made visible**

| Id | What the XR build must do, on screen | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| PLUS-08 | **Provenance per field, on the card.** Every number on the inspector carries one of four glyphs — `=` Measured, `~` Inferred, `*` Procedural, `?` Fictional — and its source. Mars's radius is `=` and cites WGCCRE; its surface colour is `*`; a generated world's whole card is `*` with its seed; a galaxy whose position angle came from the LVDB default is `*`, not `=`, and is drawn round and faint to match. No reference page does any of this | `Provenance` on every catalogue field, surfaced by the inspector and by INFO -> SOURCES with a licence tag per layer | partial | ui | `U/Core/Catalog/Provenance.cs` has five classes and `U/Runtime/UI/ProvenanceHud.cs` renders glyphs — but only in the desktop IMGUI, and there is no per-layer licence line |
| PLUS-09 | **Every presentation lie declares itself.** The Atlas calls its brightness assists "honestly artificial" in a README and shows nothing on screen; one of them is already active at the default view. Here, any gain that is not 1 raises a header chip naming the factor: `BRIGHTNESS x2.4`, `GALAXY GLOW x6`, `DUST x1.6`, `SIZES x100`. Planets are exaggerated by default, and the headset says so — today that warning exists only on the desktop | presentation-gain chips over `LayerParam.IsPresentationGain` | absent | ui | `U/Runtime/UI/ProvenanceHud.cs:130-135` holds the "SIZES NOT TO SCALE" text, desktop only |
| PLUS-10 | **"What we do not know here", per rung.** The Atlas's best page is its README's Honest limits section, and it is in a file nobody in a headset can read. Put it on the INFO tab, keyed to where you are standing: at the dust rung, "no whole-Galaxy dust cube exists — extinction tomography needs stars behind every parcel"; at the Local Group, "no 3D dark-matter or intergalactic-dust map exists, so none is drawn"; at the galaxy rung, "we see one projection of every other galaxy, so their internal 3D structure is not measured" | INFO -> THIS VIEW, per rung | absent | ui | - |
| PLUS-24 | **Nothing fails silently.** A dataset that did not load gets a row that says so, with the file and the reason, instead of an empty sky that looks like a design decision. The Atlas does this for its 11 fetches; CosmoSynth does not do it at all | failure-visible layer rows in the status card | absent | ui | `U/Runtime/CosmoSynthBootstrap.cs` loads catalogues with no per-file surface |
| PLUS-25 | **The app never touches the network.** Every page in the reference set fetches at runtime, and three of the URLs they depend on are 404 today — the Vitals solar-wind block is blank, and two planet textures are missing. Everything here is baked on triton with a retrieval timestamp, and cards read "Recorded <UTC>. Not live." | all data under `SA/`, listed by `manifest.json` | present | integrator | `U/Runtime/CosmoSynthBootstrap.cs` loads only from StreamingAssets; `CLAUDE.md` makes it a rule |

**Content the web build has no way to hold**

| Id | What the XR build must do, on screen | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| PLUS-11 | **The fiction bridge.** Invented content lives in the same universe as the measured content and is never mistaken for it: a Fictional sector sits on a projector pad with an emitter beam, its labels are the bracket type in the Fictional hue, and every field on its inspector carries `?`. The holographic star map becomes that sector rather than a separate page, and the projector skin is reserved — anything drawn on a pad is authored, and you learn that in the first ten seconds | `Runtime/Stage/**`, the Fictional catalogue, the projector skin | partial | fiction | `U/Core/Catalog/Provenance.cs` has the Fictional class and `U/Core/Sky/StarSystemFactory.cs` generates worlds with a seed; there is no stage and no skin |
| PLUS-12 | **Phenomena hosted at their real objects.** The nine Cosmic Atlas exhibits — an accretion disc, a black-hole merger, gravitational lensing, a galactic encounter, Zel'dovich structure growth, a dark-matter halo, stellar interior, magnetic reconnection, an Alfven wave — play at the object they are about: the accretion disc at Sgr A*, the encounter at M31, the interior inside the Sun. A timeline you can scrub, and a "Model & limits" page that says what the model is | `Runtime/Phenomena/**`, `SA/phenomena/**` | absent | fiction | - |
| PLUS-22 | **A trajectory sandbox that flies what it drew.** Pinch and pull to launch a probe from Earth's surface into the **real** ephemeris field, with the prediction ribbon computed by the same integrator and the same step size as the flight — which is precisely what the owner's own slingshot page gets wrong, drawing a 16 ms Euler path and flying a 64-substep one. A live readout of the resulting osculating elements: a, e, i, periapsis, apoapsis, period, and an escape flag. Thirty-three versions of that page say this is wanted | `Core/Orbits/RestrictedNBody.cs`, `Runtime/Sandbox/**` | absent | fiction | - |

**The things a headset has and a browser tab does not**

| Id | What the XR build must do, on screen | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| PLUS-16 | **A room-scale hologram in the user's own room.** Passthrough first: the galaxy sits on the actual floor, at a scale you set by pulling it with two hands, and nothing the app draws writes alpha into the passthrough buffer, so the room never gets holes punched in it. The Atlas offers an MR button; it does not treat the room as the stage | passthrough-first bootstrap, `AnchorBeacon`, `ColorMask RGB` on every additive shader | partial | nav | `U/Runtime/XR/AnchorBeacon.cs` places content in the room and `U/Editor/PlayModeShot.cs` probes destination alpha; `U/Runtime/Rendering/StarSprite.cs` is the only renderer that follows the alpha rule |
| PLUS-17 | **Hand tracking as a first-class input.** Every control reachable with bare hands: pinch to grab and stretch the universe, a fingertip that presses the panel at a real 6 mm surface crossing, and a wrist panel that stays when the controllers are put down. Today the dashboard **disappears** under pure hand tracking, because `XrInput.Read` only queries controller devices and `FollowWrist` dismisses when it has no pose | `XrHandInput` joints feeding the panel pointer and the manipulator, through one binding table | partial | nav | `U/Runtime/XR/XrHandInput.cs` has pinch hysteresis at 22/35 mm and tap detection; `U/Runtime/UI/WristDashboard.cs` needs a controller to exist |
| PLUS-18 | **Sound.** A soft tick on hover and a click on press, so the panel feels like a surface. Data voices: the Crab pulsar at its measured 29.6 Hz, a black-hole merger's strain as a chirp. An ambient bed you can mute, persisted. The project has no `AudioSource` anywhere today | `Core/Audio/**`, `Runtime/Audio/**` | absent | audio | `grep -ril audiosource U/` is empty |
| PLUS-15 | **Measured surfaces, and relief you can exaggerate.** Real maps where a public-domain one exists — Blue Marble on Earth, LROC on the Moon, MOLA on Mars — with a RELIEF x1 / x10 / x50 control, so Olympus Mons is a shape rather than a colour, and x1 shows you honestly how flat a planet really is | `surfaces/` maps plus DEM-driven displacement | partial | solar | `SA/surfaces/` has earth.jpg (239 kB) and moon.jpg (139 kB) and nothing else; every other body is procedurally painted by `U/Core/Procedural/SurfacePainter.cs` |

**Navigation and time, which is where the owner's feedback keeps landing**

| Id | What the XR build must do, on screen | CosmoSynth home | State | Lane | Evidence / note |
|---|---|---|---|---|---|
| PLUS-21 | **Follow: the viewpoint rides the body.** Go to Earth, turn the warp up to a day per second, and Earth stays in front of you instead of leaving at 2.6 million km per real second. "Following Earth" is on the panel, and any fly or grab re-captures the offset | `UniverseSimulation.FollowTarget`, `GO / FOLLOW / FREE` | absent | nav | `U/Runtime/XR/UniverseNavigator.cs` parks at a fixed ICRF point after travelling. Without this, time warp and travel fight each other |
| PLUS-19 | **Time that runs backwards, and a date you can type.** A signed warp ladder with "Now", so you can run Voyager 2 back through the Neptune encounter, or jump to 1969-07-16 and watch Apollo 11 leave the pad. Today the clock has 15 forward rates, no negatives, and the only way to reset it is a desktop key | signed `WarpLadder`, Now, and a date stepper on the TIME tab | absent | nav | `U/Runtime/Simulation/SimulationClock.cs`, `U/Runtime/XR/DesktopInputSource.cs:102` |
| PLUS-13 | **Real spacecraft that vanish when the data stops.** 24 craft on Horizons trajectory tables, each drawn only inside its `KnownFrom-KnownUntil` window, so nothing is ever shown at a position nobody computed. The Heliosphere page extrapolates Voyager radially at 3.57 AU/yr past its last anchor; this refuses to | `spacecraft` and `trails` layers over `catalog/spacecraft.json` | present | solar | `SA/catalog/spacecraft.json` (24 craft, 979 kB) with honest windows; `docs/accuracy.md` already warns the windows expire and the importer must be re-run each release |
| PLUS-14 | **Mission playback.** Pick Voyager 2, press play: the epoch jumps to launch, the warp picks a sensible rung, the view follows the craft, and the encounters step past one at a time with their dates. Add New Horizons, BepiColombo and Artemis I from Horizons — and Artemis I with real data is what replaces the placeholder Moon page entirely | mission list on the TIME tab, event stepping, stop | absent | nav | - |
| PLUS-20 | **Measure, across 22 orders of magnitude.** Pick any two things — a moon and a rover pin, Sirius and the Sun, M31 and the Milky Way — and get the separation in the unit that suits it: km, AU, light-minutes, pc, ly, Mpc. The Atlas can measure between 489 objects and cannot measure to a planet at all | `TOOLS -> MEASURE` over `BodySelector` plus star picking | absent | ui | - |
| PLUS-23 | **A specimen tray you are allowed to touch.** A shelf of twelve bodies, labelled MODEL COPY, that you can pick up and turn in your hand at their real rotation and real relative sizes — because the one thing you cannot do to the universe itself is pick a planet up | `TOOLS -> SPECIMEN TRAY` | absent | ui | `U/Runtime/XR/TabletopManipulator.cs` deliberately manipulates the universe, never a body; the tray is the explicit copy that makes that safe |
| PLUS-26 | **One binding table, one legend.** Every control is a row in one table that both the code and the on-screen legend read, so a legend cannot go stale and a second claim on a control is a visible duplicate rather than an emergent bug | `XrInput.Bindings` | present | nav | `U/Runtime/XR/XrInput.cs:58-69`, whose own comment records that the previous hand-written legend advertised a binding that did not exist |

---

## 3. The gate

`tools/gates/parity_gate.py`, wired into `tools/gate-all.sh` as **G0i**.

### 3.1 What it does

It parses this file for six-column table rows whose first cell is an id, and applies the eight
checks of §0.4. It prints one line per violation with the row id, then a summary:

```
parity_gate: PASS - 240 rows, 0 unaccounted (absent 144, partial 58, present 28, dropped 10)
  144 rows are absent: accounted for, not built. --require-parity fails on these.
```

Exit 0 when every row is accounted for; exit 10 on the first violation class found. With
`--require-parity` it also exits 10 while any row is `absent`, which is the form `gate-all.sh`
switches to when the phase that clears the absent rows completes.

### 3.2 Why the gate is not just a word count

The checks are chosen against the specific ways a parity list rots:

| Check | The failure it catches |
|---|---|
| duplicate id | two rows claiming the same feature, one green, one red |
| unknown state | "planned", "in progress", "mostly" — states that cannot be counted |
| unknown lane | a row parked on a lane that does not exist, so nobody owns it |
| `lane none` off a dropped row | a row quietly orphaned |
| `dropped` without a `DROP:` reason | a feature dropped without saying why |
| **`present`/`partial` with no file named** | the important one: a row marked done because a design document says it will be. The evidence cell must contain a path ending `.cs`, `.py`, `.sh`, `.json`, `.md` or `.txt`, or a `SA/` path |
| empty cell | a row that looks complete and says nothing |
| no rows found | the file renamed, moved, or its tables reformatted — "all green" must never be able to mean "nothing was read" |

### 3.3 Its self-test

`tools/gates/parity_gate.py --self-test` builds a deliberately broken table in a temp file with one
row per violation class, runs the checker over it, and asserts that **each** violation is reported;
it then runs the checker over a clean table and asserts a pass. It prints `SELF-TEST PASS` or
`SELF-TEST FAIL` and exits accordingly. This follows the project rule that a gate which has never
failed is not a gate: the broken fixture is committed inside the script, so the failing run is
reproducible by anyone.

### 3.4 Status today, and the phase that clears it

G0i is **green today** and reports a large absent count, which is the intended reading: the rows
are all accounted for, and most of them are not built. It is recorded in
`tools/gates/known-red.txt` as a **comment**, not as an excused gate, with the phase that clears
it. That deviation is deliberate:

> `tools/build.sh:56-60` refuses to run at all while `known-red.txt` holds any non-comment line,
> and the file's own header scopes it to Phase 0. A real entry for G0i would block every APK for
> the whole of Phases 1 and 2 — for a gate that is not failing. The comment records the intent
> where a reader will look for it; the enforcement is the `--require-parity` flag, which the
> integrator turns on in `gate-all.sh` once Phase 2 closes.

---

## 4. The order

Rows are cheap when the data is already on the box, the renderer is a variant of one that exists,
and nothing about the frame budget is in question. They are expensive when they need a new shader
under stereo instancing, a new container format, or a headset measurement before anyone can say
whether they fit.

### 4.1 Cheap and high value — do these first

| Rows | Why cheap | Why valuable |
|---|---|---|
| PLUS-21 Follow, PLUS-19 signed time, HV2-01/02 | `SimulationClock` and `UniverseNavigator` exist; Follow is one offset recomputed per frame | Without Follow, every other feature that moves is unusable at warp. This is the cheapest row in the file with the largest reach |
| MWA-29/30/31 travel and crosshair | `TravelPlan` is pure maths with exact endpoint tests; it drives components that exist | Turns six destinations into an experience rather than a teleport, and the crosshair is what makes arriving among 100,000 dots legible |
| MWA-33/33a SOLO and layer toggles | one table, generated UI; no new rendering | Every layer added after this inherits a toggle for free. Doing it late means retrofitting 46 rows |
| PLUS-09 honesty chips, PLUS-08 provenance surfacing | `Provenance` and `ProvenanceHud` already hold the data; it needs moving into the headset | The project's whole claim rests on it, and it currently exists only where a headset user cannot see it |
| MWA-19 constellations | a by-product of `build_starfield.py`, which already holds every HYG row including HIP | 88 recognisable figures that then visibly shear as PLUS-01 lands — it is the demo for 3D stars |
| HV2-29 / PLUS-04 scale rings | one ribbon layer plus labels, no new data | The best "how far is that?" device in the reference set, and it works at every rung |
| MWA-28 measure | a mode over `BodySelector`, plus a formatter that half exists | Same reason; and it is the tool the owner's own README leads with |
| HV2-16 passthrough fade | `XrRig.SetPassthrough` exists; it needs an eased blend and a control | V2's single best UX discovery, roughly an afternoon |

### 4.2 Expensive — schedule deliberately

| Rows | Why expensive |
|---|---|
| MWA-09 dust raymarch | The one row that can sink the frame budget on its own: up to 72 trilinear 3D fetches per pixel over half of both eye buffers. The Atlas leaned on WebXR foveation 0.7; the contract's move to URP is what gets foveation back. **Needs a GPU-time measurement on the headset before anyone designs around it** |
| PLUS-01 3D stars | ~110k stars at 4 verts each is 440k vertices per stereo pass, plus per-star extinction sampling. It is also the first custom procedural draw under single-pass instancing in this project, which is untested here — prototype one on the headset before building the rest on top of it |
| PLUS-05 CMB, PLUS-06 large-scale structure | New containers, HEALPix ordering, numpy importers, and datasets in the tens of megabytes against a 150 MB APK ceiling currently at ~46 MB |
| MWA-37 panel touch | The constants are known (6 mm press, 20 mm release, 6 mm deadband, 20 deg yaw, 300 ms debounce) but they were tuned over three field-test rounds on a real headset, and they cannot be validated headlessly. Budget headset time, not code time |
| HV2-36 Apollo 11, PLUS-14 playback | Horizons has only the S-IVB, so the CSM path must be authored and labelled Illustrative — the one place in the project where authoring real-looking geometry is correct, which makes the labelling load-bearing |
| PLUS-22 sandbox | A double-precision adaptive integrator with impact and SOI events, and the requirement that the drawn prediction and the flown path come from the same code |

### 4.3 Recommended sequence

1. **Foundation.** Fix `StandardFrames` (in flight on `lane/foundation-code`), `ScaleLimits` to the
   full range, one camera and the distance map. Nothing above 130 ly can be placed correctly until
   the galactic rotation is right, and every row in §1 that mentions a scale depends on the limits.
2. **Navigation and time** — `TravelPlan`, Follow, Free, the signed ladder, destinations, the
   crosshair. Cheap, and everything after it is easier to look at.
3. **The layer table and the panel** — `LayerTable`, SOLO, tabs, the watch, PIN, fingertip touch.
   Every later layer then costs one row instead of one dialogue.
4. **Stars in 3D**, with observed mode and the Sun handover. The headline row, and the thing that
   makes constellations, Cepheids and globulars worth drawing.
5. **Deep sky points** — Cepheids, globulars, Local Volume galaxies, landmarks, masers. One
   renderer family, four datasets, all staged.
6. **Lines and guides** — constellations, plane and cylindrical grids, scale rings, the ecliptic
   grid, the dust extent box. One ribbon renderer serves all of them.
7. **Solar decorations** — belts from real SBDB elements, the heliosphere shells, comet tails,
   winds, clouds, sites, attitude. The Solar System is already the strongest part of the app;
   these are the parts the owner's own pages have and it does not.
8. **Beyond** — 2MRS, SDSS, CF4, basins, the CMB. The rungs nothing in the reference set reaches.
9. **Dust**, last of the real-data layers, because it is the riskiest for performance and because
   PLUS-02 wants the stars in place first to dim.
10. **Fiction, phenomena, sandbox, tray, sound.** Content and play, on top of a universe that
    works.

Each step ends with a headset check, and with the lane's report naming the file that moved a row
from `absent` to `present` in this table. When `parity_gate.py --require-parity` exits 0, the
owner's instruction has been carried out, and that is the only definition of it this project is
going to get.
