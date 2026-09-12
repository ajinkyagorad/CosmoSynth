"""Stream a HEALPix-NESTED FITS map over HTTP, degrade it, and bake an equirectangular sky texture.

No healpy, no astropy, no temp file: the FITS BINTABLE is read row-chunk by row-chunk from the
socket, and NESTED ordering makes degrading a reshape + mean (a parent's children are 4^k
consecutive pixels).

  python3 cmb_bake.py URL COLUMN SCALE_TO_uK NSIDE_OUT WIDTH OUTPREFIX
  e.g. ... wmap_ilc_9yr_v5.fits TEMPERATURE 1000 512 2048 /tmp/cs-research/wmap
       ... COM_CMB_IQU-smica-nosz_2048_R3.00_full.fits I_STOKES_INP 1 1024 4096 /tmp/cs-research/smica
       (SCALE 1, not 1e6: that column is in uK although its TUNIT says K_CMB)

Writes OUTPREFIX.f16 (float16 uK, H x W, row 0 = dec +90, column 0 = RA 0, RA increasing with
column), OUTPREFIX.json (sidecar) and OUTPREFIX.png (preview, +-400 uK diverging ramp).
"""
import json, sys, time, urllib.request
import numpy as np
from healpix_nest import ang2pix_nest

# ICRS (J2000) -> Galactic; rows are the Galactic x, y, z axes expressed in ICRS (same matrix
# as the Milky Way Atlas extract_data.py).
EQ2GAL = np.array([[-0.0548755604, -0.8734370902, -0.4838350155],
                   [+0.4941094279, -0.4448296300, +0.7469822445],
                   [-0.8676661490, -0.1980763734, +0.4559837762]])
TFORM_BYTES = {"E": 4, "D": 8, "J": 4, "I": 2, "B": 1, "K": 8}


def read_header(f):
    cards = {}
    while True:
        block = f.read(2880)
        if len(block) < 2880:
            raise SystemExit("stream ended inside a FITS header")
        for i in range(0, 2880, 80):
            c = block[i:i + 80].decode("ascii", "replace")
            key = c[:8].strip()
            if key == "END":
                return cards
            if c[8:10] == "= ":
                v = c[10:].split("/")[0].strip().strip("'").strip()
                cards[key] = v


def main(url, column, scale, nside_out, width, out):
    t0 = time.time()
    f = urllib.request.urlopen(url, timeout=120)
    prim = read_header(f)
    if int(prim.get("NAXIS", "0")) != 0:
        raise SystemExit("primary HDU has data; not a HEALPix BINTABLE file")
    h = read_header(f)
    assert h["XTENSION"] == "BINTABLE" and h["ORDERING"].startswith("NEST"), h
    nside_in, rowlen, nrows = int(h["NSIDE"]), int(h["NAXIS1"]), int(h["NAXIS2"])
    assert nrows == 12 * nside_in ** 2, "cut-sky / multi-element-per-row maps not handled"
    off = 0
    for k in range(1, int(h["TFIELDS"]) + 1):
        form = h[f"TFORM{k}"].strip().lstrip("1")
        if h[f"TTYPE{k}"] == column:
            assert form == "E", form
            break
        off += TFORM_BYTES[form]
    else:
        raise SystemExit(f"no column {column}")
    kids = (nside_in // nside_out) ** 2
    npix_out = 12 * nside_out ** 2
    out_map = np.empty(npix_out, np.float32)
    per = max(1, (1 << 20) // kids) * kids            # rows per chunk, multiple of kids
    done = 0
    rowdt = np.dtype({"names": ["v"], "formats": [">f4"], "offsets": [off], "itemsize": rowlen})
    while done < nrows:
        n = min(per, nrows - done)
        buf = f.read(n * rowlen)
        while len(buf) < n * rowlen:                  # urllib may return short reads
            more = f.read(n * rowlen - len(buf))
            if not more:
                raise SystemExit(f"stream ended at row {done}")
            buf += more
        v = np.frombuffer(buf, rowdt)["v"].astype(np.float32)
        v[v < -1e30] = np.nan                         # HEALPix BAD_DATA sentinel
        out_map[done // kids:(done + n) // kids] = np.nanmean(v.reshape(-1, kids), axis=1) * scale
        done += n
    t1 = time.time()
    # CMB anisotropy rms is ~100 uK at these resolutions. Refuse anything else: TUNIT is not to be
    # trusted (smica-nosz R3.00 I_STOKES_INP says K_CMB and is in uK).
    rms = float(np.nanstd(out_map))
    if not 30 < rms < 300:
        raise SystemExit(f"rms {rms:.3g} uK is not a CMB map: wrong column, or SCALE_TO_uK is wrong")
    # equirectangular in ICRS: texel centres, RA = 0 at column 0 increasing with column
    H = width // 2
    ra = (np.arange(width) + 0.5) / width * 2 * np.pi
    dec = np.pi / 2 - (np.arange(H) + 0.5) / H * np.pi
    RA, DEC = np.meshgrid(ra, dec)
    eq = np.stack([np.cos(DEC) * np.cos(RA), np.cos(DEC) * np.sin(RA), np.sin(DEC)], -1)
    g = eq @ EQ2GAL.T
    tex = out_map[ang2pix_nest(nside_out, g[..., 2], np.arctan2(g[..., 1], g[..., 0]))]
    t2 = time.time()
    tex.astype(np.float16).tofile(out + ".f16")
    finite = np.isfinite(out_map)
    stats = {"source": url, "column": column, "unit": "uK_CMB (dipole and monopole removed)",
             "nside_in": nside_in, "nside_out": nside_out, "width": width, "height": H,
             "frame": "ICRS equirectangular, row 0 = dec +90, column 0 = RA 0, RA increases with column",
             "bad_pixels_after_degrade": int((~finite).sum()),
             "min_uK": float(np.nanmin(out_map)), "max_uK": float(np.nanmax(out_map)),
             "rms_uK": float(np.nanstd(out_map)), "p01_uK": float(np.nanpercentile(out_map, 1)),
             "p99_uK": float(np.nanpercentile(out_map, 99)),
             "seconds_stream": round(t1 - t0, 1), "seconds_project": round(t2 - t1, 1)}
    json.dump(stats, open(out + ".json", "w"), indent=1)
    try:
        from PIL import Image
        x = np.clip(np.nan_to_num(tex) / 400.0, -1, 1)
        rgb = np.stack([np.clip(1 + x, 0, 1) * np.where(x > 0, 1, 1 + x),
                        1 - np.abs(x), np.clip(1 - x, 0, 1) * np.where(x < 0, 1, 1 - x)], -1)
        Image.fromarray((rgb * 255).astype(np.uint8)).save(out + ".png")
    except ImportError:
        pass
    print(json.dumps(stats))


if __name__ == "__main__":
    a = sys.argv[1:]
    main(a[0], a[1], float(a[2]), int(a[3]), int(a[4]), a[5])
