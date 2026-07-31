using CosmoSynth.Core;
using UnityEngine;
using UnityEngine.XR;

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
        private XrHandInput _input;
        private SpatialPanel _panel;
        private LineRenderer _ray;
        private Material _rayMaterial;
        private Transform _highlight;
        private Material _highlightMaterial;

        private CelestialBodyView _hovered;
        private bool _triggerHeld;

        /// <summary>The currently selected body, or null.</summary>
        public CelestialBodyView Selected { get; private set; }

        private void Awake()
        {
            _simulation = FindAnyObjectByType<UniverseSimulation>();
            _rig = FindAnyObjectByType<XrRig>();
            _input = FindAnyObjectByType<XrHandInput>();

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

            _rayMaterial = CosmoShaders.Material(CosmoShaders.UnlitColor);
            _rayMaterial.color = new Color(0.45f, 0.85f, 1f, 0.8f);
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
            _highlightMaterial.SetColor("_Color", new Color(0.35f, 0.75f, 1f, 0.6f));
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
            _hovered = Pick(origin, direction);

            float length = _hovered != null
                ? Vector3.Distance(origin, _hovered.transform.position)
                : _rayLengthMetres;

            _ray.SetPosition(0, origin);
            _ray.SetPosition(1, origin + direction * length);
            _rayMaterial.color = _hovered != null
                ? new Color(0.55f, 1f, 0.75f, 0.9f)
                : new Color(0.45f, 0.85f, 1f, 0.5f);

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

            if (TryGetDevicePose(InputDeviceCharacteristics.Right, out origin, out Quaternion rotation) ||
                TryGetDevicePose(InputDeviceCharacteristics.Left, out origin, out rotation))
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

        private bool TryGetDevicePose(InputDeviceCharacteristics side, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller | side, devices);

            if (devices.Count == 0 || !devices[0].isValid) return false;
            if (!devices[0].TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 local)) return false;
            devices[0].TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRotation);

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

                Vector3 toBody = view.transform.position - origin;
                float along = Vector3.Dot(toBody, direction);
                if (along <= 0f) continue;                       // behind the pointer

                float perpendicular = (toBody - direction * along).magnitude;

                // The drawn radius, which is what the user is aiming at — not the physical one.
                float radius = view.RenderedRadius + along * forgiveness;
                if (perpendicular > radius) continue;

                if (along < bestDistance)
                {
                    bestDistance = along;
                    best = view;
                }
            }

            return best;
        }

        private void PollSelect()
        {
            bool pressed = IsSelectPressed();
            if (pressed && !_triggerHeld)
            {
                // Clicking empty space deselects, which is the only way to dismiss the panel.
                Selected = _hovered;
                _panel.Show(Selected);

                if (Selected != null)
                {
                    _simulation.SetFocus(Selected.Body.Id);
                    Debug.Log($"[CosmoSynth] selected {Selected.Body.Name}");
                }
            }
            _triggerHeld = pressed;
        }

        private bool IsSelectPressed()
        {
            // Trigger on either controller.
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller, devices);

            foreach (InputDevice device in devices)
            {
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger) && trigger) return true;
            }

            // With hands, a pinch selects — but only when the manipulator is not already using it
            // to hold the universe, or every grab would also re-select whatever it passed over.
            return _input != null && _input.Active == XrHandInput.Modality.Hands && _input.SelectPinch;
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
            _highlight.localScale = Vector3.one * (shown.RenderedRadius * 2.18f / parentScale);

            // A gentle pulse, so the highlight reads as an interface element rather than as part
            // of the body.
            // Alpha is the brightness dial the rim shader actually reads.
            float pulse = 0.55f + Mathf.Sin(Time.unscaledTime * 2.6f) * 0.12f;
            _highlightMaterial.SetColor("_Color",
                new Color(0.35f, 0.75f, 1f, Selected == shown ? pulse + 0.35f : pulse));
        }

        private void OnDestroy()
        {
            if (_rayMaterial != null && Application.isPlaying) Destroy(_rayMaterial);
            if (_highlightMaterial != null && Application.isPlaying) Destroy(_highlightMaterial);
        }
    }
}
