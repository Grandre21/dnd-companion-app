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
> Ultimo aggiornamento: **2026-08-06**

Legenda: 🔴 **bloccante** per il lancio pubblico · 🟠 **alta** · 🟡 **media** · 🟢 **bassa/idea**.

---

## ⛔ Verifiche manuali in sospeso

> Il gate automatico non copre nulla di ciò che segue, e `main` pubblica: da rileggere **prima** di
> ogni push e da segnalare all'utente.

> ✅ **Nessuna migrazione in sospeso.** Applicate all'hosted: `20260806130000_scheda_carta.sql` e
> `20260806120000_close_campaign_hopping.sql` (2026-08-06), `20260801000000_class_subclasses.sql` e
> `20260731000000_party_visibility.sql` (2026-08-01). Il client live corrisponde.

- 🔴 **Scheda alla pari con la carta, quattro prove** (nuovo, 2026-08-06): (a) un riposo lungo che
  **ripristina le risorse** e le nomina nel riepilogo; (b) un'arma **accurata** su un personaggio con
  Destrezza maggiore della Forza: il bonus deve usare Destrezza; (c) il **form di modifica** aperto e
  salvato su un personaggio con risorse e addestramento — non devono sparire (era il bloccante di
  `CloneCharacter`); (d) una risorsa con ricarica **«nessuna»**: il riposo non deve toccarla.
- 🔴 **Creazione guidata, cinque prove** (nuovo, 2026-08-06): (a) un **Mago di 1°**: deve nascere con
  gli slot e con la CD degli incantesimi valorizzata (era il difetto: `SpellcastingAbility` vuota);
  (b) un **Barbaro di 5°**: sottoclasse chiesta al 3°, talento al 4°, PF coerenti col dado; (c) una
  **classe del tavolo senza tabella**: il ripiego deve *vedersi ed essere spiegato*, campo PF e
  livello di nuovo liberi, mai un vicolo cieco; (d) **cambio di classe** dopo aver scelto le
  competenze: le vecchie spariscono, quelle del background restano; (e) **cambiare idea** su una
  decisione già risposta — riaprire il 3° dopo aver scelto la sottoclasse e sceglierne un'altra.
- 🔴 **Level-up guidato, tre prove** (nuovo, 2026-08-06): (a) un PG del manuale che sale a un livello
  **con una scelta** — sottoclasse al 3° o talento al 4° — e la conferma scrive davvero; (b) un PG con
  classe **del tavolo** senza tabella: il dialogo non deve aprirsi, e deve comparire il toast che
  rimanda al form; (c) un incantatore che **sblocca un cerchio nuovo**, con il rimando al tab Magia.
- 🟠 **Level-up con salvataggio fallito**: togliere la rete a metà conferma. Il dialogo deve restare
  aperto con le risposte intatte ed essere ritentabile — e al secondo tentativo i punteggi **non**
  devono incrementarsi due volte.
