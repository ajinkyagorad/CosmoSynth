using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Every continuous change of scale, and the readout that says what the scale means.
    ///
    /// <b>One limit for every input.</b> The WebXR Milky Way Atlas this ports has the bug being
    /// avoided here: its slider allows <c>log10 s</c> from -7.3 to <b>+5.7</b> while its two-hand
    /// gesture clamps the same quantity to <c>[10^-7.3, 10^0.5]</c> — 5.2 decades apart at the top,
    /// so the same universe has two different maximum sizes depending which control you touched
    /// last (VERIFIED <c>refs/webxr/nebula-gallery/index.html:88</c> against <c>:1474</c>). Here the
    /// only clamp is <see cref="ScaleLimits"/>, applied inside <c>FloatingOrigin.SetUnitsPerKm</c>,
    /// so a control that does not know the type still obeys it.
    ///
    /// The pivot rule is the other half: whatever the zoom is about stays where it is in the room,
    /// so zooming feels like moving a lens rather than like the scene jumping.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    [AddComponentMenu("CosmoSynth/Scale Control")]
    public sealed class ScaleControl : MonoBehaviour
    {
        /// <summary>Decades per second at full stick deflection (§5.1).</summary>
        public const double DecadesPerSecond = 0.6;

        /// <summary>Stick deadzone, below which nothing happens.</summary>
        public const float StickDeadzone = 0.12f;

        /// <summary>Decades per mouse-wheel notch on the desktop.</summary>
        public const double DecadesPerNotch = 0.1;

        /// <summary>Largest zoom boost from the left trigger.</summary>
        public const float MaximumZoomBoost = 4f;

        /// <summary>How long the limit readout flashes.</summary>
        public const float LimitFlashSeconds = 1.5f;

        private UniverseSimulation _simulation;
        private XrInput _input;
        private BodySelector _selector;
        private TravelController _travel;
        private ViewOptions _options;

        private double _rate;
        private float _limitFlashAge = float.PositiveInfinity;

        /// <summary>"limit: true scale (1:1)" or "limit: observable universe", while it flashes.</summary>
        public string LimitMessage { get; private set; }

        public bool LimitFlashing => _limitFlashAge < LimitFlashSeconds;

        public void Bind(UniverseSimulation simulation, XrInput input, BodySelector selector,
            TravelController travel, ViewOptions options)
        {
            _simulation = simulation;
            _input = input;
            _selector = selector;
            _travel = travel;
            _options = options;
        }

        // ---- the readout -----------------------------------------------------

        public double UnitsPerKm => _simulation != null ? _simulation.Origin.UnitsPerKm : 1.0;

        public double Log10U => MathD.SafeLog10(UnitsPerKm);

        /// <summary>The rung nearest in log10 U. Continuous zoom updates it; it never snaps.</summary>
        public Rung CurrentRung => LayerTable.NearestRung(Log10U);

        /// <summary>True when the scale is within 0.05 decades of the nearest rung.</summary>
        public bool AtRung => LayerTable.IsAtRung(CurrentRung, Log10U);

        /// <summary>The header line: <c>Whole Solar System · 1 m = 88.6 AU</c>.</summary>
        public string Readout()
        {
            Rung rung = CurrentRung;
            string name = rung != null ? rung.Caption : "Free scale";
            return name + " · " + Format.ScaleReadout(UnitsPerKm);
        }

        /// <summary>Where the ladder arc's tick sits, 0 at the smallest scale and 1 at true scale.</summary>
        public float LadderFraction()
        {
            double span = ScaleLimits.Decades;
            if (span <= 0.0) return 0f;
            return (float)MathD.Clamp01((Log10U - MathD.SafeLog10(ScaleLimits.MinUnitsPerKm)) / span);
        }

        // ---- inputs ----------------------------------------------------------

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (_limitFlashAge < LimitFlashSeconds) _limitFlashAge += deltaTime;
            else LimitMessage = null;

            double wanted = 0.0;

            if (_input != null && !_input.IsPanelHot(_input.Dominant))
            {
                float y = _input.Stick(_input.Dominant).y;
                if (Mathf.Abs(y) > StickDeadzone)
                {
                    double normalised = (Mathf.Abs(y) - StickDeadzone) / (1f - StickDeadzone);
                    double boost = 1.0 + (MaximumZoomBoost - 1.0) * _input.BoostAxis;
                    wanted = DecadesPerSecond * Mathf.Sign(y) * System.Math.Pow(normalised, 1.5) * boost;
                }
            }

            // Accelerate over 0.25 s and stop over 0.15 s (§5.7.5 rule 5): a scale that starts and
            // stops instantly is the part of a zoom that makes people put the headset down.
            double tau = System.Math.Abs(wanted) > System.Math.Abs(_rate) ? 0.25 : 0.15;
            _rate = MathD.Lerp(_rate, wanted, 1.0 - System.Math.Exp(-deltaTime / tau));

            if (System.Math.Abs(_rate) > 1e-4) ApplyZoom(_rate * deltaTime, "stick zoom");
        }

        /// <summary>The desktop wheel, and any other discrete step.</summary>
        public void Notch(float notches)
        {
            if (Mathf.Abs(notches) < 1e-4f) return;
            ApplyZoom(DecadesPerNotch * notches, "wheel zoom");
        }

        /// <summary>
        /// Change the scale by a number of decades about the pivot.
        ///
        /// The pivot's absolute position is held fixed in the room: after the scale changes from
        /// U0 to U1 the origin moves so <c>(pivot - origin) * U</c> is unchanged.
        /// </summary>
        public void ApplyZoom(double decades, string reason)
        {
            if (_simulation == null || _simulation.Origin == null) return;

            FloatingOrigin origin = _simulation.Origin;
            double before = origin.UnitsPerKm;
            double requested = before * System.Math.Pow(10.0, decades);
            double after = ScaleLimits.Clamp(requested);

            _travel?.Cancel(reason);

            if (after == before)
            {
                if (requested != before) FlashLimit(requested > before);
                return;
            }

            Double3 pivot = ZoomPivot();
            Double3 originKm = origin.OriginKm;

            origin.SetUnitsPerKm(after);
            origin.SetOrigin(pivot - (pivot - originKm) * (before / after));

            if (ScaleLimits.AtLimit(after, out bool atMax)) FlashLimit(atMax);
        }

        /// <summary>Jump to a rung by index, for Page Up / Page Down and the ladder buttons.</summary>
        public bool StepRung(int direction)
        {
            Rung rung = CurrentRung;
            if (rung == null || _travel == null) return false;

            // Rung 0 is the largest scale, 18 the smallest, so "up the ladder" is a lower index.
            int index = rung.Index;
            if (AtRung) index += direction > 0 ? -1 : 1;
            else if (direction > 0 && rung.Log10U < Log10U) index -= 1;
            else if (direction < 0 && rung.Log10U > Log10U) index += 1;

            index = Mathf.Clamp(index, 0, LayerTable.Rungs.Length - 1);
            return _travel.TravelToRung(LayerTable.Rungs[index].Id);
        }

        /// <summary>
        /// What the zoom is about (§5.1): the followed object, else the selection if it is drawn,
        /// else the anchor. The hands' midpoint case belongs to the two-hand gesture and is offered
        /// through <see cref="ZoomPivotNear"/>.
        /// </summary>
        public Double3 ZoomPivot()
        {
            if (_simulation == null) return Double3.Zero;

            if (_travel != null && _travel.FollowingId != null &&
                _simulation.Views.TryGetValue(_travel.FollowingId, out CelestialBodyView followed))
            {
                return followed.AbsolutePositionKm;
            }

            CelestialBodyView selected = _selector != null ? _selector.Selected : null;
            if (selected != null && selected.IsGeometryDrawn) return selected.AbsolutePositionKm;

            return _simulation.Origin.OriginKm;
        }

        /// <summary>
        /// The two-hand pivot: the hands' midpoint when a drawn object is within 0.6 m of it,
        /// otherwise the ordinary pivot. Today's rule, kept.
        /// </summary>
        public Double3 ZoomPivotNear(Vector3 midpointWorld)
        {
            if (_simulation == null) return Double3.Zero;

            CelestialBodyView nearest = null;
            float best = 0.6f;
            foreach (CelestialBodyView view in _simulation.Views.Values)
            {
                if (view == null || !view.IsGeometryDrawn) continue;
                float distance = Vector3.Distance(view.transform.position, midpointWorld);
                if (distance >= best) continue;
                best = distance;
                nearest = view;
            }

            return nearest != null ? nearest.AbsolutePositionKm : ZoomPivot();
        }

        private void FlashLimit(bool atMax)
        {
            LimitMessage = atMax ? "limit: true scale (1:1)" : "limit: observable universe";
            if (_limitFlashAge < LimitFlashSeconds) return;

            _limitFlashAge = 0f;
            _input?.Pulse(_input.Dominant, 0.5f, 0.03f);
        }

        /// <summary>Diagnostics for the play-mode report.</summary>
        public string Describe() =>
            $"{Readout()} (log10 U {Log10U:F3}, rung {(CurrentRung != null ? CurrentRung.Id : "-")}" +
            $"{(AtRung ? ", exact" : string.Empty)}, ladder {LadderFraction():F3})";
    }
}
