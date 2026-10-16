using UnityEngine;

namespace QuickChecks.Track.Obstacles
{
    /// <summary>
    /// Black hole obstacle. When a kart or trail touches it, the kart is teleported
    /// backward by teleportDistance units along the reverse of the track direction.
    ///
    /// Visual: dark purple/black circle with a subtle glow.
    /// Behavior: trigger collider. On kart entry → teleport kart backward.
    /// Cooldown: 2 seconds per kart to prevent rapid re-triggering.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class BlackHole : MonoBehaviour
    {
        [Tooltip("How far back to teleport the kart (world units).")]
        public float teleportDistance = 8f;

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
                sr.color = new Color(0.15f, 0f, 0.2f, 0.9f);  // dark purple
                sr.sortingOrder = 7;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var kart = other.GetComponent<Racing.KartController>();
            if (kart == null) return;

            // Prevent re-triggering the same kart rapidly
            if (kart.GetInstanceID() == _lastKartInstanceId && Time.time - _lastTriggerTime < cooldownSec)
            {
                return;
            }

            _lastKartInstanceId = kart.GetInstanceID();
            _lastTriggerTime = Time.time;

            // Compute teleport position: behind the kart along its current movement direction
            // (reverse of velocity, or if stopped, reverse of facing direction)
            Vector2 currentVel = kart.Velocity;
            Vector2 teleportDir;

            if (currentVel.magnitude > 1f)
            {
                // Teleport opposite to velocity direction
                teleportDir = -currentVel.normalized;
            }
            else
            {
                // Kart is stopped — teleport toward the track center (away from black hole)
                teleportDir = ((Vector2)transform.position - (Vector2)kart.transform.position).normalized;
            }

            Vector2 teleportPos = (Vector2)kart.transform.position + teleportDir * teleportDistance;

            // Teleport the kart
            kart.RespawnAt(teleportPos);
            Debug.Log($"[BlackHole] Teleported {kart.gameObject.name} backward {teleportDistance} units to {teleportPos}.");
        }
    }
}
