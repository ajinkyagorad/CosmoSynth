using System;

namespace CosmoSynth.Core
{
    /// <summary>Which way the false-colour ramp runs. The legend always states which.</summary>
    public enum CmbPalette
    {
        /// <summary>Cold blue, through white at zero, to hot red. The usual one.</summary>
        Diverging = 0,

        /// <summary>Grey, for anyone who wants to see structure without a colour story.</summary>
        Grey = 1,

        /// <summary>Diverging with red and blue swapped — cold red, hot blue.</summary>
        Inverted = 2,
    }

    /// <summary>
    /// The shipped cosmic microwave background: a cube map of the anisotropy in ICRF, plus the
    /// mask saying where the galaxy is still in the way.
    ///
    /// <para>The texture holds ΔT only — the monopole and the dipole are removed in the source
    /// product and live in <see cref="CmbModel"/> as constants. <see cref="TemperatureK"/> puts
    /// them back on demand, so the app can show "the anisotropy", "the sky as we move through
    /// it" or "2.7 kelvin everywhere" and say which it is doing.</para>
    ///
    /// <para><b>Nothing here is a colour.</b> <see cref="FalseColour"/> is a function of ΔT and
    /// a contrast, evaluated on demand, so the ramp stays a decision the viewer can change and
    /// not a palette burned into an asset that can no longer be questioned.</para>
    ///
    /// <para>Frame: every direction in and out of this class is ICRF. The file contains no
    /// Unity axis; the basis change to the engine belongs to the runtime layer, and it must be
    /// the same one the star layer uses.</para>
    /// </summary>
    public sealed class CmbSky
    {
        private const string Magic = "CSCB";
        private const int HeaderBytes = 32;

        private readonly byte[] _t;             // 6 * F * F, face-major, then row j, then i
        private readonly byte[] _mask;          // 6 * M * M, 1 = galactic foreground residual
        private readonly double _scale;         // µK per raw step
        private readonly double _offset;        // µK at raw 0

        /// <summary>Edge length of one anisotropy cube face, texels.</summary>
        public int FaceSize { get; }

        /// <summary>Edge length of one mask cube face, texels.</summary>
        public int MaskFaceSize { get; }

        /// <summary>The whole meta block, for legends, provenance and the shipped assertions.</summary>
        public JsonValue Meta { get; }

        /// <summary>Beam full width of the source map, arcminutes. WMAP's ILC is 60.</summary>
        public double BeamArcmin { get; }

        private CmbSky(byte[] t, byte[] mask, int faceSize, int maskFaceSize,
                       double scale, double offset, JsonValue meta, double beamArcmin)
        {
            _t = t;
            _mask = mask;
            FaceSize = faceSize;
            MaskFaceSize = maskFaceSize;
            _scale = scale;
            _offset = offset;
            Meta = meta;
            BeamArcmin = beamArcmin;
        }

        /// <summary>The attribution line the app must display wherever this layer is shown.</summary>
        public string Attribution() => Meta["sources"][0]["attribution"].AsString("");

        /// <summary>
        /// Parse a `.cscb`. Every refusal is a <see cref="FormatException"/> naming what is
        /// wrong and the importer to re-run.
        ///
        /// ponytail: the header walk below is inline because Core/Data/CsData.cs (CONTRACT
        /// 6.1.3, integrator-owned, P0.2) does not exist yet. When it lands this becomes
        /// CsData.Parse(data, "CSCB", 1) plus two U8 lookups; the byte layout is identical.
        /// </summary>
        public static CmbSky Parse(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < HeaderBytes)
                throw new FormatException(
                    $"A CSCB file is at least {HeaderBytes} bytes; got {data.Length}.");
            if (!BitConverter.IsLittleEndian)
                throw new FormatException("CSCB is little-endian and this machine is not.");

            for (int i = 0; i < 4; i++)
            {
                if (data[i] != (byte)Magic[i])
                    throw new FormatException(
                        "This is not a CSCB cube-map sky. Rebuild it with " +
                        "'python3 tools/cosmodata/build_cmb.py'.");
            }

