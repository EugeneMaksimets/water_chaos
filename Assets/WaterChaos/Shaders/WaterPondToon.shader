Shader "WaterChaos/WaterPondToon"
{
    Properties
    {
        [Header(Depth Colors)]
        _ShallowColor("Shallow Color", Color) = (0.45, 0.86, 0.92, 1)
        _DeepColor("Deep Color", Color) = (0.12, 0.42, 0.66, 1)
        _DepthMaxDistance("Depth Max Distance (m)", Float) = 1.5
        _ShallowAlpha("Shallow Alpha", Range(0, 1)) = 0.35
        _DeepAlpha("Deep Alpha", Range(0, 1)) = 0.85

        [Header(Shore Foam)]
        _FoamColor("Foam Color", Color) = (0.95, 1, 1, 1)
        _FoamDistance("Foam Distance (m)", Float) = 0.25
        _FoamNoise("Foam Noise", Range(0, 1)) = 0.4

        [Header(Toon Ripples)]
        _RippleColor("Ripple Color", Color) = (0.8, 0.97, 1, 1)
        _RippleScale("Ripple Scale", Float) = 1.2
        _RippleSpeed("Ripple Speed", Float) = 0.8
        _RippleCutoff("Ripple Cutoff", Range(0, 1)) = 0.72
        _RippleStrength("Ripple Strength", Range(0, 1)) = 0.6

        [Header(Waves)]
        _WaveHeight("Wave Height (m)", Float) = 0.04
        _WaveFrequency("Wave Frequency", Float) = 1.5
        _WaveSpeed("Wave Speed", Float) = 1.2
        _ShadeThreshold("Shade Threshold", Range(0, 1)) = 0.55
        _ShadeAmount("Shade Amount", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthMaxDistance;
                half _ShallowAlpha;
                half _DeepAlpha;
                half4 _FoamColor;
                float _FoamDistance;
                half _FoamNoise;
                half4 _RippleColor;
                float _RippleScale;
                float _RippleSpeed;
                half _RippleCutoff;
                half _RippleStrength;
                float _WaveHeight;
                float _WaveFrequency;
                float _WaveSpeed;
                half _ShadeThreshold;
                half _ShadeAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Distance to the nearest moving cell point (0 = cell center, ~1 = cell border)
            float Voronoi(float2 p, float time)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float minDist = 8.0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 offset = float2(x, y);
                    float2 point = 0.5 + 0.5 * sin(time + 6.2831 * Hash2(cell + offset));
                    float2 r = offset + point - local;
                    minDist = min(minDist, dot(r, r));
                }
                return sqrt(minDist);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                float t = _Time.y * _WaveSpeed;
                positionWS.y += sin(positionWS.x * _WaveFrequency + t)
                              * cos(positionWS.z * _WaveFrequency * 0.8 + t * 1.3) * _WaveHeight;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. How deep is the water under this pixel (in meters along the view ray)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterDepth = sceneDepth - input.screenPos.w;

                float depth01 = saturate(waterDepth / _DepthMaxDistance);
                half3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);
                half alpha = lerp(_ShallowAlpha, _DeepAlpha, depth01);

                // 2. Faceted two-tone lighting (low-poly look on the waves)
                float3 faceNormal = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                faceNormal *= sign(dot(faceNormal, viewDir));
                half ndl = dot(faceNormal, GetMainLight().direction) * 0.5h + 0.5h;
                color *= lerp(1.0h - _ShadeAmount, 1.0h, step(_ShadeThreshold, ndl));

                // 3. Toon ripples: bright blobs on the borders of moving cells
                float cells = Voronoi(input.positionWS.xz * _RippleScale, _Time.y * _RippleSpeed);
                half ripple = step(_RippleCutoff, cells) * _RippleStrength;
                color = lerp(color, _RippleColor.rgb, ripple);
                alpha = saturate(alpha + ripple * 0.3h);

                // 4. Shore foam: hard-edged band where water meets geometry
                float foamEdge = 1.0 - _FoamNoise * cells;
                half foam = step(waterDepth / _FoamDistance, foamEdge);
                color = lerp(color, _FoamColor.rgb, foam);
                alpha = lerp(alpha, 1.0h, foam);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}