using Archipelago.Player;
using Archipelago.Scanner;
using Archipelago.Session;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    /// <summary>
    /// Scene-level installer. Attach to SceneContext в каждой игровой сцене.
    ///
    /// Порядок инициализации через BindExecutionOrder:
    ///   -200  GameClock        (корневой тик)
    ///   -100  SessionManager   (FSM)
    ///     0   остальные сервисы
    /// </summary>
    public sealed class SceneInstaller : MonoInstaller
    {
        [Header("Scene References")]
        [SerializeField] private FirstPersonController _fpc;
        [SerializeField] private GameLoopRunner        _gameLoopRunner;
        [SerializeField] private InputRouter           _inputRouter;

        [Header("Input")]
        [SerializeField] private InputReader _inputReader;

        public override void InstallBindings()
        {
            InstallMessagePipe();
            InstallInput();
            InstallServices();
            InstallSceneObjects();
        }

        // ── MessagePipe ───────────────────────────────────────────

        private void InstallMessagePipe()
        {
            var options = Container.BindMessagePipe();

            // Clock
            Container.BindMessageBroker<GameTickMessage>(options);
            Container.BindMessageBroker<DayChangedMessage>(options);

            // Session
            Container.BindMessageBroker<SessionStateChangedMessage>(options);

            // Economy
            Container.BindMessageBroker<TokensChangedMessage>(options);
            Container.BindMessageBroker<TokensSyncedMessage>(options);
            Container.BindMessageBroker<TokensInsufficientMessage>(options);

            // Effects
            Container.BindMessageBroker<EffectAppliedMessage>(options);
            Container.BindMessageBroker<EffectExpiredMessage>(options);
            Container.BindMessageBroker<EarnMultiplierChangedMessage>(options);   // <-- новый
            Container.BindMessageBroker<CleanStateChangedMessage>(options);       // <-- новый
            Container.BindMessageBroker<GardenMultiplierChangedMessage>(options); // <-- новый

            // Routine
            Container.BindMessageBroker<RoutineCompletedMessage>(options);

            // Scanner
            Container.BindMessageBroker<ObjectCapturedMessage>(options);
            Container.BindMessageBroker<ScanRequestedMessage>(options);
            Container.BindMessageBroker<ScanCompletedMessage>(options);

            // Mini-games
            Container.BindMessageBroker<MiniGameStartedMessage>(options);
            Container.BindMessageBroker<MiniGameCompletedMessage>(options);

            // Save
            Container.BindMessageBroker<SaveCompletedMessage>(options);
            Container.BindMessageBroker<SaveDeniedMessage>(options);
        }

        // ── Input ─────────────────────────────────────────────────

        private void InstallInput()
        {
            if (_inputReader != null)
                Container.BindInstance(_inputReader).AsSingle();
            else
                Debug.LogError("[SceneInstaller] InputReader не назначен.");

            if (_inputRouter != null)
                Container.Bind<InputRouter>().FromInstance(_inputRouter).AsSingle();
            else
                Debug.LogWarning("[SceneInstaller] InputRouter не назначен.");
        }

        // ── POCO Services ─────────────────────────────────────────

        private void InstallServices()
        {
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
            Container.Bind<GameBootstrapper>()
                .FromComponentInHierarchy()
                .AsSingle();

            if (_gameLoopRunner != null)
                Container.Bind<GameLoopRunner>().FromInstance(_gameLoopRunner).AsSingle();
            else
                Debug.LogWarning("[SceneInstaller] GameLoopRunner не назначен.");

            if (_fpc != null)
                Container.Bind<FirstPersonController>().FromInstance(_fpc).AsSingle();
            else
                Debug.LogWarning("[SceneInstaller] FirstPersonController не назначен.");
        }
    }
}