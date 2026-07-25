# Mappa UX dei flussi — analisi degli attriti

> **Data:** 2026-07-25
> **Tipo:** documento di analisi (non uno spec di implementazione).
> **Scopo:** capire *dove* e *perché* l'inserimento delle informazioni è macchinoso, prima di decidere
> cosa vale la pena costruire. Le direzioni di soluzione qui abbozzate non sono impegni: ognuna
> richiederà il proprio spec.

---

## 0. Metodo, perimetro, metro di giudizio

**Metodo.** Analisi statica del codice sorgente: tutti i `.razor` di `Pages/`, `Shared/` e `Layout/`,
i `Models/`, i repository in `Services/Repositories/` e gli helper puri in `Services/`. Nessuna
sessione con l'app in esecuzione, nessuna telemetria. Dove compaiono numeri (campi, controlli,
interazioni) sono **contati sul markup reale**, con il riferimento `file:riga` accanto.

**Limite del metodo, dichiarato.** L'analisi statica non misura tempi, non vede il reso visivo e non
coglie l'attrito percettivo (contrasto, densità, dimensione dei tap target). Le conclusioni riguardano
la *struttura* dell'interazione, non la sua estetica.

**Perimetro.** Onboarding e navigazione · cataloghi (razze, classi, incantesimi, mostri) · creazione
personaggio · scheda in uso e modifica · combattimento · note. Fuori perimetro: login/OAuth,
PWA/aggiornamenti, sicurezza (coperti altrove).

**Utente di riferimento.** Gruppo misto, **con giocatori novizi**. Questo è il metro: l'attrito non è
solo il tap in più, è anche la domanda a cui il giocatore non sa rispondere. Un campo che un esperto
compila in due secondi può essere un muro per chi non sa cosa gli si stia chiedendo.

**Bersaglio di regole: D&D 5e (2024)**, confermato. È il manuale disponibile
(`docs/D&D 5e - Players Handbook 2024.pdf`) ed è la versione a cui il gruppo gioca. Il PDF è una
**copia locale deliberatamente fuori dal repository** (`.gitignore`: `docs/*.pdf`): è materiale
protetto e il repo è pubblico.

**Convenzione terminologica.** In questo documento **"razza"** indica l'entità così com'è oggi
nell'app (tabella `races`, pagina Razze, campo del wizard); **"specie"** indica la nomenclatura 2024
verso cui il modello dovrebbe migrare. Dove i due termini compaiono vicini non sono sinonimi
distratti: segnano il prima e il dopo della migrazione discussa in A1.

---

## 1. I cinque attriti strutturali

Sono le *cause*. Quasi tutti i sintomi osservabili nei singoli flussi (§2) discendono da questi.

### A1 — Il modello dati implementa le regole 2014, il bersaglio è il 2024

Non è una scelta esplicita ma un **ibrido stratificato**:

| Elemento | Dove | Edizione |
|---|---|---|
| `Race.StrBonus … ChaBonus` | `Models/Race.cs:21-37` | 2014 (bonus sulla specie) |
| `RaceBonuses()` / `FinalAbilityScores()` | `Services/CharacterWizardLogic.cs:14-32` | 2014 |
| `Character.SpeciesTraits` | `Models/Character.cs:252-253` | 2024 (nomenclatura "specie") |
| `Character.HeroicInspiration` | `Models/Character.cs:114-115` | 2024 (Ispirazione Eroica) |
| `Character.Background` | `Models/Character.cs:69-70` | testo libero, nessuna tabella |

Nel PHB 2024 (pag. 177) i punteggi di caratteristica arrivano dal **background**, non dalla specie:

> *"A background lists three of your character's ability scores. Increase one by 2 and another one by 1,
> or increase all three by 1. None of these increases can raise a score above 20."*

