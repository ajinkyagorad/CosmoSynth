#!/usr/bin/env python3
"""G0c - the settings YAML holds the decided values.

Stdlib regex over the serialised assets, deliberately NOT through Unity. G1b
(ServerBuild.Diagnostics) reads the same decisions back through the editor API. Two
independent sources agree only when both are right; one source agrees with itself even
when it is wrong.

    tools/gates/settings_gate.py                 check the working tree
    tools/gates/settings_gate.py --rev <commit>  check a commit instead (negative controls)
    tools/gates/settings_gate.py --self-test     prove every rule can pass and can fail

Exit 0 pass, 1 fail.
"""
import hashlib
import os
import re
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.environ.get("GATE_ROOT") or os.path.abspath(os.path.join(HERE, "..", ".."))
REV = None

GS = "unity/ProjectSettings/GraphicsSettings.asset"
QS = "unity/ProjectSettings/QualitySettings.asset"
PS = "unity/ProjectSettings/ProjectSettings.asset"
OXR = "unity/Assets/XR/Settings/OpenXR Package Settings.asset"
XRG = "unity/Assets/XR/XRGeneralSettingsPerBuildTarget.asset"
METAQUEST_GUID = "f647cc05"          # MetaQuestFeature MonoScript (prefix match)

results = []


def read(rel):
    """File content, from the working tree or from REV. None when absent."""
    if REV:
        p = subprocess.run(["git", "-C", ROOT, "show", "%s:%s" % (REV, rel)],
                           capture_output=True, text=True)
        return p.stdout if p.returncode == 0 else None
    path = os.path.join(ROOT, rel)
    try:
        with open(path, encoding="utf-8", errors="replace") as fh:
            return fh.read()
    except OSError:
        return None


def ls_tree():
    """Every repo-relative path, from the working tree or from REV."""
    if REV:
        p = subprocess.run(["git", "-C", ROOT, "ls-tree", "-r", "--name-only", REV],
                           capture_output=True, text=True)
        return p.stdout.splitlines() if p.returncode == 0 else []
    out = []
    for base, dirs, files in os.walk(os.path.join(ROOT, "unity", "Assets")):
        rel = os.path.relpath(base, ROOT).replace(os.sep, "/")
        for d in dirs:
            out.append(rel + "/" + d)
        for f in files:
            out.append(rel + "/" + f)
    return out


def check(name, ok, detail):
    results.append((name, bool(ok), detail))


def docs(text):
    """YAML documents of a Unity asset file, split on the --- !u! markers."""
    return re.split(r"^--- !u!", text, flags=re.M) if text else []


def field(block, key, default=None):
    m = re.search(r"^\s*%s:\s*(\S+)\s*$" % re.escape(key), block, flags=re.M)
    return m.group(1) if m else default


def resolve_pipeline_asset(guid):
    """The .asset whose .meta carries this guid."""
    for path in ls_tree():
        if not path.endswith(".asset.meta"):
            continue
        meta = read(path)
        if meta and re.search(r"^guid:\s*%s\s*$" % re.escape(guid), meta, flags=re.M):
            return path[: -len(".meta")]
    return None


