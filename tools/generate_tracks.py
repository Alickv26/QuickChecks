#!/usr/bin/env python3
"""
generate_tracks.py
==================

Generates TrackDefinition .asset files (Unity YAML) for all 8 QuickChecks tracks,
extracting the data that was previously authored in code at:
    Assets/_Project/Scripts/Track/TrackFactory.cs

Also generates:
  - 9 PowerUp_*.asset files (Boost, Shield, Magnet, DoubleMove, TrailWall,
    Bounce, Slingshot, PhaseDodge, Rewind)
  - 1 TrackLibrary.asset referencing all 8 tracks

Each .asset file gets a paired .meta file with a fresh GUID.

Run from anywhere; paths are absolute. Re-runnable (overwrites prior output).

Usage:
    python3 generate_tracks.py
"""

import os
import uuid

# ============================================================================
# Paths
# ============================================================================

REPO_ROOT = "/home/z/my-project/repos/QuickChecks"
SCRIPTS_DIR = os.path.join(REPO_ROOT, "Assets/_Project/ScriptableObjects")
TRACKS_OUT_DIR = os.path.join(SCRIPTS_DIR, "Tracks")
POWERUPS_OUT_DIR = os.path.join(SCRIPTS_DIR, "PowerUps")

# ============================================================================
# Verified script GUIDs (from .meta files in source tree)
# ============================================================================

TRACK_DEF_SCRIPT_GUID   = "98640449f4566d859a57797cf37b1e50"  # TrackDefinition.cs
TRACK_LIB_SCRIPT_GUID   = "7251d07ab034e34538fe2dadc5cebaa5"  # TrackLibrary.cs

POWERUP_SCRIPT_GUIDS = {
    "boost":        "f4e0562fd3d4977cbb1d82a793e750ab",  # BoostPowerUp.cs
    "shield":       "d43f52436d35da53f9d9b2fd3cce7602",  # ShieldPowerUp.cs
    "magnet":       "04808c061c1b5dd698418415fec5d20c",  # MagnetPowerUp.cs
    "double_move":  "ccf97791489424dbe96fba7c90bece03",  # DoubleMovePowerUp.cs
    "trail_wall":   "72b90c5927ee16a36a2f4e2a011f705f",  # TrailWallPowerUp.cs
    "bounce":       "59d99424224503629837d01179d0cd71",  # BouncePowerUp.cs
    "slingshot":    "91cc3cb345b1fa46bb7b9e7e1be57f8f",  # SlingshotPowerUp.cs
    "phase_dodge":  "2a6c780eac83c102c9a87e13b22678b9",  # PhaseDodgePowerUp.cs
    "rewind":       "f251be5403fa0f488946c498029da792",  # RewindPowerUp.cs
}

# ============================================================================
# PowerUp asset definitions (matches C# constructor defaults + TrackFactory's overrides)
# ============================================================================

POWERUPS = [
    # (key,        fileName,              scriptGUID,                            powerUpId,       displayName,   effectDescription,                                                                     effectColorRGB,            instantUse, durationSec, expiresOnTurnEnd, extraFields)
    # durationSec=0 + expiresOnTurnEnd=True  = turn-based (expires at end of current turn).
    # durationSec=0 + expiresOnTurnEnd=False = until used / replaced (persists across turns).
    # durationSec>0                           = legacy time-based (not used in v1 turn-based game).
    ("boost",       "PowerUp_Boost",       POWERUP_SCRIPT_GUIDS["boost"],       "boost",         "Boost",        "Velocity multiplied by 1.8x - maxSpeed lifted until end of turn",                      (1.0, 0.42, 0.42),        True,       0.0,        True,  {}),
    ("shield",      "PowerUp_Shield",      POWERUP_SCRIPT_GUIDS["shield"],      "shield",        "Shield",       "Persists across turns until consumed by an offensive effect (v1: placeholder)",          (0.306, 0.804, 0.769),    True,       0.0,        False, {}),
    ("magnet",      "PowerUp_Magnet",      POWERUP_SCRIPT_GUIDS["magnet"],       "magnet",        "Magnet",       "Pulls nearby power-ups toward your kart until end of turn",                            (0.78, 0.639, 1.0),       True,       0.0,        True,  {"pullRadius": 8.0, "pullSpeed": 6.0}),
    ("double_move", "PowerUp_DoubleMove",  POWERUP_SCRIPT_GUIDS["double_move"], "double_move",   "2x Move",      "Take 2 swipes this turn instead of 1",                                                  (1.0, 0.84, 0.0),         True,       0.0,        False, {}),
    ("trail_wall",  "PowerUp_TrailWall",   POWERUP_SCRIPT_GUIDS["trail_wall"],  "trail_wall",    "Trail Wall",   "Your next swipe leaves a wall behind that blocks opponents",                           (1.0, 0.42, 0.79),        True,       0.0,        False, {"wallDurationSec": 30.0}),
    ("bounce",      "PowerUp_Bounce",      POWERUP_SCRIPT_GUIDS["bounce"],      "bounce",        "Bounce",       "Walls + track limits bounce you off at 90% velocity until end of turn",                 (0.3, 0.9, 0.5),          True,       0.0,        True,  {"bounceVelocityRetention": 0.9}),
    ("slingshot",   "PowerUp_Slingshot",   POWERUP_SCRIPT_GUIDS["slingshot"],   "slingshot",     "Slingshot",    "Instantly launches you toward the next checkpoint",                                    (0.725, 0.514, 1.0),      True,       0.0,        False, {}),
    ("phase_dodge", "PowerUp_PhaseDodge",  POWERUP_SCRIPT_GUIDS["phase_dodge"], "phase_dodge",   "Phase Dodge",  "Next wall hit phases through - used OR expires at end of turn",                        (1.0, 0.71, 0.15),        True,       0.0,        True,  {}),
    ("rewind",      "PowerUp_Rewind",      POWERUP_SCRIPT_GUIDS["rewind"],      "rewind",        "Rewind",       "Sends you back to your previous stop position",                                        (0.42, 0.792, 0.467),     False,      0.0,        False, {"rewindDurationSec": 1.0}),
]


