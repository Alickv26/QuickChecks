using System.Collections.Generic;
using UnityEngine;

namespace QuickChecks.Track
{
    /// <summary>
    /// Cubic Bezier spline math utilities. A spline is a sequence of Bezier
    /// segments; each segment has 4 control points (p0, p1, p2, p3) where
    /// p0 is the start, p3 is the end, and p1/p2 are tangent handles.
    ///
    /// For continuity, the end of segment N equals the start of segment N+1
    /// (shared control point). This gives a smooth curve through all points.
    ///
    /// TrackDefinition.splinePoints stores control points as a flat array.
    /// For a CLOSED LOOP (which our tracks always are):
    ///   - Segment 0: p0, p1, p2, p3   (4 points stored)
    ///   - Segment 1: p1, p2, p3       (3 points, p0 = previous p3)
    ///   - Segment 2: p1, p2, p3       (3 points)
    ///   - Segment N-1 (last): p1, p2  (only 2 points, p3 = segment 0's p0)
    ///
    /// So total points for N segments in a closed loop = 4 + 3*(N-2) + 2 = 3N.
    /// For example: 4 segments = 12 points, 6 segments = 18 points.
    ///
    /// For an OPEN spline (not closed):
    ///   - Total = 3N + 1 (last segment stores its p3 explicitly).
    /// </summary>
    public static class BezierSpline
    {
        /// <summary>
        /// Number of control points needed for N segments.
        /// CLOSED LOOP: 3N (last segment's p3 wraps to first segment's p0).
        /// OPEN: 3N + 1 (last segment stores its p3 explicitly).
        /// </summary>
        public static int ControlPointCount(int segmentCount, bool closedLoop = true)
        {
            if (segmentCount <= 0) return 0;
            return closedLoop ? 3 * segmentCount : 3 * segmentCount + 1;
        }

        /// <summary>
        /// Number of segments in a spline with the given control point count.
        /// Assumes closed loop (3N points). For open spline (3N+1), use SegmentCount(count, false).
        /// </summary>
        public static int SegmentCount(int controlPointCount, bool closedLoop = true)
        {
            if (closedLoop)
            {
                if (controlPointCount < 3) return 0;
                // closed loop: 3N points, so N = points / 3. Must be divisible by 3.
                if (controlPointCount % 3 != 0) return 0;
                return controlPointCount / 3;
            }
            else
            {
                // open: 3N + 1 points, so N = (points - 1) / 3
                if (controlPointCount < 4) return 0;
                if ((controlPointCount - 1) % 3 != 0) return 0;
                return (controlPointCount - 1) / 3;
            }
        }

        /// <summary>
        /// Evaluate a point on a cubic Bezier segment.
        /// t ranges from 0 (start, p0) to 1 (end, p3).
        /// </summary>
        public static Vector2 EvaluateSegment(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
        }

        /// <summary>
        /// Evaluate the tangent (first derivative) of a cubic Bezier segment.
        /// Returns a normalized direction vector.
        /// </summary>
        public static Vector2 EvaluateTangent(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;

            Vector2 tangent = 3f * uu * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * tt * (p3 - p2);
            return tangent.normalized;
        }

        /// <summary>
        /// Evaluate a point on the spline at parameter t (0 = start, 1 = end of entire spline).
        /// For a closed loop, t=0 and t=1 give the same point.
        /// </summary>
        public static Vector2 EvaluateSpline(IReadOnlyList<Vector2> points, float t, bool closedLoop)
        {
            int segCount = SegmentCount(points.Count, closedLoop);
            if (segCount == 0) return Vector2.zero;

            // Wrap t to [0, 1)
            t = Mathf.Repeat(t, 1f);

            // Find which segment t falls into
            float scaled = t * segCount;
            int segIndex = Mathf.FloorToInt(scaled);
            if (segIndex >= segCount) segIndex = segCount - 1;
            float localT = scaled - segIndex;

            int baseIdx = segIndex * 3;
            Vector2 p0 = points[baseIdx];
            Vector2 p1 = points[baseIdx + 1];
            Vector2 p2 = points[baseIdx + 2];
            Vector2 p3 = closedLoop && segIndex == segCount - 1
                ? points[0] // closed loop: last segment ends at first point
                : points[baseIdx + 3];

            return EvaluateSegment(p0, p1, p2, p3, localT);
        }

