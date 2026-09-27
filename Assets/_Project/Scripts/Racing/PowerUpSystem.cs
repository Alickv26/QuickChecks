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

        public PowerUpBase HeldPowerUp => _heldPowerUp;
        public PowerUpBase ActivePowerUp => _activePowerUp;
        public bool HasHeldPowerUp => _heldPowerUp != null;
        public bool HasActiveEffect => _effectTimer > 0f;
        public float EffectTimeRemaining => _effectTimer;

        private void Update()
        {
            if (_effectTimer > 0f)
            {
                _effectTimer -= Time.deltaTime;
                _effectElapsed += Time.deltaTime;

                if (_activePowerUp != null)
                {
                    _activePowerUp.OnUpdate(kart, _effectElapsed);
                }

                if (_effectTimer <= 0f)
                {
                    // Effect expired — call cleanup
                    if (_activePowerUp != null)
                    {
                        _activePowerUp.OnExpire(kart);
                        Debug.Log($"[PowerUpSystem] Effect '{_activePowerUp.powerUpId}' expired on {kart.gameObject.name}.");
                    }
                    _activePowerUp = null;
                    _effectTimer = 0f;
                    _effectElapsed = 0f;
                }
            }
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
            if (_activePowerUp != null && _effectTimer > 0f)
            {
                _activePowerUp.OnExpire(kart);
                Debug.Log($"[PowerUpSystem] Replacing active effect '{_activePowerUp.powerUpId}' with '{powerUp.powerUpId}'.");
            }

            _activePowerUp = powerUp;
            _effectTimer = powerUp.durationSec;
            _effectElapsed = 0f;

            // Apply the pickup effect
            powerUp.OnPickup(kart);
            Debug.Log($"[PowerUpSystem] Activated '{powerUp.powerUpId}' on {kart.gameObject.name} " +
                      $"(duration {powerUp.durationSec}s).");
        }

        /// <summary>Clears all power-up state (used on race reset).</summary>
        public void Clear()
        {
            if (_activePowerUp != null && _effectTimer > 0f)
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
