using UnityEngine;

namespace Archipelago.Scanner
{
    /// <summary>
    /// Компонент на GameObject который можно сканировать.
    /// Требует Collider на том же или дочернем GameObject для raycast.
    ///
    /// Сцена: добавь этот компонент на любой объект,
    /// назначь ScannableObjectSO в поле Data.
    /// </summary>
    public sealed class ScannableObject : MonoBehaviour
    {
        [SerializeField] private ScannableObjectSO _data;

        public ScannableObjectSO Data => _data;

        private void Awake()
        {
            if (_data == null)
                Debug.LogWarning($"[ScannableObject] '{gameObject.name}' has no ScannableObjectSO assigned.", this);
        }
    }
}