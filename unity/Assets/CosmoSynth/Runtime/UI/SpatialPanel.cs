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
    /// did not exist where it matters most.
    ///
    /// Two things were wrong with the version this replaces, and both came from sizing the
    /// container independently of its contents. Its text was set to a 7.5 cm line height on a
    /// 23 cm panel and then given twelve lines to draw — four times what fits — so the readable
    /// part was whatever happened to fall inside the backing quad. And it held apparent size by
    /// scaling the whole object with <c>Clamp(distance * 0.9, 0.35, 6)</c>, where the lower clamp
    /// bites at exactly the tabletop distances the app is normally used at: closer than 40 cm the
    /// panel stopped shrinking, so the nearer you brought a planet the larger its label became
    /// relative to it. <see cref="CosmoPanel"/> sizes type angularly and the panel to its text, so
    /// neither is possible.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    [AddComponentMenu("CosmoSynth/Spatial Panel")]
    public sealed class SpatialPanel : MonoBehaviour
    {
        private XrRig _rig;
        private CosmoPanel _panel;
        private CelestialBodyView _target;

        private void Awake()
        {
            _rig = FindAnyObjectByType<XrRig>();
            _panel = new CosmoPanel(transform, "Provenance Panel");
            _panel.Opacity = 0f;
        }

        /// <summary>Show the panel for a body, or hide it when passed null.</summary>
        public void Show(CelestialBodyView view)
        {
            _target = view;
            _panel.Opacity = view != null ? 1f : 0f;
        }

        private void LateUpdate()
        {
            if (_target == null || _rig?.Camera == null) return;

            // A panel offset from a body's limb has to know the limb is there. IsGeometryDrawn,
            // re-read every frame: a selection made while a body was visible outlives the body,
            // and RenderedRadius does not go to zero when the geometry does.
            if (!_target.IsPointable)
            {
                _panel.Opacity = 0f;
                return;
            }

            // Sit beside the body, clear of its limb, and always face the viewer.
            Transform camera = _rig.Camera.transform;
            Vector3 toCamera = camera.position - _target.transform.position;
            float distance = toCamera.magnitude;
            if (distance < 1e-4f) return;

            Vector3 right = Vector3.Cross(Vector3.up, toCamera.normalized).normalized;

            // Clear the body's drawn radius, not a fixed fraction of the view distance. A gas
            // giant filling the room and a two-pixel moon need very different offsets, and the
            // old rule — 28% of the distance to the camera — knew about neither.
            float clearance = _target.RenderedRadius + _panel.Size.x * 0.5f +
                              CosmoTheme.Metres(CosmoTheme.SpacePanelPadding, distance);

            transform.position = _target.transform.position + right * clearance;

            float panelDistance = _panel.FaceViewer(camera);
            Refresh(panelDistance);
        }

        private void Refresh(float viewingDistance)
        {
            CelestialBody body = _target.Body;

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
                Append(text, body, "atmosphere", "surface pressure", $"{body.Atmosphere.SurfacePressureBar:G3} bar");
            }

            text.AppendLine();
            text.Append("* surface: procedural, no measured imagery");

            _panel.SetContent(body.Name ?? body.Id, text.ToString(), viewingDistance);

            // The title carries the object's weakest claim, so a body whose orbit is measured but
            // whose surface is invented is never presented as simply "measured".
            _panel.SetTitleColour(CosmoTheme.Provenance(body.Provenance.WeakestClass()));
        }

        /// <summary>
        /// Append one field with a provenance marker.
        ///
        /// The marker is a character, not a colour. A single TextMesh cannot colour parts of a
        /// string — and colour alone is not readable to everyone, which matters more in a headset
        /// where there is no tooltip to fall back on.
        /// </summary>
        private static void Append(StringBuilder text, CelestialBody body, string field, string label, string value)
        {
            FieldProvenance provenance = body.Provenance.Get(field);
            text.AppendLine($"{CosmoTheme.ProvenanceGlyph(provenance.Class)} {label}: {value}");
        }

        private static string FormatDuration(double seconds)
        {
            if (double.IsInfinity(seconds)) return "unbound";
            if (seconds < 7200.0) return $"{seconds / 60.0:N1} min";
            if (seconds < 2.0 * Constants.SecondsPerDay) return $"{seconds / 3600.0:N1} hr";
            if (seconds < 2.0 * Constants.SecondsPerJulianYear) return $"{seconds / Constants.SecondsPerDay:N1} days";
            return $"{seconds / Constants.SecondsPerJulianYear:N2} yr";
        }

        private void OnDestroy() => _panel?.Destroy();
    }
}
