# COSMOSYNTH

**A scientifically grounded universe engine for XR.**
Explore the known universe. Generate the unknown. Touch space in VR/MR.

---

## What exists today

This is the foundation of the *Solar System XR Laboratory* — the load-bearing parts, built and
verified first, rather than a demo scene with the hard problems deferred.

**Working and tested:**

- **Double-precision orbital mechanics.** Kepler solvers for elliptic, parabolic and hyperbolic
  orbits; classical elements ↔ state vectors; universal-variable propagation. Two
  algorithmically independent propagators that cross-validate against each other.
- **Validated against NASA.** 260 real state vectors from JPL Horizons are committed to the
  repo. Recovered orbital elements match JPL's published values to **under a milliarcsecond**.
- **A real Solar System catalogue.** 31 bodies imported live from JPL Horizons — Sun, eight
  planets, Pluto, 14 moons, dwarf planets, asteroids and a comet — with masses, radii, orbits,
  IAU rotation elements and atmospheres.
- **Per-field data provenance.** Every value is labelled Measured, Inferred, Procedural or
  Fictional, with its source, and the in-app inspector shows it. A viewer can always ask "how
  do you know that?"
- **Reference-frame tree and floating origin**, so a lander near Enceladus keeps millimetre
  precision instead of drowning in a 1.4-billion-kilometre coordinate.
- **Scale space** from a handrail to a galaxy: pixel-driven LOD, banded depth ranges,
  logarithmic navigation, and true-scale/readable-scale presentation modes.
- **Procedural generation**: deterministic hashing, 3D coherent noise, cube-sphere quadtree
  terrain with screen-space-error LOD, and a physically-constrained star system generator that
  validates what it produces.
- **Immersive Quest 3 mixed reality**: passthrough, hand tracking with pinch gestures, and
  Touch controllers — the solar system is a hologram anchored in your actual room. Pinch to
  grab and move it; pinch with both hands and pull apart to scale from a handheld globe out
  to the whole system around you. Point and pull the trigger to select a body and read its
  provenance panel in world space. Thumbstick steps time warp.
- **Generated surfaces**: oceans, continents, polar ice, deserts, crater fields, ice shells,
  lava plains and zonal cloud bands — biome chosen by temperature and moisture first,
  elevation second. Baked on worker threads so the headset never stalls.
- **Star field, rings and point-source lighting**: 2500 stars with a power-law magnitude
  distribution and black-body colour; Saturn/Uranus/Jupiter/Neptune rings at real radii with
  the Cassini division; a point light at the star so terminators and phases are correct.
