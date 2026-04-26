using Archipelago.Scanner;
using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    public sealed class ScannerInstaller : MonoInstaller
    {
        [Header("Config")]
        [SerializeField] private ScannerConfig _config;

        [Header("Scene References")]
        [SerializeField] private ScannerController   _scannerController;
        [SerializeField] private ScannerUIController _scannerUI;

        public override void InstallBindings()
        {
            if (_config == null)
            {
                Debug.LogError("[ScannerInstaller] ScannerConfig не назначен.");
                return;
            }

            Container.Bind<ScannerConfig>()
                .FromInstance(_config)
                .AsSingle();

            Container.Bind<ScanCache>()
                .AsSingle();

            Container.Bind<GroqClient>()
                .AsSingle();

            Container.BindInterfacesAndSelfTo<ScannerService>()
                .AsSingle();

            // Регистрируем MonoBehaviour-компоненты из сцены
            if (_scannerController != null)
                Container.Bind<ScannerController>()
                    .FromInstance(_scannerController)
                    .AsSingle();
            else
                Debug.LogWarning("[ScannerInstaller] ScannerController не назначен.");

            if (_scannerUI != null)
                Container.QueueForInject(_scannerUI);
            else
                Debug.LogWarning("[ScannerInstaller] ScannerUIController не назначен.");
        }
    }
}