# CosmoSynth

*Quest 3 · simulation · validated against NASA*

A solar-system laboratory: an n-body integrator you can hold, with the physics checked against real ephemerides rather than trusted by eye.

Built and shipped through the [ClaudeAI-UnityMCP-APK pipeline](https://github.com/ajinkyagorad/ClaudeAI-UnityMCP-APK-pipeline) — a headless Unity build server driven by Claude Code, with APKs served to the headset over the browser.

## What is here

| | |
|---|---|
| Unity | 6000.5.5f1 |
| Product | CosmoSynth |
| Package | `com.triton.cosmosynth` |
| Scenes | SolarSystemLab |
| Source in this repo | 2.0 MB across 251 files |

## AI

Built with Claude Code. The .NET test host under `tests/` exists so the integrator can be verified outside Unity, which is what makes the NASA comparison practical.

## Notes

- **Validated against NASA JPL Horizons** — 260 real state vectors are committed as fixtures; `./run-tests.sh Horizons` prints the accuracy tables.
- The Unity project is nested under `unity/`; the solver is a plain netstandard library so it can be tested without the editor.

## Build

```bash
# 1. open once in the editor so Unity restores Packages/ and regenerates Library/
Unity -projectPath unity

# 2. or build headless (Android / Quest)
Unity -batchmode -projectPath unity \
      -executeMethod ServerBuild.BuildAndroid -logFile build.log
```

Requires **Unity 6000.5.5f1** with the Android module. First open takes a few minutes
while Unity rebuilds `Library/` from what is in this repo — that directory is
regenerated state and is deliberately not committed.

**Do not build with `-nographics`.** It drops the Vulkan `xr-*` entries from
`boot.config`, and the APK then shows a black screen instead of passthrough while
reporting a successful build.

## Related

- [ToolBench XR](https://github.com/ajinkyagorad/toolbench-xr) — Quest 3 · mixed reality
- [Holo Idea Tests](https://github.com/ajinkyagorad/holo-idea-tests) — Quest 3 · mixed reality · sketches
- [Planetary Vitals](https://github.com/ajinkyagorad/PlanetaryVitals) — Quest 3 · mixed reality · real data
- [MRI Holo Viewer](https://github.com/ajinkyagorad/MRI-Holo-Viewer) — Quest 3 · passthrough · medical imaging
- [AI-XR](https://github.com/ajinkyagorad/AI-XR) — Quest 3 · passthrough · on-device inference
- [PRISM](https://github.com/ajinkyagorad/Prism) — Quest 3 · mixed reality · fully procedural
- [PlanetGolf](https://github.com/ajinkyagorad/PlanetGolf) — Quest 3 · mixed reality · orbital mechanics

---

Source only: `Library/`, `Temp/`, `obj/` and build output are regenerated and are not committed.
