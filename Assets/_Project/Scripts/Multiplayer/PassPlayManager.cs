using System.Collections.Generic;
using UnityEngine;
using QuickChecks.Input;

namespace QuickChecks.Multiplayer
{
    /// <summary>
    /// Manages local pass-and-play: 2-4 human players sharing one device.
    /// Each player picks a kart, then takes turns in order. Between turns,
    /// a full-screen "Pass to Player N" overlay is shown (UI layer).
    ///
    /// Flow:
    ///   1. SetupPassPlay(playerCount) -> creates PlayerState list with distinct kart colors
    ///   2. TurnManager drives the actual turn logic
    ///   3. Between turns, this manager shows the pass overlay via OnTurnEnd event
    ///   4. Player taps overlay -> OnTurnStart fires for next player
    /// </summary>
    public class PassPlayManager : MonoBehaviour
    {
        [SerializeField] private Racing.TurnManager turnManager;
        [SerializeField] private List<GameObject> kartPrefabs; // one per player slot
        [SerializeField] private UI.PassPlayOverlay passOverlay; // UI controller

        private List<Racing.TurnManager.PlayerState> _players = new();

        public void SetupPassPlay(int playerCount, Track.TrackDefinition track)
        {
            if (playerCount < 2 || playerCount > 4)
            {
                Debug.LogError($"[PassPlay] Invalid player count: {playerCount}. Must be 2-4.");
                return;
            }

            _players.Clear();

            for (int i = 0; i < playerCount; i++)
            {
                var prefab = kartPrefabs[i % kartPrefabs.Count];
                var kartGo = Instantiate(prefab);
                var state = new Racing.TurnManager.PlayerState
                {
                    playerName = $"Player {i + 1}",
                    kart = kartGo.GetComponent<Racing.KartController>(),
                    isCPU = false,
                    isFinished = false,
                    swipeCount = 0,
                    finishTimeMs = 0,
                    checkpointIndex = 0
                };
                _players.Add(state);
            }

            turnManager.OnTurnEnd += HandleTurnEnd;
            turnManager.OnTurnStart += HandleTurnStart;
            turnManager.OnRaceFinished += HandleRaceFinished;

            turnManager.StartRace(_players, track, Racing.TurnManager.RaceMode.PassAndPlay);
        }

        private void HandleTurnEnd(int playerIndex, int swipeCount)
        {
            // Show "Pass to Player N" overlay.
            int nextPlayer = (playerIndex + 1) % _players.Count;
            if (passOverlay != null)
            {
                passOverlay.Show($"Pass to Player {nextPlayer + 1}");
            }
        }

        private void HandleTurnStart(int playerIndex)
        {
            // Hide overlay when new player is ready.
            if (passOverlay != null) passOverlay.Hide();

            // Decision 4 (DECISIONS.md): dim inactive karts to 40% alpha, restore active to 100%.
            UpdateKartVisibility(playerIndex);
        }

        /// <summary>
        /// All karts remain visible (preserves spatial context), but inactive karts
        /// are dimmed to 40% alpha so visual focus is on the active player.
        /// </summary>
        private void UpdateKartVisibility(int activePlayerIndex)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                var kart = _players[i].kart;
                if (kart == null) continue;
                var renderer = kart.GetComponent<SpriteRenderer>();
                if (renderer == null) continue;

                var color = renderer.color;
                color.a = (i == activePlayerIndex) ? 1f : 0.4f;
                renderer.color = color;
            }
        }

        private void HandleRaceFinished(int winnerIndex)
        {
            if (passOverlay != null)
            {
                string msg = winnerIndex >= 0
                    ? $"Player {winnerIndex + 1} wins!"
                    : "Race ended — no winner (all DNF).";
                passOverlay.Show(msg);
            }

            turnManager.OnTurnEnd -= HandleTurnEnd;
            turnManager.OnTurnStart -= HandleTurnStart;
            turnManager.OnRaceFinished -= HandleRaceFinished;
        }

        private void OnDestroy()
        {
            // Defensive: ensure we never leak event subscriptions.
            if (turnManager != null)
            {
                turnManager.OnTurnEnd -= HandleTurnEnd;
                turnManager.OnTurnStart -= HandleTurnStart;
                turnManager.OnRaceFinished -= HandleRaceFinished;
            }
        }
    }
}
