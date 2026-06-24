# DIARIO DI PROGETTO — D&D Companion

> Promemoria sintetico di **cosa è stato fatto e perché**. Per ciò che resta aperto vedi [DA-FARE.md](./DA-FARE.md).
> Aggiornato: **2026-06-24**.

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
ok (gli assembly radicati Gotrue/Postgrest sopravvivono). Dettagli e caveat `wasm-tools` in [DA-FARE.md](./DA-FARE.md) §2.

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
