using UnityEngine;
using FMODUnity; // Добавляем интеграцию FMOD

namespace Archipelago.Player
{
    public sealed class FootstepPlayer : MonoBehaviour
    {
        [Header("FMOD Settings")]
        [SerializeField] private EventReference _footstepEvent;
        [SerializeField] private string _surfaceParameterName = "Surface";

        [Header("Database")]
        [SerializeField] private FootstepSurfaceDatabase _database;

        [Header("Raycast")]
        [SerializeField] private float _raycastDistance = 1.2f;
        [SerializeField] private LayerMask _groundLayers = ~0;

        private PhysicsMaterial _lastMaterial;

        public void PlayStep()
        {
            if (_footstepEvent.IsNull) return;

            PhysicsMaterial surface = ResolveSurface();
            
            // Получаем индекс поверхности из базы (просто int или float для параметра)
            if (!_database.TryResolve(surface, out float parameterValue))
                return;

            // Создаем инстанс события
            var instance = RuntimeManager.CreateInstance(_footstepEvent);
            
            // Привязываем к объекту для работы Steam Audio (позиционирование)
            RuntimeManager.AttachInstanceToGameObject(instance, transform);
            
            // Устанавливаем поверхность
            instance.setParameterByName(_surfaceParameterName, parameterValue);
            
            // Играем и освобождаем память
            instance.start();
            instance.release();
        }

        private PhysicsMaterial ResolveSurface()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, _raycastDistance, _groundLayers, QueryTriggerInteraction.Ignore))
            {
                _lastMaterial = hit.collider.sharedMaterial;
            }
            return _lastMaterial;
        }
    }
}