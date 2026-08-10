# La scheda che non mente, e l'input che non stanca — design

> Spec del 2026-08-10. Nasce da «vorrei migliorare ancora un po' l'esperienza d'uso nelle varie
> schermate, specialmente nel proprio personaggio, sento che c'è ancora modo di migliorare
> l'interfaccia» — una sensazione, non una specifica. Il lavoro di questo documento è per metà
> **nominare l'attrito** che la segnalazione non nominava.

## 1. Da dove nasce, e cosa hanno detto i dati

Le tre sessioni precedenti (vista di gioco, board a mosaico, passivi dietro un tasto) hanno lavorato
tutte sulla **lettura** della scheda, sul presupposto — stabilito allora dall'utente stesso — che
«aggiornare è più veloce a matita» fosse **falso**: l'input andava bene, falliva la lettura.

Quel presupposto è cambiato. Nell'intervista di oggi l'aggiornamento **rientra** fra gli attriti, e
questo sposta il perimetro: le sessioni precedenti non avevano toccato nessuna scrittura.

Quattro domande a scelta multipla. Gli esiti, in ordine di utilità:

| Domanda | Risposta | Cosa implica |
|---|---|---|
| Quando senti l'attrito? | **tutti e quattro** i momenti | Non discrimina da sola; serve scendere |
| Cosa ti frena nell'aggiornare? | «non sono sicuro che sia andato» + «troppi tocchi» | Due problemi distinti: **fiducia** e **profondità** |
| Quali aggiornamenti pesano? | **zaino** e **monete** | L'attrito è **solo dopo il turno** |
| Cosa cerchi e non trovi? | bonus di una prova · cosa fa una capacità · una regola | La lettura fallisce sul **manuale** |

### Le assoluzioni contano quanto le accuse

Tre risposte **negative** hanno cancellato tre interventi che erano sul tavolo. Vanno registrate,
perché senza di esse questo spec conterrebbe lavoro inutile — e perché due di esse contraddicono una
raccomandazione esplicita del consulto:

- **Slot, risorse e PF: scagionati.** «Un tocco, va bene così.» Non si tocca la striscia degli attivi
  né il tastierino dei PF.
- **«Temo di toccare la cosa sbagliata»: negato.** Il consulto raccomandava di mettere i pallini di
  competenza di `StatCard` dietro una ✎ esplicita, perché `Pages/Characters.razor:222` passa
  `IsEditMode="@CanEdit"` e i pallini restano armati anche in lettura. Il rischio meccanico esiste,
  ma **all'utente non è mai capitato**: aggiungere un tocco per proteggerlo da un problema che non ha
  sarebbe un peggioramento netto. Resta solo l'esito+revert di §2, che serve comunque.
- **«Una cosa che avevo scritto io»: negato.** Le proprie note e annotazioni si ritrovano. La
  partizione delle schermate **regge**: nessuna ri-partizione, nessun riquadro spostato.

Sommato al vincolo già deciso il 2026-08-08 (la board deve assestarsi sotto partite vere prima di
ricevere altro), il perimetro di questo spec è: **nessun ridisegno, solo componenti esistenti**.

## 2. I salvataggi che dicono la verità

### Il difetto

`CharacterRepository.UpdateCharacterAsync` ritorna `null` quando le RLS rifiutano: PostgREST aggiorna
zero righe, risponde `[]`, **non solleva eccezioni**. Un figlio che notifica il genitore con un
`EventCallback` non può sapere com'è andata — `EventCallback.InvokeAsync` ritorna `Task`, non
`Task<bool>`, e non esiste una variante generica sul valore di ritorno.

Conseguenza: il tab chiude l'editor e mostra a schermo un valore **che il database non ha**. E il
valore resta applicato in memoria fino al primo salvataggio riuscito *da qualunque altro punto della
scheda*, che a quel punto lo persiste in silenzio — lo stesso meccanismo già documentato per l'editor
del denaro il 2026-08-08.

### Il pattern, che esiste già in casa

