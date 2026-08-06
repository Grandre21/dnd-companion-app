# Level-up guidato — design

**Data:** 2026-08-06 · **Stato:** approvato, da pianificare
**Copre:** `docs/DA-FARE.md` §3, i primi due punti («motore di derivazione condiviso», «level-up guidato»).

## Perché

Oggi salire di livello significa aprire il form di modifica e correggere a mano punti ferita, dado
vita, nove slot incantesimo e le competenze. È l'attrito che torna a ogni sessione di gioco, ed è
l'unico punto dell'app dove il giocatore deve conoscere le regole *meglio* dell'app che le contiene.

Il modello di riferimento è Baldur's Gate 3: al passaggio di livello il gioco propone le sole scelte
legali, calcola tutto il resto e non chiede mai di digitare un numero. Il perimetro qui è più
stretto, e la differenza è deliberata: BG3 conosce la *semantica* di ogni privilegio perché ha un
reparto contenuti dietro. Quest'app conosce le *tabelle*, che è ciò che serve per togliere l'attrito.

## Decisioni

| # | Decisione | Perché |
|---|---|---|
| D1 | **Guida vincolante nel flusso, con via di fuga.** Nel dialogo si vedono solo scelte legali e i derivati sono in sola lettura. L'editor libero di oggi resta invariato come strada separata. | Il tavolo ha PG scritti a mano e classi homebrew. Un motore che «sa la verità» deve convivere con loro senza rifiutarli. |
| D2 | **Si parte dal level-up, non dalla creazione.** | Il level-up oggi non esiste affatto; la creazione esiste e funziona male. E costruire il primo obbliga a scrivere il motore che la seconda riuserà. |
| D3 | **Un livello alla volta.** Niente salto 3°→7°. Il recupero si fa ripetendo, con un «Sali ancora» nel toast. | Dentro un salto nascono dipendenze in cascata: la sottoclasse scelta al 3° sblocca privilegi al 6°, l'incremento di Costituzione al 4° cambia i PF del 5°-7°. Triplo della superficie di bug per il caso raro. È anche come fa BG3. |
| D4 | **L'app non tira mai il dado dei PF.** Due opzioni: media (preselezionata) o inserimento di un risultato tirato al tavolo, vincolato a `1..dado`. | Il tiro nell'app richiederebbe di renderlo vincolante — cioè registrato e non ripetibile — per non essere un reroll silenzioso. Non offrendo l'RNG, il problema non esiste. Il dado si tira davanti al master: è il momento sociale, e l'app non deve arbitrarlo. |
| D5 | **Davanti a una scelta di cui non conosciamo le opzioni** (invocazioni occulte, metamagia, maestrie): campo di testo libero con avviso «il manuale non è nei dati». | Zero data entry nella v1. L'app non finge di sapere le regole, ma non lascia dimenticare che una scelta c'era. |
| D6 | **Il piano è funzione delle risposte, non un valore statico.** Si rigenera a ogni risposta. | L'incremento di Costituzione ha **effetto retroattivo sui PF**: +2 COS all'8° vale +1 PF per ogni livello già posseduto. Un piano statico divergerebbe dalle regole proprio nel flusso che promette di calcolarle. |
| D7 | **Zero migrazioni.** Si scrive solo in colonne che esistono. La caratteristica da incantatore sta in un helper di codice, non nel formato di scambio. | Tiene la v1 fuori dalla fascia a tre giri di gate e dal publish trimmato obbligatorio, e non crea incompatibilità col client PWA rimasto in cache. |

## Architettura

Un helper puro `Services/LevelUpPlanner.cs`, nello stile di `ClassProgression`: classe inglese,
membri italiani, nessuna dipendenza da Blazor. Il calcolo è separato dall'applicazione.

```csharp
/// Cosa comporta salire di un livello, viste le risposte date finora.
/// null = questa classe non ha una tabella: nessuna guida, resta il form libero.
public static LevelUpPlan? Pianifica(Character pg, string? testoProgressione,
                                     IReadOnlyList<PackageSubclass> sottoclassi,
                                     IReadOnlyList<PackageFeat> talenti,
                                     Risposte risposte);

/// Produce il PG salito di livello. Separata da Pianifica: testabile senza UI.
public static Character Applica(Character pg, LevelUpPlan piano, Risposte risposte);

/// Il diff che la UI mostra: sempre attuale e proposto affiancati.
public sealed record Proposta<T>(T Attuale, T Proposto);
```

