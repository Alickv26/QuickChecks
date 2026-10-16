using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Input;
using QuickChecks.Racing;
using QuickChecks.Ghost;
using QuickChecks.Core;
using QuickChecks.Audio;
using QuickChecks.Track;
using QuickChecks.Camera;

namespace QuickChecks.Prototype
{
    /// <summary>
    /// Turn flow debugger. Attaches to the RaceSceneController and logs every
    /// event in the turn-based cycle so we can see exactly what's happening.
    ///
    /// Usage: automatically added by RaceSceneController in multiplayer mode.
    /// Check Console for [TurnFlow] logs.
    /// </summary>
    public class TurnFlowDebugger : MonoBehaviour
    {
        private Racing.TurnManager _turnManager;
        private List<KartController> _allKarts;
        private List<SwipeDetector> _allSwipeDetectors;

        public void Initialize(Racing.TurnManager turnManager, List<KartController> karts, List<SwipeDetector> detectors)
        {
            _turnManager = turnManager;
            _allKarts = karts;
            _allSwipeDetectors = detectors;

            if (_turnManager != null)
            {
                _turnManager.OnTurnStart += (idx) => LogState($"OnTurnStart({idx})");
                _turnManager.OnTurnEnd += (idx, swipes) => LogState($"OnTurnEnd({idx}, swipes={swipes})");
                _turnManager.OnRaceFinished += (idx) => LogState($"OnRaceFinished(winner={idx})");
            }

            Debug.Log("[TurnFlow] Debugger initialized. Will log all turn events.");
        }

        private void LogState(string trigger)
        {
            if (_turnManager == null) return;

            string state = $"[TurnFlow] {trigger}\n";
            state += $"  Phase: {_turnManager.Phase}\n";
            state += $"  CurrentPlayer: {_turnManager.CurrentPlayerIndex}\n";
            state += $"  Players:\n";
            for (int i = 0; i < _turnManager.Players.Count; i++)
            {
                var p = _turnManager.Players[i];
                state += $"    [{i}] {p.playerName}: swipes={p.swipeCount}, finished={p.isFinished}";
                if (p.kart != null)
                {
                    state += $", vel={p.kart.Velocity.magnitude:F1}, stopped={p.kart.IsStopped}";
                }
                if (_allSwipeDetectors != null && i < _allSwipeDetectors.Count && _allSwipeDetectors[i] != null)
                {
                    state += $", detectorEnabled={_allSwipeDetectors[i].enabled}";
                }
                state += "\n";
            }
            Debug.Log(state);
        }
    }
}
