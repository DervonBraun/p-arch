using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Archipelago.Audio
{
    /// <summary>
    /// Упрощённый роутер реверберации для экстремальных переходов.
    /// Steam Audio закрывает большинство случаев.
    /// Этот компонент только для: улица→подвал, галерея→генераторная.
    ///
    /// Trigger-зоны с lerp send-уровня в AudioMixer (0.3–0.5 сек).
    ///
    /// Прикрепить на Player GameObject.
    /// </summary>
    public sealed class ReverbZoneRouter : MonoBehaviour
    {
        [Serializable]
        public sealed class ReverbZone
        {
            [Tooltip("Имя зоны (для дебага)")]
            public string ZoneName;
            [Tooltip("Trigger-коллайдер зоны")]
            public Collider Trigger;
            [Tooltip("AudioMixer exposed параметр Send уровня")]
            public string   MixerParam;
            [Tooltip("Целевое значение send при входе в зону (dB или 0-1 в зависимости от микшера)")]
            public float    TargetSend = 0f;
            [Tooltip("Значение send вне зоны")]
            public float    DefaultSend = -80f;
        }

        // ── Inspector ─────────────────────────────────────────────

        [SerializeField] private AudioMixer      _mixer;
        [SerializeField] private float           _transitionTime = 0.4f;
        [SerializeField] private List<ReverbZone> _zones = new();

        // ── State ─────────────────────────────────────────────────

        // Текущие и целевые значения send для каждой зоны
        private float[] _currentSends;
        private float[] _targetSends;

        // ── Unity Lifecycle ───────────────────────────────────────

        private void Awake()
        {
            _currentSends = new float[_zones.Count];
            _targetSends  = new float[_zones.Count];

            for (int i = 0; i < _zones.Count; i++)
            {
                _currentSends[i] = _zones[i].DefaultSend;
                _targetSends[i]  = _zones[i].DefaultSend;
            }
        }

        private void Update()
        {
            float speed = 1f / Mathf.Max(0.01f, _transitionTime);

            for (int i = 0; i < _zones.Count; i++)
            {
                _currentSends[i] = Mathf.MoveTowards(
                    _currentSends[i], _targetSends[i],
                    speed * Time.deltaTime * Mathf.Abs(_zones[i].TargetSend - _zones[i].DefaultSend));

                if (_mixer != null && !string.IsNullOrEmpty(_zones[i].MixerParam))
                    _mixer.SetFloat(_zones[i].MixerParam, _currentSends[i]);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            for (int i = 0; i < _zones.Count; i++)
            {
                if (_zones[i].Trigger == other)
                    _targetSends[i] = _zones[i].TargetSend;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            for (int i = 0; i < _zones.Count; i++)
            {
                if (_zones[i].Trigger == other)
                    _targetSends[i] = _zones[i].DefaultSend;
            }
        }
    }
}
