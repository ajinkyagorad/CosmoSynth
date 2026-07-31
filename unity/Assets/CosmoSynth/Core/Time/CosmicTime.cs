using System;
using System.Globalization;

namespace CosmoSynth.Core
{
    /// <summary>
    /// An instant in the simulation, stored as <b>seconds past the J2000.0 epoch in TDB</b>
    /// (Barycentric Dynamical Time). This is the same quantity SPICE calls "ET" / ephemeris
    /// time, so imported kernels and Horizons vector tables drop straight in.
    ///
    /// Why TDB and not UTC: UTC is discontinuous (leap seconds) and is therefore useless as
    /// the independent variable of an equation of motion. UTC exists here only as a display
    /// and input format, converted at the boundary.
    ///
    /// Why seconds and not a Julian Date: a JD near 2.46e6 spends most of a double's mantissa
    /// on the integer part, leaving only ~20 microseconds of resolution in the fraction.
    /// Seconds past J2000 stay under 1e9 for the next thirty years, giving sub-microsecond
    /// resolution. The calendar conversions below therefore route through an exact integer
    /// day number and a separate seconds-of-day term, never through a single-double JD.
    /// <see cref="JulianDateTdb"/> is still offered for interoperability, with that caveat.
    /// </summary>
    [Serializable]
    public struct CosmicTime : IEquatable<CosmicTime>, IComparable<CosmicTime>
    {
        /// <summary>Seconds past J2000.0 TDB. Negative values are before 2000-01-01.</summary>
        public double SecondsPastJ2000;

        public CosmicTime(double secondsPastJ2000)
        {
            SecondsPastJ2000 = secondsPastJ2000;
        }

        public static readonly CosmicTime J2000 = new CosmicTime(0.0);

        /// <summary>Julian Day Number of J2000.0, i.e. of the calendar date 2000-01-01.</summary>
        public const long J2000DayNumber = 2451545L;

        /// <summary>TT - TAI, seconds. Fixed by definition.</summary>
        public const double TtMinusTai = 32.184;

        // ---- Julian dates (interop; see the precision note on the type) ------

        /// <summary>
        /// Julian Date in the TDB scale. Convenient for talking to external tools, but a
        /// double JD resolves only about 20 microseconds — do not round-trip through it when
        /// precision matters, use <see cref="SecondsPastJ2000"/> directly.
        /// </summary>
        public double JulianDateTdb => Constants.J2000JulianDate + SecondsPastJ2000 / Constants.SecondsPerDay;

        /// <summary>Julian centuries past J2000 in TDB — the argument of most precession/nutation series.</summary>
        public double JulianCenturiesPastJ2000 =>
            SecondsPastJ2000 / (Constants.SecondsPerDay * Constants.DaysPerJulianCentury);

        /// <summary>Julian days past J2000 in TDB.</summary>
        public double DaysPastJ2000 => SecondsPastJ2000 / Constants.SecondsPerDay;

        public static CosmicTime FromJulianDateTdb(double jd) =>
            new CosmicTime((jd - Constants.J2000JulianDate) * Constants.SecondsPerDay);

        public static CosmicTime FromDaysPastJ2000(double days) => new CosmicTime(days * Constants.SecondsPerDay);

        // ---- Time scales ----------------------------------------------------

        /// <summary>
        /// TDB - TT in seconds. The dominant term is an annual ~1.657 ms oscillation caused by
        /// Earth's motion in the Sun's gravity well. This is the standard Fairhead-Bretagnon
        /// first-order approximation; good to a few microseconds, which is well beyond what
        /// any rendering or trajectory display needs.
        /// </summary>
        public double TdbMinusTt => TdbMinusTtFromDays(DaysPastJ2000);

        private static double TdbMinusTtFromDays(double daysPastJ2000)
        {
            double g = (357.53 + 0.9856003 * daysPastJ2000) * MathD.Deg2Rad;
            double l = (246.11 + 0.90251792 * daysPastJ2000) * MathD.Deg2Rad;
            return 0.001657 * System.Math.Sin(g) + 0.000022 * System.Math.Sin(l);
        }

        /// <summary>Seconds past J2000 in Terrestrial Time.</summary>
        public double SecondsPastJ2000Tt => SecondsPastJ2000 - TdbMinusTt;

        /// <summary>Seconds past the J2000 instant measured on the TAI scale.</summary>
        public double SecondsPastJ2000Tai => SecondsPastJ2000Tt - TtMinusTai;

        /// <summary>TAI - UTC (the accumulated leap seconds) applicable at this instant.</summary>
        public double LeapSeconds => LeapSecondTable.TaiMinusUtcFromShiftedSeconds(
            SecondsPastJ2000Tt + 43200.0 - TtMinusTai);

