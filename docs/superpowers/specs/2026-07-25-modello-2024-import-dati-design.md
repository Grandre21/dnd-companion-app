# Modello D&D 5e 2024 + import dei dati — design

> Data: **2026-07-25** · Stato: **design approvato** · **Fasi 1 e 2 implementate** (2026-07-25 / 2026-07-29),
> Fase 3 aperta — piani:
> [fase 1](../plans/2026-07-25-modello-2024-import-dati-fase-1.md) ·
> [fase 2](../plans/2026-07-27-modello-2024-import-dati-fase-2.md)
> Origine: [mappa UX dei flussi](./2026-07-25-ux-mappa-flussi-analisi.md) §4 (attriti **A1** e **A2**)
> Backlog: [`DA-FARE.md`](../../DA-FARE.md) §8-bis

## 1. Il problema

Due fatti misurati nell'analisi UX del 2026-07-25:

- **A1** — l'app implementa le regole **2014** (i bonus di caratteristica stanno sulla specie) mentre il
  gruppo gioca a **2024**, dove quei bonus vengono dal **background** — che nell'app è un campo di testo
  libero senza tabella dietro. I dati 2024 non sono importabili nello schema attuale.
- **A2** — una campagna minima richiede **~670 campi digitati a mano** prima di poter giocare. I cataloghi
  hanno tutti `campaign_id`: sono **per campagna**, quindi ogni gruppo che si iscrive riparte da zero.

Con l'app pubblicata il secondo diventa il primo muro contro cui sbatte chiunque si registri.

## 2. Decisioni prese (2026-07-25, con l'utente)

| # | Decisione | Scelta |
|---|---|---|
| 1 | Come si popolano i cataloghi | **File di dati** importabile/esportabile. Niente estrazione PDF dentro l'app |
| 2 | Cosa distribuisce l'app pubblicata | **Solo SRD 5.2** (CC BY 4.0). Il contenuto non-SRD resta un file privato del nostro gruppo |
| 3 | Personaggi e cataloghi esistenti | **Si congelano**. Nessun ricalcolo, nessuna migrazione di dati |
| 4 | Contenuto del primo pacchetto | **Tutto il SRD, testo integrale** in italiano |
| 5 | Dove vivono i dati | **Pacchetto come file dell'app** (sola lettura, unito lato client) · **import utente nel database** |

La decisione 2 risolve il problema di copyright senza rinunce: noi non ridistribuiamo nulla di protetto, e
chi vuole più contenuti carica il proprio file. Lo stesso meccanismo serve entrambi i casi.

La decisione 5 è stata scelta contro l'alternativa "cataloghi di sistema nel database" perché non riapre
le RLS chiuse e testate a giugno e non peggiora il caricamento (§5 di `DA-FARE`). **Vale però solo per la
lettura**: gli incantesimi che un PG usa davvero devono atterrare nel database (§4.4).

## 3. Perimetro

**Dentro:** modello 2024 minimo · formato di scambio · import ed export · pacchetto SRD italiano completo ·
wizard che prende i bonus dal background · **adeguamento del filtro per classe degli incantesimi**, oggi
scritto su stringhe inglesi (§4.6).

**Fuori, con spec propri:** motore di derivazione condiviso (creazione + modifica + level-up) · level-up
guidato · aiuto AI alla compilazione · traduzione dell'interfaccia (le stringhe UI sono già italiane).

## 4. Modello dati

### 4.1 Cosa non cambia, e cosa invece va toccato

Nessuna migrazione **di dati**, come da decisione 3. Le migrazioni **di schema** sono tutte additive:

| Intervento | Tabella | Motivo |
|---|---|---|
| Tabella nuova | `backgrounds` | nel 2024 porta i punteggi: non può restare testo libero (§4.2) |
| Colonna `source_id text` | `races`, `classes`, `spells`, `monsters`, `backgrounds` | riconoscere la voce di pacchetto dopo l'import (§4.3, §4.4) |
| Colonna `speed_unit text default 'ft'` | `races` | l'unità non è deducibile dalla sorgente (§4.5) |
| Colonna `background_ability_choice text` | `characters` | la ripartizione dei bonus scelta dal giocatore (§4.7) |
| Vincolo `UNIQUE (campaign_id, source_id)` | `races`, `classes`, `spells`, `monsters` | una sola riga per provenienza in una campagna (§4.3) |

In totale: **1 tabella nuova + 6 colonne additive su 5 tabelle esistenti + 4 vincoli `UNIQUE` additivi**
(`source_id` su `races`, `classes`, `spells`, `monsters`; `speed_unit` su `races`;
`background_ability_choice` su `characters`) — formulazione da usare identica in `DA-FARE` e nel `DIARIO`.
`backgrounds` ha lo stesso vincolo, ma nella propria `CREATE TABLE`: non è una migrazione e non entra nel
conteggio (§4.2).

