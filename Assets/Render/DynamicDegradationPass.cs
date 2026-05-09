using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Custom Pass: Динамическая деградация изображения.
/// Реализует пикселизацию, спектральное расслоение и temporal ghosting
/// на основе яркости сцены.
/// </summary>
public class DynamicDegradationPass : CustomPass
{
    //[Header("Parameters")]
    //[Tooltip("Целевой размер пикселя в темных участках.")]
    [Range(1f, 128f)]
    public float pixelSize = 16f;

    [Tooltip("Сила спектрального расслоения (Chromatic Aberration) в темных участках.")]
    [Range(0f, 0.5f)]
    public float aberrationStrength = 0.05f;

    [Tooltip("Интенсивность цифрового шлейфа (Temporal Ghosting).")]
    [Range(0f, 1f)]
    public float ghostingIntensity = 0.0f;

    [Header("Shader")]
    public Shader degradationShader;

    private Material _material;
    private RTHandle _tempColorRT;

    // Кэшированные ID свойств
    private static readonly int ID_PixelSize = Shader.PropertyToID("_PixelSize");
    private static readonly int ID_AberrationStrength = Shader.PropertyToID("_AberrationStrength");
    private static readonly int ID_GhostingIntensity = Shader.PropertyToID("_GhostingIntensity");
    private static readonly int ID_CustomPassColorBuffer = Shader.PropertyToID("_CustomPassColorBuffer");

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        if (degradationShader == null)
        {
            degradationShader = Shader.Find("FullScreen/DynamicDegradation");
        }

        if (degradationShader != null)
        {
            _material = CoreUtils.CreateEngineMaterial(degradationShader);
        }
        else
        {
            Debug.LogError("[DynamicDegradationPass] Shader не найден. Убедитесь, что назначен правильный шейдер.");
        }

        // Выделяем временный RT для копии экрана
        _tempColorRT = RTHandles.Alloc(
            scaleFactor: Vector2.one,
            colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
            useDynamicScale: true,
            name: "DynamicDegradation_TempColor"
        );
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (_material == null || _tempColorRT == null) return;

        // Копируем текущий кадр в наш временный RT
        HDUtils.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, _tempColorRT);

        // Передаем параметры
        _material.SetFloat(ID_PixelSize, pixelSize);
        _material.SetFloat(ID_AberrationStrength, aberrationStrength);
        _material.SetFloat(ID_GhostingIntensity, ghostingIntensity);
        
        // Передаем scale для правильного сэмплинга RTHandle (решает проблему серого экрана из-за кривых UV)
        Vector4 rtScale = RTHandles.rtHandleProperties.rtHandleScale;
        _material.SetVector(Shader.PropertyToID("_RTHandleScaleOverride"), new Vector4(rtScale.x, rtScale.y, 0, 0));
        
        // Передаем текстуру
        _material.SetTexture(Shader.PropertyToID("_MainTex"), _tempColorRT);
        
        // Отрисовка
        HDUtils.DrawFullScreen(ctx.cmd, _material, ctx.cameraColorBuffer, shaderPassId: 0);
    }

    protected override void Cleanup()
    {
        _tempColorRT?.Release();
        if (_material != null)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
}
