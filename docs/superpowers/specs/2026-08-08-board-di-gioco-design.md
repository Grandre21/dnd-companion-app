# La board di gioco — decisioni di progetto

**Data:** 2026-08-08 · **Stato:** approvato dall'utente (meccanismo e perimetro)

Segue e sostituisce il layout deciso in [2026-08-08-vista-di-gioco-design.md](./2026-08-08-vista-di-gioco-design.md).
Quel documento ha stabilito **quale informazione** mostrare e resta valido su tutto: privilegi derivati,
annotazioni in `character_features`, raggruppamento per economia d'azione, nessun campo effetto. Qui si
decide solo **come disporla**.

## Il problema

Segnalazione dell'utente, dopo aver usato la vista appena messa online: «le informazioni che ci sono mi
vanno benissimo, però è diventato un wall of text… le sezioni possono essere divise in modo migliore,
usando **widget** o cose simili per tenere tutto **a portata di occhio**».

Il tab «Gioco» è oggi **una colonna con dieci blocchi in fila**: 1900–2400px, cinque o sei schermate su
un telefono. La barra fissa in cima ne prende altri 223, il 30% dello schermo.

**Il muro non è fatto di informazione.** Le note dell'utente sono già compattissime — «Ira: 3/Riposo
Lungo. +2 Danni» sono 34 caratteri — ma ogni voce da 34 caratteri costa ~80px fra padding, margine e
intestazione di sezione. È un problema di spazio bianco, non di parole. E una quota rilevante è
**amministrazione renderizzata nella superficie di gioco**: «+ Aggiungi voce» e «+ Aggiungi risorsa» in
cima alle sezioni, la ✎ su ogni riga, «Nessuna nota: tocca ✎ per scriverla», «Ricarica: riposo lungo»
ripetuto. La scheda di carta non ha affordance di modifica: ci si scrive sopra e basta.

## D1 — Mosaico di riquadri, non fisarmonica

Due consulenze indipendenti hanno raccomandato meccanismi **incompatibili**, e la divergenza va
registrata perché la scelta non sembri l'unica possibile:

- **fisarmonica con lo stato ricordato**, riassunto dentro l'intestazione chiusa;
- **mosaico di riquadri** su griglia a 12 colonne, dettaglio in un foglio dal basso.

**Si adotta il mosaico.** Tre ragioni, in ordine di peso:

1. La consulenza che proponeva la fisarmonica ha identificato correttamente il vantaggio della carta —
   la **memoria spaziale**, «la mano sa dove sono i PF senza leggere» — e poi ha scelto il meccanismo
   che quella proprietà la danneggia: aprire una sezione sposta in basso tutto ciò che segue. Dopo due
   tocchi la mappa che il giocatore ha in testa non è assente, è **sbagliata**, che è peggio.
2. Lo ammette da sé: «la fisarmonica *differisce* il testo, non lo riduce — se l'utente apre tutto e lo
   stato persiste, il wall of text torna identico».
3. L'utente ha chiesto «widget», e il metro di paragone che vince è un mosaico. Riprodurre ciò che
   funziona batte inventarne un sostituto.

**Cosa costa**: il mosaico è più lavoro, e ogni riquadro mostra poco per costruzione — il dettaglio è
sempre a un tocco. La contromisura è D5.

Dalla consulenza scartata si tengono due punti che l'altra non aveva:

- **D1a — L'ordine dei riquadri è fisso e non si riordina mai**, nemmeno quando un riquadro resta
  quasi vuoto. La stabilità posizionale *è* la funzione; un layout che si compatta da sé la distrugge.
- **D1b — Se un giorno il personaggio è un incantatore** che dentro il turno salta di continuo fra
  «Gioco» e «Magia», allora è la partizione in quattro tab a essere sbagliata, e questa decisione va
  riaperta. Oggi non lo è.

## D2 — La regola che decide widget o elenco

> **Diventa widget una domanda con una risposta di poche cifre, che ti poni durante un turno.
> Resta elenco tutto ciò che si legge invece di leggersi.**
>
> La campata la decide la lunghezza della risposta, non l'importanza:
> **un numero → 2–3 colonne · nome + numero + contesto → 6 · prosa → 12.**

Il criterio separa per **momento d'uso**, non per rilevanza. «Atletica +7» ha una risposta brevissima ed
è importante, ma la innesca il master, non il tuo turno: non è un widget. «Spadone +7 / 2d6» lo è. È lo
stesso principio per cui i privilegi sono già raggruppati in azione/bonus/reazione invece che in
classe/talenti/specie.

Vale anche per i tab non ancora ridisegnati: in **Zaino** «340 mo» e «Carico 47/180» sono widget, «Corda
di canapa · 15 m · 5 kg» è una riga; in **Magia** «Slot 3° ●●○» e «CD 15 / +7» sono widget, la
descrizione dell'incantesimo è una riga.

## D3 — Il dettaglio è un foglio dal basso, uno solo per tutto

Niente espansione in linea, per la ragione di D1: sposterebbe il resto della board. Il foglio copre e
non muove.

**Dal basso e non al centro**: il telefono sta sul tavolo o in una mano, e il terzo inferiore è l'unico
raggiungibile col pollice. Il gesto per chiuderlo è dove il pollice è già.

