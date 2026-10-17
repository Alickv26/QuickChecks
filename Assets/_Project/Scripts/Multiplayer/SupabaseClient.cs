using System.Threading.Tasks;
using UnityEngine;

namespace QuickChecks.Multiplayer
{
    /// <summary>
    /// Backward-compat shim for code that still references SupabaseClient. The
    /// real work now lives in SupabaseRpcClient + SupabaseAuth + SupabaseLeaderboard.
    ///
    /// This class is responsible for:
    ///   1. Spawning the three singletons on first use (so scenes that don't
    ///      explicitly include them still get the backend).
    ///   2. Exposing the project URL + publishable key as a config singleton.
    /// </summary>
    public class SupabaseClient : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string supabaseUrl = "https://wjeiddjeueysfsvjgzyx.supabase.co";
        [SerializeField] private string publishableKey = "sb_publishable_SJKpvEFlsgRSKniELN5V-Q_pyL5YDVa";

        public static SupabaseClient Instance { get; private set; }

        public string SupabaseUrl => supabaseUrl;
        public string PublishableKey => publishableKey;

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

            // Spawn the three real singletons if they don't already exist.
            if (FindObjectOfType<SupabaseRpcClient>() == null)
            {
                var go = new GameObject("[SupabaseRpcClient]");
                var rpc = go.AddComponent<SupabaseRpcClient>();
                // Wire the config via reflection (the fields are private serialized).
                var urlField = typeof(SupabaseRpcClient).GetField("supabaseUrl",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var keyField = typeof(SupabaseRpcClient).GetField("publishableKey",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                urlField?.SetValue(rpc, supabaseUrl);
                keyField?.SetValue(rpc, publishableKey);
                DontDestroyOnLoad(go);
            }
            if (FindObjectOfType<SupabaseAuth>() == null)
            {
                var go = new GameObject("[SupabaseAuth]");
                go.AddComponent<SupabaseAuth>();
                DontDestroyOnLoad(go);
            }
            if (FindObjectOfType<SupabaseLeaderboard>() == null)
            {
                var go = new GameObject("[SupabaseLeaderboard]");
                go.AddComponent<SupabaseLeaderboard>();
                DontDestroyOnLoad(go);
            }
        }

        // --- Legacy stub API (kept so existing code doesn't break) ---

        public async Task<bool> SignInAnonymouslyAsync()
        {
            bool ok = false;
            await System.Threading.Tasks.Task.Run(() =>
            {
                // The real sign-in is a coroutine — kick it off and wait.
                bool done = false;
                Instance.StartCoroutine(SupabaseAuth.Instance.SignInAndLoadProfile(success =>
                {
                    ok = success;
                    done = true;
                }));
                // Spin until done. (We're on a background Task, but the coroutine runs
                // on the main thread via Instance.StartCoroutine. This is acceptable for
                // the legacy bridge — new code should use the coroutine directly.)
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (!done && sw.Elapsed < TimeSpan.FromSeconds(10))
                {
                    System.Threading.Thread.Sleep(50);
                }
            });
            return ok;
        }

        public string PlayerId => SupabaseRpcClient.Instance?.UserId;
        public bool IsSignedIn => SupabaseRpcClient.Instance != null && SupabaseRpcClient.Instance.IsAuthenticated;
    }
}
