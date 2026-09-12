using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The watch face and the panel it opens: the successor to <c>WristDashboard</c>.
    ///
    /// Three things the dashboard could not do, and this is built around all three.
    ///
    /// <b>It works with hands.</b> The dashboard was controller-only — it read the left stick and
    /// the X button — so on a Quest 3 with the controllers in the drawer there was no interface at
    /// all. The fingertip model here is the one tuned in the owner's own WebXR Milky Way Atlas and
    /// measured out of it: press when the tip crosses 6 mm, release only past 20 mm, a 6 mm
    /// position deadband on the anchor and a 20 degree deadband on its yaw
    /// (<c>refs/webxr/nebula-gallery/index.html:1370,1413-1419</c>).
    ///
    /// <b>It is generated.</b> Every row comes from <see cref="PanelModel"/>, which reads
    /// <c>LayerTable</c>, <c>WarpLadder</c>, the destinations and <c>XrInput.Bindings</c>. Nothing
    /// is listed twice.
    ///
    /// <b>It is sized from its contents.</b> The dashboard declared a backing height and a
    /// character size independently and showed three and a half of its twelve lines.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    [AddComponentMenu("CosmoSynth/Wrist Panel")]
    public sealed class WristPanel : MonoBehaviour
    {
        // Tuned constants, all from CONTRACT 5.6.2 and 5.6.8.
        private const float WatchRadius = 0.0095f;          // a 19 mm disc
        private const float WatchRingInner = 0.0105f;
        private const float WatchRingOuter = 0.0135f;
        private const float WatchTapMetres = 0.05f;
        private const float WatchDebounceSeconds = 0.8f;
        private const float PositionDeadbandMetres = 0.006f;
        private const float YawDeadbandRad = 0.35f;         // 20 degrees
        private const float PositionDamping = 7f;
        private const float YawDamping = 4f;
        private const float RayReachMetres = 1.2f;
        private const float TouchPressMetres = 0.006f;
        private const float TouchReleaseMetres = 0.020f;
        private const float HoverPadMetres = 0.030f;
        private const float HoverDepthMetres = 0.120f;
        private const float ButtonDebounceSeconds = 0.3f;
        private const float PressFlashSeconds = 0.2f;
        private const float DragScrollMetres = 0.008f;

        private XrRig _rig;
        private XrInput _input;
        private XrHandInput _hands;
        private ViewOptions _options;
        private PanelModel _model;
        private TravelController _travel;
        private BodySelector _selector;
        private MeasureTool _measure;
        private DestinationIndex _destinations;

        private CosmoSurface _surface;
        private CosmoSurface _watch;

        private readonly List<string> _actions = new List<string>(64);
        private readonly List<string> _sliders = new List<string>(8);

        private bool _open;
        private bool _pinned;
        private Vector3 _pinPosition;
        private Quaternion _pinRotation;

        private Vector3 _smoothPosition;
        private float _smoothYaw;
        private bool _poseSeeded;

        private float _watchTapAge = 99f;
        private float _pressFlashAge = 99f;
        private float _debounceAge = 99f;
        private int _hoverId = -1;
        private int _pressedId = -1;
        private bool _fingerDown;
        private bool _triggerWasDown;
        private float _dragStartY;
        private bool _dragging;

        /// <summary>True while a hand is over the panel, so its pinch does not also grab the stage.</summary>
        public bool PanelHot { get; private set; }

        public bool IsOpen => _open;

        public bool IsPinned => _pinned;

        public PanelModel Model => _model;

        public void Bind(XrRig rig, XrInput input, XrHandInput hands, ViewOptions options, PanelModel model,
            TravelController travel, BodySelector selector, MeasureTool measure, DestinationIndex destinations)
        {
            _rig = rig;
            _input = input;
            _hands = hands;
            _options = options;
            _model = model;
            _travel = travel;
            _selector = selector;
            _measure = measure;
            _destinations = destinations;
        }

        public void Toggle()
        {
            _open = !_open;
            // Opening while pinned is the recall: the panel comes back to the wrist.
            if (_open && _pinned) _pinned = false;
            _input?.Pulse(WatchHand, 0.8f, 0.05f);
        }

        public void Pin()
        {
            if (!_open || _surface == null) return;
            _pinned = !_pinned;
            if (!_pinned) return;
            _pinPosition = _surface.Root.position;
            _pinRotation = _surface.Root.rotation;
        }

        private Hand WatchHand => _options != null && _options.Handedness == Handedness.Left ? Hand.Right : Hand.Left;

        private Hand PointerHand => _options != null && _options.Handedness == Handedness.Left ? Hand.Left : Hand.Right;

        private void Awake()
        {
            if (_rig == null) _rig = FindAnyObjectByType<XrRig>();
            if (_input == null) _input = FindAnyObjectByType<XrInput>();
            if (_hands == null) _hands = FindAnyObjectByType<XrHandInput>();
        }

        private void LateUpdate()
        {
            if (_model == null || _rig == null || _rig.Camera == null) return;

            EnsureSurfaces();

            float deltaTime = Time.unscaledDeltaTime;
            _watchTapAge += deltaTime;
            _pressFlashAge += deltaTime;
            _debounceAge += deltaTime;

            Transform camera = _rig.Camera.transform;

            PlaceWatch(camera);
            if (_options != null && _options.CleanView && !_open)
            {
                _surface.Opacity = 0f;
                return;
            }

            if (!_open)
            {
                _surface.Opacity = 0f;
                PollOpenGesture(camera);
                return;
            }

            PlacePanel(camera, deltaTime);
            _surface.Opacity = 1f;
            Layout();
            PollInput(camera);
            PollOpenGesture(camera);
        }

        // ---- placement -------------------------------------------------------

        private void PlaceWatch(Transform camera)
        {
            if (!TryWristPose(out Vector3 wrist, out Quaternion rotation))
            {
                _watch.Opacity = 0f;
                return;
            }

            _watch.Root.position = wrist;
            _watch.Root.LookAt(camera.position);

            // 40 % until the back of the wrist is turned towards the eyes, then full: a watch you
            // are not looking at should not compete with the sky.
            float facing = Vector3.Angle(_watch.Root.forward, (camera.position - wrist).normalized);
            _watch.Opacity = facing < 55f ? 1f : 0.4f;
            LayoutWatch();
        }

        private void PlacePanel(Transform camera, float deltaTime)
        {
            if (_pinned)
            {
                _surface.Root.SetPositionAndRotation(_pinPosition, _pinRotation);
                return;
            }

            Vector3 target;
            float height = Mathf.Max(0.05f, _surface.Size.y);

            if (TryWristPose(out Vector3 wrist, out _))
            {
                Vector3 horizontal = wrist - camera.position;
                horizontal.y = 0f;
                if (horizontal.sqrMagnitude < 1e-6f) horizontal = camera.forward;
                horizontal.Normalize();

                // Bottom edge 10 cm above the wrist and 10 cm beyond it, so the panel does not sit
                // on the hand that is reaching for it.
                target = new Vector3(wrist.x + horizontal.x * 0.10f,
                    wrist.y + 0.10f + height * 0.5f,
                    wrist.z + horizontal.z * 0.10f);
            }
            else
            {
                // Hands-free fallback: no wrist, tracking lost, or first run.
                Vector3 forward = camera.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
                forward.Normalize();
                Vector3 left = new Vector3(-forward.z, 0f, forward.x);
                target = camera.position + forward * 0.55f + left * 0.26f + Vector3.up * -0.15f;
            }

            if (!_poseSeeded)
            {
                _smoothPosition = target;
                _smoothYaw = Mathf.Atan2(camera.position.x - target.x, camera.position.z - target.z);
                _poseSeeded = true;
            }

            // Deadband then damping, on both axes. Without the deadband the panel hunts around the
            // hand's own tremor and is unreadable; without the damping it snaps.
            if (Vector3.Distance(target, _smoothPosition) > PositionDeadbandMetres)
                _smoothPosition = Vector3.Lerp(_smoothPosition, target, 1f - Mathf.Exp(-deltaTime * PositionDamping));

            float wantYaw = Mathf.Atan2(camera.position.x - _smoothPosition.x, camera.position.z - _smoothPosition.z);
            float delta = Mathf.DeltaAngle(_smoothYaw * Mathf.Rad2Deg, wantYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            if (Mathf.Abs(delta) > YawDeadbandRad)
                _smoothYaw += delta * (1f - Mathf.Exp(-deltaTime * YawDamping));

            // Upright always: no roll, no pitch. A panel that tips with the wrist is unreadable.
            _surface.Root.SetPositionAndRotation(_smoothPosition,
                Quaternion.Euler(0f, _smoothYaw * Mathf.Rad2Deg, 0f));
        }

        private bool TryWristPose(out Vector3 position, out Quaternion rotation)
        {
            if (_hands != null && _hands.TryGetWristWorld(WatchHand, out position, out rotation)) return true;
            if (_input != null && _input.TryGetPose(WatchHand, out Vector3 local, out rotation))
            {
                position = ToWorld(local);
                return true;
            }
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        private Vector3 ToWorld(Vector3 trackingSpace)
        {
            Transform origin = _rig != null && _rig.Origin != null
                ? _rig.Origin.CameraFloorOffsetObject.transform
                : null;
            return origin != null ? origin.TransformPoint(trackingSpace) : trackingSpace;
        }

        // ---- layout ----------------------------------------------------------

        private void EnsureSurfaces()
        {
            if (_surface == null) _surface = new CosmoSurface(transform, "Wrist Panel");
            if (_watch == null) _watch = new CosmoSurface(transform, "Watch Face");
        }

        private void LayoutWatch()
        {
            // Chrome only: no glyphs, so the watch costs one draw call and the panel's two leave
            // room inside the six §7.3 allows for the whole interface.
            _watch.Begin(0.42f);
            _watch.Rect(0f, 0f, WatchRingOuter * 2f, WatchRingOuter * 2f, CosmoTheme.Stroke);
            _watch.Rect(0f, 0f, WatchRingInner * 2f, WatchRingInner * 2f, CosmoTheme.Surface);
            _watch.Rect(0f, 0f, WatchRadius * 2f, WatchRadius * 2f,
                _open ? CosmoTheme.Accent : CosmoTheme.SurfaceRaised, SurfaceStyle.Fill, hitId: WatchHitId);

            // The ladder arc: where the current scale sits across the 27.7 decades, as a tick.
            float fraction = _model != null && ScaleFraction(out float f) ? f : 0.5f;
            float x = Mathf.Lerp(-WatchRingOuter, WatchRingOuter, fraction);
            _watch.Rect(0f, -WatchRingOuter - 0.004f, WatchRingOuter * 2f, 0.0015f, CosmoTheme.TextMuted);
            _watch.Rect(x, -WatchRingOuter - 0.004f, 0.0022f, 0.005f, CosmoTheme.Accent);

            _watch.End(WatchRingOuter * 2.2f, WatchRingOuter * 2.6f);
        }

        private const int WatchHitId = 10000;

        private ScaleControl _scale;

        private bool ScaleFraction(out float fraction)
        {
            if (_scale == null) _scale = FindAnyObjectByType<ScaleControl>();
            fraction = _scale != null ? _scale.LadderFraction() : 0.5f;
            return _scale != null;
        }

        /// <summary>
        /// Build the panel from the model's rows.
        ///
        /// Everything is in dmm and converted through the viewing distance, so the same code gives
        /// a panel that reads the same worn at 42 cm and pinned at 3 m.
        /// </summary>
        private void Layout()
        {
            float distance = _pinned && _rig != null && _rig.Camera != null
                ? Vector3.Distance(_surface.Root.position, _rig.Camera.transform.position)
                : CosmoTheme.WristDistanceMetres;

            _surface.Begin(distance);
            _actions.Clear();
            _sliders.Clear();

            float pad = _surface.Dmm(CosmoTheme.SpacePanelPadding);
            float gap = _surface.Dmm(CosmoTheme.ButtonGap);
            float rowHeight = _surface.Dmm(CosmoTheme.ButtonHeight);
            float lineHeight = _surface.Dmm(30f);

            float contentWidth = ContentWidth();
            float width = contentWidth + 2f * pad;

            float headerHeight = 2f * lineHeight + gap;
            float tabHeight = rowHeight + gap;
            float contentHeight = PanelModel.VisibleRows * (rowHeight + gap);
            float footerHeight = rowHeight;
            float height = 2f * pad + headerHeight + tabHeight + contentHeight + gap + footerHeight;

            float left = -0.5f * contentWidth;
            float top = 0.5f * height - pad;

            top = DrawHeader(left, top, contentWidth, lineHeight, gap);
            top = DrawTabs(left, top, contentWidth, rowHeight, gap);
            top = DrawContent(left, top, contentWidth, rowHeight, gap);
            DrawFooter(left, -0.5f * height + pad + footerHeight, contentWidth, rowHeight);

            _surface.End(width, height);
        }

        private float ContentWidth()
        {
            // The widest row sets the width, floored so four minimum-width buttons always fit.
            float floor = _surface.Dmm(4f * CosmoTheme.ButtonMinWidth + 3f * CosmoTheme.ButtonGap);
            float widest = Mathf.Max(floor,
                _surface.MeasureWidth(_model.HeaderClock, CosmoTheme.TypeCaption) + _surface.Dmm(200f));
            widest = Mathf.Max(widest, _surface.MeasureWidth(_model.HeaderScale, CosmoTheme.TypeCaption));

            IReadOnlyList<PanelRow> rows = _model.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Span < 4) continue;
                string text = rows[i].Text + (rows[i].Suffix != null ? "  " + rows[i].Suffix : string.Empty);
                widest = Mathf.Max(widest, _surface.MeasureWidth(text, CosmoTheme.TypeBody) + _surface.Dmm(40f));
            }
            return widest;
        }

        private float DrawHeader(float left, float top, float width, float lineHeight, float gap)
        {
            _surface.Text(_model.HeaderMode + "  " + _model.HeaderClock, left, top,
                CosmoTheme.TypeCaption, CosmoTheme.TextSecondary);

            float pinWidth = _surface.Dmm(110f);
            _surface.Rect(left + width - 0.5f * pinWidth, top - 0.5f * lineHeight, pinWidth, lineHeight * 0.9f,
                _pinned ? CosmoTheme.Accent : CosmoTheme.SurfaceRaised, SurfaceStyle.Fill, Action("pin"));
            _surface.TextCentred("PIN", left + width - 0.5f * pinWidth, top - lineHeight * 0.12f,
                CosmoTheme.TypeCaption, _pinned ? CosmoTheme.Surface : CosmoTheme.TextPrimary);

            top -= lineHeight;
            _surface.Text(_model.HeaderScale, left, top, CosmoTheme.TypeCaption, CosmoTheme.TextPrimary);

            // Chips, right to left, so the first (an honesty chip) is furthest from the clutter.
            List<HonestyChip> chips = HonestyState.Shown(_model.Chips());
            float x = left + width;
            for (int i = 0; i < chips.Count; i++)
            {
                float chipWidth = _surface.MeasureWidth(chips[i].Text, CosmoTheme.TypeCaption) + _surface.Dmm(24f);
                x -= chipWidth + _surface.Dmm(8f);
                _surface.Rect(x + 0.5f * chipWidth, top - 0.45f * lineHeight, chipWidth, lineHeight * 0.85f,
                    CosmoTheme.WithAlpha(chips[i].Colour, 0.22f), SurfaceStyle.Fill,
                    chips[i].Id == "more" ? Action("info.view") : -1);
                _surface.TextCentred(chips[i].Text, x + 0.5f * chipWidth, top - lineHeight * 0.08f,
                    CosmoTheme.TypeCaption, chips[i].Colour);
            }

            return top - lineHeight - gap;
        }

        private static readonly string[] TabNames = { "GO", "LAYERS", "TIME", "TOOLS", "INFO" };

        private float DrawTabs(float left, float top, float width, float rowHeight, float gap)
        {
            float cell = (width - 4f * gap) / 5f;
            for (int i = 0; i < TabNames.Length; i++)
            {
                bool on = (int)_model.ActiveTab == i;
                float cx = left + i * (cell + gap) + 0.5f * cell;
                _surface.Rect(cx, top - 0.5f * rowHeight, cell, rowHeight,
                    on ? CosmoTheme.WithAlpha(CosmoTheme.Accent, 0.28f) : CosmoTheme.SurfaceRaised,
                    SurfaceStyle.Fill, Action("tab." + i));
                _surface.TextCentred(TabNames[i], cx, top - rowHeight * 0.28f, CosmoTheme.TypeBody,
                    on ? CosmoTheme.Accent : CosmoTheme.TextSecondary);
            }
            return top - rowHeight - gap;
        }

        private float DrawContent(float left, float top, float width, float rowHeight, float gap)
        {
            IReadOnlyList<PanelRow> rows = _model.Rows;
            float startTop = top;

            int line = 0;
            float x = left;
            int spanUsed = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                PanelRow row = rows[i];
                if (row.Span >= 4 || spanUsed + row.Span > 4)
                {
                    if (spanUsed > 0) { line++; x = left; spanUsed = 0; }
                }

                int visibleLine = line - _model.ScrollRow;
                float cellWidth = (width - 3f * gap) * row.Span / 4f + (row.Span - 1) * gap;

                if (visibleLine >= 0 && visibleLine < PanelModel.VisibleRows)
                {
                    float rowTop = startTop - visibleLine * (rowHeight + gap);
                    DrawRow(row, x, rowTop, cellWidth, rowHeight, i);
                }

                if (row.Span >= 4) { line++; x = left; spanUsed = 0; }
                else { x += cellWidth + gap; spanUsed += row.Span; }
            }

            return startTop - PanelModel.VisibleRows * (rowHeight + gap);
        }

        private void DrawRow(in PanelRow row, float x, float top, float cellWidth, float rowHeight, int index)
        {
            float cx = x + 0.5f * cellWidth;
            float cy = top - 0.5f * rowHeight;
            int id = row.Pressable ? Action(row.Id, row.Kind == PanelRowKind.Slider) : -1;
            bool hovered = id >= 0 && id == _hoverId;
            bool flashing = id >= 0 && id == _pressedId && _pressFlashAge < PressFlashSeconds;

            switch (row.Kind)
            {
                case PanelRowKind.Caption:
                    _surface.Text(row.Text, x, top - rowHeight * 0.25f, CosmoTheme.TypeCaption, CosmoTheme.TextMuted);
                    return;

                case PanelRowKind.Text:
                {
                    Color colour = row.ShowGlyph ? CosmoTheme.Provenance(row.Provenance) : CosmoTheme.TextSecondary;
                    string glyph = row.ShowGlyph ? CosmoTheme.ProvenanceGlyph(row.Provenance) + " " : string.Empty;
                    _surface.Text(glyph + row.Text, x, top - rowHeight * 0.22f, CosmoTheme.TypeCaption, colour);
                    return;
                }

                case PanelRowKind.Slider:
                {
                    _surface.Rect(cx, cy, cellWidth, rowHeight * 0.5f, CosmoTheme.SurfaceRaised, SurfaceStyle.Fill, id);
                    float fillWidth = Mathf.Max(0.001f, cellWidth * row.Value);
                    _surface.Rect(x + 0.5f * fillWidth, cy, fillWidth, rowHeight * 0.5f,
                        CosmoTheme.WithAlpha(CosmoTheme.Accent, 0.55f));
                    _surface.Text(row.Text, x + _surface.Dmm(14f), top - rowHeight * 0.2f,
                        CosmoTheme.TypeCaption, CosmoTheme.TextPrimary);
                    return;
                }
            }

            Color fill = !row.Enabled
                ? CosmoTheme.WithAlpha(CosmoTheme.Surface, 0.5f)
                : flashing
                    ? CosmoTheme.WithAlpha(CosmoTheme.Accent, 0.6f)
                    : row.On
                        ? CosmoTheme.WithAlpha(CosmoTheme.Accent, 0.28f)
                        : hovered ? CosmoTheme.SurfaceRaised : CosmoTheme.Surface;

            Color stroke = row.On || hovered ? CosmoTheme.StrokeActive : CosmoTheme.Stroke;
            _surface.StrokedRect(cx, cy, cellWidth, rowHeight, fill, stroke, SurfaceStyle.Fill, id);

            Color text = row.Enabled ? CosmoTheme.TextPrimary : CosmoTheme.TextMuted;
            string prefix = row.ShowGlyph ? CosmoTheme.ProvenanceGlyph(row.Provenance) + " " : string.Empty;

            if (row.Span >= 4)
            {
                _surface.Text(prefix + row.Text, x + _surface.Dmm(16f), top - rowHeight * 0.22f,
                    CosmoTheme.TypeBody, text);
                if (!string.IsNullOrEmpty(row.Suffix))
                {
                    float suffixWidth = _surface.MeasureWidth(row.Suffix, CosmoTheme.TypeCaption);
                    _surface.Text(row.Suffix, x + cellWidth - suffixWidth - _surface.Dmm(16f),
                        top - rowHeight * 0.26f, CosmoTheme.TypeCaption, CosmoTheme.TextMuted);
                }
            }
            else
            {
                _surface.TextCentred(row.Text, cx, top - rowHeight * 0.26f, CosmoTheme.TypeBody, text);
            }
        }

        private void DrawFooter(float left, float bottomTop, float width, float rowHeight)
        {
            bool hasSelection = _selector != null && _selector.Selected != null;
            string name = _model.FooterName;
            if (!hasSelection)
            {
                name = _hands != null && _hands.Active == XrHandInput.Modality.Hands
                    ? "Nothing selected: point and tap-pinch"
                    : "Nothing selected: point and pull the trigger";
            }

            _surface.Text(name, left, bottomTop, CosmoTheme.TypeBody,
                hasSelection ? CosmoTheme.TextPrimary : CosmoTheme.TextMuted);

            string[] ids = { "footer.go", "footer.follow", "footer.inspect", "footer.measure" };
            string[] labels =
            {
                "GO",
                _travel != null && _travel.FollowingId != null ? "FREE" : "FOLLOW",
                "INSPECT",
                "MEASURE"
            };

            float gap = _surface.Dmm(CosmoTheme.ButtonGap);
            float cell = (width - 3f * gap) / 4f;
            for (int i = 0; i < ids.Length; i++)
            {
                float cx = left + i * (cell + gap) + 0.5f * cell;
                int id = hasSelection ? Action(ids[i]) : -1;
                _surface.StrokedRect(cx, bottomTop - rowHeight * 0.8f, cell, rowHeight * 0.8f,
                    hasSelection ? CosmoTheme.Surface : CosmoTheme.WithAlpha(CosmoTheme.Surface, 0.5f),
                    hasSelection && id == _hoverId ? CosmoTheme.StrokeActive : CosmoTheme.Stroke,
                    SurfaceStyle.Fill, id);
                _surface.TextCentred(labels[i], cx, bottomTop - rowHeight * 0.34f, CosmoTheme.TypeCaption,
                    hasSelection ? CosmoTheme.TextPrimary : CosmoTheme.TextMuted);
            }
        }

        private int Action(string id, bool slider = false)
        {
            _actions.Add(id);
            if (slider) _sliders.Add(id);
            return _actions.Count - 1;
        }

        // ---- input -----------------------------------------------------------

        private void PollOpenGesture(Transform camera)
        {
            // The fingertip within 5 cm of the watch, debounced, is open/close/recall. The same
            // gesture the Atlas uses, and the only one that needs no controller at all.
            if (_hands != null && _hands.TryGetIndexTipWorld(PointerHand, out Vector3 tip) &&
                _watch != null && _watch.Opacity > 0.01f &&
                Vector3.Distance(tip, _watch.Root.position) < WatchTapMetres &&
                _watchTapAge > WatchDebounceSeconds)
            {
                _watchTapAge = 0f;
                Toggle();
            }

            if (_input != null && _input.PanelTogglePressed) Toggle();
        }

        private void PollInput(Transform camera)
        {
            PanelHot = false;
            int hit = -1;
            float sliderT = 0f;
            bool haveTouch = false;
            float touchDepth = 0f;
            Vector3 localPoint = Vector3.zero;

            // Fingertip first: if the hand is on the panel, that is what the user means.
            if (_hands != null && _hands.TryGetIndexTipWorld(PointerHand, out Vector3 tip))
            {
                Vector3 local = _surface.Root.InverseTransformPoint(tip);
                if (Mathf.Abs(local.x) < 0.5f * _surface.Size.x + HoverPadMetres &&
                    Mathf.Abs(local.y) < 0.5f * _surface.Size.y + HoverPadMetres &&
                    local.z > -HoverPadMetres && local.z < HoverDepthMetres)
                {
                    haveTouch = true;
                    touchDepth = local.z;
                    localPoint = local;
                    hit = _surface.HitId(new Vector2(local.x, local.y));
                    PanelHot = true;
                }
            }

            if (!haveTouch && _input != null && _input.TryGetPose(PointerHand, out Vector3 origin, out Quaternion rotation))
            {
                Vector3 worldOrigin = ToWorld(origin);
                Vector3 direction = ToWorldDirection(rotation * Vector3.forward);
                if (TryRayHit(worldOrigin, direction, out Vector3 local, out float distance) &&
                    distance < RayReachMetres)
                {
                    localPoint = local;
                    hit = _surface.HitId(new Vector2(local.x, local.y));
                    PanelHot = hit >= 0;
                }
            }

            // The claim: while this hand is on the panel its pinch, trigger and grip do not
            // select or grab. The other hand is unaffected, so one hand can drive the panel while
            // the other still holds the universe.
            _input?.SetPanelHot(PointerHand, PanelHot);

            if (hit != _hoverId)
            {
                _hoverId = hit;
                if (hit >= 0) _input?.Pulse(PointerHand, 0.22f, 0.01f);
            }

            if (hit < 0) { _fingerDown = false; _dragging = false; _triggerWasDown = false; return; }

            if (haveTouch)
            {
                // Press on crossing 6 mm, but only from beyond 20 mm: without that, a fingertip
                // entering from behind the panel fires the moment it appears.
                if (!_fingerDown && touchDepth <= TouchPressMetres)
                {
                    _fingerDown = true;
                    _dragStartY = localPoint.y;
                    _dragging = false;
                    Press(hit, localPoint);
                }
                else if (_fingerDown && touchDepth <= TouchReleaseMetres)
                {
                    if (Mathf.Abs(localPoint.y - _dragStartY) > DragScrollMetres) _dragging = true;
                    if (_dragging) DragScroll(localPoint.y);
                    else if (IsSlider(hit)) DragSlider(hit, localPoint);
                }
                else if (_fingerDown && touchDepth > TouchReleaseMetres)
                {
                    _fingerDown = false;
                    _dragging = false;
                }
                return;
            }

            // Its own edge, not XrInput.SelectPressed: that flag is suppressed by the very
            // claim this panel just made.
            bool triggerDown = _input != null && _input.Trigger(PointerHand) > 0.6f;
            if (triggerDown && !_triggerWasDown) Press(hit, localPoint);
            if (triggerDown && IsSlider(hit)) DragSlider(hit, localPoint);
            _triggerWasDown = triggerDown;

            // The dominant stick scrolls while the ray is on the panel, which is why zoom stands
            // down there: two meanings on one stick is exactly the collision XrInput exists to stop.
            if (_input != null)
            {
                float stickY = _input.Stick(PointerHand).y;
                if (Mathf.Abs(stickY) > 0.7f && _debounceAge > 0.18f)
                {
                    _debounceAge = 0f;
                    _model.Scroll(stickY > 0f ? -1 : 1);
                }
            }
        }

        private bool IsSlider(int hit) => hit >= 0 && hit < _actions.Count && _sliders.Contains(_actions[hit]);

        private void Press(int hit, Vector3 local)
        {
            if (hit < 0 || _debounceAge < ButtonDebounceSeconds) return;
            _debounceAge = 0f;
            _pressedId = hit;
            _pressFlashAge = 0f;
            _input?.Pulse(PointerHand, 0.7f, 0.04f);

            if (hit == WatchHitId) { Toggle(); return; }
            if (hit >= _actions.Count) return;

            string id = _actions[hit];
            if (id == "pin") { Pin(); return; }
            if (id.StartsWith("tab.", System.StringComparison.Ordinal) &&
                int.TryParse(id.Substring(4), out int tab))
            {
                _model.SetTab((PanelModel.Tab)tab);
                return;
            }
            if (id.StartsWith("footer.", System.StringComparison.Ordinal)) { Footer(id); return; }
            if (_sliders.Contains(id)) { DragSlider(hit, local); return; }

            _model.Activate(id);
        }

        private void Footer(string id)
        {
            CelestialBodyView selected = _selector != null ? _selector.Selected : null;
            if (selected == null) return;

            switch (id)
            {
                case "footer.go":
                {
                    Destination destination = _destinations?.Find("body:" + selected.Body.Id);
                    if (destination != null) _travel?.Travel(destination);
                    break;
                }
                case "footer.follow": _travel?.ToggleFollow(selected.Body.Id); break;
                case "footer.inspect": _model.SetTab(PanelModel.Tab.Info); _model.Activate("info.inspector"); break;
                case "footer.measure": _measure?.SetActive(true); _measure?.PickBody(selected); break;
            }
        }

        private void DragSlider(int hit, Vector3 local)
        {
            if (hit < 0 || hit >= _actions.Count) return;
            float half = 0.5f * _surface.Size.x - _surface.Dmm(CosmoTheme.SpacePanelPadding);
            float t = Mathf.InverseLerp(-half, half, local.x);
            _model.SetSlider(_actions[hit], t);
        }

        private void DragScroll(float y)
        {
            float rowHeight = _surface.Dmm(CosmoTheme.ButtonHeight + CosmoTheme.ButtonGap);
            int lines = Mathf.RoundToInt((y - _dragStartY) / Mathf.Max(1e-4f, rowHeight));
            if (lines == 0) return;
            _model.Scroll(lines);
            _dragStartY = y;
        }

        private bool TryRayHit(Vector3 origin, Vector3 direction, out Vector3 local, out float distance)
        {
            local = Vector3.zero;
            distance = 0f;

            Vector3 localOrigin = _surface.Root.InverseTransformPoint(origin);
            Vector3 localDirection = _surface.Root.InverseTransformDirection(direction);
            if (Mathf.Abs(localDirection.z) < 1e-5f) return false;

            float t = -localOrigin.z / localDirection.z;
            if (t <= 0f) return false;

            local = localOrigin + localDirection * t;
            distance = t;
            return Mathf.Abs(local.x) <= 0.5f * _surface.Size.x &&
                   Mathf.Abs(local.y) <= 0.5f * _surface.Size.y;
        }

        private Vector3 ToWorldDirection(Vector3 trackingSpace)
        {
            Transform origin = _rig != null && _rig.Origin != null
                ? _rig.Origin.CameraFloorOffsetObject.transform
                : null;
            return origin != null ? origin.TransformDirection(trackingSpace) : trackingSpace;
        }

        /// <summary>Diagnostics for the play-mode report.</summary>
        public string Describe() =>
            _surface == null
                ? "(not built)"
                : $"open={_open} pinned={_pinned} tab={_model?.ActiveTab} rows={_model?.Rows.Count} " +
                  $"hot={PanelHot} hover={_hoverId} {_surface.Describe()}";

        private void OnDestroy()
        {
            _surface?.Destroy();
            _watch?.Destroy();
        }
    }
}
