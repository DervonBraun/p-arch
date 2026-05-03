using UnityEngine;
using Zenject;

namespace Archipelago.MiniGames
{
    /// <summary>
    /// Zenject installer для системы мини-игр.
    /// Добавить на SceneContext вместе с остальными installers.
    /// </summary>
    public sealed class MiniGamesInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<MiniGameManager>()
                .AsSingle()
                .NonLazy();

            Container.Bind<CalibrationGame>().AsSingle();
            Container.Bind<WireRepairGame>().AsSingle();
            Container.Bind<MonitoringGame>().AsSingle();
        }
    }
}
