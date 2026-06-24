# Wizard di creazione scheda PG — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere un wizard guidato di *sola creazione* per i personaggi, che applichi i bonus derivati puliti (razza → caratteristiche, classe → dado vita) e suggerisca PF e tiri salvezza, lasciando l'accordion attuale per la modifica.

**Architecture:** Approccio ibrido. La logica D&D vive in helper puri statici testabili (`Services/CharacterWizardLogic.cs`, stesso pattern di `CharacterCalculations`). La UI è un unico componente `Shared/CharacterTabs/CharacterWizard.razor` (tutti gli step in un file, per condividere un solo foglio di stile scoped — vedi Global Constraints sul CSS). `Pages/Characters.razor` guadagna un `ViewMode.Wizard` e riusa integralmente `SaveFormAsync`/`CancelForm` esistenti: nessuna nuova logica di persistenza.

**Tech Stack:** Blazor WebAssembly .NET 10, xUnit, Supabase (postgrest-csharp). Niente nuove dipendenze.

**Spec di riferimento:** `docs/superpowers/specs/2026-06-24-wizard-scheda-pg-design.md`.

## Global Constraints

- **Solo `main`; nessun push.** L'utente verifica in locale su `https://localhost:7076` e pusha lui. Gli step "commit" sono commit **locali**.
- **Zero modifiche a schema DB / RLS.** Il wizard non introduce colonne né policy.
- **CSS isolato per componente.** In questo codebase le classi di form (`.input`, `.primary-btn`, `.field`, `.score-grid`, `.skill-row`, …) sono **scoped** dentro `CharacterEditForm.razor.css`, NON globali. Solo i **design token** in `wwwroot/css/app.css` (`:root` → `--gold`, `--bg`, `--bg-card`, `--gold-light`, `--gold-dim`, …) sono globali. Conseguenza vincolante: (a) **non** estrarre markup condiviso dall'accordion (romperebbe il suo stile, cfr. memoria `blazor-css-isolation-extraction`); (b) il wizard deve avere il **proprio** `CharacterWizard.razor.css` con le classi che gli servono, usando i token globali.
- **Niente bUnit.** I componenti `.razor` non hanno unit test (coerente con la suite attuale): la rete è build pulita + test puri sugli helper + verifica a vista in locale.
- **Build pulita obbligatoria:** ogni task UI deve chiudere con `dotnet build DndCompanion.csproj` a **0 warning / 0 errori**.
- **Lingua:** UI e commenti in italiano, con accenti corretti.
- **Ordine caratteristiche** ovunque: FOR, DES, COS, INT, SAG, CAR (`Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma`).

**Comandi ricorrenti:**
- Test (tutti): `dotnet test Tests/DndCompanion.Tests.csproj`
- Test (un singolo): `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterWizardLogicTests"`
- Build app: `dotnet build DndCompanion.csproj`

---

## Task 1: `CharacterWizardLogic.FinalAbilityScores`

**Files:**
- Create: `Services/CharacterWizardLogic.cs`
- Test: `Tests/CharacterWizardLogicTests.cs`

**Interfaces:**
- Consumes: `DndCompanion.Models.Race` (campi `StrBonus/DexBonus/ConBonus/IntBonus/WisBonus/ChaBonus`, int).
- Produces: `public static int[] CharacterWizardLogic.FinalAbilityScores(int[] baseScores, Race? race)` — 6 finali (ordine FOR…CAR), clamp 1–30; `race` null → base clampati; `baseScores` più corto → mancanti trattati come 10.

- [ ] **Step 1: Scrivere il test che fallisce**

```csharp
using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

public class CharacterWizardLogicTests
{
    // ===== FinalAbilityScores =====

    [Fact]
    public void FinalAbilityScores_with_null_race_returns_base_unchanged()
    {
        var result = CharacterWizardLogic.FinalAbilityScores(new[] { 10, 12, 14, 8, 15, 13 }, null);
        Assert.Equal(new[] { 10, 12, 14, 8, 15, 13 }, result);
    }

    [Fact]
    public void FinalAbilityScores_adds_race_bonuses_in_order()
    {
        var race = new Race { StrBonus = 2, ConBonus = 1, ChaBonus = 1 };
        var result = CharacterWizardLogic.FinalAbilityScores(new[] { 10, 10, 10, 10, 10, 10 }, race);
        Assert.Equal(new[] { 12, 10, 11, 10, 10, 11 }, result);
    }

    [Fact]
    public void FinalAbilityScores_clamps_to_30()
    {
        var race = new Race { StrBonus = 5 };
        var result = CharacterWizardLogic.FinalAbilityScores(new[] { 29, 10, 10, 10, 10, 10 }, race);
        Assert.Equal(30, result[0]);
    }

    [Fact]
    public void FinalAbilityScores_clamps_to_1()
    {
        var race = new Race { StrBonus = -5 };
        var result = CharacterWizardLogic.FinalAbilityScores(new[] { 3, 10, 10, 10, 10, 10 }, race);
        Assert.Equal(1, result[0]);
    }

    [Fact]
    public void FinalAbilityScores_short_array_treats_missing_as_10()
    {
        var result = CharacterWizardLogic.FinalAbilityScores(new[] { 15 }, null);
        Assert.Equal(new[] { 15, 10, 10, 10, 10, 10 }, result);
    }
}
```

- [ ] **Step 2: Eseguire il test e verificare che fallisca**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterWizardLogicTests.FinalAbilityScores"`
Expected: FAIL in compilazione ("CharacterWizardLogic does not exist").

- [ ] **Step 3: Scrivere l'implementazione minima**

```csharp
using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Helper di sole funzioni pure per il wizard di creazione PG: applicazione bonus razza,
/// costruzione dado vita, suggerimento PF e tiri salvezza. Nessuno stato, nessuna I/O.
/// Stesso pattern di <see cref="CharacterCalculations"/>.
/// </summary>
public static class CharacterWizardLogic
{
    /// <summary>Finali = base + bonus razza (ordine FOR,DES,COS,INT,SAG,CAR), clamp 1..30.
    /// race null → base clampati; baseScores più corto → mancanti = 10.</summary>
    public static int[] FinalAbilityScores(int[] baseScores, Race? race)
    {
        var bonuses = race is null
            ? new[] { 0, 0, 0, 0, 0, 0 }
            : new[] { race.StrBonus, race.DexBonus, race.ConBonus, race.IntBonus, race.WisBonus, race.ChaBonus };

        var result = new int[6];
        for (var i = 0; i < 6; i++)
        {
            var b = baseScores is not null && i < baseScores.Length ? baseScores[i] : 10;
            result[i] = Math.Clamp(b + bonuses[i], 1, 30);
        }
        return result;
    }
}
```

- [ ] **Step 4: Eseguire il test e verificare che passi**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterWizardLogicTests.FinalAbilityScores"`
Expected: PASS (5 test).

- [ ] **Step 5: Commit**

```bash
git add Services/CharacterWizardLogic.cs Tests/CharacterWizardLogicTests.cs
git commit -m "feat(wizard): helper FinalAbilityScores + test"
```

---

## Task 2: `CharacterWizardLogic.BuildHitDice` (+ `ParseDieSize` privato)

**Files:**
- Modify: `Services/CharacterWizardLogic.cs`
- Test: `Tests/CharacterWizardLogicTests.cs`

**Interfaces:**
- Produces: `public static string BuildHitDice(string? classHitDie, int level)` — `"d12"`+3 → `"3d12"`; dado non riconosciuto/vuoto → `""`; `level < 1` trattato come 1. Più un `private static int? ParseDieSize(string?)` (estrae la dimensione del dado dopo `d`/`D`), riusato dal Task 3.

- [ ] **Step 1: Scrivere il test che fallisce**

Aggiungere in `CharacterWizardLogicTests`:

```csharp
    // ===== BuildHitDice =====

    [Theory]
    [InlineData("d12", 3, "3d12")]
    [InlineData("D8", 1, "1d8")]
    [InlineData("1d6", 5, "5d6")]
    [InlineData("d10", 0, "1d10")]   // livello < 1 trattato come 1
    public void BuildHitDice_builds_expected(string die, int level, string expected)
        => Assert.Equal(expected, CharacterWizardLogic.BuildHitDice(die, level));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("custom")]   // niente 'd' → non riconosciuto
    public void BuildHitDice_unrecognized_returns_empty(string? die)
        => Assert.Equal("", CharacterWizardLogic.BuildHitDice(die, 3));
```

- [ ] **Step 2: Eseguire il test e verificare che fallisca**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterWizardLogicTests.BuildHitDice"`
Expected: FAIL in compilazione ("BuildHitDice does not exist").

- [ ] **Step 3: Scrivere l'implementazione**

