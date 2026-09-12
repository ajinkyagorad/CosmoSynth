using System.Collections.Generic;

namespace CosmoSynth.Core
{
    /// <summary>How one node's own renderers are placed this frame.</summary>
    public enum GroupMode : byte
    {
        /// <summary>An ancestor is Rigid and owns this node; it inherits the ancestor's similarity.</summary>
        Collapsed,

        /// <summary>Placed by a similarity about the centre eye.</summary>
        Rigid,

        /// <summary>The eye is near a body that is large on screen: placed at true scale, compressed per vertex.</summary>
        Envelope,

        /// <summary>A layer volume that may contain the eye.</summary>
        Enclosing
    }

    /// <summary>
    /// One node per catalogue body (parents before children) plus one per layer group, filled
    /// every frame before <see cref="ScaledSpace.Solve"/> runs.
    /// </summary>
    public struct GroupNode
    {
        /// <summary>Index of the parent node, -1 for a root. Parents must precede children.</summary>
        public int Parent;

        /// <summary>Absolute ICRF km of the subtree root body, or of the layer group's centre.</summary>
        public Double3 AnchorKm;

        /// <summary>Bounding radius of the whole subtree about the anchor, km (drawn extents, +10 %).</summary>
        public double RadiusKm;

        /// <summary>The anchor body's own drawn radius, including rings and atmosphere, km.</summary>
        public double LeafRadiusKm;

        /// <summary>True when this node has children that would collapse into it.</summary>
        public bool HasChildren;

        /// <summary>True when this node's own renderers write depth this frame.</summary>
        public bool HasOpaque;

        /// <summary>Layer volumes and the CMB object form only: this group may contain the eye.</summary>
        public bool MayEnclose;
    }

    /// <summary>The solver's answer for one node.</summary>
    public struct GroupPlacement
    {
        public GroupMode Mode;

        /// <summary>Rigid with the whole subtree collapsed into it. False means the group dissolved.</summary>
        public bool OwnsSubtree;

        /// <summary>Node whose similarity applies (self, or a rigid ancestor); -1 for Envelope.</summary>
        public int RigidOwner;

        /// <summary>The similarity factor s. Always 1 for Envelope.</summary>
        public double Scale;

        /// <summary>Rendered distance to the near and far extremes of this node's own geometry, metres.</summary>
        public double RenderedNearMetres, RenderedFarMetres;

        /// <summary>True when the inter-group pass moved this group toward the eye.</summary>
        public bool Pushed;
    }

    /// <summary>
    /// The single account of distance in CosmoSynth: how thirty orders of magnitude reach one
    /// 24-bit depth buffer.
    ///
    /// Near content is exact. Past <see cref="IdentityRadiusMetres"/> the distance to the eye is
    /// mapped by <c>f(d) = d0 (1 + ln(d/d0))</c>, which spends the same 23.03 m of rendered depth
    /// on every decade — so every scale of structure gets an equal share, which is what a
    /// thirty-decade universe needs. The map is applied as <b>one similarity per group about the
    /// centre eye</b>, never per body: scaling each body by its own f(d)/d moves a moon and its
    /// planet by different factors and a transiting moon disappears behind the planet it is in
    /// front of. <see cref="Solve"/> decides which groups are rigid, which dissolve, and pushes
    /// overlapping groups apart by at least two depth quanta.
    ///
    /// Everything here is arithmetic on doubles with no engine dependency, so every claim above
    /// is a unit test rather than a screenshot.
    /// </summary>
    public static class ScaledSpace
    {
        /// <summary>Inside this rendered distance placement is exact (s = 1): the room, the hologram, the wrist.</summary>
        public const double IdentityRadiusMetres = 10.0;

        /// <summary>A rigid group's rendered-radius cap, entering and leaving (hysteresis).</summary>
        public const double RigidEnterMetres = 9.0;

        /// <summary>The upper edge of the hysteresis band: a group that already owns its subtree keeps it to here.</summary>
        public const double RigidLeaveMetres = 11.0;

        /// <summary>Camera near plane. Hands and wrist panels come closer than the usual 0.3.</summary>
        public const double NearPlaneMetres = 0.05;

        /// <summary>Camera far plane: at least 1.33x the largest placed distance (f(1e30) + H = 689 m).</summary>
        public const double FarPlaneMetres = 2000.0;

        /// <summary>Radius of the per-eye sky sphere, and the cap for enclosing volumes.</summary>
        public const double SkyRadiusMetres = 1500.0;

