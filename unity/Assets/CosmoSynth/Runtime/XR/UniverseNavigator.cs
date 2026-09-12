using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Free flight through the universe, at any scale.
    ///
    /// Travel speed is tied to the distance to the nearest surface rather than being a fixed
    /// number. No constant speed can work here: 10 m/s is unusable between planets and lethal
    /// in a room. Scaling with the surroundings makes the control feel identical everywhere —
    /// the same stick deflection always crosses the visible scene in about the same few
    /// seconds, whether that scene is a landing site or a galactic arm.
    ///
    /// Input arrives through the public methods rather than being read here, so the desktop,
    /// controller and hand-tracking sources can all drive the same navigator without this class
    /// depending on any input package.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CosmoSynth/Universe Navigator")]
    public sealed class UniverseNavigator : MonoBehaviour
    {
        [SerializeField] private UniverseSimulation _simulation;

        [Header("Speed")]
        [Tooltip("Seconds to cross the distance to the nearest surface at full throttle.")]
        [SerializeField] private float _secondsToCrossScene = 4f;

        [Tooltip("Manual speed multiplier, applied on top of the automatic scaling.")]
        [SerializeField] private float _speedMultiplier = 1f;

        [Header("Look")]
        [SerializeField] private float _lookSensitivity = 0.15f;
        [SerializeField] private bool _invertPitch;

        [Header("Target lock")]
        [Tooltip("Fraction of the remaining distance covered per second when travelling to a target.")]
        [SerializeField] private float _approachRate = 1.2f;

        [Tooltip("Stop this many body radii from the target's centre.")]
        [SerializeField] private float _arrivalRadii = 4f;

        private Double3 _absolutePositionKm;
        private float _yaw;
        private float _pitch;
        private CelestialBodyView _travelTarget;
        private bool _initialised;

        /// <summary>The viewer's absolute position, km. Authoritative; the transform follows it.</summary>
        public Double3 AbsolutePositionKm
        {
            get => _absolutePositionKm;
            set
            {
                _absolutePositionKm = value;
                _initialised = true;
            }
        }

        /// <summary>Current automatic speed, km/s, before the manual multiplier.</summary>
        public double CurrentSpeedKmPerSec { get; private set; }

        /// <summary>Distance to the nearest body's surface, km.</summary>
        public double NearestSurfaceKm { get; private set; } = double.PositiveInfinity;

        /// <summary>The nearest body, updated each frame.</summary>
        public CelestialBodyView NearestBody { get; private set; }

        /// <summary>The body being travelled to, or null.</summary>
        public CelestialBodyView TravelTarget => _travelTarget;

        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set => _speedMultiplier = Mathf.Clamp(value, 0.001f, 1000f);
        }

        private Vector3 _moveInput;
        private Vector2 _lookInput;

        /// <summary>Movement in the viewer's local axes, each component in [-1,1].</summary>
        public void Move(Vector3 localAxes) => _moveInput = Vector3.ClampMagnitude(localAxes, 1f);

        /// <summary>Look delta in degrees-ish units, scaled by the sensitivity setting.</summary>
        public void Look(Vector2 delta) => _lookInput = delta;

        /// <summary>Begin travelling to a body. Cancelled by any manual movement.</summary>
        public void TravelTo(CelestialBodyView target) => _travelTarget = target;

        public void CancelTravel() => _travelTarget = null;

        private void Awake()
        {
            if (_simulation == null) _simulation = FindAnyObjectByType<UniverseSimulation>();
        }

        private void Start()
        {
            if (_initialised || _simulation == null) return;

            // Open somewhere the scene is legible: a few planetary radii from the first body,
            // rather than at the barycentre where everything is either inside you or invisible.
            if (_simulation.Catalog != null && _simulation.Catalog.RootIds.Count > 0)
            {
                var root = _simulation.Views[_simulation.Catalog.RootIds[0]];
                double radius = root.Body.EquatorialRadiusKm ?? 1e6;
                _absolutePositionKm = root.AbsolutePositionKm + new Double3(0, -radius * 40.0, radius * 8.0);
                _initialised = true;
                LookAt(root);
            }
        }

        private void Update()
        {
            if (_simulation == null) return;

            NearestBody = _simulation.NearestBody(_absolutePositionKm, out double surfaceKm);
            NearestSurfaceKm = surfaceKm;

            CurrentSpeedKmPerSec = ScaleSpace.NavigationSpeed(surfaceKm, _secondsToCrossScene);

            if (_travelTarget != null) UpdateTravel(Time.unscaledDeltaTime);
            else UpdateManual(Time.unscaledDeltaTime);

            ApplyToTransform();
        }

        private void UpdateManual(float deltaTime)
        {
            if (_lookInput.sqrMagnitude > 0f)
            {
                _yaw += _lookInput.x * _lookSensitivity;
                _pitch += (_invertPitch ? _lookInput.y : -_lookInput.y) * _lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, -89.9f, 89.9f);
                _lookInput = Vector2.zero;
            }

            if (_moveInput.sqrMagnitude <= 0f) return;

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 worldDirection = rotation * _moveInput;

            double speed = CurrentSpeedKmPerSec * _speedMultiplier;

            // Through the anchor into the universe's own space, THEN into ICRF. UnmapDirection
            // expects a universe-host-LOCAL direction; handing it a world one steers correctly
            // only while the universe is unrotated, and drifts off-axis as soon as the anchor is
            // turned — which the two-hand gesture and the anchor placement both do. XrLocomotion
            // already does this; the two navigators must not disagree about what a direction is.
            Vector3 localDirection = _simulation.transform.InverseTransformDirection(worldDirection);
            Double3 stepKm = _simulation.Origin.UnmapDirection(localDirection) * (speed * deltaTime);

            _absolutePositionKm += stepKm;
            _initialised = true;
        }

        private void UpdateTravel(float deltaTime)
        {
            if (_travelTarget == null) return;

            Double3 toTarget = _travelTarget.AbsolutePositionKm - _absolutePositionKm;
            double distance = toTarget.Magnitude;

            double radius = _travelTarget.Body.EquatorialRadiusKm ?? 1.0;
            double arrival = radius * _arrivalRadii;

            if (distance <= arrival)
            {
                _travelTarget = null;
                return;
            }

            // Exponential approach: cover a fixed fraction of the remaining gap each second.
            // This is what makes an interstellar hop and a hop across a room take a similar
            // time, and it decelerates into arrival without needing a separate braking phase.
            double remaining = distance - arrival;
            double step = remaining * (1.0 - System.Math.Exp(-_approachRate * deltaTime));

            _absolutePositionKm += toTarget.Normalized * step;
            _initialised = true;

            LookAt(_travelTarget);
        }

        /// <summary>Point the viewer at a body.</summary>
        public void LookAt(CelestialBodyView target)
        {
            if (target == null) return;

            Double3 toTarget = target.AbsolutePositionKm - _absolutePositionKm;
            if (toTarget.SqrMagnitude <= 0.0) return;

            // MapDirection answers in the universe host's LOCAL space; a LookRotation axis is a
            // world one. The same mismatch as the move vector above, in the other direction.
            Vector3 direction = _simulation.transform.TransformDirection(
                _simulation.Origin.MapDirection(toTarget.Normalized));
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 euler = rotation.eulerAngles;

            _yaw = euler.y;
            _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        private void ApplyToTransform()
        {
            FloatingOrigin origin = _simulation.Origin;

            // The origin is anchored to the viewer, so the rendered camera sits at (or very
            // near) zero and never accumulates float error no matter how far it has travelled.
            origin.SetOrigin(_absolutePositionKm);

            // Through the universe host, not straight from the mapping. ToRender answers in the
            // host's LOCAL space; assigning it to a world position ignores the anchor entirely,
            // which at tabletop scale is metres of error in the room.
            transform.position = _simulation.transform.TransformPoint(origin.ToRender(_absolutePositionKm));
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}
