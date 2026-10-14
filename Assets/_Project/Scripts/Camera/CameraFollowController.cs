using UnityEngine;
using QuickChecks.Ghost;

namespace QuickChecks.Camera
{
    /// <summary>
    /// Controls which kart the camera follows. In turn-based ghost racing,
    /// the camera follows whoever is currently moving:
    /// - If the ghost is moving (position changing), follow the ghost
    /// - Otherwise, follow the player kart
    ///
    /// Uses GhostPlayer.IsMoving to detect ghost movement (position-based, not velocity-based).
    /// </summary>
    public class CameraFollowController : MonoBehaviour
    {
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private Racing.KartController playerKart;
        [SerializeField] private GhostPlayer ghostPlayer;

        [Tooltip("How long to keep following the ghost after it stops moving (seconds).")]
        [SerializeField] private float ghostFollowLingerSec = 0.5f;

        private bool _followingGhost = false;
        private float _ghostStopTimer = 0f;

        private void Update()
        {
            if (cameraRig == null || playerKart == null) return;

            bool ghostMoving = ghostPlayer != null && ghostPlayer.IsMoving;

            if (ghostMoving)
            {
                _ghostStopTimer = ghostFollowLingerSec;
                if (!_followingGhost && ghostPlayer.ghostKart != null)
                {
                    _followingGhost = true;
                    cameraRig.target = ghostPlayer.ghostKart;
                    Debug.Log("[CameraFollow] Switched to follow GHOST (moving).");
                }
            }
            else
            {
                if (_ghostStopTimer > 0f)
                {
                    _ghostStopTimer -= Time.deltaTime;
                }
                else if (_followingGhost)
                {
                    _followingGhost = false;
                    cameraRig.target = playerKart.transform;
                    Debug.Log("[CameraFollow] Switched to follow PLAYER.");
                }
            }
        }

        public void SetPlayerKart(Racing.KartController player)
        {
            playerKart = player;
            if (cameraRig != null && player != null)
            {
                cameraRig.target = player.transform;
            }
        }

        public void SetGhostPlayer(GhostPlayer ghost)
        {
            ghostPlayer = ghost;
            Debug.Log($"[CameraFollow] Ghost player assigned: {(ghost != null ? "OK" : "null")}");
        }
    }
}
