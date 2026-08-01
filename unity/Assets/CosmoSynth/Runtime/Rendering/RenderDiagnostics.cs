using System.Text;
using UnityEngine;
using UnityEngine.XR;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// What the device is actually doing to produce a frame, in one line, on screen.
    ///
    /// This exists because of a class of bug that cannot be seen from here at all. The play-mode
    /// harness renders flat and monoscopic, so it exercises none of the stereo path: not the
    /// eye-texture array, not the multiview resolve, not MSAA against either. A build shipped on
    /// 2026-07-31 came back reported as "100 plus instances of earth ... repeated on a grid",
    /// which is what a bad multiview resolve looks like, and every desktop capture of the same
    /// tree showed exactly one Earth. There was no way to tell which of three plausible causes
    /// it was without asking the person wearing the headset to describe pixels.
    ///
    /// So the app says it itself. Reading the answer off the status card is worth more than any
    /// amount of reasoning about a device nobody here can see — the same argument as the anchor
    /// beacon, which exists so "passthrough but nothing in it" diagnoses itself.
    ///
    /// The interesting fields, and what a wrong value would mean:
    ///
    ///   mode          SinglePassInstanced is what XrSetup asks for. MultiPass or
    ///                 SinglePassMultiview means the setting did not take, and the eye buffers
    ///                 are laid out differently from what every shader assumes.
    ///   eye WxH:D     D is the eye texture's volume depth — 2 for the texture array that
    ///                 single-pass instancing renders into, 1 for a plain 2D target. A 1 here
    ///                 with the mode claiming instanced is the contradiction that would explain
    ///                 a tiled frame.
    ///   msaa          Requested against actual. Quality level 2 ships the Android default.
    ///   cams          Enabled cameras rendering to the eye buffer. DepthBands adds a second on
    ///                 purpose; a third means something is creating cameras per catalogue load,
    ///                 and the catalogue loads three times.
    /// </summary>
    public static class RenderDiagnostics
    {
        /// <summary>One compact line for the status card.</summary>
        public static string OneLine()
        {
            var line = new StringBuilder();

            if (!XRSettings.isDeviceActive)
            {
                line.Append("render: flat (no XR device)");
            }
            else
            {
                RenderTextureDescriptor eye = XRSettings.eyeTextureDesc;
                line.Append($"render: {XRSettings.stereoRenderingMode} ");
                line.Append($"{XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}:{eye.volumeDepth} ");
                line.Append($"msaa {QualitySettings.antiAliasing}/{eye.msaaSamples} ");
            }

            line.Append($"cams {EnabledCameras()} q{QualitySettings.GetQualityLevel()}");
            return line.ToString();
        }

        /// <summary>
        /// Cameras that are actually enabled and drawing. Counted rather than assumed: the
        /// scene is built entirely in code and rebuilt on every catalogue merge, so "how many
        /// cameras exist" is a question with a real answer rather than a constant.
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

        private static bool _logged;
    }
}
