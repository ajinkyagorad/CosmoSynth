// Analytic single-scattering atmosphere for CosmoSynth.
//
// PORTED IN PLACE from Built-in CG to URP HLSL (contract 7.2). The maths is unchanged, line for
// line; what changed is the pipeline skeleton, the camera accessor, and one added uniform:
//   - CGPROGRAM -> HLSLPROGRAM, UnityCG.cginc -> CosmoCore.hlsl;
//   - "RenderPipeline"="UniversalPipeline" and "LightMode"="UniversalForward" tags, without which
//     URP does not draw the pass at all and the shader is silently absent rather than wrong;
//   - _WorldSpaceCameraPos -> GetCameraPositionWS(), read AFTER the eye macro;
//   - material properties moved into CBUFFER_START(UnityPerMaterial) so the SRP Batcher works;
//   - VFACE -> URP's FRONT_FACE_SEMANTIC macros, which are correct on every backend;
//   - _PerVertexCompress, the Envelope-mode switch (contract 2.6, 7.2).
//
// Rendered on a sphere a few scale heights larger than the planet. The shell is only a way to
// get the fragment shader running over the right pixels: the actual work is a ray march, so the
// result does not depend on the shell's tessellation and stays smooth when a planet is scaled up
// to fill the room.
//
// The model is single-scattering ray marching: step along the view ray through the shell,
// accumulate Rayleigh and Mie density weighted by how much light reaches each sample, and
// apply the phase functions. It is not a full multiple-scattering solution — that needs
// precomputed transmittance tables and is not affordable per-frame on a mobile GPU — but it
// reproduces the effects that actually read: a blue limb, a red sunset at the terminator,
// forward-scattered haze around the star, and the sky thinning with altitude.
//
// ENVELOPE MODE (_PerVertexCompress = 1). When the eye is near a big body the body renders
// per-vertex-compressed, so its drawn shell is no longer a sphere in world space and a
// world-space integral against it is wrong. In that mode the integral runs in TRUE eye-relative
// space in units of the planet radius: the CPU supplies _PlanetCenter, _PlanetRadius,
// _AtmosphereRadius and the rescaled coefficients in that normalisation, and the ray origin
// becomes the origin. The view DIRECTION is unchanged either way, because compression is radial
// about the centre eye and preserves direction exactly.
//
// MIXED-REALITY RULE, and the reason for the odd blend mode below:
// on Quest the compositor treats framebuffer ALPHA as coverage. A normal additive pass
// (`Blend One One`) blends alpha by the same rule as colour, so a bright limb pixel saturates
// alpha to 1 and tells the compositor "this pixel is solid" — punching a hole in the
// passthrough view exactly where the atmosphere glows. `Blend One One, Zero One` adds colour
// while leaving destination alpha untouched, so the room still shows through.

