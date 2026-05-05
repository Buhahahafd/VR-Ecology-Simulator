Shader "Custom/NoMansSkyNebula"
{
    Properties
    {
        [Header(Cloud Colors)]
        _CloudColor1 ("Cloud Color 1", Color) = (0.5, 0.8, 1.0, 1.0)
        _CloudColor2 ("Cloud Color 2", Color) = (1.0, 0.4, 0.9, 1.0)
        _Brightness ("Brightness", Range(0, 10)) = 5.0
        
        [Header(Cloud Structure)]
        _CloudScale ("Cloud Scale", Range(0.1, 5)) = 1.5
        _CloudDensity ("Cloud Density", Range(0, 3)) = 2.0
        _DetailScale ("Detail Scale", Range(1, 5)) = 2.5
        _DetailAmount ("Detail Amount", Range(0, 1)) = 0.4
        
        [Header(Movement)]
        _ScrollSpeed ("Scroll Speed", Vector) = (0.01, 0.005, 0.008, 0)
        
        [Header(Raymarching)]
        _Steps ("Steps", Range(16, 128)) = 64
        _StepSize ("Step Size", Range(0.01, 0.3)) = 0.08
        
        [Header(Fade)]
        _EdgeFade ("Edge Fade", Range(0, 1)) = 0.3
        _AlphaMultiplier ("Alpha Multiplier", Range(0, 2)) = 1.5
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
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
            
            CBUFFER_START(UnityPerMaterial)
                float4 _CloudColor1;
                float4 _CloudColor2;
                float _Brightness;
                float _CloudScale;
                float _CloudDensity;
                float _DetailScale;
                float _DetailAmount;
                float4 _ScrollSpeed;
                float _Steps;
                float _StepSize;
                float _EdgeFade;
                float _AlphaMultiplier;
            CBUFFER_END
            
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            
            float noise3D(float3 x)
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                
                return lerp(
                    lerp(
                        lerp(hash(p + float3(0, 0, 0)), hash(p + float3(1, 0, 0)), f.x),
                        lerp(hash(p + float3(0, 1, 0)), hash(p + float3(1, 1, 0)), f.x),
                        f.y),
                    lerp(
                        lerp(hash(p + float3(0, 0, 1)), hash(p + float3(1, 0, 1)), f.x),
                        lerp(hash(p + float3(0, 1, 1)), hash(p + float3(1, 1, 1)), f.x),
                        f.y),
                    f.z);
            }
            
            float fbm(float3 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                
                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * noise3D(p * frequency);
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }
                
                return value;
            }
            
            float cloudDensity(float3 pos)
            {
                float3 scrolledPos = pos + _ScrollSpeed.xyz * _Time.y;
                
                float baseNoise = fbm(scrolledPos * _CloudScale, 4);
                float detailNoise = fbm(scrolledPos * _CloudScale * _DetailScale, 2);
                
                float density = baseNoise;
                density = lerp(density, density * detailNoise, _DetailAmount);
                
                density = saturate((density - (1.0 - _CloudDensity)) * 4.0);
                
                float distanceToCenter = length(pos);
                float centerFade = smoothstep(0.5, 0.5 - _EdgeFade, distanceToCenter);
                density *= centerFade;
                
                return density;
            }
            
            float4 raymarch(float3 rayOrigin, float3 rayDir)
            {
                float3 pos = rayOrigin;
                float3 color = float3(0, 0, 0);
                float alpha = 0.0;
                
                int steps = (int)_Steps;
                float stepSize = _StepSize;
                
                for (int i = 0; i < steps; i++)
                {
                    if (alpha > 0.99) break;
                    
                    float density = cloudDensity(pos);
                    
                    if (density > 0.01)
                    {
                        float noiseValue = fbm(pos * _CloudScale * 0.5, 2);
                        
                        float3 cloudColor = lerp(_CloudColor1.rgb, _CloudColor2.rgb, noiseValue);
                        cloudColor *= _Brightness;
                        
                        float contribution = density * stepSize * (1.0 - alpha);
                        color += cloudColor * contribution;
                        alpha += contribution * _AlphaMultiplier;
                    }
                    
                    pos += rayDir * stepSize;
                    if (length(pos) > 0.5) break;
                }
                
                alpha = saturate(alpha);
                return float4(color, alpha);
            }
            
            bool rayBoxIntersection(float3 rayOrigin, float3 rayDir, out float tNear, out float tFar)
            {
                float3 boxMin = float3(-0.5, -0.5, -0.5);
                float3 boxMax = float3(0.5, 0.5, 0.5);
                
                float3 invDir = 1.0 / (rayDir + 0.0001);
                float3 t0 = (boxMin - rayOrigin) * invDir;
                float3 t1 = (boxMax - rayOrigin) * invDir;
                
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                
                tNear = max(max(tMin.x, tMin.y), tMin.z);
                tFar = min(min(tMax.x, tMax.y), tMax.z);
                
                return tFar > max(tNear, 0.0);
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                float3 cameraWS = GetCameraPositionWS();
                float3 cameraPosLocal = TransformWorldToObject(cameraWS);
                float3 rayDir = normalize(input.positionWS - cameraWS);
                float3 localRayDir = normalize(TransformWorldToObjectDir(rayDir));
                
                float tNear, tFar;
                if (!rayBoxIntersection(cameraPosLocal, localRayDir, tNear, tFar))
                {
                    return float4(0, 0, 0, 0);
                }
                
                float3 startPos = cameraPosLocal + localRayDir * max(tNear, 0.0);
                
                float4 color = raymarch(startPos, localRayDir);
                
                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
