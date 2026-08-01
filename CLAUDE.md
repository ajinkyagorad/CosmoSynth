# CosmoSynth — conventions

Read this before changing code here. `~/README.md` is the authority for the machine;
this file is the authority for this project.

---

## The one rule

**`unity/Assets/CosmoSynth/Core/` must never reference `UnityEngine`.**

Enforced structurally: `CosmoSynth.Core.asmdef` sets `noEngineReferences: true`, so a stray
`using UnityEngine` is a compile error rather than a review comment.

Everything follows from it. Because the physics has no engine dependency, the whole numerical
layer compiles against a plain .NET runtime and 87 tests run in under a second in a terminal —
no editor, no licence, no graphics device, no play mode. That is the only reason validating
the orbital mechanics against 260 real JPL state vectors was practical.

`Runtime/` does only what genuinely needs an engine: loading files, creating GameObjects, and
pushing double positions into float transforms once per frame.

---

## Layout

This repo is **not** just a Unity project, which is why the editor project lives one level
down. Build scripts must point at `~/projects/CosmoSynth/unity`, not at the repo root.

```
CosmoSynth/
├── run-tests.sh              Headless suite; also compile-checks the Unity layer
├── unity/                    <- the Unity project
│   └── Assets/CosmoSynth/{Core,Runtime,Editor}
├── tools/cosmodata/          Python importers (stdlib only)
├── tests/                    .NET test host + committed NASA fixtures
└── docs/                     accuracy.md, architecture.md, data-sources.md
```

---

## Building

```sh
bash ~/server-setup/build-cosmosynth.sh          # APK -> ~/builds/cosmosynth/
```

- **After a release build, save its shot as
  `~/builds/shots/cosmosynth/v<version>__<caption>.png`.** Old non-functional APKs get pruned as
  disk fills, so the screenshots are the surviving history — the build page heading says "kept
  when the APK is deleted" and groups the gallery by that version tag, listing untagged shots
  last because it cannot place them in the history. The parser is `shot_version()` in
  `~/control-panel/serve.py`; **read it rather than inventing a name**, which is how
  `cosmosynth-1.7.0.png` ended up untagged. Take the shot from the play-mode capture of the same
  tree that produced the APK — `SceneShot` builds its own scene and proves nothing about the
  shipped app. Working captures do **not** belong there: put them in
  `~/builds/cosmosynth/diagnostics/`. A subdirectory under `shots/` whose name is not a project
  key lands in "Unmatched screenshots", so out of the tree is the only quiet place. See the
  README beside the shots.
- **Bump `ServerBuild.Version` before building, or you destroy the previous APK.** The output
  filename is `CosmoSynth-<Version>.apk` and the build overwrites it in place, so rebuilding
  without a bump silently replaces the last binary in `~/builds/cosmosynth/` — and its notes file
  with it. The build page's timeline is one row per APK, so the old build does not merely get
  hidden, it stops existing. There is no recovering it from git either: the tree it was built
  from has moved on. This is exactly what happened on 2026-07-31, losing the earlier 1.6.0.
- **Never `-nographics`.** It omits the Vulkan `xr-*` entries from `boot.config`, producing an
  APK that shows black instead of passthrough while reporting complete success.
- The script refuses to start if another editor is open **on this project**, and warns if the
  box is short on memory. Other sessions routinely build other projects concurrently; that is
  fine, and blocking on it would mean never building.
- `ServerBuild.cs` **does** set PlayerSettings, unlike the other projects on this box. It has
  to: this project was authored from a terminal and has never been configured in the editor, so
  there is nothing to preserve. Once someone configures it by hand, reduce that method to
  asserting rather than writing.

```sh
bash ~/server-setup/shot-cosmosynth.sh           # scene still -> ~/builds/shots/cosmosynth/
```

`SceneShot` runs in **edit mode**, not play mode — no domain reload, no `SessionState`
gymnastics, byte-identical output run to run. It can, because the simulation is a pure function
of time: construct, evaluate at one instant, render, discard.

---

## Testing

```sh
./run-tests.sh               # everything
./run-tests.sh Horizons      # the NASA validation, prints accuracy tables
```

Finds a .NET SDK automatically, **including the one bundled inside Unity** — no separate dotnet
is installed on this machine.

**When a numeric test fails, work out whether the code or the expectation is wrong before
touching a tolerance.** Real cases from this project:

- Hermite error 2.6e-6 "failed" a 1e-6 bound; the analytic truncation error predicts 2.3e-6, so
  the code was right and the bound was invented. Now asserts *convergence order* instead — a
  magnitude check passes with a wrong basis, an order check does not.
