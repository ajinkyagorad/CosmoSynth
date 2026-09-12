# Solar system & heliosphere pages → CosmoSynth: feature inventory and port plan

*Research report, 2026-09-11. Slug `solar-heliosphere-port`. Read-only phase: nothing under
`~/projects` was changed except this file. No Unity editor, no build.*

Evidence markers: **VERIFIED** = I read the code / ran the measurement / fetched the URL in this
session. **INFERRED** = reasoning, memory or arithmetic not checked against a source; treat as a
lead, not a fact.

Paths: the WebXR pages live on the laptop under
`D:\PhD_Backup\OneDrive - Aalto University\Projects\Nebulytic\webxr-examples\` (abbreviated `wx/`).
CosmoSynth paths are relative to `~/projects/CosmoSynth/` on triton (abbreviated `cs/`), and Unity
sources are under `cs/unity/Assets/CosmoSynth/`.

---

## 0. TL;DR

1. **The eight pages are one family plus three toys.** `solar-xr.html` / `solar-xr2.html` /
   `sol-xr-vitals.html` are what the user's own directory (`Nebulytic/webxr.html:780`) calls the
   **"Heliosphere" family: V1, V2, Vitals**. V2 (2026-07-16) is the most complete real-scale solar
   system the user has built: a 2 103-line single file with ephemerides, 22 moons, 12 small bodies,
   56 nearest stars, 8 missions, GPU asteroid/Kuiper belts, zonal-wind shaders, light-time scale
   rings, a scrollable wrist tablet and a live passthrough⇄space fade. The others are prototypes:
   a specimen tray (`planetsview.html`), a restricted N-body launch sandbox (`orbital_simulator.html`),
   a hand-gesture slingshot game (`spacecraft.html`, "v33 - Gesture Reliability"), an Artemis
   placeholder (`dummyartemis.html`) and a Mars globe whose moons are decorative.
2. **Despite its name, no page in scope draws the heliosphere.** `solar-xr2.html` only mentions the
   heliopause in Voyager 1's note (line 407). The one heliosphere model in the user's files is in
   `wx/Fable5/nebula-gallery/index.html:792-822`, which is outside this task's scope: three glow
   shells, termination shock ~90 AU, heliopause ~120 AU with a 1.9× downwind tail, bow wave ~230 AU.
3. **CosmoSynth is already equal or better on the hard parts**: data provenance, real catalogues
   (31 JPL bodies, 24 Horizons spacecraft, 180 exoplanet-archive records, 119 613 HYG stars), double
   precision with a floating origin, depth bands, IAU rotation with pole precession, honest exoplanet
   orbits, pinch hysteresis, two-hand zoom about a sensible pivot, angular UI sizing, one owner for
   all input bindings.
4. **CosmoSynth is missing most of V2's *features and UX***: no follow / co-moving viewpoint, no
   reverse time and no date jump in the headset, no mission playback, no light-time scale rings,
   no belts, no heliosphere, no live numbers in the inspector (distance, light-time, speed, phase),
   no passthrough⇄space control, no user-selectable magnification, no wind or cloud layers, no
   spacecraft attitude, 7 fewer moons and 8 fewer TNOs or dwarf planets. VERIFIED by grep; §5
   gives file references.
5. **Measured (VERIFIED), against CosmoSynth's own committed Horizons fixture (2026–2031)** — see §4:
   * V2's JPL "approximate elements" planets: worst heliocentric direction error **0.0048° to
     0.074°** (Saturn worst, 1.87 × 10⁶ km). That is comparable to CosmoSynth's two-body fit inside
     2026–2031, and — the point that matters — it stays valid from 1800 to 2050, whereas
     CosmoSynth's conic degrades away from its 2026 epoch.
   * V2's Schlyter Moon: **2 762 km median / 3 204 km max** as shipped, because an of-date series is
     used as J2000. With the missing precession applied, **313 km median / 773 km max**. CosmoSynth's
     Kepler Moon drifts 22 % of a lunar distance (≈ 85 000 km) in five years (`cs/docs/accuracy.md`
     §2), so the corrected series is **about 100× better** and would fix CosmoSynth's documented
     worst case.
   * V2's Ceres is **64° out of phase** (M₀ = 60° is wrong).
6. **Real bugs in the pages that must not be ported** (§4.2). The worst: V2 places every non-Earth
   moon in its planet's **rotating** body frame (`solar-xr2.html:1364`), so Titan laps Saturn every
   0.46 d instead of every 15.95 d; and the local orbit formula runs **retrograde** for every regular
   moon in both V1 and V2.
7. **Live network data** is used by `sol-xr-vitals.html` (NOAA SWPC, SDO, SOHO) and by the texture
   loaders in four pages. Two SWPC endpoints (`products/solar-wind/plasma-1-day.json`,
   `mag-1-day.json`) and three three.js texture URLs **return 404 today**, which is itself the case
   for offline snapshots. §6 proposes a stdlib importer on triton that bakes the monthly sunspot and
   F10.7 record (1749 → 2026-08), a "space weather as of `<UTC>`" card, validated solar stills and
   Carrington maps, real small-body orbits from JPL SBDB, and public-domain textures.
8. **Top carry-overs, ranked** (§7): (a) a real Moon and secular planetary elements in Core;
   (b) navigation — Follow / Go to / Free, date jump, reverse warp, "Now"; (c) mission playback over
   CosmoSynth's real trajectories, plus New Horizons, BepiColombo and Artemis I from Horizons;
   (d) light-time scale rings extended up the whole ladder; (e) Measured belts from SBDB on a GPU
   Kepler point cloud; (f) the heliosphere, drawn for real; (g) offline Sun vitals keyed to
   simulation time; (h) live numbers in the inspector and a "not to scale" warning in the headset;
   (i) the passthrough⇄space fade; (j) the slingshot gesture as the prototype of the "grab-and-pull
   trajectory editor" already on CosmoSynth's roadmap, with the N-body launch sandbox behind it.

---

## 1. Scope and method

| File (wx/) | Lines | Bytes | Git history (VERIFIED, `git log`) | User-facing name |
|---|---|---|---|---|
| `solar-xr2.html` | 2103 | 111 451 | created 2026-07-16 "Add solar-xr2 as V2 alongside solar-xr"; restyled 2026-09-10 | **Heliosphere V2** |
| `solar-xr.html` | 1842 | 96 030 | 2026-07-15; restyled 2026-09-10 | **Heliosphere V1** |
| `sol-xr-vitals.html` | 545 | 25 281 | 2026-07-18 | **Heliosphere Vitals** |
| `planetsview.html` | 215 | 11 221 | 2026-04-13 | "Miniature Tray Solar System" |
| `orbital_simulator.html` | 508 | 22 043 | 2026-03-23/24 | "N-Body Gravitational" |
| `spacecraft.html` | 536 | 23 132 | 2026-03-25 | "Orbital Slingshot Probe" |
| `dummyartemis.html` | 183 | 7 381 | 2026-04-13 | "Artemis Moon & Orion AR" |
| `interactive_mars_globe_moons.html` | 1170 | 55 597 | 2026-07-15 | "Interactive Mars Globe" |

All read in full, except that only the space, moons and panel parts of the Mars globe were read
closely (lines 1-180, 186-339, 400-860, 1055-1170). The directory names come from
`Nebulytic/webxr.html:772-780` (VERIFIED). The 2026-09-10 commit ("Apply Lumen design…") only added
a stylesheet link and a class to each page.

CosmoSynth side (VERIFIED, read this session): `README.md`, `CLAUDE.md`, all of `docs/*.md`, the 11
memory notes in `~/.claude/projects/-home-triton-projects-CosmoSynth/memory/`, a keyword census over
the 71 C# files (19 645 lines), and targeted reads of `XrInput.cs`, `WristDashboard.cs`,
`SimulationClock.cs`, `TabletopManipulator.cs`, `UniverseNavigator.cs`, `UniverseSimulation.cs`,
`ScaleSpace.cs`, `ProvenanceHud.cs`, `SpatialPanel.cs`, `XrRig.cs`, `XrHandInput.cs`,
`NearbyStarSystems.cs`, `BodyOrientation.cs`, `SkyAndRings.cs`, `build_exoplanets.py`,
`fetch_surface_maps.py`, and summaries of the three shipped catalogues and the Horizons fixture.

Commands run on triton, all read-only, all scripts under `/tmp/cs_helio_*`:

* The ephemeris comparison in §4.1 (script reproduced in Appendix C).
* `curl` status probes of every live URL the pages use, plus replacement and offline-source
  candidates (§6).
* Five Horizons API look-ups and four SBDB query-API look-ups.

---

## 2. Heliosphere V2 (`solar-xr2.html`) — full inventory

Three.js 0.160 from cdnjs, a single ES module, `logarithmicDepthBuffer: true` (line 643).
Coordinates: double-precision JS numbers in km, heliocentric **ecliptic J2000**, mapped to three.js
Y-up by `eclToWorld(p) = (x, z, -y)` (line 98). Render scale `scaleMK` is render-metres per km;
the floating origin is `rigKm` (lines 634-636). Every frame computes
`kmWorld(p) = eclToWorld(p - rigKm) * scaleMK` (line 803). This is CosmoSynth's own
FloatingOrigin + UnitsPerKm design, in miniature.

### 2.1 Time (lines 130-153, 2067-2075)

* `Time.jd` starts from the device clock (`new Date()`). `rate` is simulated seconds per real second,
  and **may be negative**. There is a `paused` flag and a "Now" button that resets to real time.
* Rate ladder (line 2067): −10 yr, −1 yr, −30 d, −1 d, −1 h, 1, 1 h, 1 d, 30 d, 1 yr, 10 yr per second.
  The tablet exposes −1 yr, −30 d, −1 d, Real, 1 h, 1 d, 30 d, 1 yr and 10 yr (lines 1675-1677).
* Rates are labelled "real time", "N s/s", "min/s", "hr/s", "d/s" or "yr/s" (lines 145-153). The
  date is shown as ISO UTC.
* UTC is treated as TDB (≈ 69 s error; harmless for rendering).

### 2.2 Ephemerides

| Item | Lines | Model | Accuracy (§4.1) |
|---|---|---|---|
| Planets Mercury–Neptune, Pluto | 159-194 | JPL/Standish "Keplerian elements for approximate positions", **Table 1 (1800–2050)**: `[a, e, I, L, ϖ, Ω]` plus rates per Julian century. Kepler solve by Newton, 12 iterations (107-115). Speed from vis-viva, `v = √(GM(2/r − 1/a))`, with GM☉ = 1.32712440018e11 km³/s² | **VERIFIED**: 0.0048°–0.074° worst heliocentric direction, 2026–2031 |
| Moon | 197-220 | Schlyter truncated series: 12 longitude, 5 latitude and 2 distance terms; output geocentric ecliptic in km (Earth radii × 6378.137) | **VERIFIED**: 2 762 / 3 204 km median / max as used; 313 / 773 km with precession applied |
| 21 moons | 254-277, 1359-1365 | Circular orbits in the planet's equatorial plane, `[a km, P d, R km, inc to equator, L0 °, Ld °/day]`. The Galilean L0/Ld are Meeus' low-accuracy mean longitudes. Saturn, Uranus and Neptune moons have **placeholder L0** (10, 90, 180, 270, 45, 120, 300…) | Orbit sizes right; phases fictional; **direction and frame bugs**, §4.2 |
| 12 small bodies | 282-300 | Osculating elements with M₀ at J2000 and period P: Ceres, Vesta, Pallas, Hygiea, Eris, Makemake, Haumea, Sedna, Quaoar, Gonggong, Orcus, 1P/Halley. The note says "along-track phase approximate" | **VERIFIED**: Ceres 64° out of phase |
| Body orientation | 715-748 | IAU pole RA/Dec + prime meridian `W = W0 + Wd·d`. Earth uses the Earth Rotation Angle, `2π(0.7790572732640 + 1.00273781191135448·d)`. A proper basis: local +Y = pole, local +X = prime meridian, matching texture u = 0.5 | Correct design (VERIFIED by reading) |
| Surface points | 742-748 | lat / east-lon → body frame → world. Used for the KSC pad, the splashdown point and rover pins | Correct |

### 2.3 Body catalogue and presentation (lines 226-250, 628-641, 1370-1437)

* 11 hand-entered bodies (Sun, 8 planets, Pluto, Moon) with radius, IAU pole, rotation, colour,
  wind key, oblateness (Jupiter 0.06487, Saturn 0.09796, Uranus 0.0229, Neptune 0.0171), and a
  free-text **facts** dictionary shown on the info card: mass, gravity, day, temperature, winds,
  missions. Saturn's rings run 74 500–140 220 km with the Cassini division at 117 580–122 170 km.
* **Planet magnification ×1 / ×10 / ×100 (default) / ×1000** (tablet heading "×1 = true scale; moon
  orbits scale with it", line 1693). Two refinements the user added in V2:
  * The Sun is capped at ×25, "so it never swallows Mercury" (lines 638-641). At ×25 the Sun is
    17.4 × 10⁶ km = 0.116 AU, against Mercury's 0.307 AU perihelion.
  * Moon orbits scale about their parent by the same factor (`dispKm`, lines 1370-1379), so a
    magnified planet does not swallow its satellites. **The cost**: at ×100 the Moon's drawn orbit
    is 0.257 AU, and at ×1000 it is 2.57 AU (outside Mars's orbit), with Callisto at 12.6 AU (outside
    Saturn's). VERIFIED arithmetic.
* **Elevation exaggeration ×1 / ×10 / ×50**, applied as `bumpScale = 0.004·elev` to remote bump maps
  (lines 869, 1428).
* **LOD by projected pixel radius** (1381-1384, 1411-1417): sphere segments 96×64 above 140 px,
  40×28 above 18 px, 16×12 below that, and a sprite below 1.2 px. The mesh is hidden when the camera
  is inside the body.
* Labels appear above 0.05 px or when focused, inside a gaze cone (dot > 0.55). There is no overlap
  avoidance.

### 2.4 Rendering layers

| Layer | Lines | What it does |
|---|---|---|
| **Space dome** | 663-667, 1393-1397, 1871-1883 | An inside-out sphere, radius 4 × 10⁶ m, drawn first. Its opacity eases toward `domeTarget` (1 = space, 0 = passthrough) at `dt·4`. The session is **always `immersive-ar`** when supported, so one tablet button cross-fades the room ⇄ space without restarting (help text: "Both modes run in one session — switch Passthrough⇄Space live from the wrist tablet", line 1887) |
| Milky Way band | 669-683 | An open cylinder with a canvas gradient, oriented to the north galactic pole (RA 192.85948°, Dec 27.12825°). Shown only when the dome is more than 35 % opaque |
| Atmosphere | 753-763 | Back-face additive fresnel shell `pow(1 − |N·V|, 2.5)·0.7`, at 1.045 R |
| **Zonal wind flow** | 465-492, 764-798 | Per-planet zonal wind profiles (m/s, east positive) baked to a 128×1 signed LUT. The shader advects streak noise by the **real angular rate** `dLon = v·t / (R·cos φ) / 2π` revolutions, with time accelerated 14 400× (`t·3600·4`, line 1426), coloured blue → cream → red by \|v\|/max. Shown only above 60 px. Profiles: Jupiter (22 knots, max 180), Saturn (13, 430), Neptune (7, 410), Uranus (5, 210), Venus (9, 100), Earth (14, 45), Mars (7, 40). Table in Appendix A.3. No citations in the file |
| Clouds | 582-592, 822-829 | Procedural alpha noise, replaced by the remote `earthcloudmap.jpg` when it loads; drifts at 0.003 rad/s of real time (cosmetic) |
| Saturn rings | 830-858 | RingGeometry with radial UVs. Alpha: C ring (< 91 000 km) ×0.35, Cassini ×0.08, outer A (> 136 500 km) ×0.4. Remote `saturnringcolor.jpg` |
| Procedural textures | 498-581 | Fallback value noise per body class. Latitude band palettes for the giants, a Great Red Spot at ~22°S, Neptune's dark spot, Venus chevrons, Earth land/ice, Mars caps and albedo, Pluto's Sputnik-like patch |
| **Belts, GPU-Keplerian** | 946-1013, 1449-1458 | One `Points` object per belt. Each point carries `(a, e, i, Ω)` and `(ω, M0, n, size)` as attributes; the vertex shader solves Kepler (4 Newton steps) at `uD = jd − J2000` and projects. **Main belt, 6 500 points**: a ∈ [2.1, 3.3] AU with **Kirkwood gaps** at 2.502 / 2.825 / 2.958 / 3.279 AU (3:1, 5:2, 7:3, 2:1 — they check out against Kepler III with Jupiter at 5.2029 AU), e Rayleigh σ = 0.07 capped at 0.35, i Rayleigh σ = 6° wrapped at 25°. **Kuiper belt, 7 000 points**: 55 % classical (a 42–47.5, e Rayleigh σ 0.06 capped at 0.2, i 60 % cold σ = 2° / 40 % hot σ = 10°); 25 % plutinos (a 39.4 ± 0.25, e 0.1–0.3); 20 % scattered (a = 48 + 45u², q 32–40 AU). Uses `Math.random()`, so not deterministic. Shown only while 1 AU renders between 1e-5 and 3e3 m, with fades |
| Stars v2 | 1018-1071 | 56 hand-entered nearest stars (lines 330-385; ≤ 16.3 ly, "RECONS-style", Appendix A.5), 19 of them with planets with **temperature colours** (Tanner-Helland `kelvinRGB` from a spectral-class temperature), **diffraction spikes** in the point shader, and **flare flicker** for 12 named M-dwarf flare stars (a sin pulse plus a `pow(sin, 24)` burst; labels get "⚡"). Beyond `STAR_FAR` = 6 × 10⁴ render-m, stars are clamped to a sky shell |
| Exoplanets | 1073-1089, 1480-1496 | Within 0.5 ly of a star with planets: circular, coplanar orbits drawn in the local XZ plane, dots moving at the real period with phase zero at J2000, labels "Proxima b" etc. `gotoStar` fixes the scale at **1 AU = 0.35 m**, 6 m from the star (1610-1615), so Proxima b's orbit is 1.7 cm across its radius (VERIFIED arithmetic) |
| **Light-time scale rings** | 1094-1105, 1498-1508 | Sun-centred ecliptic circles with labels: 1 light-second, 1 light-minute, 1 light-hour, "1 AU · 8.3 light-min", 1 light-day, "30 AU · Neptune", 1 light-month, "1 LIGHT-YEAR". Drawn only while the ring radius is between 0.4 m and 8 km in render units; labels at 45° |
| Ecliptic grid | 1106-1116 | Circles at 1–10 AU every 1, then every 5 AU to 40, plus 12 spokes |
| **Probe models** | 1121-1182, 1528-1532 | Low-poly procedural stand-ins (Voyager/Pioneer, New Horizons, Cassini, Juno, JWST, Parker, BepiColombo, Apollo CSM, Venera). **Each is aimed so its dish faces Earth** ("real attitude habit", line 1531) |
| Info card | 1294-1347, 1571-1584 | 560×640 canvas billboard beside the focused body. **Live values**: distance from the Sun (AU and light-minutes), distance from Earth (AU), orbital speed now (vis-viva), inclination, **phase angle** Sun–body–Earth, radius. Then the facts. Redrawn every 0.5 s |

### 2.5 Missions (lines 405-459, 1187-1289)

* Eight missions, drawn as **Catmull-Rom curves through waypoints anchored to real bodies at real
  dates**. Each waypoint is either `{jd, body}` (the planet's ephemeris position that day) or
  `{jd, radec:[RA, Dec, AU]}`.
  * Voyager 1: Earth 1977-09-05 → Jupiter 1979-03-05 → Saturn 1980-11-12 → 2026-01-01 at RA 261.9°,
    Dec +12.25°, 168 AU.
  * Voyager 2: Earth → Jupiter → Saturn → Uranus 1986-01-24 → Neptune 1989-08-25 → RA 301.0°,
    Dec −58.9°, 140.3 AU.
  * New Horizons, Pioneer 10, Cassini (VVEJGA), Venera 7, and BepiColombo (11 waypoints).
  * Past the last anchor, outbound craft are extrapolated radially at `extrapAUyr`: 3.57, 3.24, 2.9
    and 2.53 AU/yr. Craft whose missions ended park at their target (`endAt`).
* **Apollo 11** (1204-1232) is geocentric. Launch from **LC-39A (28.608° N, 80.604° W) on the
  rotating Earth at the real instant** (1969-07-16 13:00 UTC is used; the real lift-off was 13:32):
  * a parking orbit at R + 190 km;
  * a translunar figure-8 toward the Moon's real position on 1969-07-19 17:00;
  * three points on a 1 849 km-radius lunar orbit (110 km altitude);
  * return to the real Pacific splashdown at 13.32° N, 169.15° W on 1969-07-24.
* "**▶ play**" (1821-1828) jumps to the launch date and sets the rate (2 weeks/s heliocentric,
  6 h/s for Apollo), with the magnification forced to ×1 for Apollo. The default selection is
  Voyager 1, Voyager 2 and New Horizons.
* "Live probes" (450-454):
  * JWST at 0.01 AU anti-sunward of Earth (≈ L2);
  * Juno on a made-up 1.5 × 10⁶ km, 38-day loop;
  * Parker on heliocentric elements with Ω = ω = 0 (a fictional orientation).
* Rover pins (455-459): Perseverance (18.44, 77.45), Curiosity (−4.59, 137.44), Apollo 11 site
  (0.674, 23.473). Shown on the body above 170 px.

### 2.6 Navigation UX (lines 1590-1633, 1893-1949, 1978-2024)

* **Go to** (`gotoBody`): sets the scale so the (magnified) body is **1.4 m across**, 2.3 m in
  front, and starts following it. **Follow** keeps the current offset and rides along with the
  moving body: `rigKm = body.dispKm + followOff` every frame. **Free** releases.
* **Presets**:
  * Overview: 35 AU → 1.6 m.
  * Inner system: 1.7 AU → 1.4 m.
  * Earth–Moon: Earth radius × mag → 0.25 m, following Earth.
  * Star map: 5 ly → 0.8 m.
* Desktop: click focuses, double-click goes to, F follows, G goes to, X frees, the wheel scales the
  universe by ×1.2 per notch, and WASD/QE fly at speed `max(nearestDist, 0.5)·1.4` render-m/s
  (proximity-scaled; Shift ×4).
* Quest:
  * right stick flies (forward/strafe), and scrolls the tablet while the laser hovers it;
  * left stick rises and turns (1.6 rad/s);
  * **both grips = grab space**: move, and pinch-scale by the ratio of hand separations, clamped
    to 0.2–5 per gesture, **pivoting on the raw hand midpoint**;
  * left X toggles the tablet; the trigger selects (tablet first, then bodies).

### 2.7 Wrist tablet v2 (lines 1638-1832, 1951-1973)

* A 720×1000 canvas on a 0.30 × 0.42 m plane. Header: "HELIOSPHERE", the UTC date, and the rate.
* Four tabs — **Time / View / Go to / Missions** — each tab and control **category-coloured**: time
  amber `#ffcf7d`, view cyan `#7de3ff`, go-to green `#8dffb0`, missions violet `#d3a8ff`, system
  pink `#ff9db0`, stars `#b8c6ff`.
* Scrollable content with ▲/▼ buttons, a scroll bar, and stick scroll. A fixed footer shows the
  focus name with "→ Go to", "◎ Follow" and "✕ Free". It collapses to a "☰ MENU" pill.
* Contents:
  * **Time**: Pause/Play, Now, nine rates, and the Passthrough / Space mode.
  * **View**: 7 display toggles, 6 population toggles, magnification, elevation.
  * **Go to**: 4 presets, 18 bodies, all moons, 9 nearest stars.
  * **Missions**: 8 × (toggle + ▶ play) and a note.
* **Smooth wrist follow**: lerps (`dt·7`) to 0.17 m above the left grip, pulled 0.06 m toward the
  eyes, with a slerped billboard. The "hands-free fallback" floats ahead-left of the head (0.55 m
  forward, 0.26 m left).

### 2.8 V1 → V2: what the user changed in one day (VERIFIED by `diff`, 1 139 changed lines)

V1 (`solar-xr.html`, 2026-07-15) already had the ephemerides, catalogue, belts, missions, scale
rings, grid and info card. V2 added:

1. **One AR session with the dome fade.** V1 started either `immersive-ar` or `immersive-vr` and
   swapped `scene.background`.
2. **A smooth-follow tablet with a hands-free fallback.** V1 bolted the tablet rigidly to the left
   grip at a fixed tilt.
3. Scrolling, category colours, and a fixed focus/follow footer.
4. **True IAU rotation phase** (`W0 + Wd·d`, a proper prime-meridian basis). V1 had the pole plus a
   spin angle applied after an arbitrary-azimuth pole quaternion, so the prime-meridian phase had no
   IAU reference. It also kept **moons in the non-rotating pole frame** (`P.poleQ`,
   `solar-xr.html:1214`).
5. Moon orbits magnified with their planet, and the Sun capped at ×25. V1 magnified planets only.
6. Temperature colours, spikes and flare stars. V1 had flat spectral colours.
7. 3D probe models aimed at Earth. V1 had markers only.
8. Apollo from the real KSC pad on the rotating Earth. V1 started from a fixed ecliptic point.
9. Remote real textures (threex.planets). V1 used only three.js's Earth and Moon.
10. The mesh hides when the camera is inside a body.

V1's tablet had a line V2 dropped: "Positions from JPL Keplerian elements, valid 1800–2050. Earth
spin uses true ERA." Change 4 **introduced the rotating-frame moon bug** (§4.2 #1).

---

## 3. The other pages

### 3.1 Heliosphere Vitals (`sol-xr-vitals.html`) — "SOL·XR — live solar observatory · vital signs"

* **Not XR at all**: a 2D dashboard with a boot screen offering "ENTER OBSERVATORY" / "AMBIENT SUN".
  Its style differs from the rest (amber on near-black, monospace, `#ffb85c` accent). There are no
  three.js or WebXR calls.
* **Imagery** (FEEDS, lines 234-255), five sources with their channels:
  * GOES-19 SUVI: 94, 131, 171, 195, 284, 304 Å (default 304). The animation is the last 48 frames
    of the SWPC frame list.
  * SDO: AIA 171, 193, 211, 304, 131, 335, 94, 1600, HMI magnetogram, HMI continuum. Stills from
    `sdo.gsfc.nasa.gov`; only HMI continuum animates, via SWPC.
  * SOHO LASCO C2/C3: stills plus SWPC animation.
  * SOHO EIT 171, 195, 284, 304: stills only.
  * WSA–Enlil model: SWPC animation.
* Player controls: play/pause, scrubber, 4/8/12/20 fps, ←/→, Space, and "a" for ambient.
* Preload shows progress. When there is no animation it falls back to a still refreshed every
  60 s, with a 5-minute cache-bust bucket.
* **Telemetry** (439-518), polled every 60 s. Each poller "fails soft" to a status line.
  * GOES XRS 0.1–0.8 nm: class via `xrayClass`, where X ≥ 1e-4, M ≥ 1e-5, C ≥ 1e-6, B ≥ 1e-7,
    else A (W/m²). A 6-hour log-scale plot with B/C/M/X gridlines. Plus the latest flare
    (`max_class @ time`).
  * **Solar wind at L1**: speed, density, temperature, IMF Bt and Bz. Bz ≤ −8 nT is shown red,
    ≤ −4 amber. A 24-hour speed plot.
  * **Kp**: last 24 three-hour bins, bars coloured ≥ 7 / ≥ 5 / ≥ 4. `gScale(k) = G0` if k < 5,
    else `G min(5, round(k) − 4)`.
  * **Protons ≥ 10 MeV**: `sScale(p) = S0` if p < 10 pfu, else `S min(5, floor(log10(p/10)) + 1)`.
  * F10.7 (sfu), active regions and summed sunspot count on the latest day, and the latest SWPC
    alert (first 6 lines).
* Credit line (the user's ethic, verbatim): "All feeds fetched live in your browser; nothing is
  simulated except nothing."

### 3.2 Celestial Tray (`planetsview.html`)

* A **specimen tray**: a 1.05 × 0.09 m bevelled glass tray (`MeshPhysicalMaterial`,
  transmission 0.6) with a chrome handle, 1 m high and 0.4 m in front. Ten bodies (Sun → Neptune
  plus the Moon) dock at 0.1 m spacing, each **3.5 cm** in radius.
* Trigger-grab a body and it animates to **0.22 m** radius. Release it within 0.2 m of the tray and
  it re-docks and shrinks (lerp 0.12/frame). Grabbing the handle carries the whole tray.
* Everything spins at 0.004 rad/frame.
* Textures are placeholders: the Sun, Mercury and Jupiter–Neptune all use the **Moon texture,
  tinted**. Mars and Venus point at three.js URLs that are **404 today** (VERIFIED, §6).
* Displacement 0.06 (heavily exaggerated). 128×128 segment spheres, i.e. 16 641 vertices each.

### 3.3 Astro-Volume / "Mission Control" (`orbital_simulator.html`)

* A **restricted N-body launch sandbox**. The UI is labelled "TRUE TIME-SYNCED INTEGRATION" and
  "Physics Engine: Fully synchronized exact integration".
* Units are AU, years and solar masses, with GM☉ = 4π². 1 km/s = 0.210945021 AU/yr (correct).
* Planets Mercury–Saturn move on **circular, coplanar orbits with random initial phases** (not an
  ephemeris), with real mass fractions.
* The probe starts at Earth's surface (4.26352e-5 AU; VERIFIED ≈ 6 378 km) with Earth's orbital
  velocity plus an injection of 0–45 km/s (×0.1 / ×10 buttons). The direction comes from "Launch
  Latitude / Longitude" in a prograde / north / zenith basis ("Lon 0° = Prograde, 180° =
  Retrograde").
* Integrator: **semi-implicit (symplectic) Euler**. Planet positions are evaluated at each
  micro-time — the "NOW SYNCHRONIZED … to prevent teleporting bodies" comment.
* The frame dt is fixed at 0.016 s regardless of the real frame time. At 1× (1 day/s), 1 024
  substeps give 1.35 s per step. At 1 000×, 8 192 substeps give **169 s per step** and 57 000
  force evaluations a frame.
* The probe is dropped beyond 40 AU or inside 0.003 AU of the Sun.
* Trails: "10s Fade" or "Infinite". "Focus Visuals" toggles exaggerated vs true sizes. The
  **"gravity well" grid** is a fixed funnel, `z = −0.3/(d + 0.35)`, centred on the Sun and not
  derived from any mass.
* Time buttons 0–1000×. Status: "Escape V: Earth=11.2km/s | Solar=~42km/s".
* In AR, both triggers pinch-scale and move the container.

### 3.4 Orbital Slingshot v33 — "Gesture Reliability" (`spacecraft.html`)

* **A hand-tracked arcade game**. Five textured planets (gas / rocky / ice canvases) are placed
  randomly at radius 0.15–0.27 m, at least 0.6 m from the probe start. `mass = r·220 000` and
  `G = 7e-6`, so **GM = 1.54·r m³/s²**.
  * Consequence (VERIFIED arithmetic): **every planet has the same surface escape speed,
    √(2GM/r) = 1.755 m/s**, and `MAX_IMPULSE` = 1.8 m/s sits just above it.
  * A circular orbit at 1.5 r takes about 1.9 s for r = 0.2 m.
* **Pinch-and-pull**: the pinch point anchors, you pull back, and the impulse is
  `min(|Δ|·4.8, 1.8)` opposite to the pull. An arrow and a band visualise it.
* **Hysteresis**: pinch closes below 22 mm and opens above 45 mm (lines 312-325). A
  **release buffer** re-uses `lastValidImpulse` when the release frame reads zero (470-494). This is
  what "v33 Gesture Reliability" was about.
* A live **predicted trajectory** is drawn in orange (300 steps × 16 ms ≈ 4.8 s). It adds the
  impulse to the current velocity, so you can re-aim in flight.
* Physics: 64 substeps per frame (symplectic Euler) with softening max(r², 0.01).
* Collision gives "**Surface Lock**" (landed); relaunching from the surface pushes the probe out
  0.12 m and ignores collisions for 0.5 s. A neon additive trail keeps 400 points. The probe
  resets beyond 10 m.
* There are no goals, no score and no persistence.

### 3.5 Artemis Mini Model (`dummyartemis.html`)

* A 0.1 m Moon built from a 256² sphere. The "terrain" is sine products plus 15 fake craters in
  CPU vertex displacement.
* A mini Orion (cylinder and four panels) circles in a 0.25 × 0.18 m ellipse. Its orbit *line* is
  tilted π/2.2 about X while the capsule moves in the horizontal plane (mismatch).
* No data, no controls. A placeholder, as the name says.

### 3.6 Mars XR — "High Fidelity GPS Explorer" (space and moons parts)

* The globe starts at 0.22 m radius and is **capped at 1.0 m diameter**.
* The shader displaces vertices by `uRadius·(elev_km/3389.5)·exag`, with the elevation decoded as
  −8.2 to +21.9 km from a height map; relief exaggeration runs 1–50× (default 15). At 0.22 m and
  ×15, Olympus Mons rises 21 mm; at ×1 it would be 1.4 mm.
* A procedural offline Mars map is generated first (Olympus, Hellas, Valles, micro-craters, caps),
  then remote three.js maps are tried; those are **404 today**.
* Laser "GPS": ray-picks lat / lon / elevation from a 128×64 CPU copy of the height map, pulsing
  reticle.
* Six landmarks. Seven landers, marked ACTIVE (cyan) or COMPLETE (amber): Perseverance, Curiosity,
  Viking 1 and 2, Sojourner, Opportunity, InSight. Coordinates are in Appendix A.6.
* **Moons** (673-725, 1136-1145): Phobos and Deimos as squashed dodecahedra, with axis scales
  1.3 : 1.0 : 0.8 and 1.1 : 0.9 : 0.7.
  * Orbit radii 0.38 and 0.52 m on a 0.22 m globe: Phobos at 1.73 R (real **2.77 R**), Deimos at
    2.36 R (real **6.92 R**).
  * Angular speed ratio 3.2 (real 3.96).
  * The motion runs **retrograde** (+X → +Z); the moons ride the user-rotated globe; the orbit
    lines are not inclined.
* An auto-rotating sun pivot; an "ILLUM" toggle for unshaded reading; a wrist panel with EXAG −/+,
  MOONS, ILLUM and RESET; WebAudio beeps.

---

## 4. Measurements and bugs

### 4.1 Ephemeris accuracy vs JPL Horizons (VERIFIED, run 2026-09-11 on triton)

Reference: `cs/tests/CosmoSynth.Core.Tests/fixtures/horizons_validation.json` — 10 bodies,
26 geometric state vectors each, 2026-01-01 → 2031-01-01 every 73 days, ICRF/J2000 equatorial.
Planets are Sun-centred (500@10) and the Moon is geocentric (500@399).

Method:

* The V2 formulas were transcribed to Python doubles verbatim.
* Ecliptic output was rotated to equatorial with V2's own obliquity, 23.43928°.
* Each position was differenced with the fixture vector.
* The "max deg" column is the angle between the two position vectors as seen from the centre.

Script: Appendix C.

| Body | Median error (km) | Max error (km) | Max / r | Max direction (°) | CosmoSynth two-body fit, 5 yr (from accuracy.md) |
|---|---|---|---|---|---|
| Mercury | 1 584 | 4 154 | 8.5e-5 | 0.0048 | 3.8e-4 |
| Venus | 6 888 | 12 509 | 1.2e-4 | 0.0064 | 2.0e-4 |
| Earth–Moon barycentre | 5 321 | 15 145 | 1.0e-4 | 0.0056 | 9.9e-5 |
| Mars barycentre | 38 754 | 67 948 | 3.2e-4 | 0.0185 | 4.2e-4 |
| Jupiter barycentre | 516 905 | 617 590 | 7.6e-4 | 0.0434 | 2.8e-4 |
| Saturn barycentre | 1 829 986 | 1 867 848 | 1.35e-3 | 0.0740 | 1.2e-3 |
| Uranus barycentre | 358 452 | 1 017 532 | 3.5e-4 | 0.0067 | 6.8e-4 |
| Neptune barycentre | 440 537 | 826 286 | 1.85e-4 | 0.0096 | 4.1e-4 |
| **Moon, as V2 uses it** | **2 762** | **3 204** | 8.3e-3 | 0.469 | **2.2e-1 (≈ 85 000 km)** |
| **Moon, precession added** | **313** | **773** | ≈ 2e-3 | — | — |
| **Ceres (V2 elements)** | 3.84e8 | 4.08e8 | 1.07 | **63.9** | 3.0e-3 |

What the table means:

* **Inside 2026–2031** the Standish table matches CosmoSynth's fitted conics to within a factor of
  about 2 either way. Its advantage is **validity 1800–2050**, published by Standish (INFERRED
  from his stated error table: tens to hundreds of arcseconds, worst for Saturn). CosmoSynth's
  2026-epoch conic is documented as off by "millions of kilometres" at 1979
  (`cs/docs/accuracy.md`, "A second-order consequence…"). For mission playback in the 1970s this
  is the difference that matters.
* **The Schlyter series is used wrongly in V2.** Schlyter's elements are referred to the
  **equinox of date** (INFERRED from the series' documentation; the fit is decisive: removing
  1.396971°/century of general precession cuts the error by 9×). As used, it is 0.36–0.43° off in
  longitude over 2026–2031.
* **Even this 1990s hobby series, corrected, beats CosmoSynth's Moon by ~100× at 5 years.** A Meeus
  chapter-47 truncated ELP-2000/82 (~60 longitude/distance terms and ~60 latitude terms) should
  reach ~10″ ≈ 20 km (INFERRED — measure it with the same fixture before claiming it).
* V2's Ceres phase is simply wrong. SBDB gives M = 274.42° at JD 2461200.5 (VERIFIED, §6.3). V2's
  "orbit exact; along-track phase approximate" disclaimer understates a 64° error.
* Both V2 and CosmoSynth put **Earth at the Earth–Moon barycentre**. Earth sits 4 671 km from the
  EMB (INFERRED: 384 400/82.3), so every Moon placement built on it inherits that offset.

### 4.2 Bugs in the pages (do not port them)

| # | Page:lines | Bug | Evidence |
|---|---|---|---|
| 1 | solar-xr2:1359-1365 (also Juno, 1282-1285) | Moon positions go through `P.qTmp`, the planet's **spin-inclusive** body orientation, so moons are placed in the rotating frame. Inertial rate = W ± Ld: **Titan 810.8 − 22.6 = 788°/d (0.46 d period, real 15.95 d)**, Io 667°/d (0.54 d, real 1.77 d), Phobos −778°/d. V1 used the non-rotating `P.poleQ` | VERIFIED by reading and arithmetic; not run |
| 2 | solar-xr:1213, solar-xr2:1363 | Local orbit `(cos L·a, sin L·sin i·a, sin L·cos i·a)` with local +Y = pole runs +X → +Z, i.e. **clockwise seen from the north pole = retrograde** for every regular moon (planets, via `eclToWorld`, run correctly +X → −Z). Triton's `inc = 157.35` with `Ld < 0` also comes out wrong | VERIFIED by reading and the rotation-matrix check |
| 3 | solar-xr2:197-220, 1357 | Of-date lunar series added to a J2000 Earth; 2.8–3.2 × 10³ km error | VERIFIED, §4.1 |
| 4 | solar-xr2:283 | Ceres M₀ wrong (64°). Other small-body phases unverified | VERIFIED |
| 5 | solar-xr2:1370-1379 | Moon systems co-scaled with magnification leave their Hill spheres: at ×1000 the Moon's drawn orbit is 2.57 AU | VERIFIED arithmetic |
| 6 | solar-xr2:264-276 | Saturn, Uranus and Neptune moon L0 are round placeholders (fictional phases, unlabelled) | VERIFIED by reading |
| 7 | solar-xr2:1610-1615 | Exoplanet view locked at 1 AU = 0.35 m; M-dwarf systems (a < 0.05 AU) end up centimetres across | VERIFIED arithmetic |
| 8 | solar-xr2:453 | Parker's orbit orientation is fictional (Ω = ω = 0); Juno's orbit is invented | VERIFIED by reading |
| 9 | solar-xr2:1063, 795 | Additive effects write alpha. On Quest passthrough that punches holes — CosmoSynth has a documented gotcha for exactly this (`cs/CLAUDE.md`, "Needs a headset…") | INFERRED for WebXR; a hard rule for the Unity port |
| 10 | sol-xr-vitals:460, 470 | `plasma-1-day.json` and `mag-1-day.json` **404 today**, so the solar-wind block is blank | VERIFIED 2026-09-11 |
| 11 | planetsview:31-42 | Placeholder Moon texture on the Sun and giants; Mars and Venus URLs 404 | VERIFIED |
| 12 | orbital_simulator:215, 496 | Random planet phases; fixed dt of 0.016 s; the probe passes through planets (their gravity is just switched off inside the radius); the trail rebuilds an unbounded array every frame; the funnel grid is not physical | VERIFIED by reading |
| 13 | spacecraft:496-528 | Prediction takes single 16 ms Euler steps while flight takes 64 substeps a frame, so the drawn path ≠ the flown path near planets. `PREDICT_SUBSTEPS` is declared and never used. Planets re-randomise on every load | VERIFIED by reading; divergence INFERRED |
| 14 | dummyartemis:123 vs 169-171 | Orbit line tilted, capsule moving flat | VERIFIED by reading |
| 15 | mars_globe:717-721, 1138-1144 | Moon distances and speed ratio not to scale; retrograde; attached to the user-spun globe | VERIFIED arithmetic |
| 16 | all | **"Heliosphere" draws no heliosphere** | VERIFIED by grep |
| 17 | solar-xr2:1712-1713 | The tablet's "GJ 876" button uses index 52, which is GJ 1002 (line 382); GJ 876 is index 51 (line 381). Hard-coded indices into a hand list | VERIFIED by counting lines 330-385 |

---

## 5. Feature-by-feature: does CosmoSynth already have it?

Legend: **CS≥** CosmoSynth equal or better · **GAP** missing in CosmoSynth · **PART** partial.

| Feature (source) | CosmoSynth today (VERIFIED file refs) | Verdict | What the port needs |
|---|---|---|---|
| Double-precision positions + floating origin (V2 `rigKm`/`scaleMK`) | `Core/Math/Double3.cs`, `Runtime/Universe/FloatingOrigin.cs`, reference-frame tree `Core/Frames/ReferenceFrame.cs` | CS≥ | — |
| Log depth (V2 `logarithmicDepthBuffer`) | Two-band `Runtime/Rendering/DepthBands.cs`; sky pinning | CS≥ | — |
| Planet ephemeris (Standish Table 1) | Two-body conics fitted near 2026 from Horizons (`solar_system.json`); `TrajectoryTable` exists but is not used for planets | PART: better near 2026, **worse far from it** | Add Standish secular elements as the fallback outside the fit window (`Core/Orbits/StandishElements.cs`), or sampled planet tables; §7.1 |
| Moon (Schlyter) | Kepler Moon, 22 %/5 yr (`docs/accuracy.md` §2) | **GAP (accuracy)** | `Core/Orbits/LunarTheory.cs`; test against the fixture Moon; §7.1 |
| Moons: 22 in V2 | 15 moons in `solar_system.json` (Moon, Phobos, Deimos, 4 Galileans, Enceladus, Rhea, Titan, Iapetus, Titania, Oberon, Triton, Charon) with Horizons elements | CS≥ in accuracy; **GAP of 7**: Mimas, Tethys, Dione, Miranda, Ariel, Umbriel, Proteus | Add to `tools/cosmodata/bodies.py` and re-run `build_solar_system.py` |
| Small bodies (12) | Ceres, Vesta, Pallas, Eros, Bennu, Halley (+ Pluto, Charon) | **GAP of 8**: Hygiea, Eris, Makemake, Haumea, Sedna, Quaoar, Gonggong, Orcus | Same importer. Sedna has no measured mass, and the importer's "skip if no GM" rule (`cs/CLAUDE.md` Data) would drop it. Decide: allow GM = Unknown for a heliocentric test particle |
| IAU orientation (V2) | `Core/Frames/BodyOrientation.cs`: WGCCRE 2015 with pole rates, 20 bodies. Earth `W = 190.147 + 360.9856235 d` (line 166) | CS≥ (V2's ERA-exact Earth differs by ~0.3°, INFERRED) | — |
| Oblate giants | Real polar radii (accuracy.md §5) | CS≥ | — |
| Saturn rings + Cassini | `Runtime/Rendering/SkyAndRings.cs:522-556` (procedural radial texture with Cassini *and* Encke) | CS≥ | — |
| Atmosphere shells | `Runtime/Rendering/AtmosphereShell.cs`, Rayleigh-tinted | CS≥ | — |
| Surfaces / textures | Procedural `SurfacePainter`; real **Earth (Blue Marble 2048) and Moon (LROC 1k)** only (`tools/cosmodata/fetch_surface_maps.py:37-51`) | PART | More maps offline; §6.4 |
| Earth clouds | None found | GAP | NASA Visible Earth clouds (PD), static or slow |
| **Zonal wind flow** | None (grep "zonal" hits only the painter's cloud bands) | GAP | §7.9 — cited profiles, ColorMask RGB |
| **Elevation exaggeration** | None; planets are smooth spheres (terrain meshing is phase 3) | GAP | Needs real DEMs (§6.4) plus phase-3 terrain, or a normal-map bump |
| LOD by pixels | `Core/Scale/ScaleSpace.cs:119-127` (Point / Impostor / Sphere / Terrain / Tiles / Local) | CS≥ | — |
| Labels | `Runtime/UI/BodyLabelLayer.cs`, with overlap resolution | CS≥ | — |
| Magnification ×1–×1000 + moon co-scaling | Power-law `ScaleSpace.PresentationRadius` (`Core/Scale/ScaleSpace.cs:280-285`), compression **0.55 fixed** in the bootstrap (`Runtime/CosmoSynthBootstrap.cs:65`); `RadiusExaggeration` exists (`UniverseSimulation.cs:80-84`). **No headset control; the "SIZES NOT TO SCALE" warning appears only in the desktop IMGUI** (`Runtime/UI/ProvenanceHud.cs:130-135`) | PART + honesty gap | Expose presentation presets on the dashboard; draw the warning in the headset; §7.8 |
| Info card live values | `Runtime/UI/SpatialPanel.cs:76-108`: static catalogue fields with provenance glyphs only | GAP | Add a "now" section: r☉, Δ⊕ (AU, light-min), light-time, vis-viva speed, phase angle, `SizeExaggeration`; §7.8 |
| **Time**: reverse rates, Now, date jump | `Runtime/Simulation/SimulationClock.cs:29-46`: 15 forward rates 0 → 1 Myr/s, **no negative**; `SetEpochToNow` reachable only from desktop "N" (`Runtime/XR/DesktopInputSource.cs:102`) | GAP | Signed warp, Now, and a date/epoch picker on the dashboard; §7.2 |
| **Go to / Follow / Free** | Travel-to exists (`Runtime/XR/UniverseNavigator.cs:150-176`), but afterwards the viewer sits at a fixed ICRF point (lines 194-204). No per-frame focus tracking found by grep | **GAP (Follow)** | A co-moving viewer frame; §7.2 |
| Presets | 7 scale rungs (`Runtime/UI/WristDashboard.cs:93-102`): Held in hand … Solar neighbourhood | CS≥ | Add Heliosphere and Oort rungs; §7.4 |
| Two-hand grab-scale | `Runtime/XR/TabletopManipulator.cs:143-176`: scale ∝ (span ratio)², log-smoothed, pivot falls back to the focus body (fixes the 1.4.0 "can't go close" complaint) | CS≥ (V2's raw-midpoint pivot is the bug CS fixed) | — |
| Flying | `Core/Scale/ScaleSpace.cs:245-251` `NavigationSpeed = d/4 s`; L stick fly, R stick rise/turn (`XrInput.cs:58-69`) | CS≥ | — |
| Passthrough ⇄ Space | `Runtime/XR/XrRig.cs:194-204` `SetPassthrough`; only caller is the bootstrap (line 85). Instant, no UI | PART | Bind it to a control and fade (§7.10) |
| Milky Way band | None (phase 4 not started) | GAP (a phase-4 item) | Out of scope here. V2's trick — a gradient cylinder oriented to the NGP, shown only in Space mode — is a cheap placeholder until real galactic structure exists |
| Stars | 119 613 HYG stars, Ballesteros colours, spikes for the brightest (`docs/data-sources.md`; `StarSprite`, `SkyAndRings`) | CS≥ | Brown-dwarf supplement (§7.12) |
| Flare-star flicker | None | Optional | Only if labelled illustrative (timing would be fiction) |
| Exoplanets | 60 hosts / 120 planets from the NASA Exoplanet Archive; real e and i, **orientation Procedural and labelled** (`tools/cosmodata/build_exoplanets.py:335-358`); never invents over a real host (`Runtime/Universe/NearbyStarSystems.cs:139-141`) | CS≥ | Borrow only V2's "system appears as you arrive" framing, via `TabletopManipulator.FrameSystem` |
| Scale rings (light-time) | None (grep "light-second" = 0) | **GAP** | §7.4 |
| Ecliptic grid | None | GAP (small) | Optional companion to the rings |
| Belts | None (grep Kirkwood / Kuiper / "asteroid belt" = 0) | **GAP** | §7.5 (Measured, from SBDB) |
| Missions | **24 real spacecraft** with Horizons tables, conics or surface sites, NASA models and honest windows (`spacecraft.json`) | CS≥ in data. **GAP**: New Horizons (dropped for lack of a model), BepiColombo, Artemis I, Apollo 11 trajectory, Venera 7; no "play" | §7.3 |
| Probe attitude (dish to Earth) | None found (grep "attitude" = 0) | GAP (small) | Per-mission attitude rule; §7.11 |
| Rover / landmark pins | Surface sites: Perseverance, InSight, Viking 1, Apollo 11 LM | PART | Curiosity, Viking 2, Sojourner, Opportunity; landmarks only once real Mars imagery exists |
| Wrist tablet | `Runtime/UI/WristDashboard.cs`: wrist-turn reveal, X to engage, 2 pages (Scale, Destinations), angular sizing, single input owner | CS≥ in design, **GAP in content** | Add Time / View / Missions pages; §7.2 |
| Specimen tray | None; manipulation is deliberately on the universe (`TabletopManipulator.cs:21-34`) | GAP (by design) | §7.13 — a labelled copy |
| N-body sandbox | None; n-body explicitly deferred (`docs/architecture.md`, "Deliberately deferred") | GAP | §7.7 |
| Slingshot gesture | Pinch hysteresis 22/35 mm and tap detection exist (`Runtime/XR/XrHandInput.cs:31-34, 220-227`) | PART (input CS≥) | §7.7 |
| Sun vitals | None (grep flare / solar wind = 0) | GAP | §6.2, §7.6 |
| Heliosphere | None | GAP | §7.6 |

---

## 6. Live network data → offline snapshot pipeline

CosmoSynth's rule: "**The app never touches the network at runtime.**" (`cs/CLAUDE.md` Data;
`cs/README.md`). Every live dependency below must become an offline bake on triton, shipped in
StreamingAssets, with a retrieval timestamp and provenance.

### 6.1 What the pages fetch (probed from triton 2026-09-11, `curl`, VERIFIED)

| Page | URL | Status today | Size |
|---|---|---|---|
| Vitals | `https://services.swpc.noaa.gov/json/goes/primary/xrays-6-hour.json` | 200 | 161 kB |
| Vitals | `…/json/goes/primary/xray-flares-latest.json` | 200 | 0.45 kB |
| Vitals | `…/products/solar-wind/plasma-1-day.json` | **404** | — |
| Vitals | `…/products/solar-wind/mag-1-day.json` | **404** | — |
| Vitals | `…/products/noaa-planetary-k-index.json` | 200 | 4.7 kB |
| Vitals | `…/json/goes/primary/integral-protons-1-day.json` | 200 | 229 kB |
| Vitals | `…/products/summary/10cm-flux.json` | 200 | 47 B |
| Vitals | `…/json/solar_regions.json` | 200 | 138 kB |
| Vitals | `…/products/alerts.json` | 200 | 46 kB |
| Vitals | `…/products/animations/suvi-primary-304.json` | 200 (**338 frames**, 2026-09-10 15:08 → 2026-09-11 15:00Z) | 39 kB |
| Vitals | `…/products/animations/lasco-c3.json`, `sdo-hmii.json` | 200 | 5 / 11 kB |
| Vitals | `…/products/animations/enlil.json` | 200 (169 frames, runs to **2026-09-15** — it is a forecast) | 12.5 kB |
| Vitals | `…/images/animations/suvi/primary/304/latest.png` | 200 | **1.16 MB** per frame |
| Vitals | `https://sdo.gsfc.nasa.gov/assets/img/latest/latest_512_0171.jpg` | 200 | 54 kB |
| Vitals | `https://soho.nascom.nasa.gov/data/realtime/c3/512/latest.jpg`, `eit_304/512/latest.jpg` | 200 | 150 / 96 kB |
| V2 | `https://raw.githubusercontent.com/jeromeetienne/threex.planets/master/images/*.jpg` | 200 | e.g. 158 kB |
| V2 | `https://threejs.org/examples/textures/planets/earth_atmos_2048.jpg` | 200 | 513 kB |
| Tray, Mars globe | `raw.githubusercontent.com/mrdoob/three.js/{master,dev}/examples/textures/planets/{mars_1k_color,mars_1k_topo,venus_magellan_1024}.jpg` | **404** | — |

Working replacements found for the dead solar-wind endpoints (VERIFIED 200):
`https://services.swpc.noaa.gov/json/rtsw/rtsw_wind_1m.json` (2.56 MB; fields `proton_speed`,
`proton_density`, `proton_temperature`, `source` ACE/DSCOVR, `active`) and `…/rtsw/rtsw_mag_1m.json`
(1.45 MB).

Also VERIFIED 200 and far more useful offline:

* `…/json/solar-cycle/observed-solar-cycle-indices.json` — 512 kB, **3 332 monthly rows from
  1749-01 to 2026-08**. Fields `ssn`, `smoothed_ssn`, `observed_swpc_ssn`, `f10.7`, …; 2026-08 has
  ssn 76.0 and F10.7 116.22.
* `…/predicted-solar-cycle.json` — 18 kB.
* `…/planetary_k_index_1m.json` — 28 kB.
* `…/ovation_aurora_latest.json` — 921 kB.

**Licence flags**:

* The threex.planets images are commonly traced to Planet Pixel Emporium (James Hastings-Trew), which
  is *not* public domain (INFERRED). Do not ship them.
* The three.js example textures have unclear provenance (INFERRED). Do not ship them.
* NOAA SWPC products and NASA SDO/SOHO imagery are US-government/NASA public (INFERRED). SOHO is
  ESA/NASA, so check the credit line per `cs/docs/data-sources.md`'s "Hosted by NASA ≠ public domain"
  rule.

### 6.2 Proposed `tools/cosmodata/build_heliophysics.py` (stdlib only; run on triton)

Same pattern as the existing importers: retrieve, validate, write to a temp name, `os.replace` on
success, fail loudly on any failure and **keep the previous artefact** rather than shipping none. The
plasma-1-day 404 shows endpoints move: "an artefact on disk is not evidence of the run meant to
produce it", `cs/CLAUDE.md`.

Outputs under `unity/Assets/StreamingAssets/CosmoSynth/sun/`:

1. `activity.json` (~40 kB; or binary, 3 332 × 2 float32 = 27 kB): the monthly sunspot number and
   F10.7 since 1749. Provenance: **Measured** (SILSO/SWPC) for observed months, **Inferred**
   (prediction) for months after the last observation.
   * Runtime `Core/Sun/SolarActivity.cs` interpolates at `CosmicTime`.
   * It drives spot density, faculae and flare rate on the Sun's surface, the corona's visual
     extent, and the aurora oval size.
   * All such visuals are **Procedural over Measured** and must be labelled.
   * This turns the Vitals page's "now" into "at the simulation date", which is the CosmoSynth-shaped
     version of the idea: it works for 1859 (Carrington) or 1989 as well as today.
2. `snapshot.json` (~5 kB): a "**Space weather — snapshot as of 2026-09-11 15:07 UTC (not live)**"
   card.
   * Fields: XRS class and flux, the last flare class and time, Kp and G-scale, L1 speed / density /
     temperature / Bt / Bz from RTSW, ≥10 MeV protons and S-scale, F10.7, the numbered-region count
     and summed spots, and the alert headline.
   * Use the Vitals thresholds verbatim (§3.1). Record `retrievedUtc` and every source URL.
   * The UI must say "as of", preserving the user's "nothing is simulated" ethic by being explicit
     that it is a recording.
3. Disk imagery, 3–6 stills (SUVI 304 / 171 / 195, AIA 171 / 193, LASCO C3), each ≤ 512², JPEG
   ~50–150 kB; about 0.6 MB total.
   * Validate with magic bytes and decoded dimensions, reusing `fetch_surface_maps.py`'s
     `image_dimensions`.
   * Show them as "the Sun **as seen from Earth** by GOES-19 SUVI at `<time>`" on a billboard in the
     Sun inspector. They are disk projections; never wrap them on the sphere.
   * A 24-frame loop at 512² ETC2 is ~3 MB — probably not worth the APK budget (43 MB,
     `cs/docs/roadmap-universe.md`).
4. Optional: a Carrington synoptic map wrapped on the Sun sphere. The JSOC HMI synoptic tree
   `http://jsoc.stanford.edu/data/hmi/synoptic/` answers 200 with a listing (VERIFIED). The files are
   FITS; a stdlib FITS image reader is ~50 lines (INFERRED).
   * Registration is free: the IAU solar prime meridian **is** Carrington's system,
     W = 84.176° + 14.1844°/day (360/25.38 d), so u = 0 on the map is CosmoSynth's existing
     `BodyOrientation.Sun` prime meridian (INFERRED from the IAU definition; check against a known
     Carrington-rotation start date in a test).
   * Label it "Carrington rotation NNNN, Measured, static".
5. Do **not** bake WSA–Enlil frames. They are annotated forecast plots, and in 3D they would be
   fiction. Use a Parker spiral instead (§7.6).

Cadence: run once per release, just before `build_spacecraft.py`, because the close orbiters' windows
expire too.

### 6.3 Small bodies: `tools/cosmodata/build_small_bodies.py` (replaces procedural belts)

* The JPL SBDB query API `https://ssd-api.jpl.nasa.gov/sbdb_query.api` is reachable (VERIFIED). A
  test query returned Ceres with a = 2.766, e = 0.0797, i = 10.59, Ω = 80.25, ω = 73.29,
  M = 274.42, epoch JD 2461200.5, H = 3.34, D = 939.4 km.
* Counts as returned: **15 191 asteroids with H < 13**, **7 287 objects in class TNO**.
  Re-verify the filter syntax — one of my probes returned an implausible count, so quoting via ssh
  may have mangled it.
* Same terms as Horizons (`cs/docs/data-sources.md`, "Small bodies").
* Output: a flat binary of `a, e, i, Ω, ω, M, n, H` as float32 (32 B/object). 22.5 k objects is
  about **0.72 MB**, or ~0.36 MB with 16-bit quantisation. Provenance **Measured** with the epoch
  stated; drift away from the epoch is a slow precession, as with CosmoSynth's conics.
* This shows the *real* Kirkwood gaps, Hildas, Jupiter trojans, Hungarias and the TNO classes, so
  V2's statistical generator becomes unnecessary. Keep it only as a clearly Procedural fallback for
  fictional systems.

### 6.4 Textures (extend `fetch_surface_maps.py` SOURCES)

Candidates probed (VERIFIED status):

* `https://eoimages.gsfc.nasa.gov/images/imagerecords/57000/57747/cloud_combined_2048.jpg` —
  200 image/jpeg, 829 kB. NASA Visible Earth clouds, PD.
* `https://svs.gsfc.nasa.gov/vis/a000000/a004700/a004720/ldem_3_8bit.jpg` — 200 image/jpeg, 112 kB.
  Lunar elevation for exaggeration.
* `https://www.solarsystemscope.com/textures/download/2k_mars.jpg`, `2k_jupiter.jpg` — 200
  image/jpeg. Licence believed to be **CC BY 4.0** (INFERRED; check).
* **Warning**: `https://photojournal.jpl.nasa.gov/jpeg/PIA07782.jpg` (and PIA11141) answer
  **200 with text/html, 288 kB** — a soft-404. This is exactly the trap `fetch_surface_maps.py`'s
  header documents; its validator must stay.
* Prefer USGS Astrogeology products (Mars MOLA / MDIM, Mercury MESSENGER, Pluto New Horizons) where
  a stable URL can be pinned (INFERRED; per-product licence check).

### 6.5 Spacecraft freshness (VERIFIED from `spacecraft.json`)

As of today, the windows have already closed for **Hubble (2026-09-01)**, **SOHO (2026-06-27)**,
**MAVEN (2026-03-01)** and **Juno (2025-09-30)**; the ISS closes 2026-10-11, Odyssey 2026-10-08 and
MRO 2026-10-22. CosmoSynth's honest window rule therefore hides Hubble and SOHO in a build made
today. Re-run `build_spacecraft.py` before any release (`cs/docs/accuracy.md` already warns of this
shelf life).

Horizons IDs for the missions V2 draws by hand (VERIFIED via the Horizons API):

| ID | Object | Note |
|---|---|---|
| `-98` | New Horizons | "Revised Sep 01, 2026" |
| `-121` | BepiColombo | ESA trajectory "fit through 2026-Aug-25, prediction thereafter" |
| `-1023` | Artemis I Orion | Launched 2022-11-16 06:47:44 UTC from pad 39B; post-launch trajectory from NASA/JSC |
| `-23` | Pioneer 10 | Already shipped |
| `-399110` | Apollo 11 **S-IVB** only | Adamo 2012 reconstruction from 1969-Jul-17 16:40. **No CSM/LM trajectory** |
| — | "Venera 7" | No matches |

---

## 7. Ideas worth carrying over, with Unity port designs

Each item follows CosmoSynth's rules:

* Core stays engine-free, with a headless test per piece.
* Runtime components clear their output on each `OnCatalogLoaded` (it runs three times).
* Anything that decorates a body checks `CelestialBodyView.IsGeometryDrawn` every frame.
* Positions are `localPosition` under the universe anchor.
* New shaders go into `CosmoShaders.All` / Always Included; the pipeline is Built-in, not URP.
* Additive effects use `ColorMask RGB` so they never write passthrough coverage.
* Sizes are in dmm via `CosmoTheme`; containers are sized from their contents.
* Every binding lives in `XrInput.Bindings`.
* Any small thing gets both a minimum and a maximum apparent size.
* A curve is drawn only from outside it, and only when it is larger than a dot.

### 7.1 Ephemeris upgrades in Core (highest value per line of code)

* `Core/Orbits/LunarTheory.cs`: Meeus chapter 47 (ELP-2000/82 truncated) returning a geocentric
  ecliptic-of-date vector, then **precessed to J2000** and rotated to ICRF with the existing
  `StandardFrames`.
  * Test against the fixture Moon (26 vectors) with a bound set by the theory. My corrected
    Schlyter reached 773 km max; Meeus should be well under 100 km (INFERRED).
  * Use it in place of the Kepler Moon everywhere. This removes CosmoSynth's documented worst case,
    and LRO, Artemis I and the Apollo site depend on it.
* `Core/Orbits/StandishElements.cs`: Table 1 (1800–2050), transcribed from V2 lines 159-178
  (Appendix A.1) or from JPL's page. Add Table 2a/2b (3000 BC – AD 3000, with the b, c, s, f terms
  for Jupiter–Pluto) for deep-time scrubbing (INFERRED that those terms exist as described).
  * Use it when `|t − fitEpoch|` exceeds the conic's validated span, or blend.
  * Test: the §4.1 bounds (≤ 0.08° through 2031) plus a 1979-03-05 Jupiter check against a new
    Horizons vector added to the fixture.
* Better still, as `accuracy.md` fix #1 recommends: sampled planet tables. 8 planets × 10-day
  sampling × 1950–2050 × 48 B ≈ **1.4 MB** (VERIFIED arithmetic). That breaks the 900-sample cap,
  which was written for spacecraft, so needs its own rule; Mercury needs a finer step.

### 7.2 Navigation and time UX (the user's evident priority)

* **Follow (co-moving)**: add `UniverseSimulation.FollowTarget`. Each frame,
  `origin = target.AbsolutePositionKm + followOffsetKm`, with the offset re-captured whenever the
  user flies or grabs, exactly like V2's `followOff` (V2 lines 1590-1597).
  * Make it the default after Go to, and show "following Earth" on the dashboard.
  * Without it, at 1 d/s warp Earth leaves a hologram in seconds (30 km/s × 86 400 ≈ 2.6 × 10⁶ km
    per real second — VERIFIED arithmetic).
* **Go to** already exists (destinations plus `FrameBodyAtSize(target, 0.55f)`); add **Free** and a
  **Follow** toggle.
* **Signed warp**: mirror `SimulationClock.WarpRates` with negative entries, and add **Now** and a
  "jump to date" (a year / month / day stepper, or mission key events).
  * Bindings: Y/B already step warp. Add a Time page, reached under X-engage, for Now, reverse and
    date.
  * Keep the ladder labels human ("a week per second").
* Wrist dashboard pages. Carry V2's information architecture — **Time / View / Go to / Missions**
  plus the category colours, restated as `CosmoTheme` tokens — into the existing wrist-turn + X
  engage model. Do not port V2's canvas-in-metres sizing: at a 0.45 m reading distance, 1 canvas px
  of V2's tablet is 0.93 dmm (VERIFIED arithmetic), so its 26 px body text is ≈ 24 dmm. Use that as
  the size target in `CosmoTheme`.

### 7.3 Missions page with playback

* List the spacecraft (24 now), each with its `KnownFrom–KnownUntil` window. "**▶ play**" sets the
  epoch to a key event, picks a warp from the ladder and a scale rung (Whole system for
  heliocentric; Planet and moons for geocentric), and follows the craft.
* V2's 1 209 600 s/s (2 weeks/s) is not on CosmoSynth's ladder; use 604 800 or 2 592 000.
* Add from Horizons (§6.5):
  * **New Horizons (-98)** with an *abstract* marker and no model. CosmoSynth dropped it for having
    no NASA model, but an honest glyph plus a Measured trajectory is not "another probe's body under
    its name".
  * **BepiColombo (-121)** and **Artemis I (-1023)**. Artemis I also replaces
    `dummyartemis.html` with real data.
* **Apollo 11**: Horizons has only the S-IVB. Author the CSM path from V2's anchors (Appendix A.4)
  as a `TrajectoryTable` labelled **Illustrative (Fictional geometry anchored to Measured events)**.
  * Launch from LC-39A on the rotating Earth using `SurfaceFixedEphemeris`; the real lift-off time
    is 13:32 UTC, not V2's 13:00.
  * Arrive at the Moon's position from `LunarTheory`; splash down at 13.32° N, 169.15° W.
* Voyager and Pioneer outbound extrapolation is unnecessary: CosmoSynth's tables already run to
  2034–2035.

### 7.4 Light-time scale rings, up the whole ladder

* `Core/Scale/ScaleRings.cs` holds the list; `Runtime/Rendering/ScaleRingRenderer.cs` draws them.
* Sun-centred ecliptic circles with labels. V2's set: 1 light-second (299 792 km), light-minute
  (1.80e7 km), light-hour (1.08e9 km = 7.2 AU), "1 AU · 8.3 light-min", light-day (2.59e10 km =
  173 AU), "30 AU · Neptune", light-month (7.88e11 km = 5 270 AU), 1 light-year (63 241 AU).
* **Extend** toward the project goal: the heliopause (~120 AU), the Oort cloud inner edge
  (~2 000 AU) and outer edge (~1–1.6 ly), 1 pc, 10 ly, 100 ly, then hand over to the galactic rings
  (Galactic Centre 26 kly, Milky Way disc ~100 kly, M31 2.5 Mly, observable universe 46 Gly).
  The Oort and Galactic distances are INFERRED; cite them before shipping.
* Visibility: V2 drew rings with a radius between 0.4 m and 8 km in the room. CosmoSynth's rule is
  stricter and better: draw only from **outside** (the camera outside the circle's disc footprint)
  and only above ~8 px (`OrbitLineRenderer.MinimumApparentPixels = 8`).
* Labels through `BodyLabelLayer`. These rings are the single best "how far is that?" device across
  22 orders of magnitude. Pair them with the dashboard's "1 km = X u" readout.

### 7.5 Belts as a GPU Kepler point cloud

* One mesh, 4 vertices per object (camera-facing quad expanded in the vertex shader). Per-vertex
  attributes: `(a, e, i, Ω)`, `(ω, M0, n, size)`. Uniforms: `daysPastEpoch`, `UnitsPerKm`, and the
  Sun's render position.
* Kepler solved in the vertex shader with 4 Newton steps (V2 lines 965-984). Float precision is fine
  at the scales where the cloud is shown: at 4.5e9 km a float resolves ~270 km, sub-millimetre in
  the room at those scales (INFERRED).
* 22.5 k objects = 90 k vertices. Star field for comparison: 37 328 vertices
  (`cs/docs/roadmap-universe.md`). Budget it; drop to the brightest 10 k first if Quest frame time
  suffers.
* Data from §6.3 (Measured). Colour by class, or by H (brightness). Fade in and out on 1 AU's render
  size as V2 does: visible while 1e-5 < AU-in-metres < 3e3, with a smooth fade.
* A shader not listed in `CosmoShaders.All` will be stripped from the build (the "everything is
  magenta" bug).

### 7.6 The heliosphere, finally drawn (plus a Parker spiral)

* `Core/Sky/Heliosphere.cs`, a pure geometry model with provenance:
  * **Nose direction**: the interstellar inflow comes *from* ecliptic λ ≈ 255.7°, β ≈ 5.1° (IBEX;
    INFERRED — cite McComas et al. 2015). The nebula-gallery file uses galactic l = 5°, b = +19° for
    the same thing.
  * **Termination shock**: Measured at the two Voyager crossings — V1 ≈ 94 AU (Dec 2004), V2 ≈ 84 AU
    (Aug 2007) (INFERRED; cite Stone et al. 2005 and 2008). Model it as an ellipsoid through those
    points, elongated downwind, labelled **Inferred model**.
  * **Heliopause**: Measured points at V1 121.6 AU (Aug 2012) and V2 ≈ 119 AU (Nov 2018) (INFERRED;
    Stone 2013 and 2019). Shape: an **axisymmetric Rankine half-body**,
    `r(θ) = r_n · √(2(1 + cos θ)) / sin θ` with θ measured from the *downwind* axis. It gives
    r = r_n at the nose, √2·r_n on the flanks and a tail asymptotic to 2·r_n (derived here, INFERRED
    — the test should assert exactly those three values). Keep "tail length unknown" in the
    provenance.
  * **Bow wave**: ~230 AU, labelled "no bow shock (IBEX)" as nebula-gallery does (INFERRED).
  * **Crossing markers**: pins where V1 and V2 crossed, anchored on CosmoSynth's own Voyager tables
    at those dates — Measured, and a very strong "this is real" moment.
* Rendering: fresnel shells with `ColorMask RGB`, drawn **only when the whole shell subtends less
  than ~90°** (viewer outside). From inside, a DoubleSide additive shell tints the whole view and
  hazes the passthrough. Add a "Heliosphere" rung (e.g. 2 × 300 AU across 0.7 m) between Whole
  system and Nearest stars.
* **Parker spiral** of the interplanetary magnetic field, from the Sun's sidereal rate Ω =
  2π/(25.38 d) = 2.865e-6 rad/s:
  * `tan ψ = Ω·r·sin θ / v_sw` — at 1 AU with 400 km/s, ψ = 47.0°; with 700 km/s, 31.5° (VERIFIED
    arithmetic).
  * Streamlines `φ(r) = φ₀ − Ω(r − r₀)/v_sw` in the rotating Sun's frame. v_sw comes from the
    snapshot (Measured at L1) or a slow/fast-wind default (Inferred).
  * An optional current sheet tilted by solar-cycle phase (Procedural; label it).
* Sun "vitals" (§6.2): spots, flares and corona driven by `SolarActivity`; the snapshot card and
  "seen from Earth" stills in the Sun inspector.

### 7.7 Slingshot gesture → trajectory editor, and the N-body launch sandbox

The user's slingshot (33 versions) is literally the **"grab-and-pull trajectory editor"** listed on
CosmoSynth's roadmap (`cs/README.md` "Then…").

* **Core**: `Core/Orbits/RestrictedNBody.cs` — a massless probe in the field of catalogue bodies
  whose positions come from the existing ephemerides. This is what `orbital_simulator.html` really
  is: restricted N-body, not N-body.
  * Adaptive Dormand–Prince 5(4) or RKF45 in double, with events for surface impact (using the
    ellipsoid radius as `Validate` does) and SOI changes.
  * **The prediction must be the same integrator and the same steps as the flight** (bug #13), so
    what is drawn is what will be flown.
* **Tests**:
  * Energy conservation in a two-body case.
  * Radial launch from Earth at 11.19 km/s escapes and at 11.0 does not.
  * A Hohmann transfer Earth → Mars takes ~259 days (INFERRED; compute the bound from the vis-viva
    ellipse rather than hard-coding).
  * An integration forward and back returns to within tolerance.
* **Runtime**:
  * `SlingshotGesture` reuses `XrHandInput`'s hysteresis pinch (22/35 mm; V2's 22/45 is an
    alternative) and V2's **release buffer** (re-use the last non-zero impulse if the release frame
    reads zero).
  * The impulse maps to Δv logarithmically across 0.1–45 km/s, borrowing the ×0.1 / ×10 idea.
  * The direction basis is prograde / normal / radial, from the launch body's state.
  * Live readout of the resulting osculating elements via `OrbitalElements.FromState`: a, e, i, q,
    Q, period, and an escape flag — something none of the web pages show.
* **Game layer (optional)**: "gravity-assist challenges" (reach Jupiter's SOI with Δv ≤ X; flyby
  bonus). An **arcade mode** keeps spacecraft.html's feel on a tabletop with abstract planets and
  GM ∝ r — the equal-escape-speed property is a neat design lever — labelled **Fictional**.
* Trail: a ring buffer, not an unbounded array (bug #12).

### 7.8 Honest presentation controls and live numbers

* Dashboard presets: **True scale** (compression 1, exaggeration 1), **Readable** (today's 0.55
  power law), **Magnified ×10 / ×100**.
  * If magnifying, co-scale satellite systems but cap the factor so the outermost moon's display
    orbit stays inside a fraction k of the planet's Hill radius, `r_H = a·(m/3M☉)^(1/3)`. This keeps
    V2's intent and fixes its ×1000 absurdity.
  * Earth's Hill radius is ≈ 1.5 × 10⁶ km, so with k = 0.5 the cap is ≈ 2× for the Moon (INFERRED).
    A strict cap may be too strict for Earth; the alternative is to accept overlap and label it.
    **Decide in design.**
* **Always show "SIZES NOT TO SCALE ×N" in the headset** (today it is desktop-only).
* Inspector "Now" section (V2 lines 1320-1335):
  * r☉ in AU and light-minutes, and Δ⊕ in AU;
  * **one-way light time from Earth** — Voyager 1 at 168 AU ≈ 23.3 h (VERIFIED arithmetic);
  * speed by vis-viva, `v = √(μ(2/r − 1/a))`, or from the state vector;
  * **phase angle**, `acos(((−r_b)·(r⊕ − r_b)) / (|r_b|·|r⊕ − r_b|))`;
  * `SizeExaggeration` for spacecraft.
* Provenance: Inferred, computed from Measured.

### 7.9 Surface overlays: winds, clouds, relief

* **Wind overlay**: an overlay sphere at 1.012 R with a Built-in shader that samples a 128×1 signed
  LUT and advects streaks at `v/(R cos φ)` × warp. Additive, `ColorMask RGB`, shown only above
  ~60 px.
  * Profiles need citations before they can be called Measured. Candidates (INFERRED): Jupiter
    Porco et al. 2003 / Tollefson et al. 2017; Saturn García-Melendo et al. 2011; Uranus and
    Neptune Sromovsky et al.; Venus Sánchez-Lavega et al. 2008.
  * V2's numbers (Appendix A.3) are plausible placeholders.
* **Earth clouds**: NASA PD map (§6.4), static, or with a slow illustrative drift labelled as such.
* **Relief**: Mars-globe-style displacement `R·(h/R_body)·exag`, but only on real DEMs (LOLA LDEM
  for the Moon; MOLA for Mars) through phase-3 terrain tiles. Until then, normal-map shading with
  an exaggeration slider. The ×1 / ×10 / ×50 presets and a label are the user's own pattern.

### 7.10 Passthrough ⇄ Space fade

* Add `XrRig.SetPassthroughBlend(float)` that eases the camera clear alpha, or draws an inside-out
  dome whose alpha is coverage, from 0 to 1 over ~0.3 s (V2: `dt·4`).
* Bind it to a dashboard toggle ("◉ Passthrough / ✦ Space").
* This reproduces V2's best UX discovery — one session, live switch — with no session restart.
* Check against `AnchorBeacon` / `PlayModeShot.ProbeDestinationAlpha` that coverage is exactly 0 in
  passthrough.

### 7.11 Spacecraft attitude

* A per-mission attitude rule in the mission table (`tools/cosmodata/spacecraft.py`): `earth`
  (HGA axis toward Earth: Voyager, Pioneer, New Horizons in cruise, Cassini in cruise), `sun`
  (JWST's shield, Parker's heat shield, SOHO), `lvlh` (ISS, Hubble), or `inertial`.
* Each rule names the model axis it drives. Provenance **Inferred** ("typical attitude"). Cheap, and
  the models stop tumbling at arbitrary orientations.

### 7.12 Catalogue gaps V2 fills

* 7 moons and 8 dwarf planets / TNOs (§5).
* **Brown dwarfs absent from HYG** (Hipparcos / Yale / Gliese predate them): Luhman 16 A/B
  (6.52 ly), WISE 0855−0714 (7.43 ly), SCR 1845−6357 (12.57 ly), DEN 1048−3956 (13.17 ly). V2's
  RA / Dec / distance are in Appendix A.5. INFERRED that they are missing — **confirm by position
  match** against `stars.csstars` before adding.
* Teegarden's Star is already present as an exoplanet host.

### 7.13 Specimen tray ("Miniature Tray Solar System")

* Keep `TabletopManipulator`'s principle — you never drag a real body — but add an explicit
  **copy**: a room-anchored shelf of ≤ 12 specimens at equal size, labelled "MODEL — not the
  simulation".
* Pinch one and it grows to hand-held size (0.22 m radius); release it near the shelf and it docks.
* It shows the body's current IAU rotation and real texture. A "true relative size" toggle switches
  to `PresentationRadius` or log sizing, because at 3.5 cm for Earth the true-scale Sun would be
  3.8 m in radius (VERIFIED arithmetic).
* Great for comparing Earth / Mars / Moon sizes and the giants side by side. It costs one component
  and no physics.

### 7.14 Mars moons done right

Phobos and Deimos are already in the catalogue with Horizons orbits. Draw them as **triaxial
ellipsoids** with IAU radii — Phobos ≈ 13.0 × 11.4 × 9.1 km, Deimos ≈ 7.8 × 6.0 × 5.1 km (INFERRED;
cite WGCCRE 2015) — instead of spheres, and tidally locked. Add the "Phobos rises in the west" fact
card: its 7 h 39 m period is shorter than the Mars sol (INFERRED).

---

## 8. What the user evidently cared about

* **The family name "Heliosphere"** (directory card: "Heliosphere V1 / V2 / Vitals — Explore a
  Solar System visualization across three spatial prototypes"), and the V2 subtitle "Real-scale
  solar system · live ephemeris · WebXR". *Real scale* and *now* are the headline promises.
* **Seamless AR ⇄ VR**, "Both modes run in one session", changed V1 → V2 within one day
  (2026-07-15 → 16).
* **Honest scale labelling**: "×1 (true scale)" is always the first option; the tablet heading says
  "moon orbits scale with it"; the Sun cap comment reads "so it never swallows Mercury". The user
  thought about what magnification does to the scene.
* **Historical and physical anchoring**: "Anchored to real launch/flyby dates & positions.", "Apollo
  11 launches from the real KSC pad on the rotating Earth and returns to the Pacific splashdown.",
  and the comment on the probe models, "high-gain dish aimed at Earth (real attitude habit)".
* **Precision claims** ("TRUE TIME-SYNCED INTEGRATION", "Fully synchronized exact integration",
  "Extremely high precision integration needed for real-world distances"). The user values
  correctness, even where the code does not fully deliver it.
* **Gesture robustness**: "Orbital Slingshot v33 - Gesture Reliability", with comments on hysteresis
  and the release buffer. Thirty-three iterations is the strongest signal in the set.
* **Live data, honestly sourced**: "nothing is simulated except nothing", and "Everything fails soft".
* **Offline resilience, already**: the Mars globe's intro says it renders "without waiting for slow
  or blocked network asset streams". The user has met network-asset failure before — and today three
  of those URLs are 404.
* **Wrist UI that follows hands and controllers alike**: "smooth wrist-follow panel: same behaviour
  for controllers AND hand tracking", with a hands-free fallback.
* The CosmoSynth feedback log (`cs/docs/feedback.md`) points the same way: "restrictive to solar
  system distance", "I can't go close…", "blue orbits is just not good", "ugly, nonsense controls",
  "make stars as stars". Navigation and legibility have been the recurring pain, which is why §7.2
  ranks second.

---

## 9. Do not port

* The procedural texture generator (V2 498-581). CosmoSynth's `SurfacePainter` is better
  (temperature and moisture first, 3D noise with no pole pinch).
* Remote texture loading of any kind, and the threex.planets / three.js textures (licence).
* `kelvinRGB` (CosmoSynth uses Ballesteros B−V), the hand-entered 56-star positions (HYG is better),
  and the hand exoplanet list (the Archive is better). Exception: brown dwarfs, §7.12.
* Waypoint Catmull-Rom trajectories where Horizons has the real thing. Only Apollo 11 needs
  authoring.
* The random flare flicker — or only as "illustrative variability", clearly labelled.
* V2's moon orbit code (bugs #1, #2, #6) and its raw-midpoint grab pivot (the 1.4.0 regression
  CosmoSynth already fixed).
* The arcade slingshot physics as anything but a labelled Fictional mode; `orbital_simulator`'s
  random phases and fixed dt; the rubber-sheet funnel. If a gravity-well visual is wanted, derive
  the depression from the actual potential Φ = −Σ GMᵢ/|r − rᵢ| and label it a diagram.
* `dummyartemis.html` entirely; Artemis I comes from Horizons instead.

---

## 10. Risks and open questions

1. **The headset regression gates everything.** 1.7.0 was reported as "100 plus isntances of earth…
   repeated ona grid flat" (verbatim, per the task), and it was never confirmed fixed. Every feature
   above that adds a camera, an additive pass or a large transparent shell (heliosphere, winds,
   dome, belts) should land only after that is resolved, and each needs its own headset check. The
   play-mode harness renders flat and mono (`cs/docs/roadmap-universe.md`).
2. **APK budget** (43 MB, roadmap):

   | Item | Size |
   |---|---|
   | Small bodies | ~0.4–0.7 MB |
   | Sun activity + snapshot + stills | ~0.7 MB |
   | Planet tables, if chosen | ~1.4 MB |
   | New Horizons / Bepi / Artemis tables | ~0.25 MB each (INFERRED from the Voyager table's 5 238 samples × 48 B) |
   | Textures | 0.5–0.8 MB each at 2k |

   Fine, but track it.
3. **Licences**:
   * HYG is already CC BY-SA.
   * Solar System Scope is CC BY 4.0 (to verify).
   * NOAA / NASA are generally PD, but SOHO is ESA/NASA and some imagery carries credits.
   * SBDB follows Horizons' terms.
   * Record each in `docs/data-sources.md` before shipping (the project's own rule).
4. **Moon theory choice**: Meeus 47 is ~120 terms of tables. The corrected Schlyter is ~20 terms and
   measured at ≤ 773 km over 2026–31. Either is a large improvement; measure before claiming.
5. **Presentation decision**: V2-style magnification plus moon co-scaling versus CosmoSynth's power
   law. Both are lies; the rule is only that the headset must say which lie is active.
6. **Freshness**: snapshots and spacecraft windows age. Put the build date and "as of" on screen,
   and re-run the importers each release.
7. My SBDB counts came from a quick probe whose filter quoting may have been mangled. The API is
   verified; the counts are indicative.

---

## Appendix A — data worth reusing (numbers are facts; sources noted)

### A.1 Standish Table 1 (1800–2050), as in V2 lines 159-178 (`[a AU, e, I°, L°, ϖ°, Ω°]` + rate/century)

```
mercury [0.38709927,0.20563593,7.00497902,252.25032350,77.45779628,48.33076593] [0.00000037,0.00001906,-0.00594749,149472.67411175,0.16047689,-0.12534081]
venus   [0.72333566,0.00677672,3.39467605,181.97909950,131.60246718,76.67984255] [0.00000390,-0.00004107,-0.00078890,58517.81538729,0.00268329,-0.27769418]
EMB     [1.00000261,0.01671123,-0.00001531,100.46457166,102.93768193,0.0] [0.00000562,-0.00004392,-0.01294668,35999.37244981,0.32327364,0.0]
mars    [1.52371034,0.09339410,1.84969142,-4.55343205,-23.94362959,49.55953891] [0.00001847,0.00007882,-0.00813131,19140.30268499,0.44441088,-0.29257343]
jupiter [5.20288700,0.04838624,1.30439695,34.39644051,14.72847983,100.47390909] [-0.00011607,-0.00013253,-0.00183714,3034.74612775,0.21252668,0.20469106]
saturn  [9.53667594,0.05386179,2.48599187,49.95424423,92.59887831,113.66242448] [-0.00125060,-0.00050991,0.00193609,1222.49362201,-0.41897216,-0.28867794]
uranus  [19.18916464,0.04725744,0.77263783,313.23810451,170.95427630,74.01692503] [-0.00196176,-0.00004397,-0.00242939,428.48202785,0.40805281,0.04240589]
neptune [30.06992276,0.00859048,1.77004347,-55.12002969,44.96476227,131.78422574] [0.00026291,0.00005105,0.00035372,218.45945325,-0.32241464,-0.00508664]
pluto   [39.48211675,0.24882730,17.14001206,238.92903833,224.06891629,110.30393684] [-0.00031596,0.00005170,0.00004818,145.20780515,-0.04062942,-0.01183482]
```

Evaluation: `T = (JD − 2451545)/36525`, element = e₀ + rate·T; ω = ϖ − Ω, M = L − ϖ; solve
`E − e sin E = M`; `x' = a(cos E − e)`, `y' = a√(1−e²) sin E`; rotate by ω, i, Ω to ecliptic J2000;
rotate by ε = 84381.448″ (IAU 1976, what Horizons uses; V2 uses 23.43928°) to ICRF.

### A.2 Kirkwood gaps (V2 line 994; VERIFIED by Kepler III against a_J = 5.2029 AU)

3:1 → 2.502 AU · 5:2 → 2.825 · 7:3 → 2.958 · 2:1 → 3.279 (`a = a_J·(q/p)^{2/3}`).

### A.3 Zonal wind profiles (V2 lines 465-474; m/s, east positive; placeholders pending citations)

```
jupiter (max 180): -80:-10 -65:25 -55:-25 -45:40 -38:-40 -30:45 -24:-55 -18:35 -12:-45 -6:80 0:105 6:120 10:-20 16:-30 21:35 24:175 28:-20 35:30 43:-25 50:35 60:-15 75:10
saturn  (max 430): -80:10 -60:80 -45:20 -35:120 -25:60 -15:200 0:420 15:200 25:60 35:120 45:20 60:80 80:10
neptune (max 410): -70:250 -50:150 -20:-250 0:-400 20:-250 50:150 70:250
uranus  (max 210): -70:200 -40:80 0:-100 40:80 70:200
venus   (max 100): -85:-10 -70:-40 -50:-80 -30:-95 0:-95 30:-95 50:-80 70:-40 85:-10
earth   (max 45):  -70:5 -60:15 -45:28 -35:35 -25:5 -10:-6 0:-5 10:-6 25:5 30:30 40:38 50:25 60:15 75:5
mars    (max 40):  -60:20 -40:35 -20:10 0:-10 20:10 40:35 60:20
```

### A.4 Mission anchors (V2 lines 405-447) — for "play" key events and the Apollo 11 authoring

* Voyager 1: launch 1977-09-05 · Jupiter 1979-03-05 · Saturn 1980-11-12 · 2026-01-01 RA 261.9°
  Dec +12.25° 168 AU.
* Voyager 2: 1977-08-20 · J 1979-07-09 · S 1981-08-25 · U 1986-01-24 · N 1989-08-25 · 2026-01-01
  RA 301.0° Dec −58.9° 140.3 AU.
* New Horizons: 2006-01-19 · J 2007-02-28 · Pluto 2015-07-14 · 2026-01-01 RA 288.0° Dec −20.5°
  62.4 AU.
* Pioneer 10: 1972-03-02 · J 1973-12-03 · 2026-01-01 RA 76.0° Dec +25.9° 137.0 AU (Horizons says
  launch 1972-03-03 01:49 UTC — VERIFIED).
* Cassini: 1997-10-15 · V 1998-04-26 · V 1999-06-24 · E 1999-08-18 · J 2000-12-30 · S 2004-07-01 ·
  end 2017-09-15.
* Venera 7: 1970-08-17 → Venus 1970-12-15.
* BepiColombo: 2018-10-20 · E 2020-04-10 · V 2020-10-15 · V 2021-08-10 · Me 2021-10-01,
  2022-06-23, 2023-06-19, 2024-09-04, 2024-12-01, 2025-01-08 · MOI 2026-11-15.
* Apollo 11: LC-39A 28.608° N 80.604° W; launch 1969-07-16 (13:32 UTC real); parking orbit
  ~190 km; lunar orbit radius 1 849 km (~112 km altitude); site 0.674° N 23.473° E; splashdown
  13.32° N 169.15° W, 1969-07-24.

### A.5 Nearest-star supplement candidates (V2 lines 329-386: name, RA h, Dec °, ly, spectral)

Luhman 16 A 10.8210 h −53.319° 6.52 ly L7.5 · Luhman 16 B 10.8210 −53.321 6.52 T0.5 ·
WISE 0855−0714 8.9235 −7.244 7.43 Y2 · SCR 1845−6357 18.7519 −63.963 12.57 M8.5 ·
DEN 1048−3956 10.8100 −39.938 13.17 M8.5.

The 12 flare stars V2 marks: Proxima Cen, Wolf 359, UV Ceti, BL Ceti, AD Leonis, DX Cancri,
Kruger 60 B, Ross 154, EZ Aquarii, Ross 248, Wolf 424 A, GJ 1245 A.

### A.6 Mars surface pins (Mars globe lines 119-136; lat, east lon)

* Landmarks: Olympus Mons 18.65, −133.8 · Valles Marineris −13.9, −59.2 · Hellas Planitia −42.4,
  70.5 · Ascraeus Mons 11.9, −104.4 · Pavonis Mons 1.4, −113.1 · Arsia Mons −8.3, −120.5.
* Landers: Perseverance 18.38, 77.58 (2021, active) · Curiosity −4.58, 137.44 (2012, active) ·
  Viking 1 22.48, −49.97 (1976) · Viking 2 48.27, 134.26 (1976) · Sojourner 19.13, −33.22 (1997) ·
  Opportunity −1.95, −5.53 (2004) · InSight 4.5, 135.62 (2018).
* V2 gives Perseverance as 18.44, 77.45.
* Cross-check all of these against the published landing-site coordinates before shipping (INFERRED
  that the small disagreements are rounding).

### A.7 Space-weather scales (Vitals, VERIFIED by reading)

* X-ray class from flux F (W/m², 0.1–0.8 nm): X ≥ 1e-4, M ≥ 1e-5, C ≥ 1e-6, B ≥ 1e-7, else A.
  The number is F divided by the threshold.
* G-scale: G0 if Kp < 5, else min(5, round(Kp) − 4).
* S-scale: S0 if p < 10 pfu, else min(5, ⌊log₁₀(p/10)⌋ + 1).
* Bz alert colours at ≤ −8 nT (red) and ≤ −4 nT (amber).

---

## Appendix B — CosmoSynth facts used in this report (VERIFIED)

* Catalogue sizes:
  * `solar_system.json`: 31 bodies, 88 kB.
  * `spacecraft.json`: 24 craft, 979 kB, with windows as listed in §6.5.
  * `exoplanets.json`: 180 records, 60 stars + 120 planets, 330 kB.
  * `sky/stars.csstars`: 2.87 MB; `stars_named.json`: 57 kB.
  * `surfaces/`: `earth.jpg` 239 kB, `moon.jpg` 139 kB.
* Warp ladder (`SimulationClock.cs:29-46`): 0, 1, 10, 60, 600, 3600, 21600, 86400, 604800, 2592000,
  31557600, 315576000, 3.15576e9, 3.15576e11, 3.15576e13 s/s.
* Scale rungs (`WristDashboard.cs:93-102`), each as the span shown across a room size in metres,
  centred on the focus body, its primary, or the Sun:
  * Held in hand: 2 R⊕ → 0.28 m (focus).
  * Room-scale planet: 2 R⊕ → 2.5 m (focus).
  * Planet and moons: 2 × 384 400 km → 0.55 m (primary).
  * Inner system: 2 × 1.6 AU → 0.6 m (star).
  * Whole system: 2 × 31 AU → 0.7 m (star).
  * Nearest stars: 2 × 12 ly → 0.7 m (star).
  * Solar neighbourhood: 2 × 130 ly → 0.7 m (star).
* Bindings (`XrInput.cs:58-69`): L stick fly · R stick rise·turn · grip hold·scale · R trigger
  select · L trigger boost · X dashboard · A recentre · Y/B time warp · R click pause.
* Presentation: `sizeCompression` 0.55 (`CosmoSynthBootstrap.cs:65`), `PresentationRadius =
  R_ref·(R/R_ref)^c` with R_ref = R⊕. For example, Jupiter draws at ≈ 24 000 km and the Moon at
  ≈ 3 100 km (VERIFIED arithmetic). Moons stay outside their compressed primaries: Phobos at
  9 376 km vs Mars drawn at ≈ 4 500 km.
* Body minimum apparent size 2.5 px (`CelestialBodyView.cs:300`); orbit minimum 8 px
  (`OrbitLineRenderer.cs:288`); orbits drawn only from outside (`OrbitLineRenderer.cs:273`).

---

## Appendix C — measurement script (run as `/tmp/cs_helio_measure.py` on triton)

Reads only `cs/tests/CosmoSynth.Core.Tests/fixtures/horizons_validation.json`. Key parts:

```python
AU=1.495978707e8; DEG=math.pi/180; J2000=2451545.0; OBLIQ=23.43928*DEG
# planetPosKm: Standish Table 1 (A.1) exactly as solar-xr2.html:179-189
# moonGeoKm:   Schlyter series exactly as solar-xr2.html:197-220 (returns ecliptic-of-date km)
# precession variant: rotate the Moon vector about the ecliptic pole by -1.396971*T deg (T = Julian centuries)
# ceresKm:     a=2.7666 e=0.0785 i=10.587 Om=80.27 w=73.74 M0=60.0@J2000 P=4.60 yr (solar-xr2.html:283)
def ecl2eq(p):  # ecliptic J2000 -> ICRF equatorial, same obliquity solar-xr2 uses
    x,y,z=p; c,s=math.cos(OBLIQ),math.sin(OBLIQ); return (x, y*c - z*s, y*s + z*c)
for body in fixture['bodies']:
    for v in body['vectors']:            # {'jdTdb', 'positionKm', 'velocityKmPerSec'}
        err = |ecl2eq(model(v['jdTdb'])) - v['positionKm']|   # km; angle = acos of normalised dot
```

Output reproduced in §4.1. Runtime is well under a second.
