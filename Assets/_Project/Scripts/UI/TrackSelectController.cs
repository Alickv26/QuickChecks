using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Core;
using QuickChecks.Track;
using QuickChecks.Racing;

namespace QuickChecks.UI
{
    /// <summary>
    /// Track select controller. Shows a grid of track cards.
    /// Each card shows: track name, difficulty stars, best swipes, locked/unlocked state.
    /// Tapping an unlocked track → starts the race.
    /// Tapping a locked track → shows paywall (if paid) or "complete previous track" message.
    /// </summary>
    public class TrackSelectController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform trackGridContainer;
        [SerializeField] private GameObject trackCardPrefab;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleText;

        [Header("Track Library")]
        [SerializeField] private TrackLibrary trackLibrary;

        private void Start()
        {
            if (titleText != null) titleText.text = "Select Track";

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
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
            GameObject cardGo;
            if (trackCardPrefab != null)
            {
                cardGo = Instantiate(trackCardPrefab, trackGridContainer);
            }
            else
            {
                // Create a simple card at runtime
                cardGo = CreateRuntimeTrackCard(track, index);
                cardGo.transform.SetParent(trackGridContainer, false);
            }

            // Setup card content
            SetupCard(cardGo, track, index);
        }

        private GameObject CreateRuntimeTrackCard(TrackDefinition track, int index)
        {
            var go = new GameObject($"TrackCard_{index}");
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 200);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.106f, 0.165f, 0.165f, 1f);  // dark teal

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;

            // Track name
            var nameGo = new GameObject("TrackName");
            nameGo.transform.SetParent(go.transform, false);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.text = $"{index + 1}. {track.displayName}";
            nameText.fontSize = 32;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.5f, 1);
            nameRect.anchorMax = new Vector2(0.5f, 1);
            nameRect.pivot = new Vector2(0.5f, 1);
            nameRect.anchoredPosition = new Vector2(0, -10);
            nameRect.sizeDelta = new Vector2(380, 50);

            // Difficulty stars
            var starsGo = new GameObject("Difficulty");
            starsGo.transform.SetParent(go.transform, false);
            var starsText = starsGo.AddComponent<TextMeshProUGUI>();
            starsText.text = new string('★', track.difficultyStars);
            starsText.fontSize = 28;
            starsText.alignment = TextAlignmentOptions.Center;
            starsText.color = new Color(1f, 0.82f, 0.4f);  // yellow
            var starsRect = starsGo.GetComponent<RectTransform>();
            starsRect.anchorMin = new Vector2(0.5f, 0.5f);
            starsRect.anchorMax = new Vector2(0.5f, 0.5f);
            starsRect.pivot = new Vector2(0.5f, 0.5f);
            starsRect.anchoredPosition = new Vector2(0, 10);
            starsRect.sizeDelta = new Vector2(380, 40);

            // Best swipes
            var bestGo = new GameObject("BestSwipes");
            bestGo.transform.SetParent(go.transform, false);
            var bestText = bestGo.AddComponent<TextMeshProUGUI>();
            int bestSwipes = GameFlowManager.Instance?.GetBestSwipesForTrack(track.trackId) ?? 0;
            bestText.text = bestSwipes > 0 ? $"Best: {bestSwipes} swipes" : "Not completed";
            bestText.fontSize = 24;
            bestText.alignment = TextAlignmentOptions.Center;
            bestText.color = new Color(0.247f, 0.878f, 0.760f);  // cyan
            var bestRect = bestGo.GetComponent<RectTransform>();
            bestRect.anchorMin = new Vector2(0.5f, 0);
            bestRect.anchorMax = new Vector2(0.5f, 0);
            bestRect.pivot = new Vector2(0.5f, 0);
            bestRect.anchoredPosition = new Vector2(0, 10);
            bestRect.sizeDelta = new Vector2(380, 40);

            // Locked indicator
            bool unlocked = IsTrackUnlocked(track);
            if (!unlocked)
            {
                var lockGo = new GameObject("Locked");
                lockGo.transform.SetParent(go.transform, false);
                var lockText = lockGo.AddComponent<TextMeshProUGUI>();
                lockText.text = track.isFree ? "Complete previous track" : "Premium — $4.99";
                lockText.fontSize = 22;
                lockText.alignment = TextAlignmentOptions.Center;
                lockText.color = new Color(1f, 0.42f, 0.42f);  // red
                var lockRect = lockGo.GetComponent<RectTransform>();
                lockRect.anchorMin = new Vector2(0.5f, 0);
                lockRect.anchorMax = new Vector2(0.5f, 0);
                lockRect.pivot = new Vector2(0.5f, 0);
                lockRect.anchoredPosition = new Vector2(0, 50);
                lockRect.sizeDelta = new Vector2(380, 30);

                // Dim the card
                img.color = new Color(0.05f, 0.08f, 0.08f, 1f);  // darker
                nameText.color = new Color(0.5f, 0.5f, 0.5f);  // gray
            }

            return go;
        }

        private void SetupCard(GameObject cardGo, TrackDefinition track, int index)
        {
            var button = cardGo.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnTrackClicked(track));
            }
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
                if (!track.isFree)
                {
                    Debug.Log($"[TrackSelect] Track '{track.displayName}' is locked — show paywall.");
                    // TODO: Show paywall modal (PaywallController)
                }
                else
                {
                    Debug.Log($"[TrackSelect] Track '{track.displayName}' is locked — complete previous track first.");
                }
                return;
            }

            Debug.Log($"[TrackSelect] Track '{track.displayName}' selected — starting race.");

            // Use default kart for now (Track 1's kart)
            KartStats kart = TrackFactory.GetDefaultKart();

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.StartRace(track, kart);
            }
            else
            {
                SceneManager.LoadScene("Race");
            }
        }

        private void OnBackClicked()
        {
            Debug.Log("[TrackSelect] BACK clicked — going to main menu.");
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.GoToMainMenu();
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
        }
    }
}
