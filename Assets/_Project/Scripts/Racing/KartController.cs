using UnityEngine;
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

        /// <summary>
        /// Public setter for swipeEvent — used by SoloGhostPlayer to assign the ghost
        /// kart's swipe event at runtime. Triggers immediate subscription if the
        /// component is already enabled (skips the OnEnable race condition).
        /// </summary>
        public void SetSwipeEvent(Core.GameEventSO<SwipeData> newSwipeEvent)
        {
            // Unsubscribe from old event if we were subscribed
            if (swipeEvent != null && _isSubscribed)
            {
                swipeEvent.Unregister(OnSwipe);
                _isSubscribed = false;
            }

            swipeEvent = newSwipeEvent;

            // Immediately try to subscribe to the new event
            TrySubscribe(silent: true);
        }

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
            _rb.gravityScale = 0f;       // Top-down game — no gravity.
            _rb.drag = 0f;                // We do friction manually for predictability.
            _rb.angularDrag = 0f;
            _rb.mass = 1f;                // Default mass, explicit for predictability.

            // DYNAMIC (not Kinematic) so collisions actually happen with static colliders (walls).
            // Kinematic bodies don't collide with static colliders — they pass through.
            // Dynamic bodies with gravityScale=0 + drag=0 behave like Kinematic for our purposes,
            // but DO generate collision responses when they hit BoxCollider2D walls.
            _rb.bodyType = RigidbodyType2D.Dynamic;

            // Freeze Z rotation so collisions don't spin the kart uncontrollably.
            // We control rotation manually via MoveRotation in FixedUpdate.
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // Collision detection: Continuous prevents fast-moving kart from tunneling through thin walls.
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Sleep mode: Never sleep — we want responsive physics even when kart is idle.
            _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;

            _mainCam = UnityEngine.Camera.main;
        }

        private void OnEnable()
        {
            // OnEnable fires immediately on AddComponent, before reflection-based wiring
            // sets the swipeEvent field. Don't warn here — Start() will retry and warn
            // if swipeEvent is STILL null at that point (which would indicate a real problem).
            TrySubscribe(silent: true);
        }

        private void Start()
        {
            // Re-attempt subscription (this is the reliable point — after all reflection
            // field assignment in the bootstrapper has completed).
            TrySubscribe(silent: false);
        }

        private void OnDisable()
        {
            if (swipeEvent != null && _isSubscribed)
            {
                swipeEvent.Unregister(OnSwipe);
                _isSubscribed = false;
            }
        }

        private bool _isSubscribed = false;

        private void TrySubscribe(bool silent = false)
        {
            if (swipeEvent != null && !_isSubscribed)
            {
                swipeEvent.Register(OnSwipe);
                _isSubscribed = true;
                Debug.Log($"[Kart] Subscribed to swipeEvent on {gameObject.name}");
            }
            else if (swipeEvent == null && !silent)
            {
                Debug.LogWarning($"[Kart] TrySubscribe called but swipeEvent is still null on {gameObject.name} " +
                                  "in Start(). Field was not wired via reflection. Check the bootstrapper.");
            }
        }

        private void OnSwipe(SwipeData swipe)
        {
            if (isOffTrack)
            {
                Debug.LogWarning("[Kart] Swipe received but kart is OFF_TRACK. Respawn needed.");
                return;
            }

            if (stats == null)
            {
                Debug.LogError("[Kart] Swipe received but stats is null! " +
                               "KartStats ScriptableObject not assigned. Check Inspector on " + gameObject.name);
                return;
            }

            // Convert screen-space direction to world-space (top-down: just rotate).
            Vector2 worldDir = ScreenDirectionToWorld(swipe.direction);

            float impulse = swipe.magnitude * stats.impulseMultiplier;
            // In gravel: reduce swipe impulse (each swipe moves the kart less)
            if (_inGravel) impulse *= _gravelSwipeMult;
            // Cap impulse so resulting velocity doesn't exceed maxSpeed.
            impulse = Mathf.Min(impulse, stats.maxSpeed);

            // SET velocity directly (billiards/golf model — replaces, not adds).
            // We use MovePosition in FixedUpdate which respects collisions because
            // the Rigidbody is Dynamic (not Kinematic).
            currentVelocity = worldDir * impulse;
            isStopped = false;

            Debug.Log($"[Kart] Impulse applied: vel={currentVelocity}, mag={currentVelocity.magnitude:F0} " +
                      $"(impulse={impulse:F0}, mult={stats.impulseMultiplier}, maxSpeed={stats.maxSpeed})");
        }

        private void FixedUpdate()
        {
            // Always record position for rewind (even when stopped, so first swipe has history)
            RecordPositionForRewind();

            if (isStopped) return;

            if (stats == null)
            {
                Debug.LogError("[Kart] stats is null in FixedUpdate — cannot apply friction. Stopping kart.");
                isStopped = true;
                currentVelocity = Vector2.zero;
                return;
            }

            if (_rb == null)
            {
                Debug.LogError("[Kart] _rb is null in FixedUpdate — Rigidbody2D missing. Stopping kart.");
                isStopped = true;
                return;
            }

            // Apply friction (per-second rate, scaled by fixed deltaTime).
            float friction = Mathf.Clamp01(stats.frictionPerSecond * Time.fixedDeltaTime);
            // In gravel: apply MUCH more friction (kart decelerates faster)
            if (_inGravel) friction = Mathf.Clamp01(friction + (1f - _gravelFrictionMult) * Time.fixedDeltaTime * 5f);
            currentVelocity *= (1f - friction);

            // Stop if below threshold.
            if (currentVelocity.magnitude < stats.stopThreshold)
            {
                currentVelocity = Vector2.zero;
                isStopped = true;
                _rb.velocity = Vector2.zero;
            }
            else
            {
                // Cap at maxSpeed.
                if (currentVelocity.magnitude > stats.maxSpeed)
                    currentVelocity = currentVelocity.normalized * stats.maxSpeed;
            }

            // Drive the Rigidbody2D's velocity directly (Dynamic body — collisions still apply).
            // Setting .velocity on a Dynamic Rigidbody2D is the recommended way to drive it
            // while still respecting collision responses. Walls (BoxCollider2D) will stop the kart.
            _rb.velocity = currentVelocity;

            // Rotate kart to face velocity direction (for sprite flip / visual).
            if (currentVelocity.sqrMagnitude > 1f)
            {
                float angle = Mathf.Atan2(currentVelocity.y, currentVelocity.x) * Mathf.Rad2Deg;
                _rb.MoveRotation(angle);
            }
        }

        // Unity's collision callbacks — fired by Dynamic Rigidbody2D hitting BoxCollider2D walls.
        private void OnCollisionEnter2D(Collision2D collision)
        {
            // BOUNCE MODE: reflect velocity off the wall instead of stopping
            if (_bounceMode && currentVelocity.magnitude > 5f)
            {
                // Get the collision normal (direction the wall pushes back)
                Vector2 normal = collision.contacts[0].normal;
                // Reflect velocity: v' = v - 2(v·n)n
                Vector2 reflected = Vector2.Reflect(currentVelocity, normal);
                currentVelocity = reflected * _bounceRetention;
                if (_rb != null) _rb.velocity = currentVelocity;
                isStopped = false;
                Debug.Log($"[Kart] BOUNCED off '{collision.gameObject.name}' — velocity reflected, " +
                          $"new direction ({currentVelocity.normalized.x:F2}, {currentVelocity.normalized.y:F2}), " +
                          $"speed {currentVelocity.magnitude:F0} u/s.");
                return;
            }

            // PHASE DODGE: ignore this collision, pass through
            if (_phaseDodgeActive)
            {
                _phaseDodgeActive = false;
                Debug.Log($"[Kart] Phase Dodge consumed — passed through '{collision.gameObject.name}'.");
                return;
            }

            // Normal: stop the kart on wall impact (golf-feel: stop dead).
            currentVelocity = Vector2.zero;
            if (_rb != null) _rb.velocity = Vector2.zero;
            isStopped = true;

            Debug.Log($"[Kart] Collision with '{collision.gameObject.name}' — kart stopped (golf-feel).");
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            // If we're somehow still moving while touching a wall, zero out velocity.
            if (!isStopped && currentVelocity.sqrMagnitude > 0.01f)
            {
                currentVelocity = Vector2.zero;
                if (_rb != null) _rb.velocity = Vector2.zero;
                isStopped = true;
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
            isStopped = false;
            Debug.Log($"[Kart] Boost applied: x{multiplier}, new velocity {currentVelocity.magnitude:F0} u/s.");
        }

        /// <summary>
        /// Slingshot power-up: sets velocity to a specific vector (direction + magnitude).
        /// Used by SlingshotPowerUp to pull kart toward next checkpoint.
        /// </summary>
        public void ApplySlingshot(Vector2 newVelocity)
        {
            currentVelocity = newVelocity;
            isStopped = false;
            if (_rb != null) _rb.velocity = newVelocity;
            Debug.Log($"[Kart] Slingshot applied: new velocity {currentVelocity} (mag {currentVelocity.magnitude:F0}).");
        }

        /// <summary>
        /// Phase Dodge power-up: when active, next boundary exit doesn't stop the kart.
        /// The kart "phases through" one wall.
        /// </summary>
        private bool _phaseDodgeActive = false;
        public bool IsPhaseDodgeActive => _phaseDodgeActive;

        public void ActivatePhaseDodge()
        {
            _phaseDodgeActive = true;
            Debug.Log("[Kart] Phase Dodge activated — next boundary hit will be ignored.");
        }

        public void DeactivatePhaseDodge()
        {
            _phaseDodgeActive = false;
        }

        /// <summary>
        /// Rewind power-up: stores position history, rewinds 1 second on use.
        /// </summary>
        private readonly System.Collections.Generic.List<Vector2> _positionHistory = new();
        private const float REWIND_HISTORY_DURATION_SEC = 2f;
        private const float REWIND_SAMPLE_INTERVAL = 0.05f;
        private float _lastRewindSampleTime = 0f;

        public void RecordPositionForRewind()
        {
            if (Time.time - _lastRewindSampleTime >= REWIND_SAMPLE_INTERVAL)
            {
                _lastRewindSampleTime = Time.time;
                _positionHistory.Add(_rb != null ? _rb.position : (Vector2)transform.position);
                // Trim history to ~2 seconds worth of samples
                int maxSamples = Mathf.CeilToInt(REWIND_HISTORY_DURATION_SEC / REWIND_SAMPLE_INTERVAL);
                while (_positionHistory.Count > maxSamples)
                {
                    _positionHistory.RemoveAt(0);
                }
            }
        }

        public void Rewind(float secondsToRewind)
        {
            int samplesToRewind = Mathf.CeilToInt(secondsToRewind / REWIND_SAMPLE_INTERVAL);
            if (_positionHistory.Count == 0 || samplesToRewind <= 0)
            {
                Debug.Log("[Kart] Rewind: no history available.");
                return;
            }

            int targetIndex = Mathf.Max(0, _positionHistory.Count - samplesToRewind - 1);
            Vector2 rewindPos = _positionHistory[targetIndex];
            if (_rb != null)
            {
                _rb.position = rewindPos;
                _rb.velocity = Vector2.zero;
            }
            currentVelocity = Vector2.zero;
            isStopped = true;
            Debug.Log($"[Kart] Rewound {secondsToRewind}s to position {rewindPos}.");
        }

        // Note: FixedUpdate is defined earlier in this file. Rewind position recording
        // happens via RecordPositionForRewind() called from the existing FixedUpdate.

        // --- NEW POWER-UP FLAGS ---

        // Double Move: player gets an extra swipe before turn switches
        private bool _hasExtraMove = false;
        public bool HasExtraMove => _hasExtraMove;
        public void SetExtraMove(bool value)
        {
            _hasExtraMove = value;
        }
        public bool ConsumeExtraMove()
        {
            if (_hasExtraMove)
            {
                _hasExtraMove = false;
                return true;
            }
            return false;
        }

        // Bounce: kart bounces off walls instead of stopping
        private bool _bounceMode = false;
        private float _bounceRetention = 0.7f;
        public bool IsBounceMode => _bounceMode;
        public void SetBounceMode(bool enabled, float retention)
        {
            _bounceMode = enabled;
            _bounceRetention = retention;
        }

        // Trail Wall: next swipe's trail becomes a barrier
        private bool _trailWallPending = false;
        public bool IsTrailWallPending => _trailWallPending;
        public void SetTrailWallPending(bool value)
        {
            _trailWallPending = value;
        }
        public bool ConsumeTrailWallPending()
        {
            if (_trailWallPending)
            {
                _trailWallPending = false;
                return true;
            }
            return false;
        }
        // Gravel mode: high friction + reduced swipe impulse
        private bool _inGravel = false;
        private float _gravelFrictionMult = 1f;
        private float _gravelSwipeMult = 1f;
        public bool IsInGravel => _inGravel;

        public void SetGravelMode(bool enabled, float frictionMult, float swipeMult)
        {
            _inGravel = enabled;
            _gravelFrictionMult = frictionMult;
            _gravelSwipeMult = swipeMult;
        }
    }
}
