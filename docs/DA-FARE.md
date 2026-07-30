# DA FARE — D&D Companion

> Cose ancora da implementare, debito tecnico da pianificare e idee aperte da ragionare.
> Per lo stato di ciò che è già fatto vedi [DIARIO.md](./DIARIO.md).
>
> Sintetizza analisi pregresse (audit sicurezza/architettura e diagnosi dipendenze) ormai integrate qui;
> riporta solo ciò che resta effettivamente aperto dopo la migrazione a Supabase Auth.
>
> Ultimo aggiornamento: **2026-07-29**
>
> I punti legati alla **monetizzazione** (entitlement/Play Billing, modello free-vs-pagamento) sono accantonati
> in [DA-FARE-MONETIZZAZIONE.md](./DA-FARE-MONETIZZAZIONE.md): da affrontare solo quando si deciderà di aprire
> la monetizzazione.

Legenda priorità: 🔴 **bloccante** per il lancio pubblico · 🟠 **alta** · 🟡 **media** · 🟢 **bassa/idea**.

---

## 🔜 Pronti per /loop — quick-win ingegnerizzati

> Tre interventi a basso rischio, indipendenti tra loro, pensati per una singola sessione `/loop`.
> Emersi dall'uso reale dell'app (sessione del 2026-06-19).

### A. Recovery cache negli errori DB (caso Firefox) — ✅ FATTO (2026-06-20)
**Problema:** all'apertura l'app a volte mostra "errore di connessione al DB" per cache PWA corrotta; pulire
la cache a mano è proibitivo per utenti non esperti.
**Come:** helper JS `window.repairApp()` in `wwwroot` che deregistra il service worker, svuota tutte le
Cache API e ricarica — **senza toccare `localStorage`**, così la sessione Google resta attiva. Lato Blazor
un piccolo componente riutilizzabile (`Shared/DbErrorBanner.razor`) con messaggio + pulsante
"🔧 Ripara e ricarica" che invoca `repairApp()`, agganciato ai banner di errore di connessione (almeno
Home, Characters, Combat).
**Fatto quando:** con cache corrotta su Firefox, un click rimette in piedi l'app **già loggata**.
**Stato:** ✅ `window.repairApp()` in `index.html` + `Shared/DbErrorBanner.razor` (tasto "🔧 Ripara e ricarica"
solo sugli errori di sistema), applicato a tutte e 8 le pagine. Build Debug pulita.

### B. Showroom galleria componenti — ✅ FATTO (2026-06-20)
**Problema:** serve una base per rendere la UI più curata e coerente.
**Come:** nuova pagina `Pages/Showroom.razor` su rotta `/_showroom`, fuori dalla navigazione normale
(raggiungibile via URL). Renderizza la libreria UI a tema: palette colori attuali, tipografia, bottoni
(primario/secondario/danger), card, `StatCard`, `SpellListItem`, banner errore, FAB, campi input, empty
state.
**Si ripaga:** diventa il banco di lavoro per estrarre i **design token** (vedi §6) — guardando tutto
insieme si vedono i colori da centralizzare.
**Fatto quando:** `/_showroom` mostra tutti i mattoncini visivi a tema in un'unica pagina.
**Stato:** ✅ `Pages/Showroom.razor` (rotta `/_showroom`, `LoginLayout` → niente guard). Palette colori con
hex (bozza token), tipografia, bottoni, form, card, banner (`DbErrorBanner` reale), `StatCard`/`SpellListItem`
con dati di esempio, FAB, empty state. Build Debug pulita.

### C. Bonus raggruppati + scaletta di compilazione — ✅ FATTO (2026-06-20)
**Problema:** compilare la scheda è lento e disorientante — i bonus sono sparsi e non c'è un ordine chiaro.
**Come:** in `Characters.razor` (form di modifica) radunare i bonus/derivati oggi sparsi (competenza,
iniziativa, modificatori caratteristiche, bonus razziali) in **un blocco riepilogo coerente**, e dare alle
sezioni del form una **scaletta numerata in ordine logico** (1. Identità → 2. Caratteristiche →
3. Combattimento → 4. Risorse → 5. Incantesimi → …). Intervento UX **mirato sul markup**, non refactor del
mega-componente (quello resta in §3).
**Fatto quando:** il form ha sezioni numerate in ordine logico e i bonus stanno in un unico blocco.
**Stato:** ✅ I 7 titoli del form di modifica numerati (1. Identità → 7. Incantesimi) + blocco riepilogo
(competenza + 6 modificatori) in cima alla sezione Caratteristiche, riusando `.derived-info`. Build Debug pulita.

> ⚠️ Tampone, non redesign: il flusso di compilazione vero (wizard guidato) è in §8. Questo lo rende solo
> più sopportabile subito.

---

## 1. Sicurezza — prerequisito al lancio pubblico

> **Stato (2026-06-24): RLS attive e corrette su tutte le tabelle.** L'audit del DB ha rivelato che le
> Row-Level Security erano **già implementate** (non permissive come annotato in passato); abbiamo chiuso i
> due gap residui. L'autorità sui dati è ora lato server: chi ha la anon key non può più leggere/scrivere
> dati altrui via REST. Dettaglio in `docs/superpowers/` (spec + piano del 2026-06-24).
> ⚠️ **Qualifica (2026-07-25):** vale per la lettura e per la generalità della scrittura; resta un varco
> sull'`UPDATE` di **sette tabelle** — un autore può riassegnare `campaign_id` di una propria riga verso una
> campagna di cui non è membro. Non è puntuale come sembrava a prima vista: su `characters` e `notes`
> l'effetto è **iniezione persistente** in una campagna altrui, non semplice perdita (voce sotto, "Lacuna
> nella `WITH CHECK`...").

