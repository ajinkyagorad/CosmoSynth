using System;
using System.IO;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Reader for the ".csmesh" spacecraft meshes written by tools/cosmodata/build_spacecraft.py.
    ///
    /// The format exists because Unity's own model import is an editor-time operation, and this
    /// project has no editor-time: the whole scene is constructed in code and every asset it
    /// uses is streamed from StreamingAssets. Shipping the original glTF instead would mean
    /// decompressing Draco on the headset's CPU at load, for geometry that has already been
    /// decimated to a fixed budget offline. This is a flat little file that uploads with no
    /// parsing worth the name.
    ///
    /// Layout, all little-endian, positions in metres, axes already Unity's:
    ///
    ///     magic       4       "CSM1"
    ///     flags       u32     bit 0: 32-bit indices rather than 16-bit
    ///     vertices    u32
    ///     indices     u32
    ///     submeshes   u32
    ///     boundsMin   3xf32   metres
    ///     boundsMax   3xf32   metres
    ///     lengthM     f32     longest axis, metres
    ///     ---
    ///     vertices    vertexCount x (position 3xf32, normal 3xf32)
    ///     indices     indexCount x (u16 or u32)
    ///     submeshes   submeshCount x (start u32, count u32, rgba 4xf32, metallic f32, smoothness f32)
    /// </summary>
    public static class CsMesh
    {
        public const int HeaderBytes = 48;
        private const uint Flag32BitIndices = 1u;

        /// <summary>One run of triangles sharing an appearance.</summary>
        public struct Submesh
        {
            public Color Color;
            public float Metallic;
            public float Smoothness;
        }

        /// <summary>A loaded model: the geometry, its per-submesh appearance, and its real size.</summary>
        public sealed class Model
        {
            public Mesh Mesh;
            public Submesh[] Submeshes;

            /// <summary>Longest axis in metres. The mesh is authored at true scale, so this is
            /// simply its bounding size — but carrying it explicitly means the UI can state the
            /// real dimension even while the thing is drawn hugely exaggerated.</summary>
            public float LengthMetres;

            public int TriangleCount;
        }

        /// <summary>
        /// Parse a .csmesh. Returns null and logs on anything malformed, rather than throwing
        /// into a coroutine where the exception would strand the loader mid-queue.
        /// </summary>
        public static Model Parse(byte[] data, string label)
        {
            if (data == null || data.Length < HeaderBytes)
            {
                Debug.LogError($"[CosmoSynth] model '{label}' is {(data == null ? 0 : data.Length)} " +
                               $"bytes, too short to be a .csmesh.");
                return null;
            }

            try
            {
                using (var stream = new MemoryStream(data, writable: false))
                using (var reader = new BinaryReader(stream))
                {
                    // Checked rather than assumed: StreamingAssets on Android is reached through
                    // UnityWebRequest, which answers a missing file with a perfectly valid
                    // response containing an error page. A magic number is the only proof.
                    if (reader.ReadByte() != 'C' || reader.ReadByte() != 'S' ||
                        reader.ReadByte() != 'M' || reader.ReadByte() != '1')
                    {
                        Debug.LogError($"[CosmoSynth] model '{label}' does not begin with 'CSM1'; " +
                                       "it is not a .csmesh. Rebuild with build_spacecraft.py.");
                        return null;
                    }

                    uint flags = reader.ReadUInt32();
                    int vertexCount = (int)reader.ReadUInt32();
                    int indexCount = (int)reader.ReadUInt32();
                    int submeshCount = (int)reader.ReadUInt32();

                    // Bounds are recomputed by Unity from the vertices; they are in the file so
                    // a tool can inspect it without decoding the geometry.
                    for (int i = 0; i < 6; i++) reader.ReadSingle();
                    float lengthMetres = reader.ReadSingle();

                    bool wideIndices = (flags & Flag32BitIndices) != 0;
                    long expected = HeaderBytes
                                    + (long)vertexCount * 24
                                    + (long)indexCount * (wideIndices ? 4 : 2)
                                    + (long)submeshCount * 32;

                    if (data.Length != expected)
                    {
                        Debug.LogError(
                            $"[CosmoSynth] model '{label}' declares {vertexCount} vertices, " +
                            $"{indexCount} indices and {submeshCount} submeshes, which needs " +
                            $"{expected} bytes, but the file is {data.Length}. It is truncated " +
                            "or was written by a different version of the packer.");
                        return null;
                    }

                    if (vertexCount == 0 || indexCount < 3 || submeshCount == 0)
                    {
                        Debug.LogError($"[CosmoSynth] model '{label}' contains no geometry.");
                        return null;
                    }

                    var vertices = new Vector3[vertexCount];
                    var normals = new Vector3[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                    {
                        vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    }

                    var indices = new int[indexCount];
                    if (wideIndices)
                    {
                        for (int i = 0; i < indexCount; i++) indices[i] = (int)reader.ReadUInt32();
                    }
                    else
                    {
                        for (int i = 0; i < indexCount; i++) indices[i] = reader.ReadUInt16();
                    }

                    var starts = new int[submeshCount];
                    var counts = new int[submeshCount];
                    var submeshes = new Submesh[submeshCount];
                    for (int i = 0; i < submeshCount; i++)
                    {
                        starts[i] = (int)reader.ReadUInt32();
                        counts[i] = (int)reader.ReadUInt32();
                        submeshes[i] = new Submesh
                        {
                            Color = new Color(reader.ReadSingle(), reader.ReadSingle(),
                                              reader.ReadSingle(), reader.ReadSingle()),
                            Metallic = reader.ReadSingle(),
                            Smoothness = reader.ReadSingle(),
                        };
                    }

                    var mesh = new Mesh { name = label };

                    // Anything past 65535 vertices needs the wide index buffer explicitly; the
                    // default silently wraps, producing geometry shredded into confetti.
                    if (vertexCount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                    mesh.vertices = vertices;
                    mesh.normals = normals;
                    mesh.subMeshCount = submeshCount;

                    for (int i = 0; i < submeshCount; i++)
                    {
                        if (starts[i] < 0 || counts[i] < 0 || starts[i] + counts[i] > indexCount)
                        {
                            Debug.LogError(
                                $"[CosmoSynth] model '{label}' submesh {i} spans indices " +
                                $"{starts[i]}..{starts[i] + counts[i]} of {indexCount}.");
                            UnityEngine.Object.Destroy(mesh);
                            return null;
                        }

                        var triangles = new int[counts[i]];
                        Array.Copy(indices, starts[i], triangles, 0, counts[i]);
                        mesh.SetTriangles(triangles, i, calculateBounds: false);
                    }

                    mesh.RecalculateBounds();
                    mesh.UploadMeshData(markNoLongerReadable: true);

                    return new Model
                    {
                        Mesh = mesh,
                        Submeshes = submeshes,
                        LengthMetres = lengthMetres > 0f ? lengthMetres : 1f,
                        TriangleCount = indexCount / 3,
                    };
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CosmoSynth] model '{label}' failed to parse: {exception.Message}");
                return null;
            }
        }
    }
}
