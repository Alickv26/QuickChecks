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
    /// Wall avoidance: casts rays in a fan pattern; picks the direction with
    /// the most clearance toward the checkpoint.
    /// Stuck detection: if the kart hasn't moved between turns, tries a
    /// perpendicular swipe to escape.
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

        [Header("Wall Avoidance")]
        [SerializeField] private float wallCheckDistance = 8f;
        [SerializeField] private float wallAvoidSteerDeg = 30f;
        [SerializeField] private int wallAvoidRayCount = 6;  // More rays = better pathfinding

        [Header("Stuck Detection")]
        [Tooltip("If the kart moves less than this distance between turns, it's considered stuck.")]
        [SerializeField] private float stuckThreshold = 1.0f;
        [Tooltip("Max consecutive stuck turns before the CPU tries a random perpendicular direction.")]
        [SerializeField] private int maxStuckTurns = 2;

        private KartController _kart;
        private Transform _nextCheckpoint;
        private float _thinkTimer;
        private bool _thinking;

        // Stuck detection
        private Vector3 _lastTurnEndPos;
        private int _stuckCount = 0;

        private void Awake()
        {
            _kart = GetComponent<KartController>();
            _lastTurnEndPos = transform.position;
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

            // Check if stuck (hasn't moved much since last turn)
            float moveDist = Vector3.Distance(transform.position, _lastTurnEndPos);
            bool isStuck = moveDist < stuckThreshold;
            if (isStuck)
            {
                _stuckCount++;
                Debug.Log($"[CPUPlayer] Stuck detection: moved {moveDist:F1} units, stuck count = {_stuckCount}");
            }
            else
            {
                _stuckCount = 0;
            }
            _lastTurnEndPos = transform.position;

            // Direction from kart to next checkpoint.
            Vector2 toCheckpoint = (Vector2)(_nextCheckpoint.position - _kart.transform.position);
            float distance = toCheckpoint.magnitude;
            if (distance < 0.1f)
            {
                Debug.Log("[CPUPlayer] Already at target — skipping.");
                return;
            }

            Vector2 direction = toCheckpoint.normalized;

            // STUCK ESCAPE: if stuck for too many turns, try a perpendicular direction
            // to break out of the corner/wall the kart is trapped against.
            if (_stuckCount >= maxStuckTurns)
            {
                // Try perpendicular directions (left or right of the checkpoint direction)
                // Pick whichever has more clearance
                Vector2 leftPerp = Rotate(direction, 90f);
                Vector2 rightPerp = Rotate(direction, -90f);
                float leftClear = GetClearance(leftPerp);
                float rightClear = GetClearance(rightPerp);
                direction = leftClear > rightClear ? leftPerp : rightPerp;
                _stuckCount = 0;  // reset after escape attempt
                Debug.Log($"[CPUPlayer] STUCK ESCAPE: trying perpendicular direction ({direction.x:F2},{direction.y:F2})");
            }
            else
            {
                // Normal: try to avoid walls using fan-cast
                direction = AvoidWalls(direction, distance);
            }

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

            // Magnitude: scale by distance, with a strong floor for competitive play.
            var stats = _kart.Stats;
            float optimalMag = Mathf.Min(distance * 1.5f, stats.maxSpeed * 0.9f);
            optimalMag = Mathf.Max(optimalMag, stats.maxSpeed * 0.7f);

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
                Debug.Log($"[CPUPlayer] Swipe emitted: dir=({direction.x:F2},{direction.y:F2}), mag={magnitude:F0}, stuck={isStuck}");
            }
            else
            {
                Debug.LogError("[CPUPlayer] swipeEvent is null — cannot emit swipe!");
            }
        }

        /// <summary>
        /// Casts a fan of rays to find the best direction that avoids walls
        /// while still heading toward the checkpoint.
        ///
        /// Strategy:
        ///   1. Cast the center ray (directly toward checkpoint)
        ///   2. If blocked, cast rays at increasing angles left and right
        ///   3. Score each direction: clearance distance × dot product with target direction
        ///      (so a direction that has clearance AND heads toward the target wins)
        ///   4. Pick the highest-scoring direction
        /// </summary>
        private Vector2 AvoidWalls(Vector2 baseDirection, float targetDistance)
        {
            float checkDist = Mathf.Min(wallCheckDistance, targetDistance);

            // Score the center direction first
            float centerClearance = GetClearance(baseDirection, checkDist);
            if (centerClearance >= checkDist)
            {
                // Clear path — go straight
                return baseDirection;
            }

            Debug.Log($"[CPUPlayer] Wall ahead at {centerClearance:F1} units — searching for detour.");

            // Fan out: try angles from 15° to 180° on both sides
            float bestScore = -1f;
            Vector2 bestDirection = baseDirection;

            for (int i = 1; i <= wallAvoidRayCount; i++)
            {
                float steerAngle = wallAvoidSteerDeg * i;
                if (steerAngle > 180f) break;

                // Try left
                Vector2 leftDir = Rotate(baseDirection, steerAngle);
                float leftClearance = GetClearance(leftDir, checkDist * 2f);
                // Score: clearance × alignment with target (dot product 0-1)
                float leftAlignment = Mathf.Max(0f, Vector2.Dot(leftDir.normalized, baseDirection));
                float leftScore = leftClearance * (0.3f + 0.7f * leftAlignment);  // weight clearance more than alignment

                // Try right
                Vector2 rightDir = Rotate(baseDirection, -steerAngle);
                float rightClearance = GetClearance(rightDir, checkDist * 2f);
                float rightAlignment = Mathf.Max(0f, Vector2.Dot(rightDir.normalized, baseDirection));
                float rightScore = rightClearance * (0.3f + 0.7f * rightAlignment);

                if (leftScore > bestScore)
                {
                    bestScore = leftScore;
                    bestDirection = leftDir;
                }
                if (rightScore > bestScore)
                {
                    bestScore = rightScore;
                    bestDirection = rightDir;
                }

                // If we found a direction with enough clearance that still heads roughly toward target, use it
                if (bestScore > checkDist * 0.5f)
                {
                    Debug.Log($"[CPUPlayer] Found detour at {steerAngle}° (clearance={bestScore:F1}, score={bestScore:F1}).");
                    break;
                }
            }

            return bestDirection;
        }

        /// <summary>Raycasts in the given direction and returns how far the nearest wall is (or maxDist if clear).</summary>
        private float GetClearance(Vector2 direction, float maxDist = 10f)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                _kart.transform.position, direction, maxDist,
                LayerMask.GetMask("Default"));
            return hit.collider == null ? maxDist : hit.distance;
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
