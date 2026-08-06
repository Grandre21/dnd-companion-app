# La scheda, alla pari con la carta — design

**Data:** 2026-08-06 · **Stato:** approvato, da pianificare
**Origine:** `character-sheet.pdf` nella root — la scheda cartacea reale di un personaggio del tavolo
(Grunnok Baldus, Barbaro 5 Berserker, Orco, Soldato), usata come pietra di paragone.

## Perché

La richiesta è che la scheda dell'app mostri tutto ciò che si vede sulla carta, e che sia **più
comoda della carta**. Dal confronto sono emerse tre mancanze e una premessa sbagliata.

**La premessa sbagliata era la mia:** credevo servisse riorganizzare la scheda per momento d'uso. Ma
quella struttura **esiste già** dal commit `a3f36c8`: il tab «Tiri» compone `CharacterCombatTab` e
`CharacterStatsTab` sotto il commento «Tutto ciò che si tira o si spende in un turno», «Scheda» è
consultazione, «Zaino» e «Magia» sono contenitori. Non c'è una struttura da scegliere: c'è una
struttura giusta da **completare**.

**Il principio che guida tutto il resto:** la carta vince sul testo statico a colpo d'occhio, l'app
vince sui contatori. Tutto ciò che sulla scheda di Grunnok è un segno a matita da cancellare — punti
ferita, usi dell'Ira, dadi vita, slot, tiri contro morte — è dove l'app può davvero fare meglio. La
prosa dei privilegi resta prosa: se ogni privilegio diventasse una card interattiva, la scheda
sarebbe *più lenta* della carta.

## Cosa manca, e cosa no

Il modello `Character` copre già quasi tutta la scheda, e **tutto ciò che contiene è già mostrato**:
gli unici campi mai visualizzati sono metadati (`OwnerId`, `CreatedAt`, `BackgroundAbilityChoice`).
Mancano tre cose.

| Manca | Dove sta sulla carta | Costo |
|---|---|---|
| **Risorse di classe con i loro usi** | le caselline a matita accanto ai privilegi | colonna `jsonb` + helper + riposo |
| **Addestramento** | `EQUIPMENT TRAINING & PROFICIENCIES` | tre colonne di testo |
| **Bonus d'attacco calcolato** | scritto a mano nel riquadro armi | due flag sull'arma + helper |

## Decisioni

| # | Decisione | Perché |
|---|---|---|
| D1 | **Struttura invariata.** Le cose nuove si collocano per *momento d'uso*: risorse e armi in «Tiri», addestramento in «Scheda». | Il criterio esiste già. Senza, le risorse finirebbero in «Scheda» perché *sono* privilegi di classe, invece che in «Tiri» perché l'Ira *si spende in combattimento*. |
| D2 | **Le risorse sono un `jsonb` additivo su `characters`**, non una tabella. | Sono 1-4 voci per personaggio, non si interrogano mai trasversalmente, e una tabella porterebbe RLS nuove. La colonna additiva è compatibile col client già online: il vecchio non la mappa, quindi i suoi update integrali non la toccano. Precedente: `combat_state.combatants`. |
| D3 | **Quattro campi per risorsa e non uno di più**: nome, massimo, spesi, ricarica. | Niente campo «effetto», niente formule, niente inneschi: la semantica resta nella prosa dei privilegi. Il contatore conta e basta. |
| D4 | **L'addestramento è testo libero**, tre colonne, non una griglia di caselle. | Si consulta due volte a campagna e non alimenta alcun calcolo. Strutturarlo sarebbe il «modulo da compilare» che la richiesta vuole evitare — e le stesse tre colonne pagano il debito già annotato in `DA-FARE` §1.B sul formato di scambio. |
| D5 | **Il bonus d'attacco calcolato è il default mostrato; il valore scritto a mano vince.** | È la valvola per l'arma magica +1, lo stile di combattimento, l'homebrew. Stesso contratto del level-up guidato: la guida è il default, la mano dell'utente è sovrana. |
| D6 | **La competenza con l'arma si assume vera**, con un interruttore «non competente» per l'eccezione. | Senza addestramento strutturato non è verificabile, e al tavolo quasi nessuno usa armi in cui non è competente. Collegare il calcolo all'addestramento costringerebbe a strutturarlo, contro D4. |
| D7 | **Una sola migrazione** per risorse, flag delle armi e addestramento. | L'applicazione a mano sull'hosted è il passaggio più rischioso del processo di questo repo: ridurne il numero vale di per sé. |
| D8 | **Il redesign visivo viene per ultimo.** | Pillole delle risorse e schede delle armi cambiano la gerarchia visiva proprio del tab «Tiri»: farlo prima significherebbe disegnarlo due volte. |

## Le risorse di classe

### Il dato

Colonna `class_resources jsonb DEFAULT '[]'::jsonb NOT NULL` su `characters`, mappata come
`List<ClassResource>` con `[Column("class_resources")]` — lo stesso pattern di
`CombatState.Combatants`, che è il precedente funzionante del progetto. Il POCO segue `Combatant`:
proprietà PascalCase, nessun attributo di serializzazione.

```csharp
public class ClassResource
{
    public string Nome { get; set; } = string.Empty;
    public int Max { get; set; }
    public int Spesi { get; set; }
    public string Ricarica { get; set; } = "lungo";   // lungo | breve | nessuna
}
```

`ricarica` vale `lungo`, `breve` o `nessuna`. L'ultimo caso serve alle risorse che si ricaricano in
altro modo (una volta per turno, a discrezione del master): il contatore c'è, il ripristino no.

