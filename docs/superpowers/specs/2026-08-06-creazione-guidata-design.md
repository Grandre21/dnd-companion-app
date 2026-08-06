# La creazione guidata — design

**Data:** 2026-08-06 · **Stato:** approvato, da pianificare
**Origine:** `docs/DA-FARE.md` §9 punto 1, deciso col consulto di analisi del 2026-08-06.
**Consulto:** Fable, 2026-08-06 — punti A (catena), B (competenze), C (form di modifica).

## Perché

Il wizard di creazione **produce personaggi incompleti**, e nessuno se ne accorge: build verde, test
verdi, scheda che si apre. Mancano tre cose, e ognuna si paga più tardi.

| Manca | Conseguenza |
|---|---|
| Gli **slot incantesimo** | un Mago di 1° nasce senza slot: il tab Magia c'è ma non si può lanciare nulla |
| `SpellcastingAbility` | CD degli incantesimi e bonus d'attacco magico **vuoti** ([[dndcompanion-stack-gotchas]] §10) |
| Le **competenze vincolate** | il wizard mostra «2 tra Atletica, …» come prosa e non fa scegliere: le caselle si spuntano dopo, a mano, senza vincolo — e chi non lo sa parte con zero competenze |

E manca il caso che al tavolo capita davvero: **entrare in una campagna già avviata**, cioè creare un
personaggio al 5° livello. Oggi si digita «5» nel campo livello e si arriva a una scheda con i PF di
un 5° ma **senza** sottoclasse, senza il talento del 4°, con gli slot di nessun livello.

Il motore che sa fare tutto questo **esiste già** dal 2026-08-06: `LevelUpPlanner`. La tappa non è
scrivere una progressione: è **collegarla**.

## Il principio

> **Il wizard possiede il livello 1. Il planner possiede i livelli 2→N.**

Non ci sono due motori, e non ce ne sarà mai un secondo: qualunque strada in cui il wizard calcoli da
sé cosa succede salendo produce due implementazioni della stessa tabella, che divergeranno alla prima
correzione applicata a una sola delle due. Vale anche per il livello 1, dove il planner non gira:
slot e caratteristica da incantatore si leggono dalle **stesse funzioni** che il planner usa —
`ClassProgression.SlotFinoAl(testo, 1)` e `LevelUpRules.CaratteristicaIncantatore(classe)` — non da
un calcolo scritto nel wizard.

## Decisioni

