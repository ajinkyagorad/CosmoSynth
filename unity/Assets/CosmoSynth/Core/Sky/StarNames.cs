using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>One IAU proper name, joined to a catalogue row rather than to a position.</summary>
    public sealed class StarName
    {
        public string Name, Designation, Constellation;

        /// <summary>Row index in the star catalogue. Joining by index is what keeps the two in step.</summary>
        public int Row = -1;

        public int Hip;

        /// <summary>Set on the named flare stars, which the <c>flare</c> layer makes flicker.</summary>
        public bool Flare;
    }

    /// <summary>
    /// STUB (stars lane, CONTRACT C.2) — the 640 proper names from <c>sky/stars_named.json</c>.
    ///
    /// <b>What is missing on purpose:</b> <c>Parse(json)</c>. The <c>stars</c> data lane writes
    /// the file and owns its schema. Until it lands, stars are drawn and nothing is named, which
    /// is a visible and honest degradation rather than a crash.
    /// </summary>
    public sealed class StarNames
    {
        private readonly List<StarName> _names;
        private readonly Dictionary<int, string> _byRow = new Dictionary<int, string>();

        public StarNames(IEnumerable<StarName> names, string source)
        {
            _names = new List<StarName>();
            if (names != null) _names.AddRange(names);
            Source = source;
            for (int i = 0; i < _names.Count; i++)
                if (_names[i].Row >= 0) _byRow[_names[i].Row] = _names[i].Name;
        }

        public IReadOnlyList<StarName> Names => _names;

        public string Source { get; }

        public string NameOfRow(int row) => _byRow.TryGetValue(row, out string name) ? name : null;
    }
}
