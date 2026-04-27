// Assets/Effects/Runtime/EffectService.cs
using System;
using System.Collections.Generic;
using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Effects
{
    /// <summary>
    /// Управляет активными эффектами. Тикает через GameTickMessage.
    /// Хендлеры регистрируются извне (через installer) — EffectService
    /// не знает о конкретных типах эффектов.
    ///
    /// Apply(definitionSO):
    ///   — если эффект не активен → создаёт ActiveEffect, вызывает OnApply
    ///   — если активен → AddStack(), вызывает OnStack или просто продлевает таймер
    /// </summary>
    public sealed class EffectService : IInitializable, IDisposable
    {
        // ── State ─────────────────────────────────────────────────

        // PERF: Dictionary по string effectId, не List — O(1) lookup
        private readonly Dictionary<string, ActiveEffect>    _active   = new();
        private readonly Dictionary<string, IEffectHandler>  _handlers = new();

        private IDisposable _tickSub;

        // ── Dependencies ──────────────────────────────────────────

        private readonly ISubscriber<GameTickMessage>      _tickSub2;
        private readonly IPublisher<EffectAppliedMessage>  _appliedPub;
        private readonly IPublisher<EffectExpiredMessage>  _expiredPub;

        [Inject]
        public EffectService(
            ISubscriber<GameTickMessage>     tickSub,
            IPublisher<EffectAppliedMessage> appliedPub,
            IPublisher<EffectExpiredMessage> expiredPub)
        {
            _tickSub2   = tickSub;
            _appliedPub = appliedPub;
            _expiredPub = expiredPub;
        }

        // ── IInitializable ────────────────────────────────────────

        public void Initialize()
        {
            _tickSub = _tickSub2.Subscribe(OnGameTick);
        }

        public void Dispose()
        {
            _tickSub?.Dispose();
        }

        // ── Handler registration ──────────────────────────────────

        /// <summary>Регистрирует хендлер. Вызывается из EffectsInstaller.</summary>
        public void RegisterHandler(IEffectHandler handler)
        {
            if (_handlers.ContainsKey(handler.EffectId))
            {
                Debug.LogWarning($"[EffectService] Handler already registered for '{handler.EffectId}', replacing.");
            }
            _handlers[handler.EffectId] = handler;
        }

        // ── Public API ────────────────────────────────────────────

        /// <summary>
        /// Применить эффект. Если уже активен — стакнуть или продлить.
        /// </summary>
        public void Apply(EffectDefinitionSO definition)
        {
            if (definition == null)
            {
                Debug.LogWarning("[EffectService] Apply called with null definition.");
                return;
            }

            string id = definition.effectId;

            if (_active.TryGetValue(id, out var existing))
            {
                bool stacked = existing.AddStack();
                if (_handlers.TryGetValue(id, out var h))
                    h.OnStack(existing);

                _appliedPub.Publish(new EffectAppliedMessage(id, existing.CurrentStacks, existing.RemainingTime));
            }
            else
            {
                var effect = new ActiveEffect(definition);
                _active[id] = effect;

                if (_handlers.TryGetValue(id, out var h))
                    h.OnApply(effect);

                _appliedPub.Publish(new EffectAppliedMessage(id, effect.CurrentStacks, effect.RemainingTime));
            }
        }

        /// <summary>Проверить активен ли эффект.</summary>
        public bool IsActive(string effectId) => _active.ContainsKey(effectId);

        /// <summary>Получить активный эффект (null если не активен).</summary>
        public ActiveEffect Get(string effectId)
            => _active.TryGetValue(effectId, out var e) ? e : null;

        // ── Tick ──────────────────────────────────────────────────

        // PERF: итерация по словарю с удалением через список — избегаем
        // InvalidOperationException при модификации коллекции во время итерации
        private readonly List<string> _toExpire = new();

        private void OnGameTick(GameTickMessage msg)
        {
            _toExpire.Clear();

            foreach (var (id, effect) in _active)
            {
                if (_handlers.TryGetValue(id, out var h))
                    h.OnTick(effect, msg.DeltaGameTime);

                effect.Tick(msg.DeltaGameTime);

                if (effect.IsExpired)
                    _toExpire.Add(id);
            }

            foreach (var id in _toExpire)
            {
                var effect = _active[id];
                _active.Remove(id);

                if (_handlers.TryGetValue(id, out var h))
                    h.OnExpire(effect);

                _expiredPub.Publish(new EffectExpiredMessage(id));
            }
        }
    }
}