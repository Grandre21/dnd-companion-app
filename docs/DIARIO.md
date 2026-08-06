# DIARIO DI PROGETTO — D&D Companion

> Promemoria sintetico di **cosa è stato fatto e perché**. Per ciò che resta aperto vedi [DA-FARE.md](./DA-FARE.md).
> Aggiornato: **2026-07-31**.

## Cos'è
PWA per gestire campagne **D&D 5e**: schede personaggio, cataloghi (incantesimi, mostri, razze, classi),
tracker del combattimento e note. Nata come strumento privato tra amici, in evoluzione verso un prodotto
pubblico (TWA per Play Store, installabile anche su iPhone). Tema dark fantasy, mobile-first.

**Stack:** Blazor WebAssembly su **.NET 10**, backend **Supabase** (PostgreSQL + PostgREST + Gotrue),
hosting **GitHub Pages** (sottopercorso `/dnd-companion-app/`) con deploy via GitHub Actions.

## Cosa è stato fatto (e perché)

**Scheda personaggio.** È il cuore dell'app. Costruita prima come modello dati completo (TS, skill,
incantatore, denari, sintonie), poi separati i **calcoli derivati** in `CharacterCalculations` (funzioni
pure) per non duplicare le formule D&D e poterle un giorno testare. La UI è stata riorganizzata a **tab**
(Combat/Stats/Bio/Items/Magic): un'unica schermata era ingestibile su mobile.

**Migrazione auth a Supabase + Google.** Inizialmente l'accesso era un PIN custom salvato in localStorage.
È stato **abbandonato** perché insicuro e non adatto a un prodotto reale: ora si usa **OAuth Google** con
sessione JWT gestita da Gotrue. Il bootstrap della sessione è centralizzato in `GetClientAsync()` per
risolvere una race condition al ritorno OAuth e la persistenza dopo reload.

**Multi-campagna.** Insieme all'auth è stato introdotto il modello a campagne: `owner_id`/`campaign_id`,
selettore della campagna attiva, **join via codice invito** (risolto server-side con la RPC
`find_campaign_by_invite_code` per non esporre tutte le campagne), ruoli per-membro (Master/Player) da
`campaign_members.role`. Permessi: creazione aperta a tutti i membri, modifica per owner del dato o Master.

**PWA aggiornabile.** Il caveat classico Blazor offline è che l'utente resta su una build vecchia. È stato
aggiunto un **aggiornamento on-demand**: banner "nuova versione" + `skipWaiting` solo su click (niente
auto-reload a sorpresa). Corretto anche il **base path dinamico** del service worker (prima era `/`, rotto
sul sottopercorso di GitHub Pages) e `clients.claim` per avere l'offline dalla prima visita.

**Alleggerimento bundle.** Il primo caricamento WASM è pesante. Nel `.csproj` (Release) sono stati attivati
**trimming `full`**, `InvariantGlobalization` e i feature-switch runtime (debugger/eventsource/ecc.).
Necessario aggiungere `TrimmerRootAssembly` per `Supabase.Gotrue`/`Supabase.Postgrest`: con `TrimMode=full`
il trimmer rimuoveva i costruttori usati via reflection da Newtonsoft, rompendo la deserializzazione.
Realtime è disattivato (`AutoConnectRealtime = false`) ma la dipendenza è ancora inclusa — rimozione in da-fare.

**Rifinitura pre-lancio.** Restyling Home a tema dark fantasy, FAB di creazione centralizzato in `app.css`,
meta tag iOS per l'installazione su iPhone, e pulizia dei log diagnostici (rimosso un leak dell'access token
nei log OAuth).

**Consolidamento UX e fondamenta (giu 2026).** Tre quick-win nati dall'uso reale: tasto di **riparazione
cache** negli errori di connessione (`DbErrorBanner` + `repairApp`, risolve il caso Firefox senza far
ri-loggare), pagina interna **`/_showroom`** come libreria UI, e **scheda PG più leggibile** (sezioni del
form numerate + riepilogo bonus). Poi un giro di consolidamento sulle fondamenta: **suite di test**
(`DndCompanion.Tests`, xUnit, su `CharacterCalculations`), **`ErrorBoundary`** globale a tema, dedup del
parsing dei dadi vita, **design token** (palette in `:root` e conversione completa dei colori nei
`.razor.css`), **accessibilità da tastiera** dei controlli interattivi (`StatCard`, `SpellListItem` e i toggle
di `Characters.razor`: death save, ispirazione, slot incantesimo — role/aria/Enter-Space, senza cambiare
l'aspetto), e uno **spinner di caricamento** a tema al posto dei "Caricamento..." testuali.

**Funzionalità e robustezza (giu 2026, 2ª parte).** **Combat condiviso**: il tracker iniziativa, prima locale
al solo Master, è diventato un dato condiviso per campagna (tabella `combat_state` con `combatants` jsonb +
polling ~4s per i giocatori) — i giocatori vedono turno e PF in tempo quasi reale. **Fix auth**: al riavvio
l'access token scaduto non veniva rinnovato (`LoadSession` non fa rete) → "JWT expired" e app bloccata; ora si
tenta il refresh col refresh token e, se fallisce, logout pulito. **Feedback**: toast a tema
("✓ Salvato/Eliminato") sul salvataggio PG e su tutti i CRUD; **dialog di conferma a tema** (`ConfirmDialog`)
al posto dei `confirm()` nativi; contrasto `--gold-dim` alzato.
Restano (lavoro grande, vedi [DA-FARE.md](./DA-FARE.md)): **mega-refactor**
(`Characters.razor`/`SupabaseService`), e le feature di prodotto (AI alla compilazione, wizard scheda, i18n).

**Sicurezza RLS (giu 2026).** Audit del DB: le Row-Level Security erano **già attive e corrette** su tutte le
tabelle (helper `is_campaign_member`/`is_campaign_master`, FK già `ON DELETE CASCADE`), contrariamente a quanto
annotato in passato. Chiusi i **due gap** residui: `combat_state` era spalancato (policy `ALL true/true`) → ora
lettura ai membri e scrittura al solo master; e `campaign_members` permetteva l'auto-promozione a master → ora i
join dei player passano dalla RPC `SECURITY DEFINER` `join_campaign` (codice validato server-side) e l'insert
diretto è riservato all'owner. Con questo il **gate del lancio pubblico è soddisfatto**. Spec e piano in
`docs/superpowers/`.

**Refactor Characters.razor — tab estratti (Fase 2B, giu 2026).** La pagina monstre (~2.4k righe) è scesa a
~1.35k: i 5 tab sono ora componenti in `Shared/CharacterTabs/` (`CharacterBioTab`, `CharacterStatsTab`,
`CharacterCombatTab`, `CharacterItemsTab`, `CharacterMagicTab`), col pattern `Character` + `EventCallback`
(precedente: `StatCard`). Gli helper puri condivisi vivono in `CharacterView` (FormatBonus/AriaBool/OnKey +
slot incantesimo) importati via `@using static`. Estrazione a **comportamento invariato**, un tab per commit con
verifica in locale; il CSS isolato spostato per-tab (classi davvero condivise — `card-label`/`section-header`/
`empty-note` — promosse in `app.css`). Note d'architettura: l'inventario resta del genitore (Combat ne legge le
armi → `OnInventoryChanged` ricarica), il catalogo incantesimi resta in cache nel genitore e si passa ai figli.
**Form estratto (follow-up Fase 2B, 2026-06-24):** anche il form di modifica/creazione è ora un componente,
`CharacterEditForm` (accordion a 7 sezioni + lo stato UI del solo form: `formSections`, classe/razza custom,
handler degli slot incantesimo), con interfaccia `Draft`/`Classes`/`Races`/`IsBusy`/`OnSave`/`OnCancel`; il
genitore mantiene la proprietà del draft, `NormalizeDraft`/`SaveFormAsync`/`CancelForm` e il cambio vista. Il
componente si auto-inizializza in `OnParametersSet` (confronto `ReferenceEquals` sul `Draft`), così `OpenEditForm`
non setta più `formSections`/custom. La media query desktop di `.form-view` è stata **replicata** nel CSS del
componente (lo scope isolato del genitore non raggiunge il figlio — vale anche per le `@media`). `Characters.razor`
è così scesa da ~1.35k a **~660 righe**. Della componentizzazione non resta nulla; aperte solo le sotto-fasi
A (`SupabaseService` → repository) e C (stato auth/ruolo).

**Sotto-fase A — `SupabaseService` → repository (2026-06-24).** Il god-object dell'accesso dati (~43 metodi, 577
righe) è stato spezzato in **11 repository per aggregato dietro interfacce** in `Services/Repositories/` (Character,
Spell, Monster, Note, CombatState, Profile, Race, Class, Inventory, CharacterSpell, Campaign). Ogni repository
dipende da `SupabaseService` per il client e mantiene i metodi **identici** (estrazione a comportamento invariato);
i consumatori (9 pagine/tab + `CampaignStateService`) iniettano i repo invece del servizione. `SupabaseService`
resta il **provider di sessione/client** (`GetClientAsync` + bootstrap OAuth/refresh/persistenza), sceso a 127
righe; lo usano ancora `AuthRedirect`/`Login`/`Home`/`AuthStateService` per il client. Tutti Singleton in DI.
Vantaggio chiave: superficie testabile (mocking dei repo, §4 di [DA-FARE.md](.\DA-FARE.md)). Resta della §3 solo
la sotto-fase C (stato auth/ruolo centralizzato). Piano in `docs/superpowers/`.

**Sotto-fase C — stato utente centralizzato (2026-06-24).** Nuovo `CurrentUserService`, facade su
`AuthStateService` + `CampaignStateService`: espone `UserId`/`DisplayName`/`IsMaster`/`CampaignId` dietro un'unica
`EnsureLoadedAsync()`. Le 7 pagine dati (Characters, Combat, Spells, Classes, Races, Notes, Monsters) hanno
sostituito il boilerplate ripetuto (`InitializeAsync` + lettura di `userId`/`isMaster`/`campaignId` + 3 campi
locali) con una sola chiamata, leggendo direttamente dal facade; rimosse da quelle pagine le iniezioni di
`AuthState`/`CampaignState`. `Home` resta l'hub auth/campagna (logout, scelta/uscita campagna). Rimosso
`AuthStateService.GetRoleAsync()` perché codice morto (il ruolo vive in `CampaignStateService`). Scelta di
**non** fare un provider full-reactive con eventi (YAGNI: nessuna pagina ha bisogno di aggiornarsi live al cambio
auth/campagna). Comportamento invariato, build 0/0 + 62 test. **Con questo la §3 (architettura) è completa**:
restano aperte solo voci minori (gestione errori, performance, a11y) e le feature di prodotto.

**Import mostri nel combattimento (2026-06-24).** Il tracker combattimento del Master ora permette di importare
direttamente i mostri della campagna. Helper puro `Services/CombatImport.cs`: `ParseLeadingHp(string?)` estrae i
PF dal **primo intero** del testo libero del campo HitPoints (fallback 1, il Master corregge inline);
`FromMonster(monster, quantity)` genera una lista di `Combatant` con nomi numerati per le copie ("Goblin",
"Goblin 2"…), iniziativa 0 e `CurrentHp = MaxHp`. Coperto da test xUnit. In `Combat.razor` un pannello inline
**master-only** "Importa mostri" carica i mostri via `IMonsterRepository` (lazy, al primo click), mostra uno
stepper quantità per riga e aggiunge i combattenti via `SaveCombatStateAsync`. Nessuna modifica a DB o RLS.

**Visibilità limitata del player nel tracker (2026-07-23).** Un giocatore non deve vedere le informazioni
altrui: nel tracker ora vede **solo la propria scheda** (PF e iniziativa) e, delle altre entità, **solo il
nome** — niente statistiche né ordine di turno. Riceve però il segnale "È il tuo turno!" quando tocca a lui,
mentre l'indicatore **non svela mai** di chi sia il turno corrente (che rivelerebbe la sequenza). L'aggancio
"riga mia" nasce all'import: `ImportCharactersAsync` marca ogni `Combatant` con l'`owner_id` del PG (nuovo
campo sul POCO — `combatants` è già `jsonb`, nessuna migrazione), e il player riconosce le proprie righe da
lì; mostri e aggiunte a mano (owner null) restano semplici nomi. La decisione su cosa mostrare vive in un
helper puro testato `Services/CombatVisibility.cs` (`IsOwn`/`IsCurrentTurnOwn`/`OwnForPlayer`/`OthersForPlayer`,
con gli "altri" ordinati per nome così l'ordine non svela l'iniziativa); `Combat.razor` biforca la vista
player da quella master (invariata). È una redazione **cosmetica lato UI** (i dati grezzi arrivano comunque
al browser via polling): scelta accettabile per un gruppo di amici, senza toccare DB/RLS. Spec/piano in
`docs/superpowers/` (2026-07-23).

**Rifiniture da revisione di progetto (2026-07-23).** Passata dei due agenti `critico`/`conformità` sull'intero
codebase. Chiuso un gap di autorizzazione UI: il tab **Note libere** (`CharacterBioTab`) era l'unico a non
ricevere `CanEdit` → textarea e "Salva note" erano attivi anche per un non-proprietario/non-master. Il
salvataggio era un **falso successo silenzioso** (coerente col gotcha RLS noto): la policy filtrava la riga → 0
update → PostgREST 200 con array vuoto → `UpdateCharacterAsync` ritorna null → nessun sync e nessun errore
mostrato, con la nota modificata solo localmente e **mai persistita**. Ora il Bio è speculare agli altri tab e
alle RLS (`disabled`/pulsante nascosto + guardia `if (!CanEdit) return;` anche nel genitore). Piccoli irrobustimenti in
`Combat.razor`: l'import personaggi clampa gli HP (`MaxHp≥1`, `CurrentHp∈[0,MaxHp]`) come già fanno
`AddCombatant`/`CombatImport`, e l'indice di turno è protetto da valori fuori range (negativo/≥Count → inizio
round). Pulizia conformità: rimossi da `StatCard` gli helper `FormatBonus`/`AriaBool`/`OnKey` duplicati (ora
dai condivisi in `CharacterView`, stessa direzione di `826ed1c`) e tokenizzato l'ultimo literal con token esatto
rimasto nei `.razor.css` (`#1a0e1f` → `var(--bg)` in `MainLayout.razor.css`).

