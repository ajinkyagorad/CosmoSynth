# Fictional and phenomena content: inventory and port plan

*Research note, 2026-09-11, slug `fictional-and-phenomena-port`. Read-only phase. Nothing
under `~/projects` was modified except this file; nothing was built, rendered or launched.*

**Evidence legend.** **VERIFIED** = I read the code or data, or ran a light command (curl,
sha256sum, git diff --shortstat, arithmetic on values in the file). **INFERRED** = reasoning or
prior knowledge, not executed here. Line numbers refer to the files as they are on disk today.

---

## 0. Verdict on one screen

| Source | What it is | Real vs fictional | Verdict | Where it lands in CosmoSynth |
|---|---|---|---|---|
| `holographic_star_map*.html` (3 variants) | Neon holo-table: 8 invented "star/planet nodes", flowing filament orbits, projector base | 100 % fictional (variant 0 is headed `SYS_MAP // MARVEL_COSMOS`) | **Port the look and the system, as Fictional mode.** Base = variant **(2)** (the most complete), plus the names and rings from variant (0). Drop its input code and fake diagnostics | A Fictional catalogue loaded through the existing loader and validator, shown only as a tabletop hologram with a projector "skin" that means *this is not the sky* |
| `html/astrophysical_nebula_gallery.html` | Raymarched 64³ volumes of Orion / Local Bubble / Crab, random "Gaia" stars, fake spectrometer, Gemini chat | Real names, RA/Dec and rough distances. **Everything volumetric or numeric is invented, but labelled as data** ("Gaia DR3", "3D tomography", "atoms/cm³") | **Port the renderer idea and the three analytic fields as Procedural close-ups at Measured positions.** Drop the fake stars, the probe, the Doppler mode, the random B-fields and the LLM chat | Nebula layer inside the Galactic band, with new scale rungs; the real dust cube is the Measured base |
| `Fable5/nebula-gallery/index.html` "Milky Way Atlas" (**found during this survey**) | Real-data WebXR atlas: Leike/Lallement dust cube, HYG, Cepheids, globulars, 911 galaxies, wrist menu | Real by design, with a written "honest limits" section | **The data blueprint for the nebula and Milky Way layers.** The detailed port belongs to the Milky-Way research; noted here because it is the correction of the nebula gallery | Source of nebula positions (`data/markers.json`) and of the scale rungs |
| `html/cosmic_modular_synthesizer.html` | 2D DOM modular synth (Bitwig-Grid homage), ambient "sleep at the edge of the universe" preset | No astronomy at all; sound design only | **Port the DSP recipe and the preset as the bed of a new sonification layer.** Drop the patch-cable editor | New `Core` DSP + one `OnAudioFilterRead` source + Unity's built-in filters; data-driven voices (pulsars, GW chirp) |
| `cosmic-portal-test.html` + `js/cosmic-portal-original.js` | Desktop-only neon portal with bloom; **the test page's `js/cosmic.js` does not exist** | 100 % fictional, no XR | **Drop.** At most, reuse the expanding pulse-ring as a transition cue | none |
| `arcane_astral*.html` (3) | Hand-tracked Doctor-Strange-style glyph mandalas | Not star or cosmos content | **Excluded** (checked, not relevant) | none |
| `~/projects/CosmicAtlasXR` (live at build.nebulytic.com/cosmos/) | Nine honest phenomenon exhibits: black hole, merger, lensing, N-body galaxy encounter, cosmic web, dark-matter halo, stellar interior, reconnection, Alfvén wave | Real physics laws and ratios, normalized parameters, schematic visuals, **each with a written limits statement** | **Port all nine, the highest-value source.** Physics into `Core` with its tests, the N-body capture as a StreamingAsset, and the texts verbatim | A "Phenomena" list on the wrist dashboard; four exhibits also anchored to real hosts (the Sun ×3, Sgr A*) |
| `~/projects/StarHolo` + `StarHoloXR` | Sci-fi starship HUD (web) and its Unity 6 URP twin | Fictional; its only real element is the Helsinki clock | **Do not port the scenes.** Reuse the mesh helpers and the noise math after rewriting for Built-in. Heed its traps | A shader-math donor for the Fictional skin and the stellar-interior granulation |

The four integration asks all have a lazy, coherent answer that reuses what CosmoSynth already
has: provenance, catalogue JSON, `WristDashboard`, `TabletopManipulator`, `CosmoShaders.All`,
star sprites and orbit ribbons. The new code is limited to roughly four to six Built-in shaders
(filament, beam, nebula raymarch, accretion disk, granulation), a `Core/Phenomena` module, a
`Core/Audio` module and one audio MonoBehaviour. See §4.

---

## 1. Scope and method

Read in full on the laptop (VERIFIED):

