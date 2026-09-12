using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>A named place in the Galaxy: a cloud, a nebula, a cluster, a remnant.</summary>
    public sealed class Place
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string Category { get; internal set; }
        public string Description { get; internal set; }

        /// <summary>The one citation every entry is required to carry.</summary>
        public string Reference { get; internal set; }

        /// <summary>Galactic longitude and latitude in degrees, or NaN when given as RA/Dec.</summary>
        public double LongitudeDeg { get; internal set; } = double.NaN;

        public double LatitudeDeg { get; internal set; } = double.NaN;

        public double RightAscensionDeg { get; internal set; } = double.NaN;

        public double DeclinationDeg { get; internal set; } = double.NaN;

        public double DistancePc { get; internal set; } = double.NaN;

        public double DistanceErrorPc { get; internal set; } = double.NaN;

        public double RadiusPc { get; internal set; } = double.NaN;

        /// <summary>Name of a shipped point set this place takes its position from, or null.</summary>
        public string Source { get; internal set; }

        /// <summary>Row name to look up in <see cref="Source"/>.</summary>
        public string SourceName { get; internal set; }

        /// <summary>Procedural close-up field to draw here: orion, crab or localbubble.</summary>
        public string Closeup { get; internal set; }

        /// <summary>Pulsar spin frequency in Hz, where this place has one.</summary>
        public double PulsarHz { get; internal set; } = double.NaN;

        internal Dictionary<string, ProvenanceClass> ProvenanceByField =
            new Dictionary<string, ProvenanceClass>(StringComparer.Ordinal);

        /// <summary>True when the position lives in a catalogue rather than in this file.</summary>
        public bool IsResolvedFromCatalogue => !string.IsNullOrEmpty(Source);

        public ProvenanceClass ProvenanceOf(string field)
        {
            ProvenanceClass value;
            return ProvenanceByField.TryGetValue(field, out value) ? value : ProvenanceClass.Unknown;
        }

        public override string ToString() => Name + " (" + Category + ")";
    }

    /// <summary>
    /// The authored places of the Milky Way, read from <c>galaxy/landmarks.json</c> and the
    /// generated <c>galaxy/namedclouds.json</c>, which share the <c>cosmosynth.places/1</c>
    /// schema so that one parser serves both.
    ///
    /// <b>Two rules, and the file is refused if either is broken.</b> Every entry carries a
    /// reference — the owner's WebXR Atlas ships its markers with none, and two of its numbers
    /// are wrong. And a place either states a position or names a catalogue row to take one
    /// from, never both, so a landmark that is also a catalogued object has exactly one
    /// position in the build.
    /// </summary>
    public sealed class PlaceCatalog
    {
        private readonly List<Place> _places = new List<Place>();
        private readonly Dictionary<string, Place> _byId =
            new Dictionary<string, Place>(StringComparer.Ordinal);

        public IReadOnlyList<Place> Places => _places;

        public string Attribution { get; private set; }

        public int Count => _places.Count;

        public Place this[string id]
        {
            get
            {
                Place place;
                return _byId.TryGetValue(id, out place) ? place : null;
            }
        }

        public static PlaceCatalog Parse(string json)
        {
            JsonValue root;
            if (!JsonValue.TryParse(json, out root) || !root.IsObject)
                throw new FormatException("places: not a JSON object.");
            string schema = root["schema"].AsString(null);
            if (schema != "cosmosynth.places/1")
            {
                throw new FormatException(
                    $"places: schema is '{schema}', expected 'cosmosynth.places/1'. Re-run " +
                    "tools/cosmodata/build_authored.py.");
            }

            var catalogue = new PlaceCatalog
            {
                Attribution = root["attribution"].AsString(string.Empty),
            };

            JsonValue places = root["places"];
            if (!places.IsArray || places.Count == 0)
                throw new FormatException("places: the 'places' array is missing or empty.");

            for (int i = 0; i < places.Count; i++)
            {
                JsonValue entry = places[i];
                var place = new Place
                {
                    Id = entry["id"].AsString(null),
                    Name = entry["name"].AsString(null),
                    Category = entry["category"].AsString(null),
                    Description = entry["description"].AsString(string.Empty),
                    Reference = entry["reference"].AsString(null),
                    Source = entry.ContainsKey("source") ? entry["source"].AsString(null) : null,
                    SourceName = entry.ContainsKey("sourceName")
                        ? entry["sourceName"].AsString(null) : null,
                    Closeup = entry.ContainsKey("closeup") ? entry["closeup"].AsString(null) : null,
                };
                if (string.IsNullOrEmpty(place.Id))
                    throw new FormatException($"places[{i}]: no id.");
                if (string.IsNullOrEmpty(place.Reference))
                {
                    throw new FormatException(
                        $"places[{i}] '{place.Id}': no reference. Every place needs exactly one, " +
                        "and build_authored.py refuses a file without them.");
                }
                if (catalogue._byId.ContainsKey(place.Id))
                    throw new FormatException($"places: duplicate id '{place.Id}'.");

                if (place.IsResolvedFromCatalogue)
                {
                    if (string.IsNullOrEmpty(place.SourceName))
                    {
                        throw new FormatException(
                            $"places '{place.Id}': source '{place.Source}' without a sourceName.");
                    }
                }
                else
                {
                    bool hasGalactic = entry.ContainsKey("l") && entry.ContainsKey("b");
                    bool hasEquatorial = entry.ContainsKey("ra") && entry.ContainsKey("dec");
                    if (hasGalactic == hasEquatorial)
                    {
                        throw new FormatException(
                            $"places '{place.Id}': needs exactly one of (l, b) or (ra, dec).");
                    }
                    if (hasGalactic)
                    {
                        place.LongitudeDeg = entry["l"].AsDouble();
                        place.LatitudeDeg = entry["b"].AsDouble();
                    }
                    else
                    {
                        place.RightAscensionDeg = entry["ra"].AsDouble();
                        place.DeclinationDeg = entry["dec"].AsDouble();
                    }
                    if (!entry.ContainsKey("distancePc"))
                        throw new FormatException($"places '{place.Id}': no distancePc.");
                    place.DistancePc = entry["distancePc"].AsDouble();
                }

                if (entry.ContainsKey("distanceErrPc"))
                    place.DistanceErrorPc = entry["distanceErrPc"].AsDouble();
                if (entry.ContainsKey("radiusPc"))
                    place.RadiusPc = entry["radiusPc"].AsDouble();
                if (entry.ContainsKey("pulsarHz"))
                    place.PulsarHz = entry["pulsarHz"].AsDouble();

                JsonValue provenance = entry["provenance"];
                if (provenance.IsObject)
                {
                    foreach (string field in provenance.Keys)
                    {
                        place.ProvenanceByField[field] =
                            ParseClass(provenance[field].AsString(null));
                    }
                }

                catalogue._places.Add(place);
                catalogue._byId[place.Id] = place;
            }
            return catalogue;
        }

        private static ProvenanceClass ParseClass(string text)
        {
            switch (text)
            {
                case "Measured": return ProvenanceClass.Measured;
                case "Inferred": return ProvenanceClass.Inferred;
                case "Procedural": return ProvenanceClass.Procedural;
                case "Fictional": return ProvenanceClass.Fictional;
                default: return ProvenanceClass.Unknown;
            }
        }

        /// <summary>
        /// Position in heliocentric galactic parsecs. Places given as RA/Dec are rotated;
        /// places that name a catalogue are resolved through
        /// <see cref="Resolve(string, PointSet)"/> first and throw until they are.
        /// </summary>
        public static Double3 GalacticPc(Place place)
        {
            if (place == null) throw new ArgumentNullException(nameof(place));
            if (place.IsResolvedFromCatalogue && double.IsNaN(place.DistancePc))
            {
                throw new InvalidOperationException(
                    $"place '{place.Id}' takes its position from '{place.Source}', which has not " +
                    "been supplied. Call PlaceCatalog.Resolve first.");
            }
            if (!double.IsNaN(place.LongitudeDeg))
            {
                // Given in galactic coordinates: no rotation at all, which is the point of
                // storing the authored clouds that way.
                return StandardFrames.FromSpherical(place.LongitudeDeg * MathD.Deg2Rad,
                    place.LatitudeDeg * MathD.Deg2Rad, place.DistancePc);
            }
            return StandardFrames.EquatorialToGalactic * IcrfPc(place);
        }

        /// <summary>Position in ICRF parsecs, the frame every shipped column uses.</summary>
        public static Double3 IcrfPc(Place place)
        {
            if (place == null) throw new ArgumentNullException(nameof(place));
            if (!double.IsNaN(place.RightAscensionDeg))
            {
                return StandardFrames.FromSpherical(place.RightAscensionDeg * MathD.Deg2Rad,
                    place.DeclinationDeg * MathD.Deg2Rad, place.DistancePc);
            }
            if (place.IsResolvedFromCatalogue && double.IsNaN(place.LongitudeDeg))
            {
                throw new InvalidOperationException(
                    $"place '{place.Id}' has not been resolved against '{place.Source}'.");
            }
            return StandardFrames.GalacticToEquatorial * GalacticPc(place);
        }

        /// <summary>
        /// Fill in the positions of every place whose <c>source</c> is <paramref name="source"/>,
        /// from the matching row of <paramref name="points"/>. Returns how many were resolved.
        /// A place whose <c>sourceName</c> is not in the catalogue is an error, not a silent
        /// skip: a landmark that quietly loses its position draws at the Sun.
        /// </summary>
        public int Resolve(string source, PointSet points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            int resolved = 0;
            foreach (Place place in _places)
            {
                if (place.Source != source) continue;
                int row = points.IndexOf(place.SourceName);
                if (row < 0)
                {
                    throw new InvalidOperationException(
                        $"place '{place.Id}' names '{place.SourceName}' in '{source}', and that " +
                        "catalogue has no such row.");
                }
                Double3 galactic = points.GalacticPosition(row);
                double distance = galactic.Magnitude;
                StandardFrames.ToSpherical(galactic, out double lon, out double lat, out _);
                place.LongitudeDeg = lon * MathD.Rad2Deg;
                place.LatitudeDeg = lat * MathD.Rad2Deg;
                place.DistancePc = distance;
                place.DistanceErrorPc = points.DistanceErrorPc(row);
                resolved++;
            }
            return resolved;
        }

        /// <summary>Places of one category, in file order.</summary>
        public IEnumerable<Place> OfCategory(string category)
        {
            foreach (Place place in _places)
                if (string.Equals(place.Category, category, StringComparison.Ordinal))
                    yield return place;
        }

        /// <summary>Merge another places file into this one; ids must not collide.</summary>
        public void Add(PlaceCatalog other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            foreach (Place place in other._places)
            {
                if (_byId.ContainsKey(place.Id))
                    throw new InvalidOperationException($"duplicate place id '{place.Id}'.");
                _places.Add(place);
                _byId[place.Id] = place;
            }
            if (!string.IsNullOrEmpty(other.Attribution))
            {
                Attribution = string.IsNullOrEmpty(Attribution)
                    ? other.Attribution
                    : Attribution + "\n" + other.Attribution;
            }
        }
    }
}
