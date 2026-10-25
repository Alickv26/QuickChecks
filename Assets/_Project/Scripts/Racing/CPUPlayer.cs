using UnityEngine;
using QuickChecks.Track;
using QuickChecks.Core;

namespace QuickChecks.Racing
{
    /// <summary>
    /// AI opponent for Local vs CPU mode. Attached to a CPU kart prefab
    /// alongside KartController. When TurnManager starts a CPU player's
    /// turn, this component computes a swipe and emits it via the kart's
    /// own SwipeEvent.
    ///
    /// Navigation: targets the next un-passed checkpoint via CheckpointTracker.
    /// Wall avoidance: casts a short ray forward; if it hits a wall, steers
    /// left or right to go around.
    /// </summary>
    public class CPUPlayer : MonoBehaviour
    {
        public enum Difficulty { Easy, Medium, Hard }

        [Header("Config")]
        [SerializeField] private Difficulty difficulty = Difficulty.Medium;
        [SerializeField] private GameEventSO<Input.SwipeData> swipeEvent;
        [SerializeField] private float thinkDelaySec = 1.0f;

        [Header("Difficulty Tuning")]
        [SerializeField] private float easyDirectionNoiseDeg = 25f;
        [SerializeField] private float mediumDirectionNoiseDeg = 10f;
        [SerializeField] private float hardDirectionNoiseDeg = 3f;

        [SerializeField] private Vector2 easyMagnitudeRange = new Vector2(0.5f, 0.8f);
        [SerializeField] private Vector2 mediumMagnitudeRange = new Vector2(0.75f, 0.95f);
        [SerializeField] private Vector2 hardMagnitudeRange = new Vector2(0.92f, 1.0f);

        [Header("Wall Avoidance")]
        [SerializeField] private float wallCheckDistance = 5f;
        [SerializeField] private float wallAvoidSteerDeg = 35f;
        [SerializeField] private int wallAvoidRayCount = 3;

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

        public void TakeTurn(Track.TrackDefinition track)
        {
            _nextCheckpoint = FindNextCheckpoint(track);
            _thinkTimer = thinkDelaySec;
            _thinking = true;
            Debug.Log($"[CPUPlayer] TakeTurn called. Thinking for {thinkDelaySec}s. Target: {(_nextCheckpoint?.name ?? "null")}");
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
            if (_nextCheckpoint == null)
            {
                Debug.LogWarning("[CPUPlayer] No checkpoint target — skipping turn.");
                return;
            }

            // Direction from kart to next checkpoint.
            Vector2 toCheckpoint = (Vector2)(_nextCheckpoint.position - _kart.transform.position);
            float distance = toCheckpoint.magnitude;
            if (distance < 0.1f)
            {
                Debug.Log("[CPUPlayer] Already at target — skipping.");
                return;
            }

            Vector2 direction = toCheckpoint.normalized;

            // WALL AVOIDANCE: Cast rays ahead to check for walls.
            // If a wall is in the way, steer left or right to go around.
            direction = AvoidWalls(direction, distance);

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

            // Magnitude: scale by distance to checkpoint, but always strong enough to be competitive.
            // The old formula was: optimalMag = min(distance * 2, maxSpeed * 0.85) * Random.Range(0.75, 0.95)
            // This could result in very weak swipes when distance was small or when the random scale was low.
            //
            // New approach: aim for a magnitude that covers ~80% of the distance to the checkpoint,
            // with a minimum of 70% of max speed so every swipe is meaningful.
            var stats = _kart.Stats;
            float optimalMag = Mathf.Min(distance * 1.5f, stats.maxSpeed * 0.9f);
            // Floor: at least 70% of max speed so every swipe is competitive
            optimalMag = Mathf.Max(optimalMag, stats.maxSpeed * 0.7f);

            // Difficulty scaling: higher difficulty = closer to optimal magnitude
            // Easy: 70-90% of optimal (weaker, more variable)
            // Medium: 85-100% of optimal
            // Hard: 95-105% of optimal (slightly overshoots, aggressive)
            Vector2 magRange = difficulty switch
            {
                Difficulty.Easy => new Vector2(0.70f, 0.90f),
                Difficulty.Medium => new Vector2(0.85f, 1.00f),
                Difficulty.Hard => new Vector2(0.95f, 1.05f),
                _ => new Vector2(0.85f, 1.00f)
            };
            float scale = Random.Range(magRange.x, magRange.y);
            float magnitude = optimalMag * scale;

            // Emit synthetic swipe via event.
            var swipe = new Input.SwipeData
            {
                timestampMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                screenPosition = _kart.transform.position,
                direction = direction,
                magnitude = magnitude,
                rawVelocityPxS = magnitude / 0.1f
            };

            if (swipeEvent != null)
            {
                swipeEvent.Raise(swipe);
                Debug.Log($"[CPUPlayer] Swipe emitted: dir=({direction.x:F2},{direction.y:F2}), mag={magnitude:F0}");
            }
            else
            {
                Debug.LogError("[CPUPlayer] swipeEvent is null — cannot emit swipe!");
            }
        }

