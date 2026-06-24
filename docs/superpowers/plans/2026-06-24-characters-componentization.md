# Estrazione tab di Characters.razor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> Spec: [`../specs/2026-06-24-characters-componentization-design.md`](../specs/2026-06-24-characters-componentization-design.md).

**Goal:** Estrarre i 5 tab di `Pages/Characters.razor` (~2.4k righe) in componenti figli in `Shared/CharacterTabs/`, a comportamento e aspetto identici.

**Architecture:** Pattern già provato da `StatCard`: ogni tab è un componente che riceve `Character` (oggetto vivo) + `CanEdit` + `EventCallback OnChanged`; muta i campi e notifica il genitore, che persiste. Stato UI locale (draft, flag) nel figlio; helper puri condivisi in `CharacterView` via `@using static`. L'inventario resta del genitore (condiviso tra Combat e Items); `character_spells` è privato del Magic tab. CSS isolato spostato per-tab.

**Tech Stack:** Blazor WebAssembly .NET 10, componenti `.razor` + CSS isolation (`.razor.css`), xUnit (`DndCompanion.Tests`).

## Global Constraints

- Solo branch `main`; **push = deploy in produzione**. `Characters.razor` è la **feature principale**: **verifica locale prima di ogni push** (regola di prudenza).
- **Commit/push solo su ok esplicito dell'utente.**
- Login locale abilitato: verifica su **`https://localhost:7076`** (profilo https di VS).
- Build: **`dotnet build -c Debug`** (atteso 0 errori/0 avvisi). Test: **`dotnet test Tests/DndCompanion.Tests.csproj`** (atteso 62 verdi) come guardia di non-regressione.
- **Comportamento e aspetto INVARIATI**: estrazione pura. Nessun cambio di logica, testo, selettori CSS.
- **Un tab per task**, ognuno con build + test + verifica locale + commit. Fuori scope: form di modifica (`ViewMode.Form`, righe ~700-1330) e data layer.
- Regola di trasformazione del markup spostato: dentro un componente, `selected` → `Character`; le chiamate agli helper puri (`FormatBonus`/`AriaBool`/`OnKey`) restano invariate grazie a `@using static` (Task 0); `CharacterCalculations.*` invariato.

---

## File Structure

- `Shared/CharacterTabs/CharacterView.cs` — helper statici puri condivisi (FormatBonus, AriaBool, OnKey).
- `Shared/CharacterTabs/CharacterBioTab.razor` (+ `.razor.css`) — tab Bio.
- `Shared/CharacterTabs/CharacterStatsTab.razor` (+ `.razor.css`) — tab Stats.
- `Shared/CharacterTabs/CharacterCombatTab.razor` (+ `.razor.css`) — tab Combat.
- `Shared/CharacterTabs/CharacterItemsTab.razor` (+ `.razor.css`) — tab Items.
- `Shared/CharacterTabs/CharacterMagicTab.razor` (+ `.razor.css`) — tab Magic.
- `Pages/Characters.razor` — diventa orchestratore (tab-bar, `selected`, persistenza, inventario condiviso).
- `Pages/Characters.razor.css` — perde i blocchi CSS spostati nei figli; resta layout tab-bar + condivisi.
- `_Imports.razor` — aggiunge `@using static DndCompanion.Shared.CharacterTabs.CharacterView`.

---

### Task 0: Helper condivisi `CharacterView` + `@using static`

Estrae i 3 helper puri usati da più tab in una classe statica, così i componenti (e il genitore) li chiamano senza qualificarli.

**Files:**
- Create: `Shared/CharacterTabs/CharacterView.cs`
- Modify: `_Imports.razor` (aggiunta using static)
- Modify: `Pages/Characters.razor` (rimozione delle 3 definizioni dal `@code`)

**Interfaces:**
- Produces: `public static class CharacterView` con `string FormatBonus(int)`, `string AriaBool(bool)`, `Task OnKey(KeyboardEventArgs, Action)` (firme da copiare verbatim dal `@code` attuale).

- [ ] **Step 1: Individuare le 3 definizioni nel `@code`**