- ✅ **Scrivere e testare le RLS per ogni tabella** — FATTO (2026-06-24). Policy su `characters`,
  `campaign_members`, `notes`, inventario/incantesimi, cataloghi e `campaigns`: un Player legge/modifica solo
  ciò che gli compete; le note private restano del proprietario. Chiusi i due gap emersi dall'audit:
  `combat_state` (era `ALL true/true` → ora scrittura al solo master) e `campaign_members_insert` (consentiva
  l'auto-promozione a master → ora i join passano dalla RPC `join_campaign`). Verificato a due account + REST.
- ✅ **Spostare le autorizzazioni sul server** — FATTO. Ruolo e proprietà (`isMaster`, owner del PG) sono
  applicati via RLS basate su `auth.uid()` e sugli helper `is_campaign_member`/`is_campaign_master`, non più
  solo nella UI.
- 🟡 **Vincoli e validazione a livello DB.** ✅ Integrità referenziale: l'audit (2026-06-24) ha confermato
  **FK + `ON DELETE CASCADE`** già presenti su tutte le relazioni verso `campaigns`/`characters` (gli
  `added_by` dei cataloghi sono `SET NULL`, corretto). ✅ **Validazione di dominio lato client** (2026-06-24):
  helper puro testato `Services/FormValidation.cs` (`ValidateMonster`/`ValidateRace`/`InRange`, 11 test);
  form Mostri (caratteristiche 1–30, CA 0–40) e Razze (velocità 0–120) ora validano con messaggi chiari
  (Incantesimi/Personaggi erano già coperti: livello 0–9 / `CharacterNormalizer`). ✅ **`CHECK` sul dominio
  di `speed_unit`** (Task 4 del modello 2024, 2026-07-25): `races.speed_unit` ora accetta solo `'m'`/`'ft'`
  a livello DB, non solo lato client (`supabase/migrations/20260726000000_catalog_packages.sql`).
  **Resta (a livello DB):** `NOT NULL`, lunghezze e gli altri `CHECK` SQL sui range numerici
  (caratteristiche, CA, velocità) — l'accesso alle migrazioni Supabase non è più un ostacolo, dimostrato
  dalla migrazione del Task 4.
- 🟡 **Header di sicurezza.** ✅ **CSP in `<meta>`** (2026-06-24): `default-src 'self'`, `connect-src` ai soli
  self+Supabase (blocca esfiltrazione), `object-src 'none'`, `base-uri 'self'`, `script-src` con
  `'unsafe-inline'` + `'wasm-unsafe-eval'`. Scelta pragmatica: l'approccio a hash è insostenibile perché
  .NET inietta un `<script type="importmap">` auto-generato il cui contenuto cambia ad ogni build (motivazione
  completa nel commento accanto al `<meta>` in `wwwroot/index.html`). Verificato in locale (boot pulito,
  login/CRUD ok). **Resta:** GitHub Pages non
  permette header HTTP → `frame-ancestors` (anti-clickjacking)/HSTS/`report-uri` non ottenibili via `<meta>`;
  servirebbe un hosting con controllo header.
- 🟡 **Lacuna nella `WITH CHECK` di update ("campaign hopping" dell'autore) — cataloghi, personaggi e note.** Scoperta in
  revisione durante il Task 4 del modello 2024 (`backgrounds`, 2026-07-25): le policy `*_update` di
  `races`/`classes`/`spells`/`monsters`/`characters` (e ora `backgrounds`, che le ricalca fedelmente) hanno
  `USING`/`WITH CHECK` identiche e simmetriche (`added_by = auth.uid() OR is_campaign_master(campaign_id)`;
  su `characters` la stessa struttura usa `owner_id` al posto di `added_by`).
  Siccome quella colonna non cambia con uno spostamento, per l'**autore/proprietario** di una riga la `WITH CHECK` resta
  sempre vera indipendentemente dalla campagna di destinazione: via REST diretto (non esposto dalla UI
  attuale, che non offre un modo di riassegnare `campaign_id`) un giocatore potrebbe spostare una propria
  riga di catalogo verso una campagna di cui non è membro. La `WITH CHECK` protegge invece correttamente il
  caso "un master sposta una riga altrui fuori dalla propria autorità" (verificato con un test dedicato,
  `Tests.Integration/BackgroundsRlsIntegrationTests.cs`). **Non corretto** in Task 4: irrobustirlo
  significherebbe divergere da `races_update` e affini su più tabelle già in produzione — decisione fuori
  mandato di quel task, serve conferma esplicita. Piste se si deciderà di chiuderla: `WITH CHECK` che leghi
  il ramo autore alla membership di destinazione (`(added_by = auth.uid() AND is_campaign_member(campaign_id))
  OR is_campaign_master(campaign_id)` — permetterebbe comunque lo spostamento verso campagne di cui l'autore
  è già membro) oppure un trigger `BEFORE UPDATE` che confronti `OLD.campaign_id`/`NEW.campaign_id`.
  **Portata reale (rivista il 2026-07-25 dopo verifica sulle policy — la prima stima era troppo generosa):**
  le tabelle colpite sono **sette, non sei**. L'unica mancante dall'elenco sopra è **`notes`**
  (`notes_update` è `USING/WITH CHECK (owner_id = auth.uid())`, senza alcun vincolo sulla destinazione);
  `characters` c'era già, ma se ne sottovalutava l'effetto — sopra si parla solo di spostare «una propria
  riga di catalogo», e non è il caso peggiore.
  **In tutti e sette i casi la riga entra nella campagna bersaglio** e diventa visibile ai suoi membri: è
  sempre iniezione. Quello che cambia è **se la vittima può ripulire** e se l'autore mantiene la vista:
  - **cataloghi** — la voce compare nel catalogo della campagna bersaglio; il master di quella campagna
    **può rimuoverla** (`*_update`/`*_delete` hanno il ramo `is_campaign_master`). L'autore perde la vista
    (`*_select` richiede `is_campaign_member`) ma non il controllo: il ramo `added_by = auth.uid()` di
    `USING` non dipende dalla campagna, quindi continua a modificarla e cancellarla **per id**;
  - **`characters`** — il PG compare fra i personaggi della campagna bersaglio e nell'import del tracker
    combattimento; anche qui il master **può rimuoverlo**. In più l'autore **non perde l'accesso**, perché
    `characters_select` ha il ramo `owner_id = auth.uid()` (dall'app il PG sparisce comunque dal suo elenco,
    che filtra per `campaign_id`; resta raggiungibile per id via REST);
  - **`notes`** — con `is_shared = true` la nota si riversa nelle **note condivise** della campagna
    bersaglio, e **nessuno può rimuoverla**: `notes_update`/`notes_delete` hanno il solo ramo
    `owner_id = auth.uid()`, senza alcun ramo master. La vittima non ha rimedio applicativo. È il caso
    peggiore dei sette, ed è questa la ragione — non la sola visibilità.

  Resta vero che non c'è né lettura di dati altrui né escalation di ruolo: è **iniezione di contenuto**, non
  esfiltrazione. Ma "vandalismo mirato" descrive male il caso `notes`.
  **L'uuid della campagna bersaglio non è una barriera** per l'attaccante plausibile: un ex-membro lo
  conserva (lo rilegge dai propri PG e dalle proprie note rimasti lì, che `owner_id = auth.uid()` gli lascia
  vedere anche dopo la rimozione), e `find_campaign_by_invite_code` è `SECURITY DEFINER` concessa ad `anon`:
  chiunque abbia visto un codice invito ottiene l'uuid senza unirsi. Non si indovina, ma non serve.
  **Priorità:** la voce resta 🟡 e non 🟢 nonostante l'assenza di esfiltrazione, perché §1 è il gate di
  pubblicazione e questo è l'unico varco di scrittura **fra campagne** noto.
  **Da valutare nella stessa migrazione, il caso gemello:** il ramo autore/proprietario di
  `*_update`/`*_delete` non richiede la membership **corrente**, quindi un ex-membro mantiene scrittura e
  cancellazione su tutte le proprie righe rimaste in campagna — note, personaggi, voci di catalogo — senza
  alcuno spostamento. Il caso più acuto è di nuovo la nota condivisa, che nessun altro può rimuovere.
  **Come chiuderla:** migrazione **autonoma**, con il suo giro di test RLS — sette tabelle, di cui sei già in
  produzione (`backgrounds` ci arriverà col deploy della Fase 1). **Aggiornamento (2026-07-29):** la Fase 2 si
  è chiusa senza toccare le policy, come previsto — la finestra indicata («in prossimità della Fase 2») **è
  ora**, fra Fase 2 e Fase 3, e **comunque prima di aprire l'app al pubblico**, coerentemente con §10 punto 2.

---

## 2. Bundle & dipendenze

- ✅ **Eliminare Realtime / `System.Reactive`.** — FATTO (2026-06-24). Il meta-pacchetto `supabase-csharp`
  è stato sostituito dagli standalone `postgrest-csharp 3.5.1` + `gotrue-csharp 4.2.7`; rimossi
  `realtime-csharp`, `supabase-storage`, `System.Reactive` e `Websocket.Client`. Auth e dati vivono dietro
  la facade `Services/SupabaseClient.cs` (`From<T>`/`Rpc<T>`/`Auth`), a superficie invariata per tutti i
  repository e le pagine. Token per-request via `GetHeaders`. Build 0/0, 111 test verdi. Il combat resta a
  polling (§8) — il punto di tensione non esiste più. Verifica runtime manuale (login/CRUD/RLS) in sospeso
  prima del push.
- ✅ **Misurare il bundle pubblicato** — FATTO (2026-06-24). Confronto publish Release `before` (commit
  `f84e133`, meta `supabase-csharp 0.16.2`) vs `after` (`main`, split standalone) su `wwwroot/_framework`:
  **−9 assembly** (77 → 68), **−272 KB** RAW (10.62 → 10.35 MB), **−124 KB Brotli** (3.57 → 3.45 MB),
  −160 KB Gzip. Eliminati: `Supabase`(meta)/`Supabase.Realtime`/`Supabase.Functions`/`Supabase.Storage`,
  `System.Reactive`, `Websocket.Client`, `System.Net.WebSockets`(+`.Client`), `System.Threading.Channels`.
  **Smoke test trim `full`:** publish exit 0, 0 avvisi, gli assembly radicati `Supabase.Gotrue`/`Supabase.Postgrest`
  presenti → nessun ctor strippato. Il delta è modesto perché `TrimMode=full` già sfrondava `System.Reactive`
  (70.8 KB trimmato nel `before`); il guadagno vero è rimuovere **9 file interi** (meno richieste/decompressione
  al cold-load). ⚠️ Numeri assoluti misurati **senza** workload `wasm-tools` (non installato in locale): in
  produzione la CI fa `dotnet workload restore` → relinking nativo del `dotnet.native.wasm` (2.9 MB) → bundle
  reale più piccolo. Il *delta* del taglio resta valido.
- ✅ **Indagine `System.Private.Xml`** — FATTO (2026-06-24, dump dipendenze del trimmer). I ~1.4 MB di
  `System.Private.Xml` (+ `System.Private.Xml.Linq`) sono trascinati da `Newtonsoft.Json.Converters.XmlNodeConverter`
  (col suo `XObjectWrapper`/`XContainerWrapper`); il trimmer non può eliminarlo perché Newtonsoft produce trim
  warning (IL2104, reflection). **Non eliminabile in sicurezza** finché Newtonsoft è il serializzatore dei Model
  Postgrest (vedi sotto): si libererà da solo quando Supabase mollerà Newtonsoft. (Collaterale: anche
  `System.Data.Common` ~463 KB nel bundle, target separato.)
- ℹ️ `Newtonsoft.Json` **non è rimuovibile** finché si usa Supabase 0.16.x (serializzatore runtime dei Model).

---

## 3. Architettura & manutenibilità

- ✅ **Spezzare `Characters.razor`** — FATTO (Fase 2B, 2026-06-24). I 5 tab **e** il form di modifica/creazione sono
  componenti in `Shared/CharacterTabs/` (`CharacterBioTab`/`StatsTab`/`CombatTab`/`ItemsTab`/`MagicTab` +
  `CharacterEditForm`) con helper `CharacterView`; la pagina è scesa da ~2.4k a ~660 righe, comportamento invariato.
  Il genitore resta proprietario di stato/persistenza (draft + `NormalizeDraft`/`SaveFormAsync`, inventario,
  catalogo incantesimi). Restano (indipendenti) le sotto-fasi A (repository) e C (stato auth) qui sotto.
- ✅ **Spezzare `SupabaseService` (god-object, ~40 metodi)** — FATTO (sotto-fase A, 2026-06-24). 11 repository per
  aggregato dietro interfacce in `Services/Repositories/` (`ICharacterRepository`, `ISpellRepository`,
  `IMonsterRepository`, `INoteRepository`, `ICombatStateRepository`, `IProfileRepository`, `IRaceRepository`,
  `IClassRepository`, `IInventoryRepository`, `ICharacterSpellRepository`, `ICampaignRepository`). `SupabaseService`
  resta il **provider di sessione/client** (`GetClientAsync` + bootstrap OAuth/refresh), da 577 a 127 righe. I
  consumatori iniettano i repo; abilita il mocking nei test (§4). Comportamento invariato, build 0/0 + 62 test.
- ✅ **Centralizzare lo stato di auth/ruolo** — FATTO (sotto-fase C, 2026-06-24). Nuovo `CurrentUserService`
  (facade su `AuthStateService` + `CampaignStateService`): espone `UserId`/`DisplayName`/`IsMaster`/`CampaignId`
  dietro un'unica `EnsureLoadedAsync()`. Le 7 pagine dati hanno sostituito il boilerplate ripetuto
  (`InitializeAsync` + lettura di `userId`/`isMaster`/`campaignId` + 3 campi locali) con una sola chiamata,
  leggendo dal facade. Rimosso `AuthStateService.GetRoleAsync()` (era codice morto: il ruolo vive già in
  `CampaignStateService`). `Home` resta hub auth/campagna. Con questo la **§3 è completa**.
- 🟡 **Gestione errori coerente.** ✅ `<ErrorBoundary>` in `MainLayout` (fallback a tema + "Ripara e ricarica"),
  `DbErrorBanner` centralizzato, e firme `Delete` dei repository ora **coerenti** (tutte `Task`;
  `RemoveCharacterSpellAsync` non ritorna più un `bool` sempre `true` con ramo `else` morto).
  **Indagine (2026-06-24):** far ritornare ai `Delete` l'esito reale (per intercettare il blocco RLS silenzioso)
  **non è fattibile in modo pulito con supabase-csharp 0.16.2** — `Table.Delete(QueryOptions)` ritorna `void`
  (niente `Models`) e col default segnala "successo" anche quando l'RLS blocca la cancellazione (0 righe; bug noto
  `postgrest-csharp` #91). Gli errori HTTP/rete lanciano comunque `PostgrestException` (gestiti dai try/catch →
  banner). Il blocco RLS silenzioso **non si presenta nell'uso normale** perché la UI fa da gate via
  `CanEdit`/`AccessControl` (speculare alle RLS). **Da rivalutare** su upgrade libreria (Delete che ritorni la
  rappresentazione) o con un check di esistenza post-delete (round-trip extra).
  **Decisione (2026-06-24): accettato** lo stato attuale del delete-outcome (il gate `CanEdit` copre il caso
  pratico); si rivaluta solo su upgrade della libreria.
  ✅ **Precedente (2026-07-29, Fase 2):** sulle cancellazioni **in blocco** il check post-delete è stato
  applicato davvero — la rimozione per provenienza riconta gli id congelati (`CatalogRemovalPlan.StillPresent`)
  invece di assumere l'esito, e il resoconto dice quante voci il server non ha tolto. Lì il round-trip extra
  vale il prezzo; sulle singole cancellazioni della UI resta valida la decisione del 2026-06-24.
  ✅ **Toast sugli errori di validazione** (2026-06-24): i messaggi di validazione input (8 pagine) ora sono
  toast (`Toasts.ShowError`) invece del banner; gli errori di sistema/operazione restano nel banner persistente
  (con "Ripara e ricarica"). **Bug risolto nello stesso giro:** tutti i toast erano invisibili per una collisione
  con la classe `.toast` di Bootstrap (`.toast:not(.show){display:none}`) → rinominate le classi in `.app-toast`.
- ✅ **Deduplicare il parsing dei dadi vita** — FATTO (2026-06-21): estratto `CharacterCalculations.GetHitDiceTotal(string?)`,
  riusato da `GetHitDiceRemaining` e da `Characters.razor.HitDiceTotal()`. Coperto da test (8 casi).
- ✅ **Manutenzione CI: GitHub Actions del deploy** — FATTO (2026-06-24). Bump alle ultime major (verificate via
  API GitHub `releases/latest`): `checkout` v4→**v7**, `setup-dotnet` v4→**v5**, `configure-pages` v4→**v6**,
  `upload-pages-artifact` v3→**v5**, `deploy-pages` v4→**v5**. Esce dal runtime Node 20 in deprecazione. Verificato
  con un **run di prova reale** (il push stesso): deploy `success` (1m54s) + sito live che boota pulito. Il web
  search dava versioni sbagliate → fidarsi dell'API GitHub.

---

## 4. Test

- ✅ **Suite di test** — progetto `DndCompanion.Tests` (xUnit), **387 unit test** (220 → 285 con la Fase 1 del
  modello 2024, 285 → 387 con la Fase 2) + **suite d'integrazione RLS** (`Tests.Integration/`, 11 scenari verdi
  su stack locale, vedi voce 5 — restano 11: la Fase 2 non tocca le policy). Coperti: `CharacterCalculations`
  (modificatori, competenza, TS/skill, iniziativa, percezione passiva, spellcasting, dadi vita incl. parsing
  `HitDiceMax`); la **logica pura dei repository** (estratta in helper `internal static`, esposti via
  `InternalsVisibleTo`): visibilità/ordinamento note (`NoteRepository.FilterAndSortVisible`, regola di sicurezza),
  ordinamento inventario (`InventoryRepository.SortForDisplay`), codice invito (`CampaignRepository.GenerateInviteCode`);
  e la **logica di dominio estratta dai `.razor`**: `CharacterNormalizer.Normalize` (trim/null/clamp del draft PG),
  `AccessControl.CanEdit` (autorizzazione master-o-proprietario), il JOIN incantesimi/orfani
  (`CharacterSpellJoin.WithCatalog`), gli helper di vista `CharacterView` (formattazione/a11y +
  mapping slot incantesimo 1-9, con valori distinti per livello), la redazione player del tracker
  (`CombatVisibility`) e il grado sfida del catalogo mostri (`MonsterCatalog.ParseChallengeRating`).
  Con la **Fase 2** (2026-07-29) si aggiungono gli helper puri dell'import/export: `PackageImportPlan`
  (esiti dell'anteprima e gate dei permessi), `PackageRowMerge` (creazione e fusione delle righe —
  l'invariante che un aggiornamento non tocchi identità, proprietà e colonne fuori formato),
  `SpellMaterialization`, `CampaignExport` (id del pacchetto, degrado delle provenienze, suffissi anti-collisione),
  `CatalogRemovalPlan` (selezione per provenienza senza `LIKE`, partizione per permessi, riconteggio) e
  `SpellClassNames`.
  Restano da coprire:
  1. ~~`CharacterCalculations`~~ ✅ · ~~Parsing `HitDiceMax`~~ ✅ · ~~Logica pura repository (note/inventario/invito)~~ ✅
  2. ~~Normalizzazione/clamp dei form PG (`NormalizeDraft`)~~ ✅ (`CharacterNormalizer`)
  3. ~~Autorizzazioni (`CanEdit`/`isMaster`)~~ ✅ (`AccessControl`, usato da tutte le pagine) — **irrobustito
     (2026-07-23):** `CanEdit` esclude il match degenere `null==null` / `""==""`, così il gate client combacia
     con la RLS (riga 51 spec RLS: seed `added_by` NULL → solo master).
  4. ~~Filtro/JOIN incantesimi del PG (gestione orfani)~~ ✅ (`CharacterSpellJoin.WithCatalog`)
  5. ~~Test d'integrazione sulle **RLS**~~ ✅ **FATTO (2026-06-24).** Progetto separato `Tests.Integration/`
     (xUnit + `Xunit.SkippableFact`) che gira contro uno **stack Supabase locale** (`supabase start`) il cui
     schema+policy sono importati da produzione (`supabase/migrations/*_remote_schema.sql`). 6 scenari verdi:
     un player non legge la nota privata altrui ma sì la condivisa; un non-membro non vede nulla; il proprietario
     vede le proprie; un player non scrive `combat_state`; niente auto-promozione a master. **Auto-skip** se lo
     stack locale non è attivo (non rompe CI/altre macchine). Istruzioni in `Tests.Integration/README.md`.
     **+5 scenari con `backgrounds` (2026-07-25) → 11 in tutto**, in
     `Tests.Integration/BackgroundsRlsIntegrationTests.cs`.
- 🟡 **Refactoring abilitanti**: ✅ interfacce sui repository (sotto-fase A) + estrazione di helper puri
  testabili dai repository e dai `.razor` (`CharacterNormalizer`, `AccessControl`). **Resta:** per testare interi
  componenti (rendering/eventi) servirebbe bUnit; per ora si estrae la logica pura man mano.

---

## 5. Performance

- 🟡 **Caricamento intere tabelle filtrate nel client.** La mappatura nickname scarica più del necessario:
  esporre una view nickname-only (richiede vista DB). **Note (2026-06-24):** tentato il filtro di visibilità
  server-side nella query (`.Where(... && (IsShared || OwnerId == userId))`) ma **postgrest-csharp 3.5.1 va in
  NullReferenceException** sul predicato con OR annidato → ripristinata la query per-campagna + filtro client.
  Non è una perdita: **l'RLS filtra già le note per visibilità lato server**, quindi non si scaricano note
  private altrui. Resta aperta solo la view nickname-only. (Si lega alla sicurezza, §1.)
- 🟡 **Virtualizzazione liste — riaperta il 2026-07-29 (Fase 2).** ⛔ **Era stata scartata a questi volumi
  (2026-06-24)**, decisione confermata dall'utente: i
  cataloghi restano sotto le ~50 voci, dove `<Virtualize>` non dà beneficio percepibile e la memoizzazione del
  filtro su 50 elementi è microsecondi (YAGNI). Inoltre le card sono espandibili (altezza variabile), caso ostico
  per `<Virtualize>`. **Da rivalutare solo se i cataloghi crescono** (es. import massivo / generazione AI, §8).
  ⚠️ **Rimessa in gioco dal design del 2026-07-25** (§8-bis): un pacchetto SRD completo supera la soglia delle
  ~50 voci su cui poggiava la decisione — è il trigger di rivalutazione che era stato dichiarato.
  **Dalla Fase 2 (2026-07-29) non è più un'ipotesi:** l'import di un file esiste in codice, quindi la soglia
  si supera già oggi con un pacchetto dell'utente, senza attendere il contenuto SRD della Fase 3. È per
  questo che la voce torna 🟡: il «da rivalutare **a pacchetto pieno**, non prima» del piano di Fase 2 è
  superato — il pacchetto pieno non serve, basta un file dell'utente.
- 🟡 **Cache dati semi-statici** (razze/classi/catalogo spell) in memoria con invalidazione esplicita.
  **Rialzata da 🟢 a 🟡 il 2026-07-25** (§8-bis): senza barra di navigazione ogni spostamento passa da
  Home e ricarica tutto, e 4 pagine su 6 rifanno anche `GetProfilesAsync()` a ogni ingresso.
- ✅ **Stati di caricamento** — FATTO (2026-06-24). I "Caricamento..." testuali rimasti (Incantesimi, Mostri,
  Classi, Razze, Note) ora usano `<LoadingSpinner>` a tema (già usato da Combat/inventario). Skeleton non fatto
  (spinner sufficiente).

---

## 6. UI / UX / Accessibilità

- ✅ **Design token** — FATTO (2026-06-21): palette in `:root` (`app.css`) + **conversione dei literal in tutti
  i `.razor.css`** (376 sostituzioni 1:1, valori identici → nessun cambiamento visivo). ✅ **Token alpha/rgba
  (2026-07-23):** aggiunti **19 canali `--X-rgb`** in `:root` e convertiti i ~363 literali `rgba(<tripla>, α)`
  del CSS di progetto in `rgba(var(--X-rgb), α)` (mapping 1:1, **invariato**; Bootstrap vendored escluso). Spec in
  `docs/superpowers/specs/2026-07-23-css-alpha-tokens-design.md`. **Resta (idea):** consolidare le sfumature
  quasi-duplicate (6 rossi / 4 verdi / 6 oro-bronzo) in meno token — è un cambio di colore, decisione separata.
  Riferimento visivo: `/_showroom`. **Resta (2026-07-27, Fase 2 Task 7):** aggiunto `--author-badge-text`
  (`#b89a80`, stesso valore del literal preesistente) per `.author-badge` in `Pages/Monsters.razor.css`.
  `Pages/Spells.razor.css`, `Pages/Races.razor.css` e `Pages/Classes.razor.css` hanno lo stesso badge
  con lo stesso colore ancora hardcodato (`Pages/Notes.razor.css` usa invece già `var(--gold)`, colore
  diverso: non è nel novero): da convertire al token in un passaggio
  dedicato (nessun cambio visivo, stesso valore esatto).
- 🟡 **Accessibilità** — ✅ avanzato (2026-06-21): resi accessibili da **tastiera** (`role`/`tabindex`/
  `aria-pressed`/`aria-expanded` + Enter/Space, additivi e senza impatto visivo) i controlli interattivi
  principali: `StatCard` (pallini TS/skill), `SpellListItem` (prep-toggle + header) e in `Characters.razor`
  i tiri salvezza morte, l'ispirazione e gli slot incantesimo; `aria-label` sui pulsanti icona-pura di Combat
  (PF +/−, rimuovi). ✅ `aria-label` sui 6 FAB "+" (Spells/Monsters/Races/Notes/Classes/Characters) — 2026-06-24.
  ✅ `DbErrorBanner`: chiusura ora con un vero pulsante **✕** (`aria-label="Chiudi"`, da tastiera) al posto del
  click-sul-testo — 2026-06-24. **Contrasti:** ✅ alzato `--gold-dim` (#8b6f3a → #b08842) per la leggibilità su fondo scuro — da
  verificare a vista e affinare se serve (cambia i testi/bordi "spenti" ovunque, via token).
- 🟡 **Feedback azioni** — ✅ fatto (2026-06-21): infrastruttura toast (`ToastService` + `ToastHost` nel
  layout, auto-dismiss, a tema con i token); conferma "✓ Salvato/Eliminato" sul salvataggio del form PG
  (`SaveFormAsync`) e su
  **tutti i CRUD** dei cataloghi (Spell/Monster/Race/Class) e delle Note. **dialog di conferma a tema**
  (`ConfirmService` + `ConfirmDialog`) al posto di **tutti** i `confirm()` nativi (10 punti in 8 pagine). ✅ fatto.
  ⚠️ **Precisazione (2026-07-25):** i salvataggi *impliciti* dei tab della scheda (`SaveCharacterAsync`)
  restano **silenziosi** — segnalano solo gli errori. Punto riaperto in §8-bis.

---

## 7. Internazionalizzazione

- 🟡 **i18n.** Tutte le stringhe UI sono hardcodate in italiano. Se l'inglese entra in roadmap (Play Store
  globale), estrarre in risorse `.resx` + `IStringLocalizer`. Altrimenti accettare consapevolmente IT-only.

---

## 8. Funzionalità emerse dall'uso (da ingegnerizzare)

> Richieste nate dall'uso reale che **non sono quick-win**: ognuna merita un proprio giro di
> brainstorming → design prima dello sviluppo.

- ✅ **Combat condiviso + polling** — FATTO e verificato (2026-06-21): tabella
  `combat_state` creata + model `CombatState`/`Combatant`; `GetCombatStateAsync`/`SaveCombatStateAsync`
  (upsert) in `SupabaseService`; `Combat.razor` carica/salva lo stato — il Master fa upsert a ogni azione, i
  giocatori (non-master) leggono con **polling ~4s**. **Da verificare a vista:** serializzazione jsonb dei
  combattenti, l'upsert, e che il giocatore veda i cambi del Master. Con RLS permissive funziona, andrà
  protetto (§1). Limite noto: l'iniziativa modificata inline si persiste al successivo salvataggio
  (es. "Ordina"/"Prossimo turno"), non all'istante.
- ✅ **Import mostri nel combattimento.** — FATTO (2026-06-24). Pannello inline master-only "Importa mostri"
  in `Combat.razor` (lazy-load via `IMonsterRepository`, stepper quantità per mostro, "Aggiungi N combattenti"
  → `SaveCombatStateAsync`). Helper puro `Services/CombatImport.cs` testato (xUnit): `ParseLeadingHp` ricava i PF
  dal **primo intero** del testo libero (fallback 1); `FromMonster(monster, quantity)` genera la lista di
  `Combatant` con nomi numerati per le copie, iniziativa 0, `CurrentHp = MaxHp`. Nessuna modifica a DB/RLS.
- ✅ **Visibilità limitata del player nel tracker** — FATTO (2026-07-23). Il giocatore vede **solo la propria
  scheda** (PF/iniziativa) e degli altri **solo il nome** (niente statistiche né ordine di turno); riceve il
  segnale "È il tuo turno!" ma l'indicatore non svela mai di chi sia il turno corrente. Aggancio "riga mia" via
  `owner_id` marcato all'import (nuovo campo su `Combatant`, `jsonb` → nessuna migrazione); helper puro testato
  `Services/CombatVisibility.cs`; `Combat.razor` biforca player/master. Redazione **cosmetica lato UI** (i dati
  grezzi restano nel browser via polling), nessun cambio a DB/RLS. Spec/piano in `docs/superpowers/` (2026-07-23).
- 🟡 **Aiuto AI alla compilazione (generazione da testo).** Da una descrizione testuale, generare bozze di
  **personaggi, classi, incantesimi, razze, mostri** (estende in modo strutturale il bisogno dei quick-win C).
  ⚠️ **Da riordinare dopo il design del 2026-07-25** (§8-bis): precaricare il pacchetto SRD riduce molto ciò
  che resterebbe da generare — i due filoni vanno pianificati insieme, non separatamente.
  Requisiti emersi (2026-06-24):
  - **Accesso riservato (entitlement).** Anche con l'app pubblica la feature resta attiva **solo per un
    allowlist** (owner + amici). È una scelta di *autorizzazione server-side* (coerente con §1): vive
    naturalmente nel **proxy/edge function** che custodisce la API key dell'LLM (la anon key è già nel bundle
    → chiamate dirette dal client escluse). L'allowlist (`user_id`) sta lì → **nessuno schema DB nuovo**,
    quindi **non blocca né cambia il lavoro RLS** (§1): le policy attuali restano valide quando si aggiunge l'AI.
  - **Contesto dal manuale ufficiale.** Per ora solo **incollare testo**; in futuro valutare l'ingestione del
    manuale acquistato. ⚠️ Caveat copyright: il manuale è protetto — uso privato del gruppo, non da caricare a
    cuor leggero su provider terzi. Per la generazione *base* il modello conosce già lo **SRD 5e** (aperto): il
    manuale serve solo per contenuti non-SRD/homebrew. Se servirà ingerire molto testo la strada è **RAG**
    (chunk + embedding + retrieval), non l'intero manuale nel prompt.
  - **Provider.** Valutare opzioni gratuite: free tier di **Gemini**, **Groq** (inferenza veloce di modelli
    open — da non confondere con **Grok** di xAI). Da decidere nel brainstorm dedicato: provider, gestione
    della API key nel proxy, prompt, parsing dell'output nei Model, costi/limiti, UX. **Merita il suo spec
    separato**, da fare *dopo* le RLS.
