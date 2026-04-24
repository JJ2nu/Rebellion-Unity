using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion.Utils
{
    /// <summary>
    /// Lightweight event bus for decoupled communication between systems.
    /// Any system can publish or subscribe to typed events without holding references.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> subscribers = new Dictionary<Type, List<Delegate>>();

        public static void Subscribe<T>(Action<T> callback)
        {
            Type type = typeof(T);
            if (!subscribers.ContainsKey(type))
                subscribers[type] = new List<Delegate>();

            subscribers[type].Add(callback);
        }

        public static void Unsubscribe<T>(Action<T> callback)
        {
            Type type = typeof(T);
            if (subscribers.ContainsKey(type))
                subscribers[type].Remove(callback);
        }

        public static void Publish<T>(T eventData)
        {
            Type type = typeof(T);
            if (!subscribers.ContainsKey(type)) return;

            foreach (Delegate del in subscribers[type])
                (del as Action<T>)?.Invoke(eventData);
        }

        public static void Clear()
        {
            subscribers.Clear();
        }
    }

    // Example event structs – add more as needed.
    public struct PlayerDeathEvent { }
    public struct EnemyKilledEvent { public int ExpValue; }
    public struct LevelCompleteEvent { public int Score; }
    public struct BossEncounterEvent { public string BossName; }
}
