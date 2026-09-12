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
        // The bright-end grades that used to live here are gone: CosmoSynth/Point derives both
        // size and brightness from one apparent magnitude, so a second window grading the same
        // stars a second way is exactly the drift this file's own comment warns about.

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
        /// Append one CosmoSynth/Point quad: four coincident vertices, spread in the shader.
        ///
        /// The positions are ICRF directions and nothing else — no radius, no axis reorder, no
        /// mapping. In sky mode the shader rotates them by the global <c>_CS_IcrfToWorld</c> and
        /// anchors them at THIS eye, so the shell is at infinity by construction rather than at a
        /// 900-unit sphere parented to the anchor, and the one ICRF-to-Unity mapping stays inside
        /// FloatingOrigin. The reorder that used to stand here was the second copy of that
        /// conversion in the codebase, and a sky that is silently mirrored is a sky in which every
        /// constellation is its own reflection — which no screenshot shows.
        ///
        /// All four vertices carry the same data.w (R14): the shader's cull writes an off-screen
        /// clip position, which CLIPS a primitive rather than discarding it, so a quad whose
        /// vertices disagreed would rasterise as a wedge rather than as nothing.
        /// </summary>
        /// <param name="shape">0 core, 1 halo, 2 spike across, 3 spike up. The shader turns the
        /// last three off by themselves for anything but the brightest stars.</param>
        private static void AddPoint(List<Vector3> vertices, List<Color> colors,
            List<Vector4> data, List<int> triangles, Vector3 direction, Color colour,
            float apparentMagnitude, int shape)
        {
            int b = vertices.Count;

            for (int c = 0; c < 4; c++)
            {
                vertices.Add(direction);
                colors.Add(colour);
                data.Add(new Vector4(apparentMagnitude, c, shape, 0f));
            }

            // Corner bits are (x = c & 1, y = c & 2), so 0,1,3,2 walks the perimeter and the two
            // triangles cover the quad rather than folding across it.
            triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 3);
            triangles.Add(b); triangles.Add(b + 3); triangles.Add(b + 2);
        }

        /// <summary>
        /// Emit one star. The single place a star becomes triangles, so the invented sky and the
        /// measured one cannot drift apart in appearance — the defect the deleted StarSprite's
        /// warned about and which happened anyway when the two populations kept separate size and
        /// brightness rules.
        ///
        /// Size and brightness are now one law evaluated once, in the shader, from the apparent
        /// magnitude: <c>_SizeDmm</c>, <c>_MagWindow</c> and <c>_Look</c> on the material are
        /// StarLook's own constants. The CPU decides only WHICH shapes to emit, because the halo
        /// and the spikes are what separate the conspicuous handful from the field and there is no
        /// point paying for four extra quads on a star that will scale all three to zero.
        /// </summary>
        /// <param name="direction">Unit vector toward the star, ICRF.</param>
        private static void EmitStar(List<Vector3> vertices, List<Color> colors,
            List<Vector4> data, List<int> triangles, Double3 direction, float apparentMagnitude,
            double temperatureK)
        {
            // Colour from a black-body temperature drawn across the main sequence: many cool
            // red dwarfs, few hot blue stars. Display-referred, because the shader decodes vertex
            // colours exactly once (contract 7.4) -- so a physically linear triple is encoded on
            // the way in rather than handed over raw.
            Color colour = CosmoTheme.LinearRgb(PlanetGenerator.BlackBodyColor(temperatureK));

            Vector3 icrf = new Vector3((float)direction.x, (float)direction.y, (float)direction.z);

            AddPoint(vertices, colors, data, triangles, icrf, colour, apparentMagnitude, 0);

            // ---- The bright end, which one quad cannot reach --------------------------
            //
            // Measured across three renders with completely different quad sizes and profiles,
            // the brightest a SINGLE additive star quad ever gets is about 0.27 of full scale --
            // 69/255 in the last frame, with three separate stars landing on exactly that value.
            // Widening the core removed the width-dependence of the gain but it did not raise the
            // ceiling. So no amount of size or colour tuning produces a star that stands out, and
            // the sky stays a field where everything is within a factor of two of everything else.
            //
            // Additive geometry sums in the framebuffer, so overlapping quads is the way past it
            // -- and it is not a workaround, it is what the thing actually looks like. A bright
            // point source is a saturated core, an extended halo several times wider, and
            // diffraction spikes. Drawing those as separate pieces decouples "how bright" from
            // "how big", which is precisely the tension that made the first attempt cotton wool.
            //
            // The shader scales all three by saturate(2b - 1), so they are already zero below the
            // midpoint of the magnitude window. Emitting them there would be four quads of
            // guaranteed nothing per star, on the layer with the most stars in it.
            if (apparentMagnitude > BrightShapeMagnitude) return;

            AddPoint(vertices, colors, data, triangles, icrf, colour, apparentMagnitude, 1);
            AddPoint(vertices, colors, data, triangles, icrf, colour, apparentMagnitude, 2);
            AddPoint(vertices, colors, data, triangles, icrf, colour, apparentMagnitude, 3);
        }

        /// <summary>
        /// Magnitude at which the halo and spikes first have any strength at all.
        ///
        /// Not a taste threshold: it is exactly where the shader's own saturate(2b - 1) reaches
        /// zero, so emitting geometry on the faint side of it can only ever draw nothing. Derived
        /// from StarLook rather than written down, or the two drift.
        /// </summary>
        private static float BrightShapeMagnitude =>
            StarLook.MagnitudeFromBrightness(0.5f);

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
            List<Vector4> data, List<int> triangles)
        {
            int drawn = System.Math.Min(_catalog.Count, _catalogStarBudget);

            for (int i = 0; i < drawn; i++)
            {
                CatalogStar star = _catalog.Stars[i];

                // The measured magnitude, straight through. Size, brightness and the bright-end
                // shapes are all functions of it in CosmoSynth/Point, so the two grades this used
                // to compute here — one saturating rank and one separate SmoothStep window — are
                // one number again.
                EmitStar(vertices, colors, data, triangles,
                    star.Direction, (float)star.ApparentMagnitude, star.TemperatureK);
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
            List<Vector4> data, List<int> triangles)
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

                double temperature = 2600.0 + System.Math.Pow(random.Unit(), 2.6) * 22000.0;

                // An invented star has no magnitude, so one is invented from the rank through the
                // same window the real ones are read with. That keeps both populations on one law
                // — which is the whole point of this method existing beside the catalogue one.
                EmitStar(vertices, colors, data, triangles, direction,
                    StarLook.MagnitudeFromBrightness(rank), temperature);
            }

            Debug.Log($"[CosmoSynth] star field: {_starCount} INVENTED stars " +
                      "(catalogue missing — run tools/cosmodata/build_starfield.py)");
        }

        private void BuildStarField(Transform anchor)
        {
            // Lists, not fixed arrays: the brightest stars are drawn with more than one quad.
            int budget = _catalog != null ? _catalog.Count : _starCount;
            var vertices = new List<Vector3>(budget * 4);
            var colors = new List<Color>(budget * 4);
            var data = new List<Vector4>(budget * 4);
            var triangles = new List<int>(budget * 6);

            if (_catalog != null) EmitCatalogSky(vertices, colors, data, triangles);
            else EmitInventedSky(vertices, colors, data, triangles);

            var mesh = new Mesh { name = "StarField", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, data);
            mesh.SetTriangles(triangles, 0);

            // The vertices are unit ICRF directions and the shader places them at this eye, so the
            // mesh's own extent says nothing about where it is drawn. Bounds large enough that it
            // is never culled — and the play-mode `far` assertion exempts sky-mode renderers by
            // reading _Space back off the material, rather than measuring a shell that is not
            // where its vertices say it is.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2e6f);

            var starObject = Own(new GameObject("Star Field"));
            _starField = starObject;
            starObject.transform.SetParent(anchor, false);
            starObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = starObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Soft round dots, not squares, and no texture to supply them.
            //
            // These quads carried no texture and Unity's default _MainTex is solid white, so every
            // star rendered as a hard-edged coloured tile and the sky read as confetti. The
            // point-spread is now analytic in CosmoSynth/Point — there is no sprite for the build
            // step to strip, and both star populations are drawn by the one shader, so they cannot
            // look like different kinds of object. It also declares the passthrough blend rather
            // than hoping for it: `Blend One One, Zero One`, additive colour with destination alpha
            // untouched, or every star stamps an opaque hole into the room.
            Material material = CosmoShaders.Material(CosmoShaders.Point);
            if (material == null) { renderer.enabled = false; return; }

            material.SetFloat("_Space", 2f);                      // sky: directions, per eye
            material.SetVector("_MagWindow", new Vector4(StarLook.MagnitudeFaint, StarLook.MagnitudeBright, 0f, 0f));
            material.SetVector("_SizeDmm", new Vector4(StarLook.FaintestQuadDmm, StarLook.BrightestQuadDmm, 0f, 0f));
            material.SetVector("_Look", new Vector4(StarLook.FaintestBrightness, 1f, 0f, 0f));
            // The bright-end shape constants, unchanged; they moved from this file's geometry into
            // the one shader that now draws every star.
            material.SetVector("_Shape", new Vector4(HaloScale, SpikeLength, SpikeWidth, 0f));
            material.SetVector("_ShapeGain", new Vector4(HaloBrightness, SpikeBrightness, 0f, 0f));
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

            var material = CosmoShaders.Material(CosmoShaders.Ring);
            if (material == null) { renderer.enabled = false; return; }
            material.mainTexture = BuildRingTexture(view.Body.Id, opacity);
            // The ring plane's radial coordinate is uv.x, which this mesh already writes: 0 at the
            // inner edge and 1 at the outer. The shader takes it from there rather than from a
            // pair of radius uniforms that would be a second copy of the same fact.
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
                Color colour = CosmoTheme.WithAlpha(CosmoTheme.RingDust, (float)density * opacity);
                pixels[x] = colour;
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        // ---- Lighting --------------------------------------------------------
        //
        // There is none, and that is the design (contract 8.2 item 12).
        //
        // A Unity light cannot express this scene. A universe has many stars and one camera sees
        // several systems at once, while the Android quality level allows exactly one per-pixel
        // light — so a single point light gets the terminator right on one system and wrong on
        // every other. Worse, the star it was attached to came from RootIds[0] whenever the focus
        // could not be resolved, and RootIds is in insertion order: the moment the exoplanet
        // catalogue merged in, the light modelling the entire Solar System moved to 61 Vir, eleven
        // parsecs away. Every planet went dark and the build reported complete success.
        //
        // CosmoSynth/Body is lit by _LightDirWS and _LightColor instead, set per body per frame by
        // CelestialBodyView from its own host star through CelestialCatalog.HostStarOf. Every body
        // is then lit by the star it actually orbits, and the URP asset turns both light modes off
        // (contract 8.1) because nothing reads them.


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
                if (material == null) { renderer.enabled = false; continue; }
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
