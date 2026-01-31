Shader "Unlit/OutlinePostProcess"
{
     Properties
    {
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _Threshold("Depth Threshold", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalRenderPipeline" }
        Pass
        {
            Name "OutlinePass"
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _OutlineColor;
            float _Threshold;
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float DecodeDepth(float2 uv)
            {
                return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float depth = DecodeDepth(IN.uv);
                float outline = 0.0;

                // Sample depth at surrounding pixels
                float2 offset = 1.0 / _ScreenParams.xy;

                float depthL = DecodeDepth(IN.uv + float2(-offset.x, 0));
                float depthR = DecodeDepth(IN.uv + float2(offset.x, 0));
                float depthU = DecodeDepth(IN.uv + float2(0, offset.y));
                float depthD = DecodeDepth(IN.uv + float2(0, -offset.y));

                if (abs(depth - depthL) > _Threshold ||
                    abs(depth - depthR) > _Threshold ||
                    abs(depth - depthU) > _Threshold ||
                    abs(depth - depthD) > _Threshold)
                {
                    outline = 1.0;
                }

                float4 original = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                return lerp(original, _OutlineColor, outline);
            }
            ENDHLSL
        }
    }
}
