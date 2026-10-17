-- ============================================================================
-- QuickChecks — Supabase Row-Level Security policies (v1.0)
-- ----------------------------------------------------------------------------
-- Run this AFTER supabase/schema.sql in Supabase Dashboard → SQL Editor.
-- It enables RLS on every table and defines who can read/write what.
--
-- Design principles:
--   * Leaderboard (race_results, daily_results) is public-read — anyone can see
--     anyone's runs (that's the point of a leaderboard).
--   * Inserts: a player can only insert rows where player_id matches their own
--     auth.uid(). This stops spoofed ghost uploads.
--   * Updates/deletes: blocked at the client level. Only the player can update
--     their own players row (display_name, email, is_premium). Race results
--     are immutable once inserted (delete allowed only for the owner, in case
--     a future "wipe my data" flow needs it).
--   * Ghosts bucket: any authenticated player can read + write to their own
--     folder (/player_id/*.json). Reading other players' ghosts is allowed
--     (so leaderboard viewers can replay top runs) but writes are restricted.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- players
-- ----------------------------------------------------------------------------
alter table public.players enable row level security;

-- Anyone can read the player list (display_name is needed for leaderboards).
-- Email is private — projected out via a view in a future iteration if needed.
drop policy if exists "players: public read" on public.players;
create policy "players: public read"
    on public.players for select
    using (true);

-- A player can update only their own row.
drop policy if exists "players: self update" on public.players;
create policy "players: self update"
    on public.players for update
    using (auth.uid() = id)
    with check (auth.uid() = id);

-- The handle_new_player() trigger inserts the row after signup. It runs as
-- security definer, so it bypasses RLS. We do NOT need an insert policy here.

-- ----------------------------------------------------------------------------
-- race_results
-- ----------------------------------------------------------------------------
alter table public.race_results enable row level security;

-- Public read (leaderboard).
drop policy if exists "race_results: public read" on public.race_results;
create policy "race_results: public read"
    on public.race_results for select
    using (true);

-- Self insert: a player can only insert rows where player_id matches their own.
drop policy if exists "race_results: self insert" on public.race_results;
create policy "race_results: self insert"
    on public.race_results for insert
    with check (auth.uid() = player_id);

-- Self delete (for "wipe my data" flows — not used in v1 but available).
drop policy if exists "race_results: self delete" on public.race_results;
create policy "race_results: self delete"
    on public.race_results for delete
    using (auth.uid() = player_id);

-- Updates NOT allowed — race results are immutable. Players post a new row
-- for each completed race.

-- ----------------------------------------------------------------------------
-- daily_results — same policies as race_results
-- ----------------------------------------------------------------------------
alter table public.daily_results enable row level security;

drop policy if exists "daily_results: public read" on public.daily_results;
create policy "daily_results: public read"
    on public.daily_results for select
    using (true);

drop policy if exists "daily_results: self insert" on public.daily_results;
create policy "daily_results: self insert"
    on public.daily_results for insert
    with check (auth.uid() = player_id);

drop policy if exists "daily_results: self delete" on public.daily_results;
create policy "daily_results: self delete"
    on public.daily_results for delete
    using (auth.uid() = player_id);

-- ----------------------------------------------------------------------------
-- Storage bucket: 'ghosts' — JSON ghost replay files
-- ----------------------------------------------------------------------------
-- Path convention: ghosts/{player_id}/{track_id}_{timestamp}.json
-- Any authenticated player can READ any ghost (so leaderboard viewers can replay
-- top runs). Writes are restricted to the owner's own folder.

drop policy if exists "ghosts: authenticated read" on storage.objects;
create policy "ghosts: authenticated read"
    on storage.objects for select
    using (bucket_id = 'ghosts' and auth.role() = 'authenticated');

drop policy if exists "ghosts: owner write" on storage.objects;
create policy "ghosts: owner write"
    on storage.objects for insert
    with check (
        bucket_id = 'ghosts'
        and auth.role() = 'authenticated'
        and (storage.foldername(name))[1] = auth.uid()::text
    );

drop policy if exists "ghosts: owner update" on storage.objects;
create policy "ghosts: owner update"
    on storage.objects for update
    using (
        bucket_id = 'ghosts'
        and auth.role() = 'authenticated'
        and (storage.foldername(name))[1] = auth.uid()::text
    );

drop policy if exists "ghosts: owner delete" on storage.objects;
create policy "ghosts: owner delete"
    on storage.objects for delete
    using (
        bucket_id = 'ghosts'
        and auth.role() = 'authenticated'
        and (storage.foldername(name))[1] = auth.uid()::text
    );

-- ============================================================================
-- End of policies.sql
-- ============================================================================
