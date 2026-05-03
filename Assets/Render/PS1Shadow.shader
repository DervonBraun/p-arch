Shader "PS1/Shadow"
{
    // Кастомный shadow шейдер для PS1-стиля.
    // Переопределяет стандартный ShadowCaster pass HDRP.
    // Результат — жёсткие пиксельные тени без PCF/PCSS размытия.

    Properties
    {
        _MainTex        ("Albedo",          2D)    = "white" {}
        _Color          ("Color",           Color) = (1,1,1,1)
        _Cutoff         ("Alpha Cutoff",    Range(0,1)) = 0.5
        [Toggle] _AlphaClip ("Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        // -------------------------------------------------------
        // ShadowCaster Pass
        // HDRP ищет pass с LightMode = "ShadowCaster" на каждом
        // материале когда рендерит shadow map.
        // Здесь мы форсируем point sampleр и убираем bias tricks.
        // -------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0        // Shadow pass пишет только в depth
            Cull Back

            HLSLPROGRAM

            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile_shadowcaster
            #pragma multi_compile _ _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_point_clamp);   // Point — никакой интерполяции

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Cutoff;
                float  _AlphaClip;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS);

                // Normal bias вручную — сдвигаем вершину вдоль нормали
                // чтобы избежать shadow acne без размытия.
                // 0.01 — подбирается под масштаб сцены.
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalWS * 0.01;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);

                return output;
            }

            float frag(Varyings input) : SV_Depth
            {
                // Alpha clip для прозрачных объектов (листья, решётки)
                #if defined(_ALPHATEST_ON)
                    float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_point_clamp, input.uv).a;
                    clip(alpha - _Cutoff);
                #endif

                // Просто возвращаем глубину — ColorMask 0 выше,
                // так что цвет всё равно не пишется.
                return input.positionCS.z / input.positionCS.w;
            }

            ENDHLSL
        }

        // -------------------------------------------------------
        // DepthOnly Pass
        // Нужен HDRP для prepass и ambient occlusion.
        // Тоже с point sampling чтобы не было размытых краёв в AO.
        // -------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R     // HDRP пишет motion vectors в R канал
            Cull Back

            HLSLPROGRAM

            #pragma vertex   vertDepth
            #pragma fragment fragDepth

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_point_clamp);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Cutoff;
                float  _AlphaClip;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vertDepth(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            void fragDepth(Varyings input)
            {
                #if defined(_ALPHATEST_ON)
                    float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_point_clamp, input.uv).a;
                    clip(alpha - _Cutoff);
                #endif
            }

            ENDHLSL
        }
    }

    Fallback Off
}
