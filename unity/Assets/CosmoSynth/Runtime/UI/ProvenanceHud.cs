using System.Collections.Generic;
using System.Text;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The data-provenance inspector, plus a compact status readout.
    ///
    /// This is the piece that separates the project from a pretty space renderer. For the
    /// selected body it shows, field by field, whether a value was measured, inferred from
    /// measurements, generated procedurally or invented — and names the dataset. A viewer can
    /// always answer "how do you know that?", which is exactly the question a scientifically
    /// honest simulator has to be able to survive.
    ///
    /// Drawn with IMGUI deliberately: it needs no scene setup, no canvas, no package and no
    /// prefab, so it works the moment the project opens. The volumetric spatial version
    /// described in the design belongs in the XR layer; this is the flat fallback and the
    /// developer's window onto the data.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Provenance HUD")]
    public sealed class ProvenanceHud : MonoBehaviour
    {
        [SerializeField] private UniverseSimulation _simulation;
        [SerializeField] private UniverseNavigator _navigator;
        [SerializeField] private bool _visible = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.Tab;

        private string _selectedId;
        private Vector2 _bodyListScroll;
        private Vector2 _detailScroll;
        private GUIStyle _headerStyle;
        private GUIStyle _monoStyle;
        private GUIStyle _panelStyle;
        private Texture2D _panelBackground;

        private void Update()
        {
            // Resolved here rather than in Awake: the navigator is created later, after the
            // catalogue loads, so an Awake-time lookup caches null and never recovers.
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();
            if (_navigator == null) _navigator = FindAnyObjectByType<UniverseNavigator>();

            if (Input.GetKeyDown(_toggleKey)) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible || _simulation?.Catalog == null) return;

            EnsureStyles();

            GUILayout.BeginArea(new Rect(16, 16, 340, Screen.height - 32), _panelStyle);
            DrawStatus();
            GUILayout.Space(10);
            DrawBodyList();
            GUILayout.EndArea();

            if (!string.IsNullOrEmpty(_selectedId))
            {
                GUILayout.BeginArea(new Rect(Screen.width - 452, 16, 436, Screen.height - 32), _panelStyle);
                DrawDetail(_simulation.Catalog.Get(_selectedId));
                GUILayout.EndArea();
            }
        }

        /// <summary>
        /// Styles matching the world-space surfaces.
        ///
        /// The default <c>GUI.skin.box</c> is the grey Unity chrome, which against a black sky is
        /// the loudest thing on screen and belongs to a different application than everything
        /// else here. Colours come from <see cref="CosmoTheme"/>, so this and the headset panels
        /// cannot drift apart the way the two provenance tables had.
        /// </summary>
        private void EnsureStyles()
        {
            if (_headerStyle != null) return;

            _panelBackground = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _panelBackground.SetPixel(0, 0, CosmoTheme.Surface);
            _panelBackground.Apply();

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _panelBackground },
                padding = new RectOffset(14, 14, 12, 12)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                normal = { textColor = CosmoTheme.TextPrimary }
            };

            _monoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = CosmoTheme.TextSecondary }
            };
        }

        private void OnDestroy()
        {
            if (_panelBackground != null && Application.isPlaying) Destroy(_panelBackground);
        }

        private void DrawStatus()
        {
            GUILayout.Label("COSMOSYNTH", _headerStyle);

            SimulationClock clock = _simulation.Clock;
            GUILayout.Label($"{clock.Now.ToIso8601()}   [{clock.WarpLabel}]", _monoStyle);

            if (_navigator != null)
            {
                string nearest = _navigator.NearestBody != null ? _navigator.NearestBody.Body.Name : "-";
                GUILayout.Label($"near: {nearest}   {FormatDistance(_navigator.NearestSurfaceKm)}", _monoStyle);
                GUILayout.Label($"speed: {FormatSpeed(_navigator.CurrentSpeedKmPerSec * _navigator.SpeedMultiplier)}",
                    _monoStyle);
            }

            GUILayout.Label($"scale: 1 km = {_simulation.Origin.UnitsPerKm:G3} units", _monoStyle);

            // If sizes are not physically true, say so. A viewer who cannot tell whether they
            // are looking at real proportions has been shown a diagram, not a measurement.
            if (_simulation.IsPresentationScaled)
            {
                Color previous = GUI.color;
                GUI.color = CosmoTheme.Warning;
                GUILayout.Label("SIZES NOT TO SCALE (presentation mode)", _monoStyle);
                GUI.color = previous;
            }
        }

        private void DrawBodyList()
        {
            GUILayout.Label("Bodies", _headerStyle);
            _bodyListScroll = GUILayout.BeginScrollView(_bodyListScroll);

            foreach (var body in _simulation.Catalog.Traverse())
            {
                int depth = Depth(body);
                GUILayout.BeginHorizontal();
                GUILayout.Space(depth * 12);

                Color previous = GUI.color;
                GUI.color = CosmoTheme.Provenance(body.Provenance.WeakestClass());

                if (GUILayout.Button(body.Name ?? body.Id, GUI.skin.label))
                {
                    _selectedId = body.Id;
                    if (_navigator != null && _simulation.Views.TryGetValue(body.Id, out var view))
                    {
                        _navigator.TravelTo(view);
                    }
                }

                GUI.color = previous;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private int Depth(CelestialBody body)
        {
            int depth = 0;
            var current = body;
            while (current != null && !string.IsNullOrEmpty(current.ParentId) && depth < 16)
            {
                current = _simulation.Catalog.Get(current.ParentId);
                depth++;
            }
            return depth;
        }

        private void DrawDetail(CelestialBody body)
        {
            if (body == null) return;

            GUILayout.Label(body.Name ?? body.Id, _headerStyle);
            GUILayout.Label($"{body.Type}" +
                            (string.IsNullOrEmpty(body.ParentId) ? "" : $" orbiting {body.ParentId}"), _monoStyle);

            _detailScroll = GUILayout.BeginScrollView(_detailScroll);

            DrawField(body, "gravitationalParameter", "GM", $"{body.GravitationalParameter:G8} km^3/s^2");
            DrawField(body, "gravitationalParameter", "mass", $"{body.MassKg:G6} kg");

            if (body.EquatorialRadiusKm.HasValue)
            {
                DrawField(body, "equatorialRadiusKm", "equatorial radius", $"{body.EquatorialRadiusKm.Value:N1} km");
            }
            if (body.PolarRadiusKm.HasValue)
            {
                DrawField(body, "polarRadiusKm", "polar radius", $"{body.PolarRadiusKm.Value:N1} km");
            }
            if (body.DensityKgPerM3.HasValue)
            {
                DrawField(body, "equatorialRadiusKm", "mean density", $"{body.DensityKgPerM3.Value:N0} kg/m^3");
            }
            if (body.SurfaceGravity.HasValue)
            {
                DrawField(body, "equatorialRadiusKm", "surface gravity", $"{body.SurfaceGravity.Value:N2} m/s^2");
            }
            if (body.EscapeSpeed.HasValue)
            {
                DrawField(body, "equatorialRadiusKm", "escape speed", $"{body.EscapeSpeed.Value:N2} km/s");
            }
            if (body.BondAlbedo.HasValue)
            {
                DrawField(body, "bondAlbedo", "Bond albedo", $"{body.BondAlbedo.Value:F3}");
            }

            if (body.Orbit.HasValue)
            {
                GUILayout.Space(4);
                GUILayout.Label("Orbit", _headerStyle);
                var orbit = body.Orbit.Value;
                DrawField(body, "orbit", "semi-major axis",
                    $"{orbit.SemiMajorAxisKm:N0} km  ({orbit.SemiMajorAxisKm / Constants.AstronomicalUnit:F4} AU)");
                DrawField(body, "orbit", "eccentricity", $"{orbit.Eccentricity:F6}");
                DrawField(body, "orbit", "inclination", $"{orbit.InclinationRad * MathD.Rad2Deg:F4} deg");
                DrawField(body, "orbit", "period", FormatDuration(orbit.PeriodSeconds));
                DrawField(body, "orbit", "periapsis", $"{orbit.PeriapsisKm:N0} km");
                DrawField(body, "orbit", "apoapsis", $"{orbit.ApoapsisKm:N0} km");
            }

            if (body.Orientation.HasValue)
            {
                GUILayout.Space(4);
                GUILayout.Label("Rotation", _headerStyle);
                DrawField(body, "orientation", "sidereal day",
                    FormatDuration(System.Math.Abs(body.Orientation.Value.SiderealPeriodSeconds))
                    + (body.Orientation.Value.IsRetrograde ? " (retrograde)" : ""));
            }

            if (body.Atmosphere != null && body.Atmosphere.IsPresent)
            {
                GUILayout.Space(4);
                GUILayout.Label("Atmosphere", _headerStyle);
                DrawField(body, "atmosphere", "surface pressure", $"{body.Atmosphere.SurfacePressureBar:G4} bar");
                DrawField(body, "atmosphere", "scale height", $"{body.Atmosphere.ScaleHeightKm:F1} km");

                var composition = new StringBuilder();
                foreach (var species in body.Atmosphere.Composition)
                {
                    if (composition.Length > 0) composition.Append(", ");
                    composition.Append($"{species.Key} {species.Value:P1}");
                }
                if (composition.Length > 0) DrawField(body, "atmosphere", "composition", composition.ToString());
            }

            GUILayout.Space(8);
            GUILayout.Label("Provenance summary", _headerStyle);
            foreach (var entry in body.Provenance.Summary())
            {
                Color previous = GUI.color;
                GUI.color = CosmoTheme.Provenance(entry.Key);
                GUILayout.Label($"  {entry.Key}: {entry.Value} field(s)", _monoStyle);
                GUI.color = previous;
            }

            GUILayout.EndScrollView();
        }

        /// <summary>Draw one value with its provenance colour and source.</summary>
        private void DrawField(CelestialBody body, string provenanceKey, string label, string value)
        {
            FieldProvenance provenance = body.Provenance.Get(provenanceKey);

            Color previous = GUI.color;
            GUI.color = CosmoTheme.Provenance(provenance.Class);

            GUILayout.Label($"{label}: {value}", _monoStyle);

            string detail = provenance.Class.ToString().ToLowerInvariant();
            if (provenance.HasUncertainty) detail += $" +/-{provenance.Uncertainty:G3}";
            if (!string.IsNullOrEmpty(provenance.Source)) detail += $" — {provenance.Source}";

            GUI.color = previous * 0.75f;
            GUILayout.Label($"    {detail}", _monoStyle);
            GUI.color = previous;
        }

        private static string FormatDistance(double km)
        {
            if (double.IsInfinity(km)) return "-";
            if (km < 1.0) return $"{km * 1000.0:N0} m";
            if (km < 1e6) return $"{km:N0} km";
            if (km < Constants.AstronomicalUnit * 0.1) return $"{km:G4} km";
            if (km < Constants.LightYear) return $"{km / Constants.AstronomicalUnit:N2} AU";
            return $"{km / Constants.LightYear:N2} ly";
        }

        private static string FormatSpeed(double kmPerSecond)
        {
            if (kmPerSecond < 0.001) return $"{kmPerSecond * 1e6:N0} mm/s";
            if (kmPerSecond < 1.0) return $"{kmPerSecond * 1000.0:N1} m/s";
            if (kmPerSecond < Constants.SpeedOfLight) return $"{kmPerSecond:N1} km/s";
            return $"{kmPerSecond / Constants.SpeedOfLight:N1}c";
        }

        private static string FormatDuration(double seconds)
        {
            if (double.IsInfinity(seconds)) return "unbound";
            if (seconds < 120.0) return $"{seconds:N1} s";
            if (seconds < 7200.0) return $"{seconds / 60.0:N1} min";
            if (seconds < 2.0 * Constants.SecondsPerDay) return $"{seconds / 3600.0:N2} hr";
            if (seconds < 2.0 * Constants.SecondsPerJulianYear) return $"{seconds / Constants.SecondsPerDay:N2} days";
            return $"{seconds / Constants.SecondsPerJulianYear:N2} yr";
        }
    }
}
