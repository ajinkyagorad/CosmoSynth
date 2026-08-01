using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CosmoSynth.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The centre of the running application: it owns the catalogue, the reference-frame tree,
    /// the clock and the floating origin, and drives every body's position each frame.
    ///
    /// The division of labour is deliberate. All the physics lives in CosmoSynth.Core, which
    /// knows nothing about Unity and is unit-tested headlessly. This class does only the
    /// things that need an engine: loading files, creating GameObjects, and pushing double
    /// positions into float transforms once per frame. Keeping that boundary sharp is what
    /// makes the orbital mechanics testable at all.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("CosmoSynth/Universe Simulation")]
    public sealed class UniverseSimulation : MonoBehaviour
    {
        [Header("Catalogue")]
        [Tooltip("Path under StreamingAssets to the catalogue to load.")]
        [SerializeField] private string _catalogPath = "CosmoSynth/catalog/solar_system.json";

        [Tooltip("Load the catalogue on Awake. Off by default: the bootstrap loads it asynchronously, " +
                 "which is the only way that works on Android.")]
        [SerializeField] private bool _loadOnAwake;

        [Header("References")]
        [SerializeField] private SimulationClock _clock;
        [SerializeField] private FloatingOrigin _origin;

        [Header("Presentation")]
        [Tooltip("1 = true relative scale. Lower values pull the size range together so small bodies stay visible.")]
        [Range(0f, 1f)]
        [SerializeField] private float _sizeCompression = 1f;

        [Tooltip("Multiplier applied to every body's radius. Purely cosmetic; 1 is physically correct.")]
        [SerializeField] private float _radiusExaggeration = 1f;

        [Header("Diagnostics")]
        [SerializeField] private int _bodyCount;

        private readonly Dictionary<string, CelestialBodyView> _views =
            new Dictionary<string, CelestialBodyView>(StringComparer.OrdinalIgnoreCase);

        private ReferenceFrame _rootFrame;

        /// <summary>The loaded catalogue, or null before one has been loaded.</summary>
        public CelestialCatalog Catalog { get; private set; }

        /// <summary>The live reference-frame tree mirroring the catalogue.</summary>
        public ReferenceFrame RootFrame => _rootFrame;

        public SimulationClock Clock => _clock;
        public FloatingOrigin Origin => _origin;

        /// <summary>The body the camera is currently anchored to, if any.</summary>
        public CelestialBodyView FocusBody { get; private set; }

        /// <summary>Raised once the catalogue has been loaded and all views exist.</summary>
        public event Action<CelestialCatalog> CatalogLoaded;

        /// <summary>Raised when the focused body changes.</summary>
        public event Action<CelestialBodyView> FocusChanged;

        public IReadOnlyDictionary<string, CelestialBodyView> Views => _views;

        public float SizeCompression
        {
            get => _sizeCompression;
            set => _sizeCompression = Mathf.Clamp01(value);
        }

        public float RadiusExaggeration
        {
            get => _radiusExaggeration;
            set => _radiusExaggeration = Mathf.Max(0.001f, value);
        }

        private void Awake()
        {
            if (_clock == null) _clock = GetComponent<SimulationClock>() ?? gameObject.AddComponent<SimulationClock>();
            if (_origin == null) _origin = GetComponent<FloatingOrigin>() ?? gameObject.AddComponent<FloatingOrigin>();

            if (_loadOnAwake) LoadCatalogFromStreamingAssets(_catalogPath);
        }

        /// <summary>
        /// Read the catalogue out of StreamingAssets, on any platform, and hand the JSON to
        /// <paramref name="onComplete"/>. The string is null if it could not be read.
        ///
        /// Two code paths are needed because StreamingAssets is not a directory everywhere. On
        /// Android — and therefore on Quest — it lives compressed inside the APK, where
        /// System.IO cannot reach it at all and every File API silently reports "not found".
        /// UnityWebRequest is the only way in, and it is asynchronous, which is why this is a
        /// coroutine rather than a plain function.
        /// </summary>
        public IEnumerator ReadStreamingAsset(string relativePath, Action<string> onComplete)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);

            // A path that already carries a URI scheme (Android's jar:file://, or WebGL's http)
            // must go through the web request; a plain filesystem path need not, but doing so
            // uniformly would add a frame of latency on desktop for no benefit.
            bool needsWebRequest = fullPath.Contains("://");

            if (!needsWebRequest)
            {
                string text = null;
                if (File.Exists(fullPath))
                {
                    try
                    {
                        text = File.ReadAllText(fullPath);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[CosmoSynth] Failed to read '{fullPath}': {exception.Message}");
                    }
                }
                else
                {
                    Debug.LogError(
                        $"[CosmoSynth] Catalogue not found at '{fullPath}'. " +
                        "Build it with 'python3 tools/cosmodata/build_solar_system.py'.");
                }

                onComplete?.Invoke(text);
                yield break;
            }

            using (var request = UnityWebRequest.Get(fullPath))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[CosmoSynth] Failed to read '{fullPath}': {request.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                onComplete?.Invoke(request.downloadHandler.text);
            }
        }

        /// <summary>
        /// The same read, for a binary asset: spacecraft meshes rather than JSON.
        ///
        /// Kept separate from <see cref="ReadStreamingAsset"/> rather than folded into it,
        /// because <c>downloadHandler.text</c> decodes as UTF-8 and would quietly mangle every
        /// byte sequence that is not valid UTF-8 — which for a mesh is most of them. The
        /// failure would be a model that loads without error and is made of noise.
        /// </summary>
        public IEnumerator ReadStreamingAssetBytes(string relativePath, Action<byte[]> onComplete)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            bool needsWebRequest = fullPath.Contains("://");

            if (!needsWebRequest)
            {
                byte[] bytes = null;
                if (File.Exists(fullPath))
                {
                    try
                    {
                        bytes = File.ReadAllBytes(fullPath);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[CosmoSynth] Failed to read '{fullPath}': {exception.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[CosmoSynth] Asset not found at '{fullPath}'.");
                }

                onComplete?.Invoke(bytes);
                yield break;
            }

            using (var request = UnityWebRequest.Get(fullPath))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[CosmoSynth] Failed to read '{fullPath}': {request.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                onComplete?.Invoke(request.downloadHandler.data);
            }
        }

        /// <summary>
        /// Load a catalogue from StreamingAssets and build the scene from it.
        ///
        /// Synchronous, and therefore desktop and editor only — see
        /// <see cref="ReadStreamingAsset"/> for the reason. On Android this returns false
        /// rather than pretending to work.
        /// </summary>
        public bool LoadCatalogFromStreamingAssets(string relativePath)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);

            if (fullPath.Contains("://"))
            {
                Debug.LogError(
                    "[CosmoSynth] StreamingAssets is not a readable directory on this platform. " +
                    "Use the ReadStreamingAsset coroutine instead of the synchronous loader.");
                return false;
            }

            if (!File.Exists(fullPath))
            {
                Debug.LogError(
                    $"[CosmoSynth] Catalogue not found at '{fullPath}'. " +
                    "Build it with 'python3 tools/cosmodata/build_solar_system.py'.");
                return false;
            }

            try
            {
                return LoadCatalogFromJson(File.ReadAllText(fullPath));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CosmoSynth] Failed to read '{fullPath}': {exception.Message}");
                return false;
            }
        }

        /// <summary>Load a catalogue from JSON text and build the scene from it.</summary>
        public bool LoadCatalogFromJson(string json)
        {
            CelestialCatalog catalog;
            try
            {
                catalog = CelestialCatalog.Parse(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CosmoSynth] Catalogue is malformed: {exception.Message}");
                return false;
            }

            return SetCatalog(catalog);
        }

        /// <summary>
        /// Merge a second catalogue's JSON into the loaded one, and rebuild the scene.
        ///
        /// Used for the exoplanets, which ship as a separate file so a Solar-System-only build
        /// does not carry 2 MB it will not use. Both are positioned in ICRF about the solar
        /// system barycentre, so they belong in one frame tree — that is what makes "how far is
        /// Proxima b from Earth" a question with an answer.
        /// </summary>
        public bool MergeCatalogFromJson(string json)
        {
            if (Catalog == null)
            {
                Debug.LogError("[CosmoSynth] cannot merge before a catalogue is loaded.");
                return false;
            }

            CelestialCatalog incoming;
            try
            {
                incoming = CelestialCatalog.Parse(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CosmoSynth] merged catalogue is malformed: {exception.Message}");
                return false;
            }

            int added = Catalog.MergeFrom(incoming);
            Debug.Log($"[CosmoSynth] merged '{incoming.Name}': {added} bodies added, " +
                      $"{Catalog.Count} total.");

            // Rebuild the whole scene from the combined catalogue, so the frame tree and the
            // views agree with it.
            return SetCatalog(Catalog);
        }

        /// <summary>Adopt an in-memory catalogue — the entry point the generators use.</summary>
        public bool SetCatalog(CelestialCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            // Validation is a warning, not a refusal. A catalogue with one implausible moon is
            // still worth exploring, and refusing to show it would make the problem harder to
            // diagnose, not easier. The messages name the body and the reason.
            var problems = catalog.Validate();
            foreach (string problem in problems) Debug.LogWarning($"[CosmoSynth] {problem}");

            ClearViews();

            Catalog = catalog;
            _rootFrame = catalog.BuildFrameTree();
            _bodyCount = catalog.Count;

            foreach (var body in catalog.Traverse()) CreateView(body);

            Debug.Log($"[CosmoSynth] Loaded '{catalog.Name}': {catalog.Count} bodies, " +
                      $"{problems.Count} validation warnings.");

            CatalogLoaded?.Invoke(catalog);

            if (FocusBody == null && catalog.RootIds.Count > 0) SetFocus(catalog.RootIds[0]);
            return true;
        }

        private void CreateView(CelestialBody body)
        {
            var host = new GameObject(body.Name ?? body.Id);
            host.transform.SetParent(transform, worldPositionStays: false);

            var view = host.AddComponent<CelestialBodyView>();
            view.Initialise(this, body, _rootFrame.Find(body.Id), _clock.Now);
            _views[body.Id] = view;
        }

        private void ClearViews()
        {
            foreach (var view in _views.Values)
            {
                if (view != null) Destroy(view.gameObject);
            }
            _views.Clear();
            FocusBody = null;
        }

        /// <summary>
        /// Where the viewer is, expressed in the universe's own space.
        ///
        /// Not the camera's world position. Bodies are placed with localPosition under this
        /// transform, which is the universe's anchor in the room, so anything comparing a body
        /// against the camera has to cross that transform first — and at tabletop scale getting
        /// it wrong is worth thousands of kilometres of apparent distance.
        /// </summary>
        public Vector3 ViewerLocalPosition { get; private set; }

        /// <summary>
        /// Render distance beyond which a body is drawn on the sky rather than in space, in
        /// render units. Zero disables the treatment.
        ///
        /// Set by <see cref="ScaleRenderer"/> from the depth range the camera can actually
        /// express. Past this distance parallax is unmeasurable anyway — nobody perceives depth
        /// at four light years — so pinning the direction and giving up the distance costs
        /// nothing visible and keeps the far plane finite.
        /// </summary>
        public float StarPointHorizonUnits { get; set; }

        private void LateUpdate()
        {
            if (Catalog == null || _rootFrame == null) return;

            Camera viewer = Camera.main;
            if (viewer != null)
                ViewerLocalPosition = transform.InverseTransformPoint(viewer.transform.position);

            CosmicTime now = _clock.Now;

            // Frame states are memoised per instant, so this walk costs one propagation per
            // body rather than one per ancestor per body.
            foreach (var view in _views.Values) view.UpdateState(now);
        }

        /// <summary>Absolute position of a body at an instant, in km, in the root frame.</summary>
        public Double3 AbsolutePositionKm(string bodyId, CosmicTime time)
        {
            var frame = _rootFrame?.Find(bodyId);
            return frame != null ? frame.AbsoluteState(time).Position : Double3.Zero;
        }

        /// <summary>Anchor the origin to a body, so the camera stays in its local frame.</summary>
        public void SetFocus(string bodyId)
        {
            if (!_views.TryGetValue(bodyId, out var view))
            {
                Debug.LogWarning($"[CosmoSynth] Cannot focus unknown body '{bodyId}'.");
                return;
            }

            FocusBody = view;
            FocusChanged?.Invoke(view);
        }

        /// <summary>
        /// Radius a body should be drawn at, after presentation scaling. The physical radius is
        /// always available separately; this is only for display, and the UI is expected to say
        /// when it is not 1:1.
        /// </summary>
        public double DisplayRadiusKm(CelestialBody body)
        {
            double radius = body.EquatorialRadiusKm ?? 1.0;

            if (_sizeCompression < 1f)
            {
                radius = ScaleSpace.PresentationRadius(radius, Constants.EarthEquatorialRadius, _sizeCompression);
            }

            return radius * _radiusExaggeration;
        }

        /// <summary>True when the view is not showing true relative proportions.</summary>
        public bool IsPresentationScaled => _sizeCompression < 1f || !Mathf.Approximately(_radiusExaggeration, 1f);

        /// <summary>
        /// Nearest body to an absolute position, and its surface distance in km.
        ///
        /// Bodies outside their coverage window are skipped. Their position is not merely
        /// unknown, it is stale: <see cref="CelestialBodyView.UpdateState"/> stops writing one
        /// the moment the clock leaves the window, so what is left is wherever the body was
        /// when it stopped being known — or, if it was never known since the catalogue loaded,
        /// the closed form extrapolated to an epoch it does not cover. Answering "what is
        /// nearest" with a spacecraft that is not there, at a position that is not real, is
        /// exactly the fabrication the window exists to prevent.
        /// </summary>
        public CelestialBodyView NearestBody(Double3 absoluteKm, out double surfaceDistanceKm)
        {
            CelestialBodyView nearest = null;
            surfaceDistanceKm = double.PositiveInfinity;

            foreach (var view in _views.Values)
            {
                if (view == null || !view.IsKnown) continue;

                double centreDistance = Double3.Distance(absoluteKm, view.AbsolutePositionKm);
                double radius = view.Body.EquatorialRadiusKm ?? 0.0;
                double surfaceDistance = System.Math.Max(0.0, centreDistance - radius);

                if (surfaceDistance < surfaceDistanceKm)
                {
                    surfaceDistanceKm = surfaceDistance;
                    nearest = view;
                }
            }

            return nearest;
        }
    }
}
