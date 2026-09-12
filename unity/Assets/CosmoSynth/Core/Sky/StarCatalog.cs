using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>One catalogued star: where it is, how it moves, how bright it looks, its colour.</summary>
    public readonly struct CatalogStar
    {
        /// <summary>Position in ICRF equatorial cartesian coordinates, parsecs, epoch J2000.0.</summary>
        public Double3 PositionParsecs { get; }

        /// <summary>ICRF space velocity, km/s. Zero when <see cref="StarFlags.VelocityUnknown"/>.</summary>
        public Double3 VelocityKmPerSecond { get; }

        /// <summary>Apparent visual magnitude. Smaller is brighter; Sirius is -1.44.</summary>
        public double ApparentMagnitude { get; }

        /// <summary>Effective temperature, kelvin. Inferred, never measured - see Flags.</summary>
        public double TemperatureK { get; }

        /// <summary>Johnson B-V, or NaN when none was published.</summary>
        public double ColourIndex { get; }

        /// <summary>
        /// Visual extinction toward this star, magnitudes, already folded out of the distance
        /// modulus by whoever built the file. Applied exactly ONCE, here: a dust volume that
        /// also dims it would count the same cloud twice.
        /// </summary>
        public double ExtinctionAv { get; }

        /// <summary>Hipparcos number, 0 if none. An identifier, used for joins and picking.</summary>
        public uint Hip { get; }

        /// <summary>Henry Draper number, 0 if none.</summary>
        public uint Hd { get; }

        /// <summary>Spectral class and subclass packed as class*10+subclass, 255 unknown.</summary>
        public byte SpectralCode { get; }

        public StarFlags Flags { get; }

        public CatalogStar(Double3 positionParsecs, Double3 velocityKmPerSecond,
            double apparentMagnitude, double temperatureK, double colourIndex,
            double extinctionAv, uint hip, uint hd, byte spectralCode, StarFlags flags)
        {
            PositionParsecs = positionParsecs;
            VelocityKmPerSecond = velocityKmPerSecond;
            ApparentMagnitude = apparentMagnitude;
            TemperatureK = temperatureK;
            ColourIndex = colourIndex;
            ExtinctionAv = extinctionAv;
            Hip = hip;
            Hd = hd;
            SpectralCode = spectralCode;
            Flags = flags;
        }

        /// <summary>
        /// The short form: position, brightness, colour temperature and flags, with every
        /// other field absent rather than zero. Used by the procedural star-system generator
        /// and by tests, which have no catalogue row behind them. `VelocityUnknown` and
        /// `ExtinctionUnknown` are set because that is what is true of such a star.
        /// </summary>
        public CatalogStar(Double3 positionParsecs, double apparentMagnitude, double temperatureK,
            StarFlags flags)
            : this(positionParsecs, default, apparentMagnitude, temperatureK, double.NaN, 0.0,
                0u, 0u, 255, flags | StarFlags.VelocityUnknown | StarFlags.ExtinctionUnknown)
        {
        }

        /// <summary>
        /// Unit vector toward the star in ICRF. Always meaningful, including for the stars
        /// whose distance is not known - direction is what was measured.
        /// </summary>
        public Double3 Direction => PositionParsecs.Normalized;

        /// <summary>Distance in parsecs, or null when the parallax was unusable.</summary>
        public double? DistanceParsecs =>
            (Flags & StarFlags.DistanceUnknown) != 0 ? (double?)null : PositionParsecs.Magnitude;

        public bool HasProperName => (Flags & StarFlags.HasProperName) != 0;

        /// <summary>Spectral class letter, or '?' when none was published.</summary>
        public char SpectralClass =>
            SpectralCode == 255 || SpectralCode / 10 >= StarCatalog.SpectralClasses.Length
                ? '?'
                : StarCatalog.SpectralClasses[SpectralCode / 10];

        /// <summary>
        /// Absolute visual magnitude, de-reddened. NaN when the distance is unknown, which is
        /// the honest answer: without a distance there is no absolute magnitude.
        /// </summary>
        public double AbsoluteMagnitude
        {
            get
            {
                double? d = DistanceParsecs;
                if (!d.HasValue) return double.NaN;
                return Photometry.AbsoluteMagnitude(ApparentMagnitude - ExtinctionAv, d.Value);
            }
        }

        /// <summary>
        /// Position at an epoch offset from J2000.0, in years, by linear space motion.
        /// Good to about 1e5 years: a curved galactic orbit bends the path by under 0.1 deg
        /// over that span, which is far below anything this renderer can show.
        /// </summary>
        public Double3 PositionAt(double yearsFromJ2000)
        {
            if ((Flags & StarFlags.VelocityUnknown) != 0 ||
                (Flags & StarFlags.VelocitySuspect) != 0)
                return PositionParsecs;
            // km/s -> pc/yr
            const double KmPerSecondToParsecPerYear = 1.0 / 977792.2;
            return PositionParsecs +
                   VelocityKmPerSecond * (yearsFromJ2000 * KmPerSecondToParsecPerYear);
        }

        public override string ToString() =>
            $"mag {ApparentMagnitude:F2}, {TemperatureK:F0} K, " +
            (DistanceParsecs.HasValue ? $"{DistanceParsecs.Value:F2} pc" : "distance unknown");
    }

    [Flags]
    public enum StarFlags
    {
        None = 0,

        /// <summary>
        /// The parallax was unusable, so only the direction is known. Kept and flagged rather
        /// than dropped: silently placing the star at the sentinel distance would invent a
        /// measurement, and dropping it would delete part of the sky.
        /// </summary>
        DistanceUnknown = 1 << 0,

        /// <summary>
        /// No B-V was published, so the temperature comes from the spectral class alone. That
        /// is a weaker claim than a colour measurement and is not the same thing.
        /// </summary>
        TemperatureFromSpectrum = 1 << 1,

        HasProperName = 1 << 2,

        /// <summary>V came from Tycho photometry, not Johnson V. Wrong by up to 4.6 mag at the bright end.</summary>
        PhotometryFromTycho = 1 << 3,

        /// <summary>No radial velocity, so the velocity is tangential only.</summary>
        RadialVelocityMissing = 1 << 4,

        /// <summary>Appended from the nearby-star census rather than the main catalogue.</summary>
        FromNearbyCensus = 1 << 5,

        /// <summary>No visual magnitude at all (brown dwarfs); <see cref="CatalogStar.ApparentMagnitude"/> is NaN.</summary>
        NoVisualMagnitude = 1 << 6,

        /// <summary>
        /// The computed speed exceeded 1,000 km/s, which is an astrometric error and not a
        /// star: disc and halo stars stay under about 600 km/s relative to the Sun. Zeroed.
        /// </summary>
        VelocitySuspect = 1 << 7,

        // bits 8-10 are the distance-source code, read with DistanceSourceOf.

        /// <summary>
        /// No extinction model covered this star, so <see cref="CatalogStar.ExtinctionAv"/> is
        /// zero because nothing measured it - not because the sightline is clear.
        /// </summary>
        ExtinctionUnknown = 1 << 11,

        /// <summary>No proper motion was published, so the velocity is unknown, not zero.</summary>
        VelocityUnknown = 1 << 12,

        /// <summary>
        /// The row came through the CSS1 container, which carried no velocity, colour index,
        /// spectral class or cross-identifier. Those fields are absent, not measured as zero.
        /// </summary>
        FromCss1Migration = 1 << 13,
    }

    public enum DistanceSource
    {
        Unknown = 0, GaiaDr3 = 1, GaiaDr2 = 2, Hipparcos = 3,
        Gliese = 4, Other = 5, NearbyCensus = 6, GroundTrigonometric = 7,
    }

    /// <summary>
    /// The shipped star catalogue: real stars at their measured three-dimensional positions.
    ///
    /// Read from the CSS2 container that tools/cosmodata/build_starfield.py writes. Parsing
    /// lives in Core rather than the renderer because it is pure arithmetic over bytes, so it
    /// is tested headlessly in under a second against the file that actually ships, instead of
    /// needing an editor and a play-mode run to notice that a field moved.
    ///
    /// **Sorted brightest first**, which is the property the whole design rests on: the render
    /// budget cannot afford every star, the ones worth drawing are the bright ones, and a
    /// prefix turns "which stars do I draw" from a search into a length. NaN magnitudes sort
    /// last, so a prefix never contains one.
    ///
    /// **The distances are the point.** The previous renderer's own comment said "distance is
    /// deliberately discarded here" and painted every star on a 900-unit shell. With distances,
    /// walking toward Sirius makes Sirius leave the field behind it, and the constellation
    /// figures shear into their true depths - which is the entire argument for a headset.
    /// </summary>
    public sealed class StarCatalog
    {
        public const string ContainerMagic = "CSS2";
        public const string SpectralClasses = "OBAFGKMLTYCSW";

        private readonly CatalogStar[] _stars;

        public IReadOnlyList<CatalogStar> Stars => _stars;
        public int Count => _stars.Length;

        /// <summary>Rows in the file, which may exceed <see cref="Count"/> after a prefix read.</summary>
        public int TotalRows { get; }

        /// <summary>The container meta: provenance, sources, counts, assertions and limits.</summary>
        public CsData Data { get; }

        /// <summary>
        /// The faintest magnitude the file holds. The observed-sky limit slider cannot reveal
        /// anything past this, and the panel says so rather than offering a setting that does
        /// nothing - which is the defect in the Atlas, whose data stops at 8.54 and whose
        /// slider goes to 14.
        /// </summary>
        public double FaintestMagnitude { get; }

        public string Attribution => Data?.Attribution() ?? "";
        public string LicenceProfile => Data?.LicenceProfile ?? "unknown";

        private StarCatalog(CatalogStar[] stars, int totalRows, CsData data, double faintest)
        {
            _stars = stars;
            TotalRows = totalRows;
            Data = data;
            FaintestMagnitude = faintest;
        }

        /// <summary>
        /// Parse the catalogue. <paramref name="maximumStars"/> reads only the brightest N,
        /// which is the normal case: the file holds everything the source had so it is there
        /// when wanted, and a headset draws a few thousand of them.
        /// </summary>
        public static StarCatalog Parse(byte[] data, int maximumStars = int.MaxValue)
        {
            CsData container = CsData.Parse(data, ContainerMagic);

            int rows = container.Rows;
            int wanted = Math.Min(rows, Math.Max(0, maximumStars));

            float[] pos = container.F32("pos", wanted);
            float[] vmag = container.F32("vmag", wanted);
            float[] teff = container.F32("teff", wanted);
            uint[] flags = container.U32("flags", wanted);

            float[] vel = container.Has("vel") ? container.F32("vel", wanted) : null;
            ushort[] av = container.Has("av") ? container.U16("av", wanted) : null;
            byte[] bv = container.Has("bv") ? container.U8("bv", wanted) : null;
            byte[] spec = container.Has("spec") ? container.U8("spec", wanted) : null;
            uint[] hip = container.Has("hip") ? container.U32("hip", wanted) : null;
            uint[] hd = container.Has("hd") ? container.U32("hd", wanted) : null;

            var stars = new CatalogStar[wanted];
            for (int i = 0; i < wanted; i++)
            {
                stars[i] = new CatalogStar(
                    new Double3(pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]),
                    vel != null
                        ? new Double3(vel[i * 3], vel[i * 3 + 1], vel[i * 3 + 2])
                        : default,
                    vmag[i],
                    teff[i],
                    bv != null && bv[i] != 0 ? container.Decode("bv", bv[i]) : double.NaN,
                    av != null ? container.Decode("av", av[i]) : 0.0,
                    hip != null ? hip[i] : 0u,
                    hd != null ? hd[i] : 0u,
                    spec != null ? spec[i] : (byte)255,
                    (StarFlags)flags[i]);
            }

            double faintest = container.Meta["limits"]["faintestVmag"].AsDouble(double.NaN);
            if (double.IsNaN(faintest) && wanted > 0) faintest = stars[wanted - 1].ApparentMagnitude;

            return new StarCatalog(stars, rows, container, faintest);
        }

        /// <summary>Which measurement the distance came from, decoded from flag bits 8-10.</summary>
        public static DistanceSource DistanceSourceOf(in CatalogStar star) =>
            (DistanceSource)(((uint)star.Flags >> 8) & 0x7);

        /// <summary>How many of the loaded stars are at or brighter than a magnitude.</summary>
        public int CountBrighterThan(double magnitude)
        {
            // Binary search rather than a scan: the array is sorted by magnitude, which is the
            // whole reason the importer sorts it.
            int low = 0, high = _stars.Length;
            while (low < high)
            {
                int middle = (low + high) / 2;
                double m = _stars[middle].ApparentMagnitude;
                if (!double.IsNaN(m) && m <= magnitude) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        /// <summary>
        /// How many stars a viewer at <paramref name="vantageParsecs"/> could see down to
        /// <paramref name="limitMagnitude"/>. This is a real count, not a prefix: from ten
        /// parsecs away the naked-eye sky is a different set of stars, which is the whole
        /// point of carrying distances.
        /// </summary>
        public int CountVisibleFrom(Double3 vantageParsecs, double limitMagnitude)
        {
            int visible = 0;
            for (int i = 0; i < _stars.Length; i++)
            {
                if (ObservedMagnitude(_stars[i], vantageParsecs) <= limitMagnitude) visible++;
            }
            return visible;
        }

        /// <summary>
        /// Apparent magnitude of a star seen from an arbitrary vantage, in parsecs, ICRF.
        /// NaN when the star has no distance: from anywhere but the Sun, a direction-only star
        /// has no defined brightness, and pretending otherwise is what puts a whole shell of
        /// fake stars at the sky radius.
        /// </summary>
        public static double ObservedMagnitude(in CatalogStar star, Double3 vantageParsecs)
        {
            double absolute = star.AbsoluteMagnitude;
            if (double.IsNaN(absolute)) return double.NaN;
            double distance = (star.PositionParsecs - vantageParsecs).Magnitude;
            return Photometry.ApparentMagnitude(absolute, Math.Max(distance, 0.02),
                star.ExtinctionAv);
        }

        /// <summary>Row index of a star by Hipparcos number, or -1.</summary>
        public int IndexOfHip(uint hip)
        {
            if (hip == 0) return -1;
            for (int i = 0; i < _stars.Length; i++)
                if (_stars[i].Hip == hip) return i;
            return -1;
        }

        /// <summary>
        /// Colour of a star from its temperature, as linear RGB normalised so the brightest
        /// channel is 1. Shares <see cref="PlanetGenerator.BlackBodyColor"/> so a catalogue
        /// star and a generated one cannot end up on different colour curves.
        /// </summary>
        public static Double3 ColorFor(in CatalogStar star) =>
            PlanetGenerator.BlackBodyColor(star.TemperatureK);

        /// <summary>Provenance of one field on one star, from the file's own declaration.</summary>
        public FieldProvenance ProvenanceFor(string field, in CatalogStar star) =>
            Data.ProvenanceFor(field, (uint)star.Flags);

        public override string ToString() =>
            $"{Count} of {TotalRows} stars, brightest mag " +
            (Count > 0 ? _stars[0].ApparentMagnitude.ToString("F2") : "n/a") +
            $", faintest {FaintestMagnitude:F2}";
    }
}
