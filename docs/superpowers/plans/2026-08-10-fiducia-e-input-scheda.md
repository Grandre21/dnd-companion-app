# La scheda che non mente, e l'input che non stanca — piano di implementazione

> **Per chi esegue:** questo piano si esegue col **protocollo di `CLAUDE.md`** («Chi scrive il
> codice»): l'orchestratore taglia e reviso, i **Sonnet** scrivono, il gate a due agenti
> (`bug-hunter` + `conformity`) chiude ogni unità. **Non** si usano le skill
> `subagent-driven-development` / `dispatching-parallel-agents`: le istruzioni utente le rimpiazzano.

**Obiettivo:** rendere visibile ogni salvataggio rifiutato dalle RLS, ridurre l'aggiunta di un
oggetto da 12 controlli a 3, dare alle monete i gesti «uso» e «ricevo», e collegare le specie al
manuale.

**Architettura:** nessuna migrazione, nessuna colonna, nessuna RLS. Si estende a tre componenti il
contratto `Func<Task<bool>>` **già in produzione** su `CharacterVitalsBar`/`CharacterCombatTab`; la
logica nuova delle monete entra in `CoinConversion` come helper puro testabile; le specie riusano
`CharacterManualJoin`.

**Stack:** Blazor WebAssembly .NET 10, xUnit, `postgrest-csharp 3.5.1`.

Spec: [`docs/superpowers/specs/2026-08-10-fiducia-e-input-scheda-design.md`](../specs/2026-08-10-fiducia-e-input-scheda-design.md).

## Vincoli globali

Valgono per **ogni** task, senza ripeterli:

- **Non committare.** Gli agenti condividono il working tree: due `git commit` simultanei si
  contendono il lock dell'index. Committa l'orchestratore.
- **Se il build fallisce in file che non sono tuoi**, è un altro agente a metà lavoro: aspetta e
  ritenta, non provare a ripararli.
- **Logica di dominio in helper puri `static`** in `Services/`, mai nel `.razor`. Nel repo non c'è
  component-testing: ciò che non è in `Services/` non è testabile.
- **Toast `.app-toast`** (mai `.toast`), `ConfirmDialog` (mai `confirm()`), design token in `:root`.
- **a11y**: controlli interattivi con `role`/`tabindex`/`aria-*` e gestione Enter/Space;
  `aria-label` sui pulsanti icona-pura. Bersaglio dito **≥44px**.
- **CSS isolato**: lo scope del genitore **non raggiunge i figli**, `@media` comprese.
- **Mobile-first**: base per il telefono, enhancement in `@media (min-width: 641px)`.
- **Nessuna dipendenza nuova** (trimming `full` attivo).
- Verifica finale di ogni task: `dotnet build` (0 warning / 0 errori) e
  `dotnet test Tests/DndCompanion.Tests.csproj`.

## Partizione e ordine

| Unità | Agente | File di proprietà | Dipendenze |
|---|---|---|---|
| **A — esito** | 1 | `Shared/StatCard.razor`, `Shared/CharacterTabs/CharacterStatsTab.razor`, `Shared/CharacterTabs/CharacterMagicTab.razor`, `Pages/Characters.razor` | nessuna |
| **B — zaino e monete** | 2 | `Shared/CharacterTabs/CharacterItemsTab.razor` (+`.css`), `Services/CoinConversion.cs`, `Tests/CoinConversionTests.cs` | nessuna |
| **C1 — helper specie** | 3 | `Services/CharacterManualJoin.cs`, `Tests/CharacterManualJoinTests.cs` | nessuna |
| **C2 — innesto specie** | dopo A | `Shared/CharacterTabs/CharacterBioTab.razor`, `Pages/Characters.razor` | **A e C1** |

**A, B e C1 partono insieme.** C2 tocca `Pages/Characters.razor`, che è di A: **non può essere
parallela ad A**, va lanciata quando A ha finito. Il pezzo di C1 resta codice morto finché C2 non lo
innesta — è previsto, non dimenticato.

**La giuntura** è il contratto `OnChanged`: A e B lo cambiano entrambi, su file diversi. Il gate
finale va puntato lì.

---

# Unità A — i salvataggi che dicono la verità

**Il pattern da replicare esiste ed è commentato**: leggi `Shared/CharacterTabs/CharacterVitalsBar.razor:76-115`
(la terna `Func<Task<bool>> OnChanged` + `EventCallback OnLocalRevert` + `bool IsSaving`) e il
call-site `Pages/Characters.razor:97-99`. Non inventare una variante: replica quella.

Nel genitore **non c'è nulla da scrivere**: `SaveCharacterCoreAsync` (`Pages/Characters.razor:1186`)
è già un `Task<bool>` che controlla il ritorno.

### Task A1: `StatCard` — competenze e tiri salvezza

**File:**
- Modifica: `Shared/StatCard.razor:43` (firma), `:47-52` (`CycleSave`), `:54-64` (`CycleSkill`)
- Modifica: `Pages/Characters.razor:222-223` (call-site)

**Interfacce:**
- Consuma: `SaveCharacterCoreAsync()` → `Task<bool>` da `Pages/Characters.razor:1186`
- Produce: `StatCard` con `Func<Task<bool>> OnChanged` + `EventCallback OnLocalRevert`

- [ ] **Passo 1: cambiare la firma del parametro**

In `Shared/StatCard.razor`, sostituisci la riga 43:

```csharp
    [Parameter] public EventCallback OnChanged { get; set; }
```

con:

```csharp
    /// <summary>Deve riportare l'ESITO (non un EventCallback): un update rifiutato dalle RLS non
    /// solleva eccezioni — PostgREST aggiorna zero righe e risponde [] — quindi senza l'esito una
    /// competenza mai confermata resterebbe a schermo e verrebbe poi persistita in silenzio dal
    /// primo salvataggio riuscito da qualunque altro punto della scheda. Stesso contratto di
    /// CharacterVitalsBar.OnChanged (v. il commento lì).</summary>
    [Parameter, EditorRequired] public Func<Task<bool>> OnChanged { get; set; } = default!;

    /// <summary>Da invocare dopo aver riportato Character allo stato pre-tentativo: questo
    /// componente si ridisegna da sé (è il receiver del proprio click), ma il foglio che lo ospita
    /// no. Bound dal genitore a StateHasChanged su di sé.</summary>
    [Parameter] public EventCallback OnLocalRevert { get; set; }
```

- [ ] **Passo 2: `CycleSave` cattura, tenta, ripristina**

Sostituisci il metodo `CycleSave` (righe ~47-52) con:

```csharp
    private async Task CycleSave()
    {
        if (!IsEditMode) return;
        // Il valore va catturato PRIMA dell'await: fra un await e l'altro Blazor smista gli eventi,
        // e ripristinare rileggendo la proprietà scriverebbe il valore sbagliato.
        var precedente = GetSaveProf(Character, Ability);
        SetSaveProf(Character, Ability, !precedente);
        if (!await OnChanged())
        {
            SetSaveProf(Character, Ability, precedente);
            await OnLocalRevert.InvokeAsync();
        }
    }
```

- [ ] **Passo 3: `CycleSkill` cattura entrambi i flag**

Sostituisci `CycleSkill` (righe ~54-64) con:

```csharp
    private async Task CycleSkill(SkillType skill)
    {
        if (!IsEditMode) return;
        var prof = GetSkillProf(Character, skill);
        var exp = GetSkillExp(Character, skill);
        // Ciclo: non competente -> competente -> expertise -> non competente
        if (!prof && !exp) { SetSkillProf(Character, skill, true); }
        else if (prof && !exp) { SetSkillExp(Character, skill, true); }
        else { SetSkillProf(Character, skill, false); SetSkillExp(Character, skill, false); }

        if (!await OnChanged())
        {
            // Ripristino di ENTRAMBI i flag: il ciclo ne tocca uno o due a seconda del ramo, e
            // rimetterne solo uno lascerebbe uno stato che nessun ramo del ciclo produce.
            SetSkillProf(Character, skill, prof);
            SetSkillExp(Character, skill, exp);
            await OnLocalRevert.InvokeAsync();
        }
    }
```

- [ ] **Passo 4: aggiornare il call-site**

In `Pages/Characters.razor`, righe 222-223, sostituisci:

```razor
                                <StatCard Ability="@sheetCaratteristica.Value" Character="@selected"
                                          IsEditMode="@CanEdit" OnChanged="@SaveCharacterAsync" />
```

con:

```razor
                                <StatCard Ability="@sheetCaratteristica.Value" Character="@selected"
                                          IsEditMode="@CanEdit" OnChanged="@SaveCharacterCoreAsync"
                                          OnLocalRevert="@(() => StateHasChanged())" />
```

- [ ] **Passo 5: build**

Esegui: `dotnet build`
Atteso: **0 warning / 0 errori**. Se un call-site avesse ignorato l'esito, qui non compilerebbe: è
la verifica strutturale che sostituisce il test che non possiamo scrivere.

### Task A2: `CharacterMagicTab` — slot incantesimo

**File:**
- Modifica: `Shared/CharacterTabs/CharacterMagicTab.razor:97` (firma), `:147-161` (`ToggleSpellSlot`)
- Modifica: `Pages/Characters.razor:328` (call-site)

**Interfacce:**
- Consuma: `SaveCharacterCoreAsync()` → `Task<bool>`
- Produce: `CharacterMagicTab` con `Func<Task<bool>> OnChanged` + `EventCallback OnLocalRevert`

- [ ] **Passo 1: cambiare la firma**

In `Shared/CharacterTabs/CharacterMagicTab.razor`, sostituisci la riga 97:

```csharp
    [Parameter] public EventCallback OnChanged { get; set; }
```

con:

```csharp
    /// <summary>Deve riportare l'ESITO: uno slot speso e rifiutato dalle RLS resterebbe segnato a
    /// schermo — e al tavolo uno slot creduto speso è peggio di un errore, perché non si rigioca.
    /// Stesso contratto di CharacterVitalsBar.OnChanged.</summary>
    [Parameter, EditorRequired] public Func<Task<bool>> OnChanged { get; set; } = default!;

    /// <summary>Da invocare dopo il ripristino: v. CharacterVitalsBar.OnLocalRevert.</summary>
    [Parameter] public EventCallback OnLocalRevert { get; set; }
```

- [ ] **Passo 2: `ToggleSpellSlot` ripristina il valore precedente**

Sostituisci il metodo (righe ~147-161) con:

