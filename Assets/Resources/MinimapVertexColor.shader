Shader "Custom/MinimapVertexColor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.6
        _AmbientColor ("Ambient Color", Color) = (0.3, 0.3, 0.4, 1)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // URP lighting keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _ShadowStrength;
                half4 _AmbientColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                
                // Shadow coordinates
                output.shadowCoord = GetShadowCoord(vertexInput);
                
                // Fog
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample texture and apply vertex color
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                albedo *= input.color;
                
                // Get main light and shadow
                Light mainLight = GetMainLight(input.shadowCoord);
                
                // Calculate lighting
                half3 normalWS = normalize(input.normalWS);
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                
                // Main light contribution with shadows
                half3 mainLightColor = mainLight.color * mainLight.shadowAttenuation;
                half3 diffuseLight = mainLightColor * NdotL;
                
                // Additional lights
                half3 additionalLights = 0;
                #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half3 lightColor = light.color * light.shadowAttenuation;
                    half lightNdotL = saturate(dot(normalWS, light.direction));
                    additionalLights += lightColor * lightNdotL;
                }
                #endif
                
                // Combine all lighting
                half3 totalLight = _AmbientColor.rgb + diffuseLight + additionalLights;
                
                // Apply shadow strength
                half shadowFactor = lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);
                totalLight = _AmbientColor.rgb + (diffuseLight + additionalLights) * shadowFactor;
                
                // Final color
                half3 finalColor = albedo.rgb * totalLight;
                
                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);
                
                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
