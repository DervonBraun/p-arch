using UnityEngine;
using UnityEngine.Audio;

namespace Archipelago.Audio
{
    /// <summary>
    /// Динамический Low-Pass Filter на основе raycast окклюзии.
    ///
    /// Каждые N физических кадров — fan cast от AudioSource к AudioListener.
    /// Попадание в коллайдер → читает PhysicsMaterial → маппинг на cutoff-частоту.
    /// Интерполяция cutoff пропорционально числу непробитых лучей.
    ///
    /// Материалы → cutoff (Hz):
    ///   Камень  = 200
    ///   Дерево  = 800
    ///   Металл  = 400
    ///   Стекло  = 2000
    ///   Default = 500
    ///
    /// Requires: AudioMixerGroup с exposed параметром "{groupName}_Cutoff"
    ///
    /// PERF: fan cast каждые _checkInterval физических кадров, не каждый кадр.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class OcclusionFilter : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────

        [Header("Raycast")]
        [Tooltip("Количество лучей в fan cast")]
        [SerializeField] private int   _rayCount      = 6;
        [Tooltip("Проверять каждые N FixedUpdate кадров")]
        [SerializeField] private int   _checkInterval = 3;
        [Tooltip("Маска слоёв для окклюзии (стены, двери, пропсы)")]
        [SerializeField] private LayerMask _occlusionMask = ~0;

        [Header("Mixer")]
        [Tooltip("AudioMixerGroup с exposed параметром Cutoff")]
        [SerializeField] private AudioMixer _mixer;
        [Tooltip("Имя exposed параметра в AudioMixer")]
        [SerializeField] private string _cutoffParam = "SFX_Cutoff";

        [Header("Cutoff Range")]
        [SerializeField] private float _openCutoff    = 22000f;  // нет окклюзии
        [SerializeField] private float _blockedCutoff = 200f;    // полная окклюзия
        [SerializeField] private float _smoothSpeed   = 8f;

        // ── State ─────────────────────────────────────────────────

        private AudioSource  _source;
        private Transform    _listenerTransform;
        private float        _currentCutoff;
        private float        _targetCutoff;
        private int          _frameCounter;

        // PERF: переиспользуем массив хитов чтобы не аллоцировать каждый кадр
        private readonly RaycastHit[] _hits = new RaycastHit[4];

        // ── Unity Lifecycle ───────────────────────────────────────

        private void Awake()
        {
            _source        = GetComponent<AudioSource>();
            _currentCutoff = _openCutoff;
            _targetCutoff  = _openCutoff;
        }

        private void Start()
        {
            var listener = FindFirstObjectByType<AudioListener>();
            if (listener != null)
                _listenerTransform = listener.transform;
            else
                Debug.LogWarning("[OcclusionFilter] AudioListener not found in scene.");
        }

        private void FixedUpdate()
        {
            _frameCounter++;
            if (_frameCounter < _checkInterval) return;
            _frameCounter = 0;

            if (_listenerTransform == null) return;

            _targetCutoff = ComputeTargetCutoff();
        }

        private void Update()
        {
            // Плавная интерполяция cutoff
            _currentCutoff = Mathf.Lerp(_currentCutoff, _targetCutoff,
                Time.deltaTime * _smoothSpeed);

            if (_mixer != null)
                _mixer.SetFloat(_cutoffParam, _currentCutoff);
        }

        // ── Occlusion Computation ─────────────────────────────────

        private float ComputeTargetCutoff()
        {
            Vector3 from = transform.position;
            Vector3 to   = _listenerTransform.position;
            Vector3 dir  = to - from;
            float   dist = dir.magnitude;

            if (dist < 0.01f) return _openCutoff;

            // Fan cast: N лучей вокруг прямого направления
            int   blocked      = 0;
            float minCutoff    = _openCutoff;

            for (int i = 0; i < _rayCount; i++)
            {
                Vector3 rayDir = FanDirection(dir.normalized, i, _rayCount);
                int hitCount   = Physics.RaycastNonAlloc(from, rayDir, _hits, dist, _occlusionMask);

                if (hitCount > 0)
                {
                    blocked++;
                    float hitCutoff = GetMaterialCutoff(_hits[0]);
                    minCutoff = Mathf.Min(minCutoff, hitCutoff);
                }
            }

            if (blocked == 0) return _openCutoff;

            // Интерполяция: чем больше заблокированных лучей — тем ниже cutoff
            float ratio = (float)blocked / _rayCount;
            return Mathf.Lerp(_openCutoff, minCutoff, ratio);
        }

        // Распределяем лучи в конусе вокруг основного направления
        private static Vector3 FanDirection(Vector3 main, int index, int total)
        {
            if (total <= 1) return main;

            float angle  = (index / (float)(total - 1) - 0.5f) * 30f; // ±15°
            return Quaternion.AngleAxis(angle, Vector3.up) * main;
        }

        private float GetMaterialCutoff(RaycastHit hit)
        {
            // Маппинг PhysicsMaterial.name → cutoff Hz
            string matName = hit.collider.sharedMaterial?.name?.ToLower() ?? "";

            if (matName.Contains("stone") || matName.Contains("concrete") || matName.Contains("brick"))
                return 200f;
            if (matName.Contains("metal") || matName.Contains("iron") || matName.Contains("steel"))
                return 400f;
            if (matName.Contains("wood"))
                return 800f;
            if (matName.Contains("glass"))
                return 2000f;

            return _blockedCutoff; // дефолт
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_listenerTransform == null) return;

            Vector3 dir  = (_listenerTransform.position - transform.position).normalized;
            float   dist = Vector3.Distance(transform.position, _listenerTransform.position);

            Gizmos.color = Color.yellow;
            for (int i = 0; i < _rayCount; i++)
            {
                Vector3 rayDir = FanDirection(dir, i, _rayCount);
                Gizmos.DrawRay(transform.position, rayDir * dist);
            }
        }
#endif
    }
}
