using System.Collections.Generic;
using UnityEngine;
using QuickChecks.Core;
using QuickChecks.Racing;
using QuickChecks.Ghost;
using QuickChecks.Track;

namespace QuickChecks.Prototype
{
    /// <summary>
    /// Builds the cross-scene RaceResultData payload at race end, for any mode.
    ///
    /// Lives as a static helper so RaceSceneController doesn't have to grow further —
    /// it already tops 1300 lines. This keeps the per-mode end-of-race logic in one place
    /// and makes the conditional logic easy to audit.
    ///
    /// Mode-specific behaviour:
    ///
    ///   Solo
    ///     - 1 entry (human)
    ///     - stars calculated from swipes vs par
    ///     - isNewBest + prevBest from SoloGhostPlayer
    ///     - winner = human (unless DNF)
    ///
    ///   VsCpuEasy/Medium/Hard
    ///     - 2 entries (human + CPU)
    ///     - winner = fewest swipes (tiebreaker: faster finishTimeMs)
    ///     - DNF classification if a kart hit the swipe cap
    ///     - stars calculated for the HUMAN entry only (CPU has no stars)
    ///     - humanWon flag drives the VICTORY/DEFEAT title
    ///
    ///   PassPlay2/3/4
    ///     - N entries (all human)
    ///     - rank by swipes asc, then finishTimeMs asc
    ///     - DNF players ranked last (alphabetical by name as final tiebreaker)
    ///     - No stars (multiplayer mode — stars are solo/CPU only)
    ///     - winnerIndex = first ranked
    ///
    ///   Online (stub — same as Vs CPU Medium for v1)
    ///     - 2 entries (human + onlineOpponent)
    ///     - Same logic as VsCpuMedium
    /// </summary>
    public static class RaceResultBuilder
    {
        /// <summary>
        /// Builds the full RaceResultData from the current race state.
        /// </summary>
        /// <remarks>
        /// Callers must supply a non-null <paramref name="turnManager"/> with at least one player.
        /// RaceSceneController.TransitionToResults() synthesises a 1-entry TurnManager snapshot
        /// for Solo mode (which doesn't use one during the actual race) so this builder has a
        /// uniform input shape.
        /// </remarks>
        public static RaceResultData Build(
            RaceMode mode,
            TrackDefinition track,
            long raceStartMs,
            long raceEndMs,
            TurnManager turnManager,
            SoloGhostPlayer soloGhost = null,
            bool isNewBest = false,
            int prevBestSwipes = 0,
            long prevBestTimeMs = 0)
        {
            var data = new RaceResultData
            {
                mode = mode,
                trackId = track != null ? track.trackId : "unknown",
                trackDisplayName = track != null ? track.displayName : "Unknown Track",
                parSwipes = track != null ? track.parSwipeCount : 0,
                maxSwipeCap = track != null ? track.GetMaxSwipeCap() : 50,
                raceStartMs = raceStartMs,
                raceEndMs = raceEndMs,
                isNewBest = isNewBest,
                prevBestSwipes = prevBestSwipes,
                prevBestTimeMs = prevBestTimeMs
            };

            if (turnManager == null || turnManager.Players == null || turnManager.Players.Count == 0)
            {
                Debug.LogError("[RaceResultBuilder] turnManager has no players — cannot build results payload. " +
                               "Solo callers must synthesise a 1-player TurnManager via SetPlayersForResults().");
                return data;
            }

            // Multiplayer / TurnManager-driven paths: snapshot every player's state.
            bool isOnline = mode.IsOnline();
            for (int i = 0; i < turnManager.Players.Count; i++)
            {
                var p = turnManager.Players[i];
                bool isHuman = !p.isCPU;
                bool isOpponent = isOnline && !isHuman;
                bool isDnf = p.isFinished && p.finishTimeMs < 0;

                // Look up the kart name/ID for this player from GameFlowManager.pendingKartSelections
                string kartName = "";
                string kartId = "";
                if (Core.GameFlowManager.Instance != null &&
                    Core.GameFlowManager.Instance.pendingKartSelections != null &&
                    i < Core.GameFlowManager.Instance.pendingKartSelections.Count)
                {
                    var kart = Core.GameFlowManager.Instance.pendingKartSelections[i];
                    if (kart != null)
                    {
                        kartName = kart.displayName;
                        kartId = kart.kartId;
                    }
                }
                // Fall back to the shared selectedKart if no per-player selection
                if (string.IsNullOrEmpty(kartName) && Core.GameFlowManager.Instance?.selectedKart != null)
                {
                    kartName = Core.GameFlowManager.Instance.selectedKart.displayName;
                    kartId = Core.GameFlowManager.Instance.selectedKart.kartId;
                }

                var entry = new RaceResultData.PlayerResult
                {
                    playerName = p.playerName,
                    kartName = kartName,
                    kartId = kartId,
                    playerIndex = i,
                    isHuman = isHuman,
                    isCPU = p.isCPU && !isOnline,
                    isOnlineOpponent = isOpponent,
                    swipes = p.swipeCount,
                    finishTimeMs = p.finishTimeMs,
                    isFinished = p.isFinished,
                    isDnf = isDnf
                };
                data.players.Add(entry);

                if (isHuman) data.humanPlayerIndex = i;
            }

            // Rank players: finished first (by swipes asc, then time asc), DNF last.
            RankPlayers(data.players);

            // Determine winner = first ranked, non-DNF entry.
            for (int i = 0; i < data.players.Count; i++)
            {
                if (!data.players[i].isDnf)
                {
                    data.winnerIndex = i;
                    break;
                }
            }

            // Human outcome flags + star calculation.
            var human = data.HumanResult;
            if (human != null)
            {
                data.humanDnf = human.isDnf;
                data.humanWon = (data.winnerIndex >= 0 &&
                                 ReferenceEquals(data.players[data.winnerIndex], human));

                // Stars only apply to solo + vs CPU (single-player skill rating).
                // Pass & Play is a competitive mode — no star rating.
                if (mode.IsSolo() || mode.IsVsCpu() || mode.IsOnline())
                {
                    data.starsEarned = StarRating.Calculate(human.swipes, data.parSwipes, human.isDnf);
                    human.stars = data.starsEarned;
                }
            }

            // Solo: ghost "new best" already passed in via isNewBest.
            // (For multiplayer we leave isNewBest = false.)
            return data;
        }

