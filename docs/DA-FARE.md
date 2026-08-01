# DA FARE — D&D Companion

> **Solo ciò che è aperto.** Una voce = 1-3 righe. Se serve più spazio, il racconto va in
> [DIARIO.md](./DIARIO.md) e qui resta il rimando: questo documento si legge a ogni sessione, e la
> sua lunghezza è un costo fisso.
>
> - Perché delle scelte già fatte → [DIARIO.md](./DIARIO.md).
> - Punti **chiusi** con motivazioni, misure e alternative scartate →
>   [archivio/DA-FARE-chiuso.md](./archivio/DA-FARE-chiuso.md) (il documento come era fino al 2026-08-01).
> - Spec e piani → `docs/superpowers/specs/` e `docs/superpowers/plans/`.
> - Monetizzazione → [DA-FARE-MONETIZZAZIONE.md](./DA-FARE-MONETIZZAZIONE.md) (accantonata).
>
> Ultimo aggiornamento: **2026-08-01**

Legenda: 🔴 **bloccante** per il lancio pubblico · 🟠 **alta** · 🟡 **media** · 🟢 **bassa/idea**.

---

## ⛔ Verifiche manuali in sospeso

> Il gate automatico non copre nulla di ciò che segue, e `main` pubblica: da rileggere **prima** di
> ogni push e da segnalare all'utente.

- 🔴 **Migrazione `20260731000000_party_visibility.sql`** applicata a mano al Supabase hosted, se non
  già fatto: finché non gira, la pagina Party mostra il banner d'errore. Verifica:
  `SELECT proname FROM pg_proc WHERE proname = 'get_party_overview';`
- 🔴 **Prova a due account** (master + giocatore in incognito): il giocatore vede solo i propri PG, il
  master tutti; entrambi vedono il gruppo in Party con le sole stat sintetiche.
- 🟠 **Publish Release trimmato**: `dotnet publish DndCompanion.csproj -c Release -o publish`, servire
  `publish/wwwroot` **con accesso fatto**, aprendo la pagina Party **e** una scheda su una classe del
  manuale (è lì che si deserializzano `PartyMember` e `PackageSubclass`).
- 🟠 **Android reale**: barra inferiore allo scroll (l'emulazione non riproduce la barra URL dinamica)
  e card dei PF a 360/375/412px su un PG con PF a tre cifre.
- 🟠 **Tracker → «Importa mostri»**: a ricerca vuota solo le righe di campagna; cercando, anche il
  manuale; le quantità devono sopravvivere al cambio di ricerca.
- 🟠 **Export «tutto, manuale incluso» + reimport** in una campagna di prova. Caso sottile: l'export
  della **sola** campagna, in un tavolo che ha materializzato un incantesimo, deve comunque portare la
  licenza.
- 🟠 **Eliminazione di un PG** (Scheda → in fondo): inventario e incantesimi devono sparire con lui
  (`ON DELETE CASCADE`). La prova che conta è il master che elimina il PG di un giocatore.
- 🟡 **Scelta della sottoclasse** in creazione e modifica: menu con una classe del manuale, testo
  libero con una classe propria; cambiando classe quella dell'altra sparisce, una scritta a mano resta.
- 🟡 **PG di livello ≥ 3 con classe importata prima del 2026-07-31**: la scheda ripiega sul pacchetto,
  ma per aggiornare il catalogo di campagna serve un re-import dalla pagina Dati.

---

## 1. La direzione scelta il 2026-08-01

> Tre filoni decisi con l'utente, in un solo blocco perché toccano gli stessi file. Motivazioni in
> [DIARIO.md](./DIARIO.md), sezione «La direzione scelta».

### A. 🟠 La sottoclasse è una **scelta**, non un campo di testo
Richiesta ripetuta più volte: nel manuale la sottoclasse porta privilegi e abilità uniche, quindi
ogni classe deve contenere le sue e il personaggio deve scegliere fra quelle.
- Oggi funziona **solo** per le classi che il manuale conosce: una classe del tavolo, o una importata,
  non ha dove tenere le proprie sottoclassi.
- Serve una **casa nei dati**: colonna testuale su `classes` (come già per la tabella dei livelli) o
  tabella dedicata — decisione da prendere, la seconda costa una migrazione e una RLS.
- L'import deve **scriverle** (oggi le legge e le scarta); la pagina Classi deve permettere di
  aggiungerle e modificarle; scheda e wizard le pescano da lì, non solo dal manuale.
- I privilegi della sottoclasse vanno **applicati**, non solo elencati: è il ponte verso §3.

### B. 🟠 Il file di dati porta **tutto**
Tutto ciò che l'app sa deve uscire in JSON, essere editabile e rientrare senza perdite: sono i
giocatori a portarsi dentro i contenuti.
- **Perimetro (deciso):** cataloghi al completo — specie, classi *con sottoclassi e livelli*,
  background, talenti, incantesimi, mostri. PG, appunti e stato del combattimento restano fuori.
- Da chiudere le perdite attuali: `skillChoices` digitato a mano non ha inversione; le sottoclassi
  escono solo sulle righe di provenienza manuale; i talenti non hanno tabella (decidere se dargliela).
- **Nessun limite di volume** (deciso): il formato deve scalare. Niente tetti al numero di voci.
- Criterio di fatto: un test di **round-trip** — export → import → export produce lo stesso file.

### C. 🔴 Sicurezza dell'import (nello stesso lavoro di B)
Un file scritto a mano può dichiarare `"id": "srd-2024-it"` e **spacciarsi per il manuale**: le righe
che ne nascono si presentano come ufficiali, non sono modificabili dall'interfaccia (nemmeno dal
master) e «Rimuovi un import» rifiuta quel prefisso — restano indelebili, recuperabili solo via
database. Il parser deve rifiutare o rinominare gli id che rivendicano il prefisso del manuale.
Verificato il 2026-08-01: nessuna escalation (le RLS tengono) e nessuna iniezione HTML
(`MarkupString` non è usato). Minore: nomi con caratteri simili possono oscurare una voce ufficiale
sfruttando «a parità di nome vince la riga locale».