*Seconda passata (2026-07-24, stessa revisione):* rimosso l'evento morto `CampaignStateService.OnActiveCampaignChanged`
(nessun sottoscrittore: si era deciso di non agganciarci un refresh live, quindi via il codice morto —
resta l'idea del combat in Realtime in DA-FARE §8); estratta da `Monsters.razor` la logica pura del grado
sfida in `Services/MonsterCatalog.cs` (`ParseChallengeRating`: frazioni 5e "1/8"/"1/4"/"1/2" non parsabili
come numero, sentinella −1 per l'ignoto) ora **coperta da test**; estratto
`CharacterWizardLogic.RaceBonuses(Race?)` come unica fonte dell'ordine FOR,DES,COS,INT,SAG,CAR, così il
wizard non reimplementa più il mapping bonus-razza + clamp ma delega all'helper. Tutto a comportamento
invariato. Nessun cambio a DB/RLS; build 0/0, 220 test verdi.

**Rimozione Realtime/System.Reactive (2026-06-24).** Il meta-pacchetto `supabase-csharp` è stato sostituito
dagli standalone `postgrest-csharp 3.5.1` + `gotrue-csharp 4.2.7`; rimossi `realtime-csharp`,
`supabase-storage`, `System.Reactive` e `Websocket.Client`. La riscrittura è trasparente ai consumatori:
auth e dati sono esposti dalla facade `Services/SupabaseClient.cs` (`From<T>()`/`Rpc<T>()`/`Auth`) che
replica la superficie pubblica del vecchio `SupabaseService`; il token di accesso viene iniettato
per-request tramite `GetHeaders` (l'RLS del DB continua a ricevere il JWT corretto). Build
0/0, 111 test verdi. Il combat resta a **polling** — il Realtime non era usato a runtime e la sua rimozione
non cambia il comportamento. Verifica manuale (login, CRUD, RLS) affidata all'utente prima del push.
*Misura del taglio (publish Release before/after, 2026-06-24):* **−9 assembly** dal bundle
(`Supabase.Realtime`/`Functions`/`Storage`/meta, `System.Reactive`, `Websocket.Client`, lo stack WebSockets,
`System.Threading.Channels`), **−124 KB Brotli** (3.57 → 3.45 MB) / −272 KB RAW. Delta contenuto perché
`TrimMode=full` già sfrondava `System.Reactive`; il valore vero è rimuovere file interi. Smoke test del trim
ok (gli assembly radicati Gotrue/Postgrest sopravvivono) — ⚠️ criterio **smentito il 2026-07-30**: la presenza di
quei due assembly non prova il rooting (li istanzia direttamente `SupabaseService`, quindi ci sarebbero comunque,
solo più piccoli), e gli avvisi di trim li spegne il Blazor SDK. Il risultato di allora resta plausibile, la prova
no: v. `DA-FARE` §2 e la regola sul ramo unico in `CLAUDE.md`. Dettagli e caveat `wasm-tools` in
[DA-FARE.md](./DA-FARE.md) §2.

**Rifinitura UX/a11y + CSP + validazione (2026-06-24).** Tre interventi a basso rischio in un solo /loop.
(1) **UX/a11y**: i "Caricamento..." testuali rimasti (Incantesimi/Mostri/Classi/Razze/Note) ora usano il
componente `<LoadingSpinner>` a tema; `aria-label` aggiunte ai 6 FAB "+" per gli screen-reader. (2) **Validazione
di dominio lato client**: nuovo helper puro `Services/FormValidation.cs` (`ValidateMonster`/`ValidateRace`/`InRange`,
11 unit test) — il form Mostri valida caratteristiche 1–30 e CA 0–40, Razze la velocità 0–120 (Incantesimi e
Personaggi erano già coperti). (3) **CSP** in `<meta>` (unica via su GitHub Pages): `default-src 'self'`,
`connect-src` ai soli self+Supabase, `object-src 'none'`, `base-uri 'self'`. Inizialmente tentato l'approccio a
**hash** sugli script inline (più forte), **abbandonato** perché .NET inietta un `<script type="importmap">`
auto-generato il cui contenuto (fingerprint asset + integrity) cambia ad ogni build → hash fisso insostenibile;
si è scelto `'unsafe-inline'` per gli script (l'app non rende mai HTML grezzo, rischio teorico) tenendo le
direttive restrittive che danno il valore reale. Verificato in locale: boot pulito (0 violazioni CSP), login
Google + CRUD ok, spinner e validazione ok. La virtualizzazione liste (§5) è stata **scartata** (cataloghi < ~50
voci → YAGNI). Build 0/0, 122 test verdi. Spec/piano in `docs/superpowers/`.

**Rifiniture code-side (2026-06-24, 2ª tornata).** Altro /loop su quattro voci puramente di codice.
(1) **a11y banner errori**: `DbErrorBanner` ora si chiude con un vero pulsante **✕** (`aria-label`, da tastiera)
invece del click-sul-testo. (2) **Toast sugli errori di validazione**: i messaggi di validazione input di 8
pagine ora sono toast (`Toasts.ShowError`) anziché banner; sistema/operazione restano banner. **Bug scoperto e
risolto** durante la verifica: *tutti* i toast (anche i "✓ Salvato") erano invisibili da sempre per una collisione
con la classe `.toast` di **Bootstrap** (`.toast:not(.show){display:none}`) → rinominate le classi del componente
in `.app-toast` (confermato in browser: `.app-toast` → display block, `.toast` → none). (3) **Indagine
`System.Private.Xml`** (dump dipendenze del trimmer): i ~1.4 MB sono trascinati da
`Newtonsoft.Json.Converters.XmlNodeConverter`; non eliminabile finché Newtonsoft è il serializzatore dei Model
(IL2104) → documentato, si libererà quando Supabase mollerà Newtonsoft. (4) **Filtro note server-side**: tentato
ma **postgrest-csharp 3.5.1 va in NullReferenceException** sul predicato con OR annidato → ripristinata la query
per-campagna (l'RLS filtra comunque le note per visibilità lato server, quindi nessuna perdita reale). Inoltre
la CSP consente `localhost` (ws/wss/http) in `connect-src` **solo** per gli strumenti dev (hot-reload + Browser
Link); su segnalazione del security-review automatico, un passo `sed` nel workflow di deploy lo **rimuove dalla
CSP in produzione** (dev-only via CI). Build 0/0, 122 test verdi; verifica locale (mostri/toast/note) + prod
(boot pulito, CSP pulita) ok.

**Bump GitHub Actions del deploy (2026-06-24).** Aggiornate le 5 action di `deploy.yml` alle ultime major
(verificate via API GitHub `releases/latest`, perché il web search dava versioni sbagliate): `checkout` v4→v7,
`setup-dotnet` v4→v5, `configure-pages` v4→v6, `upload-pages-artifact` v3→v5, `deploy-pages` v4→v5. Esce dal
runtime Node 20 in deprecazione. Non testabile in locale → verificato col run reale del push stesso (deploy
`success` + sito live che boota pulito). Con questo il backlog **autonomo code-side** è esaurito: il resto
richiede decisioni di prodotto (i18n, tema chiaro, markdown, wizard, AI) o risorse esterne (test RLS, vincoli DB).

**Test d'integrazione RLS (2026-06-24).** Le RLS sono applicate da Postgres → non testabili coi mock: serve un
DB vero. Montato lo **stack Supabase locale** (`supabase` CLI via scoop + Docker): schema+policy importati da
produzione con `supabase db dump` → `supabase/migrations/<ts>_remote_schema.sql` (12 tabelle, 45 policy, 5
funzioni), applicati allo stack locale (`supabase start`, config alleggerito ai soli db/auth/rest). Nuovo progetto
`Tests.Integration/` (xUnit + `Xunit.SkippableFact`, solo HttpClient): il fixture rileva lo stack (altrimenti
**auto-skip**), crea 2 utenti e semina dati idempotenti via `service_role`. **6 scenari RLS verdi**: isolamento
nota privata, visibilità condivisa al membro, gate non-membro, lettura propria, `combat_state` solo-master, niente
auto-promozione a master. `db pull` non funzionava (motore `pgdelta` → "no schema changes"), aggirato con
`db dump`. CI invariata (non esegue test). Istruzioni in `Tests.Integration/README.md`.

**Wizard di creazione scheda PG (2026-06-25).** Implementato il **wizard guidato di sola creazione** a 6 step
(Identità → Caratteristiche → Vitalità & combattimento → Competenze → Incantesimi → Riepilogo), accessibile via
`ViewMode.Wizard` in `Pages/Characters.razor`. Automazione intermedia: i bonus razza vengono applicati
automaticamente alle caratteristiche alla selezione della razza, e il dado vita viene pre-compilato alla scelta
della classe; PF massimi e tiri salvezza suggeriti con un tap (non forzati, il giocatore può sovrascrivere).
Helper puri e completamente testabili in `Services/CharacterWizardLogic.cs` (`FinalAbilityScores`,
`BuildHitDice`, `SuggestMaxHp`, `ParseSaveProficiencies`). Il salvataggio riusa `SaveFormAsync`/`CancelForm`
già esistenti: zero duplicazione logica. L'accordion `CharacterEditForm` resta **invariato** per la modifica di
PG esistenti. Zero impatto su DB/RLS: nessuna tabella nuova, nessuna policy modificata. 147 test verdi (suite
`DndCompanion.Tests`), build Release 0 warning / 0 errori. Verifica manuale end-to-end (scenario spec §9 in
locale a `https://localhost:7076`) affidata all'utente prima del push.
File toccati: `Services/CharacterWizardLogic.cs`, `Tests/CharacterWizardLogicTests.cs`,
`Shared/CharacterTabs/CharacterWizard.razor`, `Shared/CharacterTabs/CharacterWizard.razor.css`,
`Pages/Characters.razor`.

**Metodo di lavoro a due agenti + copertura test + hardening autorizzazione (2026-07-23).** Introdotta una
**regola di revisione a due agenti** (`.claude/agents/critico` + `conformità`, orchestrata da `CLAUDE.md`,
versionata): ogni modifica passa da un **gate a ciclo chiuso** — un agente caccia bug/regressioni, l'altro
verifica i pattern documentati del progetto — con loop "correggi → rilancia" fino a *nessun problema* e guardia
a 3 giri. La regola ha già ripagato al primo uso: durante il setup ha trovato una contraddizione logica nella
propria guardia e un pattern documentato infedele al codice (`internal static` dove 5/7 helper sono
`public static`), corretti prima del commit. Primo giro di lavoro sotto la regola: **+24 test** sugli helper
finora scoperti — `CharacterView` (mapping degli slot incantesimo livelli 1-9 con valori distinti per livello,
così un `case` mal-cablato che scrive/legge lo slot sbagliato fa fallire il test; più `FormatBonus`/`AriaBool`/
`OnKey`). E **irrobustito `AccessControl.CanEdit`**: la vecchia logica `ownerId == currentUserId` restituiva
`true` sul match degenere `null == null` / `"" == ""`, rendendo il gate client **più permissivo delle RLS** (una
riga di catalogo con `added_by` NULL risultava "modificabile" da un utente senza id, mentre la RLS la riserva al
solo master — spec RLS riga 51). Ora `CanEdit` esclude il caso degenere e il gate UX combacia col server; nessun
call-site reale regredisce (i proprietari hanno UUID reali, il master resta sempre abilitato). 172 test verdi,
build pulita. File toccati: `.claude/agents/*.md`, `CLAUDE.md`, `Tests/CharacterViewTests.cs`,
`Tests/AccessControlTests.cs`, `Services/AccessControl.cs`.

**Token per i colori con opacità — §6 (2026-07-23).** Ultimo residuo dei design token: i literali `rgba()`
con alpha (bordi/ombre oro semitrasparenti, ecc.) non erano tokenizzati. Un token hex (`--gold: #d4a574`) non
è usabile in `rgba(var(--gold), α)` perché `var()` dà la stringa hex, non i canali. Scelto — su brainstorm —
l'approccio **canali RGB affiancati** (`--gold-rgb: 212, 165, 116;` → `rgba(var(--gold-rgb), α)`) invece di
`color-mix()`/relative-color, per **supporto browser universale** e una **trasformazione meccanica e
visivamente invariante** (cruciale su centinaia di occorrenze). Aggiunti **19 canali `--X-rgb`** in `:root`
(app.css) e convertiti i ~363 literali `rgba(<tripla>, α)` del CSS di progetto (app.css + 20 `.razor.css`) via
uno script perl map-driven, **1:1** (il token vale esattamente la tripla → colore identico al bit). *Gotcha
evitato:* lo sweep aveva inizialmente toccato anche i `.css` **vendored di Bootstrap** (`wwwroot/lib/`) e le
copie in `bin/obj` — ripristinati; il perimetro è il solo CSS sorgente del progetto. Le 3 triple solo-Bootstrap
(blu/grigi) non sono diventate token (niente token morti). Nessun consolidamento delle sfumature quasi-duplicate
(sarebbe un cambio di colore → follow-up). Build 0/0, 187 test verdi, 0 literali `rgba(<numeri>)` residui.
Verifica a vista su `/_showroom` e push affidati all'utente. Spec:
`docs/superpowers/specs/2026-07-23-css-alpha-tokens-design.md`.

**Mappa UX dei flussi — analisi degli attriti (2026-07-25).** Su richiesta dell'utente ("è ancora tutto
un po' macchinoso, non sei molto guidato all'inserimento delle informazioni"), analisi statica di
**tutti** i flussi — onboarding, cataloghi, creazione PG, scheda in uso, combat, note — con l'utente di
riferimento fissato a **gruppo misto con novizi** e il bersaglio di regole confermato a **D&D 5e 2024**.
Nessun codice toccato: il deliverable è
`docs/superpowers/specs/2026-07-25-ux-mappa-flussi-analisi.md`, agganciato al backlog in
[DA-FARE.md](./DA-FARE.md) §8-bis.
*Il finding che riordina le priorità:* il modello dati implementa le regole **2014** (bonus di
caratteristica su `Race`) mentre il manuale disponibile e il gioco reale sono **2024**, dove quei bonus
vengono dal **background** — che nell'app è una stringa libera senza tabella (verificato sul PHB 2024
pag. 177). Conseguenza: **i dati 2024 non sono importabili nello schema attuale**, quindi la decisione
di modello viene prima di qualunque lavoro su cataloghi e wizard, o si rifà due volte. Il modello è già
un ibrido: `SpeciesTraits` e `HeroicInspiration` sono 2024, i bonus sono 2014.
*Gli altri numeri, contati sul markup:* ~670 campi da digitare per una campagna minima (81 solo per gli
incantesimi di un mago di livello 1); il wizard chiede 70 controlli di cui ~50 derivabili dalle regole
(i 9 campi degli slot incantesimo dipendono solo da classe e livello e sono interamente manuali).
*Collaterale trovato dalla revisione:* il PDF del manuale (~85 MB, materiale protetto) non era né
tracciato né ignorato — un `git add .` lo avrebbe committato in un repo pubblico. Aggiunto
`docs/*.pdf` a `.gitignore`.
Il gate a due agenti ha lavorato su un diff solo-`.md` scalando a coerenza e accuratezza del testo, ed è
andato a **tre giri** (guardia anti-loop scattata, chiusura con 4 finding minori residui riportati
all'utente e corretti dietro sua conferma). *Primo giro:* 2 conteggi sbagliati, 1 esempio che dimostrava
il contrario della tesi (il filtro classe degli incantesimi usa `Contains`, quindi "Bardo" *funziona*
perché contiene "Bard"; a fallire sono Mago, Chierico e Stregone), 3 riferimenti `file:riga` sfasati,
1 citazione troncata che invertiva la regola citata, più i due agganci mancanti al backlog.
*Secondo giro:* i pool di scelta abilità erano sbagliati — Guerriero "2 fra 8" e Ladro "4 fra 11" sono
i numeri **2014** (in 2024: 2 fra 9 e 4 fra 10, che perde Performance), mentre "Mago 2 fra 5" non è di
nessuna edizione (2014: 2 fra 6; 2024: 2 fra 7) — proprio l'errore di edizione contro cui il documento
mette in guardia;
più una contraddizione §6↔§8-bis su `SaveCharacterAsync`, un marcatore fuori legenda e tre rimandi
disallineati. Nel merito il secondo giro ha prodotto un cambio di stato nel backlog: la **cache dati
semi-statici di §5 rialzata da 🟢 a 🟡**. *Terzo giro:* solo rifiniture, due delle quali introdotte
dalle correzioni del giro precedente — motivo per cui l'ultima passata è stata rilanciata invece di
chiudere al buio.
File toccati: `docs/superpowers/specs/2026-07-25-ux-mappa-flussi-analisi.md` (nuovo), `docs/DA-FARE.md`,
`docs/DIARIO.md`, `.gitignore`.

**Design: modello 2024 + import dei dati (2026-07-25).** Primo dei quattro filoni aperti dalla mappa UX,
brainstormato con l'utente che ha posto due vincoli nuovi: **tutto in italiano** e, in vista della
pubblicazione, **i giocatori devono poter caricare i propri dati** come stiamo facendo noi con il 2024.
Il secondo vincolo risolve da solo il nodo del copyright che l'analisi aveva lasciato aperto: se ognuno
importa il proprio manuale, l'app non ridistribuisce nulla di protetto. Cinque decisioni: import via
**file di dati** (non estrazione PDF nell'app); pacchetto pubblico limitato al **SRD 5.2** (CC BY 4.0),
col contenuto non-SRD come file privato del gruppo; PG e cataloghi esistenti **congelati**; pacchetto
**completo a testo integrale**; dati del pacchetto come **file dell'app** in sola lettura, uniti lato
client ai cataloghi di campagna, mentre l'import dell'utente resta nel database.
*Perché il file invece dei cataloghi di sistema nel DB:* l'alternativa avrebbe richiesto di riaprire le
RLS di quattro tabelle — chiuse e testate a giugno — e di rendere nullabile una colonna `NOT NULL`,
lasciando comunque intatto il problema di §5 (il client riscarica tutto a ogni ingresso). Il file non
modifica nessuna policy esistente; aggiunge solo quelle della tabella nuova.
*Due verifiche sul codice hanno ridotto il preventivo:* `FinalAbilityScores` salva i punteggi **già
sommati**, quindi spostare la fonte dei bonus dalla specie al background non può cambiare i numeri di un
PG in gioco; `background` e `subclass` **esistono già** come colonne. Una terza verifica sembrava ridurlo
e invece era un errore, corretto dal gate: `characters.race`/`class` sono testo, ma da lì avevo concluso
che *nulla* fosse referenziato — vedi sotto.
*Esito sullo schema:* zero migrazioni di **dati**; **1 tabella nuova + 6 colonne additive su 5 tabelle
esistenti + 4 vincoli `UNIQUE` additivi**. L'unità di velocità (nell'analisi UX §4 riga 8 era data per bisognosa di una migrazione di
dati) si chiude senza toccare una riga, ma con una colonna `speed_unit` — dedurre l'unità dalla sorgente
non reggeva alle voci duplicate dal pacchetto né a quelle create a mano dopo il cambio.
*Divergenza registrata:* avevo consigliato di tradurre a scaglioni, per non riversare ~350 incantesimi in
un formato non ancora provato; l'utente ha scelto il pacchetto completo. Il rischio è neutralizzato
nell'ordine dei lavori dello spec (§12, punto 7: **campione SRD** che valida il formato sul campo prima
della traduzione di massa), che non riduce il risultato consegnato.
*Il gate a due agenti ha corretto il design nel merito*, non solo nella forma. Un **bloccante**:
`character_spells.spell_id` è una **chiave esterna reale** verso `spells(id)` — avevo generalizzato da
`race`/`class` (che sono testo) alla conclusione che nulla fosse referenziato, e con il pacchetto fuori dal
database nessun PG avrebbe potuto aggiungere un incantesimo alla propria lista. Risolto con la
**materializzazione su uso** (§4.4): solo gli incantesimi che un personaggio conosce davvero diventano
righe, non tutti e ~350. Un secondo errore verificato sul codice: il service worker precarica **ogni
`.json`** del manifest con un `cache.addAll` atomico (`offlineAssetsInclude`), quindi "non entra nel
bundle, si scarica su richiesta" era falso e un fetch fallito avrebbe fatto fallire l'installazione,
facendo perdere l'offline all'intera app → il pacchetto va escluso dal precache e l'offline è "dopo il
primo caricamento", non gratis. Altri quattro rilievi sostanziali: il piano d'import prometteva
aggiornamenti che le **RLS bloccano in silenzio** (ora `PackageImportPlan` riceve chi importa e usa
`AccessControl.CanEdit`); gli **id stabili** non avevano una colonna dove atterrare (aggiunta `source_id`);
la **ripartizione dei bonus** era modellata sul background invece che sul personaggio, ed era sparito il
**tetto di 20**; l'**unità di velocità** dedotta dalla sorgente si rompeva su "duplica e modifica" e sulle
voci nuove (ora colonna `speed_unit`). Infine il **filtro per classe degli incantesimi**, scritto su
stringhe inglesi, è entrato nel perimetro: con un catalogo italiano di ~350 voci non avrebbe trovato nulla,
senza errore visibile.
*Secondo giro — otto rilievi, quattro dei quali nati dalle correzioni del primo.* La materializzazione
appena introdotta creava una riga con `added_by` di chi la usava: sarebbe diventata una voce posseduta da
quel giocatore e, con il `CASCADE` della FK, cancellandola l'incantesimo sarebbe sparito dalle liste di
**tutti** i PG → le righe con `source_id` restano prive dei comandi, e il gate non è `CanEdit` ma la
presenza del `source_id` stesso. Il tie-break sui duplicati usava `created_at`, che **`spells` e
`monsters` non hanno** (solo `races`, `classes`, `characters`) → si ordina per `id`, arbitrario ma
deterministico ovunque. E la chiave di confronto prometteva accenti normalizzati: il progetto compila con
`InvariantGlobalization=true` (scelta di bundle di §2), quindi senza ICU `String.Normalize` **non fa
nulla e non lo dice** — verificato a runtime dall'agente — e su ~350 nomi come *Invisibilità* o *Oscurità*
avrebbe prodotto duplicati invece di riconoscerli; serve una piega esplicita, testabile. Il difetto
vecchio: mancava la **pagina Background**, senza la quale chi non importa un pacchetto avrebbe trovato un
elenco vuoto proprio nel passo del wizard che ora concede i bonus — il vicolo cieco che questo lavoro
dovrebbe chiudere.
*Terzo giro — la guardia anti-loop scatta con tre finding seri, di nuovo tutti generati dalla correzione
precedente e tutti sulla stessa decisione:* aver reso di sola lettura le righe con `source_id`. La regola
era troppo grossolana. (a) `source_id` finisce su **ogni** riga importata, quindi congelava anche il
pacchetto privato del gruppo e l'homebrew ripreso via export — cioè proprio ciò che l'import esiste per
portare — e contraddiceva §7, che quelle righe le aggiorna a ogni import. (b) §4.3 diceva che le righe
perdenti "restano visibili" mentre §4.4 diceva che il merge deduplica: incompatibili, e nella seconda
lettura `CharacterSpellJoin.WithCatalog` **scarta gli orfani in silenzio**, facendo sparire l'incantesimo
dalla scheda di chi puntava alla riga nascosta. (c) "duplica e modifica" collideva per nome con
l'originale e il tie-break fra due uuid era un sorteggio: l'utente avrebbe modificato la copia continuando
a vedere l'originale metà delle volte.
Riportato all'utente come previsto dalla guardia (nessun commit), che ha autorizzato un ciclo
supplementare. La correzione unica: il gate di sola lettura segue la **provenienza** — prefisso
`<id del pacchetto>/…`, verificabile anche offline — non la presenza del `source_id`; nel merge le righe di
database sono sempre tutte visibili e la chiave decide solo quale oscura la voce di pacchetto, con
precedenza a quella **senza** `source_id`; `UNIQUE (campaign_id, source_id)` toglie i doppioni alla radice;
e la schermata di import guadagna una **rimozione per provenienza**, senza la quale un import sbagliato
sarebbe irreversibile.
*Quinto ciclo — l'impianto passa, due effetti collaterali no.* Entrambi gli agenti hanno dichiarato
corretto l'impianto (gate per provenienza, righe di database sempre visibili, unicità sulla provenienza);
i due `SERIO` residui erano generati dalle correzioni del ciclo stesso. La **rimozione in blocco** appena
aggiunta riapriva il buco che §4.4 aveva chiuso — e `CanEdit` non faceva da freno, perché le righe
materializzate nascono con l'`added_by` di chi le ha usate → la provenienza del pacchetto dell'app è ora
**esclusa** dalla rimozione, e il resto passa da anteprima, conta dei PG toccati dal `CASCADE` e resoconto
parziale. Il **vincolo `UNIQUE`** appena introdotto trasformava il "riusa la riga esistente" in un errore
di sistema ogni volta che il client aveva una lista stantia (`Pages/Characters.razor` carica gli
incantesimi una volta sola: bastano due giocatori che preparano le schede la stessa sera) → l'inserimento
diventa un `Upsert` con `on_conflict` *(corretto in Fase 2: quell'`Upsert` non è realizzabile con
`postgrest-csharp 3.5.1` — v. la voce del 2026-07-29)*. Più quattro rifiniture.
*Nota di metodo:* le correzioni di quest'ultimo passaggio **non sono state verificate da un giro di gate**
— cinque cicli, ognuno con finding reali in parte generati dal precedente, con valore marginale calante e
ambito sempre più ristretto (dal rovesciamento dell'architettura al testo di quattro sezioni). Chiusura
decisa con l'utente: i difetti residui di uno spec vengono comunque ripresi scrivendo il piano, dove il
codice reale fa da controprova.
Spec in `docs/superpowers/specs/2026-07-25-modello-2024-import-dati-design.md`.

**Modello 2024 + import dei dati — Fase 1: leggere un pacchetto (2026-07-25).** Nove task in sequenza,
ognuno a comportamento invariato sul resto dell'app: `CatalogPackageParser` (Task 1) deserializza e valida
il pacchetto; `CatalogKey`/provenienza (Task 2) riconoscono una riga di pacchetto dal prefisso
`<id pacchetto>/…` dell'id — *nota:* il progetto compila con `InvariantGlobalization=true` (bundle, §2),
quindi `String.Normalize` **non piegherebbe gli accenti** (e non lo segnalerebbe): la chiave non usa
`String.Normalize`, piega gli accenti con una mappa scritta a mano (`CatalogKey.NormalizeName`, helper
`public static`), oltre a maiuscole e spazi, verificato a runtime; `CatalogMerge` (Task 3) unisce pacchetto e cataloghi di campagna
tenendo **sempre visibili** le righe di database, con la chiave che decide solo quale oscura la voce di
pacchetto (precedenza a chi non ha `source_id`); migrazione schema (Task 4) additiva pura — **1 tabella
nuova + 6 colonne + 4 `UNIQUE`**, zero migrazione di dati; model/repository/RLS dei background (Task 5);
`CatalogService` (Task 6) fa da unico punto di unione fra pacchetto (via `HttpClient`) e repository — le
pagine non compongono da sole, così le quattro pagine di catalogo della Fase 2 aggiungono un metodo invece
di duplicare la logica; esclusione del pacchetto dal precache del service worker (Task 7) — `cache.addAll`
in `onInstall` è atomico, quindi un pacchetto SRD nel manifest avrebbe fatto fallire l'installazione
dell'intera PWA a un solo fetch fallito; è ora messo in cache al primo uso da un ramo dedicato in
`onFetch`; pagina `Backgrounds.razor` (Task 8) come primo catalogo con voci di pacchetto in sola lettura,
modello da replicare nella Fase 2 per gli altri quattro; **unità di velocità esplicita nel form Razze**
(Task 9) — `FormValidation.ValidateRace` sceglie il limite (0–120 piedi o 0–36 metri) in base a
`r.SpeedUnit` e lo cita nel messaggio, il form aggiunge un `<select>` (`aria-label="Unità di velocità"`)
legato a `editDraft.SpeedUnit` e il `max` dell'input numerico segue l'unità scelta, altrimenti il browser
lascerebbe digitare fino a 120 in modalità metri e l'utente scoprirebbe il limite solo dal toast di errore.
*Perché ora e in quest'ordine:* il "duplica e modifica" della Fase 2 creerà righe di razza in **metri**
(dal pacchetto SRD), e senza il Task 9 il form le avrebbe mostrate con l'aiuto e il tetto pensati per i
piedi — l'ambiguità che l'intero lavoro sull'unità (introdotta nel design, §8-bis) doveva chiudere,
reintrodotta proprio dall'ultimo pezzo mancante.
*Cosa NON fa ancora questa fase:* nessuna delle quattro pagine di catalogo esistenti (Razze, Classi,
Incantesimi, Mostri) marca le voci di pacchetto né offre "duplica e modifica" — in Fase 1 non esistono
ancora righe con provenienza (nessun import, nessun pacchetto pubblicato), quindi non c'è nulla da
marcare; la logica (`CatalogMerge`, `CatalogKey.IsFromAppPackage`) è già pronta e testata. Fase 2 (import
ed export, con `PackageImportPlan` e il gate dei permessi) e Fase 3 (contenuto 2024 e wizard) restano
aperte — piano in `docs/superpowers/plans/2026-07-25-modello-2024-import-dati-fase-1.md`.
*Seconda passata (2026-07-25, revisione d'insieme):* nove correzioni circoscritte emerse guardando i nove
task nel loro insieme, non visibili dalle revisioni per singolo task. Le più sostanziali: `NormalizeLists`
proteggeva solo le sei sezioni di primo livello, non le liste annidate dentro le voci (una sezione tipo
`"abilityScores": null` in un background superava il parser e poi faceva lanciare `ArgumentNullException`
fuori dal `try/catch` del rendering); il trim di id/nomi si è spostato al confine — il parser, non
`CatalogMerge` — perché è lì che nasce l'asimmetria con `CatalogKey.For` (che già fa il trim del `sourceId`
letto dal database); `Check` ora rileva id duplicati nella stessa sezione del pacchetto, che il vincolo
`UNIQUE (campaign_id, source_id)` del database avrebbe altrimenti fatto fallire a metà import; `speed_unit`
accetta solo `'m'`/`'ft'` anche a livello DB (`CHECK`, aggiunto alla stessa migrazione del Task 4); il badge
"Dal manuale" e il bordo della card in `Backgrounds.razor` seguivano `entry.IsPackage` invece della stessa
condizione che sopprime i comandi di modifica/cancellazione, lasciando una riga di database non modificabile
senza marcatura; la ricerca della stessa pagina ora usa `CatalogKey.NormalizeName` su entrambi i lati del
confronto invece di un confronto ordinale, coerente con la normalizzazione che questa fase ha costruito;
`CharacterWizard.razor`/`CharacterEditForm.razor` stampavano `@SelectedRace.Speed` senza unità — dal Task 9
una razza può essere in metri, quindi ora citano `FormValidation.IsMetric(...SpeedUnit)` accanto al numero,
come già fa `Pages/Races.razor`. Più due correzioni al testo di questa stessa voce (sopra): il nome della
classe (`CatalogKey`, non `CatalogCompareKey`) e il comportamento reale della piega accenti (piega gli
accenti con una mappa scritta a mano — non "solo maiuscole e spazi" come diceva prima).
285 test unitari verdi (279 + 6 di regressione della seconda passata),
11 di integrazione verdi contro lo stack Supabase locale,
build Release 0 warning / 0 errori.

**"Campaign hopping" dell'autore: portata rivista e decisione (2026-07-25).** Scrivendo le policy di
`backgrounds` (Task 4) era emerso che la `WITH CHECK` di `*_update`, ricalcata fedelmente da `races_update`,
non impedisce all'**autore** di una riga di riassegnarne il `campaign_id` verso una campagna di cui non è
membro: il ramo `added_by = auth.uid()` resta vero perché quella colonna non cambia con lo spostamento. Non
corretta lì (avrebbe significato divergere da policy multiple già in produzione, fuori mandato del task) ma
documentata. *La stima di gravità iniziale — "bassa, vandalismo mirato" — è stata poi **smentita da una
verifica sulle policy**, ed è il motivo per cui vale la pena registrarla.* Due cose erano sbagliate. La
prima: mancava una tabella all'elenco — **`notes`**, che ha la stessa forma di policy (`notes_update` è
`USING/WITH CHECK (owner_id = auth.uid())`), quindi le tabelle colpite sono **sette e non sei**. La seconda:
l'effetto era stato valutato solo sui cataloghi, e **non è uniforme**. Lì l'autore perde la vista ma non il
controllo (può ancora aggiornare e cancellare per id). Su `characters` — che nell'elenco c'era già — non
perde nemmeno l'accesso, perché `characters_select` ha il ramo `owner_id`, e il PG compare nell'elenco della
campagna bersaglio. Su `notes`, con `is_shared = true`, la nota si riversa nelle note condivise altrui:
**iniezione persistente di contenuto**, non perdita. Cade anche la barriera dell'uuid: un ex-membro lo conserva (i suoi PG e le sue
note restano leggibili via `owner_id`), e `find_campaign_by_invite_code` è `SECURITY DEFINER` concessa ad
`anon`, quindi chiunque abbia visto un codice invito lo ottiene senza unirsi. *Decisione:* la voce resta 🟡
in `DA-FARE` §1 — non 🟢, perché §1 è il gate di pubblicazione ed è l'unico varco di scrittura **fra
campagne** noto — e la chiusura è una **migrazione autonoma** col suo giro di test RLS, da fare in
prossimità della Fase 2 e comunque **prima di aprire l'app al pubblico**. Nella stessa occasione va valutato
il caso gemello, che non richiede alcuno spostamento: un ex-membro conserva `owner_id` sulle righe rimaste
in campagna, quindi può continuare a riscrivere una propria nota condivisa anche dopo essere stato rimosso.

**Modello 2024 + import dei dati — Fase 2: import ed export (2026-07-27 → 2026-07-29).** Undici task, zero
migrazioni: questa fase non tocca schema né policy, usa quelle che la Fase 1 aveva già messo in piedi.
L'app ora **legge un file di pacchetto dentro una campagna** con anteprima e resoconto, **riesporta** i
propri dati, **rimuove un import per provenienza**, e materializza nel database i soli incantesimi di
pacchetto che un personaggio usa davvero. I quattro cataloghi che erano rimasti indietro (Razze, Classi,
Incantesimi, Mostri) marcano le voci di pacchetto e offrono "duplica e modifica", replicando il modello
di `Backgrounds.razor`.

*Quattro decisioni che divergono dallo spec o lo precisano, e il perché:*

**Niente `Upsert`, contro §4.4 dello spec — misurato, non congetturato.** Intercettando le richieste reali
di `postgrest-csharp 3.5.1`: `Insert` rispetta `[PrimaryKey("id", false)]` ed esclude la chiave, **`Upsert`
la serializza sempre**, e con `id uuid NOT NULL` un `""` è `invalid input syntax for type uuid` — HTTP 400
su *ogni* scrittura. (L'unico `Upsert` in produzione, `CombatStateRepository`, non lo incontra perché il suo
Model ha `[PrimaryKey("campaign_id", true)]`, sempre valorizzata.) Valorizzare l'`id` a mano non è un
rimedio: su conflitto il `DO UPDATE` riscriverebbe la **chiave primaria** della riga, e
`character_spells_spell_id_fkey` non è `ON UPDATE CASCADE`. Quindi due percorsi distinti — creazioni in
blocco con `Insert`, aggiornamenti riga per riga — e per la materializzazione un leggi-poi-inserisci con
**rilettura sul conflitto**, che ottiene lo stesso risultato che §4.4 voleva dall'`Upsert`. Le tre
occorrenze nello spec e in `DA-FARE` sono state corrette: uno spec che descrive un'implementazione
impossibile è peggio di uno spec incompleto.

**Gli aggiornamenti fondono, non sovrascrivono.** Un `Update` invia tutte le colonne del Model e il formato
di scambio non le copre tutte: un file senza `languages`, senza i sei bonus di specie, senza le sei
caratteristiche di un mostro le azzererebbe **tutte al primo reimport, senza che il conteggio delle righe
cambi di una unità**. L'esecuzione parte quindi dalla riga esistente e vi applica sopra i soli campi che il
pacchetto trasporta; **tutto il resto resta com'era** — `added_by` compreso, che altrimenti un reimport del
master trasferirebbe a sé stesso, togliendo in silenzio al giocatore la modifica delle voci che aveva caricato. Nello stesso spirito
`PackageSpell.Level` e `PackageMonster.ArmorClass` sono diventati `int?`: con `int` un campo assente arriva
come `0`, e `0` è un valore **valido** sia per il livello (i trucchetti) sia per la classe armatura — un
incantesimo di livello 3 sarebbe diventato un trucchetto in silenzio.

**`SkippedLocalWins`.** Lo spec §7 elencava «saltato» come un esito solo; sono due, con cause e rimedi
diversi. Se la corrispondenza è **solo per nome** (riga senza `source_id`) la riga è dell'utente — magari
nata da "duplica e modifica" — e §6 dice che vince lei. Senza questo esito quella voce sarebbe stata
marcata `Create` e l'inserimento in blocco avrebbe creato un **doppione** accanto alla riga personalizzata:
`UNIQUE (campaign_id, source_id)` non lo impedisce, perché un `source_id` nullo non collide con nulla.

**Nessun `LIKE` costruito con testo dell'utente.** La rimozione per provenienza riceve un prefisso digitato:
in SQL `LIKE`, `_` vale "un carattere qualsiasi". Con `source_id LIKE 'srd-2024-i_/%'` la guardia «il
pacchetto dell'app non è rimovibile» — un confronto di prefisso esatto — direbbe di sì mentre la `DELETE`
colpirebbe proprio le voci del manuale, incluse quelle materializzate (che nascono con l'`added_by` del
giocatore che le ha usate, quindi `CanEdit` non frena), portandosi via per `CASCADE` gli incantesimi dalle
schede. Il filtro per provenienza si fa quindi **in memoria** (`CatalogRemovalPlan`, helper puro con
`StartsWith(Ordinal)`) e la cancellazione per **elenco di id**: l'insieme cancellato è esattamente quello
che l'anteprima ha mostrato, e l'anteprima dice anche quanti personaggi perderanno un incantesimo.

*Un'asimmetria che ha richiesto due giri di revisione per emergere:* import e rimozione scrivono sulle
**stesse cinque tabelle**, quindi ognuna invalida l'anteprima dell'altra. Senza riallineamento, un
"Conferma import" dopo una rimozione ripercorreva il ramo `Update` su righe cancellate — PostgREST accetta
l'`UPDATE` toccando zero righe e il resoconto ne dava la colpa al server. Nella stessa famiglia: il `Delete`
di questa libreria non riporta le righe toccate e un `Delete` bloccato dalla RLS "riesce" a vuoto
(`DA-FARE` §3), quindi il resoconto della rimozione **riconta gli id congelati ancora presenti** invece di
assumere l'esito, e il toast arriva dopo quel conteggio, non prima.

*Export:* l'id del file prodotto è `campagna-<nome normalizzato>`, **mai** `srd-2024-it` — dare al proprio
file l'id del pacchetto dell'app renderebbe le proprie voci di sola lettura al reimport. Le righe che hanno
già un `source_id` lo conservano — è ciò che permette a un reimport di aggiornarle invece di duplicarle —
con un'eccezione: le provenienze `srd-2024-it/…` (righe materializzate) **degradano** a
`<id campagna>/<slug>`, perché conservarle propagherebbe la sola-lettura del manuale dentro campagne che non
hanno mai importato nulla di ufficiale. Tutte le righe che non conservano una provenienza prendono uno slug
dal nome, e gli slug che collidono ricevono un **suffisso progressivo**: nessuna tabella vieta due omonimi,
«Palla di Fuoco» e «palla di fuoco» normalizzano allo stesso slug, e il parser di Fase 1 rifiuta l'**intero**
pacchetto — non la singola voce — se trova un identificatore ripetuto.

*Conseguenze note, da tenere presenti:* i **mostri di pacchetto non compaiono** nel pannello "Importa
mostri" del tracker finché non vengono duplicati in campagna; un file **esportato perde la provenienza delle
righe materializzate** dal manuale, che altrove saranno normali contenuti di campagna; e la
**virtualizzazione delle liste** (`DA-FARE` §5, scartata "sotto le ~50 voci") va rivalutata **già ora**: non
serve attendere il pacchetto SRD della Fase 3, perché l'import esiste in codice e un file dell'utente supera
la soglia da subito — è il trigger che quella decisione aveva dichiarato.

387 test unitari verdi, build 0 warning / 0 errori. Piano in
`docs/superpowers/plans/2026-07-27-modello-2024-import-dati-fase-2.md`; resta la **Fase 3** (contenuto SRD
tradotto e wizard 2024). *Nota di metodo sull'ultimo task:* la rimozione per provenienza ha richiesto
quattro giri di gate — i due `SERIO` (resoconto che dava per fallita una rimozione riuscita; anteprima di
import non riallineata) sono emersi solo al secondo. Il piano prescriveva la logica inline nel blocco
`@code`: è stata estratta in `Services/CatalogRemovalPlan.cs` con 26 test, perché `CLAUDE.md` vuole la
logica di dominio in helper puri e lì vivono gli invarianti di sicurezza dell'operazione più distruttiva
dell'app. Non eseguita la **verifica manuale end-to-end** dello Step 4 (pacchetto di prova + secondo account
non-master), che resta da fare prima del deploy.


## Mobile-first totale (2026-07-30)

L'app nasce «mobile-first» e il CSS lo era davvero — base per il telefono, un solo blocco
`@media (min-width: 641px)` per pagina, nessuna `max-width` usata come breakpoint. L'ipotesi di
partenza («è desktop-first, va convertito») è caduta alla prima misura. Il lavoro è stato quindi
un altro: **chiudere i punti in cui il mobile-first era dichiarato ma non realizzato**. Analisi e
misure in `docs/superpowers/specs/2026-07-30-mobile-first-design.md`.

**Il codice per il telefono c'era ma non girava.** Il FAB usava `env(safe-area-inset-bottom)` con
tanto di commento sul notch, ma il `<meta viewport>` non aveva `viewport-fit=cover`: senza, iOS
risolve **tutte** le `env()` a zero. Una parola aggiunta, e insieme a essa il resto del lavoro sulle
tacche (barra di navigazione, controlli sticky del Combat, banner PWA). Nello stesso registro:
`lang="en"` su un'app interamente in italiano, e 14 `100vh` — 13 regole CSS più quella
dello `<style>` critico di `index.html`, e sul telefono `vh` conta anche la barra URL che si
ritrae — diventati `100dvh`.

**Il layout era sbilanciato solo sul telefono.** `article` aveva `padding-left: 2rem` e
`padding-right: 1.5rem`, mentre tutti e dieci i container di pagina lo annullano con lo stesso
`margin: … -1.5rem`: restavano 8px di scarto a sinistra e zero a destra. Sopra i 641px il blocco di
enhancement correggeva, sotto no — l'esatto contrario del mobile-first. Ora il padding è simmetrico
e le due righe di `margin` dentro i dieci blocchi `≥641px` sono sparite perché ridondanti.

**Bootstrap rimosso.** L'app ne usava **due classi**: `btn` in Login (dove `.btn` è comunque
ridefinita per intero nel CSS della pagina) e `px-4` in MainLayout, già inerte per via di un
`!important`. In cambio, il service worker precacheva **per estensione** tutti i 22 file css/js di
`wwwroot/lib` — grid, utilities, reboot, le varianti RTL, i bundle JS: **2,4 MB** scaricati sulla
rete del telefono, per confronto con i 3,45 MB dell'intero `_framework` compresso. Quello che l'app
usava davvero era il solo *reboot*, ora replicato **alla lettera** in testa a `css/app.css` con i
valori `--bs-*` risolti: `box-sizing` universale, `line-height: 1.5`, la scala fluida dei titoli,
`font: inherit` sui controlli. Due regole erano invisibili ma indispensabili —
`-webkit-tap-highlight-color: transparent` e `-webkit-text-size-adjust: 100%`: senza replicarle, il
flash grigio di sistema al tocco sarebbe comparso **a causa** della rimozione. Verificato a runtime
che body, titoli, paragrafi e controlli rendano identici a prima.

**Una barra di navigazione, finalmente** (chiude la voce §8-bis del backlog). Prima ogni
spostamento passava dalla Home: due tap e un ricaricamento dati per cambiare sezione. Cinque voci e
non nove — nove darebbero celle da 43px, sotto la misura del dito — con le quattro sezioni di uso
continuo al tavolo (Personaggi, Iniziativa, Incantesimi, Appunti) più la Home, che resta l'accesso a
tutto il resto e al cambio campagna. Compare solo con una campagna attiva, e poiché il cambio
campagna avviene in una **pagina** mentre la barra vive nel **layout**, `CampaignStateService`
espone ora un evento `ActiveCampaignChanged`, inoltrato da `CurrentUserService` perché la facade
resti l'unico punto d'ingresso. Le misure della barra sono token (`--bottom-nav-height`,
`--bottom-nav-space`) usati da FAB, toast, banner PWA, padding di fondo del layout e controlli
sticky del Combat. `--bottom-nav-space` si riduce alla **sola safe-area quando la barra non c'è**
— gli overlay devono restare fuori dalla home indicator comunque, e con `viewport-fit=cover` la
pagina ci si estende sotto — e vi somma l'altezza della barra solo quando c'è davvero, altrimenti
quegli elementi resterebbero sollevati sul nulla: caso raggiungibile dagli shortcut del manifest,
che aprono una sezione direttamente.

*La lezione del giro di revisione:* la barra sta nel layout, **fuori** dall'`<ErrorBoundary>` che
protegge le pagine, e il suo caricamento tocca la rete (identità e ruolo nella campagna). All'avvio
offline — scenario di prima classe per una PWA con precache completo — l'eccezione sarebbe arrivata
alla barra d'errore di Blazor e la navigazione sarebbe rimasta assente **per tutta la sessione**.
Ora c'è un try/catch che degrada in silenzio, e le sottoscrizioni agli eventi si registrano *prima*
dell'await: l'evento vive su un Singleton, quindi un componente disposto durante l'attesa avrebbe
lasciato l'iscrizione appesa per sempre.

*Stessa radice, secondo effetto — e questo vale per tutta l'app, non per la sola barra:*
`CampaignStateService.InitializeAsync` alzava un flag booleano **prima** degli await. Un errore di
rete durante il caricamento del ruolo lasciava quindi `ActiveCampaignRole` nullo **per l'intera
sessione**, e ogni chiamata successiva era un no-op: il master vedeva l'interfaccia da giocatore,
in silenzio e senza rimedio se non ricaricare. Fino a ieri il caso era improbabile perché il primo
chiamante era la Home, che ci arriva dopo quattro await; con la barra nel layout il primo chiamante
è lei, e arriva subito. Ora il servizio si tiene il `Task` invece del flag: se fallisce lo scarta e
il chiamante successivo riprova. Di conseguenza `Combat.razor` — l'unica delle nove pagine che chiamano
`EnsureLoadedAsync` a non avere un `try/catch` attorno — ne ha ricevuto uno: un'eccezione lì avrebbe
sostituito il tracker con il fallback dell'ErrorBoundary, che offre "Ripara e ricarica", cioè
svuotare le cache proprio mentre si è senza rete.

**Le dita.** `.db-error-dismiss` misurava 27×22 — sotto il minimo WCAG 2.2 AA di 24×24, l'unico
controllo dell'app a esserlo. Gli altri erano conformi ma scomodi: i ± dei PF in combattimento, il
controllo più toccato dell'app, stavano a 34px. Pulsanti portati a 44px; i pallini tengono il
pattern del progetto (`::after` trasparente che allarga l'area senza toccare il layout); i
`<input type="checkbox">` non accettano nessuna delle due strade — sono elementi rimpiazzati, e a
44px il quadratino nativo si deforma — e restano a 24px.

*Il difetto che solo la misura ha rivelato:* i tap target dei pallini **si accavallavano**. Sette
coppie su `sc-dot` (area 36px contro centri distanti 26px), e per costruzione anche i tiri salvezza
morte (36 contro 22) e gli slot incantesimo (32 contro 18): toccare il bordo di un pallino attivava
il vicino. Un'area allargata che sconfina su quella accanto non allarga il bersaglio, **lo sposta**.
La regola adottata: l'area non supera mai il passo fra i centri, e dove serve è il passo ad
aumentare. Su `sc-dot` l'area è asimmetrica — 44×28, larga quanto il dito dove c'è spazio, alta
quanto la riga dove non ce n'è.

**iOS non deve più ingrandire la pagina.** Cinque campi stavano sotto i 16px (`spell-picker-input`,
`notes-textarea`, i campi delle monete, `attunement-input`, `sr-input`): sotto quella soglia Safari
ingrandisce al focus e **non torna indietro all'uscita**. Tutti a 1rem.

**PWA installabile come si deve.** Il manifest aveva solo nome e icone: ora ha `scope`, `lang`,
`description`, `categories`, tre `shortcuts` (Scheda, Combat, Appunti) e soprattutto un'icona
`purpose: "maskable"` generata apposta — il d20 dentro il cerchio di sicurezza dell'80%, su fondo
del tema — perché senza, Android incastra l'icona in un cerchietto bianco.

400 test unitari verdi (13 nuovi su `BottomNavRoutes.IsActive`, la logica della voce attiva estratta
dal componente secondo la regola degli helper puri), build Release 0 warning / 0 errori. Verificato a
runtime su viewport 390×844 e 320×568: nessuno scroll orizzontale, nessun campo sotto i 16px di
carattere, zero sovrapposizioni fra i tap target e nessun controllo sotto i 24×24 di WCAG 2.2 AA —
con una sola eccezione dichiarata, il campo del punteggio nel wizard, che a 320px si stringe a 23px
per non far sconfinare i due pulsanti da 44 che ha accanto. **Resta all'utente** la verifica a
vista da telefono e loggato: l'accesso è OAuth Google e va completato da una persona, quindi in
questa sessione le pagine raggiungibili sono state `/_showroom` e `/login` — per gli altri
componenti (barra di navigazione, riga del tracker) è stato iniettato il markup reale con le loro
classi di scope, così da misurare le regole vere e non una loro imitazione.


## Bottom-nav che si distorceva allo scroll + audit mobile-first ulteriore (2026-07-31)

L'utente ha segnalato che la barra di navigazione «si distorce» muovendosi, riprodotto su
Android/Chrome sia nel browser sia nella PWA installata (standalone). Prima ipotesi scartata:
`100vh`. Era già stata convertita a `100dvh` ovunque il 2026-07-30 proprio per il motivo opposto
(su mobile `vh` non segue la barra URL che si ritrae) — non c'era altro `vh` da correggere.
Scartate anche `viewport-fit=cover` (già presente) e `overscroll-behavior` (già `contain` su
`html`): nessuna delle tre regressioni "da manuale" era in gioco.

La causa più probabile, verificata nel codice: `.bottom-nav` è `position: fixed` con un
gradiente e una `box-shadow` sfocata, senza layer di composizione proprio. Su Chrome Android la
barra URL dinamica (e, in standalone, la barra di sistema/gesture) sposta il viewport a ogni
frame durante lo scroll; senza `transform`/`will-change`, il browser deve ridisegnare gradiente e
ombra sul thread principale a ogni ricollocamento invece di ricomporre un layer GPU già
rasterizzato — il tremolio segnalato. Aggiunto `transform: translateZ(0)` a `.bottom-nav`
(`Shared/BottomNav.razor.css`), con la motivazione del costo commentata sul posto (un layer GPU in
più, trascurabile per un elemento piccolo e sempre visibile a campagna attiva): buona prassi per
le scelte non ovvie, seguita dal resto del CSS di progetto anche se CLAUDE.md non la impone come
regola esplicita. **Non verificabile empiricamente in questa sessione**: Chrome
DevTools in emulazione mobile non riproduce la barra URL dinamica, quindi resta da confermare a
vista su un Android reale.

Verificando la coerenza dei token `--bottom-nav-*` (uno dei sospetti elencati) è emerso un
secondo difetto, minore ma reale: `.bottom-nav` usa `box-sizing: content-box` apposta (per tenere
l'altezza fissa e sommarci sopra la safe-area variabile), ma il suo `border-top` di 1px si
aggiunge all'altezza resa e nessuno dei token lo contava — `--bottom-nav-space` restava 1px più
corto della barra vera, quindi FAB, toast, banner PWA e i controlli sticky del Combat sedevano 1px
sopra il bordo reale invece che a filo. Nuovo token `--bottom-nav-border-width` (app.css),
referenziato sia nel `border-top` di `BottomNav.razor.css` sia nel calcolo di
`--bottom-nav-space`, così i due non possono più andare fuori sincrono.

**Audit mobile-first delle pagine catalogo.** `.form-grid` (Backgrounds/Classes/Combat/Monsters/
Races/Spells) era a due colonne **anche sotto i 641px** — a 320px, campi da ~137px l'uno, contro
il pattern mobile-first che il resto di ognuno di quei file già segue (base per il telefono, un
solo blocco `@media (min-width: 641px)` di enhancement). Ora la base è a colonna singola e le due
colonne tornano nel blocco desktop già esistente. `.chip` (i pulsanti di filtro — CR/tipo in
Monsters, livello/classe in Spells) misurava ~38px di altezza, sotto la soglia del dito e non
compreso nel giro di tap target del 2026-07-30: portato a 44px con `min-height` + flex di
centratura, senza toccare `min-width`/aspetto.

400 test verdi, build Release 0 warning/0 errori invariati (nessuna modifica C#, solo CSS).

## Metodo: ramo unico `main` (2026-07-30)

Deciso di lavorare su **un solo ramo**, senza feature branch né pull request. La ragione per cui la
scelta va scritta e non solo applicata è la sua conseguenza: `deploy.yml` parte a ogni push su
`main` e non ha approvazioni, quindi **spingere è rilasciare**. Non esiste uno stadio intermedio in
cui guardare il risultato prima degli utenti.

La regola in `CLAUDE.md` mette perciò nero su bianco ciò che il ramo unico rende critico, e che la
revisione a due agenti ha fatto emergere punto per punto: il push resta sempre su richiesta
esplicita (il commit no, quello segue il gate); il verde della CI non dice che l'app funziona,
perché il workflow non esegue i test e `dotnet build` non attiva il trimming che rompe i costruttori
usati via reflection; il deploy rilascia **solo il client**, mentre le migrazioni Supabase si
applicano a mano e devono restare compatibili col client già live; l'aggiornamento PWA è on-demand,
quindi dopo il push gli utenti restano sulla versione in cache — e i loro dati passano dallo stesso
database; il rollback è `git revert` più push, mai un force-push sul ramo di rilascio.

## Home come guida di primo avvio (2026-07-31)

La Home esponeva nove destinazioni (otto card + Combattimento) tutte allo stesso peso, senza dire a
chi non conosce l'app **da dove iniziare né in che ordine**: esattamente il sintomo descritto in
`docs/superpowers/specs/2026-07-25-ux-mappa-flussi-analisi.md` §2.1 ("la campagna appena creata è un
guscio vuoto... nessuna delle sette card avverte che dietro non c'è nulla").

**Percorso guidato "Primi passi".** Quattro passi in ordine (tre per il giocatore, che salta
l'invito): creare/entrare in campagna → creare il proprio personaggio (rotta `characters/nuovo`,
il wizard guidato, realizzato nella stessa sessione — v. la sezione più sotto) → il master invita i compagni (copia il
codice invito negli appunti) → aprire il tracker iniziativa (`combat`). Ogni passo mostra una
spunta se già fatto. La logica di quali passi mostrare e quali risultano completati, dato lo stato
(campagna attiva, personaggio proprio, altri membri, ruolo), vive in
`Services/HomeOnboardingLogic.cs` — statico, puro, testato in
`Tests/HomeOnboardingLogicTests.cs` — non nel `.razor`, per lo stesso motivo di
`BottomNavRoutes.IsActive`: sono rami di decisione che meritano test propri.

L'ultimo passo (tracker iniziativa) non ha un segnale di completamento persistito — aprirlo non
lascia traccia in nessuna tabella, e una colonna nuova solo per questo sarebbe fuori scopo — quindi
resta sempre "da fare" nel modello. `IsSetupComplete` lo esclude apposta dal conteggio: altrimenti
il percorso guidato non sparirebbe mai, restando visibile anche a campagna avviata da mesi. Con
questo, la guida compare al primo avvio e si toglie da sola una volta che campagna, personaggio e
(per il master) compagni ci sono.

**Sfoltimento.** Personaggi, Iniziativa, Incantesimi e Appunti sono raggiungibili dalla barra di
navigazione inferiore (`Shared/BottomNav.razor`, aggiunta il 2026-07-30): le tre card
corrispondenti — e il pulsante Combattimento, cioè quattro delle nove destinazioni originarie — sono state tolte
dalla Home perché duplicate. Restano le cinque voci non ospitate dalla barra (Mostri, Classi, Razze,
Background, Dati), in una lista "Cataloghi" volutamente più dimessa delle vecchie card quadrate: sono
materiale di consultazione, non il percorso per iniziare a giocare.

**Un dettaglio di stato da non perdere.** Il selettore di campagna attiva non è disabilitato durante
il caricamento (già così prima di questo intervento): un cambio rapido di campagna può avviare due
caricamenti concorrenti dei dati che alimentano il percorso guidato, e l'ordine di arrivo delle
risposte di rete non è garantito. Il caricamento verifica quindi, appena prima di scrivere lo stato,
che la campagna attiva non sia nel frattempo cambiata — altrimenti scarta il risultato — per non
mostrare passi "fatto/non fatto" riferiti alla campagna sbagliata.

414 test unitari verdi (14 nuovi su `HomeOnboardingLogic`), build 0 warning / 0 errori. File toccati:
`Pages/Home.razor`, `Pages/Home.razor.css`, `Services/HomeOnboardingLogic.cs`,
`Tests/HomeOnboardingLogicTests.cs`.

---

## Privacy dei personaggi e menu Party (2026-07-31)

**Il difetto era in una riga di SQL, non nella UI.** Alcuni giocatori vedevano il personaggio di un
altro nella schermata Personaggi. La causa: `characters_select` diceva
`owner_id = auth.uid() OR is_campaign_member(campaign_id)` — cioè *qualunque* membro della campagna
poteva leggere *qualunque* personaggio. Non era un difetto di visualizzazione: la stessa lettura era
possibile via REST diretto, quindi filtrare lato client non avrebbe risolto nulla.

**Nuova regola:** un giocatore vede solo i propri PG, il master li vede tutti
(`owner_id = auth.uid() OR is_campaign_master(campaign_id)`).

**Il Party non è una SELECT sulla tabella.** Le stat dei compagni servono comunque, ma non la scheda
intera: la RPC `get_party_overview` (SECURITY DEFINER, `search_path` fisso, `EXECUTE` revocato a
`public`/`anon` e concesso ad `authenticated`) restituisce **solo** nome, specie, classe, livello, CA,
PF, percezione passiva, velocità e nickname del proprietario, e solo se il chiamante è membro della
campagna. La percezione passiva è calcolata **dentro** la funzione proprio per non dover restituire
saggezza e competenze grezze al client.

**La restrizione si propaga da sola, ed è una buona notizia.** Le policy di `inventory` e
`character_spells` leggono `characters` con un `EXISTS`; in PostgreSQL la RLS si applica anche alle
subquery dentro le policy, quindi quella `SELECT 1 FROM characters` subisce a sua volta la nuova
`characters_select`. Effetto: un giocatore non vede più nemmeno inventario e incantesimi dei PG
altrui, il master continua a vederli. Nessuna modifica necessaria a quelle policy — verificato riga
per riga sullo schema, non assunto.

**Gotcha di libreria che sarebbe esploso solo in produzione.** `Rpc<T>` di postgrest-csharp 3.5.1
deserializza con `JsonConvert.DeserializeObject<T>` **senza** passare le `SerializerSettings` del
client, quindi senza il contract resolver che altrove traduce `[Column("snake_case")]`. Con
l'attributo `Column`, `PartyMember` si sarebbe popolato di campi vuoti — build verde, test verdi,
pagina vuota online. La mappatura usa perciò `[JsonProperty]` di Newtonsoft.

⚠️ **La migrazione `20260731000000_party_visibility.sql` va applicata a mano al progetto hosted
PRIMA del push**: il deploy rilascia solo il client. Nell'ordine inverso, la pagina Party chiamerebbe
una funzione inesistente.

---

## Wizard di creazione guidato (2026-07-31)

**Un wizard c'era già, e non bastava.** `Shared/CharacterTabs/CharacterWizard.razor` riceveva
`Classes` e `Races` come parametri dalla pagina, che li caricava dai **soli cataloghi di campagna**:
righe che qualcuno deve aver creato a mano. Da lì l'attrito segnalato — un principiante deve capire
da sé che prima si creano classe, razza e background nelle pagine catalogo, e solo dopo il
personaggio.

**Ora il wizard pesca da `ICatalogService`**, che unisce le righe di campagna con le voci del
pacchetto SRD: alla prima apertura le scelte ci sono già. È diventato una **pagina**
(`/characters/nuovo`) invece di un componente: sei passi, uno per schermata, e un indirizzo proprio —
la creazione non è più uno stato nascosto dentro la lista dei personaggi. Il vecchio componente è
stato rimosso, non affiancato.

⚠️ I **passi** non stanno nell'URL, che resta uno solo: il tasto indietro del telefono esce dal
wizard invece di tornare al passo precedente, e per giunta scavalca la conferma «Uscire senza
salvare?». Metterli in query string è la correzione vera — annotata in `DA-FARE`, non fatta qui.

**Difetto intercettato in integrazione:** tre `NavigateTo("/characters...")` con lo slash iniziale.
In locale il `<base href>` è `/` e funzionano; in produzione è `/dnd-companion-app/` e sarebbero
usciti dal sottopercorso di GitHub Pages. Convertiti alla forma relativa già usata da `BottomNav`.

**Sesta voce nella barra.** `Party` sta accanto a `Personaggi`, di cui è la controparte da quando
la lista mostra solo i PG propri. Sei è il tetto: a 390px sono celle da 65px, ancora sopra i 44px di
target tap; una settima scenderebbe a 55px con le etichette quasi tutte troncate.

---

## Il manuale SRD 5.2.1 caricato (2026-07-31)

`CatalogService` cercava `wwwroot/data/srd-2024-it.json` da quando esiste la Fase 1, ma quel file non
era mai stato prodotto: i cataloghi erano vuoti finché l'utente non digitava tutto a mano. Ora c'è.

**Il perimetro è la licenza, non il manuale.** Lo SRD 5.2.1 (CC BY 4.0) è un sottoinsieme
deliberatamente ridotto del Manuale del Giocatore 2024: contiene **4 background** (Accolito,
Criminale, Saggio, Soldato) e **17 talenti**, non i 16 e i ~40 del manuale commerciale. Artigiano,
Marinaio, Fortunato, Robusto e simili **non sono ridistribuibili**: sono stati esclusi
deliberatamente, non dimenticati. L'attribuzione CC BY 4.0 è nel campo `license` del pacchetto ed è
la condizione della licenza — se sparisce, la ridistribuzione non è più conforme.

**Fonte: il PDF ufficiale, anche per l'italiano.** WotC pubblica l'SRD 5.2.1 tradotto, e i nomi
italiani non sono deducibili dall'inglese: *Eldritch Blast* è **Deflagrazione occulta**, *Acid Splash*
è **Fiotto acido**, *Vicious Mockery* è **Beffa crudele**. Un primo giro di traduzione a mano aveva
prodotto nomi plausibili ma inventati — al livello 1 solo 27 su 57 coincidevano con il manuale. Sono
stati riallineati tutti: **339 nomi su 339** ora identici all'ufficiale, con la convenzione italiana
della minuscola dopo la prima parola («Palla di fuoco», non «Palla di Fuoco»). Stessa verifica per la
terminologia: la condizione *Charmed* è **affascinato** (105 occorrenze nel PDF ufficiale, zero di
«ammaliato»), e il master è **GM**.

**Velocità in piedi, non in metri.** `PackageSpeed.Value` è un `int` e un decimale fa fallire la
deserializzazione dell'**intero** pacchetto, non della singola voce: il Golia a 35 piedi diventerebbe
10,5 m. Il formato ammette già `"unit": "ft"` e il `CHECK` sul database pure, quindi il pacchetto
dichiara i piedi — nessun dato falsificato per arrotondamento, nessuna migrazione di colonna, e il
nodo `int`-vs-`decimal` di `DA-FARE` resta aperto ma non più bloccante per il contenuto dell'app.

**Il pacchetto è dato, non codice: si verifica con i test.** `Tests/SrdPackageContentTests.cs` (14
test) controlla ciò che né il compilatore né la CI vedrebbero: che il parser lo accetti, che ogni
sezione abbia contenuto, che ogni voce porti il prefisso di provenienza `srd-2024-it/` (senza, la
voce sembrerebbe modificabile invece che di sola lettura), che le velocità siano intere e nel
dominio, che ogni classe copra 20 livelli con 9 slot ciascuno, che i tiri salvezza siano riconosciuti
da `ParseSaveProficiencies` e i nomi di classe da `SpellClassNames` — con una prova end-to-end che
il filtro per classe trovi davvero incantesimi per tutte e otto le classi incantatrici.

**Chiusura della sessione del 2026-07-31.** 552 test unitari verdi (erano 400 a inizio giornata),
build Release 0 warning / 0 errori. Il gate a due agenti è girato **tre volte** sul diff completo:
il primo giro ha prodotto un BLOCCANTE — il wizard copiava la velocità della specie senza convertire
i piedi in metri, difetto reso attuale proprio dalla scelta di dichiarare i piedi nel pacchetto — più
sette SERI, fra cui la validazione delle caratteristiche aggirabile dai pallini di passo (si salvava
un personaggio con sei 15, o con 54 punti spesi su 27) e cinque affermazioni false nei documenti
scritti in questa stessa sessione. Il secondo giro ha chiuso senza bloccanti, con tre SERI residui —
due letture divergenti dell'unità di velocità (risolta facendo dipendere `SpeedInMeters` dalla fonte
unica `PackageRowMerge.UnitaValida`) e due voci di backlog che descrivevano codice cancellato dal
diff. Il terzo giro non ha trovato né bloccanti né seri: solo rifiniture, fra cui una regressione
introdotta dalla correzione precedente (il `@key` sul campo dei punteggi dipendeva anche dal valore,
quindi le frecce dello spinner perdevano il fuoco dopo il primo scatto) e un residuo della stessa
divergenza sull'unità, rimasto nel testo del suggerimento invece che nel calcolo. Vale la pena
annotarlo: la parte più difficile da tenere vera non è stata il codice, ma i numeri e le affermazioni
nei documenti, che il diff stesso invecchiava mentre veniva scritto.

## Privilegi di classe, eliminazione dei PG e quattro segnalazioni dal campo (2026-07-31, secondo giro)

Quattro segnalazioni arrivate dall'uso reale, subito dopo il rilascio di `0b74fa4`. Una sola era un
difetto vero; le altre tre erano una funzione mai scritta, un ordinamento infelice e un limite di
licenza scambiato per un caricamento a metà. Distinguerle era metà del lavoro: solo la prima si
correggeva col codice che sembrava servire.

**«La sottoclasse non porta benefici.»** Il difetto più grosso, e invisibile a build e test: il
pacchetto porta 12 classi × 20 livelli con i privilegi e gli slot incantesimo — **274 privilegi in
tutto** — il parser li leggeva correttamente in `PackageClass.Levels`, e poi `PackageRowMerge` li
**buttava via**: né `NuovaClasse` né `ApplicaClasse` li copiavano da nessuna parte. Ogni classe
importata arrivava senza un solo privilegio, e con essa la sottoclasse: al 3° livello non succedeva
nulla perché non c'era nulla da far succedere. Il conteggio delle righe di catalogo, che è l'unica
cosa che l'import mostra, restava giusto.

La tabella `classes` non ha colonne per livello, e aggiungerle avrebbe voluto dire un'altra migrazione
da applicare a mano al progetto hosted. I dati sono quindi finiti nell'unico posto che li accetta senza
migrazione — il campo testuale `Features` — ma in una **forma riconoscibile**, non appiattiti in prosa:
`L3 — Sottoclasse del Chierico · Slot 4/2`. Il nuovo helper puro `ClassProgression` la scrive e la
rilegge, così la scheda del personaggio può mostrare i soli privilegi **già raggiunti** al suo livello,
e la pagina Classi continua a mostrare un elenco leggibile a chi di questo formato non sa nulla.

Due trappole, entrambe scoperte dai test e non a occhio. La prima: uno dei 274 privilegi si chiama
«Movimento senza armatura (+4,5 m)» — con la virgola decimale italiana. Separare i privilegi sul
carattere `,` lo spezzava in due voci senza senso, in silenzio; il separatore è quindi la stringa
`", "`. La seconda: `RiguardaSottoclasse` cercava la parola «sottoclasse», e nove classi su dodici la
usano — ma **Mago, Monaco e Paladino** conservano il nome tradizionale («Tradizione arcana»,
«Tradizione monastica», «Giuramento sacro»). Il test che pretende una voce di sottoclasse al 3° livello
per tutte e dodici le classi ha fatto emergere il buco: senza, proprio quei tre giocatori non avrebbero
visto evidenziata la scelta che al 3° definisce il personaggio.

Un aggiornamento non deve però cancellare il lavoro altrui: `ApplicaClasse` riscrive la tabella **solo**
se il campo è vuoto o contiene già una tabella generata da noi. Chi ha compilato la classe a mano si
sarebbe visto sostituire gli appunti da un re-import, senza alcun segnale.

**«Non posso eliminare un mio personaggio.»** Vero, e non per un difetto: la funzione non era mai stata
scritta. `ICharacterRepository` aveva create/read/update e basta. Lato database c'era già tutto — la
policy `characters_delete` (proprietario o master) esiste dallo schema iniziale e le due chiavi esterne
da `inventory` e `character_spells` sono `ON DELETE CASCADE` — quindi è bastato il client, senza
migrazioni. Il comando sta **in fondo alla modifica**, non nell'intestazione della scheda: lì sarebbe
finito accanto alla matita, a distanza di pollice da un'azione quotidiana. Dopo la cancellazione la
pagina **rilegge l'elenco dal server** invece di rimuovere la riga in memoria: se la RLS ha rifiutato,
PostgREST non solleva errori — cancella zero righe e risponde bene — e senza la rilettura l'utente
vedrebbe sparire un personaggio ancora vivo.

**«I mostri hanno tutti CD a 0.»** La sigla è quella della segnalazione: si intendeva il **grado
sfida**, che in italiano si abbrevia GS — «CD» è la Classe Difficoltà. E qui non c'era alcun difetto
nei dati: dei 331 mostri solo 30 hanno grado sfida 0, e i valori nel pacchetto sono corretti. Il
problema era l'**ordinamento predefinito**,
per grado sfida crescente: quei 30 occupavano l'intera prima schermata, e da telefono il bestiario
sembrava fatto solo di creature a zero. Ora l'ordine di default è alfabetico, con un selettore
Nome/Grado sfida — l'ordine per potenza serve a preparare uno scontro, non a sfogliare l'elenco.
Nell'occasione l'etichetta è passata da «CR» a «GS», che è la sigla italiana ufficiale.

**«Non ci sono abbastanza background.»** Nemmeno questo è un difetto: sono quattro perché **lo SRD 5.2.1
ne concede quattro** (Accolito, Criminale, Saggio, Soldato). Gli altri del Manuale del Giocatore non
sono ridistribuibili, e aggiungerli significherebbe pubblicare online materiale protetto. La correzione
è quindi di **interfaccia**, non di dati: la pagina Background e il passo del wizard ora lo dicono, e
indicano come aggiungerne di propri. Un limite di licenza che non si spiega viene letto come un
caricamento andato male — ed era esattamente quello che era successo.

Nell'occasione sono state tradotte le etichette delle tab della scheda (Combat/Stats/Bio/Items/Magic →
Scontro/Stat/Bio/Zaino/Magia): erano rimaste in inglese dentro un'app interamente italiana.

**Chiusura del secondo giro (2026-07-31).** 609 test unitari verdi (erano 552 a fine primo giro),
build 0 warning / 0 errori. Il gate a due agenti è girato **tre volte**. Il primo giro ha trovato un
SERIO — le righe di livello con i soli slot venivano stampate vuote, e sette classi su dodici ne
hanno: un Mago avrebbe letto «L7» seguito dal nulla, cioè la stessa aria di dato mancante che questo
lavoro doveva togliere — più sei rifiniture, fra cui una che valeva davvero: l'export di campagna non
riesportava la tabella dei livelli, quindi una campagna esportata e reimportata altrove sarebbe
tornata senza privilegi. Il secondo giro ha trovato un SERIO **nato dalla correzione precedente**:
avendo fatto mostrare i venti livelli anche alla voce «Dal manuale», «Duplica e modifica» apriva una
textarea vuota e la copia salvata nascondeva per nome la voce di pacchetto, facendo sparire la
tabella dalla pagina. Il terzo giro non ha trovato né bloccanti né seri: sei rifiniture, di cui una
sola sul comportamento — il ripiego sul manuale scattava anche per una classe scritta dal tavolo,
che nella pagina Classi oscura la voce di pacchetto: la scheda avrebbe mostrato i privilegi SRD di
una classe deliberatamente sostituita. La regola finale distingue per **provenienza**: una riga
importata senza tabella è solo vecchia e si aggiorna dal manuale, una riga del tavolo no.

Due osservazioni che valgono più delle singole correzioni. La prima: entrambi i SERI riguardavano
casi che i dati reali producono e gli esempi inventati no — i livelli di soli slot, l'omonimia fra
una classe del manuale e una del tavolo. I test scritti sul pacchetto vero li hanno trovati, quelli
scritti a tavolino no. La seconda: il secondo SERIO l'ha introdotto una correzione del giro
precedente, il che è l'argomento migliore per non fermare il gate al primo giro pulito.

## La scheda riorganizzata attorno ai valori del turno (2026-08-01)

Tre proposte di disposizione, scelta la seconda. Il difetto che risolve è di posizione, non di
contenuto: **PF, CA, iniziativa e percezione passiva** — i quattro numeri che al tavolo servono a
ogni turno — stavano dentro la tab «Scontro», e per leggerli bisognava tornare lì anche mentre si
guardavano gli incantesimi o l'inventario. Ora stanno in `CharacterVitalsBar`, **sopra le tab**, e
il gruppo barra + tab è sticky: restano leggibili scorrendo qualunque scheda. I ± dei punti ferita
sono saliti con loro, perché sono il controllo più premuto dell'app e spostare il numero senza i
pulsanti avrebbe peggiorato le cose.

Lo sticky è **sul gruppo**, non sulle due parti: impilare due elementi sticky richiede di conoscere
l'altezza del primo per calcolare il `top` del secondo, e quell'altezza cambia con la larghezza —
sotto i 360px i punti ferita prendono una riga tutta loro.

Il primo giro del gate ha trovato lì un bloccante, e i due agenti ci sono arrivati con conti
indipendenti concordi. Nella card dei PF il numero stava **in mezzo ai due pulsanti**: 44 + 44 di
bersagli che non si rimpiccioliscono, più il numero, fanno una card da almeno 160px, e in una barra
a quattro riquadri su un telefono da 390px ce ne sono 110. L'eccedenza usciva dalla card e finiva
sotto quella della CA che, essendo successiva nel DOM, la copriva e ne intercettava i tocchi: il
«+» diventava in parte impremibile, sul controllo più premuto dell'app e proprio sulle larghezze
di tutti i telefoni comuni. La soglia di 380px sembrava prudente perché era stata calcolata sul
caso che funzionava. Ora il numero sta **sopra** i pulsanti: impilati ne bastano 88, e la card ne
ha 99 già a 360px — il secondo giro del gate ha poi fatto notare che, caduto il vincolo dei 160px,
tenere la soglia a 380 mandava su due righe proprio gli schermi da 360 e 375px senza alcun
motivo.

Le tab scendono da cinque a quattro. «Scontro» e «Stat» si fondono in **«Tiri»** — tutto ciò che si
tira o si spende in un turno: velocità, ispirazione, dadi vita, tiri salvezza contro morte, armi, e
poi le sei caratteristiche con tiri salvezza e abilità. I due componenti restano due: la pagina li
compone uno dopo l'altro invece di fonderli in un file solo. **Le difese** (resistenze, immunità,
vulnerabilità) lasciano il combattimento per la tab «Scheda»: non sono un'azione, sono tratti del
personaggio e si consultano come i tratti di specie.

**L'eliminazione del personaggio si sposta in superficie**, dal fondo del form di modifica al fondo
della tab «Scheda». Era stata messa nella modifica per tenerla lontana dal pollice, ma è stata
richiesta due volte da chi ce l'aveva già a disposizione: chi cerca «come cancello il mio
personaggio» guarda il personaggio, non pensa di entrare in modifica. Resta dietro la stessa
conferma e la stessa verifica di permesso.

Lo spostamento del markup si porta dietro il proprio CSS, come impone l'isolamento di Blazor: le
regole dei punti ferita sono ora in `CharacterVitalsBar.razor.css`, quelle delle difese in
`CharacterBioTab.razor.css`. Con un'eccezione che vale la pena annotare: `.section-header` **non**
è stata replicata, perché è globale in `app.css` e raggiunge già i componenti — `CharacterCombatTab`
la usa così per il titolo «ARMI». Una copia scoped avrebbe la meglio sulla globale (0,2,0 contro
0,1,0) e da lì in poi le due definizioni divergerebbero alla prima modifica di `app.css`, in
silenzio.

**Chiusura.** 609 test verdi, build 0 warning / 0 errori — il redesign non tocca logica di dominio,
quindi la suite resta quella del giro precedente. Il gate a due agenti ha fatto tre giri. Il primo
ha trovato **lo stesso bloccante da entrambe le parti**, con conti indipendenti che coincidevano: la
card dei punti ferita traboccava sui telefoni comuni. Vale la pena notare come è nato — non da una
svista, ma da una misura presa sul ramo sbagliato: la soglia di 380px era stata calcolata verificando
il caso in cui i PF vanno a capo, che funzionava, invece del caso a quattro riquadri, che era quello
in uso su ogni telefono reale. Il secondo giro ha poi mostrato il rovescio: caduto il vincolo dei
160px, quella stessa soglia mandava su due righe schermi che non ne avevano bisogno. Il terzo non ha
trovato né bloccanti né seri, solo due numeri rimasti indietro nei commenti.

## Mostri al tavolo, sottoclassi vere, export che serve a qualcosa (2026-08-01)

Tre segnalazioni dall'uso, tutte e tre difetti reali — e con una radice comune: **il manuale era
visibile ma non utilizzabile**. Le sue voci comparivano nei cataloghi perché la UI le sovrappone a
quelle di campagna, ma nulla, oltre a quelle schermate, sapeva della loro esistenza.

**«I dati non posso usarli in combattimento.»** Il tracker iniziativa interrogava solo
`IMonsterRepository`, cioè le righe della campagna: i 331 mostri del pacchetto non erano
utilizzabili al tavolo senza prima importarli a mano. Non serviva alcuna migrazione — `Combatant`
è un POCO dentro il jsonb di `combat_state`, senza chiave esterna verso `monsters` — quindi è
bastato far attingere la pagina a `ICatalogService`. Con una condizione: 331 voci in un elenco
piatto sono inservibili su un telefono, e renderebbero altrettanti stepper, per cui la **ricerca è
parte del meccanismo**, non un ornamento. A ricerca vuota si vedono le sole righe di campagna —
il comportamento che il tracker ha sempre avuto — e il manuale entra quando lo si cerca.

**«La sottoclasse non dà benefici, è solo un campo di testo.»** Vero, e stavolta il difetto era nei
dati: lo SRD contiene **una sottoclasse per classe** con i propri privilegi (Cammino del berserker,
Dominio della Vita, Campione…), ma l'estrazione del 2026-07-31 le aveva saltate. Sono state
estratte dal PDF italiano ufficiale, e la storia dell'estrazione vale più del risultato. Il primo
tentativo cercava i titoli nel testo corrente e sforava: i «Livello N:» di una sottoclasse
finivano mescolati a quelli della classe seguente, e il Warlock inglobava una sezione intera
(10 924 caratteri contro i 2 500 tipici). I confini corretti li danno le **pagine**, che il testo
estratto conserva. Il secondo difetto era più insidioso: nel documento il titolo «Livello 3:
Frenesia» non è separato dal proprio corpo da alcuna riga vuota, quindi ragionare per paragrafi
incollava l'intera descrizione dentro il nome del privilegio. E un terzo, ancora più silenzioso:
l'impaginazione a due colonne spezzava i titoli, producendo «Incantesimi del Dominio della» —
un nome plausibile, troncato.

La verifica che ha chiuso la questione **non è stata una rilettura**: i livelli in cui la tabella
di ogni classe promette un privilegio di sottoclasse sono stati confrontati con quelli che la
sottoclasse dichiara. Due estrazioni indipendenti, fatte in sessioni diverse per percorsi diversi,
concordano su tutte e dodici le classi.

Non è bastata. Il gate ha trovato **due nomi ancora troncati** che quel confronto non poteva
vedere, perché il livello era giusto e sbagliato era il nome: «Stile di combattimento» invece di
«Stile di combattimento aggiuntivo» — e il Guerriero ha già un privilegio di classe con quel nome
al 1° livello, quindi la scheda di un Campione ne mostrava due identici — e «Incantesimi del
Giuramento» invece di «Incantesimi del Giuramento di devozione». Il test che avevo scritto cercava
i titoli che finiscono con una preposizione e per costruzione non poteva prenderli entrambi: il
troncamento ha **due** forme, e la seconda si riconosce dall'altro capo — il testo che segue il
titolo riprende in minuscola, perché la coda del nome è finita lì. Ora il test guarda entrambi i
lati, e un terzo controlla che le descrizioni comincino con la maiuscola: tre di esse si aprivano
a metà frase, perché il riconoscimento del titolo provava le finestre di righe dalla più lunga e
si portava via anche il sottotitolo.

Le descrizioni sono **testo piano**: la prima versione marcava i titoli dei privilegi con gli
asterischi di Markdown, che il progetto non rende da nessuna parte — sulla scheda si sarebbero letti
così com'erano.

Il formato porta le sottoclassi **annidate dentro le classi** e non come sezione di primo livello,
per compatibilità: i client già installati leggono lo stesso file e ignorano i campi che non
conoscono, mentre un incremento di `schemaVersion` glielo farebbe rifiutare per intero. Nel wizard
e nella modifica il campo diventa una scelta guidata quando il manuale conosce la classe, e resta
testo libero altrimenti — un tavolo può inventarsi la propria sottoclasse.

**«Se volessi implementare i dati della sessione non so come fare.»** La più giusta delle tre.
L'export costruiva il file dai soli cataloghi di campagna, quindi chi non aveva importato nulla
scaricava un file con le sezioni vuote: senza i mostri, e — cosa peggiore — senza una sola voce
compilata da cui capire come si scrive. Ora accanto a «Esporta la campagna» c'è **«Esporta tutto,
manuale incluso»**, che unisce le voci del manuale non già coperte da una riga di campagna e
riporta l'attribuzione CC BY, che di quel materiale è condizione di ridistribuzione e non un
ornamento. L'attribuzione, va detto, non dipende dal pulsante premuto ma dal contenuto: anche
l'export della *sola* campagna può portare materiale SRD, perché `SpellMaterialization` scrive una
riga di database con quella provenienza — descrizione inclusa — ogni volta che un giocatore aggiunge
alla scheda un incantesimo che vive solo nel manuale. Senza il controllo, sarebbe uscito un file con
testo SRD, senza attribuzione e per giunta senza traccia dell'origine, che `AssignIds` cancella. Le voci incluse perdono la provenienza `srd-2024-it/`: nel tavolo che le reimporta
devono essere righe proprie, modificabili, non voci di sola lettura. Alla pagina Dati si aggiunge
una **guida al formato** richiudibile: la struttura, i due soli campi obbligatori (`id` e `name`),
il fatto che un campo assente significa «non lo so» e non «cancellalo», e perché conviene dare agli
id un prefisso proprio — è da quello che «Rimuovi un import» li riconosce.

Due precisazioni che il gate ha imposto di mettere per iscritto, perché **superano lo spec del
formato**. La prima: l'export «manuale incluso» emette anche `feats`, mentre lo spec §5 afferma che
un export non ne produce mai — affermazione vera fintanto che il file si costruiva dalle sole righe
di database, dove talenti non ce ne sono. Al reimport restano marcati non importabili, quindi il giro
resta coerente: escono per essere letti e copiati, non per rientrare. La seconda è dello stesso
genere e più insidiosa: la guida invita a partire dal file esportato, che ora contiene le
sottoclassi, ma l'import **non le scrive da nessuna parte** — la tabella `classes` non ha una colonna
per portarle. Taciuto, avrebbe fatto perdere il lavoro a chi ne aggiunge una propria; l'anteprima ora
lo dichiara nella sezione Classi con la stessa formula dei talenti, e la guida lo dice prima. Nello
stesso giro le sottoclassi sono entrate nei due controlli che il parser applica a ogni altra sezione
(trim di id e nome, id presente e non ripetuto): sono una sezione a tutti gli effetti, e il nome
finisce dentro un `<option value>` che il menu confronta per stringa esatta.

Il resto della revisione ha corretto un difetto che nessuna delle tre segnalazioni prevedeva:
all'apertura della modifica il form applicava **metà** del risultato di `RisolviScelta` — sapeva che
una sottoclasse scritta a mano va mostrata nel campo libero, ma non che quella di un'**altra** classe
va togliersi. Un PG creato prima di oggi con «Mago» e «Cammino del berserker» (dato legittimo: il
campo era testo libero e il cambio di classe non lo ripuliva) apriva il menu senza selezione mentre
il valore restava lì, e il primo salvataggio lo riscriveva nel database. Lo stesso vale per la classe
digitata a mano: ora anche quel campo rivaluta la sottoclasse a ogni carattere, perché appena il
testo diventa il nome di una classe del manuale il controllo passa da input libero a menu. E
`RisolviScelta` restituisce il nome **come lo scrive il manuale**: il confronto normalizza accenti e
maiuscole, il `<select>` no, quindi un «invocatore» salvato a mano lasciava il menu vuoto pur essendo
la scelta giusta.

Il secondo giro ha poi trovato il rovescio di quella stessa correzione, ed è la lezione del giorno:
**scollegare è distruttivo come conservare**. Applicando il record intero, un PG con una classe che
il manuale non ha — il «Guerriero del sale» di un tavolo — e una sottoclasse chiamata «Campione»
perdeva il valore alla sola apertura della modifica, perché «Campione» nel manuale è del Guerriero.
La domanda «è di un'altra classe?» ha senso solo se la classe corrente sta essa stessa nel manuale:
altrimenti non c'è nessun confronto da fare. Nello stesso giro è emersa un'**asimmetria fra le tre
schermate**: la scheda si chiedeva se la classe fosse ancora quella del manuale prima di mostrare i
privilegi di sottoclasse, mentre wizard e modifica offrivano comunque il menu — così il wizard
prometteva «al livello 3 dà Frenesia» e la scheda poi non mostrava nulla. Ora le tre pongono la
stessa domanda (`ClassProgression.ClasseDelManuale`), e l'export la pone sulla provenienza della
riga: su una classe del tavolo non innesta le sottoclassi SRD, che le attribuirebbero un contenuto
non suo.

Il terzo giro ha mostrato che il criterio non era ancora **uno**: il punto che *cancella* si
accontentava di trovare il nome della classe nel manuale, quindi un tavolo con la propria «Mago»
perdeva comunque una sottoclasse chiamata «Campione» — e la perdeva senza che il menu, che la domanda
giusta la poneva già, gli fosse mai stato offerto. Ora `RisolviScelta` riceve anche le righe di
campagna e chiede la stessa cosa. La regola generale che resta: **il criterio che distrugge non può
essere il più debole di quelli in campo**.

## La direzione scelta (2026-08-01)

Chiuso il lavoro, la conversazione ha messo a fuoco che i tre feedback dicevano la stessa cosa da tre
angoli: **l'app è un ottimo archivio e un mediocre aiuto al gioco.** Conserva 331 mostri e 339
incantesimi, ma non li mette in mano a nessuno al momento giusto. Da qui tre filoni decisi, elencati
in `DA-FARE.md` §1.

**La sottoclasse deve essere una scelta.** È la richiesta ripetuta più volte, e la consegna di oggi la
soddisfa solo a metà: il menu compare dove il manuale conosce la classe, ma una classe del tavolo — o
una importata — non ha **dove tenere** le proprie sottoclassi, perché la tabella `classes` non ha una
colonna per portarle. Finché è così, il formato le dichiara e l'import le butta, e per quelle classi il
campo resta il testo libero da cui si voleva uscire. Serve una casa nei dati; poi la scelta va
alimentata da lì, non dal solo pacchetto, e i privilegi vanno **applicati** e non solo elencati.

**Il file di dati deve portare tutto.** L'export nasce come «backup dei cataloghi» e oggi è anche il
modello da cui copiare, ma perde ancora dei pezzi: `skillChoices` digitato a mano non ha inversione, le
sottoclassi escono solo dalle righe di provenienza manuale, i talenti non hanno tabella. Il perimetro
è stato deciso — **cataloghi al completo, PG e appunti fuori** — e con esso due vincoli espliciti
dell'utente: **nessun limite di volume** (il formato deve scalare; un tetto al numero di voci era
stato proposto ed è stato scartato) e il criterio di fatto in forma di test: **export → import →
export deve dare lo stesso file**.

**E un buco di sicurezza, trovato rispondendo a una sua domanda.** Nulla impedisce a un file scritto a
mano di dichiarare `"id": "srd-2024-it"`, cioè l'identità del manuale ufficiale. Le righe che ne
nascono si presentano come ufficiali, l'interfaccia le rende **non modificabili** (le pagine di
catalogo nascondono la ✎ sulle righe di provenienza SRD, master compreso) e «Rimuovi un import»
**rifiuta** quel prefisso, per non cancellare il manuale: diventano indelebili, recuperabili solo via
database. È lo stesso schema del caso «note condivise» già noto — non furto di dati, contenuto
indelebile. Le RLS invece tengono (si scrive col proprio token, quindi solo dove le policy
consentono) e non c'è iniezione HTML, perché `MarkupString` non è usato da nessuna parte. Si chiude nel
parser, e va fatto **insieme** al lavoro sul formato: sono gli stessi file.

