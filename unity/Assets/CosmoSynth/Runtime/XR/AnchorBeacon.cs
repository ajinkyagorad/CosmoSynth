using System.Text;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// A marker at the universe's anchor, a control legend, and a status card — the three things
    /// that explain an empty headset.
    ///
    /// This exists because of a specific failure: the first Quest build showed passthrough and
    /// nothing else. From inside the headset there was no way to tell whether the app had crashed,
    /// failed to load its data, placed the solar system behind the user, or rendered it too small
    /// to see — all four look identical, and there is no console on the device. The marker is
    /// deliberately the dumbest thing that can still be seen: an unlit mesh at a known place, at a
    /// fixed size in metres, drawn before any of the simulation runs.
    ///
    /// What it was <i>not</i> allowed to be is the app's main interface, and that is what it had
    /// become. Being the only surface visible at launch, its diagnostics were the first and often
    /// only thing a user read — seven lines of unbacked centred text, sized in metres so it grew
    /// with distance, positioned 18 cm above an anchor holding a 55 cm globe, which put it inside
    /// the planet. The shipped screenshot shows "XR: NOT ACTIVE (flat)" written across the middle
    /// of Earth. So it now sits beside the hologram rather than in it, on a real panel, with the
    /// legend a reader needs separated from the diagnostics a developer needs.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Anchor Beacon")]
    public sealed class AnchorBeacon : MonoBehaviour
    {
        [Tooltip("Edge length of the marker cross, in metres.")]
        [SerializeField] private float _sizeMetres = 0.06f;

        [Tooltip("Hide the marker and diagnostics once the simulation has bodies on screen.")]
        [SerializeField] private bool _hideWhenHealthy = true;

        [Tooltip("Seconds the control legend stays up after the app is healthy.")]
        [SerializeField] private float _legendSeconds = 20f;

        private UniverseSimulation _simulation;
        private XrRig _rig;
        private XrHandInput _input;
        private Transform _marker;
        private CosmoPanel _panel;
        private Material _material;
        private float _elapsed;
        private bool _healthy;

        private void Awake()
        {
            _simulation = FindAnyObjectByType<UniverseSimulation>();
            _rig = FindAnyObjectByType<XrRig>();
            _input = FindAnyObjectByType<XrHandInput>();

            BuildMarker();
            _panel = new CosmoPanel(transform, "Status Card");
        }

        private void BuildMarker()
        {
            var markerObject = new GameObject("Beacon");
            markerObject.transform.SetParent(transform, false);
            _marker = markerObject.transform;

            // Three thin bars forming an axis cross. Unlit, so it shows even with no light in the
            // scene — one of the things that could be wrong.
            _material = CosmoShaders.Material(CosmoShaders.Unlit);
            if (_material == null) return;
            _material.color = CosmoTheme.Accent;

            CreateBar(new Vector3(1f, 0.06f, 0.06f));
            CreateBar(new Vector3(0.06f, 1f, 0.06f));
            CreateBar(new Vector3(0.06f, 0.06f, 1f));
        }

        private void CreateBar(Vector3 proportions)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Bar";
            bar.transform.SetParent(_marker, false);
            bar.transform.localScale = proportions * _sizeMetres;
            DestroyImmediate(bar.GetComponent<Collider>());
            bar.GetComponent<MeshRenderer>().sharedMaterial = _material;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;

            _healthy = _simulation != null && _simulation.Catalog != null && _simulation.Views.Count > 0;

            // Once the app is clearly working, the marker gets out of the way — immediately, not
            // after a grace period.
            //
            // It used to linger eight seconds, which sounds harmless and is not: the marker sits
            // at the anchor, the anchor is wherever the interesting thing has been placed, and a
            // 4.6 degree unlit cross is then drawn straight through it. In the whole-system view
            // that is a cyan cross over the Sun. The beacon exists to explain an empty view; the
            // moment the view is not empty, the planets say everything it was there to say, and
            // the status card beside them carries the rest.
            bool showMarker = !_hideWhenHealthy || !_healthy;
            if (_marker.gameObject.activeSelf != showMarker) _marker.gameObject.SetActive(showMarker);

            UpdatePanel();
        }

        private void UpdatePanel()
        {
            bool showLegend = !_healthy || _elapsed < _legendSeconds;
            _panel.Opacity = showLegend ? Mathf.Clamp01(_legendSeconds - _elapsed) : 0f;
            if (!showLegend || _rig?.Camera == null) return;

            Transform camera = _rig.Camera.transform;

            // Beside the hologram, not above or below it.
            //
            // Above was where it used to be, 18 cm up, which for a 55 cm globe is inside the
            // planet — the shipped screenshot has "XR: NOT ACTIVE (flat)" written across Earth.
            // Below is no better: the anchor already sits 25 cm under the eyeline, so clearing a
            // 27 cm radius puts the card 48 degrees down, outside a 60 degree frustum entirely.
            // There is room to the side and nowhere else, and the left is free because the
            // provenance panel opens to the right.
            float clearance = _sizeMetres * 2f;

            // RoomRadius, not RenderedRadius: it is zero for a body that is not drawn (the dot
            // the point path left behind) AND for one the eye is inside, whose transform
            // measures a planet in kilometres. Reading the raw radius here threw this card
            // 6378 km out of the room at true scale.
            if (_simulation != null && _simulation.FocusBody != null)
            {
                clearance = Mathf.Max(clearance, _simulation.FocusBody.RoomRadius);
            }
            clearance += _panel.Size.x * 0.5f + _sizeMetres;

            // Keep it out at the edge even when the focused body is tiny.
            //
            // Clearing the focus alone is the right rule for a globe filling the view and the
            // wrong one for a Solar System on a table, where the focus is a two-millimetre dot and
            // the card slides in to sit on top of the orbits. Half the anchor distance is a little
            // over twenty-five degrees off-axis: out of the way, comfortably inside the frustum.
            float distanceToAnchor = Vector3.Distance(transform.position, camera.position);
            clearance = Mathf.Max(clearance, distanceToAnchor * 0.5f);

            _panel.Root.position = transform.position - camera.right * clearance;

            float distance = _panel.FaceViewer(camera);

            // Caption type on a narrow measure. This card is a reference consulted once, not a
            // panel of values read repeatedly, and it has to fit beside a globe that already
            // occupies 40 degrees of the view.
            _panel.SetContent(_healthy ? "COSMOSYNTH" : "COSMOSYNTH — STARTING", BuildBody(), distance,
                CosmoTheme.TypeCaption, 300f);
        }

        /// <summary>
        /// The legend first, diagnostics second — and the diagnostics only while something is
        /// wrong.
        ///
        /// Nothing about the controls is guessable. An app whose sticks fly and whose face buttons
        /// warp time has to say so somewhere the user is already looking, or they will push the
        /// stick, arrive somewhere they did not intend, and conclude the controls are broken
        /// rather than undocumented. The legend is generated from <see cref="XrInput.Bindings"/>
        /// rather than written out here, because the hand-written version had already drifted from
        /// the code: it advertised the trigger as "boost" when the trigger mostly grabbed the
        /// universe, and never mentioned the wrist dashboard at all.
        /// </summary>
        private string BuildBody()
        {
            var body = new StringBuilder();

            for (int i = 0; i < XrInput.Bindings.Length; i++)
            {
                (string control, string action) = XrInput.Bindings[i];
                if (i > 0) body.AppendLine();
                body.Append($"{control} — {action}");
            }

            // The render path, always — not only when something is already known to be wrong.
            // A frame that is being composited incorrectly still reports itself as healthy by
            // every other measure the app has, so this is the one line that would have named a
            // tiled eye buffer without anyone having to describe pixels over a chat window.
            body.AppendLine();
            body.AppendLine();
            body.Append(RenderDiagnostics.OneLine());
            RenderDiagnostics.LogOnce();

            if (_healthy && _simulation != null) return body.ToString();

            body.AppendLine();

            body.AppendLine();
            body.AppendLine();

            if (_rig != null)
            {
                body.AppendLine(_rig.XrActive
                    ? $"XR {UnityEngine.XR.XRSettings.loadedDeviceName} " +
                      $"{UnityEngine.XR.XRSettings.eyeTextureWidth}x{UnityEngine.XR.XRSettings.eyeTextureHeight}"
                    : "XR not active — flat fallback");
                body.AppendLine($"passthrough {(_rig.PassthroughEnabled ? "on" : "off")}");
            }

            if (_simulation == null || _simulation.Catalog == null)
            {
                body.Append($"catalogue loading… {_elapsed:F0}s");
                return body.ToString();
            }

            body.AppendLine($"{_simulation.Catalog.Count} bodies · {_simulation.Views.Count} views");
            body.AppendLine($"1 km = {_simulation.Origin.UnitsPerKm:G3} units");

            // The number that actually explains a blank view: how big the focused body is on
            // screen right now.
            if (_simulation.FocusBody != null)
            {
                body.AppendLine($"{_simulation.FocusBody.Body.Name} " +
                                $"{_simulation.FocusBody.ApparentDiameterPixels:F0} px, " +
                                $"{_simulation.FocusBody.Representation}");
            }

            if (_input != null) body.Append($"input: {_input.StatusLine}");

            return body.ToString();
        }

        /// <summary>Diagnostics: the status card's size and placement.</summary>
        public string DescribePanel() => _panel != null ? _panel.Describe() : "(none)";

        private void OnDestroy()
        {
            if (_material != null && Application.isPlaying) Destroy(_material);
            _panel?.Destroy();
        }
    }
}
