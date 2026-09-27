# QuickChecks — Implementation Plan

**Document version**: 1.0
**Last updated**: 2026-09-27
**Owner**: Game design + dev

---

## 1. Vision Statement

QuickChecks is a top-down, swipe-controlled kart racing game where every flick is a deliberate, weighted impulse — closer to billiards than to thumbstick steering. Tracks are handcrafted, minimal-vector circuits designed around precise swipes through tight chicanes, power-up pickups, and risk/reward shortcuts.

The competitive layer is **asynchronous**: your best run becomes a ghost that other players race against on the same track. No real-time netcode, no matchmaking delays, no rage quits. Just you, the track, and 50 ghosts of players slightly better than you.

**North Star metric**: Average session length ≥ 8 minutes, week-1 retention ≥ 35%.

---

## 2. Core Gameplay Specification

### 2.1 Swipe Input Model

A valid move is a **quick flick**, not a slow drag. We detect this by:

- **Velocity threshold**: Swipe must reach ≥ 800 px/s peak velocity
- **Duration cap**: Swipe must complete in ≤ 250ms from touch-down to lift
- **Distance floor**: Swipe must travel ≥ 40px (prevents taps registering)
- **Direction**: Free-angle (any direction, not locked to 8-way)

When a valid swipe fires, the kart receives an **impulse vector**:
- Direction: normalized swipe vector (in screen space, transformed to world)
- Magnitude: scaled by swipe speed (capped at max impulse)
- The kart's existing velocity is **replaced**, not added (billiards model)

Slow drags (below thresholds) are ignored — no input. This forces intentional flicks, prevents "drag steering," and creates the skill ceiling.

### 2.2 Kart Physics

```
Every FixedUpdate tick:
  velocity *= frictionCoefficient (0.985 per tick at 60fps)
  position += velocity * deltaTime
  
  if (currentSpeed < 5 px/s):
    state = STOPPED
    // Player must swipe again to move
```

Key tuning constants (in `KartStats` ScriptableObject):
- `maxImpulse` (per-kart cap on swipe force)
- `frictionCoefficient` (per-kart deceleration)
- `mass` (affects collision response with other karts/ghosts)
- `boostMultiplier` (how much a speed power-up multiplies velocity)

### 2.3 Track Boundaries

Tracks are defined as **Bezier spline paths** with a track width. The playable area is between the inner and outer splines. If the kart center exits the outer boundary:

1. Velocity is set to 0 immediately
2. State → `OFF_TRACK`
3. Player can swipe again — kart restarts from last checkpoint passed
4. Penalty: +2 seconds added to race time per off-track event

### 2.4 Power-Up System

Six power-ups split into two categories. Each kart has 1 power-up slot (last collected replaces previous).

**Speed / Utility (3):**

| Power-up | Effect | Duration | Spawn Pattern |
|----------|--------|----------|---------------|
| **Boost** | Velocity ×1.8 in current direction | 1.5s | Line of 3 in speed sections |
| **Slingshot** | Pulls kart toward next checkpoint, +50% velocity | Instant | Single, on long straights |
| **Phase Dodge** | Next boundary exit doesn't stop the kart (phases through walls once) | Until used or 5s expires | Before tricky chicanes |

**Defensive (3):**

| Power-up | Effect | Duration | Spawn Pattern |
|----------|--------|----------|---------------|
| **Shield** | Negates next offensive effect (relevant in future PVP modes) | 10s | After boost sections |
| **Rewind** | On tap, kart rewinds 1 second of position | 1 use | Mid-track safety net |
| **Magnet** | Pulls nearby power-ups toward kart | 6s | Power-up dense sections |

### 2.5 Camera & Zoom

- **Default**: Top-down, follows kart with slight lag (smoothing factor 0.15)
- **Pinch zoom**: Pinch in/out adjusts orthographic camera size between `minZoom` (close, tight sections) and `maxZoom` (far, long straights)
- **Auto-zoom**: Tracks declare zoom hints per segment (e.g., "tight chicane ahead → zoom in")
- **Player override**: Player pinch always takes precedence for 2s, then auto-zoom resumes

### 2.6 Checkpoints & Race Format

