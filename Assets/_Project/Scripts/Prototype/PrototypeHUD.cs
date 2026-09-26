using System;
using System.Reflection;
using UnityEngine;
using TMPro;
using QuickChecks.Racing;

namespace QuickChecks.Prototype
{
    /// <summary>
    /// On-screen debug HUD for the Week 1 prototype.
    /// Shows: swipe count, kart velocity, kart state, race time, finish status.
    /// </summary>
    public class PrototypeHUD : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrapper raceStarter;
        [SerializeField] private KartController kart;
        [SerializeField] private TMP_Text statusText;

        private long _finishMs = -1;
        private int _finishSwipes = -1;

        private void OnEnable()
        {
            if (raceStarter != null) raceStarter.OnRaceFinished += HandleRaceFinished;
        }

        private void OnDisable()
        {
            if (raceStarter != null) raceStarter.OnRaceFinished -= HandleRaceFinished;
        }

        private void Update()
        {
            if (statusText == null) return;

            if (raceStarter != null && raceStarter.IsFinished && _finishMs >= 0)
            {
                statusText.text =
                    $"<b>RACE COMPLETE</b>\n\n" +
                    $"Swipes: <color=#3FE0C2><b>{_finishSwipes}</b></color>\n" +
                    $"Time: <color=#3FE0C2><b>{_finishMs / 1000f:F2}s</b></color>\n\n" +
                                    $"Press <b>R</b> to restart";
                return;
            }

            if (kart == null) return;

            float speed = kart.Velocity.magnitude;
            statusText.text =
                $"<b>QuickChecks Prototype</b>\n\n" +
                $"Swipes: <color=#3FE0C2><b>{(raceStarter != null ? raceStarter.SwipeCount : 0)}</b></color>\n" +
                $"Velocity: <b>{speed:F0}</b> u/s\n" +
                $"Stopped: <b>{kart.IsStopped}</b>\n" +
                $"Off-track: <b>{kart.IsOffTrack}</b>\n\n" +
                $"<i>Flick to move • Pinch to zoom • R to reset</i>";
        }

        private void HandleRaceFinished(int swipes, long timeMs)
        {
            _finishSwipes = swipes;
            _finishMs = timeMs;
        }
    }
}