Aggiungere dentro la classe `CharacterWizardLogic`:

```csharp
    /// <summary>"d12" + livello 3 → "3d12". Dado vuoto/non riconosciuto → "". livello &lt; 1 trattato 1.</summary>
    public static string BuildHitDice(string? classHitDie, int level)
    {
        var die = ParseDieSize(classHitDie);
        if (die is null) return string.Empty;
        var lvl = level < 1 ? 1 : level;
        return $"{lvl}d{die.Value}";
    }

    /// <summary>Dimensione del dado dopo la prima 'd'/'D' (es. "d12"/"1d6" → 12/6). null se assente o non parsabile.</summary>
    private static int? ParseDieSize(string? hitDie)
    {
        if (string.IsNullOrWhiteSpace(hitDie)) return null;
        var lower = hitDie.ToLowerInvariant();
        var idx = lower.IndexOf('d');
        if (idx < 0 || idx + 1 >= lower.Length) return null;
        var digits = new string(lower.Skip(idx + 1).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) && n > 0 ? n : (int?)null;
    }
```

- [ ] **Step 4: Eseguire il test e verificare che passi**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterWizardLogicTests.BuildHitDice"`
Expected: PASS (7 casi).

- [ ] **Step 5: Commit**

```bash
git add Services/CharacterWizardLogic.cs Tests/CharacterWizardLogicTests.cs
git commit -m "feat(wizard): helper BuildHitDice + ParseDieSize + test"
```

---

## Task 3: `CharacterWizardLogic.SuggestMaxHp`

**Files:**
- Modify: `Services/CharacterWizardLogic.cs`
- Test: `Tests/CharacterWizardLogicTests.cs`

**Interfaces:**
- Consumes: `ParseDieSize` (privato, Task 2).
- Produces: `public static int SuggestMaxHp(string? classHitDie, int conModifier, int level)` — metodo medio 5e; dado non riconosciuto → **0** (sentinella: la UI nasconde "Usa suggerito"); minimo 1 quando il dado è valido.

- [ ] **Step 1: Scrivere il test che fallisce**

Aggiungere in `CharacterWizardLogicTests`:

```csharp
    // ===== SuggestMaxHp =====

    [Fact]
    public void SuggestMaxHp_level1_is_full_die_plus_con()
        => Assert.Equal(14, CharacterWizardLogic.SuggestMaxHp("d12", 2, 1)); // 12 + 2

    [Fact]
    public void SuggestMaxHp_multilevel_uses_rounded_up_average()
        // liv1: 12+1 ; liv2,3: (7)+1 ciascuno → 13 + 8 + 8 = 29
        => Assert.Equal(29, CharacterWizardLogic.SuggestMaxHp("d12", 1, 3));

    [Fact]
    public void SuggestMaxHp_floors_at_1()
        => Assert.Equal(1, CharacterWizardLogic.SuggestMaxHp("d6", -5, 1)); // 6-5=1

    [Fact]
    public void SuggestMaxHp_negative_total_floored_to_1()
        => Assert.Equal(1, CharacterWizardLogic.SuggestMaxHp("d4", -10, 1)); // 4-10 → 1

    [Fact]
    public void SuggestMaxHp_unrecognized_die_returns_0()
        => Assert.Equal(0, CharacterWizardLogic.SuggestMaxHp("custom", 2, 3));
```

- [ ] **Step 2: Eseguire il test e verificare che fallisca**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterWizardLogicTests.SuggestMaxHp"`
Expected: FAIL in compilazione ("SuggestMaxHp does not exist").

- [ ] **Step 3: Scrivere l'implementazione**

Aggiungere dentro la classe `CharacterWizardLogic`:

```csharp
    /// <summary>PF suggeriti (metodo medio 5e): liv1 = dado pieno + modCOS; ogni livello oltre += media
    /// del dado (arrotondata per eccesso) + modCOS. Minimo 1. Dado non riconosciuto → 0 (sentinella).</summary>
    public static int SuggestMaxHp(string? classHitDie, int conModifier, int level)
    {
        var die = ParseDieSize(classHitDie);
        if (die is null) return 0;
        var lvl = level < 1 ? 1 : level;
        var avgPerLevel = (die.Value / 2) + 1; // media di un dN arrotondata per eccesso
        var hp = die.Value + conModifier;
        for (var i = 2; i <= lvl; i++)
            hp += avgPerLevel + conModifier;
        return Math.Max(1, hp);
    }
```

- [ ] **Step 4: Eseguire il test e verificare che passi**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterWizardLogicTests.SuggestMaxHp"`
Expected: PASS (5 test).

- [ ] **Step 5: Commit**

```bash
git add Services/CharacterWizardLogic.cs Tests/CharacterWizardLogicTests.cs
git commit -m "feat(wizard): helper SuggestMaxHp + test"
```

---

## Task 4: `CharacterWizardLogic.ParseSaveProficiencies`

**Files:**
- Modify: `Services/CharacterWizardLogic.cs`
- Test: `Tests/CharacterWizardLogicTests.cs`

**Interfaces:**
- Produces: `public static IReadOnlyList<string> ParseSaveProficiencies(string? savingThrowsText)` — testo libero italiano → chiavi inglesi (`"strength"`,`"dexterity"`,`"constitution"`,`"intelligence"`,`"wisdom"`,`"charisma"`); tollerante a maiuscole/spazi; voci ignote scartate; nessun duplicato; vuoto/null → lista vuota. Le chiavi servono alla UI per spuntare `Draft.ProfSave*` (Task 9).

- [ ] **Step 1: Scrivere il test che fallisce**

Aggiungere in `CharacterWizardLogicTests`:

```csharp
    // ===== ParseSaveProficiencies =====

    [Fact]
    public void ParseSaveProficiencies_maps_two_abilities()
        => Assert.Equal(new[] { "strength", "constitution" },
                        CharacterWizardLogic.ParseSaveProficiencies("Forza, Costituzione"));

    [Fact]
    public void ParseSaveProficiencies_is_case_and_space_insensitive()
        => Assert.Equal(new[] { "strength", "constitution" },
                        CharacterWizardLogic.ParseSaveProficiencies("  FORZA , costituzione "));

    [Fact]
    public void ParseSaveProficiencies_drops_unknown_tokens()
        => Assert.Equal(new[] { "wisdom" },
                        CharacterWizardLogic.ParseSaveProficiencies("Pippo, Saggezza"));

    [Fact]
    public void ParseSaveProficiencies_dedupes()
        => Assert.Equal(new[] { "strength" },
                        CharacterWizardLogic.ParseSaveProficiencies("Forza, Forza"));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ParseSaveProficiencies_empty_returns_empty(string? text)
        => Assert.Empty(CharacterWizardLogic.ParseSaveProficiencies(text));

    [Fact]
    public void ParseSaveProficiencies_maps_all_six()
        => Assert.Equal(
            new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" },
            CharacterWizardLogic.ParseSaveProficiencies("Forza, Destrezza, Costituzione, Intelligenza, Saggezza, Carisma"));
```

- [ ] **Step 2: Eseguire il test e verificare che fallisca**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterWizardLogicTests.ParseSaveProficiencies"`
Expected: FAIL in compilazione ("ParseSaveProficiencies does not exist").

- [ ] **Step 3: Scrivere l'implementazione**

Aggiungere dentro la classe `CharacterWizardLogic`:

```csharp
    /// <summary>Testo libero dei tiri salvezza (es. "Forza, Costituzione") → chiavi caratteristica inglesi.
    /// Tollerante a maiuscole/spazi; voci ignote scartate; nessun duplicato; vuoto/null → lista vuota.</summary>
    public static IReadOnlyList<string> ParseSaveProficiencies(string? savingThrowsText)
    {
        if (string.IsNullOrWhiteSpace(savingThrowsText)) return Array.Empty<string>();

        var result = new List<string>();
        foreach (var raw in savingThrowsText.Split(','))
        {
            var key = raw.Trim().ToLowerInvariant() switch
            {
                "forza" => "strength",
                "destrezza" => "dexterity",
                "costituzione" => "constitution",
                "intelligenza" => "intelligence",
                "saggezza" => "wisdom",
                "carisma" => "charisma",
                _ => null
            };
            if (key is not null && !result.Contains(key)) result.Add(key);
        }
        return result;
    }
```

- [ ] **Step 4: Eseguire il test e verificare che passi**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterWizardLogicTests"`
Expected: PASS (tutta la suite del file; ~24 casi).

- [ ] **Step 5: Commit**

```bash
git add Services/CharacterWizardLogic.cs Tests/CharacterWizardLogicTests.cs
git commit -m "feat(wizard): helper ParseSaveProficiencies + test"
```

---

## Task 5: Punto d'ingresso + scaffold del wizard (navigazione)

