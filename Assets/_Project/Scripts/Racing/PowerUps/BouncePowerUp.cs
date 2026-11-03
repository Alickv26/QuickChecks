using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// BOUNCE — Speed/Utility power-up.
    /// Effect: Instead of stopping dead when hitting a wall OR track boundary, the kart
    /// BOUNCES off at a reflected angle, preserving 90% of velocity. Gives a billiard-ball
    /// feel — every wall hit during the turn ricochets.
    ///
    /// TURN-BASED: Expires at the end of the current turn. Every wall/track-boundary hit
    /// during this turn bounces (one swipe can produce multiple bounces).
    ///
    /// Strategy: Use on tracks with tight angles — bounce off walls to change direction
    /// without spending an extra swipe. Can save 1-2 swipes per bounce.
    ///
    /// Implementation: KartController.OnCollisionEnter2D checks the _bounceMode flag first.
    /// If true AND currentVelocity > 2 u/s, reflects the velocity instead of zeroing it.
    /// The threshold of 2 u/s is below the kart's stopThreshold (8 u/s), so any actually-
    /// moving kart will bounce — even slow approaches bounce off track limits.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Bounce", fileName = "PowerUp_Bounce")]
    public class BouncePowerUp : PowerUpBase
    {
        [Tooltip("How much velocity is preserved on bounce (0.5 = half speed, 1.0 = full speed). " +
                 "0.9 = billiard-ball feel — most energy preserved, slight loss per bounce.")]
        public float bounceVelocityRetention = 0.9f;

        public BouncePowerUp()
        {
            powerUpId = "bounce";
            displayName = "Bounce";
            effectColor = new Color(0.3f, 0.9f, 0.5f);  // bright green
            instantUse = true;
            durationSec = 0f;            // No time-based expiry
            expiresOnTurnEnd = true;     // Expires at end of current turn
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;
            kart.SetBounceMode(true, bounceVelocityRetention);
            Debug.Log($"[Bounce] Activated on {kart.gameObject.name} — walls + track limits will bounce " +
                      $"the kart at {bounceVelocityRetention * 100:F0}% velocity until end of turn.");
        }

        public override void OnExpire(KartController kart)
        {
            if (kart == null) return;
            kart.SetBounceMode(false, 0f);
            Debug.Log("[Bounce] Expired (turn end) — kart stops dead on walls again.");
        }
    }
}

