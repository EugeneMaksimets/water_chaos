// Big soap bubble shell around a character (SoapBubbleView), on a unit sphere.
// Almost clear in the middle so the character inside stays fully visible; the rim, thin-film rainbow,
// sun highlight and a fake sky reflection make it read as a bubble from any angle.
// Drawn on both sides (back faces fainter) with no depth writes, so it never hides what is inside.
Shader "WaterChaos/SoapBubble"
{
    Properties
    {
        [Header(Film)]
        _Tint("Tint", Color) = (0.85, 0.95, 1, 1)
        _CenterAlpha("Center Opacity", Range(0, 0.5)) = 0.04
        _RimAlpha("Rim Opacity", Range(0, 1)) = 0.6
        _RimPower("Rim Width", Range(0.5, 8)) = 2.5

        [Header(Iridescence)]
        _IridescenceStrength("Rainbow Strength", Range(0, 1)) = 0.7
        _IridescenceScale("Rainbow Bands", Range(0.2, 6)) = 2.2
        _SwirlSpeed("Swirl Speed", Range(0, 2)) = 0.25

        [Header(Highlights)]
        _HighlightColor("Highlight Color", Color) = (1, 1, 1, 1)
        _HighlightPower("Sun Highlight Tightness", Range(8, 512)) = 160
        _HighlightStrength("Sun Highlight Strength", Range(0, 4)) = 1.6
        _SkyColor("Reflected Sky", Color) = (0.75, 0.9, 1, 1)
        _GroundColor("Reflected Ground", Color) = (0.35, 0.3, 0.25, 1)
        _ReflectionStrength("Reflection Strength", Range(0, 1)) = 0.35

        [Header(Back Side)]
        _BackFade("Back Side Opacity", Range(0, 1)) = 0.45
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _Tint;
        half _CenterAlpha;
        half _RimAlpha;
        half _RimPower;
        half _IridescenceStrength;
        half _IridescenceScale;
        half _SwirlSpeed;
        half4 _HighlightColor;
        half _HighlightPower;
        half _HighlightStrength;
        half4 _SkyColor;
        half4 _GroundColor;
        half _ReflectionStrength;
        half _BackFade;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
        float3 normalWS : TEXCOORD1;
        float3 normalOS : TEXCOORD2;
        half fogFactor : TEXCOORD3;
    };

    Varyings vert(Attributes input)
    {
        Varyings output;
        VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
        output.positionCS = position.positionCS;
        output.positionWS = position.positionWS;
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        output.normalOS = input.normalOS;
        output.fogFactor = ComputeFogFactor(position.positionCS.z);
        return output;
    }

    // Thin-film colors: a smooth rainbow from a single phase value.
    half3 Rainbow(half phase)
    {
        return 0.5h + 0.5h * cos(6.28318h * (phase + half3(0.0h, 0.33h, 0.67h)));
    }

    half4 Shade(Varyings input, half side)
    {
        float3 normal = normalize(input.normalWS) * side;
        float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
        half facing = saturate(dot(normal, view));
        half rim = pow(1.0h - facing, _RimPower);

        // The film thickness swirls slowly over the surface, so the colors flow like on a real bubble.
        half swirl = sin(input.normalOS.x * 3.1h + _Time.y * _SwirlSpeed * 2.0h)
                   + sin(input.normalOS.y * 4.3h - _Time.y * _SwirlSpeed * 1.3h)
                   + sin(input.normalOS.z * 2.7h + _Time.y * _SwirlSpeed);
        half3 film = Rainbow(facing * _IridescenceScale + swirl * 0.15h);

        float3 reflected = reflect(-view, normal);
        half3 sky = lerp(_GroundColor.rgb, _SkyColor.rgb, saturate(reflected.y * 0.5h + 0.5h));

        Light sun = GetMainLight();
        half spec = pow(saturate(dot(reflected, sun.direction)), _HighlightPower) * _HighlightStrength;
        // A second, softer window-like highlight keeps the bubble readable when the sun is behind the camera.
        half soft = pow(saturate(dot(reflected, normalize(float3(-0.4, 0.8, -0.45)))), 24.0h) * 0.35h;

        half3 color = _Tint.rgb * lerp(1.0h, film, _IridescenceStrength * (0.35h + 0.65h * rim));
        color += sky * _ReflectionStrength * rim;
        color += (_HighlightColor.rgb * sun.color) * spec + soft;

        half alpha = lerp(_CenterAlpha, _RimAlpha, rim) + saturate(spec + soft);
        alpha *= side > 0 ? 1.0h : _BackFade;
        color = MixFog(color, input.fogFactor);
        return half4(color, saturate(alpha));
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }

        // Inside of the far wall first, then the near wall over it.
        Pass
        {
            Name "BubbleBack"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragBack
            #pragma multi_compile_fog
            half4 fragBack(Varyings input) : SV_Target { return Shade(input, -1.0h); }
            ENDHLSL
        }

        Pass
        {
            Name "BubbleFront"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragFront
            #pragma multi_compile_fog
            half4 fragFront(Varyings input) : SV_Target { return Shade(input, 1.0h); }
            ENDHLSL
        }
    }
}
