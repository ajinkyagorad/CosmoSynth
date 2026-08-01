using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Draws a body's orbit as a comet-tail trailing the body around its parent.
    ///
    /// Two details make the geometry less trivial than it looks:
    ///
    /// 1. The path is sampled uniformly in eccentric anomaly, not true anomaly. Uniform true
    ///    anomaly starves the apoapsis half of an eccentric orbit of vertices while piling them up
    ///    uselessly at periapsis, which makes a comet's orbit render as a polygon at the far end.
    ///    <see cref="OrbitalElements.SampleOrbitPath"/> handles that.
    ///
    /// 2. The vertices are regenerated whenever the floating origin shifts. A LineRenderer holds
    ///    its own copy of the positions, which an origin shift does not move — so without
    ///    <see cref="IFloatingOriginListener"/> the orbit tears away from its planet the first
    ///    time the camera travels far enough to trigger a rebase.
    ///
    /// Three things about how it *looked* were wrong, and they are what made the app read as a
    /// wireframe diagram rather than as a sky:
    ///
    /// <list type="bullet">
    /// <item><b>The lines were a pixel wide.</b> 1.1 px, hard-capped at 4 mm. On a headset a
    /// 1 px line is below what the panel can resolve cleanly, so it shimmers and breaks up as the
    /// head moves; on a monitor it is a hairline. Nothing is gained by it — a diagram drawn too
    /// faintly to follow is not a subtler diagram, it is a worse one.</item>
    /// <item><b>The fade did not track the body.</b> The gradient runs along the vertex array,
    /// which starts at periapsis, so the bright end sat at a fixed point on the ellipse while the
    /// planet wandered around it — the "reads as motion" the old comment claimed was, at almost
    /// all times, a bright patch nowhere near anything. The array is now rotated each frame so
    /// index zero is the body's current position, which is what makes the tail mean something.
    /// </item>
    /// <item><b>Everything was the same blue.</b> A dozen identical hairlines say nothing about
    /// which path belongs to which world. The hue now comes from the body, with its lightness
    /// normalised so no orbit is drawn in a colour that vanishes.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    [AddComponentMenu("CosmoSynth/Orbit Line")]
    public sealed class OrbitLineRenderer : MonoBehaviour, IFloatingOriginListener
    {
        [SerializeField] private int _sampleCount = 256;

        /// <summary>
        /// Line width, in pixels of the eye buffer.
        ///
        /// 2.6 rather than the 1.1 it was. Below about two pixels a line on an LCD headset panel
        /// aliases into a dotted crawl as the head moves, which reads as a rendering fault; the
        /// figure that matters is not how thin the line can be drawn but how thin it can be drawn
        /// and still hold still.
        /// </summary>
        [SerializeField] private float _widthPixels = 2.6f;

        [Tooltip("Fraction of the orbit behind the body that is drawn brightly.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _tailFraction = 0.55f;

        private UniverseSimulation _simulation;
        private CelestialBodyView _view;
        private LineRenderer _line;
        private List<Double3> _pathRelativeToParentKm;
        private Vector3[] _vertices;
        private Camera _camera;
        private Color _colour;
        private float _dim = 1f;

        public void Initialise(UniverseSimulation simulation, CelestialBodyView view)
        {
            _simulation = simulation;
            _view = view;

            _line = GetComponent<LineRenderer>();
            // Local space, matching CelestialBodyView: the line object sits at local zero under
            // the simulation transform, so its vertices share the bodies' coordinate space and the
            // whole universe moves as one when the anchor is placed in the room.
            _line.useWorldSpace = false;
            _line.loop = false;
            _line.material = CosmoShaders.Material(CosmoShaders.SpritesDefault);
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.alignment = LineAlignment.View;

            // Smooth the polyline so an eccentric orbit does not read as a polygon.
            _line.numCornerVertices = 2;
            _line.numCapVertices = 2;

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

            _colour = CosmoTheme.OrbitColour(_view.Body.DisplayColor);
            ApplyGradient();
        }

        /// <summary>
        /// The tail: opaque at the body, falling away behind it, gone by the time the path comes
        /// back round.
        ///
        /// Index zero of the vertex array is kept at the body's current position by
        /// <see cref="LateUpdate"/>, so a gradient anchored at t=0 is anchored to the planet.
        /// </summary>
        private void ApplyGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(_colour, 0f),
                    new GradientColorKey(_colour, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.95f * _dim, 0f),
                    new GradientAlphaKey(0.45f * _dim, _tailFraction * 0.45f),
                    new GradientAlphaKey(0.14f * _dim, _tailFraction),
                    // Never quite zero: the far side of an orbit still says how big it is, and a
                    // path that disappears entirely behind the body reads as a broken arc.
                    new GradientAlphaKey(0.07f * _dim, 1f),
                });

            _line.colorGradient = gradient;
        }

        private void LateUpdate()
        {
            if (_pathRelativeToParentKm == null || _simulation == null) return;

            // An orbit nobody is on is not an orbit.
            //
            // The conic is a closed form and will happily draw forever, which is the same
            // property that made CelestialBodyView refuse to place a spacecraft outside the span
            // its ephemeris covers — and this component was never told. So Cassini's ellipse is
            // still drawn around Saturn today, nine years after the spacecraft entered the
            // atmosphere, with nothing anywhere on it. That is worse than either half alone: the
            // path asserts a spacecraft is there and the missing dot asserts it is not, and a
            // viewer has no way to tell which of the two is the bug.
            if (_view != null && !_view.IsKnown)
            {
                if (_line != null && _line.enabled) _line.enabled = false;
                return;
            }

            // The sampled conic is expressed relative to the parent, so it has to be re-anchored
            // to wherever the parent is right now.
            ReferenceFrame parentFrame = _view.Frame?.Parent;
            if (parentFrame == null) return;

            Double3 parentAbsolute = parentFrame.AbsoluteState(_simulation.Clock.Now).Position;
            FloatingOrigin origin = _simulation.Origin;

            int count = _pathRelativeToParentKm.Count;
            int phase = PhaseIndex(count);

            // Rotated so vertex 0 is the body. This is the whole of the comet-tail effect; the
            // gradient does the rest.
            for (int i = 0; i < count; i++)
            {
                int source = phase - i;
                if (source < 0) source += count;
                _vertices[i] = origin.ToRender(parentAbsolute + _pathRelativeToParentKm[source]);
            }

            if (_camera == null) _camera = Camera.main;
            if (_camera == null)
            {
                _line.SetPositions(_vertices);
                return;
            }

            // Distance to the CLOSEST part of the orbit, sampled coarsely, plus the orbit's own
            // size and centre — everything the width and the legibility test need, in one pass.
            //
            // A LineRenderer has one width for the whole line, so sizing it from a single vertex
            // is wrong for any orbit large compared to its distance: pick a far vertex and the
            // near half of the ellipse is drawn hundreds of units thick, sweeping grey bands
            // across the view. The nearest point makes the visible part correct and lets the far
            // part fall below a pixel, which is where it belongs.
            Vector3 cameraLocal = transform.InverseTransformPoint(_camera.transform.position);
            Vector3 centreLocal = origin.ToRender(parentAbsolute);

            float nearest = float.MaxValue;
            float meanRadius = 0f;
            int samples = 0;
            int step = Mathf.Max(1, _vertices.Length / 16);
            for (int i = 0; i < _vertices.Length; i += step)
            {
                nearest = Mathf.Min(nearest, Vector3.Distance(cameraLocal, _vertices[i]));
                meanRadius += Vector3.Distance(centreLocal, _vertices[i]);
                samples++;
            }
            if (nearest >= float.MaxValue) nearest = 1f;
            if (samples > 0) meanRadius /= samples;

            // Local units and world units differ by the anchor's scale, which is identity today
            // but need not stay that way; the width calculation below is in world units.
            float scale = Mathf.Max(1e-6f, transform.lossyScale.x);
            nearest *= scale;

            float worldPerPixel = ViewportInfo.WorldPerPixel(_camera, nearest);
            float apparentPixels = worldPerPixel > 0f ? 2f * meanRadius * scale / worldPerPixel : 0f;

            if (!IsLegible(Vector3.Distance(cameraLocal, centreLocal), meanRadius, apparentPixels)) return;

            _line.SetPositions(_vertices);

            // Constant apparent width: an orbit line is a diagram, and a diagram that becomes
            // invisible when you zoom out has failed at its job.
            //
            // Via ViewportInfo, not Camera.pixelHeight — see that class. Trusting the camera here
            // is what drew these as centimetre-thick slabs: a viewport reported as a couple of
            // pixels tall makes one pixel enormous.
            float width = ViewportInfo.WorldPerPixel(_camera, nearest) * _widthPixels;

            // A hard cap still, because an orbit is a diagram drawn over the scene and a path more
            // than a few millimetres thick at arm's length has stopped being a line. The cap now
            // scales with distance rather than being an absolute 4 mm, so it constrains a path
            // beside your hand without also flattening one across the room.
            _line.widthMultiplier = Mathf.Clamp(width, 1e-5f, Mathf.Max(0.004f, nearest * 0.006f));
        }

        /// <summary>
        /// Whether this orbit reads as a path rather than as a streak.
        ///
        /// The condition that matters is whether the viewer is <i>inside</i> the orbit. From
        /// outside, a closed conic projects to a closed conic and says where the body has been and
        /// where it is going. From inside — and at a hologram on a table you are inside almost
        /// everything — it projects to a band right across the field of view, and all you can see
        /// of it is one edge sweeping the frame. That is not a faint version of the information,
        /// it is none of it, and it looks like a rendering fault. Both shipped screenshots show
        /// it: the Moon's ellipse is sixteen render units across while the camera sits two thirds
        /// of a unit from Earth, and it draws as a straight line corner to corner.
        ///
        /// The test this replaces compared the orbit's major axis against the distance to
        /// <c>_vertices[0]</c> — one arbitrary point on the ellipse, and now a moving one. In
        /// exactly the case it was written to catch, vertex zero is on the far side and so is
        /// itself about a major axis away, which made the comparison pass and drew the streak.
        /// </summary>
        private bool IsLegible(float distanceToCentre, float meanRadius, float apparentPixels)
        {
            _lastCentreDistance = distanceToCentre;
            _lastMeanRadius = meanRadius;
            _lastApparentPixels = apparentPixels;

            // Well outside, not merely outside. A ratio near 1 is the *worst* case, not a good
            // one: it means the viewer is standing on the path, where the ellipse projects to a
            // line running through them. Anchored on Earth the ratio for Earth's own orbit is
            // 1.01, which is precisely the band sweeping the shipped screenshots. Comfortably
            // outside is where a conic starts to look like a conic.
            //
            // And big enough to be a shape at all. With the whole Solar System on a table every
            // moon's orbit is still being drawn — Phobos' is seven ten-millionths of a unit
            // across — while the line width has a four-millimetre floor, so each one renders as a
            // blob some thousands of times wider than the path it represents, piled on the planet
            // it belongs to. That is the cyan smudge over the Sun in the captured frame, and it
            // is thirteen line renderers of per-frame work to produce it.
            bool legible = distanceToCentre > meanRadius * 1.5f && apparentPixels > MinimumApparentPixels;
            if (_line.enabled != legible) _line.enabled = legible;
            return legible;
        }

        /// <summary>
        /// Smallest an orbit may appear, in pixels across, and still be worth drawing. Below
        /// roughly this it is a dot, and a dot on a planet says nothing the planet did not.
        ///
        /// Eight rather than a rounder fifteen because of where the real cases fall: on a
        /// table-top Solar System, Earth's orbit is thirteen pixels across and is a small but
        /// perfectly readable ellipse, while the largest moon orbit is under a hundredth of a
        /// pixel. There are four orders of magnitude between the two, so the threshold only has to
        /// land somewhere in the gap — and the inner planets are the wrong side of fifteen.
        /// </summary>
        private const float MinimumApparentPixels = 8f;

        private float _lastCentreDistance;
        private float _lastMeanRadius;
        private float _lastApparentPixels;

        /// <summary>Diagnostics: why this orbit is or is not on screen, and how thick it is.</summary>
        public string Describe() =>
            $"{_view?.Body?.Id,-10} drawn={(_line != null && _line.enabled ? "y" : "n")} " +
            $"viewer={_lastCentreDistance:G4} radius={_lastMeanRadius:G4} " +
            $"ratio={(_lastMeanRadius > 0f ? _lastCentreDistance / _lastMeanRadius : 0f):F2} " +
            $"px={_lastApparentPixels:F1} width={(_line != null ? _line.widthMultiplier : 0f):G3}";

        /// <summary>
        /// Which sample the body currently sits on.
        ///
        /// The path is sampled uniformly in eccentric anomaly, so the index is E/2pi of the way
        /// along it. E comes from the true anomaly by the half-angle identity — going via the mean
        /// anomaly instead would mean solving Kepler's equation once per frame per orbit for a
        /// number only used to pick an array index.
        /// </summary>
        private int PhaseIndex(int count)
        {
            OrbitalElements orbit = _view.Body.Orbit.Value;
            if (orbit.Conic != ConicType.Elliptic) return 0;

            double trueAnomaly = orbit.TrueAnomalyAt(_simulation.Clock.Now);
            double e = orbit.Eccentricity;

            double eccentric = 2.0 * System.Math.Atan2(
                System.Math.Sqrt(1.0 - e) * System.Math.Sin(trueAnomaly * 0.5),
                System.Math.Sqrt(1.0 + e) * System.Math.Cos(trueAnomaly * 0.5));

            if (eccentric < 0.0) eccentric += MathD.TwoPi;

            int index = (int)(eccentric / MathD.TwoPi * (count - 1));
            return Mathf.Clamp(index, 0, count - 1);
        }

        public void OnOriginShifted(Vector3 shift)
        {
            // Vertices are rebuilt from absolute coordinates every LateUpdate, so the shift needs
            // no correction here — but applying it immediately avoids one frame of the orbit
            // hanging in the old position.
            if (_vertices == null || _line == null) return;
            for (int i = 0; i < _vertices.Length; i++) _vertices[i] += shift;
            _line.SetPositions(_vertices);
        }

        /// <summary>Fade this orbit, for satellites that would otherwise clutter their primary.</summary>
        public void Dim(float factor)
        {
            _dim = Mathf.Clamp01(factor);
            if (_line != null) ApplyGradient();
        }

        private void OnDestroy()
        {
            if (_simulation != null && _simulation.Origin != null) _simulation.Origin.Unregister(this);
            if (_line != null && _line.material != null && Application.isPlaying) Destroy(_line.material);
        }
    }
}
