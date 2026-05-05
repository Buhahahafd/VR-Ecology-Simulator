Shader "Custom/NMSSkybox"
{
    // ─────────────────────────────────────────────────────────────────────────
    // Cinematic Space Skybox — Unified single-pass renderer
    //
    // Architecture (one draw call, zero overdraw):
    //   1. Deep-space gradient
    //   2. Dense star field + bright accents  (no marching)
    //   3. Galactic dust band                 (FBM, cheap)
    //   4. Nebula                             (48-step march, 3 blobs, shared loop)
    //
    // Optimisation vs. previous 3-cube setup:
    //   • 1 draw call  vs 4  (SkyboxCube + 3 nebula volumes)
    //   • 48 steps     vs 96 × 3 = 288 in overdraw regions
    //   • 3-oct FBM    vs 5-oct + double domain-warp per sample
    //   • No transparent blend — writes directly to background
    // ─────────────────────────────────────────────────────────────────────────

    Properties
    {
        [Header(Sky Gradient)]
        _HorizonColor   ("Horizon Color",    Color) = (0.04, 0.01, 0.08, 1)
        _ZenithColor    ("Zenith Color",     Color) = (0.00, 0.00, 0.03, 1)
        _GradientPower  ("Gradient Power",   Range(0.5, 4)) = 1.6

        [Header(Dense Star Field)]
        _StarDensity    ("Star Density",     Range(100, 600)) = 340
        _StarBrightness ("Star Brightness",  Range(0, 4))     = 1.6
        _StarSharpness  ("Star Sharpness",   Range(4, 32))    = 20.0
        _StarTwinkle    ("Twinkle Speed",    Range(0, 3))     = 0.6

        [Header(Bright Accent Stars)]
        _AccentDensity  ("Accent Density",   Range(10, 120))  = 60
        _AccentSize     ("Accent Size",      Range(0.002, 0.06)) = 0.016
        _AccentGlow     ("Accent Glow",      Range(0, 8))     = 4.5

        [Header(Galactic Dust Band)]
        _HazeColor      ("Haze Color",       Color) = (0.08, 0.02, 0.14, 1)
        _HazeIntensity  ("Haze Intensity",   Range(0, 1))   = 0.28
        _HazeScale      ("Haze Scale",       Range(0.1, 4)) = 1.2
        _HazeBandWidth  ("Band Width",       Range(0.05, 1)) = 0.30

        [Header(Nebula A — Blue Purple)]
        _NebColorA1     ("Color A Dense",    Color) = (0.05, 0.10, 0.75, 1)
        _NebColorA2     ("Color A Wisps",    Color) = (0.45, 0.05, 0.70, 1)
        _NebPosA        ("Position A",       Vector) = (0.6, 0.3, 0.5, 0)
        _NebScaleA      ("Scale A",          Range(0.1, 3)) = 0.9
        _NebBrightnessA ("Brightness A",     Range(0, 6)) = 3.5
        _NebRadiusA     ("Radius A",         Range(0.1, 1.5)) = 0.85

        [Header(Nebula B — Crimson)]
        _NebColorB1     ("Color B Dense",    Color) = (0.70, 0.03, 0.18, 1)
        _NebColorB2     ("Color B Wisps",    Color) = (0.40, 0.02, 0.55, 1)
        _NebPosB        ("Position B",       Vector) = (-0.5, -0.2, 0.7, 0)
        _NebScaleB      ("Scale B",          Range(0.1, 3)) = 1.1
        _NebBrightnessB ("Brightness B",     Range(0, 6)) = 2.8
        _NebRadiusB     ("Radius B",         Range(0.1, 1.5)) = 0.65

        [Header(Nebula C — Teal Haze)]
        _NebColorC1     ("Color C Dense",    Color) = (0.03, 0.45, 0.55, 1)
        _NebColorC2     ("Color C Wisps",    Color) = (0.06, 0.60, 0.38, 1)
        _NebPosC        ("Position C",       Vector) = (0.1, 0.7, -0.4, 0)
        _NebScaleC      ("Scale C",          Range(0.1, 3)) = 0.75
        _NebBrightnessC ("Brightness C",     Range(0, 6)) = 2.2
        _NebRadiusC     ("Radius C",         Range(0.1, 1.5)) = 0.55

        [Header(Nebula Shared)]
        _NebScrollSpeed ("Scroll Speed",     Range(0, 0.05)) = 0.006
        _NebDensity     ("Density",          Range(0, 1))    = 0.48
        _NebSteps       ("March Steps",      Range(16, 64))  = 48
        _NebAbsorption  ("Absorption",       Range(0.5, 6))  = 2.2

        [Header(Rotation)]
        _SkyRotation    ("Sky Rotation (deg)", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Background-10"
            "RenderType"     = "Background"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Front
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "CinematicSkybox"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ─── Structs ─────────────────────────────────────────────────────

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir        : TEXCOORD0;
            };

            // ─── Uniforms ─────────────────────────────────────────────────────

            CBUFFER_START(UnityPerMaterial)
                float4 _HorizonColor;
                float4 _ZenithColor;
                float  _GradientPower;

                float  _StarDensity;
                float  _StarBrightness;
                float  _StarSharpness;
                float  _StarTwinkle;

                float  _AccentDensity;
                float  _AccentSize;
                float  _AccentGlow;

                float4 _HazeColor;
                float  _HazeIntensity;
                float  _HazeScale;
                float  _HazeBandWidth;

                float4 _NebColorA1;
                float4 _NebColorA2;
                float4 _NebPosA;
                float  _NebScaleA;
                float  _NebBrightnessA;
                float  _NebRadiusA;

                float4 _NebColorB1;
                float4 _NebColorB2;
                float4 _NebPosB;
                float  _NebScaleB;
                float  _NebBrightnessB;
                float  _NebRadiusB;

                float4 _NebColorC1;
                float4 _NebColorC2;
                float4 _NebPosC;
                float  _NebScaleC;
                float  _NebBrightnessC;
                float  _NebRadiusC;

                float  _NebScrollSpeed;
                float  _NebDensity;
                float  _NebSteps;
                float  _NebAbsorption;

                float  _SkyRotation;
            CBUFFER_END

            // ─── Hash & Noise ─────────────────────────────────────────────────

            float hash31(float3 p)
            {
                p  = frac(p * float3(443.8975, 397.2973, 491.1871));
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            // Value noise — quintic smooth
            float vnoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                return lerp(
                    lerp(lerp(hash31(i),              hash31(i+float3(1,0,0)), u.x),
                         lerp(hash31(i+float3(0,1,0)), hash31(i+float3(1,1,0)), u.x), u.y),
                    lerp(lerp(hash31(i+float3(0,0,1)), hash31(i+float3(1,0,1)), u.x),
                         lerp(hash31(i+float3(0,1,1)), hash31(i+float3(1,1,1)), u.x), u.y),
                    u.z);
            }

            // 3-octave FBM
            float fbm3(float3 p)
            {
                float v = 0.0;
                v += 0.500 * vnoise(p); p *= 2.01;
                v += 0.250 * vnoise(p); p *= 2.02;
                v += 0.250 * vnoise(p);
                return v;
            }

            // 2-octave FBM — colour & warp
            float fbm2(float3 p)
            {
                float v = 0.0;
                v += 0.6 * vnoise(p); p *= 2.0;
                v += 0.4 * vnoise(p);
                return v;
            }

            // ─── Sky helpers ──────────────────────────────────────────────────

            float3 rotateY(float3 d, float deg)
            {
                float a = deg * (PI / 180.0);
                float s = sin(a), c = cos(a);
                return float3(d.x*c + d.z*s, d.y, -d.x*s + d.z*c);
            }

            // ─── Star field ───────────────────────────────────────────────────

            float StarField(float3 dir, float density, float sharpness, float twinkle)
            {
                float3 p    = dir * density;
                float3 cell = floor(p);
                float3 f    = frac(p);
                float  bright = 0.0;

                UNITY_UNROLL
                for (int x = -1; x <= 1; x++)
                UNITY_UNROLL
                for (int y = -1; y <= 1; y++)
                UNITY_UNROLL
                for (int z = -1; z <= 1; z++)
                {
                    float3 nb  = cell + float3(x, y, z);
                    float3 off = float3(hash31(nb+13.1), hash31(nb+27.3), hash31(nb+51.7));
                    float  dist = length(float3(x,y,z) + off - f);
                    float  tw  = 1.0 - 0.22 * sin(_Time.y * twinkle + hash31(nb) * 6.283);
                    bright = max(bright, pow(saturate(1.0 - dist * sharpness * 0.12), sharpness) * tw);
                }
                return bright;
            }

            float3 AccentStars(float3 dir, float density, float size, float glow)
            {
                float3 result = 0;
                float3 p    = dir * density;
                float3 cell = floor(p);
                float3 f    = frac(p);

                UNITY_UNROLL
                for (int x = -1; x <= 1; x++)
                UNITY_UNROLL
                for (int y = -1; y <= 1; y++)
                UNITY_UNROLL
                for (int z = -1; z <= 1; z++)
                {
                    float3 nb   = cell + float3(x, y, z);
                    if (hash31(nb * 7.3 + 3.1) < 0.35) continue;

                    float3 off  = float3(hash31(nb+91.3), hash31(nb+53.7), hash31(nb+17.9));
                    float  dist = length(float3(x,y,z) + off - f);

                    float core  = saturate(1.0 - dist / size * density * 0.003);
                    float halo  = pow(saturate(1.0 - dist / (size*5.0) * density * 0.003), 2.5);

                    float hue = hash31(nb * 3.7);
                    float3 col;
                    if      (hue < 0.25) col = float3(1.0, 0.70, 0.80);
                    else if (hue < 0.50) col = float3(0.75, 0.87, 1.00);
                    else if (hue < 0.72) col = float3(1.00, 0.94, 0.78);
                    else                 col = float3(0.65, 1.00, 0.80);

                    result += col * (core * glow + halo * glow * 0.30);
                }
                return result;
            }

            // ─── Nebula ───────────────────────────────────────────────────────
            //
            // Three spherical blobs, sampled in a single shared march loop.
            // Single-level domain warp + 3-oct FBM — half the cost of old shader.
            // Sphere falloff replaces box edge fade — no AABB test needed.

            float nebulaDensity(float3 pos, float3 blobCenter, float radius,
                                float scale, float scrollMult)
            {
                float3 delta  = pos - blobCenter;
                float  dist   = length(delta);
                float  sphere = saturate(1.0 - dist / radius);
                if (sphere < 0.002) return 0.0;

                float3 scroll = float3(1.3, 0.7, 1.1) * _Time.y * _NebScrollSpeed * scrollMult;
                float3 p      = pos * scale + scroll;

                // Single domain warp — cheaper, still organic
                float3 warp = float3(
                    fbm2(p + float3(1.7, 9.2, 3.4)),
                    fbm2(p + float3(8.3, 2.8, 5.1)),
                    fbm2(p + float3(4.1, 6.7, 0.9))
                ) * 2.0 - 1.0;

                float d = fbm3(p + warp * 0.55);
                d = saturate((d - (1.0 - _NebDensity)) * 4.5);
                d *= sphere * sphere;
                return d;
            }

            float3 MarchNebula(float3 dir)
            {
                float3 accum    = 0;
                float  transmit = 1.0;

                int   steps    = clamp((int)_NebSteps, 16, 64);
                float stepSize = 1.0 / (float)steps;

                UNITY_LOOP
                for (int i = 0; i < steps; i++)
                {
                    if (transmit < 0.005) break;

                    // Sample point along normalised direction (conceptual unit sphere)
                    float3 pos = dir * ((float(i) + 0.5) * stepSize);

                    // ── Blob A
                    float dA = nebulaDensity(pos, _NebPosA.xyz, _NebRadiusA, _NebScaleA, 1.0);
                    if (dA > 0.002)
                    {
                        float3 colA = lerp(_NebColorA1.rgb, _NebColorA2.rgb,
                                          saturate(fbm2(pos * _NebScaleA * 0.4)));
                        float  alp  = 1.0 - exp(-dA * _NebAbsorption * stepSize);
                        accum      += colA * _NebBrightnessA * alp * transmit;
                        transmit   *= (1.0 - alp);
                    }

                    // ── Blob B
                    float dB = nebulaDensity(pos, _NebPosB.xyz, _NebRadiusB, _NebScaleB, 0.8);
                    if (dB > 0.002)
                    {
                        float3 colB = lerp(_NebColorB1.rgb, _NebColorB2.rgb,
                                          saturate(fbm2(pos * _NebScaleB * 0.4 + 5.1)));
                        float  alp  = 1.0 - exp(-dB * _NebAbsorption * stepSize);
                        accum      += colB * _NebBrightnessB * alp * transmit;
                        transmit   *= (1.0 - alp);
                    }

                    // ── Blob C
                    float dC = nebulaDensity(pos, _NebPosC.xyz, _NebRadiusC, _NebScaleC, 1.2);
                    if (dC > 0.002)
                    {
                        float3 colC = lerp(_NebColorC1.rgb, _NebColorC2.rgb,
                                          saturate(fbm2(pos * _NebScaleC * 0.4 + 3.9)));
                        float  alp  = 1.0 - exp(-dC * _NebAbsorption * stepSize);
                        accum      += colC * _NebBrightnessC * alp * transmit;
                        transmit   *= (1.0 - alp);
                    }
                }

                return accum;
            }

            // ─── Vertex ───────────────────────────────────────────────────────

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 dir = IN.positionOS.xyz;
                dir = rotateY(dir, _SkyRotation);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir        = dir;
                return OUT;
            }

            // ─── Fragment ─────────────────────────────────────────────────────

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dir);

                // 1. Deep-space gradient
                float  t   = saturate(pow(abs(dir.y * 0.5 + 0.5), _GradientPower));
                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, t);

                // 2. Galactic dust band (cheap FBM, no march)
                float band = exp(-abs(dir.y) / max(_HazeBandWidth, 0.01));
                float haze = fbm3(dir * _HazeScale) * band;
                sky += _HazeColor.rgb * haze * _HazeIntensity;

                // 3. Dense stars (analytic, no march)
                sky += StarField(dir, _StarDensity, _StarSharpness, _StarTwinkle) * _StarBrightness;

                // 4. Bright accent stars
                sky += AccentStars(dir, _AccentDensity, _AccentSize, _AccentGlow);

                // 5. Nebula — single shared march, 3 blobs
                sky += MarchNebula(dir);

                return half4(sky, 1.0);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