Run: `grep -nE "FormatBonus|AriaBool|OnKey" Pages/Characters.razor | grep -E "static|private|=>"`
Atteso: le righe di definizione (FormatBonus a `:1408`; AriaBool e OnKey altrove nel `@code`).

- [ ] **Step 2: Creare `Shared/CharacterTabs/CharacterView.cs`**

Copiare i corpi **verbatim** dal `@code`, resi `public static`, in:

```csharp
using Microsoft.AspNetCore.Components.Web;

namespace DndCompanion.Shared.CharacterTabs;

public static class CharacterView
{
    // FormatBonus: verbatim da Characters.razor (oggi `private static string FormatBonus(int v) => ...`)
    public static string FormatBonus(int v) => v >= 0 ? $"+{v}" : v.ToString();

    // AriaBool e OnKey: copiare verbatim i corpi attuali dal @code di Characters.razor,
    // cambiando solo la visibilità in `public static`. (Firme tipiche:
    //   public static string AriaBool(bool b) => b ? "true" : "false";
    //   public static Task OnKey(KeyboardEventArgs e, Action action) { ... } )
}
```

> Nota: AriaBool/OnKey vanno copiati dal sorgente reale (non inventati); FormatBonus è già noto (`:1408`).

- [ ] **Step 3: Aggiungere lo using static globale**

In `_Imports.razor` aggiungere:

```razor
@using static DndCompanion.Shared.CharacterTabs.CharacterView
```

- [ ] **Step 4: Rimuovere le 3 definizioni dal `@code` di `Characters.razor`**

Cancellare le definizioni di `FormatBonus`, `AriaBool`, `OnKey` da `Pages/Characters.razor` (ora vivono in `CharacterView`). I call-site nel markup restano invariati (risolti via using static).

- [ ] **Step 5: Build + test**

Run: `dotnet build -c Debug` → atteso 0 errori/0 avvisi.
Run: `dotnet test Tests/DndCompanion.Tests.csproj` → atteso 62 verdi.

- [ ] **Step 6: [UTENTE] Verifica locale** — su `https://localhost:7076`, aprire un PG: i tab Combat/Stats/Magic mostrano bonus/ispirazione/slot come prima (gli helper risolvono).

- [ ] **Step 7: [su ok utente] Commit**

```bash
git add Shared/CharacterTabs/CharacterView.cs _Imports.razor Pages/Characters.razor
git commit -m "refactor(characters): estrai helper puri in CharacterView (using static)"
```

---

### Task 1: `CharacterBioTab` (pattern-setter)

Tab più pulito: campi `Character` in sola lettura + textarea "note libere" con salvataggio. Stabilisce il pattern `Character` + `OnChanged` + stato locale.

**Files:**
- Create: `Shared/CharacterTabs/CharacterBioTab.razor`, `Shared/CharacterTabs/CharacterBioTab.razor.css`
- Modify: `Pages/Characters.razor` (sostituzione blocco `:293-360`, rimozione `notesDraft`/`isSavingNotes`/`SaveNotesAsync`)
- Modify: `Pages/Characters.razor.css` (spostamento regole `.bio-*`, `.notes-textarea`)

**Interfaces:**
- Consumes: `CharacterView` (Task 0) — non strettamente necessario qui.
- Produces: `<CharacterBioTab Character OnChanged />`. `OnChanged` invocato dopo il salvataggio note (il genitore persiste via `SaveCharacterAsync`).

- [ ] **Step 1: Creare `CharacterBioTab.razor`**

Markup = righe `:295-359` di `Characters.razor` (il contenuto dentro `@if (activeTab == "bio")`), con `selected` → `Character`. `@code`:

```razor
@code {
    [Parameter, EditorRequired] public Character Character { get; set; } = default!;
    [Parameter] public EventCallback OnChanged { get; set; }

    private string notesDraft = string.Empty;
    private bool isSavingNotes;
    private string? _lastId;

    // Re-inizializza il draft quando cambia il PG mostrato (oggi lo fa il genitore alla selezione).
    protected override void OnParametersSet()
    {
        if (Character.Id != _lastId)
        {
            _lastId = Character.Id;
            notesDraft = Character.Notes ?? string.Empty;
        }
    }

    private async Task SaveNotesAsync()
    {
        isSavingNotes = true;
        Character.Notes = notesDraft;
        await OnChanged.InvokeAsync();   // il genitore persiste + toast
        isSavingNotes = false;
    }
}
```