- **24 real spacecraft**, with trajectories from JPL Horizons and geometry from
  [NASA 3D Resources](https://github.com/nasa/NASA-3D-Resources): the Voyagers and Pioneer 10
  on their way out, Webb at L2, Hubble and the ISS in low Earth orbit, orbiters at the Moon and
  Mars, Juno at Jupiter, Cassini at Saturn, and Perseverance, Viking 1 and the Apollo 11
  descent stage on the ground where they actually are. Each is placed by whichever of three
  mechanisms is least wrong for its path — a sampled state table, osculating elements, or a
  fixed landing site — and each carries the window over which its position is known, so an
  ended mission is **not drawn** unless you wind the clock back to when it was flying.
- **Unity 6 runtime**: simulation clock with time warp, body views, orbit rendering, and
  scale-aware free flight for the desktop path.
- **114 tests, all passing**, runnable in under a second with no Unity Editor required.

**Not yet built** — see [docs/accuracy.md](docs/accuracy.md) for the full list: real measured
terrain and imagery (all surfaces are procedural), an atmospheric scattering integral, a real
star catalogue, sampled ephemerides for the *planets* (the machinery exists and the spacecraft
use it), the trajectory editor, and the AI architect.

**Not yet run on a headset.** The APK is verified to declare immersive launch, passthrough and
hand tracking, and the whole numerical layer is tested — but nobody has put it on a Quest.

---

## Quick start

### Run the tests (no Unity needed)

```bash
./run-tests.sh
```

CosmoSynth.Core is deliberately engine-free, so the entire numerical layer compiles and runs on
a bare .NET SDK. The script finds one automatically — including the one bundled inside any
Unity 6 install, so if you have the editor you already have everything.

```bash
./run-tests.sh Kepler        # just the Kepler solvers
./run-tests.sh Horizons      # the NASA validation, with its accuracy tables
./run-tests.sh -v            # stack traces for non-assertion errors
```

### Open in Unity

1. Add `unity/` as a project in Unity Hub (Unity **6000.5.5f1**, or any Unity 6.1+).
2. Let it import — the first import takes a few minutes.
3. Menu → **CosmoSynth → Create Solar System Lab Scene**.
4. Press Play.

The scene is generated from one legible C# method rather than committed as a `.unity` file.
Scene assets are opaque YAML full of GUIDs: unreviewable in a diff, silently broken by a
renamed component, and the most merge-hostile file a Unity project can hold.

**Desktop controls:** `WASD` + `QE` to fly · right-drag to look · scroll to trim speed ·
`shift`/`ctrl` to boost/slow · `,` `.` to change time warp · `space` to pause · `N` for now ·
`Tab` to toggle the HUD · click any body to travel to it.

Speed scales with the distance to the nearest surface, so the same stick deflection crosses the
visible scene in about the same few seconds whether that scene is a landing site or a planetary
system.

### Rebuild the catalogue from NASA

```bash
python3 tools/cosmodata/build_solar_system.py
```

Queries JPL Horizons and writes
`unity/Assets/StreamingAssets/CosmoSynth/catalog/solar_system.json`. Only stdlib Python is
needed. The app never touches the network at runtime — data is fetched offline, shipped
locally, and interpolated in-engine, which is what makes it work on a headset, in a classroom
and on a plane.

---

## Layout

```
CosmoSynth/
├── run-tests.sh                     Headless test runner
├── docs/
│   ├── accuracy.md                  What the numbers are worth, measured
│   ├── architecture.md              How it fits together, and why
│   └── data-sources.md              Every archive, and its licence
├── unity/
│   ├── Assets/CosmoSynth/
│   │   ├── Core/                    Engine-free. All the physics. Fully tested.
│   │   ├── Runtime/                 MonoBehaviours. Unity-only.
│   │   └── Editor/                  Scene generation, catalogue validation
│   └── Assets/StreamingAssets/      The shipped catalogue
├── tools/cosmodata/                 Python importers (Horizons)
└── tests/
    ├── CosmoSynth.Core.Tests/       94 tests + committed NASA fixtures
    └── CosmoSynth.Runtime.Compile/  Compile-checks Unity code without the Editor
```

### The one architectural rule

**`Core/` must never reference `UnityEngine`.**

That single constraint is what makes the orbital mechanics testable at all. Because the physics
has no engine dependency, it compiles against a plain .NET runtime and the whole suite runs in
under a second in a terminal — no editor, no licence, no graphics device, no play mode. The
`Runtime/` layer does only what genuinely needs an engine: loading files, creating GameObjects,
and pushing double positions into float transforms once per frame.

`tests/CosmoSynth.Runtime.Compile/` compiles the Unity layer against the editor's own
`UnityEngine` assemblies. It cannot verify behaviour, but it catches the misremembered API and
the changed signature in about a second, instead of after a multi-minute editor import.

---

## What makes this different

Most universe simulators quietly erase the difference between what is known and what is
invented. A rendered exoplanet looks identical whether its radius was measured to four digits
or guessed by a noise function.

CosmoSynth records provenance **per field, not per object**, and shows it. Earth's radius is
Measured (IAU 2015). Its display colour is Inferred. Its terrain is Procedural — no measured
elevation model is loaded yet, and the inspector says so rather than letting a convincing
render imply otherwise.

The same honesty applies to the physics. The test suite doesn't just assert that the code is
self-consistent; it measures how far a two-body Kepler orbit drifts from the real perturbed
solar system, prints the number for every body, and documents it. The Moon — where a Kepler fit
fails badly, because the Sun perturbs it almost as strongly as Earth binds it — is in the
validation fixture on purpose, so nobody assumes the approximation is universally good enough.

---

## Roadmap

**Near term** — the physics is ready, the rendering is not:
sampled ephemerides from SPICE (removes almost all propagation error);
atmospheric scattering; cube-sphere terrain meshes from the existing quadtree;
real elevation data (USGS Astropedia) for the Moon and Mars; rings.

**Then**: patched-conic mission planning on top of the spacecraft that are now in the
catalogue; the grab-and-pull trajectory editor; Gaia star catalogue; the XR volumetric
interface; AI-generated systems validated through the existing `CelestialCatalog.Validate()`
path.

See [docs/architecture.md](docs/architecture.md) for how each of these slots in.

---

## Attribution

Built on public data from **NASA/JPL Horizons**, the **IAU WGCCRE**, **NASA planetary fact
sheets**, and **[NASA 3D Resources](https://github.com/nasa/NASA-3D-Resources)** for the
spacecraft models. Full source list and licensing notes in
[docs/data-sources.md](docs/data-sources.md).

NASA material is generally reusable, but not every third-party contribution hosted by a NASA or
ESA archive carries the same terms. Each dataset needs its own licence check before commercial
distribution — the ones currently used are itemised in that document.
