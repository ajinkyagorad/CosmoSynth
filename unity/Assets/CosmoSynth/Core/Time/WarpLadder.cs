namespace CosmoSynth.Core
{
    /// <summary>
    /// The signed warp ladder: 28 steps, symmetric about real time, with no zero step.
    ///
    /// Pause is a separate state that keeps the warp, which is why zero is absent. A zero rung
    /// would mean "paused" and "real time backwards" share a neighbour, so stepping out of pause
    /// would land on whichever side the last press came from — a control whose result depends on
    /// history rather than on the button.
    ///
    /// Lives in Core, engine-free, because the index-to-rate-to-label mapping is the part with
    /// answers that can be wrong, and it is worth a test rather than a glance.
    /// </summary>
    public static class WarpLadder
    {
        /// <summary>Forward rates in simulated seconds per real second, index 1..14.</summary>
        private static readonly double[] Forward =
        {
            1.0,            // +1  real time
            10.0,           // +2
            60.0,           // +3
            600.0,          // +4
            3600.0,         // +5
            21600.0,        // +6
            86400.0,        // +7
            604800.0,       // +8
            2592000.0,      // +9
            31557600.0,     // +10  1 Julian year
            3.15576e8,      // +11
            3.15576e9,      // +12
            3.15576e11,     // +13
            3.15576e13      // +14
        };

        private static readonly string[] ForwardLabels =
        {
            "real time",
            "10 seconds per second",
            "1 minute per second",
            "10 minutes per second",
            "1 hour per second",
            "6 hours per second",
            "1 day per second",
            "1 week per second",
            "30 days per second",
            "1 year per second",
            "10 years per second",
            "100 years per second",
            "10,000 years per second",
            "1,000,000 years per second"
        };

        public const int MinIndex = -14;
        public const int MaxIndex = 14;

        /// <summary>Real time forward: the default, and what "Now" restores.</summary>
        public const int RealTimeIndex = 1;

        /// <summary>Number of rungs. 28, not 29: there is no zero.</summary>
        public static int Count => Forward.Length * 2;

        /// <summary>True for every index the ladder actually has.</summary>
        public static bool IsValid(int index) =>
            index != 0 && index >= MinIndex && index <= MaxIndex;

        /// <summary>Clamp to the ladder, taking 0 to real time.</summary>
        public static int Clamp(int index)
        {
            if (index == 0) return RealTimeIndex;
            if (index < MinIndex) return MinIndex;
            if (index > MaxIndex) return MaxIndex;
            return index;
        }

        /// <summary>Simulated seconds per real second. Negative indices run time backwards.</summary>
        public static double Rate(int index)
        {
            index = Clamp(index);
            double magnitude = Forward[System.Math.Abs(index) - 1];
            return index < 0 ? -magnitude : magnitude;
        }

        /// <summary>The header text for a rung.</summary>
        public static string Label(int index)
        {
            index = Clamp(index);
            string forward = ForwardLabels[System.Math.Abs(index) - 1];
            return index < 0 ? forward + " backwards" : forward;
        }

        /// <summary>
        /// One step towards +14 (<paramref name="direction"/> &gt; 0) or towards -14.
        ///
        /// The only interesting case is the crossing: from +1 the next step down is -1, and from
        /// -1 the next step up is +1. Everything else is arithmetic.
        /// </summary>
        public static int Step(int index, int direction)
        {
            index = Clamp(index);
            if (direction == 0) return index;

            int next = direction > 0 ? index + 1 : index - 1;
            if (next == 0) next = direction > 0 ? RealTimeIndex : -RealTimeIndex;
            return Clamp(next);
        }

        /// <summary>The index whose rate is nearest a wanted one, for the RATE preset buttons.</summary>
        public static int NearestIndex(double secondsPerSecond)
        {
            int best = RealTimeIndex;
            double bestError = double.PositiveInfinity;
            for (int index = MinIndex; index <= MaxIndex; index++)
            {
                if (index == 0) continue;
                double error = System.Math.Abs(MathD.SafeLog10(System.Math.Abs(Rate(index))) -
                                               MathD.SafeLog10(System.Math.Abs(secondsPerSecond)));
                bool sameSign = (Rate(index) < 0.0) == (secondsPerSecond < 0.0);
                if (!sameSign) error += 100.0;
                if (error >= bestError) continue;
                bestError = error;
                best = index;
            }
            return best;
        }
    }
}
