# Import mostri nel combattimento — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) o superpowers:executing-plans per implementare task-by-task. Step in checkbox (`- [ ]`).
>
> Spec: [`../specs/2026-06-24-combat-import-monsters-design.md`](../specs/2026-06-24-combat-import-monsters-design.md).

**Goal:** Permettere al Master di importare i mostri della campagna come combattenti nel tracker iniziativa, scegliendone uno o più e con copie multiple.

**Architecture:** Logica pura testabile in `Services/CombatImport.cs` (parsing PF + mappatura `Monster`→`Combatant`); `Pages/Combat.razor` aggiunge un pannello inline "Importa mostri" che la usa e persiste via `SaveCombatStateAsync` (stesso pattern di `ImportCharactersAsync`).

**Tech Stack:** Blazor WebAssembly .NET 10, xUnit (`DndCompanion.Tests`), repository `IMonsterRepository` (già esistente).

## Global Constraints

- Solo branch `main`; **push = deploy** → build + test + **verifica locale** (`https://localhost:7076`) prima del push; **commit/push solo su ok esplicito dell'utente**.
- Build: `dotnet build -c Debug` (atteso 0 errori / 0 avvisi). Test: `dotnet test Tests/DndCompanion.Tests.csproj` (atteso: tutti verdi; 97 attuali + i nuovi).
- Niente modifiche a DB/RLS: i `Combatant` vivono nel jsonb `combatants` di `combat_state`.
- Comportamento coerente con l'import PG: `Initiative = 0`, persistenza via `SaveCombatStateAsync`, azioni **master-only**.
- `Monster.HitPoints` è **testo libero** (es. `"45 (6d8+18)"`); `Combatant.CurrentHp/MaxHp` sono `int`.

## File Structure

- `Services/CombatImport.cs` — nuovo. Helper statico puro: `ParseLeadingHp`, `FromMonster`.
- `Tests/CombatImportTests.cs` — nuovo. Unit test xUnit del sopra.
- `Pages/Combat.razor` — modifica: inject `IMonsterRepository`, stato + metodi del pannello, markup pulsante + pannello.
- `Pages/Combat.razor.css` — modifica: piccolo blocco CSS per il pannello (riusa `.add-form` e `.hp-btn`).
- `docs/DA-FARE.md`, `docs/DIARIO.md` — modifica: marcare la feature.

---

### Task 1: Logica pura `CombatImport` (TDD)

**Files:**
- Create: `Tests/CombatImportTests.cs`
- Create: `Services/CombatImport.cs`

**Interfaces:**
- Consumes: `DndCompanion.Models.Monster` (`Name`, `HitPoints`), `DndCompanion.Models.Combatant` (`Name`, `Initiative`, `CurrentHp`, `MaxHp`).
- Produces: `CombatImport.ParseLeadingHp(string?) -> int`; `CombatImport.FromMonster(Monster, int) -> IEnumerable<Combatant>`.

- [ ] **Step 1: Scrivere i test che falliscono**

Create `Tests/CombatImportTests.cs`:

