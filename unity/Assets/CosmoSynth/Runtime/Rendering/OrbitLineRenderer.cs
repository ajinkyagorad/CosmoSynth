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
    /// 2. The vertices are rebuilt from absolute coordinates every frame, because the path is
    ///    stored relative to the parent and the parent moves. There is no origin-shift listener
    ///    any more — there is no rebase to listen for (contract 3.5) — but the reason the listener
    ///    existed still holds: a renderer holding its own copy of world positions tears away from
    ///    its planet the moment anything moves the frame underneath it.
    ///
    /// The geometry is a <c>CosmoSynth/Ribbon</c> mesh rather than a <c>LineRenderer</c>. Three
    /// things follow, and the first two were bugs:
    ///
    /// <list type="bullet">
    /// <item><b>The width is angular.</b> A LineRenderer's width is in world units, so keeping a
    /// line a constant two pixels wide meant recomputing a world width from the distance to the
    /// nearest point of the ellipse every frame — and one width for the whole line is wrong for
    /// any orbit large compared to its distance, which is why the near half used to sweep grey
    /// bands across the view and why there had to be a hard millimetre cap. The shader expands the
    /// ribbon in clip space against each eye's own projection, so the width is right everywhere on
    /// the line and in both eyes, and the cap is gone with the problem.</item>
    /// <item><b>It is billboarded per eye.</b> <c>LineAlignment.View</c> orients the strip from one
    /// camera transform, which in a headset is one quad shown to two eyes.</item>
    /// <item><b>It does not write alpha.</b> <c>Blend One One, Zero One</c>, so an orbit line adds
    /// light to passthrough instead of punching a hairline hole through the room.</item>
    /// </list>
    ///
    /// Three things about how it <i>looked</i> were wrong, and they are what made the app read as a
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
    [DefaultExecutionOrder(60)]
    [AddComponentMenu("CosmoSynth/Orbit Line")]
    public sealed class OrbitLineRenderer : MonoBehaviour
    {
        [SerializeField] private int _sampleCount = 256;

        /// <summary>
        /// Line width in dmm — millimetres subtended at one metre, which is milliradians.
        ///
        /// 1.8 dmm is 2.05 px at the 1140 px/rad of a Quest 3 eye buffer, which is the 2.6 px the
        /// LineRenderer was asked for measured against the Game view's own reckoning: the two
        /// differ by about 2.2x and the eye buffer is the one that ships. Below about two pixels a
        /// line on an LCD headset panel aliases into a dotted crawl as the head moves, which reads
        /// as a rendering fault; the figure that matters is not how thin the line can be drawn but
        /// how thin it can be drawn and still hold still.
        /// </summary>
        [SerializeField] private float _widthDmm = 1.8f;

        [Tooltip("Fraction of the orbit behind the body that is drawn brightly.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _tailFraction = 0.55f;

        private UniverseSimulation _simulation;
        private CelestialBodyView _view;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Material _material;
        private List<Double3> _pathRelativeToParentKm;
        private Vector3[] _points;                       // one per sample, this frame
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _others = new List<Vector3>();
        private Camera _camera;
        private Color _colour;
        private float _dim = 1f;

        private static readonly int WidthDmmId = Shader.PropertyToID("_WidthDmm");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int PlacementId = Shader.PropertyToID("_Placement");

        public void Initialise(UniverseSimulation simulation, CelestialBodyView view)
        {
            _simulation = simulation;
            _view = view;

            _mesh = new Mesh { name = "Orbit", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            // Local space, matching CelestialBodyView: the line object sits at local zero under
            // the simulation transform, so its vertices share the bodies' coordinate space and the
            // whole universe moves as one when the anchor is placed in the room. The bounds are
            // rebuilt with the vertices each frame, so the culler tracks the real extent.
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            _material = CosmoShaders.Material(CosmoShaders.Ribbon);
            if (_material == null)
            {
                _renderer.enabled = false;
                return;
            }
            _material.SetFloat(WidthDmmId, _widthDmm);
            _material.SetFloat(PlacementId, 0f);        // rigid: the CPU writes the vertices
            _material.SetFloat(IntensityId, _dim);
            _renderer.sharedMaterial = _material;

            Rebuild();
        }

        /// <summary>Resample the conic. Only needed when the orbit itself changes, not per frame.</summary>
        public void Rebuild()
        {
            if (_view?.Body?.Orbit == null || !_view.Body.Orbit.HasValue || _mesh == null)
            {
                if (_mesh != null) _mesh.Clear();
                _pathRelativeToParentKm = null;
                return;
            }

            _pathRelativeToParentKm = _view.Body.Orbit.Value.SampleOrbitPath(_sampleCount);
            _points = new Vector3[_pathRelativeToParentKm.Count];

            _colour = CosmoTheme.OrbitColour(_view.Body.DisplayColor);
            BuildTopology();
        }

        /// <summary>
        /// The four vertices per segment CosmoSynth/Ribbon expects, and the tail gradient.
        ///
        /// Each vertex carries its own endpoint (POSITION), the segment's other endpoint
        /// (TEXCOORD0) and (side, order) in TEXCOORD1; the shader takes the screen-space direction
        /// between the two and offsets by half the drawn width along its normal. Order is +1 when
        /// the other endpoint is the next point and -1 when it is the previous one, so both ends of
        /// a segment agree which way "side +1" is and the ribbon does not twist.
        ///
        /// Index and gradient only. Positions change every frame; this runs once per orbit, and the
        /// alpha it bakes is anchored to the ARRAY, which <see cref="LateUpdate"/> rotates so index
        /// zero is the body. That is the whole of the comet tail.
        /// </summary>
        private void BuildTopology()
        {
            int count = _pathRelativeToParentKm.Count;
            int segments = count - 1;
            if (segments < 1) { _mesh.Clear(); return; }

            var sideOrder = new List<Vector2>(segments * 4);
            var colours = new List<Color>(segments * 4);
            var triangles = new List<int>(segments * 6);

            _vertices.Clear();
            _others.Clear();
            for (int s = 0; s < segments; s++)
            {
                int b = s * 4;

                // Placeholders; LateUpdate fills them. The lists must already be the right length
                // so the per-frame path can index rather than allocate.
                _vertices.Add(Vector3.zero); _vertices.Add(Vector3.zero);
                _vertices.Add(Vector3.zero); _vertices.Add(Vector3.zero);
                _others.Add(Vector3.zero); _others.Add(Vector3.zero);
                _others.Add(Vector3.zero); _others.Add(Vector3.zero);

                sideOrder.Add(new Vector2(-1f, 1f));
                sideOrder.Add(new Vector2(1f, 1f));
                sideOrder.Add(new Vector2(-1f, -1f));
                sideOrder.Add(new Vector2(1f, -1f));

                Color near = _colour; near.a = TailAlpha(s / (float)segments);
                Color far = _colour; far.a = TailAlpha((s + 1) / (float)segments);
                colours.Add(near); colours.Add(near);
                colours.Add(far); colours.Add(far);

                triangles.Add(b); triangles.Add(b + 2); triangles.Add(b + 1);
                triangles.Add(b + 1); triangles.Add(b + 2); triangles.Add(b + 3);
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _others);
            _mesh.SetUVs(1, sideOrder);
            _mesh.SetColors(colours);
            _mesh.SetTriangles(triangles, 0);
        }

        /// <summary>
        /// The tail: opaque at the body, falling away behind it, gone by the time the path comes
        /// back round.
        ///
        /// The same four keys the LineRenderer gradient carried, evaluated directly — a Gradient
        /// object was only ever a way to hand these numbers to a component that wanted an asset.
        /// Never quite zero at the far end: the far side of an orbit still says how big it is, and
        /// a path that disappears entirely behind the body reads as a broken arc.
        /// </summary>
        private float TailAlpha(float t)
        {
            float a;
            if (t < _tailFraction * 0.45f) a = Mathf.Lerp(0.95f, 0.45f, t / Mathf.Max(1e-6f, _tailFraction * 0.45f));
            else if (t < _tailFraction) a = Mathf.Lerp(0.45f, 0.14f,
                (t - _tailFraction * 0.45f) / Mathf.Max(1e-6f, _tailFraction * 0.55f));
            else a = Mathf.Lerp(0.14f, 0.07f, (t - _tailFraction) / Mathf.Max(1e-6f, 1f - _tailFraction));
            return a;
        }

        private void LateUpdate()
        {
            if (_pathRelativeToParentKm == null || _simulation == null || _renderer == null) return;

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
                if (_renderer.enabled) _renderer.enabled = false;
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
            // gradient baked into the vertex colours does the rest.
            for (int i = 0; i < count; i++)
            {
                int source = phase - i;
                if (source < 0) source += count;
                _points[i] = origin.ToRender(parentAbsolute + _pathRelativeToParentKm[source]);
            }

            if (_camera == null) _camera = Camera.main;

            // Distance to the CLOSEST part of the orbit, sampled coarsely, plus the orbit's own
            // size and centre — everything the legibility test needs, in one pass. The width no
            // longer needs any of it: it is angular, set once on the material.
            Vector3 centreLocal = origin.ToRender(parentAbsolute);
            Vector3 cameraLocal = _camera != null
                ? transform.InverseTransformPoint(_camera.transform.position)
                : centreLocal;

            float nearest = float.MaxValue;
            float meanRadius = 0f;
            int samples = 0;
            int step = Mathf.Max(1, count / 16);
            for (int i = 0; i < count; i += step)
            {
                nearest = Mathf.Min(nearest, Vector3.Distance(cameraLocal, _points[i]));
                meanRadius += Vector3.Distance(centreLocal, _points[i]);
                samples++;
            }
            if (nearest >= float.MaxValue) nearest = 1f;
            if (samples > 0) meanRadius /= samples;

            if (_camera == null)
            {
                WriteVertices(count);
                return;
            }

            // Local units and world units differ by the anchor's scale, which is identity today
            // but need not stay that way.
            float scale = Mathf.Max(1e-6f, transform.lossyScale.x);
            float worldPerPixel = ViewportInfo.WorldPerPixel(_camera, nearest * scale);
            float apparentPixels = worldPerPixel > 0f ? 2f * meanRadius * scale / worldPerPixel : 0f;

            if (!IsLegible(Vector3.Distance(cameraLocal, centreLocal), meanRadius, apparentPixels)) return;

            WriteVertices(count);
        }

        /// <summary>Push this frame's path into the four-vertices-per-segment streams.</summary>
        private void WriteVertices(int count)
        {
            int segments = count - 1;
            if (segments < 1 || _vertices.Count != segments * 4) return;

            for (int s = 0; s < segments; s++)
            {
                int b = s * 4;
                Vector3 a = _points[s], c = _points[s + 1];
                _vertices[b] = a; _vertices[b + 1] = a;
                _vertices[b + 2] = c; _vertices[b + 3] = c;
                _others[b] = c; _others[b + 1] = c;
                _others[b + 2] = a; _others[b + 3] = a;
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _others);
            _mesh.RecalculateBounds();
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
            // across — while the line has a floor of about two pixels, so each one renders as a
            // blob some thousands of times wider than the path it represents, piled on the planet
            // it belongs to. That is the cyan smudge over the Sun in the captured frame.
            bool legible = distanceToCentre > meanRadius * 1.5f && apparentPixels > MinimumApparentPixels;
            if (_renderer.enabled != legible) _renderer.enabled = legible;
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
            $"{_view?.Body?.Id,-10} drawn={(_renderer != null && _renderer.enabled ? "y" : "n")} " +
            $"viewer={_lastCentreDistance:G4} radius={_lastMeanRadius:G4} " +
            $"ratio={(_lastMeanRadius > 0f ? _lastCentreDistance / _lastMeanRadius : 0f):F2} " +
            $"px={_lastApparentPixels:F1} width={_widthDmm:G3}dmm";

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

        // OnOriginShifted is gone with the rebase machinery it existed for. There is no rebase:
        // the camera stays in the room, the movers change the origin, and every vertex here is
        // rebuilt from absolute coordinates each LateUpdate anyway.

        /// <summary>Fade this orbit, for satellites that would otherwise clutter their primary.</summary>
        public void Dim(float factor)
        {
            _dim = Mathf.Clamp01(factor);
            if (_material != null) _material.SetFloat(IntensityId, _dim);
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying) return;
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
