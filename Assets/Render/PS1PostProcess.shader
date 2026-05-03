Shader "Hidden/PS1PostProcess"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    TEXTURE2D_X(_InputTexture);
    SAMPLER(sampler_PointClamp);

    float  _ColorLevels;
    float  _DitherStrength;
    float4 _LowResolution;  // xy = lowres размер, zw = 1/размер

    // ── Матрица Байера 4x4 (встроена прямо в шейдер, не нужна текстура) ──────
    float BayerMatrix(float2 pos)
    {
        int2 p = int2(fmod(pos, 4.0));
        // 4x4 Bayer matrix
        int bayer[16] = {
             0,  8,  2, 10,
            12,  4, 14,  6,
             3, 11,  1,  9,
            15,  7, 13,  5
        };
        return bayer[p.y * 4 + p.x] / 16.0;
    }

    // ── Квантизация + дизеринг ────────────────────────────────────────────────
    float3 Quantize(float3 color, float2 screenPixel)
    {
        float bayer  = BayerMatrix(screenPixel);
        float offset = (bayer - 0.5) * _DitherStrength / _ColorLevels;
        float3 c = saturate(color + offset);
        return floor(c * _ColorLevels + 0.5) / _ColorLevels;
    }

    struct Attributes { uint vertexID : SV_VertexID; };
    struct Varyings   { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

    Varyings Vert(Attributes IN)
    {
        Varyings OUT;
        OUT.posCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
        OUT.uv    = GetFullScreenTriangleTexCoord(IN.vertexID);
        return OUT;
    }

    // ── Pass 0: Point-sample + квантизация + дизеринг ─────────────────────────
    float4 FragPS1(Varyings IN) : SV_Target
    {
        // Снэпаем UV к сетке низкого разрешения — это и есть "пикселизация"
        float2 lowResUV = floor(IN.uv * _LowResolution.xy) * _LowResolution.zw;

        float4 col = LOAD_TEXTURE2D_X(_InputTexture,
            lowResUV * _ScreenSize.xy);

        // Пиксель в координатах низкого разрешения (для матрицы Байера)
        float2 pixelPos = floor(IN.uv * _LowResolution.xy);

        col.rgb = Quantize(col.rgb, pixelPos);
        return col;
    }

    ENDHLSL

    SubShader
    {
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "PS1_Effect"
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment FragPS1
            #pragma target   4.5
            #pragma only_renderers d3d11 vulkan metal
            ENDHLSL
        }
    }
    Fallback Off
}
