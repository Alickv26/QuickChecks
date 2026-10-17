using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Audio;

namespace QuickChecks.UI
{
    /// <summary>
    /// Mode-aware neon-styled results screen.
    ///
    /// Reads the full RaceResultData payload from GameFlowManager.lastRaceResults and renders
    /// a layout appropriate to the mode:
    ///
    ///   Solo
    ///     Title    : "Race Complete!" (or "DNF" if humanDnf)
    ///     Stars    : ★★★ / ★★☆ / ★☆☆ / ☆☆☆ + subtitle ("Under par!", etc.)
    ///     Stats    : Your swipes / Par / Your time
    ///     Best     : Best swipes / Best time (or "Previous best" if isNewBest)
    ///     NewBest  : "NEW BEST! Ghost updated." banner (cyan)
    ///     Buttons  : Retry / Track Select
    ///
    ///   VsCpuEasy/Medium/Hard
    ///     Title    : "VICTORY!" (green/cyan) | "DEFEAT" (red) | "TIE" (yellow)
    ///     Stars    : ★★☆ etc. (based on player's swipes vs par)
    ///     Compare  : You vs CPU — side-by-side swipes + time + DNF
    ///     Summary  : "You beat CPU Easy by 3 swipes." (or "CPU beat you by 2 swipes.")
    ///     Buttons  : Retry / Track Select
    ///
    ///   PassPlay2/3/4
    ///     Title    : "Player N Wins!" (or "TIE!" if #1 tied)
    ///     Stars    : (none — competitive mode)
    ///     Ranking  : Full leaderboard of all players — rank / name / swipes / time / DNF
    ///     Highlight: Winner row glows cyan
    ///     Buttons  : Rematch (same players) / Track Select
    ///
    ///   Online (stub)
    ///     Same as VsCpu layout, with title "ONLINE MATCH" and "Opponent" label.
    ///     ELO change indicator stub (Δ +0 for now; backend in v1.1).
    ///
    /// Style (per ART_BIBLE.md):
    ///   - Background: #0E1414 (deep dark teal)
    ///   - Accent: #3FE0C2 (cyan)
    ///   - Text: #F7F7F2 (off-white)
    ///   - Typography: TextMeshProUGUI (Inter not bundled by default — LiberationSans SDF fallback)
    /// </summary>
    public class ResultsScreenController : MonoBehaviour
    {
        [Header("UI References — Common")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text modeLabelText;
        [SerializeField] private TMP_Text trackLabelText;

        [Header("UI References — Solo / Vs CPU")]
        [SerializeField] private TMP_Text starsText;          // ★★★ etc.
        [SerializeField] private TMP_Text swipesValueText;
        [SerializeField] private TMP_Text parValueText;
        [SerializeField] private TMP_Text timeValueText;
        [SerializeField] private TMP_Text bestValueText;
        [SerializeField] private TMP_Text newBestText;

        [Header("UI References — Vs CPU / Online comparison")]
        [SerializeField] private GameObject comparisonPanel;   // holds two columns
        [SerializeField] private TMP_Text youNameText;
        [SerializeField] private TMP_Text youSwipesText;
        [SerializeField] private TMP_Text youTimeText;
        [SerializeField] private TMP_Text opponentNameText;
        [SerializeField] private TMP_Text opponentSwipesText;
        [SerializeField] private TMP_Text opponentTimeText;
        [SerializeField] private TMP_Text comparisonSummaryText;

        [Header("UI References — Pass & Play ranking")]
        [SerializeField] private GameObject rankingPanel;
        [SerializeField] private Transform rankingRowsContainer;
        [SerializeField] private GameObject rankingRowPrefab;  // optional; runtime-built if null

        [Header("UI References — Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button leaderboardButton;     // Solo only — opens leaderboard modal
        [SerializeField] private TMP_Text retryButtonText;  // so label can be "Retry" / "Rematch"

        [Header("Leaderboard panel (optional — Solo only)")]
        [SerializeField] private LeaderboardPanel leaderboardPanel;

        [Header("Colors (Art Bible palette)")]
        [SerializeField] private Color accentCyan = new Color(0.247f, 0.878f, 0.760f);    // #3FE0C2
        [SerializeField] private Color textOffWhite = new Color(0.969f, 0.969f, 0.949f); // #F7F7F2
        [SerializeField] private Color victoryGreen = new Color(0.42f, 0.79f, 0.47f);
        [SerializeField] private Color defeatRed = new Color(1f, 0.42f, 0.42f);
        [SerializeField] private Color tieYellow = new Color(1f, 0.82f, 0.4f);
        [SerializeField] private Color dnfGray = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color dimPlayer = new Color(0.6f, 0.6f, 0.6f);

        private void Start()
        {
            var flow = GameFlowManager.Instance;
            if (flow == null)
            {
                Debug.LogError("[Results] GameFlowManager not found!");
                return;
            }

            // Phase 2 path: use the structured payload if available.
            if (flow.lastRaceResults != null)
            {
                RenderFromPayload(flow.lastRaceResults);
            }
            else
            {
                // Legacy fallback for any callers still using the old scalar path.
                RenderLegacy(flow);
            }

            WireButtons();
        }

        // --- Mode-aware rendering ---

        private void RenderFromPayload(RaceResultData r)
        {
            // Common: mode + track label
            if (modeLabelText != null)
            {
                modeLabelText.text = r.mode.DisplayName().ToUpperInvariant();
                modeLabelText.color = accentCyan;
            }
            if (trackLabelText != null)
            {
                trackLabelText.text = $"{r.trackDisplayName}   ·   Par {r.parSwipes}";
                trackLabelText.color = dimPlayer;
            }

            // Hide everything mode-specific first; we'll re-enable what we need.
            if (starsText != null) starsText.gameObject.SetActive(false);
            if (swipesValueText != null) swipesValueText.gameObject.SetActive(false);
            if (parValueText != null) parValueText.gameObject.SetActive(false);
            if (timeValueText != null) timeValueText.gameObject.SetActive(false);
            if (bestValueText != null) bestValueText.gameObject.SetActive(false);
            if (newBestText != null) newBestText.gameObject.SetActive(false);
            if (comparisonPanel != null) comparisonPanel.SetActive(false);
            if (rankingPanel != null) rankingPanel.SetActive(false);
            if (subtitleText != null) subtitleText.gameObject.SetActive(false);

            // Branch by mode.
            if (r.IsSolo)             RenderSolo(r);
            else if (r.IsVsCpu)       RenderVsCpu(r);
            else if (r.IsPassPlay)    RenderPassPlay(r);
            else if (r.IsOnline)      RenderOnline(r);
            else                      RenderSolo(r);  // safe fallback

            // Retry button label: "Rematch" for multiplayer (same players), "Retry" otherwise.
            if (retryButtonText != null)
            {
                retryButtonText.text = r.IsPassPlay ? "REMATCH" : "RETRY";
            }
        }

        // --- Solo ---

        private void RenderSolo(RaceResultData r)
        {
            var human = r.HumanResult;
            if (human == null) return;

            // Show the Leaderboard button (Solo only).
            if (leaderboardButton != null)
            {
                leaderboardButton.gameObject.SetActive(true);
                if (leaderboardButton.GetComponent<ButtonHoverFx>() == null)
                {
                    leaderboardButton.gameObject.AddComponent<ButtonHoverFx>();
                }
            }

            if (titleText != null)
            {
                titleText.text = r.humanDnf ? "DNF" : "Race Complete!";
                titleText.color = r.humanDnf ? dnfGray : accentCyan;
            }

            if (subtitleText != null)
            {
                subtitleText.text = StarRating.SubtitleFor(r.starsEarned, r.humanDnf);
                subtitleText.color = textOffWhite;
                subtitleText.gameObject.SetActive(true);
            }

            if (starsText != null)
            {
                starsText.text = StarRating.DisplayString(r.starsEarned);
                starsText.fontSize = 96;
                starsText.color = r.starsEarned >= 3 ? accentCyan : tieYellow;
                starsText.gameObject.SetActive(true);
            }

            if (swipesValueText != null)
            {
                swipesValueText.text = $"Swipes: <b>{human.SwipesDisplay}</b>";
                swipesValueText.color = textOffWhite;
                swipesValueText.gameObject.SetActive(true);
            }

            if (parValueText != null)
            {
                parValueText.text = $"Par: <b>{r.parSwipes}</b>";
                parValueText.color = dimPlayer;
                parValueText.gameObject.SetActive(true);
            }

            if (timeValueText != null)
            {
                timeValueText.text = $"Time: <b>{human.TimeDisplay}</b>";
                timeValueText.color = textOffWhite;
                timeValueText.gameObject.SetActive(true);
            }

            if (bestValueText != null)
            {
                if (r.isNewBest)
                {
                    bestValueText.text = $"Previous best: {r.prevBestSwipes} swipes / {r.prevBestTimeMs / 1000f:F2}s";
                }
                else if (r.prevBestSwipes > 0)
                {
                    bestValueText.text = $"Best: {r.prevBestSwipes} swipes / {r.prevBestTimeMs / 1000f:F2}s";
                }
                else
                {
                    bestValueText.text = "First run on this track!";
                }
                bestValueText.color = accentCyan;
                bestValueText.gameObject.SetActive(true);
            }

            if (newBestText != null && r.isNewBest)
            {
                newBestText.text = "*** NEW BEST! GHOST UPDATED ***";
                newBestText.color = accentCyan;
                newBestText.fontSize = 42;
                newBestText.gameObject.SetActive(true);
            }
        }

        // --- Vs CPU ---

        private void RenderVsCpu(RaceResultData r)
        {
            var human = r.HumanResult;
            var opp = r.OpponentResult;
            if (human == null) return;

            // Title: VICTORY / DEFEAT / TIE
            bool tie = opp != null && !human.isDnf && !opp.isDnf &&
                       human.swipes == opp.swipes && human.finishTimeMs == opp.finishTimeMs;

            if (titleText != null)
            {
                if (r.humanDnf)
                {
                    titleText.text = "DNF";
                    titleText.color = dnfGray;
                }
                else if (tie)
                {
                    titleText.text = "TIE!";
                    titleText.color = tieYellow;
                }
                else if (r.humanWon)
                {
                    titleText.text = "VICTORY!";
                    titleText.color = victoryGreen;
                }
                else
                {
                    titleText.text = "DEFEAT";
                    titleText.color = defeatRed;
                }
            }

            // Stars (player only — CPU has no stars)
            if (starsText != null)
            {
                starsText.text = StarRating.DisplayString(r.starsEarned);
                starsText.fontSize = 64;
                starsText.color = r.starsEarned >= 3 ? accentCyan : tieYellow;
                starsText.gameObject.SetActive(true);
            }

            // Comparison panel
            if (comparisonPanel != null) comparisonPanel.SetActive(true);

            if (youNameText != null)
            {
                youNameText.text = "YOU";
                youNameText.color = r.humanWon ? victoryGreen : textOffWhite;
            }
            if (youSwipesText != null)
            {
                youSwipesText.text = human.SwipesDisplay;
                youSwipesText.color = r.humanWon ? victoryGreen : textOffWhite;
            }
            if (youTimeText != null)
            {
                youTimeText.text = human.TimeDisplay;
                youTimeText.color = textOffWhite;
            }

            if (opp != null)
            {
                if (opponentNameText != null)
                {
                    opponentNameText.text = r.mode.IsOnline() ? "OPPONENT" : "CPU";
                    opponentNameText.color = !r.humanWon && !opp.isDnf ? defeatRed : dimPlayer;
                }
                if (opponentSwipesText != null)
                {
                    opponentSwipesText.text = opp.SwipesDisplay;
                    opponentSwipesText.color = !r.humanWon && !opp.isDnf ? defeatRed : dimPlayer;
                }
                if (opponentTimeText != null)
                {
                    opponentTimeText.text = opp.TimeDisplay;
                    opponentTimeText.color = dimPlayer;
                }
            }

            // Summary line
            if (comparisonSummaryText != null)
            {
                comparisonSummaryText.text = BuildComparisonSummary(r, human, opp, tie);
                comparisonSummaryText.color = r.humanWon ? victoryGreen :
                                               r.humanDnf ? dnfGray : defeatRed;
            }
        }

        // --- Online (stub) — same as Vs CPU with different label ---

        private void RenderOnline(RaceResultData r)
        {
            RenderVsCpu(r);
            // Subtitle hints this is a stub.
            if (subtitleText != null)
            {
                subtitleText.text = "Online mode is a CPU stub in v1. Real netcode in v1.1.";
                subtitleText.color = dimPlayer;
                subtitleText.gameObject.SetActive(true);
            }
        }

        // --- Pass & Play ranking ---

        private void RenderPassPlay(RaceResultData r)
        {
            // Title: "Player N Wins!"
            if (titleText != null)
            {
                if (r.winnerIndex < 0 || r.winnerIndex >= r.players.Count)
                {
                    titleText.text = "RACE OVER";
                    titleText.color = dimPlayer;
                }
                else
                {
                    var winner = r.players[r.winnerIndex];
                    // Check for tie: if two players share the same rank-1 (rank==1 with identical swipes+time)
                    // We didn't store ties explicitly, but if rank counts > 1 with rank==1, it's a tie.
                    int rank1Count = 0;
                    foreach (var p in r.players) if (p.rank == 1) rank1Count++;

                    if (rank1Count > 1)
                    {
                        titleText.text = "TIE!";
                        titleText.color = tieYellow;
                    }
                    else
                    {
                        titleText.text = $"{winner.playerName} Wins!";
                        titleText.color = victoryGreen;
                    }
                }
            }

            if (rankingPanel != null)
            {
                rankingPanel.SetActive(true);
                BuildRankingRows(r);
            }
        }

        private void BuildRankingRows(RaceResultData r)
        {
            if (rankingRowsContainer == null)
            {
                Debug.LogWarning("[Results] rankingRowsContainer is null — cannot build P&P rows.");
                return;
            }

            // Clear any existing rows.
            for (int i = rankingRowsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(rankingRowsContainer.GetChild(i).gameObject);
            }

            // Sort: by rank (rank 1 first), DNF last.
            var sorted = new List<RaceResultData.PlayerResult>(r.players);
            sorted.Sort((a, b) =>
            {
                if (a.rank > 0 && b.rank > 0) return a.rank.CompareTo(b.rank);
                if (a.rank > 0) return -1;
                if (b.rank > 0) return 1;
                return string.Compare(a.playerName, b.playerName, System.StringComparison.Ordinal);
            });

            for (int i = 0; i < sorted.Count; i++)
            {
                var p = sorted[i];
                GameObject rowGo = rankingRowPrefab != null
                    ? Instantiate(rankingRowPrefab, rankingRowsContainer)
                    : CreateRuntimeRankingRow(p, i, r);
                if (rankingRowPrefab != null) PopulateRankingRow(rowGo, p, i, r);
            }
        }

        private GameObject CreateRuntimeRankingRow(RaceResultData.PlayerResult p, int rowIdx, RaceResultData r)
        {
            var go = new GameObject($"Row_{rowIdx}_{p.playerName}");
            go.transform.SetParent(rankingRowsContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, 70);
            rt.anchoredPosition = new Vector2(0, -rowIdx * 80);

            // Background strip — winner gets cyan tint
            var bg = go.AddComponent<Image>();
            bool isWinner = p.rank == 1;
            bg.color = isWinner
                ? new Color(0.247f, 0.878f, 0.760f, 0.12f)
                : new Color(1f, 1f, 1f, 0.03f);

            // Horizontal layout: rank | name | swipes | time
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 20, 8, 8);
            hlg.spacing = 24;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;

            AddRowLabel(go, p.rank > 0 ? $"{p.rank}." : "—", 60,
                isWinner ? victoryGreen : dimPlayer, 36);
            AddRowLabel(go, p.playerName + (p.isHuman ? "  [YOU]" : ""), 0,
                isWinner ? victoryGreen : textOffWhite, 32);
            AddRowLabel(go, p.SwipesDisplay, 140,
                isWinner ? victoryGreen : textOffWhite, 32);
            AddRowLabel(go, p.TimeDisplay, 160,
                isWinner ? victoryGreen : dimPlayer, 32);

            return go;
        }

        private void PopulateRankingRow(GameObject rowGo, RaceResultData.PlayerResult p, int rowIdx, RaceResultData r)
        {
            // If a prefab was provided, find named children to populate.
            var rankText = FindChildTMP(rowGo.transform, "RankText");
            var nameText = FindChildTMP(rowGo.transform, "NameText");
            var swipesText = FindChildTMP(rowGo.transform, "SwipesText");
            var timeText = FindChildTMP(rowGo.transform, "TimeText");

            if (rankText != null) rankText.text = p.rank > 0 ? $"{p.rank}." : "—";
            if (nameText != null) nameText.text = p.playerName + (p.isHuman ? "  [YOU]" : "");
            if (swipesText != null) swipesText.text = p.SwipesDisplay;
            if (timeText != null) timeText.text = p.TimeDisplay;

            // Winner highlight
            var bg = rowGo.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = p.rank == 1
                    ? new Color(0.247f, 0.878f, 0.760f, 0.12f)
                    : new Color(1f, 1f, 1f, 0.03f);
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

        private static void AddRowLabel(GameObject parent, string text, float width, Color color, float fontSize)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = color;
            tmp.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            if (width > 0)
            {
                rt.sizeDelta = new Vector2(width, 60);
            }
        }

        // --- Comparison summary text ---

        private static string BuildComparisonSummary(RaceResultData r,
                                                     RaceResultData.PlayerResult human,
                                                     RaceResultData.PlayerResult opp,
                                                     bool tie)
        {
            if (opp == null) return "";
            if (human.isDnf) return "You hit the swipe cap — better luck next time.";
            if (r.humanWon && !opp.isFinished) return $"You finished before your opponent could. {opp.playerName} was on {opp.swipes} swipe{(opp.swipes == 1 ? "" : "s")}.";
            if (opp.isDnf) return $"Opponent hit the swipe cap — you win by default.";
            if (tie) return "Perfect tie — same swipes, same time!";

            int diff = Mathf.Abs(human.swipes - opp.swipes);
            if (r.humanWon)
            {
                if (diff == 0)
                {
                    // Same swipes, faster time
                    return $"You won by {(opp.finishTimeMs - human.finishTimeMs) / 1000f:F2}s — same swipes, faster run!";
                }
                return $"You beat {opp.playerName} by {diff} swipe{(diff == 1 ? "" : "s")}.";
            }
            else
            {
                if (diff == 0)
                {
                    return $"{opp.playerName} beat you by {(human.finishTimeMs - opp.finishTimeMs) / 1000f:F2}s.";
                }
                return $"{opp.playerName} beat you by {diff} swipe{(diff == 1 ? "" : "s")}.";
            }
        }

        // --- Button wiring ---

        private void WireButtons()
        {
            if (retryButton != null)
            {
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(OnRetryClicked);
            }
            if (menuButton != null)
            {
                menuButton.onClick.RemoveAllListeners();
                menuButton.onClick.AddListener(OnMenuClicked);
            }
            if (leaderboardButton != null)
            {
                leaderboardButton.onClick.RemoveAllListeners();
                leaderboardButton.onClick.AddListener(OnLeaderboardClicked);
                // Show the button only in Solo mode (other modes don't have a global leaderboard).
                leaderboardButton.gameObject.SetActive(false);  // default — set true in RenderSolo
            }
        }

        private void OnLeaderboardClicked()
        {
            if (Audio.SfxPlayer.Instance != null) Audio.SfxPlayer.Instance.PlayMenuClick();
            if (leaderboardPanel == null)
            {
                Debug.LogWarning("[Results] Leaderboard panel not wired.");
                return;
            }
            // Use the last race results to know which track to query.
            var r = GameFlowManager.Instance?.lastRaceResults;
            if (r == null)
            {
                Debug.LogWarning("[Results] No lastRaceResults — can't query leaderboard.");
                return;
            }
            leaderboardPanel.Show(r.trackId, r.trackDisplayName);
        }

        private void OnRetryClicked()
        {
            // Play SFX
            if (Audio.SfxPlayer.Instance != null) Audio.SfxPlayer.Instance.PlayMenuClick();
            Debug.Log("[Results] RETRY clicked — restarting race.");
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.RetryRace();
            }
        }

        private void OnMenuClicked()
        {
            if (Audio.SfxPlayer.Instance != null) Audio.SfxPlayer.Instance.PlayMenuClick();
            Debug.Log("[Results] MENU clicked — going to track select.");
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.GoToTrackSelect();
            }
        }

