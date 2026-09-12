#!/usr/bin/env python3
"""The licence switch (design B section 1.5) plus the amendment in docs/DATA-LICENCE-AMENDMENT.md.

    COSMODATA_LICENCE=commercial python3 tools/cosmodata/build_starfield.py

One knob decides which sources an importer may use. Drop means the importer writes nothing
for that source and deletes the shipped file, so dropping a source changes no runtime code:
the manifest simply stops listing it.

Standard library only.
"""

from __future__ import annotations

import os

PROFILES = ("sideload-nc", "public-nc", "commercial")

#: tag -> the profiles that allow shipping it. Rows are docs/design/2026-09-11-B-data.md 1.5.
_ALLOWED = {
    "pd":      ("sideload-nc", "public-nc", "commercial"),   # US government / public domain
    "pd-ack":  ("sideload-nc", "public-nc", "commercial"),   # public, acknowledgement required
    "cc0":     ("sideload-nc", "public-nc", "commercial"),
    "by":      ("sideload-nc", "public-nc", "commercial"),
    "by-sa":   ("sideload-nc", "public-nc", "commercial"),   # the derived file is BY-SA too
    "nc":      ("sideload-nc", "public-nc"),                 # ESA, AAS via VizieR, A&A, SILSO
    "unclear": ("sideload-nc",),                             # no redistribution grant at all
    "own":     ("sideload-nc", "public-nc", "commercial"),
}

#: The stars amendment. docs/licence/stars.md paragraph 1: astronexus grants CC BY-SA 4.0,
#: but astronexus cannot grant commercial rights in ESA's Hipparcos/Gaia astrometry
#: underneath it, and ESA's own terms are CC BY-NC 3.0 IGO. So a `by-sa` tag is not the whole
#: story for HYG and AT-HYG: these two carry a second, stricter upstream tag.
UPSTREAM_OVERRIDE = {
    "athyg": "nc",
    "hyg44": "nc",
}


def profile() -> str:
    value = os.environ.get("COSMODATA_LICENCE", "sideload-nc").strip()
    if value not in PROFILES:
        raise SystemExit(
            f"COSMODATA_LICENCE={value!r} is not one of {', '.join(PROFILES)}")
    return value


def effective_tag(source_id: str, tag: str) -> str:
    """The tag actually binding on a source, after the upstream override."""
    upstream = UPSTREAM_OVERRIDE.get(source_id)
    if upstream is None:
        return tag
    # The stricter of the two wins: fewest profiles allowed.
    return tag if len(_ALLOWED[tag]) <= len(_ALLOWED[upstream]) else upstream


def allows(tag: str, source_id: str = "", current: str = None) -> bool:
    """May a source with this tag ship under the current profile?"""
    if tag not in _ALLOWED:
        raise ValueError(f"unknown licence tag {tag!r}; known: {', '.join(sorted(_ALLOWED))}")
    return (current or profile()) in _ALLOWED[effective_tag(source_id, tag)]


def describe(current: str = None) -> str:
    current = current or profile()
    ok = sorted(t for t in _ALLOWED if current in _ALLOWED[t])
    return f"profile {current}: ships {', '.join(ok)}"


if __name__ == "__main__":
    print(describe())
    for tag in sorted(_ALLOWED):
        print(f"  {tag:8s} {'ship' if allows(tag) else 'DROP'}")
    print("  hyg44   (by-sa, ESA upstream) ->",
          "ship" if allows("by-sa", "hyg44") else "DROP")