**Files:**
- Create: `Shared/CharacterTabs/CharacterWizard.razor`
- Modify: `Pages/Characters.razor` (switch `ViewMode`, `OpenCreateForm`)

**Interfaces:**
- Consumes: `Character`, `CharacterClass`, `Race`; helper `FinalAbilityScores` (Task 1).
- Produces (membri `@code` del wizard usati dai task step successivi):
  - `int currentStep` (0–5), `const int TotalSteps = 6`;
  - `int[] baseScores` (6 elementi);
  - `bool isClassCustom`, `bool isRaceCustom`; `const string CustomOptionValue = "__custom__"`;
  - `Race? SelectedRace`, `CharacterClass? SelectedClass` (proprietà derivate);
  - `void RecalcFinals()` (scrive `Draft.Strength…Charisma` = `FinalAbilityScores(baseScores, SelectedRace)`);
  - `void GoToStep(int)`, `void Next()`, `void Prev()`.

- [ ] **Step 1: Creare il componente wizard (scaffold con navigazione, step segnaposto)**

Creare `Shared/CharacterTabs/CharacterWizard.razor`:

```razor
@using DndCompanion.Models

<div class="wizard-view">
    <div class="wizard-top">
        <button type="button" class="wiz-cancel" @onclick="@(() => OnCancel.InvokeAsync())">← Annulla</button>
    </div>

    <h2 class="wizard-title">Nuovo Personaggio</h2>

    <div class="wizard-steps">
        @for (var i = 0; i < TotalSteps; i++)
        {
            var idx = i;
            <button type="button"
                    class="wiz-step-dot @(idx == currentStep ? "active" : "") @(idx < currentStep ? "done" : "")"
                    @onclick="() => GoToStep(idx)"
                    aria-label="@($"Vai al passo {idx + 1}: {StepTitles[idx]}")">
                @(idx + 1)
            </button>
        }
    </div>
    <p class="wiz-step-caption">Passo @(currentStep + 1) di @TotalSteps — @StepTitles[currentStep]</p>

    <div class="wizard-body">
        @switch (currentStep)
        {
            case 0: <p class="wiz-placeholder">Identità</p> break;
            case 1: <p class="wiz-placeholder">Caratteristiche</p> break;
            case 2: <p class="wiz-placeholder">Vitalità &amp; combattimento</p> break;
            case 3: <p class="wiz-placeholder">Competenze</p> break;
            case 4: <p class="wiz-placeholder">Incantesimi</p> break;
            case 5: <p class="wiz-placeholder">Riepilogo</p> break;
        }
    </div>

    <div class="wizard-nav">
        @if (currentStep > 0)
        {
            <button type="button" class="secondary-btn" @onclick="Prev" disabled="@IsBusy">← Indietro</button>
        }
        @if (currentStep < TotalSteps - 1)
        {
            <button type="button" class="primary-btn" @onclick="Next">Avanti →</button>
        }
        else
        {
            <button type="button" class="primary-btn" @onclick="@(() => OnSave.InvokeAsync())" disabled="@IsBusy">
                @(IsBusy ? "Salvataggio..." : "Salva personaggio")
            </button>
        }
    </div>
</div>

@code {
    [Parameter, EditorRequired] public Character Draft { get; set; } = default!;
    [Parameter] public List<CharacterClass> Classes { get; set; } = new();
    [Parameter] public List<Race> Races { get; set; } = new();
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback OnSave { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private const int TotalSteps = 6;
    private const string CustomOptionValue = "__custom__";

    private static readonly string[] StepTitles =
        { "Identità", "Caratteristiche", "Vitalità & combattimento", "Competenze", "Incantesimi", "Riepilogo" };

    private int currentStep;
    private int[] baseScores = { 10, 10, 10, 10, 10, 10 };
    private bool isClassCustom;
    private bool isRaceCustom;
    private Character? _lastDraft;

    // Reset a ogni apertura: il genitore passa un nuovo editDraft (nuovo riferimento) per ogni creazione.
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Draft, _lastDraft)) return;
        _lastDraft = Draft;

        currentStep = 0;
        // In creazione il Draft parte con caratteristiche a 10: sono i "base" del wizard.
        baseScores = new[]
        {
            Draft.Strength, Draft.Dexterity, Draft.Constitution,
            Draft.Intelligence, Draft.Wisdom, Draft.Charisma
        };
        isClassCustom = !string.IsNullOrEmpty(Draft.Class)
            && Classes.All(c => !string.Equals(c.Name, Draft.Class, StringComparison.Ordinal));
        isRaceCustom = !string.IsNullOrEmpty(Draft.Race)
            && Races.All(r => !string.Equals(r.Name, Draft.Race, StringComparison.Ordinal));
    }

    private Race? SelectedRace =>
        (!isRaceCustom && !string.IsNullOrEmpty(Draft.Race))
            ? Races.FirstOrDefault(r => r.Name == Draft.Race)
            : null;

    private CharacterClass? SelectedClass =>
        (!isClassCustom && !string.IsNullOrEmpty(Draft.Class))
            ? Classes.FirstOrDefault(c => c.Name == Draft.Class)
            : null;

    // Riscrive i finali del Draft dai base + bonus razza. Chiamato a ogni cambio di base o razza.
    private void RecalcFinals()
    {
        var f = CharacterWizardLogic.FinalAbilityScores(baseScores, SelectedRace);
        Draft.Strength = f[0];
        Draft.Dexterity = f[1];
        Draft.Constitution = f[2];
        Draft.Intelligence = f[3];
        Draft.Wisdom = f[4];
        Draft.Charisma = f[5];
    }

    private void GoToStep(int step) => currentStep = Math.Clamp(step, 0, TotalSteps - 1);
    private void Next() => GoToStep(currentStep + 1);
    private void Prev() => GoToStep(currentStep - 1);
}
```

- [ ] **Step 2: Collegare il punto d'ingresso in `Pages/Characters.razor`**

In `Pages/Characters.razor`, aggiungere il nuovo valore all'enum (riga ~173):

```csharp
    private enum ViewMode { Loading, Empty, List, Detail, Form, Wizard }
```

Aggiungere il `case` di rendering nello `@switch (mode)` **subito dopo** il blocco `case ViewMode.Form:` (riga ~157-160):

```razor
        case ViewMode.Wizard:
            <CharacterWizard Draft="@editDraft" Classes="@classes" Races="@races"
                             IsBusy="@isBusy" OnSave="@SaveFormAsync" OnCancel="@CancelForm" />
            break;
```

In `OpenCreateForm()` (riga ~330), cambiare **solo** l'ultima riga da `mode = ViewMode.Form;` a:

```csharp
        mode = ViewMode.Wizard;
    }
```

(Il corpo che pre-popola `editDraft` resta identico: `OwnerId`, `CampaignId`, `Level = 1`, `ArmorClass = 10`, `HitPoints/MaxHitPoints = 10`, caratteristiche a 10.)

Aggiornare la guardia del FAB e dei rami `CancelForm`/`SaveFormAsync` **NON** serve: `SaveFormAsync` apre la detail su successo, `CancelForm` torna a List/Empty già correttamente (non distingue Form da Wizard).

- [ ] **Step 3: Build pulita**

Run: `dotnet build DndCompanion.csproj`
Expected: 0 warning, 0 errori.

- [ ] **Step 4: Verifica a vista (locale)**

Su `https://localhost:7076`, in una campagna attiva: il "+" (o "Crea Personaggio") apre il wizard "Nuovo Personaggio"; i pallini 1–6 e "Avanti/Indietro" navigano tra i 6 segnaposto; "Annulla" torna alla lista/empty; allo step 6 "Salva personaggio" mostra il toast "Il nome è obbligatorio" (prova che `OnSave`→`SaveFormAsync` è collegato). La modifica di un PG esistente (✎) apre ancora l'accordion.

- [ ] **Step 5: Commit**

```bash
git add Shared/CharacterTabs/CharacterWizard.razor Pages/Characters.razor
git commit -m "feat(wizard): scaffold componente + entry point ViewMode.Wizard"
```

---

## Task 6: Step 1 — Identità

**Files:**
- Modify: `Shared/CharacterTabs/CharacterWizard.razor`

**Interfaces:**
- Consumes: `SelectedClass`, `SelectedRace`, `RecalcFinals`, `CustomOptionValue` (Task 5).
- Produces: handler `OnClassSelectionChanged`, `OnRaceSelectionChanged`; helper `CurrentClassSelection`, `CurrentRaceSelection`, `FormatRaceBonuses` usati anche dal Riepilogo (Task 11).

- [ ] **Step 1: Sostituire il segnaposto dello step 0 con il markup Identità**

