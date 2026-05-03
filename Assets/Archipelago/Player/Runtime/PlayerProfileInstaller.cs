using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.PlayerProfile
{
    /// <summary>
    /// Zenject installer для системы профиля и флагов.
    /// Добавить на SceneContext после EconomyInstaller.
    /// </summary>
    public sealed class PlayerProfileInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            // Единственный экземпляр данных профиля — синглтон
            Container.Bind<PlayerProfileData>()
                .AsSingle();

            Container.BindInterfacesAndSelfTo<FlagService>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<PlayerProfileTracker>()
                .AsSingle()
                .NonLazy();
        }
    }
}
