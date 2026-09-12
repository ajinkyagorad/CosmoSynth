using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>
    /// A named place with a reference: a molecular cloud, a nebula, a supercluster, a wall, a void.
    /// Positions are stored as sky coordinates plus a distance, and Core derives the Cartesian —
    /// so the file never carries two spellings of one position.
    /// </summary>
    public sealed class Place
    {
        public string Id, Name, Kind, Reference;
        public double RaDeg, DecDeg, DistancePc, RadiusPc;
        public ProvenanceClass Provenance = ProvenanceClass.Unknown;

        /// <summary>ICRF parsecs. The one conversion, so nothing downstream re-derives it.</summary>
        public Double3 IcrfPc => StandardFrames.FromSpherical(
            RaDeg * MathD.Deg2Rad, DecDeg * MathD.Deg2Rad, DistancePc);
    }

    /// <summary>
    /// STUB (milkyway / galaxies lanes, CONTRACT C.9, C.11) — the hand-authored landmark and
    /// structure catalogues, which share one schema.
    ///
    /// <b>What is missing on purpose:</b> <c>Parse(json)</c>. Both JSON files are authored by
    /// their data lanes and validated by <c>build_authored.py</c>; parsing them here would be a
    /// second schema. Until they land, <c>cloudMarkers</c> and <c>basins</c> have no places to
    /// name and say so.
    ///
    /// The rule the schema exists to enforce: <b>every entry carries a reference</b>. A named
    /// structure with no citation is indistinguishable from one somebody made up, and this app
    /// has a provenance panel that would then be lying.
    /// </summary>
    public sealed class PlaceCatalog
    {
        private readonly List<Place> _places;

        public PlaceCatalog(IEnumerable<Place> places)
        {
            _places = new List<Place>();
            if (places != null) _places.AddRange(places);
        }

        public IReadOnlyList<Place> Places => _places;

        public Place Find(string id)
        {
            for (int i = 0; i < _places.Count; i++)
                if (string.Equals(_places[i].Id, id, StringComparison.Ordinal)) return _places[i];
            return null;
        }

        /// <summary>Places of one kind, so a layer can take just the clouds or just the voids.</summary>
        public IEnumerable<Place> OfKind(string kind)
        {
            for (int i = 0; i < _places.Count; i++)
                if (string.Equals(_places[i].Kind, kind, StringComparison.Ordinal)) yield return _places[i];
        }

        /// <summary>Entries with no reference. Must be empty; the data lane's test asserts it.</summary>
        public IEnumerable<Place> Unreferenced()
        {
            for (int i = 0; i < _places.Count; i++)
                if (string.IsNullOrEmpty(_places[i].Reference)) yield return _places[i];
        }
    }
}
