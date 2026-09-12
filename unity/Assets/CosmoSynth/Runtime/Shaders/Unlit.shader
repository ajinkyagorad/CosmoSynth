// Flat opaque colour. Contract Appendix A.8.
//
// Beacon bars (AnchorBeacon.cs:65) and the pointer ray (BodySelector.cs:70). Opaque, so it
// writes alpha 1 and occludes passthrough: that is correct, a solid bar in the room is solid.

Shader "CosmoSynth/Unlit"
{
    Properties
    {
        // [MainColor]: AnchorBeacon and BodySelector assign through Material.color, which
        // binds to _Color or to the tagged property and is silently dropped without one.
        [MainColor] _BaseColor ("Base colour", Color) = (1,1,1,1)
        _Fade ("Layer fade", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Fade;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // No CS_DisplayToLinear on _BaseColor. Contract 7.4 rules that material Colors
                // set through SetColor are linearised by Unity itself; decoding again would be
                // a second sRGB curve. Rim and Ring now follow the same rule, so the three no
                // longer hold three opinions -- and the G1c grey capture settles the rule in
                // one place. Only VERTEX colours are decoded in a shader.
                return half4(_BaseColor.rgb, 1.0h) * (half)_Fade;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
