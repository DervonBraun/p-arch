using Archipelago.Player;
using Archipelago.Session;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    /// <summary>
    /// Scene-level installer. Attach to SceneContext в каждой игровой сцене.
    ///
    /// Биндит всё в одном контейнере:
    ///   - MessagePipe (BindMessagePipe + все брокеры)
    ///   - GameClock, SessionManager (POCO сервисы)
    ///   - GameLoopRunner, FirstPersonController (scene MonoBehaviours)
    ///
    /// Почему всё здесь, а не в ProjectInstaller:
    ///   MessagePipe.Zenject требует что IPublisher/ISubscriber резолвятся
    ///   из того же контейнера что и их потребители. GameLoopRunner и FPC
    ///   живут в SceneContext — значит MessagePipe и сервисы тоже должны
    ///   быть здесь. ProjectInstaller остаётся пустым для single-scene проекта.
    /// </summary>
    public sealed class SceneInstaller : MonoInstaller
    {
        [Header("Scene References")]
        [SerializeField] private FirstPersonController _fpc;
        [SerializeField] private GameLoopRunner        _gameLoopRunner;

        public override void InstallBindings()
        {
            InstallMessagePipe();
            InstallServices();
            InstallSceneObjects();
        }

        // ── MessagePipe ───────────────────────────────────────────

        private void InstallMessagePipe()
        {
            var options = Container.BindMessagePipe();

            Container.BindMessageBroker<GameTickMessage>(options);
            Container.BindMessageBroker<DayChangedMessage>(options);
            Container.BindMessageBroker<SessionStateChangedMessage>(options);
            Container.BindMessageBroker<TokensChangedMessage>(options);
            Container.BindMessageBroker<EffectAppliedMessage>(options);
            Container.BindMessageBroker<EffectExpiredMessage>(options);
            Container.BindMessageBroker<RoutineCompletedMessage>(options);
            Container.BindMessageBroker<ScanRequestedMessage>(options);
            Container.BindMessageBroker<ScanCompletedMessage>(options);
            Container.BindMessageBroker<MiniGameStartedMessage>(options);
            Container.BindMessageBroker<MiniGameCompletedMessage>(options);
            Container.BindMessageBroker<SaveCompletedMessage>(options);
            Container.BindMessageBroker<SaveDeniedMessage>(options);
        }

        // ── POCO Services ─────────────────────────────────────────

        private void InstallServices()
        {
            // BindInterfacesAndSelfTo биндит конкретный тип + все реализованные
            // интерфейсы (IInitializable, IDisposable, ISaveable) на один инстанс.
            // Zenject автоматически вызывает Initialize() и Dispose().

            Container.BindInterfacesAndSelfTo<GameClock>()
                     .AsSingle()
                     .NonLazy();

            Container.BindExecutionOrder<GameClock>(-200);

            Container.BindInterfacesAndSelfTo<SessionManager>()
                     .AsSingle()
                     .NonLazy();

            Container.BindExecutionOrder<SessionManager>(-100);
        }

        // ── Scene MonoBehaviours ──────────────────────────────────

        private void InstallSceneObjects()
        {
            if (_gameLoopRunner != null)
                Container.Bind<GameLoopRunner>().FromInstance(_gameLoopRunner).AsSingle();
            else
                Debug.LogWarning("[SceneInstaller] GameLoopRunner не назначен в инспекторе.");

            if (_fpc != null)
                Container.Bind<FirstPersonController>().FromInstance(_fpc).AsSingle();
            else
                Debug.LogWarning("[SceneInstaller] FirstPersonController не назначен в инспекторе.");
        }
    }
}