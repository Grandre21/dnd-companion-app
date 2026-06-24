# DIARIO DI PROGETTO — D&D Companion

> Promemoria sintetico di **cosa è stato fatto e perché**. Per ciò che resta aperto vedi [DA-FARE.md](./DA-FARE.md).
> Aggiornato: **2026-06-21**.

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
Restano (lavoro grande o lato DB, vedi [DA-FARE.md](./DA-FARE.md)): **sicurezza RLS** (gate del pubblico),
**mega-refactor** (`Characters.razor`/`SupabaseService`), e le feature di prodotto (AI alla compilazione,
wizard scheda, i18n).
