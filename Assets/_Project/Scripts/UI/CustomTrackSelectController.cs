using System;
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
    /// Lists all saved custom tracks. Tap a track to Play / Edit / Delete.
    /// Top has a "New Track" button that opens the Custom Track Builder with a fresh track.
    /// </summary>
    public class CustomTrackSelectController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform trackListContainer;
        [SerializeField] private Button newTrackButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text emptyStateLabel;

        [Header("Toast")]
        [SerializeField] private GameObject toastDialog;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private Button toastConfirmButton;
        [SerializeField] private Button toastCancelButton;

        private List<CustomTrackData> _tracks = new();
        private CustomTrackData _pendingDeleteTrack;

        private void Start()
        {
            if (titleText != null) titleText.text = "Custom Tracks";

            WireButton(newTrackButton, OnNewTrackClicked);
            WireButton(backButton, OnBackClicked);
            WireButton(toastConfirmButton, OnConfirmDeleteClicked);
            WireButton(toastCancelButton, OnCancelDeleteClicked);

            if (toastDialog != null) toastDialog.SetActive(false);

            RefreshList();
        }

        private void RefreshList()
        {
            _tracks = CustomTrackLibrary.LoadAll();

            if (trackListContainer == null) return;
            for (int i = trackListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(trackListContainer.GetChild(i).gameObject);
            }

            if (_tracks.Count == 0)
            {
                if (emptyStateLabel != null)
                {
                    emptyStateLabel.text = "No custom tracks yet.\nTap 'New Track' to design your first!";
                    emptyStateLabel.gameObject.SetActive(true);
                }
                return;
            }

            if (emptyStateLabel != null) emptyStateLabel.gameObject.SetActive(false);

            for (int i = 0; i < _tracks.Count; i++)
            {
                CreateTrackCard(_tracks[i], i);
            }
        }

        private void CreateTrackCard(CustomTrackData track, int index)
        {
            var go = new GameObject($"CustomTrackCard_{index}");
            go.transform.SetParent(trackListContainer, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 180);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.106f, 0.165f, 0.165f, 1f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(20, 20, 12, 12);
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Track name
            var nameGo = new GameObject("TrackName");
            nameGo.transform.SetParent(go.transform, false);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.text = track.trackName;
            nameText.fontSize = 36;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = Phase2UiPalette.TextPrimary;
            nameText.raycastTarget = false;

            // Stats line
            var statsGo = new GameObject("Stats");
            statsGo.transform.SetParent(go.transform, false);
            var statsText = statsGo.AddComponent<TextMeshProUGUI>();
            DateTime created = DateTimeOffset.FromUnixTimeMilliseconds(track.createdAtMs).LocalDateTime;
            statsText.text = $"{track.splinePoints.Count} points  ·  {track.obstacles.Count} obstacles  ·  " +
                             $"{track.powerUps.Count} power-ups  ·  Par {track.parSwipeCount}\n" +
                             $"Created {created:MMM d, yyyy}";
            statsText.fontSize = 22;
            statsText.alignment = TextAlignmentOptions.Left;
            statsText.color = Phase2UiPalette.TextDim;
            statsText.raycastTarget = false;

            // Button row
            var rowGo = new GameObject("ButtonRow");
            rowGo.transform.SetParent(go.transform, false);
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;

            var playBtn = CreateCardButton(rowGo.transform, "Play", new Color(0.247f, 0.878f, 0.760f, 0.9f),
                new Color(0.055f, 0.078f, 0.078f));
            playBtn.onClick.AddListener(() => OnPlayClicked(track));

            var editBtn = CreateCardButton(rowGo.transform, "Edit", new Color(0.42f, 0.58f, 1f, 0.9f),
                Color.white);
            editBtn.onClick.AddListener(() => OnEditClicked(track));

            var deleteBtn = CreateCardButton(rowGo.transform, "Delete", new Color(1f, 0.42f, 0.42f, 0.9f),
                Color.white);
            deleteBtn.onClick.AddListener(() => OnDeleteClicked(track));
        }

        private Button CreateCardButton(Transform parent, string label, Color bg, Color labelColor)
        {
            var go = new GameObject($"{label}Button");
            go.transform.SetParent(parent, false);
            var btn = go.AddComponent<Button>();
            var img = go.AddComponent<Image>();
            img.color = bg;
            btn.targetGraphic = img;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 60);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelT = labelGo.AddComponent<TextMeshProUGUI>();
            labelT.text = label;
            labelT.fontSize = 24;
            labelT.alignment = TextAlignmentOptions.Center;
            labelT.color = labelColor;
            labelT.raycastTarget = false;
            var labelR = labelGo.GetComponent<RectTransform>();
            labelR.anchorMin = Vector2.zero; labelR.anchorMax = Vector2.one;
            labelR.pivot = new Vector2(0.5f, 0.5f);
            labelR.offsetMin = Vector2.zero; labelR.offsetMax = Vector2.zero;

            if (btn.GetComponent<ButtonHoverFx>() == null) btn.gameObject.AddComponent<ButtonHoverFx>();
            return btn;
        }

        // =====================================================================
        // Handlers
        // =====================================================================

        private void OnNewTrackClicked()
        {
            Debug.Log("[CustomSelect] New Track clicked.");
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.pendingCustomTrack = null;  // fresh track
                GameFlowManager.Instance.GoToCustomTrackBuilder();
            }
            else
            {
                SceneManager.LoadScene("CustomTrackBuilder");
            }
        }

        private void OnPlayClicked(CustomTrackData track)
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            Debug.Log($"[CustomSelect] Play '{track.trackName}'.");
            if (GameFlowManager.Instance != null)
            {
                var def = CustomTrackLibrary.ToTrackDefinition(track);
                GameFlowManager.Instance.selectedMode = RaceMode.Solo;
                GameFlowManager.Instance.selectedTrack = def;
                GameFlowManager.Instance.StartRace(
                    def, GameFlowManager.Instance.selectedKart ?? TrackFactory.GetDefaultKart());
            }
        }

        private void OnEditClicked(CustomTrackData track)
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            Debug.Log($"[CustomSelect] Edit '{track.trackName}'.");
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.pendingCustomTrack = track;
                GameFlowManager.Instance.GoToCustomTrackBuilder();
            }
        }

        private void OnDeleteClicked(CustomTrackData track)
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            _pendingDeleteTrack = track;
            ShowToast($"Delete '{track.trackName}'?\nThis cannot be undone.");
        }

        private void ShowToast(string message)
        {
            if (toastDialog == null) return;
            if (toastText != null) toastText.text = message;
            toastDialog.SetActive(true);
        }

        private void OnConfirmDeleteClicked()
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            if (_pendingDeleteTrack != null)
            {
                CustomTrackLibrary.Delete(_pendingDeleteTrack.trackId);
                Debug.Log($"[CustomSelect] Deleted '{_pendingDeleteTrack.trackName}'.");
                _pendingDeleteTrack = null;
            }
            if (toastDialog != null) toastDialog.SetActive(false);
            RefreshList();
        }

        private void OnCancelDeleteClicked()
        {
            if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
            _pendingDeleteTrack = null;
            if (toastDialog != null) toastDialog.SetActive(false);
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

        private void WireButton(Button btn, Action onClick)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (SfxPlayer.Instance != null) SfxPlayer.Instance.PlayMenuClick();
                onClick?.Invoke();
            });
            if (btn.GetComponent<ButtonHoverFx>() == null) btn.gameObject.AddComponent<ButtonHoverFx>();
        }
    }
}
