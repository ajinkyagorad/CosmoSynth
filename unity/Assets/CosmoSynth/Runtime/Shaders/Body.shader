// Opaque lit or unlit body. Contract Appendix A.3, verbatim.
//
// Lit by the HOST STAR through a uniform, not by a Unity light. A universe has many stars and
// one camera sees several systems at once; Unity's per-pixel light slots cannot express that,
// and a single directional light gets the terminator wrong on every body but one.

Shader "CosmoSynth/Body"
{
    Properties
    {
        // [MainColor] / [MainTexture]: the runtime assigns through Material.color and
        // Material.mainTexture, which bind to _Color/_MainTex or to a tagged property and
        // silently do nothing without one -- a white, untextured planet with no error.
        [MainColor] _BaseColor ("Base colour", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("Albedo (sRGB)", 2D) = "white" {}
        _NormalMap ("Normal (linear RGB, created with linear:true)", 2D) = "bump" {}
        _SurfaceMap ("Surface (linear; a = smoothness)", 2D) = "black" {}
        _LightDirWS ("Direction to host star, world", Vector) = (0,1,0,0)
        _LightColor ("Star light (linear)", Color) = (1,1,1,1)
        _Ambient ("Ambient fill (from CosmoTheme)", Color) = (0.02,0.02,0.03,1)
        _Emission ("Emission, x albedo (stars, spacecraft)", Color) = (0,0,0,1)
        _Smoothness ("Smoothness scale", Range(0,1)) = 0
        _SpecGain ("Specular gain", Range(0,2)) = 0.6
        _Terminator ("Terminator softness", Range(0,0.3)) = 0.05
        _PerVertexCompress ("Envelope mode (contract 2.6)", Float) = 0
        _Fade ("Layer fade", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Body"
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

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
            TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                float4 _LightDirWS;
                half4  _LightColor;
                half4  _Ambient;
                half4  _Emission;
                half   _Smoothness;
                half   _SpecGain;
                half   _Terminator;
                float  _PerVertexCompress;
                float  _Fade;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half4  tangentWS  : TEXCOORD2;   // w: bitangent sign
                float2 uv         : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(v.positionOS);
                // Rigid mode: the transform already holds the group similarity. Envelope mode:
                // the eye is near a large body, so compress each vertex about the centre eye.
                //
                // APPROXIMATION, stated so a future reader does not take it for a lighting bug:
                // in Envelope mode the normal is NOT recomputed and positionWS is written after
                // compression, so the shading normal belongs to the uncompressed surface and V
                // is measured from this eye to a centre-eye-compressed point. Inside d0 = 10 m
                // f is the identity and both are exact -- which is where the user actually
                // stands in this mode -- and the error grows logarithmically toward the horizon,
                // where the terminator is most visible. Recomputing the normal means the
                // Jacobian of f, which is not worth a per-vertex 3x3 for a term that is zero
                // over the whole near field.
                if (_PerVertexCompress > 0.5) positionWS = CS_CompressWS(positionWS);

                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS  = half4(TransformObjectToWorldDir(v.tangentOS.xyz),
                                     v.tangentOS.w * GetOddNegativeScale());
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);   // per-eye camera position below

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 nTS = UnpackNormalRGBNoScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                half3 n = normalize(i.normalWS);
                half3 t = normalize(i.tangentWS.xyz);
                half3 b = cross(n, t) * i.tangentWS.w;
                half3 N = normalize(nTS.x * t + nTS.y * b + nTS.z * n);

                // Lit by the HOST STAR (found through the parent chain on the CPU), not by a Unity
                // light: no per-pixel light slots, no RootIds[0], correct in every group.
                half3 L = normalize(_LightDirWS.xyz);
                half  ndl = dot(N, L);
                half  diffuse = saturate((ndl + _Terminator) / (1.0h + _Terminator));

                half3 V = normalize(CS_EyePositionWS() - i.positionWS);   // this eye
                half3 H = normalize(L + V);
                half  smoothness = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, i.uv).a * _Smoothness;
                half  spec = pow(saturate(dot(N, H)), exp2(1.0h + 10.0h * smoothness))
                           * smoothness * _SpecGain * step(0.0h, ndl);

                half3 colour = albedo * (_LightColor.rgb * diffuse + _Ambient.rgb)
                             + _LightColor.rgb * spec
                             + albedo * _Emission.rgb;
                // _Fade is the layer framework's one visibility mechanism (R30): an opaque pass
                // fades its coverage with its colour, or a layer cannot fade out and pops.
                return half4(colour, 1.0h) * (half)_Fade;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
