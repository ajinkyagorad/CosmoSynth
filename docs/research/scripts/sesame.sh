#!/bin/bash
# Resolve names through CDS Sesame (SIMBAD first, then NED); print name | ra dec (deg) | redshift / velocity if given
while IFS= read -r n; do
  [ -z "$n" ] && continue
  q=$(python3 -c "import urllib.parse,sys;print(urllib.parse.quote(sys.argv[1]))" "$n")
  r=$(curl -s --max-time 30 "https://cds.unistra.fr/cgi-bin/nph-sesame/-ox/SN?$q")
  ra=$(printf '%s' "$r" | grep -o '<jradeg>[^<]*' | head -1 | cut -d'>' -f2)
  de=$(printf '%s' "$r" | grep -o '<jdedeg>[^<]*' | head -1 | cut -d'>' -f2)
  z=$(printf '%s' "$r" | grep -o '<z>[^<]*' | head -1 | cut -d'>' -f2)
  v=$(printf '%s' "$r" | grep -o '<v>[^<]*' | head -1 | cut -d'>' -f2)
  ot=$(printf '%s' "$r" | grep -o '<otype>[^<]*' | head -1 | cut -d'>' -f2)
  oname=$(printf '%s' "$r" | grep -o '<oname>[^<]*' | head -1 | cut -d'>' -f2)
  printf '%-26s | %-22s | ra=%-10s dec=%-10s | z=%s v=%s | %s\n' "$n" "$oname" "$ra" "$de" "$z" "$v" "$ot"
done < "$1"
