using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// Custom Pass: адаптивная пикселизация + ordered dither в тенях.
///
/// Алгоритм:
/// 1. Копируем full-res камерный буфер в промежуточный RT той же резолюшн.
/// 2. Запускаем fullscreen-шейдер, который из этой копии читает luminance,
///    адаптивно выбирает размер блока пикселизации и накладывает
///    Bayer 4x4 dither с фиксированным screen-space размером в тенях.
/// 3. Результат пишем обратно в камерный буфер.
///
/// ВАЖНО: low-res RT не используется. Старая схема (downscale → upscale)
/// давала двойную пикселизацию и интерференционные артефакты.
/// Тут пикселизация делается одним snap-сэмплом из full-res, никакой
/// предварительной потери информации.
///
/// Точка инъекции — рекомендуется BeforePostProcess (уже посчитан весь
/// свет, тени, GI, но ещё не сделан tonemap/grading — поэтому luminance
/// в HDR работает корректно через Reinhard внутри шейдера).
/// </summary>
public class PixelizePass : CustomPass
{
    // ===================== Параметры пикселизации =====================

    [Tooltip("Размер блока в светлых зонах (в экранных пикселях).")]
    [Range(1, 64)]
    public int blockLight = 4;

    [Tooltip("Размер блока в тёмных зонах. Должен быть >= blockLight.")]
    [Range(1, 64)]
    public int blockDark = 12;

    [Tooltip("Ниже этого значения luminance считается 'темно'.")]
    [Range(0f, 1f)]
    public float lumThreshold = 0.35f;

    [Tooltip("Мягкость перехода между мелкими и крупными блоками.")]
    [Range(0.001f, 1f)]
    public float lumSoftness = 0.25f;

    // ===================== Параметры dither =====================

    [Tooltip("Размер ячейки Bayer-dither в экранных пикселях. " +
             "Одинаков для всех теней — это и даёт 'тени равно пиксельные'.")]
    [Range(1, 8)]
    public int ditherScale = 2;

    [Tooltip("Сила dither эффекта в тенях.")]
    [Range(0f, 1f)]
    public float ditherStrength = 0.85f;

    [Tooltip("Ниже этого luminance включается dither. " +
             "Обычно >= lumThreshold.")]
    [Range(0f, 1f)]
    public float shadowThreshold = 0.45f;

    [Tooltip("Мягкость нарастания dither.")]
    [Range(0.001f, 1f)]
    public float shadowSoftness = 0.3f;

    // ===================== Квантование цвета =====================

    [Tooltip("Уровней на канал. 32 = классика PS1 (R5G5B5). " +
             "256 = практически выкл.")]
    [Range(2, 256)]
    public int colorLevels = 32;

    // ===================== Шейдер =====================

    [Header("Shader")]
    public Shader pixelizeShader;

    // ===================== Внутреннее =====================

    private Material _material;
    private RTHandle _tempColorRT;

    // ----- Property IDs (кэшируем чтобы не дёргать Shader.PropertyToID каждый кадр) -----
    private static readonly int ID_MainTex            = Shader.PropertyToID("_MainTex");
    private static readonly int ID_BlockLight         = Shader.PropertyToID("_BlockLight");
    private static readonly int ID_BlockDark          = Shader.PropertyToID("_BlockDark");
    private static readonly int ID_LumThreshold       = Shader.PropertyToID("_LumThreshold");
    private static readonly int ID_LumSoftness        = Shader.PropertyToID("_LumSoftness");
    private static readonly int ID_DitherScale        = Shader.PropertyToID("_DitherScale");
    private static readonly int ID_DitherStrength     = Shader.PropertyToID("_DitherStrength");
    private static readonly int ID_ShadowThreshold    = Shader.PropertyToID("_ShadowThreshold");
    private static readonly int ID_ShadowSoftness     = Shader.PropertyToID("_ShadowSoftness");
    private static readonly int ID_ColorLevels             = Shader.PropertyToID("_ColorLevels");
    private static readonly int ID_ScreenSizeOverride      = Shader.PropertyToID("_ScreenSizeOverride");
    private static readonly int ID_RTHandleScaleOverride   = Shader.PropertyToID("_RTHandleScaleOverride");

    // ----- Setup -----

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        if (pixelizeShader == null)
        {
            Debug.LogError("[PixelizePass] Pixelize shader не назначен.");
            return;
        }

        _material = CoreUtils.CreateEngineMaterial(pixelizeShader);

        // Промежуточный RT — full-res, такой же формат как у камеры.
        // RTHandles.Alloc с scaleFactor=Vector2.one даёт авто-resize при
        // изменении размера окна / dynamic resolution.
        _tempColorRT = RTHandles.Alloc(
            scaleFactor: Vector2.one,
            colorFormat: GraphicsFormat.R16G16B16A16_SFloat,  // HDR
            filterMode: FilterMode.Bilinear,
            wrapMode: TextureWrapMode.Clamp,
            useDynamicScale: true,
            name: "PixelizePass_TempColor"
        );
    }

    // ----- Execute -----

    protected override void Execute(CustomPassContext ctx)
    {
        if (_material == null || _tempColorRT == null)
            return;

        CommandBuffer cmd = ctx.cmd;

        // (1) Копируем камерный цвет в темп-RT.
        // Это даёт нам "input snapshot" — нельзя читать и писать одновременно
        // в один RT, так что это обязательный шаг.
        HDUtils.BlitCameraTexture(cmd, ctx.cameraColorBuffer, _tempColorRT);

        // (2) Заполняем параметры материала.
        _material.SetTexture(ID_MainTex, _tempColorRT);

        _material.SetFloat(ID_BlockLight,     blockLight);
        _material.SetFloat(ID_BlockDark,      Mathf.Max(blockDark, blockLight));
        _material.SetFloat(ID_LumThreshold,   lumThreshold);
        _material.SetFloat(ID_LumSoftness,    lumSoftness);

        _material.SetFloat(ID_DitherScale,    Mathf.Max(ditherScale, 1));
        _material.SetFloat(ID_DitherStrength, ditherStrength);
        _material.SetFloat(ID_ShadowThreshold, shadowThreshold);
        _material.SetFloat(ID_ShadowSoftness, shadowSoftness);

        _material.SetFloat(ID_ColorLevels,    colorLevels);

        // Передаём фактический размер используемой области cameraColorBuffer.
        // ctx.hdCamera.actualWidth/Height — live-разрешение (учитывает DRS).
        float w = ctx.hdCamera.actualWidth;
        float h = ctx.hdCamera.actualHeight;
        _material.SetVector(ID_ScreenSizeOverride,
            new Vector4(w, h, 1.0f / w, 1.0f / h));

        // RTHandleScale — отношение реально используемой части RT к
        // его allocated размеру. Без этого UV в шейдере "уедут" если
        // HDRP аллоцировал RT больше чем нужно (что почти всегда так).
        Vector4 rtScale = RTHandles.rtHandleProperties.rtHandleScale;
        _material.SetVector(ID_RTHandleScaleOverride,
            new Vector4(rtScale.x, rtScale.y, 0, 0));

        // (3) Рисуем в камерный буфер.
        HDUtils.DrawFullScreen(cmd, _material, ctx.cameraColorBuffer, shaderPassId: 0);
    }

    // ----- Cleanup -----

    protected override void Cleanup()
    {
        if (_tempColorRT != null)
        {
            RTHandles.Release(_tempColorRT);
            _tempColorRT = null;
        }

        if (_material != null)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
}