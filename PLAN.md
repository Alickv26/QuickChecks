# QuickChecks — Implementation Plan

**Document version**: 2.0
**Last updated**: 2026-09-27
**Owner**: Game design + dev
**Supersedes**: v1.0 (which described async ghost racing — that model was replaced)

---

## 1. Vision Statement

QuickChecks is a top-down, swipe-controlled kart racing game where every flick is a deliberate, weighted impulse — closer to billiards or golf than to thumbstick steering. Tracks are handcrafted, minimal-vector circuits designed around precise swipes through tight chicanes, power-up pickups, and risk/reward shortcuts.

The competitive layer is **real-time turn-based**: players alternate swipes during an active race, and the global leaderboard ranks by **fewest swipes to complete** each track — not fastest time. Each track has a "par" (theoretical minimum swipe count), and players compete to beat par, the global leaderboard, and their own personal ghost.

Three game modes:
- **Solo practice** — race against your personal best ghost, climb global leaderboard, beat par
- **Local multiplayer** — pass-and-play (2-4 players sharing device) or vs CPU (3 difficulty levels)
- **Online** — real-time turn-based matches via WebSocket (v1 stubbed with simulated CPU opponent, real netcode ships in v1.1)

Plus a **daily track** (procedurally generated, same for all players globally, resets UTC midnight) for daily engagement.

**North Star metrics**:
- Average session length: 1-3 minutes (quick burst)
- Day-1 retention ≥ 40%
- Day-7 retention ≥ 25%
- Freemium conversion ≥ 8% (free → paid unlock)

---

## 2. Core Gameplay Specification

### 2.1 Swipe Input Model

A valid move is a **quick flick**, not a slow drag. We detect this by:

- **Velocity threshold**: Swipe must reach ≥ 1000 px/s peak velocity (golf-feel tuned)
- **Duration cap**: Swipe must complete in ≤ 220ms from touch-down to lift
- **Distance floor**: Swipe must travel ≥ 50px (prevents taps registering)
- **Direction**: Free-angle (any direction, not locked to 8-way)

When a valid swipe fires, the kart receives an **impulse vector**:
- Direction: normalized swipe vector (in screen space, transformed to world)
- Magnitude: scaled by swipe speed (capped at `maxImpulseMagnitude = 1500`)
- The kart's existing velocity is **replaced**, not added (billiards/golf model)

Slow drags (below thresholds) are ignored — no input. This forces intentional flicks, prevents "drag steering," and creates the skill ceiling.

**Tutorial exception**: Tutorial uses 300 px/s threshold (lower) so players with motor impairments can complete it; auto-restores to 1000 px/s after tutorial finishes.

### 2.2 Kart Physics

```
Every FixedUpdate tick:
  velocity *= frictionPerSecond (0.22 per second, applied as 1 - 0.22*dt)
  position += velocity * deltaTime

  if (currentSpeed < stopThreshold (8 px/s)):
    state = STOPPED
    // Player must swipe again to move
    // In multiplayer, turn ends here
```

Key tuning constants (in `KartStats` ScriptableObject):
- `impulseMultiplier` (per-kart responsiveness; default 1.1)
- `frictionPerSecond` (deceleration; default 0.22 for golf feel)
- `stopThreshold` (clean stop boundary; default 8 px/s)
- `maxSpeed` (hard cap; default 1500 u/s)
- `boostMultiplier` (boost power-up effect; default 1.8×)
- `boostDurationSec` (boost duration; default 1.5s)

### 2.3 Turn-Based Multiplayer Model

Each player's turn:
1. **Turn start** — camera snaps to active player's kart, input enabled
2. **Player input window** — player plans + executes a swipe (no time limit in local; 30s in online)
3. **Swipe execution** — kart receives impulse, starts moving
4. **Coast phase** — kart decelerates due to friction
5. **Turn end** — kart velocity drops below `stopThreshold`
6. **Hand-off** — turn passes to next player

**Boundary collision**: Kart stops dead at track walls (golf-like, no sliding). Turn ends. Next turn, player swipes from current stuck position.

**Off-track**: No penalty beyond lost position (no extra turn loss, no time penalty).

**Race end**: All karts cross finish line OR all hit max-swipe cap (50-80 per track based on difficulty).
- **Winner**: fewest total swipes
- **Tiebreaker**: faster finish time
- **DNF**: kart hits max-swipe cap without finishing

