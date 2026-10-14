using UnityEngine;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// Replays a recorded ghost by setting its transform.position from recorded
    /// position samples. Guarantees the ghost follows the EXACT same path.
    ///
    /// TURN-TAKING SEQUENCE:
    /// 1. Player swipes → player moves → camera follows player
    /// 2. Player stops → camera pans to ghost
    /// 3. Ghost advances ~1 second of recorded path (smooth interpolation) → camera follows ghost
    /// 4. Ghost stops → camera pans back to player
    /// 5. Repeat
    ///
    /// The ghost advance is triggered by the PLAYER STOPPING, not by the player
    /// swiping. This ensures the camera and movements are in sync.
    /// </summary>
    public class GhostPlayer : MonoBehaviour
    {
        [SerializeField] private Transform ghostKart;

        [Tooltip("Number of position samples to advance per turn (~1 second at 20Hz recording = 20 samples).")]
        [SerializeField] private int samplesPerTurn = 20;

        [Tooltip("Time (seconds) over which to interpolate the ghost movement. " +
                 "1.0 = smooth movement over 1 second. 0 = instant jump.")]
        [SerializeField] private float turnDurationSec = 1.0f;

        private GhostData _data;
        private int _currentSampleIndex = 0;
        private int _targetSampleIndex = 0;
        private bool _isPlaying = false;
        private bool _isAdvancing = false;
        private float _advanceTimer = 0f;
        private Vector3 _advanceStartPos;
        private Vector3 _advanceEndPos;
        private Quaternion _advanceStartRot;
        private Quaternion _advanceEndRot;

        public bool IsPlaying => _isPlaying;
        public bool IsAdvancing => _isAdvancing;
        public int CurrentSampleIndex => _currentSampleIndex;
        public int TotalSamples => _data?.positionSamples?.Count ?? 0;
        public Transform GhostKartTransform => ghostKart;
        public bool IsMoving => _isAdvancing;

        public void StartPlayback(GhostData data, long startUnixMs)
        {
            _data = data;
            _currentSampleIndex = 0;
            _targetSampleIndex = 0;
            _isPlaying = true;
            _isAdvancing = false;

            if (_data != null && _data.positionSamples != null && _data.positionSamples.Count > 0)
            {
                ApplySample(0);
                Debug.Log($"[GhostPlayer] Playback started. {TotalSamples} position samples. " +
                          $"Will advance {samplesPerTurn} samples per turn over {turnDurationSec}s.");
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
            _isAdvancing = false;
        }

        /// <summary>
        /// Called when the player has STOPPED moving (not when they swipe).
        /// Advances the ghost by ~1 second of recorded movement, smoothly interpolated.
        /// </summary>
        public void AdvanceGhost()
        {
            if (!_isPlaying || _data == null) return;
            if (_isAdvancing) return;  // Already advancing, ignore
            if (_currentSampleIndex >= TotalSamples - 1) return;  // Reached end

            _targetSampleIndex = Mathf.Min(_currentSampleIndex + samplesPerTurn, TotalSamples - 1);
            _advanceTimer = 0f;
            _isAdvancing = true;

            _advanceStartPos = ghostKart.position;
            _advanceStartRot = ghostKart.rotation;
            _advanceEndPos = GetSamplePosition(_targetSampleIndex);
            _advanceEndRot = GetSampleRotation(_targetSampleIndex);

            Debug.Log($"[GhostPlayer] Advancing from sample {_currentSampleIndex} to {_targetSampleIndex}/{TotalSamples} " +
                      $"over {turnDurationSec}s. Pos: {_advanceStartPos} -> {_advanceEndPos}");
        }

        private void Update()
        {
            if (!_isPlaying || _data == null) return;

            if (_isAdvancing)
            {
                _advanceTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_advanceTimer / turnDurationSec);

                // Smooth interpolation (ease-in-out)
                float smoothT = t * t * (3f - 2f * t);  // smoothstep

                // Interpolate position + rotation
                if (ghostKart != null)
                {
                    ghostKart.position = Vector3.Lerp(_advanceStartPos, _advanceEndPos, smoothT);
                    ghostKart.rotation = Quaternion.Slerp(_advanceStartRot, _advanceEndRot, smoothT);
                }

                if (t >= 1f)
                {
                    _isAdvancing = false;
                    _currentSampleIndex = _targetSampleIndex;
                    Debug.Log($"[GhostPlayer] Reached sample {_currentSampleIndex}/{TotalSamples}.");

                    if (_currentSampleIndex >= TotalSamples - 1)
                    {
                        _isPlaying = false;
                        Debug.Log("[GhostPlayer] Playback complete — ghost reached end of recording.");
                    }
                }
            }
        }

        private void ApplySample(int index)
        {
            if (_data == null || _data.positionSamples == null || index < 0 || index >= TotalSamples) return;
            if (ghostKart == null) return;

            var sample = _data.positionSamples[index];
            ghostKart.position = new Vector3(sample.x, sample.y, 0);
            ghostKart.rotation = Quaternion.Euler(0, 0, sample.rot);
        }

        private Vector3 GetSamplePosition(int index)
        {
            if (_data == null || index < 0 || index >= TotalSamples) return Vector3.zero;
            var s = _data.positionSamples[index];
            return new Vector3(s.x, s.y, 0);
        }

        private Quaternion GetSampleRotation(int index)
        {
            if (_data == null || index < 0 || index >= TotalSamples) return Quaternion.identity;
            var s = _data.positionSamples[index];
            return Quaternion.Euler(0, 0, s.rot);
        }

        private void OnDisable()
        {
            StopPlayback();
        }
    }
}
