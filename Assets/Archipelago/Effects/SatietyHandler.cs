using Archipelago.Core;
using MessagePipe;

namespace Archipelago.Effects
{
    /// <summary>
    /// Сытость: модифицирует множитель заработка красных токенов.
    /// Модификатор применяется через EarnMultiplierChangedMessage —
    /// TokenService подписывается и учитывает при начислении.
    /// </summary>
    public sealed class SatietyHandler : IEffectHandler
    {
        public string EffectId => "satiety";

        private readonly IPublisher<EarnMultiplierChangedMessage> _multiplierPub;

        public SatietyHandler(IPublisher<EarnMultiplierChangedMessage> multiplierPub)
            => _multiplierPub = multiplierPub;

        public void OnApply(ActiveEffect e)  => PublishMultiplier(e);
        public void OnStack(ActiveEffect e)  => PublishMultiplier(e);
        public void OnTick(ActiveEffect e, float dt) { } // модификатор не меняется в тике
        public void OnExpire(ActiveEffect e) => _multiplierPub.Publish(new EarnMultiplierChangedMessage(1f, "satiety_expired"));

        private void PublishMultiplier(ActiveEffect e)
            => _multiplierPub.Publish(new EarnMultiplierChangedMessage(1f + e.CurrentModifier, "satiety"));
    }
}