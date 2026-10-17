using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Track;
using QuickChecks.Racing;
using QuickChecks.Audio;

namespace QuickChecks.UI
{
    /// <summary>
    /// Track select controller (Phase 2). Renders neon-styled track cards.
    ///
    /// Each card shows:
    ///   - Track index (large, accent color)
    ///   - Track name (large, off-white)
    ///   - Difficulty label + stars (EASY ★☆☆, MEDIUM ★★☆, etc.)
    ///   - Par swipes target ("Par 12")
    ///   - Player's best swipes (or "Not completed" if first run)
    ///   - Free / Premium / Locked indicator
    ///   - Hover FX (ButtonHoverFx auto-attached)
    ///
    /// Tapping an unlocked track → starts the race.
    /// Tapping a locked paid track → opens paywall modal (PaywallController).
    /// Tapping a locked progression track → shows "Complete previous track first" toast.
    /// </summary>
    public class TrackSelectController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform trackGridContainer;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text modeLabel;        // shows current mode (e.g. "SOLO PRACTICE")

        [Header("Track Library")]
        [SerializeField] private TrackLibrary trackLibrary;

        [Header("Toast (optional)")]
        [SerializeField] private GameObject toastDialog;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private Button toastOkButton;

        private void Start()
        {
            if (titleText != null) titleText.text = "Select Track";
            if (modeLabel != null)
            {
                var mode = GameFlowManager.Instance != null ? GameFlowManager.Instance.selectedMode : RaceMode.Solo;
                modeLabel.text = mode.DisplayName().ToUpperInvariant();
                modeLabel.color = Phase2UiPalette.ModeAccent(mode);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
                EnsureHoverFx(backButton);
            }

            // Toast dialog wiring
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

            BuildTrackGrid();
        }

        private void BuildTrackGrid()
        {
            if (trackGridContainer == null)
            {
                Debug.LogError("[TrackSelect] trackGridContainer is null!");
                return;
            }

            // Clear existing cards
            for (int i = trackGridContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(trackGridContainer.GetChild(i).gameObject);
            }

            // Get tracks from library (or fallback to runtime-created tracks)
            TrackDefinition[] tracks = GetTracks();
            if (tracks == null || tracks.Length == 0)
            {
                Debug.LogWarning("[TrackSelect] No tracks found in library.");
                return;
            }

            for (int i = 0; i < tracks.Length; i++)
            {
                CreateTrackCard(tracks[i], i);
            }

            Debug.Log($"[TrackSelect] Built grid with {tracks.Length} track cards.");
        }

        private TrackDefinition[] GetTracks()
        {
            if (trackLibrary != null && trackLibrary.tracks != null && trackLibrary.tracks.Length > 0)
            {
                return trackLibrary.tracks;
            }

            // Fallback: generate tracks at runtime via TrackFactory
            Debug.Log("[TrackSelect] No TrackLibrary assigned — generating tracks via TrackFactory.");
            return TrackFactory.GenerateAllTracks();
        }

        private void CreateTrackCard(TrackDefinition track, int index)
        {
            var cardGo = CreateRuntimeTrackCard(track, index);
            cardGo.transform.SetParent(trackGridContainer, false);
        }

        private GameObject CreateRuntimeTrackCard(TrackDefinition track, int index)
        {
            bool unlocked = IsTrackUnlocked(track);
            int bestSwipes = GameFlowManager.Instance?.GetBestSwipesForTrack(track.trackId) ?? 0;
            int stars = bestSwipes > 0 ? StarRating.Calculate(bestSwipes, track.parSwipeCount) : 0;
            Color accent = Phase2UiPalette.AccentCyan;

            var go = new GameObject($"TrackCard_{index}");
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 200);

            var img = go.AddComponent<Image>();
            img.color = unlocked ? Phase2UiPalette.CardSurface : Phase2UiPalette.CardLocked;

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.interactable = unlocked;

