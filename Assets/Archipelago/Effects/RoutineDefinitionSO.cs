// Assets/Effects/Data/RoutineDefinitionSO.cs
using UnityEngine;

namespace Archipelago.Effects
{
    [CreateAssetMenu(
        fileName = "RoutineDefinition",
        menuName  = "Archipelago/Effects/Routine Definition")]
    public sealed class RoutineDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string routineId   = "eat";
        public string displayName = "Поесть";

        [Header("Reward")]
        [Tooltip("Базовое количество синих токенов за выполнение")]
        public int baseBlueReward = 10;

        [Tooltip("k для формулы убывания: earned = base / (1 + k * dailyCount)")]
        public float diminishingK = 0.5f;

        [Header("Effect")]
        [Tooltip("Какой эффект применяется после выполнения (опционально)")]
        public EffectDefinitionSO appliedEffect;

        [Header("Timing")]
        [Tooltip("Длительность анимации/действия в секундах реального времени")]
        public float actionDurationSeconds = 2f;
    }
}