#!/usr/bin/env python3
"""Bake the cosmic microwave background into cmb/cmb_sky.cscb (CSCB).

    python3 tools/cosmodata/build_cmb.py                  # from the staged WMAP cube
    python3 tools/cosmodata/build_cmb.py --in map.fits    # from a HEALPix NESTED FITS
    python3 tools/cosmodata/build_cmb.py --face 2048      # bigger, no code change
    python3 tools/cosmodata/build_cmb.py --selftest       # synthetic known-answer run

Ships a 6 x F^2 R8 cube of the anisotropy in ICRF face components (CONTRACT Appendix C.12)
plus a 6 x 256^2 galactic-foreground mask. The monopole and the dipole are NOT in the
texture: they are analytic constants, here in the meta block and in Core/Cmb/CmbModel.cs, so
the app can add either one in the shader and label which mode it is in.

numpy is the one declared exception for this importer (CONTRACT 6.2.2). Everything else is
stdlib, including the PNG reader below -- Pillow is not a permitted dependency. The file is
written to a temporary name and os.replace'd only after every check passes; on any failure
both the temporary file and the stale target are deleted.

SOURCE. NASA LAMBDA, WMAP nine-year (DR5) Internal Linear Combination map plus the KQ85
temperature analysis mask, both CC0 (a NASA-led mission, no restrictive notice in either
FITS header). Section B named Planck PR3 SMICA-noSZ; the licence amendment kills it -- ESA
Space Science Archive data is CC BY-NC 3.0 IGO and the IRSA mirror does not launder it.
**Nothing in this pipeline or in the app may say "Planck" about the map.** The cosmological
parameters are a different matter: those are published numbers in a paper, not archive data.

The HEALPix -> cube projection was run on 2026-09-12 by the research script
data-staging/wmap9-ilc-lambda/cmb_cube.py (which uses healpix_nest.py, copied here beside
this file) and its float16 output is the default input, because the raw 25 MB FITS was
deleted after that bake and triton was below the 6 GB free-disk floor for re-fetching. The
--in FITS branch is that same projection and is exercised by --selftest on a synthetic map
whose answer is known.
"""
import argparse
import hashlib
import json
import os
import struct
import subprocess
import sys
import time
import zlib

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from healpix_nest import ang2pix_nest, pix2ang_nest  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def _staging():
    """data-staging/ is untracked, so it exists only in the main checkout, not in a lane
    worktree. Look there first, then fall back to the main tree beside CosmoSynth-wt/."""
    here = os.path.join(REPO, "data-staging")
    if os.path.isdir(here):
        return here
    main = os.path.join(os.path.dirname(os.path.dirname(REPO)), "CosmoSynth", "data-staging")
    return main if os.path.isdir(main) else here


STAGING = os.path.join(_staging(), "wmap9-ilc-lambda")
DEFAULT_IN = os.path.join(STAGING, "cmb_cube_1024.f16")
DEFAULT_MASK = os.path.join(STAGING, "mask_kq85_equirect_2048x1024.png")
DEFAULT_OUT = os.path.join(REPO, "unity", "Assets", "StreamingAssets", "CosmoSynth",
                           "cmb", "cmb_sky.cscb")

# ---- the encoding the container ships (CONTRACT C.12) -----------------------
LO, HI = -500.0, 500.0
SCALE = (HI - LO) / 255.0                      # 3.9215686274509802 uK per step
MASK_FACE = 256

# ICRS (J2000) -> Galactic. Hipparcos, ESA SP-1200 Vol 1 s1.5.3. Rows are the galactic x, y
# and z axes in ICRS components. That it sends the published north galactic pole to (0, 0, 1)
# is asserted at the top of build(), not taken on trust.
EQ2GAL = np.array([[-0.0548755604, -0.8734370902, -0.4838350155],
                   [+0.4941094279, -0.4448296300, +0.7469822445],
                   [-0.8676661490, -0.1980763734, +0.4559837762]])

# Published sky positions used ONLY by the checks, never by the bake, so a check cannot
# agree with the bake merely by sharing its rotation.
NGP_RADEC = (192.85948, 27.12825)              # north galactic pole, ICRS J2000
GC_RADEC = (266.40510, -28.93617)              # galactic centre, ICRS J2000
# The CMB Cold Spot. Cruz et al. 2005 give (l, b) = (209, -57), quoted equatorially as
# RA 03h15m20s, Dec -19d35m. The two disagree by about 1 degree because the galactic pair is
# rounded to whole degrees; both are measured below and 5 degrees is far wider than the gap.
COLDSPOT_RADEC = (48.83, -19.58)
COLDSPOT_LB = (209.0, -57.0)

FACES = ["+x", "-x", "+y", "-y", "+z", "-z"]


def unit(lon_deg, lat_deg):
    a, d = np.radians(lon_deg), np.radians(lat_deg)
    return np.array([np.cos(d) * np.cos(a), np.cos(d) * np.sin(a), np.sin(d)])


