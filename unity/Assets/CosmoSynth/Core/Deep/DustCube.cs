using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// STUB (milkyway lane, CONTRACT C.7) — the Milky Way dust map, in galactic heliocentric
    /// parsecs, over a <see cref="VoxelGrid"/> whose values are <c>A0(550 nm) per parsec</c>.
    ///
    /// <b>What is missing on purpose:</b> <c>Parse</c>. The <c>milkyway</c> lane owns the
    /// <c>CSDU</c> reader and the Vergely+2022 bake. Everything that does not depend on the file
    /// format is here, because the renderer and the star importer must agree with it exactly:
    /// the extinction a star carries is <see cref="ExtinctionAlong"/> from the Sun, and it is
    /// applied ONCE, at bake time, to the star's absolute magnitude (R41). The dust layer draws
    /// the cloud; it never re-dims a star that has already been de-reddened.
    /// </summary>
    public sealed class DustCube
    {
        public DustCube(VoxelGrid grid) { Grid = grid; }

        /// <summary>The grid, in galactic heliocentric parsecs. Null when no file was delivered.</summary>
        public VoxelGrid Grid { get; }

        public bool IsPresent => Grid != null && Grid.Raw != null;

        /// <summary>Extinction per parsec at a point given in GALACTIC heliocentric pc. 0 outside the box.</summary>
        public double DensityAt(Double3 galacticPc) =>
            Grid == null ? 0.0 : Grid.SampleTrilinear(galacticPc);

        /// <summary>
        /// <c>A_V</c> in magnitudes along a straight line, both ends in galactic heliocentric pc.
        ///
        /// Symmetric by construction — the midpoint rule over a uniform partition gives the same
        /// answer in both directions, which the test asserts, because an asymmetric integrator
        /// would mean a star's extinction depended on which end you started from.
        /// </summary>
        public double ExtinctionAlong(Double3 aPc, Double3 bPc, double stepPc = 10.0)
        {
            if (Grid == null || stepPc <= 0.0) return 0.0;
            double length = Double3.Distance(aPc, bPc);
            if (length <= 0.0) return 0.0;

            int steps = (int)Math.Ceiling(length / stepPc);
            if (steps < 1) steps = 1;
            double h = length / steps;
            Double3 dir = (bPc - aPc) / length;

            double sum = 0.0;
            for (int i = 0; i < steps; i++)
            {
                Double3 mid = aPc + dir * ((i + 0.5) * h);
                sum += DensityAt(mid);
            }
            return sum * h;
        }

        /// <summary>ICRF parsecs to the galactic heliocentric parsecs the cube is sampled in.</summary>
        public static Double3 GalacticPcFromIcrfPc(Double3 icrfPc) =>
            StandardFrames.EquatorialToGalactic * icrfPc;

        public static Double3 IcrfPcFromGalacticPc(Double3 galacticPc) =>
            StandardFrames.GalacticToEquatorial * galacticPc;
    }
}
