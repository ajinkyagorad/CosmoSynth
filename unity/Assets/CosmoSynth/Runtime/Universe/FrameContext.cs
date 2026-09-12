using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Everything about this frame that placement depends on, computed once by
    /// <see cref="ScaleRenderer"/> and handed to every consumer.
    ///
    /// It exists so that the centre-eye position is computed exactly once per frame. When each
    /// component worked it out for itself, some crossed the universe host's transform and some did
    /// not — and at tabletop scale the difference is thousands of kilometres of apparent distance,
    /// which drops every body to a point and blanks the scene while passthrough carries on working.
    /// </summary>
    public readonly struct FrameContext
    {
        /// <summary>The centre eye, absolute ICRF km, in double.</summary>
        public readonly Double3 EyeKm;

        /// <summary>The centre eye in Unity world space (the camera's transform position).</summary>
        public readonly Vector3 EyeWorld;

        /// <summary>The centre eye in the universe host's local space, which is where bodies live.</summary>
        public readonly Vector3 EyeUniverse;

        /// <summary>Room metres per real kilometre this frame.</summary>
        public readonly double UnitsPerKm;

        /// <summary>The rotation taking ICRF directions into Unity world space.</summary>
        public readonly Matrix4x4 IcrfToWorld;

        /// <summary>Centre-eye estimate of pixels per radian, for every angular-size rule.</summary>
        public readonly float PixelsPerRadian;

        /// <summary>The instant being drawn.</summary>
        public readonly CosmicTime Now;

        public FrameContext(Double3 eyeKm, Vector3 eyeWorld, Vector3 eyeUniverse, double unitsPerKm,
            Matrix4x4 icrfToWorld, float pixelsPerRadian, CosmicTime now)
        {
            EyeKm = eyeKm;
            EyeWorld = eyeWorld;
            EyeUniverse = eyeUniverse;
            UnitsPerKm = unitsPerKm;
            IcrfToWorld = icrfToWorld;
            PixelsPerRadian = pixelsPerRadian;
            Now = now;
        }

        /// <summary>The argument every layer visibility window is a function of.</summary>
        public double Log10U => MathD.SafeLog10(UnitsPerKm);

        /// <summary>Apparent diameter in pixels of a sphere, from the centre eye.</summary>
        public double ApparentPixels(double radiusKm, double distanceKm) =>
            ScaleSpace.AngularDiameterRad(radiusKm, distanceKm) * PixelsPerRadian;
    }
}
