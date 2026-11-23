Shader "WaterChaos/WaterToon"
{
    Properties
    {
        [Header(Colors)]
        _BaseColor("Base Color", Color) = (0.36, 0.78, 0.88, 1)
        _ShadeColor("Shade Color", Color) = (0.16, 0.5, 0.7, 1)
        _RimColor("Rim Color", Color) = (0.85, 0.97, 1, 1)
        _Alpha("Alpha", Range(0, 1)) = 0.9

        [Header(Toon Lighting)]
        _ShadeThreshold("Shade Threshold", Range(0, 1)) = 0.5
        _RimPower("Rim Power", Range(0.5, 8)) = 3
        _RimThreshold("Rim Threshold", Range(0, 1)) = 0.5
        [Toggle] _Faceted("Faceted (low-poly look)", Float) = 1

        [Header(Flow Stripes)]
        _StripeColor("Stripe Color", Color) = (0.85, 0.97, 1, 1)
        _StripeStrength("Stripe Strength", Range(0, 1)) = 0.6
        _StripeFrequency("Stripe Frequency", Float) = 2
        _StripeSpeed("Stripe Speed", Float) = 3
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

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadeColor;
                half4 _RimColor;
                half _Alpha;
                half _ShadeThreshold;
                half _RimPower;
                half _RimThreshold;
                half _Faceted;
                half4 _StripeColor;
                half _StripeStrength;
                half _StripeFrequency;
                half _StripeSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));

                // Faceted: the normal of the flat triangle instead of the smooth vertex normal
                float3 faceNormal = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                faceNormal *= sign(dot(faceNormal, viewDir));
                float3 normal = lerp(normalize(input.normalWS), faceNormal, _Faceted);

                // Two-tone lighting from the main light
                Light mainLight = GetMainLight();
                half ndl = dot(normal, mainLight.direction) * 0.5h + 0.5h;
                half lit = step(_ShadeThreshold, ndl);
                half3 color = lerp(_ShadeColor.rgb, _BaseColor.rgb, lit);

                // Foam stripes running away from the nozzle (V = distance along the stream)
                // Streaks run along the stream and slowly drift outward
                float wave = sin(input.uv.x * 12.566 + input.uv.y * _StripeFrequency - _Time.y * _StripeSpeed);
                half stripe = smoothstep(0.75, 0.85, wave * 0.5 + 0.5) * _StripeStrength;
                color = lerp(color, _StripeColor.rgb, stripe);

                // Hard light rim on the silhouette
                half fresnel = pow(1.0h - saturate(dot(normal, viewDir)), _RimPower);
                half rim = smoothstep(_RimThreshold, _RimThreshold + 0.05h, fresnel);
                color = lerp(color, _RimColor.rgb, rim);

                half alpha = saturate(_Alpha + rim);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