| # | Decisione | Perché |
|---|---|---|
| D1 | **Il wizard possiede il 1°, il planner i 2→N.** Il livello 1 usa comunque le funzioni del planner, non calcoli propri. | Il principio qui sopra. |
| D2 | **Lo stato del wizard non è mai «il personaggio al livello k»**: è la coppia **(baseline, risposte per livello)**. Il personaggio a livello N si **deriva** rieseguendo il fold. | `Applica` muta l'istanza che riceve: su quattro giri concatenati un undo per snapshot sarebbe fragile. Col replay tornare indietro non richiede undo — si butta il derivato e si rifà — ed è corretto per costruzione anche quando l'utente cambia idea sull'ASI del 4° e il retroattivo di Costituzione va rifatto da capo. |
| D3 | **Pipeline rigida e mai interlacciata:** assembla baseline (**incluso** il sync di `FinalScores`) → valida → fold → `Normalize` → salva. | `SaveAsync` sincronizza oggi le sei caratteristiche **in coda** (`CharacterWizard.razor:1147-1153`). Se il fold girasse prima, quel sync **cancellerebbe l'ASI** applicato al 4° — il gemello esatto del bloccante `CloneCharacter` del 2026-08-06, e altrettanto silenzioso. |
| D4 | **`SuggestMaxHp` non si rimuove.** Resta come seed del 1°, come ripiego per la classe senza tabella, e come **test che incrocia le due fonti**. | Grazie al retroattivo di Costituzione la catena con la media produce **lo stesso numero** di `SuggestMaxHp` calcolato sulla COS finale: divergono solo se l'utente tira i dadi, dove il planner è semplicemente più capace. Due fonti che devono coincidere sono un test gratuito (v. «test auto-vigili» in `CLAUDE.md`). |
| D5 | **Passo «Progressione» a elenco**, una riga per livello: i livelli senza decisioni si **auto-confermano con la media**, solo quelli con una decisione vera chiedono un tap. | Creare un 5° costa 2 tap invece di attraversare quattro volte lo stesso dialogo. Il caso dominante resta zero attrito, come già per il level-up. |
| D6 | **Nessun default automatico per le `DecisioneFraOpzioni`.** | Scegliere la sottoclasse o il talento al posto del giocatore non è un default: è una scelta rubata. L'auto-conferma di D5 vale **solo** per i livelli che non chiedono nulla. |
| D7 | **Il tiro dei punti ferita è per livello.** | Oggi è un singolo `int?` per piano, e nella catena servono N−1 tiri indipendenti. L'app continua a non tirare i dadi al posto dell'utente. |
| D8 | **Le competenze N-su-M vivono in un helper nuovo, non nei contratti del level-up.** | `Decisione`/`Risposta` sono contratti *del piano*: chiavi `L{n}:tipo`, applicazione differita da `Applica` — che **non ha alcun caso** che scriva i `prof_*`. Forzarcele dentro darebbe o un allargamento di `Applica` per un caso che non è un level-up, o una `Decisione` che non passa mai da `Applica`: un contratto che mente sul proprio ciclo di vita. Condividono la filosofia, non i tipi. |
| D9 | **La mappa abilità esce da `StatCard.razor`.** | Oggi `SkillLabel` è `private` dentro un `.razor`: logica di dominio nel markup, contro il pattern chiave del progetto. Ne hanno bisogno il picker, il parsing delle competenze di background e — domani — un talento che conceda abilità. È il **ponte** fra i due mondi, e deve essere uno solo. |
| D10 | **Il degrado del vincolo è totale, mai parziale.** Vincolo assente o anche un solo nome non mappabile → picker libero a 18 caselle, con la prosa come suggerimento. | Le 18 coppie di bool **non sanno rappresentare** un'abilità homebrew: un picker vincolato che ne elencasse una la renderebbe irraggiungibile. Il vincolo vale intero o non vale. |
| D11 | **Il conteggio esatto blocca «Avanti».** | È l'obiettivo dichiarato della tappa: il wizard smette di creare personaggi incompleti. Una competenza dimenticata alla creazione poi non la rivendica più nessuno. Il blocco vale **solo** dove il vincolo esiste (D10): il picker libero non blocca mai. |
| D12 | **Le competenze di background si pre-spuntano; la sovrapposizione si segnala, non si vieta.** | Sono *concesse*, non scelte. Le regole 2024 permettono di sostituire un duplicato, e la mano dell'utente è sovrana: badge «già dal background» e avviso di spreco, casella comunque selezionabile. |
| D13 | **Le scelte si invalidano al cambio di classe; le concesse si ricalcolano al cambio di background.** | Stessa finestra di `ApplyClassSaveProficiencies`, che già azzera-e-riapplica: scelte per il Barbaro, cambio in Mago, e le caselle del Barbaro resterebbero addosso al draft. |
| D14 | **Il form di modifica resta fuori da questa tappa.** | v. sotto. |

## La catena

### La forma

```
baseline (livello 1, mai salvato)
   │
   ├── livello richiesto = 1 ──────────────────► salva
   │
   └── livello richiesto = N > 1
          │
          clone del baseline
          │
          for k = 2 … N:
              piano = LevelUpPlanner.Pianifica(pg, tabella, sottoclassi, talenti,
                                               risposte[k], tiri[k], dadoVita)
              se piano è null  → catena interrotta, si ripiega (v. «Senza tabella»)
              pg = LevelUpPlanner.Applica(pg, piano, risposte[k])
          │
          └──► personaggio derivato ──► valida ──► Normalize ──► salva
```

Il fold **si riesegue da capo** a ogni cambiamento: cambio di una risposta, cambio del livello
richiesto, cambio di una caratteristica base. Non si mantiene mai uno stato intermedio mutato.

`Pianifica` e `Applica` **non leggono `Id`, non fanno I/O, non passano dal repository**: su un draft
non salvato funzionano già oggi, senza modifiche al motore.

### Il livello 1

Il planner non gira, ma le fonti restano le sue:

- **Slot**: `ClassProgression.SlotFinoAl(tabella, 1)`, ampliato a nove valori e scritto in
  `SpellSlots1Max … SpellSlots9Max`. Un Mago di 1° nasce con i suoi due slot di 1° cerchio.
