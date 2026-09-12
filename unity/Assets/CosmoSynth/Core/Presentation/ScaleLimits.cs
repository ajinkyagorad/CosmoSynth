namespace CosmoSynth.Core
{
    /// <summary>
    /// The only clamp on the scale ladder.
    ///
    /// There used to be two that disagreed — <c>TabletopManipulator</c> carried 1e-20 to 1e6 while
    /// nothing else carried any — and a control whose range depends on which component last wrote
    /// it is a control the user cannot learn. One pair of numbers, applied inside
    /// <c>FloatingOrigin.SetUnitsPerKm</c>, so every writer of the scale goes through it whether it
    /// knows about this type or not.
    ///
    /// <c>MaxUnitsPerKm = 1e3</c> is true scale: one metre of space is one metre of room, and
    /// nothing is ever drawn larger than life. <c>MinUnitsPerKm = 2e-25</c> shrinks the observable
    /// universe to a 17.6 cm ball. 27.7 decades between them.
    /// </summary>
    public static class ScaleLimits
    {
        /// <summary>Smallest room metres per real kilometre: the observable universe as a 17.6 cm ball.</summary>
        public const double MinUnitsPerKm = 2e-25;

        /// <summary>Largest room metres per real kilometre: 1e3 is 1:1, and nothing is drawn larger than life.</summary>
        public const double MaxUnitsPerKm = 1e3;

        /// <summary>Decades of scale the ladder spans.</summary>
        public static double Decades => MathD.SafeLog10(MaxUnitsPerKm) - MathD.SafeLog10(MinUnitsPerKm);

        /// <summary>Clamp a requested scale. A non-finite or non-positive request returns the minimum.</summary>
        public static double Clamp(double unitsPerKm)
        {
            if (double.IsNaN(unitsPerKm) || unitsPerKm <= 0.0) return MinUnitsPerKm;
            return MathD.Clamp(unitsPerKm, MinUnitsPerKm, MaxUnitsPerKm);
        }

        /// <summary>True when a scale is inside the ladder without clamping.</summary>
        public static bool Contains(double unitsPerKm) =>
            unitsPerKm >= MinUnitsPerKm && unitsPerKm <= MaxUnitsPerKm;
    }
}
