# Visibilità limitata del player nel tracker — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nel tracker combattimento il player vede solo la propria scheda (statistiche complete + segnale di turno) e degli altri solo il nome; la vista del Master resta invariata.

**Architecture:** Redazione **cosmetica lato UI**. Si aggiunge `OwnerId` al POCO `Combatant` (marcato all'import dei PG), un helper puro `CombatVisibility` decide "riga mia / mio turno / altri", e `Combat.razor` biforca la vista player da quella master. Nessuna migrazione (`combatants` è già `jsonb`), nessun cambio a DB/RLS.

**Tech Stack:** Blazor WebAssembly / .NET 10, xUnit, Supabase (postgrest-csharp). Spec: `docs/superpowers/specs/2026-07-23-combat-player-visibility-design.md`.

## Global Constraints

- Logica di dominio in **helper puri `static`** in `Services/`, testabili xUnit (modello: `CombatImport`, `AccessControl`). Mai nei `.razor`.
- Build Release: **0 warning / 0 errori**.
- CSS: solo **design token** in `:root`; stili scoped alla pagina in `Combat.razor.css`.
- Aggancio "riga mia" via `owner_id` marcato in `ImportCharactersAsync`; righe senza owner → per il player valgono come "non mie" (default sicuro).
- Regola progetto: dopo le modifiche, gate a due agenti (`critico` + `conformità`) prima del commit finale.

---

### Task 1: `Combatant.OwnerId` + helper `CombatVisibility`

**Files:**
- Modify: `Models/Combatant.cs`
- Create: `Services/CombatVisibility.cs`
- Test: `Tests/CombatVisibilityTests.cs`

**Interfaces:**
- Consumes: `Models.Combatant` (aggiunge `string? OwnerId`).
- Produces:
  - `CombatVisibility.IsOwn(Combatant c, string? userId) -> bool`
  - `CombatVisibility.IsCurrentTurnOwn(IReadOnlyList<Combatant> combatants, int currentTurnIndex, string? userId) -> bool`
  - `CombatVisibility.OwnForPlayer(IReadOnlyList<Combatant> combatants, string? userId) -> List<Combatant>`
  - `CombatVisibility.OthersForPlayer(IReadOnlyList<Combatant> combatants, string? userId) -> List<Combatant>`

- [ ] **Step 1: Aggiungi `OwnerId` a `Combatant`**

In `Models/Combatant.cs`, dentro la classe, dopo `Id`:

```csharp
/// <summary>
/// Proprietario del PG se il combattente è stato importato da un personaggio (owner_id);
/// null per mostri, PNG e aggiunte manuali. Usato solo dalla redazione lato player.
/// </summary>
public string? OwnerId { get; set; }
```

- [ ] **Step 2: Scrivi i test (falliscono: helper inesistente)**

Crea `Tests/CombatVisibilityTests.cs`:

```csharp
using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

public class CombatVisibilityTests
{
    private static Combatant Own(string owner, string name = "PG", int init = 0)
        => new() { Name = name, OwnerId = owner, Initiative = init };

    private static Combatant Foreign(string name = "Mostro", int init = 0)
        => new() { Name = name, OwnerId = null, Initiative = init };

    [Fact]
    public void IsOwn_true_quando_owner_combacia()
        => Assert.True(CombatVisibility.IsOwn(Own("u1"), "u1"));

    [Fact]
    public void IsOwn_false_quando_owner_diverso()
        => Assert.False(CombatVisibility.IsOwn(Own("u1"), "u2"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsOwn_false_quando_owner_nullo_o_vuoto(string? owner)
        => Assert.False(CombatVisibility.IsOwn(new Combatant { OwnerId = owner }, "u1"));

    [Fact]
    public void IsOwn_false_quando_userId_nullo()
        => Assert.False(CombatVisibility.IsOwn(Own("u1"), null));

    [Fact]
    public void IsCurrentTurnOwn_true_quando_il_turno_e_del_player()
    {
        var list = new List<Combatant> { Foreign(), Own("u1"), Foreign() };
        Assert.True(CombatVisibility.IsCurrentTurnOwn(list, 1, "u1"));
    }

    [Fact]
    public void IsCurrentTurnOwn_false_quando_il_turno_e_altrui()
    {
        var list = new List<Combatant> { Foreign(), Own("u1"), Foreign() };
        Assert.False(CombatVisibility.IsCurrentTurnOwn(list, 0, "u1"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void IsCurrentTurnOwn_false_quando_indice_fuori_range(int idx)
    {
        var list = new List<Combatant> { Own("u1"), Foreign() };
        Assert.False(CombatVisibility.IsCurrentTurnOwn(list, idx, "u1"));
    }

    [Fact]
    public void OwnForPlayer_solo_le_righe_del_player_in_ordine_originale()
    {
        var a = Own("u1", "Gorik");
        var b = Own("u1", "Alba");
        var list = new List<Combatant> { Foreign(), a, Foreign(), b };
        Assert.Equal(new[] { a, b }, CombatVisibility.OwnForPlayer(list, "u1"));
    }

    [Fact]
    public void OthersForPlayer_esclude_le_mie_e_ordina_per_nome_case_insensitive()
    {
        var list = new List<Combatant>
        {
            Own("u1", "Gorik"),
            Foreign("zombi"),
            Foreign("Goblin"),
            Own("u1", "Alba"),
        };
        var others = CombatVisibility.OthersForPlayer(list, "u1");
        Assert.Equal(new[] { "Goblin", "zombi" }, others.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void OthersForPlayer_include_le_righe_senza_owner()
    {
        var list = new List<Combatant> { Foreign("Goblin"), Own("u1") };
        var others = CombatVisibility.OthersForPlayer(list, "u1");
        Assert.Single(others);
        Assert.Equal("Goblin", others[0].Name);
    }
}
```

- [ ] **Step 3: Verifica che i test falliscano (compilazione)**

Run: `dotnet test Tests/DndCompanion.Tests.csproj`
Expected: FAIL — `CombatVisibility` non esiste.

- [ ] **Step 4: Implementa l'helper**

Crea `Services/CombatVisibility.cs`:

```csharp
using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Redazione lato client di ciò che un player può vedere nel tracker combattimento: la propria
/// scheda con statistiche complete, e degli altri solo il nome. Funzioni pure (nessuno stato/I/O),
/// testabili. È una redazione UX cosmetica: i dati grezzi arrivano comunque al browser.
/// Una riga "mia" è quella importata dal proprio PG (<c>OwnerId</c> == utente corrente); righe
/// senza owner (mostri, aggiunte a mano) non sono mai "mie".
/// </summary>
public static class CombatVisibility
{
    /// <summary>La riga appartiene al player. Owner null/vuoto o userId null/vuoto → false (specchio di AccessControl).</summary>
    public static bool IsOwn(Combatant c, string? userId)
        => !string.IsNullOrEmpty(userId)
           && !string.IsNullOrEmpty(c.OwnerId)
           && c.OwnerId == userId;

    /// <summary>Il combattente il cui turno è corrente appartiene al player. Indice fuori range → false.</summary>
    public static bool IsCurrentTurnOwn(IReadOnlyList<Combatant> combatants, int currentTurnIndex, string? userId)
        => currentTurnIndex >= 0
           && currentTurnIndex < combatants.Count
           && IsOwn(combatants[currentTurnIndex], userId);

    /// <summary>Le righe del player, nell'ordine originale (coerente con la scheda).</summary>
    public static List<Combatant> OwnForPlayer(IReadOnlyList<Combatant> combatants, string? userId)
        => combatants.Where(c => IsOwn(c, userId)).ToList();

    /// <summary>Le righe non del player, ordinate per nome (case-insensitive) per non svelare l'iniziativa.</summary>
    public static List<Combatant> OthersForPlayer(IReadOnlyList<Combatant> combatants, string? userId)
        => combatants
            .Where(c => !IsOwn(c, userId))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
```

- [ ] **Step 5: Verifica che i test passino**

Run: `dotnet test Tests/DndCompanion.Tests.csproj`
Expected: PASS (tutti, inclusi i nuovi).

- [ ] **Step 6: Commit**

```bash
git add Models/Combatant.cs Services/CombatVisibility.cs Tests/CombatVisibilityTests.cs
git commit -m "feat(combat): helper CombatVisibility + Combatant.OwnerId per redazione player"
```

---

### Task 2: Marca l'owner all'import + vista player in `Combat.razor` + CSS

**Files:**
- Modify: `Pages/Combat.razor` (import + branch vista)
- Modify: `Pages/Combat.razor.css` (stili vista player)

**Interfaces:**
- Consumes: `CombatVisibility.*` (Task 1), `Combatant.OwnerId` (Task 1), `CurrentUser.UserId`.
- Produces: vista player redatta (nessun contratto verso altri task).

- [ ] **Step 1: Marca `OwnerId` all'import dei personaggi**

In `Pages/Combat.razor`, dentro `ImportCharactersAsync`, nel `foreach (var ch in characters)`, aggiungi `OwnerId` al nuovo `Combatant`:

```csharp
combatants.Add(new Combatant
{
    Name = ch.Name,
    OwnerId = ch.OwnerId,
    Initiative = 0,
    CurrentHp = ch.HitPoints,
    MaxHp = ch.MaxHitPoints
});
```

- [ ] **Step 2: Round indicator differenziato per il player**

In `Pages/Combat.razor`, sostituisci il blocco `round-indicator` (attualmente mostra "Turno di *Nome*" a tutti) con una versione che per il player non svela l'attore corrente:

```razor
@if (combatants.Count > 0)
{
    <div class="round-indicator">
        <div class="round-label">Round <strong>@roundNumber</strong></div>
        @if (CurrentUser.IsMaster)
        {
            @if (CurrentCombatant is not null)
            {
                <div class="current-turn">
                    Turno di <strong>@CurrentCombatant.Name</strong>
                </div>
            }
        }
        else if (CombatVisibility.IsCurrentTurnOwn(combatants, currentTurnIndex, CurrentUser.UserId))
        {
            <div class="current-turn your-turn">È il tuo turno!</div>
        }
    </div>
}
```

- [ ] **Step 3: Biforca la lista combattenti (master invariata, player redatta)**

In `Pages/Combat.razor`, sostituisci il contenuto di `<div class="combatants-list">` con un branch. Il ramo Master è **identico all'attuale** (empty-state + `@for` con le righe e i controlli). Aggiungi il ramo player:

```razor
<div class="combatants-list">
    @if (combatants.Count == 0)
    {
        <div class="empty-state">
            <div class="empty-icon">⚔️</div>
            @if (CurrentUser.IsMaster)
            {
                <p class="empty-text">Nessun combattente. Aggiungili dal form qui sopra o importa i personaggi dal database.</p>
            }
            else
            {
                <p class="empty-text">In attesa del Master...</p>
            }
        </div>
    }
    else if (CurrentUser.IsMaster)
    {
        @for (var i = 0; i < combatants.Count; i++)
        {
            var c = combatants[i];
            var isCurrent = i == currentTurnIndex;
            var isDefeated = c.CurrentHp <= 0;
            var pos = i + 1;
            <div class="combatant-row @(isCurrent ? "current" : "") @(isDefeated ? "defeated" : "")">
                <div class="pos">@pos</div>
                <input type="number" class="init-input" @bind="c.Initiative" @bind:event="onchange" />
                <div class="combatant-main">
                    <div class="combatant-name">@c.Name</div>
                    <div class="hp-display">
                        PF <strong>@c.CurrentHp</strong> / @c.MaxHp
                    </div>
                </div>
                <div class="hp-btns">
                    <button type="button" class="hp-btn hp-minus" aria-label="Diminuisci PF"
                            @onclick="() => AdjustHp(c, -1)">
                        −
                    </button>
                    <button type="button" class="hp-btn hp-plus" aria-label="Aumenta PF"
                            @onclick="() => AdjustHp(c, 1)">
                        +
                    </button>
                </div>
                <button type="button" class="remove-btn" title="Rimuovi" aria-label="Rimuovi combattente"
                        @onclick="() => RemoveCombatant(c.Id)">
                    ✕
                </button>
            </div>
        }
    }
    else
    {
        @{
            var mine = CombatVisibility.OwnForPlayer(combatants, CurrentUser.UserId);
            var others = CombatVisibility.OthersForPlayer(combatants, CurrentUser.UserId);
            var myTurn = CombatVisibility.IsCurrentTurnOwn(combatants, currentTurnIndex, CurrentUser.UserId);
        }

        @if (mine.Count > 0)
        {
            <div class="player-section">
                <div class="player-section-title">La tua scheda</div>
                @foreach (var c in mine)
                {
                    var isDefeated = c.CurrentHp <= 0;
                    <div class="combatant-row own @(myTurn ? "current" : "") @(isDefeated ? "defeated" : "")">
                        <div class="init-badge">@c.Initiative</div>
                        <div class="combatant-main">
                            <div class="combatant-name">@c.Name</div>
                            <div class="hp-display">
                                PF <strong>@c.CurrentHp</strong> / @c.MaxHp
                            </div>
                        </div>
                    </div>
                }
            </div>
        }

        @if (others.Count > 0)
        {
            <div class="player-section">
                <div class="player-section-title">Altri nella scena</div>
                <ul class="others-list">
                    @foreach (var c in others)
                    {
                        <li class="other-name">@c.Name</li>
                    }
                </ul>
            </div>
        }
    }
</div>
```

- [ ] **Step 4: Aggiungi gli stili della vista player**

In coda a `Pages/Combat.razor.css` aggiungi (usa i design token già presenti nel file; verifica i nomi con una lettura del `:root` di `app.css` se in dubbio):

```css
/* --- Vista player: segnale di turno e sezioni --- */
.current-turn.your-turn {
    color: var(--color-success, #2e7d32);
    font-weight: 700;
}

.player-section {
    margin-top: var(--space-md, 1rem);
}

.player-section-title {
    font-size: 0.85rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--color-text-muted, #6b7280);
    margin-bottom: var(--space-sm, 0.5rem);
}

.combatant-row.own {
    border-left: 3px solid var(--color-accent, #6d28d9);
}

.others-list {
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-xs, 0.25rem);
}

.other-name {
    padding: var(--space-sm, 0.5rem) var(--space-md, 0.75rem);
    background: var(--color-surface-2, #f3f4f6);
    border-radius: var(--radius-sm, 6px);
    color: var(--color-text, #111827);
}
```

- [ ] **Step 5: Build + test**

Run: `dotnet build` poi `dotnet test Tests/DndCompanion.Tests.csproj`
Expected: 0 warning / 0 errori; test verdi.

- [ ] **Step 6: Verifica manuale rapida (facoltativa se lo stack è su)**

Con `dotnet run`, come player controlla: vedi solo la tua scheda con PF/iniziativa, gli altri come soli nomi, e "È il tuo turno!" appare quando il Master arriva al tuo turno; l'indicatore non mostra mai "Turno di *altro nome*".

- [ ] **Step 7: Commit**

```bash
git add Pages/Combat.razor Pages/Combat.razor.css
git commit -m "feat(combat): vista player redatta (solo la propria scheda + nomi altrui)"
```

---

### Task 3: Documentazione

**Files:**
- Modify: `docs/DIARIO.md`
- Modify: `docs/DA-FARE.md` (se il punto è tracciato lì)

- [ ] **Step 1: Aggiorna il diario**

Aggiungi in cima a `docs/DIARIO.md` una voce datata 2026-07-23 che descrive la feature e il *perché* (privacy delle informazioni tra player), citando la natura cosmetica lato UI e l'aggancio via `owner_id`.

- [ ] **Step 2: Chiudi il punto nel backlog**

Se in `docs/DA-FARE.md` esiste il punto sulla visibilità del player, marcalo come fatto / rimuovilo.

- [ ] **Step 3: Commit**

```bash
git add docs/DIARIO.md docs/DA-FARE.md
git commit -m "docs: registra la visibilità limitata del player nel tracker"
```

---

## Note per l'esecuzione

- Dopo Task 2 e Task 3, **prima di dichiarare fatto**, lanciare il gate obbligatorio: subagent `critico` + `conformità` in parallelo sul diff, correggere, rilanciare (max 3 giri). Commit finale solo a uscita pulita.
- Nessuna migrazione DB. Nessun cambio a RLS. Il polling resta invariato.
