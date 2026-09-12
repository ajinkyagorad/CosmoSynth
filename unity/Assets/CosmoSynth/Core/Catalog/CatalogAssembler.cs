using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Merges the catalogue files into one catalogue, in Core, before anything Unity sees them.
    ///
    /// The runtime used to merge by re-adopting: load the Solar System, build 240 views, merge
    /// the spacecraft, <b>destroy and rebuild all 264 views</b>, merge the exoplanets, destroy and
    /// rebuild all 1254. Every adoption raised <c>CatalogLoaded</c>, so the bootstrap re-anchored
    /// the viewer three times; every adoption discarded work other components had already applied
    /// to the views, which is how the model loader reported "24 loaded" with the ISS still a grey
    /// sphere; and the third adoption left <c>RootIds[0]</c> pointing at whichever exoplanet host
    /// happened to sort first, so the light that models the Solar System was attached to a star
    /// eleven parsecs away.
    ///
    /// Merging here is the fix, and it is engine-free, so "what is in the catalogue" is a
    /// question with a headless test. The runtime then adopts once, and every later addition —
    /// a generated star system, a fictional sector — goes through <c>AddBodies</c>, which keeps
    /// the views it already has.
    /// </summary>
    public static class CatalogAssembler
    {
        /// <summary>One JSON document to fold in, and a label for the log.</summary>
        public readonly struct Source
        {
            public readonly string Label;
            public readonly string Json;

            public Source(string label, string json)
            {
                Label = label;
                Json = json;
            }

            public bool HasContent => !string.IsNullOrEmpty(Json);
        }

        /// <summary>
        /// Parse and merge, in order. The first document that parses supplies the catalogue's
        /// name and its root ordering, so the base file must come first — that ordering is what
        /// <c>RootIds[0]</c> means, and it is load-bearing.
        ///
        /// A document that is missing or malformed is reported in <paramref name="log"/> and
        /// skipped. It is never fatal: a build shipped without the spacecraft file should show
        /// the Solar System, not nothing.
        ///
        /// Returns null when nothing could be parsed at all, so the caller can fall back rather
        /// than adopt an empty universe.
        /// </summary>
        public static CelestialCatalog Assemble(IReadOnlyList<Source> sources, IList<string> log)
        {
            CelestialCatalog assembled = null;

            for (int i = 0; sources != null && i < sources.Count; i++)
            {
                Source source = sources[i];
                if (!source.HasContent)
                {
                    log?.Add($"{source.Label}: not present, skipped");
                    continue;
                }

                CelestialCatalog parsed;
                try
                {
                    parsed = CelestialCatalog.Parse(source.Json);
                }
                catch (System.Exception exception)
                {
                    log?.Add($"{source.Label}: malformed, skipped ({exception.Message})");
                    continue;
                }

                if (assembled == null)
                {
                    assembled = parsed;
                    log?.Add($"{source.Label}: {parsed.Count} bodies (base)");
                    continue;
                }

                int added = assembled.MergeFrom(parsed);
                log?.Add($"{source.Label}: {added} of {parsed.Count} bodies added");
            }

            if (assembled != null) log?.Add($"assembled: {assembled.Count} bodies, " +
                                            $"{assembled.RootIds.Count} roots, first root '{FirstRoot(assembled)}'");
            return assembled;
        }

        private static string FirstRoot(CelestialCatalog catalog) =>
            catalog.RootIds.Count > 0 ? catalog.RootIds[0] : "(none)";
    }
}
