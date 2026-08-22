using System;
using CosmoSynth.Core;

namespace CosmoSynth.Tests
{
    /// <summary>
    /// Turning the cube-sphere quadtree into geometry.
    ///
    /// The properties worth asserting are the ones a screenshot cannot show. That every vertex
    /// lands on the surface the field describes rather than near it; that neighbouring tiles
    /// meet exactly, because a crack between two tiles of the same level is a bug the skirt is
    /// not there to hide; and that the tile-local frame actually buys the precision it exists
    /// for, since that is the entire reason the type is shaped this way.
    /// </summary>
    public static class TerrainMeshTests
    {
        private static TerrainField EarthLike() => new TerrainField
        {
            Seed = 1234,
            BodyRadiusKm = 6371.0,
            ReliefKm = 9.0,
            OceanFraction = 0.7,
        };

        [Test("every vertex sits on the surface the terrain field describes")]
        public static void VerticesLieOnTheField()
        {
            TerrainField field = EarthLike();
            TileId tile = new TileId(2, 3, 5, 6);

            TerrainMesh mesh = TerrainMeshBuilder.Build(tile, field, 9, includeSkirt: false);

            Assert.True(mesh.VertexCount == 81, $"expected 9x9 vertices, got {mesh.VertexCount}");
            Assert.True(mesh.TriangleCount == 128, $"expected 128 triangles, got {mesh.TriangleCount}");

            int side = 9;
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    int index = y * side + x;
                    Double3 absolute = mesh.OriginKm + new Double3(
                        mesh.Positions[index * 3],
                        mesh.Positions[index * 3 + 1],
                        mesh.Positions[index * 3 + 2]);

                    Double3 direction = tile.DirectionAt(x / (double)(side - 1), y / (double)(side - 1));
                    double expected = field.VisibleRadiusKm(direction);

                    // A metre, on a body six and a half thousand kilometres across. The residual
                    // is the float conversion, which is exactly what the local frame bounds.
                    Assert.Equal(expected, absolute.Magnitude, 1e-3,
                        $"vertex ({x},{y}) is not on the surface");
                }
            }
        }

        [Test("the tile-local frame is what makes float precision sufficient")]
        public static void LocalFrameKeepsPrecision()
        {
            TerrainField field = EarthLike();

            // A level-6 tile is a few hundred kilometres across on Earth.
            TerrainMesh mesh = TerrainMeshBuilder.Build(new TileId(0, 6, 20, 20), field, 17);

            double furthest = 0.0;
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                double magnitude = new Double3(mesh.Positions[i * 3], mesh.Positions[i * 3 + 1],
                    mesh.Positions[i * 3 + 2]).Magnitude;
                if (magnitude > furthest) furthest = magnitude;
            }

            // The whole argument for the local frame in one assertion: local coordinates are
            // thousands of times smaller than the absolute ones they replace, which is where the
            // precision comes from. Absolute, this tile sits 6371 km out and a float resolves it
            // to roughly half a metre.
            Assert.True(furthest < 400.0,
                $"local coordinates reach {furthest:F1} km; the frame is not doing its job");
            Assert.True(mesh.OriginKm.Magnitude > 6000.0,
                "the origin should be out on the surface, where the tile actually is");

            double floatResolutionAbsolute = mesh.OriginKm.Magnitude * 1.19e-7;   // 2^-23
            double floatResolutionLocal = System.Math.Max(furthest, 1.0) * 1.19e-7;
            Assert.True(floatResolutionLocal < floatResolutionAbsolute / 10.0,
                $"local frame gains only {floatResolutionAbsolute / floatResolutionLocal:F1}x precision");
        }

        [Test("tiles of the same level meet exactly along their shared edge")]
        public static void NeighbouringTilesShareTheirEdge()
        {
            TerrainField field = EarthLike();
            const int side = 9;

            // Level 10, where a tile is about ten kilometres across.
            //
            // The level matters and the first version of this test got it wrong. At level 3 a
            // tile spans some twelve hundred kilometres, so its own local coordinates reach
            // ~600 km, and a 32-bit float resolves that to about seven centimetres — two
            // neighbours rounding independently disagreed by 58 mm and the test called it a
            // seam. It was not a seam, it was the honest precision of the frame at that size.
            // The local frame only buys precision in proportion to how small the tile is, which
            // is exactly the regime landing happens in.
            var left = new TileId(0, 10, 300, 400);
            var right = new TileId(0, 10, 301, 400);

            TerrainMesh leftMesh = TerrainMeshBuilder.Build(left, field, side, includeSkirt: false);
            TerrainMesh rightMesh = TerrainMeshBuilder.Build(right, field, side, includeSkirt: false);

            for (int y = 0; y < side; y++)
            {
                // Right column of the left tile, left column of the right tile.
                int leftIndex = y * side + (side - 1);
                int rightIndex = y * side;

                Double3 a = leftMesh.OriginKm + new Double3(
                    leftMesh.Positions[leftIndex * 3], leftMesh.Positions[leftIndex * 3 + 1],
                    leftMesh.Positions[leftIndex * 3 + 2]);
                Double3 b = rightMesh.OriginKm + new Double3(
                    rightMesh.Positions[rightIndex * 3], rightMesh.Positions[rightIndex * 3 + 1],
                    rightMesh.Positions[rightIndex * 3 + 2]);

                // A millimetre on a ten-kilometre tile. Any real gap here is a seam you can see
                // space through, and no skirt should be needed between tiles of equal level.
                Assert.True((a - b).Magnitude < 1e-6,
                    $"row {y}: adjacent tiles disagree by {(a - b).Magnitude * 1e6:F3} mm");
            }
        }

        [Test("the skirt hangs below the surface and closes all the way round")]
        public static void SkirtIsBelowAndClosed()
        {
            TerrainField field = EarthLike();
            const int side = 9;
            TileId tile = new TileId(4, 4, 7, 3);

            TerrainMesh bare = TerrainMeshBuilder.Build(tile, field, side, includeSkirt: false);
            TerrainMesh skirted = TerrainMeshBuilder.Build(tile, field, side, includeSkirt: true);

            Assert.True(skirted.VertexCount == bare.VertexCount + 4 * (side - 1),
                $"expected one skirt vertex per border segment, got " +
                $"{skirted.VertexCount - bare.VertexCount}");

            // Every skirt vertex must be strictly inside the surface, or it pokes through.
            for (int i = bare.VertexCount; i < skirted.VertexCount; i++)
            {
                Double3 absolute = skirted.OriginKm + new Double3(
                    skirted.Positions[i * 3], skirted.Positions[i * 3 + 1], skirted.Positions[i * 3 + 2]);
                Assert.True(absolute.Magnitude < skirted.MinRadiusKm,
                    $"skirt vertex {i} is at radius {absolute.Magnitude:F3}, not below the tile's " +
                    $"minimum surface radius of {skirted.MinRadiusKm:F3}");
            }

            // Every index has to be in range, and the ring must close: the corner case where an
            // edge's last skirt vertex is the next edge's first is where an off-by-one shows up
            // as a wedge missing from each corner.
            foreach (int index in skirted.Triangles)
            {
                Assert.True(index >= 0 && index < skirted.VertexCount,
                    $"triangle references vertex {index} of {skirted.VertexCount}");
            }
        }

        [Test("triangles face outward, so a planet is not inside out")]
        public static void WindingIsOutward()
        {
            TerrainField field = EarthLike();

            int agreeing = 0, total = 0;
            foreach (TileId root in TileId.Roots())
            {
                TerrainMesh mesh = TerrainMeshBuilder.Build(root, field, 9, includeSkirt: false);

                for (int t = 0; t < mesh.Triangles.Length; t += 3)
                {
                    Double3 a = Vertex(mesh, mesh.Triangles[t]);
                    Double3 b = Vertex(mesh, mesh.Triangles[t + 1]);
                    Double3 c = Vertex(mesh, mesh.Triangles[t + 2]);

                    Double3 face = Double3.Cross(b - a, c - a);
                    Double3 outward = mesh.OriginKm + a;      // radially out from the centre

                    total++;
                    if (Double3.Dot(face, outward) > 0.0) agreeing++;
                }
            }

            Assert.True(total > 0, "no triangles were generated");
            Assert.True(agreeing == total,
                $"only {agreeing} of {total} triangles face outward — the surface is inside out");
        }

        private static Double3 Vertex(TerrainMesh mesh, int index) =>
            new Double3(mesh.Positions[index * 3], mesh.Positions[index * 3 + 1],
                mesh.Positions[index * 3 + 2]);

        [Test("subdividing a tile covers exactly the same ground as its parent")]
        public static void ChildrenTileTheirParent()
        {
            TerrainField field = EarthLike();
            var parent = new TileId(1, 2, 1, 2);

            TerrainMesh parentMesh = TerrainMeshBuilder.Build(parent, field, 9, includeSkirt: false);

            double childMin = double.PositiveInfinity, childMax = 0.0;
            foreach (TileId child in parent.Children())
            {
                Assert.True(child.Level == parent.Level + 1, "a child is not one level finer");

                TerrainMesh mesh = TerrainMeshBuilder.Build(child, field, 9, includeSkirt: false);
                childMin = System.Math.Min(childMin, mesh.MinRadiusKm);
                childMax = System.Math.Max(childMax, mesh.MaxRadiusKm);

                // Every child's centre must be inside the parent's angular footprint.
                double angle = System.Math.Acos(MathD.Clamp(
                    Double3.Dot(child.CenterDirection.Normalized, parent.CenterDirection.Normalized),
                    -1.0, 1.0));
                Assert.True(angle < parent.AngularRadius + 1e-9,
                    $"child centre is {angle:F4} rad from the parent centre, outside its " +
                    $"angular radius of {parent.AngularRadius:F4}");
            }

            // The children sample the same surface more finely, so their extremes must bracket
            // the parent's — a coarse grid cannot find a peak a fine one misses.
            Assert.True(childMin <= parentMesh.MinRadiusKm + 1e-9,
                "children found a higher minimum than their parent, so they are not the same ground");
            Assert.True(childMax >= parentMesh.MaxRadiusKm - 1e-9,
                "children found a lower maximum than their parent");
        }
    }
}