        /// <summary>
        /// Evaluate the tangent (direction) of the spline at parameter t.
        /// </summary>
        public static Vector2 EvaluateSplineTangent(IReadOnlyList<Vector2> points, float t, bool closedLoop)
        {
            int segCount = SegmentCount(points.Count, closedLoop);
            if (segCount == 0) return Vector2.right;

            t = Mathf.Repeat(t, 1f);
            float scaled = t * segCount;
            int segIndex = Mathf.FloorToInt(scaled);
            if (segIndex >= segCount) segIndex = segCount - 1;
            float localT = scaled - segIndex;

            int baseIdx = segIndex * 3;
            Vector2 p0 = points[baseIdx];
            Vector2 p1 = points[baseIdx + 1];
            Vector2 p2 = points[baseIdx + 2];
            Vector2 p3 = closedLoop && segIndex == segCount - 1
                ? points[0]
                : points[baseIdx + 3];

            return EvaluateTangent(p0, p1, p2, p3, localT);
        }

        /// <summary>
        /// Compute the normal (perpendicular to tangent) at parameter t.
        /// Points to the LEFT of the spline direction (standard 2D convention).
        /// </summary>
        public static Vector2 EvaluateSplineNormal(IReadOnlyList<Vector2> points, float t, bool closedLoop)
        {
            Vector2 tangent = EvaluateSplineTangent(points, t, closedLoop);
            // Rotate -90 degrees to get left normal (or +90 for right).
            return new Vector2(-tangent.y, tangent.x);
        }

        /// <summary>
        /// Approximate length of the spline by sampling N points.
        /// 50 samples is enough for most track-length calculations.
        /// </summary>
        public static float ApproximateLength(IReadOnlyList<Vector2> points, bool closedLoop, int samples = 50)
        {
            int segCount = SegmentCount(points.Count, closedLoop);
            if (segCount == 0) return 0f;

            float total = 0f;
            Vector2 prev = EvaluateSpline(points, 0f, closedLoop);
            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector2 cur = EvaluateSpline(points, t, closedLoop);
                total += Vector2.Distance(prev, cur);
                prev = cur;
            }
            return total;
        }

        /// <summary>
        /// Sample N evenly-spaced points along the spline (by parameter, not arc length).
        /// Useful for generating checkpoint positions or visual mesh vertices.
        /// </summary>
        public static List<Vector2> SamplePoints(IReadOnlyList<Vector2> points, int sampleCount, bool closedLoop)
        {
            var result = new List<Vector2>(sampleCount);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                result.Add(EvaluateSpline(points, t, closedLoop));
            }
            return result;
        }

        /// <summary>
        /// Sample the left/right boundary points (offset from centerline by half-width).
        /// Returns two lists: leftBoundary, rightBoundary.
        /// </summary>
        public static void SampleBoundaries(
            IReadOnlyList<Vector2> points,
            float halfWidth,
            int sampleCount,
            bool closedLoop,
            out List<Vector2> leftBoundary,
            out List<Vector2> rightBoundary)
        {
            leftBoundary = new List<Vector2>(sampleCount);
            rightBoundary = new List<Vector2>(sampleCount);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                Vector2 center = EvaluateSpline(points, t, closedLoop);
                Vector2 normal = EvaluateSplineNormal(points, t, closedLoop);
                leftBoundary.Add(center + normal * halfWidth);
                rightBoundary.Add(center - normal * halfWidth);
            }
        }
    }
}