### Le decisioni pendenti

Tre forme, non una gerarchia di casi speciali. **Il nome `Decisione` è scelto per non collidere con
`SubclassCatalog.SceltaSottoclasse`**, che esiste già e significa un'altra cosa.

| Forma | Copre | Opzioni da |
|---|---|---|
| `DecisioneFraOpzioni` | sottoclasse, talento Generale (livelli 4/8/12/16), stile di combattimento, dono epico (19°) | `subclasses` del catalogo, `feats` filtrati per `category` |
| `DecisionePunteggi` | la sotto-scelta +2 a uno / +1 a due, aperta **solo** se il talento scelto è l'incremento | i punteggi del PG |
| `DecisioneLibera` | invocazioni occulte, metamagia, maestrie | nessuna: testo, con avviso |

Nella 5e 2024 l'incremento di caratteristica **è un talento** (categoria Generale). I livelli
4/8/12/16 non sono quindi un caso speciale: sono «scegli un talento Generale», e solo scegliendo
quello si apre la `DecisionePunteggi` come figlia.

**Chiavi delle decisioni:** `L{n}:{tipo}` — `L4:talento`, `L4:talento/punteggi`, `L3:sottoclasse`.
Il prefisso di livello serve anche con D3, perché è lo stesso prefisso delle righe appese ai campi
testuali, e rende il flusso idempotente se ripetuto.

### Regole di calcolo

- **I PF si sommano, non si ricalcolano da zero.** `MaxHitPoints + media(dado) + modCOS`, più il
  retroattivo se la risposta all'incremento cambia il modificatore di Costituzione. Chi ai livelli
  passati ha tirato i PF non se li vede silenziosamente sovrascritti. Il ricalcolo col metodo medio
  resta un'azione esplicita e separata, mai il default.
- **Gli slot sono assoluti**, da `ClassProgression.SlotFinoAl`: la tabella è la verità e il vettore
  intero è sicuro anche su un PG incoerente. Verificato che regge il Warlock, la cui magia del patto
  il pacchetto codifica come `[0,0,0,0,4]` al 20°.
- **`Pianifica` torna `null`** se `ClassProgression.Risolvi` non trova una tabella. Il motore guida
  dove ha dati; non inventa.
- **Il planner non riparsa mai il testo dei livelli**: passa solo da `Leggi`/`FinoAl`/`SlotFinoAl`.
  Un secondo parser dello stesso formato è il difetto di giuntura classico.
- **La sottoclasse già presente passa da `SubclassCatalog.RisolviScelta`**, che esiste e sa già
  distinguere «a catalogo» / «scritta a mano» / «di un'altra classe». Il dialogo non deve **mai**
  azzerare una sottoclasse scritta a mano: è il terreno delle sei perdite silenziose già annotate
  fra le verifiche manuali di `DA-FARE`.

### Cosa `Applica` scrive, e cosa non tocca

Scrive **solo** in: `Level`, `MaxHitPoints`, `HitPoints`, `HitDiceMax`, i nove `SpellSlotsNMax`,
`Subclass`, `SpellcastingAbility` (solo se vuota), `ClassFeatures`, `Feats`, e i punteggi toccati
dall'incremento.

I **PF correnti crescono dello stesso ammontare dei massimi**, anche su un PG ferito: è la regola,
non un arrotondamento di comodo. Un PG a 30/45 che guadagna 7 massimi va a 37/52, non a 30/52 né a
52/52 — il primo lo penalizzerebbe, il secondo lo curerebbe gratis.

**Mai** gli `SpellSlotsNUsed`: a metà giornata di gioco gli slot spesi sono dati vivi.

Le righe appese ai campi testuali sono **auto-descrittive, col prefisso di livello**: `L6: Ira
migliorata`, `L4: Talento — Attento`. Così correggere a mano è lettura, non archeologia, e un
eventuale undo futuro avrebbe già gli appigli sintattici senza che oggi si costruisca nulla.

**`HitDiceMax` sporco** (vuoto, `5` senza dado, o multiclasse `3d12+2d8`): nei primi due casi si
propone l'assoluto da `BuildHitDice`; nel terzo **non si tocca il campo** e si mostra un avviso. Il
planner assume classe singola e deve dichiararlo, non pasticciare.

## Il flusso