            // Horizontal layout: index | text column | chip
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 20, 16, 16);
            hlg.spacing = 16;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;

            // Track index (large)
            var idxGo = new GameObject("TrackIndex");
            idxGo.transform.SetParent(go.transform, false);
            var idxText = idxGo.AddComponent<TextMeshProUGUI>();
            idxText.text = $"{index + 1:D2}";
            idxText.fontSize = 56;
            idxText.alignment = TextAlignmentOptions.Center;
            idxText.color = unlocked ? accent : Phase2UiPalette.TextDim;
            idxText.raycastTarget = false;
            var idxLayout = idxGo.AddComponent<LayoutElement>();
            idxLayout.preferredWidth = 100;
            idxLayout.flexibleWidth = 0;

            // Text column (name + stars + best)
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

            // Track name
            var nameGo = new GameObject("TrackName");
            nameGo.transform.SetParent(textColGo.transform, false);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.text = track.displayName;
            nameText.fontSize = 36;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = unlocked ? Phase2UiPalette.TextPrimary : Phase2UiPalette.TextDim;
            nameText.raycastTarget = false;

            // Stars + difficulty + par
            var starsGo = new GameObject("StarsRow");
            starsGo.transform.SetParent(textColGo.transform, false);
            var starsText = starsGo.AddComponent<TextMeshProUGUI>();
            starsText.text = $"{Phase2UiPalette.StarsDisplay(track.difficultyStars)}   " +
                             $"{Phase2UiPalette.DifficultyLabel(track.difficultyStars)}   " +
                             $"Par {track.parSwipeCount}";
            starsText.fontSize = 24;
            starsText.alignment = TextAlignmentOptions.Left;
            starsText.color = unlocked ? Phase2UiPalette.StarsFilled : Phase2UiPalette.TextDim;
            starsText.raycastTarget = false;

            // Best swipes line
            var bestGo = new GameObject("BestLine");
            bestGo.transform.SetParent(textColGo.transform, false);
            var bestText = bestGo.AddComponent<TextMeshProUGUI>();
            if (unlocked)
            {
                if (bestSwipes > 0)
                {
                    bestText.text = $"Best: {bestSwipes} swipes   {Phase2UiPalette.StarsDisplay(stars)}";
                }
                else
                {
                    bestText.text = "Not completed";
                }
            }
            else
            {
                bestText.text = track.isFree ? "Locked — complete previous track" : "Premium — $4.99 to unlock";
            }
            bestText.fontSize = 22;
            bestText.alignment = TextAlignmentOptions.Left;
            bestText.color = unlocked
                ? (bestSwipes > 0 ? accent : Phase2UiPalette.TextDim)
                : new Color(1f, 0.42f, 0.42f);
            bestText.raycastTarget = false;

            // Right-side chip (Free / Paid / Locked icon)
            var chipGo = new GameObject("StateChip");
            chipGo.transform.SetParent(go.transform, false);
            var chipImg = chipGo.AddComponent<Image>();
            Color chipColor = !unlocked ? new Color(1f, 0.42f, 0.42f, 0.20f) :
                              track.isFree  ? new Color(accent.r, accent.g, accent.b, 0.20f) :
                                              new Color(1f, 0.82f, 0.40f, 0.20f);  // gold for paid-unlocked
            chipImg.color = chipColor;
            var chipLayout = chipGo.AddComponent<LayoutElement>();
            chipLayout.preferredWidth = 120;
            chipLayout.flexibleWidth = 0;

            var chipLabelGo = new GameObject("Label");
            chipLabelGo.transform.SetParent(chipGo.transform, false);
            var chipLabel = chipLabelGo.AddComponent<TextMeshProUGUI>();
            chipLabel.text = !unlocked ? "LOCKED" :
                             track.isFree  ? "FREE" : "PREMIUM";
            chipLabel.fontSize = 22;
            chipLabel.alignment = TextAlignmentOptions.Center;
            chipLabel.color = !unlocked ? new Color(1f, 0.42f, 0.42f) :
                              track.isFree  ? accent : new Color(1f, 0.82f, 0.40f);
            chipLabel.raycastTarget = false;
            var chipLabelRect = chipLabelGo.GetComponent<RectTransform>();
            chipLabelRect.anchorMin = Vector2.zero; chipLabelRect.anchorMax = Vector2.one;
            chipLabelRect.pivot = new Vector2(0.5f, 0.5f);
            chipLabelRect.offsetMin = Vector2.zero; chipLabelRect.offsetMax = Vector2.zero;

            // Wire button
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnTrackClicked(track));

            // Attach hover FX only if interactive
            if (unlocked) EnsureHoverFx(button);

            return go;
        }

        private bool IsTrackUnlocked(TrackDefinition track)
        {
            if (GameFlowManager.Instance != null)
            {
                return GameFlowManager.Instance.IsTrackUnlocked(track);
            }
            return track.isFree;
        }

        private void OnTrackClicked(TrackDefinition track)
        {
            bool unlocked = IsTrackUnlocked(track);

            if (!unlocked)
            {
                if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();

                if (!track.isFree)
                {
                    Debug.Log($"[TrackSelect] Track '{track.displayName}' is locked — show paywall.");
                    ShowToast($"Unlock all tracks for $4.99?");
                    // TODO: Open PaywallController modal (stubbed for now)
                }
                else
                {
                    Debug.Log($"[TrackSelect] Track '{track.displayName}' is locked — complete previous track first.");
                    ShowToast("Complete the previous track to unlock this one.");
                }
                return;
            }

            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            Debug.Log($"[TrackSelect] Track '{track.displayName}' selected — going to kart select.");

            // Store the selected track on GameFlowManager and go to the Kart Select scene.
            // The player picks their kart next, then race starts.
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.selectedTrack = track;
                GameFlowManager.Instance.GoToKartSelect();
            }
            else
            {
                SceneManager.LoadScene("KartSelect");
            }
        }

        private void ShowToast(string message)
        {
            if (toastDialog == null)
            {
                Debug.Log($"[TrackSelect] (toast missing) {message}");
                return;
            }
            if (toastText != null) toastText.text = message;
            toastDialog.SetActive(true);
        }

        private void OnBackClicked()
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            Debug.Log("[TrackSelect] BACK clicked — going to mode select.");
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
