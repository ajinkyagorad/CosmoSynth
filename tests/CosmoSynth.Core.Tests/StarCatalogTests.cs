using System;
using System.Collections.Generic;
using System.IO;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The shipped star catalogue, checked against the sky.
    ///
    /// The interesting assertions here are not about parsing - they are about whether the
    /// positions and magnitudes are the real ones. A star field that loads without error and
    /// puts Sirius in the wrong place is exactly the failure this project keeps meeting: a
    /// plausible artefact that nobody can tell from the truth by looking at it.
    ///
    /// Every tolerance below is justified in its own assertion message. A tolerance loose
    /// enough to pass is not a test: this project once asserted Jupiter's equatorial radius
    /// and passed for months on a value 2.2% off.
    /// </summary>
    public static class StarCatalogTests
    {
        private static byte[] _data;
        private static StarCatalog _catalog;

        private static byte[] Data
        {
            get
            {
                if (_data == null)
                {
                    string path = TestFiles.Find(Path.Combine(
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

        private static StarCatalog Catalog => _catalog ?? (_catalog = StarCatalog.Parse(Data));

        /// <summary>Look a star up by its proper name through the shipped name index.</summary>
        private static int IndexOfName(string name)
        {
            string path = TestFiles.Find(Path.Combine(
                "unity", "Assets", "StreamingAssets", "CosmoSynth", "sky", "stars_named.json"));
            Assert.True(path != null, "stars_named.json is missing; rebuild the star layer");
            JsonValue root = JsonValue.Parse(File.ReadAllText(path));
            foreach (JsonValue star in root["stars"])
            {
                if (string.Equals(star["name"].AsString(""), name, StringComparison.OrdinalIgnoreCase))
                    return star["index"].AsInt(-1);
            }
            return -1;
        }

        [Test("the shipped star catalogue parses and is sorted brightest first")]
        public static void CatalogParsesAndIsSorted()
        {
            StarCatalog catalog = Catalog;

            Assert.True(catalog.Count > 5000,
                $"expected at least the naked-eye sky, got {catalog.Count} stars");

            double previous = double.NegativeInfinity;
            bool seenNaN = false;
            foreach (CatalogStar star in catalog.Stars)
            {
                double m = star.ApparentMagnitude;
                if (double.IsNaN(m)) { seenNaN = true; continue; }
                Assert.False(seenNaN,
                    "a star with a magnitude follows one without: NaN must sort last, or a " +
                    "prefix read stops being the brightest N");
                Assert.True(m >= previous - 1e-4,
                    $"catalogue is not sorted by magnitude at mag {m}");
                previous = m;

                Assert.True(!double.IsNaN(star.PositionParsecs.x) &&
                            !double.IsInfinity(star.PositionParsecs.x),
                    "a star has a non-finite position");
                Assert.True(star.TemperatureK > 500.0 && star.TemperatureK < 100000.0,
                    $"temperature {star.TemperatureK:F0} K is outside any stellar range");
                Assert.True(star.VelocityKmPerSecond.Magnitude < 1000.0,
                    $"space velocity {star.VelocityKmPerSecond.Magnitude:F0} km/s is an " +
                    "astrometric error, not a star: disc and halo stars stay under ~600 km/s");
            }
        }

        [Test("the brightest stars are the real brightest stars, at their real distances")]
        public static void BrightStarsMatchThePublishedSky()
        {
            // Read the whole file, not a prefix. Counting naked-eye stars in a catalogue capped
            // at N can never return more than N, so a prefix read would be measuring the cap
            // rather than the sky - which is exactly what this test did on its first run, and
            // it reported 4000 because it had been given 4000.
            StarCatalog catalog = Catalog;

            // Sirius. V -1.44 in HYG, -1.46 in Yale; the published literature value is -1.46.
            // 0.05 covers the spread between catalogues and nothing more.
            int sirius = IndexOfName("Sirius");
            Assert.True(sirius >= 0, "Sirius is not in the shipped name index");
            CatalogStar star = catalog.Stars[sirius];
            Assert.Equal(-1.44, star.ApparentMagnitude, 0.05, "Sirius V");
            Assert.True(star.DistanceParsecs.HasValue,
                "Sirius has the best-measured parallax in the sky; a catalogue without a " +
                "distance for it is not a 3-D catalogue");
            // 2.637 pc: Hipparcos 2007 gives 379.21 mas +- 1.58, so 3 sigma is 0.011 pc. The
            // ground-based parallax in the clean build is 375 mas, which is 2.667 pc, so the
            // tolerance that covers both source paths is 0.05 pc and is stated as such.
            Assert.Equal(2.637, star.DistanceParsecs.Value, 0.05, "Sirius distance");

            // Vega. 7.68 pc from Hipparcos; the Yale ground-based parallax of 123 mas gives
            // 8.13 pc, which is a 5.9% error and the honest cost of the clean path.
            int vega = IndexOfName("Vega");
            Assert.True(vega >= 0, "Vega is not in the shipped name index");
            star = catalog.Stars[vega];
            Assert.Equal(0.03, star.ApparentMagnitude, 0.05, "Vega V");
            Assert.True(star.DistanceParsecs.HasValue, "Vega has no distance");
            Assert.Equal(7.68, star.DistanceParsecs.Value, 0.60, "Vega distance");

            // Arcturus. 11.26 pc; Yale's 90 mas gives 11.11 pc.
            int arcturus = IndexOfName("Arcturus");
            Assert.True(arcturus >= 0, "Arcturus is not in the shipped name index");
            star = catalog.Stars[arcturus];
            Assert.Equal(-0.05, star.ApparentMagnitude, 0.05, "Arcturus V");
            Assert.True(star.DistanceParsecs.HasValue, "Arcturus has no distance");
            Assert.Equal(11.26, star.DistanceParsecs.Value, 0.60, "Arcturus distance");

            // The naked-eye sky. About 5,000 stars reach V 6.0 over the whole celestial
            // sphere. 10% allows the real difference between HYG and Yale photometry; it does
            // not allow a catalogue that has lost half the sky or gained a duplicate of it.
            int nakedEye = catalog.CountBrighterThan(6.0);
            Assert.True(nakedEye > 4500 && nakedEye < 5500,
                $"{nakedEye} stars at V <= 6.0; the real sky has about 5,000");
        }

        [Test("stars sit at measured distances, not on a shell")]
        public static void DistancesAreRealAndVaried()
        {
            StarCatalog catalog = Catalog;

            int withDistance = 0, unknown = 0;
            double nearest = double.MaxValue, farthest = 0.0;
            foreach (CatalogStar star in catalog.Stars)
            {
                double? d = star.DistanceParsecs;
                if (!d.HasValue) { unknown++; continue; }
                withDistance++;
                if (d.Value < nearest) nearest = d.Value;
                if (d.Value > farthest) farthest = d.Value;
            }

            Assert.True(withDistance >= 200,
                $"only {withDistance} stars have a distance: this is a dome, not a volume");

            // The whole argument for a headset: the field must have depth, not one radius.
            Assert.True(farthest / Math.Max(nearest, 1e-6) > 100.0,
                $"distances span only {nearest:F2} to {farthest:F2} pc, which is a shell");

            // Proxima is the nearest star and it is 1.30 pc away. Anything nearer is a bug.
            Assert.True(nearest > 1.2,
                $"nearest star is {nearest:F3} pc; nothing is closer than Proxima at 1.30 pc");

            // Every distance-unknown star must still carry an exact direction, because that
            // is what was actually measured about it.
            foreach (CatalogStar star in catalog.Stars)
            {
                if (star.DistanceParsecs.HasValue) continue;
                Assert.Equal(1.0, star.Direction.Magnitude, 1e-4,
                    "a distance-unknown star must still have a usable direction");
            }
        }

        [Test("a prefix read is the brightest N and nothing else")]
        public static void PrefixReadMatchesTheFullRead()
        {
            StarCatalog full = Catalog;
            StarCatalog prefix = StarCatalog.Parse(Data, 1000);

            Assert.True(prefix.Count == 1000, $"asked for 1000 stars, got {prefix.Count}");
            Assert.True(prefix.TotalRows == full.Count,
                "a prefix read must still report how many rows the file holds");

            for (int i = 0; i < prefix.Count; i++)
            {
                Assert.Equal(full.Stars[i].ApparentMagnitude, prefix.Stars[i].ApparentMagnitude,
                    0.0, $"prefix star {i} magnitude");
                Assert.Equal(full.Stars[i].PositionParsecs.x, prefix.Stars[i].PositionParsecs.x,
                    0.0, $"prefix star {i} position");
                Assert.True(full.Stars[i].Flags == prefix.Stars[i].Flags,
                    $"prefix star {i} flags");
            }
        }

        [Test("the file declares its own magnitude floor, its licence and its attribution")]
        public static void FileIsSelfDescribing()
        {
            StarCatalog catalog = Catalog;

            // The observed-sky limit slider cannot reveal anything past this, and the panel
            // must say so rather than offering a setting that does nothing - the defect in the
            // Atlas, whose data stops at 8.54 and whose slider runs to 14.
            Assert.True(catalog.FaintestMagnitude > 5.0 && catalog.FaintestMagnitude < 25.0,
                $"faintest magnitude {catalog.FaintestMagnitude} is not a plausible floor");
            Assert.True(catalog.FaintestMagnitude >= catalog.Stars[0].ApparentMagnitude,
                "the faintest magnitude must not be brighter than the brightest star");

            Assert.True(!string.IsNullOrEmpty(catalog.Attribution),
                "the catalogue ships no attribution string, and every source it can be built " +
                "from requires one");
            Assert.True(catalog.LicenceProfile == "sideload-nc" ||
                        catalog.LicenceProfile == "public-nc" ||
                        catalog.LicenceProfile == "commercial",
                $"unknown licence profile '{catalog.LicenceProfile}'");
        }

        [Test("provenance says which values are measured and which are not")]
        public static void ProvenanceIsHonest()
        {
            StarCatalog catalog = Catalog;
            CatalogStar sirius = catalog.Stars[IndexOfName("Sirius")];

            FieldProvenance position = catalog.ProvenanceFor("pos", sirius);
            Assert.True(position.Class == ProvenanceClass.Measured,
                $"Sirius's position should be Measured, not {position.Class}");

            FieldProvenance temperature = catalog.ProvenanceFor("teff", sirius);
            Assert.True(temperature.Class == ProvenanceClass.Inferred,
                "temperature comes from a colour index through Ballesteros' formula: it is " +
                $"Inferred, never Measured, and this file says {temperature.Class}");

            // The rule this lane exists to keep: a zero extinction is the absence of a
            // measurement until a dust map fills it, and the file must say so rather than
            // letting a renderer read 0.0 as "clear sightline".
            int flagged = 0, zeroed = 0;
            foreach (CatalogStar star in catalog.Stars)
            {
                if ((star.Flags & StarFlags.ExtinctionUnknown) != 0)
                {
                    flagged++;
                    if (star.ExtinctionAv == 0.0) zeroed++;
                }
            }
            Assert.True(flagged == zeroed,
                $"{flagged - zeroed} stars are flagged ExtinctionUnknown but carry a non-zero " +
                "A_V, which is a contradiction");
            if (flagged > 0)
            {
                FieldProvenance extinction =
                    catalog.Data.ProvenanceFor("av", (uint)StarFlags.ExtinctionUnknown);
                Assert.True(extinction.Class == ProvenanceClass.Unknown,
                    $"an unmeasured A_V must be Unknown, not {extinction.Class}");
            }
        }

        [Test("a CSS1 file is refused by name, with the command that fixes it")]
        public static void OldFormatIsRefused()
        {
            var css1 = new byte[16 + 24];
            css1[0] = (byte)'C'; css1[1] = (byte)'S'; css1[2] = (byte)'S'; css1[3] = (byte)'1';
            BitConverter.GetBytes(1u).CopyTo(css1, 4);
            BitConverter.GetBytes(1u).CopyTo(css1, 8);

            try
            {
                StarCatalog.Parse(css1);
                throw new AssertionException("a CSS1 file should not parse as CSS2");
            }
            catch (FormatException error)
            {
                Assert.True(error.Message.Contains("build_starfield.py"),
                    "the refusal must name the command that rebuilds the file; it said: " +
                    error.Message);
            }
        }
    }
}
