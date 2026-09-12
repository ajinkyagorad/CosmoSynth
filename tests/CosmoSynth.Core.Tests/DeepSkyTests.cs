using System;
using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The arithmetic the deep-sky layers rest on. Everything here is engine-free on purpose:
    /// these are the claims that a screenshot cannot check, and several of them are claims a
    /// wrong answer would leave looking entirely plausible — a mirrored Galaxy, a flipped cube
    /// face, a spiral drawn where nobody has measured one.
    /// </summary>
    public static class DeepSkyTests
    {
        // ------------------------------------------------------------------ PointChunks

        [Test("chunked offsets are exact to well under a pixel")]
        public static void ChunkOffsetPrecision()
        {
            Double3[] points = SpreadOfStars(20000, seed: 12345);
            PointChunk[] chunks = PointChunks.Build(points, maxPerChunk: 4096, maxChunks: 64);

            Assert.True(chunks.Length > 1, "20,000 points over 400 pc should split into several chunks");

            double worst = 0.0;
            int seen = 0;
            bool[] covered = new bool[points.Length];
            for (int c = 0; c < chunks.Length; c++)
            {
                PointChunk chunk = chunks[c];
                for (int i = 0; i < chunk.Count; i++)
                {
                    int index = chunk.Indices[i];
                    Assert.False(covered[index], "point " + index + " is in two chunks");
                    covered[index] = true;
                    seen++;

                    Double3 rebuilt = chunk.OriginUnits + new Double3(
                        chunk.OffsetsXyz[i * 3 + 0],
                        chunk.OffsetsXyz[i * 3 + 1],
                        chunk.OffsetsXyz[i * 3 + 2]);
                    double error = Double3.Distance(rebuilt, points[index]);
                    double bound = PointChunks.OffsetErrorBound * Math.Max(chunk.RadiusUnits, 1e-12);
                    Assert.True(error <= bound,
                        "chunk " + c + " point " + index + ": float offset error " + error +
                        " exceeds " + bound + " (radius " + chunk.RadiusUnits + ")");
                    worst = Math.Max(worst, error / Math.Max(chunk.RadiusUnits, 1e-12));
                }
            }

            Assert.Equal(points.Length, seen, 0, "every point lands in exactly one chunk");
            Assert.True(worst <= PointChunks.OffsetErrorBound,
                "worst relative offset error " + worst + " over the 6e-8 bound");
        }

        [Test("the chunk ceiling is a ceiling, because every chunk is a draw call")]
        public static void ChunkCeiling()
        {
            Double3[] points = SpreadOfStars(50000, seed: 99);
            PointChunk[] chunks = PointChunks.Build(points, maxPerChunk: 256, maxChunks: 16);
            Assert.True(chunks.Length <= 16,
                chunks.Length + " chunks from a 16-chunk ceiling — a chunk is a draw call");
        }

        [Test("coincident points do not send the octree split into an infinite recursion")]
        public static void ChunkDegenerateBox()
        {
            // The distance-unknown rows of the star catalogue all share one sentinel position.
            Double3[] points = new Double3[5000];
            for (int i = 0; i < points.Length; i++) points[i] = new Double3(1e5, 0, 0);
            PointChunk[] chunks = PointChunks.Build(points, maxPerChunk: 64, maxChunks: 64);
            Assert.Equal(1, chunks.Length, 0, "one degenerate box, not a stack overflow");
            Assert.Equal(0.0, chunks[0].RadiusUnits, 1e-12, "a box of one point has no radius");
        }

        [Test("the cone cull keeps what is in front and drops what is behind")]
        public static void ChunkCone()
        {
            PointChunk ahead = new PointChunk
            {
                OriginUnits = new Double3(0, 0, 100), RadiusUnits = 1.0,
                Indices = new int[0], OffsetsXyz = new float[0],
            };
            PointChunk behind = new PointChunk
            {
                OriginUnits = new Double3(0, 0, -100), RadiusUnits = 1.0,
                Indices = new int[0], OffsetsXyz = new float[0],
            };
            Double3 eye = Double3.Zero;
            Double3 forward = Double3.UnitZ;
            double half = 0.6;   // about 34 degrees

            Assert.True(PointChunks.InCone(ahead, eye, forward, half), "a chunk straight ahead is kept");
            Assert.False(PointChunks.InCone(behind, eye, forward, half), "a chunk behind is dropped");

            // The eye inside the box is always a hit: there is no direction that misses it.
            PointChunk around = new PointChunk
            {
                OriginUnits = Double3.Zero, RadiusUnits = 10.0,
                Indices = new int[0], OffsetsXyz = new float[0],
            };
            Assert.True(PointChunks.InCone(around, eye, forward, 0.01),
                "a chunk containing the eye is always in view");
        }

        // -------------------------------------------------------------------- StarQuery

        [Test("the Sun seen from Alpha Centauri is a first-magnitude star")]
        public static void SunFromAlphaCentauri()
        {
            // V = -26.74 at 1 AU = 4.8481368e-6 pc gives the textbook M_V = 4.83.
            double absolute = StarQuery.AbsoluteMagnitude(-26.74, 4.8481368e-6);
            Assert.Equal(4.83, absolute, 0.01, "the Sun's absolute visual magnitude");

            // From Proxima's 1.3012 pc the Sun is magnitude 0.40 — naked-eye, and about as bright
            // as Procyon. This is the known answer the observed-sky mode is anchored on.
            double apparent = StarQuery.ApparentMagnitude(absolute, 1.3012);
            Assert.Equal(0.40, apparent, 0.02, "the Sun's apparent magnitude from Alpha Centauri");
        }

        [Test("absolute and apparent magnitude are exact inverses")]
        public static void MagnitudeRoundTrip()
        {
            double[] distances = { 0.1, 1.0, 10.0, 137.0, 5000.0, 120000.0 };
            foreach (double d in distances)
            {
                double m = StarQuery.ApparentMagnitude(3.25, d, extinctionAv: 0.4);
                double back = StarQuery.AbsoluteMagnitude(m, d, extinctionAv: 0.4);
                Assert.Equal(3.25, back, 1e-12, "round trip at " + d + " pc");
            }
        }

        [Test("extinction is subtracted once, and only once")]
        public static void ExtinctionAppliedOnce()
        {
            // A star observed at V = 8 behind one magnitude of dust is intrinsically brighter than
            // the same star with no dust. De-reddening twice would make it two magnitudes brighter
            // and the whole sky would come out too luminous in a way that looks like an exposure
            // problem (R41).
            double reddened = StarQuery.AbsoluteMagnitude(8.0, 100.0, extinctionAv: 1.0);
            double clean = StarQuery.AbsoluteMagnitude(8.0, 100.0, extinctionAv: 0.0);
            Assert.Equal(1.0, clean - reddened, 1e-12, "one magnitude of dust, applied once");
        }

        [Test("the visual response is 1 at the limit and 0 below it")]
        public static void LimitMagnitude()
        {
            Assert.Equal(1.0, StarQuery.VisualResponse(6.5, 6.5), 1e-12, "exactly at the limit");
            Assert.Equal(0.0, StarQuery.VisualResponse(6.6, 6.5), 0.0, "just under the limit is absent");
            Assert.True(StarQuery.VisualResponse(1.0, 6.5) > 1.0, "a bright star is over the limit value");

            // The eye's compression is the point: 5.5 magnitudes is a factor of 158 in flux but
            // only about 3.2 in what the response reports.
            double ratio = StarQuery.VisualResponse(1.0, 6.5) / StarQuery.VisualResponse(6.5, 6.5);
            Assert.Equal(Math.Pow(Math.Pow(10.0, 0.4 * 5.5), StarQuery.EyeExponent), ratio, 1e-9,
                "the 0.23 eye exponent, not a raw flux ratio");
            Assert.True(ratio < 4.0, "158x in flux must not become 158x on screen");
        }

        [Test("a star with no distance has no apparent magnitude from anywhere else")]
        public static void UnknownDistanceHasNoPhotometry()
        {
            // AT-HYG's sentinel: 146 rows. They keep the sky shell and are never re-photometered,
            // because computing a distance modulus from a distance nobody measured is invention.
            CatalogStar unknown = new CatalogStar(new Double3(1e5, 0, 0), 6.0, 5000.0,
                StarFlags.DistanceUnknown);
            Assert.True(double.IsNaN(StarQuery.ApparentMagnitudeFrom(unknown, Double3.Zero)),
                "no distance, no magnitude — not a plausible-looking number");
        }

        // ------------------------------------------------------------- GalaxyOrientation

        [Test("position angle 0 is north and 90 is east, exactly")]
        public static void PositionAngleGeometry()
        {
            double ra = 1.234, dec = -0.567;
            GalaxyOrientation.SkyBasis(ra, dec, out Double3 north, out Double3 east, out Double3 outward);

            Assert.Equal(0.0, Double3.Distance(GalaxyOrientation.MajorAxis(ra, dec, 0.0), north), 1e-12,
                "PA 0 is parallel to north");
            Assert.Equal(0.0, Double3.Distance(GalaxyOrientation.MajorAxis(ra, dec, 90.0), east), 1e-12,
                "PA 90 is parallel to east");

            Assert.Equal(0.0, Double3.Dot(north, east), 1e-12, "the sky basis is orthogonal");
            Assert.Equal(0.0, Double3.Dot(north, outward), 1e-12, "north is on the sky");
            Assert.Equal(1.0, outward.Magnitude, 1e-12, "the outward direction is a unit vector");
        }

        [Test("inclination from axis ratio matches the Hubble formula at three published points")]
        public static void InclinationKnownAnswers()
        {
            Assert.Equal(78.46, GalaxyOrientation.InclinationDeg(0.28), 0.01, "q = 0.28");
            Assert.Equal(74.46, GalaxyOrientation.InclinationDeg(0.33), 0.01, "q = 0.33");
            Assert.Equal(55.49, GalaxyOrientation.InclinationDeg(0.59), 0.01, "q = 0.59");

            Assert.Equal(90.0, GalaxyOrientation.InclinationDeg(0.2), 1e-9,
                "a disc no thinner than q0 is edge-on, not imaginary");
            Assert.Equal(90.0, GalaxyOrientation.InclinationDeg(0.05), 1e-9, "thinner still is still 90");
            Assert.Equal(0.0, GalaxyOrientation.InclinationDeg(1.0), 1e-9, "a round galaxy is face-on");
            Assert.True(double.IsNaN(GalaxyOrientation.InclinationDeg(double.NaN)),
                "unknown axis ratio stays unknown — never a default");
        }

        [Test("the disc normal is perpendicular to the major axis at any inclination")]
        public static void DiscNormalGeometry()
        {
            double ra = 0.19, dec = 0.72;
            GalaxyOrientation.SkyBasis(ra, dec, out _, out _, out Double3 outward);
            foreach (double inclination in new[] { 0.0, 30.0, 60.0, 90.0 })
            {
                Double3 major = GalaxyOrientation.MajorAxis(ra, dec, 45.0);
                Double3 normal = GalaxyOrientation.DiscNormal(outward, inclination, major, 1);
                Assert.Equal(1.0, normal.Magnitude, 1e-12, "the normal is a unit vector");
                Assert.Equal(0.0, Double3.Dot(normal, major), 1e-9,
                    "the disc is tilted ABOUT its major axis, so the two stay perpendicular");
            }

            // Face-on means looking straight down the normal.
            Double3 faceOn = GalaxyOrientation.DiscNormal(outward, 0.0,
                GalaxyOrientation.MajorAxis(ra, dec, 0.0), 1);
            Assert.Equal(-1.0, Double3.Dot(faceOn, outward.Normalized), 1e-9,
                "a face-on disc looks back along the line of sight");
        }

        [Test("the front/back choice is invented, and therefore never changes")]
        public static void FaceSignIsStable()
        {
            Assert.Equal(GalaxyOrientation.FaceSign("NGC 224"), GalaxyOrientation.FaceSign("NGC 224"), 0,
                "the same name gives the same sign");
            int a = GalaxyOrientation.FaceSign("NGC 224");
            int b = GalaxyOrientation.FaceSign("NGC 5128");
            Assert.True(a == 1 || a == -1, "the sign is a sign");
            Assert.True(b == 1 || b == -1, "the sign is a sign");
        }

        // ------------------------------------------------------------------ GalaxyModel

        [Test("the Galactic centre is where Sgr A* is")]
        public static void GalacticCentreDirection()
        {
            // The strongest check available without a data file: the galactocentric frame, the
            // heliocentric galactic frame and the Hipparcos matrix have to compose into the
            // published radio position of Sgr A*. Any sign error anywhere in that chain moves it.
            Double3 centre = GalaxyModel.IcrfPcFrom(Double3.Zero);
            StandardFrames.ToSpherical(centre, out double ra, out double dec, out double distance);

            double raDeg = ra * MathD.Rad2Deg;
            if (raDeg < 0.0) raDeg += 360.0;
            Assert.Equal(266.41684, raDeg, 0.02, "Sgr A* right ascension");
            Assert.Equal(-29.00781, dec * MathD.Rad2Deg, 0.02, "Sgr A* declination");
            Assert.Equal(8150.0, distance, 1.0, "R0 in parsecs");
        }

        [Test("the galactocentric frame is not mirrored")]
        public static void GalactocentricFrameIsProper()
        {
            // A handedness error here produces a Galaxy that is correct in every measurable
            // respect except that it is inside out, and no screenshot can show it. The test is
            // that the map preserves the cross product.
            Double3 x = Direction(new Double3(1, 0, 0));
            Double3 y = Direction(new Double3(0, 1, 0));
            Double3 z = Direction(new Double3(0, 0, 1));
            Double3 cross = Double3.Cross(x, y);
            Assert.Equal(0.0, Double3.Distance(cross, z), 1e-9,
                "x cross y must still be z: determinant +1, not -1");
        }

        private static Double3 Direction(Double3 galactocentricOffsetKpc)
        {
            Double3 a = GalaxyModel.HeliocentricGalacticPcFrom(Double3.Zero);
            Double3 b = GalaxyModel.HeliocentricGalacticPcFrom(galactocentricOffsetKpc);
            return (b - a).Normalized;
        }

        [Test("the Sun sits on its own frame's origin, both ways round")]
        public static void SunFrameRoundTrip()
        {
            Double3 sunHel = GalaxyModel.HeliocentricGalacticPcFrom(GalaxyModel.SunGalactocentricKpc);
            Assert.Equal(0.0, sunHel.Magnitude, 1e-9, "the Sun is the origin of the heliocentric frame");

            Double3[] samples =
            {
                new Double3(0, 0, 0), new Double3(3, -4, 0.2), new Double3(-12, 7, -0.5),
            };
            foreach (Double3 p in samples)
            {
                Double3 back = GalaxyModel.GalactocentricKpcFromHeliocentric(
                    GalaxyModel.HeliocentricGalacticPcFrom(p));
                Assert.Equal(0.0, Double3.Distance(p, back), 1e-9, "galactocentric round trip");

                Double3 backFromIcrf = GalaxyModel.GalactocentricKpcFrom(GalaxyModel.IcrfPcFrom(p));
                Assert.Equal(0.0, Double3.Distance(p, backFromIcrf), 1e-9, "and through ICRF");
            }

            // The Sun's derived height above the IAU galactic plane is about 6.5 pc, NOT the 20.8
            // pc of GalaxyModel.SunHeightPc. The two measure different planes -- the 1958 HI frame
            // and the true mid-plane -- and conflating them is what put the Galactic centre 0.14
            // degrees from Sgr A*. Pinning both here is the only thing that keeps the distinction
            // from quietly collapsing back into one number.
            double derivedHeightPc = GalaxyModel.SunGalactocentricKpc.z * 1000.0;
            Assert.Equal(6.5, derivedHeightPc, 1.5, "the Sun's height above the IAU plane");
            Assert.True(Math.Abs(derivedHeightPc - GalaxyModel.SunHeightPc) > 5.0,
                "these are two different measurements and must not agree by construction");
        }

        [Test("the arms wind the way the Galaxy rotates")]
        public static void GalacticRotationSense()
        {
            // Reid's azimuth beta is zero toward the Sun and increases in the direction of
            // galactic rotation. At the Sun that direction is galactic longitude 90 degrees --
            // and in the galactocentric basis that is MINUS y, because y is a cross product built
            // from the pole and the Sun direction. Get this backwards and every arm mirrors about
            // the Sun-centre line: a Milky Way that winds the wrong way, and still looks like one.
            Double3 towardL90 = StandardFrames.GalacticToEquatorial * Double3.UnitY;
            Double3 basisY = GalaxyModel.GalactocentricToIcrf * Double3.UnitY;
            Assert.Equal(-1.0, Double3.Dot(towardL90, basisY), 1e-3,
                "galactocentric +y is the anti-rotation direction");

            // So a small positive beta moves an arm point toward l = 90.
            SpiralArm arm = GalaxyModel.Arms[4];
            Double3 atZero = GalaxyModel.ArmPointKpc(arm, 0.0);
            Double3 atSmall = GalaxyModel.ArmPointKpc(arm, 5.0);
            Assert.True(Double3.Dot((atSmall - atZero).Normalized, -Double3.UnitY) > 0.9,
                "increasing beta follows the rotation");
        }

        [Test("an arm point is at the radius its own fit gives")]
        public static void ArmRadius()
        {
            foreach (SpiralArm arm in GalaxyModel.Arms)
            {
                Assert.Equal(arm.RefRadiusKpc, arm.RadiusKpcAt(arm.RefBetaDeg), 1e-12,
                    arm.Name + " passes through its own reference point");
                for (double beta = arm.BetaMinDeg; beta <= arm.BetaMaxDeg; beta += 10.0)
                {
                    Double3 p = GalaxyModel.ArmPointKpc(arm, beta);
                    Assert.Equal(arm.RadiusKpcAt(beta), Math.Sqrt(p.x * p.x + p.y * p.y), 1e-9,
                        arm.Name + " at beta " + beta);
                }
                Assert.True(arm.RefRadiusKpc > 0.5 && arm.RefRadiusKpc < 20.0,
                    arm.Name + " reference radius is inside the disc");
                Assert.True(arm.BetaMaxDeg > arm.BetaMinDeg, arm.Name + " has a non-empty fitted range");
            }
        }

        [Test("an arm is only drawn at full strength where it was fitted")]
        public static void ArmSupport()
        {
            SpiralArm arm = GalaxyModel.Arms[3];
            double mid = 0.5 * (arm.BetaMinDeg + arm.BetaMaxDeg);

            Assert.Equal(1.0, arm.Support(mid), 1e-12, "inside the fitted range");
            Assert.Equal(1.0, arm.Support(arm.BetaMaxDeg), 1e-12, "at the edge of the fit");
            Assert.Equal(0.575, arm.Support(arm.BetaMaxDeg + 15.0), 1e-9,
                "halfway through the extrapolation, halfway to the ghost");
            Assert.Equal(0.15, arm.Support(arm.BetaMaxDeg + 30.0), 1e-9, "the far end of the guess");
            Assert.Equal(0.0, arm.Support(arm.BetaMaxDeg + 31.0), 0.0,
                "beyond that the arm simply stops — this is the Atlas's complete-spiral bug");
            Assert.Equal(0.0, arm.Support(arm.BetaMinDeg - 60.0), 0.0, "and the same on the other side");
        }

        // ---------------------------------------------------------------------- CmbModel

        [Test("the CMB byte encoding hits its stated endpoints exactly")]
        public static void CmbDecode()
        {
            Assert.Equal(-500.0, CmbModel.DecodeMicroK(0), 1e-12, "raw 0");
            Assert.Equal(500.0, CmbModel.DecodeMicroK(255), 1e-12, "raw 255");
            Assert.Equal(0.0, CmbModel.DecodeMicroK(128), 4.0, "the middle byte is near zero");
        }

        [Test("the dipole points where Planck says, and is switchable")]
        public static void CmbDipole()
        {
            Double3 dir = CmbModel.DipoleDirectionIcrf;
            Assert.Equal(1.0, dir.Magnitude, 1e-12, "unit vector");

            Double3 galactic = StandardFrames.EquatorialToGalactic * dir;
            StandardFrames.ToSpherical(galactic, out double lon, out double lat, out _);
            double lonDeg = lon * MathD.Rad2Deg;
            if (lonDeg < 0.0) lonDeg += 360.0;
            Assert.Equal(264.021, lonDeg, 0.01, "dipole galactic longitude");
            Assert.Equal(48.253, lat * MathD.Rad2Deg, 0.01, "dipole galactic latitude");

            // Hottest along the dipole, coldest against it, and 3.4 mK between them.
            double hot = CmbModel.TemperatureK(dir, 0.0, CmbFrame.Sun);
            double cold = CmbModel.TemperatureK(-dir, 0.0, CmbFrame.Sun);
            Assert.Equal(2.0 * CmbModel.DipoleAmplitudeMicroK * 1e-6, hot - cold, 1e-12,
                "peak to peak is twice the amplitude");
            Assert.Equal(CmbModel.T0Kelvin, 0.5 * (hot + cold), 1e-12, "and the mean is the monopole");

            // In the Local Group frame the dipole is gone, which is the whole reason it is a toggle:
            // it is our motion, not the early universe.
            Assert.Equal(0.0, CmbModel.DipoleAmplitude(CmbFrame.LocalGroup), 1e-12,
                "the Local Group frame is the one the dipole vanishes in");
        }

        [Test("the false-colour ramp is a function, monotone and clamped")]
        public static void CmbPalette()
        {
            Double3 cold = CmbModel.Palette(-250.0);
            Double3 mid = CmbModel.Palette(0.0);
            Double3 hot = CmbModel.Palette(250.0);

            Assert.True(cold.z > cold.x, "cold is blue");
            Assert.True(hot.x > hot.z, "hot is red");
            Assert.True(Math.Abs(mid.x - mid.z) < 0.2, "zero is neutral, so the eye can find it");

            // Beyond the contrast the ramp clamps rather than wrapping; a wrap would put the
            // hottest spots back at cold blue and invent structure that is not in the data.
            Assert.Equal(0.0, Double3.Distance(CmbModel.Palette(9999.0), CmbModel.Palette(250.0)), 1e-12,
                "above the contrast it clamps");
            Assert.Equal(0.0, Double3.Distance(CmbModel.Palette(-9999.0), CmbModel.Palette(-250.0)), 1e-12,
                "and below it too");

            for (double t = -250; t <= 250; t += 10)
            {
                Double3 c = CmbModel.Palette(t);
                Assert.True(c.x >= 0 && c.x <= 1 && c.y >= 0 && c.y <= 1 && c.z >= 0 && c.z <= 1,
                    "ramp stays in range at " + t + " uK");
            }
        }

        [Test("every cube face maps to the direction it came from")]
        public static void CmbFaceRoundTrip()
        {
            // This is the offline half of the `cmb-faces` play-mode assertion. A flipped face
            // reads as hundreds of microkelvin of error somewhere nobody can check by eye, on a
            // map that still looks exactly like the microwave background.
            for (int face = 0; face < CmbSky.Faces; face++)
            {
                foreach (double u in new[] { 0.1, 0.5, 0.9 })
                {
                    foreach (double v in new[] { 0.1, 0.5, 0.9 })
                    {
                        Double3 dir = CmbSky.DirectionOf(face, u, v);
                        int back = CmbSky.FaceOf(dir, out double u2, out double v2);
                        Assert.Equal(face, back, 0, "face " + face + " at (" + u + ", " + v + ")");
                        Assert.Equal(u, u2, 1e-9, "u round trip on face " + face);
                        Assert.Equal(v, v2, 1e-9, "v round trip on face " + face);
                    }
                }
            }

            // And the six face centres are the six axes, in Unity's cube order.
            Assert.Equal(0.0, Double3.Distance(CmbSky.DirectionOf(0, 0.5, 0.5), Double3.UnitX), 1e-12, "+X");
            Assert.Equal(0.0, Double3.Distance(CmbSky.DirectionOf(1, 0.5, 0.5), -Double3.UnitX), 1e-12, "-X");
            Assert.Equal(0.0, Double3.Distance(CmbSky.DirectionOf(2, 0.5, 0.5), Double3.UnitY), 1e-12, "+Y");
            Assert.Equal(0.0, Double3.Distance(CmbSky.DirectionOf(3, 0.5, 0.5), -Double3.UnitY), 1e-12, "-Y");
            Assert.Equal(0.0, Double3.Distance(CmbSky.DirectionOf(4, 0.5, 0.5), Double3.UnitZ), 1e-12, "+Z");
            Assert.Equal(0.0, Double3.Distance(CmbSky.DirectionOf(5, 0.5, 0.5), -Double3.UnitZ), 1e-12, "-Z");
        }

        [Test("an absent CMB file reads as a flat sky, not as a crash")]
        public static void CmbAbsent()
        {
            CmbSky empty = new CmbSky(0, null, null, "none");
            Assert.False(empty.IsPresent, "no faces, not present");
            Assert.Equal(0.0, empty.Sample(Double3.UnitZ), 0.0, "and no anisotropy");
            Assert.Equal(CmbModel.T0Kelvin, empty.TemperatureK(Double3.UnitZ, CmbFrame.LocalGroup), 1e-12,
                "still the right monopole");
        }

        // ------------------------------------------------------------------ DistanceGrids

        [Test("a ring is drawn only when it is legible, at both ends")]
        public static void GridLegibility()
        {
            double km = 1.0;
            Assert.False(DistanceGrids.IsLegible(km, 0.01), "1 cm in the room is a blob on the centre");
            Assert.True(DistanceGrids.IsLegible(km, 0.5), "half a metre reads");
            Assert.False(DistanceGrids.IsLegible(km, 10.0),
                "ten metres is a band across the whole view — the floor needs a ceiling");
        }

        [Test("the right grid for a scale is a fact about the scale")]
        public static void GridRegimeAtEachRung()
        {
            // The three rungs the three regimes exist for, from CONTRACT 5.1.
            Assert.True(DistanceGrids.Best(2.846e-16) == GridRegime.Stellar,
                "solar neighbourhood, 1 m = 371 ly");
            Assert.True(DistanceGrids.Best(5.671e-19) == GridRegime.Galactic,
                "Milky Way, 1 m = 57.1 kpc");
            Assert.True(DistanceGrids.Best(1.458e-21) == GridRegime.LocalGroup,
                "Local Volume, 1 m = 22.2 Mpc");

            Assert.False(DistanceGrids.Best(1e3).HasValue,
                "at true scale nothing is legible, so nothing is drawn");
            Assert.False(DistanceGrids.Best(2e-25).HasValue,
                "and at the observable universe the grids are gone too");
        }

        [Test("light-travel time is the other half of every ring label")]
        public static void GridLightTime()
        {
            IReadOnlyList<GridRing> galactic = DistanceGrids.Rings(GridRegime.Galactic);
            foreach (GridRing ring in galactic)
            {
                Assert.True(ring.LightYears > 0.0, ring.DistanceLabel + " has a light time");
                Assert.Equal(ring.RadiusKm / DistanceGrids.KmPerLightYear, ring.LightYears, 1e-9,
                    "light time is the radius in light years, not an independent number");
            }

            // R0 is 8.15 kpc, which is 26,580 light years.
            GridRing sun = galactic[2];
            Assert.Equal(26580.0, sun.LightYears, 40.0, "the Sun's orbit in light years");
        }

        [Test("the grid planes come from the frame matrices, not from a second spelling")]
        public static void GridPlanes()
        {
            Double3 galactic = DistanceGrids.PlaneNormalIcrf(GridRegime.Galactic);
            Double3 expected = StandardFrames.GalacticToEquatorial * Double3.UnitZ;
            Assert.Equal(0.0, Double3.Distance(galactic, expected), 1e-12,
                "the galactic grid normal IS the north galactic pole");

            Double3 supergalactic = DistanceGrids.PlaneNormalIcrf(GridRegime.LocalGroup);
            Assert.Equal(0.0, Double3.Dot(supergalactic, supergalactic) - 1.0, 1e-12, "unit");
            Assert.True(Double3.Angle(galactic, supergalactic) > 0.1,
                "the two planes are genuinely different, which is why both grids exist");
        }

        // --------------------------------------------------------------------- DustCube

        [Test("extinction along a line is the same in both directions")]
        public static void ExtinctionIsSymmetric()
        {
            DustCube cube = new DustCube(ConstantGrid(0.001));
            Double3 a = new Double3(-400, -300, -100);
            Double3 b = new Double3(500, 200, 150);
            double forward = cube.ExtinctionAlong(a, b, 10.0);
            double back = cube.ExtinctionAlong(b, a, 10.0);
            Assert.Equal(forward, back, 1e-9, "a star's extinction cannot depend on which end you start");
            Assert.True(forward > 0.0, "and it is not zero through a cloud");
        }

        [Test("a uniform cloud integrates to density times path length")]
        public static void ExtinctionKnownAnswer()
        {
            DustCube cube = new DustCube(ConstantGrid(0.001));
            Double3 a = new Double3(-500, 0, 0);
            Double3 b = new Double3(500, 0, 0);
            // 1000 pc at 0.001 mag/pc is 1.0 magnitudes of extinction.
            Assert.Equal(1.0, cube.ExtinctionAlong(a, b, 10.0), 0.02, "1 kpc through 0.001 mag/pc");
        }

        [Test("outside the survey box there is no dust, because nobody measured any")]
        public static void OutsideTheBoxIsEmpty()
        {
            DustCube cube = new DustCube(ConstantGrid(0.001));
            Assert.Equal(0.0, cube.DensityAt(new Double3(5000, 0, 0)), 0.0, "far outside the box");
            Assert.Equal(0.0, cube.ExtinctionAlong(new Double3(4000, 4000, 4000),
                new Double3(5000, 5000, 5000), 10.0), 0.0, "and along a line entirely outside it");
        }

        [Test("a missing dust cube is a layer that draws nothing, not an exception")]
        public static void AbsentDustCube()
        {
            DustCube cube = new DustCube(null);
            Assert.False(cube.IsPresent, "no grid, not present");
            Assert.Equal(0.0, cube.DensityAt(Double3.Zero), 0.0, "no density");
            Assert.Equal(0.0, cube.ExtinctionAlong(Double3.Zero, new Double3(100, 0, 0)), 0.0,
                "no extinction");
        }

        [Test("the voxel decode interpolates in density, not in the log index")]
        public static void VoxelTrilinear()
        {
            // Two nodes an order of magnitude apart: the midpoint of a LINEAR interpolation is the
            // arithmetic mean. Interpolating the byte first and decoding after would give the
            // geometric mean, which systematically under-reads the edge of every cloud.
            byte[] raw = new byte[8];
            VoxelGrid probe = new VoxelGrid(2, 2, 2, Double3.Zero, Double3.One, 0.01, Math.Log(100.0), raw);
            raw[0] = ByteFor(probe, 0.01);
            raw[1] = ByteFor(probe, 1.0);
            for (int i = 2; i < 8; i++) raw[i] = raw[i % 2];

            double mid = probe.SampleTrilinear(new Double3(0.5, 0.0, 0.0));
            double arithmetic = 0.5 * (probe.Decode(raw[0]) + probe.Decode(raw[1]));
            double geometric = Math.Sqrt(probe.Decode(raw[0]) * probe.Decode(raw[1]));
            Assert.Equal(arithmetic, mid, 1e-9, "linear in density");
            Assert.True(Math.Abs(mid - geometric) > 0.1 * arithmetic,
                "and the two really do differ, so the test is not vacuous");
        }

        // ------------------------------------------------------------------- LayerState

        [Test("a layer whose file is absent has no weight and says why")]
        public static void AbsentDataIsHonest()
        {
            LayerState state = new LayerState();
            LayerContext empty = Context(LoadedKinds.Empty);

            Assert.Equal(0.0, state.Weight("dust", -17.0, empty), 0.0,
                "no dust file, no weight, whatever the scale says");
            Assert.True(state.Status("dust", -17.0, empty) == LayerStatus.NotInThisBuild,
                "and the panel row says so rather than reading `on` and drawing nothing");

            LayerContext loaded = Context(LoadedKinds.From(new List<string> { "dust" }));
            Assert.True(state.Weight("dust", -17.0, loaded) > 0.9, "with the file, full weight mid-window");
        }

        [Test("a window fades rather than pops, at both ends")]
        public static void WindowFadesContinuously()
        {
            LayerDef dust = LayerTable.Find("dust");
            Assert.True(dust != null, "the dust row exists");

            double previous = LayerState.Window(dust, dust.MinLog10U - 1.0);
            for (double u = dust.MinLog10U - 1.0; u <= dust.MaxLog10U + 1.0; u += 0.01)
            {
                double w = LayerState.Window(dust, u);
                Assert.True(w >= 0.0 && w <= 1.0, "weight in range at " + u);
                Assert.True(Math.Abs(w - previous) <= 0.1,
                    "step of " + Math.Abs(w - previous) + " at log10U " + u + " — that is a pop");
                previous = w;
            }

            Assert.Equal(0.0, LayerState.Window(dust, dust.MinLog10U), 1e-12, "zero at the lower edge");
            Assert.Equal(0.0, LayerState.Window(dust, dust.MaxLog10U), 1e-12, "zero at the upper edge");
            Assert.Equal(1.0, LayerState.Window(dust, 0.5 * (dust.MinLog10U + dust.MaxLog10U)), 1e-12,
                "one in the middle");
        }

        [Test("SOLO isolates a group and never hides the guides")]
        public static void SoloKeepsGuides()
        {
            LayerState state = new LayerState { Solo = SoloGroup.Beyond };
            LayerContext ctx = Context(LoadedKinds.Empty);

            state.SetOn("cylGrids", true);
            Assert.True(state.Weight("cylGrids", -18.0, ctx) > 0.0,
                "a guide has SoloGroup.None and survives every SOLO");
            Assert.Equal(0.0, state.Weight("arms", -18.0, ctx), 0.0, "a Galaxies layer does not");
        }

        [Test("every layer row is inside the scale limits and the right way round")]
        public static void LayerTableIsWellFormed()
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (LayerDef def in LayerTable.Layers)
            {
                Assert.True(ids.Add(def.Id), "duplicate layer id " + def.Id);
                Assert.True(def.MinLog10U < def.MaxLog10U, def.Id + " has an empty window");
                Assert.True(def.MinLog10U >= -24.71, def.Id + " starts below the scale floor");
                Assert.True(def.MaxLog10U <= 3.01, def.Id + " ends above true scale");
                Assert.True(def.MaxLog10U - def.MinLog10U > 2.0 * LayerState.FadeDecades,
                    def.Id + " is narrower than its own two fades, so it never reaches full weight");
                foreach (LayerParam p in def.Params)
                {
                    Assert.True(p.Min <= p.Default && p.Default <= p.Max,
                        def.Id + "." + p.Id + " default is outside its range");
                }
            }
        }

        [Test("a presentation gain raises its chip only when it is away from its default")]
        public static void PresentationGainChip()
        {
            LayerState state = new LayerState();
            Assert.False(state.IsGainActive("stars", "exposure"), "at the default, no chip");
            state.SetParam("stars", "exposure", 2.0);
            Assert.True(state.IsGainActive("stars", "exposure"), "away from it, a chip");
            state.SetParam("stars", "exposure", 99.0);
            Assert.Equal(3.0, state.Param("stars", "exposure"), 1e-12, "and the range is enforced");
        }

        // ------------------------------------------------------------------------ helpers

        private static LayerContext Context(LoadedKinds kinds) =>
            new LayerContext(kinds, Stage.Real, 1f, false, false, 0.0, double.PositiveInfinity,
                0f, 0f, 0f, false, 0f, double.PositiveInfinity, false);

        private static byte ByteFor(VoxelGrid grid, double value)
        {
            for (int b = 1; b < 256; b++)
                if (grid.Decode((byte)b) >= value - 1e-12) return (byte)b;
            return 255;
        }

        private static VoxelGrid ConstantGrid(double density)
        {
            const int n = 21;
            VoxelGrid probe = new VoxelGrid(n, n, n, new Double3(-1000, -1000, -400),
                new Double3(100, 100, 40), 1e-5, Math.Log(1e4), new byte[n * n * n]);
            byte value = ByteFor(probe, density);
            byte[] raw = new byte[n * n * n];
            for (int i = 0; i < raw.Length; i++) raw[i] = value;
            return new VoxelGrid(n, n, n, new Double3(-1000, -1000, -400),
                new Double3(100, 100, 40), 1e-5, Math.Log(1e4), raw);
        }

        /// <summary>A deterministic cloud a few hundred parsecs across, like the real catalogue.</summary>
        private static Double3[] SpreadOfStars(int count, uint seed)
        {
            Double3[] points = new Double3[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = new Double3(
                    (Next(ref seed) - 0.5) * 400.0,
                    (Next(ref seed) - 0.5) * 400.0,
                    (Next(ref seed) - 0.5) * 400.0);
            }
            return points;
        }

        private static double Next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFFu) / 16777216.0;
        }
    }
}
