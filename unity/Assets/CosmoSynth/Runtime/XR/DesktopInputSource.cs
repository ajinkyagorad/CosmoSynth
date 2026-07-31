using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Keyboard and mouse driving for the desktop build.
    ///
    /// Kept as a separate component that only calls into <see cref="UniverseNavigator"/> and
    /// <see cref="SimulationClock"/>. The navigator itself reads no input, so a controller or
    /// hand-tracking source can replace this one without touching any navigation logic — and
    /// the navigation stays testable, because it has no input dependency to stub out.
    ///
    /// Uses the legacy Input class so the project runs with no package configuration at all.
    /// If Active Input Handling is set to "Input System Package (New)", either switch it to
    /// "Both" or swap this component for an Input System version; nothing else changes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UniverseNavigator))]
    [AddComponentMenu("CosmoSynth/Desktop Input")]
    public sealed class DesktopInputSource : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private float _mouseSensitivity = 2f;
        [SerializeField] private int _lookMouseButton = 1;   // right button

        [Header("Speed")]
        [SerializeField] private float _boostMultiplier = 8f;
        [SerializeField] private float _slowMultiplier = 0.15f;

        private UniverseNavigator _navigator;
        private SimulationClock _clock;

        private void Awake()
        {
            _navigator = GetComponent<UniverseNavigator>();
            _clock = FindAnyObjectByType<SimulationClock>();
        }

        private void Update()
        {
            UpdateMovement();
            UpdateLook();
            UpdateTime();
        }

        /// <summary>
        /// The persistent speed trim, adjusted by the scroll wheel. Held separately from the
        /// navigator's multiplier so that the transient shift/control modifiers can be applied
        /// fresh each frame rather than multiplied into a value that also has to be divided
        /// back out again — an approach that accumulates drift the moment a frame is missed or
        /// a key is released while the window is unfocused.
        /// </summary>
        private float _speedTrim = 1f;

        private void UpdateMovement()
        {
            var move = new Vector3(
                AxisFromKeys(KeyCode.D, KeyCode.A),
                AxisFromKeys(KeyCode.E, KeyCode.Q),
                AxisFromKeys(KeyCode.W, KeyCode.S));

            if (move.sqrMagnitude > 0f) _navigator.CancelTravel();
            _navigator.Move(move);

            // The scroll wheel trims speed multiplicatively, so one notch means the same
            // proportional change whether you are docking or crossing a system.
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _speedTrim = Mathf.Clamp(_speedTrim * Mathf.Pow(1.4f, scroll), 0.001f, 1000f);
            }

            float transient = 1f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) transient *= _boostMultiplier;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) transient *= _slowMultiplier;

            _navigator.SpeedMultiplier = _speedTrim * transient;
        }

        private void UpdateLook()
        {
            if (!Input.GetMouseButton(_lookMouseButton)) return;

            var delta = new Vector2(
                Input.GetAxisRaw("Mouse X") * _mouseSensitivity,
                Input.GetAxisRaw("Mouse Y") * _mouseSensitivity);

            if (delta.sqrMagnitude > 0f)
            {
                _navigator.CancelTravel();
                _navigator.Look(delta);
            }
        }

        private void UpdateTime()
        {
            if (_clock == null) return;

            if (Input.GetKeyDown(KeyCode.Period)) _clock.FasterWarp();
            if (Input.GetKeyDown(KeyCode.Comma)) _clock.SlowerWarp();
            if (Input.GetKeyDown(KeyCode.Space)) _clock.TogglePause();
            if (Input.GetKeyDown(KeyCode.N)) _clock.SetEpochToNow();
        }

        private static float AxisFromKeys(KeyCode positive, KeyCode negative)
        {
            float value = 0f;
            if (Input.GetKey(positive)) value += 1f;
            if (Input.GetKey(negative)) value -= 1f;
            return value;
        }
    }
}
