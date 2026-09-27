namespace QuickChecks.Onboarding
{
    /// <summary>
    /// The 4 steps of the forced tutorial (Decision 7 — see DECISIONS.md).
    /// Order matters: each step must be completed before the next begins.
    ///
    /// Total target duration: ~30 seconds.
    /// </summary>
    public enum TutorialStep
    {
        /// <summary>
        /// Step 1: Player must swipe to move the kart.
        /// Validates: swipe detection works, player understands the core mechanic.
        /// Success: kart moves (any direction, any distance).
        /// </summary>
        SwipeToMove,

        /// <summary>
        /// Step 2: Player must try a slow drag (which gets rejected), then a fast swipe (which works).
        /// Validates: player understands that slow drags don't count — only flicks do.
        /// Success: at least 1 slow drag attempted (rejected), then 1 fast swipe (accepted).
        /// </summary>
        RejectSlowDrag,

        /// <summary>
        /// Step 3: Player reads explanation of the par system (1-3 stars based on swipe count).
        /// No interaction required — just displays text for 3 seconds, then auto-advances.
        /// </summary>
        ExplainPar,

        /// <summary>
        /// Step 4: Player must swipe to reach a short finish line demo.
        /// Validates: player can complete a mini-race using the swipe mechanic.
        /// Success: kart crosses finish line.
        /// </summary>
        FinishLineDemo,

        /// <summary>
        /// Tutorial is complete. Persists 'tutorial_completed = true' to PlayerPrefs.
        /// </summary>
        Complete
    }
}
