#!/usr/bin/env bash
# D §D3.3 and §D4 name the G0 runner `tools/gates/g0.sh`; the runner itself lives one
# level up as tools/gate-all.sh, next to the other entry points. This is the name the
# spec uses, so both work and neither is a second implementation.
exec bash "$(dirname "$0")/../gate-all.sh" "$@"
