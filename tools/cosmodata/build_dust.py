#!/usr/bin/env python3
"""Bake the shipped Milky Way dust cube, galaxy/dust.csdu (CSDU).

Source: Edenhofer et al. 2024, A&A 685, A82, the 1.25 kpc 3D dust map (Zenodo 8187943),
CC BY 4.0.  Section B named Vergely+2022 via CDS; the licence amendment of 2026-09-12 blocked
it (no redistribution grant) and promoted Edenhofer, which was section B's own fallback.

The 3.25 GB source was reduced once, during the data-staging pass, to a 256^3 10 pc Cartesian
cube in the heliocentric Galactic frame (see data-staging/edenhofer2024-3d-dust/SOURCE.txt for
the exact HTTP range requests and the reduction).  This importer re-encodes that staged cube
into the container of design B section 1.1 and, before it writes anything, checks the result
against sources the staged cube knows nothing about:

  * ten named molecular clouds from Zucker et al. 2020 must appear as density peaks at their
    published distances -- a catalogue that was never used to build the map;
  * the all-sky column-extinction map staged beside the cube, integrated independently through
    the HEALPix shells, must agree with this importer's own ray integral through the cube, which
    is what pins the direction convention;
  * the eight-flip test of design B section 2.7 step 4: the identity axis assignment must beat
    all seven sign flips by a margin.

numpy is the declared exception here (binding decision 6).  Nothing else imports it.

    python3 tools/cosmodata/build_dust.py [--out DIR] [--step N]
"""

import argparse
import json
import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import csdata

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
STAGING = os.environ.get("COSMODATA_STAGING",
                         "/home/triton/projects/CosmoSynth/data-staging")
EDEN = os.path.join(STAGING, "edenhofer2024-3d-dust")
SRC_BIN = os.path.join(EDEN, "edenhofer2024_dust_256x256x256_10pc_u8.bin")
SRC_META = os.path.join(EDEN, "cube_meta.json")
SRC_COLUMN = os.path.join(EDEN, "dust_column_nside128_nest.f32")

SRC_SHA256 = "d06dbe9b090c09b7bb6459d760e734c0ce44bbf27a02a55230d6c200c4401cde"

ATTRIBUTION = (
    "3D dust map: Edenhofer, Zucker, Frank, Saydjari, Speagle, Finkbeiner and Ensslin (2024), "
    "A&A 685, A82. Data via Zenodo record 8187943, CC BY 4.0. Changes: the MEAN HEALPix "
    "reconstruction was reduced from nside 256 to nside 128, resampled onto a 256^3 Cartesian "
    "grid of 10 pc voxels, and log-quantised to 8 bits.")

ZUCKER = os.path.join(STAGING, "zucker2020-cloud-distances",
                      "Handbook_Distances_Zucker2020.dat")

# The named clouds used to check the cube.  Their (l, b, d) are NOT written here: they are read
# from the staged Zucker et al. 2020 (AJ 879, 125) table, and for each name the importer takes
# the one real sightline whose d50 is that cloud's median.  A hand-copied median of l, of b and
# of d separately is not a sightline -- it mixes three different rows, and the first run of this
# importer refused because of exactly that (California came out 54 pc off).
CLOUD_NAMES = ["Orion", "Taurus", "Ophiuchus", "Perseus", "California",
               "Cepheus", "Chamaeleon", "Aquila_Rift", "Lupus", "Coalsack"]


