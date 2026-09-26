using System.Threading.Tasks;
using UnityEngine;

namespace QuickChecks.Multiplayer
{
    /// <summary>
    /// Stub for Supabase REST client. Real implementation in week 9 will use
    /// UnityWebRequest with Supabase's REST API. For now, returns mock data
    /// so UI/race flow can be built without backend.
    /// </summary>
    public class SupabaseClient : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string supabaseUrl = "https://YOUR-PROJECT.supabase.co";
        [SerializeField] private string anonKey = "YOUR-ANON-KEY";

        public static SupabaseClient Instance { get; private set; }

        private string _authToken;
        private string _playerId;

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

        public async Task<bool> SignInAnonymouslyAsync()
        {
            // TODO: POST /auth/v1/signup with anonymous payload.
            // For now, mock:
            await Task.Delay(100);
            _authToken = "mock-token";
            _playerId = System.Guid.NewGuid().ToString();
            return true;
        }

        public async Task<string> UploadGhostAsync(string ghostJson)
        {
            // TODO: PUT to /storage/v1/object/ghosts/{playerId}_{trackId}_{timestamp}.json
            await Task.Delay(100);
            return $"https://mock.supabase.co/storage/v1/object/ghosts/{_playerId}_{System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.json";
        }

        public async Task<string> FetchTopGhostsAsync(string trackId, int count)
        {
            // TODO: GET /rest/v1/rpc/get_top_ghosts?track_id={trackId}&n={count}
            await Task.Delay(100);
            return "[{\"player_id\":\"mock-1\",\"finish_time_ms\":42000,\"ghost_url\":\"mock-url\"}]";
        }

        public string PlayerId => _playerId;
        public bool IsSignedIn => !string.IsNullOrEmpty(_authToken);
    }
}
