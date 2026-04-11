using System;
using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Session
{
    /// <summary>
    /// In-game clock. Completely decoupled from real time — pauseable,
    /// acceleratable, and serializable independently of Unity's time system.
    ///
    /// All gameplay systems (EffectService, GardenSystem, RoutineManager)
    /// subscribe to GameTickMessage and use DeltaGameTime — never Time.deltaTime.
    ///
    /// Architecture:
    ///   POCO class managed by Zenject (not a MonoBehaviour).
    ///   Tick() is called by GameLoopRunner (MonoBehaviour ticker) each frame.
    ///   IInitializable → Zenject calls Initialize() after full DI resolution.
    ///   IDisposable    → Zenject calls Dispose() on container teardown.
    ///   ISaveable      → SaveService calls CaptureState/RestoreState.
    /// </summary>
    public sealed class GameClock : IInitializable, IDisposable, ISaveable
    {
        // ── Config ───────────────────────────────────────────────

        /// <summary>In-game seconds per real second at scale 1.0.</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>
        /// Minimum real seconds between GameTickMessage publishes.
        /// 0 = publish every Tick() call (every frame).
        /// </summary>
        public float TickInterval { get; set; } = 0f;

        /// <summary>In-game seconds per full day. Default: 1440 → 24 real min at 1x.</summary>
        public float SecondsPerDay { get; set; } = 1440f;

        // ── State ────────────────────────────────────────────────

        public float TotalGameTime { get; private set; }
        public float DeltaGameTime { get; private set; }
        public int   DayIndex      { get; private set; }
        public bool  IsPaused      { get; private set; }

        // ── Dependencies (constructor-injected by Zenject) ───────

        private readonly IPublisher<GameTickMessage>  _tickPublisher;
        private readonly IPublisher<DayChangedMessage> _dayPublisher;

        // ── Private ──────────────────────────────────────────────

        private float _tickAccumulator;

        // ── Constructor ──────────────────────────────────────────

        /// <summary>
        /// Zenject resolves IPublisher&lt;T&gt; automatically because
        /// ProjectInstaller called Container.BindMessageBroker&lt;T&gt;().
        /// </summary>
        [Inject]
        public GameClock(
            IPublisher<GameTickMessage>   tickPublisher,
            IPublisher<DayChangedMessage> dayPublisher)
        {
            _tickPublisher = tickPublisher;
            _dayPublisher  = dayPublisher;
        }

        // ── IInitializable ───────────────────────────────────────

        public void Initialize()
        {
            // GameClock has no cross-service init requirements.
            // This is intentionally empty — left for future use.
            Debug.Log("[GameClock] Initialized.");
        }

        // ── IDisposable ──────────────────────────────────────────

        public void Dispose()
        {
            // No resources to release. Left intentionally empty.
        }

        // ── Public API ───────────────────────────────────────────

        public void Pause()  => IsPaused = true;
        public void Resume() => IsPaused = false;

        /// <summary>
        /// Advances in-game time. Called every frame by GameLoopRunner.
        /// PERF: O(1), no allocations.
        /// </summary>
        /// <param name="realDeltaTime">Time.deltaTime from the MonoBehaviour ticker.</param>
        public void Tick(float realDeltaTime)
        {
            if (IsPaused) return;

            float gameDelta = realDeltaTime * TimeScale;
            TotalGameTime += gameDelta;
            DeltaGameTime  = gameDelta;

            // Day rollover
            int newDay = Mathf.FloorToInt(TotalGameTime / SecondsPerDay);
            if (newDay != DayIndex)
            {
                DayIndex = newDay;
                _dayPublisher.Publish(new DayChangedMessage(DayIndex));
            }

            // Tick interval gate
            _tickAccumulator += realDeltaTime;
            bool fire = TickInterval <= 0f || _tickAccumulator >= TickInterval;

            if (fire)
            {
                if (TickInterval > 0f)
                    _tickAccumulator -= TickInterval;
                else
                    _tickAccumulator = 0f;

                _tickPublisher.Publish(new GameTickMessage(TotalGameTime, DeltaGameTime, DayIndex));
            }
        }

        // ── ISaveable ────────────────────────────────────────────

        [Serializable]
        public sealed class SaveData
        {
            public float TotalGameTime;
            public int   DayIndex;
            public float TimeScale;
        }

        public object CaptureState() => new SaveData
        {
            TotalGameTime = TotalGameTime,
            DayIndex      = DayIndex,
            TimeScale     = TimeScale
        };

        public void RestoreState(object state)
        {
            if (state is SaveData d)
            {
                TotalGameTime = d.TotalGameTime;
                DayIndex      = d.DayIndex;
                TimeScale     = d.TimeScale;
            }
            else
            {
                Debug.LogError($"[GameClock] RestoreState: unexpected type {state?.GetType().Name}");
            }
        }
    }
}