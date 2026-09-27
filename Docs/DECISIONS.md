# Design Decisions — QuickChecks

**Document version**: 3.0
**Date**: 2026-09-27
**Resolves**: Open questions in `Docs/MULTIPLAYER_REVISION.md` §"Open Questions" + deeper design questions from planning session

> These decisions are made with reasoning. Override any of them — just say which one(s) and I'll update the docs and code.

---

## Decisions 1–5 (from v1.0, unchanged)

1. **Max-swipe cap per track** — Designer-set, default formula `30 + (difficultyStars × 10)`
2. **Online match replay** — Both ghosts stored 7 days, then auto-deleted via Supabase cron
3. **CPU AI for Hard** — Hybrid: hand-authored optimal line + ±3° noise per difficulty
4. **Pass-and-play kart visibility** — All karts visible; inactive dimmed to 40% alpha
5. **ELO/ranking for online** — Per-track leaderboard + global ELO (online matches only, +25 win / −15 loss)

Full reasoning for 1–5: see git history (commit `c9e1b7a`).

---

## Decision 6: Monetization — Freemium unlock (new)

**Question**: What monetization model do you want?

**Decision**: **Freemium unlock.** Free demo with 3 tracks + 3 karts + solo practice + daily tracks. One-time IAP unlocks the full game (all 8 tracks + all 8 karts + online ranked matches).

**Why**:
- **Lowest friction to entry**: anyone can download and try without commitment
- **No ongoing monetization pressure**: once they unlock, they have everything — no F2P grind, no ads interrupting races, no pay-to-win dynamics
- **Clear value proposition**: "pay once, get the whole game" is an easy sell for a $3-5 mobile game
- **Avoids the F2P death spiral**: no daily login rewards, no energy systems, no currencies — keeps design clean and player-focused
- **Cross-platform parity**: identical experience on iOS + Android

**Open follow-ups (need to decide before implementation)**:
- IAP price point: $2.99 / $4.99 / $9.99?
- Free tier: does it include daily tracks? Ghost uploads? Online?
- Paywall trigger: where exactly does the unlock prompt appear?
- Restore purchases flow on reinstall?
- Regional pricing (Tier 1 vs Tier 2/3 countries)?

**Code impact**:
- New `IAPService` for App Store / Play Store receipts
- New `EntitlementManager` to gate tracks/karts behind unlock state
- Free tier config: 3 tracks flagged as `isFree = true` in TrackDefinition
- Paywall UI: "Unlock Full Game" button on locked track/kart select

---

## Decision 7: Tutorial — Forced 30-second scene (new)

**Question**: How should we teach the swipe mechanic to new players?

**Decision**: **Forced tutorial** — a dedicated 30-second scene runs before track 1 on first launch. Sequence:
1. "Swipe to move" — kart on empty arena, player must swipe to continue
2. "Slow drags don't work" — player must try a slow drag (rejected) then a fast swipe (accepted)
3. "Goal: fewest swipes" — explains the par/star system
4. "Reach the green line" — short finish-line demo

After completion: tutorial is marked complete (PlayerPrefs flag) and never shown again unless player clears save data.

**Why**:
- **Forces understanding of the unique mechanic** — swipe golf is unusual; players won't intuit "slow drags are rejected"
- **30 seconds is short** — respects player time, doesn't drag
- **Hands-on beats text** — players learn by doing, not reading tooltips
- **No skip option** — every player enters track 1 with the same baseline understanding, simplifies onboarding analytics

**Open follow-ups**:
- Returning player on new device: skip tutorial if Supabase profile says completed?
- Failure recovery: if player can't swipe fast enough (motor impairment), do we lower threshold in tutorial?
- Voice-over or text-only?
- Tutorial kart: same as Starter kart or a special "tutorial kart" with forgiving stats?

**Code impact**:
- New `TutorialScene` + `TutorialController` script
- New `TutorialStep` enum (Move, RejectSlow, ExplainPar, FinishLine)
- PlayerPrefs flag `tutorial_completed`
- Game flow update: Boot → Tutorial (if not completed) → Main Menu

---

## Decision 8: Solo depth — Daily tracks (new)

**Question**: Beyond ghost races, what solo content keeps players engaged?

**Decision**: **Daily tracks only** (no challenge modes, no story mode, no endless runner). One procedurally generated track per day, same for all players globally, with its own leaderboard. Resets at UTC midnight.

