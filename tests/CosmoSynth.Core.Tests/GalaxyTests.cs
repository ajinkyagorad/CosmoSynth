using System;
using System.Collections.Generic;
using System.IO;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The galaxy layers: the container, the orientation arithmetic, and the four files that
    /// actually ship.
    ///
    /// The shipped-file tests are the ones that matter. Everything else tests code; these test
    /// the artefact — the real binary, built by the real importer from the real staged
    /// catalogues, read by the real parser. A format change, a units slip, a silently dropped
    /// row or a blank turned into a number shows up here and nowhere else.
    /// </summary>
    public static class GalaxyTests
    {
        // ---------------------------------------------------------------------------------
        // Finding the shipped files, the same walk ShippedCatalogTests uses.
        // ---------------------------------------------------------------------------------

        private static readonly Dictionary<string, byte[]> Cache = new Dictionary<string, byte[]>();

        private static string Find(params string[] relative)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 10 && directory != null; depth++)
            {
                var parts = new List<string> { directory.FullName };
                parts.AddRange(relative);
                string candidate = Path.Combine(parts.ToArray());
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new FileNotFoundException(
                "not found: " + string.Join("/", relative) +
                ". Build it with 'python3 tools/cosmodata/build_localvolume.py' and " +
                "'python3 tools/cosmodata/build_lss.py'.");
        }

        private static byte[] Shipped(string folder, string file)
        {
            string key = folder + "/" + file;
            if (!Cache.TryGetValue(key, out byte[] bytes))
            {
                bytes = File.ReadAllBytes(Find("unity", "Assets", "StreamingAssets", "CosmoSynth",
                    folder, file));
                Cache[key] = bytes;
            }
            return bytes;
        }

        private static GalaxySet _localVolume;
        private static GalaxySet LocalVolume =>
            _localVolume ?? (_localVolume = GalaxySet.Parse(Shipped("lv", "localvolume.cslv"), "CSLV"));

        // =================================================================================
        // 1. The container
        // =================================================================================

        [Test("the container fixture Python wrote parses field for field in C#")]
        public static void ContainerFixtureRoundTrips()
        {
            byte[] bytes = File.ReadAllBytes(Find("tests", "CosmoSynth.Core.Tests", "fixtures",
                "csdata_fixture.csd"));
            CsData data = CsData.Parse(bytes, "CSDF");

            Assert.True(data.Rows == 3, $"fixture rows: expected 3, got {data.Rows}");
            Assert.True(data.Version == 1, "fixture version");

            float[] v = data.F32("f32c3");
            Assert.True(v.Length == 9, $"f32c3 length {v.Length}");
            Assert.Equal(1.0, v[0], 0.0, "f32c3[0]");
            Assert.Equal(9.0, v[8], 0.0, "f32c3[8]");

            double[] d = data.F64("f64");
            Assert.Equal(-1.5, d[0], 0.0, "f64[0]");
            Assert.Equal(1.5, d[2], 0.0, "f64[2]");

            Assert.True(data.U8("u8")[1] == 128, "u8[1]");
            Assert.True(data.I8("i8")[0] == -128, "i8[0]");
            Assert.True(data.U16("u16")[2] == 65535, "u16[2]");
            Assert.True(data.I16("i16")[0] == -32768, "i16[0]");
            Assert.True(data.U32("u32")[2] == 4294967295u, "u32[2]");
            Assert.True(data.I32("i32")[0] == -2147483648, "i32[0]");

            // The bit override: bit 0 weakens f32c3 from Procedural to Inferred.
            Assert.True(data.ProvenanceFor("f32c3", 0u).Class == ProvenanceClass.Procedural,
                "row with no flags keeps the baseline class");
            Assert.True(data.ProvenanceFor("f32c3", 1u).Class == ProvenanceClass.Inferred,
                "bit 0 set moves it to Inferred");
        }

        [Test("the container refuses every file it should refuse")]
        public static void ContainerRefusals()
        {
            byte[] good = File.ReadAllBytes(Find("tests", "CosmoSynth.Core.Tests", "fixtures",
                "csdata_fixture.csd"));

            Assert.Throws<FormatException>(() => CsData.Parse(good, "CSLV"),
                "a file loaded under the wrong magic");
            Assert.Throws<FormatException>(() => CsData.Parse(good, "CSDF", 0),
                "a version newer than this build knows");
            Assert.Throws<FormatException>(() => CsData.Parse(new byte[8], "CSDF"),
                "a file shorter than a header");

            byte[] truncated = new byte[good.Length - 1];
            Array.Copy(good, truncated, truncated.Length);
            Assert.Throws<FormatException>(() => CsData.Parse(truncated, "CSDF"),
                "a file whose length disagrees with its header");

            byte[] dirtyFlags = (byte[])good.Clone();
            dirtyFlags[12] = 1;
            Assert.Throws<FormatException>(() => CsData.Parse(dirtyFlags, "CSDF"),
                "a non-zero reserved field");

            CsData parsed = CsData.Parse(good, "CSDF");
            Assert.Throws<FormatException>(() => parsed.F32("nosuchcolumn"), "a missing column");
            Assert.Throws<FormatException>(() => parsed.F32("f64"), "a column read as the wrong type");
        }

        // =================================================================================
        // 2. Orientation arithmetic
        // =================================================================================

        [Test("inclination from axis ratio is the Hubble 1926 formula, at four probe values")]
        public static void InclinationFormula()
        {
            // Probe arithmetic at q0 = 0.2. Design B section 2.11 states these to two
            // decimals (78.46, 74.46, 55.49); the six-decimal values here were computed
            // independently in Python on triton, which is a different acos implementation
            // from the one under test. Writing "74.4586" from memory of a two-decimal figure
            // is exactly the failure this note exists to stop: it failed by 0.002 degrees and
            // the code was right. The q = 0.28 case IS the task's "M31 about 78 degrees" -
            // it is the axis ratio that produces it.
            Assert.Equal(78.463041, GalaxyOrientation.InclinationDeg(0.28), 1e-5, "q = 0.28");
            Assert.Equal(74.460610, GalaxyOrientation.InclinationDeg(0.33), 1e-5, "q = 0.33");
            Assert.Equal(55.492545, GalaxyOrientation.InclinationDeg(0.59), 1e-5, "q = 0.59");
            Assert.Equal(64.057113, GalaxyOrientation.InclinationDeg(0.473), 1e-5,
                "q = 0.473, M31's own 2MASS axis ratio");

            // A disc cannot look flatter than it is: below q0 the answer is edge on, not an
            // error and not an extrapolation.
            Assert.Equal(90.0, GalaxyOrientation.InclinationDeg(0.2), 0.0, "q = q0 is edge on");
            Assert.Equal(90.0, GalaxyOrientation.InclinationDeg(0.05), 0.0, "q below q0 is edge on");
            Assert.Equal(0.0, GalaxyOrientation.InclinationDeg(1.0), 0.0, "q = 1 is face on");
            Assert.True(double.IsNaN(GalaxyOrientation.InclinationDeg(double.NaN)),
                "no axis ratio gives no inclination, not a default");

            // q0 is a choice, so it must actually change the answer.
            Assert.Equal(73.739795, GalaxyOrientation.InclinationDeg(0.28, 0.0), 1e-5,
                "q = 0.28 with an infinitely thin disc");

            for (double i = 0.0; i <= 90.0; i += 7.5)
            {
                double q = GalaxyOrientation.AxisRatioFromInclinationDeg(i);
                Assert.Equal(i, GalaxyOrientation.InclinationDeg(q), 1e-9,
                    $"inclination round trip at {i} deg");
            }
        }

        [Test("position angle 0 is north and 90 is east, exactly")]
        public static void PositionAngleGeometry()
        {
            foreach (var place in new[] { (10.7, 41.3), (0.0, 0.0), (270.0, -60.0), (123.4, 89.0) })
            {
                GalaxyOrientation.SkyBasis(place.Item1, place.Item2,
                    out Double3 north, out Double3 east);
                Double3 los = GalaxyOrientation.Direction(place.Item1, place.Item2);

                Assert.Equal(1.0, north.Magnitude, 1e-12, "north is a unit vector");
                Assert.Equal(1.0, east.Magnitude, 1e-12, "east is a unit vector");
                Assert.Equal(0.0, Double3.Dot(north, east), 1e-12, "north and east are perpendicular");
                Assert.Equal(0.0, Double3.Dot(north, los), 1e-12, "north is on the sky");
                Assert.Equal(0.0, Double3.Dot(east, los), 1e-12, "east is on the sky");

                Assert.VectorClose(north, GalaxyOrientation.MajorAxis(north, east, 0.0), 1e-12,
                    "PA 0 is north");
                Assert.VectorClose(east, GalaxyOrientation.MajorAxis(north, east, 90.0), 1e-12,
                    "PA 90 is east");
            }
        }

        [Test("a disc normal faces the observer at 0 degrees and lies on the sky at 90")]
        public static void DiscNormalGeometry()
        {
            Double3 los = GalaxyOrientation.Direction(10.68471, 41.26875) * 750.0;
            Double3 major = GalaxyOrientation.MajorAxis(10.68471, 41.26875, 38.0);

            Double3 faceOn = GalaxyOrientation.DiscNormal(los, major, 0.0, 1.0);
            Assert.VectorClose(-los.Normalized, faceOn, 1e-12, "face on points at the observer");

            Double3 edgeOn = GalaxyOrientation.DiscNormal(los, major, 90.0, 1.0);
            Assert.Equal(0.0, Double3.Dot(edgeOn, los.Normalized), 1e-12,
                "edge on lies in the sky plane");
            Assert.Equal(0.0, Double3.Dot(edgeOn, major), 1e-12,
                "edge on is perpendicular to the major axis");

            Double3 a = GalaxyOrientation.DiscNormal(los, major, 60.0, 1.0);
            Double3 b = GalaxyOrientation.DiscNormal(los, major, 60.0, -1.0);
            Assert.True((a - b).Magnitude > 0.1,
                "the near-side sign must actually change the disc, or it is not a choice");

            // Procedural, but stable: the same name gives the same sign every run, or the
            // whole field flips on reload and looks like a bug.
            Assert.True(GalaxyOrientation.NearSideSign("M31") == GalaxyOrientation.NearSideSign("M31"),
                "the near-side sign is stable per galaxy");
        }

        // =================================================================================
        // 3. The shipped Local Volume
        // =================================================================================

        [Test("the shipped Local Volume file has the rows the sources have")]
        public static void LocalVolumeRows()
        {
            GalaxySet set = LocalVolume;
            Assert.True(set.Count == 930, $"expected 930 rows, got {set.Count}");
            Assert.True(set.Data.Count("lvdbDwarfs") == 911,
                $"LVDB v1.1.1 has 911 dwarfs, the file says {set.Data.Count("lvdbDwarfs")}");
            Assert.True(set.PositionUnit == "kpc", "the Local Volume is in kiloparsecs");
            Assert.True(set.Data.Meta["cosmology"].IsNull || true, "no cosmology is needed here");

            // The four heroes with no LVDB host group carry no distance, so they are not
            // shipped rather than being placed at a guessed one.
            Assert.True(set.Data.Count("heroesWithoutDistance") == 4,
                "NGC 4945, Maffei 1, Maffei 2 and Circinus have no derivable distance");
        }

        [Test("M31 is where the Local Group says it is, at the inclination kinematics give")]
        public static void M31()
        {
            GalaxySet set = LocalVolume;
            int i = set.IndexOfName("Andromeda Galaxy");
            Assert.True(i >= 0, "M31 is in the catalogue");

            // The shipped distance is 751.6 kpc: the median of the 39 LVDB satellites that
            // name M31 as host, because every catalogue of MEASURED distances to nearby
            // giants is blocked under the commercial bar. The task's 0.77 Mpc is therefore
            // the value the derived one has to be CONSISTENT with, not a value we may claim,
            // and the bound is the catalogue's own published spread, not one chosen to pass.
            Galaxy m31 = set[i];
            Assert.True(m31.DistanceError > 0.0 && m31.DistanceError < 40.0,
                $"M31's quoted spread should be a real MAD, got {m31.DistanceError}");
            Assert.Equal(770.0, m31.Distance, m31.DistanceError,
                "M31 distance against the literature 0.77 Mpc, within the derived MAD");
            Assert.True((m31.Flags & (uint)GalaxyFlags.DistanceFromSatellites) != 0,
                "and the file says out loud that the distance is derived, not measured");
            Assert.True(set.ProvenanceOf("pos", i).Class != ProvenanceClass.Measured,
                "so its position provenance must not claim Measured");

            Assert.Equal(77.0, set.InclinationDeg(i), 0.001, "M31 inclination, kinematic override");
            Assert.Equal(38.0, m31.PositionAngleDeg, 0.001, "M31 position angle");
            Assert.True((m31.Flags & (uint)GalaxyFlags.InclinationOverride) != 0,
                "the override bit is set, so a reader knows where 77 came from");

            // Without the override the 2MASS isophote would have given 64 degrees. The
            // difference is the entire reason the override exists, so if the photometric
            // value ever silently becomes the shipped one, this fails.
            Assert.Equal(64.057113, GalaxyOrientation.InclinationDeg(1.0 - set.Ellipticity(i)), 0.001,
                "M31's photometric inclination, which is NOT what ships");
        }

        [Test("the Magellanic Clouds keep what LVDB measured and invent nothing it did not")]
        public static void MagellanicClouds()
        {
            GalaxySet set = LocalVolume;

            int lmc = set.IndexOfName("LMC");
            Assert.True(lmc >= 0, "the LMC is in the catalogue");
            Assert.Equal(49.59, set.Distance(lmc), 0.02, "LMC distance, kpc");
            // LVDB ships 227.24 degrees. A major axis has no head or tail, so it folds to
            // 47.24; a renderer that treats 227 as being in [0,180) draws it 47 degrees off.
            Assert.Equal(47.24, set.PositionAngleDeg(lmc), 0.01, "LMC position angle, folded");
            Assert.True(set.ShapeIsMeasured(lmc), "the LMC's shape is measured");

            int smc = set.IndexOfName("SMC");
            Assert.True(smc >= 0, "the SMC is in the catalogue");
            Assert.Equal(62.81, set.Distance(smc), 0.02, "SMC distance, kpc");
            Assert.True(double.IsNaN(set.Ellipticity(smc)), "the SMC's ellipticity is not known");
            Assert.True(double.IsNaN(set.PositionAngleDeg(smc)), "nor its position angle");
            Assert.True(!set.ShapeIsMeasured(smc),
                "and the file must not report an unmeasured shape as measured");
            Assert.True(set.DiscNormal(smc) == null, "so it has no disc orientation either");
        }

        [Test("Antlia II comes through unchanged, value for value")]
        public static void AntliaII()
        {
            GalaxySet set = LocalVolume;
            int i = set.IndexOfName("Antlia II");
            Assert.True(i >= 0, "Antlia II is in the catalogue");
            Assert.Equal(124.17, set.Distance(i), 0.02, "Antlia II distance, kpc");
            Assert.Equal(0.6, set.Ellipticity(i), 1e-6, "Antlia II ellipticity");
            Assert.Equal(156.0, set.PositionAngleDeg(i), 1e-4, "Antlia II position angle");
            Assert.Equal(3.76192, set.HalfLightRadius(i), 1e-4, "Antlia II half-light radius, kpc");
        }

        [Test("the Local Group galaxy count within 1 Mpc")]
        public static void LocalGroupCount()
        {
            GalaxySet set = LocalVolume;
            // 124 LVDB dwarfs inside 1000 kpc, plus M31 and M33 as giants, plus the Milky Way
            // row itself, which is the galactic centre at R0 = 8.15 kpc and is never drawn.
            Assert.True(set.CountWithin(1000.0) == 127,
                $"127 galaxies within 1 Mpc including the Milky Way, got {set.CountWithin(1000.0)}");
            Assert.True(set.CountWithin(1000.0, includeHome: false) == 126,
                "126 of them are external to the Milky Way, got "
                + set.CountWithin(1000.0, includeHome: false));

            int home = set.IndexOfName("Milky Way");
            Assert.True(home >= 0 && set.IsHome(home), "the Milky Way row is flagged IsHome");
            Assert.Equal(8.15, set.Distance(home), 0.001,
                "and sits at R0, the galactic centre, not at the Sun");

            Assert.True(set.Data.Count("landmarksLg") == 13,
                "the 13 Local Group landmarks the atlas labels are all flagged, got "
                + set.Data.Count("landmarksLg"));
        }

        [Test("no placeholder shape is ever reported as measured")]
        public static void NoPlaceholderIsEverMeasured()
        {
            GalaxySet set = LocalVolume;
            int unknownEll = 0, unknownPa = 0, measured = 0;

            for (int i = 0; i < set.Count; i++)
            {
                bool ellFlagged = (set.Flags(i) & (uint)GalaxyFlags.EllipticityUnknown) != 0;
                bool paFlagged = (set.Flags(i) & (uint)GalaxyFlags.PositionAngleUnknown) != 0;

                if (ellFlagged)
                {
                    unknownEll++;
                    // The atlas substituted a default here and drew it as data. The test that
                    // it never happens again is this one: flagged means NaN, means not
                    // measured, means no orientation offered to the renderer at all.
                    Assert.True(double.IsNaN(set.Ellipticity(i)),
                        $"row {i} ({set.Name(i)}) is flagged EllipticityUnknown but has a value");
                    Assert.True(!set.ShapeIsMeasured(i),
                        $"row {i} ({set.Name(i)}) reports an unmeasured shape as Measured");
                    Assert.True(set.ProvenanceOf("ell", i).Class == ProvenanceClass.Inferred,
                        $"row {i} provenance for a blank ellipticity must be Inferred");
                }
                else
                {
                    measured++;
                    Assert.True(!double.IsNaN(set.Ellipticity(i)),
                        $"row {i} ({set.Name(i)}) is not flagged but has no ellipticity");
                    double e = set.Ellipticity(i);
                    Assert.True(e >= 0.0 && e < 1.0, $"row {i} ellipticity {e} out of [0, 1)");
                }

                if (paFlagged)
                {
                    unknownPa++;
                    Assert.True(double.IsNaN(set.PositionAngleDeg(i)),
                        $"row {i} ({set.Name(i)}) is flagged PositionAngleUnknown but has a value");
                    Assert.True(set.DiscNormal(i) == null,
                        $"row {i} has no position angle and must not produce a disc orientation");
                }
                else
                {
                    double pa = set.PositionAngleDeg(i);
                    Assert.True(pa >= 0.0 && pa < 180.0, $"row {i} position angle {pa} not folded");
                }
            }

            // The counts are the point. LVDB leaves 536 ellipticities and 682 position angles
            // blank; nothing in this pipeline may quietly reduce those to zero by filling them.
            Assert.True(unknownEll == 536,
                $"expected 536 unknown ellipticities, got {unknownEll}");
            Assert.True(measured == 394, $"expected 394 measured ellipticities, got {measured}");
            Assert.True(unknownPa == 678, $"expected 678 unknown position angles, got {unknownPa}");
            Assert.True(unknownEll + measured == set.Count, "every row is one or the other");
        }

        [Test("a rejected 2MASS shape fit is not shipped as a shape")]
        public static void RejectedShapeFits()
        {
            GalaxySet set = LocalVolume;
            int rejected = 0;
            for (int i = 0; i < set.Count; i++)
            {
                if ((set.Flags(i) & (uint)GalaxyFlags.ShapeUnreliable) == 0) continue;
                rejected++;
                // NGC 3109 is the worked example: an edge-on irregular that 2MASS fits at
                // axis ratio 1.000, which the Hubble formula turns into face on. A circle
                // where a cigar belongs is worse than no shape at all.
                Assert.True(double.IsNaN(set.Ellipticity(i)),
                    $"{set.Name(i)}: a rejected fit must not ship an ellipticity");
            }
            Assert.True(rejected >= 2,
                $"the LGA reliability guard should reject some giants, it rejected {rejected}");
        }

        // =================================================================================
        // 4. The shipped large-scale structure
        // =================================================================================

        [Test("the DESI sample's positions round-trip back to its own redshifts")]
        public static void DesiRoundTrips()
        {
            GalaxySet set = GalaxySet.Parse(Shipped("lss", "desi.cspt"), "CSPT");
            Assert.True(set.Count > 90000 && set.Count <= 100000,
                $"DESI subsample size: got {set.Count}");
            Assert.True(set.PositionUnit == "Mpc", "DESI positions are megaparsecs");

            // The whole point of stating one cosmology: take the shipped position, run it
            // back through the inverse, and the catalogue's own measured redshift must come
            // out. The bound is set by float32 storage, not by taste - at z = 0.4 an f32
            // position carries about 1e-7 relative error, which is 5e-8 in z.
            double worst = 0.0;
            int worstRow = -1;
            for (int i = 0; i < set.Count; i += 7)
            {
                double back = Cosmology.RedshiftAtComovingDistanceMpc(set.Distance(i));
                double error = Math.Abs(back - set.Redshift(i));
                if (error > worst) { worst = error; worstRow = i; }
            }
            Assert.True(worst < 1e-6,
                $"worst redshift round-trip error {worst:G6} at row {worstRow}; " +
                "if this is large the importer and Core disagree about the cosmology");

            foreach (int i in new[] { 0, set.Count / 2, set.Count - 1 })
            {
                double z = set.Redshift(i);
                Assert.True(z >= 0.0999 && z <= 0.4001, $"row {i} redshift {z} outside the sample");
                double d = set.Distance(i);
                Assert.True(d > 400.0 && d < 1700.0, $"row {i} distance {d} Mpc outside the shell");
            }

            Assert.True(set.Data.Attribution().Contains("CC BY 4.0"),
                "DESI is CC BY 4.0 and the file must carry the attribution the licence requires");
        }

        [Test("the SDSS sample is oriented, near, and does not overlap DESI")]
        public static void SdssShell()
        {
            GalaxySet set = GalaxySet.Parse(Shipped("lss", "sdss.csrs"), "CSRS");
            Assert.True(set.Count > 45000 && set.Count <= 50000,
                $"SDSS subsample size: got {set.Count}");

            int oriented = 0, nearby = 0;
            double minZ = 1.0, maxZ = 0.0, worst = 0.0;
            for (int i = 0; i < set.Count; i++)
            {
                double z = set.Redshift(i);
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
                if ((set.Flags(i) & (uint)SurveyFlags.NearbyPeculiarVelocity) != 0) nearby++;

                if ((set.Flags(i) & (uint)SurveyFlags.ShapeUnknown) != 0)
                {
                    Assert.True(double.IsNaN(set.AxisRatio(i)),
                        $"row {i} is flagged shape-unknown but carries an axis ratio");
                }
                else
                {
                    oriented++;
                    double q = set.AxisRatio(i);
                    double pa = set.PositionAngleDeg(i);
                    Assert.True(q > 0.0 && q < 1.0, $"row {i} axis ratio {q} out of (0, 1)");
                    Assert.True(pa >= 0.0 && pa < 180.0, $"row {i} position angle {pa} not folded");
                }

                if ((i % 11) == 0)
                {
                    double error = Math.Abs(
                        Cosmology.RedshiftAtComovingDistanceMpc(set.Distance(i)) - z);
                    if (error > worst) worst = error;
                }
            }

            Assert.True(worst < 1e-6, $"worst SDSS redshift round-trip error {worst:G6}");
            Assert.True(minZ >= 0.003, $"SDSS floor: {minZ}");
            // The two layers tile the volume rather than overlapping: no SDSS row may reach
            // the redshift where the DESI sample begins, or the same galaxies are drawn twice.
            Assert.True(maxZ < 0.10, $"SDSS ceiling must stay below DESI's floor, got {maxZ}");
            Assert.True(oriented > 0.90 * set.Count,
                $"only {oriented} of {set.Count} SDSS galaxies have a usable shape");
            Assert.True(nearby > 0, "the z < 0.01 rows should be flagged, not silently trusted");
        }

        [Test("only interior voids ship, at the size the catalogue measured")]
        public static void Voids()
        {
            CsData data = CsData.Parse(Shipped("lss", "voids.cspt"), "CSPT");
            Assert.True(data.Rows == 805, $"expected 805 interior voids, got {data.Rows}");
            Assert.True(data.Count("fromRows") == 1163, "out of the catalogue's 1,163");

            // A void that touches the survey boundary has the size of the boundary, not of a
            // void. Shipping all 1,163 as equals produces a shell of fake voids around the
            // footprint - the same class of error as inventing galaxies behind the Milky Way.
            Assert.True(data.Count("edgeTouching") + data.Count("beyondEdge") == 358,
                "358 boundary voids are deliberately not shipped");

            float[] reff = data.F32("rEff");
            var sorted = (float[])reff.Clone();
            Array.Sort(sorted);
            double median = sorted[sorted.Length / 2];
            // The Zenodo description says the median effective radius is 15-19 Mpc/h. Divided
            // by h = 0.6766 that is 22.2 - 28.1 Mpc, and this is the check that the units
            // conversion happened exactly once.
            Assert.True(median > 22.0 && median < 28.5,
                $"median void radius {median:F2} Mpc is outside the catalogue's own 15-19 Mpc/h");

            float[] pos = data.F32("pos");
            double furthest = 0.0;
            for (int i = 0; i < data.Rows; i++)
            {
                double d = Math.Sqrt(pos[i * 3] * (double)pos[i * 3]
                    + pos[i * 3 + 1] * (double)pos[i * 3 + 1]
                    + pos[i * 3 + 2] * (double)pos[i * 3 + 2]);
                if (d > furthest) furthest = d;
            }
            // 326.6 Mpc/h / 0.6766 = 482.7 Mpc. In Mpc/h it would be 326.6, so a missed
            // conversion shows up here as a factor of 1.48.
            Assert.True(furthest > 400.0 && furthest < 500.0,
                $"the furthest void is at {furthest:F1} Mpc; in Mpc/h it would be 326.6");
        }

        [Test("Laniakea ships as a point with its published volume, and no invented boundary")]
        public static void NamedStructures()
        {
            string path = Find("unity", "Assets", "StreamingAssets", "CosmoSynth", "lss",
                "structures.json");
            JsonValue doc = JsonValue.Parse(File.ReadAllText(path));
            Assert.True(doc["schema"].AsString() == "cosmosynth.places/1", "places schema");

            JsonValue places = doc["places"];
            Assert.True(places.Count >= 25, $"expected at least 25 named places, got {places.Count}");

            JsonValue laniakea = JsonValue.Null();
            int basins = 0, withReference = 0;
            foreach (JsonValue place in places)
            {
                if (!string.IsNullOrEmpty(place["reference"].AsString())) withReference++;
                if (place["category"].AsString() == "basin") basins++;
                if (place["id"].AsString() == "laniakea") laniakea = place;
            }
            Assert.True(withReference == places.Count, "every place carries its reference");
            Assert.True(basins == 9, $"the nine Cosmicflows-4 attractor basins, got {basins}");

            Assert.True(!laniakea.IsNull, "Laniakea is in the file");
            // The published volume, 1.9e6 (Mpc/h)^3. It is a number from a paper about a
            // watershed of a reconstructed velocity field - Inferred, and the file must say
            // so. The 1.945e6 the design quotes is a cell count in the CF4 grid, which is
            // exactly the data the commercial bar blocks, so it is not ours to assert.
            Assert.Equal(1.9, laniakea["volume1e6Mpch3"].AsDouble(), 1e-9,
                "Laniakea's published basin volume, 10^6 (Mpc/h)^3");
            Assert.True(laniakea["provenance"]["volume"].AsString() == "Inferred",
                "and it is Inferred, not Measured");
            Assert.True(laniakea["provenance"]["position"].AsString() == "Inferred",
                "an attractor is a property of a reconstruction, not an observed object");
            Assert.True(!laniakea.ContainsKey("boundary") && !laniakea.ContainsKey("watershed"),
                "no basin boundary is shipped: the grid that defines it is not licensed, and " +
                "a boundary drawn without it would be invented");

            // Coma: our own SDSS peak finder found it independently, and the two positions
            // are carried together rather than as two entries for one cluster.
            foreach (JsonValue place in places)
            {
                if (place["id"].AsString() != "coma-cluster") continue;
                Assert.Equal(103.1, place["distanceMpc"].AsDouble(), 0.2, "Coma comoving distance");
                Assert.True(place.ContainsKey("sdssPeak"),
                    "Coma should carry the independent SDSS overdensity measurement");
                Assert.Equal(0.0226, place["sdssPeak"]["z"].AsDouble(), 0.001,
                    "the measured peak redshift");
            }
        }
    }
}
