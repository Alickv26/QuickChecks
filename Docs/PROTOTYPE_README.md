# Week 1 Prototype — Swipe Feel Validation

> Goal: validate the core swipe-racing feel before building 8 tracks, 8 karts, 6 power-ups, and a multiplayer backend. If the feel is wrong, fix it now — it's 100x cheaper to fix here than after the full game is built.

---

## What's in the prototype

A single scene (`Assets/_Project/Scenes/Prototype.unity`) that contains:

- A rectangular play area (40 × 20 units) with 4 walls
- A yellow square kart that responds to swipes (billiards-style: each swipe replaces velocity)
- A green finish line on the right side
- An on-screen HUD showing swipe count, velocity, and finish status
- Pinch-to-zoom camera that follows the kart
- Swipe event recording (in memory; not persisted)

**The loop**: flick the kart from the left side to the right. Each flick is a discrete impulse — slow drags are ignored. The kart coasts and decelerates between swipes. When you cross the green line, the race ends and shows your swipe count + time. Press **R** to reset.

---

## One-click setup

In Unity, after opening the project:

1. Wait for Unity to import packages and compile scripts (first time only, ~2-5 minutes)
2. If prompted by TextMeshPro, click **Import TMP Essential Resources** (Window > TextMeshPro > Import TMP Essential Resources)
3. Run menu: **Tools > QuickChecks > Build Prototype Scene**
4. The scene opens automatically — press **Play** in the Unity Editor

That's it. You should see:
- A dark teal background
- 4 translucent cyan walls forming a rectangle
- A yellow square (the kart) on the left
- A vertical green line on the right (finish line)
- HUD text in the top-left showing live stats

If anything fails to load, check the Console (Window > General > Console) for errors with `[Prototype]` prefix.

---

## Testing on a real device

The editor's mouse fallback works for basic testing, but the **swipe feel is different on touch**. To validate the actual mobile feel:

### Option A: Unity Remote (fastest, no build)
1. Install **Unity Remote 5** on your phone (Play Store / App Store)
2. In Unity: Edit > Project Settings > Editor > Unity Remote > Device = Any
3. Connect phone via USB, press Play
4. Phone becomes your touch screen, editor shows the game

Limitation: latency is ~50-100ms, which can make swipes feel sluggish. Use for sanity checks, not final tuning.

### Option B: Build to device (true feel)
1. File > Build Settings
2. Add Open Scenes (Prototype.unity)
3. Switch platform to iOS or Android
4. Plug in phone, click **Build and Run**
5. First build takes 5-10 minutes (compiling IL2CPP)

This gives you the real feel — no latency, real touch hardware. Use this for the actual tuning session.

---

## Validation checklist

Run through these in order. If any of them feel wrong, the swipe model needs adjustment before proceeding.

### 1. Swipe detection thresholds

| Test | Expected |
|------|----------|
| Slow drag across screen (over 0.5s) | Kart does NOT move (swipe rejected) |
| Quick flick (under 250ms) | Kart moves |
| Tap (no movement) | Kart does NOT move |
| Flick with low peak velocity | Kart doesn't move (must exceed 800 px/s) |
| Flick at 45° angle | Kart moves diagonally at 45° |

If too many flicks get rejected → lower `InputSettings.minSwipeVelocity` to ~600 px/s.
If slow drags accidentally register → raise `InputSettings.minSwipeVelocity` or shorten `maxSwipeDurationMs`.

### 2. Impulse feel

| Test | Expected |
|------|----------|
| Hard flick from rest | Kart shoots across, decelerates, stops |
| Gentle flick from rest | Kart moves short distance, stops quickly |
| Flick while kart is moving | Velocity is REPLACED (not added) — direction changes immediately |
| Stop threshold | Kart stops cleanly below ~5 px/s (no jitter) |

If kart feels "floaty" → raise `KartStats.frictionPerSecond` (try 0.25).
If kart stops too abruptly → lower friction (try 0.10).
If kart feels sluggish → raise `KartStats.impulseMultiplier` or `InputSettings.velocityToImpulseScale`.

### 3. Coast + stop rhythm

This is the **core game feel**. Swipe, coast, swipe, coast — should feel like billiards or golf.

- Each swipe should feel "weighted" — you commit to a direction + magnitude, no take-backs
- Between swipes, there should be a clear "now I'm planning my next shot" moment
- The kart should stop completely (not crawl) so you can think

If coast time is too long → kart takes forever to stop, breaks rhythm. Raise friction.
If coast time is too short → kart stops too fast, no time to evaluate. Lower friction.

### 4. Boundary collision