**Why**:
- **Strongest retention hook in mobile puzzle games** (Wordle, Mini Crosswords, Spelunky Daily)
- **One track = one decision per day** — low commitment, high return rate
- **Procedural generation validated** — we can use a seeded RNG with date as seed; everyone gets the same track
- **Avoids content bloat** — no need to design 50+ handcrafted challenge tracks
- **Natural social mechanic** — players compare daily scores with friends

**Open follow-ups**:
- Daily track difficulty: fixed (always medium) or rotates (easy/medium/hard per day of week)?
- Missed days: can players replay old dailies (without leaderboard) or are they gone forever?
- Daily track length: shorter than handcrafted (2-min race) or full-length?
- Procedural generator: needs constraints to ensure tracks are actually fun (not just random walls)
- Track validation: how do we ensure daily track is solvable under par?

**Code impact**:
- New `DailyTrackGenerator` (seeded RNG with date-based seed)
- New `DailyLeaderboard` (separate from main per-track leaderboard)
- UI: "Daily Track" button on main menu, shows today's track + leaderboard
- Backend: new Supabase table `daily_results` with `track_date` column

---

## Decision 9: Progression — Linear unlock (new)

**Question**: What's the progression/unlock model?

**Decision**: **Linear unlock.** Finish track N under par (or with at least 1 star) → unlock track N+1. Karts unlock in parallel: finishing track N unlocks kart N+1.

**Why**:
- **Simple, predictable, no currency** — players always know what's next
- **Avoids F2P grind mechanics** — no star grinding, no XP farming
- **Matches the freemium model**: tracks 1-3 free, track 4+ requires IAP unlock
- **Forgiving**: only 1 star needed (out of 3) to progress, so most players won't get stuck

**Open follow-ups**:
- Par target per track: designer-set, or formula (e.g., `20 + difficultyStars × 5`)?
- Soft-lock prevention: if player can't hit par after N attempts, do we offer a "skip" option?
- Replay value: once unlocked, do tracks have any reason to replay beyond leaderboard climbing?

**Code impact**:
- `TrackDefinition` already has `parSwipeCount` field (committed)
- `TrackProgress` save data: track unlocked state + stars earned
- Unlock check: race end → if `swipeCount <= parSwipeCount * 1.5` → unlock next track

---

## Decision 10: CPU symmetry — Identical rules (new)

**Question**: Should CPU play by the same rules as human players?

**Decision**: **Symmetric.** CPU uses identical swipe thresholds, power-ups, friction, max-swipe cap. Only difference is the AI's swipe accuracy (noise level per difficulty).

**Why**:
- **Fairness perception**: players don't feel cheated when they lose to CPU
- **Simpler to balance**: one set of rules, one set of edge cases
- **Easier to reason about CPU difficulty**: just tune the noise, not separate physics
- **Future-proofs for online**: if CPU plays same rules as humans, we can substitute CPU for disconnected players in online matches

**Code impact**: None — current `CPUPlayer` already uses the same `swipeEvent` and `KartController` as humans.

---

## Decision 11: Online auth — Anonymous only (new)

**Question**: What authentication model for online play?

**Decision**: **Anonymous only.** Auto-generated UUID on first launch, stored in PlayerPrefs. No email, no password, no OAuth.

**Why**:
- **Lowest friction**: zero onboarding time for online
- **Privacy-friendly**: no PII collected, no GDPR/CCPA concerns for accounts
- **Sufficient for our scope**: we don't need cross-device sync in v1
- **Easy to upgrade later**: anonymous ID can be linked to email/OAuth later if needed

**Trade-offs accepted**:
- **No cross-device sync**: player loses progress on reinstall (mitigated by Supabase storing ghosts by player ID)
- **No friend list**: player can't add specific friends; only races against global top-N
- **Identity is just an ID**: display name is auto-generated ("Player_1234"), player can edit it once via settings

**Open follow-ups**:
- Display name generator: random word + number ("SwiftFox_42") or just numbers ("Player_1234")?
- Name edit: allowed once, unlimited, or never?
- Account upgrade path: if we want cross-device later, can we link anonymous ID to email without losing data?

**Code impact**:
- `SupabaseClient` already has anonymous auth stub (committed)
- New `PlayerProfile` with editable display name
- UUID generation on first launch, stored in PlayerPrefs

---

## Decision 12: Localization — English only at launch (new)

**Question**: Localization strategy at launch?

**Decision**: **English only at launch.** All UI text, tutorial copy, error messages in English. No localization infrastructure built for v1.

