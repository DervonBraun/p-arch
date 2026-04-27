using Archipelago.Core;
using Archipelago.UI;
using UnityEngine;
using Zenject;

namespace Archipelago.Economy
{
    /// <summary>
    /// Zenject installer для Economy системы.
    /// Добавить на SceneContext в каждой игровой сцене.
    ///
    /// Inspector wiring:
    ///   _config          — EconomyConfig.asset (Assets/Economy/Data/)
    ///   _hudTokenDisplay — HUDTokenDisplay MonoBehaviour на HUD Canvas
    /// </summary>
    public sealed class EconomyInstaller : MonoInstaller
    {
        [Header("Config")]
        [SerializeField] private EconomyConfig _config;

        [Header("Scene References")]
        [SerializeField] private HUDTokenDisplay _hudTokenDisplay;

        public override void InstallBindings()
        {
            if (_config == null)
            {
                Debug.LogError("[EconomyInstaller] EconomyConfig не назначен.");
                return;
            }

            // Config SO — инжектируется в ServerBridge, ScanCostCalculator, ScannerService
            Container.BindInstance(_config).AsSingle();

            // TokenWallet — локальный кэш, передаём размер очереди из конфига
            Container.Bind<TokenWallet>()
                .AsSingle()
                .WithArguments(_config.SyncQueueMaxSize);

            // ServerBridge — HTTP-клиент Railway, зависит от EconomyConfig
            Container.Bind<ServerBridge>()
                .AsSingle();

            // TokenService — фасад экономики.
            // NonLazy: создаётся сразу при старте сцены, не ждёт первого резолва.
            // Это нужно чтобы InitializeAsync() запустился через IInitializable.
            Container.BindInterfacesAndSelfTo<TokenService>()
                .AsSingle()
                .NonLazy();

            // HUD — опционально, предупреждаем если не назначен
            if (_hudTokenDisplay != null)
                Container.Bind<HUDTokenDisplay>()
                    .FromInstance(_hudTokenDisplay)
                    .AsSingle()
                    .NonLazy();
            else
                Debug.LogWarning("[EconomyInstaller] HUDTokenDisplay не назначен — счётчики токенов не будут отображаться.");
        }
    }
}