- **Caratteristica da incantatore**: `LevelUpRules.CaratteristicaIncantatore(classe)`, che scrive la
  chiave **inglese minuscola** — `"intelligence"`, non `"Intelligenza"`. È la trappola §10 delle
  gotchas: il nome italiano lascia CD e bonus d'attacco vuoti, con build e test verdi.
- **Punti ferita**: `CharacterWizardLogic.SuggestMaxHp` con livello 1 (dado pieno + modificatore di
  Costituzione), che è già ciò che fa oggi.

### Senza tabella

Una classe del tavolo scritta a mano non ha progressione: `Pianifica` torna `null`. Il ripiego è un
**percorso dichiarato, non un `null` silenzioso** — il passo «Progressione» mostra il motivo («Questa
classe non ha una tabella dei livelli: i punti ferita sono una stima, il resto va compilato a mano»)
e il wizard degrada al comportamento di oggi: campo livello libero e PF da `SuggestMaxHp`. Il tavolo
homebrew non è mai bloccato.

## Le competenze

### Il contratto

Due helper puri nuovi, e la mappa promossa fuori dal markup.

- **`SkillCatalog`** — la fonte unica: `SkillType` ↔ nome italiano ↔ le due proprietà `Prof*`/`Exp*`
  del `Character`. Il match dei nomi ignora maiuscole e accenti: la prosa di un tavolo non garantisce
  le maiuscole dello SRD, e «Rapidità di mano» si scrive anche senza accento.
- **`SkillChoiceRules`** — il vincolo e la sua validazione. Due sovraccarichi di `Vincolo`: uno per
  `PackageSkillChoices` (classe di pacchetto, dato già strutturato), uno per il testo libero della
  classe di campagna, che passa da `PackageRowMerge.LeggiScelte` — la funzione che **esiste già** e
  sa invertire il formato canonico `"2 fra: Arcano, Storia"` restituendo `null` sulla prosa che non
  riconosce. Non si scrive un secondo parser.

`StatCard.razor` **delega** a `SkillCatalog` invece di tenere la propria copia: è un refactor a
comportamento invariato, e la coincidenza fra i due elenchi va inchiodata da un test — i nomi del
pacchetto SRD e quelli della mappa devono combaciare **esattamente**, o il vincolo cade in silenzio
sul degrado totale di D10 proprio dove dovrebbe funzionare.

### Il background

`skillProficiencies` è una lista nel pacchetto e testo separato da virgole nella riga di campagna:
entrambi si leggono con `SkillCatalog.DaNome`. Le abilità concesse si pre-spuntano e portano il badge
(D12); se il giocatore ne sceglie una già concessa, avviso di spreco e casella comunque selezionabile.

## L'ordine dei passi

L'ordine attuale non regge la catena: il **livello** si sceglie in Dettagli (passo 5), ma la
progressione ha bisogno delle **caratteristiche finali** (passo 4) prima di girare, e deve
pre-riempire i **punti ferita** che stanno in Dettagli.

| Oggi | Dopo |
|---|---|
| 1 Specie · 2 Classe · 3 Background · 4 Caratteristiche · 5 Dettagli (con Livello) · 6 Riepilogo | 1 Specie · 2 Classe (**con Livello** e le **competenze**) · 3 Background · 4 Caratteristiche · **5 Progressione** · 6 Dettagli · 7 Riepilogo |

Il passo «Progressione» **non compare** se il livello richiesto è 1: chi crea un personaggio nuovo
non deve attraversare un passo vuoto.

Una giuntura funziona già gratis: la sottoclasse scelta al passo Classe **pre-risponde** la decisione
del 3° livello, perché `CostruisciDecisioni` la salta quando `pg.Subclass` è valorizzata.

## Il form di modifica: perché resta fuori

`CharacterEditForm.razor` (790 righe) duplica il markup del wizard e non calcola nulla. La
duplicazione è reale, ma è **il sintomo sbagliato da curare**: i due file hanno contratti *opposti*,
non lo stesso contratto scritto due volte. Il wizard **deriva** valori da scelte; il form **presenta**
valori memorizzati con libertà totale — e su un personaggio a metà campagna è *giusto* che non
calcoli: i punti ferita massimi divergono legittimamente dalla formula (oggetti, privilegi, ruling
del tavolo), e un form che «aiutasse» ricalcolandoli riscriverebbe **valori veri con valori teorici**,
la stessa famiglia di corruzione del master che assegna 100 monete con una copia stantia.

