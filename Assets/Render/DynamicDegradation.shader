Shader "FullScreen/DynamicDegradation"
{
    Properties
    {
        _PixelSize ("Pixel Size", Range(1, 128)) = 16
        _AberrationStrength ("Aberration Strength", Range(0.0, 0.5)) = 0.05
        _GhostingIntensity ("Ghosting Intensity", Range(0.0, 1.0)) = 0.0
    }

    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"

    // Exposed parameters in CBUFFER as requested
    CBUFFER_START(UnityPerMaterial)
        float _PixelSize;
        float _AberrationStrength;
        float _GhostingIntensity;
        float4 _RTHandleScaleOverride;
    CBUFFER_END

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_linear_clamp);
    TEXTURE2D_X(_LastFrameColorPyramid);
            
    // Переопределяем SampleCameraColor для надежного чтения из нашей копии (без _X макросов, которые могут ломаться на Metal с обычными RT)
    #define SampleCameraColor(uv) SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_linear_clamp, (uv) * _RTHandleScaleOverride.xy, 0)

    // Helper for luminance
    float GetLuminance(float3 color)
    {
        return dot(color, float3(0.2126, 0.7152, 0.0722));
    }

    float4 CustomPostProcess(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

        // Инициализация структуры FragInputs, чтобы соответствовать требованиям "Используй FragInputs.hlsl"
        FragInputs fragInputs;
        ZERO_INITIALIZE(FragInputs, fragInputs);
        fragInputs.positionSS = varyings.positionCS;
        fragInputs.positionRWS = posInput.positionWS;

        // Для надежных UV в полноэкранном пассе используем positionNDC
        float2 uv = posInput.positionNDC;

        // --- Mask Logic ---
        // 1. Логика маски: Рассчитываем яркость (Luminance).
        // Используем SampleCameraColor() (нашу надежную обертку) 
        // так как в некоторых Injection Point (например, Before Post Process) 
        // Color Pyramid может быть еще не собрана, что дает сплошной серый экран.
        float3 baseColor = SampleCameraColor(uv).rgb;
        float luminance = GetLuminance(baseColor);
        
        // Inverted mask: darker areas = stronger effects (values closer to 1.0)
        float mask = saturate(1.0 - luminance);

        // --- Effect 1: Advanced Pixelation ---
        // Increase UV step size in dark areas
        float2 pixelatedUV = uv;
        if (mask > 0.0)
        {
            // Blend from 1.0 (no pixelation) to _PixelSize
            float currentBlockSize = lerp(1.0, max(1.0, _PixelSize), mask);
            
            float2 screenPixels = uv * _ScreenSize.xy;
            float2 snappedPixels = floor(screenPixels / currentBlockSize) * currentBlockSize + (currentBlockSize * 0.5);
            
            pixelatedUV = snappedPixels * _ScreenSize.zw;
        }

        // --- Effect 2: Multi-Tap Chromatic Aberration ---
        // Spectral separation stretching from the center
        float2 center = float2(0.5, 0.5);
        float2 dir = pixelatedUV - center;
        
        float currentAberration = _AberrationStrength * mask;
        
        float3 resultColor = float3(0.0, 0.0, 0.0);
        float totalWeight = 0.0;
        
        // Minimum 5 taps for smooth color blur
        const int TAPS = 5;
        for (int i = 0; i < TAPS; i++)
        {
            // Normalize t between 0.0 and 1.0
            float t = (float)i / (float)(TAPS - 1);
            
            // "Stretch" RGB channels from the center based on the mask
            // Red channel stretches the most, Blue the least
            float2 uvR = pixelatedUV + dir * currentAberration * t;
            float2 uvG = pixelatedUV + dir * currentAberration * (t * 0.5);
            float2 uvB = pixelatedUV + dir * currentAberration * (t * 0.1);
            
            // Using SampleCameraColor as requested
            resultColor.r += SampleCameraColor(uvR).r;
            resultColor.g += SampleCameraColor(uvG).g;
            resultColor.b += SampleCameraColor(uvB).b;
            
            totalWeight += 1.0;
        }
        resultColor /= totalWeight;

        // --- Effect 3: Temporal Ghosting ---
        // Mix current frame with the previous frame using _LastFrameColorPyramid
        float3 lastFrameColor = SAMPLE_TEXTURE2D_X_LOD(_LastFrameColorPyramid, s_linear_clamp_sampler, pixelatedUV, 0).rgb;
        
        // High mixing coefficient in dark (pixelated) areas
        float currentGhosting = _GhostingIntensity * mask;
        resultColor = lerp(resultColor, lastFrameColor, currentGhosting);

        // HDR safety - ensure we don't return NaNs or negative colors
        resultColor = max(0.0, resultColor);

        return float4(resultColor, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "DynamicDegradation"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment CustomPostProcess
            ENDHLSL
        }
    }
    Fallback Off
}
