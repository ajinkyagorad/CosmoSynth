// Inside-out sky sphere at infinity. Contract Appendix A.6, verbatim.
//
// The mesh is a unit sphere of about 2 k vertices whose object-space vertices ARE ICRF
// directions. Identity transform, huge bounds. Eye-anchored in the VERTEX stage, which under
// multiview runs per eye, so the sphere sits at the same angular place in both eyes: zero
// stereo disparity and zero head-motion parallax, which is what infinity looks like.
//
// Sampling by the ICRF direction rather than a Unity axis keeps the one axis mapping inside
// FloatingOrigin: the cube lookup needs no mapping at all, and is hardware-seamless with no
// pole oversampling and no inside-out mirroring.

Shader "CosmoSynth/Sky"
{
    Properties
    {
        _SkyCube ("CMB cube map, faces in ICRF components (linear R8)", Cube) = "grey" {}
        _Lut ("Colour LUT 256x1 (sRGB)", 2D) = "white" {}
        _Contrast ("Contrast about 0 uK (1 = +-500 uK full LUT)", Float) = 2
        _Immersion ("0 = passthrough untouched, 1 = replaces the room (VR / full-space)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero   // first draw of the frame; replaces the (0,0,0,0) clear with a premultiplied colour
            ZWrite Off
            ZTest LEqual
            Cull Off         // inside a closed sphere every view ray meets the surface exactly once

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            TEXTURECUBE(_SkyCube); SAMPLER(sampler_SkyCube);
            TEXTURE2D(_Lut);       SAMPLER(sampler_Lut);

            CBUFFER_START(UnityPerMaterial)
                float _Contrast;
                float _Immersion;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;   // ICRF direction (unit sphere)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirIcrf    : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 dirIcrf = normalize(v.positionOS);
                float3 dirWS = normalize(CS_IcrfDirToWorld(dirIcrf));
                // Anchored to THIS eye (per eye in the vertex stage under multiview): zero disparity
                // and zero head-motion parallax, which is what infinity looks like.
                o.positionCS = TransformWorldToHClip(GetCameraPositionWS() + dirWS * _CS_Compress.z);
                o.dirIcrf = dirIcrf;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Sample by ICRF direction: hardware-seamless, no pole oversampling, no inside-out
                // mirroring, and no Unity axis in the lookup.
                half raw = SAMPLE_TEXTURECUBE(_SkyCube, sampler_SkyCube, normalize(i.dirIcrf)).r;
                half t   = saturate(0.5h + (raw - 0.5h) * (half)_Contrast);   // raw 0.5 = 0 uK
                half3 c  = SAMPLE_TEXTURE2D(_Lut, sampler_Lut, float2(t, 0.5)).rgb;
                // Premultiplied. This is the ONE pass that writes coverage on purpose: immersion is
                // the opt-in full-space mode. In MR, _Immersion = 0 and the layer skips the draw.
                return half4(c * (half)_Immersion, (half)_Immersion);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
