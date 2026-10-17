using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace QuickChecks.Multiplayer
{
    /// <summary>
    /// Low-level Supabase REST wrapper. Handles auth + raw HTTP calls to Supabase's
    /// PostgREST + Storage endpoints. Used by SupabaseAuth + SupabaseLeaderboard.
    ///
    /// Endpoints:
    ///   POST /auth/v1/signup                   — anonymous signup, returns access_token
    ///   GET  /rest/v1/{table}                 — query a table
    ///   POST /rest/v1/{table}                 — insert into a table
    ///   POST /rest/v1/rpc/{function_name}     — call a stored function
    ///   POST /storage/v1/object/{bucket}/{path} — upload to storage bucket
    ///
    /// All requests carry:
    ///   apikey: <publishable_key>           — required by all Supabase endpoints
    ///   Authorization: Bearer <access_token> — required for authenticated calls
    ///
    /// The publishable key is the new sb_publishable_... format (replaces the
    /// legacy anon JWT). It's safe to ship in client code — RLS protects the data.
    /// </summary>
    public class SupabaseRpcClient : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string supabaseUrl = "https://wjeiddjeueysfsvjgzyx.supabase.co";
        [SerializeField] private string publishableKey = "sb_publishable_SJKpvEFlsgRSKniELN5V-Q_pyL5YDVa";

        public static SupabaseRpcClient Instance { get; private set; }

        private string _accessToken;
        private string _refreshToken;
        private string _userId;          // UUID of the auth.users row
        private DateTime _tokenExpiry;

        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.UtcNow;
        public string UserId => _userId;
        public string AccessToken => _accessToken;

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

        // ============================================================================
        // Auth
        // ============================================================================

        /// <summary>
        /// Signs in anonymously. The first call creates a new auth.users row + a
        /// corresponding public.players row (via the handle_new_player trigger).
        /// Subsequent calls return the same user if localStorage cached the refresh token.
        /// </summary>
        public IEnumerator SignInAnonymouslyAsync(Action<bool, string> onComplete = null)
        {
            // Try cached refresh token first (so the same player gets the same UUID across sessions).
            string cachedRefresh = PlayerPrefs.GetString("QC_Supabase_RefreshToken", "");
            string cachedUserId   = PlayerPrefs.GetString("QC_Supabase_UserId", "");

            if (!string.IsNullOrEmpty(cachedRefresh))
            {
                Debug.Log("[SupabaseRpcClient] Trying cached refresh token...");
                yield return RefreshTokenCoroutine(cachedRefresh,
                    (ok, access, refresh, userId, expires) =>
                    {
                        if (ok)
                        {
                            _accessToken = access;
                            _refreshToken = refresh;
                            _userId = !string.IsNullOrEmpty(userId) ? userId : cachedUserId;
                            _tokenExpiry = expires;
                            PlayerPrefs.SetString("QC_Supabase_RefreshToken", _refreshToken);
                            PlayerPrefs.SetString("QC_Supabase_UserId", _userId);
                            PlayerPrefs.Save();
                            Debug.Log($"[SupabaseRpcClient] Refreshed session. UserId={_userId}");
                            onComplete?.Invoke(true, _userId);
                        }
                        else
                        {
                            // Refresh failed — fall through to fresh signup below.
                            // We can't yield from inside a callback, so we manually restart.
                            Instance.StartCoroutine(FreshSignupCoroutine(onComplete));
                        }
                    });
                yield break;
            }

            yield return FreshSignupCoroutine(onComplete);
        }

        private IEnumerator FreshSignupCoroutine(Action<bool, string> onComplete)
        {
            string url = $"{supabaseUrl}/auth/v1/signup";
            string body = "{\"email\":\"\",\"password\":\"\"}";  // empty creds → anonymous signup
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", publishableKey);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var json = req.downloadHandler.text;
                ParseAuthResponse(json, out _accessToken, out _refreshToken, out _userId, out _tokenExpiry);

                if (!string.IsNullOrEmpty(_refreshToken))
                {
                    PlayerPrefs.SetString("QC_Supabase_RefreshToken", _refreshToken);
                    PlayerPrefs.SetString("QC_Supabase_UserId", _userId);
                    PlayerPrefs.Save();
                    Debug.Log($"[SupabaseRpcClient] Anonymous signup OK. UserId={_userId}");
                    onComplete?.Invoke(true, _userId);
                }
                else
                {
                    Debug.LogError($"[SupabaseRpcClient] Signup response missing tokens: {json}");
                    onComplete?.Invoke(false, null);
                }
            }
            else
            {
                Debug.LogError($"[SupabaseRpcClient] Anonymous signup failed: {req.error} — {req.downloadHandler?.text}");
                onComplete?.Invoke(false, null);
            }
        }

        private IEnumerator RefreshTokenCoroutine(string refreshToken,
            Action<bool, string, string, string, DateTime> onComplete)
        {
            string url = $"{supabaseUrl}/auth/v1/token?grant_type=refresh_token";
            string body = $"{{\"refresh_token\":\"{refreshToken}\"}}";
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", publishableKey);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var json = req.downloadHandler.text;
                ParseAuthResponse(json, out string access, out string refresh, out string userId, out DateTime expires);
                onComplete?.Invoke(true, access, refresh, userId, expires);
            }
            else
            {
                Debug.LogWarning($"[SupabaseRpcClient] Refresh failed: {req.error}");
                onComplete?.Invoke(false, null, null, null, DateTime.MinValue);
            }
        }

        /// <summary>
        /// Parses a Supabase auth response (signup or refresh) into access/refresh tokens,
        /// user ID, and expiry timestamp.
        /// </summary>
        private void ParseAuthResponse(string json,
            out string accessToken, out string refreshToken, out string userId, out DateTime expiry)
        {
            accessToken = ""; refreshToken = ""; userId = ""; expiry = DateTime.UtcNow + TimeSpan.FromHours(1);
            try
            {
                var root = Newtonsoft.Json.Linq.JObject.Parse(json);
                accessToken  = root["access_token"]?.ToString() ?? "";
                refreshToken = root["refresh_token"]?.ToString() ?? "";
                userId       = root["user"]?["id"]?.ToString() ?? "";
                int expiresIn = root["expires_in"]?.ToObject<int>() ?? 3600;
                expiry = DateTime.UtcNow + TimeSpan.FromSeconds(expiresIn);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SupabaseRpcClient] Failed to parse auth response: {ex.Message}\n{json}");
            }
        }

        // ============================================================================
        // REST queries (PostgREST)
        // ============================================================================

        /// <summary>GET /rest/v1/{table}?{queryParams}</summary>
        public IEnumerator GetAsync(string table, string queryParams, Action<bool, string> onComplete)
        {
            if (!EnsureAuthenticated()) { onComplete?.Invoke(false, null); yield break; }

            string url = $"{supabaseUrl}/rest/v1/{table}";
            if (!string.IsNullOrEmpty(queryParams)) url += $"?{queryParams}";

            using var req = new UnityWebRequest(url, "GET");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("apikey", publishableKey);
            req.SetRequestHeader("Authorization", $"Bearer {_accessToken}");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(true, req.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"[SupabaseRpcClient] GET {table} failed: {req.error} — {req.downloadHandler?.Text}");
                onComplete?.Invoke(false, req.downloadHandler?.Text);
            }
        }

        /// <summary>POST /rest/v1/{table} — insert a row.</summary>
        public IEnumerator PostAsync(string table, string jsonBody, Action<bool, string> onComplete = null)
        {
            if (!EnsureAuthenticated()) { onComplete?.Invoke(false, null); yield break; }

            string url = $"{supabaseUrl}/rest/v1/{table}";
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", publishableKey);
            req.SetRequestHeader("Authorization", $"Bearer {_accessToken}");
            req.SetRequestHeader("Prefer", "return=minimal");  // don't echo back the inserted row

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(true, req.downloadHandler?.Text);
            }
            else
            {
                Debug.LogError($"[SupabaseRpcClient] POST {table} failed: {req.error} — {req.downloadHandler?.Text}");
                onComplete?.Invoke(false, req.downloadHandler?.Text);
            }
        }

        /// <summary>POST /rest/v1/rpc/{function} — call a Postgres function.</summary>
        public IEnumerator CallRpcAsync(string functionName, string jsonBody, Action<bool, string> onComplete)
        {
            if (!EnsureAuthenticated()) { onComplete?.Invoke(false, null); yield break; }

            string url = $"{supabaseUrl}/rest/v1/rpc/{functionName}";
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", publishableKey);
            req.SetRequestHeader("Authorization", $"Bearer {_accessToken}");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(true, req.downloadHandler.Text);
            }
            else
            {
                Debug.LogError($"[SupabaseRpcClient] RPC {functionName} failed: {req.error} — {req.downloadHandler?.Text}");
                onComplete?.Invoke(false, req.downloadHandler?.Text);
            }
        }

        // ============================================================================
        // Storage (ghost uploads)
        // ============================================================================

        /// <summary>
        /// Uploads a ghost JSON to the 'ghosts' bucket. Path: ghosts/{playerId}/{filename}.
        /// Returns the public URL on success.
        /// </summary>
        public IEnumerator UploadGhostAsync(string filename, string ghostJson,
            Action<bool, string> onComplete)
        {
            if (!EnsureAuthenticated()) { onComplete?.Invoke(false, null); yield break; }
            if (string.IsNullOrEmpty(_userId)) { onComplete?.Invoke(false, null); yield break; }

            string path = $"{_userId}/{filename}";
            string url = $"{supabaseUrl}/storage/v1/object/ghosts/{path}";
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ghostJson));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", publishableKey);
            req.SetRequestHeader("Authorization", $"Bearer {_accessToken}");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                // Return the path that future readers can use to fetch this ghost.
                string ghostUrl = $"{supabaseUrl}/storage/v1/object/ghosts/{path}";
                Debug.Log($"[SupabaseRpcClient] Ghost uploaded to {ghostUrl}");
                onComplete?.Invoke(true, ghostUrl);
            }
            else
            {
                Debug.LogError($"[SupabaseRpcClient] Ghost upload failed: {req.error} — {req.downloadHandler?.Text}");
                onComplete?.Invoke(false, req.downloadHandler?.Text);
            }
        }

        // ============================================================================
        // Helpers
        // ============================================================================

        private bool EnsureAuthenticated()
        {
            if (IsAuthenticated) return true;
            Debug.LogError("[SupabaseRpcClient] Not authenticated. Call SignInAnonymouslyAsync first.");
            return false;
        }
    }
}
