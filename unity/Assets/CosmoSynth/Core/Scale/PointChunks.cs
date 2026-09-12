using System;
using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>
    /// One spatial box of a population, with its points expressed as float offsets from a double
    /// origin. CONTRACT §3.4.
    /// </summary>
    public sealed class PointChunk
    {
        /// <summary>The box centre, in the layer's own units (pc, kpc, Mpc), double.</summary>
        public Double3 OriginUnits;

        /// <summary>Radius of the sphere about <see cref="OriginUnits"/> that contains every point.</summary>
        public double RadiusUnits;

        /// <summary>Indices into the caller's original array, in this chunk's order.</summary>
        public int[] Indices;

        /// <summary>3 floats per point: the offset from <see cref="OriginUnits"/>, same order as <see cref="Indices"/>.</summary>
        public float[] OffsetsXyz;

        public int Count => Indices.Length;
    }

    /// <summary>
    /// Split a population into chunks small enough that a float offset from the chunk origin is
    /// exact to well under a pixel. CONTRACT §3.4.
    ///
    /// The problem this solves: a star 8 kpc away is 2.5e17 km from the eye, and a float cannot
    /// hold that with any useful precision. It does not have to. Nothing needs the absolute
    /// position on the GPU — it needs the direction and the distance from the eye — so the
    /// double subtraction happens on the CPU once per chunk, and the GPU only ever sees an offset
    /// inside a box a few tens of parsecs across.
    ///
    /// The guarantee: two float roundings give an angular error of at most
    /// <c>1.2e-7 * chunkRadius / pointDistance</c>, which is under half a pixel whenever the
    /// point is farther from the eye than 3.4e-4 of the chunk radius. Closer than that the point
    /// is faded out and the promoted double-precision star system stands in for it, which is why
    /// nearby-star parallax stays exact.
    ///
    /// The split is octree-wise on the bounding box rather than a k-d tree on the median: a box
    /// centre is a stable number that does not move when one point is added, and every layer here
    /// re-bakes its chunks whenever the catalogue changes.
    /// </summary>
    public static class PointChunks
    {
        /// <summary>The float-rounding bound the tests assert, relative to the chunk radius.</summary>
        public const double OffsetErrorBound = 6e-8;

        public static PointChunk[] Build(ReadOnlySpan<Double3> positionsUnits,
            int maxPerChunk = 4096, int maxChunks = 64)
        {
            if (positionsUnits.Length == 0) return new PointChunk[0];
            if (maxPerChunk < 1) maxPerChunk = 1;
            if (maxChunks < 1) maxChunks = 1;

            int[] all = new int[positionsUnits.Length];
            for (int i = 0; i < all.Length; i++) all[i] = i;

            // Copy out of the span: the recursion allocates lists, and a span cannot be captured.
            Double3[] points = new Double3[positionsUnits.Length];
            for (int i = 0; i < points.Length; i++) points[i] = positionsUnits[i];

            List<int[]> groups = Split(points, all, maxPerChunk, maxChunks);

            PointChunk[] chunks = new PointChunk[groups.Count];
            for (int c = 0; c < groups.Count; c++) chunks[c] = Finish(points, groups[c]);
            return chunks;
        }

        /// <summary>
        /// Breadth-first octant split: repeatedly take the largest group that does not fit and
        /// split it, stopping BEFORE a split could carry the total past the ceiling.
        ///
        /// Breadth-first for one reason. A depth-first recursion can only test the count on the
        /// way in, so by the time it unwinds it has already produced far more leaves than the
        /// ceiling allows -- a 16-chunk limit produced 29. A chunk is a draw call, so a ceiling
        /// that does not hold is a budget that does not hold.
        ///
        /// When the two limits conflict, <paramref name="maxChunks"/> wins and a chunk is allowed
        /// to be larger than <paramref name="maxPerChunk"/>. That is the right way round:
        /// <c>maxPerChunk</c> only bounds a mesh's vertex count, which <see cref="PointMesh"/>
        /// handles by widening its index format, while <c>maxChunks</c> is draw calls.
        /// </summary>
        private static List<int[]> Split(Double3[] points, int[] all, int maxPerChunk, int maxChunks)
        {
            List<int[]> open = new List<int[]> { all };
            List<int[]> closed = new List<int[]>();

            while (open.Count > 0)
            {
                int worst = 0;
                for (int i = 1; i < open.Count; i++)
                    if (open[i].Length > open[worst].Length) worst = i;

                int[] group = open[worst];

                // The largest group fits, so every group fits; or one more split could overshoot.
                if (group.Length <= maxPerChunk || group.Length < 2 ||
                    open.Count + closed.Count + 7 > maxChunks)
                {
                    closed.AddRange(open);
                    break;
                }

                Bounds(points, group, out Double3 min, out Double3 max);
                Double3 centre = (min + max) * 0.5;

                List<int>[] octants = new List<int>[8];
                for (int i = 0; i < 8; i++) octants[i] = new List<int>();
                for (int i = 0; i < group.Length; i++)
                {
                    Double3 p = points[group[i]];
                    int o = (p.x >= centre.x ? 1 : 0) | (p.y >= centre.y ? 2 : 0) |
                            (p.z >= centre.z ? 4 : 0);
                    octants[o].Add(group[i]);
                }

                int nonEmpty = 0;
                for (int i = 0; i < 8; i++) if (octants[i].Count > 0) nonEmpty++;

                open.RemoveAt(worst);

                // Every point in one octant means the box is degenerate: coincident points, or the
                // star catalogue's sentinel position shared by 146 rows. Splitting again would
                // never terminate, and there is nothing to gain -- one box, one chunk.
                if (nonEmpty <= 1) { closed.Add(group); continue; }

                for (int i = 0; i < 8; i++)
                    if (octants[i].Count > 0) open.Add(octants[i].ToArray());
            }

            return closed;
        }

        private static PointChunk Finish(Double3[] points, int[] group)
        {
            Bounds(points, group, out Double3 min, out Double3 max);
            Double3 origin = (min + max) * 0.5;

            float[] offsets = new float[group.Length * 3];
            double radius = 0.0;
            for (int i = 0; i < group.Length; i++)
            {
                Double3 d = points[group[i]] - origin;
                offsets[i * 3 + 0] = (float)d.x;
                offsets[i * 3 + 1] = (float)d.y;
                offsets[i * 3 + 2] = (float)d.z;
                double m = d.Magnitude;
                if (m > radius) radius = m;
            }

            return new PointChunk
            {
                OriginUnits = origin,
                RadiusUnits = radius,
                Indices = group,
                OffsetsXyz = offsets,
            };
        }

        private static void Bounds(Double3[] points, int[] group, out Double3 min, out Double3 max)
        {
            min = points[group[0]];
            max = min;
            for (int i = 1; i < group.Length; i++)
            {
                min = Double3.Min(min, points[group[i]]);
                max = Double3.Max(max, points[group[i]]);
            }
        }

        /// <summary>
        /// The eye in one chunk's units, with the subtraction done in double before the cast.
        /// Getting this wrong is not a rounding error: it is the whole reason the chunks exist.
        /// </summary>
        public static Double3 EyeInChunk(in PointChunk chunk, Double3 eyeLayerUnits) =>
            eyeLayerUnits - chunk.OriginUnits;

        /// <summary>
        /// Does this chunk's bounding sphere intersect a view cone of half-angle
        /// <paramref name="halfAngleRad"/> about <paramref name="forward"/> from the eye?
        /// O(chunks) per frame, which is 64 at worst.
        /// </summary>
        public static bool InCone(in PointChunk chunk, Double3 eyeLayerUnits, Double3 forward,
            double halfAngleRad)
        {
            Double3 rel = chunk.OriginUnits - eyeLayerUnits;
            double d = rel.Magnitude;
            if (d <= chunk.RadiusUnits) return true;              // the eye is inside the box
            double toCentre = Math.Acos(Math.Min(1.0, Math.Max(-1.0,
                Double3.Dot(rel / d, forward.Normalized))));
            double angularRadius = Math.Asin(Math.Min(1.0, chunk.RadiusUnits / d));
            return toCentre - angularRadius <= halfAngleRad;
        }
    }
}
