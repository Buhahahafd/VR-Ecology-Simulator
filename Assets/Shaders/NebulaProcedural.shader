Shader "Custom/NebulaProcedural"
{
    Properties
    {
        [Header(Colors)]
        _ColorA         ("Color A",             Color)          = (0.6, 0.2, 1.0, 1.0)
        _ColorB         ("Color B",             Color)          = (0.1, 0.7, 1.0, 1.0)
        _Brightness     ("Brightness",          Range(0, 8))    = 4.0

        [Header(Shape)]
        _Density        ("Density",             Range(0, 3))    = 1.5
        _Scale          ("Scale",               Range(0.1, 5))  = 1.8
        _EdgeSoftness   ("Edge Softness",       Range(0, 1))    = 0.35

        [Header(Animation)]
        _AnimSpeed      ("Animation Speed",     Range(0, 1))    = 0.08
        _ScrollDir      ("Scroll Direction",    Vector)         = (1, 0.3, 0.5, 0)

        [Header(Raymarching)]
        _Steps          ("Steps",               Range(16, 128)) = 64
        _StepSize       ("Step Size",           Range(0.01, 0.3)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue"               = "Transparent"
            "RenderType"          = "Transparent"
            "RenderPipeline"      = "UniversalPipeline"
            "IgnoreProjector"     = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend  SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest  LEqual
            Cull   Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Structs ──────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            // ── Uniforms ─────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float  _Brightness;
                float  _Density;
                float  _Scale;
                float  _EdgeSoftness;
                float  _AnimSpeed;
                float4 _ScrollDir;
                float  _Steps;
                float  _StepSize;
            CBUFFER_END

            // ── Noise ────────────────────────────────────────────────
            // Value noise hash (returns float in [0,1])
            float Hash(float3 p)
            {
                p  = frac(p * float3(443.897, 441.423, 437.195));
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            // Smooth 3-D value noise
            float Noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);   // smoothstep

                return lerp(
                    lerp(
                        lerp(Hash(i + float3(0,0,0)), Hash(i + float3(1,0,0)), u.x),
                        lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), u.x),
                        u.y),
                    lerp(
                        lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), u.x),
                        lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), u.x),
                        u.y),
                    u.z);
            }

            // Fractal Brownian Motion – 5 octaves
            float FBM(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                float3 shift = float3(100, 100, 100);

                for (int i = 0; i < 5; i++)
                {
                    v += a * Noise3D(p);
                    p  = p * 2.0 + shift;
                    a *= 0.5;
                }
                return v;
            }

            // ── Density sample ───────────────────────────────────────
            float SampleDensity(float3 posLocal)
            {
                // Animate by offsetting the noise coordinate over time
                float3 animated = posLocal * _Scale
                    + _ScrollDir.xyz * _Time.y * _AnimSpeed;

                float base   = FBM(animated);
                float detail = FBM(animated * 2.1 + float3(5.2, 1.3, 9.8));

                // Mix base and detail, then threshold with Density control
                float d = base + detail * 0.35;
                d = saturate((d - (1.0 - _Density * 0.6)) * 3.5);

                // Soft sphere mask so edges dissolve naturally
                float dist      = length(posLocal);
                float edgeMask  = 1.0 - smoothstep(0.5 - _EdgeSoftness, 0.5, dist);
                return d * edgeMask;
            }

            // ── Ray–AABB intersection ────────────────────────────────
            // Returns true if the ray hits [-0.5,0.5]^3.
            // tEntry: distance to box entry (>=0), tExit: distance to box exit.
            bool RayBox(float3 ro, float3 rd, out float tEntry, out float tExit)
            {
                float3 inv = 1.0 / (rd + 1e-6);         // avoid div-by-zero
                float3 t0  = (float3(-0.5, -0.5, -0.5) - ro) * inv;
                float3 t1  = (float3( 0.5,  0.5,  0.5) - ro) * inv;
                float3 tNr = min(t0, t1);
                float3 tFr = max(t0, t1);
                tEntry = max(max(tNr.x, tNr.y), tNr.z);
                tExit  = min(min(tFr.x, tFr.y), tFr.z);
                return tExit > max(tEntry, 0.0);
            }

            // ── Raymarch ─────────────────────────────────────────────
            float4 Raymarch(float3 ro, float3 rd)
            {
                float tEntry, tExit;
                if (!RayBox(ro, rd, tEntry, tExit))
                    return float4(0, 0, 0, 0);

                float3 pos   = ro + rd * max(tEntry, 0.0);
                float3 col   = float3(0, 0, 0);
                float  alpha = 0.0;

                int   steps    = (int)clamp(_Steps, 16, 128);
                float stepSize = _StepSize;

                for (int i = 0; i < steps; i++)
                {
                    if (alpha >= 0.98) break;
                    if (length(pos) > 0.5) break;

                    float d = SampleDensity(pos);

                    if (d > 0.005)
                    {
                        // Color from noise position → lerp between two palette colors
                        float  t       = FBM(pos * _Scale * 0.6 + float3(1.7, 9.2, 0.4));
                        float3 nebCol  = lerp(_ColorA.rgb, _ColorB.rgb, saturate(t));
                        nebCol        *= _Brightness;

                        // Front-to-back compositing
                        float contrib  = d * stepSize * (1.0 - alpha);
                        col   += nebCol * contrib;
                        alpha += contrib * 1.6;
                    }

                    pos += rd * stepSize;
                }

                return float4(col, saturate(alpha));
            }

            // ── Vertex ───────────────────────────────────────────────
            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            // ── Fragment ─────────────────────────────────────────────
            half4 Frag(Varyings IN) : SV_Target
            {
                // Ray origin = camera in object space
                float3 camWS  = GetCameraPositionWS();
                float3 ro     = TransformWorldToObject(camWS);

                // Ray direction = from camera toward this fragment, in object space
                float3 dirWS  = normalize(IN.positionWS - camWS);
                float3 rd     = normalize(TransformWorldToObjectDir(dirWS));

                return Raymarch(ro, rd);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
