// Neon Grid shader for URP.
// Renders a perspective infinite-looking grid on a flat plane.
// Supports a major/minor grid, fade-out by distance, and HDR emission for Bloom.
Shader "SpaceDebris/NeonGrid"
{
    Properties
    {
        _GridColor      ("Grid Line Color (HDR)", Color)  = (0, 0.6, 1, 1)
        _BgColor        ("Background Color",      Color)  = (0, 0, 0, 0.85)
        _GridScale      ("Grid Scale (tiles/unit)", Float) = 1.0
        _LineWidth      ("Line Width",            Range(0.005, 0.1)) = 0.025
        _MajorEvery     ("Major Line Every N",    Float)  = 5.0
        _MajorBrightness("Major Line Brightness", Float)  = 3.0
        _MinorBrightness("Minor Line Brightness", Float)  = 1.0
        _FadeStart      ("Fade Start Distance",   Float)  = 4.0
        _FadeEnd        ("Fade End Distance",     Float)  = 12.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "GRID"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GridColor;
                half4 _BgColor;
                float _GridScale;
                float _LineWidth;
                float _MajorEvery;
                float _MajorBrightness;
                float _MinorBrightness;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 worldXZ     : TEXCOORD0;   // World-space XZ for grid sampling.
                float  distToCamera: TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float3 worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS     = TransformWorldToHClip(worldPos);
                OUT.worldXZ        = worldPos.xz;
                OUT.distToCamera   = distance(worldPos, GetCameraPositionWS());
                return OUT;
            }

            // Returns a 0-1 grid mask: 1 on a line, 0 off.
            float GridLine(float2 p, float scale, float lineWidth)
            {
                float2 grid  = frac(p * scale);
                float2 half2 = float2(0.5, 0.5);
                // Center the fraction so lines are at the cell edges.
                float2 d = abs(grid - half2) - (0.5 - lineWidth * scale * 0.5);
                float2 fw = fwidth(p * scale);
                float2 mask = 1.0 - smoothstep(float2(0, 0), fw * 1.5, d);
                return saturate(max(mask.x, mask.y));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 p = IN.worldXZ;

                // Minor grid.
                float minor = GridLine(p, _GridScale, _LineWidth);

                // Major grid (every N minor cells).
                float major = GridLine(p, _GridScale / _MajorEvery, _LineWidth * 1.6);

                // Combine: major overrides minor.
                float minorMask = minor * (1.0 - major);
                float majorMask = major;

                half3 color = _GridColor.rgb * (minorMask * _MinorBrightness
                                              + majorMask * _MajorBrightness);
                float alpha = saturate(minorMask + majorMask);

                // Distance fade.
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, IN.distToCamera);
                alpha *= fade;

                // Blend with background.
                half3 finalColor = lerp(_BgColor.rgb, color, alpha);
                float finalAlpha = lerp(_BgColor.a * (1.0 - fade * 0.5), saturate(alpha + _BgColor.a * 0.4), alpha);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/InternalErrorShader"
}
