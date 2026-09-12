// Screen-constant-width line with analytic anti-aliasing. Contract Appendix A.5, verbatim.
//
// Orbit lines, constellation figures and distance grids. The anti-aliasing is computed in the
// fragment shader from the signed pixel distance to the centre line rather than left to MSAA:
// a hairline thinner than a sample grid breaks into dashes under MSAA, and a 4x pattern cannot
// represent one-third of a pixel of coverage at all.
//
// The mesh has 4 vertices per segment. Each vertex carries its own endpoint (POSITION), the
// segment's other endpoint (TEXCOORD0), and the side and order (TEXCOORD1). One draw covers all
// the segments of one owner. The tail gradient rides in COLOR.a. The legibility cull ("only
// from outside, only if bigger than a dot") stays on the CPU.

Shader "CosmoSynth/Ribbon"
{
    Properties
    {
        _WidthDmm ("Line width, dmm (mrad); 1.4 dmm ~ 2 px on Quest 3", Float) = 1.4
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Float) = 1
        _Placement ("0 rigid (CPU transform), 1 per-vertex (owner contains the eye), 2 sky (ICRF directions, per eye)", Float) = 0
    }
    SubShader
    {
        // DisableBatching: otherOS is an object-space endpoint, and static batching bakes
        // vertices into world space (R12).
        Tags { "RenderType"="Transparent" "Queue"="Transparent+60" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            Name "Ribbon"
            Tags { "LightMode"="UniversalForward" }
            Blend One One, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _WidthDmm;
                float _Intensity;
                float _Fade;
                float _Placement;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;    // this vertex's endpoint
                float3 otherOS    : TEXCOORD0;   // the segment's other endpoint
                float2 sideOrder  : TEXCOORD1;   // x: side -1/+1; y: +1 if other is the next point, -1 if previous
                half4  color      : COLOR;       // display colour; a = tail gradient
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3  color      : COLOR;
                noperspective float2 aa : TEXCOORD0;   // x: signed px from the centre line; y: half width px
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 PlaceWS(float3 positionOS)
            {
                if (_Placement > 1.5)   // sky: positionOS is an ICRF direction; this eye, at infinity
                    return CS_EyePositionWS() + normalize(CS_IcrfDirToWorld(positionOS)) * _CS_Compress.z;
                float3 p = TransformObjectToWorld(positionOS);
                return _Placement > 0.5 ? CS_CompressWS(p) : p;
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 selfVS  = TransformWorldToView(PlaceWS(v.positionOS));
                float3 otherVS = TransformWorldToView(PlaceWS(v.otherOS));

                // Clip the segment against a plane just beyond the near plane (view space looks down -z).
                const float nearVS = 0.06;
                bool selfBehind  = -selfVS.z  < nearVS;
                bool otherBehind = -otherVS.z < nearVS;
                if (selfBehind && otherBehind) { o.positionCS = float4(2, 2, 2, 1); return o; }
                if (selfBehind)  selfVS  = lerp(selfVS, otherVS, (-nearVS - selfVS.z) / (otherVS.z - selfVS.z));
                if (otherBehind) otherVS = lerp(otherVS, selfVS, (-nearVS - otherVS.z) / (selfVS.z - otherVS.z));

                float4 selfCS  = TransformWViewToHClip(selfVS);
                float4 otherCS = TransformWViewToHClip(otherVS);
                float2 halfScreen = 0.5 * _ScreenParams.xy;
                float2 d = (otherCS.xy / otherCS.w - selfCS.xy / selfCS.w) * halfScreen * v.sideOrder.y;
                float2 dir = dot(d, d) > 1e-8 ? normalize(d) : float2(1.0, 0.0);
                float2 nrm = float2(-dir.y, dir.x);

                float halfPx = 0.5 * _WidthDmm * 1e-3 * CS_PixelsPerRadian();   // angular width, this eye
                float drawnHalf = max(halfPx, 0.5) + 1.0;                        // +1 px for the analytic edge
                selfCS.xy += nrm * (v.sideOrder.x * drawnHalf) / halfScreen * selfCS.w;

                o.positionCS = selfCS;
                o.aa = float2(v.sideOrder.x * drawnHalf, halfPx);
                o.color = CS_DisplayToLinear(v.color.rgb) * (half)(v.color.a * _Intensity * _Fade);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float halfPx = i.aa.y;
                float coverage = saturate(max(halfPx, 0.5) + 0.5 - abs(i.aa.x));   // analytic AA, no MSAA needed
                coverage *= saturate(halfPx * 2.0);   // thinner than a pixel: fades instead of breaking up
                return half4(i.color * (half)coverage, 0.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
