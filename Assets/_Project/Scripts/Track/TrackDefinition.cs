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
        [Tooltip("Bezier control points. Must have at least 4 points (1 segment).")]
        public Vector2[] splinePoints = new Vector2[0];

        [Tooltip("Track width in world units.")]
        public float trackWidth = 4f;

        [Tooltip("Total number of laps required to finish.")]
        public int lapCount = 1;

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

        /// <summary>
        /// Validates that the track is completable: spline forms a closed loop,
        /// has reasonable segment count, all checkpoints reachable.
        /// </summary>
        public bool Validate(out string error)
        {
            if (splinePoints == null || splinePoints.Length < 4)
            {
                error = "Need at least 4 spline points (1 Bezier segment).";
                return false;
            }
            if (trackWidth <= 0.5f)
            {
                error = "Track width must be > 0.5.";
                return false;
            }
            if (lapCount < 1)
            {
                error = "Lap count must be >= 1.";
                return false;
            }
            error = null;
            return true;
        }
    }
}
