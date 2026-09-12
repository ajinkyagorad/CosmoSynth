using System;
using System.Collections.Generic;
using System.Globalization;

namespace CosmoSynth.Core
{
    /// <summary>
    /// The one column-store container every new CosmoSynth binary uses. One header, one JSON
    /// meta block, one array of 8-byte-aligned columns.
    ///
    /// Layout (design B §1.1), all integers little-endian:
    ///
    ///     0   4   magic         4 ASCII bytes naming the kind
    ///     4   4   version u32
    ///     8   4   rows u32      rows of the primary table
    ///     12  4   flags u32     reserved, must be 0
    ///     16  4   metaBytes u32 a multiple of 8, space padded
    ///     20  4   reserved u32  must be 0
    ///     24  8   payloadBytes u64
    ///     32      meta JSON, then the columns
    ///
    /// Written by tools/cosmodata/csdata.py. The committed fixture
    /// tests/CosmoSynth.Core.Tests/fixtures/csdata_fixture.csd is the contract between the
    /// two, and <see cref="CsDataTests"/> parses it field by field: the alternative is two
    /// implementations of one layout drifting apart silently, which is precisely how a
    /// column offset becomes a wrong universe.
    ///
    /// Every refusal is a <see cref="FormatException"/> naming the kind and the importer to
    /// re-run, following <see cref="StarCatalog"/>'s existing wording.
    /// </summary>
    public sealed class CsData
    {
        public const int HeaderBytes = 32;

        private readonly byte[] _bytes;
        private readonly int _payloadStart;
        private readonly Dictionary<string, ColumnInfo> _columns;

        public string Magic { get; }
        public uint Version { get; }
        public int Rows { get; }
        public JsonValue Meta { get; }