E il background porta con sé molto altro: 2 competenze in abilità, una competenza in strumenti, un
**talento origine** e l'equipaggiamento iniziale. Nell'app il background è una stringa libera
(`Shared/CharacterTabs/CharacterWizard.razor:96-100`, placeholder *"es. Soldato, Criminale…"*): non
porta nulla, non è collegato a niente.

**Conseguenza operativa, ed è la più importante del documento:** i dati 2024 **non sono importabili
nello schema attuale**. I bonus di caratteristica non hanno una casa. Qualunque intervento sui
cataloghi o sul wizard va deciso *dopo* questa scelta di modello, o va rifatto due volte.

**Nota sul clamp.** `FinalAbilityScores` limita a `1..30` (`CharacterWizardLogic.cs:29`), mentre la
regola 2024 sui bonus da background è *"None of these increases can raise a score above 20"* — cioè
in creazione il tetto è 20. Il clamp attuale a 30 è corretto come limite assoluto di gioco, ma non
applica il tetto di creazione: oggi non ha effetto pratico (i bonus sono sulla specie e piccoli), lo
avrà se i bonus diventano scelte guidate dal background.

### A2 — Costo di avvio: ~670 campi da digitare prima di poter giocare

I cataloghi nascono **vuoti** e si popolano solo a mano. Campi contati sui form:

| Catalogo | Campi per voce | Riferimento |
|---|---|---|
| Classi | 9 | `Pages/Classes.razor:36-79` |
| Razze | 11 (di cui 6 bonus) | `Pages/Races.razor:36-91` |
| Incantesimi | 9 | `Pages/Spells.razor:36-79` |
| Mostri | 16 (di cui 6 caratteristiche) | `Pages/Monsters.razor:36-113` |

Per una campagna minimamente utilizzabile — 12 classi, 10 specie, ~50 incantesimi di base —
servono **≈ 670 campi** compilati copiando dal manuale, prima che l'app diventi utile.

Il caso peggiore è il tab Magic. Un mago di livello 1 conosce 3 trucchetti + 6 incantesimi: sono
**81 campi** solo per lui. Il codice stesso lo confessa
(`Shared/CharacterTabs/CharacterMagicTab.razor:84`):

> *"Suggerimento: il catalogo globale è vuoto. Aggiungete incantesimi dalla sezione Incantesimi della home."*

Due moltiplicatori peggiorano il conto:

1. **I cataloghi sono per campagna.** Ogni repository filtra rigidamente
   (`Services/Repositories/RaceRepository.cs:23-26`, e identicamente per classi/incantesimi/mostri).
   Una seconda campagna riparte da zero.
2. **Ogni membro può creare.** Gli empty state lo incoraggiano esplicitamente — *"Ogni giocatore può
   aggiungere le classi usando il bottone +"* (`Pages/Classes.razor:213-217`) — e non esiste alcuna
   deduplicazione: due giocatori che aggiungono *Palla di Fuoco* producono due righe.

### A3 — Il wizard chiede 70 controlli, ~50 sarebbero derivabili dalle regole

Conteggio sul markup di `Shared/CharacterTabs/CharacterWizard.razor`:

