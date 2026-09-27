using UnityEngine;

namespace QuickChecks.Monetization
{
    /// <summary>
    /// Persists the player's entitlement state (free vs premium) in PlayerPrefs.
    /// Gates access to locked tracks, karts, and online ranked matches.
    ///
    /// Decision 16 (DECISIONS.md): tracks 1-3 are free, tracks 4-8 require IAP unlock.
    /// Decision 17: one-time IAP of $4.99 unlocks the full game.
    ///
    /// Storage: PlayerPrefs key "QuickChecks_IsPremium" (0 = free, 1 = premium).
    /// For production launch: consider migrating to Supabase-backed storage
    /// so entitlement survives reinstall (currently lost if PlayerPrefs wiped).
    /// </summary>
    public class EntitlementManager : MonoBehaviour
    {
        public static EntitlementManager Instance { get; private set; }

        private const string PREFS_KEY = "QuickChecks_IsPremium";

        /// <summary>Fires when entitlement changes (e.g., user purchased).</summary>
        public event System.Action<bool> OnEntitlementChanged;

        public bool IsPremium { get; private set; } = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadEntitlement();
        }

        private void LoadEntitlement()
        {
            IsPremium = PlayerPrefs.GetInt(PREFS_KEY, 0) == 1;
            Debug.Log($"[EntitlementManager] Loaded entitlement: IsPremium = {IsPremium}.");
        }

        /// <summary>
        /// Marks the player as premium (called by IAPService after successful purchase).
        /// Persists to PlayerPrefs immediately.
        /// </summary>
        public void SetPremium(bool premium)
        {
            if (IsPremium == premium) return;
            IsPremium = premium;
            PlayerPrefs.SetInt(PREFS_KEY, premium ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[EntitlementManager] Entitlement updated: IsPremium = {IsPremium}. Persisted.");
            OnEntitlementChanged?.Invoke(IsPremium);
        }

        /// <summary>
        /// Returns true if the given track is accessible to this player.
        /// Free tracks (1-3) are always accessible; paid tracks (4-8) require IsPremium.
        /// </summary>
        public bool IsTrackUnlocked(Track.TrackDefinition track)
        {
            if (track == null) return false;
            // Free tracks: always accessible
            if (track.isFree) return true;
            // Paid tracks: require premium
            return IsPremium;
        }

        /// <summary>
        /// Returns true if the given kart is accessible.
        /// Karts 1-3 (trackIndex 0-2) are free; karts 4-8 (trackIndex 3-7) require premium.
        /// </summary>
        public bool IsKartUnlocked(Racing.KartStats kart)
        {
            if (kart == null) return false;
            // Free karts have unlockAfterTrackIndex < 3 (or -1 = unlocked from start)
            if (kart.unlockAfterTrackIndex < 0 || kart.unlockAfterTrackIndex < 3) return true;
            return IsPremium;
        }

        /// <summary>
        /// Returns true if the player can access online ranked matches (premium-only per Decision 16).
        /// </summary>
        public bool CanAccessOnlineRanked()
        {
            return IsPremium;
        }

        /// <summary>
        /// Returns true if the player can submit scores to the daily track leaderboard (premium-only).
        /// Free players can still PLAY the daily track, but can't submit scores.
        /// </summary>
        public bool CanSubmitDailyLeaderboard()
        {
            return IsPremium;
        }
    }
}
