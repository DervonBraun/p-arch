// Assets/Effects/Runtime/EffectsInstaller.cs
using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Effects
{
    /// <summary>
    /// Zenject installer для системы эффектов и рутины.
    /// Добавить на SceneContext вместе с SceneInstaller и EconomyInstaller.
    /// </summary>
    public sealed class EffectsInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            // ── Messages ──────────────────────────────────────────
            // Регистрируем здесь, а не в SceneInstaller, чтобы не раздувать его
            // ВАЖНО: если SceneInstaller уже регистрирует эти brokers — убрать отсюда

            // ── Handlers ──────────────────────────────────────────
            Container.Bind<SatietyHandler>().AsSingle();
            Container.Bind<CleanHandler>().AsSingle();
            Container.Bind<GardenHandler>().AsSingle();

            // ── Core services ─────────────────────────────────────
            Container.BindInterfacesAndSelfTo<EffectService>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<RoutineManager>()
                .AsSingle()
                .NonLazy();
        }
    }
}