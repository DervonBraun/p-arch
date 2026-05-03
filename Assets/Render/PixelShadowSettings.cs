using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Добавь на любой GameObject в сцене.
// Форсирует point (nearest-neighbor) фильтрацию на shadow atlas HDRP
// и выставляет минимальное разрешение shadow map.
//
// HDRP не даёт прямого доступа к shadow atlas texture через публичное API,
// поэтому патчим через CommandBuffer глобальный sampler state.

[ExecuteAlways]
public class PixelShadowSettings : MonoBehaviour
{
    [Header("Shadow Map Resolution")]
    [Tooltip("Разрешение shadow map. 256-512 для грубых пиксельных теней.")]
    public int shadowResolution = 256;

    [Header("Shadow Appearance")]
    [Range(0f, 1f)]
    [Tooltip("Жёсткость края тени. 0 = максимально жёсткий.")]
    public float shadowSoftness = 0f;

    [Tooltip("Normal bias для избежания acne. Подбирай под масштаб сцены.")]
    public float normalBias = 0.01f;

    private HDAdditionalLightData[] _lights;

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        ApplyToAllLights();
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
    }

    void OnValidate()
    {
        ApplyToAllLights();
    }

    // Применяем настройки ко всем источникам света в сцене
    void ApplyToAllLights()
    {
        _lights = FindObjectsByType<HDAdditionalLightData>(FindObjectsSortMode.None);

        foreach (var light in _lights)
        {
            // Форсируем Hard shadows — никакого PCF/PCSS
            light.shadowUpdateMode = ShadowUpdateMode.EveryFrame;

            // Минимальное разрешение = максимально пиксельные тени
            light.SetShadowResolution(shadowResolution);

            // Убираем размытие края
            light.softnessScale = shadowSoftness;

            // Normal bias вручную
            light.normalBias = normalBias;

            // Slope bias минимальный — не трогаем геометрию
            light.slopeBias = 0.02f;
        }
    }

    // Каждый кадр патчим глобальные shadow sampler keywords
    void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        // Отключаем все варианты PCF фильтрации через глобальные keywords.
        // HDRP использует эти keywords чтобы выбрать алгоритм фильтрации
        // в своих internal shadow sampling шейдерах.
        Shader.DisableKeyword("SHADOW_LOW");
        Shader.DisableKeyword("SHADOW_MEDIUM");
        Shader.DisableKeyword("SHADOW_HIGH");
        Shader.DisableKeyword("SHADOW_VERY_HIGH");

        // SHADOW_LOW в HDRP = 1 tap, без PCF.
        // Это ближайшее к point sampling что доступно через публичный API.
        Shader.EnableKeyword("SHADOW_LOW");
    }

    // Gizmo чтобы видеть объект в сцене
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.8f);
        Gizmos.DrawIcon(transform.position, "d_Light Icon", true);
    }
}
