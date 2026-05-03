using System;
using System.Collections.Generic;
using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.MiniGames
{
    /// <summary>
    /// Управляет жизненным циклом мини-игр.
    /// Регистрирует реализации через RegisterGame().
    /// Тикает активную игру через ITickable (каждый кадр, реальное время).
    /// </summary>
    public sealed class MiniGameManager : IInitializable, IDisposable, ITickable
    {
        // ── State ─────────────────────────────────────────────────

        public bool      IsPlaying  { get; private set; }
        public IMiniGame ActiveGame { get; private set; }

        private readonly Dictionary<string, IMiniGame> _games = new();

        // ── Dependencies ──────────────────────────────────────────

        private readonly IPublisher<MiniGameStartedMessage>   _startedPub;
        private readonly IPublisher<MiniGameCompletedMessage> _completedPub;

        [Inject]
        public MiniGameManager(
            IPublisher<MiniGameStartedMessage>   startedPub,
            IPublisher<MiniGameCompletedMessage> completedPub)
        {
            _startedPub   = startedPub;
            _completedPub = completedPub;
        }

        public void Initialize() { }
        public void Dispose()    { }

        // ── Registration ──────────────────────────────────────────

        public void RegisterGame(IMiniGame game)
        {
            if (_games.ContainsKey(game.MiniGameId))
                Debug.LogWarning($"[MiniGameManager] Replacing game '{game.MiniGameId}'.");
            _games[game.MiniGameId] = game;
        }

        // ── Public API ────────────────────────────────────────────

        public void StartGame(string miniGameId)
        {
            if (IsPlaying)
            {
                Debug.LogWarning("[MiniGameManager] Already playing.");
                return;
            }
            if (!_games.TryGetValue(miniGameId, out var game))
            {
                Debug.LogError($"[MiniGameManager] Game '{miniGameId}' not registered.");
                return;
            }

            ActiveGame = game;
            IsPlaying  = true;
            game.Initialize();
            game.Begin();
            _startedPub.Publish(new MiniGameStartedMessage(miniGameId));
        }

        /// <summary>Вызывается самой мини-игрой когда она завершена.</summary>
        public void CompleteGame(string miniGameId, bool success, float quality)
        {
            if (ActiveGame?.MiniGameId != miniGameId) return;

            ActiveGame.End();
            ActiveGame = null;
            IsPlaying  = false;
            _completedPub.Publish(new MiniGameCompletedMessage(miniGameId, success, quality));
        }

        // ITickable — тикает активную игру каждый кадр
        public void Tick()
        {
            if (IsPlaying && ActiveGame != null)
                ActiveGame.Tick(UnityEngine.Time.deltaTime);
        }
    }
}
