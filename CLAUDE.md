# DndCompanion — istruzioni di progetto

PWA per campagne **D&D 5e** (schede PG, cataloghi, tracker combattimento, note).
Stack: **Blazor WebAssembly / .NET 10** + **Supabase** (PostgreSQL + PostgREST + Gotrue), hosting **GitHub Pages**.

Fonti di verità del progetto (consultale prima di agire):
- `docs/DIARIO.md` — cosa è stato fatto e *perché*. **È l'unico documento di stato che si legge
  sempre.** Non esiste più un elenco di cose da fare: v. «Cosa resta da fare si dice, non si archivia».
- `docs/storico/` — traccia archiviata, **da non leggere di routine**: `db.md` (modifiche al
  database) e `backlog.md` (punti mai ripresi). Si aprono su richiesta esplicita.
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
- **Il deploy rilascia solo il client.** Il database hosted si cambia a mano, **prima** del push, e
  ogni cambio deve restare compatibile col client attualmente live. Nell'ordine inverso vale lo
  stesso: una modifica applicata a mano colpisce subito il sito. Il *come* sta in «Come si cambia il
  database».
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
- **Prima di ogni push, elenca all'utente le verifiche manuali che il push richiede** — ricavandole
  dal lavoro appena fatto, non da un documento. Ciò che il gate automatico non può coprire (una
  pagina che richiede l'accesso, un flusso a due account, la build trimmata) va detto **prima**, non
  dopo.

## Regola obbligatoria: revisione a due agenti (gate calibrato al rischio)

Dopo ogni modifica al **codice**, prima di dichiarare un task completato o di proporre un commit,
**lancia in parallelo, nello stesso messaggio,** i due agenti globali `bug-hunter` e `conformity`
(definiti in `~/.claude/agents/`, Sonnet, sola lettura) sul diff corrente (`git diff HEAD` + file
non tracciati). `bug-hunter` → bug e regressioni; `conformity` → pattern documentati del progetto.

Sono generici per costruzione: **il dominio glielo passi tu nel brief**. A entrambi il diff, e in più:

- a `conformity` → i **file omologhi già esistenti** (lì si vede il riuso mancato) e i «Pattern
  chiave» in fondo a questo file. Su questo progetto guarda in particolare: logica di dominio negli
  helper puri e non nei `.razor`, repository-per-aggregato dietro interfaccia, `.app-toast` (mai
  `.toast`), `ConfirmDialog` invece di `confirm()`, design token in `:root`, isolamento CSS scoped
  che non raggiunge i figli, `CurrentUserService`/`AccessControl` invece del boilerplate auth per
  pagina, a11y sui controlli interattivi.
- a `bug-hunter` → i **call-site dei simboli modificati** (lì si vedono i contratti rotti) e le aree
  calde di questo progetto: formule D&D (modificatori, competenza, TS, PF, spellcasting), lifecycle
  Blazor (`OnParametersSet`, `StateHasChanged`, `EventCallback` non invocati), Singleton con stato
  mutabile condiviso, autorizzazione UI non speculare alle RLS, `.Result`/`.Wait()` e `async void`,
  e se i test esistenti coprono davvero il comportamento nuovo.

Input diversi, altrimenti paghi due volte gli stessi rilievi. Chiedi nel brief la classificazione
`BLOCCANTE`/`SERIO`/`MINORE` e l'uscita secca `NESSUN PROBLEMA`: la guardia al punto 4 ci si appoggia.

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
- I due agenti sono **globali** (`~/.claude/agents/`): non conoscono questo progetto e non lo
  imparano da soli. Tutto ciò che devono sapere passa dal brief — un pattern che non gli hai
  passato, per loro non esiste. È il motivo per cui i «Pattern chiave» stanno qui e non altrove.
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
- **Gli agenti in parallelo non committano: committo io** (regola del 2026-08-06). Condividono lo
  stesso working tree, e due `git commit` simultanei si contendono il lock dell'index. Nel prompt va
  detto esplicitamente, insieme a: «se il build fallisce in file che non sono tuoi, è un altro agente
  a metà lavoro — aspetta e ritenta, non provare a ripararli».
- **Le firme che fanno da confine le decido io e le passo a tutti in anticipo**, anche a chi scriverà
  contro tipi che ancora non esistono: è ciò che permette di lanciare in parallelo fette che
  altrimenti andrebbero in fila.
- **Quando un agente si ferma al confine e segnala del lavoro scollegato, collegalo subito**
  (verificato due volte il 2026-08-06: un helper testato che nessuno chiamava, e un pulsante che
  invocava un callback vuoto). Fermarsi al perimetro è il comportamento giusto, ma il pezzo resta
  morto finché non lo si innesta — e in un fan-out largo è facile perderlo di vista.
- **Il gate individuale non vede i difetti di giuntura, per costruzione.** Ogni fetta può uscire
  pulita dal proprio giro e il difetto stare comunque nell'incontro fra due: il 2026-08-06, su
  sei agenti, il gate sulle giunture ha trovato un BLOCCANTE che cancellava dati (`CloneCharacter`
  non copiava i campi nuovi) dopo che tutte le fette erano già passate. Il giro sulle giunture non è
  un doppione del gate: è l'unico che può trovare quella classe di difetti.

## Regola obbligatoria: le migrazioni si verificano eseguendole

Una migrazione **non è verificata** finché non gira contro un Postgres vero. Il 2026-08-06 una
verifica statica («parentesi bilanciate, istruzioni contate, sintassi coerente») è stata smentita da
**dieci test rossi** appena lo stack locale è stato acceso — nessuno dei due problemi era della
migrazione, ma nessuno dei due si vedeva senza eseguirla, e applicarla all'hosted fidandosi del
conteggio di parentesi avrebbe significato scoprire lo stato delle policy col sito online.

- **`supabase start` NON riapplica le migrazioni** se il volume del database esiste già: serve
  `supabase db reset`. Senza, i test girano contro lo schema vecchio e falliscono in modo
  fuorviante.
- Se Docker è spento, **accendilo** invece di accettare l'auto-skip: i test saltati non sono test
  verdi, e l'auto-skip esiste per non rompere le altre macchine, non per saltare la verifica qui.
- **Le colonne nuove che entrano nel Model sono la fascia di rischio più alta**: `Update` serializza
  tutte le colonne mappate, quindi se il client va online prima della migrazione **falliscono tutti
  i salvataggi di quella tabella**, non solo la funzione nuova. Un test d'integrazione che faccia il
  giro andata-ritorno col client Postgrest reale (non REST grezzo) è l'unico modo di vederlo prima.
- Il gate a due agenti resta **invariato** e si applica al lavoro dei Sonnet come a qualunque altro:
  è ciò che rende sicura la delega, non un adempimento in più.

## Regola obbligatoria: come si cambia il database (regola del 2026-08-08)

Nata dall'incidente raccontato in [docs/storico/db.md](docs/storico/db.md): un file `.sql` scritto,
committato, **dato per applicato in un documento** e mai eseguito. Per due giorni ogni salvataggio
di `characters` e `inventory` è fallito in produzione, punti ferita compresi, e nessun controllo
automatico poteva accorgersene — perché il controllo era una riga di prosa che diceva «✅».

**1 — Le query si consegnano in chat, non in un file.** Quando serve cambiare il database hosted,
il DDL va **nel messaggio all'utente**, pronto da incollare nell'SQL editor, con sopra una riga che
dice cosa fa. Mai «ho preparato il file X, lanciatelo»: un file è una cosa che si può rimandare, e
ciò che si rimanda si dimentica. Se in futuro avrò un accesso diretto al database (connection string
o Management API), le applicherò io e questo punto decadrà — ma **finché la consegna passa
dall'utente, passa dalla chat**.

**2 — Lo stato di applicazione non si dichiara mai: si interroga.** Nessun documento di questo
repo può contenere la frase «migrazione applicata». La domanda ha una sola risposta valida, ed è
quella del server: `supabase/verifica-schema.sh` confronta i `[Column(...)]` dei Model con le
colonne che PostgREST dichiara e stampa le mancanti. Si esegue **prima di ogni push** e ogni volta
che l'utente segnala un `PGRST204`/`42703`.

**3 — `supabase/migrations/` non è una to-do list.** È lo schema che `supabase db reset` rilegge per
ricostruire lo stack di test locale, e va tenuto allineato per quello. Non è il posto da cui si
applica qualcosa all'hosted, e i suoi file non vanno più scritti come istruzioni per l'utente:
la prosa lunga sul *perché* appartiene al [DIARIO](docs/DIARIO.md), il registro di *cosa e quando* a
[docs/storico/db.md](docs/storico/db.md) — una riga per modifica.

**4 — `supabase db push` è vietato su questo progetto.** Le modifiche storiche sono state applicate a
mano e non risultano registrate in `supabase_migrations`: `db push` proverebbe a rieseguire anche la
baseline `20260624225146_remote_schema.sql`, che è un dump **non idempotente** su un database
popolato.

## Regola obbligatoria: cosa resta da fare si dice, non si archivia (regola del 2026-08-08)

Stessa regola già valida per le query SQL, estesa al lavoro in sospeso: **ciò che l'utente deve fare
glielo dico in chat, non lo lascio in un file che dovrebbe aprire.**

`docs/DA-FARE.md` non esiste più. Si leggeva a ogni sessione — un costo fisso in token — e l'utente
non lo apriva: le verifiche manuali ci restavano dentro per giorni, esattamente come la migrazione
che nessuno aveva applicato. Un elenco che nessuno legge non è una lista di cose da fare: è una
lista di cose dimenticate con una data sopra.

Quindi:
- **A fine lavoro elenco in chat cosa resta**: verifiche manuali che il gate non può coprire, punti
  lasciati aperti di proposito, decisioni che spettano all'utente. Sempre, anche quando è una riga.
- **Non creo documenti di to-do**, e non ne resuscito uno con un altro nome.
- **Il `DIARIO` resta l'unico documento di stato che si legge sempre**, ed è il racconto del
  *perché*: lì la prosa distesa è voluta. Quando un punto si chiude, il suo perché va lì.
- **`docs/storico/`** raccoglie ciò che va conservato ma non riletto: `db.md` (modifiche al
  database, una riga per modifica) e `backlog.md` (i punti tecnici mai ripresi, archiviati il
  2026-08-08). **Non si aprono di routine**, solo su domanda esplicita dell'utente — e se un punto
  viene ripreso, esce di lì.
- I documenti storici (`docs/superpowers/`, `docs/archivio/`) citano ancora `DA-FARE`: sono
  archivio, raccontano com'era allora, **non si riscrivono**.

## Regola obbligatoria: come si scrive sul personaggio di un altro (2026-08-06)

`characters` è una riga monolitica da ~90 colonne, non ha `updated_at`, e
`CharacterRepository.UpdateCharacterAsync` fa `Update(character)`: **riga intera, last-write-wins**.
Finché scrive una persona sola sulla propria scheda va bene. Le RLS però ammettono già un **secondo
scrittore** — `characters_update` vale `owner_id = auth.uid() OR is_campaign_master(campaign_id)` —
e appena l'interfaccia lo sfrutta, quel `last-write-wins` diventa il meccanismo di corruzione dati
numero uno dell'app: il master che assegna 100 mo con in mano una copia stantia della scheda
riscrive *tutte* le colonne, cancellando i PF, l'incantesimo annotato e il level-up appena fatto —
e il salvataggio **riesce**, quindi nessun rollback lo intercetta.

Quindi: **ogni scrittura su un personaggio che non è quello aperto nella propria scheda è o un
insert di riga nuova (`inventory`) o un incremento atomico server-side (`UPDATE … SET col = col + n`
dentro una RPC `SECURITY INVOKER`). Mai `UpdateCharacterAsync` su un PG altrui.** Nemmeno «solo per
una colonna»: il read-modify-write lato client ha la stessa finestra.

## Verifica prima di "fatto"
- Build pulita: `dotnet build` (0 warning / 0 errori atteso in Release).
- Test verdi: `dotnet test Tests/DndCompanion.Tests.csproj`.
- Le RLS si testano solo con lo stack Supabase locale (`Tests.Integration/`, auto-skip se giù).
- Aggiorna `docs/DIARIO.md` quando chiudi un punto, col *perché*. Ciò che resta aperto **si dice
  all'utente in chat**, non si archivia (v. la regola del 2026-08-08).

## Pattern chiave (è questo che passi a `conformity`)
- Logica di dominio → **helper puri `static`** testabili (xUnit) — per lo più `public static`; `internal static` + `InternalsVisibleTo` quando l'helper è privato di un repository/servizio — non nei `.razor`. Modelli già in casa: `CharacterCalculations`, `CharacterNormalizer`, `AccessControl`, `CharacterSpellJoin`, `CombatImport`, `FormValidation`, `CharacterWizardLogic`.
- Dati → **repository-per-aggregato** dietro interfaccia in `Services/Repositories/`, iniettati via DI (Singleton); nessuna query dentro i `.razor`; client e sessione dietro la facade `SupabaseClient`/`SupabaseService` (`From<T>`/`Rpc<T>`/`Auth`).
- Stato utente → `CurrentUserService.EnsureLoadedAsync()` (`UserId`/`DisplayName`/`IsMaster`/`CampaignId`): non ricreare il boilerplate auth pagina per pagina.
- Autorizzazione UI → `AccessControl.CanEdit` (master-o-proprietario), **speculare** alle RLS server-side.
- UI → toast `.app-toast` (mai `.toast`: collide con Bootstrap e diventa invisibile), `ConfirmDialog` (mai `confirm()`), `<LoadingSpinner>`, `DbErrorBanner` per errori di sistema. Errori di validazione → toast; errori di sistema/operazione → banner.
- a11y → controlli interattivi con `role`/`tabindex`/`aria-*` e Enter/Space; `aria-label` sui pulsanti icona-pura.
- CSS → **design token** in `:root`; lo scope isolato del genitore non raggiunge i figli, `@media` incluse (replica nel figlio o promuovi in `app.css`).
- Refactor → dichiarati e verificati **a comportamento invariato**.
- **Gotchas da non reintrodurre**: le caratteristiche si clampano **a monte** (`CharacterNormalizer` non clampa); `postgrest-csharp 3.5.1` va in NRE sui predicati con **OR annidato** → filtra client-side (l'RLS copre comunque la visibilità); `Table.Delete` ritorna `void`, quindi non dice se le RLS hanno bloccato; niente dipendenze pesanti (trimming `full` attivo, Realtime/`System.Reactive` rimossi di proposito, `Newtonsoft.Json` è il serializzatore Supabase e non è rimovibile ora); CSP restrittiva in `<meta>`, `connect-src` solo self+Supabase, `localhost` solo in dev.
- **Chi muta il personaggio prima di salvarlo deve saper tornare indietro**, e catturare il
  riferimento **prima** dell'`await`. Due difetti gemelli, trovati il 2026-08-06 in otto punti:
  (a) un update rifiutato dalle RLS **non solleva eccezioni** — PostgREST aggiorna zero righe e
  risponde `[]` — quindi chi non controlla il valore di ritorno annuncia un successo che non c'è
  stato; (b) fra un `await` e l'altro Blazor smista gli eventi, quindi `selected` e il parametro
  `Character` possono essere **un altro personaggio** al rientro, e ripristinare leggendo la
  proprietà scrive i valori di una scheda dentro un'altra.
- **Un campo nuovo su `Character` va aggiunto anche a `CloneCharacter`**, o il form di modifica lo
  cancella al primo salvataggio. È già successo due volte: ora `Tests/CharacterCloneTests.cs`
  confronta per riflessione ogni proprietà e fallisce al prossimo campo dimenticato.
- **Test che si accorgono da soli del prossimo errore.** Dove un elenco scritto a mano può
  scollegarsi dalla realtà, il test incrocia le due fonti invece di ricopiarne una: la whitelist di
  `LevelUpPlanner.Applica` confronta il personaggio prima e dopo campo per campo, i suggerimenti di
  `ClassResourceRules` si verificano contro il pacchetto SRD, `CharacterCloneTests` contro il
  modello. Costano poco e reggono senza manutenzione.
- **Un test nato per sorvegliare una correzione va provato per mutazione**: togli la correzione,
  verifica che diventi **rosso**, ripristina. Costa un minuto ed è la sola prova che serva a
  qualcosa. Il 2026-08-06 il test della doppia pianificazione usava Costituzione 14, e il
  modificatore di 14 è lo stesso di 15: **passava identico anche senza il fix**. Quando il valore
  scelto è ciò che rende il test non vacuo (una parità, uno zero, una soglia), scrivilo **accanto al
  valore** — altrimenti la prossima «semplificazione» riporta il test alla vacuità senza segnali.