**Come si lavora, rivisto.** Il gate a due agenti è costato ~820k token in tre giri e ha trovato
quattro difetti reali, uno dei quali cancellava dati in silenzio: non si butta, si **calibra**. Da oggi
non si lancia affatto sui diff di sola documentazione, fa un giro solo su UI e modifiche circoscritte,
e arriva a tre solo dove il diff tocca dati, RLS, serializzazione o modelli. Nello stesso spirito
`DA-FARE.md` è passato da 730 righe (29k token a ogni lettura) a un indice di soli punti aperti: la
storia era già qui nel DIARIO, e il documento la ripeteva. La versione integrale è in
`docs/archivio/DA-FARE-chiuso.md`, che non si aggiorna più.

---

## Le sottoclassi hanno una casa nei dati (2026-08-01)

Prima di oggi la sottoclasse era una **scelta a metà**. Il manuale precaricato ne conosceva una per
classe e la scheda la offriva in un menu, ma solo per le dodici classi SRD: per una classe inventata
dal tavolo, o per una importata da un file, il campo tornava a essere testo libero — e non perché
qualcuno l'avesse deciso, ma perché non esisteva un posto dove tenere le sottoclassi di una classe che
il manuale non conosce. Il formato di scambio le portava già, `subclasses` annidato dentro la classe;
il parser le leggeva e le validava; e poi l'import **le buttava via**, perché la tabella `classes` non
aveva una colonna per riceverle.