def zucker_sightlines(path=ZUCKER, names=None):
    """One representative (name, l, b, d50, d16, d84, n) per named cloud."""
    with open(path) as handle:
        head = handle.readline().split()
        rows = [line.split() for line in handle if line.strip()]
    ix = {k: i for i, k in enumerate(head)}
    groups = {}
    for row in rows:
        groups.setdefault(row[ix["name"]], []).append(row)
    out = []
    for name in (names if names is not None else sorted(groups)):
        group = groups[name]
        group.sort(key=lambda r: float(r[ix["d50"]]))
        pick = group[len(group) // 2]                       # a real row, at the median distance
        out.append((name, float(pick[ix["l"]]), float(pick[ix["b"]]), float(pick[ix["d50"]]),
                    float(pick[ix["d16"]]), float(pick[ix["d84"]]), len(group)))
    return out


sha256 = csdata.sha256


def commit():
    return csdata.generator_commit(REPO)


def lbd_to_xyz(l_deg, b_deg, d):
    lr, br = math.radians(l_deg), math.radians(b_deg)
    return (d * math.cos(br) * math.cos(lr), d * math.cos(br) * math.sin(lr), d * math.sin(br))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(REPO, "unity", "Assets", "StreamingAssets",
                                                  "CosmoSynth", "galaxy"))
    ap.add_argument("--step", type=int, default=1,
                    help="block-average factor; 1 ships the native 10 pc grid")
    args = ap.parse_args()
    target = os.path.join(args.out, "dust.csdu")
    assertions = []

    def check(name, ok, expected=None, got=None, note=None):
        row = {"name": name, "ok": bool(ok)}
        if expected is not None:
            row["expected"] = expected
        if got is not None:
            row["got"] = got
        if note:
            row["note"] = note
        assertions.append(row)
        if not ok:
            for stale in (target,):
                if os.path.exists(stale):
                    os.remove(stale)
            raise SystemExit("REFUSED: assertion '%s' failed (expected %r, got %r)%s"
                             % (name, expected, got, " - " + note if note else ""))
        print("  ok  %-34s %s" % (name, "" if got is None else got))

    print("build_dust.py: %s" % SRC_BIN)
    src_meta = json.load(open(SRC_META))
    size = os.path.getsize(SRC_BIN)
    check("staged cube size", size == 256 ** 3, 256 ** 3, size)
    check("staged cube sha256", sha256(SRC_BIN) == SRC_SHA256, SRC_SHA256[:16],
          sha256(SRC_BIN)[:16])

    dims = tuple(src_meta["dims"])
    check("staged dims", dims == (256, 256, 256), [256, 256, 256], list(dims))
    voxel_pc = float(src_meta["voxel_pc"])
    check("staged voxel size", abs(voxel_pc - 10.0) < 1e-9, 10.0, voxel_pc)
    vmin, vmax = float(src_meta["vmin"]), float(src_meta["vmax"])
    r_inner, r_outer = [float(v) for v in src_meta["map_radius_pc"]]

    raw = np.fromfile(SRC_BIN, dtype=np.uint8).reshape(256, 256, 256)   # (iz, iy, ix)

    # --- decode, asserting on VALUES, never on the header ---------------------------------
    rho = np.zeros(raw.shape, dtype=np.float32)
    nz = raw > 0
    rho[nz] = (vmin * (vmax / vmin) ** (raw[nz].astype(np.float64) / 255.0)).astype(np.float32)
    peak = float(rho.max())
    check("no NaN", bool(np.isfinite(rho).all()), True, True)
    check("peak density in range", 0.01 < peak < 0.2, "0.01 < E/pc < 0.2", round(peak, 6),
          "E of Zhang, Green & Rix (2023) per parsec; the file's CUNIT card, asserted by value")
    check("non-negative", float(rho.min()) >= 0.0, ">= 0", float(rho.min()))
    filled = float(nz.mean())
    check("filled fraction", 0.4 < filled < 0.6, "0.4 .. 0.6", round(filled, 4),
          "the map is a sphere of radius 1248 pc inscribed in a 2550 pc box")

    # Node centres: x = (ix - 127.5) * 10 pc.
    axis = (np.arange(256, dtype=np.float32) - 127.5) * voxel_pc
    origin = float(axis[0])

    def sample(x, y, z):
        """Nearest-node density at a heliocentric galactic position in pc."""
        ix = int(round(x / voxel_pc + 127.5))
        iy = int(round(y / voxel_pc + 127.5))
        iz = int(round(z / voxel_pc + 127.5))
        if not (0 <= ix < 256 and 0 <= iy < 256 and 0 <= iz < 256):
            return 0.0
        return float(rho[iz, iy, ix])

    def ray(l_deg, b_deg, d0, d1, step=voxel_pc):
        n = max(2, int((d1 - d0) / step))
        ds = np.linspace(d0, d1, n)
        vals = np.array([sample(*lbd_to_xyz(l_deg, b_deg, d)) for d in ds], dtype=np.float64)
        return ds, vals

    # --- the clouds: peaks must land at distances this map was not built from -------------
    sightlines = zucker_sightlines(names=CLOUD_NAMES)
    clouds = [(n, l, b, d) for n, l, b, d, _, _, _ in sightlines]
    peaks = []
    fracs = []
    worst_sigma = 0.0
    weakest = 1e9
    for name, l, b, d, d16, d84, _ in sightlines:
        ds, vals = ray(l, b, 60.0, min(1240.0, d * 2.4))
        peak_d = float(ds[int(np.argmax(vals))])
        med = float(np.median(vals[vals > 0])) if (vals > 0).any() else 0.0
        contrast = float(vals.max() / med) if med > 0 else 0.0
        # What this bound is for: catching a swapped axis, an l/b swap or a wrong distance
        # scale.  Any of those moves a peak by hundreds of per cent, or leaves no peak at all.
        # It is deliberately NOT tight enough to adjudicate the 10-15% disagreements between
        # two independent reconstructions of the same cloud -- Edenhofer's posterior mean and
        # Zucker's per-sightline fit are different measurements with different systematics, and
        # the staging pass saw the same offsets (Ophiuchus 139 -> 150 pc there).
        miss = abs(peak_d - d) / d
        worst_sigma = max(worst_sigma, miss)
        fracs.append(miss)
        weakest = min(weakest, contrast)
        peaks.append({"cloud": name, "publishedPc": d, "peakPc": round(peak_d, 1),
                      "d16": d16, "d84": d84, "fracMiss": round(miss, 4),
                      "contrast": round(contrast, 1)})
        print("      %-12s published %6.1f +%.0f/-%.0f pc -> peak %6.1f pc (%+5.1f%%),"
              " %6.0fx sightline median"
              % (name, d, d84 - d, d - d16, peak_d, 100 * (peak_d - d) / d, contrast))
    fracs.sort()
    median_miss = fracs[len(fracs) // 2]
    check("cloud peak distances (worst)", worst_sigma <= 0.15,
          "<= 15% of the published distance", round(worst_sigma, 4),
          "ten named clouds from Zucker+2020, a catalogue this map was not built from")
    check("cloud peak distances (median)", median_miss <= 0.08, "<= 8%", round(median_miss, 4),
          "every cloud but the Coalsack peaks slightly FARTHER than Zucker places it: a "
          "systematic of the reconstruction, recorded rather than tuned away")
    check("cloud peak contrast", weakest >= 5.0, ">= 5x sightline median", round(weakest, 1),
          "the weakest is the Aquila Rift, a diffuse lane rather than a compact cloud")

    # --- the eight-flip test of B section 2.7 step 4 --------------------------------------
    scores = {}
    for sx in (1, -1):
        for sy in (1, -1):
            for sz in (1, -1):
                total = 0.0
                for name, l, b, d in clouds:
                    x, y, z = lbd_to_xyz(l, b, d)
                    v = sample(sx * x, sy * y, sz * z)
                    total += math.log(max(v, 1e-12))
                scores["%+d%+d%+d" % (sx, sy, sz)] = total / len(clouds)
    order = sorted(scores.items(), key=lambda kv: -kv[1])
    best, second = order[0], order[1]
    check("axis signs: identity wins", best[0] == "+1+1+1", "+1+1+1", best[0])
    # Mean log density, so a margin is a log ratio; 1.5x in density is ln 1.5 = 0.405.
    margin = best[1] - second[1]
    check("axis signs: margin", margin >= math.log(1.5), ">= ln 1.5", round(margin, 3),
          "runner-up %s at %.3f" % (second[0], second[1]))

    # --- independent column map: pins the direction convention ----------------------------
    sys.path.insert(0, os.path.join(REPO, "docs", "research", "scripts"))
    import healpix_nest
    column = np.fromfile(SRC_COLUMN, dtype="<f4")
    check("column map size", column.size == 12 * 128 * 128, 196608, int(column.size))
    # Correlate the cube's own ray integral against the staged column map over 768 directions.
    # Amplitude cannot match: the column map integrates the nside-128 shells at 0.46 degree
    # resolution, while a ray through 10 pc voxels under-resolves a compact nearby cloud by up
    # to 4x. What the two DO share is direction, and that is the thing worth pinning -- so this
    # asserts the correlation, and then asserts that the same correlation collapses when the
    # sky is rotated by 90 degrees in longitude. A test that cannot fail proves nothing, so the
    # control is run every time and recorded beside the result.
    def column_correlation(shift_deg):
        ours, theirs, lats = [], [], []
        for pix in range(12 * 8 * 8):
            z, phi = healpix_nest.pix2ang_nest(8, pix)
            b_deg = math.degrees(math.asin(max(-1.0, min(1.0, z))))
            l_deg = math.degrees(phi)
            ds, vals = ray(l_deg, b_deg, r_inner, r_outer, 20.0)
            integral = float(np.trapezoid(vals, ds))
            if integral <= 0:
                continue
            lr = math.radians((l_deg + shift_deg) % 360.0)
            ref = float(column[healpix_nest.ang2pix_nest(128, z, lr)])
            if ref <= 0:
                continue
            ours.append(math.log(integral))
            theirs.append(math.log(ref))
            lats.append(abs(b_deg))
        a, b_, lat = np.array(ours), np.array(theirs), np.array(lats)
        # Both series are dominated by the Galactic plane, and a shift in longitude leaves that
        # trend untouched -- so a raw correlation cannot tell a right convention from a wrong
        # one. Remove the |b| trend from each (12 latitude bins) and correlate what is left:
        # that residual is the clouds, and the clouds are what has to line up.
        bins = np.clip((lat / 90.0 * 12).astype(int), 0, 11)
        for series in (a, b_):
            for k in range(12):
                m = bins == k
                if m.any():
                    series[m] -= series[m].mean()
        return float(np.corrcoef(a, b_)[0, 1]), len(ours)

    r_same, n_dir = column_correlation(0.0)
    r_shift, _ = column_correlation(90.0)
    check("column map direction", r_same >= 0.80,
          ">= 0.80 correlation of the de-trended log column", round(r_same, 3),
          "%d directions" % n_dir)
    check("column map control", r_shift <= 0.25,
          "<= 0.25 with the sky turned 90 deg in l", round(r_shift, 3),
          "the same statistic on a deliberately wrong convention: a gate that cannot fail "
          "proves nothing, so the wrong answer is computed every run and recorded")

    # --- optional reduction ---------------------------------------------------------------
    n = 256
    if args.step > 1:
        k = args.step
        if n % k:
            raise SystemExit("--step %d does not divide 256" % k)
        rho = rho.reshape(n // k, k, n // k, k, n // k, k).mean(axis=(1, 3, 5)).astype(np.float32)
        n //= k
        voxel_pc *= k
        origin = origin + 0.5 * (k - 1) * float(src_meta["voxel_pc"])
        print("  reduced to %d^3 at %.0f pc" % (n, voxel_pc))

    # --- re-encode into the container's log8 (section 1.2), which is NOT the staged codec --
    positive = rho[rho > 0]
    lo = float(np.percentile(positive, 1.0))
    hi = float(rho.max())
    enc = np.zeros(rho.shape, dtype=np.uint8)
    over = rho > lo
    ratio = np.log(rho[over] / lo) / math.log(hi / lo)
    enc[over] = np.clip(np.rint(ratio * 254.0) + 1.0, 1, 255).astype(np.uint8)
    back = np.zeros(rho.shape, dtype=np.float32)
    nzb = enc > 0
    back[nzb] = (lo * (hi / lo) ** ((enc[nzb].astype(np.float64) - 1.0) / 254.0)).astype(np.float32)
    err = np.abs(np.log(back[nzb] / rho[nzb]))
    # The bound is analytic, not chosen: rounding to the nearest of 254 log-spaced levels can
    # be wrong by at most half a step. If the measured error exceeds that, the encoder is wrong,
    # and no widening of the number would make it right.
    half_step = math.log(hi / lo) / (2.0 * 254.0)
    check("log8 round trip", float(err.max()) <= half_step * 1.001,
          "<= half a step, %.5f" % half_step, round(float(err.max()), 5),
          "254 log-spaced levels over %.3g .. %.3g E/pc" % (lo, hi))

    rows = n ** 3
    meta = {
        "kind": "dust", "schema": 1,
        "generator": "tools/cosmodata/build_dust.py", "generatorCommit": commit(),
        "generatedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "licenceProfile": os.environ.get("COSMODATA_LICENCE", "commercial"),
        "frame": "galactic-helio", "epoch": "none (static reconstruction)",
        "sources": [{
            "id": "edenhofer2024",
            "name": "Edenhofer et al. 2024, 1.25 kpc 3D dust map (MEAN, HEALPix)",
            "url": "https://zenodo.org/records/8187943/files/mean_and_std_healpix.fits",
            "retrievedUtc": "2026-09-12T14:16:28Z",
            "licence": "CC BY 4.0", "licenceTag": "by",
            "attribution": ATTRIBUTION,
            "sha256": SRC_SHA256,
        }],
        "grid": {
            "dims": [n, n, n], "sampling": "nodes",
            "origin": [origin, origin, origin],
            "step": [voxel_pc, voxel_pc, voxel_pc],
            "unit": "E(Zhang, Green & Rix 2023) per parsec",
            "frame": "galactic-helio",
            "sunNode": [(n - 1) / 2.0] * 3,
            "mapRadiusPc": [r_inner, r_outer],
        },
        "provenance": {
            "rho": {"class": "Inferred", "source": "edenhofer2024",
                    "reference": "Gaussian-process reconstruction from ~54 million stellar "
                                 "extinctions (Zhang, Green & Rix 2023 catalogue); posterior "
                                 "mean, not a measurement. Resampled and log-quantised here."},
            "grid geometry": {"class": "Measured", "source": "edenhofer2024",
                              "reference": "map footprint: a sphere of radius 68.6-1248.1 pc "
                                           "about the Sun"},
        },
        "notes": [
            "Values are NOT magnitudes. The FITS CUNIT card reads, verbatim, 'E of Zhang, "
            "Green, and Rix (2023)', per parsec. Converting E to A_V needs that catalogue's "
            "extinction curve; a monochromatic scaling is fine for rendering, but the result "
            "must not be labelled 'magnitudes'.",
            "Outside grid.mapRadiusPc[1] the cube is UNKNOWN, not empty. A raymarcher that "
            "treats the encoded 0 there as vacuum punches square corners of clear sky into "
            "the Galaxy. DustCube returns 0 outside the sphere and says so.",
            "Inside grid.mapRadiusPc[0] the reconstruction has no shells; those voxels carry "
            "the innermost shell value. The Sun sits inside the Local Bubble, so this is the "
            "one region of the cube that is an extrapolation.",
        ],
        "cloudPeaks": peaks,
        "columnMapCorrelation": {"identity": round(r_same, 4), "lPlus90": round(r_shift, 4),
                                 "directions": n_dir},
        "counts": {"rows": rows, "nonzero": int(nzb.sum()),
                   "outsideMapSphere": int(rows - int(nz.sum())) if args.step == 1 else None},
        "assertions": assertions,
    }
    columns = [csdata.Column("rho", "u8", 1, enc.tobytes(order="C"),
                             unit=meta["grid"]["unit"],
                             extra={"encoding": {"kind": "log8", "lo": lo, "hi": hi}})]
    written = csdata.write(target, "CSDU", meta, columns, rows=rows)
    csdata.write_unity_meta(target)
    print("wrote %s  %d bytes  (%d^3 at %.0f pc)" % (target, written, n, voxel_pc))


if __name__ == "__main__":
    main()
