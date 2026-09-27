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

        [Tooltip("Total number of checkpoints on the track (for lap completion check).")]
        public int requiredCount;

        [Tooltip("If true, this checkpoint is invisible (no debug sprite). Set false to see in editor.")]
        public bool hideInGame = true;

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

            if (hideInGame)
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;
            }
        }
    }
}