        // ---- Arithmetic -----------------------------------------------------

        public static CosmicTime operator +(CosmicTime t, double seconds) => new CosmicTime(t.SecondsPastJ2000 + seconds);
        public static CosmicTime operator -(CosmicTime t, double seconds) => new CosmicTime(t.SecondsPastJ2000 - seconds);
        public static double operator -(CosmicTime a, CosmicTime b) => a.SecondsPastJ2000 - b.SecondsPastJ2000;

        public static bool operator <(CosmicTime a, CosmicTime b) => a.SecondsPastJ2000 < b.SecondsPastJ2000;
        public static bool operator >(CosmicTime a, CosmicTime b) => a.SecondsPastJ2000 > b.SecondsPastJ2000;
        public static bool operator <=(CosmicTime a, CosmicTime b) => a.SecondsPastJ2000 <= b.SecondsPastJ2000;
        public static bool operator >=(CosmicTime a, CosmicTime b) => a.SecondsPastJ2000 >= b.SecondsPastJ2000;
        public static bool operator ==(CosmicTime a, CosmicTime b) => a.SecondsPastJ2000 == b.SecondsPastJ2000;
        public static bool operator !=(CosmicTime a, CosmicTime b) => a.SecondsPastJ2000 != b.SecondsPastJ2000;

        public int CompareTo(CosmicTime other) => SecondsPastJ2000.CompareTo(other.SecondsPastJ2000);
        public bool Equals(CosmicTime other) => SecondsPastJ2000 == other.SecondsPastJ2000;
        public override bool Equals(object obj) => obj is CosmicTime other && Equals(other);
        public override int GetHashCode() => SecondsPastJ2000.GetHashCode();

        // ---- Calendar -------------------------------------------------------

        /// <summary>
        /// Build an instant from a proleptic Gregorian UTC calendar date.
        ///
        /// The chain is UTC -> TAI (add leap seconds) -> TT (add 32.184 s) -> TDB (add the
        /// periodic term). Everything is carried in seconds relative to an exact integer day
        /// number, so no precision is lost to a large Julian Date.
        /// </summary>
        public static CosmicTime FromUtc(int year, int month, int day, int hour = 0, int minute = 0, double second = 0.0)
        {
            long dayNumber = CalendarUtil.GregorianToJulianDayNumber(year, month, day);

            // Seconds from 2000-01-01T00:00:00 UTC, counting every day as 86400 s. This is the
            // same "quasi-linear UTC" that SOFA/ERFA use as the bridge to TAI.
            double utcSeconds = (dayNumber - J2000DayNumber) * Constants.SecondsPerDay
                                + hour * 3600.0 + minute * 60.0 + second;

            double taiMinusUtc = LeapSecondTable.TaiMinusUtcAtUtcSeconds(utcSeconds);

            // J2000 sits at 12:00 TT, hence the half-day shift.
            double secondsPastJ2000Tt = utcSeconds + taiMinusUtc + TtMinusTai - 43200.0;

            // TDB - TT is well under 2 ms, so evaluating its argument at the TT estimate rather
            // than iterating leaves an error of order 1e-13 s.
            double days = secondsPastJ2000Tt / Constants.SecondsPerDay;
            return new CosmicTime(secondsPastJ2000Tt + TdbMinusTtFromDays(days));
        }

        /// <summary>Decompose this instant into a proleptic Gregorian UTC calendar date.</summary>
        public CalendarDate ToUtc()
        {
            // Invert the chain above. 'shifted' is utcSeconds + (TAI-UTC), which is what we can
            // compute before knowing the leap-second count.
            double shifted = SecondsPastJ2000Tt + 43200.0 - TtMinusTai;
            double taiMinusUtc = LeapSecondTable.TaiMinusUtcFromShiftedSeconds(shifted);
            double utcSeconds = shifted - taiMinusUtc;

            // Split into an exact whole day plus a seconds-of-day remainder.
            double dayOffsetReal = System.Math.Floor(utcSeconds / Constants.SecondsPerDay);
            long dayOffset = (long)dayOffsetReal;
            double secondsOfDay = utcSeconds - dayOffsetReal * Constants.SecondsPerDay;

            CalendarUtil.JulianDayNumberToGregorian(J2000DayNumber + dayOffset,
                out int year, out int month, out int day);

            int hour = (int)(secondsOfDay / 3600.0);
            secondsOfDay -= hour * 3600.0;
            int minute = (int)(secondsOfDay / 60.0);
            double sec = secondsOfDay - minute * 60.0;

            // Guard against a rounding hair pushing us to exactly 60.
            if (sec >= 60.0) { sec -= 60.0; minute++; }
            if (minute >= 60) { minute -= 60; hour++; }
            if (hour >= 24)
            {
                hour -= 24;
                CalendarUtil.JulianDayNumberToGregorian(J2000DayNumber + dayOffset + 1, out year, out month, out day);
            }

            return new CalendarDate(year, month, day, hour, minute, sec);
        }

