using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace QuickChecks.Monetization
{
    /// <summary>
    /// UI controller for the paywall modal. Shown when a free player taps a locked track.
    /// Modal contents:
    ///   - Title: "Unlock Full Game"
    ///   - Price: $4.99 (or local equivalent)
    ///   - Features list: 5 more tracks, 5 more karts, online ranked, full daily leaderboard
    ///   - Buy button (calls IAPService.PurchaseUnlockAsync)
    ///   - Restore Purchases button
    ///   - Close button (Maybe later)
    ///
    /// Decision 16: hard wall — paywall is the only way to unlock.
    /// </summary>
    public class PaywallController : MonoBehaviour
    {
        [Header("References (auto-created if null)")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text featuresText;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button restoreButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text statusText;

        [Header("UI Text")]
        [SerializeField] private string title = "Unlock Full Game";
        [SerializeField] private string features = "Get the complete QuickChecks experience:\n\n" +
                                                   "• 5 more tracks (4-8)\n" +
                                                   "• 5 more karts\n" +
                                                   "• Online ranked matches\n" +
                                                   "• Full daily leaderboard\n" +
                                                   "• No ads, ever\n\n" +
                                                   "One-time purchase. No subscriptions.";

        [Header("Status Messages")]
        [SerializeField] private string processingMessage = "Processing purchase...";
        [SerializeField] private string successMessage = "Purchase complete! All content unlocked.";
        [SerializeField] private string restoreSuccessMessage = "Purchases restored! All content unlocked.";
        [SerializeField] private string restoreEmptyMessage = "No previous purchases found.";
        [SerializeField] private string failureMessage = "Purchase failed. Please try again.";

        private IAPService _iapService;
        private EntitlementManager _entitlementManager;
        private bool _isProcessing = false;

        private void Awake()
        {
            _iapService = FindObjectOfType<IAPService>();
            _entitlementManager = EntitlementManager.Instance;

            if (_iapService != null)
            {
                _iapService.OnPurchaseSuccess += HandlePurchaseSuccess;
                _iapService.OnPurchaseFailed += HandlePurchaseFailed;
                _iapService.OnRestoreComplete += HandleRestoreComplete;
            }

            // Wire up button click handlers
            if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);
            if (restoreButton != null) restoreButton.onClick.AddListener(OnRestoreClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);

            Hide();
        }

        private void OnDestroy()
        {
            if (_iapService != null)
            {
                _iapService.OnPurchaseSuccess -= HandlePurchaseSuccess;
                _iapService.OnPurchaseFailed -= HandlePurchaseFailed;
                _iapService.OnRestoreComplete -= HandleRestoreComplete;
            }
        }

        public void Show()
        {
            if (modalRoot != null) modalRoot.SetActive(true);

            // Populate text
            if (titleText != null) titleText.text = title;
            if (featuresText != null) featuresText.text = features;
            if (priceText != null && _iapService != null) priceText.text = _iapService.PriceDisplay;
            if (statusText != null) statusText.text = "";

            // Enable buttons (in case they were disabled during processing)
            SetButtonsInteractable(true);

            Debug.Log("[PaywallController] Paywall shown.");
        }

        public void Hide()
        {
            if (modalRoot != null) modalRoot.SetActive(false);
        }

        private async void OnBuyClicked()
        {
            if (_isProcessing) return;
            _isProcessing = true;
            SetButtonsInteractable(false);
            if (statusText != null) statusText.text = processingMessage;

            Debug.Log("[PaywallController] Buy button clicked.");

            if (_iapService != null)
            {
                bool success = await _iapService.PurchaseUnlockAsync();
                if (!success)
                {
                    // OnPurchaseFailed event will handle status text
                }
            }
            else
            {
                Debug.LogError("[PaywallController] IAPService not found!");
                if (statusText != null) statusText.text = "Error: IAP service not available.";
                SetButtonsInteractable(true);
            }

            _isProcessing = false;
        }

        private async void OnRestoreClicked()
        {
            if (_isProcessing) return;
            _isProcessing = true;
            SetButtonsInteractable(false);
            if (statusText != null) statusText.text = "Restoring purchases...";

            if (_iapService != null)
            {
                await _iapService.RestorePurchasesAsync();
            }

            _isProcessing = false;
            SetButtonsInteractable(true);
        }

        private void HandlePurchaseSuccess()
        {
            if (_entitlementManager != null)
            {
                _entitlementManager.SetPremium(true);
            }
            if (statusText != null) statusText.text = successMessage;
            SetButtonsInteractable(true);

            // Auto-close after 2 seconds
            Invoke(nameof(Hide), 2f);
        }

        private void HandlePurchaseFailed(string error)
        {
            if (statusText != null) statusText.text = failureMessage;
            SetButtonsInteractable(true);
            Debug.LogWarning($"[PaywallController] Purchase failed: {error}");
        }

        private void HandleRestoreComplete(bool restored)
        {
            if (statusText != null)
            {
                statusText.text = restored ? restoreSuccessMessage : restoreEmptyMessage;
            }
            SetButtonsInteractable(true);

            if (restored && _entitlementManager != null)
            {
                _entitlementManager.SetPremium(true);
                Invoke(nameof(Hide), 2f);
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (buyButton != null) buyButton.interactable = interactable;
            if (restoreButton != null) restoreButton.interactable = interactable;
            if (closeButton != null) closeButton.interactable = interactable;
        }
    }
}
