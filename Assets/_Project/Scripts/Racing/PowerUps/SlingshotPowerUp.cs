using UnityEngine;

namespace QuickChecks.Racing.PowerUps
{
    /// <summary>
    /// SLINGSHOT — Speed/utility power-up.
    /// Effect: Pulls kart toward the next checkpoint, +50% velocity.
    /// Duration: Instant (resolves immediately on pickup, no ticking).
    /// Pickup: Instant use.
    ///
    /// Strategy tip: Slingshot is best on long straights where the next checkpoint
    /// is far away. The pull direction is toward the checkpoint, not the spline tangent.
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Power-Up/Slingshot", fileName = "PowerUp_Slingshot")]
    public class SlingshotPowerUp : PowerUpBase
    {
        public SlingshotPowerUp()
        {
            powerUpId = "slingshot";
            displayName = "Slingshot";
            effectColor = new Color(0.725f, 0.514f, 1f);  // #B983FF purple
            instantUse = true;
            durationSec = 0f;
        }

        public override void OnPickup(KartController kart)
        {
            if (kart == null) return;

            // Find the next checkpoint (via CheckpointTracker on the kart).
            var tracker = kart.GetComponent<CheckpointTracker>();
            if (tracker == null)
            {
                Debug.LogWarning("[Slingshot] No CheckpointTracker on kart — slingshot has no target.");
                return;
            }

            // Get the position of the next checkpoint.
            // We need access to the checkpoint array — for now, use FindObjectsOfType as a fallback.
            // (In a real impl, the tracker would expose NextCheckpointPosition.)
            var checkpoints = FindObjectsOfType<Track.Checkpoint>();
            Track.Checkpoint next = null;
            foreach (var cp in checkpoints)
            {
                if (cp.index == tracker.NextCheckpointIndex)
                {
                    next = cp;
                    break;
                }
            }

            if (next == null)
            {
                Debug.LogWarning($"[Slingshot] No checkpoint found for index {tracker.NextCheckpointIndex}.");
                return;
            }

            // Compute direction from kart to next checkpoint
            Vector2 toCheckpoint = (Vector2)(next.transform.position - kart.transform.position);
            float distance = toCheckpoint.magnitude;
            if (distance < 0.1f)
            {
                Debug.Log("[Slingshot] Already at checkpoint — no effect.");
                return;
            }

            // Compute new velocity: direction toward checkpoint, magnitude = current * 1.5
            Vector2 currentVel = kart.Velocity;
            float currentSpeed = currentVel.magnitude;
            float newSpeed = Mathf.Max(currentSpeed * 1.5f, kart.Stats.maxSpeed * 0.5f);

            Vector2 newVel = toCheckpoint.normalized * newSpeed;

            // Apply via kart's RespawnAt + manual velocity set is hacky. Better: kart.ApplyImpulse
            // For prototype, we use the same ApplyBoost method (which multiplies velocity).
            // Actually, we need a different method — let's just set the velocity directly via reflection.
            // Simpler: kart has ApplyBoost method that does currentVelocity *= multiplier.
            // For slingshot, we want to REPLACE velocity with new direction + magnitude.
            // That's a different operation, so let's add a method to KartController.

            // Use the new ApplySlingshot method (added in same commit).
            kart.ApplySlingshot(newVel);
            Debug.Log($"[Slingshot] Kart pulled toward checkpoint {next.index} at speed {newSpeed:F0}.");
        }
    }
}
