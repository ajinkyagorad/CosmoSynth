# Lane tooling — report

Status: ready for review (Phase 0a)
Branch: `lane/tooling`
Worktree: `~/projects/CosmoSynth-wt/tooling`
Authority: `docs/design/2026-09-11-D-process.md` §D3 (tooling port spec) and §D4 (gates)

## Summary

`~/projects/CosmoSynth/tools/` did not exist as a toolchain: the four scripts in
`~/server-setup/` were copy-pasted, unversioned, ungated, used a display band shared with
six other projects, and wrote straight into the published gallery. It now exists, in git:
one `common.sh` with a flock editor queue and resource guards, a generic editor runner
that can tell a resolution bug from a module failure, a build that refuses more than it
accepts, an artefact verifier with a proof harness that runs on the real 1.9.0 APK, a
play-mode capture wrapper that cannot write to the gallery, worktree lanes, four static
gates that each fail on a known-bad input, and a runner that prints ABSENT for the four
gates this lane did not write.

`run-tests.sh` now runs from a Library-less worktree: **142 passed, 0 failed** in 9 s from
`~/projects/CosmoSynth-wt/tooling`, which previously died with 16 CS errors before the
suite started.

## Files

```
 M run-tests.sh                    ScriptAssemblies fallback, mktemp log, per-tree lock, nice
?? tools/common.sh                 paths, band 461-490, editor_lock, guards, unity_run, runner_bug
?? tools/editor-method.sh          generic -executeMethod runner; --self-test
?? tools/build.sh                  G0 -> G1a -> G1b -> G1c -> G2 -> G3 -> notes
?? tools/verify-apk.sh             G3 artefact gate; STORE=1; --self-test on the .KEEP 1.9.0
?? tools/shot.sh                   play-mode capture; --rev, --queue; never the gallery
?? tools/lane.sh                   add / list / check / rm worktree lanes
?? tools/gate-all.sh               the G0 runner; ABSENT for every gate that does not exist
?? tools/xr-baseline.txt           accepted boot.config xr-block MD5s
?? tools/gates/g0.sh               the name D §D3.3 uses; execs ../gate-all.sh
?? tools/gates/stereo_gate.sh      G0b
?? tools/gates/settings_gate.py    G0c
?? tools/gates/code_gate.sh        G0d
?? tools/gates/class_gate.sh       G0e
?? tools/gates/qsettings_scan.py   the baked-MSAA scan used by G3
?? tools/gates/known-red.txt       empty on purpose
?? tools/gates/test-floor.txt      142
?? docs/reports/tooling.md         this file
```

All shell scripts are LF-only and `bash -n` clean; both Python files parse.

## What each script does

