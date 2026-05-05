Shader "Custom/SpaceNebulaRaymarching"
{
    Properties
    {
        [Header(Color)]
        _CloudColor1 ("Color A  Dense Core", Color) = (0.2, 0.6, 1.0, 1.0)
        _CloudColor2 ("Color B  Mid Layer",  Color) = (0.8, 0.2, 0.9, 1.0)
        _CloudColor3 ("Color C  Thin Wisps", Color) = (0.05, 0.8, 0.95, 1.0)
        _Brightness    ("Brightness",           Range(0, 10)) = 2.5
        _EmissionBoost ("Emission Boost",       Range(0,  8)) = 2.0
        _Contrast      ("Contrast Gamma",       Range(0.3, 3)) = 1.2

        [Header(Volume Shape)]
        _CloudScale   ("Noise Scale",        Range(0.05, 8))  = 1.5
        _CloudDensity ("Density Threshold",  Range(0, 1))     = 0.42
        _DetailScale  ("Detail Multiplier",  Range(1, 8))     = 3.5
        _DetailAmount ("Detail Carve",       Range(0, 1))     = 0.4
        _WispStrength ("Domain Warp",        Range(0, 3))     = 1.2
        _Turbulence   ("Turbulence",         Range(0, 2))     = 0.6

        [Header(Movement)]
        _ScrollSpeedX ("Scroll X", Range(-0.1, 0.1)) =  0.018
        _ScrollSpeedY ("Scroll Y", Range(-0.1, 0.1)) =  0.007
        _ScrollSpeedZ ("Scroll Z", Range(-0.1, 0.1)) =  0.013

        [Header(Raymarching)]
        _Steps         ("Steps  Quality",  Range(16, 256)) = 96
        _AbsorptionFactor ("Absorption",   Range(0, 10))   = 2.5
        _EdgeFade      ("Box Edge Fade",   Range(0, 0.5))  = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue"            = "Transparent"
            "RenderType"       = "Transparent"
            "RenderPipeline"   = "UniversalPipeline"
            "IgnoreProjector"  = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Pre-multiplied alpha: Blend One OneMinusSrcAlpha
            // Allows HDR glow accumulation without "dark halo" artefacts
            Blend One OneMinusSrcAlpha
            ZWrite Off
            // Always render — ignore depth buffer so SkyboxCube can't occlude the nebula
            ZTest Always
            // Cull Front: rasterize BACK faces only.
            // This is the key: whether the camera is OUTSIDE or INSIDE the volume,
            // the back face of the cube is always visible — we ray-march from the
            // camera position toward that back face, giving true 3-D parallax.
            Cull Front

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // -----------------------------------------------------------------------
            // Structs
            // -----------------------------------------------------------------------

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionOS  : TEXCOORD0;   // back-face position in object space
                float3 positionWS  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // -----------------------------------------------------------------------
            // Uniforms
            // -----------------------------------------------------------------------

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudColor1;
                float4 _CloudColor2;
                float4 _CloudColor3;
                float  _Brightness;
                float  _EmissionBoost;
                float  _Contrast;
                float  _CloudScale;
                float  _CloudDensity;
                float  _DetailScale;
                float  _DetailAmount;
                float  _WispStrength;
                float  _Turbulence;
                float  _ScrollSpeedX;
                float  _ScrollSpeedY;
                float  _ScrollSpeedZ;
                float  _Steps;
                float  _AbsorptionFactor;
                float  _EdgeFade;
            CBUFFER_END

            // -----------------------------------------------------------------------
            // Value noise — quintic interpolation
            // -----------------------------------------------------------------------

            float hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float vnoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0); // quintic

                return lerp(
                    lerp(lerp(hash3(i            ), hash3(i + float3(1,0,0)), u.x),
                         lerp(hash3(i+float3(0,1,0)), hash3(i + float3(1,1,0)), u.x), u.y),
                    lerp(lerp(hash3(i+float3(0,0,1)), hash3(i + float3(1,0,1)), u.x),
                         lerp(hash3(i+float3(0,1,1)), hash3(i + float3(1,1,1)), u.x), u.y),
                    u.z);
            }

            // -----------------------------------------------------------------------
            // FBM variants — unrolled to avoid dynamic-loop GPU driver issues
            // -----------------------------------------------------------------------

            // 5-octave high-quality FBM (base shape)
            float fbm5(float3 p)
            {
                float v  = 0.0;
                float a  = 0.5;
                v += a * vnoise(p); p *= 2.01; a *= 0.5;
                v += a * vnoise(p); p *= 2.02; a *= 0.5;
                v += a * vnoise(p); p *= 2.03; a *= 0.5;
                v += a * vnoise(p); p *= 2.01; a *= 0.5;
                v += a * vnoise(p);
                return v; // range ≈ [0, 0.97]
            }

            // 3-octave medium FBM (domain warp & detail)
            float fbm3(float3 p)
            {
                float v = 0.0;
                v += 0.500 * vnoise(p); p *= 2.02;
                v += 0.250 * vnoise(p); p *= 2.01;
                v += 0.250 * vnoise(p);
                return v;
            }

            // 2-octave cheap FBM (color sampling)
            float fbm2(float3 p)
            {
                float v = 0.0;
                v += 0.5 * vnoise(p); p *= 2.01;
                v += 0.5 * vnoise(p);
                return v;
            }

            // -----------------------------------------------------------------------
            // Density field
            // -----------------------------------------------------------------------
            //
            // Architecture (similar to Nebula Forge):
            //   1. Two-level domain warp  →  organic tendrils & filaments
            //   2. Turbulence layer       →  breaks up regularity
            //   3. BOX-face edge fade     →  rectangular volume, no sphere
            //   4. All-positive density accumulation per march step

            float sampleDensity(float3 posL)
            {
                float3 scroll = float3(_ScrollSpeedX, _ScrollSpeedY, _ScrollSpeedZ) * _Time.y;
                float3 p      = posL * _CloudScale + scroll * _CloudScale;

                // ── Level-1 domain warp (large scale, creates pillar-like structures)
                float3 warp1 = float3(
                    fbm3(p + float3(1.7,  9.2,  3.4)),
                    fbm3(p + float3(8.3,  2.8,  5.1)),
                    fbm3(p + float3(4.1,  6.7,  0.9))
                ) * 2.0 - 1.0; // remap [0,1]→[-1,1]

                float3 p2 = p + warp1 * _WispStrength;

                // ── Level-2 domain warp (smaller scale, creates thin wisps)
                float3 warp2 = float3(
                    fbm2(p2 + float3(5.2, 1.3, 7.8)),
                    fbm2(p2 + float3(2.9, 6.4, 3.1)),
                    fbm2(p2 + float3(7.6, 3.8, 1.5))
                ) * 2.0 - 1.0;

                float3 warpedP = p2 + warp2 * (_WispStrength * 0.5);

                // ── Base shape
                float base   = fbm5(warpedP);

                // ── Detail carve (punches holes for organic silhouette)
                float detail = fbm3(warpedP * _DetailScale);
                float density = base - (1.0 - detail) * _DetailAmount;

                // ── Turbulence (breaks up banding, adds cloudiness)
                float turb   = abs(vnoise(warpedP * 1.7 + scroll * 0.5) * 2.0 - 1.0);
                density     -= turb * _Turbulence * 0.15;

                // ── Threshold remap — sharper definition
                density = saturate((density - (1.0 - _CloudDensity)) * 5.0);

                // ── BOX-face edge fade (NOT spherical!)
                //    Computes min distance to any of the 6 faces → rectangular fade
                float3 edgeDist  = 0.5 - abs(posL);
                float  boxFade   = min(min(edgeDist.x, edgeDist.y), edgeDist.z);
                density         *= smoothstep(0.0, _EdgeFade + 0.001, boxFade);

                return density;
            }

            // -----------------------------------------------------------------------
            // Color
            // -----------------------------------------------------------------------

            float3 sampleColor(float density, float3 posL)
            {
                float3 scroll = float3(_ScrollSpeedX, _ScrollSpeedY, _ScrollSpeedZ) * _Time.y;
                float  n      = fbm2(posL * _CloudScale * 0.5 + scroll);
                float  t      = saturate(n * 1.6);
                float3 col    = lerp(_CloudColor1.rgb, _CloudColor2.rgb, t);
                       col    = lerp(col, _CloudColor3.rgb, saturate((density - 0.4) * 2.5));
                return col;
            }

            // -----------------------------------------------------------------------
            // Ray–AABB intersection (object space, box = [-0.5, 0.5]³)
            // -----------------------------------------------------------------------

            bool rayBoxIntersect(float3 ro, float3 rd, out float tNear, out float tFar)
            {
                // Guard against division by zero on axis-aligned rays
                float3 invDir = rcp(rd + sign(rd) * 1e-6 + 1e-10);
                float3 t0 = (float3(-0.5,-0.5,-0.5) - ro) * invDir;
                float3 t1 = (float3( 0.5, 0.5, 0.5) - ro) * invDir;
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                tNear = max(max(tMin.x, tMin.y), tMin.z);
                tFar  = min(min(tMax.x, tMax.y), tMax.z);
                return tFar > max(tNear, 0.0);
            }

            // -----------------------------------------------------------------------
            // Raymarching
            // -----------------------------------------------------------------------

            float4 raymarch(float3 camPosL, float3 rayDirL, float marchStart, float marchLength)
            {
                float3 accumColor = float3(0, 0, 0);
                float  transmit   = 1.0;

                int   steps    = clamp((int)_Steps, 16, 256);
                // Step size derived from actual march length — uniform coverage
                float stepSize = marchLength / (float)steps;

                float3 pos = camPosL + rayDirL * marchStart;

                UNITY_LOOP
                for (int i = 0; i < steps; i++)
                {
                    if (transmit < 0.004) break;

                    float d = sampleDensity(pos);

                    if (d > 0.004)
                    {
                        float3 col = sampleColor(d, pos);

                        // Emission: base brightness + boost in dense knots
                        float emit = _Brightness + _EmissionBoost * smoothstep(0.25, 1.0, d);
                        col       *= emit;

                        // Beer–Lambert: physically-based opacity accumulation
                        float sigma  = d * _AbsorptionFactor;
                        float alpha  = 1.0 - exp(-sigma * stepSize);

                        accumColor  += col * alpha * transmit;
                        transmit    *= (1.0 - alpha);
                    }

                    pos += rayDirL * stepSize;
                }

                // Per-channel contrast (gamma curve) — applied to colour only
                float ig = 1.0 / max(_Contrast, 0.01);
                accumColor = pow(max(accumColor, 1e-5), float3(ig, ig, ig));

                return float4(accumColor, 1.0 - transmit);
            }

            // -----------------------------------------------------------------------
            // Vertex shader
            // -----------------------------------------------------------------------

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.positionOS = IN.positionOS.xyz;   // back-face OS position → ray endpoint
                return OUT;
            }

            // -----------------------------------------------------------------------
            // Fragment shader
            // -----------------------------------------------------------------------

            half4 frag(Varyings IN) : SV_Target
            {
                // Build ray in object space: from camera toward the back face vertex
                float3 cameraWS  = GetCameraPositionWS();
                float3 camPosL   = TransformWorldToObject(cameraWS);
                float3 rayDirL   = normalize(IN.positionOS - camPosL);

                // AABB test: gives [tNear, tFar] along the ray
                float tNear, tFar;
                if (!rayBoxIntersect(camPosL, rayDirL, tNear, tFar))
                    return half4(0, 0, 0, 0);

                // If camera is INSIDE the volume: tNear < 0 → start marching from camera
                float marchStart  = max(tNear, 0.0);
                float marchLength = max(tFar - marchStart, 0.001);

                float4 col = raymarch(camPosL, rayDirL, marchStart, marchLength);

                // Pre-multiply alpha for Blend One OneMinusSrcAlpha
                col.rgb *= col.a;
                return half4(col);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