| File (under `D:\PhD_Backup\OneDrive - Aalto University\Projects\Nebulytic\`) | Bytes | Lines |
|---|---|---|
| `webxr-examples\holographic_star_map.html` | 29 531 | ~684 |
| `webxr-examples\holographic_star_map(1).html` | 24 550 | ~591 |
| `webxr-examples\holographic_star_map(2).html` | 30 073 | ~706 |
| `webxr-examples\html\astrophysical_nebula_gallery.html` | 64 787 | 1 247 |
| `webxr-examples\html\cosmic_modular_synthesizer.html` | 76 107 | 1 650 |
| `cosmic-portal-test.html` | 6 445 | 192 |
| `js\cosmic-portal-original.js` (the implementation the test page is meant to load) | 23 391 | 625 |
| `webxr-examples\arcane_astral.html`, `arcane_astral2.html` | 7 498 / 11 308 | 186 / 237 |
| `webxr-examples\arcane_astral_multi.html` (skimmed: title + grep) | 14 173 | — |
| `webxr-examples\Fable5\nebula-gallery\index.html` (lines 1–420 read, rest grepped), `README.md` (data and limits sections), `data\markers.json`, `data\dust_meta.json` | 76 124 | — |
| `js\cosmic_ref.html` (grepped only: the same `CosmicPortalVisualization` class inlined, three 0.166, no XR) | 29 780 | — |

Read on triton (VERIFIED): `~/projects/CosmicAtlasXR/{README.md, VALIDATION.md, public/SOURCES.md,
src/exhibits.js, src/physics.js, src/catalog.js, src/observatory.js, src/materials.js,
tools/capture.py, public/data/encounter.json}`; `~/projects/StarHolo/{CLAUDE.md, README.md}`;
`~/projects/StarHoloXR/{CLAUDE.md, FEEDBACK-NOW.md, Assets/StarHolo/Shaders/SHLine.shader,
SHPlanet.shader}` plus grep over all six StarHolo shaders and `StarHoloFx.cs`. CosmoSynth:
`CLAUDE.md`, `docs/{roadmap-universe,architecture,feedback}.md`, `docs/data-sources.md` (planned
section), memory notes (status, shader stripping, UI, empty scene, headless verification),
`Core/Catalog/Provenance.cs`, `Runtime/Rendering/CosmoShaders.cs`, parts of `WristDashboard.cs`,
`CosmoTheme.cs`, `ScaleSpace.cs`, and grep of `Runtime/Shaders/*.shader`.

Light commands run (VERIFIED): `git diff --no-index --shortstat` between the three star maps;
`curl https://build.nebulytic.com/cosmos/` → 200 (4 799 B) and `…/cosmos/data/encounter.bin` →
200 (1 486 488 B); `sha256sum encounter.bin` = `d5faf6d5…45ca78`, which matches
`encounter.json`. Web checks: Crab pulsar spin frequency, Local Bubble size (Zucker et al. 2022),
and the Edenhofer 2023 dust map licence (URLs in §10).

Nothing was rendered. Every visual claim below comes from reading shader and scene code.

---

## 2. CosmoSynth facts that constrain any port (VERIFIED from the repo)

1. **Built-in render pipeline, not URP.** `CLAUDE.md:196`; `ProjectSettings/GraphicsSettings.asset:55`
   `m_CustomRenderPipeline: {fileID: 0}`. StarHoloXR, by contrast, is URP (`…:103` points to a
   pipeline asset), so its shaders cannot be dropped in (§3.8).
2. **The scene is built at runtime, so every shader must be listed in `CosmoShaders.All`**
   (`Runtime/Rendering/CosmoShaders.cs:49`). `XrSetup` writes that list into Always Included, and
   anything missing renders magenta on device only. StarHoloXR 0.1.10 shipped two magenta stereo
   quads for exactly this reason (`StarHoloXR/FEEDBACK-NOW.md`).
3. **Mixed-reality alpha rule.** Additive effects use `Blend One One, Zero One`, which adds colour
   but leaves destination alpha untouched, because the Quest compositor reads alpha as coverage
   (`Runtime/Shaders/CosmoAtmosphere.shader:17-19,50`, `CosmoRim.shader:14,32`). **Every source
   in this inventory breaks this rule**: three.js `AdditiveBlending` and StarHolo's `Blend One One`
   both add alpha. `PlayModeShot.ProbeDestinationAlpha` turns the violation into a number
   (`CLAUDE.md`, "Needs a headset is often measurable").
4. **Stereo:** single-pass instanced. Custom shaders carry `#pragma multi_compile_instancing`,
   `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`,
   `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO` and `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`
   (`CosmoAtmosphere.shader:72-140`). Copy that skeleton.
5. **Provenance** (`Core/Catalog/Provenance.cs:17-32`): `Unknown, Measured, Inferred, Procedural,
   Fictional`. Fictional is documented as *"Authored for narrative or teaching purposes. Makes no
   claim about reality."* Provenance is recorded **per field** (`ProvenanceRecord`), with
   `WeakestClass()` (`:152`) giving the object badge. Theme colours are in `Runtime/UI/CosmoTheme.cs:71-74`
   (Fictional = RGB 0.894/0.541/0.847, a pink-violet) and glyphs in `:84-87` (Measured `=`,
   Inferred `~`, Procedural `*`, Fictional `?`). `FieldProvenance.Fictional(author)` and
   `.Procedural(generator, seed)` exist (`:80,83`).
6. **UI rules** (`CLAUDE.md`, Interface): sizes are angular (dmm); containers are sized from their
   contents (`CosmoPanel.SetContent`); **all bindings live in `XrInput.Bindings`**, and the left
   stick is arbitrated by `ClaimNavigationStick`. `WristDashboard` already has a scale-rung list
   and a destinations list, with separate glance and engage states (`WristDashboard.cs:1-60,93-102`).
7. **No audio exists.** The only audio object is the `AudioListener` at `Runtime/XR/XrRig.cs:75`.
   (grep for `AudioSource|AudioClip|OnAudioFilterRead` found nothing else.)
8. **No runtime network. Importers are stdlib-only Python**, with one documented exception
   (`CLAUDE.md`, Data).
9. **Scale:** `ScaleSpace.DefaultBands` (`Core/Scale/ScaleSpace.cs:163-167`) runs to a Galactic
   band of 1e15–1e22 km. The wrist rungs stop at "Solar neighbourhood", 2×130 ly (`WristDashboard.cs:93-102`).
10. **Roadmap Phase 4** (`docs/roadmap-universe.md`): deep-sky content is *"Procedural over a
    Measured position, and must be labelled that way."*
11. **Phase 0 (the "100 plus instances of earth" report) is still open.** Nothing in this port
    should ship before that is diagnosed.
12. **User complaints not to repeat** (`docs/feedback.md`): "thin lines everywhere", and stars as
    "square looking patches". Every web source here draws 1-px `GL_LINES` and `gl_PointSize`
    points, so the port must use CosmoSynth's ribbon and star-sprite machinery instead.

---

## 3. Source-by-source inventory

### 3.1 Holographic Star Map (three variants)

**Diff (VERIFIED, `git diff --no-index --shortstat`).** base→(1): +326/−419; (1)→(2): +256/−141;
base→(2): +446/−424. So (2) evolved from (1), and the base file is the earliest. That ordering is
INFERRED from content, because all three mtimes are the 2026-09-10 copy date.

| | `holographic_star_map.html` (base) | `(1)` | `(2)` — **most complete** |
|---|---|---|---|
| Header | `SYS_MAP // MARVEL_COSMOS` (l.44) | `SYSTEM_MAP // ACTIVE` | `SYSTEM_MAP // ACTIVE`, "Orbit-aligned trajectories, static 3D HUD pointers, Quest 3 dual-controller grab, move, and scale" (l.33) |
| Nodes | 8, static `pos`, **with names** (l.142-151) | 8, static, tighter layout (l.109-118) | VG-429 at the centre + 7 on **inclined ellipses** (a, b, i, speed) (l.117-126) |
| Star look | `FresnelShader` (l.89-110): `pow(c − N·dir, p)`, c=0.8, p=2.2 pulsing ±0.3, **centre-bright**; plus a wireframe overlay; wire rings on 5 nodes (l.317-329) | `StarGlowShader`: `1 − max(N·V,0)` to the power 1.8, **rim-bright**; plus a canvas radial-glow sprite ×3.5 | same shader to the power 2.0; sprite ×3.8, pulsing ±5 % |
| Labels | camera-facing sprite, dark box, "PLANETARY NODE" | sprite, dark box + corner brackets, "PLANET NODE // ESTABLISHED" | **fixed 3D plane** (not billboard), tilted −0.15 rad, bracket art, "SYS_NODE // REC_OK", plus a vertical pointer line (l.321-348) |
| Paths | 3 closed CatmullRom "energy trails", tube r 0.008 with a dashed pulse shader, plus a wireframe copy (l.398-473) | 3 bundles of helical threads (4/3/3), tube r 0.0035 (l.362-457) | 2 helical threads **per orbit**, tube r 0.002 (l.402-467) |
| Base | ring + grid + beam | octagonal plate, border, emitter, grid, beam (l.186-259) | same, solid plate (l.202-273) |
| Dust | 350 coloured points in a ball | 200 | 250 |
| Panel | "PLANETARY METRICS" side panel with **random** velocity (l.559) | none | none |
| XR | `immersive-ar`, no input | `immersive-ar`, no input | `immersive-ar` + trigger grab near the base, one-hand move/yaw, two-hand scale 0.15–2.5 (l.512-601) |

**Merged fictional data** (names from base, orbits from (2); `e = √(1 − b²/a²)` computed here):

| id | name (base) | colour | radius | a | b | incl (rad) | speed | e | rings (base) |
|---|---|---|---|---|---|---|---|---|---|
| VG-429 | Alpha Core Prime | #ff7a1a | 0.14 | 0 | 0 | 0 | 0 | — | yes |
| PF-919 | Chrono Frontier | #ff4f1a | 0.08 | 0.35 | 0.30 | 0.15 | 0.30 | 0.515 | yes |
| DA-125 | Desert Sentinel | #ee9a44 | 0.06 | 0.52 | 0.45 | −0.22 | 0.20 | 0.501 | no |
| AG-514 | Sub-Zero Basin | #4dd2ff | 0.09 | 0.75 | 0.65 | 0.35 | 0.15 | 0.499 | yes |
| CH-024 | Gaseous Nebula Orbit | #2b8eff | 0.05 | 0.90 | 0.80 | −0.10 | 0.10 | 0.458 | no |
| AZ-704 | Outer Core Ring | #ffa200 | 0.085 | 1.10 | 0.95 | 0.45 | 0.08 | 0.504 | yes |
| AR-503 | Deep Boundary | #2b77ff | 0.10 | 1.30 | 1.10 | −0.30 | 0.06 | 0.533 | yes |
| DD-210 | Searing Fragment | #ff3b1a | 0.04 | 0.22 | 0.18 | 0.05 | 0.50 | 0.575 | no |

**Algorithms (VERIFIED, variant (2)):**
- Orbit point (l.276-285): `x = a cos θ`, `z' = b sin θ`, `y = z' sin i`, `z = z' cos i`. The
  ellipse is **centred on the star, not focused on it**, and θ advances uniformly:
  `θ += speed·0.015` **per frame** (l.678). At 72 Hz PF-919 laps in 19.4 s and DD-210 in 11.6 s;
  at 90 Hz both run 25 % faster. Speeds are Kepler-ish: `speed·a^1.5` spans 0.052–0.097.
- Helical filament (l.424-443): `p(u) + A[cos φ · N̂ + sin φ · B̂]`, with `φ = 20πu + πk/n`,
  `A = 0.009`, `N̂ = normalize(T × up)` and `B̂ = normalize(T × N̂)` (up swapped when |T·up| > 0.9).
  Variant (1) uses `φ = 16πu + 2πk/n` and `A = 0.015 + U(0, 0.012)`.
- Flow shader (l.99-111): `f = ½ + ½ sin(u·R − t·s)`; `b = f⁸`; `rgb = mix(0.2c, 2c, b)`;
  `α = 0.4 + 0.6b` (R = 30, s = 2–4.5). The phase travels along u, so it **shows direction of motion**.
- Projector beam (l.258-265): `fade = (1 − v)⁴`; `stripes = step(0.98, sin(64u))`;
  `rgb = mix((0, .5, 1), (1, .4, 0), 0.5v)`; `α = fade·(0.07 + 0.1·stripes)`.
- Two-hand scale (l.585-600): `s = clamp(s₀ · d/d₀, 0.15, 2.5)`, placed at the midpoint of the hands.

**Visual style.** Near-black ground, cyan (#0ea5e9) and orange (#ff7700) neon, additive
everything, monospace bracket labels, an octagonal projector with an upward beam. It reads as a
"hologram on a table".

**What broke or is wrong** (VERIFIED by reading unless marked):
1. Centred ellipses and uniform angular speed: not Kepler (no focus, no second law).
2. Frame-rate-dependent orbit speed (l.678).
3. One-hand yaw reads `controller.rotation.y`, an Euler angle, so tilting the controller makes
   the yaw jump (l.582; INFERRED effect). The grab test is "controller within 0.8 m of the plate
   centre" in unscaled world units (l.548).
4. (2)'s labels are fixed `DoubleSide` planes, so they read mirrored from behind.
5. Base: the "Velocity" column is `Math.random()` at load time; "Radius 0.18M Km" is meaningless.
6. All additive materials add alpha (`AdditiveBlending`), which breaks the MR alpha rule in §2.3.
   The effect on Quest passthrough is INFERRED.
7. Everything is 1-px lines or points (grid, borders, wireframes).
8. **IP:** base says "MARVEL_COSMOS", and comments cite an absent reference image
   (`image_18a7bd.jpg`, base l.222). Rename everything before anything ships.

**How it should appear in CosmoSynth.** As **Fictional mode** (§4.2): the eight-node system
becomes a small catalogue JSON whose every field is `Fictional`, loaded through the existing
`CelestialCatalog` and validation path. That gives real conic orbits and depth/LOD rules for free
and fixes defects 1–3. It is shown only as a tabletop hologram, grabbed with the existing
`TabletopManipulator`. The projector base, beam and filament orbit lines become a
**hologram skin** whose meaning is "this is authored, not observed". The eccentricities above
(0.46–0.58) would make focus-centred orbits overlap radially: DD-210 reaches 0.35 while PF-919
comes in to 0.17. So either set e ≤ 0.1 (a new authored value, still Fictional) or treat the
paths as decorative. INFERRED that `Validate()` would object to the current values.

**Port:** the system data, the names, the skin (beam, base, flow-filament shader, bracket
labels). **Drop:** the page input code (CosmoSynth has `XrInput` and `TabletopManipulator`), the
random diagnostics, the Marvel naming, the dust ball (use the real star field), and the
centre-bright Fresnel variant.

---

### 3.2 Astrophysical Nebula Gallery (`html/astrophysical_nebula_gallery.html`)

**Features.** Three targets (Orion M42 plus the Trapezium, the Local Bubble, the Crab M1). Each
is a 64³ RGBA volume generated on the CPU and raymarched in a `BoxGeometry(20)`. Other features:
- a random star cloud labelled "Gaia Stars" (up to 8 000);
- 80 "B-field" helices;
- a parsec grid;
- layer toggles (gas, dust, stars, B-fields) and sliders (steps 32–128, density 0.1–4, star count);
- a "Spectrometric Probe" panel;
- an "AI Astro-Analyst" chat through the Gemini API;
- an "MR Enter" button.

**Algorithms (VERIFIED):**
- Value noise (l.348-378): `h = frac(sin(12.9898x + 78.233y + 37.719z)·43758.5453)`, smoothstep
  trilinear. fBm (l.381-393): frequency ×2.1, amplitude ×0.5, normalized by the amplitude sum;
  3 octaves (4 for the Crab).
- Fields, with `p ∈ [−1,1]³`, `r = |p|` and `n` = fbm (l.396-478):
  - **Orion:** `dC = |p − (0,0,0.25)|`; `cloud = z > −0.1 ? exp(−0.7(z + 0.1)) : 0`;
    `ion = max(0, 1 − 5|dC − 0.4|)`; `R(Hα) = (0.9·ion + 0.3·cloud)·n`;
    `G([O III]) = 0.8·max(0, 1 − 2.2·dC)·n`; `B(dust) = 0.9·cloud·(1 − 0.5·ion)·n`.
  - **Local Bubble:** `b = 0.5 + 0.15 sin 3θ + 0.1z²`; `R = r < b ? 0.2(1 − r/b) : 0`;
    `G = max(0, 1 − 4|r − b|)·n·150/255`; `B = (r > b ? exp(−2(r − b)) : 0)·n`.
  - **Crab:** `shell = max(0, 1 − 6.5|r − 0.6|)`; `core = max(0, 1 − 1.8r)(1 − 0.3n)`;
    `R = shell·n`; `G = 0.4·shell + 0.6·core`; `B = 0.9·core`.
  - In all three, `A = max(R, G, B)`.
- Raymarch (l.491-596): analytic slab test against ±0.5; front-to-back compositing
  `α_i = 1 − exp(−15ρΔs)`, `C += (1 − A)α_i·c_i`, `A += (1 − A)α_i`; exits early at A ≥ 0.95.
  The composite mode (the only one reachable) uses emission `R·(0.95,0.25,0.35)·1.5 + G·(0.15,0.85,0.75)·1.2`,
  dust extinction `2.2B`, dims the emission by `max(0.05, 1 − 0.8·ext)`, and sets
  `ρ = 0.3(R + G) + 0.3·ext`.

**Claim audit** (the core problem with this page is that it presents invention as data):

| UI claim | What the code does | Lines |
|---|---|---|
| "Gaia Stars", "Gaia DR3 catalogs", "real gas cubes" | Random points with 5 fixed colour buckets; analytic fields × noise | 716-812, 396-478, 276 |
| "3D TOMOGRAPHY", "Lallement Dust Tomography Boundary" | Labels only | 131, 1073 |
| Probe: "~22,500 atoms/cm³", "Trapezium Stellar Core" | Thresholds on camera distance from the origin (<15, <28 …) | 1037-1087 |
| Spectral bars Hα / [O III] / Hβ / extinction | Static inline heights 65/40/15/85 %, never updated | 226-231 |
| "Spectroscopic Doppler velocity mapping" (mode 3) | A recolour of the same channels, with no velocity field. **Modes 0, 1 and 3 are unreachable**: `uVisualizationMode` is fixed at 2 (grep: only l.299, 310) | 568-572 |
| "B-Fields" | 80 random helices, `r = r₀ + 2 sin(πs)`, `φ = φ₀ + 1.5s` | 833-883 |
| "AI Astro-Analyst · GEMINI LIVE" | `fetch` with an empty key (`apiKey = ""`, "provided automatically by Canvas"), falling back to template text | 1110-1177 |
| "MR Enter" | Checks `immersive-vr`, then calls `renderer.xr.startPresenting()`, which three r128's WebXRManager does not have (INFERRED from the three.js API); it also asks for VR, not AR | 942-955 |
| "Stellar Instancing Limit" | Regenerates a different random cloud | 927-933 |

**Numbers, checked:**
- M42 RA 05h35m17s, Dec −05°23′28″ and M1 05h34m32s, +22°00′52″ match the standard catalogue
  positions (INFERRED from memory). The 412 pc Orion distance agrees with the Atlas's
  Orion-complex marker at (−342.4, −189.8, −134.8) pc, which is 414 pc (VERIFIED arithmetic).
- The Crab pulsar "spinning at 30.2 Hz" (l.342) is outdated. It was **29.6 Hz at MJD 58058
  (2017)** (VERIFIED web: MNRAS 478, 3832). 30.2 Hz corresponds to P ≈ 33.1 ms, the
  discovery-era value (INFERRED).
- The Local Bubble is given as "diameter 150 pc" (l.332) but "(100pc)" on its button (l.145).
  Zucker et al. 2022 give **~300 pc (1 000 ly), peanut-shaped** (VERIFIED web).
- Orion "diameter 15 pc" is unsourced (INFERRED: M42 proper is usually quoted at ~24 ly ≈ 7 pc;
  the whole complex is much larger).

**Rendering defects:**
1. **The volume renders at 1/20 scale.** `vLocalPos = position`, but the geometry is
   `BoxGeometry(20,20,20)` (l.701), so positions run ±10 while the slab test clips to ±0.5
   (l.506-517). The whole cube is drawn in the central unit cube, a small blob inside a 24-unit
   star cloud. VERIFIED by reading; not rendered.
2. Ray origin = `volumeBox.worldToLocal(camera.position)` (l.972). In XR three.js sets that to
   the head centre, so each eye's rays start about ±32 mm off and the volume's depth is
   inconsistent between eyes. INFERRED.
3. `PointsMaterial` ignores the custom `size` attribute (l.817-827), so the "Trapezium giants"
   (`sizes[i] = 1.8`) render like every other star. INFERRED from the three.js API.
4. 128-step full-screen raymarching, unpinned `webxr-polyfill@latest`, CDN Tailwind and FontAwesome.

**How it should appear in CosmoSynth.** As the **close-up tier of the nebula layer** (§4.3):
- At the real positions (Orion complex 414 pc; the Crab at 2 kpc with a sourced distance; the
  Local Bubble centred on the Sun), draw the analytic fields above as `Procedural` volumes, with
  explicit per-field provenance: position Measured, shape and colour Procedural.
- The Crab's pulsar frequency is the one Measured number worth keeping, and it doubles as a
  sonification voice (§4.5).
- The volume is generated in `Core` (engine-free fBm → `byte[64³·4]`, testable) and uploaded as a
  `Texture3D`. One Built-in raymarch shader is needed, using the **per-eye**
  `_WorldSpaceCameraPos` after `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` (INFERRED: in Built-in
  with stereo matrices that macro resolves per eye). The box must be sized consistently with the
  slab test.

**Port:** the three field functions, the compositing loop, and the layer idea (gas / dust
toggles). **Drop:** the fake Gaia stars (CosmoSynth already ships 119 613 real HYG stars), the
probe, the spectral bars, Doppler mode 3, the random B-fields (or keep them as an explicitly
Fictional illustration), the LLM chat (no runtime network; replace with authored text, as the
Cosmic Atlas does), and the polyfill.

---

### 3.3 Milky Way Atlas (`webxr-examples/Fable5/nebula-gallery/`), the real-data counterpart

This was not on the task list, but it is the correction of §3.2 and it answers the question of
"nebulae at real positions" directly, so it is recorded here. Its detailed port belongs to the
Milky-Way / deep-sky research.

- **Layers** (`README.md:7-14`, VERIFIED):
  - dust volume from the Leike & Enßlin / Lallement Gaia-based maps: a 201×201×81 cube spanning
    2000×2000×800 pc, galactic Cartesian with the Sun at the origin, +X towards the Galactic
    Centre, +Z towards the NGP, uint8 log-encoded `ρ = 0.02·(79.81/0.02)^(v/255)`
    (`data/dust_meta.json`). `dust.bin` = 3 272 481 B = 201·201·81 (VERIFIED);
  - HYG v4.1, 60 000 stars within 1 400 pc;
  - 2 214 Cepheids (Skowron+2019);
  - 213 globulars (Harris / LVDB);
  - 911 galaxies (LVDB + M31/M33) at real half-light sizes.
- **Nebula positions** (`data/markers.json`, pc, VERIFIED):

  | Marker | Position (pc) | Note |
  |---|---|---|
  | Orion complex | (−342.4, −189.8, −134.8) | 414 pc |
  | Taurus | (−133.9, 18.8, −36.2) | |
  | Ophiuchus | (132.9, −16.3, 40.9) | |
  | Perseus | (−263.2, 101.0, −102.6) | |
  | California Nebula | (−432.0, 157.2, −97.7) | |
  | Cepheus | (−115.6, 317.7, 90.6) | |
  | Chamaeleon | (86.5, −149.8, −49.6) | |
  | Aquila Rift | (220.4, 117.2, 13.1) | |

  The file also has 24 named bright stars.
- **Rendering ideas worth copying** (index.html):
  - dust raymarch with `d = smoothstep(cut, 1, s)`, `α = 1 − exp(−d²·ρ·dt·60)`, a 4-stop ramp,
    and a **per-pixel start jitter** `t += dt·hash(fragCoord)` that trades banding for noise
    (l.226-255);
  - an "observed sky" mode `m = −2.5 log L + 5 log D − 5` from any vantage point (l.291-301);
  - B−V → T via `T = 4600(1/(0.92BV + 1.7) + 1/(0.92BV + 0.62))` (l.326-333).
- **Scale presets** map directly onto new CosmoSynth rungs. Model scale is metres per parsec:
  10^−3.1 → 2 kpc spans 1.59 m; 10^−4.75 → 40 kpc spans 0.71 m; 10^−6.05 → 2 Mpc spans 1.78 m
  (VERIFIED arithmetic). All three fall within the Galactic band: 2 kpc = 6.2e16 km,
  2 Mpc = 6.2e19 km.
- **Honest limits** (README, the "Honest limits" section): sub-parsec 3D nebula structure is
  largely not measurable, so published "3D nebulae" at that scale are reconstructions from 2D.
  No whole-Milky-Way dust cube exists. Dark-matter blobs and intergalactic dust are "artwork", so
  they are omitted. Higher resolution: Leike 2020 (Zenodo 3993082) and Edenhofer 2023 (Zenodo
  8187943, **CC BY 4.0**, parsec-scale to 1.25 kpc, 8–15.7 GB Cartesian; VERIFIED web).
- **Consequence for CosmoSynth.** The analytic Orion/Crab volumes of §3.2 are legitimate only as
  labelled Procedural close-ups *inside* a Measured dust context. This atlas's own text is the
  argument for the provenance split.

---

### 3.4 Cosmic Modular Synthesizer (`html/cosmic_modular_synthesizer.html`)

**Features.** A 2D DOM workspace (not WebXR) that recreates a Bitwig Poly Grid patch
("Soundscape for Sleeping at the Edge of the Universe", credited to Polarity Music; the
reference image `image_6f2604.jpg` is absent). Nine module types (l.242-388):

| Module | Parameters |
|---|---|
| VCO | 40–800 Hz; fine ±50 ct; FM depth 0–1000; sine/tri/saw/square |
| LFO | 0.02–15 Hz; depth 0.1–1 |
| Pink noise | level |
| VCF | lowpass 60–5000 Hz; Q 0.5–20; mod depth 0–4000 |
| VCA | gain |
| Delay | 0.05–2 s; feedback 0–0.95; mix |
| Reverb | convolution, decay 0.5–8 s; mix |
| Scope | — |
| Master | stereo in; volume |

Master also runs a limiter (threshold −1 dB, knee 4, ratio 12, attack 3 ms, release 80 ms;
l.399-404). Patching is done with drag-and-drop bezier "cosmic cables".

**The preset** (l.1455-1550), the only "cosmic" content:
- sub VCO sine 65 Hz and VCO triangle 130 Hz (+6 ct intended) and pink noise 0.4, all into VCF
  lowpass 220 Hz, Q 6;
- LFO sine 0.04 Hz, depth 0.95 → cutoff mod depth 400;
- VCF → delay 0.65 s, feedback 0.75, mix 0.45 → reverb 6.5 s, mix 0.65 → scope → master L and R at 0.8.

**DSP (VERIFIED):**
- Pink noise is Paul Kellet's refined filter over a 4 s looped buffer (l.454-479):
  `b0 = .99886b0 + .0555179w; b1 = .99332b1 + .0750759w; b2 = .969b2 + .153852w;
  b3 = .8665b3 + .3104856w; b4 = .55b4 + .5329522w; b5 = −.7616b5 − .016898w;
  out = .11(Σb + .5362w); b6 = .115926w`.
- Reverb IR (l.482-498): stereo `U(−1,1)·(1 − i/N)^2.5`, with N = sampleRate·duration.

**What broke** (VERIFIED by reading unless marked):
1. **Fine tune is never applied at creation.** `createAudioNode` for VCO (l.506-534) ignores
   `vco-fine`, so the preset's +6 ct detune (l.1471) has no effect and the two drones do not beat.
2. `initialValues[x] || default` turns a genuine 0 into the default (for example FM depth 0 → 200, l.514).
3. Modules created before the AudioContext exists (the "Explore Visuals Only" path, l.1636-1641)
   never get audio nodes, even after audio is started.
4. The cutoff swings `220 ± 0.95·400` = −160…600 Hz. The Web Audio nominal-range clamp holds it at
   0 Hz for about **30 % of each 25 s LFO cycle** (≈7.6 s). INFERRED from the spec and the
   arithmetic: sin φ < −0.579 over 1.907 rad of 2π.
5. Stereo reverb decorrelation is lost: both channels come from one mono-summed path through
   `ChannelMerger` inputs (INFERRED, Web Audio semantics).
6. Moving the reverb knob regenerates a 2×(sr·decay) IR on every mousemove (l.1118), about
   1.2 M floats at 6.5 s, which will glitch (INFERRED).

**How it should appear in CosmoSynth: a sonification layer** (§4.5). Keep the *recipe* (drone +
pink noise + slowly swept lowpass + long feedback delay + long reverb) as the ambient bed, with the
detune bug fixed and the sweep kept above 0 Hz. Drive voices from **data**, not knobs.

**Port:** Kellet pink noise, the oscillator and LFO maths, and the preset values. **Drop:** the
whole patch editor, and the convolution reverb (Unity's built-in `AudioReverbFilter`,
`AudioEchoFilter` and `AudioLowPassFilter` cover it with no code; INFERRED that they suit
Android/FMOD, to be checked on device).

---

### 3.5 Cosmic Dimensional Portal (`cosmic-portal-test.html` → `js/cosmic-portal-original.js`)

- **The test page is broken:** it loads `<script type="module" src="js/cosmic.js">` (l.189), and
  `Nebulytic/js/cosmic.js` does not exist. Only `cosmic-portal-original.js` and `cosmic_ref.html`
  (the same class inlined) exist (VERIFIED by directory listing). The page shows its HUD chrome
  and loader only (INFERRED).
- **Features** (original.js):
  - portal core sphere with a warped shader (l.243-300);
  - 5 PBR torus rings, 12 clearcoat crystals, 8 helical "dimensional streams",
    `r = 3 + 1.5 sin(6πt)`, `φ = 12πt + iπ/4`, `h = 15(t − ½)` (l.347-379);
  - frame torus;
  - 4 000 random background stars and 1 500 particles;
  - a Fresnel shell;
  - 6 orbiting point lights;
  - bloom, FXAA and ACES tone mapping;
  - an "activate" pulse that expands a ring through every `MeshPhysicalMaterial` via
    `onBeforeCompile`: `r = 8(t − t₀)`, width 1.5, `emissive += c·ring·4` for 3 s (l.152-194);
  - energy −25 per pulse, +8 per second regen.
- **Defects:**
  - the Fresnel terms mix object-space normal and position with the world-space `cameraPosition`
    (l.282, 458);
  - `portalComplexity` is never read (l.543);
  - every frame writes 5 500 point positions on the CPU with unbounded drift (l.593-601);
  - no WebXR at all;
  - post-process bloom is a poor fit for Quest.
- **Verdict:** fictional eye candy with no information content. **Drop.** The one reusable idea
  is the **expanding emissive ring as a transition cue** (for example when a wrist rung jumps
  scale), done as a single cheap shader parameter rather than `onBeforeCompile` on PBR materials.

### 3.6 `arcane_astral*.html`: excluded

Hand-tracked glyph mandalas: chakra-coloured rings with Runic and Phoenician glyphs, a hexagram,
and wrist bangles. `arcane_astral2` toggles "drop/recall" on a 2 cm thumb-index pinch with a
500 ms debounce (l.165-186). `_multi` v19.1 adds camera utilities (l.144). There is no star or
cosmos content. CosmoSynth's pinch is already arbitrated by `XrInput`. **Not ported.**

---

### 3.7 Cosmic Atlas XR (`~/projects/CosmicAtlasXR`, live at `https://build.nebulytic.com/cosmos/`)

**State (VERIFIED):**
- The live page returns 200.
- Three.js r180 is vendored, so there is no CDN.
- 11 Node tests (per `VALIDATION.md`).
- A headless Chromium pass rendered all nine exhibits (`qa/exhibit-1..9.png` exist).
- **Not tested on a Quest.** `VALIDATION.md:22-25` says so: *"Physical Quest 3 session entry,
  stereo comfort, room placement, controller/hand tracking and sustained performance have not
  been tested here."*

**The nine exhibits** (`src/catalog.js:9-82`, `src/exhibits.js:19-81`):

| id | Family | Badge | Parameter | What is actually modelled | What is schematic |
|---|---|---|---|---|---|
| black-hole | Relativity | ANALYTIC MODEL | mass 5–50 M☉ | Keplerian differential flow `flow = a − 0.30t·√M / r^1.5`; horizon : photon sphere : ISCO = 0.11 : 0.165 : 0.33 = **1 : 1.5 : 3** (VERIFIED, exhibits.js l.20-22) | Curvature mesh `y = −0.10 − 0.38/√(1 + 22r²)`, disk colour, cube-root display scaling |
| merger | Relativity | SCHEMATIC | mass ratio q 0.25–1 | Prescribed inspiral `sep = 0.11 + 1.05(1 − p/0.75)^0.55`, `φ = 9i + 25i³`, strain `(0.025 + 0.16i³)cos 2φ`, ringdown `0.185e^(−5ρ)cos(2(34 + 20ρ))` (physics.js l.4) | Not a calibrated waveform |
| lensing | Relativity | THIN-LENS | β/θE 0–1.5 | `θ± = ½(β ± √(β² + 4θE²))`, exact point-mass lens (physics.js l.2) | Broken straight rays, exaggerated angles |
| galaxies | Cosmology | HEADLESS CAPTURE | trail persistence | **Real N-body integration**, below | Normalized units, no gas or halo |
| cosmic-web | Cosmology | DISPLACEMENT | amplitude 0.3–1.3 | Zel'dovich `x = q − D∇Φ` on 19³ = 6 859 points. The field (physics.js l.7) **is** curl-free: VERIFIED by hand, `Φ = −½(cos 2x + cos 2y + cos 2z) − .35cos(x+y) − .28cos(y+z) − .22cos(x−z)` | Periodic toy potential, no calibrated growth |
| dark-matter | Cosmology | ORBITAL | halo 0–2 | `v² = 1.1r²/(r² + 0.14)^{3/2} + 0.8h·r²/(r² + 0.30)` (Plummer disk + cored flat halo, physics.js l.3); 1 800 test stars | Purple halo glow ("dark matter itself does not emit") |
| stellar-interior | Stellar | STRUCTURAL | cutaway 0.35–1.8 rad | Shell radii .20/.63/.86/.88, so core/R = 0.23 and radiative-zone top/R = 0.72, close to the Sun's ~0.25 and 0.713 (VERIFIED ratios from l.63; solar values INFERRED); granulation fBm shader | Energy "photons" and convection loops are illustrative |
| reconnection | Stellar | SCHEMATIC | rate 0.3–1.5 | Parametric field lines `y = ±(narrow + 0.041n + 0.45 tanh²(2x))`, prescribed inflow and exhaust | Not resistive MHD |
| alfven | Stellar | LINEAR MHD | B₀ 0.5–2 | `y = 0.09 sin(2.8x − 2.8B₀t + φ)`, so phase speed ∝ B₀ (physics.js l.8) | Normalized ρ, μ₀; exaggerated amplitude |

**N-body capture** (`tools/capture.py`, `public/data/encounter.json`, VERIFIED):
- 514 particles: two disks of 1 central + 256, central masses 1.0 and 0.8, disk particle mass
  0.13/256, total 2.06.
- `r = 0.12 + min(Γ(2, 0.19), 0.94)`; circular speed `√(M r²/(r² + ε²)^{3/2})`; the second disk
  is tilted 0.65 rad about x; centres at ±(1.65, 0.13, 0.2) with velocities ±(0.17, 0, 0.13).
- Kick-drift-kick leapfrog, dt 0.002, Plummer ε 0.09, 240×48 = 11 520 steps, 241 snapshots.
- Energy drift 0.575 %; centre-of-mass drift 4.0e−14; seed 1701 (numpy PCG64); 142 s on triton.
- `encounter.bin` = 241·514·3·4 = 1 486 488 B, SHA-256 `d5faf6d5…45ca78`.
- Direct summation is 514² ≈ 2.6e5 pairs × 11 520 steps ≈ 3.0e9 pair evaluations, so **ship the
  capture; never integrate on the headset**. Its RNG is numpy PCG64 and cannot be reproduced in
  C# cheaply, which is another reason to ship the bytes.

**UX worth keeping** (catalog.js):
- every exhibit carries `explanation`, `equation`, `question`, `observation`, `methodTitle`,
  `method` (including what it is *not*), 3 `notes` bound to clickable labels, 3 named layers, and
  a timeline of 30–40 s (scrub pauses, play stops at the end);
- the workspace is placed 1.9 m ahead and 0.2 m down, at scale 0.67 (observatory.js l.19);
- right stick scales and left stick yaws (l.31);
- `setFoveation(.7)`, framebuffer scale 0.9, and a 72 Hz request (l.8, 29).

**Defects and limits for Quest:**
1. Every line is 1-px `THREE.Line`, and the black-hole curvature mesh alone is 50 polylines × 65
   vertices. That repeats the "thin lines everywhere" complaint.
2. Points use `gl_PointSize` clamped 1.5–36 px (materials.js l.6), which is the same failure class
   as CosmoSynth's "square patches".
3. Per-frame CPU vertex rewrites: cosmic web 6 859 points, dark matter 1 800, reconnection 22×90
   + 550.
4. White studio styling (labels are white rounded canvases).
5. Never seen in a headset.

**How it should appear in CosmoSynth: the Phenomena layer** (§4.4). A wrist list of nine
exhibits, each a tabletop diorama. Four are also reachable *in context* from a real host:
- stellar interior, reconnection and Alfvén → **the Sun**;
- black hole → **Sgr A\*** (with the display-scaling caveat kept);
- lensing, galaxy encounter, cosmic web → the Galactic / Local Group / large-scale rungs;
- dark-matter halo → the Milky Way rung, next to a Measured rotation curve (source to be chosen).

**Port:**
- `physics.js` becomes `Core/Phenomena/*.cs` (engine-free; translate the Node tests);
- `encounter.bin` + `.json` go into StreamingAssets, with SHA-256 checked at load;
- `catalog.js` texts become a JSON, verbatim;
- analytic motions (cosmic web, Alfvén, dark matter) go into vertex shaders (per-vertex base
  position + uniforms, so zero CPU); the N-body stays a CPU lerp of 514 points;
- lines use ribbon meshes; points use CosmoSynth star sprites.

**Drop:** the white background, the DOM panels, the Three.js-specific picking, and the
unbounded label canvases.

---

### 3.8 StarHolo (web) and StarHoloXR (Unity 6 URP)

**What.** A volumetric sci-fi starship HUD rebuilt from a 2D wallpaper (`shots/reference.jpg`),
in two scenes:
- **NAV CANOPY:** shader planets, a gimbal reticle, an eclipse void, a warp starfield, and a
  **real Helsinki clock**, which the user insisted on (`StarHolo/CLAUDE.md:29-33`);
- **WARP CORE:** a ~130-part engineering bay.

The trigger is an analog throttle. The web version keeps ~40 draw calls and "holds 72 Hz" by its
own README's claim. Unity 0.1.11
passes `verify-apk`, but **0.1.10 showed two magenta stereo quads on the headset** (stripped
shaders), and **0.1.11 was never confirmed on device**: "Sideload 0.1.11" is backlog item #1
(`CLAUDE.md:103-105`).

**Traps that transfer** (VERIFIED from its files):
1. **URP vs Built-in.** All six `StarHolo/*` shaders are `"RenderPipeline"="UniversalPipeline"`
   HLSL including URP `Core.hlsl` (for example `SHLine.shader:6,17`). In CosmoSynth's Built-in
   pipeline they would find no valid SubShader, so the object would not render or would render
   magenta (INFERRED). They must be rewritten in CG with `UnityCG.cginc` and the Built-in stereo
   macros (§2.4). The handoff's URP naming rules (`_WorldSpaceCameraPos`,
   `TransformWViewToHClip`, `CLAUDE.md:84-87`) do not apply there.
2. **Alpha.** Every StarHolo shader is `Blend One One` (adds alpha), which violates §2.3.
3. **`ZTest Always` everywhere**, relying on painter's queues. That is fine for one floating
   hologram and wrong inside CosmoSynth's depth-banded universe, where it would draw through planets.
4. **Stripped shaders → magenta stereo quads.** CosmoSynth already solves this with `CosmoShaders.All`.
5. **Handedness mirror:** the nav composition is x-mirrored against the web version (`CLAUDE.md:106-108`).
   This is the same class as CosmoSynth's ICRF right-handed vs Unity left-handed conversion.
6. **TextMeshPro with empty text → NaN scale / giant glyphs** (`CLAUDE.md:88-89`). CosmoSynth uses
   `TextMesh` through `CosmoText`, so the TMP label helper does not port.

**Reusable:**
- `StarHoloFx.cs` mesh builders are engine-mesh only: `LineMesh`, `LineLoopMesh`, `TorusMesh`,
  `OctahedronMesh`, `ConeMesh`, `DiskMesh`, `BoxEdges`, `GlowTex` (l.81-327). They are useful for
  the projector base and beam.
- The noise math in `SHPlanet.shader`: `Hash31`, trilinear `Vnoise`, 4-octave `Fbm` with ×2.03
  and an offset, domain warp `Fbm(p + 0.9·Fbm(0.7p))`, Fresnel `pow(1 − N·V, 2.2)`. It is the same
  family as the Cosmic Atlas granulation, so write it once as a Built-in `.cginc`.
- `HelsinkiClock.cs` (hand-computed EU DST) only if a clock is wanted. It is not needed:
  CosmoSynth has `CosmicTime` in TDB.

**Verdict:** **do not port the scenes.** They are a starship, not the universe, and terms like
"warp core" and "dilithium" invite an IP question in a store build (INFERRED, not legal advice).
Use StarHolo as a donor of mesh helpers and noise math, and as a list of traps already paid for.

---

## 4. One coherent app: the proposed shape

### 4.1 The contract: one universe, three kinds of truth, always visible

- **Real** (default): Measured / Inferred fields. Procedural fields are allowed only as
  "Procedural over a Measured position" (roadmap Phase 4).
- **Phenomena:** exhibits. They are not catalogue objects, but they carry a `ProvenanceRecord`
  so the existing panel can show a per-field breakdown. Proposed mapping, using the existing
  enum with no sixth class:
  - laws and exact ratios → `Inferred`, with the formula as the source string (lens equation,
    Schwarzschild 1 : 1.5 : 3, v_A ∝ B);
  - simulated dynamics → `Procedural(generator, seed)` (the N-body with seed 1701; the Zel'dovich
    field with seed 91; the halo stars with seed 71);
  - illustrative visuals → `Fictional("Cosmic Atlas illustration")` (curvature mesh, halo glow,
    disk colour).

  `WeakestClass()` then badges almost every exhibit Fictional or Procedural, which is honest. The
  per-field list tells the viewer which parts are laws.
- **Fictional:** authored worlds (the holo system). Every field is `Fictional`. It is never
  merged into the real catalogue. Add one headless test mirroring the Phase 2 one: *no field
  reachable from the real catalogue is Fictional, and no field of a Fictional catalogue is
  anything but Fictional.*

### 4.2 Fictional mode (from §3.1)

- **Data:** one JSON catalogue (for example `StreamingAssets/CosmoSynth/catalog/fictional-holosystem.json`),
  schema as `solar_system.json`, provenance `{"class": "Fictional", "source": "Nebulytic
  holographic star map (2026)"}`. It gets new, non-Marvel names; the table in §3.1 is the
  starting point. Masses, periods and e are authored and still go through `CelestialCatalog.Validate()`.
- **Presentation:** a tabletop hologram only, in its own simulation root, with no ICRF placement.
  The hologram skin (projector plate, beam, flow-filament orbit ribbons, bracket labels in
  `CosmoTheme` Fictional colour with the `?` glyph) is the visual signifier that the content is
  authored. Reserving the neon-projector look *only* for Fictional content turns the aesthetic into
  information.
- **Entry:** one wrist entry, "Fictional: Holo system". Optional later: "place at a real star"
  (star fields stay Measured, planets Fictional). The per-field design already supports it, and
  the badge makes it obvious.
- **New code:** two Built-in shaders (flow filament, beam) plus a few lines to spawn the root.
  Everything else exists.

### 4.3 Nebula layer (from §3.2 and §3.3)

- **New rungs on `WristDashboard.ScaleRungs`**, taken from the Atlas presets: "Local clouds
  (2 kpc)", "Milky Way (40 kpc)", "Local Group (2 Mpc)". All sit inside `ScaleSpace`'s Galactic
  band. A double's ulp is 2^(⌊log₂x⌋−52): 128 km at 1e18 km and 16 384 km at 1e20 km. That is
  irrelevant for objects known to a parsec (3.1e13 km), so the deferred "sector offsets" are not a
  blocker here.
- **Measured base:** a dust cube (Leike/Lallement as in the Atlas, or Edenhofer 2023, CC BY 4.0)
  imported offline to a `uint8` `Texture3D`, with log encoding as in `dust_meta.json`. Importer
  caveat: the Edenhofer Cartesian products are 8–15.7 GB, and their file format (FITS or HDF5)
  must be checked against the stdlib-only importer rule. FITS is readable in stdlib; HDF5 is not
  (INFERRED).
- **Named nebulae:** positions from `markers.json` (8 clouds) plus a Messier/NGC import (licence
  to be settled per `data-sources.md`), shown as labelled markers with Measured positions.
- **Close-ups:** the three analytic fields of §3.2, generated in `Core`, shown only when the
  viewer is within the object's box, provenance Procedural. The numbers are fixed first: Local
  Bubble ~300 pc and peanut-shaped (Zucker+2022; the analytic hourglass stays Procedural); Crab
  pulsar 29.6 Hz; Orion size sourced.
- **Rendering budget (INFERRED, untested on Quest):**
  - ≤ 48 steps with a start jitter (the Atlas trick);
  - early exit at α ≥ 0.95;
  - the box drawn with `Cull Front` so the camera can enter it;
  - no full-screen march at galactic rungs, where the cube is small on screen.
- **MR alpha decision needed:** emission should be additive with no alpha (`Zero One`). Dust
  *extinction* over passthrough means writing alpha, which darkens the room like smoke. Either
  skip extinction in MR or make it a toggle; this is a design question, not a code question.

### 4.4 Phenomena exhibits (from §3.7)

- **Access:** a third `WristDashboard` list, "Phenomena", with 9 rows, following the existing
  glance/engage rule. Engaging spawns the diorama in front of the user, placed from the camera
  pose as in the anchor fix (`cosmosynth-xr-empty-scene`), grabbed and scaled by
  `TabletopManipulator`. Exhibit time is local: it never drives `SimulationClock`. The timeline
  scrub takes the stick through `ClaimNavigationStick`, and the bindings go in `XrInput.Bindings`
  so the legend regenerates.
- **Context entries:** when the Sun is selected, its provenance panel offers "Inside the Sun",
  "Magnetic reconnection" and "Alfvén waves"; Sgr A\* offers "At the event horizon".
- **Text:** `catalog.js` fields go into a JSON, shown through `CosmoPanel` (sized from contents).
  `question` and `observation` become the panel's second page. The user wrote this content, so it
  can be reused verbatim.
- **Geometry:** CosmoSynth star sprites instead of GL points. Ribbon lines (`OrbitLineRenderer`'s
  approach) with angular width, instead of 1-px lines. For the curvature mesh, a ribbon grid with
  about 12 lines per axis, not 25.

### 4.5 Sonification layer (new; from §3.4)

- **Architecture:** DSP in `Core/Audio` (engine-free, so it is testable headlessly; for example
  assert that pink noise falls ≈ −3 dB/octave, that the chirp frequency rises monotonically, and
  that the LFO never takes the cutoff ≤ 0). One `Runtime` MonoBehaviour fills
  `OnAudioFilterRead`, followed by Unity's built-in low-pass, echo and reverb filter components.
  That is one voice pool with a hard cap of about 6–8 voices (INFERRED Quest budget).
- **Bed:** the fixed synth preset. It plays by default at low level, can be muted from the wrist,
  and its filter sweep follows the scale rung (brighter as you zoom out, for example). The mapping
  is arbitrary, so it is labelled "ambient, not data".
- **Data voices,** each carrying the provenance of what drives it:
  - **Pulsars.** The spin frequency is played as a click train: Crab 29.6 Hz (Measured, VERIFIED
    web); Vela ≈ 11.2 Hz and PSR J1748−2446ad ≈ 716 Hz (INFERRED, to be imported with a source).
    This is the one sonification with no invented mapping at all.
  - **Binary merger exhibit:** the strain from `mergerState` sent straight to audio, an
    illustrative chirp. A real GW150914 sweep (~35→250 Hz, INFERRED) would be a Measured option
    if GWOSC data is imported under its licence.
  - **Star field:** the viewer's nearest bright stars mapped temperature → timbre and flux →
    loudness. The mapping is authored, so it is marked as a *sonification convention* in the
    legend.
  - **CMB end of the ladder (future):** the acoustic-peak "sound of the early universe" is a
    known sonification idea. It is only a placeholder here.
- **Drop:** the patch UI and the convolution IR generator.

### 4.6 One visual language

`CosmoTheme` stays the only palette. The Atlas's muted scientific colours (#a97540, #617aaf,
#b66844) suit exhibit geometry. The neon cyan/orange projector look is reserved for Fictional.
The portal's bloom and PBR crystals are not used. Labels always come from `CosmoText`.

---

## 5. Shader-port checklist (apply to every effect above)

1. Built-in CG (`UnityCG.cginc`), not URP HLSL.
2. Stereo macros per `CosmoAtmosphere.shader:72-140`. A fragment that uses the camera position
   calls `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` first, which matters for raymarch origins.
3. Additive: `Blend One One, Zero One`. Premultiplied: `Blend One OneMinusSrcAlpha, Zero One`
   unless occlusion of passthrough is intended.
4. `ZTest LEqual` (not `Always`) and `ZWrite Off` for transparents, so the depth bands keep working.
5. Add the name to `CosmoShaders.All`, run `xrsetup-cosmosynth.sh`, and check the APK for the
   shader, as StarHoloXR's `verify-apk` does.
6. Verify alpha coverage with `PlayModeShot.ProbeDestinationAlpha` and `COSMOSYNTH_HIDE`.
7. No feature under ~3 px (star-sprite rule); ribbons with angular width, not lines.
8. Frame-rate independence: use `Time.deltaTime`, never per-frame increments (star map defect 2).

---

## 6. Port vs drop, master list

| Item | Decision | Reason |
|---|---|---|
| Cosmic Atlas physics (8 formulas + N-body capture) | **Port** | Validated, honest, small; `Core`-testable |
| Cosmic Atlas texts (explanation, question, observation, method, notes) | **Port verbatim** | Already written with limits; the app has no network for an LLM |
| Cosmic Atlas rendering (1-px lines, GL points, white studio) | Drop | Repeats known user complaints; Built-in rewrite anyway |
| Holo star map system + names + skin | **Port as Fictional** | The user's fictional content; the skin becomes a signifier |
| Holo star map input, diagnostics, Marvel naming | Drop | Duplicates `XrInput` / `TabletopManipulator`; random numbers; IP |
| Nebula gallery analytic fields + compositing | **Port as Procedural close-ups** | Cheap, pretty, honest once labelled |
| Nebula gallery fake Gaia, probe, Doppler mode, B-fields, AI chat | Drop | Mislabelled invention; runtime network forbidden |
| Milky Way Atlas data layers | **Port (Milky-Way research)** | Real, cited, same scale problem already solved |
| Synth preset + Kellet noise | **Port into sonification** | Good ambient recipe; tiny DSP |
| Synth patch editor, convolution reverb | Drop | Not XR; Unity's built-in filters cover it |
| Portal | Drop (keep the pulse-ring idea) | No information content, desktop-only, broken test page |
| arcane_astral | Not ported | Not cosmos |
| StarHolo scenes | Drop | Starship, not universe; URP; IP-flavoured terms |
| StarHolo mesh helpers + noise math | **Reuse after Built-in rewrite** | Saves writing torus, disk and fbm again |

---

## 7. Risks and open questions

1. **Phase 0 first.** Every item above adds draw calls. The unresolved "100 instances of earth"
   report must be diagnosed on a headset before any of this ships.
2. **Quest cost of volumes** (raymarch steps, overdraw) is unmeasured; the budget in §4.3 is INFERRED.
3. **Provenance semantics for exhibits:** mapping "illustration" to `Fictional` is a judgement.
   If the design agent prefers a distinct "Illustrative" class, that is a `Core` enum change with
   ripple effects (`WeakestClass`, `CosmoTheme`, JSON). The recommendation is not to add one.
4. **IP:** "MARVEL_COSMOS" (star map), "warp core" / "dilithium" (StarHolo), the Bitwig and
   Polarity attribution (synth). Rename and do not reference them in the app.
5. **Licences to settle** before shipping: Leike 2020 and Lallement cubes, LVDB, the Harris
   catalogue, Skowron+2019, a pulsar catalogue, GWOSC, and a Messier/NGC source. Edenhofer 2023 is
   CC BY 4.0 (VERIFIED). HYG CC BY-SA is already handled in `data-sources.md`.
6. **Import-tool rule:** multi-GB FITS or HDF5 dust cubes against the stdlib-only importers.
7. **MR extinction:** whether dark dust should darken the passthrough room (§4.3).
8. **The Atlas was never headset-tested either.** Its interaction numbers (1.9 m placement, 0.67
   scale) are desktop-derived.

---

## 8. Appendix: formulas and constants for implementers

- **Keplerian replacement for the holo orbits:** `e = √(1 − b²/a²)`; `r = a(1 − e²)/(1 + e cos ν)`;
  periapsis `a(1 − e)`. The §3.1 values give periapsides 0.094 (DD-210) … 0.607 (AR-503) and
  apoapsides 0.347 … 1.993, so the ranges overlap.
- **Filament ribbon:** `P(u) = C(u) + A[cos(ωu + φk)N̂ + sin(ωu + φk)B̂]`, with ω = 20π, A = 0.009
  (hologram units). Shader: `f = ½ + ½ sin(uR − ts)`, `b = f⁸`.
- **Nebula fields:** §3.2. Compositing: `α = 1 − exp(−kρΔs)`, k = 15 (gallery) or `d²·ρ·dt·60`
  (Atlas).
- **Dust decode:** `ρ = 0.02·(79.8145/0.02)^(v/255)`. Galactic frame +X → GC, +Z → NGP, Sun at the origin.
- **Atlas scale presets:** metres per pc = 10^−3.1 / 10^−4.75 / 10^−6.05.
- **Lens:** `θ± = ½(β ± √(β² + 4θE²))`.
- **Rotation curve:** `v² = 1.1r²/(r² + .14)^1.5 + .8h·r²/(r² + .30)`.
- **Merger:** physics.js l.4 (see §3.7).
- **Zel'dovich:** `x = q − D∇Φ`, `D = 0.69·(t/T)·amp`, Φ in §3.7.
- **Alfvén:** `y = 0.09 sin(2.8x − 2.8B₀t + φ)`.
- **N-body:** dt 0.002, ε 0.09, stride 48, 241 frames, 514 particles, float32 little-endian xyz,
  SHA-256 `d5faf6d5e436b7fba63cc2e66e4cf21d97bcfafecfe4d077c71181ba4745ca78`.
- **Pink noise:** Kellet coefficients in §3.4.
- **Ambient preset:** 65 Hz sine + 130 Hz triangle (+6 ct) + pink 0.4 → LP 220 Hz Q 6 (LFO
  0.04 Hz, keep the swing above 0) → delay 0.65 s, feedback 0.75, mix 0.45 → reverb ~6.5 s, mix 0.65.
- **Crab pulsar:** ν = 29.6 Hz (MJD 58058).

---

## 9. Incidental observation (outside this task)

`unity/Assets/XR/Settings 1..3` and `unity/Assets/XR 1..3` exist and are **empty**; only
`Assets/XR/Settings` holds the OpenXR assets (VERIFIED `ls`). They are probably harmless editor
leftovers. They are mentioned only because `CLAUDE.md` records a duplicate-XR-asset build failure
before; the render-regression work may want to delete them.

## 10. Web sources checked

- Crab pulsar ν = 29.6 Hz at MJD 58058: <https://academic.oup.com/mnras/article/478/3/3832/4998873>
  (also <https://arxiv.org/pdf/1805.05110>)
- Local Bubble ≈ 1 000 ly (≈300 pc), peanut-shaped (Zucker et al. 2022):
  <https://www.cfa.harvard.edu/news/1000-light-year-wide-bubble-surrounding-earth-source-all-nearby-young-stars>
- Edenhofer et al. 2023 dust map, CC BY 4.0, sizes: <https://zenodo.org/records/8187943>
