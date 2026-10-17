using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace QuickChecks.Track
{
    /// <summary>
    /// Persists CustomTrackData to PlayerPrefs as a JSON array. Each player's custom tracks
    /// are stored under one key, so saving a track = load list, add/replace, save list.
    ///
    /// For v1.1: also push to Supabase so custom tracks can be shared / synced across devices.
    /// For v1: local-only is fine (single device, no sharing).
    /// </summary>
    public static class CustomTrackLibrary
    {
        private const string PREFS_KEY = "QuickChecks_CustomTracks";

        /// <summary>Loads all saved custom tracks, ordered by most recently updated.</summary>
        public static List<CustomTrackData> LoadAll()
        {
            string json = PlayerPrefs.GetString(PREFS_KEY, "");
            if (string.IsNullOrEmpty(json)) return new List<CustomTrackData>();

            try
            {
                var list = JsonConvert.DeserializeObject<List<CustomTrackData>>(json);
                if (list == null) return new List<CustomTrackData>();
                list.Sort((a, b) => b.updatedAtMs.CompareTo(a.updatedAtMs));
                return list;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CustomTrackLibrary] Failed to load: {ex.Message}");
                return new List<CustomTrackData>();
            }
        }

        /// <summary>Saves (or replaces if same trackId) a custom track.</summary>
        public static void Save(CustomTrackData track)
        {
            if (track == null) return;
            if (string.IsNullOrEmpty(track.trackId))
                track.trackId = $"custom_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            track.updatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (track.createdAtMs == 0)
                track.createdAtMs = track.updatedAtMs;

            var all = LoadAll();
            int existingIdx = all.FindIndex(t => t.trackId == track.trackId);
            if (existingIdx >= 0) all[existingIdx] = track;
            else all.Add(track);

            SaveAll(all);
            Debug.Log($"[CustomTrackLibrary] Saved '{track.trackName}' ({all.Count} total).");
        }

        /// <summary>Deletes a custom track by ID.</summary>
        public static bool Delete(string trackId)
        {
            var all = LoadAll();
            int removed = all.RemoveAll(t => t.trackId == trackId);
            if (removed > 0)
            {
                SaveAll(all);
                Debug.Log($"[CustomTrackLibrary] Deleted track {trackId} ({removed}).");
            }
            return removed > 0;
        }

        /// <summary>Returns the track with the given ID, or null.</summary>
        public static CustomTrackData FindById(string trackId)
        {
            if (string.IsNullOrEmpty(trackId)) return null;
            var all = LoadAll();
            return all.Find(t => t.trackId == trackId);
        }

        private static void SaveAll(List<CustomTrackData> tracks)
        {
            string json = JsonConvert.SerializeObject(tracks);
            PlayerPrefs.SetString(PREFS_KEY, json);
            PlayerPrefs.Save();
        }

        /// <summary>Converts a CustomTrackData into a TrackDefinition that the race scene can build.</summary>
        public static TrackDefinition ToTrackDefinition(CustomTrackData custom)
        {
            if (custom == null) return null;
            var def = ScriptableObject.CreateInstance<TrackDefinition>();
            def.trackId = custom.trackId;
            def.displayName = custom.trackName;
            def.difficultyStars = 3;  // custom tracks default to ★★★ medium
            def.isClosedLoop = custom.isClosedLoop;
            def.isFree = true;        // custom tracks are always playable (player made them)
            def.trackIndex = -1;      // not in the linear progression
            def.trackWidth = custom.trackWidth;
            def.lapCount = 1;
            def.defaultZoom = custom.defaultZoom;
            def.parSwipeCount = custom.parSwipeCount;
            def.maxSwipeCap = custom.maxSwipeCap;
            def.splinePoints = custom.GetSplinePointsAsVector2();

            // Convert obstacles
            var obsList = new List<TrackDefinition.ObstacleSpawn>();
            if (custom.obstacles != null)
            {
                foreach (var o in custom.obstacles)
                {
                    var type = o.type switch
                    {
                        "Wall" => TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                        "Gravel" => TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                        "BlackHole" => TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                        _ => TrackDefinition.ObstacleSpawn.ObstacleType.Wall
                    };
                    obsList.Add(new TrackDefinition.ObstacleSpawn
                    {
                        type = type,
                        t = o.t,
                        lateralOffset = o.lateralOffset,
                        size = o.size.ToVector2(),
                        teleportDistance = type == TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole ? 8f : 0f
                    });
                }
            }
            def.obstacleSpawns = obsList.ToArray();

            // Convert power-ups
            var puList = new List<TrackDefinition.PowerUpSpawn>();
            if (custom.powerUps != null)
            {
                foreach (var p in custom.powerUps)
                {
                    puList.Add(new TrackDefinition.PowerUpSpawn
                    {
                        t = p.t,
                        lateralOffset = p.lateralOffset,
                        powerUpId = p.type
                    });
                }
            }
            def.powerUpSpawns = puList.ToArray();

            return def;
        }
    }
}
