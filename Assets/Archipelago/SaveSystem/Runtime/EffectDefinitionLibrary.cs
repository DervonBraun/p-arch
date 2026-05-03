using System.Collections.Generic;
using Archipelago.Effects;
using UnityEngine;

namespace Archipelago.SaveSystem
{
    /// <summary>
    /// Реестр всех EffectDefinitionSO.
    /// Используется EffectsSaveable для восстановления эффектов по effectId.
    ///
    /// Assign all effect SOs in Inspector.
    /// Assets/SaveSystem/Data/EffectDefinitionLibrary.asset
    /// </summary>
    [CreateAssetMenu(
        fileName = "EffectDefinitionLibrary",
        menuName  = "Archipelago/SaveSystem/Effect Definition Library")]
    public sealed class EffectDefinitionLibrary : ScriptableObject
    {
        [SerializeField] private List<EffectDefinitionSO> _definitions = new();

        private Dictionary<string, EffectDefinitionSO> _lookup;

        private void OnEnable()
        {
            _lookup = new Dictionary<string, EffectDefinitionSO>();
            foreach (var def in _definitions)
                if (def != null) _lookup[def.effectId] = def;
        }

        public EffectDefinitionSO Get(string effectId)
        {
            _lookup ??= new Dictionary<string, EffectDefinitionSO>();
            return _lookup.TryGetValue(effectId, out var def) ? def : null;
        }
    }
}
