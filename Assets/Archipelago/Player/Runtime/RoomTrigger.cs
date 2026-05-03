using Archipelago.Core;
using MessagePipe;
using UnityEngine;
using Zenject;

namespace Archipelago.Environment
{
    /// <summary>
    /// Trigger-зона комнаты. При входе игрока публикует RoomChangedMessage.
    /// Прикрепить на GameObject с Collider (Is Trigger = true) в каждой зоне.
    ///
    /// roomId должен совпадать с одним из:
    /// "hub" | "garden" | "gallery" | "residential" | "generator" | "reservoir" | "street"
    /// </summary>
    public sealed class RoomTrigger : MonoBehaviour
    {
        [SerializeField] private string _roomId = "hub";

        [Inject] private IPublisher<RoomChangedMessage> _roomPub;

        private void OnTriggerEnter(Collider other)
        {
            // Проверяем что это игрок (тег Player)
            if (!other.CompareTag("Player")) return;
            _roomPub.Publish(new RoomChangedMessage(_roomId));
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.2f);
            var col = GetComponent<Collider>();
            if (col != null) Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            UnityEditor.Handles.Label(transform.position + Vector3.up, $"Room: {_roomId}");
        }
#endif
    }
}