### La casa: una colonna, non una tabella

La scelta era fra una colonna testuale su `classes` e una tabella `subclasses` con la sua RLS. Ha
vinto la colonna, per tre ragioni che vale la pena scrivere perché la seconda non è ovvia.

La prima è che il precedente esiste già ed è recente: la tabella dei livelli di classe vive dentro
`classes.features`, in un formato di righe `L3 — Frenesia` che una persona legge a occhio e il codice
rilegge senza ambiguità (`ClassProgression`). Un secondo campo testuale accanto al primo è coerente, e
riusa la stessa sintassi per i privilegi — che così resta **una sola**.

La seconda è che una tabella nuova avrebbe aggiunto quattro policy su una superficie RLS che ha ancora
aperto il varco «campaign hopping» (DA-FARE §2), e avrebbe portato con sé un aggregato intero da
attraversare: repository, piano di import, piano di rimozione, merge dei cataloghi. La colonna eredita
le policy di `classes` così come sono, e tiene la sottoclasse **dentro la riga della classe**, che è
dove la provenienza (`source_id`) vive già: una sottoclasse importata dal manuale e la classe che la
porta si rimuovono insieme, senza una riga di codice in più.

La terza è la compatibilità col client già online, che il service worker lascia in vita finché
l'utente non preme «Aggiorna». In lettura Newtonsoft ignora le colonne che il modello non dichiara; in
scrittura `Update` serializza solo le colonne mappate, quindi il client vecchio non può azzerare una
colonna che non conosce. Nell'ordine inverso, però, la migrazione è un **prerequisito duro**: il
client nuovo mappa `subclasses` e la manda su ogni scrittura, quindi senza `ALTER TABLE` fallirebbero
con 400 anche «salva classe» e l'import — non le sole sottoclassi. Applicata all'hosted prima del push,
e verificata; vale come regola per ogni futura `ADD COLUMN` che entri anche nel modello.

