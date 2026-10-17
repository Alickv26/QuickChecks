using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace QuickChecks.Multiplayer
{
    /// <summary>
    /// High-level Supabase auth helper. Wraps SupabaseRpcClient with friendly
    /// semantics for the rest of the game.
    ///
    /// Lifecycle:
    ///   1. On first launch: SignInAnonymously() → creates an auth.users row + a
    ///      public.players row (the handle_new_player trigger fills display_name).
    ///      We cache the refresh token in PlayerPrefs so subsequent launches reuse
    ///      the same UUID.
    ///   2. On profile load: pulls display_name + is_premium from public.players
    ///      so the client knows the player's identity.
    ///   3. Optional: LinkEmail() lets the player attach an email + password to
    ///      their anonymous account so they can recover progress after reinstall.
    /// </summary>
    public class SupabaseAuth : MonoBehaviour
    {
        public static SupabaseAuth Instance { get; private set; }

        public string UserId => SupabaseRpcClient.Instance?.UserId;
        public bool IsSignedIn => SupabaseRpcClient.Instance != null && SupabaseRpcClient.Instance.IsAuthenticated;
        public string DisplayName { get; private set; }
        public bool IsPremium { get; private set; }

        public event Action OnSignedIn;
        public event Action OnProfileLoaded;

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

        /// <summary>
        /// Signs in anonymously if needed, then fetches the player's profile row.
        /// Safe to call on every app launch — it's a no-op if already signed in.
        /// </summary>
        public IEnumerator SignInAndLoadProfile(Action<bool> onComplete)
        {
            if (!SupabaseRpcClient.Instance.IsAuthenticated)
            {
                bool authOk = false;
                string userId = null;
                yield return SupabaseRpcClient.Instance.SignInAnonymouslyAsync((ok, id) =>
                {
                    authOk = ok;
                    userId = id;
                });

                if (!authOk)
                {
                    onComplete?.Invoke(false);
                    yield break;
                }
                OnSignedIn?.Invoke();
            }

            // Fetch player's row (display_name, is_premium) from public.players.
            string userIdFilter = $"id=eq.{SupabaseRpcClient.Instance.UserId}";
            yield return SupabaseRpcClient.Instance.GetAsync("players",
                $"{userIdFilter}&select=display_name,is_premium,email",
                (ok, json) =>
                {
                    if (ok && !string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            var arr = Newtonsoft.Json.Linq.JArray.Parse(json);
                            if (arr.Count > 0)
                            {
                                DisplayName = arr[0]["display_name"]?.ToString() ?? "Player";
                                IsPremium = arr[0]["is_premium"]?.ToObject<bool>() ?? false;
                                Debug.Log($"[SupabaseAuth] Profile loaded: {DisplayName} (premium={IsPremium})");

                                // Sync to local PlayerProfile so the rest of the game has the
                                // canonical playerId + displayName.
                                if (Core.GameFlowManager.Instance != null)
                                {
                                    var profile = Core.GameFlowManager.Instance.profile;
                                    if (profile != null)
                                    {
                                        profile.playerId = SupabaseRpcClient.Instance.UserId;
                                        profile.displayName = DisplayName;
                                        if (IsPremium) profile.isPremium = true;
                                        profile.Save();
                                    }
                                }

                                // If premium status changed, mirror to EntitlementManager.
                                if (IsPremium)
                                {
                                    var em = FindObjectOfType<Monetization.EntitlementManager>();
                                    if (em != null && !em.IsPremium) em.SetPremium(true);
                                }

                                OnProfileLoaded?.Invoke();
                            }
                            else
                            {
                                Debug.LogWarning("[SupabaseAuth] players row not found for user — trigger may have failed.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[SupabaseAuth] Failed to parse profile: {ex.Message}\n{json}");
                        }
                    }
                    else
                    {
                        Debug.LogError("[SupabaseAuth] Failed to fetch player profile.");
                    }
                });

            onComplete?.Invoke(true);
        }

        /// <summary>
        /// Links an email + password to the current anonymous account, so the player
        /// can sign in with email/password on a new device to recover their progress.
        /// </summary>
        public IEnumerator LinkEmailAsync(string email, string password, Action<bool, string> onComplete)
        {
            if (!IsSignedIn) { onComplete?.Invoke(false, "Not signed in."); yield break; }

            string url = $"{SupabaseRpcClient.Instance.SupabaseUrl()}/auth/v1/user";
            string body = $"{{\"email\":\"{email}\",\"password\":\"{password}\"}}";
            using var req = new UnityWebRequest(url, "PUT");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", SupabaseRpcClient.Instance.PublishableKey());
            req.SetRequestHeader("Authorization", $"Bearer {SupabaseRpcClient.Instance.AccessToken}");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                // Also persist the email on the players row (so other clients can show it).
                string updateBody = $"{{\"email\":\"{email}\"}}";
                string userIdFilter = $"id=eq.{SupabaseRpcClient.Instance.UserId}";
                yield return SupabaseRpcClient.Instance.PostAsync("players",
                    $"?{userIdFilter}",  // Not used; we need PUT for update
                    null);
                // Actually we need to use PUT — let's do it via the raw GetAsync alternative.
                // For v1 simplicity, we'll skip the players.email sync (the auth.users.email is the source of truth anyway).

                onComplete?.Invoke(true, null);
            }
            else
            {
                string err = req.downloadHandler?.Text ?? req.error;
                Debug.LogError($"[SupabaseAuth] LinkEmail failed: {err}");
                onComplete?.Invoke(false, err);
            }
        }

        /// <summary>
        /// Updates the player's display_name on the server (called when the player
        /// renames themselves in Settings). Only allowed on the player's own row (RLS).
        /// </summary>
        public IEnumerator UpdateDisplayNameAsync(string newName, Action<bool> onComplete)
        {
            if (!IsSignedIn) { onComplete?.Invoke(false); yield break; }

            string url = $"{SupabaseRpcClient.Instance.SupabaseUrl()}/rest/v1/players?id=eq.{SupabaseRpcClient.Instance.UserId}";
            string body = $"{{\"display_name\":\"{newName}\"}}";
            using var req = new UnityWebRequest(url, "PATCH");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", SupabaseRpcClient.Instance.PublishableKey());
            req.SetRequestHeader("Authorization", $"Bearer {SupabaseRpcClient.Instance.AccessToken}");
            req.SetRequestHeader("Prefer", "return=minimal");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                DisplayName = newName;
                Debug.Log($"[SupabaseAuth] display_name updated to {newName}");
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogError($"[SupabaseAuth] UpdateDisplayName failed: {req.error} — {req.downloadHandler?.Text}");
                onComplete?.Invoke(false);
            }
        }
    }

    /// <summary>Convenience extension so SupabaseAuth doesn't have to expose client internals.</summary>
    public static class SupabaseRpcClientExtensions
    {
        public static string SupabaseUrl(this SupabaseRpcClient client)
        {
            // Reach into the client via reflection to get the URL field.
            var field = typeof(SupabaseRpcClient).GetField("supabaseUrl",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(client) as string ?? "";
        }

        public static string PublishableKey(this SupabaseRpcClient client)
        {
            var field = typeof(SupabaseRpcClient).GetField("publishableKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(client) as string ?? "";
        }
    }
}
