using SteamAudio;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Archipelago.Player
{
    /// <summary>
    /// Воспроизводит шаги через SteamAudioSource.
    /// Определяет поверхность под ногами через raycast → PhysicsMaterial.
    /// Вызывается из PlayerFeelController.
    ///
    /// Требования:
    ///   • SteamAudioSource на этом же GameObject (или назначен вручную)
    ///   • FootstepSurfaceDatabase назначена в инспекторе
    ///   • SteamAudioGeometry на мешах сцены
    ///   • SteamAudioListener на камере / Audio Listener
    /// </summary>
    [RequireComponent(typeof(SteamAudioSource))]
    public sealed class FootstepPlayer : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────

        [Header("Database")]
        [SerializeField] private FootstepSurfaceDatabase _database;

        [Header("Audio")]
        [SerializeField] [Range(0f, 1f)]   private float _masterVolume      = 0.5f;
        [SerializeField] [Range(0f, 0.3f)] private float _pitchVariance     = 0.08f;

        [Header("Raycast")]
        [SerializeField] private float _raycastDistance  = 1.2f;
        [SerializeField] private LayerMask _groundLayers = ~0;

        // ── Private ───────────────────────────────────────────────

        private SteamAudioSource _steamSource;

        // Кэш последнего материала — не делаем новый raycast если не шевелились
        private PhysicsMaterial  _lastMaterial;

        // ── Lifecycle ─────────────────────────────────────────────

        private void Awake()
        {
            _steamSource = GetComponent<SteamAudioSource>();

            // SteamAudioSource должен быть настроен на прямую передачу звука
            // (Direct Sound, Occlusion, Reverb) — через Inspector на компоненте.
            // Здесь мы только воспроизводим клипы.
        }

        // ── Public API ────────────────────────────────────────────

        /// <summary>
        /// Вызывается из PlayerFeelController когда пора сделать шаг.
        /// </summary>
        public void PlayStep()
        {
            if (_database == null || _steamSource == null) return;

            PhysicsMaterial surface = ResolveSurface();

            if (!_database.TryResolve(surface, out AudioClip clip, out float volumeMult))
                return;

            // SteamAudioSource не поддерживает PlayOneShot —
            // меняем клип и вызываем Play, предварительно остановив предыдущий
            _steamSource.GetComponent<AudioSource>().pitch =
                1f + Random.Range(-_pitchVariance, _pitchVariance);

            // Воспроизводим через нативный AudioSource который лежит под SteamAudioSource
            var audioSource = _steamSource.GetComponent<AudioSource>();
            audioSource.clip   = clip;
            audioSource.volume = _masterVolume * volumeMult;
            audioSource.Play();
        }

        // ── Surface Detection ─────────────────────────────────────

        private PhysicsMaterial ResolveSurface()
        {
            // Raycast строго вниз от позиции игрока
            if (Physics.Raycast(
                    transform.position,
                    Vector3.down,
                    out RaycastHit hit,
                    _raycastDistance,
                    _groundLayers,
                    QueryTriggerInteraction.Ignore))
            {
                _lastMaterial = hit.collider.sharedMaterial;
            }

            // Возвращаем последний известный материал даже если raycast промахнулся
            return _lastMaterial;
        }
    }
}
