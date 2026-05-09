Shader "PS1/PixelizeUpscale"
{
    Properties
    {
        _MainTex      ("Camera Color (full-res)", 2D) = "white" {}

        _BlockLight   ("Block Size (light)",  Range(1, 64)) = 4
        _BlockDark    ("Block Size (dark)",   Range(1, 64)) = 12
        _LumThreshold ("Lum Threshold",       Range(0, 1)) = 0.35
        _LumSoftness  ("Lum Softness",        Range(0.001, 1)) = 0.25

        _ColorLevels  ("Color Levels",        Range(2, 256)) = 32

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
            float  _LumSoftness;

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
                // % через побитовое И работает только для положительных int.
                // floor() может дать отрицательный результат у краёв — на всякий случай abs.
                int2 c = abs(pixelCoord) & 3;
                return Bayer4x4[c.y * 4 + c.x];
            }

            float Luminance709(float3 c)
            {
                return dot(c, float3(0.2126, 0.7152, 0.0722));
            }

            // Привязка UV к центру блока заданного размера
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
                float2 invScreenSize = _ScreenSizeOverride.zw;
                float2 rtHandleScale = _RTHandleScaleOverride.xy;
                float2 vpUV = input.uv;

                // ----------------------------------------------------------
                // 1. Считаем ОПОРНУЮ яркость для решения "крупный/мелкий блок"
                //
                //    КЛЮЧЕВОЕ ИЗМЕНЕНИЕ:
                //    Раньше яркость бралась из уже мелко-семплированного цвета (colLight),
                //    из-за чего любая тёмная деталь (контур, тень) внутри светлой области
                //    локально опускала яркость и переключала шейдер на крупный блок —
                //    отсюда "много артефактов в светлых областях".
                //
                //    Решение: считать яркость по УСРЕДНЁННОМУ цвету в окне размером
                //    с крупный блок (_BlockDark). Линейная фильтрация даёт нам этот
                //    усреднённый сэмпл бесплатно, если просто взять центр блока.
                //    Плюс — снимаем 4 сэмпла с бихинейарной фильтрацией внутри блока,
                //    чтобы ещё стабильнее усреднить.
                // ----------------------------------------------------------
                float2 darkBlockCenterVP = SnapUVToBlock(vpUV, _BlockDark, screenSize);
                float2 lumOffset         = (_BlockDark * 0.25) * invScreenSize;

                float2 s0 = (darkBlockCenterVP + float2(-lumOffset.x, -lumOffset.y)) * rtHandleScale;
                float2 s1 = (darkBlockCenterVP + float2( lumOffset.x, -lumOffset.y)) * rtHandleScale;
                float2 s2 = (darkBlockCenterVP + float2(-lumOffset.x,  lumOffset.y)) * rtHandleScale;
                float2 s3 = (darkBlockCenterVP + float2( lumOffset.x,  lumOffset.y)) * rtHandleScale;

                float3 avg = 0.25 * (
                    SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_linear_clamp, s0, 0).rgb +
                    SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_linear_clamp, s1, 0).rgb +
                    SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_linear_clamp, s2, 0).rgb +
                    SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_linear_clamp, s3, 0).rgb
                );

                // Тон-маппинг яркости в [0..1] для устойчивости к HDR
                float lumLin = Luminance709(avg);
                float lum    = lumLin / (1.0 + lumLin);

                // ----------------------------------------------------------
                // 2. Семплируем мелкий и крупный варианты пикселизации
                // ----------------------------------------------------------
                float2 snappedLightVP = SnapUVToBlock(vpUV, _BlockLight, screenSize);
                float2 snappedLightRT = snappedLightVP * rtHandleScale;
                float4 colLight = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_point_clamp, snappedLightRT, 0);

                float2 snappedDarkVP  = SnapUVToBlock(vpUV, _BlockDark, screenSize);
                float2 snappedDarkRT  = snappedDarkVP * rtHandleScale;
                float4 colDark  = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_point_clamp, snappedDarkRT, 0);

                // ----------------------------------------------------------
                // 3. Плавный переход через bayer-дитер по сетке КРУПНОГО блока.
                //
                //    Считаем дитер по _BlockDark — тогда каждая "клетка" перехода
                //    имеет размер ровно крупного пикселя, и переход выглядит как
                //    мозаика: часть крупных клеток заменяется блоком из мелких пикселей.
                //    (Раньше было по _BlockLight, что давало более шумную границу.)
                // ----------------------------------------------------------
                float t = smoothstep(_LumThreshold, _LumThreshold + _LumSoftness, lum);

                int2 transitionDitherCoord = int2(floor(vpUV * screenSize / _BlockDark));
                float transitionBayer      = GetBayer4x4(transitionDitherCoord);

                // mixMask = 1 → мелкий пиксель (светло), 0 → крупный (темно)
                float mixMask = step(transitionBayer, t - (1.0/16.0)*0.5);
                float4 col = lerp(colDark, colLight, mixMask);

                // ----------------------------------------------------------
                // 4. Квантование цвета (PS1-стиль)
                // ----------------------------------------------------------
                float levels = max(_ColorLevels - 1.0, 1.0);
                float3 q = saturate(col.rgb);
                q = floor(q * levels + 0.5) / levels;

                return float4(q, col.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
