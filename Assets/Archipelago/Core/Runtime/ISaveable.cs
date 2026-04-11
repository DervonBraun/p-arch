namespace Archipelago.Core
{
    /// <summary>
    /// Contract for any system that participates in save/load.
    /// SaveService (Этап 6) resolves all ISaveable bindings from the container
    /// and calls CaptureState / RestoreState on each.
    ///
    /// Bind alongside the concrete type in the Installer:
    ///   Container.BindInterfacesAndSelfTo&lt;GameClock&gt;().AsSingle();
    /// This makes ISaveable, IInitializable, IDisposable, and GameClock
    /// all resolve to the same instance.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// Returns a JSON-serializable snapshot. Must be pure — no side effects.
        /// </summary>
        object CaptureState();

        /// <summary>
        /// Restores state from a snapshot. Called before Initialize() on load.
        /// </summary>
        void RestoreState(object state);
    }
}