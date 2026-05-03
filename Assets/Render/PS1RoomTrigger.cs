using UnityEngine;

/// <summary>
/// Вешается на триггер-коллайдер комнаты.
/// При входе игрока меняет настройки PS1Effect на камере.
/// </summary>
public class PS1RoomTrigger : MonoBehaviour
{
    [System.Serializable]
    public struct RoomSettings
    {
        [Tooltip("320 = обычно, 160 = генераторная")]
        public int   targetWidth;
        [Tooltip("5 = PS1 стандарт, 3 = экстремальная деградация")]
        [Range(1,8)] public int colorBits;
        [Tooltip("Сила дизеринга")]
        [Range(0f,2f)] public float ditherStrength;
    }

    [Header("Настройки этой комнаты")]
    public RoomSettings settings = new RoomSettings
    {
        targetWidth   = 320,
        colorBits     = 5,
        ditherStrength = 0.5f
    };

    // Быстрые пресеты прямо в инспекторе
    public enum Preset { Custom, Normal, Dark, Generator, Outside, Corrupted }
    public Preset preset = Preset.Custom;

    private void OnValidate()
    {
        // При смене пресета в инспекторе — заполняем поля
        settings = preset switch
        {
            Preset.Normal    => new RoomSettings { targetWidth = 320, colorBits = 5, ditherStrength = 0.5f },
            Preset.Dark      => new RoomSettings { targetWidth = 240, colorBits = 4, ditherStrength = 0.9f },
            Preset.Generator => new RoomSettings { targetWidth = 160, colorBits = 3, ditherStrength = 1.5f },
            Preset.Outside   => new RoomSettings { targetWidth = 480, colorBits = 6, ditherStrength = 0.2f },
            Preset.Corrupted => new RoomSettings { targetWidth = 128, colorBits = 2, ditherStrength = 2.0f },
            _                => settings
        };
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Ищем PS1Effect на Main Camera
        var effect = Camera.main?.GetComponent<PS1Effect>();
        if (effect == null) return;

        effect.targetWidth    = settings.targetWidth;
        effect.colorBits      = settings.colorBits;
        effect.ditherStrength = settings.ditherStrength;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
        var col = GetComponent<Collider>();
        if (col != null) Gizmos.DrawCube(col.bounds.center, col.bounds.size);

        // Показываем текущий пресет над триггером
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            $"PS1: {preset}\n{settings.targetWidth}px | {settings.colorBits}bit"
        );
        #endif
    }
}