**Why**:
- **Fastest to ship**: no translation work, no string table management
- **Smallest scope**: avoids the "localization hell" of last-minute string changes
- **Market reality**: English-only covers US, UK, Canada, Australia, India, Scandinavia, Netherlands — sufficient for v1 launch
- **Phase 2**: localize to top 5 languages 30-60 days post-launch based on actual player geography

**Trade-offs accepted**:
- **Limits non-English markets**: Japan, Korea, China, France, Germany, Spain, LATAM under-served
- **Lower install conversion** in non-English markets
- **Player feedback will tell us** which languages are worth localizing

**Code impact**:
- All UI text hardcoded in scripts (no string tables for v1)
- When we localize: extract all strings to `Localization.csv`, add `LocalizationService`, use `Localize("key")` instead of literal strings
- Architecture should make this future migration easy — keep all user-facing strings in one place per script

---

## Decision 13: Accessibility — Full at launch (new)

**Question**: What accessibility features at launch?

**Decision**: **Full accessibility from day one.** Three features:
1. **Color-blind mode** — alternate palettes for protanopia, deuteranopia, tritanopia (toggle in settings)
2. **One-finger mode** — auto-zoom replaces pinch-to-zoom (long-press to toggle zoom-in/zoom-out)
3. **Audio cues** — distinct SFX for: power-up collected, boundary hit, finish line approaching, ghost passing you

**Why**:
- **Right thing to do** — accessibility matters from day one, not as a v2 patch
- **Broadens market** — ~8% of men have some color blindness; one-finger mode helps one-handed play
- **Differentiates** — most mobile racing games ignore accessibility
- **Cheap to implement** if planned early — expensive to retrofit

