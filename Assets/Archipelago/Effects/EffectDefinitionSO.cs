// Assets/Effects/Data/EffectDefinitionSO.cs
using Archipelago.Core;
using UnityEngine;

namespace Archipelago.Effects
{
    [CreateAssetMenu(
        fileName = "EffectDefinition",
        menuName  = "Archipelago/Effects/Effect Definition")]
    public sealed class EffectDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string effectId = "satiety";
        public string displayName = "Сытость";

        [Header("Timer")]
        [Tooltip("Максимальная длительность в игровых секундах")]
        public float maxDuration = 300f;

        [Tooltip("Сколько секунд добавляется при стаке (не превышает maxDuration)")]
        public float stackTimerExtension = 20f;

        [Header("Stacks")]
        [Tooltip("Максимальное количество стаков (1 = не стакается)")]
        public int maxStacks = 3;

        [Header("Modifier")]
        [Tooltip("Базовый модификатор при стаке 1 (например 0.10 = +10%)")]
        public float baseModifier = 0.10f;

        [Tooltip("Прирост модификатора за каждый стак сверх первого")]
        public float modifierPerStack = 0.05f;

        // ── Runtime helper ────────────────────────────────────────

        /// <summary>Модификатор для заданного количества стаков.</summary>
        public float GetModifier(int stacks)
            => baseModifier + (stacks - 1) * modifierPerStack;
    }
}