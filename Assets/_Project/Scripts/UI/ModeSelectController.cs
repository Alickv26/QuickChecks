using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Audio;

namespace QuickChecks.UI
{
    /// <summary>
    /// Mode select controller (Phase 2). Renders neon-styled mode cards grouped by category.
    ///
    /// Layout (built at runtime via CreateRuntimeModeCard):
    ///   SOLO PRACTICE       (cyan accent)    — single card spanning full width
    ///       "Race alone + your personal best ghost"
    ///
    ///   VS CPU               (3 cards side-by-side: green / yellow / red)
    ///       Easy / Medium / Hard
    ///
    ///   PASS & PLAY          (3 cards side-by-side: blue)
    ///       2P / 3P / 4P
    ///
    ///   ONLINE               (single card, purple, with "STUB" tag)
    ///       "Live opponent (CPU stub in v1 — real netcode in v1.1)"
    ///
    /// Each card has:
    ///   - Mode name (large, on accent color background)
    ///   - Player count chip (small, top-right of card)
    ///   - Description (small, off-white)
    ///   - Neon hover FX (ButtonHoverFx auto-attached)
    /// </summary>
    public class ModeSelectController : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text modeSummaryText;  // optional: shows currently hovered mode's full description
        [SerializeField] private TMP_Text statusText;  // optional: shows transient messages (e.g. "Online coming soon")

        [Header("Containers (optional — built at runtime if null)")]
        [SerializeField] private Transform soloContainer;
        [SerializeField] private Transform cpuContainer;
        [SerializeField] private Transform passPlayContainer;
        [SerializeField] private Transform onlineContainer;

        [Header("Category labels (optional)")]
        [SerializeField] private TMP_Text soloLabel;
        [SerializeField] private TMP_Text cpuLabel;
        [SerializeField] private TMP_Text passPlayLabel;
        [SerializeField] private TMP_Text onlineLabel;

        private void Start()
        {
            if (titleText != null) titleText.text = "Select Mode";

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
                EnsureHoverFx(backButton);
            }

            // Wire up category labels (visible if assigned in scene)
            if (soloLabel != null) soloLabel.text = "SOLO";
            if (cpuLabel != null) cpuLabel.text = "VS CPU";
            if (passPlayLabel != null) passPlayLabel.text = "PASS & PLAY";
            if (onlineLabel != null) onlineLabel.text = "ONLINE";

            // Build mode cards
            BuildModeCards();

