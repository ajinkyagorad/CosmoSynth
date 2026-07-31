using System;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    public static class TimeTests
    {
        [Test("J2000 is 2000-01-01 12:00 TT, which is 11:58:55.816 UTC")]
        public static void J2000MapsToTheCorrectUtcInstant()
        {
            // The J2000 epoch is defined in Terrestrial Time, and TT ran 64.184 s ahead of UTC
            // in 2000 (32.184 s TT-TAI plus 32 leap seconds). A simulator that treats J2000 as
            // "noon UTC" is a minute out before it starts.
            var utc = CosmicTime.J2000.ToUtc();

            Assert.Equal(2000, utc.Year, 0, "year");
            Assert.Equal(1, utc.Month, 0, "month");
            Assert.Equal(1, utc.Day, 0, "day");
            Assert.Equal(11, utc.Hour, 0, "hour");
            Assert.Equal(58, utc.Minute, 0, "minute");
            Assert.Equal(55.816, utc.Second, 1e-3, "second");
        }

        [Test("Julian dates match published values")]
        public static void JulianDatesMatchPublishedValues()
        {
            // These two are the standard reference points for any date library.
            Assert.Equal(2440587.5, CalendarUtil.GregorianToJulianDate(1970, 1, 1, 0, 0, 0), 1e-9,
                "JD of the Unix epoch");
            Assert.Equal(2451545.0, CalendarUtil.GregorianToJulianDate(2000, 1, 1, 12, 0, 0), 1e-9,
                "JD of 2000-01-01 12:00");
            Assert.Equal(2451544.5, CalendarUtil.GregorianToJulianDate(2000, 1, 1, 0, 0, 0), 1e-9,
                "JD of 2000-01-01 00:00");
            Assert.Equal(2415020.5, CalendarUtil.GregorianToJulianDate(1900, 1, 1, 0, 0, 0), 1e-9,
                "JD of 1900-01-01 00:00");
        }

        private static readonly (int y, int m, int d, int h, int mi, double s)[] CalendarCases =
        {
            (1583, 1, 1, 0, 0, 0),
            (1600, 2, 29, 12, 30, 15.25),      // leap year divisible by 400
            (1700, 3, 1, 6, 0, 0),             // NOT a leap year in the Gregorian calendar
            (1900, 2, 28, 23, 59, 59.5),
            (2000, 2, 29, 0, 0, 0),
            (2026, 7, 30, 14, 48, 0),
            (2100, 3, 1, 1, 2, 3.75),
            (2400, 12, 31, 23, 59, 59.0),
        };

        [Test("integer day-number conversion is exact across centuries")]
        public static void DayNumberConversionIsExact()
        {
            foreach (var (y, m, d, _, _, _) in CalendarCases)
            {
                long dayNumber = CalendarUtil.GregorianToJulianDayNumber(y, m, d);
                CalendarUtil.JulianDayNumberToGregorian(dayNumber, out int by, out int bm, out int bd);

                Assert.Equal(y, by, 0, $"year for {y}-{m:D2}-{d:D2}");
                Assert.Equal(m, bm, 0, $"month for {y}-{m:D2}-{d:D2}");
                Assert.Equal(d, bd, 0, $"day for {y}-{m:D2}-{d:D2}");
            }

            // Consecutive days must map to consecutive day numbers with no gaps, including
            // across the Gregorian century-leap rules.
            long previous = CalendarUtil.GregorianToJulianDayNumber(1899, 12, 31);
            foreach (var date in new[] { (1900, 1, 1), (1900, 2, 28), (1900, 3, 1) })
            {
                long current = CalendarUtil.GregorianToJulianDayNumber(date.Item1, date.Item2, date.Item3);
                Assert.True(current > previous, "day numbers must increase monotonically");
                previous = current;
            }

            // 1900 was not a Gregorian leap year, so Feb 28 -> Mar 1 is a single day.
            Assert.Equal(1,
                CalendarUtil.GregorianToJulianDayNumber(1900, 3, 1) - CalendarUtil.GregorianToJulianDayNumber(1900, 2, 28),
                0, "1900 has no 29 February");

            // 2000 was, because it is divisible by 400.
            Assert.Equal(2,
                CalendarUtil.GregorianToJulianDayNumber(2000, 3, 1) - CalendarUtil.GregorianToJulianDayNumber(2000, 2, 28),
                0, "2000 does have a 29 February");
        }

        [Test("double Julian Date round trip stays inside its documented precision floor")]
        public static void JulianDateRoundTripStaysInsidePrecisionFloor()
        {
            // A JD near 2.5e6 spends most of a double's mantissa on the integer part, leaving
            // roughly 20 microseconds in the fraction. That is a property of the format, not a
            // defect in the conversion — this test pins the bound so a regression that made it
            // worse would be caught, and documents why CosmicTime does not store a JD.
            const double julianDatePrecisionFloorSeconds = 1e-4;

            foreach (var (y, m, d, h, mi, s) in CalendarCases)
            {
                double jd = CalendarUtil.GregorianToJulianDate(y, m, d, h, mi, s);
                var back = CalendarUtil.JulianDateToGregorian(jd);

                Assert.Equal(y, back.Year, 0, $"year for {y}-{m:D2}-{d:D2}");
                Assert.Equal(m, back.Month, 0, $"month for {y}-{m:D2}-{d:D2}");
                Assert.Equal(d, back.Day, 0, $"day for {y}-{m:D2}-{d:D2}");
                Assert.Equal(h, back.Hour, 0, $"hour for {y}-{m:D2}-{d:D2}");
                Assert.Equal(mi, back.Minute, 0, $"minute for {y}-{m:D2}-{d:D2}");
                Assert.Equal(s, back.Second, julianDatePrecisionFloorSeconds, $"second for {y}-{m:D2}-{d:D2}");
            }
        }

        [Test("CosmicTime round-trips through UTC")]
        public static void CosmicTimeRoundTripsThroughUtc()
        {
            (int y, int m, int d, int h, int mi, double s)[] cases =
            {
                (1980, 6, 15, 3, 20, 10.0),
                (2000, 1, 1, 12, 0, 0.0),
                (2017, 6, 1, 0, 0, 0.0),
                (2026, 7, 30, 14, 48, 30.5),
                (2050, 11, 5, 18, 45, 0.0),
            };

            foreach (var (y, m, d, h, mi, s) in cases)
            {
                var t = CosmicTime.FromUtc(y, m, d, h, mi, s);
                var back = t.ToUtc();
                Assert.Equal(y, back.Year, 0, $"year round trip {y}");
                Assert.Equal(m, back.Month, 0, $"month round trip {y}-{m}");
                Assert.Equal(d, back.Day, 0, $"day round trip {y}-{m}-{d}");
                Assert.Equal(h, back.Hour, 0, "hour round trip");
                Assert.Equal(mi, back.Minute, 0, "minute round trip");
                // Nanosecond-level, because this path never touches a double Julian Date.
                Assert.Equal(s, back.Second, 1e-9, "second round trip");
            }
        }

        [Test("leap seconds are applied from the correct dates")]
        public static void LeapSecondsApplyFromTheCorrectDates()
        {
            // Spot checks against the IERS table.
            Assert.Equal(10, LeapSecondTable.TaiMinusUtcAtUtc(CalendarUtil.GregorianToJulianDate(1972, 1, 5, 0, 0, 0)),
                1e-12, "TAI-UTC in early 1972");
            Assert.Equal(32, LeapSecondTable.TaiMinusUtcAtUtc(CalendarUtil.GregorianToJulianDate(2000, 1, 1, 0, 0, 0)),
                1e-12, "TAI-UTC in 2000");
            Assert.Equal(35, LeapSecondTable.TaiMinusUtcAtUtc(CalendarUtil.GregorianToJulianDate(2013, 6, 1, 0, 0, 0)),
                1e-12, "TAI-UTC in 2013");
            Assert.Equal(37, LeapSecondTable.TaiMinusUtcAtUtc(CalendarUtil.GregorianToJulianDate(2026, 7, 30, 0, 0, 0)),
                1e-12, "TAI-UTC today");
            Assert.Equal(37, LeapSecondTable.Current, 1e-12, "most recent tabulated leap second count");

            // Before the 1972 redefinition we clamp rather than model the rubber-band era.
            Assert.Equal(10, LeapSecondTable.TaiMinusUtcAtUtc(CalendarUtil.GregorianToJulianDate(1960, 1, 1, 0, 0, 0)),
                1e-12, "pre-1972 clamp");
        }

        [Test("TDB minus TT stays within its known millisecond envelope")]
        public static void TdbMinusTtStaysInEnvelope()
        {
            // The TDB-TT difference is a periodic term bounded by roughly +/-1.7 ms. If this
            // ever comes out large, the argument is being fed degrees where it wants radians.
            double maximum = 0.0;
            for (int day = 0; day < 4000; day += 7)
            {
                var t = CosmicTime.FromDaysPastJ2000(day);
                maximum = System.Math.Max(maximum, System.Math.Abs(t.TdbMinusTt));
            }
            Assert.True(maximum < 0.002, $"TDB-TT reached {maximum:G6} s, which is outside the physical envelope");
            Assert.True(maximum > 0.0015, $"TDB-TT only reached {maximum:G6} s — the annual term looks to be missing");
        }

        [Test("time arithmetic and ordering behave")]
        public static void TimeArithmeticBehaves()
        {
            var t0 = CosmicTime.FromUtc(2026, 1, 1);
            var t1 = t0 + Constants.SecondsPerDay;

            Assert.Equal(Constants.SecondsPerDay, t1 - t0, 1e-9, "one day of separation");
            Assert.True(t1 > t0, "later instant compares greater");
            Assert.True(t0 < t1, "earlier instant compares lesser");
            Assert.Equal(1.0, t1.DaysPastJ2000 - t0.DaysPastJ2000, 1e-12, "day difference");

            // Going out to a double JD and back costs the ~20 microseconds discussed on
            // CosmicTime; the point of asserting it here is that the loss is bounded and only
            // ever incurred by the interop accessor, never by the internal representation.
            var jdRoundTrip = CosmicTime.FromJulianDateTdb(t0.JulianDateTdb);
            Assert.Equal(t0.SecondsPastJ2000, jdRoundTrip.SecondsPastJ2000, 1e-4, "Julian date round trip");
        }

        [Test("ISO 8601 formatting is well formed")]
        public static void Iso8601FormattingIsWellFormed()
        {
            var t = CosmicTime.FromUtc(2026, 7, 30, 14, 48, 0);
            string iso = t.ToIso8601();
            Assert.True(iso.StartsWith("2026-07-30T14:48:", StringComparison.Ordinal),
                $"unexpected ISO rendering: {iso}");
            Assert.True(iso.EndsWith("Z", StringComparison.Ordinal), $"ISO string must be UTC-marked: {iso}");
        }
    }
}
