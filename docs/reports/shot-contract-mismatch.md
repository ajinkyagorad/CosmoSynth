# tools/shot.sh and PlayModeShot disagree about -shotOut (found 2026-09-12)

Running the new capture tool against the pre-migration tree failed with

    [PlayModeShot] FAILED: System.UnauthorizedAccessException: Access to the path
    /tmp/cosmosynth-shots/2a42de6 is denied.

Not a permissions problem: the directory is owned by triton and writable. The old
PlayModeShot treats -shotOut as a FILE path (default /tmp/cosmosynth-play.png) and the new
tools/shot.sh passes a DIRECTORY, as CONTRACT G1c requires for its seven views. Writing a
file at a directory path is what Linux reports as access denied.

The tool is right and the harness is behind it. PlayModeShot must take -shotOut as an output
directory, honour -shotViews with the seven fixed views, write one PNG and one report per
view, and stamp -shotCommit and -shotDirty into each report. Until it does, G1c cannot run.

Also seen in the same log, and benign: an ArgumentOutOfRangeException from
UnityEditor.Search.SearchDatabase at editor startup. It is Unity indexing its own asset
database and happens before anything of ours runs.

What the run did prove, on the current tree: the real app boots in play mode, loads 235
bodies through three catalogue merges, builds a 9,000-star field of 39,136 vertices, bakes
the measured Earth and Moon surfaces, and re-anchors on Earth. It also printed the line
that matters for the headless harness: "No XR display came up - running flat."