In `CharacterWizard.razor`, nello `@switch (currentStep)`, sostituire `case 0: ... break;` con:

```razor
            case 0:
                <div class="wiz-step">
                    <div class="field">
                        <label>Nome <span class="req">*</span></label>
                        <input type="text" class="input" maxlength="64" @bind="Draft.Name" />
                    </div>

                    <div class="field">
                        <label>Classe</label>
                        <select class="input" value="@CurrentClassSelection" @onchange="OnClassSelectionChanged">
                            <option value="">-- Seleziona --</option>
                            @foreach (var c in Classes.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                            {
                                <option value="@c.Name">@c.Name</option>
                            }
                            <option value="@CustomOptionValue">Altro (testo libero)</option>
                        </select>
                        @if (isClassCustom)
                        {
                            <input type="text" class="input custom-input" maxlength="40"
                                   placeholder="Scrivi la classe" @bind="Draft.Class" @bind:event="oninput" />
                        }
                        @if (SelectedClass is not null)
                        {
                            <div class="info-summary">
                                <strong>Dado vita:</strong> @(string.IsNullOrEmpty(SelectedClass.HitDie) ? "—" : SelectedClass.HitDie)
                                @if (!string.IsNullOrWhiteSpace(SelectedClass.PrimaryAbility))
                                {
                                    <span> · <strong>Abilità primaria:</strong> @SelectedClass.PrimaryAbility</span>
                                }
                            </div>
                        }
                    </div>

                    <div class="field">
                        <label>Sottoclasse</label>
                        <input type="text" class="input" maxlength="60"
                               placeholder="es. Berserker, Campione..." @bind="Draft.Subclass" />
                    </div>

                    <div class="field">
                        <label>Razza</label>
                        <select class="input" value="@CurrentRaceSelection" @onchange="OnRaceSelectionChanged">
                            <option value="">-- Seleziona --</option>
                            @foreach (var r in Races.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
                            {
                                <option value="@r.Name">@r.Name</option>
                            }
                            <option value="@CustomOptionValue">Altro (testo libero)</option>
                        </select>
                        @if (isRaceCustom)
                        {
                            <input type="text" class="input custom-input" maxlength="40"
                                   placeholder="Scrivi la razza" @bind="Draft.Race" @bind:event="oninput" />
                        }
                        @if (SelectedRace is not null)
                        {
                            <div class="info-summary">
                                <strong>Bonus razziali:</strong> @FormatRaceBonuses(SelectedRace)
                                · <strong>Velocità:</strong> @SelectedRace.Speed
                            </div>
                        }
                    </div>

                    <div class="field">
                        <label>Livello</label>
                        <input type="number" class="input" min="1" max="20" @bind="Draft.Level" />
                    </div>

                    <div class="field">
                        <label>Background</label>
                        <input type="text" class="input" maxlength="60"
                               placeholder="es. Soldato, Criminale..." @bind="Draft.Background" />
                    </div>

                    <div class="field">
                        <label>Allineamento</label>
                        <select class="input" @bind="Draft.Alignment">
                            <option value="">-- Nessuno --</option>
                            <option value="Legale Buono">Legale Buono</option>
                            <option value="Neutrale Buono">Neutrale Buono</option>
                            <option value="Caotico Buono">Caotico Buono</option>
                            <option value="Legale Neutrale">Legale Neutrale</option>
                            <option value="Neutrale Puro">Neutrale Puro</option>
                            <option value="Caotico Neutrale">Caotico Neutrale</option>
                            <option value="Legale Malvagio">Legale Malvagio</option>
                            <option value="Neutrale Malvagio">Neutrale Malvagio</option>
                            <option value="Caotico Malvagio">Caotico Malvagio</option>
                        </select>
                    </div>
                </div>
                break;
```

- [ ] **Step 2: Aggiungere gli handler in `@code`**

Aggiungere in fondo al blocco `@code` del wizard:

```csharp
    private string CurrentClassSelection => isClassCustom ? CustomOptionValue : (Draft.Class ?? string.Empty);
    private string CurrentRaceSelection => isRaceCustom ? CustomOptionValue : (Draft.Race ?? string.Empty);

    private void OnClassSelectionChanged(ChangeEventArgs e)
    {
        var val = e.Value?.ToString() ?? string.Empty;
        if (val == CustomOptionValue) { isClassCustom = true; Draft.Class = string.Empty; }
        else { isClassCustom = false; Draft.Class = val; }
        // La classe non tocca le caratteristiche: nessun RecalcFinals qui.
    }

    private void OnRaceSelectionChanged(ChangeEventArgs e)
    {
        var val = e.Value?.ToString() ?? string.Empty;
        if (val == CustomOptionValue) { isRaceCustom = true; Draft.Race = string.Empty; }
        else { isRaceCustom = false; Draft.Race = val; }
        RecalcFinals(); // i bonus razziali cambiano i finali
    }

    private static string FormatRaceBonuses(Race race)
    {
        var parts = new List<string>();
        void Add(int val, string label)
        {
            if (val != 0) parts.Add($"{(val > 0 ? "+" : "")}{val} {label}");
        }
        Add(race.StrBonus, "FOR");
        Add(race.DexBonus, "DES");
        Add(race.ConBonus, "COS");
        Add(race.IntBonus, "INT");
        Add(race.WisBonus, "SAG");
        Add(race.ChaBonus, "CAR");
        return parts.Count > 0 ? string.Join(", ", parts) : "nessuno";
    }
```

- [ ] **Step 3: Build pulita**

Run: `dotnet build DndCompanion.csproj`
Expected: 0 warning, 0 errori.

- [ ] **Step 4: Verifica a vista (locale)**

Step 1 mostra i campi identità; scegliendo una classe a catalogo appare il riquadro "Dado vita/Abilità primaria"; scegliendo una razza appare "Bonus razziali/Velocità"; "Altro" mostra il campo testo. Impostando un nome e andando fino allo step 6, "Salva personaggio" ora **crea** il PG e apre la sua scheda (verifica il giro completo).

- [ ] **Step 5: Commit**

```bash
git add Shared/CharacterTabs/CharacterWizard.razor
git commit -m "feat(wizard): step 1 Identità"
```

---

## Task 7: Step 2 — Caratteristiche (base + bonus razza + array standard)

**Files:**
- Modify: `Shared/CharacterTabs/CharacterWizard.razor`

**Interfaces:**
- Consumes: `baseScores`, `SelectedRace`, `RecalcFinals` (Task 5); `CharacterCalculations.GetModifier/GetProficiencyBonus` (esistenti); `FormatBonus` (da `CharacterView`, importato globalmente).
- Produces: `BaseBonus(int)`, `FinalScore(int)`, `IncBase/DecBase/SetBase`, `LoadStandardArray`, costante `AbilityShort`.

- [ ] **Step 1: Sostituire il segnaposto dello step 1 con il markup Caratteristiche**

Sostituire `case 1: ... break;` con:

```razor
            case 1:
                <div class="wiz-step">
                    <div class="derived-info">
                        Bonus competenza: <strong>@FormatBonus(CharacterCalculations.GetProficiencyBonus(Draft.Level))</strong>
                    </div>

                    <button type="button" class="secondary-btn wiz-array-btn" @onclick="LoadStandardArray">
                        Carica array standard (15,14,13,12,10,8)
                    </button>

                    <div class="wiz-ability-head">
                        <span>Caratteristica</span><span>Base</span><span>Razza</span><span>Totale</span>
                    </div>

                    @for (var i = 0; i < 6; i++)
                    {
                        var idx = i;
                        <div class="wiz-ability-row">
                            <span class="wiz-ability-name">@AbilityShort[idx]</span>
                            <div class="wiz-stepper">
                                <button type="button" class="qty-btn" @onclick="() => DecBase(idx)" aria-label="Diminuisci">−</button>
                                <input type="number" class="input wiz-base-input" min="1" max="30"
                                       value="@baseScores[idx]"
                                       @onchange="e => SetBase(idx, e)" />
                                <button type="button" class="qty-btn" @onclick="() => IncBase(idx)" aria-label="Aumenta">+</button>
                            </div>
                            <span class="wiz-ability-bonus">@FormatBonus(BaseBonus(idx))</span>
                            <span class="wiz-ability-total">@FinalScore(idx) <small>(@FormatBonus(CharacterCalculations.GetModifier(FinalScore(idx))))</small></span>
                        </div>
                    }
                    <p class="field-hint">Il totale = base + bonus razziale, limitato a 1–30. Il modificatore è tra parentesi.</p>
                </div>
                break;
```

- [ ] **Step 2: Aggiungere i membri in `@code`**