`CharacterVitalsBar.razor:76-115` e `CharacterCombatTab.razor:415-430` usano una **terna**, e ogni
pezzo ha una ragione dichiarata nel proprio commento:

1. **`Func<Task<bool>> OnChanged`** — il figlio conosce l'esito.
2. **`EventCallback OnLocalRevert`** — passando da `EventCallback` a `Func<…>` si **perde il re-render
   automatico** che Blazor fa per gli `EventCallback`. Il figlio si ridisegna da sé (è il receiver del
   proprio click), i **fratelli** no: questo li avvisa dopo un ripristino.
3. **`bool IsSaving` tenuto dal genitore** — un flag locale per componente lascerebbe partire due
   scritture in corsa sulla stessa riga da 113 colonne.

**Il genitore non deve scrivere nulla di nuovo**: `Pages/Characters.razor:1186`
`SaveCharacterCoreAsync` è **già** un `Task<bool>` che controlla il ritorno, ed è ciò che alimenta i
due componenti già convertiti. La conversione consiste nel passarla anche agli altri.

### I sette punti scoperti

Mappati esaustivamente (`grep` su `OnChanged.InvokeAsync` + `UpdateCharacterAsync`), non per campione:

| File | Metodo | Riga della chiamata cieca |
|---|---|---|
| `Shared/CharacterTabs/CharacterItemsTab.razor` | `SaveMoney` | 909 |
| ″ | `ConfermaSpesaAsync` | 961 |
| ″ | `SetAttunementAsync` (sintonie) | 983 |
| `Shared/StatCard.razor` | `CycleSave` (tiro salvezza) | 51 |
| ″ | `CycleSkill` (competenza/maestria) | 63 |
| `Shared/CharacterTabs/CharacterMagicTab.razor` | `ToggleSpellSlot` | 160 |
| `Pages/Characters.razor` | `SaveNotesAsync` (riga 1087) | 1096 |

Le firme da cambiare sono **tre**: `CharacterItemsTab.razor:513`, `StatCard.razor:43`,
`CharacterMagicTab.razor:97`. `CharacterStatsTab.razor:21` fa da tramite verso `StatCard` e va
adeguato di conseguenza.

**`SaveNotesAsync` è il caso peggiore e va trattato a parte.** Lì `previous` è già catturato, ma il
ripristino vive **solo dentro il `catch`** — e un rifiuto RLS non lancia eccezioni. Non c'è ramo
`else` su `saved is null`: niente ripristino, **niente banner**. Le note di una serata sparirebbero
senza un segnale. Va allineato agli altri: ripristino *e* `errorMessage`.

### Cosa non entra

**Nessun toast di conferma sui salvataggi riusciti.** Il silenzio quando va bene è corretto: al tavolo
un toast a ogni tocco di pallino è rumore. Si rende visibile solo il fallimento, che oggi non si vede.
(Chiude il 🟢 «Conferma sui salvataggi impliciti» del backlog §3 — non facendolo, e con una ragione.)

## 3. Lo zaino

### Oggi

`OpenAddItemForm` apre un form con **Nome, Quantità, Peso, Tipo, Descrizione**; se il tipo è un'arma
si aggiungono bonus d'attacco, danno, tipo di danno, note e **tre caselle** (finesse / a distanza /
non competente): fino a **12 controlli** prima di poter salvare. Il gesto reale al tavolo è «ho preso
3 pozioni».

### La forma scelta: un solo form, tre campi in vista

**Nome + Quantità + Tipo** sempre visibili; Peso, Descrizione e l'intero blocco arma dietro un
pannello richiudibile.

Regge su tre fatti **verificati sul codice**, non assunti:

- **Solo il nome è obbligatorio** (`CharacterItemsTab.razor:626`); la quantità si autocorregge a 1
  (riga 631).
- **Il bonus d'attacco vuoto attiva il calcolo automatico** — lo dichiara il placeholder stesso
  (riga 60), e `Services/WeaponCalculations.cs` esiste. Una spada salvata con solo Nome e Tipo produce
  **già oggi** una riga di combattimento usabile.