Il formato tiene un `id:` facoltativo in testa a ogni blocco. Non serve a niente dentro l'app —
nessun ramo decisionale consulta l'id di una sottoclasse — e serve a due cose fuori: la fedeltà del
giro export → import → export, e (scoperta durante il gate) riconoscere la prosa SRD sopravvissuta a
un «duplica e modifica» che ne ha azzerato la provenienza, per non emettere un file senza attribuzione.

### Tre agenti in parallelo, e i difetti stanno sulle giunture

Il lavoro è stato scritto da tre subagent in parallelo su insiemi di file **disgiunti**: import
(parser, merge, piano), export (round-trip), interfaccia (CRUD nella pagina Classi e le tre schermate
del personaggio). Ha funzionato: nessun conflitto, e ciascuno ha prodotto helper puri con i loro test.

Ma tutti e tre i difetti gravi che il gate ha trovato stavano **esattamente sulle giunture**, dove per
costruzione nessuno dei tre poteva guardare:

- l'export «tutto, manuale incluso» — il file che la guida indica come modello da cui partire —
  produceva un JSON che il parser nuovo **rifiutava per intero**: 17 talenti con l'id del manuale,
  copiati verbatim perché i talenti erano la sola sezione che non passava da `AssignIds`. Chi
  esportava e reimportava si vedeva incolpare il proprio file;
