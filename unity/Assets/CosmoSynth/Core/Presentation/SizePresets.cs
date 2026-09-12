namespace CosmoSynth.Core
{
    /// <summary>How large bodies are drawn relative to the truth (§5.9.6).</summary>
    public enum SizePreset
    {
        /// <summary>Every radius true. The honest setting, and mostly a view of empty space.</summary>
        True,

        /// <summary>The shipped default: radii compressed so moons stay visible beside giants.</summary>
        Readable,

        Times10,
        Times100,
        Times1000
    }

    /// <summary>
    /// The size presets, and the arithmetic that decides when a moon has been inflated into its
    /// own primary.
    ///
    /// Engine-free because every one of these numbers is a claim about the drawing that the
    /// honesty chip and badge then report, and a claim worth making is worth a known-answer test:
    /// Mars at x100 must hide Phobos and keep Deimos, Earth at x1000 must hide the Moon, and the
    /// Sun must never be drawn more than 25 times life size.
    /// </summary>
    public static class SizePresets
    {
        /// <summary>Earth, the reference the Readable law leaves unchanged.</summary>
        public const double ReferenceRadiusKm = Constants.EarthMeanRadius;

        /// <summary>The Readable exponent. Below 1 it pulls the range together; 0.55 is shipped.</summary>
        public const double ReadableExponent = 0.55;

        /// <summary>The Sun's ceiling in the multiplied presets: beyond this it swallows the inner system.</summary>
        public const double SunMaximumFactor = 25.0;

        /// <summary>A factor below this is not worth a warning; matches §5.9.6's badge threshold.</summary>
        public const double HonestyThreshold = 1.05;

        /// <summary>The plain multiplier of a preset. Readable has none: it is a law, not a factor.</summary>
        public static double PresetFactor(SizePreset preset) => preset switch
        {
            SizePreset.Times10 => 10.0,
            SizePreset.Times100 => 100.0,
            SizePreset.Times1000 => 1000.0,
            _ => 1.0
        };

        /// <summary>
        /// The radius a body is drawn at.
        /// </summary>
        /// <param name="isStar">Stars are capped at <see cref="SunMaximumFactor"/>: a Sun at x1000
        /// is 0.0047 AU in radius and Mercury orbits inside it.</param>
        public static double DisplayRadiusKm(SizePreset preset, double trueRadiusKm, bool isStar)
        {
            if (trueRadiusKm <= 0.0 || double.IsNaN(trueRadiusKm)) return trueRadiusKm;

            switch (preset)
            {
                case SizePreset.True:
                    return trueRadiusKm;
                case SizePreset.Readable:
                    return ReferenceRadiusKm * System.Math.Pow(trueRadiusKm / ReferenceRadiusKm, ReadableExponent);
                default:
                    double factor = PresetFactor(preset);
                    if (isStar) factor = System.Math.Min(factor, SunMaximumFactor);
                    return trueRadiusKm * factor;
            }
        }

        /// <summary>How much larger than life a drawn radius is. 1 means true.</summary>
        public static double Exaggeration(double displayRadiusKm, double trueRadiusKm) =>
            trueRadiusKm > 0.0 && displayRadiusKm > 0.0 ? displayRadiusKm / trueRadiusKm : 1.0;

        /// <summary>
        /// The Hill radius of a body about its primary: where its own gravity stops winning.
        /// </summary>
        public static double HillRadiusKm(double semiMajorAxisKm, double massKg, double primaryMassKg)
        {
            if (semiMajorAxisKm <= 0.0 || massKg <= 0.0 || primaryMassKg <= 0.0) return 0.0;
            return semiMajorAxisKm * System.Math.Pow(massKg / (3.0 * primaryMassKg), 1.0 / 3.0);
        }

        /// <summary>
        /// The one factor a whole satellite system's orbits are co-scaled by.
        ///
        /// One factor per system, not one per moon, because scaling each moon by whatever keeps it
        /// visible reorders them — and a Jupiter whose Galilean moons are in the wrong order is a
        /// worse lie than a small moon.
        ///
        /// Capped so the outermost moon stays inside half the primary's Hill radius: past that the
        /// drawn system is larger than the region the primary actually governs.
        /// </summary>
        public static double SystemOrbitFactor(SizePreset preset, double hillRadiusKm,
            double outermostMoonSemiMajorAxisKm)
        {
            double requested = PresetFactor(preset);
            if (requested <= 1.0) return 1.0;
            if (hillRadiusKm <= 0.0 || outermostMoonSemiMajorAxisKm <= 0.0) return requested;
            return System.Math.Min(requested, 0.5 * hillRadiusKm / outermostMoonSemiMajorAxisKm);
        }

        /// <summary>
        /// True when a moon's drawn orbit has ended up inside its drawn primary, so the moon is
        /// hidden and counted in the <c>N moons hidden</c> chip rather than drawn inside a planet.
        /// </summary>
        public static bool MoonIsHidden(double drawnOrbitRadiusKm, double drawnPrimaryRadiusKm) =>
            drawnOrbitRadiusKm > 0.0 && drawnOrbitRadiusKm <= drawnPrimaryRadiusKm;

        /// <summary>The preset's name as the panel and the chip write it.</summary>
        public static string Name(SizePreset preset) => preset switch
        {
            SizePreset.True => "True",
            SizePreset.Readable => "Readable",
            SizePreset.Times10 => "x10",
            SizePreset.Times100 => "x100",
            SizePreset.Times1000 => "x1000",
            _ => "?"
        };

        /// <summary>Parse the harness's <c>sizes:</c> view argument.</summary>
        public static bool TryParse(string text, out SizePreset preset)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "true": preset = SizePreset.True; return true;
                case "readable": preset = SizePreset.Readable; return true;
                case "x10": preset = SizePreset.Times10; return true;
                case "x100": preset = SizePreset.Times100; return true;
                case "x1000": preset = SizePreset.Times1000; return true;
                default: preset = SizePreset.Readable; return false;
            }
        }
    }
}