Shader "CosmoSynth/Atmosphere"
{
    Properties
    {
        _PlanetRadius     ("Planet radius (world units, or 1 in Envelope mode)", Float) = 1.0
        _AtmosphereRadius ("Atmosphere radius (same units as _PlanetRadius)", Float) = 1.05
        _PlanetCenter     ("Planet centre (world, or true eye-relative in planet radii)", Vector) = (0,0,0,0)
        _SunDirection     ("Direction to star (world)", Vector) = (0,0,1,0)

        // Per-kilometre Rayleigh coefficients from the catalogue, rescaled into shell units by
        // the C# side. These are what make Earth's limb blue, Mars's butterscotch and Titan's
        // orange — the same numbers the provenance panel cites.
        _RayleighCoeff  ("Rayleigh scattering (RGB)", Vector) = (5.8, 13.5, 33.1, 0)
        _MieCoeff       ("Mie scattering", Float) = 2.1
        _MieG           ("Mie asymmetry", Range(-0.99, 0.99)) = 0.758
        _ScaleHeight    ("Density scale height (fraction of shell)", Range(0.01, 1.0)) = 0.25
        _Intensity      ("Star intensity", Float) = 18.0
        _Samples        ("View ray samples", Range(4, 24)) = 10
        _LightSamples   ("Light ray samples", Range(2, 8)) = 4
        _PerVertexCompress ("Envelope mode (contract 2.6)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+100" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Atmosphere"
            Tags { "LightMode" = "UniversalForward" }

            // Additive colour, destination alpha preserved — see the note above.
            Blend One One, Zero One
            ZWrite Off
            // Both faces, which is the only setting that covers every camera position.
            //
            // Culling back faces loses the atmosphere the moment the camera enters the shell —
            // and someone scaling a planet up to room size does that immediately. Culling front
            // faces is worse in a quieter way: the shell's far side is BEHIND the planet, so the
            // depth test rejects it across the entire disc and all that survives is a thin ring
            // outside the silhouette, with no sky over the daylit face and no terminator at all.
            // Rendering both and rejecting the duplicate in the fragment shader costs one
            // comparison and is right everywhere.
            Cull Off
            // Still depth-test, so nearer geometry occludes the glow.
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            // Stereo instancing. Without it the right eye is drawn with the left eye's camera
            // position and matrices: the limb lands in the wrong place in one eye, and the
            // result is a headache rather than a planet.
            #pragma multi_compile_instancing
            #include "CosmoCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _PlanetRadius;
                float  _AtmosphereRadius;
                float4 _PlanetCenter;
                float4 _SunDirection;
                float4 _RayleighCoeff;
                float  _MieCoeff;
                float  _MieG;
                float  _ScaleHeight;
                float  _Intensity;
                float  _Samples;
                float  _LightSamples;
                float  _PerVertexCompress;
            CBUFFER_END

            struct Attributes
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 pos     : SV_POSITION;
                float3 worldPos: TEXCOORD0;
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
                return o;
            }

            // Ray/sphere intersection. Returns false when the ray misses; otherwise the two
            // parametric distances along the (normalised) direction.
            bool RaySphere(float3 origin, float3 dir, float3 centre, float radius,
                           out float nearT, out float farT)
            {
                float3 oc = origin - centre;
                float b = dot(oc, dir);
                float c = dot(oc, oc) - radius * radius;
                float disc = b * b - c;
                if (disc < 0.0) { nearT = 0.0; farT = 0.0; return false; }
                float s = sqrt(disc);
                nearT = -b - s;
                farT  = -b + s;
                return farT > 0.0;
            }

            // Atmospheric density at a point: exponential falloff with altitude, which is what
            // the barometric law gives for an isothermal atmosphere and is the same assumption
            // the C# AtmosphereDefinition makes.
            float Density(float3 position, float3 centre, float planetR, float atmosR, float scaleHeight)
            {
                float altitude = (length(position - centre) - planetR) / max(1e-5, atmosR - planetR);
                return exp(-saturate(altitude) / scaleHeight);
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // Selects the correct eye's camera position and matrices under single-pass
                // instanced rendering. GetCameraPositionWS() below is per-eye only after this.
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 eyeWS = GetCameraPositionWS();
                // Rigid: world space. Envelope: true eye-relative space in units of the planet
                // radius, so the ray starts at the origin. Direction is the same either way.
                float3 camera = (_PerVertexCompress > 0.5) ? float3(0, 0, 0) : eyeWS;
                float3 centre = _PlanetCenter.xyz;
                float3 dir = normalize(i.worldPos - eyeWS);
                float3 sunDir = normalize(_SunDirection.xyz);

                float atmosNear, atmosFar;
                if (!RaySphere(camera, dir, centre, _AtmosphereRadius, atmosNear, atmosFar))
                    return half4(0, 0, 0, 0);

                // Reject the duplicate.
                //
                // The integral below depends only on the ray, not on which triangle spawned the
                // fragment, so a pixel covered by both the near and far side of the shell would
                // otherwise accumulate the same atmosphere twice — a ring of double brightness
                // around the limb, exactly where the effect is most visible. When the camera is
                // outside the shell (atmosNear > 0) the near face is the one that counts; when it
                // is inside, there is no near face to rasterise and the far one must do the work.
                bool isFront = IS_FRONT_VFACE(frontFace, true, false);
                if (!isFront && atmosNear > 0.0) return half4(0, 0, 0, 0);

                // Start at the camera when it is already inside the shell.
                float start = max(atmosNear, 0.0);
                float end = atmosFar;

                // Stop at the planet's surface — the solid body blocks everything behind it.
                float planetNear, planetFar;
                if (RaySphere(camera, dir, centre, _PlanetRadius, planetNear, planetFar) && planetNear > 0.0)
                    end = min(end, planetNear);

                if (end <= start) return half4(0, 0, 0, 0);

                int samples = (int)_Samples;
                float segment = (end - start) / samples;

                float3 rayleighSum = 0;
                float  mieSum = 0;
                float  opticalDepthR = 0;
                float  opticalDepthM = 0;

                [loop]
                for (int s = 0; s < samples; s++)
                {
                    float3 p = camera + dir * (start + (s + 0.5) * segment);
                    float density = Density(p, centre, _PlanetRadius, _AtmosphereRadius, _ScaleHeight);
                    float dR = density * segment;

                    opticalDepthR += dR;
                    opticalDepthM += dR;

                    // How much starlight reaches this sample: march toward the star and
                    // accumulate the density in the way. This is what produces the terminator
                    // and the red sunset, because a grazing light ray passes through far more
                    // air than an overhead one.
                    float lightNear, lightFar;
                    RaySphere(p, sunDir, centre, _AtmosphereRadius, lightNear, lightFar);

                    int lightSamples = (int)_LightSamples;
                    float lightSegment = max(lightFar, 0.0) / lightSamples;
                    float lightDepth = 0;
                    bool occluded = false;

                    [loop]
                    for (int l = 0; l < lightSamples; l++)
                    {
                        float3 lp = p + sunDir * ((l + 0.5) * lightSegment);
                        // A sample in the planet's shadow receives nothing.
                        if (length(lp - centre) < _PlanetRadius) { occluded = true; break; }
                        lightDepth += Density(lp, centre, _PlanetRadius, _AtmosphereRadius, _ScaleHeight)
                                      * lightSegment;
                    }

                    if (occluded) continue;

                    float3 tau = _RayleighCoeff.rgb * (lightDepth + opticalDepthR)
                               + _MieCoeff * 1.1 * (lightDepth + opticalDepthM);
                    float3 attenuation = exp(-tau);

                    rayleighSum += attenuation * dR;
                    mieSum += attenuation.g * dR;
                }

                // Phase functions: Rayleigh is nearly symmetric, Mie is strongly forward-biased,
                // which is why haze blooms around the star and the blue sky does not.
                float cosTheta = dot(dir, sunDir);
                float rayleighPhase = 0.0596831 * (1.0 + cosTheta * cosTheta);

                float g = _MieG;
                float g2 = g * g;
                float miePhase = 0.1193662 * ((1.0 - g2) * (1.0 + cosTheta * cosTheta)) /
                                 ((2.0 + g2) * pow(abs(1.0 + g2 - 2.0 * g * cosTheta), 1.5));

                float3 colour = (rayleighSum * _RayleighCoeff.rgb * rayleighPhase
                                 + mieSum * _MieCoeff * miePhase) * _Intensity;

                // Alpha stays 0: this pass only ADDS light. Writing coverage here is what would
                // black out passthrough behind the glow.
                return half4(max(colour, 0.0), 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