- **Il completamento successivo ha un meccanismo nato apposta**: il form di modifica dichiara in
  commento (righe 659-663) di esistere per retro-compilare le armi incomplete.

Il **Tipo** resta in vista perché è il selettore che alimenta il tab Combattimento
(`CharacterItemsTab.razor:542-546`, il valore `"weapon"` → consumato in
`CharacterCombatTab.razor:99-104`): nasconderlo
spezzerebbe il legame fra zaino e combattimento nel momento della creazione.

### Le due alternative scartate, e perché

- **Quick-add a un campo solo**: fallisce il caso citato dall'utente, perché la **quantità fa parte
  del gesto** («3 pozioni»).
- **Predefiniti per tipo**: **non esiste un catalogo di equipaggiamento** — verificato su
  `Models/Packages/CatalogPackage.cs`, dove `equipment` compare solo come testo libero del background.
  Sarebbero contenuto nuovo da scrivere, mantenere e tradurre, per risparmiare un campo Peso
  facoltativo.

**Contropartita dichiarata:** un'arma nascerà spesso senza il danno finché non la si riapre — una riga
di combattimento monca, ma onesta. E chi vuole compilare tutto paga un tocco in più.

## 4. Le monete

### Il caso peggiore non è pagare, è incassare

Oggi esistono due percorsi, **entrambi da cinque caselle** (MP/MO/ME/MA/MR): l'editor completo
(`ApriEditorMonete`, riga 888) e la spesa (`isSpendingMoney`, con anteprima su `CoinConversion.Spendi`).

L'**incasso non esiste come operazione**. Per «+30 mo» si apre l'editor, si legge il valore attuale, si
**somma a mente** e si riscrive: un read-modify-write eseguito nella testa del giocatore. Non è un
percorso lento, è un percorso **sbagliato**.

### La forma scelta

Due pannelli gemelli, **Usa** e **Ricevi**, entrambi con **un campo numerico + un selettore di taglio
a segmenti** (MP/MO/ME/MA/MR, default **MO**). I cinque campi misti restano dietro un «più tagli».

**È una modifica di sola forma sulla spesa.** `CoinConversion.Spendi` accetta già la quintupla
arbitraria (`Services/CoinConversion.cs:176`): «12 mo» è `Spendi(0,12,0,0,0)`. Anteprima, regola dei
tagli e «non hai spiccioli: si rompe 1 mo» restano **identici** — nessun rischio sul valore già
costruito e testato.

**L'incasso è un helper nuovo, `CoinConversion.Incassa`**, più semplice della spesa: pura addizione,
nessun taglio da rompere, anteprima ridotta a «Ora hai: …». Stessa disciplina calcola-poi-`Applica`
già usata dagli altri metodi del file, e stessa aritmetica in `long` con il clamp di `TotaleInRame`
per l'overflow su `int`.

**L'editor completo a cinque caselle resta invariato**: è la correzione/allineamento col foglio
cartaceo, un mestiere diverso dal gesto post-turno.

### L'alternativa scartata

**Campo unico in oro con decimali** (`12,5`): introdurrebbe il parsing di decimali con la virgola
sotto `InvariantGlobalization` — gotcha già pagato da questo progetto — e non corrisponde al modello
mentale del tavolo, dove il prezzo si dice per taglio. Il selettore costa lo stesso singolo tocco
senza quel rischio.

**Contropartita dichiarata:** un secondo pulsante consuma spazio verticale su mobile, e il pagamento a
tagli misti (raro) costa un tocco in più.

## 5. Le specie collegate al manuale

`CharacterManualJoin.BackgroundRiconosciuto` (`Services/CharacterManualJoin.cs:70`) prende il nome
scritto sul personaggio, lo confronta **normalizzato ed esatto** col catalogo e restituisce la voce.
Serve il gemello per le specie: `Character.Race` è un nome, e `PackageSpecies`
(`Models/Packages/CatalogPackage.cs:42`) porta **sia `Description` sia `Traits`** — verificato.

