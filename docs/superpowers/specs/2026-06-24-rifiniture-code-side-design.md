# Spec — Rifiniture code-side (a11y, toast errori, System.Private.Xml, filtro note)

> Stato: **design approvato** (2026-06-24). Quattro voci code-side del backlog, in un solo /loop.
> Brainstorming → questa spec → piano (`writing-plans`) → implementazione. Ordine: 1 → 2 → 4 → 3.

## 1. Obiettivo e confini

Chiudere i punti del DA-FARE realizzabili **puramente lato codice** (niente migrazioni DB, infra, decisioni di prodotto):

1. **a11y `DbErrorBanner`** (§6) — chiusura accessibile da tastiera.
2. **Toast per gli errori di validazione** (§3) — coerenza col feedback di successo.
3. **Indagine `System.Private.Xml`** (§2) — capire chi lo tira; eliminarlo se sicuro, altrimenti documentare.
4. **Filtro note server-side** (§5) — spostare il predicato di visibilità nella query.

**Fuori scope:** view nickname-only (richiede vista DB) e vincoli SQL (§1, migrazioni); modifiche al `ToastHost`
(durata/chiudibilità) — quindi gli errori di sistema/operazione **restano nel banner**.

**Criterio di successo:** dopo ogni punto, build 0/0 + 122 test verdi (più eventuali nuovi); verifica locale.

## 2. Punto 1 — a11y `DbErrorBanner` (`Shared/DbErrorBanner.razor`)

Oggi la chiusura è un click sul testo (`<span class="db-error-text" @onclick="Dismiss">`), non raggiungibile
da tastiera. **Modifica:** affiancare al messaggio un vero pulsante di chiusura:
```razor
<button type="button" class="db-error-dismiss" aria-label="Chiudi" @onclick="Dismiss">✕</button>
```
Il testo resta visibile ma non più l'unico modo di chiudere (il click sul testo può restare o essere rimosso;
si mantiene per non cambiare l'abitudine). Aggiungere uno stile minimale per `.db-error-dismiss` nel CSS isolato
del componente (coerente con `.pwa-update-dismiss`). **Contrasti:** verifica a vista in locale (nessuna modifica
ai token salvo si noti un problema).

## 3. Punto 2 — toast per la validazione (8 file `Pages/*.razor`)

**Regola:** i messaggi di **validazione input** (impostati prima dell'operazione, l'utente corregge e riprova)
→ `Toasts.ShowError(...)` + `return;` (niente banner). Gli errori di **sistema/operazione** (catch `Errore …`,
"Il server non ha restituito…", precondizioni di stato) → **restano** `errorMessage` (banner).

Siti da convertire (validazione → toast):
- `Spells.razor`: "Il nome è obbligatorio", "Il livello deve essere tra 0 e 9".
- `Monsters.razor`: `errorMessage = validationError` (risultato `FormValidation.ValidateMonster`).
- `Races.razor`: `errorMessage = validationError` (risultato `FormValidation.ValidateRace`).
- `Classes.razor`: "Il nome è obbligatorio".
- `Characters.razor`: "Il nome è obbligatorio", "Gli HP massimi devono essere almeno 1".
- `Notes.razor`: "Il titolo è obbligatorio".
- `Combat.razor`: "Inserisci il nome del combattente".
- `Home.razor`: "Inserisci un nome per la campagna", "Inserisci un codice invito", "Codice invito non valido".

**Restano banner** (esempi): "Il server non ha restituito…", "Nessuna campagna attiva.", "Nessun personaggio
nella campagna da importare.", tutti i catch `Errore …`.

**Dipendenza:** dove un file non inietta già `ToastService Toasts`, aggiungere `@inject ToastService Toasts`.

## 4. Punto 4 — filtro note server-side (`Services/Repositories/NoteRepository.cs`)

In `GetNotesForCampaignAsync`, aggiungere il predicato di visibilità alla query Postgrest:
```csharp
var response = await client.From<Note>()
    .Where(n => n.CampaignId == campaignId && (n.IsShared || n.OwnerId == userId))
    .Get();
return FilterAndSortVisible(response.Models, userId);
```
`FilterAndSortVisible` resta invariato (difesa in profondità + ordinamento `UpdatedAt ?? CreatedAt` che PostgREST
non esprime). I test esistenti su `FilterAndSortVisible` restano validi.

**Rischio:** la traduzione del `||` da parte di postgrest-csharp va verificata a runtime — la pagina Note deve
caricare e mostrare condivise + proprie private. L'RLS resta la rete di sicurezza. Se la query `||` non funziona,
**fallback:** ripristinare il solo `.Where(CampaignId == id)` (stato attuale) e lasciare il filtro al solo helper.

## 5. Punto 3 — indagine `System.Private.Xml` (`DndCompanion.csproj` / nessuna)

Nessun uso diretto di `System.Xml`/`System.Data` nel codice → è transitivo. **Indagine:**
1. Pubblicare Release e, dal grafo (es. `*.deps.json` / report del trimmer), risalire a chi referenzia
   `System.Private.Xml` (sospetti: `System.Data.Common`, `Newtonsoft.Json`).
2. Se esiste un knob sicuro (es. feature switch del trimmer / rimozione di un riferimento inutile) che lo elimina
   senza rompere la (de)serializzazione → applicarlo e ri-misurare il bundle (smoke test runtime).
3. **Altrimenti documentare l'esito** in DA-FARE §2 (perché resta) e chiudere — è un'indagine, non un impegno a
   rimuoverlo.

**Vincolo:** non rompere il publish (build 0/0) né la (de)serializzazione Newtonsoft dei Model.

## 6. Verifica locale

- **#1:** il banner d'errore si chiude col Tab+Invio sul pulsante ✕; contrasti ok a vista.
- **#2:** un errore di validazione (es. salva PG senza nome) → appare come toast; un errore di sistema → resta nel banner.
- **#4:** la pagina Note carica; si vedono le note condivise + le proprie private, non quelle private altrui.
- **#3:** publish Release exit 0; se rimosso, app ancora funzionante (login/CRUD); altrimenti nota in DA-FARE.

## 7. Test

- `#2`/`#1`/`#4`: nessun nuovo unit test indispensabile (markup/routing/query). `FilterAndSortVisible` già coperto.
- Build 0/0 + i 122 test esistenti verdi dopo ogni punto.
