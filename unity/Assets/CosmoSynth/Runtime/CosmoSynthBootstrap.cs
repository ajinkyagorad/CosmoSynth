using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Builds the whole running application from code: XR rig, passthrough, simulation, clock,
    /// origin, hand and controller input, orbit lines and HUD.
    ///
    /// Constructing the scene in code rather than shipping a hand-authored .unity asset is a
    /// deliberate choice. A scene file is opaque YAML full of GUIDs — it cannot be reviewed in
    /// a diff, it breaks when a component is renamed, and it is the single most merge-hostile
    /// kind of file a Unity project can contain. This is legible, versionable, and works the
    /// moment the project opens with no manual wiring at all.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Bootstrap")]
    public sealed class CosmoSynthBootstrap : MonoBehaviour
    {
        [Header("Catalogue")]
        [SerializeField] private string _catalogPath = "CosmoSynth/catalog/solar_system.json";

        [Tooltip("Ignore the catalogue file and generate a procedural system from this seed instead. 0 uses the file.")]
        [SerializeField] private long _proceduralSeed;

        [Tooltip("Also load the NASA Exoplanet Archive catalogue and place its host stars.")]
        [SerializeField] private bool _loadExoplanets = true;

        [SerializeField] private string _exoplanetPath = "CosmoSynth/catalog/exoplanets.json";

        [Tooltip("Also load the spacecraft: real trajectories from JPL, real models from NASA.")]
        [SerializeField] private bool _loadSpacecraft = true;

        [SerializeField] private string _spacecraftPath = "CosmoSynth/catalog/spacecraft.json";

        [Header("Start conditions")]
        [Tooltip("ISO-8601 UTC start instant. Empty means the real current time.")]
        [SerializeField] private string _startEpochUtc = "";

        [SerializeField] private string _focusBodyId = "earth";

        [Header("Mixed reality")]
        [Tooltip("Start with passthrough on, so the solar system sits in your actual room.")]
        [SerializeField] private bool _passthrough = true;

        [Tooltip("How far in front of you the universe is placed, in metres.")]
        [SerializeField] private float _anchorDistance = 0.7f;

        [Tooltip("Height of the universe relative to your eyes, in metres. Negative is below eye level.")]
        [SerializeField] private float _anchorHeightBelowEyes = -0.25f;

        [Tooltip("Diameter the focused body occupies at startup, in metres.")]
        [SerializeField] private float _initialHologramMetres = 0.55f;

        [Header("Features")]
        [SerializeField] private bool _drawOrbits = true;
        [SerializeField] private bool _showHud = true;

        [Tooltip("Also add keyboard/mouse flight, for testing in the editor.")]
        [SerializeField] private bool _enableDesktopInput = true;

        [Header("Presentation")]
        [Range(0f, 1f)]
        [Tooltip("1 is true relative scale. Lower pulls the size range together so moons stay visible.")]
        [SerializeField] private float _sizeCompression = 0.55f;

        public UniverseSimulation Simulation { get; private set; }
        public XrRig Rig { get; private set; }
        public Transform UniverseAnchor { get; private set; }

        private SimulationClock _clock;

        private void Awake()
        {
            BuildRig();
            BuildSimulation();
        }

        private void BuildRig()
        {
            var rigObject = new GameObject("XR Rig");
            Rig = rigObject.AddComponent<XrRig>();
            // XrRig reads this in its own Awake, which has already run by the time AddComponent
            // returns, so passthrough is applied through the public setter instead.
            Rig.SetPassthrough(_passthrough);

            rigObject.AddComponent<ScaleRenderer>();
        }

        private void BuildSimulation()
        {
            // The anchor is what makes this mixed reality rather than a viewer: the universe is
            // a physical object at a place in the room, not a backdrop locked to the camera.
            // Everything the simulation creates is parented here, so moving this one transform
            // moves the whole solar system — which is exactly what the grab gesture does.
            var anchorObject = new GameObject("Universe Anchor");
            anchorObject.transform.SetParent(Rig.Origin.CameraFloorOffsetObject.transform, false);
            UniverseAnchor = anchorObject.transform;

            // Placed in front of the user in Start, not here.
            //
            // A fixed offset in tracking space is measured from the play-area origin — the spot
            // the guardian was drawn around — which is not where the user is standing and not
            // the direction they are facing. A 55 cm hologram put 65 cm along tracking +Z ends
            // up somewhere arbitrary in the room, very possibly behind the user or inside a
            // wall. The app then looks completely broken: passthrough works, and there is
            // simply nothing in it.
            anchorObject.transform.localPosition = new Vector3(0f, 1.05f, 0.7f);

            var host = new GameObject("Universe");
            host.transform.SetParent(UniverseAnchor, false);

            _clock = host.AddComponent<SimulationClock>();
            host.AddComponent<FloatingOrigin>();

            Simulation = host.AddComponent<UniverseSimulation>();
            Simulation.SizeCompression = _sizeCompression;

            if (!string.IsNullOrWhiteSpace(_startEpochUtc) &&
                SimulationClock.TryParseIso8601(_startEpochUtc, out CosmicTime epoch))
            {
                _clock.Now = epoch;
            }

            // The controller bindings, read once per frame and handed out from one place. Added
            // before anything that consumes input; it also runs at a negative execution order, so
            // its edge flags are already current when the consumers look at them.
            host.AddComponent<XrInput>();

            host.AddComponent<TabletopManipulator>();
            var handInput = host.AddComponent<XrHandInput>();
            handInput.RecenterRequested += Recenter;

            // Thumbstick flight. The grab gesture above is for placing the universe precisely;
            // this is for going somewhere, and it is the control users reach for first.
            host.AddComponent<XrLocomotion>();

            // Always-visible marker at the anchor. Added before the catalogue loads, so there is
            // something in the headset even if loading fails entirely.
            UniverseAnchor.gameObject.AddComponent<AnchorBeacon>();

            // Point-and-pinch selection with the world-space provenance panel.
            host.AddComponent<BodySelector>();

            // BuildLayers(): the populations that are not catalogue bodies -- stars in three
            // dimensions, the dust volume, the galaxies, the microwave background. Parented to the
            // universe host, because every one of them places itself in universe-local space and
            // anywhere else would offset the entire deep sky by the anchor.
            host.AddComponent<DeepSkyLayers>();

            // Wrist dashboard: the scale ladder and destination list. Parented to the tracking
            // space rather than to the universe, because it belongs to the user, not the scene.
            var dashboardObject = new GameObject("Wrist Dashboard");
            dashboardObject.transform.SetParent(Rig.Origin.CameraFloorOffsetObject.transform, false);
            dashboardObject.AddComponent<WristDashboard>();

            // Surface textures, generated on worker threads and applied a body at a time.
            host.AddComponent<PlanetTextureBaker>();

            // Spacecraft geometry, streamed out of StreamingAssets a mesh at a time.
            host.AddComponent<SpacecraftModelLoader>();

            // Worlds around whichever real star you are nearest to, generated on arrival from
            // that star's own measured position and discarded when you leave.
            host.AddComponent<NearbyStarSystems>();

            // Names for whatever is on screen. Parented under the simulation, so labels travel
            // with the universe when the anchor is moved, exactly as the bodies do.
            var labelObject = new GameObject("Body Labels");
            labelObject.transform.SetParent(host.transform, false);
            labelObject.AddComponent<BodyLabelLayer>();

            if (_showHud) host.AddComponent<ProvenanceHud>();

            Simulation.CatalogLoaded += OnCatalogLoaded;

            if (_proceduralSeed != 0)
            {
                Adopt(PlanetGenerator.GenerateSystem(new PlanetGenerator.SystemSettings
                {
                    Seed = _proceduralSeed,
                    Name = $"System {_proceduralSeed}",
                    Epoch = _clock.Now
                }));
                return;
            }

            // Asynchronous, because on Android StreamingAssets lives inside the compressed APK
            // and can only be reached through UnityWebRequest. Using the coroutine path on every
            // platform means the headset and the desktop run the same code, rather than the
            // device taking a branch that was never exercised in the editor.
            StartCoroutine(LoadCatalogRoutine());
        }

        /// <summary>
        /// Read every catalogue file, merge them in Core, and adopt the result <b>once</b>.
        ///
        /// Asynchronous, because on Android StreamingAssets lives inside the compressed APK and
        /// can only be reached through UnityWebRequest. Using the coroutine path on every platform
        /// means the headset and the desktop run the same code, rather than the device taking a
        /// branch that was never exercised in the editor.
        ///
        /// Order matters and is not cosmetic: the base file first, because its first root is what
        /// <c>RootIds[0]</c> means; then the spacecraft, which hang off Solar System bodies, so a
        /// lander's position is only meaningful once Mars is in the tree.
        /// </summary>
        private System.Collections.IEnumerator LoadCatalogRoutine()
        {
            var sources = new System.Collections.Generic.List<CatalogAssembler.Source>();

            string json = null;
            yield return Simulation.ReadStreamingAsset(_catalogPath, result => json = result);
            sources.Add(new CatalogAssembler.Source("solar_system", json));

            if (_loadSpacecraft)
            {
                string spacecraftJson = null;
                yield return Simulation.ReadStreamingAsset(_spacecraftPath, result => spacecraftJson = result);
                sources.Add(new CatalogAssembler.Source("spacecraft", spacecraftJson));
            }

            // Exoplanets are a separate file so a Solar-System-only build ships 87 kB instead of
            // 2.2 MB. CONTRACT C10 retires them from 2.0.0-alpha.1 entirely; when the data half
            // deletes the file this flag goes with it, and nothing else has to change.
            if (_loadExoplanets)
            {
                string exoplanetJson = null;
                yield return Simulation.ReadStreamingAsset(_exoplanetPath, result => exoplanetJson = result);
                sources.Add(new CatalogAssembler.Source("exoplanets", exoplanetJson));
            }

            var log = new System.Collections.Generic.List<string>();
            CelestialCatalog assembled = CatalogAssembler.Assemble(sources, log);
            foreach (string line in log) Debug.Log($"[CosmoSynth] catalogue {line}");

            if (assembled == null)
            {
                // A missing catalogue is a setup problem, not a crash. Falling back to a
                // generated system means the app still does something useful, and the log says
                // exactly what to run to get the real data.
                Debug.LogWarning(
                    "[CosmoSynth] Falling back to a procedurally generated system. " +
                    "Run 'python3 tools/cosmodata/build_solar_system.py' for the real Solar System.");
                assembled = PlanetGenerator.GenerateSystem(
                    new PlanetGenerator.SystemSettings { Seed = 20260730, Name = "Fallback", Epoch = _clock.Now });
            }

            Adopt(assembled);
        }

        /// <summary>
        /// The one place a whole catalogue is adopted. Gate G0d allows exactly one such call site
        /// in the runtime, which is what stops the three-adoptions-at-startup bug returning by a
        /// different route.
        /// </summary>
        private void Adopt(CelestialCatalog catalog) => Simulation.SetCatalog(catalog);

        private void OnCatalogLoaded(CelestialCatalog catalog)
        {
            if (_drawOrbits) CreateOrbitLines();

            // Frame the focus body as a hologram of the requested size, centred on the anchor.
            var focus = catalog.Get(_focusBodyId) ?? catalog.Get(catalog.RootIds[0]);
            if (focus != null && Simulation.Views.TryGetValue(focus.Id, out var view))
            {
                Simulation.SetFocus(focus.Id);

                double radiusKm = focus.EquatorialRadiusKm ?? 6371.0;
                Simulation.Origin.SetUnitsPerKm(
                    ScaleSpace.HologramUnitsPerKm(radiusKm, _initialHologramMetres));
                Simulation.Origin.SetOrigin(view.AbsolutePositionKm);

                Debug.Log($"[CosmoSynth] anchored on {focus.Name} at {_initialHologramMetres:F2} m " +
                          $"({Simulation.Origin.UnitsPerKm:G3} units/km)");
            }

            // Star field, rings, the point-source Sun and atmospheric limbs. Built after the
            // scale is set, because the ring meshes are generated in render units.
            // Get-or-add, because this runs again when the exoplanet catalogue merges in and
            // SkyAndRings is [DisallowMultipleComponent] — a second AddComponent returns null,
            // and the NullReferenceException on the next line took the whole rest of the
            // bootstrap with it.
            var sky = UniverseAnchor.gameObject.GetComponent<SkyAndRings>()
                      ?? UniverseAnchor.gameObject.AddComponent<SkyAndRings>();
            sky.Build(Simulation, UniverseAnchor);

            // Desktop flight is only useful when there is no headset; with one, the rig owns the
            // camera and a second thing writing to it would fight the pose driver.
            if (_enableDesktopInput && !Rig.XrActive)
            {
                // Get-or-add again — same reason as the sky above. This runs once per catalogue
                // load, and both of these are [DisallowMultipleComponent].
                GameObject cameraObject = Rig.Camera.gameObject;
                var navigator = cameraObject.GetComponent<UniverseNavigator>()
                                ?? cameraObject.AddComponent<UniverseNavigator>();
                if (cameraObject.GetComponent<DesktopInputSource>() == null)
                    cameraObject.AddComponent<DesktopInputSource>();
                navigator.AbsolutePositionKm = Simulation.Origin.OriginKm;
            }
        }

        private void Start() => StartCoroutine(PlaceAnchorWhenTrackingSettles());

        /// <summary>
        /// Put the universe in front of wherever the user is actually looking.
        ///
        /// Waits for the head pose to become valid first. On the first frames after launch the
        /// camera sits at the origin with identity rotation, so anchoring immediately would
        /// place the hologram relative to a pose that has not arrived yet — which is the same
        /// failure as anchoring to the play-area origin, just harder to spot.
        /// </summary>
        private System.Collections.IEnumerator PlaceAnchorWhenTrackingSettles()
        {
            // Give the runtime a moment to report a real pose. Waiting on a non-identity
            // rotation is a better signal than a frame count, but cap it either way so a flat
            // run (where the pose never changes) still places the anchor.
            const int maximumFrames = 180;
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                bool poseLooksReal = Rig.Camera.transform.localPosition.sqrMagnitude > 1e-6f ||
                                     Quaternion.Angle(Rig.Camera.transform.localRotation, Quaternion.identity) > 0.5f;
                if (poseLooksReal && frame > 5) break;
                yield return null;
            }

            Recenter();
        }

        /// <summary>
        /// Move the universe back in front of the user. Bound to the controller face button and
        /// available from the HUD — because "I have lost the solar system" must always be
        /// recoverable without relaunching.
        /// </summary>
        public void Recenter()
        {
            if (Rig == null || Rig.Camera == null || UniverseAnchor == null) return;

            Transform camera = Rig.Camera.transform;

            // Use only the horizontal component of gaze, so the universe lands in front of the
            // user rather than above or below them when they happen to be looking up or down.
            Vector3 forward = camera.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 target = camera.localPosition
                             + forward * _anchorDistance
                             + Vector3.up * _anchorHeightBelowEyes;

            UniverseAnchor.localPosition = target;

            // Face the user, so any orientation-dependent presentation reads correctly.
            UniverseAnchor.localRotation = Quaternion.LookRotation(-forward, Vector3.up);

            TurnFocusIntoTheLight(camera);

            Debug.Log($"[CosmoSynth] universe re-anchored to {target} (tracking space), " +
                      $"camera at {camera.localPosition}");
        }

        /// <summary>
        /// Spin the universe about the room's vertical so the focused body faces the user lit.
        ///
        /// Where a planet's day side happens to point is decided by the epoch and by where the
        /// body is in its orbit, and roughly half the time that is away from the viewer. Launch
        /// on one of those and the app opens on a black sphere with a hairline of atmosphere —
        /// technically a correct render of the night side, and indistinguishable from a broken
        /// one. Since the anchor's orientation in the room is already arbitrary, choosing it is
        /// free, and it is the difference between opening on the Blue Marble and opening on
        /// nothing.
        ///
        /// Only yaw is applied, so the ecliptic stays level with the table. The offset from
        /// straight-on is deliberate: a fully lit disc has no terminator and reads flat, whereas
        /// a gibbous phase shows the curvature, the shadow line and the atmosphere along it.
        /// </summary>
        private void TurnFocusIntoTheLight(Transform camera)
        {
            const float TerminatorOffsetDegrees = 32f;

            CelestialBodyView focus = Simulation != null ? Simulation.FocusBody : null;
            if (focus == null || Simulation.Catalog == null) return;

            // The star this body ultimately orbits.
            CelestialBody star = focus.Body;
            for (int hops = 0; hops < 16 && star != null && !star.IsStar; hops++)
            {
                star = string.IsNullOrEmpty(star.ParentId) ? null : Simulation.Catalog.Get(star.ParentId);
            }
            if (star == null || !Simulation.Views.TryGetValue(star.Id, out CelestialBodyView starView)) return;

            Vector3 toStar = starView.transform.position - focus.transform.position;
            Vector3 toViewer = camera.position - focus.transform.position;

            // Work in the horizontal plane: yaw is the only freedom being used.
            toStar.y = 0f;
            toViewer.y = 0f;
            if (toStar.sqrMagnitude < 1e-8f || toViewer.sqrMagnitude < 1e-8f) return;

            float degrees = Vector3.SignedAngle(toStar.normalized, toViewer.normalized, Vector3.up);
            UniverseAnchor.Rotate(Vector3.up, degrees - TerminatorOffsetDegrees, Space.World);
        }

        private void OnDestroy()
        {
            if (Simulation != null) Simulation.CatalogLoaded -= OnCatalogLoaded;
        }

        private readonly System.Collections.Generic.List<GameObject> _orbitLines =
            new System.Collections.Generic.List<GameObject>();

        private void CreateOrbitLines()
        {
            // Clear first.
            //
            // This runs once per catalogue load and the catalogue loads twice — once for the
            // Solar System and again when the exoplanets merge in — so without this every orbit
            // existed twice. The play-mode report says so plainly: 60 line renderers for 30
            // orbits, each pair drawn on top of itself. Two semi-transparent lines at the same
            // place composite to roughly twice the intended alpha, so the paths were both heavier
            // than designed and twice the per-frame cost, and neither symptom named its cause.
            for (int i = 0; i < _orbitLines.Count; i++)
            {
                if (_orbitLines[i] != null) Destroy(_orbitLines[i]);
            }
            _orbitLines.Clear();

            // Only for the focused system.
            //
            // Every orbit line is a 256-vertex LineRenderer rebuilt each frame; drawing one for
            // all 753 exoplanets is ~190 000 vertices of per-frame CPU work for paths that are
            // light years away and subtend nothing. Orbits are created for the Solar System, and
            // an exoplanet system gets them when it becomes the focus.
            int created = 0;
            foreach (var view in Simulation.Views.Values)
            {
                if (view?.Body?.Orbit == null || !view.Body.Orbit.HasValue) continue;
                if (view.Body.Id.StartsWith("exo-", System.StringComparison.Ordinal)) continue;
                created++;

                var lineObject = new GameObject($"Orbit_{view.Body.Id}");
                lineObject.transform.SetParent(Simulation.transform, worldPositionStays: false);
                _orbitLines.Add(lineObject);

                var line = lineObject.AddComponent<OrbitLineRenderer>();

                // Initialise derives the colour from the body itself; a moon only needs to be
                // dimmed so a gas giant's satellite system is not an unreadable tangle.
                line.Initialise(Simulation, view);
                if (view.Body.Type == BodyType.Moon) line.Dim(0.45f);
            }

            Debug.Log($"[CosmoSynth] {created} orbit lines created (exoplanet orbits are drawn on focus)");
        }
    }
}