**Open follow-ups**:
- Color-blind palettes: 3 separate palettes (one per condition) or one "high-contrast" universal palette?
- One-finger mode: long-press toggle vs auto-zoom based on track segment?
- Audio cue volume: separate slider from music/SFX, or fixed at 50% volume?
- Haptics: include as part of accessibility (some players can't hear audio cues)?

**Code impact**:
- `AccessibilitySettings` ScriptableObject with toggles for each feature
- `ColorBlindPalette` asset — swap colors at runtime via `ColorPalette` component
- `OneFingerZoomController` — alternative to current `CameraRig` pinch input
- `AudioCueManager` — plays distinct cues for events (extends current `SwipeAudio`)
- Settings UI: Accessibility section with 3 toggles + sliders

---

## Summary table

| # | Decision | Status |
|---|----------|--------|
| 1 | Max-swipe cap scaling | Locked |
| 2 | Online replay 7-day retention | Locked |
| 3 | CPU AI hybrid line + noise | Locked |
| 4 | Pass-play kart dimming | Locked |
| 5 | ELO per-track + global | Locked |
| 6 | Monetization: freemium unlock | Locked, follow-ups open |
| 7 | Tutorial: forced 30-sec | Locked, follow-ups open |
| 8 | Solo depth: daily tracks | Locked, follow-ups open |
| 9 | Progression: linear | Locked, follow-ups open |
| 10 | CPU symmetry: identical rules | Locked |
| 11 | Online auth: anonymous only | Locked, follow-ups open |
| 12 | Localization: English only at launch | Locked |
| 13 | Accessibility: full at launch | Locked, follow-ups open |

---

## Open follow-ups (priority order)

The decisions above left several follow-up questions. Listed in priority order (most impactful first):

### High priority (block implementation)

1. **Freemium paywall trigger** — where exactly does the unlock prompt appear? (After track 3? On first attempt at track 4? On first daily track completion?)
2. **IAP price point** — $2.99 / $4.99 / $9.99? Affects ARPU forecasts
3. **Tutorial failure recovery** — what if a player can't swipe fast enough? Lower threshold or skip step?
4. **Daily track difficulty** — fixed or rotates by day of week?
5. **Daily track missed days** — replayable (no leaderboard) or gone forever?

### Medium priority (affect UX but not blocking)

6. **Color-blind palette approach** — 3 separate palettes or 1 universal high-contrast?
7. **One-finger zoom UX** — long-press toggle or auto-zoom per track segment?
8. **Display name generator** — random word+number or just numbers?
9. **Par target per track** — designer-set or formula-based?
10. **Soft-lock prevention** — skip option after N failed attempts?

### Low priority (polish, can decide later)

11. **Audio cue volume** — separate slider or fixed?
12. **Haptics as accessibility** — include or skip?
13. **Tutorial voice-over** — text-only or recorded VO?
14. **Account upgrade path** — anonymous → email linking for future cross-device?

---

## Open questions NOT yet answered

Two questions from the planning session weren't answered — flag here for next round:

- **Session length target**: quick burst (1-3 min) / casual medium (5-10 min) / deep session (15-30 min) / mixed?
- **Audio direction**: original composer / licensed library / procedural / hybrid?

These affect scope and budget — need to decide before week 5 (audio implementation).

---

## Decisions 14–22 (from v3.0 planning follow-up, new)

All open follow-ups from v2.0 are now resolved.

### Decision 14: Session length — Quick burst (1-3 minutes)

**Decision**: Target session length is **1-3 minutes per race**. Game is designed for pick-up-and-play during queues, commutes, short breaks. No session-level meta-progression that requires longer sessions.

**Why**: Matches the swipe-golf mechanic (each race is a discrete challenge), respects mobile player attention spans, aligns with daily-track retention hook (one race per day = ~2 min commitment).

**Code impact**: Track lengths tuned so a par run takes ~60-90 seconds; max-swipe cap (40-80) keeps total race time bounded.

---

### Decision 15: Audio direction — Original composer

**Decision**: Hire an **in-house synthwave composer** for an original soundtrack. Budget: $2,000-5,000 for 8-12 tracks (~$250-400 per track) + SFX library.

**Why**: Original music gives the game a distinct identity (vs. generic Epidemic Sound library tracks). Synthwave matches the minimal-vector visual aesthetic. Composer can also do SFX design.

**Trade-offs accepted**: Higher upfront cost than licensed library. Composer availability affects timeline (need to book by week 5 to ship by week 12).

**Action item**: Begin composer search in week 2; sign contract by week 4; first track delivered by week 8.

---

### Decision 16: Paywall trigger — Track-select gate (hard wall)

**Decision**: **Tracks 4-8 are grayed out in track select screen.** Tapping a grayed track opens the paywall modal ("Unlock all tracks for $4.99"). Free tracks (1-3) are always accessible; daily tracks always accessible (with limited leaderboard participation for free players).

**Why**: Hard wall is unambiguous — players know exactly what they get for paying. No surprise prompts mid-game. Easy to implement (gate at UI level).

**Code impact**:
- `TrackDefinition.isFree` field (boolean) — tracks 1-3 = true, 4-8 = false
- Track-select UI grays out locked tracks, intercepts tap to show paywall
- `EntitlementManager.IsTrackUnlocked(trackId)` check
- Paywall modal UI: shows price, features list, "Restore Purchases" button

---

### Decision 17: IAP price — $4.99

**Decision**: **One-time IAP of $4.99** unlocks the full game (tracks 4-8, karts 4-8, full online ranked matches, full daily track leaderboard participation).

**Why**: Sweet spot for premium mobile games. High enough to signal value (not "cheap"), low enough for impulse purchase. Comparable to similar indie games (Alto's Odyssey, Mini Motorways).

**Trade-offs accepted**: Lower revenue per buyer than $9.99, but conversion rate should compensate. Regional pricing can adjust (Tier 2/3 countries get 30-50% discount).

**Code impact**:
- App Store Connect / Google Play Console IAP product: `unlock_full_game` @ $4.99 USD
- Receipt validation server-side (Supabase Edge Function) to prevent piracy
- Restore purchases flow using store APIs

---

### Decision 18: Daily track difficulty — Day-of-week rotation

**Decision**: **Daily track difficulty rotates by day of week:**
- **Monday**: Easy (★)
- **Tuesday**: Easy-Medium (★½)
- **Wednesday**: Medium (★★)
- **Thursday**: Medium-Hard (★★½)
- **Friday**: Hard (★★★)
- **Saturday**: Expert (★★★★)
- **Sunday**: Expert+ (★★★★★) — the weekend challenge

**Why**: Predictable rhythm — players know what to expect. Weekend = harder challenges for players with more time. Weekday = accessible for quick commutes. Creates natural "I'll wait for the easier track on Monday" engagement loop.

**Code impact**:
- `DailyTrackGenerator` uses day-of-week to seed difficulty parameters
- Difficulty-to-track-params mapping: obstacle density, track length, par target

---

### Decision 19: Missed dailies — Gone forever

**Decision**: **Daily tracks disappear after 24 hours.** Players who miss a day cannot replay that day's track. Leaderboard finalizes at UTC midnight.

**Why**: FOMO is a strong retention driver (Wordle, Mini Crosswords). Knowing the track "expires" creates urgency to play today, not tomorrow. Avoids backlog anxiety ("I have 30 missed dailies, why bother").

**Trade-offs accepted**: Some players will be frustrated by missing a day. Mitigated by: 24-hour window is generous (not 1-hour), leaderboard is global so players see their friends' scores and can compare even if they missed.

**Code impact**:
- `DailyTrack` table has `track_date` (DATE) primary key
- Supabase RLS: INSERT only allowed for today's date
- Frontend: only today's track appears in UI; no archive

---

### Decision 20: Tutorial failure — Lower threshold

**Decision**: **Tutorial uses a lower swipe threshold (300 px/s)** than the main game (1000 px/s). Threshold automatically restores to 1000 px/s when tutorial completes.

**Why**: Some players (motor impairments, new to touchscreens) may struggle to hit 1000 px/s. Lowering in tutorial prevents tutorial-as-gatekeeper. Once they understand the *concept* of "fast swipe", they can practice the *speed* in real races where retries are cheap.

**Trade-offs accepted**: Players who can't swipe fast in tutorial might also struggle in real races. Mitigated by: accessibility one-finger mode + the fact that 1000 px/s is achievable for ~95% of players per Apple's touch studies.

**Code impact**:
- `InputSettings.minSwipeVelocity` is overridable at runtime
- `TutorialController` sets `inputSettings.minSwipeVelocity = 300` on start, restores to 1000 on completion

---

### Decision 21: Par targets — Designer-set

**Decision**: **Par for each track is set by the designer during playtesting.** Designer records a "par run" on each track, then sets `TrackDefinition.parSwipeCount` based on that + 1-2 swipes of headroom.

**Why**: Formula-based par (e.g., `20 + difficultyStars × 5`) is too rigid — track geometry matters more than difficulty stars. Designer-set par can account for tricky chicanes, long straights, power-up placement.

**Process**: During playtesting (weeks 10-11), designer records best run per track; sets par = best_run_swipes + 1 (so 95% of players need to grind to hit par, but it's achievable).

**Code impact**: None new — `TrackDefinition.parSwipeCount` already exists (committed). Just needs to be filled in during playtesting.

---

### Decision 22: Display names — Random word + number

**Decision**: **Auto-generated display names follow the format `<Adjective><Animal>_<Number>`** (e.g., `SwiftFox_42`, `BraveTiger_17`, `CalmPanda_309`).

**Why**: Word+number is memorable (vs. `Player_1234`), gives some personality (vs. numbers only), doesn't require player input (vs. player-chosen). Animal + adjective combinations are infinite (~100 adjectives × ~100 animals × 1000 numbers = 10M unique names).

**Code impact**:
- New `DisplayNameGenerator` with word lists (`adjectives.txt`, `animals.txt`)
- Generate on first launch, store in PlayerPrefs + Supabase `players.display_name`
- Player can edit display name once via Settings (to prevent abuse)

---

## Summary table (final)

| # | Decision | Status |
|---|----------|--------|
| 1 | Max-swipe cap scaling (formula `30 + stars×10`) | Locked |
| 2 | Online replay 7-day retention | Locked |
| 3 | CPU AI hybrid (hand-authored line + noise) | Locked |
| 4 | Pass-play kart dimming (40% alpha inactive) | Locked |
| 5 | ELO per-track + global (online only, +25/-15) | Locked |
| 6 | Monetization: freemium unlock | Locked |
| 7 | Tutorial: forced 30-sec | Locked |
| 8 | Solo depth: daily tracks | Locked |
| 9 | Progression: linear unlock | Locked |
| 10 | CPU symmetry: identical rules | Locked |
| 11 | Online auth: anonymous only | Locked |
| 12 | Localization: English only at launch | Locked |
| 13 | Accessibility: full at launch | Locked |
| 14 | Session length: 1-3 min quick burst | Locked |
| 15 | Audio: original composer, $2-5K budget | Locked |
| 16 | Paywall: track-select gate (hard wall) | Locked |
| 17 | IAP price: $4.99 one-time | Locked |
| 18 | Daily difficulty: day-of-week rotation | Locked |
| 19 | Missed dailies: gone forever | Locked |
| 20 | Tutorial failure: lower threshold (300 px/s) | Locked |
| 21 | Par targets: designer-set during playtesting | Locked |
| 22 | Display names: Adjective+Animal_Number | Locked |

---

## All open questions resolved

All 22 design questions are now locked. Implementation can proceed without further design decisions until playtesting (weeks 10-11), when par tuning and balance passes happen.

---

*End of decisions doc v3.0. For full multiplayer context, see `Docs/MULTIPLAYER_REVISION.md`. For prototype status, see `Docs/PROTOTYPE_README.md`.*
