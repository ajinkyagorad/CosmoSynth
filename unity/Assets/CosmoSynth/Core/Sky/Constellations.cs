using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>One IAU figure: polyline segments as pairs of star-catalogue row indices.</summary>
    public sealed class ConstellationFigure
    {
        public string Abbr, Iau, English;

        /// <summary>Segment endpoints as CSS2 row indices, two per segment.</summary>
        public int[] Pairs = new int[0];

        /// <summary>Row index of the figure's brightest star: where the name goes, with no dot.</summary>
        public int LabelStar = -1;

        public int SegmentCount => Pairs.Length / 2;
    }

    /// <summary>
    /// STUB (stars lane, CONTRACT C.2) — the 88 modern constellation figures as index pairs into
    /// the star catalogue.
    ///
    /// <b>What is missing on purpose:</b> <c>Parse(json, StarCatalog)</c>. The <c>stars</c> data
    /// lane resolves Stellarium's HIP polylines to CSS2 row indices at bake time, and doing it at
    /// runtime would mean shipping the HIP index too.
    ///
    /// <b>Indices, not directions, is the whole design.</b> The figures are drawn between the real
    /// three-dimensional stars, so they distort as the viewer leaves the Solar System — which is
    /// the point: a constellation is a line-of-sight coincidence, and flying a few parsecs out and
    /// watching Orion come apart is the clearest demonstration of that anyone can give.
    ///
    /// <b>Provenance.</b> The endpoints are Measured (they are star positions). The figure itself
    /// is a cultural convention and is tagged <see cref="ProvenanceClass.Fictional"/> — not as a
    /// slight, but because "who decided these seven stars are a saucepan" has an answer and it is
    /// not an observation.
    /// </summary>
    public sealed class Constellations
    {
        private readonly List<ConstellationFigure> _figures;

        public Constellations(IEnumerable<ConstellationFigure> figures, string source, string licence)
        {
            _figures = new List<ConstellationFigure>();
            if (figures != null) _figures.AddRange(figures);
            Source = source;
            Licence = licence;
        }

        public IReadOnlyList<ConstellationFigure> Figures => _figures;
        public string Source { get; }
        public string Licence { get; }

        public int SegmentCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _figures.Count; i++) n += _figures[i].SegmentCount;
                return n;
            }
        }

        public ConstellationFigure Find(string abbr)
        {
            for (int i = 0; i < _figures.Count; i++)
                if (string.Equals(_figures[i].Abbr, abbr, StringComparison.OrdinalIgnoreCase))
                    return _figures[i];
            return null;
        }

        /// <summary>
        /// Every index that falls outside a catalogue of <paramref name="rows"/> rows. Must be
        /// empty: a shifted index does not fail, it joins two random stars, and the result still
        /// looks like a constellation to anyone who does not know the sky.
        /// </summary>
        public IEnumerable<int> OutOfRange(int rows)
        {
            for (int f = 0; f < _figures.Count; f++)
            {
                int[] pairs = _figures[f].Pairs;
                for (int i = 0; i < pairs.Length; i++)
                    if (pairs[i] < 0 || pairs[i] >= rows) yield return pairs[i];
            }
        }
    }
}
