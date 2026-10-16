using UnityEngine;

namespace QuickChecks.Audio
{
    /// <summary>
    /// Plays one-shot sound effects. Used for:
    /// - Swipe whoosh (pitched by velocity)
    /// - Power-up pickup chime
    /// - Boundary hit thud
    /// - Checkpoint pass tick
    /// - Lap complete fanfare
    /// - Race complete fanfare
    /// - Menu button clicks
    ///
    /// Attach to a GameObject with an AudioSource.
    /// Uses AudioSource.PlayOneShot for overlapping SFX.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SfxPlayer : MonoBehaviour
    {
        public static SfxPlayer Instance { get; private set; }

        [Header("SFX Clips (assign in Inspector or load at runtime)")]
        [SerializeField] private AudioClip swipeWhoosh;
        [SerializeField] private AudioClip powerUpPickup;
        [SerializeField] private AudioClip boundaryHit;
        [SerializeField] private AudioClip checkpointPass;
        [SerializeField] private AudioClip lapComplete;
        [SerializeField] private AudioClip raceComplete;
        [SerializeField] private AudioClip raceFailed;
        [SerializeField] private AudioClip menuClick;
        [SerializeField] private AudioClip menuHover;

        [Header("Settings")]
        [SerializeField] private float sfxVolume = 0.6f;
        [SerializeField] private float swipePitchMin = 0.8f;
        [SerializeField] private float swipePitchMax = 1.5f;

        private AudioSource _audioSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;  // 2D
            _audioSource.volume = sfxVolume;
        }

        // --- Generic playback ---

        public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || _audioSource == null) return;
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(clip, volume * sfxVolume);
        }

        // --- Specific SFX methods ---

        /// <summary>
        /// Plays swipe whoosh. Pitch is scaled by swipe velocity (faster = higher pitch).
        /// Falls back to SwipeAudio synthesized whoosh if no clip assigned.
        /// </summary>
        public void PlaySwipe(float velocity, float maxVelocity)
        {
            if (swipeWhoosh == null) return;  // SwipeAudio.cs handles synthesized fallback

            float t = Mathf.Clamp01(velocity / maxVelocity);
            float pitch = Mathf.Lerp(swipePitchMin, swipePitchMax, t);
            PlayClip(swipeWhoosh, 0.7f, pitch);
        }

        public void PlayPowerUpPickup()
        {
            PlayClip(powerUpPickup, 0.8f, 1.2f);
        }

        public void PlayBoundaryHit()
        {
            PlayClip(boundaryHit, 0.9f, 1f);
        }

        public void PlayCheckpointPass()
        {
            PlayClip(checkpointPass, 0.5f, 1f);
        }

        public void PlayLapComplete()
        {
            PlayClip(lapComplete, 0.8f, 1f);
        }

        public void PlayRaceComplete()
        {
            PlayClip(raceComplete, 1f, 1f);
        }

        public void PlayRaceFailed()
        {
            PlayClip(raceFailed, 0.8f, 0.9f);
        }

        public void PlayMenuClick()
        {
            PlayClip(menuClick, 0.6f, 1f);
        }

        public void PlayMenuHover()
        {
            PlayClip(menuHover, 0.3f, 1f);
        }

        // --- Setters (for runtime clip assignment) ---

        public void SetSwipeWhoosh(AudioClip clip) { swipeWhoosh = clip; }
        public void SetPowerUpPickup(AudioClip clip) { powerUpPickup = clip; }
        public void SetBoundaryHit(AudioClip clip) { boundaryHit = clip; }
        public void SetCheckpointPass(AudioClip clip) { checkpointPass = clip; }
        public void SetLapComplete(AudioClip clip) { lapComplete = clip; }
        public void SetRaceComplete(AudioClip clip) { raceComplete = clip; }
        public void SetRaceFailed(AudioClip clip) { raceFailed = clip; }
        public void SetMenuClick(AudioClip clip) { menuClick = clip; }
        public void SetMenuHover(AudioClip clip) { menuHover = clip; }

        public void SetVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            if (_audioSource != null) _audioSource.volume = sfxVolume;
        }
    }
}
