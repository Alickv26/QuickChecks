# QuickChecks Supabase backend

This directory contains the SQL schema + Row-Level Security policies for the
QuickChecks backend (hosted on Supabase, free tier).

## Project details

- **URL**: https://wjeiddjeueysfsvjgzyx.supabase.co
- **Region**: eu-west-1 (Ireland)
- **Auth model**: Anonymous auto-sign-in on first launch + optional email link
- **Leaderboard scope**: top-10 + player's own rank per track
- **Ghost uploads**: auto-upload every new best

## Setup (one-time)

1. Open Supabase Dashboard → SQL Editor → New query.
2. Paste the contents of `schema.sql` → Run. Should complete in ~1s.
3. Open a new query tab, paste `policies.sql` → Run.
4. Verify: open Table Editor → you should see `players`, `race_results`, `daily_results`.
5. Verify Storage → Buckets → you should see `ghosts` (private).

## How the client talks to it

The C# `SupabaseClient` (in `Assets/_Project/Scripts/Multiplayer/`) talks to:

- `POST /auth/v1/signup` — anonymous signup, returns access_token
- `POST /auth/v1/token?grant_type=password` — link email later (optional)
- `GET  /rest/v1/rpc/get_leaderboard?p_track_id=track_01_first_lap&p_limit=10` — top-10 + your rank
- `POST /rest/v1/race_results` — insert a new race result
- `POST /storage/v1/object/ghosts/{player_id}/{track_id}_{timestamp}.json` — upload ghost

All requests carry the header `Authorization: Bearer sb_publishable_...`
(replaced with the access_token after anonymous sign-in) and `apikey: sb_publishable_...`.

## Schema overview

```
public.players
    id (uuid, FK -> auth.users)
    display_name (text)
    email (text, nullable)
    is_premium (bool)
    total_races (int)
    created_at, updated_at

public.race_results
    id (uuid)
    player_id (uuid, FK -> players)
    track_id (text)
    kart_id (text)
    swipe_count (int)
    finish_time_ms (bigint)
    is_dnf (bool)
    ghost_url (text, nullable)
    completed_at (timestamptz)

public.daily_results  -- same shape but track_date instead of track_id
    ...
    track_date (date)
```

## RPCs

- `get_leaderboard(p_track_id text, p_limit int default 10)` — returns top-N
  with each player's BEST run only (lowest swipes, tiebreaker fastest time).
  Includes `is_requester` flag so the client knows which row is "you".
- `get_player_rank(p_track_id text)` — returns the requesting player's rank
  + their best swipes/time on the track. Faster than fetching the full leaderboard
  when the player is ranked far below #10.

## Row-Level Security

- `players`: public read, self update (player can edit own display_name/email/is_premium).
- `race_results` / `daily_results`: public read, self insert + self delete (no updates — results are immutable).
- Storage bucket `ghosts`: authenticated read (any logged-in player can read any ghost), folder-scoped write.

## Verification queries (paste into SQL Editor after setup)

```sql
-- Count your players (should be 0 right after setup):
select count(*) from public.players;

-- See the most recent 5 race results across all tracks:
select * from public.race_results order by completed_at desc limit 5;

-- Test the leaderboard RPC for Track 1 (should return empty array initially):
select * from public.get_leaderboard('track_01_first_lap', 10);
```

## Deleting the schema (if you want a clean slate)

```sql
drop table if exists public.daily_results cascade;
drop table if exists public.race_results cascade;
drop table if exists public.players cascade;
drop function if exists public.get_leaderboard(text, int) cascade;
drop function if exists public.get_player_rank(text) cascade;
drop function if exists public.handle_new_player() cascade;
drop function if exists public.touch_updated_at() cascade;
delete from storage.buckets where id = 'ghosts';
```