```csharp
using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// Logica pura di import mostri nel combattimento (CombatImport).
public class CombatImportTests
{
    private static Monster M(string name, string hp) => new() { Name = name, HitPoints = hp };

    [Theory]
    [InlineData("45 (6d8+18)", 45)]
    [InlineData("7", 7)]
    [InlineData("  12 hp", 12)]
    [InlineData("256", 256)]
    [InlineData("", 1)]
    [InlineData(null, 1)]
    [InlineData("n/a", 1)]
    [InlineData("0", 1)] // clamp a >= 1
    public void ParseLeadingHp_extracts_first_int_or_falls_back_to_1(string? text, int expected)
        => Assert.Equal(expected, CombatImport.ParseLeadingHp(text));

    [Fact]
    public void FromMonster_single_copy_uses_plain_name_and_parsed_hp()
    {
        var result = CombatImport.FromMonster(M("Goblin", "7 (2d6)"), 1).ToList();

        var c = Assert.Single(result);
        Assert.Equal("Goblin", c.Name);
        Assert.Equal(0, c.Initiative);
        Assert.Equal(7, c.CurrentHp);
        Assert.Equal(7, c.MaxHp);
    }

    [Fact]
    public void FromMonster_multiple_copies_are_numbered_with_same_hp()
    {
        var result = CombatImport.FromMonster(M("Orco", "15 (2d8+6)"), 3).ToList();

        Assert.Equal(new[] { "Orco 1", "Orco 2", "Orco 3" }, result.Select(c => c.Name));
        Assert.All(result, c => Assert.Equal(15, c.MaxHp));
        Assert.All(result, c => Assert.Equal(15, c.CurrentHp));
    }

    [Fact]
    public void FromMonster_falls_back_to_hp_1_when_text_has_no_number()
    {
        var c = Assert.Single(CombatImport.FromMonster(M("Slime", "boh"), 1).ToList());
        Assert.Equal(1, c.CurrentHp);
        Assert.Equal(1, c.MaxHp);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FromMonster_returns_empty_for_non_positive_quantity(int qty)
        => Assert.Empty(CombatImport.FromMonster(M("Goblin", "7"), qty));

    [Fact]
    public void FromMonster_gives_each_copy_a_distinct_id()
    {
        var ids = CombatImport.FromMonster(M("Orco", "15"), 3).Select(c => c.Id).ToList();
        Assert.Equal(3, ids.Distinct().Count());
    }
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano (compilazione)**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CombatImportTests"`
Expected: FAIL di compilazione — `CombatImport` non esiste.

- [ ] **Step 3: Implementare `CombatImport`**

Create `Services/CombatImport.cs`:

```csharp
using System.Text.RegularExpressions;
using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Logica pura per importare i mostri della campagna come combattenti nel tracker iniziativa.
/// Nessuno stato/I/O: il PF di Combatant è int, mentre Monster.HitPoints è testo libero
/// (es. "45 (6d8+18)") → si estrae il primo intero.
/// </summary>
public static class CombatImport
{
    // Primo intero nel testo PF; fallback 1 (mai < 1). Es. "45 (6d8+18)" -> 45, "" -> 1, "n/a" -> 1.
    public static int ParseLeadingHp(string? hitPointsText)
    {
        if (string.IsNullOrWhiteSpace(hitPointsText)) return 1;
        var match = Regex.Match(hitPointsText, @"\d+");
        if (match.Success && int.TryParse(match.Value, out var n) && n >= 1) return n;
        return 1;
    }

    // q copie di un Combatant dal mostro: nome numerato se q>1, Initiative=0, CurrentHp=MaxHp=ParseLeadingHp.
    // q <= 0 -> sequenza vuota.
    public static IEnumerable<Combatant> FromMonster(Monster monster, int quantity)
    {
        var hp = ParseLeadingHp(monster.HitPoints);
        for (var i = 1; i <= quantity; i++)
        {
            yield return new Combatant
            {
                Name = quantity == 1 ? monster.Name : $"{monster.Name} {i}",
                Initiative = 0,
                CurrentHp = hp,
                MaxHp = hp,
            };
        }
    }
}
```

- [ ] **Step 4: Eseguire i test e verificare che passino**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CombatImportTests"`
Expected: PASS (tutti i casi).

- [ ] **Step 5: Commit**

```bash
git add Services/CombatImport.cs Tests/CombatImportTests.cs
git commit -m "feat(combat): logica pura import mostri (CombatImport) + test"
```

---

### Task 2: Pannello "Importa mostri" in `Combat.razor`

**Files:**
- Modify: `Pages/Combat.razor` (inject, `@code`, markup)
- Modify: `Pages/Combat.razor.css` (blocco pannello)

**Interfaces:**
- Consumes: `CombatImport.FromMonster`/`ParseLeadingHp` (Task 1); `IMonsterRepository.GetMonstersForCampaignAsync(string)`; metodi/campi esistenti di `Combat.razor` (`SaveCombatStateAsync`, `combatants`, `isImporting`, `errorMessage`, `CurrentUser`).
- Produces: nessuna API pubblica (solo UI).

- [ ] **Step 1: Aggiungere l'inject del repository mostri**

In `Pages/Combat.razor`, dopo `@inject ICharacterRepository CharacterRepository` aggiungere:

```razor
@inject IMonsterRepository MonsterRepository
```

- [ ] **Step 2: Aggiungere stato e metodi nel blocco `@code`**

