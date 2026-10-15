using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using QuickChecks.Racing;
using QuickChecks.Core;

namespace QuickChecks.Prototype
{
    /// <summary>
    /// On-screen HUD for the prototype. Shows:
    /// - Swipe count / par / best
    /// - Lap completion message (when CheckpointTracker fires OnLapCompleted)
    /// - Active + held power-up indicators
    /// - Ghost status
    /// - Velocity, stopped, off-track state
    /// </summary>
    public class PrototypeHUD : MonoBehaviour
    {
        [SerializeField] private IRaceStarter raceStarter;
        [SerializeField] private KartController kart;
        [SerializeField] private TMP_Text statusTextTMP;
        [SerializeField] private UnityEngine.UI.Text statusTextLegacy;

        [Header("Lap Notification")]
        [SerializeField] private TMP_Text lapNotificationTMP;
        [SerializeField] private UnityEngine.UI.Text lapNotificationLegacy;
        [SerializeField] private float lapNotificationDuration = 2f;
        private float _lapNotificationTimer = 0f;

        [Header("Power-Up Display")]
        [SerializeField] private TMP_Text powerUpTextTMP;
        [SerializeField] private UnityEngine.UI.Text powerUpTextLegacy;

        [Header("Reset Button (Mobile)")]
        [SerializeField] private Button resetButton;

        private long _finishMs = -1;
        private int _finishSwipes = -1;
        private bool _isFinished;
        private bool _isNewBest = false;
        private int _newBestSwipes = 0;
        private long _newBestTimeMs = 0;

        // Lap tracking
        private int _currentLap = 0;
        private int _totalLaps = 1;

        // Power-up tracking
        private PowerUpSystem _powerUpSystem;
        private string _lastHeldPowerUpId = "";
        private string _lastActivePowerUpId = "";

        private void OnEnable()
        {
            // Don't subscribe to events here — resetButton isn't wired yet (field set via reflection after AddComponent).
            // Event subscriptions happen in Start() after all fields are wired.
        }

        private void OnDisable()
        {
            if (raceStarter != null)
            {
                raceStarter.OnRaceFinished -= HandleRaceFinished;
                raceStarter.OnGhostUpdated -= HandleGhostUpdated;
            }
            if (resetButton != null) resetButton.onClick.RemoveListener(OnResetClicked);
        }

        private void Start()
        {
            // Subscribe to events (after all fields are wired by the bootstrapper via reflection)
            if (raceStarter != null)
            {
                raceStarter.OnRaceFinished += HandleRaceFinished;
                raceStarter.OnGhostUpdated += HandleGhostUpdated;
            }
            // Wire reset button click listener (resetButton field is now set)
            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(OnResetClicked);
                Debug.Log("[HUD] Reset button onClick listener wired.");
            }

            // Find PowerUpSystem on the kart (wired by bootstrapper)
            if (kart != null)
            {
                _powerUpSystem = kart.GetComponent<PowerUpSystem>();

                // Find CheckpointTracker for lap events
                var tracker = kart.GetComponent<CheckpointTracker>();
                if (tracker != null)
                {
                    tracker.OnLapCompleted += HandleLapCompleted;
                    tracker.OnRaceComplete += HandleRaceComplete;
                    _totalLaps = tracker.TotalLaps;
                }
            }
        }

        private void Update()
        {
            string text = BuildStatusText();
            if (!string.IsNullOrEmpty(text)) SetText(text);

            UpdatePowerUpDisplay();
            UpdateLapNotification();
        }

        private string BuildStatusText()
        {
            if (_isFinished && _finishMs >= 0)
            {
                int finishedPar = raceStarter != null ? raceStarter.ParSwipes : 0;
                int bestSwipes = raceStarter?.SoloGhost?.BestGhost?.swipeCount ?? _finishSwipes;
                long bestTime = raceStarter?.SoloGhost?.BestGhost?.finishTimeMs ?? _finishMs;

                int deltaVsPar = _finishSwipes - finishedPar;
                string parStr = deltaVsPar <= 0
                    ? $"{_finishSwipes} (par -{Math.Abs(deltaVsPar)})"
                    : $"{_finishSwipes} (par +{deltaVsPar})";

                string bestStr = _isNewBest
                    ? $"*** NEW FASTEST LAP! ***\n{_newBestSwipes} swipes in {_newBestTimeMs / 1000f:F2}s\nGhost updated!"
                    : $"Best: {bestSwipes} swipes / {bestTime / 1000f:F2}s\n(+{_finishSwipes - bestSwipes} vs best)";

                return
                    "RACE COMPLETE\n\n" +
                    $"Swipes: {parStr}\n" +
                    $"Time: {_finishMs / 1000f:F2}s\n\n" +
                    $"{bestStr}\n\n" +
                    "Tap RESET to retry";
            }

            if (kart == null) return "";

            float speed = kart.Velocity.magnitude;
            int currentSwipes = raceStarter != null ? raceStarter.SwipeCount : 0;
            int currentPar = raceStarter != null ? raceStarter.ParSwipes : 0;
            int currentBest = raceStarter?.SoloGhost?.BestGhost?.swipeCount ?? 0;
            bool hasGhost = raceStarter?.SoloGhost?.HasBestRun ?? false;

            string ghostStr = hasGhost
                ? $"Ghost active ({currentBest} swipes)"
                : "No ghost - finish a lap";

            string lapStr = _totalLaps > 1
                ? $"Lap: {_currentLap + 1}/{_totalLaps}\n"
                : "";

            return
                "QuickChecks Prototype\n\n" +
                lapStr +
                $"Swipes: {currentSwipes} / par {currentPar}\n" +
                $"Velocity: {speed:F0} u/s\n" +
                $"Stopped: {kart.IsStopped}   Off-track: {kart.IsOffTrack}\n\n" +
                $"{ghostStr}\n\n" +
                "Flick to move - Pinch to zoom";
        }

