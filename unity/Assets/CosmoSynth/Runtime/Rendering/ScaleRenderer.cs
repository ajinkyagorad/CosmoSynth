using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Decides how each body is drawn this frame, and keeps the camera's clip planes sane.
    ///
    /// Selection is driven by apparent size in pixels rather than distance, so one rule covers
    /// a 500 m asteroid and a 140000 km gas giant, and it adapts by itself to a headset's
    /// per-eye resolution instead of needing a separate tuning pass per device.
    ///
    /// The near and far planes are re-derived every frame from what is actually visible. A
    /// fixed pair cannot work across this range: a near plane close enough for a handheld
    /// hologram and a far plane distant enough for Neptune would give a depth ratio of 1e13,
    /// which no depth buffer survives. Tracking the real bounds keeps the ratio small enough
    /// that a reversed float depth buffer stays precise.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    [AddComponentMenu("CosmoSynth/Scale Renderer")]
    public sealed class ScaleRenderer : MonoBehaviour
    {
        [SerializeField] private UniverseSimulation _simulation;
        [SerializeField] private Camera _camera;

        [Header("Depth")]
        [Tooltip("Splits the frustum into two passes so hands and planets can share a frame.")]
        [SerializeField] private DepthBands _bands;

        [Header("Diagnostics")]
        [SerializeField] private int _visibleBodies;
        [SerializeField] private int _pointBodies;

        private readonly List<CelestialBodyView> _sorted = new List<CelestialBodyView>();

        /// <summary>
        /// Resolve dependencies lazily rather than once in Awake.
        ///
        /// This component lives on the camera rig, which the bootstrap builds *before* the
        /// simulation — so an Awake-time lookup finds null, and a component that caches null
        /// once never recovers. The symptom is silent and total: LateUpdate returns early every
        /// frame, no LOD is ever computed, every body reports zero apparent pixels, and nothing
        /// anywhere logs a complaint. Retrying until found costs one null check per frame.
        /// </summary>
        private bool TryResolve()
        {
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();
            if (_camera == null) _camera = Camera.main;
            if (_bands == null && _camera != null) _bands = _camera.GetComponent<DepthBands>();
            return _simulation != null && _camera != null;
        }

        private void LateUpdate()
        {
            if (!TryResolve() || _simulation.Catalog == null) return;

            float verticalFov = _camera.fieldOfView * Mathf.Deg2Rad;
            int verticalPixels = ViewportInfo.VerticalPixels(_camera);

            // The viewer position must be expressed in the *universe's* space, not the world's.
            // Bodies are positioned with localPosition under the simulation transform, which is
            // the anchor in the room — so converting the camera's world position directly gives
            // a viewer that is offset by the whole anchor transform. At tabletop scale that
            // offset is thousands of kilometres of apparent distance, which is enough to drop
            // every body to Point and blank the scene while passthrough carries on working.
            Vector3 cameraInUniverseSpace =
                _simulation.transform.InverseTransformPoint(_camera.transform.position);
            Double3 viewerAbsoluteKm = _simulation.Origin.ToAbsolute(cameraInUniverseSpace);

            float farthestRenderUnits = 0f;

            _visibleBodies = 0;
            _pointBodies = 0;
            _sorted.Clear();

            foreach (var view in _simulation.Views.Values)
            {
                if (view == null) continue;

                double distanceKm = Double3.Distance(viewerAbsoluteKm, view.AbsolutePositionKm);
                double radiusKm = _simulation.DisplayRadiusKm(view.Body);

                double angular = ScaleSpace.AngularDiameterRad(radiusKm, distanceKm);
                double pixels = ScaleSpace.ApparentDiameterPixels(angular, verticalFov, verticalPixels);
                var level = ScaleSpace.SelectRepresentation(pixels, insideBody: distanceKm <= radiusKm);

                view.SetRepresentation(level, pixels);

                if (level >= RepresentationLevel.Sphere) _visibleBodies++;
                else _pointBodies++;

                // Measure the far plane against where the body is DRAWN, not where it is.
                //
                // Those differ for anything past the sky horizon, and using the true distance is
                // what pushed the far plane to 2e10 render units and the near plane out to
                // twenty-six kilometres with it — clipping the room, the hologram and the hands
                // in one go. What the depth buffer has to cover is only what is rasterised.
                float drawnDistance = Vector3.Distance(cameraInUniverseSpace, view.transform.localPosition);
                farthestRenderUnits = Mathf.Max(farthestRenderUnits, drawnDistance + view.RenderedRadius);

                _sorted.Add(view);
            }

            if (_bands != null)
            {
                _simulation.StarPointHorizonUnits = _bands.FitTo(farthestRenderUnits);
            }
        }

        /// <summary>Bodies sorted by apparent size, largest first. Useful for labelling and picking.</summary>
        public IReadOnlyList<CelestialBodyView> BodiesByApparentSize()
        {
            _sorted.Sort((a, b) => b.ApparentDiameterPixels.CompareTo(a.ApparentDiameterPixels));
            return _sorted;
        }
    }
}
