using Archipelago.Economy;
using Archipelago.Scanner;
using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    /// <summary>
    /// Zenject installer for the Scanner system.
    /// Attach to SceneContext (or a child MonoInstaller object) in each gameplay scene.
    ///
    /// Inspector wiring:
    ///   _config              — ScannerConfig ScriptableObject asset
    ///   _circleSearchCtrl    — CircleSearchController MonoBehaviour on Player GO
    ///   _circleSearchOverlay — CircleSearchOverlay MonoBehaviour on HUD Canvas GO
    ///   _scannerUI           — ScannerUIController MonoBehaviour on HUD Canvas GO
    /// </summary>
    public sealed class ScannerInstaller : MonoInstaller
    {
        [Header("Config")]
        [SerializeField] private ScannerConfig _config;

        [Header("Scene References")]
        [SerializeField] private CircleSearchController _circleSearchCtrl;
        [SerializeField] private CircleSearchOverlay    _circleSearchOverlay;
        [SerializeField] private ScannerUIController    _scannerUI;

        public override void InstallBindings()
        {
            if (_config == null)
            {
                Debug.LogError("[ScannerInstaller] ScannerConfig not assigned.");
                return;
            }

            // ── Configs & POCO services ───────────────────────────

            Container.Bind<ScannerConfig>()
                .FromInstance(_config)
                .AsSingle();

            Container.Bind<ScanCache>()
                .AsSingle();

            Container.Bind<GroqClient>()
                .AsSingle();

            Container.Bind<ScanCollection>()
                .AsSingle();

            Container.BindInterfacesAndSelfTo<ScannerService>()
                .AsSingle();

            // ── MonoBehaviour scene objects ───────────────────────

            if (_circleSearchCtrl != null)
                Container.Bind<CircleSearchController>()
                    .FromInstance(_circleSearchCtrl)
                    .AsSingle();
            else
                Debug.LogWarning("[ScannerInstaller] CircleSearchController not assigned.");

            if (_circleSearchOverlay != null)
                Container.Bind<CircleSearchOverlay>()
                    .FromInstance(_circleSearchOverlay)
                    .AsSingle();
            else
                Debug.LogWarning("[ScannerInstaller] CircleSearchOverlay not assigned.");

            if (_scannerUI != null)
                Container.Bind<ScannerUIController>()
                    .FromInstance(_scannerUI)
                    .AsSingle()
                    .NonLazy();
            else
                Debug.LogWarning("[ScannerInstaller] ScannerUIController not assigned.");
        }
    }
}
