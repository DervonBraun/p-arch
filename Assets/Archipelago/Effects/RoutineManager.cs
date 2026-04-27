// Assets/Effects/Runtime/RoutineManager.cs
using System;
using System.Collections.Generic;
using System.Threading;
using Archipelago.Core;
using Archipelago.Economy;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Effects
{
    /// <summary>
    /// Исполняет рутинные действия игрока.
    ///
    /// Поток:
    ///   ExecuteRoutine(def) → анимация/таймер → Earn(blue) → Apply(effect)
    ///                       → публикует RoutineCompletedMessage
    ///
    /// Убывание заработка: earned = base / (1 + k * dailyCount)
    /// dailyCount сбрасывается при DayChangedMessage.
    /// </summary>
    public sealed class RoutineManager : IInitializable, IDisposable
    {
        // ── State ─────────────────────────────────────────────────

        // PERF: Dictionary вместо List — O(1) поиск по routineId
        private readonly Dictionary<string, int> _dailyCounts = new();
        private bool     _executing;
        private IDisposable _subs;
        private CancellationTokenSource _cts;

        // ── Dependencies ──────────────────────────────────────────

        private readonly EffectService                          _effectService;
        private readonly TokenService                           _tokenService;
        private readonly ISubscriber<DayChangedMessage>        _daySub;
        private readonly IPublisher<RoutineCompletedMessage>    _completedPub;

        [Inject]
        public RoutineManager(
            EffectService                       effectService,
            TokenService                        tokenService,
            ISubscriber<DayChangedMessage>      daySub,
            IPublisher<RoutineCompletedMessage> completedPub)
        {
            _effectService = effectService;
            _tokenService  = tokenService;
            _daySub        = daySub;
            _completedPub  = completedPub;
        }

        // ── IInitializable / IDisposable ──────────────────────────

        public void Initialize()
        {
            _subs = _daySub.Subscribe(OnDayChanged);
            _cts  = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _subs?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // ── Public API ────────────────────────────────────────────

        /// <summary>
        /// Запускает выполнение рутины. Игнорирует если уже выполняется.
        /// </summary>
        public void ExecuteRoutine(RoutineDefinitionSO definition)
        {
            if (definition == null) return;
            if (_executing)
            {
                Debug.LogWarning("[RoutineManager] Already executing a routine.");
                return;
            }

            ExecuteRoutineAsync(definition, _cts.Token).Forget();
        }

        // ── Internal ─────────────────────────────────────────────

        private async UniTaskVoid ExecuteRoutineAsync(
            RoutineDefinitionSO definition, CancellationToken ct)
        {
            _executing = true;

            try
            {
                // Анимация/пауза (реальное время, не игровое)
                if (definition.actionDurationSeconds > 0f)
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(definition.actionDurationSeconds),
                        cancellationToken: ct);

                // Убывающий заработок
                int earned = CalculateEarned(definition);
                if (earned > 0)
                    _tokenService.Earn(TokenType.Blue, earned, definition.routineId);

                // Применить эффект
                if (definition.appliedEffect != null)
                    _effectService.Apply(definition.appliedEffect);

                // Обновить счётчик дня
                _dailyCounts.TryGetValue(definition.routineId, out int count);
                _dailyCounts[definition.routineId] = count + 1;

                _completedPub.Publish(new RoutineCompletedMessage(definition.routineId, 1f));
            }
            catch (OperationCanceledException) { }
            finally
            {
                _executing = false;
            }
        }

        /// <summary>
        /// Формула убывания: earned = base / (1 + k * dailyCount)
        /// Результат минимум 1 если base > 0.
        /// </summary>
        private int CalculateEarned(RoutineDefinitionSO def)
        {
            _dailyCounts.TryGetValue(def.routineId, out int count);
            float earned = def.baseBlueReward / (1f + def.diminishingK * count);
            return Mathf.Max(1, Mathf.RoundToInt(earned));
        }

        private void OnDayChanged(DayChangedMessage msg)
        {
            _dailyCounts.Clear();
            Debug.Log($"[RoutineManager] Day {msg.NewDayIndex} — diminishing return counters reset.");
        }
    }
}