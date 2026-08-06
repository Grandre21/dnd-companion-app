# DndCompanion — istruzioni di progetto

PWA per campagne **D&D 5e** (schede PG, cataloghi, tracker combattimento, note).
Stack: **Blazor WebAssembly / .NET 10** + **Supabase** (PostgreSQL + PostgREST + Gotrue), hosting **GitHub Pages**.

Fonti di verità del progetto (consultale prima di agire):
- `docs/DA-FARE.md` — backlog aperto, con priorità. **Solo punti aperti, 1-3 righe ciascuno**
  (v. «Forma dei documenti»); i punti chiusi stanno in `docs/archivio/DA-FARE-chiuso.md`.
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

## Regola obbligatoria: revisione a due agenti (gate calibrato al rischio)

Dopo ogni modifica al **codice**, prima di dichiarare un task completato o di proporre un commit,
**lancia in parallelo** i due subagent `critico` e `conformità` (definiti in `.claude/agents/`) sul
diff corrente (`git diff HEAD` + file non tracciati). `critico` → bug e regressioni; `conformità` →
pattern documentati del progetto.

**Quanti giri dipende da cosa il diff tocca** (calibrazione decisa il 2026-08-01: il gate costa
centinaia di migliaia di token, e applicarlo uguale a un refuso e a una migrazione è spreco):

| Il diff tocca | Giri |
|---|---|
| **Solo documentazione** (`.md`) | **nessuno.** Gli agenti non si lanciano affatto: rileggo io il testo contro il codice, verificando i numeri che cito. |
| UI, CSS, testo dell'interfaccia, refactor circoscritti | **1 giro**, poi correggo e riporto quel che resta |
| Dati, RLS, migrazioni, serializzazione, modelli, permessi, formato di scambio | fino a **3 giri** |

1. Se emergono finding: **correggili** — autonomamente quelli certi; per gli ambigui scegli
   l'interpretazione più sicura e annotala.
2. Dove sono previsti più giri, **rilancia entrambi gli agenti**; dal secondo giro fai rivedere **solo
   gli hunk cambiati**, non tutto il diff.
3. **Ripeti** finché entrambi rispondono `NESSUN PROBLEMA` (**uscita pulita**) o finché finisci i giri
   previsti dalla tabella.
4. **Guardia anti-loop**: mai più di **3 giri**, qualunque sia la gravità residua. Esaurita la quota
   **non committare**: riporta i finding ancora presenti — marcando come **bloccanti** quelli
   `BLOCCANTE`/`SERIO` — e chiedi come procedere.
5. **Solo con l'uscita pulita** procedo **autonomamente** a dichiarare il lavoro fatto / a committare.
   Dopo l'uscita via guardia serve la **conferma esplicita** dell'utente. In nessuno dei due casi il
   **push** è automatico: su `main` pubblica, e resta su richiesta esplicita.

Note:
- **Se il lavoro è stato scritto da più subagent in parallelo, il gate va puntato sulle giunture.**
  Verificato il 2026-08-01 su un fan-out di tre agenti a insiemi di file disgiunti: la divisione ha
  funzionato (nessun conflitto, helper puri e test da ciascuno), ma **tutti** i difetti gravi stavano
  dove un insieme incontra l'altro — l'export produceva un file che il parser nuovo rifiutava, e un
  test rosso stava in un file che nessuno dei tre aveva in mano. Nessuno dei tre poteva vederli, per
  costruzione. Quindi: nel prompt del gate elenca **esplicitamente** i confini (chi scrive / chi legge
  lo stesso dato) e chiedi che li attraversino.
- **Economia del prompt**: agli agenti passa **solo i file rilevanti** e i fatti già verificati
  («build Release 0/0, N test verdi: non rilanciarli»). Senza questa riga rifanno build e test a ogni
  giro, per ciascuno.
- Gli agenti del gate sono in **sola lettura**. Le correzioni fra un giro e l'altro **non le scrivo
  io**: le fa scrivere a un Sonnet, come ogni altra modifica al codice (v. «Chi scrive il codice»).
- Le **definizioni degli agenti** (`.claude/agents/`) stanno in una cartella **git-ignored**: non
  compaiono in `git diff`/`git status`. Se le modifichi, passale **esplicitamente** agli agenti.
- I due agenti sono complementari a `/code-review` e `/security-review`, non li sostituiscono.

## Regola obbligatoria: chi scrive il codice (regola del 2026-08-06)

Tre ruoli distinti, e **non li cumulo**:

| Ruolo | Chi | Cosa fa |
|---|---|---|
| **Progetto e revisione** | io (Opus) | taglio il lavoro in fette, scrivo i contratti fra le fette, reviso ciò che torna, tengo la conversazione con l'utente |
| **Scrittura** | subagent **Sonnet** (`model: sonnet`) | ogni modifica ai file di codice, test compresi |
| **Consulenza** | subagent **Fable** (`model: fable`) | pareri di progettazione nei punti dubbi. **Sola lettura**: non tocca file |

- **Non scrivo codice di mia mano.** Nemmeno la correzione di una riga, nemmeno quelle che nascono
  dal gate: preparo l'istruzione e la passa un Sonnet. Se la modifica è troppo piccola per meritare
  un subagent, è comunque un subagent — la regola vale perché è uniforme, non perché ogni singola
  delega convenga.
- **Faccio io la documentazione di progetto** (`CLAUDE.md`, `docs/`): è la mia memoria di lavoro, e
  passarla di mano le farebbe perdere il *perché*. La tabella del gate lo conferma già: sui `.md` non
  si lanciano agenti.
- **Nel dubbio, chiedo a Fable prima di decidere**, non dopo aver fatto scrivere il codice. Un
  consulto costa molto meno di una fetta da rifare. Vale in particolare per: la forma di
  un'astrazione nuova, un contratto fra due fette, e ogni volta che due letture del requisito
  porterebbero a codice diverso.
- **Il fan-out ai Sonnet va a file disgiunti**, con i confini dichiarati nel prompt di ciascuno. Il
  gate poi si punta sulle **giunture**, per la ragione già verificata il 2026-08-01: i difetti gravi
  stanno dove una fetta incontra l'altra, e nessuno degli autori può vederli per costruzione.
- Il gate a due agenti resta **invariato** e si applica al lavoro dei Sonnet come a qualunque altro:
  è ciò che rende sicura la delega, non un adempimento in più.

## Forma dei documenti (regola del 2026-08-01)

`docs/DA-FARE.md` si legge a ogni sessione: la sua lunghezza è un **costo fisso**. Quindi:
- **DA-FARE = indice di soli punti aperti**, una voce in 1-3 righe. Niente storia, niente misure,
  niente alternative scartate: quelle vanno nel `DIARIO`, e la voce rimanda lì.
- Quando un punto si chiude, **non si annota «✅ FATTO» in DA-FARE**: si toglie da lì e il perché
  finisce nel `DIARIO`. Il materiale storico sta in `docs/archivio/DA-FARE-chiuso.md`, che è un
  archivio e non si aggiorna.
- Il `DIARIO` resta il racconto, e lì la prosa distesa è voluta: è la sola sede del *perché*.

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
