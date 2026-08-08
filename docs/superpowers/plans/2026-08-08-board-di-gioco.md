# La board di gioco — piano di implementazione

Attua [2026-08-08-board-di-gioco-design.md](../specs/2026-08-08-board-di-gioco-design.md). Leggi quel
documento: le decisioni D1–D10 sono vincolanti e qui non si ridiscutono.

**Obiettivo:** il tab «Gioco» passa da una colonna di 1900–2400px a un mosaico di riquadri che sta in
una schermata, col dettaglio in un foglio dal basso.

**Non si tocca:** nessuna scrittura, nessun repository, nessun modello, nessuna migrazione. È un
ri-layout. `StatCard.razor` e il suo CSS restano **invariati** (D5).

---

## Contratti fra le fette — decisi a monte, validi per tutti

Chi scrive contro un simbolo che ancora non esiste lo scriva comunque: appare in un'altra fetta dello
stesso giro.

### Vocabolario CSS condiviso → `wwwroot/css/app.css`, NON scoped

`.board` e i riquadri li usano **tre componenti** (`CharacterCombatTab`, `CharacterFeaturesSection`,
`CharacterStatsTab`). L'isolamento CSS di Blazor costringerebbe a dichiararli tre volte: si promuovono
globali. Lo stile *specifico* di un componente resta scoped.

```css
.board { display: grid; grid-template-columns: repeat(12, minmax(0, 1fr)); gap: 6px; align-items: start; }
.w-2 { grid-column: span 2; } .w-3 { grid-column: span 3; }
.w-6 { grid-column: span 6; } .w-12 { grid-column: span 12; }
.w { /* riquadro */ } .w-value { /* il numero */ } .w-label { /* l'etichetta */ }
.w-sub { /* contesto, sparisce se stretto */ } .w-list { /* elenco dentro un riquadro largo */ }
```

### Parametri nuovi

```csharp
// Shared/CharacterTabs/CharacterCombatTab.razor — lo propaga a CharacterFeaturesSection, non lo consuma
[Parameter] public EventCallback<VistaPrivilegio> OnApriDettaglioPrivilegio { get; set; }

// Shared/CharacterTabs/CharacterFeaturesSection.razor
[Parameter] public EventCallback<VistaPrivilegio> OnApriDettaglio { get; set; }

// Shared/CharacterTabs/CharacterStatsTab.razor  (AbilityType è un enum, v. StatCard.razor:40)
[Parameter] public EventCallback<AbilityType> OnApriDettaglio { get; set; }
```

`CharacterStatsTab` è dichiarato **direttamente in `Pages/Characters.razor`** come `ChildContent` di
`CharacterCombatTab`: riceve il proprio callback dalla pagina, **senza passare** da `CharacterCombatTab`.

### Lo stato del foglio → `Pages/Characters.razor`

```csharp
private VistaPrivilegio? sheetPrivilegio;
private AbilityType? sheetCaratteristica;
private bool SheetAperto => sheetPrivilegio is not null || sheetCaratteristica is not null;
private void ApriSheetPrivilegio(VistaPrivilegio v) { sheetPrivilegio = v; sheetCaratteristica = null; }
private void ApriSheetCaratteristica(AbilityType a) { sheetCaratteristica = a; sheetPrivilegio = null; }
private void ChiudiSheet() { sheetPrivilegio = null; sheetCaratteristica = null; }
```

Il foglio si rende **fuori da `.sheet-sticky`** (D4), accanto al tastierino PF.

---

## Fetta A — `wwwroot/css/app.css` · `Pages/Characters.razor` · `Pages/Characters.razor.css`

**A1 — Token `--border-card: #5a3f20`** in `:root`, accanto a `--bg-card`, con un commento che dica che
era un literal ripetuto in quattro file. **Non** sostituirlo negli altri file: lo sweep è fuori scope,
lo fa chi tocca quei file per altri motivi.

**A2 — Il vocabolario dei riquadri** in `app.css`, in fondo, sotto un'intestazione di sezione. Regole
portanti:

- `.board`: `minmax(0, 1fr)` e non `1fr` — senza, un nome d'arma lungo sfonda la colonna (stesso motivo
  per cui `.vitals-bar` usa già `repeat(3, minmax(0, 1fr))`). `align-items: start` perché ogni riquadro
  sia alto quanto il suo contenuto: con lo stretch, un riquadro AZIONE da tre voci alzerebbe anche
  quello REAZIONE da una, e la board tornerebbe a sprecare verticale.
- `.w`: `background: var(--bg-card)`, `border: 1px solid var(--border-card)`, `border-radius: 8px`,
  `padding: 0.4rem 0.45rem`, `min-height: 56px` (è tappabile: apre il foglio), `min-width: 0`
  (obbligatorio dentro grid, o il contenuto detta la larghezza e la campata smette di valere),
  `container-type: inline-size`.
