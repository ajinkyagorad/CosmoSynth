using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>
    /// STUB (integrator / data lanes, CONTRACT C.8, C.11) — a population of points with named
    /// optional columns: Cepheids, globulars, masers, the CF4 groups and the SDSS subsample all
    /// read through this one type.
    ///
    /// <b>What is missing on purpose:</b> <c>Parse</c>. The column-store readers belong to the
    /// <c>milkyway</c> and <c>galaxies</c> data lanes, and inventing a second reader here would
    /// guarantee two formats that disagree. Until those land, no <c>.csgp</c>/<c>.cspt</c> file is
    /// delivered, every consuming layer's <c>Weight</c> is 0 through <c>LayerState.Available</c>,
    /// and the panel row reads "not in this build" — which is the honest state, not a bug.
    ///
    /// Delete this file at merge and keep the lane's own. The read API below is what the
    /// <c>deep-sky</c> renderers consume, so it is the API the real type has to offer.
    /// </summary>
    public sealed class PointSet
    {
        private readonly Double3[] _positions;
        private readonly ushort[] _flags;
        private readonly string[] _names;
        private readonly Dictionary<string, float[]> _columns;

        public PointSet(string kind, double kmPerUnit, Double3[] positionsUnits, ushort[] flags,
            string[] names, Dictionary<string, float[]> columns)
        {
            Kind = kind;
            KmPerUnit = kmPerUnit;
            _positions = positionsUnits ?? new Double3[0];
            _flags = flags;
            _names = names;
            _columns = columns ?? new Dictionary<string, float[]>();
        }

        /// <summary>The manifest kind this set was delivered as: cepheids, globulars, masers, ...</summary>
        public string Kind { get; }

        /// <summary>Kilometres per storage unit. Positions are ICRF in the file's natural unit.</summary>
        public double KmPerUnit { get; }

        public int Count => _positions.Length;

        /// <summary>ICRF position in the file's own unit (pc for the galactic sets, Mpc for LSS).</summary>
        public Double3 PositionUnits(int i) => _positions[i];

        public Double3 PositionKm(int i) => _positions[i] * KmPerUnit;

        public ushort Flags(int i) => _flags == null ? (ushort)0 : _flags[i];

        public string Name(int i) => _names == null || i >= _names.Length ? null : _names[i];

        public bool HasColumn(string column) => _columns.ContainsKey(column);

        /// <summary>A named column, or NaN when this file does not carry it.</summary>
        public double Column(string column, int i) =>
            _columns.TryGetValue(column, out float[] values) && i < values.Length
                ? values[i]
                : double.NaN;

        public IEnumerable<string> Columns => _columns.Keys;
    }
}
