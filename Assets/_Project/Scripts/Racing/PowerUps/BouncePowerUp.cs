using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// BOUNCE — Speed/Utility power-up.
    /// Effect: Instead of stopping dead when hitting a wall, the kart BOUNCES off
    /// at a reflected angle. The bounce preserves most of the velocity.
    ///
    /// Duration: 5 seconds (all wall hits during this time bounce).
    ///
    /// Strategy: Use on tracks with tight angles — bounce off walls to change direction
    /// without spending an extra swipe. Can save 1-2 swipes per bounce.
    ///
    /// Implementation: KartController checks this flag in OnCollisionEnter2D.
    /// If bounce mode is active, reflects the velocity instead of zeroing it.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Bounce", fileName = "PowerUp_Bounce")]
    public class BouncePowerUp : PowerUpBase
    {
        [Tooltip("How much velocity is preserved on bounce (0.5 = half speed, 1.0 = full speed).")]
        public float bounceVelocityRetention = 0.7f;

        public BouncePowerUp()
        {
            powerUpId = "bounce";
            displayName = "Bounce";
            effectColor = new Color(0.3f, 0.9f, 0.5f);  // bright green
            instantUse = true;
            durationSec = 5f;
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;
            kart.SetBounceMode(true, bounceVelocityRetention);
            Debug.Log($"[Bounce] Activated on {kart.gameObject.name} for {durationSec}s. Walls will bounce the kart at {bounceVelocityRetention*100}% velocity.");
        }

        public override void OnExpire(KartController kart)
        {
            if (kart == null) return;
            kart.SetBounceMode(false, 0f);
            Debug.Log("[Bounce] Expired — kart stops dead on walls again.");
        }
    }
}
