namespace Archipelago.Core
{
    /// <summary>
    /// Lifecycle contract for all services managed by Zenject.
    /// IInitializable / IDisposable from Zenject cover Init/Shutdown,
    /// but this interface lets SaveService enumerate all ISaveable services
    /// without knowing concrete types.
    ///
    /// Zenject calls Initialize() automatically on IInitializable bindings.
    /// Zenject calls Dispose() automatically on IDisposable bindings.
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// Called by Zenject after all bindings are resolved.
        /// Use for cross-service wiring that can't happen in the constructor.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Called by Zenject on container disposal (scene unload / app quit).
        /// Unsubscribe events, cancel UniTask tokens, flush queues.
        /// </summary>
        void Dispose();
    }
}