using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Draws a body that has been pinned to the sky sphere as a soft dot facing the camera.
    ///
    /// A sphere is the wrong primitive for something two pixels across. Unity's primitive is a
    /// twenty-segment UV sphere, so at that size its silhouette is a handful of flat facets —
    /// a blocky speck — and, being a lit surface, it is nearly black anyway once the only light
    /// in the scene is light years behind it. Every catalogue star came out as a dark angular
    /// tile.
    ///
    /// A camera-facing quad with the shared point-spread solves all of it at once: round at any
    /// size, unlit so brightness comes from the star's own magnitude rather than from whatever
    /// happens to be illuminating it, and one quad instead of 500 triangles.
    ///
    /// The billboard is rebuilt from the camera every frame rather than being parented to it,
    /// because the body's transform belongs to the simulation and must keep tracking the sky
    /// sphere position the view assigns it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class StarPointBillboard : MonoBehaviour
    {
        private static Mesh _quad;

        private MeshRenderer _renderer;
        private Material _material;
        private Transform _transform;

        /// <summary>Build the quad and its material. Colour is the star's own display colour.</summary>
        public void Initialise(Color colour)
        {
            _transform = transform;

            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = Quad();

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            // Sorted with the transparent queue but drawn before the planets, since a star on
            // the sky sphere is behind everything by construction.
            _renderer.sharedMaterial = _material = StarSprite.CreateMaterial();
            _material.renderQueue = SkyQueue;
            _material.color = colour;

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

        /// <summary>Show or hide, and set the on-screen diameter in world units.</summary>
        public void SetVisible(bool visible, float diameter)
        {
            if (_renderer == null) return;
            _renderer.enabled = visible;
            if (!visible) return;

            _transform.localScale = Vector3.one * diameter;

            Camera camera = Camera.main;
            if (camera == null) return;

            // Face the camera squarely. Using the camera's rotation rather than a look-at keeps
            // every star on screen parallel to the view plane, which is what stops them
            // shearing into ellipses toward the edges of a wide field of view.
            _transform.rotation = camera.transform.rotation;
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
            if (_material != null) _material.color = colour;
        }

        private void OnDestroy()
        {
            if (_material != null && Application.isPlaying) Destroy(_material);
        }

        /// <summary>A unit quad in the XY plane, shared by every star point.</summary>
        private static Mesh Quad()
        {
            if (_quad != null) return _quad;

            _quad = new Mesh { name = "StarPointQuad" };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            _quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _quad.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };

            // Generous bounds: the quad is re-oriented every frame, and Unity culls against the
            // bounds it was built with. Too tight and stars pop out at the edge of the view.
            _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return _quad;
        }
    }
}
