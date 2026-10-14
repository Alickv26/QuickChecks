using UnityEngine;
using QuickChecks.Racing;

namespace QuickChecks.Camera
{
    /// <summary>
    /// Controls which kart the camera follows. In turn-based ghost racing,
    /// the camera should follow whoever is currently moving:
    /// - If the ghost kart is moving (velocity > threshold), follow the ghost
    /// - Otherwise, follow the player kart
    ///
    /// This creates the turn-taking visual feel: player swipes, camera follows player;
    /// ghost swipes, camera pans to follow ghost; ghost stops, camera returns to player.
    /// </summary>
    public class CameraFollowController : MonoBehaviour
    {
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private KartController playerKart;
        [SerializeField] private KartController ghostKart;

        [Tooltip("Velocity threshold above which a kart is considered 'moving'.")]
        [SerializeField] private float movingThreshold = 15f;

        [Tooltip("How quickly the camera switches targets (0 = instant, 1 = very slow).")]
        [Range(0f, 1f)] [SerializeField] private float switchSmoothing = 0.05f;

        private Transform _currentTarget;
        private bool _followingGhost = false;

        private void Update()
        {
            if (cameraRig == null || playerKart == null) return;

            // Determine which kart to follow
            bool ghostMoving = ghostKart != null && ghostKart.Velocity.magnitude > movingThreshold && !ghostKart.IsStopped;
            bool playerMoving = playerKart.Velocity.magnitude > movingThreshold && !playerKart.IsStopped;

            // Follow ghost if it's moving; otherwise follow player
            if (ghostMoving && !_followingGhost)
            {
                _followingGhost = true;
                cameraRig.target = ghostKart.transform;
                Debug.Log("[CameraFollow] Switched to follow GHOST (moving).");
            }
            else if (!ghostMoving && _followingGhost)
            {
                _followingGhost = false;
                cameraRig.target = playerKart.transform;
                Debug.Log("[CameraFollow] Switched to follow PLAYER.");
            }
        }

        /// <summary>
        /// Sets the ghost kart reference (called by SoloGhostPlayer when ghost spawns).
        /// </summary>
        public void SetGhostKart(KartController ghost)
        {
            ghostKart = ghost;
            Debug.Log($"[CameraFollow] Ghost kart assigned: {(ghost != null ? ghost.gameObject.name : "null")}");
        }

        /// <summary>
        /// Sets the player kart reference (called by PrototypeBootstrapper).
        /// </summary>
        public void SetPlayerKart(KartController player)
        {
            playerKart = player;
            if (cameraRig != null && player != null)
            {
                cameraRig.target = player.transform;
            }
        }
    }
}
