using System;
using UnityEngine;

namespace Archipelago.Player
{
    [CreateAssetMenu(menuName = "Archipelago/Footstep Surface Database v2")]
    public sealed class FootstepSurfaceDatabase : ScriptableObject
    {
        [Serializable]
        public struct SurfaceEntry
        {
            public PhysicsMaterial Material;
            public float ParameterValue; // Значение для Labeled Parameter в FMOD (0, 1, 2...)
        }

        [SerializeField] private SurfaceEntry[] _surfaces;
        [SerializeField] private float _fallbackValue = 0f;

        public bool TryResolve(PhysicsMaterial material, out float parameterValue)
        {
            foreach (var entry in _surfaces)
            {
                if (entry.Material == material)
                {
                    parameterValue = entry.ParameterValue;
                    return true;
                }
            }
            parameterValue = _fallbackValue;
            return true;
        }
    }
}