# QuickChecks — Swipe Golf Racing

A mobile-first swipe-based racing game for touch screens. Players flick their kart across handcrafted tracks, alternating turns with opponents (CPU, pass-and-play, or online). Each swipe is a discrete impulse — closer to billiards than to thumbstick steering. The global leaderboard ranks by **fewest swipes to finish**, making every flick a strategic decision.

> **Status**: Planning + scaffold phase
> **Target platform**: iOS + Android (Unity 2022.3 LTS+)
> **Multiplayer model**: Real-time turn-based (CPU / pass-and-play / online)

---

## Core Loop

1. Player selects a mode: Solo practice, vs CPU, pass-and-play, or online
2. Each player picks a kart (distinct stats: speed / boost / friction tradeoffs)
3. **Turn-based racing**: player A swipes → kart coasts to stop → player B swipes → repeat
4. Collect power-ups during your coast phase (boost, slingshot, shield, etc.)
5. Off-track = kart stops at boundary (no extra penalty, just lost position)
6. Race ends when all karts finish or hit max-swipe cap (50)
7. **Winner = fewest swipes**; tiebreaker = faster finish time
8. Solo runs also upload to global leaderboard ("swipe golf" — beat par per track)

**Feel target**: Each swipe should feel like a billiards shot or a golf stroke — satisfying, deliberate, weighted. Each swipe is precious.

---

## Implementation Plan

Full plan with milestones, architecture, and file structure lives in **[`PLAN.md`](./PLAN.md)**. Multiplayer revision (turn-based model) is in **[`Docs/MULTIPLAYER_REVISION.md`](./Docs/MULTIPLAYER_REVISION.md)**.

### Quick Architecture Snapshot