            uint version = BitConverter.ToUInt32(data, 4);
            if (version != 1)
                throw new FormatException($"CSCB version {version} is not version 1.");
            uint rows = BitConverter.ToUInt32(data, 8);
            if (BitConverter.ToUInt32(data, 12) != 0 || BitConverter.ToUInt32(data, 20) != 0)
                throw new FormatException("CSCB flags and reserved must both be zero.");
            uint metaBytes = BitConverter.ToUInt32(data, 16);
            if (metaBytes == 0 || metaBytes % 8 != 0)
                throw new FormatException($"CSCB metaBytes {metaBytes} is not a positive multiple of 8.");
            ulong payloadBytes = BitConverter.ToUInt64(data, 24);
            long expected = HeaderBytes + (long)metaBytes + (long)payloadBytes;
            if (data.Length != expected)
                throw new FormatException(
                    $"CSCB declares {metaBytes} meta + {payloadBytes} payload bytes, needing " +
                    $"{expected}, but the file is {data.Length}. It is truncated or was " +
                    "written by another version.");

            string json = System.Text.Encoding.UTF8.GetString(data, HeaderBytes, (int)metaBytes);
            if (!JsonValue.TryParse(json, out JsonValue meta) || !meta.IsObject)
                throw new FormatException("CSCB meta block is not a JSON object.");
            foreach (string key in new[] { "kind", "schema", "generator", "generatedUtc",
                                           "licenceProfile", "frame", "epoch", "sources",
                                           "columns", "provenance" })
            {
                if (!meta.ContainsKey(key))
                    throw new FormatException($"CSCB meta block has no \"{key}\".");
            }
            if (meta["kind"].AsString() != "cmb")
                throw new FormatException($"CSCB meta kind is \"{meta["kind"].AsString()}\", not \"cmb\".");
            if (meta["frame"].AsString() != "ICRF")
                throw new FormatException(
                    $"CSCB frame is \"{meta["frame"].AsString()}\"; this parser only reads ICRF. " +
                    "A galactic map would sit 60 degrees off every star in the scene.");

            int faceSize = meta["faceSize"].AsInt();
            int maskFaceSize = meta["maskFaceSize"].AsInt();
            if (faceSize <= 0 || (long)6 * faceSize * faceSize != rows)
                throw new FormatException(
                    $"CSCB faceSize {faceSize} does not account for {rows} rows.");

            int payloadAt = HeaderBytes + (int)metaBytes;
            byte[] t = Column(data, payloadAt, (int)payloadBytes, meta, "t",
                              6L * faceSize * faceSize, out JsonValue tColumn);
            byte[] mask = Column(data, payloadAt, (int)payloadBytes, meta, "mask",
                                 6L * maskFaceSize * maskFaceSize, out _);

            JsonValue encoding = tColumn["encoding"];
            if (encoding.IsNull || encoding["kind"].AsString() != "linear")
                throw new FormatException("CSCB column \"t\" must carry a linear encoding.");
            double scale = encoding["scale"].AsDouble();
            double offset = encoding["offset"].AsDouble();
            if (!(scale > 0.0))
                throw new FormatException($"CSCB encoding scale {scale} is not positive.");

            return new CmbSky(t, mask, faceSize, maskFaceSize, scale, offset, meta,
                              meta["beamArcmin"].AsDouble(60.0));
        }

        private static byte[] Column(byte[] data, int payloadAt, int payloadBytes,
                                     JsonValue meta, string name, long count,
                                     out JsonValue column)
        {
            column = null;
            foreach (JsonValue c in meta["columns"])
            {
                if (c["name"].AsString() == name) { column = c; break; }
            }
            if (column == null)
                throw new FormatException($"CSCB has no column \"{name}\".");
            if (column["type"].AsString() != "u8")
                throw new FormatException(
                    $"CSCB column \"{name}\" is {column["type"].AsString()}; this parser reads u8.");
            long offset = column["offset"].AsLong();
            if (offset % 8 != 0)
                throw new FormatException($"CSCB column \"{name}\" starts at {offset}, not 8-byte aligned.");
            if (offset < 0 || offset + count > payloadBytes)
                throw new FormatException(
                    $"CSCB column \"{name}\" wants {count} bytes at {offset} but the payload " +
                    $"is {payloadBytes}.");
            var bytes = new byte[count];
            Buffer.BlockCopy(data, payloadAt + (int)offset, bytes, 0, (int)count);
            return bytes;
        }

        // ---- the face table ------------------------------------------------

