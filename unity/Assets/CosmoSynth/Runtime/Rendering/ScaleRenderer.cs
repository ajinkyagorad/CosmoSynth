using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Places everything, once per frame, in one pass.
    ///
    /// The order is fixed: build the frame's context, choose each body's level of detail from its
    /// <i>true</i> apparent size, build one <see cref="GroupNode"/> per body in catalogue order,
    /// run <see cref="ScaledSpace.Solve"/>, then hand every view its placement. Decorations run at
    /// execution order 60, after this, so they read placed transforms rather than last frame's —
    /// which is what used to give <c>IsGeometryDrawn</c> a one-frame lag.
    ///
    /// What is gone: this component used to re-fit the camera's clip planes every frame from the
    /// farthest drawn thing, and hand a "horizon" distance back to the simulation so that anything
    /// past it was pinned to a sky sphere. Both were symptoms of trying to reach light years with
    /// a linear depth buffer. The distance map and the group solver replace them, and the clip
    /// planes are now two constants set once in <see cref="XrRig"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    [AddComponentMenu("CosmoSynth/Scale Renderer")]
    public sealed class ScaleRenderer : MonoBehaviour
    {
        [SerializeField] private UniverseSimulation _simulation;
        [SerializeField] private Camera _camera;

        [Header("Diagnostics")]
        [SerializeField] private int _rigidGroups;
        [SerializeField] private int _envelopeGroups;
        [SerializeField] private int _enclosingGroups;
        [SerializeField] private int _pushConflicts;
        [SerializeField] private int _volumeOcclusion;
        [SerializeField] private float _farthestRenderedMetres;
        private bool _historyValid;

        private readonly List<GroupNode> _nodes = new List<GroupNode>();
        private readonly List<CelestialBodyView> _ordered = new List<CelestialBodyView>();
        private readonly Dictionary<string, int> _indexById = new Dictionary<string, int>();
        private GroupPlacement[] _placement = new GroupPlacement[0];
        private GroupPlacement[] _previous = new GroupPlacement[0];
        private bool _topologyDirty = true;

        /// <summary>Rigid groups that own a subtree, this frame.</summary>
        public int RigidGroups => _rigidGroups;

        /// <summary>Bodies drawn at true scale because the eye is close to them.</summary>
        public int EnvelopeGroups => _envelopeGroups;

        /// <summary>Volumes that may contain the eye.</summary>
        public int EnclosingGroups => _enclosingGroups;

        /// <summary>
        /// Groups whose push was refused by the near-point floor, so their depth order against
        /// the group behind them is not guaranteed. Red on the render line; the play-mode gate
        /// requires zero.
        /// </summary>
        public int PushConflicts => _pushConflicts;

        /// <summary>The farthest rendered surface this frame, metres. Must stay inside the far plane.</summary>
        public float FarthestRenderedMetres => _farthestRenderedMetres;

        /// <summary>
        /// Drawn bodies inside or in front of an active enclosing volume (R6). Red on the render
        /// line: each one punches a body-shaped hole through the whole of that volume.
        /// </summary>
        public int VolumeOcclusion => _volumeOcclusion;

        /// <summary>The context built this frame, for anything that needs the eye or the scale.</summary>
        public FrameContext Context { get; private set; }

        /// <summary>
        /// Resolve dependencies lazily rather than once in Awake.
        ///
        /// This component lives on the camera rig, which the bootstrap builds <i>before</i> the
        /// simulation — so an Awake-time lookup finds null, and a component that caches null once
        /// never recovers. The symptom is silent and total: LateUpdate returns early every frame,
        /// nothing is ever placed, and nothing anywhere logs a complaint.
        /// </summary>
        private bool TryResolve()
        {
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();

            // Hoisted out of the null branch above, and named rather than a lambda.
            //
            // _simulation is a [SerializeField], so a prefab or an inspector assignment means the
            // branch above never runs — and the subscriptions used to live inside it. One rebuild
            // then happened because _topologyDirty starts true, and every body added afterwards
            // was never placed, silently, with nothing logged. The lambdas also captured `this`
            // for the life of the event; named handlers can be unsubscribed, and OnDestroy does.
            if (!_subscribed && _simulation != null)
            {
                _subscribed = true;
                _simulation.CatalogLoaded += OnCatalogLoaded;
                _simulation.BodiesChanged += OnBodiesChanged;
            }

            if (_camera == null) _camera = Camera.main;
            return _simulation != null && _simulation.Origin != null && _camera != null;
        }

        private bool _subscribed;

        private void OnCatalogLoaded(CelestialCatalog catalog) => _topologyDirty = true;

        private void OnBodiesChanged() => _topologyDirty = true;

        private void OnDestroy()
        {
            if (!_subscribed || _simulation == null) return;
            _simulation.CatalogLoaded -= OnCatalogLoaded;
            _simulation.BodiesChanged -= OnBodiesChanged;
            _subscribed = false;
        }

        /// <summary>
        /// The constant half of the shader globals, before anything can draw.
        ///
        /// See <see cref="Placement.PublishConstants"/>: unset, _CS_Compress is zero and every
        /// compressing vertex shader emits NaN for the whole of the catalogue's streaming window.
        /// </summary>
        private void OnEnable() => Placement.PublishConstants();

        private void LateUpdate()
        {
            if (!TryResolve()) return;

            // Above the catalogue guard. The eye and the ICRF mapping do not depend on the
            // catalogue, and the shaders need both from frame 1 — the sky shell, the star field
            // and every point layer are anchored on them.
            FrameContext ctx = BuildContext();
            Context = ctx;
            Placement.PublishGlobals(ctx);

            if (_simulation.Catalog == null) return;

            if (_topologyDirty) RebuildTopology();
            if (_ordered.Count == 0) return;

            FillNodes(ctx);

            if (_placement.Length < _nodes.Count)
            {
                _placement = new GroupPlacement[_nodes.Count];
                _previous = new GroupPlacement[_nodes.Count];
            }

            // No hysteresis across a topology rebuild. `previous` is read BY INDEX, and a
            // rebuild renumbers every node from the catalogue — so after an add or a remove,
            // previous[i] describes a different body and the Rigid/Envelope cut can flip for one
            // frame on an arbitrary set of groups. A visible pop at exactly the moment content
            // arrives. Solve documents null as "first frame", which is what this is.
            _pushConflicts = ScaledSpace.Solve(_nodes, ctx.EyeKm, ctx.UnitsPerKm,
                _historyValid ? _previous : null, _placement);
            _historyValid = true;

            _rigidGroups = 0;
            _envelopeGroups = 0;
            _enclosingGroups = 0;
            _farthestRenderedMetres = 0f;
            _volumeOcclusion = 0;

            for (int i = 0; i < _ordered.Count; i++)
            {
                CelestialBodyView view = _ordered[i];
                if (view == null) continue;

                GroupPlacement p = _placement[i];
                switch (p.Mode)
                {
                    case GroupMode.Rigid: _rigidGroups++; break;
                    case GroupMode.Envelope: _envelopeGroups++; break;
                    case GroupMode.Enclosing: _enclosingGroups++; break;
                }

                view.Place(ctx, p);

                // Measured against what is DRAWN, not where the body is. Those differ by decades
                // and the far plane only has to cover what is rasterised.
                if (view.IsGeometryDrawn || view.IsMarkerDrawn)
                {
                    _farthestRenderedMetres = Mathf.Max(_farthestRenderedMetres,
                        (float)p.RenderedFarMetres + view.RenderedRadius);
                }
            }

            CountVolumeOcclusion();

            // Hysteresis: next frame's cut is decided partly by this frame's, so a group sitting
            // on the 9 m threshold does not flip mode with every twitch of the head.
            GroupPlacement[] swap = _previous;
            _previous = _placement;
            _placement = swap;
        }

        private FrameContext BuildContext()
        {
            FloatingOrigin origin = _simulation.Origin;
            Transform universe = _simulation.transform;

            Vector3 eyeWorld = _camera.transform.position;
            Vector3 eyeUniverse = universe.InverseTransformPoint(eyeWorld);
            Double3 eyeKm = origin.ToAbsolute(eyeUniverse);

            // Pixels per radian from the eye buffer, not from the editor's Game view: every
            // angular rule in the app is denominated in this, and the two differ by about 2.2x.
            int verticalPixels = ViewportInfo.VerticalPixels(_camera);
            float verticalFov = _camera.fieldOfView * Mathf.Deg2Rad;
            float pixelsPerRadian = verticalFov > 0f ? verticalPixels / verticalFov : 0f;

            Matrix4x4 icrfToWorld = universe.localToWorldMatrix * origin.IcrfToUniverse;

            return new FrameContext(eyeKm, eyeWorld, eyeUniverse, origin.UnitsPerKm,
                icrfToWorld, pixelsPerRadian, _simulation.Clock != null ? _simulation.Clock.Now : default);
        }

        /// <summary>
        /// Rebuild the node order from the catalogue tree. Only when the catalogue changes: the
        /// order is parents before children, which is what lets the solver resolve a subtree in
        /// one forward pass.
        /// </summary>
        private void RebuildTopology()
        {
            _topologyDirty = false;
            _historyValid = false;
            _ordered.Clear();
            _indexById.Clear();
            _nodes.Clear();

            foreach (CelestialBody body in _simulation.Catalog.Traverse())
            {
                if (!_simulation.Views.TryGetValue(body.Id, out CelestialBodyView view) || view == null) continue;
                _indexById[body.Id] = _ordered.Count;
                _ordered.Add(view);
                _nodes.Add(default);
            }

            AssertParentsPrecedeChildren();
        }

        /// <summary>
        /// The one thing FillNodes and ScaledSpace.Solve both assume and neither states: a body's
        /// parent has a LOWER index than the body.
        ///
        /// Both silently drop the link when it does not hold, which degrades a moon to an
        /// independent group — exactly the per-body placement the solver's own tests prove breaks
        /// a transit. Catalog.Traverse is depth-first from the roots, so it holds today; a change
        /// to the traversal order would otherwise turn moons back into independent groups with
        /// nothing anywhere noticing. One pass over a list that is already built.
        /// </summary>
        private void AssertParentsPrecedeChildren()
        {
            for (int i = 0; i < _ordered.Count; i++)
            {
                CelestialBodyView view = _ordered[i];
                if (view == null) continue;
                string parentId = view.Body.ParentId;
                if (string.IsNullOrEmpty(parentId)) continue;
                if (!_indexById.TryGetValue(parentId, out int parent) || parent < i) continue;

                Debug.LogError(
                    $"[CosmoSynth] catalogue order puts '{parentId}' (index {parent}) at or after " +
                    $"its child '{view.Body.Id}' (index {i}). The child will be placed as an " +
                    "independent group, which breaks intra-group depth order — see ScaledSpace.");
            }
        }

        /// <summary>
        /// R6: drawn bodies inside or in front of an active enclosing volume, this frame.
        ///
        /// The volume shaders use ZTest LEqual on their BACK face, so an opaque surface nearer
        /// than that face rejects the whole primitive and the entire ray integral is lost — a
        /// body-shaped hole through the whole cloud, near half included. That cost is accepted
        /// and bounded (contract 7.1 rule 5, 13); what is not acceptable is it being invisible,
        /// so the count goes on the render line and the play-mode gate reads it.
        ///
        /// Zero cost while nothing encloses, which is every frame of the foundation alpha: no
        /// layer declares MayEnclose until the dust volume lands.
        /// </summary>
        private void CountVolumeOcclusion()
        {
            if (_enclosingGroups == 0) return;

            for (int v = 0; v < _ordered.Count; v++)
            {
                if (_placement[v].Mode != GroupMode.Enclosing) continue;
                double back = _placement[v].RenderedFarMetres;

                for (int i = 0; i < _ordered.Count; i++)
                {
                    CelestialBodyView body = _ordered[i];
                    if (body == null || !body.IsGeometryDrawn) continue;
                    if (_placement[i].Mode == GroupMode.Enclosing) continue;
                    if (_placement[i].RenderedNearMetres < back) _volumeOcclusion++;
                }
            }
        }

        /// <summary>
        /// Fill this frame's group nodes: anchor, own drawn radius, and the subtree radius.
        ///
        /// The subtree radius accumulates in reverse order, so each parent sees its children's
        /// finished radii in one O(n) pass. For a two-level system that is exactly the flat
        /// definition; deeper than that it is an upper bound, which makes a group dissolve
        /// slightly earlier than it strictly must — the conservative direction.
        ///
        /// Rings and atmospheres are deliberately NOT in the radius. Neither writes depth, so
        /// neither can take part in the occlusion ordering the radius exists to drive; including
        /// them would dissolve Saturn's system two and a half times sooner for no benefit.
        /// </summary>
        private void FillNodes(in FrameContext ctx)
        {
            for (int i = 0; i < _ordered.Count; i++)
            {
                CelestialBodyView view = _ordered[i];
                GroupNode node = default;
                node.Parent = -1;

                if (view != null)
                {
                    string parentId = view.Body.ParentId;
                    if (!string.IsNullOrEmpty(parentId) && _indexById.TryGetValue(parentId, out int parent) &&
                        parent < i)
                    {
                        node.Parent = parent;
                    }

                    node.AnchorKm = view.AbsolutePositionKm;
                    node.LeafRadiusKm = view.IsKnown ? _simulation.DisplayRadiusKm(view.Body) : 0.0;
                    node.RadiusKm = node.LeafRadiusKm;
                    node.HasOpaque = view.IsKnown;

                    // True apparent size decides the level of detail, and the 2.5 px floor inside
                    // Place() decides whether there is any geometry at all.
                    double distanceKm = Double3.Distance(ctx.EyeKm, view.AbsolutePositionKm);
                    double pixels = ctx.ApparentPixels(node.LeafRadiusKm, distanceKm);
                    view.SetRepresentation(
                        ScaleSpace.SelectRepresentation(pixels, insideBody: distanceKm <= node.LeafRadiusKm),
                        pixels);
                }

                _nodes[i] = node;
            }

            for (int i = _ordered.Count - 1; i >= 0; i--)
            {
                GroupNode node = _nodes[i];
                if (node.Parent < 0) continue;

                GroupNode parent = _nodes[node.Parent];
                parent.HasChildren = true;

                // 10 % of headroom, so a group does not dissolve on the frame a moon reaches
                // apoapsis.
                double reach = (Double3.Distance(node.AnchorKm, parent.AnchorKm) + node.RadiusKm) * 1.1;
                if (reach > parent.RadiusKm) parent.RadiusKm = reach;

                _nodes[node.Parent] = parent;
            }
        }
    }
}
