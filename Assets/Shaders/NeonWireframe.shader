// Neon Wireframe shader for URP.
// Pass 1: Semi-transparent dark body fill (gives depth without hiding the grid behind).
// Pass 2: Geometry-shader wireframe edges rendered as bright neon lines.
// Supports MaterialPropertyBlock for per-instance color overrides.
Shader "SpaceDebris/NeonWireframe"
{
    Properties
    {
        _BaseColor      ("Base Fill Color",     Color)  = (0, 0.8, 1, 0.15)
        _EdgeColor      ("Edge Neon Color",     Color)  = (0, 0.8, 1, 1)
        _EmissionColor  ("Emission (HDR)",      Color)  = (0, 1.6, 2, 1)
        _EdgeThickness  ("Edge Thickness",      Range(0, 3)) = 1.2
        _EdgeSoftness   ("Edge Softness",       Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
        }

        // ── Pass 1: Dark transparent fill ────────────────────────────────────
        Pass
        {
            Name "FILL"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert_fill
            #pragma fragment frag_fill
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _EmissionColor;
                float _EdgeThickness;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert_fill(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag_fill(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                return half4(_BaseColor.rgb + _EmissionColor.rgb * 0.05, _BaseColor.a);
            }
            ENDHLSL
        }

        // ── Pass 2: Neon edge wireframe using barycentric coordinates ─────────
        // We rely on barycentric coords baked into UV2 by the helper script
        // NeonWireframeBaker on each spawned mesh. Falls back gracefully when
        // UV2 is absent (edges still render but cover full face).
        Pass
        {
            Name "WIREFRAME"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One          // Additive — edges stack bright over fill.
            ZWrite Off
            Cull Off               // Draw edges on both sides.

            HLSLPROGRAM
            #pragma vertex   vert_wire
            #pragma fragment frag_wire
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _EmissionColor;
                float _EdgeThickness;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 bary       : TEXCOORD1;   // XY = two barycentric coords; Z inferred.
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 bary       : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert_wire(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                // Reconstruct third barycentric coordinate.
                OUT.bary = float3(IN.bary.x, IN.bary.y, 1.0 - IN.bary.x - IN.bary.y);
                return OUT;
            }

            half4 frag_wire(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Distance to the nearest edge (minimum barycentric component).
                float minBary = min(IN.bary.x, min(IN.bary.y, IN.bary.z));

                // Anti-aliased edge mask using fwidth for screen-space consistent thickness.
                float fw       = fwidth(minBary);
                float edgeMask = 1.0 - smoothstep(fw * _EdgeThickness * _EdgeSoftness,
                                                   fw * _EdgeThickness, minBary);

                half3 neonColor = _EdgeColor.rgb + _EmissionColor.rgb;
                return half4(neonColor * edgeMask, edgeMask);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