```csharp
    private async Task ToggleSpellSlot(int level, int index)
    {
        if (!CanEdit) return;
        var max = GetSpellSlotMax(Character, level);
        if (max <= 0) return;
        var used = GetSpellSlotUsed(Character, level);
        var available = max - used;
        // Click su uno slot disponibile: spende fino a quel punto (ne restano index-1).
        // Click su uno slot già speso: lo ripristina (ne restano index).
        var newUsed = index <= available ? max - (index - 1) : max - index;
        newUsed = Math.Clamp(newUsed, 0, max);
        if (newUsed == used) return;
        SetSpellSlotUsed(Character, level, newUsed);
        if (!await OnChanged())
        {
            SetSpellSlotUsed(Character, level, used);
            await OnLocalRevert.InvokeAsync();
        }
    }
```

- [ ] **Passo 3: aggiornare il call-site**

In `Pages/Characters.razor` riga 328, sostituisci `OnChanged="@SaveCharacterAsync"` con
`OnChanged="@SaveCharacterCoreAsync"` e aggiungi `OnLocalRevert="@(() => StateHasChanged())"` fra i
parametri del `<CharacterMagicTab>`.

- [ ] **Passo 4: build**

Esegui: `dotnet build` → atteso **0/0**.

### Task A3: `SaveNotesAsync`, il parametro morto e l'alias

**File:**
- Modifica: `Pages/Characters.razor:1087-1108` (`SaveNotesAsync`), `:300` (call-site StatsTab), `:1221` (alias)
- Modifica: `Shared/CharacterTabs/CharacterStatsTab.razor:21`

**Interfacce:**
- Produce: `SaveCharacterAsync` **rimosso** — nessun chiamante residuo dopo A1/A2.

- [ ] **Passo 1: il ramo `else` che manca**

In `Pages/Characters.razor`, dentro `SaveNotesAsync`, il `try` è oggi:

```csharp
            var saved = await CharacterRepository.UpdateCharacterAsync(selected);
            if (saved is not null)
            {
                selected.Notes = saved.Notes;
                SyncListFromSelected();
            }
```

Sostituiscilo con:

```csharp
            var saved = await CharacterRepository.UpdateCharacterAsync(selected);
            if (saved is null)
            {
                // Rifiuto RLS: zero righe aggiornate, risposta [], NESSUNA eccezione — quindi il
                // catch qui sotto non scatta. Senza questo ramo le note restavano a schermo e mai
                // sul server, e la prossima riapertura della scheda le trovava sparite.
                selected.Notes = previous;
                errorMessage = "Il server non ha restituito il personaggio salvato.";
                return;
            }

            selected.Notes = saved.Notes;
            SyncListFromSelected();
```

- [ ] **Passo 2: rimuovere il parametro morto di `CharacterStatsTab`**

`CharacterStatsTab` non modifica nulla — apre solo il foglio via `OnApriDettaglio`. Il suo
`OnChanged` non è invocato da nessuna riga del componente.

In `Shared/CharacterTabs/CharacterStatsTab.razor` **elimina** la riga 21:

```csharp
    [Parameter] public EventCallback OnChanged { get; set; }
```

In `Pages/Characters.razor` riga 300, **elimina** `OnChanged="@SaveCharacterAsync"` dal
`<CharacterStatsTab ... />`.

- [ ] **Passo 3: rimuovere l'alias ormai morto**

Verifica che non resti nessun chiamante:

```bash
grep -rn "SaveCharacterAsync" --include=*.razor .
```

Atteso: **solo** le occorrenze dentro i commenti (`Characters.razor:707`, `:1181`, `:1227`,
`CharacterCombatTab.razor:442`, `CharacterItemsTab.razor:512`). **Se compare un call-site reale,
fermati e segnalalo** — significa che B non ha ancora convertito il suo, e la rimozione va
posticipata.

Se non ce ne sono, elimina la riga 1221 di `Pages/Characters.razor`:

```csharp
    private Task SaveCharacterAsync() => SaveCharacterCoreAsync();
```

e aggiorna i commenti che lo citano perché non mentano: dove dicono «il wrapper `SaveCharacterAsync`»
va detto che i tab usano ora direttamente `SaveCharacterCoreAsync`.

- [ ] **Passo 4: build e test**

Esegui: `dotnet build` → **0/0**, poi `dotnet test Tests/DndCompanion.Tests.csproj` → tutti verdi.

**Nota per l'orchestratore:** il Passo 3 dipende da B (che converte l'ultimo call-site,
`CharacterItemsTab`). Se B non ha finito, A3 lascia l'alias e lo rimuove l'orchestratore alla
giuntura.

---

# Unità B — lo zaino e le monete

### Task B1: `CoinConversion.Incassa`

**File:**
- Modifica: `Services/CoinConversion.cs` (in fondo, accanto a `Spendi`)
- Modifica: `Tests/CoinConversionTests.cs`

**Interfacce:**
- Produce:
  - `public sealed class EsitoIncasso` con `PlatinumPieces`/`GoldPieces`/`ElectrumPieces`/`SilverPieces`/`CopperPieces` (`int`, `init`)
  - `public static EsitoIncasso Incassa(int platino, int oro, int electrum, int argento, int rame, int incassoPlatino, int incassoOro, int incassoElectrum, int incassoArgento, int incassoRame)`
  - `public static EsitoIncasso Incassa(Character c, int incassoPlatino, int incassoOro, int incassoElectrum, int incassoArgento, int incassoRame)`
  - `public static void Applica(Character c, EsitoIncasso esito)`

- [ ] **Passo 1: scrivere i test che falliscono**

In `Tests/CoinConversionTests.cs` aggiungi:

```csharp
    [Fact]
    public void Incassa_SommaSoloIlTaglioRicevuto_ELasciaGliAltriIntatti()
    {
        // 15 ma è il valore che rende il test non vacuo: se Incassa ricompattasse (come fa
        // Compatta), 15 ma diventerebbero 1 mo + 5 ma e l'asserzione sull'argento fallirebbe.
        // È la stessa proprietà che regge Spendi: i tagli non coinvolti restano come erano.
        var esito = CoinConversion.Incassa(0, 2, 0, 15, 3, 0, 30, 0, 0, 0);

        Assert.Equal(32, esito.GoldPieces);
        Assert.Equal(15, esito.SilverPieces);
        Assert.Equal(3, esito.CopperPieces);
        Assert.Equal(0, esito.PlatinumPieces);
        Assert.Equal(0, esito.ElectrumPieces);
    }

    [Fact]
    public void Incassa_IlTotaleInRameCresceEsattamenteDellIncasso()
    {
        var prima = CoinConversion.TotaleInRame(1, 2, 3, 4, 5);
        var esito = CoinConversion.Incassa(1, 2, 3, 4, 5, 0, 7, 0, 0, 0);
        var dopo = CoinConversion.TotaleInRame(
            esito.PlatinumPieces, esito.GoldPieces, esito.ElectrumPieces,
            esito.SilverPieces, esito.CopperPieces);

        Assert.Equal(prima + 7 * 100, dopo);
    }

    [Fact]
    public void Incassa_ValoriNegativiContanoComeZero()
    {
        // Il DB non ha vincoli CHECK sulle valute: stessa difesa di TotaleInRame e Spendi.
        var esito = CoinConversion.Incassa(0, -5, 0, 0, 0, 0, 10, 0, 0, 0);

        Assert.Equal(10, esito.GoldPieces);
    }

    [Fact]
    public void Incassa_OltreIlMassimoDiInt_Clampa()
    {
        var esito = CoinConversion.Incassa(0, int.MaxValue, 0, 0, 0, 0, 100, 0, 0, 0);

        Assert.Equal(int.MaxValue, esito.GoldPieces);
    }
```

- [ ] **Passo 2: eseguire i test e vederli fallire**

Esegui: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~Incassa"`
Atteso: **FAIL** con errore di compilazione — `Incassa` non esiste.

- [ ] **Passo 3: implementare**

In `Services/CoinConversion.cs`, dopo `Applica(Character, EsitoSpesa)`:

```csharp
    /// <summary>
    /// Aggiunge monete al gruzzolo, <b>senza compattare e senza convertire</b>: chi riceve 30 mo si
    /// ritrova 30 mo in più, non un platino e degli spiccioli. È la stessa scelta di gioco di
    /// <see cref="Spendi"/> — i tagli non coinvolti restano esattamente come erano — e il motivo per
    /// cui questo non è un caso particolare di <see cref="Compatta(int,int,int,int,int)"/>.
    ///
    /// Valute negative (il DB non ha vincoli CHECK) contano come 0, come in
    /// <see cref="TotaleInRame(int,int,int,int,int)"/>. La somma è in <c>long</c> e viene clampata a
    /// <see cref="int.MaxValue"/>: le colonne sono <c>integer</c>, e un traboccamento silenzioso
    /// scriverebbe un negativo sul personaggio.
    /// </summary>
    public static EsitoIncasso Incassa(int platino, int oro, int electrum, int argento, int rame,
                                       int incassoPlatino, int incassoOro, int incassoElectrum,
                                       int incassoArgento, int incassoRame)
        => new()
        {
            PlatinumPieces = SommaClamp(platino, incassoPlatino),
            GoldPieces = SommaClamp(oro, incassoOro),
            ElectrumPieces = SommaClamp(electrum, incassoElectrum),
            SilverPieces = SommaClamp(argento, incassoArgento),
            CopperPieces = SommaClamp(rame, incassoRame),
        };

    public static EsitoIncasso Incassa(Character c, int incassoPlatino, int incassoOro,
                                       int incassoElectrum, int incassoArgento, int incassoRame) =>
        Incassa(c.PlatinumPieces, c.GoldPieces, c.ElectrumPieces, c.SilverPieces, c.CopperPieces,
                incassoPlatino, incassoOro, incassoElectrum, incassoArgento, incassoRame);

    /// <summary>Scrive l'esito sul personaggio: mutazione in place, nessuna I/O — il salvataggio
    /// resta a chi chiama, come per gli altri Applica di questa classe.</summary>
    public static void Applica(Character c, EsitoIncasso esito)
    {
        c.PlatinumPieces = esito.PlatinumPieces;
        c.GoldPieces = esito.GoldPieces;
        c.ElectrumPieces = esito.ElectrumPieces;
        c.SilverPieces = esito.SilverPieces;
        c.CopperPieces = esito.CopperPieces;
    }

    private static int SommaClamp(int posseduto, int aggiunto)
    {
        var somma = (long)Math.Max(0, posseduto) + Math.Max(0, aggiunto);
        return somma > int.MaxValue ? int.MaxValue : (int)somma;
    }
```

E la classe dell'esito, accanto a `EsitoSpesa`:

```csharp
/// <summary>Esito di un incasso: i cinque tagli dopo l'aggiunta. Non ha un flag Riuscita — un
/// incasso non può fallire, a differenza di una spesa che può non trovare i fondi.</summary>
public sealed class EsitoIncasso
{
    public int PlatinumPieces { get; init; }
    public int GoldPieces { get; init; }
    public int ElectrumPieces { get; init; }
    public int SilverPieces { get; init; }
    public int CopperPieces { get; init; }
}
```

