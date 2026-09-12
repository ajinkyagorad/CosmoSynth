using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Builds the screen-constant-width line mesh <c>CosmoSynth/Ribbon</c> expands: four vertices
    /// per segment, each carrying its own endpoint and the segment's other endpoint so the shader
    /// can widen the line in clip space.
    ///
    /// <b>Why not <c>LineRenderer</c>.</b> A line whose width is set in world metres is either a
    /// hairline or a hosepipe depending on where the viewer is standing, and at these scales the
    /// viewer moves twenty decades. The width here is angular — dmm, the same unit as every panel
    /// and every label — so a constellation figure is the same thickness from Earth and from
    /// Aldebaran.
    ///
    /// <b>The alpha channel is the tail gradient, not opacity.</b> The blend is additive and
    /// ignores destination alpha entirely; the shader multiplies colour by <c>COLOR.a</c>, which
    /// is what makes a fading tail possible without a second material.
    /// </summary>
    public static class RibbonMesh
    {
        /// <summary>One polyline to draw, with its own colour and a per-vertex fade.</summary>
        public sealed class Polyline
        {
            public readonly List<Vector3> Points = new List<Vector3>();
            public Color Colour = new Color32(255, 255, 255, 255);

            /// <summary>Per-point gradient in [0, 1]; empty means flat. Same length as Points.</summary>
            public readonly List<float> Gradient = new List<float>();

            /// <summary>Close the loop back to the first point — for rings and boundaries.</summary>
            public bool Closed;
        }

        /// <summary>
        /// Build one mesh from many polylines: one draw call for every constellation figure, or
        /// every ring of a grid. Polylines with fewer than two points contribute nothing.
        /// </summary>
        public static Mesh Build(IReadOnlyList<Polyline> lines, string name)
        {
            int segments = 0;
            for (int l = 0; l < lines.Count; l++)
            {
                int p = lines[l].Points.Count;
                if (p < 2) continue;
                segments += lines[l].Closed ? p : p - 1;
            }

            Mesh mesh = new Mesh { name = name };
            if (segments == 0) return mesh;

            mesh.indexFormat = segments * 4 > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            Vector3[] positions = new Vector3[segments * 4];
            Vector3[] others = new Vector3[segments * 4];
            Vector2[] sideOrder = new Vector2[segments * 4];
            Color[] colours = new Color[segments * 4];
            int[] indices = new int[segments * 6];

            int s = 0;
            for (int l = 0; l < lines.Count; l++)
            {
                Polyline line = lines[l];
                int p = line.Points.Count;
                if (p < 2) continue;
                int count = line.Closed ? p : p - 1;

                for (int i = 0; i < count; i++)
                {
                    int ia = i;
                    int ib = (i + 1) % p;
                    Vector3 a = line.Points[ia];
                    Vector3 b = line.Points[ib];
                    Color ca = Fade(line, ia);
                    Color cb = Fade(line, ib);

                    int v = s * 4;
                    positions[v + 0] = a; others[v + 0] = b; sideOrder[v + 0] = new Vector2(-1f, 1f); colours[v + 0] = ca;
                    positions[v + 1] = a; others[v + 1] = b; sideOrder[v + 1] = new Vector2(1f, 1f); colours[v + 1] = ca;
                    positions[v + 2] = b; others[v + 2] = a; sideOrder[v + 2] = new Vector2(-1f, -1f); colours[v + 2] = cb;
                    positions[v + 3] = b; others[v + 3] = a; sideOrder[v + 3] = new Vector2(1f, -1f); colours[v + 3] = cb;

                    int t = s * 6;
                    indices[t + 0] = v + 0; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                    indices[t + 3] = v + 2; indices[t + 4] = v + 1; indices[t + 5] = v + 3;
                    s++;
                }
            }

            mesh.SetVertices(positions);
            mesh.SetUVs(0, others);
            mesh.SetUVs(1, sideOrder);
            mesh.SetColors(colours);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e9f);
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static Color Fade(Polyline line, int i)
        {
            Color c = line.Colour;
            c.a = i < line.Gradient.Count ? line.Gradient[i] : 1f;
            return c;
        }

        /// <summary>A circle in a plane, as a closed polyline. Used by every grid ring and orbit ring.</summary>
        public static Polyline Circle(Vector3 centre, Vector3 normal, float radius, int segments,
            Color colour)
        {
            Polyline line = new Polyline { Colour = colour, Closed = true };
            Vector3 n = normal.normalized;
            Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(n, u);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                line.Points.Add(centre + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * radius);
            }
            return line;
        }
    }
}
