using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// TRAIL WALL — Defensive/Offensive power-up.
    /// Effect: The player's NEXT trail segment becomes a solid wall that other karts
    /// collide with. When the player swipes after picking this up, a LineRenderer
    /// barrier is created along their path. Other karts that touch it stop dead (golf-feel).
    ///
    /// Duration: The wall persists until the player's next turn ends, then disappears.
    ///
    /// Strategy: Cut across the racing line to block opponents from taking the optimal path.
    /// In turn-based multiplayer, this creates "area denial" — the trail wall forces
    /// opponents to take a longer route.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Trail Wall", fileName = "PowerUp_TrailWall")]
    public class TrailWallPowerUp : PowerUpBase
    {
        [Tooltip("How long the trail wall persists after creation (seconds).")]
        public float wallDurationSec = 10f;

        public TrailWallPowerUp()
        {
            powerUpId = "trail_wall";
            displayName = "Trail Wall";
            effectColor = new Color(1f, 0.42f, 0.79f);  // hot pink
            instantUse = true;
            durationSec = 0f;
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;
            kart.SetTrailWallPending(true);
            Debug.Log($"[TrailWall] Activated on {kart.gameObject.name}. Next swipe creates a wall along the trail path.");
        }

        public override void OnExpire(KartController kart)
        {
            if (kart == null) return;
            // Wall is created by the kart when it swipes — nothing to expire here.
        }
    }
}