- ✅ **Redesign del flusso scheda / wizard** — FATTO (2026-06-25). Wizard di **sola creazione** a 6 step
  (Identità → Caratteristiche → Vitalità & combattimento → Competenze → Incantesimi → Riepilogo), accessibile
  via `ViewMode.Wizard` in `Characters.razor`. Automazione intermedia: bonus razza applicati alle
  caratteristiche e dado vita pre-compilato alla scelta di razza/classe; PF e tiri salvezza suggeriti con un
  tap. Helper puri testabili in `Services/CharacterWizardLogic.cs` (`FinalAbilityScores`, `BuildHitDice`,
  `SuggestMaxHp`, `ParseSaveProficiencies`). L'accordion `CharacterEditForm` resta **invariato** per la
  modifica di PG esistenti. Zero impatto su DB/RLS (nessuna tabella nuova, `SaveFormAsync` riusato). 147 test
  verdi, build Release 0/0. Verifica manuale end-to-end (scenario spec §9 in locale) in sospeso prima del push.
- 💡 **Combat in Realtime.** Evoluzione futura del combat condiviso con push istantaneo invece del polling —
  richiederebbe la reintroduzione di `realtime-csharp` (rimosso in §2); valutare solo se il costo bundle è
  accettabile.

---

## 8-bis. Attrito d'uso: mappa UX dei flussi (2026-07-25)