        /// <summary>Expected eye depth format, D24_UNORM_S8. Logged on the device; a 16-bit format is a failure.</summary>
        public const int DepthBits = 24;

        /// <summary>Inter-group clearance, in depth quanta.</summary>
        public const double PushMarginQuanta = 2.0;

        /// <summary>A push never brings a group's near point closer to the eye than this.</summary>
        public const double NearPointFloorMetres = 5.0;

        /// <summary>
        /// The distance map f. Identity inside d0, logarithmic beyond it, C-1 at the join.
        ///
        /// Strictly increasing, so depth order along any ray is preserved exactly; each decade
        /// costs d0 ln 10 = 23.03 m of rendered depth.
        /// </summary>
        public static double Compress(double metres)
        {
            if (metres <= IdentityRadiusMetres) return metres;
            return IdentityRadiusMetres * (1.0 + System.Math.Log(metres / IdentityRadiusMetres));
        }

        /// <summary>The inverse of <see cref="Compress"/>.</summary>
        public static double Decompress(double renderedMetres)
        {
            if (renderedMetres <= IdentityRadiusMetres) return renderedMetres;
            return IdentityRadiusMetres * System.Math.Exp(renderedMetres / IdentityRadiusMetres - 1.0);
        }

        /// <summary>
        /// The similarity factor s = f(d)/d for a group whose anchor is d metres from the eye.
        /// Exactly 1 inside d0, so near content is untouched.
        /// </summary>
        public static double GroupScale(double metres)
        {
            if (metres <= IdentityRadiusMetres || metres <= 0.0) return 1.0;
            return Compress(metres) / metres;
        }

        /// <summary>
        /// The size of one depth quantum at view distance z, for a UNORM depth buffer:
        /// <c>z^2 / (near * 2^bits)</c>. Reversed-Z does not change this for UNORM.
        /// </summary>
        public static double DepthQuantum(double viewMetres, int bits = DepthBits,
            double nearMetres = NearPlaneMetres)
        {
            if (viewMetres <= 0.0 || nearMetres <= 0.0) return 0.0;
            return viewMetres * viewMetres / (nearMetres * System.Math.Pow(2.0, bits));
        }

        /// <summary>
        /// CPU replica of the shader's <c>CS_CompressWS</c>, for picking and for the gates:
        /// eye-relative metres in, eye-relative metres out. The direction is exact.
        /// </summary>
        public static Double3 CompressRelative(Double3 eyeRelativeMetres)
        {
            double d = eyeRelativeMetres.Magnitude;
            if (d <= IdentityRadiusMetres || d <= 0.0) return eyeRelativeMetres;
            return eyeRelativeMetres * (Compress(d) / d);
        }

