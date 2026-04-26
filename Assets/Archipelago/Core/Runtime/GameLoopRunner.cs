using Archipelago.Session;
using UnityEngine;
using Zenject;

namespace Archipelago.Core
{
    public sealed class GameLoopRunner : MonoBehaviour
    {
        private GameClock _gameClock;

        // Zenject вызывает метод помеченный [Inject] после резолюции —
        // это надёжнее чем [Inject] на поле для MonoBehaviour из FromInstance.
        [Inject]
        public void Construct(GameClock gameClock)
        {
            _gameClock = gameClock;
        }

        private void Update()
        {
            _gameClock?.Tick(Time.deltaTime);
        }
    }
}