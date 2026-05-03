using UnityEngine;
using UnityEngine.Audio;

namespace Archipelago.Audio
{
    /// <summary>
    /// Звук из соседней комнаты через дверь / окно.
    ///
    /// Если прямой луч от AudioSource к AudioListener заблокирован —
    /// ищем ближайший коллайдер с тегом "AudioPortal".
    /// Создаём виртуальный AudioSource в точке портала.
    /// Громкость = f(угол между направлением к порталу и взглядом игрока, дистанция).
    /// Дополнительный LPF имитирует поглощение дверью.
    ///
    /// Attach на AudioSource объект в соседней комнате.
    /// Portal — дверной проём (Collider с тегом "AudioPortal", Is Trigger = true).
    ///
    /// LIMITATION: один портал на источник. Для сложных маршрутов нужен граф.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PortalAudio : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────

        [Header("Portal Settings")]
        [Tooltip("Максимальная дистанция поиска портала")]
        [SerializeField] private float _portalSearchRadius = 20f;
        [Tooltip("LPF cutoff через дверь (Hz). Имитирует поглощение.")]
        [SerializeField] private float _portalCutoff       = 1200f;
        [Tooltip("Максимальный угол (°) для слышимости через портал")]
        [SerializeField] private float _maxAngle           = 120f;

        [Header("Volume")]
        [Tooltip("Множитель громкости звука через портал")]
        [SerializeField] private float _portalVolumeScale  = 0.6f;
        [SerializeField] private float _smoothSpeed        = 5f;

        [Header("Mixer")]
        [SerializeField] private AudioMixerGroup _portalMixerGroup;

        // ── State ─────────────────────────────────────────────────

        private AudioSource  _source;
        private AudioSource  _portalSource;   // виртуальный источник в точке портала
        private Transform    _listenerTransform;
        private Collider     _cachedPortal;
        private float        _targetVolume;
        private float        _currentVolume;

        // ── Unity Lifecycle ───────────────────────────────────────

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
        }

        private void Start()
        {
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener != null)
                _listenerTransform = listener.transform;

            // Создаём виртуальный AudioSource для портала
            var go = new GameObject($"[PortalSource] {gameObject.name}");
            go.transform.SetParent(transform);
            _portalSource = go.AddComponent<AudioSource>();
            _portalSource.clip        = _source.clip;
            _portalSource.loop        = _source.loop;
            _portalSource.playOnAwake = false;
            _portalSource.spatialBlend = 1f;
            _portalSource.volume      = 0f;

            if (_portalMixerGroup != null)
                _portalSource.outputAudioMixerGroup = _portalMixerGroup;

            if (_source.isPlaying)
                _portalSource.Play();
        }

        private void Update()
        {
            if (_listenerTransform == null) return;

            bool directBlocked = IsDirectPathBlocked();

            if (directBlocked)
            {
                _cachedPortal = FindNearestPortal();
                _targetVolume = _cachedPortal != null
                    ? ComputePortalVolume(_cachedPortal)
                    : 0f;

                if (_cachedPortal != null)
                    _portalSource.transform.position = _cachedPortal.bounds.center;
            }
            else
            {
                _targetVolume = 0f;
            }

            // Плавная интерполяция громкости
            _currentVolume = Mathf.Lerp(_currentVolume, _targetVolume,
                Time.deltaTime * _smoothSpeed);

            _portalSource.volume = _currentVolume * _portalVolumeScale;

            // Синхронизируем воспроизведение с основным источником
            SyncPlayback();
        }

        // ── Internal ─────────────────────────────────────────────

        private bool IsDirectPathBlocked()
        {
            Vector3 dir  = _listenerTransform.position - transform.position;
            float   dist = dir.magnitude;
            return Physics.Raycast(transform.position, dir.normalized, dist);
        }

        private Collider FindNearestPortal()
        {
            // PERF: OverlapSphere каждый кадр — приемлемо для малого числа источников
            var colliders = Physics.OverlapSphere(transform.position, _portalSearchRadius);
            Collider nearest  = null;
            float    minDist  = float.MaxValue;

            foreach (var col in colliders)
            {
                if (!col.CompareTag("AudioPortal")) continue;
                float dist = Vector3.Distance(transform.position, col.bounds.center);
                if (dist < minDist) { minDist = dist; nearest = col; }
            }

            return nearest;
        }

        private float ComputePortalVolume(Collider portal)
        {
            Vector3 portalPos  = portal.bounds.center;
            Vector3 toPortal   = (portalPos - _listenerTransform.position).normalized;
            Vector3 listenerFwd = _listenerTransform.forward;

            // Угол между взглядом игрока и направлением к порталу
            float angle = Vector3.Angle(listenerFwd, toPortal);
            if (angle > _maxAngle) return 0f;

            float angleFactor = 1f - (angle / _maxAngle);

            // Затухание по дистанции от слушателя до портала
            float distToPortal = Vector3.Distance(_listenerTransform.position, portalPos);
            float distFactor   = Mathf.Clamp01(1f - distToPortal / _portalSearchRadius);

            return angleFactor * distFactor;
        }

        private void SyncPlayback()
        {
            if (_source.isPlaying && !_portalSource.isPlaying)
                _portalSource.Play();
            else if (!_source.isPlaying && _portalSource.isPlaying)
                _portalSource.Stop();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _portalSearchRadius);

            if (_cachedPortal != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _cachedPortal.bounds.center);
                Gizmos.DrawSphere(_cachedPortal.bounds.center, 0.15f);
            }
        }
#endif
    }
}