# ============================================================================
# Track definitions (extracted from TrackFactory.cs)
# Each track is a dict with the exact fields TrackDefinition.cs declares.
# ============================================================================

TRACKS = [
    # ===== Track 1: First Lap (★, Free) =====
    {
        "fileName":        "Track_1_FirstLap",
        "trackId":         "track_01_first_lap",
        "displayName":     "First Lap",
        "difficultyStars": 1,
        "isFree":          True,
        "trackIndex":      0,
        "trackWidth":      6.0,
        "lapCount":        1,
        "defaultZoom":     16.0,
        "parSwipeCount":   8,
        "maxSwipeCap":     -1,
        "unlockTimeOnPreviousTrack": 0.0,
        "splinePoints": [
            (-15, 0), (-15, 8), (-7, 10), (0, 10),
            (7, 10), (15, 8), (15, 0),
            (15, -8), (7, -10), (0, -10),
            (-7, -10), (-15, -8),
        ],
        "powerUpSpawns": [],
        "obstacleSpawns": [
            {"type": "Gravel", "t": 0.5, "lateralOffset": 0.0, "size": (6.0, 3.0), "teleportDistance": 0.0},
        ],
        "trackColor":      (0.106, 0.165, 0.165, 1.0),
        "borderColor":     (0.247, 0.878, 0.760, 1.0),
        "backgroundColor": (0.055, 0.078, 0.078, 1.0),
    },
    # ===== Track 2: Curves 101 (★, Free) =====
    {
        "fileName":        "Track_2_Curves101",
        "trackId":         "track_02_curves_101",
        "displayName":     "Curves 101",
        "difficultyStars": 1,
        "isFree":          True,
        "trackIndex":      1,
        "trackWidth":      5.0,
        "lapCount":        1,
        "defaultZoom":     15.0,
        "parSwipeCount":   10,
        "maxSwipeCap":     -1,
        "unlockTimeOnPreviousTrack": 45.0,
        "splinePoints": [
            (-15, 0), (-15, 5), (-12, 8), (-8, 8),
            (-4, 8), (-2, 10), (0, 10),
            (4, 10), (8, 8), (10, 5),
            (12, 2), (10, -2), (8, -5),
            (5, -8), (0, -10), (-5, -10),
            (-10, -8), (-15, -5),
        ],
        "powerUpSpawns": [
            {"t": 0.5, "lateralOffset": 0.0, "powerUp": "double_move", "oneTimeUse": False},
        ],
        "obstacleSpawns": [
            {"type": "Wall", "t": 0.3, "lateralOffset": 1.0, "size": (1.5, 1.5), "teleportDistance": 0.0},
            {"type": "Gravel", "t": 0.7, "lateralOffset": 0.0, "size": (7.0, 3.0), "teleportDistance": 0.0},
        ],
        "trackColor":      (0.106, 0.165, 0.165, 1.0),
        "borderColor":     (0.337, 0.706, 0.913, 1.0),
        "backgroundColor": (0.055, 0.078, 0.078, 1.0),
    },
    # ===== Track 3: Chicane Sprint (★★, Free) =====
    {
        "fileName":        "Track_3_ChicaneSprint",
        "trackId":         "track_03_chicane_sprint",
        "displayName":     "Chicane Sprint",
        "difficultyStars": 2,
        "isFree":          True,
        "trackIndex":      2,
        "trackWidth":      5.0,
        "lapCount":        1,
        "defaultZoom":     14.0,
        "parSwipeCount":   14,
        "maxSwipeCap":     -1,
        "unlockTimeOnPreviousTrack": 55.0,
        "splinePoints": [
            (-18, 0), (-16, 4), (-12, 8), (-6, 9),
            (0, 8), (4, 10), (8, 8),
            (12, 6), (14, 2), (13, -3),
            (11, -7), (6, -9), (0, -10),
            (-6, -9), (-10, -7), (-13, -4),
            (-17, -1), (-18, -2),
        ],
        "powerUpSpawns": [
            {"t": 0.25, "lateralOffset": 0.0, "powerUp": "boost",      "oneTimeUse": False},
            {"t": 0.5,  "lateralOffset": 0.0, "powerUp": "bounce",    "oneTimeUse": False},
            {"t": 0.75, "lateralOffset": 0.0, "powerUp": "trail_wall", "oneTimeUse": False},
        ],
        "obstacleSpawns": [
            {"type": "Wall",      "t": 0.2,  "lateralOffset": 1.0, "size": (1.5, 1.5), "teleportDistance": 0.0},
            {"type": "BlackHole", "t": 0.55, "lateralOffset": 0.0, "size": (1.0, 1.0), "teleportDistance": 8.0},
            {"type": "Gravel",    "t": 0.85, "lateralOffset": 0.0, "size": (8.0, 3.5), "teleportDistance": 0.0},
        ],
        "trackColor":      (0.106, 0.165, 0.165, 1.0),
        "borderColor":     (0.878, 0.416, 0.769, 1.0),
        "backgroundColor": (0.055, 0.078, 0.078, 1.0),
    },
    # ===== Track 4: Neon Sweep (★★, Free for beta) =====
    {
        "fileName":        "Track_4_NeonSweep",
        "trackId":         "track_04_neon_sweep",
        "displayName":     "Neon Sweep",
        "difficultyStars": 2,
        "isFree":          True,  # TrackFactory sets True ("TEMP: free for beta testing")
        "trackIndex":      3,
        "trackWidth":      4.5,
        "lapCount":        1,
        "defaultZoom":     15.0,
        "parSwipeCount":   16,
        "maxSwipeCap":     -1,
        "unlockTimeOnPreviousTrack": 70.0,
        "splinePoints": [
            (-20, -8), (-15, -8), (-10, -8), (-5, -8),
            (0, -8), (5, -6), (8, -3),
            (10, 0), (10, 4), (8, 7),
            (5, 9), (0, 10), (-5, 10),
            (-10, 10), (-15, 8), (-18, 5),
            (-20, 2), (-20, -4),
        ],
        "powerUpSpawns": [
            {"t": 0.15, "lateralOffset": 0.0, "powerUp": "boost",       "oneTimeUse": False},
            {"t": 0.35, "lateralOffset": 0.0, "powerUp": "double_move", "oneTimeUse": False},
            {"t": 0.5,  "lateralOffset": 0.0, "powerUp": "boost",       "oneTimeUse": False},
            {"t": 0.8,  "lateralOffset": 0.0, "powerUp": "magnet",      "oneTimeUse": False},
        ],
        "obstacleSpawns": [
            {"type": "Wall",      "t": 0.1, "lateralOffset": 1.0,  "size": (1.5, 1.5), "teleportDistance": 0.0},
            {"type": "BlackHole", "t": 0.4, "lateralOffset": 0.0,  "size": (1.0, 1.0), "teleportDistance": 10.0},
            {"type": "Wall",      "t": 0.6, "lateralOffset": -1.0, "size": (1.5, 1.5), "teleportDistance": 0.0},
            {"type": "Gravel",    "t": 0.9, "lateralOffset": 0.0,  "size": (9.0, 4.0), "teleportDistance": 0.0},
        ],
        "trackColor":      (0.106, 0.165, 0.165, 1.0),
        "borderColor":     (0.941, 0.627, 0.251, 1.0),
        "backgroundColor": (0.055, 0.078, 0.078, 1.0),
    },
    # ===== Track 5: Hairpin Highway (★★★, Free for beta) =====
    {
        "fileName":        "Track_5_HairpinHighway",
        "trackId":         "track_05_hairpin_highway",
        "displayName":     "Hairpin Highway",
        "difficultyStars": 3,
        "isFree":          True,
        "trackIndex":      4,
        "trackWidth":      4.0,
        "lapCount":        1,
        "defaultZoom":     14.0,
        "parSwipeCount":   28,
        "maxSwipeCap":     -1,
        "unlockTimeOnPreviousTrack": 90.0,
        "splinePoints": [
            (-18, 8), (-12, 9), (-6, 10), (0, 10),
            (6, 10), (10, 9), (12, 6), (10, 3),
            (8, 0), (8, -4), (10, -7),
            (12, -10), (8, -11), (4, -10),
            (-2, -10), (-8, -10), (-12, -9),
            (-15, -7), (-14, -3), (-12, 0),
            (-14, 4), (-16, 5),
            (-20, 7), (-20, 8),
        ],
        "powerUpSpawns": [
            {"t": 0.15, "lateralOffset": 0.0, "powerUp": "phase_dodge", "oneTimeUse": False},
            {"t": 0.35, "lateralOffset": 0.0, "powerUp": "shield",      "oneTimeUse": False},
            {"t": 0.60, "lateralOffset": 0.0, "powerUp": "boost",       "oneTimeUse": False},
            {"t": 0.85, "lateralOffset": 0.0, "powerUp": "boost",       "oneTimeUse": False},
        ],
        "obstacleSpawns": [
            {"type": "Gravel",    "t": 0.20, "lateralOffset": 0.0,  "size": (7.0, 3.0), "teleportDistance": 0.0},
            {"type": "BlackHole", "t": 0.28, "lateralOffset": 1.5,  "size": (1.0, 1.0), "teleportDistance": 10.0},
            {"type": "Wall",      "t": 0.45, "lateralOffset": 1.5,  "size": (2.0, 2.0), "teleportDistance": 0.0},
            {"type": "Gravel",    "t": 0.50, "lateralOffset": 0.0,  "size": (8.0, 3.5), "teleportDistance": 0.0},
            {"type": "BlackHole", "t": 0.75, "lateralOffset": -1.5, "size": (1.0, 1.0), "teleportDistance": 10.0},
        ],
        "trackColor":      (0.106, 0.165, 0.165, 1.0),
        "borderColor":     (1.0, 0.42, 0.42, 1.0),
        "backgroundColor": (0.055, 0.078, 0.078, 1.0),
    },
    # ===== Track 6: Slipstream (★★★, Free for beta) =====
    {
        "fileName":        "Track_6_Slipstream",
        "trackId":         "track_06_slipstream",
        "displayName":     "Slipstream",
        "difficultyStars": 3,
        "isFree":          True,
        "trackIndex":      5,
        "trackWidth":      5.0,
        "lapCount":        1,
        "defaultZoom":     18.0,
        "parSwipeCount":   32,
        "maxSwipeCap":     -1,
        "unlockTimeOnPreviousTrack": 110.0,
        "splinePoints": [
            (-25, -10), (-18, -11), (-10, -11), (-2, -11), (6, -11),
            (14, -10), (20, -7), (24, -2),
            (25, 4), (25, 10), (24, 16),
            (20, 21), (12, 23), (4, 24),
            (-4, 24), (-12, 24), (-20, 23),
            (-24, 20), (-26, 14), (-26, 4), (-26, -4),
        ],
        "powerUpSpawns": [
            {"t": 0.05, "lateralOffset": 0.0, "powerUp": "boost",     "oneTimeUse": False},
            {"t": 0.10, "lateralOffset": 0.0, "powerUp": "boost",     "oneTimeUse": False},
            {"t": 0.15, "lateralOffset": 0.0, "powerUp": "boost",     "oneTimeUse": False},
            {"t": 0.30, "lateralOffset": 0.0, "powerUp": "slingshot", "oneTimeUse": False},
            {"t": 0.55, "lateralOffset": 0.0, "powerUp": "magnet",    "oneTimeUse": False},
        ],
        "obstacleSpawns": [
            {"type": "Wall",      "t": 0.12, "lateralOffset": 1.5, "size": (2.0, 2.0), "teleportDistance": 0.0},
            {"type": "Gravel",    "t": 0.22, "lateralOffset": 0.0, "size": (7.0, 3.5), "teleportDistance": 0.0},
            {"type": "Gravel",    "t": 0.45, "lateralOffset": 0.0, "size": (7.0, 3.5), "teleportDistance": 0.0},
            {"type": "BlackHole", "t": 0.90, "lateralOffset": 0.0, "size": (1.0, 1.0), "teleportDistance": 8.0},
        ],
        "trackColor":      (0.106, 0.165, 0.165, 1.0),
        "borderColor":     (0.247, 0.878, 0.760, 1.0),
        "backgroundColor": (0.055, 0.078, 0.078, 1.0),
    },
    # ===== Track 7: Maze Run (★★★★, Free for beta) =====
    {
        "fileName":        "Track_7_MazeRun",
        "trackId":         "track_07_maze_run",
        "displayName":     "Maze Run",
        "difficultyStars": 4,
        "isFree":          True,
        "trackIndex":      6,
        "trackWidth":      3.5,
        "lapCount":        1,
        "defaultZoom":     16.0,
        "parSwipeCount":   40,
        "maxSwipeCap":     -1,
        "unlockTimeOnPreviousTrack": 130.0,
        "splinePoints": [
            (-22, 0), (-18, 1), (-14, 2),
            (-10, 5), (-8, 9), (-4, 10),
            (0, 10), (4, 10), (8, 10),
            (12, 9), (13, 5), (12, 1),
            (8, 0), (4, 0), (0, 0), (-4, 0),
            (-8, -1), (-10, -5), (-8, -9),
            (-4, -10), (0, -10), (4, -10), (8, -10),
            (12, -9), (14, -5), (15, 0),
            (16, 5), (18, 9), (20, 12),
            (16, 14), (8, 15), (-2, 15), (-12, 14), (-20, 12), (-22, 8), (-22, 4),
        ],
        "powerUpSpawns": [
            {"t": 0.05, "lateralOffset": 0.0, "powerUp": "boost",       "oneTimeUse": False},
            {"t": 0.18, "lateralOffset": 0.0, "powerUp": "phase_dodge", "oneTimeUse": False},
            {"t": 0.40, "lateralOffset": 0.0, "powerUp": "slingshot",    "oneTimeUse": False},
            {"t": 0.60, "lateralOffset": 0.0, "powerUp": "double_move", "oneTimeUse": False},
            {"t": 0.75, "lateralOffset": 0.0, "powerUp": "shield",      "oneTimeUse": False},
            {"t": 0.92, "lateralOffset": 0.0, "powerUp": "magnet",      "oneTimeUse": False},
        ],
        "obstacleSpawns": [
            {"type": "Wall",      "t": 0.08, "lateralOffset": 1.5,  "size": (2.0, 2.0), "teleportDistance": 0.0},
            {"type": "BlackHole", "t": 0.20, "lateralOffset": 0.0,  "size": (1.0, 1.0), "teleportDistance": 10.0},
            {"type": "Gravel",    "t": 0.28, "lateralOffset": 0.0,  "size": (8.0, 3.5), "teleportDistance": 0.0},
            {"type": "Wall",      "t": 0.42, "lateralOffset": -1.5, "size": (2.0, 2.0), "teleportDistance": 0.0},
            {"type": "Gravel",    "t": 0.50, "lateralOffset": 0.0,  "size": (7.0, 3.5), "teleportDistance": 0.0},
            {"type": "Wall",      "t": 0.65, "lateralOffset": 1.5,  "size": (2.0, 2.0), "teleportDistance": 0.0},
            {"type": "BlackHole", "t": 0.80, "lateralOffset": 0.0,  "size": (1.0, 1.0), "teleportDistance": 12.0},
        ],
        "trackColor":      (0.106, 0.165, 0.165, 1.0),
        "borderColor":     (0.725, 0.514, 1.0, 1.0),
        "backgroundColor": (0.055, 0.078, 0.078, 1.0),
    },
    # ===== Track 8: Final Check (★★★★★, Free for beta) =====
    # NOTE: Original TrackFactory coordinates produced a 224-unit spline (over the
    # TrackValidator.cs 200-unit limit). We scale the entire track by 0.85 to bring
    # it to ~190 units — still the longest track in the game (Track 7 is 163),
    # but within validator range. Identity "longest finale track" is preserved.
    {
        "fileName":        "Track_8_FinalCheck",
        "trackId":         "track_08_final_check",
        "displayName":     "Final Check",
        "difficultyStars": 5,
        "isFree":          True,
        "trackIndex":      7,
        "trackWidth":      3.5,
        "lapCount":        1,
        "defaultZoom":     17.0,
        "parSwipeCount":   50,
        "maxSwipeCap":     -1,
        "unlockTimeOnPreviousTrack": 150.0,
        "splinePoints": [(x * 0.85, y * 0.85) for (x, y) in [
            (-26, -16), (-20, -16), (-14, -15), (-8, -14),
            (-2, -14), (4, -15), (8, -13), (10, -10),
            (11, -6), (12, 0), (13, 6), (14, 12),
            (16, 16), (12, 18), (8, 17),
            (2, 16), (-4, 16), (-10, 17),
            (-16, 18), (-20, 16), (-22, 12),
            (-23, 6), (-24, 0), (-24, -6),
            (-24, -12), (-20, -16), (-16, -18),
            (-10, -19), (-4, -19), (2, -19),
            (4, -16), (2, -12), (-2, -10), (-2, -6),
            (2, -4), (6, -2), (6, 2), (4, 6),
            (0, 8), (-6, 6), (-12, 2), (-18, -4),
        ]],
        "powerUpSpawns": [
            {"t": 0.04, "lateralOffset": 0.0, "powerUp": "boost",       "oneTimeUse": False},
            {"t": 0.25, "lateralOffset": 0.0, "powerUp": "phase_dodge", "oneTimeUse": False},
            {"t": 0.50, "lateralOffset": 0.0, "powerUp": "shield",      "oneTimeUse": False},
            {"t": 0.85, "lateralOffset": 0.0, "powerUp": "slingshot",   "oneTimeUse": False},
        ],
        "obstacleSpawns": [
            {"type": "Wall",      "t": 0.06, "lateralOffset": 1.5,  "size": (2.0, 2.0), "teleportDistance": 0.0},
            {"type": "BlackHole", "t": 0.12, "lateralOffset": 0.0,  "size": (1.0, 1.0), "teleportDistance": 12.0},
            {"type": "Gravel",    "t": 0.20, "lateralOffset": 0.0,  "size": (9.0, 4.0), "teleportDistance": 0.0},
            {"type": "Wall",      "t": 0.28, "lateralOffset": -1.5, "size": (2.0, 2.0), "teleportDistance": 0.0},
            {"type": "BlackHole", "t": 0.35, "lateralOffset": 0.0,  "size": (1.0, 1.0), "teleportDistance": 10.0},
            {"type": "Gravel",    "t": 0.42, "lateralOffset": 0.0,  "size": (9.0, 4.0), "teleportDistance": 0.0},
            {"type": "Wall",      "t": 0.60, "lateralOffset": 1.5,  "size": (2.0, 2.0), "teleportDistance": 0.0},
            {"type": "BlackHole", "t": 0.75, "lateralOffset": 0.0,  "size": (1.0, 1.0), "teleportDistance": 10.0},
            {"type": "Gravel",    "t": 0.95, "lateralOffset": 0.0,  "size": (9.0, 4.0), "teleportDistance": 0.0},
        ],
        "trackColor":      (0.106, 0.165, 0.165, 1.0),
        "borderColor":     (1.0, 0.82, 0.4, 1.0),
        "backgroundColor": (0.055, 0.078, 0.078, 1.0),
    },
]


