Shader "PS1/PixelizeUpscale"
{
    Properties
    {
        // Главный вход — копия камеры в полном разрешении.
        _MainTex      ("Camera Color (full-res)", 2D) = "white" {}

        // ===================== ПИКСЕЛИЗАЦИЯ =====================

        // Размер блока в светлых зонах (в "пикселях экрана").
        // Большее значение = более крупные пиксели.
        // 4 = пиксели 4x4 при 1080p, легкая пикселизация.
        _BlockLight   ("Block Size (light)",  Range(1, 64)) = 4

        // Размер блока в тёмных зонах. Больше блок = более грубая картинка.
        // 12 = пиксели 12x12, заметная PS1-стилистика в тенях.
        _BlockDark    ("Block Size (dark)",   Range(1, 64)) = 12

        // Порог luminance, ниже которого считаем "темно".
        // 0.0 = пикселизация только в чистом чёрном.
        // 0.5 = пикселизация в средних тонах и темнее.
        _LumThreshold ("Lum Threshold",       Range(0, 1)) = 0.35

        // Мягкость перехода от светлого к тёмному.
        // Малое значение = резкая граница между уровнями.
        // Большое = плавный градиент.
        _LumSoftness  ("Lum Softness",        Range(0.001, 1)) = 0.25

        // ===================== ТЕНИ / DITHER =====================

        // Размер ячейки Bayer-сетки в экранных пикселях.
        // 1 = 1px = настоящая 1:1 dither.
        // 2..4 = более крупный, "ретро" dither.
        // ВАЖНО: одинаков для всей сцены — это и даёт "тени одинаково пиксельные".
        _DitherScale  ("Dither Scale (px)",   Range(1, 8)) = 2

        // Сила dither в тенях.
        // 0 = выкл, 1 = полный dither.
        _DitherStrength ("Dither Strength",   Range(0, 1)) = 0.85

        // Порог luminance, ниже которого включается dither.
        // Обычно ставится >= _LumThreshold.
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.45

        // Мягкость нарастания dither.
        _ShadowSoftness  ("Shadow Softness",  Range(0.001, 1)) = 0.3

        // ===================== КВАНТОВАНИЕ ЦВЕТА =====================

        // Количество градаций на канал (как PS1 R5G5B5).
        // 32 = 5 бит на канал. 16 = 4 бита (грубее).
        // 256 = практически выкл.
        _ColorLevels  ("Color Levels (per channel)", Range(2, 256)) = 32

        // ===================== ВНУТРЕННИЕ =====================

        // Размер viewport-а в пикселях. Передаётся из C# (учитывает
        // dynamic resolution и не равен размеру allocated RT).
        _ScreenSizeOverride ("Screen Size (xy = px, zw = 1/px)", Vector) = (1920, 1080, 0.0005208, 0.0009259)

        // RTHandle scale — отношение фактического использованного куска
        // RT к его allocated size (HDRP пулит RT и часто аллоцирует
        // больше чем нужно). По умолчанию (1,1).
        _RTHandleScaleOverride ("RTHandle Scale (xy)", Vector) = (1, 1, 0, 0)
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

            // Common.hlsl даёт GetFullScreenTriangleVertexPosition,
            // GetFullScreenTriangleTexCoord, базовые TEXTURE2D/SAMPLER.
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            // _MainTex биндится из C# через Material.SetTexture как
            // обычная 2D-текстура — поэтому НЕ TEXTURE2D_X.
            TEXTURE2D(_MainTex);

            // Inline-сэмплеры. Имена в стиле sampler_linear_clamp /
            // sampler_point_clamp Unity распознаёт автоматически и
            // создаёт корректные SamplerState.
            SAMPLER(sampler_linear_clamp);
            SAMPLER(sampler_point_clamp);

            float  _BlockLight;
            float  _BlockDark;
            float  _LumThreshold;
            float  _LumSoftness;

            float  _DitherScale;
            float  _DitherStrength;
            float  _ShadowThreshold;
            float  _ShadowSoftness;

            float  _ColorLevels;

            float4 _ScreenSizeOverride;
            float4 _RTHandleScaleOverride;

            // ============================================================
            //   Bayer 4x4 матрица — классический ordered dither.
            //   Значения нормализованы в [0..1], центр ~ 0.5.
            // ============================================================
            static const float Bayer4x4[16] =
            {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

            float GetBayer4x4(int2 pixelCoord)
            {
                // Берём по модулю 4. & 3 быстрее на GPU чем %.
                int2 c = pixelCoord & 3;
                return Bayer4x4[c.y * 4 + c.x];
            }

            // BT.709 luminance.
            float Luminance709(float3 c)
            {
                return dot(c, float3(0.2126, 0.7152, 0.0722));
            }

            // ============================================================
            //   Snap UV в сетку blockSize-экранных пикселей.
            //   uv         — UV в [0..1] относительно VIEWPORT.
            //   blockSize  — размер блока в screen px.
            //   screenSize — фактический размер viewport в пикселях.
            // ============================================================
            float2 SnapUVToBlock(float2 uv, float blockSize, float2 screenSize)
            {
                float2 pixelCoord = uv * screenSize;
                float2 snapped    = floor(pixelCoord / blockSize) * blockSize
                                  + (blockSize * 0.5);
                return snapped / screenSize;
            }

            struct Attributes { uint vertexID : SV_VertexID; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;   // viewport UV [0..1]
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

                // Viewport UV (от 0 до 1 по используемой части).
                float2 vpUV = input.uv;

                // RTHandle UV — для сэмплинга реальной текстуры,
                // которая может быть аллоцирована больше чем viewport.
                float2 sampleUV = vpUV * rtHandleScale;

                // -------------------------------------------------------
                // (1) Luminance probe.
                //     Линейный сэмпл — даёт area-averaged яркость без
                //     ступенек. На основе этого выбираем размер блока
                //     и силу dither.
                // -------------------------------------------------------
                float4 probe = SAMPLE_TEXTURE2D_LOD(
                    _MainTex, sampler_linear_clamp, sampleUV, 0);

                // HDRP color buffer хранит pre-tonemap HDR. Reinhard
                // для нормализации в [0..1).
                float lumLin = Luminance709(probe.rgb);
                float lum    = lumLin / (1.0 + lumLin);

                // -------------------------------------------------------
                // (2) Adaptive block size.
                //     t = 0 → темно → blockDark.
                //     t = 1 → светло → blockLight.
                // -------------------------------------------------------
                float t = smoothstep(_LumThreshold,
                                     _LumThreshold + _LumSoftness,
                                     lum);
                float blockSize = lerp(_BlockDark, _BlockLight, t);

                // -------------------------------------------------------
                // (3) Snap viewport-UV в сетку блоков, далее в RT-UV.
                // -------------------------------------------------------
                float2 snappedVP = SnapUVToBlock(vpUV, blockSize, screenSize);
                float2 snappedRT = snappedVP * rtHandleScale;

                float4 col = SAMPLE_TEXTURE2D_LOD(
                    _MainTex, sampler_point_clamp, snappedRT, 0);

                // -------------------------------------------------------
                // (4) Bayer dither + квантование.
                //
                //     Bayer координата считается по реальным экранным
                //     пикселям (vpUV * screenSize), а НЕ по snappedUV —
                //     иначе все пиксели одного блока имели бы одинаковое
                //     значение Bayer и dither не был бы виден.
                //
                //     _DitherScale — размер ячейки Bayer в экранных px,
                //     не зависит от уровня пикселизации сцены. Это и
                //     даёт "тени везде одинаково пиксельные".
                // -------------------------------------------------------
                int2 ditherCoord = int2(floor(vpUV * screenSize / _DitherScale));
                float bayer      = GetBayer4x4(ditherCoord);

                // shadowMask: 1 в тенях, 0 в свету.
                float shadowMask = 1.0 - smoothstep(
                    _ShadowThreshold,
                    _ShadowThreshold + _ShadowSoftness,
                    lum);

                float ditherK      = _DitherStrength * shadowMask;
                // bayer центрируем вокруг 0: [-0.5..+0.5].
                float ditherOffset = (bayer - 0.5) * ditherK;

                // Квантование на _ColorLevels уровней.
                // Прибавляем dither до floor() → ordered dither.
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
