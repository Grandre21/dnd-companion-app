# Rifinitura UX/a11y + CSP + validazione form — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tre rifiniture indipendenti del backlog (spinner+a11y, CSP, validazione form) in un solo /loop, fatte in sequenza con verifica locale.

**Architecture:** Modifiche mirate a `.razor`/`index.html` + un helper puro testabile `Services/FormValidation.cs`. Nessun cambiamento di architettura, DB o RLS.

**Tech Stack:** Blazor WebAssembly .NET 10, xUnit (progetto `DndCompanion.Tests`), CSP via `<meta>`.

## Global Constraints

- **Niente commit/push senza ok esplicito dell'utente.** I task terminano con build + test (+ verifica locale), NON con commit. I commit si fanno alla fine, raggruppati, solo su ok.
- **Verifica in locale su `https://localhost:7076`** (login abilitato) prima di considerare fatto uno step a impatto runtime.
- **Build attesa:** `dotnet build` 0 errori; **test attesi:** 111 esistenti verdi + i nuovi.
- **CSP:** `'wasm-unsafe-eval'` obbligatorio per Blazor WASM; unica risorsa esterna consentita: `https://tbgjwtfmijrcmeracfzh.supabase.co`. Hash SHA-256 sugli script inline; `'unsafe-inline'` solo per gli stili.
- **Ordine:** Task 1 → Task 2 → Task 3 (la CSP per ultima).
- Niente toccare `Pages/Characters.razor` (validazione già in `CharacterNormalizer`).

---

### Task 1: UX/a11y — spinner + aria-label sui FAB

**Files:**
- Modify: `Pages/Classes.razor`, `Pages/Monsters.razor`, `Pages/Notes.razor`, `Pages/Races.razor`, `Pages/Spells.razor`
- Modify (FAB): gli stessi 5 + `Pages/Characters.razor`

**Interfaces:**
- Consumes: `Shared/LoadingSpinner.razor` (`[Parameter] string? Text`).
- Produces: nessuna API nuova (solo markup).

- [ ] **Step 1: Sostituire i 5 "Caricamento..." testuali con `<LoadingSpinner>`**

