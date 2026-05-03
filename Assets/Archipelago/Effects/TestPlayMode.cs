using Archipelago.SaveSystem;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Archipelago.Effects
{
    public class TestPlayMode : MonoBehaviour
    {
        [Inject] private SaveService _saveService;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
                _saveService.SaveAsync(0).Forget();

            if (Input.GetKeyDown(KeyCode.F11))
                _saveService.LoadAsync(0).Forget();
        }
    }
}