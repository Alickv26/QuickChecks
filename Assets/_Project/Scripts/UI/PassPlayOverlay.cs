using UnityEngine;
using TMPro;

namespace QuickChecks.UI
{
    /// <summary>
    /// Full-screen overlay shown between turns in pass-and-play mode.
    /// Displays "Pass to Player N" message and a tap-to-continue prompt.
    /// </summary>
    public class PassPlayOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text promptText;

        private void Awake()
        {
            Hide();
        }

        public void Show(string message)
        {
            if (overlayRoot != null) overlayRoot.SetActive(true);
            if (messageText != null) messageText.text = message;
            if (promptText != null) promptText.text = "Tap to continue";
        }

        public void Hide()
        {
            if (overlayRoot != null) overlayRoot.SetActive(false);
        }
    }
}