- Neptune's eccentricity differs from JPL by 2.4e-11. That is conditioning, not error. Now
  asserts on `a·e`, the focus displacement in km.
- Ocean coverage came out 97% when 70% was asked for. That one *was* a real bug.

Widening a bound is sometimes right — the Moon's Kepler drift genuinely is ~10%/year — but the
reason goes in the comment and in `docs/accuracy.md`.

**A tolerance loose enough to pass is not a test.** `ShippedCatalogTests` expected Jupiter's
equatorial radius to be 71492 km and passed for months on **69911** — the volumetric mean radius,
2.2% away — because the bound was 5%. The number in the test was right the whole time; the bound
was wide enough that it never had to be. A tolerance should be set by what the measurement is
worth, not by what makes the suite green: if you cannot say why a bound is the size it is, it is
too big.

---

## Data

```sh
python3 tools/cosmodata/build_solar_system.py       # 31 bodies from JPL Horizons
python3 tools/cosmodata/fetch_validation_fixture.py # 260 state vectors for the test suite
python3 tools/cosmodata/build_spacecraft.py         # 24 spacecraft: JPL trajectories, NASA models
```

Network-bound, several minutes. **The app never touches the network at runtime.**

Stdlib only, with one deliberate exception: `build_spacecraft.py` shells out to
`npx @gltf-transform/cli` because nearly every NASA model is Draco-compressed and a correct
Draco decoder is not something to hand-write. The dependency is confined to that step — the
`.csmesh` output is committed, so the app, the tests and the build need nothing but Python.

Every field carries provenance: Measured / Inferred / Procedural / Fictional, with its source.
Where Horizons publishes no GM, the importer **skips the body and says so** rather than
guessing — a fabricated mass propagates into every dependent orbit and looks entirely
plausible while doing so.

**An artefact on disk is not evidence of the run meant to produce it.** The generalisation of the
Draco trap, one step further out. `build_spacecraft.py` used to write meshes straight to their
output path, so a model the importer *correctly refused* kept the previous run's file — and
`--skip-models` then adopted it by name. That is how the inside-out SOHO nearly shipped **after**
the check had already caught it: refusal printed, old file survived, later run adopted it. Worse,
`--only` read as a filter and behaved as a truncation, replacing 24 spacecraft with 1 and
reporting success. Now: write to a temporary name and `os.replace` only on success, delete the
target on any failure, re-validate anything adopted against its own header, and merge rather than
truncate.

Four small bodies carry hand-entered GM values in `bodies.py` with paper citations, because
Horizons writes small-body masses as `Mass (10^20 kg ) = 1.08 (10^-4)` and misreading that
trailing multiplier is a factor-of-10⁴ error.

### Spacecraft

**Placement is chosen per mission, and the obvious reasoning is backwards.** Sampled tables
are for *smooth* motion — heliocentric cruise, escape hyperbolas, halo orbits about Lagrange
points, which no conic describes at all. Anything in a tight orbit about one body uses
**elements**, because uniform sampling in time puts the fewest samples exactly where the object
moves fastest. At any cadence small enough to ship, Juno crosses millions of kilometres between
samples near perijove and the interpolated path cuts the corner *through Jupiter*. A conic's
error is a slow precession; a coarse table's error is impossible geometry. `build_spacecraft.py`
refuses to write a table over 900 samples rather than letting a bad cadence through quietly.

**Every spacecraft carries the window its position is known over, and is not drawn outside it.**
Both closed forms evaluate happily forever — a conic does not know its spacecraft crashed, and
the sampled interpolator clamps rather than failing. Without the window Cassini still orbits
Saturn today. The windows come from JPL's real coverage, which must be *checked*: `-25` is Lunar
Prospector, not Magellan; the Webb ephemeris ends July 2031; Dawn's starts at 12:40 on launch
day, so a request from midnight that morning returns nothing at all.

**A silent Draco decode failure is indistinguishable from success by triangle count.** A
well-known Python mesh library, given one of these models with no Draco decoder present,
returns the right number of meshes and *exactly* the right triangle count with every vertex at
the origin, and raises nothing. `gltf.validate_decoded` checks the geometry occupies space,
which is the only cheap test that separates the two.

---

## XR / Quest configuration

Configured in code by `Assets/CosmoSynth/Editor/XrSetup.cs`, not by hand-editing the XR
settings YAML:

```sh
bash ~/server-setup/xrsetup-cosmosynth.sh     # after any manifest.json change
```

It assigns the OpenXR loader for Android, enables the Meta features, disables the ones that
break the build, sets single-pass-instanced + depth submission, and forces Active Input
Handling to "Both".

