-- =====================================================================
-- Task 3 — RPC join_campaign (ADDITIVO, retro-compatibile)
-- Eseguire sul dashboard Supabase di PRODUZIONE.
-- Valida il codice invito e garantisce la membership "player" server-side,
-- bypassando la RLS. Va creata PRIMA del deploy del nuovo client e PRIMA del
-- lockdown (db/30-rls-fixes.sql).
-- =====================================================================
create or replace function public.join_campaign(p_code text)
returns uuid language plpgsql security definer set search_path = public as $$
declare v_campaign uuid;
begin
  select id into v_campaign from campaigns where invite_code = upper(trim(p_code));
  if v_campaign is null then return null; end if;           -- codice non valido
  insert into campaign_members (campaign_id, user_id, role, joined_at)
  values (v_campaign, auth.uid(), 'player', now())
  on conflict (campaign_id, user_id) do nothing;            -- già membro → no-op
  return v_campaign;
end; $$;
