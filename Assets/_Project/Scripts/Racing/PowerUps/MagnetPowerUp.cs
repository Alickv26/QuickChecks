using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// MAGNET — Defensive power-up.
    /// Effect: Pulls nearby power-up pickups toward the kart for 6 seconds.
    /// Duration: 6 seconds.
    /// Pickup: Instant use.
    ///
    /// Implementation: OnUpdate() ticks while active, finds all PowerUpPickup
    /// components within a radius, and lerps them toward the kart.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Magnet", fileName = "PowerUp_Magnet")]
    public class MagnetPowerUp : PowerUpBase
    {
        [Tooltip("Radius (in world units) within which power-up pickups are pulled toward the kart.")]
        public float pullRadius = 8f;

        [Tooltip("Speed at which pickups are pulled toward the kart (units/sec).")]
        public float pullSpeed = 6f;

        public MagnetPowerUp()
        {
            powerUpId = "magnet";
            displayName = "Magnet";
            effectColor = new Color(0.78f, 0.639f, 1f);  // #C7A3FF lavender
            instantUse = true;
            durationSec = 6f;
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;
            Debug.Log($"[Magnet] Activated on {kart.gameObject.name} for {durationSec}s. " +
                      $"Pull radius: {pullRadius} units, pull speed: {pullSpeed} u/s.");
        }

        public override void OnUpdate(KartController kart, float elapsedSec)
        {
            if (kart == null) return;

            // Find all PowerUpPickup components within radius and pull them toward kart
            var pickups = FindObjectsOfType<PowerUpPickup>();
            Vector2 kartPos = kart.transform.position;

            foreach (var pickup in pickups)
            {
                if (pickup == null || pickup.IsCollected) continue;
                Vector2 pickupPos = pickup.transform.position;
                float distance = Vector2.Distance(kartPos, pickupPos);

                if (distance < pullRadius && distance > 0.1f)
                {
                    // Move pickup toward kart at pullSpeed
                    Vector2 direction = (kartPos - pickupPos).normalized;
                    Vector2 newPos = pickupPos + direction * pullSpeed * Time.deltaTime;
                    pickup.transform.position = newPos;
                }
            }
        }

        public override void OnExpire(KartController kart)
        {
            Debug.Log("[Magnet] Expired — pickups no longer pulled.");
        }
    }
}