### 2.4 Track Boundaries

Tracks are defined as **Bezier spline paths** with a track width. The playable area is between the inner and outer splines. If the kart center exits the outer boundary:

1. Velocity is set to 0 immediately
2. State → `OFF_TRACK`
3. Turn ends (in multiplayer)
4. Player can swipe again — kart restarts from current stuck position

### 2.5 Power-Up System

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

Power-up effects tick in real game time. Duration power-ups (Boost, Shield, Magnet) only benefit the active player's turn — opponent doesn't benefit during their turn since they're a different kart.

**Spawn rules**:
- Power-ups respawn 5s after pickup
- Max 2 active pickups on a track at once (prevents hoarding)
- Last 200m before finish line: no new pickups (pure skill section)

### 2.6 Camera & Zoom

- **Default**: Top-down, follows kart with slight lag (smoothing factor 0.15)
- **Pinch zoom**: Pinch in/out adjusts orthographic camera size between `minZoom` (5, tight sections) and `maxZoom` (20, long straights)
- **Auto-zoom**: Tracks declare zoom hints per segment (e.g., "tight chicane ahead → zoom in")
- **Player override**: Player pinch always takes precedence for 2s, then auto-zoom resumes
- **Accessibility**: One-finger mode (long-press toggles zoom-in/zoom-out) for one-handed play — Decision 13

### 2.7 Checkpoints & Race Format

- Tracks have 6-12 checkpoints along the spline
- Must pass all in order to register a lap
- Race format: 1 lap for sprints, 3 laps for circuit (per track config)
- Off-track doesn't reset to checkpoint — kart stays stuck at boundary (golf-like)

---

## 3. Game Modes

### 3.1 Solo Practice

- Race any unlocked track alone
- Personal ghost replays alongside (best run by swipe count)
- Auto-uploads to global leaderboard on race end
- Par comparison shown on results screen

### 3.2 Local vs CPU (3 difficulties)

- Player vs 1-3 CPU karts
- Turn-based: human swipes, CPU takes its turn, etc.
- Difficulty levels:
  - **Easy**: ±25° direction noise, 50-80% magnitude
  - **Medium**: ±10° direction noise, 75-95% magnitude
  - **Hard**: ±3° direction noise, 92-100% magnitude (hybrid: follows designer-authored optimal line + noise)
- CPU plays by identical rules (Decision 10: symmetric)

### 3.3 Local Pass-and-Play

- 2-4 human players share one device
- Between turns: full-screen "Pass to Player N" overlay (tap to dismiss)
- 30s turn timer optional (default off for casual play)
- All karts visible at all times; inactive dimmed to 40% alpha (Decision 4)

### 3.4 Online Multiplayer (v1.1 — stubbed in v1)

- 2 players connected via WebSocket
- Real-time turn sync: each swipe event sent to opponent, replayed on their screen
- 30s turn timer; auto-skip on timeout (kart stays put)
- Match result uploads to global leaderboard + ELO (+25 win / −15 loss)
- v1 ships with stub: `OnlineTurnClient` simulates opponent using CPUPlayer (Medium difficulty)
- v1.1 ships real WebSocket infrastructure

### 3.5 Daily Track

- One procedurally generated track per day, same for all players globally
- Resets at UTC midnight (Decision 19: missed dailies gone forever)
- Difficulty rotates by day of week (Decision 18):
  - Mon: Easy (★) / Tue: Easy-Med / Wed: Med (★★) / Thu: Med-Hard / Fri: Hard (★★★) / Sat: Expert (★★★★) / Sun: Expert+ (★★★★★)
- Separate global leaderboard per daily track
- Available to free players (gated only by IAP for full leaderboard participation)

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
- `parSwipeCount` (designer-set during playtesting — Decision 21)
- `maxSwipeCap` (default formula `30 + difficultyStars × 10` — Decision 1)
- `isFree` (boolean for freemium gating — Decision 16)
- `optimalLineGhostUrl` (optional, for Hard CPU AI — Decision 3)

### 4.2 Track Editor Tool

A custom Unity Editor window (`Tools > QuickChecks > Track Builder`) lets designers:
1. Draw spline points by clicking in scene view
2. Adjust track width with a slider
3. Auto-generate boundaries + checkpoints
4. Drag-drop power-up spawn points
5. Set per-segment zoom hints
6. Playtest in-place (skip main menu)

