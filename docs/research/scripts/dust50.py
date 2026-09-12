"""Measure what the Vergely+2022 50 pc-resolution cube (20 pc step) costs to ship."""
import sys, zlib
import numpy as np
d = open(sys.argv[1], "rb").read()
off = 2880                                   # header ends at byte 1280 -> one block
a = np.frombuffer(d[off:off + 501 * 501 * 41 * 4], ">f4").reshape(41, 501, 501)   # (z, y, x)
pos = a[a > 0]
print("A0/pc (mag): max %.4g  p50 %.3g  p99 %.3g  p99.9 %.3g  zero-or-neg frac %.3f" %
      (a.max(), np.median(pos), np.percentile(pos, 99), np.percentile(pos, 99.9), float((a <= 0).mean())))
lo, hi = np.percentile(pos, 1), a.max()
q = np.round(np.log(np.clip(a, lo, hi) / lo) / np.log(hi / lo) * 255).astype(np.uint8)
print("uint8 log-encoded full 501x501x41: raw %.2f MB, zlib-9 %.2f MB" % (q.nbytes / 1e6, len(zlib.compress(q.tobytes(), 9)) / 1e6))
h = a[:40, :500, :500].reshape(20, 2, 250, 2, 250, 2).mean(axis=(1, 3, 5))  # 40 pc step
qh = np.round(np.log(np.clip(h, lo, hi) / lo) / np.log(hi / lo) * 255).astype(np.uint8)
print("40 pc step 250x250x20: raw %.2f MB, zlib-9 %.2f MB" % (qh.nbytes / 1e6, len(zlib.compress(qh.tobytes(), 9)) / 1e6))
# integrated extinction toward the Galactic centre from the Sun: sum along +X at the Sun's row
sx, sy, sz = 250, 250, 20
print("A0 Sun->+5 kpc toward GC (x): %.1f mag; toward anticentre: %.1f mag; to NGP (z top): %.3f mag"
      % (a[sz, sy, sx:].sum() * 20, a[sz, sy, :sx].sum() * 20, a[sz:, sy, sx].sum() * 20))
