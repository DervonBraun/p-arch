using Archipelago.Session;
using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    /// <summary>
    /// Runtime debug overlay — shows GameClock + SessionManager state in the game view.
    /// Stripped from release builds via DEVELOPMENT_BUILD define.
    ///
    /// Attach to any GameObject in the test scene.
    /// Uses OnGUI (legacy) intentionally — debug-only, no render pipeline impact.
    /// </summary>
    public sealed class GameClockDebugOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        [Inject] private GameClock      _clock;
        [Inject] private SessionManager _session;

        private GUIStyle _style;

        private void OnGUI()
        {
            if (_clock == null) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = Color.cyan }
            };

            float totalSec = _clock.TotalGameTime;
            int   hours    = Mathf.FloorToInt(totalSec / 3600f);
            int   minutes  = Mathf.FloorToInt((totalSec % 3600f) / 60f);

            string info =
                $"[GameClock]\n" +
                $"  Total : {totalSec:F1}s\n" +
                $"  Time  : {hours:D2}:{minutes:D2}\n" +
                $"  Day   : {_clock.DayIndex}\n" +
                $"  Scale : {_clock.TimeScale:F1}x\n" +
                $"  Paused: {_clock.IsPaused}\n" +
                $"\n[Session]\n" +
                $"  State : {_session?.CurrentState}";

            GUI.Label(new Rect(10, 10, 230, 180), info, _style);

            if (GUI.Button(new Rect(10, 200, 110, 24), _clock.IsPaused ? "Resume" : "Pause"))
            {
                if (_clock.IsPaused) _clock.Resume();
                else _clock.Pause();
            }

            if (GUI.Button(new Rect(128, 200, 40, 24), "2x"))  _clock.TimeScale = 2f;
            if (GUI.Button(new Rect(174, 200, 40, 24), "10x")) _clock.TimeScale = 10f;
            if (GUI.Button(new Rect(220, 200, 40, 24), "1x"))  _clock.TimeScale = 1f;

            if (_session?.CurrentState == SessionState.WakeUp)
            {
                if (GUI.Button(new Rect(10, 232, 160, 24), "Skip WakeUp → FreeRoam"))
                    _session.OnWakeUpComplete();
            }
        }

#endif
    }
}