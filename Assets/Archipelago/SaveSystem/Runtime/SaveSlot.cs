using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Archipelago.SaveSystem
{
    /// <summary>
    /// Метаданные одного слота сохранения.
    /// Хранятся в отдельном незашифрованном .meta файле рядом с .dat —
    /// чтобы показать список слотов без расшифровки основного файла.
    /// </summary>
    [Serializable]
    public sealed class SaveSlotMeta
    {
        public int      SlotIndex;
        public bool     IsEmpty    = true;
        public DateTime SavedAt    = DateTime.MinValue;
        public float    GameHours  = 0f;
        public int      DayIndex   = 0;
        public string   LastRoom   = "";
        public int      BlueTokens = 0;

        public string DisplayTime =>
            IsEmpty ? "Пусто" : SavedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        public string DisplayProgress =>
            IsEmpty ? "" : $"День {DayIndex + 1} · {GameHours:F1}ч · {BlueTokens} 🔵";
    }

    /// <summary>
    /// Пути файлов для одного слота.
    /// </summary>
    public static class SavePaths
    {
        private static string SaveDir =>
            Path.Combine(Application.persistentDataPath, "saves");

        public static string DataPath(int slot) =>
            Path.Combine(SaveDir, $"slot{slot}.dat");

        public static string MetaPath(int slot) =>
            Path.Combine(SaveDir, $"slot{slot}.meta");

        public static void EnsureDir() =>
            Directory.CreateDirectory(SaveDir);
    }
}
