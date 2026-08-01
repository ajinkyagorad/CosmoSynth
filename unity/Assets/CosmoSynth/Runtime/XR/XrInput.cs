using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace CosmoSynth.Runtime
{
    /// <summary>Which controller a reading came from.</summary>
    public enum Hand
    {
        Left,
        Right
    }

    /// <summary>
    /// The single owner of the controller bindings.
    ///
    /// It exists because four components were each reading the raw devices for themselves, and
    /// between them they had bound one physical control to several unrelated actions. None of
    /// this was visible in any one file; every collision lived in the gap between two of them:
    ///
    /// <list type="bullet">
    /// <item><b>The trigger did three things at once.</b> <c>BodySelector</c> treated it as
    /// select, <c>XrHandInput</c> folded it into the grip test so it also grabbed the universe,
    /// and <c>XrLocomotion</c> read it as a 6x speed boost. One pull re-targeted the selection,
    /// started dragging the world, and — because locomotion returns early while the manipulator
    /// is grabbing — did <i>not</i> boost, so the third binding was dead code that still cost the
    /// other two their meaning.</item>
    /// <item><b>Stick click both paused time and activated the wrist menu</b>, so choosing a
    /// destination also stopped the clock.</item>
    /// <item><b>The A button both recentred the universe and activated the wrist menu</b>, so
    /// travelling somewhere immediately hauled the universe back in front of the user.</item>
    /// <item><b>The left stick both flew and drove the menu</b>, with nothing arbitrating: flying
    /// with the wrist turned up scrolled the destination list at the same time.</item>
    /// </list>
    ///
    /// Fixing those individually would not stop it happening again, because the property that was
    /// violated — each control means one thing — is not stateable in any single component. It is
    /// stateable here. Every binding is in <see cref="Bindings"/>, one screenful, and a second
    /// claim on a control is a visible duplicate rather than an emergent behaviour.
    ///
    /// Arbitration is explicit for the one control that genuinely is modal: the left stick flies,
    /// unless the dashboard is open, in which case it drives the dashboard and locomotion stands
    /// down. Modes are fine; undeclared modes are not.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("CosmoSynth/XR Input")]
    public sealed class XrInput : MonoBehaviour
    {
        /// <summary>
        /// The complete binding table, for the legend and for review.
        ///
        /// Kept as data rather than as prose in a comment so the on-screen legend is generated
        /// from the same source the code uses. The old legend was written by hand and was already
        /// wrong — it advertised "trigger: boost" for a trigger that mostly grabbed, and never
        /// mentioned the wrist dashboard at all.
        /// </summary>
        public static readonly (string Control, string Action)[] Bindings =
        {
            ("L stick", "fly"),
            ("R stick", "rise · turn"),
            ("grip", "hold · scale"),
            ("R trigger", "select"),
            ("L trigger", "boost"),
            ("X", "dashboard"),
            ("A", "recentre"),
            ("Y / B", "time warp"),
            ("R click", "pause"),
        };

        [Tooltip("Stick displacement past which a direction counts as a deliberate press.")]
        [SerializeField] private float _stickPressThreshold = 0.65f;

        private readonly List<InputDevice> _leftDevices = new List<InputDevice>();
        private readonly List<InputDevice> _rightDevices = new List<InputDevice>();

        private HandState _left;
        private HandState _right;

        private struct HandState
        {
            public bool Present;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector2 Stick;
            public float Trigger;
            public bool Grip;
            public bool Primary;
            public bool Secondary;
            public bool StickClick;

            // Previous-frame copies, for edge detection.
            public bool PreviousPrimary;
            public bool PreviousSecondary;
            public bool PreviousStickClick;
            public bool PreviousTriggerDown;
            public Vector2 PreviousStickStep;
        }

        private ref HandState State(Hand hand)
        {
            if (hand == Hand.Left) return ref _left;
            return ref _right;
        }

        // ---- Raw, per hand ---------------------------------------------------

        public bool HasController(Hand hand) => State(hand).Present;

        public Vector2 Stick(Hand hand) => State(hand).Stick;

        public float Trigger(Hand hand) => State(hand).Trigger;

        public bool Grip(Hand hand) => State(hand).Grip;

        /// <summary>Controller pose, in tracking space.</summary>
        public bool TryGetPose(Hand hand, out Vector3 position, out Quaternion rotation)
        {
            ref HandState state = ref State(hand);
            position = state.Position;
            rotation = state.Rotation;
            return state.Present;
        }

        // ---- Bound actions, edge-triggered -----------------------------------

        /// <summary>Right trigger. The universal "click" — and now the only thing it does.</summary>
        public bool SelectPressed { get; private set; }

        /// <summary>A, on the right hand.</summary>
        public bool RecenterPressed { get; private set; }

        /// <summary>X, on the left hand.</summary>
        public bool DashboardTogglePressed { get; private set; }

        /// <summary>B, on the right hand.</summary>
        public bool WarpFasterPressed { get; private set; }

        /// <summary>Y, on the left hand.</summary>
        public bool WarpSlowerPressed { get; private set; }

        /// <summary>Right stick click. Left stick click is deliberately unbound — the left stick
        /// is the one under the thumb during flight, and a pause you can trigger by gripping
        /// harder while manoeuvring is a control that fires when you did not mean it.</summary>
        public bool PauseTogglePressed { get; private set; }

        /// <summary>Left trigger, as an analogue throttle.</summary>
        public float BoostAxis => _left.Trigger;

        /// <summary>
        /// A discrete step from a stick, for menu navigation: one event per push rather than one
        /// per frame while held.
        /// </summary>
        public Vector2Int StickStep(Hand hand)
        {
            ref HandState state = ref State(hand);

            var now = new Vector2(
                Mathf.Abs(state.Stick.x) > _stickPressThreshold ? Mathf.Sign(state.Stick.x) : 0f,
                Mathf.Abs(state.Stick.y) > _stickPressThreshold ? Mathf.Sign(state.Stick.y) : 0f);

            // Only the dominant axis, so a diagonal push does not both move the highlight and
            // switch the page.
            if (Mathf.Abs(now.x) > 0f && Mathf.Abs(now.y) > 0f)
            {
                if (Mathf.Abs(state.Stick.x) > Mathf.Abs(state.Stick.y)) now.y = 0f;
                else now.x = 0f;
            }

            var step = new Vector2Int(
                Mathf.Approximately(state.PreviousStickStep.x, now.x) ? 0 : (int)now.x,
                Mathf.Approximately(state.PreviousStickStep.y, now.y) ? 0 : (int)now.y);

            state.PreviousStickStep = now;
            return step;
        }

        // ---- Arbitration -----------------------------------------------------

        private object _navigationStickOwner;

        /// <summary>
        /// Claim the left stick for something other than flying.
        ///
        /// Returns false if someone else already holds it, so two claimants cannot both act on
        /// the same push — which is precisely what locomotion and the dashboard used to do.
        /// </summary>
        public bool ClaimNavigationStick(object owner)
        {
            if (_navigationStickOwner != null && !ReferenceEquals(_navigationStickOwner, owner)) return false;
            _navigationStickOwner = owner;
            return true;
        }

        public void ReleaseNavigationStick(object owner)
        {
            if (ReferenceEquals(_navigationStickOwner, owner)) _navigationStickOwner = null;
        }

        /// <summary>True when nothing has taken the left stick, so flight may use it.</summary>
        public bool NavigationStickFree => _navigationStickOwner == null;

        // ---- Haptics ---------------------------------------------------------

        /// <summary>
        /// A short buzz in one hand.
        ///
        /// There was none anywhere in the app. In a headset a selection that produces no click,
        /// no detent and no tick is genuinely hard to tell from a selection that did not
        /// register, and the user's response is to press again — which is how a working control
        /// comes to feel broken.
        /// </summary>
        public void Pulse(Hand hand, float amplitude = 0.35f, float seconds = 0.04f)
        {
            List<InputDevice> devices = hand == Hand.Left ? _leftDevices : _rightDevices;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].TryGetHapticCapabilities(out HapticCapabilities capabilities) &&
                    capabilities.supportsImpulse)
                {
                    devices[i].SendHapticImpulse(0, Mathf.Clamp01(amplitude), seconds);
                }
            }
        }

        // ---- Polling ---------------------------------------------------------

        private void Update()
        {
            Read(InputDeviceCharacteristics.Left, _leftDevices, ref _left);
            Read(InputDeviceCharacteristics.Right, _rightDevices, ref _right);

            // Bindings, in one place. Each physical control appears exactly once.
            SelectPressed = _right.Trigger > 0.6f && !_right.PreviousTriggerDown;
            RecenterPressed = _right.Primary && !_right.PreviousPrimary;
            DashboardTogglePressed = _left.Primary && !_left.PreviousPrimary;
            WarpFasterPressed = _right.Secondary && !_right.PreviousSecondary;
            WarpSlowerPressed = _left.Secondary && !_left.PreviousSecondary;
            PauseTogglePressed = _right.StickClick && !_right.PreviousStickClick;

            Latch(ref _left);
            Latch(ref _right);
        }

        private static void Latch(ref HandState state)
        {
            state.PreviousPrimary = state.Primary;
            state.PreviousSecondary = state.Secondary;
            state.PreviousStickClick = state.StickClick;
            state.PreviousTriggerDown = state.Trigger > 0.6f;
        }

        private static void Read(InputDeviceCharacteristics side, List<InputDevice> devices, ref HandState state)
        {
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | side, devices);

            if (devices.Count == 0 || !devices[0].isValid)
            {
                state.Present = false;
                state.Stick = Vector2.zero;
                state.Trigger = 0f;
                state.Grip = false;
                state.Primary = false;
                state.Secondary = false;
                state.StickClick = false;
                return;
            }

            InputDevice device = devices[0];
            state.Present = device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
            state.Position = position;

            state.Rotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation)
                ? rotation
                : Quaternion.identity;

            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
            state.Stick = stick;

            device.TryGetFeatureValue(CommonUsages.trigger, out float trigger);
            state.Trigger = trigger;

            device.TryGetFeatureValue(CommonUsages.gripButton, out bool grip);
            state.Grip = grip;

            device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary);
            state.Primary = primary;

            device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondary);
            state.Secondary = secondary;

            device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool click);
            state.StickClick = click;
        }
    }
}
