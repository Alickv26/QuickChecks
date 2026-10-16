namespace QuickChecks.Core
{
    /// <summary>
    /// Star rating calculation based on swipes vs par.
    ///
    /// Per DECISIONS.md Decision 9 + Decision 21:
    ///   - Par is designer-set per track (TrackDefinition.parSwipeCount).
    ///   - 3 stars : swipes <= par                  (perfect / better than par)
    ///   - 2 stars : swipes <= par * 1.25           (good)
    ///   - 1 star  : swipes <= par * 1.5            (passing — unlocks next track)
    ///   - 0 stars : swipes >  par * 1.5            (completed but stuck — no unlock)
    ///
    /// DNF (hit max-swipe cap) is always 0 stars and no unlock.
    /// </summary>
    public static class StarRating
    {
        /// <summary>
        /// Returns 0-3 stars based on the player's swipe count vs par.
        /// Pass isDnf=true to force 0 stars.
        /// </summary>
        public static int Calculate(int swipes, int parSwipes, bool isDnf = false)
        {
            if (isDnf) return 0;
            if (parSwipes <= 0) return 3;  // no par set — treat as 3 stars

            if (swipes <= parSwipes)               return 3;
            if (swipes <= parSwipes * 1.25f)        return 2;
            if (swipes <= parSwipes * 1.5f)         return 1;
            return 0;
        }

        /// <summary>
        /// Returns the swipes threshold for a given star tier.
        /// starTier: 3, 2, or 1. Returns int.MaxValue for 0 stars (no upper bound).
        /// </summary>
        public static int ThresholdFor(int parSwipes, int starTier)
        {
            return starTier switch
            {
                3 => parSwipes,
                2 => MathfCeil(parSwipes * 1.25f),
                1 => MathfCeil(parSwipes * 1.5f),
                _ => int.MaxValue
            };
        }

        /// <summary>True if the player earned at least 1 star (unlocks next track per Decision 8).</summary>
        public static bool UnlocksNextTrack(int stars) => stars >= 1;

        /// <summary>Display string for stars (filled vs empty).</summary>
        public static string DisplayString(int starsEarned)
        {
            // Unicode BLACK STAR ★ U+2605 and WHITE STAR ☆ U+2606 — both render in TMP.
            // Filled stars = starsEarned, empty = 3 - starsEarned.
            return new string('\u2605', starsEarned) + new string('\u2606', 3 - starsEarned);
        }

        /// <summary>One-line subtitle for the result, e.g. "Under par! Excellent run."</summary>
        public static string SubtitleFor(int starsEarned, bool isDnf)
        {
            if (isDnf) return "Hit the swipe cap — better luck next time.";
            return starsEarned switch
            {
                3 => "Under par! Excellent run.",
                2 => "Solid run — close to par.",
                1 => "Completed — that was tough.",
                0 => "Finished, but over par.",
                _ => "Completed."
            };
        }

        // Avoid pulling in UnityEngine for a tiny ceil op in this utility; replicate Mathf.CeilToInt.
        private static int MathfCeil(float f)
        {
            int i = (int)f;
            return f > i ? i + 1 : i;
        }
    }
}
