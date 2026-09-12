// A soft rim glow for the selected body.
//
// PORTED IN PLACE from Built-in CG to URP HLSL (contract 7.2). The maths is unchanged; what
// changed is the pipeline skeleton, the camera accessor, the CBUFFER, and one added uniform:
//   - CGPROGRAM -> HLSLPROGRAM, UnityCG.cginc -> CosmoCore.hlsl;
//   - "RenderPipeline"="UniversalPipeline" and "LightMode"="UniversalForward" tags;
//   - the camera position -> CS_EyePositionWS(), read AFTER the eye macro (R1);
//   - _Color moved into CBUFFER_START(UnityPerMaterial) for the SRP Batcher;
//   - _PerVertexCompress, so the rim still hugs the silhouette of an Envelope-mode body.
//
// Drawn on a sphere slightly larger than the body, with FRONT faces culled so only the far
// hemisphere is rasterised. The body then occludes all of it except the sliver just outside its
// own silhouette, which is exactly where a selection outline belongs. Rendering the near
// hemisphere instead would lay a coloured filter over the whole planet — which is what a filled
// additive sphere did here, and it turned the Blue Marble into a flat cyan disc.
//
// The falloff is what separates this from a flat annulus. On the far hemisphere the surface
// normal turns away from the viewer as you move outward from the silhouette, so a power of
// (1 - |N.V|) concentrates the glow at the edge and fades it to nothing by the outer boundary.
// A constant-brightness ring reads as a second, larger object; a faded one reads as an outline.
//
// MIXED-REALITY RULE: `Blend One One, Zero One` adds colour while leaving destination alpha
// alone. On Quest the compositor reads framebuffer alpha as coverage, so an ordinary additive
// pass would saturate alpha along the rim and punch a hole in passthrough exactly there.

Shader "CosmoSynth/Rim"
{
    Properties
    {
        _Color    ("Colour (alpha scales brightness)", Color) = (0.35, 0.75, 1.0, 0.6)
        _Power    ("Edge falloff", Range(0.5, 8.0)) = 2.5
        _PerVertexCompress ("Envelope mode (contract 2.6)", Float) = 0
        _Fade ("Layer fade", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+100" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One, Zero One
            ZWrite Off
            Cull Front
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Power;
                float _PerVertexCompress;
                float _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNrm : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
                if (_PerVertexCompress > 0.5) positionWS = CS_CompressWS(positionWS);
                o.pos = TransformWorldToHClip(positionWS);
                o.worldPos = positionWS;
                o.worldNrm = TransformObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 view = normalize(CS_EyePositionWS() - i.worldPos);
                // abs, because these are back faces: the normal points away from the camera.
                float facing = saturate(abs(dot(normalize(i.worldNrm), view)));

                // 1 at the silhouette, falling to 0 as the surface turns to face the viewer.
                float rim = pow(1.0 - facing, _Power);

                // Alpha is the brightness dial, and it must not reach the framebuffer.
                //
                // No CS_DisplayToLinear here. _Color is a material Color set through SetColor,
                // which Unity linearises itself (contract 7.4); decoding again would darken the
                // rim by a second sRGB curve. Only VERTEX colours are decoded in the shader.
                return half4(_Color.rgb * (half)(_Color.a * rim * _Fade), 0.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
