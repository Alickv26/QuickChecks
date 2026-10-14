using UnityEngine;
using QuickChecks.Ghost;

namespace QuickChecks.Camera
{
    /// <summary>
    /// Controls which kart the camera follows in turn-based ghost racing.
    ///
    /// Sequence:
    /// 1. Player moves → camera follows player
    /// 2. Player stops → camera stays on player briefly (ghostStartDelay)
    /// 3. Ghost starts moving → camera pans to ghost
    /// 4. Ghost stops → camera stays on ghost briefly (ghostFollowLinger)
    /// 5. Camera pans back to player → wait for next swipe
    /// </summary>
    public class CameraFollowController : MonoBehaviour
    {
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private Racing.KartController playerKart;
        [SerializeField] private GhostPlayer ghostPlayer;

        [Tooltip("How long to keep following the ghost after it stops moving (seconds). " +
                 "Gives player time to see where the ghost ended up.")]
        [SerializeField] private float ghostFollowLingerSec = 0.3f;

        private bool _followingGhost = false;
        private float _ghostStopTimer = 0f;

        private void Update()
        {
            if (cameraRig == null || playerKart == null) return;

            bool ghostMoving = ghostPlayer != null && ghostPlayer.IsMoving;

            if (ghostMoving)
            {
                // Ghost is moving — follow it
                _ghostStopTimer = ghostFollowLingerSec;
                if (!_followingGhost && ghostPlayer.GhostKartTransform != null)
                {
                    _followingGhost = true;
                    cameraRig.target = ghostPlayer.GhostKartTransform;
                    Debug.Log("[CameraFollow] → Following GHOST");
                }
            }
            else
            {
                // Ghost not moving — linger, then switch to player
                if (_ghostStopTimer > 0f)
                {
                    _ghostStopTimer -= Time.deltaTime;
                }
                else if (_followingGhost)
                {
                    _followingGhost = false;
                    cameraRig.target = playerKart.transform;
                    Debug.Log("[CameraFollow] → Following PLAYER");
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
