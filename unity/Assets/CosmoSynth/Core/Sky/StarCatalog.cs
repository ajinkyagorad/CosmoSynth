using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>One catalogued star: where it is, how bright it looks, and what colour it is.</summary>
    public readonly struct CatalogStar
    {
        /// <summary>Position in ICRF equatorial cartesian coordinates, parsecs.</summary>
        public Double3 PositionParsecs { get; }

        /// <summary>Apparent visual magnitude. Smaller is brighter; Sirius is -1.44.</summary>
        public double ApparentMagnitude { get; }

        /// <summary>Effective temperature, kelvin. Inferred, never measured — see Flags.</summary>
        public double TemperatureK { get; }

        public StarFlags Flags { get; }

        public CatalogStar(Double3 positionParsecs, double apparentMagnitude, double temperatureK,
            StarFlags flags)
        {
            PositionParsecs = positionParsecs;
            ApparentMagnitude = apparentMagnitude;
            TemperatureK = temperatureK;
            Flags = flags;
        }

        /// <summary>
        /// Unit vector toward the star in ICRF. Always meaningful, including for the ten
        /// thousand stars whose distance is not known — direction is what was measured.
        /// </summary>
        public Double3 Direction => PositionParsecs.Normalized;

        /// <summary>Distance in parsecs, or null when the parallax was unusable.</summary>
        public double? DistanceParsecs =>
            (Flags & StarFlags.DistanceUnknown) != 0 ? (double?)null : PositionParsecs.Magnitude;

        public bool HasProperName => (Flags & StarFlags.HasProperName) != 0;

        public override string ToString() =>
            $"mag {ApparentMagnitude:F2}, {TemperatureK:F0} K, " +
            (DistanceParsecs.HasValue ? $"{DistanceParsecs.Value:F2} pc" : "distance unknown");
    }

    [Flags]
    public enum StarFlags
    {
        None = 0,

        /// <summary>
        /// The parallax was unusable, so only the direction is known. The importer keeps these
        /// and flags them rather than dropping a tenth of the sky or inventing a distance.
        /// </summary>
        DistanceUnknown = 1 << 0,

        /// <summary>
        /// No B-V colour index was published, so the temperature comes from the spectral class
        /// alone. That is a weaker claim than a colour measurement and is not the same thing.
        /// </summary>
        TemperatureFromSpectrum = 1 << 1,

        HasProperName = 1 << 2,
    }

    /// <summary>
    /// The shipped star catalogue: 119 613 real stars, read from the flat binary that
    /// tools/cosmodata/build_starfield.py writes.
    ///
    /// Parsing lives in Core rather than in the renderer for the usual reason — it is pure
    /// arithmetic over bytes, so it is tested headlessly in under a second against the file
    /// that actually ships, instead of needing an editor and a play-mode run to notice that a
    /// field moved.
    ///
    /// **Sorted brightest first**, which is the property the whole design rests on. The render
    /// budget cannot afford every star, and the ones worth drawing are the bright ones, so the
    /// runtime takes a prefix. That turns "which stars do I draw" from a search into a length.
    ///
    /// Layout, little-endian:
    ///     magic     4      "CSS1"
    ///     version   u32
    ///     count     u32
    ///     flags     u32    reserved
    ///     ---
    ///     count x (position 3xf32 parsecs, magnitude f32, temperature f32, flags u32)
    /// </summary>
    public sealed class StarCatalog
    {
        public const int HeaderBytes = 16;
        public const int RecordBytes = 24;

        private static readonly byte[] Magic = { (byte)'C', (byte)'S', (byte)'S', (byte)'1' };

        private readonly CatalogStar[] _stars;

        public IReadOnlyList<CatalogStar> Stars => _stars;
        public int Count => _stars.Length;

        private StarCatalog(CatalogStar[] stars) => _stars = stars;

        /// <summary>
        /// Parse the catalogue. <paramref name="maximumStars"/> reads only the brightest N,
        /// which is the normal case: the file holds everything down to magnitude 13 so it is
        /// there when wanted, and a headset draws a few thousand of them.
        /// </summary>
        public static StarCatalog Parse(byte[] data, int maximumStars = int.MaxValue)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < HeaderBytes)
                throw new FormatException(
                    $"A star catalogue is at least {HeaderBytes} bytes; got {data.Length}.");

            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                    throw new FormatException(
                        "Star catalogue does not begin with 'CSS1'. Rebuild it with " +
                        "tools/cosmodata/build_starfield.py.");
            }

            int count = (int)BitConverter.ToUInt32(data, 8);
            long expected = HeaderBytes + (long)count * RecordBytes;
            if (data.Length != expected)
            {
                throw new FormatException(
                    $"Star catalogue declares {count} stars, needing {expected} bytes, but the " +
                    $"file is {data.Length}. It is truncated or was written by another version.");
            }

            int wanted = System.Math.Min(count, System.Math.Max(0, maximumStars));
            var stars = new CatalogStar[wanted];

            for (int i = 0; i < wanted; i++)
            {
                int at = HeaderBytes + i * RecordBytes;
                stars[i] = new CatalogStar(
                    new Double3(BitConverter.ToSingle(data, at),
                                BitConverter.ToSingle(data, at + 4),
                                BitConverter.ToSingle(data, at + 8)),
                    BitConverter.ToSingle(data, at + 12),
                    BitConverter.ToSingle(data, at + 16),
                    (StarFlags)BitConverter.ToUInt32(data, at + 20));
            }

            return new StarCatalog(stars);
        }

        /// <summary>How many of the loaded stars are at or brighter than a magnitude.</summary>
        public int CountBrighterThan(double magnitude)
        {
            // Binary search rather than a scan: the array is sorted by magnitude, which is the
            // whole reason the importer sorts it.
            int low = 0, high = _stars.Length;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (_stars[middle].ApparentMagnitude <= magnitude) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        /// <summary>
        /// Colour of a star from its temperature, as linear RGB normalised so the brightest
        /// channel is 1. Shares <see cref="PlanetGenerator.BlackBodyColor"/> so a catalogue
        /// star and a generated one cannot end up on different colour curves.
        /// </summary>
        public static Double3 ColorFor(in CatalogStar star) =>
            PlanetGenerator.BlackBodyColor(star.TemperatureK);

        public override string ToString() => $"{Count} stars, brightest mag " +
            (Count > 0 ? _stars[0].ApparentMagnitude.ToString("F2") : "n/a");
    }
}