In ciascuna pagina, sostituire il blocco
```razor
                @if (isLoading)
                {
                    <span>Caricamento...</span>
                }
                else
```
con
```razor
                @if (isLoading)
                {
                    <LoadingSpinner Text="@LOADING_TEXT" />
                }
                else
```
dove `LOADING_TEXT` è, per pagina: Spells → `"Caricamento incantesimi..."`, Monsters → `"Caricamento mostri..."`, Classes → `"Caricamento classi..."`, Races → `"Caricamento razze..."`, Notes → `"Caricamento note..."`. (Scrivere la stringa letterale direttamente nell'attributo, es. `<LoadingSpinner Text="Caricamento incantesimi..." />`.)

- [ ] **Step 2: Aggiungere `aria-label` ai 6 FAB**

In ogni FAB, aggiungere `aria-label` accanto a `title` (stesso testo):
- `Pages/Spells.razor:220` → `title="Nuovo incantesimo" aria-label="Nuovo incantesimo"`
- `Pages/Monsters.razor:273` → `... aria-label="Nuovo mostro"`
- `Pages/Races.razor:214` → `... aria-label="Nuova razza"`
- `Pages/Notes.razor:153` → `... aria-label="Nuova nota"`
- `Pages/Classes.razor:229` → `... aria-label="Nuova classe"`
- `Pages/Characters.razor:166` → `... aria-label="Nuovo personaggio"`

- [ ] **Step 3: Build + test**

Run: `dotnet build` → 0 errori. `dotnet test` → tutti verdi (nessun test nuovo qui).

- [ ] **Step 4: Verifica locale (manuale/browser)**

Su `https://localhost:7076`: aprire un catalogo → durante il caricamento appare lo spinner a tema (non più "Caricamento..."); nell'albero di accessibilità (DevTools) i FAB hanno nome "Nuovo …".

---

### Task 2: Validazione di dominio nei form (helper puro + wiring)

**Files:**
- Create: `Services/FormValidation.cs`
- Create: `Tests/FormValidationTests.cs`
- Modify: `Pages/Monsters.razor` (in `SaveFormAsync`), `Pages/Races.razor` (in `SaveFormAsync`)

**Interfaces:**
- Produces:
  - `internal static bool FormValidation.InRange(int value, int min, int max)`
  - `internal static string? FormValidation.ValidateMonster(Monster m)` — primo errore o `null`
  - `internal static string? FormValidation.ValidateRace(Race r)` — primo errore o `null`
- Consumes: `DndCompanion.Models.Monster`, `DndCompanion.Models.Race`.

- [ ] **Step 1: Scrivere il test che fallisce — `Tests/FormValidationTests.cs`**

```csharp
using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// Validazione di dominio lato client per i form (FormValidation): ritorna il primo errore o null.
public class FormValidationTests
{
    private static Monster ValidMonster() => new()
    {
        Name = "Goblin", ArmorClass = 15,
        Strength = 8, Dexterity = 14, Constitution = 10,
        Intelligence = 10, Wisdom = 8, Charisma = 8
    };

    private static Race ValidRace() => new() { Name = "Elfo", Speed = 30 };

    [Fact]
    public void Valid_monster_returns_null() => Assert.Null(FormValidation.ValidateMonster(ValidMonster()));

    [Fact]
    public void Monster_blank_name_is_rejected()
    {
        var m = ValidMonster(); m.Name = "  ";
        Assert.Equal("Il nome è obbligatorio", FormValidation.ValidateMonster(m));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(41)]
    public void Monster_armor_class_out_of_range_is_rejected(int ac)
    {
        var m = ValidMonster(); m.ArmorClass = ac;
        Assert.Equal("La CA deve essere tra 0 e 40", FormValidation.ValidateMonster(m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Monster_ability_score_out_of_range_is_rejected(int score)
    {
        var m = ValidMonster(); m.Strength = score;
        Assert.Equal("Forza: il punteggio deve essere tra 1 e 30", FormValidation.ValidateMonster(m));
    }

    [Fact]
    public void Valid_race_returns_null() => Assert.Null(FormValidation.ValidateRace(ValidRace()));

    [Fact]
    public void Race_blank_name_is_rejected()
    {
        var r = ValidRace(); r.Name = "";
        Assert.Equal("Il nome è obbligatorio", FormValidation.ValidateRace(r));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    public void Race_speed_out_of_range_is_rejected(int speed)
    {
        var r = ValidRace(); r.Speed = speed;
        Assert.Equal("La velocità deve essere tra 0 e 120", FormValidation.ValidateRace(r));
    }

    [Fact]
    public void InRange_is_inclusive()
    {
        Assert.True(FormValidation.InRange(0, 0, 40));
        Assert.True(FormValidation.InRange(40, 0, 40));
        Assert.False(FormValidation.InRange(41, 0, 40));
    }
}
```

- [ ] **Step 2: Eseguire il test → deve fallire (compilazione: `FormValidation` non esiste)**

Run: `dotnet test --filter FullyQualifiedName~FormValidationTests`
Expected: FAIL (build error: il tipo `FormValidation` non esiste).

- [ ] **Step 3: Implementare `Services/FormValidation.cs`**

```csharp
using DndCompanion.Models;

namespace DndCompanion.Services;

// Validazione di dominio lato client per i form (UX, non sicurezza: l'autorità resta RLS).
// Ogni Validate* ritorna il PRIMO messaggio d'errore, o null se i dati sono validi.
internal static class FormValidation
{
    internal static bool InRange(int value, int min, int max) => value >= min && value <= max;

    internal static string? ValidateMonster(Monster m)
    {
        if (string.IsNullOrWhiteSpace(m.Name)) return "Il nome è obbligatorio";
        if (!InRange(m.ArmorClass, 0, 40)) return "La CA deve essere tra 0 e 40";

        var stats = new (string Label, int Value)[]
        {
            ("Forza", m.Strength), ("Destrezza", m.Dexterity), ("Costituzione", m.Constitution),
            ("Intelligenza", m.Intelligence), ("Saggezza", m.Wisdom), ("Carisma", m.Charisma)
        };
        foreach (var (label, value) in stats)
            if (!InRange(value, 1, 30)) return $"{label}: il punteggio deve essere tra 1 e 30";

        return null;
    }

    internal static string? ValidateRace(Race r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Il nome è obbligatorio";
        if (!InRange(r.Speed, 0, 120)) return "La velocità deve essere tra 0 e 120";
        return null;
    }
}
```

- [ ] **Step 4: Eseguire i test → devono passare**

Run: `dotnet test --filter FullyQualifiedName~FormValidationTests`
Expected: PASS (tutti).

- [ ] **Step 5: Wiring in `Pages/Monsters.razor` (`SaveFormAsync`)**

Sostituire il check del solo nome:
```csharp
        if (string.IsNullOrWhiteSpace(editDraft.Name))
        {
            errorMessage = "Il nome è obbligatorio";
            return;
        }
```
con:
```csharp
        var validationError = FormValidation.ValidateMonster(editDraft);
        if (validationError is not null)
        {
            errorMessage = validationError;
            return;
        }
```

- [ ] **Step 6: Wiring in `Pages/Races.razor` (`SaveFormAsync`)**

Sostituire l'analogo check del solo nome con:
```csharp
        var validationError = FormValidation.ValidateRace(editDraft);
        if (validationError is not null)
        {
            errorMessage = validationError;
            return;
        }
```

- [ ] **Step 7: Build + test completi**

Run: `dotnet build` → 0 errori. `dotnet test` → 111 + 9 nuovi verdi.

- [ ] **Step 8: Verifica locale**

Form Mostri: FOR 99 o CA 99 → messaggio chiaro, niente salvataggio; valori validi → salva. Form Razze: Velocità 999 → messaggio; valore valido → salva.

---

### Task 3: CSP in `<meta>` (con hash sugli script inline)

> **AGGIORNAMENTO (2026-06-24, in implementazione):** gli step a hash sotto sono stati **superati**. La verifica
> nel browser ha mostrato che .NET inietta un `<script type="importmap">` auto-generato il cui hash cambia ad
> ogni build → hash fissi insostenibili. Soluzione adottata (scelta utente): **CSP pragmatica** con
> `script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'` + `default-src 'self'`, `connect-src 'self' <supabase>`,
> `object-src 'none'`, `base-uri 'self'`, `img/font-src 'self' data:`, `manifest-src/worker-src 'self'`.
> Verificato in locale: boot pulito (0 violazioni), login Google + CRUD ok.

**Files:**
- Modify: `wwwroot/index.html` (aggiunta `<meta http-equiv="Content-Security-Policy" ...>` nel `<head>`)

**Interfaces:** nessuna (config statica).

- [ ] **Step 1: Aggiungere la meta CSP (prima iterazione, senza hash script)**

Nel `<head>` di `wwwroot/index.html`, dopo `<meta name="viewport" ...>`, inserire (con commento-guida per il ricalcolo futuro degli hash):
```html
    <!--
      Content-Security-Policy (GitHub Pages: solo via <meta>; frame-ancestors/report-uri sono IGNORATI nel meta).
      script-src usa hash SHA-256 sugli script inline di questo file: se modifichi uno script inline, ricalcola
      l'hash (il browser, con questa CSP attiva, stampa in console l'hash atteso "sha256-..." da incollare qui).
      'wasm-unsafe-eval' è obbligatorio per Blazor WebAssembly.
    -->
    <meta http-equiv="Content-Security-Policy" content="default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' https://tbgjwtfmijrcmeracfzh.supabase.co; object-src 'none'; base-uri 'self'; manifest-src 'self'; worker-src 'self'" />
```

- [ ] **Step 2: Avviare l'app in locale e raccogliere gli hash dalle violazioni**

Avviare il dev server (es. `dotnet run` → `https://localhost:7076`). Aprire la pagina con Chrome DevTools (MCP) e leggere i messaggi di console: per ogni script inline bloccato il browser stampa `Refused to execute inline script ... a hash ('sha256-...')`. Raccogliere tutti gli `sha256-...` riportati (attesi: i 3 script inline + eventualmente l'importmap vuoto).

- [ ] **Step 3: Inserire gli hash raccolti in `script-src`**

Aggiornare `script-src` aggiungendo gli hash:
```
script-src 'self' 'wasm-unsafe-eval' 'sha256-AAA...' 'sha256-BBB...' 'sha256-CCC...';
```
(AAA/BBB/CCC = gli hash effettivi letti in console.)

- [ ] **Step 4: Ricaricare e verificare la console pulita**

Reload con DevTools aperto: **nessuna** violazione CSP residua; l'app si carica (spinner Blazor → home); la navigazione tra le pagine funziona; le chiamate a Supabase (caricamento dati) non sono bloccate (connect-src ok).

- [ ] **Step 5: Verifica flussi critici (login + CRUD)**

⚠️ Il login Google reale non è completabile in autonomia (serve l'account dell'utente): questo passo va eseguito/confermato dall'utente, oppure con una sessione già attiva nel browser. Verificare: login Google completo (redirect e ritorno senza blocchi CSP), e un CRUD (es. crea/elimina un incantesimo). Reload finale: service worker e manifest non bloccati (`worker-src`/`manifest-src`).

- [ ] **Step 6: Build (smoke) + test**

Run: `dotnet build` → 0 errori. `dotnet test` → tutti verdi (la CSP non tocca codice C#). Nota: la conferma definitiva è il deploy di produzione (Release), gated dall'ok dell'utente; eventuali violazioni Release-only emergerebbero solo lì.

---

## Self-Review

**Spec coverage:**
- §3 spec (UX/a11y) → Task 1. ✓
- §4 spec (validazione: Mostri AC/caratteristiche, Razze velocità; Personaggi esclusi; helper testabile) → Task 2. ✓ (Classi/Spells già coperti da check esistenti → nessun task, coerente con la spec.)
- §5 spec (CSP con hash + caveat meta) → Task 3. ✓
- Verifiche locali (§6 spec) → Step di verifica in ogni task. ✓

**Placeholder scan:** gli hash `sha256-AAA...` in Task 3 NON sono placeholder dimenticati: sono valori che per definizione si ottengono solo a runtime dal browser (Step 2 li raccoglie). Tutto il resto è codice completo.

**Type consistency:** `FormValidation.ValidateMonster(Monster)`/`ValidateRace(Race)`/`InRange(int,int,int)` usati in modo identico tra test (Task 2 Step 1) e implementazione (Step 3) e call-site (Step 5/6). `Monster`/`Race` property (`ArmorClass`, `Strength..Charisma`, `Speed`, `Name`) verificate sui Model.

## Note di esecuzione
- Commit raggruppati alla fine, solo su ok utente (Global Constraints). Suggerimento di suddivisione finale: 1 commit per task (feat/a11y, feat/validazione+test, feat/csp) + 1 doc (spec/piano/DA-FARE/DIARIO).
