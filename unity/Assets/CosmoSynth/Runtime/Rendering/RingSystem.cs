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
    [DefaultExecutionOrder(60)]
    [AddComponentMenu("")]
    public sealed class RingSystem : MonoBehaviour
    {
        private CelestialBodyView _body;
        private MeshRenderer _renderer;

        private static readonly int LightDirId = Shader.PropertyToID("_LightDirWS");
        private static readonly int LightColorId = Shader.PropertyToID("_LightColor");
        private static readonly int AmbientId = Shader.PropertyToID("_Ambient");
        private static readonly int ParentCentreId = Shader.PropertyToID("_ParentCentreWS");
        private static readonly int PerVertexCompressId = Shader.PropertyToID("_PerVertexCompress");

        public void Track(CelestialBodyView body) => _body = body;

        private void Awake() => Resolve();

        /// <summary>
        /// Find the renderer, late if necessary.
        ///
        /// Awake runs the instant AddComponent returns, and SkyAndRings.BuildRings adds this
        /// component BEFORE the MeshRenderer, so resolving only in Awake left _renderer null
        /// for the life of the object. Every guard below is written as `_renderer != null`, so
        /// the effect was silent and threefold: the rings were never switched off when their
        /// planet stopped being drawn, their transform kept whatever scale it last had, and
        /// _LightDirWS and _ParentCentreWS were never written, so the analytic planet shadow
        /// was cast from the material defaults. At the Local Group scale that left four ring
        /// discs of up to 8.5 m enabled a few picometres from the eye (G1c capture,
        /// 2026-09-12). Resolving lazily fixes every caller, in any construction order,
        /// instead of fixing the one caller that happens to be wrong today.
        /// </summary>
        private bool Resolve()
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            return _renderer != null;
        }

        private void LateUpdate()
        {
            if (_body == null || !Resolve()) return;

            // Ask whether the planet is DRAWN, every frame, before reading its radius.
            //
            // RenderedRadius survives a body being switched off: it keeps whatever the last
            // update left, and for a body that has handed over to the point path that is the
            // point's size, not the planet's. Rings sized from a stale radius are what put a
            // translucent band across half the sky in every screenshot for weeks. Re-checking
            // every frame matters as much as checking at all — a body that was drawn when the
            // ring was created stops being drawn the moment you zoom out.
            bool visible = _body.IsGeometryDrawn;
            if (_renderer != null && _renderer.enabled != visible) _renderer.enabled = visible;
            if (!visible) return;

            // RenderedRadius already accounts for the render scale, presentation compression and
            // the group similarity, so the rings inherit all of it for free.
            float radius = _body.RenderedRadius;
            Transform parent = transform.parent;
            float parentScale = parent != null ? Mathf.Max(1e-9f, parent.lossyScale.x) : 1f;

            transform.localScale = Vector3.one * (radius / parentScale);

            // The planet shadow, from the planet as DRAWN.
            //
            // CosmoSynth/Ring casts it with an analytic ray-sphere test toward the host star, and
            // the radius it tests against has to be the drawn one: every rigid group renders at
            // its own similarity, so the physical radius would cast a shadow the wrong size at
            // every scale but one. Both come from the view, which is the only thing that knows
            // either — and which asks IsGeometryDrawn before answering, above.
            Material material = _renderer != null ? _renderer.sharedMaterial : null;
            if (material == null) return;

            material.SetVector(LightDirId, _body.LightDirectionWS);
            material.SetColor(LightColorId, _body.LightColour);
            material.SetColor(AmbientId, CosmoTheme.AmbientFill);
            Vector3 centre = _body.transform.position;
            material.SetVector(ParentCentreId, new Vector4(centre.x, centre.y, centre.z, radius));
            material.SetFloat(PerVertexCompressId, _body.PerVertexCompressed ? 1f : 0f);
        }
    }
}
