using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Keeps a planet's rings sized to the planet.
    ///
    /// The mesh is built once in units of the body's own radius — an inner edge at 1.04 and an
    /// outer edge at 2.27 for Saturn — and this component scales the transform by whatever the
    /// body is currently drawn at. That is the only way it can be right at every zoom level.
    ///
    /// Baking the render scale into the vertices instead, which is what this replaces, produces
    /// a specific and very visible bug: the rings are correct at the scale they happened to be
    /// created at and wrong everywhere else, so zooming out shrinks the planets while the rings
    /// stay put and end up larger than the entire solar system. Exactly the same mistake as
    /// sizing an atmosphere against a unit-scale parent, and worth fixing the same way.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class RingSystem : MonoBehaviour
    {
        private CelestialBodyView _body;

        public void Track(CelestialBodyView body) => _body = body;

        private void LateUpdate()
        {
            if (_body == null) return;

            // RenderedRadius already accounts for the render scale, presentation compression and
            // the minimum-visible-size clamp, so the rings inherit all of it for free.
            float radius = _body.RenderedRadius;
            Transform parent = transform.parent;
            float parentScale = parent != null ? Mathf.Max(1e-9f, parent.lossyScale.x) : 1f;

            transform.localScale = Vector3.one * (radius / parentScale);
        }
    }
}
