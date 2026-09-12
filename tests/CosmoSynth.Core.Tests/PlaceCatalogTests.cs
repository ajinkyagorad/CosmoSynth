using System;
using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The named places of the Milky Way: the hand-authored landmarks and the 94 named clouds
    /// generated from Zucker et al. 2020, both in the cosmosynth.places/1 schema.
    ///
    /// The rule these tests exist for: every place carries exactly one reference. The owner's
    /// WebXR Atlas ships its markers with none, and two of the numbers it states are wrong --
    /// the Crab pulsar at 30.2 Hz, and the Local Bubble at 150 pc across in one place and 100 pc
    /// in another. Both are corrected here with a citation attached.
    /// </summary>
    public static class PlaceCatalogTests
    {
        private static PlaceCatalog _landmarks, _clouds;

        private static PlaceCatalog Landmarks => _landmarks ??
            (_landmarks = PlaceCatalog.Parse(Shipped.Text("galaxy", "landmarks.json")));

        private static PlaceCatalog Clouds => _clouds ??
            (_clouds = PlaceCatalog.Parse(Shipped.Text("galaxy", "namedclouds.json")));

        [Test("every landmark and every named cloud carries a reference")]
        public static void EveryPlaceIsCited()
        {
            var uncited = new List<string>();
            foreach (PlaceCatalog catalogue in new[] { Landmarks, Clouds })
                foreach (Place place in catalogue.Places)
                    if (string.IsNullOrEmpty(place.Reference)) uncited.Add(place.Id);
            Assert.True(uncited.Count == 0,
                "places with no reference: " + string.Join(", ", uncited));
            Assert.True(Landmarks.Count >= 12,
                $"expected at least 12 landmarks, got {Landmarks.Count}");
            Assert.True(Clouds.Count == 94,
                $"Zucker et al. 2020 names 94 clouds across 326 sightlines, got {Clouds.Count}");
        }

        [Test("the Orion complex resolves to 414 pc, the VLBA parallax distance")]
        public static void OrionComplexDistance()
        {
            Place orion = Landmarks["orion-complex"];
            Assert.True(orion != null, "the Orion complex landmark is missing");
            Assert.Equal(414.0, PlaceCatalog.IcrfPc(orion).Magnitude, 0.5,
                "|r| of the Orion complex in ICRF parsecs");
            Assert.Equal(414.0, PlaceCatalog.GalacticPc(orion).Magnitude, 0.5,
                "and the same distance in the galactic frame");
            Assert.True(orion.Reference.Contains("Menten"),
                "the distance is Menten et al. 2007's parallax: " + orion.Reference);
            Assert.True(orion.ProvenanceOf("distance") == ProvenanceClass.Measured,
                "a VLBA parallax is a measurement");

            // Given in (l, b), the galactic position needs no rotation at all, which is why the
            // authored clouds are stored that way.
            Double3 galactic = PlaceCatalog.GalacticPc(orion);
            double l = Math.Atan2(galactic.y, galactic.x) * MathD.Rad2Deg;
            double b = Math.Asin(galactic.z / galactic.Magnitude) * MathD.Rad2Deg;
            Assert.Angle(209.0 * MathD.Deg2Rad, l * MathD.Deg2Rad, 1e-9 , "galactic longitude");
            Assert.Equal(-19.0, b, 1e-9, "galactic latitude");
        }

        [Test("the Crab pulsar spins at 29.6 Hz, not the 30.2 the Atlas states")]
        public static void CrabPulsarFrequency()
        {
            Place crab = Landmarks["crab"];
            Assert.True(crab != null, "the Crab landmark is missing");
            Assert.Equal(29.6, crab.PulsarHz, 1e-9,
                "spin frequency at MJD 58058, from Lyne et al. 2018");
            Assert.True(crab.Category == "snr", $"the Crab is a remnant, categorised as {crab.Category}");
            Assert.True(crab.ProvenanceOf("distance") == ProvenanceClass.Inferred,
                "the Crab's distance is the worst-known number in the entry and must not claim " +
                "to be measured");
            Assert.True(crab.DistanceErrorPc > 100.0,
                "and it must carry an uncertainty that says so");
            Assert.True(crab.Closeup == "crab",
                "the Crab is one of the three procedural close-up fields");
        }

        [Test("the Local Bubble is about 300 pc across, which is not what the Atlas says either time")]
        public static void LocalBubbleSize()
        {
            Place bubble = Landmarks["local-bubble"];
            Assert.True(bubble != null, "the Local Bubble landmark is missing");
            Assert.Equal(150.0, bubble.RadiusPc, 1e-9,
                "radius in parsecs: about 1000 light years across (Zucker et al. 2022)");
            Assert.Equal(0.0, bubble.DistancePc, 1e-9, "the Sun is inside it");
            Assert.True(bubble.ProvenanceOf("radius") == ProvenanceClass.Inferred,
                "the bubble is not a sphere and the radius is a summary, not a measurement");
            Assert.True(bubble.Reference.Contains("Zucker"), bubble.Reference);
        }

        [Test("globular landmarks take their positions from the catalogue, so they exist once")]
        public static void GlobularLandmarksResolve()
        {
            PointSet globulars = PointSet.Parse(Shipped.Bytes("galaxy", "globulars.csgp"),
                "globulars");

            Place tuc = Landmarks["47-tuc"];
            Assert.True(tuc != null, "the 47 Tucanae landmark is missing");
            Assert.True(tuc.IsResolvedFromCatalogue,
                "47 Tuc should name a row of globulars.csgp rather than restate a position");
            try
            {
                PlaceCatalog.GalacticPc(tuc);
                throw new AssertionException(
                    "an unresolved catalogue-backed place returned a position anyway");
            }
            catch (InvalidOperationException)
            {
            }

            int resolved = Landmarks.Resolve("globulars", globulars);
            Assert.True(resolved == 5,
                $"five globular landmarks should resolve, {resolved} did");
            Assert.Equal(4520.6, PlaceCatalog.GalacticPc(tuc).Magnitude, 5.0,
                "47 Tuc's distance, taken from the catalogue rather than retyped");
            Assert.True(tuc.DistanceErrorPc > 0.0,
                "and its uncertainty comes across with it");

            Place m4 = Landmarks["m4"];
            Assert.True(PlaceCatalog.GalacticPc(m4).Magnitude < 2000.0,
                "M4 is the nearest globular");
        }

        [Test("a place that names a catalogue row that does not exist is an error, not a shrug")]
        public static void UnresolvableLandmarkThrows()
        {
            const string json = @"{""schema"":""cosmosynth.places/1"",""places"":[
                {""id"":""ghost"",""name"":""Ghost"",""category"":""cluster"",
                 ""source"":""globulars"",""sourceName"":""NGC 99999"",
                 ""reference"":""nowhere""}]}";
            PlaceCatalog catalogue = PlaceCatalog.Parse(json);
            PointSet globulars = PointSet.Parse(Shipped.Bytes("galaxy", "globulars.csgp"),
                "globulars");
            try
            {
                catalogue.Resolve("globulars", globulars);
                throw new AssertionException(
                    "a landmark naming a missing row resolved anyway, which would draw it at " +
                    "the Sun");
            }
            catch (InvalidOperationException e)
            {
                Assert.True(e.Message.Contains("NGC 99999"), e.Message);
            }
        }

        [Test("a place with no reference, or with two ways of saying where it is, is refused")]
        public static void MalformedPlacesAreRefused()
        {
            Refuses(@"{""schema"":""cosmosynth.places/1"",""places"":[
                {""id"":""x"",""name"":""X"",""category"":""cloud"",""l"":0,""b"":0,
                 ""distancePc"":100}]}", "reference", "a place with no reference");

            Refuses(@"{""schema"":""cosmosynth.places/1"",""places"":[
                {""id"":""x"",""name"":""X"",""category"":""cloud"",""l"":0,""b"":0,
                 ""ra"":10,""dec"":20,""distancePc"":100,""reference"":""r""}]}",
                "exactly one", "a place given in both coordinate systems");

            Refuses(@"{""schema"":""cosmosynth.places/1"",""places"":[
                {""id"":""x"",""name"":""X"",""category"":""cloud"",""reference"":""r"",
                 ""source"":""globulars""}]}", "sourceName",
                "a place with a source and nothing to look up");

            Refuses(@"{""schema"":""cosmosynth.places/2"",""places"":[]}", "schema",
                "a places file from a schema this build does not know");

            Refuses(@"{""schema"":""cosmosynth.places/1"",""places"":[
                {""id"":""x"",""name"":""X"",""category"":""cloud"",""l"":0,""b"":0,
                 ""distancePc"":1,""reference"":""r""},
                {""id"":""x"",""name"":""Y"",""category"":""cloud"",""l"":1,""b"":1,
                 ""distancePc"":2,""reference"":""r""}]}", "duplicate", "two places with one id");
        }

        private static void Refuses(string json, string expectedInMessage, string what)
        {
            try
            {
                PlaceCatalog.Parse(json);
            }
            catch (FormatException e)
            {
                Assert.True(e.Message.IndexOf(expectedInMessage, StringComparison.OrdinalIgnoreCase) >= 0,
                    $"{what}: refused, but the message did not mention '{expectedInMessage}': " +
                    e.Message);
                return;
            }
            throw new AssertionException(what + " was accepted");
        }

        [Test("the named clouds land in the distance order three separate surveys agree on")]
        public static void NamedCloudsAreOrdered()
        {
            double taurus = Clouds["taurus"].DistancePc;
            double perseus = Clouds["perseus"].DistancePc;
            double orion = Clouds["orion"].DistancePc;
            double california = Clouds["california"].DistancePc;
            Assert.True(taurus < perseus && perseus < orion && orion < california,
                $"Taurus {taurus:F0} < Perseus {perseus:F0} < Orion {orion:F0} < California " +
                $"{california:F0} pc is the published ordering");
            Assert.Equal(420.0, orion, 25.0, "Orion's grouped distance, pc");

            foreach (Place place in Clouds.Places)
            {
                Assert.True(place.DistancePc > 50.0 && place.DistancePc < 3000.0,
                    $"{place.Name} at {place.DistancePc:F0} pc is outside the range this " +
                    "handbook covers");
                Assert.True(place.DistanceErrorPc > 0.0,
                    $"{place.Name} has no distance uncertainty, and every row of this table has one");
                Assert.True(place.ProvenanceOf("distance") == ProvenanceClass.Inferred,
                    $"{place.Name}: a posterior median is Inferred, never Measured");
            }
        }

        [Test("landmarks and named clouds merge into one catalogue without colliding")]
        public static void CataloguesMerge()
        {
            PlaceCatalog merged = PlaceCatalog.Parse(Shipped.Text("galaxy", "landmarks.json"));
            merged.Add(PlaceCatalog.Parse(Shipped.Text("galaxy", "namedclouds.json")));
            Assert.True(merged.Count == Landmarks.Count + Clouds.Count,
                $"{Landmarks.Count} + {Clouds.Count} should merge to " +
                $"{Landmarks.Count + Clouds.Count}, got {merged.Count}");
            Assert.True(merged["sgr-a-star"] != null && merged["taurus"] != null,
                "both files' places must survive the merge");

            int clouds = 0;
            foreach (Place place in merged.OfCategory("cloud")) clouds++;
            Assert.True(clouds == 95,
                $"94 named clouds plus the authored Orion complex is 95 cloud places, got {clouds}");
            Assert.True(merged.Attribution.Contains("Zucker"),
                "the merged attribution must carry the clouds' credit line");
        }
    }
}
