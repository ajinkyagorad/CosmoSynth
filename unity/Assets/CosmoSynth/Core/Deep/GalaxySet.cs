using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Per-row flags of the Local Volume file. Mirrors the constants at the top of
    /// tools/cosmodata/build_localvolume.py and the `provenanceBits` block it writes; the
    /// three must change together.
    /// </summary>
    [Flags]
    public enum GalaxyFlags : uint
    {
        None = 0,

        /// <summary>No ellipticity was published. Draw it round and faint. Never "0.5".</summary>
        EllipticityUnknown = 1u << 0,

        /// <summary>No position angle was published. Do not orient this galaxy.</summary>
        PositionAngleUnknown = 1u << 1,

        /// <summary>No half-light radius was published.</summary>
        SizeUnknown = 1u << 2,

        /// <summary>M_V was derived as m − DM rather than published.</summary>
        AbsMagDerived = 1u << 3,

        /// <summary>The Milky Way row: the galactic centre. Never drawn as a point.</summary>
        IsHome = 1u << 4,

        /// <summary>One of the Local Group landmarks the label layer names.</summary>
        LandmarkLocalGroup = 1u << 5,

        /// <summary>Shape filled from a 2MASS XSC counterpart, not from the primary catalogue.</summary>
        ShapeFromXsc = 1u << 6,

        /// <summary>
        /// The 2MASS ellipse fit was rejected because two band fits disagreed. NGC 3109, an
        /// edge-on irregular, fits at axis ratio 1.000 — perfectly face on. A shape like that
        /// is worse than no shape.
        /// </summary>
        ShapeUnreliable = 1u << 7,

        /// <summary>
        /// The distance is the median of the LVDB satellites that name this galaxy as host,
        /// not a measurement of the galaxy itself.
        /// </summary>
        DistanceFromSatellites = 1u << 8,

        /// <summary>An authored kinematic inclination overrides the photometric one.</summary>
        InclinationOverride = 1u << 9,
    }

    /// <summary>Per-row flags of the redshift-survey files (CSRS).</summary>
    [Flags]
    public enum SurveyFlags : ushort
    {
        None = 0,

        /// <summary>
        /// z &lt; 0.01, where the galaxy's own motion is a large fraction of the redshift, so
        /// the distance is weak. No CMB-frame correction has been applied to any row.
        /// </summary>
        NearbyPeculiarVelocity = 1 << 0,

        /// <summary>No usable shape fit: the axis ratio and position angle are NaN.</summary>
        ShapeUnknown = 1 << 1,
    }

    /// <summary>One galaxy, as the shipped catalogue has it.</summary>
    public readonly struct Galaxy
    {
        /// <summary>ICRF cartesian, in <see cref="GalaxySet.PositionUnit"/>.</summary>
        public Double3 Position { get; }

        public double DistanceError { get; }
        public double AbsoluteMagnitude { get; }
        public double HalfLightRadius { get; }

        /// <summary>Projected axis ratio b/a, or NaN where no shape was measured.</summary>
        public double AxisRatio { get; }

        /// <summary>Position angle of the major axis, degrees East of North in [0, 180), or NaN.</summary>
        public double PositionAngleDeg { get; }

        /// <summary>An authored kinematic inclination, or NaN — see <see cref="GalaxySet.InclinationDeg"/>.</summary>
        public double OverrideInclinationDeg { get; }

        public int TType { get; }
        public uint Flags { get; }
        public string Name { get; }

        public Galaxy(Double3 position, double distanceError, double absoluteMagnitude,
            double halfLightRadius, double axisRatio, double positionAngleDeg,
            double overrideInclinationDeg, int tType, uint flags, string name)
        {
            Position = position;
            DistanceError = distanceError;
            AbsoluteMagnitude = absoluteMagnitude;
            HalfLightRadius = halfLightRadius;
            AxisRatio = axisRatio;
            PositionAngleDeg = positionAngleDeg;
            OverrideInclinationDeg = overrideInclinationDeg;
            TType = tType;
            Flags = flags;
            Name = name;
        }

        public double Distance => Position.Magnitude;

        public bool HasShape => !double.IsNaN(AxisRatio) && !double.IsNaN(PositionAngleDeg);

        public override string ToString() =>
            (string.IsNullOrEmpty(Name) ? "galaxy" : Name) + $" at {Distance:F1}";
    }

    /// <summary>
    /// Every galaxy catalogue the app ships, behind one reader: the Local Volume (`CSLV`,
    /// kiloparsecs, with shapes and sizes), the redshift surveys (`CSRS`, megaparsecs, with
    /// shapes), and the plain point sets (`CSPT`, megaparsecs, positions and photometry only).
    /// They differ in which columns are present, not in what the columns mean, so one class
    /// reads all three and <see cref="Has"/> answers the difference.
    ///
    /// **The rule this class exists to enforce.** The WebXR atlas drew 542 default
    /// ellipticities and 689 default position angles as if they had been measured, and its
    /// galaxies therefore had confident orientations that nobody had ever observed. Here a
    /// field that was not measured is NaN, its flag bit is set, and
    /// <see cref="ShapeIsMeasured"/> answers honestly. A renderer that asks gets the truth; a
    /// renderer that does not ask gets NaN, which is loud.
    /// </summary>
    public sealed class GalaxySet
    {
        private readonly CsData _data;
        private readonly float[] _pos;
        private readonly float[] _distErr;
        private readonly float[] _absMag;
        private readonly float[] _size;
        private readonly float[] _ell;          // CSLV: 1 − b/a
        private readonly float[] _ba;           // CSRS: b/a directly
        private readonly float[] _pa;
        private readonly float[] _incl;
        private readonly float[] _redshift;
        private readonly sbyte[] _ttype;
        private readonly uint[] _flags32;
        private readonly ushort[] _flags16;
        private readonly ushort[] _refIndex;
        private readonly string[] _names;
        private readonly string[] _refs;

        public CsData Data => _data;
        public int Count { get; }

        /// <summary>"kpc" for the Local Volume, "Mpc" for everything beyond it.</summary>
        public string PositionUnit { get; }

        /// <summary>Multiply a position by this to get megaparsecs.</summary>
        public double PositionScaleToMpc => PositionUnit == "kpc" ? 0.001 : 1.0;

        /// <summary>
        /// The intrinsic disc thickness the inclinations assume. Not a measurement — the
        /// conventional 0.2 — which is why it is exposed rather than buried.
        /// </summary>
        public double IntrinsicAxisRatio { get; set; } = GalaxyOrientation.DefaultIntrinsicAxisRatio;

        private GalaxySet(CsData data)
        {
            _data = data;
            Count = data.Rows;

            _pos = data.F32("pos");
            if (_pos.Length != Count * 3)
                throw new FormatException($"{data.Magic}: 'pos' is not {Count} × 3 floats.");

            PositionUnit = UnitOf(data, "pos");

            _distErr = data.Has("distErr") ? data.F32("distErr") : null;
            _absMag = data.Has("absMag") ? data.F32("absMag") : null;
            _size = data.Has("size") ? data.F32("size") : null;
            _ell = data.Has("ell") ? data.F32("ell") : null;
            _ba = data.Has("ba") ? data.F32("ba") : null;
            _pa = data.Has("pa") ? data.F32("pa") : null;
            _incl = data.Has("incl") ? data.F32("incl") : null;
            _redshift = data.Has("z") ? data.F32("z") : null;
            _ttype = data.Has("ttype") ? data.I8("ttype") : null;
            _refIndex = data.Has("refIndex") ? data.U16("refIndex") : null;

            if (data.Has("flags"))
            {
                if (data.ComponentsOf("flags") != 1)
                    throw new FormatException($"{data.Magic}: 'flags' must be scalar.");
                try { _flags32 = data.U32("flags"); }
                catch (FormatException) { _flags16 = data.U16("flags"); }
            }

            _names = data.Strings("names");
            _refs = data.Strings("refs");
        }

        private static string UnitOf(CsData data, string column)
        {
            foreach (JsonValue c in data.Meta["columns"])
            {
                if (c["name"].AsString() == column) return c["unit"].AsString("Mpc");
            }
            return "Mpc";
        }

        /// <summary>Parse a shipped galaxy file. <paramref name="magic"/> is CSLV, CSRS or CSPT.</summary>
        public static GalaxySet Parse(byte[] data, string magic) =>
            new GalaxySet(CsData.Parse(data, magic));

        public bool Has(string column) => _data.Has(column);

        public Double3 Position(int i) =>
            new Double3(_pos[i * 3], _pos[i * 3 + 1], _pos[i * 3 + 2]);

        public double Distance(int i) => Position(i).Magnitude;

        public uint Flags(int i) =>
            _flags32 != null ? _flags32[i] : (_flags16 != null ? _flags16[i] : 0u);

        public string Name(int i) => i < _names.Length ? _names[i] : null;

        public string Reference(int i)
        {
            if (_refIndex == null || _refs.Length == 0) return null;
            int index = _refIndex[i];
            return index < _refs.Length ? _refs[index] : null;
        }

        public double AbsoluteMagnitude(int i) => _absMag != null ? _absMag[i] : double.NaN;

        public double HalfLightRadius(int i) => _size != null ? _size[i] : double.NaN;

        public double Redshift(int i) => _redshift != null ? _redshift[i] : double.NaN;

        public double PositionAngleDeg(int i) => _pa != null ? _pa[i] : double.NaN;

        /// <summary>Projected axis ratio b/a, from whichever of `ell` or `ba` the file carries.</summary>
        public double AxisRatio(int i)
        {
            if (_ba != null) return _ba[i];
            if (_ell != null) return 1.0 - _ell[i];
            return double.NaN;
        }

        /// <summary>Ellipticity 1 − b/a, or NaN where no shape was measured.</summary>
        public double Ellipticity(int i)
        {
            if (_ell != null) return _ell[i];
            if (_ba != null) return 1.0 - _ba[i];
            return double.NaN;
        }

        /// <summary>
        /// Inclination in degrees: the authored kinematic value where the file has one,
        /// otherwise derived from the axis ratio, and NaN where there is no shape at all.
        ///
        /// The derived value is a model, not a measurement — <see cref="ProvenanceOf"/> for
        /// "incl" says so — and for an elliptical it is a model of something the galaxy has
        /// not got, which is why <see cref="GalaxyOrientation.IsDiscLike"/> exists.
        /// </summary>
        public double InclinationDeg(int i)
        {
            if (_incl != null && !float.IsNaN(_incl[i])) return _incl[i];
            double q = AxisRatio(i);
            return double.IsNaN(q) ? double.NaN
                : GalaxyOrientation.InclinationDeg(q, IntrinsicAxisRatio);
        }

        public int TType(int i) => _ttype != null ? _ttype[i] : 99;

        /// <summary>
        /// Was this row's shape measured, or is it absent? The question the atlas never asked.
        /// Reads the file's own provenance block, so it cannot drift from what shipped.
        /// </summary>
        public bool ShapeIsMeasured(int i)
        {
            string field = _ba != null ? "ba" : "ell";
            if (double.IsNaN(AxisRatio(i))) return false;
            return _data.ProvenanceFor(field, Flags(i)).Class == ProvenanceClass.Measured;
        }

        /// <summary>The provenance of one field of row <paramref name="i"/>.</summary>
        public FieldProvenance ProvenanceOf(string field, int i) =>
            _data.ProvenanceFor(field, Flags(i));

        public bool IsHome(int i) => (Flags(i) & (uint)GalaxyFlags.IsHome) != 0;

        public bool IsLocalGroupLandmark(int i) =>
            (Flags(i) & (uint)GalaxyFlags.LandmarkLocalGroup) != 0;

        public Galaxy this[int i] => new Galaxy(
            Position(i),
            _distErr != null ? _distErr[i] : 0.0,
            AbsoluteMagnitude(i), HalfLightRadius(i), AxisRatio(i), PositionAngleDeg(i),
            _incl != null ? _incl[i] : double.NaN, TType(i), Flags(i), Name(i));

        /// <summary>
        /// How many galaxies lie within a distance, in this file's own position unit.
        /// <paramref name="includeHome"/> decides whether the Milky Way row counts.
        /// </summary>
        public int CountWithin(double distance, bool includeHome = true)
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
            {
                if (!includeHome && IsHome(i)) continue;
                if (Distance(i) <= distance) n++;
            }
            return n;
        }

        /// <summary>Index of the row whose name matches, ignoring case, spaces and punctuation.</summary>
        public int IndexOfName(string name)
        {
            string want = Normalise(name);
            for (int i = 0; i < _names.Length; i++)
            {
                if (Normalise(_names[i]) == want) return i;
            }
            return -1;
        }

        private static string Normalise(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>
        /// The disc normal of row <paramref name="i"/> in ICRF, or null where the shape was
        /// never measured. The near-side sign is Procedural and stable per galaxy.
        /// </summary>
        public Double3? DiscNormal(int i)
        {
            double inclination = InclinationDeg(i);
            double pa = PositionAngleDeg(i);
            if (double.IsNaN(inclination) || double.IsNaN(pa)) return null;

            Double3 los = Position(i);
            if (los.SqrMagnitude <= 0.0) return null;
            Double3 major = MajorAxisOf(i);
            return GalaxyOrientation.DiscNormal(los, major, inclination,
                GalaxyOrientation.NearSideSign(Name(i)));
        }

        /// <summary>The major axis of row <paramref name="i"/> in ICRF, from its position angle.</summary>
        public Double3 MajorAxisOf(int i)
        {
            Double3 unit = Position(i).Normalized;
            double decDeg = Math.Asin(Math.Max(-1.0, Math.Min(1.0, unit.z))) * 180.0 / Math.PI;
            double raDeg = Math.Atan2(unit.y, unit.x) * 180.0 / Math.PI;
            if (raDeg < 0.0) raDeg += 360.0;
            GalaxyOrientation.SkyBasis(raDeg, decDeg, out Double3 north, out Double3 east);
            return GalaxyOrientation.MajorAxis(north, east, PositionAngleDeg(i));
        }
    }
}
