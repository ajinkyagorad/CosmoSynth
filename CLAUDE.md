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

---

## Data

```sh
python3 tools/cosmodata/build_solar_system.py       # 31 bodies from JPL Horizons
python3 tools/cosmodata/fetch_validation_fixture.py # 260 state vectors for the test suite
```

Stdlib only, network-bound, several minutes. **The app never touches the network at runtime.**

Every field carries provenance: Measured / Inferred / Procedural / Fictional, with its source.
Where Horizons publishes no GM, the importer **skips the body and says so** rather than
guessing — a fabricated mass propagates into every dependent orbit and looks entirely
plausible while doing so.

Four small bodies carry hand-entered GM values in `bodies.py` with paper citations, because
Horizons writes small-body masses as `Mass (10^20 kg ) = 1.08 (10^-4)` and misreading that
trailing multiplier is a factor-of-10⁴ error.

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
- **Body positions are `localPosition`, not `position`.** Everything is parented to the
  simulation transform, which is the universe's anchor in the room. Writing world position pins
  the solar system to the play-area origin and makes the anchor silently do nothing.
- Installing `com.unity.inputsystem` flips Active Input Handling to New-only, and every legacy
  `Input.GetKeyDown` then throws at runtime while the build reports success. `XrSetup` forces
  "Both".