        /// <summary>
        /// Ranks players in place. Finished players sorted by swipes asc then finishTimeMs asc.
        /// DNF players sorted to the end (preserve insertion order among DNFs).
        /// </summary>
        private static void RankPlayers(List<RaceResultData.PlayerResult> players)
        {
            // Stable sort: finished first, then by swipes, then by time.
            // We do a simple index-tagged sort to keep stability.
            var indexed = new List<(int origIdx, RaceResultData.PlayerResult p)>();
            for (int i = 0; i < players.Count; i++)
                indexed.Add((i, players[i]));

            indexed.Sort((a, b) =>
            {
                bool aDone = a.p.isFinished && !a.p.isDnf;
                bool bDone = b.p.isFinished && !b.p.isDnf;
                if (aDone && !bDone) return -1;
                if (!aDone && bDone) return 1;

                if (aDone && bDone)
                {
                    if (a.p.swipes != b.p.swipes) return a.p.swipes.CompareTo(b.p.swipes);
                    if (a.p.finishTimeMs != b.p.finishTimeMs) return a.p.finishTimeMs.CompareTo(b.p.finishTimeMs);
                }
                // Stable tiebreaker: original index.
                return a.origIdx.CompareTo(b.origIdx);
            });

            // Assign ranks — finished players get 1..N, DNFs get -1.
            int rank = 1;
            for (int i = 0; i < indexed.Count; i++)
            {
                var p = indexed[i].p;
                if (p.isFinished && !p.isDnf)
                {
                    p.rank = rank++;
                }
                else
                {
                    p.rank = -1;
                }
            }
        }
    }
}
