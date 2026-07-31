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
        [SerializeField] private int _starCount = 2500;

        [Tooltip("Radius of the star sphere in render units. Sits beyond everything else.")]
        [SerializeField] private float _starSphereRadius = 900f;

        [Header("Rings")]
        [SerializeField] private int _ringSegments = 128;

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
        private void BuildStarField(Transform anchor)
        {
            var random = new DeterministicRandom(20260730);

            var vertices = new Vector3[_starCount * 4];
            var colors = new Color[_starCount * 4];
            var uvs = new Vector2[_starCount * 4];
            var triangles = new int[_starCount * 6];

            for (int i = 0; i < _starCount; i++)
            {
                Double3 direction = random.OnUnitSphere();
                var centre = new Vector3((float)direction.x, (float)direction.z, (float)direction.y)
                             * _starSphereRadius;

                // Apparent magnitude: a steep power law, so the sky is mostly faint stars with a
                // handful of bright ones, as a real sky is.
                double brightness = System.Math.Pow(random.Unit(), 3.2);
                float size = _starSphereRadius * (0.0009f + (float)brightness * 0.0045f);

                // Colour from a black-body temperature drawn across the main sequence: many cool
                // red dwarfs, few hot blue stars.
                double temperature = 2600.0 + System.Math.Pow(random.Unit(), 2.6) * 22000.0;
                Double3 rgb = PlanetGenerator.BlackBodyColor(temperature);
                var colour = new Color((float)rgb.x, (float)rgb.y, (float)rgb.z,
                    0.25f + (float)brightness * 0.75f);

                // Camera-facing quad. Billboarding in a shader would be better; at this count the
                // difference is not measurable and a fixed quad needs no custom shader.
                Vector3 forward = centre.normalized;
                Vector3 right = Vector3.Cross(forward, Vector3.up).normalized * size;
                if (right.sqrMagnitude < 1e-9f) right = Vector3.right * size;
                Vector3 up = Vector3.Cross(forward, right).normalized * size;

                int v = i * 4;
                vertices[v + 0] = centre - right - up;
                vertices[v + 1] = centre + right - up;
                vertices[v + 2] = centre + right + up;
                vertices[v + 3] = centre - right + up;

                uvs[v + 0] = new Vector2(0, 0);
                uvs[v + 1] = new Vector2(1, 0);
                uvs[v + 2] = new Vector2(1, 1);
                uvs[v + 3] = new Vector2(0, 1);

                for (int c = 0; c < 4; c++) colors[v + c] = colour;

                int t = i * 6;
                triangles[t + 0] = v + 0; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 0; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "StarField", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * _starSphereRadius * 4f);

            var starObject = Own(new GameObject("Star Field"));
            starObject.transform.SetParent(anchor, false);
            starObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = starObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Additive, with destination alpha preserved.
            //
            // This is the mixed-reality trap: `Blend One One` blends the ALPHA channel by the
            // same rule as colour, so a star returning alpha 1 saturates the framebuffer's alpha
            // and tells the Quest compositor "this pixel is opaque" — punching a black hole in
            // the passthrough view wherever a star is drawn. `Blend One One, Zero One` keeps the
            // destination alpha and leaves the room showing through.
            var material = CosmoShaders.Material(CosmoShaders.ParticlesUnlit);
            material.SetFloat("_Mode", 4f);                       // additive
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = 1000;                          // behind everything
            material.EnableKeyword("_ALPHABLEND_ON");
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
            light.color = new Color(1f, 0.97f, 0.92f);
            // 3.2 blew every planet out to pure white. Unity's point light barely attenuates
            // when the range is far larger than the distance, so the intensity here is close to
            // what actually lands on the surface.
            light.intensity = 0.85f;
            light.shadows = LightShadows.None;   // mobile GPU; shadows cost more here than they add

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
