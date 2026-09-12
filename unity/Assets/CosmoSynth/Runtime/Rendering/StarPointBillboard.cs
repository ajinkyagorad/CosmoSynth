using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Draws a body that is too small to draw as geometry as a soft dot.
    ///
    /// A sphere is the wrong primitive for something two pixels across. Unity's primitive is a
    /// twenty-segment UV sphere, so at that size its silhouette is a handful of flat facets —
    /// a blocky speck — and, being a lit surface, it is nearly black anyway once the only light
    /// in the scene is light years behind it. Every catalogue star came out as a dark angular
    /// tile.
    ///
    /// The quad is now billboarded by <c>CosmoSynth/Point</c> in CLIP space rather than by this
    /// component in world space, which is what makes it correct in a headset: a CPU billboard is
    /// built from one camera transform and is therefore the same quad in both eyes, while the
    /// shader expands the corners against each eye's own projection. The Gaussian profile is
    /// analytic, so there is no sprite texture to sample and nothing for the build step to strip.
    ///
    /// The four vertices all sit at the body's own origin; only <c>data.y</c> (the corner index)
    /// distinguishes them. All four carry the same <c>data.w</c>, which R14 requires: the shader's
    /// cull writes an off-screen clip position, and that CLIPS a primitive rather than discarding
    /// it, so a quad whose vertices disagreed would rasterise as a wedge rather than as nothing.
    ///
    /// The colour rides in the VERTEX stream, not on the material, because that is where
    /// CosmoSynth/Point reads it and because contract 7.4 decodes vertex colours exactly once in
    /// the shader. A material tint would be linearised by Unity on the way in and then decoded
    /// again by the same shader line — a second sRGB curve on every dot in the sky. That costs a
    /// four-vertex mesh per body rather than a shared one, which is four floats of geometry.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class StarPointBillboard : MonoBehaviour
    {
        private MeshRenderer _renderer;
        private Material _material;
        private Mesh _mesh;
        private readonly Color[] _colours = new Color[4];
        private Color _colour = Color.clear;   // gate-justified: a sentinel meaning "never set", not a palette choice

        private static readonly int SizeDmmId = Shader.PropertyToID("_SizeDmm");
        private static readonly int MagWindowId = Shader.PropertyToID("_MagWindow");
        private static readonly int PxBoundsId = Shader.PropertyToID("_PxBounds");
        private static readonly int SpaceId = Shader.PropertyToID("_Space");

        /// <summary>
        /// The magnitude window this billboard drives the shader with.
        ///
        /// The CPU has already decided this dot's angular size and its colour — a body drawn as a
        /// point is at the visibility floor, and a star's size comes from <see cref="StarLook"/>
        /// against its apparent magnitude, which is arithmetic that belongs in Core rather than in
        /// a shader constant. So the shader's own magnitude law is short-circuited: the window is
        /// (1, 0) and every vertex carries magnitude 0, which puts the shader's brightness term b
        /// at exactly 1. At b = 1 the colour passes through untinted and undimmed and the size is
        /// <c>_SizeDmm.y</c>, which this component writes each frame. One law, evaluated once, on
        /// the side that knows the distance.
        /// </summary>
        private static readonly Vector4 PassThroughWindow = new Vector4(1f, 0f, 0f, 0f);

        /// <summary>Build the quad and its material. Colour is the star's own display colour.</summary>
        public void Initialise(Color colour)
        {
            _mesh = BuildQuad();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            _material = CosmoShaders.Material(CosmoShaders.Point);
            if (_material == null)
            {
                // No shader, no dot. Degrading beats an exception out of Awake; CosmoShaders has
                // already logged which shader is missing.
                _renderer.enabled = false;
                return;
            }

            _material.SetFloat(SpaceId, 1f);                    // object space: the CPU places it
            _material.SetVector(MagWindowId, PassThroughWindow);
            // The floor matches CelestialBodyView's own 2.5 px floor, so the two cannot disagree
            // about how small a dot may get. The ceiling is inert here: the largest a point-drawn
            // body ever asks for is StarLook's 9 dmm, about 10 px.
            _material.SetVector(PxBoundsId, new Vector4(2.5f, 64f, 0f, 0f));
            _renderer.sharedMaterial = _material;

            SetColour(colour);
            _renderer.enabled = false;
        }

        /// <summary>Behind everything: a star on the sky sphere is, by construction.</summary>
        public const int SkyQueue = 1100;

        /// <summary>
        /// After opaque geometry, so the dot is depth-tested against it.
        ///
        /// The sky queue is wrong for anything that is not on the sky sphere. This same billboard
        /// is now also the marker a spacecraft degrades to when its mesh is too small to read,
        /// and that marker sits a few hundred kilometres above a planet that fills the view —
        /// drawn before the opaque pass it would be painted over by the planet every time the
        /// spacecraft was in front of it, which is most of the time and precisely when it is
        /// wanted. In the transparent queue it is occluded when it is genuinely behind the planet
        /// and visible when it is not, which is the whole of the correct behaviour.
        /// </summary>
        public const int MarkerQueue = 3050;

        /// <summary>Choose which of the two this billboard is being used as.</summary>
        public void SetRenderQueue(int queue)
        {
            if (_material != null && _material.renderQueue != queue) _material.renderQueue = queue;
        }

        /// <summary>
        /// Show or hide, and set the on-screen ANGULAR diameter in radians.
        ///
        /// Angular, not a world diameter: the shader expands the quad against this eye's
        /// projection, so a world size would have to be converted back out of a distance the
        /// shader does not have. The caller already computed an angle and then multiplied it by
        /// the distance to get a scale; that multiplication is now gone on both sides.
        /// </summary>
        public void SetVisible(bool visible, float angleRadians)
        {
            if (_renderer == null || _material == null) return;
            _renderer.enabled = visible;
            if (!visible) return;

            float dmm = Mathf.Max(angleRadians, 0f) * 1000f;
            _material.SetVector(SizeDmmId, new Vector4(dmm, dmm, 0f, 0f));
        }

        /// <summary>
        /// Tint, carrying both the star's temperature and its magnitude.
        ///
        /// Both, because under an additive blend the colour is the only channel that reaches the
        /// screen — so brightness has to be baked into the RGB rather than expressed as alpha.
        /// <see cref="StarLook"/> owns that arithmetic and the procedural field reads the same
        /// table, which is what stops the two star populations on one sky looking like different
        /// kinds of object. They did: measured off a frame, catalogue stars peaked at full
        /// brightness and varied only in diameter while field stars were pinned at a quarter
        /// brightness and varied only in alpha, a channel that was doing nothing.
        /// </summary>
        public void SetColour(Color colour)
        {
            if (_mesh == null || colour == _colour) return;
            _colour = colour;
            for (int i = 0; i < 4; i++) _colours[i] = colour;
            _mesh.colors = _colours;
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying) return;
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
        }

        /// <summary>
        /// Four coincident vertices at the object's own origin.
        ///
        /// CosmoSynth/Point reads TEXCOORD0 as (magnitude, corner 0..3, shape, hidden) and spreads
        /// the corners itself. Magnitude is 0 and shape is 0 (the round core) for every vertex; see
        /// <see cref="PassThroughWindow"/> for why 0 is the pass-through value.
        /// </summary>
        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "StarPointQuad" };
            mesh.vertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };
            mesh.SetUVs(0, new System.Collections.Generic.List<Vector4>
            {
                new Vector4(0f, 0f, 0f, 0f),
                new Vector4(0f, 1f, 0f, 0f),
                new Vector4(0f, 2f, 0f, 0f),
                new Vector4(0f, 3f, 0f, 0f),
            });
            // Corner bits are (x = ci & 1, y = ci & 2), so 0,1,3,2 walks the perimeter and the two
            // triangles cover the quad rather than folding across it.
            mesh.triangles = new[] { 0, 1, 3, 0, 3, 2 };

            // The vertices are coincident, so the mesh's own bounds are a point and Unity would
            // cull the renderer at almost every angle. One metre of slack around the body is far
            // more than the shader's widest expansion (64 px) can ever need, and small enough that
            // the play-mode `far` assertion still measures something meaningful.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }
    }
}
