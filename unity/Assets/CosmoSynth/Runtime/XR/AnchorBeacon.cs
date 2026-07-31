using System.Text;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// A small marker at the universe's anchor, plus a status readout, both drawn in world space.
    ///
    /// This exists because of a specific failure: the first Quest build showed passthrough and
    /// nothing else. From inside the headset there was no way to tell whether the app had
    /// crashed, failed to load its data, placed the solar system behind the user, or rendered it
    /// too small to see — all four look identical, and there is no console on the device.
    ///
    /// The beacon is deliberately the dumbest possible thing that can still be seen: an unlit
    /// mesh at a known place, at a fixed size in metres, drawn before any of the simulation
    /// runs. If the beacon is visible and the planets are not, the rig and the anchor are fine
    /// and the problem is in the simulation. If the beacon is not visible either, the problem is
    /// the rig, the anchor placement or passthrough. That single bit of information is the
    /// difference between a diagnosis and another blind rebuild.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Anchor Beacon")]
    public sealed class AnchorBeacon : MonoBehaviour
    {
        [Tooltip("Edge length of the marker cross, in metres.")]
        [SerializeField] private float _sizeMetres = 0.08f;

        [Tooltip("Hide the marker once the simulation has bodies on screen.")]
        [SerializeField] private bool _hideWhenHealthy = true;

        private UniverseSimulation _simulation;
        private XrRig _rig;
        private XrHandInput _input;
        private Transform _marker;
        private TextMesh _status;
        private Material _material;
        private float _elapsed;

        private void Awake()
        {
            _simulation = FindAnyObjectByType<UniverseSimulation>();
            _rig = FindAnyObjectByType<XrRig>();
            _input = FindAnyObjectByType<XrHandInput>();

            BuildMarker();
            BuildStatusText();
        }

        private void BuildMarker()
        {
            var markerObject = new GameObject("Beacon");
            markerObject.transform.SetParent(transform, false);
            _marker = markerObject.transform;

            // Three thin bars forming an axis cross. Unlit, so it shows even with no light in
            // the scene — one of the things that could be wrong.
            _material = CosmoShaders.Material(CosmoShaders.UnlitColor);
            _material.color = new Color(0.4f, 0.95f, 0.75f);

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

        private void BuildStatusText()
        {
            var textObject = new GameObject("Status");
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = new Vector3(0f, _sizeMetres * 2.2f, 0f);

            _status = textObject.AddComponent<TextMesh>();
            // Small. At 0.012 the readout filled the entire view from arm's length and buried
            // the thing it was reporting on.
            _status.characterSize = 0.0022f;
            _status.fontSize = 64;
            _status.anchor = TextAnchor.MiddleCenter;
            _status.alignment = TextAlignment.Center;
            _status.color = new Color(0.75f, 0.95f, 0.9f);
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;

            if (_status != null)
            {
                _status.text = BuildStatusLine();

                // Face the user, so the readout is legible from wherever they are standing.
                if (_rig != null && _rig.Camera != null)
                {
                    Vector3 toCamera = _status.transform.position - _rig.Camera.transform.position;
                    if (toCamera.sqrMagnitude > 1e-6f)
                        _status.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
                }
            }

            if (!_hideWhenHealthy) return;

            // Once the app is clearly working, get out of the way entirely — marker and readout
            // both. The beacon exists to explain an empty view, and leaving it up over a working
            // one just obscures the thing it was there to vouch for.
            bool healthy = _simulation != null && _simulation.Catalog != null && _simulation.Views.Count > 0;
            if (healthy && _elapsed > 8f)
            {
                if (_marker != null && _marker.gameObject.activeSelf) _marker.gameObject.SetActive(false);
                if (_status != null && _status.gameObject.activeSelf) _status.gameObject.SetActive(false);
            }
        }

        private string BuildStatusLine()
        {
            var line = new StringBuilder("COSMOSYNTH\n");

            if (_rig != null)
            {
                line.Append(_rig.XrActive
                    ? $"XR: {UnityEngine.XR.XRSettings.loadedDeviceName} " +
                      $"{UnityEngine.XR.XRSettings.eyeTextureWidth}x{UnityEngine.XR.XRSettings.eyeTextureHeight}\n"
                    : "XR: NOT ACTIVE (flat)\n");
                line.Append($"passthrough: {(_rig.PassthroughEnabled ? "on" : "off")}\n");
            }

            if (_simulation == null || _simulation.Catalog == null)
            {
                line.Append($"catalogue: LOADING ({_elapsed:F0}s)\n");
            }
            else
            {
                line.Append($"catalogue: {_simulation.Catalog.Name} — {_simulation.Catalog.Count} bodies\n");
                line.Append($"views: {_simulation.Views.Count}   scale: {_simulation.Origin.UnitsPerKm:G3} u/km\n");

                // The number that actually explains a blank view: how big the focused body is
                // on screen right now.
                if (_simulation.FocusBody != null)
                {
                    line.Append($"{_simulation.FocusBody.Body.Name}: " +
                                $"{_simulation.FocusBody.ApparentDiameterPixels:F0} px, " +
                                $"{_simulation.FocusBody.Representation}\n");
                }
            }

            if (_input != null) line.Append($"input: {_input.StatusLine}\n");

            return line.ToString();
        }

        private void OnDestroy()
        {
            if (_material != null && Application.isPlaying) Destroy(_material);
        }
    }
}
