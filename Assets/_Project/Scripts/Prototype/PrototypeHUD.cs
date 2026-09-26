using System;
using System.Reflection;
using UnityEngine;
using TMPro;
using QuickChecks.Racing;

namespace QuickChecks.Prototype
{
    /// <summary>
    /// On-screen debug HUD for the polished Week 1 prototype.
    /// Shows: swipe count (current / par / best), velocity, kart state, race time,
    /// finish status with delta vs best.
    /// </summary>
    public class PrototypeHUD : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrapper raceStarter;
        [SerializeField] private KartController kart;
        [SerializeField] private TMP_Text statusText;

        private long _finishMs = -1;
        private int _finishSwipes = -1;
        private bool _isFinished;

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

            if (_isFinished && _finishMs >= 0)
            {
                int par = raceStarter != null ? raceStarter.ParSwipes : 0;
                int best = raceStarter?.SoloGhost?.BestGhost?.swipeCount ?? _finishSwipes;
                long bestTime = raceStarter?.SoloGhost?.BestGhost?.finishTimeMs ?? _finishMs;

                int deltaVsPar = _finishSwipes - par;
                int deltaVsBest = _finishSwipes - best;
                string parStr = deltaVsPar <= 0
                    ? $"<color=#3FE0C2><b>{_finishSwipes}</b> (par -{Math.Abs(deltaVsPar)})</color>"
                    : $"<color=#FFD166><b>{_finishSwipes}</b> (par +{deltaVsPar})</color>";

                string bestStr = best == _finishSwipes
                    ? "<color=#3FE0C2>NEW BEST!</color>"
                    : $"<color=#7B8A8A>+{deltaVsBest} vs best ({best})</color>";

                statusText.text =
                    $"<b>RACE COMPLETE</b>\n\n" +
                    $"Swipes: {parStr}\n" +
                    $"Time: <color=#3FE0C2><b>{_finishMs / 1000f:F2}s</b></color>\n" +
                    $"Best: <b>{best}</b> swipes / <b>{bestTime / 1000f:F2}s</b>\n" +
                    $"{bestStr}\n\n" +
                    $"<i>Press <b>R</b> to retry</i>";
                return;
            }

            if (kart == null) return;

            float speed = kart.Velocity.magnitude;
            int currentSwipes = raceStarter != null ? raceStarter.SwipeCount : 0;
            int par = raceStarter != null ? raceStarter.ParSwipes : 0;
            int best = raceStarter?.SoloGhost?.BestGhost?.swipeCount ?? 0;
            bool hasGhost = raceStarter?.SoloGhost?.HasBestRun ?? false;

            string ghostStr = hasGhost
                ? $"<color=#7B8A8A>Ghost active ({best} swipes)</color>"
                : "<color=#7B8A8A>No ghost yet — finish a run</color>";

            statusText.text =
                $"<b>QuickChecks Prototype</b>\n\n" +
                $"Swipes: <color=#3FE0C2><b>{currentSwipes}</b></color> / par {par}\n" +
                $"Velocity: <b>{speed:F0}</b> u/s\n" +
                $"Stopped: <b>{kart.IsStopped}</b>   Off-track: <b>{kart.IsOffTrack}</b>\n\n" +
                $"{ghostStr}\n\n" +
                $"<i>Flick to move • Pinch to zoom • R = reset • C = clear best</i>";
        }

        private void HandleRaceFinished(int swipes, long timeMs)
        {
            _finishSwipes = swipes;
            _finishMs = timeMs;
            _isFinished = true;
        }
    }
}