---

## 2. Sicurezza — gate del lancio pubblico

- 🔴 **«Campaign hopping» nelle `WITH CHECK` di update**, 7 tabelle: l'autore può spostare una propria
  riga in una campagna di cui non è membro. Il caso peggiore è `notes` condivise, che **nessuno** può
  rimuovere. Caso gemello: l'ex-membro conserva scrittura sulle proprie righe rimaste in campagna.
  Una migrazione autonoma col suo giro di test RLS. Dettaglio nell'archivio, §1.
- 🔴 **Prefisso del manuale spoofabile** all'import → §1.C.
- 🟡 **Vincoli DB residui**: `NOT NULL`, lunghezze e `CHECK` sui range numerici (caratteristiche, CA,
  velocità). Oggi validati solo lato client (`FormValidation`).
- 🟡 **Header di sicurezza**: `frame-ancestors`/HSTS/`report-uri` non ottenibili via `<meta>`; GitHub
  Pages non permette header HTTP. Servirebbe un altro hosting (v. §7).

---

## 3. Gioco al tavolo

- 🟠 **Motore di derivazione condiviso** (PF, slot, competenze, taglia, velocità, privilegi) usato da
  creazione, **modifica** e level-up: oggi solo il wizard suggerisce qualcosa e il form duplica il
  markup senza calcolare niente.
- 🟠 **Level-up guidato**: oggi salire di livello è editare a mano PF, dadi vita, 9 slot e competenze.
  È l'attrito che torna a ogni sessione di gioco. Poggia sul motore qui sopra.
- 🟠 **Combattimento consultabile**: il tracker porta solo nome e PF, quindi le statistiche del mostro
  non si vedono mentre si combatte. Serve un riferimento alla sorgente nel `Combatant` (campo
  additivo nel `jsonb`, nessuna migrazione) e un blocco statistiche apribile sulla riga.
- 🟡 **Iniziativa precompilata o tirata**: gli import mettono `Initiative = 0` benché l'app conosca il
  bonus di ogni PG; i PF si regolano ±1 per click (tastierino). Unico punto: `CombatImport`.
- 🟡 **Aiuto contestuale dal manuale**: nessuna spiegazione di cosa siano tiro salvezza, competenza,
  CD incantesimo. Indipendente da tutto il resto.
- 🟢 **Conferma sui salvataggi impliciti** dei tab della scheda (`SaveCharacterAsync` è muto).

---

## 4. Performance

- 🟡 **Cache dei cataloghi** in memoria con invalidazione esplicita: ogni pagina ricarica i propri dati
  a ogni ingresso e 4 su 6 rifanno `GetProfilesAsync()`. Con la barra di navigazione gli ingressi sono
  più frequenti, non meno.
- 🟡 **Virtualizzazione delle liste** nelle pagine di catalogo: col manuale caricato superano di molto
  le ~50 voci su cui poggiava la decisione di scartarla. Nel tracker il caso è stato risolto con
  ricerca + tetto a 40 voci (`MonsterPicker`), che vale come precedente: dove si **sceglie**, filtrare
  batte virtualizzare; dove si **sfoglia**, la domanda resta aperta (card espandibili = caso ostico).
