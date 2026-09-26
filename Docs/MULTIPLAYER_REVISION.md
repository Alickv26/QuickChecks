# Multiplayer Revision — Turn-Based "Swipe Golf" Model

**Document version**: 2.0
**Replaces**: Section 3 (Async Multiplayer Architecture) of `PLAN.md`
**Date**: 2026-09-27

---

## TL;DR — What Changed

The original plan was **async ghost racing** (solo time trials against recorded replays). The new model is **real-time turn-based multiplayer** — players alternate swipes during an active race, and the global leaderboard ranks by **fewest swipes to finish**, not fastest time.

This reframes QuickChecks as **"swipe golf"**: each swipe is a deliberate, weighted stroke, and the goal is to complete each track in as few strokes as possible. Every track has a "par" (theoretical minimum swipe count), and players compete to beat par.

---

## Three Game Modes

### Mode 1: Local vs CPU
- 1 human player races against 1-3 AI karts on the same track
- Turn-based: human swipes, kart coasts to stop, CPU takes its turn, etc.
- CPU difficulty: Easy / Medium / Hard (see §CPU AI below)
- Each player has a distinct kart color
- Race ends when all karts cross finish line or hit max-swipe cap

### Mode 2: Local Pass-and-Play
- 2-4 human players share one device
- Each player picks a kart at race start
- Between turns: "Pass to Player N" full-screen overlay (tap to dismiss)
- Same turn-based flow as vs CPU, just with humans alternating
- No turn timer (relaxed, casual mode)
- Great for couch multiplayer on tablets

### Mode 3: Online (Real-Time Turn-Based)
- 2 players connected via WebSocket
- Both karts visible on screen at all times
- 30-second turn timer; if it expires, turn auto-skips (kart stays in place)
- Live sync: each swipe event sent to opponent, replayed on their screen
- Race ends when both players finish or one concedes
- Result uploads to global leaderboard

---

## Turn Structure

A turn consists of:
1. **Turn start** — it becomes player N's turn, camera snaps to their kart
2. **Player input window** — player plans and executes a swipe (no time limit in local modes; 30s in online)
3. **Swipe execution** — kart receives impulse, starts moving
4. **Coast phase** — kart decelerates due to friction
5. **Turn end** — kart velocity drops below `stopThreshold` (5 px/s by default)
6. **Hand-off** — turn passes to next player

**Power-ups during turn**: collected by driving through them during your coast phase. Effects apply immediately. Duration power-ups tick in real game time (so they only benefit you during your turn, since you can't swipe during opponent's turn).

**Off-track during turn**: kart stops at boundary, turn ends. Position loss is the only penalty (no extra turn loss). Next turn, player swipes from current stuck position. Strategic implications: don't swipe into a corner you can't escape.

---

## Win Conditions

- **Race end**: all karts have crossed finish line, OR all karts hit max-swipe cap (50 swipes per track, configurable per track)
- **Winner**: kart with fewest total swipes
- **Tiebreaker**: faster finish time (in ms)
- **DNF**: kart that hits max-swipe cap without finishing — recorded as 50 swipes + their position-based estimated finish time

