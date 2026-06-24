# DA FARE — D&D Companion

> Cose ancora da implementare, debito tecnico da pianificare e idee aperte da ragionare.
> Per lo stato di ciò che è già fatto vedi [DIARIO.md](./DIARIO.md).
>
> Sintetizza analisi pregresse (audit sicurezza/architettura e diagnosi dipendenze) ormai integrate qui;
> riporta solo ciò che resta effettivamente aperto dopo la migrazione a Supabase Auth.
>
> Ultimo aggiornamento: **2026-06-24**
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
  (Incantesimi/Personaggi erano già coperti: livello 0–9 / `CharacterNormalizer`). **Resta (a livello DB):**
  `NOT NULL`, lunghezze, `CHECK` SQL — serve accesso alle migrazioni Supabase, non ancora fatto.
- 🟡 **Header di sicurezza.** ✅ **CSP in `<meta>`** (2026-06-24): `default-src 'self'`, `connect-src` ai soli
  self+Supabase (blocca esfiltrazione), `object-src 'none'`, `base-uri 'self'`, `script-src` con
  `'unsafe-inline'` + `'wasm-unsafe-eval'`. Scelta pragmatica: l'approccio a hash è insostenibile perché
  .NET inietta un `<script type="importmap">` auto-generato il cui contenuto cambia ad ogni build (motivazione
  completa nel commento accanto al `<meta>` in `wwwroot/index.html`). Verificato in locale (boot pulito,
  login/CRUD ok). **Resta:** GitHub Pages non
  permette header HTTP → `frame-ancestors` (anti-clickjacking)/HSTS/`report-uri` non ottenibili via `<meta>`;
  servirebbe un hosting con controllo header.

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
  ✅ **Toast sugli errori di validazione** (2026-06-24): i messaggi di validazione input (8 pagine) ora sono
  toast (`Toasts.ShowError`) invece del banner; gli errori di sistema/operazione restano nel banner persistente
  (con "Ripara e ricarica"). **Bug risolto nello stesso giro:** tutti i toast erano invisibili per una collisione
  con la classe `.toast` di Bootstrap (`.toast:not(.show){display:none}`) → rinominate le classi in `.app-toast`.
- ✅ **Deduplicare il parsing dei dadi vita** — FATTO (2026-06-21): estratto `CharacterCalculations.GetHitDiceTotal(string?)`,
  riusato da `GetHitDiceRemaining` e da `Characters.razor.HitDiceTotal()`. Coperto da test (8 casi).
- 🟢 **Manutenzione CI: aggiornare le GitHub Actions del deploy.** `deploy.yml` usa action su **Node.js 20**
  (`actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/configure-pages@v4`, `actions/upload-pages-artifact@v3`,
  `actions/deploy-pages@v4`), che GitHub sta deprecando (oggi forzate su Node 24 con warning). Bumpare alle versioni
  più recenti prima che Node 20 venga rimosso dai runner, per non rischiare la rottura del deploy.
  **Valutato nel loop (2026-06-21): NON bumpato in autonomia** — su un workflow di deploy pubblico non testabile in
  locale, un bump alla cieca (versione errata o breaking change negli input) romperebbe il deploy. Da fare verificando
  le versioni reali, idealmente con un run di prova.

---

## 4. Test

- 🟠 **Suite di test** — ✅ progetto `DndCompanion.Tests` (xUnit), **97 test**. Coperti: `CharacterCalculations`
  (modificatori, competenza, TS/skill, iniziativa, percezione passiva, spellcasting, dadi vita incl. parsing
  `HitDiceMax`); la **logica pura dei repository** (estratta in helper `internal static`, esposti via
  `InternalsVisibleTo`): visibilità/ordinamento note (`NoteRepository.FilterAndSortVisible`, regola di sicurezza),
  ordinamento inventario (`InventoryRepository.SortForDisplay`), codice invito (`CampaignRepository.GenerateInviteCode`);
  e la **logica di dominio estratta dai `.razor`**: `CharacterNormalizer.Normalize` (trim/null/clamp del draft PG),
  `AccessControl.CanEdit` (autorizzazione master-o-proprietario) e il JOIN incantesimi/orfani
  (`CharacterSpellJoin.WithCatalog`). Restano da coprire:
  1. ~~`CharacterCalculations`~~ ✅ · ~~Parsing `HitDiceMax`~~ ✅ · ~~Logica pura repository (note/inventario/invito)~~ ✅
  2. ~~Normalizzazione/clamp dei form PG (`NormalizeDraft`)~~ ✅ (`CharacterNormalizer`)
  3. ~~Autorizzazioni (`CanEdit`/`isMaster`)~~ ✅ (`AccessControl`, usato da tutte le pagine)
  4. ~~Filtro/JOIN incantesimi del PG (gestione orfani)~~ ✅ (`CharacterSpellJoin.WithCatalog`)
  5. Test d'integrazione sulle **RLS** (un utente non legge note/PG altrui) — richiede bUnit o un DB di prova.
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
- ✅/⛔ **Virtualizzazione liste — SCARTATA a questi volumi (2026-06-24).** Decisione confermata dall'utente: i
  cataloghi restano sotto le ~50 voci, dove `<Virtualize>` non dà beneficio percepibile e la memoizzazione del
  filtro su 50 elementi è microsecondi (YAGNI). Inoltre le card sono espandibili (altezza variabile), caso ostico
  per `<Virtualize>`. **Da rivalutare solo se i cataloghi crescono** (es. import massivo / generazione AI, §8).
