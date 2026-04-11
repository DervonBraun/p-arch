using Archipelago.Scanner;
using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    /// <summary>
    /// Zenject installer для Scanner-системы.
    /// Добавить в список Mono Installers на SceneContext рядом с SceneInstaller.
    ///
    /// Требует ScannerConfig SO — создать через
    ///   Assets → Create → Archipelago → Scanner → ScannerConfig
    /// и назначить в поле Config в инспекторе.
    /// </summary>
    public sealed class ScannerInstaller : MonoInstaller
    {
        [Header("Config")]
        [SerializeField] private ScannerConfig _config;

        public override void InstallBindings()
        {
            if (_config == null)
            {
                Debug.LogError("[ScannerInstaller] ScannerConfig not assigned. " +
                               "Create one via Assets → Create → Archipelago → Scanner → ScannerConfig");
                return;
            }

            // Config SO — синглтон из инспектора
            Container.Bind<ScannerConfig>()
                .FromInstance(_config)
                .AsSingle();

            // ScanCache — POCO, создаётся один раз, читает/пишет файл сам
            Container.Bind<ScanCache>()
                .AsSingle();

            // GroqClient — POCO, зависит от ScannerConfig
            Container.Bind<GroqClient>()
                .AsSingle();

            // ScannerService — IInitializable + IDisposable
            Container.BindInterfacesAndSelfTo<ScannerService>()
                .AsSingle();
        }
    }
}