Lettura e scrittura in un helper puro `Services/ClassResources.cs`, nello stile di `SubclassText`:
tolleranza al malformato — un `jsonb` che non si capisce diventa lista vuota, mai un'eccezione, mai
una scheda che non si apre.

### I suggerimenti per classe

Il sistema è generico, ma partire da un campo vuoto significa che ogni giocatore deve sapere da sé
cosa tracciare. «Aggiungi risorsa» propone quindi le risorse **della propria classe**, con nome e
tipo di ricarica già compilati; il massimo lo scrive l'utente, perché i contatori per livello non
sono nel pacchetto dati.

I nomi vanno presi **esattamente** come compaiono nel pacchetto — sotto, quelli verificati il
2026-08-06 — e inchiodati da un test che li incrocia col JSON, nello stile di `LevelUpRulesTests`:
se il pacchetto cambia una grafia, il test lo dice invece di lasciare un suggerimento che non
combacia con la tabella dei privilegi.

| Classe | Risorse suggerite | Ricarica |
|---|---|---|
| Barbaro | Ira | lungo |
| Bardo | Ispirazione bardica | lungo |
| Druido | Forma selvatica | breve |
| Guerriero | Secondo fiato, Azione impetuosa | breve |
| Mago | Recupero arcano | lungo |
| Monaco | Focus del monaco | breve |
| Paladino | Imposizione delle mani | lungo |
| Stregone | Stregoneria innata | lungo |

Le classi assenti dalla tabella non hanno risorse SRD da contare; il pulsante resta e permette di
scriverne una a mano — al tavolo capita di dover tracciare qualcosa che il manuale non prevede.

### Il riposo

**La regola di quale riposo ripristina cosa vive in `RestCalculations` e solo lì**: il riposo lungo
azzera gli spesi delle risorse con ricarica `lungo` **e** `breve`; il riposo breve solo di quelle
`breve`; `nessuna` non si tocca mai. `EsitoRiposo` si estende con l'elenco delle risorse
ripristinate, così il riepilogo le nomina.

Il componente non decide mai cosa si ricarica: mostra l'esito. Se la regola finisse anche nelle
pillole avremmo due implementazioni della stessa cosa — il difetto di giuntura tipico di questo repo.

Questo completa il riposo introdotto il 2026-08-06, che oggi ripristina punti ferita, slot e dadi
vita ma ignora le risorse perché non sapeva che esistessero.

## Il bonus d'attacco

Due flag booleani su `inventory`: `is_finesse` (accurata) e `is_ranged` (a distanza). Con quelli, un
helper puro `Services/WeaponCalculations.cs`:

- mischia → modificatore di Forza;
- accurata → il migliore fra Forza e Destrezza;
- a distanza → Destrezza;
- più il bonus di competenza, salvo che l'arma sia segnata come non competente.

Il valore calcolato si mostra come suggerimento accanto al campo; se `attack_bonus` è valorizzato a
mano, **quello vince e si mostra**, col calcolato accanto come confronto.

**Linea rossa:** nessun bonus condizionato allo stato («+2 se in Ira»). È semantica dei privilegi,
cioè il rules engine escluso due volte. Quel «+2» resta nelle note dell'arma, dove il giocatore lo
legge.

Il danno resta scritto a mano — i dadi dell'arma non sono nel modello — al più con un suggerimento
che somma il modificatore ai dadi già digitati.

## L'addestramento

Tre colonne di testo su `characters`: `armor_training`, `weapon_proficiencies`,
`tool_proficiencies`. Mostrate in «Scheda», in fondo, come le altre voci di consultazione.

Non una griglia di caselle: il dato non alimenta calcoli (v. D6) e si consulta due volte a campagna.

## Fuori perimetro

- **Nessun campo «effetto» sulle risorse**, nessun innesco automatico, nessun bonus condizionale.
- **Nessuna struttura per l'addestramento.**
- **Nessun contatore derivato dal livello**: i massimi li scrive l'utente finché i dati non ci sono.
- **Nessuna card interattiva per i privilegi**: la prosa resta prosa.
- **Niente sezioni vuote per simmetria con la carta.** La scheda cartacea di Grunnok ha mezzo foglio
  di riquadri da incantatore vuoti; l'app fa già meglio, nascondendo «Magia» a chi non incanta.

## Ordine

1. **Migrazione unica** — `class_resources` su `characters`, i tre campi di addestramento, i due flag
   su `inventory`. Da applicare a mano all'hosted, verificata sullo stack locale.
2. **Risorse di classe** — helper, suggerimenti per classe, estensione di `RestCalculations`, pillole
   nel tab «Tiri». È il dolore quotidiano più grande e completa il riposo.
3. **Attacchi calcolati** — `WeaponCalculations` e il suggerimento sulla scheda dell'arma.
4. **Addestramento** — tre campi in «Scheda».
5. **Redesign visivo** — per ultimo, su contenuti ormai stabili.

Ogni tappa è committabile da sola e non passa mai da uno stato peggiore di oggi: è la proprietà che
conta per un'app già in uso al tavolo.

## Verifica

- `dotnet build -c Release` pulita, `dotnet test` verde.
- Test d'integrazione RLS sullo stack locale per la migrazione.
- Test che incrocia i nomi delle risorse suggerite col pacchetto SRD.
- Gate a due agenti puntato sulle giunture: `ClassResources` ↔ `RestCalculations` (chi decide il
  ripristino), e `WeaponCalculations` ↔ il campo scritto a mano (chi vince).
- Verifiche manuali da aggiungere a `DA-FARE`: un riposo che ripristina le risorse; un'arma accurata
  su un personaggio con Destrezza maggiore della Forza; una risorsa scritta a mano che sopravvive al
  riposo se ha ricarica `nessuna`.
