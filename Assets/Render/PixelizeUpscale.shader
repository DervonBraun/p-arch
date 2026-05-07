Shader "PS1/PixelizeUpscale"
{
    Properties
    {
        _MainTex      ("Camera Color (full-res)", 2D) = "white" {}

        _BlockLight   ("Block Size (light)",  Range(1, 64)) = 4
        _BlockDark    ("Block Size (dark)",   Range(1, 64)) = 12
        _LumThreshold ("Lum Threshold", Range(0, 1)) = 0.35

        _DitherScale  ("Dither Scale (px)",   Range(1, 8)) = 2
        _DitherStrength ("Dither Strength",   Range(0, 1)) = 0.85
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.45
        _ShadowSoftness  ("Shadow Softness",  Range(0.001, 1)) = 0.3
        _ColorLevels  ("Color Levels (per channel)", Range(2, 256)) = 32

        _ScreenSizeOverride ("Screen Size", Vector) = (1920, 1080, 0, 0)
        _RTHandleScaleOverride ("RTHandle Scale", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "PixelizeUpscale"
            ZWrite Off
            ZTest  Always
            Cull   Off
            Blend  Off

            HLSLPROGRAM

            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone vulkan metal switch

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_linear_clamp);
            SAMPLER(sampler_point_clamp);

            float  _BlockLight;
            float  _BlockDark;
            float  _LumThreshold;

            float  _DitherScale;
            float  _DitherStrength;
            float  _ShadowThreshold;
            float  _ShadowSoftness;
            float  _ColorLevels;

            float4 _ScreenSizeOverride;
            float4 _RTHandleScaleOverride;

            static const float Bayer4x4[16] =
            {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

            float GetBayer4x4(int2 pixelCoord)
            {
                int2 c = pixelCoord & 3;
                return Bayer4x4[c.y * 4 + c.x];
            }

            float Luminance709(float3 c)
            {
                return dot(c, float3(0.2126, 0.7152, 0.0722));
            }

            float2 SnapUVToBlock(float2 uv, float blockSize, float2 screenSize)
            {
                float2 pixelCoord = uv * screenSize;
                float2 snapped    = floor(pixelCoord / blockSize) * blockSize + (blockSize * 0.5);
                return snapped / screenSize;
            }

            struct Attributes { uint vertexID : SV_VertexID; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv         = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 screenSize    = _ScreenSizeOverride.xy;
                float2 rtHandleScale = _RTHandleScaleOverride.xy;
                float2 vpUV = input.uv;

                // 1. Считаем мелкую сетку (для светлых зон)
                float2 snappedLightVP = SnapUVToBlock(vpUV, _BlockLight, screenSize);
                float2 snappedLightRT = snappedLightVP * rtHandleScale;
                float4 colLight = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_point_clamp, snappedLightRT, 0);

                // Оцениваем яркость именно по мелкой сетке. Это убьет всё мерцание!
                float lumLin = Luminance709(colLight.rgb);
                float lum    = lumLin / (1.0 + lumLin);

                // 2. Считаем крупную сетку (для тёмных зон)
                float2 snappedDarkVP  = SnapUVToBlock(vpUV, _BlockDark, screenSize);
                float2 snappedDarkRT  = snappedDarkVP * rtHandleScale;
                float4 colDark  = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_point_clamp, snappedDarkRT, 0);

                // 3. ФИКС: Жесткий выбор ЦВЕТА, а не размера блока. 
                // step вернет 1, если яркость выше порога (светло), и 0 если ниже (темно).
                // Никаких разрывов UV-сеток, просто переключаем два идеальных слоя.
                float t = step(_LumThreshold, lum);
                float4 col = lerp(colDark, colLight, t);

                // 4. Dither и квантование
                int2 ditherCoord = int2(floor(vpUV * screenSize / _DitherScale));
                float bayer      = GetBayer4x4(ditherCoord);

                float shadowMask = 1.0 - smoothstep(_ShadowThreshold, _ShadowThreshold + _ShadowSoftness, lum);
                float ditherK      = _DitherStrength * shadowMask;
                float ditherOffset = (bayer - 0.5) * ditherK;

                float levels = max(_ColorLevels - 1.0, 1.0);
                float3 q = col.rgb;
                q = q + ditherOffset / levels;
                q = floor(saturate(q) * levels + 0.5) / levels;

                return float4(q, col.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}