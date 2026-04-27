using UnityEngine;
using Zenject;

namespace Archipelago.Effects
{
    public class TestPlayMode : MonoBehaviour
    {
        [Inject] private RoutineManager  _routineManager;
        [SerializeField] private RoutineDefinitionSO _eatRoutine;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5))
                _routineManager.ExecuteRoutine(_eatRoutine);
        }
    }
}