# ============================================================================
# Helpers
# ============================================================================

def new_guid() -> str:
    """Generate a fresh 32-char lowercase hex GUID (Unity's preferred format)."""
    return uuid.uuid4().hex


def fmt(v) -> str:
    """Format a float for Unity YAML — strips trailing zeros but keeps 0 for integers."""
    if isinstance(v, bool):
        return "1" if v else "0"
    if isinstance(v, int):
        return str(v)
    # float
    s = repr(v)
    # Unity prefers short forms like 0.5, 1, -15 — strip trailing .0
    if s.endswith(".0"):
        s = s[:-2]
    return s


def fmt_color(rgba) -> str:
    r, g, b, a = rgba
    return f"{{r: {fmt(r)}, g: {fmt(g)}, b: {fmt(b)}, a: {fmt(a)}}}"


def fmt_vec2(v) -> str:
    return f"{{x: {fmt(v[0])}, y: {fmt(v[1])}}}"


# ============================================================================
# Bezier math (mirrors BezierSpline.cs for validation)
# ============================================================================

def evaluate_segment(p0, p1, p2, p3, t):
    u = 1.0 - t
    tt = t * t
    uu = u * u
    uuu = uu * u
    ttt = tt * t
    x = uuu * p0[0] + 3 * uu * t * p1[0] + 3 * u * tt * p2[0] + ttt * p3[0]
    y = uuu * p0[1] + 3 * uu * t * p1[1] + 3 * u * tt * p2[1] + ttt * p3[1]
    return (x, y)


