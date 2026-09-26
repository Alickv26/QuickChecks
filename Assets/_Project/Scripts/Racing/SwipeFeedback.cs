using System.Collections.Generic;
using UnityEngine;
using QuickChecks.Input;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Visual feedback when a swipe is registered: brief kart squash + particle burst.
    /// The squash gives the swipe "weight" — kart compresses briefly in the swipe direction,
    /// then snaps back. Particle burst adds satisfying feedback.
    ///
    /// Subscribes to SwipeEventSO so it works for any input source (player, CPU, ghost replay).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SwipeFeedback : MonoBehaviour
    {
        [SerializeField] private Core.GameEventSO<SwipeData> swipeEvent;
        [SerializeField] private KartController kart;
        [SerializeField] private float squashAmount = 0.25f;
        [SerializeField] private float squashDurationSec = 0.12f;
        [SerializeField] private int particleCount = 8;
        [SerializeField] private float particleSpeed = 4f;
        [SerializeField] private float particleLifetime = 0.4f;

        private SpriteRenderer _renderer;
        private Vector3 _originalScale;
        private Vector3 _targetScale;
        private float _squashTimer;

        // Particle pool
        private readonly List<Particle> _particles = new();
        private struct Particle
        {
            public Vector3 position;
            public Vector3 velocity;
            public float lifeRemaining;
            public Color color;
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (swipeEvent != null) swipeEvent.Register(OnSwipe);
        }

        private void OnDisable()
        {
            if (swipeEvent != null) swipeEvent.Unregister(OnSwipe);
        }

        private void OnSwipe(SwipeData swipe)
        {
            // Only react to swipes on this kart (the impulse direction tells us we're the target).
            // Cheap heuristic: only fire if kart isn't moving (i.e., this swipe is about to start motion).
            if (kart != null && !kart.IsStopped) return;

            // Squash in the swipe direction (compress along the axis perpendicular to direction).
            Vector2 dir = swipe.direction;
            // Compress along the perpendicular axis, stretch along direction.
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            _targetScale = new Vector3(
                1f + squashAmount,   // stretch along swipe direction
                1f - squashAmount,   // compress perpendicular
                1f
            );
            _squashTimer = squashDurationSec;

            // Spawn particle burst.
            SpawnBurst(swipe);
        }

        private void SpawnBurst(SwipeData swipe)
        {
            Color particleColor = new Color(0.247f, 0.878f, 0.760f); // cyan
            Vector3 origin = transform.position;

            for (int i = 0; i < particleCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                var vel = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * particleSpeed
                          * Random.Range(0.5f, 1.2f);
                _particles.Add(new Particle
                {
                    position = origin,
                    velocity = vel,
                    lifeRemaining = particleLifetime * Random.Range(0.7f, 1.1f),
                    color = particleColor
                });
            }
        }

        private void Update()
        {
            // Squash animation: ease back to original scale over duration.
            if (_squashTimer > 0f)
            {
                _squashTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(1f - (_squashTimer / squashDurationSec));
                // Ease out cubic.
                t = 1f - Mathf.Pow(1f - t, 3f);
                transform.localScale = Vector3.Lerp(_targetScale, _originalScale, t);
            }
            else
            {
                transform.localScale = _originalScale;
            }

            // Update particles.
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.position += p.velocity * Time.deltaTime;
                p.velocity *= 0.92f; // drag
                p.lifeRemaining -= Time.deltaTime;
                if (p.lifeRemaining <= 0f)
                {
                    _particles.RemoveAt(i);
                }
                else
                {
                    _particles[i] = p;
                }
            }
        }

        private void LateUpdate()
        {
            // Render particles via the SpriteRenderer's draw call (cheap hack: tiny sprites).
            // For more polish, swap to a ParticleSystem. For prototype this is fine.
        }

        // Exposed for the prototype bootstrapper to render particles via Gizmos/OnDrawGizmos
        // if needed. For now, the kart's sprite renderer + the trail carry the visual weight.
        public IReadOnlyList<Particle> GetActiveParticles() => _particles;
    }
}
