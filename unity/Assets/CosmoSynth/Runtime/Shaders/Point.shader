// Additive point-sprite billboard. Contract Appendix A.4, verbatim.
//
// One static mesh of 4-vertex quads; magnitude becomes size AND brightness in the vertex stage.
//
// The two bounds in _PxBounds are not tuning knobs, they are the fix for two shipped bugs.
// The MINIMUM exists because a one-pixel feature has no shape: given a Gaussian profile the
// visible core is about half the quad, so a two-pixel quad renders as a square patch whatever
// texture it is cut from (CLAUDE.md, the star field). The MAXIMUM exists because a floor with
// no ceiling is unbounded in the worst direction: the same rule applied to a 3 m lander
// inflated it past the size of Mars and put the camera inside it, giving a black screen with
// passthrough working and nothing in it.
//
// There is NO per-material rotation matrix (R4). ShaderLab has no matrix property type, so a
// matrix in UnityPerMaterial can never appear in Properties -- which is the documented condition
// for SRP Batcher compatibility -- and, having no Properties entry, it has no default either: an
// unconfigured material carried the ZERO matrix, every direction normalised to NaN and the whole
// layer collapsed to undefined clip positions. Layer and sky modes both rotate through the global
// _CS_IcrfToWorld (contract 3.4, 8.2 item 13); a layer that ever genuinely needs its own rotation
// declares three Vector properties.

Shader "CosmoSynth/Point"
{
    Properties
    {
        _EyeInChunk ("Layer mode: eye in this chunk's units", Vector) = (0,0,0,0)
        _Units      ("x: metres per unit, y: parsecs per unit", Vector) = (1,1,0,0)
        _MagWindow  ("x: magnitude at brightness 0, y: at brightness 1 (StarLook: 7, 1.5)", Vector) = (7,1.5,0,0)
        _SizeDmm    ("x: faint width, y: bright width (StarLook: 3.8, 9.0)", Vector) = (3.8,9,0,0)
        _PxBounds   ("x: min px, y: max px", Vector) = (3,64,0,0)
        _Shape      ("x: halo scale, y: spike length, z: spike width (x core width)", Vector) = (2.8,7,0.35,0)
        _ShapeGain  ("x: halo gain, y: spike gain (bright end only)", Vector) = (0.35,0.25,0,0)
        _Look       ("x: faintest brightness 0.40, y: exposure, z/w: near-fade start/end (layer units)", Vector) = (0.4,1,0,0)
        _Fade       ("Layer fade", Float) = 1
        // Default 1 (object space, CPU-placed), not 0. An unconfigured material must degrade to
        // the branch that needs nothing but its own transform; the layer branch needs _EyeInChunk
        // and _Units, which only a layer sets.
        _Space      ("0 layer (per-point compression), 1 object (CPU-placed), 2 sky (direction, per eye)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Points"
            Tags { "LightMode"="UniversalForward" }
            Blend One One, Zero One     // additive colour; destination alpha (passthrough coverage) untouched
            ZWrite Off
            ZTest LEqual                // hidden behind opaque bodies, which draw first
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EyeInChunk;
                float4 _Units;
                float4 _MagWindow;
                float4 _SizeDmm;
                float4 _PxBounds;
                float4 _Shape;
                float4 _ShapeGain;
                float4 _Look;
                float  _Fade;
                float  _Space;
            CBUFFER_END

            struct Attributes
            {
                float3 offset : POSITION;   // layer: offset from chunk origin; object: object space; sky: direction
                half4  color  : COLOR;      // display (sRGB) colour; a unused
                float4 data   : TEXCOORD0;  // x magnitude (absolute in layer mode, apparent otherwise),
                                            // y corner 0..3, z shape 0 core/1 halo/2 spike-x/3 spike-y, w hidden
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Rotate into world through the global ICRF mapping, refusing to normalise a vector
            // the rotation collapsed. A zero global (the first frames, before Placement has
            // published) would otherwise give NaN clip positions, which on a tile GPU is
            // undefined rasterisation rather than nothing drawn.
            float3 SafeIcrfDir(float3 v)
            {
                float3 r = CS_IcrfDirToWorld(v);
                return dot(r, r) > 1e-20 ? normalize(r) : float3(0, 0, 1);
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                if (v.data.w > 0.5) { o.positionCS = float4(2, 2, 2, 1); return o; }   // de-duplicated: culled

                float3 centreWS;
                float  magnitude = v.data.x;
                float  nearFade = 1.0;
                if (_Space < 0.5)
                {
                    // Chunk-relative, eye-relative, precise near the chunk (contract 3.4), then f(d).
                    float3 rel = v.offset - _EyeInChunk.xyz;
                    float  du  = max(CS_Length(rel), 1e-30);
                    float3 dirWS = SafeIcrfDir(rel / du);
                    centreWS  = _CS_CentreEyeWS.xyz + dirWS * CS_CompressDistance(du * _Units.x);
                    magnitude = v.data.x + 5.0 * log10(max(du * _Units.y, 1e-12)) - 5.0;   // M -> m
                    nearFade  = smoothstep(_Look.z, _Look.w, du);
                }
                else if (_Space < 1.5)
                {
                    centreWS = TransformObjectToWorld(v.offset);          // placed by the CPU
                }
                else
                {
                    float3 dirWS = SafeIcrfDir(v.offset);
                    centreWS = CS_EyePositionWS() + dirWS * _CS_Compress.z;   // this eye: infinity
                }

                // Magnitude -> brightness and size, in one law (StarLook): brighter is bigger AND
                // brighter, with a size floor so the profile has pixels to resolve in.
                float b = saturate((_MagWindow.x - magnitude) / (_MagWindow.x - _MagWindow.y));
                uint  shape = (uint)v.data.z;
                float2 extent = shape == 1u ? _Shape.xx
                              : shape == 2u ? float2(_Shape.y, _Shape.z)
                              : shape == 3u ? float2(_Shape.z, _Shape.y) : float2(1.0, 1.0);
                float gain = shape == 0u ? 1.0 : (shape == 1u ? _ShapeGain.x : _ShapeGain.y) * saturate(2.0 * b - 1.0);

                float ppr = CS_PixelsPerRadian();
                float widthRad = clamp(lerp(_SizeDmm.x, _SizeDmm.y, b) * 1e-3, _PxBounds.x / ppr, _PxBounds.y / ppr);

                float4 cs = TransformWorldToHClip(centreWS);
                uint ci = (uint)v.data.y;
                float2 corner = float2((ci & 1u) ? 1.0 : -1.0, (ci & 2u) ? 1.0 : -1.0);
                cs.xy += corner * CS_AngularToClip(0.5 * widthRad * extent, cs.w);
                o.positionCS = cs;
                o.uv = corner;

                half3 tinted = lerp(half3(1, 1, 1), v.color.rgb, 0.30h + 0.70h * (half)b);   // faint stars desaturate
                half  level  = (half)(_Look.x + b * (1.0 - _Look.x));
                o.color = CS_DisplayToLinear(tinted * level) * (half)(gain * _Look.y * _Fade * nearFade);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half a = exp2(-4.0h * (half)dot(i.uv, i.uv));   // Gaussian: 1 at the centre, 1/16 at the edge
                return half4(i.color * a, 0.0h);                // additive; alpha 0
            }
            ENDHLSL
        }
    }
    Fallback Off
}
