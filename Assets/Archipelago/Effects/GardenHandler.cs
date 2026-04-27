// Assets/Effects/Handlers/GardenHandler.cs
using Archipelago.Core;
using MessagePipe;

namespace Archipelago.Effects
{
    /// <summary>
    /// Полив сада: модифицирует скорость накопления зелёных токенов.
    /// </summary>
    public sealed class GardenHandler : IEffectHandler
    {
        public string EffectId => "garden_watered";

        private readonly IPublisher<GardenMultiplierChangedMessage> _gardenPub;

        public GardenHandler(IPublisher<GardenMultiplierChangedMessage> gardenPub)
            => _gardenPub = gardenPub;

        public void OnApply(ActiveEffect e)          => PublishMultiplier(e);
        public void OnStack(ActiveEffect e)          => PublishMultiplier(e);
        public void OnTick(ActiveEffect e, float dt) { }
        public void OnExpire(ActiveEffect e)         => _gardenPub.Publish(new GardenMultiplierChangedMessage(1f, "garden_expired"));

        private void PublishMultiplier(ActiveEffect e)
            => _gardenPub.Publish(new GardenMultiplierChangedMessage(1f + e.CurrentModifier, "garden_watered"));
    }
}