In `Pages/Combat.razor`, subito dopo la riga `private Combatant newDraft = new() { CurrentHp = 10, MaxHp = 10 };` aggiungere i campi:

```csharp
    // ----- Import mostri -----
    private bool showMonsterImport;
    private bool monstersLoaded;
    private bool isLoadingMonsters;
    private List<Monster> campaignMonsters = new();
    private readonly Dictionary<string, int> monsterQty = new();

    private int TotalMonsterQty => monsterQty.Values.Sum();
    private int GetQty(string monsterId) => monsterQty.TryGetValue(monsterId, out var q) ? q : 0;
```

Poi, subito dopo il metodo `ImportCharactersAsync` (dopo la sua `}` di chiusura), aggiungere:

```csharp
    private async Task ToggleMonsterImportAsync()
    {
        if (!CurrentUser.IsMaster) return;
        showMonsterImport = !showMonsterImport;
        if (showMonsterImport && !monstersLoaded)
            await LoadCampaignMonstersAsync();
    }

    private async Task LoadCampaignMonstersAsync()
    {
        if (string.IsNullOrEmpty(CurrentUser.CampaignId)) return;
        isLoadingMonsters = true;
        errorMessage = null;
        try
        {
            campaignMonsters = await MonsterRepository.GetMonstersForCampaignAsync(CurrentUser.CampaignId);
            monsterQty.Clear();
            monstersLoaded = true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Errore caricamento mostri: {ex.Message}";
        }
        finally
        {
            isLoadingMonsters = false;
        }
    }

    private void AdjustQty(string monsterId, int delta)
        => monsterQty[monsterId] = Math.Max(0, GetQty(monsterId) + delta);

    private async Task AddSelectedMonstersAsync()
    {
        if (!CurrentUser.IsMaster) return;
        isImporting = true;
        errorMessage = null;
        try
        {
            foreach (var m in campaignMonsters)
            {
                var qty = GetQty(m.Id);
                if (qty > 0) combatants.AddRange(CombatImport.FromMonster(m, qty));
            }
            await SaveCombatStateAsync();
            showMonsterImport = false;
            monsterQty.Clear();
        }
        catch (Exception ex)
        {
            errorMessage = $"Errore importazione mostri: {ex.Message}";
        }
        finally
        {
            isImporting = false;
        }
    }
```

- [ ] **Step 3: Aggiungere il pulsante e il pannello nel markup**

In `Pages/Combat.razor`, dentro `<div class="master-controls">`, dopo il pulsante "Importa personaggi", aggiungere:

```razor
            <button type="button" class="ctrl-btn" @onclick="ToggleMonsterImportAsync" disabled="@isImporting">
                👹 Importa mostri
            </button>
```

Poi, subito **dopo** la chiusura `</div>` di `master-controls` (e ancora dentro il blocco `@if (isMaster)`), aggiungere il pannello:

```razor
        @if (showMonsterImport)
        {
            <div class="add-form monster-import">
                <div class="form-title">Importa mostri</div>
                @if (isLoadingMonsters)
                {
                    <LoadingSpinner Text="Caricamento mostri..." />
                }
                else if (campaignMonsters.Count == 0)
                {
                    <div class="mi-empty">Nessun mostro nella campagna.</div>
                }
                else
                {
                    @foreach (var m in campaignMonsters)
                    {
                        <div class="mi-row">
                            <span class="mi-name">@m.Name</span>
                            <span class="mi-hp">PF @CombatImport.ParseLeadingHp(m.HitPoints)</span>
                            <div class="mi-stepper">
                                <button type="button" class="hp-btn hp-minus" aria-label="Meno"
                                        @onclick="() => AdjustQty(m.Id, -1)" disabled="@(GetQty(m.Id) == 0)">−</button>
                                <span class="mi-qty">@GetQty(m.Id)</span>
                                <button type="button" class="hp-btn hp-plus" aria-label="Più"
                                        @onclick="() => AdjustQty(m.Id, 1)">+</button>
                            </div>
                        </div>
                    }
                    <button type="button" class="add-btn" @onclick="AddSelectedMonstersAsync"
                            disabled="@(TotalMonsterQty == 0 || isImporting)">
                        @(isImporting ? "Aggiunta..." : $"Aggiungi {TotalMonsterQty} combattenti")
                    </button>
                }
            </div>
        }
```