- 🟠 **Sottoclassi nella pagina Classi**: aggiungere, modificare (il nome resta al suo posto nell'elenco),
  rimuovere; «duplica e modifica» da una voce SRD deve portarsele dietro; le righe del manuale restano
  di sola lettura. E il menu della sottoclasse deve comparire nella scheda anche per una classe **del
  tavolo** o importata, non solo per quelle del manuale.
- 🟠 **File esportato dal client precedente**: prendere un export «tutto, manuale incluso» fatto *prima*
  del 2026-08-01 e reimportarlo. Deve entrare senza errori — è la compatibilità che ha fatto esentare
  gli id di sottoclassi e talenti dal divieto del prefisso. Se si rompe, quella scelta era sbagliata.
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
- 🟡 **Scelta della sottoclasse** in creazione e modifica, cambiando classe: quella di un'altra classe
  sparisce, una scritta a mano resta. È il punto su cui il gate ha trovato sei perdite silenziose in
  tre giri: vale una prova a mano.
- 🟡 **PG di livello ≥ 3 con classe importata prima del 2026-07-31**: la scheda ripiega sul pacchetto,
  ma per aggiornare il catalogo di campagna serve un re-import dalla pagina Dati.

---

## 1. Quel che resta della direzione scelta il 2026-08-01

> I punti A (sottoclassi con una casa nei dati) e C (prefisso del manuale spoofabile) sono **chiusi**:
> il racconto e le decisioni in [DIARIO.md](./DIARIO.md), sezione «Le sottoclassi hanno una casa nei
> dati». Di B resta la parte qui sotto.

### B. 🟠 Il file di dati non porta ancora **tutto**
Il perimetro deciso è «cataloghi al completo», e il giro export → import → export è ora idempotente
sui campi che il formato dichiara (test di round-trip). Restano fuori dei **campi che il formato non
ha**, quindi il giro li perde in silenzio:
- **Specie**: `languages` e i sei bonus di caratteristica.
- **Classi**: `description`, competenze in armature e armi.
- **Mostri**: taglia, tipo, allineamento, velocità, le sei caratteristiche, `abilities`.
- **Talenti**: non hanno tabella, quindi escono solo dal manuale e un tavolo non può averne di propri
  — da decidere se dargliela (è una migrazione, e allora il divieto del prefisso torna a valere anche
  su quella sezione: v. il commento in `CatalogPackageParser`).
- **`skillChoices`** scritto in prosa libera resta non invertibile e viene omesso: le varianti vicine
  al formato generato ora si invertono, la prosa no. Se serve trasportarla, servirebbe un campo
  gemello di solo testo nel formato.

**Nessun limite di volume** (deciso): niente tetti al numero di voci.

### 🟡 Nomi con caratteri simili
Restano capaci di oscurare una voce ufficiale sfruttando «a parità di nome vince la riga locale».
Residuo minore di C.

---

## 2. Sicurezza — gate del lancio pubblico

- 🟢 **Residuo del campaign hopping** (il varco è chiuso il 2026-08-06): l'autore può ancora spostare
  una propria riga verso una campagna di cui è **già** membro. Non è accesso a dati altrui, quindi
  resta come nota, non come lavoro.
- 🟡 **Vincoli DB residui**: `NOT NULL`, lunghezze e `CHECK` sui range numerici (caratteristiche, CA,
  velocità). Oggi validati solo lato client (`FormValidation`).
- 🟡 **Header di sicurezza**: `frame-ancestors`/HSTS/`report-uri` non ottenibili via `<meta>`; GitHub
  Pages non permette header HTTP. Servirebbe un altro hosting (v. §7).

---

## 3. Gioco al tavolo

- 🟡 **Il form di modifica non usa il motore**: il wizard ora sì (`CreationChain` incatena
  `LevelUpPlanner`), il form no — ed è **deliberato**: su un PG a metà campagna i PF massimi
  divergono legittimamente dalla formula, e un form che «aiutasse» riscriverebbe valori veri con
  valori teorici. Quel che può entrarci è opt-in («Usa suggerito») e gli avvisi soft del vincolo
  competenze. V. [DIARIO](./DIARIO.md), «La creazione guidata».
- 🟡 **Il pannello delle decisioni è scritto due volte**: `LevelUpDialog` e il passo «Progressione»
  del wizard rendono gli stessi contratti (`DecisioneFraOpzioni`, `DecisionePunteggi`,
  `DecisioneLibera`) con markup indipendente. Duplicazione **accettata il 2026-08-06** per non
  toccare `LevelUpDialog` prima delle sue prove manuali; da estrarre in un componente condiviso
  quando quelle sono fatte.
- 🟡 **I privilegi di sottoclasse vanno applicati, non solo elencati**: il dialogo li annuncia, la
  scheda li deriva dalla tabella, ma nessuno li traduce in effetti.
- 🟠 **Combattimento consultabile**: il tracker porta solo nome e PF, quindi le statistiche del mostro
  non si vedono mentre si combatte. Serve un riferimento alla sorgente nel `Combatant` (campo
  additivo nel `jsonb`, nessuna migrazione) e un blocco statistiche apribile sulla riga.
- 🟡 **Aiuto contestuale dal manuale**: nessuna spiegazione di cosa siano tiro salvezza, competenza,
  CD incantesimo. Indipendente da tutto il resto.
- 🟢 **Conferma sui salvataggi impliciti** dei tab della scheda: il *fallimento* ora si vede
  (`SaveCharacterCoreAsync` restituisce l'esito e valorizza `errorMessage`), il *successo* no —
  Stats/Zaino/Magia salvano in silenzio.

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
  mano. Stato attuale: **924 unit test** + **32 test d'integrazione** sullo stack Supabase locale
  (RLS e serializzazione col client Postgrest reale; auto-skip se lo stack è giù).

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

## 9. Ordine consigliato (rivisto il 2026-08-06)

Deciso col consulto di analisi del 2026-08-06; il ragionamento sta in [DIARIO](./DIARIO.md), «Il
master che assegna». Ogni tappa è usabile da sola; la 2 e la 3 si possono fare in parallelo alla 1.

1. ~~**Creazione guidata**~~ — **fatta il 2026-08-06**, spec e racconto in
   [DIARIO](./DIARIO.md), «La creazione guidata». Restano le **cinque prove manuali** in testa a
   questo documento: finché non sono fatte, la tappa non è verificata.
2. **Il master assegna, parte atomica**: RPC `grant_to_characters` per monete e PE, multi-selezione,
   divisione del bottino, più l'ispirazione. Una migrazione, fascia a 3 giri, test RLS sullo stack
   locale. Vale la regola di `CLAUDE.md`: mai `UpdateCharacterAsync` su un PG altrui.
3. **§3 combattimento consultabile** — indipendente, aggredibile in parallelo.
4. **§1.B campi mancanti del formato** — quando si passa di lì per altri motivi.
5. Il resto (§4-§6) a spizzichi.

Chiuse il 2026-08-06: il varco RLS (migrazione applicata all'hosted), riposo lungo/breve, tastierino
dei PF, iniziativa precompilata, «dai oggetto» dalla vista Party. Con esse è caduto anche l'ultimo
🔴 di codice: da qui in avanti il gate del lancio pubblico non ha più bloccanti aperti.
