using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace QuickChecks.Core
{
    /// <summary>
    /// Persistent player profile. Stored in PlayerPrefs as JSON.
    /// Tracks:
    ///   - Which tracks are unlocked (by trackIndex)
    ///   - Best swipes + time per track
    ///   - Total races completed
    ///   - Premium status (also mirrored in EntitlementManager)
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        private const string PREFS_KEY = "QuickChecks_PlayerProfile";

        [JsonProperty] public Dictionary<string, TrackResult> trackResults = new();
        [JsonProperty] public int highestUnlockedTrackIndex = 0;
        [JsonProperty] public int totalRacesCompleted = 0;
        [JsonProperty] public bool isPremium = false;
        [JsonProperty] public string displayName = "";
        [JsonProperty] public string playerId = "";

        [Serializable]
        [JsonObject]
        public class TrackResult
        {
            [JsonProperty] public int bestSwipes;
            [JsonProperty] public long bestTimeMs;
            [JsonProperty] public int completions;
            [JsonProperty] public bool completed;
        }

        public static PlayerProfile Load()
        {
            string json = PlayerPrefs.GetString(PREFS_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var profile = JsonConvert.DeserializeObject<PlayerProfile>(json);
                    if (profile != null)
                    {
                        Debug.Log($"[PlayerProfile] Loaded: {profile.trackResults.Count} track results, " +
                                  $"highest unlocked: {profile.highestUnlockedTrackIndex}, " +
                                  $"premium: {profile.isPremium}.");
                        return profile;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayerProfile] Failed to load: {ex.Message}");
                }
            }

            // Create new profile
            var newProfile = new PlayerProfile
            {
                playerId = System.Guid.NewGuid().ToString(),
                displayName = GenerateDisplayName(),
                highestUnlockedTrackIndex = 0,
                isPremium = false
            };
            Debug.Log($"[PlayerProfile] Created new profile: {newProfile.playerId} / {newProfile.displayName}");
            return newProfile;
        }

        public void Save()
        {
            string json = JsonConvert.SerializeObject(this);
            PlayerPrefs.SetString(PREFS_KEY, json);
            PlayerPrefs.Save();
            Debug.Log("[PlayerProfile] Saved.");
        }

        /// <summary>
        /// Records a track result. Returns true if this is a new best (fewer swipes).
        /// Also unlocks the next track if this one was completed under par.
        /// </summary>
        public bool RecordTrackResult(string trackId, int swipes, long timeMs)
        {
            if (trackResults == null) trackResults = new();

            bool isNewBest = false;
            if (trackResults.TryGetValue(trackId, out var existing))
            {
                if (swipes < existing.bestSwipes ||
                    (swipes == existing.bestSwipes && timeMs < existing.bestTimeMs))
                {
                    existing.bestSwipes = swipes;
                    existing.bestTimeMs = timeMs;
                    isNewBest = true;
                }
                existing.completions++;
                existing.completed = true;
                trackResults[trackId] = existing;
            }
            else
            {
                trackResults[trackId] = new TrackResult
                {
                    bestSwipes = swipes,
                    bestTimeMs = timeMs,
                    completions = 1,
                    completed = true
                };
                isNewBest = true;
            }

            totalRacesCompleted++;
            return isNewBest;
        }

        /// <summary>
        /// Unlocks the next track (trackIndex + 1).
        /// Called when a track is completed under par.
        /// </summary>
        public void UnlockNextTrack(int currentTrackIndex)
        {
            int nextIndex = currentTrackIndex + 1;
            if (nextIndex > highestUnlockedTrackIndex)
            {
                highestUnlockedTrackIndex = nextIndex;
                Debug.Log($"[PlayerProfile] Unlocked track index {nextIndex}.");
            }
        }

        public bool IsTrackCompleted(int trackIndex)
        {
            // Check if any track result has been completed for this index
            // Track IDs follow pattern "track_01_first_lap", "track_02_curves_101", etc.
            // We check by index prefix
            if (trackResults == null) return false;
            string prefix = $"track_{trackIndex + 1:D2}";
            foreach (var kvp in trackResults)
            {
                if (kvp.Key.StartsWith(prefix) && kvp.Value.completed)
                {
                    return true;
                }
            }
            return highestUnlockedTrackIndex >= trackIndex;
        }

        public int GetBestSwipes(string trackId)
        {
            if (trackResults != null && trackResults.TryGetValue(trackId, out var result))
            {
                return result.bestSwipes;
            }
            return 0;
        }

        public long GetBestTimeMs(string trackId)
        {
            if (trackResults != null && trackResults.TryGetValue(trackId, out var result))
            {
                return result.bestTimeMs;
            }
            return 0;
        }

        public void SetPremium(bool premium)
        {
            isPremium = premium;
            Save();
        }

        // --- Display name generation (Decision 22: Adjective+Animal_Number) ---

        private static readonly string[] ADJECTIVES = {
            "Swift", "Brave", "Calm", "Bold", "Quick", "Bright", "Eager", "Fierce",
            "Gentle", "Happy", "Jolly", "Keen", "Lively", "Mighty", "Noble", "Proud",
            "Rapid", "Silent", "Steady", "Valiant", "Wise", "Zesty", "Clever", "Daring"
        };

        private static readonly string[] ANIMALS = {
            "Fox", "Tiger", "Panda", "Eagle", "Wolf", "Bear", "Hawk", "Lion",
            "Otter", "Falcon", "Lynx", "Puma", "Seal", "Owl", "Raven", "Stag",
            "Viper", "Cobra", "Jaguar", "Panther", "Heron", "Kraken", "Mustang", "Phantom"
        };

        private static string GenerateDisplayName()
        {
            string adj = ADJECTIVES[UnityEngine.Random.Range(0, ADJECTIVES.Length)];
            string animal = ANIMALS[UnityEngine.Random.Range(0, ANIMALS.Length)];
            int number = UnityEngine.Random.Range(10, 100);
            return $"{adj}{animal}_{number}";
        }
    }
}
