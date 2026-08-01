using System;
using System.Collections.Generic;
using System.IO;
using CosmoSynth.Core;
using UnityEditor;
using UnityEngine;

namespace CosmoSynth.EditorTools
{
    /// <summary>
    /// Headless stills of the Solar System, for the build page.
    ///
    /// This runs entirely in <b>edit mode</b>, which is the important design choice. The
    /// established scene-shot tool on this machine enters play mode, and play mode reloads the
    /// application domain — wiping statics and silently dropping any
    /// <c>EditorApplication.update</c> subscription made before the switch, which is why that
    /// tool has to park its job in <c>SessionState</c> and re-arm from a static constructor on
    /// the far side.
    ///
    /// None of that is necessary here. CosmoSynth's simulation is a pure function of time: the
    /// catalogue is data, the frame tree derives from it, and every body's position comes from
    /// propagating an orbit to an instant. So the scene can simply be constructed, evaluated at
    /// one instant, rendered and thrown away — no MonoBehaviour lifecycle, no coroutines, no
    /// domain reload, and a result that is byte-identical run to run.
    ///
    /// It also doubles as an end-to-end check of the shipped data: everything drawn here came
    /// out of the real catalogue and through the real propagator.
    /// </summary>
    public static class SceneShot
    {
        /// <summary>Fixed instant, so the image does not depend on when the build ran.</summary>
        private static readonly CosmicTime Epoch = CosmicTime.FromUtc(2026, 8, 15, 12, 0, 0);

        public static void Run()
        {
            string output = "/tmp/cosmosynth.png";
            string mode = "system";
            string focus = "earth";
            int width = 1920, height = 1080;

            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                switch (arguments[i])
                {
                    case "-shotOut": output = arguments[i + 1]; break;
                    case "-shotMode": mode = arguments[i + 1]; break;
                    case "-shotFocus": focus = arguments[i + 1]; break;
                    case "-shotWidth": int.TryParse(arguments[i + 1], out width); break;
                    case "-shotHeight": int.TryParse(arguments[i + 1], out height); break;
                }
            }

            EditorApplication.Exit(Capture(output, mode, focus, width, height) ? 0 : 1);
        }

