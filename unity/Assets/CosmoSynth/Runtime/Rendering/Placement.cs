using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The only code that turns a scale into a transform.
    ///
    /// One function, so that "where is this drawn" has exactly one answer. Every body,
    /// decoration, orbit line, label, spacecraft and sky element goes through here or through the
    /// shader globals published here; nothing computes its own render position.
    /// </summary>
    public static class Placement
    {
        /// <summary>Shader global: the centre eye in world space, the point everything compresses about.</summary>
        public static readonly int CentreEyeId = Shader.PropertyToID("_CS_CentreEyeWS");

        /// <summary>Shader global: (d0, 1/d0, sky radius, 0).</summary>
        public static readonly int CompressId = Shader.PropertyToID("_CS_Compress");

        /// <summary>Shader global: ICRF directions to world space.</summary>
        public static readonly int IcrfToWorldId = Shader.PropertyToID("_CS_IcrfToWorld");

        /// <summary>
        /// A rigid similarity about the centre eye, in the universe host's local space.
        ///
        /// <paramref name="trueUniverse"/> must come from <see cref="FloatingOrigin.ToRender"/>, so
        /// the subtraction of the origin has already happened in double. Compressing about the
        /// centre eye — one point, not one per eye — is what keeps both eyes looking at a single
        /// rigid scene; per-eye compression gives binocular rivalry and is forbidden.
        /// </summary>
        public static void PlaceRigid(Transform t, Vector3 trueUniverse, double s, in FrameContext ctx)
        {
            t.localPosition = ctx.EyeUniverse + (float)s * (trueUniverse - ctx.EyeUniverse);
        }

        /// <summary>
        /// Publish the globals every CosmoSynth shader reads. Called once per frame by
        /// <see cref="ScaleRenderer"/> and by nobody else, so the CPU and the GPU cannot disagree
        /// about where the eye is or what d0 is.
        /// </summary>
        public static void PublishGlobals(in FrameContext ctx)
        {
            Shader.SetGlobalVector(CentreEyeId, ctx.EyeWorld);
            Shader.SetGlobalVector(CompressId, new Vector4(
                (float)ScaledSpace.IdentityRadiusMetres,
                (float)(1.0 / ScaledSpace.IdentityRadiusMetres),
                (float)ScaledSpace.SkyRadiusMetres,
                0f));
            Shader.SetGlobalMatrix(IcrfToWorldId, ctx.IcrfToWorld);
        }
    }
}
