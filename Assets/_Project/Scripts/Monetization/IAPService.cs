using System;
using System.Threading.Tasks;
using UnityEngine;

namespace QuickChecks.Monetization
{
    /// <summary>
    /// Wraps the platform IAP system (App Store / Play Store) and exposes a simple
    /// async API for purchasing the "Unlock Full Game" product.
    ///
    /// For v1 prototype/beta: this is a STUB that simulates a purchase with a delay
    /// and returns success. No real money is charged.
    ///
    /// For production launch: replace the stub body with Unity IAP integration
    /// (https://docs.unity3d.com/Packages/com.unity.purchasing@latest):
    ///   - Initialize Unity IAP with product ID "unlock_full_game"
    ///   - On purchase success: validate receipt via Supabase Edge Function
    ///   - Update EntitlementManager.SetPremium(true)
    ///   - Restore purchases flow on app launch
    /// </summary>
    public class IAPService : MonoBehaviour
    {
        public static IAPService Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private string productId = "unlock_full_game";
        [SerializeField] private string priceDisplayString = "$4.99";

        [Header("Stub Behavior (v1 beta)")]
        [Tooltip("If true, purchases always succeed after a fake delay (no real money charged). " +
                 "Set to false when integrating real Unity IAP for production launch.")]
        [SerializeField] private bool useStubMode = true;
        [SerializeField] private float stubPurchaseDelaySec = 1.5f;

        public string ProductId => productId;
        public string PriceDisplay => priceDisplayString;
        public bool IsStubMode => useStubMode;

        /// <summary>Fired when a purchase completes successfully (stub or real).</summary>
        public event Action OnPurchaseSuccess;

        /// <summary>Fired when a purchase fails (user cancelled, network error, etc.).</summary>
        public event Action<string> OnPurchaseFailed;

        /// <summary>Fired when restore-purchases completes (with bool: was anything restored?).</summary>
        public event Action<bool> OnRestoreComplete;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Initiates a purchase of the unlock_full_game product.
        /// Returns true on success, false on failure.
        /// </summary>
        public async Task<bool> PurchaseUnlockAsync()
        {
            Debug.Log($"[IAPService] Initiating purchase for '{productId}' ({priceDisplayString}).");

            if (useStubMode)
            {
                Debug.Log("[IAPService] STUB MODE: simulating purchase with fake delay. No real money charged.");
                await Task.Delay((int)(stubPurchaseDelaySec * 1000));

                // Simulate success
                Debug.Log("[IAPService] STUB purchase succeeded.");
                OnPurchaseSuccess?.Invoke();
                return true;
            }

            // Real Unity IAP integration goes here for production launch:
            //   - Call IStoreController.InitiatePurchase(productId)
            //   - Wait for ProcessPurchase() callback
            //   - Validate receipt via Supabase Edge Function
            //   - On validation success: return true; else: return false
            //
            // For now (v1 beta), this code path is unreachable because useStubMode=true.
            await Task.Delay(100);
            OnPurchaseFailed?.Invoke("Real IAP not yet integrated — set useStubMode=true for beta testing.");
            return false;
        }

        /// <summary>
        /// Restores previous purchases (called on app launch + when user taps "Restore Purchases").
        /// </summary>
        public async Task<bool> RestorePurchasesAsync()
        {
            Debug.Log("[IAPService] Restoring purchases...");

            if (useStubMode)
            {
                await Task.Delay((int)(stubPurchaseDelaySec * 500));

                // Check if EntitlementManager already has premium flag set (from previous stub purchase)
                bool wasPremium = EntitlementManager.Instance != null && EntitlementManager.Instance.IsPremium;
                Debug.Log($"[IAPService] STUB restore: returning {wasPremium} (was premium).");
                OnRestoreComplete?.Invoke(wasPremium);
                return wasPremium;
            }

            // Real Unity IAP restore:
            //   - Call IStoreController.extensions.GetExtension<IAppleExtensions>().RestoreTransactions()
            //   - Wait for restore callbacks
            await Task.Delay(100);
            OnRestoreComplete?.Invoke(false);
            return false;
        }
    }
}