- la rilettura del formato scartava le righe vuote **dentro** le descrizioni. Tutte e dodici le
  descrizioni di sottoclasse del manuale hanno da cinque a sette capoversi, e quattromila caratteri
  diventavano un blocco unico — nel file esportato e nella scheda, che li rende con `pre-wrap`. Peggio
  del difetto: la stessa sottoclasse si leggeva in due modi diversi a seconda che il master avesse
  importato le classi o no;
- il divieto sul prefisso `srd-2024-it/`, nato per chiudere un buco di sicurezza vero, colpiva anche
  gli id di **sottoclassi e talenti** — e lì rompeva i file già esportati dal client online senza
  comprare niente. Il criterio giusto è uno: l'immunità nasce dal `source_id`, quindi il divieto vale
  dove l'id *diventa* un `source_id`. Un id di sottoclasse non lo diventa mai; un talento non ha
  nemmeno una tabella. Il giorno in cui i talenti l'avranno, il divieto tornerà su quella sezione
  insieme alla tabella.

Due difetti erano invece **miei**, introdotti dalle correzioni del primo giro, e sono lo stesso
errore: usare un risolutore per **nome** dove il dato è per **riga**. L'export emette una voce per
riga di catalogo, e io gli avevo dato in mano la funzione che le schermate usano a ragione, quella che
risolve su tutte le righe omonime. Con due «Barbaro» — una importata e una creata da «duplica e
modifica», che è una collisione per costruzione — la copia del tavolo esportava le sottoclassi SRD
dell'altra, e una riga a cui l'utente le aveva *tolte* se le ritrovava nel file.

