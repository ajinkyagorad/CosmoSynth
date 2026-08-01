using System.Collections.Generic;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// A finer sphere than Unity's primitive, for the few bodies large enough to show its edges.
    ///
    /// <c>GameObject.CreatePrimitive</c> returns a 20-segment UV sphere, which is a 20-gon in
    /// silhouette. That is invisible at the size most bodies are drawn and unmistakable at the
    /// size the one you are actually looking at is: measured off a captured frame, Earth's limb
    /// oscillated between 388 and 400 pixels with an 18 degree period — 360/20 — on a disc 790
    /// pixels across. Five pixels of scalloping around the primary subject of the shot, on a
    /// display where the limb is the highest-contrast edge in the scene.
    ///
    /// <b>Built by subdividing Unity's own mesh rather than by generating one</b>, and that is
    /// the whole reason this is safe to do. The planet surface maps are baked equirectangularly
    /// and applied through the primitive's UV layout; a hand-written UV sphere would have to
    /// reproduce that layout exactly, and getting it wrong does not fail loudly — it rotates or
    /// mirrors the planet, which looks like an entirely plausible world until somebody checks
    /// where Africa is. Splitting each triangle's edges and interpolating what is already there
    /// cannot change the parameterisation at all. Unity duplicates the seam column of vertices so
    /// u never wraps within a single triangle, which is what makes interpolating it safe.
    ///
    /// The mesh is built once and shared, and swapped in only above the size threshold — 235
    /// bodies must not each pay twelve thousand triangles to draw two pixels.
    /// </summary>
    public static class CosmoSphere
    {
        /// <summary>
        /// Each level quadruples the triangles and roughly quarters the silhouette's sagitta.
        /// Two takes Unity's 768 triangles to 12288 and Earth's five pixels of scalloping to
        /// about a third of one, which is under the resampling the compositor does anyway.
        /// </summary>
        private const int Subdivisions = 2;

        private static Mesh _detailed;
        private static bool _attempted;

        /// <summary>
        /// The subdivided sphere, or null if it could not be built — in which case the caller
        /// keeps the primitive and the limb stays faceted, which is a worse picture and not a
        /// broken one.
        /// </summary>
        public static Mesh Detailed(Mesh primitive)
        {
            if (_detailed != null) return _detailed;
            if (_attempted) return null;
            _attempted = true;

            // Reading a mesh Unity has marked non-readable throws, and the built-in resources are
            // not contractually readable on every platform. Checking is cheaper than discovering
            // it from a stack trace inside a headset.
            if (primitive == null || !primitive.isReadable || primitive.vertexCount == 0)
            {
                Debug.LogWarning("[CosmoSynth] the built-in sphere is not readable, so planet " +
                                 "limbs will be drawn with its 20 segments. Cosmetic only.");
                return null;
            }

            Vector3[] positions = primitive.vertices;
            Vector2[] uvs = primitive.uv;
            int[] triangles = primitive.triangles;

            if (uvs == null || uvs.Length != positions.Length) return null;

            // Unity's primitive sphere has radius 0.5, and every vertex is on it. Taking the
            // radius from the mesh rather than assuming keeps this correct if that ever changes.
            float radius = positions[0].magnitude;
            if (radius <= 0f) return null;

            var outPositions = new List<Vector3>(triangles.Length * 4);
            var outUvs = new List<Vector2>(triangles.Length * 4);
            var outTriangles = new List<int>(triangles.Length * 4);

            for (int t = 0; t < triangles.Length; t += 3)
            {
                Subdivide(
                    positions[triangles[t]], positions[triangles[t + 1]], positions[triangles[t + 2]],
                    uvs[triangles[t]], uvs[triangles[t + 1]], uvs[triangles[t + 2]],
                    Subdivisions, radius, outPositions, outUvs, outTriangles);
            }

            // Deliberately unwelded: 12288 triangles is 36864 vertices, which still fits a 16-bit
            // index buffer, and welding would buy nothing because the normals are exact anyway.
            var mesh = new Mesh { name = "CosmoSphere" };
            mesh.SetVertices(outPositions);
            mesh.SetUVs(0, outUvs);
            mesh.SetTriangles(outTriangles, 0);

            // Exact, not averaged. On a sphere the normal *is* the normalised position, so
            // RecalculateNormals would only approximate what is already known — and on an
            // unwelded mesh it would approximate it facet by facet.
            var normals = new Vector3[outPositions.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = outPositions[i].normalized;
            mesh.SetNormals(normals);

            // Tangents are not optional: the surface baker ships a normal map, and Standard's
            // _NORMALMAP path reads the tangent frame. Without them a planet's relief is lit from
            // an arbitrary direction.
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(markNoLongerReadable: true);

            _detailed = mesh;
            return _detailed;
        }

        private static void Subdivide(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc,
            int depth, float radius, List<Vector3> positions, List<Vector2> uvs, List<int> triangles)
        {
            if (depth <= 0)
            {
                int start = positions.Count;
                positions.Add(a); positions.Add(b); positions.Add(c);
                uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                return;
            }

            // The midpoint of a chord is inside the sphere; pushing it back out is what actually
            // refines the silhouette rather than merely adding coplanar triangles to it.
            Vector3 ab = (a + b).normalized * radius;
            Vector3 bc = (b + c).normalized * radius;
            Vector3 ca = (c + a).normalized * radius;

            Vector2 uab = (ua + ub) * 0.5f;
            Vector2 ubc = (ub + uc) * 0.5f;
            Vector2 uca = (uc + ua) * 0.5f;

            Subdivide(a, ab, ca, ua, uab, uca, depth - 1, radius, positions, uvs, triangles);
            Subdivide(ab, b, bc, uab, ub, ubc, depth - 1, radius, positions, uvs, triangles);
            Subdivide(ca, bc, c, uca, ubc, uc, depth - 1, radius, positions, uvs, triangles);
            Subdivide(ab, bc, ca, uab, ubc, uca, depth - 1, radius, positions, uvs, triangles);
        }
    }
}
