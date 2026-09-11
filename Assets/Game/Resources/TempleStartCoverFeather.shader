Shader "TempleCircuit/StartCoverFeather"
{
    Properties
    {
        [PerRendererData] _MainTex ("Cover", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Feather ("Edge Feather", Range(0.01, 0.25)) = 0.105
        _Aspect ("Cover Aspect", Float) = 1.7777778
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "StartCoverFeather"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _Color;
            float _Feather;
            float _Aspect;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 cover = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                // Measure from all four edges in short-axis units so the fade
                // has an even physical width on this 16:9 world-space panel.
                float horizontalDistance = min(input.uv.x, 1.0 - input.uv.x) * max(1.0, _Aspect);
                float verticalDistance = min(input.uv.y, 1.0 - input.uv.y);
                float edgeDistance = min(horizontalDistance, verticalDistance);
                float featherAlpha = smoothstep(0.0, max(0.001, _Feather), edgeDistance);

                cover.a *= featherAlpha;
                return cover;
            }
            ENDHLSL
        }
    }
}
