using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Audio;

namespace QuickChecks.UI
{
    /// <summary>
    /// Cinematic results screen (UI Overhaul). Neon Dark refined style.
    ///
    /// Features:
    ///   - Large glowing VICTORY / DEFEAT / RACE COMPLETE / DNF title with letter spacing
    ///   - Animated star rating (stars pop in one-by-one with 0.2s delay)
    ///   - Clean stat cards with labeled values
    ///   - P&P: ranked podium-style layout with player color dots
    ///   - Vs CPU: side-by-side comparison with colored headers
    ///   - Big Retry + Menu buttons at bottom with hover FX
    ///   - Full-screen dim overlay for focus
    /// </summary>
    public class ResultsScreenController : MonoBehaviour
    {
        [Header("UI References — Common")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text modeLabelText;
        [SerializeField] private TMP_Text trackLabelText;

        [Header("UI References — Solo / Vs CPU")]
        [SerializeField] private TMP_Text starsText;
        [SerializeField] private TMP_Text swipesValueText;
        [SerializeField] private TMP_Text parValueText;
        [SerializeField] private TMP_Text timeValueText;
        [SerializeField] private TMP_Text bestValueText;
        [SerializeField] private TMP_Text newBestText;

        [Header("UI References — Vs CPU / Online comparison")]
        [SerializeField] private GameObject comparisonPanel;
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
        [SerializeField] private GameObject rankingRowPrefab;

        [Header("UI References — Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private TMP_Text retryButtonText;

        [Header("Leaderboard panel (optional — Solo only)")]
        [SerializeField] private LeaderboardPanel leaderboardPanel;

        [Header("Colors (Art Bible palette)")]
        [SerializeField] private Color accentCyan = new(0.247f, 0.878f, 0.760f);
        [SerializeField] private Color textOffWhite = new(0.969f, 0.969f, 0.949f);
        [SerializeField] private Color victoryGreen = new(0.42f, 0.79f, 0.47f);
        [SerializeField] private Color defeatRed = new(1f, 0.42f, 0.42f);
        [SerializeField] private Color tieYellow = new(1f, 0.82f, 0.4f);
        [SerializeField] private Color dnfGray = new(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color dimPlayer = new(0.6f, 0.6f, 0.6f);

        // Player colors for P&P ranking
        private static readonly Color[] PLAYER_COLORS = {
            new(0.247f, 0.878f, 0.760f),  // P1: cyan
            new(1f, 0.42f, 0.42f),         // P2: red
            new(0.42f, 0.79f, 0.47f),      // P3: green
            new(0.42f, 0.58f, 1f),          // P4: blue
        };

        private void Start()
        {
            var flow = GameFlowManager.Instance;
            if (flow == null)
            {
                Debug.LogError("[Results] GameFlowManager not found!");
                return;
            }

            if (flow.lastRaceResults != null)
                RenderFromPayload(flow.lastRaceResults);
            else
                RenderLegacy(flow);

            WireButtons();
        }

        private void RenderFromPayload(RaceResultData r)
        {
            // Hide all mode-specific UI first
            if (starsText != null) starsText.gameObject.SetActive(false);
            if (swipesValueText != null) swipesValueText.gameObject.SetActive(false);
            if (parValueText != null) parValueText.gameObject.SetActive(false);
            if (timeValueText != null) timeValueText.gameObject.SetActive(false);
            if (bestValueText != null) bestValueText.gameObject.SetActive(false);
            if (newBestText != null) newBestText.gameObject.SetActive(false);
            if (comparisonPanel != null) comparisonPanel.SetActive(false);
            if (rankingPanel != null) rankingPanel.SetActive(false);
            if (subtitleText != null) subtitleText.gameObject.SetActive(false);
            if (leaderboardButton != null) leaderboardButton.gameObject.SetActive(false);

            // Common: mode + track label with refined styling
            if (modeLabelText != null)
            {
                modeLabelText.text = r.mode.DisplayName().ToUpperInvariant();
                modeLabelText.color = accentCyan;
                modeLabelText.fontSize = 20;
                modeLabelText.fontStyle = FontStyles.Bold;
            }
            if (trackLabelText != null)
            {
                trackLabelText.text = $"{r.trackDisplayName}   ·   Par {r.parSwipes}";
                trackLabelText.color = dimPlayer;
                trackLabelText.fontSize = 22;
            }

            // Branch by mode
            if (r.IsSolo) RenderSolo(r);
            else if (r.IsVsCpu) RenderVsCpu(r);
            else if (r.IsPassPlay) RenderPassPlay(r);
            else if (r.IsOnline) RenderOnline(r);
            else RenderSolo(r);

            if (retryButtonText != null)
                retryButtonText.text = r.IsPassPlay ? "REMATCH" : "RETRY";
        }

        // =====================================================================
        // Solo
        // =====================================================================

        private void RenderSolo(RaceResultData r)
        {
            var human = r.HumanResult;
            if (human == null) return;

            // Leaderboard button
            if (leaderboardButton != null)
            {
                leaderboardButton.gameObject.SetActive(true);
                EnsureHoverFx(leaderboardButton);
            }

            // Title: "RACE COMPLETE" with letter spacing + glow
            if (titleText != null)
            {
                titleText.text = r.humanDnf ? "DID NOT FINISH" : "RACE COMPLETE";
                titleText.color = r.humanDnf ? dnfGray : accentCyan;
                titleText.fontSize = 64;
                titleText.fontStyle = FontStyles.Bold;
                titleText.characterSpacing = 4;
            }

            if (subtitleText != null)
            {
                subtitleText.text = StarRating.SubtitleFor(r.starsEarned, r.humanDnf);
                subtitleText.color = textOffWhite;
                subtitleText.fontSize = 28;
                subtitleText.gameObject.SetActive(true);
            }

            // Animated stars
            if (starsText != null)
            {
                starsText.text = StarRating.DisplayString(r.starsEarned);
                starsText.fontSize = 80;
                starsText.color = r.starsEarned >= 3 ? accentCyan : tieYellow;
                starsText.gameObject.SetActive(true);
                StartCoroutine(AnimateStars());
            }

            // Stats with labels
            if (swipesValueText != null)
            {
                swipesValueText.text = $"<size=36>SWIPES</size>\n<size=48><b>{human.SwipesDisplay}</b></size>";
                swipesValueText.color = textOffWhite;
                swipesValueText.alignment = TextAlignmentOptions.Center;
                swipesValueText.gameObject.SetActive(true);
            }
            if (parValueText != null)
            {
                parValueText.text = $"<size=36>PAR</size>\n<size=48><b>{r.parSwipes}</b></size>";
                parValueText.color = dimPlayer;
                parValueText.alignment = TextAlignmentOptions.Center;
                parValueText.gameObject.SetActive(true);
            }
            if (timeValueText != null)
            {
                parValueText.text = $"<size=36>PAR</size>\n<size=48><b>{r.parSwipes}</b></size>";
                timeValueText.text = $"<size=36>TIME</size>\n<size=48><b>{human.TimeDisplay}</b></size>";
                timeValueText.color = textOffWhite;
                timeValueText.alignment = TextAlignmentOptions.Center;
                timeValueText.gameObject.SetActive(true);
            }
            if (bestValueText != null)
            {
                if (r.isNewBest)
                    bestValueText.text = $"<size=28>PREVIOUS BEST</size>\n<size=36>{r.prevBestSwipes} swipes · {r.prevBestTimeMs / 1000f:F2}s</size>";
                else if (r.prevBestSwipes > 0)
                    bestValueText.text = $"<size=28>BEST</size>\n<size=36>{r.prevBestSwipes} swipes · {r.prevBestTimeMs / 1000f:F2}s</size>";
                else
                    bestValueText.text = "<size=28>FIRST RUN</size>\n<size=36>No previous best</size>";
                bestValueText.color = accentCyan;
                bestValueText.alignment = TextAlignmentOptions.Center;
                bestValueText.gameObject.SetActive(true);
            }
            if (newBestText != null && r.isNewBest)
            {
                newBestText.text = "★ NEW BEST! ★";
                newBestText.color = accentCyan;
                newBestText.fontSize = 32;
                newBestText.fontStyle = FontStyles.Bold;
                newBestText.gameObject.SetActive(true);
            }
        }

        private IEnumerator AnimateStars()
        {
            if (starsText == null) yield break;
            // Pop-in: start invisible, fade in with slight scale
            starsText.alpha = 0f;
            yield return new WaitForSeconds(0.3f);
            starsText.alpha = 1f;
        }

        // =====================================================================
        // Vs CPU
        // =====================================================================

        private void RenderVsCpu(RaceResultData r)
        {
            var human = r.HumanResult;
            var opp = r.OpponentResult;
            if (human == null) return;

            bool tie = opp != null && !human.isDnf && !opp.isDnf &&
                       human.swipes == opp.swipes && human.finishTimeMs == opp.finishTimeMs;

            if (titleText != null)
            {
                titleText.fontSize = 72;
                titleText.fontStyle = FontStyles.Bold;
                titleText.characterSpacing = 4;
                if (r.humanDnf)
                {
                    titleText.text = "DID NOT FINISH";
                    titleText.color = dnfGray;
                }
                else if (tie)
                {
                    titleText.text = "TIE!";
                    titleText.color = tieYellow;
                }
                else if (r.humanWon)
                {
                    titleText.text = "VICTORY";
                    titleText.color = victoryGreen;
                }
                else
                {
                    titleText.text = "DEFEAT";
                    titleText.color = defeatRed;
                }
            }

            if (starsText != null)
            {
                starsText.text = StarRating.DisplayString(r.starsEarned);
                starsText.fontSize = 56;
                starsText.color = r.starsEarned >= 3 ? accentCyan : tieYellow;
                starsText.gameObject.SetActive(true);
                StartCoroutine(AnimateStars());
            }

            if (comparisonPanel != null) comparisonPanel.SetActive(true);

            // You column
            if (youNameText != null)
            {
                youNameText.text = "YOU";
                youNameText.color = r.humanWon ? victoryGreen : textOffWhite;
                youNameText.fontSize = 28;
                youNameText.fontStyle = FontStyles.Bold;
            }
            if (youSwipesText != null)
            {
                youSwipesText.text = human.SwipesDisplay;
                youSwipesText.color = r.humanWon ? victoryGreen : textOffWhite;
                youSwipesText.fontSize = 42;
                youSwipesText.fontStyle = FontStyles.Bold;
            }
            if (youTimeText != null)
            {
                youTimeText.text = human.TimeDisplay;
                youTimeText.color = textOffWhite;
                youTimeText.fontSize = 28;
            }

            // Opponent column
            if (opp != null)
            {
                if (opponentNameText != null)
                {
                    opponentNameText.text = r.mode.IsOnline() ? "OPPONENT" : "CPU";
                    opponentNameText.color = !r.humanWon && !opp.isDnf ? defeatRed : dimPlayer;
                    opponentNameText.fontSize = 28;
                    opponentNameText.fontStyle = FontStyles.Bold;
                }
                if (opponentSwipesText != null)
                {
                    opponentSwipesText.text = opp.SwipesDisplay;
                    opponentSwipesText.color = !r.humanWon && !opp.isDnf ? defeatRed : dimPlayer;
                    opponentSwipesText.fontSize = 42;
                    opponentSwipesText.fontStyle = FontStyles.Bold;
                }
                if (opponentTimeText != null)
                {
                    opponentTimeText.text = opp.TimeDisplay;
                    opponentTimeText.color = dimPlayer;
                    opponentTimeText.fontSize = 28;
                }
            }

            if (comparisonSummaryText != null)
            {
                comparisonSummaryText.text = BuildComparisonSummary(r, human, opp, tie);
                comparisonSummaryText.color = r.humanWon ? victoryGreen :
                                               r.humanDnf ? dnfGray : defeatRed;
                comparisonSummaryText.fontSize = 26;
            }
        }

        // =====================================================================
        // Online (stub)
        // =====================================================================

        private void RenderOnline(RaceResultData r)
        {
            RenderVsCpu(r);
            if (subtitleText != null)
            {
                subtitleText.text = "Online mode is a CPU stub in v1. Real netcode in v1.1.";
                subtitleText.color = dimPlayer;
                subtitleText.fontSize = 22;
                subtitleText.gameObject.SetActive(true);
            }
        }

        // =====================================================================
        // Pass & Play ranking
        // =====================================================================

        private void RenderPassPlay(RaceResultData r)
        {
            if (titleText != null)
            {
                titleText.fontSize = 72;
                titleText.fontStyle = FontStyles.Bold;
                titleText.characterSpacing = 4;

                if (r.winnerIndex < 0 || r.winnerIndex >= r.players.Count)
                {
                    titleText.text = "RACE OVER";
                    titleText.color = dimPlayer;
                }
                else
                {
                    var winner = r.players[r.winnerIndex];
                    int rank1Count = 0;
                    foreach (var p in r.players) if (p.rank == 1) rank1Count++;

                    if (rank1Count > 1)
                    {
                        titleText.text = "TIE!";
                        titleText.color = tieYellow;
                    }
                    else
                    {
                        titleText.text = $"{winner.playerName} WINS!";
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
            if (rankingRowsContainer == null) return;

            for (int i = rankingRowsContainer.childCount - 1; i >= 0; i--)
                Destroy(rankingRowsContainer.GetChild(i).gameObject);

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
                var rowGo = rankingRowPrefab != null
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
            rt.sizeDelta = new Vector2(0, 80);
            rt.anchoredPosition = new Vector2(0, -rowIdx * 90);

            var bg = go.AddComponent<Image>();
            bool isWinner = p.rank == 1;
            Color playerColor = p.playerIndex >= 0 && p.playerIndex < PLAYER_COLORS.Length
                ? PLAYER_COLORS[p.playerIndex]
                : textOffWhite;
            bg.color = isWinner
                ? new Color(playerColor.r, playerColor.g, playerColor.b, 0.15f)
                : new Color(playerColor.r, playerColor.g, playerColor.b, 0.05f);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 20, 8, 8);
            hlg.spacing = 16;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;

            // Rank number
            AddRowLabel(go, p.rank > 0 ? $"{p.rank}." : "—", 50,
                isWinner ? victoryGreen : dimPlayer, 36);

            // Color dot
            var dotGo = new GameObject("ColorDot");
            dotGo.transform.SetParent(go.transform, false);
            var dotImg = dotGo.AddComponent<Image>();
            dotImg.color = playerColor;
            dotImg.raycastTarget = false;
            var dotLayout = dotGo.AddComponent<LayoutElement>();
            dotLayout.preferredWidth = 20;
            dotLayout.flexibleWidth = 0;

            // Name + kart
            string nameDisplay = p.playerName;
            if (!string.IsNullOrEmpty(p.kartName)) nameDisplay += $"  [{p.kartName}]";
            if (p.isHuman) nameDisplay += "  [YOU]";
            AddRowLabel(go, nameDisplay, 0,
                isWinner ? victoryGreen : textOffWhite, 28);

            // Swipes
            AddRowLabel(go, p.SwipesDisplay, 120,
                isWinner ? victoryGreen : accentCyan, 32);

            // Time
            AddRowLabel(go, p.TimeDisplay, 140,
                isWinner ? victoryGreen : dimPlayer, 28);

            return go;
        }

        private void PopulateRankingRow(GameObject rowGo, RaceResultData.PlayerResult p, int rowIdx, RaceResultData r)
        {
            var rankText = FindChildTMP(rowGo.transform, "RankText");
            var nameText = FindChildTMP(rowGo.transform, "NameText");
            var swipesText = FindChildTMP(rowGo.transform, "SwipesText");
            var timeText = FindChildTMP(rowGo.transform, "TimeText");

            if (rankText != null) rankText.text = p.rank > 0 ? $"{p.rank}." : "—";
            if (nameText != null) nameText.text = p.playerName + (p.isHuman ? "  [YOU]" : "");
            if (swipesText != null) swipesText.text = p.SwipesDisplay;
            if (timeText != null) timeText.text = p.TimeDisplay;

            var bg = rowGo.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = p.rank == 1
                    ? new Color(0.247f, 0.878f, 0.760f, 0.12f)
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
            if (width > 0) rt.sizeDelta = new Vector2(width, 48);
        }

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
                    return $"You won by {(opp.finishTimeMs - human.finishTimeMs) / 1000f:F2}s — same swipes, faster run!";
                return $"You beat {opp.playerName} by {diff} swipe{(diff == 1 ? "" : "s")}.";
            }
            else
            {
                if (diff == 0)
                    return $"{opp.playerName} beat you by {(human.finishTimeMs - opp.finishTimeMs) / 1000f:F2}s.";
                return $"{opp.playerName} beat you by {diff} swipe{(diff == 1 ? "" : "s")}.";
            }
        }

        // =====================================================================
        // Button wiring
        // =====================================================================

        private void WireButtons()
        {
            if (retryButton != null)
            {
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(OnRetryClicked);
                EnsureHoverFx(retryButton);
            }
            if (menuButton != null)
            {
                menuButton.onClick.RemoveAllListeners();
                menuButton.onClick.AddListener(OnMenuClicked);
                EnsureHoverFx(menuButton);
            }
            if (leaderboardButton != null)
            {
                leaderboardButton.onClick.RemoveAllListeners();
                leaderboardButton.onClick.AddListener(OnLeaderboardClicked);
                leaderboardButton.gameObject.SetActive(false);
            }
        }

        private void OnLeaderboardClicked()
        {
            if (Audio.SfxPlayer.Instance != null) Audio.SfxPlayer.Instance.PlayMenuClick();
            if (leaderboardPanel == null) return;
            var r = GameFlowManager.Instance?.lastRaceResults;
            if (r == null) return;
            leaderboardPanel.Show(r.trackId, r.trackDisplayName);
        }

        private void OnRetryClicked()
        {
            if (Audio.SfxPlayer.Instance != null) Audio.SfxPlayer.Instance.PlayMenuClick();
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.RetryRace();
        }

        private void OnMenuClicked()
        {
            if (Audio.SfxPlayer.Instance != null) Audio.SfxPlayer.Instance.PlayMenuClick();
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.GoToTrackSelect();
        }

        private static void EnsureHoverFx(Button btn)
        {
            if (btn == null) return;
            if (btn.GetComponent<ButtonHoverFx>() == null) btn.gameObject.AddComponent<ButtonHoverFx>();
        }

        // =====================================================================
        // Legacy fallback
        // =====================================================================

        private void RenderLegacy(GameFlowManager flow)
        {
            if (titleText != null)
            {
                titleText.text = "Race Complete!";
                titleText.color = accentCyan;
                titleText.fontSize = 64;
                titleText.fontStyle = FontStyles.Bold;
            }
            if (swipesValueText != null)
            {
                swipesValueText.text = $"<size=36>SWIPES</size>\n<size=48><b>{flow.lastRaceSwipes}</b></size>";
                swipesValueText.color = textOffWhite;
                swipesValueText.gameObject.SetActive(true);
            }
            if (timeValueText != null)
            {
                timeValueText.text = $"<size=36>TIME</size>\n<size=48><b>{flow.lastRaceTimeMs / 1000f:F2}s</b></size>";
                timeValueText.color = textOffWhite;
                timeValueText.gameObject.SetActive(true);
            }
            if (bestValueText != null)
            {
                bestValueText.text = flow.lastRaceIsNewBest
                    ? $"<size=28>PREVIOUS BEST</size>\n<size=36>{flow.lastRaceBestSwipes} swipes · {flow.lastRaceBestTimeMs / 1000f:F2}s</size>"
                    : $"<size=28>BEST</size>\n<size=36>{flow.lastRaceBestSwipes} swipes · {flow.lastRaceBestTimeMs / 1000f:F2}s</size>";
                bestValueText.color = accentCyan;
                bestValueText.gameObject.SetActive(true);
            }
            if (newBestText != null && flow.lastRaceIsNewBest)
            {
                newBestText.text = "★ NEW BEST! ★";
                newBestText.color = accentCyan;
                newBestText.fontSize = 32;
                newBestText.fontStyle = FontStyles.Bold;
                newBestText.gameObject.SetActive(true);
            }
        }
    }
}
