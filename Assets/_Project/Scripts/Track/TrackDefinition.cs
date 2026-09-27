using UnityEngine;

namespace QuickChecks.Track
{
    /// <summary>
    /// Defines a single handcrafted track: spline path, width, checkpoints,
    /// power-up spawn points, camera zoom hints, and visual style.
    ///
    /// Created by designers via the Track Builder editor tool (Tools > Track Builder).
    /// </summary>
    [CreateAssetMenu(menuName = "QuickChecks/Track Definition", fileName = "NewTrackDefinition")]
    public class TrackDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string trackId;
        public string displayName;
        public int difficultyStars = 1;
        public Sprite previewImage;

        [Header("Spline Path")]
        [Tooltip("Bezier control points. Layout: [p0, p1, p2, p3, p1, p2, p3, p1, p2, p3, ...] " +
                 "where the first segment uses 4 points (p0, p1, p2, p3) and each subsequent " +
                 "segment uses 3 (p0 is shared with previous segment's p3).")]
        public Vector2[] splinePoints = new Vector2[0];

        [Tooltip("If true, the track is a closed loop (last p3 = first p0). Required for lap racing.")]
        public bool isClosedLoop = true;

        [Tooltip("Track width in world units.")]
        public float trackWidth = 4f;

        [Tooltip("Total number of laps required to finish.")]
        public int lapCount = 1;

        [Header("Freemium Gating (Decision 16 — see DECISIONS.md)")]
        [Tooltip("If true, this track is available in the free demo. If false, requires IAP unlock.")]
        public bool isFree = false;

        [Header("Camera")]
        [Tooltip("Default ortho camera size when on this track.")]
        public float defaultZoom = 12f;

        [Tooltip("If true, segments can override the camera zoom (see segmentZoomHints).")]
        public bool useSegmentZoomHints = true;

        [Header("Style")]
        public Color trackColor = new Color(0.106f, 0.165f, 0.165f, 1f);
        public Color borderColor = new Color(0.247f, 0.878f, 0.760f, 1f);
        public Color backgroundColor = new Color(0.055f, 0.078f, 0.078f, 1f);

        [Header("Unlock")]
        [Tooltip("Track index (0-based) — track N is unlocked by completing track N-1 under target time.")]
        public int trackIndex = 0;

        [Tooltip("Time (seconds) required on previous track to unlock this one.")]
        public float unlockTimeOnPreviousTrack = 60f;

        [Header("Swipe Cap (Decision 1 — see DECISIONS.md)")]
        [Tooltip("Max swipes per player before DNF. Set to -1 to use formula: 30 + (difficultyStars * 10).")]
        public int maxSwipeCap = -1;

        [Tooltip("Par = theoretical minimum swipe count to finish. Used for star ratings + leaderboard comparison.")]
        public int parSwipeCount = 20;

        [Header("CPU AI (Decision 3 — see DECISIONS.md)")]
        [Tooltip("Optional: URL to a hand-authored 'optimal line' ghost. Hard CPU follows this with ±3° noise. Leave empty for v1 (uses heuristic).")]
        public string optimalLineGhostUrl = "";

        /// <summary>
        /// Resolves the effective max swipe cap. If designer set -1, falls back to formula.
        /// </summary>
        public int GetMaxSwipeCap()
        {
            return maxSwipeCap > 0 ? maxSwipeCap : 30 + (difficultyStars * 10);
        }

        /// <summary>
        /// Validates that the track is completable. Delegates to TrackValidator.
        /// </summary>
        public bool Validate(out string error)
        {
            return TrackValidator.Validate(this, out error);
        }
    }
}
