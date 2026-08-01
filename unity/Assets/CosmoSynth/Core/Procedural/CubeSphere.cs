using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>
    /// Cube-sphere mapping and quadtree tile addressing — the spatial index a planet's surface
    /// is built and streamed on.
    ///
    /// Why a cube and not a latitude/longitude grid: a lat/long grid has a coordinate
    /// singularity at each pole, where cells collapse to zero width and every algorithm that
    /// touches them needs a special case. Six square faces have no singularity anywhere, tile
    /// uniformly, and give each face its own trivially-subdivided quadtree. The cost is that
    /// most planetary datasets arrive on a lat/long grid and must be resampled once, offline.
    /// </summary>
    public static class CubeSphere
    {
        public const int FaceCount = 6;

        /// <summary>Outward normal of each cube face.</summary>
        private static readonly Double3[] FaceNormals =
        {
            new Double3(1, 0, 0), new Double3(-1, 0, 0),
            new Double3(0, 1, 0), new Double3(0, -1, 0),
            new Double3(0, 0, 1), new Double3(0, 0, -1)
        };

        /// <summary>In-face axis along increasing u.</summary>
        private static readonly Double3[] FaceRight =
        {
            new Double3(0, 0, -1), new Double3(0, 0, 1),
            new Double3(1, 0, 0), new Double3(1, 0, 0),
            new Double3(1, 0, 0), new Double3(-1, 0, 0)
        };

        /// <summary>In-face axis along increasing v.</summary>
        private static readonly Double3[] FaceUp =
        {
            new Double3(0, 1, 0), new Double3(0, 1, 0),
            new Double3(0, 0, -1), new Double3(0, 0, 1),
            new Double3(0, 1, 0), new Double3(0, 1, 0)
        };

        public static readonly string[] FaceNames = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };

        public static Double3 FaceNormal(int face) => FaceNormals[face];

        /// <summary>
        /// Tangent warp that evens out cell areas across a face.
        ///
        /// Projecting a uniformly divided cube face onto the sphere leaves centre cells nearly
        /// twice the angular size of corner cells, so a uniform tile grid would carry visibly
        /// different detail in the middle of a face than at its edges. Pre-warping the face
        /// coordinate by tan(u*pi/4) removes almost all of that. Unlike the "spherified cube"
        /// polynomial, this warp has a closed-form inverse, which the surface-to-tile lookup
        /// needs on every terrain query.
        /// </summary>
        public static double Warp(double u) => System.Math.Tan(u * MathD.Pi * 0.25);

        /// <summary>Inverse of <see cref="Warp"/>.</summary>
        public static double Unwarp(double warped) => System.Math.Atan(warped) * 4.0 / MathD.Pi;

        /// <summary>
        /// Unit direction for a point on a cube face, with face coordinates in [-1,1].
        /// </summary>
        public static Double3 FaceToDirection(int face, double u, double v, bool applyWarp = true)
        {
            if (face < 0 || face >= FaceCount) throw new ArgumentOutOfRangeException(nameof(face));

            if (applyWarp)
            {
                u = Warp(u);
                v = Warp(v);
            }

            return (FaceNormals[face] + FaceRight[face] * u + FaceUp[face] * v).Normalized;
        }

        /// <summary>
        /// Face index and face coordinates of a direction — the inverse of
        /// <see cref="FaceToDirection"/>.
        /// </summary>
        public static void DirectionToFace(Double3 direction, out int face, out double u, out double v,
            bool applyWarp = true)
        {
            Double3 d = direction.Normalized;
            double ax = System.Math.Abs(d.x), ay = System.Math.Abs(d.y), az = System.Math.Abs(d.z);

            // The dominant component names the face the direction pierces.
            if (ax >= ay && ax >= az) face = d.x >= 0 ? 0 : 1;
            else if (ay >= az) face = d.y >= 0 ? 2 : 3;
            else face = d.z >= 0 ? 4 : 5;

            double axial = Double3.Dot(d, FaceNormals[face]);
            if (axial <= 0.0) { u = 0.0; v = 0.0; return; }

            u = Double3.Dot(d, FaceRight[face]) / axial;
            v = Double3.Dot(d, FaceUp[face]) / axial;

            if (applyWarp)
            {
                u = Unwarp(u);
                v = Unwarp(v);
            }
        }

        /// <summary>Total number of tiles across one face at a subdivision level.</summary>
        public static int TilesPerSide(int level) => 1 << level;

        /// <summary>Total number of tiles over the whole sphere at a level.</summary>
        public static long TotalTiles(int level)
        {
            long side = TilesPerSide(level);
            return FaceCount * side * side;
        }
    }

    /// <summary>
    /// Address of one quadtree tile on a cube-sphere: which face, how deep, and where in the
    /// face's grid. Small and comparable, so it works as a dictionary key for the streaming
    /// cache without allocating.
    /// </summary>
    [Serializable]
    public readonly struct TileId : IEquatable<TileId>
    {
        public int Face { get; }
        public int Level { get; }
        public int X { get; }
        public int Y { get; }

        public TileId(int face, int level, int x, int y)
        {
            if (face < 0 || face >= CubeSphere.FaceCount) throw new ArgumentOutOfRangeException(nameof(face));
            if (level < 0 || level > 30) throw new ArgumentOutOfRangeException(nameof(level), "Level must be 0..30.");

            int side = CubeSphere.TilesPerSide(level);
            if (x < 0 || x >= side) throw new ArgumentOutOfRangeException(nameof(x), $"x must be 0..{side - 1} at level {level}.");
            if (y < 0 || y >= side) throw new ArgumentOutOfRangeException(nameof(y), $"y must be 0..{side - 1} at level {level}.");

            Face = face;
            Level = level;
            X = x;
            Y = y;
        }

        /// <summary>The six root tiles, one per cube face.</summary>
        public static IEnumerable<TileId> Roots()
        {
            for (int face = 0; face < CubeSphere.FaceCount; face++) yield return new TileId(face, 0, 0, 0);
        }

        public bool IsRoot => Level == 0;

        public TileId Parent =>
            Level == 0 ? this : new TileId(Face, Level - 1, X >> 1, Y >> 1);

        /// <summary>The four children, in the order (0,0) (1,0) (0,1) (1,1).</summary>
        public TileId[] Children() => new[]
        {
            new TileId(Face, Level + 1, X * 2, Y * 2),
            new TileId(Face, Level + 1, X * 2 + 1, Y * 2),
            new TileId(Face, Level + 1, X * 2, Y * 2 + 1),
            new TileId(Face, Level + 1, X * 2 + 1, Y * 2 + 1)
        };

        /// <summary>Face-coordinate bounds of this tile, each in [-1,1].</summary>
        public void Bounds(out double minU, out double minV, out double maxU, out double maxV)
        {
            double size = 2.0 / CubeSphere.TilesPerSide(Level);
            minU = -1.0 + X * size;
            minV = -1.0 + Y * size;
            maxU = minU + size;
            maxV = minV + size;
        }

        /// <summary>Face-coordinate width of the tile.</summary>
        public double FaceSize => 2.0 / CubeSphere.TilesPerSide(Level);

        /// <summary>Direction through the tile's centre.</summary>
        public Double3 CenterDirection
        {
            get
            {
                Bounds(out double minU, out double minV, out double maxU, out double maxV);
                return CubeSphere.FaceToDirection(Face, 0.5 * (minU + maxU), 0.5 * (minV + maxV));
            }
        }

        /// <summary>The tile's four corner directions, counter-clockwise from (minU,minV).</summary>
        public Double3[] CornerDirections()
        {
            Bounds(out double minU, out double minV, out double maxU, out double maxV);
            return new[]
            {
                CubeSphere.FaceToDirection(Face, minU, minV),
                CubeSphere.FaceToDirection(Face, maxU, minV),
                CubeSphere.FaceToDirection(Face, maxU, maxV),
                CubeSphere.FaceToDirection(Face, minU, maxV)
            };
        }

        /// <summary>Direction at normalised coordinates within the tile, both in [0,1].</summary>
        public Double3 DirectionAt(double localU, double localV)
        {
            Bounds(out double minU, out double minV, out double maxU, out double maxV);
            return CubeSphere.FaceToDirection(Face,
                MathD.Lerp(minU, maxU, localU),
                MathD.Lerp(minV, maxV, localV));
        }

        /// <summary>Approximate ground width of the tile on a body of the given radius, km.</summary>
        public double GroundSizeKm(double bodyRadiusKm)
        {
            Double3[] corners = CornerDirections();
            double diagonal = Double3.Angle(corners[0], corners[2]);
            // Convert the diagonal of the spherical quad to the side of an equivalent square.
            return bodyRadiusKm * diagonal / System.Math.Sqrt(2.0);
        }

        /// <summary>
        /// Geometric error of representing this tile at its own level rather than subdividing,
        /// in km. Halves with each level, which is the quantity the LOD selector compares
        /// against a screen-space pixel budget.
        /// </summary>
        public double GeometricErrorKm(double bodyRadiusKm, double maxTerrainReliefKm)
        {
            // Sagitta of the spherical cap the tile spans, plus the unresolved relief.
            double halfAngle = 0.5 * Double3.Angle(CornerDirections()[0], CornerDirections()[2]);
            double sagitta = bodyRadiusKm * (1.0 - System.Math.Cos(halfAngle));
            return sagitta + maxTerrainReliefKm / CubeSphere.TilesPerSide(Level);
        }

        /// <summary>
        /// Whether this tile should be subdivided, from the standard screen-space-error test:
        /// project the tile's geometric error to pixels and compare against a budget.
        /// </summary>
        public bool ShouldSubdivide(double distanceKm, double bodyRadiusKm, double maxTerrainReliefKm,
            double verticalFovRad, int verticalPixels, double pixelErrorBudget = 2.0, int maxLevel = 20)
        {
            if (Level >= maxLevel) return false;
            if (distanceKm <= 0.0) return true;

            double errorKm = GeometricErrorKm(bodyRadiusKm, maxTerrainReliefKm);
            double errorPixels = ScaleSpace.ApparentDiameterPixels(
                errorKm / distanceKm, verticalFovRad, verticalPixels);
            return errorPixels > pixelErrorBudget;
        }

        /// <summary>Locate the tile containing a direction at a given level.</summary>
        public static TileId FromDirection(Double3 direction, int level)
        {
            CubeSphere.DirectionToFace(direction, out int face, out double u, out double v);
            int side = CubeSphere.TilesPerSide(level);

            int x = (int)MathD.Clamp(System.Math.Floor((u + 1.0) * 0.5 * side), 0, side - 1);
            int y = (int)MathD.Clamp(System.Math.Floor((v + 1.0) * 0.5 * side), 0, side - 1);
            return new TileId(face, level, x, y);
        }

        /// <summary>Half-angle of the spherical cap that encloses this tile, radians.</summary>
        public double AngularRadius
        {
            get
            {
                Double3 centre = CenterDirection;
                double largest = 0.0;
                foreach (Double3 corner in CornerDirections())
                {
                    largest = System.Math.Max(largest, Double3.Angle(centre, corner));
                }
                return largest;
            }
        }

        /// <summary>
        /// Whether any part of this tile could be above the viewer's horizon.
        ///
        /// The tile has to be treated as a region, not as a handful of sample points. From
        /// 100 km up, the horizon is only about 10 degrees wide, while a level-0 tile spans a
        /// whole cube face — so the viewer can be directly above the tile's interior while its
        /// centre and all four corners lie outside the visible cap. Sampling points would cull
        /// it, and culling a tile discards its entire subtree, punching a hole in the terrain
        /// precisely underneath the viewer.
        ///
        /// Comparing the angle to the tile's centre against the horizon plus the tile's own
        /// angular radius is conservative: it can keep a tile that turns out to be invisible,
        /// which costs a little work, but it can never drop one that is visible. As
        /// subdivision proceeds the angular radius shrinks and the test tightens automatically.
        /// </summary>
        public bool IsPotentiallyVisible(Double3 viewerFromCentreKm, double bodyRadiusKm)
        {
            double viewerDistance = viewerFromCentreKm.Magnitude;

            // On or inside the surface there is no horizon to cull against.
            if (viewerDistance <= bodyRadiusKm) return true;

            double horizonHalfAngle = MathD.SafeAcos(bodyRadiusKm / viewerDistance);
            double angleToTileCentre = Double3.Angle(viewerFromCentreKm, CenterDirection);

            return angleToTileCentre <= horizonHalfAngle + AngularRadius;
        }

        /// <summary>
        /// Select the set of visible tiles by descending the quadtree, subdividing wherever
        /// the screen-space error exceeds the budget. This is the whole LOD algorithm.
        /// </summary>
        public static List<TileId> SelectVisible(Double3 viewerDirectionFromCentre, double viewerDistanceFromCentreKm,
            double bodyRadiusKm, double maxTerrainReliefKm, double verticalFovRad, int verticalPixels,
            double pixelErrorBudget = 2.0, int maxLevel = 12, int maxTiles = 4096)
        {
            var selected = new List<TileId>();
            var pending = new Stack<TileId>();
            foreach (var root in Roots()) pending.Push(root);

            Double3 viewer = viewerDirectionFromCentre.Normalized * viewerDistanceFromCentreKm;

            while (pending.Count > 0 && selected.Count < maxTiles)
            {
                TileId tile = pending.Pop();

                if (!tile.IsPotentiallyVisible(viewer, bodyRadiusKm)) continue;

                double distance = (viewer - tile.CenterDirection * bodyRadiusKm).Magnitude;
                if (tile.ShouldSubdivide(distance, bodyRadiusKm, maxTerrainReliefKm,
                        verticalFovRad, verticalPixels, pixelErrorBudget, maxLevel))
                {
                    foreach (var child in tile.Children()) pending.Push(child);
                }
                else
                {
                    selected.Add(tile);
                }
            }

            return selected;
        }

        public bool Equals(TileId other) =>
            Face == other.Face && Level == other.Level && X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is TileId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Face;
                hash = (hash * 397) ^ Level;
                hash = (hash * 397) ^ X;
                hash = (hash * 397) ^ Y;
                return hash;
            }
        }

        public static bool operator ==(TileId a, TileId b) => a.Equals(b);
        public static bool operator !=(TileId a, TileId b) => !a.Equals(b);

        public override string ToString() => $"{CubeSphere.FaceNames[Face]}/L{Level}/{X},{Y}";
    }
}