| Step | Controlli | Righe | Derivabile da classe/specie/background + livello |
|---|---:|---|---|
| 1. Identità | 7 | `29-116` | Background: oggi testo libero, in 2024 è una scelta con effetti |
| 2. Caratteristiche | 6 | `133-148` | No: i punteggi *base* restano scelta del giocatore (array/point buy/tiri). Dal background arrivano solo i *bonus* |
| 3. Vitalità & combattimento | 5 | `154-206` | 4 su 5: PF, dadi vita, taglia, velocità. Resta la CA (dipende dall'armatura) |
| 4. Competenze | **42** | `226-271` | 6 tiri salvezza al 100%; le 36 checkbox skill diventano poche scelte guidate (il numero varia: Mago 2 fra 7, Guerriero 2 fra 9, Ladro 4 fra 10) |
| 5. Incantesimi | 10 | `279-306` | **10 su 10**: caratteristica e slot sono tabella fissa per classe e livello |
| 6. Riepilogo | 0 | `313-347` | — |

Il caso più netto sono i **9 campi degli slot incantesimo** (`CharacterWizard.razor:297-306`), con
l'istruzione *"Imposta gli slot massimi secondo la tabella della tua classe"*: l'app chiede all'utente
di trascrivere una tabella che è identica per tutti e nota a priori.

Il wizard **già oggi** deriva quello che può, ma può poco: bonus di specie applicati in automatico,
PF e dadi vita suggeriti con un tap, tiri salvezza suggeriti. Il limite non è il wizard — è che
`CharacterClass.Features`, `SkillChoices`, `SavingThrows` e `Race.Traits` sono **stringhe libere**
(`Models/CharacterClass.cs`, `Models/Race.cs`), quindi non c'è quasi nulla da cui derivare.

Un sintomo di questa fragilità: `ParseSaveProficiencies` (`Services/CharacterWizardLogic.cs:66-75`)
riconosce **solo i nomi italiani** (`"forza"`, `"destrezza"`, …) e scarta in silenzio ciò che non
riconosce. Un catalogo popolato in inglese fa sparire il suggerimento dei tiri salvezza **senza alcun
errore visibile**. Vale come vincolo per qualunque import: o i dati sono in italiano, o questo helper
va esteso.

### A4 — L'app chiede risposte al novizio, non gliele insegna

- **Il vicolo cieco del catalogo vuoto.** Alla prima creazione, il select Classe contiene
  `-- Seleziona --` e **"Altro (testo libero)"** e nient'altro
  (`CharacterWizard.razor:36-43`, identico per la razza a `69-76`). Nessun messaggio spiega che il
  catalogo va popolato, nessuna azione porta a farlo dal wizard. Il giocatore o scrive a mano il nome
  della classe — perdendo ogni automazione, perché `SelectedClass` resta `null` e con esso PF, dadi
  vita e tiri salvezza suggeriti — oppure deve intuire di dover uscire, andare in Classi, compilare 9
  campi copiando dal manuale, e ricominciare.
- **Nessuna spiegazione dei concetti.** I testi d'aiuto presenti sono note per chi già sa:
  *"L'Expertise senza Competenza non ha effetto: i calcoli lo ignorano"* (`CharacterWizard.razor:271`).
  Non esiste una riga che dica cosa sia un tiro salvezza, il bonus di competenza, la CD incantesimo,
  la sintonia. Il manuale in `docs/` contiene esattamente queste definizioni ed è inutilizzato.
- **Ordine che presuppone competenza.** Lo step 4 mostra 42 controlli tutti allo stesso peso: il
  novizio non ha modo di sapere che dei sei tiri salvezza ne sceglie **zero** (li dà la classe) e che
  delle diciotto abilità ne sceglie poche — **due gliele dà il background**, le altre le sceglie la
  classe in numero variabile (2 per la maggior parte, 3 per Bardo e Ranger, 4 per il Ladro).

### A5 — Due incoerenze che colpiscono soprattutto chi non conosce le regole

**Unità di misura della velocità.** La razza la memorizza in piedi (`Models/Race.cs:18-19`, default
`30`; il vincolo `0..120` non è nel modello ma nella validazione client,
`Services/FormValidation.cs:30` e `Pages/Races.razor:48`), il personaggio in metri
(`Models/Character.cs:84-85`, default `9`). Il
wizard le mostra affiancate: il riepilogo della specie stampa *"Velocità: 30"*
(`CharacterWizard.razor:86`) accanto a un campo che si aspetta `9`, con l'avvertenza
*"La velocità della razza è solo informativa"* (`CharacterWizard.razor:176`). Chi conosce le regole
traduce a mente; chi non le conosce vede due numeri diversi per la stessa cosa. È un gotcha già noto
e mai risolto.

**Due modelli di salvataggio nella stessa schermata.** Nel form di modifica il salvataggio è
esplicito e dà conferma: `Toasts.ShowSuccess("Personaggio salvato")` (`Pages/Characters.razor:423`).
Nei tab della scheda ogni click persiste subito e **in silenzio**: `SaveCharacterAsync`
(`Pages/Characters.razor:461-478`) non emette alcun toast — segnala solo gli errori. Un PF tolto, uno
slot speso, l'ispirazione attivata: nessun riscontro. Il giocatore non sa se il dato è stato scritto.

---

## 2. Mappa per flusso

Per ogni flusso: cosa succede oggi, dove attrita, in che direzione si potrebbe intervenire.

### 2.1 Onboarding e navigazione

**Oggi.** Login Google → Home. Senza campagne, empty state con *"Crea una nuova campagna o unisciti a
una esistente"* (`Pages/Home.razor:46-49`). Creata la campagna appaiono 7 card di navigazione
(`Pages/Home.razor:156-164` + Combattimento a `137-141`).

**Attriti.**

1. **Nessuna barra di navigazione.** `Layout/MainLayout.razor` non ospita alcuna navigazione: dentro
   l'`ErrorBoundary` c'è solo `@Body`, accanto ai soli componenti globali (`AuthRedirect`,
   `UpdateBanner`, `ToastHost`, `ConfirmDialog`). Nessun menu, nessuna tab bar.
   Ogni pagina ha il proprio `← Home` (`Pages/Races.razor:16-18` e gemelli). Passare
   da Personaggi a Incantesimi costa due navigazioni **e due ricaricamenti dati completi**: ogni
   pagina rifà `OnInitializedAsync`, e quattro delle sei richiamano anche `GetProfilesAsync()` a ogni
   ingresso (`Pages/Races.razor:246`, `Pages/Classes.razor:261`, `Pages/Spells.razor:270`,
   `Pages/Notes.razor:194`). Non esiste cache: è a backlog in `DA-FARE.md` §5, voce rialzata a 🟡
   proprio per questo motivo.
2. **La campagna appena creata è un guscio vuoto.** Nessuna delle 7 card avverte che dietro non c'è
   nulla. Il percorso naturale — Personaggi → crea — porta dritto nel vicolo cieco di A4.
3. **Il codice invito è mostrato solo al master** (`Pages/Home.razor:65-72`), correttamente, ma non è
   copiabile con un tap: è un `<code>` da selezionare a mano.

**Direzione.** Un percorso di primo avvio che, alla creazione della campagna, proponga di popolare i
cataloghi (→ A2) invece di lasciare l'utente davanti a sette porte che danno tutte su stanze vuote.
La barra di navigazione è un intervento indipendente e molto più economico.

### 2.2 Cataloghi (razze, classi, incantesimi, mostri)

**Oggi.** Quattro pagine con struttura identica: ricerca con debounce 300 ms, card espandibili,
FAB `+`, form a campi liberi, permessi via `AccessControl.CanEdit` allineati alle RLS. Gli
incantesimi hanno in più i filtri per livello e classe (`Pages/Spells.razor:98-120`).

**Attriti.**

1. **Sono la sorgente di A2**: tutto a mano, per campagna, senza deduplicazione.
2. **Campi liberi dove servirebbe struttura.** `SavingThrows` è una stringa che un helper prova a
   riparsare (A3); `SkillChoices` è una textarea con esempio *"Scegli 2 tra Acrobazia, Atletica…"*
   (`Pages/Classes.razor:73`) — leggibile da un umano, inservibile per il wizard; `Features` è una
   textarea da 8 righe che dovrebbe contenere tutti i privilegi di classe di 20 livelli.
3. **Nessuna progressione per livello.** Non esiste alcuna struttura che leghi classe e livello a
   slot, privilegi o dadi vita. È la ragione per cui i 9 campi slot restano manuali.
4. **Il filtro classe degli incantesimi cerca stringhe inglesi** (`"Bard"`, `"Cleric"`, …,
   `Pages/Spells.razor:245-255`) dentro un campo `Classes` compilato liberamente. Il match è per
   sottostringa (`Pages/Spells.razor:314-315`), quindi il comportamento è **incoerente fra i chip**:
   scrivendo in italiano, *Bardo* funziona (contiene `Bard`) e così Druido, Paladino, Ranger,
   Warlock; ma *Mago*, *Chierico* e *Stregone* non vengono mai trovati dai rispettivi chip, che
   cercano `Wizard`, `Cleric` e `Sorcerer`. Senza alcun errore visibile.

**Direzione.** È qui che il manuale paga: cataloghi precaricati e strutturati. Vedi §3 per cosa è
realmente estraibile e a quali condizioni.

### 2.3 Creazione del personaggio (wizard)

**Oggi.** Sei step (`CharacterWizard.razor:382-383`), navigabili avanti/indietro e per numero.
Automazioni presenti: bonus specie applicati alla selezione, PF suggeriti, dadi vita precompilati,
tiri salvezza suggeriti — tutte con conferma esplicita dell'utente, mai forzate.

**Attriti.** È il concentrato di A3 e A4. Inoltre:

1. **Nessuna validazione fino alla fine.** Si può percorrere tutto il wizard con il nome vuoto: il
   controllo scatta solo al salvataggio (`Pages/Characters.razor:380-384`). Il campo è marcato `*`
   ma nulla blocca l'avanzamento (`CharacterWizard.razor:433`: `Next()` non valida). E il fallimento
   è peggiore di quanto sembri: appare un toast di errore, ma il wizard **resta sul Riepilogo** —
   `currentStep` si azzera solo quando cambia il riferimento di `Draft`
   (`CharacterWizard.razor:392-397`), che qui non cambia. L'utente deve risalire a mano fino allo
   step 1 senza che nulla gli indichi dove.
2. **Il draft vive solo in memoria.** `editDraft` è un campo del componente
   (`Pages/Characters.razor:183`): un refresh o una navigazione accidentale perde tutto il lavoro.
   Nessun salvataggio parziale, nessun ripristino.
3. **Lo step Incantesimi compare a tutti.** Anche per un barbaro: il giocatore deve sapere che
   *"Nessuna (non incantatore)"* è la risposta giusta per lui (`CharacterWizard.razor:280`). La classe
   scelta al passo 1 sa già la risposta.
4. **Sottoclasse chiesta sempre.** In 5e 2024 la sottoclasse si sceglie al livello 3 per **tutte** le
   classi, ma il campo è presente e vuoto anche per un personaggio di livello 1
   (`CharacterWizard.razor:61-65`).

**Direzione.** Derivare invece di chiedere, e chiedere solo dove c'è davvero una scelta — con accanto
la spiegazione di cosa si sta scegliendo. Dipende interamente da A1 (modello) e A2 (dati).

### 2.4 Scheda in uso e modifica

**Oggi.** Vista a 5 tab (Combat/Stats/Bio/Items/Magic, `Pages/Characters.razor:91-115`); il tab Magic
compare solo se `SpellcastingAbility` è valorizzata. La modifica apre un accordion a 7 sezioni
(`Shared/CharacterTabs/CharacterEditForm.razor`, 718 righe).

**Attriti.**

1. **Nessun level-up.** Salire di livello significa aprire il form e aggiornare a mano livello, PF
   massimi, dadi vita, slot incantesimo (9 campi), competenze nuove. Nulla lo assiste, e il wizard —
   che almeno saprebbe suggerire — è **di sola creazione** (`Pages/Characters.razor:352` usa
   `ViewMode.Wizard` solo in `OpenCreateForm`; `OpenEditForm` va sempre a `ViewMode.Form`).
   È il punto in cui l'attrito si ripresenta a ogni sessione di gioco, non solo all'inizio.
2. **Il salvataggio silenzioso** dei tab (A5).
3. **Nel form di modifica scompaiono i suggerimenti.** `CharacterEditForm` mostra le info di classe e
   specie (`righe 44-53`, `79-85`) ma non offre né PF suggeriti né tiri salvezza né precompilazione
   dadi vita: quelle vivono solo nel wizard. Chi modifica è **meno** assistito di chi crea.
4. **Duplicazione tra i due componenti.** Le 36 checkbox delle abilità, i 9 slot, il blocco identità
   esistono in due copie quasi identiche (`CharacterWizard.razor:244-271` e
   `CharacterEditForm.razor:263-361`). Ogni miglioria va applicata due volte, o divergono.
5. **Difese e tratti come testo libero.** `DamageResistances` e simili sono textarea con separatore a
   virgola (`CharacterEditForm.razor:377-397`), riparsate per la visualizzazione a badge
   (`CharacterCombatTab.razor:178-186`). Funziona finché si scrive con la punteggiatura giusta.

**Direzione.** Un motore di derivazione unico, condiviso da creazione, modifica e level-up: è la
risposta naturale sia al punto 1 sia al punto 4.

### 2.5 Combattimento

**Oggi.** Master: form di aggiunta, import personaggi, import mostri con stepper quantità, ordinamento
per iniziativa, avanzamento turni. Giocatore: vista ridotta con la sola scheda propria + i nomi degli
altri, aggiornata via polling a 4 secondi (`Pages/Combat.razor:339`).

**Attriti.**

1. **Le iniziative si inseriscono a mano, una per una.** L'import personaggi assegna
   `Initiative = 0` a tutti (`Pages/Combat.razor:401-412`), l'import mostri anche
   (`Services/CombatImport.cs`). Con 4 PG e 5 goblin il master compila 9 caselle prima di iniziare.
   L'app conosce già il bonus di iniziativa di ogni PG
   (`CharacterCalculations.GetInitiative`) e non lo usa: né per tirare, né per precompilare.
2. **Il form "Aggiungi combattente" occupa la cima della pagina** anche a combattimento in corso
   (`Pages/Combat.razor:53-78`), spingendo in basso ciò che serve davvero durante il turno.
3. **I PF si regolano di ±1 per click** (`Pages/Combat.razor:167-176`). Un colpo da 14 danni sono
   quattordici tap.
4. **Import personaggi ripetibile senza controllo**: premuto due volte, duplica tutti i combattenti.
   Nessun controllo di presenza.

**Direzione.** Il combat è l'unico flusso dove l'attrito è tutto *durante* la sessione: qui il valore
sta nei tap risparmiati al master mentre gli altri aspettano.

### 2.6 Note

**Oggi.** Lista con ricerca, form titolo + contenuto, toggle privata/condivisa, sola lettura sulle
note altrui. Ordinamento per data di aggiornamento.

**Attriti.** Il flusso più pulito dei sei. Due osservazioni minori: il contenuto è plain text (il
markdown è a backlog come idea, `DA-FARE.md` §9) e non c'è collegamento tra note e il resto — una nota
non si può agganciare a un PG, a un mostro o a una sessione.

**Direzione.** Nessun intervento prioritario. Va segnalato perché conferma per contrasto la tesi del
documento: **le Note non attritano perché non chiedono dati di dominio.** L'attrito non nasce dalla
UI, nasce dal dover trascrivere a mano un manuale.

---

## 3. Il manuale come fonte dati: cosa è realmente utilizzabile

Verifiche eseguite su `docs/D&D 5e - Players Handbook 2024.pdf` (~85 MB).

**Il testo è estraibile.** Di poppler è disponibile solo `pdftotext`: manca `pdftoppm`, il binario di
rendering che il tool `Read` usa per aprire i PDF (e manca anche `pdfinfo`). L'estrazione testuale
funziona.

**L'OCR è sporco.** Campioni reali dall'indice: `Barbarian … SO` per `50`, `College of Da nee` per
*Dance*, `T o o l s ... 2 2 0`, `Ability Scores: Dexterity, Constitution , Charisma`. Spaziature
irregolari dentro le parole, cifre scambiate con lettere, virgole staccate. **Un import automatico
cieco produrrebbe dati corrotti**: serve normalizzazione più una validazione a campione.

**È in inglese, l'app è in italiano.** Non è un dettaglio cosmetico: come mostrato in A3,
`ParseSaveProficiencies` riconosce solo i nomi italiani e fallisce in silenzio, e il filtro classe
degli incantesimi cerca stringhe inglesi su un campo compilato in italiano (§2.2). Le due convenzioni
già oggi convivono male; un import va deciso su **una** lingua e le due funzioni vanno allineate.

**Il perimetro legale va scelto consapevolmente.** Il PHB 2024 è materiale protetto. L'**SRD 5.2**
copre sotto licenza aperta le classi base, le specie e buona parte degli incantesimi — cioè
quasi tutto ciò che serve a chiudere A2. La distinzione conta soprattutto se i dati precaricati
finiscono **versionati nel repository** e pubblicati su GitHub Pages, che è pubblico. Non blocca
nulla oggi; è una decisione da prendere prima di scrivere il seed, non dopo.

---

## 4. Priorità

Ordinate per rapporto tra sollievo prodotto e costo, tenendo conto delle dipendenze reali.

| # | Intervento | Attrito | Costo | Note |
|---|---|---|---|---|
| 1 | **Decidere il modello 2024** (background come entità con bonus, specie senza) | A1 | medio | **Blocca 2, 3, 4.** Tocca schema DB, RLS, `CharacterWizardLogic`, wizard e form |
| 2 | **Cataloghi precaricati e strutturati** | A2 | alto | Il grosso del sollievo. Richiede 1; richiede di sciogliere lingua e perimetro legale (§3). **Riapre due voci del backlog** — vedi sotto |
| 3 | **Motore di derivazione condiviso** (slot, PF, competenze, taglia, velocità) | A3 | medio | Richiede 2 per avere dati da cui derivare. Serve creazione, modifica e level-up insieme |
| 4 | **Level-up guidato** | §2.4 | medio | Riuso quasi totale di 3. È l'attrito che si ripresenta a ogni sessione |
| 5 | **Aiuto contestuale dal manuale** | A4 | basso-medio | Indipendente dai precedenti: si può fare anche solo sui concetti chiave |
| 6 | **Barra di navigazione + cache dei cataloghi** | §2.1 | basso | Del tutto indipendente. Il guadagno più immediato per costo |
| 7 | **Iniziativa precompilata/tirata + PF a passo variabile** | §2.5 | basso | Indipendente. Valore concentrato sul master, durante la sessione |
| 8 | **Unificare velocità in una sola unità** | A5 | basso | ~~Migrazione dati sulla tabella `races`~~ → superato dal [design del 2026-07-25](./2026-07-25-modello-2024-import-dati-design.md) §4.5: colonna additiva `speed_unit`, nessuna migrazione di dati |
| 9 | **Conferma visibile sui salvataggi impliciti** | A5 | molto basso | Poche righe; toglie un dubbio ricorrente |

I punti **6, 7, 8, 9 non dipendono da nulla**: sono aggredibili subito e in qualsiasi ordine, anche
mentre si decide sui primi cinque.

**Due voci del backlog che l'intervento #2 rimette in gioco.**

- **Virtualizzazione delle liste** (`DA-FARE.md` §5): scartata il 2026-06-24 con la motivazione
  esplicita che «i cataloghi restano sotto le ~50 voci», e con la riserva «da rivalutare solo se i
  cataloghi crescono (es. import massivo)». Precaricare l'SRD è *esattamente* quel trigger: le
  centinaia di incantesimi invalidano il presupposto della decisione. Va rivalutata **insieme** al
  precaricamento, non dopo averlo scoperto sul campo.
- **Aiuto AI alla compilazione** (`DA-FARE.md` §8): la voce esiste già, con entitlement server-side,
  caveat copyright sul manuale e la nota che «per la generazione base il modello conosce già lo SRD
  5e». Copre in parte lo stesso bisogno delle priorità #2 e #5. Vanno messe in relazione
  esplicitamente: **precaricare i dati riduce molto ciò che resterebbe all'AI da generare**, e
  l'ordine fra le due cambia il valore di entrambe. Non sono filoni indipendenti.

---

## 5. Decisioni aperte

Nessuna di queste è risolvibile leggendo il codice: richiedono una scelta di prodotto.

> **Aggiornamento 2026-07-25:** le prime quattro sono state decise nel
> [design del modello 2024 + import dati](./2026-07-25-modello-2024-import-dati-design.md) §2, che le
> raccoglie in tabella. Resta aperta la quinta (wizard e form di modifica), rinviata allo spec del motore
> di derivazione. Il testo qui sotto resta come traccia di ciò che era in gioco.

1. **Dove vivono i cataloghi ufficiali?** Copia per campagna alla creazione (nessuna modifica alle
   RLS, il master può fare homebrew sulle voci ufficiali, dati duplicati) *oppure* catalogo di sistema
   condiviso (nessuna duplicazione, ma tocca le RLS chiuse a giugno e serve una regola per l'homebrew).
2. **In che lingua sono i dati precaricati?** Italiano (coerente con l'intera UI, richiede traduzione
   dei contenuti) o inglese (fedele alla fonte, ma incoerente con la UI e con
   `ParseSaveProficiencies`).
3. **Quale perimetro di contenuti?** Solo SRD 5.2 — sicuro da versionare in un repository pubblico —
   oppure l'intero manuale, che resterebbe adeguato solo a un uso privato del gruppo.
4. **Cosa fare dei personaggi esistenti** quando i bonus si spostano dalla specie al background: una
   migrazione, o si accetta che le schede già create restino a regole 2014?
5. **Il wizard sostituisce il form di modifica** o restano due strade separate? Oggi divergono e
   duplicano markup (§2.4).

---

## 6. Cosa non risulta essere un problema

Per onestà dell'analisi, e per non spendere lavoro dove non serve:

- **Le Note** funzionano bene così (§2.6).
- **La visibilità del giocatore nel combat** è curata: redazione via helper puro testato, il
  giocatore non vede né statistiche altrui né l'ordine di turno.
- **I permessi** sono coerenti fra client e server: `AccessControl.CanEdit` è speculare alle RLS, il
  caso degenere `null == null` è stato chiuso a luglio.
- **L'infrastruttura UI è a posto**: toast, dialog di conferma, spinner, banner d'errore con
  riparazione della cache, design token. Il problema di questo documento **non è il vestito, è ciò
  che l'app chiede all'utente di sapere e di scrivere.**

---

## 7. Riferimenti

- Backlog: [`docs/DA-FARE.md`](../../DA-FARE.md) — **§8-bis (gli interventi di questo documento, raccolti
  in otto voci: il design del 2026-07-25 ha unito i punti 1 e 2)**, §6 (UI/UX), §8 (funzionalità emerse
  dall'uso)
- Seguito: [design modello 2024 + import dati](./2026-07-25-modello-2024-import-dati-design.md)
- Storico: [`docs/DIARIO.md`](../../DIARIO.md) — wizard PG (2026-06-25)
- Spec del wizard esistente: [`2026-06-24-wizard-scheda-pg-design.md`](./2026-06-24-wizard-scheda-pg-design.md)
- Voce AI già a backlog: [`docs/DA-FARE.md`](../../DA-FARE.md) §8 — «Aiuto AI alla compilazione»
- Manuale: `docs/D&D 5e - Players Handbook 2024.pdf` (pag. 177 per le origini) — **copia locale non
  versionata**, esclusa via `.gitignore` (`docs/*.pdf`): materiale protetto, repository pubblico