Il pattern esiste già in casa — `ConfirmDialog`, `LevelUpDialog`, il tastierino PF sono tutti «overlay
fisso + box + clic fuori chiude + Esc chiude». Il foglio è lo stesso con `align-items: flex-end`.

## D4 — Il foglio si rende da `Pages/Characters.razor`, fuori da `.sheet-sticky`

Non è una preferenza di stile: `.sheet-sticky` è `position: sticky; z-index: 10` e **crea uno stacking
context**. Un overlay reso lì dentro verrebbe dipinto a quota 10 nella radice — sotto BottomNav (100),
UpdateBanner (1050) e i toast (1100) — e alzare lo z-index del contenitore non servirebbe, perché il
contesto resterebbe annidato. È lo stesso ragionamento già scritto in `Characters.razor:124-135` per il
tastierino danni/cura.

Renderlo dalla pagina compra anche il ri-render: la pagina possiede `selected` e chiama già
`StateHasChanged`. Un `SheetService` in stile `ToastService` sarebbe più elegante da invocare, ma il suo
host vivrebbe nel layout e non si ridisegnerebbe al cambio di personaggio.

**Prezzo dichiarato**: l'apertura è richiesta da un nipote (`CharacterFeaturesSection`) e risale due
livelli via `EventCallback`, come fa già `OnSpendiContatore`. Il payload è **tipizzato e di soli dati**,
mai un `RenderFragment` che risale.

## D5 — Il riquadro porta l'azione, il foglio porta la spiegazione

Spendere un uso dell'Ira si fa **sul riquadro**: un tocco, niente foglio. Il foglio si apre per
**leggere**.

Ne segue che il contenuto del foglio è di sola lettura più una ✎, e non serve alcun meccanismo per
tenerlo sincronizzato mentre ci si interagisce. Ne segue anche che il foglio di «FOR» può contenere
`<StatCard Ability="Strength" …>` **così com'è**: sei riquadri sulla board, la card completa nel foglio,
zero modifiche a `StatCard`.

## D6 — Un dato, una sola superficie primaria

`RisorseSenzaScheda` lo codifica già per l'Ira e va esteso alla board: i pallini dell'Ira stanno **solo**
nel riquadro BONUS. Un contatore senza privilegio a cui agganciarsi prende un riquadro proprio.

## D7 — Il chrome di modifica esce dal percorso del pollice

I due `+ Aggiungi` vanno **in fondo**, sotto un divisore «Modifica». Le ✎ e gli Elimina stanno nel
foglio, non sulla board. Motivo: al tavolo si è in lettura il 95% del tempo, e un tocco sbagliato oggi
apre un pannello di modifica che spinge in giù tutta la schermata **nel mezzo del turno**.

È l'unico punto su cui **entrambe** le consulenze convergono, e da solo recupera spazio a costo
informativo zero.

## D8 — Le abilità passano dietro un tocco (scelta dell'utente)

Le sei caratteristiche con TS e abilità occupano ~550px, metà del muro. Diventano **sei riquadri da
65px in totale**; le abilità si leggono nel foglio della caratteristica.

Decisione dell'utente, motivata: le abilità le chiede il master, non le innesca il turno.

## D9 — Un solo token nuovo: `--border-card`

`#5a3f20` è oggi un **literal ripetuto in almeno quattro file** (`CharacterCombatTab.razor.css` 7 volte,
`CharacterVitalsBar.razor.css`, `StatCard.razor.css`, `Characters.razor.css`). La board ne moltiplica
l'uso: si promuove in `:root`. Nient'altro di nuovo — `--bg-card`, `--gold`, `--gold-dim`,
`--gold-muted`, `--text`, `--text-body`, `--gold-rgb`, `--black-rgb` coprono il resto.

## D10 — Le container query, con la variante stretta come base

Un riquadro non sa quanto è largo: la larghezza dipende dalla **campata**, non dal viewport, e nessun
`@media` può distinguere un riquadro da 2 colonne da uno da 6 sullo stesso schermo. Serve
`container-type: inline-size`.

**Ma `@container` è Safari 16+ / Chrome 105+.** Su un telefono più vecchio quei blocchi vengono ignorati
in blocco: quindi **la variante stretta si scrive come base**, e `@container` aggiunge solo il caso
largo. Ciò che resta senza supporto dev'essere il caso leggibile, non quello sfondato.

## Fuori perimetro

- **La partizione in quattro tab non si tocca** (v. D1b).
- **Non si tocca nessuna scrittura**: né i repository, né il modello, né il database. È un ri-layout.
- **Il testo non si rimpicciolisce** per far stare più roba. Con la luce bassa, il telefono piatto sul
  tavolo e una palette a basso contrasto per scelta, sotto i 12px il testo non c'è. Lo spazio si prende
  togliendo ripetizioni e padding; i numeri che si tirano vanno nella direzione opposta.
- **Niente gesti**: nessuno swipe fra i tab, niente dietro uno strisciamento. Al tavolo il pollice
  appoggia sullo schermo mentre l'altra mano raccoglie i dadi, e un gesto non ha affordance — con la
  luce bassa non si vede ciò che non si tocca. Tutto dev'essere un bersaglio visibile.
