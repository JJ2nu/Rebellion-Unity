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

    // ── Built-in event structs ─────────────────────────────────────────────
    public struct PlayerDeathEvent { }
    public struct EnemyKilledEvent { public int ExpValue; }
    public struct LevelCompleteEvent { public int Score; }
    public struct BossEncounterEvent { public string BossName; }

    // ── Tactical battle events (adapted from 4Q-Rebellion game flow) ───────

    /// <summary>
    /// Published by BattleManager when the action phase ends.
    /// IsVictory = true  → all enemies defeated (any of the win conditions).
    /// IsVictory = false → all allies defeated (Lose / AllyDeadLose).
    /// ResultCode mirrors the eBattleResult enum from 4Q-Rebellion.
    /// </summary>
    public struct BattleFinishedEvent
    {
        public bool IsVictory;
        public int  ResultCode; // Matches BattleResult enum value.
    }

    /// <summary>
    /// Published when the player requests to retry (ResetGame).
    /// </summary>
    public struct BattleRetryRequestedEvent { }
}
