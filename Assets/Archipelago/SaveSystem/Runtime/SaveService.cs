using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Archipelago.Core;
using Archipelago.Economy;
using Cysharp.Threading.Tasks;
using MessagePipe;
using Newtonsoft.Json;
using UnityEngine;
using Zenject;

namespace Archipelago.SaveSystem
{
    /// <summary>
    /// Оркестратор системы сохранений.
    ///
    /// Поток сохранения:
    ///   1. Проверить баланс токенов (стоимость из SaveConfig)
    ///   2. Списать токены (Spend)
    ///   3. Собрать SaveData у всех ISaveable
    ///   4. JSON сериализация → AES-256 шифрование → запись .dat
    ///   5. Записать .meta (незашифрованный)
    ///   6. Опубликовать SaveCompletedMessage
    ///
    /// Поток загрузки:
    ///   1. Прочитать .dat → расшифровать → десериализовать
    ///   2. Раздать SaveData всем ISaveable
    ///   3. Опубликовать SaveLoadedMessage
    ///
    /// LIMITATION: SaveData собирается на main thread — не используй
    /// ISaveable.OnSave для тяжёлых вычислений.
    /// </summary>
    public sealed class SaveService
    {
        public const int SlotCount    = 3;
        public const int SaveCostBlue = 10;   // TODO: вынести в SaveConfig SO

        // ── Dependencies ──────────────────────────────────────────

        private readonly TokenService                        _tokenService;
        private readonly List<ISaveable>                     _saveables;
        private readonly IPublisher<SaveCompletedMessage>    _completedPub;
        private readonly IPublisher<SaveDeniedMessage>       _deniedPub;
        private readonly IPublisher<SaveLoadedMessage>       _loadedPub;

        [Inject]
        public SaveService(
            TokenService                     tokenService,
            List<ISaveable>                  saveables,
            IPublisher<SaveCompletedMessage> completedPub,
            IPublisher<SaveDeniedMessage>    deniedPub,
            IPublisher<SaveLoadedMessage>    loadedPub)
        {
            _tokenService = tokenService;
            _saveables    = saveables;
            _completedPub = completedPub;
            _deniedPub    = deniedPub;
            _loadedPub    = loadedPub;
        }

        // ── Public API ────────────────────────────────────────────

        /// <summary>
        /// Сохранить игру в указанный слот.
        /// Возвращает false если недостаточно токенов.
        /// </summary>
        public async UniTask<bool> SaveAsync(int slot, CancellationToken ct = default)
        {
            ValidateSlot(slot);

            // Проверяем стоимость
            if (!_tokenService.CanAfford(TokenType.Blue, SaveCostBlue))
            {
                int current = _tokenService.Balance.Blue;
                _deniedPub.Publish(new SaveDeniedMessage(SaveCostBlue, current));
                Debug.LogWarning($"[SaveService] Save denied: need {SaveCostBlue} blue, have {current}.");
                return false;
            }

            // Списываем токены
            _tokenService.Spend(TokenType.Blue, SaveCostBlue, "save");

            // Собираем данные
            var data = CollectSaveData();

            // Пишем на диск в фоне
            await WriteSlotAsync(slot, data, ct);

            _completedPub.Publish(new SaveCompletedMessage(slot));
            Debug.Log($"[SaveService] Saved to slot {slot}.");
            return true;
        }

        /// <summary>Загрузить игру из указанного слота.</summary>
        public async UniTask LoadAsync(int slot, CancellationToken ct = default)
        {
            ValidateSlot(slot);

            var data = await ReadSlotAsync(slot, ct);
            if (data == null)
            {
                Debug.LogWarning($"[SaveService] Slot {slot} is empty or corrupted.");
                return;
            }

            foreach (var s in _saveables)
                s.OnLoad(data);

            _loadedPub.Publish(new SaveLoadedMessage(slot));
            Debug.Log($"[SaveService] Loaded from slot {slot}.");
        }

        /// <summary>Начать новую игру — сбросить все системы.</summary>
        public void NewGame()
        {
            foreach (var s in _saveables)
                s.OnReset();
        }

        /// <summary>Прочитать метаданные всех слотов (для UI).</summary>
        public SaveSlotMeta[] ReadAllMeta()
        {
            var metas = new SaveSlotMeta[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                metas[i] = ReadMeta(i) ?? new SaveSlotMeta { SlotIndex = i };
            return metas;
        }

        // ── Internal ──────────────────────────────────────────────

        private SaveData CollectSaveData()
        {
            var data = new SaveData();
            foreach (var s in _saveables)
            {
                try   { s.OnSave(data); }
                catch (Exception ex) 
                { Debug.LogError($"[SaveService] {s.GetType().Name}.OnSave failed: {ex.Message}"); }
            }
            return data;
        }

        private async UniTask WriteSlotAsync(int slot, SaveData data, CancellationToken ct)
        {
            SavePaths.EnsureDir();
            string dataPath = SavePaths.DataPath(slot);
            string metaPath = SavePaths.MetaPath(slot);

            string json      = JsonConvert.SerializeObject(data, Formatting.None);
            // THREAD: GetKey() на main thread
            byte[] key       = SaveCrypto.GetKey();
            byte[] encrypted = SaveCrypto.Encrypt(json, key);

            await UniTask.RunOnThreadPool(() =>
            {
                File.WriteAllBytes(dataPath, encrypted);
                WriteMeta(slot, data, metaPath);
            }, cancellationToken: ct);
        }

        private async UniTask<SaveData> ReadSlotAsync(int slot, CancellationToken ct)
        {
            string path = SavePaths.DataPath(slot);
            if (!File.Exists(path)) return null;

            // THREAD: GetKey() на main thread
            byte[] key = SaveCrypto.GetKey();

            return await UniTask.RunOnThreadPool<SaveData>(() =>
            {
                try
                {
                    byte[] encrypted = File.ReadAllBytes(path);
                    string json      = SaveCrypto.Decrypt(encrypted, key);
                    return JsonConvert.DeserializeObject<SaveData>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveService] Failed to read slot {slot}: {ex.Message}");
                    return null;
                }
            }, cancellationToken: ct);
        }

        private void WriteMeta(int slot, SaveData data, string metaPath = null)
        {
            metaPath ??= SavePaths.MetaPath(slot); // фоллбэк для вызова с main thread
            var meta = new SaveSlotMeta
            {
                SlotIndex  = slot,
                IsEmpty    = false,
                SavedAt    = data.SavedAt,
                GameHours  = data.Session.TotalGameTime / 3600f,
                DayIndex   = data.Session.DayIndex,
                LastRoom   = data.Session.LastRoom,
                BlueTokens = data.Economy.Blue,
            };
            string metaJson = JsonConvert.SerializeObject(meta);
            File.WriteAllText(metaPath, metaJson);
        }

        private SaveSlotMeta ReadMeta(int slot)
        {
            string path = SavePaths.MetaPath(slot);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<SaveSlotMeta>(json);
            }
            catch { return null; }
        }

        private static void ValidateSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slot),
                    $"Slot must be 0–{SlotCount - 1}, got {slot}.");
        }
    }
}
