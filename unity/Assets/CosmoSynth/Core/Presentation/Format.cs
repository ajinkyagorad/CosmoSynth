using System.Globalization;

namespace CosmoSynth.Core
{
    /// <summary>Unit the viewer asked for, or Auto to let the distance choose.</summary>
    public enum DistanceUnit
    {
        Auto,
        Kilometres,
        AstronomicalUnits,
        LightYears,
        Parsecs
    }

    /// <summary>
    /// The one formatter for every number the interface shows.
    ///
    /// There were two — <c>WristDashboard.FormatDistance</c> and the provenance HUD's — and they
    /// disagreed about where kilometres become AU, so the same separation read differently on two
    /// surfaces a hand's width apart. Worse, both were written as a chain of magnitude tests with
    /// no test of their own, and a chain of magnitude tests is exactly the shape of code that is
    /// right in the middle of each band and wrong at every boundary.
    ///
    /// Engine-free, so the boundaries are testable: <c>FormatTests</c> asserts the unit chosen on
    /// both sides of every switch.
    /// </summary>
    public static class Format
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        // ---- significant figures --------------------------------------------

        /// <summary>
        /// A number to N significant figures, grouped, without an exponent unless it needs one.
        ///
        /// "3 significant figures" is what §5.1 asks the readout for, and `G3` is not it: `G3`
        /// gives 8.8E+23 where the panel wants 880 Gpc, and 1.4E+06 where it wants 1,400,000.
        /// </summary>
        public static string Significant(double value, int digits = 3)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "?";
            if (value == 0.0) return "0";
            if (digits < 1) digits = 1;

            double magnitude = System.Math.Abs(value);
            int exponent = (int)System.Math.Floor(System.Math.Log10(magnitude));

            if (magnitude >= 1e15 || magnitude < 1e-4)
            {
                double mantissa = value / System.Math.Pow(10.0, exponent);
                return mantissa.ToString("0." + new string('#', digits - 1), Invariant) + "e" +
                       exponent.ToString(Invariant);
            }

            // Round to the figure FIRST. Formatting with fewer decimal places does not do it:
            // 5096.8 with zero decimals is "5,097", which is four significant figures wearing the
            // costume of three.
            double unit = System.Math.Pow(10.0, exponent - (digits - 1));
            double rounded = System.Math.Round(value / unit, System.MidpointRounding.AwayFromZero) * unit;

