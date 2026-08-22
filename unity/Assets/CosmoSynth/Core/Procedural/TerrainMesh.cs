using System;

namespace CosmoSynth.Core
{
    /// <summary>
    /// One cube-sphere tile turned into geometry: positions, normals and triangles, ready to
    /// hand to a renderer.
    ///
    /// Positions are **relative to <see cref="OriginKm"/>**, and that is the point of the type
    /// rather than an implementation detail. A 32-bit float carries about seven significant
    /// digits, so an absolute position on Earth's surface — 6371 km from the centre — is
    /// quantised to about 60 centimetres, and every vertex of a metre-scale landscape would snap
    /// to the same handful of values. Expressed relative to the tile's own centre the numbers are
    /// small: a tile a hundred kilometres across resolves to a centimetre, and one a kilometre
    /// across to a tenth of a millimetre. The double-precision layer exists to make this
    /// conversion possible at the last moment, and this is the last moment.
    /// </summary>
    public sealed class TerrainMesh
    {
        /// <summary>The tile this was built for.</summary>
        public TileId Tile { get; }

        /// <summary>Absolute position, km, that the vertices are measured from.</summary>
        public Double3 OriginKm { get; }

        /// <summary>Vertex positions relative to <see cref="OriginKm"/>, km, as xyz triples.</summary>
        public float[] Positions { get; }

        /// <summary>Outward unit normals, xyz triples, parallel to <see cref="Positions"/>.</summary>
        public float[] Normals { get; }

        public int[] Triangles { get; }

        /// <summary>Smallest and largest surface radius in this tile, km. Used for culling.</summary>
        public double MinRadiusKm { get; }
        public double MaxRadiusKm { get; }

        /// <summary>Vertices per side of the tile's grid, excluding the skirt.</summary>
        public int Resolution { get; }

        public int VertexCount => Positions.Length / 3;
        public int TriangleCount => Triangles.Length / 3;

        internal TerrainMesh(TileId tile, Double3 originKm, float[] positions, float[] normals,
            int[] triangles, double minRadiusKm, double maxRadiusKm, int resolution)
        {
            Tile = tile;
            OriginKm = originKm;
            Positions = positions;
            Normals = normals;
            Triangles = triangles;
            MinRadiusKm = minRadiusKm;
            MaxRadiusKm = maxRadiusKm;
            Resolution = resolution;
        }

        public override string ToString() =>
            $"tile {Tile}, {VertexCount} vertices, {TriangleCount} triangles, " +
            $"r {MinRadiusKm:F3}-{MaxRadiusKm:F3} km";
    }

    /// <summary>
    /// Builds tile meshes from a <see cref="TerrainField"/>.
    ///
    /// Pure arithmetic on doubles with no engine types anywhere, which is what lets it run on a
    /// worker thread — the same arrangement <c>PlanetTextureBaker</c> already uses, and the
    /// reason a planet can be subdivided without stalling a frame.
    /// </summary>
    public static class TerrainMeshBuilder
    {
        /// <summary>Default grid resolution: 17 vertices a side, so 16x16 quads, 512 triangles.</summary>
        public const int DefaultResolution = 17;

        /// <summary>
        /// How far a skirt hangs below the surface, as a fraction of the tile's ground size.
        ///
        /// Skirts exist because neighbouring tiles at different subdivision levels do not share
        /// vertices along their common edge, so the finer one's surface wanders away from the
        /// coarser one's straight line and leaves a crack you can see space through. A skirt is a
        /// band of geometry dropped inward from the tile's border that plugs the gap. It is
        /// hidden by the terrain in front of it and costs one ring of quads.
        ///
        /// Sized from the tile rather than fixed: the gap scales with the tile's ground size, so
        /// a constant depth is either a wasteful curtain on a large tile or too short on a small
        /// one.
        /// </summary>
        public const double SkirtFraction = 0.02;

        /// <summary>
        /// Build the geometry for one tile.
        ///
        /// <paramref name="resolution"/> is vertices per side and must be at least 2. The whole
        /// grid is evaluated including the border, so adjacent tiles at the same level sample the
        /// identical direction along their shared edge and meet exactly — cracks between equal
        /// levels would be a bug, not something for the skirt to hide.
        /// </summary>
        public static TerrainMesh Build(TileId tile, TerrainField field,
            int resolution = DefaultResolution, bool includeSkirt = true)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (resolution < 2)
                throw new ArgumentOutOfRangeException(nameof(resolution), "A tile needs at least 2x2 vertices.");

            int side = resolution;
            int gridVertices = side * side;
            int skirtVertices = includeSkirt ? 4 * (side - 1) : 0;
            int totalVertices = gridVertices + skirtVertices;

            var positions = new float[totalVertices * 3];
            var normals = new float[totalVertices * 3];

            // The tile's centre on the actual surface, which every vertex is measured from.
            Double3 centreDirection = tile.CenterDirection;
            Double3 originKm = centreDirection * field.VisibleRadiusKm(centreDirection);

