# Online Multiplayer Protocol

**Document version**: 1.0 (v1.1 — schema ready, client impl TBD)
**Date**: 2026-10-18
**Related**: `supabase/schema.sql` (match_sessions + match_events tables), `Docs/MULTIPLAYER_REVISION.md` (turn-based spec)

---

## TL;DR

Real-time turn-based multiplayer over **Supabase Realtime** (Postgres Changes). No custom WebSocket server — the client subscribes to inserts on a `match_events` table. When either player inserts a row (via RPC), both clients get a notification with the event payload.

---

## Architecture

```
Unity Client A                   Supabase                       Unity Client B
─────────────                    ─────────                       ─────────────
                                 Postgres
OnlineTurnClient ─── wss ───►   Realtime (PCR)  ──── wss ────►  OnlineTurnClient
   │                             │
   ├─ find_or_create_match()     ├─ match_sessions table
   ├─ submit_match_event()       ├─ match_events table (append-only)
   ├─ Subscribe(matchId)         ├─ broadcasts INSERT events to subscribers
   └─ OnOpponentEvent callback   └─ validates turn order server-side (security definer)
```

**Why not a custom WS server?**
- No server code to write or deploy
- Uses the same Supabase auth + RLS we already have
- Match state lives in Postgres (recoverable on reconnect)
- Latency is acceptable for turn-based (no sub-100ms sync needed)

---

## Tables (defined in `supabase/schema.sql`)

### `match_sessions`
One row per active online match. Created by `find_or_create_match` RPC.

| Column | Type | Notes |
|--------|------|-------|
| `id` | uuid (PK) | Match ID |
| `track_id` | text | Which track |
| `player_a` | uuid | Player A (creator) |
| `player_b` | uuid | Player B (joiner) — null during matchmaking |
| `current_turn_player` | uuid | Whose turn it is (alternates) |
| `status` | text | `'matchmaking'` → `'active'` → `'finished'` / `'conceded'` / `'abandoned'` |
| `winner` | uuid | Set when race ends |
| `player_a_swipe_count` | int | Running total |
| `player_b_swipe_count` | int | Running total |
| `player_a_finish_time_ms` | bigint | Set when A finishes |
| `player_b_finish_time_ms` | bigint | Set when B finishes |
| `created_at` / `last_activity_at` / `finished_at` | timestamptz | Timestamps |

### `match_events`
Append-only log of every event. Each row = one swipe, turn_end, etc. Clients subscribe to inserts.

| Column | Type | Notes |
|--------|------|-------|
| `id` | bigint (PK, auto-increment) | Event ID |
| `match_id` | uuid (FK → match_sessions) | Which match |
| `player_id` | uuid (FK → players) | Who sent it |
| `event_type` | text | `'swipe'` / `'turn_end'` / `'turn_skip'` / `'race_end'` / `'concede'` |
| `swipe_data` | jsonb | `{dx, dy, mag, t}` — null for non-swipe events |
| `final_position` | jsonb | `{x, y, rot, time_ms}` — for turn_end + race_end |
| `sequence_num` | int | 0-based, increments per match |
| `created_at` | timestamptz | When |

---

## RPCs

### `find_or_create_match(p_track_id text) → (match_id, player_role, opponent_id)`

Called by the client when the player taps "Online" → picks a track.

**Behavior**:
- If there's an open match (`status='matchmaking'`, `player_b` is null) for the same track created by someone else, join it as player B → status becomes `'active'`.
- Otherwise, create a new match as player A → status stays `'matchmaking'` until someone joins.

**Returns**:
```json
{ "match_id": "uuid", "player_role": "A" | "B", "opponent_id": "uuid" | null }
```

**RLS**: `security definer` — runs as the server, not the player. Uses `FOR UPDATE SKIP LOCKED` to prevent race conditions when two players join the same match simultaneously.

### `submit_match_event(p_match_id, p_event_type, p_swipe_data, p_final_pos) → boolean`

Called by the active player on each event.

**Server-side validation**:
1. Match must exist + be `'active'`
2. `current_turn_player` must match `auth.uid()` — **clients can't spoof opponent turns**
3. Computes `sequence_num` (max + 1)
4. Inserts the event row
5. Updates match state based on event type:
   - `swipe` → increments player's swipe count (turn does NOT flip)
   - `turn_end` / `turn_skip` → flips `current_turn_player` to the other player
   - `race_end` → records finish time; if both finished, sets `winner` + `status='finished'`
   - `concede` → opponent wins by default, `status='conceded'`

**Returns**: `true` on success, `false` if validation failed (not your turn, etc.)

---

## Client Flow

### 1. Matchmaking
```
Client taps Online → picks track
  → calls find_or_create_match(trackId)
  → gets back { matchId, role: "A"|"B", opponentId }
  → if role == "A": shows "Waiting for opponent..." (spinner)
  → if role == "B": opponentId is set, jumps to race
  → subscribes to match_events where match_id = matchId
```

### 2. Real-time subscription (Supabase Realtime)
```
Client subscribes to match_events inserts filtered by match_id:
  wss://{project}.supabase.co/realtime/v1/websocket
  → channel: "public:match_events"
  → filter: "match_id=eq.{matchId}"
  → event: "INSERT"

On INSERT received:
  → if event.player_id == my_id: ignore (I sent it)
  → else: dispatch to OnOpponentSwipeReceived / OnOpponentTurnEnd / etc.
```