Unificare il markup spingerebbe verso l'unificazione dei comportamenti, che è l'errore. Ciò che potrà
entrarci, in una tappa sua: i pulsanti **opt-in** «Usa suggerito», che propongono senza scrivere, e
gli avvisi soft del vincolo competenze. Ciò che non deve entrarci mai: la catena del planner sul campo
Livello — modificare il livello a mano nel form è un override deliberato, e il percorso guidato è e
resta il dialogo di level-up.

Il candidato naturale della tappa successiva è l'estrazione del trio selettore
specie/classe/sottoclasse, che vive **identico** nei due file — `ScollegaSottoclasseNonPiuValida`
compreso.

## Fuori perimetro

- **Nessuna migrazione, nessuna colonna nuova, nessun data entry.**
- **Nessun equipaggiamento iniziale**: il pacchetto porta l'equipaggiamento del background come prosa,
  e trasformarlo in righe di inventario è un parser di prosa libera — cioè la cosa che questo progetto
  ha già deciso di non fare due volte.
- **Nessun incantesimo scelto in creazione**: il tab Magia esiste e fa già quel lavoro.
- **Nessun effetto dei privilegi di sottoclasse**: resta il punto aperto §3 di `DA-FARE`.
- **Il form di modifica** (D14).

## I rischi

In ordine di gravità. I primi tre sono **di giuntura**, la classe che `CLAUDE.md` marca come la più
grave perché nessun autore di una singola fetta può vederla.

1. **Il sync `FinalScores` che cancella l'ASI** (D3). Il più grave e il più silenzioso: build verde,
   test verdi, e il talento del 4° semplicemente non c'è.
2. **`CloneCharacter` e il baseline**: il fold gira su un clone, e un campo che `CloneCharacter` non
   copia sparisce dal personaggio creato. `Tests/CharacterCloneTests.cs` confronta già per
   riflessione ogni proprietà: se la fetta ne aggiunge una, il test la reclama.
3. **Il reset al cambio di classe/background** (D13).
4. **Il tiro PF per livello** (D7): oggi è un valore solo, e riusarlo per tutti i livelli darebbe lo
   stesso tiro a ogni dado.
5. **Il ripiego senza tabella** (v. «Senza tabella»): deve essere visibile, non un `null` che passa
   inosservato.

## Ordine

1. **`SkillCatalog` + `SkillChoiceRules`**, con `StatCard` che delega. Nessuna UI: helper e test.
2. **`CreationChain`**, il fold puro. Nessuna UI: helper e test, incluso l'incrocio con
   `SuggestMaxHp` (D4).
3. **Il wizard**: passo Progressione, picker competenze, slot e caratteristica al 1°, riordino dei
   passi, pipeline di salvataggio (D3).

Le prime due sono indipendenti e si possono scrivere in parallelo; la terza le consuma entrambe. Il
gate va puntato sulle **giunture**: 1↔3 (chi scrive i `prof_*`), 2↔3 (chi possiede il livello),
3↔`SaveAsync` (l'ordine della pipeline), 2↔`LevelUpPlanner` (chi muta il personaggio).

## Verifica

- `dotnet build -c Release` pulita, `dotnet test` verde.
- Test che incrocia i nomi di `SkillCatalog` con quelli del pacchetto SRD (tutte e 12 le classi):
  ogni voce di ogni `skillChoices` deve mappare.
- Test che incrocia catena-con-media e `SuggestMaxHp` (D4): devono coincidere.
- Test che il fold sia **idempotente**: rieseguirlo con le stesse risposte dà lo stesso personaggio.
- Test che il sync `FinalScores` preceda il fold (D3): un baseline con ASI al 4° deve conservarlo.
- Verifiche manuali da aggiungere a `DA-FARE`: creazione di un Mago di 1° (slot e CD presenti);
  creazione di un Barbaro di 5° (sottoclasse al 3°, talento al 4°, PF coerenti); creazione con una
  classe del tavolo senza tabella (il ripiego si vede ed è spiegato); cambio di classe dopo aver
  scelto le competenze (le vecchie spariscono).
