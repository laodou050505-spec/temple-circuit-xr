Shader "TempleCircuit/CameraBackground"
{
    Properties
    {
        _MainTex ("Background", 2D) = "black" {}
        _CropScale ("Crop Scale", Vector) = (1, 1, 0, 0)
        _UvRotation ("UV Rotation", Vector) = (1, 0, 0, 0)
        _Mirror ("Mirror", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DesktopMRBackground"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _CropScale;
            float4 _UvRotation;
            float4 _Mirror;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUv = input.positionCS.xy / _ScreenParams.xy;
                float sceneDepth = SampleSceneDepth(screenUv);
                float sceneEyeDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                if (sceneEyeDepth < _ProjectionParams.z * 0.999)
                    discard;

                float2 uv = (input.uv - 0.5) * _CropScale.xy;
                uv = float2(
                    uv.x * _UvRotation.x - uv.y * _UvRotation.y,
                    uv.x * _UvRotation.y + uv.y * _UvRotation.x);
                uv *= _Mirror.xy;
                uv += 0.5;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            }
            ENDHLSL
        }
    }
}
