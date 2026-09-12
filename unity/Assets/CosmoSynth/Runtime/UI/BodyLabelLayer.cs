using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Names for whatever is on screen: bodies, and everything the layers offer through
    /// <c>CollectLabels</c>.
    ///
    /// <b>One mesh for all of them.</b> A legacy <c>TextMesh</c> per label is one draw call each and
    /// dynamic batching is off, so twenty-four labels were twenty-four draws — a sixth of the whole
    /// 150-draw frame budget spent on text. The glyphs go into a single mesh parented to the camera,
    /// each at its own depth so the labels stay sorted against the bodies they name.
    ///
    /// <b>Only drawn things are labelled.</b> A body that has dropped below the visibility floor
    /// keeps a stale <c>RenderedRadius</c>, and a label placed from one used to float in empty space
    /// where the planet had been. <c>IsGeometryDrawn</c> is re-checked every frame, because a
    /// selection outlives the body.
    ///
    /// <b>No crowd shrink.</b> At the measured 1140 px/rad a 19 dmm caption is 21.7 px, which is
    /// above a legibility floor but not comfortably; shrinking a crowded label pushes it under. So
    /// crowding drops labels instead, and the selected, followed and travel-target labels are one
    /// size up.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("CosmoSynth/Body Label Layer")]
    public sealed class BodyLabelLayer : MonoBehaviour
    {
        /// <summary>At most this many world labels (§5.6.10). Today's build drew 14.</summary>
        public const int MaximumLabels = 24;

        /// <summary>Minimum separation in viewport fractions before a label is dropped.</summary>
        public const float DeclutterRadius = 0.055f;

        private struct Candidate
        {
            public string Text;
            public Vector3 World;
            public float Priority;
            public bool Emphasised;
            public Color Colour;
            public LabelCategory Category;
        }

        private readonly List<Candidate> _candidates = new List<Candidate>(128);
        private readonly List<LabelCandidate> _fromLayers = new List<LabelCandidate>(64);
        private readonly List<Vector2> _placed = new List<Vector2>(MaximumLabels);

        private UniverseSimulation _simulation;
        private ViewOptions _options;
        private BodySelector _selector;
        private TravelController _travel;
        private Camera _camera;
        private CosmoTextMesh _mesh;

        /// <summary>How many labels were drawn this frame; the play-mode report prints it.</summary>
        public int Drawn { get; private set; }

        public void Bind(UniverseSimulation simulation, ViewOptions options, BodySelector selector,
            TravelController travel, Camera camera)
        {
            _simulation = simulation;
            _options = options;
            _selector = selector;
            _travel = travel;
            _camera = camera;
        }

        private void Awake()
        {
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();
            if (_camera == null) _camera = Camera.main;
        }

        private void LateUpdate()
        {
            if (_mesh == null) _mesh = new CosmoTextMesh(transform, "World Labels");
            if (_camera == null) { _mesh.Visible = false; return; }

            bool show = _options == null || (_options.LabelsMaster && !_options.CleanView);
            _mesh.Visible = show;
            if (!show) { Drawn = 0; return; }

            // The mesh rides the camera, so a label's local z is its forward distance and its
            // angular size converts to metres there.
            _mesh.Root.SetPositionAndRotation(_camera.transform.position, _camera.transform.rotation);

            Gather();
            Place();
        }

        private void Gather()
        {
            _candidates.Clear();
            if (_simulation == null) return;

            string selectedId = _selector != null && _selector.Selected != null ? _selector.Selected.Body.Id : null;
            string followedId = _travel != null ? _travel.FollowingId : null;
            string targetId = _travel != null && _travel.Target != null ? _travel.Target.BodyId : null;

            foreach (CelestialBodyView view in _simulation.Views.Values)
            {
                if (view == null || view.Body == null) continue;
                if (!view.IsPointable) continue;

                LabelCategory category = view.Body.Type == BodyType.Spacecraft
                    ? LabelCategory.Spacecraft
                    : view.Body.Site.HasValue ? LabelCategory.Sites : LabelCategory.Bodies;
                if (_options != null && !_options.LabelCategoryOn(category)) continue;

                bool emphasised = view.Body.Id == selectedId || view.Body.Id == followedId ||
                                  view.Body.Id == targetId;

                // Apparent size is the ranking, because a label is worth drawing in proportion to
                // how much of the view its subject occupies.
                float priority = (float)view.ApparentDiameterPixels + (emphasised ? 1e6f : 0f);

                float above = view.IsGeometryDrawn ? view.RenderedRadius : 0f;
                _candidates.Add(new Candidate
                {
                    Text = view.Body.Name,
                    World = view.transform.position + _camera.transform.up * (above * 1.15f),
                    Priority = priority,
                    Emphasised = emphasised,
                    Colour = CosmoTheme.Provenance(view.Body.Provenance != null
                        ? view.Body.Provenance.WeakestClass()
                        : ProvenanceClass.Unknown),
                    Category = category
                });
            }

            GatherFromLayers();
            _candidates.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        private void GatherFromLayers()
        {
            IReadOnlyList<CosmoLayer> layers = LayerRegistry.All;
            for (int i = 0; i < layers.Count; i++)
            {
                CosmoLayer layer = layers[i];
                if (layer == null || layer.Weight <= 0f) continue;

                _fromLayers.Clear();
                layer.CollectLabels(_fromLayers);

                for (int j = 0; j < _fromLayers.Count; j++)
                {
                    LabelCandidate candidate = _fromLayers[j];
                    if (_options != null && !_options.LabelCategoryOn(candidate.Category)) continue;

                    Vector3 world = candidate.IsDirectionOnly
                        ? _camera.transform.position +
                          _simulation.Origin.MapDirection(candidate.DirectionIcrf).normalized * 50f
                        : _simulation.transform.TransformPoint(_simulation.Origin.ToRender(candidate.PositionKm));

                    _candidates.Add(new Candidate
                    {
                        Text = candidate.Text,
                        World = world,
                        Priority = candidate.Priority,
                        Emphasised = false,
                        Colour = CosmoTheme.Provenance(candidate.Provenance),
                        Category = candidate.Category
                    });
                }
            }
        }

        private void Place()
        {
            _mesh.Begin();
            _placed.Clear();
            Drawn = 0;

            for (int i = 0; i < _candidates.Count && Drawn < MaximumLabels; i++)
            {
                Candidate candidate = _candidates[i];

                Vector3 viewport = _camera.WorldToViewportPoint(candidate.World);
                if (viewport.z <= 0.05f || viewport.x < -0.05f || viewport.x > 1.05f ||
                    viewport.y < -0.05f || viewport.y > 1.05f) continue;

                // Greedy screen-space rejection: the highest-priority label in a crowded patch
                // wins and the rest are dropped, rather than every label shrinking below legible.
                var screen = new Vector2(viewport.x, viewport.y);
                bool crowded = false;
                for (int p = 0; p < _placed.Count; p++)
                {
                    if (Vector2.Distance(_placed[p], screen) >= DeclutterRadius) continue;
                    crowded = true;
                    break;
                }
                if (crowded) continue;

                Vector3 local = _camera.transform.InverseTransformPoint(candidate.World);
                float dmm = candidate.Emphasised ? CosmoTheme.TypeBody : CosmoTheme.TypeCaption;
                float characterSize = CosmoText.CharacterSizeFor(CosmoTheme.Metres(dmm, local.z));
                float width = CosmoText.MeasureWidth(candidate.Text, characterSize);

                _mesh.AddLine(candidate.Text, local.x - 0.5f * width, local.y, local.z, characterSize,
                    candidate.Emphasised ? CosmoTheme.TextPrimary : candidate.Colour);

                // The on-universe honesty badge rides under the focus's own label, in the headset.
                if (candidate.Emphasised)
                {
                    string badge = HonestyState.FocusBadge(_options);
                    if (badge != null)
                    {
                        float badgeSize = CosmoText.CharacterSizeFor(
                            CosmoTheme.Metres(CosmoTheme.TypeCaption, local.z));
                        float badgeWidth = CosmoText.MeasureWidth(badge, badgeSize);
                        _mesh.AddLine(badge, local.x - 0.5f * badgeWidth,
                            local.y - CosmoTheme.Metres(30f, local.z), local.z, badgeSize, CosmoTheme.Warning);
                    }
                }

                _placed.Add(screen);
                Drawn++;
            }

            _mesh.End();
        }

        /// <summary>Diagnostics for the play-mode report.</summary>
        public string Describe() =>
            $"{Drawn} of {_candidates.Count} candidates, {(_mesh != null ? _mesh.Glyphs : 0)} glyphs, 1 draw";

        private void OnDestroy() => _mesh?.Destroy();
    }
}
