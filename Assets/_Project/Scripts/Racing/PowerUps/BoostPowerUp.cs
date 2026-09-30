using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// BOOST — Speed/utility power-up.
    /// Effect: Multiplies current velocity by kart's boostMultiplier (default 1.8x).
    /// Duration: 1.5 seconds (boost ticks as deceleration continues, but initial impulse carries the kart).
    /// Pickup: Instant use.
    ///
    /// Strategy tip: Pick up boost when moving in the right direction — the velocity
    /// multiply only helps if you're already going somewhere.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Boost", fileName = "PowerUp_Boost")]
    public class BoostPowerUp : PowerUpBase
    {
        public BoostPowerUp()
        {
            powerUpId = "boost";
            displayName = "Boost";
            effectColor = new Color(1f, 0.42f, 0.42f);  // #FF6B6B coral red
            instantUse = true;
            durationSec = 1.5f;
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;
            kart.ApplyBoost(kart.Stats.boostMultiplier, kart.Stats.boostDurationSec);
            Debug.Log($"[Boost] Applied {kart.Stats.boostMultiplier}x velocity boost to {kart.gameObject.name}.");
        }
    }
}
