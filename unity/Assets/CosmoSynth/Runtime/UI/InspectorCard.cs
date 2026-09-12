using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The card beside whatever is selected: its name, the three numbers that are true right now,
    /// and the three things you can do with it.
    ///
    /// It replaces <c>SpatialPanel</c>, which was sized four times too small for its own text and
    /// showed a fixed block of catalogue facts with no "now" in it at all — no distance from the
    /// viewer, no light time, no phase. Those are the numbers a person actually asks for while
    /// looking at a planet, and they are the ones that change while you watch.
    ///
    /// <b>It re-checks that the body is still drawn every frame.</b> A selection outlives the body:
    /// <c>RenderedRadius</c> survives a body being switched off and stops updating, so the stale
    /// value it keeps can be enormous. That is how the selection ring once wrapped itself round a
    /// spacecraft at the size of the sky.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    [AddComponentMenu("CosmoSynth/Inspector Card")]
    public sealed class InspectorCard : MonoBehaviour
    {
        /// <summary>How far to the object's right the card sits, in radians.</summary>
        public const float OffsetRadians = 0.12f;

        private UniverseSimulation _simulation;
        private ViewOptions _options;
        private TravelController _travel;
        private DestinationIndex _destinations;
        private Camera _camera;

        private CosmoSurface _surface;
        private CelestialBodyView _view;
        private InspectorModel _model;
        private double _nextRefresh;

        public void Bind(UniverseSimulation simulation, ViewOptions options, TravelController travel,
            DestinationIndex destinations, Camera camera)
        {
            _simulation = simulation;
            _options = options;
            _travel = travel;
            _destinations = destinations;
            _camera = camera;
        }

        /// <summary>Show the card for a body, or hide it with null.</summary>
        public void Show(CelestialBodyView view)
        {
            _view = view;
            _model = null;
            _nextRefresh = 0.0;
        }

        private void LateUpdate()
        {
            if (_surface == null) _surface = new CosmoSurface(transform, "Inspector Card");

            bool show = _view != null && _view.IsGeometryDrawn && _camera != null &&
                        (_options == null || !_options.CleanView);
            if (!show)
            {
                _surface.Opacity = 0f;
                return;
            }

            if (_model == null || Time.unscaledTimeAsDouble >= _nextRefresh)
            {
                Double3 viewer = _simulation != null ? _simulation.Origin.OriginKm : Double3.Zero;
                _model = InspectorModel.For(_simulation, _view, viewer, _options);
                _nextRefresh = Time.unscaledTimeAsDouble + InspectorModel.RefreshSeconds;
            }

            Place();
            _surface.Opacity = 1f;
            Layout();
        }

        private void Place()
        {
            Transform cameraTransform = _camera.transform;
            Vector3 toObject = _view.transform.position - cameraTransform.position;
            float distance = Mathf.Clamp(toObject.magnitude, 0.5f, 2f);

            Vector3 direction = toObject.sqrMagnitude > 1e-8f ? toObject.normalized : cameraTransform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            if (right.sqrMagnitude < 1e-8f) right = cameraTransform.right;
            right.Normalize();

            // A fixed angular offset, not a fixed metric one: at a fixed offset the card sits on
            // top of a room-sized planet and a metre away from a distant one.
            Vector3 offset = (direction + right * Mathf.Tan(OffsetRadians)).normalized * distance;
            _surface.Root.position = cameraTransform.position + offset;
            _surface.Root.rotation = cameraTransform.rotation;
        }

        private void Layout()
        {
            float distance = Vector3.Distance(_surface.Root.position, _camera.transform.position);
            _surface.Begin(distance);

            float pad = _surface.Dmm(CosmoTheme.SpacePanelPadding);
            float lineHeight = _surface.Dmm(34f);
            float titleHeight = _surface.Dmm(44f);
            float buttonHeight = _surface.Dmm(CosmoTheme.ButtonHeight);
            float gap = _surface.Dmm(CosmoTheme.ButtonGap);

            int lines = Mathf.Min(3, _model.Now.Count);
            float contentWidth = _surface.MeasureWidth(_model.Name, CosmoTheme.TypeTitle);
            for (int i = 0; i < lines; i++)
            {
                contentWidth = Mathf.Max(contentWidth,
                    _surface.MeasureWidth(_model.Now[i].Label + ": " + _model.Now[i].Value, CosmoTheme.TypeCaption));
            }
            contentWidth = Mathf.Max(contentWidth, _surface.Dmm(3f * CosmoTheme.ButtonMinWidth + 2f * CosmoTheme.ButtonGap));

            float badge = HonestyState.FocusBadge(_options) != null ? lineHeight : 0f;
            float noticeHeight = _model.Notice != null ? lineHeight : 0f;
            float height = 2f * pad + titleHeight + lines * lineHeight + noticeHeight + badge + gap + buttonHeight;
            float width = contentWidth + 2f * pad;

            float left = -0.5f * contentWidth;
            float top = 0.5f * height - pad;

            _surface.Text(CosmoTheme.ProvenanceGlyph(_model.Weakest) + " " + _model.Name, left, top,
                CosmoTheme.TypeTitle, CosmoTheme.Provenance(_model.Weakest));
            top -= titleHeight;

            if (_model.Notice != null)
            {
                _surface.Text("! " + _model.Notice, left, top, CosmoTheme.TypeCaption, CosmoTheme.Warning);
                top -= lineHeight;
            }

            for (int i = 0; i < lines; i++)
            {
                InspectorField field = _model.Now[i];
                _surface.Text(field.Label + ": " + field.Value, left, top, CosmoTheme.TypeCaption,
                    CosmoTheme.TextPrimary);
                top -= lineHeight;
            }

            // The on-universe badge, in the headset and not only in the desktop HUD.
            string focusBadge = HonestyState.FocusBadge(_options);
            if (focusBadge != null)
            {
                _surface.Text(focusBadge, left, top, CosmoTheme.TypeCaption, CosmoTheme.Warning);
                top -= lineHeight;
            }

            string[] labels = { "GO", _travel != null && _travel.FollowingId == _view.Body.Id ? "FREE" : "FOLLOW", "MORE" };
            float cell = (contentWidth - 2f * gap) / 3f;
            for (int i = 0; i < labels.Length; i++)
            {
                float cx = left + i * (cell + gap) + 0.5f * cell;
                _surface.StrokedRect(cx, top - 0.5f * buttonHeight, cell, buttonHeight,
                    CosmoTheme.Surface, CosmoTheme.Stroke, SurfaceStyle.Fill, i);
                _surface.TextCentred(labels[i], cx, top - buttonHeight * 0.3f, CosmoTheme.TypeCaption,
                    CosmoTheme.TextPrimary);
            }

            _surface.End(width, height);
        }

        /// <summary>Press one of the card's three buttons, by index. Routed from the selector's ray.</summary>
        public void Press(int index)
        {
            if (_view == null) return;
            switch (index)
            {
                case 0:
                {
                    Destination destination = _destinations?.Find("body:" + _view.Body.Id);
                    if (destination != null) _travel?.Travel(destination);
                    break;
                }
                case 1: _travel?.ToggleFollow(_view.Body.Id); break;
            }
        }

        /// <summary>Hit test in the card's own plane, for the ray. -1 when it misses.</summary>
        public int HitId(Vector3 worldPoint)
        {
            if (_surface == null) return -1;
            Vector3 local = _surface.Root.InverseTransformPoint(worldPoint);
            return _surface.HitId(new Vector2(local.x, local.y));
        }

        /// <summary>Diagnostics for the play-mode report.</summary>
        public string Describe() =>
            _surface == null ? "(none)" : (_view == null ? "hidden" : _view.Body.Name + " " + _surface.Describe());

        private void OnDestroy() => _surface?.Destroy();
    }
}
