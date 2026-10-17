using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuickChecks.Audio
{
    /// <summary>
    /// Bootstrap for the audio system + Supabase backend. Created once at app launch.
    /// Spawns MusicPlayer + SfxPlayer + SupabaseRpcClient + SupabaseAuth + SupabaseLeaderboard
    /// as DontDestroyOnLoad singletons.
    ///
    /// Also kicks off anonymous Supabase sign-in on first launch (Decision 11) so by the
    /// time the player reaches the Results screen, the leaderboard can be fetched without
    /// a delay.
    ///
    /// Usage: Add to MainMenu scene (or any first-loaded scene).
    /// </summary>
    public class AudioBootstrap : MonoBehaviour
    {
        private static bool _initialized = false;

        private void Awake()
        {
            if (_initialized) return;
            _initialized = true;

            // --- Audio singletons ---
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

            var sfxGo = new GameObject("[SfxPlayer]");
            var sfxSource = sfxGo.AddComponent<AudioSource>();
            sfxGo.AddComponent<SfxPlayer>();

            Debug.Log("[AudioBootstrap] Audio system initialized (MusicPlayer + SfxPlayer).");

            // Play menu music on launch
            musicPlayer.PlayRaceMusic();  // For now, race music is also menu music

            // --- Supabase backend singletons (Phase 2) ---
            // Spawned here so every scene has them available without each scene having to
            // include them explicitly. Each is a DontDestroyOnLoad singleton.
            if (FindObjectOfType<Multiplayer.SupabaseClient>() == null)
            {
                var supabaseGo = new GameObject("[SupabaseClient]");
                supabaseGo.AddComponent<Multiplayer.SupabaseClient>();  // spawns the three real singletons in Awake
                Debug.Log("[AudioBootstrap] Supabase backend initialized (RpcClient + Auth + Leaderboard).");

                // Kick off anonymous sign-in. The result is cached + reused on subsequent launches.
                // We don't await — sign-in completes in the background while the player navigates menus.
                StartCoroutine(SignInWhenReady());
            }
        }

        private System.Collections.IEnumerator SignInWhenReady()
        {
            // Wait a frame so all Awake() calls finish + the SupabaseAuth.Instance is ready.
            yield return null;
            if (Multiplayer.SupabaseAuth.Instance != null)
            {
                Multiplayer.SupabaseAuth.Instance.StartCoroutine(
                    Multiplayer.SupabaseAuth.Instance.SignInAndLoadProfile(ok =>
                    {
                        Debug.Log($"[AudioBootstrap] Supabase sign-in result: {ok}");
                    })
                );
            }
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
