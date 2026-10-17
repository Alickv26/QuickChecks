using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace QuickChecks.Track
{
    /// <summary>
    /// Player-authored custom track. Serialized to JSON + stored in PlayerPrefs (and optionally
    /// uploaded to Supabase for sharing in a future iteration).
    ///
    /// Structure mirrors TrackDefinition but is JSON-friendly (uses List instead of array,
    /// plain Vector2 for positions, no ScriptableObject baggage).
    /// </summary>
    [Serializable]
    [JsonObject]
    public class CustomTrackData
    {
        [JsonProperty] public string trackId;          // "custom_<timestamp>"
        [JsonProperty] public string trackName;        // user-entered
        [JsonProperty] public string authorId;         // Supabase player UUID
        [JsonProperty] public string authorName;        // display name at time of save
        [JsonProperty] public long createdAtMs;        // Unix ms
        [JsonProperty] public long updatedAtMs;
        [JsonProperty] public List<SerializableVector2> splinePoints = new();
        [JsonProperty] public bool isClosedLoop = true;
        [JsonProperty] public float trackWidth = 6f;
        [JsonProperty] public float defaultZoom = 16f;
        [JsonProperty] public int parSwipeCount = 10;  // auto-calculated on save
        [JsonProperty] public int maxSwipeCap = 60;    // 30 + 3*10 (medium difficulty)
        [JsonProperty] public List<PlacedObstacle> obstacles = new();
        [JsonProperty] public List<PlacedPowerUp> powerUps = new();

        [Serializable]
        [JsonObject]
        public class SerializableVector2
        {
            [JsonProperty] public float x;
            [JsonProperty] public float y;
            public SerializableVector2() { }
            public SerializableVector2(float x, float y) { this.x = x; this.y = y; }
            public Vector2 ToVector2() => new(x, y);
            public static SerializableVector2 From(Vector2 v) => new(v.x, v.y);
        }

        [Serializable]
        [JsonObject]
        public class PlacedObstacle
        {
            [JsonProperty] public string type;     // "Wall", "Gravel", "BlackHole"
            [JsonProperty] public SerializableVector2 position;
            [JsonProperty] public SerializableVector2 size;       // x=width, y=height
            [JsonProperty] public float rotation;                 // degrees
            [JsonProperty] public float t;                        // spline parameter 0-1 (where on track)
            [JsonProperty] public float lateralOffset;            // distance from centerline
        }

        [Serializable]
        [JsonObject]
        public class PlacedPowerUp
        {
            [JsonProperty] public string type;     // "Boost", "Shield", "Magnet", "Slingshot", etc.
            [JsonProperty] public SerializableVector2 position;
            [JsonProperty] public float t;
            [JsonProperty] public float lateralOffset;
        }

        // --- Convenience helpers ---

        public Vector2[] GetSplinePointsAsVector2()
        {
            var arr = new Vector2[splinePoints?.Count ?? 0];
            if (splinePoints == null) return arr;
            for (int i = 0; i < splinePoints.Count; i++)
                arr[i] = splinePoints[i].ToVector2();
            return arr;
        }

        /// <summary>Recalculates parSwipeCount based on track length + obstacle count.</summary>
        public void RecalculatePar()
        {
            // Rough heuristic: 1 swipe per 2 spline segments + 1 per 2 obstacles
            int points = splinePoints?.Count ?? 0;
            int obstacleCount = obstacles?.Count ?? 0;
            parSwipeCount = Mathf.Max(8, (points / 2) + (obstacleCount / 2) + 4);
            maxSwipeCap = 30 + 3 * 10;  // ★★★ difficulty = 60 swipes
        }
    }
}
