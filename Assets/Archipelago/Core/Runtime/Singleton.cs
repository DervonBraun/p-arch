using UnityEngine;

namespace Archipelago.Core
{
    /// <summary>
    /// Base class for MonoBehaviours that must have exactly one instance per scene.
    /// Does NOT call DontDestroyOnLoad — lifecycle is managed by Zenject SceneContext.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}