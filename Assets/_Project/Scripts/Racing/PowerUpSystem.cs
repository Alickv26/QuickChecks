using System;
using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Manages the active power-up slot on a kart. Receives pickup events
    /// from PowerUpPickup triggers, applies effects, ticks duration, and
    /// handles expiry.
    ///
    /// Behavior:
    ///   - 1 power-up slot per kart
    ///   - instantUse power-ups apply immediately on pickup, tick for durationSec
    ///   - held power-ups (instantUse=false) wait for player to call Use()
    ///   - On expiry, OnExpire() is called for cleanup
    /// </summary>
    public class PowerUpSystem : MonoBehaviour
    {
        [SerializeField] private KartController kart;

        private PowerUpBase _heldPowerUp;       // power-up waiting to be used (instantUse=false)
        private PowerUpBase _activePowerUp;     // power-up currently ticking (effect active)
        private float _effectTimer = 0f;        // seconds remaining for active effect (0 = no active)
        private float _effectElapsed = 0f;      // seconds since effect started (for OnUpdate)

        // Turn-end detection: track the kart's isStopped state from the previous frame.
        // When the kart transitions moving → stopped AND has no extra move available,
        // we expire any active turn-based power-up (expiresOnTurnEnd = true).
        // This works for both Solo (kart stops = turn ends) and multiplayer (TurnManager
        // also fires OnTurnEnd, but the stop transition is the natural signal here).
        private bool _wasStoppedLastFrame = false;

        public PowerUpBase HeldPowerUp => _heldPowerUp;
        public PowerUpBase ActivePowerUp => _activePowerUp;
        public bool HasHeldPowerUp => _heldPowerUp != null;
        public bool HasActiveEffect => _effectTimer > 0f;
        public float EffectTimeRemaining => _effectTimer;

        /// <summary>
        /// Fired when the kart picks up a power-up (instant-use AND held).
        /// Subscribers: PowerUpPickupNotifier (shows on-screen toast), HUD icon indicator, etc.
        /// Args: the power-up that was just collected.
        /// </summary>
        public event Action<PowerUpBase> OnCollected;

        /// <summary>
        /// Fired when a power-up effect STARTS (instant-use on pickup, or held on Use()).
        /// Subscribers: KartVisualFeedback (tints the kart to the power-up's effectColor).
        /// Args: the power-up whose effect just started.
        /// </summary>
        public event Action<PowerUpBase> OnEffectStarted;

        /// <summary>
        /// Fired when a power-up effect EXPIRES (duration runs out, or effect resolves).
        /// Subscribers: KartVisualFeedback (restores the kart's original color).
        /// </summary>
        public event Action<PowerUpBase> OnEffectExpired;

        private void Update()
        {
            // --- Turn-end expiry check ---
            // Polls the kart's isStopped state. When the kart transitions from moving → stopped
            // (and has no extra move available), the turn has ended — expire any active
            // turn-based power-up (expiresOnTurnEnd = true).
            if (kart != null && _activePowerUp != null && _activePowerUp.expiresOnTurnEnd)
            {
                bool isStoppedNow = kart.IsStopped;
                if (!_wasStoppedLastFrame && isStoppedNow && !kart.HasExtraMove)
                {
                    // Turn ended — expire the active turn-based power-up
                    ExpireActivePowerUp(reason: "turn end");
                }
                _wasStoppedLastFrame = isStoppedNow;
            }
            else if (kart != null)
            {
                // Keep tracking the stopped state even when no active power-up, so the next
                // pickup correctly detects the transition.
                _wasStoppedLastFrame = kart.IsStopped;
            }

            // --- Effect tick (runs for both time-based and turn-based power-ups) ---
            // For time-based power-ups (_effectTimer > 0): decrement timer, fire OnUpdate,
            // expire when timer reaches 0.
            // For turn-based power-ups (_effectTimer == 0 but expiresOnTurnEnd = true):
            // still fire OnUpdate every frame so ticking effects (e.g. Magnet pulling pickups)
            // continue working. Expiry is handled by the turn-end check above.
            if (_activePowerUp != null && kart != null)
            {
                if (_effectTimer > 0f)
                {
                    _effectTimer -= Time.deltaTime;
                    _effectElapsed += Time.deltaTime;
                }
                else if (_activePowerUp.expiresOnTurnEnd)
                {
                    // Turn-based: keep incrementing elapsed for OnUpdate, no timer decrement
                    _effectElapsed += Time.deltaTime;
                }

                // Call OnUpdate so ticking effects (Magnet pull) work for both kinds
                try
                {
                    _activePowerUp.OnUpdate(kart, _effectElapsed);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[PowerUpSystem] OnUpdate threw: {ex.Message}");
                }

                // Time-based expiry only (turn-based handled above by stop transition)
                if (_effectTimer <= 0f && !_activePowerUp.expiresOnTurnEnd)
                {
                    ExpireActivePowerUp(reason: "duration elapsed");
                }
            }
        }

        /// <summary>Shared expiry path — calls OnExpire, fires OnEffectExpired event, clears state.</summary>
        private void ExpireActivePowerUp(string reason)
        {
            if (_activePowerUp != null && kart != null)
            {
                try
                {
                    _activePowerUp.OnExpire(kart);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PowerUpSystem] OnExpire threw: {ex.Message}");
                }
                Debug.Log($"[PowerUpSystem] Effect '{_activePowerUp.powerUpId}' expired on {kart.gameObject.name} ({reason}).");

                try { OnEffectExpired?.Invoke(_activePowerUp); }
                catch (Exception ex) { Debug.LogError($"[PowerUpSystem] OnEffectExpired subscriber threw: {ex.Message}"); }
            }
            _activePowerUp = null;
            _effectTimer = 0f;
            _effectElapsed = 0f;
        }

        /// <summary>
        /// Called when kart drives through a PowerUpPickup trigger.
        /// </summary>
        public void Collect(PowerUpBase powerUp)
        {
            if (powerUp == null) return;

            // If we already have a held power-up, replace it (last-in-first-out).
            if (_heldPowerUp != null)
            {
                Debug.Log($"[PowerUpSystem] Replacing held power-up '{_heldPowerUp.powerUpId}' with '{powerUp.powerUpId}'.");
            }

            _heldPowerUp = powerUp;

            if (powerUp.instantUse)
            {
                // Apply immediately
                ActivatePowerUp(powerUp);
                _heldPowerUp = null;  // consumed
            }
            else
            {
                Debug.Log($"[PowerUpSystem] Holding power-up '{powerUp.powerUpId}'. Press Use button to activate.");
            }

            // Fire the OnCollected event so the UI notifier can show a toast.
            try { OnCollected?.Invoke(powerUp); }
            catch (Exception ex) { Debug.LogError($"[PowerUpSystem] OnCollected subscriber threw: {ex.Message}"); }
        }

        /// <summary>
        /// Activates a held power-up (called by player input — e.g., tapping the power-up icon).
        /// </summary>
        public void Use()
        {
            if (_heldPowerUp == null)
            {
                Debug.Log("[PowerUpSystem] Use() called but no held power-up.");
                return;
            }

            if (_heldPowerUp.instantUse)
            {
                Debug.LogWarning($"[PowerUpSystem] Use() called on instant-use power-up '{_heldPowerUp.powerUpId}'. Should have been consumed on pickup.");
                return;
            }

            var powerUp = _heldPowerUp;
            _heldPowerUp = null;
            ActivatePowerUp(powerUp);
            powerUp.OnUse(kart);  // for held power-ups, OnUse is the trigger
        }

        private void ActivatePowerUp(PowerUpBase powerUp)
        {
            // If there's an active effect, expire it first (replace)
            if (_activePowerUp != null && (_effectTimer > 0f || _activePowerUp.expiresOnTurnEnd))
            {
                _activePowerUp.OnExpire(kart);
                Debug.Log($"[PowerUpSystem] Replacing active effect '{_activePowerUp.powerUpId}' with '{powerUp.powerUpId}'.");
            }

            _activePowerUp = powerUp;
            // Turn-based power-ups (expiresOnTurnEnd = true) don't use a timer — they expire
            // when the kart stops. Time-based power-ups use durationSec as before.
            _effectTimer = powerUp.expiresOnTurnEnd ? 0f : powerUp.durationSec;
            _effectElapsed = 0f;

            // Apply the pickup effect
            powerUp.OnPickup(kart);
            Debug.Log($"[PowerUpSystem] Activated '{powerUp.powerUpId}' on {kart.gameObject.name} " +
                      (powerUp.expiresOnTurnEnd
                          ? "(expires at end of current turn)."
                          : $"(duration {powerUp.durationSec}s)."));

            // Fire OnEffectStarted so visual feedback can tint the kart.
            try { OnEffectStarted?.Invoke(powerUp); }
            catch (Exception ex) { Debug.LogError($"[PowerUpSystem] OnEffectStarted subscriber threw: {ex.Message}"); }
        }

        /// <summary>Clears all power-up state (used on race reset).</summary>
        public void Clear()
        {
            if (_activePowerUp != null && (_effectTimer > 0f || _activePowerUp.expiresOnTurnEnd))
            {
                _activePowerUp.OnExpire(kart);
            }
            _heldPowerUp = null;
            _activePowerUp = null;
            _effectTimer = 0f;
            _effectElapsed = 0f;
        }
    }
}
