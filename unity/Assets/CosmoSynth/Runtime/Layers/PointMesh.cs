using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Builds the one mesh every point population is drawn from: four vertices per sprite,
    /// billboarded in the vertex stage by <c>CosmoSynth/Point</c>.
    ///
    /// <b>One mesh, never instancing.</b> <c>RenderMeshInstanced</c> and the indirect variants
    /// double their instance count under multiview on this device, and the failure is invisible
    /// on a desktop capture. A static mesh of quads has no such failure mode, batches perfectly,
    /// and costs four vertices per point — which for 120,000 stars is 480 k vertices, inside the
    /// per-frame budget with the chunk cone cull typically leaving a fifth of that in view.
    ///
    /// <b>All four vertices of a quad carry the same <c>data.w</c></b> (R14). The shader's "cull"
    /// writes an off-screen clip position, which CLIPS a primitive rather than discarding it: a
    /// quad whose corners disagreed would rasterise as a wedge, not as nothing.
    /// </summary>
    public static class PointMesh
    {
        /// <summary>Shape codes, matching <c>CosmoSynth/Point</c>'s <c>data.z</c>.</summary>
        public const float ShapeCore = 0f;
        public const float ShapeHalo = 1f;
        public const float ShapeSpikeX = 2f;
        public const float ShapeSpikeY = 3f;

        /// <summary>One sprite to emit: where, how bright, what colour, which shape.</summary>
        public struct Sprite
        {
            public Vector3 Offset;      // layer: offset from the chunk origin; object: object space; sky: direction
            public Color Colour;        // display (sRGB); alpha unused
            public float Magnitude;     // absolute in layer mode, apparent otherwise
            public float Shape;
            public float Hidden;        // 1 = drawn by something else this frame (de-duplicated)
        }

        /// <summary>
        /// Build a mesh from a sprite list. The bounds are deliberately enormous: every vertex
        /// moves in the shader, so Unity's own culling has nothing true to work with and the
        /// layer does its culling per chunk instead.
        /// </summary>
        public static Mesh Build(IReadOnlyList<Sprite> sprites, string name)
        {
            int n = sprites.Count;
            Mesh mesh = new Mesh { name = name };
            if (n == 0) return mesh;

            mesh.indexFormat = n * 4 > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            Vector3[] positions = new Vector3[n * 4];
            Color[] colours = new Color[n * 4];
            Vector4[] data = new Vector4[n * 4];
            int[] indices = new int[n * 6];

            for (int i = 0; i < n; i++)
            {
                Sprite s = sprites[i];
                int v = i * 4;
                for (int c = 0; c < 4; c++)
                {
                    positions[v + c] = s.Offset;
                    colours[v + c] = s.Colour;
                    // x magnitude, y corner, z shape, w hidden — the same w on all four corners.
                    data[v + c] = new Vector4(s.Magnitude, c, s.Shape, s.Hidden);
                }

                int t = i * 6;
                indices[t + 0] = v + 0; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v + 2; indices[t + 4] = v + 1; indices[t + 5] = v + 3;
            }

            mesh.SetVertices(positions);
            mesh.SetColors(colours);
            mesh.SetUVs(0, data);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e9f);
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>
        /// Rewrite only the hidden flag, for de-duplication against catalogue bodies. Rare (it
        /// fires on <c>BodiesChanged</c>), so a full stream rewrite is cheaper than keeping a
        /// second index around.
        ///
        /// <paramref name="pointOfSprite"/> maps each sprite in this mesh back to the population
        /// index it came from. It cannot be derived from the vertex count: a bright star carries a
        /// halo and two spike quads as well as its core, so sprites and points are not one to one.
        /// Getting this wrong hides the wrong star, which nobody would ever notice.
        /// </summary>
        public static void SetHidden(Mesh mesh, IReadOnlyList<int> pointOfSprite,
            ICollection<int> hiddenPoints)
        {
            if (mesh == null || pointOfSprite == null) return;
            int sprites = pointOfSprite.Count;
            if (mesh.vertexCount != sprites * 4) return;

            List<Vector4> data = new List<Vector4>(sprites * 4);
            mesh.GetUVs(0, data);
            for (int s = 0; s < sprites; s++)
            {
                float hidden = hiddenPoints != null && hiddenPoints.Contains(pointOfSprite[s]) ? 1f : 0f;
                for (int c = 0; c < 4; c++)
                {
                    Vector4 d = data[s * 4 + c];
                    d.w = hidden;
                    data[s * 4 + c] = d;
                }
            }
            mesh.SetUVs(0, data);
        }
    }
}
