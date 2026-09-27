using UnityEngine;
using QuickChecks.Input;

namespace QuickChecks.Audio
{
    /// <summary>
    /// Synthesized swipe whoosh — no audio asset files needed for prototype.
    /// Generates a short sine-wave burst on each swipe, pitched by swipe velocity.
    ///
    /// Higher velocity = higher pitch. Gives the player audible feedback that
    /// faster swipes are "different" from slower ones.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SwipeAudio : MonoBehaviour
    {
        [SerializeField] private Core.GameEventSO<SwipeData> swipeEvent;
        [SerializeField] private int sampleRate = 44100;
        [SerializeField] private float durationSec = 0.15f;
        [SerializeField] private float baseFrequency = 220f;
        [SerializeField] private float maxFrequency = 880f;
        [SerializeField] private float maxExpectedVelocity = 2000f;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D
            _audioSource.volume = 0.3f;
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
            // Map velocity to frequency.
            float t = Mathf.Clamp01(swipe.rawVelocityPxS / maxExpectedVelocity);
            float frequency = Mathf.Lerp(baseFrequency, maxFrequency, t);

            var clip = GenerateWhooshClip(frequency);
            _audioSource.clip = clip;
            _audioSource.Play();
        }

        private AudioClip GenerateWhooshClip(float frequency)
        {
            int sampleCount = Mathf.RoundToInt(sampleRate * durationSec);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                // Envelope: fast attack, exponential decay.
                float envelope;
                if (time < 0.02f)
                {
                    envelope = time / 0.02f;
                }
                else
                {
                    envelope = Mathf.Exp(-(time - 0.02f) * 8f);
                }

                // Frequency sweep (whoosh effect): start high, drop quickly.
                float sweepFreq = frequency * (1f + 0.5f * Mathf.Exp(-time * 15f));
                float wave = Mathf.Sin(2f * Mathf.PI * sweepFreq * time);

                // Add slight noise for texture.
                float noise = (Random.value - 0.5f) * 0.2f;

                samples[i] = (wave + noise) * envelope;
            }

            var clip = AudioClip.Create("SwipeWhoosh", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
