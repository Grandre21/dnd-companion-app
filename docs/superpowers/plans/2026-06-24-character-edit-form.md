# Estrazione CharacterEditForm — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (o subagent-driven-development) per eseguire task-by-task. Step in checkbox (`- [ ]`).
>
> Spec: [`../specs/2026-06-24-character-edit-form-design.md`](../specs/2026-06-24-character-edit-form-design.md).
> Continuazione di Fase 2B (i 5 tab sono già estratti in `Shared/CharacterTabs/`). Questo estrae l'ultimo blocco
> grosso: il form di modifica/creazione.

**Goal:** Estrarre il blocco `case ViewMode.Form:` di `Pages/Characters.razor` in `Shared/CharacterTabs/CharacterEditForm.razor`, a comportamento e aspetto invariati.

**Architecture:** Un unico componente che riceve `Draft` (l'`editDraft`, mutato in place via `@bind`), i cataloghi `Classes`/`Races`, e gli `EventCallback OnSave`/`OnCancel`. Il genitore resta proprietario della creazione del draft, della persistenza (`SaveFormAsync`/`NormalizeDraft`) e del cambio vista. Stesso pattern dei tab già estratti — usare quelli come riferimento (`CharacterItemsTab` è il più simile per dimensione e per la gestione CSS).

**Tech Stack:** Blazor WebAssembly .NET 10, componenti `.razor` + CSS isolation, xUnit (`DndCompanion.Tests`).

## Global Constraints

- Solo branch `main`; **push = deploy**. `Characters.razor` è la **feature principale**: build + test + **verifica locale** (`https://localhost:7076`) prima del push.
- **Commit/push solo su ok esplicito dell'utente.**
- Build: `dotnet build -c Debug` (atteso 0 errori/0 avvisi). Test: `dotnet test Tests/DndCompanion.Tests.csproj` (atteso 62 verdi).
- **Comportamento e aspetto INVARIATI.** Estrazione pura.
- Regola markup spostato: dentro il componente `editDraft` → `Draft`, `classes` → `Classes`, `races` → `Races`; gli helper puri (`FormatBonus`/`AriaBool`/`OnKey`/`GetSpellSlotMax`/`GetSpellSlotUsed`/`SetSpellSlotUsed`) restano invariati (risolti via `@using static CharacterView`); `CharacterCalculations.*` invariato.
- I numeri di riga in questo piano sono indicativi (stato al 2026-06-24): **riconfermare con grep** prima di ogni `sed`.

## File Structure

- `Shared/CharacterTabs/CharacterEditForm.razor` (+ `.razor.css`) — il form, nuovo.
- `Pages/Characters.razor` — il `case ViewMode.Form:` diventa `<CharacterEditForm … />`; perde i membri del form.
- `Pages/Characters.razor.css` — perde i blocchi CSS "Form" + "Form: accordion".

---

### Task 1: Estrarre `CharacterEditForm`

Estrazione unica (il form è un blocco coerente). Mirror della procedura usata per `CharacterItemsTab`.

**Files:**
- Create: `Shared/CharacterTabs/CharacterEditForm.razor`, `Shared/CharacterTabs/CharacterEditForm.razor.css`
- Modify: `Pages/Characters.razor` (sostituzione blocco `case ViewMode.Form:` + rimozione membri form), `Pages/Characters.razor.css`

**Interfaces:**
- Consumes: `CharacterView` (helper slot/format via using static), `CharacterCalculations`, `Character`/`CharacterClass`/`Race`.
- Produces: `<CharacterEditForm Draft Classes Races OnSave OnCancel />`.

- [ ] **Step 1: Mappare confini e membri**

```
grep -nE "case ViewMode\.(Detail|Form)|class=\"form-view\"|@onclick=\"SaveFormAsync\"|break;" Pages/Characters.razor   # confini markup form (da ~case ViewMode.Form a break;)
grep -nE "editDraft|formSections|ToggleFormSection|ResetFormSections|isClassCustom|isRaceCustom|SelectedClassInfo|SelectedRaceInfo|CurrentClassSelection|CurrentRaceSelection|OnClassSelectionChanged|OnRaceSelectionChanged|FormatRaceBonuses|CustomOptionValue|OnSpellSlotMaxChanged|OnSpellSlotUsedChanged|ParseSlotInput|SetSpellSlotMax" Pages/Characters.razor
grep -nE "^/\* =====" Pages/Characters.razor.css | grep -iE "Form|Desktop"
```
Annotare: range markup form (≈154-720), range CSS form ("Form" + "Form: accordion", ≈317-713), e quali membri `@code` sono usati SOLO nel form (vs anche altrove — es. `SetSpellSlotMax`/`ParseSlotInput` solo form; `editDraft`/`classes`/`races`/`SelectedClassInfo`/`SelectedRaceInfo` usati anche fuori dal form? verificare).

- [ ] **Step 2: Creare `CharacterEditForm.razor`**

Markup = contenuto del `case ViewMode.Form:` (dal `<div class="form-view">` fino alla chiusura prima di `break;`), con `editDraft`→`Draft`, `classes`→`Classes`, `races`→`Races`. Bottoni: il "Salva" (oggi `@onclick="SaveFormAsync"`) → `@onclick="OnSave"`? No: deve invocare la callback → `@onclick="@(() => OnSave.InvokeAsync())"`; "Annulla" (`CancelForm`) → `@onclick="@(() => OnCancel.InvokeAsync())"`. Il `disabled="@isBusy"` del Salva: `isBusy` è del genitore (stato di salvataggio) → aggiungere `[Parameter] bool IsBusy` e usarlo, oppure rimuovere il disabled (preferito: passare `IsBusy`). `@code`:

```razor
@code {
    [Parameter, EditorRequired] public Character Draft { get; set; } = default!;
    [Parameter] public List<CharacterClass> Classes { get; set; } = new();
    [Parameter] public List<Race> Races { get; set; } = new();
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback OnSave { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private const string CustomOptionValue = "__custom__";   // (verbatim dal valore attuale nel parent)
    private bool isClassCustom;
    private bool isRaceCustom;
    private Character? _lastDraft;

    private Dictionary<string, bool> formSections = new()
    {
        { "identity", true }, { "combat", false }, { "stats", false },
        { "defenses", false }, { "lore", false }, { "resources", false }, { "spellcasting", false }
    };

    // Reset stato UI a ogni apertura del form (nuovo editDraft = nuovo riferimento).
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(Draft, _lastDraft))
        {
            _lastDraft = Draft;
            foreach (var k in formSections.Keys.ToList()) formSections[k] = k == "identity";
            // isClassCustom/isRaceCustom: custom se la classe/razza non è nel catalogo (replica la logica di OpenEditForm).
            isClassCustom = !string.IsNullOrEmpty(Draft.Class) && Classes.All(c => c.Name != Draft.Class);
            isRaceCustom = !string.IsNullOrEmpty(Draft.Race) && Races.All(r => r.Name != Draft.Race);
        }
    }

    // Spostare verbatim dal @code di Characters.razor (con editDraft→Draft, classes→Classes, races→Races):
    //   ToggleFormSection, (ResetFormSections NON serve: la sostituisce OnParametersSet),
    //   SelectedClassInfo, SelectedRaceInfo, CurrentClassSelection, CurrentRaceSelection,
    //   OnClassSelectionChanged, OnRaceSelectionChanged, FormatRaceBonuses,
    //   OnSpellSlotMaxChanged, OnSpellSlotUsedChanged, ParseSlotInput, SetSpellSlotMax.
    // Nota: GetSpellSlotMax/GetSpellSlotUsed/SetSpellSlotUsed sono in CharacterView (using static) → invariati.
}
```

> ⚠️ Verificare la logica reale di `OpenEditForm` per `isClassCustom`/`isRaceCustom` e replicarla esatta in `OnParametersSet` (il criterio sopra è quello atteso, ma confermare leggendo `OpenEditForm`).

- [ ] **Step 3: Creare `CharacterEditForm.razor.css`**

Spostare i blocchi CSS "Form (…)" e "Form: sezioni a fisarmonica (accordion)" da `Characters.razor.css` (tecnica: `sed -n 'A,Bp' > file` poi `sed -i 'A,Bd'`, con A,B da grep). **Duplicare** in coda al file le classi form generiche `.field`/`.field-full`/`.field label`/`.input`/`.input:focus`/`select.input`/`select.input option`/`.primary-btn`(+varianti)/`.secondary-btn`(+varianti) — sono scoped per-pagina, servono al form nel componente (vedi memoria CSS isolation). Verificare con grep se restano usate nel parent: se NO, spostarle (non duplicarle).

- [ ] **Step 4: Sostituire il blocco nel parent**

Nel `case ViewMode.Form:` di `Characters.razor`, sostituire tutto il markup interno con:

```razor
        case ViewMode.Form:
            <CharacterEditForm Draft="@editDraft" Classes="@classes" Races="@races"
                               IsBusy="@isBusy" OnSave="@SaveFormAsync" OnCancel="@CancelForm" />
            break;
```

(Tecnica: `sed -i 'A,Bc__FORM_SLOT__'` sul range interno, poi Edit del segnaposto — come fatto per i tab.)

- [ ] **Step 5: Rimuovere i membri spostati dal parent + pulire `OpenEditForm`**

Rimuovere dal `@code` di `Characters.razor`: `formSections`, `ToggleFormSection`, `ResetFormSections`, `isClassCustom`, `isRaceCustom`, `SelectedClassInfo`, `SelectedRaceInfo`, `CurrentClassSelection`, `CurrentRaceSelection`, `OnClassSelectionChanged`, `OnRaceSelectionChanged`, `FormatRaceBonuses`, `OnSpellSlotMaxChanged`, `OnSpellSlotUsedChanged`, `ParseSlotInput`, `SetSpellSlotMax`, `CustomOptionValue`. In `OpenEditForm`, **togliere** le righe che settano `formSections`/`ResetFormSections`/`isClassCustom`/`isRaceCustom` (ora le fa il figlio); **tenere** la creazione di `editDraft` (clone) e il cambio vista. **Tenere**: `editDraft`, `classes`, `races`, `CloneCharacter`, `NormalizeDraft`, `SaveFormAsync`, `CancelForm`, `StartEdit`, `OpenEditForm`, il flusso "nuovo PG". Verificare con grep nessun riferimento orfano dopo la rimozione.

- [ ] **Step 6: Build + test**

`dotnet build -c Debug` → 0/0. `dotnet test Tests/DndCompanion.Tests.csproj` → 62 verdi. Sistemare eventuali orfani che il compilatore segnala.

- [ ] **Step 7: [UTENTE] Verifica locale**

Su `https://localhost:7076`: **nuovo PG** (FAB) — compilare le 7 sezioni, classe/razza da catalogo e "Altro", slot incantesimo, salva → compare in lista. **Modifica** PG esistente (✎) — campi precompilati, accordion, classe/razza custom rilevata, salva persiste; **Annulla** scarta. Aspetto identico.

- [ ] **Step 8: [su ok utente] Commit + (su ok) push**

```bash
git add Shared/CharacterTabs/CharacterEditForm.razor Shared/CharacterTabs/CharacterEditForm.razor.css Pages/Characters.razor Pages/Characters.razor.css
git commit -m "refactor(characters): estrai CharacterEditForm (form modifica/creazione)"
# push solo dopo verifica locale e ok esplicito
```

---

### Task 2: Documentazione

- [ ] **Step 1: DIARIO** — aggiungere al paragrafo "Refactor Characters.razor" che anche il form è estratto (`CharacterEditForm`); aggiornare le righe finali di `Characters.razor`.
- [ ] **Step 2: DA-FARE §3** — marcare ✅ anche il form; resta solo, della componentizzazione, niente (o eventuali sotto-sezioni minori). Restano le sotto-fasi A (repository) e C (stato auth).
- [ ] **Step 3: [su ok utente] Commit.**

---

## Self-Review

- **Copertura spec:** interfaccia §3 → Step 2/4; membri spostati §4 → Step 2/5; cosa resta §5 → Step 5; CSS §6 → Step 3; verifica §7 → Step 7. Tutto coperto.
- **Placeholder:** gli elenchi "spostare verbatim i membri" sono mosse meccaniche di codice esistente identificato per nome; i numeri di riga sono espliciti come "riconfermare con grep". L'unico punto da verificare a runtime è la logica `isClassCustom`/`isRaceCustom` (Step 2 lo segnala).
- **Coerenza nomi:** `Draft`/`Classes`/`Races`/`IsBusy`/`OnSave`/`OnCancel`; `editDraft`/`classes`/`races`/`SaveFormAsync`/`CancelForm`/`OpenEditForm` lato genitore.
- **Rischio chiave:** init `isClassCustom`/`isRaceCustom` in `OnParametersSet` (replicare `OpenEditForm`); CSS form generico duplicato/spostato secondo uso residuo nel parent.
