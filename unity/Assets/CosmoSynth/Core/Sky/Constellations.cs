using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>One IAU constellation figure: line segments between catalogue stars.</summary>
    public sealed class ConstellationFigure
    {
        /// <summary>Three-letter IAU abbreviation, e.g. "Ori".</summary>
        public string Abbreviation { get; }

        /// <summary>Latin name, e.g. "Orion".</summary>
        public string Name { get; }

        /// <summary>English translation, e.g. "Hunter".</summary>
        public string EnglishName { get; }

        /// <summary>
        /// Segment endpoints as pairs of row indices into the star catalogue. Flat: segment i
        /// runs from <c>StarIndices[2i]</c> to <c>StarIndices[2i + 1]</c>. One array rather
        /// than an array of pairs because the renderer wants exactly this to build one mesh.
        /// </summary>
        public int[] StarIndices { get; }

        /// <summary>
        /// The row to hang the figure's label on: its brightest star. Deliberately NOT the
        /// centroid of the endpoints, which is what the Atlas uses - the centroid is not a
        /// star, and in three dimensions it is usually nowhere near the figure, because the
        /// stars in one figure are at wildly different distances.
        /// </summary>
        public int LabelStarIndex { get; }

        public int SegmentCount => StarIndices.Length / 2;

        public ConstellationFigure(string abbreviation, string name, string englishName,
            int[] starIndices, int labelStarIndex)
        {
            Abbreviation = abbreviation;
            Name = name;
            EnglishName = englishName;
            StarIndices = starIndices;
            LabelStarIndex = labelStarIndex;
        }

        public override string ToString() => $"{Abbreviation} ({Name}), {SegmentCount} segments";
    }

    /// <summary>
    /// The 88 IAU constellation figures, as pairs of star-catalogue row indices.
    ///
    /// Read from sky/constellations.json, which build_starfield.py writes in the SAME RUN as
    /// stars.csstars, so the indices cannot disagree with the catalogue they point into.
    /// <see cref="Validate"/> is the check that says so out loud, and the test suite fails if
    /// any figure references a star the loaded catalogue does not contain.
    ///
    /// **The resolved fraction is not always 1.** Stellarium keys every figure vertex by
    /// Hipparcos number; the Hipparcos catalogue itself is ESA CC BY-NC 3.0 IGO and is not
    /// redistributable, so the importer has to bridge HIP to the shipped catalogue through
    /// other people's identifier tables. What it cannot bridge, it drops rather than draws to
    /// a guessed position. <see cref="SegmentsInSource"/> against <see cref="SegmentCount"/>
    /// is how much of the sky this file actually carries, and the UI should say it rather
    /// than implying all 88 figures are complete.
    /// </summary>
    public sealed class Constellations
    {
        private readonly ConstellationFigure[] _figures;

        public IReadOnlyList<ConstellationFigure> Figures => _figures;
        public int Count => _figures.Length;

        /// <summary>Figures the source catalogue defines: 88 for the IAU modern sky culture.</summary>
        public int FiguresInSource { get; }

        /// <summary>Segments the source defines, against how many resolved to catalogue rows.</summary>
        public int SegmentsInSource { get; }

        public int SegmentCount { get; }

        /// <summary>Segments resolved as a fraction of those the source defines, 0 to 1.</summary>
        public double ResolvedFraction =>
            SegmentsInSource > 0 ? (double)SegmentCount / SegmentsInSource : 0.0;

        /// <summary>The attribution this file obliges the app to display. CC BY-SA 4.0.</summary>
        public string Attribution { get; }

        public string LicenceProfile { get; }

        private Constellations(ConstellationFigure[] figures, int figuresInSource,
            int segmentsInSource, string attribution, string licenceProfile)
        {
            _figures = figures;
            FiguresInSource = figuresInSource;
            SegmentsInSource = segmentsInSource;
            Attribution = attribution;
            LicenceProfile = licenceProfile;
            int total = 0;
            foreach (ConstellationFigure figure in figures) total += figure.SegmentCount;
            SegmentCount = total;
        }

        public static Constellations Parse(string json)
        {
            if (!JsonValue.TryParse(json, out JsonValue root) || !root.IsObject)
                throw new FormatException(
                    "constellations.json is not a JSON object. Rebuild it with " +
                    "tools/cosmodata/build_starfield.py.");
            if (!root.ContainsKey("figures"))
                throw new FormatException(
                    "constellations.json has no 'figures'. Rebuild it with " +
                    "tools/cosmodata/build_starfield.py.");

            var figures = new List<ConstellationFigure>();
            foreach (JsonValue entry in root["figures"])
            {
                JsonValue segments = entry["segments"];
                if (!segments.IsArray) continue;
                var indices = new int[segments.Count * 2];
                int at = 0;
                foreach (JsonValue segment in segments)
                {
                    if (!segment.IsArray || segment.Count != 2)
                        throw new FormatException(
                            "a constellation segment is not a pair of star indices.");
                    indices[at++] = segment[0].AsInt(-1);
                    indices[at++] = segment[1].AsInt(-1);
                }
                figures.Add(new ConstellationFigure(
                    entry["abbr"].AsString("?"),
                    entry["name"].AsString("?"),
                    entry["english"].AsString(entry["name"].AsString("?")),
                    indices,
                    entry["labelIndex"].AsInt(-1)));
            }

            JsonValue counts = root["counts"];
            return new Constellations(
                figures.ToArray(),
                counts["figuresInSource"].AsInt(figures.Count),
                counts["segmentsInSource"].AsInt(0),
                root["source"].AsString(""),
                root["licenceProfile"].AsString("unknown"));
        }

        /// <summary>
        /// Check every index against a catalogue of <paramref name="starCount"/> rows.
        /// Returns null when sound, or the first fault as a sentence.
        ///
        /// This is the check that makes the file trustworthy: a figure pointing at row 90,000
        /// of a catalogue loaded with the brightest 4,000 draws a line to whatever happens to
        /// be there, and it will look plausible. There is no visual symptom, which is exactly
        /// why it is asserted rather than eyeballed.
        /// </summary>
        public string Validate(int starCount)
        {
            foreach (ConstellationFigure figure in _figures)
            {
                if (figure.StarIndices.Length == 0)
                    return $"{figure.Abbreviation} has no segments; it should not be in the file.";
                if ((figure.StarIndices.Length & 1) != 0)
                    return $"{figure.Abbreviation} has an odd number of endpoints.";
                for (int i = 0; i < figure.StarIndices.Length; i++)
                {
                    int index = figure.StarIndices[i];
                    if (index < 0 || index >= starCount)
                        return $"{figure.Abbreviation} references star {index}, and the " +
                               $"catalogue has {starCount} rows.";
                }
                for (int i = 0; i < figure.StarIndices.Length; i += 2)
                {
                    if (figure.StarIndices[i] == figure.StarIndices[i + 1])
                        return $"{figure.Abbreviation} has a segment from star " +
                               $"{figure.StarIndices[i]} to itself.";
                }
                if (figure.LabelStarIndex < 0 || figure.LabelStarIndex >= starCount)
                    return $"{figure.Abbreviation} labels star {figure.LabelStarIndex}, and " +
                           $"the catalogue has {starCount} rows.";
            }
            return null;
        }

        /// <summary>
        /// The distinct star rows every figure touches, for a renderer that wants to force
        /// those stars to stay drawn even when they fall outside the magnitude prefix.
        /// </summary>
        public HashSet<int> ReferencedStars()
        {
            var set = new HashSet<int>();
            foreach (ConstellationFigure figure in _figures)
                foreach (int index in figure.StarIndices)
                    set.Add(index);
            return set;
        }

        public ConstellationFigure Find(string abbreviation)
        {
            foreach (ConstellationFigure figure in _figures)
            {
                if (string.Equals(figure.Abbreviation, abbreviation,
                        StringComparison.OrdinalIgnoreCase))
                    return figure;
            }
            return null;
        }

        public override string ToString() =>
            $"{Count} of {FiguresInSource} figures, {SegmentCount} of {SegmentsInSource} " +
            $"segments ({ResolvedFraction:P1})";
    }
}