def face_dirs(face, side):
    """Unit ICRF directions of every texel centre on one cube face.

    CONTRACT C.12 exactly: u = 2s - 1 across the face, v = 2t - 1 down it, texel (i, j) at
    s = (i + 0.5)/F and t = (j + 0.5)/F, row j = 0 at t = 0. Axis 0 is j, axis 1 is i.
    """
    t = (np.arange(side) + 0.5) / side * 2.0 - 1.0
    u, v = np.meshgrid(t, t)
    o = np.ones_like(u)
    d = {"+x": (o, -v, -u), "-x": (-o, -v, u),
         "+y": (u, o, v), "-y": (u, -o, -v),
         "+z": (u, -v, o), "-z": (-u, -v, -o)}[face]
    d = np.stack(d, -1)
    return d / np.linalg.norm(d, axis=-1, keepdims=True)


def solid_angle_weights(side):
    """Relative solid angle of each texel on a face, so sky means are not face-centre heavy."""
    t = (np.arange(side) + 0.5) / side * 2.0 - 1.0
    u, v = np.meshgrid(t, t)
    return (1.0 + u * u + v * v) ** -1.5


# ---- input: a baked float16 cube, or a HEALPix NESTED FITS ------------------

def read_fits_header(f):
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
                cards[key] = c[10:].split("/")[0].strip().strip("'").strip()


def read_healpix_column(path, column):
    """(map in native units, nside). NESTED, big-endian f32, one element per row."""
    tform_bytes = {"E": 4, "D": 8, "J": 4, "I": 2, "B": 1, "K": 8}
    with open(path, "rb") as f:
        prim = read_fits_header(f)
        if int(prim.get("NAXIS", "0")) != 0:
            raise SystemExit("primary HDU has data; not a HEALPix BINTABLE file")
        h = read_fits_header(f)
        if h.get("XTENSION") != "BINTABLE" or not h.get("ORDERING", "").startswith("NEST"):
            raise SystemExit("expected a NESTED HEALPix BINTABLE, got %r" % h.get("ORDERING"))
        nside, rowlen, nrows = int(h["NSIDE"]), int(h["NAXIS1"]), int(h["NAXIS2"])
        if nrows != 12 * nside ** 2:
            raise SystemExit("cut-sky or multi-element maps are not handled")
        off = 0
        for k in range(1, int(h["TFIELDS"]) + 1):
            form = h["TFORM%d" % k].strip().lstrip("1")
            if h["TTYPE%d" % k] == column:
                if form != "E":
                    raise SystemExit("column %s is TFORM %s, expected E" % (column, form))
                break
            off += tform_bytes[form]
        else:
            raise SystemExit("no column %s in %s" % (column, path))
        dt = np.dtype({"names": ["v"], "formats": [">f4"], "offsets": [off], "itemsize": rowlen})
        m = np.frombuffer(f.read(nrows * rowlen), dt)["v"].astype(np.float32).copy()
    m[m < -1e30] = np.nan                      # the HEALPix BAD_DATA sentinel
    return m, nside


def project_healpix(m, nside, side):
    """Nearest-neighbour lookup of a galactic HEALPix map along ICRF cube-texel directions."""
    out = np.empty((6, side, side), np.float32)
    for k, f in enumerate(FACES):
        g = face_dirs(f, side) @ EQ2GAL.T
        out[k] = m[ang2pix_nest(nside, g[..., 2], np.arctan2(g[..., 1], g[..., 0]))]
    return out


