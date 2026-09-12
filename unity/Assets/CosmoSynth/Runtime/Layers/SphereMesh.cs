using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// A unit sphere whose object-space vertices <b>are</b> ICRF directions — which is exactly
    /// what <c>CosmoSynth/Sky</c> expects, because it samples the cube by direction rather than by
    /// a Unity axis. That keeps the one axis mapping inside <c>FloatingOrigin</c>: the lookup
    /// needs no mapping at all, has no pole oversampling and cannot be inside-out.
    /// </summary>
    public static class SphereMesh
    {
        /// <summary>A latitude-longitude sphere. 48 x 24 is about 1,200 vertices, the §7.3 row.</summary>
        public static Mesh Unit(string name, int longitudes = 48, int latitudes = 24)
        {
            int vertexCount = (longitudes + 1) * (latitudes + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            int[] indices = new int[longitudes * latitudes * 6];

            for (int y = 0; y <= latitudes; y++)
            {
                float v = (float)y / latitudes;
                float theta = v * Mathf.PI;           // 0 at +z (the ICRF pole), pi at -z
                float sinT = Mathf.Sin(theta), cosT = Mathf.Cos(theta);
                for (int x = 0; x <= longitudes; x++)
                {
                    float u = (float)x / longitudes;
                    float phi = u * Mathf.PI * 2f;
                    vertices[y * (longitudes + 1) + x] =
                        new Vector3(sinT * Mathf.Cos(phi), sinT * Mathf.Sin(phi), cosT);
                }
            }

            int t = 0;
            for (int y = 0; y < latitudes; y++)
            {
                for (int x = 0; x < longitudes; x++)
                {
                    int a = y * (longitudes + 1) + x;
                    int b = a + longitudes + 1;
                    indices[t++] = a; indices[t++] = b; indices[t++] = a + 1;
                    indices[t++] = a + 1; indices[t++] = b; indices[t++] = b + 1;
                }
            }

            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            // Huge bounds: the vertex stage anchors this to the eye, so the mesh's own extent is
            // not where it ends up and frustum culling on it would blink the sky out.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e9f);
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>
        /// Three orthogonal great circles as ribbon polylines — the cheapest shape that reads as a
        /// sphere without claiming to be a surface anyone measured.
        /// </summary>
        public static RibbonMesh.Polyline[] WireFrame(Color colour, int segments = 96)
        {
            return new[]
            {
                RibbonMesh.Circle(Vector3.zero, Vector3.forward, 1f, segments, colour),
                RibbonMesh.Circle(Vector3.zero, Vector3.up, 1f, segments, colour),
                RibbonMesh.Circle(Vector3.zero, Vector3.right, 1f, segments, colour),
            };
        }
    }
}
