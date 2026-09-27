# Design Document (GDD) — QuickChecks

> Living document. Update when mechanics change.

## Pillars

1. **Flick, don't steer** — every input is a deliberate impulse
2. **Read the track, not the screen** — minimal HUD, deep visual focus
3. **Race the field, not the clock** — ghosts are rivals, not just time trials
4. **Tight beats fast** — precision through narrow gaps feels better than top speed

---

## Swipe Mechanics — Detail

### What counts as a "valid" swipe

| Threshold | Value | Why |
|-----------|-------|-----|
| Min peak velocity | 800 px/s | Filter out slow drags |
| Max duration | 250 ms | Force a flick, not a draw |
| Min distance | 40 px | Filter out taps |

If swipe fails any threshold → silently ignored (no negative feedback to player, just no input).

### Impulse math

```
swipeDirection = (endPos - startPos).normalized
swipeMagnitude = min(peakVelocity * velocityToImpulseScale, maxImpulseMagnitude)
newKartVelocity = swipeDirection * swipeMagnitude
```

Velocity is **replaced**, not added. This is the billiards model: each flick is a fresh shot, not continuous steering.

### Why this feels good

- **Discrete inputs** = clear cause-effect (player knows each swipe mattered)
- **No continuous control** = simpler to balance, no "perfect line" exploit
- **Friction between swipes** = creates rhythm (swipe → coast → swipe → coast)
- **Speed cap on impulse** = top players can't break the game with super-fast swipes

---

## Track Design Principles

1. **Read from afar, execute up close** — long straights let player plan; tight sections demand execution
2. **Power-ups as decision points** — place at forks where picking one path = picking one power-up
3. **Camera zoom is a difficulty dial** — tight sections auto-zoom in; long straights auto-zoom out
4. **Punish mistakes, don't kill runs** — off-track = stop + 2s penalty, not race over
5. **Every track has a memorable moment** — one signature corner or feature

### Track anatomy

Each track contains:
- 1 start/finish straight
- 2-3 fast sections (boost-friendly)
- 2-3 technical sections (zoom-in required)
- 1 signature moment (e.g., a hairpin, a slingshot around an obstacle, a multi-path fork)
- 6-12 checkpoints
- 4-8 power-up spawn points

---

## Kart Roster (8 karts)

Balanced across 3 archetypes: **Speed**, **Balanced**, **Grip**.

| # | Name | Archetype | Top Speed | Boost | Friction | Unlock |
|---|------|-----------|-----------|-------|----------|--------|
| 1 | Starter | Balanced | Med | Med | Med | Default |
| 2 | Viper | Speed | High | High | High friction | Finish Track 1 |
| 3 | Anchor | Grip | Low | Low | Low friction | Finish Track 2 |
| 4 | Dart | Speed | High | Med | High friction | Finish Track 3 |
| 5 | Comet | Balanced | High | Low | Med | Finish Track 4 |
| 6 | Boulder | Grip | Low | Med | Low friction | Finish Track 5 |
| 7 | Whisper | Speed | Med | High | Low friction | Finish Track 6 |
| 8 | Apex | Balanced | High | High | Low friction | Finish Track 7 |

Trade-off: high-top-speed karts need bigger swipes (slower flicks lose impulse). Low-friction karts coast longer between swipes (easier on long straights, harder in tight sections).

---

## Power-Up Tuning

See `PLAN.md` section 2.4 for the full power-up list.

**Spawn rules**:
- Power-ups respawn 5s after pickup
- Max 2 active pickups on a track at once (prevents hoarding)
- Last 200m before finish line: no new pickups (pure skill section)

**Slot rules**:
- Each kart holds 1 power-up
- Picking up a new one discards the old (visual indicator: dropped power-up stays on track for 3s)

---

## Camera Behavior

| Situation | Zoom | Transition |
|-----------|------|------------|
| Long straight (default) | maxZoom (20) | Slow lerp |
| Approaching chicane | minZoom (5) | Fast lerp (0.5s) |
| In chicane | minZoom (5) | Hold |
| Exiting chicane | defaultZoom (12) | Medium lerp |
| Player pinch | Whatever they set | Instant, holds 2s |
| Finish line | Slow zoom out to 18 | Cinematic |

---

## Race Flow

1. **Pre-race**: track preview, kart select, loadout pick (3 power-up slots)
2. **Countdown**: 3-2-1, kart pulses with each tick (visual anticipation)
3. **Racing**: HUD shows lap counter, timer, ghost positions (top-right)
4. **Finish**: confetti + slowmo + final time vs personal best + ghost upload prompt
5. **Results**: leaderboard rank (if uploaded), compare to top 10 ghosts, replay option

---

## Progression

- Tracks unlock linearly: finish track N under target time → unlock track N+1
- Karts unlock linearly: finish track N → unlock kart N+1
- No currency, no IAP — pure progression
- Stars per track (1-3): bronze = finish, silver = beat target time, gold = beat target + no off-track events

---

## Sound Direction

- Music: 120-130 BPM synthwave, chill (not aggressive)
- SFX layers: swipe whoosh (pitched), power-up chime, boundary bonk, finish fanfare
- Haptics: light impact on swipe, medium on power-up, heavy on boundary hit
- Audio ducking: music dips during power-up SFX for clarity

---

*End of GDD. For architecture, see PLAN.md.*
