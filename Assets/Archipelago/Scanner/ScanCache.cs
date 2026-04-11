using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Локальный JSON-кэш первичных ответов сканера.
    /// Файл: Application.persistentDataPath/scan_cache.json
    ///
    /// Кэшируется только ПЕРВЫЙ ответ на каждый objectId.
    /// Follow-up вопросы не кэшируются — у них уникальный контекст.
    /// Используется как fallback при недоступности сервера.
    ///
    /// THREAD: Read/Write только с main thread.
    /// </summary>
    public sealed class ScanCache
    {
        private readonly string _filePath;

        // Ключ: objectId. Значение: первый ответ AI.
        private Dictionary<string, string> _cache = new();

        private bool _isDirty;

        public ScanCache()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "scan_cache.json");
            Load();
        }

        // ── Public API ───────────────────────────────────────────

        /// <summary>
        /// Возвращает кэшированный ответ или null если не найден.
        /// </summary>
        public string Get(string objectId)
        {
            _cache.TryGetValue(objectId, out var result);
            return result;
        }

        /// <summary>
        /// Сохраняет первичный ответ в кэш.
        /// Вызывать только для первого скана (не follow-up).
        /// </summary>
        public void Set(string objectId, string response)
        {
            if (_cache.TryGetValue(objectId, out var existing) && existing == response)
                return;

            _cache[objectId] = response;
            _isDirty = true;
            Save();
        }

        public bool HasEntry(string objectId) => _cache.ContainsKey(objectId);

        // ── Persistence ──────────────────────────────────────────

        private void Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return;

                var json = File.ReadAllText(_filePath);
                _cache = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                         ?? new Dictionary<string, string>();

                Debug.Log($"[ScanCache] Loaded {_cache.Count} entries from {_filePath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ScanCache] Failed to load cache: {e.Message}. Starting fresh.");
                _cache = new Dictionary<string, string>();
            }
        }

        private void Save()
        {
            if (!_isDirty) return;

            try
            {
                var json = JsonConvert.SerializeObject(_cache, Formatting.Indented);
                File.WriteAllText(_filePath, json);
                _isDirty = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ScanCache] Failed to save cache: {e.Message}");
            }
        }
    }
}