using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.MiniGames
{
    /// <summary>
    /// Калибровка датчиков.
    /// Стрелка дрожит (синус + Perlin шум). Игрок удерживает её в зелёной зоне мышью/стиком.
    /// Quality = % времени в зоне от общего времени игры.
    /// </summary>
    public sealed class CalibrationGame : IMiniGame
    {
        public string MiniGameId => "calibration";

        private const float GameDuration   = 10f;
        private const float ZoneHalfWidth  = 0.15f;
        private const float NoiseFrequency = 0.8f;
        private const float SineFrequency  = 1.2f;
        private const float SineAmplitude  = 0.4f;
        private const float NoiseAmplitude = 0.25f;

        // State
        private float _elapsed;
        private float _timeInZone;
        private float _noiseOffset;

        public float NeedlePosition { get; private set; }
        public float PlayerPosition { get; private set; }
        public bool  InZone         => Mathf.Abs(NeedlePosition - PlayerPosition) < ZoneHalfWidth;

        private readonly MiniGameManager                     _manager;
        private readonly IPublisher<CalibrationStateMessage> _statePub;

        [Inject]
        public CalibrationGame(
            MiniGameManager                     manager,
            IPublisher<CalibrationStateMessage> statePub)
        {
            _manager  = manager;
            _statePub = statePub;
        }

        public void Initialize()
        {
            _elapsed        = 0f;
            _timeInZone     = 0f;
            _noiseOffset    = Random.Range(0f, 100f);
            NeedlePosition  = 0f;
            PlayerPosition  = 0f;
        }

        public void Begin() { }

        public void Tick(float dt)
        {
            _elapsed += dt;

            float sine  = Mathf.Sin(_elapsed * SineFrequency * Mathf.PI * 2f) * SineAmplitude;
            float noise = (Mathf.PerlinNoise(_elapsed * NoiseFrequency, _noiseOffset) * 2f - 1f) * NoiseAmplitude;
            NeedlePosition = Mathf.Clamp(sine + noise, -1f, 1f);

            float input    = Input.GetAxis("Mouse X") * 0.05f + Input.GetAxis("Horizontal") * 0.03f;
            PlayerPosition = Mathf.Clamp(PlayerPosition + input, -1f, 1f);

            if (InZone) _timeInZone += dt;

            _statePub.Publish(new CalibrationStateMessage(NeedlePosition, PlayerPosition, InZone));

            if (_elapsed >= GameDuration)
            {
                float quality = _timeInZone / GameDuration;
                _manager.CompleteGame(MiniGameId, quality > 0.3f, quality);
            }
        }

        public void End() { }
    }
}
