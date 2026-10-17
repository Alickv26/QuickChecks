using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace QuickChecks.Multiplayer
{
    /// <summary>
    /// High-level leaderboard API for the QuickChecks UI.
    ///
    /// Wraps SupabaseRpcClient with friendly data shapes. Used by:
    ///   - RaceSceneController.FinishRace() to upload new results
    ///   - LeaderboardPanel UI to render top-10 + player rank
    ///
    /// Endpoints used:
    ///   POST /rest/v1/race_results      — insert a finished race row
    ///   POST /rest/v1/rpc/get_leaderboard — fetch top-N + requesting player's row
    ///   POST /storage/v1/object/ghosts/{playerId}/{filename} — upload ghost JSON
    /// </summary>
    public class SupabaseLeaderboard : MonoBehaviour
    {
        public static SupabaseLeaderboard Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // =====================================================================
        // Upload (called after Solo race finish)
        // =====================================================================

        /// <summary>
        /// Uploads a finished Solo race result to the leaderboard.
        /// Fire-and-forget — the caller doesn't need to wait (the player is already
        /// on the Results screen). Failures are logged but don't block the UI.
        /// </summary>
        public void UploadRaceResult(string trackId, string kartId, int swipeCount,
            long finishTimeMs, bool isDnf, string ghostJson = null)
        {
            if (!SupabaseRpcClient.Instance.IsAuthenticated)
            {
                Debug.LogWarning("[SupabaseLeaderboard] Skipping upload — not authenticated.");
                return;
            }

            StartCoroutine(UploadRaceResultCoroutine(trackId, kartId, swipeCount,
                finishTimeMs, isDnf, ghostJson));
        }

        private IEnumerator UploadRaceResultCoroutine(string trackId, string kartId,
            int swipeCount, long finishTimeMs, bool isDnf, string ghostJson)
        {
            string ghostUrl = null;

            // Step 1: if a ghost JSON is provided, upload it to storage first.
            // We only upload ghosts on new bests (caller decides), per Decision 23.
            if (!string.IsNullOrEmpty(ghostJson))
            {
                string filename = $"{trackId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.json";
                bool ghostOk = false;
                string uploadedUrl = null;
                yield return SupabaseRpcClient.Instance.UploadGhostAsync(filename, ghostJson,
                    (ok, url) =>
                    {
                        ghostOk = ok;
                        uploadedUrl = url;
                    });

                if (ghostOk) ghostUrl = uploadedUrl;
                // Continue even if ghost upload failed — the race result row is still valuable.
            }

            // Step 2: insert the race_results row.
            var row = new
            {
                player_id = SupabaseRpcClient.Instance.UserId,
                track_id = trackId,
                kart_id = kartId,
                swipe_count = swipeCount,
                finish_time_ms = finishTimeMs,
                is_dnf = isDnf,
                ghost_url = ghostUrl,
                completed_at = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
            string body = JsonConvert.SerializeObject(row);

            bool insertOk = false;
            yield return SupabaseRpcClient.Instance.PostAsync("race_results", body,
                (ok, _) => insertOk = ok);

            if (insertOk)
            {
                Debug.Log($"[SupabaseLeaderboard] Uploaded race result: track={trackId}, " +
                          $"swipes={swipeCount}, time={finishTimeMs / 1000f:F2}s, dnf={isDnf}");
            }
        }

        // =====================================================================
        // Fetch (called by LeaderboardPanel UI)
        // =====================================================================

        /// <summary>
        /// Fetches the top-10 leaderboard for a track. The result INCLUDES the
        /// requesting player's row (flagged with is_requester=true) even if they
        /// aren't in the top-10 — so the UI can show "Your rank: 47th" below.
        /// </summary>
        public IEnumerator FetchLeaderboardAsync(string trackId, int topN,
            Action<bool, List<LeaderboardEntry>, int> onComplete)
        {
            var body = JsonConvert.SerializeObject(new
            {
                p_track_id = trackId,
                p_limit = topN
            });

            bool ok = false;
            string responseJson = null;
            yield return SupabaseRpcClient.Instance.CallRpcAsync("get_leaderboard", body,
                (success, json) =>
                {
                    ok = success;
                    responseJson = json;
                });

            if (!ok)
            {
                onComplete?.Invoke(false, null, -1);
                yield break;
            }

            // Parse the RPC response — array of {rank, player_id, display_name, swipe_count,
            // finish_time_ms, completed_at, is_requester}
            var entries = new List<LeaderboardEntry>();
            int playerRank = -1;
            try
            {
                var arr = Newtonsoft.Json.Linq.JArray.Parse(responseJson);
                foreach (var item in arr)
                {
                    var entry = new LeaderboardEntry
                    {
                        rank = item["rank"]?.ToObject<int>() ?? 0,
                        playerId = item["player_id"]?.ToString() ?? "",
                        displayName = item["display_name"]?.ToString() ?? "Player",
                        swipeCount = item["swipe_count"]?.ToObject<int>() ?? 0,
                        finishTimeMs = item["finish_time_ms"]?.ToObject<long>() ?? 0,
                        completedAt = item["completed_at"]?.ToString() ?? "",
                        isRequester = item["is_requester"]?.ToObject<bool>() ?? false
                    };
                    entries.Add(entry);
                    if (entry.isRequester) playerRank = entry.rank;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SupabaseLeaderboard] Parse failed: {ex.Message}\n{responseJson}");
                onComplete?.Invoke(false, null, -1);
                yield break;
            }

            // If the player wasn't in the top-N, fetch their rank separately.
            if (playerRank < 0 && SupabaseRpcClient.Instance.IsAuthenticated)
            {
                bool rankOk = false;
                string rankJson = null;
                var rankBody = JsonConvert.SerializeObject(new { p_track_id = trackId });
                yield return SupabaseRpcClient.Instance.CallRpcAsync("get_player_rank", rankBody,
                    (success, json) =>
                    {
                        rankOk = success;
                        rankJson = json;
                    });

                if (rankOk)
                {
                    try
                    {
                        var rankArr = Newtonsoft.Json.Linq.JArray.Parse(rankJson);
                        if (rankArr.Count > 0)
                        {
                            playerRank = rankArr[0]["rank"]?.ToObject<int>() ?? -1;
                            // Also append the player's row to the entries list (so the UI can show it).
                            entries.Add(new LeaderboardEntry
                            {
                                rank = playerRank,
                                playerId = SupabaseRpcClient.Instance.UserId,
                                displayName = SupabaseAuth.Instance?.DisplayName ?? "You",
                                swipeCount = rankArr[0]["swipe_count"]?.ToObject<int>() ?? 0,
                                finishTimeMs = rankArr[0]["finish_time_ms"]?.ToObject<long>() ?? 0,
                                completedAt = "",
                                isRequester = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SupabaseLeaderboard] Failed to parse player rank: {ex.Message}");
                    }
                }
            }

            onComplete?.Invoke(true, entries, playerRank);
        }

        // =====================================================================
        // DTO
        // =====================================================================

        [Serializable]
        public class LeaderboardEntry
        {
            public int rank;
            public string playerId;
            public string displayName;
            public int swipeCount;
            public long finishTimeMs;
            public string completedAt;
            public bool isRequester;

            public string TimeDisplay => $"{finishTimeMs / 1000f:F2}s";
        }
    }
}
