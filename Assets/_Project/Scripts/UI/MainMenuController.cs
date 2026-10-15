using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Core;

namespace QuickChecks.UI
{
    /// <summary>
    /// Main menu controller. Shows title, Play button, Settings button.
    /// Play → goes to Track Select.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;

        private void Start()
        {
            if (titleText != null) titleText.text = "QuickChecks";
            if (subtitleText != null) subtitleText.text = "Swipe Golf Racing";

            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(OnPlayClicked);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(OnSettingsClicked);
            }

            Debug.Log("[MainMenu] Ready. Tap PLAY to go to track select.");
        }

        private void OnPlayClicked()
        {
            Debug.Log("[MainMenu] PLAY clicked — going to mode select.");
            if (GameFlowManager.Instance != null)
            {
                // Go to ModeSelect (not TrackSelect directly)
                UnityEngine.SceneManagement.SceneManager.LoadScene("ModeSelect");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("ModeSelect");
            }
        }

        private void OnSettingsClicked()
        {
            Debug.Log("[MainMenu] SETTINGS clicked — (settings UI not yet implemented).");
            // TODO: Show settings panel (audio, accessibility, restore purchases)
        }
    }
}
