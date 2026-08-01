using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Bakes procedural surface textures for catalogue bodies, off the main thread.
    ///
    /// The cost is the reason this is not trivial. A 512x256 albedo map is 131 000 pixels, each
    /// needing several octaves of 3D noise for elevation plus more for moisture and grain —
    /// call it a few million noise evaluations per body, times thirty-one bodies. Done inline
    /// that is tens of seconds of frozen frames, which on a headset is not a slow load, it is
    /// motion sickness.
    ///
    /// So: the pixel work runs on a thread-pool worker (it touches only <see cref="CosmoSynth.Core"/>,
    /// which is engine-free and therefore thread-safe), and only the final
    /// <c>SetPixels32</c>/<c>Apply</c> happens on the main thread, one body per frame. Bodies
    /// are baked in order of how large they currently appear, so whatever the user is actually
    /// looking at resolves first and the rest fill in behind it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Planet Texture Baker")]
    public sealed class PlanetTextureBaker : MonoBehaviour
    {
        [Header("Resolution")]
        [Tooltip("Albedo width for a major body. Height is half this.")]
        [SerializeField] private int _majorWidth = 512;

        [Tooltip("Albedo width for a small body (under 1000 km radius).")]
        [SerializeField] private int _minorWidth = 256;

        [Header("Budget")]
        [Tooltip("Maximum textures applied per frame. Applying is a main-thread GPU upload.")]
        [SerializeField] private int _uploadsPerFrame = 1;

        [Tooltip("Maximum bakes running at once. Keep below the core count so the sim keeps its slice.")]
        [SerializeField] private int _maxConcurrentBakes = 3;

        [Header("Budget guards")]
        [Tooltip("Never bake more than this many surfaces. Each 512x256 map costs ~0.5 MB of VRAM.")]
        [SerializeField] private int _maxTextures = 48;

        [Tooltip("Do not bake a body that has never appeared larger than this, in pixels.")]
        [SerializeField] private double _minimumApparentPixels = 8.0;

        private UniverseSimulation _simulation;
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly HashSet<string> _requested = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<BakeResult> _ready = new Queue<BakeResult>();
        private int _running;

        /// <summary>Bodies whose texture has been generated and applied.</summary>
        public int Baked { get; private set; }

        /// <summary>Bodies still queued or in flight.</summary>
        public int Pending => _requested.Count - Baked;

        private sealed class BakeResult
        {
            public string BodyId;
            public Color32[] Albedo;
            /// <summary>Tangent-space normals from the height field — terrain relief without geometry.</summary>
            public Color32[] Normal;
            /// <summary>Unity metallic/smoothness packing: RGB metallic, ALPHA smoothness.</summary>
            public Color32[] Surface;
            public int Width;
            public int Height;
        }

        private void Awake() => _simulation = FindAnyObjectByType<UniverseSimulation>();

        private void OnDestroy()
        {
            foreach (Texture2D texture in _textures.Values)
            {
                if (texture != null) Destroy(texture);
            }
            _textures.Clear();
        }

        private bool _measuredMapsLoaded;

        private void Update()
        {
            if (_simulation?.Catalog == null) return;

            if (!_measuredMapsLoaded)
            {
                _measuredMapsLoaded = true;
                StartCoroutine(LoadMeasuredMaps());
            }

            ApplyReadyTextures();
            RequestNextBakes();
        }

        /// <summary>
        /// Load real NASA imagery for the bodies that have it, in preference to generating one.
        ///
        /// A measured map beats anything the generator can invent, and it changes what the app
        /// is entitled to claim: those bodies flip from Procedural to Measured in the provenance
        /// panel, with the mission and the citation attached. Bodies with no map keep the
        /// procedural surface and keep saying so — which is the whole point of tracking this per
        /// field rather than per object.
        /// </summary>
        private IEnumerator LoadMeasuredMaps()
        {
            string manifestJson = null;
            yield return _simulation.ReadStreamingAsset("CosmoSynth/surfaces/surfaces.json",
                result => manifestJson = result);

            if (string.IsNullOrEmpty(manifestJson))
            {
                Debug.Log("[CosmoSynth] no measured surface maps found; all surfaces will be procedural. " +
                          "Run 'python3 tools/cosmodata/fetch_surface_maps.py' to fetch them.");
                yield break;
            }

            JsonValue manifest;
            try
            {
                manifest = JsonValue.Parse(manifestJson);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CosmoSynth] surfaces.json is malformed: {exception.Message}");
                yield break;
            }

            JsonValue maps = manifest["maps"];
            foreach (string bodyId in maps.Keys)
            {
                JsonValue entry = maps[bodyId];
                string file = entry["file"].AsString();
                if (string.IsNullOrEmpty(file)) continue;

                byte[] bytes = null;
                yield return ReadStreamingAssetBytes($"CosmoSynth/surfaces/{file}", result => bytes = result);
                if (bytes == null) continue;

                // Loaded READABLE, unlike before: the water mask below is derived from these
                // pixels, and marking the texture non-readable here would make them unreachable.
                // It is released after the derivation.
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
                if (!texture.LoadImage(bytes, markNonReadable: false))
                {
                    Debug.LogError($"[CosmoSynth] could not decode surface map '{file}'");
                    Destroy(texture);
                    continue;
                }

                texture.wrapModeU = TextureWrapMode.Repeat;
                texture.wrapModeV = TextureWrapMode.Clamp;
                texture.anisoLevel = 4;

                _textures[bodyId] = texture;

                // Claiming this body so the generator does not overwrite measured imagery with
                // an invention a few frames later.
                _requested.Add(bodyId);
                Baked++;

                if (_simulation.Views.TryGetValue(bodyId, out CelestialBodyView view))
                {
                    Texture2D water = DeriveWaterMap(texture, out double waterFraction);
                    view.ApplySurfaceTexture(texture, null, water);

                    // Upgrade the provenance: this surface is now a measurement, with a source.
                    view.Body.Provenance.Set("surface", FieldProvenance.Measured(
                        entry["source"].AsString(), entry["reference"].AsString(),
                        retrievedUtc: entry["retrieved"].AsString()));

                    // The water mask is NOT measured. It is a classifier run over a measured
                    // image, which is a different and weaker claim, and the provenance system
                    // exists precisely so the difference is recorded rather than assumed.
                    if (water != null)
                    {
                        view.Body.Provenance.Set("surface.water", FieldProvenance.Inferred(
                            $"classified from {entry["source"].AsString()} by colour and luminance"));
                        Debug.Log($"[CosmoSynth] water mask for '{bodyId}': {waterFraction:P1} of the surface");
                    }
                }

                // No longer needed on the CPU. Freeing it here rather than at load keeps the
                // derivation above possible without holding a copy of every map for the session.
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

                Debug.Log($"[CosmoSynth] measured surface for '{bodyId}': " +
                          $"{entry["width"].AsInt()}x{entry["height"].AsInt()} — {entry["source"].AsString()}");
            }
        }

        /// <summary>
        /// Classify open water in a measured albedo map, so oceans can catch the sun.
        ///
        /// The alternative was to reuse the procedural terrain's water mask, and it would have
        /// been wrong in the most visible way possible: the generated coastlines have nothing to
        /// do with the real ones, so the glint would fall on Brazil. Deriving the mask from the
        /// measured image itself is the only way it lines up with the continents the user is
        /// looking at.
        ///
        /// The test is that open water is both blue-dominant and dark — Blue Marble's deep ocean
        /// runs about (10, 25, 50) — while vegetation, rock and desert are red or green
        /// dominant, and cloud, snow and ice are bright. That brightness cut is doing real work:
        /// without it, cloud over ocean classifies as water and the glint appears on top of the
        /// weather. It also means an airless grey body like the Moon classifies as entirely dry
        /// on its own, with no special case.
        ///
        /// Returns null when the body has no appreciable water, so nothing pays for a texture
        /// that would be uniformly zero.
        /// </summary>
        private static Texture2D DeriveWaterMap(Texture2D albedo, out double waterFraction)
        {
            waterFraction = 0.0;
            if (albedo == null) return null;

            Color32[] pixels;
            try
            {
                pixels = albedo.GetPixels32();
            }
            catch (UnityException)
            {
                // Not readable for some reason; a missing glint is not worth an exception.
                return null;
            }

            var mask = new Color32[pixels.Length];
            int wet = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                float r = p.r / 255f, g = p.g / 255f, b = p.b / 255f;
                float luminance = 0.2126f * r + 0.7152f * g + 0.0722f * b;

                // Blue-dominance, measured against the warmer channels together so that cyan ice
                // and pale desert are both excluded.
                float blueness = b - Mathf.Max(r, g);

                float water = Mathf.Clamp01(blueness * 9f) * Mathf.Clamp01((0.42f - luminance) * 5f);
                if (water > 0.5f) wet++;

                // Metallic stays zero; SMOOTHNESS IS THE ALPHA CHANNEL for Unity's Standard
                // shader, and 0.03 to 0.88 is the span from wet rock to open sea.
                mask[i] = new Color32(0, 0, 0, (byte)(Mathf.Lerp(0.03f, 0.88f, water) * 255f));
            }

            waterFraction = wet / (double)pixels.Length;

            // Below a percent there is nothing worth a texture — that is a compression artefact
            // or a blue-tinted crater, not an ocean.
            if (waterFraction < 0.01) return null;

            var texture = new Texture2D(albedo.width, albedo.height, TextureFormat.RGBA32,
                mipChain: true, linear: true)
            {
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            texture.SetPixels32(mask);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return texture;
        }

        private IEnumerator ReadStreamingAssetBytes(string relativePath, Action<byte[]> onComplete)
        {
            string fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, relativePath);

            if (!fullPath.Contains("://"))
            {
                onComplete?.Invoke(System.IO.File.Exists(fullPath) ? System.IO.File.ReadAllBytes(fullPath) : null);
                yield break;
            }

            using (var request = UnityEngine.Networking.UnityWebRequest.Get(fullPath))
            {
                yield return request.SendWebRequest();
                onComplete?.Invoke(
                    request.result == UnityEngine.Networking.UnityWebRequest.Result.Success
                        ? request.downloadHandler.data
                        : null);
            }
        }

        private void ApplyReadyTextures()
        {
            int applied = 0;
            while (applied < _uploadsPerFrame && _ready.Count > 0)
            {
                BakeResult result;
                lock (_ready) result = _ready.Dequeue();

                var texture = new Texture2D(result.Width, result.Height, TextureFormat.RGBA32, mipChain: true)
                {
                    wrapModeU = TextureWrapMode.Repeat,   // longitude wraps...
                    wrapModeV = TextureWrapMode.Clamp,    // ...latitude does not
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 4
                };
                texture.SetPixels32(result.Albedo);
                texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);

                Texture2D normal = BuildMap(result.Normal, result.Width, result.Height, linear: true);
                Texture2D surface = BuildMap(result.Surface, result.Width, result.Height, linear: true);

                _textures[result.BodyId] = texture;
                if (_simulation.Views.TryGetValue(result.BodyId, out CelestialBodyView view))
                {
                    view.ApplySurfaceTexture(texture, normal, surface);
                }

                Baked++;
                applied++;
            }
        }

        private void RequestNextBakes()
        {
            if (_running >= _maxConcurrentBakes) return;

            CelestialBodyView next = SelectNextBody();
            if (next == null) return;

            _requested.Add(next.Body.Id);
            StartBake(next.Body);
        }

        /// <summary>
        /// Pick the un-baked body with the largest apparent size — so what the user is looking at
        /// becomes real first, and a moon on the far side of the system waits its turn.
        /// </summary>
        private CelestialBodyView SelectNextBody()
        {
            // Hard cap on how many surfaces exist at once.
            //
            // With the exoplanet catalogue merged there are over twelve hundred bodies. Baking a
            // 512x256 map for each is roughly 600 MB of texture memory and several minutes of
            // multi-octave noise — enough to get the process killed outright, which is exactly
            // what happened the first time the two catalogues were loaded together. A body
            // that is a handful of pixels across gains nothing from a surface map anyway; it
            // keeps its catalogue colour, which for a star is its real black-body colour.
            if (_textures.Count >= _maxTextures) return null;

            CelestialBodyView best = null;
            double bestPixels = _minimumApparentPixels;

            foreach (CelestialBodyView view in _simulation.Views.Values)
            {
                if (view == null || _requested.Contains(view.Body.Id)) continue;

                double pixels = view.ApparentDiameterPixels;
                if (pixels > bestPixels)
                {
                    bestPixels = pixels;
                    best = view;
                }
            }
            return best;
        }

        private void StartBake(CelestialBody body)
        {
            Interlocked.Increment(ref _running);

            double radius = body.EquatorialRadiusKm ?? 1000.0;
            int width = radius >= 1000.0 ? _majorWidth : _minorWidth;
            int height = width / 2;

            // Everything captured here is either a value type or belongs to CosmoSynth.Core,
            // which has no Unity dependency and no shared mutable state — so it is safe to touch
            // from a worker. Nothing below this line may call a UnityEngine API.
            // Measured surface temperature where the catalogue has one, otherwise equilibrium
            // plus a greenhouse estimate. Equilibrium alone freezes Earth's oceans.
            double equilibrium = SurfacePainter.SurfaceTemperatureFor(body, EquilibriumTemperatureFor(body));
            var baseColor = new ColorRgb(
                (float)body.DisplayColor.x, (float)body.DisplayColor.y, (float)body.DisplayColor.z);
            string bodyId = body.Id;

            TerrainField terrain = SurfacePainter.TerrainFor(body, equilibrium);

            Task.Run(() =>
            {
                try
                {
                    BakeResult baked = BakeMaps(terrain, equilibrium, baseColor, width, height);
                    baked.BodyId = bodyId;
                    lock (_ready) _ready.Enqueue(baked);
                }
                catch (Exception exception)
                {
                    // A worker exception would otherwise vanish into the task and the body would
                    // simply never get a texture, with nothing said.
                    Debug.LogError($"[CosmoSynth] texture bake failed for '{bodyId}': {exception.Message}");
                }
                finally
                {
                    Interlocked.Decrement(ref _running);
                }
            });
        }

        /// <summary>
        /// Render an equirectangular albedo map.
        ///
        /// Equirectangular because that is the layout Unity's built-in sphere UVs expect, so no
        /// custom mesh or shader is needed. It over-samples the poles badly, which is why the
        /// cube-sphere exists for real terrain — but for a colour map viewed from outside, the
        /// distortion is invisible and the simplicity is worth a great deal.
        /// </summary>
        /// <summary>Upload a generated map. Normal and surface maps must be linear, not sRGB.</summary>
        private static Texture2D BuildMap(Color32[] pixels, int width, int height, bool linear)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: true, linear: linear)
            {
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return texture;
        }

        /// <summary>
        /// Generate albedo, normal and metallic/smoothness in one pass over the sphere.
        ///
        /// Doing all three together matters: they are sampled from the same terrain field at the
        /// same points, so the sun glint appears exactly where the ocean is drawn and the relief
        /// follows the coastline instead of crossing it. Generating them in separate passes
        /// invites exactly that kind of drift.
        /// </summary>
        private static BakeResult BakeMaps(TerrainField terrain, double surfaceTemperatureK,
            ColorRgb baseColor, int width, int height)
        {
            var albedo = new Color32[width * height];
            var normal = new Color32[width * height];
            var surface = new Color32[width * height];

            // Finite-difference step for the normals, in radians of arc.
            double step = MathD.TwoPi / width;

            for (int y = 0; y < height; y++)
            {
                double latitude = (y / (double)(height - 1) - 0.5) * MathD.Pi;
                double cosLat = System.Math.Cos(latitude);
                double sinLat = System.Math.Sin(latitude);
                int row = y * width;

                for (int x = 0; x < width; x++)
                {
                    double longitude = x / (double)width * MathD.TwoPi;
                    Double3 direction = new Double3(
                        cosLat * System.Math.Cos(longitude), cosLat * System.Math.Sin(longitude), sinLat);

                    albedo[row + x] = ToColor32(
                        SurfacePainter.Paint(terrain, direction, surfaceTemperatureK, baseColor));

                    // --- Normal, from the height field's slope ---
                    //
                    // Sampling the height at four neighbours and differencing gives the surface
                    // gradient. Near the poles the longitude step shrinks to nothing, so it is
                    // divided by cos(latitude) — without that, polar terrain shows a hard
                    // pinch where the normals blow up.
                    double hL = SurfacePainter.HeightAt(terrain, Spherical(latitude, longitude - step));
                    double hR = SurfacePainter.HeightAt(terrain, Spherical(latitude, longitude + step));
                    double hD = SurfacePainter.HeightAt(terrain, Spherical(latitude - step, longitude));
                    double hU = SurfacePainter.HeightAt(terrain, Spherical(latitude + step, longitude));

                    double lonScale = System.Math.Max(0.15, cosLat);
                    double dx = (hR - hL) / (2.0 * lonScale);
                    double dy = (hU - hD) * 0.5;

                    // Relief strength scales with the body's actual relief against its radius,
                    // so a rugged little moon looks rugged and a smooth gas giant stays smooth.
                    double relief = MathD.Clamp(terrain.ReliefKm / System.Math.Max(1.0, terrain.BodyRadiusKm) * 90.0,
                        0.0, 3.0);

                    var n = new Double3(-dx * relief, -dy * relief, 1.0).Normalized;
                    normal[row + x] = new Color32(
                        (byte)((n.x * 0.5 + 0.5) * 255), (byte)((n.y * 0.5 + 0.5) * 255), 
                        (byte)((n.z * 0.5 + 0.5) * 255), 255);

                    // --- Metallic / smoothness ---
                    //
                    // Unity's Standard shader reads metallic from RGB and SMOOTHNESS FROM ALPHA.
                    // Nothing here is metallic; what varies is roughness, and water is the only
                    // common surface that is genuinely smooth.
                    double water = SurfacePainter.WaterMask(terrain, direction, surfaceTemperatureK);
                    byte smoothness = (byte)(MathD.Lerp(0.03, 0.85, water) * 255);
                    surface[row + x] = new Color32(0, 0, 0, smoothness);
                }
            }

            return new BakeResult
            {
                Albedo = albedo, Normal = normal, Surface = surface, Width = width, Height = height
            };
        }

        private static Double3 Spherical(double latitude, double longitude)
        {
            double cosLat = System.Math.Cos(latitude);
            return new Double3(cosLat * System.Math.Cos(longitude), cosLat * System.Math.Sin(longitude),
                System.Math.Sin(latitude));
        }

        private static Color32[] BakeAlbedo(TerrainField terrain, double equilibriumTemperatureK,
            ColorRgb baseColor, int width, int height)
        {
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                // v=0 at the south pole, matching Unity's sphere UV convention.
                double latitude = (y / (double)(height - 1) - 0.5) * MathD.Pi;
                double cosLat = System.Math.Cos(latitude);
                double sinLat = System.Math.Sin(latitude);
                int row = y * width;

                for (int x = 0; x < width; x++)
                {
                    double longitude = x / (double)width * MathD.TwoPi;

                    var direction = new Double3(
                        cosLat * System.Math.Cos(longitude),
                        cosLat * System.Math.Sin(longitude),
                        sinLat);

                    ColorRgb color = SurfacePainter.Paint(terrain, direction, equilibriumTemperatureK, baseColor);
                    pixels[row + x] = ToColor32(color);
                }
            }

            return pixels;
        }

        private static Color32 ToColor32(ColorRgb color)
        {
            // Linear to sRGB, because the texture is sampled as sRGB by the standard shader.
            return new Color32(
                (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(color.R)) * 255f),
                (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(color.G)) * 255f),
                (byte)(Mathf.Clamp01(Mathf.LinearToGammaSpace(color.B)) * 255f),
                255);
        }

        private double EquilibriumTemperatureFor(CelestialBody body)
        {
            // Distance to the star, through whatever chain of parents this body hangs off — a
            // moon's temperature is set by its planet's orbit, not its own.
            CelestialBody star = null;
            foreach (string rootId in _simulation.Catalog.RootIds)
            {
                CelestialBody candidate = _simulation.Catalog.Get(rootId);
                if (candidate != null && candidate.IsStar) { star = candidate; break; }
            }

            if (star?.LuminosityWatts == null) return 255.0;

            CelestialBody current = body;
            double distanceKm = 0.0;
            for (int depth = 0; depth < 8 && current != null; depth++)
            {
                if (current.Orbit.HasValue) distanceKm = current.Orbit.Value.SemiMajorAxisKm;
                if (current.ParentId == star.Id) break;
                current = _simulation.Catalog.Get(current.ParentId);
            }

            if (distanceKm <= 0.0) return 5772.0;   // the star itself
            return Constants.EquilibriumTemperature(star.LuminosityWatts.Value, distanceKm,
                body.BondAlbedo ?? 0.3);
        }
    }
}
