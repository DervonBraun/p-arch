using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;

public class PixelizePass : CustomPass
{
    //[Header("Пикселизация")]
    [Range(1, 64)] public int blockLight = 4;
    [Range(1, 64)] public int blockDark = 12;

    //[Tooltip("Порог яркости для перехода.")]
    [Range(0f, 1f)] public float lumThreshold = 0.35f;

    //[Tooltip("Плавность перехода (создает дитер-зону между крупными и мелкими пикселями).")]
    [Range(0.001f, 1f)] public float lumSoftness = 0.25f;

    //[Header("Квантование")]
    [Range(2, 256)] public int colorLevels = 32;

    [Header("Shader")]
    public Shader pixelizeShader;

    private Material _material;
    private RTHandle _tempColorRT;

    private static readonly int ID_MainTex            = Shader.PropertyToID("_MainTex");
    private static readonly int ID_BlockLight         = Shader.PropertyToID("_BlockLight");
    private static readonly int ID_BlockDark          = Shader.PropertyToID("_BlockDark");
    private static readonly int ID_LumThreshold       = Shader.PropertyToID("_LumThreshold");
    private static readonly int ID_LumSoftness       = Shader.PropertyToID("_LumSoftness");
    private static readonly int ID_ColorLevels        = Shader.PropertyToID("_ColorLevels");
    private static readonly int ID_ScreenSizeOverride = Shader.PropertyToID("_ScreenSizeOverride");
    private static readonly int ID_RTHandleScaleOverride = Shader.PropertyToID("_RTHandleScaleOverride");

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        if (pixelizeShader == null) return;

        _material = CoreUtils.CreateEngineMaterial(pixelizeShader);
        _tempColorRT = RTHandles.Alloc(
            scaleFactor: Vector2.one,
            colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
            filterMode: FilterMode.Bilinear,
            wrapMode: TextureWrapMode.Clamp,
            useDynamicScale: true,
            name: "PixelizePass_TempColor"
        );
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (_material == null || _tempColorRT == null) return;

        CommandBuffer cmd = ctx.cmd;
        HDUtils.BlitCameraTexture(cmd, ctx.cameraColorBuffer, _tempColorRT);

        _material.SetTexture(ID_MainTex, _tempColorRT);
        _material.SetFloat(ID_BlockLight,   blockLight);
        _material.SetFloat(ID_BlockDark,    Mathf.Max(blockDark, blockLight));
        _material.SetFloat(ID_LumThreshold, lumThreshold);
        _material.SetFloat(ID_LumSoftness,  lumSoftness);
        _material.SetFloat(ID_ColorLevels,  colorLevels);

        float w = ctx.hdCamera.actualWidth;
        float h = ctx.hdCamera.actualHeight;
        _material.SetVector(ID_ScreenSizeOverride, new Vector4(w, h, 1.0f / w, 1.0f / h));

        Vector4 rtScale = RTHandles.rtHandleProperties.rtHandleScale;
        _material.SetVector(ID_RTHandleScaleOverride, new Vector4(rtScale.x, rtScale.y, 0, 0));

        HDUtils.DrawFullScreen(cmd, _material, ctx.cameraColorBuffer, shaderPassId: 0);
    }

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