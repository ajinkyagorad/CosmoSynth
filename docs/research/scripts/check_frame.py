"""Frame check for a baked ICRS equirect CMB texture: is the Galactic plane where it should be?
The ILC/SMICA maps keep residual foreground along b=0, so the texel spread must peak at |b|<~2 deg.
Also samples the texture at the Cold Spot (l=209, b=-57), a well-known ~-100 uK feature."""
import sys, json
import numpy as np
from cmb_bake import EQ2GAL

pre = sys.argv[1]
m = json.load(open(pre + ".json"))
W, H = m["width"], m["height"]
t = np.fromfile(pre + ".f16", np.float16).astype(np.float32).reshape(H, W)
ra = (np.arange(W) + 0.5) / W * 2 * np.pi
dec = np.pi / 2 - (np.arange(H) + 0.5) / H * np.pi
RA, DEC = np.meshgrid(ra, dec)
eq = np.stack([np.cos(DEC) * np.cos(RA), np.cos(DEC) * np.sin(RA), np.sin(DEC)], -1)
g = eq @ EQ2GAL.T
b = np.degrees(np.arcsin(np.clip(g[..., 2], -1, 1)))
w = np.cos(DEC)                                         # equal-area weights
def wstd(sel):
    x, ww = t[sel], w[sel]
    mu = np.average(x, weights=ww)
    return float(np.sqrt(np.average((x - mu) ** 2, weights=ww)))
for lo, hi in ((0, 2), (2, 10), (10, 30), (30, 90)):
    print(f"|b| in [{lo},{hi}) deg: rms {wstd((np.abs(b) >= lo) & (np.abs(b) < hi)):7.1f} uK")
# Cold Spot, l=209 b=-57 (Cruz et al. 2005): mean within 5 deg
l0, b0 = np.radians(209.0), np.radians(-57.0)
v = np.array([np.cos(b0) * np.cos(l0), np.cos(b0) * np.sin(l0), np.sin(b0)])
sel = (g @ v) > np.cos(np.radians(5))
print(f"Cold Spot mean within 5 deg: {float(np.average(t[sel], weights=w[sel])):.1f} uK")
if len(sys.argv) > 2:                                   # correlation with a second bake
    m2 = json.load(open(sys.argv[2] + ".json"))
    t2 = np.fromfile(sys.argv[2] + ".f16", np.float16).astype(np.float32).reshape(m2["height"], m2["width"])
    if t2.shape != t.shape:
        f = t2.shape[0] // t.shape[0]
        t2 = t2.reshape(t.shape[0], f, t.shape[1], f).mean(axis=(1, 3))
    hi = np.abs(b) > 30
    print("corr(|b|>30):", float(np.corrcoef(t[hi], t2[hi])[0, 1]))
