using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// PHASE DODGE — Speed/utility power-up.
    /// Effect: Next boundary hit doesn't stop the kart (phases through walls once).
    ///
    /// TURN-BASED: Expires when either of these conditions is met:
    ///   (1) The kart hits a wall/track boundary (effect consumed — kart passes through)
    ///   (2) The kart's turn ends (effect wasted — kart stops without hitting anything)
    ///
    /// Strategy tip: Best used before tricky chicanes where a small miscalculation
    /// would normally stop the kart dead. Don't grab it if you're not planning to
    /// approach a wall this turn — you'll waste it.
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
            durationSec = 0f;            // No time-based expiry
            expiresOnTurnEnd = true;     // Expires at end of current turn (if unused)
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;
            kart.ActivatePhaseDodge();
            Debug.Log($"[PhaseDodge] Activated on {kart.gameObject.name}. " +
                      "Next wall collision will be ignored, OR expires at end of turn.");
        }

        public override void OnExpire(KartController kart)
        {
            // The kart's ActivatePhaseDodge set _phaseDodgeActive=true. If unused at turn end,
            // we should clear it so the next turn doesn't accidentally phase through.
            if (kart != null && kart.IsPhaseDodgeActive)
            {
                kart.DeactivatePhaseDodge();
                Debug.Log("[PhaseDodge] Expired (turn end) — phase dodge was unused, cleared.");
            }
            else
            {
                Debug.Log("[PhaseDodge] Expired (turn end) — phase dodge was already consumed.");
            }
        }
    }
}
