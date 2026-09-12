// Flat opaque colour. Contract Appendix A.8.
//
// Beacon bars (AnchorBeacon.cs:65) and the pointer ray (BodySelector.cs:70). Opaque, so it
// writes alpha 1 and occludes passthrough: that is correct, a solid bar in the room is solid.

Shader "CosmoSynth/Unlit"
{
    Properties
    {
        _BaseColor ("Base colour (display-referred sRGB)", Color) = (1,1,1,1)
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
                // Verbatim from contract A.8. NOTE the tension with contract 7.4, which says
                // material Colors set through SetColor are linearised by Unity itself (marked
                // INFERRED there). If that is true, this decodes a second time and beacons and
                // the pointer ray come out too dark. It is one of the two colour questions the
                // first capture must settle; see docs/reports/foundation-shaders.md.
                return half4(CS_DisplayToLinear(_BaseColor.rgb), 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