- Tracks have 6-12 checkpoints along the spline
- Must pass all in order to register a lap
- Race format: 1 lap for sprints, 3 laps for circuit (per track config)
- Off-track respawn = last checkpoint + 2s penalty

---

## 3. Async Multiplayer Architecture

### 3.1 Ghost Format

Each completed race produces a **ghost file** — a compact JSON recording of every swipe event:

```json
{
  "trackId": "track_04_neon_sprint",
  "kartId": "kart_viper",
  "finishTimeMs": 42850,
  "swipeEvents": [
    {"t": 120, "x": 540, "y": 980, "dx": 0.7, "dy": -0.3, "mag": 850},
    {"t": 320, "x": 580, "y": 950, "dx": 0.4, "dy": -0.9, "mag": 720},
    // ... ~40-80 events for a typical race
  ],
  "powerUpsCollected": ["boost", "slingshot", "shield"]
}
```

Payload size: ~5-8 KB per ghost. Stored as JSON blob in Supabase Storage.

### 3.2 Race Flow (Async)

1. Player selects track → app fetches top 10 global ghosts + top 3 friend ghosts
2. Player races; up to 3 ghosts are rendered simultaneously during the race
3. On finish, player's ghost is uploaded; leaderboard updates
4. Other players see this ghost in future races on the same track

### 3.3 Backend Schema (Supabase / Postgres)

```sql
-- Players (anonymous auth, opt-in profile)
CREATE TABLE players (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  display_name TEXT,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Ghosts (one per finished race)
CREATE TABLE ghosts (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  player_id UUID REFERENCES players(id),
  track_id TEXT NOT NULL,
  kart_id TEXT NOT NULL,
  finish_time_ms INTEGER NOT NULL,
  ghost_url TEXT NOT NULL,  -- Supabase Storage URL
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Leaderboard (materialized view for fast reads)
CREATE VIEW leaderboard AS
SELECT DISTINCT ON (player_id, track_id)
  player_id, track_id, finish_time_ms, ghost_url, created_at
FROM ghosts
ORDER BY player_id, track_id, finish_time_ms ASC;
```

Row-level security: players can only insert ghosts for their own `player_id`.

### 3.4 Race Integrity

To prevent cheating (impossibly fast times):
- Server validates `finish_time_ms` against theoretical min time per track (precomputed via speedrun)
- Ghost file must have plausible swipe count (≥ 20, ≤ 200)
- Flag suspicious runs for review; don't auto-ban (false positive cost too high)

---

## 4. Track Design System

### 4.1 Track Representation

Each track is a **ScriptableObject** with:
- Spline path (Bezier control points)
- Inner + outer boundary (offset from centerline)
- Checkpoints (auto-generated every N spline segments)
- Power-up spawn points (manually placed)
- Camera zoom hints per segment
- Visual style (color palette, decorations)

### 4.2 Track Editor Tool

A custom Unity Editor window (`Tools > Track Builder`) lets designers:
1. Draw spline points by clicking in scene view
2. Adjust track width with a slider
3. Auto-generate boundaries + checkpoints
4. Drag-drop power-up spawn points
5. Set per-segment zoom hints
6. Playtest in-place (skip main menu)

This tool is built in weeks 3-4 and used to build all 8 tracks in weeks 7-8.

### 4.3 Track Difficulty Progression

| Track # | Name | Difficulty | Feature focus |
|---------|------|------------|---------------|
| 1 | First Lap | ★ | Straight-line swipes, single power-up type |
| 2 | Curves 101 | ★ | Gentle curves, introduce zoom |
| 3 | Chicane Sprint | ★★ | Tight sections require zoom-in |
| 4 | Neon Sweep | ★★ | Long straights with boosts |
| 5 | Hairpin Highway | ★★★ | Sharp 180° turns, phase dodge power-up |
| 6 | Slipstream | ★★★ | Slingshot-heavy, defensive power-ups matter |
| 7 | Maze Run | ★★★★ | Complex layout, requires path memorization |
| 8 | Final Check | ★★★★★ | All mechanics combined, longest track |

---

## 5. Visual & Audio Direction

### 5.1 Art Bible (Minimal Vector)