        private readonly struct ColumnInfo
        {
            public readonly string Type;
            public readonly int Components;
            public readonly int Rows;
            public readonly int Offset;

            public ColumnInfo(string type, int components, int rows, int offset)
            {
                Type = type;
                Components = components;
                Rows = rows;
                Offset = offset;
            }

            public int ElementBytes => WidthOf(Type) * Components;
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

        private CsData(byte[] bytes, string magic, uint version, int rows, JsonValue meta,
            int payloadStart, Dictionary<string, ColumnInfo> columns)
        {
            _bytes = bytes;
            _payloadStart = payloadStart;
            _columns = columns;
            Magic = magic;
            Version = version;
            Rows = rows;
            Meta = meta;
        }

        public static CsData Parse(byte[] data, string expectedMagic, uint maxVersion = 1)
        {
            if (!BitConverter.IsLittleEndian)
            {
                throw new FormatException(
                    "CosmoSynth data containers are little-endian and this machine is not. " +
                    "Every target the project ships to (arm64, x64) is little-endian.");
            }
            if (data == null || data.Length < HeaderBytes)
            {
                throw new FormatException(
                    $"{expectedMagic}: file is shorter than a {HeaderBytes}-byte header. " +
                    "Rebuild it with the importer named in its meta block.");
            }

            string magic = System.Text.Encoding.ASCII.GetString(data, 0, 4);
            if (magic != expectedMagic)
            {
                throw new FormatException(
                    $"expected a '{expectedMagic}' file and the magic is '{magic}'. " +
                    "A file loaded under the wrong name is refused rather than reinterpreted.");
            }

            uint version = BitConverter.ToUInt32(data, 4);
            if (version > maxVersion)
            {
                throw new FormatException(
                    $"{magic}: layout version {version} is newer than this build understands " +
                    $"({maxVersion}). Update the app, do not guess at the layout.");
            }

            uint rows = BitConverter.ToUInt32(data, 8);
            uint flags = BitConverter.ToUInt32(data, 12);
            uint metaBytes = BitConverter.ToUInt32(data, 16);
            uint reserved = BitConverter.ToUInt32(data, 20);
            ulong payloadBytes = BitConverter.ToUInt64(data, 24);

            if (flags != 0 || reserved != 0)
                throw new FormatException($"{magic}: reserved header fields must be zero.");
            if (metaBytes % 8 != 0)
                throw new FormatException($"{magic}: metaBytes {metaBytes} is not a multiple of 8.");

            long want = HeaderBytes + (long)metaBytes + (long)payloadBytes;
            if (data.LongLength != want)
            {
                throw new FormatException(
                    $"{magic}: the header declares {want} bytes and the file is {data.LongLength}. " +
                    "A truncated download or a file from another version is refused.");
            }

            string metaText = System.Text.Encoding.UTF8.GetString(data, HeaderBytes, (int)metaBytes);
            if (!JsonValue.TryParse(metaText, out JsonValue meta) || !meta.IsObject)
                throw new FormatException($"{magic}: the meta block is not a JSON object.");

            foreach (string required in RequiredMetaKeys)
            {
                if (!meta.ContainsKey(required))
                    throw new FormatException($"{magic}: the meta block has no '{required}'.");
            }

            int payloadStart = HeaderBytes + (int)metaBytes;
            var columns = new Dictionary<string, ColumnInfo>(StringComparer.Ordinal);
            JsonValue columnList = meta["columns"];
            if (!columnList.IsArray)
                throw new FormatException($"{magic}: 'columns' is not an array.");

            foreach (JsonValue c in columnList)
            {
                string name = c["name"].AsString();
                string type = c["type"].AsString();
                if (string.IsNullOrEmpty(name) || WidthOf(type) == 0)
                    throw new FormatException($"{magic}: column '{name}' has unknown type '{type}'.");

                int offset = c["offset"].AsInt(-1);
                if (offset < 0 || offset % 8 != 0)
                    throw new FormatException($"{magic}: column '{name}' offset {offset} is not 8-byte aligned.");

                int components = c.ContainsKey("components") ? c["components"].AsInt(1) : 1;
                int columnRows = c.ContainsKey("rows") ? c["rows"].AsInt((int)rows) : (int)rows;
                long end = (long)offset + (long)columnRows * components * WidthOf(type);
                if (end > (long)payloadBytes)
                {
                    throw new FormatException(
                        $"{magic}: column '{name}' runs {end - (long)payloadBytes} bytes past the " +
                        "end of the payload.");
                }
                columns[name] = new ColumnInfo(type, components, columnRows, offset);
            }

            return new CsData(data, magic, version, (int)rows, meta, payloadStart, columns);
        }

        private static readonly string[] RequiredMetaKeys =
        {
            "kind", "schema", "generator", "generatedUtc", "licenceProfile", "frame",
            "sources", "provenance", "columns",
        };

        public bool Has(string column) => _columns.ContainsKey(column);

        public int RowsOf(string column) => Info(column).Rows;

        public int ComponentsOf(string column) => Info(column).Components;

        private ColumnInfo Info(string column)
        {
            if (!_columns.TryGetValue(column, out ColumnInfo info))
            {
                throw new FormatException(
                    $"{Magic}: there is no column '{column}'. Rebuild the file with " +
                    Meta["generator"].AsString("its importer") + ".");
            }
            return info;
        }

        private T[] Read<T>(string column, string expectedType, int elementBytes)
        {
            ColumnInfo info = Info(column);
            if (info.Type != expectedType)
            {
                throw new FormatException(
                    $"{Magic}: column '{column}' is {info.Type}, not {expectedType}.");
            }
            int count = info.Rows * info.Components;
            var result = new T[count];
            Buffer.BlockCopy(_bytes, _payloadStart + info.Offset, result, 0, count * elementBytes);
            return result;
        }

        public float[] F32(string column) => Read<float>(column, "f32", 4);
        public double[] F64(string column) => Read<double>(column, "f64", 8);
        public short[] I16(string column) => Read<short>(column, "i16", 2);
        public ushort[] U16(string column) => Read<ushort>(column, "u16", 2);
        public int[] I32(string column) => Read<int>(column, "i32", 4);
        public uint[] U32(string column) => Read<uint>(column, "u32", 4);

        /// <summary>Byte columns, copied out so a caller cannot scribble on the file image.</summary>
        public byte[] U8(string column)
        {
            ColumnInfo info = Info(column);
            if (info.Type != "u8" && info.Type != "i8")
                throw new FormatException($"{Magic}: column '{column}' is {info.Type}, not u8/i8.");
            int count = info.Rows * info.Components;
            var result = new byte[count];
            Buffer.BlockCopy(_bytes, _payloadStart + info.Offset, result, 0, count);
            return result;
        }

        public sbyte[] I8(string column)
        {
            byte[] raw = U8(column);
            var result = new sbyte[raw.Length];
            for (int i = 0; i < raw.Length; i++) result[i] = unchecked((sbyte)raw[i]);
            return result;
        }

        /// <summary>Meta string arrays: names, refs, code tables. Empty where absent.</summary>
        public string[] Strings(string key)
        {
            if (!Meta.ContainsKey(key) || !Meta[key].IsArray) return Array.Empty<string>();
            JsonValue array = Meta[key];
            var result = new string[array.Count];
            for (int i = 0; i < array.Count; i++) result[i] = array[i].AsString(string.Empty);
            return result;
        }

        /// <summary>
        /// The provenance of one field of one row.
        ///
        /// A field's baseline class comes from meta `provenance`. A row's `flags` word can
        /// then override it through meta `provenanceBits`: that is how 536 unmeasured
        /// ellipticities in one file say "Inferred" while the other 394 say "Measured",
        /// without shipping a string per row. Later bits win, so a file may both weaken a
        /// field (the source left it blank) and strengthen it (it was filled from a second
        /// catalogue) and the last word in the list is the one that applies.
        /// </summary>
        public FieldProvenance ProvenanceFor(string field, uint rowFlags)
        {
            var result = new FieldProvenance(ProvenanceClass.Unknown);
            JsonValue baseline = Meta["provenance"];
            if (baseline.IsObject && baseline.ContainsKey(field))
                result = FieldProvenance.FromJson(baseline[field]);

            if (!Meta.ContainsKey("provenanceBits")) return result;
            foreach (JsonValue bit in Meta["provenanceBits"])
            {
                if (bit["field"].AsString() != field) continue;
                int index = bit["bit"].AsInt(-1);
                if (index < 0 || index > 31) continue;
                if ((rowFlags & (1u << index)) == 0) continue;

                FieldProvenance over = FieldProvenance.FromJson(bit);
                if (string.IsNullOrEmpty(over.Source)) over.Source = result.Source;
                if (string.IsNullOrEmpty(over.Reference)) over.Reference = result.Reference;
                result = over;
            }
            return result;
        }

        /// <summary>
        /// Every attribution string this file's licences require, one per line. CC BY and
        /// CC BY-SA oblige the build to carry these where a user can read them; a string that
        /// lives only in an importer's source satisfies nothing.
        /// </summary>
        public string Attribution()
        {
            var lines = new List<string>();
            if (Meta.ContainsKey("sources"))
            {
                foreach (JsonValue source in Meta["sources"])
                {
                    string text = source["attribution"].AsString(null);
                    if (!string.IsNullOrEmpty(text) && !lines.Contains(text)) lines.Add(text);
                }
            }
            return string.Join("\n", lines);
        }

        /// <summary>A count recorded by the importer, or -1. `counts` is where a row budget lives.</summary>
        public int Count(string name)
        {
            if (!Meta.ContainsKey("counts")) return -1;
            JsonValue counts = Meta["counts"];
            return counts.ContainsKey(name) ? counts[name].AsInt(-1) : -1;
        }

        /// <summary>
        /// Did the importer's own named check pass? An importer that fails an assertion writes
        /// nothing, so in a shipped file these are all true — but a reader can see *what was
        /// checked*, which is the point of recording them.
        /// </summary>
        public bool Asserted(string name)
        {
            if (!Meta.ContainsKey("assertions")) return false;
            foreach (JsonValue a in Meta["assertions"])
            {
                if (a["name"].AsString() == name) return a["ok"].AsBool();
            }
            return false;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0} v{1}, {2} rows, {3} columns",
                Magic, Version, Rows, _columns.Count);
    }
}
