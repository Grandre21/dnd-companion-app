# Spec — Estrazione `CharacterEditForm` da `Characters.razor`

> Stato: **design approvato** (2026-06-24). Implementazione prevista in una sessione nuova.
> Continuazione di Fase 2B (vedi [`2026-06-24-characters-componentization-design.md`](./2026-06-24-characters-componentization-design.md));
> il form era esplicitamente fuori scope lì, qui è il suo follow-up. Indipendente da sotto-fasi A (repository) e C (stato auth).

## 1. Obiettivo e confini

Dopo l'estrazione dei 5 tab, il blocco più grosso rimasto in `Pages/Characters.razor` (~1.35k righe) è il
**form di modifica/creazione PG** (`case ViewMode.Form:`, righe ~154-720): ~566 righe di markup (accordion a 7
sezioni) + un'ampia superficie `@code`.

**Obiettivo:** estrarlo in un unico componente `Shared/CharacterTabs/CharacterEditForm.razor`, **comportamento
e aspetto invariati** (estrazione pura). Attesa: `Characters.razor` scende a ~750-800 righe.

**Fuori scope:** sotto-fasi A (`SupabaseService` → repository) e C (stato auth/ruolo). Nessun cambio di logica
del form (validazione, clamp, round-trip dei campi).

**Criterio di successo:** creare un nuovo PG e modificarne uno esistente funziona identico (classe/razza da
catalogo e custom, slot incantesimo, salva/annulla); build 0/0 + 62 test verdi.

## 2. Grana: un solo componente

L'accordion ha 7 sezioni (1. Identità → 7. Incantesimi), ma **non** si spezzano in 7 sotto-componenti (YAGNI):
un unico `CharacterEditForm`, coerente con "ogni tab è un componente". Le sezioni restano blocchi
`@if (formSections["..."])` interni al componente.

## 3. Interfaccia

```razor
<CharacterEditForm Draft="@editDraft"
                   Classes="@classes"
                   Races="@races"
                   OnSave="@SaveFormAsync"
                   OnCancel="@CancelForm" />
```

- `[Parameter] Character Draft` — l'`editDraft` (oggetto vivo). Gli input fanno `@bind="Draft.X"` → mutano in
  place; al salvataggio il genitore legge `editDraft`.
- `[Parameter] List<CharacterClass> Classes`, `[Parameter] List<Race> Races` — cataloghi per le tendine (già
  caricati e posseduti dal genitore).
- `[Parameter] EventCallback OnSave` — il genitore esegue `SaveFormAsync` (normalizza via `NormalizeDraft`,
  persiste, aggiorna la lista, cambia vista, toast).
- `[Parameter] EventCallback OnCancel` — il genitore esegue `CancelForm` (cambio vista).

## 4. Cosa si sposta nel componente

Stato UI e logica **del solo form**:
- `formSections` (dizionario apertura sezioni) + `ToggleFormSection` + `ResetFormSections`.
- `isClassCustom` / `isRaceCustom` + `SelectedClassInfo` / `SelectedRaceInfo` +
  `CurrentClassSelection` / `CurrentRaceSelection` + `OnClassSelectionChanged` / `OnRaceSelectionChanged`.
- `FormatRaceBonuses`, `CustomOptionValue`.
- Handler degli **input slot del form**: `OnSpellSlotMaxChanged`, `OnSpellSlotUsedChanged`, `ParseSlotInput`,
  `SetSpellSlotMax`. (`GetSpellSlotMax`/`GetSpellSlotUsed`/`SetSpellSlotUsed` sono già in `CharacterView`,
  risolti via `@using static` — il componente li usa così com'è.)

**Inizializzazione (`OnParametersSet`):** al cambio del riferimento `Draft` (ogni apertura del form crea un
nuovo `editDraft`), il componente reinizializza: `formSections` (Identità aperta, resto chiuso) e
`isClassCustom`/`isRaceCustom` (in base a se `Draft.Class`/`Draft.Race` esistono nei cataloghi). Traccia
`Character? _lastDraft` e confronta con `ReferenceEquals` (non per Id: un nuovo PG ha Id vuoto).

## 5. Cosa resta nel genitore

- Creazione dell'`editDraft`: `OpenEditForm` (modifica → clone via `CloneCharacter`) e il flusso "nuovo PG"
  (FAB); `CloneCharacter`; `StartEdit`.
- `NormalizeDraft` + `SaveFormAsync` (persistenza + aggiornamento lista + cambio vista) e `CancelForm`.
- Caricamento e possesso di `classes`/`races` (passati come parametri).
- Il `case ViewMode.Form:` nel markup diventa il tag `<CharacterEditForm … />`.

> Nota: `OpenEditForm` oggi setta anche `formSections`/`isClassCustom`/`isRaceCustom`. Dopo l'estrazione **non**
> li setta più (vivono nel figlio, che si auto-inizializza in `OnParametersSet`): rimuovere quelle righe da
> `OpenEditForm`.

## 6. CSS

Le sezioni di `Characters.razor.css` **"Form (…)"** e **"Form: sezioni a fisarmonica (accordion)"** (attualmente
~righe 317-713, da riconfermare con grep) si **spostano** in `CharacterEditForm.razor.css`. Includono i selettori
usati solo dal form: `.form-view`, `.form-accordion`, `.form-section*`, `.score-grid`/`.stat-field`/`.score-mod`,
`.checkbox-col`, `.dual-field`, `.spell-slots-head`, `.spell-slot-input-*`, `.field-group`, `.field-hint`,
`.custom-input`, `.info-summary`, `.sub-block*`. Le **classi form generiche** (`.field`, `.field-full`,
`.field label`, `.input`, `.input:focus`, `select.input`, `.primary-btn`, `.secondary-btn`) seguono il pattern
già usato per `CharacterItemsTab`: **duplicate** nel `*.razor.css` del componente (restano anche nel parent se
ancora servono altrove; verificare con grep — dopo questa estrazione il parent potrebbe non usarle più, nel qual
caso si spostano del tutto). Riferimento: memoria di progetto sul gotcha CSS isolation.

## 7. Testing e verifica

Nessun unit test nuovo (componenti `.razor` non testabili senza bUnit). Rete di sicurezza: **build 0/0** + i
**62 test esistenti** + prova manuale su `https://localhost:7076`:
- **Nuovo PG** (FAB): compilare le 7 sezioni, classe/razza da catalogo e "Altro (testo libero)", slot
  incantesimo, salva → compare nella lista.
- **Modifica** PG esistente (✎): i campi sono precompilati, le sezioni si aprono/chiudono, classe/razza custom
  rilevata, salva persiste; **Annulla** scarta.

## 8. Rischi e mitigazioni

- **Binding `editDraft`**: il form muta `Draft` in place (riferimento condiviso col genitore) → al salvataggio
  il genitore vede i valori. Pattern standard.
- **Init di `isClassCustom`/`isRaceCustom`**: l'attuale `OpenEditForm` li calcola; spostando la logica nel
  figlio, replicarla in `OnParametersSet` (stesso criterio: classe/razza non presente nei cataloghi ⇒ custom).
  Da verificare a vista che modificando un PG con classe custom il campo testo appaia.
- **Reset accordion** a ogni apertura: garantito dal confronto `ReferenceEquals(Draft, _lastDraft)`.
- **CSS form generico** (`.field`/`.input`/…): duplicare nel componente; verificare con grep se restano usate
  nel parent prima di rimuoverle da lì.
- **Estrazione grande**: procedere come per i tab — un commit unico per l'intera estrazione, build + test +
  verifica locale prima del push; `Characters.razor` è la feature principale.
