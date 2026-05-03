using UnityEngine;
using UnityEngine.Rendering;

namespace Archipelago.Rendering
{
    /// <summary>
    /// Volume компонент для PS1 пикселизации.
    /// Все параметры управляются через Volume stack — можно плавно менять
    /// через Local Volumes (генераторная = сильнее, хаб = слабее).
    ///
    /// HDRP 17.4: Inherit от VolumeComponent, поля — VolumeParameter<T>.
    /// </summary>
    [VolumeComponentMenu("Archipelago/PS1 Pixelate")]
    public sealed class PS1PixelateVolume : VolumeComponent, IPostProcessComponent
    {
        // ── Pixelation ────────────────────────────────────────────

        [Tooltip("Включить эффект пикселизации")]
        public BoolParameter Enabled = new(false, overrideState: true);

        [Tooltip("Минимальный downscale при полном свете (1 = нет эффекта)")]
        public ClampedFloatParameter MinDownscale = new(1f, 1f, 8f);

        [Tooltip("Максимальный downscale в тени")]
        public ClampedFloatParameter MaxDownscale = new(4f, 1f, 8f);

        [Tooltip("Порог luminance ниже которого начинается пикселизация [0,1]")]
        public ClampedFloatParameter LuminanceThreshold = new(0.3f, 0f, 1f);

        // ── Color Quantization ────────────────────────────────────

        [Tooltip("Количество уровней на канал (2=1bit, 4=2bit, 8=PS1-like, 256=off)")]
        public ClampedIntParameter ColorLevels = new(32, 2, 256);

        [Tooltip("Интенсивность color quantization [0,1]")]
        public ClampedFloatParameter QuantizationStrength = new(0.8f, 0f, 1f);

        // ── Dithering ─────────────────────────────────────────────

        [Tooltip("Включить Bayer ordered dithering")]
        public BoolParameter DitheringEnabled = new(true, overrideState: true);

        [Tooltip("Размер матрицы Байера: 2=2x2, 4=4x4, 8=8x8")]
        public ClampedIntParameter BayerSize = new(4, 2, 8);

        [Tooltip("Интенсивность дизеринга [0,1]")]
        public ClampedFloatParameter DitheringStrength = new(0.5f, 0f, 1f);

        // ── IPostProcessComponent ─────────────────────────────────

        public bool IsActive() => Enabled.value;
        public bool IsTileCompatible() => false;
    }
}