> Refinement sullo spec: Bio usa `OnChanged` (non un `OnSaveNotes` dedicato) — stesso effetto, pattern unico. Il bottone "Salva note" e la sua disabilitazione restano guidati da `isSavingNotes`/confronto `notesDraft`.

- [ ] **Step 2: Creare `CharacterBioTab.razor.css`**

Tagliare da `Pages/Characters.razor.css` le regole dei selettori usati dal blocco Bio (`.bio-blocks`, `.bio-row`, `.bio-label`, `.bio-value`, `.bio-block`, `.bio-block-title`, `.bio-block-text`, `.notes-textarea`, e il `.small-btn` se usato solo qui) e incollarle qui **invariate**.

Run (per individuarle): `grep -nE "\.bio-|\.notes-textarea" Pages/Characters.razor.css`

- [ ] **Step 3: Sostituire il blocco nel genitore**

In `Pages/Characters.razor`, il blocco `@if (activeTab == "bio") { ... }` (`:293-360`) diventa:

```razor
@if (activeTab == "bio")
{
    <CharacterBioTab Character="@selected" OnChanged="@SaveCharacterAsync" />
}
```

- [ ] **Step 4: Rimuovere i membri spostati dal `@code` del genitore**

Cancellare da `Characters.razor`: `notesDraft`, `isSavingNotes`, e il metodo `SaveNotesAsync`. (Verificare con `grep -n "notesDraft\|isSavingNotes\|SaveNotesAsync" Pages/Characters.razor` che non restino riferimenti orfani.)

- [ ] **Step 5: Build + test**

Run: `dotnet build -c Debug` → 0 errori/0 avvisi.
Run: `dotnet test Tests/DndCompanion.Tests.csproj` → 62 verdi.

- [ ] **Step 6: [UTENTE] Verifica locale** — tab Bio: i blocchi (aspetto/storia/tratti/talenti) appaiono come prima; modificare le note e "Salva note" persiste (toast); cambiare PG ricarica le note giuste.

- [ ] **Step 7: [su ok utente] Commit**

```bash
git add Shared/CharacterTabs/CharacterBioTab.razor Shared/CharacterTabs/CharacterBioTab.razor.css Pages/Characters.razor Pages/Characters.razor.css
git commit -m "refactor(characters): estrai CharacterBioTab"
```

---

### Task 2: `CharacterStatsTab`

Il più banale: bonus competenza + 6 `StatCard` (già componenti).

**Files:**
- Create: `Shared/CharacterTabs/CharacterStatsTab.razor` (+ `.razor.css`)
- Modify: `Pages/Characters.razor` (`:277-290`), `Pages/Characters.razor.css` (`.stats-header`, `.stats-grid`, `.bc-display`)

**Interfaces:**
- Consumes: `CharacterView.FormatBonus`, `CharacterCalculations`, `StatCard`.
- Produces: `<CharacterStatsTab Character CanEdit OnChanged />`.

- [ ] **Step 1: Creare `CharacterStatsTab.razor`**

Markup = righe `:279-289` con `selected` → `Character` e `@CanEdit` → `@CanEdit` (parametro). `@code`:

```razor
@code {
    [Parameter, EditorRequired] public Character Character { get; set; } = default!;
    [Parameter] public bool CanEdit { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }
}
```

I 6 `<StatCard … Character="@Character" IsEditMode="@CanEdit" OnChanged="@OnChanged" />`.

- [ ] **Step 2: Spostare il CSS** — tagliare `.stats-header`, `.stats-grid`, `.bc-display` da `Characters.razor.css` in `CharacterStatsTab.razor.css`. (`grep -nE "\.stats-header|\.stats-grid|\.bc-display" Pages/Characters.razor.css`)

- [ ] **Step 3: Sostituire nel genitore** (`:277-290`):

