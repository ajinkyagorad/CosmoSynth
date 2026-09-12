"""Stream a Planck HEALPix mask from the PLA and report the sky fraction flagged (value 0)."""
import sys, urllib.request
import numpy as np
from cmb_bake import read_header, TFORM_BYTES
for mid in sys.argv[1:]:
    f = urllib.request.urlopen("https://pla.esac.esa.int/pla/aio/product-action?MAP.MAP_ID=" + mid, timeout=120)
    read_header(f); h = read_header(f)
    rowlen, nrows = int(h["NAXIS1"]), int(h["NAXIS2"])
    form = h["TFORM1"].strip().lstrip("1")
    dt = {"E": ">f4", "B": "u1", "D": ">f8", "J": ">i4", "I": ">i2"}[form]
    rowdt = np.dtype({"names": ["v"], "formats": [dt], "offsets": [0], "itemsize": rowlen})
    zero = total = 0
    while total < nrows:
        n = min(1 << 22, nrows - total)
        buf = b""
        while len(buf) < n * rowlen:
            buf += f.read(n * rowlen - len(buf))
        v = np.frombuffer(buf, rowdt)["v"]
        zero += int((v < 0.5).sum()); total += n
    print(f"{mid}: {h.get('TTYPE1')} {form} nside {h.get('NSIDE')} -> masked (0) fraction {zero/total:.4f}, kept {1-zero/total:.4f}")
