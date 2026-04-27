// Assets/Effects/Handlers/CleanHandler.cs
using Archipelago.Core;
using MessagePipe;

namespace Archipelago.Effects
{
    /// <summary>
    /// Чистота: только визуальный сигнал. Никакой механики.
    /// UI/VFX подписываются на CleanStateChangedMessage и реагируют.
    /// </summary>
    public sealed class CleanHandler : IEffectHandler
    {
        public string EffectId => "clean";

        private readonly IPublisher<CleanStateChangedMessage> _cleanPub;

        public CleanHandler(IPublisher<CleanStateChangedMessage> cleanPub)
            => _cleanPub = cleanPub;

        public void OnApply(ActiveEffect e)          => _cleanPub.Publish(new CleanStateChangedMessage(true));
        public void OnStack(ActiveEffect e)          { } // maxStacks=1, никогда не вызывается
        public void OnTick(ActiveEffect e, float dt) { }
        public void OnExpire(ActiveEffect e)         => _cleanPub.Publish(new CleanStateChangedMessage(false));
    }
}