This tool is built in weeks 3-4 and used to build all 8 tracks in weeks 7-8.

### 4.3 Track Difficulty Progression

| Track # | Name | Difficulty | Par (target) | Max Swipes | Free/Paid |
|---------|------|------------|---------------|------------|-----------|
| 1 | First Lap | ★ | ~12 | 40 | Free |
| 2 | Curves 101 | ★ | ~15 | 40 | Free |
| 3 | Chicane Sprint | ★★ | ~18 | 50 | Free |
| 4 | Neon Sweep | ★★ | ~22 | 50 | Paid |
| 5 | Hairpin Highway | ★★★ | ~28 | 60 | Paid |
| 6 | Slipstream | ★★★ | ~32 | 60 | Paid |
| 7 | Maze Run | ★★★★ | ~40 | 70 | Paid |
| 8 | Final Check | ★★★★★ | ~50 | 80 | Paid |

(Par values are targets — designer sets actual values during playtesting weeks 10-11.)

### 4.4 Daily Track Generator

- Seeded RNG: `seed = YYYYMMDD` (date as integer)
- Difficulty parameters derived from day of week (Decision 18)
- Constraints to ensure track is solvable under par:
  - Track length bounded by difficulty (Easy: ~30 units, Expert+: ~80 units)
  - Obstacle density bounded (Easy: 2-3 obstacles, Expert+: 8-12)
  - Always at least one path through (validated by TrackValidator before publishing)
- Generated track uploaded to Supabase once per day (server-side cron job)

---

## 5. Visual & Audio Direction

### 5.1 Art Bible (Minimal Vector)

**Color palette** (subject to refinement):
- Background: `#0E1414` (deep dark teal)
- Track surface: `#1B2A2A` (slightly lighter)
- Track borders: `#3FE0C2` (cyan accent)
- Kart player: `#FFD166` (warm yellow)
- Ghost karts: `#7B8A8A` (muted gray-blue, 60% alpha)
- Power-ups: Category-coded (boost=`#FF6B6B`, shield=`#4ECDC4`, magnet=`#C7A3FF`)
- UI text: `#F7F7F2` (off-white)

**Color-blind palettes** (Decision 13): 3 alternate palettes (protanopia, deuteranopia, tritanopia) toggleable in settings.

**Typography**: Inter or Manrope (clean geometric sans-serif), bold for numbers.

**Animation principles**:
- Kart bounces slightly on swipe impact (squash & stretch)
- Power-up pickup = particle burst + 0.1s screen flash in power-up color
- Finish line = confetti + slowmo on victory lap

### 5.2 Audio (Free / Open-Source — Decision 15 revised)

- **Music**: Curated from free libraries (Free Music Archive, Pixabay, Incompetech, YouTube Audio Library)
  - 120-130 BPM synthwave / chill electronic
  - 8-12 tracks sourced + edited for seamless looping (Audacity)
  - All licenses verified for commercial use (CC0, CC-BY, Pixabay License)
  - Attribution recorded for credits screen (CC-BY requires this)
- **SFX**: From Freesound + Kenney.nl (CC0 game SFX packs)
  - Swipe whoosh (current `SwipeAudio.cs` synthesized fallback works if no asset found)
  - Power-up chime, boundary bonk, finish fanfare
- **Haptics**: Light impact on swipe, medium on power-up, heavy on boundary hit
- **Audio cues (accessibility)**: Distinct SFX for power-up collected, boundary hit, finish line approaching, ghost passing (Decision 13)

---

## 6. Technical Architecture

### 6.1 Code Architecture