```csharp
    private static readonly string[] AbilityShort = { "FOR", "DES", "COS", "INT", "SAG", "CAR" };

    private int BaseBonus(int idx)
    {
        var race = SelectedRace;
        if (race is null) return 0;
        return idx switch
        {
            0 => race.StrBonus, 1 => race.DexBonus, 2 => race.ConBonus,
            3 => race.IntBonus, 4 => race.WisBonus, 5 => race.ChaBonus, _ => 0
        };
    }

    // Totale 1–30 della singola caratteristica (coerente con FinalAbilityScores).
    private int FinalScore(int idx) => Math.Clamp(baseScores[idx] + BaseBonus(idx), 1, 30);

    private void SetBase(int idx, ChangeEventArgs e)
    {
        var v = int.TryParse(e.Value?.ToString(), out var n) ? n : baseScores[idx];
        baseScores[idx] = Math.Clamp(v, 1, 30);
        RecalcFinals();
    }

    private void IncBase(int idx)
    {
        baseScores[idx] = Math.Clamp(baseScores[idx] + 1, 1, 30);
        RecalcFinals();
    }

    private void DecBase(int idx)
    {
        baseScores[idx] = Math.Clamp(baseScores[idx] - 1, 1, 30);
        RecalcFinals();
    }

    private void LoadStandardArray()
    {
        baseScores = new[] { 15, 14, 13, 12, 10, 8 };
        RecalcFinals();
    }
```

- [ ] **Step 3: Build pulita**

Run: `dotnet build DndCompanion.csproj`
Expected: 0 warning, 0 errori.

- [ ] **Step 4: Verifica a vista (locale)**

Con una razza che dà bonus (es. +2 FOR): step 2 mostra base 10, razza +2, totale 12 (mod +1). Gli stepper +/− e l'input cambiano base e ricalcolano il totale; "Carica array standard" mette 15/14/13/12/10/8; il clamp tiene il totale ≤ 30. Tornando allo step 1 e cambiando razza, i totali si aggiornano.

- [ ] **Step 5: Commit**

```bash
git add Shared/CharacterTabs/CharacterWizard.razor
git commit -m "feat(wizard): step 2 Caratteristiche (base + bonus razza + array standard)"
```

---

## Task 8: Step 3 — Vitalità & combattimento

**Files:**
- Modify: `Shared/CharacterTabs/CharacterWizard.razor`

**Interfaces:**
- Consumes: `SelectedClass`, `Draft` (Task 5); `BuildHitDice`/`SuggestMaxHp` (Task 2/3); `CharacterCalculations.GetModifier/GetInitiative/GetPassivePerception/GetProficiencyBonus`; `FormatBonus`.
- Produces: `SuggestedHp` (proprietà), `UseSuggestedHp`, `PrefillHitDice`.

- [ ] **Step 1: Sostituire il segnaposto dello step 2 con il markup Vitalità & combattimento**

Sostituire `case 2: ... break;` con:

```razor
            case 2:
                <div class="wiz-step">
                    <div class="field">
                        <label>Classe Armatura</label>
                        <input type="number" class="input" min="0" max="40" @bind="Draft.ArmorClass" />
                    </div>

                    <div class="field">
                        <label>PF Massimi</label>
                        <div class="wiz-hp-row">
                            <input type="number" class="input" min="1" @bind="Draft.MaxHitPoints" />
                            @if (SuggestedHp > 0)
                            {
                                <button type="button" class="secondary-btn wiz-suggest-btn" @onclick="UseSuggestedHp">
                                    Usa suggerito (@SuggestedHp)
                                </button>
                            }
                        </div>
                        <span class="field-hint">I PF correnti vengono impostati uguali ai massimi alla creazione.</span>
                    </div>

                    <div class="field">
                        <label>Velocità (m)</label>
                        <input type="number" class="input" min="0" @bind="Draft.Speed" />
                        <span class="field-hint">9m = 30 piedi (umano standard). La velocità della razza è solo informativa.</span>
                    </div>

                    <div class="field">
                        <label>Taglia</label>
                        <select class="input" @bind="Draft.Size">
                            <option value="Minuscola">Minuscola</option>
                            <option value="Piccola">Piccola</option>
                            <option value="Media">Media</option>
                            <option value="Grande">Grande</option>
                            <option value="Enorme">Enorme</option>
                            <option value="Mastodontica">Mastodontica</option>
                        </select>
                    </div>

                    <div class="field">
                        <label>Dadi Vita</label>
                        <div class="wiz-hp-row">
                            <input type="text" class="input" maxlength="40"
                                   placeholder="es. 3d12" @bind="Draft.HitDiceMax" />
                            @{
                                var prefill = CharacterWizardLogic.BuildHitDice(SelectedClass?.HitDie, Draft.Level);
                            }
                            @if (!string.IsNullOrEmpty(prefill) && Draft.HitDiceMax != prefill)
                            {
                                <button type="button" class="secondary-btn wiz-suggest-btn" @onclick="PrefillHitDice">
                                    Usa @prefill
                                </button>
                            }
                        </div>
                    </div>

                    <div class="derived-info">
                        Iniziativa: <strong>@FormatBonus(CharacterCalculations.GetInitiative(Draft))</strong>
                        · Competenza: <strong>@FormatBonus(CharacterCalculations.GetProficiencyBonus(Draft.Level))</strong>
                        · Percezione passiva: <strong>@CharacterCalculations.GetPassivePerception(Draft)</strong>
                    </div>
                </div>
                break;
```

- [ ] **Step 2: Aggiungere i membri in `@code`**

```csharp
    // PF suggeriti dal dado vita della classe + mod COS (del totale) + livello. 0 = nessun suggerimento.
    private int SuggestedHp =>
        CharacterWizardLogic.SuggestMaxHp(SelectedClass?.HitDie,
            CharacterCalculations.GetModifier(Draft.Constitution), Draft.Level);

    private void UseSuggestedHp()
    {
        Draft.MaxHitPoints = SuggestedHp;
        Draft.HitPoints = SuggestedHp; // in creazione i correnti = massimi
    }

    private void PrefillHitDice()
    {
        Draft.HitDiceMax = CharacterWizardLogic.BuildHitDice(SelectedClass?.HitDie, Draft.Level);
    }
```

- [ ] **Step 3: Build pulita**

Run: `dotnet build DndCompanion.csproj`
Expected: 0 warning, 0 errori.

- [ ] **Step 4: Verifica a vista (locale)**

Con una classe con dado vita (es. d12) e COS impostata: appare "Usa suggerito (N)" sui PF e "Usa 3d12" sui dadi vita; cliccando, i campi si compilano (i PF correnti seguono i massimi). Con classe "Altro"/senza dado: i pulsanti non appaiono. I derivati (iniziativa, competenza, percezione) si aggiornano.

- [ ] **Step 5: Commit**

```bash
git add Shared/CharacterTabs/CharacterWizard.razor
git commit -m "feat(wizard): step 3 Vitalità & combattimento (PF/dado vita suggeriti)"
```

---

## Task 9: Step 4 — Competenze (tiri salvezza + abilità)

**Files:**
- Modify: `Shared/CharacterTabs/CharacterWizard.razor`

**Interfaces:**
- Consumes: `SelectedClass` (Task 5); `ParseSaveProficiencies` (Task 4).
- Produces: `SaveSuggestions` (proprietà), `ApplySaveSuggestions`, `SaveSuggestionLabel`.

- [ ] **Step 1: Sostituire il segnaposto dello step 3 con il markup Competenze**

Sostituire `case 3: ... break;` con:

