namespace CosmoSynth.Core
{
    /// <summary>
    /// Everything a travel interpolates: where the room is centred, how big the room is, which way
    /// the viewer faces, and which way is up.
    ///
    /// A plain readonly struct with an explicit constructor rather than a <c>readonly record
    /// struct</c>: that is C# 10 and this project is pinned to 9.0 (R18).
    /// </summary>
    public readonly struct ViewState
    {
        /// <summary>Absolute ICRF position, km, that sits at the room anchor.</summary>
        public readonly Double3 FocusKm;

        /// <summary>log10 of room metres per kilometre.</summary>
        public readonly double Log10U;

        /// <summary>Anchor yaw about room up, radians.</summary>
        public readonly double YawRad;

        /// <summary>The ICRF direction placed on room up.</summary>
        public readonly Double3 PlaneUpIcrf;

        public ViewState(Double3 focusKm, double log10U, double yawRad, Double3 planeUpIcrf)
        {
            FocusKm = focusKm;
            Log10U = log10U;
            YawRad = yawRad;
            PlaneUpIcrf = planeUpIcrf;
        }
    }

    /// <summary>
    /// One flight from one view to another (§5.7.1).
    ///
    /// There were three unrelated travel mechanisms — the dashboard's teleport, the desktop
    /// navigator's <c>TravelTo</c> and the harness's <c>FlyToStar</c> — so "go there" meant three
    /// different motions depending on which surface asked, and only one of them changed scale at
    /// all. This is the only one.
    ///
    /// Two things about the profile are not decoration:
    ///
    /// <b>Orientation happens first</b>, and is complete at 35 % of the flight. Turning and
    /// travelling at once is what makes a fly-through nauseating; turning, then travelling, is what
    /// every telescope control and every map application does.
    ///
    /// <b>The dip.</b> A straight interpolation of log scale from Earth to M31 puts both ends
    /// millions of room metres away at the midpoint — the viewer watches an empty room for four
    /// seconds and arrives with no idea what happened. So when the two ends cannot fit the room
    /// together at the smaller of the two scales, the scale falls until they can, the focus crosses
    /// while they are both in view, and the scale comes back up. That is a cheap version of van
    /// Wijk and Nuij's optimal zoom-pan, and the powers-of-ten moment is the whole reason the app
    /// exists.
    /// </summary>
    public sealed class TravelPlan
    {
        private readonly ViewState _from;
        private readonly ViewState _to;
        private readonly double _fitLog10U;
        private readonly bool _usesDip;

        /// <summary>How long the flight takes. Capped at 9 s: past that it is a cutscene.</summary>
        public double DurationSeconds { get; }

        /// <summary>True when the scale dips to fit both ends in the room before crossing.</summary>
        public bool UsesDip => _usesDip;

        /// <summary>The scale, as log10 U, at the bottom of a dip. Meaningless without one.</summary>
        public double FitLog10U => _fitLog10U;

        public ViewState From => _from;
        public ViewState To => _to;

        private TravelPlan(ViewState from, ViewState to, double fitLog10U, bool usesDip, double duration)
        {
            _from = from;
            _to = to;
            _fitLog10U = fitLog10U;
            _usesDip = usesDip;
            DurationSeconds = duration;
        }

        /// <param name="fitSpanMetres">How much of the room both ends have to share for the direct
        /// profile to be allowed. 0.6 m is the tabletop the user has lived with since 1.4.0.</param>
        public static TravelPlan Create(ViewState from, ViewState to, double fitSpanMetres = 0.6)
        {
            double separationKm = (to.FocusKm - from.FocusKm).Magnitude;
            double smaller = System.Math.Min(from.Log10U, to.Log10U);

            // The scale at which the two ends are fitSpan apart in the room. With no separation at
            // all any scale fits, so the direct profile always wins.
            double fit = separationKm > 0.0
                ? MathD.SafeLog10(fitSpanMetres / separationKm)
                : double.PositiveInfinity;

            bool usesDip = fit < smaller;
            double decades = usesDip
                ? System.Math.Abs(from.Log10U - fit) + System.Math.Abs(to.Log10U - fit)
                : System.Math.Abs(to.Log10U - from.Log10U);

            double duration = System.Math.Min(2.6 + 0.35 * decades, 9.0);
            return new TravelPlan(from, to, usesDip ? fit : smaller, usesDip, duration);
        }

        /// <summary>Smootherstep: zero first and second derivative at both ends.</summary>
        private static double Ease(double x)
        {
            x = MathD.Clamp01(x);
            return x * x * x * (x * (6.0 * x - 15.0) + 10.0);
        }

        /// <summary>
        /// The view at a moment of the flight.
        /// </summary>
        /// <param name="currentTargetKm">Where the target is <i>now</i>. Re-read every frame, so a
        /// flight to a planet lands on the planet rather than on its wake.</param>
        public ViewState At(double seconds, Double3 currentTargetKm)
        {
            double u = DurationSeconds > 0.0 ? MathD.Clamp01(seconds / DurationSeconds) : 1.0;

            // Orientation: complete at u = 0.35, before any of the translation has happened.
            double turn = Ease(System.Math.Min(1.0, u / 0.35));
            double yaw = _from.YawRad + MathD.WrapPi(_to.YawRad - _from.YawRad) * turn;
            Double3 up = Slerp(_from.PlaneUpIcrf, _to.PlaneUpIcrf, turn);

            double log10U;
            Double3 focus;

            if (!_usesDip)
            {
                double e = Ease(u);
                log10U = MathD.Lerp(_from.Log10U, _to.Log10U, e);
                focus = Double3.Lerp(_from.FocusKm, currentTargetKm, e);
            }
            else
            {
                log10U = u <= 0.5
                    ? MathD.Lerp(_from.Log10U, _fitLog10U, Ease(2.0 * u))
                    : MathD.Lerp(_fitLog10U, _to.Log10U, Ease(2.0 * u - 1.0));

                // The focus only moves while zoomed out, over the middle half of the flight.
                focus = Double3.Lerp(_from.FocusKm, currentTargetKm, Ease(MathD.Clamp01((u - 0.25) / 0.5)));
            }

            return new ViewState(focus, log10U, yaw, up);
        }

        /// <summary>The view at a moment, with the target fixed where the plan was made.</summary>
        public ViewState At(double seconds) => At(seconds, _to.FocusKm);

        /// <summary>
        /// Shortest-arc interpolation of two unit directions, falling back to a linear blend when
        /// they are nearly parallel (where the arc form divides by a vanishing sine).
        /// </summary>
        public static Double3 Slerp(Double3 a, Double3 b, double t)
        {
            Double3 from = a.Normalized;
            Double3 to = b.Normalized;
            double dot = MathD.Clamp(Double3.Dot(from, to), -1.0, 1.0);

            if (dot > 0.9995) return Double3.Lerp(from, to, t).Normalized;

            // Antiparallel has no shortest arc; pick any perpendicular so the motion is defined.
            if (dot < -0.9995)
            {
                Double3 axis = Double3.Cross(from, Double3.UnitZ);
                if (axis.SqrMagnitude < 1e-12) axis = Double3.Cross(from, Double3.UnitX);
                return Double3.Rotate(from, axis.Normalized, MathD.Pi * t);
            }

            double angle = MathD.SafeAcos(dot);
            double sin = System.Math.Sin(angle);
            return (from * (System.Math.Sin((1.0 - t) * angle) / sin) +
                    to * (System.Math.Sin(t * angle) / sin)).Normalized;
        }
    }
}
