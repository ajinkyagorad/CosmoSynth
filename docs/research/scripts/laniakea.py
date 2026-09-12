"""Fetch only CF4_new_128-z008_BoA.fits out of the 67 MB watershed zip (range requests), then check
the grid geometry against Dupuy & Courtois 2023: Laniakea = label 1, volume 1.9e6 (Mpc/h)^3,
attractor at SG [-62,-8,39] Mpc/h, 1000 Mpc/h box of 128^3 centred on the observer."""
import struct, urllib.request, zlib
import numpy as np
URL = "https://projets.ip2i.in2p3.fr//cosmicflows/CF4_new_128-z008_watersheds-fits.zip"
WANT = "CF4_new_128-z008_BoA.fits"
def rng(a, b):
    return urllib.request.urlopen(urllib.request.Request(URL, headers={"Range": f"bytes={a}-{b}"}), timeout=120).read()
size = int(urllib.request.urlopen(urllib.request.Request(URL, method="HEAD"), timeout=60).headers["Content-Length"])
tail = rng(size - 65536, size - 1)
i = tail.rfind(b"PK\x05\x06")
n, cds, cdo = struct.unpack("<HII", tail[i + 10:i + 20])
cd = rng(cdo, cdo + cds - 1)
p = 0
for _ in range(n):
    method = struct.unpack("<H", cd[p + 10:p + 12])[0]
    comp, unc, nl, el, cl = struct.unpack("<IIHHH", cd[p + 20:p + 34])
    lho = struct.unpack("<I", cd[p + 42:p + 46])[0]
    name = cd[p + 46:p + 46 + nl].decode()
    if name == WANT:
        break
    p += 46 + nl + el + cl
lh = rng(lho, lho + 29)
data_off = lho + 30 + struct.unpack("<HH", lh[26:30])[0] + struct.unpack("<HH", lh[26:30])[1]
raw = rng(data_off, data_off + comp - 1)
fits = raw if method == 0 else zlib.decompress(raw, -15)
cards, off = {}, 0
while True:
    blk = fits[off:off + 2880].decode("ascii"); off += 2880
    for j in range(0, 2880, 80):
        c = blk[j:j + 80]
        if c[8:10] == "= ":
            cards[c[:8].strip()] = c[10:].split("/")[0].strip().strip("'").strip()
    if "\nEND" in "\n" + "\n".join(blk[j:j + 80] for j in range(0, 2880, 80)):
        break
print({k: cards[k] for k in cards if k in ("BITPIX", "NAXIS", "NAXIS1", "NAXIS2", "NAXIS3", "BUNIT", "CDELT1", "CRVAL1")})
dt = {-64: ">f8", -32: ">f4", 64: ">i8", 32: ">i4", 16: ">i2", 8: "u1"}[int(cards["BITPIX"])]
g = np.frombuffer(fits[off:off + 128 ** 3 * np.dtype(dt).itemsize], dt).reshape(128, 128, 128)
lab = np.rint(g).astype(np.int32)
vox = 1000.0 / 128
print("labels present:", int(lab.max()), "| voxels labelled 1:", int((lab == 1).sum()),
      "| Laniakea volume (Mpc/h)^3: %.3g" % ((lab == 1).sum() * vox ** 3))
def at(sg):  # FITS axis order is (z, y, x) in numpy; x = SGX
    ix = [int((c + 500.0) / vox) for c in sg]
    return int(lab[ix[2], ix[1], ix[0]]), int(lab[ix[0], ix[1], ix[2]])
print("label at attractor SG[-62,-8,39] (x-fastest, z-fastest):", at([-62, -8, 39]))
print("label at observer (box centre):", int(lab[64, 64, 64]), int(lab[63, 63, 63]))
m = (lab == 1).astype(np.uint8)
print("Laniakea mask 128^3 uint8 zlib-9: %d bytes; all labels uint8 zlib-9: %d bytes" %
      (len(zlib.compress(m.tobytes(), 9)), len(zlib.compress(lab.astype(np.uint8).tobytes(), 9))))
print("label at Apus attractor SG[-125,-23,-31] (x-fastest, z-fastest):", at([-125, -23, -31]))
print("label at mirrored Laniakea attractor SG[62,8,-39]:", at([62, 8, -39]))