        /// <summary>ISO-8601 UTC rendering, e.g. <c>2026-07-30T14:48:00Z</c>.</summary>
        public string ToIso8601() => ToUtc().ToIso8601();

        public override string ToString() => ToIso8601();
    }

    /// <summary>
    /// A calendar date decomposed into fields. Kept engine-free and range-unlimited (unlike
    /// <see cref="DateTime"/>, which cannot represent years before 1 or past 9999 — a real
    /// constraint when winding a simulation back through geological time).
    /// </summary>
    [Serializable]
    public struct CalendarDate
    {
        public int Year;
        public int Month;
        public int Day;
        public int Hour;
        public int Minute;
        public double Second;

        public CalendarDate(int year, int month, int day, int hour, int minute, double second)
        {
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
            Minute = minute;
            Second = second;
        }

        public string ToIso8601()
        {
            // Round to milliseconds for display, carrying overflow so we never print ":60".
            double rounded = System.Math.Round(Second, 3);
            int wholeSeconds = (int)rounded;
            int milliseconds = (int)System.Math.Round((rounded - wholeSeconds) * 1000.0);
            int minute = Minute, hour = Hour;
            if (wholeSeconds >= 60) { wholeSeconds -= 60; minute++; }
            if (minute >= 60) { minute -= 60; hour++; }

            return string.Format(CultureInfo.InvariantCulture,
                "{0:D4}-{1:D2}-{2:D2}T{3:D2}:{4:D2}:{5:D2}{6}Z",
                Year, Month, Day, hour, minute, wholeSeconds,
                milliseconds != 0 ? "." + milliseconds.ToString("D3", CultureInfo.InvariantCulture) : string.Empty);
        }

        public override string ToString() => ToIso8601();
    }

    /// <summary>
    /// Proleptic Gregorian calendar &lt;-&gt; Julian day numbering, using the Fliegel-Van Flandern
    /// integer expressions. Exact in both directions for any year from about -4700 onward;
    /// earlier than that the intermediate terms go negative and C#'s truncating integer
    /// division stops agreeing with the floor division the algorithm assumes.
    /// </summary>
    public static class CalendarUtil
    {
        /// <summary>Earliest year for which the integer expressions below are exact.</summary>
        public const int MinimumSupportedYear = -4700;

        /// <summary>
        /// Julian Day Number of a calendar date — an exact integer, with no fractional day and
        /// therefore no precision loss. The JDN labels the day beginning at noon UT.
        /// </summary>
        public static long GregorianToJulianDayNumber(int year, int month, int day)
        {
            if (year < MinimumSupportedYear)
                throw new ArgumentOutOfRangeException(nameof(year),
                    $"Years before {MinimumSupportedYear} are outside the exact range of this conversion.");

            long a = (14 - month) / 12;
            long y = year + 4800 - a;
            long m = month + 12 * a - 3;
            return day + (153 * m + 2) / 5 + 365 * y + y / 4 - y / 100 + y / 400 - 32045;
        }

        /// <summary>Calendar date of a Julian Day Number — the exact inverse of the above.</summary>
        public static void JulianDayNumberToGregorian(long dayNumber, out int year, out int month, out int day)
        {
            long a = dayNumber + 32044;
            long b = (4 * a + 3) / 146097;
            long c = a - (146097 * b) / 4;
            long d = (4 * c + 3) / 1461;
            long e = c - (1461 * d) / 4;
            long m = (5 * e + 2) / 153;

            day = (int)(e - (153 * m + 2) / 5 + 1);
            month = (int)(m + 3 - 12 * (m / 10));
            year = (int)(100 * b + d - 4800 + m / 10);
        }

        /// <summary>
        /// Gregorian calendar to Julian Date as a single double.
        ///
        /// Provided for interoperability with tools that speak JD. Because the integer part is
        /// around 2.5 million, the fraction retains only about 20 microseconds of resolution;
        /// prefer <see cref="GregorianToJulianDayNumber"/> plus a seconds-of-day term, or
        /// <see cref="CosmicTime.FromUtc"/>, whenever the result feeds a computation.
        /// </summary>
        public static double GregorianToJulianDate(int year, int month, int day, int hour, int minute, double second)
        {
            long dayNumber = GregorianToJulianDayNumber(year, month, day);
            double dayFraction = (hour + minute / 60.0 + second / 3600.0) / 24.0;
            // The JDN refers to noon, so the civil day begins half a day earlier.
            return dayNumber - 0.5 + dayFraction;
        }

