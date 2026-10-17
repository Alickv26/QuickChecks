using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using QuickChecks.Track;
using QuickChecks.Racing;
using QuickChecks.Ghost;

namespace QuickChecks.Core
{
    /// <summary>
    /// Persistent singleton that manages game flow: scene transitions + player state.
    /// Survives scene loads via DontDestroyOnLoad.
    ///
    /// Scenes:
    ///   MainMenu → TrackSelect → Race → Results → (back to TrackSelect or MainMenu)
    ///
    /// State carried between scenes:
    ///   - Selected track (TrackDefinition)
    ///   - Selected kart (KartStats)
    ///   - Last race results (swipes, time, is new best)
    ///   - Player profile (unlocked tracks, best times, premium status)
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [Header("Scene Names")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string trackSelectScene = "TrackSelect";
        [SerializeField] private string kartSelectScene = "KartSelect";
        [SerializeField] private string raceScene = "Race";
        [SerializeField] private string resultsScene = "Results";

        [Header("Current Selection (carried between scenes)")]
        public TrackDefinition selectedTrack;
        public KartStats selectedKart;
        public RaceMode selectedMode = RaceMode.Solo;

        [Header("Pending External Ghost (set by Leaderboard 'Replay' button)")]
        [Tooltip("If non-null, RaceSceneController loads this ghost into SoloGhostPlayer on race start, " +
                 "replacing the player's local best. Cleared after the race ends.")]
        public GhostData pendingExternalGhost;
        [Tooltip("The trackId the external ghost belongs to — used to validate that the ghost matches the track being raced.")]
        public string pendingExternalTrackId;

        [Header("Last Race Results (carried to Results scene)")]
        public int lastRaceSwipes;
        public long lastRaceTimeMs;
        public bool lastRaceIsNewBest;
        public int lastRaceBestSwipes;
        public long lastRaceBestTimeMs;

        /// <summary>
        /// Full race-end payload — set by RaceSceneController before transitioning to Results scene.
        /// Consumed by ResultsScreenController. When non-null, the legacy scalar fields above are
        /// ignored in favour of this structured payload.
        /// </summary>
        [Header("Full Race Results (Phase 2 — used by all modes)")]
        public RaceResultData lastRaceResults;

        [Header("Player Profile")]
        public PlayerProfile profile;

        public event Action OnProfileChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load player profile
            if (profile == null)
            {
                profile = PlayerProfile.Load();
            }
        }

        // --- Scene transitions ---

        public void GoToMainMenu()
        {
            SceneManager.LoadScene(mainMenuScene);
        }

        public void GoToTrackSelect()
        {
            SceneManager.LoadScene(trackSelectScene);
        }

        /// <summary>
        /// Goes to the Kart Select scene after the player has chosen a track.
        /// The selected track is carried via `selectedTrack`.
        /// </summary>
        public void GoToKartSelect()
        {
            SceneManager.LoadScene(kartSelectScene);
        }

        public void StartRace(TrackDefinition track, KartStats kart)
        {
            selectedTrack = track;
            selectedKart = kart;
            SceneManager.LoadScene(raceScene);
        }

        public void GoToResults(int swipes, long timeMs, bool isNewBest, int bestSwipes, long bestTimeMs)
        {
            lastRaceSwipes = swipes;
            lastRaceTimeMs = timeMs;
            lastRaceIsNewBest = isNewBest;
            lastRaceBestSwipes = bestSwipes;
            lastRaceBestTimeMs = bestTimeMs;
            SceneManager.LoadScene(resultsScene);
        }

        /// <summary>
        /// Phase 2 results path — carries the full structured payload (works for any mode).
        /// Called by RaceSceneController.FinishRace*() after building RaceResultData.
        /// </summary>
        public void GoToResults(RaceResultData results)
        {
            lastRaceResults = results;
            // Also mirror the legacy scalar fields so any code still reading them stays consistent
            // (used by some legacy UI bits and by PlayerProfile recording).
            if (results != null && results.HumanResult != null)
            {
                var h = results.HumanResult;
                lastRaceSwipes = h.swipes;
                lastRaceTimeMs = h.finishTimeMs > 0 ? h.finishTimeMs : results.RaceDurationMs;
                lastRaceIsNewBest = results.isNewBest;
                lastRaceBestSwipes = results.prevBestSwipes;
                lastRaceBestTimeMs = results.prevBestTimeMs;
            }
            SceneManager.LoadScene(resultsScene);
        }

        public void RetryRace()
        {
            SceneManager.LoadScene(raceScene);
        }

        // --- Profile management ---

        public void OnTrackCompleted(string trackId, int swipes, long timeMs)
        {
            if (profile == null) profile = PlayerProfile.Load();

            bool isNewBest = profile.RecordTrackResult(trackId, swipes, timeMs);
            profile.Save();
            OnProfileChanged?.Invoke();
        }

        public bool IsTrackUnlocked(TrackDefinition track)
        {
            if (track == null) return false;
            if (track.isFree) return true;

            // Paid tracks: check entitlement
            var em = Monetization.EntitlementManager.Instance;
            if (em != null && em.IsPremium) return true;

            // Also check if player has unlocked via progression (track N-1 completed)
            if (track.trackIndex > 0)
            {
                // Need to have completed the previous track
                string prevTrackId = $"track_{track.trackIndex:D2}";  // rough ID pattern
                return profile.IsTrackCompleted(track.trackIndex - 1);
            }
            return false;
        }

        public int GetBestSwipesForTrack(string trackId)
        {
            return profile?.GetBestSwipes(trackId) ?? 0;
        }

        public long GetBestTimeForTrack(string trackId)
        {
            return profile?.GetBestTimeMs(trackId) ?? 0;
        }
    }
}
