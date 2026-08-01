using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Thumbstick flight through the universe — the control every VR user reaches for first.
    ///
    /// Until this existed there was no locomotion on the headset at all. The only way to move
    /// was to grip and haul the whole universe past yourself, and the left thumbstick was bound
    /// to the time warp, so pushing it forward did not fly anywhere: it made the planets orbit
    /// faster. That is the worst possible answer to "why did I end up somewhere else" — the
    /// stick did something, just not the thing every other application in the headset does with
    /// it.
    ///
    /// Three things have to be right for stick flight to feel correct here.
    ///
    /// **Move the universe, never the camera.** The headset owns the camera transform. Writing
    /// to it fights the pose driver and the scene judders in time with head motion. Travel is
    /// applied to the floating origin instead, which is also what keeps float precision intact
    /// after a light year of flying.
    ///
    /// **Directions must cross the anchor.** "Forward" means where the user is looking, in world
    /// space, but the universe hangs under an anchor with its own arbitrary rotation — the
    /// bootstrap yaws it on launch so the focused planet is lit. Converting head-forward
    /// straight to ICRF without passing through the anchor's inverse sends the user off at
    /// whatever angle that yaw happened to be, which is exactly the "goes somewhere else than I
    /// expected" complaint.
    ///
    /// **Speed comes from the surroundings.** No constant works across twenty orders of
    /// magnitude: a metre per second is lethal in a room and invisible between planets. Speed is
    /// set so full deflection crosses the distance to the nearest surface in a few seconds, so
    /// the same push always takes about the same time to cross whatever is in front of you.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/XR Locomotion")]
    public sealed class XrLocomotion : MonoBehaviour
    {
        [Header("Speed")]
        [Tooltip("Seconds at full deflection to cross the gap to the nearest surface.")]
        [SerializeField] private float _secondsToCrossScene = 3.5f;

        [Tooltip("Multiplier at full left-trigger deflection.")]
        [SerializeField] private float _boostMultiplier = 6f;

        [Tooltip("Stick displacement below this is ignored.")]
        [SerializeField] private float _deadzone = 0.18f;

        [Header("Turning")]
        [Tooltip("Degrees per snap turn. Snap rather than smooth: smooth turning is the single "
                 + "largest cause of discomfort in VR, and this app is worn for long sessions.")]
        [SerializeField] private float _snapDegrees = 30f;

        [Tooltip("Stick displacement needed to trigger a snap turn.")]
        [SerializeField] private float _snapThreshold = 0.7f;

        [Header("References")]
        [SerializeField] private UniverseSimulation _simulation;
        [SerializeField] private XrRig _rig;
        [SerializeField] private Transform _universeAnchor;
        [SerializeField] private TabletopManipulator _manipulator;
        [SerializeField] private XrInput _input;

        /// <summary>Metres per second the user is currently travelling, in render units.</summary>
        public double SpeedKmPerSec { get; private set; }

        /// <summary>True while the stick is being pushed, so other systems can defer.</summary>
        public bool IsMoving { get; private set; }

        private bool _snapArmed = true;

        private void Awake()
        {
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();
            if (_rig == null) _rig = FindAnyObjectByType<XrRig>();
            if (_manipulator == null) _manipulator = FindAnyObjectByType<TabletopManipulator>();
            if (_input == null) _input = FindAnyObjectByType<XrInput>();

            // The anchor, not the simulation's own transform. They are parent and child with an
            // identity offset between them, so rotating either looks the same — but Recenter
            // writes to the anchor, and two systems turning different links of the same chain
            // is how a scene ends up at an orientation neither of them chose.
            if (_universeAnchor == null)
            {
                var bootstrap = FindAnyObjectByType<CosmoSynthBootstrap>();
                if (bootstrap != null) _universeAnchor = bootstrap.UniverseAnchor;
            }
        }

        private void Update()
        {
            IsMoving = false;

            if (_input == null) _input = FindAnyObjectByType<XrInput>();
            if (_simulation == null || _rig == null || _rig.Camera == null || _input == null) return;
            if (_universeAnchor == null) _universeAnchor = _simulation.transform;

            // A hand on the universe wins. Both this and the grab gesture move the origin, and
            // running them together in one frame applies both displacements — the universe
            // slides out from under the hand that is supposedly holding it still.
            if (_manipulator != null && _manipulator.IsGrabbing) return;

            // The dashboard takes the left stick while it is open. Without this both it and
            // flight act on the same push: the user scrolls a menu and travels at once.
            Vector2 left = _input.NavigationStickFree ? _input.Stick(Hand.Left) : Vector2.zero;
            Vector2 right = _input.Stick(Hand.Right);

            Translate(left, right.y);
            SnapTurn(right.x);
        }

        /// <summary>
        /// Left stick flies along the gaze and strafes; the right stick's vertical rises and falls.
        ///
        /// Vertical on a separate stick rather than on the trigger, because "up" is a direction
        /// and belongs on a directional control — and because the trigger is worth more as a
        /// throttle, which is the control users actually miss when it is absent.
        /// </summary>
        private void Translate(Vector2 planar, float vertical)
        {
            var input = new Vector3(Deadzone(planar.x), Deadzone(vertical), Deadzone(planar.y));
            if (input.sqrMagnitude <= 0f) return;

            Transform camera = _rig.Camera.transform;

            // Forward is GAZE, in full 3D — not gaze flattened onto the floor.
            //
            // Flattening is the usual choice for a walking simulator and it is wrong here. This
            // app is used looking down at a solar system on a table as often as looking out into
            // one, and with a flattened forward the stick sails horizontally past the hologram
            // the user is staring straight at. "I looked at Saturn and pushed forward and went
            // somewhere else" is precisely the complaint. Gaze-directed flight has exactly one
            // rule and the user can see it: you go where you are looking.
            //
            // Strafe stays level with the room, so sliding sideways never introduces a climb the
            // user did not ask for.
            Vector3 forward = camera.forward;
            if (forward.sqrMagnitude < 1e-8f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 rightAxis = Vector3.Cross(Vector3.up, forward);
            if (rightAxis.sqrMagnitude < 1e-8f) rightAxis = camera.right;   // looking straight up
            rightAxis.Normalize();
            Vector3 worldDirection = forward * input.z + rightAxis * input.x + Vector3.up * input.y;
            if (worldDirection.sqrMagnitude < 1e-8f) return;

            // Through the anchor into the universe's own space, THEN into ICRF. Skipping the
            // first step is what makes the stick point somewhere other than where the user is
            // looking, and it gets worse the more the anchor has been turned.
            Vector3 localDirection = _universeAnchor.InverseTransformDirection(worldDirection.normalized);
            Double3 icrf = CelestialBodyView.ToIcrfDirection(localDirection.normalized);

            FloatingOrigin origin = _simulation.Origin;
            Double3 viewerKm = origin.ToAbsolute(_simulation.ViewerLocalPosition);

            _simulation.NearestBody(viewerKm, out double surfaceKm);
            double speed = ScaleSpace.NavigationSpeed(surfaceKm, _secondsToCrossScene);

            // Analogue, on the left trigger — the hand that is already flying. It used to read
            // whichever trigger was pressed hardest, which put boost on the same control as
            // select and as grab; and since a grab makes this method return early, the boost
            // could not fire at all from the hand that was pulling the trigger.
            speed *= Mathf.Lerp(1f, _boostMultiplier, _input.BoostAxis);

            double magnitude = Mathf.Min(1f, worldDirection.magnitude);
            SpeedKmPerSec = speed * magnitude;
            IsMoving = true;

            // Moving the origin FORWARD moves the universe backward past the viewer, which is
            // the same operation the grab gesture performs and keeps the two consistent.
            origin.SetOrigin(origin.OriginKm + icrf * (SpeedKmPerSec * Time.unscaledDeltaTime));
        }

        /// <summary>
        /// Snap the universe about the user in fixed steps.
        ///
        /// Rotating about the camera rather than about the anchor's own pivot: the user expects
        /// to turn on the spot, and turning about a pivot metres away sweeps them sideways
        /// through the scene instead, which reads as the controls throwing them somewhere.
        /// </summary>
        private void SnapTurn(float stick)
        {
            if (Mathf.Abs(stick) < _snapThreshold)
            {
                _snapArmed = true;
                return;
            }

            if (!_snapArmed) return;
            _snapArmed = false;

            _universeAnchor.RotateAround(_rig.Camera.transform.position, Vector3.up,
                -Mathf.Sign(stick) * _snapDegrees);

            // A detent on each step. A snap turn with no feedback is indistinguishable from the
            // scene glitching, which is the last thing a comfort feature should feel like.
            _input.Pulse(Hand.Right, 0.3f, 0.02f);
        }

        private float Deadzone(float value) =>
            Mathf.Abs(value) < _deadzone ? 0f : Mathf.Sign(value) * ((Mathf.Abs(value) - _deadzone) / (1f - _deadzone));
    }
}
