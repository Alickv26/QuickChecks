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

        [Header("Kart Detail Panel (shown on kart click)")]
        [Tooltip("Modal panel shown when a kart is tapped. Contains the kart's full context: name, archetype, description, stats with explanations, and a 'Race with this kart' button.")]
        [SerializeField] private GameObject kartDetailPanel;
        [SerializeField] private TMP_Text detailKartName;
        [SerializeField] private TMP_Text detailArchetype;
        [SerializeField] private TMP_Text detailDescription;
        [SerializeField] private TMP_Text detailPlaystyle;
        [SerializeField] private Transform detailStatsContainer;
        [SerializeField] private Button detailRaceButton;
        [SerializeField] private TMP_Text detailRaceButtonText;
        [SerializeField] private Button detailCloseButton;

        private KartStats _selectedKart;
        private TrackDefinition _selectedTrack;

        // P&P multi-player selection state
        private int _currentPlayerIndex = 0;  // 0-based
        private int _totalPlayers = 1;
        private bool _isMultiKartMode = false;
        private System.Collections.Generic.List<KartStats> _playerKarts = new();

        // The kart the user is currently viewing in the detail panel (set on click).
        // Becomes the selected kart only if they tap the 'Race with this kart' button.
        private KartStats _pendingKart;

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

            // Kart detail panel — hidden by default, shown on kart click
            if (kartDetailPanel != null)
            {
                kartDetailPanel.SetActive(false);
                if (detailRaceButton != null)
                {
                    detailRaceButton.onClick.RemoveAllListeners();
                    detailRaceButton.onClick.AddListener(OnDetailRaceClicked);
                    EnsureHoverFx(detailRaceButton);
                }
                if (detailCloseButton != null)
                {
                    detailCloseButton.onClick.RemoveAllListeners();
                    detailCloseButton.onClick.AddListener(() =>
                    {
                        if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                        HideKartDetail();
                    });
                    EnsureHoverFx(detailCloseButton);
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

            // Stats row — horizontal progress bars for Power / Control / Boost
            var statsContainer = new GameObject("StatBars");
            statsContainer.transform.SetParent(textColGo.transform, false);
            var statsVlg = statsContainer.AddComponent<VerticalLayoutGroup>();
            statsVlg.spacing = 6;
            statsVlg.childAlignment = TextAnchor.UpperLeft;
            statsVlg.childControlWidth = true;
            statsVlg.childControlHeight = false;
            statsVlg.childForceExpandWidth = true;
            statsVlg.childForceExpandHeight = false;
            var statsLayout = statsContainer.AddComponent<LayoutElement>();
            statsLayout.preferredHeight = 80;

            CreateStatBar(statsContainer.transform, "Power", kart.PowerRating, unlocked ? new Color(1f, 0.42f, 0.42f) : Phase2UiPalette.TextDim, unlocked);
            CreateStatBar(statsContainer.transform, "Control", kart.ControlRating, unlocked ? new Color(0.247f, 0.878f, 0.760f) : Phase2UiPalette.TextDim, unlocked);
            CreateStatBar(statsContainer.transform, "Boost", kart.BoostRating, unlocked ? new Color(1f, 0.82f, 0.4f) : Phase2UiPalette.TextDim, unlocked);

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
            // TEMP: all karts unlocked for beta testing
            return true;
        }

        private void OnKartClicked(KartStats kart)
        {
            if (kart == null) return;
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

            // Show the kart detail panel instead of selecting immediately.
            // User confirms selection via the panel's 'Race with this kart' button.
            ShowKartDetail(kart);
        }

        // =====================================================================
        // Kart detail panel
        // =====================================================================

        private void ShowKartDetail(KartStats kart)
        {
            _pendingKart = kart;
            if (kartDetailPanel == null)
            {
                // No panel wired — fall back to old behavior (select immediately)
                Debug.LogWarning("[KartSelect] kartDetailPanel not wired — selecting kart directly.");
                ConfirmKartSelection(kart);
                return;
            }

            // Header: name + archetype
            if (detailKartName != null)
            {
                detailKartName.text = kart.displayName;
                detailKartName.color = Phase2UiPalette.TextPrimary;
            }
            if (detailArchetype != null)
            {
                detailArchetype.text = kart.archetypeLabel.ToUpperInvariant();
                detailArchetype.color = kart.kartColor;
            }
            if (detailDescription != null)
            {
                detailDescription.text = kart.description;
                detailDescription.color = Phase2UiPalette.TextPrimary;
            }

            // Playstyle summary — human-readable explanation of what this kart is good at,
            // derived from its stats. Helps the user understand the numeric stats below.
            if (detailPlaystyle != null)
            {
                detailPlaystyle.text = BuildPlaystyleSummary(kart);
                detailPlaystyle.color = new Color(0.247f, 0.878f, 0.760f, 1f);
            }

            // Stats list — labeled with what each stat MEANS (not just the raw number)
            if (detailStatsContainer != null)
            {
                // Clear old stat rows
                for (int i = detailStatsContainer.childCount - 1; i >= 0; i--)
                    Destroy(detailStatsContainer.GetChild(i).gameObject);

                AddDetailStatRow(detailStatsContainer, "Power",
                    "How hard each swipe pushes the kart. Higher = faster off the line.",
                    $"{kart.impulseMultiplier:F2}×", kart.PowerRating,
                    new Color(1f, 0.42f, 0.42f), kart.kartColor);

                AddDetailStatRow(detailStatsContainer, "Control",
                    "How quickly the kart stops after a swipe. Higher = stops where you aim.",
                    $"{kart.frictionPerSecond:F2} / sec", kart.ControlRating,
                    new Color(0.247f, 0.878f, 0.760f), kart.kartColor);

                AddDetailStatRow(detailStatsContainer, "Boost",
                    "Multiplier + duration of the Boost power-up effect.",
                    $"{kart.boostMultiplier:F1}× for {kart.boostDurationSec:F1}s", kart.BoostRating,
                    new Color(1f, 0.82f, 0.4f), kart.kartColor);

                AddDetailStatRow(detailStatsContainer, "Top Speed",
                    "Hard cap on velocity. Reach it with a big swipe + boost.",
                    $"{kart.maxSpeed:F0} u/s", -1,  // no star rating for this one
                    new Color(0.725f, 0.514f, 1f), kart.kartColor);
            }

            // Race button label includes the kart name so it's clear what you're starting
            if (detailRaceButtonText != null)
            {
                detailRaceButtonText.text = _isMultiKartMode
                    ? $"CONFIRM FOR PLAYER {_currentPlayerIndex + 1}"
                    : $"RACE WITH {kart.displayName.ToUpperInvariant()}";
            }

            kartDetailPanel.SetActive(true);
        }

        private void HideKartDetail()
        {
            if (kartDetailPanel != null) kartDetailPanel.SetActive(false);
            _pendingKart = null;
        }

        /// <summary>Called when the user taps 'Race with this kart' in the detail panel.</summary>
        private void OnDetailRaceClicked()
        {
            if (_pendingKart == null) return;
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            var kart = _pendingKart;
            HideKartDetail();
            ConfirmKartSelection(kart);
        }

        /// <summary>Commits the kart selection (was the body of OnKartClicked before this refactor).</summary>
        private void ConfirmKartSelection(KartStats kart)
        {
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

        /// <summary>Builds a one-line human-readable summary of the kart's playstyle based on its stats.</summary>
        private static string BuildPlaystyleSummary(KartStats kart)
        {
            // Highest stat wins → that's the kart's strength
            int power = kart.PowerRating;
            int control = kart.ControlRating;
            int boost = kart.BoostRating;
            int maxRating = Mathf.Max(power, control, boost);

            if (maxRating == 0)
                return "Balanced — no standout strengths, no weaknesses.";

            // Multiple stats tied at the top → describe the combo
            var strengths = new System.Collections.Generic.List<string>();
            if (power == maxRating) strengths.Add("big swipes");
            if (control == maxRating) strengths.Add("precise stops");
            if (boost == maxRating) strengths.Add("boost chains");
            string strengthStr = strengths.Count == 1
                ? strengths[0]
                : string.Join(" + ", strengths.ToArray());

            // Speed archetype
            bool isHighSpeed = kart.maxSpeed >= 280;
            bool isLowFriction = kart.frictionPerSecond < 0.18f;  // coasts longer
            string archetype = isHighSpeed
                ? (isLowFriction ? "Glass cannon — fast but unforgiving" : "Speed — high top speed")
                : (isLowFriction ? "Coaster — long coast between swipes" : "All-rounder");

            return $"Best at: {strengthstr}. {archetype}.";
        }

        /// <summary>Adds a labeled stat row to the detail panel: name (left), description (small below name), value (right).</summary>
        private void AddDetailStatRow(Transform parent, string label, string description, string valueStr,
                                       int starRating, Color barColor, Color accentColor)
        {
            var rowGo = new GameObject($"DetailStat_{label}");
            rowGo.transform.SetParent(parent, false);
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;

            // Left: label + description (stacked vertically)
            var labelColGo = new GameObject("LabelCol");
            labelColGo.transform.SetParent(rowGo.transform, false);
            var labelVlg = labelColGo.AddComponent<VerticalLayoutGroup>();
            labelVlg.spacing = 2;
            labelVlg.childAlignment = TextAnchor.UpperLeft;
            labelVlg.childControlWidth = true;
            labelVlg.childControlHeight = false;
            labelVlg.childForceExpandWidth = true;
            labelVlg.childForceExpandHeight = false;
            var labelColLayout = labelColGo.AddComponent<LayoutElement>();
            labelColLayout.flexibleWidth = 1;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(labelColGo.transform, false);
            var labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 26;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = accentColor;
            labelText.raycastTarget = false;

            var descGo = new GameObject("Description");
            descGo.transform.SetParent(labelColGo.transform, false);
            var descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.text = description;
            descText.fontSize = 18;
            descText.alignment = TextAlignmentOptions.Left;
            descText.color = new Color(0.7f, 0.7f, 0.7f, 0.9f);
            descText.enableWordWrapping = true;
            descText.raycastTarget = false;
            var descLayout = descGo.AddComponent<LayoutElement>();
            descLayout.preferredHeight = 40;

            // Right: value text + optional stars
            var valColGo = new GameObject("ValueCol");
            valColGo.transform.SetParent(rowGo.transform, false);
            var valVlg = valColGo.AddComponent<VerticalLayoutGroup>();
            valVlg.spacing = 2;
            valVlg.childAlignment = TextAnchor.MiddleRight;
            valVlg.childControlWidth = false;
            valVlg.childControlHeight = false;
            valVlg.childForceExpandWidth = false;
            valVlg.childForceExpandHeight = false;
            var valColLayout = valColGo.AddComponent<LayoutElement>();
            valColLayout.preferredWidth = 220;
            valColLayout.flexibleWidth = 0;

            var valGo = new GameObject("Value");
            valGo.transform.SetParent(valColGo.transform, false);
            var valText = valGo.AddComponent<TextMeshProUGUI>();
            valText.text = valueStr;
            valText.fontSize = 22;
            valText.alignment = TextAlignmentOptions.Right;
            valText.color = barColor;
            valText.raycastTarget = false;

            // Star rating (if applicable)
            if (starRating >= 0)
            {
                var starsGo = new GameObject("Stars");
                starsGo.transform.SetParent(valColGo.transform, false);
                var starsText = starsGo.AddComponent<TextMeshProUGUI>();
                // Filled stars = rating, empty = 3-rating. Use Unicode stars (U+2605/2606).
                starsText.text = new string('★', starRating) + new string('☆', 3 - starRating);
                starsText.fontSize = 24;
                starsText.alignment = TextAlignmentOptions.Right;
                starsText.color = barColor;
                starsText.raycastTarget = false;
            }
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

        /// <summary>Creates a labeled horizontal progress bar (0-3 rating) for a kart stat.</summary>
        private void CreateStatBar(Transform parent, string label, int rating, Color barColor, bool unlocked)
        {
            var rowGo = new GameObject($"Stat_{label}");
            rowGo.transform.SetParent(parent, false);
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;

            // Label (left)
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowGo.transform, false);
            var labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 18;
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = unlocked ? Phase2UiPalette.TextPrimary : Phase2UiPalette.TextDim;
            labelText.raycastTarget = false;
            var labelLayout = labelGo.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 80;
            labelLayout.flexibleWidth = 0;

            // Bar background (dark)
            var barBgGo = new GameObject("BarBg");
            barBgGo.transform.SetParent(rowGo.transform, false);
            var barBgImg = barBgGo.AddComponent<Image>();
            barBgImg.color = new Color(0.05f, 0.07f, 0.07f, 1f);
            barBgImg.raycastTarget = false;
            var barBgRect = barBgGo.GetComponent<RectTransform>();
            barBgRect.sizeDelta = new Vector2(0, 12);

            // Fill (colored, width = rating/3)
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barBgGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = unlocked ? barColor : Phase2UiPalette.TextDim;
            fillImg.raycastTarget = false;
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(rating / 3f, 1f);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // Value text (right)
            var valGo = new GameObject("Value");
            valGo.transform.SetParent(rowGo.transform, false);
            var valText = valGo.AddComponent<TextMeshProUGUI>();
            valText.text = $"{rating}/3";
            valText.fontSize = 16;
            valText.alignment = TextAlignmentOptions.Right;
            valText.color = unlocked ? barColor : Phase2UiPalette.TextDim;
            valText.raycastTarget = false;
            var valLayout = valGo.AddComponent<LayoutElement>();
            valLayout.preferredWidth = 40;
            valLayout.flexibleWidth = 0;
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
