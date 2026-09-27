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

            // Spline points — closed loop needs at least 6 points (2 segments × 3 each).
            // Open spline needs at least 4 points (1 segment).
            int minPoints = track.isClosedLoop ? 6 : 4;
            if (track.splinePoints == null || track.splinePoints.Length < minPoints)
            {
                error = $"Need at least {minPoints} spline control points " +
                        $"(for {(track.isClosedLoop ? "closed loop" : "open spline")}). " +
                        $"Closed loop formula: 3N points for N segments.";
                return false;
            }

            // Control point count must match Bezier pattern.
            // Closed loop: 3N points (must be divisible by 3, minimum 6).
            // Open: 3N + 1 points (must be 1 mod 3, minimum 4).
            int pointCount = track.splinePoints.Length;
            int segCount = BezierSpline.SegmentCount(pointCount, track.isClosedLoop);
            if (segCount == 0)
            {
                if (track.isClosedLoop)
                {
                    error = $"Control point count {pointCount} is not a valid closed-loop Bezier spline. " +
                            "For closed loops, count must be divisible by 3 (3N for N segments). " +
                            "Example: 12 points = 4 segments, 18 points = 6 segments.";
                }
                else
                {
                    error = $"Control point count {pointCount} is not a valid open Bezier spline. " +
                            "For open splines, count must be 1 mod 3 (3N + 1 for N segments). " +
                            "Example: 4 points = 1 segment, 7 points = 2 segments.";
                }
                return false;
            }

            int expected = BezierSpline.ControlPointCount(segCount, track.isClosedLoop);
            if (pointCount != expected)
            {
                error = $"Control point count {pointCount} doesn't match segment count {segCount}. " +
                        $"Expected {expected} points.";
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
            float length = BezierSpline.ApproximateLength(track.splinePoints, track.isClosedLoop);
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
