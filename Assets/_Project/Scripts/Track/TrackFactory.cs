using UnityEngine;

namespace QuickChecks.Track
{
    /// <summary>
    /// Generates TrackDefinition assets for tracks 1-4 at runtime.
    /// Tracks are code-generated (Decision: code-generated track authoring).
    ///
    /// Track difficulty progression:
    ///   Track 1: First Lap (★) — Very forgiving: wide track, gentle curves, few obstacles
    ///   Track 2: Curves 101 (★) — Similar to Track 1, slightly more curves
    ///   Track 3: Chicane Sprint (★★) — Introduces tight sections, narrower
    ///   Track 4: Neon Sweep (★★) — Longer straights with boost power-ups
    /// </summary>
    public static class TrackFactory
    {
        /// <summary>
        /// Generates all 4 tracks (tracks 1-4).
        /// </summary>
        public static TrackDefinition[] GenerateAllTracks()
        {
            return new TrackDefinition[]
            {
                GenerateTrack1_FirstLap(),
                GenerateTrack2_Curves101(),
                GenerateTrack3_ChicaneSprint(),
                GenerateTrack4_NeonSweep(),
            };
        }

        /// <summary>
        /// Returns a default kart (Starter) for use when no specific kart is selected.
        /// </summary>
        public static Racing.KartStats GetDefaultKart()
        {
            var stats = ScriptableObject.CreateInstance<Racing.KartStats>();
            stats.kartId = "kart_starter";
            stats.displayName = "Starter";
            stats.impulseMultiplier = 1.1f;
            stats.frictionPerSecond = 0.22f;
            stats.stopThreshold = 8f;
            stats.maxSpeed = 250f;
            stats.boostMultiplier = 1.8f;
            stats.boostDurationSec = 1.5f;
            stats.unlockAfterTrackIndex = -1;
            return stats;
        }

        // --- Track 1: First Lap (★) — Very forgiving ---
        // Wide track (6 units), gentle oval, no obstacles, par 8

        public static TrackDefinition GenerateTrack1_FirstLap()
        {
            var def = ScriptableObject.CreateInstance<TrackDefinition>();
            def.trackId = "track_01_first_lap";
            def.displayName = "First Lap";
            def.difficultyStars = 1;
            def.isClosedLoop = true;
            def.isFree = true;
            def.trackIndex = 0;
            def.trackWidth = 6f;  // Wide — very forgiving
            def.lapCount = 1;
            def.defaultZoom = 16f;  // Zoomed out — see whole track
            def.parSwipeCount = 8;
            def.maxSwipeCap = -1;  // Uses formula: 30 + 1*10 = 40

            // Simple oval: 4 segments, 12 control points
            def.splinePoints = new Vector2[]
            {
                // Segment 1: (-15, 0) -> (0, 10)
                new Vector2(-15, 0),
                new Vector2(-15, 8),
                new Vector2(-7, 10),
                new Vector2(0, 10),
                // Segment 2: (0, 10) -> (15, 0)
                new Vector2(7, 10),
                new Vector2(15, 8),
                new Vector2(15, 0),
                // Segment 3: (15, 0) -> (0, -10)
                new Vector2(15, -8),
                new Vector2(7, -10),
                new Vector2(0, -10),
                // Segment 4: (0, -10) -> (-15, 0) [closed loop]
                new Vector2(-7, -10),
                new Vector2(-15, -8),
            };

            // No power-ups on track 1 (very forgiving, focus on learning swipe mechanic)
            def.powerUpSpawns = new TrackDefinition.PowerUpSpawn[0];

            return def;
        }

        // --- Track 2: Curves 101 (★) — Slightly more curves ---

