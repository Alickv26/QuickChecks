using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Racing;
using QuickChecks.Track;
using QuickChecks.Audio;

namespace QuickChecks.UI
{
    /// <summary>
    /// Kart select controller (Phase 2). Renders neon-styled kart cards in a vertical
    /// scrollable list, similar to TrackSelectController.
    ///
    /// Each card shows:
    ///   - Kart name (large, off-white)
    ///   - Archetype label (small, accent color — e.g. "Glass cannon")
    ///   - Description (small, off-white)
    ///   - 3 stat bars: Power / Control / Boost (each 0-3 stars)
    ///   - State chip: FREE / PREMIUM / LOCKED
    ///
    /// Tapping an unlocked kart → sets GameFlowManager.selectedKart, goes to Race scene.
    /// Tapping a locked paid kart → shows toast.
    /// Tapping a locked progression kart → shows toast ("complete track N to unlock").
    /// </summary>
    public class KartSelectController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform kartGridContainer;
        [SerializeField] private Button backButton;
        [SerializeField] private Button raceButton;          // "START RACE" CTA at bottom
        [SerializeField] private TMP_Text raceButtonText;     // so label can show kart name
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text trackLabel;        // shows selected track

        [Header("Library")]
        [SerializeField] private KartLibrary kartLibrary;

        [Header("Toast (optional — reused from TrackSelect style)")]
        [SerializeField] private GameObject toastDialog;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private Button toastOkButton;

        private KartStats _selectedKart;
        private TrackDefinition _selectedTrack;

        // P&P multi-player selection state
        private int _currentPlayerIndex = 0;  // 0-based
        private int _totalPlayers = 1;
        private bool _isMultiKartMode = false;
        private System.Collections.Generic.List<KartStats> _playerKarts = new();

        private void Start()
        {
            // Detect if this is a Pass & Play multi-kart selection
            if (GameFlowManager.Instance != null)
            {
                _selectedTrack = GameFlowManager.Instance.selectedTrack;
                _selectedKart = GameFlowManager.Instance.selectedKart;
                var mode = GameFlowManager.Instance.selectedMode;
                _isMultiKartMode = mode.IsPassPlay();
                _totalPlayers = mode.PlayerCount();
            }

            UpdateTitleForCurrentPlayer();

            if (trackLabel != null)
            {
                trackLabel.text = _selectedTrack != null
                    ? $"{_selectedTrack.displayName}   ·   Par {_selectedTrack.parSwipeCount}"
                    : "";
                trackLabel.color = Phase2UiPalette.TextDim;
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
                EnsureHoverFx(backButton);
            }

            if (raceButton != null)
            {
                raceButton.onClick.RemoveAllListeners();
                raceButton.onClick.AddListener(OnRaceClicked);
                EnsureHoverFx(raceButton);
            }

            if (toastDialog != null)
            {
                toastDialog.SetActive(false);
                if (toastOkButton != null)
                {
                    toastOkButton.onClick.RemoveAllListeners();
                    toastOkButton.onClick.AddListener(() =>
                    {
                        if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                        toastDialog.SetActive(false);
                    });
                    EnsureHoverFx(toastOkButton);
                }
            }

            BuildKartGrid();
            UpdateRaceButton();
        }

        private void BuildKartGrid()
        {
            if (kartGridContainer == null)
            {
                Debug.LogError("[KartSelect] kartGridContainer is null!");
                return;
            }

            // Clear existing cards
            for (int i = kartGridContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(kartGridContainer.GetChild(i).gameObject);
            }

            KartStats[] karts = GetKarts();
            if (karts == null || karts.Length == 0)
            {
                Debug.LogWarning("[KartSelect] No karts found in library.");
                return;
            }

            for (int i = 0; i < karts.Length; i++)
            {
                if (karts[i] == null) continue;
                CreateKartCard(karts[i], i);
            }

            Debug.Log($"[KartSelect] Built grid with {karts.Length} kart cards.");
        }

        private KartStats[] GetKarts()
        {
            if (kartLibrary != null && kartLibrary.karts != null && kartLibrary.karts.Length > 0)
            {
                return kartLibrary.karts;
            }

            // Fallback: load from Resources/KartLibrary if available
            var lib = Resources.Load<KartLibrary>("KartLibrary");
            if (lib != null) return lib.karts;

            // Last-resort fallback: create a single Starter kart in code (shouldn't happen
            // if KartLibrary.asset is properly wired in the scene)
            Debug.LogWarning("[KartSelect] KartLibrary not assigned — using TrackFactory.GetDefaultKart() as fallback.");
            return new KartStats[] { TrackFactory.GetDefaultKart() };
        }

