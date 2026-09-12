# Foundation review: every issue and what was done about it

**Date:** 2026-09-12
**Branches:** `lane/foundation-shaders` (43a39d5), `lane/foundation-code` (the merge commit below)
**Scope:** the 36 issues raised against the two foundation lanes.

**Disposition summary: 36 fixed, 0 rejected, 0 deferred.** One part of one issue — §7.4's
*numeric re-tune* inside issue 12 — is code-complete but its measurement is a G1d step that needs
an editor run and a capture; the code defect it hung off is fixed and the tuning is the
integrator's, which is where the contract already puts it (§7.4, §9.1 P0.5).

---

## The composition decision

Issues 1, 23 and 24 are one defect seen from three sides: the two lanes do not compose, and
neither lane alone could fix it. CONTRACT P0.4b assigns the shaders and the `Shader.Find`
migration to the same step.

So the lanes were **not** merged as separate commits. The shader lane's own defects were fixed and
committed on `lane/foundation-shaders`; then, on `lane/foundation-code`,
`git merge --no-commit lane/foundation-shaders` was followed by the call-site migration and every
remaining C# fix, and the result is **one commit** in which the eleven shaders and the code that
asks for them arrive together. There is no tree, at any point in the history of either branch,
where `CosmoUrpConfig` has made the project URP while a runtime site still asks for `Standard`.

Integrating is therefore: merge `lane/foundation-code`. It already contains
`lane/foundation-shaders`.

---

## Blockers

### 1. The two lanes do not compose (`CelestialBodyView.cs` and seven other sites) — FIXED

All eight sites migrated, plus two the review's list did not name:

| was | now |
|---|---|
| `CelestialBodyView.cs:84` — `CreatePrimitive`'s implicit material (URP/Lit) | `CosmoShaders.Material(CosmoShaders.Body)` |
| `CelestialBodyView.cs:435` — `Standard`, per spacecraft submesh | `CosmoSynth/Body` |
| `SkyAndRings.cs:529` — `Unlit/Transparent` (rings) | `CosmoSynth/Ring` |
| `SkyAndRings.cs:458` — `Particles/Standard Unlit` (star field) | `CosmoSynth/Point`, sky mode |
| `StarPointBillboard.cs:45` — `Particles/Standard Unlit` (point-path bodies) | `CosmoSynth/Point`, object mode |
| `OrbitLineRenderer.cs:83` — `Sprites/Default` on a `LineRenderer` | `CosmoSynth/Ribbon` mesh |
| `CosmoTheme.cs:286,297` — `Sprites/Default` (panel, row) | `CosmoSynth/Panel` |
| `AnchorBeacon.cs:65`, `BodySelector.cs:70` — `Unlit/Color` | `CosmoSynth/Unlit` |
| `CosmoText.cs:78` — `new Material(Font.material)`, i.e. `GUI/Text Shader` | `CosmoSynth/Text` + the font's atlas |

`StarSprite.cs` is deleted: its point-spread texture is analytic in `CosmoSynth/Point`, and it was
the last thing holding the built-in particle shader.

`CosmoShaders`' six `[Obsolete]` built-in constants are deleted with their last callers, and
`using System;` with them.

