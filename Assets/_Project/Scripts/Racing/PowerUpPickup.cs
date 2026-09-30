using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Trigger collider placed on the track. When a kart with a PowerUpSystem
    /// drives through, the assigned PowerUpBase ScriptableObject is collected.
    ///
    /// PowerUpPickup is purely a trigger — it doesn't apply effects itself.
    /// The kart's PowerUpSystem handles activation, duration, expiry.
    ///
    /// Spawn behavior:
    ///   - Respawn delay: after collection, the pickup respawns at the same
    ///     position after respawnDelaySec seconds (default 5s).
    ///   - Visual: pickup has a colored sprite (effectColor of the assigned power-up).
    ///   - Maximum 2 active pickups per track at once (per DECISIONS.md spec).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PowerUpPickup : MonoBehaviour
    {
        [Header("Power-Up Assignment")]
        [Tooltip("The power-up that this pickup grants when collected. " +
                 "Assign a PowerUpBase asset (e.g., PowerUp_Boost.asset).")]
        public PowerUpBase powerUp;

        [Header("Respawn")]
        [Tooltip("Seconds before this pickup respawns at the same position after collection.")]
        public float respawnDelaySec = 5f;

        [Tooltip("If true, the pickup is collected permanently (no respawn).")]
        public bool oneTimeUse = false;

        [Header("Visual")]
        [Tooltip("If true, the pickup's sprite rotates slowly for visibility.")]
        public bool rotateSprite = true;
        [Tooltip("Rotation speed (degrees/sec) for the visual sprite.")]
        public float rotationSpeed = 60f;

        private Collider2D _collider;
        private SpriteRenderer _spriteRenderer;
        private bool _isCollected = false;
        private float _respawnTimer = 0f;

        public bool IsCollected => _isCollected;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            if (_collider != null) _collider.isTrigger = true;
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // Set sprite color to match the power-up's effectColor
            if (powerUp != null && _spriteRenderer != null)
            {
                _spriteRenderer.color = powerUp.effectColor;
            }
        }

        private void Update()
        {
            // Handle respawn timer
            if (_isCollected && !oneTimeUse)
            {
                _respawnTimer -= Time.deltaTime;
                if (_respawnTimer <= 0f)
                {
                    Respawn();
                }
            }

            // Rotate the sprite for visual flair
            if (rotateSprite && !_isCollected && _spriteRenderer != null)
            {
                _spriteRenderer.transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isCollected || powerUp == null) return;

            // Find the kart (other should be the kart's collider)
            var kart = other.GetComponent<KartController>();
            if (kart == null) return;

            // Find the kart's PowerUpSystem
            var powerUpSystem = other.GetComponent<PowerUpSystem>();
            if (powerUpSystem == null)
            {
                Debug.LogWarning($"[PowerUpPickup] Kart {kart.gameObject.name} has no PowerUpSystem — pickup ignored.");
                return;
            }

            // Collect!
            powerUpSystem.Collect(powerUp);

            if (oneTimeUse)
            {
                // Permanent collection — destroy the GameObject
                Destroy(gameObject);
            }
            else
            {
                // Temporarily hide + disable collider, will respawn after delay
                Collect();
            }
        }

        private void Collect()
        {
            _isCollected = true;
            _respawnTimer = respawnDelaySec;
            if (_collider != null) _collider.enabled = false;
            if (_spriteRenderer != null) _spriteRenderer.enabled = false;
            Debug.Log($"[PowerUpPickup] Collected '{powerUp.powerUpId}'. Respawns in {respawnDelaySec}s.");
        }

        private void Respawn()
        {
            _isCollected = false;
            if (_collider != null) _collider.enabled = true;
            if (_spriteRenderer != null) _spriteRenderer.enabled = true;
            Debug.Log($"[PowerUpPickup] '{powerUp.powerUpId}' respawned at {transform.position}.");
        }
    }
}
