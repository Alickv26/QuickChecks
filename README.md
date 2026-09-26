# QuickChecks — Swipe Racing Game

A mobile-first swipe-based racing game for touch screens. Players flick their kart across handcrafted tracks, time swipes to grab power-ups, and race against async ghosts of friends and the global community.

> **Status**: Planning + scaffold phase
> **Target platform**: iOS + Android (Unity 2022.3 LTS+)
> **Multiplayer model**: Async turn-based (ghost races + leaderboards)

---

## Core Loop

1. Player selects a track + kart + power-up loadout
2. Swipe to flick the kart forward; each swipe is a discrete impulse (no slow-drag steering)
3. Kart coasts and decelerates; a new swipe redirects momentum
4. Collect power-ups on track for speed boosts / shields / phase-dodges
5. Leaving the track = stop (restart from checkpoint or DNF)
6. Finish lap(s) → time recorded as ghost → uploaded to leaderboard
7. Other players race the same track, see your ghost competing alongside them

**Feel target**: Each swipe should feel like a billiards shot — satisfying, deliberate, with weight.

---

## Implementation Plan

Full plan with milestones, architecture, and file structure lives in **[`PLAN.md`](./PLAN.md)**.

### Quick Architecture Snapshot

```
┌─────────────────────────────────────────────────────────┐
│                    Game Flow Manager                     │
│   (Menu → Track Select → Race → Results → Upload Ghost)│
└─────────────────────────────────────────────────────────┘
        │                                  │
        ▼                                  ▼
┌──────────────────┐              ┌─────────────────────┐
│  Input Layer     │              │  Replay/Ghost Layer│
│  - SwipeDetector │              │  - GhostRecorder    │
│  - ZoomGesture   │              │  - GhostPlayer      │
└──────────────────┘              └─────────────────────┘
        │
        ▼
┌──────────────────────────────────────────────────────────┐
│              Kart Physics & Control                      │
│  - Swipe → impulse vector (direction + magnitude)        │
│  - Momentum + friction + track-boundary collision       │
│  - PowerUp state machine (boost / shield / phase)        │
└──────────────────────────────────────────────────────────┘
        │
        ▼
┌──────────────────────────────────────────────────────────┐
│              Track System                                │
│  - Handcrafted TrackSegments (Bezier spline paths)       │
│  - Boundary colliders + checkpoints                      │
│  - Power-up spawn points                                 │
│  - Camera rig with pinch-to-zoom                         │
└──────────────────────────────────────────────────────────┘
        │
        ▼
┌──────────────────────────────────────────────────────────┐
│              Async Multiplayer Layer                     │
│  - Local ghost storage (PlayerPrefs/JSON)                │
│  - Backend: Supabase (Postgres + REST)                   │
│  - Leaderboards per track per region                      │
└──────────────────────────────────────────────────────────┘
```

---

## Tech Stack

| Layer | Choice | Why |
|-------|--------|-----|
| Engine | Unity 2022.3 LTS (C#) | Best mobile pipeline, mature 2D, large asset store |
| 2D Rendering | Unity 2D + Universal RP | Crisp vector sprites, mobile-friendly post-processing |
| Physics | Built-in 2D physics + custom kart impulse model | Lightweight, predictable |
| Input | New Input System + custom swipe detector | Multi-touch + pinch zoom support |
| Backend | Supabase (Postgres) | Free tier, REST + RLS, no server to manage |
| Auth | Supabase Anonymous Auth | Zero-friction onboarding |
| Leaderboards | Supabase view + indexed queries | Simple, scales to ~10K concurrent users |
| Ghost format | Compact JSON (timestamp + swipe events) | Small payloads (~5KB per race) |
| Analytics | Unity Analytics + Sentry | Free, built-in crash reporting |
| Cloud save | Supabase Storage (JSON blobs) | Cross-device ghost sync |

---

## Repo Structure

```
QuickChecks/
├── Assets/
│   ├── _Project/
│   │   ├── Art/              # Sprites, vector sources (.svg/.ai/.png)
│   │   ├── Audio/            # Music + SFX
│   │   ├── Prefabs/          # Karts, power-ups, track pieces
│   │   ├── Scenes/           # Boot, Menu, TrackSelect, Race, Results
│   │   ├── ScriptableObjects/# Track definitions, kart stats, power-up defs
│   │   └── Scripts/
│   │       ├── Core/         # GameFlow, SceneManagement, EventBus
│   │       ├── Input/        # SwipeDetector, PinchZoom
│   │       ├── Racing/       # KartController, KartPhysics, PowerUpSystem
│   │       ├── Track/        # TrackSegment, TrackValidator, Checkpoints
│   │       ├── Camera/       # CameraRig, ZoomController
│   │       ├── Ghost/        # GhostRecorder, GhostPlayer
│   │       ├── Multiplayer/  # AsyncMultiplayerManager, LeaderboardService
│   │       ├── UI/           # HUD, Menu, TrackSelect, Results
│   │       └── Data/         # SaveSystem, SupabaseClient
│   ├── Plugins/              # Third-party (Supabase SDK, DOTween, etc.)
│   └── Settings/             # URP assets, input mappings
├── Packages/
│   └── manifest.json         # Package dependencies
├── ProjectSettings/          # Unity project config
├── Docs/
│   ├── DESIGN.md             # Game design doc (GDD)
│   ├── ART_BIBLE.md          # Visual style guide
│   └── TECH_SPEC.md          # Detailed technical architecture
├── Tools/                    # Editor scripts, track-building tools
└── README.md
```

---

## Roadmap (12 weeks)

| Week | Milestone | Deliverable |
|------|-----------|-------------|
| 1 | **Project setup + swipe prototype** | Empty Unity project, swipe detection working, kart moves with flicks |
| 2 | **Kart physics + track prototype** | Impulse-based movement, friction, boundary collision, 1 test track |
| 3-4 | **Camera + zoom + track tooling** | Pinch-to-zoom, dynamic follow cam, in-editor track builder |
| 5 | **Power-up system** | 3 speed/utility + 3 defensive power-ups, spawn points |
| 6 | **Ghost recording + playback** | Record swipe events, replay as ghost kart on track |
| 7-8 | **Full game flow** | Menu → track select → race → results, 4 of 8 tracks built |
| 9 | **Supabase backend** | Auth, ghost upload, leaderboard query, top-10 display |
| 10 | **All 8 tracks + 8 karts** | Content complete, balancing pass |
| 11 | **Polish: juice, SFX, haptics** | Particle effects, screen shake, haptic feedback, audio |
| 12 | **Beta release** | TestFlight + Google Play internal track, crash fixes |

---

## Getting Started

This repo is initialized with the folder structure and stub scripts. To start developing:

1. Install Unity Hub + Unity 2022.3 LTS
2. Open Unity Hub → Add project → point to this repo's root
3. Unity will recognize the project structure; create a new scene in `Assets/_Project/Scenes/`
4. Open `Assets/_Project/Scripts/Input/SwipeDetector.cs` to see the swipe detection interface

See **[`PLAN.md`](./PLAN.md)** for the full design document.

---

## License

TBD — pick before public release.
