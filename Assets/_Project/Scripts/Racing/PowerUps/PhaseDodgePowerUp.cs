using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// PHASE DODGE — Speed/utility power-up.
    /// Effect: Next boundary hit doesn't stop the kart (phases through walls once).
    /// Duration: Until used (one wall pass) OR 5 seconds expires.
    /// Pickup: Instant use (activates immediately, awaits next collision).
    ///
    /// Strategy tip: Best used before tricky chicanes where a small miscalculation
    /// would normally stop the kart dead.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Phase Dodge", fileName = "PowerUp_PhaseDodge")]
    public class PhaseDodgePowerUp : PowerUpBase
    {
        public PhaseDodgePowerUp()
        {
            powerUpId = "phase_dodge";
            displayName = "Phase Dodge";
            effectColor = new Color(1f, 0.71f, 0.15f);  // #FFB627 amber
            instantUse = true;
            durationSec = 5f;
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;
            kart.ActivatePhaseDodge();
            Debug.Log($"[PhaseDodge] Activated on {kart.gameObject.name}. Next wall collision will be ignored.");
        }

        public override void OnExpire(KartController kart)
        {
            if (kart == null) return;
            kart.DeactivatePhaseDodge();
            Debug.Log("[PhaseDodge] Expired without use — kart is solid again.");
        }
    }
}
