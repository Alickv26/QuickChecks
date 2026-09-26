using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace QuickChecks.Multiplayer
{
    /// <summary>
    /// Orchestrates ghost loading before a race starts. Given a trackId,
    /// fetches top-N global ghosts from Supabase, downloads their JSON,
    /// and exposes them to the race scene for GhostPlayer instantiation.
    /// </summary>
    public class AsyncMultiplayerManager : MonoBehaviour
    {
        [SerializeField] private int maxGhostsPerRace = 3;

        private List<Ghost.GhostData> _loadedGhosts = new();
        public IReadOnlyList<Ghost.GhostData> LoadedGhosts => _loadedGhosts;

        public async Task LoadGhostsForTrackAsync(string trackId)
        {
            _loadedGhosts.Clear();

            if (SupabaseClient.Instance == null || !SupabaseClient.Instance.IsSignedIn)
            {
                Debug.LogWarning("[AsyncMP] Not signed in — racing without ghosts.");
                return;
            }

            // Fetch top ghost metadata.
            string metaJson = await SupabaseClient.Instance.FetchTopGhostsAsync(trackId, maxGhostsPerRace);
            // TODO: parse metaJson into list of {player_id, finish_time_ms, ghost_url}.

            // For each ghost URL, fetch the ghost JSON, deserialize, add to list.
            // For now, mock:
            _loadedGhosts.Add(new Ghost.GhostData
            {
                trackId = trackId,
                kartId = "kart_viper",
                finishTimeMs = 42000,
                swipeEvents = new List<Ghost.GhostData.SwipeEventEntry>()
            });
        }

        public async Task<bool> SubmitRaceResultAsync(Ghost.GhostData ghost)
        {
            if (SupabaseClient.Instance == null) return false;
            string json = JsonUtility.ToJson(ghost); // NOTE: JsonUtility doesn't handle List<struct> well; replace with Newtonsoft in week 9.
            string url = await SupabaseClient.Instance.UploadGhostAsync(json);
            return !string.IsNullOrEmpty(url);
        }
    }
}
