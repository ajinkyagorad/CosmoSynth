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

    /// <summary>Which input a binding is for.</summary>
    public enum BindDevice : byte { Controller, Hand, Keyboard }

    /// <summary>
    /// When a binding applies. An <b>enum, not prose</b> (R37): the column used to be free text with
    /// ten spellings for about six states — "any", "default", "panel", "ray on the panel" — and a
    /// gate comparing sentences would have passed a real collision.
    /// </summary>
    public enum BindContext : byte
    {
        Any, Default, PanelRay, NotPanelRay, PanelTouch, Measure,
        Sandbox, HasSelection, ExhibitStage, RealOrFictionalStage, TrayHot
    }

    /// <summary>One row of the binding table. A struct, because C# 10 record structs are not available.</summary>
    public readonly struct Binding
    {
        public readonly string Control, Action;
        public readonly BindDevice Device;
        public readonly BindContext Context;

        public Binding(string control, BindDevice device, BindContext context, string action)
        {
            Control = control;
            Device = device;
            Context = context;
            Action = action;
        }
    }

    /// <summary>
    /// The single owner of the bindings.
    ///
    /// It exists because four components each read the raw devices for themselves, and between them
    /// they had bound one physical control to several unrelated actions — none of it visible in any
    /// one file. The trigger meant select <i>and</i> grab <i>and</i> boost; stick click meant pause
    /// <i>and</i> confirm; A meant recentre <i>and</i> confirm.
    ///
    /// Fixing those individually would not stop it happening again, because the property that was
    /// violated — each control means one thing — is not stateable in any single component. It is
    /// stateable here, and <c>binding_gate.py</c> keys uniqueness on the
    /// (Control, Device, Context) triple, so a second claim is a gate failure rather than an
    /// emergent behaviour.
    ///
    /// The two genuinely modal controls are arbitrated explicitly: <see cref="ClaimStick"/> for the
    /// sticks, and <see cref="SetPanelHot"/> for the hand that is on the panel — whose pinch,
    /// trigger and grip stand down while the claim holds. <b>The other hand is unaffected.</b>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("CosmoSynth/XR Input")]
    public sealed class XrInput : MonoBehaviour
    {
        /// <summary>
        /// The complete binding table (Appendix E), and the source of the on-screen legend.
        ///
        /// Kept as data rather than as prose so the legend is generated from what the code does.
        /// The hand-written one had already drifted: it advertised "trigger: boost" for a trigger
        /// that mostly grabbed, and never mentioned the wrist panel at all.
        /// </summary>
        public static readonly Binding[] Bindings =
        {
            new Binding("L stick", BindDevice.Controller, BindContext.RealOrFictionalStage, "fly along the full head direction; speed from the nearest object of any layer"),
            new Binding("L stick X", BindDevice.Controller, BindContext.ExhibitStage, "yaw the diorama"),
            new Binding("L stick click", BindDevice.Controller, BindContext.Any, "FOLLOW / FREE on the selection"),
            new Binding("L trigger", BindDevice.Controller, BindContext.Any, "boost: flight x1-x6, zoom x1-x4"),
            new Binding("L grip", BindDevice.Controller, BindContext.Any, "grab the stage; with R grip, scale plus a yaw twist"),
            new Binding("X", BindDevice.Controller, BindContext.Any, "wrist panel open / close / recall"),
            new Binding("Y", BindDevice.Controller, BindContext.Any, "time: step slower, towards reverse"),
            new Binding("L menu, hold 0.8 s", BindDevice.Controller, BindContext.Any, "recentre"),
            new Binding("R stick X", BindDevice.Controller, BindContext.Any, "snap turn 30 deg, or smooth turn if enabled"),
            new Binding("R stick Y", BindDevice.Controller, BindContext.NotPanelRay, "zoom, 0.6 decades per second"),
            new Binding("R stick Y", BindDevice.Controller, BindContext.PanelRay, "scroll the panel"),
            new Binding("R stick click", BindDevice.Controller, BindContext.Any, "pause / play"),
            new Binding("R trigger", BindDevice.Controller, BindContext.Default, "select"),
            new Binding("R trigger", BindDevice.Controller, BindContext.PanelRay, "press"),
            new Binding("R trigger", BindDevice.Controller, BindContext.Measure, "pick a measure point"),
            new Binding("R trigger, hold, pull, release", BindDevice.Controller, BindContext.Sandbox, "aim and launch a probe"),
            new Binding("R grip", BindDevice.Controller, BindContext.Any, "grab the stage; with L grip, scale plus twist"),
            new Binding("A", BindDevice.Controller, BindContext.HasSelection, "GO to the selection"),
            new Binding("B", BindDevice.Controller, BindContext.Any, "time: step faster"),
            new Binding("pinch, hold and move", BindDevice.Hand, BindContext.Default, "grab and drag the stage"),
            new Binding("pinch with both hands", BindDevice.Hand, BindContext.Any, "scale by the span ratio squared, plus a yaw twist"),
            new Binding("tap-pinch", BindDevice.Hand, BindContext.Default, "select"),
            new Binding("tap-pinch", BindDevice.Hand, BindContext.PanelRay, "press a pinned panel out of reach"),
            new Binding("tap-pinch", BindDevice.Hand, BindContext.Measure, "pick a measure point"),
            new Binding("double tap-pinch", BindDevice.Hand, BindContext.Default, "GO to the object"),
            new Binding("index fingertip on the panel", BindDevice.Hand, BindContext.PanelTouch, "touch press, slider drag or drag-scroll"),
            new Binding("index tip within 5 cm of the watch", BindDevice.Hand, BindContext.Any, "panel open / close / recall"),
            new Binding("pinch near the launch body, pull back, release", BindDevice.Hand, BindContext.Sandbox, "launch a probe"),
            new Binding("pinch on a tray specimen", BindDevice.Hand, BindContext.TrayHot, "grab it off the shelf"),
            new Binding("W A S D", BindDevice.Keyboard, BindContext.RealOrFictionalStage, "fly"),
            new Binding("Q E", BindDevice.Keyboard, BindContext.RealOrFictionalStage, "down, up"),
            new Binding("right-drag", BindDevice.Keyboard, BindContext.Any, "look"),
            new Binding("wheel", BindDevice.Keyboard, BindContext.NotPanelRay, "zoom 0.1 decade per notch"),
            new Binding("Shift", BindDevice.Keyboard, BindContext.Any, "boost"),
            new Binding("Ctrl", BindDevice.Keyboard, BindContext.Any, "slow"),
            new Binding("left click", BindDevice.Keyboard, BindContext.Default, "select"),
            new Binding("left click", BindDevice.Keyboard, BindContext.PanelRay, "press"),
            new Binding("left click", BindDevice.Keyboard, BindContext.Measure, "pick a measure point"),
            new Binding("double click", BindDevice.Keyboard, BindContext.Default, "GO"),
            new Binding("G", BindDevice.Keyboard, BindContext.HasSelection, "GO"),
            new Binding("F", BindDevice.Keyboard, BindContext.HasSelection, "FOLLOW toggle"),
            new Binding("X", BindDevice.Keyboard, BindContext.HasSelection, "FREE"),
            new Binding(",", BindDevice.Keyboard, BindContext.Any, "time slower"),
            new Binding(".", BindDevice.Keyboard, BindContext.Any, "time faster"),
            new Binding("Space", BindDevice.Keyboard, BindContext.Any, "pause"),
            new Binding("N", BindDevice.Keyboard, BindContext.Any, "Now"),
            new Binding("Tab", BindDevice.Keyboard, BindContext.Any, "show or hide the panel"),
            new Binding("1-5", BindDevice.Keyboard, BindContext.Any, "panel tabs GO, LAYERS, TIME, TOOLS, INFO"),
            new Binding("Page Up", BindDevice.Keyboard, BindContext.Any, "the next rung up the ladder"),
            new Binding("Page Down", BindDevice.Keyboard, BindContext.Any, "the next rung down the ladder"),
            new Binding("M", BindDevice.Keyboard, BindContext.Any, "measure mode"),
            new Binding("R", BindDevice.Keyboard, BindContext.Any, "recentre"),
            // lane:ui BEGIN
            // lane:ui END
            // lane:fiction BEGIN
            // lane:fiction END
            // lane:audio BEGIN
            // lane:audio END
        };

        [Tooltip("Stick displacement past which a direction counts as a deliberate press.")]
        [SerializeField] private float _stickPressThreshold = 0.65f;

        [Tooltip("How long the menu button must be held to recentre.")]
        [SerializeField] private float _menuHoldSeconds = 0.8f;

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
            public bool Menu;
            public float MenuHeld;

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

        /// <summary>Right-handed by default; Left mirrors every L/R pair and moves the watch.</summary>
        public Handedness Handedness { get; set; } = Handedness.Right;

        /// <summary>The hand that points and presses.</summary>
        public Hand Dominant => Handedness == Handedness.Left ? Hand.Left : Hand.Right;

        /// <summary>The hand that wears the watch and flies.</summary>
        public Hand NonDominant => Handedness == Handedness.Left ? Hand.Right : Hand.Left;

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

        /// <summary>Dominant trigger. Select, press or a measure pick, depending on context.</summary>
        public bool SelectPressed { get; private set; }

        /// <summary>Menu, held. Recentre: it was A, which also confirmed.</summary>
        public bool RecenterPressed { get; private set; }

        /// <summary>X on the non-dominant hand: the wrist panel.</summary>
        public bool PanelTogglePressed { get; private set; }

        /// <summary>A on the dominant hand: GO to the selection.</summary>
        public bool GoPressed { get; private set; }

        /// <summary>Non-dominant stick click: FOLLOW / FREE.</summary>
        public bool FollowTogglePressed { get; private set; }

        public bool WarpFasterPressed { get; private set; }

        public bool WarpSlowerPressed { get; private set; }

        /// <summary>Dominant stick click: pause / play.</summary>
        public bool PauseTogglePressed { get; private set; }

        /// <summary>Non-dominant trigger, as an analogue throttle for flight and zoom.</summary>
        public float BoostAxis => State(NonDominant).Trigger;

        /// <summary>A discrete step from a stick: one event per push rather than one per frame.</summary>
        public Vector2Int StickStep(Hand hand)
        {
            ref HandState state = ref State(hand);

            var now = new Vector2(
                Mathf.Abs(state.Stick.x) > _stickPressThreshold ? Mathf.Sign(state.Stick.x) : 0f,
                Mathf.Abs(state.Stick.y) > _stickPressThreshold ? Mathf.Sign(state.Stick.y) : 0f);

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

        private readonly object[] _stickOwners = new object[2];
        private readonly bool[] _panelHot = new bool[2];

        /// <summary>
        /// Claim one stick for something other than flying or zooming.
        ///
        /// Generalises the old single-stick claim to both, because the panel now scrolls with the
        /// dominant stick while the same stick zooms everywhere else.
        /// </summary>
        public bool ClaimStick(Hand hand, object owner)
        {
            int index = (int)hand;
            if (_stickOwners[index] != null && !ReferenceEquals(_stickOwners[index], owner)) return false;
            _stickOwners[index] = owner;
            return true;
        }

        public void ReleaseStick(Hand hand, object owner)
        {
            int index = (int)hand;
            if (ReferenceEquals(_stickOwners[index], owner)) _stickOwners[index] = null;
        }

        public bool StickFree(Hand hand) => _stickOwners[(int)hand] == null;

        /// <summary>Kept for the locomotion path: the non-dominant stick is the flight stick.</summary>
        public bool ClaimNavigationStick(object owner) => ClaimStick(NonDominant, owner);

        public void ReleaseNavigationStick(object owner) => ReleaseStick(NonDominant, owner);

        public bool NavigationStickFree => StickFree(NonDominant);

        /// <summary>
        /// Mark a hand as being on the panel: its pinch, trigger and grip do not select or grab
        /// while the claim holds. The other hand is unaffected.
        /// </summary>
        public void SetPanelHot(Hand hand, bool hot) => _panelHot[(int)hand] = hot;

        public bool IsPanelHot(Hand hand) => _panelHot[(int)hand];

        // ---- Haptics ---------------------------------------------------------

        /// <summary>
        /// A short buzz in one hand. There was none anywhere in the app, and in a headset a
        /// selection that produces no click is hard to tell from one that did not register — so
        /// the user presses again, and a working control comes to feel broken.
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

            Hold(ref _left, Time.unscaledDeltaTime);
            Hold(ref _right, Time.unscaledDeltaTime);

            ref HandState dominant = ref State(Dominant);
            ref HandState other = ref State(NonDominant);

            // Bindings, in one place. Each physical control appears exactly once, and the hand it
            // is on follows Handedness rather than being hard-coded to the right.
            SelectPressed = !IsPanelHot(Dominant) && dominant.Trigger > 0.6f && !dominant.PreviousTriggerDown;
            GoPressed = dominant.Primary && !dominant.PreviousPrimary;
            PanelTogglePressed = other.Primary && !other.PreviousPrimary;
            WarpFasterPressed = dominant.Secondary && !dominant.PreviousSecondary;
            WarpSlowerPressed = other.Secondary && !other.PreviousSecondary;
            PauseTogglePressed = dominant.StickClick && !dominant.PreviousStickClick;
            FollowTogglePressed = other.StickClick && !other.PreviousStickClick;
            RecenterPressed = other.MenuHeld >= _menuHoldSeconds;
            if (RecenterPressed) other.MenuHeld = float.NegativeInfinity;

            Latch(ref _left);
            Latch(ref _right);
        }

        private static void Hold(ref HandState state, float deltaTime)
        {
            if (state.Menu)
            {
                if (float.IsNegativeInfinity(state.MenuHeld)) return;
                state.MenuHeld += deltaTime;
            }
            else
            {
                state.MenuHeld = 0f;
            }
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
                state.Menu = false;
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

            device.TryGetFeatureValue(CommonUsages.menuButton, out bool menu);
            state.Menu = menu;
        }

        /// <summary>The legend for one device, generated from the table the code reads.</summary>
        public static List<string> Legend(BindDevice device)
        {
            var lines = new List<string>(Bindings.Length);
            for (int i = 0; i < Bindings.Length; i++)
            {
                if (Bindings[i].Device != device) continue;
                lines.Add(Bindings[i].Control + " · " + Bindings[i].Action);
            }
            return lines;
        }
    }
}
