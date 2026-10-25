using UnityEngine;

namespace QuickChecks.Track
{
    /// <summary>
    /// Generates TrackDefinition assets for tracks 1-8 at runtime.
    /// Tracks are code-generated (Decision: code-generated track authoring).
    ///
    /// Track difficulty progression (PLAN.md Section 4.3):
    ///   Track 1: First Lap        (★)     Par 8   Free    Wide oval, gentle curves
    ///   Track 2: Curves 101       (★)     Par 10  Free    Slightly more curves
    ///   Track 3: Chicane Sprint   (★★)   Par 14  Free    Tight chicanes
    ///   Track 4: Neon Sweep       (★★)   Par 16  Paid    Long straights + boost pickups
    ///   Track 5: Hairpin Highway  (★★★)  Par 28  Paid    Tight U-turns + gravel + black holes
    ///   Track 6: Slipstream       (★★★)  Par 32  Paid    Long straights + boost chains
    ///   Track 7: Maze Run         (★★★★) Par 40  Paid    Branching paths, decision points
    ///   Track 8: Final Check      (★★★★★) Par 50  Paid    Gauntlet — all obstacle types
    /// </summary>
    public static class TrackFactory
    {
        /// <summary>
        /// Generates all 8 tracks.
        /// </summary>
        public static TrackDefinition[] GenerateAllTracks()
        {
            return new TrackDefinition[]
            {
                GenerateTrack1_FirstLap(),
                GenerateTrack2_Curves101(),
                GenerateTrack3_ChicaneSprint(),
                GenerateTrack4_NeonSweep(),
                GenerateTrack5_HairpinHighway(),
                GenerateTrack6_Slipstream(),
                GenerateTrack7_MazeRun(),
                GenerateTrack8_FinalCheck(),
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

            // 1 gravel spot on track 1 (introduces obstacle gently — long enough to actually slow you down)
            def.obstacleSpawns = new TrackDefinition.ObstacleSpawn[]
            {
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.5f,
                    lateralOffset = 0f,
                    size = new Vector2(6f, 3f),  // 6 units along track, 3 across — multiple swipes to escape
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

            // 2 obstacles: 1 wall + 1 gravel (SMALLER sizes for navigability)
            def.obstacleSpawns = new TrackDefinition.ObstacleSpawn[]
            {
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.3f,
                    lateralOffset = 1.0f,
                    size = new Vector2(1.5f, 1.5f),  // Small wall — plenty of room around
                    teleportDistance = 0f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.7f,
                    lateralOffset = 0f,
                    size = new Vector2(7f, 3f),  // Long gravel trap — punishes misjudged swipes
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
                new Vector2(-17, -1),
                new Vector2(-18, -2),
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
                    lateralOffset = 1.0f,
                    size = new Vector2(1.5f, 1.5f),  // Small wall
                    teleportDistance = 0f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.55f,
                    lateralOffset = 0f,
                    size = new Vector2(1.0f, 1.0f),  // Small black hole
                    teleportDistance = 8f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.85f,
                    lateralOffset = 0f,
                    size = new Vector2(8f, 3.5f),  // Long gravel trap before the finish — punishes a sloppy approach
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
            def.isFree = true;  // TEMP: free for beta testing
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

            // 4 obstacles: 2 walls + 1 black hole + 1 gravel (ALL SMALL)
            def.obstacleSpawns = new TrackDefinition.ObstacleSpawn[]
            {
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.1f,
                    lateralOffset = 1.0f,
                    size = new Vector2(1.5f, 1.5f),  // Small wall
                    teleportDistance = 0f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.4f,
                    lateralOffset = 0f,
                    size = new Vector2(1.0f, 1.0f),  // Small black hole
                    teleportDistance = 10f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.6f,
                    lateralOffset = -1.0f,
                    size = new Vector2(1.5f, 1.5f),  // Small wall
                    teleportDistance = 0f
                },
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.9f,
                    lateralOffset = 0f,
                    size = new Vector2(9f, 4f),  // Final long gravel trap — meaningful slowdown
                    teleportDistance = 0f
                }
            };

            return def;
        }

        // --- Track 5: Hairpin Highway (★★★) — Tight U-turns + gravel + black holes ---
        // Identity: precision track where every swipe matters. Tight hairpin turns punish
        // overshoots. Long gravel traps slow you mid-corner. Black holes rewind you back
        // 4 swipes if you mistime an approach. Par 28, maxSwipeCap 60.

        public static TrackDefinition GenerateTrack5_HairpinHighway()
        {
            var def = ScriptableObject.CreateInstance<TrackDefinition>();
            def.trackId = "track_05_hairpin_highway";
            def.displayName = "Hairpin Highway";
            def.difficultyStars = 3;
            def.isClosedLoop = true;
            def.isFree = true;  // TEMP: free for beta testing
            def.trackIndex = 4;
            def.trackWidth = 4f;  // Narrower — punishes overshoots
            def.lapCount = 1;
            def.defaultZoom = 14f;  // Zoomed in closer — precision mode
            def.parSwipeCount = 28;
            def.maxSwipeCap = -1;  // 30 + 3*10 = 60

            // 8-segment loop with hairpin U-turns at the four corners
            def.splinePoints = new Vector2[]
            {
                // Segment 1: Approach to first hairpin (top-right)
                new Vector2(-18, 8),
                new Vector2(-12, 9),
                new Vector2(-6, 10),
                new Vector2(0, 10),
                // Segment 2: First hairpin (right side, U-turn down)
                new Vector2(6, 10),
                new Vector2(10, 9),
                new Vector2(12, 6),
                new Vector2(10, 3),
                // Segment 3: Down the right side
                new Vector2(8, 0),
                new Vector2(8, -4),
                new Vector2(10, -7),
                // Segment 4: Second hairpin (bottom-right, U-turn left)
                new Vector2(12, -10),
                new Vector2(8, -11),
                new Vector2(4, -10),
                // Segment 5: Bottom straight (left)
                new Vector2(-2, -10),
                new Vector2(-8, -10),
                new Vector2(-12, -9),
                // Segment 6: Third hairpin (bottom-left, U-turn up)
                new Vector2(-15, -7),
                new Vector2(-14, -3),
                new Vector2(-12, 0),
                // Segment 7: Up the left side
                new Vector2(-14, 4),
                new Vector2(-16, 5),
                // Segment 8: Approach finish line from the left (aligned with start direction)
                new Vector2(-20, 7),
                new Vector2(-20, 8),
            };

            // 4 power-ups: Phase Dodge (for hairpins) + Shield (for gravel) + 2 Boost
            def.powerUpSpawns = new TrackDefinition.PowerUpSpawn[]
            {
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.15f, lateralOffset = 0f,
                    powerUp = CreatePhaseDodgePowerUp(), oneTimeUse = false
                },
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.35f, lateralOffset = 0f,
                    powerUp = CreateShieldPowerUp(), oneTimeUse = false
                },
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.60f, lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(), oneTimeUse = false
                },
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.85f, lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(), oneTimeUse = false
                }
            };

            // 5 obstacles: 2 long gravel traps + 2 black holes + 1 wall
            def.obstacleSpawns = new TrackDefinition.ObstacleSpawn[]
            {
                // Long gravel trap in the first hairpin approach
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.20f, lateralOffset = 0f,
                    size = new Vector2(7f, 3f),  // Long gravel — punishes overshoot into the corner
                    teleportDistance = 0f
                },
                // Black hole at the first hairpin exit (rewinds you if you exit too wide)
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.28f, lateralOffset = 1.5f,
                    size = new Vector2(1.0f, 1.0f),
                    teleportDistance = 10f  // ~4 swipes rewind
                },
                // Wall mid-track (forces precise line)
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.45f, lateralOffset = 1.5f,
                    size = new Vector2(2f, 2f),
                    teleportDistance = 0f
                },
                // Long gravel trap before the bottom hairpin
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.50f, lateralOffset = 0f,
                    size = new Vector2(8f, 3.5f),
                    teleportDistance = 0f
                },
                // Black hole at the third hairpin (most punishing position)
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.75f, lateralOffset = -1.5f,
                    size = new Vector2(1.0f, 1.0f),
                    teleportDistance = 10f
                }
            };

            return def;
        }

        // --- Track 6: Slipstream (★★★) — Long straights + boost chains ---
        // Identity: speed track. Long straights reward big-magnitude swipes. Boost pickups
        // are placed in chains — collect all 3 in sequence for maximum velocity. Tight
        // chicane in the middle tests precision after high-speed approach. Par 32, cap 60.

        public static TrackDefinition GenerateTrack6_Slipstream()
        {
            var def = ScriptableObject.CreateInstance<TrackDefinition>();
            def.trackId = "track_06_slipstream";
            def.displayName = "Slipstream";
            def.difficultyStars = 3;
            def.isClosedLoop = true;
            def.isFree = true;  // TEMP: free for beta testing
            def.trackIndex = 5;
            def.trackWidth = 5f;  // Slightly wider — high-speed forgives less
            def.lapCount = 1;
            def.defaultZoom = 18f;  // Zoomed out — see the whole speed track
            def.parSwipeCount = 32;
            def.maxSwipeCap = -1;  // 60

            // 6-segment loop with long straights connected by sweeping curves
            def.splinePoints = new Vector2[]
            {
                // Segment 1: Long bottom straight (right to left)
                new Vector2(-25, -10),
                new Vector2(-18, -11),
                new Vector2(-10, -11),
                new Vector2(-2, -11),
                new Vector2(6, -11),
                // Segment 2: Sweeping right curve up
                new Vector2(14, -10),
                new Vector2(20, -7),
                new Vector2(24, -2),
                // Segment 3: Long right straight (up)
                new Vector2(25, 4),
                new Vector2(25, 10),
                new Vector2(24, 16),
                // Segment 4: Sweeping top curve (right to left)
                new Vector2(20, 21),
                new Vector2(12, 23),
                new Vector2(4, 24),
                // Segment 5: Long top straight (right to left)
                new Vector2(-4, 24),
                new Vector2(-12, 24),
                new Vector2(-20, 23),
                // Segment 6: Sweeping left curve down (back to start)
                new Vector2(-24, 20),
                new Vector2(-26, 14),
                new Vector2(-26, 4),
                new Vector2(-26, -4),
            };

            // 5 power-ups: 3 Boost (chain) + Magnet + Slingshot
            def.powerUpSpawns = new TrackDefinition.PowerUpSpawn[]
            {
                // Boost chain on the bottom straight — collect all 3 for max speed
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.05f, lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(), oneTimeUse = false
                },
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.10f, lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(), oneTimeUse = false
                },
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.15f, lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(), oneTimeUse = false
                },
                // Slingshot after the right curve (pulls you up the right straight)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.30f, lateralOffset = 0f,
                    powerUp = CreateSlingshotPowerUp(), oneTimeUse = false
                },
                // Magnet on the top straight (collects the next boost early)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.55f, lateralOffset = 0f,
                    powerUp = CreateMagnetPowerUp(), oneTimeUse = false
                }
            };

            // 4 obstacles: tight chicane wall + gravel at the apex of each sweeping curve
            def.obstacleSpawns = new TrackDefinition.ObstacleSpawn[]
            {
                // Tight chicane wall mid-bottom-straight (tests precision at high speed)
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.12f, lateralOffset = 1.5f,
                    size = new Vector2(2f, 2f),
                    teleportDistance = 0f
                },
                // Gravel at the right curve apex
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.22f, lateralOffset = 0f,
                    size = new Vector2(7f, 3.5f),
                    teleportDistance = 0f
                },
                // Gravel at the top curve apex
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.45f, lateralOffset = 0f,
                    size = new Vector2(7f, 3.5f),
                    teleportDistance = 0f
                },
                // Black hole at the left curve (final approach to start)
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.90f, lateralOffset = 0f,
                    size = new Vector2(1.0f, 1.0f),
                    teleportDistance = 8f  // ~3 swipes rewind
                }
            };

            return def;
        }

        // --- Track 7: Maze Run (★★★★) — Branching paths, decision points ---
        // Identity: puzzle track. Multiple paths through the same area — choose the
        // faster route based on which obstacles are placed where. Power-ups reward
        // exploration of side routes. Par 40, cap 70.

        public static TrackDefinition GenerateTrack7_MazeRun()
        {
            var def = ScriptableObject.CreateInstance<TrackDefinition>();
            def.trackId = "track_07_maze_run";
            def.displayName = "Maze Run";
            def.difficultyStars = 4;
            def.isClosedLoop = true;
            def.isFree = true;  // TEMP: free for beta testing
            def.trackIndex = 6;
            def.trackWidth = 3.5f;  // Narrow — precision required
            def.lapCount = 1;
            def.defaultZoom = 16f;
            def.parSwipeCount = 40;
            def.maxSwipeCap = -1;  // 30 + 4*10 = 70

            // 10-segment loop that traces a complex path through a maze-like layout
            def.splinePoints = new Vector2[]
            {
                // Segment 1: Entry from start (left)
                new Vector2(-22, 0),
                new Vector2(-18, 1),
                new Vector2(-14, 2),
                // Segment 2: First decision — go up or stay low (we go up)
                new Vector2(-10, 5),
                new Vector2(-8, 9),
                new Vector2(-4, 10),
                // Segment 3: Top corridor (right)
                new Vector2(0, 10),
                new Vector2(4, 10),
                new Vector2(8, 10),
                // Segment 4: Down into the middle section
                new Vector2(12, 9),
                new Vector2(13, 5),
                new Vector2(12, 1),
                // Segment 5: Middle corridor (left)
                new Vector2(8, 0),
                new Vector2(4, 0),
                new Vector2(0, 0),
                new Vector2(-4, 0),
                // Segment 6: Down to bottom corridor
                new Vector2(-8, -1),
                new Vector2(-10, -5),
                new Vector2(-8, -9),
                // Segment 7: Bottom corridor (right)
                new Vector2(-4, -10),
                new Vector2(0, -10),
                new Vector2(4, -10),
                new Vector2(8, -10),
                // Segment 8: Up to right side
                new Vector2(12, -9),
                new Vector2(14, -5),
                new Vector2(15, 0),
                // Segment 9: Right side up
                new Vector2(16, 5),
                new Vector2(18, 9),
                new Vector2(20, 12),
                // Segment 10: Top right back to start (long way around)
                new Vector2(16, 14),
                new Vector2(8, 15),
                new Vector2(-2, 15),
                new Vector2(-12, 14),
                new Vector2(-20, 12),
                new Vector2(-22, 8),
                new Vector2(-22, 4),
            };

            // 6 power-ups: distributed to reward exploring different corridors
            def.powerUpSpawns = new TrackDefinition.PowerUpSpawn[]
            {
                // Boost at start (entry corridor)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.05f, lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(), oneTimeUse = false
                },
                // Phase Dodge at top corridor (helps avoid walls)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.18f, lateralOffset = 0f,
                    powerUp = CreatePhaseDodgePowerUp(), oneTimeUse = false
                },
                // Slingshot at middle corridor (pulls through the tight middle)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.40f, lateralOffset = 0f,
                    powerUp = CreateSlingshotPowerUp(), oneTimeUse = false
                },
                // DoubleMove at bottom corridor (extra move for free)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.60f, lateralOffset = 0f,
                    powerUp = CreateDoubleMovePowerUp(), oneTimeUse = false
                },
                // Shield at right side (protects against the upcoming black holes)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.75f, lateralOffset = 0f,
                    powerUp = CreateShieldPowerUp(), oneTimeUse = false
                },
                // Magnet at the final approach (collects nothing now but banks for next race)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.92f, lateralOffset = 0f,
                    powerUp = CreateMagnetPowerUp(), oneTimeUse = false
                }
            };

            // 7 obstacles: dense placement to make the maze feel maze-y
            def.obstacleSpawns = new TrackDefinition.ObstacleSpawn[]
            {
                // Wall in the first decision area
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.08f, lateralOffset = 1.5f,
                    size = new Vector2(2f, 2f),
                    teleportDistance = 0f
                },
                // Black hole at the top corridor midpoint
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.20f, lateralOffset = 0f,
                    size = new Vector2(1.0f, 1.0f),
                    teleportDistance = 10f
                },
                // Long gravel at the descent into the middle
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.28f, lateralOffset = 0f,
                    size = new Vector2(8f, 3.5f),
                    teleportDistance = 0f
                },
                // Wall in the middle corridor
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.42f, lateralOffset = -1.5f,
                    size = new Vector2(2f, 2f),
                    teleportDistance = 0f
                },
                // Gravel at descent to bottom
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.50f, lateralOffset = 0f,
                    size = new Vector2(7f, 3.5f),
                    teleportDistance = 0f
                },
                // Wall in the bottom corridor
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.65f, lateralOffset = 1.5f,
                    size = new Vector2(2f, 2f),
                    teleportDistance = 0f
                },
                // Black hole at the right side (mid-climb)
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.80f, lateralOffset = 0f,
                    size = new Vector2(1.0f, 1.0f),
                    teleportDistance = 12f  // ~5 swipes rewind — most punishing on this track
                }
            };

            return def;
        }

        // --- Track 8: Final Check (★★★★★) — Gauntlet, every obstacle type ---
        // Identity: finale track. Combines elements from all previous tracks: hairpins
        // from 5, straights from 6, branching from 7. Every obstacle type appears at
        // least once. Power-ups are scarce (only 4). Par 50, cap 80.

        public static TrackDefinition GenerateTrack8_FinalCheck()
        {
            var def = ScriptableObject.CreateInstance<TrackDefinition>();
            def.trackId = "track_08_final_check";
            def.displayName = "Final Check";
            def.difficultyStars = 5;
            def.isClosedLoop = true;
            def.isFree = true;  // TEMP: free for beta testing
            def.trackIndex = 7;
            def.trackWidth = 3.5f;  // Narrow — endgame precision
            def.lapCount = 1;
            def.defaultZoom = 17f;
            def.parSwipeCount = 50;
            def.maxSwipeCap = -1;  // 30 + 5*10 = 80

            // 12-segment loop — the longest track in the game
            def.splinePoints = new Vector2[]
            {
                // Segment 1: Start straight (right)
                new Vector2(-26, -16),
                new Vector2(-20, -16),
                new Vector2(-14, -15),
                new Vector2(-8, -14),
                // Segment 2: First hairpin (down-right corner)
                new Vector2(-2, -14),
                new Vector2(4, -15),
                new Vector2(8, -13),
                new Vector2(10, -10),
                // Segment 3: Up the right side (long straight)
                new Vector2(11, -6),
                new Vector2(12, 0),
                new Vector2(13, 6),
                new Vector2(14, 12),
                // Segment 4: Top-right hairpin
                new Vector2(16, 16),
                new Vector2(12, 18),
                new Vector2(8, 17),
                // Segment 5: Top straight (left)
                new Vector2(2, 16),
                new Vector2(-4, 16),
                new Vector2(-10, 17),
                // Segment 6: Top-left hairpin
                new Vector2(-16, 18),
                new Vector2(-20, 16),
                new Vector2(-22, 12),
                // Segment 7: Down the left side (long straight)
                new Vector2(-23, 6),
                new Vector2(-24, 0),
                new Vector2(-24, -6),
                // Segment 8: Bottom-left hairpin
                new Vector2(-24, -12),
                new Vector2(-20, -16),
                new Vector2(-16, -18),
                // Segment 9: Bottom middle (right)
                new Vector2(-10, -19),
                new Vector2(-4, -19),
                new Vector2(2, -19),
                // Segment 10: Inner chicane (up then right then down)
                new Vector2(4, -16),
                new Vector2(2, -12),
                new Vector2(-2, -10),
                new Vector2(-2, -6),
                // Segment 11: Inner loop (right then down)
                new Vector2(2, -4),
                new Vector2(6, -2),
                new Vector2(6, 2),
                new Vector2(4, 6),
                // Segment 12: Exit back to start
                new Vector2(0, 8),
                new Vector2(-6, 6),
                new Vector2(-12, 2),
                new Vector2(-18, -4),
                new Vector2(-22, -10),
            };

            // 4 power-ups only — scarce on the finale track
            def.powerUpSpawns = new TrackDefinition.PowerUpSpawn[]
            {
                // Boost at the start straight (helps with the first hairpin)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.04f, lateralOffset = 0f,
                    powerUp = CreateBoostPowerUp(), oneTimeUse = false
                },
                // Phase Dodge at the top-right hairpin (critical for survival)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.25f, lateralOffset = 0f,
                    powerUp = CreatePhaseDodgePowerUp(), oneTimeUse = false
                },
                // Shield at the top-left hairpin (protects against black holes ahead)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.50f, lateralOffset = 0f,
                    powerUp = CreateShieldPowerUp(), oneTimeUse = false
                },
                // Slingshot at the inner loop (pulls you through the tightest section)
                new TrackDefinition.PowerUpSpawn
                {
                    t = 0.85f, lateralOffset = 0f,
                    powerUp = CreateSlingshotPowerUp(), oneTimeUse = false
                }
            };

            // 9 obstacles: maximum density, every obstacle type represented
            def.obstacleSpawns = new TrackDefinition.ObstacleSpawn[]
            {
                // Wall at the entry to the first hairpin
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.06f, lateralOffset = 1.5f,
                    size = new Vector2(2f, 2f),
                    teleportDistance = 0f
                },
                // Black hole at the first hairpin exit
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.12f, lateralOffset = 0f,
                    size = new Vector2(1.0f, 1.0f),
                    teleportDistance = 12f  // 5-swipe rewind — most punishing
                },
                // Long gravel up the right side
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.20f, lateralOffset = 0f,
                    size = new Vector2(9f, 4f),
                    teleportDistance = 0f
                },
                // Wall at the top-right hairpin entry
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.28f, lateralOffset = -1.5f,
                    size = new Vector2(2f, 2f),
                    teleportDistance = 0f
                },
                // Black hole at top straight midpoint
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.35f, lateralOffset = 0f,
                    size = new Vector2(1.0f, 1.0f),
                    teleportDistance = 10f
                },
                // Long gravel on the top straight
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.42f, lateralOffset = 0f,
                    size = new Vector2(9f, 4f),
                    teleportDistance = 0f
                },
                // Wall at the bottom-left hairpin
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Wall,
                    t = 0.60f, lateralOffset = 1.5f,
                    size = new Vector2(2f, 2f),
                    teleportDistance = 0f
                },
                // Black hole at the inner chicane entry
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.BlackHole,
                    t = 0.75f, lateralOffset = 0f,
                    size = new Vector2(1.0f, 1.0f),
                    teleportDistance = 10f
                },
                // Final gravel trap at the exit (last obstacle before finish)
                new TrackDefinition.ObstacleSpawn
                {
                    type = TrackDefinition.ObstacleSpawn.ObstacleType.Gravel,
                    t = 0.95f, lateralOffset = 0f,
                    size = new Vector2(9f, 4f),
                    teleportDistance = 0f
                }
            };

            return def;
        }

        // --- Power-up factory methods (same as PrototypeBootstrapper) ---

        public static Racing.PowerUpBase CreateBoostPowerUp()
        {
            var pu = ScriptableObject.CreateInstance<Racing.PowerUps.BoostPowerUp>();
            pu.displayName = "Boost";
            pu.effectDescription = "Velocity multiplied by 1.8× for 1.5s — punch through gravel + walls";
            return pu;
        }

        public static Racing.PowerUpBase CreateShieldPowerUp()
        {
            var pu = ScriptableObject.CreateInstance<Racing.PowerUps.ShieldPowerUp>();
            pu.displayName = "Shield";
            pu.effectDescription = "Invulnerable to obstacles + black holes for 10s";
            return pu;
        }

        public static Racing.PowerUpBase CreateMagnetPowerUp()
        {
            var pu = ScriptableObject.CreateInstance<Racing.PowerUps.MagnetPowerUp>();
            pu.displayName = "Magnet";
            pu.effectDescription = "Pulls nearby power-ups toward your kart for 6s";
            return pu;
        }

        public static Racing.PowerUpBase CreateDoubleMovePowerUp()
        {
            var pu = ScriptableObject.CreateInstance<Racing.PowerUps.DoubleMovePowerUp>();
            pu.displayName = "Double Move";
            pu.effectDescription = "Take 2 swipes this turn instead of 1";
            return pu;
        }

        public static Racing.PowerUpBase CreateTrailWallPowerUp()
        {
            var pu = ScriptableObject.CreateInstance<Racing.PowerUps.TrailWallPowerUp>();
            pu.displayName = "Trail Wall";
            pu.effectDescription = "Your next swipe leaves a wall behind that blocks opponents";
            return pu;
        }

        public static Racing.PowerUpBase CreateBouncePowerUp()
        {
            var pu = ScriptableObject.CreateInstance<Racing.PowerUps.BouncePowerUp>();
            pu.displayName = "Bounce";
            pu.effectDescription = "Your next wall hit bounces you off instead of stopping";
            return pu;
        }

        public static Racing.PowerUpBase CreateSlingshotPowerUp()
        {
            var pu = ScriptableObject.CreateInstance<Racing.PowerUps.SlingshotPowerUp>();
            pu.displayName = "Slingshot";
            pu.effectDescription = "Instantly launches you toward the next checkpoint";
            return pu;
        }

        public static Racing.PowerUpBase CreatePhaseDodgePowerUp()
        {
            var pu = ScriptableObject.CreateInstance<Racing.PowerUps.PhaseDodgePowerUp>();
            pu.displayName = "Phase Dodge";
            pu.effectDescription = "Next swipe phases through walls — used or expires in 5s";
            return pu;
        }

        public static Racing.PowerUpBase CreateRewindPowerUp()
        {
            var pu = ScriptableObject.CreateInstance<Racing.PowerUps.RewindPowerUp>();
            pu.displayName = "Rewind";
            pu.effectDescription = "Sends you back to your previous stop position";
            return pu;
        }
    }
}
