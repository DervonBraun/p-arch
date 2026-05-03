using System;
using UnityEngine;

namespace Archipelago.Player
{
    /// <summary>
    /// ScriptableObject-база поверхностей.
    /// Создать через Assets → Create → Archipelago → Footstep Surface Database.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Archipelago/Footstep Surface Database",
        fileName = "FootstepSurfaceDatabase")]
    public sealed class FootstepSurfaceDatabase : ScriptableObject
    {
        [Serializable]
        public struct SurfaceEntry
        {
            [Tooltip("PhysicsMaterial коллайдера поверхности. Null = дефолт.")]
            public PhysicsMaterial Material;

            [Tooltip("Клипы для этой поверхности — будет выбран случайный.")]
            public AudioClip[] Clips;

            [Range(0f, 1f)]
            [Tooltip("Громкость относительно мастер-громкости шагов.")]
            public float VolumeMultiplier;
        }

        [SerializeField] private SurfaceEntry[] _surfaces;

        [Header("Fallback")]
        [SerializeField] private AudioClip[] _fallbackClips;
        [SerializeField] [Range(0f, 1f)] private float _fallbackVolume = 0.4f;

        /// <summary>
        /// Возвращает клип и громкость для переданного PhysicsMaterial.
        /// Если материал не найден — возвращает fallback.
        /// </summary>
        public bool TryResolve(PhysicsMaterial material,
                               out AudioClip    clip,
                               out float        volumeMultiplier)
        {
            if (_surfaces != null)
            {
                foreach (var entry in _surfaces)
                {
                    if (entry.Material == material && entry.Clips is { Length: > 0 })
                    {
                        clip             = entry.Clips[UnityEngine.Random.Range(0, entry.Clips.Length)];
                        volumeMultiplier = entry.VolumeMultiplier;
                        return true;
                    }
                }
            }

            // Fallback
            if (_fallbackClips is { Length: > 0 })
            {
                clip             = _fallbackClips[UnityEngine.Random.Range(0, _fallbackClips.Length)];
                volumeMultiplier = _fallbackVolume;
                return true;
            }

            clip             = null;
            volumeMultiplier = 0f;
            return false;
        }
    }
}
