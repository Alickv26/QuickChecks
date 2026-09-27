# Audio Brief — QuickChecks

**Audience**: Composer candidates evaluating whether to take on the project
**Length**: 1-page reference, expand as needed

---

## Game overview

**QuickChecks** is a mobile-first swipe-racing game where every flick is a deliberate, weighted impulse — closer to billiards or golf than to thumbstick steering. The competitive layer ranks by **fewest swipes to complete each track**, not fastest time.

**Visual style**: Minimal vector (think Mini Motorways, Data Wing). Dark teal background, cyan accent borders, yellow player kart, muted gray-blue ghost karts.

**Session length**: 1-3 minutes per race. Designed for pick-up-and-play during queues and commutes.

---

## Music direction

### Genre

**Synthwave / chill electronic**, 110-135 BPM depending on track intensity.

### Why synthwave

- Matches the minimal-vector visual aesthetic (dark, neon, geometric)
- 120-130 BPM aligns with the natural swipe rhythm (~1 swipe per second)
- Synth textures are non-intrusive during focused play
- Synthwave has a strong identity vs. generic electronic music

### Reference tracks

Listen to these to understand the target mood:

| Artist | Track | What to take from it |
|--------|-------|----------------------|
| Tycho | "Awake" | Melodic synth layers, warm pads |
| Kavinsky | "Nightcall" | Synthwave edge, driving rhythm |
| HOME | "Resonance" | Chill electronic, nostalgic warmth |
| Com Truise | "Brokewear" | Retro synth textures, tape saturation |
| The Midnight | "Days of Thunder" | Sweeping leads, vocal-adjacent melody |

### What we DON'T want

- ❌ EDM / dubstep drops (too aggressive for focused play)
- ❌ Orchestral / cinematic (doesn't match minimal vector art)
- ❌ Lo-fi hip-hop (too relaxed, kills urgency)
- ❌ Generic royalty-free "corporate tech" music
- ❌ Vocal tracks (distracting during races)

---

## Track list (8-12 tracks)

| # | Track name | In-game purpose | BPM | Length | Mood |
|---|-----------|-----------------|-----|--------|------|
| 1 | Main Menu Theme | Title screen, settings, track-select | 110 | 2-3 min, loopable | Calm, inviting |
| 2 | First Lap | Track 1 (★ difficulty) | 120 | 3-4 min, loopable | Upbeat, simple |
| 3 | Curves 101 | Track 2 (★) | 122 | 3-4 min | Similar to #2, slight variation |
| 4 | Chicane Sprint | Track 3 (★★) | 125 | 3-4 min | Building energy |
| 5 | Neon Sweep | Track 4 (★★) | 128 | 3-4 min | Classic synthwave |
| 6 | Hairpin Highway | Track 5 (★★★) | 130 | 3-4 min | Driving, intense |
| 7 | Slipstream | Track 6 (★★★) | 128 | 3-4 min | Sustained tension |
| 8 | Maze Run | Track 7 (★★★★) | 132 | 3-4 min | Complex, layered |
| 9 | Final Check | Track 8 (★★★★★) | 135 | 3-4 min | Climactic, epic |
| 10 | Daily Track | Procedural daily (any difficulty) | 125 | 3-4 min | Neutral, versatile |
| 11 | Race Complete | Victory fanfare | — | 5-10 sec | Triumphant |
| 12 | Race Failed | Loss / DNF sting | — | 3-5 sec | Subtle disappointment, not punishing |

---

## SFX library

| Event | Sound character | Notes |
|-------|-----------------|-------|
| Swipe (light) | Whoosh, ~220 Hz | Pitched by swipe velocity (220-880 Hz range) |
| Swipe (heavy) | Whoosh, ~880 Hz | Higher velocity = higher pitch |
| Power-up: Boost | Bright ascending chime | 0.3 sec |
| Power-up: Shield | Sustained tone, hum | 0.5 sec |
| Power-up: Magnet | Sparkle, arpeggio | 0.4 sec |
| Power-up: Slingshot | Forward-rushing whoosh | 0.5 sec |
| Power-up: Phase | Glitchy shimmer | 0.4 sec |
| Power-up: Rewind | Reverse tape effect | 0.6 sec |
| Boundary hit | Low thud, like golf ball hitting tree | 0.2 sec |
| Checkpoint pass | Subtle tick | Barely audible |
| Lap complete | Short fanfare | 1-2 sec |
| Race complete | Full layered fanfare | 3-5 sec |
| Race failed (DNF) | Descending tone, not punishing | 2-3 sec |
| Ghost pass | Descending whoosh | When opponent's ghost passes you |
| Menu hover | Soft click | UI feedback |
| Menu select | Confirm chime | UI feedback |
| Menu back | Descending click | UI feedback |

---

## Technical requirements

### File formats

- **Music**: WAV (44.1 kHz, 16-bit) + MP3 (320 kbps) for each track
- **SFX**: WAV (44.1 kHz, 16-bit), mono for SFX, stereo for ambient
- **Stems**: per-track stems as separate WAV files (drums, bass, melody, pads, etc.)
- **Source files**: project files (FL Studio, Ableton, Logic, etc.) delivered at end

### Length & looping

- Music tracks must **loop seamlessly** (start and end on the same beat)
- Target loop length: 2-4 minutes per track
- Race complete / failed stings: non-looping, fixed duration

### Loudness

- Mastered to **-14 LUFS** (Spotify standard, matches mobile listening)
- True peak: -1 dBTP

---

## Credits & ownership

- Composer credited in game credits screen + App Store / Play Store listing
- **Full buyout**: we own all audio outright, royalty-free, perpetual, worldwide
- Composer may use the work in their portfolio after launch (Dec 2026)
- Composer may NOT re-sell the tracks to other clients

---

## Optional: haptics

If the composer also does haptic design (rare but valuable):

- Light impact on swipe (10ms vibration)
- Medium impact on power-up pickup (30ms)
- Heavy impact on boundary hit (50ms)

If not, we'll handle haptics in-house using iOS/Android native APIs.

---

## Demos

If a composer wants to demonstrate fit, the most useful demo is:

**10-15 second snippet** of "First Lap" (Track 2 in the list above)
- 120 BPM
- Upbeat but not aggressive
- Synth lead + pad + simple drum pattern
- Should feel "swipeable" — like you'd flick the kart to the beat

We pay for any demo we use at the composer's standard rate.

---

## Contact

Project repo: https://github.com/Alickv26/QuickChecks
Contact: [your email]

---

*End of audio brief. For composer search process, see `Docs/COMPOSER_SEARCH.md`.*
