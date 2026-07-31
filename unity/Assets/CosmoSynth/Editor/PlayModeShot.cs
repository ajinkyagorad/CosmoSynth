using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CosmoSynth.Core;
using CosmoSynth.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmoSynth.EditorTools
{
    /// <summary>
    /// Runs the <b>actual application</b> in play mode and screenshots it.
    ///
    /// This is different from <see cref="SceneShot"/> in the way that matters.
    /// <c>SceneShot</c> builds its own scene: it shares <c>CosmoSynth.Core</c>, so it proves the
    /// orbital mechanics and the catalogue, but it never instantiates
    /// <c>CosmoSynthBootstrap</c>, <c>XrRig</c>, <c>PlanetTextureBaker</c>, <c>SkyAndRings</c>,
    /// <c>BodySelector</c> or <c>AnchorBeacon</c>. Every bug reported from the headset so far
    /// lived in exactly that layer, and a reconstruction can never catch them — it can only
    /// produce a reassuring picture of code that is not the code being shipped.
    ///
    /// This enters play mode on the real scene, lets the real bootstrap run, waits for the
    /// asynchronous catalogue load and the threaded texture bakes to settle, then captures what
    /// the app is genuinely rendering and writes a diagnostic report beside it.
    ///
    /// Without a headset the rig falls back to flat mode (<c>XRSettings.isDeviceActive</c> is
    /// false), so this does not prove passthrough, tracking or gestures. It proves everything
    /// else: that the scene builds, the data loads, the bodies exist, the textures bake, and
    /// there is something in front of the camera.
    ///
    /// The domain-reload dance is mandatory, not defensive. Entering play mode reloads the
    /// application domain, which wipes statics and silently drops any
    /// <c>EditorApplication.update</c> subscription made beforehand — the run then hangs forever
    /// with no error at all. The job is therefore parked in <see cref="SessionState"/>, which
    /// survives the reload, and re-armed from a static constructor on the far side.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeShot
    {
        private const string PendingKey = "CosmoSynth.PlayModeShot.Pending";
        private const string OutputKey = "CosmoSynth.PlayModeShot.Output";
        private const string FramesKey = "CosmoSynth.PlayModeShot.Frames";
        private const string ViewKey = "CosmoSynth.PlayModeShot.View";

        private static int _frames;

        static PlayModeShot()
        {
            // Re-arm after the domain reload that entering play mode causes.
            if (SessionState.GetBool(PendingKey, false) && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update += Tick;
            }
        }

        public static void Run()
        {
            string output = Argument("-shotOut", "/tmp/cosmosynth-play.png");
            int frames = int.TryParse(Argument("-shotFrames", "600"), out int parsed) ? parsed : 600;
            string view = Argument("-shotView", "default");

            SessionState.SetBool(PendingKey, true);
            SessionState.SetString(OutputKey, output);
            SessionState.SetInt(FramesKey, frames);
            SessionState.SetString(ViewKey, view);

            const string scenePath = "Assets/CosmoSynth/Scenes/SolarSystemLab.unity";
            if (!File.Exists(scenePath))
            {
                Log($"FAILED: no scene at {scenePath}. Run ServerBuild.BuildAndroid once to generate it.");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Log($"entering play mode on {scenePath}, capturing after {frames} frames");

            // No -quit on the command line, or the editor exits before play mode starts.
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            _frames++;

            // Wait on the app's actual state, not on a frame count.
            //
            // Batchmode play mode runs unthrottled, so a frame budget is an unknown amount of
            // wall-clock time — 900 frames turned out to be a few seconds, which is not long
            // enough for threaded texture bakes to land. Waiting until the work is genuinely
            // done, with a frame cap as a backstop, makes the capture reproducible instead of
            // a race.
            if (!IsSettled() && _frames < SessionState.GetInt(FramesKey, 600)) return;

            EditorApplication.update -= Tick;
            SessionState.SetBool(PendingKey, false);

            string output = SessionState.GetString(OutputKey, "/tmp/cosmosynth-play.png");
            int exitCode = 0;

            try
            {
                string report = Inspect();
                Log(report);
                File.WriteAllText(Path.ChangeExtension(output, ".report.txt"), report);

                if (!Capture(output, SessionState.GetString(ViewKey, "default"))) exitCode = 3;
            }
            catch (Exception exception)
            {
                Log($"FAILED: {exception}");
                exitCode = 2;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// True once the app has finished starting up: catalogue loaded, bodies instantiated,
        /// and most surface textures baked.
        /// </summary>
        private static bool IsSettled()
        {
            var simulation = UnityEngine.Object.FindAnyObjectByType<UniverseSimulation>();
            if (simulation?.Catalog == null || simulation.Views.Count == 0) return false;

            var baker = UnityEngine.Object.FindAnyObjectByType<PlanetTextureBaker>();
            if (baker == null) return true;

            // Settled means the baker has stopped producing, not that every body has a texture.
            //
            // The baker caps itself at a texture budget and skips anything too small to benefit,
            // so with the exoplanet catalogue loaded most of the 1254 bodies will never be baked
            // at all. A condition of "three quarters are done" is then unsatisfiable, and the
            // harness simply ran until it was killed. Waiting for the count to stop rising is
            // the property that actually holds in both cases.
            if (baker.Baked != _lastBakedCount)
            {
                _lastBakedCount = baker.Baked;
                _framesSinceBakeChanged = 0;
                return false;
            }

            return ++_framesSinceBakeChanged > 240;
        }

        private static int _lastBakedCount = -1;
        private static int _framesSinceBakeChanged;

        private static bool Unused()
        {
            return false;
        }

        /// <summary>
        /// Interrogate the running app.
        ///
        /// The report is the point as much as the image. A screenshot showing nothing is
        /// ambiguous in exactly the way the headset was — these numbers say whether the
        /// catalogue loaded, whether bodies exist, whether textures baked, and how large the
        /// focused body is on screen, which distinguishes "broken" from "correctly far away".
        /// </summary>
        private static string Inspect()
        {
            var report = new StringBuilder();
            report.AppendLine("CosmoSynth play-mode report");
            report.AppendLine($"  captured at frame {_frames}");

            var bootstrap = UnityEngine.Object.FindAnyObjectByType<CosmoSynthBootstrap>();
            if (bootstrap == null)
            {
                report.AppendLine("  FAIL: no CosmoSynthBootstrap in the scene");
                return report.ToString();
            }

            var rig = UnityEngine.Object.FindAnyObjectByType<XrRig>();
            report.AppendLine($"  XR active: {(rig != null && rig.XrActive ? "yes" : "no (flat fallback)")}");
            report.AppendLine($"  passthrough requested: {rig?.PassthroughEnabled}");
            report.AppendLine($"  camera: {(rig?.Camera != null ? rig.Camera.name : "MISSING")}");

            if (rig?.Camera != null)
            {
                Camera camera = rig.Camera;
                report.AppendLine($"  clip planes: near {camera.nearClipPlane:G4}, far {camera.farClipPlane:G4}");

                var shells = UnityEngine.Object.FindObjectsByType<CosmoSynth.Runtime.AtmosphereShell>(
                    FindObjectsSortMode.None);
                for (int i = 0; i < shells.Length; i++)
                {
                    report.AppendLine($"  atmosphere {shells[i].Describe()}");
                }
                report.AppendLine($"  atmosphere shells: {shells.Length}");

                var selector = UnityEngine.Object.FindAnyObjectByType<CosmoSynth.Runtime.BodySelector>();
                report.AppendLine($"  selection ring: {(selector != null ? selector.DescribeHighlight() : "(no selector)")}");

                var bands = camera.GetComponent<CosmoSynth.Runtime.DepthBands>();
                if (bands != null)
                {
                    report.AppendLine($"  depth bands: split {bands.SplitDistance:G4}, far {bands.FarDistance:G4}");
                }
                report.AppendLine($"  clear: {camera.clearFlags}, colour alpha {camera.backgroundColor.a:F2}");
            }

            var simulation = UnityEngine.Object.FindAnyObjectByType<UniverseSimulation>();
            if (simulation?.Catalog == null)
            {
                report.AppendLine("  FAIL: catalogue not loaded");
                return report.ToString();
            }

            report.AppendLine($"  catalogue: '{simulation.Catalog.Name}' — {simulation.Catalog.Count} bodies");
            report.AppendLine($"  views instantiated: {simulation.Views.Count}");
            report.AppendLine($"  scale: {simulation.Origin.UnitsPerKm:G4} units/km");

            if (bootstrap.UniverseAnchor != null)
            {
                report.AppendLine($"  anchor at: {bootstrap.UniverseAnchor.position}");
                if (rig?.Camera != null)
                {
                    float distance = Vector3.Distance(bootstrap.UniverseAnchor.position, rig.Camera.transform.position);
                    report.AppendLine($"  anchor is {distance:F2} m from the camera");
                }
            }

            var baker = UnityEngine.Object.FindAnyObjectByType<PlanetTextureBaker>();
            if (baker != null) report.AppendLine($"  surface textures baked: {baker.Baked}");

            // The inputs the LOD decision is actually made from. Reporting the derived pixel
            // count alone is what made the last two rounds guesswork — the interesting question
            // is always "distance to what, measured from where".
            if (rig?.Camera != null)
            {
                Vector3 cameraInUniverse =
                    simulation.transform.InverseTransformPoint(rig.Camera.transform.position);
                Double3 viewerKm = simulation.Origin.ToAbsolute(cameraInUniverse);

                report.AppendLine($"  camera world: {rig.Camera.transform.position}");
                report.AppendLine($"  camera in universe space: {cameraInUniverse}");
                report.AppendLine($"  origin anchored at: {simulation.Origin.OriginKm.ToString("G6")} km");
                report.AppendLine($"  viewer resolves to: {viewerKm.ToString("G6")} km");

                // Sorted largest-first. Dictionary order buries whatever is actually filling the
                // screen behind ten alphabetically-lucky exoplanets, which makes the report
                // useless for the question it is usually asked: what am I looking at?
                var ordered = new List<CelestialBodyView>(simulation.Views.Values);
                ordered.Sort((a, b) =>
                {
                    if (a == null) return 1;
                    if (b == null) return -1;
                    return b.ApparentDiameterPixels.CompareTo(a.ApparentDiameterPixels);
                });

                report.AppendLine("  bodies (distance / display radius / apparent size), largest first:");
                int listed = 0, visible = 0;
                foreach (CelestialBodyView view in ordered)
                {
                    if (view == null) continue;
                    if (view.ApparentDiameterPixels >= 1.0) visible++;
                    if (listed++ >= 12) continue;

                    double distanceKm = Double3.Distance(viewerKm, view.AbsolutePositionKm);
                    double displayRadiusKm = simulation.DisplayRadiusKm(view.Body);

                    report.AppendLine(
                        $"    {view.Body.Name,-13} d={distanceKm,11:G5} km  R={displayRadiusKm,10:G5} km  " +
                        $"{view.ApparentDiameterPixels,8:F1} px  {view.Representation,-12} " +
                        $"tex={(view.HasSurfaceTexture ? "y" : "n")} rWorld={view.RenderedRadius:G3} " +
                        $"{(view.DrawnAsStarPoint ? "sky" : "space")}");
                }
                report.AppendLine($"  bodies at 1 px or larger: {visible} of {simulation.Views.Count}");
            }

            return report.ToString();
        }

        /// <summary>
        /// Render the app's own camera to a texture.
        ///
        /// Rendering the live camera rather than using <c>ScreenCapture</c>, which is unreliable
        /// in batchmode. Everything about the frame — clip planes, clear colour, what the LOD
        /// system decided to draw — is whatever the running app set it to.
        /// </summary>
        private static bool Capture(string outputPath, string view)
        {
            var rig = UnityEngine.Object.FindAnyObjectByType<XrRig>();
            Camera camera = rig != null ? rig.Camera : Camera.main;
            if (camera == null)
            {
                Log("FAILED: no camera to capture from");
                return false;
            }

            // Optionally reposition to frame the whole system rather than the default close-up.
            if (view == "wide")
            {
                var bootstrap = UnityEngine.Object.FindAnyObjectByType<CosmoSynthBootstrap>();
                if (bootstrap?.UniverseAnchor != null)
                {
                    Vector3 anchor = bootstrap.UniverseAnchor.position;
                    camera.transform.position = anchor + new Vector3(0.55f, 0.42f, -1.15f);
                    camera.transform.rotation = Quaternion.LookRotation(anchor - camera.transform.position, Vector3.up);
                }
            }

            const int width = 1920, height = 1080;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };

            RenderTexture previousTarget = camera.targetTexture;
            // The app clears to alpha 0 for passthrough. With no compositor behind it that would
            // save as a transparent PNG showing nothing, so force an opaque clear for the
            // capture only — and say so, because it is a deviation from what the device does.
            CameraClearFlags previousFlags = camera.clearFlags;
            Color previousColor = camera.backgroundColor;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.004f, 0.005f, 0.011f, 1f);
            camera.targetTexture = target;
            camera.Render();

            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            camera.targetTexture = previousTarget;
            camera.clearFlags = previousFlags;
            camera.backgroundColor = previousColor;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, image.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);

            Log($"wrote {outputPath} ({new FileInfo(outputPath).Length / 1024} kB)");
            return true;
        }

        private static string Argument(string name, string fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i] == name) return arguments[i + 1];
            }
            return fallback;
        }

        private static void Log(string message) => Debug.Log($"[PlayModeShot] {message}");
    }
}
