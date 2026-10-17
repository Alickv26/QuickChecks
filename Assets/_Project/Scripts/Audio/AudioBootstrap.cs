using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuickChecks.Audio
{
    /// <summary>
    /// Bootstrap for the audio system. Created once at app launch.
    /// Spawns MusicPlayer + SfxPlayer as DontDestroyOnLoad singletons.
    /// Loads 'track.mp3' from Resources or Assets/_Project/Audio/Music/.
    ///
    /// Usage: Add to MainMenu scene (or any first-loaded scene).
    /// Or: GameFlowManager can spawn it.
    /// </summary>
    public class AudioBootstrap : MonoBehaviour
    {
        private static bool _initialized = false;

        private void Awake()
        {
            if (_initialized) return;
            _initialized = true;

            // Create MusicPlayer
            var musicGo = new GameObject("[MusicPlayer]");
            var musicSource = musicGo.AddComponent<AudioSource>();
            var musicPlayer = musicGo.AddComponent<MusicPlayer>();

            // Try to load track.mp3 from file path
            AudioClip raceClip = LoadClipFromPath("Assets/_Project/Audio/Music/track.mp3");
            if (raceClip == null)
            {
                // Try Resources folder
                raceClip = Resources.Load<AudioClip>("track");
            }
            if (raceClip != null)
            {
                musicPlayer.SetRaceMusic(raceClip);
                Debug.Log($"[AudioBootstrap] Loaded race music: {raceClip.name} ({raceClip.length:F1}s)");
            }
            else
            {
                Debug.LogWarning("[AudioBootstrap] track.mp3 not found. Place at Assets/_Project/Audio/Music/track.mp3");
            }

            // Create SfxPlayer
            var sfxGo = new GameObject("[SfxPlayer]");
            var sfxSource = sfxGo.AddComponent<AudioSource>();
            sfxGo.AddComponent<SfxPlayer>();

            Debug.Log("[AudioBootstrap] Audio system initialized (MusicPlayer + SfxPlayer).");

            // Play menu music on launch
            musicPlayer.PlayRaceMusic();  // For now, race music is also menu music
        }

        /// <summary>
        /// Loads an AudioClip from a file path using UnityWebRequest (for MP3 files).
        /// </summary>
        private AudioClip LoadClipFromPath(string path)
        {
            // For MP3 files, we need to use UnityWebRequestAudioClipHandler
            // But in the editor, we can try AssetDatabase first
#if UNITY_EDITOR
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) return clip;
#endif

            // For runtime loading (build), use WWW/UnityWebRequest
            // Note: file must be in StreamingAssets or a web URL for runtime loading
            // For now, return null — clips should be assigned via Inspector or Resources
            return null;
        }

        /// <summary>
        /// Called when a race starts. Plays race music.
        /// </summary>
        public static void OnRaceStart()
        {
            if (MusicPlayer.Instance != null)
            {
                MusicPlayer.Instance.PlayRaceMusic();
            }
        }

        /// <summary>
        /// Called when returning to menu. Plays menu music (or stops).
        /// </summary>
        public static void OnReturnToMenu()
        {
            if (MusicPlayer.Instance != null)
            {
                MusicPlayer.Instance.PlayRaceMusic();  // Same track for now
            }
        }
    }
}
