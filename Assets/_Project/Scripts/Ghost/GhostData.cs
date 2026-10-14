using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuickChecks.Ghost
{
    /// <summary>
    /// JSON-serializable recording of a finished race.
    ///
    /// Three recording systems:
    /// 1. Swipe events — for leaderboard ranking (swipe count)
    /// 2. Position samples — continuous path recording (for visualization)
    /// 3. Stop positions — where the kart stopped after each swipe (for ghost replay)
    ///
    /// The STOP POSITIONS are the authoritative ghost replay method.
    /// Each stop position = one "move" in the turn-taking sequence.
    /// Ghost advances to the next stop position when the player stops,
    /// creating a true 1:1 swipe correspondence.
    /// </summary>
    [Serializable]
    [JsonObject]
    public class GhostData
    {
        [JsonProperty] public string trackId;
        [JsonProperty] public string kartId;

        [JsonProperty] public int swipeCount;
        [JsonProperty] public long finishTimeMs;
        [JsonProperty] public string mode;

        [JsonProperty] public List<SwipeEventEntry> swipeEvents = new();
        [JsonProperty] public List<PositionSample> positionSamples = new();

        /// <summary>
        /// Positions where the kart STOPPED after each swipe.
        /// Index 0 = starting position (before first swipe).
        /// Index 1 = where kart stopped after swipe 1.
        /// Index N = where kart stopped after swipe N.
        /// The ghost advances through these one at a time during turn-taking.
        /// </summary>
        [JsonProperty] public List<StopPosition> stopPositions = new();

        [JsonProperty] public List<string> powerUpsCollected = new();

        [Serializable]
        [JsonObject]
        public struct SwipeEventEntry
        {
            [JsonProperty] public long t;
            [JsonProperty] public float dx;
            [JsonProperty] public float dy;
            [JsonProperty] public float mag;
        }

        [Serializable]
        [JsonObject]
        public struct PositionSample
        {
            [JsonProperty] public long t;
            [JsonProperty] public float x;
            [JsonProperty] public float y;
            [JsonProperty] public float rot;
        }

        /// <summary>
        /// A position where the kart stopped after a swipe.
        /// The ghost replays by advancing through these one at a time.
        /// </summary>
        [Serializable]
        [JsonObject]
        public struct StopPosition
        {
            [JsonProperty] public float x;
            [JsonProperty] public float y;
            [JsonProperty] public float rot;
            [JsonProperty] public int swipeIndex;  // which swipe this stop corresponds to (0-based)
        }
    }
}
