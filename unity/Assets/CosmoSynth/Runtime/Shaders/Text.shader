// Legacy TextMesh glyphs. Contract Appendix A.8. Replaces "GUI/Text Shader".
//
// TMP is NOT used (contract C3): the legacy dynamic font always ships with the player, whereas a
// TMP asset is a project asset that a runtime-built scene references from nothing and the build
// step is free to strip — which is the same class of failure as the magenta build.
//
// _MainTex is the dynamic font's atlas, and it is REBUILT at runtime whenever a new glyph is
// requested. CosmoText.cs keeps the Font.textureRebuilt handling that re-points this material at
// the new atlas; without it the labels turn to garbage the first time an unseen character
// appears.
//
// The property is named _MainTex, not _BaseMap, because Unity's own Font material assigns the
// atlas to _MainTex and CosmoText clones that material onto this shader.

Shader "CosmoSynth/Text"
{
    Properties
    {
        _MainTex ("Font atlas (alpha)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+210" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Text"
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

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
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
                o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
                return half4(CS_DisplayToLinear(i.color.rgb) * alpha, alpha);   // premultiplied
            }
            ENDHLSL
        }
    }
    Fallback Off
}