```
Scripts/
├── Core/
│   ├── GameFlowManager.cs      # State machine: Boot → Tutorial → Menu → Race → Results
│   ├── SceneLoader.cs          # Async scene loading with progress
│   ├── EventBus.cs             # Decoupled event system (ScriptableObject events)
│   └── ServiceLocator.cs       # DI for services (auth, leaderboard, save, IAP)
├── Input/
│   ├── SwipeDetector.cs        # Touch → swipe event detection
│   ├── PinchZoomInput.cs       # Pinch gesture → zoom factor
│   ├── OneFingerZoomController.cs  # Accessibility alt (Decision 13)
│   └── InputSettings.cs       # Tunable thresholds (ScriptableObject)
├── Racing/
│   ├── KartController.cs      # Receives swipes, applies impulse
│   ├── KartStats.cs            # Per-kart tunable stats (ScriptableObject)
│   ├── KartTrail.cs            # Fading visual trail
│   ├── SwipeFeedback.cs        # Squash + particle burst
│   ├── PowerUpSystem.cs        # Manages active power-up, applies effects
│   ├── PowerUpBase.cs          # Abstract base for power-ups
│   ├── PowerUps/               # 6 concrete power-up classes
│   ├── TurnManager.cs          # Turn orchestration, race end detection
│   ├── CPUPlayer.cs            # AI with difficulty-based noise
│   └── CheckpointTracker.cs    # Tracks passed checkpoints, validates laps
├── Track/
│   ├── TrackDefinition.cs      # ScriptableObject: spline, width, power-ups, par
│   ├── TrackSegment.cs         # One Bezier segment + boundary colliders
│   ├── TrackBuilder.cs         # Editor tool for designers
│   ├── TrackValidator.cs       # Validates track is completable
│   ├── BoundaryCollider.cs     # 2D collider for off-track detection
│   └── DailyTrackGenerator.cs  # Seeded procedural generation (Decision 8)
├── Camera/
│   ├── CameraRig.cs            # Follows kart, applies smoothing
│   ├── ZoomController.cs       # Ortho size control, pinch + auto-zoom
│   └── ZoomHints.cs            # Per-segment zoom hints from track
├── Ghost/
│   ├── GhostRecorder.cs        # Captures swipe events during race
│   ├── GhostPlayer.cs          # Replays swipe events as ghost kart
│   ├── GhostData.cs            # JSON-serializable ghost format
│   ├── SoloGhostPlayer.cs      # Records + replays personal best (local)
│   └── GhostRepository.cs      # Local cache + remote fetch
├── Multiplayer/
│   ├── AsyncMultiplayerManager.cs  # (renamed) MultiplayerManager
│   ├── PassPlayManager.cs      # Local pass-and-play flow
│   ├── OnlineTurnClient.cs     # WebSocket stub (v1) / real (v1.1)
│   ├── LeaderboardService.cs   # Fetches top-N ghosts for track
│   ├── SupabaseClient.cs       # REST wrapper for Supabase
│   ├── AuthService.cs          # Anonymous auth (Decision 11)
│   └── DisplayNameGenerator.cs # Adjective+Animal_Number (Decision 22)
├── Monetization/
│   ├── IAPService.cs           # App Store / Play Store IAP wrapper (Decision 6)
│   ├── EntitlementManager.cs   # Gate tracks/karts behind unlock state (Decision 16)
│   └── PaywallController.cs    # Paywall modal UI
├── Onboarding/
│   ├── TutorialController.cs   # 4-step tutorial scene (Decision 7)
│   ├── TutorialStep.cs         # Enum: Move, RejectSlow, ExplainPar, FinishLine
│   └── FirstLaunchFlow.cs     # Auth + tutorial + name generation
├── Accessibility/
│   ├── AccessibilitySettings.cs # Toggles + slider (Decision 13)
│   ├── ColorBlindPalette.cs    # Alternate color palettes
│   └── AudioCueManager.cs      # Distinct SFX for events
├── Audio/
│   ├── SwipeAudio.cs           # Synthesized whoosh (fallback)
│   ├── MusicPlayer.cs          # Crossfades between composer tracks
│   └── SfxPlayer.cs            # One-shot SFX playback
├── UI/
│   ├── HUDController.cs        # Lap counter, timer, position
│   ├── MainMenuController.cs
│   ├── TrackSelectController.cs  # Includes paywall gating (Decision 16)
│   ├── ResultsScreenController.cs
│   ├── LeaderboardPanel.cs
│   ├── DailyTrackPanel.cs      # Decision 8
│   └── PassPlayOverlay.cs      # Decision 4
└── Data/
    ├── SaveSystem.cs           # Local save (PlayerPrefs + JSON)
    ├── PlayerProfile.cs
    └── TrackProgress.cs        # Stars/unlocks per track
```

### 6.2 Event-Driven Communication

Systems communicate via a **ScriptableObject event bus** to avoid tight coupling. Each event is a separate ScriptableObject asset.

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

## 7. Backend Architecture

### 7.1 Supabase Schema (Postgres)