            // '0', not '#': a trailing zero in "1.30 pc" is a significant figure and dropping it
            // claims less precision than the number has.
            int decimals = digits - 1 - exponent;
            if (decimals < 0) decimals = 0;
            if (decimals > 12) decimals = 12;
            return rounded.ToString("#,##0" + (decimals > 0 ? "." + new string('0', decimals) : string.Empty),
                Invariant);
        }

        /// <summary>A presentation gain, as the chips and badges write it: <c>x2,400</c>.</summary>
        public static string Multiplier(double factor) => "x" + Significant(factor, 2);

        // ---- distances -------------------------------------------------------

        /// <summary>
        /// The unit Auto picks for a separation, as one function so the label, the sub-line and
        /// the tests cannot disagree about the boundary.
        ///
        /// §5.9.1: km below 1e6 km; AU below 0.1 ly; ly (with pc) below 1 kpc; kpc or Mpc above.
        /// </summary>
        public static DistanceUnit AutoUnit(double km)
        {
            double magnitude = System.Math.Abs(km);
            if (magnitude < 1e6) return DistanceUnit.Kilometres;
            if (magnitude < 0.1 * Constants.LightYear) return DistanceUnit.AstronomicalUnits;
            if (magnitude < Constants.KiloParsec) return DistanceUnit.LightYears;
            return DistanceUnit.Parsecs;
        }

        /// <summary>A separation, in the unit asked for or the one Auto picks.</summary>
        public static string Distance(double km, DistanceUnit unit = DistanceUnit.Auto)
        {
            if (double.IsNaN(km) || double.IsInfinity(km)) return "unknown";
            if (unit == DistanceUnit.Auto) unit = AutoUnit(km);

            double magnitude = System.Math.Abs(km);
            switch (unit)
            {
                case DistanceUnit.Kilometres:
                    return magnitude < 1.0
                        ? Significant(km * 1000.0) + " m"
                        : Significant(km) + " km";
                case DistanceUnit.AstronomicalUnits:
                    return Significant(km / Constants.AstronomicalUnit) + " AU";
                case DistanceUnit.LightYears:
                    return Significant(km / Constants.LightYear) + " ly";
                default:
                    // The parsec family carries its own prefix, because "3,260,000 pc" is a
                    // number nobody reads and "1.00 Mpc" is one everybody does.
                    if (magnitude >= 1e3 * Constants.MegaParsec)
                        return Significant(km / (1e3 * Constants.MegaParsec)) + " Gpc";
                    if (magnitude >= Constants.MegaParsec)
                        return Significant(km / Constants.MegaParsec) + " Mpc";
                    if (magnitude >= Constants.KiloParsec)
                        return Significant(km / Constants.KiloParsec) + " kpc";
                    return Significant(km / Constants.Parsec) + " pc";
            }
        }

        /// <summary>
        /// The companion the measure label carries after a middle dot, or an empty string.
        ///
        /// Astronomy is bilingual: a stellar distance is quoted in light-years by the public and
        /// in parsecs by the literature, and a viewer comparing the app with either should not
        /// have to convert. Extragalactic distances get the same treatment in Mly.
        /// </summary>
        public static string DistanceDetail(double km, DistanceUnit unit = DistanceUnit.Auto)
        {
            if (double.IsNaN(km) || double.IsInfinity(km)) return string.Empty;
            if (unit == DistanceUnit.Auto) unit = AutoUnit(km);

            switch (unit)
            {
                case DistanceUnit.AstronomicalUnits:
                    return LightTime(km);
                case DistanceUnit.LightYears:
                    return Significant(km / Constants.Parsec) + " pc";
                case DistanceUnit.Parsecs:
                    return Significant(km / (1e6 * Constants.LightYear)) + " Mly";
                default:
                    return string.Empty;
            }
        }

        /// <summary>One-way light time over a distance, in the largest unit that reads well.</summary>
        public static string LightTime(double km)
        {
            double seconds = System.Math.Abs(km) / Constants.SpeedOfLight;
            if (seconds < 1.0) return Significant(seconds * 1000.0) + " light-ms";
            if (seconds < 120.0) return Significant(seconds) + " light-seconds";
            if (seconds < 7200.0) return Significant(seconds / 60.0) + " light-minutes";
            if (seconds < 48.0 * 3600.0) return Significant(seconds / 3600.0) + " light-hours";
            if (seconds < 2.0 * Constants.SecondsPerJulianYear) return Significant(seconds / Constants.SecondsPerDay) + " light-days";
            return Significant(seconds / Constants.SecondsPerJulianYear) + " light-years";
        }

        /// <summary>A duration, for mission elapsed time and travel estimates.</summary>
        public static string Duration(double seconds)
        {
            double magnitude = System.Math.Abs(seconds);
            if (magnitude < 120.0) return Significant(seconds) + " s";
            if (magnitude < 7200.0) return Significant(seconds / 60.0) + " min";
            if (magnitude < 48.0 * 3600.0) return Significant(seconds / 3600.0) + " h";
            if (magnitude < 2.0 * Constants.SecondsPerJulianYear) return Significant(seconds / Constants.SecondsPerDay) + " days";
            return Significant(seconds / Constants.SecondsPerJulianYear) + " years";
        }

        /// <summary>A speed. Below a kilometre per second the interesting digits are in m/s.</summary>
        public static string Speed(double kmPerSecond)
        {
            if (double.IsNaN(kmPerSecond)) return "unknown";
            return System.Math.Abs(kmPerSecond) < 1.0
                ? Significant(kmPerSecond * 1000.0) + " m/s"
                : Significant(kmPerSecond) + " km/s";
        }

        /// <summary>An angle, stepping down to arcminutes and arcseconds as it shrinks.</summary>
        public static string Angle(double radians)
        {
            double degrees = radians * MathD.Rad2Deg;
            double magnitude = System.Math.Abs(degrees);
            if (magnitude >= 1.0) return Significant(degrees) + "°";
            if (magnitude >= 1.0 / 60.0) return Significant(degrees * 60.0) + "′";
            return Significant(degrees * 3600.0) + "″";
        }

        // ---- the scale readout ----------------------------------------------

        /// <summary>
        /// The header's <c>1 m = &lt;distance&gt;</c>, on §5.1's ladder m, km, AU, ly, kpc, Mpc, Gpc.
        ///
        /// Deliberately not <see cref="Distance"/>: the readout ladder skips parsecs below a
        /// kiloparsec (the viewer is reading "how big is the room", not comparing a catalogue) and
        /// it must reach metres, because at true scale one room metre is one metre.
        /// </summary>
        public static string ScaleReadout(double unitsPerKm)
        {
            if (unitsPerKm <= 0.0 || double.IsNaN(unitsPerKm)) return "1 m = ?";

            double km = 1.0 / unitsPerKm;
            if (km < 1.0) return "1 m = " + Significant(km * 1000.0) + " m";
            if (km < 1e7) return "1 m = " + Significant(km) + " km";
            if (km < 0.1 * Constants.LightYear) return "1 m = " + Significant(km / Constants.AstronomicalUnit) + " AU";
            if (km < Constants.KiloParsec) return "1 m = " + Significant(km / Constants.LightYear) + " ly";
            if (km < Constants.MegaParsec) return "1 m = " + Significant(km / Constants.KiloParsec) + " kpc";
            if (km < 1e3 * Constants.MegaParsec) return "1 m = " + Significant(km / Constants.MegaParsec) + " Mpc";
            return "1 m = " + Significant(km / (1e3 * Constants.MegaParsec)) + " Gpc";
        }
    }
}
