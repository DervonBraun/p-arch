using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Economy
{
    /// <summary>
    /// Zenject installer для Economy системы.
    /// Добавить на GameContext или SceneContext в сцене.
    /// 
    /// EconomyConfig назначается через Inspector.
    /// </summary>
    public sealed class EconomyInstaller : MonoInstaller
    {
        [SerializeField] private EconomyConfig _config;

        public override void InstallBindings()
        {
            if (_config == null)
            {
                Debug.LogError("[EconomyInstaller] EconomyConfig не назначен!");
                return;
            }

            Container.Bind<EconomyConfig>()
                .FromInstance(_config)
                .AsSingle();

            Container.Bind<ServerBridge>()
                .AsSingle();

            Container.Bind<TokenService>()
                .AsSingle()
                .NonLazy(); // инициализируем сразу, не по запросу
        }
    }
}