| Script | What it does |
|---|---|
| `tools/common.sh` | Sourced by everything. Paths (`REPO`, `PROJECT`, `STAGE` outside `~/builds`, `OUTDIR`), Xvfb band **461-490**, and: `resolve_unity` (falls back to the newest Hub editor and says so, instead of "no such file"); `start_xvfb` (first free display in the band, kept only if it survives 3 s, killed by an EXIT trap, never a pattern pkill); `editor_lock` (flock on `/tmp/cosmosynth-editor.lock`, a no-op when `COSMOSYNTH_LOCK_HELD` is set so a child cannot deadlock against its parent, then a pgrep refusal for a foreign editor, then `reap_band`); `reap_band` (while holding the only lock, every Xvfb in our band is stale by definition — PIDs read from the lock files); `guard_resources DISK MEM` (exit 6); `require_main_clean` (exit 8 for a non-master branch, a dirty tree, or the `--rev` detach sentinel); `unity_run` (never `-nographics`, always `-buildTarget Android`, `timeout -k 60`, 124/137 mapped to 9, log to `mktemp` under `unity/Logs`, log filter widened to `^\[[A-Za-z][A-Za-z0-9]*\]`); `runner_bug` (Unity could not resolve the method). |
| `tools/editor-method.sh` | The only way lane code reaches the editor. Lock, guards, display, `unity_run` at `METHOD_TIMEOUT` (1200 s). On "could not be found" it prints **RUNNER-BUG**, names the caller as the defect, and exits 11 — so a wrong namespace can never be logged as a module failure. `--self-test` exercises everything in `common.sh` except the Unity launch. |
| `tools/build.sh` | `require_main_clean`; refuses unless `git rev-parse --show-toplevel` is main; reads `Version`/`VersionCode` from `ServerBuild.cs`; version gate against `aapt2 dump badging` of every APK in `$OUTDIR` and `$STAGE` (artefacts, not names) plus a filename-collision refusal; refuses while `known-red.txt` has any non-comment line; `gate-all.sh --full`; lock, `guard_resources 8 10`, display; G1a XrSetup (needs `XRSETUP_OK`); G1b Diagnostics (needs `DIAGNOSTICS_OK`, zero `DIAGNOSTICS_FAIL`, zero `error CS`); G1c `shot.sh --views release` (needs the report to name HEAD with dirty 0); G2 `BuildAndroid` under `timeout 3600` with `/usr/bin/time -v`, APK at exactly `$STAGE/CosmoSynth-$VER.apk`; G3 `verify-apk.sh`; then the notes file. No skip flags. |
| `tools/verify-apk.sh` | G3. Counts matches, never `grep -q`. Reads the artefact only. |
| `tools/shot.sh` | Wraps `PlayModeShot.Run` with `NOQUIT=1`. Default `/tmp/cosmosynth-shots/<sha7>/`; `--out` under `~/builds/shots` is refused outright. `--rev` detaches main, writes the `/tmp/cosmosynth-main-detached` sentinel every other tool checks, and restores master plus `git clean -fdq -- unity/Assets` from a trap. `--queue` serves `/tmp/cosmosynth-shotq/*.req` oldest first and writes `<epoch>.done` or `.failed`. Treats any `ASSERT_FAIL` in the log as exit 14. |
| `tools/lane.sh` | Worktrees, not rsync clones — Worldfall's lanes "worked for parallelism and failed at integration". `add` branches `lane/<name>` off `green`, else `phase0-contracts`, else `master`, and refuses a name not in `tools/lanes.txt` (when that file exists). `rm` refuses a branch not merged into master. `list` shows each lane's report Status line. |
| `tools/gate-all.sh` | Runs G0a-G0h, one line each, `--full` for build.sh, `--self-test` to run every gate's own self-test. Prints **ABSENT** with the missing filename for any gate that does not exist, counts them, and says in as many words that "all green" covers only the gates that ran. `--full` exits 10 when anything is absent. |

## What each gate proves, and what it cannot

| Gate | Proves | Cannot prove |
|---|---|---|
| **G0a** `run-tests.sh` | The engine-free Core layer is correct against its own tests, and the Unity runtime layer still compiles against the editor's assemblies. The floor (142) means a deleted test cannot pass silently. | Nothing about Unity, rendering, the editor layer (no `CosmoSynth.Editor.Compile` project exists yet), or the device. |
| **G0b** `stereo_gate.sh` | Every shader with a vertex stage carries all six single-pass-instanced macros; CosmoSynth shaders are named `CosmoSynth/…`, tagged `UniversalPipeline`, free of unjustified variants and `ZTest Always`, and any additive pass leaves destination alpha alone. | That the shader compiles, that the macros are used correctly, or that the passthrough composite is right on a device. The additive-blend rule is file-scoped, not pass-scoped (marked `ponytail:`). |
| **G0c** `settings_gate.py` | The serialised YAML holds the decided values: a custom render pipeline is set, its URP asset has MSAA 4 / HDR off / depth and opaque off / SRP batcher on / render scale 1, the Android quality level does not override it, colour space is Linear, the Android OpenXR document has renderMode 1, depthSubmissionMode 0, latencyOptimization 1, symmetricProjection 1, optimizeBufferDiscards 1, foveation API 1, exactly one `MetaQuestFeature`, hand-tracking off, exactly one "Android Providers" manager, and no duplicated `Assets/XR N` dirs. Cross-checks buffer discards against G0d's camera count. | That Unity applies any of it at build time — that is G1b through a different source (the editor API), which is the point of having both. Field names were read off a real URP asset and this project's own OpenXR settings, never from memory. |
| **G0d** `code_gate.sh` | Exactly one `AddComponent<Camera>`; none of `CopyFrom(`, `CameraClearFlags.Depth/Nothing`, `targetTexture =`, `Graphics.DrawMesh/RenderMesh/Blit`, `CommandBuffer`, `OnRenderImage`, `GL.`, `Camera.current`; `Shader.Find(` only in `CosmoShaders.cs`; axis swaps only in `FloatingOrigin.cs`; literal colours only in `CosmoTheme.cs`; `SetCatalog(` called at most once; every `RenderedRadius` reader also names `IsGeometryDrawn`. Writes the camera count for G0c and G3. | That the one camera is configured correctly, or that code not under `Runtime/**` is clean. A `// gate-justified:` line excuses a hit (and is still counted) — the escape hatch is itself covered by the self-test. |
| **G0e** `class_gate.sh` | Every `.cs` declares a type named after its file, holds at most one `MonoBehaviour`, and that MonoBehaviour's name equals the file name — the trap where a second class is silently stripped from a headlessly-saved prefab and the app "partially works, no errors" on device. | Anything about behaviour, and nothing about types declared in files Unity never imports as MonoScripts. |
| **G3** `verify-apk.sh` | Of the artefact: package, `versionName` against the filename, not debuggable; the boot.config OpenXR wiring, xr-entry count, offscreen swapchain, and the **whole xr block by MD5** against a headset-confirmed baseline (individual greps pass while a value inside the block has changed); the four XR `.so` files; arm64-only; the VR intent category read from `xmltree` (badging cannot print it); headtracking, passthrough, hand tracking, OpenXR; the absence of six permissions; hygiene items as warnings, or failures with `STORE=1`; every StreamingAssets file present at the same size, derived from the tree; every `CosmoShaders.All` name present in the built data; the font and TMP shader; the project's own URP asset by name; the baked per-level MSAA decoded and self-checked against the project YAML. | Behaviour, and that the *values* inside those files are right. It reads the artefact, never the build log. |

