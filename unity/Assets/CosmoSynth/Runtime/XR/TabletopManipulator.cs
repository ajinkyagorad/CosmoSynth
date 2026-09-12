using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>One hand's pose and grip state, fed in by whichever input backend is active.</summary>
    public struct HandPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public bool IsGripping;

        public HandPose(Vector3 position, Quaternion rotation, bool isGripping)
        {
            Position = position;
            Rotation = rotation;
            IsGripping = isGripping;
        }
    }

    /// <summary>
    /// The room-scale manipulation model: grab the universe with one hand to move and turn it,
    /// pull two hands apart to change its scale.
    ///
    /// The interaction is on the <i>universe</i>, not on individual objects. Grabbing a planet
    /// and dragging it would either move a real body to a false position or need a separate
    /// "model" copy that then disagrees with the simulation. Instead the hand transforms the
    /// mapping between absolute coordinates and the room, so nothing about the physics changes
    /// — the user is moving their viewpoint, expressed as moving the world, which is what
    /// actually feels right in a headset.
    ///
    /// Input arrives through <see cref="UpdateHands"/>, so this depends on no XR package and
    /// can be driven by controllers, hand tracking, or a mouse in the editor.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Tabletop Manipulator")]
    public sealed class TabletopManipulator : MonoBehaviour
    {
        [SerializeField] private UniverseSimulation _simulation;

        // The scale limits used to live here, as two serialised fields, and they disagreed with
        // every other opinion in the codebase about how far the ladder goes. There is now exactly
        // one pair, in Core/Presentation/ScaleLimits.cs, applied inside
        // FloatingOrigin.SetUnitsPerKm — so every writer is clamped whether it knows about the
        // limits or not, and this component no longer has an opinion.

        [Header("Feel")]
        [Tooltip("Hands must separate by at least this much before scaling engages, in metres.")]
        [SerializeField] private float _minimumSpanForScale = 0.08f;

        [Tooltip("Smoothing applied to scale changes. 0 is instant.")]
        [Range(0f, 0.95f)]
        [SerializeField] private float _scaleSmoothing = 0.5f;

        private HandPose _left;
        private HandPose _right;

        private bool _wasOneHanded;
        private bool _wasTwoHanded;

        private Vector3 _grabAnchorRoom;
        private Double3 _grabAnchorAbsoluteKm;
        private float _grabSpan;
        private double _grabUnitsPerKm;
        /// <summary>
        /// Scale the user has asked for, or 0 meaning "no request — leave the world alone".
        ///
        /// The sentinel matters. This used to be seeded from the origin's scale in Awake, which
        /// runs long before the catalogue finishes loading asynchronously and the bootstrap sets
        /// the real scale. So it captured the default of 1 unit/km and then quietly interpolated
        /// the whole solar system back to it — Earth inflating from a 55 cm hologram to 12 756
        /// units across, with the camera buried inside it and nothing coherent in view. Nothing
        /// logged an error; the app simply looked empty.
        /// </summary>
        private double _targetUnitsPerKm;

        /// <summary>When the last hand let go, so a reached scale stops being a standing order.</summary>
        private float _lastReleaseTime = float.NegativeInfinity;

        /// <summary>How long after the last release a scale request survives, seconds.</summary>
        private const float ReleaseHoldSeconds = 0.5f;

        /// <summary>True while either hand is holding the universe.</summary>
        public bool IsGrabbing => _left.IsGripping || _right.IsGripping;

        /// <summary>True while both hands are gripping, which is the scaling gesture.</summary>
        public bool IsScaling => _left.IsGripping && _right.IsGripping;

        /// <summary>Metres of room space one kilometre currently occupies.</summary>
        public double UnitsPerKm => _simulation != null ? _simulation.Origin.UnitsPerKm : 1.0;

        private void Awake()
        {
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();

            // Deliberately no initial target: until the user gestures, or something calls
            // FrameBodyAtSize, this component must not touch the scale at all.
            _targetUnitsPerKm = 0.0;
        }

        /// <summary>
        /// Feed the current hand poses. Call once per frame from the input backend.
        ///
        /// Poses arrive in WORLD space and are converted to the universe host's local space here,
        /// once, at the only door they come through. Everything downstream then calls
        /// <c>origin.ToAbsolute</c> on a universe-local point, which is what that method expects.
        /// Converting at each of the four call sites instead is how one of them gets missed, and a
        /// grab that is correct with the universe unrotated and drifts as soon as it is turned is
        /// exactly what that looks like.
        /// </summary>
        public void UpdateHands(HandPose left, HandPose right)
        {
            _left = ToUniverseSpace(left);
            _right = ToUniverseSpace(right);
        }

        private HandPose ToUniverseSpace(HandPose pose)
        {
            if (_simulation == null) return pose;

            Transform universe = _simulation.transform;
            pose.Position = universe.InverseTransformPoint(pose.Position);
            pose.Rotation = Quaternion.Inverse(universe.rotation) * pose.Rotation;
            return pose;
        }

        private void Update()
        {
            if (_simulation == null) return;

            if (IsScaling) UpdateTwoHanded();
            else if (IsGrabbing) UpdateOneHanded();
            else
            {
                if (_wasOneHanded || _wasTwoHanded) _lastReleaseTime = Time.unscaledTime;
                _wasOneHanded = false;
                _wasTwoHanded = false;
            }

            ApplySmoothedScale();
        }

        private void UpdateOneHanded()
        {
            HandPose hand = _left.IsGripping ? _left : _right;
            FloatingOrigin origin = _simulation.Origin;

            if (!_wasOneHanded)
            {
                // Record which absolute point the hand grabbed, so it stays pinned to the hand
                // for the whole drag no matter how the origin is rebased underneath.
                _grabAnchorRoom = hand.Position;
                _grabAnchorAbsoluteKm = origin.ToAbsolute(hand.Position);
                _wasOneHanded = true;
                _wasTwoHanded = false;
                return;
            }

            // Shift the origin so the grabbed absolute point tracks the hand exactly.
            Double3 handAbsoluteNow = origin.ToAbsolute(hand.Position);
            Double3 correction = _grabAnchorAbsoluteKm - handAbsoluteNow;
            origin.SetOrigin(origin.OriginKm + correction);
        }

        private void UpdateTwoHanded()
        {
            FloatingOrigin origin = _simulation.Origin;
            float span = Vector3.Distance(_left.Position, _right.Position);

            if (!_wasTwoHanded)
            {
                _grabSpan = Mathf.Max(span, _minimumSpanForScale);
                _grabUnitsPerKm = origin.UnitsPerKm;
                _grabAnchorRoom = (_left.Position + _right.Position) * 0.5f;
                _grabAnchorAbsoluteKm = ChooseScaleAnchor(origin, _grabAnchorRoom);
                _wasTwoHanded = true;
                _wasOneHanded = false;
                return;
            }

            if (span < _minimumSpanForScale) return;

            // Scale by the ratio of hand separations, raised to a power.
            //
            // A plain ratio is only about 16x across the full range of arm movement, which felt
            // like the zoom was walled off at solar-system distances. Cubing it fixed the range
            // and overshot the feel: ~4000x per gesture means a hand tremor is a visible lurch,
            // and the universe leaps past whatever the user was aiming at. Squaring gives ~250x
            // per pull — still only a handful of gestures across the whole catalogue, now that
            // the thumbstick carries actual travel and this gesture only has to handle scale.
            double ratio = System.Math.Pow(span / _grabSpan, 2.0);
            _targetUnitsPerKm = ScaleLimits.Clamp(_grabUnitsPerKm * ratio);

            // Keep the chosen anchor pinned, so the world scales about it.
            Vector3 midpoint = (_left.Position + _right.Position) * 0.5f;
            Double3 midpointAbsolute = origin.ToAbsolute(midpoint);
            origin.SetOrigin(origin.OriginKm + (_grabAnchorAbsoluteKm - midpointAbsolute));
        }

        /// <summary>
        /// Decide what the zoom pivots around.
        ///
        /// Pinning the raw midpoint between the hands is right when there is something between
        /// them, and badly wrong when there is not. Zoom out with your hands over empty space
        /// and the whole solar system rushes away from that empty point; zooming back in then
        /// re-expands about the same nothing, so the system never comes back and the controls
        /// feel like they have trapped you. Falling back to the focused body when the hands are
        /// not near anything makes the gesture mean "zoom toward what I am looking at", which
        /// is what a user expects and what makes the zoom reversible.
        /// </summary>
        private Double3 ChooseScaleAnchor(FloatingOrigin origin, Vector3 midpointRoom)
        {
            Double3 midpointAbsolute = origin.ToAbsolute(midpointRoom);

            CelestialBodyView nearest = _simulation.NearestBody(midpointAbsolute, out double surfaceKm);
            if (nearest != null)
            {
                // "Near" in render units, not kilometres — at tabletop scale a body a million km
                // away is still only centimetres from the hand, and pivoting on it is correct.
                double renderDistance = surfaceKm * origin.UnitsPerKm;
                if (renderDistance < 0.6) return midpointAbsolute;
            }

            CelestialBodyView focus = _simulation.FocusBody;
            return focus != null ? focus.AbsolutePositionKm : midpointAbsolute;
        }

        private void ApplySmoothedScale()
        {
            // No request means no change. Seeding a target here instead would reintroduce
            // exactly the bug the sentinel exists to prevent.
            if (_targetUnitsPerKm <= 0.0) return;

            FloatingOrigin origin = _simulation.Origin;

            // Clear the target once it is reached, and shortly after the hands let go.
            //
            // A target that is never cleared is a standing instruction: every rung tap and every
            // arrival that sets the scale through FloatingOrigin is then dragged back toward
            // whatever the last gesture asked for, one log-space step per frame, for the rest of
            // the session. The exact-equality test below could never fire, because the smoothing
            // approaches the target asymptotically and lands on it only by luck.
            if (System.Math.Abs(MathD.SafeLog10(origin.UnitsPerKm) - MathD.SafeLog10(_targetUnitsPerKm)) < 1e-3
                || (!IsGrabbing && Time.unscaledTime - _lastReleaseTime > ReleaseHoldSeconds))
            {
                _targetUnitsPerKm = 0.0;
                return;
            }

            if (_targetUnitsPerKm == origin.UnitsPerKm) return;

            if (_scaleSmoothing <= 0f)
            {
                origin.SetUnitsPerKm(_targetUnitsPerKm);
                return;
            }

            // Interpolate in log space: scale is multiplicative, so a linear blend between
            // 1e-9 and 1e-3 would spend almost all its time at the large end and snap through
            // six orders of magnitude at the last moment.
            double current = MathD.SafeLog10(origin.UnitsPerKm);
            double target = MathD.SafeLog10(_targetUnitsPerKm);
            double blend = 1.0 - System.Math.Pow(_scaleSmoothing, Time.unscaledDeltaTime * 60.0);

            origin.SetUnitsPerKm(System.Math.Pow(10.0, MathD.Lerp(current, target, blend)));
        }

        /// <summary>
        /// Set the scale so a body appears at a chosen size in the room — the "hold Earth in
        /// your hands" transform.
        /// </summary>
        public void FrameBodyAtSize(CelestialBodyView view, float diameterMetres)
        {
            if (view == null || _simulation == null) return;

            double radiusKm = view.Body.EquatorialRadiusKm ?? 1.0;
            _targetUnitsPerKm = ScaleLimits.Clamp(ScaleSpace.HologramUnitsPerKm(radiusKm, diameterMetres));

            // Put the body at the room's focal point rather than wherever it happened to be.
            _simulation.Origin.SetOrigin(view.AbsolutePositionKm);

            // Count as a release. The release clause below clears any target that is more than
            // ReleaseHoldSeconds old, and _lastReleaseTime is otherwise only ever written when a
            // gesture ends — so a request that did not come from a hand was discarded on the very
            // next Update, before a single step was applied. The origin move still happened, which
            // is the worst shape: the user is teleported to the body at the previous scale and the
            // scale they asked for silently vanishes.
            _lastReleaseTime = Time.unscaledTime;
        }

        /// <summary>Set the scale so a whole system spans a given size in the room.</summary>
        public void FrameSystemAtSize(string rootBodyId, float diameterMetres)
        {
            if (_simulation?.Catalog == null) return;

            double extentKm = 0.0;
            foreach (var childId in _simulation.Catalog.ChildIdsOf(rootBodyId))
            {
                var child = _simulation.Catalog.Get(childId);
                if (child?.Orbit == null || !child.Orbit.HasValue) continue;
                extentKm = System.Math.Max(extentKm, child.Orbit.Value.ApoapsisKm);
            }

            if (extentKm <= 0.0) return;

            _targetUnitsPerKm = ScaleLimits.Clamp(diameterMetres / (2.0 * extentKm));

            if (_simulation.Views.TryGetValue(rootBodyId, out var rootView))
            {
                _simulation.Origin.SetOrigin(rootView.AbsolutePositionKm);
            }

            // See FrameBodyAtSize: a non-gesture request must not be cleared before it is applied.
            _lastReleaseTime = Time.unscaledTime;
        }
    }
}
