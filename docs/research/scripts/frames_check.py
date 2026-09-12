"""Verify the three frame rotations a universe viewer needs against catalogue columns.
ICRS->Galactic (EQ2GAL) against 2MRS RA/Dec vs GLON/GLAT; Galactic->Supergalactic (GAL2SG) against
CF4 GLON/GLAT vs SGL/SGB. Prints the worst angular error in arcsec."""
import gzip, urllib.request
import numpy as np
from cmb_bake import EQ2GAL
def unit(lon, lat):
    lon, lat = np.radians(lon), np.radians(lat)
    return np.stack([np.cos(lat) * np.cos(lon), np.cos(lat) * np.sin(lon), np.sin(lat)], -1)
# Supergalactic: pole at (l,b)=(47.37,+6.32); origin SGL=0 at (l,b)=(137.37,0)  (de Vaucouleurs 1976 / Lahav 2000)
sgz = unit(47.37, 6.32); sgx = unit(137.37, 0.0); sgy = np.cross(sgz, sgx)
GAL2SG = np.stack([sgx, sgy, sgz])
def lines(p):
    return gzip.decompress(urllib.request.urlopen("https://cdsarc.cds.unistra.fr/ftp/" + p, timeout=300).read()).decode().splitlines()
def f(l, a, b):
    return float(l[a - 1:b])
L = lines("J/ApJS/199/26/table3.dat.gz")
eq = unit(np.array([f(l, 18, 26) for l in L]), np.array([f(l, 28, 36) for l in L]))
gal = unit(np.array([f(l, 38, 46) for l in L]), np.array([f(l, 48, 56) for l in L]))
err = np.degrees(np.arccos(np.clip(np.sum((eq @ EQ2GAL.T) * gal, 1), -1, 1))) * 3600
print(f"ICRS->Galactic vs 2MRS ({len(L)} rows): max {err.max():.2f} arcsec, median {np.median(err):.3f}")
L = lines("J/ApJ/944/94/table2.dat.gz")
gal = unit(np.array([f(l, 156, 163) for l in L]), np.array([f(l, 165, 172) for l in L]))
sg = unit(np.array([f(l, 174, 181) for l in L]), np.array([f(l, 183, 190) for l in L]))
err = np.degrees(np.arccos(np.clip(np.sum((gal @ GAL2SG.T) * sg, 1), -1, 1))) * 3600
print(f"Galactic->Supergalactic vs CF4 ({len(L)} rows): max {err.max():.2f} arcsec, median {np.median(err):.3f}")
np.set_printoptions(precision=10, suppress=True)
print("GAL2SG =\n", GAL2SG)
