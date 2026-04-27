using Archipelago.Economy;
using Archipelago.Effects;
using Archipelago.Session;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [Inject] private TokenService   _tokenService;
        [Inject] private SessionManager _sessionManager;
        [Inject] private EffectService  _effectService;   // <-- новый
        [Inject] private SatietyHandler _satietyHandler;  // <-- новый
        [Inject] private CleanHandler   _cleanHandler;    // <-- новый
        [Inject] private GardenHandler  _gardenHandler;   // <-- новый

        private void Start() => BootAsync().Forget();

        private async UniTaskVoid BootAsync()
        {
            if (_tokenService == null || _sessionManager == null || _effectService == null)
            {
                Debug.LogError("[GameBootstrapper] Inject не завершён. " +
                               "Проверь порядок installers в SceneContext.");
                return;
            }

            Debug.Log("[GameBootstrapper] Boot start.");

            await _tokenService.InitializeAsync();
            Debug.Log($"[GameBootstrapper] Token balance loaded: {_tokenService.Balance}");

            // Регистрируем хендлеры эффектов
            _effectService.RegisterHandler(_satietyHandler);
            _effectService.RegisterHandler(_cleanHandler);
            _effectService.RegisterHandler(_gardenHandler);
            Debug.Log("[GameBootstrapper] Effect handlers registered.");

            _sessionManager.Initialize();
            Debug.Log("[GameBootstrapper] Boot complete.");
        }
    }
}