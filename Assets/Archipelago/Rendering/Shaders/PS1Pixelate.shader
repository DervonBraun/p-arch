Shader "Hidden/Archipelago/PS1Pixelate"
{
    // FullScreen shader для PS1PixelatePass.
    // Pass 0: downscale + quantize + dither (пишем в tempRT)
    // Pass 1: upscale обратно в color buffer (point sampling для пиксельного вида)
    //
    // HDRP 17.4: используется через CoreUtils.DrawFullScreen.
    // НЕ использовать с Graphics.Blit.

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    TEXTURE2D_X(_InputTexture);
    SAMPLER(sampler_LinearClamp);
    SAMPLER(sampler_PointClamp);

    float _ColorLevels;
    float _QuantStrength;
    float _DitheringStrength;
    float _BayerSize;
    float _Downscale;
    float _LumThreshold;

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    // ── Bayer Matrix 4x4 ─────────────────────────────────────────
    // Ordered dithering. Значения нормализованы в [0, 1].
    float BayerMatrix4x4(int2 pos)
    {
        const float bayer[16] = {
             0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
            12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
             3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
            15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0,
        };
        int idx = (pos.y % 4) * 4 + (pos.x % 4);
        return bayer[idx];
    }

    // ── Color Quantization ────────────────────────────────────────
    // Снижает количество цветовых уровней на канал.
    float3 Quantize(float3 color, float levels, float strength)
    {
        float3 quantized = floor(color * levels + 0.5) / levels;
        return lerp(color, quantized, strength);
    }

    // ── Luminance ─────────────────────────────────────────────────
    float Luminance(float3 color)
    {
        return dot(color, float3(0.2126, 0.7152, 0.0722));
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        // Pass 0: Downscale + Quantize + Dither
        Pass
        {
            Name "PS1_Quantize"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragQuantize

            float4 FragQuantize(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Pixelated sampling — снаппинг UV к пиксельной сетке
                float2 resolution = _ScreenSize.xy / _Downscale;
                float2 snappedUV  = floor(input.texcoord * resolution) / resolution;

                float3 color = SAMPLE_TEXTURE2D_X(_InputTexture,
                    sampler_PointClamp, snappedUV).rgb;

                // Bayer dithering перед квантизацией
                int2   pixelPos  = int2(input.positionCS.xy);
                float  bayerVal  = BayerMatrix4x4(pixelPos % int(_BayerSize));
                float  lum       = Luminance(color);

                // Дизеринг применяется сильнее в тёмных областях
                float  darkFactor = 1.0 - saturate(lum / max(_LumThreshold, 0.001));
                float3 dithered   = color + (bayerVal - 0.5) * _DitheringStrength * darkFactor * 0.1;

                // Color quantization
                float3 result = Quantize(dithered, _ColorLevels, _QuantStrength);

                return float4(result, 1.0);
            }
            ENDHLSL
        }

        // Pass 1: Upscale обратно (point sampling для пиксельного вида)
        Pass
        {
            Name "PS1_Upscale"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpscale

            float4 FragUpscale(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Point sampling — сохраняем пиксельный вид при upscale
                float3 color = SAMPLE_TEXTURE2D_X(_InputTexture,
                    sampler_PointClamp, input.texcoord).rgb;

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
