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
        [SerializeField] private InputReader _inputReader;   // ScriptableObject asset

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

        // ── Input ─────────────────────────────────────────────────

        private void InstallInput()
        {
            // InputReader — ScriptableObject, живёт в Assets/.
            // Биндим как инстанс чтобы все потребители получили один и тот же SO.
            if (_inputReader != null)
                Container.BindInstance(_inputReader).AsSingle();
            else
                Debug.LogError("[SceneInstaller] InputReader не назначен. Создай asset и назначь в Inspector.");

            // InputRouter — MonoBehaviour на Player, переключает Input Maps по SessionState.
            if (_inputRouter != null)
                Container.Bind<InputRouter>().FromInstance(_inputRouter).AsSingle();
            else
                Debug.LogWarning("[SceneInstaller] InputRouter не назначен в инспекторе.");
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