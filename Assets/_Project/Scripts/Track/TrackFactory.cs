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

            // 1 gravel spot on track 1 (introduces obstacle gently)
            def.obstacleSpawns = new TrackDefinition.ObstacleSpawn[]
            {
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.5f,
                    lateralOffset = 0f,
                    size = new Vector2(3f, 4f),
                    teleportDistance = 0f
                }
            };

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

            // 1 power-up: Double Move (introduces the 2x mechanic)
            def.powerUpSpawns = new TrackDefinition.PowerUpSpawn[]
            {
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.5f,
                    lateralOffset = 0f,
                    powerUp = CreateDoubleMovePowerUp(),
                    oneTimeUse = false
                }
            };

            // 2 obstacles: 1 wall + 1 gravel
            def.obstacleSpawns = new TrackDefinition.ObstacleSpawn[]
            {
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.3f,
                    lateralOffset = 0f,
                    size = new Vector2(2f, 3f),  // Narrower than trackWidth (5), player can go around
                    teleportDistance = 0f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.7f,
                    lateralOffset = 0f,
                    size = new Vector2(3f, 3f),
                    teleportDistance = 0f
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
            def.trackWidth = 5f;  // Widened from 4 to 5 to prevent invisible wall issue
            def.lapCount = 1;
            def.defaultZoom = 14f;
            def.parSwipeCount = 14;
            def.maxSwipeCap = -1;  // 30 + 2*10 = 50

            // 6-segment loop with gentle S-curves (widened to prevent boundary overlap)
            def.splinePoints = new Vector2[]
            {
                // Segment 1: Start → top-right curve
                new Vector2(-18, 0),
                new Vector2(-16, 4),
                new Vector2(-12, 8),
                new Vector2(-6, 9),
                // Segment 2: Top straight with gentle S
                new Vector2(0, 8),
                new Vector2(4, 10),
                new Vector2(8, 8),
                // Segment 3: Right side going down
                new Vector2(12, 6),
                new Vector2(14, 2),
                new Vector2(13, -3),
                // Segment 4: Bottom right
                new Vector2(11, -7),
                new Vector2(6, -9),
                new Vector2(0, -10),
                // Segment 5: Bottom straight
                new Vector2(-6, -9),
                new Vector2(-10, -7),
                new Vector2(-13, -4),
                // Segment 6: Left side going up (back to start)
                new Vector2(-16, -2),
                new Vector2(-18, 0),  // wrap to start
            };

            // 3 power-ups: Boost + Bounce + Trail Wall
            def.powerUpSpawns = new TrackDefinition.PowerUpSpawn[]
            {
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.25f,
                    lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(),
                    oneTimeUse = false
                },
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.5f,
                    lateralOffset = 0f,
                    powerUp = CreateBouncePowerUp(),
                    oneTimeUse = false
                },
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.75f,
                    lateralOffset = 0f,
                    powerUp = CreateTrailWallPowerUp(),
                    oneTimeUse = false
                }
            };

            // 3 obstacles: 1 wall + 1 black hole + 1 gravel
            def.obstacleSpawns = new TrackDefinition.ObstacleSpawn[]
            {
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.2f,
                    lateralOffset = 0f,
                    size = new Vector2(2f, 3f),
                    teleportDistance = 0f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.55f,
                    lateralOffset = 0f,
                    size = new Vector2(1.5f, 1.5f),
                    teleportDistance = 8f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.85f,
                    lateralOffset = 0f,
                    size = new Vector2(3f, 4f),
                    teleportDistance = 0f
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

            // 4 power-ups: 2 Boost + Magnet + Double Move
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
                    t = 0.35f,
                    lateralOffset = 0f,
                    powerUp = CreateDoubleMovePowerUp(),
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

            // 4 obstacles: 2 walls + 1 black hole + 1 gravel
            def.obstacleSpawns = new TrackDefinition.ObstacleSpawn[]
            {
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.1f,
                    lateralOffset = 0f,
                    size = new Vector2(2f, 2f),
                    teleportDistance = 0f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.4f,
                    lateralOffset = 0f,
                    size = new Vector2(1.5f, 1.5f),
                    teleportDistance = 10f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.6f,
                    lateralOffset = 0f,
                    size = new Vector2(2f, 2f),
                    teleportDistance = 0f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.9f,
                    lateralOffset = 0f,
                    size = new Vector2(4f, 3f),
                    teleportDistance = 0f
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

        private static Racing.PowerUpBase CreateDoubleMovePowerUp()
        {
            return ScriptableObject.CreateInstance<Racing.PowerUps.DoubleMovePowerUp>();
        }

        private static Racing.PowerUpBase CreateTrailWallPowerUp()
        {
            return ScriptableObject.CreateInstance<Racing.PowerUps.TrailWallPowerUp>();
        }

        private static Racing.PowerUpBase CreateBouncePowerUp()
        {
            return ScriptableObject.CreateInstance<Racing.PowerUps.BouncePowerUp>();
        }

        private static Racing.PowerUpBase CreateSlingshotPowerUp()
        {
            return ScriptableObject.CreateInstance<Racing.PowerUps.SlingshotPowerUp>();
        }

        private static Racing.PowerUpBase CreatePhaseDodgePowerUp()
        {
            return ScriptableObject.CreateInstance<Racing.PowerUps.PhaseDodgePowerUp>();
        }

        private static Racing.PowerUpBase CreateRewindPowerUp()
        {
            return ScriptableObject.CreateInstance<Racing.PowerUps.RewindPowerUp>();
        }
    }
}
