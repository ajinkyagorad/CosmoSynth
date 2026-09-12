"""Pure-numpy HEALPix NESTED ang2pix / pix2ang (Gorski et al. 2005), no healpy.

Ported from the HEALPix C++ healpix_base.cc loc2pix / pix2loc (NEST branch).
Only what a sky-texture bake needs. Run this file to self-check.
"""
import numpy as np

_JRLL = np.array([2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4])
_JPLL = np.array([1, 3, 5, 7, 0, 2, 4, 6, 1, 3, 5, 7])


def _spread(v):                      # interleave: bit i of v -> bit 2i
    v = v.astype(np.int64)
    out = np.zeros_like(v)
    for i in range(30):
        out |= ((v >> i) & 1) << (2 * i)
    return out


def _compress(v):                    # inverse of _spread on the even bits
    out = np.zeros_like(v)
    for i in range(30):
        out |= ((v >> (2 * i)) & 1) << i
    return out


def ang2pix_nest(nside, z, phi):
    """z = cos(colatitude), phi = longitude (rad). Arrays in, int64 pixel indices out."""
    z = np.asarray(z, float)
    za = np.abs(z)
    tt = np.mod(np.asarray(phi, float) / (np.pi / 2), 4.0)
    eq = za <= 2.0 / 3.0
    # equatorial belt
    t1 = nside * (0.5 + tt)
    t2 = nside * z * 0.75
    jp = np.floor(t1 - t2).astype(np.int64)
    jm = np.floor(t1 + t2).astype(np.int64)
    ifp, ifm = jp // nside, jm // nside
    face_e = np.where(ifp == ifm, ifp | 4, np.where(ifp < ifm, ifp, ifm + 8))
    ix_e = jm & (nside - 1)
    iy_e = nside - (jp & (nside - 1)) - 1
    # polar caps
    ntt = np.minimum(np.floor(tt).astype(np.int64), 3)
    tp = tt - ntt
    tmp = nside * np.sqrt(3.0 * (1.0 - za))
    jp2 = np.minimum(np.floor(tp * tmp).astype(np.int64), nside - 1)
    jm2 = np.minimum(np.floor((1.0 - tp) * tmp).astype(np.int64), nside - 1)
    north = z > 0
    face_p = np.where(north, ntt, ntt + 8)
    ix_p = np.where(north, nside - jm2 - 1, jp2)
    iy_p = np.where(north, nside - jp2 - 1, jm2)
    face = np.where(eq, face_e, face_p)
    ix = np.where(eq, ix_e, ix_p)
    iy = np.where(eq, iy_e, iy_p)
    return face * nside * nside + _spread(ix) + (_spread(iy) << 1)


def pix2ang_nest(nside, pix):
    """Pixel centres: returns (z, phi)."""
    pix = np.asarray(pix, np.int64)
    npface = nside * nside
    face = pix // npface
    ipf = pix % npface
    ix, iy = _compress(ipf), _compress(ipf >> 1)
    jr = _JRLL[face] * nside - ix - iy - 1
    fact2 = 4.0 / (12 * npface)
    nr = np.where(jr < nside, jr, np.where(jr > 3 * nside, 4 * nside - jr, nside))
    z = np.where(jr < nside, 1 - nr * nr * fact2,
        np.where(jr > 3 * nside, nr * nr * fact2 - 1, (2 * nside - jr) * 2 * nside * fact2))
    kshift = np.where((jr >= nside) & (jr <= 3 * nside), (jr - nside) & 1, 0)
    jp = (_JPLL[face] * nr + ix - iy + 1 + kshift) // 2
    jp = np.where(jp > 4 * nside, jp - 4 * nside, jp)
    jp = np.where(jp < 1, jp + 4 * nside, jp)
    phi = (jp - (kshift + 1) * 0.5) * (np.pi / 2 / nr)
    return z, phi


if __name__ == "__main__":
    # 1. the 12 base pixels sit where the HEALPix paper puts them
    z, phi = pix2ang_nest(1, np.arange(12))
    assert np.allclose(z, [2/3] * 4 + [0] * 4 + [-2/3] * 4), z
    assert np.allclose(phi[:4], np.pi / 4 + np.arange(4) * np.pi / 2)
    assert np.allclose(phi[4:8], np.arange(4) * np.pi / 2)
    # 2. round trip through every pixel centre at small nside, random ones at Planck's
    for ns in (1, 2, 4, 64, 1024, 2048):
        p = np.arange(12 * ns * ns) if ns <= 64 else np.random.default_rng(1).integers(0, 12 * ns * ns, 200000)
        assert np.array_equal(ang2pix_nest(ns, *pix2ang_nest(ns, p)), p), ns
    # 3. equal area: uniform random directions fill nside=8 pixels evenly (768 px)
    rng = np.random.default_rng(2)
    n = 3_000_000
    cnt = np.bincount(ang2pix_nest(8, rng.uniform(-1, 1, n), rng.uniform(0, 2 * np.pi, n)), minlength=768)
    exp = n / 768
    assert np.abs(cnt - exp).max() < 6 * np.sqrt(exp), (cnt.min(), cnt.max(), exp)
    # 4. nested hierarchy: a parent's 4 children are 4p..4p+3
    zc, pc = pix2ang_nest(64, np.arange(4 * 1000, 4 * 1000 + 4))
    assert np.all(ang2pix_nest(32, zc, pc) == 1000)
    print("healpix_nest self-check OK")
