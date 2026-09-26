using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace QuickChecks.UI
{
    /// <summary>
    /// Full-screen overlay shown between turns in pass-and-play mode.
    /// Displays "Pass to Player N" message and a tap-to-continue prompt.
    ///
    /// Supports both TMP_Text and legacy UnityEngine.UI.Text via separate fields.
    /// Assign whichever one is appropriate for your UI prefab in the Inspector.
    /// </summary>
    public class PassPlayOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private TMP_Text messageTextTMP;
        [SerializeField] private TMP_Text promptTextTMP;
        [SerializeField] private Text messageTextLegacy;
        [SerializeField] private Text promptTextLegacy;

        private void Awake()
        {
            Hide();
        }

        public void Show(string message)
        {
            if (overlayRoot != null) overlayRoot.SetActive(true);
            SetText(messageTextTMP, messageTextLegacy, message);
            SetText(promptTextTMP, promptTextLegacy, "Tap to continue");
        }

        public void Hide()
        {
            if (overlayRoot != null) overlayRoot.SetActive(false);
        }

        private static void SetText(TMP_Text tmp, Text legacy, string value)
        {
            if (tmp != null) tmp.text = value;
            else if (legacy != null) legacy.text = value;
        }
    }
}
