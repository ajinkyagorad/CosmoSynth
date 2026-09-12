// Shared helpers for every CosmoSynth shader. Contract Appendix A.2, verbatim.
//
// Everything here exists because the same arithmetic has to happen identically on the CPU and
// the GPU. CS_CompressDistance MUST equal Core ScaledSpace.Compress: the CPU places rigid
// groups with it and the vertex shader places points with it, and if the two disagree a moon
// placed by one path sinks into a planet placed by the other.

#ifndef COSMOSYNTH_CORE_INCLUDED
#define COSMOSYNTH_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

// Per-frame globals. Written ONLY by Placement.PublishGlobals (ScaleRenderer, LateUpdate order 50).
float4   _CS_CentreEyeWS;   // xyz: centre eye = the one compression centre for both eyes
float4   _CS_Compress;      // x: d0 identity radius (m), y: 1/d0, z: sky radius (m), w: unused
float4x4 _CS_IcrfToWorld;   // upper 3x3: ICRF components -> world (anchor * P * Q); det = -1

// THIS eye's world position. UNITY_MATRIX_I_V is unity_StereoMatrixInvV[unity_StereoEyeIndex]
// under single-pass instanced, so this is per eye by construction, in the vertex stage after
// UNITY_SETUP_INSTANCE_ID and in the fragment stage after UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX.
//
// Do NOT use URP's own camera-position helper: it resolves to _WorldSpaceCameraPos, whose per-eye
// definition under XR is a package detail we do not control (CONTRACT R1, 7.1 rule 2). G0b greps
// for that helper's name as a plain string anywhere under Runtime/Shaders, which is why it is
// described here rather than written out -- a rule enforced by grep has to be grep-clean in its
// own documentation, or the first thing it flags is the comment explaining it.
//
// This is NOT the compression centre: that is _CS_CentreEyeWS, which is the CENTRE eye for both
// eyes on purpose (2.5 rule 1) and must never be made per-eye.
float3 CS_EyePositionWS()
{
    return float3(UNITY_MATRIX_I_V._m03, UNITY_MATRIX_I_V._m13, UNITY_MATRIX_I_V._m23);
}

// |v| without overflowing float: render distances reach 4.4e29 m, and x*x overflows above ~1.8e19.
float CS_Length(float3 v)
{
    float m = max(max(abs(v.x), abs(v.y)), abs(v.z));
    return m > 0.0 ? m * length(v / m) : 0.0;
}

// f(d): identity inside d0, logarithmic beyond. Must equal Core ScaledSpace.Compress.
float CS_CompressDistance(float d)
{
    return d <= _CS_Compress.x ? d : _CS_Compress.x * (1.0 + log(d * _CS_Compress.y));
}

// Per-vertex radial compression about the centre eye: direction exact, distance f(d).
float3 CS_CompressWS(float3 positionWS)
{
    float3 rel = positionWS - _CS_CentreEyeWS.xyz;
    float d = CS_Length(rel);
    return d > 0.0 ? _CS_CentreEyeWS.xyz + rel * (CS_CompressDistance(d) / d) : positionWS;
}

float3 CS_IcrfDirToWorld(float3 dirIcrf) { return mul((float3x3)_CS_IcrfToWorld, dirIcrf); }

// Vertex colours and tuned constants are display-referred (sRGB); decode once, here.
half3 CS_DisplayToLinear(half3 c) { return SRGBToLinear(c); }

// Pixels per radian for THIS eye near the view centre (P11 = cot(fovY/2)).
float CS_PixelsPerRadian() { return 0.5 * _ScreenParams.y * UNITY_MATRIX_P._m11; }

// Angular half-extent (radians, x and y) to a clip-space offset at clip w, for this eye.
float2 CS_AngularToClip(float2 radians, float w)
{
    return radians * float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11) * w;
}

// Ray-space jitter for ray marches. A function of the RAY, never of the pixel: both eyes
// rasterize the same world point at different pixel coordinates, so a per-pixel jitter
// decorrelates between the eyes and becomes binocular rivalry on a high-frequency pattern -- in
// the one layer the design expects you to stand inside (CONTRACT R7, 2.5 point 7). It also
// decorrelates under SRP foveation, which rasterizes non-uniformly.
float CS_JitterDir(float3 dirOS)
{
    return frac(52.9829189 * frac(dot(dirOS.xy + dirOS.z, float2(0.06711056, 0.00583715))));
}

#endif
