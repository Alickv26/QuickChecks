using UnityEngine;

namespace QuickChecks.Track
{
    /// <summary>
    /// A checkpoint trigger on the track. Has an index (0-based, in order along the spline).
    /// When a kart passes through, CheckpointTracker.OnTriggerEnter2D is called.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("0-based index in track order. Must be passed in sequence (0, 1, 2, ..., N-1).")]
        public int index;

        [Tooltip("Total number of checkpoints on the track (for lap-completion check).")]
        public int requiredCount;

        [Tooltip("If true, this checkpoint is invisible (no debug sprite). Set false to see in editor.")]
        public bool hideInGame = false;  // Default false: show checkpoints during prototype testing.

        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;

        private void Reset()
        {
            // Ensure the collider is a trigger
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            // Make sure collider is a trigger at runtime too (in case it was reset)
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
            }

            if (hideInGame && _spriteRenderer != null)
            {
                _spriteRenderer.enabled = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Visual feedback when a kart passes through — flash cyan.
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = new Color(0.247f, 0.878f, 0.760f, 0.8f);  // Cyan flash
            }
            Debug.Log($"[Checkpoint {index}] Triggered by {other.gameObject.name}.");
        }

        /// <summary>
        /// Resets the checkpoint visual state (color back to original).
        /// Called by PrototypeBootstrapper.ResetRace() so checkpoints look fresh on new race.
        /// </summary>
        public void ResetVisual()
        {
            if (_spriteRenderer != null && _originalColor != default)
            {
                _spriteRenderer.color = _originalColor;
            }
        }
    }
}