- [ ] **Passo 4: eseguire i test e vederli passare**

Esegui: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~Incassa"`
Atteso: **4 test PASS**.

- [ ] **Passo 5: prova per mutazione**

Questo repo ha già incontrato **quattro** test vacui, e gli ultimi due nascevano da un'istruzione
dell'orchestratore. Verifica che questi non lo siano:

1. Sostituisci il corpo di `SommaClamp` con `return Math.Max(0, posseduto);` (cioè: ignora l'incasso).
2. Esegui i test → devono diventare **ROSSI**.
3. Ripristina l'implementazione corretta.
4. Riesegui → **VERDI**.

Se restano verdi al passo 2, i test non sorvegliano nulla: fermati e segnalalo.

### Task B2: il form dello zaino a tre campi

**File:**
- Modifica: `Shared/CharacterTabs/CharacterItemsTab.razor:20-115` (form di aggiunta)
- Modifica: `Shared/CharacterTabs/CharacterItemsTab.razor.css`

- [ ] **Passo 1: raggruppare i campi**

Nel form di aggiunta (che parte da riga ~20), lascia **fuori da qualunque contenitore richiudibile**
i tre campi: **Nome**, **Quantità**, **Tipo** (il `<select>` bindato a `newItemDraft.ItemType`).

Avvolgi **tutto il resto** — Peso, Descrizione, e il blocco `@if (IsWeaponType(newItemDraft.ItemType))`
per intero — in:

```razor
                    <button type="button" class="item-details-toggle"
                            @onclick="() => showNewItemDetails = !showNewItemDetails"
                            aria-expanded="@showNewItemDetails.ToString().ToLowerInvariant()">
                        @(showNewItemDetails ? "− Meno dettagli" : "+ Altri dettagli")
                    </button>

                    @if (showNewItemDetails)
                    {
                        @* Peso, Descrizione e il blocco arma, invariati *@
                    }
```

Aggiungi il campo di stato accanto a `newItemDraft`:

```csharp
    // Il pannello nasce chiuso a ogni apertura del form: il gesto comune è «3 pozioni», e chi deve
    // compilare un'arma per intero lo apre. OpenAddItemForm lo rimette a false (v. sotto).
    private bool showNewItemDetails;
```

e in `OpenAddItemForm`, dopo l'inizializzazione di `newItemDraft`, aggiungi:

```csharp
        showNewItemDetails = false;
```

- [ ] **Passo 2: lo stile del toggle**

In `Shared/CharacterTabs/CharacterItemsTab.razor.css` aggiungi:

```css
/* Bersaglio dito: 44px pieni (WCAG 2.2 AA chiede 24, il progetto usa 44 come minimo comodo). */
.item-details-toggle {
    width: 100%;
    min-height: 44px;
    background: transparent;
    border: 1px dashed rgba(var(--gold-rgb), 0.35);
    border-radius: 6px;
    color: var(--gold-muted);
    font-size: 0.9rem;
    cursor: pointer;
    margin: 0.5rem 0;
}

.item-details-toggle:hover {
    border-color: rgba(var(--gold-rgb), 0.6);
    color: var(--gold);
}
```

**Non** usare literal esadecimali: solo token. Verifica che `--gold-rgb` e `--gold-muted` esistano in
`wwwroot/css/app.css` prima di usarli; se un nome non c'è, usa quelli che ci sono — **non
inventarne**, e non confondere `--gold-muted` con `--text-body` (sono colori diversi per un punto sul
canale rosso).

- [ ] **Passo 3: build e verifica a vista**

Esegui: `dotnet build` → **0/0**.
Verifica che salvando con solo Nome + Tipo=«Arma» la voce compaia nel tab Combattimento (il bonus
d'attacco vuoto attiva `WeaponCalculations.BonusAttacco`, v. il placeholder a riga 60).

### Task B3: «Usa» e «Ricevi» con quantità + taglio

**File:**
- Modifica: `Shared/CharacterTabs/CharacterItemsTab.razor:513` (firma `OnChanged`), `:880-990` (pannelli monete)
- Modifica: `Shared/CharacterTabs/CharacterItemsTab.razor.css`

**Interfacce:**
- Consuma: `CoinConversion.Incassa`/`Applica` dal Task B1; `SaveCharacterCoreAsync()` → `Task<bool>`
- Produce: `CharacterItemsTab` con `Func<Task<bool>> OnChanged` + `EventCallback OnLocalRevert`

- [ ] **Passo 1: convertire il contratto e i tre punti ciechi**

Sostituisci la riga 513:

```csharp
    /// <summary>Persistenza di campi Character (denaro/sintonie): il genitore fa SaveCharacterAsync.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }
```

con:

```csharp
    /// <summary>Persistenza di campi Character (denaro/sintonie). Deve riportare l'ESITO: un update
    /// rifiutato dalle RLS non solleva eccezioni, quindi senza controllarlo la scheda mostrerebbe un
    /// borsello che il server non ha. Stesso contratto di CharacterVitalsBar.OnChanged.</summary>
    [Parameter, EditorRequired] public Func<Task<bool>> OnChanged { get; set; } = default!;

    /// <summary>Da invocare dopo il ripristino: v. CharacterVitalsBar.OnLocalRevert.</summary>
    [Parameter] public EventCallback OnLocalRevert { get; set; }
```

Poi correggi i **tre** call-site, ognuno catturando lo stato **prima** dell'`await`:

- `SaveMoney` (~899, chiamata a 909)
- `ConfermaSpesaAsync` (~945, chiamata a 961)
- `SetAttunementAsync` (~972, chiamata a 983)

Schema per tutti e tre — esempio su `SaveMoney`:

```csharp
        var precedente = new[] { Character.PlatinumPieces, Character.GoldPieces,
                                 Character.ElectrumPieces, Character.SilverPieces,
                                 Character.CopperPieces };
        // … mutazione esistente …
        if (!await OnChanged())
        {
            Character.PlatinumPieces = precedente[0];
            Character.GoldPieces = precedente[1];
            Character.ElectrumPieces = precedente[2];
            Character.SilverPieces = precedente[3];
            Character.CopperPieces = precedente[4];
            await OnLocalRevert.InvokeAsync();
            return;
        }
```

Per `SetAttunementAsync` cattura il valore della sintonia dello slot toccato, non delle monete.

- [ ] **Passo 2: aggiungere il pannello «Ricevi»**

Accanto al pulsante che apre la spesa, aggiungi il gemello. Introduci lo stato:

```csharp
    private bool isReceivingMoney;
    private int receiveAmount;
    private string receiveDenom = "mo";   // il taglio più comune al tavolo
```

E il pannello, sul modello di `isSpendingMoney` (righe ~899-955) ma **senza** anteprima di fallimento
— un incasso non può fallire:

```razor
else if (isReceivingMoney)
{
    var esito = CoinConversion.Incassa(Character,
        receiveDenom == "mp" ? receiveAmount : 0,
        receiveDenom == "mo" ? receiveAmount : 0,
        receiveDenom == "me" ? receiveAmount : 0,
        receiveDenom == "ma" ? receiveAmount : 0,
        receiveDenom == "mr" ? receiveAmount : 0);

    <div class="money-spend">
        <p class="money-spend-title">Quanto ricevi?</p>
        <div class="money-quick">
            <input type="number" min="0" class="input money-quick-amount"
                   aria-label="Quantità ricevuta" @bind="receiveAmount" />
            <select class="input money-quick-denom" aria-label="Taglio" @bind="receiveDenom">
                <option value="mp">MP</option>
                <option value="mo">MO</option>
                <option value="me">ME</option>
                <option value="ma">MA</option>
                <option value="mr">MR</option>
            </select>
        </div>

        @if (receiveAmount > 0)
        {
            <p class="money-spend-result">
                Ora hai: @CoinConversion.FormattaTotaleInOro(CoinConversion.TotaleInRame(
                    esito.PlatinumPieces, esito.GoldPieces, esito.ElectrumPieces,
                    esito.SilverPieces, esito.CopperPieces)) mo
            </p>
        }

        <div class="money-editor-actions">
            <button type="button" class="money-cancel-btn" @onclick="AnnullaIncasso">Annulla</button>
            <button type="button" class="money-save-btn" @onclick="ConfermaIncassoAsync"
                    disabled="@(receiveAmount <= 0)">Conferma</button>
        </div>
    </div>
}
```

E i metodi:

```csharp
    private void AnnullaIncasso()
    {
        isReceivingMoney = false;
        receiveAmount = 0;
    }

    private async Task ConfermaIncassoAsync()
    {
        if (!CanEdit || receiveAmount <= 0) return;

        var precedente = new[] { Character.PlatinumPieces, Character.GoldPieces,
                                 Character.ElectrumPieces, Character.SilverPieces,
                                 Character.CopperPieces };

        var esito = CoinConversion.Incassa(Character,
            receiveDenom == "mp" ? receiveAmount : 0,
            receiveDenom == "mo" ? receiveAmount : 0,
            receiveDenom == "me" ? receiveAmount : 0,
            receiveDenom == "ma" ? receiveAmount : 0,
            receiveDenom == "mr" ? receiveAmount : 0);
        CoinConversion.Applica(Character, esito);

        if (!await OnChanged())
        {
            Character.PlatinumPieces = precedente[0];
            Character.GoldPieces = precedente[1];
            Character.ElectrumPieces = precedente[2];
            Character.SilverPieces = precedente[3];
            Character.CopperPieces = precedente[4];
            await OnLocalRevert.InvokeAsync();
            return;
        }

        isReceivingMoney = false;
        receiveAmount = 0;
    }
```

- [ ] **Passo 3: la stessa coppia quantità+taglio nella spesa**

Dichiara i tre campi di stato accanto a quelli dell'incasso:

```csharp
    private int spendAmount;
    private string spendDenom = "mo";
    private bool showSpendMulti;
```

`AnnullaSpesa` e `ConfermaSpesaAsync` devono azzerare **anche** questi (`spendAmount = 0;
showSpendMulti = false;`), altrimenti la spesa successiva si apre precompilata con l'importo di
quella precedente — e un «Conferma» distratto paga due volte.

Nel pannello `isSpendingMoney`, sostituisci le cinque caselle con la stessa coppia
quantità+taglio dell'incasso (`spendAmount` + `spendDenom`, stesse classi `.money-quick*`,
stessi `aria-label`), e **conserva** le cinque caselle esistenti dietro un toggle:

```razor
        <button type="button" class="item-details-toggle"
                @onclick="() => showSpendMulti = !showSpendMulti"
                aria-expanded="@showSpendMulti.ToString().ToLowerInvariant()">
            @(showSpendMulti ? "− Un taglio solo" : "+ Più tagli")
        </button>
```

`AnteprimaSpesa()` va adattata a leggere dalla coppia quando il toggle è chiuso e dalle cinque
caselle quando è aperto — ma il **corpo** dell'anteprima («non hai spiccioli: si rompe 1 mo», «ti
restano…») **non si tocca**: chiama già `CoinConversion.Spendi` con la quintupla, e «12 mo» è
semplicemente `Spendi(0,12,0,0,0)`. A toggle chiuso le altre quattro quantità valgono 0.