Un bottone «Sali di livello» sulla scheda — visibile a chi già può editarla, nascosto al 20°. Nessun
permesso nuovo, nessuna RLS toccata.

**Hub con checklist, non un wizard a passi.** Il caso dominante è *zero scelte*: lì dev'essere un
tap, un colpo d'occhio, conferma. Un wizard lineare punirebbe il caso frequente con tre «Avanti»
vuoti per ottimizzare quello raro.

```
┌─ Diventa livello 6 ─────────────┐
│  BARBARO 5 → 6                  │   il guadagno prima della burocrazia:
│                                 │   si apre sui privilegi, non su un form
│  OTTIENI                        │
│   • Ira implacabile             │
│                                 │
│  PF max      45 → 52            │   i derivati come delta.
│  Dado vita   5d12 → 6d12        │   la freccia è la ricompensa
│  Competenza  +3                 │
│                                 │
│  (●) Media +7   ( ) Ho tirato   │
│                                 │
│  DA SCEGLIERE                   │   righe con stato; il tap apre
│   Talento Generale  ▸ da fare   │   il pannello e si torna qui
│                                 │
│         [ Diventa livello 6 ]   │   il bottone nomina l'evento
└─────────────────────────────────┘
```

- **Nessuna schermata di riepilogo separata**: se i derivati sono in sola lettura, il dialogo *è già*
  il riepilogo. Il riepilogo è il toast dopo la conferma, con «Sali ancora».
- **Le opzioni si leggono in accordion**, una espansa alla volta, nome più prima frase. Delle
  sottoclassi **non** si mostra la prosa intera (~2.900 caratteri): introduzione più i soli privilegi
  del livello d'ingresso, che `SubclassCatalog.PrivilegiFinoAl` e `PrimoLivello` già estraggono.
  Niente confronto affiancato: con i dati SRD le opzioni sono spesso una o due, e su 360px non c'è
  spazio. Si riparla se un giorno i cataloghi si riempiono.
- **Conferma abilitata solo a checklist completa**, e un solo `UpdateCharacterAsync` **atomico**: un
  salvataggio a metà è l'unico modo di corrompere una scheda con questo flusso. Se fallisce, il
  dialogo resta aperto con le risposte intatte, errore via `DbErrorBanner`, ritentabile.
- **Il dialogo non salva**: restituisce il PG mutato via `EventCallback`, e la scheda salva col suo
  percorso esistente. Nessun repository iniettato nel componente.
- **Contenitore dedicato**, non `ConfirmDialog` (che è un sì/no): foglio a tutto schermo su mobile,
  scroll interno, conferma sticky in basso — con l'attenzione alla barra URL dinamica di Android già
  annotata in `DA-FARE`.
- **Cerchio nuovo sbloccato**: quando il diff degli slot apre un cerchio mai avuto, una riga nel
  toast con link al tab Magia. Costa una condizione e un link, ed è metà della sensazione di
  ricompensa per un incantatore.

## Fuori perimetro (dichiarato)

- **Nessun rules engine.** Il motore propone numeri e presenta scelte; non interpreta mai la
  semantica di un privilegio. «Difesa senza armatura» arriva come nome, non diventa una formula per
  la classe armatura. Attraversare questa linea significa data entry di meccaniche per sempre.
- **Nessun multiclasse.** `Character.Class` è una stringa singola e resta tale.
- **Nessun log delle scelte, nessun undo.** Le righe auto-descrittive rendono la correzione manuale
  praticabile. Se dopo mesi di uso reale il «disfa l'ultimo livello» emerge come bisogno vero, il log
  si aggiunge allora, con un caso d'uso davanti.
- **Nessun conteggio di incantesimi conosciuti o preparati**: i dati non ci sono, e inventarli
  sarebbe peggio che tacere.
- **Niente auto-level da punti esperienza**, e niente promozione di gruppo del master: non potrebbe
  essere atomica, perché le scelte spettano ai giocatori, e si ridurrebbe a infrastruttura per un
  messaggio che il master pronuncia a voce.
- **Nessun ricalcolo retroattivo dei PG esistenti.** Le incoerenze diventano un avviso accanto al
  campo, mai una correzione. Un bottone «rimetti tutto a norma» è una tentazione da resistere.

## Fette e giunture

Il lavoro va a più agenti in parallelo. I difetti gravi stanno dove una fetta incontra l'altra, e
nessun autore può vederli per costruzione: le giunture sono quindi dichiarate qui, non lasciate
emergere.