            Debug.Log("[ModeSelect] Ready. Select a game mode.");
        }

        private void BuildModeCards()
        {
            // SOLO
            BuildCard(soloContainer, "SoloButton", RaceMode.Solo);

            // VS CPU (Easy / Medium / Hard)
            BuildCard(cpuContainer, "VsCpuEasyButton",   RaceMode.VsCpuEasy);
            BuildCard(cpuContainer, "VsCpuMediumButton", RaceMode.VsCpuMedium);
            BuildCard(cpuContainer, "VsCpuHardButton",   RaceMode.VsCpuHard);

            // PASS & PLAY
            BuildCard(passPlayContainer, "PassPlay2Button", RaceMode.PassPlay2);
            BuildCard(passPlayContainer, "PassPlay3Button", RaceMode.PassPlay3);
            BuildCard(passPlayContainer, "PassPlay4Button", RaceMode.PassPlay4);

            // ONLINE
            BuildCard(onlineContainer, "OnlineButton", RaceMode.Online);
        }

        private void BuildCard(Transform container, string buttonName, RaceMode mode)
        {
            if (container == null)
            {
                Debug.LogWarning($"[ModeSelect] Container missing for {buttonName} — card not built.");
                return;
            }

            var cardGo = CreateRuntimeModeCard(buttonName, mode);
            cardGo.transform.SetParent(container, false);

            var btn = cardGo.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnModeSelected(mode));
                EnsureHoverFx(btn);
            }
        }

        private GameObject CreateRuntimeModeCard(string name, RaceMode mode)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 140);

            var img = go.AddComponent<Image>();
            Color accent = Phase2UiPalette.ModeAccent(mode);
            // Card face: dim version of accent (so text on accent stays readable)
            img.color = new Color(accent.r * 0.18f, accent.g * 0.18f, accent.b * 0.18f, 1f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;

            // Horizontal layout: text fills, chip on right
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 16, 12, 12);
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;

            // Left accent strip (4px wide vertical bar in mode color)
            var stripGo = new GameObject("AccentStrip");
            stripGo.transform.SetParent(go.transform, false);
            var stripImg = stripGo.AddComponent<Image>();
            stripImg.color = accent;
            var stripLayout = stripGo.AddComponent<LayoutElement>();
            stripLayout.preferredWidth = 6;
            stripLayout.flexibleWidth = 0;
            var stripRect = stripGo.GetComponent<RectTransform>();
            stripRect.sizeDelta = new Vector2(6, 100);

            // Text column (mode name + description)
            var textColGo = new GameObject("TextColumn");
            textColGo.transform.SetParent(go.transform, false);
            var textColRect = textColGo.AddComponent<RectTransform>();
            var vlg = textColGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var textColLayout = textColGo.AddComponent<LayoutElement>();
            textColLayout.flexibleWidth = 1;

            // Mode name
            var nameGo = new GameObject("ModeName");
            nameGo.transform.SetParent(textColGo.transform, false);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.text = mode.DisplayName();
            nameText.fontSize = 32;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = accent;
            nameText.raycastTarget = false;

            // Description
            var descGo = new GameObject("Description");
            descGo.transform.SetParent(textColGo.transform, false);
            var descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.text = Phase2UiPalette.ModeDescription(mode);
            descText.fontSize = 22;
            descText.alignment = TextAlignmentOptions.Left;
            descText.color = Phase2UiPalette.TextPrimary;
            descText.raycastTarget = false;

            // Player count chip (top-right)
            var chipGo = new GameObject("PlayerChip");
            chipGo.transform.SetParent(go.transform, false);
            var chipImg = chipGo.AddComponent<Image>();
            chipImg.color = new Color(accent.r, accent.g, accent.b, 0.25f);
            var chipLayout = chipGo.AddComponent<LayoutElement>();
            chipLayout.preferredWidth = 80;
            chipLayout.flexibleWidth = 0;

            var chipLabelGo = new GameObject("Label");
            chipLabelGo.transform.SetParent(chipGo.transform, false);
            var chipLabel = chipLabelGo.AddComponent<TextMeshProUGUI>();
            chipLabel.text = $"{mode.PlayerCount()}P";
            chipLabel.fontSize = 28;
            chipLabel.alignment = TextAlignmentOptions.Center;
            chipLabel.color = accent;
            chipLabel.raycastTarget = false;
            var chipLabelRect = chipLabelGo.GetComponent<RectTransform>();
            chipLabelRect.anchorMin = Vector2.zero; chipLabelRect.anchorMax = Vector2.one;
            chipLabelRect.pivot = new Vector2(0.5f, 0.5f);
            chipLabelRect.offsetMin = Vector2.zero; chipLabelRect.offsetMax = Vector2.zero;

            // Online "Coming soon" badge (v1.1)
            if (mode == RaceMode.Online)
            {
                var badgeGo = new GameObject("ComingSoonBadge");
                badgeGo.transform.SetParent(textColGo.transform, false);
                var badgeText = badgeGo.AddComponent<TextMeshProUGUI>();
                badgeText.text = "COMING IN v1.1";
                badgeText.fontSize = 16;
                badgeText.alignment = TextAlignmentOptions.Left;
                badgeText.color = Phase2UiPalette.Online;
                badgeText.raycastTarget = false;
            }

            return go;
        }

        private void OnModeSelected(RaceMode mode)
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            Debug.Log($"[ModeSelect] Mode selected: {mode.DisplayName()} ({mode.PlayerCount()} players)");

            // Online mode is gated for v1.1 — show a coming soon message instead of going to TrackSelect
            if (mode == RaceMode.Online)
            {
                ShowStatus("Online multiplayer is coming in v1.1. Stay tuned!");
                return;
            }

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.selectedMode = mode;
                GameFlowManager.Instance.GoToTrackSelect();
            }
            else
            {
                SceneManager.LoadScene("TrackSelect");
            }
        }

        private void ShowStatus(string message)
        {
            if (statusText == null)
            {
                Debug.Log($"[ModeSelect] (statusText not wired) {message}");
                return;
            }
            statusText.text = message;
            statusText.color = Phase2UiPalette.AccentCyan;
            CancelInvoke(nameof(ClearStatus));
            Invoke(nameof(ClearStatus), 4f);
        }

        private void ClearStatus()
        {
            if (statusText != null) statusText.text = "";
        }

        private void OnBackClicked()
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            Debug.Log("[ModeSelect] BACK clicked — going to main menu.");
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.GoToMainMenu();
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
        }

        private static void EnsureHoverFx(Button btn)
        {
            if (btn == null) return;
            if (btn.GetComponent<ButtonHoverFx>() == null)
            {
                btn.gameObject.AddComponent<ButtonHoverFx>();
            }
        }
    }
}
