using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The things that make a solar system read as a place rather than as spheres on black:
    /// a star field, planetary rings, a point-source Sun, and atmospheric limbs.
    ///
    /// All four are cheap and all four are load-bearing. A ringless Saturn reads as wrong to
    /// anyone; a directional "sun" gives every planet the same lighting direction regardless of
    /// where it is, which quietly destroys the sense that they orbit anything; and a planet with
    /// an atmosphere but no limb glow looks like a billiard ball.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Sky And Rings")]
    public sealed class SkyAndRings : MonoBehaviour
    {
        [Header("Star field")]
        [Tooltip("Number of background stars. Procedural — no real catalogue is loaded.")]
        [SerializeField] private int _starCount = 3200;

        [Tooltip("How many real stars to draw, brightest first. The catalogue holds 119613 down " +
                 "to magnitude 13; a headset draws the bright end of it.")]
        [SerializeField] private int _catalogStarBudget = 9000;

        [Tooltip("Radius of the star sphere in render units. Sits beyond everything else.")]
        [SerializeField] private float _starSphereRadius = 900f;

        [Header("Rings")]
        [SerializeField] private int _ringSegments = 128;

        /// <summary>The real sky, once it has streamed in. Null until then, and on failure.</summary>
        private StarCatalog _catalog;

        /// <summary>
        /// The loaded star catalogue, or null until it arrives. Read lazily by anything that
        /// needs the stars — resolving it in Awake would cache a null forever, which is the
        /// shape of three bugs this project has already shipped.
        /// </summary>
        public StarCatalog Stars => _catalog;
        private GameObject _starField;

        private readonly List<Material> _materials = new List<Material>();
        private readonly List<GameObject> _created = new List<GameObject>();

        /// <summary>
        /// Ring systems, inner and outer radius in km, from the body's centre.
        /// Real figures — Saturn's main rings run from the inner C ring to the outer A ring.
        /// </summary>
        private static readonly Dictionary<string, (double innerKm, double outerKm, float opacity)> Rings =
            new Dictionary<string, (double, double, float)>
            {
                { "saturn",  (74500.0, 140220.0, 0.85f) },
                { "uranus",  (41800.0,  51150.0, 0.25f) },   // narrow and dark
                { "jupiter", (92000.0, 122500.0, 0.10f) },   // gossamer, barely visible
                { "neptune", (41900.0,  62933.0, 0.15f) },
            };

        /// <summary>
        /// Build the sky for the catalogue as it now stands. Safe to call again.
        ///
        /// It is called again: the exoplanet catalogue is merged in after the solar system has
        /// already loaded, and that raises CatalogLoaded a second time. Everything created last
        /// time has to go first, or the scene ends up with two star fields, two suns lighting it
        /// from the same point at double intensity, and two atmospheres per planet — each
        /// additive, so every limb renders twice as bright as the physics says.
        /// </summary>
        public void Build(UniverseSimulation simulation, Transform anchor)
        {
            Clear();

            BuildStarField(anchor);

            // The real sky, once. Build() runs again on every catalogue merge; the file does not
            // change between those and re-reading 2.9 MB three times would be pure waste.
            if (!_catalogRequested && isActiveAndEnabled)
            {
                _catalogRequested = true;
                StartCoroutine(LoadStarCatalogRoutine(simulation, anchor));
            }

            BuildRings(simulation);
            BuildStarLight(simulation, anchor);
            BuildAtmospheres(simulation);
        }

        /// <summary>Destroy everything a previous Build created.</summary>
        private void Clear()
        {
            foreach (GameObject created in _created)
            {
                if (created != null) Destroy(created);
            }
            _created.Clear();

            foreach (Material material in _materials)
            {
                if (material != null) Destroy(material);
            }
            _materials.Clear();
        }

        /// <summary>Registers an object for teardown and returns it, so callers stay one-liners.</summary>
        private GameObject Own(GameObject created)
        {
            _created.Add(created);
            return created;
        }

        // ---- Star field ------------------------------------------------------

        /// <summary>
        /// A procedural star field on a large sphere.
        ///
        /// Marked procedural and meant honestly: these are not real stars in real places. A Gaia
        /// import would put them where they belong, and the provenance system already has a
        /// class for the difference. What this does get right is the *statistics* — a power-law
        /// magnitude distribution so faint stars vastly outnumber bright ones, and colour drawn
        /// from black-body temperature rather than picked at random, so the field reads as a sky
        /// instead of as confetti.
        /// </summary>
        // ---- The brightest few -----------------------------------------------
        //
        // These say how rare "conspicuous" is. Driven by the raw uniform draw, so the fractions
        // are read directly: above 0.965 is the top 3.5% of stars, which over the roughly 380 in
        // view at any moment is about a dozen — the order of a real constellation's worth of
        // obviously bright stars. The ceiling is set just short of 1 so the effect is fully on
        // for the top handful rather than only ever asymptotically approached.

        /// <summary>Uniform draw above which a star starts to get a halo at all.</summary>
        /// <summary>Apparent magnitude at which a catalogued star starts earning a halo.</summary>
        private const float CatalogBrightMagnitudeFloor = 3.4f;

        /// <summary>...and the magnitude at which it gets the full treatment. Sirius is -1.44.</summary>
        private const float CatalogBrightMagnitudeCeiling = -1.5f;

        private const float BrightStarFloor = 0.965f;

        /// <summary>Uniform draw at which the halo, second core and spikes are at full strength.</summary>
        private const float BrightStarCeiling = 0.9995f;

        /// <summary>Halo width, as a multiple of the core quad. Wide and faint.</summary>
        private const float HaloScale = 2.8f;

        /// <summary>Halo brightness, as a fraction of the star's own colour.</summary>
        private const float HaloBrightness = 0.5f;

        /// <summary>Spike length and thickness, as multiples of the core quad.</summary>
        private const float SpikeLength = 3.6f;
        private const float SpikeWidth = 0.22f;

        /// <summary>Spike brightness, as a fraction of the star's own colour.</summary>
        private const float SpikeBrightness = 0.42f;

        /// <summary>
        /// Append one camera-facing quad carrying the shared star point-spread.
        ///
        /// The two half-extents are separate so the same helper draws a round core, a wide halo
        /// and a thin elongated spike — a radially symmetric sprite on a non-square quad is an
        /// elliptical Gaussian, which is all a diffraction spike needs to be.
        /// </summary>
        private static void AddQuad(List<Vector3> vertices, List<Color> colors, List<Vector2> uvs,
            List<int> triangles, Vector3 centre, Vector3 right, Vector3 up, Color colour)
        {
            int b = vertices.Count;

            vertices.Add(centre - right - up);
            vertices.Add(centre + right - up);
            vertices.Add(centre + right + up);
            vertices.Add(centre - right + up);

            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(1, 1));
            uvs.Add(new Vector2(0, 1));

            for (int c = 0; c < 4; c++) colors.Add(colour);

            triangles.Add(b); triangles.Add(b + 2); triangles.Add(b + 1);
            triangles.Add(b); triangles.Add(b + 3); triangles.Add(b + 2);
        }

        /// <summary>
        /// Emit one star's geometry. The single place a star becomes triangles, so the invented
        /// sky and the measured one cannot drift apart in appearance — the defect StarSprite's
        /// own docstring warned about and which happened anyway when the two populations kept
        /// separate size and brightness rules.
        /// </summary>
        /// <param name="direction">Unit vector toward the star, ICRF.</param>
        /// <param name="rank">Normalised brightness, 0 at the visibility floor and 1 at the top.
        /// Drives both the drawn size and the colour intensity, because a star has one apparent
        /// magnitude and on a display both channels are expressions of it.</param>
        /// <param name="bright">How far into the bright end this star is, 0 to 1. Graded
        /// separately from <paramref name="rank"/> because rank saturates at the very top and
        /// therefore cannot order the handful of stars that get a halo and spikes.</param>
        private void EmitStar(List<Vector3> vertices, List<Color> colors, List<Vector2> uvs,
            List<int> triangles, Double3 direction, float rank, float bright, double temperatureK)
        {
            float size = _starSphereRadius * StarLook.QuadRadians(rank) * 0.5f;

            // Colour from a black-body temperature drawn across the main sequence: many cool
            // red dwarfs, few hot blue stars.
            Double3 rgb = PlanetGenerator.BlackBodyColor(temperatureK);

            // Magnitude goes into the RGB, not the alpha.
            //
            // It was in the alpha, and the alpha was doing nothing. Measured across forty
            // stars in a captured frame, every one of them peaked at 63/255 in its brightest
            // channel — the faintest six-pixel star and the brightest nineteen-pixel one
            // alike — so the dial that was supposed to carry the entire magnitude range was
            // carrying none of it, and size had been enlarged to compensate for a signal
            // that had simply been disconnected. An additive blend consumes colour, not
            // coverage; see StarLook.
            var colour = StarLook.Colour(rgb, rank);

            // Camera-facing quad. Billboarding in a shader would be better; at this count the
            // difference is not measurable and a fixed quad needs no custom shader.
            var centre = new Vector3((float)direction.x, (float)direction.z, (float)direction.y)
                     * _starSphereRadius;

            Vector3 forward = centre.normalized;
            Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
            if (right.sqrMagnitude < 1e-9f) right = Vector3.right;
            Vector3 up = Vector3.Cross(forward, right).normalized;

            AddQuad(vertices, colors, uvs, triangles, centre, right * size, up * size, colour);

            // ---- The bright end, which one quad cannot reach --------------------------
            //
            // Measured across three renders with completely different quad sizes and
            // profiles, the brightest a SINGLE additive star quad ever gets is about 0.27 of
            // full scale — 69/255 in the last frame, with three separate stars landing on
            // exactly that value. Widening the core removed the width-dependence of the gain
            // (it is now flat at 0.25 to 0.27 from the faintest star to the brightest) but it
            // did not raise the ceiling. So no amount of size or colour tuning produces a
            // star that stands out, and the sky stays a field where everything is within a
            // factor of two of everything else.
            //
            // Additive geometry sums in the framebuffer, so overlapping quads is the way
            // past it — and it is not a workaround, it is what the thing actually looks like.
            // A bright point source is a saturated core, an extended halo several times
            // wider, and diffraction spikes. Drawing those as separate pieces decouples "how
            // bright" from "how big", which is precisely the tension that made the first
            // attempt cotton wool: brightness had been gated by width, and width was what
            // ruined it.
            //
            // Driven by the raw uniform draw rather than by rank. Rank is rank = u^2.2, which
            // saturates against 1.0 for the top few stars — that is why three of them
            // rendered at identical values — so it cannot grade the very top. u can.
            if (bright <= 0.01f) return;

            // Second core, exactly on the first. This is the part that actually breaks the
            // ceiling: two overlapping cores reach ~0.54 where one reaches ~0.27.
            Color core = colour * bright; core.a = 1f;
            AddQuad(vertices, colors, uvs, triangles, centre, right * size, up * size, core);

            // The halo: wide and faint. It is what reads as "bright" at a glance, because the
            // eye judges a point source by the glow around it long before it can resolve the
            // core, and it adds to the centre as well.
            Color halo = colour * (bright * HaloBrightness); halo.a = 1f;
            AddQuad(vertices, colors, uvs, triangles, centre,
                right * (size * HaloScale), up * (size * HaloScale), halo);

            // Diffraction spikes, from geometry alone — no new shader, no new texture.
            //
            // The sprite is radially symmetric, so a quad stretched long in one axis and thin
            // in the other samples it as an elongated Gaussian, which is exactly what a spike
            // looks like. Two crossed ones give the four-point star. Confined to this handful
            // because spikes on every star is the look that destroys a sky rather than making
            // one: they are a property of the instrument, and instruments only show them on
            // sources bright enough to saturate.
            Color spike = colour * (bright * SpikeBrightness); spike.a = 1f;
            AddQuad(vertices, colors, uvs, triangles, centre,
                right * (size * SpikeLength), up * (size * SpikeWidth), spike);
            AddQuad(vertices, colors, uvs, triangles, centre,
                right * (size * SpikeWidth), up * (size * SpikeLength), spike);
        }

        /// <summary>Path under StreamingAssets to the real star catalogue.</summary>
        private const string CatalogPath = "CosmoSynth/sky/stars.csstars";

        private bool _catalogRequested;

        /// <summary>
        /// Stream the real sky in, then rebuild the field with it.
        ///
        /// Asynchronous because on Android StreamingAssets lives compressed inside the APK and
        /// only UnityWebRequest can reach it — and read once rather than per Build(), because
        /// Build runs again on every catalogue merge and the file is 2.9 MB. Until it lands the
        /// invented sky is on screen, which is the same graceful degradation the spacecraft
        /// meshes use: something correct-looking immediately, the truth a moment later.
        /// </summary>
        private System.Collections.IEnumerator LoadStarCatalogRoutine(UniverseSimulation simulation,
            Transform anchor)
        {
            byte[] data = null;
            yield return simulation.ReadStreamingAssetBytes(CatalogPath, result => data = result);

            if (data == null)
            {
                Debug.LogWarning(
                    $"[CosmoSynth] no star catalogue at StreamingAssets/{CatalogPath}; keeping the " +
                    "invented sky. Build it with 'python3 tools/cosmodata/build_starfield.py'.");
                yield break;
            }

            try
            {
                _catalog = StarCatalog.Parse(data, _catalogStarBudget);
            }
            catch (System.Exception exception)
            {
                // A malformed catalogue must not take the sky with it. The invented field is
                // already drawn and stays drawn.
                Debug.LogError($"[CosmoSynth] star catalogue is unreadable: {exception.Message}");
                yield break;
            }

            // Rebuild only the star field. Calling Build() again would tear down and recreate the
            // rings, the star light and every atmosphere shell for no reason.
            if (_starField != null) Destroy(_starField);
            BuildStarField(anchor);
        }

        /// <summary>
        /// The real sky: 119 613 catalogued stars at measured positions, brightest first.
        ///
        /// Only a prefix is drawn. The catalogue reaches magnitude 13, which no headset can
        /// show and no eye could see, so the budget buys the bright end — and because the file
        /// is sorted by magnitude the choice is a length rather than a search. Magnitude 6 is
        /// the naked-eye limit and about five thousand stars reach it, which is *fewer* than
        /// the invented field this replaces: the real sky costs nothing extra.
        ///
        /// Distance is deliberately discarded here. These are painted on the sky sphere at a
        /// fixed radius because from anywhere inside the solar system the parallax of even the
        /// nearest star is far below one pixel — the direction is the whole of what is visible.
        /// The measured distance stays in the catalogue for anything that needs it, and it is
        /// what the interstellar phase will fly along.
        /// </summary>
        private void EmitCatalogSky(List<Vector3> vertices, List<Color> colors,
            List<Vector2> uvs, List<int> triangles)
        {
            int drawn = System.Math.Min(_catalog.Count, _catalogStarBudget);

            for (int i = 0; i < drawn; i++)
            {
                CatalogStar star = _catalog.Stars[i];

                float rank = StarLook.BrightnessFromMagnitude(star.ApparentMagnitude);

                // The bright end is graded by magnitude, not by the rank above, for the same
                // reason the invented sky grades it by the raw draw: rank saturates at the top
                // and cannot order the few stars that earn a halo and spikes. Real magnitudes
                // order themselves — this window puts roughly the fifty brightest stars in,
                // which is about what a dark sky shows as obviously bright.
                float bright = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                    CatalogBrightMagnitudeFloor, CatalogBrightMagnitudeCeiling,
                    (float)star.ApparentMagnitude));

                EmitStar(vertices, colors, uvs, triangles,
                    star.Direction, rank, bright, star.TemperatureK);
            }

            Debug.Log($"[CosmoSynth] star field: {drawn} real stars of {_catalog.Count} " +
                      $"(to magnitude {_catalog.Stars[drawn - 1].ApparentMagnitude:F1}), " +
                      $"{vertices.Count} vertices");
        }

        /// <summary>
        /// The fallback sky, used only when the catalogue is missing or unreadable.
        ///
        /// Kept rather than deleted: a headset showing no stars at all because a file failed to
        /// load looks like a broken app, and this project has shipped that failure before. It is
        /// statistically plausible and honestly labelled — but it is not the sky, and the
        /// provenance says Procedural where the catalogue says Measured.
        /// </summary>
        private void EmitInventedSky(List<Vector3> vertices, List<Color> colors,
            List<Vector2> uvs, List<int> triangles)
        {
            var random = new DeterministicRandom(20260730);

            for (int i = 0; i < _starCount; i++)
            {
                Double3 direction = random.OnUnitSphere();
                double draw = random.Unit();

                // ONE rank, driving both size and brightness. Splitting them into independent
                // draws made the bright end impossible rather than rare: rendered peak depends
                // on drawn width, so a star only lands conspicuously bright when it draws high
                // on both, which over the stars in view happens about once.
                float rank = (float)System.Math.Pow(draw, 2.2);

                float bright = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(BrightStarFloor, BrightStarCeiling, (float)draw));

                double temperature = 2600.0 + System.Math.Pow(random.Unit(), 2.6) * 22000.0;

                EmitStar(vertices, colors, uvs, triangles, direction, rank, bright, temperature);
            }

            Debug.Log($"[CosmoSynth] star field: {_starCount} INVENTED stars " +
                      "(catalogue missing — run tools/cosmodata/build_starfield.py)");
        }

        private void BuildStarField(Transform anchor)
        {
            // Lists, not fixed arrays: the brightest stars are drawn with more than one quad.
            int budget = _catalog != null ? _catalog.Count : _starCount;
            var vertices = new List<Vector3>(budget * 5);
            var colors = new List<Color>(budget * 5);
            var uvs = new List<Vector2>(budget * 5);
            var triangles = new List<int>(budget * 8);

            if (_catalog != null) EmitCatalogSky(vertices, colors, uvs, triangles);
            else EmitInventedSky(vertices, colors, uvs, triangles);

            var mesh = new Mesh { name = "StarField", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * _starSphereRadius * 4f);

            var starObject = Own(new GameObject("Star Field"));
            _starField = starObject;
            starObject.transform.SetParent(anchor, false);
            starObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = starObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Soft round dots, not squares.
            //
            // These quads carried no texture, and Unity's default _MainTex is solid white — so
            // every star rendered as a hard-edged coloured tile and the sky read as a field of
            // confetti. StarSprite supplies the point-spread the quad was always missing, and
            // the same one the catalogue stars use, so the two cannot look like different
            // things. It also handles the passthrough blend: additive colour with destination
            // alpha left alone, or every star stamps a black hole into the room.
            Material material = StarSprite.CreateMaterial();
            material.renderQueue = 1000;                          // behind everything
            _materials.Add(material);
            renderer.sharedMaterial = material;
        }

        // ---- Rings -----------------------------------------------------------

        private void BuildRings(UniverseSimulation simulation)
        {
            foreach (KeyValuePair<string, (double innerKm, double outerKm, float opacity)> entry in Rings)
            {
                if (!simulation.Views.TryGetValue(entry.Key, out CelestialBodyView view)) continue;
                BuildRing(view, entry.Value.innerKm, entry.Value.outerKm, entry.Value.opacity, simulation);
            }
        }

        private void BuildRing(CelestialBodyView view, double innerKm, double outerKm, float opacity,
            UniverseSimulation simulation)
        {
            // Built in units of the BODY'S OWN RADIUS, not in render units.
            //
            // Baking the current render scale into the vertices makes the rings correct at
            // exactly one zoom level: zoom out and the planets shrink while the rings do not,
            // until Saturn's rings are larger than the solar system. RingSystem rescales the
            // transform from the body's drawn radius every frame instead, so the geometry is
            // scale-free and stays right everywhere.
            double bodyRadiusKm = view.Body.EquatorialRadiusKm ?? 1.0;
            float inner = (float)(innerKm / bodyRadiusKm);
            float outer = (float)(outerKm / bodyRadiusKm);

            var vertices = new Vector3[(_ringSegments + 1) * 2];
            var uvs = new Vector2[(_ringSegments + 1) * 2];
            var triangles = new int[_ringSegments * 6];

            for (int i = 0; i <= _ringSegments; i++)
            {
                float angle = i / (float)_ringSegments * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                vertices[i * 2 + 0] = direction * inner;
                vertices[i * 2 + 1] = direction * outer;
                // u runs across the ring, so the texture's radial banding maps to it.
                uvs[i * 2 + 0] = new Vector2(0f, i / (float)_ringSegments);
                uvs[i * 2 + 1] = new Vector2(1f, i / (float)_ringSegments);
            }

            for (int i = 0; i < _ringSegments; i++)
            {
                int t = i * 6, v = i * 2;
                triangles[t + 0] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = $"Rings_{view.Body.Id}" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            var ringObject = Own(new GameObject($"Rings_{view.Body.Id}"));
            // Parented to the body so it inherits the axial tilt — which is what makes Saturn's
            // rings open and close over its orbit, exactly as they do from Earth.
            ringObject.transform.SetParent(view.transform, false);
            ringObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            ringObject.AddComponent<RingSystem>().Track(view);

            var renderer = ringObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var material = CosmoShaders.Material(CosmoShaders.UnlitTransparent);
            material.mainTexture = BuildRingTexture(view.Body.Id, opacity);
            material.renderQueue = 3000;
            _materials.Add(material);
            renderer.sharedMaterial = material;
        }

        /// <summary>
        /// A radial ring texture: banded opacity with the Cassini division.
        ///
        /// Rings are not a solid disc — they are billions of independent particles whose density
        /// varies sharply with radius because of orbital resonances with the moons. The gaps are
        /// the visually distinctive part, so they are what this reproduces.
        /// </summary>
        private static Texture2D BuildRingTexture(string bodyId, float opacity)
        {
            const int width = 256;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            long seed = Hash.HashString(bodyId + "-rings");
            var pixels = new Color32[width];

            for (int x = 0; x < width; x++)
            {
                double t = x / (double)(width - 1);

                // Banding from noise, so no two ring systems look identical.
                double band = NoiseD.Fbm(seed, new Double3(t * 26.0, 0, 0),
                    new NoiseD.FractalSettings { Octaves = 5, Frequency = 1.0, Lacunarity = 2.3, Persistence = 0.6 });
                double density = MathD.Clamp01(0.55 + band * 0.45);

                // The Cassini division, at roughly 68% of the way out through Saturn's rings.
                if (bodyId == "saturn")
                {
                    double distanceFromGap = System.Math.Abs(t - 0.63);
                    if (distanceFromGap < 0.035) density *= MathD.SmoothStep(0.0, 0.035, distanceFromGap);
                    // Encke gap, narrower and further out.
                    if (System.Math.Abs(t - 0.90) < 0.012) density *= 0.25;
                }

                // Fade at both edges so the ring does not end in a hard line.
                density *= MathD.SmoothStep(0.0, 0.06, t) * MathD.SmoothStep(0.0, 0.06, 1.0 - t);

                // Ring particles are dirty ice: bright and slightly warm.
                var colour = new Color(0.88f, 0.84f, 0.76f, (float)density * opacity);
                pixels[x] = colour;
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        // ---- Lighting --------------------------------------------------------

        /// <summary>
        /// Light the system from the star's actual position, as a point source.
        ///
        /// A directional light gives every body the same lighting direction wherever it is, so
        /// planets on opposite sides of the Sun are lit identically — which reads as obviously
        /// wrong the moment you look at two of them at once. A point light at the star produces
        /// correct terminators and correct phases for free.
        /// </summary>
        private void BuildStarLight(UniverseSimulation simulation, Transform anchor)
        {
            if (simulation.Catalog == null || simulation.Catalog.RootIds.Count == 0) return;

            string starId = simulation.Catalog.RootIds[0];
            if (!simulation.Views.TryGetValue(starId, out CelestialBodyView starView)) return;

            var lightObject = Own(new GameObject("Star Point Light"));
            lightObject.transform.SetParent(starView.transform, false);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;

            // The star's own colour, from its own temperature.
            //
            // This was hardcoded to (1, 0.97, 0.92) — near enough for the Sun and wrong for every
            // other star the catalogue can hold. Core already computes black-body colour and the
            // catalogue already carries the temperature, so there is no reason for a G2V constant
            // to be baked into the renderer; an M dwarf's system should be lit orange.
            double temperature = starView.Body.EffectiveTemperatureK ?? 5772.0;
            Double3 rgb = PlanetGenerator.BlackBodyColor(temperature);
            light.color = new Color((float)rgb.x, (float)rgb.y, (float)rgb.z);

            // 3.2 blew every planet out to pure white. Unity's point light barely attenuates
            // when the range is far larger than the distance, so the intensity here is close to
            // what actually lands on the surface.
            light.intensity = 0.85f;
            light.shadows = LightShadows.None;   // mobile GPU; shadows cost more here than they add

            // Per-pixel, explicitly.
            //
            // The Android quality level this ships on allows exactly ONE per-pixel light, and
            // Unity decides which by its own importance heuristic — in which a directional light
            // ranks highly whatever its intensity, because it has infinite range. So the 0.05
            // fill below could take the only per-pixel slot and demote the star that lights the
            // entire solar system to a vertex light, on the device only. That does not look like
            // a bug, it looks like flat, terminator-less planets: correct geometry, no modelling,
            // no obvious cause, and nothing whatever in the editor to reproduce it against.
            light.renderMode = LightRenderMode.ForcePixel;

            // Reach the whole system. Unity's point light falls off to zero at its range, so this
            // has to cover the outermost body or Neptune sits in the dark.
            light.range = 1e6f;

            // A dim fill so night sides read as unlit rather than as missing geometry.
            var ambientObject = Own(new GameObject("Ambient Fill"));
            ambientObject.transform.SetParent(anchor, false);
            var ambient = ambientObject.AddComponent<Light>();
            ambient.type = LightType.Directional;
            ambient.intensity = 0.05f;
            ambient.color = new Color(0.5f, 0.55f, 0.7f);
            ambient.shadows = LightShadows.None;
            // Never the per-pixel one. See the star light above: this exists to keep a night side
            // off pure black, and it must not outrank the light that models every lit face.
            ambient.renderMode = LightRenderMode.ForceVertex;
            ambientObject.transform.rotation = Quaternion.Euler(45f, 130f, 0f);
        }

        // ---- Atmospheres -----------------------------------------------------

        /// <summary>
        /// A single-scattering atmosphere on every body that has air, integrated per pixel from
        /// that body's own Rayleigh and Mie coefficients.
        ///
        /// The shader ray-marches the shell rather than tinting it, which is what buys the
        /// effects a flat translucent sphere can never show: the limb brightening toward the
        /// edge of the disc, the red band at the terminator where sunlight has crossed the most
        /// air, and the forward haze around the star. The numbers driving it are the catalogue's
        /// measured per-body coefficients, so Earth's limb is blue, Mars's is butterscotch and
        /// Titan's is orange for the reason they actually are — not because three constants were
        /// chosen to look like it.
        /// </summary>
        private void BuildAtmospheres(UniverseSimulation simulation)
        {
            foreach (CelestialBodyView view in simulation.Views.Values)
            {
                AtmosphereDefinition atmosphere = view.Body.Atmosphere;
                if (atmosphere == null || !atmosphere.IsPresent) continue;

                double radiusKm = view.Body.EquatorialRadiusKm ?? 1000.0;

                // Shell height from the catalogue's own top-of-atmosphere, not from a round
                // number of scale heights.
                //
                // It matters more than it sounds. Four scale heights puts Earth's shell 34 km up
                // — half a percent of the radius, which is about two pixels of limb on a
                // full-disc view and reads as a faint outline rather than as air. The catalogued
                // top is 100 km, the altitude the atmosphere is genuinely optically finished by,
                // and it gives three times the limb depth for free. The column's optical depth is
                // unchanged either way: it is fixed by the scattering coefficients and the scale
                // height, both of which stay exactly as measured. Only the geometry the light
                // travels through gets the right shape.
                double topKm = atmosphere.TopAltitudeKm > 0.0
                    ? atmosphere.TopAltitudeKm
                    : atmosphere.ScaleHeightKm * 4.0;

                // Pluto's atmosphere genuinely reaches beyond its own radius, and a shell that
                // large stops looking like a planet with air and starts looking like a bubble
                // with a rock in it. Cap the drawn extent; the physics is untouched.
                topKm = System.Math.Min(topKm, radiusKm * 0.28);

                float relativeScale = (float)((radiusKm + topKm) / radiusKm);

                // Density falls by 1/e over one scale height. Expressed as a fraction of the
                // shell, that is what makes a thin atmosphere hug the ground and a puffy one
                // fill its whole shell — the difference between Earth's hairline limb and
                // Titan's deep orange haze.
                float densityFalloff = Mathf.Clamp((float)(atmosphere.ScaleHeightKm / topKm), 0.03f, 1f);

                var shell = Own(GameObject.CreatePrimitive(PrimitiveType.Sphere));
                shell.name = $"Atmosphere_{view.Body.Id}";
                DestroyImmediate(shell.GetComponent<Collider>());
                shell.transform.SetParent(view.transform, false);

                // The same subdivided sphere the planet inside it now uses.
                //
                // Giving the planet a finer mesh and leaving its atmosphere on the 20-segment
                // primitive puts a faceted halo around a smooth world, which is worse than the
                // faceted pair it replaced: the eye reads the mismatch long before it reads
                // either edge on its own. And the shell is the more exposed of the two, because
                // it is a limb glow — brightest exactly at the silhouette, which is precisely
                // where the polygon corners are. There are nine of these, not two hundred, and
                // the scattering shader raymarches in world space so it never looks at a UV.
                var shellFilter = shell.GetComponent<MeshFilter>();
                Mesh shellMesh = CosmoSphere.Detailed(shellFilter.sharedMesh);
                if (shellMesh != null) shellFilter.sharedMesh = shellMesh;

                // Sized from the body's *rendered* sphere, tracked every frame.
                //
                // Scaling it against the view transform instead is wrong in a way that is easy
                // to miss: the view transform is always unit scale, so every atmosphere came out
                // the same half-unit radius no matter how large the planet was drawn — swamping
                // Earth in a cyan shell twice its size while the camera sat almost inside it.
                var tracker = shell.AddComponent<AtmosphereShell>();
                tracker.Track(view, relativeScale);

                var renderer = shell.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var material = CosmoShaders.Material(CosmoShaders.Atmosphere);
                material.SetFloat("_ScaleHeight", densityFalloff);
                material.SetFloat("_MieG", (float)atmosphere.MieAsymmetry);

                // Sample counts are a straight quality-for-frame-time trade, and this runs on a
                // mobile GPU at 72 Hz per eye. Ten view samples is enough that the limb reads as
                // a gradient rather than as bands; four light samples is enough for a terminator.
                material.SetFloat("_Samples", 10f);
                material.SetFloat("_LightSamples", 4f);
                material.SetFloat("_Intensity", StarIntensityFor(simulation, view));

                tracker.Scatter(material, atmosphere, StarTransformFor(simulation, view), densityFalloff);

                _materials.Add(material);
                renderer.sharedMaterial = material;
            }
        }

        /// <summary>The transform of the star this body ultimately orbits, walking up the tree.</summary>
        private static Transform StarTransformFor(UniverseSimulation simulation, CelestialBodyView view)
        {
            CelestialBody body = view.Body;
            // Bounded, because a malformed catalogue with a parent cycle must not hang the frame.
            for (int hops = 0; hops < 16 && body != null; hops++)
            {
                if (body.IsStar)
                {
                    return simulation.Views.TryGetValue(body.Id, out CelestialBodyView star)
                        ? star.transform : null;
                }
                if (string.IsNullOrEmpty(body.ParentId)) break;
                body = simulation.Catalog?.Get(body.ParentId);
            }
            return null;
        }

        /// <summary>
        /// How brightly the star lights this body's air, falling off with distance.
        ///
        /// Without this every atmosphere in the catalogue glows equally, so Neptune's limb is as
        /// bright as Venus's — 900 times more light than it actually receives. The inverse-square
        /// law against Earth's insolation is the whole correction, and it is what makes the outer
        /// system read as dim rather than as underexposed.
        /// </summary>
        private static float StarIntensityFor(UniverseSimulation simulation, CelestialBodyView view)
        {
            // Calibrated against Earth seen full-disc from close in.
            //
            // This pass only ever ADDS light — it has no way to attenuate the surface underneath
            // it, which a real atmosphere does. So the honest ceiling is well below the physical
            // scattered radiance, or the two brightnesses stack and a blue marble comes out as a
            // flat cyan disc. At 8 the haze visibly softens the disc and the limb still lifts
            // clear of it, which is the part worth having.
            const float ReferenceIntensity = 8f;

            double? distanceKm = view.Body.Orbit?.SemiMajorAxisKm;
            if (distanceKm == null || distanceKm.Value <= 0.0) return ReferenceIntensity;

            // Moons orbit their planet, so their own semi-major axis says nothing about how far
            // they are from the star; use the planet's.
            CelestialBody parent = simulation.Catalog?.Get(view.Body.ParentId);
            if (parent != null && !parent.IsStar && parent.Orbit.HasValue)
                distanceKm = parent.Orbit.Value.SemiMajorAxisKm;

            double au = distanceKm.Value / Constants.AstronomicalUnit;
            if (au <= 0.0) return ReferenceIntensity;

            // Clamped at both ends: a hot Jupiter at 0.02 AU would otherwise be 2500x too bright
            // to render at all, and the floor keeps a distant limb visible rather than black.
            return (float)MathD.Clamp(ReferenceIntensity / (au * au), 1.2, 26.0);
        }

        private void OnDestroy()
        {
            _created.Clear();
            foreach (Material material in _materials)
            {
                if (material != null) Destroy(material);
            }
            _materials.Clear();
        }
    }
}
