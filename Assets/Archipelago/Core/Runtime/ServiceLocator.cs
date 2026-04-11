using System;
using System.Collections.Generic;
using UnityEngine;

namespace Archipelago.Core
{
    /// <summary>
    /// Global service registry. All systems register here and resolve dependencies
    /// without direct references. Thread-safe for reads; writes must happen on main thread.
    /// </summary>
    public static class ServiceLocator
    {
        // THREAD: Dictionary reads are safe from any thread. Writes (Register/Unregister)
        //         must be called from the main thread only (Bootstrap phase).
        private static readonly Dictionary<Type, object> _services = new(32);

        /// <summary>
        /// Registers a service instance under its interface type.
        /// Call during Bootstrap before any system tries to resolve it.
        /// </summary>
        public static void Register<TInterface>(TInterface instance)
        {
            var type = typeof(TInterface);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Overwriting existing registration for {type.Name}");
            }
            _services[type] = instance;
        }

        /// <summary>
        /// Resolves a registered service. Throws InvalidOperationException if not found.
        /// Use TryGet for optional dependencies.
        /// </summary>
        public static TInterface Get<TInterface>()
        {
            var type = typeof(TInterface);
            if (_services.TryGetValue(type, out var service))
                return (TInterface)service;

            throw new InvalidOperationException(
                $"[ServiceLocator] Service '{type.Name}' is not registered. " +
                $"Ensure it is registered in GameBootstrapper before use.");
        }

        /// <summary>
        /// Attempts to resolve a service without throwing. Returns false if not found.
        /// </summary>
        public static bool TryGet<TInterface>(out TInterface service)
        {
            if (_services.TryGetValue(typeof(TInterface), out var raw))
            {
                service = (TInterface)raw;
                return true;
            }
            service = default;
            return false;
        }

        /// <summary>
        /// Unregisters a service. Call during teardown / scene unload.
        /// </summary>
        public static void Unregister<TInterface>()
        {
            _services.Remove(typeof(TInterface));
        }

        /// <summary>
        /// Clears all registrations. Use only in tests or full application shutdown.
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
        }

        /// <summary>Returns true if the service is currently registered.</summary>
        public static bool IsRegistered<TInterface>() =>
            _services.ContainsKey(typeof(TInterface));
    }
}