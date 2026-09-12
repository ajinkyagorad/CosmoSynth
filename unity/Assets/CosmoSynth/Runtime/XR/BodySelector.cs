using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Point at a body and select it: a ray from the hand or controller, a highlight, and the
    /// provenance panel.
    ///
    /// Selection uses an explicit sphere-vs-ray test rather than Unity physics. Colliders on
    /// celestial bodies would be meaningless — they are scaled by many orders of magnitude as the
    /// user zooms, and a collider that large wrecks the broadphase — so the test is done against
    /// each body's known render radius directly. That also means selection keeps working when a
    /// body is drawn at its minimum visible size rather than its true one, which is exactly when
    /// a physics raycast would start missing things the user can plainly see.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Body Selector")]
    public sealed class BodySelector : MonoBehaviour
    {
        [Header("Ray")]
        [SerializeField] private float _rayLengthMetres = 6f;
        [SerializeField] private float _rayWidthMetres = 0.004f;

        [Tooltip("Extra angular tolerance so small bodies are selectable without pixel-hunting.")]
        [SerializeField] private float _selectionForgivenessDegrees = 1.6f;

        private UniverseSimulation _simulation;
        private XrRig _rig;
        private XrHandInput _hands;
        private XrInput _input;
        private SpatialPanel _panel;
        private LineRenderer _ray;
        private Material _rayMaterial;
        private Transform _highlight;
        private Material _highlightMaterial;

        private CelestialBodyView _hovered;

        /// <summary>The currently selected body, or null.</summary>
        public CelestialBodyView Selected { get; private set; }

        private void Awake()
        {
            _simulation = FindAnyObjectByType<UniverseSimulation>();
            _rig = FindAnyObjectByType<XrRig>();
            _hands = FindAnyObjectByType<XrHandInput>();
            _input = FindAnyObjectByType<XrInput>();

            var panelObject = new GameObject("Spatial Panel");
            _panel = panelObject.AddComponent<SpatialPanel>();

            BuildRay();
            BuildHighlight();
        }

        private void BuildRay()
        {
            var rayObject = new GameObject("Pointer Ray");
            rayObject.transform.SetParent(transform, false);

            _ray = rayObject.AddComponent<LineRenderer>();
            _ray.useWorldSpace = true;
            _ray.positionCount = 2;
            _ray.startWidth = _rayWidthMetres;
            _ray.endWidth = _rayWidthMetres * 0.4f;
            _ray.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ray.receiveShadows = false;

            _rayMaterial = CosmoShaders.Material(CosmoShaders.Unlit);
            if (_rayMaterial == null) { _ray.enabled = false; return; }
            _rayMaterial.color = CosmoTheme.Accent;
            _ray.sharedMaterial = _rayMaterial;
            _ray.enabled = false;
        }

        private void BuildHighlight()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Selection Highlight";
            DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(transform, false);
            _highlight = sphere.transform;

            var renderer = sphere.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The finer sphere, unconditionally.
            //
            // This one is always large when it is on at all — it traces a silhouette at 2.18x the
            // body's radius — so the primitive's 20 segments are always visible on it, and there
            // is only ever one of it. The rim shader makes it worse than a lit surface would: it
            // culls front faces and peaks exactly at the silhouette, so the polygon edge is
            // drawn at the brightest part of the effect rather than the dimmest.
            var filter = sphere.GetComponent<MeshFilter>();
            Mesh detailed = CosmoSphere.Detailed(filter.sharedMesh);
            if (detailed != null) filter.sharedMesh = detailed;

            // A dedicated rim shader rather than a tinted particle material.
            //
            // Two reasons. Particles/Standard Unlit in additive mode does not modulate by the
            // tint's alpha, so the obvious brightness dial silently does nothing — dropping the
            // alpha from 0.5 to 0.16 changed the picture not at all. And a filled additive
            // sphere reads as a blue filter over the planet rather than as an outline; at 2.35x
            // it covered Earth completely and turned the Blue Marble into a flat cyan disc.
            // CosmoRim culls front faces and fades toward the outer edge, so what is left is a
            // soft outline hugging the silhouette that does not fight the atmosphere sitting in
            // the same few percent of radius.
            _highlightMaterial = CosmoShaders.Material(CosmoShaders.Rim);
            if (_highlightMaterial == null) { renderer.enabled = false; sphere.SetActive(false); return; }
            _highlightMaterial.SetColor("_Color", CosmoTheme.WithAlpha(CosmoTheme.Accent, 0.6f));
            _highlightMaterial.SetFloat("_Power", 2.5f);
            renderer.sharedMaterial = _highlightMaterial;

            sphere.SetActive(false);
        }

        private void Update()
        {
            if (_simulation?.Catalog == null) return;

            if (!TryGetPointer(out Vector3 origin, out Vector3 direction))
            {
                _ray.enabled = false;
                SetHovered(null);
                return;
            }

            _ray.enabled = true;

            CelestialBodyView previousHover = _hovered;
            _hovered = Pick(origin, direction);

            // A tick when the ray first crosses a body. Aiming at a two-pixel moon is otherwise
            // pure guesswork — the ray's colour change is the only cue, and it is at the far end
            // of the beam rather than under the user's attention.
            if (_hovered != null && _hovered != previousHover) _input?.Pulse(Hand.Right, 0.18f, 0.015f);

            float length = _hovered != null
                ? Vector3.Distance(origin, _hovered.transform.position)
                : _rayLengthMetres;

            _ray.SetPosition(0, origin);
            _ray.SetPosition(1, origin + direction * length);

            Color rayColour = _hovered != null ? CosmoTheme.StrokeActive : CosmoTheme.Accent;
            _rayMaterial.color = CosmoTheme.WithAlpha(rayColour, _hovered != null ? 0.95f : 0.45f);

            PollSelect();
            UpdateHighlight();
        }

        /// <summary>
        /// Where the pointer comes from. Prefers a controller's aim pose; falls back to the
        /// dominant hand; falls back again to head gaze, so selection still works with no
        /// tracked input at all.
        /// </summary>
        private bool TryGetPointer(out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            // The right hand aims, because the right trigger selects. Falling back to the left
            // for the left-handed and to gaze when nothing is tracked at all, so selection is
            // never simply unavailable.
            if (TryGetHandPose(Hand.Right, out origin, out Quaternion rotation) ||
                TryGetHandPose(Hand.Left, out origin, out rotation))
            {
                direction = rotation * Vector3.forward;
                return true;
            }

            if (_rig?.Camera != null)
            {
                origin = _rig.Camera.transform.position;
                direction = _rig.Camera.transform.forward;
                return true;
            }

            return false;
        }

        private bool TryGetHandPose(Hand hand, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (_input == null || !_input.TryGetPose(hand, out Vector3 local, out Quaternion localRotation))
                return false;

            Transform space = _rig != null && _rig.Origin != null
                ? _rig.Origin.CameraFloorOffsetObject.transform
                : null;

            position = space != null ? space.TransformPoint(local) : local;
            rotation = space != null ? space.rotation * localRotation : localRotation;
            return true;
        }

        /// <summary>
        /// Nearest body whose rendered sphere the ray passes through, with a small angular
        /// tolerance so a distant moon does not require sub-degree aim.
        /// </summary>
        private CelestialBodyView Pick(Vector3 origin, Vector3 direction)
        {
            CelestialBodyView best = null;
            float bestDistance = float.MaxValue;
            float forgiveness = Mathf.Tan(_selectionForgivenessDegrees * Mathf.Deg2Rad);

            foreach (CelestialBodyView view in _simulation.Views.Values)
            {
                if (view == null) continue;

                // You cannot point at something that is not being drawn.
                //
                // Without this the pointer happily picks a body the renderer has switched off —
                // a planet pinned to the sky sphere, or a spacecraft outside the span its
                // ephemeris covers. Worse, such a body's transform is never rescaled (UpdateState
                // returns early), so it keeps whatever RenderedRadius it last had, and the aim
                // test below reads that stale value as an enormous target. That is what put a
                // fresnel shell the size of the sky across half the view: the highlight ring
                // wrapped around Dawn, a spacecraft that stopped transmitting in 2018 and was
                // not on screen at all.
                // IsPointable, not IsGeometryDrawn: a spacecraft that has degraded to a marker
                // dot is still the only thing on screen at that point, and being unable to select
                // the dot you can plainly see is the same failure in the other direction.
                if (!view.IsPointable) continue;

                Vector3 toBody = view.transform.position - origin;
                float along = Vector3.Dot(toBody, direction);
                if (along <= 0f) continue;                       // behind the pointer

                float perpendicular = (toBody - direction * along).magnitude;

                // The drawn radius, which is what the user is aiming at — not the physical one,
                // and not the true-scale one a body the eye is inside reports (RoomRadius is
                // zero there, so the forgiveness cone alone decides, which is the honest answer
                // for a surface you are standing on).
                float radius = view.RoomRadius + along * forgiveness;
                if (perpendicular > radius) continue;

                if (along < bestDistance)
                {
                    bestDistance = along;
                    best = view;
                }
            }

            return best;
        }

        /// <summary>
        /// Select on the right trigger, or on a hand-tracked tap.
        ///
        /// Both edges are detected by their owners rather than here: <see cref="XrInput"/> knows
        /// the trigger's rising edge, and <see cref="XrHandInput"/> knows a pinch was a tap rather
        /// than the start of a drag. This component used to do its own edge detection on a trigger
        /// that was simultaneously the grab button, so hauling the universe across the room
        /// re-selected whatever the ray happened to sweep over on the way.
        /// </summary>
        private void PollSelect()
        {
            bool pressed = (_input != null && _input.SelectPressed) ||
                           (_hands != null && _hands.Active == XrHandInput.Modality.Hands && _hands.SelectTap);
            if (!pressed) return;

            // Clicking empty space deselects, which is the only way to dismiss the panel.
            Selected = _hovered;
            _panel.Show(Selected);

            _input?.Pulse(Hand.Right, Selected != null ? 0.5f : 0.15f, 0.05f);

            if (Selected != null)
            {
                _simulation.SetFocus(Selected.Body.Id);
                Debug.Log($"[CosmoSynth] selected {Selected.Body.Name}");
            }
        }

        private void SetHovered(CelestialBodyView view) => _hovered = view;

        /// <summary>Diagnostics: whether a ring is on screen and how big it is.</summary>
        public string DescribeHighlight() => _highlight == null ? "(none)"
            : $"active={_highlight.gameObject.activeSelf} worldScale={_highlight.lossyScale.x:G4} "
              + $"selected={(Selected != null ? Selected.Body.Id : "-")} "
              + $"hovered={(_hovered != null ? _hovered.Body.Id : "-")}";

        private void UpdateHighlight()
        {
            CelestialBodyView shown = Selected ?? _hovered;

            // A ring around something that is not drawn is a ring around nothing. A selection
            // made while a body was visible outlives it — the clock warps past the end of a
            // spacecraft's ephemeris, or the user flies far enough out that the body is pinned
            // to the sky sphere — so this has to be re-checked every frame, not only when the
            // selection changes.
            if (shown != null && !shown.IsPointable) shown = null;

            if (shown == null)
            {
                _highlight.gameObject.SetActive(false);
                return;
            }

            _highlight.gameObject.SetActive(true);
            _highlight.position = shown.transform.position;
            // 1.09x: the rim fades out well before this, so the extra room is what the
            // falloff needs to be a gradient rather than a hard edge. Any bigger and the glow
            // detaches from the silhouette it is supposed to be tracing.
            //
            // Set in world terms. localScale alone is wrong whenever this component's transform
            // carries a scale of its own, and the ring then misses the silhouette it is supposed
            // to trace by whatever that scale happens to be.
            float parentScale = _highlight.parent != null ? Mathf.Max(1e-6f, _highlight.parent.lossyScale.x) : 1f;
            _highlight.localScale = Vector3.one * (shown.RoomRadius * 2.18f / parentScale);

            // A gentle pulse, so the highlight reads as an interface element rather than as part
            // of the body. Alpha is the brightness dial the rim shader actually reads.
            //
            // Hover and selection are told apart by brightness alone, which is weak; the ring is
            // the confirmation, and the panel that opens beside the body is what actually says
            // "this one is selected".
            float pulse = 0.55f + Mathf.Sin(Time.unscaledTime * 2.6f) * 0.12f;
            Color ring = Selected == shown ? CosmoTheme.StrokeActive : CosmoTheme.Accent;
            _highlightMaterial.SetColor("_Color",
                CosmoTheme.WithAlpha(ring, Selected == shown ? pulse + 0.35f : pulse * 0.8f));
        }

        private void OnDestroy()
        {
            if (_rayMaterial != null && Application.isPlaying) Destroy(_rayMaterial);
            if (_highlightMaterial != null && Application.isPlaying) Destroy(_highlightMaterial);
        }
    }
}
