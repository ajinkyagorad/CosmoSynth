// Bounded raymarch with a per-eye ray origin. Contract Appendix A.7, verbatim.
//
// The Milky Way dust cube. The mesh is a unit cube [-0.5, 0.5]^3; the transform is the group
// placement (Rigid or Enclosing) with the layer frame's rotation.
//
// The march runs in OBJECT space from THIS eye. The eye position is read in the VERTEX stage,
// after UNITY_SETUP_INSTANCE_ID, and passed down as a varying: it is constant across the
// primitive for a given eye, so perspective-correct interpolation returns it exactly, and the
// fragment never touches unity_WorldToObject (R5). A similarity about the centre eye leaves
// every eye at the same object-space point, so compression never touches the integral.
//
// The step count comes from the CPU through the step-coverage governor:
// N = clamp(B / coverage, 16, 64), B = 30 (contract 7.3).

Shader "CosmoSynth/Volume"
{
    Properties
    {
        _Density ("Density: R8, log-encoded, x fastest", 3D) = "black" {}
        _Ramp ("Colour ramp 256x1 (sRGB)", 2D) = "white" {}
        _Decode ("x: vmin, y: ln(vmax/vmin), z: extinction per (density x object unit), w: ramp scale", Vector) = (0.02,8.3,1,1)
        _Steps ("x: steps (CPU governor), y: empty cut (encoded value)", Vector) = (32,0.22,0,0)
        _Fade ("Layer fade", Float) = 1
    }
    SubShader
    {
        // DisableBatching: positionOS is the march's object-space entry point, and static
        // batching bakes vertices into world space (R12).
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            Name "Volume"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha   // premultiplied over: dust absorbs, so coverage = opacity
            ZWrite Off
            ZTest LEqual     // an opaque body inside the box rejects the back face: the body shows, no depth texture needed
            Cull Front       // back faces: correct with the eye outside or inside the box

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            TEXTURE3D(_Density); SAMPLER(sampler_Density);
            TEXTURE2D(_Ramp);    SAMPLER(sampler_Ramp);

            CBUFFER_START(UnityPerMaterial)
                float4 _Decode;
                float4 _Steps;
                float  _Fade;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 eyeOS      : TEXCOORD1;   // constant across the primitive for a given eye,
                                                 // so perspective-correct interpolation returns it
                                                 // exactly. Computed in vert, where the instance
                                                 // and eye ids are set up (R5).
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.positionOS = v.positionOS;
                o.eyeOS = TransformWorldToObject(CS_EyePositionWS());   // R1 + R5
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // NOT TransformWorldToObject here: under INSTANCING_ON unity_WorldToObject is an
                // array indexed by unity_InstanceID, which no fragment sets up, so a fragment reads
                // matrix 0 -- the wrong one the moment this layer is ever GPU-instanced. It is also
                // a per-pixel 4x4 multiply for a value constant across the primitive (R5).
                float3 eyeOS = i.eyeOS;
                float3 dir = normalize(i.positionOS - eyeOS);
                float3 inv = 1.0 / dir;                           // IEEE inf on an axis is handled by min/max
                float3 tA = (-0.5 - eyeOS) * inv;
                float3 tB = ( 0.5 - eyeOS) * inv;
                float3 t0 = min(tA, tB), t1 = max(tA, tB);
                float tNear = max(max(max(t0.x, t0.y), t0.z), 0.0);   // 0 when the eye is inside
                float tFar  = min(min(t1.x, t1.y), t1.z);
                if (tFar <= tNear) return half4(0, 0, 0, 0);

                int   steps = max((int)_Steps.x, 1);
                float dt = (tFar - tNear) / steps;
                float t  = tNear + dt * CS_JitterDir(dir);   // ray-space jitter: banding becomes fine
                                                            // noise, and the SAME noise in both
                                                            // eyes -- a pixel-space jitter is
                                                            // binocular rivalry here (R7)
                half3 colour = 0;
                half  alpha  = 0;
                [loop] for (int s = 0; s < steps; s++, t += dt)
                {
                    half v = SAMPLE_TEXTURE3D_LOD(_Density, sampler_Density, eyeOS + dir * t + 0.5, 0).r;
                    if (v <= (half)_Steps.y) continue;
                    float density = _Decode.x * exp(v * _Decode.y);                       // log decode
                    half  a = (half)(1.0 - exp(-density * _Decode.z * dt));               // Beer-Lambert per step
                    half3 c = SAMPLE_TEXTURE2D_LOD(_Ramp, sampler_Ramp, float2(saturate(v * _Decode.w), 0.5), 0).rgb;
                    colour += (1.0h - alpha) * a * c;
                    alpha  += (1.0h - alpha) * a;
                    if (alpha > 0.985h) break;
                }
                return half4(colour, alpha) * (half)_Fade;           // premultiplied
            }
            ENDHLSL
        }
    }
    Fallback Off
}
