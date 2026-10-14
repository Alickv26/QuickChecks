using UnityEngine;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// Replays a recorded ghost by advancing through STOP POSITIONS.
    ///
    /// Each stop position = where the kart stopped after a swipe.
    /// The ghost advances one stop position per turn, creating a true
    /// 1:1 correspondence with the player's swipes.
    ///
    /// TURN-TAKING SEQUENCE:
    /// 1. Player swipes → player moves → camera follows player
    /// 2. Player stops → camera pans to ghost
    /// 3. Ghost advances to NEXT stop position (smooth interpolation)
    /// 4. Ghost stops → camera pans back to player
    /// 5. Repeat
    ///
    /// The ghost moves to the EXACT position where the kart stopped after
    /// the corresponding swipe in the recorded lap. This guarantees
    /// the ghost's path matches the recorded lap exactly.
    /// </summary>
    public class GhostPlayer : MonoBehaviour
    {
        [SerializeField] private Transform ghostKart;

        [Tooltip("Time (seconds) for ghost to move between stop positions.")]
        [SerializeField] private float moveDurationSec = 0.8f;

        private GhostData _data;
        private int _currentStopIndex = 0;
        private bool _isPlaying = false;
        private bool _isAdvancing = false;
        private float _advanceTimer = 0f;
        private Vector3 _advanceStartPos;
        private Vector3 _advanceEndPos;
        private Quaternion _advanceStartRot;
        private Quaternion _advanceEndRot;

        public bool IsPlaying => _isPlaying;
        public bool IsMoving => _isAdvancing;
        public int CurrentStopIndex => _currentStopIndex;
        public int TotalStops => _data?.stopPositions?.Count ?? 0;
        public Transform GhostKartTransform => ghostKart;

        public void StartPlayback(GhostData data, long startUnixMs)
        {
            _data = data;
            _currentStopIndex = 0;
            _isPlaying = true;
            _isAdvancing = false;

            if (_data != null && _data.stopPositions != null && _data.stopPositions.Count > 0)
            {
                // Set ghost to starting position
                ApplyStopPosition(0);
                Debug.Log($"[GhostPlayer] Playback started. {TotalStops} stop positions. " +
                          $"Starting at stop 0: ({_data.stopPositions[0].x:F1}, {_data.stopPositions[0].y:F1})");
            }
            else
            {
                Debug.LogWarning("[GhostPlayer] No stop positions to replay!");
                _isPlaying = false;
            }
        }

        public void StopPlayback()
        {
            _isPlaying = false;
            _isAdvancing = false;
        }

        /// <summary>
        /// Called when the player has STOPPED moving.
        /// Advances the ghost to the NEXT stop position (where the kart stopped
        /// after the corresponding swipe in the recorded lap).
        /// </summary>
        public void AdvanceGhost()
        {
            if (!_isPlaying || _data == null) return;
            if (_isAdvancing) return;  // Already moving
            if (_currentStopIndex >= TotalStops - 1)  // Reached end
            {
                Debug.Log("[GhostPlayer] Already at last stop position — playback complete.");
                _isPlaying = false;
                return;
            }

            int nextIndex = _currentStopIndex + 1;
            _advanceTimer = 0f;
            _isAdvancing = true;

            _advanceStartPos = ghostKart.position;
            _advanceStartRot = ghostKart.rotation;
            _advanceEndPos = GetStopPosition(nextIndex);
            _advanceEndRot = GetStopRotation(nextIndex);

            Debug.Log($"[GhostPlayer] Advancing to stop {nextIndex}/{TotalStops} " +
                      $"(swipe {_data.stopPositions[nextIndex].swipeIndex}): " +
                      $"({ghostKart.position.x:F1},{ghostKart.position.y:F1}) → " +
                      $"({_advanceEndPos.x:F1},{_advanceEndPos.y:F1})");
        }

        private void Update()
        {
            if (!_isPlaying || _data == null) return;

            if (_isAdvancing)
            {
                _advanceTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_advanceTimer / moveDurationSec);

                // Smooth interpolation (ease-in-out)
                float smoothT = t * t * (3f - 2f * t);  // smoothstep

                if (ghostKart != null)
                {
                    ghostKart.position = Vector3.Lerp(_advanceStartPos, _advanceEndPos, smoothT);
                    ghostKart.rotation = Quaternion.Slerp(_advanceStartRot, _advanceEndRot, smoothT);
                }

                if (t >= 1f)
                {
                    _isAdvancing = false;
                    _currentStopIndex++;
                    Debug.Log($"[GhostPlayer] Reached stop {_currentStopIndex}/{TotalStops} " +
                              $"at ({_advanceEndPos.x:F1},{_advanceEndPos.y:F1})");

                    if (_currentStopIndex >= TotalStops - 1)
                    {
                        _isPlaying = false;
                        Debug.Log("[GhostPlayer] Playback complete — ghost reached final position.");
                    }
                }
            }
        }

        private void ApplyStopPosition(int index)
        {
            if (_data == null || _data.stopPositions == null || index < 0 || index >= TotalStops) return;
            if (ghostKart == null) return;

            var stop = _data.stopPositions[index];
            ghostKart.position = new Vector3(stop.x, stop.y, 0);
            ghostKart.rotation = Quaternion.Euler(0, 0, stop.rot);
        }

        private Vector3 GetStopPosition(int index)
        {
            if (_data == null || index < 0 || index >= TotalStops) return Vector3.zero;
            var s = _data.stopPositions[index];
            return new Vector3(s.x, s.y, 0);
        }

        private Quaternion GetStopRotation(int index)
        {
            if (_data == null || index < 0 || index >= TotalStops) return Quaternion.identity;
            var s = _data.stopPositions[index];
            return Quaternion.Euler(0, 0, s.rot);
        }

        private void OnDisable()
        {
            StopPlayback();
        }
    }
}
