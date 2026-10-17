using UnityEngine;

namespace QuickChecks.Racing
{
    /// <summary>
    /// Library of all karts in the game. References the 8 KartStats assets.
    /// Used by the kart-select UI to render the grid and look up a selected kart by ID.
    ///
    /// Karts are ordered by unlock progression (index 0 = Starter, unlocked from start;
    /// index 7 = Apex, unlocked after track 7 completed).
    /// First 3 karts have isFree=true (Decision 16: freemium gating).
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Kart Library", fileName = "KartLibrary")]
    public class KartLibrary : ScriptableObject
    {
        [Tooltip("All karts in the game, ordered by unlock progression. " +
                 "Index 0 = Starter (unlocked from start), Index 7 = Apex (endgame).")]
        public KartStats[] karts = new KartStats[8];

        /// <summary>
        /// Returns the kart at the given 0-based index, or null if out of range.
        /// </summary>
        public KartStats GetKart(int index)
        {
            if (karts == null || index < 0 || index >= karts.Length) return null;
            return karts[index];
        }

        /// <summary>
        /// Returns the first kart with the given kartId, or null if not found.
        /// </summary>
        public KartStats FindById(string kartId)
        {
            if (karts == null || string.IsNullOrEmpty(kartId)) return null;
            foreach (var k in karts)
            {
                if (k != null && k.kartId == kartId) return k;
            }
            return null;
        }

        /// <summary>
        /// Returns the default kart (Starter, index 0) — used when no selection has been made yet.
        /// </summary>
        public KartStats GetDefaultKart()
        {
            if (karts != null && karts.Length > 0 && karts[0] != null) return karts[0];
            return null;
        }

        /// <summary>
        /// Returns true if the kart is unlocked for the given player progress
        /// (highest track index completed).
        ///
        /// Rules:
        ///   - Starter (unlockAfterTrackIndex == -1) → always unlocked
        ///   - Other karts → unlocked if highestUnlockedTrackIndex >= unlockAfterTrackIndex
        ///   - Paid karts → also require premium entitlement (checked by caller via EntitlementManager)
        /// </summary>
        public bool IsUnlocked(KartStats kart, int highestUnlockedTrackIndex)
        {
            if (kart == null) return false;
            if (kart.unlockAfterTrackIndex < 0) return true;
            return highestUnlockedTrackIndex >= kart.unlockAfterTrackIndex;
        }

        /// <summary>
        /// Returns true if the kart is free (no IAP required).
        /// Decision 16: 3 free karts, 5 paid karts.
        /// </summary>
        public bool IsFree(KartStats kart)
        {
            return kart != null && kart.isFree;
        }
    }
}