def load_cube(path, side, column, scale_to_uk):
    """(6, side, side) float32 microkelvin, faces in the order FACES."""
    if path.endswith(".f16"):
        raw = np.fromfile(path, np.float16)
        f = int(round((raw.size / 6.0) ** 0.5))
        if raw.size != 6 * f * f:
            raise SystemExit("%s is %d float16, not six square faces" % (path, raw.size))
        if f != side:
            raise SystemExit("%s holds %d^2 faces but --face is %d; pass --face %d"
                             % (path, f, side, f))
        return raw.reshape(6, f, f).astype(np.float32), "staged float16 cube, %d^2 faces" % f

    m, nside = read_healpix_column(path, column)
    nside_out = min(nside, side)
    if nside_out < nside:                      # NESTED: a degrade is a reshape
        m = np.nanmean(m.reshape(-1, (nside // nside_out) ** 2), axis=1)
    return (project_healpix(m * scale_to_uk, nside_out, side),
            "HEALPix NESTED nside %d -> %d, nearest" % (nside, nside_out))


# ---- stdlib PNG reader (8-bit greyscale, non-interlaced) --------------------

def read_png_gray8(path):
    raw = open(path, "rb").read()
    if raw[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit("%s is not a PNG" % path)
    pos, idat, w, h = 8, bytearray(), 0, 0
    while pos + 8 <= len(raw):
        n = struct.unpack(">I", raw[pos:pos + 4])[0]
        typ = raw[pos + 4:pos + 8]
        body = raw[pos + 8:pos + 8 + n]
        pos += 12 + n
        if typ == b"IHDR":
            w, h, depth, colour, _, _, interlace = struct.unpack(">IIBBBBB", body)
            if (depth, colour, interlace) != (8, 0, 0):
                raise SystemExit("expected 8-bit non-interlaced greyscale, got "
                                 "depth %d colour %d interlace %d" % (depth, colour, interlace))
        elif typ == b"IDAT":
            idat += body
        elif typ == b"IEND":
            break
    data = zlib.decompress(bytes(idat))
    if len(data) != h * (w + 1):
        raise SystemExit("PNG %s: %d bytes of scanline, expected %d" % (path, len(data), h * (w + 1)))
    out = np.empty((h, w), np.uint8)
    prev = np.zeros(w, np.int32)
    for j in range(h):
        at = j * (w + 1)
        ft = data[at]
        cur = np.frombuffer(data, np.uint8, w, at + 1).astype(np.int32)
        if ft == 0:
            pass
        elif ft == 2:
            cur = (cur + prev) & 255
        elif ft == 1:
            cur = cur.copy()
            for i in range(1, w):
                cur[i] = (cur[i] + cur[i - 1]) & 255
        elif ft == 3:
            cur = cur.copy()
            for i in range(w):
                cur[i] = (cur[i] + (((cur[i - 1] if i else 0) + prev[i]) >> 1)) & 255
        elif ft == 4:
            cur = cur.copy()
            for i in range(w):
                a, b, c = (cur[i - 1] if i else 0), prev[i], (prev[i - 1] if i else 0)
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                cur[i] = (cur[i] + pr) & 255
        else:
            raise SystemExit("PNG filter type %d on row %d" % (ft, j))
        out[j] = cur.astype(np.uint8)
        prev = cur
    return out


def load_mask_cube(path, side):
    """(6, side, side) uint8: 1 = rejected by KQ85 (galactic foreground), 0 = kept."""
    a = read_png_gray8(path)
    h, w = a.shape
    keep = a > 127                             # the staged PNG is 255 = keep, 0 = reject
    out = np.empty((6, side, side), np.uint8)
    for k, f in enumerate(FACES):
        d = face_dirs(f, side)
        ra = np.arctan2(d[..., 1], d[..., 0]) % (2 * np.pi)
        dec = np.arcsin(np.clip(d[..., 2], -1, 1))
        j = np.clip(((np.pi / 2 - dec) / np.pi * h).astype(int), 0, h - 1)
        i = np.clip((ra / (2 * np.pi) * w).astype(int), 0, w - 1)
        out[k] = (~keep[j, i]).astype(np.uint8)
    return out


def sample_cube(cube, d):
    """Nearest-texel lookup of a cube at arbitrary directions; d is (..., 3)."""
    side = cube.shape[1]
    flat = d.reshape(-1, 3)
    ax = np.argmax(np.abs(flat), axis=1)
    mv = np.take_along_axis(flat, ax[:, None], 1)[:, 0]
    fi = ax * 2 + (mv <= 0)
    ma = np.abs(mv)
    x, y, z = flat[:, 0], flat[:, 1], flat[:, 2]
    u = np.select([fi == 0, fi == 1, fi == 2, fi == 3, fi == 4], [-z, z, x, x, x], -x)
    v = np.select([fi == 0, fi == 1, fi == 2, fi == 3, fi == 4], [-y, -y, z, -z, -y], -y)
    i = np.clip(((u / ma + 1) / 2 * side).astype(int), 0, side - 1)
    j = np.clip(((v / ma + 1) / 2 * side).astype(int), 0, side - 1)
    return cube[fi, j, i].reshape(d.shape[:-1])


# ---- checks ----------------------------------------------------------------

def run_checks(t, mask, res, equirect=None):
    """Every number the shipped file claims, measured here. res is filled in place."""
    side = t.shape[1]
    w = np.repeat(solid_angle_weights(side)[None], 6, 0)
    dirs = np.stack([face_dirs(f, side) for f in FACES])

    # Range. TUNIT is never trusted; these are the numbers that identify a CMB map.
    res["rms_uK"] = float(np.sqrt(np.average(t ** 2, weights=w)))
    res["mean_uK"] = float(np.average(t, weights=w))
    res["min_uK"], res["max_uK"] = float(t.min()), float(t.max())
    res["p01_uK"], res["p99_uK"] = [float(x) for x in np.percentile(t, [1, 99])]
    res["nan_count"] = int(np.isnan(t).sum())
    res["check_rms_is_cmb"] = bool(30.0 < res["rms_uK"] < 300.0)
    res["check_mean_small"] = bool(abs(res["mean_uK"]) < 10.0)
    res["check_percentiles"] = bool(abs(res["p01_uK"]) < 400.0 and abs(res["p99_uK"]) < 400.0)
    res["check_no_nan"] = res["nan_count"] == 0

    # ORIENTATION 1. The galactic plane. b comes from the PUBLISHED pole direction, not from
    # EQ2GAL, so this cannot agree with the bake by sharing its rotation. Even a hard-cleaned
    # ILC keeps its largest residual on the plane.
    ngp = unit(*NGP_RADEC)
    b = np.degrees(np.arcsin(np.clip(dirs @ ngp, -1, 1)))
    edges = np.arange(0, 91, 2.0)
    idx = np.clip(np.digitize(np.abs(b), edges) - 1, 0, len(edges) - 2)
    prof = (np.bincount(idx.ravel(), (np.abs(t) * w).ravel(), len(edges) - 1) /
            np.bincount(idx.ravel(), w.ravel(), len(edges) - 1))
    peak = float(edges[int(np.argmax(prof))])
    res["absT_by_absb"] = {"%g-%g" % (edges[i], edges[i + 1]): round(float(prof[i]), 2)
                           for i in range(len(prof))}
    res["absT_peak_bin_deg"] = [peak, peak + 2.0]
    inner_sel, outer_sel = np.abs(b) < 2, np.abs(b) > 30
    inner = float(np.sqrt(np.average(t[inner_sel] ** 2, weights=w[inner_sel])))
    outer = float(np.sqrt(np.average(t[outer_sel] ** 2, weights=w[outer_sel])))
    res["rms_b_under_2_uK"], res["rms_b_over_30_uK"] = round(inner, 2), round(outer, 2)
    res["plane_rms_ratio"] = round(inner / outer, 4)
    res["check_plane_peaks_on_plane"] = bool(peak < 2.0 and inner > 1.05 * outer)

    # ORIENTATION 2. A known feature at its published position.
    def disc_mean(c, radius_deg):
        sel = (dirs @ c) > np.cos(np.radians(radius_deg))
        return float(np.average(t[sel], weights=w[sel])), int(sel.sum())

    cold, n = disc_mean(unit(*COLDSPOT_RADEC), 5.0)
    cold_lb, _ = disc_mean(EQ2GAL.T @ unit(*COLDSPOT_LB), 5.0)
    res["coldspot_5deg_mean_uK"] = round(cold, 2)
    res["coldspot_5deg_texels"] = n
    res["coldspot_from_lb_5deg_mean_uK"] = round(cold_lb, 2)
    # A mis-rotated map reads 0 +- 20 uK here (CONTRACT C.12). WMAP's 1 degree beam plus this
    # resample give about -107 +- 6; the bar is set well inside that, not at it.
    res["check_coldspot"] = bool(cold < -60.0)
    # Informational: the ILC is cleaned hard enough that a 5 degree disc on the galactic centre
    # barely beats a polar one, so this is not a second vote on the plane.
    res["info_gc_5deg_mean_uK"] = round(disc_mean(unit(*GC_RADEC), 5.0)[0], 2)

    # ORIENTATION 3a. Agreement with an independent product. The KQ85 analysis mask is a
    # different FITS file, built by the WMAP team from K-band, H-alpha and dust foreground
    # TEMPLATES, not from the ILC map this cube holds. If the cube is correctly oriented, the
    # sky the mask rejects must be the sky where the ILC residual is large. The control
    # resamples the mask along directions turned 90 degrees about the ICRS pole.
    #
    # Bars set from the measurement, not from taste. Measured on the shipped cube: real
    # 1.172, z90 control 0.923, x90 0.986, y90 1.001 -- so the null is 0.92 to 1.00 and the
    # effect is 17%. It is only 17% because the ILC is cleaned hard; the decisive plane test
    # is ORIENTATION 1, and this one adds a SECOND FILE agreeing about where the galaxy is.
    # (A point reflection is not a usable control: the galactic plane maps to itself, and it
    # duly scores 1.155.)
    def rms_where(sel):
        return float(np.sqrt(np.average(t[sel] ** 2, weights=w[sel])))

    def mask_ratio(mk):
        if not (mk == 1).any() or not (mk == 0).any():
            return 1.0                         # an empty or full mask says nothing
        return rms_where(mk == 1) / rms_where(mk == 0)

    rot = np.stack([-dirs[..., 1], dirs[..., 0], dirs[..., 2]], -1)
    res["mask_rms_ratio"] = round(mask_ratio(mask), 4)
    res["mask_rms_ratio_rotated_control"] = round(mask_ratio(sample_cube(mask, rot)), 4)
    res["mask_rejected_fraction"] = round(float(np.average(mask, weights=w)), 4)
    res["check_mask_agrees"] = bool(
        res["mask_rms_ratio"] > 1.10 and
        res["mask_rms_ratio"] > 1.15 * res["mask_rms_ratio_rotated_control"])

    # ORIENTATION 3b. The same sky through an independently written projection. The staged
    # equirectangular bake came out of cmb_bake.py, a separate script with its own raster and
    # its own rotation application; this cube came out of cmb_cube.py. Agreement is evidence
    # about the code, not about the data -- but it is sharp: measured 0.99649, a scrambled
    # face order scores -0.0099, and a misorientation of only ONE DEGREE drops it to 0.754.
    # Skipped when the importer is run from a FITS with no equirect beside it.
    if equirect and os.path.exists(equirect):
        H, W = 2048, 4096
        tex = np.fromfile(equirect, np.float16).reshape(H, W).astype(np.float32)
        rng = np.random.default_rng(7)
        n = 200000
        z = rng.uniform(-1, 1, n)
        phi = rng.uniform(0, 2 * np.pi, n)
        d = np.stack([np.sqrt(1 - z * z) * np.cos(phi),
                      np.sqrt(1 - z * z) * np.sin(phi), z], -1)
        ra = np.arctan2(d[:, 1], d[:, 0]) % (2 * np.pi)
        dec = np.arcsin(np.clip(d[:, 2], -1, 1))
        ev = tex[np.clip(((np.pi / 2 - dec) / np.pi * H).astype(int), 0, H - 1),
                 np.clip((ra / (2 * np.pi) * W).astype(int), 0, W - 1)]
        res["equirect_corr"] = round(float(np.corrcoef(sample_cube(t, d), ev)[0, 1]), 5)
        res["equirect_corr_scrambled_control"] = round(
            float(np.corrcoef(sample_cube(t[[3, 5, 1, 4, 0, 2]], d), ev)[0, 1]), 5)
        res["check_equirect_agrees"] = bool(
            res["equirect_corr"] > 0.98 and abs(res["equirect_corr_scrambled_control"]) < 0.05)
    else:
        res["equirect_corr"] = None

    res["all_checks_pass"] = all(v for k, v in res.items() if k.startswith("check_"))
    return res


# ---- the container ---------------------------------------------------------

def write_container(path, magic, rows, meta, columns):
    """CONTRACT 6.1.1. columns is [(name, bytes)], each 8-byte aligned in the payload.

    Written to path + '.tmp', read back through the offsets it declared, then os.replace'd.
    On any failure the temporary AND the stale target are removed, so a half-written or
    superseded layer can never be loaded by the app.

    ponytail: inline because tools/cosmodata/csdata.py (CONTRACT 6.1.3, integrator-owned,
    P0.2) does not exist yet. When it lands, delete this and call csdata.write.
    """
    payload, offsets = bytearray(), {}
    for name, blob in columns:
        payload += b"\0" * ((-len(payload)) % 8)
        offsets[name] = len(payload)
        payload += blob
    payload += b"\0" * ((-len(payload)) % 8)
    for c in meta["columns"]:
        c["offset"] = offsets[c["name"]]
    text = json.dumps(meta, separators=(",", ":")).encode("utf-8")
    text += b" " * ((-len(text)) % 8)
    header = (magic.encode("ascii") + (1).to_bytes(4, "little") +
              int(rows).to_bytes(4, "little") + (0).to_bytes(4, "little") +
              len(text).to_bytes(4, "little") + (0).to_bytes(4, "little") +
              len(payload).to_bytes(8, "little"))
    assert len(header) == 32, len(header)
    tmp = path + ".tmp"
    os.makedirs(os.path.dirname(path), exist_ok=True)
    try:
        with open(tmp, "wb") as f:
            f.write(header)
            f.write(text)
            f.write(bytes(payload))
            f.flush()
            os.fsync(f.fileno())
        raw = open(tmp, "rb").read()
        if len(raw) != 32 + len(text) + len(payload):
            raise SystemExit("readback: length %d, declared %d"
                             % (len(raw), 32 + len(text) + len(payload)))
        if raw[:4].decode("ascii") != magic:
            raise SystemExit("readback: magic")
        if json.loads(raw[32:32 + len(text)].decode("utf-8"))["kind"] != meta["kind"]:
            raise SystemExit("readback: meta kind")
        base = 32 + len(text)
        for name, blob in columns:
            at = base + offsets[name]
            if raw[at:at + len(blob)] != bytes(blob):
                raise SystemExit("readback: column %s differs" % name)
        os.replace(tmp, path)
    except BaseException:
        for p in (tmp, path):
            if os.path.exists(p):
                os.remove(p)
        raise
    return len(raw)


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def git_commit():
    try:
        rev = subprocess.run(["git", "-C", REPO, "rev-parse", "--short", "HEAD"],
                             capture_output=True, text=True, timeout=10).stdout.strip()
        dirty = subprocess.run(["git", "-C", REPO, "status", "--porcelain"],
                               capture_output=True, text=True, timeout=30).stdout.strip()
        return (rev or "unknown") + ("+dirty" if dirty else "")
    except Exception:
        return "unknown"


# ---- main ------------------------------------------------------------------

def build(in_path, mask_path, out_path, side, column, scale_to_uk, source_id):
    ngp_gal = EQ2GAL @ unit(*NGP_RADEC)
    if abs(ngp_gal[2] - 1.0) > 1e-9:
        raise SystemExit("EQ2GAL does not send the north galactic pole to +z: %r" % (ngp_gal,))

    t0 = time.time()
    cube, how = load_cube(in_path, side, column, scale_to_uk)
    mask_full = load_mask_cube(mask_path, side)
    mask = load_mask_cube(mask_path, MASK_FACE)

    res = {"input": os.path.basename(in_path), "mask": os.path.basename(mask_path),
           "projection": how, "faceSize": side, "maskFaceSize": MASK_FACE}
    run_checks(cube, mask_full, res,
               equirect=os.path.join(STAGING, "cmb_equirect_4096x2048.f16"))
    if not res["all_checks_pass"]:
        for p in (out_path, out_path + ".tmp"):
            if os.path.exists(p):
                os.remove(p)
        print(json.dumps(res, indent=1))
        raise SystemExit("refused: a check failed. Nothing written; any stale target removed.")

    q = np.clip(np.round((np.nan_to_num(cube) - LO) / SCALE), 0, 255).astype(np.uint8)
    res["clippedFraction"] = round(float(np.mean((cube < LO) | (cube > HI))), 7)
    res["quantisationMaxErrorUk"] = round(float(np.abs(q * SCALE + LO - cube).max()), 4)
    if res["quantisationMaxErrorUk"] > SCALE:
        raise SystemExit("quantisation error %g exceeds one step %g"
                         % (res["quantisationMaxErrorUk"], SCALE))

    meta = {
        "kind": "cmb", "schema": 1,
        "generator": "tools/cosmodata/build_cmb.py", "generatorCommit": git_commit(),
        "generatedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "licenceProfile": "commercial",
        "frame": "ICRF", "epoch": "J2000.0",
        "sources": [{
            "id": source_id,
            "name": "WMAP nine-year (DR5) Internal Linear Combination CMB map, and the KQ85 "
                    "temperature analysis mask",
            "url": "https://lambda.gsfc.nasa.gov/data/map/dr5/dfp/ilc/wmap_ilc_9yr_v5.fits",
            "retrievedUtc": "2026-09-12T16:11:00Z",
            "licence": "CC0 1.0 (NASA science data)", "licenceTag": "pd-ack",
            "attribution":
                "Cosmic microwave background: WMAP 9-year Internal Linear Combination map and "
                "KQ85 temperature analysis mask. Credit: NASA / WMAP Science Team, via the "
                "Legacy Archive for Microwave Background Data Analysis (LAMBDA)."}],
        "faceSize": side, "maskFaceSize": MASK_FACE, "source": "wmap", "beamArcmin": 60.0,
        "units": "uK_CMB, monopole and dipole removed",
        "faceOrder": FACES,
        "faceConvention":
            "u = 2s-1 across, v = 2t-1 down; texel (i,j) at s=(i+0.5)/F, t=(j+0.5)/F; row j=0 "
            "is t=0; +x (1,-v,-u)  -x (-1,-v,u)  +y (u,1,v)  -y (u,-1,-v)  +z (u,-v,1)  "
            "-z (-u,-v,-1). Every component is ICRF. No Unity axis appears in this file.",
        "maskMeaning":
            "1 = outside the WMAP KQ85 temperature analysis mask, so the ILC there carries "
            "galactic foreground residual and dT is Inferred rather than Measured. 0 = kept. "
            "WMAP does no inpainting: nothing in this file is a constrained realisation.",
        "columns": [
            {"name": "t", "type": "u8", "components": 1, "unit": "uK", "offset": 0,
             "encoding": {"kind": "linear", "scale": SCALE, "offset": LO}},
            {"name": "mask", "type": "u8", "components": 1, "table": "mask",
             "rows": 6 * MASK_FACE * MASK_FACE, "offset": 0},
        ],
        "provenance": {
            "t": {"class": "Measured", "source": source_id,
                  "reference": "WMAP nine-year ILC, 1 degree beam. Inferred where mask = 1."},
            "mask": {"class": "Measured", "source": source_id,
                     "reference": "WMAP KQ85 temperature analysis mask, r9"},
            "dipole": {"class": "Measured", "source": "hinshaw2009",
                       "reference": "WMAP five-year, ApJS 180, 225"},
            "monopole": {"class": "Measured", "source": "fixsen2009",
                         "reference": "COBE/FIRAS, ApJ 707, 916"},
            "colour": {"class": "Procedural",
                       "reference": "false colour; the CMB's Wien peak is at 1.06 mm"},
        },
        "counts": {"rows": 6 * side * side, "maskRows": 6 * MASK_FACE * MASK_FACE,
                   "rejectedTexels": int(mask.sum())},
        "stats": {"rmsUk": round(res["rms_uK"], 3), "meanUk": round(res["mean_uK"], 3),
                  "p01": round(res["p01_uK"], 2), "p99": round(res["p99_uK"], 2),
                  "min": round(res["min_uK"], 2), "max": round(res["max_uK"], 2),
                  "clippedFraction": res["clippedFraction"],
                  "maskRejectedFraction": res["mask_rejected_fraction"],
                  "coldSpot5degMeanUk": res["coldspot_5deg_mean_uK"],
                  "coldSpot5degMeanFromLbUk": res["coldspot_from_lb_5deg_mean_uK"],
                  "rmsUnderB2": res["rms_b_under_2_uK"], "rmsOverB30": res["rms_b_over_30_uK"]},
        "assertions": [
            {"name": "rms_is_cmb", "expected": "30 < rms < 300 uK",
             "got": round(res["rms_uK"], 3), "ok": res["check_rms_is_cmb"]},
            {"name": "mean_small", "expected": "|mean| < 10 uK",
             "got": round(res["mean_uK"], 3), "ok": res["check_mean_small"]},
            {"name": "percentiles", "expected": "|p01| and |p99| < 400 uK",
             "got": [round(res["p01_uK"], 2), round(res["p99_uK"], 2)],
             "ok": res["check_percentiles"]},
            {"name": "no_nan", "expected": 0, "got": res["nan_count"], "ok": res["check_no_nan"]},
            {"name": "galactic_plane_residual",
             "expected": "|dT| peaks in |b| < 2 deg, and rms(|b|<2) > 1.05 x rms(|b|>30)",
             "got": {"peakBin": res["absT_peak_bin_deg"], "ratio": res["plane_rms_ratio"]},
             "ok": res["check_plane_peaks_on_plane"]},
            {"name": "cold_spot_5deg_mean_uK", "expected": -107.0, "tolerance": 47.0,
             "got": res["coldspot_5deg_mean_uK"], "ok": res["check_coldspot"]},
            {"name": "kq85_mask_agreement",
             "expected": "rms(rejected)/rms(kept) > 1.10 and > 1.15x the z90-rotated control",
             "got": {"ratio": res["mask_rms_ratio"],
                     "control": res["mask_rms_ratio_rotated_control"]},
             "ok": res["check_mask_agrees"]},
            {"name": "equirect_agreement",
             "expected": "corr > 0.98 with the independently written equirect bake; a "
                         "scrambled face order scores under 0.05",
             "got": {"corr": res.get("equirect_corr"),
                     "control": res.get("equirect_corr_scrambled_control")},
             "ok": res.get("check_equirect_agrees")},
        ],
        "dipole": {
            "note": "NOT baked into t. Add it in the shader from these constants, so the app "
                    "can say which mode it is in. Core/Cmb/CmbModel.cs holds the same numbers.",
            "amplitudeUk": 3355.0, "amplitudeUncertaintyUk": 8.0,
            "galacticLonDeg": 263.99, "galacticLatDeg": 48.26,
            "icrsRaDeg": 167.9287, "icrsDecDeg": -6.9269,
            "impliedSpeedKmPerS": 369.0,
            "reference": "Hinshaw et al. 2009, WMAP five-year, ApJS 180, 225"},
        "monopole": {"kelvin": 2.72548, "uncertaintyKelvin": 0.00057,
                     "reference": "Fixsen 2009, ApJ 707, 916 (COBE/FIRAS)"},
    }

    res["bytes"] = write_container(out_path, "CSCB", 6 * side * side, meta,
                                   [("t", q.tobytes()), ("mask", mask.tobytes())])
    res["sha256"] = sha256(out_path)
    res["seconds"] = round(time.time() - t0, 1)
    print(json.dumps(res, indent=1))
    print("wrote %s  %d bytes  sha256 %s" % (out_path, res["bytes"], res["sha256"]))
    return res


# ---- self-test: the FITS branch on a map whose answer is known -------------

def _card(k, v):
    if isinstance(v, str):
        v = "'%-8s'" % v
    return ("%-8s= %20s" % (k, v)).ljust(80).encode("ascii")


def _write_synthetic_fits(path, nside, values):
    prim = b"".join([_card("SIMPLE", "T"), _card("BITPIX", 32), _card("NAXIS", 0),
                     b"END".ljust(80)])
    prim += b" " * ((-len(prim)) % 2880)
    ext = b"".join([_card("XTENSION", "BINTABLE"), _card("BITPIX", 8), _card("NAXIS", 2),
                    _card("NAXIS1", 4), _card("NAXIS2", values.size), _card("PCOUNT", 0),
                    _card("GCOUNT", 1), _card("TFIELDS", 1), _card("TTYPE1", "TEMPERATURE"),
                    _card("TFORM1", "1E"),
                    _card("TUNIT1", "K"),            # a lie, exactly as Planck's is
                    _card("PIXTYPE", "HEALPIX"), _card("ORDERING", "NESTED"),
                    _card("NSIDE", nside), b"END".ljust(80)])
    ext += b" " * ((-len(ext)) % 2880)
    data = values.astype(">f4").tobytes()
    data += b"\0" * ((-len(data)) % 2880)
    open(path, "wb").write(prim + ext + data)


def selftest(tmpdir):
    nside, side = 32, 64
    z, phi = pix2ang_nest(nside, np.arange(12 * nside * nside))
    vals = (100.0 * np.sin(3 * phi) * z).astype(np.float32)      # CMB-like rms
    hg = unit(120.0, 30.0)                                        # galactic (l, b)
    hot = int(ang2pix_nest(nside, np.array([hg[2]]), np.array([np.arctan2(hg[1], hg[0])]))[0])
    vals[hot] = 480.0
    hz, hphi = pix2ang_nest(nside, np.array([hot]))
    hot_dir = np.array([np.sqrt(1 - hz[0] ** 2) * np.cos(hphi[0]),
                        np.sqrt(1 - hz[0] ** 2) * np.sin(hphi[0]), hz[0]])

    fits = os.path.join(tmpdir, "synthetic.fits")
    _write_synthetic_fits(fits, nside, vals / 1000.0)             # stored in "mK"
    cube, how = load_cube(fits, side, "TEMPERATURE", 1000.0)

    # 1. the hot pixel comes out where it was put, in ICRF, to within a texel. A wrong face
    #    convention or a transposed rotation moves it by tens of degrees.
    k, j, i = np.unravel_index(int(np.argmax(cube)), cube.shape)
    got = face_dirs(FACES[k], side)[j, i] @ EQ2GAL.T
    sep = np.degrees(np.arccos(np.clip(np.dot(got / np.linalg.norm(got), hot_dir), -1, 1)))
    texel = 90.0 / side
    assert sep < 1.5 * texel, "hot pixel landed %.3f deg away (texel %.3f)" % (sep, texel)
    assert abs(cube.max() - 480.0) < 1e-2, cube.max()

    # 2. the smooth part is the map, not something like it
    g = face_dirs("+z", side) @ EQ2GAL.T
    want = vals[ang2pix_nest(nside, g[..., 2], np.arctan2(g[..., 1], g[..., 0]))]
    assert np.allclose(cube[FACES.index("+z")], want, atol=1e-2), "the +z face is not the map"

    # 3. the shipped encoding is exact at both ends
    assert 0 * SCALE + LO == -500.0 and abs(255 * SCALE + LO - 500.0) < 1e-12

    # 4. the range gate has teeth: the same map in the wrong units must be refused
    bad = os.path.join(tmpdir, "bad.fits")
    _write_synthetic_fits(bad, nside, vals)                       # already uK, so x1000 is wrong
    c2, _ = load_cube(bad, side, "TEMPERATURE", 1000.0)
    r = {}
    run_checks(c2, np.zeros((6, side, side), np.uint8), r)
    assert not r["check_rms_is_cmb"], "a 1000x map passed the rms gate at %g" % r["rms_uK"]
    assert not r["all_checks_pass"]

    # 5. the container round trips through its own reader
    meta = {"kind": "cmb", "columns": [{"name": "t"}, {"name": "mask"}]}
    p = os.path.join(tmpdir, "t.cscb")
    n = write_container(p, "CSCB", 4, meta, [("t", b"\x01\x02\x03\x04"), ("mask", b"\xff")])
    assert n == os.path.getsize(p) and meta["columns"][1]["offset"] == 8, meta["columns"]

    print("build_cmb selftest OK (%s; hot pixel %.3f deg, texel %.3f deg; "
          "wrong-units rms %.0f uK refused)" % (how, sep, texel, r["rms_uK"]))


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--in", dest="inp", default=DEFAULT_IN)
    p.add_argument("--mask", default=DEFAULT_MASK)
    p.add_argument("--out", default=DEFAULT_OUT)
    p.add_argument("--face", type=int, default=1024)
    p.add_argument("--column", default="TEMPERATURE")
    p.add_argument("--scale-to-uk", type=float, default=1000.0,
                   help="native units to microkelvin; WMAP mK = 1000. Never read from TUNIT.")
    p.add_argument("--source-id", default="wmap9-ilc")
    p.add_argument("--selftest", action="store_true")
    a = p.parse_args()
    if a.selftest:
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            selftest(d)
        return
    build(a.inp, a.mask, a.out, a.face, a.column, a.scale_to_uk, a.source_id)


if __name__ == "__main__":
    main()