**Color palette** (subject to refinement):
- Background: `#0E1414` (deep dark teal)
- Track surface: `#1B2A2A` (slightly lighter)
- Track borders: `#3FE0C2` (cyan accent)
- Kart player: `#FFD166` (warm yellow)
- Ghost karts: `#7B8A8A` (muted gray-blue, low alpha)
- Power-ups: Category-coded (boost=`#FF6B6B`, shield=`#4ECDC4`, magnet=`#C7A3FF`)
- UI text: `#F7F7F2` (off-white)

**Typography**: Inter or Manrope (clean geometric sans-serif), bold for numbers.

**Animation principles**:
- Kart bounces slightly on swipe impact (squash & stretch)
- Power-up pickup = particle burst + 0.1s screen flash in power-up color
- Finish line = confetti + slowmo on victory lap

### 5.2 Audio

- **Music**: Synthwave + chill electronic, 120-130 BPM (matches swipe rhythm)
- **SFX**: Swipe whoosh (pitched by swipe speed), power-up chime, boundary bonk, finish fanfare
- **Haptics**: Light impact on swipe, medium on power-up, heavy on boundary hit

---

## 6. Technical Architecture

### 6.1 Code Architecture

```
Scripts/
├── Core/
│   ├── GameFlowManager.cs      # State machine: Boot → Menu → Race → Results
│   ├── SceneLoader.cs          # Async scene loading with progress
│   ├── EventBus.cs             # Decoupled event system (ScriptableObject events)
│   └── ServiceLocator.cs       # DI for services (auth, leaderboard, save)
├── Input/
│   ├── SwipeDetector.cs        # Touch → swipe event detection
│   ├── PinchZoomInput.cs       # Pinch gesture → zoom factor
│   └── InputSettings.cs        # Tunable thresholds (ScriptableObject)
├── Racing/
│   ├── KartController.cs       # Receives swipes, applies impulse
│   ├── KartPhysics.cs          # Friction, velocity integration
│   ├── KartStats.cs            # Per-kart tunable stats (ScriptableObject)
│   ├── PowerUpSystem.cs        # Manages active power-up, applies effects
│   ├── PowerUpBase.cs          # Abstract base for power-ups
│   ├── PowerUps/
│   │   ├── BoostPowerUp.cs
│   │   ├── SlingshotPowerUp.cs
│   │   ├── PhaseDodgePowerUp.cs
│   │   ├── ShieldPowerUp.cs
│   │   ├── RewindPowerUp.cs
│   │   └── MagnetPowerUp.cs
│   └── CheckpointTracker.cs    # Tracks passed checkpoints, validates laps
├── Track/
│   ├── TrackDefinition.cs      # ScriptableObject: spline, width, power-ups
│   ├── TrackSegment.cs         # One Bezier segment + boundary colliders
│   ├── TrackBuilder.cs         # Editor tool for designers
│   ├── TrackValidator.cs       # Validates track is completable
│   └── BoundaryCollider.cs    # 2D collider for off-track detection
├── Camera/
│   ├── CameraRig.cs            # Follows kart, applies smoothing
│   ├── ZoomController.cs       # Ortho size control, pinch + auto-zoom
│   └── ZoomHints.cs            # Per-segment zoom hints from track
├── Ghost/
│   ├── GhostRecorder.cs        # Captures swipe events during race
│   ├── GhostPlayer.cs          # Replays swipe events as ghost kart
│   ├── GhostData.cs            # JSON-serializable ghost format
│   └── GhostRepository.cs      # Local cache + remote fetch
├── Multiplayer/
│   ├── AsyncMultiplayerManager.cs # Orchestrates ghost loading for race
│   ├── LeaderboardService.cs   # Fetches top-N ghosts for track
│   ├── SupabaseClient.cs       # REST wrapper for Supabase
│   └── AuthService.cs          # Anonymous auth, profile creation
├── UI/
│   ├── HUDController.cs        # Lap counter, timer, position
│   ├── MainMenuController.cs
│   ├── TrackSelectController.cs
│   ├── ResultsScreenController.cs
│   └── LeaderboardPanel.cs
└── Data/
    ├── SaveSystem.cs           # Local save (PlayerPrefs + JSON)
    ├── PlayerProfile.cs
    └── TrackProgress.cs        # Stars/unlocks per track
```