**`com.unity.openxr.feature.metaquest` is the feature that decides immersive vs. flat.** It
injects `com.oculus.intent.category.VR` into the merged manifest. Without it you get an APK
that installs, launches, and shows a flat window in the Quest shell while reporting complete
success.

The render pipeline is **Built-in**, not URP, even though the URP package is in the manifest —
`GraphicsSettings.m_CustomRenderPipeline` is `{fileID: 0}`. So materials use `Standard`, not
`Universal Render Pipeline/Lit`. Switching to URP means changing every `Shader.Find` in
`SceneShot.cs` and `CelestialBodyView.cs` at the same time, or everything renders magenta.

## Interface

Three rules, each of which exists because breaking it shipped.

**Sizes are angular, never linear.** `CosmoTheme` states type and spacing in **dmm** —
millimetres subtended at one metre. Multiply by viewing distance for metres. A panel worn on the
wrist is read at 35 cm and one beside a planet at 3 m; a size in metres that suits one is
illegible or absurd on the other.

**Containers are sized from their contents, never alongside them.** `CosmoPanel.SetContent`
measures the text through `CosmoText` and lays the backing out around the result. Every panel that
declared a width *and* a character size had them disagree — the wrist dashboard drew twelve 3.4 cm
lines onto a 12.4 cm backing and showed three and a half of them, the provenance panel was out by
four times. Note that `TextMesh.fontSize` is **not** a quality knob: rendered size is
`fontSize × characterSize × 0.1`, so "raise it for crispness" silently changes the layout.

**All controller bindings live in `XrInput.Bindings`.** Nothing else reads a button. When four
components each polled the devices, the trigger meant select *and* grab *and* boost, stick-click
meant pause *and* confirm, and A meant recentre *and* confirm — none of it visible in any one
file. The one genuinely modal control, the left stick, is arbitrated explicitly through
`ClaimNavigationStick`. The on-screen legend is generated from that same table, because the
hand-written one had already drifted from the code.

Colour comes from `CosmoTheme` alone. It was defined twice, and the two had diverged.

## Gotchas found here

- **`StreamingAssets` is not a directory on Android.** It lives compressed inside the APK,
  where `System.IO` silently reports "not found". `UniverseSimulation.ReadStreamingAsset` is a
  coroutine using `UnityWebRequest` for exactly this, and the desktop path uses it too so the
  headset never takes a branch the editor did not exercise.
- **ICRF is right-handed with z toward the pole; Unity is left-handed with y up.** Conversions
  live in `CelestialBodyView.ToUnityDirection` / `ToIcrfDirection`. Getting it wrong gives a
  mirrored solar system that orbits correctly and rotates backwards.
- **Frame-vs-vector rotation sign.** Tilting a frame by +ε transforms components by −ε.
  Reversed, the ecliptic tilt doubles to 46.9°, which still looks like a plausible solar system
  until you check an inclination against a catalogue. There is a test for it.
- **`CelestialBody` and `AtmosphereDefinition` are deliberately not `[Serializable]`.** Their
  nullable fields and dictionary are outside Unity's serialiser, so the attribute only produced
  a stream of UAC1001 warnings for a facility nothing uses. Persistence is the JSON layer.
- `ApiCompatibilityLevel.NET_Standard_2_1` does not exist in Unity 6 — it is `NET_Standard`.
  `NamedBuildTarget` needs `using UnityEditor.Build;`. `FeatureHelpers` is in
  `UnityEditor.XR.OpenXR.Features`, not the runtime `UnityEngine.XR.OpenXR.Features`.
- **Do not copy another project's XR settings YAML.** Copying holoshowcase's
  `Assets/XR` produced a `MetaQuestFeature` asset that `AssetDatabase.FindAssets` did not index
  as that type, so Meta's build hook — which bails to `null` unless it finds *exactly one* —
  threw a bare `NullReferenceException` from inside the package with nothing naming the cause.
  Deleting it and letting `XrSetup` regenerate through the official API fixed it. `XrSetup`
  now logs that count on every run so the same failure diagnoses itself.
- **Foveated rendering fails the build on Built-in RP.** OpenXR project validation treats
  "Only Legacy Foveated Rendering API usage is possible" as an *error*, not a warning, and the
  build aborts without naming the feature. It is in `XrSetup.DisabledAndroidFeatures`.
- **Anything that decorates or targets a body must ask whether the body is being drawn.**
  `RenderedRadius` survives a body being switched off, and a body that is not drawn stops
  updating its scale at all — so the stale value it keeps is whatever the minimum-visible clamp
  last produced, which can be enormous. Two bugs from this one root, both of which put a
  translucent shell across half the sky in every screenshot for weeks:
  - `AtmosphereShell` sized itself from the radius alone, so eight distant planets pinned to
    the sky sphere kept their atmospheres. The planets were correctly invisible; their air was
    not.
  - `BodySelector.Pick` aimed at the same stale radius, so the pointer picked **Dawn** — a
    spacecraft that stopped transmitting in 2018 and is outside its ephemeris window — and
    wrapped the fresnel highlight ring around it at the size of the sky.

  Everything that decorates, picks or measures a body consults
  `CelestialBodyView.IsGeometryDrawn`, never the radius on its own. Re-check it every frame:
  a selection made while a body was visible outlives the body.
