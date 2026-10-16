using UnityEngine;
using UnityEngine.UI;
using TMPro;
using QuickChecks.Core;

namespace QuickChecks.UI
{
    /// <summary>
    /// Results screen controller. Shows:
    ///   - Race complete message
    ///   - Swipe count vs par
    ///   - Time
    ///   - Best swipes / time
    ///   - "NEW FASTEST LAP!" if applicable
    ///   - Retry button (re-races same track)
    ///   - Menu button (back to track select)
    /// </summary>
    public class ResultsScreenController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text swipesText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text bestText;
        [SerializeField] private TMP_Text newBestText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;

        private void Start()
        {
            var flow = GameFlowManager.Instance;
            if (flow == null)
            {
                Debug.LogError("[Results] GameFlowManager not found!");
                return;
            }

            // Populate results
            int swipes = flow.lastRaceSwipes;
            long timeMs = flow.lastRaceTimeMs;
            bool isNewBest = flow.lastRaceIsNewBest;
            int bestSwipes = flow.lastRaceBestSwipes;
            long bestTimeMs = flow.lastRaceBestTimeMs;

            if (titleText != null) titleText.text = "Race Complete!";
            if (swipesText != null) swipesText.text = $"Swipes: {swipes}";
            if (timeText != null) timeText.text = $"Time: {timeMs / 1000f:F2}s";

            if (bestText != null)
            {
                bestText.text = isNewBest
                    ? $"Previous best: {bestSwipes} swipes / {bestTimeMs / 1000f:F2}s"
                    : $"Best: {bestSwipes} swipes / {bestTimeMs / 1000f:F2}s";
            }

            if (newBestText != null)
            {
                newBestText.gameObject.SetActive(isNewBest);
                if (isNewBest)
                {
                    newBestText.text = "*** NEW FASTEST LAP! ***\nGhost updated!";
                    newBestText.color = new Color(0.247f, 0.878f, 0.760f);  // cyan
                }
            }

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

            Debug.Log($"[Results] Showing results: {swipes} swipes, {timeMs / 1000f:F2}s, " +
                      $"new best: {isNewBest}.");
        }

        private void OnRetryClicked()
        {
            Debug.Log("[Results] RETRY clicked — restarting race.");
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.RetryRace();
            }
        }

        private void OnMenuClicked()
        {
            Debug.Log("[Results] MENU clicked — going to track select.");
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.GoToTrackSelect();
            }
        }
    }
}
