using Archipelago.Economy;
using Archipelago.MiniGames;
using UnityEngine;
using Zenject;

namespace Archipelago.Effects
{
    public class TestPlayMode : MonoBehaviour
    {
        [Inject] private MiniGameManager _miniGameManager;
        [Inject] private GardenService   _gardenService;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6))
                _miniGameManager.StartGame("calibration");

            if (Input.GetKeyDown(KeyCode.F7))
            {
                int collected = _gardenService.Collect();
                Debug.Log($"Collected {collected} green tokens");
            }
        }
    }
}