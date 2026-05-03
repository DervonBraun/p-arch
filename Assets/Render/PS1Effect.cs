using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Вся PS1-система в одном скрипте.
/// Повесьте на GameObject с Camera. Больше ничего не нужно.
/// </summary>
[RequireComponent(typeof(Camera))]
public class PS1Effect : MonoBehaviour
{
    [Header("Пикселизация")]
    [Tooltip("Ширина в пикселях. 320 = аутентичный PS1")]
    public int targetWidth = 320;

    [Tooltip("Принудительно 4:3 как на PS1")]
    public bool force4x3 = true;

    [Header("Цвет")]
    [Tooltip("Бит на канал. 5 = PS1 (32768 цветов)")]
    [Range(1, 8)] public int colorBits = 5;

    [Header("Дизеринг Байера")]
    [Range(0f, 2f)] public float ditherStrength = 0.5f;

    [Header("Vertex Snapping")]
    [Tooltip("Применяется к материалам с шейдером PS1/PS1Material")]
    [Range(32f, 512f)] public float snapResolution = 128f;

    // ── Приватное ─────────────────────────────────────────────────────────────
    private Material _mat;
    private bool     _ready;

    private void OnEnable()
    {
        var shader = Shader.Find("Hidden/PS1PostProcess");
        if (shader == null)
        {
            Debug.LogError("[PS1Effect] Шейдер 'Hidden/PS1PostProcess' не найден.\n" +
                           "Убедитесь что PS1PostProcess.shader лежит в папке Assets.");
            return;
        }
        _mat   = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        _ready = true;

        // Обновляем Snap Resolution на всех PS1-материалах в сцене
        RefreshSceneMaterials();
    }

    private void OnDisable()
    {
        if (_mat != null) DestroyImmediate(_mat);
        _ready = false;
    }

    // OnRenderImage — стандартный Unity hook, работает без Custom Pass Volume
    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (!_ready || _mat == null)
        {
            Graphics.Blit(src, dst);
            return;
        }

        int w = targetWidth;
        int h = force4x3
            ? Mathf.RoundToInt(w * 3f / 4f)
            : Mathf.RoundToInt(w * (float)src.height / src.width);

        _mat.SetFloat ("_ColorLevels",   Mathf.Pow(2f, colorBits));
        _mat.SetFloat ("_DitherStrength", ditherStrength);
        _mat.SetVector("_LowResolution", new Vector4(w, h, 1f / w, 1f / h));

        // Рендерим в низкое разрешение с Point filter (пиксельный результат)
        var lowRes = RenderTexture.GetTemporary(w, h, 0, src.format);
        lowRes.filterMode = FilterMode.Point;

        Graphics.Blit(src, lowRes, _mat, 0);    // квантизация + дизеринг
        Graphics.Blit(lowRes, dst);              // Point-апскейл на экран

        RenderTexture.ReleaseTemporary(lowRes);
    }

    // Обновляем snapResolution на всех материалах с шейдером PS1/PS1Material
    public void RefreshSceneMaterials()
    {
        foreach (var r in FindObjectsOfType<Renderer>())
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m != null && m.shader != null &&
                    m.shader.name == "PS1/PS1Material")
                {
                    m.SetFloat("_SnapResolution", snapResolution);
                }
            }
        }
    }

    // В Update чтобы изменения в инспекторе сразу применялись
    private void Update()
    {
        RefreshSceneMaterials();
    }
}
