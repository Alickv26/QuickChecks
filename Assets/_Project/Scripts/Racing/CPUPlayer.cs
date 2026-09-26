using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// AI opponent for Local vs CPU mode. Attached to a CPU kart prefab
    /// alongside KartController. When TurnManager starts a CPU player's
    /// turn, this component computes a swipe and emits it via the same
    /// SwipeEventSO that human input uses — so kart physics is identical.
    /// </summary>
    public class CPUPlayer : MonoBehaviour
    {
        public enum Difficulty { Easy, Medium, Hard }

        [Header("Config")]
        [SerializeField] private Difficulty difficulty = Difficulty.Medium;
        [SerializeField] private Core.GameEventSO<Input.SwipeData> swipeEvent;
        [SerializeField] private float thinkDelaySec = 1.0f;

        [Header("Difficulty Tuning")]
        [SerializeField] private float easyDirectionNoiseDeg = 25f;
        [SerializeField] private float mediumDirectionNoiseDeg = 10f;
        [SerializeField] private float hardDirectionNoiseDeg = 3f;

        [SerializeField] private Vector2 easyMagnitudeRange = new Vector2(0.5f, 0.8f);
        [SerializeField] private Vector2 mediumMagnitudeRange = new Vector2(0.75f, 0.95f);
        [SerializeField] private Vector2 hardMagnitudeRange = new Vector2(0.92f, 1.0f);

        private KartController _kart;
        private Transform _nextCheckpoint;
        private float _thinkTimer;
        private bool _thinking;

        private void Awake()
        {
            _kart = GetComponent<KartController>();
        }

        public void SetDifficulty(Difficulty d)
        {
            difficulty = d;
            thinkDelaySec = d switch
            {
                Difficulty.Easy => 0.5f,
                Difficulty.Medium => 1.0f,
                Difficulty.Hard => 1.5f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Called by TurnManager when it's this CPU's turn.
        /// </summary>
        public void TakeTurn(Track.TrackDefinition track)
        {
            // Find next checkpoint (simplified — real impl uses CheckpointTracker).
            _nextCheckpoint = FindNextCheckpoint(track);
            _thinkTimer = thinkDelaySec;
            _thinking = true;
        }

        private void Update()
        {
            if (!_thinking) return;

            _thinkTimer -= Time.deltaTime;
            if (_thinkTimer <= 0f)
            {
                _thinking = false;
                ExecuteSwipe();
            }
        }

        private void ExecuteSwipe()
        {
            if (_nextCheckpoint == null) return;

            // Direction from kart to next checkpoint.
            Vector2 toCheckpoint = (Vector2)(_nextCheckpoint.position - _kart.transform.position);
            float distance = toCheckpoint.magnitude;
            if (distance < 0.1f) return;

            Vector2 direction = toCheckpoint.normalized;

            // Apply difficulty-based noise.
            float noiseDeg = difficulty switch
            {
                Difficulty.Easy => easyDirectionNoiseDeg,
                Difficulty.Medium => mediumDirectionNoiseDeg,
                Difficulty.Hard => hardDirectionNoiseDeg,
                _ => mediumDirectionNoiseDeg
            };
            float angleOffset = Random.Range(-noiseDeg, noiseDeg);
            direction = Rotate(direction, angleOffset);

            // Magnitude scaled by distance, capped by kart max impulse.
            var stats = _kart.Stats;
            float optimalMag = Mathf.Min(distance * 2.5f, stats.maxSpeed);

            Vector2 magRange = difficulty switch
            {
                Difficulty.Easy => easyMagnitudeRange,
                Difficulty.Medium => mediumMagnitudeRange,
                Difficulty.Hard => hardMagnitudeRange,
                _ => mediumMagnitudeRange
            };
            float scale = Random.Range(magRange.x, magRange.y);
            float magnitude = optimalMag * scale;

            // Emit synthetic swipe via event.
            var swipe = new Input.SwipeData
            {
                timestampMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                screenPosition = _kart.transform.position, // For ghost recording — CPU doesn't really have a screen pos.
                direction = direction,
                magnitude = magnitude,
                rawVelocityPxS = magnitude / 0.1f // Approximation for haptics.
            };

            swipeEvent?.Raise(swipe);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        // TODO: replace with CheckpointTracker query.
        private Transform FindNextCheckpoint(Track.TrackDefinition track)
        {
            // For now: find the closest checkpoint object tagged "Checkpoint" that the kart
            // hasn't passed yet. Real impl uses CheckpointTracker.nextCheckpoint.
            var checkpoints = GameObject.FindGameObjectsWithTag("Checkpoint");
            Transform best = null;
            float bestDist = float.MaxValue;
            foreach (var go in checkpoints)
            {
                float d = Vector2.Distance(go.transform.position, _kart.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = go.transform;
                }
            }
            return best;
        }
    }
}