- 🟢 **Cache dati semi-statici** (razze/classi/catalogo spell) in memoria con invalidazione esplicita.
- ✅ **Stati di caricamento** — FATTO (2026-06-24). I "Caricamento..." testuali rimasti (Incantesimi, Mostri,
  Classi, Razze, Note) ora usano `<LoadingSpinner>` a tema (già usato da Combat/inventario). Skeleton non fatto
  (spinner sufficiente).

---

## 6. UI / UX / Accessibilità

- ✅ **Design token** — FATTO (2026-06-21): palette in `:root` (`app.css`) + **conversione dei literal in tutti
  i `.razor.css`** (376 sostituzioni 1:1, valori identici → nessun cambiamento visivo). **Resta (minore):** le
  `rgba()` con alpha (bordi/ombre oro semitrasparenti) e i pochi colori unici non hanno un token diretto —
  valutare se aggiungere token con alpha. Riferimento visivo: `/_showroom`.
- 🟡 **Accessibilità** — ✅ avanzato (2026-06-21): resi accessibili da **tastiera** (`role`/`tabindex`/
  `aria-pressed`/`aria-expanded` + Enter/Space, additivi e senza impatto visivo) i controlli interattivi
  principali: `StatCard` (pallini TS/skill), `SpellListItem` (prep-toggle + header) e in `Characters.razor`
  i tiri salvezza morte, l'ispirazione e gli slot incantesimo; `aria-label` sui pulsanti icona-pura di Combat
  (PF +/−, rimuovi). ✅ `aria-label` sui 6 FAB "+" (Spells/Monsters/Races/Notes/Classes/Characters) — 2026-06-24.
  ✅ `DbErrorBanner`: chiusura ora con un vero pulsante **✕** (`aria-label="Chiudi"`, da tastiera) al posto del
  click-sul-testo — 2026-06-24. **Contrasti:** ✅ alzato `--gold-dim` (#8b6f3a → #b08842) per la leggibilità su fondo scuro — da
  verificare a vista e affinare se serve (cambia i testi/bordi "spenti" ovunque, via token).
- 🟡 **Feedback azioni** — ✅ fatto (2026-06-21): infrastruttura toast (`ToastService` + `ToastHost` nel
  layout, auto-dismiss, a tema con i token); conferma "✓ Salvato/Eliminato" su `SaveCharacterAsync` e su
  **tutti i CRUD** dei cataloghi (Spell/Monster/Race/Class) e delle Note. **dialog di conferma a tema**
  (`ConfirmService` + `ConfirmDialog`) al posto di **tutti** i `confirm()` nativi (10 punti in 8 pagine). ✅ fatto.

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
- 🟡 **Aiuto AI alla compilazione (generazione da testo).** Da una descrizione testuale, generare bozze di
  **personaggi, classi, incantesimi, razze, mostri** (estende in modo strutturale il bisogno dei quick-win C).
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
- 🟡 **Redesign del flusso scheda / wizard.** I quick-win C sono un tampone; il vero rimedio a "troppi posti
  da compilare, nessuna scaletta" è un **wizard guidato** di creazione/compilazione, da fare insieme al
  refactor di `Characters.razor` (§3).
- 💡 **Combat in Realtime.** Evoluzione futura del combat condiviso con push istantaneo invece del polling —
  richiederebbe la reintroduzione di `realtime-csharp` (rimosso in §2); valutare solo se il costo bundle è
  accettabile.

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
7. Il resto (AI compilazione, wizard scheda, performance, a11y, i18n, idee) secondo priorità di prodotto.