## Proof that each gate can fail

Every self-test builds a good fixture, asserts it passes, then mutates it once per rule and
asserts each mutant fails. A gate that cannot fail is decoration; a gate that cannot pass is
equally broken, so both directions are checked.

```
G0b  good passes; 7 mutants fail: no UNITY_VERTEX_OUTPUT_STEREO, no instancing pragma,
     wrong shader-name prefix, no URP tag, unjustified multi_compile, ZTest Always,
     additive blend writing alpha.                                        SELF-TEST PASS
G0c  good fixture passes; 10 mutants fail by name: pipeline, colour-space,
     depth-submission, latency, symmetric-projection, msaa, render-mode,
     metaquest-feature, handtracking-off, android-providers. PLUS the historical
     negative control: the real 1.7.2 tree (c450708) FAILs on pipeline, colour-space,
     depth-submission, latency and symmetric-projection, each by name.    SELF-TEST PASS
G0d  good passes; 10 mutants fail: second camera, CopyFrom, Depth clear, targetTexture,
     Graphics.DrawMesh, GL., Shader.Find outside CosmoShaders.cs, axis swap outside
     FloatingOrigin.cs, colour outside CosmoTheme.cs, RenderedRadius without
     IsGeometryDrawn; and a `// gate-justified:` hit still passes.        SELF-TEST PASS
G0e  good passes; 3 mutants fail: two MonoBehaviours in one file, a MonoBehaviour named
     unlike its file, a file with no type named after it.                 SELF-TEST PASS
G3   the whole verifier on the .KEEP 1.9.0: xrmd5 ok, mf-vr ok, lib-meta ok (checks can
     pass); vername FAIL, font FAIL, urp FAIL (checks can fail); the string matcher
     reports an impossible shader name as absent; qsettings SELF-CHECK PASS against
     `git show bcb2886:unity/ProjectSettings/QualitySettings.asset`.      SELF-TEST PASS
common.sh via `tools/editor-method.sh --self-test`: editor resolved; runner_bug both
     ways; guard_resources refuses an impossible threshold and accepts a real one;
     start_xvfb takes :461 in band and releases it; the lock is re-entrant for a child
     and exits 4 for a stranger; require_main_clean refuses today's dirty main with 8.
                                                                          SELF-TEST PASS
```

## Gate results on the current tree (cf93a25, the 1.9.0 source)

```
G0a   test suite (floor 142)        PASS    142 passed, 0 failed
G0b   shader stereo macros          FAIL    CosmoAtmosphere.shader, CosmoRim.shader have no UniversalPipeline tag
G0c   settings YAML                 FAIL    14 checks: Built-in pipeline, gamma colour space,
                                            depthSubmissionMode 1, latency 0, symmetricProjection 0,
                                            hand tracking on, foveation feature off, 5 "Android Providers"
