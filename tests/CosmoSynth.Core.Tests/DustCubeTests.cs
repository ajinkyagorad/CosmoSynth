using System;
using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The shipped dust cube: galaxy/dust.csdu, the file that actually goes into the APK.
    ///
    /// Every expectation here comes from somewhere the cube does not: the named-cloud distances
    /// are Zucker et al. 2020's, read out of the shipped namedclouds.json, and the geometry
    /// checks are properties of the Galaxy rather than of this reconstruction. That matters,
    /// because a test whose expected value is read from the artefact under test only proves the
    /// artefact is self-consistent.
    /// </summary>
    public static class DustCubeTests
    {
        private static DustCube _cube;

        private static DustCube Cube =>
            _cube ?? (_cube = DustCube.Parse(Shipped.Bytes("galaxy", "dust.csdu")));

        private static PlaceCatalog _clouds;

        private static PlaceCatalog Clouds =>
            _clouds ?? (_clouds = PlaceCatalog.Parse(Shipped.Text("galaxy", "namedclouds.json")));

        [Test("the shipped dust cube parses, and its grid is the one the meta declares")]
        public static void GridGeometry()
        {
            VoxelGrid grid = Cube.Grid;
            Assert.True(grid.Nx == 256 && grid.Ny == 256 && grid.Nz == 256,
                $"expected 256^3, got {grid.Nx}x{grid.Ny}x{grid.Nz}");
            Assert.Equal(10.0, grid.Step.x, 1e-9, "voxel size in x, pc");
            Assert.Equal(10.0, grid.Step.y, 1e-9, "voxel size in y, pc");
            Assert.Equal(10.0, grid.Step.z, 1e-9, "voxel size in z, pc");
            Assert.Equal(-1275.0, grid.Min.x, 1e-6, "the box starts at -1275 pc");
            Assert.Equal(1275.0, grid.Max.x, 1e-6, "and ends at +1275 pc");
            Assert.True(grid.Frame == "galactic-helio",
                $"the cube must be in the heliocentric galactic frame, meta says '{grid.Frame}'");
            Assert.True(grid.VoxelCount == 16777216L,
                $"expected 16,777,216 voxels, got {grid.VoxelCount}");
            // The unit is the source's own words, kept verbatim so nobody calls it magnitudes.
            Assert.True(grid.Unit.Contains("Zhang"),
                "the unit must name the Zhang, Green & Rix extinction parameter, got " + grid.Unit);
            Assert.True(Cube.Attribution().Contains("Edenhofer"),
                "the CC BY 4.0 attribution must name the authors");
        }

        [Test("dust density spans the range a Galactic extinction map should, and no more")]
        public static void DensityRange()
        {
            double peak = Cube.PeakDensity();
            // E per parsec. A peak below 0.01 would mean the log ramp lost the dense cores; a
            // peak above 0.2 would mean the decode is wrong by orders of magnitude. The source's
            // own maximum is 0.0575.
            Assert.True(peak > 0.01 && peak < 0.2,
                $"peak density {peak:G4} E/pc is outside the range a dust map can have");
            Assert.Equal(0.0, Cube.DensityAt(new Double3(0, 0, 100000)), 0.0,
                "100 kpc above the plane is outside the box and must read exactly zero");
            Assert.Equal(0.0, Cube.DensityAt(new Double3(1274, 1274, 1274)), 0.0,
                "a corner of the box is outside the mapped sphere");
            Assert.False(Cube.IsMapped(new Double3(1274, 1274, 1274)),
                "the corners of the box were never mapped, and the cube must say so rather " +
                "than let a raymarcher read the encoded zero there as clear sky");
            Assert.True(Cube.IsMapped(new Double3(0, 0, 0)),
                "the Sun's own voxel is inside the map");
            Assert.True(Cube.DensityAt(Double3.Zero) > 0.0,
                "the Sun sits in the Local Bubble, not in a vacuum with no value at all");
        }

        [Test("extinction along a ray does not depend on which end it is traced from")]
        public static void ExtinctionIsSymmetric()
        {
            var a = new Double3(-300, 120, -60);
            var b = new Double3(500, -340, 90);
            double forward = Cube.ExtinctionAlong(a, b);
            double backward = Cube.ExtinctionAlong(b, a);
            Assert.True(forward > 0.0, "this ray should cross some dust");
            Assert.Relative(forward, backward, 1e-9,
                "an asymmetric integrator makes an object's extinction depend on the order the " +
                "renderer happened to pass the endpoints in");
        }

        [Test("halving the integration step changes the extinction by a few per cent, not a factor")]
        public static void ExtinctionConverges()
        {
            var target = new Double3(-380, -190, -135);         // toward the Orion complex
            double coarse = Cube.ExtinctionToward(target, 10.0);
            double fine = Cube.ExtinctionToward(target, 2.5);
            Assert.True(coarse > 0.0, "the Orion sightline must have extinction on it");
            Assert.Relative(fine, coarse, 0.10,
                $"10 pc steps gave {coarse:G4} and 2.5 pc steps {fine:G4}; more than 10% apart " +
                "means the integrator is not resolving the field it is integrating");
        }

        [Test("named clouds appear as density peaks at the distances another survey measured")]
        public static void CloudsPeakAtTheirPublishedDistances()
        {
            // Zucker et al. 2020 distances, out of the shipped places file. The dust map was
            // reconstructed from stellar extinctions and never saw this catalogue.
            string[] wanted = { "orion", "taurus", "perseus", "california", "chamaeleon",
                                "coalsack" };
            var misses = new List<string>();
            foreach (string id in wanted)
            {
                Place place = Clouds[id];
                Assert.True(place != null, $"namedclouds.json has no '{id}'");
                Double3 direction = PlaceCatalog.GalacticPc(place).Normalized;

                double best = -1.0, bestDistance = 0.0, total = 0.0;
                int samples = 0;
                for (double d = 60.0; d < Math.Min(1240.0, place.DistancePc * 2.4); d += 10.0)
                {
                    double value = Cube.DensityAt(direction * d);
                    total += value;
                    samples++;
                    if (value > best) { best = value; bestDistance = d; }
                }
                double mean = total / samples;
                double fractionalMiss = Math.Abs(bestDistance - place.DistancePc) / place.DistancePc;
                if (fractionalMiss > 0.15 || best < 5.0 * mean)
                {
                    misses.Add($"{place.Name}: published {place.DistancePc:F0} pc, peak " +
                               $"{bestDistance:F0} pc ({100 * fractionalMiss:F1}%), " +
                               $"{best / mean:F0}x the sightline mean");
                }
            }
            Assert.True(misses.Count == 0,
                "clouds that did not land where an independent survey puts them:\n  " +
                string.Join("\n  ", misses));
        }

        [Test("the cube's axis signs are the identity, not one of the seven flips")]
        public static void AxisSignsWinAgainstEveryFlip()
        {
            // Design B section 2.7 step 4, recomputed in Core so the importer's numpy check is
            // re-proved by the code that will actually read the file on a headset.
            var clouds = new List<Double3>();
            foreach (Place place in Clouds.Places)
            {
                if (place.DistancePc > 900.0) continue;         // stay well inside the map
                clouds.Add(PlaceCatalog.GalacticPc(place));
            }
            // 59 of the 94 named clouds are inside 900 pc, which is where the reconstruction
            // has enough stars behind it to be worth testing against.
            Assert.True(clouds.Count >= 50, $"only {clouds.Count} clouds inside the map");

            double best = double.NegativeInfinity, second = double.NegativeInfinity;
            string bestFlip = null;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        double total = 0.0;
                        foreach (Double3 p in clouds)
                        {
                            double value = Cube.DensityAt(new Double3(sx * p.x, sy * p.y, sz * p.z));
                            total += Math.Log(Math.Max(value, 1e-12));
                        }
                        double score = total / clouds.Count;
                        string flip = $"{sx:+0;-0}{sy:+0;-0}{sz:+0;-0}";
                        if (score > best) { second = best; best = score; bestFlip = flip; }
                        else if (score > second) second = score;
                    }
            Assert.True(bestFlip == "+1+1+1",
                $"the best axis assignment is {bestFlip}, not the identity: the cube is mirrored");
            // Mean log density, so the margin is a log ratio. ln 1.5 is the contract's 1.5x.
            Assert.True(best - second >= Math.Log(1.5),
                $"the identity only beats the next flip by {Math.Exp(best - second):F2}x, and " +
                "the contract asks for at least 1.5x before the axes count as pinned");
        }

        [Test("the Sun sits in a cavity, and the dust lies in a thin disc around it")]
        public static void LocalBubbleAndDiscFlattening()
        {
            // Two facts about the Galaxy, not about this reconstruction: the Sun is inside the
            // Local Bubble, and molecular dust is confined near the plane. Either would break if
            // the axes, the units or the origin were wrong.
            double inner = MeanOverShell(0.0, 60.0);
            double wall = MeanOverShell(100.0, 400.0);
            Assert.True(wall > 3.0 * inner,
                $"mean density within 60 pc is {inner:G4} and between 100 and 400 pc is " +
                $"{wall:G4}: the Local Bubble cavity is not there");

            double near = MeanOverSlab(0.0, 100.0);
            double far = MeanOverSlab(400.0, 900.0);
            Assert.True(near > 10.0 * far,
                $"mean density within 100 pc of the plane is {near:G4} and beyond 400 pc is " +
                $"{far:G4}: the disc is not flattened, so z is not the pole");
        }

        private static double MeanOverShell(double r0, double r1)
        {
            double total = 0.0;
            int n = 0;
            var random = new Random(20260912);
            while (n < 20000)
            {
                var p = new Double3(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1);
                double m = p.Magnitude;
                if (m <= 0.0 || m > 1.0) continue;
                Double3 q = p / m * (r0 + (r1 - r0) * random.NextDouble());
                total += Cube.DensityAt(q);
                n++;
            }
            return total / n;
        }

        private static double MeanOverSlab(double z0, double z1)
        {
            double total = 0.0;
            int n = 0;
            var random = new Random(20260912);
            while (n < 20000)
            {
                double x = (random.NextDouble() * 2 - 1) * 600.0;
                double y = (random.NextDouble() * 2 - 1) * 600.0;
                double z = (z0 + (z1 - z0) * random.NextDouble()) * (random.Next(2) == 0 ? 1 : -1);
                var q = new Double3(x, y, z);
                if (!Cube.IsMapped(q)) continue;
                total += Cube.DensityAt(q);
                n++;
            }
            return total / n;
        }

        [Test("a file that is not a dust cube is refused, not read as one")]
        public static void RefusesWrongFiles()
        {
            try
            {
                DustCube.Parse(Shipped.Bytes("galaxy", "globulars.csgp"));
                throw new AssertionException("a CSGP point set was accepted as a dust cube");
            }
            catch (FormatException)
            {
            }

            var random = new byte[4096];
            new Random(7).NextBytes(random);
            try
            {
                DustCube.Parse(random);
                throw new AssertionException("random bytes were accepted as a dust cube");
            }
            catch (FormatException)
            {
            }
        }

        [Test("trilinear sampling interpolates the decoded density, not the log codes")]
        public static void TrilinearInterpolatesDecodedValues()
        {
            VoxelGrid grid = Cube.Grid;
            // Find a pair of adjacent nodes with real contrast, then check the midpoint is their
            // arithmetic mean. Interpolating the bytes and decoding afterwards would give the
            // geometric mean instead, which is smaller wherever the field has structure.
            for (int iz = 100; iz < 160; iz++)
                for (int iy = 100; iy < 160; iy++)
                    for (int ix = 100; ix < 160; ix++)
                    {
                        double a = grid.Node(ix, iy, iz);
                        double b = grid.Node(ix + 1, iy, iz);
                        if (a <= 0.0 || b <= 0.0 || b < 4.0 * a) continue;
                        var mid = new Double3(
                            grid.Origin.x + grid.Step.x * (ix + 0.5),
                            grid.Origin.y + grid.Step.y * iy,
                            grid.Origin.z + grid.Step.z * iz);
                        double sample = grid.SampleTrilinear(mid);
                        Assert.Relative(0.5 * (a + b), sample, 1e-6,
                            "the midpoint of two nodes must be their arithmetic mean");
                        double geometric = Math.Sqrt(a * b);
                        Assert.True(Math.Abs(sample - geometric) > 1e-9,
                            "this pair has too little contrast to tell the two means apart");
                        return;
                    }
            throw new AssertionException("no adjacent node pair with a factor of four between " +
                                         "them was found, so this test proved nothing");
        }
    }
}
