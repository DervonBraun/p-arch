using System;
using System.Collections.Generic;

namespace Archipelago.PlayerProfile
{
    public enum FlagType
    {
        Abuse,
        Curious,
        Technical,
        Silent,
    }

    /// <summary>
    /// Хранит счётчики флагов и состояние блокировки сканера.
    /// Персистируется как часть PlayerProfileData (JSON).
    /// </summary>
    [Serializable]
    public sealed class FlagProfile
    {
        // ── Counts ────────────────────────────────────────────────

        public Dictionary<FlagType, int> Counts = new()
        {
            { FlagType.Abuse,     0 },
            { FlagType.Curious,   0 },
            { FlagType.Technical, 0 },
            { FlagType.Silent,    0 },
        };

        // ── Block state ───────────────────────────────────────────

        /// <summary>До какого момента (UTC) сканер заблокирован. null = не заблокирован.</summary>
        public DateTime? BlockedUntil;

        /// <summary>Когда последний раз применялся decay.</summary>
        public DateTime LastDecayApplied = DateTime.UtcNow;

        // ── Helpers ───────────────────────────────────────────────

        public int Get(FlagType type)
            => Counts.TryGetValue(type, out int v) ? v : 0;

        public void Increment(FlagType type, int amount = 1)
        {
            Counts[type] = Get(type) + amount;
        }

        public void Decrement(FlagType type, int amount = 1)
        {
            Counts[type] = Math.Max(0, Get(type) - amount);
        }

        public bool IsBlocked => BlockedUntil.HasValue && DateTime.UtcNow < BlockedUntil.Value;

        public TimeSpan BlockedRemaining =>
            IsBlocked ? BlockedUntil!.Value - DateTime.UtcNow : TimeSpan.Zero;

        public int AbuseCount => Get(FlagType.Abuse);
    }
}