### La regola che continua a costare, formulata meglio

Il campo Sottoclasse si «scollega» quando il valore salvato appartiene a un'altra classe. È la stessa
regola che a fine luglio è costata tre giri di gate, e ne ha chiesti altri due adesso, ogni volta per
la stessa ragione: **scollegare è distruttivo quanto conservare**, quindi il criterio che cancella non
può essere più forte di quello che offre il menu. Le due formulazioni sbagliate di questo giro:

1. il ramo che cancella si attivava quando la classe offriva un elenco, e consultava il manuale anche
   per una classe che il tavolo aveva sostituito con una propria. Rivelatore: con l'elenco proprio
   *vuoto* il valore sopravviveva, con l'elenco pieno no;
2. corretto quello, la prova «è di un'altra classe» poteva ancora arrivare da una riga **importata**
   dal manuale — che da oggi porta lo stesso testo SRD del pacchetto. Così l'esito dipendeva dal fatto
   che il tavolo avesse importato le classi: stesso contenuto, due risposte diverse, e in una delle
   due il valore veniva cancellato.

La forma che regge distingue la **provenienza della prova**, non la sua sede: le classi che il tavolo
ha scritto valgono sempre; il materiale del manuale — pacchetto *o* riga importata — vale solo se la
classe corrente è ancora quella del manuale.

