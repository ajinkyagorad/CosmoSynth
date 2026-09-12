using System.Text;
using CosmoSynth.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// What the device is actually doing to produce a frame, in one line, on screen and in logcat.
    ///
    /// This exists because of a class of bug that cannot be seen from here at all. The play-mode
    /// harness renders flat and monoscopic, so it exercises none of the stereo path: not the
    /// eye-texture array, not the multiview resolve, not MSAA against either. A build shipped on
    /// 2026-07-31 came back reported as "100 plus instances of earth ... repeated on a grid",
    /// which is what a bad multiview resolve looks like, and every desktop capture of the same
    /// tree showed exactly one Earth. There was no way to tell which of three plausible causes it
    /// was without asking the person wearing the headset to describe pixels.
    ///
    /// So the app says it itself. Every field below is a decision this project made on purpose,
    /// printed so that "did it take?" is a question the headset answers rather than one we reason
    /// about. The ones that turn the line red:
    ///
    ///   cams != 1        a second camera is the 1.7.0 bug class itself
    ///   a != 0           an opaque clear in passthrough is a black wall in front of the room
    ///   dep 16-bit       the depth separability in ScaledSpace assumes 24; 16 is 256x coarser
    ///   cs Gamma         every colour in the app was tuned under Linear
    ///   BiRP             the shaders are URP-only and render magenta under Built-in
    ///   push &gt; 0         groups the solver could not order; see ScaledSpace.Solve
    ///   vol &gt; 0          a drawn body punches a hole through an enclosing volume (R6)
    /// </summary>
    public static class RenderDiagnostics
    {
        private static ScaleRenderer _renderer;
        private static bool _logged;

        /// <summary>One compact line for the status card and for logcat.</summary>
        public static string OneLine()
        {
            var line = new StringBuilder("render: ");

            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            line.Append(pipeline != null ? "URP " : "BiRP ");

            if (!XRSettings.isDeviceActive)
            {
                line.Append("flat ");
            }
            else
            {
                RenderTextureDescriptor eye = XRSettings.eyeTextureDesc;
                line.Append($"{XRSettings.stereoRenderingMode} ");
                line.Append($"{XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}:{eye.volumeDepth} ");
                line.Append($"msaa {QualitySettings.antiAliasing}/{eye.msaaSamples} ");
                line.Append($"dep {eye.depthStencilFormat} ");
            }

            line.Append($"cams {EnabledCameras()} ");

            Camera main = Camera.main;
            if (main != null)
            {
                line.Append($"clr {ShortClear(main.clearFlags)}/a={main.backgroundColor.a:F2} ");   // gate-justified: reading the clear alpha, which is the whole point of this line
            }

            line.Append($"sub {DepthSubmission()} ");
            line.Append($"cs {QualitySettings.activeColorSpace} ");
            line.Append($"fov {Foveation()} ");

            if (_renderer == null) _renderer = Object.FindAnyObjectByType<ScaleRenderer>();
            if (_renderer != null)
            {
                // ppr is R8's MEASURED pixels per radian, and every pixel figure in the contract —
                // the 2.5 px visibility floor, the 8 px orbit cull, the label legibility floor,
                // the stereo assertion's 103 px — is restated against it. Until the headset prints
                // it, 1140 is an inference.
                line.Append($"ppr {_renderer.Context.PixelsPerRadian:F0} ");
                line.Append($"grp {_renderer.RigidGroups}/{_renderer.EnvelopeGroups}/{_renderer.EnclosingGroups} ");
                line.Append($"push {_renderer.PushConflicts} ");
                line.Append($"vol {_renderer.VolumeOcclusion} ");
                line.Append($"far {_renderer.FarthestRenderedMetres:F0}/{ScaledSpace.FarPlaneMetres:F0}");
            }

            return line.ToString().TrimEnd();
        }

        /// <summary>
        /// True when anything on the line is a value this project decided against. The status card
        /// colours itself from this, so a wrong setting announces itself rather than waiting to be
        /// read.
        /// </summary>
        public static bool IsHealthy()
        {
            if (GraphicsSettings.currentRenderPipeline == null) return false;
            if (EnabledCameras() != 1) return false;
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) return false;

            Camera main = Camera.main;
            if (main != null && main.backgroundColor.a > 0.001f && XRSettings.isDeviceActive) return false;   // gate-justified: reading the clear alpha, not choosing a colour

            // A 16-bit depth buffer is 256x coarser than the D24 every separability figure in
            // ScaledSpace is computed against. The doc comment above has always listed it as a red
            // condition and the line has always printed the format; nothing ever compared them, so
            // a device handing back D16 printed it and reported healthy.
            if (XRSettings.isDeviceActive &&
                XRSettings.eyeTextureDesc.depthStencilFormat.ToString().Contains("D16")) return false;

            if (_renderer == null) _renderer = Object.FindAnyObjectByType<ScaleRenderer>();
            if (_renderer == null) return true;
            if (_renderer.PushConflicts > 0) return false;
            if (_renderer.VolumeOcclusion > 0) return false;

            return true;
        }

        private static string ShortClear(CameraClearFlags flags) =>
            flags == CameraClearFlags.SolidColor ? "SC" : flags.ToString();

        /// <summary>
        /// The depth-submission mode OpenXR is configured with, read at runtime rather than
        /// assumed from the YAML — two independent sources that agree only when both are right.
        /// </summary>
        private static string DepthSubmission()
        {
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.Instance;
            return settings != null ? settings.depthSubmissionMode.ToString() : "?";
        }

        private static string Foveation()
        {
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.Instance;
            return settings != null ? settings.foveatedRenderingApi.ToString() : "?";
        }

        /// <summary>
        /// Cameras that are actually enabled and drawing. Counted rather than assumed: the
        /// scene is built entirely in code, so "how many cameras exist" is a question with a
        /// real answer rather than a constant. It must be 1.
        /// </summary>
        private static int EnabledCameras()
        {
            int count = 0;
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].isActiveAndEnabled) count++;
            }
            return count;
        }

        /// <summary>
        /// Log the same facts once, so they reach adb logcat as well as the headset. Called from
        /// the beacon a few seconds in, by which time XR has settled — reading these at Awake
        /// gives zeros and a mode of "MultiPass" before the loader has finished.
        /// </summary>
        public static void LogOnce()
        {
            if (_logged) return;
            _logged = true;
            Debug.Log($"[CosmoSynth] {OneLine()}");
        }
    }
}