```razor
            case 3:
                <div class="wiz-step">
                    <div class="sub-block">
                        <p class="sub-block-title">TIRI SALVEZZA COMPETENTI</p>
                        @if (SaveSuggestions.Count > 0)
                        {
                            <div class="wiz-suggest-chip">
                                <span>Suggeriti dalla classe: <strong>@SaveSuggestionLabel</strong></span>
                                <button type="button" class="secondary-btn" @onclick="ApplySaveSuggestions">Applica</button>
                            </div>
                        }
                        <div class="checkbox-col">
                            <label class="checkbox-row"><input type="checkbox" @bind="Draft.ProfSaveStrength" /> <span>Forza</span></label>
                            <label class="checkbox-row"><input type="checkbox" @bind="Draft.ProfSaveDexterity" /> <span>Destrezza</span></label>
                            <label class="checkbox-row"><input type="checkbox" @bind="Draft.ProfSaveConstitution" /> <span>Costituzione</span></label>
                            <label class="checkbox-row"><input type="checkbox" @bind="Draft.ProfSaveIntelligence" /> <span>Intelligenza</span></label>
                            <label class="checkbox-row"><input type="checkbox" @bind="Draft.ProfSaveWisdom" /> <span>Saggezza</span></label>
                            <label class="checkbox-row"><input type="checkbox" @bind="Draft.ProfSaveCharisma" /> <span>Carisma</span></label>
                        </div>
                    </div>

                    <div class="sub-block">
                        <p class="sub-block-title">ABILITÀ COMPETENTI</p>
                        @if (SelectedClass is not null && !string.IsNullOrWhiteSpace(SelectedClass.SkillChoices))
                        {
                            <p class="field-hint">La classe permette di scegliere: @SelectedClass.SkillChoices</p>
                        }
                        <div class="skill-head"><span>Skill</span><span>Comp</span><span>Exp</span></div>

                        <p class="skill-group">FORZA</p>
                        <div class="skill-row"><span class="skill-name">Atletica</span><input type="checkbox" class="skill-check" @bind="Draft.ProfAthletics" /><input type="checkbox" class="skill-check" @bind="Draft.ExpAthletics" /></div>

                        <p class="skill-group">DESTREZZA</p>
                        <div class="skill-row"><span class="skill-name">Acrobazia</span><input type="checkbox" class="skill-check" @bind="Draft.ProfAcrobatics" /><input type="checkbox" class="skill-check" @bind="Draft.ExpAcrobatics" /></div>
                        <div class="skill-row"><span class="skill-name">Rapidità di mano</span><input type="checkbox" class="skill-check" @bind="Draft.ProfSleightOfHand" /><input type="checkbox" class="skill-check" @bind="Draft.ExpSleightOfHand" /></div>
                        <div class="skill-row"><span class="skill-name">Furtività</span><input type="checkbox" class="skill-check" @bind="Draft.ProfStealth" /><input type="checkbox" class="skill-check" @bind="Draft.ExpStealth" /></div>

                        <p class="skill-group">INTELLIGENZA</p>
                        <div class="skill-row"><span class="skill-name">Arcano</span><input type="checkbox" class="skill-check" @bind="Draft.ProfArcana" /><input type="checkbox" class="skill-check" @bind="Draft.ExpArcana" /></div>
                        <div class="skill-row"><span class="skill-name">Storia</span><input type="checkbox" class="skill-check" @bind="Draft.ProfHistory" /><input type="checkbox" class="skill-check" @bind="Draft.ExpHistory" /></div>
                        <div class="skill-row"><span class="skill-name">Indagare</span><input type="checkbox" class="skill-check" @bind="Draft.ProfInvestigation" /><input type="checkbox" class="skill-check" @bind="Draft.ExpInvestigation" /></div>
                        <div class="skill-row"><span class="skill-name">Natura</span><input type="checkbox" class="skill-check" @bind="Draft.ProfNature" /><input type="checkbox" class="skill-check" @bind="Draft.ExpNature" /></div>
                        <div class="skill-row"><span class="skill-name">Religione</span><input type="checkbox" class="skill-check" @bind="Draft.ProfReligion" /><input type="checkbox" class="skill-check" @bind="Draft.ExpReligion" /></div>

                        <p class="skill-group">SAGGEZZA</p>
                        <div class="skill-row"><span class="skill-name">Addestrare animali</span><input type="checkbox" class="skill-check" @bind="Draft.ProfAnimalHandling" /><input type="checkbox" class="skill-check" @bind="Draft.ExpAnimalHandling" /></div>
                        <div class="skill-row"><span class="skill-name">Intuizione</span><input type="checkbox" class="skill-check" @bind="Draft.ProfInsight" /><input type="checkbox" class="skill-check" @bind="Draft.ExpInsight" /></div>
                        <div class="skill-row"><span class="skill-name">Medicina</span><input type="checkbox" class="skill-check" @bind="Draft.ProfMedicine" /><input type="checkbox" class="skill-check" @bind="Draft.ExpMedicine" /></div>
                        <div class="skill-row"><span class="skill-name">Percezione</span><input type="checkbox" class="skill-check" @bind="Draft.ProfPerception" /><input type="checkbox" class="skill-check" @bind="Draft.ExpPerception" /></div>
                        <div class="skill-row"><span class="skill-name">Sopravvivenza</span><input type="checkbox" class="skill-check" @bind="Draft.ProfSurvival" /><input type="checkbox" class="skill-check" @bind="Draft.ExpSurvival" /></div>

                        <p class="skill-group">CARISMA</p>
                        <div class="skill-row"><span class="skill-name">Inganno</span><input type="checkbox" class="skill-check" @bind="Draft.ProfDeception" /><input type="checkbox" class="skill-check" @bind="Draft.ExpDeception" /></div>
                        <div class="skill-row"><span class="skill-name">Intimidire</span><input type="checkbox" class="skill-check" @bind="Draft.ProfIntimidation" /><input type="checkbox" class="skill-check" @bind="Draft.ExpIntimidation" /></div>
                        <div class="skill-row"><span class="skill-name">Intrattenere</span><input type="checkbox" class="skill-check" @bind="Draft.ProfPerformance" /><input type="checkbox" class="skill-check" @bind="Draft.ExpPerformance" /></div>
                        <div class="skill-row"><span class="skill-name">Persuasione</span><input type="checkbox" class="skill-check" @bind="Draft.ProfPersuasion" /><input type="checkbox" class="skill-check" @bind="Draft.ExpPersuasion" /></div>
                        <p class="field-hint">L'Expertise senza Competenza non ha effetto: i calcoli lo ignorano.</p>
                    </div>
                </div>
                break;
```

- [ ] **Step 2: Aggiungere i membri in `@code`**

```csharp
    private IReadOnlyList<string> SaveSuggestions =>
        CharacterWizardLogic.ParseSaveProficiencies(SelectedClass?.SavingThrows);

    private string SaveSuggestionLabel
    {
        get
        {
            var map = new Dictionary<string, string>
            {
                ["strength"] = "Forza", ["dexterity"] = "Destrezza", ["constitution"] = "Costituzione",
                ["intelligence"] = "Intelligenza", ["wisdom"] = "Saggezza", ["charisma"] = "Carisma"
            };
            return string.Join(", ", SaveSuggestions.Select(k => map.TryGetValue(k, out var v) ? v : k));
        }
    }

    private void ApplySaveSuggestions()
    {
        foreach (var key in SaveSuggestions)
        {
            switch (key)
            {
                case "strength": Draft.ProfSaveStrength = true; break;
                case "dexterity": Draft.ProfSaveDexterity = true; break;
                case "constitution": Draft.ProfSaveConstitution = true; break;
                case "intelligence": Draft.ProfSaveIntelligence = true; break;
                case "wisdom": Draft.ProfSaveWisdom = true; break;
                case "charisma": Draft.ProfSaveCharisma = true; break;
            }
        }
    }
```

- [ ] **Step 3: Build pulita**

Run: `dotnet build DndCompanion.csproj`
Expected: 0 warning, 0 errori.

- [ ] **Step 4: Verifica a vista (locale)**

Con una classe il cui `saving_throws` è "Forza, Costituzione": appare il chip "Suggeriti dalla classe: Forza, Costituzione [Applica]"; "Applica" spunta i due tiri salvezza. Le checkbox abilità si attivano. Con `saving_throws` vuoto/non standard: niente chip.

- [ ] **Step 5: Commit**

```bash
git add Shared/CharacterTabs/CharacterWizard.razor
git commit -m "feat(wizard): step 4 Competenze (suggerimento tiri salvezza + abilità)"
```

---

## Task 10: Step 5 — Incantesimi

**Files:**
- Modify: `Shared/CharacterTabs/CharacterWizard.razor`

**Interfaces:**
- Consumes: `Draft`; `CharacterCalculations.GetSpellSaveDc/GetSpellAttackBonus`; `GetSpellSlotMax`/`GetSpellSlotUsed` (da `CharacterView`, importati globalmente); `FormatBonus`.
- Produces: `SetSpellSlotMax` (privato), `OnSpellSlotMaxChanged`.

- [ ] **Step 1: Sostituire il segnaposto dello step 4 con il markup Incantesimi**

Sostituire `case 4: ... break;` con:

