// A soft rim glow for the selected body.
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
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+150" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend One One, Zero One
            ZWrite Off
            Cull Front
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float4 _Color;
            float  _Power;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNrm : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNrm = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 view = normalize(_WorldSpaceCameraPos - i.worldPos);
                // abs, because these are back faces: the normal points away from the camera.
                float facing = saturate(abs(dot(normalize(i.worldNrm), view)));

                // 1 at the silhouette, falling to 0 as the surface turns to face the viewer.
                float rim = pow(1.0 - facing, _Power);

                // Alpha is the brightness dial, and it must not reach the framebuffer.
                return float4(_Color.rgb * (_Color.a * rim), 0.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
