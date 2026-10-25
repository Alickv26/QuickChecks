using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Audio;

namespace QuickChecks.UI
{
    /// <summary>
    /// Swipeable carousel mode select with 4 top-level cards:
    ///   1. Solo Practice
    ///   2. vs CPU → tap to reveal difficulty sub-options (Easy / Medium / Hard)
    ///   3. Local PvP → tap to reveal player count sub-options (2P / 3P / 4P)
    ///   4. Online (coming soon)
    ///
    /// Swipe left/right to browse the 4 cards. Tap to select.
    /// For vs CPU + Local PvP, tapping shows inline sub-option buttons.
    /// Pick a sub-option → goes to Track Select.
    ///
    /// Dot indicators at the bottom show: Solo · vs CPU · Local PvP · Online
    /// </summary>
    public class ModeSelectController : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;

        [Header("Card References")]
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image cardIcon;
        [SerializeField] private TMP_Text cardNameText;
        [SerializeField] private TMP_Text cardDescText;
        [SerializeField] private TMP_Text cardBadgeText;

        [Header("Sub-option panel (shown when tapping vs CPU or Local PvP)")]
        [SerializeField] private GameObject subOptionPanel;
        [SerializeField] private Transform subOptionContainer;

        [Header("Dots")]
        [SerializeField] private Transform dotsContainer;

        [Header("Swipe Settings")]
        [SerializeField] private float swipeThreshold = 80f;
        [SerializeField] private float cardTransitionDuration = 0.25f;

        // --- Mode data ---

        private enum CardType { Solo, VsCpu, LocalPvp, Online }

        private struct CardInfo
        {
            public CardType type;
            public string name;
            public string description;
            public Color color;
            public string dotLabel;
            public bool isLocked;
        }

        private CardInfo[] _cards;
        private int _currentIdx = 0;
        private Vector2 _pointerDownPos;
        private bool _isSwiping = false;

        private void Start()
        {
            if (titleText != null) titleText.text = "Select Mode";

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
                EnsureHoverFx(backButton);
            }

            if (subOptionPanel != null) subOptionPanel.SetActive(false);

            BuildCardData();
            BuildDots();
            ShowCard(0, animate: false);