        /// <summary>
        /// Casts rays ahead to check for walls. If a wall blocks the path,
        /// steers left or right to go around it.
        /// </summary>
        private Vector2 AvoidWalls(Vector2 baseDirection, float targetDistance)
        {
            float checkDist = Mathf.Min(wallCheckDistance, targetDistance);

            // Check center ray first
            RaycastHit2D centerHit = Physics2D.Raycast(
                _kart.transform.position, baseDirection, checkDist,
                LayerMask.GetMask("Default"));

            if (centerHit.collider == null)
            {
                // No wall ahead — go straight
                return baseDirection;
            }

            Debug.Log($"[CPUPlayer] Wall detected ahead at {centerHit.distance:F1} units — steering around.");

            // Wall ahead — try steering left and right, pick whichever has more clearance
            float bestClearance = -1f;
            Vector2 bestDirection = baseDirection;

            for (int i = 1; i <= wallAvoidRayCount; i++)
            {
                float steerAngle = wallAvoidSteerDeg * i;

                // Try left
                Vector2 leftDir = Rotate(baseDirection, steerAngle);
                RaycastHit2D leftHit = Physics2D.Raycast(
                    _kart.transform.position, leftDir, checkDist * 1.5f,
                    LayerMask.GetMask("Default"));
                float leftClearance = leftHit.collider == null ? checkDist * 1.5f : leftHit.distance;

                // Try right
                Vector2 rightDir = Rotate(baseDirection, -steerAngle);
                RaycastHit2D rightHit = Physics2D.Raycast(
                    _kart.transform.position, rightDir, checkDist * 1.5f,
                    LayerMask.GetMask("Default"));
                float rightClearance = rightHit.collider == null ? checkDist * 1.5f : rightHit.distance;

                if (leftClearance > bestClearance)
                {
                    bestClearance = leftClearance;
                    bestDirection = leftDir;
                }
                if (rightClearance > bestClearance)
                {
                    bestClearance = rightClearance;
                    bestDirection = rightDir;
                }

                // If we found a clear path, use it
                if (bestClearance >= checkDist)
                {
                    Debug.Log($"[CPUPlayer] Found clear path steering {steerAngle}° (clearance: {bestClearance:F1}).");
                    break;
                }
            }

            return bestDirection;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private Transform FindNextCheckpoint(Track.TrackDefinition track)
        {
            var checkpoints = FindObjectsOfType<Track.Checkpoint>();
            if (checkpoints == null || checkpoints.Length == 0)
            {
                Debug.LogWarning("[CPUPlayer] No checkpoints found in scene.");
                return null;
            }

            var tracker = _kart.GetComponent<CheckpointTracker>();
            int targetIndex = tracker != null ? tracker.GetNextCheckpointToTarget() : 0;

            if (targetIndex == -1)
            {
                var finishObj = GameObject.Find("FinishLine");
                if (finishObj != null)
                {
                    Debug.Log($"[CPUPlayer] Targeting finish line.");
                    return finishObj.transform;
                }
                return null;
            }

            foreach (var cp in checkpoints)
            {
                if (cp.index == targetIndex)
                {
                    Debug.Log($"[CPUPlayer] Targeting checkpoint {cp.index}.");
                    return cp.transform;
                }
            }

            // Fallback: closest checkpoint
            Transform best = null;
            float bestDist = float.MaxValue;
            foreach (var cp in checkpoints)
            {
                float d = Vector2.Distance(cp.transform.position, _kart.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = cp.transform;
                }
            }
            return best;
        }
    }
}
