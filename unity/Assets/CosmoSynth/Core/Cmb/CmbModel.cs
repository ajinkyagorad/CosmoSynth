using System;

namespace CosmoSynth.Core
{
    /// <summary>Which rest frame the CMB is shown in. Only the dipole changes. CONTRACT §5.5.2.</summary>
    public enum CmbFrame : byte { Sun, LocalGroup, GalacticCentre }

    /// <summary>What the CMB layer is showing: the legend always names it. CONTRACT §5.5.2.</summary>
    public enum CmbMode : byte { Anisotropy, WithDipole, MonopoleDipoleAnisotropy }

    /// <summary>
    /// The physics and the false colour of the microwave background, as functions. CONTRACT C.12.
    ///
    /// The colour is a <b>function</b>, not a texture and not a table buried in a shader, for one
    /// reason: the map is false colour. The Wien peak of a 2.725 K black body is at 1.06 mm, which
    /// the eye cannot see at all, so every CMB image ever published is an arbitrary choice of
    /// palette over a temperature range. Writing the mapping as code with its range in the
    /// signature means the legend, the tap-to-measure readout and the GPU LUT are all generated
    /// from the same three lines and cannot drift into telling three different stories about what
    /// red means.
    ///
    /// The dipole is separated from the anisotropy for the same reason. It is not a feature of the
    /// early universe at all — it is the Solar System moving at 369 km/s — and a viewer who cannot
    /// switch it off has no way to see that.
    /// </summary>
    public static class CmbModel
    {
        /// <summary>Monopole, kelvin. Fixsen 2009: 2.72548 +/- 0.00057 K.</summary>
        public const double T0Kelvin = 2.72548;

        /// <summary>Solar-system dipole amplitude, microkelvin (Planck 2018).</summary>
        public const double DipoleAmplitudeMicroK = 3362.08;

        /// <summary>Dipole direction in galactic coordinates, degrees (Planck 2018).</summary>
        public const double DipoleLonDeg = 264.021;
        public const double DipoleLatDeg = 48.253;

        /// <summary>The full-LUT range in microkelvin. Raw byte 0 is -500, raw 255 is +500.</summary>
        public const double EncodedRangeMicroK = 500.0;

        /// <summary>Redshift of last scattering (INFERRED, the standard value).</summary>
        public const double RecombinationRedshift = 1089.9;

        /// <summary>Comoving distance to last scattering, megaparsecs (INFERRED, Planck 2018 cosmology).</summary>
        public const double LastScatteringMpc = 13869.0;

        /// <summary>The dipole direction as an ICRF unit vector.</summary>
        public static Double3 DipoleDirectionIcrf =>
            StandardFrames.GalacticToEquatorial * StandardFrames.FromSpherical(
                DipoleLonDeg * MathD.Deg2Rad, DipoleLatDeg * MathD.Deg2Rad);

        /// <summary>
        /// The dipole amplitude seen from a frame. The Sun's is the measured 3362 uK; the Local
        /// Group frame is the one in which the dipole nearly vanishes, and the Galactic-centre
        /// frame is in between. Only the first is Measured.
        /// </summary>
        public static double DipoleAmplitude(CmbFrame frame) => frame switch
        {
            CmbFrame.Sun => DipoleAmplitudeMicroK,
            CmbFrame.LocalGroup => 0.0,
            CmbFrame.GalacticCentre => 2350.0,     // INFERRED: the Sun's orbital component removed
            _ => DipoleAmplitudeMicroK,
        };

        /// <summary>Temperature in a direction, kelvin: monopole + dipole + the map's anisotropy.</summary>
        public static double TemperatureK(Double3 dirIcrf, double anisotropyMicroK, CmbFrame frame)
        {
            double dipole = DipoleAmplitude(frame) * Double3.Dot(dirIcrf.Normalized, DipoleDirectionIcrf);
            return T0Kelvin + (dipole + anisotropyMicroK) * 1e-6;
        }

        /// <summary>
        /// The false-colour map: microkelvin to linear RGB, over +/- <paramref name="contrastMicroK"/>.
        ///
        /// A diverging ramp with a neutral middle, so "zero" is a value the eye can find rather
        /// than a point on a rainbow. Cold is blue, hot is red, which is the convention every
        /// published Planck image uses — reversing it would be defensible and confusing.
        /// </summary>
        public static Double3 Palette(double microK, double contrastMicroK = 250.0,
            CmbPalette palette = CmbPalette.Diverging)
        {
            double t = MathD.Clamp01(0.5 + 0.5 * microK / Math.Max(1.0, contrastMicroK));
            switch (palette)
            {
                case CmbPalette.Grey:
                    return new Double3(t, t, t);
                case CmbPalette.Inverted:
                    return Diverging(1.0 - t);
                default:
                    return Diverging(t);
            }
        }

        /// <summary>Five stops, linear RGB, interpolated. Deep blue - blue - neutral - orange - red.</summary>
        private static Double3 Diverging(double t)
        {
            Double3[] stops =
            {
                new Double3(0.02, 0.05, 0.35),
                new Double3(0.10, 0.45, 0.85),
                new Double3(0.88, 0.88, 0.84),
                new Double3(0.95, 0.55, 0.15),
                new Double3(0.55, 0.03, 0.03),
            };
            double u = MathD.Clamp01(t) * (stops.Length - 1);
            int i = (int)Math.Floor(u);
            if (i >= stops.Length - 1) return stops[stops.Length - 1];
            return Double3.Lerp(stops[i], stops[i + 1], u - i);
        }

        /// <summary>Byte to microkelvin, exactly: 0 to -500, 255 to +500.</summary>
        public static double DecodeMicroK(byte raw) =>
            (raw / 255.0) * 2.0 * EncodedRangeMicroK - EncodedRangeMicroK;

        /// <summary>The radius of the observable universe in kilometres — what the object form is.</summary>
        public const double ObservableRadiusKm = LastScatteringMpc * 1e6 * GalaxyModel.KmPerParsec;
    }

    /// <summary>The three palettes the CMB legend can name. CONTRACT §5.5.2.</summary>
    public enum CmbPalette : byte { Diverging, Grey, Inverted }
}
