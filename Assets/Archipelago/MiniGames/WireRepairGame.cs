using System.Collections.Generic;
using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.MiniGames
{
    /// <summary>
    /// Починка соединения.
    /// N проводов случайного цвета, игрок соединяет пары.
    /// Quality = 1 если все правильно и в срок, 0 если время вышло.
    /// </summary>
    public sealed class WireRepairGame : IMiniGame
    {
        public string MiniGameId => "wire_repair";

        private const float TimeLimit  = 20f;
        private const int   WireCount  = 4;

        private float _elapsed;
        private int[] _solution;
        private bool[] _connected;
        private int   _selected = -1;

        public float TimeRemaining => Mathf.Max(0f, TimeLimit - _elapsed);
        public bool  IsComplete    => AllConnected();

        private readonly MiniGameManager                    _manager;
        private readonly IPublisher<WireRepairStateMessage> _statePub;

        [Inject]
        public WireRepairGame(
            MiniGameManager                    manager,
            IPublisher<WireRepairStateMessage> statePub)
        {
            _manager  = manager;
            _statePub = statePub;
        }

        public void Initialize()
        {
            _elapsed   = 0f;
            _connected = new bool[WireCount];
            _solution  = GenerateSolution(WireCount);
            _selected  = -1;
        }

        public void Begin() { }

        public void Tick(float dt)
        {
            _elapsed += dt;
            _statePub.Publish(new WireRepairStateMessage(_connected, TimeRemaining, _selected));

            if (_elapsed >= TimeLimit)
            {
                _manager.CompleteGame(MiniGameId, false, 0f);
                return;
            }

            if (IsComplete)
                _manager.CompleteGame(MiniGameId, true, 1f);
        }

        /// <summary>UI вызывает при клике на провод.</summary>
        public void SelectWire(int index)
        {
            if (_selected < 0)
            {
                _selected = index;
                return;
            }

            if (_solution[_selected] == index && _solution[index] == _selected)
            {
                _connected[_selected] = true;
                _connected[index]     = true;
            }

            _selected = -1;
        }

        public void End() { }

        private bool AllConnected()
        {
            foreach (var c in _connected)
                if (!c) return false;
            return true;
        }

        // Fisher-Yates shuffle для генерации пар
        private static int[] GenerateSolution(int count)
        {
            var indices  = new List<int>();
            for (int i = 0; i < count; i++) indices.Add(i);

            var solution = new int[count];
            while (indices.Count > 0)
            {
                int a = indices[Random.Range(0, indices.Count)];
                indices.Remove(a);
                int b = indices[Random.Range(0, indices.Count)];
                indices.Remove(b);
                solution[a] = b;
                solution[b] = a;
            }
            return solution;
        }
    }
}