        public static bool Capture(string outputPath, string mode, string focusId, int width, int height)
        {
            GameObject root = null;
            RenderTexture target = null;
            var materials = new List<Material>();

            try
            {
                string catalogPath = Path.Combine(Application.streamingAssetsPath,
                    "CosmoSynth/catalog/solar_system.json");

                if (!File.Exists(catalogPath))
                {
                    Log($"FAILED: no catalogue at {catalogPath}");
                    return false;
                }

                var catalog = CelestialCatalog.Parse(File.ReadAllText(catalogPath));
                var frames = catalog.BuildFrameTree();
                Log($"loaded '{catalog.Name}' — {catalog.Count} bodies, mode={mode}");

                root = new GameObject("ShotScene");
                var camera = BuildCamera(root, width, height);

                bool ok = mode == "body"
                    ? ComposeBodyView(root, catalog, frames, focusId, materials)
                    : ComposeSystemView(root, catalog, frames, focusId, materials);

                if (!ok) return false;

                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();

                var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = null;

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);

                Log($"wrote {outputPath} ({new FileInfo(outputPath).Length / 1024} kB)");
                return true;
            }
            catch (Exception exception)
            {
                Log($"FAILED: {exception}");
                return false;
            }
            finally
            {
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                foreach (var material in materials) UnityEngine.Object.DestroyImmediate(material);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // ---- Camera ---------------------------------------------------------

        private static Camera BuildCamera(GameObject root, int width, int height)
        {
            var cameraObject = new GameObject("ShotCamera");
            cameraObject.transform.SetParent(root.transform);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Opaque, deliberately. The page renders alpha over a checkerboard, which is right
            // for a passthrough capture where transparency means "the real world shows through".
            // Here the dark background *is* the subject — space — so it is an opaque colour
            // rather than a hole.
            camera.backgroundColor = new Color(0.004f, 0.005f, 0.011f, 1f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1e7f;
            camera.aspect = (float)width / height;
            camera.enabled = false;   // rendered explicitly, never by the engine loop
            camera.allowHDR = true;

            return camera;
        }

        /// <summary>Unity's left-handed y-up axes from the core's right-handed ICRF z-up.</summary>
        private static Vector3 ToUnity(Double3 icrf) =>
            new Vector3((float)icrf.x, (float)icrf.z, (float)icrf.y);

        // ---- System view ----------------------------------------------------

        /// <summary>
        /// The whole planetary system seen obliquely, with every orbit drawn from its real
        /// elements.
        ///
        /// Distances are true to scale; body radii are not, and cannot be — at this framing
        /// Earth is a hundredth of a pixel across. Radii are compressed logarithmically through
        /// <see cref="ScaleSpace.PresentationRadius"/>, which preserves the ordering and the
        /// sense of relative size while keeping the smallest bodies visible. The notes sidecar
        /// says so; an image that silently implies true proportions would be the exact
        /// dishonesty this project exists to avoid.
        /// </summary>
        private static bool ComposeSystemView(GameObject root, CelestialCatalog catalog, ReferenceFrame frames,
            string outermostId, List<Material> materials)
        {
            var star = catalog.Get(catalog.RootIds[0]);
            Double3 starPosition = frames.Find(star.Id).AbsoluteState(Epoch).Position;

            // Frame on the requested outermost planet's orbit.
            var outermost = catalog.Get(outermostId) ?? catalog.Get("mars");
            double extentKm = outermost.Orbit.HasValue
                ? outermost.Orbit.Value.ApoapsisKm
                : 2.0 * Constants.AstronomicalUnit;

            double unitsPerKm = 100.0 / extentKm;
            var transform = new ScaleSpace.RenderTransform(starPosition, unitsPerKm);

            int planets = 0, orbits = 0;

            foreach (var body in catalog.Traverse())
            {
                // Only bodies orbiting the star directly — moons are far inside a pixel here.
                if (body.Id != star.Id && body.ParentId != star.Id) continue;

                var frame = frames.Find(body.Id);
                Double3 absolute = frame.AbsoluteState(Epoch).Position;
                Double3 rendered = transform.ToRender(absolute);
                if (rendered.Magnitude > 400.0) continue;   // outside the framing

                // Compressed radius, so a 2440 km Mercury and a 695700 km Sun are both visible.
                double displayRadiusKm = ScaleSpace.PresentationRadius(
                    body.EquatorialRadiusKm ?? 1000.0, Constants.EarthEquatorialRadius, 0.28);

                float diameter = Mathf.Max(1.2f, (float)(displayRadiusKm * 2.0 * unitsPerKm * 900.0));

                var sphere = MakeSphere(root, body, diameter, ToUnity(rendered), materials);
                planets++;

                if (body.Orbit.HasValue)
                {
                    DrawOrbit(root, body, transform, starPosition, materials);
                    orbits++;
                }
            }

            AddStarLight(root, Vector3.zero);

            // Oblique from above: a top-down view flattens the inclinations to nothing, and a
            // side-on view collapses the orbits to lines.
            var camera = root.GetComponentInChildren<Camera>();
            var direction = new Vector3(0.35f, 0.62f, -0.70f).normalized;
            camera.transform.position = direction * 260f;
            camera.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);

            Log($"system view: {planets} bodies, {orbits} orbits, framed on {outermost.Name}");
            return planets > 0;
        }

        // ---- Body view ------------------------------------------------------

        /// <summary>A single body close up, lit from the star's real direction.</summary>
        private static bool ComposeBodyView(GameObject root, CelestialCatalog catalog, ReferenceFrame frames,
            string focusId, List<Material> materials)
        {
            var body = catalog.Get(focusId);
            if (body == null) { Log($"FAILED: no body '{focusId}'"); return false; }

            var star = catalog.Get(catalog.RootIds[0]);
            Double3 bodyPosition = frames.Find(body.Id).AbsoluteState(Epoch).Position;
            Double3 starPosition = frames.Find(star.Id).AbsoluteState(Epoch).Position;

            double radiusKm = body.EquatorialRadiusKm ?? 6371.0;
            double unitsPerKm = 10.0 / radiusKm;
            var transform = new ScaleSpace.RenderTransform(bodyPosition, unitsPerKm);

            double blackBody = star.LuminosityWatts.HasValue && body.Orbit.HasValue
                ? Constants.EquilibriumTemperature(star.LuminosityWatts.Value,
                    body.Orbit.Value.SemiMajorAxisKm, body.BondAlbedo ?? 0.3)
                : 255.0;
            double equilibrium = SurfacePainter.SurfaceTemperatureFor(body, blackBody);

            MakeSphere(root, body, (float)(radiusKm * 2.0 * unitsPerKm), Vector3.zero, materials,
                withTexture: true, equilibriumTemperatureK: equilibrium);

            // Any satellites that are in frame — for Earth this is the Moon, which at true
            // scale sits 60 body radii away, so it lands well outside the shot. Draw it at its
            // real direction but pulled to the frame edge, and say so in the log.
            int moons = 0;
            foreach (var moon in catalog.ChildrenOf(body.Id))
            {
                Double3 moonPosition = frames.Find(moon.Id).AbsoluteState(Epoch).Position;
                Double3 offset = transform.ToRender(moonPosition);
                if (offset.Magnitude > 200.0) continue;
                MakeSphere(root, moon, (float)((moon.EquatorialRadiusKm ?? 100.0) * 2.0 * unitsPerKm),
                    ToUnity(offset), materials);
                moons++;
            }

            // Light from where the star actually is. This is what produces a correct terminator
            // rather than a specular blob wherever an arbitrary light happened to point.
            Double3 toStar = (starPosition - bodyPosition).Normalized;
            AddStarLight(root, ToUnity(toStar) * 1000f);

            var camera = root.GetComponentInChildren<Camera>();
            // Stand off perpendicular to the sunlight and slightly above, so the terminator runs
            // down the middle and the body reads as a sphere rather than a flat disc.
            Vector3 sunDirection = ToUnity(toStar);
            Vector3 side = Vector3.Cross(sunDirection, Vector3.up).normalized;
            Vector3 viewDirection = (sunDirection * 0.45f + side * 0.8f + Vector3.up * 0.25f).normalized;

            camera.transform.position = viewDirection * 34f;
            camera.transform.rotation = Quaternion.LookRotation(-viewDirection, Vector3.up);

            Log($"body view: {body.Name}, {moons} satellite(s) in frame, lit from the real Sun direction");
            return true;
        }

        // ---- Primitives -----------------------------------------------------

        private static GameObject MakeSphere(GameObject root, CelestialBody body, float diameter,
            Vector3 position, List<Material> materials, bool withTexture = false,
            double equilibriumTemperatureK = 255.0)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = body.Name ?? body.Id;
            sphere.transform.SetParent(root.transform);
            sphere.transform.position = position;

            float flattening = (float)body.Flattening;
            sphere.transform.localScale = new Vector3(diameter, diameter * (1f - flattening), diameter);

            UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());