| Fetta | File | Note |
|---|---|---|
| **0 — contratti** | i record e le firme (senza implementazione) + `Services/ClassSpellcasting.cs` + `Tests/ClassSpellcastingTests.cs` | **Una sola mano, prima del fan-out.** È il 20% del lavoro che determina il 100% delle giunture. La mappa è dati puri: dodici righe, e il test che le incrocia col JSON sta con lei. |
| **1 — motore** | `Services/LevelUpPlanner.cs`, `Tests/LevelUpPlannerTests.cs` | `Pianifica` **e** `Applica` insieme: `Applica` è l'unico punto che scrive su schede di produzione. |
| **2 — dialogo** | `Shared/CharacterTabs/LevelUpDialog.razor` + `.css` | Scritto contro i contratti della fetta 0. Non tocca repository. |
| **3 — innesto** | `Pages/Characters.razor`, `CharacterVitalsBar` | File caldi e condivisi: **dopo** le altre, da solo. |

Parallelizzabili senza ansia: 1 e 2. Da una mano sola: 0 e 3.

### I contratti, e come si verificano

| Giuntura | Contratto | Verifica |
|---|---|---|
| Motore ↔ Dialogo | I record della fetta 0. `Pianifica` → `null` significa «niente guida». Il piano si rigenera a ogni risposta. | Test: `Applica` con risposte incomplete **non muta il Character** (clone e confronto campo a campo). |
| `Applica` ↔ dati di produzione | Solo le colonne dichiarate cambiano. | **Test di whitelist**: serializza il PG prima e dopo, asserisci che differiscano *solo* le colonne elencate sopra. Mai gli `Used`, mai l'inventario, mai i punteggi non toccati dall'incremento. È il test più importante del progetto. |
| Motore ↔ `ClassProgression` | Il motore non riparsa il formato testuale. | In revisione: un `Split('\n')` dentro il planner è un finding bloccante. |
| `ClassSpellcasting` ↔ pacchetto | Ogni classe del JSON con slot non tutti zero ha una voce nella mappa. | Test che **legge `srd-2024-it.json`** e incrocia, nello stile di `SrdPackageContentTests`. Idem per i marcatori dei privilegi e le `category` dei talenti: inchioda dati e codice insieme. |
| Dialogo ↔ Scheda | Il dialogo calcola e restituisce; la scheda salva. | In revisione: nessun `@inject` di repository in `LevelUpDialog.razor`. Più prova manuale: salvataggio fallito → dialogo aperto, risposte intatte, ritentabile. |
| `Applica` ↔ `CharacterNormalizer` | Il PG uscito da `Applica` è un punto fisso di `Normalize` sui campi toccati. | Test: `Normalize(Applica(...))` non cambia nulla di ciò che il dialogo ha mostrato — altrimenti un clamp silenzioso smentisce il diff appena confermato. |
| Fetta 1 ↔ tutte | — | **Test end-to-end pure-code**, commissionato esplicitamente perché nessun singolo agente lo scriverebbe: Guerriero 2°→3° e Mago 2°→3° **con i dati reali dal JSON**, da `Pianifica` alle risposte ad `Applica`, con asserzioni su livello, PF, dado vita, slot, `SpellcastingAbility`, sottoclasse e righe appese. |

### Casi limite da coprire nei test

- PG incoerente con le regole: non viene «aggiustato».
- Classe senza tabella: `Pianifica` torna `null`.
- Tabella parziale (classe del tavolo con 5 livelli): livello target senza riga = nessun privilegio,
  non un errore.
- `Subclass` già valorizzata alla salita al 3°: preselezionata, non una scelta da rifare.
- `Subclass` scritta a mano: conservata, mai azzerata.
- Warlock: gli slot restano quelli del patto.
- Incremento su Costituzione: i PF crescono anche retroattivamente.
- `HitDiceMax` multiclasse: campo non toccato, avviso mostrato.

## Verifica prima di «fatto»

- `dotnet build` pulita e `dotnet test Tests/DndCompanion.Tests.csproj` verdi.
- Gate a due agenti puntato sulle **giunture** elencate sopra, non sul diff intero.
- Verifica manuale, da aggiungere a `DA-FARE`: salita reale di un PG del manuale con scelta, di un PG
  con classe del tavolo (il dialogo non deve aprirsi), e di un incantatore che sblocca un cerchio.
