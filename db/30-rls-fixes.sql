-- =====================================================================
-- Task 5 — LOCKDOWN: chiusura dei due gap RLS (Fase 1)
-- Eseguire sul dashboard Supabase di PRODUZIONE, TUTTO IN BLOCCO (transazione).
-- ⚠️ Applicare SOLO DOPO che il deploy del client nuovo (RPC join_campaign) è LIVE,
--    altrimenti il vecchio client online romperebbe il join dei player.
-- Rollback d'emergenza: db/99-rollback.sql
-- =====================================================================
begin;

-- Gap A: combat_state (sostituisce la policy "combat_state access" = ALL true/true).
-- Lettura ai membri, scrittura solo al master (il client già non scrive mai come player).
drop policy if exists "combat_state access" on combat_state;
create policy combat_state_select on combat_state for select using ( is_campaign_member(campaign_id) );
create policy combat_state_insert on combat_state for insert with check ( is_campaign_master(campaign_id) );
create policy combat_state_update on combat_state for update using ( is_campaign_master(campaign_id) )
                                                            with check ( is_campaign_master(campaign_id) );

-- Gap B: campaign_members_insert. Dal client si può inserire solo se stessi come master
-- E solo se si è owner della campagna (creazione). Tutti gli altri join passano dalla RPC
-- join_campaign (SECURITY DEFINER), che bypassa questa policy. Chiude auto-promozione e auto-join.
drop policy if exists campaign_members_insert on campaign_members;
create policy campaign_members_insert on campaign_members for insert
  with check ( user_id = auth.uid() and role = 'master'
               and exists (select 1 from campaigns c where c.id = campaign_id and c.owner_id = auth.uid()) );

commit;