### Che cosa è chiuso, e a che prezzo

Chiusi: la sottoclasse è una scelta per ogni classe, di qualunque provenienza
(`SubclassCatalog.Disponibili`); l'import la scrive, con la guardia che impedisce a un re-import di
cancellare un elenco scritto a mano; la pagina Classi la crea, la modifica e la rimuove; l'export la
porta su ogni riga; e il prefisso del manuale non è più spoofabile dove conta. Il criterio di fatto
sull'export non è «lo stesso file di partenza» ma l'**idempotenza del ciclo**: export → import →
export dà due file identici, perché un file scritto a mano può legittimamente arrivare in una forma
che l'app conserva in modo canonico. La suite è passata da 676 a 755 test.

Resta aperto ciò che il formato non ha: `languages` e i bonus di caratteristica delle specie, la
descrizione e le competenze delle classi, taglia/tipo/statistiche dei mostri. Sono buchi preesistenti
che chiedono campi nuovi, non correzioni — e la voce in DA-FARE dice di aggiungerli quando si passa da
quei modelli per altri motivi. Resta aperto anche il pezzo che rende la sottoclasse davvero utile: i
suoi privilegi vanno **applicati**, non solo elencati, e quello passa dal motore di derivazione (§3).

Sul costo del gate calibrato: il diff toccava dati, serializzazione, modelli e formato di scambio,
quindi tre giri pieni. Hanno trovato un BLOCCANTE, quattro SERIO e undici minori — e il BLOCCANTE
avrebbe rotto in produzione proprio la funzione che la guida dell'app indica per prima. Qui i tre giri
si sono pagati.

## Level-up guidato: il piano è una funzione, non un valore (2026-08-06)

L'innesco è stato Baldur's Gate 3. Lì il passaggio di livello propone le sole scelte legali e non
chiede mai di digitare un numero; qui salire di livello significava aprire il form e correggere a mano
punti ferita, dadi vita, nove slot e competenze. Era l'attrito che tornava a ogni sessione di gioco, e
l'unico punto dell'app in cui il giocatore doveva conoscere le regole **meglio** dell'app che le
contiene.

Il perimetro è più stretto di BG3, e la differenza è voluta: BG3 conosce la *semantica* di ogni
privilegio perché ha un reparto contenuti dietro. Quest'app conosce le *tabelle*, che è ciò che serve
per togliere l'attrito. «Difesa senza armatura» arriva come nome nell'elenco e non diventa una formula
per la classe armatura: attraversare quella linea significa data entry di meccaniche per sempre.

Il conto dei costi si è rivelato più basso del previsto, e per una ragione trovata nei dati: i 17
talenti del pacchetto hanno già `category` — Origine, Generale, Stile di combattimento, Epico. Nella
5e 2024 l'incremento di caratteristica **è** un talento Generale, quindi i livelli 4/8/12/16 non sono
un caso speciale ma «scegli un talento Generale», e la sotto-scelta dei punteggi si apre solo se cade
lì. Le quattro scelte più frequenti della progressione — 66 occorrenze su 274 — erano già alimentate
dai dati esistenti. Il primo giro non ha richiesto una riga di contenuto nuovo.

**Due decisioni hanno cambiato la forma del codice**, entrambe emerse dal consulto prima di scrivere.

La prima: l'incremento di Costituzione ha **effetto retroattivo** sui punti ferita — +2 all'8° vale +1
per ognuno dei livelli già posseduti. Un piano calcolato una volta sola divergerebbe dalle regole
proprio nel flusso che promette di calcolarle. Quindi `LevelUpPlan` non è un valore ma il risultato di
una funzione `(stato, risposte)`, e si rigenera a ogni risposta.

La seconda: **si sale un livello alla volta**. Dentro un salto 3°→7° le scelte si influenzano a
cascata (la sottoclasse presa al 3° sblocca privilegi al 6°, l'incremento al 4° cambia i punti ferita
del 5°-7°): il triplo della superficie di bug per il caso raro. Il recupero si fa ripetendo, con un
«Sali ancora» nel toast — che è anche come fa BG3.

**L'app non tira il dado.** Offre la media, oppure un campo dove inserire il risultato tirato al
tavolo, vincolato a `1..dado`. Un tiro dentro l'app andrebbe reso vincolante — registrato e non
ripetibile — per non essere un reroll silenzioso; e il dado si tira davanti al master, che è il momento
sociale bello. Non offrendo l'RNG, il problema non esiste.

I punti ferita si **sommano**, non si ricalcolano da zero: chi ai livelli passati aveva tirato non se
li vede corretti, cioè sovrascritti. Gli slot invece sono assoluti, perché la tabella è la verità e il
vettore intero è sicuro anche su una scheda incoerente. E `Pianifica` torna `null` se la classe non ha
una tabella: il motore guida dove ha dati, e per le classi del tavolo resta il form di prima.

**Scostamento dichiarato rispetto alla spec**: delle sottoclassi si mostra il nome con la descrizione
troncata ed espandibile, non l'estratto del solo livello d'ingresso. Estrarre quel blocco significa
parsare prosa libera («Livello 3 — Frenesia: …»), che è fragile e mal testabile, per un guadagno
modesto. La spec diceva l'estratto, il piano operativo la troncatura: la contraddizione era fra i due
documenti, e si chiude qui a favore della troncatura.

**Sul metodo.** È il primo lavoro fatto con la regola nuova (v. `CLAUDE.md`, «Chi scrive il codice»):
progetto e reviso io, scrivono i Sonnet, Fable consiglia in sola lettura. Il consulto è servito prima
di scrivere, non dopo: le due decisioni qui sopra sono nate lì, e correggerle a codice fatto sarebbe
costato una riscrittura del motore.

La conferma più netta è arrivata dal gate. Il lavoro è stato scritto da tre agenti a file disgiunti, e
**tutti i difetti gravi stavano sulle giunture** — dove un file incontra l'altro e nessun autore può
vedere per costruzione. I peggiori: `Applica` sommava i punteggi senza tetto a 20, e
`CharacterNormalizer` le caratteristiche non le clampa (gotcha già noto); `Applica` non era idempotente
sui punteggi, quindi un salvataggio fallito e ritentato dava +2 due volte; una scelta con catalogo
vuoto — rete assente — rendeva la conferma irraggiungibile per sempre; e i privilegi passivi finivano a
schermo **due volte**, perché la scheda li deriva già dalla stessa tabella, sotto un titolo
(«annotati a mano») che dichiarava il falso. Nessuno di questi era visibile dall'interno di un file
solo.

## Il master che assegna, e perché la riga intera è il problema (2026-08-06)

Analisi fatta col consulto, dopo il level-up guidato, sul goal dichiarato: creazione e progressi
guidati, il master che assegna esperienza, denaro e oggetti a un giocatore o a tutto il gruppo, e le
comodità di Baldur's Gate 3 che al tavolo hanno senso.

**La scoperta che ordina tutto il resto** è che le RLS sono già più avanti dell'interfaccia:
`characters_update` vale `owner_id = auth.uid() OR is_campaign_master(campaign_id)`, e le policy di
`inventory` fanno lo stesso passando dal personaggio. Il master **può già** scrivere sui PG altrui,
oggi, senza una migrazione. Manca solo il flusso che glielo faccia fare comodamente.

E qui sta il pericolo, perché `characters` è una riga monolitica da ~90 colonne, non ha `updated_at`,
e `UpdateCharacterAsync` fa `Update(character)`: **riga intera, last-write-wins**. Finché scrive una
persona sola sulla propria scheda va bene. Nel momento in cui il master diventa un secondo scrittore,
quello stesso meccanismo diventa il modo numero uno per corrompere dati veri: il master che assegna
100 mo a cinque personaggi con in mano una copia stantia riscrive *tutte* le colonne, e cancella i
punti ferita aggiornati, l'incantesimo annotato, il level-up appena fatto. Lo snapshot/rollback messo
in `Characters.razor` non protegge da questo: lì il salvataggio **riesce**, ed è proprio il problema.

La soluzione scelta è la più semplice che funziona davvero: gli oggetti sono **insert di righe
nuove** (zero concorrenza per costruzione), monete ed esperienza passano da una **RPC con incremento
atomico** (`SET gold_pieces = gold_pieces + delta`, `SECURITY INVOKER` perché le RLS continuino a
valere dentro la funzione). Non un optimistic locking con `updated_at`, che sarebbe sproporzionato; e
soprattutto non un read-modify-write lato client, che ha esattamente la stessa finestra del bug che
si vuole togliere. La regola è ora in `CLAUDE.md`, perché la prima implementazione frettolosa del
«dai 100 mo» la violerebbe.

**Cosa NON si costruisce**, e il perché conta quanto il cosa. Niente registro delle assegnazioni in
v1: raddoppierebbe migrazione, policy e test per comprare uno storico che al tavolo è surrogato dalla
voce del master — si ripesca se dopo un mese emergono davvero i «ti avevo già pagato?», e allora come
*log*, mai come fonte del saldo. Niente notifiche al giocatore: l'app è **pull** per scelta
architetturale documentata (il realtime è stato tolto dal bundle, il service worker non fa
`skipWaiting`), e il tavolo ha già il suo canale in tempo reale, cioè la voce di chi arbitra. Niente
riposo comandato dal master: sono scritture multi-riga cross-utente per replicare una frase che il
master pronuncia comunque — il bottone del riposo sta sul personaggio e lo preme il giocatore. E
niente avanzamento automatico da esperienza: l'esperienza si assegna come contatore, ma le soglie che
promuovono contraddirebbero il level-up appena costruito come **atto deliberato del giocatore**, con
scelte che solo lui può fare.

**Sulla creazione guidata**, la decisione architetturale è una sola: «crea al livello N» non
riscrive la progressione dentro il wizard, ma produce il personaggio di 1° completo e poi **incatena
il dialogo di level-up** che esiste già. Qualunque altra strada produce due motori da tenere
allineati — la stessa malattia dei due form di oggi, appena curata.

**Il criterio con cui filtrare le comodità di BG3**, infine, e vale per tutto ciò che verrà: BG3 è
arbitro assoluto di un gioco per un giocatore solo; qui l'arbitro è il master, a voce. Una funzione
che *sostituisce contabilità* (riposi, bottino, slot, iniziativa) è benvenuta; una che *sostituisce
l'autorità del tavolo* (avanzamento automatico, notifiche che anticipano il master, validazioni
bloccanti sull'editor libero) sembra completezza e invece toglie flessibilità. L'editor libero e i
campi a testo libero sono una funzionalità, non un debito.
