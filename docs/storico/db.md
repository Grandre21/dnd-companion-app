# Storico delle modifiche al database

> **Questo file non si legge per sapere se una modifica è applicata.** Per quello si esegue
> `supabase/verifica-schema.sh`, che interroga il database vero. Questo è solo il registro di *cosa*
> abbiamo cambiato e *quando* — si consulta di rado, e per questo è una tabella e non un racconto.
>
> Il *perché* delle scelte sta in [DIARIO.md](../DIARIO.md). Lo schema eseguibile sta in
> `supabase/migrations/`, che serve a `supabase db reset` per ricostruire lo stack di test locale:
> **non è un elenco di cose da lanciare a mano**.

| Applicata | File | Cosa cambia |
|---|---|---|
| 2026-06-24 | `20260624225146_remote_schema.sql` | Baseline: dump dello schema di produzione — 12 tabelle, 45 policy, 5 funzioni. **Non idempotente**: non rieseguire su un database popolato. |
| 2026-07-26 | `20260726000000_catalog_packages.sql` | `source_id` su races/classes/spells/monsters · `races.speed_unit` · `characters.background_ability_choice` · nuova tabella `backgrounds` con le sue RLS. |
| 2026-08-01 | `20260731000000_party_visibility.sql` | Policy `characters_select` riscritta · RPC `get_party_overview` (stat sintetiche del gruppo senza esporre le schede). |
| 2026-08-01 | `20260801000000_class_subclasses.sql` | `classes.subclasses`. |
| 2026-08-06 | `20260806120000_close_campaign_hopping.sql` | Policy `*_update` legate a `is_campaign_member(campaign_id)`: chiude il campaign hopping e la scrittura degli ex-membri. |
| **2026-08-08** ⚠️ | `20260806130000_scheda_carta.sql` | `characters`: `class_resources`, `armor_training`, `weapon_proficiencies`, `tool_proficiencies` · `inventory`: `is_finesse`, `is_ranged`, `is_not_proficient`. |

## ⚠️ L'incidente del 2026-08-08

L'ultima riga porta due date perché è **arrivata due giorni dopo il client che la richiedeva**.

`docs/DA-FARE.md` la dichiarava applicata il 2026-08-06. Non lo era. Poiché `postgrest-csharp`
serializza *ogni* colonna del model a ogni `Update`, le sette colonne mancanti hanno bloccato **tutte**
le scritture su `characters` e `inventory` — punti ferita compresi — per due giorni di produzione.
Il difetto si è visto solo perché l'utente ha giocato una sessione.

Le due regole nate da qui stanno in [CLAUDE.md](../../CLAUDE.md), sezione «Come si cambia il
database». In breve: le query si danno **in chat**, non in un file che qualcuno deve ricordarsi di
aprire; e lo stato di applicazione **non si dichiara mai**, si interroga.
