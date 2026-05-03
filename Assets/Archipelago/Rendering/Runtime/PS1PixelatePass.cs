using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Archipelago.Rendering
{
    /// <summary>
    /// CustomPass: экранная пикселизация + color quantization + Bayer dithering.
    ///
    /// Читает параметры из PS1PixelateVolume через VolumeManager.
    /// Адаптивный downscale на основе luminance буфера.
    ///
    /// HDRP 17.4:
    ///   - Наследуем CustomPass
    ///   - Используем CoreUtils.SetRenderTarget (не Graphics.Blit)
    ///   - Все текстуры — RTHandle
    ///   - Добавить CustomPassVolume в сцену с этим пассом
    ///
    /// LIMITATION: Render Graph API в HDRP 17.x — CustomPass всё ещё
    /// использует старый CommandBuffer API внутри Execute().
    /// Для полного Render Graph перехода нужен FullScreenCustomPass.
    /// </summary>
    public sealed class PS1PixelatePass : CustomPass
    {
        // ── Shader property IDs (кэшируем для PERF) ──────────────
        private static readonly int _ColorLevelsId        = Shader.PropertyToID("_ColorLevels");
        private static readonly int _QuantStrengthId      = Shader.PropertyToID("_QuantStrength");
        private static readonly int _DitheringStrengthId  = Shader.PropertyToID("_DitheringStrength");
        private static readonly int _BayerSizeId          = Shader.PropertyToID("_BayerSize");
        private static readonly int _DownscaleId          = Shader.PropertyToID("_Downscale");
        private static readonly int _LumThresholdId       = Shader.PropertyToID("_LumThreshold");
        private static readonly int _InputTextureId       = Shader.PropertyToID("_InputTexture");

        // ── Material ──────────────────────────────────────────────
        private Material _material;
        private RTHandle _tempRT;

        // ── CustomPass lifecycle ──────────────────────────────────

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            // Shader создаётся из embedded HLSL — см. PS1Pixelate.shader
            var shader = Shader.Find("Hidden/Archipelago/PS1Pixelate");
            if (shader == null)
            {
                Debug.LogError("[PS1PixelatePass] Shader 'Hidden/Archipelago/PS1Pixelate' not found. " +
                               "Убедись что PS1Pixelate.shader находится в папке проекта.");
                return;
            }
            _material = CoreUtils.CreateEngineMaterial(shader);
        }

        protected override void Execute(
            CustomPassContext ctx)
        {
            // Читаем параметры из Volume stack
            var stack  = VolumeManager.instance.stack;
            var volume = stack.GetComponent<PS1PixelateVolume>();

            if (volume == null || !volume.IsActive()) return;

            var cmd        = ctx.cmd;
            var hdCamera   = ctx.hdCamera;
            var colorBuffer = ctx.cameraColorBuffer;

            if (_material == null) return;

            // Передаём параметры в шейдер
            _material.SetFloat(_ColorLevelsId,       volume.ColorLevels.value);
            _material.SetFloat(_QuantStrengthId,     volume.QuantizationStrength.value);
            _material.SetFloat(_DitheringStrengthId, volume.DitheringEnabled.value
                ? volume.DitheringStrength.value : 0f);
            _material.SetFloat(_BayerSizeId,         volume.BayerSize.value);
            _material.SetFloat(_DownscaleId,         volume.MaxDownscale.value);
            _material.SetFloat(_LumThresholdId,      volume.LuminanceThreshold.value);

            // HDRP 17.4: используем CoreUtils.SetRenderTarget + DrawFullScreen
            // НЕ Graphics.Blit — не совместим с Render Graph
            int w = hdCamera.actualWidth;
            int h = hdCamera.actualHeight;

            // Временный RT для downscale
            var tempDesc = new RenderTextureDescriptor(
                Mathf.Max(1, w / (int)volume.MaxDownscale.value),
                Mathf.Max(1, h / (int)volume.MaxDownscale.value),
                RenderTextureFormat.DefaultHDR, 0);

            _tempRT = RTHandles.Alloc(tempDesc.width, tempDesc.height,
                colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat);

            // Downscale → quantize → dither → blit обратно
            _material.SetTexture(_InputTextureId, colorBuffer);

            CoreUtils.SetRenderTarget(cmd, _tempRT);
            CoreUtils.DrawFullScreen(cmd, _material, shaderPassId: 0);

            // Blit обратно в color buffer
            _material.SetTexture(_InputTextureId, _tempRT);
            CoreUtils.SetRenderTarget(cmd, colorBuffer);
            CoreUtils.DrawFullScreen(cmd, _material, shaderPassId: 1);

            // Освобождаем временный RT
            RTHandles.Release(_tempRT);
            _tempRT = null;
        }

        protected override void Cleanup()
        {
            CoreUtils.Destroy(_material);
            if (_tempRT != null)
            {
                RTHandles.Release(_tempRT);
                _tempRT = null;
            }
        }
    }
}
