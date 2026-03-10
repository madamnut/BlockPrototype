Shader "YD/Voxel Fluid"
{
    Properties
    {
        _WaterColor("Water Color", Color) = (0.08, 0.34, 0.72, 0.72)
        _ReflectionColor("Reflection Color", Color) = (0.85, 0.95, 1.0, 1.0)
        _SurfaceAlpha("Surface Alpha", Range(0, 1)) = 0.72
        _ExteriorReflectionStrength("Exterior Reflection Strength", Range(0, 4)) = 1.6
        _WaveNormalStrength("Wave Normal Strength", Range(0, 1)) = 0.2

        _WaveADirection("Wave A Direction", Vector) = (0.8, 0.2, 0, 0)
        _WaveASpacing("Wave A Spacing", Float) = 18
        _WaveASpeed("Wave A Speed", Float) = 0.35
        _WaveAScale("Wave A Scale", Float) = 0.06
        _WaveAAmplitude("Wave A Amplitude", Float) = 0.7
        _WaveANoiseScale("Wave A Noise Scale", Float) = 0.18
        _WaveANoiseStrength("Wave A Noise Strength", Float) = 0.45
        _WaveADistortion("Wave A Distortion", Float) = 0.012

        _WaveBDirection("Wave B Direction", Vector) = (-0.35, 0.95, 0, 0)
        _WaveBSpacing("Wave B Spacing", Float) = 7
        _WaveBSpeed("Wave B Speed", Float) = 0.7
        _WaveBScale("Wave B Scale", Float) = 0.14
        _WaveBAmplitude("Wave B Amplitude", Float) = 0.35
        _WaveBNoiseScale("Wave B Noise Scale", Float) = 0.41
        _WaveBNoiseStrength("Wave B Noise Strength", Float) = 0.6
        _WaveBDistortion("Wave B Distortion", Float) = 0.018

        [NoScaleOffset] _PlanarReflectionTex("Planar Reflection", 2D) = "black" {}

        [HideInInspector] _SrcBlend("Src Blend", Float) = 1
        [HideInInspector] _DstBlend("Dst Blend", Float) = 10
        [HideInInspector] _ZWrite("ZWrite", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_PlanarReflectionTex);
            SAMPLER(sampler_PlanarReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor;
                half4 _ReflectionColor;
                half _SurfaceAlpha;
                half _ExteriorReflectionStrength;
                half _WaveNormalStrength;
                half4 _WaveADirection;
                half _WaveASpacing;
                half _WaveASpeed;
                half _WaveAScale;
                half _WaveAAmplitude;
                half _WaveANoiseScale;
                half _WaveANoiseStrength;
                half _WaveADistortion;
                half4 _WaveBDirection;
                half _WaveBSpacing;
                half _WaveBSpeed;
                half _WaveBScale;
                half _WaveBAmplitude;
                half _WaveBNoiseScale;
                half _WaveBNoiseStrength;
                half _WaveBDistortion;
            CBUFFER_END

            float4x4 _PlanarReflectionVP;
            float4 _PlanarReflectionTex_TexelSize;

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
                half3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = Hash12(i);
                float b = Hash12(i + float2(1.0, 0.0));
                float c = Hash12(i + float2(0.0, 1.0));
                float d = Hash12(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - (2.0 * f));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half2 EvaluateWave(
                float2 worldXZ,
                half2 direction,
                half spacing,
                half speed,
                half scale,
                half amplitude,
                half noiseScale,
                half noiseStrength)
            {
                float2 dir = normalize(float2(direction.x, direction.y));
                float spacingFactor = max(spacing, 0.001h);
                float phase = dot(worldXZ, dir) * (6.28318530718 / spacingFactor);
                phase += _Time.y * speed;

                float lateral = dot(worldXZ, float2(-dir.y, dir.x));
                float noise = (Noise2D(float2(phase * max(scale, 0.001h), lateral * noiseScale)) * 2.0) - 1.0;
                phase += noise * noiseStrength;

                float wave = sin(phase);
                float slope = cos(phase) * amplitude;
                return half2(wave * amplitude, slope);
            }

            half2 ComputeSurfaceNormalOffset(float3 positionWS)
            {
                float2 worldXZ = positionWS.xz;

                half2 waveA = EvaluateWave(
                    worldXZ,
                    _WaveADirection.xy,
                    _WaveASpacing,
                    _WaveASpeed,
                    _WaveAScale,
                    _WaveAAmplitude,
                    _WaveANoiseScale,
                    _WaveANoiseStrength);

                half2 waveB = EvaluateWave(
                    worldXZ,
                    _WaveBDirection.xy,
                    _WaveBSpacing,
                    _WaveBSpeed,
                    _WaveBScale,
                    _WaveBAmplitude,
                    _WaveBNoiseScale,
                    _WaveBNoiseStrength);

                half2 dirA = normalize(_WaveADirection.xy);
                half2 dirB = normalize(_WaveBDirection.xy);
                half2 combinedSlope = (dirA * waveA.y) + (dirB * waveB.y);
                return combinedSlope * _WaveNormalStrength;
            }

            half2 ComputeReflectionDistortion(float3 positionWS)
            {
                float2 worldXZ = positionWS.xz;

                half2 waveA = EvaluateWave(
                    worldXZ,
                    _WaveADirection.xy,
                    _WaveASpacing,
                    _WaveASpeed,
                    _WaveAScale,
                    _WaveAAmplitude,
                    _WaveANoiseScale,
                    _WaveANoiseStrength);

                half2 waveB = EvaluateWave(
                    worldXZ,
                    _WaveBDirection.xy,
                    _WaveBSpacing,
                    _WaveBSpeed,
                    _WaveBScale,
                    _WaveBAmplitude,
                    _WaveBNoiseScale,
                    _WaveBNoiseStrength);

                half2 dirA = normalize(_WaveADirection.xy);
                half2 dirB = normalize(_WaveBDirection.xy);
                return (dirA * waveA.x * _WaveADistortion) + (dirB * waveB.x * _WaveBDistortion);
            }

            half4 SamplePlanarReflection(float3 positionWS, half2 distortionOffset)
            {
                float4 reflectionClip = mul(_PlanarReflectionVP, float4(positionWS, 1.0));
                float2 planarUV = reflectionClip.xy / max(reflectionClip.w, 0.0001) * 0.5 + 0.5;
                planarUV.y = 1.0 - planarUV.y;
                planarUV += distortionOffset;

                half isValid =
                    step(0.0, planarUV.x) *
                    step(0.0, planarUV.y) *
                    step(planarUV.x, 1.0) *
                    step(planarUV.y, 1.0) *
                    step(0.0001, reflectionClip.w);

                half3 sampled = SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_PlanarReflectionTex, saturate(planarUV)).rgb;
                return half4(sampled, isValid);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                half2 normalOffset = ComputeSurfaceNormalOffset(input.positionWS);
                half2 distortionOffset = ComputeReflectionDistortion(input.positionWS);
                half3 normalWS = normalize(input.normalWS + half3(normalOffset.x, 0.0h, normalOffset.y));
                half3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 orientedNormalWS = normalWS * (isFrontFace ? 1.0h : -1.0h);
                half viewAlignment = saturate(dot(orientedNormalWS, viewDirWS));
                half fresnel = saturate(1.0h - viewAlignment);
                fresnel *= fresnel;
                half underwaterView = isFrontFace ? 0.0h : 1.0h;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                half2 refractionOffset = distortionOffset * lerp(3.0h, 4.5h, underwaterView);
                half3 refractedScene = SampleSceneColor(saturate(screenUV + refractionOffset));
                half refractedTint = lerp(0.35h, 0.12h, underwaterView);
                half3 refractedColor = lerp(refractedScene, refractedScene * _WaterColor.rgb, refractedTint);

                half4 planarReflectionSample = SamplePlanarReflection(input.positionWS, distortionOffset * lerp(1.0h, 1.35h, underwaterView));
                half exteriorReflectionStrength = max(_ExteriorReflectionStrength, 0.0h);
                half reflectionStrength = lerp(
                    saturate(fresnel * exteriorReflectionStrength),
                    saturate(0.16h + (fresnel * 0.84h)),
                    underwaterView);
                half reflectionMask = saturate(reflectionStrength * planarReflectionSample.a);
                half3 reflectedColor = planarReflectionSample.rgb * _ReflectionColor.rgb;
                half exteriorBaseBlend = saturate(1.0h - (0.08h * exteriorReflectionStrength));
                half baseBlend = lerp(exteriorBaseBlend, 0.985h, underwaterView);
                half3 baseColor = lerp(_WaterColor.rgb, refractedColor, baseBlend);
                half3 color = lerp(baseColor, reflectedColor, reflectionMask);
                color = MixFog(color, input.fogFactor);
                return half4(color, _SurfaceAlpha);
            }
            ENDHLSL
        }
    }
}
