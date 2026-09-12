using System.Collections.Generic;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The status card: what the app is doing, what it could not load, and the honesty statement —
    /// <b>in the headset</b>, not only in a desktop IMGUI window nobody wearing one can read.
    ///
    /// Frame one shows it 0.8 m ahead with one line per data file, because "passthrough works but
    /// there is nothing in it" must never be a state the user has to diagnose. A healthy run
    /// collapses it to a single line at five seconds and hides it at twenty; an unhealthy one keeps
    /// it, in <c>Warning</c>, until it is dismissed.
    ///
    /// It keeps the name <c>AnchorBeacon</c> because the harness and the bootstrap both reference
    /// it, and renaming a component in a runtime-built scene is how a MonoScript stops binding.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    [AddComponentMenu("CosmoSynth/Anchor Beacon")]
    public sealed class AnchorBeacon : MonoBehaviour
    {
        /// <summary>How far ahead of the eyes the card sits.</summary>
        public const float CardDistanceMetres = 0.8f;

        /// <summary>When a healthy card collapses to one line.</summary>
        public const float CollapseSeconds = 5f;

        /// <summary>When a healthy card disappears.</summary>
        public const float DismissSeconds = 20f;

        private readonly List<string> _lines = new List<string>(12);
        private readonly List<string> _problems = new List<string>(4);

        private XrRig _rig;
        private UniverseSimulation _simulation;
        private ViewOptions _options;
        private XrHandInput _hands;

        private CosmoSurface _surface;
        private float _age;
        private bool _dismissed;

        public void Bind(XrRig rig, UniverseSimulation simulation, ViewOptions options, XrHandInput hands)
        {
            _rig = rig;
            _simulation = simulation;
            _options = options;
            _hands = hands;
        }

        /// <summary>A progress or state line: "solar_system loaded (31 bodies)".</summary>
        public void SetLine(string key, string text)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                if (!_lines[i].StartsWith(key + ": ", System.StringComparison.Ordinal)) continue;
                _lines[i] = key + ": " + text;
                return;
            }
            _lines.Add(key + ": " + text);
        }

        /// <summary>
        /// A problem. It is drawn in <c>Warning</c> and the card stays until dismissed — nothing
        /// fails silently, and a missing file is a line the user can read rather than an empty room.
        /// </summary>
        public void SetProblem(string text)
        {
            if (string.IsNullOrEmpty(text) || _problems.Contains(text)) return;
            _problems.Add(text);
            _dismissed = false;
        }

        public void Dismiss() => _dismissed = true;

        public bool Healthy => _problems.Count == 0;

        private void Awake()
        {
            if (_rig == null) _rig = FindAnyObjectByType<XrRig>();
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();
            if (_hands == null) _hands = FindAnyObjectByType<XrHandInput>();
        }

        private void LateUpdate()
        {
            if (_surface == null) _surface = new CosmoSurface(transform, "Status Card");
            if (_rig == null || _rig.Camera == null) return;

            _age += Time.unscaledDeltaTime;

            bool show = !_dismissed && (!Healthy || _age < DismissSeconds) &&
                        (_options == null || !_options.CleanView);
            if (!show) { _surface.Opacity = 0f; return; }

            Transform camera = _rig.Camera.transform;
            Vector3 forward = camera.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            forward.Normalize();

            _surface.Root.position = camera.position + forward * CardDistanceMetres + Vector3.up * -0.05f;
            _surface.Root.rotation = Quaternion.LookRotation(forward, Vector3.up);
            _surface.Opacity = 1f;

            Layout(Vector3.Distance(_surface.Root.position, camera.position));
        }

        private void Layout(float distance)
        {
            bool collapsed = Healthy && _age > CollapseSeconds;

            _surface.Begin(distance);

            float pad = _surface.Dmm(CosmoTheme.SpacePanelPadding);
            float titleHeight = _surface.Dmm(40f);
            float lineHeight = _surface.Dmm(30f);

            var body = new List<string>(16);
            var colours = new List<Color>(16);

            if (!collapsed)
            {
                for (int i = 0; i < _lines.Count; i++) { body.Add(_lines[i]); colours.Add(CosmoTheme.TextSecondary); }
                for (int i = 0; i < _problems.Count; i++) { body.Add("! " + _problems[i]); colours.Add(CosmoTheme.Warning); }

                if (_options != null)
                {
                    body.Add(HonestyState.Statement(_options));
                    colours.Add(CosmoTheme.Warning);
                }

                // The first-run hint, generated from the bindings for whatever input is live.
                string hint = Hint();
                if (hint != null && _age < DismissSeconds) { body.Add(hint); colours.Add(CosmoTheme.TextMuted); }
            }
            else
            {
                body.Add(Summary());
                colours.Add(CosmoTheme.TextSecondary);
            }

            string title = Healthy ? "CosmoSynth 2.0" : "CosmoSynth 2.0 · needs attention";
            float width = _surface.MeasureWidth(title, CosmoTheme.TypeTitle);
            for (int i = 0; i < body.Count; i++)
                width = Mathf.Max(width, _surface.MeasureWidth(body[i], CosmoTheme.TypeCaption));

            float height = 2f * pad + titleHeight + body.Count * lineHeight;
            float left = -0.5f * width;
            float top = 0.5f * height - pad;

            _surface.Text(title, left, top, CosmoTheme.TypeTitle,
                Healthy ? CosmoTheme.TextPrimary : CosmoTheme.Warning);
            top -= titleHeight;

            for (int i = 0; i < body.Count; i++)
            {
                _surface.Text(body[i], left, top, CosmoTheme.TypeCaption, colours[i]);
                top -= lineHeight;
            }

            _surface.End(width + 2f * pad, height);
        }

        private string Summary()
        {
            if (_simulation?.Catalog == null) return "loading the universe";
            return "all data loaded · " + _simulation.Catalog.Count + " bodies";
        }

        private string Hint()
        {
            if (_age > DismissSeconds) return null;
            bool hands = _hands != null && _hands.Active == XrHandInput.Modality.Hands;
            BindDevice device = hands ? BindDevice.Hand
                : _rig != null && _rig.XrActive ? BindDevice.Controller : BindDevice.Keyboard;

            return device switch
            {
                BindDevice.Hand => "Pinch with both hands and pull apart to zoom out · tap your wrist for the map",
                BindDevice.Controller => "Right stick to zoom · X for the map",
                _ => "Wheel to zoom · Tab for the map"
            };
        }

        /// <summary>Diagnostics; the play-mode report prints this line.</summary>
        public string DescribePanel() =>
            _surface == null
                ? "(none)"
                : $"{(Healthy ? "healthy" : _problems.Count + " problems")} age={_age:F1}s {_surface.Describe()}";

        private void OnDestroy() => _surface?.Destroy();
    }
}
