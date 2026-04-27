using Archipelago.Economy;
using Archipelago.Session;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    /// <summary>
    /// Точка входа. Запускается первым (Script Execution Order: -100).
    /// Порядок инициализации:
    ///   1. TokenService.InitializeAsync  — загрузка баланса с сервера
    ///   2. SessionManager.Initialize     — старт FSM
    ///
    /// THREAD: всё на main thread.
    /// </summary>
    public sealed class GameBootstrapper : MonoBehaviour
    {
        // ── Injected ──────────────────────────────────────────────

        [Inject] private TokenService   _tokenService;
        [Inject] private SessionManager _sessionManager;

        // ── Unity ─────────────────────────────────────────────────

        private void Start()
        {
            BootAsync().Forget();
        }

        // ── Boot sequence ─────────────────────────────────────────

        private async UniTaskVoid BootAsync()
        {
            
            // LIMITATION: если Zenject не завершил inject — сервисы null.
            if (_tokenService == null || _sessionManager == null)
            {
                Debug.LogError("[GameBootstrapper] Inject не завершён. " +
                               "Проверь порядок installers в SceneContext.");
                return;
            }
            Debug.Log("[GameBootstrapper] Boot start.");

            // 1. Загружаем баланс токенов с сервера.
            //    TokenService сам обработает offline-кейс (баланс = 0, pending queue).
            await _tokenService.InitializeAsync();

            Debug.Log($"[GameBootstrapper] Token balance loaded: {_tokenService.Balance}");

            // 2. Стартуем FSM сессии.
            //    SessionManager.Initialize() публикует первый SessionStateChangedMessage,
            //    на который подписаны InputRouter, ScannerUIController и т.д.
            _sessionManager.Initialize();

            Debug.Log("[GameBootstrapper] Boot complete.");
        }
    }
}