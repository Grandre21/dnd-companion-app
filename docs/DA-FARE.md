# DA FARE — D&D Companion

> Cose ancora da implementare, debito tecnico da pianificare e idee aperte da ragionare.
> Per lo stato di ciò che è già fatto vedi [DIARIO.md](./DIARIO.md).
>
> Sintetizza analisi pregresse (audit sicurezza/architettura e diagnosi dipendenze) ormai integrate qui;
> riporta solo ciò che resta effettivamente aperto dopo la migrazione a Supabase Auth.
>
> Ultimo aggiornamento: **2026-06-21**

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
- 🟠 **Gate di registrazione/ingresso.** Se l'app diventa a pagamento, legare l'accesso all'entitlement
  d'acquisto (Play Billing) anziché a un codice invito; validare i codici invito server-side
  (monouso/scadenza) se restano.
- 🟡 **Vincoli e validazione a livello DB.** ✅ Integrità referenziale: l'audit (2026-06-24) ha confermato
  **FK + `ON DELETE CASCADE`** già presenti su tutte le relazioni verso `campaigns`/`characters` (gli
  `added_by` dei cataloghi sono `SET NULL`, corretto). **Resta:** `NOT NULL`, lunghezze, `CHECK` su
  livelli/punteggi (validazione di dominio, non ancora a livello DB).
- 🟢 **Header di sicurezza.** GitHub Pages non permette CSP/HSTS via header: valutare almeno una CSP in
  `<meta>`, o un hosting con controllo header.

---

## 2. Bundle & dipendenze

- 🟠 **Eliminare Realtime / `System.Reactive`.** Realtime è disattivato ma la dipendenza viene comunque
  inclusa (504 KB di `System.Reactive` + `Websocket.Client`). Sostituire il meta-pacchetto
  `supabase-csharp` con i soli `postgrest-csharp` + `gotrue-csharp` (+ `supabase-core`) per tagliarla alla
  radice. È un cambio architetturale da verificare con attenzione (è il prossimo passo già pianificato).
  ⚠️ In tensione con il combat in Realtime (§8): decidere prima se il combat resta a polling.
- 🟢 **Misurare il bundle pubblicato.** Dopo trimming `full` + feature-switch (già attivi), verificare il
  peso reale di `System.Reactive` e `System.Private.Xml` sulla build di Release e confermare i tagli.
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
- 🟡 **Gestione errori coerente.** ✅ `<ErrorBoundary>` aggiunto in `MainLayout` (fallback a tema + "Ripara e
  ricarica") e `DbErrorBanner` centralizzato (quick-win A). **Resta:** far ritornare ai metodi `Delete` di
  dei repository l'esito reale (oggi `RemoveCharacterSpellAsync` ritorna sempre `true`; gli altri sono
  `void`) — da fare verificando se Postgrest 0.16.2 lancia su errore (non testabile in locale); toast
  "salvato"/"errore" centralizzati.
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

- 🟠 **Suite di test** — ✅ progetto `DndCompanion.Tests` (xUnit), **91 test**. Coperti: `CharacterCalculations`
  (modificatori, competenza, TS/skill, iniziativa, percezione passiva, spellcasting, dadi vita incl. parsing
  `HitDiceMax`); la **logica pura dei repository** (estratta in helper `internal static`, esposti via
  `InternalsVisibleTo`): visibilità/ordinamento note (`NoteRepository.FilterAndSortVisible`, regola di sicurezza),
  ordinamento inventario (`InventoryRepository.SortForDisplay`), codice invito (`CampaignRepository.GenerateInviteCode`);
  e la **logica di dominio estratta dai `.razor`**: `CharacterNormalizer.Normalize` (trim/null/clamp del draft PG) e
  `AccessControl.CanEdit` (autorizzazione master-o-proprietario). Restano da coprire:
  1. ~~`CharacterCalculations`~~ ✅ · ~~Parsing `HitDiceMax`~~ ✅ · ~~Logica pura repository (note/inventario/invito)~~ ✅
  2. ~~Normalizzazione/clamp dei form PG (`NormalizeDraft`)~~ ✅ (`CharacterNormalizer`)
  3. ~~Autorizzazioni (`CanEdit`/`isMaster`)~~ ✅ (`AccessControl`, usato da tutte le pagine)
  4. Filtro/JOIN incantesimi del PG (gestione orfani).
  5. Test d'integrazione sulle **RLS** (un utente non legge note/PG altrui).
- 🟡 **Refactoring abilitanti**: ✅ interfacce sui repository (sotto-fase A) + estrazione di helper puri
  testabili dai repository e dai `.razor` (`CharacterNormalizer`, `AccessControl`). **Resta:** per testare interi
  componenti (rendering/eventi) servirebbe bUnit; per ora si estrae la logica pura man mano.

---

## 5. Performance

- 🟡 **Caricamento intere tabelle filtrate nel client.** `GetNotesForPlayerAsync` e la mappatura nickname
  scaricano più del necessario: filtrare server-side (RLS + `.Where` su colonne indicizzate), esporre una
  view nickname-only. (Si lega alla sicurezza, §1.)
- 🟡 **Virtualizzazione liste.** Nessun `<Virtualize>`: con cataloghi lunghi di spell/mostri, virtualizzare
  e **memoizzare i filtri** (oggi `FilteredSpells` è una property che ricalcola la LINQ a ogni render).
  **Valutato nel loop (2026-06-21): rimandato** — memoize/`<Virtualize>` cambiano il comportamento di liste e
  ricerca (invalidazione, scroll), verificabile solo a runtime: rischioso in autonomia senza test manuale.
- 🟢 **Cache dati semi-statici** (razze/classi/catalogo spell) in memoria con invalidazione esplicita.
- 🟢 **Stati di caricamento.** Sostituire i "Caricamento..." testuali con spinner/skeleton a tema.

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
  (PF +/−, rimuovi). **Resta:** `aria-label` sui pochi pulsanti simbolo minori (FAB, dismiss). **Contrasti:** ✅ alzato `--gold-dim` (#8b6f3a → #b08842) per la leggibilità su fondo scuro — da
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
  solo se si decide di **mantenere** Realtime (in tensione con la rimozione in §2).

---

## 9. Idee aperte (da ragionare)

> Non ancora decise: spunti da valutare, non impegni.

- 💡 **Offline dei dati read-only.** Oggi offline funziona solo la shell; cache dei cataloghi per
  consultazione senza rete, se diventa una promessa del prodotto.
- 💡 **Markdown nelle note** (oggi plain text).
- 💡 **Modello di monetizzazione.** Free vs a pagamento, entitlement, cosa sta dietro al paywall.
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
6. **Rimozione Realtime** (§2) e **design token / refactor `Characters.razor`** (§3, §6) — manutenibilità.
7. Il resto (AI compilazione, wizard scheda, performance, a11y, i18n, idee) secondo priorità di prodotto.
