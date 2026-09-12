using System;
using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The shipped galactic point populations: globulars, molecular clouds and the Radcliffe
    /// Wave. These test the artefacts, so a broken importer, a mis-parsed column or a silently
    /// dropped catalogue shows up here rather than as a wrong-looking Galaxy on a headset.
    ///
    /// The Cepheid layer that design B section 2.8 specified is absent on purpose: Skowron et
    /// al. 2019's repository licence excludes scientific data and no commercial-clean Galactic
    /// Cepheid catalogue exists (DATA-LICENCE-AMENDMENT section 2 row 11). The CO clouds trace
    /// the same young disc.
    /// </summary>
    public static class GalacticPointsTests
    {
        private static PointSet _globulars, _clouds, _radcliffe, _rwClusters;

        private static PointSet Globulars => _globulars ??
            (_globulars = PointSet.Parse(Shipped.Bytes("galaxy", "globulars.csgp"), "globulars"));

        private static PointSet Clouds => _clouds ??
            (_clouds = PointSet.Parse(Shipped.Bytes("galaxy", "clouds.csgp"), "clouds"));

        private static PointSet Radcliffe => _radcliffe ??
            (_radcliffe = PointSet.Parse(Shipped.Bytes("galaxy", "radcliffe.csgp"), "radcliffe"));

        private static PointSet RwClusters => _rwClusters ??
            (_rwClusters = PointSet.Parse(Shipped.Bytes("galaxy", "radcliffe_clusters.csgp"),
                "radcliffeClusters"));

        // ---- globular clusters ------------------------------------------------------------

        [Test("the globular catalogue holds all 213 Milky Way clusters, at plausible distances")]
        public static void GlobularsParse()
        {
            Assert.True(Globulars.Count == 213,
                $"expected 213 clusters, got {Globulars.Count}. The dwarf-hosted LVDB file is " +
                "deliberately not read: those clusters sit at 20 to 200 kpc and would look " +
                "like a catastrophic distance bug.");
            double near = double.MaxValue, far = 0.0;
            for (int i = 0; i < Globulars.Count; i++)
            {
                double d = Globulars.DistancePc(i);
                if (d < near) near = d;
                if (d > far) far = d;
            }
            Assert.True(near >= 1000.0 && far <= 200000.0,
                $"distances run {near:F0} to {far:F0} pc, outside the 1 to 200 kpc a Milky Way " +
                "globular population occupies");
            Assert.True(Globulars.Attribution().Contains("Local Volume Database"),
                "the CC0 credit line must name the Local Volume Database");
        }

        [Test("47 Tuc, M4 and omega Centauri are where and what the catalogues say")]
        public static void GlobularKnownAnswers()
        {
            int tuc = Globulars.IndexOf("NGC 104");
            Assert.True(tuc >= 0, "NGC 104 (47 Tucanae) is missing");
            // LVDB distance modulus 13.28 from Baumgardt et al. 2021, the reference the file
            // carries per object. 5 pc is a tenth of a per cent: this catches a parse, not an
            // astrophysical disagreement.
            Assert.Equal(4520.6, Globulars.DistancePc(tuc), 5.0, "47 Tuc distance, pc");

            int nearest = 0;
            for (int i = 1; i < Globulars.Count; i++)
                if (Globulars.DistancePc(i) < Globulars.DistancePc(nearest)) nearest = i;
            Assert.True(Globulars.Name(nearest) == "NGC 6121",
                $"the nearest globular should be M4 (NGC 6121), got {Globulars.Name(nearest)}");
            Assert.True(Globulars.DistancePc(nearest) < 2000.0,
                $"M4 is under 2 kpc away, this file says {Globulars.DistancePc(nearest):F0} pc");

            int brightest = -1;
            for (int i = 0; i < Globulars.Count; i++)
            {
                double mv = Globulars.Value("absV", i);
                if (double.IsNaN(mv)) continue;
                if (brightest < 0 || mv < Globulars.Value("absV", brightest)) brightest = i;
            }
            Assert.True(Globulars.Name(brightest) == "NGC 5139",
                $"omega Centauri should be the most luminous globular, got " +
                $"{Globulars.Name(brightest)}");
            // LVDB's own value. Harris (2010) gives -10.26 for the same cluster; the 0.28 mag
            // difference is between two catalogues, and asserting on the one we ship is what
            // catches a column shift or an f32 truncation.
            Assert.Equal(-10.54, Globulars.Value("absV", brightest), 0.02,
                "omega Cen M_V as LVDB publishes it");
        }

        [Test("a globular with no published magnitude carries NaN and says so, never zero")]
        public static void MissingMagnitudesAreFlagged()
        {
            int flagged = 0;
            for (int i = 0; i < Globulars.Count; i++)
            {
                bool noMv = (Globulars.Flags(i) & PointFlags.NoAbsoluteMagnitude) != 0;
                double mv = Globulars.Value("absV", i);
                if (noMv)
                {
                    flagged++;
                    Assert.True(double.IsNaN(mv),
                        $"{Globulars.Name(i)} is flagged as having no M_V but carries {mv}");
                }
                else
                {
                    Assert.False(double.IsNaN(mv),
                        $"{Globulars.Name(i)} has M_V NaN without the flag that explains it");
                }
            }
            Assert.True(flagged == 7,
                $"expected 7 clusters without a published M_V, found {flagged}");

            // The flag must change what the app is willing to claim, not just be recorded.
            int example = -1;
            for (int i = 0; i < Globulars.Count && example < 0; i++)
                if ((Globulars.Flags(i) & PointFlags.NoAbsoluteMagnitude) != 0) example = i;
            FieldProvenance provenance = Globulars.ProvenanceFor("absV", example);
            Assert.True(provenance.Class == ProvenanceClass.Inferred,
                $"a cluster with no published M_V must not report Measured, got " +
                $"{provenance.Class}");
        }

        [Test("a globular's position provenance carries its own distance uncertainty")]
        public static void GlobularProvenanceCarriesUncertainty()
        {
            int tuc = Globulars.IndexOf("NGC 104");
            FieldProvenance provenance = Globulars.ProvenanceFor("pos", tuc);
            Assert.True(provenance.Class == ProvenanceClass.Measured,
                "a globular's direction is measured");
            Assert.True(provenance.HasUncertainty && provenance.Uncertainty > 0.0,
                "the distance uncertainty must reach the provenance, got " +
                provenance.Uncertainty);
            Assert.True(provenance.Uncertainty < 0.2 * Globulars.DistancePc(tuc),
                "a 20% distance error on 47 Tuc would mean the modulus errors were misread");
        }

        // ---- molecular clouds -------------------------------------------------------------

        [Test("the CO clouds trace a thin molecular disc that flares outward")]
        public static void CloudsTraceTheDisc()
        {
            Assert.True(Clouds.Count == 1039,
                $"expected 1039 clouds with a distance, got {Clouds.Count}");

            var inner = new List<double>();
            var outer = new List<double>();
            for (int i = 0; i < Clouds.Count; i++)
            {
                Double3 gc = GalaxyModel.ToGalactocentricPc(Clouds.GalacticPosition(i));
                double r = GalaxyModel.RadiusKpc(gc);
                if (r >= 4.0 && r < 10.0) inner.Add(Math.Abs(gc.z));
                else if (r >= 10.0 && r < 16.0) outer.Add(Math.Abs(gc.z));
            }
            inner.Sort();
            outer.Sort();
            Assert.True(inner.Count > 300 && outer.Count > 100,
                $"too few clouds to measure the disc: {inner.Count} inner, {outer.Count} outer");
            double innerMedian = inner[inner.Count / 2];
            double outerMedian = outer[outer.Count / 2];
            // Both are published facts about the Galaxy: the molecular disc is thin, and it
            // flares beyond the solar circle. Either breaks if the galactic rotation, the kpc
            // to pc conversion or the z axis is wrong.
            Assert.True(innerMedian < 60.0,
                $"median |z| between 4 and 10 kpc is {innerMedian:F1} pc; the molecular disc " +
                "is thinner than that");
            Assert.True(outerMedian / innerMedian >= 1.8,
                $"the outer disc is only {outerMedian / innerMedian:F2}x thicker " +
                $"({innerMedian:F1} -> {outerMedian:F1} pc), and the flaring is a robust result");
        }

        [Test("kinematically ambiguous cloud distances are flagged as the guesses they are")]
        public static void AmbiguousCloudDistancesAreFlagged()
        {
            int ambiguous = 0;
            for (int i = 0; i < Clouds.Count; i++)
                if ((Clouds.Flags(i) & PointFlags.AmbiguousDistance) != 0) ambiguous++;
            Assert.True(ambiguous == 663,
                $"expected 663 clouds with an unresolved near/far kinematic distance, got " +
                $"{ambiguous}. 64% of this catalogue is not a distance measurement.");

            int example = -1;
            for (int i = 0; i < Clouds.Count && example < 0; i++)
                if ((Clouds.Flags(i) & PointFlags.AmbiguousDistance) != 0) example = i;
            FieldProvenance provenance = Clouds.ProvenanceFor("pos", example);
            Assert.True(provenance.Class == ProvenanceClass.Inferred,
                "an unresolved kinematic distance must never report as Measured");
            Assert.True(provenance.Reference != null &&
                        provenance.Reference.IndexOf("ambiguity", StringComparison.OrdinalIgnoreCase) >= 0,
                "the provenance note should say why: " + provenance.Reference);
        }

        [Test("cloud sizes and masses are physical, in the units the columns declare")]
        public static void CloudColumnsAreInRange()
        {
            double maxSize = 0.0, maxMass = 0.0;
            for (int i = 0; i < Clouds.Count; i++)
            {
                maxSize = Math.Max(maxSize, Clouds.Value("sizePc", i));
                maxMass = Math.Max(maxMass, Clouds.Value("massMsun", i));
            }
            Assert.True(maxSize > 50.0 && maxSize < 500.0,
                $"the largest cloud is {maxSize:F0} pc across; giant molecular clouds run to a " +
                "couple of hundred parsecs, so this is a unit error either way");
            Assert.True(maxMass > 1e5 && maxMass < 1e8,
                $"the most massive cloud is {maxMass:G3} solar masses");
        }

        // ---- the Radcliffe Wave -----------------------------------------------------------

        [Test("the Radcliffe Wave is a 2.7 kpc undulating line that passes near the Sun")]
        public static void RadcliffeWaveShape()
        {
            Assert.True(Radcliffe.Count == 1500,
                $"expected 1500 spine points, got {Radcliffe.Count}");
            Assert.True(Radcliffe.IsPolyline,
                "consecutive rows are one open polyline; a renderer must not scatter them");

            double span = Double3.Distance(Radcliffe.Position(0),
                Radcliffe.Position(Radcliffe.Count - 1));
            Assert.True(span > 2000.0 && span < 3200.0,
                $"end to end the wave is {span:F0} pc, and the paper's figure is 2.7 kpc");

            double closest = double.MaxValue, amplitude = 0.0;
            for (int i = 0; i < Radcliffe.Count; i++)
            {
                closest = Math.Min(closest, Radcliffe.DistancePc(i));
                amplitude = Math.Max(amplitude, Math.Abs(Radcliffe.GalacticPosition(i).z));
            }
            Assert.True(closest > 100.0 && closest < 500.0,
                $"the wave's closest approach to the Sun is {closest:F0} pc; the paper puts it " +
                "about 300 pc away, and this is the check that the frame conversion is right");
            Assert.True(amplitude > 50.0 && amplitude < 400.0,
                $"vertical amplitude {amplitude:F0} pc: the undulation is the finding");

            // Consecutive points must actually be consecutive, or it is not a polyline.
            double longestGap = 0.0;
            for (int i = 1; i < Radcliffe.Count; i++)
            {
                longestGap = Math.Max(longestGap,
                    Double3.Distance(Radcliffe.Position(i - 1), Radcliffe.Position(i)));
            }
            Assert.True(longestGap < 20.0,
                $"the largest step along the spine is {longestGap:F1} pc, so the rows are not " +
                "in path order");
        }

        [Test("Radcliffe Wave member clusters keep their names as names, and non-members are flagged")]
        public static void RadcliffeClusters()
        {
            Assert.True(RwClusters.Count == 125,
                $"expected 125 clusters, got {RwClusters.Count}");
            int nonMembers = 0;
            for (int i = 0; i < RwClusters.Count; i++)
                if ((RwClusters.Flags(i) & PointFlags.NotAMember) != 0) nonMembers++;
            Assert.True(nonMembers > 0 && nonMembers < RwClusters.Count,
                $"{nonMembers} of {RwClusters.Count} flagged as non-members; drawing the " +
                "inlier = 0 rows as part of the wave misstates the paper's result");

            // The catalogue's names are bare numbers in quotes. Parsed as integers, cluster
            // "2082" silently becomes an index into something.
            Assert.True(RwClusters.Names.Length == RwClusters.Count,
                "every cluster should carry a name");
            bool numericName = false;
            for (int i = 0; i < RwClusters.Count; i++)
            {
                Assert.False(RwClusters.Name(i).Contains("\""),
                    $"the quotes were not stripped from '{RwClusters.Name(i)}'");
                if (int.TryParse(RwClusters.Name(i), out _)) numericName = true;
            }
            Assert.True(numericName,
                "this catalogue has numeric names, and the fact that one survived as a string " +
                "is what this test is for");

            for (int i = 0; i < RwClusters.Count; i++)
            {
                double age = RwClusters.Value("ageMyr", i);
                Assert.True(age >= 0.0 && age <= 100.0,
                    $"cluster {RwClusters.Name(i)} has age {age} Myr");
                Assert.True(RwClusters.DistanceErrorPc(i) > 0.0,
                    $"cluster {RwClusters.Name(i)} has no distance uncertainty");
            }
        }

        [Test("a point set loaded under the wrong kind is refused")]
        public static void RefusesTheWrongKind()
        {
            try
            {
                PointSet.Parse(Shipped.Bytes("galaxy", "clouds.csgp"), "globulars");
                throw new AssertionException("the cloud catalogue was accepted as globulars");
            }
            catch (FormatException e)
            {
                Assert.True(e.Message.Contains("clouds"),
                    "the refusal should name what the file actually is: " + e.Message);
            }
        }

        [Test("every shipped point set states the credit its licence requires")]
        public static void EveryFileCarriesItsAttribution()
        {
            foreach (PointSet set in new[] { Globulars, Clouds, Radcliffe, RwClusters })
            {
                Assert.True(!string.IsNullOrEmpty(set.Attribution()),
                    $"the '{set.Kind}' file has no attribution line");
                Assert.True(set.Attribution().Contains("CC0"),
                    $"the '{set.Kind}' file should name its CC0 dedication: {set.Attribution()}");
            }
        }
    }
}
