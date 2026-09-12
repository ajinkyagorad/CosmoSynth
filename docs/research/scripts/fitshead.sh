#!/bin/bash
# usage: fitshead.sh URL [bytes]  -> print the FITS header cards of the primary + first extension
u="$1"; n="${2:-23040}"
curl -s --max-time 60 -r 0-$((n-1)) "$u" -o /tmp/cs-research/h.bin
python3 - <<'EOF'
d = open('/tmp/cs-research/h.bin','rb').read()
print('bytes fetched', len(d))
keep = ('SIMPLE','XTENSION','BITPIX','NAXIS','TFIELDS','TTYPE','TFORM','TUNIT','PIXTYPE','ORDERING','NSIDE','COORDSYS','INDXSCHM','FIRSTPIX','LASTPIX','EXTNAME','BAD_DATA','POLCCONV','TELESCOP','METHOD','BEAMSIZE','FWHM','OBJECT','COMMENT','CREATOR','DATE','UNITS','NPIX')
for i in range(0, len(d), 80):
    c = d[i:i+80].decode('ascii','replace')
    k = c[:8].strip()
    if k == 'END':
        print('--- END at byte', i)
        continue
    if any(k.startswith(p) for p in keep) and k != 'COMMENT':
        print(c.rstrip())
EOF
