using System;
using System.IO;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The CMB that actually ships, read by the parser that will read it on the headset.
    ///
    /// These are known-answer tests against published sky positions, not tolerance-shaped
    /// ones. The decisive property is orientation, and the way a CMB layer fails is that it
    /// looks perfect and sits 60 degrees off every star in the scene — a randomly rotated map
    /// has the same rms, the same histogram and the same beautiful mottling. So every check
    /// below is one a rotated map fails: the Cold Spot reads 0 ± 20 µK instead of −107, the
    /// galactic residual stops sitting on the galactic plane, and the mask stops agreeing
    /// with the map about where the galaxy is.
    ///
    /// Directions here are ICRF and the galactic ones are converted with the PUBLISHED pole
    /// position rather than with StandardFrames, so a bug in the shared rotation cannot make
    /// these tests agree with a wrongly baked file.
    /// </summary>
    public static class CmbSkyTests
    {
        private static CmbSky _sky;

        private static CmbSky Sky
        {
            get
            {
                if (_sky == null)
                {
                    var directory = new DirectoryInfo(AppContext.BaseDirectory);
                    string found = null;
                    for (int depth = 0; depth < 10 && directory != null && found == null; depth++)
                    {
                        string candidate = Path.Combine(directory.FullName, "unity", "Assets",
                            "StreamingAssets", "CosmoSynth", "cmb", "cmb_sky.cscb");
                        if (File.Exists(candidate)) found = candidate;
                        directory = directory.Parent;
                    }
                    if (found == null)
                        throw new FileNotFoundException(
                            "cmb_sky.cscb not found. Build it with " +
                            "'python3 tools/cosmodata/build_cmb.py'.");
                    _sky = CmbSky.Parse(File.ReadAllBytes(found));
                }
                return _sky;
            }
        }

        /// <summary>North galactic pole, ICRS J2000. Published, not derived from any matrix.</summary>
        private static Double3 NorthGalacticPole =>
            StandardFrames.FromSpherical(192.85948 * MathD.Deg2Rad, 27.12825 * MathD.Deg2Rad);

        private static Double3 RaDec(double raDeg, double decDeg) =>
            StandardFrames.FromSpherical(raDeg * MathD.Deg2Rad, decDeg * MathD.Deg2Rad);

        /// <summary>
        /// Directions spread evenly over the sphere by the Fibonacci lattice: equal area by
        /// construction, so a plain mean over them is a solid-angle-weighted sky mean and no
        /// cos(dec) factor can be forgotten.
        /// </summary>
        private static Double3[] SphereLattice(int count)
        {
            var result = new Double3[count];
            double golden = System.Math.PI * (3.0 - System.Math.Sqrt(5.0));
            for (int i = 0; i < count; i++)
            {
                double z = 1.0 - 2.0 * (i + 0.5) / count;
                double r = System.Math.Sqrt(System.Math.Max(0.0, 1.0 - z * z));
                double phi = i * golden;
                result[i] = new Double3(r * System.Math.Cos(phi), r * System.Math.Sin(phi), z);
            }
            return result;
        }

        [Test("the shipped file is a CSCB cube map with the geometry the contract specifies")]
        public static void ShippedFileHeader()
        {
            CmbSky sky = Sky;
            Assert.True(sky.FaceSize == 1024, $"face size is {sky.FaceSize}, expected 1024");
            Assert.True(sky.MaskFaceSize == 256, $"mask face size is {sky.MaskFaceSize}, expected 256");
            Assert.True(sky.Meta["frame"].AsString() == "ICRF", "the file must be in ICRF");
            Assert.True(sky.Meta["source"].AsString() == "wmap",
                "the map must be WMAP: the licence amendment removed every Planck data product");
            Assert.True(sky.Attribution().Contains("WMAP"),
                "the attribution line must name WMAP: " + sky.Attribution());
            Assert.False(sky.Attribution().Contains("Planck"),
                "nothing in this layer may credit Planck for the map");
        }

        [Test("the 8-bit encoding is exact at both ends: raw 0 is -500 uK, raw 255 is +500 uK")]
        public static void EncodingEndpointsAreExact()
        {
            Assert.Equal(-500.0, Sky.Decode(0), 1e-9, "decode(0)");
            Assert.Equal(500.0, Sky.Decode(255), 1e-9, "decode(255)");
            // and the step is one 255th of the span, so no value is unreachable
            Assert.Equal(1000.0 / 255.0, Sky.Decode(1) - Sky.Decode(0), 1e-12, "one step");
        }

        [Test("direction -> face -> direction round trips to within half a texel on all six faces")]
        public static void FaceTableRoundTrips()
        {
            int f = Sky.FaceSize;

            // 1. every texel centre selects its own face and its own indices. This is the one
            //    place the cube-face table is written twice — forward in TexelDirection and
            //    backward in SelectFace — so a typo in either copy shows up here and nowhere
            //    else until the headset renders a mirrored sky.
            for (int face = 0; face < 6; face++)
            {
                foreach (int i in new[] { 0, 1, f / 3, f / 2, f - 2, f - 1 })
                {
                    foreach (int j in new[] { 0, 1, f / 3, f / 2, f - 2, f - 1 })
                    {
                        Double3 d = CmbSky.TexelDirection(face, i, j, f);
                        CmbSky.SelectFace(d, out int gotFace, out double s, out double t);
                        Assert.True(gotFace == face,
                            $"texel ({i},{j}) of face {face} selected face {gotFace}");
                        Assert.True((int)(s * f) == i && (int)(t * f) == j,
                            $"face {face} texel ({i},{j}) round tripped to " +
                            $"({(int)(s * f)},{(int)(t * f)})");
                    }
                }
            }

            // 2. an arbitrary direction lands within half a texel of where it came from. The
            //    quantisation to a texel centre is the whole error, so the bound is the texel
            //    half-diagonal where texels are angularly largest, which is the face CENTRE:
            //    the face maps u in [-1, 1] onto 90 degrees through an arctangent, so dtheta/du
            //    is 1 rad/unit at u = 0 and only 0.5 at the edge. A texel is therefore 2/F
            //    radians across at the centre (0.112 deg at F = 1024), not the 90/F degrees a
            //    uniform reading of the face would suggest — a distinction that would quietly
            //    make this bound 21 % too tight.
            double bound = 0.5 * System.Math.Sqrt(2.0) * (2.0 / f) * MathD.Rad2Deg;
            double worst = 0.0;
            foreach (Double3 d in SphereLattice(60000))
            {
                CmbSky.SelectFace(d, out int face, out double s, out double t);
                Double3 back = CmbSky.TexelDirection(face, (int)(s * f), (int)(t * f), f);
                worst = System.Math.Max(worst, Double3.Angle(d, back) * MathD.Rad2Deg);
            }
            Assert.True(worst <= bound,
                $"worst direction round trip {worst:G4} deg exceeds the texel half-diagonal " +
                $"{bound:G4} deg");
        }

        [Test("the Cold Spot is where it was measured, and it is cold")]
        public static void ColdSpotIsWhereItWasMeasured()
        {
            // Cruz et al. 2005 / Planck 2013 XXIII: RA 03h15m20s, Dec -19d35m.
            double mean = DiscMean(RaDec(48.83, -19.58), 5.0, out int samples);
            Assert.True(samples >= 20000, $"only {samples} samples in the disc");
            // The contract expects -107 +- 6 uK for WMAP at 5 degrees. A map rotated anywhere
            // else reads 0 +- 20, so this bound is a statement about the map's orientation,
            // not a fitted tolerance: it is six times narrower than the failure mode's spread.
            Assert.Equal(-107.0, mean, 6.0, "Cold Spot 5 deg disc mean, uK");

            // and it agrees with what the importer measured over the same disc, across the
            // language boundary: Python's solid-angle-weighted texel mean against C#'s
            // bilinear samples on an equal-area lattice.
            double recorded = Sky.Meta["stats"]["coldSpot5degMeanUk"].AsDouble(double.NaN);
            Assert.True(!double.IsNaN(recorded), "the file records no coldSpot5degMeanUk");
            Assert.Equal(recorded, mean, 4.0, "C# disc mean vs the importer's recorded value");
        }

        [Test("the galactic residual sits on the galactic plane, using the published pole")]
        public static void GalacticResidualIsOnThePlane()
        {
            Double3 pole = NorthGalacticPole;
            double innerSum = 0.0, outerSum = 0.0;
            int innerN = 0, outerN = 0;
            foreach (Double3 d in SphereLattice(400000))
            {
                double b = System.Math.Abs(System.Math.Asin(Double3.Dot(d, pole)) * MathD.Rad2Deg);
                if (b >= 2.0 && b <= 30.0) continue;
                double v = Sky.Sample(d);
                if (b < 2.0) { innerSum += v * v; innerN++; }
                else { outerSum += v * v; outerN++; }
            }
            Assert.True(innerN > 5000 && outerN > 100000, $"{innerN} inner, {outerN} outer samples");
            double inner = System.Math.Sqrt(innerSum / innerN);
            double outer = System.Math.Sqrt(outerSum / outerN);
            // The importer measured 89.1 against 66.4 on the cube, a ratio of 1.34. The
            // contract's bar is 1.05. A map whose plane is somewhere else gives about 1.00.
            Assert.True(inner > 1.05 * outer,
                $"rms at |b| < 2 is {inner:F2} uK, at |b| > 30 it is {outer:F2} uK, " +
                $"ratio {inner / outer:F3}: the residual is not on the galactic plane");
            Assert.True(inner / outer < 2.0,
                $"ratio {inner / outer:F3} is implausibly high for a cleaned ILC map");
        }

        [Test("the KQ85 mask, a separately produced file, agrees about where the galaxy is")]
        public static void MaskAgreesWithTheMap()
        {
            double inSum = 0.0, outSum = 0.0;
            int inN = 0, outN = 0;
            foreach (Double3 d in SphereLattice(200000))
            {
                double v = Sky.Sample(d);
                if (Sky.IsMasked(d)) { inSum += v * v; inN++; }
                else { outSum += v * v; outN++; }
            }
            double fraction = (double)inN / (inN + outN);
            // The published KQ85 keeps about 75 % of the sky; the importer measured 0.2520.
            Assert.Equal(0.252, fraction, 0.01, "fraction of the sky the mask rejects");
            Assert.Equal(Sky.MaskedFraction, fraction, 0.01, "against the fraction the file records");
            double ratio = System.Math.Sqrt(inSum / inN) / System.Math.Sqrt(outSum / outN);
            // Measured 1.172 on the shipped cube. Rotating the mask 90 degrees about the ICRS
            // pole drops it to 0.923, and rotating it about x or y gives 0.99-1.00, so the
            // null is about 1.0 and the bar at 1.10 sits between the null and the signal.
            Assert.True(ratio > 1.10,
                $"rms inside the mask over rms outside is {ratio:F4}; a mask that did not " +
                "line up with this map would give about 1.00");
        }

        [Test("the sky statistics are a CMB anisotropy map, not something else")]
        public static void SkyStatisticsAreCmb()
        {
            double sum = 0.0, sumSq = 0.0, lo = double.MaxValue, hi = double.MinValue;
            Double3[] lattice = SphereLattice(300000);
            foreach (Double3 d in lattice)
            {
                double v = Sky.Sample(d);
                sum += v;
                sumSq += v * v;
                if (v < lo) lo = v;
                if (v > hi) hi = v;
            }
            double rms = System.Math.Sqrt(sumSq / lattice.Length);
            double mean = sum / lattice.Length;
            // The ILC at a 1 degree beam has an rms of 71 uK. Bilinear sampling of an 8-bit
            // cube shaves a little off; 2 uK either side of the importer's 71.09 is the whole
            // budget, and this is four orders of magnitude from the 3355 uK dipole or the
            // 2.7 K monopole, either of which being present would be obvious here.
            Assert.Equal(Sky.Meta["stats"]["rmsUk"].AsDouble(), rms, 2.0, "sky rms, uK");
            Assert.True(30.0 < rms && rms < 300.0, $"rms {rms:F2} uK is not a CMB anisotropy");
            Assert.Equal(0.0, mean, 5.0, "sky mean, uK (monopole and dipole are removed)");
            Assert.True(lo > -500.0 && hi < 500.0, $"range {lo:F1} .. {hi:F1} uK escapes the encoding");
        }

        [Test("temperature in kelvin is the monopole plus the anisotropy, and the dipole on request")]
        public static void TemperatureAddsTheConstantsBack()
        {
            Double3 apex = CmbModel.DipoleDirectionIcrf;
            double plain = Sky.TemperatureK(apex, CmbFrame.None);
            Assert.Equal(CmbModel.MonopoleKelvin, plain, 6e-4,
                "with no dipole the temperature is the monopole plus a few hundred uK");

            double withDipole = Sky.TemperatureK(apex, CmbFrame.Sun);
            Assert.Equal(CmbModel.DipoleAmplitudeMicrokelvin * 1e-6, withDipole - plain, 1e-9,
                "at the apex the dipole adds its full amplitude");
            Double3 antapex = -apex;
            Assert.Equal(-CmbModel.DipoleAmplitudeMicrokelvin * 1e-6,
                Sky.TemperatureK(antapex, CmbFrame.Sun) - Sky.TemperatureK(antapex, CmbFrame.None),
                1e-9, "at the antapex it subtracts it");
        }

        [Test("provenance is Inferred inside the mask and Measured outside it")]
        public static void ProvenanceFollowsTheMask()
        {
            // the galactic centre is deep inside any foreground mask
            Double3 gc = RaDec(266.40510, -28.93617);
            Assert.True(Sky.IsMasked(gc), "the galactic centre must be inside the KQ85 mask");
            Assert.True(Sky.ProvenanceAt(gc).Class == ProvenanceClass.Inferred,
                "dT at the galactic centre is not a clean measurement of the CMB");
            // the north galactic pole is the cleanest sky there is
            Assert.False(Sky.IsMasked(NorthGalacticPole),
                "the north galactic pole must be outside the mask");
            Assert.True(Sky.ProvenanceAt(NorthGalacticPole).Class == ProvenanceClass.Measured,
                "dT at the north galactic pole is measured");
        }

        [Test("false colour is a function of the value, monotonic and clamped")]
        public static void FalseColourIsAFunction()
        {
            CmbSky.FalseColour(0.0, 250.0, CmbPalette.Diverging, out double r, out double g, out double b);
            Assert.Equal(1.0, r, 1e-12, "zero is white: red");
            Assert.Equal(1.0, g, 1e-12, "zero is white: green");
            Assert.Equal(1.0, b, 1e-12, "zero is white: blue");

            CmbSky.FalseColour(250.0, 250.0, CmbPalette.Diverging, out double hr, out _, out double hb);
            CmbSky.FalseColour(-250.0, 250.0, CmbPalette.Diverging, out double cr, out _, out double cb);
            Assert.True(hr > hb, "hot must be redder than it is blue");
            Assert.True(cb > cr, "cold must be bluer than it is red");

            // saturation: beyond the contrast nothing more happens, so a spike cannot invent colour
            CmbSky.FalseColour(5000.0, 250.0, CmbPalette.Diverging, out double sr, out _, out double sb);
            Assert.Equal(hr, sr, 1e-12, "clamped red");
            Assert.Equal(hb, sb, 1e-12, "clamped blue");

            // the contrast is a real control: half the contrast doubles the response
            CmbSky.FalseColour(125.0, 250.0, CmbPalette.Diverging, out _, out _, out double b1);
            CmbSky.FalseColour(125.0, 125.0, CmbPalette.Diverging, out _, out _, out double b2);
            Assert.True(b2 < b1, "a tighter contrast must push further along the ramp");

            // inverted really is the mirror, grey carries no hue
            CmbSky.FalseColour(250.0, 250.0, CmbPalette.Inverted, out double ir, out _, out double ib);
            Assert.Equal(hb, ir, 1e-12, "inverted red is the diverging blue");
            Assert.Equal(hr, ib, 1e-12, "inverted blue is the diverging red");
            CmbSky.FalseColour(100.0, 250.0, CmbPalette.Grey, out double gr, out double gg, out double gb);
            Assert.Equal(gr, gg, 1e-12, "grey has no hue");
            Assert.Equal(gg, gb, 1e-12, "grey has no hue");
        }

        [Test("a corrupt or foreign file is refused by name, not read as if it were fine")]
        public static void RefusesWhatItShould()
        {
            byte[] good = File.ReadAllBytes(FindShipped());

            Refuses(new byte[8], "too short");
            byte[] wrongMagic = (byte[])good.Clone();
            wrongMagic[0] = (byte)'X';
            Refuses(wrongMagic, "wrong magic");
            byte[] wrongVersion = (byte[])good.Clone();
            wrongVersion[4] = 9;
            Refuses(wrongVersion, "version 9");
            byte[] truncated = new byte[good.Length - 64];
            Buffer.BlockCopy(good, 0, truncated, 0, truncated.Length);
            Refuses(truncated, "truncated");
            byte[] dirtyFlags = (byte[])good.Clone();
            dirtyFlags[12] = 1;
            Refuses(dirtyFlags, "non-zero flags");
        }

        private static void Refuses(byte[] data, string what)
        {
            try
            {
                CmbSky.Parse(data);
            }
            catch (FormatException)
            {
                return;
            }
            throw new AssertionException($"CmbSky.Parse accepted a {what} file");
        }

        private static string FindShipped()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 10 && directory != null; depth++)
            {
                string candidate = Path.Combine(directory.FullName, "unity", "Assets",
                    "StreamingAssets", "CosmoSynth", "cmb", "cmb_sky.cscb");
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new FileNotFoundException("cmb_sky.cscb not found");
        }

        /// <summary>
        /// Mean over a disc, sampled on an equal-area lattice inside the cap itself rather
        /// than by filtering a whole-sky one: cos(theta) uniform on [cos r, 1] with the
        /// golden angle in azimuth gives equal area, so a plain mean is the solid-angle
        /// weighted mean and no cos(dec) factor can be dropped.
        /// </summary>
        private static double DiscMean(Double3 centre, double radiusDeg, out int samples)
        {
            samples = 20000;
            Double3 n = centre.Normalized;
            Double3 seed = System.Math.Abs(n.z) < 0.9 ? Double3.UnitZ : Double3.UnitX;
            Double3 e1 = Double3.Cross(n, seed).Normalized;
            Double3 e2 = Double3.Cross(n, e1);
            double cosR = System.Math.Cos(radiusDeg * MathD.Deg2Rad);
            double golden = System.Math.PI * (3.0 - System.Math.Sqrt(5.0));
            double sum = 0.0;
            for (int i = 0; i < samples; i++)
            {
                double c = 1.0 - (1.0 - cosR) * (i + 0.5) / samples;
                double s = System.Math.Sqrt(System.Math.Max(0.0, 1.0 - c * c));
                double phi = i * golden;
                sum += Sky.Sample(n * c + (e1 * System.Math.Cos(phi) + e2 * System.Math.Sin(phi)) * s);
            }
            return sum / samples;
        }
    }
}
