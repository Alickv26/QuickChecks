using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace QuickChecks.Core
{
    /// <summary>
    /// Cross-scene payload that carries full race-end data from Race scene to Results scene.
    ///
    /// Built by RaceSceneController.FinishRace*() and stored on GameFlowManager.lastRaceResults.
    /// Consumed by ResultsScreenController.Start() to render the appropriate layout per mode.
    ///
    /// Modes covered:
    ///   - Solo      : single entry (player), with par + best comparison + ghostUpdated flag
    ///   - VsCpu*    : 2 entries (player + CPU), with win/loss/tie outcome
    ///   - PassPlay* : 2-4 entries (all human), ranked by swipes then time
    ///   - Online    : 2 entries (player + opponent), treated like Vs CPU for now (ELO stub v1.1)
    /// </summary>
    [Serializable]
    [JsonObject]
    public class RaceResultData
    {
        [JsonProperty] public RaceMode mode;
        [JsonProperty] public string trackId;
        [JsonProperty] public string trackDisplayName;
        [JsonProperty] public int parSwipes;
        [JsonProperty] public int maxSwipeCap;
        [JsonProperty] public long raceStartMs;
        [JsonProperty] public long raceEndMs;
        [JsonProperty] public List<PlayerResult> players = new();
        [JsonProperty] public int winnerIndex = -1;          // -1 = DNF/no winner
        [JsonProperty] public int humanPlayerIndex = 0;      // which entry is the local human (0 in solo/CPU, varies in P&P)
        [JsonProperty] public bool humanWon;
        [JsonProperty] public bool humanDnf;
        [JsonProperty] public bool isNewBest;                 // solo: ghost updated
        [JsonProperty] public int prevBestSwipes;             // solo: previous best (for "previous best: N swipes" line)
        [JsonProperty] public long prevBestTimeMs;
        [JsonProperty] public int starsEarned;                // 0-3, solo/CPU only

        /// <summary>Total elapsed race time in ms.</summary>
        public long RaceDurationMs => raceEndMs - raceStartMs;

        [Serializable]
        [JsonObject]
        public class PlayerResult
        {
            [JsonProperty] public string playerName;
            [JsonProperty] public bool isCPU;
            [JsonProperty] public bool isHuman;
            [JsonProperty] public bool isOnlineOpponent;
            [JsonProperty] public int swipes;
            [JsonProperty] public long finishTimeMs;     // -1 = DNF
            [JsonProperty] public bool isFinished;
            [JsonProperty] public bool isDnf;
            [JsonProperty] public int rank;              // 1-based; -1 = DNF/unranked
            [JsonProperty] public int stars;             // 0-3, calculated for human player only

            /// <summary>Display label for the player row (e.g. "1. SwiftFox_42").</summary>
            public string RankLabel => rank > 0 ? $"{rank}." : "—";

            /// <summary>Display string for swipes column.</summary>
            public string SwipesDisplay => isDnf ? "DNF" : swipes.ToString();

            /// <summary>Display string for time column. Unfinished (not DNF, not finished) shows "—".</summary>
            public string TimeDisplay => (isDnf || finishTimeMs < 0 || !isFinished)
                ? "—"
                : $"{finishTimeMs / 1000f:F2}s";
        }

        // --- Convenience helpers ---

        public bool IsSolo => mode.IsSolo();
        public bool IsVsCpu => mode.IsVsCpu();
        public bool IsPassPlay => mode.IsPassPlay();
        public bool IsOnline => mode.IsOnline();
        public bool IsMultiplayer => mode.IsMultiplayer();

        /// <summary>The human player's entry (first isHuman=true).</summary>
        public PlayerResult HumanResult
        {
            get
            {
                if (players == null) return null;
                for (int i = 0; i < players.Count; i++)
                    if (players[i] != null && players[i].isHuman) return players[i];
                return players.Count > 0 ? players[0] : null;
            }
        }

        /// <summary>For Vs CPU / Online: the opponent entry.</summary>
        public PlayerResult OpponentResult
        {
            get
            {
                if (players == null) return null;
                for (int i = 0; i < players.Count; i++)
                    if (players[i] != null && !players[i].isHuman) return players[i];
                return null;
            }
        }
    }
}
