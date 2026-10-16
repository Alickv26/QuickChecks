using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QuickChecks.Input;

namespace QuickChecks.Multiplayer
{
    /// <summary>
    /// Manages local pass-and-play: 2-4 human players sharing one device.
    /// Shows "Pass to Player N" overlay between turns.
    ///
    /// This component subscribes to TurnManager events via OnEnable (with a
    /// 1-frame delay for reflection wiring). It does NOT need SetupPassPlay
    /// — it reads the player list from the TurnManager directly.
    /// </summary>
    public class PassPlayManager : MonoBehaviour
    {
        [SerializeField] private Racing.TurnManager turnManager;
        [SerializeField] private UI.PassPlayOverlay passOverlay;

        private bool _subscribed = false;

        private void OnEnable()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        private IEnumerator SubscribeWhenReady()
        {
            yield return null; // Wait 1 frame for reflection wiring

            if (turnManager != null && !_subscribed)
            {
                turnManager.OnTurnEnd += HandleTurnEnd;
                turnManager.OnTurnStart += HandleTurnStart;
                turnManager.OnRaceFinished += HandleRaceFinished;
                _subscribed = true;
                Debug.Log("[PassPlay] Subscribed to TurnManager events.");
            }
        }

        private void OnDisable()
        {
            if (turnManager != null && _subscribed)
            {
                turnManager.OnTurnEnd -= HandleTurnEnd;
                turnManager.OnTurnStart -= HandleTurnStart;
                turnManager.OnRaceFinished -= HandleRaceFinished;
                _subscribed = false;
            }
        }

        private void HandleTurnEnd(int playerIndex, int swipeCount)
        {
            // Use TurnManager's player list (not our own _players which is empty)
            int playerCount = turnManager?.Players?.Count ?? 0;
            if (playerCount == 0) return;

            int nextPlayer = (playerIndex + 1) % playerCount;
            if (passOverlay != null)
            {
                string nextName = turnManager.Players[nextPlayer].playerName;
                passOverlay.Show($"Pass to {nextName}");
            }
        }

        private void HandleTurnStart(int playerIndex)
        {
            if (passOverlay != null) passOverlay.Hide();

            // Decision 4: dim inactive karts to 40% alpha, restore active to 100%.
            UpdateKartVisibility(playerIndex);
        }

        private void UpdateKartVisibility(int activePlayerIndex)
        {
            if (turnManager == null || turnManager.Players == null) return;

            for (int i = 0; i < turnManager.Players.Count; i++)
            {
                var kart = turnManager.Players[i].kart;
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
                    ? $"{turnManager?.Players[winnerIndex]?.playerName ?? "Player"} wins!"
                    : "Race ended — no winner (all DNF).";
                passOverlay.Show(msg);
            }
        }
    }
}