```
┌─────────────────────────────────────────────────────────┐
│                    Game Flow Manager                     │
│   (Menu → Mode Select → Track Select → Race → Results)  │
└─────────────────────────────────────────────────────────┘
        │                                  │
        ▼                                  ▼
┌──────────────────┐              ┌─────────────────────┐
│  Input Layer     │              │  Turn-Based Layer  │
│  - SwipeDetector │              │  - TurnManager     │
│  - ZoomGesture   │              │  - CPUPlayer       │
│  - CPUPlayer     │              │  - PassPlayManager │
└──────────────────┘              │  - OnlineTurnClient│
        │                         └─────────────────────┘
        ▼                                  │
┌──────────────────────────────────────────┴───────────────┐
│              Kart Physics & Control                      │
│  - Swipe → impulse vector (direction + magnitude)        │
│  - Momentum + friction + track-boundary collision       │
│  - PowerUp state machine (boost / shield / phase)        │
│  - OnKartStopped event → TurnManager.NotifyKartStopped   │
└──────────────────────────────────────────────────────────┘
        │
        ▼
┌──────────────────────────────────────────────────────────┐
│              Track System                                │
│  - Handcrafted TrackSegments (Bezier spline paths)       │
│  - Boundary colliders + checkpoints                      │
│  - Power-up spawn points                                 │
│  - Camera rig with pinch-to-zoom                         │
│  - Per-track "par" (target swipe count for leaderboard)  │
└──────────────────────────────────────────────────────────┘
        │
        ▼
┌──────────────────────────────────────────────────────────┐
│              Multiplayer + Backend Layer                 │
│  - Local vs CPU (3 difficulty levels)                    │
│  - Local pass-and-play (2-4 players, same device)       │
│  - Online (real-time WebSocket, v1.1 — v1 uses stub)     │
│  - Supabase backend: auth, race results, leaderboards    │
│  - Leaderboard ranks by swipe_count ASC, finish_time ASC │
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
│   │   │   └── Scripts/
│   │   │       ├── Core/         # GameFlow, EventBus
│   │   │       ├── Input/        # SwipeDetector, InputSettings
│   │   │       ├── Racing/       # KartController, TurnManager, CPUPlayer, PowerUps
│   │   │       ├── Track/        # TrackDefinition, TrackBuilder (editor)
│   │   │       ├── Camera/       # CameraRig, ZoomController
│   │   │       ├── Ghost/        # GhostRecorder, GhostPlayer, GhostData
│   │   │       ├── Multiplayer/  # PassPlayManager, OnlineTurnClient, SupabaseClient
│   │   │       ├── UI/           # HUD, PassPlayOverlay, Menus
│   │   │       └── Data/         # SaveSystem
│   │   ├── Editor/              # TrackBuilderWindow, validation tools
│   │   └── (QuickChecks.Runtime.asmdef + QuickChecks.Editor.asmdef)
│   ├── Plugins/              # Third-party (Supabase SDK, DOTween, etc.)
│   └── Settings/             # URP assets, input mappings
├── Packages/
│   └── manifest.json         # URP, Input System, TMP, Cinemachine, Newtonsoft, Addressables
├── ProjectSettings/
│   ├── ProjectVersion.txt    # Unity 2022.3.20f1
│   └── TagManager.asset      # Custom layers (Track, Kart, PowerUp, Checkpoint...) + tags
├── Docs/
│   ├── DESIGN.md             # Game design doc (GDD)
│   ├── ART_BIBLE.md          # Visual style guide
│   ├── MULTIPLAYER_REVISION.md # Turn-based 'swipe golf' spec
│   └── DECISIONS.md          # Resolved design decisions with reasoning
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

### Prerequisites

- **Unity Hub** (latest): https://unity.com/download
- **Unity 2022.3.20f1** (LTS) — install via Unity Hub
- **IDE** with C# support: Visual Studio, Rider, or VS Code (Unity Hub will install one)
- **Supabase project** (free tier is fine) — for backend, set up in week 9

### Open in Unity

1. **Clone the repo**:
   ```bash
   git clone https://github.com/Alickv26/QuickChecks.git
   cd QuickChecks
   ```

2. **Open Unity Hub** → click **Add project from disk** → select the `QuickChecks` folder

3. Unity detects `ProjectSettings/ProjectVersion.txt` and prompts you to install the matching version if missing

4. **First open** takes ~2-5 minutes — Unity imports `Packages/manifest.json` and compiles the Assembly Definitions

5. **Open the Track Builder** once Unity loads: menu `Tools > QuickChecks > Track Builder`

### Assembly Definitions

The project uses **two Assembly Definitions** for clean compile boundaries:

- `QuickChecks.Runtime` — all gameplay code (in `Assets/_Project/Scripts/`)
- `QuickChecks.Editor` — editor-only tools like Track Builder (in `Assets/_Project/Editor/`)

Drop new scripts anywhere under `Assets/_Project/Scripts/` — they're picked up automatically.

### Layers and Tags

The project ships with custom layers in `ProjectSettings/TagManager.asset`:

| Layer | Name | Used by |
|-------|------|--------|
| 8 | Track | Track surface (no collision) |
| 9 | TrackBoundary | Off-track collision detection |
| 10 | Kart | Player + CPU karts |
| 11 | GhostKart | Ghost replays (no collision) |
| 12 | PowerUp | Power-up pickup triggers |
| 13 | Checkpoint | Lap progress triggers |
| 14 | Camera | Camera rig |
| 15 | UI | In-world UI overlays |

Custom tags: `Checkpoint`, `Boundary`, `PowerUp`, `FinishLine`, `Kart`, `GhostKart`.

### Key Dependencies (`Packages/manifest.json`)

| Package | Why |
|---------|-----|
| `com.unity.render-pipelines.universal` (URP 14) | Modern mobile rendering |
| `com.unity.inputsystem` (1.7) | Multi-touch + swipe + pinch detection |
| `com.unity.textmeshpro` (3.0) | UI text rendering |
| `com.unity.cinemachine` (2.10) | Camera follow + damping |
| `com.unity.nuget.newtonsoft-json` | Ghost JSON serialization |
| `com.unity.addressables` | Asset loading + remote content delivery |

See **[`PLAN.md`](./PLAN.md)** for the full design document and **[`Docs/DECISIONS.md`](./Docs/DECISIONS.md)** for resolved design decisions.

---

## License

TBD — pick before public release.
