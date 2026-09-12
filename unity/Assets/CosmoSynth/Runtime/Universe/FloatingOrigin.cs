using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The one place Unity vectors meet ICRF kilometres.
    ///
    /// Absolute positions live in double kilometres about the solar system barycentre. This maps
    /// them into the universe host's local metres: subtract the origin in <b>double</b>, apply the
    /// single axis permutation, then scale. Because the subtraction happens before anything is
    /// cast to float, float error has nowhere to accumulate and there is no rebase — the camera
    /// simply stays in the room and the movers change <see cref="OriginKm"/> and
    /// <see cref="UnitsPerKm"/>.
    ///
    /// The rebase machinery that used to live here was dead: it only ran when <c>Focus</c> was
    /// set, and the bootstrap set it to null. What it left behind was worse than nothing — an
    /// identity mapping between a right-handed frame and a left-handed one, which is a mirror.
    /// A mirrored solar system orbits along the correct paths and rotates backwards, and there is
    /// no screenshot in which that is visible.
    ///
    /// Gate G0d enforces that no other file reorders an axis.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("CosmoSynth/Floating Origin")]
    public sealed class FloatingOrigin : MonoBehaviour
    {
        /// <summary>The absolute position, in km, that maps to the universe host's local origin.</summary>
        public Double3 OriginKm { get; private set; } = Double3.Zero;

        /// <summary>Room metres emitted per real kilometre. Clamped to <see cref="ScaleLimits"/>.</summary>
        public double UnitsPerKm { get; private set; } = 1.0;

        /// <summary>The current absolute-to-universe mapping, as engine-free arithmetic.</summary>
        public ScaleSpace.RenderTransform Transform => new ScaleSpace.RenderTransform(OriginKm, UnitsPerKm);

        /// <summary>Absolute ICRF kilometres to universe-host-local metres.</summary>
        public Vector3 ToRender(Double3 absoluteKm)
        {
            Double3 local = Transform.ToRender(absoluteKm);
            return new Vector3((float)local.x, (float)local.y, (float)local.z);
        }

        /// <summary>Universe-host-local metres back to absolute ICRF kilometres.</summary>
        public Double3 ToAbsolute(Vector3 universeLocal) =>
            Transform.ToAbsolute(new Double3(universeLocal.x, universeLocal.y, universeLocal.z));

        /// <summary>An ICRF direction in universe-local axes.</summary>
        public Vector3 MapDirection(Double3 icrf)
        {
            Double3 mapped = Transform.MapDirection(icrf);
            return new Vector3((float)mapped.x, (float)mapped.y, (float)mapped.z);
        }

        /// <summary>A universe-local direction back in ICRF axes.</summary>
        public Double3 UnmapDirection(Vector3 universeLocal) =>
            Transform.UnmapDirection(new Double3(universeLocal.x, universeLocal.y, universeLocal.z));

        /// <summary>
        /// A body-fixed-to-ICRF rotation as a Unity rotation.
        ///
        /// P M P has determinant +1, so it is a proper rotation and converts directly. Universe
        /// local +y is the body's pole, which is what the sphere's UV layout assumes.
        /// </summary>
        public Quaternion MapRotation(Matrix3d bodyToIcrf)
        {
            Matrix3d m = Transform.MapRotation(bodyToIcrf);
            var forward = new Vector3((float)m.M02, (float)m.M12, (float)m.M22);
            var up = new Vector3((float)m.M01, (float)m.M11, (float)m.M21);
            if (forward.sqrMagnitude < 1e-12f || up.sqrMagnitude < 1e-12f) return Quaternion.identity;
            return Quaternion.LookRotation(forward, up);
        }

        /// <summary>The axis mapping as a matrix, for shaders and for orienting layer meshes.</summary>
        public Matrix4x4 IcrfToUniverse
        {
            get
            {
                Matrix3d p = ScaleSpace.RenderTransform.AxisSwap;
                var m = Matrix4x4.identity;
                m.m00 = (float)p.M00; m.m01 = (float)p.M01; m.m02 = (float)p.M02;
                m.m10 = (float)p.M10; m.m11 = (float)p.M11; m.m12 = (float)p.M12;
                m.m20 = (float)p.M20; m.m21 = (float)p.M21; m.m22 = (float)p.M22;
                return m;
            }
        }

        /// <summary>
        /// The proper half of the axis mapping, for a mesh whose vertices are authored in ICRF
        /// components — an orbit ribbon, a volume box.
        ///
        /// P is improper, so it cannot be a rotation on its own. Writing it as (P D) D with
        /// D = diag(1, 1, -1) puts a rotation on the transform and the mirror in the scale, which
        /// is what <see cref="IcrfMeshScale"/> carries. Unity flips the cull winding for a
        /// negative-determinant transform by itself.
        /// </summary>
        public static Quaternion IcrfMeshRotation => Quaternion.Euler(90f, 0f, 0f);

        /// <summary>The mirror that accompanies <see cref="IcrfMeshRotation"/>, times a uniform scale.</summary>
        public static Vector3 IcrfMeshScale(float uniform) => new Vector3(uniform, uniform, -uniform);

        /// <summary>Scale a length from km into universe-local metres, with no translation.</summary>
        public float ScaleLength(double km) => (float)(km * UnitsPerKm);

        /// <summary>
        /// Change the render scale. Everything is re-derived from absolute coordinates next frame,
        /// so this is safe at any time — it is how the rungs, travel and the two-hand gesture work.
        ///
        /// The clamp lives here, and only here. Two components used to carry their own disagreeing
        /// limits, which is a control whose range depends on which one wrote it last.
        /// </summary>
        public void SetUnitsPerKm(double unitsPerKm)
        {
            if (unitsPerKm <= 0.0 || double.IsNaN(unitsPerKm))
            {
                Debug.LogError($"[CosmoSynth] Ignoring invalid render scale {unitsPerKm}.");
                return;
            }
            UnitsPerKm = ScaleLimits.Clamp(unitsPerKm);
        }

        /// <summary>Move the absolute origin, without touching any transforms.</summary>
        public void SetOrigin(Double3 absoluteKm) => OriginKm = absoluteKm;
    }
}
