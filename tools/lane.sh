#!/usr/bin/env bash
# Development lanes as git WORKTREES, not clones.
#
# Worldfall's rsync lanes "worked for parallelism and failed at integration: lanes edit
# overlapping files and a whole-file copy makes the last one merged the only survivor"
# (HANDOVER.md:245). Worktrees give each lane a branch off a shared base, so `git merge`
# does a real three-way. They are also code-only: no Library, no Unity, ~40 MB instead of
# ~20 GB. Lanes never launch an editor - the integrator does, in main.
#
#   tools/lane.sh add <name>     create ~/projects/CosmoSynth-wt/<name> on lane/<name>
#   tools/lane.sh list           worktrees, branches and each report's Status line
#   tools/lane.sh check <name>   ownership gate (G0h) on master...lane/<name>
#   tools/lane.sh rm <name>      remove, only when the branch is merged into master
#   tools/lane.sh merge <name>   not implemented here - see D §D5 (integrator)
set -uo pipefail
cd "$(dirname "$0")/.." || exit 1
source tools/common.sh

WT=/home/triton/projects/CosmoSynth-wt
LANES=tools/lanes.txt

cmd="${1:-list}"
name="${2:-}"

# The base a new lane branches from: the contracts tag until `green` exists.
base_ref() {
  for r in green phase0-contracts master; do
    git -C "$REPO" rev-parse --verify -q "$r" >/dev/null && { echo "$r"; return; }
  done
  echo master
}

known_lane() {   # lanes.txt is the machine-readable claim list; a lane not in it owns nothing
  [ -f "$LANES" ] || return 0          # no registry yet: allow, and say so
  [ "$(grep -cE "^${1}[[:space:]]" "$LANES" | tr -dc '0-9')" -gt 0 ]
}

case "$cmd" in
  add)
    [ -n "$name" ] || { echo "usage: tools/lane.sh add <name>" >&2; exit 1; }
    if [ ! -f "$LANES" ]; then
      echo "### note: no $LANES yet - not checking the claim list"
    elif ! known_lane "$name"; then
      echo "### REFUSED: '$name' is not a lane in $LANES" >&2; exit 1
    fi
    [ -e "$WT/$name" ] && { echo "### exists: $WT/$name" >&2; exit 1; }
    b=$(base_ref)
    mkdir -p "$WT"
    git -C "$REPO" worktree add "$WT/$name" -b "lane/$name" "$b" || exit 1
    echo
    echo "worktree : $WT/$name"
    echo "branch   : lane/$name (from $b)"
    echo "hand the lane agent D §D7 (standard preamble) plus its card in D §D2.x"
    ;;

  list)
    git -C "$REPO" worktree list
    echo
    printf '%-14s %-10s %s\n' LANE MERGED STATUS
    for d in "$WT"/*; do
      [ -d "$d" ] || continue
      n=$(basename "$d")
      m=no
      [ "$(git -C "$REPO" branch --merged master --list "lane/$n" | grep -c . | tr -dc '0-9')" -gt 0 ] && m=yes
      s=$(sed -n 's/^Status:[[:space:]]*//p' "$d/docs/reports/$n.md" 2>/dev/null | head -1)
      printf '%-14s %-10s %s\n' "$n" "$m" "${s:-<no Status line in docs/reports/$n.md>}"
    done
    ;;

  check)
    [ -n "$name" ] || { echo "usage: tools/lane.sh check <name>" >&2; exit 1; }
    if [ -x tools/gates/own_gate.py ] || [ -f tools/gates/own_gate.py ]; then
      python3 tools/gates/own_gate.py "$name"; exit $?
    fi
    echo "G0h ABSENT: tools/gates/own_gate.py does not exist - ownership is NOT being checked"
    echo "            (D §D3.6; not part of the tooling lane's scope)"
    exit 1
    ;;

  rm)
    [ -n "$name" ] || { echo "usage: tools/lane.sh rm <name>" >&2; exit 1; }
    [ -d "$WT/$name" ] || { echo "### no worktree $WT/$name" >&2; exit 1; }
    if [ "$(git -C "$REPO" branch --merged master --list "lane/$name" | grep -c . | tr -dc '0-9')" -eq 0 ]; then
      echo "### REFUSED: lane/$name is not merged into master - removing it would lose the work" >&2
      exit 1
    fi
    git -C "$REPO" worktree remove "$WT/$name" || exit 1
    git -C "$REPO" branch -d "lane/$name"
    echo "removed $name"
    ;;

  merge)
    echo "Not implemented here. The merge protocol is D §D5 steps 1-9 and belongs to the"
    echo "integrator in main: it runs G0, G1a-c and the ownership gate between merges."
    exit 2
    ;;

  *)
    echo "usage: tools/lane.sh add|list|check|rm|merge <name>" >&2
    exit 1 ;;
esac
