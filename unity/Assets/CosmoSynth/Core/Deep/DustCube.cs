using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// The Milky Way's dust, as a density field that can be sampled and integrated along a ray.
    ///
    /// Reads <c>galaxy/dust.csdu</c>: Edenhofer et al. 2024's 1.25 kpc reconstruction, resampled
    /// to 256^3 voxels of 10 pc in the heliocentric galactic frame and log-quantised to 8 bits.
    ///
    /// <b>Three things here are easy to get wrong, and each has cost someone a day somewhere.</b>
    ///
    /// 1. <b>The values are not magnitudes.</b> The source's own unit card reads, verbatim,
    ///    "E of Zhang, Green, and Rix (2023)" per parsec. E is that catalogue's unitless
    ///    extinction parameter. <see cref="ExtinctionAlong"/> therefore returns E, not A_V, and
    ///    is named for what it is. Converting needs the Zhang+2023 curve; a single scale factor
    ///    is fine for rendering and must not be labelled "magnitudes".
    /// 2. <b>Outside the mapped sphere the cube is UNKNOWN, not empty.</b> The reconstruction is
    ///    a ball of radius 1248 pc inscribed in a 2550 pc box. A ray march that reads the
    ///    encoded zero out there as vacuum cuts square corners of clear sky into the Galaxy.
    ///    <see cref="DensityAt"/> returns 0 beyond <see cref="MappedRadiusPc"/> and
    ///    <see cref="IsMapped"/> says whether a point was measured at all.
    /// 3. <b>Inside 68.6 pc there are no shells.</b> Those voxels carry the innermost shell's
    ///    value. The Sun sits inside the Local Bubble, so this is both the most visible part of
    ///    the cube and the only part that is an extrapolation.
    /// </summary>
    public sealed class DustCube
    {
        private readonly VoxelGrid _grid;

        /// <summary>The raw grid, for a renderer that wants to upload the bytes.</summary>
        public VoxelGrid Grid => _grid;

        public CsData Data { get; private set; }

        /// <summary>Inner and outer radius of the reconstruction, parsecs from the Sun.</summary>
        public double InnerRadiusPc { get; private set; }

        public double MappedRadiusPc { get; private set; }

        /// <summary>The unit string the source declares, quoted rather than interpreted.</summary>
        public string Unit => _grid.Unit;

        private DustCube(CsData data, VoxelGrid grid)
        {
            Data = data;
            _grid = grid;
        }

        public static DustCube Parse(byte[] file)
        {
            CsData data = CsData.Parse(file, "CSDU");
            if (data.Meta["kind"].AsString(null) != "dust")
                throw new FormatException("CSDU: meta.kind is not 'dust'.");
            VoxelGrid grid = VoxelGrid.Parse(data, "rho");
            if (grid.Frame != "galactic-helio")
            {
                throw new FormatException(
                    $"CSDU: grid frame is '{grid.Frame}'. DustCube samples in heliocentric " +
                    "galactic parsecs and will not silently work in another frame.");
            }

            var cube = new DustCube(data, grid);
            JsonValue radii = data.Meta["grid"]["mapRadiusPc"];
            if (radii.IsArray && radii.Count == 2)
            {
                cube.InnerRadiusPc = radii[0].AsDouble(0.0);
                cube.MappedRadiusPc = radii[1].AsDouble(double.PositiveInfinity);
            }
            else
            {
                // No declared sphere: the whole box is mapped. Never assume a radius.
                cube.InnerRadiusPc = 0.0;
                cube.MappedRadiusPc = double.PositiveInfinity;
            }
            return cube;
        }

        /// <summary>Was this point inside the reconstruction at all?</summary>
        public bool IsMapped(Double3 galacticPc) =>
            galacticPc.Magnitude <= MappedRadiusPc && _grid.Contains(galacticPc);

        /// <summary>
        /// Dust density at a heliocentric galactic position, in the source's E-per-parsec.
        /// Zero outside the box and outside the mapped sphere — which means "not measured",
        /// not "no dust".
        /// </summary>
        public double DensityAt(Double3 galacticPc)
        {
            if (!IsMapped(galacticPc)) return 0.0;
            return _grid.SampleTrilinear(galacticPc);
        }

        /// <summary>
        /// Integrated extinction E between two heliocentric galactic points, in parsecs.
        ///
        /// Trapezoidal, at <paramref name="stepPc"/>, which defaults to one voxel. Symmetric in
        /// its endpoints by construction: the sample positions of a -> b are the reverse of the
        /// sample positions of b -> a, and the trapezoid weights are symmetric too. The test
        /// suite asserts that, because an asymmetric integrator makes a body's own extinction
        /// depend on which way the ray was traced.
        /// </summary>
        public double ExtinctionAlong(Double3 aPc, Double3 bPc, double stepPc = 10.0)
        {
            if (stepPc <= 0.0) throw new ArgumentOutOfRangeException(nameof(stepPc));
            Double3 delta = bPc - aPc;
            double length = delta.Magnitude;
            if (length <= 0.0) return 0.0;

            int steps = (int)Math.Ceiling(length / stepPc);
            if (steps < 1) steps = 1;
            double ds = length / steps;
            Double3 direction = delta / length;

            double total = 0.0;
            double previous = DensityAt(aPc);
            for (int i = 1; i <= steps; i++)
            {
                double current = DensityAt(aPc + direction * (ds * i));
                total += 0.5 * (previous + current) * ds;
                previous = current;
            }
            return total;
        }

        /// <summary>Extinction from the Sun out to a point, the common case.</summary>
        public double ExtinctionToward(Double3 galacticPc, double stepPc = 10.0) =>
            ExtinctionAlong(Double3.Zero, galacticPc, stepPc);

        /// <summary>
        /// Heliocentric ICRF parsecs to the cube's heliocentric galactic parsecs.
        ///
        /// This goes through <see cref="StandardFrames.EquatorialToGalactic"/> deliberately:
        /// there is one galactic rotation in this codebase and a private copy here would be a
        /// second one to keep in step. Every shipped position column is already ICRF, so this is
        /// the only rotation the Milky Way layer needs.
        /// </summary>
        public static Double3 GalacticPcFromIcrfPc(Double3 icrfPc) =>
            StandardFrames.EquatorialToGalactic * icrfPc;

        public static Double3 IcrfPcFromGalacticPc(Double3 galacticPc) =>
            StandardFrames.GalacticToEquatorial * galacticPc;

        /// <summary>Density at an ICRF position, for callers holding catalogue coordinates.</summary>
        public double DensityAtIcrf(Double3 icrfPc) => DensityAt(GalacticPcFromIcrfPc(icrfPc));

        /// <summary>Highest density anywhere in the cube, decoded. Walks the bytes once.</summary>
        public double PeakDensity()
        {
            byte peak = 0;
            for (int iz = 0; iz < _grid.Nz; iz++)
                for (int iy = 0; iy < _grid.Ny; iy++)
                    for (int ix = 0; ix < _grid.Nx; ix++)
                    {
                        byte v = _grid.RawNode(ix, iy, iz);
                        if (v > peak) peak = v;
                    }
            return _grid.Decode(peak);
        }

        public string Attribution() => Data.Attribution();
    }
}
