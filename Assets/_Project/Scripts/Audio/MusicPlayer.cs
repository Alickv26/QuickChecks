using UnityEngine;

namespace QuickChecks.Audio
{
    /// <summary>
    /// Plays background music during races. Loops the assigned AudioClip.
    /// In the future, will crossfade between different tracks per race track.
    ///
    /// Attach to a GameObject with an AudioSource.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }

        [Header("Music Clips")]
        [Tooltip("Default race music (used for all tracks until individual clips are assigned).")]
        [SerializeField] private AudioClip raceMusic;

        [Tooltip("Menu music (played on main menu, track select, results).")]
        [SerializeField] private AudioClip menuMusic;

        [Header("Settings")]
        [SerializeField] private float musicVolume = 0.4f;
        [SerializeField] private float crossfadeDuration = 1.5f;

        private AudioSource _audioSource;
        private AudioSource _secondarySource;  // For crossfading
        private bool _isCrossfading = false;

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
            _audioSource.loop = true;
            _audioSource.volume = musicVolume;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;  // 2D

            // Create secondary source for crossfading
            var secondaryGo = new GameObject("SecondaryAudioSource");
            secondaryGo.transform.SetParent(transform, false);
            _secondarySource = secondaryGo.AddComponent<AudioSource>();
            _secondarySource.loop = true;
            _secondarySource.volume = 0f;
            _secondarySource.playOnAwake = false;
            _secondarySource.spatialBlend = 0f;
        }

        /// <summary>
        /// Plays race music on loop. Called by RaceSceneController when race starts.
        /// </summary>
        public void PlayRaceMusic()
        {
            if (raceMusic == null)
            {
                Debug.LogWarning("[MusicPlayer] No race music clip assigned — silence.");
                return;
            }
            PlayClip(raceMusic);
            Debug.Log("[MusicPlayer] Playing race music: " + raceMusic.name);
        }

        /// <summary>
        /// Plays menu music on loop. Called by menu scenes.
        /// </summary>
        public void PlayMenuMusic()
        {
            if (menuMusic == null)
            {
                StopMusic();
                return;
            }
            PlayClip(menuMusic);
            Debug.Log("[MusicPlayer] Playing menu music: " + menuMusic.name);
        }

        /// <summary>
        /// Stops all music.
        /// </summary>
        public void StopMusic()
        {
            _audioSource.Stop();
            _secondarySource.Stop();
            Debug.Log("[MusicPlayer] Music stopped.");
        }

        /// <summary>
        /// Sets the race music clip at runtime (for loading different tracks per level).
        /// </summary>
        public void SetRaceMusic(AudioClip clip)
        {
            raceMusic = clip;
        }

        /// <summary>
        /// Sets the volume (0-1).
        /// </summary>
        public void SetVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            _audioSource.volume = musicVolume;
        }

        /// <summary>Returns the current music volume (0-1) — for UI sliders to read on init.</summary>
        public float Volume => musicVolume;

        private void PlayClip(AudioClip clip)
        {
            if (clip == null) return;

            // Simple play (no crossfade for now — can add later)
            _audioSource.clip = clip;
            _audioSource.volume = musicVolume;
            _audioSource.Play();
        }

        /// <summary>
        /// Crossfade between two clips (future enhancement).
        /// </summary>
        private System.Collections.IEnumerator CrossfadeTo(AudioClip newClip)
        {
            if (newClip == null || _isCrossfading) yield break;

            _isCrossfading = true;

            // Move current to secondary
            _secondarySource.clip = _audioSource.clip;
            _secondarySource.time = _audioSource.time;
            _secondarySource.volume = musicVolume;
            _secondarySource.Play();

            // Start new on primary
            _audioSource.clip = newClip;
            _audioSource.volume = 0f;
            _audioSource.Play();

            // Crossfade
            float elapsed = 0f;
            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / crossfadeDuration;
                _audioSource.volume = Mathf.Lerp(0f, musicVolume, t);
                _secondarySource.volume = Mathf.Lerp(musicVolume, 0f, t);
                yield return null;
            }

            _secondarySource.Stop();
            _audioSource.volume = musicVolume;
            _isCrossfading = false;
        }
    }
}