        /// <summary>
        /// Assign a mode and a scale to every node, then push overlapping groups apart.
        ///
        /// <paramref name="previous"/> may be null on the first frame; when it is supplied it
        /// supplies the hysteresis, so a group sitting on the 9 m cut does not flip mode with
        /// every frame of head motion.
        ///
        /// Returns the number of unresolved conflicts: groups whose push was clamped by
        /// <see cref="NearPointFloorMetres"/> and which may therefore still be mis-ordered
        /// against the group in front of them. The render line prints it, and the play-mode
        /// gate requires zero.
        /// </summary>
        public static int Solve(IReadOnlyList<GroupNode> nodes, Double3 eyeKm, double unitsPerKm,
            GroupPlacement[] previous, GroupPlacement[] result)
        {
            if (nodes == null) return 0;
            int n = nodes.Count;
            // Not a return of 0. Zero is the value that means "no conflicts", and both
            // RenderDiagnostics.IsHealthy and the play-mode `push` assertion key off exactly that
            // — so a caller that mis-sized the array would silently re-use last frame's placements
            // and be reported healthy. A short result array is a programming error, not a runtime
            // condition, and it says so.
            if (result == null || result.Length < n)
            {
                throw new System.ArgumentException(
                    $"result must hold at least {n} placements, not {(result == null ? 0 : result.Length)}",
                    nameof(result));
            }

            for (int i = 0; i < n; i++)
            {
                GroupNode node = nodes[i];
                double d = Double3.Distance(node.AnchorKm, eyeKm) * unitsPerKm;   // D, rendered metres
                double r = node.RadiusKm * unitsPerKm;                            // R
                double rLeaf = node.LeafRadiusKm * unitsPerKm;                    // R_leaf

                int parent = node.Parent;
                if (parent >= 0 && parent < i &&
                    (result[parent].Mode == GroupMode.Collapsed ||
                     (result[parent].Mode == GroupMode.Rigid && result[parent].OwnsSubtree)))
                {
                    double s = result[parent].Scale;
                    result[i] = new GroupPlacement
                    {
                        Mode = GroupMode.Collapsed,
                        OwnsSubtree = true,
                        RigidOwner = result[parent].RigidOwner,
                        Scale = s,
                        RenderedNearMetres = s * System.Math.Max(0.0, d - rLeaf),
                        RenderedFarMetres = s * (d + rLeaf)
                    };
                    continue;
                }

                if (node.MayEnclose)
                {
                    // A volume the eye may be inside. When it is small enough to behave like any
                    // other far group it gets the ordinary similarity; otherwise it is scaled to
                    // sit inside the far plane, which is the only requirement on something whose
                    // raymarch runs in its own object space.
                    double gs = GroupScale(d);
                    double s = (d > r && gs * r <= RigidEnterMetres)
                        ? gs
                        : System.Math.Min(1.0, 0.75 * FarPlaneMetres / System.Math.Max(1e-9, d + r));
                    result[i] = new GroupPlacement
                    {
                        Mode = GroupMode.Enclosing,
                        OwnsSubtree = true,
                        RigidOwner = i,
                        Scale = s,
                        RenderedNearMetres = System.Math.Max(0.0, s * (d - r)),
                        RenderedFarMetres = System.Math.Min(SkyRadiusMetres, s * (d + r))
                    };
                    continue;
                }

                bool wasOwner = previous != null && previous.Length > i &&
                                previous[i].Mode == GroupMode.Rigid && previous[i].OwnsSubtree;
                bool wasRigid = previous != null && previous.Length > i &&
                                previous[i].Mode == GroupMode.Rigid;

                double scale = GroupScale(d);

                if (node.HasChildren && d > r && scale * r <= (wasOwner ? RigidLeaveMetres : RigidEnterMetres))
                {
                    result[i] = new GroupPlacement
                    {
                        Mode = GroupMode.Rigid,
                        OwnsSubtree = true,
                        RigidOwner = i,
                        Scale = scale,
                        RenderedNearMetres = scale * (d - r),
                        RenderedFarMetres = scale * (d + r)
                    };
                }
                else if (d > rLeaf && scale * rLeaf <= (wasRigid ? RigidLeaveMetres : RigidEnterMetres))
                {
                    result[i] = new GroupPlacement
                    {
                        Mode = GroupMode.Rigid,
                        OwnsSubtree = false,
                        RigidOwner = i,
                        Scale = scale,
                        RenderedNearMetres = scale * (d - rLeaf),
                        RenderedFarMetres = scale * (d + rLeaf)
                    };
                }
                else
                {
                    // The eye is close to something big: a similarity would put the ground
                    // 36 micrometres away, inside the near plane. Draw it at true scale and let
                    // the vertex shader compress each vertex about the eye instead.
                    result[i] = new GroupPlacement
                    {
                        Mode = GroupMode.Envelope,
                        OwnsSubtree = false,
                        RigidOwner = -1,
                        Scale = 1.0,
                        RenderedNearMetres = Compress(System.Math.Max(0.0, d - rLeaf)),
                        RenderedFarMetres = Compress(d + rLeaf)
                    };
                }
            }

            return Push(nodes, eyeKm, unitsPerKm, result, n);
        }