| Test | Expected |
|------|----------|
| Swipe directly into a wall | Kart stops at wall, no bouncing |
| Swipe at 45° into wall | Kart slides along wall? (currently stops — design call) |
| Multiple swipes into walls | Each swipe from stopped position at wall |

**Design question for you**: should hitting a wall stop the kart dead (current), or should the kart slide along the wall (preserving tangential velocity)? The latter feels more like air hockey, the former feels more like golf. Pick one.

### 5. Pinch-to-zoom

- Pinch out (fingers apart) → camera zooms out
- Pinch in (fingers together) → camera zooms in
- Zoom should feel smooth, not jittery
- Player pinch should override auto-zoom for 2 seconds

For the prototype, auto-zoom is off (no track segments to trigger it). You're testing the pinch input itself.

### 6. Finish line + race end

- Drive kart across green finish line → HUD switches to "RACE COMPLETE" with swipe count + time
- Console logs `[Prototype] 🏁 Race finished!`
- Press **R** → kart respawns at start, race restarts, swipe count resets to 0

### 7. Swipe count = quality metric

The whole game hinges on **fewest swipes = best**. Try to complete the race in:
- 1 swipe (impossible? maybe with a perfect angle)
- 3 swipes (great)
- 5 swipes (good)
- 10+ swipes (you're "driving", not "flicking" — adjust your technique)

If a typical first-time player can do it in 3-5 swipes, the model works. If they need 10+, the friction or impulse scaling is off.

---

## Suggested tuning session

1. **First run**: don't change anything. Just play 5 races. Note your swipe counts.
2. **Identify the worst-feeling aspect** from the checklist above.
3. **Adjust ONE parameter at a time** (in the relevant ScriptableObject).
4. **Re-test** — keep a notes file with: param value → swipe count → feel rating (1-5).
5. **Repeat** until you'd describe the feel as "weighty but satisfying" or "snappy".

### Key parameters to tune (in order of impact)

| Parameter | Asset | Default | Try if feel is... |
|-----------|-------|---------|-------------------|
| `minSwipeVelocity` | InputSettings | 800 px/s | Too high if swipes get rejected often |
| `maxSwipeDurationMs` | InputSettings | 250 ms | Lower if slow drags are slipping through |
| `velocityToImpulseScale` | InputSettings | 1.2 | Higher = harder flicks feel more impactful |
| `maxImpulseMagnitude` | InputSettings | 1200 | Cap on max speed per swipe |
| `frictionPerSecond` | KartStats | 0.15 | Higher = stops faster (more "golf-like") |
| `impulseMultiplier` | KartStats | 1.0 | Per-kart responsiveness scaling |
| `stopThreshold` | KartStats | 5 px/s | Below this = stopped (cleanness of stop) |
| `maxSpeed` | KartStats | 1200 u/s | Hard cap, prevents physics breakage |

### Where to edit

- **InputSettings**: `Assets/_Project/ScriptableObjects/Settings/InputSettings.asset`
- **KartStats**: `Assets/_Project/ScriptableObjects/Karts/Kart_Starter.asset`

Double-click the .asset file in Unity's Project window → tweak values in Inspector → press Play to test. Changes save automatically.

---

## Known limitations (intentional — week 1 scope)

- **No checkpoints**: race ends as soon as kart crosses finish line X. No required path.
- **No off-track respawn**: hitting a wall just stops the kart. You can swipe again from there.
- **No power-ups**: kart doesn't pick anything up.
- **No other karts**: solo only, no CPU or pass-and-play yet.
- **No ghost persistence**: swipe recording happens in memory, lost on scene reload.
- **No mobile-specific UI**: HUD is debug-style, not final game UI.

These are all v2+ features. The prototype is purely about validating swipe feel.

---

## What "success" looks like

After 10-15 minutes of tuning, you should be able to say:

> "Yes — flicking the kart feels satisfying. Each swipe has weight. The coast-then-stop rhythm is right. I can complete the prototype race in 3-5 swipes with practice, and I want to try again to do it in fewer."

If you can say that, we proceed to Week 2 (track prototype with boundaries + checkpoints).
If you can't, we adjust the model: maybe the velocity-REPLACE math should be velocity-ADD, or maybe the swipe thresholds need to be way different, or maybe the friction curve needs to be non-linear.

---

## Decision log

Record your tuning decisions here as you test:

| Date | Param changed | Old → New | Result | Feel rating (1-5) |
|------|---------------|-----------|--------|-------------------|
| 2026-09-27 | (defaults) | — | baseline | — |

---

*End of prototype readme. For architecture context, see `PLAN.md`.*
