using System.Collections.Generic;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// What ends up in the catalogue, and in what order.
    ///
    /// <see cref="CatalogAssembler"/> exists because merging by re-adoption left
    /// <c>RootIds[0]</c> pointing at whichever exoplanet host happened to sort first, and the one
    /// light modelling the Solar System followed it eleven parsecs away. Its own docstring says
    /// "it is engine-free, so what is in the catalogue is a question with a headless test" — and
    /// until this file there was no test of <c>Assemble</c> at all, on the one type whose whole
    /// job is that ordering.
    /// </summary>
    public static class CatalogAssemblerTests
    {
        /// <summary>An id, or "child:parent". A body with no parent is a root.</summary>
        private static string Catalogue(string name, params string[] ids)
        {
            var bodies = new List<string>();
            foreach (string spec in ids)
            {
                int split = spec.IndexOf(':');
                if (split < 0)
                {
                    bodies.Add($"{{\"id\":\"{spec}\",\"name\":\"{spec}\"}}");
                }
                else
                {
                    string id = spec.Substring(0, split);
                    string parent = spec.Substring(split + 1);
                    bodies.Add($"{{\"id\":\"{id}\",\"name\":\"{id}\",\"parent\":\"{parent}\"}}");
                }
            }
            return $"{{\"name\":\"{name}\",\"bodies\":[{string.Join(",", bodies)}]}}";
        }

        private static CatalogAssembler.Source Source(string label, string json) =>
            new CatalogAssembler.Source(label, json);

        private static string Roots(CelestialCatalog catalog) =>
            "[" + string.Join(", ", catalog.RootIds) + "]";

        [Test("the FIRST source fixes the name and RootIds[0], whatever the others hold")]
        public static void BaseFirstFixesTheRootOrder()
        {
            var log = new List<string>();
            CelestialCatalog merged = CatalogAssembler.Assemble(new[]
            {
                Source("solar_system", Catalogue("Solar System", "sun", "earth:sun")),
                Source("spacecraft", Catalogue("Spacecraft", "voyager-1:sun")),
            }, log);

            Assert.True(merged != null, "both documents parse, so something is assembled");
            Assert.True(merged.Count == 3, $"every body is merged, not {merged.Count}");
            Assert.True(merged.RootIds.Count == 1,
                $"the merged tree has one root, not {Roots(merged)}");
            Assert.True(merged.RootIds[0] == "sun",
                $"RootIds[0] must come from the base document, not {Roots(merged)}");
            Assert.True(merged.Name.StartsWith("Solar System"),
                $"the name comes from the base document, not '{merged.Name}'");
        }

        [Test("a malformed BASE returns null rather than promoting the next source")]
        public static void MalformedBaseIsFatal()
        {
            var log = new List<string>();
            CelestialCatalog merged = CatalogAssembler.Assemble(new[]
            {
                Source("solar_system", "{bad json"),
                Source("spacecraft", Catalogue("Spacecraft", "voyager-1")),
            }, log);

            // The failing case this test was written for: skipping the base promotes source 2, so
            // the catalogue's name, its root ordering and RootIds[0] all come from the spacecraft
            // file — and OnCatalogLoaded then anchors the viewer on a spacecraft. Silently, at
            // Debug.Log level.
            Assert.True(merged == null,
                "a base that does not parse must not be replaced by a later document");

            bool said = false;
            foreach (string line in log) if (line.Contains("malformed")) said = true;
            Assert.True(said, "and it must say so in the log");
        }

        [Test("a missing BASE is fatal for the same reason a malformed one is")]
        public static void MissingBaseIsFatal()
        {
            var log = new List<string>();
            CelestialCatalog merged = CatalogAssembler.Assemble(new[]
            {
                Source("solar_system", null),
                Source("spacecraft", Catalogue("Spacecraft", "voyager-1")),
            }, log);

            Assert.True(merged == null, "an absent base must not be replaced by a later document");
        }

        [Test("a missing or malformed LATER source is skipped without affecting the rest")]
        public static void LaterSourcesDegrade()
        {
            var log = new List<string>();
            CelestialCatalog merged = CatalogAssembler.Assemble(new[]
            {
                Source("solar_system", Catalogue("Solar System", "sun", "earth:sun")),
                Source("spacecraft", null),
                Source("exoplanets", Catalogue("Exoplanets", "exo-1")),
            }, log);

            Assert.True(merged != null, "a build shipped without one file still shows the rest");
            Assert.True(merged.Count == 3, $"the third source still merged, giving {merged.Count}");

            // The ordering claim, with a second root in the tree to make it mean something: the
            // base document's root must still come first. This is CONTRACT C10's reason for
            // keeping exoplanets out of the merge at all — with them in, RootIds[0] decided which
            // star the whole scene was lit by.
            Assert.True(merged.RootIds[0] == "sun",
                $"the base document's root must still be first, not {Roots(merged)}");
        }

        [Test("the merge count is the number of NEW ids, not the number of bodies offered")]
        public static void DuplicatesAreNotCounted()
        {
            var log = new List<string>();
            CelestialCatalog merged = CatalogAssembler.Assemble(new[]
            {
                Source("solar_system", Catalogue("Solar System", "sun", "earth:sun")),
                Source("spacecraft", Catalogue("Spacecraft", "earth", "voyager-1:earth")),
            }, log);

            Assert.True(merged.Count == 3,
                $"'earth' appears in both documents and must be merged once, not {merged.Count} bodies");
            Assert.True(merged.RootIds.Count == 1 && merged.RootIds[0] == "sun",
                $"the duplicate must not become a second root: {Roots(merged)}");
        }

        [Test("nothing at all assembles to null rather than to an empty universe")]
        public static void NothingAssemblesToNull()
        {
            Assert.True(CatalogAssembler.Assemble(new CatalogAssembler.Source[0], null) == null,
                "no sources");
            Assert.True(CatalogAssembler.Assemble(null, null) == null, "a null source list");
        }
    }
}
