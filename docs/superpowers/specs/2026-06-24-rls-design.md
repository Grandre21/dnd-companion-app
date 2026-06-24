# Spec — Fase 1: Row-Level Security (RLS) su Supabase

> Stato: **design approvato** (2026-06-24), in attesa di review dello spec scritto prima di passare al piano.
> Contesto: vedi [DA-FARE.md §1](../../DA-FARE.md) (sicurezza) e [DIARIO.md](../../DIARIO.md).

## 1. Obiettivo e perché

Oggi l'identità è un JWT firmato (Gotrue), ma **manca l'autorità lato server sui dati**: il client
filtra da solo (es. `GetNotesForCampaignAsync` scarica tutte le note della campagna e poi tiene
`is_shared || owner_id == me`). Chiunque abbia la **anon key** (pubblica nel bundle) può leggere/scrivere
ogni tabella via REST bypassando la UI. Questo spec definisce le **policy RLS** che spostano la logica di
autorizzazione nel DB, replicando *esattamente* il comportamento attuale del client. È il **gate 🔴 bloccante**
per aprire l'app al pubblico.

**Fuori scope (progetti separati):** feature AI/generazione da testo (vedi DA-FARE §8; il suo gate vive nel
proxy, non tocca queste RLS), gate di registrazione a pagamento, header CSP. **Adiacente** (valutato allo
Step 0, eventualmente agganciato): FK + `ON DELETE CASCADE` (DA-FARE §1).

## 2. Modello dei permessi (decisioni confermate)

Ruoli per-campagna da `campaign_members.role` ∈ {`master`, `player`}. Principi:
- **Lettura** = appartenenza alla campagna.
- **Scrittura del proprio dato** = owner del dato; **override del master** dove l'app lo prevede.
- **Note**: private (`is_shared = false`) visibili **solo all'owner**, master incluso (decisione E).
- **Inventario e incantesimi del PG**: leggibili da **tutti i membri** della campagna (si sfoglia la scheda
  di un compagno), scrivibili da owner-del-PG o master (decisione D).
- **combat_state**: lettura membri, scrittura **solo master**.
- **Join campagna**: tramite RPC `SECURITY DEFINER` che valida il codice invito (decisione C), così la
  policy di INSERT su `campaign_members` può essere restrittiva e si chiude l'auto-iscrizione abusiva.

### Matrice