```razor
@if (activeTab == "stats")
{
    <CharacterStatsTab Character="@selected" CanEdit="@CanEdit" OnChanged="@SaveCharacterAsync" />
}
```

- [ ] **Step 4: Build + test** — `dotnet build -c Debug` (0/0); `dotnet test …` (62 verdi).

- [ ] **Step 5: [UTENTE] Verifica locale** — tab Stats: bonus competenza e le 6 StatCard identiche; in modifica i punteggi si salvano.

- [ ] **Step 6: [su ok utente] Commit**

```bash
git add Shared/CharacterTabs/CharacterStatsTab.razor Shared/CharacterTabs/CharacterStatsTab.razor.css Pages/Characters.razor Pages/Characters.razor.css
git commit -m "refactor(characters): estrai CharacterStatsTab"
```

---

### Task 3: `CharacterCombatTab`

PF/CA/dadi vita/tiri salvezza morte/secondari/armi/difese. Riceve `Weapons` dal genitore (inventario condiviso).

**Files:**
- Create: `Shared/CharacterTabs/CharacterCombatTab.razor` (+ `.razor.css`)
- Modify: `Pages/Characters.razor` (`:117-274` + rimozione membri), `Pages/Characters.razor.css`

**Interfaces:**
- Consumes: `CharacterView` (FormatBonus/AriaBool/OnKey), `CharacterCalculations` (GetInitiative/GetPassivePerception/GetHitDiceTotal), `InventoryItem`.
- Produces: `<CharacterCombatTab Character CanEdit OnChanged Weapons />` con `IReadOnlyList<InventoryItem> Weapons`.

- [ ] **Step 1: Creare `CharacterCombatTab.razor`**

Markup = righe `:120-273` (tutto dentro `@if (activeTab=="combat")`, **escludendo** la riga `:119` `var weapons = …` che diventa il parametro). `selected` → `Character`. `@code`:

```razor
@code {
    [Parameter, EditorRequired] public Character Character { get; set; } = default!;
    [Parameter] public bool CanEdit { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }
    [Parameter] public IReadOnlyList<InventoryItem> Weapons { get; set; } = Array.Empty<InventoryItem>();

    // Spostare verbatim dal @code di Characters.razor i membri usati SOLO da questo tab:
    //   isSavingHp; IncreaseHp/DecreaseHp; IncrementHitDice/DecrementHitDice; HitDiceTotal();
    //   ShowDeathSaves; SetDeathSaveSuccesses/SetDeathSaveFailures; ToggleHeroicInspiration;
    //   HasDefenses; SplitDefenses.
    // Ogni mutazione di un campo di Character chiama `await OnChanged.InvokeAsync()` al posto
    // dell'attuale `await SaveCharacterAsync()`. HitDiceTotal usa CharacterCalculations.GetHitDiceTotal(Character.HitDiceMax).
}
```

Nel markup, sostituire l'uso di `weapons` (la lista locale) con `Weapons` (il parametro).

- [ ] **Step 2: Spostare il CSS** — tagliare in `CharacterCombatTab.razor.css` i selettori del blocco Combat: `.combat-vitals`, `.hp-card`, `.hp-row`, `.hp-circle`, `.hp-value`, `.hp-max`, `.temp-hp`, `.ac-card`, `.ac-value`, `.hit-dice-card`, `.hit-dice-row`, `.hd-btn`, `.hit-dice-*`, `.death-saves-*`, `.ds-*`, `.secondary-stats`, `.mini-*`, `.inspiration-toggle`, `.weapon-card`, `.weapon-*`, `.defenses-badges`, `.defense-badge`, `.empty-note` (se non condiviso), `.section-header` (se non condiviso — vedi nota). (`grep` i selettori in `Characters.razor.css`.)

> Nota CSS condivisi: `.section-header` e `.empty-note` sono usati da più tab (Combat, Items, Magic). **Non spostarli** in un singolo figlio: lasciarli in `Characters.razor.css` o, se isolati lì non si applicano più ai figli, promuoverli a `app.css` (globali). Decidere alla prima occorrenza e annotare.

