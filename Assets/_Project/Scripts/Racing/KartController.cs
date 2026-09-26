using UnityEngine;
using UnityEngine.Camera = UnityEngine.Camera;
using QuickChecks.Input;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Applies swipe impulses to a Rigidbody2D and integrates friction.
    /// The kart does NOT have continuous control — each swipe is a discrete
    /// impulse that replaces the current velocity. Between swipes, the kart
    /// coasts and decelerates due to friction.
    ///
    /// Subscribes to SwipeEventSO to receive swipes.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class KartController : MonoBehaviour
    {
        [SerializeField] private KartStats stats;
        [SerializeField] private Core.GameEventSO<SwipeData> swipeEvent;

        [Header("State (debug)")]
        [SerializeField] private Vector2 currentVelocity;
        [SerializeField] private bool isStopped = true;
        [SerializeField] private bool isOffTrack = false;

        public bool IsOffTrack => isOffTrack;
        public bool IsStopped => isStopped;
        public Vector2 Velocity => currentVelocity;
        public KartStats Stats => stats;

        private Rigidbody2D _rb;
        private UnityEngine.Camera _mainCam;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.drag = 0f; // We do friction manually for predictability.
            _rb.angularDrag = 0f;
            _rb.bodyType = RigidbodyType2D.Kinematic; // We drive position manually
            _mainCam = UnityEngine.Camera.main;
        }

        private void OnEnable()
        {
            if (swipeEvent != null)
                swipeEvent.Register(OnSwipe);
        }

        private void OnDisable()
        {
            if (swipeEvent != null)
                swipeEvent.Unregister(OnSwipe);
        }

        private void OnSwipe(SwipeData swipe)
        {
            if (isOffTrack) return; // Kart must be respawned to move again

            // Convert screen-space direction to world-space (top-down: just rotate).
            Vector2 worldDir = ScreenDirectionToWorld(swipe.direction);

            float impulse = swipe.magnitude * stats.impulseMultiplier;
            // Cap impulse so resulting velocity doesn't exceed maxSpeed.
            impulse = Mathf.Min(impulse, stats.maxSpeed);

            currentVelocity = worldDir * impulse;
            isStopped = false;
        }

        private void FixedUpdate()
        {
            if (isStopped) return;

            // Apply friction (per-second rate, scaled by fixed deltaTime).
            float friction = Mathf.Clamp01(stats.frictionPerSecond * Time.fixedDeltaTime);
            currentVelocity *= (1f - friction);

            // Stop if below threshold.
            if (currentVelocity.magnitude < stats.stopThreshold)
            {
                currentVelocity = Vector2.zero;
                isStopped = true;
            }
            else
            {
                // Cap at maxSpeed.
                if (currentVelocity.magnitude > stats.maxSpeed)
                    currentVelocity = currentVelocity.normalized * stats.maxSpeed;
            }

            // Move the kart.
            Vector2 delta = currentVelocity * Time.fixedDeltaTime;
            _rb.MovePosition(_rb.position + delta);

            // Rotate kart to face velocity direction (for sprite flip / visual).
            if (currentVelocity.sqrMagnitude > 1f)
            {
                float angle = Mathf.Atan2(currentVelocity.y, currentVelocity.x) * Mathf.Rad2Deg;
                _rb.MoveRotation(angle);
            }
        }

        /// <summary>
        /// Converts a screen-space 2D direction to world-space 2D direction.
        /// For a top-down camera looking down -Z, this is identity if camera is orthographic
        /// and axis-aligned. If camera is rotated, adjust here.
        /// </summary>
        private Vector2 ScreenDirectionToWorld(Vector2 screenDir)
        {
            // Simple case: top-down ortho camera, no rotation.
            // Swipe up on screen = move +Y in world, swipe right = +X.
            return screenDir.normalized;
        }

        public void MarkOffTrack()
        {
            isOffTrack = true;
            currentVelocity = Vector2.zero;
            isStopped = true;
        }

        public void RespawnAt(Vector2 position)
        {
            _rb.position = position;
            currentVelocity = Vector2.zero;
            isStopped = true;
            isOffTrack = false;
        }

        public void ApplyBoost(float multiplier, float durationSec)
        {
            // TODO: implement boost state with timer. For now, instant velocity multiply.
            currentVelocity *= multiplier;
        }
    }
}
