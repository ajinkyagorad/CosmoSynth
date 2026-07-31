using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Turns hand tracking and controllers into the grab/scale gestures the universe responds to.
    ///
    /// Both input paths converge on one place — <see cref="TabletopManipulator.UpdateHands"/> —
    /// which takes a pose and a boolean per hand and knows nothing about where they came from.
    /// That is why the manipulator was written to accept injected poses from the start: the
    /// manipulation logic has no input dependency to stub out, so a controller, a tracked hand
    /// and a mouse in the editor all drive identical code, and a bug in the gesture recogniser
    /// can never be confused with a bug in the manipulation.
    ///
    /// Hands take priority when they are being tracked, because on Quest picking up a
    /// controller stops hand tracking and putting it down resumes it — the user switches
    /// modality mid-session without any explicit mode change, and the app should follow.
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

        [Header("References")]
        [SerializeField] private TabletopManipulator _manipulator;
        [SerializeField] private XrRig _rig;

        private XRHandSubsystem _handSubsystem;
        private readonly List<XRHandSubsystem> _handSubsystems = new List<XRHandSubsystem>();
        private readonly List<InputDevice> _devices = new List<InputDevice>();

        // Latched per hand, so the hysteresis band below actually has an effect.
        private bool _leftPinching;
        private bool _rightPinching;

        /// <summary>Which input the user is currently driving with.</summary>
        public enum Modality { None, Hands, Controllers }

        public Modality Active { get; private set; } = Modality.None;

        /// <summary>
        /// Raised when the user asks for the universe to be brought back in front of them.
        /// Bound to either controller's face button — "I have lost it" has to be recoverable
        /// without taking the headset off.
        /// </summary>
        public event System.Action RecenterRequested;

        private bool _recenterHeld;

        private void PollRecenter()
        {
            bool pressed = ReadPrimaryButton(InputDeviceCharacteristics.Left) ||
                           ReadPrimaryButton(InputDeviceCharacteristics.Right);

            // Edge-triggered, or holding the button would fire it every frame.
            if (pressed && !_recenterHeld) RecenterRequested?.Invoke();
            _recenterHeld = pressed;
        }

        private SimulationClock _clock;
        private bool _warpHeld;

        /// <summary>
        /// Time warp on the thumbstick: push right to speed time up, left to slow it, click to
        /// pause.
        ///
        /// Stepped rather than continuous, and edge-triggered rather than repeating. The clock's
        /// ladder runs from real time to a million years per second, so a continuous control
        /// would make it impossible to land on a rate you can name — and impossible to return to
        /// a moment you just flew past.
        /// </summary>
        private void PollTimeWarp()
        {
            if (_clock == null) _clock = FindAnyObjectByType<SimulationClock>();
            if (_clock == null) return;

            float axis = 0f;
            bool click = false;

            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller, _devices);

            foreach (InputDevice device in _devices)
            {
                if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick) &&
                    Mathf.Abs(stick.x) > Mathf.Abs(axis))
                {
                    axis = stick.x;
                }
                if (device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool pressed) && pressed)
                {
                    click = true;
                }
            }

            const float deadzone = 0.65f;
            bool active = Mathf.Abs(axis) > deadzone || click;

            if (active && !_warpHeld)
            {
                if (click) _clock.TogglePause();
                else if (axis > 0f) _clock.FasterWarp();
                else _clock.SlowerWarp();
            }
            _warpHeld = active;
        }

        private bool ReadPrimaryButton(InputDeviceCharacteristics side)
        {
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | side,
                _devices);

            if (_devices.Count == 0) return false;
            return _devices[0].TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed) && pressed;
        }

        private void Awake()
        {
            if (_manipulator == null) _manipulator = FindAnyObjectByType<TabletopManipulator>();
            if (_rig == null) _rig = FindAnyObjectByType<XrRig>();

            // Open is deliberately larger than close: a single threshold makes a pinch flicker
            // on and off while the fingers hover near it, which reads as the object being
            // dropped repeatedly.
            if (_pinchOpenMetres <= _pinchCloseMetres) _pinchOpenMetres = _pinchCloseMetres * 1.6f;
        }

        private void Update()
        {
            PollRecenter();
            PollTimeWarp();

            if (_manipulator == null) return;

            if (TryReadHands(out HandPose left, out HandPose right))
            {
                Active = Modality.Hands;
                _manipulator.UpdateHands(left, right);
                return;
            }

            if (TryReadControllers(out left, out right))
            {
                Active = Modality.Controllers;
                _manipulator.UpdateHands(left, right);
                return;
            }

            Active = Modality.None;
            _manipulator.UpdateHands(default, default);
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
                    if (subsystem.running) { _handSubsystem = subsystem; break; }
                }
                if (_handSubsystem == null) return false;
            }

            bool anyTracked = false;

            if (TryReadHand(_handSubsystem.leftHand, ref _leftPinching, out left)) anyTracked = true;
            if (TryReadHand(_handSubsystem.rightHand, ref _rightPinching, out right)) anyTracked = true;

            return anyTracked;
        }

        private bool TryReadHand(XRHand hand, ref bool pinching, out HandPose pose)
        {
            pose = default;
            if (!hand.isTracked) { pinching = false; return false; }

            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose index))
            {
                pinching = false;
                return false;
            }

            float separation = Vector3.Distance(thumb.position, index.position);

            // Hysteresis: close below one threshold, release only above a larger one.
            if (pinching) { if (separation > _pinchOpenMetres) pinching = false; }
            else { if (separation < _pinchCloseMetres) pinching = true; }

            // The grab point is between the fingertips — where the user perceives the pinch to
            // be — rather than the palm or wrist, which lag behind it by a hand's length and
            // make a held object feel anchored to the wrong place.
            Vector3 pinchPoint = (thumb.position + index.position) * 0.5f;

            Quaternion rotation = hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm)
                ? palm.rotation
                : index.rotation;

            pose = new HandPose(ToWorld(pinchPoint), rotation, pinching);
            return true;
        }

        // ---- Controllers -----------------------------------------------------

        private bool TryReadControllers(out HandPose left, out HandPose right)
        {
            // Presence is reported explicitly rather than inferred from the pose.
            //
            // The tempting test — "is the rotation still default?" — is quietly wrong: Unity's
            // Quaternion equality compares by dot product, and two default (0,0,0,0) quaternions
            // have a dot of 0, so they compare as NOT equal. A presence check written that way
            // returns true even with no controllers connected, and the manipulator then acts on
            // an identity pose sitting at the play-area origin.
            bool hasLeft = TryReadController(InputDeviceCharacteristics.Left, out left);
            bool hasRight = TryReadController(InputDeviceCharacteristics.Right, out right);
            return hasLeft || hasRight;
        }

        private bool TryReadController(InputDeviceCharacteristics side, out HandPose pose)
        {
            pose = default;

            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | side,
                _devices);

            if (_devices.Count == 0) return false;

            InputDevice device = _devices[0];
            if (!device.isValid) return false;
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position)) return false;

            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
                rotation = Quaternion.identity;

            // Grip, with the analogue trigger as a second way in — grabbing with either feels
            // natural and neither is obviously "the" grab button on a Touch controller.
            device.TryGetFeatureValue(CommonUsages.gripButton, out bool grip);
            device.TryGetFeatureValue(CommonUsages.trigger, out float trigger);

            pose = new HandPose(ToWorld(position), rotation, grip || trigger > 0.6f);
            return true;
        }

        // ---- Space conversion -------------------------------------------------

        /// <summary>
        /// Convert a tracking-space point into world space.
        ///
        /// Both hand joints and controller poses arrive in the XR Origin's tracking space, not
        /// in world space. Feeding them to the manipulator unconverted works right up until the
        /// rig is moved or the world is rescaled — at which point the grab anchor drifts away
        /// from the hand, which looks like a tracking fault rather than a coordinate bug.
        /// </summary>
        private Vector3 ToWorld(Vector3 trackingSpacePoint)
        {
            Transform origin = _rig != null && _rig.Origin != null
                ? _rig.Origin.CameraFloorOffsetObject.transform
                : null;
            return origin != null ? origin.TransformPoint(trackingSpacePoint) : trackingSpacePoint;
        }

        /// <summary>
        /// True when the pointing hand is pinching but the other is not — the gesture the
        /// selector treats as a click.
        ///
        /// A two-hand pinch is the scale gesture, so it must not also fire selection, or every
        /// resize would re-target whatever the ray happened to cross.
        /// </summary>
        public bool SelectPinch => _rightPinching ^ _leftPinching;

        /// <summary>Human-readable state, for the HUD.</summary>
        public string StatusLine
        {
            get
            {
                switch (Active)
                {
                    case Modality.Hands:
                        return $"hands{(_leftPinching ? " L-pinch" : "")}{(_rightPinching ? " R-pinch" : "")}";
                    case Modality.Controllers:
                        return "controllers";
                    default:
                        return "no XR input";
                }
            }
        }
    }
}
