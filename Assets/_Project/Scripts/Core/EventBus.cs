using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuickChecks.Core
{
    /// <summary>
    /// Lightweight ScriptableObject-based event bus.
    /// Allows systems to communicate without direct references.
    /// Each event type is a separate ScriptableObject asset.
    /// </summary>
    public abstract class GameEvent { }

    [CreateAssetMenu(menuName = "Events/GameEvent", fileName = "NewGameEvent")]
    public class GameEventSO : ScriptableObject
    {
        private readonly List<Action> _listeners = new();

        public void Register(Action listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void Unregister(Action listener)
        {
            _listeners.Remove(listener);
        }

        public void Raise()
        {
            // Iterate backwards so listeners can unregister themselves.
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i].Invoke();
            }
        }
    }

    [CreateAssetMenu(menuName = "Events/GameEvent<T>", fileName = "NewGameEventT")]
    public class GameEventSO<T> : ScriptableObject
    {
        private readonly List<Action<T>> _listeners = new();

        public void Register(Action<T> listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void Unregister(Action<T> listener)
        {
            _listeners.Remove(listener);
        }

        public void Raise(T payload)
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i].Invoke(payload);
            }
        }
    }
}