G0d   runtime code invariants       FAIL    2 AddComponent<Camera>, 1 CopyFrom, 3 CameraClearFlags.Depth,
                                            5 axis swaps, 32 literal colours, 5 SetCatalog calls,
                                            5 RenderedRadius readers without IsGeometryDrawn
G0e   MonoScript filename/class     FAIL    Provenance.cs, Json.cs declare no type of that name
G0f   data documented + droppable   ABSENT  tools/gates/data_gate.py does not exist - NOT CHECKED
G0g   unique input bindings         ABSENT  tools/gates/binding_gate.py does not exist - NOT CHECKED
G0h   lane ownership                ABSENT  tools/gates/own_gate.py does not exist - NOT CHECKED
exit 10
```

These are findings, not tooling defects: the tree is still 1.9.0, which is Built-in, gamma,
and has **two cameras** — the 1.7.0 bug class D §D4 was written to catch. Each was
spot-checked by hand (`Provenance.cs` declares `ProvenanceClass`, `FieldProvenance` and
`ProvenanceRecord` but nothing called `Provenance`). They go green through P0.4/P0.5 and
the Foundation lane, not through an edit here — this lane does not touch `unity/Assets`.

`verify-apk.sh` on the 1.9.0 reference likewise finds `versionName` mismatched to the
filename, no TMP font, `AA 2 baked while G0d counted 2 cameras`, and four store-hygiene
warnings (INTERNET, BOUNDARY_VISIBILITY, no pre-splash, targetSdk 36).

## Deviations from D §D3, and why

1. **`tools/gate-all.sh` is the G0 runner; `tools/gates/g0.sh` execs it.** D §D3.7 names
   `g0.sh`, the lane brief names `gate-all.sh`. One implementation, both names, no second
   copy to drift.
2. **`build.sh` stops at G3 + notes and prints `RESULT=STAGED`, not `RESULT=OK`.** G4
   publish needs `tools/shotname.py` (§D3.11), the gallery copies, the git tag and the
   handoff writer (§D6.7), all integrator-owned and out of this lane's scope. The APK stays
   in `$STAGE`. Nothing in this lane can write to `$OUTDIR` or `~/builds/shots`.
3. **G1b and G1c are hard steps in `build.sh` even though `ServerBuild.Diagnostics` and the
   `PlayModeShot` assertions do not exist yet.** D says "no skip flags", so they fail closed
   (RUNNER-BUG, exit 11) rather than being conditional. `build.sh` therefore cannot succeed
   until P0.4c/P0.4e land — which is correct for Phase 0, but means the build path beyond
   the G0/version gates is **UNVERIFIED end to end**.
4. **The URP check does not accept the string `UniversalRenderPipelineAsset`.** D §D3.4
   offers it as an alternative pattern. It is present in the built data of any project that
   merely has the URP package installed: the self-test caught it reporting `ok` on the
   Built-in 1.9.0 APK. The check now resolves the project's own URP asset name through
   `GraphicsSettings.m_CustomRenderPipeline` → the guid's `.meta` → the filename, and a
   project with no custom pipeline fails. (`verification-gap-pattern`: the check was green
   for a reason unrelated to its name.)
5. **`mf-headtracking` matches `headtracking`, not `com.oculus.feature.HEADTRACKING`.** The
   real manifest string is `android.hardware.vr.headtracking`; the Oculus spelling reported
   a correct 1.9.0 as a 3DOF panel.
6. **`verify-apk.sh --self-test` does not assert a fixed verdict for the shader check.** D's
   table expects `CosmoSynth/Planet` to be absent from 1.9.0, but the shader list is derived
   from today's `CosmoShaders.All`, whose `CosmoSynth/Atmosphere` and `CosmoSynth/Rim` *are*
   in that APK. Instead the harness asserts `shaders-absent`: the matcher reports a
   deliberately impossible name as absent, which proves the same thing without depending on
   the shader set of the moment.
7. **Gate fixtures are generated in `mktemp -d`, not committed under `tools/gates/fixtures/`.**
   Same proof, no fixture files to drift from the rules they are supposed to exercise.
8. **The additive-blend rule in G0b is file-scoped, not pass-scoped.** A file mixing one
   guarded and one unguarded additive pass would pass. Marked with a `ponytail:` comment and
   the upgrade path (a per-`Pass` parser).
9. **`tools/gates/{data_gate.py,binding_gate.py,own_gate.py}` are not written.** Out of the
   lane's scope. They are listed in `gate-all.sh` so they report ABSENT rather than being
   invisible, and `lane.sh check` says the ownership gate is not running rather than
   returning green.
10. **`tests/CosmoSynth.Editor.Compile` is not created** (D §D3.8 time-boxes it to 2 h and
    allows dropping it). `run-tests.sh` builds it when it appears.
11. **`~/server-setup/*-cosmosynth.sh` are NOT retired** (D §D3.12 wants an `exit 2` line
    inserted). Editing `~/server-setup` is outside this worktree and outside the lane; left
    for the integrator. Until then the old ungated scripts still work, still use band
    141-180, and `playshot-`/`shot-cosmosynth.sh` still default to writing into the
    published gallery.
12. **`unity_run` logs to `mktemp` inside `unity/Logs/`**, satisfying both D §D3.1's log path
    and the lane brief's "logging to mktemp"; the directory is already gitignored.
13. **Exit code 11 is used for RUNNER-BUG.** D §D3.0 leaves 11 unassigned.

## What this lane did NOT verify

- **No Unity editor was launched.** The box was at 97 % disk (15 GB free) with another
  session's Unity editor mid-run on Worldfall, so a second editor was the wrong trade for
  the information. `editor-method.sh --self-test` covers the lock, the guards, the band, the
  log filter and the RUNNER-BUG detection on a synthetic log; **the Unity launch itself, and
  therefore G1a/G1b/G1c/G2, are unverified end to end.**
- `shot.sh --rev` and `--queue` are unexercised (they need the editor).
- `build.sh` past the G0/version gates is unexercised, for the same reason.
- The `xr-baseline.txt` MD5 is the confirmed 1.9.0/Mechanarium/Microverse block only. The
  MVPVV variant `9dd8284e…` is deliberately commented out: if the Foundation turns MVPVV on,
  G3 will fail until somebody confirms that block in a headset and moves the line.

## Requests to the integrator

- Retire `~/server-setup/{build,shot,playshot,xrsetup}-cosmosynth.sh` per D §D3.12 (one
  `exit 2` line each). They are outside every worktree.
- Create `tools/lanes.txt` (D §D2.15); `lane.sh add` allows any name until it exists.
- Write `tools/gates/{data_gate.py,binding_gate.py,own_gate.py}`, `tools/shotname.py`, and
  `build.sh`'s G4 publish stage; raise `tools/gates/test-floor.txt` after each merge.
- `ServerBuild.cs` must gain `Diagnostics()` and take its output directory from
  `COSMOSYNTH_OUTDIR` (today it hardcodes `/home/triton/builds/cosmosynth`, so it would
  publish straight past the gates).

## VERIFIED / INFERRED

- VERIFIED: band 461-490 is claimed by no `seq` in `~/projects/*/tools`, `~/projects/*/*.sh`
  or `~/server-setup/*.sh` (highest other bound is 460), and had no live display or lock at
  measurement.
- VERIFIED: 142 passed, 0 failed from the Library-less worktree with the ScriptAssemblies
  fallback; 9 s wall.
- VERIFIED: every self-test result quoted above, run on triton on 2026-09-12.
- VERIFIED: the 1.9.0 xr block hashes to `2e1fc6ccd7f09d6cbe013eb1c74e9e02`.
- INFERRED: the resource thresholds (5/6 GB for editor methods, 8/10 for builds) are the
  spec's guesses, marked `ponytail:` in `common.sh`; tune from the first measured IL2CPP peak,
  which `build.sh` logs.
- INFERRED: `PlayModeShot.Run`'s argument names (`-shotViews`, `-shotCommit`, `-shotDirty`,
  `-shotHide`, `-shotProfile`) follow D §D3.10; the current `PlayModeShot.cs` takes
  `-shotOut`, `-shotFrames`, `-shotView`, so `shot.sh` will not work until P0.4c rewrites it.

## Open questions

1. Should `build.sh` refuse when `$STAGE` and `$OUTDIR` disagree about the newest
   versionCode, rather than taking the max of both? Today a stale staged APK raises the bar.
2. `xr-baseline.txt` holds one hash. If P0.5's XrSetup legitimately changes the block, the
   first 2.0 build fails G3 by design. Who confirms the new block, and on which headset run?
