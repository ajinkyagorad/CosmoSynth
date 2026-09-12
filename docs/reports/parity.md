# Lane report: `parity`

Branch `lane/parity` · worktree `~/projects/CosmoSynth-wt/parity` · base `e72d89a` (master)
Status: **READY** · 2026-09-12

This lane wrote no data and no runtime code. It wrote the checklist that says what the owner's
instruction — *"follow that webxr properly, need entire functionality plus more"* — means, and the
gate that stops that checklist drifting.

---

## 1. What I built

| File | Lines | Bytes | What it is |
|---|---|---|---|
| `docs/ATLAS-PARITY.md` | 621 | 95,038 | **240 gated rows.** Every feature of the Milky Way Atlas, the Heliosphere family and the fictional star map, each with a state, an owning lane and the file that does it today; plus 28 `PLUS-nn` rows for what the XR build must do that the web build cannot |
| `tools/gates/parity_gate.py` | 262 | 11,036 | **G0i.** Reads the table, fails on any unaccounted row; `--require-parity` also fails while any row is `absent`; `--self-test` runs a deliberately broken fixture |
| `tools/gate-all.sh` | +1 | — | one `G0i` row in the gate table |
| `tools/gates/known-red.txt` | +14 | — | a comment block, not an entry, explaining why G0i is not excused (§4.1) |

Nothing under `unity/`, `tools/cosmodata/`, `data-staging/` or `SA/` was touched. **Shipped bytes
in the APK: 0.**

## 2. Row counts

| Source | Rows | present | partial | absent | dropped |
|---|---|---|---|---|---|
| Milky Way Atlas | 121 | 11 | 23 | 85 | 2 |
| Heliosphere V2 | 56 | 13 | 21 | 19 | 3 |
| Heliosphere V1 | 6 | 1 | 5 | 0 | 0 |
| SOL-XR Vitals | 13 | 0 | 0 | 10 | 3 |
| Holographic Star Map | 16 | 0 | 4 | 10 | 2 |
| **Web-app parity** | **212** | **25** | **53** | **124** | **10** |
| Plus more | 28 | 3 | 5 | 20 | 0 |
| **Total the gate reads** | **240** | **28** | **58** | **144** | **10** |

The headline for the owner: **of 212 features in his own web pages, 25 are present in the Unity app
today and 124 are absent.** Every data layer the Atlas has past 130 light years — dust, Cepheids,
globulars, galaxies, constellations, landmarks, the grids — is absent. The 13 present rows in the
Heliosphere table are all numerical: Horizons ephemerides, IAU orientation, the floating origin,
real spacecraft with honest windows. The physics is the part that got built.

## 3. What the gate proves, and what it cannot

`tools/gates/parity_gate.py` parses every six-column markdown row whose first cell is an id and
checks eight things. It **proves**:

- no row is silently vague: no unknown state, no lane outside the contract's list, no empty "what it
  does" or "home" cell, no duplicate id;
- no feature is dropped without a written `DROP:` reason, and no non-dropped row is orphaned onto
  `lane none`;
- **no row claims `present` or `partial` without naming a file** — the evidence cell must contain a
  path ending `.cs`, `.py`, `.sh`, `.json`, `.md`, `.txt` or `.shader`, or a path under `SA/`;
- the document was actually read: an empty or reformatted file is red, so "all green" can never mean
  "nothing was parsed".

It **cannot** prove:

- that the named file does what the row says. `FILE_RE` matches a path, not a behaviour. A lane
  could point `MWA-09` at `SurfacePainter.cs` and the gate would pass. The defence is that the
  lane's own report and G1d (a human reads every capture) are where a state change gets justified;
  the gate stops the cheaper failure, which is a row marked done with no file at all;
- that the file still exists or still compiles. It does not stat the path;
- that the row list is complete. If a feature of the Atlas is missing from the table entirely, the
  gate is happy. The list came from two research reports that read the pages in full with line
  numbers, and C-product §10's 225 rows; I split those into 240. A feature nobody wrote down is
  still invisible;
- anything about the runtime. No Unity editor was launched and no APK was built by this lane.

