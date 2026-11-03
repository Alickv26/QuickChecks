using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// TRAIL WALL — Defensive/Offensive power-up.
    /// Effect: The player's NEXT trail segment becomes a solid wall that other karts
    /// collide with. When the player swipes after picking this up, a LineRenderer
    /// barrier is created along their path. Other karts that touch it stop dead (golf-feel).
    ///
    /// TURN-BASED (intended): The wall should persist until the player's NEXT turn ends
    /// (i.e., one full round of play), so opponents get a chance to be blocked by it.
    /// In Solo mode, the wall would persist until the next time the player's kart stops.
    ///
    /// NOTE: Wall creation is not yet implemented — `kart.SetTrailWallPending(true)` sets
    /// a flag, but no code currently reads `kart.IsTrailWallPending` to actually build
    /// the wall GameObject. The `wallDurationSec` field below is the placeholder duration
    /// for when wall creation is added. TODO: implement wall creation in KartController
    /// or RaceSceneController, reading IsTrailWallPending after each swipe.
    ///
    /// Strategy: Cut across the racing line to block opponents from taking the optimal path.
    /// In turn-based multiplayer, this creates "area denial" — the trail wall forces
    /// opponents to take a longer route.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Trail Wall", fileName = "PowerUp_TrailWall")]
    public class TrailWallPowerUp : PowerUpBase
    {
        [Tooltip("How long the trail wall persists after creation (seconds). " +
                 "PLACEHOLDER — intended to be replaced with turn-based expiry when wall " +
                 "creation is implemented (track the creator's player index, destroy on " +
                 "their next turn start).")]
        public float wallDurationSec = 30f;

        public TrailWallPowerUp()
        {
            powerUpId = "trail_wall";
            displayName = "Trail Wall";
            effectColor = new Color(1f, 0.42f, 0.79f);  // hot pink
            instantUse = true;
            durationSec = 0f;            // No time-based expiry for the PICKUP effect
            expiresOnTurnEnd = false;    // The pickup itself is "until used" (next swipe creates wall).
                                         // The wall object has its own lifetime (wallDurationSec).
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;
            kart.SetTrailWallPending(true);
            Debug.Log($"[TrailWall] Activated on {kart.gameObject.name}. " +
                      "Next swipe will create a wall along the trail path.");
        }

        public override void OnExpire(KartController kart)
        {
            if (kart == null) return;
            // If the player picks up TrailWall but never swipes, clear the pending flag.
            // (This is called only if a new power-up replaces this one or the race ends —
            // since expiresOnTurnEnd=false, OnExpire is NOT called at turn end.)
            kart.SetTrailWallPending(false);
            Debug.Log("[TrailWall] Expired — trail wall pending flag cleared.");
        }
    }
}
