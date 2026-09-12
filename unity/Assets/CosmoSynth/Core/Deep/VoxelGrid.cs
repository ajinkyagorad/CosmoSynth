using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// A regular 3D scalar grid read out of a <see cref="CsData"/> container: the dust cube now,
    /// the Cosmicflows-4 basin and density grids later. Shared because the sampling arithmetic
    /// is the part that is easy to get subtly wrong, and there is no reason to write it twice.
    ///
    /// Grids are the one thing the containers store in their NATIVE frame rather than in ICRF,
    /// because resampling a reconstruction into another frame blurs it. The frame is declared in
    /// the meta and the caller rotates, not this class.
    ///
    /// <b>Sampling is in the grid's own units</b> (parsecs for the dust cube), with the origin at
    /// node [0,0,0]. `nodes` sampling means the value lives AT <c>origin + i*step</c>; `cells`
    /// means it covers the interval starting there. Only `nodes` is implemented, and a `cells`
    /// grid is refused rather than silently read half a voxel out of place.
    /// </summary>
    public sealed class VoxelGrid
    {
        private readonly byte[] _raw;
        private readonly Func<double, double> _decode;
        private readonly double[] _lut;          // decoded value for each of the 256 byte codes

        public int Nx { get; private set; }
        public int Ny { get; private set; }
        public int Nz { get; private set; }
        public Double3 Origin { get; private set; }
        public Double3 Step { get; private set; }
        public string Frame { get; private set; }
        public string Unit { get; private set; }

        private VoxelGrid(byte[] raw, Func<double, double> decode)
        {
            _raw = raw;
            _decode = decode;
            _lut = new double[256];
            for (int i = 0; i < 256; i++) _lut[i] = decode(i);
        }

        /// <summary>Read the named u8 column of <paramref name="data"/> as a grid.</summary>
        public static VoxelGrid Parse(CsData data, string column)
        {
            if (!data.Meta.ContainsKey("grid"))
                throw new FormatException($"{data.Magic}: meta has no 'grid' block.");
            JsonValue grid = data.Meta["grid"];

            string sampling = grid["sampling"].AsString("nodes");
            if (sampling != "nodes")
            {
                throw new FormatException(
                    $"{data.Magic}: grid sampling is '{sampling}'. Only 'nodes' is implemented, " +
                    "and reading a 'cells' grid as nodes puts every value half a voxel out.");
            }

            JsonValue dims = grid["dims"];
            if (!dims.IsArray || dims.Count != 3)
                throw new FormatException($"{data.Magic}: grid.dims must be three integers.");
            int nx = dims[0].AsInt(0), ny = dims[1].AsInt(0), nz = dims[2].AsInt(0);
            if (nx < 2 || ny < 2 || nz < 2)
                throw new FormatException($"{data.Magic}: grid.dims {nx}x{ny}x{nz} is degenerate.");

            long expected = (long)nx * ny * nz;
            if (data.RowsOf(column) != expected)
            {
                throw new FormatException(
                    $"{data.Magic}: column '{column}' has {data.RowsOf(column)} values and the " +
                    $"grid is {nx}x{ny}x{nz} = {expected}.");
            }

            var result = new VoxelGrid(data.U8(column), data.Decoder(column))
            {
                Nx = nx, Ny = ny, Nz = nz,
                Origin = ReadVector(grid, "origin", data.Magic),
                Step = ReadVector(grid, "step", data.Magic),
                Frame = grid["frame"].AsString("unknown"),
                Unit = grid["unit"].AsString("unknown"),
            };
            if (!(result.Step.x > 0.0 && result.Step.y > 0.0 && result.Step.z > 0.0))
                throw new FormatException($"{data.Magic}: grid.step must be positive.");
            return result;
        }

        private static Double3 ReadVector(JsonValue grid, string key, string magic)
        {
            JsonValue value = grid[key];
            if (!value.IsArray || value.Count != 3)
                throw new FormatException($"{magic}: grid.{key} must be three numbers.");
            return new Double3(value[0].AsDouble(), value[1].AsDouble(), value[2].AsDouble());
        }

        public long VoxelCount => (long)Nx * Ny * Nz;

        /// <summary>Lowest and highest coordinate covered by a node, in the grid's own frame.</summary>
        public Double3 Min => Origin;

        public Double3 Max => new Double3(
            Origin.x + Step.x * (Nx - 1),
            Origin.y + Step.y * (Ny - 1),
            Origin.z + Step.z * (Nz - 1));

        public bool Contains(Double3 p) =>
            p.x >= Min.x && p.x <= Max.x && p.y >= Min.y && p.y <= Max.y &&
            p.z >= Min.z && p.z <= Max.z;

        /// <summary>The decoded value of one node. x is fastest in memory.</summary>
        public double Node(int ix, int iy, int iz)
        {
            if (ix < 0 || ix >= Nx || iy < 0 || iy >= Ny || iz < 0 || iz >= Nz) return 0.0;
            return _lut[_raw[((long)iz * Ny + iy) * Nx + ix]];
        }

        public byte RawNode(int ix, int iy, int iz)
        {
            if (ix < 0 || ix >= Nx || iy < 0 || iy >= Ny || iz < 0 || iz >= Nz) return 0;
            return _raw[((long)iz * Ny + iy) * Nx + ix];
        }

        /// <summary>Decode one raw code, for callers that walk the bytes themselves.</summary>
        public double Decode(double raw) => _decode(raw);

        public double SampleNearest(Double3 p)
        {
            if (!Contains(p)) return 0.0;
            int ix = (int)Math.Round((p.x - Origin.x) / Step.x);
            int iy = (int)Math.Round((p.y - Origin.y) / Step.y);
            int iz = (int)Math.Round((p.z - Origin.z) / Step.z);
            return Node(ix, iy, iz);
        }

        /// <summary>
        /// Trilinear sample. The corners are DECODED first and then interpolated: interpolating
        /// the log-encoded bytes and decoding afterwards would be a geometric mean, which is a
        /// different and wrong answer everywhere the field has contrast — which, in a dust map,
        /// is exactly where it matters.
        /// </summary>
        public double SampleTrilinear(Double3 p)
        {
            if (!Contains(p)) return 0.0;
            double fx = (p.x - Origin.x) / Step.x;
            double fy = (p.y - Origin.y) / Step.y;
            double fz = (p.z - Origin.z) / Step.z;
            int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy), iz = (int)Math.Floor(fz);
            if (ix >= Nx - 1) ix = Nx - 2;
            if (iy >= Ny - 1) iy = Ny - 2;
            if (iz >= Nz - 1) iz = Nz - 2;
            if (ix < 0) ix = 0;
            if (iy < 0) iy = 0;
            if (iz < 0) iz = 0;
            double tx = fx - ix, ty = fy - iy, tz = fz - iz;

            double c000 = Node(ix, iy, iz), c100 = Node(ix + 1, iy, iz);
            double c010 = Node(ix, iy + 1, iz), c110 = Node(ix + 1, iy + 1, iz);
            double c001 = Node(ix, iy, iz + 1), c101 = Node(ix + 1, iy, iz + 1);
            double c011 = Node(ix, iy + 1, iz + 1), c111 = Node(ix + 1, iy + 1, iz + 1);

            double c00 = c000 + (c100 - c000) * tx;
            double c10 = c010 + (c110 - c010) * tx;
            double c01 = c001 + (c101 - c001) * tx;
            double c11 = c011 + (c111 - c011) * tx;
            double c0 = c00 + (c10 - c00) * ty;
            double c1 = c01 + (c11 - c01) * ty;
            return c0 + (c1 - c0) * tz;
        }
    }
}
