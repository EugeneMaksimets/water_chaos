// Small soap bubbles on camera-facing particle quads (stream, impact foam, burst droplets).
// The bubble is drawn from the quad UVs: a thin rainbow ring, a faint clear middle and a white glint,
// so no texture is needed. Particle color (vertex color) tints it and fades it out.
Shader "WaterChaos/SoapBubbleParticle"
{
    Properties
    {
        _Tint("Tint", Color) = (0.9, 0.97, 1, 1)
        _RingWidth("Ring Width", Range(0.02, 0.5)) = 0.16
        _CenterAlpha("Center Opacity", Range(0, 0.5)) = 0.08
        _IridescenceStrength("Rainbow Strength", Range(0, 1)) = 0.6
        _GlintStrength("Glint Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _RingWidth;
                half _CenterAlpha;
                half _IridescenceStrength;
                half _GlintStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                half r = length(p);
                clip(1.0h - r);

                half ring = smoothstep(1.0h - _RingWidth, 1.0h - _RingWidth * 0.3h, r) * (1.0h - smoothstep(0.92h, 1.0h, r));
                half angle = atan2(p.y, p.x);
                half3 film = 0.5h + 0.5h * cos(6.28318h * (angle * 0.16h + r * 0.6h + half3(0.0h, 0.33h, 0.67h)));
                half glint = (1.0h - smoothstep(0.0h, 0.22h, length(p - float2(-0.35, 0.38)))) * _GlintStrength;

                half3 color = _Tint.rgb * lerp(1.0h, film, _IridescenceStrength) + glint;
                half alpha = saturate(_CenterAlpha + ring * 0.85h + glint) * input.color.a * _Tint.a;
                color *= input.color.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
