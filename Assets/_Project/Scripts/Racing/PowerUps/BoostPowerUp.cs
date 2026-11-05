using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// BOOST — Speed/utility power-up.
    /// Effect: Multiplies current velocity by kart's boostMultiplier (default 1.8×), and
    /// lifts the kart's maxSpeed cap to (stats.maxSpeed × boostMultiplier) for the rest
    /// of the current turn.
    ///
    /// HELD POWER-UP: Collected into inventory on pickup (does NOT auto-activate). Player
    /// taps the inventory slot to use it strategically. On Use, applies the boost to the
    /// kart's current velocity + lifts the maxSpeed cap until end of turn.
    ///
    /// TURN-BASED: Expires at the end of the current turn (when the kart stops moving
    /// without an extra move available). No time-based duration.
    ///
    /// Strategy tip: Use boost when moving in the right direction — the velocity
    /// multiply only helps if you're already going somewhere. The lifted maxSpeed cap
    /// lets subsequent swipes during the same turn reach higher peak speeds. Save it
    /// for a long straight or to escape a gravel trap.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Boost", fileName = "PowerUp_Boost")]
    public class BoostPowerUp : PowerUpBase
    {
        public BoostPowerUp()
        {
            powerUpId = "boost";
            displayName = "Boost";
            effectColor = new Color(1f, 0.42f, 0.42f);  // #FF6B6B coral red
            instantUse = false;       // HELD — player taps to use
            durationSec = 0f;          // No time-based expiry
            expiresOnTurnEnd = true;    // Expires at end of current turn (after Use)
        }

        /// <summary>Called when the player taps the Boost slot in the inventory UI.</summary>
        public override void OnUse(KartController kart)
        {
            if (kart == null) return;
            // Pass durationSec=0 to ApplyBoost → it sets boostEndTime = +∞ (turn-based).
            // The kart's FixedUpdate detects stop-transition and clears boost state via ClearBoost().
            kart.ApplyBoost(kart.Stats.boostMultiplier, 0f);
            Debug.Log($"[Boost] Used by {kart.gameObject.name} — {kart.Stats.boostMultiplier}× velocity boost " +
                      "(expires at end of current turn).");
        }
    }
}
