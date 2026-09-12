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

        /// <summary>
        /// Point this view at a new frame after the tree has been rebuilt.
        ///
        /// Adding or removing a body rebuilds the whole reference-frame tree, which orphans every
        /// existing view's frame reference. Rebinding is what makes it possible to add eight
        /// bodies without destroying and recreating the other two hundred and thirty-five — and
        /// that matters for more than performance: recreating them all means calling SetCatalog,
        /// which raises CatalogLoaded, which re-anchors the viewer. Generating a star system
        /// would teleport you back to Earth, which is precisely what it did.
        /// </summary>
        public void Rebind(ReferenceFrame frame) => _frame = frame;

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

            // Start at nothing, not at the primitive's unit scale.
            //
            // CreatePrimitive hands back a one-unit sphere and nothing rescales it until the
            // first UpdateState. Views are destroyed and rebuilt from scratch on every catalogue
            // merge, and that happens three times during startup — so the scene got three frames
            // of 235 half-metre grey balls stacked on the anchor, which on a headset is a grey
            // ball flashing on the table while the app loads. It also handed anything reading
            // RenderedRadius before the first frame an answer five orders of magnitude out.
            _sphere.localScale = Vector3.one * DegenerateScale;

            // Kept so the LOD switch can put it back; CosmoSphere is built from it, not instead
            // of it, so the surface maps' UV layout is whatever Unity's own sphere says it is.
            _primitiveMesh = _meshFilter != null ? _meshFilter.sharedMesh : null;

            if (_renderer != null)
            {
                // CosmoSynth/Body, explicitly. CreatePrimitive hands back a renderer already
                // carrying URP's own Lit material, and taking that is the keyword-stripping
                // magenta failure CosmoShaders exists to prevent: URP/Lit is multi-variant, the
                // whole scene is built at runtime so no asset references the variant this needs,
                // and the build step strips what nothing references. It works perfectly in the
                // editor, where every variant is loaded.
                Material material = CosmoShaders.Material(CosmoShaders.Body);
                if (material == null)
                {
                    _renderer.enabled = false;
                }
                else
                {
                    // Instanced so every body can carry its own colour without a material each.
                    material.color = CosmoTheme.LinearRgb(Body.DisplayColor);
                    material.SetFloat(SmoothnessId, 0.03f);

                    // Stars emit rather than reflect. Without this the Sun renders as a dark ball,
                    // because nothing is lighting it. There are no shader keywords any more: the
                    // emission term is a colour, and black is off.
                    if (Body.IsStar) material.SetColor(EmissionId, CosmoTheme.Neutral);

                    material.SetColor(AmbientId, CosmoTheme.AmbientFill);
                    _renderer.sharedMaterial = material;
                }
            }

            // The sky-sphere representation. Built now rather than on demand, because creating a
            // GameObject the first frame a body crosses the horizon would stutter every time the
            // user zoomed out.
            var pointObject = new GameObject("Star Point");
            pointObject.transform.SetParent(transform, worldPositionStays: false);
            _starPoint = pointObject.AddComponent<StarPointBillboard>();
            _starPoint.Initialise(CosmoTheme.LinearRgb(Body.DisplayColor));
        }

        private StarPointBillboard _starPoint;

        /// <summary>
        /// Advance this body's double-precision state. State only: nothing here touches a
        /// transform.
        ///
        /// Placement is a separate question with a separate answer, because it depends on where
        /// every OTHER body in the group is — a moon cannot be placed until its planet's group has
        /// been solved. <see cref="Place"/> does that, after <see cref="ScaleRenderer"/> has run
        /// the solver.
        /// </summary>
        public void UpdateState(CosmicTime time)
        {
            if (_frame == null || _simulation == null) return;

            // Nothing is drawn at an instant the catalogue does not cover.
            //
            // This only ever fires for spacecraft, and it is the whole reason they carry a
            // window. A sampled trajectory clamps to its endpoints and a conic propagates
            // forever, so without this Cassini would sit above Saturn in 2026 — smooth,
            // plausible, and nine years after it entered the atmosphere. Drawing nothing is
            // the only honest rendering of "we do not know".
            bool known = Body.KnownAt(time);
            if (known != IsKnown)
            {
                IsKnown = known;
                RefreshRendererEnabled();
                if (_starPoint != null && !known) _starPoint.SetVisible(false, 0f);
            }
            if (!known)
            {
                // Collapse the transform, not just the renderer.
                //
                // Returning here leaves localPosition and localScale holding whatever they last
                // did — and for a spacecraft that has never once been inside its window, that is
                // the anchor, at the primitive's unit scale. RenderedRadius is read by things
                // that are not the renderer, and a stale half-metre radius is an enormous target
                // sitting at point-blank range. That is how a fresnel shell the size of the sky
                // came to be drawn around Dawn, a probe that stopped transmitting in 2018 and was
                // not on screen at all.
                if (_sphere != null) _sphere.localScale = Vector3.one * DegenerateScale;
                IsMarkerDrawn = false;
                return;
            }

            StateVector state = _frame.AbsoluteState(time);
            AbsolutePositionKm = state.Position;
            AbsoluteVelocityKmPerSec = state.Velocity;
        }

        /// <summary>
        /// Put this body where the group solver says it goes.
        ///
        /// Rigid and Collapsed nodes take the similarity about the centre eye; an Envelope node —
        /// the eye is close to something that fills the view — is placed at true scale and
        /// compressed per vertex by its material instead. Either way the direction from the eye is
        /// exact, so head-motion parallax is the real parallax and only binocular disparity
        /// reflects the compression.
        /// </summary>
        public void Place(in FrameContext ctx, in GroupPlacement placement)
        {
            if (_simulation == null || !IsKnown) return;

            FloatingOrigin origin = _simulation.Origin;

            // Local, not world. Everything is parented to the simulation's transform, which acts
            // as the universe's anchor in the room — so moving that one object places the whole
            // solar system on a table. Writing world position here would pin the universe to the
            // play-area origin and make the anchor silently do nothing.
            Vector3 trueUniverse = origin.ToRender(AbsolutePositionKm);

            GroupScale = placement.Mode == GroupMode.Envelope ? 1.0 : placement.Scale;
            PerVertexCompressed = placement.Mode == GroupMode.Envelope;

            if (PerVertexCompressed) transform.localPosition = trueUniverse;
            else Placement.PlaceRigid(transform, trueUniverse, placement.Scale, ctx);

            // Rotation about the body's own pole, from the IAU elements, through the one mapping.
            if (Body.Orientation.HasValue)
            {
                transform.localRotation = origin.MapRotation(Body.Orientation.Value.BodyFixedToIcrf(ctx.Now));
            }

            UpdateVisualScale(ctx, placement);
            ApplyLighting();
        }

        /// <summary>Direction to this body's host star, in world space. Read by the ring and the shell.</summary>
        public Vector3 LightDirectionWS { get; private set; } = Vector3.up;

        /// <summary>This body's host star's colour and intensity, as the shaders receive it.</summary>
        public Color LightColour { get; private set; } = CosmoTheme.Neutral;

        private CelestialBodyView _hostStar;
        private bool _hostStarResolved;

        /// <summary>
        /// Light this body from the star it actually orbits.
        ///
        /// Not from a Unity light: a universe has many stars, one camera sees several systems at
        /// once, and the Android quality level allows exactly one per-pixel light. A single
        /// directional or point light gets the terminator right on one system and wrong on every
        /// other — and the star it was attached to came from <c>RootIds[0]</c> whenever the focus
        /// could not be resolved, which is insertion order, which put the light that models the
        /// whole Solar System on an exoplanet host eleven parsecs away the moment that catalogue
        /// merged. <c>CelestialCatalog.HostStarOf</c> walks the parent chain instead, which is one
        /// function replacing the four copies of that loop this codebase had.
        ///
        /// The host is resolved once and remembered: the parent chain is a property of the
        /// catalogue, and the catalogue rebuilds every view when it changes.
        /// </summary>
        private void ApplyLighting()
        {
            if (_renderer == null) return;

            if (!_hostStarResolved)
            {
                _hostStarResolved = true;
                CelestialBody star = _simulation.HostStarOf(Body.Id);
                if (star != null && _simulation.Views.TryGetValue(star.Id, out CelestialBodyView view))
                {
                    _hostStar = view;
                    double temperature = star.EffectiveTemperatureK ?? 5772.0;
                    // 3.2 blew every planet out to pure white under the old point light. This is
                    // close to what actually lands on the surface, and it is the one knob 7.4's
                    // re-tune turns after the switch to Linear.
                    LightColour = CosmoTheme.LinearRgb(
                        PlanetGenerator.BlackBodyColor(temperature) * (double)StarLook.StarLightIntensity);
                }
            }

            // A star lights itself from nowhere; its own surface is emissive.
            if (_hostStar != null && _hostStar != this)
            {
                Double3 toStar = _hostStar.AbsolutePositionKm - AbsolutePositionKm;
                if (toStar.SqrMagnitude > 0.0)
                {
                    LightDirectionWS = _simulation.transform.TransformDirection(
                        _simulation.Origin.MapDirection(toStar.Normalized));
                }
            }

            ApplyLightingTo(_renderer.sharedMaterial);
            if (_model != null && _renderer.sharedMaterials.Length > 1)
            {
                Material[] materials = _renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) ApplyLightingTo(materials[i]);
            }
        }

        private void ApplyLightingTo(Material material)
        {
            if (material == null) return;
            material.SetVector(LightDirId, LightDirectionWS);
            material.SetColor(LightColorId, LightColour);
            // Envelope mode: the eye is inside d0 of a body large enough that a similarity would
            // put its surface inside the near plane, so the vertex shader compresses each vertex
            // about the centre eye instead. Nothing else in the frame changes.
            material.SetFloat(PerVertexCompressId, PerVertexCompressed ? 1f : 0f);
        }

        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int EmissionId = Shader.PropertyToID("_Emission");
        private static readonly int AmbientId = Shader.PropertyToID("_Ambient");
        private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
        private static readonly int SurfaceMapId = Shader.PropertyToID("_SurfaceMap");
        private static readonly int LightDirId = Shader.PropertyToID("_LightDirWS");
        private static readonly int LightColorId = Shader.PropertyToID("_LightColor");
        private static readonly int PerVertexCompressId = Shader.PropertyToID("_PerVertexCompress");

        /// <summary>The similarity this body was placed with this frame. 1 means exact.</summary>
        public double GroupScale { get; private set; } = 1.0;

        /// <summary>True when this body is drawn at true scale and compressed in the vertex shader.</summary>
        public bool PerVertexCompressed { get; private set; }

        /// <summary>
        /// True when this body is too small on screen to be worth geometry, and is drawn by the
        /// additive point path instead.
        ///
        /// It is NOT inflated to stay visible. An inflated sphere writes depth at a size the body
        /// does not have, so a floored moon intersects its own planet and the intra-group ordering
        /// the whole placement design rests on stops being true. A point writes no depth and
        /// cannot occlude anything, so "too small to resolve" costs nothing but a dot — which is
        /// also what a planetarium draws, and for the same reason.
        /// </summary>
        public bool DrawnAsPoint { get; private set; }

        /// <summary>
        /// False when the clock is outside the span this body's placement covers, and it is
        /// therefore not being drawn at all. Always true for anything in the Solar System
        /// catalogue; only spacecraft have an end.
        /// </summary>
        public bool IsKnown { get; private set; } = true;

        private void UpdateVisualScale(in FrameContext ctx, in GroupPlacement placement)
        {
            if (_sphere == null) return;

            if (_model != null)
            {
                UpdateModelScale(ctx, placement);
                return;
            }

            IsMarkerDrawn = false;

            double displayRadiusKm = _simulation.DisplayRadiusKm(Body);
            double distanceKm = Double3.Distance(ctx.EyeKm, AbsolutePositionKm);
            double pixels = ctx.ApparentPixels(displayRadiusKm, distanceKm);

            // A body whose whole rendered extent is inside the near plane is not drawn.
            //
            // The 2.5 px floor below cannot remove it, because angular size is invariant under
            // the group similarity: the Sun subtends 0.53 deg from Earth at every rung of the
            // ladder, so the floor sees a 9.5 px disc at the Local Group scale exactly as it
            // does in the room. What changes is rendered DISTANCE. At 1.08e-20 m/km the whole
            // Solar System collapses to about a picometre at the origin, the Sun renders
            // 1.6e-12 m from the eye, and the camera cannot see anything nearer than 0.05 m.
            //
            // Keeping the sphere enabled there draws nothing and costs a draw call, and it hands
            // the play-mode `angular` assertion a rendered distance of zero to divide by: it
            // reported the Sun as subtending 360 deg at four of the seven scales. The body is
            // invisible either way. This is the app saying so, which is what makes the
            // assertion meaningful again rather than merely quiet.
            double renderedRadius = placement.Scale * displayRadiusKm * ctx.UnitsPerKm;
            double renderedDistance = placement.Mode == GroupMode.Envelope
                ? ScaledSpace.Compress(distanceKm * ctx.UnitsPerKm)
                : placement.Scale * distanceKm * ctx.UnitsPerKm;

            // d + r < near, not d < near: a body the eye is INSIDE has its centre closer than
            // the near plane and most of its surface beyond it, which is Envelope mode and must
            // still be drawn.
            bool insideNear = renderedDistance + renderedRadius < ScaledSpace.NearPlaneMetres;
            if (insideNear != _insideNearPlane)
            {
                _insideNearPlane = insideNear;
                RefreshRendererEnabled();
            }

            // The floor decides REPRESENTATION, not size. Below two and a half pixels there is no
            // shape to draw, so the body hands over to the point path; above it the sphere is
            // drawn at exactly the size the group similarity gives it and at no other size.
            bool asPoint = pixels < MinimumApparentPixels && distanceKm > displayRadiusKm;
            if (asPoint != DrawnAsPoint)
            {
                DrawnAsPoint = asPoint;
                RefreshRendererEnabled();
            }

            if (asPoint)
            {
                UpdatePointScale(ctx);
                return;
            }

            if (_starPoint != null) _starPoint.SetVisible(false, 0f);

            float diameter = (float)(placement.Scale * displayRadiusKm * 2.0 * ctx.UnitsPerKm);

            // Never a degenerate transform, which some pipelines dislike; this is far below a
            // pixel at every scale, so it is not a visibility floor in disguise.
            if (diameter < DegenerateScale) diameter = DegenerateScale;

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
        /// Size the point that stands in for a body too small to draw.
        ///
        /// For a star, by how bright it actually appears from here: every point sits at the same
        /// apparent size otherwise, and a uniform dot grid is the one thing a real sky never looks
        /// like. For anything else, at exactly the floor it just failed — which is honest, because
        /// the point is saying "there is something here", not "it is this big".
        ///
        /// ANGULAR, not pixel-derived. A point should subtend the same angle whatever the eye
        /// buffer's resolution is; deriving it from a pixel count made it a function of the render
        /// target, and the same star measured 7.5 px in the app's reckoning and 19 px in the
        /// captured frame.
        /// </summary>
        private void UpdatePointScale(in FrameContext ctx)
        {
            if (_starPoint == null) return;

            float distance = Vector3.Distance(ctx.EyeUniverse, transform.localPosition);
            double floorAngle = ctx.PixelsPerRadian > 0f ? MinimumApparentPixels / ctx.PixelsPerRadian : 0.0;
            double angle;
            Color colour;

            if (Body.IsStar)
            {
                float brightness = StarLook.BrightnessFromMagnitude(ApparentMagnitude(ctx));
                angle = StarLook.QuadRadians(brightness);
                colour = StarLook.Colour(Body.DisplayColor, brightness);

                // The local star is not a dot. From Earth the Sun subtends half a degree, and
                // magnitude-driven sizing cannot express that — Sirius and the Sun differ by ten
                // orders of magnitude in flux and both land on the bright clamp. The true angular
                // diameter wins wherever it is larger, and falls away by itself with distance.
                double trueAngle = ScaleSpace.AngularDiameterRad(
                    _simulation.DisplayRadiusKm(Body), Double3.Distance(ctx.EyeKm, AbsolutePositionKm));
                if (trueAngle > angle) angle = trueAngle;
            }
            else
            {
                angle = floorAngle;
                colour = CosmoTheme.LinearRgb(Body.DisplayColor);
            }

            if (angle < floorAngle) angle = floorAngle;

            // The billboard takes the ANGLE. It used to take a world diameter, which meant
            // multiplying by the distance here and dividing by it again inside a CPU billboard —
            // and a CPU billboard is one quad built from one camera transform and shown to two
            // eyes. CosmoSynth/Point expands the corners against each eye's own projection.
            float diameter = distance * (float)angle;

            _starPoint.SetColour(colour);
            _starPoint.SetRenderQueue(StarPointBillboard.SkyQueue);
            _starPoint.SetVisible(true, (float)angle);

            // Kept in step so anything reading RenderedRadius — selection, the rim highlight —
            // still agrees with what is on screen.
            _sphere.localScale = new Vector3(diameter, diameter, diameter);
        }

        /// <summary>
        /// The star's apparent bolometric magnitude from here.
        ///
        /// m = M + 5 log10(d/10pc), with the absolute magnitude taken from the luminosity
        /// against the Sun's 4.83. This is bolometric, not visual: for an M dwarf the two differ
        /// by several magnitudes because most of its output is infrared. Correcting that needs a
        /// bolometric correction table the catalogue does not carry, so red dwarfs come out
        /// somewhat brighter here than the eye would see them — worth knowing, and still far
        /// closer than drawing every star identically.
        /// </summary>
        private double ApparentMagnitude(in FrameContext ctx)
        {
            // No luminosity: a planet or moon drawn as a point. Something faint, so it can never
            // outrank a real star.
            if (!Body.LuminosityWatts.HasValue || Body.LuminosityWatts.Value <= 0.0) return 5.5;

            // From the EYE, not from the solar system barycentre. The doc comment above has always
            // said "from here"; the code measured from the origin of the coordinate system, which
            // is the same thing only while the viewer is at home.
            double distanceParsecs = Double3.Distance(ctx.EyeKm, AbsolutePositionKm) / Constants.Parsec;
            if (distanceParsecs <= 0.0) return -26.0;

            double absolute = 4.83 - 2.5 * System.Math.Log10(Body.LuminosityWatts.Value / Constants.SolarLuminosity);
            return absolute + 5.0 * System.Math.Log10(distanceParsecs / 10.0);
        }

        private CsMesh.Model _model;
        private Mesh _primitiveMesh;
        private bool _usingDetailedSphere;

        /// <summary>
        /// Choose between the primitive sphere and the subdivided one for this body's LOD.
        ///
        /// Never touches a view that has adopted a shipped mesh: a spacecraft's geometry is its
        /// own, and swapping it for a sphere because the LOD system called it resolvable would be
        /// a spectacular way to lose 24 models.
        /// </summary>
        private void UpdateSphereDetail(RepresentationLevel level)
        {
            if (_meshFilter == null || _model != null || _primitiveMesh == null) return;

            bool wantsDetail = level >= RepresentationLevel.Sphere;
            if (wantsDetail == _usingDetailedSphere) return;

            Mesh detailed = wantsDetail ? CosmoSphere.Detailed(_primitiveMesh) : null;
            if (wantsDetail && detailed == null) return;      // unreadable; keep the primitive

            _meshFilter.sharedMesh = wantsDetail ? detailed : _primitiveMesh;
            _usingDetailedSphere = wantsDetail;
        }

        /// <summary>
        /// How many times larger than life this body is currently drawn. 1 is true scale.
        ///
        /// For a spacecraft this is never 1 and is not meant to be. Voyager is 13 m across and
        /// sits 25 billion km away; at any scale that also shows the Sun it is around 1e-13 of
        /// a pixel. The choice is between an exaggerated spacecraft and no spacecraft, and a
        /// planetarium has always made the same one — but the number has to be visible, which
        /// is what this property is for.
        /// </summary>
        public double SizeExaggeration { get; private set; } = 1.0;

        /// <summary>True once a shipped mesh has replaced the default sphere.</summary>
        public bool HasModel => _model != null;

        /// <summary>Triangles in the loaded model, for the diagnostics readout.</summary>
        public int ModelTriangleCount => _model != null ? _model.TriangleCount : 0;

        /// <summary>
        /// Adopt a shipped mesh in place of the sphere. Called by
        /// <see cref="SpacecraftModelLoader"/> once the file has streamed in, which is several
        /// frames after the body appears — until then a spacecraft is a small grey sphere,
        /// which is wrong-looking but present, rather than absent.
        /// </summary>
        public void ApplyModel(CsMesh.Model model)
        {
            if (model == null || model.Mesh == null || _meshFilter == null || _renderer == null) return;

            _model = model;
            _meshFilter.mesh = model.Mesh;

            // One material per submesh, in the same order the mesh's submeshes are indexed.
            var materials = new Material[model.Submeshes.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = CosmoShaders.Material(CosmoShaders.Body);
                if (material == null) { _renderer.enabled = false; return; }
                Color baseColour = model.Submeshes[i].Color;

                material.color = baseColour;
                material.SetFloat(SmoothnessId,
                    Mathf.Clamp(model.Submeshes[i].Smoothness, 0f, MaximumModelSmoothness));
                material.SetColor(AmbientId, CosmoTheme.AmbientFill);

                // Mixed toward white before being scaled, so a near-black submesh still emits.
                // Several shipped models carry parts below 0.05 luminance — Parker's is 0.007 —
                // and a fraction of nothing is nothing. CosmoSynth/Body multiplies this by the
                // albedo, so it is a fraction of the submesh's own colour and there is no keyword.
                material.SetColor(EmissionId,
                    CosmoTheme.Scaled(CosmoTheme.Desaturate(baseColour, 0.65f), ModelEmission));

                materials[i] = material;
            }

            DestroyMaterials();
            _renderer.materials = materials;
        }

        // The metallic clamp that used to stand here is gone with the metal. CosmoSynth/Body
        // has no metallic term at all: a metal shows a reflection of an environment, this scene
        // has no skybox and no reflection probe, and the ISS's baked metallic 1.0 therefore
        // rendered it as six near-black pixels on Earth's disc while the loader reported
        // "24/24 applied". A term whose only correct value is zero is better deleted than clamped.

        /// <summary>
        /// Ceiling on a baked smoothness value; several models carry 0.5 to 0.72.
        ///
        /// The same lesson the planet surfaces already learned, in a place where it bites harder.
        /// A tight specular lobe on something a handful of pixels across is not a highlight, it is
        /// one blown-out pixel that appears and vanishes as the model rotates — and with no MSAA
        /// on the device that reads as a flashing artefact rather than as metal.
        /// </summary>
        private const float MaximumModelSmoothness = 0.30f;

        /// <summary>
        /// Fraction of its own albedo a spacecraft emits.
        ///
        /// A spacecraft is a few pixels of thin structure that spends half its orbit in its
        /// planet's shadow and is composited over a lit room. Lighting alone cannot carry a
        /// silhouette that small. A little self-emission is what makes it survive the night side
        /// and passthrough both, and at this size nobody can distinguish it from bounce light. It
        /// is deliberately small enough that the terminator still reads on the day side.
        /// </summary>
        private const float ModelEmission = 0.16f;

        /// <summary>
        /// Largest a spacecraft may be drawn, as a fraction of the radius of the body it is
        /// attached to.
        ///
        /// There has to be a ceiling as well as a floor, and this is the bug the floor alone
        /// produced: a 3 m Viking lander was drawn 598 million times life size — about ten
        /// thousand kilometres across, larger than Mars, because from Earth the whole planet is
        /// three pixels and the minimum-visible rule cheerfully inflated the lander to two and a
        /// half of them. A spacecraft that swallows its own planet is worse than one you cannot
        /// see, and it is the same lesson the orbit lines already learned: something is only
        /// worth drawing when it is bigger than a dot *and* smaller than its host.
        /// </summary>
        /// 0.25 was still too generous. At the ceiling, TESS — a 3.7 metre satellite — was being
        /// drawn 1594 km across, forty per cent wider than the Moon and the second largest object
        /// anywhere near Earth. A tenth still guarantees visibility, because the ceiling only
        /// binds when the parent is itself small on screen, and that is exactly when a spacecraft
        /// should be handing over to its marker anyway.
        private const float MaximumFractionOfParent = 0.10f;

        /// <summary>
        /// Smallest a spacecraft model is drawn, in dmm — millimetres subtended at one metre.
        ///
        /// It was 2.5 <i>pixels</i>, shared with the planets, and for a planet that is right: a
        /// dot is a faithful rendering of a distant sphere. A spacecraft is not a sphere. Its
        /// whole subject is its shape — the truss, the arrays, the dish — and at 2.5 pixels there
        /// is no shape, just an unstable speck that costs several thousand triangles and
        /// shimmers because it is below what the panel can hold still. Seven dmm is about nine
        /// pixels per eye on a Quest 3, which is enough for a silhouette to read.
        ///
        /// Being angular, this is also independent of the render target, which the pixel figure
        /// was not: the shipped screenshots were captured at a height the running app had never
        /// been told about, so every pixel-denominated rule in the app was applied at roughly half
        /// the size the capture then showed.
        /// </summary>
        private const float ModelFloorDmm = 7f;

        /// <summary>
        /// Below this angular size the mesh is replaced by a marker dot rather than by nothing.
        ///
        /// Three dmm is about four pixels per eye — the point below which the mesh stops being a
        /// shape and starts being an aliasing source.
        /// </summary>
        private const float ModelMarkerDmm = 3f;

        /// <summary>
        /// How much larger the threshold is on the way back up.
        ///
        /// Without it the model swaps to the marker and back on whichever side of a single
        /// comparison the frame happens to land, and an eccentric orbit crosses that comparison
        /// twice a period. Every other threshold in this renderer already has a hysteresis band;
        /// this one was a bare less-than.
        /// </summary>
        private const float ModelHysteresis = 1.3f;

        /// <summary>
        /// Angular size of the marker dot itself, in dmm. Fixed: it is an icon, not a body.
        ///
        /// Larger than the mesh it stands in for, which is deliberate and is the opposite of what
        /// 2.4 dmm would have given. The marker is drawn with the same point spread as the stars,
        /// and that sprite's rendered brightness depends steeply on how wide it is drawn — below
        /// about four pixels the profile is narrower than a pixel and most of the colour is lost
        /// to sampling before it reaches the screen. At 2.4 dmm this would have been a dot at
        /// roughly 30/255: an interface element too faint to notice, which is the one thing a
        /// marker must not be. At 5.5 it lands near 95 and reads as a definite cyan point.
        ///
        /// So the representation visibly changes size when it changes kind. That is honest — it
        /// is no longer showing you a spacecraft, it is showing you where one is — and it is what
        /// stops the handover reading as a glitch.
        /// </summary>
        private const float MarkerDmm = 5.5f;

        /// <summary>A transform small enough to be invisible without being degenerate.</summary>
        private const float DegenerateScale = 1e-5f;

        /// <summary>Set when the model is too small to be worth drawing at its capped size.</summary>
        private bool _modelSuppressed;

        /// <summary>
        /// True for anything sitting on its parent's surface rather than flying above it.
        ///
        /// The distinction matters because exaggeration means something different for the two. A
        /// satellite drawn a thousand times life size is floating in empty space and the lie is
        /// invisible; a lander drawn a thousand times life size is a mountain, interpenetrating
        /// the very surface whose position is the only thing it is there to report. This is the
        /// unfixed half of the bug the ceiling was added for: the ceiling stopped Viking being
        /// larger than Mars, and left it at a quarter of Mars's radius — 850 km of lander,
        /// standing on the ground.
        /// </summary>
        private bool IsLanded => Body.Site.HasValue;

        /// <summary>
        /// Size a model from its real dimensions, then bound it at both ends.
        ///
        /// The mesh is authored in metres at true scale, so the physically honest transform is
        /// just the render transform applied to a length. That produces something far below a
        /// pixel in every situation this application actually puts a spacecraft in, so the same
        /// minimum-apparent-size rule the planets use takes over almost immediately and
        /// essentially never switches off. <see cref="MaximumFractionOfParent"/> then stops it
        /// running away. Where the two bounds cross — the ceiling is already below the floor —
        /// there is no size at which the thing is both visible and honest, so it is not drawn.
        ///
        /// <see cref="SizeExaggeration"/> reports the factor actually used, because at these
        /// scales it is never 1 and the interface has to be able to say so.
        /// </summary>
        private void UpdateModelScale(in FrameContext ctx, in GroupPlacement placement)
        {
            const double MetresPerKm = 1000.0;

            // Render metres per real metre, INCLUDING the group similarity: a spacecraft rides its
            // planet's group, so it must shrink with it or it grows through the planet as the
            // group compresses.
            float trueScale = (float)(placement.Scale * ctx.UnitsPerKm / MetresPerKm);
            float trueDiameter = trueScale * _model.LengthMetres;
            if (trueDiameter < 1e-12f) trueDiameter = 1e-12f;

            float diameter = trueDiameter;
            bool suppressed = false;
            float markerDiameter = 0f;

            {
                float distance = Vector3.Distance(ctx.EyeUniverse, transform.localPosition);

                // A LANDER IS NEVER EXAGGERATED.
                //
                // Everything below the floor is a judgement about an object floating in empty
                // space, where making it bigger costs nothing anyone can see. A lander is
                // touching its parent. Inflate it and it does not become a more visible lander,
                // it becomes a mountain — and its whole purpose is to say where on that surface
                // it is, which a 850 km blob standing on Mars actively destroys. So a landed
                // craft is drawn at true scale or it is drawn as a marker, and there is no third
                // option. It reaches true scale the moment you are close enough for it to matter.
                if (!IsLanded)
                {
                    diameter = Mathf.Max(trueDiameter, distance * ModelFloorDmm * 0.001f);

                    float ceiling = ParentSizeCeiling(ctx, placement);
                    if (ceiling > 0f && diameter > ceiling) diameter = ceiling;

                    // Never shrink a spacecraft below its real size; that would be a lie in the
                    // other direction, and it cannot happen for anything the catalogue contains.
                    if (diameter < trueDiameter) diameter = trueDiameter;
                }

                float angle = distance > 1e-9f ? diameter / distance : 0f;

                // Asymmetric, so an object sitting on the threshold does not strobe.
                float threshold = (_modelSuppressed ? ModelMarkerDmm * ModelHysteresis : ModelMarkerDmm) * 0.001f;
                suppressed = angle < threshold;

                markerDiameter = distance * MarkerDmm * 0.001f;
            }

            if (suppressed != _modelSuppressed)
            {
                _modelSuppressed = suppressed;
                RefreshRendererEnabled();
            }

            // Below the threshold the mesh is replaced by a marker, not by nothing.
            //
            // A spacecraft that simply vanishes is indistinguishable from one that failed to
            // load, and the threshold is crossed constantly — an eccentric orbit sweeps across it
            // twice a period. The dot is the same point spread every star in the app is drawn
            // with, at a fixed angular size, in the interface accent so it can never be mistaken
            // for a star. Paired with BodyLabelLayer keeping spacecraft named below the size
            // threshold, the object degrades to a labelled dot instead of disappearing — which is
            // the same rule the orbit lines already follow, and the same one a planetarium does.
            if (_starPoint != null)
            {
                if (suppressed)
                {
                    _starPoint.SetColour(CosmoTheme.Accent);
                    _starPoint.SetRenderQueue(StarPointBillboard.MarkerQueue);
                }
                _starPoint.SetVisible(suppressed, markerDiameter);
            }
            IsMarkerDrawn = suppressed;

            SizeExaggeration = trueDiameter > 0f ? diameter / trueDiameter : 1.0;

            // While the marker is what is on screen, RenderedRadius has to describe the marker.
            //
            // Everything that aims at, outlines or offsets from a body reads this, and the mesh's
            // own transform is meaningless while its renderer is off. UpdateStarPointScale keeps
            // the same contract for the same reason.
            if (suppressed)
            {
                _sphere.localScale = new Vector3(markerDiameter, markerDiameter, markerDiameter);
                return;
            }

            float scale = trueScale * (trueDiameter > 0f ? diameter / trueDiameter : 1f);
            _sphere.localScale = new Vector3(scale, scale, scale);
        }

        /// <summary>
        /// True when this body is drawn as a marker dot in place of its own geometry.
        ///
        /// Distinct from <see cref="IsGeometryDrawn"/> on purpose. A decoration that traces a
        /// silhouette — the atmosphere shell, a ring — must not draw around a marker, because
        /// there is no silhouette. But the marker is on screen, so anything the user can point at
        /// must count it; see <see cref="IsPointable"/>.
        /// </summary>
        public bool IsMarkerDrawn { get; private set; }

        /// <summary>
        /// True when there is something at this body's position the user can aim at.
        ///
        /// Selection has to follow what is visible, not what kind of thing is visible. A
        /// spacecraft that has degraded to a marker is still the only thing on screen at that
        /// point, and being unable to select the dot you can plainly see is the same failure as
        /// being able to select the body you cannot — just in the other direction.
        /// </summary>
        public bool IsPointable => IsGeometryDrawn || IsMarkerDrawn;

        /// <summary>
        /// Largest render-unit diameter this spacecraft may be drawn at, from the size of the
        /// body it is attached to. Zero when it has no parent in the catalogue.
        /// </summary>
        private float ParentSizeCeiling(in FrameContext ctx, in GroupPlacement placement)
        {
            if (_simulation == null || string.IsNullOrEmpty(Body.ParentId)) return 0f;
            if (!_simulation.Views.TryGetValue(Body.ParentId, out CelestialBodyView parent)) return 0f;
            if (parent?.Body == null) return 0f;

            // The parent's display radius, not its rendered one: the rendered value may itself
            // have been floored to stay visible, and deriving a ceiling from a floor would let
            // the two inflate each other.
            double parentRadiusKm = _simulation.DisplayRadiusKm(parent.Body);
            return (float)(placement.Scale * parentRadiusKm * ctx.UnitsPerKm) * MaximumFractionOfParent;
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

            Material material = _renderer.sharedMaterial;
            if (material == null) return;
            material.mainTexture = texture;

            // Always bound, and never as a NormalMap-typed import (R13): CosmoSynth/Body unpacks
            // with UnpackNormalRGBNoScale, and Unity swizzles a NormalMap-typed texture into
            // DXT5nm/ASTC with X in alpha — which the shader would read as garbage, silently, on
            // the terminator. PlanetTextureBaker creates these with linear: true.
            if (normalMap != null) material.SetTexture(NormalMapId, normalMap);

            // Smoothness rides in the surface map's ALPHA, so oceans come out glossy enough to
            // throw a sun glint while rock stays matte. With the map present the scalar is the
            // multiplier and must be 1, or the map is silently scaled to nothing.
            if (surfaceMap != null)
            {
                material.SetTexture(SurfaceMapId, surfaceMap);
                material.SetFloat(SmoothnessId, 1f);
            }

            // The tint multiplies the texture, so leave it white or the map is double-darkened
            // by the colour it was already painted with.
            material.color = CosmoTheme.Neutral;

            // Rough, with no metal anywhere.
            //
            // A broad specular highlight in the middle of the lit face is a planet rendered like
            // polished plastic, blowing out to pure white exactly where the surface detail is.
            // Rock, ice and cloud are all diffuse; the only body that genuinely wants a highlight
            // is an ocean, and that is what the surface map's alpha is for.
            if (surfaceMap == null) material.SetFloat(SmoothnessId, 0.03f);

            if (Body.IsStar)
            {
                // CosmoSynth/Body multiplies the emission by the albedo, and the albedo IS this
                // texture — so one colour does what a separate emission map used to.
                material.SetColor(EmissionId, CosmoTheme.Scaled(CosmoTheme.Neutral, 2.2f));
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

            // Swap in the finer sphere once this body is big enough for its edges to show.
            //
            // Unity's primitive is a 20-gon in silhouette. On a body filling a good part of the
            // view that is five pixels of visible scalloping around the limb — the single
            // highest-contrast edge in the frame — and on the two hundred bodies drawn at a
            // couple of pixels it is free. So it is paid for exactly where it is visible, which
            // is a handful of bodies at a time.
            UpdateSphereDetail(level);

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
            // The one exception is a body the floor has already handed to the point path, which
            // has decided for itself whether to draw — re-enabling it here would put every
            // exoplanet back on top of the star it orbits.
            RefreshRendererEnabled();
        }

        /// <summary>
        /// Apply every reason this body might not be drawn, in one place.
        ///
        /// There are four, they are set by three different callers on different frames, and
        /// they used to be applied wherever each was decided. That is how the LOD pass came to
        /// switch a spacecraft back on the frame after the clock left its ephemeris: the last
        /// writer won, and which one that was depended on execution order.
        /// </summary>
        private void RefreshRendererEnabled()
        {
            IsGeometryDrawn = IsKnown && !DrawnAsPoint && !_modelSuppressed && !_insideNearPlane;
            if (_renderer != null) _renderer.enabled = IsGeometryDrawn;
        }

        /// <summary>
        /// The whole body renders inside the near plane, so nothing of it can appear. Set by
        /// <see cref="UpdateVisualScale"/>, applied by <see cref="RefreshRendererEnabled"/>.
        /// </summary>
        private bool _insideNearPlane;

        /// <summary>
        /// Whether this body's own geometry is being drawn this frame, having applied every
        /// reason it might not be.
        ///
        /// Anything that decorates a body — an atmosphere shell, a rim highlight, a ring — must
        /// consult this rather than drawing unconditionally. A body pinned to the sky sphere
        /// switches its sphere off but keeps a <see cref="RenderedRadius"/>, so a decoration
        /// that only reads the radius carries on drawing at whatever size the minimum-visible
        /// clamp produced at horizon distance. That is what put a translucent dome the size of
        /// the sky over half the view: eight distant planets' atmospheres, all still being
        /// drawn around planets that were not.
        /// </summary>
        public bool IsGeometryDrawn { get; private set; } = true;

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

        // The three static axis helpers that used to live here (ToUnityRotation,
        // ToUnityDirection, ToIcrfDirection) are gone. They were a SECOND account of the
        // right-handed-to-left-handed conversion, they disagreed with the one in the floating
        // origin, and a renderer is the wrong place for a coordinate convention. Every caller now
        // goes through FloatingOrigin, which is the only file gate G0d lets reorder an axis.

        private void OnDestroy() => DestroyMaterials();

        /// <summary>
        /// Release the material instances this view owns.
        ///
        /// Reading <c>.material</c> or assigning <c>.materials</c> both instantiate, and both
        /// leak unless destroyed. A spacecraft carries one per submesh, so the singular form
        /// this used to call would have freed the first and abandoned the rest — and the views
        /// are rebuilt from scratch every time a catalogue merges, which happens twice at
        /// startup.
        /// </summary>
        private void DestroyMaterials()
        {
            if (_renderer == null || !Application.isPlaying) return;

            // sharedMaterials, not materials. The plural getter *instantiates* — so reading it
            // here to find what to destroy would create a fresh set on the way out, which is
            // both a leak and a warning during teardown. Every path through BuildVisual and
            // ApplyModel has already instantiated, so the shared array is our own instances.
            Material[] materials = _renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null) Destroy(materials[i]);
            }
        }
    }
}
