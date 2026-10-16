using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace QuickChecks.Track.Obstacles
{
    /// <summary>
    /// Black hole obstacle. When a kart touches it, the kart is teleported
    /// AWAY from the black hole center, staying within the track.
    ///
    /// The teleport direction is always directly away from the black hole
    /// (not random), ensuring the kart lands on the track surface behind
    /// the obstacle — not outside the track boundaries.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class BlackHole : MonoBehaviour
    {
        [Tooltip("How far to teleport the kart (world units).")]
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
                sr.color = new Color(0.15f, 0f, 0.2f, 0.9f);
                sr.sortingOrder = 7;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var kart = other.GetComponent<Racing.KartController>();
            if (kart == null) return;

            if (kart.GetInstanceID() == _lastKartInstanceId && Time.time - _lastTriggerTime < cooldownSec)
            {
                return;
            }

            _lastKartInstanceId = kart.GetInstanceID();
            _lastTriggerTime = Time.time;

            // Always teleport AWAY from the black hole center.
            // Direction: from black hole position to kart position (the direction the kart came from).
            Vector2 kartPos = kart.transform.position;
            Vector2 holePos = transform.position;
            Vector2 awayDir = (kartPos - holePos).normalized;

            // If kart is exactly at center, use the reverse of velocity
            if (awayDir == Vector2.zero)
            {
                Vector2 vel = kart.Velocity;
                if (vel.magnitude > 0.1f)
                    awayDir = -vel.normalized;
                else
                    awayDir = Vector2.up;  // Fallback
            }

            // Try teleporting in the 'away' direction, checking for walls
            Vector2 teleportPos = kartPos + awayDir * teleportDistance;

            // Validate: check if target position is inside a wall collider
            // If so, try shorter distances
            for (float dist = teleportDistance; dist > 2f; dist -= 2f)
            {
                Vector2 testPos = kartPos + awayDir * dist;
                Collider2D wallCheck = Physics2D.OverlapPoint(testPos, LayerMask.GetMask("Default"));

                // Check if the point is NOT inside a solid (non-trigger) collider
                if (wallCheck == null || wallCheck.isTrigger)
                {
                    teleportPos = testPos;
                    break;
                }
            }

            // Teleport the kart
            kart.RespawnAt(teleportPos);
            Debug.Log($"[BlackHole] Teleported {kart.gameObject.name} backward to {teleportPos} " +
                      $"(away from hole, dist={Vector2.Distance(kartPos, teleportPos):F1}).");
        }
    }
}