| Tabella | SELECT | INSERT | UPDATE | DELETE |
|---|---|---|---|---|
| `characters` | membro | owner=uid ∧ membro | owner **o** master | — (l'app non cancella PG singoli) |
| `notes` | owner **o** (`is_shared` ∧ membro) | owner=uid ∧ membro | owner | owner |
| `inventory` | membro (via PG) | owner-o-master (via PG) | owner-o-master | owner-o-master |
| `character_spells` | membro (via PG) | owner-o-master (via PG) | owner-o-master | owner-o-master |
| `spells`/`monsters`/`races`/`classes` | membro | `added_by`=uid ∧ membro | `added_by` **o** master | `added_by` **o** master |
| `combat_state` | membro | master | master | (non usato) |
| `campaigns` | owner **o** membro | owner=uid | owner | owner |
| `campaign_members` | membri stessa campagna | solo owner-come-master (resto via RPC) | — | self (leave) **o** master |
| `profiles` | qualunque autenticato | self (`id`=uid) | self | — |

> Note: righe di catalogo con `added_by` NULL (seed) restano modificabili **solo dal master**.
> `campaigns.SELECT` include `owner=uid` per non rompere il `RETURNING` dell'INSERT in `CreateCampaignAsync`
> (la membership viene inserita *dopo* la campagna; senza questo, l'insert non potrebbe rileggere la riga).

## 3. Architettura tecnica: helper `SECURITY DEFINER`

Le policy su `campaign_members` che interrogano `campaign_members` causano **ricorsione infinita** (inghippo
classico Supabase). Si centralizza la logica in funzioni `SECURITY DEFINER` (bypassano le RLS → niente
ricorsione, una sola fonte di verità):

```sql
create or replace function public.is_campaign_member(p_campaign uuid)
returns boolean language sql security definer stable set search_path = public as $$
  select exists (select 1 from campaign_members
                 where campaign_id = p_campaign and user_id = auth.uid());
$$;

create or replace function public.is_campaign_master(p_campaign uuid)
returns boolean language sql security definer stable set search_path = public as $$
  select exists (select 1 from campaign_members
                 where campaign_id = p_campaign and user_id = auth.uid() and role = 'master');
$$;

-- Per inventory/character_spells, che hanno solo character_id (niente campaign_id):
create or replace function public.can_read_character(p_char uuid)
returns boolean language sql security definer stable set search_path = public as $$
  select exists (select 1 from characters c
                 where c.id = p_char and public.is_campaign_member(c.campaign_id));
$$;

create or replace function public.can_edit_character(p_char uuid)
returns boolean language sql security definer stable set search_path = public as $$
  select exists (select 1 from characters c
                 where c.id = p_char
                   and (c.owner_id = auth.uid() or public.is_campaign_master(c.campaign_id)));
$$;
```

### Join via RPC (decisione C)

```sql
create or replace function public.join_campaign(p_code text)
returns uuid language plpgsql security definer set search_path = public as $$
declare v_campaign uuid;
begin
  select id into v_campaign from campaigns where invite_code = upper(trim(p_code));
  if v_campaign is null then return null; end if;          -- codice non valido
  insert into campaign_members (campaign_id, user_id, role, joined_at)
  values (v_campaign, auth.uid(), 'player', now())
  on conflict (campaign_id, user_id) do nothing;           -- richiede UNIQUE(campaign_id,user_id)
  return v_campaign;
end; $$;
```

Richiede un vincolo **`unique (campaign_id, user_id)`** su `campaign_members` (da verificare/aggiungere allo
Step 0; previene anche membership duplicate). Sostituisce l'attuale flusso `find_campaign_by_invite_code` +
INSERT manuale: vedi §5 per la modifica al C# e l'ordine di rilascio.

## 4. Policy rappresentative

Forma tipica (esempi; le altre tabelle seguono la matrice §2). L'SQL **definitivo** si finalizza dopo lo
Step 0 (potrebbero esserci policy già esistenti da `drop policy if exists` e coordinare).

```sql
-- characters
alter table characters enable row level security;
create policy characters_select on characters for select using ( is_campaign_member(campaign_id) );
create policy characters_insert on characters for insert
  with check ( owner_id = auth.uid() and is_campaign_member(campaign_id) );
create policy characters_update on characters for update
  using ( owner_id = auth.uid() or is_campaign_master(campaign_id) )
  with check ( owner_id = auth.uid() or is_campaign_master(campaign_id) );
-- niente policy DELETE → cancellazione singola negata (la cascata da campaigns bypassa le RLS)

-- notes (sostituisce il filtro client di GetNotesForCampaignAsync)
alter table notes enable row level security;
create policy notes_select on notes for select
  using ( owner_id = auth.uid() or (is_shared and is_campaign_member(campaign_id)) );
create policy notes_insert on notes for insert
  with check ( owner_id = auth.uid() and is_campaign_member(campaign_id) );
create policy notes_update on notes for update using ( owner_id = auth.uid() ) with check ( owner_id = auth.uid() );
create policy notes_delete on notes for delete using ( owner_id = auth.uid() );

-- inventory (stessa forma per character_spells)
alter table inventory enable row level security;
create policy inventory_select on inventory for select using ( can_read_character(character_id) );
create policy inventory_insert on inventory for insert with check ( can_edit_character(character_id) );
create policy inventory_update on inventory for update using ( can_edit_character(character_id) )
                                                       with check ( can_edit_character(character_id) );
create policy inventory_delete on inventory for delete using ( can_edit_character(character_id) );

-- combat_state (scrittura solo master; serve sia INSERT che UPDATE per l'Upsert)
alter table combat_state enable row level security;
create policy combat_select on combat_state for select using ( is_campaign_member(campaign_id) );
create policy combat_insert on combat_state for insert with check ( is_campaign_master(campaign_id) );
create policy combat_update on combat_state for update using ( is_campaign_master(campaign_id) )
                                                      with check ( is_campaign_master(campaign_id) );

-- campaign_members (INSERT diretto solo per owner-come-master; i player passano da join_campaign)
alter table campaign_members enable row level security;
create policy members_select on campaign_members for select using ( is_campaign_member(campaign_id) );
create policy members_insert_owner on campaign_members for insert
  with check ( user_id = auth.uid() and role = 'master'
               and exists (select 1 from campaigns c where c.id = campaign_id and c.owner_id = auth.uid()) );
create policy members_delete on campaign_members for delete
  using ( user_id = auth.uid() or is_campaign_master(campaign_id) );

-- profiles
alter table profiles enable row level security;
create policy profiles_select on profiles for select using ( auth.uid() is not null );
create policy profiles_insert on profiles for insert with check ( id = auth.uid() );
create policy profiles_update on profiles for update using ( id = auth.uid() ) with check ( id = auth.uid() );
```

## 5. Impatto sul codice C# e ordine di rilascio

L'unica modifica funzionale al client è il **join campagna**. `JoinCampaignAsync`
(`Services/SupabaseService.cs`) passa da `Rpc("find_campaign_by_invite_code")` + INSERT manuale a una sola
chiamata `Rpc<string>("join_campaign", { p_code })`, poi rilegge la campagna. Tutto il resto del client
**non cambia**: le query restano identiche, è il DB che ora le filtra.

⚠️ **Ordine di rilascio (critico):** se attiviamo la policy restrittiva su `campaign_members` *prima* di
deployare il nuovo client, l'app live (vecchio `JoinCampaignAsync` con INSERT diretto del player) si
**romperebbe** durante la finestra. Sequenza corretta:

1. **DB additivo:** creare le funzioni helper + `join_campaign` + il vincolo `unique` (tutto retro-compatibile,
   non rompe il client attuale, RLS ancora permissive).
2. **Deploy client:** nuovo `JoinCampaignAsync` basato su `join_campaign`. **Verifica in locale** loggati
   (`https://localhost:7076`): crea campagna, join con codice, ruoli corretti.
3. **Lockdown RLS:** applicare le policy in **un'unica transazione** (§6).

## 6. Applicazione su prod in sicurezza (modalità "a tavolino")

Si applica sul DB di produzione dal SQL editor. Misure obbligatorie:

- **Step 0 — Audit read-only (prima di scrivere l'SQL definitivo).** Eseguire lo script di audit (Appendice A)
  e incollare l'output: rivela quali tabelle hanno **già** RLS abilitata, le policy/funzioni esistenti (i
  commenti nel codice citano `campaigns_select`/`campaign_members_select` → probabilmente già presenti), i
  vincoli `unique`, e lo stato delle **FK/cascade** (per DA-FARE §1). Le policy finali si scrivono *sapendo*
  cosa c'è, con `drop policy if exists` dove serve.
- **Transazione unica** (`begin; … commit;`): prod non resta mai a metà guado.
- **Rollback pronto** (Appendice B): script che disattiva le RLS sulle tabelle toccate, da tenere a portata
  in caso di blocco.
- **Test con due account** su una campagna usa-e-getta (§7) prima di considerare chiuso.

## 7. Piano di test (due account, campagna usa-e-getta)

Account **M** (master, crea la campagna), account **P** (player, fa join col codice), più un **estraneo X**
(loggato ma non membro). Verificare sia ciò che **deve funzionare** sia ciò che **deve fallire**:

**Deve funzionare**
1. M crea campagna → diventa master; vede la campagna.
2. P fa join col codice → diventa player; vede la campagna, i PG condivisi, il combat.
3. P crea/modifica il **proprio** PG, inventario, incantesimi.
4. M modifica il PG di P (override master). M gestisce il `combat_state`; P lo **vede** (polling).
5. Note: P vede le condivise + le proprie private; M **non** vede le private di P.
6. Catalogo: P aggiunge una spell (`added_by`=P); la modifica P o M.

**Deve fallire (via REST diretto con la anon key, non solo UI)**
7. X **non** legge characters/notes/combat/cataloghi della campagna.
8. P **non** modifica il PG di un altro player (non-owner, non-master).
9. P **non** scrive `combat_state` (non master).
10. P **non** si auto-promuove a `master` né si auto-iscrive a una campagna a caso senza codice.
11. X **non** legge le note private altrui né le condivise di una campagna di cui non è membro.

> Il punto 7/11 si verifica con una `fetch`/curl autenticata come X verso l'endpoint REST, per provare che il
> blocco è nel DB e non solo nella UI.

## 8. Rischi e mitigazioni

- **Lock-out da policy errata** → transazione unica + rollback pronto + test prima di chiudere.
- **Ricorsione su `campaign_members`** → risolta dagli helper `SECURITY DEFINER` (§3).
- **`RETURNING` degli INSERT che fallisce** (insert + re-select sotto RLS) → coperto per `campaigns`
  (`owner` nella SELECT) e `campaign_members` (riga visibile in transazione). Da **verificare nei test** 1–3.
- **Finestra di rottura del join** → mitigata dall'ordine di rilascio (§5).
- **`search_path` nelle funzioni definer** → fissato a `public` per evitare hijack.

## Appendice A — Script di audit (read-only, Step 0)

```sql
-- RLS abilitata per tabella
select relname, relrowsecurity, relforcerowsecurity
from pg_class where relnamespace = 'public'::regnamespace and relkind = 'r' order by relname;
-- policy esistenti
select tablename, policyname, cmd, qual, with_check
from pg_policies where schemaname = 'public' order by tablename, policyname;
-- funzioni e flag SECURITY DEFINER
select proname, prosecdef from pg_proc where pronamespace = 'public'::regnamespace order by proname;
-- FK + tipo di cascade (confdeltype: c=cascade, a=no action, r=restrict, n=set null)
select conrelid::regclass as tbl, conname, confrelid::regclass as ref, confdeltype
from pg_constraint where contype = 'f' and connamespace = 'public'::regnamespace order by 1;
-- vincoli UNIQUE (serve per ON CONFLICT del join)
select conrelid::regclass as tbl, conname, pg_get_constraintdef(oid)
from pg_constraint where contype = 'u' and connamespace = 'public'::regnamespace order by 1;
```

## Appendice B — Rollback d'emergenza

```sql
-- Disattiva le RLS sulle tabelle toccate (riapre tutto: solo per sbloccare in emergenza).
alter table characters       disable row level security;
alter table notes            disable row level security;
alter table inventory        disable row level security;
alter table character_spells disable row level security;
alter table spells           disable row level security;
alter table monsters         disable row level security;
alter table races            disable row level security;
alter table classes          disable row level security;
alter table combat_state     disable row level security;
alter table campaigns        disable row level security;
alter table campaign_members disable row level security;
alter table profiles         disable row level security;
```