### 3. My turn — sending a swipe
```
Player swipes → SwipeDetector fires → OnlineTurnClient.SendSwipeAsync(swipe)
  → calls submit_match_event(matchId, "swipe", {dx, dy, mag, t}, null)
  → RPC validates turn order + inserts row + increments swipe count
  → my local kart gets the impulse (already happened locally — optimistic)
  → Realtime broadcasts the INSERT to opponent
  → opponent's OnOpponentSwipeReceived fires → their kart gets the same impulse
```

### 4. My turn ends
```
My kart stops → RaceSceneController detects stop
  → OnlineTurnClient.EndMyTurnAsync(finalPos)
  → calls submit_match_event(matchId, "turn_end", null, {x, y, rot})
  → RPC flips current_turn_player to opponent
  → my local UI shows "Opponent's turn..."
  → opponent receives the INSERT → their turn starts
```

### 5. Race end
```
I cross the finish line → CheckpointTracker.OnRaceComplete
  → OnlineTurnClient.SendRaceEndAsync(finalPos, finishTimeMs)
  → calls submit_match_event(matchId, "race_end", null, {x, y, rot, time_ms})
  → RPC records my finish time; if opponent also finished, sets winner
  → my UI shows "Victory!" or "Defeat..." based on whether I have fewer swipes
```

### 6. Concede / disconnect
```
I tap Concede (or close the app)
  → OnlineTurnClient.ConcedeAsync()
  → calls submit_match_event(matchId, "concede", null, null)
  → RPC sets status='conceded', winner = opponent
  → opponent receives the INSERT → their UI shows "Opponent conceded — you win!"
```

---

## Event JSON shapes

### Swipe event
```json
{
  "id": 42,
  "match_id": "abc-123",
  "player_id": "uuid-here",
  "event_type": "swipe",
  "swipe_data": { "dx": 0.7, "dy": 0.3, "mag": 850.5, "t": 12345 },
  "final_position": null,
  "sequence_num": 7,
  "created_at": "2026-10-18T12:34:56.789Z"
}
```

### Turn end event
```json
{
  "event_type": "turn_end",
  "swipe_data": null,
  "final_position": { "x": 12.5, "y": -8.3, "rot": 45.0, "time_ms": 42000 }
}
```

### Race end event
```json
{
  "event_type": "race_end",
  "swipe_data": null,
  "final_position": { "x": 0.0, "y": 10.0, "rot": 0.0, "time_ms": 38500 }
}
```

### Concede event
```json
{
  "event_type": "concede",
  "swipe_data": null,
  "final_position": null
}
```

---

## Turn timer (30s, online only)

If 30s elapse without a swipe from the active player:
1. Active player's client auto-submits `turn_skip` event
2. RPC flips turn to opponent
3. Opponent's turn starts

If the active player is disconnected (app closed, network drop):
- Their client can't submit `turn_skip` (no connection)
- Opponent's client waits 30s + 30s grace period
- After grace, opponent can call `abandon_match` RPC (TODO) to win by default

---

## Reconnect logic

If a client drops mid-match:
1. On reconnect, call `get_match_state(matchId)` (TODO RPC) to fetch current state
2. Replay missed events from `match_events` where `sequence_num > lastSeenSeq`
3. Resubscribe to Realtime channel
4. Resume normal play

The match state lives in Postgres, so it's recoverable across app restarts.

---

## ELO updates (per Decision 5)

After a match ends:
- Winner: +25 ELO
- Loser: -15 ELO
- Draw (same swipes, same time): +0 both

Stored on `players.elo` (TODO column). Updated by a `update_elo_after_match` RPC (TODO).

---

## What's ready now vs. what's needed for v1.1

### ✅ Ready (this commit)
- `match_sessions` table + schema
- `match_events` table + schema
- `find_or_create_match` RPC (matchmaking with race-safe `SKIP LOCKED`)
- `submit_match_event` RPC (turn validation + state updates)
- RLS policies (self-read only, mutations via RPCs)
- This protocol doc

### ❌ Still needed for v1.1 build
- `RealtimeChannel` wrapper in `SupabaseRpcClient` (subscribes to Postgres Changes via WebSocket)
- Replace `OnlineTurnClient` stub with real impl using the RPCs + Realtime channel
- Reconnect logic (drop detection + replay missed events)
- `abandon_match` RPC (opponent left → win by default after grace period)
- `get_match_state` RPC (reconnect recovery)
- `update_elo_after_match` RPC + `players.elo` column
- Matchmaking UI (searching spinner, opponent found modal)
- Latency compensation (interpolate opponent kart position between events)
- Enable Realtime publication on `match_events` table via Supabase Dashboard

---

## Enabling Realtime (one-time, via Dashboard)

After running `schema.sql` + `policies.sql`:

1. Open Supabase Dashboard → Database → Replication
2. Find the `supabase_realtime` publication
3. Add the `match_events` table to it
4. Verify: insert a test row → subscribed client should receive it instantly

Alternatively, run as the postgres superuser:
```sql
alter publication supabase_realtime add table public.match_events;
```

---

*End of ONLINE_PROTOCOL.md*
