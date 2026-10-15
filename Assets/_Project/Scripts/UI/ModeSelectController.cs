using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using QuickChecks.Core;

namespace QuickChecks.UI
{
    /// <summary>
    /// Mode selection controller. Shows buttons for each game mode:
    ///   - Solo Practice (with ghost)
    ///   - vs CPU (Easy / Medium / Hard)
    ///   - Pass & Play (2P / 3P / 4P)
    ///   - Online Match (v1 stub — CPU opponent)
    ///
    /// Sets GameFlowManager.selectedMode, then goes to TrackSelect.
    /// </summary>
    public class ModeSelectController : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleText;

        private void Start()
        {
            if (titleText != null) titleText.text = "Select Mode";

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
            }

            // Wire mode buttons by name
            WireModeButton("SoloButton", RaceMode.Solo);
            WireModeButton("VsCpuEasyButton", RaceMode.VsCpuEasy);
            WireModeButton("VsCpuMediumButton", RaceMode.VsCpuMedium);
            WireModeButton("VsCpuHardButton", RaceMode.VsCpuHard);
            WireModeButton("PassPlay2Button", RaceMode.PassPlay2);
            WireModeButton("PassPlay3Button", RaceMode.PassPlay3);
            WireModeButton("PassPlay4Button", RaceMode.PassPlay4);
            WireModeButton("OnlineButton", RaceMode.Online);

            Debug.Log("[ModeSelect] Ready. Select a game mode.");
        }

        private void WireModeButton(string buttonName, RaceMode mode)
        {
            // Search the entire scene for the button by name (buttons are children of Canvas,
            // not children of this [ModeSelectController] GameObject)
            var button = FindButtonRecursive(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects(), buttonName);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnModeSelected(mode));
                Debug.Log($"[ModeSelect] Wired '{buttonName}' → {mode.DisplayName()}");
            }
            else
            {
                Debug.LogWarning($"[ModeSelect] Button '{buttonName}' not found — mode {mode.DisplayName()} won't be selectable.");
            }
        }

        private Button FindButtonRecursive(GameObject[] rootObjects, string name)
        {
            foreach (var root in rootObjects)
            {
                var found = FindButtonRecursive(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private Button FindButtonRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child.GetComponent<Button>();
                }
                var found = FindButtonRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void OnModeSelected(RaceMode mode)
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

        private void OnBackClicked()
        {
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
    }
}
