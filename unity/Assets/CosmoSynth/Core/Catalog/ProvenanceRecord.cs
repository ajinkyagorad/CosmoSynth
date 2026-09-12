using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>
    /// How a particular value came to be known. Every physical quantity in the catalogue
    /// carries one of these.
    ///
    /// This is the distinction most universe simulators quietly erase. A viewer looking at a
    /// rendered exoplanet cannot otherwise tell that its orbital period was measured to seven
    /// digits, its radius inferred from a transit depth, its surface invented by a noise
    /// function and its moons made up entirely. Recording the difference per field — not per
    /// object — is what separates a scientific instrument from an attractive picture, and it
    /// costs almost nothing to carry.
    /// </summary>
    public enum ProvenanceClass
    {
        /// <summary>Unstated. Treated as the weakest claim; the UI should surface it as such.</summary>
        Unknown = 0,

        /// <summary>Directly observed and published, with an uncertainty where one exists.</summary>
        Measured = 1,

        /// <summary>Derived from measurements through an accepted physical model.</summary>
        Inferred = 2,

        /// <summary>Synthesised by our own generators to fill a gap. Deterministic, but invented.</summary>
        Procedural = 3,

        /// <summary>Authored for narrative or teaching purposes. Makes no claim about reality.</summary>
        Fictional = 4
    }

    /// <summary>Where one value came from, and how much to trust it.</summary>
    [Serializable]
    public struct FieldProvenance
    {
        public ProvenanceClass Class;

        /// <summary>Human-readable dataset name, e.g. "JPL Horizons (DE440)".</summary>
        public string Source;

        /// <summary>Citation or URL that a user can follow to the primary data.</summary>
        public string Reference;

        /// <summary>
        /// One-sigma uncertainty in the same unit as the value, or NaN when the source does
        /// not publish one. NaN means "not stated", which is different from zero and must
        /// never be rendered as an error bar of zero.
        /// </summary>
        public double Uncertainty;

        /// <summary>ISO-8601 date the value was retrieved, so a stale import is visible.</summary>
        public string RetrievedUtc;

        public FieldProvenance(ProvenanceClass provenanceClass, string source = null, string reference = null,
            double uncertainty = double.NaN, string retrievedUtc = null)
        {
            Class = provenanceClass;
            Source = source;
            Reference = reference;
            Uncertainty = uncertainty;
            RetrievedUtc = retrievedUtc;
        }

        public bool HasUncertainty => !double.IsNaN(Uncertainty);

        /// <summary>True when the value rests on observation rather than invention.</summary>
        public bool IsEmpirical => Class == ProvenanceClass.Measured || Class == ProvenanceClass.Inferred;

        public static FieldProvenance Measured(string source, string reference = null,
            double uncertainty = double.NaN, string retrievedUtc = null) =>
            new FieldProvenance(ProvenanceClass.Measured, source, reference, uncertainty, retrievedUtc);

        public static FieldProvenance Inferred(string source, string reference = null,
            double uncertainty = double.NaN) =>
            new FieldProvenance(ProvenanceClass.Inferred, source, reference, uncertainty);

        public static FieldProvenance Procedural(string generator, long seed) =>
            new FieldProvenance(ProvenanceClass.Procedural, generator, $"seed:{seed}");

        public static FieldProvenance Fictional(string author = null) =>
            new FieldProvenance(ProvenanceClass.Fictional, author);

        public JsonValue ToJson()
        {
            var json = JsonValue.Object();
            json["class"] = JsonValue.String(Class.ToString());
            if (!string.IsNullOrEmpty(Source)) json["source"] = JsonValue.String(Source);
            if (!string.IsNullOrEmpty(Reference)) json["reference"] = JsonValue.String(Reference);
            if (HasUncertainty) json["uncertainty"] = JsonValue.Number(Uncertainty);
            if (!string.IsNullOrEmpty(RetrievedUtc)) json["retrieved"] = JsonValue.String(RetrievedUtc);
            return json;
        }

        public static FieldProvenance FromJson(JsonValue json)
        {
            if (json == null || !json.IsObject) return new FieldProvenance(ProvenanceClass.Unknown);

            var result = new FieldProvenance
            {
                Source = json["source"].AsString(),
                Reference = json["reference"].AsString(),
                Uncertainty = json.ContainsKey("uncertainty") ? json["uncertainty"].AsDouble(double.NaN) : double.NaN,
                RetrievedUtc = json["retrieved"].AsString()
            };

            string name = json["class"].AsString("Unknown");
            result.Class = Enum.TryParse(name, ignoreCase: true, out ProvenanceClass parsed)
                ? parsed
                : ProvenanceClass.Unknown;

            return result;
        }

        public override string ToString()
        {
            string uncertainty = HasUncertainty ? $" +/-{Uncertainty:G4}" : string.Empty;
            string source = string.IsNullOrEmpty(Source) ? string.Empty : $" [{Source}]";
            return $"{Class}{uncertainty}{source}";
        }
    }

    /// <summary>
    /// Per-field provenance for one object, keyed by field name.
    ///
    /// Absence is meaningful: a field with no entry is reported as <see cref="ProvenanceClass.Unknown"/>
    /// rather than assumed to be measured. Optimistic defaults here would defeat the point.
    /// </summary>
    [Serializable]
    public sealed class ProvenanceRecord
    {
        private readonly Dictionary<string, FieldProvenance> _fields =
            new Dictionary<string, FieldProvenance>(StringComparer.Ordinal);

        public int Count => _fields.Count;
        public IEnumerable<string> Fields => _fields.Keys;

        public void Set(string field, FieldProvenance provenance) => _fields[field] = provenance;

        public FieldProvenance Get(string field) =>
            _fields.TryGetValue(field, out var value) ? value : new FieldProvenance(ProvenanceClass.Unknown);

        public bool Has(string field) => _fields.ContainsKey(field);

        /// <summary>
        /// The weakest claim made anywhere in this record. Suitable for a single badge on an
        /// object: a world whose orbit is measured but whose surface is invented should not be
        /// presented as "measured".
        /// </summary>
        public ProvenanceClass WeakestClass()
        {
            if (_fields.Count == 0) return ProvenanceClass.Unknown;

            // Ordering is by strength of claim, not by enum value: Unknown is weakest of all.
            var strength = new Dictionary<ProvenanceClass, int>
            {
                { ProvenanceClass.Unknown, 0 },
                { ProvenanceClass.Fictional, 1 },
                { ProvenanceClass.Procedural, 2 },
                { ProvenanceClass.Inferred, 3 },
                { ProvenanceClass.Measured, 4 }
            };

            ProvenanceClass weakest = ProvenanceClass.Measured;
            foreach (var entry in _fields.Values)
            {
                if (strength[entry.Class] < strength[weakest]) weakest = entry.Class;
            }
            return weakest;
        }

        /// <summary>Count of fields in each class, for a provenance summary panel.</summary>
        public Dictionary<ProvenanceClass, int> Summary()
        {
            var counts = new Dictionary<ProvenanceClass, int>();
            foreach (var entry in _fields.Values)
            {
                counts.TryGetValue(entry.Class, out int existing);
                counts[entry.Class] = existing + 1;
            }
            return counts;
        }

        public JsonValue ToJson()
        {
            var json = JsonValue.Object();
            foreach (var pair in _fields) json[pair.Key] = pair.Value.ToJson();
            return json;
        }

        public static ProvenanceRecord FromJson(JsonValue json)
        {
            var record = new ProvenanceRecord();
            if (json == null || !json.IsObject) return record;
            foreach (string key in json.Keys) record.Set(key, FieldProvenance.FromJson(json[key]));
            return record;
        }
    }
}