- **"Needs a headset" is often measurable on the desktop.** Quest's compositor reads framebuffer
  alpha as coverage, so an additive effect leaking into alpha punches an opaque hole in
  passthrough — invisible in every desktop capture, because the capture forces an opaque clear so
  the PNG is viewable. `PlayModeShot.ProbeDestinationAlpha` clears alpha to zero instead and
  reports what came back, which turns the question into a number. Attribute the result with
  `COSMOSYNTH_HIDE` rather than reading the total: coverage from a solid planet is correct, and
  only an additive effect contributing to it is a bug. Measured 2026-07-31 — the star sprites do
  **not** write alpha (hiding the field moved nothing), and all coverage came from opaque bodies.
- **When you cannot tell what a shape on screen is, enumerate renderers by *apparent* size**
  (world bounds ÷ distance to camera), not by world size — a three-unit sphere four units away
  fills the view while a thousand-unit orbit line does not. Then bisect with
  `COSMOSYNTH_HIDE="<path substring>"`, which `PlayModeShot` honours before capturing. Guessing
  from the source cost several rounds; the two together named it in one.
- **A point-spread texture cannot make a two-pixel quad round.** The star field was given a
  Gaussian sprite and *still* rendered as square patches. The sprite was fine and the shader was
  fine: the profile fades to nothing well inside its own quad, so the visible core is about half
  the quad's width, and a magnitude distribution of `pow(u, 3.2)` put almost every star at the
  size floor — a core one to two pixels across. **A one-pixel feature has no shape**, whatever
  texture it is cut from. The fix is to raise the size floor until the falloff has pixels to
  resolve in, and carry faintness in alpha instead: a dimmer star is not a smaller star. Verify
  by printing the pixel neighbourhood of one star, not by looking at a thumbnail — the failure
  and the fix are indistinguishable at 1:1.
- **A minimum apparent size needs a maximum to go with it.** The rule that stops a body
  vanishing has no upper bound, and applied to a spacecraft it is unbounded in the worst way: a
  3 m Viking lander was inflated to ten thousand kilometres — larger than Mars — because from
  Earth the whole planet is three pixels. The result was a **completely black screen**, the
  camera inside a lander the size of a planet, with passthrough working and nothing in it.
  `CelestialBodyView` now caps a model at a quarter of its parent's radius and draws nothing
  where that ceiling is already below the visibility floor. Same shape as the orbit-line rule:
  worth drawing only when bigger than a dot *and* smaller than its host.
- **An orbit is only worth drawing from outside it, and only if it is bigger than a dot.**
  `OrbitLineRenderer` culls on both. From inside, a conic projects to a band across the whole view
  — that is the hairline crossing both shipped screenshots, and it was Earth's own orbit seen from
  Earth. Below about eight pixels across it is a blob on the planet; at table-top scale every moon
  orbit was being drawn thousands of times wider than the path itself. The tail gradient is
  anchored by rotating the vertex array so index 0 is the body, because a gradient runs along the
  array and the array starts at periapsis, not at the planet.
- **`CosmoSynthBootstrap.OnCatalogLoaded` runs three times now** — Solar System, then spacecraft,
  then exoplanets. Anything it creates must clear its previous output first, or you get two of
  everything. Orbit lines did, which doubled both the cost and the apparent line opacity.
  **The mirror image of that bug bites anything that decorates views rather than creating them.**
  `SetCatalog` destroys and recreates every `CelestialBodyView`, so work already applied to a
  view is silently lost on the next merge. `SpacecraftModelLoader` tracked which *bodies* it had
  served and skipped them forever, so it reported "24 loaded" while the ISS was still a grey
  sphere. Drive the decision from what the view actually has (`view.HasModel`), never from what
  the component remembers doing — then it self-heals. Cache by asset path, not by body id.
- **Body positions are `localPosition`, not `position`.** Everything is parented to the
  simulation transform, which is the universe's anchor in the room. Writing world position pins
  the solar system to the play-area origin and makes the anchor silently do nothing.
- Installing `com.unity.inputsystem` flips Active Input Handling to New-only, and every legacy
  `Input.GetKeyDown` then throws at runtime while the build reports success. `XrSetup` forces
  "Both".