I vincoli non toccano le righe esistenti: in PostgreSQL più `NULL` non violano un `UNIQUE`, e `source_id`
è nullo su tutto ciò che è stato digitato a mano.

Nessuna colonna viene rimossa e nessuna policy esistente viene modificata. I bonus di caratteristica su
`races` (`str_bonus` … `cha_bonus`) restano: servono alle voci già digitate, che la decisione 3 congela.

Le colonne nuove vanno anche sui Model annotati, altrimenti `From<T>` non le vede: `SourceId` su `Race`,
`CharacterClass`, `Spell`, `Monster`; `SpeedUnit` su `Race`; `BackgroundAbilityChoice` su `Character`.

**Riferimenti dei personaggi — verificato sullo schema, e non è uniforme:**

- `characters.race` e `characters.class` sono **testo, non chiavi esterne**: un PG referenzia la specie per
  nome. Per questi due, i dati del pacchetto possono vivere fuori dal database senza conseguenze.
- `characters.background` e `characters.subclass` **esistono già** come colonne di testo. Il 2024 non
  richiede di aggiungerle, solo di dare loro un significato.
- **`character_spells.spell_id` è invece una chiave esterna reale** verso `spells(id)`
  (`character_spells_spell_id_fkey`, `ON DELETE CASCADE`), e `CharacterMagicTab` la usa inserendo
  `SpellId = spell.Id`. Un incantesimo che vive solo nel file **non può essere aggiunto alla lista di un
  PG**: l'insert violerebbe il vincolo. È il caso trattato in §4.4.

### 4.2 La tabella nuova: `backgrounds`

| Colonna | Tipo | Note |
|---|---|---|
| `id` | uuid | come gli altri cataloghi |
| `name` | text NOT NULL | |
| `description` | text | |
| `ability_scores` | text | **le tre caratteristiche** su cui il background concede i bonus |
| `origin_feat` | text | talento d'origine |
| `skill_proficiencies` | text | due abilità |
| `tool_proficiency` | text | |
| `equipment` | text | |
| `source_id` | text | id di provenienza se importata (§4.3) |
| `added_by` | uuid | |
| `campaign_id` | uuid NOT NULL | |
| `created_at` | timestamptz | |

La tabella nasce con lo stesso `UNIQUE (campaign_id, source_id)` degli altri cataloghi (§4.3): è anch'essa
destinataria degli import, e dentro un `CREATE TABLE` il vincolo non costa nulla. Per questo il conteggio di
§4.1 parla di **quattro** vincoli additivi — il quinto è qui, e non è una migrazione.

La colonna elenca **solo le tre caratteristiche**, non la ripartizione: nel 2024 scegliere fra +2/+1 e
+1/+1/+1 è una decisione del *giocatore* per quel personaggio, non una proprietà del background. Due PG
con lo stesso background devono poter scegliere diversamente (§4.7).

Le policy RLS sono **ricalcate su quelle di `races`**: lettura ai membri della campagna, inserimento a
qualunque membro con `added_by = auth.uid()`, modifica e cancellazione al proprietario della riga o al
master. È una tabella nuova, quindi nessuna policy esistente viene toccata — ma le sue policy vanno
scritte e testate.

Segue il pattern dei dati: `Models/Background.cs` con `[Table("backgrounds")]`, interfaccia
`IBackgroundRepository` e implementazione in `Services/Repositories/`, registrata `AddSingleton` in
`Program.cs` come gli altri undici repository.

### 4.3 Identificatori di provenienza

Ogni voce del pacchetto ha un identificatore stabile (`srd-2024-it/elfo`). Perché sopravviva all'import,
serve una colonna dove atterrare: `source_id`, aggiunta a tutti e cinque i cataloghi.

