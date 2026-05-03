using System;
using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.PlayerProfile
{
    /// <summary>
    /// Управляет флагами поведения игрока.
    ///
    /// ApplyFlags(flags[])  — применяет флаги из ответа сервера
    /// CheckBlocked()       — проверяет блокировку сканера
    /// GetToneModifier()    — возвращает текущий тон для промпта
    /// Decay                — срабатывает при DayChangedMessage
    ///
    /// ABUSE прогрессия:
    ///   1 → замечание в ответе (тон: cold)
    ///   2 → системное предупреждение (тон: warning)
    ///   3+ → блокировка сканера на 10 минут реального времени
    /// </summary>
    public sealed class FlagService : IInitializable, IDisposable
    {
        private const int   BlockThreshold      = 3;
        private const float BlockDurationMinutes = 10f;

        // ── State ─────────────────────────────────────────────────

        private readonly PlayerProfileData _profile;
        private FlagProfile Flags => _profile.Flags;

        private IDisposable _daySub;

        // ── Dependencies ──────────────────────────────────────────

        private readonly ISubscriber<DayChangedMessage>    _daySub2;
        private readonly IPublisher<ScannerBlockedMessage> _blockedPub;
        private readonly IPublisher<FlagsUpdatedMessage>   _flagsPub;

        [Inject]
        public FlagService(
            PlayerProfileData                  profile,
            ISubscriber<DayChangedMessage>     daySub,
            IPublisher<ScannerBlockedMessage>  blockedPub,
            IPublisher<FlagsUpdatedMessage>    flagsPub)
        {
            _profile    = profile;
            _daySub2    = daySub;
            _blockedPub = blockedPub;
            _flagsPub   = flagsPub;
        }

        public void Initialize() => _daySub = _daySub2.Subscribe(OnDayChanged);
        public void Dispose()    => _daySub?.Dispose();

        // ── Public API ────────────────────────────────────────────

        /// <summary>
        /// Применяет флаги из JSON-ответа сервера (["ABUSE", "CURIOUS"] и т.д.)
        /// </summary>
        public void ApplyFlags(string[] flagNames)
        {
            if (flagNames == null || flagNames.Length == 0) return;

            foreach (var name in flagNames)
            {
                if (!TryParseFlag(name, out var type)) continue;

                // CURIOUS/TECHNICAL/SILENT пересчитываются из метрик, не накапливаются
                if (type != FlagType.Abuse) continue;

                Flags.Increment(FlagType.Abuse);
                Debug.Log($"[FlagService] ABUSE flag applied. Total: {Flags.AbuseCount}");

                if (Flags.AbuseCount >= BlockThreshold && !Flags.IsBlocked)
                    ApplyBlock();
            }

            RefreshBehaviorFlags();
            _flagsPub.Publish(new FlagsUpdatedMessage(Flags));
        }

        /// <summary>
        /// Пересчитывает CURIOUS/TECHNICAL/SILENT из текущих метрик.
        /// Вызывается перед каждым запросом к Groq.
        /// </summary>
        public void RefreshBehaviorFlags()
        {
            var m = _profile.Metrics;

            Flags.Counts[FlagType.Curious]   = m.AnomalyInterest > 0.5f  ? 1 : 0;
            Flags.Counts[FlagType.Technical] = m.QueryDetailRatio > 0.6f ? 1 : 0;
            Flags.Counts[FlagType.Silent]    = m.QueryDetailRatio < 0.2f && m.TotalScans > 5 ? 1 : 0;
        }

        public bool IsBlocked => Flags.IsBlocked;

        public TimeSpan BlockedRemaining => Flags.BlockedRemaining;

        /// <summary>Возвращает строку тона для инжекции в system[1] промпта.</summary>
        public string GetTone()
        {
            int abuse = Flags.AbuseCount;
            return abuse switch
            {
                0 => "neutral",
                1 => "cold",
                2 => "warning",
                _ => "blocked",
            };
        }

        /// <summary>Строит JSON-блок для инжекции в system[1].</summary>
        public string BuildFlagInjection()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                abuseCount   = Flags.AbuseCount,
                curiousFlag  = Flags.Get(FlagType.Curious)   > 0,
                technicalFlag= Flags.Get(FlagType.Technical) > 0,
                silentFlag   = Flags.Get(FlagType.Silent)    > 0,
                currentTone  = GetTone(),
                blockedUntil = Flags.IsBlocked ? Flags.BlockedUntil?.ToString("o") : null,
            });
        }

        // ── Block ─────────────────────────────────────────────────

        private void ApplyBlock()
        {
            Flags.BlockedUntil = DateTime.UtcNow.AddMinutes(BlockDurationMinutes);
            Debug.LogWarning($"[FlagService] Scanner blocked until {Flags.BlockedUntil}");
            _blockedPub.Publish(new ScannerBlockedMessage(Flags.BlockedUntil.Value));
        }

        // ── Decay ─────────────────────────────────────────────────

        private void OnDayChanged(DayChangedMessage msg)
        {
            // ABUSE: -1 в день, минимум 0
            if (Flags.AbuseCount > 0)
            {
                Flags.Decrement(FlagType.Abuse);
                Debug.Log($"[FlagService] ABUSE decay. Remaining: {Flags.AbuseCount}");
            }

            Flags.LastDecayApplied = DateTime.UtcNow;
            RefreshBehaviorFlags();
            _flagsPub.Publish(new FlagsUpdatedMessage(Flags));
        }

        // ── Helpers ───────────────────────────────────────────────

        private static bool TryParseFlag(string name, out FlagType type)
        {
            type = name?.ToUpperInvariant() switch
            {
                "ABUSE"     => FlagType.Abuse,
                "CURIOUS"   => FlagType.Curious,
                "TECHNICAL" => FlagType.Technical,
                "SILENT"    => FlagType.Silent,
                _           => (FlagType)(-1),
            };
            return (int)type >= 0;
        }
    }
}
