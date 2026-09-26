# Design Decisions — QuickChecks

**Document version**: 1.0
**Date**: 2026-09-27
**Resolves**: Open questions in `Docs/MULTIPLAYER_REVISION.md` §"Open Questions"

> These decisions are made with reasoning. Override any of them — just say which one(s) and I'll update the docs and code.

---

## Decision 1: Max-swipe cap per track — Designer-set, with formula as default

**Question**: Should the 50-swipe cap scale by track length/difficulty, or be fixed?

**Decision**: **Each track gets a designer-set `maxSwipeCap` in its `TrackDefinition`.** Default formula: `30 + (difficultyStars × 10)`. So:
- Track 1 (★): 40 swipes
- Track 3 (★★): 50 swipes
- Track 5 (★★★): 60 swipes
- Track 8 (★★★★★): 80 swipes

Designers can override per track if a specific track needs more or less headroom.

**Why**: A flat cap punishes long tracks and is too generous on short ones. Scaling by difficulty is a good baseline, but designers need flexibility for outlier tracks (e.g., a very long Track 3 that needs 60). This matches how Mario Kart gives different time limits per track.

**Code change**: Add `maxSwipeCap` field to `TrackDefinition` (default `-1`, meaning "use formula"). `TurnManager` resolves `-1` to `30 + track.difficultyStars * 10` at race start.

---

## Decision 2: Online match replay — Store both ghosts for 7 days, then auto-delete

**Question**: Do online matches produce a "match ghost" both players can replay later?

**Decision**: **Yes — store both players' ghosts for 7 days, then auto-delete via Supabase cron.** Both players can replay the match from their results screen during that window.

**Why**:
- **Dispute resolution**: if a player suspects cheating (impossibly few swipes), they can review the replay
- **Learning**: losing player can study the winner's swipe pattern to improve
- **Storage cost**: 2 ghosts × ~7 KB × ~10K matches/day = ~140 MB/day. 7-day retention = ~1 GB peak. Manageable on Supabase free tier.
- **Privacy**: 7 days is short enough that no long-term tracking concerns arise

**Code change**: Add `match_id` field to `race_results` table. Both rows share the same `match_id`. Add Supabase scheduled function (pg_cron) that deletes rows older than 7 days where `mode = 'online'`.

---

## Decision 3: CPU AI training — Hybrid: hand-authored baseline + noise from random top-10 ghost

**Question**: For Hard difficulty in v2, should we mine top leaderboard ghosts for optimal lines, or have designers hand-author them?

**Decision**: **Hybrid.** Designers hand-author one "optimal line" per track (recorded as a ghost during dev). Hard CPU follows this line with ±3° noise. Medium and Easy use the same line with progressively more noise (±10°, ±25°) and lower magnitude (per existing `CPUPlayer` config).

**Why**:
- Pure ML (training on top ghosts) is overkill for v1, requires data we don't have yet
- Pure hand-authoring is rigid — same line every match feels robotic
- **Hybrid**: designer-set line + noise = consistent difficulty, but each match feels slightly different. Top-ghost mining can be a v2 enhancement once we have player data.

**Code change**: Add `optimalLineGhostUrl` field to `TrackDefinition`. `CPUPlayer` loads this ghost at race start and uses it as the basis for swipe direction (with noise added per difficulty).

---

## Decision 4: Pass-and-play kart visibility — All karts visible, inactive dimmed

**Question**: Do all karts show on screen simultaneously, or only the active player's kart?

**Decision**: **All karts always visible.** Inactive karts are dimmed to ~40% alpha. Camera follows the active player's kart. Other players' karts stay rendered at their last position.

**Why**:
- **Spatial context**: players see how far ahead/behind they are — adds tension
- **Strategic decisions**: "Player 2 is right at the chicane — I need to be precise here to catch up"
- **Visual interest**: empty track is boring; 4 karts on track is dynamic
- **Dimming inactive karts** keeps visual focus on the active player without losing spatial info

**Code change**: When `TurnManager.OnTurnStart` fires, dim all karts except the active one (`SpriteRenderer.color` × 0.4 alpha). When `OnTurnEnd` fires, restore all to full alpha for the brief transition. Camera rig follows only the active kart.

---

## Decision 5: ELO/ranking for online — Both: per-track leaderboard + global ELO

**Question**: Should online wins/losses affect a global player rank, or just track-by-track leaderboards?

**Decision**: **Both.**
- **Per-track leaderboard**: ranks by `swipeCount ASC, finishTimeMs ASC` (existing design). Shows skill on a specific track.
- **Global ELO**: starts at 1000, +25 for online win, -15 for online loss, +10 for online draw. Updated only from online ranked matches (not CPU, not pass-and-play).
- **Matchmaking**: pairs players within ±100 ELO of each other to keep matches competitive.

**Why**:
- Per-track leaderboards reward specialists (player who's great at one track)
- Global ELO rewards generalists and gives matchmaking a signal
- Asymmetric ELO delta (+25 win / -15 loss) keeps average climbing slowly — feels rewarding, not punishing. Standard in casual games (Clash Royale, Hearthstone use similar).
- Skipping CPU/pass-play from ELO prevents inflation from grinding easy CPU wins.

**Code change**: Add `elo` column to `players` table (default 1000). Add `OnlineMatchmakingService.MatchmakeAsync(playerId)` that queries `online_queue` for players within ELO range. Add `UpdateEloOnMatchEnd(winnerId, loserId)` server-side function.

---

## Summary Table

| # | Question | Decision | Risk |
|---|----------|----------|------|
| 1 | Max-swipe cap scaling | Designer-set, default formula `30 + stars×10` | Low — designers can override per track |
| 2 | Online replay storage | 7-day retention, both ghosts stored | Low — ~1 GB peak storage on free tier |
| 3 | CPU AI for Hard | Hybrid: hand-authored line + ±3° noise | Low — can enhance with ML in v2 |
| 4 | Pass-play kart visibility | All visible, inactive dimmed to 40% alpha | Low — already implemented in design |
| 5 | ELO ranking | Per-track + global ELO (online only) | Medium — matchmaking queue may be empty at launch |

---

## Implementation Order

These decisions affect different parts of the codebase and can be implemented independently:

1. **Decision 1** (cap scaling): implement in week 2 alongside track system
2. **Decision 4** (kart dimming): implement in week 5 with pass-and-play
3. **Decision 3** (CPU hybrid AI): implement in week 6 with CPU AI work
4. **Decision 5** (ELO): implement in week 9 with backend
5. **Decision 2** (replay retention): implement in week 10 with backend polish

---

*End of decisions doc. For full multiplayer context, see `Docs/MULTIPLAYER_REVISION.md`.*