> Analisi completa dei flussi in
> [`docs/superpowers/specs/2026-07-25-ux-mappa-flussi-analisi.md`](./superpowers/specs/2026-07-25-ux-mappa-flussi-analisi.md).
> Utente di riferimento: **gruppo misto con novizi**. Bersaglio di regole confermato: **D&D 5e 2024**.
> Il primo punto ha il suo design approvato (2026-07-25); gli altri richiedono ancora il proprio spec.

Cinque attriti strutturali emersi: **A1** modello dati 2014 vs bersaglio 2024 · **A2** ~670 campi da
digitare prima di poter giocare · **A3** il wizard chiede 70 controlli, ~50 derivabili · **A4** l'app
chiede risposte al novizio invece di insegnargliele · **A5** unità di velocità incoerenti e due
modelli di salvataggio opposti.

- 🟠 **Modello 2024 + import dei dati** — 📐 design approvato (2026-07-25),
  [`specs/2026-07-25-modello-2024-import-dati-design.md`](./superpowers/specs/2026-07-25-modello-2024-import-dati-design.md);
  piano in tre fasi, **Fasi 1 e 2 fatte, resta la Fase 3**:
  [`fase 1`](./superpowers/plans/2026-07-25-modello-2024-import-dati-fase-1.md) ·
  [`fase 2`](./superpowers/plans/2026-07-27-modello-2024-import-dati-fase-2.md).
  Unisce due punti che l'analisi teneva separati (modello 2024 e cataloghi precaricati): il modello è il
  prerequisito, il formato di scambio è il veicolo. Decisioni prese: pacchetto **SRD 5.2 in italiano** come
  **file dell'app** in sola lettura, unito lato client ai cataloghi di campagna; **import/export di file**
  per i contenuti dell'utente (che restano nel database); PG e cataloghi esistenti **congelati**, nessuna
  migrazione **di dati**. Sullo schema: **1 tabella nuova + 6 colonne additive su 5 tabelle esistenti +
  4 vincoli `UNIQUE` additivi** (`source_id` sui quattro cataloghi, `speed_unit` su `races`,
  `background_ability_choice` su `characters`).
  Non modifica nessuna policy esistente, ma aggiunge quelle di `backgrounds`, ricalcate su `races`: c'è
  lavoro RLS, solo confinato al nuovo.
  ⚠️ Rimette in gioco due voci già decise: la **virtualizzazione liste** (§5, scartata "sotto le ~50
  voci" — un pacchetto SRD completo supera la soglia dichiarata) e l'**aiuto AI** (§8: precaricare riduce
  molto ciò che resterebbe da generare — vanno ordinate insieme, non trattate come filoni separati).
  - ✅ **Fase 1 (leggere un pacchetto) — FATTO (2026-07-25).** Nove task: modelli del pacchetto e parser
    con validazione; chiave di confronto (`CatalogKey`) con piega accenti scritta a mano — `String.Normalize`
    **non fa nulla sotto `InvariantGlobalization`** (verificato a runtime), quindi la chiave non lo usa e
    piega gli accenti con una mappa esplicita, oltre a maiuscole e spazi — e
    riconoscimento della provenienza dal prefisso `<id pacchetto>/…`; unione fra pacchetto e cataloghi di
    campagna (righe di database sempre visibili, la chiave decide solo quale oscura la voce di pacchetto);
    migrazione schema (tabella `backgrounds` + colonne `source_id`/`speed_unit`/`background_ability_choice`
    + vincoli `UNIQUE`); model/repository/RLS dei background; `CatalogService` per caricare il pacchetto
    dell'app; esclusione del pacchetto dal precache del service worker (altrimenti un fetch fallito rompeva
    l'installazione dell'intera PWA); pagina catalogo Background in sola lettura per le voci di pacchetto;
    **unità di velocità esplicita nel form Razze** (`speed_unit`, limite 0–120 piedi o 0–36 metri, selettore
    con `aria-label`). Tutto testato (helper puri `static` — per lo più `public static`, es. `CatalogKey`/
    `CatalogMerge`/`CatalogPackageParser`; `FormValidation` resta `internal static` + `InternalsVisibleTo`
    + xUnit), 0 warning/0 errori.
    Le **quattro pagine di catalogo** (Razze, Classi, Incantesimi, Mostri) non marcano ancora le voci di
    pacchetto né offrono "duplica e modifica": in Fase 1 non ci sono ancora righe con provenienza da
    marcare (nessun import, nessun pacchetto pubblicato) — la logica (`CatalogMerge`,
    `CatalogKey.IsFromAppPackage`) è già pronta, il blocco `@code` di Background è il modello da
    replicare in Fase 2.
  - ✅ **Fase 2 (import ed export) — FATTO (2026-07-29).** Undici task, **zero migrazioni** (né schema né
    policy): `PackageImportPlan` col gate dei permessi + `PackageRowMerge`; schermata `/dati` con anteprima,
    resoconto ed export della campagna; rimozione per provenienza con anteprima dell'impatto; materializzazione
    degli incantesimi su uso; filtro per classe che riconosce nomi italiani e inglesi (`SpellClassNames`);
    marcatura e "duplica e modifica" nei quattro cataloghi esistenti. 387 test unitari, build 0/0.
    Quattro decisioni degne di nota (dettaglio e motivazioni in `DIARIO.md`):
    **(a) niente `Upsert`** — `postgrest-csharp 3.5.1` serializza la chiave primaria anche con
    `[PrimaryKey("id", false)]`, quindi manda `"id":""` e prende HTTP 400 su ogni scrittura (misurato
    intercettando le richieste): creazioni con `Insert` in blocco, aggiornamenti riga per riga,
    materializzazione con rilettura sul conflitto. Le occorrenze di `Upsert` nello spec §4.4 e §9 sono state
    corrette di conseguenza. **(b) gli aggiornamenti fondono** invece di sostituire, altrimenti un reimport
    azzererebbe le colonne che il formato non trasporta senza cambiare il conteggio delle righe.
    **(c) `SkippedLocalWins`**: a parità di solo nome vince la riga dell'utente, e marcarla `Create` avrebbe
    creato un doppione (un `source_id` nullo non collide con `UNIQUE`). **(d) nessun `LIKE` con testo
    digitato** nella rimozione: `_` e `%` sarebbero wildcard e cancellerebbero il manuale — filtro in memoria
    (`CatalogRemovalPlan`) e `DELETE` per elenco di id.
    **Conseguenze note:** i mostri di pacchetto non compaiono nel pannello "Importa mostri" del tracker
    finché non sono duplicati in campagna; un file esportato perde la provenienza delle righe materializzate
    dal manuale (diventano contenuti di campagna — voluto: l'alternativa era iniettare righe intoccabili in
    campagne che non hanno mai importato nulla di ufficiale).
    **Resta da fare:** la verifica manuale end-to-end (import di un pacchetto di prova + rimozione con un
    secondo account non-master), mai eseguita.
  - 🟠 **Fase 3 (contenuto e wizard 2024)** — non iniziata: campione SRD per validare il formato sul campo,
    traduzione del pacchetto completo, wizard che prende i bonus dal background con ripartizione, tetto di
    20 e convivenza con le specie legacy.
    ⚠️ **Da decidere PRIMA di tradurre, limite del formato emerso in Fase 2:** `PackageSpeed.Value` è un
    `int`, e un decimale nel JSON fa fallire la deserializzazione dell'**intero** pacchetto, non della singola
    voce. **Il caso si presenta già nel contenuto ufficiale:** la gran parte delle specie 2024 sta a 30 piedi
    (9 m), ma il **Golia** è a 35 piedi — **10,5 m** — e alla stirpe **Elfo dei boschi** la velocità sale
    a 35 piedi (verificato sul PHB 2024 in `docs/`). Il caso si estende poi a pacchetti di terzi e a
    conversioni di contenuto 2014, dove 25/15/5 piedi diventano 7,5/4,5/1,5 m.
    ⚠️ **Del piano di Fase 2 sono superate entrambe le affermazioni su questo punto:** l'esempio (il Nano a
    7,5 m è regola 2014 — nel 2024 è a 30 piedi) e la stima di costo, che dava le tre opzioni tutte a costo
    zero. **Non costano uguale:** arrotondare in traduzione non tocca codice ma **falsifica un dato del
    manuale** (10,5 → 10 o 11) e risolve **solo il pacchetto dell'app** — un `"value": 7.5` in un file di
    terzi continuerebbe a far fallire la lettura dell'intero pacchetto; passare a `decimal` significa toccare `PackageSpeed.Value`, `Race.Speed`
    (`int`), `PackageRowMerge`, `CampaignExport`, i punti d'uso a valle (`FormValidation.ValidateRace`/
    `InRange`, il record `Entry` di `Pages/Races.razor`) **e** migrare la colonna `races.speed` (`integer`);
    esprimere in centimetri richiede di estendere `PackageRowMerge.UnitaValida` e il `CHECK`
    `races_speed_unit_check` (che ammette solo `'m'`/`'ft'`), altrimenti un `"unit":"cm"` finisce nel fallback
    e 750 cm vengono salvati e mostrati come **750 m**, in silenzio.