        /// <summary>Julian Date to calendar date. Carries the same ~20 microsecond floor as above.</summary>
        public static CalendarDate JulianDateToGregorian(double jd)
        {
            double shifted = jd + 0.5;
            double wholeDays = System.Math.Floor(shifted);
            double fraction = shifted - wholeDays;

            JulianDayNumberToGregorian((long)wholeDays, out int year, out int month, out int day);

            double secondsOfDay = fraction * Constants.SecondsPerDay;
            int hour = (int)(secondsOfDay / 3600.0);
            secondsOfDay -= hour * 3600.0;
            int minute = (int)(secondsOfDay / 60.0);
            double second = secondsOfDay - minute * 60.0;

            if (second >= 60.0) { second -= 60.0; minute++; }
            if (minute >= 60) { minute -= 60; hour++; }
            if (hour >= 24) { hour -= 24; day++; }

            return new CalendarDate(year, month, day, hour, minute, second);
        }
    }

    /// <summary>
    /// TAI-UTC leap seconds. The table is the full IERS history since the 1972 redefinition.
    /// Before 1972, UTC was rubber-band-rated against TAI with fractional offsets; we clamp to
    /// the 1972 value rather than model that, and say so here so nobody mistakes the clamp for
    /// a real quantity. No leap second has been announced beyond the last entry, and the IERS
    /// has resolved to stop inserting them by 2035.
    ///
    /// Boundaries are stored as UTC seconds past 2000-01-01T00:00:00 rather than as Julian
    /// Dates, so the comparison the conversions actually perform needs no unit change.
    /// </summary>
    public static class LeapSecondTable
    {
        // Julian Date (UTC) at which each new value took effect.
        private static readonly double[] BoundaryJulianDate =
        {
            2441317.5, 2441499.5, 2441683.5, 2442048.5, 2442413.5, 2442778.5, 2443144.5,
            2443509.5, 2443874.5, 2444239.5, 2444786.5, 2445151.5, 2445516.5, 2446247.5,
            2447161.5, 2447892.5, 2448257.5, 2448804.5, 2449169.5, 2449534.5, 2450083.5,
            2450630.5, 2451179.5, 2453736.5, 2454832.5, 2456109.5, 2457204.5, 2457754.5
        };

        private static readonly double[] Offset =
        {
            10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23,
            24, 25, 26, 27, 28, 29, 30,
            31, 32, 33, 34, 35, 36, 37
        };

        /// <summary>Boundaries expressed as UTC seconds past 2000-01-01T00:00:00.</summary>
        private static readonly double[] BoundaryUtcSeconds = BuildBoundarySeconds();

        private static double[] BuildBoundarySeconds()
        {
            var result = new double[BoundaryJulianDate.Length];
            // JD 2451544.5 is 2000-01-01T00:00:00.
            for (int i = 0; i < result.Length; i++)
                result[i] = (BoundaryJulianDate[i] - 2451544.5) * Constants.SecondsPerDay;
            return result;
        }

        /// <summary>TAI-UTC for a given Julian Date expressed in UTC.</summary>
        public static double TaiMinusUtcAtUtc(double julianDateUtc) =>
            TaiMinusUtcAtUtcSeconds((julianDateUtc - 2451544.5) * Constants.SecondsPerDay);

        /// <summary>TAI-UTC for a UTC instant given as seconds past 2000-01-01T00:00:00 UTC.</summary>
        public static double TaiMinusUtcAtUtcSeconds(double utcSeconds)
        {
            for (int i = BoundaryUtcSeconds.Length - 1; i >= 0; i--)
            {
                if (utcSeconds >= BoundaryUtcSeconds[i]) return Offset[i];
            }
            return Offset[0];
        }

        /// <summary>
        /// Recover TAI-UTC when only the shifted quantity (utcSeconds + TAI-UTC) is known,
        /// which is the situation on the way back from TT. Comparing each candidate offset
        /// against its own boundary resolves the step function without iterating.
        /// </summary>
        public static double TaiMinusUtcFromShiftedSeconds(double shiftedSeconds)
        {
            for (int i = BoundaryUtcSeconds.Length - 1; i >= 0; i--)
            {
                if (shiftedSeconds - Offset[i] >= BoundaryUtcSeconds[i]) return Offset[i];
            }
            return Offset[0];
        }

        /// <summary>The most recent tabulated value, currently 37 s (in force since 2017-01-01).</summary>
        public static double Current => Offset[Offset.Length - 1];
    }
}