### 6.2 Event-Driven Communication

Systems communicate via a **ScriptableObject event bus** to avoid tight coupling:

```csharp
[CreateAssetMenu(menuName = "Events/SwipeEvent")]
public class SwipeEvent : ScriptableObject {
    public Action<SwipeData> OnSwipe;
    public void Raise(SwipeData data) => OnSwipe?.Invoke(data);
}
```

Benefits:
- KartController subscribes to SwipeEvent without SwipeDetector knowing about it
- Easy to add new listeners (e.g., analytics, haptics) without modifying detectors
- Plays nice with Unity Inspector for wiring

### 6.3 Performance Targets

| Metric | Target | Hard limit |
|--------|--------|------------|
| Frame rate | 60fps on iPhone 11 / Pixel 5+ | 30fps floor |
| Memory | < 300 MB peak | < 500 MB |
| Boot time | < 3s to menu | < 5s |
| Track load time | < 1s | < 2s |
| Ghost replay (3 ghosts) | < 1ms per frame | < 3ms |
| Build size | < 80 MB | < 120 MB |

---

## 7. MVP Scope (Single-Player Full)

The v1 cut delivers:

✅ **In scope:**
- 8 handcrafted tracks (difficulty 1-5 stars)
- 8 karts with distinct stats (speed/accel/grip tradeoffs)
- 6 power-ups (3 speed/utility, 3 defensive)
- Swipe input + impulse physics
- Pinch-to-zoom + auto-zoom
- Ghost recording + local replay
- Supabase backend: auth, ghost upload, global leaderboard
- Async multiplayer: race against top-10 ghosts on each track
- Full game flow: menu → track select → race → results → upload
- Player profile: best times per track, kart unlocks
- Progression: unlock next track by finishing previous in under X seconds

❌ **Out of scope (v2+):**
- Real-time multiplayer
- Offensive power-ups
- Track modifiers
- User-generated tracks
- Cosmetic kart skins (sold separately later)
- Daily challenges / seasons

---

## 8. Risk Register

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Swipe input feels bad on different screen sizes | High | Critical | Build swipe-calibration tool that runs on first launch; per-device tuning presets |
| Track builder tool takes longer than expected | Medium | High | Start with simple manual track authoring; tool comes later |
| Supabase free tier limits hit unexpectedly | Medium | Medium | Add Redis cache layer if read QPS > 50/s; migrate to dedicated Postgres if needed |
| Ghost integrity cheating | Medium | Medium | Server-side validation, manual review queue, don't auto-ban |
| Performance issues with 3 ghost replays | Low | High | Profile early (week 6), cap ghost count, use object pooling |
| Art style drift during 8 tracks | Medium | Low | Lock art bible in week 1, weekly art review |

---

## 9. Open Questions

Things we still need to decide before week 3:

1. **Kart unlock progression** — linear (track 1 unlocks kart 1) or star-based (earn stars per track, spend on karts)?
2. **Monetization** — fully free? Ads? IAP for cosmetic kart skins? Decide before launch.
3. **Track 1 difficulty** — should it be a tutorial with on-screen hints, or just an easy track that players figure out?
4. **Audio contractor** — in-house or hired? Synthwave composer rates vary 5x.
5. **TestFlight + Google Play beta rollout strategy** — how many testers, what feedback loop?
6. **Localization** — English-only at launch, or localize to top 5 languages from day 1?
7. **Accessibility** — color-blind mode, single-finger mode (no pinch required), audio cues?

---

## 10. Next Steps (Week 1)

- [ ] Install Unity 2022.3 LTS + set up project
- [ ] Initialize git, push scaffold to GitHub
- [ ] Build swipe detection prototype (single scene, cube that responds to swipes)
- [ ] Draft art bible v1 (color palette + kart silhouette)
- [ ] Pick a name + reserve App Store / Play Store listing
- [ ] Set up Supabase project + create schema
- [ ] Validate swipe thresholds on at least 3 different devices

---

*This is a living document. Update version + date when making material changes.*
