using System;
using UnityEngine;
using TMPro;
using QuickChecks.Racing;

namespace QuickChecks.Prototype
{
    /// <summary>
    /// On-screen debug HUD for the polished Week 1 prototype.
    /// Supports both TMP_Text and legacy UnityEngine.UI.Text — set whichever
    /// you're using via the Inspector. The bootstrapper wires whichever one
    /// it managed to create.
    /// </summary>
    public class PrototypeHUD : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrapper raceStarter;
        [SerializeField] private KartController kart;
        [SerializeField] private TMP_Text statusTextTMP;
        [SerializeField] private UnityEngine.UI.Text statusTextLegacy;

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
            string text = BuildStatusText();
            if (!string.IsNullOrEmpty(text)) SetText(text);
        }

        private string BuildStatusText()
        {
            if (_isFinished && _finishMs >= 0)
            {
                int par = raceStarter != null ? raceStarter.ParSwipes : 0;
                int best = raceStarter?.SoloGhost?.BestGhost?.swipeCount ?? _finishSwipes;
                long bestTime = raceStarter?.SoloGhost?.BestGhost?.finishTimeMs ?? _finishMs;

                int deltaVsPar = _finishSwipes - par;
                int deltaVsBest = _finishSwipes - best;
                string parStr = deltaVsPar <= 0
                    ? $"{_finishSwipes} (par -{Math.Abs(deltaVsPar)})"
                    : $"{_finishSwipes} (par +{deltaVsPar})";

                string bestStr = best == _finishSwipes
                    ? "NEW BEST!"
                    : $"+{deltaVsBest} vs best ({best})";

                return
                    "RACE COMPLETE\n\n" +
                    $"Swipes: {parStr}\n" +
                    $"Time: {_finishMs / 1000f:F2}s\n" +
                    $"Best: {best} swipes / {bestTime / 1000f:F2}s\n" +
                    $"{bestStr}\n\n" +
                    "Press R to retry";
            }

            if (kart == null) return "";

            float speed = kart.Velocity.magnitude;
            int currentSwipes = raceStarter != null ? raceStarter.SwipeCount : 0;
            int par = raceStarter != null ? raceStarter.ParSwipes : 0;
            int best = raceStarter?.SoloGhost?.BestGhost?.swipeCount ?? 0;
            bool hasGhost = raceStarter?.SoloGhost?.HasBestRun ?? false;

            string ghostStr = hasGhost
                ? $"Ghost active ({best} swipes)"
                : "No ghost yet - finish a run";

            return
                "QuickChecks Prototype\n\n" +
                $"Swipes: {currentSwipes} / par {par}\n" +
                $"Velocity: {speed:F0} u/s\n" +
                $"Stopped: {kart.IsStopped}   Off-track: {kart.IsOffTrack}\n\n" +
                $"{ghostStr}\n\n" +
                "Flick to move - Pinch to zoom - R = reset - C = clear best";
        }

        private void SetText(string text)
        {
            if (statusTextTMP != null) statusTextTMP.text = text;
            else if (statusTextLegacy != null) statusTextLegacy.text = text;
        }

        private void HandleRaceFinished(int swipes, long timeMs)
        {
            _finishSwipes = swipes;
            _finishMs = timeMs;
            _isFinished = true;
        }
    }
}
