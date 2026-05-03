using Archipelago.Economy;
using Archipelago.Effects;
using Archipelago.MiniGames;
using Archipelago.Session;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    /// <summary>
    /// Точка входа. Запускается первым (Script Execution Order: -100).
    ///
    /// Порядок инициализации:
    ///   1. TokenService.InitializeAsync  — загрузка баланса с сервера
    ///   2. EffectService handler registration
    ///   3. MiniGameManager game registration
    ///   4. SessionManager.Initialize     — старт FSM
    ///
    /// THREAD: всё на main thread.
    /// </summary>
    public sealed class GameBootstrapper : MonoBehaviour
    {
        // ── Injected ──────────────────────────────────────────────

        [Inject] private TokenService    _tokenService;
        [Inject] private SessionManager  _sessionManager;

        // Effects
        [Inject] private EffectService   _effectService;
        [Inject] private SatietyHandler  _satietyHandler;
        [Inject] private CleanHandler    _cleanHandler;
        [Inject] private GardenHandler   _gardenHandler;

        // Mini-games
        [Inject] private MiniGameManager _miniGameManager;
        [Inject] private CalibrationGame _calibrationGame;
        [Inject] private WireRepairGame  _wireRepairGame;
        [Inject] private MonitoringGame  _monitoringGame;

        // ── Unity ─────────────────────────────────────────────────

        private void Start() => BootAsync().Forget();

        // ── Boot sequence ─────────────────────────────────────────

        private async UniTaskVoid BootAsync()
        {
            if (_tokenService == null || _sessionManager == null || _effectService == null)
            {
                Debug.LogError("[GameBootstrapper] Inject не завершён. " +
                               "Проверь порядок installers в SceneContext.");
                return;
            }

            Debug.Log("[GameBootstrapper] Boot start.");

            // 1. Баланс токенов
            await _tokenService.InitializeAsync();
            Debug.Log($"[GameBootstrapper] Token balance loaded: {_tokenService.Balance}");

            // 2. Хендлеры эффектов
            _effectService.RegisterHandler(_satietyHandler);
            _effectService.RegisterHandler(_cleanHandler);
            _effectService.RegisterHandler(_gardenHandler);
            Debug.Log("[GameBootstrapper] Effect handlers registered.");

            // 3. Мини-игры
            _miniGameManager.RegisterGame(_calibrationGame);
            _miniGameManager.RegisterGame(_wireRepairGame);
            _miniGameManager.RegisterGame(_monitoringGame);
            Debug.Log("[GameBootstrapper] Mini-games registered.");

            // 4. FSM сессии — последним, он публикует первый SessionStateChangedMessage
            _sessionManager.Initialize();
            Debug.Log("[GameBootstrapper] Boot complete.");
        }
    }
}