```razor
            case 4:
                <div class="wiz-step">
                    <div class="field">
                        <label>Caratteristica da incantatore</label>
                        <select class="input" @bind="Draft.SpellcastingAbility">
                            <option value="">-- Nessuna (non incantatore) --</option>
                            <option value="intelligence">Intelligenza</option>
                            <option value="wisdom">Saggezza</option>
                            <option value="charisma">Carisma</option>
                        </select>
                        <span class="field-hint">Se "Nessuna", il personaggio non è un incantatore e il tab Magic non comparirà.</span>
                    </div>

                    @if (!string.IsNullOrWhiteSpace(Draft.SpellcastingAbility))
                    {
                        <div class="derived-info">
                            CD Incantesimo: <strong>@(CharacterCalculations.GetSpellSaveDc(Draft) ?? 0)</strong>
                            · Bonus Attacco: <strong>@FormatBonus(CharacterCalculations.GetSpellAttackBonus(Draft) ?? 0)</strong>
                        </div>

                        <p class="sub-block-title">SLOT INCANTESIMO (MASSIMI)</p>
                        <div class="spell-slots-head"><span>Livello</span><span>Massimi</span></div>
                        @for (var level = 1; level <= 9; level++)
                        {
                            var lvl = level;
                            <div class="spell-slot-input-row">
                                <span class="spell-slot-input-label">Liv @lvl</span>
                                <input type="number" class="input" min="0" max="9"
                                       value="@GetSpellSlotMax(Draft, lvl)"
                                       @onchange="e => OnSpellSlotMaxChanged(lvl, e)" />
                            </div>
                        }
                        <p class="field-hint">Imposta gli slot massimi secondo la tabella della tua classe. Gli "usati" si gestiscono in sessione dal tab Magic.</p>
                    }
                </div>
                break;
```

- [ ] **Step 2: Aggiungere i membri in `@code`**

```csharp
    private void OnSpellSlotMaxChanged(int level, ChangeEventArgs e)
    {
        var v = int.TryParse(e.Value?.ToString(), out var n) ? Math.Clamp(n, 0, 9) : 0;
        SetSpellSlotMax(Draft, level, v);
    }

    private static void SetSpellSlotMax(Character c, int level, int v)
    {
        switch (level)
        {
            case 1: c.SpellSlots1Max = v; break;
            case 2: c.SpellSlots2Max = v; break;
            case 3: c.SpellSlots3Max = v; break;
            case 4: c.SpellSlots4Max = v; break;
            case 5: c.SpellSlots5Max = v; break;
            case 6: c.SpellSlots6Max = v; break;
            case 7: c.SpellSlots7Max = v; break;
            case 8: c.SpellSlots8Max = v; break;
            case 9: c.SpellSlots9Max = v; break;
        }
    }
```

- [ ] **Step 3: Build pulita**

Run: `dotnet build DndCompanion.csproj`
Expected: 0 warning, 0 errori.

- [ ] **Step 4: Verifica a vista (locale)**

Selezionando una caratteristica da incantatore appaiono CD/Bonus attacco e gli slot massimi liv 1–9 (editabili, clamp 0–9); con "Nessuna" lo step resta vuoto. Salvando un incantatore, in scheda compare il tab Magic; salvando un non-incantatore, no.

- [ ] **Step 5: Commit**

```bash
git add Shared/CharacterTabs/CharacterWizard.razor
git commit -m "feat(wizard): step 5 Incantesimi"
```

---

## Task 11: Step 6 — Riepilogo & salva

**Files:**
- Modify: `Shared/CharacterTabs/CharacterWizard.razor`

**Interfaces:**
- Consumes: `Draft`; `FinalScore` (Task 7); `FormatBonus`; `CharacterCalculations.GetModifier`; `SelectedClass`/`SelectedRace` (opzionali).
- Produces: niente di nuovo (solo markup di sola lettura). Il pulsante "Salva personaggio" è già nella nav (Task 5).

- [ ] **Step 1: Sostituire il segnaposto dello step 5 con il markup Riepilogo**

Sostituire `case 5: ... break;` con:

```razor
            case 5:
                <div class="wiz-step wiz-summary">
                    <div class="wiz-summary-card">
                        <h3 class="wiz-summary-name">@(string.IsNullOrWhiteSpace(Draft.Name) ? "(senza nome)" : Draft.Name)</h3>
                        <p class="wiz-summary-line">
                            Liv @Draft.Level
                            · @(string.IsNullOrWhiteSpace(Draft.Class) ? "Classe —" : Draft.Class)@(string.IsNullOrWhiteSpace(Draft.Subclass) ? "" : $" ({Draft.Subclass})")
                            · @(string.IsNullOrWhiteSpace(Draft.Race) ? "Razza —" : Draft.Race)
                        </p>

                        <div class="wiz-summary-grid">
                            @for (var i = 0; i < 6; i++)
                            {
                                var idx = i;
                                <div class="wiz-summary-stat">
                                    <span class="wiz-summary-stat-label">@AbilityShort[idx]</span>
                                    <span class="wiz-summary-stat-val">@FinalScore(idx)</span>
                                    <span class="wiz-summary-stat-mod">@FormatBonus(CharacterCalculations.GetModifier(FinalScore(idx)))</span>
                                </div>
                            }
                        </div>

                        <p class="wiz-summary-line">
                            CA <strong>@Draft.ArmorClass</strong>
                            · PF <strong>@Draft.MaxHitPoints</strong>
                            · Velocità <strong>@Draft.Speed m</strong>
                            @if (!string.IsNullOrWhiteSpace(Draft.HitDiceMax))
                            {
                                <span> · Dadi vita <strong>@Draft.HitDiceMax</strong></span>
                            }
                        </p>

                        <p class="wiz-summary-line">
                            Incantatore: <strong>@(string.IsNullOrWhiteSpace(Draft.SpellcastingAbility) ? "no" : "sì")</strong>
                        </p>
                    </div>
                    <p class="field-hint">Controlla i dati e premi "Salva personaggio". Potrai rifinire tutto il resto (aspetto, storia, denari, difese, inventario) dalla scheda.</p>
                </div>
                break;
```

- [ ] **Step 2: Build pulita**

Run: `dotnet build DndCompanion.csproj`
Expected: 0 warning, 0 errori.

- [ ] **Step 3: Verifica a vista (locale)**

Lo step 6 riepiloga nome, classe/sottoclasse, razza, livello, le 6 caratteristiche con modificatore, CA/PF/velocità/dadi vita e incantatore sì/no. "Salva personaggio" crea il PG e apre la scheda; senza nome mostra il toast e resta sul wizard.

- [ ] **Step 4: Commit**

```bash
git add Shared/CharacterTabs/CharacterWizard.razor
git commit -m "feat(wizard): step 6 Riepilogo & salva"
```

---

## Task 12: Stile del wizard (`CharacterWizard.razor.css`)

**Files:**
- Create: `Shared/CharacterTabs/CharacterWizard.razor.css`
- Reference: `Shared/CharacterTabs/CharacterEditForm.razor.css` (da cui copiare le regole dei campi)

**Interfaces:** nessuna (solo CSS scoped).

- [ ] **Step 1: Copiare le regole dei campi dall'accordion**

Aprire `Shared/CharacterTabs/CharacterEditForm.razor.css` e **copiare verbatim** in cima a `CharacterWizard.razor.css` le regole di questi selettori (servono al wizard perché lo scoping non le eredita):
`.field`, `.field label`, `.input`, `.input:focus`, `.custom-input`, `.info-summary`, `.info-summary strong`, `.field-hint`, `.derived-info`, `.derived-info strong`, `.sub-block`, `.sub-block-title`, `.checkbox-col`, `.checkbox-row`, `.checkbox-row input[type="checkbox"]`, `.skill-head`, `.skill-head span:not(:first-child)`, `.skill-group`, `.skill-row`, `.skill-name`, `.skill-check`, `.spell-slots-head`, `.spell-slots-head span:not(:first-child)`, `.spell-slot-input-row`, `.spell-slot-input-label`, `.spell-slot-input-row .input`, `.primary-btn`, `.primary-btn:hover:not(:disabled)`, `.primary-btn:disabled`, `.secondary-btn`, `.secondary-btn:hover:not(:disabled)`, `.secondary-btn:disabled`.

- [ ] **Step 2: Aggiungere le classi specifiche del wizard**

Appendere in fondo a `CharacterWizard.razor.css` (usa i token globali di `app.css`):

