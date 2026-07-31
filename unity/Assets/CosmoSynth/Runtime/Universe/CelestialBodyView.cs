using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The rendered presence of one catalogue body: it holds the authoritative double-precision
    /// state, and each frame writes a float transform derived from it.
    ///
    /// Nothing else in the scene should hold a body's position — the transform is a projection
    /// of <see cref="AbsolutePositionKm"/> through the floating origin, and reading it back as
    /// truth would reintroduce exactly the precision loss the double pipeline exists to avoid.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class CelestialBodyView : MonoBehaviour
    {
        private UniverseSimulation _simulation;
        private ReferenceFrame _frame;
        private MeshRenderer _renderer;
        private MeshFilter _meshFilter;
        private Transform _sphere;

        /// <summary>The catalogue record this view draws.</summary>
        public CelestialBody Body { get; private set; }

        /// <summary>The body's frame in the reference-frame tree.</summary>
        public ReferenceFrame Frame => _frame;

        /// <summary>Absolute position in the root frame, km. The authoritative value.</summary>
        public Double3 AbsolutePositionKm { get; private set; }

        /// <summary>Absolute velocity in the root frame, km/s.</summary>
        public Double3 AbsoluteVelocityKmPerSec { get; private set; }

        /// <summary>The representation the scale renderer picked this frame.</summary>
        public RepresentationLevel Representation { get; private set; } = RepresentationLevel.Point;

        /// <summary>Apparent diameter in pixels for the active camera, this frame.</summary>
        public double ApparentDiameterPixels { get; private set; }

        public void Initialise(UniverseSimulation simulation, CelestialBody body, ReferenceFrame frame,
            CosmicTime now)
        {
            _simulation = simulation;
            Body = body;
            _frame = frame;

            BuildVisual();

            // Populate the state immediately rather than waiting for the first LateUpdate.
            //
            // Otherwise AbsolutePositionKm reads (0,0,0) for the whole frame in which the
            // catalogue finishes loading — and anything that anchors to a body during the
            // CatalogLoaded event silently anchors to the solar system barycentre instead. That
            // put the camera inside the Sun with Earth an AU away, which on a headset looks
            // exactly like an app that failed to load anything at all.
            if (_frame != null)
            {
                StateVector state = _frame.AbsoluteState(now);
                AbsolutePositionKm = state.Position;
                AbsoluteVelocityKmPerSec = state.Velocity;
            }
        }

        private void BuildVisual()
        {
            // A shared primitive sphere stands in until the planet engine's cube-sphere terrain
            // takes over at close range. It is deliberately a child, so the LOD system can swap
            // or rescale the visual without disturbing the body's own transform, which other
            // things (orbit lines, labels, the camera anchor) parent themselves to.
            var sphereObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereObject.name = "Visual";
            sphereObject.transform.SetParent(transform, worldPositionStays: false);

            // Physics colliders on celestial bodies would be meaningless at these scales and
            // ruinous for performance; interaction uses explicit ray tests instead.
            var collider = sphereObject.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            _sphere = sphereObject.transform;
            _renderer = sphereObject.GetComponent<MeshRenderer>();
            _meshFilter = sphereObject.GetComponent<MeshFilter>();

            if (_renderer != null)
            {
                // Instanced so every body can carry its own colour without a material each.
                _renderer.material.color = new Color(
                    (float)Body.DisplayColor.x, (float)Body.DisplayColor.y, (float)Body.DisplayColor.z);
                _renderer.material.SetFloat("_Glossiness", 0.03f);
                _renderer.material.SetFloat("_Metallic", 0f);

                // Stars emit rather than reflect. Without this the Sun renders as a dark ball,
                // because nothing is lighting it.
                if (Body.IsStar) _renderer.material.EnableKeyword("_EMISSION");
            }
        }

        /// <summary>
        /// Recompute this body's state and push it into the transform. Called once per frame by
        /// <see cref="UniverseSimulation"/>.
        /// </summary>
        public void UpdateState(CosmicTime time)
        {
            if (_frame == null || _simulation == null) return;

            StateVector state = _frame.AbsoluteState(time);
            AbsolutePositionKm = state.Position;
            AbsoluteVelocityKmPerSec = state.Velocity;

            FloatingOrigin origin = _simulation.Origin;

            // Local, not world. Everything is parented to the simulation's transform, which acts
            // as the universe's anchor in the room — so moving that one object places the whole
            // solar system on a table, and the XR rig's tracking space carries it correctly.
            // Writing world position here would pin the universe to the play-area origin and
            // make the anchor silently do nothing.
            transform.localPosition = PlaceForRendering(origin.ToRender(AbsolutePositionKm));

            // Rotation about the body's own pole, from the IAU elements.
            if (Body.Orientation.HasValue)
            {
                transform.localRotation = ToUnityRotation(Body.Orientation.Value.BodyFixedToIcrf(time));
            }

            UpdateVisualScale(origin);
        }

        /// <summary>
        /// True when this body is being drawn on the sky sphere instead of at its real distance.
        /// Its direction is exact; its distance is not.
        /// </summary>
        public bool DrawnAsStarPoint { get; private set; }

        /// <summary>
        /// Pull very distant bodies onto a sky sphere around the viewer.
        ///
        /// A catalogue that reaches other stars breaks the depth buffer before it breaks
        /// anything else. With Proxima at 4e13 km and a hologram on the table, the ratio between
        /// the nearest and furthest thing on screen is about 1e18, and no depth buffer expresses
        /// that — so the camera's near plane gets pushed out to keep the ratio sane and the
        /// entire room-scale scene is clipped away. That failure looks exactly like a black
        /// screen with working passthrough, which is what it was.
        ///
        /// Clamping the position onto a sphere at the horizon fixes it without lying about
        /// anything anyone can see: the direction to the star is preserved exactly, and its
        /// distance was never perceptible. Planetariums have drawn stars this way forever, for
        /// the same reason. <see cref="AbsolutePositionKm"/> keeps the true value, so physics,
        /// selection and the provenance panel are untouched.
        /// </summary>
        private Vector3 PlaceForRendering(Vector3 renderPosition)
        {
            DrawnAsStarPoint = false;

            float horizon = _simulation.StarPointHorizonUnits;
            if (horizon <= 0f) return renderPosition;

            Vector3 offset = renderPosition - _simulation.ViewerLocalPosition;
            float distance = offset.magnitude;
            if (distance <= horizon || distance <= 0f) return renderPosition;

            DrawnAsStarPoint = true;

            // A planet beyond the horizon is inside its own star's point — the two are separated
            // by far less than a pixel — so drawing it adds nothing but a draw call, and drawing
            // it *in front* of the star would be actively wrong.
            if (_renderer != null) _renderer.enabled = Body.IsStar;

            return _simulation.ViewerLocalPosition + offset * (horizon / distance);
        }

        private void UpdateVisualScale(FloatingOrigin origin)
        {
            if (_sphere == null) return;

            if (DrawnAsStarPoint)
            {
                UpdateStarPointScale();
                return;
            }

            double displayRadiusKm = _simulation.DisplayRadiusKm(Body);
            float diameter = origin.ScaleLength(displayRadiusKm * 2.0);

            // Never let a body collapse to a degenerate transform, which some pipelines dislike.
            if (diameter < 1e-5f) diameter = 1e-5f;

            // ...and never let it shrink below visibility, so "far away" cannot be mistaken for
            // "missing".
            Camera camera = Camera.main;
            if (camera != null)
            {
                float distance = Vector3.Distance(camera.transform.position, transform.position);
                diameter = ApplyMinimumVisibleSize(diameter, distance,
                    camera.fieldOfView * Mathf.Deg2Rad, ViewportInfo.VerticalPixels(camera));
            }

            // Oblateness: gas giants are visibly flattened and drawing them as spheres is one
            // of the more noticeable ways a solar system renderer looks wrong.
            float flattening = (float)Body.Flattening;
            _sphere.localScale = new Vector3(diameter, diameter * (1f - flattening), diameter);
        }

        /// <summary>
        /// Minimum on-screen diameter, in render units at the body's distance, that a body is
        /// allowed to shrink to. Below this it is drawn at this size instead of vanishing.
        /// </summary>
        private const float MinimumApparentPixels = 2.5f;

        /// <summary>
        /// Size a sky-sphere star by how bright it actually appears from here.
        ///
        /// Every star on the sky sphere sits at the same distance, so without this they would
        /// all be drawn the same size and the sky would read as a uniform dot grid — the one
        /// thing a real sky never looks like. Sizing by apparent bolometric magnitude restores
        /// the only cue that survives: Sirius is obviously brighter than Proxima, and the
        /// familiar constellations come out of the catalogue rather than being drawn in.
        /// </summary>
        private void UpdateStarPointScale()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            float distance = Vector3.Distance(camera.transform.position, transform.position);
            float diameter = ViewportInfo.WorldPerPixel(camera, distance) * ApparentPixelsFromMagnitude();
            _sphere.localScale = new Vector3(diameter, diameter, diameter);
        }

        /// <summary>
        /// On-screen diameter in pixels, from the star's apparent bolometric magnitude.
        ///
        /// m = M + 5 log10(d/10pc), with the absolute magnitude taken from the luminosity
        /// against the Sun's 4.83. This is bolometric, not visual: for an M dwarf the two differ
        /// by several magnitudes because most of its output is infrared. Correcting that needs a
        /// bolometric correction table the catalogue does not carry, so red dwarfs come out
        /// somewhat brighter here than the eye would see them — worth knowing, and still far
        /// closer than drawing every star identically.
        /// </summary>
        private float ApparentPixelsFromMagnitude()
        {
            const float FaintestPixels = 1.6f;
            const float BrightestPixels = 7.0f;

            if (!Body.LuminosityWatts.HasValue || Body.LuminosityWatts.Value <= 0.0) return 2.2f;

            double distanceParsecs = AbsolutePositionKm.Magnitude / Constants.Parsec;
            if (distanceParsecs <= 0.0) return BrightestPixels;

            double absolute = 4.83 - 2.5 * System.Math.Log10(Body.LuminosityWatts.Value / Constants.SolarLuminosity);
            double apparent = absolute + 5.0 * System.Math.Log10(distanceParsecs / 10.0);

            // Magnitudes run backwards — smaller is brighter — hence the negative slope.
            return Mathf.Clamp((float)(6.5 - 0.75 * apparent), FaintestPixels, BrightestPixels);
        }

        /// <summary>
        /// Adopt a generated surface texture. Called by <see cref="PlanetTextureBaker"/> once the
        /// bake completes, which may be many frames after the body first appeared — so the flat
        /// catalogue colour is what shows until then, rather than nothing.
        /// </summary>
        public void ApplySurfaceTexture(Texture2D texture) => ApplySurfaceTexture(texture, null, null);

        /// <summary>
        /// Adopt the generated surface maps.
        ///
        /// The normal map gives terrain visible relief without any geometry displacement — a
        /// sphere still, but one that catches light like a landscape. The metallic/smoothness
        /// map is what makes water read as water: Unity's Standard shader takes SMOOTHNESS FROM
        /// THE ALPHA CHANNEL, so oceans come out glossy enough to throw a sun glint while rock
        /// stays matte. Without it every surface is uniformly rough and a planet looks like
        /// painted clay.
        /// </summary>
        public void ApplySurfaceTexture(Texture2D texture, Texture2D normalMap, Texture2D surfaceMap)
        {
            if (_renderer == null || texture == null) return;

            Material material = _renderer.material;
            material.mainTexture = texture;

            if (normalMap != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", normalMap);
                material.SetFloat("_BumpScale", 1f);
            }

            if (surfaceMap != null)
            {
                material.EnableKeyword("_METALLICGLOSSMAP");
                material.SetTexture("_MetallicGlossMap", surfaceMap);
                // With a gloss map present the scalar acts as a multiplier, so it must be 1 or
                // the map is silently scaled down to nothing.
                material.SetFloat("_Glossiness", 1f);
                material.SetFloat("_GlossMapScale", 1f);
            }

            // The tint multiplies the texture, so leave it white or the map is double-darkened
            // by the colour it was already painted with.
            material.color = Color.white;

            // Rough and non-metallic.
            //
            // Standard defaults to 0.5 smoothness, which puts a broad specular highlight in the
            // middle of the lit face — a planet rendered like polished plastic, blowing out to
            // pure white exactly where the surface detail is. Rock, ice and cloud are all
            // diffuse; the only body that genuinely wants a highlight is an ocean, and that
            // needs a specular mask this shader has no channel for.
            if (surfaceMap == null) material.SetFloat("_Glossiness", 0.03f);
            material.SetFloat("_Metallic", 0f);

            if (Body.IsStar)
            {
                material.EnableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", texture);
                material.SetColor("_EmissionColor", Color.white * 2.2f);
            }

            HasSurfaceTexture = true;
        }

        /// <summary>True once a generated surface map has replaced the flat catalogue colour.</summary>
        public bool HasSurfaceTexture { get; private set; }

        /// <summary>
        /// The radius the body is actually drawn at, in world units.
        ///
        /// This is what selection and highlighting must use, not the physical radius scaled by
        /// the render transform: a body clamped to its minimum visible size is drawn larger than
        /// physics says, and aiming at what you can see has to hit it.
        /// </summary>
        public float RenderedRadius =>
            _sphere != null ? _sphere.lossyScale.x * 0.5f : 0.01f;

        /// <summary>Called by the scale renderer once it has chosen a representation.</summary>
        public void SetRepresentation(RepresentationLevel level, double apparentPixels)
        {
            Representation = level;
            ApparentDiameterPixels = apparentPixels;

            if (_renderer == null) return;

            // Geometry stays on at every level.
            //
            // This used to switch the renderer off below Sphere, on the grounds that points and
            // impostors would be drawn by the star-field renderer — which does not exist yet.
            // The result was a body that simply disappeared, and if the LOD input was wrong the
            // entire scene disappeared with it, leaving a headset showing nothing but
            // passthrough and no clue why. Until there is something to hand small bodies off
            // to, the honest behaviour is to keep drawing them.
            //
            // The one exception is a body already pulled onto the sky sphere, which has decided
            // for itself whether to draw — re-enabling it here would put every exoplanet back on
            // top of the star it orbits.
            if (!DrawnAsStarPoint) _renderer.enabled = true;
        }

        /// <summary>
        /// Enforce a minimum on-screen size so a body never shrinks below visibility.
        ///
        /// Physically a distant planet really is smaller than a pixel — but rendering it that
        /// way makes it indistinguishable from a bug, and a user cannot tell "correctly tiny"
        /// from "broken". Real planetarium software has the same convention for the same
        /// reason. <see cref="ApparentDiameterPixels"/> still reports the true value, so the
        /// HUD and any measurement path are unaffected.
        /// </summary>
        private float ApplyMinimumVisibleSize(float diameter, float distanceToCamera, float verticalFov,
            int verticalPixels)
        {
            if (verticalPixels <= 0 || verticalFov <= 0f || distanceToCamera <= 0f) return diameter;

            float unitsPerPixel = 2f * distanceToCamera * Mathf.Tan(verticalFov * 0.5f) / verticalPixels;
            float floor = unitsPerPixel * MinimumApparentPixels;
            return Mathf.Max(diameter, floor);
        }

        /// <summary>
        /// Convert a Core rotation matrix into a Unity quaternion.
        ///
        /// Note the handedness. Core works in the right-handed ICRF frame that every
        /// astronomical dataset uses; Unity's world space is left-handed. Negating the z basis
        /// column converts between them. Getting this wrong produces a solar system that is
        /// subtly mirrored — planets orbit the correct paths but rotate the wrong way, which is
        /// easy to miss and very hard to explain later.
        /// </summary>
        public static Quaternion ToUnityRotation(Matrix3d rotation)
        {
            Double3 right = rotation.Column0;
            Double3 up = rotation.Column2;      // ICRF z is "up" for a body's pole
            Double3 forward = rotation.Column1;

            var unityForward = new Vector3((float)forward.x, (float)forward.z, (float)forward.y);
            var unityUp = new Vector3((float)up.x, (float)up.z, (float)up.y);

            if (unityForward.sqrMagnitude < 1e-12f || unityUp.sqrMagnitude < 1e-12f) return Quaternion.identity;
            return Quaternion.LookRotation(unityForward, unityUp);
        }

        /// <summary>Convert an ICRF direction to Unity's left-handed axes (y up).</summary>
        public static Vector3 ToUnityDirection(Double3 icrf) =>
            new Vector3((float)icrf.x, (float)icrf.z, (float)icrf.y);

        /// <summary>Convert a Unity direction back to ICRF.</summary>
        public static Double3 ToIcrfDirection(Vector3 unity) =>
            new Double3(unity.x, unity.z, unity.y);

        private void OnDestroy()
        {
            // Material instances created by touching .material are ours to clean up.
            if (_renderer != null && Application.isPlaying && _renderer.material != null)
            {
                Destroy(_renderer.material);
            }
        }
    }
}