The self-test (`--self-test`) is a committed table with one row per violation class plus one
duplicate id — 13 expected problems — and asserts that **each** is reported, that a clean table
passes, that an empty document is red, and that `report()` returns 0 / 10 / 10 for
(clean, clean + `--require-parity`, broken). It prints `SELF-TEST PASS` or `SELF-TEST FAIL`.
Verified failing before it passed: the first run rejected the clean fixture's dropped row (which has
no home) and mis-expected the problem count; both are fixed and the broken fixture is still red by
construction on every run.

## 4. Deviations from the contract and the task, with reasons

### 4.1 G0i is **not** an entry in `known-red.txt` — it is a comment there

The task said to mark G0i known-red with the phase that clears it. I did not, and the reason is
mechanical: **`tools/build.sh:56-60` refuses to run at all while `known-red.txt` holds any
non-comment line**, and the file's own header scopes it to Phase 0. A real entry would block every
APK for the whole of Phases 1 and 2 — for a gate that is *not failing*.

What I did instead:

- the gate treats `absent` as an accounted-for state, so **G0i is green today** and prints
  `144 rows are absent: accounted for, not built`;
- the strict form is the `--require-parity` flag, which fails while any row is absent — that is the
  machine-readable meaning of "entire functionality", and `gate-all.sh`'s G0i row switches to it at
  the end of **Phase 2**, when 2.0.0 is cut;
- `known-red.txt` carries a comment block saying all of the above, where a reader looking for the
  parity exemption will find it. `grep -cvE '^[[:space:]]*(#|$)'` on that file is still 0, so
  `build.sh` is unaffected.

If the integrator disagrees, the one-line change is to add `G0i` to `known-red.txt` and change the
`gate-all.sh` row's command to `tools/gates/parity_gate.py --require-parity`. The build will then
refuse until parity is complete, which is a policy decision, not a technical one.

### 4.2 `tools/gate-all.sh` and `tools/gates/known-red.txt` are not this lane's files

The contract's lane table assigns them to nobody, so they fall to the integrator. I edited both,
because a gate that is not in the table is not a gate. Both edits are additive and in one place: one
row appended after `G0h` in the `GATES` heredoc, and one comment block appended to `known-red.txt`.
`wire.sh` (not committed) was idempotent; re-running it is a no-op. A three-way merge of either file
should be clean.

### 4.3 `lane.sh add parity`, not a lane in `tools/lanes.txt`

`tools/lanes.txt` does not exist yet (contract P0.1 item (c) creates it), so `lane.sh` printed
`### note: no tools/lanes.txt yet - not checking the claim list` and proceeded. **When the
integrator writes `lanes.txt`, add a `parity` row owning `docs/ATLAS-PARITY.md`,
`tools/gates/parity_gate.py` and `docs/reports/parity.md`**, or `own_gate.py` (G0h) will refuse this
branch.

### 4.4 Ids are C-product's, extended

`MWA-nn` / `HV2-nn` / `HV1-nn` / `VIT-nn` / `HSM-nn` are the same ids as
`docs/design/2026-09-11-C-product.md` §10, so the two tables join. Where C-product lumped several
affordances into one row and the task asked for the small ones individually, I added a letter suffix
(`MWA-09a` … `MWA-09e` for the dust controls; `MWA-25` … `MWA-25e` for the three cylindrical-grid
regimes and their tags). Where the Atlas has an affordance C-product did not list at all, I used the
next free number in the same space: `MWA-50` control legend, `MWA-51` credits, `MWA-52` title,
`MWA-53` additive line policy, `MWA-54` render order, `MWA-55` camera and depth. **C-product §10's
own numbering stops at MWA-48, so 49 is free and 50-55 are mine.** If C-product is revised, take
those six into account.

### 4.5 One row's state is not fully verified

`HV2-38` (JWST, Juno, Parker as live probes) is marked `partial` on the basis that
`SA/catalog/spacecraft.json` holds 24 craft including Juno and SOHO. I did not read the full craft
list. Whoever moves that row should list `spacecraft.json` first. Every other `present` and
`partial` state was checked by reading the file named in its evidence cell or by `grep` over
`unity/Assets/CosmoSynth/`.

### 4.6 The known-red frame bug

