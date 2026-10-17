using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Multiplayer;
using QuickChecks.Audio;

namespace QuickChecks.UI
{
    /// <summary>
    /// Leaderboard panel — shown as a modal overlay on the Results screen (Solo mode).
    ///
    /// Renders:
    ///   - Top-10 list: rank | display_name | swipes | time
    ///   - "Your rank" row below the top-10 (so players ranked #47 can see where they stand)
    ///   - Loading indicator while fetching
    ///   - Error message if fetch fails
    ///   - Close button
    ///
    /// Fetches data via SupabaseLeaderboard.FetchLeaderboardAsync(trackId, 10, callback).
    /// </summary>
    public class LeaderboardPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform rowsContainer;
        [SerializeField] private GameObject rowPrefab;     // optional; runtime-built if null
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text trackLabel;
        [SerializeField] private TMP_Text loadingLabel;
        [SerializeField] private TMP_Text errorLabel;
        [SerializeField] private TMP_Text yourRankLabel;
        [SerializeField] private Button closeButton;

        [Header("Colors")]
        [SerializeField] private Color rankColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color nameColor = new Color(0.969f, 0.969f, 0.949f);
        [SerializeField] private Color swipesColor = new Color(0.247f, 0.878f, 0.760f);
        [SerializeField] private Color timeColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color yourColor = new Color(0.247f, 0.878f, 0.760f);

        private string _trackId;

