using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// "What would the sky look like from there?" — the whole of the observed-sky mode that is
    /// arithmetic rather than rendering. CONTRACT B.1 MWA-14; Atlas port §4.4.
    ///
    /// The catalogue stores V as seen from the Sun. Move the vantage and both terms change: the
    /// distance modulus, and the extinction along the new line of sight. Doing that on the CPU in
    /// double, once, and handing the shader an absolute magnitude plus a distance is what lets the
    /// same star field serve "the sky from Earth" and "the sky from Betelgeuse" with no second
    /// data path.
    ///
    /// The Atlas did this in a vertex shader by undoing a <c>L^0.28</c> encoding
    /// (<c>pow(psize, 3.5714286)</c>) and could therefore never state an uncertainty or apply
    /// extinction. Here the photometry is the query and the shader only draws.
    /// </summary>
    public static class StarQuery
    {
        /// <summary>Below this distance the modulus is meaningless: you are inside the star.</summary>
        public const double MinimumDistanceParsecs = 1e-6;

        /// <summary>
        /// The de-reddened absolute magnitude: <c>M = V - 5 log10(d / 10 pc) - A_V</c>.
        ///
        /// Extinction is subtracted exactly once, here, from a catalogue <c>av</c> baked along the
        /// Sun-to-star line. The dust layer must never dim V a second time (R41): a star behind a
        /// cloud would then be reddened twice and the sky would be wrong in a way that looks like
        /// an exposure problem.
        /// </summary>
        public static double AbsoluteMagnitude(double apparentV, double distanceParsecs,
            double extinctionAv = 0.0)
        {
            double d = Math.Max(distanceParsecs, MinimumDistanceParsecs);
            return apparentV - 5.0 * Math.Log10(d / 10.0) - extinctionAv;
        }

        /// <summary>Apparent magnitude of an absolute-magnitude M source at D parsecs.</summary>
        public static double ApparentMagnitude(double absoluteMagnitude, double distanceParsecs,
            double extinctionAv = 0.0)
        {
            double d = Math.Max(distanceParsecs, MinimumDistanceParsecs);
            return absoluteMagnitude + 5.0 * Math.Log10(d / 10.0) + extinctionAv;
        }

        /// <summary>
        /// What this catalogue star looks like from <paramref name="vantageParsecs"/> (ICRF pc).
        /// Returns NaN when the star's distance is unknown, because then neither M nor the new
        /// distance exists — those rows stay on the sky shell and are never re-photometered.
        /// </summary>
        public static double ApparentMagnitudeFrom(in CatalogStar star, Double3 vantageParsecs,
            double extinctionAv = 0.0)
        {
            double? known = star.DistanceParsecs;
            if (!known.HasValue) return double.NaN;

            double absolute = AbsoluteMagnitude(star.ApparentMagnitude, known.Value);
            double distance = Double3.Distance(star.PositionParsecs, vantageParsecs);
            return ApparentMagnitude(absolute, distance, extinctionAv);
        }

        /// <summary>
        /// The eye's response to a star at <paramref name="apparent"/> against a limit magnitude:
        /// 1 at the limit, larger for anything brighter, 0 for anything fainter.
        ///
        /// <c>b = 10^(0.4 (limit - m))</c> is the flux ratio; the 0.23 exponent is the Atlas's
        /// perceptual compression, kept because it is what makes a magnitude-6.5 sky read like a
        /// magnitude-6.5 sky instead of like six stars and a black field (INFERRED).
        /// </summary>
        public const double EyeExponent = 0.23;

        public static double VisualResponse(double apparent, double limitMagnitude)
        {
            if (double.IsNaN(apparent) || apparent > limitMagnitude) return 0.0;
            double flux = Math.Pow(10.0, 0.4 * (limitMagnitude - apparent));
            return Math.Pow(flux, EyeExponent);
        }

        /// <summary>
        /// How many catalogue stars are naked-eye visible from a vantage. The honest answer to
        /// "how deep is this data": from the Sun at limit 6.5 the AT-HYG catalogue gives about
        /// 8,900, and raising the limit past about 8.5 reveals nothing at all because the
        /// catalogue stops there.
        /// </summary>
        public static int CountVisibleFrom(StarCatalog catalog, Double3 vantageParsecs,
            double limitMagnitude)
        {
            if (catalog == null) return 0;
            int n = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                double m = ApparentMagnitudeFrom(catalog.Stars[i], vantageParsecs);
                if (!double.IsNaN(m) && m <= limitMagnitude) n++;
            }
            return n;
        }

        /// <summary>The faintest V any row in the catalogue carries — the real depth of the data.</summary>
        public static double FaintestMagnitude(StarCatalog catalog)
        {
            double faintest = double.NegativeInfinity;
            if (catalog == null) return faintest;
            for (int i = 0; i < catalog.Count; i++)
            {
                double v = catalog.Stars[i].ApparentMagnitude;
                if (!double.IsNaN(v) && v > faintest) faintest = v;
            }
            return faintest;
        }
    }
}