**`Traits` è una stringa sola, non un elenco.** Scurovisione e Resistenza implacabile non sono
separabili automaticamente: arrivano come blocco di testo del manuale, **sotto** la casella «TRATTI
DELLA SPECIE» che l'utente già scrive a mano, non al posto suo. È lo stesso limite già registrato il
2026-08-08.

Chiude il 🟡 «Restano le specie» del backlog §3.

## 6. Cosa resta fuori di proposito

- **Riquadro PROVE** con le 18 abilità in colonna — è l'attrito di lettura che l'utente ha nominato
  per primo, ed è il **candidato numero uno del giro successivo**. Fuori da qui perché aggiungerebbe
  un riquadro alla board appena assestata, contro il vincolo di §1.
- **Glossario delle regole base** (tiro salvezza, competenza, CD): indipendente da tutto, backlog §3.
- **Descrizioni dei privilegi di classe**: bloccate sulla fonte — nel pacchetto SRD
  `levels[].features` è `List<string>`. Servirebbe il PDF ufficiale SRD 5.2.1 italiano.

## 7. Test

Il progetto non ha component-testing, quindi vale la regola di sempre: **la logica va in helper puri**.

- **`CoinConversion.Incassa`**: l'invariante è *il totale in rame cresce esattamente di quanto
  incassato*, e **i tagli non coinvolti restano identici** — la stessa proprietà che regge `Spendi`.
  Va scritto **accanto al valore** quale caso rende il test non vacuo, e provato per mutazione
  (sostituire con un no-op deve produrre **rosso**). Questo repo ha già incontrato **quattro** test
  vacui, e l'ultimo nasceva da un'istruzione dell'orchestratore, non da chi scriveva.
- **Overflow**: incassare oltre `int.MaxValue` rame deve clampare, non traboccare.
- I sei punti di §2 non sono testabili senza bUnit: la garanzia è che il **tipo** cambia
  (`Func<Task<bool>>`), quindi un call-site che ignora l'esito **non compila**. È una verifica
  strutturale, e vale più di un test.

## 8. Partizione per il piano

Tre insiemi di file **disgiunti**, lanciabili in parallelo:

| Unità | File | Confine |
|---|---|---|
| **A — esito** | `StatCard.razor`, `CharacterStatsTab.razor`, `CharacterMagicTab.razor`, `Pages/Characters.razor` (solo `SaveNotesAsync` e i call-site) | non tocca `CharacterItemsTab` |
| **B — zaino e monete** | `CharacterItemsTab.razor`, `Services/CoinConversion.cs`, `Tests/CoinConversionTests.cs` | **include** la conversione dei suoi 3 punti ciechi |
| **C — specie** | `Services/CharacterManualJoin.cs`, il punto di rendering, `Tests/` | indipendente |

**A e B toccano entrambi il contratto `OnChanged`, su file diversi.** È una **giuntura**, ed è
esattamente la classe di difetto che il gate individuale non vede per costruzione: il gate finale va
puntato lì — chi passa la funzione contro chi la riceve — e non solo sul diff in blocco.

`Pages/Characters.razor` è toccato **solo** da A: se B avesse bisogno di cambiarne un call-site, va
serializzato o passa da A.

## 9. Rischi

- **Il rischio maggiore è di scope**: §3 e §4 toccano un file da 985 righe che contiene già quattro
  modalità (lista, aggiunta, modifica, denaro). Il form progressivo è una modifica di **visibilità**,
  non di struttura: se emerge la tentazione di riscrivere il form, va fermata.
- **`SetAttunementAsync`** (sintonie) entra nel perimetro di §2 pur non essendo stato nominato
  dall'utente: è nello stesso file e nello stesso contratto, e lasciarlo indietro significherebbe
  ripassare di lì.
- **Nessuna migrazione**, nessun cambio a RLS, nessuna colonna nuova. Il deploy non richiede
  interventi sul database.
