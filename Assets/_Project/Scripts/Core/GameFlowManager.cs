using System;
using UnityEngine;
using QuickChecks.Input;

namespace QuickChecks.Core
{
    /// <summary>
    /// Top-level state machine for the entire game flow.
    /// Boot -> Menu -> TrackSelect -> Race -> Results -> (loop)
    /// Subscribes to events from the EventBus to transition states.
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public enum GameState
        {
            Boot,
            MainMenu,
            TrackSelect,
            LoadingRace,
            Racing,
            Results,
            UploadingGhost
        }

        public static GameFlowManager Instance { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.Boot;

        public event Action<GameState, GameState> OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            TransitionTo(GameState.MainMenu);
        }

        public void TransitionTo(GameState newState)
        {
            if (newState == CurrentState) return;

            var previous = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(previous, newState);

            HandleStateEnter(newState);
        }

        private void HandleStateEnter(GameState state)
        {
            // TODO: Scene loading, UI transitions, etc.
            // For now, just logs — wire up in week 7.
            Debug.Log($"[GameFlow] {state}");
        }
    }
}