```sql
-- Players (anonymous auth — Decision 11)
CREATE TABLE players (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  display_name TEXT NOT NULL,
  elo INTEGER DEFAULT 1000,
  is_premium BOOLEAN DEFAULT FALSE,
  tutorial_completed BOOLEAN DEFAULT FALSE,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Race results (every finished race)
CREATE TABLE race_results (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  player_id UUID REFERENCES players(id),
  track_id TEXT NOT NULL,
  kart_id TEXT NOT NULL,
  swipe_count INTEGER NOT NULL,
  finish_time_ms INTEGER NOT NULL,
  mode TEXT NOT NULL CHECK (mode IN ('solo', 'vs_cpu_easy', 'vs_cpu_medium', 'vs_cpu_hard', 'pass_play', 'online', 'daily')),
  ghost_url TEXT,
  match_id UUID,  -- nullable; set for online matches
  daily_track_date DATE,  -- nullable; set for daily tracks
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Per-track leaderboard (best result per player per track)
CREATE VIEW leaderboard AS
SELECT DISTINCT ON (player_id, track_id)
  player_id, track_id, swipe_count, finish_time_ms, mode, ghost_url, created_at
FROM race_results
WHERE mode IN ('solo', 'online')
ORDER BY player_id, track_id, swipe_count ASC, finish_time_ms ASC;

-- Daily track leaderboard (separate, resets per date)
CREATE VIEW daily_leaderboard AS
SELECT DISTINCT ON (player_id, daily_track_date)
  player_id, daily_track_date, swipe_count, finish_time_ms, ghost_url, created_at
FROM race_results
WHERE mode = 'daily' AND daily_track_date = CURRENT_DATE
ORDER BY player_id, daily_track_date, swipe_count ASC, finish_time_ms ASC;

-- Online match queue (for v1.1 matchmaking)
CREATE TABLE online_queue (
  player_id UUID PRIMARY KEY,
  track_id TEXT NOT NULL,
  elo INTEGER NOT NULL,
  joined_at TIMESTAMPTZ DEFAULT NOW()
);

-- Online matches (active + historical)
CREATE TABLE online_matches (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  track_id TEXT NOT NULL,
  player_a UUID REFERENCES players(id),
  player_b UUID REFERENCES players(id),
  status TEXT NOT NULL CHECK (status IN ('active', 'a_finished', 'b_finished', 'completed', 'abandoned')),
  current_turn_player UUID REFERENCES players(id),
  swipes_a INTEGER DEFAULT 0,
  swipes_b INTEGER DEFAULT 0,
  winner UUID REFERENCES players(id),
  started_at TIMESTAMPTZ DEFAULT NOW(),
  completed_at TIMESTAMPTZ
);

-- Daily track definitions (one per day, generated by cron)
CREATE TABLE daily_tracks (
  track_date DATE PRIMARY KEY,
  difficulty INTEGER NOT NULL,
  track_json TEXT NOT NULL,
  par_swipe_count INTEGER NOT NULL,
  created_at TIMESTAMPTZ DEFAULT NOW()
);
```

### 7.2 Row-Level Security

- `players`: SELECT/UPDATE only own row (via `auth.uid() = id`)
- `race_results`: INSERT only with own `player_id`; SELECT all (leaderboard is public)
- `online_queue`: INSERT only own row; DELETE only own row
- `online_matches`: INSERT/UPDATE only if `player_a` or `player_b` is self

### 7.3 Scheduled Jobs (pg_cron)

- **Daily track generation**: Every day at 00:00 UTC, generate new daily track, INSERT into `daily_tracks`
- **Old ghost cleanup**: Every day at 03:00 UTC, DELETE `race_results` older than 7 days where `mode = 'online'` (Decision 2)
- **Old online match cleanup**: Every day at 03:00 UTC, DELETE `online_matches` older than 30 days where `status = 'completed'`

---

## 8. Monetization Implementation

### 8.1 IAP Setup

- **Product ID**: `unlock_full_game`
- **Price**: $4.99 USD (Decision 17)
- **Platform**: App Store Connect + Google Play Console
- **Type**: Non-consumable (one-time purchase, restored on reinstall)

### 8.2 Receipt Validation

- Client calls Supabase Edge Function `validate-iap` with receipt
- Edge Function calls Apple/Google validation endpoints
- On success: `UPDATE players SET is_premium = TRUE WHERE id = $current_player`
- Client polls/refreshes entitlement on app launch

