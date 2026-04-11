using System;
using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Session
{
    /// <summary>
    /// FSM for a single play session.
    /// Owns all transitions: WakeUp → FreeRoam → MiniGame / Scanning / Routine → End.
    ///
    /// Does NOT own mini-game, scan, or routine logic.
    /// Those systems publish completion events; SessionManager reacts.
    ///
    /// THREAD: All state transitions must happen on the main thread.
    /// </summary>
    public sealed class SessionManager : IInitializable, IDisposable, ISaveable
    {
        // ── State ────────────────────────────────────────────────

        public SessionState CurrentState { get; private set; } = SessionState.None;

        // ── Dependencies ─────────────────────────────────────────

        private readonly IPublisher<SessionStateChangedMessage>  _statePublisher;
        private readonly ISubscriber<MiniGameCompletedMessage>   _miniGameSub;
        private readonly ISubscriber<RoutineCompletedMessage>    _routineSub;

        private IDisposable _subscriptions;

        // ── Constructor ──────────────────────────────────────────

        [Inject]
        public SessionManager(
            IPublisher<SessionStateChangedMessage>  statePublisher,
            ISubscriber<MiniGameCompletedMessage>   miniGameSub,
            ISubscriber<RoutineCompletedMessage>    routineSub)
        {
            _statePublisher = statePublisher;
            _miniGameSub    = miniGameSub;
            _routineSub     = routineSub;
        }

        // ── IInitializable ───────────────────────────────────────

        public void Initialize()
        {
            var bag = DisposableBag.CreateBuilder();

            _miniGameSub.Subscribe(OnMiniGameCompleted).AddTo(bag);
            _routineSub .Subscribe(OnRoutineCompleted) .AddTo(bag);

            _subscriptions = bag.Build();

            TransitionTo(SessionState.WakeUp);
            Debug.Log("[SessionManager] Initialized.");
        }

        // ── IDisposable ──────────────────────────────────────────

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        // ── Public Transitions ───────────────────────────────────

        /// <summary>Call from WakeUp animation sequence when it finishes.</summary>
        public void OnWakeUpComplete()
        {
            if (CurrentState != SessionState.WakeUp) return;
            TransitionTo(SessionState.FreeRoam);
        }

        /// <summary>Call from MiniGameManager before activating a game.</summary>
        public void EnterMiniGame()
        {
            if (CurrentState != SessionState.FreeRoam) return;
            TransitionTo(SessionState.MiniGame);
        }

        /// <summary>Call from ScannerController when scanner UI opens.</summary>
        public void EnterScanning()
        {
            if (CurrentState != SessionState.FreeRoam) return;
            TransitionTo(SessionState.Scanning);
        }

        /// <summary>Call from ScannerController when scanner UI closes.</summary>
        public void ExitScanning()
        {
            if (CurrentState != SessionState.Scanning) return;
            TransitionTo(SessionState.FreeRoam);
        }

        /// <summary>Call from RoutineManager when a routine action starts.</summary>
        public void EnterRoutine()
        {
            if (CurrentState != SessionState.FreeRoam) return;
            TransitionTo(SessionState.Routine);
        }

        /// <summary>Call when all code fragments are collected.</summary>
        public void TriggerCodeComplete()
        {
            TransitionTo(SessionState.CodeComplete);
        }

        // ── Event Handlers ───────────────────────────────────────

        private void OnMiniGameCompleted(MiniGameCompletedMessage msg)
        {
            if (CurrentState == SessionState.MiniGame)
                TransitionTo(SessionState.FreeRoam);
        }

        private void OnRoutineCompleted(RoutineCompletedMessage msg)
        {
            if (CurrentState == SessionState.Routine)
                TransitionTo(SessionState.FreeRoam);
        }

        // ── Internal ─────────────────────────────────────────────

        private void TransitionTo(SessionState next)
        {
            if (CurrentState == next) return;

            var prev = CurrentState;
            CurrentState = next;

            Debug.Log($"[SessionManager] {prev} → {next}");
            _statePublisher.Publish(new SessionStateChangedMessage(prev, next));
        }

        // ── ISaveable ────────────────────────────────────────────

        [Serializable]
        public sealed class SaveData
        {
            public SessionState CurrentState;
        }

        public object CaptureState() => new SaveData { CurrentState = CurrentState };

        public void RestoreState(object state)
        {
            if (state is SaveData d)
                CurrentState = d.CurrentState;
        }
    }
}