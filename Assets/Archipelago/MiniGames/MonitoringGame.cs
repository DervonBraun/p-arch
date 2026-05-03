using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.MiniGames
{
    /// <summary>
    /// Мониторинг параметров.
    /// N ползунков дрейфуют. Игрок возвращает их в норму кнопками.
    /// Провал = любой параметр вне зоны 3+ сек подряд.
    /// Quality = обратное суммарному времени вне зоны.
    /// </summary>
    public sealed class MonitoringGame : IMiniGame
    {
        public string MiniGameId => "monitoring";

        private const float GameDuration  = 15f;
        private const float DriftSpeed    = 0.08f;
        private const float AdjustSpeed   = 0.15f;
        private const float SafeZoneHalf  = 0.25f;
        private const float FailThreshold = 3f;
        private const int   ParamCount    = 3;

        private float[] _values;
        private float[] _driftDir;
        private float[] _outOfZoneTime;
        private float   _elapsed;
        private float   _totalOutTime;

        public float[] Values        => _values;
        public float   TimeRemaining => Mathf.Max(0f, GameDuration - _elapsed);

        private readonly MiniGameManager                    _manager;
        private readonly IPublisher<MonitoringStateMessage> _statePub;

        [Inject]
        public MonitoringGame(
            MiniGameManager                    manager,
            IPublisher<MonitoringStateMessage> statePub)
        {
            _manager  = manager;
            _statePub = statePub;
        }

        public void Initialize()
        {
            _elapsed       = 0f;
            _totalOutTime  = 0f;
            _values        = new float[ParamCount];
            _driftDir      = new float[ParamCount];
            _outOfZoneTime = new float[ParamCount];

            for (int i = 0; i < ParamCount; i++)
            {
                _values[i]        = Random.Range(-0.1f, 0.1f);
                _driftDir[i]      = Random.value > 0.5f ? 1f : -1f;
                _outOfZoneTime[i] = 0f;
            }
        }

        public void Begin() { }

        public void Tick(float dt)
        {
            _elapsed += dt;
            bool failed = false;

            for (int i = 0; i < ParamCount; i++)
            {
                _values[i] += _driftDir[i] * DriftSpeed * dt;
                if (_values[i] >  1f) { _values[i] =  1f; _driftDir[i] = -1f; }
                if (_values[i] < -1f) { _values[i] = -1f; _driftDir[i] =  1f; }

                bool inZone = Mathf.Abs(_values[i]) <= SafeZoneHalf;
                if (!inZone)
                {
                    _outOfZoneTime[i] += dt;
                    _totalOutTime     += dt;
                    if (_outOfZoneTime[i] >= FailThreshold) failed = true;
                }
                else
                {
                    _outOfZoneTime[i] = 0f;
                }
            }

            _statePub.Publish(new MonitoringStateMessage(_values, _outOfZoneTime, TimeRemaining));

            if (failed)
            {
                _manager.CompleteGame(MiniGameId, false, 0f);
                return;
            }

            if (_elapsed >= GameDuration)
            {
                float maxOutTime = GameDuration * ParamCount;
                float quality    = 1f - Mathf.Clamp01(_totalOutTime / maxOutTime);
                _manager.CompleteGame(MiniGameId, true, quality);
            }
        }

        /// <summary>UI вызывает при нажатии кнопки коррекции параметра.</summary>
        public void AdjustParameter(int index, float direction)
        {
            if (index < 0 || index >= ParamCount) return;
            _values[index] = Mathf.Clamp(_values[index] - direction * AdjustSpeed, -1f, 1f);
        }

        public void End() { }
    }
}
