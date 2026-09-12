using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CosmoSynth.Core
{
    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>
    /// A small, dependency-free JSON reader and writer.
    ///
    /// Rolling our own rather than taking a package is a deliberate trade. Unity's built-in
    /// <c>JsonUtility</c> cannot express dictionaries, nulls or polymorphic records, all of
    /// which the catalogue format needs; Newtonsoft would work inside Unity but would drag a
    /// package dependency into an assembly whose whole point is that it compiles on a bare
    /// .NET runtime with no Unity present. A few hundred lines here keeps the catalogue
    /// format identical in both environments and keeps the test suite restorable offline.
    ///
    /// Scope is honest about being small: it parses the JSON grammar completely (RFC 8259,
    /// including escapes, surrogate pairs and exponent forms) but offers no schema binding,
    /// streaming or comment support.
    /// </summary>
    public sealed class JsonValue : IEnumerable<JsonValue>
    {
        public JsonKind Kind { get; private set; }

        private bool _bool;
        private double _number;
        private string _string;
        private List<JsonValue> _array;
        private Dictionary<string, JsonValue> _object;

        // ---- Construction ---------------------------------------------------

        private JsonValue() { }

        public static JsonValue Null() => new JsonValue { Kind = JsonKind.Null };

        public static JsonValue Bool(bool value) => new JsonValue { Kind = JsonKind.Bool, _bool = value };

        public static JsonValue Number(double value) => new JsonValue { Kind = JsonKind.Number, _number = value };

        public static JsonValue String(string value) =>
            value == null ? Null() : new JsonValue { Kind = JsonKind.String, _string = value };

        public static JsonValue Array(IEnumerable<JsonValue> values = null) =>
            new JsonValue { Kind = JsonKind.Array, _array = values != null ? new List<JsonValue>(values) : new List<JsonValue>() };

        public static JsonValue Object() =>
            new JsonValue { Kind = JsonKind.Object, _object = new Dictionary<string, JsonValue>(StringComparer.Ordinal) };

        /// <summary>Serialise a <see cref="Double3"/> as a three-element array.</summary>
        public static JsonValue Vector(Double3 v) =>
            Array(new[] { Number(v.x), Number(v.y), Number(v.z) });

        // ---- Inspection -----------------------------------------------------

        public bool IsNull => Kind == JsonKind.Null;
        public bool IsObject => Kind == JsonKind.Object;
        public bool IsArray => Kind == JsonKind.Array;
        public bool IsNumber => Kind == JsonKind.Number;
        public bool IsString => Kind == JsonKind.String;

        public int Count
        {
            get
            {
                if (Kind == JsonKind.Array) return _array.Count;
                if (Kind == JsonKind.Object) return _object.Count;
                return 0;
            }
        }

        public IEnumerable<string> Keys => Kind == JsonKind.Object ? (IEnumerable<string>)_object.Keys : System.Array.Empty<string>();

        /// <summary>
        /// Index into an object. A missing key yields a null value rather than throwing, so
        /// that reading optional catalogue fields does not need a guard at every call site;
        /// use <see cref="ContainsKey"/> when absence is meaningful.
        /// </summary>
        public JsonValue this[string key]
        {
            get
            {
                if (Kind == JsonKind.Object && _object.TryGetValue(key, out var value)) return value;
                return Null();
            }
            set
            {
                if (Kind != JsonKind.Object)
                    throw new InvalidOperationException($"Cannot set key '{key}' on a JSON {Kind}.");
                _object[key] = value ?? Null();
            }
        }

        public JsonValue this[int index]
        {
            get
            {
                if (Kind == JsonKind.Array && index >= 0 && index < _array.Count) return _array[index];
                return Null();
            }
        }

        public bool ContainsKey(string key) => Kind == JsonKind.Object && _object.ContainsKey(key);

        public void Add(JsonValue value)
        {
            if (Kind != JsonKind.Array) throw new InvalidOperationException($"Cannot append to a JSON {Kind}.");
            _array.Add(value ?? Null());
        }

        public IEnumerator<JsonValue> GetEnumerator() =>
            (Kind == JsonKind.Array ? (IEnumerable<JsonValue>)_array : System.Array.Empty<JsonValue>()).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // ---- Typed access ---------------------------------------------------

        public double AsDouble(double fallback = 0.0) => Kind == JsonKind.Number ? _number : fallback;

        public float AsFloat(float fallback = 0f) => Kind == JsonKind.Number ? (float)_number : fallback;

        public int AsInt(int fallback = 0) => Kind == JsonKind.Number ? (int)System.Math.Round(_number) : fallback;

        public long AsLong(long fallback = 0) => Kind == JsonKind.Number ? (long)System.Math.Round(_number) : fallback;

        public bool AsBool(bool fallback = false) => Kind == JsonKind.Bool ? _bool : fallback;

        public string AsString(string fallback = null)
        {
            switch (Kind)
            {
                case JsonKind.String: return _string;
                case JsonKind.Number: return _number.ToString("R", CultureInfo.InvariantCulture);
                case JsonKind.Bool: return _bool ? "true" : "false";
                default: return fallback;
            }
        }

        /// <summary>Read a three-element array as a vector.</summary>
        public Double3 AsVector(Double3 fallback = default)
        {
            if (Kind != JsonKind.Array || _array.Count < 3) return fallback;
            return new Double3(_array[0].AsDouble(), _array[1].AsDouble(), _array[2].AsDouble());
        }

        /// <summary>
        /// Read a value that must be present. Throws with the field name rather than silently
        /// substituting a default — the right behaviour for physical quantities, where a
        /// wrong-but-plausible zero is far more damaging than a loud failure.
        /// </summary>
        public double RequireDouble(string key)
        {
            var value = this[key];
            if (value.Kind != JsonKind.Number)
                throw new FormatException($"Required numeric field '{key}' is missing or not a number.");
            return value._number;
        }

        public string RequireString(string key)
        {
            var value = this[key];
            if (value.Kind != JsonKind.String)
                throw new FormatException($"Required string field '{key}' is missing or not a string.");
            return value._string;
        }

        // ---- Parsing --------------------------------------------------------

        public static JsonValue Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            int index = 0;
            var result = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index < text.Length)
                throw new FormatException($"Unexpected trailing content at offset {index}: '{Peek(text, index)}'.");
            return result;
        }

        public static bool TryParse(string text, out JsonValue value)
        {
            try
            {
                value = Parse(text);
                return true;
            }
            catch (Exception)
            {
                value = Null();
                return false;
            }
        }

        private static JsonValue ParseValue(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length) throw new FormatException("Unexpected end of JSON input.");

            char c = text[index];
            switch (c)
            {
                case '{': return ParseObject(text, ref index);
                case '[': return ParseArray(text, ref index);
                case '"': return String(ParseString(text, ref index));
                case 't': Expect(text, ref index, "true"); return Bool(true);
                case 'f': Expect(text, ref index, "false"); return Bool(false);
                case 'n': Expect(text, ref index, "null"); return Null();
                default: return Number(ParseNumber(text, ref index));
            }
        }

        private static JsonValue ParseObject(string text, ref int index)
        {
            var result = Object();
            index++; // consume '{'
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == '}') { index++; return result; }

            while (true)
            {
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != '"')
                    throw new FormatException($"Expected an object key at offset {index}.");

                string key = ParseString(text, ref index);
                SkipWhitespace(text, ref index);

                if (index >= text.Length || text[index] != ':')
                    throw new FormatException($"Expected ':' after key '{key}' at offset {index}.");
                index++;

                result._object[key] = ParseValue(text, ref index);
                SkipWhitespace(text, ref index);

                if (index >= text.Length) throw new FormatException("Unterminated JSON object.");
                if (text[index] == ',') { index++; continue; }
                if (text[index] == '}') { index++; return result; }
                throw new FormatException($"Expected ',' or '}}' at offset {index}, found '{text[index]}'.");
            }
        }

        private static JsonValue ParseArray(string text, ref int index)
        {
            var result = Array();
            index++; // consume '['
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == ']') { index++; return result; }

            while (true)
            {
                result._array.Add(ParseValue(text, ref index));
                SkipWhitespace(text, ref index);

                if (index >= text.Length) throw new FormatException("Unterminated JSON array.");
                if (text[index] == ',') { index++; continue; }
                if (text[index] == ']') { index++; return result; }
                throw new FormatException($"Expected ',' or ']' at offset {index}, found '{text[index]}'.");
            }
        }

        private static string ParseString(string text, ref int index)
        {
            index++; // consume opening quote
            var builder = new StringBuilder();

            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '"') return builder.ToString();

                if (c != '\\') { builder.Append(c); continue; }

                if (index >= text.Length) break;
                char escape = text[index++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length) throw new FormatException("Truncated \\u escape.");
                        // Surrogate pairs arrive as two consecutive \u escapes; appending each
                        // code unit as-is reassembles them correctly in a UTF-16 string.
                        builder.Append((char)Convert.ToInt32(text.Substring(index, 4), 16));
                        index += 4;
                        break;
                    default:
                        throw new FormatException($"Unrecognised escape '\\{escape}' at offset {index - 1}.");
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private static double ParseNumber(string text, ref int index)
        {
            int start = index;
            if (index < text.Length && (text[index] == '-' || text[index] == '+')) index++;
            while (index < text.Length)
            {
                char c = text[index];
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') index++;
                else break;
            }

            string token = text.Substring(start, index - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new FormatException($"Malformed number '{token}' at offset {start}.");
            return value;
        }

        private static void Expect(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length ||
                string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
            {
                throw new FormatException($"Expected '{literal}' at offset {index}.");
            }
            index += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length)
            {
                char c = text[index];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') index++;
                else break;
            }
        }

        private static string Peek(string text, int index) =>
            text.Substring(index, System.Math.Min(20, text.Length - index));

        // ---- Writing --------------------------------------------------------

        public override string ToString() => ToString(indented: false);

        public string ToString(bool indented)
        {
            var builder = new StringBuilder();
            Write(builder, indented, 0);
            return builder.ToString();
        }

        private void Write(StringBuilder builder, bool indented, int depth)
        {
            switch (Kind)
            {
                case JsonKind.Null:
                    builder.Append("null");
                    break;

                case JsonKind.Bool:
                    builder.Append(_bool ? "true" : "false");
                    break;

                case JsonKind.Number:
                    if (double.IsNaN(_number) || double.IsInfinity(_number))
                    {
                        // JSON has no encoding for these. Emitting null is the conventional
                        // choice and keeps the document parseable by every other reader.
                        builder.Append("null");
                    }
                    else
                    {
                        // "R" round-trips exactly, which matters for orbital elements where
                        // truncating to a few digits shifts a body by kilometres.
                        builder.Append(_number.ToString("R", CultureInfo.InvariantCulture));
                    }
                    break;

                case JsonKind.String:
                    WriteEscaped(builder, _string);
                    break;

                case JsonKind.Array:
                    WriteArray(builder, indented, depth);
                    break;

                case JsonKind.Object:
                    WriteObject(builder, indented, depth);
                    break;
            }
        }

        private void WriteArray(StringBuilder builder, bool indented, int depth)
        {
            if (_array.Count == 0) { builder.Append("[]"); return; }

            builder.Append('[');
            for (int i = 0; i < _array.Count; i++)
            {
                if (i > 0) builder.Append(',');
                if (indented) { builder.Append('\n'); Indent(builder, depth + 1); }
                _array[i].Write(builder, indented, depth + 1);
            }
            if (indented) { builder.Append('\n'); Indent(builder, depth); }
            builder.Append(']');
        }

        private void WriteObject(StringBuilder builder, bool indented, int depth)
        {
            if (_object.Count == 0) { builder.Append("{}"); return; }

            builder.Append('{');
            bool first = true;
            foreach (var pair in _object)
            {
                if (!first) builder.Append(',');
                first = false;
                if (indented) { builder.Append('\n'); Indent(builder, depth + 1); }
                WriteEscaped(builder, pair.Key);
                builder.Append(':');
                if (indented) builder.Append(' ');
                pair.Value.Write(builder, indented, depth + 1);
            }
            if (indented) { builder.Append('\n'); Indent(builder, depth); }
            builder.Append('}');
        }

        private static void Indent(StringBuilder builder, int depth) => builder.Append(' ', depth * 2);

        private static void WriteEscaped(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ') builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