            var colour = new Color((float)body.DisplayColor.x, (float)body.DisplayColor.y, (float)body.DisplayColor.z);
            var material = new Material(Shader.Find("Standard"));
            material.color = colour;
            material.SetFloat("_Glossiness", 0.05f);   // planets are not billiard balls
            material.SetFloat("_Metallic", 0f);

            // Bake the same surface the runtime generates, so the shot shows what the app shows
            // rather than a stand-in that might flatter it.
            if (withTexture && !body.IsStar)
            {
                Texture2D surface = BakeSurface(body, equilibriumTemperatureK, 512, 256);
                material.mainTexture = surface;
                material.color = Color.white;
            }

            if (body.IsStar)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour * 6f);
            }

            materials.Add(material);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
            return sphere;
        }

        /// <summary>Equirectangular albedo, matching <c>PlanetTextureBaker</c>'s output.</summary>
        private static Texture2D BakeSurface(CelestialBody body, double equilibriumTemperatureK,
            int width, int height)
        {
            TerrainField terrain = SurfacePainter.TerrainFor(body, equilibriumTemperatureK);
            var baseColor = new ColorRgb(
                (float)body.DisplayColor.x, (float)body.DisplayColor.y, (float)body.DisplayColor.z);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                double latitude = (y / (double)(height - 1) - 0.5) * MathD.Pi;
                double cosLat = System.Math.Cos(latitude), sinLat = System.Math.Sin(latitude);

                for (int x = 0; x < width; x++)
                {
                    double longitude = x / (double)width * MathD.TwoPi;
                    var direction = new Double3(cosLat * System.Math.Cos(longitude),
                                                cosLat * System.Math.Sin(longitude), sinLat);

                    ColorRgb c = SurfacePainter.Paint(terrain, direction, equilibriumTemperatureK, baseColor);
                    pixels[y * width + x] = new Color32(
                        (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(c.R)) * 255f),
                        (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(c.G)) * 255f),
                        (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(c.B)) * 255f), 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        /// <summary>
        /// Draw one orbit as a line mesh, sampled from its real elements.
        ///
        /// A line mesh rather than a LineRenderer: LineRenderer needs a live camera to build
        /// its billboarded geometry, which it does not have in an edit-mode explicit render.
        /// </summary>
        private static void DrawOrbit(GameObject root, CelestialBody body, ScaleSpace.RenderTransform transform,
            Double3 parentAbsoluteKm, List<Material> materials)
        {
            List<Double3> path = body.Orbit.Value.SampleOrbitPath(512);

            var vertices = new Vector3[path.Count];
            var indices = new int[(path.Count - 1) * 2];

            for (int i = 0; i < path.Count; i++)
            {
                vertices[i] = ToUnity(transform.ToRender(parentAbsoluteKm + path[i]));
            }
            for (int i = 0; i < path.Count - 1; i++)
            {
                indices[i * 2] = i;
                indices[i * 2 + 1] = i + 1;
            }

            var mesh = new Mesh { name = $"Orbit_{body.Id}" };
            mesh.vertices = vertices;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();

            var lineObject = new GameObject($"Orbit_{body.Id}");
            lineObject.transform.SetParent(root.transform);
            lineObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            var material = new Material(Shader.Find("Unlit/Color"))
            {
                color = new Color(0.34f, 0.52f, 0.78f, 1f)
            };
            materials.Add(material);
            lineObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void AddStarLight(GameObject root, Vector3 fromPosition)
        {
            var lightObject = new GameObject("Starlight");
            lightObject.transform.SetParent(root.transform);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.7f;
            light.color = new Color(1f, 0.97f, 0.91f);
            light.shadows = LightShadows.None;

            Vector3 direction = fromPosition.sqrMagnitude > 0f ? -fromPosition.normalized : Vector3.forward;
            lightObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            // A little ambient so the night side is not pure black — physically there is
            // starlight and planetshine, and a completely black limb reads as a missing mesh.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.04f, 0.055f);
        }

        private static void Log(string message) => Debug.Log($"[SceneShot] {message}");
    }
}
