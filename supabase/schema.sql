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
begin
    insert into public.players (id, display_name)
    values (
        new.id,
        -- Generate AdjectiveAnimal_Number — same algorithm as C# PlayerProfile.
        -- Example: 'SwiftFox_42'. Pick from arrays at random.
        (
            (array['Swift','Brave','Calm','Bold','Quick','Bright','Eager','Fierce',
                   'Gentle','Happy','Jolly','Keen','Lively','Mighty','Noble','Proud',
                   'Rapid','Silent','Steady','Valiant','Wise','Zesty','Clever','Daring'])[1 + floor(random() * 24)::int]
            ||
            (array['Fox','Tiger','Panda','Eagle','Wolf','Bear','Hawk','Lion',
                   'Otter','Falcon','Lynx','Puma','Seal','Owl','Raven','Stag',
                   'Viper','Cobra','Jaguar','Panther','Heron','Kraken','Mustang','Phantom'])[1 + floor(random() * 24)::int]
            ||
            '_' || lpad((10 + floor(random() * 90))::text, 2, '0')
        )
    )
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
--                       completed_at, is_requester }
-- ----------------------------------------------------------------------------
create or replace function public.get_leaderboard(p_track_id text, p_limit int default 10)
returns table (
    rank             int,
    player_id        uuid,
    display_name     text,
    swipe_count      int,
    finish_time_ms   bigint,
    completed_at    timestamptz,
    is_requester    boolean
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
            as is_requester
    from leaderboard l
    order by l.rank
    limit p_limit;
$$;

-- ----------------------------------------------------------------------------
-- Player rank RPC: returns just the requesting player's rank on a track.
-- Faster than calling get_leaderboard with a huge limit when the player is
-- ranked far below #10.
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
    with best_runs as (
        select
            rr.player_id,
            min(rr.swipe_count) as best_swipes,
            min(rr.finish_time_ms) filter (where rr.swipe_count = min(rr.swipe_count) over (partition by rr.player_id)) as best_time
        from public.race_results rr
        where rr.track_id = p_track_id
          and rr.is_dnf = false
        group by rr.player_id
    ),
    ranked as (
        select
            br.player_id,
            br.best_swipes as swipe_count,
            br.best_time   as finish_time_ms,
            row_number() over (
                order by br.best_swipes asc, br.best_time asc
            ) as rank
        from best_runs br
    )
    select r.rank, r.swipe_count, r.finish_time_ms
    from ranked r
    where r.player_id = coalesce(auth.uid(), '00000000-0000-0000-0000-000000000000'::uuid);
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
-- End of schema.sql
-- ============================================================================