def segment_count(point_count: int, closed_loop: bool = True) -> int:
    if closed_loop:
        if point_count < 3:
            return 0
        if point_count % 3 != 0:
            return 0
        return point_count // 3
    else:
        if point_count < 4:
            return 0
        if (point_count - 1) % 3 != 0:
            return 0
        return (point_count - 1) // 3


def evaluate_spline(points, t, closed=True):
    seg_count = segment_count(len(points), closed)
    if seg_count == 0:
        return (0.0, 0.0)
    if t < 0:
        t = t - int(t) + 1  # Python's % works for negatives differently than Unity's Mathf.Repeat
    t = t % 1.0
    scaled = t * seg_count
    seg_index = int(scaled)
    if seg_index >= seg_count:
        seg_index = seg_count - 1
    local_t = scaled - seg_index
    base = seg_index * 3
    p0 = points[base]
    p1 = points[base + 1]
    p2 = points[base + 2]
    if closed and seg_index == seg_count - 1:
        p3 = points[0]
    else:
        p3 = points[base + 3]
    return evaluate_segment(p0, p1, p2, p3, local_t)


def approximate_length(points, closed=True, samples=50):
    if segment_count(len(points), closed) == 0:
        return 0.0
    total = 0.0
    prev = evaluate_spline(points, 0.0, closed)
    for i in range(1, samples + 1):
        t = i / samples
        cur = evaluate_spline(points, t, closed)
        dx = cur[0] - prev[0]
        dy = cur[1] - prev[1]
        total += (dx * dx + dy * dy) ** 0.5
        prev = cur
    return total


