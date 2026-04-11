using UnityEngine;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Конфигурация сканера. Создаётся через
    /// Assets → Create → Archipelago → Scanner → ScannerConfig.
    /// Один инстанс на проект, назначается в ScannerInstaller.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ScannerConfig",
        menuName  = "Archipelago/Scanner/ScannerConfig")]
    public sealed class ScannerConfig : ScriptableObject
    {
        [Header("Server")]
        [Tooltip("Railway proxy URL. Пример: https://your-app.railway.app")]
        public string serverBaseUrl = "https://your-app.railway.app";

        [Tooltip("Таймаут HTTP запроса в секундах.")]
        public float requestTimeoutSeconds = 15f;

        [Tooltip("Максимальное количество retry при ошибке сети.")]
        public int maxRetries = 3;

        [Header("Scanner Behaviour")]
        [Tooltip("Максимальная дистанция raycast в метрах.")]
        public float scanRaycastDistance = 5f;

        [Tooltip("Layer mask для raycast сканера.")]
        public LayerMask scanLayerMask = ~0;

        [Header("Offline Fallback")]
        [Tooltip("Тексты заглушек при недоступном сервере. Один выбирается случайно.")]
        [TextArea(2, 3)]
        public string[] offlineFallbackMessages = new[]
        {
            "Связь с базой данных прервана. Повторите попытку позже.",
            "Сервер не отвечает. Данные датчиков сохранены локально.",
            "Соединение нестабильно. Запрос будет обработан при восстановлении связи."
        };

        public string GetRandomFallback()
        {
            if (offlineFallbackMessages == null || offlineFallbackMessages.Length == 0)
                return "Нет связи с сервером.";

            return offlineFallbackMessages[
                Random.Range(0, offlineFallbackMessages.Length)];
        }
    }
}