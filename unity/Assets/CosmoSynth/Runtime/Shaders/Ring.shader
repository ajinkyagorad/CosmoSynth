// Planetary ring system. Contract Appendix A.8.
//
// A ring is a physical absorber, not a glow, so this is premultiplied "over" and DOES write
// coverage equal to the texture's alpha: a dense ringlet really should hide the room behind it,
// and a gap really should not.
//
// The planet shadow is an analytic ray-sphere test from the ring point toward the star against
// the parent's DRAWN sphere. It must be the drawn radius, passed in _ParentCentreWS.w, not the
// physical one: every rigid group is rendered at its own similarity, and the true radius would
// cast a shadow the wrong size at every scale but one.
//
// The radial coordinate comes from the MESH (TEXCOORD0.x, 0 at the inner edge and 1 at the
// outer), not from a pair of radius uniforms. The mesh already carries it, so a _Radii uniform
// would be a second copy of the same fact whose only possible effect is to be wrong when someone
// forgets to set it -- and it carried a default of (1,2) that matches no real ring. Taking u from
// the mesh also retires the assumption that the ring plane is object XZ.
//
// [MainTexture] is load-bearing: the runtime builds the ring texture and assigns it through
// Material.mainTexture, which binds to _MainTex or to the [MainTexture]-tagged property and
// silently does nothing without one.

Shader "CosmoSynth/Ring"
{
    Properties
    {
        [MainTexture] _BaseMap ("Ring texture (RGBA, sRGB; u = 0 inner, 1 outer)", 2D) = "white" {}
        [MainColor] _BaseColor ("Ring tint", Color) = (1,1,1,1)
        _LightDirWS ("Direction to host star, world", Vector) = (0,1,0,0)
        _LightColor ("Star light (linear)", Color) = (1,1,1,1)
        _Ambient ("Ambient fill", Color) = (0.02,0.02,0.03,1)
        _ParentCentreWS ("xyz: parent centre (world), w: parent DRAWN radius", Vector) = (0,0,0,1)
        _Fade ("Layer fade", Float) = 1
        _PerVertexCompress ("Envelope mode (contract 2.6)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Ring"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha   // premultiplied over: an absorber, so coverage = opacity
            ZWrite Off
            ZTest LEqual
            Cull Off       // a ring plane is seen from both sides

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                float4 _LightDirWS;
                half4  _LightColor;
                half4  _Ambient;
                float4 _ParentCentreWS;
                float  _Fade;
                float  _PerVertexCompress;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;   // x: 0 at the inner edge, 1 at the outer
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(v.positionOS);
                if (_PerVertexCompress > 0.5) positionWS = CS_CompressWS(positionWS);

                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(saturate(i.uv.x), 0.5));

                // Planet shadow: does the segment from this ring point toward the star hit the
                // parent's drawn sphere? b > 0 means the sphere is BEHIND the ring point along the
                // light direction, which casts no shadow.
                float3 oc = i.positionWS - _ParentCentreWS.xyz;
                float3 L = normalize(_LightDirWS.xyz);
                float b = dot(oc, L);
                float c = dot(oc, oc) - _ParentCentreWS.w * _ParentCentreWS.w;
                half lit = (b < 0.0 && (b * b - c) > 0.0) ? 0.0h : 1.0h;

                // No CS_DisplayToLinear on tex (_BaseMap is imported sRGB, so the sampler has
                // already decoded it) and none on _BaseColor either: contract 7.4 rules that
                // Material colours set through SetColor are linearised by Unity itself. All three
                // of Unlit, Rim and Ring now follow that one rule; the G1c grey capture settles
                // whether the rule is right, in one place rather than three.
                half3 colour = tex.rgb * _BaseColor.rgb * (_LightColor.rgb * lit + _Ambient.rgb);
                half  alpha  = tex.a * _BaseColor.a * (half)_Fade;
                return half4(colour * alpha, alpha);   // premultiplied
            }
            ENDHLSL
        }
    }
    Fallback Off
}
