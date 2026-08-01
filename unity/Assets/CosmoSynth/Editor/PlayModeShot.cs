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

            // Change the scale, then let the app run on before capturing.
            //
            // Bodies are placed in LateUpdate, so a scale set during this callback has not reached
            // a single transform yet: capturing immediately photographs the previous scale's
            // layout through the new scale's clip planes, which looks like a rendering bug and is
            // entirely the harness's doing. Twenty frames also lets the manipulator's log-space
            // scale smoothing settle.
            if (!_settledActionsDone)
            {
                _settledActionsDone = true;

                // Size the cameras to the capture BEFORE letting the app run on.
                //
                // This is why the report and the picture used to disagree. Every apparent-size
                // rule in the app — the minimum visible body, star magnitudes, orbit line width,
                // LOD selection — is denominated in pixels of the render target, and the target
                // was only swapped for a 1920x1080 one at the instant of capture. So all of that
                // ran against the batchmode Game view, which is about 486 pixels tall, and the
                // capture then photographed the result at 1080: Earth was reported at 347 px and
                // measured 790, an orbit line asked for 2.6 px and drew 7.5. Everything in the
                // shot was roughly 2.2x larger than the code had asked for, uniformly enough to
                // look deliberate. Assigning the target here and then running the settling frames
                // means the numbers in the report describe the pixels in the PNG.
                PrepareCaptureViewport();

                ApplyViewAction(SessionState.GetString(ViewKey, "default"));

                // Always, not only when a view action fired: the resize alone has to reach every
                // transform, and bodies are placed in LateUpdate.
                //
                // Flying to a star needs far longer. NearbyStarSystems checks twice a second and
                // batchmode frames are unthrottled, so twenty of them can pass in a few
                // milliseconds — the capture would photograph the moment before the system was
                // generated and report, wrongly, that nothing happened.
                _framesAfterAction = SessionState.GetString(ViewKey, "default")
                    .StartsWith("star", StringComparison.Ordinal) ? 600 : 20;
                return;
            }

            if (_framesAfterAction > 0)
            {
                _framesAfterAction--;
                return;
            }

            EditorApplication.update -= Tick;
            SessionState.SetBool(PendingKey, false);

            string output = SessionState.GetString(OutputKey, "/tmp/cosmosynth-play.png");
            int exitCode = 0;

            try
            {
                // Diagnostic switch: hide renderers whose hierarchy path contains any of these,
                // then capture. "What is that large shape" is otherwise answered by guesswork —
                // this answers it by removing candidates one at a time.
                //   COSMOSYNTH_HIDE="Star Field,Atmosphere" bash playshot-cosmosynth.sh ...
                string hide = Environment.GetEnvironmentVariable("COSMOSYNTH_HIDE");
                if (!string.IsNullOrWhiteSpace(hide)) HideRenderers(hide);

                string report = Inspect() + ProbeDestinationAlpha();
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
        private static bool _settledActionsDone;
        private static int _framesAfterAction;

        /// <summary>
        /// Put the app into the state the requested view wants to photograph, using the app's own
        /// entry points.
        ///
        /// The orrery is the view that shows the orbit rendering, the labels and the decluttering
        /// doing their jobs, and it is reached through the wrist dashboard — which needs a tracked
        /// hand that no desktop run will ever have. Calling the method the dashboard calls means
        /// what is captured is the scale the app produces, including the re-centring the rung asks
        /// for. A scale computed in the harness would only prove the harness's arithmetic, which
        /// is the whole failure mode the play-mode harness exists to avoid.
        /// </summary>
        /// <returns>True if something was changed and the app should run on before capture.</returns>
        /// <summary>
        /// Put the viewer at a catalogued star and let the simulation react.
        ///
        /// Moves the floating origin rather than the camera: everything is placed relative to
        /// the origin, so moving the camera alone would leave the universe where it was and
        /// prove nothing. Offset slightly so the viewer is beside the star rather than inside
        /// it, which is where a star's own geometry would fill the entire view.
        /// </summary>
        private static bool FlyToStar(int starIndex)
        {
            var sky = UnityEngine.Object.FindAnyObjectByType<CosmoSynth.Runtime.SkyAndRings>();
            var simulation = UnityEngine.Object.FindAnyObjectByType<UniverseSimulation>();

            if (sky?.Stars == null || simulation == null)
            {
                Log("star catalogue has not streamed in yet — cannot fly to a star");
                return false;
            }

            StarCatalog stars = sky.Stars;
            int found = -1, seen = 0;
            for (int i = 0; i < stars.Count; i++)
            {
                if ((stars.Stars[i].Flags & StarFlags.DistanceUnknown) != 0) continue;
                if (seen++ == starIndex) { found = i; break; }
            }

            if (found < 0)
            {
                Log($"no star at index {starIndex} with a known distance");
                return false;
            }

            CatalogStar star = stars.Stars[found];
            Double3 starKm = star.PositionParsecs * Constants.Parsec;

            // Forty astronomical units to one side — about Neptune's distance from the Sun.
            //
            // A tenth of a parsec was the first choice and it generated the system correctly
            // while showing absolutely nothing: three trillion kilometres is far enough that a
            // star is a point and its planets are not even that. Being inside the generation
            // radius and being able to see the result are different requirements, and only the
            // capture reveals the second one.
            // Half an astronomical unit. Forty was tried first and is the right distance to be
            // *in* the system and the wrong one to photograph it: the star itself came out at a
            // quarter of a pixel. Distance and framing are separate problems and the capture is
            // what tells them apart.
            const double astronomicalUnitKm = 1.495978707e8;
            Double3 offset = new Double3(0.5 * astronomicalUnitKm, 0.0, 0.0);
            Double3 destination = starKm + offset;

            simulation.Origin.SetOrigin(destination);

            // And tell the navigator, or it puts the viewer straight back.
            //
            // On the desktop path UniverseNavigator owns the origin and rewrites it from its own
            // stored position at the end of every Update. Setting the origin alone was silently
            // undone within a frame, and the harness reported "no star within range" from a
            // viewpoint that had never moved — a false negative that looked exactly like the
            // feature not working.
            var navigator = UnityEngine.Object.FindAnyObjectByType<CosmoSynth.Runtime.UniverseNavigator>();
            if (navigator != null) navigator.AbsolutePositionKm = destination;

            // Scale for the system, not for the hologram.
            //
            // Arriving is not the same as being able to see. The render scale is still set for
            // Earth held at arm's length, and at that scale forty astronomical units is a
            // quarter of a million render units — far past the horizon where everything is
            // pinned to the sky sphere as a point. The first capture from here generated all
            // eight bodies correctly and drew none of them.
            simulation.Origin.SetUnitsPerKm(
                ScaleSpace.HologramUnitsPerKm(offset.Magnitude, 0.9));

            Log($"flew to catalogue star {found} (mag {star.ApparentMagnitude:F2}, " +
                $"{star.DistanceParsecs:F2} pc, {star.TemperatureK:F0} K) at {starKm.ToString("G6")} km");
            return true;
        }

        private static bool ApplyViewAction(string view)
        {
            // "star<N>" — fly the viewer to the Nth brightest catalogued star with a known
            // distance, so the universe layer can be verified here rather than by asking
            // somebody to put a headset on. This is the whole point of the harness: a claim that
            // approaching a star grows worlds is checkable, and an unchecked claim is a guess.
            if (view.StartsWith("star", StringComparison.Ordinal) &&
                int.TryParse(view.Substring(4), out int starIndex))
            {
                return FlyToStar(starIndex);
            }

            if (!view.StartsWith("rung", StringComparison.Ordinal)) return false;
            if (!int.TryParse(view.Substring(4), out int rung)) return false;

            var dashboard = UnityEngine.Object.FindAnyObjectByType<CosmoSynth.Runtime.WristDashboard>();
            if (dashboard == null)
            {
                Log("no WristDashboard in the scene — cannot apply a scale rung");
                return false;
            }

            dashboard.ApplyScaleRung(rung);
            Log($"applied scale rung {rung} '{CosmoSynth.Runtime.WristDashboard.ScaleRungName(rung)}'");
            return true;
        }

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

                // Orbit lines and the status card, because both have shipped in states that a
                // screenshot showed as "a line across the frame" or "nothing there" without
                // saying which of several causes it was.
                var orbits = UnityEngine.Object.FindObjectsByType<CosmoSynth.Runtime.OrbitLineRenderer>(
                    FindObjectsSortMode.None);
                int drawnOrbits = 0;
                for (int i = 0; i < orbits.Length; i++)
                {
                    string line = orbits[i].Describe();
                    if (line.Contains("drawn=y"))
                    {
                        drawnOrbits++;
                        report.AppendLine($"  orbit {line}");
                    }
                }
                report.AppendLine($"  orbits drawn: {drawnOrbits} of {orbits.Length}");

                var beacon = UnityEngine.Object.FindAnyObjectByType<CosmoSynth.Runtime.AnchorBeacon>();
                if (beacon != null) report.AppendLine($"  status card: {beacon.DescribePanel()}");

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

            AppendLargestRenderers(report);

            var modelLoader = UnityEngine.Object.FindAnyObjectByType<SpacecraftModelLoader>();
            if (modelLoader != null) report.AppendLine($"  spacecraft {modelLoader.StatusLine()}");

            var nearby = UnityEngine.Object.FindAnyObjectByType<CosmoSynth.Runtime.NearbyStarSystems>();
            if (nearby != null) report.AppendLine($"  {nearby.StatusLine()}");

            var skyForReport = UnityEngine.Object.FindAnyObjectByType<CosmoSynth.Runtime.SkyAndRings>();
            report.AppendLine(skyForReport?.Stars != null
                ? $"  star catalogue: {skyForReport.Stars.Count} loaded, brightest mag " +
                  $"{skyForReport.Stars.Stars[0].ApparentMagnitude:F2}"
                : "  star catalogue: NOT LOADED (invented sky)");

            AppendSpacecraft(report, simulation);

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
        /// The biggest things currently being drawn, by world bounds.
        ///
        /// Answers the one question a screenshot cannot: "what IS that". A large translucent
        /// shape with no obvious owner took several rounds of guessing at the source before
        /// this existed; enumerating enabled renderers by size names it in one line.
        /// </summary>
        private static void AppendLargestRenderers(StringBuilder report)
        {
            var renderers = new List<Renderer>(UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsSortMode.None));

            renderers.RemoveAll(r => r == null || !r.enabled || !r.gameObject.activeInHierarchy);

            // Sorted by APPARENT size, not world size. What fills the screen is size over
            // distance: a three-unit sphere four units away covers everything, while a
            // thousand-unit orbit line two thousand units away is a hairline. Ranking by world
            // bounds put the actual culprit off the end of the list.
            Camera camera = Camera.main;
            Vector3 eye = camera != null ? camera.transform.position : Vector3.zero;

            float Apparent(Renderer r)
            {
                float distance = Mathf.Max(0.001f, Vector3.Distance(eye, r.bounds.center));
                return r.bounds.size.magnitude / distance;
            }

            renderers.Sort((a, b) => Apparent(b).CompareTo(Apparent(a)));

            report.AppendLine($"  enabled renderers: {renderers.Count}; largest by APPARENT size " +
                              "(world size / distance):");
            for (int i = 0; i < renderers.Count && i < 12; i++)
            {
                Renderer renderer = renderers[i];
                float distance = Vector3.Distance(eye, renderer.bounds.center);
                report.AppendLine(
                    $"    {Shorten(HierarchyPath(renderer.transform)),-46} apparent={Apparent(renderer),8:G4} " +
                    $"world={renderer.bounds.size.magnitude,9:G4} d={distance,9:G4} " +
                    $"shader={(renderer.sharedMaterial != null ? renderer.sharedMaterial.shader.name : "(none)")}");
            }
        }

        /// <summary>
        /// Does anything in the scene write destination alpha?
        ///
        /// On Quest the compositor reads the framebuffer's alpha as **coverage**: alpha 0 shows
        /// the room, alpha 1 shows our pixel. So an additive effect that leaks into alpha does
        /// not merely look wrong, it punches an opaque hole in passthrough. The star material
        /// intends `Blend One One, Zero One` but `Particles/Standard Unlit` exposes no separate
        /// alpha blend, so whether alpha is protected rests on whether that built-in shader
        /// happens to declare `ColorMask RGB` — and its source is compiled into the editor, so
        /// it cannot simply be read.
        ///
        /// It can, however, be measured. The normal capture forces an opaque clear so the PNG is
        /// viewable; this clears alpha to zero instead and reports what came back. Anything above
        /// zero in the sky is a pixel that would be opaque on the device. That turns "needs a
        /// headset" into a number, which matters because this is invisible in every desktop
        /// screenshot ever taken of this app.
        /// </summary>
        private static string ProbeDestinationAlpha()
        {
            var rig = UnityEngine.Object.FindAnyObjectByType<XrRig>();
            Camera camera = rig != null ? rig.Camera : Camera.main;
            if (camera == null) return string.Empty;

            var bands = camera.GetComponent<CosmoSynth.Runtime.DepthBands>();
            Camera far = bands != null ? bands.FarCamera : null;

            var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };

            RenderTexture previousTarget = camera.targetTexture;
            CameraClearFlags previousFlags = camera.clearFlags;
            Color previousColor = camera.backgroundColor;
            RenderTexture previousFarTarget = far != null ? far.targetTexture : null;
            Color previousFarColor = far != null ? far.backgroundColor : Color.black;

            var transparent = new Color(0f, 0f, 0f, 0f);

            try
            {
                if (far != null)
                {
                    far.backgroundColor = transparent;
                    far.targetTexture = target;
                    far.Render();
                    camera.clearFlags = CameraClearFlags.Depth;
                }
                else
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = transparent;
                }

                camera.targetTexture = target;
                camera.Render();

                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = target;

                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                RenderTexture.active = previousActive;

                Color32[] data = pixels.GetPixels32();
                int opaque = 0, partial = 0;
                byte peak = 0;
                foreach (Color32 pixel in data)
                {
                    if (pixel.a > peak) peak = pixel.a;
                    if (pixel.a >= 250) opaque++;
                    else if (pixel.a > 2) partial++;
                }

                UnityEngine.Object.DestroyImmediate(pixels);

                double total = data.Length;
                // Alpha coverage is not a defect on its own — a solid planet is *supposed* to
                // occlude the room, and writes alpha 1 to say so. What would be a defect is an
                // additive effect leaking into alpha, because it would punch a hole in
                // passthrough where the user should see straight through. The two are told apart
                // by attribution, not by the total: re-run with
                // COSMOSYNTH_HIDE="Star Field" (or any suspect) and see whether these numbers
                // move. Measured 2026-07-31: hiding the star field changed them not at all
                // (18.55% either way), and hiding the opaque bodies took them to zero — so the
                // stars do not write alpha and passthrough is safe from them.
                return $"\n  passthrough alpha probe (cleared to 0): peak alpha {peak}/255, " +
                       $"{opaque / total:P2} fully opaque ({opaque} px), " +
                       $"{partial / total:P2} partial ({partial} px)\n" +
                       "    Coverage is expected from opaque bodies and UI. To attribute it, re-run " +
                       "with COSMOSYNTH_HIDE and watch these move.\n";
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.clearFlags = previousFlags;
                camera.backgroundColor = previousColor;
                if (far != null)
                {
                    far.targetTexture = previousFarTarget;
                    far.backgroundColor = previousFarColor;
                }
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void HideRenderers(string patterns)
        {
            string[] wanted = patterns.Split(',');
            int hidden = 0;

            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(
                         FindObjectsSortMode.None))
            {
                if (renderer == null || !renderer.enabled) continue;

                string path = HierarchyPath(renderer.transform);
                foreach (string pattern in wanted)
                {
                    string trimmed = pattern.Trim();
                    if (trimmed.Length == 0) continue;
                    if (path.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    renderer.enabled = false;
                    hidden++;
                    break;
                }
            }

            Log($"diagnostic: hid {hidden} renderers matching '{patterns}'");
        }

        private static string Shorten(string path) =>
            path.Length <= 46 ? path : "..." + path.Substring(path.Length - 43);

        private static string HierarchyPath(Transform transform)
        {
            string name = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                name = parent.name + "/" + name;
            return name;
        }

        /// <summary>
        /// What the spacecraft layer actually did.
        ///
        /// The two numbers that matter are whether each one is inside its coverage window and
        /// how far it is being exaggerated to stay visible. Both are invisible in a screenshot:
        /// a spacecraft correctly hidden because the clock is outside its ephemeris looks
        /// exactly like one that failed to load, and a model drawn at 40 000 times life size
        /// looks like a model drawn at any other size.
        /// </summary>
        private static void AppendSpacecraft(StringBuilder report, UniverseSimulation simulation)
        {
            var craft = new List<CelestialBodyView>();
            foreach (CelestialBodyView view in simulation.Views.Values)
            {
                if (view != null && view.Body != null && view.Body.Type == BodyType.Spacecraft)
                    craft.Add(view);
            }

            if (craft.Count == 0)
            {
                report.AppendLine("  spacecraft: none in the catalogue");
                return;
            }

            craft.Sort((a, b) => string.CompareOrdinal(a.Body.Id, b.Body.Id));

            int known = 0, withModel = 0;
            foreach (CelestialBodyView view in craft)
            {
                if (view.IsKnown) known++;
                if (view.HasModel) withModel++;
            }

            report.AppendLine($"  spacecraft: {craft.Count} in catalogue, {known} within their " +
                              $"coverage window, {withModel} with geometry");

            foreach (CelestialBodyView view in craft)
            {
                string placement = view.Body.Site.HasValue ? "landed"
                    : view.Body.Trajectory != null ? "sampled"
                    : view.Body.Orbit.HasValue ? "elements" : "none";

                var renderer = view.GetComponentInChildren<MeshRenderer>();
                bool drawn = renderer != null && renderer.enabled;

                report.AppendLine(
                    $"    {view.Body.Name,-28} {placement,-8} parent={view.Body.ParentId,-8} " +
                    $"{(view.IsKnown ? "known  " : "OUTSIDE")} " +
                    $"model={(view.HasModel ? $"{view.ModelTriangleCount,5} tris" : "     none")} " +
                    $"size x{view.SizeExaggeration,-10:G4} {(drawn ? "drawn" : "hidden")}");
            }
        }

        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        private static RenderTexture _captureTarget;

        /// <summary>
        /// The buffer the app renders into for the rest of the session.
        ///
        /// <b>No MSAA, deliberately.</b> It was 4x here and 8x in SceneShot, and the Android
        /// quality level this actually ships on has none at all — so every shot was being judged
        /// at an edge quality the device does not have. That is the worst direction for a
        /// harness to be wrong in: the things it flattered are exactly the things this renderer
        /// is full of and a headset is worst at, namely one-pixel orbit lines, three-pixel
        /// spacecraft, star cores and the polygonal limb of a planet. A shot should be a
        /// pessimistic view of the device, not an optimistic one.
        /// </summary>
        private static RenderTexture CreateCaptureTarget() =>
            new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };

        /// <summary>
        /// Point both band cameras at the capture target, so the app spends its settling frames
        /// sized to the picture that will be saved. See the call site for why this matters.
        /// </summary>
        private static void PrepareCaptureViewport()
        {
            var rig = UnityEngine.Object.FindAnyObjectByType<XrRig>();
            Camera camera = rig != null ? rig.Camera : Camera.main;
            if (camera == null) return;

            _captureTarget = CreateCaptureTarget();
            camera.targetTexture = _captureTarget;

            // Both, or ViewportInfo answers one question for the near band and another for the
            // far one — and the far band is where the entire star field lives.
            var bands = camera.GetComponent<CosmoSynth.Runtime.DepthBands>();
            if (bands != null && bands.FarCamera != null) bands.FarCamera.targetTexture = _captureTarget;

            Log($"capture viewport: {CaptureWidth}x{CaptureHeight}, no MSAA — " +
                "apparent-size rules now run at the resolution the shot is saved at");
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


            RenderTexture target = _captureTarget != null ? _captureTarget : CreateCaptureTarget();

            // BOTH bands, far first.
            //
            // Rendering only the main camera captures the near band alone — 5 cm to a few metres
            // — and silently discards everything beyond it. That is most of the scene: the star
            // field, every planet that is not the one in front of you, the whole outer system.
            // The shot came back with a black background and looked like a rendering bug rather
            // than like a harness that was only being shown half the frame. Compositing the pair
            // the same way the device does is the only capture worth trusting.
            var bands = camera.GetComponent<CosmoSynth.Runtime.DepthBands>();
            Camera far = bands != null ? bands.FarCamera : null;

            // The app clears to alpha 0 for passthrough. With no compositor behind it that would
            // save as a transparent PNG showing nothing, so force an opaque clear for the
            // capture only — and say so, because it is a deviation from what the device does.
            var opaque = new Color(0.004f, 0.005f, 0.011f, 1f);

            RenderTexture previousTarget = camera.targetTexture;
            CameraClearFlags previousFlags = camera.clearFlags;
            Color previousColor = camera.backgroundColor;

            RenderTexture previousFarTarget = far != null ? far.targetTexture : null;
            Color previousFarColor = far != null ? far.backgroundColor : Color.black;

            if (far != null)
            {
                // The far camera owns the colour clear now, so it is the one that has to be made
                // opaque; the near camera must stay on Depth or it erases what far just drew.
                far.backgroundColor = opaque;
                far.targetTexture = target;
                far.Render();

                camera.clearFlags = CameraClearFlags.Depth;
            }
            else
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = opaque;
            }

            camera.targetTexture = target;
            camera.Render();

            var image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false);
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            camera.targetTexture = previousTarget;
            camera.clearFlags = previousFlags;
            camera.backgroundColor = previousColor;

            if (far != null)
            {
                far.targetTexture = previousFarTarget;
                far.backgroundColor = previousFarColor;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, image.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(image);

            // The target is now the same texture the app has been rendering into since the
            // settle, so restoring it above pointed both cameras back at the buffer we are about
            // to release. The editor exits on the next line and would never notice, but a camera
            // holding a destroyed render texture is exactly the kind of leftover that surfaces
            // later attached to some unrelated change.
            if (target == _captureTarget)
            {
                camera.targetTexture = null;
                if (far != null) far.targetTexture = null;
                _captureTarget = null;
            }

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
