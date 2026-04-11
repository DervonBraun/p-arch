using Archipelago.Session;
using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    /// <summary>
    /// Thin MonoBehaviour bridge: calls GameClock.Tick() every real frame.
    ///
    /// Why a separate class and not inside GameClock?
    ///   GameClock is a POCO (no MonoBehaviour) — keeps it testable and
    ///   independent of the Unity object lifecycle. GameLoopRunner is the
    ///   only MonoBehaviour allowed to touch the clock's Update pump.
    ///
    /// Scene setup: attach to the "─ GAME LOOP ─" GameObject in the
    ///   bootstrap scene. SceneContext will inject GameClock automatically.
    /// </summary>
    public sealed class GameLoopRunner : MonoBehaviour
    {
        // ── Zenject field injection ───────────────────────────────
        // Constructor injection is not available on MonoBehaviours.
        // [Inject] on a field is the Zenject-idiomatic alternative.

        [Inject] private GameClock _gameClock;

        private void Update()
        {
            // PERF: Single method call per frame, no allocations.
            _gameClock.Tick(Time.deltaTime);
        }
    }
}