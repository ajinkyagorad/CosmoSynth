using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Per-row facts a point set can carry about how much to believe its own numbers.
    /// The bits match <c>tools/cosmodata/build_galaxy_points.py</c> and are described, bit by
    /// bit, in each file's <c>meta.provenanceBits</c> — so a renderer can explain a flag without
    /// this enum, and this enum exists so the tests need not count bits.
    /// </summary>
    [Flags]
    public enum PointFlags
    {
        None = 0,

        /// <summary>Distance is implausible for this population. Kept, drawn dim, not trusted.</summary>
        Outlier = 1 << 0,

        /// <summary>No absolute magnitude was published. The stored value is NaN, not zero.</summary>
        NoAbsoluteMagnitude = 1 << 1,

        /// <summary>Parallax signal-to-noise below 5, so 1/parallax is not a distance.</summary>
        LowSignalParallax = 1 << 2,

        /// <summary>Hosted by a satellite galaxy rather than by the Milky Way itself.</summary>
        NotMilkyWayHost = 1 << 3,

        /// <summary>
        /// A kinematic near/far distance whose ambiguity was never resolved. 663 of the 1,039
        /// molecular clouds are like this: the distance column holds a choice, not a measurement,
        /// and drawing one of these the same as a parallax distance is the lie this bit prevents.
        /// </summary>
        AmbiguousDistance = 1 << 4,

        /// <summary>Not a member of the structure this file describes.</summary>
        NotAMember = 1 << 5,
    }

    /// <summary>
    /// A population of catalogued points: globular clusters, molecular clouds, the Radcliffe
    /// Wave's spine and its clusters, and later the Cosmicflows group and SDSS files.
    ///
    /// Positions are ICRF parsecs, as every shipped position column is. Everything beyond
    /// position, distance uncertainty and flags is an optional named column, because the four
    /// populations that share this type have almost nothing else in common — and inventing a
    /// union type with six mostly-NaN fields would only move the optionality somewhere less
    /// honest.
    /// </summary>
    public sealed class PointSet
    {
        private readonly CsData _data;
        private readonly float[] _pos;
        private readonly float[] _distErr;
        private readonly ushort[] _flags;
        private readonly Dictionary<string, float[]> _extra =
            new Dictionary<string, float[]>(StringComparer.Ordinal);

        /// <summary>Which population this is: globulars, clouds, radcliffe, ...</summary>
        public string Kind { get; private set; }

        public int Count { get; private set; }

        /// <summary>Names, where the catalogue has them; empty otherwise.</summary>
        public string[] Names { get; private set; }

        /// <summary>True when consecutive rows form one open polyline rather than a cloud.</summary>
        public bool IsPolyline { get; private set; }

        public CsData Data => _data;

        private PointSet(CsData data, float[] pos, float[] distErr, ushort[] flags)
        {
            _data = data;
            _pos = pos;
            _distErr = distErr;
            _flags = flags;
        }

        public static PointSet Parse(byte[] file, string expectedKind = null)
        {
            CsData data = CsData.Parse(file, "CSGP");
            string kind = data.Meta["kind"].AsString(null);
            if (expectedKind != null && kind != expectedKind)
            {
                throw new FormatException(
                    $"CSGP: this is a '{kind}' point set and a '{expectedKind}' one was asked " +
                    "for. All the galactic point sets share one magic; meta.kind is what " +
                    "distinguishes them.");
            }

            float[] pos = data.F32("pos");
            if (data.ComponentsOf("pos") != 3)
                throw new FormatException($"CSGP '{kind}': pos must have three components.");
            int count = data.RowsOf("pos");
            if (pos.Length != count * 3)
                throw new FormatException($"CSGP '{kind}': pos has the wrong length.");

            float[] distErr = data.Has("distErr") ? data.F32("distErr") : new float[count];
            ushort[] flags = data.Has("flags") ? data.U16("flags") : new ushort[count];
            if (distErr.Length != count || flags.Length != count)
                throw new FormatException($"CSGP '{kind}': columns disagree on the row count.");

            var set = new PointSet(data, pos, distErr, flags)
            {
                Kind = kind,
                Count = count,
                Names = data.Strings("names"),
                IsPolyline = data.Meta.ContainsKey("polyline") &&
                             data.Meta["polyline"].AsBool(false),
            };
            if (set.Names.Length != 0 && set.Names.Length != count)
            {
                throw new FormatException(
                    $"CSGP '{kind}': {set.Names.Length} names for {count} rows.");
            }

            // Every other f32 column becomes an optional named value. Read from the meta's
            // own column list rather than a hard-coded set, so an importer that adds a column
            // needs no change here.
            JsonValue columns = data.Meta["columns"];
            for (int i = 0; i < columns.Count; i++)
            {
                string name = columns[i]["name"].AsString(null);
                if (string.IsNullOrEmpty(name)) continue;
                if (name == "pos" || name == "distErr" || name == "flags") continue;
                if (columns[i]["type"].AsString(null) != "f32") continue;
                set._extra[name] = data.F32(name);
            }
            return set;
        }

        /// <summary>Position in ICRF parsecs.</summary>
        public Double3 Position(int row) =>
            new Double3(_pos[row * 3], _pos[row * 3 + 1], _pos[row * 3 + 2]);

        public Double3 GalacticPosition(int row) =>
            StandardFrames.EquatorialToGalactic * Position(row);

        public double DistancePc(int row) => Position(row).Magnitude;

        /// <summary>One-sigma distance uncertainty in parsecs, 0 where none was published.</summary>
        public double DistanceErrorPc(int row) => _distErr[row];

        public PointFlags Flags(int row) => (PointFlags)_flags[row];

        public bool Has(string column) => _extra.ContainsKey(column);

        /// <summary>A named optional column, or NaN when this population has no such column.</summary>
        public double Value(string column, int row)
        {
            float[] values;
            if (!_extra.TryGetValue(column, out values)) return double.NaN;
            return values[row];
        }

        public string Name(int row) => Names.Length == 0 ? string.Empty : Names[row];

        /// <summary>Row index of a named object, or -1. Linear: these catalogues are small.</summary>
        public int IndexOf(string name)
        {
            for (int i = 0; i < Names.Length; i++)
                if (string.Equals(Names[i], name, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>Provenance of one field of one row, including that row's flag overrides.</summary>
        public FieldProvenance ProvenanceFor(string field, int row)
        {
            FieldProvenance provenance = _data.ProvenanceFor(field, _flags[row]);
            // The uncertainty is per row, so it cannot come from the file-wide meta block.
            // NaN stays NaN: "not stated" is different from zero and must never be drawn as an
            // error bar of zero.
            if (field == "pos" && _distErr[row] > 0.0) provenance.Uncertainty = _distErr[row];
            return provenance;
        }

        public string Attribution() => _data.Attribution();
    }
}