- [ ] **Step 3: Sostituire nel genitore** (`:117-274`):

```razor
@if (activeTab == "combat")
{
    <CharacterCombatTab Character="@selected" CanEdit="@CanEdit" OnChanged="@SaveCharacterAsync"
                        Weapons="@inventoryItems.Where(i => string.Equals(i.ItemType, \"weapon\", StringComparison.OrdinalIgnoreCase)).ToList()" />
}
```

- [ ] **Step 4: Rimuovere i membri spostati dal genitore** — quelli elencati allo Step 1 (verificare con `grep` che non siano usati altrove: se un membro è usato anche dal form/altro tab, NON rimuoverlo o spostarlo in `CharacterView`/`CharacterCalculations`).

- [ ] **Step 5: Build + test** — `dotnet build -c Debug` (0/0); `dotnet test` (62 verdi).

- [ ] **Step 6: [UTENTE] Verifica locale** — tab Combat: ±PF, dadi vita, tiri salvezza morte (compaiono a 0 PF), ispirazione, armi (dall'inventario) e difese identici; ogni modifica persiste.

- [ ] **Step 7: [su ok utente] Commit**

```bash
git add Shared/CharacterTabs/CharacterCombatTab.razor Shared/CharacterTabs/CharacterCombatTab.razor.css Pages/Characters.razor Pages/Characters.razor.css
git commit -m "refactor(characters): estrai CharacterCombatTab"
```

---

### Task 4: `CharacterItemsTab`

Inventario CRUD + denaro + sintonie. La **lista `inventoryItems` resta del genitore** (la legge anche Combat); il tab inietta il service per le mutazioni e notifica il genitore di ricaricare.

**Files:**
- Create: `Shared/CharacterTabs/CharacterItemsTab.razor` (+ `.razor.css`)
- Modify: `Pages/Characters.razor` (`:363-605` + spostamento membri), `Pages/Characters.razor.css`

**Interfaces:**
- Consumes: `SupabaseService` (iniettato), `InventoryItem`, `LoadingSpinner`.
- Produces: `<CharacterItemsTab Character CanEdit Items OnInventoryChanged />` con `IReadOnlyList<InventoryItem> Items` e `EventCallback OnInventoryChanged`.

- [ ] **Step 1: Nel genitore, esporre il reload dell'inventario**

Verificare/garantire che `Characters.razor` abbia un metodo che ricarica `inventoryItems` dal DB per il PG corrente (oggi il caricamento avviene alla selezione). Estrarlo/nominarlo `ReloadInventoryAsync()` se non già isolato, così può essere passato come callback.

- [ ] **Step 2: Creare `CharacterItemsTab.razor`**

Markup = righe `:365-604` con `selected` → `Character`, e ogni uso della lista `inventoryItems` → `Items`. `@inject SupabaseService SupabaseService` in cima. `@code`:

```razor
@code {
    [Parameter, EditorRequired] public Character Character { get; set; } = default!;
    [Parameter] public bool CanEdit { get; set; }
    [Parameter] public IReadOnlyList<InventoryItem> Items { get; set; } = Array.Empty<InventoryItem>();
    [Parameter] public EventCallback OnInventoryChanged { get; set; }

    // Spostare verbatim dal @code di Characters.razor i membri dell'inventario/denaro/sintonie:
    //   isAddingItem, newItemDraft, isSavingInventory, isLoadingInventory, expandedItemId,
    //   ItemTypeOptions, CanEditInventory, TotalInventoryWeight, FormatWeight,
    //   OpenAddItemForm, SaveNewItemAsync, CancelAddItem, ToggleItemExpand,
    //   AdjustQuantityAsync, ToggleEquippedAsync, DeleteItemAsync,
    //   isEditingMoney, FormatMoney, SaveMoney, AttunementValue, SetAttunementAsync.
    // I metodi che oggi fanno `inventoryItems.Add/...` + chiamano il service: dopo l'operazione DB,
    // invocare `await OnInventoryChanged.InvokeAsync()` (il genitore ricarica la lista) invece di mutare
    // la lista locale. `CanEditInventory` usa `CanEdit` (passato dal genitore).
    // SaveMoney/SetAttunement* mutano campi di Character: chiamano il service come oggi (o, se preferito,
    // restano via OnInventoryChanged → il genitore persiste). Mantenere il comportamento attuale.
}
```

- [ ] **Step 3: Spostare il CSS** — selettori del blocco Items in `CharacterItemsTab.razor.css`: `.inventory-header`, `.inventory-summary`, `.inventory-section`, `.inv-*`, `.qty-*`, `.money-*`, `.attunement*`, `.field*`, `.eq-icon`, `.weapon-badge`. (`grep` per individuarli; non spostare `.section-header`/`.empty-note` condivisi — vedi nota Task 3.)

- [ ] **Step 4: Sostituire nel genitore** (`:363-605`):

```razor
@if (activeTab == "items")
{
    <CharacterItemsTab Character="@selected" CanEdit="@CanEdit"
                       Items="@inventoryItems" OnInventoryChanged="@ReloadInventoryAsync" />
}
```

- [ ] **Step 5: Rimuovere dal genitore i membri spostati** (Step 2) **tranne** `inventoryItems` e `ReloadInventoryAsync` (restano: li usa anche Combat). Verificare con `grep` nessun riferimento orfano.

- [ ] **Step 6: Build + test** — `dotnet build -c Debug` (0/0); `dotnet test` (62 verdi).

- [ ] **Step 7: [UTENTE] Verifica locale** — tab Items: aggiungi/modifica quantità/equipaggia/elimina oggetti; denaro e sintonie si salvano; **e il tab Combat mostra le armi aggiornate** (dipendenza condivisa). Da non-owner i controlli di modifica spariscono.

- [ ] **Step 8: [su ok utente] Commit**

```bash
git add Shared/CharacterTabs/CharacterItemsTab.razor Shared/CharacterTabs/CharacterItemsTab.razor.css Pages/Characters.razor Pages/Characters.razor.css
git commit -m "refactor(characters): estrai CharacterItemsTab"
```

---

### Task 5: `CharacterMagicTab`

Stats incantesimo + slot (campi `Character`) + incantesimi noti (`character_spells`, privati). Riceve `AllSpells` dal genitore.

**Files:**
- Create: `Shared/CharacterTabs/CharacterMagicTab.razor` (+ `.razor.css`)
- Modify: `Pages/Characters.razor` (`:608-694` + spostamento membri), `Pages/Characters.razor.css`

**Interfaces:**
- Consumes: `SupabaseService` (iniettato), `Spell`, `CharacterSpell`, `SpellPicker`, `SpellListItem`, `CharacterView`/`CharacterCalculations`.
- Produces: `<CharacterMagicTab Character CanEdit OnChanged AllSpells />` con `IReadOnlyList<Spell> AllSpells`.

- [ ] **Step 1: Creare `CharacterMagicTab.razor`**

Markup = righe `:610-693` con `selected` → `Character`, `allSpells` → `AllSpells`. La condizione `activeTab=="magic" && SpellcastingAbility` resta nel genitore (vedi Step 3). `@inject SupabaseService SupabaseService`. `@code`:

```razor
@code {
    [Parameter, EditorRequired] public Character Character { get; set; } = default!;
    [Parameter] public bool CanEdit { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }
    [Parameter] public IReadOnlyList<Spell> AllSpells { get; set; } = Array.Empty<Spell>();

    // Spostare verbatim dal @code di Characters.razor i membri SOLO-Magic:
    //   selectedCharacterSpells, characterSpellsForDisplay, SpellAbilityShort,
    //   GetSpellSlotMax, GetSpellSlotUsed, ToggleSpellSlot,
    //   AddSpellToCharacter, TogglePrepared, RemoveSpellFromCharacter.
    // Caricare i character_spells in OnParametersSetAsync al cambio di Character.Id (oggi lo fa il genitore
    // alla selezione). ToggleSpellSlot muta campi scalari di Character → `await OnChanged.InvokeAsync()`.
    // add/remove/toggle-prepared usano il service iniettato e aggiornano la lista locale come oggi.
}
```

- [ ] **Step 2: Spostare il CSS** — selettori del blocco Magic in `CharacterMagicTab.razor.css`: `.spell-stats`, `.spell-stat-*`, `.spell-slot-*`, `.spell-list-*`, `.spell-level-*`. (`grep`; non spostare `.section-header`/`.empty-note` condivisi.)

- [ ] **Step 3: Sostituire nel genitore** (`:608-694`):

```razor
@if (activeTab == "magic" && !string.IsNullOrWhiteSpace(selected.SpellcastingAbility))
{
    <CharacterMagicTab Character="@selected" CanEdit="@CanEdit" OnChanged="@SaveCharacterAsync"
                       AllSpells="@allSpells" />
}
```

- [ ] **Step 4: Rimuovere dal genitore i membri spostati** (Step 1). `allSpells` **resta** nel genitore (catalogo caricato per il form). Verificare con `grep` nessun riferimento orfano; se `selectedCharacterSpells` era usato altrove (es. conteggi nella lista PG), valutare prima di rimuoverlo.

- [ ] **Step 5: Build + test** — `dotnet build -c Debug` (0/0); `dotnet test` (62 verdi).

- [ ] **Step 6: [UTENTE] Verifica locale** — tab Magic (su un PG incantatore): CD/bonus attacco, slot (toggle), aggiungi/rimuovi incantesimi, toggle "preparato"; tutto identico e persistente.

- [ ] **Step 7: [su ok utente] Commit**

```bash
git add Shared/CharacterTabs/CharacterMagicTab.razor Shared/CharacterTabs/CharacterMagicTab.razor.css Pages/Characters.razor Pages/Characters.razor.css
git commit -m "refactor(characters): estrai CharacterMagicTab"
```

---

### Task 6: Chiusura e documentazione

- [ ] **Step 1: Verifica finale** — `Characters.razor` ridotto (orchestratore + `ViewMode.List`/`Form` + `selected` + inventario condiviso). `dotnet build` (0/0) + `dotnet test` (62) + giro completo dei 5 tab in locale.
- [ ] **Step 2: Aggiornare il DIARIO** — paragrafo "Refactor Characters.razor (Fase 2B)": 5 tab estratti in `Shared/CharacterTabs/`, pattern `Character`+`OnChanged`, inventario condiviso del genitore.
- [ ] **Step 3: Aggiornare DA-FARE §3** — marcare avanzata l'estrazione tab; resta il **form di modifica** (follow-up) e le sotto-fasi A (repository) / C (stato auth).
- [ ] **Step 4: [su ok utente] Commit + (se deciso) deploy**

```bash
git add docs/DIARIO.md docs/DA-FARE.md
git commit -m "docs: Fase 2B — estrazione tab di Characters.razor"
```

---

## Self-Review

- **Copertura spec:** pattern §2 → Task 0-5; 5 componenti §3 → Task 1-5; inventario condiviso/§4 → Task 3 (Weapons) + Task 4 (lista nel genitore + OnInventoryChanged); character_spells privato §4 → Task 5; CSS isolato §8 → step "sposta CSS" di ogni task (+ nota sui condivisi `.section-header`/`.empty-note`); slicing §6 → ordine Task 1→5; testing §7 → build+62 test+verifica locale per task.
- **Placeholder:** i corpi di `AriaBool`/`OnKey` (Task 0) e gli elenchi "spostare verbatim i membri" sono **mosse meccaniche di codice esistente** identificato per nome/riga, non placeholder; l'esatto set di membri si conferma con `grep` durante l'estrazione (incluso nei passi).
- **Coerenza nomi/tipi:** `CharacterView.{FormatBonus,AriaBool,OnKey}`; parametri `Character`/`CanEdit`/`OnChanged`/`Weapons`/`Items`/`OnInventoryChanged`/`AllSpells` coerenti tra spec e task; `inventoryItems`+`ReloadInventoryAsync` restano nel genitore (Task 4) e alimentano `Weapons` (Task 3).
- **Rischio chiave gestito:** dipendenza Combat→inventario risolta passando `Weapons` dal genitore; CSS isolation gestita per-tab con eccezione esplicita per i selettori condivisi.
