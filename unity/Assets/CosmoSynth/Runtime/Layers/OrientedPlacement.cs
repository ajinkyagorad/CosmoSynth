using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Placing a mesh that is authored in some frame other than ICRF — the dust cube in galactic
    /// axes, a galaxy disc in its own sky frame, a grid in supergalactic coordinates.
    ///
    /// It exists because of a trap with exactly one correct answer. The ICRF-to-universe map is a
    /// <b>handedness swap</b>: determinant -1, not a rotation. <c>Matrix4x4.rotation</c> on it
    /// returns a quaternion that is not an error and is not right, and the result is a mirrored
    /// scene which orbits correctly and rotates backwards — the failure CLAUDE.md records from
    /// the last time this was done by hand.
    ///
    /// <see cref="FloatingOrigin"/> already publishes the only legal decomposition, a quarter turn
    /// about x with a negated z scale. Everything here does is compose an author-frame rotation
    /// into it without ever letting an improper matrix reach a quaternion.
    /// </summary>
    public static class OrientedPlacement
    {
        /// <summary>
        /// Set a transform so that a mesh authored in <paramref name="icrfFromMesh"/> lands in
        /// universe-local space, at <paramref name="trueUniverse"/>, scaled by
        /// <paramref name="metresPerMeshUnit"/> and then by the group similarity
        /// <paramref name="similarity"/> about the centre eye.
        /// </summary>
        public static void Place(Transform t, Matrix3d icrfFromMesh, Vector3 trueUniverse,
            double similarity, double metresPerMeshUnit, in FrameContext ctx)
        {
            Placement.PlaceRigid(t, trueUniverse, similarity, ctx);
            SetOrientation(t, icrfFromMesh, (float)(metresPerMeshUnit * similarity));
        }

        /// <summary>
        /// The rotation and scale alone, for callers that set the position themselves.
        ///
        /// <c>A = P * icrfFromMesh</c> is improper. Write it as <c>Q * diag(1, 1, -1)</c>, where
        /// Q is a genuine rotation, and hand Unity the negative z scale that carries the
        /// handedness. That is the same decomposition <c>FloatingOrigin.IcrfMeshRotation</c> uses,
        /// generalised to a mesh that is not already in ICRF.
        /// </summary>
        public static void SetOrientation(Transform t, Matrix3d icrfFromMesh, float uniformScale)
        {
            Matrix3d a = ScaleSpace.RenderTransform.AxisSwap * icrfFromMesh;

            Vector3 y = new Vector3((float)a.M01, (float)a.M11, (float)a.M21);
            Vector3 zFlipped = new Vector3(-(float)a.M02, -(float)a.M12, -(float)a.M22);

            t.localRotation = Quaternion.LookRotation(zFlipped, y);
            t.localScale = new Vector3(uniformScale, uniformScale, -uniformScale);
        }
    }
}
