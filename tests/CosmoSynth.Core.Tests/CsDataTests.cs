using System;
using System.IO;
using System.Text;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// The container, against the fixture Python wrote.
    ///
    /// Two implementations of one byte layout drift apart silently: a column offset moves, the
    /// file still parses, and the universe is subtly wrong with no error anywhere. The fixture
    /// tests/CosmoSynth.Core.Tests/fixtures/csdata_fixture.csd is written by
    /// `python3 tools/cosmodata/csdata.py --selftest` and committed, so this suite is the
    /// cross-language contract rather than C# agreeing with itself.
    /// </summary>
    public static class CsDataTests
    {
        private static byte[] _fixture;

        private static byte[] Fixture
        {
            get
            {
                if (_fixture == null)
                {
                    string path = TestFiles.Find(Path.Combine(
                        "tests", "CosmoSynth.Core.Tests", "fixtures", "csdata_fixture.csd"));
                    if (path == null)
                        throw new FileNotFoundException(
                            "csdata_fixture.csd not found. Write it with " +
                            "'python3 tools/cosmodata/csdata.py --selftest'.");
                    _fixture = File.ReadAllBytes(path);
                }
                return _fixture;
            }
        }

        [Test("the container fixture parses with every column Python wrote")]
        public static void FixtureRoundTrips()
        {
            CsData data = CsData.Parse(Fixture, "CSDF");

            Assert.True(data.Rows == 3, $"expected 3 rows, got {data.Rows}");
            Assert.True(data.Version == 1, $"expected version 1, got {data.Version}");
            Assert.True(data.Magic == "CSDF", $"magic is {data.Magic}");

            float[] three = data.F32("f32c3");
            Assert.True(three.Length == 9, $"f32c3 should hold 3 rows x 3 components, got {three.Length}");
            Assert.Equal(1.0, three[0], 0.0, "f32c3[0]");
            Assert.Equal(9.0, three[8], 0.0, "f32c3[8]");
            Assert.True(data.ComponentsOf("f32c3") == 3, "f32c3 should declare 3 components");

            double[] doubles = data.F64("f64");
            Assert.Equal(-1.5, doubles[0], 0.0, "f64[0]");
            Assert.Equal(1.5, doubles[2], 0.0, "f64[2]");

            byte[] bytes = data.U8("u8");
            Assert.True(bytes[0] == 0 && bytes[1] == 128 && bytes[2] == 255, "u8 column");

            sbyte[] signed = data.I8("i8");
            Assert.True(signed[0] == -128 && signed[2] == 127,
                $"i8 column should span the type: got {signed[0]}..{signed[2]}");

            ushort[] words = data.U16("u16");
            Assert.True(words[2] == 65535, $"u16 should reach 65535, got {words[2]}");

            short[] signedWords = data.I16("i16");
            Assert.True(signedWords[0] == -32768 && signedWords[2] == 32767, "i16 column");

            uint[] longs = data.U32("u32");
            Assert.True(longs[2] == 4294967295u, $"u32 should reach 2^32-1, got {longs[2]}");

            int[] signedLongs = data.I32("i32");
            Assert.True(signedLongs[0] == int.MinValue && signedLongs[2] == int.MaxValue,
                "i32 column should span the type");
        }

        [Test("quantised columns decode by the encoding the file declares")]
        public static void EncodingsDecode()
        {
            CsData data = CsData.Parse(Fixture, "CSDF");

            // linear: value = raw * 0.5 - 1.0
            Assert.Equal(-1.0, data.Decode("u8", 0), 1e-12, "linear decode of 0");
            Assert.Equal(63.0, data.Decode("u8", 128), 1e-12, "linear decode of 128");

            // log8: 0 -> 0, and v >= 1 maps lo..hi geometrically over 1..255
            Assert.Equal(0.0, data.Decode("u16", 0), 1e-12, "log8 decode of 0");
            Assert.Equal(0.01, data.Decode("u16", 1), 1e-12, "log8 decode of 1 is lo");
            Assert.Equal(100.0, data.Decode("u16", 255), 1e-9, "log8 decode of 255 is hi");

            // a column with no encoding decodes to itself
            Assert.Equal(7.0, data.Decode("f64", 7.0), 0.0, "unencoded decode is the identity");
        }

        [Test("per-row provenance comes from the flag bits, not from a string per row")]
        public static void ProvenanceResolvesThroughFlags()
        {
            CsData data = CsData.Parse(Fixture, "CSDF");

            FieldProvenance clean = data.ProvenanceFor("f32c3", 0u);
            Assert.True(clean.Class == ProvenanceClass.Procedural,
                $"with no flags the field default applies, got {clean.Class}");

            FieldProvenance flagged = data.ProvenanceFor("f32c3", 1u);
            Assert.True(flagged.Class == ProvenanceClass.Inferred,
                $"bit 0 should weaken the claim to Inferred, got {flagged.Class}");

            FieldProvenance unknown = data.ProvenanceFor("no-such-field", 0u);
            Assert.True(unknown.Class == ProvenanceClass.Unknown,
                "a field the file says nothing about is Unknown, not Measured");
        }

        [Test("a malformed container is refused rather than reinterpreted")]
        public static void MalformedFilesAreRefused()
        {
            byte[] good = Fixture;

            Assert.Throws<FormatException>(
                () => CsData.Parse(good, "CSS2"),
                "a file loaded under the wrong magic must be refused");

            Assert.Throws<FormatException>(
                () => CsData.Parse(new byte[8], "CSDF"),
                "a file shorter than the header must be refused");

            byte[] truncated = new byte[good.Length - 16];
            Buffer.BlockCopy(good, 0, truncated, 0, truncated.Length);
            Assert.Throws<FormatException>(
                () => CsData.Parse(truncated, "CSDF"),
                "a truncated file must be refused");

            byte[] newer = (byte[])good.Clone();
            newer[4] = 9;
            Assert.Throws<FormatException>(
                () => CsData.Parse(newer, "CSDF"),
                "a version this build does not know must be refused");

            byte[] flagged = (byte[])good.Clone();
            flagged[12] = 1;
            Assert.Throws<FormatException>(
                () => CsData.Parse(flagged, "CSDF"),
                "a non-zero reserved flags field must be refused");

            byte[] misaligned = (byte[])good.Clone();
            misaligned[16] = (byte)(misaligned[16] + 1);
            Assert.Throws<FormatException>(
                () => CsData.Parse(misaligned, "CSDF"),
                "metaBytes that is not a multiple of 8 must be refused");

            byte[] brokenJson = (byte[])good.Clone();
            brokenJson[CsData.HeaderBytes] = (byte)'x';
            Assert.Throws<FormatException>(
                () => CsData.Parse(brokenJson, "CSDF"),
                "an unparseable meta block must be refused");

            CsData parsed = CsData.Parse(good, "CSDF");
            Assert.Throws<FormatException>(
                () => parsed.F32("f64"),
                "reading a column as the wrong type must be refused");
            Assert.Throws<FormatException>(
                () => parsed.F32("not-a-column"),
                "reading a column that does not exist must be refused");
        }

        [Test("a column running past the payload is refused, even though the file length fits")]
        public static void OverrunningColumnIsRefused()
        {
            // The nastiest malformation: the header arithmetic is self-consistent and only the
            // column table lies. Nothing about the file length catches this.
            byte[] good = Fixture;
            int metaBytes = BitConverter.ToInt32(good, 16);
            string meta = Encoding.UTF8.GetString(good, CsData.HeaderBytes, metaBytes);
            int payloadBytes = (int)BitConverter.ToUInt64(good, 24);

            string broken = meta.Replace("\"name\":\"f64\",\"type\":\"f64\",\"offset\":",
                "\"name\":\"f64\",\"type\":\"f64\",\"offset\":" + (payloadBytes + 8) + ",\"dead\":");
            Assert.True(broken != meta, "the fixture meta did not contain the expected column entry");

            byte[] padded = Encoding.UTF8.GetBytes(broken);
            int pad = (8 - (padded.Length % 8)) % 8;
            var bytes = new byte[CsData.HeaderBytes + padded.Length + pad + payloadBytes];
            Buffer.BlockCopy(good, 0, bytes, 0, CsData.HeaderBytes);
            Buffer.BlockCopy(padded, 0, bytes, CsData.HeaderBytes, padded.Length);
            for (int i = 0; i < pad; i++) bytes[CsData.HeaderBytes + padded.Length + i] = (byte)' ';
            Buffer.BlockCopy(good, CsData.HeaderBytes + metaBytes,
                bytes, CsData.HeaderBytes + padded.Length + pad, payloadBytes);
            byte[] length = BitConverter.GetBytes((uint)(padded.Length + pad));
            Buffer.BlockCopy(length, 0, bytes, 16, 4);

            Assert.Throws<FormatException>(
                () => CsData.Parse(bytes, "CSDF"),
                "a column whose offset runs past the payload must be refused");
        }
    }

    /// <summary>Finds a file by walking up from the test binary to the repository root.</summary>
    public static class TestFiles
    {
        public static string Find(string relative)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 10 && directory != null; depth++)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            return null;
        }
    }
}
