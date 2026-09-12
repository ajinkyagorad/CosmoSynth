using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The crosshair that says "this is the thing you asked for": eight strokes along the corners
    /// of a cube, from 0.55R to R with R = 7.5 cm, in Accent.
    ///
    /// <b>It appears on ARRIVAL and fades six seconds later.</b> The marker this replaces was shown
    /// when the travel started, so on a nine-second flight to M31 it had faded before the viewer
    /// got there — a confirmation that is over by the time there is anything to confirm.
    ///
    /// Open corners rather than a box, because a box outline around a planet reads as a bounding
    /// volume the app has drawn; corner ticks read as a sight. Each stroke is a cross of two thin
    /// quads so it has a silhouette from any direction without needing to face the camera.
    /// </summary>
    public sealed class ArrivalMarker
    {
        /// <summary>Half-diagonal of the sight, in room metres.</summary>
        public const float RadiusMetres = 0.075f;

        /// <summary>Where along the diagonal each stroke starts.</summary>
        public const float InnerFraction = 0.55f;

        /// <summary>How long it stays at full strength before fading.</summary>
        public const float HoldSeconds = 6f;

        /// <summary>How long the fade itself takes.</summary>
        public const float FadeSeconds = 1.2f;

        private readonly Transform _root;
        private readonly MeshRenderer _renderer;
        private readonly Mesh _mesh;
        private readonly Material _material;
        private readonly List<Color> _colours = new List<Color>(256);

        private FloatingOrigin _origin;
        private Double3 _targetKm;
        private float _age = float.PositiveInfinity;

        public ArrivalMarker(Transform parent)
        {
            var host = new GameObject("Arrival Marker");
            host.transform.SetParent(parent, false);
            _root = host.transform;

            _mesh = new Mesh { name = "ArrivalMarker" };
            _mesh.MarkDynamic();
            Build();

            host.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = host.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // The panel shader: premultiplied over, vertex colour, and it writes coverage in
            // proportion to its own alpha. An opaque shader here would punch a hole in passthrough.
            _material = CosmoTheme.SurfaceMaterial();
            _renderer.sharedMaterial = _material;
            _renderer.enabled = false;
        }

        /// <summary>Show the sight at an absolute position, starting the six-second hold.</summary>
        public void ShowAt(Transform host, FloatingOrigin origin, Double3 targetKm)
        {
            if (host != null && _root.parent != host) _root.SetParent(host, false);
            _origin = origin;
            _targetKm = targetKm;
            _age = 0f;
            _renderer.enabled = true;
            Tick(0f);
        }

        public void Hide()
        {
            _age = float.PositiveInfinity;
            _renderer.enabled = false;
        }

        public void Tick(float deltaTime)
        {
            if (float.IsPositiveInfinity(_age)) return;

            _age += deltaTime;
            float opacity = _age <= HoldSeconds
                ? 1f
                : 1f - Mathf.Clamp01((_age - HoldSeconds) / FadeSeconds);

            if (opacity <= 0.001f) { Hide(); return; }

            if (_origin != null) _root.localPosition = _origin.ToRender(_targetKm);
            SetOpacity(opacity);
        }

        public string Describe() =>
            float.IsPositiveInfinity(_age) ? "hidden" : $"shown {_age:F1}s at {_root.localPosition}";

        public void Destroy()
        {
            if (!Application.isPlaying) return;
            if (_material != null) Object.Destroy(_material);
            if (_mesh != null) Object.Destroy(_mesh);
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        private void SetOpacity(float opacity)
        {
            Color colour = CosmoTheme.WithAlpha(CosmoTheme.Accent, opacity);
            for (int i = 0; i < _colours.Count; i++) _colours[i] = colour;
            _mesh.SetColors(_colours);
        }

        private void Build()
        {
            var vertices = new List<Vector3>(128);
            var uvs = new List<Vector2>(128);
            var indices = new List<int>(192);

            // The centre of the atlas's fill tile, where the mask is solid: the stroke's shape is
            // its geometry, not a texture.
            Vector2 solid = new Vector2(CosmoTheme.AtlasFill.center.x, CosmoTheme.AtlasFill.center.y);
            float halfWidth = 0.0016f;

            for (int corner = 0; corner < 8; corner++)
            {
                var direction = new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f).normalized;

                Vector3 a = direction * (RadiusMetres * InnerFraction);
                Vector3 b = direction * RadiusMetres;

                // Two perpendicular thin quads per stroke, so it reads as a stroke from any angle.
                Vector3 side1 = Vector3.Cross(direction, Vector3.up);
                if (side1.sqrMagnitude < 1e-6f) side1 = Vector3.Cross(direction, Vector3.right);
                side1 = side1.normalized * halfWidth;
                Vector3 side2 = Vector3.Cross(direction, side1).normalized * halfWidth;

                AddStroke(vertices, uvs, indices, a, b, side1, solid);
                AddStroke(vertices, uvs, indices, a, b, side2, solid);
            }

            _colours.Clear();
            for (int i = 0; i < vertices.Count; i++) _colours.Add(CosmoTheme.Accent);

            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetUVs(0, uvs);
            _mesh.SetColors(_colours);
            _mesh.SetTriangles(indices, 0, calculateBounds: true);
        }

        private static void AddStroke(List<Vector3> vertices, List<Vector2> uvs, List<int> indices,
            Vector3 a, Vector3 b, Vector3 side, Vector2 uv)
        {
            int v = vertices.Count;
            vertices.Add(a - side);
            vertices.Add(a + side);
            vertices.Add(b + side);
            vertices.Add(b - side);
            uvs.Add(uv); uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
            indices.Add(v); indices.Add(v + 1); indices.Add(v + 2);
            indices.Add(v); indices.Add(v + 2); indices.Add(v + 3);
        }
    }
}
