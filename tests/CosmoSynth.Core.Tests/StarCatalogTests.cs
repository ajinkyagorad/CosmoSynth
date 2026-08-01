using System;
using System.IO;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The shipped star catalogue, checked against the sky.
    ///
    /// The interesting assertions here are not about parsing — they are about whether the
    /// positions and magnitudes are the real ones. A star field that loads without error and
    /// puts Sirius in the wrong place is exactly the failure this project keeps meeting: a
    /// plausible artefact that nobody can tell from the truth by looking at it.
    /// </summary>
    public static class StarCatalogTests
    {
        private static byte[] _data;

        private static byte[] Data
        {
            get
            {
                if (_data == null)
                {
                    string path = FindRepoFile(Path.Combine(
                        "unity", "Assets", "StreamingAssets", "CosmoSynth", "sky", "stars.csstars"));
                    if (path == null)
                        throw new FileNotFoundException(
                            "stars.csstars not found. Build it with " +
                            "'python3 tools/cosmodata/build_starfield.py'.");
                    _data = File.ReadAllBytes(path);
                }
                return _data;
            }
        }

        private static string FindRepoFile(string relative)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 10 && directory != null; depth++)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            return null;
        }

        [Test("the shipped star catalogue parses and is sorted brightest first")]
        public static void CatalogParsesAndIsSorted()
        {
            StarCatalog catalog = StarCatalog.Parse(Data);

            Assert.True(catalog.Count > 100000,
                $"expected a six-figure catalogue, got {catalog.Count}");

            double previous = double.NegativeInfinity;
            foreach (CatalogStar star in catalog.Stars)
            {
                Assert.True(star.ApparentMagnitude >= previous - 1e-4,
                    $"catalogue is not sorted by magnitude at mag {star.ApparentMagnitude}");
                previous = star.ApparentMagnitude;

                Assert.True(!double.IsNaN(star.PositionParsecs.x) &&
                            !double.IsInfinity(star.PositionParsecs.x),
                    "a star has a non-finite position");
                Assert.True(star.TemperatureK > 500.0 && star.TemperatureK < 100000.0,
                    $"temperature {star.TemperatureK:F0} K is outside any stellar range");
            }
        }

        [Test("the brightest stars are the real brightest stars, at their real distances")]
        public static void BrightStarsMatchThePublishedSky()
        {
            // Read the whole file, not a prefix. Counting naked-eye stars in a catalogue capped
            // at N can never return more than N, so a prefix read would be measuring the cap
            // rather than the sky — which is exactly what this test did on its first run, and it
            // reported 4000 because it had been given 4000.
            StarCatalog catalog = StarCatalog.Parse(Data);

            // Sirius is the brightest star in Earth's sky and is 2.64 pc away. If the importer
            // mangled a unit, a column or the sort, this is what notices.
            CatalogStar brightest = catalog.Stars[0];
            Assert.Equal(-1.44, brightest.ApparentMagnitude, 0.05, "brightest star's magnitude");
            Assert.True(brightest.DistanceParsecs.HasValue, "Sirius has a measured distance");
            Assert.Equal(2.64, brightest.DistanceParsecs.Value, 0.05, "Sirius's distance in parsecs");

            // Naked-eye visibility is a fact about the sky, not about this file: roughly five
            // thousand stars reach magnitude 6 from a dark site.
            int nakedEye = catalog.CountBrighterThan(6.0);
            Assert.True(nakedEye > 4000 && nakedEye < 7000,
                $"expected ~5000 naked-eye stars, found {nakedEye}");

            // The first magnitude bands, against the real sky. Two stars beat magnitude 0;
            // about fifteen beat magnitude 1.
            Assert.True(catalog.CountBrighterThan(0.0) >= 2 && catalog.CountBrighterThan(0.0) <= 6,
                $"expected a handful of stars brighter than mag 0, found {catalog.CountBrighterThan(0.0)}");
            Assert.True(catalog.CountBrighterThan(1.0) >= 12 && catalog.CountBrighterThan(1.0) <= 22,
                $"expected ~15 stars brighter than mag 1, found {catalog.CountBrighterThan(1.0)}");
        }

        [Test("a star with no measured parallax reports no distance rather than a fake one")]
        public static void UnknownDistanceIsNotInvented()
        {
            StarCatalog catalog = StarCatalog.Parse(Data);

            int unknown = 0;
            foreach (CatalogStar star in catalog.Stars)
            {
                if ((star.Flags & StarFlags.DistanceUnknown) == 0) continue;
                unknown++;

                Assert.False(star.DistanceParsecs.HasValue,
                    "a star flagged as having an unknown distance still reported one");

                // Its direction is still exact — that is what was measured, and it is why these
                // are kept rather than dropped.
                Assert.Equal(1.0, star.Direction.Magnitude, 1e-9,
                    "an unknown-distance star must still have a usable direction");
            }

            Assert.True(unknown > 1000,
                $"expected thousands of unknown-parallax stars in HYG, found {unknown}");
        }

        [Test("reading only the brightest N gives the same stars as reading all of them")]
        public static void PrefixReadMatchesFullRead()
        {
            StarCatalog all = StarCatalog.Parse(Data);
            StarCatalog prefix = StarCatalog.Parse(Data, 500);

            Assert.True(prefix.Count == 500, $"expected 500 stars, got {prefix.Count}");
            for (int i = 0; i < prefix.Count; i += 37)
            {
                Assert.Equal(all.Stars[i].ApparentMagnitude, prefix.Stars[i].ApparentMagnitude,
                    1e-9, $"magnitude at index {i}");
                Assert.VectorClose(all.Stars[i].PositionParsecs, prefix.Stars[i].PositionParsecs,
                    1e-9, $"position at index {i}");
            }
        }

        [Test("a truncated or foreign star catalogue is refused, not half-read")]
        public static void MalformedCatalogIsRefused()
        {
            Assert.Throws<FormatException>(
                () => StarCatalog.Parse(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
                "a file too short to hold a header");

            var wrongMagic = new byte[StarCatalog.HeaderBytes + StarCatalog.RecordBytes];
            wrongMagic[0] = (byte)'<';
            wrongMagic[1] = (byte)'h';
            Assert.Throws<FormatException>(() => StarCatalog.Parse(wrongMagic),
                "an HTML error page where a catalogue should be");

            // Right magic, wrong length: the case a partial download produces.
            var truncated = new byte[StarCatalog.HeaderBytes + StarCatalog.RecordBytes];
            truncated[0] = (byte)'C'; truncated[1] = (byte)'S';
            truncated[2] = (byte)'S'; truncated[3] = (byte)'1';
            BitConverter.GetBytes(9999u).CopyTo(truncated, 8);
            Assert.Throws<FormatException>(() => StarCatalog.Parse(truncated),
                "a catalogue whose header disagrees with its length");
        }
    }
}
