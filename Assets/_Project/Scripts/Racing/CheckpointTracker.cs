using System;
using UnityEngine;
using QuickChecks.Track;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Tracks which checkpoints a kart has passed. Validates they're passed
    /// in order (0, 1, 2, ..., N-1). When all checkpoints are passed AND
    /// the kart crosses the finish line, a lap is completed.
    ///
    /// Wire up:
    ///   - SetCheckpoints() called by the track builder with the ordered list of Checkpoint components
    ///   - SetFinishLine() called with the finish line's Collider2D
    ///   - OnLapCompleted event fires when a lap is validly completed
    /// </summary>
    public class CheckpointTracker : MonoBehaviour
    {
        [SerializeField] private int totalLaps = 1;

        [Header("State (debug)")]
        [SerializeField] private int nextCheckpointIndex = 0;
        [SerializeField] private int currentLap = 0;
        [SerializeField] private int totalCheckpointsPassed = 0;

        private Checkpoint[] _checkpoints;
        private Collider2D _finishLineCollider;
        private bool _allCheckpointsPassedThisLap = false;

        public int CurrentLap => currentLap;
        public int TotalLaps => totalLaps;
        public int NextCheckpointIndex => nextCheckpointIndex;
        public int TotalCheckpointsPassed => totalCheckpointsPassed;
        public bool RaceComplete => currentLap >= totalLaps;

        /// <summary>Event with the lap number (1-based) that was just completed.</summary>
        public event Action<int> OnLapCompleted;

        /// <summary>Event when the entire race is complete (all laps done).</summary>
        public event Action<int> OnRaceComplete;   // total checkpoints passed

        /// <summary>
        /// Called by SplineTrackBuilder or the bootstrapper to wire up checkpoint references.
        /// </summary>
        public void SetCheckpoints(Checkpoint[] checkpoints, Collider2D finishLineCollider, int laps = 1)
        {
            _checkpoints = checkpoints;
            _finishLineCollider = finishLineCollider;
            totalLaps = Mathf.Max(1, laps);
            nextCheckpointIndex = 0;
            currentLap = 0;
            totalCheckpointsPassed = 0;
            _allCheckpointsPassedThisLap = false;
        }

        /// <summary>Resets the tracker for a new race.</summary>
        public void Reset()
        {
            nextCheckpointIndex = 0;
            currentLap = 0;
            totalCheckpointsPassed = 0;
            _allCheckpointsPassedThisLap = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Check if we hit a checkpoint trigger
            var checkpoint = other.GetComponent<Checkpoint>();
            if (checkpoint != null)
            {
                HandleCheckpointPassed(checkpoint);
                return;
            }

            // Check if we hit the finish line
            if (_finishLineCollider != null && other == _finishLineCollider)
            {
                HandleFinishLineCrossed();
            }
        }

        private void HandleCheckpointPassed(Checkpoint checkpoint)
        {
            // Only count if it's the expected next checkpoint in order
            if (checkpoint.index != nextCheckpointIndex)
            {
                // Out of order — ignore (or could penalize player for skipping)
                Debug.Log($"[CheckpointTracker] Out-of-order checkpoint {checkpoint.index} " +
                          $"(expected {nextCheckpointIndex}) — ignored.");
                return;
            }

            totalCheckpointsPassed++;
            nextCheckpointIndex++;

            if (_checkpoints != null && nextCheckpointIndex >= _checkpoints.Length)
            {
                _allCheckpointsPassedThisLap = true;
                Debug.Log($"[CheckpointTracker] All checkpoints passed! Cross finish line to complete lap.");
            }
            else
            {
                Debug.Log($"[CheckpointTracker] Checkpoint {checkpoint.index} passed. " +
                          $"Next: {nextCheckpointIndex}/{_checkpoints?.Length ?? 0}");
            }
        }

        private void HandleFinishLineCrossed()
        {
            if (RaceComplete) return;

            // Lap completes only if all checkpoints were passed in order this lap
            if (!_allCheckpointsPassedThisLap)
            {
                Debug.Log("[CheckpointTracker] Finish line crossed but checkpoints not all passed — lap not counted.");
                return;
            }

            currentLap++;
            Debug.Log($"[CheckpointTracker] Lap {currentLap}/{totalLaps} completed! " +
                      $"Total checkpoints passed: {totalCheckpointsPassed}");
            OnLapCompleted?.Invoke(currentLap);

            // Reset for next lap
            nextCheckpointIndex = 0;
            _allCheckpointsPassedThisLap = false;

            if (currentLap >= totalLaps)
            {
                Debug.Log($"[CheckpointTracker] RACE COMPLETE! Total checkpoints: {totalCheckpointsPassed}");
                OnRaceComplete?.Invoke(totalCheckpointsPassed);
            }
        }
    }
}