- `.w-value`: `font-size: clamp(1.05rem, 6.5cqw, 1.5rem)`, `font-weight: 700`, `color: var(--text)`,
  `font-variant-numeric: tabular-nums`. `cqw` lega il corpo alla **larghezza del riquadro**, così la
  stessa classe vale a `w-2` e a `w-6`.
- `.w-label`: `'Georgia', 'Cambria', serif`, `0.62rem`, `700`, `letter-spacing: 1px`, maiuscoletto,
  `color: var(--gold-dim)` — stesso linguaggio di `.card-label`.
- **D10 — la variante stretta è la BASE**, `@container` aggiunge solo il caso largo:
  ```css
  .w-sub, .w-list { display: none; }                    /* base: stretto */
  @container (min-width: 150px) {
      .w { align-items: stretch; } .w-label { text-align: left; }
      .w-sub, .w-list { display: block; }
      .w-list li { min-height: 44px; display: flex; align-items: center; }
  }
  ```
  Su un telefono senza `@container` resta il caso leggibile, non quello sfondato.

**A3 — Lo stato del foglio** in `Characters.razor` (v. contratti) e il **cablaggio**:
- a `<CharacterCombatTab>`: `OnApriDettaglioPrivilegio="@ApriSheetPrivilegio"`
- a `<CharacterStatsTab>` (il `ChildContent`): `OnApriDettaglio="@ApriSheetCaratteristica"`

**A4 — Il markup del foglio**, reso **fuori da `.sheet-sticky`**, accanto al tastierino PF, con lo
stesso ragionamento già scritto a `Characters.razor:124-135` (stacking context). Ricalca
`ConfirmDialog`/`hp-panel`: clic sullo sfondo chiude, `Esc` chiude, `role="dialog"`, `aria-modal="true"`,
`aria-label` col nome di ciò che mostra.

Contenuto:
- se `sheetPrivilegio is not null` → nome, etichetta del tag (`CharacterFeatureRules.EtichettaTag`), la
  nota per intero **senza clamp**, e il pulsante ✎ che apre la modifica esistente;
- se `sheetCaratteristica is not null` → `<StatCard Ability="@sheetCaratteristica.Value" Character="@selected" IsEditMode="@CanEdit" OnChanged="@SaveCharacterAsync" />`
  **senza modificare `StatCard`** (D5).

**A5 — Il CSS del foglio** in `Characters.razor.css`, accanto a `.hp-panel-*`:
```css
.detail-overlay { position: fixed; inset: 0; z-index: 1150; background: rgba(var(--black-rgb), 0.6);
                  display: flex; align-items: flex-end; }
.detail-sheet   { width: 100%; max-height: 70dvh; overflow-y: auto; overscroll-behavior: contain;
                  background: var(--bg-card); border-top: 1px solid var(--gold-dim);
                  border-radius: 14px 14px 0 0;
                  padding: 0.75rem 1rem calc(1rem + env(safe-area-inset-bottom, 0px));
                  animation: detail-in 180ms ease-out; }
@keyframes detail-in { from { transform: translateY(100%); } to { transform: translateY(0); } }
@media (prefers-reduced-motion: reduce) { .detail-sheet { animation: none; } }
```
`overscroll-behavior: contain` è necessario: arrivato in fondo al foglio, lo scorrimento **non** deve
passare alla board sotto, o si perde la posizione proprio mentre si legge. `env(safe-area-inset-bottom)`
e **non** `--bottom-nav-space`: il foglio copre la BottomNav.

---

## Fetta B — `CharacterCombatTab.razor` + `.razor.css`

**B1 — La ripulitura (D7).** I due `+ Aggiungi` vanno **in fondo al tab**, sotto un divisore
«MODIFICA», nello stesso stile di `.turn-divider`. Oggi stanno in testa alle sezioni: è il controllo più
raro nel posto più prezioso.

**B2 — Fondere `hit-dice-card` e `rest-card`** in un solo riquadro sotto il divisore «a fine
combattimento». Stesso contenuto, un bordo e un padding invece di due.

**B3 — La board.** `.secondary-stats` sparisce; VEL e ISPIRAZIONE diventano due `w-3` in una riga di
quattro mini-valori insieme a **DV** (dadi vita rimasti/totali) e **COMP** (bonus di competenza —
oggi in `CharacterStatsTab`, v. D1a: qui prende posizione fissa).

Le armi diventano riquadri `w-6`: nome come `.w-label`, il bonus di attacco come `.w-value`, danno e
tipo come `.w-sub`.

