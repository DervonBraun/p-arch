// Assets/Effects/Runtime/ActiveEffect.cs
namespace Archipelago.Effects
{
    /// <summary>
    /// Рантайм-состояние одного активного эффекта.
    /// Создаётся EffectService, тикается через GameTickMessage.
    /// </summary>
    public sealed class ActiveEffect
    {
        public EffectDefinitionSO Definition     { get; }
        public int                CurrentStacks  { get; private set; }
        public float              RemainingTime  { get; private set; }

        /// <summary>Текущий модификатор с учётом стаков.</summary>
        public float CurrentModifier => Definition.GetModifier(CurrentStacks);

        public bool IsExpired => RemainingTime <= 0f;

        public ActiveEffect(EffectDefinitionSO definition)
        {
            Definition    = definition;
            CurrentStacks = 1;
            RemainingTime = definition.maxDuration;
        }

        /// <summary>
        /// Добавить стак. Продлевает таймер, не превышая maxDuration.
        /// Возвращает true если стак был добавлен, false если потолок.
        /// </summary>
        public bool AddStack()
        {
            bool atCap = CurrentStacks >= Definition.maxStacks;

            // Продлеваем таймер в любом случае (даже на потолке)
            RemainingTime = UnityEngine.Mathf.Min(
                RemainingTime + Definition.stackTimerExtension,
                Definition.maxDuration);

            if (atCap) return false;

            CurrentStacks++;
            return true;
        }

        /// <summary>Уменьшить таймер на deltaTime (игровые секунды).</summary>
        public void Tick(float deltaGameTime)
        {
            RemainingTime -= deltaGameTime;
            if (RemainingTime < 0f) RemainingTime = 0f;
        }
    }
}