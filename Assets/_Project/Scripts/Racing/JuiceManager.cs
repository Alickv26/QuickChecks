using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Central "juice" coordinator for the polish pass. Singleton, DontDestroyOnLoad.
    /// Provides:
    ///   - Camera shake (subtle offset on the main camera, decays over ~0.3s)
    ///   - Haptics (delegates to HapticsProvider for Android vibration)
    ///   - Particle bursts (pooled single-dot sprites — subtle, max 8 per event, 0.3s life)
    ///   - Slow-motion (Time.timeScale manipulation, auto-restores)
    ///
    /// Style: subtle (Mini Motorways feel). Single-dot particles, short shake, light haptics.
    /// Art Bible compliance: "Single dots, no smoke or complex effects" + "Particle effects that
    /// last >0.5s are anti-patterns."
    ///
    /// Usage from any script:
    ///   JuiceManager.Instance?.Shake(intensity: 0.3f, duration: 0.2f);
    ///   JuiceManager.Instance?.Haptic(HapticsProvider.Boundary);
    ///   JuiceManager.Instance?.Burst(position, color, count: 8);
    ///   JuiceManager.Instance?.SlowMo(0.3f, 0.5f);  // 30% speed, restore over 0.5s
    /// </summary>
    [DefaultExecutionOrder(100)]  // LateUpdate runs AFTER CameraRig so shake applies on top of follow
    public class JuiceManager : MonoBehaviour
    {
        public static JuiceManager Instance { get; private set; }

        [Header("Camera Shake")]
        [Tooltip("Maximum offset in world units at intensity=1.0.")]
        [SerializeField] private float maxShakeOffset = 0.6f;
        [Tooltip("How fast the shake decays (higher = faster decay).")]
        [SerializeField] private float shakeDecayRate = 4f;

        [Header("Particles")]
        [Tooltip("Pool size — how many particle sprites to pre-allocate.")]
        [SerializeField] private int poolSize = 32;
        [Tooltip("Size of each particle sprite (world units).")]
        [SerializeField] private float particleSize = 0.2f;
        [Tooltip("How fast particles decelerate (higher = stop faster).")]
        [SerializeField] private float particleDrag = 3f;

        [Header("Slow-Mo")]
        [Tooltip("How long to hold the slow-mo scale before restoring to 1.0.")]
        [SerializeField] private float slowMoHoldDuration = 0.2f;

        // Shake state
        private float _shakeIntensity = 0f;
        private Vector3 _shakeOffset = Vector3.zero;
        private UnityEngine.Camera _mainCam;

        // Particle pool
        private readonly List<ParticleEntry> _particles = new(32);
        private GameObject _particleRoot;
        private Sprite _dotSprite;

        // Slow-mo state
        private Coroutine _slowMoRoutine;
        private float _originalFixedDeltaTime;

        private struct ParticleEntry
        {
            public Transform transform;
            public SpriteRenderer renderer;
            public Vector3 velocity;
            public float lifeRemaining;
            public float maxLife;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _originalFixedDeltaTime = Time.fixedDeltaTime;
            InitParticlePool();
        }

        private void Start()
        {
            // Cache the main camera (may not be available in Awake if scene is still loading)
            _mainCam = UnityEngine.Camera.main;
        }

        // =====================================================================
        // Camera shake
        // =====================================================================

        /// <summary>
        /// Triggers a camera shake. Multiple calls stack (takes the max intensity).
        /// Intensity 0-1 where 1 = maxShakeOffset world units of jitter.
        /// Duration is implicit — decays at shakeDecayRate per second.
        /// </summary>
        public void Shake(float intensity, float duration = 0.3f)
        {
            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
        }

        private void LateUpdate()
        {
            _mainCam = _mainCam ?? UnityEngine.Camera.main;
            if (_mainCam == null) return;

            // Apply shake offset to the camera (on top of whatever CameraRig set this frame)
            if (_shakeIntensity > 0.01f)
            {
                // Random offset within the intensity radius
                float offset = maxShakeOffset * _shakeIntensity;
                _shakeOffset = new Vector3(
                    Random.Range(-offset, offset),
                    Random.Range(-offset, offset),
                    0f
                );
                _mainCam.transform.position += _shakeOffset;

                // Decay
                _shakeIntensity -= Time.unscaledDeltaTime * shakeDecayRate;
                if (_shakeIntensity < 0f) _shakeIntensity = 0f;
            }
            else
            {
                _shakeOffset = Vector3.zero;
            }

            // Update particles
            UpdateParticles();
        }

        // =====================================================================
        // Haptics
        // =====================================================================

        /// <summary>Calls HapticsProvider.Vibrate with the given duration (ms).</summary>
        public void Haptic(long milliseconds)
        {
            HapticsProvider.Vibrate(milliseconds);
        }

        /// <summary>Convenience: triggers the preset haptic for swipe (15ms).</summary>
        public void HapticSwipe()    => HapticsProvider.Swipe();
        /// <summary>Convenience: triggers the preset haptic for power-up pickup (30ms).</summary>
        public void HapticPickup()   => HapticsProvider.Pickup();
        /// <summary>Convenience: triggers the preset haptic for boundary hit (50ms).</summary>
        public void HapticBoundary() => HapticsProvider.Boundary();
        /// <summary>Convenience: triggers the preset haptic for black hole rewind (80ms).</summary>
        public void HapticBlackHole() => HapticsProvider.BlackHole();
        /// <summary>Convenience: triggers the preset haptic for race finish (100ms).</summary>
        public void HapticFinish()  => HapticsProvider.Finish();

        // =====================================================================
        // Particle bursts
        // =====================================================================

        /// <summary>
        /// Spawns a subtle particle burst at the given position.
        /// Single-dot sprites, max 8 per event, 0.3s lifetime (per Art Bible "subtle" style).
        /// </summary>
        public void Burst(Vector3 position, Color color, int count = 8, float speed = 3f, float lifetime = 0.3f)
        {
            count = Mathf.Min(count, poolSize);
            for (int i = 0; i < count; i++)
            {
                var entry = GetInactiveParticle();
                if (entry.renderer == null) continue;

                // Random direction (radial burst)
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float spd = speed * Random.Range(0.5f, 1.2f);
                entry.velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * spd;
                entry.lifeRemaining = lifetime * Random.Range(0.7f, 1.1f);
                entry.maxLife = entry.lifeRemaining;

                entry.transform.position = position;
                entry.transform.localScale = Vector3.one * particleSize;
                entry.renderer.color = color;
                entry.renderer.enabled = true;
            }
        }

        private void UpdateParticles()
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                if (p.renderer == null || !p.renderer.enabled) continue;

                p.transform.position += p.velocity * Time.unscaledDeltaTime;
                p.velocity *= 1f - particleDrag * Time.unscaledDeltaTime;
                p.lifeRemaining -= Time.unscaledDeltaTime;

                if (p.lifeRemaining <= 0f)
                {
                    p.renderer.enabled = false;
                }
                else
                {
                    // Fade out in the last 50% of life
                    float fadeT = Mathf.Clamp01(p.lifeRemaining / (p.maxLife * 0.5f));
                    var c = p.renderer.color;
                    c.a = fadeT;
                    p.renderer.color = c;
                }
            }
        }

        private ParticleEntry GetInactiveParticle()
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                if (!_particles[i].renderer.enabled)
                    return _particles[i];
            }
            // Pool exhausted — reuse the oldest active one
            return _particles.Count > 0 ? _particles[0] : default;
        }

        // =====================================================================
        // Slow-motion
        // =====================================================================

        /// <summary>
        /// Slows down time for a dramatic moment (e.g. crossing the finish line).
        /// Sets Time.timeScale to `scale`, holds for slowMoHoldDuration, then lerps back to 1.0
        /// over `restoreDuration` seconds.
        /// </summary>
        public void SlowMo(float scale, float restoreDuration)
        {
            if (_slowMoRoutine != null) StopCoroutine(_slowMoRoutine);
            _slowMoRoutine = StartCoroutine(SlowMoRoutine(scale, restoreDuration));
        }

        private IEnumerator SlowMoRoutine(float scale, float restoreDuration)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = _originalFixedDeltaTime * scale;

            // Hold at slow-mo
            yield return new WaitForSecondsRealtime(slowMoHoldDuration);

            // Lerp back to normal
            float elapsed = 0f;
            while (elapsed < restoreDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / restoreDuration);
                // Ease-out
                t = 1f - Mathf.Pow(1f - t, 3f);
                Time.timeScale = Mathf.Lerp(scale, 1f, t);
                Time.fixedDeltaTime = _originalFixedDeltaTime * Time.timeScale;
                yield return null;
            }

            // Restore fully
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            _slowMoRoutine = null;
        }

        // =====================================================================
        // Cleanup
        // =====================================================================

        private void OnDestroy()
        {
            // Ensure timeScale is restored if the manager is destroyed mid-slow-mo
            Time.timeScale = 1f;
            if (_originalFixedDeltaTime > 0f)
                Time.fixedDeltaTime = _originalFixedDeltaTime;
        }

        // =====================================================================
        // Particle pool init
        // =====================================================================

        private void InitParticlePool()
        {
            _particleRoot = new GameObject("[JuiceParticles]");
            _particleRoot.transform.SetParent(transform, false);

            // Create a tiny white square sprite
            const int texSize = 8;
            var tex = new Texture2D(texSize, texSize);
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            _dotSprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), texSize);

            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"Particle_{i}");
                go.transform.SetParent(_particleRoot.transform, false);
                go.transform.localScale = Vector3.one * particleSize;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _dotSprite;
                sr.color = Color.white;
                sr.enabled = false;
                sr.sortingOrder = 50;  // above everything

                _particles.Add(new ParticleEntry
                {
                    transform = go.transform,
                    renderer = sr,
                    velocity = Vector3.zero,
                    lifeRemaining = 0f,
                    maxLife = 0f
                });
            }
        }
    }
}
