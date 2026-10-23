-- ============================================================================
-- QuickChecks — Supabase schema (v1.0)
-- ----------------------------------------------------------------------------
-- Run this FIRST in Supabase Dashboard → SQL Editor → New query → Run.
-- It creates the tables, indexes, and the leaderboard RPC.
-- Run supabase/policies.sql AFTER this one to enable Row-Level Security.
--
-- Design notes:
--   * Auth: anonymous auto-sign-in on first launch (Decision 11). Players can
--     later link an email in Settings to recover their progress across devices.
--   * Leaderboard scope: top-10 + player's own rank per track (your choice).
--   * Ghost uploads: auto-upload every new best (your choice).
--   * Player identity is a Supabase auth.uid() (UUID). The C# PlayerProfile.playerId
--     field stores the same UUID so client + server agree.
-- ============================================================================

-- Enable extensions ----------------------------------------------------------
create extension if not exists "pgcrypto";    -- for gen_random_uuid()

-- ----------------------------------------------------------------------------
-- players: mirrors the auth.users table + caches display_name locally
-- ----------------------------------------------------------------------------
create table if not exists public.players (
    id              uuid primary key references auth.users(id) on delete cascade,
    display_name    text not null,
    email           text,                              -- set when player links email
    is_premium      boolean not null default false,    -- mirrors IAP entitlement
    total_races     integer not null default 0,
    created_at      timestamptz not null default now(),
    updated_at      timestamptz not null default now()
);

-- Auto-create a players row whenever a new auth.users row is created.
-- Supabase's anonymous signup creates the auth.users row; this trigger
-- populates the players row + assigns a display name (player can rename later).
create or replace function public.handle_new_player()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
    adj text;
    animal text;
    num text;
begin
    -- Pick random adjective + animal + 2-digit number (e.g. 'SwiftFox_42').
    -- Same algorithm as the C# PlayerProfile.GenerateDisplayName.
    adj    := (array['Swift','Brave','Calm','Bold','Quick','Bright','Eager','Fierce',
                     'Gentle','Happy','Jolly','Keen','Lively','Mighty','Noble','Proud',
                     'Rapid','Silent','Steady','Valiant','Wise','Zesty','Clever','Daring'])
                     [1 + floor(random() * 24)::int];
    animal := (array['Fox','Tiger','Panda','Eagle','Wolf','Bear','Hawk','Lion',
                     'Otter','Falcon','Lynx','Puma','Seal','Owl','Raven','Stag',
                     'Viper','Cobra','Jaguar','Panther','Heron','Kraken','Mustang','Phantom'])
                     [1 + floor(random() * 24)::int];
    num    := lpad((10 + floor(random() * 90)::int)::text, 2, '0');

    insert into public.players (id, display_name)
    values (new.id, adj || animal || '_' || num)
    on conflict (id) do nothing;
    return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
    after insert on auth.users
    for each row execute function public.handle_new_player();

-- ----------------------------------------------------------------------------
-- race_results: one row per finished Solo race (only finished runs count for leaderboard)
-- ----------------------------------------------------------------------------
create table if not exists public.race_results (
    id              uuid primary key default gen_random_uuid(),
    player_id       uuid not null references public.players(id) on delete cascade,
    track_id        text not null,
    kart_id         text not null,
    swipe_count     integer not null,
    finish_time_ms  bigint not null,
    is_dnf          boolean not null default false,
    ghost_url       text,                              -- populated when ghost is uploaded
    completed_at    timestamptz not null default now()
);

-- Composite index for the most common query: "best swipes for this track by anyone".
create index if not exists idx_race_results_track_swipes
    on public.race_results (track_id, swipe_count, finish_time_ms);

-- Per-player index: "list this player's runs on track X".
create index if not exists idx_race_results_player_track
    on public.race_results (player_id, track_id, completed_at desc);

-- ----------------------------------------------------------------------------
-- daily_results: separate table for daily-track races (Decision 5 — per-day leaderboard)
-- ----------------------------------------------------------------------------
create table if not exists public.daily_results (
    id              uuid primary key default gen_random_uuid(),
    player_id       uuid not null references public.players(id) on delete cascade,
    track_date      date not null,                     -- UTC date of the daily track
    swipe_count     integer not null,
    finish_time_ms  bigint not null,
    is_dnf          boolean not null default false,
    completed_at    timestamptz not null default now()
);