            double minRadius = double.PositiveInfinity, maxRadius = 0.0;

            // Directions are kept so the skirt and the normals do not have to recompute them.
            var directions = new Double3[gridVertices];
            var radii = new double[gridVertices];

            for (int y = 0; y < side; y++)
            {
                double localV = y / (double)(side - 1);
                for (int x = 0; x < side; x++)
                {
                    double localU = x / (double)(side - 1);
                    int index = y * side + x;

                    Double3 direction = tile.DirectionAt(localU, localV);
                    double radius = field.VisibleRadiusKm(direction);

                    directions[index] = direction;
                    radii[index] = radius;

                    if (radius < minRadius) minRadius = radius;
                    if (radius > maxRadius) maxRadius = radius;

                    Write(positions, index, direction * radius - originKm);
                    Write(normals, index, field.SurfaceNormal(direction));
                }
            }

            int[] triangles = BuildTriangles(side, includeSkirt);

            if (includeSkirt)
            {
                double depth = tile.GroundSizeKm(field.BodyRadiusKm) * SkirtFraction;
                BuildSkirt(side, directions, radii, originKm, depth, positions, normals);
            }

            return new TerrainMesh(tile, originKm, positions, normals, triangles,
                minRadius, maxRadius, resolution);
        }

        private static void Write(float[] target, int index, Double3 value)
        {
            target[index * 3] = (float)value.x;
            target[index * 3 + 1] = (float)value.y;
            target[index * 3 + 2] = (float)value.z;
        }

        /// <summary>
        /// The grid's triangles, plus a quad strip joining each border vertex to its skirt twin.
        ///
        /// Winding is counter-clockwise seen from outside, which is what a left-handed renderer
        /// wants after the direction convention this project uses. A tile wound the other way is
        /// not subtly wrong, it is invisible — you see the inside of the planet through it.
        /// </summary>
        private static int[] BuildTriangles(int side, bool includeSkirt)
        {
            int quads = (side - 1) * (side - 1);
            int skirtQuads = includeSkirt ? 4 * (side - 1) : 0;
            var triangles = new int[(quads + skirtQuads) * 6];

            int at = 0;
            for (int y = 0; y < side - 1; y++)
            {
                for (int x = 0; x < side - 1; x++)
                {
                    int bottomLeft = y * side + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + side;
                    int topRight = topLeft + 1;

                    triangles[at++] = bottomLeft; triangles[at++] = bottomRight; triangles[at++] = topLeft;
                    triangles[at++] = bottomRight; triangles[at++] = topRight; triangles[at++] = topLeft;
                }
            }

            if (!includeSkirt) return triangles;

            int skirtBase = side * side;
            for (int edge = 0; edge < 4; edge++)
            {
                for (int i = 0; i < side - 1; i++)
                {
                    int a = BorderIndex(side, edge, i);
                    int b = BorderIndex(side, edge, i + 1);
                    int skirtA = skirtBase + edge * (side - 1) + i;
                    int skirtB = skirtBase + edge * (side - 1) + ((i + 1) % (side - 1));

                    // The last vertex of an edge is the first of the next, so its skirt twin is
                    // that edge's first one — the ring has to close or the corner shows a gap.
                    if (i + 1 == side - 1) skirtB = skirtBase + ((edge + 1) % 4) * (side - 1);

                    triangles[at++] = a; triangles[at++] = b; triangles[at++] = skirtA;
                    triangles[at++] = b; triangles[at++] = skirtB; triangles[at++] = skirtA;
                }
            }

            return triangles;
        }

        /// <summary>Grid index of the i-th vertex along one border, walking the ring in order.</summary>
        private static int BorderIndex(int side, int edge, int i)
        {
            switch (edge)
            {
                case 0: return i;                                  // bottom, left to right
                case 1: return (side - 1) + i * side;              // right, bottom to top
                case 2: return side * side - 1 - i;                // top, right to left
                default: return (side - 1 - i) * side;             // left, top to bottom
            }
        }

        private static void BuildSkirt(int side, Double3[] directions, double[] radii,
            Double3 originKm, double depthKm, float[] positions, float[] normals)
        {
            int skirtBase = side * side;

            for (int edge = 0; edge < 4; edge++)
            {
                for (int i = 0; i < side - 1; i++)
                {
                    int border = BorderIndex(side, edge, i);
                    int index = skirtBase + edge * (side - 1) + i;

                    // Straight down along the radius, so the skirt is hidden by the terrain
                    // rather than poking through it at a glancing angle.
                    Write(positions, index, directions[border] * (radii[border] - depthKm) - originKm);

                    // The skirt borrows its border vertex's normal. It is never meant to be seen,
                    // and lighting it as a vertical wall would draw the eye to the very thing it
                    // exists to hide.
                    normals[index * 3] = normals[border * 3];
                    normals[index * 3 + 1] = normals[border * 3 + 1];
                    normals[index * 3 + 2] = normals[border * 3 + 2];
                }
            }
        }
    }
}
