using System;

namespace Archipelago.PlayerProfile
{
    /// <summary>
    /// Сериализуемый контейнер профиля игрока.
    /// Синхронизируется на сервер через PUT /profile/{playerId}.
    /// Сохраняется локально как часть SaveData.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfileData
    {
        public string         PlayerId    = "dev_player_01";
        public BehaviorMetrics Metrics    = new();
        public FlagProfile     Flags      = new();
        public DateTime        CreatedAt  = DateTime.UtcNow;
        public DateTime        UpdatedAt  = DateTime.UtcNow;
    }
}
