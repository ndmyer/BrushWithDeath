Shader "BrushWithDeath/Tempo/EtherealIndicator"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Aspect ("Aspect", Float) = 1
        _InnerFadeStart ("Inner Fade Start", Range(0, 0.5)) = 0.12
        _InnerFadeEnd ("Inner Fade End", Range(0.1, 1)) = 0.82
        _InnerFadePower ("Inner Fade Power", Range(0.1, 8)) = 2.2
        _EdgeSoftness ("Edge Softness", Range(0.005, 0.2)) = 0.05
        _WaveCount ("Wave Count", Float) = 10
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.2)) = 0.055
        _SecondaryWaveCount ("Secondary Wave Count", Float) = 18
        _SecondaryWaveAmplitude ("Secondary Wave Amplitude", Range(0, 0.15)) = 0.022
        _WaveSpeed ("Wave Speed", Float) = 1.85
        _SecondaryWaveSpeed ("Secondary Wave Speed", Float) = 1.2
        _PulseAmount ("Pulse Amount", Range(0, 0.2)) = 0.02
        _PulseSpeed ("Pulse Speed", Float) = 1.35
        _PhaseOffset ("Phase Offset", Float) = 0
        _RimBrightness ("Rim Brightness", Float) = 1.2
        _BoundsScale ("Bounds Scale", Float) = 1.25
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Opacity;
                float _Aspect;
                float _InnerFadeStart;
                float _InnerFadeEnd;
                float _InnerFadePower;
                float _EdgeSoftness;
                float _WaveCount;
                float _WaveAmplitude;
                float _SecondaryWaveCount;
                float _SecondaryWaveAmplitude;
                float _WaveSpeed;
                float _SecondaryWaveSpeed;
                float _PulseAmount;
                float _PulseSpeed;
                float _PhaseOffset;
                float _RimBrightness;
                float _BoundsScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 spriteSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                float2 centered = (input.uv * 2.0) - 1.0;
                centered.x *= max(_Aspect, 0.0001);

                float radius = length(centered) * max(_BoundsScale, 1.0);
                float angle = atan2(centered.y, centered.x);
                float timeValue = _Time.y + _PhaseOffset;

                // Layer multiple traveling angular waves so the rim motion reads as a drifting aura,
                // not just a uniform pulse.
                float wave = sin((angle * _WaveCount) - (timeValue * _WaveSpeed)) * _WaveAmplitude;
                wave += sin((angle * _SecondaryWaveCount) + (timeValue * _SecondaryWaveSpeed)) * _SecondaryWaveAmplitude;
                wave += sin((angle * ((_WaveCount * 0.55) + (_SecondaryWaveCount * 0.35))) - (timeValue * ((_WaveSpeed * 0.65) + (_SecondaryWaveSpeed * 0.8)))) * (_SecondaryWaveAmplitude * 0.6);
                wave *= 0.85 + (sin((angle * 3.0) + (timeValue * 0.75)) * 0.15);
                float pulse = sin(timeValue * _PulseSpeed) * _PulseAmount;
                float outerRadius = 1.0 + wave + pulse;

                float edgeMask = 1.0 - smoothstep(outerRadius - _EdgeSoftness, outerRadius + _EdgeSoftness, radius);
                float innerFade = saturate((radius - _InnerFadeStart) / max(_InnerFadeEnd - _InnerFadeStart, 0.0001));
                innerFade = pow(innerFade, max(_InnerFadePower, 0.1));

                float normalizedRadius = saturate(radius / max(outerRadius, 0.0001));
                float rimMask = pow(normalizedRadius, 2.5);

                half4 tint = _Tint * input.color;
                half3 rgb = spriteSample.rgb * tint.rgb * lerp(1.0h, (half)_RimBrightness, (half)rimMask);
                half alpha = spriteSample.a * tint.a * (half)_Opacity * (half)edgeMask * (half)innerFade;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
