using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>One end of a measurement: a body, a layer point, or the viewer.</summary>
    public readonly struct MeasurePoint
    {
        public readonly string Label;

        /// <summary>Catalogue id when the end moves; null for a fixed point.</summary>
        public readonly string BodyId;

        public readonly Double3 PositionKm;

        /// <summary>"From me": the end follows the viewer's own absolute position.</summary>
        public readonly bool IsViewer;

        public MeasurePoint(string label, string bodyId, Double3 positionKm, bool isViewer = false)
        {
            Label = label;
            BodyId = bodyId;
            PositionKm = positionKm;
            IsViewer = isViewer;
        }

        public static MeasurePoint Viewer() => new MeasurePoint("Me", null, Double3.Zero, true);
    }

    /// <summary>
    /// Pick any two things and read the separation, in the unit the scale calls for.
    ///
    /// <b>Every number comes from absolute double kilometres</b>, never from where anything is
    /// drawn. The rendered positions have been through a logarithmic compression and a per-group
    /// similarity; measuring those would produce a number that changes when the viewer zooms, which
    /// is the one thing a measurement may not do.
    ///
    /// Two draw calls for the whole tool: one mesh for the lines and end dots, one for the labels.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    [AddComponentMenu("CosmoSynth/Measure Tool")]
    public sealed class MeasureTool : MonoBehaviour
    {
        /// <summary>How many finished measurements stay on screen (§5.9.1).</summary>
        public const int Capacity = 3;

        /// <summary>Ribbon width in dmm: screen-constant, so it is a hairline at any distance.</summary>
        public const float RibbonWidthDmm = 4f;

        /// <summary>End dot diameter in dmm.</summary>
        public const float DotDmm = 16f;

        private sealed class Measurement
        {
            public MeasurePoint A, B;
            public bool Complete;
        }

        private readonly List<Measurement> _measurements = new List<Measurement>(Capacity + 1);

        private UniverseSimulation _simulation;
        private ScaleRenderer _renderer;
        private ViewOptions _options;
        private Camera _camera;

        private Mesh _mesh;
        private MeshRenderer _meshRenderer;
        private Material _material;
        private CosmoTextMesh _labels;

        private readonly List<Vector3> _vertices = new List<Vector3>(64);
        private readonly List<Vector2> _uvs = new List<Vector2>(64);
        private readonly List<Color> _colours = new List<Color>(64);
        private readonly List<int> _indices = new List<int>(96);

        /// <summary>While on, the dominant trigger picks points instead of selecting.</summary>
        public bool Active { get; private set; }

        /// <summary>The most recent finished measurement's separation in km, or NaN.</summary>
        public double LastDistanceKm { get; private set; } = double.NaN;

        public void Bind(UniverseSimulation simulation, ScaleRenderer renderer, ViewOptions options, Camera camera)
        {
            _simulation = simulation;
            _renderer = renderer;
            _options = options;
            _camera = camera;
        }

        public void SetActive(bool active)
        {
            Active = active;
            if (!active && _measurements.Count > 0 && !_measurements[_measurements.Count - 1].Complete)
                _measurements.RemoveAt(_measurements.Count - 1);
        }

        public void Toggle() => SetActive(!Active);

        public void Clear()
        {
            _measurements.Clear();
            LastDistanceKm = double.NaN;
        }

        /// <summary>
        /// Add an end. The first starts a measurement, the second finishes it, and a third starts a
        /// new one — the last three stay until Clear.
        /// </summary>
        public void Pick(MeasurePoint point)
        {
            if (_measurements.Count > 0 && !_measurements[_measurements.Count - 1].Complete)
            {
                Measurement open = _measurements[_measurements.Count - 1];
                open.B = point;
                open.Complete = true;
                LastDistanceKm = DistanceKm(open);
                return;
            }

            while (_measurements.Count >= Capacity) _measurements.RemoveAt(0);
            _measurements.Add(new Measurement { A = point, Complete = false });
        }

        /// <summary>Start a measurement from a body, which is what the footer's MEASURE does.</summary>
        public void PickBody(CelestialBodyView view)
        {
            if (view == null) return;
            Pick(new MeasurePoint(view.Body.Name, view.Body.Id, view.AbsolutePositionKm));
        }

        /// <summary>Start or finish a measurement at a layer's pick.</summary>
        public void PickHit(in PickHit hit) =>
            Pick(new MeasurePoint(hit.Label ?? hit.Id, null, hit.PositionKm));

        /// <summary>The label a finished measurement carries.</summary>
        public string Label(int index)
        {
            if (index < 0 || index >= _measurements.Count || !_measurements[index].Complete) return null;
            double km = DistanceKm(_measurements[index]);
            DistanceUnit unit = _options != null ? _options.MeasureUnit : DistanceUnit.Auto;
            string detail = Format.DistanceDetail(km, unit);
            return Format.Distance(km, unit) + (string.IsNullOrEmpty(detail) ? string.Empty : " · " + detail);
        }

        /// <summary>The sub-line: what was measured, and the light time across it.</summary>
        public string SubLabel(int index)
        {
            if (index < 0 || index >= _measurements.Count || !_measurements[index].Complete) return null;
            Measurement m = _measurements[index];
            return m.A.Label + " -> " + m.B.Label + " · light " + Format.LightTime(DistanceKm(m));
        }

        public int Count => _measurements.Count;

        // ---- per frame -------------------------------------------------------

        private void LateUpdate()
        {
            EnsureMesh();

            bool show = _measurements.Count > 0 && (_options == null || !_options.CleanView);
            _meshRenderer.enabled = show;
            _labels.Visible = show;
            if (!show) return;

            _vertices.Clear();
            _uvs.Clear();
            _colours.Clear();
            _indices.Clear();
            _labels.Begin();

            Vector2 solid = CosmoTheme.AtlasFill.center;
            Vector3 eye = _camera != null ? _camera.transform.position : Vector3.zero;

            for (int i = 0; i < _measurements.Count; i++)
            {
                Measurement m = _measurements[i];
                if (!m.Complete) continue;

                Vector3 a = Drawn(m.A);
                Vector3 b = Drawn(m.B);

                AddRibbon(a, b, eye, solid);
                AddDot(a, eye, solid);
                AddDot(b, eye, solid);

                Vector3 mid = 0.5f * (a + b);
                float distance = Mathf.Max(0.2f, Vector3.Distance(mid, eye));
                float body = CosmoText.CharacterSizeFor(CosmoTheme.Metres(CosmoTheme.TypeBody, distance));
                float caption = CosmoText.CharacterSizeFor(CosmoTheme.Metres(CosmoTheme.TypeCaption, distance));

                Vector3 local = transform.InverseTransformPoint(mid);
                _labels.AddCentred(Label(i), local.x, local.y + CosmoTheme.Metres(34f, distance), body,
                    CosmoTheme.TextPrimary);
                _labels.AddCentred(SubLabel(i), local.x, local.y + CosmoTheme.Metres(8f, distance), caption,
                    CosmoTheme.TextSecondary);
            }

            _mesh.Clear();
            if (_vertices.Count > 0)
            {
                _mesh.SetVertices(_vertices);
                _mesh.SetUVs(0, _uvs);
                _mesh.SetColors(_colours);
                _mesh.SetTriangles(_indices, 0, calculateBounds: true);
            }
            _labels.End();

            // The labels are one flat mesh, so they face the viewer as a plane rather than each
            // being aimed: aiming keystones badly off-axis.
            if (_camera != null) _labels.Root.rotation = _camera.transform.rotation;
        }

        // ---- geometry --------------------------------------------------------

        /// <summary>The true separation, in absolute double kilometres.</summary>
        private double DistanceKm(Measurement m) => (AbsoluteKm(m.B) - AbsoluteKm(m.A)).Magnitude;

        private Double3 AbsoluteKm(in MeasurePoint point)
        {
            if (point.IsViewer && _renderer != null) return _renderer.Context.EyeKm;
            if (!string.IsNullOrEmpty(point.BodyId) && _simulation != null &&
                _simulation.Views.TryGetValue(point.BodyId, out CelestialBodyView view))
            {
                return view.AbsolutePositionKm;
            }
            return point.PositionKm;
        }

        /// <summary>
        /// Where an end is <b>drawn</b>, which is not where it is: a body is already placed, and a
        /// free point goes through the same compression the shaders use.
        /// </summary>
        private Vector3 Drawn(in MeasurePoint point)
        {
            if (!string.IsNullOrEmpty(point.BodyId) && _simulation != null &&
                _simulation.Views.TryGetValue(point.BodyId, out CelestialBodyView view))
            {
                return view.transform.position;
            }

            if (_simulation == null || _renderer == null) return transform.position;

            Vector3 universeLocal = _simulation.Origin.ToRender(AbsoluteKm(point));
            Vector3 relative = universeLocal - _renderer.Context.EyeUniverse;
            Double3 compressed = ScaledSpace.CompressRelative(new Double3(relative.x, relative.y, relative.z));
            Vector3 local = _renderer.Context.EyeUniverse +
                            new Vector3((float)compressed.x, (float)compressed.y, (float)compressed.z);
            return _simulation.transform.TransformPoint(local);
        }

        private void AddRibbon(Vector3 a, Vector3 b, Vector3 eye, Vector2 uv)
        {
            Vector3 along = b - a;
            if (along.sqrMagnitude < 1e-10f) return;

            Vector3 toEyeA = eye - a;
            Vector3 side = Vector3.Cross(along, toEyeA);
            if (side.sqrMagnitude < 1e-12f) return;
            side.Normalize();

            float halfA = 0.5f * CosmoTheme.Metres(RibbonWidthDmm, Vector3.Distance(a, eye));
            float halfB = 0.5f * CosmoTheme.Metres(RibbonWidthDmm, Vector3.Distance(b, eye));

            int v = _vertices.Count;
            _vertices.Add(transform.InverseTransformPoint(a - side * halfA));
            _vertices.Add(transform.InverseTransformPoint(a + side * halfA));
            _vertices.Add(transform.InverseTransformPoint(b + side * halfB));
            _vertices.Add(transform.InverseTransformPoint(b - side * halfB));
            for (int i = 0; i < 4; i++) { _uvs.Add(uv); _colours.Add(CosmoTheme.TextPrimary); }
            _indices.Add(v); _indices.Add(v + 1); _indices.Add(v + 2);
            _indices.Add(v); _indices.Add(v + 2); _indices.Add(v + 3);
        }

        private void AddDot(Vector3 at, Vector3 eye, Vector2 uv)
        {
            float half = 0.5f * CosmoTheme.Metres(DotDmm, Vector3.Distance(at, eye));
            Vector3 right = _camera != null ? _camera.transform.right : Vector3.right;
            Vector3 up = _camera != null ? _camera.transform.up : Vector3.up;

            int v = _vertices.Count;
            _vertices.Add(transform.InverseTransformPoint(at - right * half - up * half));
            _vertices.Add(transform.InverseTransformPoint(at - right * half + up * half));
            _vertices.Add(transform.InverseTransformPoint(at + right * half + up * half));
            _vertices.Add(transform.InverseTransformPoint(at + right * half - up * half));
            for (int i = 0; i < 4; i++) { _uvs.Add(uv); _colours.Add(CosmoTheme.Accent); }
            _indices.Add(v); _indices.Add(v + 1); _indices.Add(v + 2);
            _indices.Add(v); _indices.Add(v + 2); _indices.Add(v + 3);
        }

        private void EnsureMesh()
        {
            if (_mesh != null) return;

            _mesh = new Mesh { name = "Measure" };
            _mesh.MarkDynamic();

            var host = new GameObject("Measure Lines");
            host.transform.SetParent(transform, false);
            host.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _meshRenderer = host.AddComponent<MeshRenderer>();
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            _material = CosmoTheme.SurfaceMaterial();
            _meshRenderer.sharedMaterial = _material;

            _labels = new CosmoTextMesh(transform, "Measure Labels");
        }

        /// <summary>Diagnostics for the play-mode report; the harness's <c>measure:</c> view reads it.</summary>
        public string Describe()
        {
            if (_measurements.Count == 0) return Active ? "on, nothing picked" : "off";
            var text = new System.Text.StringBuilder(Active ? "on" : "off");
            for (int i = 0; i < _measurements.Count; i++)
            {
                string label = Label(i);
                text.Append(label == null ? "; (open)" : "; " + SubLabel(i) + " = " + label);
            }
            return text.ToString();
        }

        private void OnDestroy()
        {
            _labels?.Destroy();
            if (!Application.isPlaying) return;
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