**Lighting, as the fix requires.** Without it every planet is lit from world +Y by
`_LightDirWS`'s default. `CelestialBodyView.ApplyLighting` resolves the host star **once** through
`CelestialCatalog.HostStarOf` — the parent chain, not `RootIds[0]` — and writes `_LightDirWS`,
`_LightColor` (black body × `StarLook.StarLightIntensity` = 0.85) and `_Ambient`
(`CosmoTheme.AmbientFill`) onto the sphere and every spacecraft submesh each frame, plus
`_PerVertexCompress` from `PerVertexCompressed`. `RingSystem` reads the same two values off the
view at order 60 and adds `_ParentCentreWS` (the parent's **drawn** centre and radius), so the
ring's analytic planet shadow is the right size at every scale.

`SkyAndRings.BuildStarLight` is deleted (CONTRACT §8.2 item 12) together with the ambient
directional fill — which removes the `RootIds[0]` defect at its source and one of issue 12's two
bad call sites. Only then were `m_MainLightRenderingMode` and `m_AdditionalLightsRenderingMode`
flipped to 0 in `CosmoUrpConfig.AssetExpect`, which is the condition that file's own note set.

### 2. `CS_EyePositionWS()` absent; seven shaders read URP's camera helper — FIXED

Added to `CosmoCore.hlsl` verbatim from CONTRACT A.2 and substituted at all seven sites (Body,
Sky, Point, Ribbon, Volume, Atmosphere, Rim).

**One deviation from A.2, deliberate.** §7.1 rule 2 bans *the string* anywhere under
`Runtime/Shaders`, comments included, and A.2's own verbatim comment contains it. A gate enforced
by grep has to be grep-clean in the documentation that explains it, or the first thing it flags is
its own comment. `CosmoCore.hlsl` therefore describes the helper in prose and says why. G0b now
has a mutant for the string in a comment as well as in code.

### 3. `Volume.shader` fragment reads `TransformWorldToObject` — FIXED

`float3 eyeOS : TEXCOORD1`, computed in `vert` after `UNITY_SETUP_INSTANCE_ID`, read as
`i.eyeOS` in `frag` — CONTRACT A.7 verbatim. It is also one 4×4 multiply per primitive instead of
per pixel.

### 4. `CS_IGN(i.positionCS.xy)` jitters in pixel space — FIXED

`CS_JitterDir(dir)` added to `CosmoCore.hlsl` (A.2's ninth helper) and used; `CS_IGN` deleted.

### 5. `float4x4 _LayerToWorld` in `UnityPerMaterial` — FIXED

Deleted. Both the layer branch and the sky branch rotate through the global `_CS_IcrfToWorld`
(CONTRACT §3.4, §8.2 item 13). `_Space` now defaults to **1** (object space, the branch that needs
nothing configured) per issue 27, and the rotation refuses to normalise a vector the matrix
collapsed. Batcher engagement still has to be confirmed in the Frame Debugger — a G1 step.

### 6. `Placement.PublishGlobals` below the catalogue guard — FIXED

`Placement.PublishConstants()` publishes d0, 1/d0, the sky radius and an identity `_CS_IcrfToWorld`
from `ScaleRenderer.OnEnable`, so they are never zero. `LateUpdate` now publishes the full globals
**above** the `Catalog == null` return, so the eye and the mapping are live from frame 1 — which
matters because the catalogue arrives over three `UnityWebRequest` reads out of a compressed APK.

### 7. `_Fade` absent from seven shaders — FIXED

Added to Body, Atmosphere, Rim, Panel, Sky, Text and Unlit in both `Properties` and
`UnityPerMaterial`, multiplying colour and — in the opaque and premultiplied passes — alpha. Sky
keeps `_Immersion` alongside it as a separate switch. All eleven now carry it; G0b checks both
halves separately, with a mutant for each.

### 22. `CatalogAssembler.Assemble` untested, and promotes a later source — FIXED

`tests/CosmoSynth.Core.Tests/CatalogAssemblerTests.cs`, six tests: base-first fixes the name and
`RootIds[0]`; a **malformed base returns null** rather than promoting source 2; a **missing base**
likewise; a missing middle source is skipped without affecting the rest; the merge count is new
ids, not bodies offered; nothing at all assembles to null.

`Assemble` now returns null on a bad base and logs *"base document unusable: refusing to promote a
later source"*. The failing case the review named — `[("solar_system", "{bad json"), ("spacecraft",
valid)]` silently returning the spacecraft catalogue as the base — is now a test.

### 23. No body ever asks for `CosmoSynth/Body` — FIXED

`BuildVisual` assigns `CosmoShaders.Material(CosmoShaders.Body)` to the sphere and each spacecraft
submesh, with `_Glossiness`→`_Smoothness`, `_BumpMap`→`_NormalMap` (always bound, no keyword),
`_MetallicGlossMap`→`_SurfaceMap`, `_EMISSION`+`_EmissionColor`→`_Emission` (a colour; black is
off). `MaximumModelMetallic` is deleted with the metallic term — `CosmoSynth/Body` has none, and a
term whose only correct value is zero is better deleted than clamped.

`code_gate.sh` gains two rules, each with a mutant that fails:

- **a CosmoShaders member outside the eleven.** This is the rule that would have caught the whole
  pre-migration tree, and it explains why G0d was green on it: the built-in names lived as
  constants *inside* the exempt `CosmoShaders.cs`, and the call sites referred to them by
  identifier, so no built-in shader **string** ever appeared in `Runtime/`. The grep must be `-o`,
  per occurrence — a line-based filter drops `CosmoShaders.Material(CosmoShaders.Standard)`
  entirely because the line also matches an allowed name. The first version of this rule had
  exactly that bug and its mutant passed; the mutant is what found it.
- **`new Material(` outside `CosmoShaders.cs`**, which is the shape `CosmoText` used.

**What grep still cannot see** is `CreatePrimitive`'s *implicit* default material. Nothing in the
source names it. That hole is closed at G1c instead, by issue 13's assertion.

### 24. Shaders expose an interface the calling code cannot drive — FIXED

`[MainColor] _BaseColor` on Body, Unlit and Ring; `[MainTexture] _BaseMap` on Body and Ring;
Panel gains `[MainTexture] _BaseMap` **and** a new `[MainColor] _BaseColor`, multiplied into both
the fragment colour and its alpha, because `CosmoPanel` builds from Unity's Quad (no COLOR
channel) and sets colour through `material.color`. `Text` is unchanged: `TextMesh` does bake
vertex colours, and `_MainTex` is already the name `Material.mainTexture` binds to.

---

## Majors

### 8 & 25. The star-field fallback *is* the mirror — FIXED, at the root

These are the same defect. Rather than deleting the fallback and keeping a 900-unit sphere placed
through a mapping, the shell became what CONTRACT §8.2 item 13 specifies as the interim path: a
`CosmoSynth/Point` mesh in **sky mode** (`_Space = 2`), whose vertices are ICRF directions with
**no mapping applied at all**. The shader rotates them by the global `_CS_IcrfToWorld` and anchors
them at *this eye* at the declared sky radius.

`SkyAndRings._origin` and `_starSphereRadius` are deleted with it — there is no longer any code
path in that file that could mirror the sky, correctly or otherwise. It also retires the
anchor-parented 900-unit sphere, which is what issue 19 was measuring.

`EmitStar` now emits four coincident vertices per shape element carrying
`(apparentMagnitude, corner, shape, hidden)`; size, brightness, the desaturation of faint stars and
the bright-end halo and spikes are one law in the shader, driven by `_SizeDmm`, `_MagWindow`,
`_Look`, `_Shape` and `_ShapeGain` — set from `StarLook`'s and `SkyAndRings`' existing constants, so
the tuned appearance is carried over rather than re-invented. The two grades the CPU used to
compute (a saturating rank *and* a separate bright-end SmoothStep) collapse back into one magnitude.
`StarLook.MagnitudeFaint`/`MagnitudeBright` name the window that both sides must agree on;
`MagnitudeFromBrightness` inverts it for the procedural sky, which has a rank and no magnitude.

### 9. `ApplySmoothedScale` discards every non-gesture request — FIXED

`_lastReleaseTime = Time.unscaledTime;` at the end of `FrameBodyAtSize` and `FrameSystemAtSize`,
one line each, with the reason. The reviewer's preferred variant (drop the time clause) was not
taken: the clause is what stops a standing target dragging the scale back for the rest of the
session, and the bug was only that non-gesture callers never marked themselves.

### 10. G0b passing proves nothing about the four new rules — FIXED

All four implemented in `stereo_gate.sh` with a mutant each (six mutants, because two rules split):

| rule | mutants |
|---|---|
| R1 — the banned camera helper anywhere under `Runtime/Shaders` | in code; **and in a comment** |
| R30 — `_Fade` in `Properties` **and** `UnityPerMaterial` | each half removed separately |
| R5 — no fragment reads the object/world matrices | the same call moved from `vert` into `frag`, so the rule is proved to be about *where* |
| R12 — `"DisableBatching"="True"` on Volume/Sky/Ribbon | a second fixture named `CosmoSynth/Volume`, tag removed |

R5 strips `//` comments before grepping, and the good fixture carries A.7's own
*"NOT TransformWorldToObject here"* comment inside `frag` as a permanent positive control. The
`Blend One` whole-token fix is kept unchanged. `--self-test`: 16 mutants, all fail as they must.

### 11. No `DisableBatching`, no `EnsureStaticBatchingOff`, no G0c row — FIXED

Tag on the three shaders; `XrSetup.EnsureStaticBatchingOff`; `m_StaticBatching` row in
`settings_gate.py` with a mutant.

**Deviation:** CONTRACT §8.1 specifies `PlayerSettings.SetBatchingForPlatform`. That overload is
not public in Unity 6.0.5 — `error CS0117` — so the setting is written through `SerializedObject`
on `ProjectSettings.asset`'s `m_BuildTargetBatching`, exactly as `EnsureBothInputHandlers` writes
`activeInputHandler`. The YAML it produces is what §11.2's row reads. Note that the list is
**empty** by default and empty means *on*, so the gate treats an absent `m_StaticBatching` as a
failure rather than as a pass.

### 12. `CosmoTheme.FromLinear` is a misnomer that double-decodes — FIXED (re-tune pending at G1d)

Renamed `LinearRgb` and it now says which direction it converts: a physically linear triple in,
gamma-**encoded** out, so that the one decode on the other side — Unity's on a `SetColor` value,
or `CS_DisplayToLinear` on a vertex colour, which §7.4 makes the same thing — returns the original.
`Color.gamma` is unconditional, which is correct because this project is Linear and G0c fails if it
ever is not.

One of the two call sites the review named (the star light) is gone with `BuildStarLight`. The
other (`CelestialBodyView`'s body tint) and `StarLook.Colour`'s black-body hue both go through the
renamed helper.

§7.4's numeric re-tune (`_Look.y`, `_LightColor` intensity, the atmosphere's `_Intensity`) is not
done here: it is a measurement against a capture, and every knob it turns now exists and is named
(`StarLook.StarLightIntensity`, `CosmoTheme.AmbientFill`, `_Look.y`). Doing it before the G1d read
would be tuning against an image nobody has looked at.

### 13. `PlayModeShot`'s `shaders` assertion cannot fail for its own failure — FIXED

Replaced with: every enabled renderer's `sharedMaterials` must have a `shader.name` present in
`CosmoShaders.All`, reported with the offending name and an example hierarchy path. The
`Hidden/InternalError` count is kept as a second line. This one fails on the pre-migration tree and
is the check that covers `CreatePrimitive`'s implicit default, which no grep can see.

The player-side question — that the eleven shader blobs are actually *in* the APK — still needs
`verify-apk.sh` to unzip and confirm, and remains open in the shader lane's own report.

### 14. `CosmoAtmosphere`'s Envelope branch mixes spaces — FIXED

The uncompressed vertex position rides as a second varying (`truePos`) and the ray direction is
taken from it. A direction is invariant under the eye-relative rescaling, so this is the same
vector in both modes — which the previous form was not, because it measured from *this eye* to a
*centre-eye-compressed* point, and compression preserves direction only about the point it is
centred on.

### 15. `UniverseNavigator` implements half of §8.2 item 13 — FIXED

`:144` now wraps with `_simulation.transform.InverseTransformDirection` before `UnmapDirection`;
`:186` wraps `MapDirection`'s result in `TransformDirection` before using it as a world
`LookRotation` axis. Both match `XrLocomotion`, with a comment saying why.

### 16. The `render:` line is missing `ppr` and `vol`; `IsHealthy` misses the depth format — FIXED

`ppr` from `ScaleRenderer.Context.PixelsPerRadian`; `vol` from a new
`ScaleRenderer.VolumeOcclusion`, which counts drawn bodies nearer than an active enclosing volume's
back face (R6) and costs one integer compare per frame while nothing encloses — which is every
frame of this alpha. `IsHealthy` now fails on a 16-bit depth format, and on `vol > 0`.

### 26. `Push` leaves a collapsed descendant's depth extents stale — FIXED

The descendant loop recomputes `RenderedNear/FarMetres` from the node, so one expression covers a
push, a re-push and a first placement. `ScaledSpaceGuardTests.PushedOwnerCarriesItsDescendants` is
the test the review observed was missing: a pushed owner with a collapsed child and a third group
behind them, asserting `RenderedFarMetres == Scale * (d + rLeaf)`. T4/T5 have children and no push;
T6/T7 have a push and no children; this has both.

### 27. `_Space` defaults to the branch that needs an unset uniform — FIXED

Default is 1 (object space). With `_LayerToWorld` gone the specific NaN it named is gone too, but
the principle stands: an unconfigured material must degrade to the branch that needs nothing
configured. The `normalize` guard is there as well.

### 28. `code_gate.sh`'s RenderedRadius rule is satisfied by a comment — FIXED

Comments are stripped before counting (`sed 's://.*::'`), and `IsPointable` counts alongside
`IsGeometryDrawn` — it *is* `IsGeometryDrawn` plus a pick test, and the rule is "did you ask
whether the body is drawn", not "did you type one identifier". Two new self-test cases: the
mutant the review described (a comment naming the guard, no code) must fail, and a file guarded by
`IsPointable` must pass.

---

## Minors

### 17. Descendant `RenderedNear/Far` after a push — FIXED
Same fix as 26.

### 18. Hysteresis reads `previous[i]` by index across a topology rebuild — FIXED
`RebuildTopology` clears a `_historyValid` flag and the next `Solve` is passed `null` for
`previous`, which the method documents as "first frame". One frame without hysteresis, rather than
one frame of arbitrary mode flips at the moment content arrives.

### 19. The `far` assertion's exemption comment exempts nothing — FIXED
`PlayModeShot.IsShaderPlaced` now skips renderers whose material is `CosmoSynth/Sky`, or
`CosmoSynth/Point` with `_Space > 1.5`. The 900-unit shell the review measured at ≈1559 m against
an 1800 m limit no longer exists at all (see 8/25), so the assertion is now measuring only
CPU-placed geometry, which is what it claims.

### 20. Three shaders hold two readings of §7.4 — FIXED
All three now follow §7.4 as written: material Colors set through `SetColor` are linearised by
Unity, so a shader does not decode them again; only **vertex** colours are decoded. `Unlit`'s
`CS_DisplayToLinear` is removed and both arguing comments are replaced by one note, in one place,
pointing at the G1c grey capture that settles whether the rule itself is right. The capture is
still the thing that settles it — but three shaders can no longer disagree about the answer.

### 21. `Material()` can reach `new Material(null)` — FIXED
Returns `null` with a third error line naming both shaders. Every call site now guards and
degrades: the renderer is disabled, the component stays alive, and the log says which shader is
missing.

### 29. `Solve` returns 0 for a short result array — FIXED
Throws `System.ArgumentException`. Zero is the value that means *no conflicts*, and both
`IsHealthy` and the `push` assertion key off exactly that. Tested.

### 30. `FillNodes` drops the parent link silently — FIXED
`ScaleRenderer.AssertParentsPrecedeChildren` runs once per rebuild and logs an error naming both
ids. One pass over a list that is already built.

### 31. `ScaleRenderer`'s subscriptions are made only when `_simulation` was null — FIXED
Hoisted behind a `_subscribed` flag set on the first successful resolve however `_simulation` was
obtained, with named handlers and an `OnDestroy` that unsubscribes them.

### 32. Every catalogue line is `Debug.Log`, including the refusals — FIXED
A line containing `skipped` goes through `Debug.LogWarning`. A refusal is louder than a success —
the Draco/SOHO rule in `CLAUDE.md`, one layer up.

### 33. Dead code shipped in a diff that deletes dead code — FIXED
`ScaleLimits.Contains` and `FrameContext.Log10U` deleted; the `IFloatingOriginListener` cref is
prose in the rewritten `OrbitLineRenderer`.

### 34. `Ring._Radii` duplicates what the mesh carries — FIXED
`u` comes from `TEXCOORD0.x`, which `BuildRing` has always written (0 inner, 1 outer). `_Radii`,
`radiusOS` and the object-XZ plane assumption are all gone together.

### 35. `RenderTransform` lets NaN through — FIXED
`if (!(unitsPerKm > 0.0))` — one character, and it catches NaN by construction. Tested alongside
`ScaleLimits.Clamp(NaN)`.

### 36. Body's Envelope normal and view vector are approximations — FIXED as documented
The comment the review asked for: exact inside d0 = 10 m, which is where the user stands in this
mode, degrading logarithmically toward the horizon, and why recomputing the normal (the Jacobian of
f, per vertex) is not worth it for a term that is zero over the whole near field.

---

## Three defects the new tests and gates found, which the review did not

**`RootIds` was alphabetical, not insertion order** — `CelestialCatalog.Rebuild:152`,
`_rootIds.Sort(StringComparer.Ordinal)`. Issue 22's prescribed test ("base-first ordering fixes
`RootIds[0]`") failed on the tree it was written for, returning `[exo-1, sun]`. Three comments in
this codebase, CONTRACT C10 and `CatalogAssembler`'s entire reason for existing all state that
RootIds is in insertion order; it never was, so `RootIds[0]` was whichever root sorted first
alphabetically — which with any `exo-*` id is an exoplanet, ahead of `sun`. That is the 61 Vir
failure, alive inside the type written to prevent it, and it is still load-bearing at
`UniverseSimulation.cs:254`, `CosmoSynthBootstrap.cs:264` and `UniverseNavigator.cs:105`, all of
which read `RootIds[0]` as the default focus.

Fixed by tracking insertion order explicitly (`_order`) rather than inferring it: a `Dictionary`
enumerates in insertion order only while nothing is removed, and `Remove` exists. The sort on
*children* is kept — nothing claims an order for them and a stable one is worth having.

**`MergeFrom` bypassed it** — and the spacecraft suite caught that within a minute:
`ShippedSpacecraftArePlacedSensibly` reported *"voyager-1 has no frame"*, because `MergeFrom`
writes into `_byId` directly (it deliberately skips `Add`'s duplicate-id throw) and so never
appended to `_order`, and `Rebuild` walks `_order`. Every merged body would have been in the
catalogue and in no reference-frame tree at all. Fixed, with the reason on the line.

**`/tmp/cosmosynth-g0d.cameras` is shared by every worktree on this box.** `code_gate.sh` writes
the camera count there and `settings_gate.py` reads it for the CONTRACT §11.1 cross-check. Six
lane agents run `gate-all.sh` concurrently here, so G0c read another lane's count and reported
*"optimizeBufferDiscards=1 with 2 cameras"* against a tree whose only `AddComponent<Camera>` G0d
had just counted as 1 — a fabricated red, on the phase whose entire subject is the camera count.
Both sides now use the md5-of-root convention `run-tests.sh` already uses for its lock. G0c's
failure count drops from 16 to 15 and every remaining one is a value an editor run writes.

## Two things that changed more than the review asked for, and why

**`OrbitLineRenderer` is a mesh, not a `LineRenderer`.** No shader in the eleven can be driven by a
`LineRenderer`'s vertex stream — `CosmoSynth/Ribbon` reads its segment's other endpoint from
TEXCOORD0, which a `LineRenderer` fills with a UV. So "migrate this call site" and "write the
ribbon mesh" (CONTRACT §8.2 item 17) are the same task. The rewrite *removes* code: the per-frame
world-width calculation, the millimetre cap, the `Gradient` object and `LineAlignment.View` all go,
because the width is now angular and expanded per eye in clip space. The tail gradient is baked
into vertex alpha once, anchored to the array index that `LateUpdate` rotates to the body.

**`StarPointBillboard` no longer billboards.** Same reason: `CosmoSynth/Point` spreads the quad's
corners against *each eye's* projection, and a CPU billboard is one quad built from one camera
transform and shown to both eyes. The component now owns a four-vertex mesh at the body's origin
and passes the shader an angle instead of a world diameter — the `distance × angle` multiplication
that used to happen here, and the division that undid it, are both gone. The magnitude law is
short-circuited with `_MagWindow = (1, 0)` so the CPU keeps owning the decision (it is the side
that knows the distance); the colour rides in the vertex stream rather than on the material, so
§7.4's single decode still holds.

## Gates after the fixes, on `lane/foundation-code`

```
G0a   test suite (floor 183)     PASS   183 passed, 0 failed   (was 174; +6 assembler, +3 solver guards)
G0b   shader stereo macros       PASS   with the four contract rules it was specified to carry
G0c   settings YAML              FAIL   15, every one a value an editor run of XrSetup/CosmoUrpConfig writes
G0d   runtime code invariants    PASS   with two new rules and four new self-test cases
G0e   MonoScript filename/class  PASS
G0f/G0g/G0h                      ABSENT the gates do not exist yet (P0.1 (b))
```

G0c is the P0.4a editor step and nothing here can turn it green: `XrSetup` and `CosmoUrpConfig`
have to run in the one editor and write the YAML. The `static-batching` row this pass adds fails
alongside the other fourteen for the same reason, and passes with them.

`--self-test` counts: `stereo_gate.sh` 16 mutants, `code_gate.sh` 14 (two new mutants and two new
positive controls), `settings_gate.py` 11 mutants plus 5 negative controls against the 1.7.2 tree.
Every one of them fails as it must, including the two added here — and the first version of G0d's
CosmoShaders rule was itself caught by its own mutant, which passed until the grep was fixed.

## What this still cannot prove

- **Nothing here has been compiled by Unity.** `./run-tests.sh` compile-checks Core, Runtime and
  Editor against the script assemblies, and that passes; the eleven shaders have never been
  compiled by the shader compiler. G1b is the first thing that will say.
- **No frame has been rendered.** Every colour decision, the star field's new appearance, the
  ribbon width, the point sizes and §7.4's re-tune are all settled by G1c/G1d captures.
- **Stereo is still only settled on the headset.** That is why R1 and R5 are static bans rather
  than assertions: the play-mode harness is monoscopic and fakes the eyes by moving the camera
  transform, so it cannot distinguish a shader that reads the centre eye from one that reads its
  own.
- **SRP Batcher engagement** for the materials whose CBUFFERs changed needs a Frame Debugger look
  (issue 5's tail).
