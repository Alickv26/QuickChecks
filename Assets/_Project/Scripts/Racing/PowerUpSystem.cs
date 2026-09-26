using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Manages the active power-up slot on a kart. Receives pickup events
    /// from PowerUpPickup triggers, applies effects, and ticks durations.
    /// </summary>
    public class PowerUpSystem : MonoBehaviour
    {
        [SerializeField] private KartController kart;
        private PowerUpBase _activePowerUp;
        private float _effectTimer = 0f;

        public PowerUpBase ActivePowerUp => _activePowerUp;
        public bool HasActiveEffect => _effectTimer > 0f;

        private void Update()
        {
            if (_effectTimer > 0f)
            {
                _effectTimer -= Time.deltaTime;
                if (_effectTimer <= 0f)
                {
                    // TODO: end-of-effect cleanup per power-up type.
                }
            }
        }

        public void Collect(PowerUpBase powerUp)
        {
            if (powerUp == null) return;

            _activePowerUp = powerUp;

            if (powerUp.instantUse)
            {
                powerUp.OnPickup(kart);
                _effectTimer = powerUp.durationSec;
            }
        }

        public void Use()
        {
            if (_activePowerUp == null || _activePowerUp.instantUse) return;
            _activePowerUp.OnUse(kart);
            _effectTimer = _activePowerUp.durationSec;
            _activePowerUp = null; // Consumed.
        }
    }
}
