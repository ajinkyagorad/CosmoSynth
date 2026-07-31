using System.Text;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The provenance inspector, in world space, floating beside the selected body.
    ///
    /// This exists because <c>OnGUI</c> draws nothing at all in VR — IMGUI renders to the screen,
    /// and in a headset there is no screen. The flat HUD is genuinely useful in the editor and
    /// completely invisible on the device, which meant the app's whole scientific-honesty layer
    /// simply did not exist where it matters most.
    ///
    /// Built from TextMesh and a quad rather than a world-space Canvas: no UI package, no event
    /// system, no raycaster to configure, and it works the instant it is instantiated. The
    /// design's curved volumetric surfaces belong here eventually; this is the readable version
    /// that ships.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Spatial Panel")]
    public sealed class SpatialPanel : MonoBehaviour
    {
        [SerializeField] private float _widthMetres = 0.30f;
        [SerializeField] private float _characterSize = 0.0075f;

        private UniverseSimulation _simulation;
        private XrRig _rig;
        private TextMesh _title;
        private TextMesh _body;
        private Transform _backing;
        private Material _backingMaterial;
        private CelestialBodyView _target;

        private static readonly Color Measured = new Color(0.45f, 0.90f, 0.55f);
        private static readonly Color Inferred = new Color(0.55f, 0.78f, 0.98f);
        private static readonly Color Procedural = new Color(0.98f, 0.78f, 0.40f);
        private static readonly Color Unknown = new Color(0.70f, 0.70f, 0.70f);

        private void Awake()
        {
            _simulation = FindAnyObjectByType<UniverseSimulation>();
            _rig = FindAnyObjectByType<XrRig>();
            Build();
            SetVisible(false);
        }

        private void Build()
        {
            // A dark backing quad, so text stays readable against a bright passthrough room.
            // Without it, white text on a white wall is unreadable — the single most common
            // mistake in passthrough UI.
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Backing";
            DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.004f);
            quad.transform.localScale = new Vector3(_widthMetres, _widthMetres * 0.78f, 1f);
            _backing = quad.transform;

            _backingMaterial = CosmoShaders.Material(CosmoShaders.UnlitTransparent);
            _backingMaterial.mainTexture = BuildPanelTexture();
            _backingMaterial.renderQueue = 3200;
            quad.GetComponent<MeshRenderer>().sharedMaterial = _backingMaterial;

            _title = CreateText("Title", new Vector3(-_widthMetres * 0.45f, _widthMetres * 0.33f, 0f),
                _characterSize * 1.5f, TextAnchor.UpperLeft);
            _body = CreateText("Body", new Vector3(-_widthMetres * 0.45f, _widthMetres * 0.24f, 0f),
                _characterSize, TextAnchor.UpperLeft);
        }

        private TextMesh CreateText(string name, Vector3 localPosition, float size, TextAnchor anchor)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = localPosition;

            var text = textObject.AddComponent<TextMesh>();
            text.characterSize = size;
            text.fontSize = 72;         // high, then scaled down — keeps glyphs crisp up close
            text.anchor = anchor;
            text.alignment = TextAlignment.Left;
            text.color = Color.white;

            text.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return text;
        }

        /// <summary>A soft rounded dark panel, generated rather than shipped as an asset.</summary>
        private static Texture2D BuildPanelTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance to the nearest edge, for a soft border.
                    float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) / (size * 0.5f);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge * 6f)) * 0.82f;
                    pixels[y * size + x] = new Color(0.02f, 0.03f, 0.05f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>Show the panel for a body, or hide it when passed null.</summary>
        public void Show(CelestialBodyView view)
        {
            _target = view;
            SetVisible(view != null);
            if (view != null) Refresh();
        }

        private void SetVisible(bool visible)
        {
            if (_backing != null) _backing.gameObject.SetActive(visible);
            if (_title != null) _title.gameObject.SetActive(visible);
            if (_body != null) _body.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (_target == null || _rig?.Camera == null) return;

            Refresh();

            // Sit beside the body, offset toward the viewer's right, and always face them.
            Transform camera = _rig.Camera.transform;
            Vector3 toCamera = camera.position - _target.transform.position;
            float distance = toCamera.magnitude;
            if (distance < 1e-4f) return;

            Vector3 right = Vector3.Cross(Vector3.up, toCamera.normalized).normalized;
            float offset = Mathf.Max(_widthMetres * 0.75f, distance * 0.28f);

            transform.position = _target.transform.position + right * offset;
            transform.rotation = Quaternion.LookRotation(transform.position - camera.position, Vector3.up);

            // Keep a constant apparent size, so the panel is equally readable whether the body is
            // held in your hands or across the room.
            float scale = Mathf.Clamp(distance * 0.9f, 0.35f, 6f);
            transform.localScale = Vector3.one * scale;
        }

        private void Refresh()
        {
            CelestialBody body = _target.Body;
            _title.text = body.Name ?? body.Id;

            var text = new StringBuilder();
            text.AppendLine($"{body.Type}" +
                            (string.IsNullOrEmpty(body.ParentId) ? "" : $"  ·  orbits {body.ParentId}"));
            text.AppendLine();

            Append(text, body, "gravitationalParameter", "mass", $"{body.MassKg:G4} kg");

            if (body.EquatorialRadiusKm.HasValue)
                Append(text, body, "equatorialRadiusKm", "radius", $"{body.EquatorialRadiusKm.Value:N0} km");
            if (body.SurfaceGravity.HasValue)
                Append(text, body, "equatorialRadiusKm", "gravity", $"{body.SurfaceGravity.Value:F2} m/s²");
            if (body.DensityKgPerM3.HasValue)
                Append(text, body, "equatorialRadiusKm", "density", $"{body.DensityKgPerM3.Value:N0} kg/m³");

            if (body.Orbit.HasValue)
            {
                OrbitalElements orbit = body.Orbit.Value;
                text.AppendLine();
                Append(text, body, "orbit", "orbit",
                    $"{orbit.SemiMajorAxisKm / Constants.AstronomicalUnit:F3} AU  e={orbit.Eccentricity:F3}");
                Append(text, body, "orbit", "period", FormatDuration(orbit.PeriodSeconds));
            }

            if (body.Orientation.HasValue)
            {
                Append(text, body, "orientation", "day",
                    FormatDuration(System.Math.Abs(body.Orientation.Value.SiderealPeriodSeconds))
                    + (body.Orientation.Value.IsRetrograde ? " (retrograde)" : ""));
            }

            if (body.Atmosphere != null && body.Atmosphere.IsPresent)
            {
                text.AppendLine();
                Append(text, body, "atmosphere", "surface pressure",
                    $"{body.Atmosphere.SurfacePressureBar:G3} bar");
            }

            text.AppendLine();
            text.AppendLine($"surface: procedural (no measured imagery)");

            _body.text = text.ToString();

            // The title carries the object's weakest claim, so a body whose orbit is measured but
            // whose surface is invented is never presented as simply "measured".
            _title.color = ColorFor(body.Provenance.WeakestClass());
        }

        /// <summary>
        /// Append one field with a provenance marker.
        ///
        /// The marker is a character, not a colour, because a single TextMesh cannot colour parts
        /// of a string — and because colour alone is not readable to everyone, which matters more
        /// in a headset where there is no tooltip to fall back on.
        /// </summary>
        private static void Append(StringBuilder text, CelestialBody body, string field, string label, string value)
        {
            FieldProvenance provenance = body.Provenance.Get(field);
            char marker = provenance.Class switch
            {
                ProvenanceClass.Measured => '=',      // measured
                ProvenanceClass.Inferred => '~',      // derived through a model
                ProvenanceClass.Procedural => '*',    // invented, deterministically
                ProvenanceClass.Fictional => '?',
                _ => '·'
            };
            text.AppendLine($"{marker} {label}: {value}");
        }

        private static Color ColorFor(ProvenanceClass provenanceClass) => provenanceClass switch
        {
            ProvenanceClass.Measured => Measured,
            ProvenanceClass.Inferred => Inferred,
            ProvenanceClass.Procedural => Procedural,
            _ => Unknown
        };

        private static string FormatDuration(double seconds)
        {
            if (double.IsInfinity(seconds)) return "unbound";
            if (seconds < 7200.0) return $"{seconds / 60.0:N1} min";
            if (seconds < 2.0 * Constants.SecondsPerDay) return $"{seconds / 3600.0:N1} hr";
            if (seconds < 2.0 * Constants.SecondsPerJulianYear) return $"{seconds / Constants.SecondsPerDay:N1} days";
            return $"{seconds / Constants.SecondsPerJulianYear:N2} yr";
        }

        private void OnDestroy()
        {
            if (_backingMaterial != null && Application.isPlaying) Destroy(_backingMaterial);
        }
    }
}
