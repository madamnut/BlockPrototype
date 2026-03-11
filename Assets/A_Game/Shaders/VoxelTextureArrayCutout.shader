Shader "YD/Voxel Texture Array Cutout"
{
    Properties
    {
        _BlockTextures("Block Textures", 2DArray) = "" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _WindDirection("Wind Direction", Vector) = (0.85, 0.35, 0, 0)
        _WindStrength("Wind Strength", Range(0, 0.2)) = 0.045
        _WindSpeed("Wind Speed", Range(0, 5)) = 1.1
        _FlutterStrength("Flutter Strength", Range(0, 0.1)) = 0.018
        _FlutterSpeed("Flutter Speed", Range(0, 10)) = 3.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_ARRAY(_BlockTextures);
            SAMPLER(sampler_BlockTextures);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Cutoff;
                half4 _WindDirection;
                half _WindStrength;
                half _WindSpeed;
                half _FlutterStrength;
                half _FlutterSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half4 color : COLOR;
                float fogFactor : TEXCOORD4;
            };

            uint DecodeLayer(half4 encodedColor)
            {
                uint low = (uint)round(saturate(encodedColor.r) * 255.0h);
                uint high = (uint)round(saturate(encodedColor.g) * 255.0h);
                return low | (high << 8);
            }

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float3 ApplyWind(float3 positionOS)
            {
                float3 positionWS = TransformObjectToWorld(positionOS);
                float2 windDir = normalize(_WindDirection.xy + float2(0.0001, 0.0001));
                float rootMask = saturate(positionOS.y / 0.9);
                float bendMask = saturate((rootMask - 0.08) / 0.92);
                bendMask *= bendMask;
                bendMask *= bendMask;

                float phaseSeed = Hash12(positionWS.xz * 0.173);
                float primaryPhase = dot(positionWS.xz, windDir * 0.22) + (_Time.y * _WindSpeed) + (phaseSeed * 6.2831853);
                float flutterPhase = dot(positionWS.xz, float2(-windDir.y, windDir.x) * 0.41) + (_Time.y * _FlutterSpeed) + (phaseSeed * 12.5663706);

                float primary = sin(primaryPhase) * _WindStrength;
                float flutter = sin(flutterPhase) * _FlutterStrength;
                float lateral = primary + flutter;

                positionWS.xz += windDir * (lateral * bendMask);
                return TransformWorldToObject(positionWS);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 swayedPositionOS = ApplyWind(input.positionOS.xyz);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(swayedPositionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.uv = input.uv;
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                uint textureLayer = DecodeLayer(input.color);
                half4 albedoSample = SAMPLE_TEXTURE2D_ARRAY(_BlockTextures, sampler_BlockTextures, input.uv, textureLayer);
                clip(albedoSample.a - _Cutoff);

                half3 albedo = albedoSample.rgb * _BaseColor.rgb;
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 direct = albedo * mainLight.color * (ndotl * mainLight.shadowAttenuation);
                half3 ambient = albedo * SampleSH(normalWS);
                half3 color = MixFog(direct + ambient, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
