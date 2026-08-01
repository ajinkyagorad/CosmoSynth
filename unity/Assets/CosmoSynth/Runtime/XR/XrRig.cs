using System.Collections;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Builds the immersive XR rig and turns passthrough on.
    ///
    /// The one line here that decides whether this is mixed reality or a black void is the
    /// camera's clear colour <b>alpha</b>. Quest composites passthrough *underneath* the app's
    /// framebuffer, so alpha is coverage: alpha 1 means "this pixel is mine, hide the room".
    /// A clear colour of (0,0,0,1) is therefore an opaque black wall, not an empty scene —
    /// and Unity's inspector draws it with exactly the same swatch as (0,0,0,0), while the
    /// editor Game view has no passthrough behind it to occlude. It looks correct everywhere
    /// except on the headset, which is the worst possible failure mode.
    ///
    /// The same rule catches out additive shaders: a pass with <c>Blend One One</c> blends
    /// alpha by the same rule as colour, so a fragment returning alpha 1 punches a hole in the
    /// world wherever the quad is drawn. Additive passes here must use
    /// <c>Blend One One, Zero One</c>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/XR Rig")]
    public sealed class XrRig : MonoBehaviour
    {
        [Header("Passthrough")]
        [Tooltip("Clear the camera to transparent so the room shows through. Off gives an opaque VR void.")]
        [SerializeField] private bool _passthrough = true;

        [Header("Player height")]
        [Tooltip("Fallback eye height in metres, used only if the headset reports no floor.")]
        [SerializeField] private float _fallbackEyeHeight = 1.6f;

        public Camera Camera { get; private set; }
        public XROrigin Origin { get; private set; }
        public Transform LeftHandAnchor { get; private set; }
        public Transform RightHandAnchor { get; private set; }

        /// <summary>True once an XR display is actually running, as opposed to editor playback.</summary>
        public bool XrActive { get; private set; }

        private void Awake()
        {
            BuildRig();
            StartCoroutine(EnablePassthroughWhenReady());
        }

        private void BuildRig()
        {
            // XROrigin is the tracking-space root: the headset's tracked pose is applied to the
            // camera *inside* it, so moving the universe means moving this object, never the
            // camera. Writing to the camera's transform directly fights the pose driver and
            // produces a scene that judders in time with head motion.
            var originObject = new GameObject("XR Origin");
            originObject.transform.SetParent(transform, false);

            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originObject.transform, false);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(cameraOffset.transform, false);
            cameraObject.tag = "MainCamera";

            Camera = cameraObject.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = _passthrough ? new Color(0f, 0f, 0f, 0f) : new Color(0.004f, 0.005f, 0.011f, 1f);
            Camera.nearClipPlane = 0.05f;   // ~5 cm: hands come closer than the usual 0.3 default
            Camera.farClipPlane = 5000f;
            Camera.allowHDR = false;        // mobile GPU; HDR costs bandwidth we need elsewhere
            Camera.allowMSAA = true;

            cameraObject.AddComponent<AudioListener>();

            // Two depth bands sharing this pose. See DepthBands for why one is not enough.
            cameraObject.AddComponent<DepthBands>().Attach(Camera);

            AttachPoseDriver(cameraObject);

            Origin = originObject.AddComponent<XROrigin>();
            Origin.Camera = Camera;
            Origin.CameraFloorOffsetObject = cameraOffset;
            // Floor-relative: the headset reports where the physical floor is, so a tabletop
            // solar system sits at a believable height instead of at an arbitrary offset.
            Origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            Origin.CameraYOffset = _fallbackEyeHeight;

            LeftHandAnchor = new GameObject("Left Hand Anchor").transform;
            RightHandAnchor = new GameObject("Right Hand Anchor").transform;
            LeftHandAnchor.SetParent(cameraOffset.transform, false);
            RightHandAnchor.SetParent(cameraOffset.transform, false);

            // Ambient light so unlit sides of bodies are not pure black against a lit room.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.055f, 0.07f);
        }

        /// <summary>
        /// Drive the camera from the headset pose.
        ///
        /// The Input System's TrackedPoseDriver needs its actions bound explicitly — unlike the
        /// legacy SpatialTracking one it replaced, which picked a device by enum. Binding them
        /// in code rather than through an .inputactions asset keeps the rig self-contained: no
        /// asset to wire up, nothing to break when someone reorders an action map.
        ///
        /// <c>UpdateAndBeforeRender</c> matters. Sampling only in Update leaves the pose a frame
        /// stale by the time it reaches the compositor, which on a headset is not a subtle
        /// artefact — it is the difference between a stable room and one that swims when you
        /// turn your head.
        /// </summary>
        private static void AttachPoseDriver(GameObject cameraObject)
        {
            var driver = cameraObject.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            var position = new InputAction("HMD Position", InputActionType.Value,
                "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            var rotation = new InputAction("HMD Rotation", InputActionType.Value,
                "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            var trackingState = new InputAction("HMD Tracking State", InputActionType.Value,
                "<XRHMD>/trackingState", expectedControlType: "Integer");

            driver.positionInput = new InputActionProperty(position);
            driver.rotationInput = new InputActionProperty(rotation);
            driver.trackingStateInput = new InputActionProperty(trackingState);

            position.Enable();
            rotation.Enable();
            trackingState.Enable();
        }

        /// <summary>
        /// Turn on Meta passthrough once the XR subsystems are up.
        ///
        /// Deferred to a coroutine because XR initialisation completes after Awake, and asking
        /// for passthrough before the session exists silently does nothing.
        /// </summary>
        private IEnumerator EnablePassthroughWhenReady()
        {
            // Give XR management a few frames to bring the loader up.
            for (int i = 0; i < 120 && !XrActive; i++)
            {
                XrActive = UnityEngine.XR.XRSettings.isDeviceActive;
                if (XrActive) break;
                yield return null;
            }

            if (!XrActive)
            {
                Debug.LogWarning("[CosmoSynth] No XR display came up — running flat. " +
                                 "On a headset this means the OpenXR loader failed to initialise.");
                // Without passthrough behind us, a transparent clear shows undefined garbage.
                if (_passthrough) Camera.backgroundColor = new Color(0.004f, 0.005f, 0.011f, 1f);
                yield break;
            }

            Debug.Log($"[CosmoSynth] XR active: {UnityEngine.XR.XRSettings.loadedDeviceName}, " +
                      $"eye texture {UnityEngine.XR.XRSettings.eyeTextureWidth}x{UnityEngine.XR.XRSettings.eyeTextureHeight}");

            if (!_passthrough) yield break;

            // Reapply the transparent clear now that XR is genuinely up.
            //
            // SetPassthrough refuses to go transparent while XrActive is false — clearing to
            // alpha 0 with no compositor behind it shows undefined garbage. But that means any
            // caller who set passthrough during Awake (the bootstrap does) left the camera
            // opaque, and nothing put it back. An opaque clear is a black wall in front of the
            // room, so passthrough would be dead with every feature flag still reporting
            // correct.
            Camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            // AR Foundation's session and camera manager are what request passthrough from the
            // Meta runtime. They are added here rather than in Awake so that a build without the
            // AR packages still compiles and runs as plain VR.
            var session = FindAnyObjectByType<UnityEngine.XR.ARFoundation.ARSession>();
            if (session == null)
            {
                var sessionObject = new GameObject("AR Session");
                sessionObject.transform.SetParent(transform, false);
                session = sessionObject.AddComponent<UnityEngine.XR.ARFoundation.ARSession>();
            }

            if (Camera.GetComponent<UnityEngine.XR.ARFoundation.ARCameraManager>() == null)
            {
                Camera.gameObject.AddComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();
            }

            Debug.Log("[CosmoSynth] passthrough requested (camera clears to alpha 0)");
        }

        /// <summary>Toggle passthrough at runtime — the "step outside into space" switch.</summary>
        public void SetPassthrough(bool enabled)
        {
            _passthrough = enabled;
            if (Camera == null) return;
            Camera.backgroundColor = enabled && XrActive
                ? new Color(0f, 0f, 0f, 0f)
                : new Color(0.004f, 0.005f, 0.011f, 1f);
        }

        public bool PassthroughEnabled => _passthrough;
    }
}
