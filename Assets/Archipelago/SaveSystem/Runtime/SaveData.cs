using System;
using System.Collections.Generic;
using Archipelago.Core;
using Archipelago.PlayerProfile;

namespace Archipelago.SaveSystem
{
    /// <summary>
    /// Корневой контейнер сохранения. Сериализуется в JSON → шифруется AES-256.
    /// Каждая система реализует ISaveable и заполняет свой раздел.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int      Version   = 1;
        public DateTime SavedAt   = DateTime.UtcNow;
        public string   PlayerId  = "dev_player_01";

        // ── Session ───────────────────────────────────────────────
        public SessionSaveData   Session   = new();

        // ── Economy ───────────────────────────────────────────────
        public EconomySaveData   Economy   = new();

        // ── Effects ──────────────────────────────────────────────
        public EffectsSaveData   Effects   = new();

        // ── Player Profile ────────────────────────────────────────
        public PlayerProfileData Profile   = new();

        // ── World ─────────────────────────────────────────────────
        public WorldSaveData     World     = new();
    }

    [Serializable]
    public sealed class SessionSaveData
    {
        public float  TotalGameTime = 0f;
        public int    DayIndex      = 0;
        public string LastRoom      = "hub";
        public float  PlayerX, PlayerY, PlayerZ;
        public float  CameraYaw, CameraPitch;
    }

    [Serializable]
    public sealed class EconomySaveData
    {
        public int Red   = 0;
        public int Green = 0;
        public int Blue  = 0;
    }

    [Serializable]
    public sealed class EffectsSaveData
    {
        public List<ActiveEffectSaveData> ActiveEffects = new();
    }

    [Serializable]
    public sealed class ActiveEffectSaveData
    {
        public string EffectId;
        public int    CurrentStacks;
        public float  RemainingTime;
    }

    [Serializable]
    public sealed class WorldSaveData
    {
        /// <summary>Произвольные флаги состояния объектов мира. Ключ: objectId.</summary>
        public Dictionary<string, bool>   ObjectFlags  = new();
        /// <summary>Архив сканирований. Ключ: objectId, значение: последний ответ.</summary>
        public Dictionary<string, string> ScanArchive  = new();
        /// <summary>Прогресс к финалу (0–1).</summary>
        public float CodeProgress = 0f;
    }
}