-- Top-N per day + per-player per-day.
create index if not exists idx_daily_results_date_swipes
    on public.daily_results (track_date, swipe_count, finish_time_ms);
create index if not exists idx_daily_results_player_date
    on public.daily_results (player_id, track_date);

-- ----------------------------------------------------------------------------
-- Ghosts are stored in the 'ghosts' Storage bucket (created via dashboard or
-- the storage schema below). Each ghost is a JSON file with stop-positions.
-- We DON'T store ghosts in Postgres (they're 50KB-1MB blobs — Storage is right).
-- ----------------------------------------------------------------------------
insert into storage.buckets (id, name, public)
    values ('ghosts', 'ghosts', false)
    on conflict (id) do nothing;

-- ----------------------------------------------------------------------------
-- Leaderboard RPC: returns top N for a track, INCLUDING the requesting player's
-- rank (even if they're not in the top N — we want "Your rank: 47th").
--
-- Usage from client:
--   POST /rest/v1/rpc/get_leaderboard
--   Body: { "p_track_id": "track_01_first_lap", "p_limit": 10 }
--   Returns: array of { rank, player_id, display_name, swipe_count, finish_time_ms,
--                       completed_at, is_requester, ghost_url }
-- ----------------------------------------------------------------------------
create or replace function public.get_leaderboard(p_track_id text, p_limit int default 10)
returns table (
    rank             int,
    player_id        uuid,
    display_name     text,
    swipe_count      int,
    finish_time_ms   bigint,
    completed_at    timestamptz,
    is_requester    boolean,
    ghost_url       text
)
language sql
stable
security definer
set search_path = public
as $$
    with ranked as (
        select
            rr.player_id,
            rr.swipe_count,
            rr.finish_time_ms,
            rr.completed_at,
            rr.ghost_url,
            -- For each player, only their BEST run on this track counts (lowest swipes,
            -- tiebreaker fastest time).
            row_number() over (
                partition by rr.player_id
                order by rr.swipe_count asc, rr.finish_time_ms asc
            ) as personal_rank
        from public.race_results rr
        where rr.track_id = p_track_id
          and rr.is_dnf = false
    ),
    best_runs as (
        select * from ranked where personal_rank = 1
    ),
    leaderboard as (
        select
            p.id           as player_id,
            p.display_name as display_name,
            br.swipe_count,
            br.finish_time_ms,
            br.completed_at,
            br.ghost_url,
            row_number() over (
                order by br.swipe_count asc, br.finish_time_ms asc
            ) as rank
        from best_runs br
        join public.players p on p.id = br.player_id
    )
    select
        l.rank,
        l.player_id,
        l.display_name,
        l.swipe_count,
        l.finish_time_ms,
        l.completed_at,
        (l.player_id = coalesce(auth.uid(), '00000000-0000-0000-0000-000000000000'::uuid))
            as is_requester,
        l.ghost_url
    from leaderboard l
    order by l.rank
    limit p_limit;
$$;

-- ----------------------------------------------------------------------------
-- Player rank RPC: returns just the requesting player's rank on a track.
-- Faster than calling get_leaderboard with a huge limit when the player is
-- ranked far below #10.
--
-- Implementation note: we use the same CTE pattern as get_leaderboard
-- (row_number() over partition) because Postgres disallows window functions
-- inside a FILTER clause — that was the original approach and it failed with
-- ERROR 42P20 "window functions are not allowed in FILTER".
-- ----------------------------------------------------------------------------
create or replace function public.get_player_rank(p_track_id text)
returns table (
    rank            int,
    swipe_count     int,
    finish_time_ms  bigint
)
language sql
stable
security definer
set search_path = public
as $$
    with ranked as (
        select
            rr.player_id,
            rr.swipe_count,
            rr.finish_time_ms,
            -- For each player, only their BEST run on this track counts (lowest swipes,
            -- tiebreaker fastest time).
            row_number() over (
                partition by rr.player_id
                order by rr.swipe_count asc, rr.finish_time_ms asc
            ) as personal_rank
        from public.race_results rr
        where rr.track_id = p_track_id
          and rr.is_dnf = false
    ),
    best_runs as (
        select * from ranked where personal_rank = 1
    ),
    leaderboard as (
        select
            br.player_id,
            br.swipe_count,
            br.finish_time_ms,
            row_number() over (
                order by br.swipe_count asc, br.finish_time_ms asc
            ) as rank
        from best_runs br
    )
    select l.rank, l.swipe_count, l.finish_time_ms
    from leaderboard l
    where l.player_id = coalesce(auth.uid(), '00000000-0000-0000-0000-000000000000'::uuid);
$$;

-- ----------------------------------------------------------------------------
-- updated_at trigger for players
-- ----------------------------------------------------------------------------
create or replace function public.touch_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

drop trigger if exists on_players_updated on public.players;
create trigger on_players_updated
    before update on public.players
    for each row execute function public.touch_updated_at();

-- ============================================================================
-- Online Multiplayer (v1.1 — schema ready, client impl TBD)
-- ----------------------------------------------------------------------------
-- Real-time turn-based matches between two players over Supabase Realtime
-- (Postgres Changes). The client subscribes to match_events inserts; when a
-- row is inserted (by either player), both clients get a notification with
-- the event payload. This avoids building a separate WebSocket server.
--
-- Match lifecycle:
--   1. Player A calls find_or_create_match(track_id) → joins or creates a match
--   2. Player B calls find_or_create_match(track_id) → joins A's match
--   3. Both clients subscribe to match_events where match_id = X
--   4. On each turn, the active player INSERTs a 'swipe' event; opponent sees it
--   5. After kart stops, active player INSERTs a 'turn_end' event; RPC flips
--      current_turn_player server-side
--   6. First to finish INSERTs a 'race_end' event; opponent sees it + loses
--   7. Concede → INSERT 'concede' event + update match_sessions.status
-- ============================================================================

create table if not exists public.match_sessions (
    id                      uuid primary key default gen_random_uuid(),
    track_id                text not null,
    player_a                uuid not null references public.players(id) on delete cascade,
    player_b                uuid references public.players(id) on delete set null,  -- null during matchmaking
    current_turn_player     uuid not null,            -- alternates between A and B
    status                  text not null default 'matchmaking',  -- 'matchmaking' | 'active' | 'finished' | 'conceded' | 'abandoned'
    winner                  uuid,                    -- null until race ends
    player_a_swipe_count    integer not null default 0,
    player_b_swipe_count    integer not null default 0,
    player_a_finish_time_ms bigint,                  -- null until A finishes
    player_b_finish_time_ms bigint,
    created_at              timestamptz not null default now(),
    last_activity_at        timestamptz not null default now(),
    finished_at             timestamptz
);

create index if not exists idx_match_sessions_matchmaking
    on public.match_sessions (status, track_id)
    where status = 'matchmaking';
create index if not exists idx_match_sessions_player_a on public.match_sessions (player_a);
create index if not exists idx_match_sessions_player_b on public.match_sessions (player_b);

-- Append-only log of every event in a match. Each row = one swipe, turn_end,
-- turn_skip, race_end, or concede. Clients subscribe to inserts on this table
-- (filtered by match_id) to receive opponent events in real time.
create table if not exists public.match_events (
    id              bigint primary key generated always as identity,
    match_id        uuid not null references public.match_sessions(id) on delete cascade,
    player_id       uuid not null references public.players(id) on delete cascade,
    event_type      text not null,                  -- 'swipe' | 'turn_end' | 'turn_skip' | 'race_end' | 'concede'
    swipe_data      jsonb,                          -- {dx, dy, mag, t} — null for non-swipe events
    final_position  jsonb,                          -- {x, y, rot} — for turn_end events
    sequence_num    integer not null,               -- 0-based, increments per match
    created_at      timestamptz not null default now()
);

create index if not exists idx_match_events_match_seq
    on public.match_events (match_id, sequence_num);
create index if not exists idx_match_events_match_created
    on public.match_events (match_id, created_at);

-- ----------------------------------------------------------------------------
-- find_or_create_match RPC: idempotent matchmaking.
-- If there's an open match (status='matchmaking', player_b is null) for the
-- same track, join it as player B. Otherwise create a new match as player A.
-- Returns the match_id + which player you are ('A' or 'B').
-- ----------------------------------------------------------------------------
create or replace function public.find_or_create_match(p_track_id text)
returns table (
    match_id    uuid,
    player_role text,            -- 'A' or 'B'
    opponent_id uuid
)
language plpgsql
security definer
set search_path = public
as $$
declare
    open_match uuid;
    my_id      uuid := auth.uid();
begin
    if my_id is null then
        return;
    end if;

    -- Try to find an open match for this track (not created by us)
    select id into open_match
    from public.match_sessions
    where track_id = p_track_id
      and status = 'matchmaking'
      and player_b is null
      and player_a <> my_id
    order by created_at asc
    limit 1
    for update skip locked;  -- avoid race: two players joining the same match

    if open_match is not null then
        -- Join as player B
        update public.match_sessions
        set player_b = my_id,
            status = 'active',
            last_activity_at = now()
        where id = open_match and player_b is null;

        return query select open_match, 'B'::text, player_a from public.match_sessions where id = open_match;
    else
        -- Create a new match as player A
        insert into public.match_sessions (track_id, player_a, current_turn_player, status)
        values (p_track_id, my_id, my_id, 'matchmaking')
        returning id into open_match;

        return query select open_match, 'A'::text, null::uuid;
    end if;
end;
$$;

-- ----------------------------------------------------------------------------
-- submit_match_event RPC: validates turn order before inserting an event.
-- Returns false (no-op) if it's not the requesting player's turn.
-- This is the server-side authority — clients can't spoof opponent turns.
-- ----------------------------------------------------------------------------
create or replace function public.submit_match_event(
    p_match_id   uuid,
    p_event_type text,
    p_swipe_data jsonb default null,
    p_final_pos  jsonb default null
)
returns boolean
language plpgsql
security definer
set search_path = public
as $$
declare
    match_rec   record;
    my_id       uuid := auth.uid();
    next_seq    integer;
    next_player uuid;
begin
    if my_id is null then return false; end if;

    select * into match_rec from public.match_sessions where id = p_match_id for update;
    if not found then return false; end if;
    if match_rec.status <> 'active' then return false; end if;
    if match_rec.current_turn_player <> my_id then return false; end if;

    -- Compute next sequence number for this match
    select coalesce(max(sequence_num), -1) + 1 into next_seq
    from public.match_events where match_id = p_match_id;

    -- Insert the event
    insert into public.match_events (match_id, player_id, event_type, swipe_data, final_position, sequence_num)
    values (p_match_id, my_id, p_event_type, p_swipe_data, p_final_pos, next_seq);

    -- Update match state based on event type
    if p_event_type = 'swipe' then
        -- Increment the player's swipe count
        if match_rec.player_a = my_id then
            update public.match_sessions
            set player_a_swipe_count = player_a_swipe_count + 1, last_activity_at = now()
            where id = p_match_id;
        else
            update public.match_sessions
            set player_b_swipe_count = player_b_swipe_count + 1, last_activity_at = now()
            where id = p_match_id;
        end if;
        -- Turn does NOT flip on swipe — only on turn_end
    elsif p_event_type = 'turn_end' or p_event_type = 'turn_skip' then
        -- Flip the turn to the other player
        next_player := case when match_rec.player_a = my_id then match_rec.player_b else match_rec.player_a end;
        update public.match_sessions
        set current_turn_player = next_player, last_activity_at = now()
        where id = p_match_id;
    elsif p_event_type = 'race_end' then
        -- First to finish wins; record their finish time
        if match_rec.player_a = my_id and match_rec.player_a_finish_time_ms is null then
            update public.match_sessions
            set player_a_finish_time_ms = (p_final_pos->>'time_ms')::bigint,
                winner = case when match_rec.player_b_finish_time_ms is not null and
                                  (p_final_pos->>'time_ms')::bigint > match_rec.player_b_finish_time_ms
                              then player_b else player_a end,
                status = case when match_rec.player_b_finish_time_ms is not null then 'finished' else status end,
                last_activity_at = now()
            where id = p_match_id;
        elsif match_rec.player_b = my_id and match_rec.player_b_finish_time_ms is null then
            update public.match_sessions
            set player_b_finish_time_ms = (p_final_pos->>'time_ms')::bigint,
                winner = case when match_rec.player_a_finish_time_ms is not null and
                                  (p_final_pos->>'time_ms')::bigint > match_rec.player_a_finish_time_ms
                              then player_a else player_b end,
                status = case when match_rec.player_a_finish_time_ms is not null then 'finished' else status end,
                last_activity_at = now()
            where id = p_match_id;
        end if;
    elsif p_event_type = 'concede' then
        -- Opponent wins by default
        update public.match_sessions
        set status = 'conceded',
            winner = case when match_rec.player_a = my_id then match_rec.player_b else match_rec.player_a end,
            finished_at = now(),
            last_activity_at = now()
        where id = p_match_id;
    end if;

    return true;
end;
$$;

-- ============================================================================
-- End of schema.sql
-- ============================================================================
