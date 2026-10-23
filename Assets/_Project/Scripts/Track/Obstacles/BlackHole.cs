using System.Collections;
using UnityEngine;

namespace QuickChecks.Track.Obstacles
{
    /// <summary>
    /// Black hole obstacle. When a kart touches it, the kart is rewound along its own
    /// recent stop-position history — taking it back N swipes to where it was stopped a
    /// few moves ago.
    ///
    /// The rewind is smooth (animated over 0.6s) rather than an instant teleport, so the
    /// player can visually track where they're being sent. This makes the obstacle feel
    /// like a "time rewind" rather than a disorienting snap.
    ///
    /// Why rewind-along-path instead of random toss:
    ///   - Predictable: player can plan around the obstacle ("I'll get sent back 3 swipes
    ///     so I should approach from a direction with a safe stop behind me").
    ///   - Fair: doesn't randomly drop the kart in a wall or off-track.
    ///   - Thematic: black hole = spacetime distortion = rewind.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class BlackHole : MonoBehaviour
    {
        [Tooltip("How many swipes to rewind the kart. 3 = back to where it was 3 swipes ago.")]
        public int rewindSwipes = 3;

        [Tooltip("Duration of the rewind animation (seconds).")]
        public float rewindDuration = 0.6f;

        [Tooltip("Cooldown per kart to prevent rapid re-triggering (seconds).")]
        public float cooldownSec = 2f;

        [Tooltip("Visual radius of the black hole.")]
        public float visualRadius = 1.5f;

        private float _lastTriggerTime;
        private int _lastKartInstanceId = -1;

        private void Awake()
        {
            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = visualRadius;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.15f, 0f, 0.2f, 0.9f);
                sr.sortingOrder = 7;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var kart = other.GetComponent<Racing.KartController>();
            if (kart == null) return;

            // Cooldown per kart
            if (kart.GetInstanceID() == _lastKartInstanceId && Time.time - _lastTriggerTime < cooldownSec)
            {
                return;
            }

            // Find the kart's stop history. If missing (older setup), fall back to the old
            // "teleport away from center" behavior so the game still works.
            var history = kart.GetComponent<Racing.KartStopHistory>();
            if (history == null)
            {
                Debug.LogWarning("[BlackHole] Kart has no KartStopHistory — falling back to old teleport.");
                FallbackTeleport(kart);
                return;
            }

            if (!history.HasHistory)
            {
                Debug.Log("[BlackHole] Kart has no stop history yet — no rewind performed.");
                return;
            }

            _lastKartInstanceId = kart.GetInstanceID();
            _lastTriggerTime = Time.time;

            // Compute the rewind target: the stop position N swipes back.
            var targetStop = history.GetStopBack(rewindSwipes);
            Vector3 targetPos = targetStop.position;
            float targetRot = targetStop.rotation;

            // Stop the kart immediately so it doesn't drift while rewinding.
            kart.StopImmediately();

            // Juice: big shake + heavy haptic — black hole rewind is the most dramatic event
            if (Racing.JuiceManager.Instance != null)
            {
                Racing.JuiceManager.Instance.Shake(0.8f, 0.4f);
                Racing.JuiceManager.Instance.HapticBlackHole();
                Racing.JuiceManager.Instance.Burst(kart.transform.position,
                    new Color(0.15f, 0f, 0.2f), 12, 4f, 0.4f);
            }

            // Start the smooth rewind animation.
            StartCoroutine(RewindRoutine(kart, kart.transform.position, kart.transform.eulerAngles.z,
                                         targetPos, targetRot, rewindDuration));

            Debug.Log($"[BlackHole] Rewinding {kart.gameObject.name} {rewindSwipes} swipes back to {targetPos} " +
                      $"(over {rewindDuration}s).");
        }

        /// <summary>
        /// Smoothly moves the kart from its current position to the rewind target over `duration` seconds.
        /// Uses an ease-in-out curve so the motion feels like time-warping (slow start, fast middle, slow end).
        /// </summary>
        private IEnumerator RewindRoutine(Racing.KartController kart, Vector3 fromPos, float fromRot,
                                          Vector3 toPos, float toRot, float duration)
        {
            // Disable physics during rewind (we're manually controlling the transform).
            var rb = kart.GetComponent<Rigidbody2D>();
            bool rbWasSimulated = rb != null && rb.simulated;
            if (rb != null) rb.simulated = false;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease in-out (cubic)
                float eased = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

                kart.transform.position = Vector3.LerpUnclamped(fromPos, toPos, eased);

                // Shortest-path rotation lerp
                float deltaRot = Mathf.DeltaAngle(fromRot, toRot);
                kart.transform.rotation = Quaternion.Euler(0, 0, fromRot + deltaRot * eased);

                yield return null;
            }

            // Snap to final position.
            kart.transform.position = toPos;
            kart.transform.rotation = Quaternion.Euler(0, 0, toRot);

            // Re-enable physics + place kart at the rewind destination (clears velocity).
            if (rb != null)
            {
                rb.simulated = rbWasSimulated;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            // Use RespawnAt to ensure the kart is fully reset (also clears trail-wall pending, etc.)
            kart.RespawnAt(toPos);

            Debug.Log($"[BlackHole] Rewind complete — kart placed at {toPos}.");
        }

        /// <summary>
        /// Fallback for karts without KartStopHistory (older builds):
        /// Teleport the kart away from the black hole center by `teleportDistance` units.
        /// </summary>
        private void FallbackTeleport(Racing.KartController kart)
        {
            float teleportDistance = 8f;
            Vector2 kartPos = kart.transform.position;
            Vector2 holePos = transform.position;
            Vector2 awayDir = (kartPos - holePos).normalized;

            if (awayDir == Vector2.zero)
            {
                Vector2 vel = kart.Velocity;
                if (vel.magnitude > 0.1f) awayDir = -vel.normalized;
                else awayDir = Vector2.up;
            }

            Vector2 teleportPos = kartPos + awayDir * teleportDistance;
            for (float dist = teleportDistance; dist > 2f; dist -= 2f)
            {
                Vector2 testPos = kartPos + awayDir * dist;
                Collider2D wallCheck = Physics2D.OverlapPoint(testPos, LayerMask.GetMask("Default"));
                if (wallCheck == null || wallCheck.isTrigger)
                {
                    teleportPos = testPos;
                    break;
                }
            }

            kart.RespawnAt(teleportPos);
            _lastKartInstanceId = kart.GetInstanceID();
            _lastTriggerTime = Time.time;
        }
    }
}
