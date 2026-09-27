# Week 1 Prototype (Polished) — Swipe Feel Validation

> Goal: validate the core swipe-racing feel before building 8 tracks, 8 karts, 6 power-ups, and a multiplayer backend. If the feel is wrong, fix it now — it's 100x cheaper to fix here than after the full game is built.

---

## What's in the prototype (polish pass)

A single scene (`Assets/_Project/Scenes/Prototype.unity`) that contains:

- A rectangular play area (40 × 20 units) with 4 walls
- **4 obstacles** in the middle forcing direction variety (not just straight L→R)
- A yellow square kart that responds to swipes (billiards-style: each swipe replaces velocity)
- A green finish line on the right side
- An on-screen HUD showing **swipe count / par / best**, velocity, kart state, race status
- Pinch-to-zoom camera that follows the kart
- Swipe event recording + **best run persisted** across sessions via PlayerPrefs

### Polish features added

1. **Kart squash + stretch** on each swipe (`SwipeFeedback.cs`)
   - Kart compresses perpendicular to swipe direction, snaps back over 120ms
   - 8-particle burst per swipe in cyan
2. **Fading trail** behind the kart (`KartTrail.cs`)
   - Path fades from cyan (recent) to yellow (old) over 3 seconds
   - Lets player visually see "did I take 4 swipes or 6?"
3. **Synthesized swipe whoosh** (`SwipeAudio.cs`)
   - No audio asset files — generates a sine-wave burst on the fly
   - Pitched by swipe velocity (faster swipe = higher pitch)
   - Audio gives immediate feedback that swipes have "weight"
4. **Solo ghost replay** (`SoloGhostPlayer.cs`)
   - Records best run (fewest swipes, tiebreaker = faster time)
   - Persists across sessions via PlayerPrefs
   - On next race, spawns a translucent gray ghost kart that replays your best swipes
   - Ghost uses a separate `GhostSwipeEvent` so it doesn't drive your kart
5. **Race results with par comparison**
   - HUD shows "par 5 swipes" — your goal is to hit par or beat it
   - On finish: shows your swipes vs par, and delta vs your best
   - "NEW BEST!" indicator when you set a new personal record
6. **Curved obstacle course**
   - 4 obstacles force direction changes — pure straight-line swipes won't work
   - Tests whether the swipe angle variety feels natural

**The loop**: flick the kart from the left side to the right, navigating around 4 obstacles. Each flick is a discrete impulse — slow drags are ignored. The kart coasts and decelerates between swipes. When you cross the green line, the race ends and shows your swipe count vs par + your best. Press **R** to retry. Press **C** to clear your best run.

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

### Linux (Manjaro / Arch) setup notes

If you're on Manjaro/Arch and the Unity Hub AppImage doesn't launch:

1. **AppImage permission**: `chmod +x UnityHub.AppImage` then `./UnityHub.AppImage`
2. **FUSE dependency** (AppImage requires it): `sudo pacman -S fuse2 fuse3`
3. **If AppImage still fails** — extract and run directly:
   ```bash
   ./UnityHub.AppImage --appimage-extract
   cd squashfs-root
   ./AppRun
   ```
4. **Alternative: install via AUR** (no AppImage needed):
   ```bash
   yay -S unityhub
   # or for the long-term support editor:
   yay -S unity-editor
   ```
5. **Linux build module**: when installing Unity 2022.3.20f1 via Unity Hub, make sure to check "Linux Build Support (IL2CPP)" in the modules list — needed if you want to build a desktop Linux binary for testing.
6. **Editor performance**: Unity's Linux editor is officially supported but can have window manager quirks. If you get black screen / GL errors, try launching with `--force-opengl` or use X11 instead of Wayland for the editor session.

Once Unity Hub is running, the project setup is identical to Mac/Windows.

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

| Parameter | Asset | Default (golf-feel) | Try if feel is... |
|-----------|-------|---------|-------------------|
| `minSwipeVelocity` | InputSettings | 1000 px/s | Too high if swipes get rejected often |
| `maxSwipeDurationMs` | InputSettings | 220 ms | Lower if slow drags are slipping through |
| `velocityToImpulseScale` | InputSettings | 1.5 | Higher = harder flicks feel more impactful |
| `maxImpulseMagnitude` | InputSettings | 1500 | Cap on max speed per swipe |
| `frictionPerSecond` | KartStats | 0.22 | Higher = stops faster (more "golf-like") |
| `impulseMultiplier` | KartStats | 1.1 | Per-kart responsiveness scaling |
| `stopThreshold` | KartStats | 8 px/s | Below this = stopped (cleanness of stop) |
| `maxSpeed` | KartStats | 1500 u/s | Hard cap, prevents physics breakage |

> **Note on defaults (v2)**: tuned toward "golf-like feel" per design call — kart stops dead on wall hit, no sliding. Friction raised from 0.15 → 0.22 so kart decelerates faster between swipes (clearer "now I'm planning my next shot" moment). Stop threshold raised from 5 → 8 px/s for cleaner stops. Swipe velocity floor raised from 800 → 1000 px/s to filter more slow drags.

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