- 🟠 **Motore di derivazione condiviso** (slot, PF, competenze, taglia, velocità) usato da creazione,
  **modifica e level-up** insieme — oggi wizard e `CharacterEditForm` duplicano il markup e solo il
  wizard suggerisce qualcosa.
- 🟠 **Level-up guidato** — oggi inesistente: salire di livello è editare a mano PF, dadi vita, 9 slot
  e competenze. È l'attrito che si ripresenta a **ogni sessione di gioco**.
- 🟡 **Aiuto contestuale dal manuale** — nessuna spiegazione di cosa siano tiro salvezza, competenza,
  CD incantesimo. Indipendente dai punti sopra.
- 🟡 **Barra di navigazione + cache dei cataloghi** — oggi ogni spostamento passa da Home e ricarica
  tutto; 4 pagine su 6 rifanno anche `GetProfilesAsync()`. Allinea la voce cache di §5 (era 🟢).
- 🟡 **Combat: iniziativa precompilata o tirata** — gli import mettono `Initiative = 0` per tutti,
  benché l'app conosca già il bonus di ogni PG; e i PF si regolano ±1 per click.
- 🟢 **Unificare l'unità di velocità** (razza in piedi, PG in metri). Il design del modello 2024 la chiude
  **senza migrazione di dati**, ma con una colonna additiva `speed_unit` su `races` (`default 'ft'`, così le
  righe esistenti restano come sono) e l'unità mostrata accanto al campo. Dedurla dalla sorgente non bastava:
  si sarebbe rotta sulle voci di pacchetto duplicate in campagna e su quelle create a mano dopo il cambio.
