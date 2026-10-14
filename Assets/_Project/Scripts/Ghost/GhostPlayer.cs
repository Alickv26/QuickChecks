using UnityEngine;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// Replays a recorded ghost by setting its transform.position directly from
    /// recorded position samples. This guarantees the ghost follows the EXACT
    /// same path as the original run — same turns, same wall stops, same everything.
    ///
    /// The ghost is a Kinematic body with isTrigger collider, so it has NO physics
    /// interactions — it doesn't collide with walls or other karts. It just follows
    /// the recorded path like a hologram.
    ///
    /// In SYNCHRONIZED mode (default), the ghost advances its position samples
    /// when the player swipes — creating the turn-taking feel.
    /// </summary>
    public class GhostPlayer : MonoBehaviour
    {
        public enum PlaybackMode
        {
            Synchronized,  // Ghost advances when player swipes (turn-taking)
            RealTime        // Ghost plays at original timestamps (parallel race)
        }

        [SerializeField] private Transform ghostKart;
        [SerializeField] private PlaybackMode mode = PlaybackMode.Synchronized;

        // Number of position samples to advance per player swipe.
        // At 20Hz recording (50ms/sample), 1 second = 20 samples.
        // We advance enough samples to cover ~1 second of ghost movement per player swipe.
        [SerializeField] private int samplesPerSwipe = 20;

        private GhostData _data;
        private int _currentSampleIndex = 0;
        private bool _isPlaying = false;
        private long _playbackStartUnixMs;

        // For RealTime mode
        private bool _advancePending = false;

        public bool IsPlaying => _isPlaying;
        public int CurrentSampleIndex => _currentSampleIndex;
        public int TotalSamples => _data?.positionSamples?.Count ?? 0;
        public Vector3 CurrentPosition => ghostKart != null ? ghostKart.position : Vector3.zero;
        public Transform GhostKartTransform => ghostKart;

        // For camera follow: track if ghost is "moving" (position changed this frame)
        private Vector3 _lastPosition;
        public bool IsMoving { get; private set; }

        public void StartPlayback(GhostData data, long startUnixMs)
        {
            _data = data;
            _currentSampleIndex = 0;
            _playbackStartUnixMs = startUnixMs;
            _isPlaying = true;
            _advancePending = false;
            _lastPosition = ghostKart != null ? ghostKart.position : Vector3.zero;
            IsMoving = false;

            if (_data != null && _data.positionSamples != null && _data.positionSamples.Count > 0)
            {
                // Set initial position
                ApplySample(0);

                if (mode == PlaybackMode.Synchronized)
                {
                    Debug.Log($"[GhostPlayer] Synchronized playback started. {TotalSamples} position samples. " +
                              $"Ghost advances {samplesPerSwipe} samples per player swipe.");
                }
                else
                {
                    Debug.Log($"[GhostPlayer] Real-time playback started. {TotalSamples} position samples.");
                }
            }
            else
            {
                Debug.LogWarning("[GhostPlayer] No position samples to replay!");
                _isPlaying = false;
            }
        }

        public void StopPlayback()
        {
            _isPlaying = false;
        }

        /// <summary>
        /// Called when the player swipes (in Synchronized mode).
        /// Advances the ghost by samplesPerSwipe position samples.
        /// </summary>
        public void OnPlayerSwipe()
        {
            if (!_isPlaying || _data == null) return;
            _advancePending = true;
        }

        private void Update()
        {
            if (!_isPlaying || _data == null) return;

            if (mode == PlaybackMode.Synchronized)
            {
                // Advance ghost position when player swipes
                if (_advancePending)
                {
                    AdvanceGhost(samplesPerSwipe);
                    _advancePending = false;
                }
            }
            else if (mode == PlaybackMode.RealTime)
            {
                // Advance based on elapsed time
                long elapsed = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _playbackStartUnixMs;

                // Find the sample closest to the current elapsed time
                while (_currentSampleIndex < TotalSamples &&
                       _data.positionSamples[_currentSampleIndex].t <= elapsed)
                {
                    _currentSampleIndex++;
                }

                if (_currentSampleIndex > 0 && _currentSampleIndex < TotalSamples)
                {
                    ApplySample(_currentSampleIndex);
                }
            }

            // Check if ghost is moving (position changed)
            if (ghostKart != null)
            {
                float delta = Vector3.Distance(ghostKart.position, _lastPosition);
                IsMoving = delta > 0.1f;
                _lastPosition = ghostKart.position;
            }

            // Check if playback is complete
            if (_currentSampleIndex >= TotalSamples)
            {
                _isPlaying = false;
                IsMoving = false;
                Debug.Log($"[GhostPlayer] Playback complete. Reached sample {_currentSampleIndex}/{TotalSamples}.");
            }
        }

        private void AdvanceGhost(int sampleCount)
        {
            int targetIndex = Mathf.Min(_currentSampleIndex + sampleCount, TotalSamples - 1);

            // Smoothly interpolate to the target sample over a few frames
            // For now, just jump to it — the 50ms intervals are small enough to look smooth
            _currentSampleIndex = targetIndex;
            ApplySample(_currentSampleIndex);

            Debug.Log($"[GhostPlayer] Advanced to sample {_currentSampleIndex}/{TotalSamples} " +
                      $"-> pos ({_data.positionSamples[_currentSampleIndex].x:F1}, " +
                      $"{_data.positionSamples[_currentSampleIndex].y:F1})");
        }

        private void ApplySample(int index)
        {
            if (_data == null || _data.positionSamples == null || index < 0 || index >= TotalSamples) return;
            if (ghostKart == null) return;

            var sample = _data.positionSamples[index];
            ghostKart.position = new Vector3(sample.x, sample.y, 0);
            ghostKart.rotation = Quaternion.Euler(0, 0, sample.rot);
        }

        private void OnDisable()
        {
            StopPlayback();
        }
    }
}