            Debug.Log("[ModeSelect] Ready. Swipe to browse, tap to select.");
        }

        private void BuildCardData()
        {
            _cards = new CardInfo[]
            {
                new CardInfo
                {
                    type = CardType.Solo,
                    name = "Solo Practice",
                    description = "Race alone against your personal best ghost.\nFewest swipes wins.",
                    color = new Color(0.247f, 0.878f, 0.760f),  // cyan
                    dotLabel = "Solo",
                    isLocked = false
                },
                new CardInfo
                {
                    type = CardType.VsCpu,
                    name = "vs CPU",
                    description = "Race against an AI opponent.\nTap to choose difficulty.",
                    color = new Color(1f, 0.82f, 0.4f),  // yellow
                    dotLabel = "vs CPU",
                    isLocked = false
                },
                new CardInfo
                {
                    type = CardType.LocalPvp,
                    name = "Local PvP",
                    description = "Pass & play on one device.\nTap to choose player count.",
                    color = new Color(0.42f, 0.58f, 1f),  // blue
                    dotLabel = "Local PvP",
                    isLocked = false
                },
                new CardInfo
                {
                    type = CardType.Online,
                    name = "Online Match",
                    description = "Real-time turn-based matches\nagainst players worldwide.",
                    color = new Color(0.725f, 0.514f, 1f),  // purple
                    dotLabel = "Online",
                    isLocked = true
                },
            };
        }

        private void BuildDots()
        {
            if (dotsContainer == null) return;
            for (int i = 0; i < dotsContainer.childCount; i++)
                Destroy(dotsContainer.GetChild(i).gameObject);

            for (int i = 0; i < _cards.Length; i++)
            {
                var dotGo = new GameObject($"Dot_{i}");
                dotGo.transform.SetParent(dotsContainer, false);

                // Dot circle
                var dotImg = dotGo.AddComponent<Image>();
                dotImg.raycastTarget = false;
                var dotLayout = dotGo.AddComponent<LayoutElement>();
                dotLayout.preferredWidth = 10;
                dotLayout.preferredHeight = 10;
                dotLayout.flexibleWidth = 0;

                // Label below the dot
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(dotGo.transform, false);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = _cards[i].dotLabel;
                label.fontSize = 22;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(0.7f, 0.7f, 0.7f, 0.9f);
                label.raycastTarget = false;
                var labelRect = labelGo.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0.5f, 0);
                labelRect.anchorMax = new Vector2(0.5f, 0);
                labelRect.pivot = new Vector2(0.5f, 1);
                labelRect.anchoredPosition = new Vector2(0, -14);
                labelRect.sizeDelta = new Vector2(100, 18);

                // Add a vertical layout so the dot + label stack
                var vlg = dotGo.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 4;
                vlg.childAlignment = TextAnchor.MiddleCenter;
                vlg.childControlWidth = false;
                vlg.childControlHeight = false;
                vlg.padding = new RectOffset(8, 8, 0, 18);
            }
            UpdateDots();
        }

        private void UpdateDots()
        {
            if (dotsContainer == null) return;
            for (int i = 0; i < dotsContainer.childCount && i < _cards.Length; i++)
            {
                var dotTransform = dotsContainer.GetChild(i);
                var dotImg = dotTransform.GetComponent<Image>();
                if (dotImg != null)
                {
                    dotImg.color = i == _currentIdx
                        ? new Color(0.247f, 0.878f, 0.760f, 1f)   // cyan active
                        : new Color(0.4f, 0.4f, 0.4f, 0.4f);       // dim inactive
                }
                // Also color the label
                var label = dotTransform.Find("Label");
                if (label != null)
                {
                    var labelT = label.GetComponent<TextMeshProUGUI>();
                    if (labelT != null)
                    {
                        labelT.color = i == _currentIdx
                            ? new Color(0.247f, 0.878f, 0.760f, 1f)
                            : new Color(0.7f, 0.7f, 0.7f, 0.9f);
                    }
                }
            }
        }

        private void ShowCard(int index, bool animate = true)
        {
            if (_cards == null || index < 0 || index >= _cards.Length) return;
            _currentIdx = index;
            var c = _cards[index];

            // Hide sub-options when switching cards
            if (subOptionPanel != null) subOptionPanel.SetActive(false);

            // Card background
            if (cardBackground != null)
            {
                float alpha = c.isLocked ? 0.35f : 0.12f;
                cardBackground.color = new Color(c.color.r, c.color.g, c.color.b, alpha);
            }

            // Icon
            if (cardIcon != null)
            {
                cardIcon.color = c.isLocked
                    ? new Color(c.color.r * 0.5f, c.color.g * 0.5f, c.color.b * 0.5f, 0.6f)
                    : c.color;
            }

            // Text
            if (cardNameText != null)
            {
                cardNameText.text = c.name;
                cardNameText.color = c.isLocked ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : Phase2UiPalette.TextPrimary;
            }
            if (cardDescText != null)
            {
                cardDescText.text = c.description;
                cardDescText.color = c.isLocked ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : new Color(0.8f, 0.8f, 0.8f, 0.9f);
            }

            // Badge
            if (cardBadgeText != null)
            {
                cardBadgeText.gameObject.SetActive(c.isLocked);
                if (c.isLocked) cardBadgeText.text = "COMING IN v1.1";
            }

            UpdateDots();

            if (animate) StartCoroutine(SlideCardAnimation());
        }

        private IEnumerator SlideCardAnimation()
        {
            if (cardContainer == null) yield break;
            Vector2 startPos = cardContainer.anchoredPosition;
            Vector2 endPos = Vector2.zero;
            float elapsed = 0f;
            while (elapsed < cardTransitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / cardTransitionDuration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                cardContainer.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, t);
                yield return null;
            }
            cardContainer.anchoredPosition = endPos;
        }

        // =====================================================================
        // Swipe + tap detection
        // =====================================================================

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDownPos = eventData.position;
            _isSwiping = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isSwiping) return;
            _isSwiping = false;

            Vector2 delta = eventData.position - _pointerDownPos;

            if (Mathf.Abs(delta.x) > swipeThreshold)
            {
                // Swipe
                if (delta.x < 0 && _currentIdx < _cards.Length - 1)
                {
                    ShowCard(_currentIdx + 1);
                    if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                }
                else if (delta.x > 0 && _currentIdx > 0)
                {
                    ShowCard(_currentIdx - 1);
                    if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                }
            }
            else
            {
                // Tap
                OnCardTapped();
            }
        }

        private void OnCardTapped()
        {
            var c = _cards[_currentIdx];
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();

            if (c.isLocked)
            {
                ShowStatus("Online multiplayer is coming in v1.1. Stay tuned!");
                return;
            }

            switch (c.type)
            {
                case CardType.Solo:
                    SelectMode(RaceMode.Solo);
                    break;
                case CardType.VsCpu:
                    ShowSubOptions("Choose Difficulty", new (string, RaceMode)[]
                    {
                        ("Easy", RaceMode.VsCpuEasy),
                        ("Medium", RaceMode.VsCpuMedium),
                        ("Hard", RaceMode.VsCpuHard),
                    });
                    break;
                case CardType.LocalPvp:
                    ShowSubOptions("Choose Players", new (string, RaceMode)[]
                    {
                        ("2 Players", RaceMode.PassPlay2),
                        ("3 Players", RaceMode.PassPlay3),
                        ("4 Players", RaceMode.PassPlay4),
                    });
                    break;
            }
        }

        // =====================================================================
        // Sub-option panel
        // =====================================================================

        private void ShowSubOptions(string title, (string label, RaceMode mode)[] options)
        {
            if (subOptionPanel == null || subOptionContainer == null)
            {
                // Fallback: just pick the first option
                SelectMode(options[0].mode);
                return;
            }

            // Clear existing sub-option buttons
            for (int i = subOptionContainer.childCount - 1; i >= 0; i--)
                Destroy(subOptionContainer.GetChild(i).gameObject);

            // Build sub-option buttons
            foreach (var opt in options)
            {
                var btnGo = new GameObject($"SubOpt_{opt.label}");
                btnGo.transform.SetParent(subOptionContainer, false);
                var btn = btnGo.AddComponent<Button>();
                var img = btnGo.AddComponent<Image>();
                img.color = new Color(_cards[_currentIdx].color.r, _cards[_currentIdx].color.g, _cards[_currentIdx].color.b, 0.3f);
                img.raycastTarget = true;
                btn.targetGraphic = img;
                var rect = btnGo.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(0, 70);

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(btnGo.transform, false);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = opt.label;
                label.fontSize = 32;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Phase2UiPalette.TextPrimary;
                label.raycastTarget = false;
                var labelRect = labelGo.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;

                if (btn.GetComponent<ButtonHoverFx>() == null)
                    btn.gameObject.AddComponent<ButtonHoverFx>();

                var mode = opt.mode;
                btn.onClick.AddListener(() =>
                {
                    if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                    SelectMode(mode);
                });
            }

            subOptionPanel.SetActive(true);
        }

        // =====================================================================
        // Mode selection
        // =====================================================================

        private void SelectMode(RaceMode mode)
        {
            Debug.Log($"[ModeSelect] Mode selected: {mode.DisplayName()} ({mode.PlayerCount()} players)");

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

        // =====================================================================
        // Desktop keyboard
        // =====================================================================

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_currentIdx < _cards.Length - 1)
                {
                    ShowCard(_currentIdx + 1);
                    if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                }
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (_currentIdx > 0)
                {
                    ShowCard(_currentIdx - 1);
                    if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                }
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                OnCardTapped();
            }
        }

        private void ShowStatus(string message)
        {
            if (statusText == null) return;
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
                btn.gameObject.AddComponent<ButtonHoverFx>();
        }
    }
}