- 🟡 **View nickname-only**: la mappatura dei nickname scarica più del necessario (richiede una vista
  DB). Le note sono già filtrate dalle RLS, quindi non c'è perdita di riservatezza.
- 🟡 **Peso del pacchetto dati**: `wwwroot/data/srd-2024-it.json` è 943 KB grezzi / 176 KB compressi,
  escluso dal precache e scaricato al primo uso. Da guardare su rete lenta.

---

## 5. UI / a11y

- 🟡 **Sweep dei literal su token**: restano `#c8b88a` in 4 file (`CharacterCombatTab`,
  `SpellListItem`, `SpellPicker`, `StatCard`) e `#e6a373` in 6 (9 occorrenze). **Non** va convertita
  la decima, in `Party.razor.css`, che è uno stop di gradiente.
- 🟡 **Trappola: `--gold-muted` (`#c9b88a`) e `--gold-muted-rgb` (`#9a8c6a`) sono colori diversi**, e
  `--text-body` (`#c8b88a`) differisce da `--gold-muted` di un punto sul canale rosso. Sostituire
  sempre **sul valore**, mai per somiglianza di nome. Da rinominare in un intervento dedicato.
- 🟡 **Caselle competenza a 24px** in `CharacterEditForm` (36 punti di markup): per arrivare a 44
  serve avvolgere ogni casella in una `<label>` che occupi la cella. Da fare quando si rimette mano a
  quel form — cioè con §3.
- 🟡 **Consolidare le sfumature quasi-duplicate** (6 rossi, 4 verdi, 6 oro-bronzo): è un cambio di
  colore, quindi una decisione, non un ritocco.
- 🟢 **Token dei gradienti**: 9 container di pagina su 10 aprono il gradiente con `var(--text-on-gold)`
  — nome che mente (significa «testo su fondo oro»). `Characters.razor.css` usa `var(--bg)`: o è
  l'unico corretto, o è l'unico diverso. Serve un token dedicato e una decisione su quale tinta vale.
- 🟢 **Il `← Home` di pagina è ridondante** dopo la barra di navigazione: toglierlo libera una riga per
  pagina ma cambia il flusso di ritorno. Decisione di prodotto.

---

## 6. Formato e contenuti

- 🟡 **`PackageSpeed.Value` è `int`**: un decimale (`7.5`) in un file di terzi fa fallire la lettura
  dell'**intero** pacchetto. Chiuso per il pacchetto dell'app (dichiara i piedi, interi), aperto per
  quelli di terzi. Passare a `decimal` toccherebbe anche `races.speed` (`integer`) e i punti d'uso.
- 🟡 **Due voci narranti negli incantesimi**: i livelli 3 e 5 (80 voci) sono in terza persona, gli
  altri otto (259) in seconda. Solo tono, ma è una scelta editoriale — e una riscrittura di massa
  passa sopra il testo delle regole.
- 🟢 **Unificare l'unità di velocità** (razza in piedi, PG in metri): la colonna `speed_unit` esiste,
  resta da renderla coerente in tutta l'interfaccia.

---

## 7. Test e infrastruttura

- 🟡 **bUnit** per testare interi componenti (rendering, eventi): per ora si estrae la logica pura man
  mano. Stato attuale: **676 unit test** + 11 scenari d'integrazione RLS (stack Supabase locale,
  auto-skip se giù).

---

## 8. Idee aperte (non impegni)

- 💡 **Aiuto AI alla compilazione** da testo libero: richiede un proxy che custodisca la chiave e un
  allowlist per utente. Merita il suo spec, dopo le RLS. Precaricare il manuale ne ha ridotto molto
  la necessità.
- 💡 **Offline dei dati read-only** (oggi offline vive solo la shell) · **markdown nelle note** ·
  **tema chiaro / multi-tema** (sbloccato dai token) · **hosting alternativo** con header di sicurezza
  e dominio custom · **combat in realtime** (rimetterebbe `realtime-csharp` nel bundle).
- 💡 **i18n**: tutte le stringhe sono hardcodate in italiano. Se l'inglese entra in roadmap, `.resx` +
  `IStringLocalizer`; altrimenti IT-only consapevole.

---

## 9. Ordine consigliato

1. **§1 A+B+C insieme** — sottoclassi con una casa nei dati, round-trip completo del file, chiusura
   del prefisso spoofabile. Sono gli stessi file: separarli significa ripassarci sopra.
2. **§2 varco RLS** — una migrazione, gate della pubblicazione.
3. **§3 motore di derivazione → level-up guidato** — in quest'ordine, il secondo poggia sul primo.
4. **§3 combattimento consultabile** — indipendente, aggredibile in parallelo.
5. Il resto (§4-§6) a spizzichi, dove si passa già per altri motivi.
