using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Turns hand tracking and controllers into the grab/scale gesture, and issues the commands
    /// that are not tied to a surface — recentre and time warp.
    ///
    /// Both input paths converge on one place — <see cref="TabletopManipulator.UpdateHands"/> —
    /// which takes a pose and a boolean per hand and knows nothing about where they came from.
    /// That is why the manipulator was written to accept injected poses: a controller, a tracked
    /// hand and a mouse in the editor all drive identical code, so a bug in the gesture recogniser
    /// can never be confused with a bug in the manipulation.
    ///
    /// Hands take priority when tracked, because on Quest picking up a controller stops hand
    /// tracking and putting it down resumes it — the user changes modality mid-session with no
    /// explicit mode change, and the app should follow.
    ///
    /// Buttons are read through <see cref="XrInput"/> rather than from the devices directly. This
    /// component used to poll them itself and bound the trigger to the grip test, which quietly
    /// made every trigger pull a grab as well as a selection.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/XR Hand Input")]
    public sealed class XrHandInput : MonoBehaviour
    {
        [Header("Pinch gesture")]
        [Tooltip("Thumb-to-index distance in metres below which a pinch is closed.")]
        [SerializeField] private float _pinchCloseMetres = 0.022f;

        [Tooltip("Distance above which a closed pinch releases. Must exceed the close distance.")]
        [SerializeField] private float _pinchOpenMetres = 0.035f;

        [Tooltip("A pinch that travels further than this is a drag, not a tap, and does not select.")]
        [SerializeField] private float _tapTravelMetres = 0.03f;

        [Header("References")]
        [SerializeField] private TabletopManipulator _manipulator;
        [SerializeField] private XrRig _rig;
        [SerializeField] private XrInput _input;

        private XRHandSubsystem _handSubsystem;
        private readonly List<XRHandSubsystem> _handSubsystems = new List<XRHandSubsystem>();

        private SimulationClock _clock;

        private PinchState _leftPinch;
        private PinchState _rightPinch;

        /// <summary>
        /// A pinch, with enough history to tell a tap from a drag.
        ///
        /// The old rule was "exactly one hand pinching means select", which fires continuously
        /// throughout a one-handed grab: hauling the universe past yourself re-selected whatever
        /// the ray crossed on the way. A tap and a drag start identically, so the only honest
        /// place to decide is on release, by how far the hand travelled.
        /// </summary>
        private struct PinchState
        {
            public bool Closed;
            public Vector3 StartPosition;
            public float Travel;
            public bool TapReleased;
        }

        /// <summary>Which input the user is currently driving with.</summary>
        public enum Modality
        {
            None,
            Hands,
            Controllers
        }

        public Modality Active { get; private set; } = Modality.None;

        /// <summary>
        /// Raised when the user asks for the universe to be brought back in front of them.
        /// "I have lost it" has to be recoverable without taking the headset off.
        /// </summary>
        public event System.Action RecenterRequested;

        private void Awake()
        {
            if (_manipulator == null) _manipulator = FindAnyObjectByType<TabletopManipulator>();
            if (_rig == null) _rig = FindAnyObjectByType<XrRig>();

            // Open is deliberately larger than close: a single threshold makes a pinch flicker on
            // and off while the fingers hover near it, which reads as the object being dropped
            // repeatedly.
            if (_pinchOpenMetres <= _pinchCloseMetres) _pinchOpenMetres = _pinchCloseMetres * 1.6f;
        }

        private void Update()
        {
            if (_input == null) _input = FindAnyObjectByType<XrInput>();
            if (_input == null) return;

            PollCommands();

            if (_manipulator == null) return;

            bool wasGrabbing = _manipulator.IsGrabbing;

            if (TryReadHands(out HandPose left, out HandPose right))
            {
                Active = Modality.Hands;
                _manipulator.UpdateHands(left, right);
            }
            else if (TryReadControllers(out left, out right))
            {
                Active = Modality.Controllers;
                _manipulator.UpdateHands(left, right);
            }
            else
            {
                Active = Modality.None;
                _manipulator.UpdateHands(default, default);
            }

            // A tick on grabbing and on letting go, so the universe feels picked up rather than
            // just starting to move.
            if (_manipulator.IsGrabbing != wasGrabbing)
            {
                _input.Pulse(Hand.Left, 0.4f, 0.03f);
                _input.Pulse(Hand.Right, 0.4f, 0.03f);
            }
        }

        /// <summary>
        /// Recentre and time warp.
        ///
        /// Time warp is on the face buttons, not the thumbstick. The stick used to do it, and it
        /// was the worst control decision in the app: pushing forward on a thumbstick means "go
        /// forward" to everyone who has ever worn a headset, and here it made the planets orbit
        /// faster instead. The stick now flies; a secondary, occasional command belongs on Y and B.
        /// </summary>
        private void PollCommands()
        {
            if (_input.RecenterPressed)
            {
                RecenterRequested?.Invoke();
                _input.Pulse(Hand.Right, 0.5f, 0.06f);
            }

            if (_clock == null) _clock = FindAnyObjectByType<SimulationClock>();
            if (_clock == null) return;

            if (_input.PauseTogglePressed)
            {
                _clock.TogglePause();
                _input.Pulse(Hand.Right, 0.35f, 0.05f);
            }
            else if (_input.WarpFasterPressed)
            {
                _clock.FasterWarp();
                _input.Pulse(Hand.Right, 0.25f, 0.03f);
            }
            else if (_input.WarpSlowerPressed)
            {
                _clock.SlowerWarp();
                _input.Pulse(Hand.Left, 0.25f, 0.03f);
            }
        }

        // ---- Hand tracking ---------------------------------------------------

        private bool TryReadHands(out HandPose left, out HandPose right)
        {
            left = default;
            right = default;

            if (_handSubsystem == null || !_handSubsystem.running)
            {
                _handSubsystem = null;
                SubsystemManager.GetSubsystems(_handSubsystems);
                foreach (XRHandSubsystem subsystem in _handSubsystems)
                {
                    if (subsystem.running)
                    {
                        _handSubsystem = subsystem;
                        break;
                    }
                }
                if (_handSubsystem == null) return false;
            }

            bool anyTracked = false;
            if (TryReadHand(_handSubsystem.leftHand, ref _leftPinch, out left)) anyTracked = true;
            if (TryReadHand(_handSubsystem.rightHand, ref _rightPinch, out right)) anyTracked = true;
            return anyTracked;
        }

        private bool TryReadHand(XRHand hand, ref PinchState pinch, out HandPose pose)
        {
            pose = default;
            pinch.TapReleased = false;

            if (!hand.isTracked)
            {
                pinch.Closed = false;
                return false;
            }

            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose index))
            {
                pinch.Closed = false;
                return false;
            }

            float separation = Vector3.Distance(thumb.position, index.position);

            // The grab point is between the fingertips — where the user perceives the pinch to be
            // — rather than the palm, which lags it by a hand's length and makes a held object
            // feel anchored to the wrong place.
            Vector3 pinchPoint = ToWorld((thumb.position + index.position) * 0.5f);

            // Hysteresis: close below one threshold, release only above a larger one.
            if (pinch.Closed)
            {
                pinch.Travel = Mathf.Max(pinch.Travel, Vector3.Distance(pinchPoint, pinch.StartPosition));
                if (separation > _pinchOpenMetres)
                {
                    pinch.Closed = false;
                    pinch.TapReleased = pinch.Travel < _tapTravelMetres;
                }
            }
            else if (separation < _pinchCloseMetres)
            {
                pinch.Closed = true;
                pinch.StartPosition = pinchPoint;
                pinch.Travel = 0f;
            }

            Quaternion rotation = hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm)
                ? palm.rotation
                : index.rotation;

            pose = new HandPose(pinchPoint, rotation, pinch.Closed);
            return true;
        }

        // ---- Controllers -----------------------------------------------------

        /// <summary>
        /// Grip, and only grip.
        ///
        /// This used to accept <c>grip || trigger &gt; 0.6</c>, on the reasoning that neither is
        /// obviously "the" grab button on a Touch controller. That is true in isolation and wrong
        /// in context: the trigger is the one control every headset application uses to click, and
        /// spending it on grab meant selection and grab fired together every time. Grip is the
        /// hold; the trigger is the click.
        /// </summary>
        private bool TryReadControllers(out HandPose left, out HandPose right)
        {
            bool hasLeft = TryReadController(Hand.Left, out left);
            bool hasRight = TryReadController(Hand.Right, out right);
            return hasLeft || hasRight;
        }

        private bool TryReadController(Hand hand, out HandPose pose)
        {
            pose = default;
            if (!_input.TryGetPose(hand, out Vector3 position, out Quaternion rotation)) return false;

            pose = new HandPose(ToWorld(position), rotation, _input.Grip(hand));
            return true;
        }

        // ---- Space conversion -------------------------------------------------

        /// <summary>
        /// Convert a tracking-space point into world space.
        ///
        /// Hand joints and controller poses both arrive in the XR Origin's tracking space. Feeding
        /// them to the manipulator unconverted works right up until the rig moves or the world is
        /// rescaled, at which point the grab anchor drifts away from the hand — which looks like a
        /// tracking fault rather than a coordinate bug.
        /// </summary>
        private Vector3 ToWorld(Vector3 trackingSpacePoint)
        {
            Transform origin = _rig != null && _rig.Origin != null
                ? _rig.Origin.CameraFloorOffsetObject.transform
                : null;
            return origin != null ? origin.TransformPoint(trackingSpacePoint) : trackingSpacePoint;
        }

        /// <summary>
        /// True on the frame a pinch is released without having travelled — a tap, which the
        /// selector treats as a click. False throughout a drag, so grabbing the universe never
        /// also re-targets the selection.
        /// </summary>
        public bool SelectTap => _rightPinch.TapReleased || _leftPinch.TapReleased;

        /// <summary>
        /// World position of a tracked hand's index fingertip.
        ///
        /// The panel's whole touch model is a function of this one point, and it is why the panel
        /// works with no controllers at all — which the wrist dashboard it replaces did not.
        /// </summary>
        public bool TryGetIndexTipWorld(Hand hand, out Vector3 position)
        {
            position = Vector3.zero;
            if (_handSubsystem == null || !_handSubsystem.running) return false;

            XRHand xrHand = hand == Hand.Left ? _handSubsystem.leftHand : _handSubsystem.rightHand;
            if (!xrHand.isTracked) return false;
            if (!xrHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose pose)) return false;

            position = ToWorld(pose.position);
            return true;
        }

        /// <summary>World pose of a tracked hand's wrist, which is where the watch face sits.</summary>
        public bool TryGetWristWorld(Hand hand, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (_handSubsystem == null || !_handSubsystem.running) return false;

            XRHand xrHand = hand == Hand.Left ? _handSubsystem.leftHand : _handSubsystem.rightHand;
            if (!xrHand.isTracked) return false;
            if (!xrHand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose pose)) return false;

            position = ToWorld(pose.position);
            rotation = pose.rotation;
            return true;
        }

        /// <summary>Human-readable state, for the status card.</summary>
        public string StatusLine => Active switch
        {
            Modality.Hands => $"hands{(_leftPinch.Closed ? " L-pinch" : "")}{(_rightPinch.Closed ? " R-pinch" : "")}",
            Modality.Controllers => "controllers",
            _ => "no XR input"
        };
    }
}