        private void Start()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
                if (closeButton.GetComponent<ButtonHoverFx>() == null)
                    closeButton.gameObject.AddComponent<ButtonHoverFx>();
            }
            if (loadingLabel != null) loadingLabel.gameObject.SetActive(false);
            if (errorLabel != null) errorLabel.gameObject.SetActive(false);
            if (yourRankLabel != null) yourRankLabel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows the panel and kicks off a leaderboard fetch for the given track.
        /// </summary>
        public void Show(string trackId, string trackDisplayName)
        {
            _trackId = trackId;
            gameObject.SetActive(true);

            if (titleLabel != null)
            {
                titleLabel.text = "Leaderboard";
                titleLabel.color = Phase2UiPalette.AccentCyan;
            }
            if (trackLabel != null)
            {
                trackLabel.text = trackDisplayName;
                trackLabel.color = Phase2UiPalette.TextDim;
            }

            StartCoroutine(FetchAndRender());
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnCloseClicked()
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            Hide();
        }

        private IEnumerator FetchAndRender()
        {
            // Loading state
            if (loadingLabel != null) loadingLabel.gameObject.SetActive(true);
            if (errorLabel != null) errorLabel.gameObject.SetActive(false);
            if (yourRankLabel != null) yourRankLabel.gameObject.SetActive(false);

            // Make sure Supabase auth has finished before fetching.
            if (SupabaseAuth.Instance != null && !SupabaseAuth.Instance.IsSignedIn)
            {
                bool authDone = false;
                StartCoroutine(SupabaseAuth.Instance.SignInAndLoadProfile(ok => authDone = true));
                float timeout = 8f;
                while (!authDone && timeout > 0f)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }
            }

            // Fetch leaderboard
            bool fetchOk = false;
            List<SupabaseLeaderboard.LeaderboardEntry> entries = null;
            int playerRank = -1;

            if (SupabaseLeaderboard.Instance != null)
            {
                yield return SupabaseLeaderboard.Instance.FetchLeaderboardAsync(_trackId, 10,
                    (ok, list, rank) =>
                    {
                        fetchOk = ok;
                        entries = list;
                        playerRank = rank;
                    });
            }
            else
            {
                Debug.LogError("[LeaderboardPanel] SupabaseLeaderboard not initialized.");
            }

            if (loadingLabel != null) loadingLabel.gameObject.SetActive(false);

            if (!fetchOk || entries == null)
            {
                if (errorLabel != null)
                {
                    errorLabel.text = "Couldn't reach the leaderboard.\nCheck your connection and try again.";
                    errorLabel.color = new Color(1f, 0.42f, 0.42f);
                    errorLabel.gameObject.SetActive(true);
                }
                yield break;
            }

            RenderRows(entries, playerRank);
        }

        private void RenderRows(List<SupabaseLeaderboard.LeaderboardEntry> entries, int playerRank)
        {
            if (rowsContainer == null)
            {
                Debug.LogError("[LeaderboardPanel] rowsContainer is null!");
                return;
            }

            // Clear any existing rows
            for (int i = rowsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(rowsContainer.GetChild(i).gameObject);
            }

            if (entries.Count == 0)
            {
                if (errorLabel != null)
                {
                    errorLabel.text = "No results yet.\nBe the first to set a record!";
                    errorLabel.color = Phase2UiPalette.TextDim;
                    errorLabel.gameObject.SetActive(true);
                }
                return;
            }

            // Render top-10
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                // Skip the appended "player rank" row for now — we render it separately below.
                if (entry.isRequester && entry.rank > 10) continue;

                var rowGo = rowPrefab != null
                    ? Instantiate(rowPrefab, rowsContainer)
                    : CreateRuntimeRow(entry);
                if (rowPrefab != null) PopulateRow(rowGo, entry);
            }

            // Show "Your rank" below the top-10
            if (yourRankLabel != null)
            {
                if (playerRank > 0)
                {
                    var playerEntry = entries.FirstOrDefault(e => e.isRequester);
                    if (playerEntry != null)
                    {
                        yourRankLabel.text = $"Your rank: #{playerRank}   ·   " +
                                              $"{playerEntry.swipeCount} swipes   ·   " +
                                              $"{playerEntry.TimeDisplay}";
                        yourRankLabel.color = yourColor;
                        yourRankLabel.gameObject.SetActive(true);
                    }
                }
                else
                {
                    yourRankLabel.text = "You haven't completed this track yet.";
                    yourRankLabel.color = Phase2UiPalette.TextDim;
                    yourRankLabel.gameObject.SetActive(true);
                }
            }
        }

        private GameObject CreateRuntimeRow(SupabaseLeaderboard.LeaderboardEntry entry)
        {
            var go = new GameObject($"Row_{entry.rank}");
            go.transform.SetParent(rowsContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 60);

            // Background — top-3 highlighted with cyan tint
            var bg = go.AddComponent<Image>();
            bool top3 = entry.rank <= 3;
            bool isYou = entry.isRequester;
            bg.color = isYou
                ? new Color(0.247f, 0.878f, 0.760f, 0.18f)
                : top3
                    ? new Color(0.247f, 0.878f, 0.760f, 0.06f)
                    : new Color(1f, 1f, 1f, 0.02f);

            // Horizontal layout: rank | name | swipes | time
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 20, 6, 6);
            hlg.spacing = 16;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;

            AddRowLabel(go.transform, $"{entry.rank}.", 60,
                top3 ? swipesColor : rankColor, 28);
            AddRowLabel(go.transform, TruncateName(entry.displayName), 0,
                isYou ? yourColor : nameColor, 28);
            AddRowLabel(go.transform, $"{entry.swipeCount}", 120,
                isYou ? yourColor : swipesColor, 28);
            AddRowLabel(go.transform, entry.TimeDisplay, 140,
                isYou ? yourColor : timeColor, 24);

            return go;
        }

        private static string TruncateName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Player";
            return name.Length > 18 ? name.Substring(0, 17) + "…" : name;
        }

        private void PopulateRow(GameObject rowGo, SupabaseLeaderboard.LeaderboardEntry entry)
        {
            var rankText = FindChildTMP(rowGo.transform, "RankText");
            var nameText = FindChildTMP(rowGo.transform, "NameText");
            var swipesText = FindChildTMP(rowGo.transform, "SwipesText");
            var timeText = FindChildTMP(rowGo.transform, "TimeText");

            if (rankText != null) rankText.text = $"{entry.rank}.";
            if (nameText != null) nameText.text = TruncateName(entry.displayName);
            if (swipesText != null) swipesText.text = $"{entry.swipeCount}";
            if (timeText != null) timeText.text = entry.TimeDisplay;

            var bg = rowGo.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = entry.isRequester
                    ? new Color(0.247f, 0.878f, 0.760f, 0.18f)
                    : entry.rank <= 3
                        ? new Color(0.247f, 0.878f, 0.760f, 0.06f)
                        : new Color(1f, 1f, 1f, 0.02f);
            }
        }

        private static TMP_Text FindChildTMP(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child.GetComponent<TMP_Text>();
            }
            return null;
        }

        private static void AddRowLabel(Transform parent, string text, float width, Color color, float fontSize)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = color;
            tmp.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            if (width > 0) rt.sizeDelta = new Vector2(width, 48);
        }
    }

    /// <summary>Null-safe FirstOrDefault for List&lt;T&gt; — Unity's .NET profile doesn't always have LINQ's.</summary>
    internal static class ListExtensions
    {
        public static T FirstOrDefault<T>(this List<T> list, System.Func<T, bool> predicate)
        {
            if (list == null) return default;
            foreach (var item in list)
            {
                if (predicate(item)) return item;
            }
            return default;
        }
    }
}
