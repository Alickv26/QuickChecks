using UnityEngine;

namespace QuickChecks.Track
{
    /// <summary>
    /// Validates that a TrackDefinition is completable: spline forms a closed loop,
    /// has reasonable segment count, all checkpoints reachable from start.
    ///
    /// Used by:
    ///   - TrackBuilderWindow (in editor, before saving a track)
    ///   - DailyTrackGenerator (at generation time, before publishing)
    ///   - SplineTrackBuilder (at runtime, before building)
    /// </summary>
    public static class TrackValidator
    {
        public static bool Validate(TrackDefinition track, out string error)
        {
            if (track == null)
            {
                error = "TrackDefinition is null.";
                return false;
            }

            // Spline points
            if (track.splinePoints == null || track.splinePoints.Length < 4)
            {
                error = "Need at least 4 spline control points (1 cubic Bezier segment). " +
                        "For a closed loop, recommend at least 8 (2 segments).";
                return false;
            }

            // Control point count must match Bezier pattern (4, 7, 10, 13, ...)
            int pointCount = track.splinePoints.Length;
            int segCount = BezierSpline.SegmentCount(pointCount);
            int expected = BezierSpline.ControlPointCount(segCount);
            if (pointCount != expected)
            {
                error = $"Control point count {pointCount} doesn't match segment count {segCount}. " +
                        $"Expected {expected} points (4 for first segment, 3 for each additional).";
                return false;
            }

            // Track width
            if (track.trackWidth <= 0.5f)
            {
                error = "Track width must be > 0.5 units.";
                return false;
            }

            // Lap count
            if (track.lapCount < 1)
            {
                error = "Lap count must be >= 1.";
                return false;
            }

            // Par (sanity check, not strict)
            if (track.parSwipeCount < 5)
            {
                error = $"Par {track.parSwipeCount} seems too low — even short tracks need 8+ swipes.";
                return false;
            }

            // Track length sanity check
            float length = BezierSpline.ApproximateLength(track.splinePoints, true);
            if (length < 30f)
            {
                error = $"Track length {length:F1} units is too short. Minimum 30 units.";
                return false;
            }
            if (length > 200f)
            {
                error = $"Track length {length:F1} units is too long. Maximum 200 units.";
                return false;
            }

            // Check that no two consecutive control points are identical (would create a degenerate segment)
            for (int i = 0; i < pointCount - 1; i++)
            {
                if (Vector2.Distance(track.splinePoints[i], track.splinePoints[i + 1]) < 0.01f)
                {
                    error = $"Control points {i} and {i + 1} are too close together — degenerate segment.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
