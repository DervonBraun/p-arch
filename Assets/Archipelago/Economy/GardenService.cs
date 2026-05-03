using System;
using Archipelago.Core;
using Archipelago.Effects;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Economy
{
    /// <summary>
    /// Пассивное накопление зелёных токенов садом.
    ///
    /// Тикается через GameTickMessage (игровое время).
    /// Множитель накопления приходит от GardenHandler через GardenMultiplierChangedMessage.
    ///
    /// Сбор: Collect() → Earn(Green, accumulated) → сброс.
    /// Decay: если накоплено > 0 и не собрано за DecayDelay — обнуляется.
    /// </summary>
    public sealed class GardenService : IInitializable, IDisposable
    {
        // ── State ─────────────────────────────────────────────────

        public int   Accumulated      { get; private set; }
        public float TimeSinceLastAdd { get; private set; }
        public bool  IsDecaying       => TimeSinceLastAdd >= _config.DecayDelay * 0.8f;

        private float _accumBuffer;
        private float _multiplier = 1f;
        private IDisposable _subs;

        // ── Dependencies ──────────────────────────────────────────

        private readonly GardenConfig                                _config;
        private readonly TokenService                                _tokenService;
        private readonly ISubscriber<GameTickMessage>                _tickSub;
        private readonly ISubscriber<GardenMultiplierChangedMessage> _multiplierSub;
        private readonly IPublisher<GardenStateChangedMessage>       _statePub;

        [Inject]
        public GardenService(
            GardenConfig                                config,
            TokenService                                tokenService,
            ISubscriber<GameTickMessage>                tickSub,
            ISubscriber<GardenMultiplierChangedMessage> multiplierSub,
            IPublisher<GardenStateChangedMessage>       statePub)
        {
            _config        = config;
            _tokenService  = tokenService;
            _tickSub       = tickSub;
            _multiplierSub = multiplierSub;
            _statePub      = statePub;
        }

        // ── IInitializable / IDisposable ──────────────────────────

        public void Initialize()
        {
            var bag = DisposableBag.CreateBuilder();
            _tickSub      .Subscribe(OnTick)             .AddTo(bag);
            _multiplierSub.Subscribe(OnMultiplierChanged).AddTo(bag);
            _subs = bag.Build();
        }

        public void Dispose() => _subs?.Dispose();

        // ── Public API ────────────────────────────────────────────

        /// <summary>Собрать накопленные токены. Возвращает количество собранных.</summary>
        public int Collect()
        {
            if (Accumulated <= 0) return 0;

            int amount   = Accumulated;
            Accumulated  = 0;
            _accumBuffer = 0f;
            TimeSinceLastAdd = 0f;

            _tokenService.Earn(TokenType.Green, amount, "garden_collect");
            _statePub.Publish(new GardenStateChangedMessage(0, false));
            return amount;
        }

        // ── Tick ──────────────────────────────────────────────────

        private void OnTick(GameTickMessage msg)
        {
            if (Accumulated >= _config.MaxAccumulated)
            {
                TimeSinceLastAdd += msg.DeltaGameTime;
                CheckDecay();
                return;
            }

            // PERF: float буфер избегает потери токенов при маленьком deltaTime
            _accumBuffer += _config.BaseAccumulationRate * _multiplier * msg.DeltaGameTime;
            int whole     = Mathf.FloorToInt(_accumBuffer);
            _accumBuffer -= whole;

            if (whole > 0)
            {
                Accumulated      = Mathf.Min(Accumulated + whole, _config.MaxAccumulated);
                TimeSinceLastAdd = 0f;
                _statePub.Publish(new GardenStateChangedMessage(Accumulated, IsDecaying));
            }

            TimeSinceLastAdd += msg.DeltaGameTime;
            CheckDecay();
        }

        private void CheckDecay()
        {
            if (Accumulated > 0 && TimeSinceLastAdd >= _config.DecayDelay)
            {
                Debug.Log($"[GardenService] Decay — {Accumulated} green tokens lost.");
                Accumulated      = 0;
                _accumBuffer     = 0f;
                TimeSinceLastAdd = 0f;
                _statePub.Publish(new GardenStateChangedMessage(0, false));
            }
        }

        private void OnMultiplierChanged(GardenMultiplierChangedMessage msg)
        {
            _multiplier = msg.Multiplier;
        }
    }
}