Le `<p class="section-header">` **spariscono**: l'etichetta entra dentro il riquadro, come nell'A4.

**B4 — I TS contro morte restano come sono**, `w-12`, **sopra la striscia ADESSO**. Se il personaggio
sta morendo non conta altro: la regola è già nel file e va tenuta.

**B5 — Propagare** `OnApriDettaglioPrivilegio` a `<CharacterFeaturesSection OnApriDettaglio="..." />`.
Questo componente non lo consuma.

**B6 — I pallini restano sui riquadri** (D5, e la decisione già presa il 2026-08-08 sulla densità): si
spende dalla board, senza aprire il foglio.

---

## Fetta C — `CharacterFeaturesSection.razor` + `.razor.css`

**C1 — Da colonna a board.** I gruppi già prodotti da `CharacterFeatureJoin` (azione / bonus / reazione
/ turno / passivi) diventano riquadri `w-6` a due per riga, **in ordine fisso** (D1a): AZIONE, BONUS,
REAZIONE, poi turno, poi PASSIVI. Un gruppo vuoto **non si rende**, ma gli altri **non si riordinano**
per riempire il buco.

Dentro un riquadro, ogni voce è una riga di `.w-list`: il nome, e i pallini se ha un contatore. La
**nota non si rende sulla board** — sta nel foglio. È questo che uccide il muro.

**C2 — Le tre densità di `CharacterFeatureDensity` restano**, ma cambiano bersaglio: ora decidono
l'aspetto della **riga dentro il riquadro** (voce attiva evidenziata, contatore esaurito smorzato), non
più card contro riga. `Classifica` **non si tocca**: cambia solo come il markup usa il suo esito.

**C3 — Il chrome di modifica esce dalla board** (D7): niente ✎ per riga, niente «Nessuna nota: tocca ✎».
Toccare una voce chiama `OnApriDettaglio.InvokeAsync(voce)`; la modifica si apre dal foglio.
`+ Aggiungi voce` va in fondo alla sezione.

**C4 — a11y**: ogni riga è un bersaglio ≥44px con `role="button"`, `tabindex="0"`, `aria-label` col nome
della voce, ed Enter/Space via `OnKey` (già importato globalmente, non qualificare).

**C5 — Il pannello di modifica in linea** (`.feature-edit-panel`) **resta nel componente** e continua a
funzionare: cambia solo chi lo apre. Non spostarlo nel foglio in questo giro.

---

## Fetta D — `CharacterStatsTab.razor` + `.razor.css`

**D1 — Sei riquadri `w-2` in una riga sola.** Ogni riquadro: sigla come `.w-label` (FOR, DES, COS, INT,
SAG, CAR), modificatore come `.w-value`, tiro salvezza come `.w-sub`. I valori si prendono da
`CharacterCalculations`, come fa già `StatCard`.

**D2 — `StatCard` non si tocca.** Né il `.razor` né il `.razor.css`: la card completa vive nel foglio,
resa dalla pagina (fetta A). Questo componente **non** rende più `<StatCard>`.

**D3 — `.stats-header` («Bonus di Competenza») sparisce**: quel dato diventa il riquadro COMP della
fetta B. Toglilo, non duplicarlo.

**D4 — Toccare un riquadro** chiama `OnApriDettaglio.InvokeAsync(AbilityType.X)`. Bersaglio ≥44px,
`role="button"`, `tabindex="0"`, `aria-label` col nome esteso della caratteristica, Enter/Space via
`OnKey`.

---

## Le giunture — è qui che il gate va puntato

| Chi scrive | Chi legge | Cosa può rompersi |
|---|---|---|
| A (vocabolario in `app.css`) | B, C, D | una classe usata con un nome diverso da quello dichiarato → riquadri senza stile |
| A (stato del foglio) | B→C, D | il callback non arriva, o arriva e la pagina non ridisegna |
| B (riquadro COMP) | D (`.stats-header` tolto) | il bonus di competenza **sparisce da entrambi** o **compare due volte** |
| B (riquadro DV) | B (`hit-dice-card` fuso) | i dadi vita in due posti che si contraddicono |
| C (pallini sulle righe) | B (`RisorseSenzaScheda`) | un contatore reso **due volte**, o sparito da tutti e due |
| A (`StatCard` nel foglio) | D (`StatCard` rimosso dal tab) | `IsEditMode`/`OnChanged` non più cablati → modifica dei punteggi rotta |

## Verifica

`dotnet build` 0/0 · `dotnet test Tests/DndCompanion.Tests.csproj` verde (partenza **1236**). Nessun
test nuovo è previsto: non nasce logica di dominio, e il layout non è testabile qui (niente bUnit).
