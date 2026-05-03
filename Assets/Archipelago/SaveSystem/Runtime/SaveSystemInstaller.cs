using System.Collections.Generic;
using Archipelago.SaveSystem;
using UnityEngine;
using Zenject;

namespace Archipelago.SaveSystem
{
    /// <summary>
    /// Zenject installer для Save System.
    /// Добавить на SceneContext последним (зависит от всех остальных систем).
    ///
    /// Inspector wiring:
    ///   _effectLibrary — EffectDefinitionLibrary.asset
    /// </summary>
    public sealed class SaveSystemInstaller : MonoInstaller
    {
        [Header("Data")]
        [SerializeField] private EffectDefinitionLibrary _effectLibrary;

        public override void InstallBindings()
        {
            if (_effectLibrary != null)
                Container.BindInstance(_effectLibrary).AsSingle();
            else
                Debug.LogWarning("[SaveSystemInstaller] EffectDefinitionLibrary не назначена.");

            // ISaveable реализации — все в один список
            Container.Bind<ISaveable>().To<SessionSaveable>() .AsSingle();
            Container.Bind<ISaveable>().To<EconomySaveable>() .AsSingle();
            Container.Bind<ISaveable>().To<EffectsSaveable>() .AsSingle();
            Container.Bind<ISaveable>().To<ProfileSaveable>() .AsSingle();

            // Список всех ISaveable — инжектируется в SaveService
            Container.Bind<List<ISaveable>>()
                .FromMethod(ctx => ctx.Container.ResolveAll<ISaveable>())
                .AsSingle();

            // Сам сервис
            Container.Bind<SaveService>()
                .AsSingle()
                .NonLazy();
        }
    }
}
