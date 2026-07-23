# Visibilità limitata del player nel tracker combattimento

**Data:** 2026-07-23
**Tipo:** feature (modifica di comportamento UI)
**Stato:** design approvato

## Problema

Nel tracker iniziativa (`Pages/Combat.razor`) la lista dei combattenti è **condivisa e
identica per tutti**: un player vede iniziativa, PF attuali/massimi e l'ordine di turno di
*ogni* entità (altri PG, mostri, PNG), oltre all'indicatore "Round N — Turno di *Nome*".

Un player deve invece vedere **solo le informazioni del proprio personaggio**. Delle altre
entità, al massimo il **nome** — nessuna statistica (PF, iniziativa) e **nessun ordine di
turno**.

## Obiettivo (vista del player)

- **La propria scheda**, con statistiche complete (PF attuali/max, iniziativa).
- Un **segnale evidente quando è il suo turno** ("È il tuo turno!"), così può giocare.
- Delle altre entità, **solo il nome** — niente PF, iniziativa, posizione, né stato "sconfitto".
- **Nessuna informazione sull'ordine di turno** quando non è il suo turno: in particolare
  il player **non** vede di chi è il turno corrente.

La vista del **Master resta invariata**.

## Vincoli e decisioni

- **Aggancio "riga mia":** i PG entrano nel tracker via il bottone *"Importa personaggi"*;
  in quel momento conosciamo il proprietario. Marchiamo ogni combattente importato con
  l'`owner_id` del PG. Il player riconosce le proprie righe da lì.
- **Livello di garanzia: cosmetico lato UI.** I dati grezzi (`combat_state`) continuano ad
  arrivare al browser del player via polling; la redazione avviene **solo al render**. Un
  player smaliziato con gli strumenti sviluppatore potrebbe leggere i dati grezzi. Accettabile
  per un gruppo di amici; **nessuna modifica a DB/RLS**.
- **Nessuna migrazione:** `CombatState.Combatants` è già una colonna `jsonb`; aggiungere un
  campo al POCO `Combatant` non richiede migrazioni.

## Approccio scelto (A: helper puro + branch nel `.razor`)

Coerente con i pattern del progetto (logica di dominio in helper puri `static` testabili in
`Services/`, sul modello di `CombatImport`/`AccessControl`; UI che vi si appoggia).

Alternative scartate: **(B)** componente `CombatPlayerView.razor` separato — più churn e
incappa nel gotcha dell'isolation CSS (gli stili scoped del genitore non raggiungono il figlio);
**(C)** redazione del modello al load — mescola le responsabilità, muta il modello di lavoro e
serve comunque l'`owner_id`.

## Design di dettaglio

### 1. Dato — `Models/Combatant.cs`

Nuovo campo:

```csharp
public string? OwnerId { get; set; }   // proprietario del PG se importato; null per mostri/aggiunte a mano
```

Valorizzato in `Combat.razor` → `ImportCharactersAsync` da `ch.OwnerId`. Righe senza owner
(mostri, aggiunte manuali, stati salvati prima di questa feature) → `null` → per il player
valgono come **"non mie"** (mostrate come semplice nome). Default sicuro.

### 2. Helper puro — `Services/CombatVisibility.cs`

Funzioni pure, nessuno stato/I/O:

- `bool IsOwn(Combatant c, string? userId)`
  → `!string.IsNullOrEmpty(c.OwnerId) && c.OwnerId == userId` (specchio di `AccessControl.CanEdit`).
- `bool IsCurrentTurnOwn(IReadOnlyList<Combatant> combatants, int currentTurnIndex, string? userId)`
  → true se il combattente di turno è del player; guardia sugli indici (fuori range → false;
  `userId` null/vuoto → false).
- `List<Combatant> OthersForPlayer(IReadOnlyList<Combatant> combatants, string? userId)`
  → i combattenti **non** del player, **ordinati per `Name`** (`StringComparer.OrdinalIgnoreCase`)
  così l'ordine mostrato **non svela l'iniziativa**.
- `List<Combatant> OwnForPlayer(IReadOnlyList<Combatant> combatants, string? userId)`
  → i combattenti del player, nell'ordine originale (per coerenza con la scheda).

### 3. UI — `Pages/Combat.razor`

Branch sulla lista dei combattenti: `@if (CurrentUser.IsMaster) { ...vista master invariata... }
else { ...vista player... }`.

**Indicatore in alto (player):**
- Sempre `Round N`.
- Se `IsCurrentTurnOwn` → banner evidente **"È il tuo turno!"**.
- Altrimenti → **niente "Turno di *Nome*"** (rivelerebbe l'attore/ordine). Solo il round.

**Vista player:**
- **"La tua scheda"** — `OwnForPlayer`: righe con statistiche complete (PF, iniziativa),
  evidenziate quando è il suo turno. Nessun controllo di modifica (già così per i player).
- **"Altri nella scena"** — `OthersForPlayer`: elenco di **soli nomi**. Niente PF, iniziativa,
  posizione, né stile "defeated".
- **Spettatore** (nessuna riga propria): solo "Altri nella scena" + messaggio d'attesa.

### 4. CSS — `Pages/Combat.razor.css`

Stili per: banner "È il tuo turno", intestazioni delle due sezioni player, evidenziazione della
scheda propria al turno, elenco nomi degli altri. Solo **design token** esistenti; scoped alla
pagina (nessun gotcha di isolation, tutto in `Combat.razor`).

### 5. Polling

Invariato (4s). La redazione è puramente al render.

## Test (xUnit, `Tests/`)

Su `CombatVisibility` (helper puro → facile da coprire):
- `IsOwn`: owner `null`/vuoto → false; match → true; non-match → false.
- `IsCurrentTurnOwn`: indice valido mio → true; indice altrui → false; indice fuori range → false;
  `userId` null → false.
- `OthersForPlayer`: esclude le righe del player; ordine alfabetico case-insensitive; righe con
  owner null incluse.
- `OwnForPlayer`: include solo le righe del player, nell'ordine originale.

## Limiti dichiarati

- Filtro **cosmetico lato UI**: i dati grezzi restano nel browser (scelta esplicita).
- I nomi tipo "Goblin 1 / Goblin 2" restano verbatim → il **numero** di nemici resta visibile
  (coerente con i token sul tavolo). Fuori scope nasconderlo.
- Nessun cambio a DB/RLS.

## Verifica

- Build pulita (`dotnet build`, 0 warning/errori in Release).
- Test verdi (`dotnet test Tests/DndCompanion.Tests.csproj`).
- Gate obbligatorio dei due agenti (`critico` + `conformità`) sul diff.
- Aggiornare `docs/DA-FARE.md` / `docs/DIARIO.md`.