**Why this works**: matches the leaderboard criterion. Solo practice runs use the same win condition (you're racing against your own best score, and the global leaderboard).

---

## CPU AI Design

Three difficulty levels. Each CPU turn:
1. Identify next checkpoint position
2. Compute direction + distance from current kart position
3. Compute "optimal" swipe (direction toward checkpoint, magnitude scaled by distance)
4. Apply difficulty-based noise

### Difficulty Profiles

| Difficulty | Direction Error | Magnitude Variance | "Thinking" Delay | Behavior |
|-----------|-----------------|-------------------|------------------|----------|
| Easy | ±25° random | 50-80% of optimal | 0.5s | Often overshoots or undershoots |
| Medium | ±10° random | 75-95% of optimal | 1.0s | Mostly on line, occasional misjudgment |
| Hard | ±3° random | 92-100% of optimal | 1.5s | Near-optimal play, punishes mistakes |

**V1 implementation**: simple heuristic. **V2**: track-specific scripted optimal lines (designers record a "perfect run" per track, hard CPU follows it with small noise).

---

## Power-Up Interaction with Turn-Based

Power-ups collected during your coast phase:

| Power-up | Effect Timing | Notes |
|----------|----------------|-------|
| Boost | Duration starts when picked up, ticks in real time | If you end your turn during Boost, opponent's kart doesn't benefit (different kart) |
| Slingshot | Instant pull to next checkpoint | Resolves immediately, your turn continues until kart stops |
| Phase Dodge | Persists until used or 5s expires | If unused at turn end, carries to your next turn |
| Shield | 10s duration, ticks in real time | Defensive against future offensive power-ups (PvP only, not in v1) |
| Rewind | 1 use, lasts until end of race | Use during your turn to rewind 1s of your own position |
| Magnet | 6s duration, ticks in real time | Only useful during your turn (you can't collect power-ups during opponent's turn) |

---

## Ghost Format (Updated)

Ghost data now records swipe count as a first-class field. Used in:
- **Single-player practice mode**: replay your best run as a ghost on the track
- **Replay sharing**: top leaderboard runs downloadable as ghosts
- **CPU AI training data** (v2): hard difficulty could study top ghosts for optimal lines

```json
{
  "trackId": "track_04_neon_sprint",
  "kartId": "kart_viper",
  "finishTimeMs": 42850,
  "swipeCount": 32,
  "swipeEvents": [ /* same as before */ ],
  "powerUpsCollected": ["boost", "slingshot", "shield"]
}
```

---

## Leaderboard (Updated)

Sort key: **`swipeCount ASC, finishTimeMs ASC`**

Each track has:
- **Par**: theoretical minimum swipe count (designer-set, validated by speedrun)
- **Global Top 10**: lowest swipe counts worldwide
- **Friends Top 10**: lowest swipe counts among friends list
- **Player's Best**: their personal record

Stars (1-3 per track):
- ★ Bronze: finished under max-swipe cap
- ★★ Silver: finished at or below par
- ★★★ Gold: finished below par (par - 2 or better)

---

## Backend Schema (Updated)

```sql
-- Race results (replaces ghosts table from v1)
CREATE TABLE race_results (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  player_id UUID REFERENCES players(id),
  track_id TEXT NOT NULL,
  kart_id TEXT NOT NULL,
  swipe_count INTEGER NOT NULL,
  finish_time_ms INTEGER NOT NULL,
  mode TEXT NOT NULL CHECK (mode IN ('solo', 'vs_cpu_easy', 'vs_cpu_medium', 'vs_cpu_hard', 'pass_play', 'online')),
  ghost_url TEXT,  -- nullable; only stored for top-N finishes to save storage
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Leaderboard: best result per player per track
CREATE VIEW leaderboard AS
SELECT DISTINCT ON (player_id, track_id)
  player_id, track_id, swipe_count, finish_time_ms, mode, ghost_url, created_at
FROM race_results
ORDER BY player_id, track_id, swipe_count ASC, finish_time_ms ASC;

-- Online matchmaking queue
CREATE TABLE online_queue (
  player_id UUID PRIMARY KEY,
  track_id TEXT NOT NULL,
  joined_at TIMESTAMPTZ DEFAULT NOW()
);
-- When 2 players queue for same track, create a match
CREATE TABLE online_matches (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  track_id TEXT NOT NULL,
  player_a UUID REFERENCES players(id),
  player_b UUID REFERENCES players(id),
  status TEXT NOT NULL CHECK (status IN ('active', 'a_finished', 'b_finished', 'completed', 'abandoned')),
  current_turn_player UUID REFERENCES players(id),
  swipes_a INTEGER DEFAULT 0,
  swipes_b INTEGER DEFAULT 0,
  started_at TIMESTAMPTZ DEFAULT NOW()
);
```

---

## Online Protocol (Stub for v1, WebSocket in v2)

Real-time turn sync messages (JSON over WebSocket):

```json
// Player A → Server → Player B
{ "type": "swipe", "matchId": "...", "swipe": { "t": 120, "dx": 0.7, "dy": -0.3, "mag": 850 } }

// Player B → Server → Player A
{ "type": "turn_end", "matchId": "...", "finalPos": { "x": 12.3, "y": 4.5 } }

// Server → Both
{ "type": "turn_start", "matchId": "...", "player": "B" }

// Server → Both (timeout)
{ "type": "turn_skip", "matchId": "...", "player": "A" }

// Server → Both (race end)
{ "type": "race_end", "matchId": "...", "winner": "B", "swipes_a": 28, "swipes_b": 25 }
```

For v1 MVP, online is **stubbed** — the `OnlineTurnClient` class exists but only simulates opponents (CPU masquerading as online). Real WebSocket infrastructure lands in week 10.

---

## Updated MVP Scope

**Now in scope** (replacing the async ghost racing scope):
- ✅ 8 handcrafted tracks (difficulty 1-5 stars) — each with declared "par"
- ✅ 8 karts with distinct stats
- ✅ 6 power-ups (3 speed/utility, 3 defensive)
- ✅ Swipe input + impulse physics
- ✅ Pinch-to-zoom + auto-zoom
- ✅ Solo practice mode + ghost of personal best
- ✅ Local vs CPU (3 difficulty levels)
- ✅ Local pass-and-play (2-4 players)
- ✅ Global leaderboard by swipe count (Supabase)
- ✅ Online mode: **stubbed** for v1 (CPU opponent presented as "online player"); real WebSocket ships in v1.1

**Still out of scope for v1**:
- Real online WebSocket sync
- Friends list (uses global top-N instead)
- Offensive power-ups
- Track modifiers
- User-generated tracks

---

## Implications for Existing Code

| File | Change |
|------|--------|
| `GhostData.cs` | Add `swipeCount` field |
| `GhostRecorder.cs` | Increment swipe count on each swipe |
| `KartController.cs` | Add `OnKartStopped` event for turn manager |
| `AsyncMultiplayerManager.cs` | Rename → `MultiplayerManager` with mode enum |
| `SupabaseClient.cs` | Update methods: `SubmitRaceResult` instead of `UploadGhost`; leaderboard query sorts by swipe_count |
| **NEW** `TurnManager.cs` | Orchestrates turn order, end-of-turn detection, win condition |
| **NEW** `CPUPlayer.cs` | AI that takes swipes with difficulty-based noise |
| **NEW** `PassPlayManager.cs` | Local pass-and-play flow with "pass the phone" overlay |
| **NEW** `OnlineTurnClient.cs` | Stub for online turn sync |

---

## Open Questions (Decide Before Week 5)

1. **Max-swipe cap per track**: 50 is a placeholder. Should it scale by track length/difficulty? (e.g., track 1 = 30 max, track 8 = 80 max)
2. **Online match replay**: do online matches produce a "match ghost" both players can replay later? Probably yes for fairness/dispute resolution.
3. **CPU AI training**: for hard difficulty in v2, should we mine top leaderboard ghosts for optimal lines per track?
4. **Pass-and-play kart visibility**: do all karts show on screen simultaneously (current plan), or only the active player's kart (cleaner visual)?
5. **ELO/ranking for online**: should online wins/losses affect a player rank, or just track-by-track leaderboard?

---

*End of revision. For full architecture context, see `PLAN.md`.*