# ============================================================================
# Validators (mirror TrackValidator.cs invariants)
# ============================================================================

def validate_spline(points, track_id):
    """Validates spline integrity. Raises AssertionError on failure."""
    n = len(points)
    assert n % 3 == 0, f"[{track_id}] spline must have 3N points for closed loop, got {n}"
    assert n >= 6,     f"[{track_id}] spline must have >=6 points (>=2 segments), got {n}"
    # TrackValidator.cs checks: no two consecutive control points within 0.01 units
    for i in range(n - 1):
        dx = points[i + 1][0] - points[i][0]
        dy = points[i + 1][1] - points[i][1]
        dist_sq = dx * dx + dy * dy
        assert dist_sq > 0.0001, f"[{track_id}] degenerate adjacent pair at index {i}->{i+1}: {points[i]} ~ {points[i+1]}"
    # Length: 30-200 units (per TrackValidator.cs)
    length = approximate_length(points, closed=True)
    assert 30.0 <= length <= 200.0, f"[{track_id}] spline length {length:.1f} out of range [30, 200]"


# ============================================================================
# YAML writers
# ============================================================================

META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_meta(path, guid):
    with open(path, "w", newline="\n") as f:
        f.write(META_TEMPLATE.format(guid=guid))


POWERUP_ASSET_TEMPLATE = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier:
  powerUpId: {power_up_id}
  displayName: {display_name}
  effectDescription: {effect_description}
  icon: {{fileID: 0}}
  effectColor: {effect_color}
  instantUse: {instant_use}
  durationSec: {duration_sec}
  expiresOnTurnEnd: {expires_on_turn_end}{extra_fields}
