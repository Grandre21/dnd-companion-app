# DndCompanion — istruzioni di progetto

PWA per campagne **D&D 5e** (schede PG, cataloghi, tracker combattimento, note).
Stack: **Blazor WebAssembly / .NET 10** + **Supabase** (PostgreSQL + PostgREST + Gotrue), hosting **GitHub Pages**.

Fonti di verità del progetto (consultale prima di agire):
- `docs/DA-FARE.md` — backlog aperto, con priorità.
- `docs/DIARIO.md` — cosa è stato fatto e *perché*.
- `docs/superpowers/specs/` e `docs/superpowers/plans/` — spec e piani.
- Memoria in `~/.claude/projects/.../memory/` (gotchas e decisioni).

## Regola obbligatoria: si lavora solo su `main`

**Un solo ramo: `main`.** Niente feature branch, niente pull request: si committa direttamente su
`main`. Questa regola sostituisce il mio comportamento di default, che su un ramo principale
creerebbe prima un branch: qui **non** va fatto. (I rami storici eventualmente rimasti sono
archivio: non ci si lavora.)

**`main` è il ramo di rilascio.** `.github/workflows/deploy.yml` parte a ogni **push** su `main`,
senza approvazioni: *ciò che spingo è già online*. Da qui tutto il resto.

- **Il push è sempre e solo su richiesta esplicita dell'utente.** Questa riga **restringe il punto 5
  del gate** qui sotto: con l'uscita pulita posso committare da solo, **pubblicare mai**.
- **Il verde della CI non dice che l'app funziona.** Il workflow non esegue i test, e `dotnet build`
  — il check della sezione «Verifica prima di "fatto"» — **non attiva il trimming**, che vive solo
  nel publish Release. Il difetto tipico di questo repo (costruttore rimosso dal trimmer e usato via
  reflection da Newtonsoft/Gotrue/Postgrest, v. i `TrimmerRootAssembly` nel `.csproj`) compila e
  gira in locale, e si vede **solo** sul sito pubblicato. Prima di un push che tocchi dipendenze,
  serializzazione o modelli: `dotnet publish DndCompanion.csproj -c Release -o publish` — il nome del
  progetto serve: senza, il CLI prende la solution, tira dentro i test e piazza le copie **non
  trimmate** accanto al `wwwroot` — e poi l'app servita da `publish/wwwroot`, **con accesso fatto e
  almeno una pagina di dati aperta**: è lì che scattano le deserializzazioni Gotrue/Postgrest, mentre
  la sola schermata di login non ne esercita nessuna. Serve `localhost` fra i Redirect URLs di
  Supabase, altrimenti il login rimbalza sul sito pubblicato e si finisce per collaudare quello.
  Non fidarsi dei controlli indiretti: gli avvisi di trim li spegne il Blazor SDK
  (`SuppressTrimAnalysisWarnings` vale `true` se non la si forza), e Gotrue/Postgrest compaiono in
  `_framework` **anche se il rooting salta** — li istanzia direttamente `SupabaseService` — solo più
  piccoli e coi costruttori via reflection già strippati: il confronto che discrimina è la loro
  taglia contro quella del `.dll` NuGet.
- **Il deploy rilascia solo il client.** Le migrazioni in `supabase/migrations/` vanno applicate a
  mano al progetto hosted, **prima** del push, e devono restare compatibili col client attualmente
  live. Nell'ordine inverso vale lo stesso: una migrazione applicata a mano colpisce subito il sito.
- **L'aggiornamento PWA è on-demand.** Il service worker non fa `skipWaiting`: dopo il push gli
  utenti restano sulla versione in cache finché non premono «Aggiorna». Quindi (a) verificare a
  vista in incognito o con hard-reload, altrimenti si guarda la build vecchia e si conclude che il
  deploy non è partito; (b) ogni cambio di formato dati deve restare compatibile **anche col client
  precedente**, che parla con lo stesso database.
- **I dati sono sempre quelli di produzione**, anche in `dotnet run` locale: c'è un solo
  `wwwroot/appsettings.json`. Per provare scritture distruttive si usa lo stack Supabase locale
  (`Tests.Integration/`).
- **Rollback = `git revert` + push.** Mai `--force` su `main`: non esiste un "ripubblica il commit
  precedente", `workflow_dispatch` rimette comunque online la punta del ramo.
- **Attenzione a `wwwroot/index.html`:** la CI riscrive `<base href>` e toglie `localhost` dalla CSP
  con due `sed` che **falliscono in silenzio** se il testo non combacia più (workflow verde, sito
  bianco o CSP di produzione che ammette ancora localhost). Se tocchi quelle due righe, verificale
  contro `deploy.yml`.
- **Prima di ogni push, rileggi le verifiche manuali in sospeso** in `docs/DA-FARE.md` e segnalale
  all'utente. Ciò che il gate automatico non può coprire (una pagina che richiede l'accesso, un
  flusso a due account) va detto **prima**, non dopo.

## Regola obbligatoria: revisione a due agenti (gate a ciclo chiuso)

Dopo **ogni** modifica — codice **o** documentazione — prima di dichiarare un task completato o di proporre un commit:

1. **Lancia in parallelo** i due subagent `critico` e `conformità` (definiti in `.claude/agents/`) sul **diff corrente** (`git diff HEAD` + file non tracciati).
   - `critico` → bug e regressioni.
   - `conformità` → rispetto dei pattern documentati del progetto.
2. Se emergono finding: **correggili** — autonomamente quelli certi; per gli ambigui scegli l'interpretazione più sicura e annotala. Poi **rilancia entrambi gli agenti** sul nuovo diff.
3. **Ripeti** finché entrambi rispondono `NESSUN PROBLEMA` (**uscita pulita**) **oppure** finché scatta la guardia anti-loop (punto 4).
4. **Guardia anti-loop**: al massimo **3 giri**, poi **fermati sempre**, qualunque sia la gravità residua. In ogni caso **non committare**: riporta all'utente i finding ancora presenti — marcando come **bloccanti** quelli `BLOCCANTE`/`SERIO` — e chiedi come procedere. Prosegui solo dietro sua conferma.
5. **Solo con l'uscita pulita del punto 3** (entrambi `NESSUN PROBLEMA`) procedo **autonomamente** a dichiarare il lavoro fatto / a committare. Qualunque conclusione dopo l'**uscita via guardia** (punto 4) avviene **senza commit automatico** e richiede la **conferma esplicita** dell'utente. In nessuno dei due casi il **push** è mai automatico: su `main` pubblica, e resta su richiesta esplicita (v. la regola sul ramo unico).

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
