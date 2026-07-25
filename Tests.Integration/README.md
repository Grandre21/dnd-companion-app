# Test d'integrazione RLS

Verificano le **Row-Level Security** di Supabase contro un database **vero** (locale), perché le RLS
sono applicate da PostgreSQL e non sono testabili con i mock/unit test. Mirano la sicurezza:
un utente non deve leggere/scrivere dati altrui.

## Prerequisiti (una tantum)
- **Docker Desktop** in esecuzione.
- **Supabase CLI** installata (`scoop install supabase`).
- Stack locale avviato dal repo: `supabase start` (la prima volta scarica le immagini).
  Lo stack applica `supabase/migrations/*_remote_schema.sql` → schema + policy identici a produzione.

## Come si eseguono
```powershell
supabase start                 # se non già attivo
dotnet test Tests.Integration/
```

## Comportamento
- Il fixture (`LocalSupabaseFixture`) **rileva** se lo stack è raggiungibile su `http://127.0.0.1:54321`.
  - Se **non** lo è, tutti i test si **auto-saltano** (Skip) → non rompono CI o altre macchine.
  - Se lo è: crea 2 utenti di test, semina dati idempotenti (via `service_role`, che bypassa le RLS) e
    esegue gli scenari come utente A / utente B.
- Le chiavi nel fixture sono quelle **fisse di Supabase locale** (uguali per chiunque, non segrete).

## Scenari coperti (`RlsIntegrationTests`)
- Un player **non** legge la nota **privata** del proprietario nella stessa campagna.
- Un player **legge** la nota **condivisa** del proprietario nella stessa campagna.
- Un **non-membro** non legge nemmeno le note condivise di un'altra campagna.
- Il **proprietario** legge le proprie note private.
- Un player **non** può scrivere `combat_state` (solo il master).
- Un player **non** può auto-promuoversi a `master` in `campaign_members`.

## Scenari coperti (`BackgroundsRlsIntegrationTests`)
- Un membro **legge** i background della propria campagna.
- Un **non-membro** non vede i background di un'altra campagna.
- Un player **non** modifica il background creato da un altro membro (ramo `USING`).
- Il **master** modifica il background di chiunque nella sua campagna.
- Un master **non** può spostare fuori dalla propria autorità un background che non ha creato lui
  (ramo `WITH CHECK`, usando una terza campagna `C3` di cui non è membro): la verifica rilegge dove
  si trova davvero la riga dopo il tentativo, invece di fidarsi dello status HTTP della PATCH.
  **Nota**: lo scenario "un autore sposta una propria riga in un'altra campagna" — quello descritto
  nel brief del task — **non** è invece bloccato da questa policy (`added_by = auth.uid()` resta
  vero indipendentemente dalla destinazione): è una lacuna ereditata da `races_update` e dalle
  policy analoghe, non introdotta né corretta da questa migrazione. Vedi il commento nel test per il
  dettaglio.

## Aggiornare lo schema locale
Se cambiano le policy/lo schema in produzione, rigenera la migration:
```powershell
supabase db dump -f db/remote-schema.sql
# poi sposta/rinomina in supabase/migrations/<timestamp>_remote_schema.sql e:
supabase db reset
```
