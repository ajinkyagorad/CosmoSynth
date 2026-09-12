#!/bin/bash
# usage: cds.sh CAT [CAT...]   e.g. J/ApJS/199/26  -> prints the ReadMe title + file table (name, lrecl, records, explanation)
for cat in "$@"; do
  echo "================ $cat"
  curl -s --max-time 40 "https://cdsarc.cds.unistra.fr/ftp/$cat/ReadMe" -o /tmp/cs-research/readme.txt
  if ! head -c 200 /tmp/cs-research/readme.txt | grep -q .; then echo "   (no ReadMe)"; continue; fi
  head -6 /tmp/cs-research/readme.txt
  # file table section
  awk '/^File Summary:/{f=1} f{print} /^See also:|^Byte-by-byte/{if(f)exit}' /tmp/cs-research/readme.txt | head -30
  # actual sizes from the directory listing
  curl -s --max-time 40 "https://cdsarc.cds.unistra.fr/ftp/$cat/" | grep -o 'href="[^"?/][^"]*"[^<]*</a>[^<]*' | sed 's/<\/a>/ /' | sed 's/href="\([^"]*\)">[^ ]*/\1/' | head -20
done