        private void UpdatePowerUpDisplay()
        {
            if (_powerUpSystem == null)
            {
                _powerUpSystem = kart?.GetComponent<PowerUpSystem>();
                if (_powerUpSystem == null) return;
            }

            string heldId = _powerUpSystem.HasHeldPowerUp ? _powerUpSystem.HeldPowerUp.powerUpId : "";
            string activeId = _powerUpSystem.HasActiveEffect ? _powerUpSystem.ActivePowerUp.powerUpId : "";
            float timeRemaining = _powerUpSystem.EffectTimeRemaining;

            // Only update if changed (avoid string allocations every frame)
            if (heldId == _lastHeldPowerUpId && activeId == _lastActivePowerUpId) return;
            _lastHeldPowerUpId = heldId;
            _lastActivePowerUpId = activeId;

            string text = "";
            if (!string.IsNullOrEmpty(activeId))
            {
                text += $"Active: {activeId.ToUpper()}";
                if (timeRemaining > 0f) text += $" ({timeRemaining:F1}s)";
                text += "\n";
            }
            if (!string.IsNullOrEmpty(heldId))
            {
                text += $"Held: {heldId.ToUpper()} (tap to use)";
            }

            SetPowerUpText(text);
        }

        private void UpdateLapNotification()
        {
            if (_lapNotificationTimer > 0f)
            {
                _lapNotificationTimer -= Time.deltaTime;
                if (_lapNotificationTimer <= 0f)
                {
                    SetLapNotification("");
                }
            }
        }

        private void HandleLapCompleted(int lapNumber)
        {
            _currentLap = lapNumber;
            string msg = lapNumber >= _totalLaps
                ? "FINAL LAP COMPLETE!"
                : $"LAP {lapNumber} COMPLETE!";
            SetLapNotification(msg);
            _lapNotificationTimer = lapNotificationDuration;
            Debug.Log($"[HUD] {msg}");
        }

        private void HandleRaceComplete(int totalCheckpoints)
        {
            SetLapNotification("RACE COMPLETE!");
            _lapNotificationTimer = lapNotificationDuration;
        }

        private void HandleRaceFinished(int swipes, long timeMs)
        {
            _finishSwipes = swipes;
            _finishMs = timeMs;
            _isFinished = true;
        }

        private void HandleGhostUpdated(int swipes, long timeMs, bool isNewBest)
        {
            if (isNewBest)
            {
                SetLapNotification($"NEW FASTEST LAP!\n{swipes} swipes in {timeMs / 1000f:F2}s");
                _lapNotificationTimer = 5f;
                _isNewBest = true;
                _newBestSwipes = swipes;
                _newBestTimeMs = timeMs;
                Debug.Log($"[HUD] NEW FASTEST LAP! {swipes} swipes, {timeMs / 1000f:F2}s. New ghost saved.");
            }
            else
            {
                Debug.Log($"[HUD] Race finished but not a new best (swipes={swipes}).");
            }
        }

        private void OnResetClicked()
        {
            if (raceStarter != null)
            {
                raceStarter.ResetRace();
                _isFinished = false;
                _finishMs = -1;
                _finishSwipes = -1;
                _currentLap = 0;
                _isNewBest = false;
                SetLapNotification("");
                Debug.Log("[HUD] Reset clicked — race restarted.");
            }
        }

        private void SetText(string text)
        {
            if (statusTextTMP != null) statusTextTMP.text = text;
            else if (statusTextLegacy != null) statusTextLegacy.text = text;
        }

        private void SetPowerUpText(string text)
        {
            if (powerUpTextTMP != null) powerUpTextTMP.text = text;
            else if (powerUpTextLegacy != null) powerUpTextLegacy.text = text;
        }

        private void SetLapNotification(string text)
        {
            if (lapNotificationTMP != null) lapNotificationTMP.text = text;
            else if (lapNotificationLegacy != null) lapNotificationLegacy.text = text;
        }
    }
}
