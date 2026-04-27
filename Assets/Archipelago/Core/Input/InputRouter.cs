using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Session
{
    /// <summary>
    /// Переключает активный Input Map в зависимости от SessionState.
    /// Единственное место где принимается решение какой map активен.
    ///
    /// FreeRoam / любой не-Scanning стейт → Gameplay map
    /// Scanning (панель открыта)           → Scanner map
    ///
    /// Attach на Player GameObject рядом с CircleSearchController.
    /// THREAD: main thread only (MessagePipe доставляет на main thread).
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        [Inject] private InputReader                             _inputReader;
        [Inject] private ISubscriber<SessionStateChangedMessage> _sessionSub;

        private System.IDisposable _subscription;

        // ── Unity Lifecycle ──────────────────────────────────────

        private void Start()
        {
            _subscription = _sessionSub.Subscribe(OnSessionStateChanged);

            // Стартовое состояние — Gameplay map уже включён в InputReader.OnEnable,
            // но явно подтверждаем на случай если стейт уже FreeRoam к этому моменту.
            _inputReader.EnableGameplayMap();
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }

        // ── Handler ───────────────────────────────────────────────

        private void OnSessionStateChanged(SessionStateChangedMessage msg)
        {
            switch (msg.Next)
            {
                case SessionState.Scanning:
                    _inputReader.EnableScannerMap();
                    break;

                default:
                    _inputReader.EnableGameplayMap();
                    break;
            }
        }
    }
}