        /// <summary>
        /// Face index and face coordinates for an ICRF direction. CONTRACT C.12, the standard
        /// OpenGL / Vulkan / D3D cube-map selection, written in ICRF components:
        /// 0 +x (1,−v,−u)  1 −x (−1,−v,u)  2 +y (u,1,v)  3 −y (u,−1,−v)
        /// 4 +z (u,−v,1)   5 −z (−u,−v,−1), with u = 2s−1 across the face and v = 2t−1 down it.
        /// </summary>
        public static void SelectFace(Double3 dirIcrf, out int face, out double s, out double t)
        {
            double ax = System.Math.Abs(dirIcrf.x);
            double ay = System.Math.Abs(dirIcrf.y);
            double az = System.Math.Abs(dirIcrf.z);
            double u, v, ma;
            if (ax >= ay && ax >= az)
            {
                ma = ax;
                if (dirIcrf.x > 0.0) { face = 0; u = -dirIcrf.z; v = -dirIcrf.y; }
                else { face = 1; u = dirIcrf.z; v = -dirIcrf.y; }
            }
            else if (ay >= az)
            {
                ma = ay;
                if (dirIcrf.y > 0.0) { face = 2; u = dirIcrf.x; v = dirIcrf.z; }
                else { face = 3; u = dirIcrf.x; v = -dirIcrf.z; }
            }
            else
            {
                ma = az;
                if (dirIcrf.z > 0.0) { face = 4; u = dirIcrf.x; v = -dirIcrf.y; }
                else { face = 5; u = -dirIcrf.x; v = -dirIcrf.y; }
            }
            if (ma <= 0.0) { face = 4; s = 0.5; t = 0.5; return; }
            s = (u / ma + 1.0) * 0.5;
            t = (v / ma + 1.0) * 0.5;
        }

        /// <summary>
        /// The ICRF direction of the centre of texel (i, j) on a face — the exact inverse of
        /// <see cref="SelectFace"/> at texel centres, and the only place the table appears
        /// twice, so a round-trip test can catch a typo in either copy.
        /// </summary>
        public static Double3 TexelDirection(int face, int i, int j, int faceSize)
        {
            double u = (i + 0.5) / faceSize * 2.0 - 1.0;
            double v = (j + 0.5) / faceSize * 2.0 - 1.0;
            Double3 d;
            switch (face)
            {
                case 0: d = new Double3(1.0, -v, -u); break;
                case 1: d = new Double3(-1.0, -v, u); break;
                case 2: d = new Double3(u, 1.0, v); break;
                case 3: d = new Double3(u, -1.0, -v); break;
                case 4: d = new Double3(u, -v, 1.0); break;
                case 5: d = new Double3(-u, -v, -1.0); break;
                default: throw new ArgumentOutOfRangeException(nameof(face), face, "0..5");
            }
            return d.Normalized;
        }

        // ---- sampling ------------------------------------------------------

        /// <summary>Decode one raw byte to microkelvin. Raw 0 is −500 µK and 255 is +500 µK.</summary>
        public double Decode(byte raw) => raw * _scale + _offset;

        /// <summary>Raw anisotropy byte at a texel, clamped to the face.</summary>
        public byte RawAt(int face, int i, int j) =>
            _t[((long)face * FaceSize + Clamp(j, FaceSize)) * FaceSize + Clamp(i, FaceSize)];

        private static int Clamp(int v, int n) => v < 0 ? 0 : (v >= n ? n - 1 : v);

        /// <summary>
        /// The anisotropy along an ICRF direction, microkelvin. Bilinear inside a face and
        /// clamped at its edges, which is good to one texel — all that tap-to-measure needs,
        /// and the shipped map has no detail below its 1° beam anyway.
        /// </summary>
        public double Sample(Double3 dirIcrf)
        {
            SelectFace(dirIcrf, out int face, out double s, out double t);
            double fx = s * FaceSize - 0.5;
            double fy = t * FaceSize - 0.5;
            int i0 = (int)System.Math.Floor(fx);
            int j0 = (int)System.Math.Floor(fy);
            double tx = fx - i0;
            double ty = fy - j0;
            double a = Decode(RawAt(face, i0, j0)) * (1 - tx) + Decode(RawAt(face, i0 + 1, j0)) * tx;
            double b = Decode(RawAt(face, i0, j0 + 1)) * (1 - tx) + Decode(RawAt(face, i0 + 1, j0 + 1)) * tx;
            return a * (1 - ty) + b * ty;
        }

