using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Star brightness, done once, in one place.
    ///
    /// Three renderers and a label ranker all need "how bright is this star from here", and
    /// the Milky Way Atlas showed what happens when each answers it in its own shader: the
    /// stars, the Cepheids and the globulars end up on three different curves and the
    /// "flux-true" claim in the README is false by 17% at the default zoom.
    ///
    /// The formulae are the Atlas's own, read out of
    /// refs/webxr/nebula-gallery/index.html lines 294-300 and 344-350, so that the port
    /// matches what the owner already looked at rather than something merely defensible:
    ///
    ///     absM  = m - 5 log10(d / 10 pc)                      (line 344)
    ///     L     = 10^(-0.4 absM)                              (luminosity, Sun = 1 at M=0)
    ///     psize = L^0.28                                      (line 345)
    ///     m'    = -2.5 log10(L) + 5 log10(D) - 5              (line 293-295, from a vantage)
    ///     b     = 10^(0.4 (limit - m'))                       (line 297, 1.0 at the limit)
    ///     s     = 1.15 b^0.23 * exposure                      (line 298, eye compression)
    ///
    /// Everything here is engine-free double arithmetic, so the exponent that decides whether
    /// the sky looks like the sky is a tested number and not a constant buried in a shader.
    /// </summary>
    public static class Photometry
    {
        /// <summary>The Sun's absolute visual magnitude. The Atlas appends it as an ordinary star.</summary>
        public const double SunAbsoluteMagnitude = 4.83;

        /// <summary>Exponent compressing luminosity into a drawn size (Atlas line 345).</summary>
        public const double SizeExponent = 0.28;

        /// <summary>Exponent compressing brightness into a drawn size in observed mode (line 298).</summary>
        public const double EyeExponent = 0.23;

        /// <summary>Scale in front of the eye-response curve (line 298).</summary>
        public const double EyeScale = 1.15;

        /// <summary>Naked-eye limiting magnitude on a dark night: the Atlas's default slider value.</summary>
        public const double NakedEyeLimit = 6.5;

        /// <summary>Absolute magnitude from apparent magnitude and distance.</summary>
        public static double AbsoluteMagnitude(double apparentMagnitude, double distanceParsecs)
        {
            if (!(distanceParsecs > 0.0)) return double.NaN;
            return apparentMagnitude - 5.0 * Math.Log10(distanceParsecs / 10.0);
        }

        /// <summary>
        /// Apparent magnitude from absolute magnitude and distance: the distance modulus, and
        /// the whole reason a 3-D star field looks different from a painted dome.
        /// </summary>
        public static double ApparentMagnitude(double absoluteMagnitude, double distanceParsecs)
        {
            if (!(distanceParsecs > 0.0)) return double.NaN;
            return absoluteMagnitude + 5.0 * Math.Log10(distanceParsecs / 10.0);
        }

        /// <summary>
        /// Apparent magnitude including interstellar extinction. A_V is applied exactly once,
        /// here, from the value baked into the star record: the dust volume draws itself but
        /// never dims a star a second time, or the Coalsack would be counted twice.
        /// </summary>
        public static double ApparentMagnitude(double absoluteMagnitude, double distanceParsecs,
            double extinctionAv)
        {
            return ApparentMagnitude(absoluteMagnitude, distanceParsecs) + extinctionAv;
        }

        /// <summary>Luminosity relative to a magnitude-zero star, from an absolute magnitude.</summary>
        public static double Luminosity(double absoluteMagnitude) =>
            Math.Pow(10.0, -0.4 * absoluteMagnitude);

        /// <summary>Absolute magnitude back out of a luminosity.</summary>
        public static double AbsoluteFromLuminosity(double luminosity) =>
            luminosity > 0.0 ? -2.5 * Math.Log10(luminosity) : double.PositiveInfinity;

        /// <summary>
        /// The drawn-size attribute: L^0.28. Compressing luminosity this hard is what keeps
        /// Rigel (about 120,000 times the Sun) from being 120,000 times the sprite.
        /// </summary>
        public static double SizeAttribute(double absoluteMagnitude) =>
            Math.Pow(Luminosity(absoluteMagnitude), SizeExponent);

        /// <summary>Inverse of <see cref="SizeAttribute"/>, as the Atlas's shader does it.</summary>
        public static double LuminosityFromSizeAttribute(double sizeAttribute) =>
            Math.Pow(sizeAttribute, 1.0 / SizeExponent);

        /// <summary>
        /// Brightness relative to the visibility limit: 1.0 exactly at the limit, 2.512 one
        /// magnitude brighter, below 1.0 for a star the eye cannot see.
        /// </summary>
        public static double RelativeBrightness(double apparentMagnitude, double limitMagnitude) =>
            Math.Pow(10.0, 0.4 * (limitMagnitude - apparentMagnitude));

        /// <summary>
        /// The eye-response size: 1.15 b^0.23, the Atlas's compression. Zero below the limit,
        /// which is what makes stars vanish as the limit slider comes down instead of fading
        /// to an indistinguishable grey.
        /// </summary>
        public static double EyeResponse(double relativeBrightness, double exposure = 1.0)
        {
            if (!(relativeBrightness >= 1.0)) return 0.0;
            return EyeScale * Math.Pow(relativeBrightness, EyeExponent) * exposure;
        }

        /// <summary>
        /// The whole observed-sky path for one star: its absolute magnitude, its distance from
        /// a named vantage, and the limiting magnitude of the eye looking at it.
        /// Returns 0 when the star is below the limit.
        /// </summary>
        public static double ObservedSize(double absoluteMagnitude, double distanceFromVantageParsecs,
            double limitMagnitude, double extinctionAv = 0.0, double exposure = 1.0)
        {
            double m = ApparentMagnitude(absoluteMagnitude,
                Math.Max(distanceFromVantageParsecs, 0.02), extinctionAv);
            return EyeResponse(RelativeBrightness(m, limitMagnitude), exposure);
        }

        /// <summary>
        /// Effective temperature from B-V. Ballesteros, F. J. (2012), "New insights into black
        /// bodies", EPL 97 34008 - a published closed form rather than a fitted table, which
        /// is why the importer and the runtime can share it without drifting apart.
        /// </summary>
        public static double TemperatureFromColourIndex(double colourIndex)
        {
            double a = 0.92 * colourIndex + 1.7;
            double b = 0.92 * colourIndex + 0.62;
            if (Math.Abs(a) < 1e-6 || Math.Abs(b) < 1e-6) return 5600.0;
            return 4600.0 * (1.0 / a + 1.0 / b);
        }

        /// <summary>
        /// Automatic brightness assists, from the Atlas (lines 859-861). These are honesty
        /// problems as much as rendering ones: the Atlas's README says the flux-true look at
        /// natural scales is untouched, and farAssist is already 1.17x at its own default
        /// preset. Both are returned so the UI can say what it is doing.
        /// </summary>
        public static double SkyBoost(double metresPerParsec) =>
            Math.Max(1.0, Math.Pow(metresPerParsec / 1.5e-3, 0.9));

        public static double FarAssist(double metresPerParsec) =>
            metresPerParsec < 1.5e-3
                ? Math.Min(6.0, Math.Pow(1.5e-3 / metresPerParsec, 0.24))
                : 1.0;
    }
}
