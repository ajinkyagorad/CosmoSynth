using System;
using System.Collections.Generic;
using System.IO;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// Photometry: the arithmetic that decides whether the sky looks like the sky.
    ///
    /// These are the Atlas's own formulae, so the checks are against published magnitudes and
    /// against the Atlas's constants, not against whatever this implementation happens to do.
    /// </summary>
    public static class PhotometryTests
    {
        [Test("the distance modulus reproduces published absolute magnitudes")]
        public static void DistanceModulusIsRight()
        {
            // Sirius: V -1.44 at 2.637 pc gives M_V +1.45. The published value is +1.45.
            Assert.Equal(1.45, Photometry.AbsoluteMagnitude(-1.44, 2.637), 0.01,
                "Sirius absolute magnitude");

            // Vega: V 0.03 at 7.68 pc gives M_V +0.60; the published value is +0.58.
            Assert.Equal(0.60, Photometry.AbsoluteMagnitude(0.03, 7.68), 0.02,
                "Vega absolute magnitude");

            // Rigel: V 0.13 at 264.6 pc gives M_V -6.98, a blue supergiant.
            Assert.Equal(-6.98, Photometry.AbsoluteMagnitude(0.13, 264.6), 0.02,
                "Rigel absolute magnitude");

            // The Sun: M 4.83 at 10 pc is by definition V 4.83, and at 1 AU it is -26.75.
            Assert.Equal(4.83, Photometry.ApparentMagnitude(Photometry.SunAbsoluteMagnitude, 10.0),
                1e-9, "the Sun at 10 pc");
            const double AstronomicalUnitInParsecs = 4.8481368e-6;
            Assert.Equal(-26.75,
                Photometry.ApparentMagnitude(Photometry.SunAbsoluteMagnitude,
                    AstronomicalUnitInParsecs), 0.02,
                "the Sun seen from Earth: the single best-known apparent magnitude there is");

            // Round trip through both directions at an awkward distance.
            double m = Photometry.ApparentMagnitude(3.21, 137.4);
            Assert.Equal(3.21, Photometry.AbsoluteMagnitude(m, 137.4), 1e-9,
                "apparent and absolute magnitude must invert exactly");
        }

        [Test("extinction is applied once, and it makes a star fainter")]
        public static void ExtinctionDimsInTheRightDirection()
        {
            double clear = Photometry.ApparentMagnitude(-5.0, 400.0, 0.0);
            double dusty = Photometry.ApparentMagnitude(-5.0, 400.0, 1.5);
            Assert.Equal(clear + 1.5, dusty, 1e-12,
                "A_V of 1.5 must make the star exactly 1.5 magnitudes fainter, no more");
            Assert.True(dusty > clear, "extinction must dim, not brighten");
        }

        [Test("the size and eye-response curves are the Atlas's, to its published constants")]
        public static void CompressionCurvesMatchTheAtlas()
        {
            // psize = L^0.28, and L = 10^(-0.4 M). A magnitude-zero star has L = 1, psize = 1.
            Assert.Equal(1.0, Photometry.SizeAttribute(0.0), 1e-12, "M=0 gives a size of 1");

            // The Sun at M 4.83: L = 0.0117, psize = 0.276. This is the Atlas's line 350.
            Assert.Equal(0.0117, Photometry.Luminosity(Photometry.SunAbsoluteMagnitude), 1e-4,
                "the Sun's luminosity relative to a magnitude-zero star");
            // L = 10^(-0.4 x 4.83) = 0.0116950, and 0.0116950^0.28 = 0.2877663.
            Assert.Equal(0.2877663, Photometry.SizeAttribute(Photometry.SunAbsoluteMagnitude),
                1e-6, "the Sun's drawn-size attribute");

            // The Atlas's shader recovers L as psize^(1/0.28) = psize^3.5714286 (line 294).
            double size = Photometry.SizeAttribute(-4.2);
            Assert.Equal(Photometry.Luminosity(-4.2),
                Photometry.LuminosityFromSizeAttribute(size), 1e-6,
                "the shader's inverse of the size attribute must round-trip");

            // Compression is the whole point. Rigel at M_V -6.98 is 619 times a
            // magnitude-zero star and about 53,000 times the Sun, and it must not be drawn
            // 53,000 times the size: L^0.28 puts it at 6.05 against the Sun's 0.2878, a ratio
            // of 21. A renderer that ever shows a 53,000x ratio has lost the exponent.
            double rigel = Photometry.SizeAttribute(-6.98);
            Assert.Equal(619.441, Photometry.Luminosity(-6.98), 0.01, "Rigel's luminosity");
            Assert.Equal(6.0501, rigel, 1e-3, "Rigel's size attribute");
            double sun = Photometry.SizeAttribute(Photometry.SunAbsoluteMagnitude);
            Assert.Equal(52967.0, Photometry.Luminosity(-6.98) / Photometry.Luminosity(4.83),
                1.0, "Rigel is this many times the Sun's luminosity");
            Assert.Equal(21.02, rigel / sun, 0.01,
                "and L^0.28 compresses that to a 21x ratio in drawn size");

            // b = 1 exactly at the limit (line 297), and one magnitude brighter is 2.512.
            Assert.Equal(1.0, Photometry.RelativeBrightness(6.5, 6.5), 1e-12,
                "relative brightness is 1.0 at the visibility limit");
            Assert.Equal(2.511886, Photometry.RelativeBrightness(5.5, 6.5), 1e-5,
                "one magnitude is a factor of 2.512 in flux");

            // s = 1.15 b^0.23 (line 298), and zero below the limit: stars must VANISH as the
            // limit comes down, not fade to an indistinguishable grey.
            Assert.Equal(1.15, Photometry.EyeResponse(1.0), 1e-12, "eye response at the limit");
            Assert.Equal(0.0, Photometry.EyeResponse(0.99), 0.0,
                "a star fainter than the limit must be drawn at size zero");
            Assert.Equal(1.15 * Math.Pow(100.0, 0.23), Photometry.EyeResponse(100.0), 1e-9,
                "eye response follows b^0.23");
        }

        [Test("the observed sky changes when the vantage does")]
        public static void ObservedSkyDependsOnWhereYouStand()
        {
            // Deneb: M_V about -8.4, roughly 802 pc away. From the Sun it is a 1.25-magnitude
            // star; from 10 pc away it would be the brightest thing in the sky by far.
            double fromSun = Photometry.ApparentMagnitude(-8.4, 802.0);
            Assert.Equal(1.12, fromSun, 0.20, "Deneb seen from the Sun");

            double fromClose = Photometry.ApparentMagnitude(-8.4, 10.0);
            Assert.Equal(-8.4, fromClose, 1e-9, "Deneb seen from 10 parsecs");
            Assert.True(fromClose < fromSun - 9.0,
                "moving to within 10 pc must brighten Deneb by more than nine magnitudes, " +
                "which is the entire reason the catalogue carries distances");

            // Below the limit from the Sun, above it from nearby.
            Assert.Equal(0.0, Photometry.ObservedSize(4.83, 802.0, 6.5), 0.0,
                "a Sun-like star at 800 pc is invisible to the naked eye");
            Assert.True(Photometry.ObservedSize(4.83, 10.0, 6.5) > 0.0,
                "a Sun-like star at 10 pc is naked-eye visible");
        }

        [Test("Ballesteros' temperatures land on the published spectral sequence")]
        public static void ColourIndexToTemperature()
        {
            // B-V 0.00 is A0V, about 9,600 K. B-V 0.65 is the Sun, about 5,770 K.
            Assert.Equal(9897.0, Photometry.TemperatureFromColourIndex(0.0), 300.0, "A0 (B-V 0)");
            Assert.Equal(5770.0, Photometry.TemperatureFromColourIndex(0.65), 200.0,
                "the Sun at B-V 0.65");
            // B-V 1.50 is about K5-M0, roughly 3,900 K; monotonic all the way down.
            Assert.True(Photometry.TemperatureFromColourIndex(1.50) <
                        Photometry.TemperatureFromColourIndex(0.65),
                "a redder star must be cooler");
        }

        [Test("the brightness assists are what the Atlas actually does, not what it claims")]
        public static void AssistsAreMeasuredNotAdvertised()
        {
            // The Atlas's README says the flux-true look at natural scales is untouched. Its
            // own farAssist is 1.17x at the default preset of 1e-3 m/pc. Keeping the formula
            // here, tested, is how the UI can say "x1.17" instead of "untouched".
            Assert.Equal(1.0, Photometry.SkyBoost(1.5e-3), 1e-12, "no sky boost at the hinge");
            Assert.True(Photometry.SkyBoost(1.5e-2) > 7.0, "zooming in must raise the sky boost");
            Assert.Equal(1.0, Photometry.FarAssist(1.5e-3), 1e-12, "no far assist at the hinge");
            // The Atlas opens at logScale = -3.1 (index.html line 828 and the uScale slider's
            // value attribute on line 88), so s = 7.9433e-4 and farAssist = (1.5e-3/s)^0.24
            // = 1.1648. Its README says the flux-true look at natural scales is untouched; it
            // is already 16% out at the scale the page loads with.
            Assert.Equal(1.1648, Photometry.FarAssist(Math.Pow(10.0, -3.1)), 1e-3,
                "the Atlas's default scale already applies a 1.16x assist");
            Assert.Equal(6.0, Photometry.FarAssist(1e-12), 1e-9, "the far assist is capped at 6");
        }
    }

    /// <summary>
    /// The 88 figures, against the catalogue they index into.
    ///
    /// The headline check is the one the brief asked for: a figure that references a star the
    /// catalogue does not contain must fail. That fault has no visual symptom - the line is
    /// drawn to whatever star happens to occupy that row - which is exactly why it is asserted
    /// rather than looked at.
    /// </summary>
    public static class ConstellationTests
    {
        private static Constellations _figures;

        private static Constellations Figures
        {
            get
            {
                if (_figures == null)
                {
                    string path = TestFiles.Find(Path.Combine(
                        "unity", "Assets", "StreamingAssets", "CosmoSynth", "sky",
                        "constellations.json"));
                    if (path == null)
                        throw new FileNotFoundException(
                            "constellations.json not found. Build it with " +
                            "'python3 tools/cosmodata/build_starfield.py'.");
                    _figures = Constellations.Parse(File.ReadAllText(path));
                }
                return _figures;
            }
        }

        private static StarCatalog Catalog
        {
            get
            {
                string path = TestFiles.Find(Path.Combine(
                    "unity", "Assets", "StreamingAssets", "CosmoSynth", "sky", "stars.csstars"));
                return StarCatalog.Parse(File.ReadAllBytes(path));
            }
        }

        [Test("no figure references a star the catalogue does not contain")]
        public static void EveryFigureIndexIsInTheCatalogue()
        {
            StarCatalog catalog = Catalog;
            string fault = Figures.Validate(catalog.Count);
            Assert.True(fault == null,
                "constellations.json disagrees with stars.csstars: " + fault +
                " Both files are written by the same run of build_starfield.py, so this means " +
                "one of them is stale.");
        }

        [Test("the figures are a real subset of the IAU sky, and say how much of it they are")]
        public static void FiguresAreHonestAboutCoverage()
        {
            Constellations figures = Figures;

            Assert.True(figures.FiguresInSource == 88,
                $"the modern sky culture has 88 figures; the file says {figures.FiguresInSource}");
            Assert.True(figures.SegmentsInSource == 695,
                $"the modern sky culture has 695 segments; the file says {figures.SegmentsInSource}");

            // Not a coverage target: a floor that catches the file becoming empty. What the
            // build actually resolved is recorded in docs/reports/data-stars.md and in the
            // file's own counts, and the UI reads ResolvedFraction rather than assuming 88.
            Assert.True(figures.Count > 0, "no constellation figures resolved at all");
            Assert.True(figures.SegmentCount > 0, "no constellation segments resolved at all");
            Assert.True(figures.SegmentCount <= figures.SegmentsInSource,
                $"{figures.SegmentCount} segments resolved from {figures.SegmentsInSource} in " +
                "the source, which is more than existed");
            Assert.True(figures.ResolvedFraction > 0.0 && figures.ResolvedFraction <= 1.0,
                $"resolved fraction {figures.ResolvedFraction} is not a fraction");
        }

        [Test("every figure has a label star and a name, and no segment is degenerate")]
        public static void FiguresAreWellFormed()
        {
            StarCatalog catalog = Catalog;
            var seen = new HashSet<string>();
            foreach (ConstellationFigure figure in Figures.Figures)
            {
                Assert.True(!string.IsNullOrEmpty(figure.Abbreviation) &&
                            figure.Abbreviation != "?",
                    "a figure has no IAU abbreviation");
                Assert.True(seen.Add(figure.Abbreviation),
                    $"{figure.Abbreviation} appears twice");
                Assert.True(!string.IsNullOrEmpty(figure.Name) && figure.Name != "?",
                    $"{figure.Abbreviation} has no name");
                Assert.True(figure.SegmentCount > 0,
                    $"{figure.Abbreviation} has no segments and should not be in the file");

                // The label goes on the figure's brightest star, and it must be one of the
                // stars the figure actually joins - not a centroid, which is not a star.
                bool labelIsAnEndpoint = false;
                foreach (int index in figure.StarIndices)
                    if (index == figure.LabelStarIndex) { labelIsAnEndpoint = true; break; }
                Assert.True(labelIsAnEndpoint,
                    $"{figure.Abbreviation} labels star {figure.LabelStarIndex}, which is not " +
                    "one of its own endpoints");

                double label = catalog.Stars[figure.LabelStarIndex].ApparentMagnitude;
                foreach (int index in figure.StarIndices)
                {
                    double m = catalog.Stars[index].ApparentMagnitude;
                    Assert.True(double.IsNaN(m) || m >= label - 1e-6,
                        $"{figure.Abbreviation} labels a star at V {label:F2} while it joins " +
                        $"a brighter one at V {m:F2}");
                }
            }
        }

        [Test("figure stars are bright, because constellation figures are naked-eye patterns")]
        public static void FigureStarsAreNakedEye()
        {
            StarCatalog catalog = Catalog;
            int faint = 0, total = 0;
            foreach (int index in Figures.ReferencedStars())
            {
                double m = catalog.Stars[index].ApparentMagnitude;
                total++;
                if (double.IsNaN(m) || m > 6.5) faint++;
            }
            Assert.True(total > 0, "no stars are referenced by any figure");
            // A bridge that resolved a HIP to the wrong row would land on a random star, and
            // the catalogue is overwhelmingly faint: 96% of the development build is fainter
            // than V 6.5. So a wrong bridge shows up here as faint figure stars, which is the
            // cheapest available check that the identifier resolution is real.
            Assert.True(faint * 20 <= total,
                $"{faint} of {total} figure stars are fainter than V 6.5 or have no magnitude; " +
                "constellation figures are naked-eye patterns, so the HIP bridge has " +
                "resolved some stars to the wrong catalogue rows");
        }

        [Test("the figure file carries the CC BY-SA attribution it obliges us to show")]
        public static void AttributionTravelsWithTheFile()
        {
            Assert.True(Figures.Attribution.Contains("Stellarium"),
                "the figure file must credit Stellarium's team: CC BY-SA 4.0 is not satisfied " +
                "by a comment in a build script");
            Assert.True(Figures.Attribution.Contains("CC BY-SA"),
                "the figure file must name its licence");
        }
    }
}
