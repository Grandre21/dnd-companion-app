# Backlog tecnico (archiviato il 2026-08-08)

> ⚠️ **Questo documento non si legge a ogni sessione, e non si aggiorna per abitudine.** Era
> `docs/DA-FARE.md`, letto sempre e da nessuno: un costo fisso in cambio di niente. Dal 2026-08-08 le
> cose da fare si **dicono in chat** a fine lavoro (v. [CLAUDE.md](../../CLAUDE.md), «Cosa resta da
> fare si dice, non si archivia»); qui resta solo ciò che nessuno ha ancora deciso di fare, perché
> buttarlo via avrebbe perso analisi già pagate.
>
> **Le verifiche manuali che stavano in testa sono state eseguite tutte** il 2026-08-08 e tolte.
>
> Si apre su richiesta — «cosa era rimasto aperto?» — non di routine. Se un punto viene ripreso, esce
> di qui e il *perché* finisce nel [DIARIO](../DIARIO.md).
>
> - Perché delle scelte già fatte → [DIARIO.md](../DIARIO.md).
> - Punti chiusi prima del 2026-08-01 → [archivio/DA-FARE-chiuso.md](../archivio/DA-FARE-chiuso.md).
> - Modifiche al database → [db.md](./db.md).
> - Spec e piani → `docs/superpowers/specs/` e `docs/superpowers/plans/`.
> - Monetizzazione → [DA-FARE-MONETIZZAZIONE.md](../DA-FARE-MONETIZZAZIONE.md) (accantonata).

Legenda: 🔴 **bloccante** per il lancio pubblico · 🟠 **alta** · 🟡 **media** · 🟢 **bassa/idea**.

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
- 🟡 **Restano le specie**: talenti e background ora mostrano la descrizione del manuale
  (`CharacterManualJoin`), le **10 specie** del pacchetto no. `Character.Race` è un nome, e accanto
  c'è già «TRATTI DELLA SPECIE» a testo libero: la descrizione ufficiale andrebbe sotto, come per il
  background. Stesso helper, un metodo in più.
- 🟡 **Privilegi di classe senza descrizione, bloccati sulla fonte**: «Ira», «Difesa senza armatura»
  restano nomi nudi perché nel pacchetto `levels[].features` è un array di **stringhe**. Sbloccarli
  richiede il **PDF ufficiale SRD 5.2.1 italiano** (CC BY 4.0), oggi non nel repo — il PHB in `docs/`
  **non** è utilizzabile: fuori licenza. Non inventarle: v. [DIARIO](./DIARIO.md), dove una
  traduzione a mano dei nomi ne azzeccò 27 su 57.
- 🟠 **Combattimento consultabile**: il tracker porta solo nome e PF, quindi le statistiche del mostro
  non si vedono mentre si combatte. Serve un riferimento alla sorgente nel `Combatant` (campo
  additivo nel `jsonb`, nessuna migrazione) e un blocco statistiche apribile sulla riga.
- 🟡 **Aiuto contestuale dal manuale**: nessuna spiegazione di cosa siano tiro salvezza, competenza,
  CD incantesimo. Indipendente da tutto il resto.
- 🟠 **I tab non sanno se il salvataggio è riuscito, quindi non tornano indietro** (2026-08-08): i tab
  delegano al genitore con `EventCallback OnChanged`, che **non restituisce valori**. Se le RLS
  rifiutano l'update PostgREST aggiorna zero righe e risponde `[]` — nessuna eccezione — quindi il tab
  chiude l'editor e mostra a schermo un valore che il database non ha (es. «500 MO» con 10 sul
  server, fino al reload). L'inventario, nello stesso file, fa già la cosa giusta: snapshot +
  ripristino su ritorno `null`. Serve portare l'esito fino al figlio, e il contratto è **condiviso**
  con sintonie, note e addestramento: intervento trasversale su 4-5 componenti, non una toppa locale.
- 🟢 **Conferma sui salvataggi impliciti** dei tab della scheda: il *fallimento* si vede in alto
  (`errorMessage` → `DbErrorBanner`), il *successo* no — Stats/Zaino/Magia salvano in silenzio. Il
  banner è però lontano dal widget che ha fallito: v. il punto qui sopra.

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

- 🟢 **Sweep dei literal, residuo `#c45638`**: `--text-body` e `--danger-text` sono chiusi (2026-08-07),
  ma resta `#c45638` — cioè `rgb(var(--danger-rgb))` — in 8 punti: `border-color` nell'`:hover`
  adiacente a ogni `.delete-action`/`.delete-link`/`.ctrl-danger` appena convertita (`Classes`,
  `Combat`×2, `Monsters`, `Notes`, `Races`, `Spells`) più un `color` in `CharacterItemsTab`.
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
  mano. Stato attuale: **1121 unit test** + **32 test d'integrazione** sullo stack Supabase locale
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
