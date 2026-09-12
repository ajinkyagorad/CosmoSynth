using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Keyboard and mouse, for the flat editor and for the headless harness.
    ///
    /// Uses the legacy <c>Input</c> class so the project runs with no package configuration at all;
    /// <c>XrSetup</c> forces Active Input Handling to "Both" for exactly this reason.
    ///
    /// Every key here has a row in <see cref="XrInput.Bindings"/> with
    /// <c>BindDevice.Keyboard</c>, and the HELP page is generated from those rows — so the legend
    /// cannot drift from the code the way the hand-written one had.
    ///
    /// <b>The wheel zooms.</b> It used to trim flight speed, which is a control nobody looks for
    /// and nobody finds: every map, every image viewer and every 3D tool made in the last thirty
    /// years zooms on the wheel.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Desktop Input")]
    public sealed class DesktopInputSource : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private float _mouseSensitivity = 2f;
        [SerializeField] private int _lookMouseButton = 1;   // right button

        [Header("Speed")]
        [SerializeField] private float _boostMultiplier = 8f;
        [SerializeField] private float _slowMultiplier = 0.15f;

        [Tooltip("Two clicks within this many seconds are a double click, which is GO.")]
        [SerializeField] private float _doubleClickSeconds = 0.35f;

        private UniverseNavigator _navigator;
        private SimulationClock _clock;
        private ScaleControl _scale;
        private TravelController _travel;
        private MeasureTool _measure;
        private BodySelector _selector;
        private WristPanel _panel;
        private DestinationIndex _destinations;
        private CosmoSynthBootstrap _bootstrap;

        private float _lastClickTime = -10f;

        private void Awake()
        {
            _navigator = GetComponent<UniverseNavigator>();
            _clock = FindAnyObjectByType<SimulationClock>();
            _scale = FindAnyObjectByType<ScaleControl>();
            _travel = FindAnyObjectByType<TravelController>();
            _measure = FindAnyObjectByType<MeasureTool>();
            _selector = FindAnyObjectByType<BodySelector>();
            _panel = FindAnyObjectByType<WristPanel>();
            _bootstrap = FindAnyObjectByType<CosmoSynthBootstrap>();
            _destinations = _bootstrap != null ? _bootstrap.Destinations : null;
        }

        private void Update()
        {
            UpdateMovement();
            UpdateLook();
            UpdateTime();
            UpdateView();
        }

        private void UpdateMovement()
        {
            if (_navigator == null) return;

            var move = new Vector3(
                AxisFromKeys(KeyCode.D, KeyCode.A),
                AxisFromKeys(KeyCode.E, KeyCode.Q),
                AxisFromKeys(KeyCode.W, KeyCode.S));

            if (move.sqrMagnitude > 0f) _travel?.Cancel("desktop flight");
            _navigator.Move(move);

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f) _scale?.Notch(scroll);

            float transient = 1f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) transient *= _boostMultiplier;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) transient *= _slowMultiplier;

            _navigator.SpeedMultiplier = transient;
        }

        private void UpdateLook()
        {
            if (_navigator == null || !Input.GetMouseButton(_lookMouseButton)) return;

            var delta = new Vector2(
                Input.GetAxisRaw("Mouse X") * _mouseSensitivity,
                Input.GetAxisRaw("Mouse Y") * _mouseSensitivity);

            if (delta.sqrMagnitude <= 0f) return;
            _travel?.Cancel("desktop look");
            _navigator.Look(delta);
        }

        private void UpdateTime()
        {
            if (_clock == null) return;

            if (Input.GetKeyDown(KeyCode.Period)) _clock.FasterWarp();
            if (Input.GetKeyDown(KeyCode.Comma)) _clock.SlowerWarp();
            if (Input.GetKeyDown(KeyCode.Space)) _clock.TogglePause();
            if (Input.GetKeyDown(KeyCode.N))
            {
                _clock.SetEpochToNow();
                _clock.WarpIndex = WarpLadder.RealTimeIndex;
            }
        }

        private void UpdateView()
        {
            if (Input.GetKeyDown(KeyCode.PageUp)) _scale?.StepRung(1);
            if (Input.GetKeyDown(KeyCode.PageDown)) _scale?.StepRung(-1);
            if (Input.GetKeyDown(KeyCode.M)) _measure?.Toggle();
            if (Input.GetKeyDown(KeyCode.R)) _bootstrap?.Recenter();
            if (Input.GetKeyDown(KeyCode.Tab)) _panel?.Toggle();

            for (int i = 0; i < 5; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
                if (_panel == null) break;
                if (!_panel.IsOpen) _panel.Toggle();
                _panel.Model?.SetTab((PanelModel.Tab)i);
            }

            CelestialBodyView selected = _selector != null ? _selector.Selected : null;
            if (selected != null)
            {
                if (Input.GetKeyDown(KeyCode.G)) GoTo(selected);
                if (Input.GetKeyDown(KeyCode.F)) _travel?.ToggleFollow(selected.Body.Id);
                if (Input.GetKeyDown(KeyCode.X)) _travel?.SetFree();
            }

            if (!Input.GetMouseButtonDown(0)) return;

            // A double click is GO. The single click is the selector's own, so this only has to
            // notice the second one.
            if (Time.unscaledTime - _lastClickTime < _doubleClickSeconds && selected != null) GoTo(selected);
            _lastClickTime = Time.unscaledTime;
        }

        private void GoTo(CelestialBodyView view)
        {
            if (_destinations == null) _destinations = _bootstrap != null ? _bootstrap.Destinations : null;
            Destination destination = _destinations?.Find("body:" + view.Body.Id);
            if (destination != null) _travel?.Travel(destination);
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