### 8.3 Entitlement Gating

| Content | Free | Premium |
|---------|------|---------|
| Tracks 1-3 | ✅ | ✅ |
| Tracks 4-8 | ❌ (grayed in track select) | ✅ |
| Karts 1-3 | ✅ | ✅ |
| Karts 4-8 | ❌ | ✅ |
| Solo practice + ghost | ✅ | ✅ |
| Daily track | ✅ (limited leaderboard) | ✅ (full leaderboard) |
| Local multiplayer | ✅ | ✅ |
| Online ranked (v1.1) | ❌ | ✅ |

### 8.4 Paywall UI (Decision 16)

- **Trigger**: Player taps grayed-out track 4+ in track select
- **Modal**: Shows "Unlock Full Game" + price + feature list + "Restore Purchases" button
- **Close**: "Maybe later" dismisses, returns to track select
- **Success**: On IAP completion, modal closes, all tracks ungrayed

---

## 9. Onboarding Flow

### 9.1 First Launch

1. **Anonymous auth** (Decision 11) — Supabase auto-creates player ID, no UI
2. **Display name generation** (Decision 22) — auto-generated as `AdjectiveAnimal_Number`, e.g., "SwiftFox_42"
3. **Tutorial** (Decision 7) — forced 30-second scene with 4 steps:
   - Step 1: "Swipe to move" — kart on empty arena, player must swipe to continue
   - Step 2: "Slow drags don't work" — player tries slow drag (rejected), then fast swipe (accepted)
   - Step 3: "Goal: fewest swipes" — explains par/star system
   - Step 4: "Reach the green line" — short finish-line demo
4. **Main menu** — player can now race, daily track, or open settings

### 9.2 Tutorial Failure Recovery (Decision 20)

- Tutorial uses 300 px/s swipe threshold (vs 1000 px/s in main game)
- If player still can't swipe fast enough after 3 attempts at step 1: threshold auto-lowers further to 200 px/s
- On tutorial completion: threshold restores to 1000 px/s for main game
- Tutorial can be re-triggered from Settings → "Replay Tutorial"

---

## 10. Accessibility Implementation (Decision 13)

### 10.1 Color-Blind Mode

- 3 alternate palettes: protanopia, deuteranopia, tritanopia
- Toggleable in Settings → Accessibility
- Implementation: `ColorBlindPalette` ScriptableObject swaps all `SpriteRenderer.color` and `TMPro.TextMeshProUGUI.color` at runtime
- UI preview in settings shows palette before applying

### 10.2 One-Finger Mode

- Long-press (500ms) toggles between zoom-in (ortho 8) and zoom-out (ortho 16)
- Replaces pinch-to-zoom requirement
- Toggleable in Settings → Accessibility
- Default: off (preserves normal pinch-to-zoom for majority)

### 10.3 Audio Cues

- Distinct SFX for events:
  - Power-up collected (high chime)
  - Boundary hit (low thud)
  - Finish line approaching (rising tone, last 200m)
  - Ghost passing you (descending whoosh)
- Volume: separate slider in Settings (default 60%)
- Implementation: `AudioCueManager` subscribes to game events, plays one-shot SFX

---

## 11. MVP Scope (v1.0)

✅ **In scope:**
- 8 handcrafted tracks (3 free, 5 paid)
- 8 karts (3 free, 5 paid)
- 6 power-ups (3 speed/utility, 3 defensive)
- Swipe input + impulse physics (golf-feel tuned)
- Pinch-to-zoom + auto-zoom + one-finger accessibility mode
- Forced 30-second tutorial
- Solo practice + ghost replay + global leaderboard
- Local vs CPU (3 difficulties) + pass-and-play (2-4 players)
- Daily tracks (procedural, day-of-week difficulty rotation)
- Freemium IAP at $4.99 (track-select gate paywall)
- Anonymous auth + auto-generated display names
- Full accessibility: color-blind mode, one-finger mode, audio cues
- Original synthwave soundtrack (8-12 tracks)
- English only at launch

❌ **Out of scope (v1.1+):**
- Real-time online WebSocket sync (v1 ships with simulated CPU opponent)
- Friends list
- Offensive power-ups
- Track modifiers
- User-generated tracks
- Cosmetic kart skins
- Cross-device sync (anonymous ID lost on reinstall)
- Localization to non-English languages
- Daily track archive (missed dailies gone forever)