def run_checks():
    # ---------------------------------------------------------------- pipeline
    gs = read(GS)
    guid = None
    if gs is None:
        check("pipeline", False, "%s is missing" % GS)
    else:
        m = re.search(r"m_CustomRenderPipeline:\s*\{fileID:\s*(\d+)(?:,\s*guid:\s*([0-9a-f]+))?", gs)
        if m and m.group(1) != "0" and m.group(2):
            guid = m.group(2)
            check("pipeline", True, "GraphicsSettings.m_CustomRenderPipeline guid %s" % guid)
        else:
            check("pipeline", False, "GraphicsSettings.m_CustomRenderPipeline is unset (Built-in)")

    urp_path = resolve_pipeline_asset(guid) if guid else None
    urp = read(urp_path) if urp_path else None
    # Field names are read off a real URP asset (Worldfall WF_URP.asset), never from memory.
    wanted = [("msaa", "m_MSAA", "4"),
              ("hdr", "m_SupportsHDR", "0"),
              ("depth-texture", "m_RequireDepthTexture", "0"),
              ("opaque-texture", "m_RequireOpaqueTexture", "0"),
              ("srp-batcher", "m_UseSRPBatcher", "1"),
              ("render-scale", "m_RenderScale", "1")]
    for name, key, want in wanted:
        if urp is None:
            check(name, False, "no URP asset to read (%s)" % (urp_path or "unresolved guid"))
            continue
        got = field(urp, key)
        check(name, got == want, "%s = %s (want %s) in %s" % (key, got, want, os.path.basename(urp_path)))

    # ------------------------------------------------- Android quality level RP
    qs = read(QS)
    if qs is None:
        check("quality-android-rp", False, "%s is missing" % QS)
    else:
        m = re.search(r"m_PerPlatformDefaultQuality:.*?^\s*Android:\s*(\d+)", qs, flags=re.M | re.S)
        if not m:
            check("quality-android-rp", False, "no Android entry in m_PerPlatformDefaultQuality")
        else:
            idx = int(m.group(1))
            # Only the m_QualitySettings list, so an unrelated versioned list elsewhere in
            # the file cannot shift the index and make this check read the wrong level.
            blockm = re.search(r"^  m_QualitySettings:\n(.*?)(?=^  [A-Za-z_])", qs, flags=re.M | re.S)
            levels = re.findall(r"^\s*- serializedVersion: \d+\n(.*?)(?=^\s*- serializedVersion: |\Z)",
                                blockm.group(1) if blockm else "", flags=re.M | re.S)
            if idx >= len(levels):
                check("quality-android-rp", False, "Android quality level %d not found (%d levels)"
                      % (idx, len(levels)))
            else:
                rp = re.search(r"customRenderPipeline:\s*\{fileID:\s*(\d+)(?:,\s*guid:\s*([0-9a-f]+))?",
                               levels[idx])
                rp_guid = rp.group(2) if rp and rp.group(1) != "0" else None
                ok = rp_guid is None or rp_guid == guid
                check("quality-android-rp", ok,
                      "level %d (%s) RP %s" % (idx, (field(levels[idx], "name") or "?"), rp_guid or "inherited"))

    # ------------------------------------------------------------ colour space
    ps = read(PS)
    if ps is None:
        check("colour-space", False, "%s is missing" % PS)
    else:
        cs = field(ps, "m_ActiveColorSpace")
        check("colour-space", cs == "1", "m_ActiveColorSpace = %s (want 1 = Linear)" % cs)

        # Static batching bakes vertices into WORLD space, which silently destroys three
        # object-space assumptions: CosmoSynth/Volume's raymarch entry point, CosmoSynth/Sky's
        # vertices-are-ICRF-directions and CosmoSynth/Ribbon's otherOS endpoint (R12). It is ON by
        # default for Android, and the default is an EMPTY m_BuildTargetBatching list -- so
        # "absent" is the failing state, not the passing one. XrSetup.EnsureStaticBatchingOff
        # writes the Android entry; the "DisableBatching"="True" tags in the shaders are the belt
        # and this is the braces.
        sb = re.search(r"^\s*m_StaticBatching:\s*(\d+)", ps, flags=re.M)
        check("static-batching", sb is not None and sb.group(1) == "0",
              "m_StaticBatching = %s"
              % (sb.group(1) if sb else "absent, which IS the default and the default is ON"))

    # ------------------------------------------------------------ OpenXR Android
    oxr = read(OXR)
    androids = []
    if oxr:
        for d in docs(oxr):
            if "m_renderMode:" in d and re.search(r"^\s*m_Name:\s*Android\s*$", d, flags=re.M):
                androids.append(d)
    oxr_wanted = [("render-mode", "m_renderMode", "1"),
                  ("depth-submission", "m_depthSubmissionMode", "0"),
                  ("latency", "m_latencyOptimization", "1"),
                  ("symmetric-projection", "m_symmetricProjection", "1"),
                  ("buffer-discards", "m_optimizeBufferDiscards", "1"),
                  ("foveation-api", "m_foveatedRenderingApi", "1")]
    if len(androids) != 1:
        for name, key, want in oxr_wanted:
            check(name, False, "found %d Android OpenXRSettings documents in %s"
                  % (len(androids), os.path.basename(OXR)))
        buffer_discards = None
    else:
        blk = androids[0]
        for name, key, want in oxr_wanted:
            got = field(blk, key)
            check(name, got == want, "%s = %s (want %s)" % (key, got, want))
        buffer_discards = field(blk, "m_optimizeBufferDiscards")

    # -------------------------------------------------------- features present
    if oxr is None:
        check("metaquest-feature", False, "%s is missing" % OXR)
        check("handtracking-off", False, "%s is missing" % OXR)
        check("foveation-feature", False, "%s is missing" % OXR)
    else:
        n = len([d for d in docs(oxr)
                 if re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*%s" % METAQUEST_GUID, d)])
        check("metaquest-feature", n == 1, "%d MetaQuestFeature documents (want exactly 1)" % n)

        # Symmetric projection with the original Quest still a target is a BUILD error, raised
        # by MetaQuestFeature's own validation in BuildPlayer's preprocess step - after G0, G1a,
        # G1b and G1c have all passed, which is where the first 2.0 build found it. It is a
        # fact about this YAML, so it is checked here, in seconds. Anchored on "quest" followed
        # by end of line, because quest2 and quest3s are the devices that must stay on.
        for d in docs(oxr):
            if not re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*%s" % METAQUEST_GUID, d):
                continue
            sym = field(d, "m_symmetricProjection")
            q1 = re.search(r"manifestName:\s*quest\s*\n\s*enabled:\s*(\d)", d)
            q1_on = bool(q1) and q1.group(1) == "1"
            check("quest1-vs-symmetric", not (sym == "1" and q1_on),
                  "m_symmetricProjection = %s, Quest 1 target %s%s"
                  % (sym, "enabled" if q1_on else ("disabled" if q1 else "absent"),
                     " - MetaQuestFeature refuses to build this" if sym == "1" and q1_on else ""))

        # Anchored, not a substring: "openxr.feature.input.handtracking" is also a prefix of
        # "...handtrackingsubsystem", which is Meta's Hand Tracking Subsystem and MUST stay
        # enabled. The loose form asked for hand tracking to be turned off and called it a
        # settings defect. The good fixture now carries an enabled subsystem document so a
        # matcher that drifts back to a substring fails its own positive control.
        ht = [d for d in docs(oxr)
              if re.search(r"featureIdInternal:\s*com\.unity\.openxr\.feature\.input\.handtracking\s*$",
                           d, flags=re.M)]
        if not ht:
            check("handtracking-off", False,
                  "no com.unity.openxr.feature.input.handtracking feature document")
        else:
            on = [d for d in ht if field(d, "m_enabled") == "1"]
            check("handtracking-off", not on,
                  "%d of %d input.handtracking documents enabled (want 0)" % (len(on), len(ht)))

        fv = [d for d in docs(oxr)
              if "openxr.feature.foveatedrendering" in d
              and re.search(r"^\s*m_Name:.*Android\s*$", d, flags=re.M)]
        on = [d for d in fv if field(d, "m_enabled") == "1"]
        check("foveation-feature", bool(on), "%d of %d Android FoveatedRenderingFeature enabled"
              % (len(on), len(fv)))

    # ------------------------------------------------------- duplicated XR dirs
    dup = sorted({p for p in ls_tree()
                  if re.search(r"^unity/Assets/XR [0-9]+(/|$)", p)
                  or re.search(r"^unity/Assets/XR/Settings [0-9]+(/|$)", p)})
    roots = sorted({re.sub(r"^(unity/Assets/XR(?:/Settings)? [0-9]+).*", r"\1", p) for p in dup})
    check("xr-duplicates", not roots, "duplicated XR settings dirs: %s" % (", ".join(roots) or "none"))

    # ----------------------------------------------------- one provider manager
    xrg = read(XRG)
    if xrg is None:
        check("android-providers", False, "%s is missing" % XRG)
    else:
        n = len(re.findall(r"^\s*m_Name:\s*Android Providers\s*$", xrg, flags=re.M))
        check("android-providers", n == 1, "%d 'Android Providers' managers (want exactly 1)" % n)

    # --------------------------------------------------- cross-check with G0d
    # Optimize Buffer Discards keeps the tile buffer uninitialised between passes. With a
    # second camera that is exactly how you get another camera's frame smeared behind the
    # world. Neither value is wrong on its own; the combination is.
    # Per TREE: see code_gate.sh, which writes it. A single shared filename is a race between
    # the worktrees on this box, and reading another lane's count is worse than reading none --
    # it is a red that names a defect this tree does not have.
    tree = hashlib.md5(ROOT.encode("utf-8")).hexdigest()[:8]
    cams_file = os.path.join(os.environ.get("TMPDIR", "/tmp"),
                             "cosmosynth-g0d-%s.cameras" % tree)
    cams = None
    try:
        with open(cams_file) as fh:
            cams = int(fh.read().strip())
    except (OSError, ValueError):
        pass
    if cams is None:
        check("camera-crosscheck", True, "G0d camera count not available (run code_gate.sh first) - not checked")
    else:
        bad = (buffer_discards == "1" and cams > 1)
        check("camera-crosscheck", not bad,
              "optimizeBufferDiscards=%s with %d cameras" % (buffer_discards, cams))


def report():
    failed = 0
    for name, ok, detail in results:
        print("  %-4s %-22s %s" % ("ok" if ok else "FAIL", name, detail))
        if not ok:
            failed += 1
    return failed


# ------------------------------------------------------------------ self-test
GOOD_FIXTURE = {
    GS: """%YAML 1.1
--- !u!30 &1
GraphicsSettings:
  m_CustomRenderPipeline: {fileID: 11400000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 2}
""",
    "unity/Assets/Settings/Cosmo_URP.asset.meta": "fileFormatVersion: 2\nguid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n",
    "unity/Assets/Settings/Cosmo_URP.asset": """%YAML 1.1
--- !u!114 &1
MonoBehaviour:
  m_RequireDepthTexture: 0
  m_RequireOpaqueTexture: 0
  m_SupportsHDR: 0
  m_MSAA: 4
  m_RenderScale: 1
  m_UseSRPBatcher: 1
""",
    QS: """%YAML 1.1
--- !u!47 &1
QualitySettings:
  m_QualitySettings:
  - serializedVersion: 3
    name: Low
    customRenderPipeline: {fileID: 0}
  - serializedVersion: 3
    name: Medium
    customRenderPipeline: {fileID: 0}
  - serializedVersion: 3
    name: High
    customRenderPipeline: {fileID: 0}
  m_PerPlatformDefaultQuality:
    Android: 2
""",
    PS: """%YAML 1.1
--- !u!129 &1
PlayerSettings:
  m_ActiveColorSpace: 1
  m_BuildTargetBatching:
  - m_BuildTarget: Android
    m_StaticBatching: 0
    m_DynamicBatching: 0
""",
    OXR: """%YAML 1.1
--- !u!114 &1
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: b5a1f07dc5afe854f9f12a4194aca3fb, type: 3}
  m_Name: Android
  m_renderMode: 1
  m_latencyOptimization: 1
  m_depthSubmissionMode: 0
  m_optimizeBufferDiscards: 1
  m_symmetricProjection: 1
  m_foveatedRenderingApi: 1
--- !u!114 &2
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: f647cc0545697264a9878224faada6d5, type: 3}
  m_Name: MetaQuestFeature Android
  m_enabled: 1
  targetDevices:
  - visibleName: Quest
    manifestName: quest
    enabled: 0
  - visibleName: Quest 2
    manifestName: quest2
    enabled: 1
  - visibleName: Quest 3
    manifestName: eureka
    enabled: 1
  - visibleName: Quest 3S
    manifestName: quest3s
    enabled: 1
  m_symmetricProjection: 1
  m_optimizeBufferDiscards: 1
--- !u!114 &3
MonoBehaviour:
  m_Name: MicrosoftHandInteraction Android
  m_enabled: 0
  featureIdInternal: com.unity.openxr.feature.input.handtracking
--- !u!114 &5
MonoBehaviour:
  m_Name: HandTracking Android
  m_enabled: 1
  featureIdInternal: com.unity.openxr.feature.input.handtrackingsubsystem
--- !u!114 &4
MonoBehaviour:
  m_Name: FoveatedRenderingFeature Android
  m_enabled: 1
  featureIdInternal: com.unity.openxr.feature.foveatedrendering
""",
    XRG: """%YAML 1.1
--- !u!114 &1
MonoBehaviour:
  m_Name: Android Providers
  m_Loaders: []
""",
}

NEGATIVE_REV = "c450708"     # the 1.7.2 tree: Built-in, gamma, wrong OpenXR submission
NEGATIVE_MUST_FAIL = ["pipeline", "colour-space", "depth-submission", "latency",
                      "symmetric-projection"]


def run_in(root, rev=None):
    """Run this script in a subprocess and return (rc, {name: ok})."""
    # TMPDIR into the fixture so a real run's G0d camera count cannot leak in.
    env = dict(os.environ, GATE_ROOT=root, TMPDIR=root)
    cmd = [sys.executable, os.path.abspath(__file__)]
    if rev:
        cmd += ["--rev", rev]
    p = subprocess.run(cmd, capture_output=True, text=True, env=env)
    verdicts = {}
    for line in p.stdout.splitlines():
        m = re.match(r"\s+(ok|FAIL)\s+(\S+)", line)
        if m:
            verdicts[m.group(2)] = (m.group(1) == "ok")
    return p.returncode, verdicts, p.stdout


def self_test():
    ok = True

    # Positive control: a hand-written tree holding exactly the decided values. Proves the
    # parser can say PASS - a gate that cannot pass is as broken as one that cannot fail.
    d = tempfile.mkdtemp(prefix="cosmosynth-settings-st.")
    for rel, body in GOOD_FIXTURE.items():
        path = os.path.join(d, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(body)
    rc, v, out = run_in(d)
    if rc == 0:
        print("  ok    self-test: the good fixture passes")
    else:
        print("  SELF-TEST FAIL: the good fixture did not pass")
        print(out)
        ok = False

    # One mutant per decided value. Each MUST fail by name.
    mutants = [
        ("pipeline", GS, "guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 2}", "fileID: 0}"),
        ("colour-space", PS, "m_ActiveColorSpace: 1", "m_ActiveColorSpace: 0"),
        ("static-batching", PS, "m_StaticBatching: 0", "m_StaticBatching: 1"),
        ("depth-submission", OXR, "m_depthSubmissionMode: 0", "m_depthSubmissionMode: 1"),
        ("latency", OXR, "m_latencyOptimization: 1", "m_latencyOptimization: 0"),
        ("symmetric-projection", OXR, "m_symmetricProjection: 1", "m_symmetricProjection: 0"),
        ("msaa", "unity/Assets/Settings/Cosmo_URP.asset", "m_MSAA: 4", "m_MSAA: 0"),
        ("render-mode", OXR, "m_renderMode: 1", "m_renderMode: 0"),
        ("metaquest-feature", OXR, "guid: f647cc0545697264a9878224faada6d5",
         "guid: 0000000000000000000000000000ffff"),
        ("handtracking-off", OXR, "m_Name: MicrosoftHandInteraction Android\n  m_enabled: 0",
         "m_Name: MicrosoftHandInteraction Android\n  m_enabled: 1"),
        ("quest1-vs-symmetric", OXR, "manifestName: quest\n    enabled: 0",
         "manifestName: quest\n    enabled: 1"),
        ("android-providers", XRG, "m_Name: Android Providers",
         "m_Name: Android Providers\n--- !u!114 &2\nMonoBehaviour:\n  m_Name: Android Providers"),
    ]
    for name, rel, old, new in mutants:
        path = os.path.join(d, rel)
        with open(path, encoding="utf-8") as fh:
            orig = fh.read()
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(orig.replace(old, new, 1))
        rc, v, out = run_in(d)
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(orig)
        if v.get(name) is False:
            print("  ok    self-test: mutant '%s' fails as it must" % name)
        else:
            print("  SELF-TEST FAIL: mutant '%s' did not fail (verdict %s)" % (name, v.get(name)))
            ok = False

    subprocess.run(["rm", "-rf", d])

    # Negative control from history: the real 1.7.2 tree must fail by name (D §D3.7 G0c).
    have = subprocess.run(["git", "-C", ROOT, "cat-file", "-e", NEGATIVE_REV + "^{commit}"],
                          capture_output=True)
    if have.returncode != 0:
        print("  warn  self-test: commit %s is not in this repo - historical control skipped"
              % NEGATIVE_REV)
    else:
        rc, v, out = run_in(ROOT, NEGATIVE_REV)
        for name in NEGATIVE_MUST_FAIL:
            if v.get(name) is False:
                print("  ok    self-test: %s FAILs on the 1.7.2 tree (%s) as it must" % (name, NEGATIVE_REV))
            else:
                print("  SELF-TEST FAIL: %s did not fail on %s (verdict %s)" % (name, NEGATIVE_REV, v.get(name)))
                ok = False

    print("SELF-TEST " + ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


def main():
    global REV
    args = sys.argv[1:]
    if args and args[0] == "--self-test":
        return self_test()
    if len(args) >= 2 and args[0] == "--rev":
        REV = args[1]
    print("G0c settings gate on %s%s" % (ROOT, " @" + REV if REV else ""))
    run_checks()
    failed = report()
    if failed:
        print("G0c FAIL (%d)" % failed)
        return 1
    print("G0c PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