        /// <summary>
        /// True where the galaxy is still in the way: outside the WMAP KQ85 temperature
        /// analysis mask, over about 25 % of the sky.
        ///
        /// Named for what the data are. CONTRACT C.12 calls this <c>IsInpainted</c>, which is
        /// right for Planck — whose masked pixels hold a constrained realisation, a plausible
        /// invention — and wrong for WMAP, which inpaints nothing. Here the map really is the
        /// measurement everywhere; it is simply contaminated inside the mask. Saying
        /// "inpainted" would claim a fiction the file does not contain.
        /// </summary>
        public bool IsMasked(Double3 dirIcrf)
        {
            SelectFace(dirIcrf, out int face, out double s, out double t);
            int i = Clamp((int)(s * MaskFaceSize), MaskFaceSize);
            int j = Clamp((int)(t * MaskFaceSize), MaskFaceSize);
            return _mask[((long)face * MaskFaceSize + j) * MaskFaceSize + i] != 0;
        }

        /// <summary>
        /// How well the anisotropy in a direction is known: Measured on the clean sky,
        /// Inferred where the mask rejects it. This is the one the inspector should print.
        /// </summary>
        public FieldProvenance ProvenanceAt(Double3 dirIcrf)
        {
            string source = Meta["sources"][0]["id"].AsString("wmap9-ilc");
            return IsMasked(dirIcrf)
                ? FieldProvenance.Inferred(source,
                    "inside the KQ85 analysis mask: galactic foreground residual, not clean CMB")
                : FieldProvenance.Measured(source,
                    Meta["provenance"]["t"]["reference"].AsString("WMAP nine-year ILC"));
        }

        /// <summary>The fraction of the sky the mask rejects, solid-angle weighted.</summary>
        public double MaskedFraction => Meta["stats"]["maskRejectedFraction"].AsDouble(double.NaN);

        /// <summary>
        /// Physical temperature in kelvin along an ICRF direction, in a chosen rest frame:
        /// T₀ + D_frame·(n̂·d̂) + ΔT(n̂). Pass <see cref="CmbFrame.None"/> for the map as it
        /// ships — 2.72548 K plus an anisotropy of a few hundred microkelvin.
        /// </summary>
        public double TemperatureK(Double3 dirIcrf, CmbFrame frame) =>
            CmbModel.MonopoleKelvin
            + (CmbModel.DipoleMicrokelvin(dirIcrf, frame) + Sample(dirIcrf)) * 1e-6;

        // ---- colour --------------------------------------------------------

        /// <summary>
        /// False colour for an anisotropy in microkelvin: a function, not a baked palette, so
        /// the contrast and the ramp stay the viewer's choice. <paramref name="contrastUk"/>
        /// is the value that saturates the ramp (the UI offers ±100 to ±500, default ±250).
        ///
        /// Linear in ΔT with no gamma of its own — the render layer owns colour space, and a
        /// curve applied here would be applied twice.
        /// </summary>
        public static void FalseColour(double microkelvin, double contrastUk, CmbPalette palette,
                                       out double r, out double g, out double b)
        {
            if (!(contrastUk > 0.0)) contrastUk = 250.0;
            double x = microkelvin / contrastUk;
            if (x > 1.0) x = 1.0;
            else if (x < -1.0) x = -1.0;
            else if (double.IsNaN(x)) x = 0.0;

            if (palette == CmbPalette.Grey)
            {
                r = g = b = (x + 1.0) * 0.5;
                return;
            }
            // Cold is blue, hot is red, white through zero: the channel opposite the sign
            // falls away linearly while green tracks how close to zero we are.
            double hot = x > 0.0 ? x : 0.0;
            double cold = x < 0.0 ? -x : 0.0;
            double warm = 1.0;
            double mid = 1.0 - System.Math.Abs(x) * 0.65;
            double cool = 1.0;
            r = warm - cold;
            g = mid;
            b = cool - hot;
            if (palette == CmbPalette.Inverted)
            {
                double swap = r;
                r = b;
                b = swap;
            }
        }
    }
}