---

## 12. Risk Register

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Swipe input feels bad on different screen sizes | High | Critical | Build swipe-calibration tool that runs on first launch; per-device tuning presets |
| Composer availability delays audio | Medium | High | Start search week 2, contract by week 4; have procedural fallback (`SwipeAudio.cs`) |
| Track builder tool takes longer than expected | Medium | High | Start with simple manual track authoring; tool comes later |
| Supabase free tier limits hit unexpectedly | Medium | Medium | Add Redis cache layer if read QPS > 50/s; migrate to dedicated Postgres if needed |
| Ghost integrity cheating | Medium | Medium | Server-side validation, manual review queue, don't auto-ban |
| Performance issues with 3 ghost replays | Low | High | Profile early (week 6), cap ghost count, use object pooling |
| Art style drift during 8 tracks | Medium | Low | Lock art bible in week 1, weekly art review |
| Online matchmaking queue empty at launch | High | Medium | v1 ships without real online (CPU stub); v1.1 launches online after critical mass |
| Daily track procedural generation produces unfun tracks | Medium | High | TrackValidator runs at generation time; manual review queue for flagged tracks |
| IAP receipt validation issues | Low | High | Use Supabase Edge Function (server-side); test on TestFlight + Google Play internal track before launch |
| Tutorial abandonment (players quit during 30s) | Low | Medium | Track step-by-step completion rates; if >10% drop, simplify steps |

---

## 13. Roadmap (12 weeks)

| Week | Milestone | Deliverable |
|------|-----------|-------------|
| 1 | **Project setup + swipe prototype** ✅ | Unity project, swipe detection, kart moves with flicks, ghost replay, audio, trail, obstacles |
| 2 | **Kart physics + Bezier spline track system + checkpoint detection** ✅ | Impulse physics polish, real Bezier track (oval test), checkpoints, lap detection, TrackValidator, TrackLibrary |
| 3-4 | **Camera + zoom polish + Track Builder editor tool** | Pinch-to-zoom input handler, dynamic follow cam with smoothing, in-editor track builder (scene-view click to add Bezier points) |
| 5 | **Power-up system + IAP + EntitlementManager** | All 6 power-ups, spawn points, $4.99 IAP integration, paywall UI |
| 6 | **Ghost recording + tutorial + daily track generator** | Ghost recorder/player, 4-step tutorial scene, seeded daily track generator |
| 7-8 | **Full game flow + tracks 1-4 + free audio curation begins** | Menu → track select → race → results, 4 of 8 tracks built, browse free music libraries |
| 9 | **Supabase backend + auth + audio integration** | Anonymous auth, race result upload, leaderboard query, top-10 display, display name generation, integrate curated audio |
| 10 | **All 8 tracks + 8 karts + SFX from Kenney/Freesound** | Content complete, balancing pass begins, SFX sourced + integrated |
| 11 | **Polish: juice, SFX, haptics, accessibility + playtesting** | Particle effects, screen shake, haptics, color-blind mode, one-finger mode, par tuning |
| 12 | **Beta release** | TestFlight + Google Play internal track, crash fixes, store listing assets |

### v1.1 Roadmap (post-launch, 4-6 weeks)

| Week | Milestone |
|------|-----------|
| 1-2 | Real-time online WebSocket sync (replace `OnlineTurnClient` stub) |
| 3 | Matchmaking queue + ELO updates on match end |
| 4 | Online match replay viewer (7-day retention) |
| 5-6 | Polish + first content update (new tracks?) |

---

## 14. Open Items (All Resolved — see DECISIONS.md v3.0)

All 22 design questions are locked. No further design decisions needed until playtesting (weeks 10-11), when:
- Par values per track get finalized based on player data
- Power-up spawn point balance gets tuned
- CPU difficulty levels get validated against real player skill distribution

For the full decisions log with reasoning, see **[`Docs/DECISIONS.md`](./Docs/DECISIONS.md)** v3.0.

---

*End of PLAN.md v2.0. For design decisions, see `Docs/DECISIONS.md`. For multiplayer spec, see `Docs/MULTIPLAYER_REVISION.md`. For prototype status, see `Docs/PROTOTYPE_README.md`.*