        public static TrackDefinition GenerateTrack2_Curves101()
        {
            var def = ScriptableObject.CreateInstance<TrackDefinition>();
            def.trackId = "track_02_curves_101";
            def.displayName = "Curves 101";
            def.difficultyStars = 1;
            def.isClosedLoop = true;
            def.isFree = true;
            def.trackIndex = 1;
            def.trackWidth = 5f;  // Slightly narrower
            def.lapCount = 1;
            def.defaultZoom = 15f;
            def.parSwipeCount = 10;
            def.maxSwipeCap = -1;

            // 6-segment loop (18 control points) — more curves than track 1
            def.splinePoints = new Vector2[]
            {
                // Segment 1: (-15, 0) -> (-8, 8)
                new Vector2(-15, 0),
                new Vector2(-15, 5),
                new Vector2(-12, 8),
                new Vector2(-8, 8),
                // Segment 2: (-8, 8) -> (0, 10)
                new Vector2(-4, 8),
                new Vector2(-2, 10),
                new Vector2(0, 10),
                // Segment 3: (0, 10) -> (10, 5)
                new Vector2(4, 10),
                new Vector2(8, 8),
                new Vector2(10, 5),
                // Segment 4: (10, 5) -> (8, -5)
                new Vector2(12, 2),
                new Vector2(10, -2),
                new Vector2(8, -5),
                // Segment 5: (8, -5) -> (-5, -10)
                new Vector2(5, -8),
                new Vector2(0, -10),
                new Vector2(-5, -10),
                // Segment 6: (-5, -10) -> (-15, 0) [closed loop]
                new Vector2(-10, -8),
                new Vector2(-15, -5),
            };

            // 1 power-up: Boost at halfway
            def.powerUpSpawns = new TrackDefinition.PowerUpSpawn[]
            {
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.5f,
                    lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(),
                    oneTimeUse = false
                }
            };

            return def;
        }

        // --- Track 3: Chicane Sprint (★★) — Tight sections ---