        private void CreateKartCard(KartStats kart, int index)
        {
            var cardGo = CreateRuntimeKartCard(kart, index);
            cardGo.transform.SetParent(kartGridContainer, false);
        }

        private GameObject CreateRuntimeKartCard(KartStats kart, int index)
        {
            bool unlocked = IsKartUnlocked(kart);
            bool isSelected = _selectedKart != null && _selectedKart.kartId == kart.kartId;
            Color accent = kart.kartColor;

            var go = new GameObject($"KartCard_{index}");
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 220);

            var img = go.AddComponent<Image>();
            img.color = unlocked ? Phase2UiPalette.CardSurface : Phase2UiPalette.CardLocked;
            if (isSelected)
            {
                // Selected card gets a brighter accent-tinted bg
                img.color = new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.25f, 1f);
            }

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.interactable = unlocked;

            // Horizontal layout: accent strip | text column | chip
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 20, 16, 16);
            hlg.spacing = 16;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;

            // Accent strip
            var stripGo = new GameObject("AccentStrip");
            stripGo.transform.SetParent(go.transform, false);
            var stripImg = stripGo.AddComponent<Image>();
            stripImg.color = unlocked ? accent : Phase2UiPalette.TextDim;
            var stripLayout = stripGo.AddComponent<LayoutElement>();
            stripLayout.preferredWidth = 6;
            stripLayout.flexibleWidth = 0;

            // Text column
            var textColGo = new GameObject("TextColumn");
            textColGo.transform.SetParent(go.transform, false);
            var vlg = textColGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var textColLayout = textColGo.AddComponent<LayoutElement>();
            textColLayout.flexibleWidth = 1;

            // Name + archetype (same row)
            var nameRowGo = new GameObject("NameRow");
            nameRowGo.transform.SetParent(textColGo.transform, false);
            var nameHlg = nameRowGo.AddComponent<HorizontalLayoutGroup>();
            nameHlg.spacing = 12;
            nameHlg.childAlignment = TextAnchor.MiddleLeft;
            nameHlg.childControlWidth = false;
            nameHlg.childControlHeight = false;
            nameHlg.childForceExpandWidth = false;
            nameHlg.childForceExpandHeight = false;

            var nameGo = new GameObject("KartName");
            nameGo.transform.SetParent(nameRowGo.transform, false);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.text = kart.displayName;
            nameText.fontSize = 36;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = unlocked ? Phase2UiPalette.TextPrimary : Phase2UiPalette.TextDim;
            nameText.raycastTarget = false;
            var nameLayout = nameGo.AddComponent<LayoutElement>();
            nameLayout.preferredWidth = 280;

            var archetypeGo = new GameObject("Archetype");
            archetypeGo.transform.SetParent(nameRowGo.transform, false);
            var archetypeText = archetypeGo.AddComponent<TextMeshProUGUI>();
            archetypeText.text = kart.archetypeLabel.ToUpperInvariant();
            archetypeText.fontSize = 20;
            archetypeText.alignment = TextAlignmentOptions.Left;
            archetypeText.color = unlocked ? accent : Phase2UiPalette.TextDim;
            archetypeText.raycastTarget = false;
            var archetypeLayout = archetypeGo.AddComponent<LayoutElement>();
            archetypeLayout.preferredWidth = 200;

            // Description
            var descGo = new GameObject("Description");
            descGo.transform.SetParent(textColGo.transform, false);
            var descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.text = kart.description;
            descText.fontSize = 22;
            descText.alignment = TextAlignmentOptions.Left;
            descText.color = unlocked ? Phase2UiPalette.TextPrimary : Phase2UiPalette.TextDim;
            descText.enableWordWrapping = true;
            descText.raycastTarget = false;
            var descLayout = descGo.AddComponent<LayoutElement>();
            descLayout.preferredHeight = 50;

            // Stats row (Power / Control / Boost with stars)
            var statsGo = new GameObject("StatsRow");
            statsGo.transform.SetParent(textColGo.transform, false);
            var statsText = statsGo.AddComponent<TextMeshProUGUI>();
            statsText.text = $"Power {Phase2UiPalette.StarsDisplay(kart.PowerRating)}    " +
                             $"Control {Phase2UiPalette.StarsDisplay(kart.ControlRating)}    " +
                             $"Boost {Phase2UiPalette.StarsDisplay(kart.BoostRating)}";
            statsText.fontSize = 22;
            statsText.alignment = TextAlignmentOptions.Left;
            statsText.color = unlocked ? Phase2UiPalette.StarsFilled : Phase2UiPalette.TextDim;
            statsText.raycastTarget = false;

            // Numeric stats row
            var numsGo = new GameObject("NumericStats");
            numsGo.transform.SetParent(textColGo.transform, false);
            var numsText = numsGo.AddComponent<TextMeshProUGUI>();
            numsText.text = $"Impulse {kart.impulseMultiplier:F2}   ·   " +
                            $"Friction {kart.frictionPerSecond:F2}   ·   " +
                            $"Max {kart.maxSpeed:F0}   ·   " +
                            $"Boost {kart.boostMultiplier:F1}× {kart.boostDurationSec:F1}s";
            numsText.fontSize = 18;
            numsText.alignment = TextAlignmentOptions.Left;
            numsText.color = Phase2UiPalette.TextDim;
            numsText.raycastTarget = false;

            // Right-side chip
            var chipGo = new GameObject("StateChip");
            chipGo.transform.SetParent(go.transform, false);
            var chipImg = chipGo.AddComponent<Image>();
            Color chipColor = !unlocked ? new Color(1f, 0.42f, 0.42f, 0.20f) :
                              kart.isFree  ? new Color(accent.r, accent.g, accent.b, 0.20f) :
                                              new Color(1f, 0.82f, 0.40f, 0.20f);
            chipImg.color = chipColor;
            var chipLayout = chipGo.AddComponent<LayoutElement>();
            chipLayout.preferredWidth = 120;
            chipLayout.flexibleWidth = 0;

            var chipLabelGo = new GameObject("Label");
            chipLabelGo.transform.SetParent(chipGo.transform, false);
            var chipLabel = chipLabelGo.AddComponent<TextMeshProUGUI>();
            string chipTextStr = !unlocked ? "LOCKED" :
                                  kart.isFree  ? "FREE" :
                                  isSelected   ? "SELECTED" : "PREMIUM";
            chipLabel.text = chipTextStr;
            chipLabel.fontSize = 20;
            chipLabel.alignment = TextAlignmentOptions.Center;
            chipLabel.color = !unlocked ? new Color(1f, 0.42f, 0.42f) :
                              kart.isFree  ? accent : new Color(1f, 0.82f, 0.40f);
            chipLabel.raycastTarget = false;
            var chipLabelRect = chipLabelGo.GetComponent<RectTransform>();
            chipLabelRect.anchorMin = Vector2.zero; chipLabelRect.anchorMax = Vector2.one;
            chipLabelRect.pivot = new Vector2(0.5f, 0.5f);
            chipLabelRect.offsetMin = Vector2.zero; chipLabelRect.offsetMax = Vector2.zero;

            // Wire button click
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnKartClicked(kart));

            if (unlocked) EnsureHoverFx(button);

            return go;
        }

        private bool IsKartUnlocked(KartStats kart)
        {
            if (kart == null) return false;
            if (kart.unlockAfterTrackIndex < 0) return true;  // Starter

            // Check progression (highest track index the player has unlocked)
            int highestUnlocked = GameFlowManager.Instance?.profile?.highestUnlockedTrackIndex ?? 0;
            if (highestUnlocked < kart.unlockAfterTrackIndex) return false;

            // Check premium for paid karts
            if (!kart.isFree)
            {
                var em = Monetization.EntitlementManager.Instance;
                if (em == null || !em.IsPremium) return false;
            }

            return true;
        }

        private void OnKartClicked(KartStats kart)
        {
            bool unlocked = IsKartUnlocked(kart);

            if (!unlocked)
            {
                if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();

                int playerHighest = GameFlowManager.Instance?.profile?.highestUnlockedTrackIndex ?? 0;
                if (!kart.isFree && playerHighest >= kart.unlockAfterTrackIndex)
                {
                    ShowToast($"Unlock all karts for $4.99?");
                }
                else
                {
                    ShowToast($"Complete Track {kart.unlockAfterTrackIndex + 1} " +
                              $"to unlock {kart.displayName}.");
                }
                return;
            }

            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            Debug.Log($"[KartSelect] Selected kart: {kart.displayName} ({kart.kartId})");

            if (_isMultiKartMode)
            {
                // P&P: add this kart to the list for the current player
                while (_playerKarts.Count <= _currentPlayerIndex)
                    _playerKarts.Add(null);
                _playerKarts[_currentPlayerIndex] = kart;

                if (_currentPlayerIndex < _totalPlayers - 1)
                {
                    // More players to select — show pass overlay
                    _currentPlayerIndex++;
                    ShowPassOverlay();
                    return;
                }
                else
                {
                    // All players have selected — set the list on GameFlowManager + start race
                    if (GameFlowManager.Instance != null)
                    {
                        GameFlowManager.Instance.pendingKartSelections = new System.Collections.Generic.List<KartStats>(_playerKarts);
                        // Also set selectedKart to the first player's pick (for Solo fallback)
                        GameFlowManager.Instance.selectedKart = _playerKarts[0];
                    }
                    StartRace();
                    return;
                }
            }

            // Solo/CPU: single selection
            _selectedKart = kart;
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.selectedKart = kart;
            }

            // Rebuild grid to update "SELECTED" chip
            BuildKartGrid();
            UpdateRaceButton();
        }

        /// <summary>Shows the "Pass to Player N" overlay between P&P kart selections.</summary>
        private void ShowPassOverlay()
        {
            // Reuse the toast dialog as the pass overlay
            if (toastDialog == null)
            {
                // No toast available — just update the title + rebuild grid
                UpdateTitleForCurrentPlayer();
                _selectedKart = null;
                BuildKartGrid();
                UpdateRaceButton();
                return;
            }

            if (toastText != null)
            {
                toastText.text = $"Pass to Player {_currentPlayerIndex + 1}\n\nTap OK when ready.";
                toastText.color = Phase2UiPalette.AccentCyan;
            }
            if (toastOkButton != null)
            {
                toastOkButton.onClick.RemoveAllListeners();
                toastOkButton.onClick.AddListener(() =>
                {
                    if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                    toastDialog.SetActive(false);
                    UpdateTitleForCurrentPlayer();
                    _selectedKart = null;
                    BuildKartGrid();
                    UpdateRaceButton();
                });
            }
            toastDialog.SetActive(true);
        }

        private void UpdateTitleForCurrentPlayer()
        {
            if (titleText == null) return;
            if (_isMultiKartMode)
            {
                titleText.text = $"Player {_currentPlayerIndex + 1}: Select Kart";
            }
            else
            {
                titleText.text = "Select Kart";
            }
            titleText.color = Phase2UiPalette.AccentCyan;
        }

        private void StartRace()
        {
            if (_selectedTrack == null)
            {
                ShowToast("No track selected.");
                return;
            }

            KartStats kart = _isMultiKartMode
                ? _playerKarts[0]
                : _selectedKart;

            if (kart == null)
            {
                ShowToast("Select a kart first.");
                return;
            }

            Debug.Log($"[KartSelect] START RACE — mode={(_isMultiKartMode ? "P&P" : "Solo/CPU")}, " +
                      $"track={_selectedTrack.trackId}, karts={(_isMultiKartMode ? _playerKarts.Count : 1)}");

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.StartRace(_selectedTrack, kart);
            }
            else
            {
                SceneManager.LoadScene("Race");
            }
        }

        private void UpdateRaceButton()
        {
            if (raceButtonText == null) return;
            if (_selectedKart == null)
            {
                raceButtonText.text = "SELECT A KART";
                if (raceButton != null) raceButton.interactable = false;
            }
            else
            {
                raceButtonText.text = $"RACE WITH {_selectedKart.displayName.ToUpperInvariant()}";
                if (raceButton != null) raceButton.interactable = true;
            }
        }

        private void OnRaceClicked()
        {
            if (_isMultiKartMode)
            {
                // P&P mode — race starts automatically after all players pick.
                // If the player taps the button before all have picked, show a hint.
                ShowToast($"Player {_currentPlayerIndex + 1} of {_totalPlayers} — pick a kart first.");
                return;
            }
            StartRace();
        }

        private void OnBackClicked()
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            Debug.Log("[KartSelect] BACK clicked — going to track select.");
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.GoToTrackSelect();
            }
            else
            {
                SceneManager.LoadScene("TrackSelect");
            }
        }

        private void ShowToast(string message)
        {
            if (toastDialog == null)
            {
                Debug.Log($"[KartSelect] (toast missing) {message}");
                return;
            }
            if (toastText != null) toastText.text = message;
            toastDialog.SetActive(true);
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
