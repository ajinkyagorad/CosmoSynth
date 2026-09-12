using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// STUB (integrator, CONTRACT C.7) — a regular 3D scalar grid with a log-encoded byte payload,
    /// shared by the dust cube and the two CF4 grids.
    ///
    /// <b>What is missing on purpose:</b> <c>Parse</c>. The <c>milkyway</c> data lane owns the
    /// <c>CSDU</c> reader. Until it lands, no grid is delivered and the layers that need one
    /// declare no group and draw nothing.
    ///
    /// The decode is the interesting half and is kept here because the renderer has to agree with
    /// it exactly: the byte is a log index, so the CPU replica used for picking and the GPU's own
    /// <c>_Decode</c> uniform must come from the same two numbers.
    /// </summary>
    public sealed class VoxelGrid
    {
        private readonly byte[] _raw;

        public VoxelGrid(int nx, int ny, int nz, Double3 minUnits, Double3 stepUnits,
            double vmin, double lnRange, byte[] raw)
        {
            Nx = nx; Ny = ny; Nz = nz;
            MinUnits = minUnits;
            StepUnits = stepUnits;
            ValueMin = vmin;
            LogRange = lnRange;
            _raw = raw;
        }

        public int Nx { get; }
        public int Ny { get; }
        public int Nz { get; }

        /// <summary>Position of node (0,0,0) in the grid's own frame and unit.</summary>
        public Double3 MinUnits { get; }

        /// <summary>Node spacing in the grid's own unit.</summary>
        public Double3 StepUnits { get; }

        /// <summary>Value of raw byte 0 (not 0: the encoding is logarithmic).</summary>
        public double ValueMin { get; }

        /// <summary><c>ln(vmax / vmin)</c>. Raw 255 decodes to <c>vmin * e^LogRange</c>.</summary>
        public double LogRange { get; }

        public Double3 MaxUnits => MinUnits + new Double3(
            StepUnits.x * (Nx - 1), StepUnits.y * (Ny - 1), StepUnits.z * (Nz - 1));

        public Double3 CentreUnits => (MinUnits + MaxUnits) * 0.5;

        public Double3 HalfExtentUnits => (MaxUnits - MinUnits) * 0.5;

        public byte[] Raw => _raw;

        /// <summary>Byte to physical value. Raw 0 is exactly zero, so empty space stays empty.</summary>
        public double Decode(byte raw) =>
            raw == 0 ? 0.0 : ValueMin * Math.Exp(LogRange * (raw - 1) / 254.0);

        public bool Contains(Double3 p) =>
            p.x >= MinUnits.x && p.x <= MaxUnits.x &&
            p.y >= MinUnits.y && p.y <= MaxUnits.y &&
            p.z >= MinUnits.z && p.z <= MaxUnits.z;

        public double SampleNearest(Double3 p)
        {
            if (_raw == null || !Contains(p)) return 0.0;
            int i = (int)Math.Round((p.x - MinUnits.x) / StepUnits.x);
            int j = (int)Math.Round((p.y - MinUnits.y) / StepUnits.y);
            int k = (int)Math.Round((p.z - MinUnits.z) / StepUnits.z);
            return Decode(At(i, j, k));
        }

        /// <summary>
        /// Trilinear in LINEAR density, not in the encoded byte: interpolating log indices and
        /// then decoding gives a geometric mean, which systematically under-reads the edge of
        /// every cloud.
        /// </summary>
        public double SampleTrilinear(Double3 p)
        {
            if (_raw == null || !Contains(p)) return 0.0;
            double fx = (p.x - MinUnits.x) / StepUnits.x;
            double fy = (p.y - MinUnits.y) / StepUnits.y;
            double fz = (p.z - MinUnits.z) / StepUnits.z;
            int i = (int)Math.Floor(fx), j = (int)Math.Floor(fy), k = (int)Math.Floor(fz);
            double tx = fx - i, ty = fy - j, tz = fz - k;

            double c00 = Lerp(Decode(At(i, j, k)), Decode(At(i + 1, j, k)), tx);
            double c10 = Lerp(Decode(At(i, j + 1, k)), Decode(At(i + 1, j + 1, k)), tx);
            double c01 = Lerp(Decode(At(i, j, k + 1)), Decode(At(i + 1, j, k + 1)), tx);
            double c11 = Lerp(Decode(At(i, j + 1, k + 1)), Decode(At(i + 1, j + 1, k + 1)), tx);
            return Lerp(Lerp(c00, c10, ty), Lerp(c01, c11, ty), tz);
        }

        private byte At(int i, int j, int k)
        {
            if (i < 0 || j < 0 || k < 0 || i >= Nx || j >= Ny || k >= Nz) return 0;
            return _raw[(k * Ny + j) * Nx + i];   // x fastest, as the file stores it
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
