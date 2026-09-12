// Panel backing and accent. Contract Appendix A.8.
//
// Premultiplied over, and it DOES write coverage: a panel is meant to be readable against a
// bright room, so it has to occlude passthrough in proportion to its own opacity.
//
// One shader, two queues. The backing material leaves the declared 3200; the accent material
// sets renderQueue = 3201 so accents land on top of their own backing without a second shader
// and without a second variant.

Shader "CosmoSynth/Panel"
{
    Properties
    {
        // The panel meshes are Unity Quads, which carry no COLOR channel, and CosmoPanel sets
        // its colour and opacity through Material.color. Without [MainColor] that write is
        // dropped and the fragment multiplies by an undefined vertex alpha: invisible panels.
        [MainTexture] _BaseMap ("Panel mask (alpha used; rounded corners, borders)", 2D) = "white" {}
        [MainColor] _BaseColor ("Panel colour", Color) = (1,1,1,1)
        _Fade ("Layer fade", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+200" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Panel"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha   // premultiplied over; coverage = opacity
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                float  _Fade;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;     // display colour; a = opacity
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Vertex colour is display-referred and decoded here; _BaseColor arrives
                // through SetColor and is already linear (contract 7.4).
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a
                           * i.color.a * _BaseColor.a * (half)_Fade;
                half3 rgb = CS_DisplayToLinear(i.color.rgb) * _BaseColor.rgb;
                return half4(rgb * alpha, alpha);   // premultiplied
            }
            ENDHLSL
        }
    }
    Fallback Off
}
