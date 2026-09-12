using System;

namespace CosmoSynth.Core
{
    /// <summary>One galaxy, in the columns <c>CSLV</c> (Local Volume) and <c>CSRS</c> (2MRS) share.</summary>
    public readonly struct GalaxyRecord
    {
        /// <summary>ICRF position in the file's unit (kpc for the Local Volume, Mpc for 2MRS).</summary>
        public readonly Double3 PositionUnits;

        /// <summary>Half of (em + ep) on the distance, same unit.</summary>
        public readonly float DistErr;

        /// <summary>M_V or M_K, per <see cref="Band"/>.</summary>
        public readonly float AbsMag;

        /// <summary>0 = V, 1 = K.</summary>
        public readonly byte Band;

        /// <summary>Half-light or Holmberg radius, in the file's unit, per <see cref="SizeKind"/>.</summary>
        public readonly float Size;

        /// <summary>0 = half-light, 1 = Holmberg.</summary>
        public readonly byte SizeKind;

        /// <summary>1 - b/a. <b>NaN when unknown</b> — never a default (see <see cref="GalaxyOrientation"/>).</summary>
        public readonly float Ell;

        /// <summary>Position angle, degrees east of north, [0, 180). NaN when unknown.</summary>
        public readonly float Pa;

        /// <summary>Published inclination override in degrees, else NaN (Core derives it from Ell).</summary>
        public readonly float Incl;

        /// <summary>UNGC morphological T-type, or 99 for a dwarf with no type.</summary>
        public readonly sbyte TType;

        public readonly byte Host, Source;
        public readonly uint Flags;

        public GalaxyRecord(Double3 positionUnits, float distErr, float absMag, byte band, float size,
            byte sizeKind, float ell, float pa, float incl, sbyte tType, byte host, byte source, uint flags)
        {
            PositionUnits = positionUnits;
            DistErr = distErr;
            AbsMag = absMag;
            Band = band;
            Size = size;
            SizeKind = sizeKind;
            Ell = ell;
            Pa = pa;
            Incl = incl;
            TType = tType;
            Host = host;
            Source = source;
            Flags = flags;
        }

        public bool EllUnknown => (Flags & 1u) != 0u || float.IsNaN(Ell);
        public bool PaUnknown => (Flags & 2u) != 0u || float.IsNaN(Pa);
        public bool SizeUnknown => (Flags & 4u) != 0u || float.IsNaN(Size);
        public bool IsHome => (Flags & 16u) != 0u;
        public bool IsLocalGroupLandmark => (Flags & 32u) != 0u;

        /// <summary>
        /// Axis ratio b/a. NaN stays NaN: the whole point is that a galaxy with no published
        /// ellipticity is drawn round and faint and SAYS it is inferred, rather than being given
        /// a plausible-looking shape it never had.
        /// </summary>
        public double AxisRatio => EllUnknown ? double.NaN : 1.0 - Ell;

        /// <summary>Inclination in degrees: the override where one exists, else Hubble from b/a.</summary>
        public double InclinationDeg =>
            !float.IsNaN(Incl) ? Incl : GalaxyOrientation.InclinationDeg(AxisRatio);
    }

    /// <summary>
    /// STUB (galaxies lane, CONTRACT C.10, C.11.1) — a galaxy catalogue read through the columns
    /// that the Local Volume and 2MRS files share.
    ///
    /// <b>What is missing on purpose:</b> <c>Parse</c>. The <c>galaxies</c> data lane owns both
    /// readers. Until they land no file is delivered and the impostor layers draw nothing, which
    /// is what "not in this build" is supposed to look like.
    /// </summary>
    public sealed class GalaxySet
    {
        private readonly GalaxyRecord[] _rows;
        private readonly string[] _names;

        public GalaxySet(string kind, double kmPerUnit, GalaxyRecord[] rows, string[] names)
        {
            Kind = kind;
            KmPerUnit = kmPerUnit;
            _rows = rows ?? new GalaxyRecord[0];
            _names = names;
        }

        /// <summary>The manifest kind: <c>localvolume</c> or <c>2mrs</c>.</summary>
        public string Kind { get; }

        /// <summary>Kilometres per storage unit (kpc for CSLV, Mpc for CSRS).</summary>
        public double KmPerUnit { get; }

        public int Count => _rows.Length;

        public GalaxyRecord this[int i] => _rows[i];

        public string Name(int i) => _names == null || i >= _names.Length ? null : _names[i];

        public Double3 PositionKm(int i) => _rows[i].PositionUnits * KmPerUnit;

        /// <summary>
        /// RA and Dec of a row, needed for the sky basis the disc orientation is built in.
        /// Recomputed from the stored ICRF Cartesian rather than stored twice — two spellings of
        /// one direction is how they come to disagree.
        /// </summary>
        public void RaDec(int i, out double raRad, out double decRad)
        {
            Double3 p = _rows[i].PositionUnits;
            raRad = Math.Atan2(p.y, p.x);
            if (raRad < 0.0) raRad += MathD.TwoPi;
            double r = p.Magnitude;
            decRad = r > 0.0 ? Math.Asin(MathD.Clamp(p.z / r, -1.0, 1.0)) : 0.0;
        }
    }
}
