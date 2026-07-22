# DndCompanion — istruzioni di progetto

PWA per campagne **D&D 5e** (schede PG, cataloghi, tracker combattimento, note).
Stack: **Blazor WebAssembly / .NET 10** + **Supabase** (PostgreSQL + PostgREST + Gotrue), hosting **GitHub Pages**.

Fonti di verità del progetto (consultale prima di agire):
- `docs/DA-FARE.md` — backlog aperto, con priorità.
- `docs/DIARIO.md` — cosa è stato fatto e *perché*.
- `docs/superpowers/specs/` e `docs/superpowers/plans/` — spec e piani.
- Memoria in `~/.claude/projects/.../memory/` (gotchas e decisioni).

## Regola obbligatoria: revisione a due agenti (gate a ciclo chiuso)

Dopo **ogni** modifica — codice **o** documentazione — prima di dichiarare un task completato o di proporre un commit:

1. **Lancia in parallelo** i due subagent `critico` e `conformità` (definiti in `.claude/agents/`) sul **diff corrente** (`git diff HEAD` + file non tracciati).
   - `critico` → bug e regressioni.
   - `conformità` → rispetto dei pattern documentati del progetto.
2. Se emergono finding: **correggili** — autonomamente quelli certi; per gli ambigui scegli l'interpretazione più sicura e annotala. Poi **rilancia entrambi gli agenti** sul nuovo diff.
3. **Ripeti** finché entrambi rispondono `NESSUN PROBLEMA` (**uscita pulita**) **oppure** finché scatta la guardia anti-loop (punto 4).
4. **Guardia anti-loop**: al massimo **3 giri**, poi **fermati sempre**, qualunque sia la gravità residua. In ogni caso **non committare**: riporta all'utente i finding ancora presenti — marcando come **bloccanti** quelli `BLOCCANTE`/`SERIO` — e chiedi come procedere. Prosegui solo dietro sua conferma.
5. **Solo con l'uscita pulita del punto 3** (entrambi `NESSUN PROBLEMA`) procedo **autonomamente** a dichiarare il lavoro fatto / a committare. Qualunque conclusione dopo l'**uscita via guardia** (punto 4) avviene **senza commit automatico** e richiede la **conferma esplicita** dell'utente.

Note:
- Gli agenti sono in **sola lettura**: le correzioni le applico io tra un giro e l'altro.
- Se il diff è solo `.md`, gli agenti scalano la revisione a coerenza/accuratezza del testo.
- Le **definizioni degli agenti** (`.claude/agents/`) stanno in una cartella **git-ignored** (`.gitignore`): non compaiono in `git diff`/`git status`. Se le modifichi, passale **esplicitamente** agli agenti per la revisione.
- I due agenti sono complementari a `/code-review` e `/security-review`, non li sostituiscono.

## Verifica prima di "fatto"
- Build pulita: `dotnet build` (0 warning / 0 errori atteso in Release).
- Test verdi: `dotnet test Tests/DndCompanion.Tests.csproj`.
- Le RLS si testano solo con lo stack Supabase locale (`Tests.Integration/`, auto-skip se giù).
- Aggiorna `docs/DA-FARE.md`/`docs/DIARIO.md` quando chiudi o apri un punto.

## Pattern chiave (dettaglio nell'agente `conformità`)
- Logica di dominio → **helper puri `static`** testabili (xUnit) — per lo più `public static`; `internal static` + `InternalsVisibleTo` quando l'helper è privato di un repository/servizio — non nei `.razor`.
- Dati → **repository-per-aggregato** dietro interfaccia in `Services/Repositories/`; client dietro la facade `SupabaseClient`.
- UI → toast `.app-toast` (mai `.toast`), `ConfirmDialog` (mai `confirm()`), `<LoadingSpinner>`, `DbErrorBanner` per errori di sistema.
- CSS → **design token** in `:root`; lo scope isolato del genitore non raggiunge i figli (replica o promuovi in `app.css`).
- Refactor → dichiarati e verificati **a comportamento invariato**.
