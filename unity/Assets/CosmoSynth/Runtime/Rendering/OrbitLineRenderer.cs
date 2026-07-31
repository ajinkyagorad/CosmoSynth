using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Draws a body's orbit as a closed line around its parent.
    ///
    /// Two details make this less trivial than it looks:
    ///
    /// 1. The path is sampled uniformly in eccentric anomaly, not true anomaly. Uniform true
    ///    anomaly starves the apoapsis half of an eccentric orbit of vertices while piling them
    ///    up uselessly at periapsis, which makes a comet's orbit render as a polygon at the far
    ///    end. <see cref="OrbitalElements.SampleOrbitPath"/> handles that.
    ///
    /// 2. The vertices are regenerated whenever the floating origin shifts. A LineRenderer in
    ///    world space holds its own copy of the positions, which the origin shift does not move
    ///    — so without <see cref="IFloatingOriginListener"/> the orbit would tear away from its
    ///    planet the first time the camera travelled far enough to trigger a rebase.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    [AddComponentMenu("CosmoSynth/Orbit Line")]
    public sealed class OrbitLineRenderer : MonoBehaviour, IFloatingOriginListener
    {
        [SerializeField] private int _sampleCount = 256;
        [SerializeField] private float _widthPixels = 1.1f;
        [SerializeField] private Color _color = new Color(0.45f, 0.62f, 0.85f, 0.55f);

        private UniverseSimulation _simulation;
        private CelestialBodyView _view;
        private LineRenderer _line;
        private List<Double3> _pathRelativeToParentKm;
        private Vector3[] _vertices;

        public void Initialise(UniverseSimulation simulation, CelestialBodyView view)
        {
            _simulation = simulation;
            _view = view;

            _line = GetComponent<LineRenderer>();
            // Local space, matching CelestialBodyView: the line object sits at local zero under
            // the simulation transform, so its vertices share the bodies' coordinate space and
            // the whole universe moves as one when the anchor is placed in the room.
            _line.useWorldSpace = false;
            _line.loop = false;
            _line.startColor = _color;
            _line.endColor = _color;
            _line.material = CosmoShaders.Material(CosmoShaders.SpritesDefault);
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            Rebuild();
            simulation.Origin.Register(this);
        }

        /// <summary>Resample the conic. Only needed when the orbit itself changes, not per frame.</summary>
        public void Rebuild()
        {
            if (_view?.Body?.Orbit == null || !_view.Body.Orbit.HasValue)
            {
                if (_line != null) _line.positionCount = 0;
                return;
            }

            _pathRelativeToParentKm = _view.Body.Orbit.Value.SampleOrbitPath(_sampleCount);
            _vertices = new Vector3[_pathRelativeToParentKm.Count];
            _line.positionCount = _pathRelativeToParentKm.Count;

            // Tint the path with the body's own colour and fade it away from the body.
            //
            // A uniform blue line for every orbit reads as a wireframe diagram laid over the
            // scene — it fights the planets for attention and tells you nothing about which
            // path belongs to which world. Taking the hue from the body makes the association
            // immediate, and fading the trailing half turns a hard ellipse into something that
            // reads as motion rather than as a drawn shape.
            var colour = new Color(
                Mathf.Lerp(0.55f, (float)_view.Body.DisplayColor.x, 0.75f),
                Mathf.Lerp(0.62f, (float)_view.Body.DisplayColor.y, 0.75f),
                Mathf.Lerp(0.78f, (float)_view.Body.DisplayColor.z, 0.75f));

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(colour, 0f),
                    new GradientColorKey(colour, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.06f, 0f),
                    new GradientAlphaKey(0.55f, 0.55f),
                    new GradientAlphaKey(0.85f, 0.98f),
                    new GradientAlphaKey(0.06f, 1f),
                });
            _line.colorGradient = gradient;

            // Smooth the polyline so an eccentric orbit does not read as a polygon.
            _line.numCornerVertices = 2;
            _line.numCapVertices = 2;
        }

        private void LateUpdate()
        {
            if (_pathRelativeToParentKm == null || _simulation == null) return;

            // The sampled conic is expressed relative to the parent, so it has to be re-anchored
            // to wherever the parent is right now.
            var parentFrame = _view.Frame?.Parent;
            if (parentFrame == null) return;

            Double3 parentAbsolute = parentFrame.AbsoluteState(_simulation.Clock.Now).Position;
            FloatingOrigin origin = _simulation.Origin;

            for (int i = 0; i < _pathRelativeToParentKm.Count; i++)
            {
                _vertices[i] = origin.ToRender(parentAbsolute + _pathRelativeToParentKm[i]);
            }

            // Hide orbits far too large to read at the current framing.
            //
            // Standing beside Earth, Halley's ellipse still crosses the view — but it is a
            // quarter of a million render units across, so all you can see of it is one edge
            // sweeping the screen as a straight band. That is not information, it is clutter
            // that looks like a rendering fault. An orbit is only worth drawing when a useful
            // part of it fits in the view.
            Camera viewCamera = Camera.main;
            if (viewCamera != null)
            {
                float orbitExtent = Vector3.Distance(
                    transform.TransformPoint(_vertices[0]),
                    transform.TransformPoint(_vertices[_vertices.Length / 2]));
                float viewDistance = Vector3.Distance(viewCamera.transform.position,
                    transform.TransformPoint(_vertices[0]));

                bool legible = orbitExtent < Mathf.Max(0.01f, viewDistance) * 6f;
                if (_line.enabled != legible) _line.enabled = legible;
                if (!legible) return;
            }

            _line.SetPositions(_vertices);

            // Constant apparent width: an orbit line is a diagram, and a diagram that becomes
            // invisible when you zoom out has failed at its job.
            Camera camera = Camera.main;
            if (camera != null)
            {
                // Distance to the CLOSEST part of the orbit, sampled coarsely.
                //
                // A LineRenderer has one width for the whole line, so sizing it from a single
                // vertex is wrong for any orbit large compared to its distance: pick a far
                // vertex and the near half of the ellipse is drawn hundreds of units thick,
                // sweeping grey bands across the view that look nothing like an orbit. Using the
                // nearest point makes the visible part correct and lets the far part fall below
                // a pixel, which is where it belongs.
                Vector3 cameraPosition = camera.transform.position;
                float distance = float.MaxValue;
                int step = Mathf.Max(1, _vertices.Length / 16);
                for (int i = 0; i < _vertices.Length; i += step)
                {
                    distance = Mathf.Min(distance, Vector3.Distance(cameraPosition, transform.TransformPoint(_vertices[i])));
                }
                if (distance >= float.MaxValue) distance = 1f;

                // Via ViewportInfo, not Camera.pixelHeight — see that class. Trusting the camera
                // here is what drew these as centimetre-thick slabs: a viewport reported as a
                // couple of pixels tall makes one pixel enormous, the width pegs to the cap, and
                // an orbit passing near the head sweeps a grey band across half the view.
                float width = ViewportInfo.WorldPerPixel(camera, distance) * _widthPixels;

                // Hard absolute cap as well. Orbits are a diagram drawn over the scene; at any
                // scale the user works at, a path more than a few millimetres thick has stopped
                // being a line and become an occluder.
                width = Mathf.Clamp(width, 1e-5f, 0.004f);

                _line.startWidth = width;
                _line.endWidth = width;
            }
        }

        public void OnOriginShifted(Vector3 shift)
        {
            // Vertices are rebuilt from absolute coordinates every LateUpdate, so the shift
            // needs no correction here — but applying it immediately avoids one frame of the
            // orbit hanging in the old position.
            if (_vertices == null || _line == null) return;
            for (int i = 0; i < _vertices.Length; i++) _vertices[i] += shift;
            _line.SetPositions(_vertices);
        }

        /// <summary>Fade this orbit, for satellites that would otherwise clutter their primary.</summary>
        public void Dim(float factor)
        {
            if (_line == null) return;

            Gradient gradient = _line.colorGradient;
            GradientAlphaKey[] alphas = gradient.alphaKeys;
            for (int i = 0; i < alphas.Length; i++) alphas[i].alpha *= Mathf.Clamp01(factor);

            var dimmed = new Gradient();
            dimmed.SetKeys(gradient.colorKeys, alphas);
            _line.colorGradient = dimmed;
        }

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                if (_line != null)
                {
                    _line.startColor = value;
                    _line.endColor = value;
                }
            }
        }

        private void OnDestroy()
        {
            if (_simulation != null && _simulation.Origin != null) _simulation.Origin.Unregister(this);
            if (_line != null && _line.material != null && Application.isPlaying) Destroy(_line.material);
        }
    }
}
