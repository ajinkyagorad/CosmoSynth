using System;
using System.Collections.Generic;
using System.Text;
using CosmoSynth.Core;
using UnityEngine;
using UnityEngine.XR;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// A panel that rides on the back of the left hand, for navigating from a handheld globe
    /// out to interstellar distances.
    ///
    /// The problem it solves is that the universe spans twenty-two orders of magnitude and a
    /// pinch-and-pull gesture covers maybe two per motion. Getting from Earth to Proxima
    /// Centauri by hand would take dozens of pulls, and there is nothing to aim at on the way —
    /// the destination is invisible until you are nearly there. So the dashboard offers the two
    /// things gesture cannot: a scale ladder with named rungs, and a destination list.
    ///
    /// Wrist-mounted rather than floating, because a panel fixed in the room is somewhere you
    /// have to walk back to, and one that follows your gaze is always in the way. Turning your
    /// wrist over to check something and turning it away to dismiss it is the interaction people
    /// already have with a watch, and it costs no buttons.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Wrist Dashboard")]
    public sealed class WristDashboard : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Offset from the tracked hand, in metres: right, up, forward.")]
        [SerializeField] private Vector3 _wristOffset = new Vector3(0f, 0.045f, -0.02f);

        [Tooltip("Panel width in metres. A watch face, not a monitor.")]
        [SerializeField] private float _widthMetres = 0.13f;

        [Tooltip("Degrees between the palm normal and the gaze below which the panel shows.")]
        [SerializeField] private float _revealAngleDegrees = 55f;

        private UniverseSimulation _simulation;
        private XrRig _rig;
        private TabletopManipulator _manipulator;
        private XrHandInput _input;
        private SimulationClock _clock;

        private Transform _panel;
        private TextMesh _text;
        private Material _backingMaterial;
        private readonly List<InputDevice> _devices = new List<InputDevice>();

        private int _selectedRung;
        private int _selectedDestination;
        private bool _stickHeld;
        private float _visibility;

        /// <summary>
        /// The scale ladder.
        ///
        /// Rungs are named for what you can do at them rather than for a number, because
        /// "1.4e-9 units per kilometre" tells a user nothing and "the whole system on a table"
        /// tells them everything. The metres value is the diameter the reference span occupies
        /// in the room.
        /// </summary>
        private static readonly (string Name, double SpanKm, float RoomMetres)[] ScaleRungs =
        {
            ("Held in hand",        2.0 * Constants.EarthEquatorialRadius,      0.30f),
            ("Room-scale planet",   2.0 * Constants.EarthEquatorialRadius,      2.50f),
            ("Earth and Moon",      2.0 * 384400.0,                             1.60f),
            ("Inner system",        2.0 * 1.6 * Constants.AstronomicalUnit,     2.00f),
            ("Whole system",        2.0 * 31.0 * Constants.AstronomicalUnit,    2.20f),
            ("Nearest stars",       2.0 * 12.0 * Constants.LightYear,           2.50f),
            ("Solar neighbourhood", 2.0 * 130.0 * Constants.LightYear,          3.00f),
        };

        private readonly List<CelestialBodyView> _destinations = new List<CelestialBodyView>();

        private void Awake()
        {
            BuildPanel();
            _visibility = 0f;
        }

        private void BuildPanel()
        {
            var panelObject = new GameObject("Wrist Panel");
            panelObject.transform.SetParent(transform, false);
            _panel = panelObject.transform;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Backing";
            DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(_panel, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            quad.transform.localScale = new Vector3(_widthMetres, _widthMetres * 0.95f, 1f);

            _backingMaterial = CosmoShaders.Material(CosmoShaders.UnlitTransparent);
            _backingMaterial.mainTexture = BuildBackingTexture();
            _backingMaterial.renderQueue = 3200;
            quad.GetComponent<MeshRenderer>().sharedMaterial = _backingMaterial;

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(_panel, false);
            textObject.transform.localPosition = new Vector3(-_widthMetres * 0.44f, _widthMetres * 0.42f, 0f);

            _text = textObject.AddComponent<TextMesh>();
            _text.characterSize = 0.0034f;
            _text.fontSize = 72;
            _text.anchor = TextAnchor.UpperLeft;
            _text.alignment = TextAlignment.Left;
            _text.color = new Color(0.82f, 0.94f, 0.98f);
            _text.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Texture2D BuildBackingTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) / (size * 0.5f);
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge * 7f)) * 0.88f;
                pixels[y * size + x] = new Color(0.015f, 0.03f, 0.055f, alpha);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void Update()
        {
            if (!Resolve()) return;

            FollowWrist();
            PollInput();
            Refresh();
        }

        private bool Resolve()
        {
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();
            if (_rig == null) _rig = FindAnyObjectByType<XrRig>();
            if (_manipulator == null) _manipulator = FindAnyObjectByType<TabletopManipulator>();
            if (_input == null) _input = FindAnyObjectByType<XrHandInput>();
            if (_clock == null) _clock = FindAnyObjectByType<SimulationClock>();
            return _simulation != null && _rig != null;
        }

        /// <summary>
        /// Track the left hand, and fade in only when it is turned toward the face.
        ///
        /// The fade is on visibility rather than on the object being enabled, so the panel does
        /// not blink in and out at the threshold as the wrist wavers — the single most annoying
        /// behaviour a wrist UI can have.
        /// </summary>
        private void FollowWrist()
        {
            if (!TryGetLeftHandPose(out Vector3 position, out Quaternion rotation))
            {
                // No tracked left hand: park it out of the way rather than at the world origin,
                // where it would sit inside whatever the user is looking at.
                _visibility = Mathf.MoveTowards(_visibility, 0f, Time.unscaledDeltaTime * 4f);
                ApplyVisibility();
                return;
            }

            transform.SetPositionAndRotation(position + rotation * _wristOffset, rotation);

            Transform camera = _rig.Camera.transform;
            Vector3 toCamera = (camera.position - transform.position).normalized;

            // The panel's own forward is the wrist's; the angle between it and the gaze says
            // whether the wrist is turned over.
            float angle = Vector3.Angle(transform.forward, toCamera);
            bool revealed = angle < _revealAngleDegrees;

            _visibility = Mathf.MoveTowards(_visibility, revealed ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            ApplyVisibility();

            // Face the reader squarely once revealed, so the text is not read at a slant.
            if (_visibility > 0.01f)
            {
                _panel.rotation = Quaternion.LookRotation(transform.position - camera.position, camera.up);
            }
        }

        private void ApplyVisibility()
        {
            bool visible = _visibility > 0.01f;
            if (_panel.gameObject.activeSelf != visible) _panel.gameObject.SetActive(visible);
            if (!visible) return;

            _panel.localScale = Vector3.one * Mathf.SmoothStep(0.6f, 1f, _visibility);
            Color colour = _text.color;
            _text.color = new Color(colour.r, colour.g, colour.b, _visibility);
        }

        private bool TryGetLeftHandPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller |
                InputDeviceCharacteristics.Left, _devices);

            if (_devices.Count == 0 || !_devices[0].isValid) return false;
            if (!_devices[0].TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 local)) return false;
            _devices[0].TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRotation);

            Transform space = _rig.Origin != null ? _rig.Origin.CameraFloorOffsetObject.transform : null;
            position = space != null ? space.TransformPoint(local) : local;
            rotation = space != null ? space.rotation * localRotation : localRotation;
            return true;
        }

        /// <summary>
        /// Thumbstick on the left hand drives the dashboard: up/down moves the highlight,
        /// left/right switches between the scale ladder and the destination list, click acts.
        /// </summary>
        private void PollInput()
        {
            if (_visibility < 0.5f) return;   // ignore input while the wrist is turned away

            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller |
                InputDeviceCharacteristics.Left, _devices);
            if (_devices.Count == 0) return;

            _devices[0].TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
            _devices[0].TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool click);
            _devices[0].TryGetFeatureValue(CommonUsages.primaryButton, out bool primary);

            bool active = stick.magnitude > 0.6f || click || primary;
            if (active && !_stickHeld)
            {
                if (click || primary) Activate();
                else if (Mathf.Abs(stick.y) > Mathf.Abs(stick.x)) MoveSelection(stick.y > 0 ? -1 : 1);
                else _showingDestinations = !_showingDestinations;
            }
            _stickHeld = active;
        }

        private bool _showingDestinations;

        private void MoveSelection(int delta)
        {
            if (_showingDestinations)
            {
                RefreshDestinations();
                if (_destinations.Count > 0)
                    _selectedDestination = (_selectedDestination + delta + _destinations.Count) % _destinations.Count;
            }
            else
            {
                _selectedRung = Mathf.Clamp(_selectedRung + delta, 0, ScaleRungs.Length - 1);
            }
        }

        private void Activate()
        {
            if (_showingDestinations)
            {
                RefreshDestinations();
                if (_destinations.Count == 0) return;

                CelestialBodyView target = _destinations[Mathf.Clamp(_selectedDestination, 0, _destinations.Count - 1)];
                _simulation.SetFocus(target.Body.Id);
                _simulation.Origin.SetOrigin(target.AbsolutePositionKm);

                // Arrive at a scale where the destination is actually legible, rather than at
                // whatever scale the previous system happened to be viewed at.
                double radiusKm = target.Body.EquatorialRadiusKm ?? Constants.EarthEquatorialRadius;
                _manipulator?.FrameBodyAtSize(target, 0.55f);

                Debug.Log($"[CosmoSynth] travelled to {target.Body.Name} " +
                          $"({target.AbsolutePositionKm.Magnitude / Constants.LightYear:F2} ly from the Sun)");
            }
            else
            {
                (string name, double spanKm, float roomMetres) = ScaleRungs[_selectedRung];
                _simulation.Origin.SetUnitsPerKm(roomMetres / spanKm);
                Debug.Log($"[CosmoSynth] scale set to '{name}' — {roomMetres:F2} m across {spanKm:G4} km");
            }
        }

        /// <summary>
        /// Destinations, nearest first: the focused body's own system, then whatever else is in
        /// the catalogue. Rebuilt on demand rather than cached, because bodies move.
        /// </summary>
        private void RefreshDestinations()
        {
            _destinations.Clear();
            if (_simulation.Catalog == null) return;

            Double3 viewer = _simulation.Origin.OriginKm;

            var candidates = new List<(CelestialBodyView view, double distance)>();
            foreach (CelestialBodyView view in _simulation.Views.Values)
            {
                if (view == null) continue;
                // Stars and planets only — a list of every moon and asteroid is unusable.
                if (view.Body.Type != BodyType.Star && view.Body.Type != BodyType.Planet &&
                    view.Body.Type != BodyType.DwarfPlanet) continue;

                candidates.Add((view, Double3.Distance(viewer, view.AbsolutePositionKm)));
            }

            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < candidates.Count && i < 40; i++) _destinations.Add(candidates[i].view);
        }

        private void Refresh()
        {
            if (_visibility < 0.01f) return;

            var text = new StringBuilder();
            text.AppendLine(_showingDestinations ? "DESTINATIONS" : "SCALE");
            text.AppendLine();

            if (_showingDestinations)
            {
                RefreshDestinations();
                int start = Mathf.Max(0, _selectedDestination - 2);
                for (int i = start; i < _destinations.Count && i < start + 6; i++)
                {
                    CelestialBodyView view = _destinations[i];
                    double distance = Double3.Distance(_simulation.Origin.OriginKm, view.AbsolutePositionKm);
                    string marker = i == _selectedDestination ? ">" : " ";
                    text.AppendLine($"{marker}{Truncate(view.Body.Name, 13),-13} {FormatDistance(distance)}");
                }
                if (_destinations.Count == 0) text.AppendLine(" (loading)");
            }
            else
            {
                for (int i = 0; i < ScaleRungs.Length; i++)
                {
                    string marker = i == _selectedRung ? ">" : " ";
                    text.AppendLine($"{marker}{ScaleRungs[i].Name}");
                }
            }

            text.AppendLine();
            if (_clock != null) text.AppendLine($"time  {_clock.WarpLabel}");
            text.AppendLine($"scale 1km={_simulation.Origin.UnitsPerKm:G2}u");
            text.Append("stick move · click go");

            _text.text = text.ToString();
        }

        private static string Truncate(string text, int length) =>
            string.IsNullOrEmpty(text) ? "" : (text.Length <= length ? text : text.Substring(0, length - 1) + "…");

        private static string FormatDistance(double km)
        {
            if (km < 1e6) return $"{km:N0}km";
            if (km < 0.05 * Constants.LightYear) return $"{km / Constants.AstronomicalUnit:N1}AU";
            return $"{km / Constants.LightYear:N1}ly";
        }

        private void OnDestroy()
        {
            if (_backingMaterial != null && Application.isPlaying) Destroy(_backingMaterial);
        }
    }
}