        // --- Legacy fallback (pre-Phase-2 callers) ---

        private void RenderLegacy(GameFlowManager flow)
        {
            Debug.LogWarning("[Results] No RaceResultData payload — using legacy scalar path. " +
                              "Some mode-specific UI will be unavailable.");

            if (titleText != null)
            {
                titleText.text = "Race Complete!";
                titleText.color = accentCyan;
            }
            if (swipesValueText != null)
            {
                swipesValueText.text = $"Swipes: <b>{flow.lastRaceSwipes}</b>";
                swipesValueText.color = textOffWhite;
                swipesValueText.gameObject.SetActive(true);
            }
            if (timeValueText != null)
            {
                timeValueText.text = $"Time: <b>{flow.lastRaceTimeMs / 1000f:F2}s</b>";
                timeValueText.color = textOffWhite;
                timeValueText.gameObject.SetActive(true);
            }
            if (bestValueText != null)
            {
                bestValueText.text = flow.lastRaceIsNewBest
                    ? $"Previous best: {flow.lastRaceBestSwipes} swipes / {flow.lastRaceBestTimeMs / 1000f:F2}s"
                    : $"Best: {flow.lastRaceBestSwipes} swipes / {flow.lastRaceBestTimeMs / 1000f:F2}s";
                bestValueText.color = accentCyan;
                bestValueText.gameObject.SetActive(true);
            }
            if (newBestText != null && flow.lastRaceIsNewBest)
            {
                newBestText.text = "*** NEW FASTEST LAP! ***\nGhost updated!";
                newBestText.color = accentCyan;
                newBestText.gameObject.SetActive(true);
            }
        }
    }
}
