using UnityEngine;

namespace Archipelago.Economy
{
    /// <summary>
    /// Конфигурация экономики. Один ассет на проект.
    /// Assets/Economy/Data/EconomyConfig.asset
    /// </summary>
    [CreateAssetMenu(
        fileName = "EconomyConfig",
        menuName  = "Archipelago/Economy/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("Server")]
        [Tooltip("Base URL Railway сервера без trailing slash")]
        public string ServerBaseUrl = "https://your-railway-app.railway.app";

        [Tooltip("Player ID для dev-режима. В продакшене заменяется Steam ID.")]
        public string DevPlayerId = "dev_player_01";

        [Header("Network")]
        [Tooltip("Timeout одного HTTP запроса в секундах")]
        public float RequestTimeoutSeconds = 10f;

        [Tooltip("Количество retry попыток при сбое")]
        public int RetryCount = 3;

        [Tooltip("Базовая задержка для exponential backoff (секунды)")]
        public float RetryBaseDelaySeconds = 0.5f;

        [Header("Offline")]
        [Tooltip("Размер очереди отложенной синхронизации")]
        public int SyncQueueMaxSize = 64;

        [Header("Scan Cost (из архдока)")]
        [Tooltip("Стоимость первого запроса в сессии (зелёные)")]
        public int ScanFirstRequestGreenCost = 1000;

        [Tooltip("Стоимость одного прикреплённого объекта (красные)")]
        public int ScanAttachedObjectRedCost = 500;

        [Tooltip("Стоимость одного символа текста (красные). Пробел/Tab не считаются.")]
        public int ScanCharRedCost = 1;

        [Tooltip("Порог символов для Reasoning Mode")]
        public int ReasoningModeThreshold = 200;
    }
}