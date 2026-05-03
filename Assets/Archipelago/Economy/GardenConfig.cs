using UnityEngine;

namespace Archipelago.Economy
{
    [CreateAssetMenu(
        fileName = "GardenConfig",
        menuName  = "Archipelago/Economy/Garden Config")]
    public sealed class GardenConfig : ScriptableObject
    {
        [Header("Accumulation")]
        [Tooltip("Зелёных токенов в секунду при базовом множителе 1.0")]
        public float BaseAccumulationRate = 0.5f;

        [Tooltip("Максимум накопленных токенов до сбора")]
        public int MaxAccumulated = 200;

        [Tooltip("Токены пропадают если не собрать за это время (игровые секунды)")]
        public float DecayDelay = 600f;
    }
}