⚠️ **Le due sorgenti non vanno sommate.** Se l'utente scrive 12 nella coppia, apre «più tagli» e
scrive altro nelle caselle, deve valere **una sola** delle due — quella visibile. Sommarle
significherebbe far pagare un importo che non è mai stato mostrato a schermo.

- [ ] **Passo 4: stile della coppia**

```css
.money-quick {
    display: flex;
    gap: 0.5rem;
    align-items: stretch;
}

.money-quick-amount { flex: 1 1 auto; min-height: 44px; }
.money-quick-denom  { flex: 0 0 5.5rem; min-height: 44px; }
```

- [ ] **Passo 5: aggiornare il call-site nel genitore**

⚠️ `Pages/Characters.razor` è di proprietà dell'**unità A**. **Non modificarlo.** Segnala
all'orchestratore che il call-site di riga 321 va portato a
`OnChanged="@SaveCharacterCoreAsync"` + `OnLocalRevert="@(() => StateHasChanged())"`.

- [ ] **Passo 6: build e test**

Esegui: `dotnet build` (fallirà finché l'orchestratore non aggiorna il call-site: è atteso) e
`dotnet test Tests/DndCompanion.Tests.csproj`.

---

# Unità C — le specie collegate al manuale

### Task C1: `CharacterManualJoin.SpecieRiconosciuta`

**File:**
- Modifica: `Services/CharacterManualJoin.cs` (in fondo, accanto a `BackgroundRiconosciuto`)
- Modifica: `Tests/CharacterManualJoinTests.cs`

**Interfacce:**
- Produce: `public static PackageSpecies? SpecieRiconosciuta(string? nomeSpecie, IReadOnlyList<PackageSpecies> catalogo)`

- [ ] **Passo 1: scrivere i test che falliscono**

```csharp
    [Fact]
    public void SpecieRiconosciuta_MatchEsattoNormalizzato()
    {
        var catalogo = new List<PackageSpecies>
        {
            new() { Id = "srd-2024-it/nano", Name = "Nano", Description = "…" },
            new() { Id = "srd-2024-it/elfo", Name = "Elfo", Description = "…" },
        };

        Assert.Equal("Nano", CharacterManualJoin.SpecieRiconosciuta("  nano ", catalogo)?.Name);
        Assert.Equal("Elfo", CharacterManualJoin.SpecieRiconosciuta("ELFO", catalogo)?.Name);
    }

    [Fact]
    public void SpecieRiconosciuta_NomeConAccento_SiRiconosce()
    {
        // Il progetto compila con InvariantGlobalization=true: String.Normalize è un no-op
        // SILENZIOSO, quindi il match deve passare da CatalogKey.NormalizeName, che piega gli
        // accenti con una mappa scritta a mano. Questo è il caso che rende il test non vacuo.
        var catalogo = new List<PackageSpecies>
        {
            new() { Id = "x/mezzelfo", Name = "Mezzelfo", Description = "…" },
        };

        Assert.NotNull(CharacterManualJoin.SpecieRiconosciuta("mezzélfo", catalogo));
    }

    [Fact]
    public void SpecieRiconosciuta_NomeScrittoAMano_TornaNull()
    {
        var catalogo = new List<PackageSpecies>
        {
            new() { Id = "x/nano", Name = "Nano", Description = "…" },
        };

        Assert.Null(CharacterManualJoin.SpecieRiconosciuta("Nanetto delle Colline", catalogo));
        Assert.Null(CharacterManualJoin.SpecieRiconosciuta("", catalogo));
        Assert.Null(CharacterManualJoin.SpecieRiconosciuta(null, catalogo));
    }
```

- [ ] **Passo 2: eseguirli e vederli fallire**

Esegui: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~SpecieRiconosciuta"`
Atteso: **FAIL**, metodo inesistente.

- [ ] **Passo 3: implementare**

```csharp
    /// <summary>La specie del catalogo il cui nome combacia <b>esattamente</b> (normalizzato) con
    /// <paramref name="nomeSpecie"/>; <c>null</c> se nessuna. <c>Character.Race</c> è un campo
    /// singolo, non testo libero: il match è esatto, non "parola intera" come per i talenti —
    /// stessa forma di <see cref="BackgroundRiconosciuto"/>.</summary>
    public static PackageSpecies? SpecieRiconosciuta(
        string? nomeSpecie, IReadOnlyList<PackageSpecies> catalogo)
    {
        if (string.IsNullOrWhiteSpace(nomeSpecie) || catalogo is null) return null;

        var chiave = CatalogKey.NormalizeName(nomeSpecie);
        return catalogo.FirstOrDefault(s => CatalogKey.NormalizeName(s.Name) == chiave);
    }
```

- [ ] **Passo 4: eseguirli e vederli passare**

Atteso: **3 test PASS**. Poi `dotnet build` → **0/0**.

**Nota:** a questo punto il metodo non ha chiamanti. È previsto — lo innesta il Task C2.

### Task C2: innesto nella scheda (**dopo A e C1**)

**File:**
- Modifica: `Shared/CharacterTabs/CharacterBioTab.razor:25-31` (zona blocchi manuale), `:331` (parametri)
- Modifica: `Pages/Characters.razor:399-400` (campi), `:418` (proprietà), `:625-626` (caricamento), `:~310` (call-site BioTab)

**Interfacce:**
- Consuma: `CharacterManualJoin.SpecieRiconosciuta` dal Task C1

- [ ] **Passo 1: caricare le specie nel genitore**

In `Pages/Characters.razor`, accanto a `packageBackgrounds` (riga 400):

```csharp
    private List<PackageSpecies> packageSpecies = new();
```

Accanto a `packageBackgrounds = pacchetto.Backgrounds;` (riga 626):

```csharp
                packageSpecies = pacchetto.Species;
```

E accanto alla proprietà di riga 418:

```csharp
    private PackageSpecies? SpecieDalManuale
        => selected is null ? null : CharacterManualJoin.SpecieRiconosciuta(selected.Race, packageSpecies);
```

- [ ] **Passo 2: passarla al BioTab**

Nel `<CharacterBioTab ... />` aggiungi `SpecieDalManuale="@SpecieDalManuale"`.

- [ ] **Passo 3: riceverla e renderla**

In `Shared/CharacterTabs/CharacterBioTab.razor`, accanto al parametro di riga 331:

```csharp
    /// <summary>La specie del manuale che corrisponde a <see cref="Character.Race"/>; null se
    /// scritta a mano o non a catalogo. Risolta dal genitore, come <see cref="BackgroundDalManuale"/>.</summary>
    [Parameter] public PackageSpecies? SpecieDalManuale { get; set; }
```

E subito **dopo** il blocco del background (righe 25-31), lo stesso markup:

```razor
    @if (SpecieDalManuale is not null && !string.IsNullOrWhiteSpace(SpecieDalManuale.Description))
    {
        <div class="bio-block">
            <p class="bio-block-title">@SpecieDalManuale.Name.ToUpperInvariant()</p>
            <p class="bio-block-text">@SpecieDalManuale.Description</p>
            @if (!string.IsNullOrWhiteSpace(SpecieDalManuale.Traits))
            {
                @* Traits è UNA stringa sola, non un elenco: Scurovisione e Resistenza implacabile
                   non sono separabili automaticamente. Va SOTTO la casella «TRATTI DELLA SPECIE»
                   scritta a mano, non al posto suo. *@
                <p class="bio-block-text">@SpecieDalManuale.Traits</p>
            }
        </div>
    }
```

- [ ] **Passo 4: build e test**

`dotnet build` → **0/0**; `dotnet test Tests/DndCompanion.Tests.csproj` → tutti verdi.

---

## Gate

Per **ogni unità**, al rientro: `bug-hunter` + `conformity` in parallelo, nello stesso messaggio,
sul diff di **quella** unità. Fascia: UI e refactor circoscritti → **1 giro**; l'unità B tocca
serializzazione di valori monetari → fino a **3 giri**.

**Poi il giro sulle giunture**, che non è un doppione: il gate della singola unità non vede questa
classe di difetti per costruzione. Elenca esplicitamente nel brief:

1. **Il contratto `OnChanged`**: A cambia le firme di `StatCard`/`MagicTab`, B quella di `ItemsTab`,
   e i call-site stanno tutti in `Pages/Characters.razor`, che è di A. Un call-site non aggiornato
   **non compila** — ma un `OnLocalRevert` dimenticato compila benissimo e lascia il fratello con un
   valore stantio a schermo.
2. **`SaveCharacterAsync` rimosso da A3** mentre B ne era ancora un chiamante.
3. **`CoinConversion.Incassa` scritto da B** e usato solo da B: verifica che `Applica(EsitoIncasso)`
   non collida per overload con `Applica(EsitoSpesa)`/`Applica(EsitoCompattazione)`.
4. **C1 senza chiamanti** finché C2 non innesta: verifica che C2 sia stato fatto e non lasciato
   indietro — un helper testato che nessuno chiama è già successo due volte su questo repo.

## Verifiche manuali che il gate non può coprire

Da fare **prima** di qualunque push, e da riferire all'utente in chat:

1. **Login effettivo** e apertura di una scheda con dati: le deserializzazioni Gotrue/Postgrest si
   esercitano solo lì, non nella schermata di login.
2. **Il ripristino su rifiuto RLS** non è simulabile in locale con un solo account. Va provato con un
   secondo account non-master su un PG altrui, oppure accettato come non verificato e dichiarato.
3. **Il form a tre campi** su telefono vero: che il toggle «Altri dettagli» sia raggiungibile col
   pollice e che il pannello aperto non spinga i pulsanti sotto la barra di navigazione.
4. **Incasso e spesa** con tagli misti, controllando che l'anteprima resti coerente col toggle
   «più tagli» aperto e chiuso.

## Cosa questo piano NON fa

Riquadro PROVE con le 18 abilità, glossario delle regole base, descrizioni dei privilegi di classe
(bloccate sulla fonte: nel pacchetto SRD `levels[].features` è `List<string>`). Sono il giro
successivo — v. §6 dello spec.
