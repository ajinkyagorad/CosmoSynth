using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Renders the scene in two depth bands so one frame can hold both a hand and a planet.
    ///
    /// A depth buffer stores precision, not range, and what it can express is governed by the
    /// ratio far/near rather than by either alone. CosmoSynth needs a near plane at five
    /// centimetres, because hands and wrist panels come that close, and a far plane that reaches
    /// the outer solar system — which at a tabletop scale is a couple of hundred thousand render
    /// units away. That is a ratio around 1e7 in a single pass, and it does not work: either the
    /// far half z-fights itself into stripes, or the near plane is pushed out to compensate and
    /// the entire room-scale scene is clipped away. The second is what was happening, and it
    /// looks precisely like a broken app — black screen, working passthrough, no error.
    ///
    /// Two cameras sharing a pose solve it. The far one draws first and owns the background; the
    /// near one clears depth only and draws on top. Each covers the square root of the total
    /// ratio, so 1e10 of range becomes two comfortable passes of 1e5 each.
    ///
    /// There is deliberately no layer assignment. Both cameras cull against their own near and
    /// far planes, so each one already draws only what falls inside its band — splitting by
    /// layer as well would add bookkeeping, and would break the case that most needs to work:
    /// an object spanning the boundary, like the planet you are standing on, which has to be
    /// drawn by both cameras to appear whole.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Depth Bands")]
    public sealed class DepthBands : MonoBehaviour
    {
        /// <summary>
        /// Largest far/near ratio one band is asked to carry.
        ///
        /// A 24-bit buffer holds about 1e5 usefully once the hyperbolic distribution is
        /// accounted for. Asking for more does not fail loudly; it just starts z-fighting in the
        /// distance, which is far harder to attribute than a hard clip.
        /// </summary>
        private const float MaximumBandRatio = 1e5f;

        /// <summary>Closest anything is ever drawn: hands and wrist panels come this near.</summary>
        private const float NearestPlane = 0.05f;

        private Camera _near;
        private Camera _far;

        /// <summary>Distance where the near band hands over to the far one, in render units.</summary>
        public float SplitDistance { get; private set; } = 1000f;

        /// <summary>The furthest anything can be drawn, in render units.</summary>
        public float FarDistance { get; private set; } = 5e5f;

        /// <summary>The far band's camera. Anything compositing a frame by hand needs both.</summary>
        public Camera FarCamera => _far;

        /// <summary>
        /// Build the far camera as a child of the near one, so it inherits the tracked head pose
        /// without a second pose driver competing for it.
        /// </summary>
        public void Attach(Camera nearCamera)
        {
            if (nearCamera == null) return;
            _near = nearCamera;

            var farObject = new GameObject("Far Band Camera");
            farObject.transform.SetParent(nearCamera.transform, false);

            _far = farObject.AddComponent<Camera>();
            _far.CopyFrom(nearCamera);

            // Renders first: Unity draws cameras in ascending depth order, and the near band has
            // to arrive on top of the far one rather than under it.
            _far.depth = nearCamera.depth - 1f;
            _far.clearFlags = nearCamera.clearFlags;
            _far.backgroundColor = nearCamera.backgroundColor;

            // Clear depth, keep colour. Clearing colour here would erase the far band — and on
            // Quest, clearing it to an opaque value would also erase passthrough.
            _near.clearFlags = CameraClearFlags.Depth;

            // One audio listener per scene; CopyFrom does not bring components, but be explicit
            // about the far camera being purely a render pass.
            _far.tag = "Untagged";

            ApplyRange(SplitDistance, FarDistance);
        }

        /// <summary>
        /// Keep the far camera's clear in step with the near one, every frame.
        ///
        /// Splitting into bands quietly moves responsibility for the background: the near camera
        /// clears depth only now, so every existing line that sets its backgroundColor — and
        /// XrRig has three, all of them toggling passthrough — is writing to a camera that no
        /// longer clears colour at all. On a headset that means the passthrough clear silently
        /// stops working, which is the single failure this project has already spent the most
        /// time on. Mirroring here is two float copies and cannot drift, whereas routing every
        /// caller through a setter relies on nobody ever writing to the camera directly again.
        /// </summary>
        private void LateUpdate()
        {
            if (_near == null || _far == null) return;

            _far.backgroundColor = _near.backgroundColor;
            _far.fieldOfView = _near.fieldOfView;

            // The near camera must never clear colour, or it erases the far band beneath it.
            if (_near.clearFlags != CameraClearFlags.Depth) _near.clearFlags = CameraClearFlags.Depth;
            if (_far.clearFlags == CameraClearFlags.Depth) _far.clearFlags = CameraClearFlags.SolidColor;
        }

        /// <summary>
        /// Fit the bands to the furthest thing actually being drawn this frame.
        ///
        /// Returns the distance past which bodies should be pinned to the sky, which is inside
        /// the far plane with margin to spare — a star sitting exactly on the far plane would
        /// flicker in and out as the range breathed.
        /// </summary>
        public float FitTo(float farthestRenderUnits)
        {
            // The floor has to clear the procedural star field, which sits on a fixed sphere at
            // 900 units. Let the far plane fall below that and the sky is clipped away, leaving
            // a hologram floating in a void — and it would happen exactly when the scene is
            // simplest, which is a confusing time for the background to vanish. It also keeps
            // the bands from collapsing and re-expanding frame to frame, which reads as pulsing.
            const float Floor = 2000f;
            float far = Mathf.Clamp(farthestRenderUnits * 1.5f, Floor,
                NearestPlane * MaximumBandRatio * MaximumBandRatio);

            // Split at the geometric mean, which is what makes the two ratios equal: near..split
            // and split..far both span sqrt(far/near).
            float split = Mathf.Sqrt(NearestPlane * far);

            ApplyRange(split, far);
            return far * 0.6f;
        }

        private void ApplyRange(float split, float far)
        {
            SplitDistance = split;
            FarDistance = far;

            if (_near != null)
            {
                _near.nearClipPlane = NearestPlane;
                _near.farClipPlane = split;
            }

            if (_far != null)
            {
                // Overlap the bands slightly. Butting them exactly together leaves a hairline of
                // depth where an object is clipped by both cameras and shows as a seam.
                _far.nearClipPlane = split * 0.9f;
                _far.farClipPlane = far;
                _far.fieldOfView = _near != null ? _near.fieldOfView : _far.fieldOfView;
            }
        }
    }
}
