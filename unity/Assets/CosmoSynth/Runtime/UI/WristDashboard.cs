using System.Collections.Generic;
using System.Text;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// A panel that rides on the back of the left hand, for navigating from a handheld globe out
    /// to interstellar distances.
    ///
    /// The problem it solves is that the universe spans twenty-two orders of magnitude and a
    /// pinch-and-pull gesture covers maybe two per motion. Getting from Earth to Proxima Centauri
    /// by hand would take dozens of pulls, and there is nothing to aim at on the way — the
    /// destination is invisible until you are nearly there. So the dashboard offers the two things
    /// gesture cannot: a scale ladder with named rungs, and a destination list.
    ///
    /// Wrist-mounted rather than floating, because a panel fixed in the room is somewhere you have
    /// to walk back to, and one that follows your gaze is always in the way. Turning your wrist
    /// over to check something and turning it away to dismiss it is the interaction people already
    /// have with a watch, and it costs no buttons.
    ///
    /// <b>Glance and engage are separate states</b>, which is the fix for the control collision
    /// this panel used to cause. Turning the wrist reveals it; that is all. It claims no input, so
    /// reading it while flying does not also drive it — the previous version acted on the left
    /// stick whenever it was merely visible, so flying with the wrist tilted up scrolled the
    /// destination list and steered at the same time. Pressing X engages it, at which point it
    /// takes the stick from locomotion through <see cref="XrInput.ClaimNavigationStick"/> and says
    /// so by lighting its edge.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Wrist Dashboard")]
    public sealed class WristDashboard : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Offset from the tracked hand, in metres: right, up, forward.")]
        [SerializeField] private Vector3 _wristOffset = new Vector3(0f, 0.055f, -0.02f);

        [Tooltip("Degrees between the palm normal and the gaze below which the panel shows.")]
        [SerializeField] private float _revealAngleDegrees = 55f;

        [Tooltip("Rows of the list shown at once. The list is windowed around the selection.")]
        [SerializeField] private int _visibleRows = 6;

        private UniverseSimulation _simulation;
        private XrRig _rig;
        private TabletopManipulator _manipulator;
        private XrInput _input;
        private SimulationClock _clock;

        private CosmoPanel _panel;
        private int _selectedRung;
        private int _selectedDestination;
        private bool _showingDestinations;
        private bool _engaged;
        private float _visibility;

        /// <summary>Where a rung puts the middle of the view.</summary>
        private enum Centre
        {
            /// <summary>The focused body itself.</summary>
            Focus,

            /// <summary>What the focused body orbits — a planet, for a moon.</summary>
            Primary,

            /// <summary>The star at the top of the focused body's chain.</summary>
            Star
        }

        /// <summary>
        /// The scale ladder.
        ///
        /// Rungs are named for what you can do at them rather than for a number, because
        /// "1.4e-9 units per kilometre" tells a user nothing and "the whole system on a table"
        /// tells them everything. The metres value is the diameter the reference span occupies in
        /// the room.
        ///
        /// Two things were wrong with the table this replaces, and between them they made the top
        /// four rungs useless.
        ///
        /// <b>The sizes were written for a room and the universe hangs 70 cm from your face.</b>
        /// "Whole system" asked for 31 AU across 2.2 m, so the outer planets orbited at 1.1 m
        /// radius about a point closer than that — the user was inside Neptune's orbit, seeing it
        /// as a band across the view rather than as a ring on a table. A span the anchor distance
        /// can actually hold is about 0.7 m, which is a comfortable 50 degrees.
        ///
        /// <b>They changed the scale and left the origin alone.</b> Zooming out to "Whole system"
        /// while anchored on Earth leaves the viewer on Earth — one point on Earth's own orbit —
        /// so the system is seen from inside it and edge-on to the very ellipse the rung is named
        /// after. A rung has to say what is at the middle of the view as well as how wide it is.
        /// </summary>
        private static readonly (string Name, double SpanKm, float RoomMetres, Centre Centre)[] ScaleRungs =
        {
            ("Held in hand", 2.0 * Constants.EarthEquatorialRadius, 0.28f, Centre.Focus),
            ("Room-scale planet", 2.0 * Constants.EarthEquatorialRadius, 2.50f, Centre.Focus),
            ("Planet and moons", 2.0 * 384400.0, 0.55f, Centre.Primary),
            ("Inner system", 2.0 * 1.6 * Constants.AstronomicalUnit, 0.60f, Centre.Star),
            ("Whole system", 2.0 * 31.0 * Constants.AstronomicalUnit, 0.70f, Centre.Star),
            ("Nearest stars", 2.0 * 12.0 * Constants.LightYear, 0.70f, Centre.Star),
            ("Solar neighbourhood", 2.0 * 130.0 * Constants.LightYear, 0.70f, Centre.Star),
        };

        private readonly List<CelestialBodyView> _destinations = new List<CelestialBodyView>();

        private void Awake()
        {
            _panel = new CosmoPanel(transform, "Wrist Panel");
            _panel.Opacity = 0f;
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
            if (_input == null) _input = FindAnyObjectByType<XrInput>();
            if (_clock == null) _clock = FindAnyObjectByType<SimulationClock>();
            return _simulation != null && _rig != null && _input != null;
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
                Dismiss();
                return;
            }

            transform.SetPositionAndRotation(position + rotation * _wristOffset, rotation);

            Transform camera = _rig.Camera.transform;
            Vector3 toCamera = (camera.position - transform.position).normalized;

            // The panel's own forward is the wrist's; the angle between it and the gaze says
            // whether the wrist is turned over. Engaging holds it open, so a deliberate
            // interaction is not cancelled by the small wrist movements the interaction requires.
            float angle = Vector3.Angle(transform.forward, toCamera);
            bool revealed = _engaged || angle < _revealAngleDegrees;

            _visibility = Mathf.MoveTowards(_visibility, revealed ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            _panel.Opacity = Mathf.SmoothStep(0f, 1f, _visibility);

            if (!revealed && _engaged && _visibility <= 0.01f) Disengage();
        }

        private void Dismiss()
        {
            _visibility = Mathf.MoveTowards(_visibility, 0f, Time.unscaledDeltaTime * 4f);
            _panel.Opacity = _visibility;
            if (_visibility <= 0.01f) Disengage();
        }

        private bool TryGetLeftHandPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (!_input.TryGetPose(Hand.Left, out Vector3 local, out Quaternion localRotation)) return false;

            Transform space = _rig.Origin != null ? _rig.Origin.CameraFloorOffsetObject.transform : null;
            position = space != null ? space.TransformPoint(local) : local;
            rotation = space != null ? space.rotation * localRotation : localRotation;
            return true;
        }

        /// <summary>
        /// X engages and dismisses. While engaged the left stick moves the highlight and switches
        /// page, and the right trigger acts — the same trigger that selects a body, because both
        /// mean "the thing I am pointing at, do it".
        /// </summary>
        private void PollInput()
        {
            if (_input.DashboardTogglePressed && _visibility > 0.4f)
            {
                if (_engaged) Disengage();
                else Engage();
            }

            if (!_engaged) return;

            Vector2Int step = _input.StickStep(Hand.Left);

            if (step.y != 0)
            {
                MoveSelection(step.y > 0 ? -1 : 1);
                _input.Pulse(Hand.Left, 0.2f, 0.015f);
            }
            else if (step.x != 0)
            {
                _showingDestinations = !_showingDestinations;
                _input.Pulse(Hand.Left, 0.25f, 0.02f);
            }

            if (_input.SelectPressed) Activate();
        }

        private void Engage()
        {
            if (!_input.ClaimNavigationStick(this)) return;
            _engaged = true;
            _panel.SetActive(true);
            _input.Pulse(Hand.Left, 0.3f, 0.03f);
        }

        private void Disengage()
        {
            if (!_engaged) return;
            _engaged = false;
            _panel.SetActive(false);
            _input.ReleaseNavigationStick(this);
        }

        private void MoveSelection(int delta)
        {
            if (_showingDestinations)
            {
                RefreshDestinations();
                if (_destinations.Count > 0)
                {
                    _selectedDestination = (_selectedDestination + delta + _destinations.Count) % _destinations.Count;
                }
            }
            else
            {
                _selectedRung = Mathf.Clamp(_selectedRung + delta, 0, ScaleRungs.Length - 1);
            }
        }

        private void Activate()
        {
            _input.Pulse(Hand.Right, 0.6f, 0.07f);

            if (_showingDestinations)
            {
                RefreshDestinations();
                if (_destinations.Count == 0) return;

                CelestialBodyView target = _destinations[Mathf.Clamp(_selectedDestination, 0, _destinations.Count - 1)];
                _simulation.SetFocus(target.Body.Id);
                _simulation.Origin.SetOrigin(target.AbsolutePositionKm);

                // Arrive at a scale where the destination is legible, rather than at whatever
                // scale the previous system happened to be viewed at.
                _manipulator?.FrameBodyAtSize(target, 0.55f);

                Debug.Log($"[CosmoSynth] travelled to {target.Body.Name} " +
                          $"({target.AbsolutePositionKm.Magnitude / Constants.LightYear:F2} ly from the Sun)");
            }
            else
            {
                ApplyScaleRung(_selectedRung);
            }
        }

        /// <summary>Number of rungs on the scale ladder.</summary>
        public static int ScaleRungCount => ScaleRungs.Length;

        /// <summary>The name of a rung, for diagnostics and for the harness.</summary>
        public static string ScaleRungName(int index) =>
            ScaleRungs[Mathf.Clamp(index, 0, ScaleRungs.Length - 1)].Name;

        /// <summary>
        /// Jump to a rung on the scale ladder.
        ///
        /// Public so the play-mode harness can exercise the real path rather than reproducing the
        /// arithmetic. A screenshot of a scale the harness computed for itself proves the
        /// harness's arithmetic; only a screenshot of the scale this method sets proves the app's.
        /// </summary>
        public void ApplyScaleRung(int index)
        {
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();
            if (_simulation == null) return;

            (string name, double spanKm, float roomMetres, Centre centre) =
                ScaleRungs[Mathf.Clamp(index, 0, ScaleRungs.Length - 1)];

            _simulation.Origin.SetUnitsPerKm(roomMetres / spanKm);

            CelestialBodyView middle = Resolve(centre);
            if (middle != null) _simulation.Origin.SetOrigin(middle.AbsolutePositionKm);

            Debug.Log($"[CosmoSynth] scale set to '{name}' — {roomMetres:F2} m across {spanKm:G4} km, " +
                      $"centred on {(middle != null ? middle.Body.Name : "the current view")}");
        }

        /// <summary>
        /// The body a rung puts at the middle of the view, walking up the catalogue's parent chain
        /// from the current focus.
        /// </summary>
        private CelestialBodyView Resolve(Centre centre)
        {
            CelestialBodyView focus = _simulation.FocusBody;
            if (focus == null || _simulation.Catalog == null) return focus;
            if (centre == Centre.Focus) return focus;

            CelestialBody body = focus.Body;

            if (centre == Centre.Primary)
            {
                if (string.IsNullOrEmpty(body.ParentId)) return focus;
                return _simulation.Views.TryGetValue(body.ParentId, out CelestialBodyView primary)
                    ? primary
                    : focus;
            }

            // Centre.Star: climb until a star, with a hop limit so a malformed catalogue cannot
            // spin here forever.
            for (int hops = 0; hops < 16 && body != null && !body.IsStar; hops++)
            {
                body = string.IsNullOrEmpty(body.ParentId) ? null : _simulation.Catalog.Get(body.ParentId);
            }

            return body != null && _simulation.Views.TryGetValue(body.Id, out CelestialBodyView star)
                ? star
                : focus;
        }

        /// <summary>
        /// Destinations, nearest first. Rebuilt on demand rather than cached, because bodies move.
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

            Transform camera = _rig.Camera.transform;
            float distance = _panel.FaceViewer(camera);

            var body = new StringBuilder();
            int selectedLine;

            if (_showingDestinations)
            {
                RefreshDestinations();
                selectedLine = AppendDestinations(body);
            }
            else
            {
                selectedLine = AppendRungs(body);
            }

            // The footer is separated by a blank line so it never reads as another list row, and
            // the selection bar can never land on it.
            body.AppendLine();
            body.Append(_clock != null ? $"{_clock.WarpLabel}   " : "");
            body.Append($"1 km = {_simulation.Origin.UnitsPerKm:G2} u");

            if (!_engaged)
            {
                body.AppendLine();
                body.Append("press X to use");
            }

            _panel.SetContent(_showingDestinations ? "DESTINATIONS" : "SCALE", body.ToString(), distance);
            _panel.SetSelectedLine(_engaged ? selectedLine : -1);
        }

        /// <summary>Append the scale ladder; returns the body line the selection sits on.</summary>
        private int AppendRungs(StringBuilder body)
        {
            int first = WindowStart(_selectedRung, ScaleRungs.Length);
            int last = Mathf.Min(ScaleRungs.Length, first + _visibleRows);

            for (int i = first; i < last; i++)
            {
                if (i > first) body.AppendLine();
                body.Append(ScaleRungs[i].Name);
            }
            return _selectedRung - first;
        }

        /// <summary>Append the destination list; returns the body line the selection sits on.</summary>
        private int AppendDestinations(StringBuilder body)
        {
            if (_destinations.Count == 0)
            {
                body.Append("loading…");
                return -1;
            }

            int first = WindowStart(_selectedDestination, _destinations.Count);
            int last = Mathf.Min(_destinations.Count, first + _visibleRows);

            for (int i = first; i < last; i++)
            {
                CelestialBodyView view = _destinations[i];
                double distance = Double3.Distance(_simulation.Origin.OriginKm, view.AbsolutePositionKm);
                if (i > first) body.AppendLine();
                body.Append($"{view.Body.Name}  ·  {FormatDistance(distance)}");
            }
            return _selectedDestination - first;
        }

        /// <summary>
        /// Keep the selection inside the visible window, and keep the window inside the list — so
        /// a selection near either end does not leave the panel half empty.
        /// </summary>
        private int WindowStart(int selected, int count)
        {
            if (count <= _visibleRows) return 0;
            return Mathf.Clamp(selected - _visibleRows / 2, 0, count - _visibleRows);
        }

        private static string FormatDistance(double km)
        {
            if (km < 1e6) return $"{km:N0} km";
            if (km < 0.05 * Constants.LightYear) return $"{km / Constants.AstronomicalUnit:N1} AU";
            return $"{km / Constants.LightYear:N1} ly";
        }

        private void OnDestroy()
        {
            _input?.ReleaseNavigationStick(this);
            _panel?.Destroy();
        }
    }
}
