using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuickChecks.Track
{
    /// <summary>
    /// Generates a procedurally-generated track per day, same for all players globally.
    /// Resets at UTC midnight (Decision 19: missed dailies gone forever).
    ///
    /// Difficulty rotates by day of week (Decision 18):
    ///   Monday    = Easy (★)
    ///   Tuesday   = Easy-Medium (★½) -> rounded to ★
    ///   Wednesday = Medium (★★)
    ///   Thursday  = Medium-Hard (★★½) -> rounded to ★★
    ///   Friday    = Hard (★★★)
    ///   Saturday  = Expert (★★★★)
    ///   Sunday    = Expert+ (★★★★★)
    ///
    /// Generation algorithm:
    ///   - Seed = YYYYMMDD integer (e.g., 20260927 for Sept 27, 2026)
    ///   - Generates N control points in a roughly circular layout
    ///   - Adds tangential noise for variety
    ///   - Validates with TrackValidator
    ///   - Returns a TrackDefinition with parSwipeCount derived from difficulty
    /// </summary>
    public static class DailyTrackGenerator
    {
        /// <summary>
        /// Generates today's daily track (UTC date).
        /// </summary>
        public static TrackDefinition GenerateForToday()
        {
            return GenerateForDate(DateTime.UtcNow);
        }

        /// <summary>
        /// Generates the daily track for a specific date (useful for testing).
        /// </summary>
        public static TrackDefinition GenerateForDate(DateTime date)
        {
            int seed = date.Year * 10000 + date.Month * 100 + date.Day;
            DayOfWeek dayOfWeek = date.DayOfWeek;
            int difficulty = GetDifficultyForDay(dayOfWeek);

            return Generate(seed, date, difficulty);
        }

        /// <summary>
        /// Returns the difficulty (1-5 stars) for a given day of week.
        /// </summary>
        public static int GetDifficultyForDay(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => 1,    // Easy
                DayOfWeek.Tuesday => 1,   // Easy-Medium (rounded)
                DayOfWeek.Wednesday => 2, // Medium
                DayOfWeek.Thursday => 2,  // Medium-Hard (rounded)
                DayOfWeek.Friday => 3,   // Hard
                DayOfWeek.Saturday => 4,  // Expert
                DayOfWeek.Sunday => 5,   // Expert+
                _ => 2
            };
        }

        /// <summary>
        /// Generates a track with the given seed + difficulty.
        /// </summary>
        public static TrackDefinition Generate(int seed, DateTime date, int difficulty)
        {
            var rng = new System.Random(seed);

            var def = ScriptableObject.CreateInstance<TrackDefinition>();
            def.trackId = $"daily_{date:yyyyMMdd}";
            def.displayName = $"Daily Track — {date:MMM d}";
            def.isClosedLoop = true;
            def.isFree = true;  // Daily tracks are free to play (Decision 8)
            def.trackIndex = -1;  // Not part of the main track progression
            def.difficultyStars = difficulty;
            def.lapCount = 1;
            def.useSegmentZoomHints = false;

            // Track length + width scale with difficulty
            float trackLength = 40f + difficulty * 8f;  // 48 (easy) -> 80 (expert+)
            float trackWidth = Mathf.Max(2f, 5f - difficulty * 0.4f);  // 4.6 (easy) -> 3.0 (expert+)
            def.trackWidth = trackWidth;
            def.defaultZoom = 12f + difficulty * 0.5f;

            // Generate control points: 6-10 segments in a closed loop
            // (Closed loop with N segments needs 3N control points)
            int segmentCount = 6 + Mathf.FloorToInt(difficulty * 0.5f);  // 6 (easy) -> 8 (expert+)
            int pointCount = BezierSpline.ControlPointCount(segmentCount, true);

            // Generate centerline points: roughly circular with tangential noise
            var centerline = new Vector2[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                float angle = (float)i / segmentCount * Mathf.PI * 2f;
                // Radius varies with difficulty: more variation = harder track
                float baseRadius = trackLength * 0.5f;
                float noiseAmplitude = 3f + difficulty * 1.5f;
                float radiusNoise = (float)(rng.NextDouble() - 0.5) * noiseAmplitude;
                float radius = baseRadius + radiusNoise;
                centerline[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            // Build Bezier control points: for each segment, p0=centerline[i], p3=centerline[(i+1)%N],
            // and p1/p2 are tangent handles offset along the spline direction.
            def.splinePoints = new Vector2[pointCount];
            for (int i = 0; i < segmentCount; i++)
            {
                int baseIdx = i * 3;
                Vector2 p0 = centerline[i];
                Vector2 p3 = centerline[(i + 1) % segmentCount];
                Vector2 direction = (p3 - p0).normalized;
                float segmentLength = Vector2.Distance(p0, p3);

                // Tangent handles: offset 1/3 of segment length along direction
                float handleDistance = segmentLength * 0.33f;
                // Add some noise to handle direction for variety
                float noiseAngle = (float)(rng.NextDouble() - 0.5) * 30f * difficulty;
                Vector2 rotatedDir = Rotate(direction, noiseAngle);

                Vector2 p1 = p0 + rotatedDir * handleDistance;
                Vector2 p2 = p3 - rotatedDir * handleDistance;

                def.splinePoints[baseIdx] = p0;
                if (i < segmentCount - 1)
                {
                    def.splinePoints[baseIdx + 1] = p1;
                    def.splinePoints[baseIdx + 2] = p2;
                    // p3 is stored as the p0 of the next segment (shared)
                }
                else
                {
                    // Last segment: store p1 and p2 (p3 wraps to points[0])
                    def.splinePoints[baseIdx + 1] = p1;
                    def.splinePoints[baseIdx + 2] = p2;
                }
            }

            // Compute par based on track length (designer-set target — Decision 21 says designer-set,
            // but for procedural daily tracks we use a formula)
            float actualLength = BezierSpline.ApproximateLength(def.splinePoints, true);
            def.parSwipeCount = Mathf.RoundToInt(actualLength / 8f);  // ~8 units per swipe
            def.maxSwipeCap = -1;  // Use formula: 30 + difficulty * 10

            // Add 1-3 power-up spawns (depending on difficulty)
            int powerUpCount = Mathf.Min(3, Mathf.Max(1, difficulty));
            def.powerUpSpawns = new TrackDefinition.PowerUpSpawn[powerUpCount];
            for (int i = 0; i < powerUpCount; i++)
            {
                def.powerUpSpawns[i] = new TrackDefinition.PowerUpSpawn
                {
                    t = (float)(i + 1) / (powerUpCount + 1),
                    lateralOffset = 0f,
                    powerUp = null,  // Designer assigns concrete power-up assets per track (in production)
                    oneTimeUse = false
                };
            }

            // Validate before returning
            if (!def.Validate(out string error))
            {
                Debug.LogError($"[DailyTrackGenerator] Generated invalid daily track for {date:yyyy-MM-dd}: {error}");
                // Return a fallback simple oval track
                return GenerateFallback(date, difficulty);
            }

            Debug.Log($"[DailyTrackGenerator] Generated daily track for {date:yyyy-MM-dd} " +
                      $"({dayOfWeekName(date.DayOfWeek)}, {difficulty}★): " +
                      $"{segmentCount} segments, length {actualLength:F1} units, par {def.parSwipeCount} swipes.");
            return def;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private static string dayOfWeekName(DayOfWeek day) => day.ToString();

        private static TrackDefinition GenerateFallback(DateTime date, int difficulty)
        {
            // Simple 4-segment oval — same as the prototype track
            var def = ScriptableObject.CreateInstance<TrackDefinition>();
            def.trackId = $"daily_{date:yyyyMMdd}_fallback";
            def.displayName = $"Daily Track — {date:MMM d}";
            def.isClosedLoop = true;
            def.isFree = true;
            def.difficultyStars = difficulty;
            def.lapCount = 1;
            def.trackWidth = 4f;
            def.defaultZoom = 14f;
            def.parSwipeCount = 8;
            def.maxSwipeCap = -1;

            def.splinePoints = new Vector2[]
            {
                new Vector2(-15, 0),
                new Vector2(-15, 7),
                new Vector2(-7, 10),
                new Vector2(0, 10),
                new Vector2(7, 10),
                new Vector2(15, 7),
                new Vector2(15, 0),
                new Vector2(15, -7),
                new Vector2(7, -10),
                new Vector2(0, -10),
                new Vector2(-7, -10),
                new Vector2(-15, -7),
            };

            return def;
        }
    }
}
