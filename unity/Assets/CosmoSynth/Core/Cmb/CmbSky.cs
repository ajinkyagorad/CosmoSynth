using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// STUB (cmb lane, CONTRACT C.12) — the baked CMB cube map: six square faces of R8 anisotropy
    /// plus an inpainting mask, addressed by ICRF direction.
    ///
    /// <b>What is missing on purpose:</b> <c>Parse</c>. The <c>cmb</c> data lane owns the HEALPix
    /// resample and the <c>CSCB</c> writer. Until it lands nothing is delivered, the CMB layer
    /// declares no group, and the panel row says so.
    ///
    /// The face order and the direction-to-face arithmetic ARE here, because the renderer and the
    /// GPU face-orientation assertion have to agree with the baker about which face is which. A
    /// flipped face reads as hundreds of microkelvin of error in a place the eye cannot check —
    /// the <c>cmb-faces</c> play-mode assertion exists precisely because "it looks like the CMB"
    /// is not evidence.
    /// </summary>
    public sealed class CmbSky
    {
        /// <summary>Face order: +X, -X, +Y, -Y, +Z, -Z, in ICRF components.</summary>
        public const int Faces = 6;

        private readonly byte[][] _faces;
        private readonly byte[][] _mask;

        public CmbSky(int faceSize, byte[][] faces, byte[][] mask, string source)
        {
            FaceSize = faceSize;
            _faces = faces;
            _mask = mask;
            Source = source;
        }

        /// <summary>Edge length of one face in texels.</summary>
        public int FaceSize { get; }

        /// <summary>"Planck PR3 SMICA-noSZ" or the WMAP fallback: the legend prints it.</summary>
        public string Source { get; }

        public bool IsPresent => _faces != null && _faces.Length == Faces;

        /// <summary>Raw face bytes, for uploading to a cube map without a second copy.</summary>
        public byte[] Face(int face) => _faces == null ? null : _faces[face];

        /// <summary>
        /// Which cube face an ICRF direction falls on, and where on it.
        /// <paramref name="u"/> and <paramref name="v"/> come back in [0, 1].
        /// </summary>
        public static int FaceOf(Double3 dirIcrf, out double u, out double v)
        {
            Double3 d = dirIcrf.Normalized;
            double ax = Math.Abs(d.x), ay = Math.Abs(d.y), az = Math.Abs(d.z);
            int face;
            double sc, tc, ma;
            if (ax >= ay && ax >= az) { face = d.x > 0 ? 0 : 1; ma = ax; sc = d.x > 0 ? -d.z : d.z; tc = -d.y; }
            else if (ay >= az) { face = d.y > 0 ? 2 : 3; ma = ay; sc = d.x; tc = d.y > 0 ? d.z : -d.z; }
            else { face = d.z > 0 ? 4 : 5; ma = az; sc = d.z > 0 ? d.x : -d.x; tc = -d.y; }
            u = 0.5 * (sc / ma + 1.0);
            v = 0.5 * (tc / ma + 1.0);
            return face;
        }

        /// <summary>
        /// The inverse of <see cref="FaceOf"/>: the ICRF direction of a point on a face.
        ///
        /// It lives here, next to the forward map, because the two have to agree exactly and the
        /// only cheap proof of that is a round trip. A flipped face is not an error anywhere: it
        /// reads as hundreds of microkelvin in a place the eye cannot check, on a map that still
        /// looks exactly like the microwave background.
        /// </summary>
        public static Double3 DirectionOf(int face, double u, double v)
        {
            double sc = u * 2.0 - 1.0;
            double tc = v * 2.0 - 1.0;
            switch (face)
            {
                case 0: return new Double3(1.0, -tc, -sc).Normalized;   // +X
                case 1: return new Double3(-1.0, -tc, sc).Normalized;   // -X
                case 2: return new Double3(sc, 1.0, tc).Normalized;     // +Y
                case 3: return new Double3(sc, -1.0, -tc).Normalized;   // -Y
                case 4: return new Double3(sc, -tc, 1.0).Normalized;    // +Z
                default: return new Double3(-sc, -tc, -1.0).Normalized; // -Z
            }
        }

        /// <summary>
        /// Anisotropy in microkelvin along an ICRF direction, bilinear within a face and clamped
        /// at the face edges. Good to about one texel, which is all tap-to-measure needs.
        /// Returns 0 when no file was delivered — a flat sky, honestly empty.
        /// </summary>
        public double Sample(Double3 dirIcrf)
        {
            if (!IsPresent) return 0.0;
            int face = FaceOf(dirIcrf, out double u, out double v);
            byte[] data = _faces[face];
            if (data == null) return 0.0;

            double x = MathD.Clamp(u * FaceSize - 0.5, 0.0, FaceSize - 1.0);
            double y = MathD.Clamp(v * FaceSize - 0.5, 0.0, FaceSize - 1.0);
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            int x1 = Math.Min(x0 + 1, FaceSize - 1), y1 = Math.Min(y0 + 1, FaceSize - 1);
            double tx = x - x0, ty = y - y0;

            double a = CmbModel.DecodeMicroK(data[y0 * FaceSize + x0]);
            double b = CmbModel.DecodeMicroK(data[y0 * FaceSize + x1]);
            double c = CmbModel.DecodeMicroK(data[y1 * FaceSize + x0]);
            double e = CmbModel.DecodeMicroK(data[y1 * FaceSize + x1]);
            return MathD.Lerp(MathD.Lerp(a, b, tx), MathD.Lerp(c, e, tx), ty);
        }

        /// <summary>
        /// True where the published map is an inpainting rather than a measurement — 2.1 % of the
        /// sky along the Galactic plane and around the brightest point sources. The inspector must
        /// say "inferred" there, not quote a temperature as if it had been observed.
        /// </summary>
        public bool IsInpainted(Double3 dirIcrf)
        {
            if (_mask == null) return false;
            int face = FaceOf(dirIcrf, out double u, out double v);
            byte[] data = _mask[face];
            if (data == null) return false;
            int x = (int)MathD.Clamp(u * FaceSize, 0, FaceSize - 1);
            int y = (int)MathD.Clamp(v * FaceSize, 0, FaceSize - 1);
            return data[y * FaceSize + x] != 0;
        }

        /// <summary>Total temperature in a direction, in the chosen frame. See <see cref="CmbModel"/>.</summary>
        public double TemperatureK(Double3 dirIcrf, CmbFrame frame) =>
            CmbModel.TemperatureK(dirIcrf, Sample(dirIcrf), frame);
    }
}
