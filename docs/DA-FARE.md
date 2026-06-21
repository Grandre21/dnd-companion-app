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

> Con la migrazione a Supabase Auth l'identità è ora
> un JWT firmato, ma **manca l'autorità lato server sui dati**: le RLS sono permissive e il client filtra
> da solo. Finché è così, chiunque con la anon key (pubblica nel bundle) può leggere/scrivere tutte le
> tabelle via REST bypassando la UI.

- 🔴 **Scrivere e testare le RLS per ogni tabella.** Regole per `characters`, `campaign_members`,
  `notes`, cataloghi: un Player legge/modifica solo ciò che gli compete; le note private restano del Master.
  Senza questo nessun altro fix di sicurezza ha valore reale.
- 🔴 **Spostare le autorizzazioni sul server.** Ruolo e proprietà (`isMaster`, owner del PG) oggi sono
  derivati lato client: vanno applicati via RLS / policy basate sul JWT, non solo nella UI.
- 🟠 **Gate di registrazione/ingresso.** Se l'app diventa a pagamento, legare l'accesso all'entitlement
  d'acquisto (Play Billing) anziché a un codice invito; validare i codici invito server-side
  (monouso/scadenza) se restano.
- 🟡 **Vincoli e validazione a livello DB.** `NOT NULL`, lunghezze, `CHECK` su livelli/punteggi; integrità
  referenziale con **FK + `ON DELETE CASCADE`** per evitare inventario/incantesimi orfani
  (il codice oggi filtra gli orfani lato client: sintomo che il problema è reale). Da verificare sul DB
  Supabase, non deducibile dal solo C#.
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

- 🟠 **Spezzare `Characters.razor` (~2.4k righe).** Estrarre componenti per tab/sezione
  (`CharacterCombatTab`, `CharacterStatsTab`, `CharacterBioTab`, `CharacterItemsTab`, `CharacterMagicTab`,
  `CharacterEditForm`, `InventorySection`) passando `Character` + `EventCallback`. `StatCard`/`SpellPicker`
  sono già un buon precedente.
- 🟡 **Spezzare `SupabaseService` (god-object, ~40 metodi).** Repository per aggregato dietro interfacce
  (`ICharacterRepository`, `ISpellRepository`, `INoteService`…); abilita anche il mocking nei test.
- 🟡 **Centralizzare lo stato di auth/ruolo.** Oggi ogni pagina rilegge identità/ruolo; un provider reattivo
  con `CurrentUser { Id, Nickname, IsMaster }` riduce duplicazione e round-trip a localStorage.
  Collegato: completare il `TODO(campagne)` in `AuthStateService.GetRoleAsync()` (oggi ritorna `null`;
  il ruolo vive in `CampaignStateService`).
- 🟡 **Gestione errori coerente.** ✅ `<ErrorBoundary>` aggiunto in `MainLayout` (fallback a tema + "Ripara e
  ricarica") e `DbErrorBanner` centralizzato (quick-win A). **Resta:** far ritornare ai metodi `Delete` di
  `SupabaseService` l'esito reale (oggi `RemoveCharacterSpellAsync` ritorna sempre `true`; gli altri sono
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

- 🟠 **Suite di test** — ✅ progetto `DndCompanion.Tests` (xUnit) creato; **`CharacterCalculations` coperto,
  54 test** (modificatori, competenza, TS/skill, iniziativa, percezione passiva, spellcasting, dadi vita
  incl. parsing `HitDiceMax`). Restano da coprire:
  1. ~~`CharacterCalculations`~~ ✅ · ~~Parsing `HitDiceMax`~~ ✅
  2. Normalizzazione/clamp dei form PG (`NormalizeDraft`, edge: negativi, vuoti, oltre-limite).
  3. Autorizzazioni (`CanEdit`/`isMaster`) — specie dopo lo spostamento server-side.
  4. Filtro/JOIN incantesimi del PG (gestione orfani).
  5. Test d'integrazione sulle **RLS** (un utente non legge note/PG altrui).
- 🟡 **Refactoring abilitanti**: interfacce sui service + estrazione logica dai `.razor` per poter usare
  bUnit.

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
  i tiri salvezza morte, l'ispirazione e gli slot incantesimo. **Resta:** `aria-label` sui pulsanti icona-only
  minori. **Contrasti:** ✅ alzato `--gold-dim` (#8b6f3a → #b08842) per la leggibilità su fondo scuro — da
  verificare a vista e affinare se serve (cambia i testi/bordi "spenti" ovunque, via token).
- 🟡 **Feedback azioni** — ✅ avviato (2026-06-21): infrastruttura toast (`ToastService` + `ToastHost` nel
  layout, auto-dismiss, a tema con i token) integrata; "✓ Personaggio salvato" su `SaveCharacterAsync`.
  **Resta:** estendere `ShowSuccess`/`ShowError` agli altri salvataggi e ai CRUD dei cataloghi; dialog di
  conferma a tema al posto dei `confirm()` nativi.

---

## 7. Internazionalizzazione

- 🟡 **i18n.** Tutte le stringhe UI sono hardcodate in italiano. Se l'inglese entra in roadmap (Play Store
  globale), estrarre in risorse `.resx` + `IStringLocalizer`. Altrimenti accettare consapevolmente IT-only.

---

## 8. Funzionalità emerse dall'uso (da ingegnerizzare)

> Richieste nate dall'uso reale che **non sono quick-win**: ognuna merita un proprio giro di
> brainstorming → design prima dello sviluppo.

- 🟡 **Combat condiviso + polling** — ✅ implementato (2026-06-21), **da verificare a runtime**: tabella
  `combat_state` creata + model `CombatState`/`Combatant`; `GetCombatStateAsync`/`SaveCombatStateAsync`
  (upsert) in `SupabaseService`; `Combat.razor` carica/salva lo stato — il Master fa upsert a ogni azione, i
  giocatori (non-master) leggono con **polling ~4s**. **Da verificare a vista:** serializzazione jsonb dei
  combattenti, l'upsert, e che il giocatore veda i cambi del Master. Con RLS permissive funziona, andrà
  protetto (§1). Limite noto: l'iniziativa modificata inline si persiste al successivo salvataggio
  (es. "Ordina"/"Prossimo turno"), non all'istante.
- 🟡 **Aiuto AI alla compilazione della scheda.** Assistere/velocizzare la compilazione (es. bozza scheda da
  descrizione testuale, suggerimenti su bonus/competenze). Da progettare: provider LLM, **gestione della API
  key** (la anon key è già esposta nel bundle → serve un proxy/edge function, non chiamate dirette dal
  client), prompt, costi, UX. Risponde allo stesso bisogno dei quick-win C ma in modo strutturale.
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
