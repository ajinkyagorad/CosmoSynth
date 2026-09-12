"""Fetch the small large-scale-structure catalogues from CDS, parse the columns a viewer needs,
and measure the compact shipped size (float32 xyz Mpc + float16 mag + float16 extra = 16 B)."""
import gzip, math, urllib.request, zlib
import numpy as np
C = 299792.458
H0 = 70.0                      # only used to express cz as a distance for the stats below
CDS = "https://cdsarc.cds.unistra.fr/ftp/"
def get(path):
    raw = urllib.request.urlopen(CDS + path, timeout=300).read()
    return (gzip.decompress(raw) if path.endswith(".gz") else raw).decode("latin-1").splitlines()
def fcol(line, a, b):
    s = line[a - 1:b].strip()
    try:
        return float(s)
    except ValueError:
        return math.nan
def xyz(ra, dec, d):
    ra, dec = np.radians(ra), np.radians(dec)
    return np.stack([d * np.cos(dec) * np.cos(ra), d * np.cos(dec) * np.sin(ra), d * np.sin(dec)], 1)
def pack(p, mag, extra):
    rec = np.zeros(len(p), dtype=[("x", "<f4", 3), ("m", "<f2"), ("e", "<f2")])
    rec["x"], rec["m"], rec["e"] = p, mag, extra
    b = rec.tobytes()
    return len(b), len(zlib.compress(b, 9))
def report(name, ra, dec, d, mag, extra):
    ok = np.isfinite(d) & (d > 0)
    raw, z = pack(xyz(ra[ok], dec[ok], d[ok]), mag[ok], extra[ok])
    print(f"{name}: rows {len(d)}, with distance {ok.sum()}, median d {np.nanmedian(d[ok]):.0f} Mpc, "
          f"p95 {np.nanpercentile(d[ok], 95):.0f} Mpc, max {np.nanmax(d[ok]):.0f} Mpc | 16 B/obj: {raw/1e6:.2f} MB, zlib {z/1e6:.2f} MB")

L = get("J/ApJS/199/26/table3.dat.gz")          # 2MRS
ra = np.array([fcol(l, 18, 26) for l in L]); de = np.array([fcol(l, 28, 36) for l in L])
cz = np.array([fcol(l, 174, 178) for l in L]); k = np.array([fcol(l, 79, 84) for l in L])
report("2MRS (cz/H0, H0=70)", ra, de, cz / H0, k, np.array([fcol(l, 148, 152) for l in L]))

L = get("J/ApJ/944/94/table3.dat.gz")           # CF4 groups
dm = np.array([fcol(l, 9, 14) for l in L])
ra = np.array([fcol(l, 35, 42) for l in L]); de = np.array([fcol(l, 44, 51) for l in L])
report("CF4 groups (DMzp)", ra, de, 10 ** (dm / 5 + 1) / 1e6, dm, np.array([fcol(l, 29, 33) for l in L]))
L = get("J/ApJ/944/94/table2.dat.gz")           # CF4 galaxies
dm = np.array([fcol(l, 29, 34) for l in L])
ra = np.array([fcol(l, 138, 145) for l in L]); de = np.array([fcol(l, 147, 154) for l in L])
report("CF4 galaxies (DM)", ra, de, 10 ** (dm / 5 + 1) / 1e6, dm, np.array([fcol(l, 36, 40) for l in L]))

L = get("VII/259/6dfgs.dat.gz")                 # 6dFGS DR3
ra = np.array([15 * (fcol(l, 18, 19) + fcol(l, 21, 22) / 60 + fcol(l, 24, 28) / 3600) for l in L])
sg = np.array([-1 if l[30] == "-" else 1 for l in L])
de = sg * np.array([fcol(l, 32, 33) + fcol(l, 35, 36) / 60 + fcol(l, 38, 41) / 3600 for l in L])
cz = np.array([fcol(l, 72, 78) for l in L]); q = np.array([fcol(l, 89, 89) for l in L])
good = (q >= 3) & (cz > 300)
report("6dFGS DR3 q>=3 (cz/H0)", ra[good], de[good], cz[good] / H0, np.array([fcol(l, 50, 54) for l in L])[good], q[good])