La chiave di confronto, usata sia da `CatalogMerge` sia da `PackageImportPlan`, è: **`source_id` se
presente su entrambi i lati, altrimenti il nome normalizzato** — trim, `ToLowerInvariant`, e una **piega
esplicita degli accenti** (mappa carattere→carattere per l'insieme latino: à→a, è→e, …).

La piega va scritta a mano, non con `String.Normalize`: il progetto compila con
`InvariantGlobalization=true` (scelta di bundle, §2 di `DA-FARE`), quindi l'ICU non c'è e le API di
normalizzazione **falliscono in silenzio** — `"À".Normalize(FormD)` non decompone e `IsNormalized` mente,
senza eccezioni né avvisi. Con ~350 nomi italiani accentati (Invisibilità, Velocità, Oscurità) il confronto
non riconoscerebbe le voci e ne creerebbe di duplicate. `ToLowerInvariant` invece funziona anche sugli
accentati.

**Cosa decide la chiave, e cosa no.** Serve a stabilire **quale voce di database oscura quale voce di
pacchetto**. Non serve mai a nascondere righe di database fra loro: quelle sono dati dell'utente e restano
**tutte visibili**, anche quando sono duplicate — oggi due giocatori possono benissimo aver creato due
"Palla di Fuoco", e nasconderne una sarebbe una perdita silenziosa.

Se più righe corrispondono alla stessa voce di pacchetto, quella voce viene oscurata una volta sola e le
righe restano tutte. A rappresentarla vince, nell'ordine:

1. la riga **senza `source_id`** — è una voce propria dell'utente, quindi la più specifica;
2. a parità, quella con l'**`id` ordinalmente minore**: criterio arbitrario, ma è l'unico deterministico
   disponibile su tutti i cataloghi (`spells` e `monsters` non hanno `created_at`, e senza `ORDER BY`
   l'ordine di ritorno di PostgREST non è garantito).

Il "rappresentante" così scelto non serve a nascondere le altre righe — restano tutte — ma a rispondere alle
due domande operative: **quale riga aggiorna un import** (§7) e **quale riusa la materializzazione** (§4.4).
La prima regola non è un dettaglio: senza di essa la copia creata da "duplica e modifica" (§6), che nasce
priva di `source_id`, collide per nome con l'originale e il confronto fra due uuid diventa un sorteggio —
un import successivo aggiornerebbe a caso la copia dell'utente o la voce da cui era stata tratta.

Un vincolo `UNIQUE (campaign_id, source_id)` sui cataloghi impedisce alla radice che due righe della stessa
campagna condividano una provenienza: senza di esso due giocatori che aggiungono lo stesso incantesimo nello
stesso momento (§4.4) creerebbero due righe gemelle.

### 4.4 Incantesimi: materializzazione su uso

La chiave esterna di §4.1 impedisce di referenziare un incantesimo che sta solo nel file. La soluzione non
è precaricare i ~350 incantesimi nel database di ogni campagna — sarebbe la duplicazione che la decisione 5
evita — ma **materializzare solo quelli che servono**:

> quando un PG aggiunge alla propria lista un incantesimo di pacchetto, l'app inserisce quella singola voce
> in `spells` per la campagna, con `source_id` valorizzato, e crea `character_spells` puntando al nuovo
> `uuid`. Se una riga con quel `source_id` esiste già nella campagna, la riusa.

L'inserimento **deve reggere il conflitto sul vincolo `(campaign_id, source_id)`**: non basta cercare la riga
prima di inserirla. La differenza conta: `SpellMaterialization` decide sulla lista che il
client ha in memoria, e quella lista è un'istantanea — `Pages/Characters.razor` carica gli incantesimi una
volta sola e non li ricarica finché la pagina resta aperta. Due giocatori che preparano le schede la stessa
sera basterebbero a far fallire il secondo inserimento contro il vincolo di unicità (§4.3), trasformando in
errore di sistema un'operazione che doveva semplicemente riusare una riga già presente.

> ⚠️ **Corretto in Fase 2 (2026-07-29): non con un `Upsert`.** Questo paragrafo prescriveva un `Upsert` con
> `on_conflict`; la misura sul campo dice che quella strada non esiste con la libreria in uso.
> `postgrest-csharp 3.5.1` serializza la chiave primaria **anche** con `[PrimaryKey("id", false)]`, quindi
> manda `"id":""` e con `id uuid NOT NULL` prende `invalid input syntax for type uuid` — HTTP 400 su *ogni*
> scrittura. (`CombatStateRepository` non lo incontra perché la sua chiave, `campaign_id`, è sempre
> valorizzata.) Valorizzare l'`id` a mano è peggio: su conflitto il `DO UPDATE` riscriverebbe la chiave
> primaria della riga esistente, e `character_spells_spell_id_fkey` non è `ON UPDATE CASCADE`.
> L'implementazione è quindi **leggi-poi-inserisci con rilettura sul conflitto**: si cerca la riga per
> `source_id` (`ISpellRepository.GetOneBySourceAsync`), si inserisce se manca, e se l'`INSERT` urta il
> vincolo si rilegge la riga vincente invece di sovrascriverla — stesso esito che ci si aspettava
> dall'`Upsert`, senza mai toccare dati di un altro giocatore.

Il costo è proporzionale all'uso reale, non al catalogo: un gruppo paga le righe degli incantesimi che i
suoi personaggi conoscono. `CatalogMerge` deduplica per `source_id`, quindi la voce materializzata non
compare due volte.

**Chi la esegue.** La decisione "riusa la riga esistente o creane una da voce di pacchetto" è logica di
dominio e vive in un helper puro `SpellMaterialization.Resolve(packageSpell, campaignSpells)`, non dentro
`CharacterMagicTab.AddSpellToCharacter`, che oggi chiama direttamente il repository. L'inserimento lo fa
`ISpellRepository`, il legame `ICharacterSpellRepository`.

**Una riga materializzata non è una riga di catalogo come le altre.** Nasce con `added_by` di chi l'ha
usata — la policy `spells_insert` lo impone — quindi senza precauzioni diventerebbe modificabile e
cancellabile da quel giocatore. E poiché `character_spells_spell_id_fkey` è `ON DELETE CASCADE`,
cancellarla toglierebbe l'incantesimo dalla lista di **tutti** i personaggi che lo conoscono, senza
preavviso e in un catalogo dove nessuno si aspetta di possedere una voce SRD.

Quindi: **le righe materializzate restano prive dei comandi di modifica e cancellazione**, esattamente come
le voci di pacchetto da cui derivano. Il gate non è `AccessControl.CanEdit` — che direbbe di sì al
proprietario — ma la **provenienza dal pacchetto dell'app** (§6), che lo scavalca. Per modificarle resta
"duplica e modifica", che crea una riga propria senza `source_id`.

La deroga vale **solo** per questa provenienza. Le righe che arrivano da un file dell'utente hanno anch'esse
`source_id` e sono esposte allo stesso `CASCADE`, ma restano modificabili: sono contenuto che il gruppo ha
scelto di caricare, quindi cancellarle è una decisione legittima di chi le possiede — mentre una voce SRD
finita nel database perché *qualcun altro* l'ha usata non è di nessuno, ed è lì che serve la protezione.
Vale comunque il `ConfirmDialog` di rito, che è il posto giusto per avvertire che l'incantesimo sparirà
dalle schede che lo conoscono.

La stessa logica **non serve** per specie, classi, background e mostri: nessuno di questi è referenziato da
una chiave esterna. Il tracker del combattimento copia i mostri per valore (`CombatImport.FromMonster`),
quindi funziona già con una voce di pacchetto.

### 4.5 Velocità: l'unità va scritta, non dedotta

Il pacchetto italiano è in **metri** (il manuale italiano usa 9 m dove l'inglese usa 30 ft); le righe
esistenti sono in **piedi**. Dedurre l'unità dalla sorgente non regge a due casi reali: una voce di
pacchetto duplicata in campagna (§6) diventerebbe una riga letta come piedi, e una razza creata a mano
dopo il cambio verrebbe digitata in metri ma mostrata in piedi.

Quindi l'unità si scrive: colonna `speed_unit` su `races`, `default 'ft'` — le righe esistenti restano
piedi senza toccarle, coerentemente con la decisione 3. Le voci nuove e le duplicazioni da pacchetto
scrivono `'m'`. Il form Razze mostra l'unità accanto al campo e `FormValidation.ValidateRace` valida
l'intervallo corretto per unità (il limite 0–120 attuale è pensato in piedi).

Questo chiude l'incoerenza di §8-bis **senza migrazione di dati**, ma con una migrazione di schema: la
voce del backlog va corretta di conseguenza.

### 4.6 Filtro degli incantesimi per classe

`Pages/Spells.razor` filtra per sottostringhe **inglesi** (`Wizard`, `Cleric`, `Sorcerer`). Con un
catalogo italiano di ~350 voci quei filtri non troverebbero mai nulla, senza errore visibile — e il filtro
è il modo principale di navigare un catalogo che passa da decine a centinaia di voci.

L'adeguamento è nel perimetro: il confronto passa per una **mappatura italiano↔inglese** applicata dal
filtro, così le voci legacy in inglese e quelle di pacchetto in italiano restano entrambe trovabili.

### 4.7 Bonus dal background: ripartizione e tetto

Tre regole. Sono logica di dominio, quindi vivono in `CharacterWizardLogic` come funzioni pure — accanto a
`RaceBonuses` e `FinalAbilityScores`, che già sono l'unica fonte dell'ordine e del calcolo — non nel markup
del wizard:

1. **Ripartizione.** Il giocatore sceglie +2/+1 su due delle tre caratteristiche del background, oppure
   +1/+1/+1 su tutte e tre. La scelta si salva in `characters.background_ability_choice`, altrimenti
   modifica e level-up non potranno ricostruirla (i punteggi sono salvati già sommati).
2. **Tetto.** Nel 2024 questi aumenti non possono portare un punteggio sopra **20**.
   `CharacterWizardLogic.FinalAbilityScores` clampa oggi a 1..30: il tetto va applicato ai bonus di
   background prima del clamp generale, non sostituendolo (30 resta il limite assoluto del modello).
3. **Convivenza con le voci legacy.** Se la specie selezionata ha bonus non nulli — cioè è una voce 2014
   già digitata — il wizard **non** applica anche i bonus del background e lo dice a schermo. Altrimenti si
   sommerebbero due edizioni, producendo punteggi illegali in entrambe. Se la specie ha bonus a zero (voce
   di pacchetto), i bonus vengono dal background.

Il background resta selezionabile anche come **testo libero**, come oggi fanno razza e classe con
"Altro": chi non ha ancora né pacchetto né background propri deve poter creare un personaggio. Un
background a testo libero non concede bonus, e il wizard lo dice invece di lasciarlo intuire.

## 5. Formato di scambio

Un JSON con intestazione e una sezione per tipo. Serve **sia l'import sia l'export**: un gruppo può
portarsi via i propri dati o passarli a un altro tavolo.

```json
{
  "schemaVersion": 1,
  "id": "srd-2024-it",
  "name": "SRD 5.2 — Italiano",
  "edition": "2024",
  "language": "it",
  "version": "1.0.0",
  "license": {
    "name": "CC BY 4.0",
    "attribution": "<testo di attribuzione richiesto dalla licenza>"
  },
  "species": [
    {
      "id": "srd-2024-it/elfo",
      "name": "Elfo",
      "description": "…",
      "size": "Media",
      "speed": { "value": 9, "unit": "m" },
      "traits": "…"
    }
  ],
  "backgrounds": [
    {
      "id": "srd-2024-it/<slug>",
      "name": "…",
      "abilityScores": ["Forza", "Destrezza", "Intelligenza"],
      "originFeat": "…",
      "skillProficiencies": ["…", "…"],
      "toolProficiency": "…",
      "equipment": "…"
    }
  ],
  "feats": [
    { "id": "srd-2024-it/<slug>", "name": "…", "category": "Origine", "description": "…" }
  ],
  "classes": [
    {
      "id": "srd-2024-it/mago",
      "name": "Mago",
      "hitDie": "d6",
      "primaryAbility": "Intelligenza",
      "savingThrows": ["Intelligenza", "Saggezza"],
      "skillChoices": { "count": 2, "from": ["…"] },
      "levels": [
        { "level": 1, "features": ["…"], "spellSlots": [2, 0, 0, 0, 0, 0, 0, 0, 0] }
      ]
    }
  ],
  "spells": [],
  "monsters": []
}
```

Quattro scelte da motivare:

- **`schemaVersion`.** Un pacchetto scritto per una versione futura viene **rifiutato** con un messaggio
  comprensibile, non interpretato a metà.
- **Identificatori stabili** invece di soli nomi: riconoscono la stessa voce fra due versioni del pacchetto
  senza dipendere dall'ortografia. Atterrano in `source_id` (§4.3). La forma è **`<id del pacchetto>/<slug>`**
  — non è cosmetica: il prefisso è ciò che permette a §6 di distinguere una riga proveniente dal pacchetto
  dell'app da una proveniente da un file dell'utente, senza dover avere il pacchetto caricato. Gli slug
  dell'esempio sono segnaposto: quelli reali si fissano quando il perimetro SRD è verificato (§13).
- **`levels` con la progressione completa** (privilegi e slot per livello) anche se il motore che li
  consuma arriva in uno spec successivo. Ometterli significherebbe tradurre le classi due volte.
- **`feats`** perché `originFeat` è un rimando: senza il testo del talento, il novizio — che è l'utente di
  riferimento dichiarato — leggerebbe un nome e nient'altro.

`feats` è **di sola consultazione e non importabile**: non ha tabella e non ne avrà una in questo spec. Il
testo si legge nella pagina Background, accanto al talento d'origine che lo richiama. Le conseguenze vanno
dette invece che taciute: `PackageImportPlan` marca la sezione **"non importabile (solo consultazione)"** e
l'anteprima la mostra, così chi carica un proprio file con dei talenti sa che non spariranno in silenzio; e
l'export di una campagna non produce mai `feats`, perché nel database non ce ne sono.

La dicitura va differenziata, perché la conseguenza è diversa: i talenti del **pacchetto dell'app** sono
davvero consultabili nella pagina Background, quelli di un **file dell'utente** non finiscono da nessuna
parte — chiusa l'anteprima non c'è più un posto dove leggerli. Per questi ultimi il piano dice
"non importabile — resta nel tuo file", non "solo consultazione", che lascerebbe intendere il contrario.

`CatalogPackageParser` **accetta** un pacchetto con `language` diverso da `"it"` ma avvisa che le funzioni
che dipendono dalla lingua (il filtro di §4.6) possono degradare. Rifiutarlo escluderebbe l'homebrew
inglese senza motivo.

## 6. Le due sorgenti

Un `ICatalogService` registrato in DI espone i cataloghi come **unione** di pacchetto e database di
campagna. Legge il database **solo attraverso i repository** — mai `From<T>` diretto — e il file del
pacchetto via `HttpClient`.

- **A parità di chiave (§4.3) vince il database.** È così che l'homebrew sovrascrive l'SRD.
- Le voci del pacchetto sono **in sola lettura**: niente matita né cestino, ma un **"duplica e modifica"**
  che ne crea una copia modificabile nella campagna, **senza `source_id`** — è una voce propria
  dell'utente, e infatti da quel momento vince sul pacchetto per nome normalizzato.
- La sola lettura segue la **provenienza**, non il semplice `source_id`. Una riga di database è priva di
  comandi solo se il suo `source_id` viene **dal pacchetto dell'app** — riconoscibile dal prefisso, che è
  l'`id` del pacchetto (`srd-2024-it/…`), una costante nota all'app e verificabile anche offline. È il caso
  delle righe materializzate di §4.4, che vivono nel database ma restano voci di pacchetto.
- **Le righe importate da un file dell'utente sono normali contenuti di campagna**: hanno `source_id`, ma
  restano governate da `AccessControl.CanEdit` come tutto il resto. Legarle alla sola presenza del
  `source_id` renderebbe intoccabile proprio ciò che l'import esiste per portare — il pacchetto privato del
  gruppo, l'homebrew esportato e ripreso a un altro tavolo — e contraddirebbe §7, che quelle stesse righe le
  aggiorna a ogni import successivo.
- Se un utente dà al proprio file lo stesso `id` del pacchetto dell'app, le sue voci finiscono in sola
  lettura. È un autogol, non un buco di sicurezza: l'autorità sui dati resta la RLS, e il rimedio è
  cambiare l'`id` del proprio pacchetto.
- `ICatalogService` espone anche i `feats`, che vengono **solo dal pacchetto dell'app** (lista vuota se non
  è ancora stato caricato): non hanno tabella, quindi non c'è nulla da unire.
- Il pacchetto **va escluso dal precache del service worker**. `offlineAssetsInclude` cattura ogni
  `/\.json$/` e `onInstall` li scarica tutti con un unico `cache.addAll`: lasciandolo nel manifest
  finirebbe addosso a ogni utente all'installazione — l'opposto del caricamento su richiesta — e poiché
  `cache.addAll` è atomico, un solo fetch fallito farebbe fallire l'installazione e l'app perderebbe
  l'offline. Va quindi aggiunto a `offlineAssetsExclude`, o collocato fuori dal manifest degli asset.
- Di conseguenza il pacchetto è **consultabile offline dopo il primo caricamento**, non da subito. La
  dimensione va **misurata** quando il pacchetto esiste; se pesa troppo si divide per tipo (§2 di
  `DA-FARE` è sensibile al peso del cold-load).

## 7. Logica pura testabile

Quattro helper `static`, secondo il pattern di progetto (logica di dominio fuori dai `.razor`):

| Helper | Responsabilità |
|---|---|
| `CatalogPackageParser` | deserializza e valida un pacchetto; gli errori indicano **quale voce** è colpevole |
| `CatalogMerge` | unione delle due sorgenti, chiave di confronto e precedenza (§4.3) |
| `PackageImportPlan` | dato un file, lo stato della campagna **e chi sta importando**, calcola cosa sarà creato, aggiornato, saltato o non importabile |
| `SpellMaterialization` | data una voce di pacchetto e gli incantesimi della campagna, decide se riusare una riga o crearne una (§4.4) |

`PackageImportPlan` riceve `userId`, `isMaster` e l'`added_by` delle righe esistenti, e marca
**"saltato (non modificabile)"** ogni voce che `AccessControl.CanEdit` rifiuterebbe. Senza questo, un
giocatore non-master che importa in una campagna dove le voci omonime sono state create da un compagno
vedrebbe un piano che il server rifiuta riga per riga.

Le RLS bloccano gli UPDATE **senza sollevare eccezioni**: la richiesta riesce e tocca zero righe. È
rilevabile — i repository già controllano i `Models` restituiti (`UpdateRaceAsync` torna `null`) — ma solo
se qualcuno guarda. Il caso davvero cieco resta il `Delete`, che con questa libreria non restituisce nulla
da controllare: è il gotcha registrato in §3 di `DA-FARE`, e non si applica qui.

L'anteprima che l'utente conferma è l'output di `PackageImportPlan`, non una stima scritta a parte: ciò che
legge è ciò che accadrà.

## 8. Interfaccia

- **Import/export** in una schermata dedicata: scelta del file → anteprima con il piano (quante voci per
  tipo, quali conflitti, quali saltate e perché) → conferma. Export come download del JSON della campagna.
  Lo stato dell'utente si legge da `CurrentUserService.EnsureLoadedAsync()`, non ripetendo il boilerplate.
- **Rimozione di un import**, dalla stessa schermata: le voci di una data provenienza si cancellano in
  blocco. Senza questo un import sbagliato — file errato, campagna errata, traduzione da rifare — sarebbe
  di fatto irreversibile: nessuno cancella ~350 righe a mano.

  È l'operazione più distruttiva dello spec e va trattata come tale:

  - **la provenienza "pacchetto dell'app" non è rimovibile.** Sarebbe il danno di §4.4 moltiplicato per
    N righe in un colpo solo, e `AccessControl.CanEdit` non farebbe da freno: le righe materializzate
    nascono con `added_by` del giocatore che le ha usate, quindi il gate direbbe di sì proprio a lui;
  - **anteprima prima della conferma**, come per l'import: quante voci per tipo, quante saltate perché
    non modificabili da chi esegue, e **quanti personaggi perderanno un incantesimo** per via del
    `ON DELETE CASCADE`. Vale anche per le provenienze rimovibili: anche una riga caricata dall'utente
    può essere nella lista di qualcuno;
  - il testo del `ConfirmDialog` riporta quei numeri invece di limitarsi a "sei sicuro?";
  - la rimozione rispetta `AccessControl.CanEdit` ed è quindi quasi sempre **parziale**: il resoconto
    finale dice cosa è stato rimosso e cosa no, per la stessa ragione dell'import (§9).
- **Pagina Background**, sul modello delle quattro esistenti (Razze, Classi, Incantesimi, Mostri), con la
  sua card in Home. Senza di essa i background esisterebbero solo via import, e chi non carica un pacchetto
  troverebbe un elenco vuoto proprio nel passo del wizard che ora concede i bonus — il vicolo cieco del
  catalogo vuoto che questo lavoro deve chiudere, non riprodurre. La pagina mostra anche il testo dei
  talenti d'origine del pacchetto (§5).
- **Cataloghi**: le voci di pacchetto sono marcate visivamente **con i token esistenti** in `:root` (niente
  literal nei `.razor.css`) e prive dei comandi di modifica; al loro posto "duplica e modifica".
- Pattern: toast `.app-toast` per la validazione, `ConfirmDialog` per la conferma, `<LoadingSpinner>`
  durante import ed export, `DbErrorBanner` per i soli errori di sistema.
- Accessibilità: i comandi nuovi (scelta file, "duplica e modifica", rimozione per provenienza) hanno
  `aria-label` e sono attivabili da tastiera, come i controlli resi accessibili in §6 di `DA-FARE`.

## 9. Errori

| Caso | Trattamento |
|---|---|
| File illeggibile, schema errato, versione non supportata | toast di validazione, con la voce colpevole |
| Voce non modificabile per permessi | segnalata **nel piano** prima della conferma, non come errore dopo |
| Sezione `feats` del pacchetto dell'app | elencata nel piano come "solo consultazione" — si legge nella pagina Background |
| Sezione `feats` di un file dell'utente | elencata come "non importabile — resta nel tuo file", mai scartata in silenzio |
| Provenienza già presente in campagna (materializzazione) | si riusa la riga esistente: nessun errore, la rilettura sul conflitto di §4.4 lo rende un caso normale |
| Errore di scrittura sul database | `DbErrorBanner` con "Ripara e ricarica" |
| Import interrotto a metà | resoconto di cosa è passato e cosa no |

PostgREST esegue ogni richiesta in transazione, ma **non offre atomicità fra richieste distinte**: l'import
procede a **blocchi**, ciascuno atomico, e chiude con un resoconto invece di fingere un'atomicità che non
abbiamo. L'esito di ogni blocco si verifica sui `Models` restituiti — non si assume il successo, per la
ragione detta in §7.

## 10. Test

- xUnit sui quattro helper puri, secondo il pattern già usato per `CharacterNormalizer` e `AccessControl`.
  Casi che non possono mancare: chiave di confronto con e senza `source_id`; **nomi accentati**, perché il
  fallimento della normalizzazione è silenzioso (§4.3); righe duplicate per nome e tie-break deterministico;
  voci non modificabili per permessi; pacchetto di versione futura; sezione `feats` marcata non importabile;
  materializzazione che riusa una riga esistente invece di crearne una seconda.
- Sulle regole di §4.7, estensione di `CharacterWizardLogic`: ripartizione +2/+1 e +1/+1/+1, tetto di 20
  applicato **prima** del clamp 1..30, specie legacy con bonus non nulli che sopprime i bonus di background,
  background a testo libero che non ne concede.
- Un test che **carica il pacchetto SRD versionato nel repo e lo valida**: un pacchetto rotto fa fallire la
  build invece di arrivare agli utenti.
- Le policy della tabella nuova `backgrounds` aggiungono i loro scenari alla suite d'integrazione RLS
  (`Tests.Integration/`). Le policy esistenti non cambiano e non vanno ritestate.

## 11. Dipendenza da procurare

Il pacchetto pubblico **non può essere derivato dal manuale che abbiamo**: il perimetro SRD è definito dal
documento **SRD 5.2**, che è un file diverso e va scaricato (è gratuito, pubblicato sotto CC BY 4.0).
Senza quel documento non sappiamo dove passa il confine fra ciò che possiamo distribuire e ciò che non
possiamo.

Prima di tradurre a mano va verificato se esiste già un pacchetto SRD **italiano** riusabile con licenza
compatibile: costa poco rispetto a ~350 incantesimi.

## 12. Ordine dei lavori

1. Formato e `CatalogPackageParser`, con i test
2. Migrazioni additive (§4.1) con le policy di `backgrounds`; i Model corrispondenti — `Models/Background.cs`
   più `SourceId`, `SpeedUnit`, `BackgroundAbilityChoice` sui Model esistenti — e `IBackgroundRepository`
3. `CatalogMerge`, `ICatalogService`, cataloghi in sola lettura nella UI (marcatura, "duplica e modifica"),
   pagina Background, **e unità di velocità visibile nel form Razze** (§4.5): "duplica e modifica" crea
   già righe in metri, quindi il form deve saperlo da subito, non alla fine
4. Esclusione del pacchetto dal precache del service worker
5. `PackageImportPlan`, schermata di import/export e rimozione per provenienza
6. Materializzazione degli incantesimi su uso (§4.4) e adeguamento del filtro per classe (§4.6)
7. **Campione SRD**: poche voci per tipo — qui il formato si dimostra sul campo
8. Traduzione del pacchetto completo, a scaglioni verificabili
9. Wizard: bonus dal background, ripartizione, tetto 20, convivenza con le specie legacy, background a
   testo libero (§4.7)

Il punto 7 esiste per una ragione precisa: l'utente ha scelto il pacchetto completo a testo integrale, e
tradurre ~350 incantesimi in un formato non ancora provato significherebbe rifarli. Il campione neutralizza
il rischio senza ridurre il risultato.

## 13. Punti da verificare sul SRD 5.2

Da chiarire sul documento prima di tradurre, invece di darli per noti:

- se le lingue nel 2024 stiano sulla specie o sul background;
- il perimetro esatto delle voci incluse nel SRD (quante specie, quanti background, quali sottoclassi,
  quali talenti d'origine) — da cui dipendono gli slug definitivi di §5;
- la formula di attribuzione richiesta dalla licenza, da riportare nel pacchetto e nell'app.

## 14. Cosa questo spec non risolve

Restano aperti in `DA-FARE` §8-bis, invariati: motore di derivazione condiviso, level-up guidato, aiuto
contestuale dal manuale, barra di navigazione e cache, iniziativa nel combat, conferma sui salvataggi
impliciti.

Due voci già decise vanno **rivalutate** quando i cataloghi si riempiono, come previsto: la
**virtualizzazione liste** (§5, scartata "sotto le ~50 voci" — un pacchetto SRD completo supera la soglia
dichiarata) e l'**aiuto AI** (§8: precaricare riduce molto ciò che resterebbe da generare).

Tre debiti che questo spec crea consapevolmente:

- la ripartizione dei bonus viene salvata (§4.7) ma **usata solo dal wizard**. Modifica e level-up la
  leggeranno quando esisterà il motore di derivazione; fino ad allora un PG modificato a mano può divergere
  dalla propria scelta registrata;
- una riga **materializzata e poi rimasta inutilizzata** non è rimovibile **singolarmente**, nemmeno dal
  master: i comandi di riga sono soppressi per provenienza (§4.4), e la rimozione in blocco di §8 esclude
  proprio la provenienza del pacchetto dell'app. Restano righe innocue in un catalogo che il merge mostra
  come voci SRD, e ripulirle richiederebbe un intervento sul database;
- **"duplica e modifica" non ritocca le schede esistenti**: la copia è una riga nuova, mentre i
  `character_spells` dei personaggi continuano a puntare all'originale. Chi corregge un incantesimo SRD non
  vede l'effetto sui PG che lo conoscono già, e deve rimuoverlo e riaggiungerlo dalla scheda.
