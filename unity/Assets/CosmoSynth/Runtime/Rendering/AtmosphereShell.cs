using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Keeps an atmospheric shell sized to the body it belongs to, and feeds the scattering
    /// shader the world-space geometry it needs.
    ///
    /// The body's drawn radius changes constantly — with the render scale as the user resizes
    /// the universe, and with the minimum-visible-size clamp as it recedes — so a scale set once
    /// at construction is wrong within a frame. Tracking it here is a few multiplications and
    /// removes a whole class of "why is that planet wearing a halo the size of a room" bug.
    ///
    /// The scattering shader raymarches in <i>world</i> space, so it needs the planet centre,
    /// both radii and the direction to the star in the same units the camera lives in. All four
    /// change every frame here, which is why they are pushed from LateUpdate rather than set
    /// once on the material.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class AtmosphereShell : MonoBehaviour
    {
        private static readonly int PlanetRadiusId = Shader.PropertyToID("_PlanetRadius");
        private static readonly int AtmosphereRadiusId = Shader.PropertyToID("_AtmosphereRadius");
        private static readonly int PlanetCenterId = Shader.PropertyToID("_PlanetCenter");
        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        private static readonly int RayleighCoeffId = Shader.PropertyToID("_RayleighCoeff");
        private static readonly int MieCoeffId = Shader.PropertyToID("_MieCoeff");

        private CelestialBodyView _body;
        private Transform _star;
        private Material _material;
        private float _relativeScale = 1.02f;

        /// <summary>
        /// Vertical optical depth of the whole atmosphere, per RGB channel — dimensionless.
        ///
        /// This is the form the physics survives a changing render scale in. The catalogue stores
        /// scattering per kilometre, but the shader marches in world units, and a world unit is
        /// worth anywhere from a metre to a light year here. Multiplying the coefficients by the
        /// scale height collapses that out: tau is a property of the air column itself, roughly
        /// 0.05 / 0.12 / 0.28 for Earth, and stays true at every zoom. It is turned back into a
        /// per-world-unit coefficient each frame, once the shell's actual thickness is known.
        /// </summary>
        private Vector3 _opticalDepth = new Vector3(0.049f, 0.115f, 0.281f);
        private float _mieOpticalDepth = 0.02f;

        /// <summary>Fraction of shell thickness the density falls by 1/e over. Matches _ScaleHeight.</summary>
        private float _densityFalloff = 0.25f;

        public void Track(CelestialBodyView body, float relativeScale)
        {
            _body = body;
            _relativeScale = Mathf.Max(1.001f, relativeScale);
        }

        /// <summary>Give the shell the scattering material and the air column it represents.</summary>
        public void Scatter(Material material, AtmosphereDefinition atmosphere, Transform star,
            float densityFalloff)
        {
            _material = material;
            _star = star;
            _densityFalloff = Mathf.Clamp(densityFalloff, 0.02f, 1f);

            if (atmosphere == null) return;

            Double3 rayleigh = atmosphere.RayleighScatteringPerKm;
            double height = System.Math.Max(1e-6, atmosphere.ScaleHeightKm);

            _opticalDepth = new Vector3(
                (float)(rayleigh.x * height), (float)(rayleigh.y * height), (float)(rayleigh.z * height));
            _mieOpticalDepth = (float)(atmosphere.MieScatteringPerKm * height);

            // A genuinely thick atmosphere — Venus at 92 bar, tau in the hundreds — marches to
            // solid white and loses the limb entirely. Clamping keeps it opaque-looking without
            // saturating every channel to the same value, so Venus stays yellow-grey.
            _opticalDepth = Vector3.Min(_opticalDepth, new Vector3(4f, 4f, 4f));
            _mieOpticalDepth = Mathf.Min(_mieOpticalDepth, 4f);
        }

        /// <summary>Diagnostics: what the shader was last told, in world units.</summary>
        public string Describe() => _body == null ? "(untracked)"
            : $"{_body.Body.Id}: planet r={_body.RenderedRadius:G4} shell r={_body.RenderedRadius * _relativeScale:G4}"
              + $" ratio={_relativeScale:F4} worldScale={transform.lossyScale.x:G4}"
              + $" tau=({_opticalDepth.x:F3},{_opticalDepth.y:F3},{_opticalDepth.z:F3})";

        private void LateUpdate()
        {
            if (_body == null) return;

            // RenderedRadius is the sphere's world radius, so the shell is expressed in the same
            // terms and inherits the clamp and the render scale automatically.
            float planetRadius = _body.RenderedRadius;
            float shellRadius = planetRadius * _relativeScale;

            transform.position = _body.transform.position;
            transform.localScale = Vector3.one * (shellRadius * 2f)
                                   / Mathf.Max(1e-6f, transform.parent.lossyScale.x);

            if (_material == null) return;

            Vector3 centre = transform.position;
            _material.SetFloat(PlanetRadiusId, planetRadius);
            _material.SetFloat(AtmosphereRadiusId, shellRadius);
            _material.SetVector(PlanetCenterId, centre);

            // Direction to the star. Falling back to the camera's forward would light every
            // atmosphere from the viewer, which is exactly the flat, phase-less look the point
            // light was introduced to get rid of.
            Vector3 toStar = _star != null ? _star.position - centre : Vector3.up;
            if (toStar.sqrMagnitude > 1e-12f) _material.SetVector(SunDirectionId, toStar.normalized);

            // Convert the column's optical depth into a per-world-unit coefficient.
            //
            // The shader integrates exp(-altitude/h) over the shell, where altitude is normalised
            // to 0..1, so a vertical path accumulates roughly thickness * h. Dividing tau by that
            // gives back exactly the depth the air really has, whatever the shell measures this
            // frame — which is what keeps Earth's limb the same blue whether the planet is a
            // marble on a table or fills the whole view.
            float thickness = Mathf.Max(1e-9f, shellRadius - planetRadius);
            float normalise = 1f / (thickness * _densityFalloff);

            _material.SetVector(RayleighCoeffId, _opticalDepth * normalise);
            _material.SetFloat(MieCoeffId, _mieOpticalDepth * normalise);
        }
    }
}