- 🟢 **Conferma visibile sui salvataggi impliciti** dei tab scheda (`SaveCharacterAsync` è muto).

---

## 9. Idee aperte (da ragionare)

> Non ancora decise: spunti da valutare, non impegni.

- 💡 **Offline dei dati read-only.** Oggi offline funziona solo la shell; cache dei cataloghi per
  consultazione senza rete, se diventa una promessa del prodotto.
- 💡 **Markdown nelle note** (oggi plain text).
- 💡 **Tema chiaro / multi-tema** (sbloccato dai design token del §6).
- 💡 **Hosting alternativo** con header di sicurezza (CSP/HSTS) e dominio custom, se GitHub Pages diventa
  un limite.

---

## 10. Ordine consigliato (sintesi)

1. **Quick-win del `/loop`** (sez. 🔜 A·B·C) — basso rischio, valore immediato, sbloccano lavori successivi.
2. **Sicurezza server-side / RLS** (§1) — *gate* di pubblicazione.
3. **Integrità DB: FK + cascade** (§1) — prima che il volume pubblico generi incoerenze.
4. **Primi test su `CharacterCalculations`** (§4) — valore alto, costo basso, in parallelo.
5. **Combat condiviso** (§8) — feature più sentita dall'uso reale.
6. ~~**Rimozione Realtime** (§2)~~ ✅ e **design token / refactor `Characters.razor`** (§3, §6) — manutenibilità.
7. Il resto (AI compilazione, ~~wizard scheda~~ ✅, performance, a11y, i18n, idee) secondo priorità di prodotto.
8. **Attrito d'uso / mappa UX** (§8-bis, 2026-07-25) — **modello 2024 + import** (design approvato) viene
   prima di motore di derivazione e level-up guidato, in quest'ordine: ognuno dipende dal precedente.
   I punti indipendenti (navigazione + cache, aiuto contestuale dal manuale, iniziativa nel combat,
   conferme sui salvataggi impliciti) sono aggredibili **in parallelo**, senza attendere quella catena.
