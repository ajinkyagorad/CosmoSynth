#!/bin/bash
# usage: heads.sh urlfile   -> for each URL: final status, size, type, last-modified
while read -r u; do
  [ -z "$u" ] && continue
  case "$u" in \#*) echo "$u"; continue;; esac
  h=$(curl -sIL --max-time 25 -A "Mozilla/5.0 cosmosynth-research" "$u" 2>&1)
  st=$(printf '%s\n' "$h" | grep -i '^HTTP/' | tail -1 | tr -d '\r')
  cl=$(printf '%s\n' "$h" | grep -i '^content-length:' | tail -1 | tr -d '\r' | awk '{print $2}')
  ct=$(printf '%s\n' "$h" | grep -i '^content-type:' | tail -1 | tr -d '\r' | cut -d' ' -f2-)
  lm=$(printf '%s\n' "$h" | grep -i '^last-modified:' | tail -1 | tr -d '\r' | cut -d' ' -f2-)
  printf '%s\n   -> %s | len=%s | %s | %s\n' "$u" "$st" "$cl" "$ct" "$lm"
done < "$1"