- [ ] **Step 4: Aggiungere il CSS del pannello**

In coda a `Pages/Combat.razor.css` aggiungere:

```css
/* ===== Import mostri ===== */
.monster-import {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
}

.mi-row {
    display: flex;
    align-items: center;
    gap: 0.6rem;
}

.mi-name {
    flex: 1;
    min-width: 0;
    color: var(--gold-light);
    font-weight: 600;
}

.mi-hp {
    color: #9a8c6a;
    font-size: 0.85rem;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
}

.mi-stepper {
    display: flex;
    align-items: center;
    gap: 0.35rem;
    flex-shrink: 0;
}

.mi-qty {
    min-width: 1.5rem;
    text-align: center;
    font-weight: 700;
    color: var(--gold-light);
    font-variant-numeric: tabular-nums;
}

.mi-empty {
    color: #7a6b4d;
    font-style: italic;
    font-size: 0.9rem;
    padding: 0.5rem 0;
}
```

- [ ] **Step 5: Build + test**

Run: `dotnet build -c Debug`
Expected: 0 errori / 0 avvisi.

Run: `dotnet test Tests/DndCompanion.Tests.csproj`
Expected: tutti verdi (97 + i nuovi del Task 1).

- [ ] **Step 6: [UTENTE] Verifica locale**

Su `https://localhost:7076` come Master con ≥1 mostro nella campagna: Combat → "👹 Importa mostri" → il pannello elenca i mostri con PF; imposta quantità (incl. >1 di uno stesso) → "Aggiungi N combattenti" → i combattenti compaiono (nomi numerati per le copie, PF corretti, iniziativa 0) → ritocca iniziativa/PF inline → da un secondo client (giocatore) vedere lo stato persistito. Caso vuoto: campagna senza mostri → messaggio "Nessun mostro nella campagna".

- [ ] **Step 7: [su ok utente] Commit**

```bash
git add Pages/Combat.razor Pages/Combat.razor.css
git commit -m "feat(combat): pannello import mostri nel tracker iniziativa"
```

---

### Task 3: Documentazione

**Files:**
- Modify: `docs/DA-FARE.md` (§8: marcare ✅ "Import mostri nel combattimento")
- Modify: `docs/DIARIO.md` (riga sulla feature)

- [ ] **Step 1: DA-FARE §8** — cambiare il bullet "🟡 **Import mostri nel combattimento.**" in "✅ … — FATTO (2026-06-24)" con una riga sul risultato (pannello inline, `CombatImport` testato, PF dal primo intero).

- [ ] **Step 2: DIARIO** — aggiungere un breve paragrafo "Import mostri nel combattimento (2026-06-24)" con: helper puro `CombatImport` + test, pannello inline master-only in `Combat.razor`, PF dal primo intero del testo, persistenza via `SaveCombatStateAsync`.

- [ ] **Step 3: [su ok utente] Commit**

```bash
git add docs/DA-FARE.md docs/DIARIO.md
git commit -m "docs: import mostri nel combattimento completato"
```

---

## Self-Review

- **Copertura spec:** §3 logica pura → Task 1; §4 UI → Task 2 (Step 1-4); §5 errori/edge → Task 2 (try/catch + empty/disabled); §6 test → Task 1; §7 verifica locale → Task 2 Step 6; §2 decisioni (PF primo intero, nome numerato, iniziativa 0, inline) → Task 1 + Task 2. Coperto.
- **Placeholder:** nessuno; tutto il codice è esplicito.
- **Coerenza tipi/nomi:** `CombatImport.ParseLeadingHp(string?)`/`FromMonster(Monster,int)` usati identici in Task 1 e Task 2; `Combatant`/`Monster` campi reali; `SaveCombatStateAsync`/`combatants`/`isImporting`/`errorMessage`/`CurrentUser` già presenti in `Combat.razor`; `IMonsterRepository.GetMonstersForCampaignAsync` reale.
- **Rischio chiave:** `Monster.HitPoints` con sola formula senza valore (es. `"6d8+18"`) → `ParseLeadingHp` ritorna 6 (primo intero); accettato per spec (il Master corregge inline). CSS stepper riusa `.hp-btn` (già a tema).
