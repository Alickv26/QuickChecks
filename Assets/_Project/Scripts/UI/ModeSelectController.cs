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
    /// Swipeable carousel mode select. One large card at a time, swipe left/right to browse.
    /// Tap the card to select that mode.
    ///
    /// Each card shows:
    ///   - Large colored circle (icon) — color-coded per category
    ///   - Mode name (large text)
    ///   - Description (small text)
    ///   - Player count chip (e.g. "1P", "2P")
    ///
    /// Dot indicators at the bottom show which card is active.
    /// Online card is dimmed (60% alpha) + "COMING v1.1" badge.
    /// </summary>
    public class ModeSelectController : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;

        [Header("Card References")]
        [SerializeField] private RectTransform cardContainer;  // parent of the single visible card
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image cardIcon;             // large colored circle
        [SerializeField] private TMP_Text cardNameText;
        [SerializeField] private TMP_Text cardDescText;
        [SerializeField] private TMP_Text cardPlayerCountText;
        [SerializeField] private TMP_Text cardBadgeText;     // "COMING v1.1" for online

        [Header("Dots")]
        [SerializeField] private Transform dotsContainer;

        [Header("Swipe Settings")]
        [SerializeField] private float swipeThreshold = 80f;
        [SerializeField] private float cardTransitionDuration = 0.25f;

        // Mode data (icon color + name + description per mode)
        private struct ModeInfo
        {
            public RaceMode mode;
            public string name;
            public string description;
            public Color color;
            public string playerCount;
            public bool isLocked;
        }

        private ModeInfo[] _modes;
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

            BuildModeData();
            BuildDots();
            ShowMode(0, animate: false);

            Debug.Log("[ModeSelect] Ready. Swipe to browse, tap to select.");
        }

        private void BuildModeData()
        {
            _modes = new ModeInfo[]
            {
                new ModeInfo
                {
                    mode = RaceMode.Solo,
                    name = "Solo Practice",
                    description = "Race alone against your personal best ghost.\nFewest swipes wins.",
                    color = new Color(0.247f, 0.878f, 0.760f),  // cyan
                    playerCount = "1P",
                    isLocked = false
                },
                new ModeInfo
                {
                    mode = RaceMode.VsCpuEasy,
                    name = "vs CPU — Easy",
                    description = "Relaxed AI. Generous aim, low pressure.\nGreat for learning tracks.",
                    color = new Color(0.42f, 0.79f, 0.47f),  // green
                    playerCount = "1v1",
                    isLocked = false
                },
                new ModeInfo
                {
                    mode = RaceMode.VsCpuMedium,
                    name = "vs CPU — Medium",
                    description = "Sharp AI. Close to a real player.\nTests your precision.",
                    color = new Color(1f, 0.82f, 0.4f),  // yellow
                    playerCount = "1v1",
                    isLocked = false
                },
                new ModeInfo
                {
                    mode = RaceMode.VsCpuHard,
                    name = "vs CPU — Hard",
                    description = "Ruthless AI. Near-perfect aim.\nPunishes every mistake.",
                    color = new Color(1f, 0.42f, 0.42f),  // red
                    playerCount = "1v1",
                    isLocked = false
                },
                new ModeInfo
                {
                    mode = RaceMode.PassPlay2,
                    name = "Pass & Play — 2P",
                    description = "Two players, one device.\nTake turns swiping.",
                    color = new Color(0.42f, 0.58f, 1f),  // blue
                    playerCount = "2P",
                    isLocked = false
                },
                new ModeInfo
                {
                    mode = RaceMode.PassPlay3,
                    name = "Pass & Play — 3P",
                    description = "Three players, one device.\nTake turns swiping.",
                    color = new Color(0.42f, 0.58f, 1f),  // blue
                    playerCount = "3P",
                    isLocked = false
                },
                new ModeInfo
                {
                    mode = RaceMode.PassPlay4,
                    name = "Pass & Play — 4P",
                    description = "Four players, one device.\nTake turns swiping.",
                    color = new Color(0.42f, 0.58f, 1f),  // blue
                    playerCount = "4P",
                    isLocked = false
                },
                new ModeInfo
                {
                    mode = RaceMode.Online,
                    name = "Online Match",
                    description = "Real-time turn-based matches\nagainst players worldwide.",
                    color = new Color(0.725f, 0.514f, 1f),  // purple
                    playerCount = "1v1",
                    isLocked = true
                },
            };
        }

        private void BuildDots()
        {
            if (dotsContainer == null) return;
            for (int i = 0; i < dotsContainer.childCount; i++)
                Destroy(dotsContainer.GetChild(i).gameObject);

            for (int i = 0; i < _modes.Length; i++)
            {
                var dotGo = new GameObject($"Dot_{i}");
                dotGo.transform.SetParent(dotsContainer, false);
                var dotImg = dotGo.AddComponent<Image>();
                dotImg.raycastTarget = false;
                var dotLayout = dotGo.AddComponent<LayoutElement>();
                dotLayout.preferredWidth = 12;
                dotLayout.preferredHeight = 12;
                dotLayout.flexibleWidth = 0;
            }
            UpdateDots();
        }

        private void UpdateDots()
        {
            if (dotsContainer == null) return;
            for (int i = 0; i < dotsContainer.childCount && i < _modes.Length; i++)
            {
                var img = dotsContainer.GetChild(i).GetComponent<Image>();
                if (img == null) continue;
                img.color = i == _currentIdx
                    ? new Color(0.247f, 0.878f, 0.760f, 1f)   // cyan active
                    : new Color(0.4f, 0.4f, 0.4f, 0.5f);       // dim inactive
            }
        }

        private void ShowMode(int index, bool animate = true)
        {
            if (_modes == null || index < 0 || index >= _modes.Length) return;
            _currentIdx = index;
            var m = _modes[index];

            // Card background — tinted with the mode's color
            if (cardBackground != null)
            {
                float alpha = m.isLocked ? 0.35f : 0.12f;
                cardBackground.color = new Color(m.color.r, m.color.g, m.color.b, alpha);
            }

            // Icon circle
            if (cardIcon != null)
            {
                cardIcon.color = m.isLocked
                    ? new Color(m.color.r * 0.5f, m.color.g * 0.5f, m.color.b * 0.5f, 0.6f)
                    : m.color;
            }

            // Text
            if (cardNameText != null)
            {
                cardNameText.text = m.name;
                cardNameText.color = m.isLocked ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : Phase2UiPalette.TextPrimary;
            }
            if (cardDescText != null)
            {
                cardDescText.text = m.description;
                cardDescText.color = m.isLocked ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : new Color(0.8f, 0.8f, 0.8f, 0.9f);
            }
            if (cardPlayerCountText != null)
            {
                cardPlayerCountText.text = m.playerCount;
                cardPlayerCountText.color = m.isLocked ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : m.color;
            }

            // Badge (Online only)
            if (cardBadgeText != null)
            {
                cardBadgeText.gameObject.SetActive(m.isLocked);
                if (m.isLocked) cardBadgeText.text = "COMING IN v1.1";
            }

            UpdateDots();

            if (animate)
            {
                StartCoroutine(SlideCardAnimation());
            }
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
                t = 1f - Mathf.Pow(1f - t, 3f);  // ease-out cubic
                cardContainer.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, t);
                yield return null;
            }
            cardContainer.anchoredPosition = endPos;
        }

        // =====================================================================
        // Swipe detection (IPointerDownHandler + IPointerUpHandler)
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

            // Check if it's a swipe (horizontal movement > threshold)
            if (Mathf.Abs(delta.x) > swipeThreshold)
            {
                if (delta.x < 0)
                {
                    // Swipe left → next mode
                    if (_currentIdx < _modes.Length - 1)
                    {
                        ShowMode(_currentIdx + 1);
                        if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                    }
                }
                else
                {
                    // Swipe right → previous mode
                    if (_currentIdx > 0)
                    {
                        ShowMode(_currentIdx - 1);
                        if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                    }
                }
            }
            else
            {
                // Tap (not a swipe) → select this mode
                OnModeSelected();
            }
        }

        private void OnModeSelected()
        {
            var m = _modes[_currentIdx];
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();

            if (m.isLocked)
            {
                ShowStatus("Online multiplayer is coming in v1.1. Stay tuned!");
                return;
            }

            Debug.Log($"[ModeSelect] Mode selected: {m.name} ({m.mode.PlayerCount()} players)");

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.selectedMode = m.mode;
                GameFlowManager.Instance.GoToTrackSelect();
            }
            else
            {
                SceneManager.LoadScene("TrackSelect");
            }
        }

        // Desktop: arrow keys to navigate
        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (_currentIdx < _modes.Length - 1)
                {
                    ShowMode(_currentIdx + 1);
                    if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                }
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (_currentIdx > 0)
                {
                    ShowMode(_currentIdx - 1);
                    if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                }
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                OnModeSelected();
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
