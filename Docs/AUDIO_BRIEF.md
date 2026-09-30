# Audio Brief — QuickChecks

**Approach**: Free / open-source audio (per Decision 15, revised)
**Budget**: $0 — sourced from public-domain and permissively-licensed libraries
**Status**: Curation phase begins Week 8

---

## Game overview

**QuickChecks** is a mobile-first swipe-racing game where every flick is a deliberate, weighted impulse — closer to billiards or golf than to thumbstick steering. The competitive layer ranks by **fewest swipes to complete each track**, not fastest time.

**Visual style**: Minimal vector (think Mini Motorways, Data Wing). Dark teal background, cyan accent borders, yellow player kart, muted gray-blue ghost karts.

**Session length**: 1-3 minutes per race. Designed for pick-up-and-play during queues and commutes.

---

## Music direction

### Genre

**Synthwave / chill electronic**, 110-135 BPM depending on track intensity.

### Reference mood (for curation)

Listen to these to understand the target vibe — we want free tracks that sound similar:

| Artist | Track | What to take from it |
|--------|-------|----------------------|
| Tycho | "Awake" | Melodic synth layers, warm pads |
| Kavinsky | "Nightcall" | Synthwave edge, driving rhythm |
| HOME | "Resonance" | Chill electronic, nostalgic warmth |
| Com Truise | "Brokewear" | Retro synth textures, tape saturation |

### What we DON'T want

- ❌ EDM / dubstep drops (too aggressive for focused play)
- ❌ Orchestral / cinematic (doesn't match minimal vector art)
- ❌ Lo-fi hip-hop (too relaxed, kills urgency)
- ❌ Generic royalty-free "corporate tech" music
- ❌ Vocal tracks (distracting during races)

---

## Track list (8-12 tracks needed)

| # | Track name | In-game purpose | Target BPM | Length | Mood |
|---|-----------|-----------------|------------|--------|------|
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

## Free audio sources

### Music libraries (curate 8-12 tracks from these)

| Source | URL | License | Notes |
|--------|-----|---------|-------|
| **Free Music Archive** | freemusicarchive.org | CC-BY, CC0 | Search "synthwave", "chillwave", "electronic" |
| **Incompetech** | incompetech.com | CC-BY | Kevin MacLeod's huge library |
| **Pixabay Music** | pixabay.com/music | Pixabay License (no attribution) | Free for commercial use |
| **YouTube Audio Library** | youtube.com/audiolibrary | YouTube License | Free for any use, including commercial |
| **SoundCloud CC** | soundcloud.com (filter CC-BY) | CC-BY | Search "synthwave" + filter "Creative Commons" |
| **Bensound** | bensound.com | Bensound License (free with attribution) | Free for commercial use with credit |
| **ccMixter** | ccmixter.org | CC-BY, CC0 | Remix-friendly community |

### SFX libraries

| Source | URL | License | Notes |
|--------|-----|---------|-------|
| **Freesound** | freesound.org | Various CC | Search "whoosh", "chime", "ui click" |
| **Kenney.nl** | kenney.nl/assets | CC0 | Game-focused packs, no attribution needed |
| **OpenGameArt** | opengameart.org | Various CC | Mixed quality, check license per asset |
| **Pixabay SFX** | pixabay.com/sound-effects | Pixabay License | Free, no attribution |

### License requirements per source

| License | Attribution required? | Commercial use? | Modifications? |
|---------|----------------------|-----------------|----------------|
| CC0 | No | Yes | Yes |
| CC-BY | **Yes** (in credits) | Yes | Yes |
| CC-BY-SA | Yes + share-alike | Yes | Yes (derivative must be CC-BY-SA) |
| CC-BY-NC | Yes | **No** | Yes |
| Pixabay License | No | Yes | Yes |
| Bensound License | Yes (in credits) | Yes | No (must use as-is) |

**Avoid**: CC-BY-NC (non-commercial) and CC-BY-ND (no derivatives) — incompatible with a commercial game.

---

## Curation criteria

For each candidate track, evaluate:

| Criterion | Pass/Fail |
|----------|-----------|
| License allows commercial use (CC0, CC-BY, Pixabay) | ✅ Required |
| Style fits synthwave / chill electronic brief | ✅ Required |
| BPM is in 110-135 range | ✅ Required |
| Loops seamlessly (or can be edited to loop) | ✅ Required |
| No vocals | ✅ Required |
| Length 2-4 minutes | ✅ Required |
| Quality is professional (no clipping, good mix) | ✅ Required |
| Doesn't sound like stock corporate music | ✅ Required |
| Attribution recorded for credits screen | ✅ Required (if CC-BY) |

---

## Credits screen

For each track with attribution requirement (CC-BY), include in credits:

```
Music:
- "Track Name" by Artist Name
  Source: freemusicarchive.org
  License: CC-BY 4.0 (https://creativecommons.org/licenses/by/4.0/)
```

For SFX with attribution:

```
Sound Effects:
- "Sound Name" by Artist Name
  Source: freesound.org
  License: CC-BY 4.0
```

CC0 and Pixabay License tracks/SFX don't need attribution but can still be credited out of courtesy.

---

## Technical requirements

### File formats

- **Music**: WAV (44.1 kHz, 16-bit) for in-game + OGG (96 kbps) for size-optimized builds
- **SFX**: WAV (44.1 kHz, 16-bit), mono for SFX, stereo for ambient
- **Length**: music tracks 2-4 min (loopable), SFX 0.1-3 sec

### Loudness

- Mastered to **-14 LUFS** (Spotify standard, matches mobile listening)
- True peak: -1 dBTP

### Loop editing

Some free tracks won't loop seamlessly. For those, edit in Audacity (free):
1. Open track in Audacity
2. Find a natural break point (end of phrase, ~16 bars)
3. Trim to that point
4. Apply a 50ms crossfade at the loop point
5. Export as WAV

---

## Curation tracking

Use this table to track candidates as you find them:

| # | Track name | Artist | Source | License | BPM | Style fit (1-5) | Loops? | Used for | Attribution needed? |
|---|-----------|--------|--------|---------|-----|-----------------|--------|----------|---------------------|
| 1 | | | | | | | | | |
| 2 | | | | | | | | | |
| 3 | | | | | | | | | |
| 4 | | | | | | | | | |

---

## Action items (Week 8-9)

- [ ] Browse Free Music Archive, Pixabay Music, Incompetech for synthwave tracks
- [ ] Curate 10-12 candidates that fit the brief (BPM 110-135, no vocals, loopable)
- [ ] Verify license per track (must be commercial-use OK)
- [ ] Download WAV files, edit for loop if needed
- [ ] Organize into `Assets/_Project/Audio/Music/` with naming convention:
  - `track_01_first_lap.wav`, `track_02_curves_101.wav`, etc.
  - `menu_theme.wav`, `daily_track.wav`, `race_complete.wav`, `race_failed.wav`
- [ ] Record attribution info in a `CREDITS.md` file at repo root
- [ ] Source SFX from Freesound + Kenney
- [ ] Integrate via MusicPlayer + SfxPlayer (scripts already in `Scripts/Audio/`)

---

*End of audio brief. For audio direction decision, see `Docs/DECISIONS.md` §15.*
