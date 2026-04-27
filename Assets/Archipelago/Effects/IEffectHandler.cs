// Assets/Effects/Runtime/IEffectHandler.cs
namespace Archipelago.Effects
{
    /// <summary>
    /// Кастомная логика конкретного эффекта.
    /// Один хендлер на тип эффекта, регистрируется в EffectService.
    /// </summary>
    public interface IEffectHandler
    {
        string EffectId { get; }

        /// <summary>Вызывается при первом применении эффекта (стак 1).</summary>
        void OnApply(ActiveEffect effect);

        /// <summary>Вызывается при повторном применении (стак 2+).</summary>
        void OnStack(ActiveEffect effect);

        /// <summary>Вызывается каждый игровой тик пока эффект активен.</summary>
        void OnTick(ActiveEffect effect, float deltaGameTime);

        /// <summary>Вызывается когда таймер достиг нуля.</summary>
        void OnExpire(ActiveEffect effect);
    }
}