"""


def write_powerup_asset(path, pu):
    # Tuple now has 11 elements (added expiresOnTurnEnd before extraFields)
    key, file_name, script_guid, power_up_id, display_name, effect_desc, color_rgb, instant_use, duration_sec, expires_on_turn_end, extra = pu
    extra_yaml = ""
    for field_name, field_val in extra.items():
        extra_yaml += f"\n  {field_name}: {fmt(field_val)}"
    content = POWERUP_ASSET_TEMPLATE.format(
        script_guid=script_guid,
        name=file_name,
        power_up_id=power_up_id,
        display_name=display_name,
        effect_description='"' + effect_desc.replace('"', '\\"') + '"',
        effect_color=fmt_color((color_rgb[0], color_rgb[1], color_rgb[2], 1.0)),
        instant_use="1" if instant_use else "0",
        duration_sec=fmt(duration_sec),
        expires_on_turn_end="1" if expires_on_turn_end else "0",
        extra_fields=extra_yaml,
    )
    with open(path, "w", newline="\n") as f:
        f.write(content)


def build_track_yaml(track, boost_powerup_guid, powerup_guid_map):
    """Build the YAML body for a TrackDefinition .asset."""
    lines = []
    lines.append("%YAML 1.1")
    lines.append("%TAG !u! tag:unity3d.com,2011:")
    lines.append("--- !u!114 &11400000")
    lines.append("MonoBehaviour:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append("  m_GameObject: {fileID: 0}")
    lines.append("  m_Enabled: 1")
    lines.append("  m_EditorHideFlags: 0")
    lines.append(f"  m_Script: {{fileID: 11500000, guid: {TRACK_DEF_SCRIPT_GUID}, type: 3}}")
    lines.append(f"  m_Name: {track['fileName']}")
    lines.append("  m_EditorClassIdentifier: ")
    # Identity
    lines.append(f"  trackId: {track['trackId']}")
    lines.append(f"  displayName: {track['displayName']}")
    lines.append(f"  difficultyStars: {track['difficultyStars']}")
    lines.append("  previewImage: {fileID: 0}")
    # Spline points
    lines.append("  splinePoints:")
    for p in track["splinePoints"]:
        lines.append(f"  - {fmt_vec2(p)}")
    lines.append(f"  isClosedLoop: 1")
    lines.append(f"  trackWidth: {fmt(track['trackWidth'])}")
    lines.append(f"  lapCount: {track['lapCount']}")
    lines.append(f"  isFree: {'1' if track['isFree'] else '0'}")
    # Power-up spawns
    lines.append("  powerUpSpawns:")
    for spawn in track["powerUpSpawns"]:
        pu_guid = powerup_guid_map[spawn["powerUp"]]
        lines.append(f"  - t: {fmt(spawn['t'])}")
        lines.append(f"    lateralOffset: {fmt(spawn['lateralOffset'])}")
        lines.append(f"    powerUp: {{fileID: 11400000, guid: {pu_guid}, type: 2}}")
        lines.append(f"    oneTimeUse: {'1' if spawn['oneTimeUse'] else '0'}")
    # Obstacle spawns
    lines.append("  obstacleSpawns:")
    for obs in track["obstacleSpawns"]:
        lines.append(f"  - type: {obs['type']}")
        lines.append(f"    t: {fmt(obs['t'])}")
        lines.append(f"    lateralOffset: {fmt(obs['lateralOffset'])}")
        lines.append(f"    size: {fmt_vec2(obs['size'])}")
        lines.append(f"    teleportDistance: {fmt(obs['teleportDistance'])}")
    # Camera
    lines.append(f"  defaultZoom: {fmt(track['defaultZoom'])}")
    lines.append(f"  useSegmentZoomHints: 1")
    # Style
    lines.append(f"  trackColor: {fmt_color(track['trackColor'])}")
    lines.append(f"  borderColor: {fmt_color(track['borderColor'])}")
    lines.append(f"  backgroundColor: {fmt_color(track['backgroundColor'])}")
    # Unlock
    lines.append(f"  trackIndex: {track['trackIndex']}")
    lines.append(f"  unlockTimeOnPreviousTrack: {fmt(track['unlockTimeOnPreviousTrack'])}")
    # Swipe cap
    lines.append(f"  maxSwipeCap: {track['maxSwipeCap']}")
    lines.append(f"  parSwipeCount: {track['parSwipeCount']}")
    # CPU AI
    lines.append(f"  optimalLineGhostUrl: ")
    return "\n".join(lines) + "\n"


def write_track_asset(path, track, powerup_guid_map):
    content = build_track_yaml(track, None, powerup_guid_map)
    with open(path, "w", newline="\n") as f:
        f.write(content)


def write_track_library(path, track_guids):
    lines = []
    lines.append("%YAML 1.1")
    lines.append("%TAG !u! tag:unity3d.com,2011:")
    lines.append("--- !u!114 &11400000")
    lines.append("MonoBehaviour:")
    lines.append("  m_ObjectHideFlags: 0")
    lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
    lines.append("  m_PrefabInstance: {fileID: 0}")
    lines.append("  m_PrefabAsset: {fileID: 0}")
    lines.append("  m_GameObject: {fileID: 0}")
    lines.append("  m_Enabled: 1")
    lines.append("  m_EditorHideFlags: 0")
    lines.append(f"  m_Script: {{fileID: 11500000, guid: {TRACK_LIB_SCRIPT_GUID}, type: 3}}")
    lines.append("  m_Name: TrackLibrary")
    lines.append("  m_EditorClassIdentifier: ")
    lines.append("  tracks:")
    for g in track_guids:
        lines.append(f"  - {{fileID: 11400000, guid: {g}, type: 2}}")
    content = "\n".join(lines) + "\n"
    with open(path, "w", newline="\n") as f:
        f.write(content)


# ============================================================================
# Main
# ============================================================================

def main():
    os.makedirs(TRACKS_OUT_DIR, exist_ok=True)
    os.makedirs(POWERUPS_OUT_DIR, exist_ok=True)
    # Remove old .gitkeep if present (no longer needed once real assets exist)
    gitkeep = os.path.join(TRACKS_OUT_DIR, ".gitkeep")
    if os.path.exists(gitkeep):
        os.remove(gitkeep)
        print(f"[cleanup] removed {gitkeep}")

    print("=" * 70)
    print("Generating PowerUp assets")
    print("=" * 70)
    powerup_guid_map = {}  # key (e.g. "boost") -> GUID
    for pu in POWERUPS:
        key, file_name = pu[0], pu[1]
        g = new_guid()
        powerup_guid_map[key] = g
        asset_path = os.path.join(POWERUPS_OUT_DIR, f"{file_name}.asset")
        meta_path = os.path.join(POWERUPS_OUT_DIR, f"{file_name}.asset.meta")
        write_powerup_asset(asset_path, pu)
        write_meta(meta_path, g)
        print(f"  [powerup] {file_name}.asset  guid={g}")

    print()
    print("=" * 70)
    print("Validating + generating TrackDefinition assets")
    print("=" * 70)
    track_guids = []
    for track in TRACKS:
        # Validate before writing
        validate_spline(track["splinePoints"], track["trackId"])
        length = approximate_length(track["splinePoints"], closed=True)
        seg_count = segment_count(len(track["splinePoints"]), closed_loop=True)
        g = new_guid()
        track_guids.append(g)
        asset_path = os.path.join(TRACKS_OUT_DIR, f"{track['fileName']}.asset")
        meta_path = os.path.join(TRACKS_OUT_DIR, f"{track['fileName']}.asset.meta")
        write_track_asset(asset_path, track, powerup_guid_map)
        write_meta(meta_path, g)
        print(f"  [track]   {track['fileName']}.asset  "
              f"segs={seg_count}  pts={len(track['splinePoints'])}  "
              f"length={length:.1f}  pu={len(track['powerUpSpawns'])}  "
              f"obs={len(track['obstacleSpawns'])}  guid={g}")

    print()
    print("=" * 70)
    print("Generating TrackLibrary asset")
    print("=" * 70)
    lib_guid = new_guid()
    lib_path = os.path.join(TRACKS_OUT_DIR, "TrackLibrary.asset")
    lib_meta_path = os.path.join(TRACKS_OUT_DIR, "TrackLibrary.asset.meta")
    write_track_library(lib_path, track_guids)
    write_meta(lib_meta_path, lib_guid)
    print(f"  [library] TrackLibrary.asset    guid={lib_guid}  refs={len(track_guids)} tracks")

    print()
    print("=" * 70)
    print("DONE")
    print("=" * 70)
    print(f"Tracks dir:   {TRACKS_OUT_DIR}")
    print(f"PowerUps dir: {POWERUPS_OUT_DIR}")
    print(f"  - 8 TrackDefinition assets + 1 TrackLibrary asset")
    print(f"  - 9 PowerUp assets")
    print(f"  - Total: 18 .asset files + 18 .meta files")


if __name__ == "__main__":
    main()
