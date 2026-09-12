using System;
using System.IO;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// Where the shipped data files live, for the tests that read the real artefacts rather
    /// than a constructed example. Same search as <see cref="ShippedCatalogTests"/>: walk up
    /// from the test binary until the StreamingAssets tree appears, so the suite runs from a
    /// worktree, from bin/, or from anywhere else the build puts it.
    /// </summary>
    public static class Shipped
    {
        public static string Path(params string[] parts)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 10 && directory != null; depth++)
            {
                string candidate = System.IO.Path.Combine(directory.FullName,
                    "unity", "Assets", "StreamingAssets", "CosmoSynth");
                candidate = System.IO.Path.Combine(candidate, System.IO.Path.Combine(parts));
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new FileNotFoundException(
                "StreamingAssets/CosmoSynth/" + string.Join("/", parts) + " not found. " +
                "Build it with the importer in tools/cosmodata/.");
        }

        public static byte[] Bytes(params string[] parts) => File.ReadAllBytes(Path(parts));

        public static string Text(params string[] parts) => File.ReadAllText(Path(parts));

        public static string Fixture(string name)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (int depth = 0; depth < 10 && directory != null; depth++)
            {
                string candidate = System.IO.Path.Combine(directory.FullName, "fixtures", name);
                if (File.Exists(candidate)) return candidate;
                candidate = System.IO.Path.Combine(directory.FullName, "tests",
                    "CosmoSynth.Core.Tests", "fixtures", name);
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new FileNotFoundException("fixture " + name + " not found");
        }
    }

    /// <summary>
    /// The column-store container's two quantised-column members, and the refusals.
    ///
    /// <b>The container itself belongs to lane data-galaxies</b>, which wrote
    /// Core/Data/CsData.cs and tools/cosmodata/csdata.py first; this lane adopted that file
    /// rather than ship a second implementation of the same wire format, and added
    /// <c>ColumnMeta</c> and <c>Decoder</c> to it, which a quantised voxel grid needs and a
    /// galaxy catalogue does not. These tests cover those two members and the refusal paths
    /// the dust cube depends on. If data-galaxies ships its own CsDataTests, the round-trip
    /// half of this file is redundant and should be deleted rather than kept in duplicate.
    ///
    /// It reads fixtures/csdata_fixture.csd, which `csdata.py --selftest` writes: one column of
    /// every type, a linear encoding, a log8 encoding and a per-row provenance override. That
    /// file is the cross-language contract, and a layout change on either side that the other
    /// does not follow fails here rather than on a headset.
    /// </summary>
    public static class CsDataTests
    {
        private static byte[] Fixture() => File.ReadAllBytes(Shipped.Fixture("csdata_fixture.csd"));

        [Test("the container the Python writer produces reads back value for value")]
        public static void FixtureRoundTrips()
        {
            CsData data = CsData.Parse(Fixture(), "CSDF");
            Assert.True(data.Rows == 3, $"expected 3 rows, got {data.Rows}");
            Assert.True(data.Version == 1, $"expected version 1, got {data.Version}");

            float[] pos = data.F32("f32c3");
            Assert.True(data.ComponentsOf("f32c3") == 3, "f32c3 should have three components");
            Assert.True(pos.Length == 9, $"expected 9 floats, got {pos.Length}");
            for (int i = 0; i < 9; i++) Assert.Equal(i + 1, pos[i], 0, $"f32c3[{i}]");

            double[] wide = data.F64("f64");
            Assert.Equal(-1.5, wide[0], 0, "f64[0]");
            Assert.Equal(1.5, wide[2], 0, "f64[2]");

            Assert.True(data.I8("i8")[0] == -128 && data.I8("i8")[2] == 127,
                "i8 extremes did not survive the round trip");
            Assert.True(data.I16("i16")[0] == short.MinValue, "i16 minimum did not survive");
            Assert.True(data.U16("u16")[2] == 65535, "u16 maximum did not survive");
            Assert.True(data.U32("u32")[2] == 4294967295u,
                "a u32 above int.MaxValue did not survive");
            Assert.True(data.I32("i32")[0] == int.MinValue, "i32 minimum did not survive");
            Assert.True(data.U8("u8")[1] == 128, "u8 column is wrong");
        }

        [Test("a column's declared encoding decodes exactly as the Python writer defines it")]
        public static void EncodingsDecodeAsDefined()
        {
            CsData data = CsData.Parse(Fixture(), "CSDF");

            // linear: value = raw * scale + offset, here 0.5 and -1.0.
            Func<double, double> linear = data.Decoder("u8");
            Assert.Equal(-1.0, linear(0), 1e-12, "linear(0)");
            Assert.Equal(0.0, linear(2), 1e-12, "linear(2)");
            Assert.Equal(126.5, linear(255), 1e-12, "linear(255)");

            // log8: 0 -> 0; 1..255 walk a 254-step log ramp from lo to hi, here 0.01 to 100.
            Func<double, double> log8 = data.Decoder("u16");
            Assert.Equal(0.0, log8(0), 0.0,
                "log8(0) is exactly zero, not the floor. Decoding it as the floor would fill " +
                "every unmapped voxel of the dust cube with faint dust.");
            Assert.Relative(0.01, log8(1), 1e-12, "log8(1) is the floor");
            Assert.Relative(100.0, log8(255), 1e-12, "log8(255) is the ceiling");
            Assert.Relative(1.0, log8(128), 1e-12,
                "the ramp is logarithmic, so the midpoint of 0.01 and 100 is 1");

            // A column with no encoding decodes as itself.
            Assert.Equal(42.0, data.Decoder("f64")(42.0), 0.0, "no encoding is the identity");

            // The column's own meta is what carries all of this.
            Assert.True(data.ColumnMeta("f32c3")["unit"].AsString(null) == "pc",
                "the unit lives on the column, not on the file");
        }

        [Test("an encoding this build does not know is refused, never read as raw values")]
        public static void UnknownEncodingIsRefused()
        {
            // Build a file that declares an encoding from the future. Reading its raw codes as
            // if they were physical values would be silently wrong by orders of magnitude.
            byte[] good = Fixture();
            string text = System.Text.Encoding.UTF8.GetString(good);
            int at = text.IndexOf("\"kind\":\"log8\"", StringComparison.Ordinal);
            Assert.True(at > 0, "the fixture no longer declares a log8 encoding");
            byte[] mangled = (byte[])good.Clone();
            System.Text.Encoding.ASCII.GetBytes("\"kind\":\"lg99\"").CopyTo(mangled, at);

            CsData data = CsData.Parse(mangled, "CSDF");
            try
            {
                data.Decoder("u16");
                throw new AssertionException("an unknown encoding was accepted");
            }
            catch (FormatException e)
            {
                Assert.True(e.Message.Contains("lg99"),
                    "the refusal should name the encoding: " + e.Message);
            }
        }

        [Test("per-row provenance bits override the file-wide class")]
        public static void ProvenanceBitsOverride()
        {
            CsData data = CsData.Parse(Fixture(), "CSDF");
            FieldProvenance plain = data.ProvenanceFor("f32c3", 0);
            Assert.True(plain.Class == ProvenanceClass.Procedural,
                $"the file-wide class should be Procedural, got {plain.Class}");
            FieldProvenance flagged = data.ProvenanceFor("f32c3", 1);
            Assert.True(flagged.Class == ProvenanceClass.Inferred,
                $"bit 0 should weaken the claim to Inferred, got {flagged.Class}");
        }

        private static void ExpectRefusal(byte[] data, string magic, string what)
        {
            try
            {
                CsData.Parse(data, magic);
            }
            catch (FormatException)
            {
                return;
            }
            throw new AssertionException(what + ": the parser accepted it");
        }

        [Test("a file under the wrong name, truncated, misaligned or from the future is refused")]
        public static void RefusesBadFiles()
        {
            byte[] good = Fixture();

            ExpectRefusal(good, "CSDU", "a fixture read as a dust cube");

            byte[] shortFile = new byte[16];
            Array.Copy(good, shortFile, 16);
            ExpectRefusal(shortFile, "CSDF", "a 16-byte file");

            byte[] future = (byte[])good.Clone();
            BitConverter.GetBytes(99u).CopyTo(future, 4);
            ExpectRefusal(future, "CSDF", "layout version 99");

            byte[] truncated = new byte[good.Length - 8];
            Array.Copy(good, truncated, truncated.Length);
            ExpectRefusal(truncated, "CSDF", "a file eight bytes short of its header");

            byte[] metaOdd = (byte[])good.Clone();
            BitConverter.GetBytes(BitConverter.ToUInt32(good, 16) + 1).CopyTo(metaOdd, 16);
            ExpectRefusal(metaOdd, "CSDF", "metaBytes not a multiple of 8");

            byte[] reserved = (byte[])good.Clone();
            BitConverter.GetBytes(1u).CopyTo(reserved, 12);
            ExpectRefusal(reserved, "CSDF", "a non-zero reserved flags word");

            byte[] noisy = (byte[])good.Clone();
            for (int i = 32; i < 64; i++) noisy[i] = (byte)'!';
            ExpectRefusal(noisy, "CSDF", "a corrupted meta block");

            byte[] random = new byte[512];
            new Random(1).NextBytes(random);
            ExpectRefusal(random, "CSDF", "512 random bytes");

            // CSS1 is the star catalogue's old magic, and the new parser must refuse it by name.
            byte[] css1 = (byte[])good.Clone();
            System.Text.Encoding.ASCII.GetBytes("CSS1").CopyTo(css1, 0);
            ExpectRefusal(css1, "CSDF", "a CSS1 header");
        }

        [Test("asking for a column that is not there, or at the wrong type, says so")]
        public static void RefusesBadColumns()
        {
            CsData data = CsData.Parse(Fixture(), "CSDF");
            try
            {
                data.F32("nosuchcolumn");
                throw new AssertionException("a missing column was accepted");
            }
            catch (FormatException e)
            {
                Assert.True(e.Message.Contains("nosuchcolumn"),
                    "the message should name the column: " + e.Message);
            }
            try
            {
                data.F32("f64");
                throw new AssertionException("an f64 column was read as f32");
            }
            catch (FormatException e)
            {
                Assert.True(e.Message.Contains("f64"),
                    "the message should name the real type: " + e.Message);
            }
        }
    }
}