        public static TrackDefinition GenerateTrack3_ChicaneSprint()
        {
            var def = ScriptableObject.CreateInstance<TrackDefinition>();
            def.trackId = "track_03_chicane_sprint";
            def.displayName = "Chicane Sprint";
            def.difficultyStars = 2;
            def.isClosedLoop = true;
            def.isFree = true;
            def.trackIndex = 2;
            def.trackWidth = 4f;  // Narrower — requires precision
            def.lapCount = 1;
            def.defaultZoom = 14f;
            def.parSwipeCount = 14;
            def.maxSwipeCap = -1;  // 30 + 2*10 = 50

            // 8-segment loop with chicanes (S-curves)
            def.splinePoints = new Vector2[]
            {
                // Segment 1: Start straight
                new Vector2(-18, 0),
                new Vector2(-15, 0),
                new Vector2(-12, 2),
                new Vector2(-10, 5),
                // Segment 2: Chicane right
                new Vector2(-8, 8),
                new Vector2(-5, 6),
                new Vector2(-3, 3),
                // Segment 3: Chicane left
                new Vector2(0, 5),
                new Vector2(3, 8),
                new Vector2(6, 6),
                // Segment 4: Right turn
                new Vector2(8, 3),
                new Vector2(10, 0),
                new Vector2(12, -3),
                // Segment 5: Bottom straight
                new Vector2(10, -6),
                new Vector2(5, -8),
                new Vector2(0, -9),
                // Segment 6: Bottom left
                new Vector2(-5, -10),
                new Vector2(-10, -8),
                new Vector2(-13, -5),
                // Segment 7: Left turn up
                new Vector2(-16, -3),
                new Vector2(-18, 0),  // back to start (closed loop)
                // Note: segment 7 only needs 2 points (p1, p2) since p3 wraps to points[0]
                // But for 7 segments we need 21 points (3*7). Let me recount...
                // Actually 7 segments = 21 points. We have 20 above. Let me fix.
            };

            // Fix: we need exactly 3*N points for N segments. Let me use 6 segments (18 points) instead.
            def.splinePoints = new Vector2[]
            {
                // Segment 1: Start → first chicane
                new Vector2(-18, 0),
                new Vector2(-14, 1),
                new Vector2(-10, 4),
                new Vector2(-8, 7),
                // Segment 2: Chicane right
                new Vector2(-5, 9),
                new Vector2(-2, 7),
                new Vector2(0, 4),
                // Segment 3: Chicane left + right turn
                new Vector2(3, 7),
                new Vector2(7, 8),
                new Vector2(10, 5),
                // Segment 4: Right side down
                new Vector2(12, 2),
                new Vector2(13, -2),
                new Vector2(11, -6),
                // Segment 5: Bottom
                new Vector2(7, -9),
                new Vector2(0, -10),
                new Vector2(-7, -9),
                // Segment 6: Left side up (back to start)
                new Vector2(-13, -6),
                new Vector2(-17, -3),
            };

            // 2 power-ups: Boost + Shield
            def.powerUpSpawns = new TrackDefinition.PowerUpSpawn[]
            {
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.33f,
                    lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(),
                    oneTimeUse = false
                },
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.66f,
                    lateralOffset = 0f,
                    powerUp = CreateShieldPowerUp(),
                    oneTimeUse = false
                }
            };

            return def;
        }

        // --- Track 4: Neon Sweep (★★) — Long straights with boosts ---

        public static TrackDefinition GenerateTrack4_NeonSweep()
        {
            var def = ScriptableObject.CreateInstance<TrackDefinition>();
            def.trackId = "track_04_neon_sweep";
            def.displayName = "Neon Sweep";
            def.difficultyStars = 2;
            def.isClosedLoop = true;
            def.isFree = false;  // First paid track (Decision 16: tracks 4-8 require IAP)
            def.trackIndex = 3;
            def.trackWidth = 4.5f;
            def.lapCount = 1;
            def.defaultZoom = 15f;
            def.parSwipeCount = 16;
            def.maxSwipeCap = -1;

            // 6-segment loop with long straights
            def.splinePoints = new Vector2[]
            {
                // Segment 1: Long straight (bottom)
                new Vector2(-20, -8),
                new Vector2(-15, -8),
                new Vector2(-10, -8),
                new Vector2(-5, -8),
                // Segment 2: Right turn
                new Vector2(0, -8),
                new Vector2(5, -6),
                new Vector2(8, -3),
                // Segment 3: Up the right side
                new Vector2(10, 0),
                new Vector2(10, 4),
                new Vector2(8, 7),
                // Segment 4: Top straight (left)
                new Vector2(5, 9),
                new Vector2(0, 10),
                new Vector2(-5, 10),
                // Segment 5: Left turn
                new Vector2(-10, 10),
                new Vector2(-15, 8),
                new Vector2(-18, 5),
                // Segment 6: Down left side (back to start)
                new Vector2(-20, 2),
                new Vector2(-20, -4),
            };

            // 3 power-ups: 2 Boosts + 1 Magnet (on long straights)
            def.powerUpSpawns = new TrackDefinition.PowerUpSpawn[]
            {
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.15f,  // On the bottom straight
                    lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(),
                    oneTimeUse = false
                },
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.5f,  // On the top straight
                    lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(),
                    oneTimeUse = false
                },
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.8f,  // On the left side
                    lateralOffset = 0f,
                    powerUp = CreateMagnetPowerUp(),
                    oneTimeUse = false
                }
            };

            return def;
        }

        // --- Power-up factory methods (same as PrototypeBootstrapper) ---

        private static Racing.PowerUpBase CreateBoostPowerUp()
        {
            return ScriptableObject.CreateInstance<Racing.PowerUps.BoostPowerUp>();
        }

        private static Racing.PowerUpBase CreateShieldPowerUp()
        {
            return ScriptableObject.CreateInstance<Racing.PowerUps.ShieldPowerUp>();
        }

        private static Racing.PowerUpBase CreateMagnetPowerUp()
        {
            return ScriptableObject.CreateInstance<Racing.PowerUps.MagnetPowerUp>();
        }
    }
}
