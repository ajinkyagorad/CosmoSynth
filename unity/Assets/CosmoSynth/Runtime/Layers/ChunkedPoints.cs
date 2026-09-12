using System;
using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The machinery every point population shares: split into chunks, one mesh and one material
    /// per chunk, and per frame hand each chunk the eye's position in its own local units.
    /// CONTRACT §3.4.
    ///
    /// Not a MonoBehaviour — it is owned by one, and a layer that needed two of these (stars in
    /// three-dimensional mode and stars on the sky shell) would otherwise need two components.
    ///
    /// The chunking exists for precision, and the culling comes free with it: a chunk is a
    /// bounding sphere, so "is any of this in view" is one dot product and one arcsine, sixty-four
    /// times. Unity's own frustum culling cannot help here at all, because every vertex is moved
    /// by the vertex shader and the mesh bounds are therefore meaningless.
    /// </summary>
    public sealed class ChunkedPoints
    {
        private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<int[]> _pointOfSprite = new List<int[]>();
        private readonly Transform _parent;
        private readonly string _name;

        private PointChunk[] _chunks = new PointChunk[0];
        private bool _enabled;

        private static readonly int EyeInChunkId = Shader.PropertyToID("_EyeInChunk");
        private static readonly int UnitsId = Shader.PropertyToID("_Units");
        private static readonly int LayerToWorldId = Shader.PropertyToID("_LayerToWorld");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int SpaceId = Shader.PropertyToID("_Space");
        private static readonly int LookId = Shader.PropertyToID("_Look");
        private static readonly int MagWindowId = Shader.PropertyToID("_MagWindow");
        private static readonly int SizeDmmId = Shader.PropertyToID("_SizeDmm");
        private static readonly int PxBoundsId = Shader.PropertyToID("_PxBounds");

        public ChunkedPoints(Transform parent, string name)
        {
            _parent = parent;
            _name = name;
        }

        /// <summary>Kilometres per storage unit — pc for stars, kpc for the Local Volume, Mpc for LSS.</summary>
        public double KmPerUnit { get; set; } = 1.0;

        /// <summary>Parsecs per storage unit, for the absolute-to-apparent magnitude step in the shader.</summary>
        public double ParsecsPerUnit { get; set; } = 1.0;

        /// <summary>Chunks whose cone test passed this frame. The play-mode budget assertion reads it.</summary>
        public int DrawCount { get; private set; }

        /// <summary>Vertices in the chunks drawn this frame.</summary>
        public int VertexCount { get; private set; }

        public int PointCount { get; private set; }

        public IReadOnlyList<PointChunk> Chunks => _chunks;

        public bool IsBuilt => _chunks.Length > 0;

        /// <summary>
        /// Split the population and build its meshes. <paramref name="describe"/> is asked for
        /// everything about one point except where it is: colour, magnitude, shape, hidden flag.
        /// Sprites with a shape other than Core are emitted as extra quads on the same point.
        /// </summary>
        public void Build(IReadOnlyList<Double3> positionsUnits,
            Func<int, IReadOnlyList<PointMesh.Sprite>> describe,
            int maxPerChunk = 4096, int maxChunks = 64)
        {
            Clear();
            if (positionsUnits == null || positionsUnits.Count == 0) return;

            Double3[] positions = new Double3[positionsUnits.Count];
            for (int i = 0; i < positions.Length; i++) positions[i] = positionsUnits[i];

            _chunks = PointChunks.Build(positions, maxPerChunk, maxChunks);
            PointCount = positions.Length;

            List<PointMesh.Sprite> sprites = new List<PointMesh.Sprite>(maxPerChunk * 2);
            List<int> pointOfSprite = new List<int>(maxPerChunk * 2);
            for (int c = 0; c < _chunks.Length; c++)
            {
                PointChunk chunk = _chunks[c];
                sprites.Clear();
                pointOfSprite.Clear();
                for (int i = 0; i < chunk.Count; i++)
                {
                    Vector3 offset = new Vector3(
                        chunk.OffsetsXyz[i * 3 + 0],
                        chunk.OffsetsXyz[i * 3 + 1],
                        chunk.OffsetsXyz[i * 3 + 2]);

                    IReadOnlyList<PointMesh.Sprite> parts = describe(chunk.Indices[i]);
                    if (parts == null) continue;
                    for (int p = 0; p < parts.Count; p++)
                    {
                        PointMesh.Sprite s = parts[p];
                        s.Offset = offset;
                        sprites.Add(s);
                        pointOfSprite.Add(chunk.Indices[i]);
                    }
                }
                _meshes.Add(PointMesh.Build(sprites, _name + " chunk " + c));
                _pointOfSprite.Add(pointOfSprite.ToArray());
            }
        }

        /// <summary>
        /// Create the renderers. Separate from <see cref="Build"/> because the mesh data can be
        /// prepared before the shader exists, and because a rebuild reuses the GameObjects.
        /// </summary>
        public void CreateRenderers(Material template, int renderQueue)
        {
            for (int c = 0; c < _meshes.Count; c++)
            {
                GameObject go = new GameObject(_name + " " + c);
                go.transform.SetParent(_parent, false);

                MeshFilter filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = _meshes[c];

                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                Material material = new Material(template) { renderQueue = renderQueue };
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.enabled = false;

                _renderers.Add(renderer);
                _materials.Add(material);
            }
        }

        /// <summary>The star look, written once at build time: the two bounds are not tuning knobs.</summary>
        public void ConfigureLook(float minPixels, float maxPixels, float faintMag, float brightMag,
            float faintDmm, float brightDmm, float space)
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                _materials[i].SetVector(PxBoundsId, new Vector4(minPixels, maxPixels, 0f, 0f));
                _materials[i].SetVector(MagWindowId, new Vector4(faintMag, brightMag, 0f, 0f));
                _materials[i].SetVector(SizeDmmId, new Vector4(faintDmm, brightDmm, 0f, 0f));
                _materials[i].SetFloat(SpaceId, space);
            }
        }

        public void SetEnabled(bool on)
        {
            _enabled = on;
            if (on) return;
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i] != null) _renderers[i].enabled = false;
            DrawCount = 0;
            VertexCount = 0;
        }

        /// <summary>
        /// Per frame: publish the eye in each chunk's units, the layer rotation, the exposure and
        /// the fade, and enable only the chunks whose bounding sphere meets the view cone.
        /// </summary>
        public void Apply(in FrameContext ctx, Camera camera, float fade, float exposure,
            float faintestBrightness, Vector2 nearFadeUnits)
        {
            DrawCount = 0;
            VertexCount = 0;
            if (!_enabled || _chunks.Length == 0) return;

            Double3 eyeUnits = ctx.EyeKm / KmPerUnit;
            float metresPerUnit = (float)(KmPerUnit * ctx.UnitsPerKm);
            Vector4 units = new Vector4(metresPerUnit, (float)ParsecsPerUnit, 0f, 0f);
            Vector4 look = new Vector4(faintestBrightness, exposure, nearFadeUnits.x, nearFadeUnits.y);

            Double3 forward = ViewForwardIcrf(ctx, camera);
            double halfAngle = ViewHalfAngle(camera);

            for (int c = 0; c < _chunks.Length && c < _renderers.Count; c++)
            {
                MeshRenderer renderer = _renderers[c];
                if (renderer == null) continue;

                bool visible = PointChunks.InCone(_chunks[c], eyeUnits, forward, halfAngle);
                renderer.enabled = visible;
                if (!visible) continue;

                Double3 eyeInChunk = PointChunks.EyeInChunk(_chunks[c], eyeUnits);
                Material m = _materials[c];
                m.SetVector(EyeInChunkId,
                    new Vector4((float)eyeInChunk.x, (float)eyeInChunk.y, (float)eyeInChunk.z, 0f));
                m.SetVector(UnitsId, units);
                m.SetVector(LookId, look);
                m.SetMatrix(LayerToWorldId, ctx.IcrfToWorld);
                m.SetFloat(FadeId, fade);

                DrawCount++;
                Mesh mesh = _meshes[c];
                if (mesh != null) VertexCount += mesh.vertexCount;
            }
        }

        /// <summary>The camera's forward, expressed in ICRF, so the cone test can be done in layer units.</summary>
        private static Double3 ViewForwardIcrf(in FrameContext ctx, Camera camera)
        {
            if (camera == null) return Double3.UnitZ;
            Vector3 worldForward = camera.transform.forward;
            Vector3 icrf = ctx.IcrfToWorld.inverse.MultiplyVector(worldForward).normalized;
            return new Double3(icrf.x, icrf.y, icrf.z);
        }

        /// <summary>Half the diagonal field of view, plus a margin so a chunk never pops at the edge.</summary>
        private static double ViewHalfAngle(Camera camera)
        {
            if (camera == null) return Math.PI;
            double vertical = camera.fieldOfView * Mathf.Deg2Rad * 0.5;
            double horizontal = Math.Atan(Math.Tan(vertical) * Math.Max(0.1f, camera.aspect));
            double diagonal = Math.Atan(Math.Sqrt(
                Math.Tan(vertical) * Math.Tan(vertical) + Math.Tan(horizontal) * Math.Tan(horizontal)));
            return diagonal + 10.0 * MathD.Deg2Rad;
        }

        /// <summary>
        /// Rewrite the de-duplication flags on every chunk. Every chunk, not only the ones that
        /// hold a hidden point: the flag has to be CLEARED when a body stops being drawn, and a
        /// pass that only ever sets it leaves stars permanently missing after the focus moves.
        /// </summary>
        public void SetHidden(ICollection<int> hiddenGlobalIndices)
        {
            for (int c = 0; c < _meshes.Count && c < _pointOfSprite.Count; c++)
                PointMesh.SetHidden(_meshes[c], _pointOfSprite[c], hiddenGlobalIndices);
        }

        public void Clear()
        {
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i] != null) UnityEngine.Object.Destroy(_renderers[i].gameObject);
            for (int i = 0; i < _materials.Count; i++)
                if (_materials[i] != null) UnityEngine.Object.Destroy(_materials[i]);
            for (int i = 0; i < _meshes.Count; i++)
                if (_meshes[i] != null) UnityEngine.Object.Destroy(_meshes[i]);
            _renderers.Clear();
            _materials.Clear();
            _meshes.Clear();
            _pointOfSprite.Clear();
            _chunks = new PointChunk[0];
            PointCount = 0;
            DrawCount = 0;
            VertexCount = 0;
        }
    }
}
