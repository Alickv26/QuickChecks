using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// DOUBLE MOVE (2x) — Speed/Utility power-up.
    /// Effect: Player gets ONE additional swipe before the turn switches to the next player.
    /// Duration: Until used (player swipes twice, then turn ends on second stop).
    ///
    /// Strategy: Grab this when you're off-track or stuck in a corner — two swipes
    /// let you reposition and then make a real move.
    ///
    /// Implementation: Sets a flag on the kart. TurnManager checks this flag when
    /// the kart stops — if true, gives an extra swipe instead of switching turns.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Double Move", fileName = "PowerUp_DoubleMove")]
    public class DoubleMovePowerUp : PowerUpBase
    {
        public DoubleMovePowerUp()
        {
            powerUpId = "double_move";
            displayName = "2x Move";
            effectColor = new Color(1f, 0.84f, 0f);  // gold
            instantUse = true;
            durationSec = 0f;  // Persists until used (no ticking)
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;
            kart.SetExtraMove(true);
            Debug.Log($"[DoubleMove] Activated on {kart.gameObject.name}. Next stop gives an extra swipe instead of ending turn.");
        }
    }
}