```css
/* ===== Chrome del wizard ===== */
.wizard-view {
    max-width: 640px;
    margin: 0 auto;
    padding: 0.5rem 1rem 2rem;
}

.wizard-top { margin: 0.5rem 0; }

.wiz-cancel {
    background: none;
    border: none;
    color: var(--gold-muted);
    cursor: pointer;
    font-size: 0.95rem;
    padding: 0.25rem 0;
}
.wiz-cancel:hover { color: var(--gold); }

.wizard-title {
    color: var(--gold);
    text-align: center;
    margin: 0.25rem 0 1rem;
}

.wizard-steps {
    display: flex;
    justify-content: center;
    gap: 0.5rem;
    margin-bottom: 0.5rem;
}

.wiz-step-dot {
    width: 2rem;
    height: 2rem;
    border-radius: 50%;
    border: 1px solid var(--gold-dim);
    background: var(--bg-card);
    color: var(--gold-muted);
    cursor: pointer;
    font-weight: 600;
}
.wiz-step-dot.active {
    border-color: var(--gold-light);
    background: linear-gradient(180deg, var(--gold) 0%, var(--gold-dark) 100%);
    color: var(--bg);
}
.wiz-step-dot.done { color: var(--gold); border-color: var(--gold); }

.wiz-step-caption {
    text-align: center;
    color: var(--gold-muted);
    font-size: 0.9rem;
    margin: 0 0 1rem;
}

.wizard-body { margin-bottom: 1.5rem; }
.wiz-step { display: flex; flex-direction: column; gap: 0.75rem; }
.wiz-placeholder { color: var(--gold-muted); text-align: center; padding: 2rem 0; }

.wizard-nav {
    display: flex;
    justify-content: space-between;
    gap: 0.75rem;
}
.wizard-nav .primary-btn { margin-left: auto; }

/* ===== Step Caratteristiche ===== */
.wiz-array-btn { align-self: flex-start; }

.wiz-ability-head,
.wiz-ability-row {
    display: grid;
    grid-template-columns: 3rem 1fr 2.5rem 4.5rem;
    align-items: center;
    gap: 0.5rem;
}
.wiz-ability-head {
    color: var(--gold-muted);
    font-size: 0.8rem;
    text-transform: uppercase;
    margin-bottom: 0.25rem;
}
.wiz-ability-name { color: var(--gold); font-weight: 600; }
.wiz-stepper { display: flex; align-items: center; gap: 0.25rem; }
.wiz-base-input { width: 3.5rem; text-align: center; }
.wiz-ability-bonus { color: var(--gold-muted); text-align: center; }
.wiz-ability-total { color: var(--gold-light); font-weight: 600; }
.wiz-ability-total small { color: var(--gold-muted); font-weight: 400; }

.qty-btn {
    width: 1.8rem;
    height: 1.8rem;
    border-radius: 4px;
    border: 1px solid var(--gold-dim);
    background: var(--bg-card);
    color: var(--gold);
    cursor: pointer;
    font-size: 1.1rem;
    line-height: 1;
}
.qty-btn:hover { border-color: var(--gold); }

/* ===== Step Vitalità & competenze ===== */
.wiz-hp-row { display: flex; gap: 0.5rem; align-items: center; }
.wiz-hp-row .input { flex: 1; }
.wiz-suggest-btn { white-space: nowrap; }

.wiz-suggest-chip {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
    background: var(--bg-alt);
    border: 1px solid var(--gold-dim);
    border-radius: 6px;
    padding: 0.5rem 0.75rem;
    margin-bottom: 0.5rem;
    color: var(--gold-muted);
    font-size: 0.9rem;
}

/* ===== Step Riepilogo ===== */
.wiz-summary-card {
    background: var(--bg-card);
    border: 1px solid var(--gold-dim);
    border-radius: 8px;
    padding: 1rem;
}
.wiz-summary-name { color: var(--gold); margin: 0 0 0.25rem; }
.wiz-summary-line { color: var(--gold-muted); margin: 0.5rem 0; }
.wiz-summary-grid {
    display: grid;
    grid-template-columns: repeat(6, 1fr);
    gap: 0.5rem;
    margin: 0.75rem 0;
}
.wiz-summary-stat {
    display: flex;
    flex-direction: column;
    align-items: center;
    background: var(--bg-alt);
    border-radius: 6px;
    padding: 0.4rem 0;
}
.wiz-summary-stat-label { color: var(--gold-muted); font-size: 0.75rem; }
.wiz-summary-stat-val { color: var(--gold-light); font-weight: 600; font-size: 1.1rem; }
.wiz-summary-stat-mod { color: var(--gold-muted); font-size: 0.8rem; }

.req { color: #e0a0a0; }
```

- [ ] **Step 3: Build pulita**

Run: `dotnet build DndCompanion.csproj`
Expected: 0 warning, 0 errori.

- [ ] **Step 4: Verifica a vista (locale)**

Tutti gli step sono a tema (oro su fondo scuro), coerenti con l'accordion: input/bottoni/checkbox stilizzati, la barra dei pallini evidenzia il passo attivo, lo step Caratteristiche allinea base/razza/totale, il Riepilogo è una card leggibile.

- [ ] **Step 5: Commit**

```bash
git add Shared/CharacterTabs/CharacterWizard.razor.css
git commit -m "feat(wizard): stile scoped del wizard"
```

---

## Task 13: Verifica finale + aggiornamento documentazione

**Files:**
- Modify: `docs/DA-FARE.md` (§8), `docs/DIARIO.md`

**Interfaces:** nessuna.

- [ ] **Step 1: Suite completa + build Release**

Run: `dotnet test Tests/DndCompanion.Tests.csproj`
Expected: tutti i test verdi (i precedenti + i nuovi di `CharacterWizardLogic`).

Run: `dotnet build DndCompanion.csproj -c Release`
Expected: 0 warning, 0 errori (verifica anche il trimming-friendliness del nuovo codice — sono solo POCO/helper, nessun rischio).

- [ ] **Step 2: Verifica a vista end-to-end (locale)**

Su `https://localhost:7076`, eseguire lo scenario di `spec §9`: creazione completa di un incantatore con razza/classe a catalogo (auto-bonus, suggerimenti, array standard, slot), salvataggio, apertura scheda con tab Magic; più i casi limite (niente razza/classe → salvataggio col solo nome; classe "Altro"). Confermare che la **modifica** di un PG esistente apra ancora l'accordion.

- [ ] **Step 3: Aggiornare `docs/DA-FARE.md`**

In §8, marcare la voce "Redesign del flusso scheda / wizard" come ✅ FATTO con data 2026-06-24, sintetizzando: wizard di sola creazione (6 step), automazione intermedia (bonus razza/dado vita applicati, PF/tiri salvezza suggeriti), helper puri `CharacterWizardLogic` con test, accordion invariato per l'edit, zero impatto DB/RLS.

- [ ] **Step 4: Aggiornare `docs/DIARIO.md`**

Aggiungere una voce datata 2026-06-24 con il riassunto della feature e i file toccati (`Services/CharacterWizardLogic.cs`, `Tests/CharacterWizardLogicTests.cs`, `Shared/CharacterTabs/CharacterWizard.razor(.css)`, `Pages/Characters.razor`).

- [ ] **Step 5: Commit**

```bash
git add docs/DA-FARE.md docs/DIARIO.md
git commit -m "docs: chiusura wizard creazione scheda PG (§8)"
```

> **Push:** NON pushare. Avvisare l'utente che tutto è pronto e in attesa del suo via per il push (deploy in produzione).

---

## Self-Review

**Spec coverage:**
- §1/§2 decisioni (solo creazione, automazione intermedia, array standard, essenziale, ibrido) → Task 5 (entry point/solo creazione), Task 1/7 (bonus razza), Task 2-3/8 (dado vita/PF), Task 7 (array standard), Task 4/9 (suggerimento TS). ✓
- §3 entry point `ViewMode.Wizard` + riuso `SaveFormAsync`/`CancelForm` + 3 file nuovi → Task 5, Task 12 (css), Task 1-4 (logic). ✓
- §3 niente estrazione `ProficiencyFields` → confermato in Global Constraints (CSS scoped) + Task 9 (markup proprio). ✓
- §4 i 6 step → Task 6-11. ✓
- §5 helper puri (4 funzioni, base-scores nello stato locale) → Task 1-4 + Task 5 (`baseScores`/`RecalcFinals`). ✓
- §6 flusso dati (finali sempre nel Draft, init baseScores) → Task 5 + Task 7. ✓
- §7 validazione/errori riusati (Normalizer non clampa le caratteristiche → garantito da FinalAbilityScores) → Task 5 (OnSave=SaveFormAsync), clamp in Task 1/7. ✓
- §8 test puri → Task 1-4. ✓
- §9 verifica locale → Task 13 step 2. ✓
- §10 rischi (velocità non auto-applicata, base non persistiti) → Task 8 (velocità solo info), Task 5/7 (base in stato locale). ✓

**Placeholder scan:** nessun TBD/TODO; ogni step UI ha codice completo; i blocchi CSS da copiare sono riferiti a un file e a selettori esatti esistenti. ✓

**Type consistency:** firme helper coerenti tra definizione (Task 1-4) e uso (`FinalAbilityScores`/`BuildHitDice`/`SuggestMaxHp`/`ParseSaveProficiencies` in Task 5/7/8/9). `baseScores`/`RecalcFinals`/`SelectedRace`/`SelectedClass`/`CustomOptionValue`/`AbilityShort` definiti in Task 5/7 e usati con lo stesso nome a valle. `GetSpellSlotMax` (CharacterView) vs `SetSpellSlotMax` (privato del wizard, Task 10) coerenti coi rispettivi usi. ✓
