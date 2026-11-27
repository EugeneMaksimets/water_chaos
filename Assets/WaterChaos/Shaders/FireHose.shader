// Fire hose drawn on a round tube mesh (HoseTubeMesh).
// U = distance along the hose in meters, from the water source to the nozzle. V = 0..1 around the hose.
// Adds a woven jacket and fakes water inside: the hose swells (vertices move out along their normals),
// and pulses and highlights travel toward the nozzle.
// Water values come from HoseView through a MaterialPropertyBlock (_Hose* properties).
Shader "WaterChaos/FireHose"
{
    Properties
    {
        [Header(Jacket Colors)]
        _BaseColor("Base Color", Color) = (1, 0.8, 0.12, 1)
        _ShadeColor("Shade Color", Color) = (0.86, 0.46, 0.05, 1)
        _DirtColor("Dirt Color", Color) = (0.62, 0.42, 0.12, 1)
        _DirtAmount("Dirt Amount", Range(0, 1)) = 0.25

        [Header(Woven Jacket)]
        _WeaveDensity("Weave Threads Per Diameter", Range(2, 30)) = 12
        _WeaveStrength("Weave Strength", Range(0, 1)) = 0.35
        _RibStrength("Lengthwise Rib Strength", Range(0, 1)) = 0.2

        [Header(Surface)]
        _Gloss("Gloss", Range(0, 1)) = 0.18
        _SpecularPower("Highlight Tightness", Range(2, 64)) = 14

        [Header(Water Inside)]
        _WaterTint("Water Tint", Color) = (0.55, 0.85, 1, 1)
        _WaterTintStrength("Water Tint Strength", Range(0, 1)) = 0.18
        _SwellAmount("Swell When Charged", Range(0, 0.1)) = 0.06
        _PulseAmount("Pulse Bulge", Range(0, 0.1)) = 0.08
        _PulseSpacing("Pulse Spacing (m)", Range(0.25, 4)) = 1.25
        _WetGloss("Extra Gloss When Charged", Range(0, 1)) = 0.3

        // Set per renderer by HoseView.
        [HideInInspector] _HoseWidth("Hose Width", Float) = 0.05
        [HideInInspector] _HoseFlowOffset("Hose Flow Offset", Float) = 0
        [HideInInspector] _HoseFullness("Hose Fullness", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half4 _ShadeColor;
        half4 _DirtColor;
        half _DirtAmount;
        half _WeaveDensity;
        half _WeaveStrength;
        half _RibStrength;
        half _Gloss;
        half _SpecularPower;
        half4 _WaterTint;
        half _WaterTintStrength;
        half _SwellAmount;
        half _PulseAmount;
        half _PulseSpacing;
        half _WetGloss;
        float _HoseWidth;
        float _HoseFlowOffset;
        float _HoseFullness;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float2 uv : TEXCOORD0;
    };

    // Water coordinate: distance along the hose minus how far the water has travelled, in meters.
    float WaterCoord(float u)
    {
        return u - _HoseFlowOffset;
    }

    // Travelling pulses of water (0..1). Two waves so the rhythm is not perfectly even.
    float WaterPulse(float u)
    {
        float w = WaterCoord(u) / max(_PulseSpacing, 0.01);
        float main = pow(saturate(sin(w * TWO_PI) * 0.5 + 0.5), 4.0);
        float detail = sin(w * TWO_PI * 2.7 + 1.3) * 0.5 + 0.5;
        return saturate(main * 0.8 + detail * 0.2);
    }

    // Hose radius at this point, relative to the base radius (1 = empty hose).
    float HoseRadius(float u)
    {
        return 1.0 + _HoseFullness * (_SwellAmount + WaterPulse(u) * _PulseAmount);
    }

    // World position of a hose vertex, moved out along its normal as water swells the hose.
    float3 SwollenPositionWS(Attributes input, float3 normalWS)
    {
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
        return positionWS + normalWS * (_HoseWidth * 0.5 * (HoseRadius(input.uv.x) - 1.0));
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = SwollenPositionWS(input, normalWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Woven jacket (0..1, 1 = thread top). Fades out when threads get smaller than a pixel.
            float Weave(float alongDiameters, float aroundDiameters)
            {
                float2 g = float2(alongDiameters, aroundDiameters) * _WeaveDensity;
                float2 f = frac(g);
                float2 id = floor(g);
                float over = fmod(abs(id.x + id.y), 2.0);
                float thread = lerp(sin(f.y * PI), sin(f.x * PI), over);

                float blur = max(fwidth(g.x), fwidth(g.y));
                float fade = saturate(1.5 - blur * 1.5);
                return lerp(0.5, thread, fade);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 normal = normalize(input.normalWS);
                float nz = saturate(dot(normal, viewDir));

                // Jacket pattern in hose diameters, so it keeps its shape when the width changes.
                // Around the hose: a whole number of threads, so the pattern meets itself at the seam.
                float width = max(_HoseWidth, 0.001);
                float along = u / width;
                float threadsAround = max(round(PI * _WeaveDensity), 1.0);
                float around = v * threadsAround / max(_WeaveDensity, 0.01);
                float aroundAngle = v * TWO_PI;
                float weave = Weave(along, around);
                float rib = sin(around * _WeaveDensity * 2.0 * TWO_PI) * 0.5 + 0.5;

                // A charged hose is stretched tight: the weave flattens a little.
                float fullness = _HoseFullness;
                float weaveStrength = _WeaveStrength * (1.0 - fullness * 0.4);
                half3 albedo = _BaseColor.rgb;
                albedo *= lerp(1.0, 0.75 + weave * 0.35, weaveStrength);
                albedo *= lerp(1.0, 0.9 + rib * 0.1, _RibStrength);

                // Wear and dirt along the hose (cos keeps the noise seamless around it).
                float dirt = ValueNoise(float2(u * 1.7, cos(aroundAngle) * 1.5)) * ValueNoise(float2(u * 0.35, 7.0));
                dirt = saturate(dirt * 1.6 - 0.2) * _DirtAmount;
                albedo = lerp(albedo, _DirtColor.rgb, dirt);

                // Lighting: soft diffuse with a warm orange shade instead of grey.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light light = GetMainLight(shadowCoord);
                float ndl = saturate(dot(normal, light.direction));
                float lit = ndl * light.shadowAttenuation;
                half3 shaded = lerp(_ShadeColor.rgb * albedo / max(_BaseColor.rgb, 0.01), albedo, saturate(lit * 1.2));
                half3 color = shaded * (light.color * (0.35 + 0.65 * lit)) + albedo * SampleSH(normal) * 0.6;

                // Water inside: pulses travel from the source to the nozzle.
                float pulse = WaterPulse(u) * fullness;
                float streak = ValueNoise(float2(WaterCoord(u) * 3.0, cos(aroundAngle) * 3.0)) * fullness;

                // Soft fabric highlight; wetter and moving with the water when charged.
                float3 halfDir = normalize(light.direction + viewDir);
                float spec = pow(saturate(dot(normal, halfDir)), _SpecularPower);
                float gloss = _Gloss + _WetGloss * fullness * (0.4 + 0.6 * pulse);
                color += light.color * spec * gloss * light.shadowAttenuation;

                // Cool light seeping through the tight jacket along the centre of the hose.
                float centre = nz * nz;
                color = lerp(color, color * _WaterTint.rgb * 1.25, _WaterTintStrength * centre * (0.35 * fullness + 0.65 * streak));
                color *= 1.0 + pulse * 0.12 * centre;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = SwollenPositionWS(input, normalWS);
                positionWS = ApplyShadowBias(positionWS, normalWS, _LightDirection);
                float4 positionCS = TransformWorldToHClip(positionWS);
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(SwollenPositionWS(input, normalWS));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Normals for screen-space effects (SSAO).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(SwollenPositionWS(input, normalWS));
                output.normalWS = normalWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}