        /// <summary>
        /// Order the groups against each other.
        ///
        /// A similarity scales a group's own depth extent by the same s as its distance, but f
        /// compresses the <i>gaps between</i> groups by far more, so two groups decades apart can
        /// arrive interleaved. Nothing about the compression can fix that — it is what the
        /// compression is for — so inter-group order is enforced by construction instead: working
        /// from the far side inward, any group that would poke through the near face of a group
        /// behind it is scaled down until it clears it by two depth quanta.
        ///
        /// Pushing changes only depth. Screen position and angular size are untouched, because a
        /// similarity about the eye cannot change either.
        /// </summary>
        private static int Push(IReadOnlyList<GroupNode> nodes, Double3 eyeKm, double unitsPerKm,
            GroupPlacement[] result, int n)
        {
            // Owners that write depth: the ones that can occlude each other.
            var order = new List<int>();
            for (int i = 0; i < n; i++)
            {
                GroupMode mode = result[i].Mode;
                if (mode != GroupMode.Rigid && mode != GroupMode.Envelope) continue;
                if (!HasOpaqueIncludingCollapsed(nodes, result, n, i)) continue;
                order.Add(i);
            }

            var distance = new double[n];
            var direction = new Double3[n];
            var extent = new double[n];
            for (int k = 0; k < order.Count; k++)
            {
                int i = order[k];
                Double3 offset = nodes[i].AnchorKm - eyeKm;
                distance[i] = offset.Magnitude * unitsPerKm;
                direction[i] = distance[i] > 0.0 ? offset.Normalized : Double3.UnitZ;
                extent[i] = (result[i].OwnsSubtree ? nodes[i].RadiusKm : nodes[i].LeafRadiusKm) * unitsPerKm;
            }

            order.Sort((a, b) => distance[b].CompareTo(distance[a]));   // farthest first

            int conflicts = 0;
            for (int k = 0; k < order.Count; k++)
            {
                int g = order[k];
                if (result[g].Mode == GroupMode.Envelope) continue;      // a fixed obstacle, never moved

                double dG = distance[g], rG = extent[g];
                if (dG <= rG) continue;                                  // the eye is inside: not a far group

                double sG = result[g].Scale;
                double unpushedNear = sG * (dG - rG);
                if (unpushedNear < NearPointFloorMetres) continue;       // room content: leave it alone

                bool pushed = false;
                for (int j = 0; j < k; j++)
                {
                    int h = order[j];
                    if (!Overlaps(direction[g], direction[h], rG, dG, extent[h], distance[h])) continue;

                    double zNearH = result[h].RenderedNearMetres;
                    double limit = zNearH - PushMarginQuanta * DepthQuantum(zNearH);
                    if (sG * (dG + rG) > limit)
                    {
                        sG = limit / (dG + rG);
                        pushed = true;
                    }
                }

                if (sG * (dG - rG) < NearPointFloorMetres)
                {
                    // Clearing the group behind would drag this one into the room. Refuse, and
                    // report it: the render line shows the count and the play-mode gate fails on
                    // anything but zero, so a scale that cannot be ordered is never silent.
                    sG = NearPointFloorMetres / (dG - rG);
                    conflicts++;
                }

                result[g].Scale = sG;
                result[g].Pushed = pushed;
                result[g].RenderedNearMetres = sG * (dG - rG);
                result[g].RenderedFarMetres = sG * (dG + rG);

                // Descendants collapsed into this owner ride its similarity — ALL of it.
                //
                // Propagating the new scale and leaving the depth extents at their pre-push values
                // leaves a Collapsed node reporting a scale and a depth that disagree, and those
                // fields are read for exactly those nodes: ScaleRenderer folds every drawn view's
                // RenderedFarMetres into the far-plane headroom the render line prints. Recomputed
                // from the node rather than rescaled, so one expression covers a push, a re-push
                // and a first placement.
                if (result[g].OwnsSubtree)
                {
                    for (int i = g + 1; i < n; i++)
                    {
                        if (result[i].Mode != GroupMode.Collapsed || result[i].RigidOwner != g) continue;

                        result[i].Scale = sG;
                        double di = Double3.Distance(nodes[i].AnchorKm, eyeKm) * unitsPerKm;
                        double ri = nodes[i].LeafRadiusKm * unitsPerKm;
                        result[i].RenderedNearMetres = sG * System.Math.Max(0.0, di - ri);
                        result[i].RenderedFarMetres = sG * (di + ri);
                    }
                }
            }

            return conflicts;
        }

        /// <summary>Two groups share screen area when their angular discs meet, with a milliradian of slack.</summary>
        private static bool Overlaps(Double3 uG, Double3 uH, double rG, double dG, double rH, double dH)
        {
            double angle = Double3.Angle(uG, uH);
            return angle < MathD.SafeAsin(rG / System.Math.Max(rG, dG)) +
                           MathD.SafeAsin(rH / System.Math.Max(rH, dH)) + 1e-3;
        }

        private static bool HasOpaqueIncludingCollapsed(IReadOnlyList<GroupNode> nodes,
            GroupPlacement[] result, int n, int owner)
        {
            if (nodes[owner].HasOpaque) return true;
            if (!result[owner].OwnsSubtree) return false;
            for (int i = owner + 1; i < n; i++)
            {
                if (result[i].Mode == GroupMode.Collapsed && result[i].RigidOwner == owner &&
                    nodes[i].HasOpaque) return true;
            }
            return false;
        }
    }
}