`Core/Frames/StandardFrames.cs` sends the north galactic pole to (-0.346, 0.176, 0.922) instead of
(0, 0, 1). That is recorded as the evidence on `MWA-01`, with the branch that fixes it
(`lane/foundation-code`). **This lane wrote no test, so nothing of mine went red because of it and
nothing was added to `tools/gates/known-red.txt` for it.** It is called out here because `MWA-01` is
the row every galactic-scale row in the table stands on: until that matrix is right, nothing placed
in galactic coordinates can be verified against a known answer, and the `stars`, `milkyway` and
`galaxies` lanes should treat it as a hard dependency, not a nuisance.

## 5. Gate and test results in this tree

```
./run-tests.sh            142 passed, 0 failed, 0 skipped  (2043 ms)
tools/gate-all.sh         G0a PASS · G0b FAIL · G0c FAIL · G0d FAIL · G0e FAIL
                          G0f/G0g/G0h ABSENT · G0i PASS      exit 10
tools/gate-all.sh --self-test
                          G0b..G0e OK · G0i OK (SELF-TEST PASS) · G0a SKIP · G0f-h ABSENT
tools/gates/parity_gate.py
                          PASS - 240 rows, 0 unaccounted (absent 144, partial 58, present 28, dropped 10)
tools/gates/parity_gate.py --require-parity
                          FAIL - 144 row(s) still absent of 240
```

**G0b, G0c, G0d and G0e are red on master too** — verified by running `tools/gate-all.sh b c d e` in
`~/projects/CosmoSynth` (exit 10, the same four). They are the pre-migration state the contract's
Phase 0 clears: Built-in RP shaders with no stereo macros, the old settings YAML, the old runtime
invariants. This lane changed nothing they read. The only difference between the two runs is
`xr-duplicates`, which fails on master because of untracked `unity/Assets/XR 1`, `XR 2`, `XR 3`
directories in that working tree and passes in the clean worktree.

`tools/gates/{data,binding,own}_gate.py` are still ABSENT, as P0.1 records.

## 6. What the runtime lanes need to know

1. **Your report moves your rows.** When a lane lands a feature, edit the row in
   `docs/ATLAS-PARITY.md`: set the state and put the file you wrote in the evidence cell. The gate
   rejects `present` or `partial` with no path in it, so there is no way to move a row without
   naming the code. Keep the id.
2. **If you need a row that is not there, add one.** Use the next free number in the right prefix,
   or a letter suffix on the row you are splitting. The gate does not police the id space beyond
   uniqueness and the `PREFIX-nn[x]` shape.
3. **`absent` rows may leave the evidence cell as `-`.** Every other state must say something.
   `dropped` must begin `DROP:` and must be owned by `lane none`.
4. **Read §4 of ATLAS-PARITY.md before sequencing.** The cheap-and-high-value list is not a
   preference: Follow (`PLUS-21`) unblocks every feature that moves under time warp, and the layer
   table (`MWA-33`) is what makes every later layer cost one row instead of one argument. The
   expensive list names the three rows that need a **headset GPU measurement before anyone designs
   around them**: the dust raymarch, the 3D star field under single-pass instancing, and the panel's
   touch constants.
5. **Four Atlas behaviours are defects and must not be ported.** They are recorded in the evidence
   cells of the rows that carry them: the scale clamp at 10^0.5 m/pc that snaps you out of the Solar
   System (`MWA-42`); galaxy position angle rotated in screen space (`MWA-17`); the crosshair fade
   timer started at departure instead of arrival (`MWA-31a`); the cylindrical grids requiring the
   plane-grid toggle (`MWA-25e`). The research report lists 27 in total; these four are the ones a
   lane is most likely to copy by accident because they look like features.
6. **Two label rules conflict and the table takes a side.** The Atlas sizes labels at a fixed 8.5 cm
   linear height; CosmoSynth sizes in dmm, which is angular. `MWA-26` is `partial` on the dmm rule,
   not the 8.5 cm one. Do not "restore parity" by making labels linear.
7. **Colour parity is deliberately not wanted.** `MWA-39` and `HV2-48` are the only two styling
   drops in the Atlas and Heliosphere tables, and both are dropped for the same reason: the pages'
   category colours (green, amber, pink) are already `CosmoTheme.Provenance` Measured, Procedural
   and Fictional. A lane that reproduces them breaks the provenance palette.
8. **`PLUS-25` is the one row that is `present` and must stay that way.** The app never touches the
   network. Three of the reference pages' own URLs are 404 today, which is the argument.
