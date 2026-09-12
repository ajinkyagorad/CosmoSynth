using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The unit cube <c>CosmoSynth/Volume</c> marches inside, and the wire box that shows where a
    /// measurement stops.
    ///
    /// Wound so the BACK faces are the ones that survive <c>Cull Front</c>: the shader marches
    /// from the eye through the box whether the eye is inside it or outside, and the back face is
    /// the only one present in both cases.
    /// </summary>
    public static class BoxMesh
    {
        /// <summary>A solid unit cube spanning [-0.5, 0.5] on every axis.</summary>
        public static Mesh Unit(string name)
        {
            Vector3[] v =
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
            };
            int[] t =
            {
                0, 2, 1, 0, 3, 2,   // -z
                4, 5, 6, 4, 6, 7,   // +z
                0, 1, 5, 0, 5, 4,   // -y
                3, 7, 6, 3, 6, 2,   // +y
                0, 4, 7, 0, 7, 3,   // -x
                1, 2, 6, 1, 6, 5,   // +x
            };
            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetIndices(t, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>
        /// The twelve edges of the unit cube as ribbon polylines: the <c>dustExtent</c> guide,
        /// which is the honest answer to "why does the dust stop there" — the survey does.
        /// </summary>
        public static RibbonMesh.Polyline[] UnitEdges(Color colour)
        {
            Vector3[] v =
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
            };
            int[,] pairs =
            {
                {0,1},{1,2},{2,3},{3,0},
                {4,5},{5,6},{6,7},{7,4},
                {0,4},{1,5},{2,6},{3,7},
            };
            RibbonMesh.Polyline[] lines = new RibbonMesh.Polyline[12];
            for (int i = 0; i < 12; i++)
            {
                lines[i] = new RibbonMesh.Polyline { Colour = colour };
                lines[i].Points.Add(v[pairs[i, 0]]);
                lines[i].Points.Add(v[pairs[i, 1]]);
            }
            return lines;
        }
    }
}
