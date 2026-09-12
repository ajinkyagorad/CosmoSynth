using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CosmoSynth.Core
{
    /// <summary>
    /// The one column-store container every shipped CosmoSynth binary uses.
    ///
    /// Written by tools/cosmodata/csdata.py. Ten datasets would otherwise mean ten
    /// hand-offset parsers on this side of the language boundary, each with its own way of
    /// being subtly wrong about an offset; instead there is one header, one JSON meta block
    /// and one array of 8-byte-aligned columns. The committed fixture
    /// tests/CosmoSynth.Core.Tests/fixtures/csdata_fixture.csd is the contract between the
    /// two implementations, and CsDataTests reads it field by field.
    ///
    /// Layout, little-endian:
    ///     0   4   magic          4 ASCII bytes naming the kind, e.g. "CSS2"
    ///     4   4   version u32    refused if newer than the caller understands
    ///     8   4   rows u32       rows of the primary table
    ///     12  4   flags u32      reserved, must be 0
    ///     16  4   metaBytes u32  UTF-8 JSON, a multiple of 8, space-padded
    ///     20  4   reserved u32   must be 0
    ///     24  8   payloadBytes u64
    ///     32      meta JSON
    ///             columns, each 8-byte aligned from the payload start
    ///
    /// Every refusal names the kind and, where it can, the command that fixes it: the only
    /// useful thing a parse error can tell you is what to run next.
    /// </summary>
    public sealed class CsData
    {
        public const int HeaderBytes = 32;

        private static readonly string[] RequiredMetaKeys =
            { "kind", "schema", "generator", "generatedUtc", "columns" };

        private readonly byte[] _payload;
        private readonly Dictionary<string, ColumnInfo> _columns =
            new Dictionary<string, ColumnInfo>(StringComparer.Ordinal);

        public string Magic { get; }
        public uint Version { get; }
        public int Rows { get; }
        public JsonValue Meta { get; }

        private sealed class ColumnInfo
        {
            public string Name;
            public string Type;
            public int Components;
            public int Offset;
            public int Rows;
            public JsonValue Json;
        }

        private CsData(string magic, uint version, int rows, JsonValue meta, byte[] payload)
        {
            Magic = magic;
            Version = version;
            Rows = rows;
            Meta = meta;
            _payload = payload;
        }

        private static int WidthOf(string type)
        {
            switch (type)
            {
                case "u8":
                case "i8": return 1;
                case "u16":
                case "i16": return 2;
                case "u32":
                case "i32":
                case "f32": return 4;
                case "f64": return 8;
                default: return 0;
            }
        }

        /// <summary>
        /// Parse and validate. <paramref name="expectedMagic"/> is the kind the caller asked
        /// for: a file loaded under the wrong name is refused, never reinterpreted.
        /// </summary>
        public static CsData Parse(byte[] data, string expectedMagic, uint maxVersion = 1)
        {
            if (!BitConverter.IsLittleEndian)
                throw new FormatException(
                    "CosmoSynth containers are little-endian and this machine is not. Every " +
                    "target the project ships to (arm64, x64) is little-endian.");
            if (data == null || data.Length < HeaderBytes)
                throw new FormatException(
                    $"{expectedMagic}: file is shorter than a {HeaderBytes}-byte header. " +
                    "Rebuild it with the importer named in its meta block.");

            string magic = Encoding.ASCII.GetString(data, 0, 4);
            if (magic != expectedMagic)
            {
                throw new FormatException(
                    $"expected a '{expectedMagic}' file and the magic is '{magic}'. " +
                    (magic == "CSS1"
                        ? "CSS1 is the 1.x star format: rebuild with " +
                          "tools/cosmodata/build_starfield.py."
                        : "A file loaded under the wrong name is refused rather than " +
                          "reinterpreted."));
            }

            uint version = BitConverter.ToUInt32(data, 4);
            if (version > maxVersion)
                throw new FormatException(
                    $"{magic}: layout version {version} is newer than this build understands " +
                    $"({maxVersion}). Update the app; do not guess at the layout.");

            uint rows = BitConverter.ToUInt32(data, 8);
            uint flags = BitConverter.ToUInt32(data, 12);
            uint metaBytes = BitConverter.ToUInt32(data, 16);
            uint reserved = BitConverter.ToUInt32(data, 20);
            ulong payloadBytes = BitConverter.ToUInt64(data, 24);

            if (flags != 0 || reserved != 0)
                throw new FormatException($"{magic}: reserved header fields must be zero.");
            if (metaBytes % 8 != 0)
                throw new FormatException(
                    $"{magic}: metaBytes {metaBytes} is not a multiple of 8.");

            long want = HeaderBytes + (long)metaBytes + (long)payloadBytes;
            if (data.LongLength != want)
                throw new FormatException(
                    $"{magic}: the header declares {want} bytes and the file is " +
                    $"{data.LongLength}. A truncated download or a file from another version " +
                    "is refused.");

            string metaText = Encoding.UTF8.GetString(data, HeaderBytes, (int)metaBytes);
            if (!JsonValue.TryParse(metaText, out JsonValue meta) || !meta.IsObject)
                throw new FormatException($"{magic}: the meta block is not a JSON object.");

            foreach (string required in RequiredMetaKeys)
            {
                if (!meta.ContainsKey(required))
                    throw new FormatException($"{magic}: the meta block has no '{required}'.");
            }

            var payload = new byte[payloadBytes];
            Buffer.BlockCopy(data, HeaderBytes + (int)metaBytes, payload, 0, (int)payloadBytes);

            var result = new CsData(magic, version, (int)rows, meta, payload);
            foreach (JsonValue column in meta["columns"])
            {
                string name = column["name"].AsString(null);
                string type = column["type"].AsString(null);
                if (name == null || type == null || WidthOf(type) == 0)
                    throw new FormatException(
                        $"{magic}: column '{name ?? "?"}' has an unknown type '{type ?? "?"}'.");
                int offset = column["offset"].AsInt(-1);
                if (offset < 0 || (offset & 7) != 0)
                    throw new FormatException(
                        $"{magic}: column '{name}' offset {offset} is not 8-byte aligned.");
                int components = column.ContainsKey("components") ? column["components"].AsInt(1) : 1;
                int columnRows = column.ContainsKey("rows") ? column["rows"].AsInt((int)rows) : (int)rows;
                if (components < 1 || columnRows < 0)
                    throw new FormatException($"{magic}: column '{name}' has a negative extent.");
                long end = (long)offset + (long)WidthOf(type) * components * columnRows;
                if (end > payload.LongLength)
                    throw new FormatException(
                        $"{magic}: column '{name}' runs {end - payload.LongLength} bytes past " +
                        "the end of the payload.");
                if (result._columns.ContainsKey(name))
                    throw new FormatException($"{magic}: column '{name}' is declared twice.");
                result._columns[name] = new ColumnInfo
                {
                    Name = name, Type = type, Components = components,
                    Offset = offset, Rows = columnRows, Json = column,
                };
            }
            return result;
        }

        public bool Has(string column) => _columns.ContainsKey(column);

        public IEnumerable<string> ColumnNames => _columns.Keys;

        public int RowsOf(string column) => Require(column, null).Rows;

        public int ComponentsOf(string column) => Require(column, null).Components;

        public JsonValue ColumnMeta(string column) => Require(column, null).Json;

        private ColumnInfo Require(string column, string type)
        {
            if (!_columns.TryGetValue(column, out ColumnInfo info))
                throw new FormatException(
                    $"{Magic} has no column '{column}'. It has: {string.Join(", ", _columns.Keys)}.");
            if (type != null && info.Type != type)
                throw new FormatException(
                    $"{Magic} column '{column}' is {info.Type}, not {type}.");
            return info;
        }

        private int Clamp(ColumnInfo c, int count) =>
            count < 0 ? 0 : (count > c.Rows ? c.Rows : count);

        // A prefix read is the normal case, not an optimisation: the star layer takes the
        // brightest N, and copying 120,000 rows to keep 4,000 shows up as a hitch on a headset
        // and nowhere else. Passing int.MaxValue reads everything.

        public float[] F32(string column, int count = int.MaxValue)
        {
            ColumnInfo c = Require(column, "f32");
            var v = new float[Clamp(c, count) * c.Components];
            Buffer.BlockCopy(_payload, c.Offset, v, 0, v.Length * 4);
            return v;
        }

        public double[] F64(string column, int count = int.MaxValue)
        {
            ColumnInfo c = Require(column, "f64");
            var v = new double[Clamp(c, count) * c.Components];
            Buffer.BlockCopy(_payload, c.Offset, v, 0, v.Length * 8);
            return v;
        }

        public byte[] U8(string column, int count = int.MaxValue)
        {
            ColumnInfo c = Require(column, "u8");
            var v = new byte[Clamp(c, count) * c.Components];
            Buffer.BlockCopy(_payload, c.Offset, v, 0, v.Length);
            return v;
        }

        public sbyte[] I8(string column, int count = int.MaxValue)
        {
            ColumnInfo c = Require(column, "i8");
            var v = new sbyte[Clamp(c, count) * c.Components];
            for (int i = 0; i < v.Length; i++) v[i] = unchecked((sbyte)_payload[c.Offset + i]);
            return v;
        }

        public ushort[] U16(string column, int count = int.MaxValue)
        {
            ColumnInfo c = Require(column, "u16");
            var v = new ushort[Clamp(c, count) * c.Components];
            Buffer.BlockCopy(_payload, c.Offset, v, 0, v.Length * 2);
            return v;
        }

        public short[] I16(string column, int count = int.MaxValue)
        {
            ColumnInfo c = Require(column, "i16");
            var v = new short[Clamp(c, count) * c.Components];
            Buffer.BlockCopy(_payload, c.Offset, v, 0, v.Length * 2);
            return v;
        }

        public uint[] U32(string column, int count = int.MaxValue)
        {
            ColumnInfo c = Require(column, "u32");
            var v = new uint[Clamp(c, count) * c.Components];
            Buffer.BlockCopy(_payload, c.Offset, v, 0, v.Length * 4);
            return v;
        }

        public int[] I32(string column, int count = int.MaxValue)
        {
            ColumnInfo c = Require(column, "i32");
            var v = new int[Clamp(c, count) * c.Components];
            Buffer.BlockCopy(_payload, c.Offset, v, 0, v.Length * 4);
            return v;
        }

        /// <summary>Decode a quantised value using the encoding declared in the column's meta.</summary>
        public double Decode(string column, double raw)
        {
            JsonValue encoding = Require(column, null).Json["encoding"];
            if (!encoding.IsObject) return raw;
            switch (encoding["kind"].AsString(""))
            {
                case "linear":
                    return raw * encoding["scale"].AsDouble(1.0) + encoding["offset"].AsDouble(0.0);
                case "log8":
                {
                    if (raw <= 0.0) return 0.0;
                    double lo = encoding["lo"].AsDouble(1.0);
                    double hi = encoding["hi"].AsDouble(1.0);
                    return lo * Math.Pow(hi / lo, (raw - 1.0) / 254.0);
                }
                default:
                    return raw;
            }
        }

        /// <summary>
        /// Provenance of one field on one row: the file's per-field default, narrowed by any
        /// flag bit or bit-field code that overrides it. This is how a record says "measured,
        /// except this one is not" without carrying a string on every row.
        /// </summary>
        public FieldProvenance ProvenanceFor(string field, uint rowFlags)
        {
            FieldProvenance result = DefaultProvenance(field);

            JsonValue bits = Meta["provenanceBits"];
            if (bits.IsArray)
            {
                foreach (JsonValue entry in bits)
                {
                    if (entry["field"].AsString(null) != field) continue;
                    int bit = entry["bit"].AsInt(-1);
                    if (bit < 0 || bit > 31 || (rowFlags & (1u << bit)) == 0) continue;
                    result = new FieldProvenance(
                        ParseClass(entry["class"].AsString("Inferred")),
                        result.Source,
                        entry["note"].AsString(result.Reference));
                }
            }

            JsonValue codes = Meta["provenanceCodes"];
            if (codes.IsArray)
            {
                foreach (JsonValue entry in codes)
                {
                    if (entry["field"].AsString(null) != field) continue;
                    JsonValue span = entry["bits"];
                    if (!span.IsArray || span.Count != 2) continue;
                    int low = span[0].AsInt(0), high = span[1].AsInt(0);
                    if (high < low || high > 31 || low < 0) continue;
                    uint mask = (uint)(((1L << (high - low + 1)) - 1) << low);
                    uint value = (rowFlags & mask) >> low;
                    JsonValue named = entry["values"][value.ToString(CultureInfo.InvariantCulture)];
                    if (!named.IsObject) continue;
                    result = new FieldProvenance(
                        ParseClass(named["class"].AsString("Measured")),
                        result.Source,
                        named["note"].AsString(result.Reference));
                }
            }
            return result;
        }

        private FieldProvenance DefaultProvenance(string field)
        {
            JsonValue entry = Meta["provenance"][field];
            if (!entry.IsObject)
                return new FieldProvenance(ProvenanceClass.Unknown, PrimarySourceId(), null);
            return new FieldProvenance(
                ParseClass(entry["class"].AsString("Unknown")),
                entry["source"].AsString(PrimarySourceId()),
                entry["reference"].AsString(null));
        }

        private static ProvenanceClass ParseClass(string name)
        {
            switch (name)
            {
                case "Measured": return ProvenanceClass.Measured;
                case "Inferred": return ProvenanceClass.Inferred;
                case "Procedural": return ProvenanceClass.Procedural;
                case "Fictional": return ProvenanceClass.Fictional;
                default: return ProvenanceClass.Unknown;
            }
        }

        private string PrimarySourceId()
        {
            JsonValue sources = Meta["sources"];
            return sources.IsArray && sources.Count > 0 ? sources[0]["id"].AsString("") : "";
        }

        /// <summary>
        /// Every attribution line the shipped file obliges the app to display, joined by
        /// newlines. A CC BY-SA obligation is not satisfied by a comment in a source file, so
        /// this string exists to be drawn.
        /// </summary>
        public string Attribution()
        {
            var lines = new List<string>();
            JsonValue sources = Meta["sources"];
            if (sources.IsArray)
            {
                foreach (JsonValue source in sources)
                {
                    string text = source["attribution"].AsString(null);
                    if (!string.IsNullOrEmpty(text) && !lines.Contains(text)) lines.Add(text);
                }
            }
            return string.Join("\n", lines);
        }

        /// <summary>The licence profile the importer ran under: sideload-nc, public-nc or commercial.</summary>
        public string LicenceProfile => Meta["licenceProfile"].AsString("unknown");

        public override string ToString() =>
            $"{Magic} v{Version}, {Rows} rows, {_columns.Count} columns, profile {LicenceProfile}";
    }
}
