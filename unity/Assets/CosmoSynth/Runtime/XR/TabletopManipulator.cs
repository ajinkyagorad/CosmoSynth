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

        [Header("Scale limits")]
        [Tooltip("Smallest render units per km. Lower means more universe fits in the room.")]
        // 1e-20 puts a thousand light years in the room; the old 1e-12 stopped at about forty
        // light years, which made the whole exoplanet catalogue feel walled off.
        [SerializeField] private double _minimumUnitsPerKm = 1e-20;

        [Tooltip("Largest render units per km. Higher means standing on a surface.")]
        // 1e6 is close enough to read a hundred-metre feature on a planet's surface.
        [SerializeField] private double _maximumUnitsPerKm = 1e6;

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

        /// <summary>Feed the current hand poses. Call once per frame from the input backend.</summary>
        public void UpdateHands(HandPose left, HandPose right)
        {
            _left = left;
            _right = right;
        }

        private void Update()
        {
            if (_simulation == null) return;

            if (IsScaling) UpdateTwoHanded();
            else if (IsGrabbing) UpdateOneHanded();
            else
            {
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
            _targetUnitsPerKm = MathD.Clamp(_grabUnitsPerKm * ratio, _minimumUnitsPerKm, _maximumUnitsPerKm);

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
            _targetUnitsPerKm = MathD.Clamp(
                ScaleSpace.HologramUnitsPerKm(radiusKm, diameterMetres),
                _minimumUnitsPerKm, _maximumUnitsPerKm);

            // Put the body at the room's focal point rather than wherever it happened to be.
            _simulation.Origin.SetOrigin(view.AbsolutePositionKm);
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

            _targetUnitsPerKm = MathD.Clamp(diameterMetres / (2.0 * extentKm),
                _minimumUnitsPerKm, _maximumUnitsPerKm);

            if (_simulation.Views.TryGetValue(rootBodyId, out var rootView))
            {
                _simulation.Origin.SetOrigin(rootView.AbsolutePositionKm);
            }
        }
    }
}
