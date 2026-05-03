namespace Archipelago.MiniGames
{
    public interface IMiniGame
    {
        string MiniGameId { get; }

        /// <summary>Подготовить данные до показа UI.</summary>
        void Initialize();

        /// <summary>Показать UI, начать игровой цикл.</summary>
        void Begin();

        /// <summary>Вызывается каждый кадр пока игра активна.</summary>
        void Tick(float deltaTime);

        /// <summary>Завершить принудительно (отмена, таймаут).</summary>
        void End();
    }
}
