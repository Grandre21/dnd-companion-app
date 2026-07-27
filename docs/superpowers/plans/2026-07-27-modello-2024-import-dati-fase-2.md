# Modello 2024 + import dati — Fase 2: import ed export

> **Per chi esegue:** SOTTO-SKILL RICHIESTA: usa `superpowers:subagent-driven-development` (consigliata) o
> `superpowers:executing-plans` per implementare il piano task per task. Gli step usano caselle
> (`- [ ]`) per il tracciamento.

**Obiettivo:** l'app sa caricare un file di pacchetto dentro una campagna con anteprima e resoconto,
riesportare i propri dati, rimuovere un import per provenienza, e materializzare nel database i soli
incantesimi di pacchetto che un personaggio usa davvero. I quattro cataloghi esistenti mostrano le voci
di pacchetto marcate e in sola lettura, con "duplica e modifica".

**Architettura:** tutte le decisioni sono in helper puri `static` (`PackageImportPlan`,
`PackageRowMerge`, `SpellMaterialization`, `CampaignExport`, `SpellClassNames`), testati con xUnit —
`PackageRowMerge` è quello che custodisce l'invariante più delicato della fase, cioè che un
aggiornamento non tocchi identità, proprietà e colonne fuori formato; l'unione delle due
sorgenti resta dentro `CatalogService`, che in questa fase copre tutti e cinque i cataloghi; i repository
guadagnano i soli metodi che servono a scrivere e rimuovere per provenienza. Le pagine orchestrano e
mostrano, non decidono.

**Stack:** Blazor WebAssembly / .NET 10 · `System.Text.Json` con source generator · xUnit ·
Supabase (PostgREST) via `postgrest-csharp 3.5.1`.

**Spec di riferimento:** [`../specs/2026-07-25-modello-2024-import-dati-design.md`](../specs/2026-07-25-modello-2024-import-dati-design.md).
I riferimenti `§N` puntano a quel documento. La Fase 1 è chiusa: il suo piano è
[`2026-07-25-modello-2024-import-dati-fase-1.md`](./2026-07-25-modello-2024-import-dati-fase-1.md).

## Cosa esiste già (Fase 1, da non riscrivere)

- `Services/CatalogPackageParser.cs` — `Parse(string?)` → `ParseResult(Package, Errors, Warnings)`;
  costanti `SupportedSchemaVersion = 1` e `AppPackageId = "srd-2024-it"`.
- `Services/CatalogKey.cs` — `NormalizeName(string?)`, `For(sourceId, name)`, `IsFromAppPackage(sourceId)`.
- `Services/CatalogMerge.cs` — `HiddenPackageIds(...)`, `Representative<T>(rows, sourceIdOf, idOf)`.
- `Services/CatalogService.cs` — `ICatalogService` con `GetPackageAsync()`, `Feats`, `LastParse`,
  `GetBackgroundsAsync(campaignId)`; record `CatalogView<TRow, TPkg>(DbRows, PackageEntries)`.
- `Models/Packages/CatalogPackage.cs` — POCO del formato di scambio.
- `Models/Background.cs` + `IBackgroundRepository`; `SourceId` su `Race`/`CharacterClass`/`Spell`/`Monster`
  e su `Background`; `SpeedUnit` su `Race`; `BackgroundAbilityChoice` su `Character`.
- `Pages/Backgrounds.razor` — **è il modello da replicare** per marcatura e "duplica e modifica".
- Migrazione `supabase/migrations/20260726000000_catalog_packages.sql` con i vincoli
  `UNIQUE (campaign_id, source_id)` su tutti e cinque i cataloghi — è ciò che rende una provenienza
  unica per campagna, e quindi affidabile la rilettura sul conflitto della materializzazione (§4.4) —
  e il `CHECK` che ammette solo `'m'`/`'ft'` in `races.speed_unit`.

## Vincoli globali

- **Tutto in italiano**: stringhe UI, commenti, messaggi di errore.
- **Zero migrazioni**: questa fase **non tocca lo schema né le policy**. Se un task sembra richiederlo,
  fermati e segnalalo invece di scrivere SQL.
- **Build pulita**: `dotnet build` a **0 warning / 0 errori**. Il progetto pubblica con `TrimMode=full`:
  `System.Text.Json` **solo** col source generator (`CatalogPackageJsonContext`), mai gli overload a
  reflection.
- **Niente `String.Normalize` né API di globalizzazione** (`InvariantGlobalization=true`: falliscono in
  silenzio). Usa `CatalogKey.NormalizeName`.
- **Logica di dominio in helper puri `static`** in `Services/`, testati con xUnit. Mai nei `.razor`.
  Per lo più `public static`; `internal static` + `InternalsVisibleTo` solo se l'helper è privato di un
  repository.
- **Dati dietro repository** per aggregato; `CatalogService` legge il database **solo** attraverso di essi,
  mai con `From<T>` diretto.
- **Pattern UI**: toast `.app-toast` (mai `.toast`), `ConfirmDialog` (mai `confirm()`),
  `<LoadingSpinner>`, `DbErrorBanner` per i **soli** errori di sistema, colori dai token in `:root`
  (mai literal esadecimali nei `.razor.css`).
- **Accessibilità**: ogni comando nuovo ha `aria-label` ed è attivabile da tastiera.
- **Prefisso del pacchetto dell'app**: `srd-2024-it/`. `CatalogKey.IsFromAppPackage` è l'unico modo
  ammesso di riconoscerlo.

## Decisioni prese in questo piano

Lo spec §7 elenca gli esiti del piano di import come «creato, aggiornato, saltato o non importabile».
Questo piano scompone «saltato» in due casi, perché hanno cause e rimedi diversi:

| Esito | Quando | Perché |
|---|---|---|
| `Create` | nessuna riga della campagna corrisponde | caso normale |
| `Update` | esiste una riga con lo **stesso `source_id`** e chi importa può modificarla | è la stessa voce, versione nuova |
| `SkippedNoPermission` | esiste con lo stesso `source_id` ma `AccessControl.CanEdit` dice no | il server rifiuterebbe la riga (§7) |
| `SkippedLocalWins` | la corrispondenza è **solo per nome** (riga senza `source_id`) | §6: «a parità di chiave vince il database». La riga è dell'utente — magari nata da "duplica e modifica" — e l'import **non la tocca** |
| `NotImportable` | sezione `feats` | non ha tabella (§5) |

`SkippedLocalWins` non è una comodità: senza di esso quella voce sarebbe marcata `Create`, e
l'inserimento in blocco creerebbe un doppione accanto alla riga che l'utente aveva personalizzato —
il vincolo `UNIQUE (campaign_id, source_id)` non lo impedisce, perché un `source_id` nullo non
collide con nulla. È l'opposto di ciò che §4.3 stabilisce.

**Niente `Upsert`, contro quanto suggerisce lo spec §4.4.** La libreria in uso lo rende inutilizzabile
su questi Model. Verificato intercettando le richieste reali di `postgrest-csharp 3.5.1`:

```
Insert →  POST /spells
          {"name":"Palla di Fuoco","source_id":"srd-2024-it/palla","campaign_id":"c1"}

Upsert →  POST /spells?on_conflict=campaign_id%2csource_id
          {"id":"","name":"Palla di Fuoco","source_id":"srd-2024-it/palla","campaign_id":"c1"}
```

`Insert` rispetta `[PrimaryKey("id", false)]` ed esclude la chiave; **`Upsert` la serializza sempre**,
e con `id uuid NOT NULL` un `""` è `invalid input syntax for type uuid` — HTTP 400 su *ogni* scrittura.
(L'unico `Upsert` già in produzione, `CombatStateRepository`, non lo incontra perché il suo Model usa
`[PrimaryKey("campaign_id", true)]`, sempre valorizzata.) Valorizzare l'`id` a mano non è un rimedio
accettabile: su conflitto il `DO UPDATE` riscriverebbe la **chiave primaria** della riga esistente, e
`character_spells_spell_id_fkey` non è `ON UPDATE CASCADE`.

Quindi le scritture sono due percorsi distinti, non uno:

- **creazioni** → `Insert(ICollection<T>)`, una richiesta per sezione, chiave primaria esclusa;
- **aggiornamenti** → `Update` riga per riga, sulla riga **già letta** dal database.

**Gli aggiornamenti fondono, non sovrascrivono.** Un `Update` invia tutte le colonne del Model, e il
formato di scambio non le copre tutte: un file che non porta `languages`, i sei bonus di specie, le
competenze in armi, le sei caratteristiche di un mostro le azzererebbe tutte al primo reimport, senza
che il conteggio delle righe cambi di una unità. L'esecuzione parte quindi dalla **riga esistente** e
vi applica sopra i soli campi che il pacchetto trasporta, lasciando intatto tutto il resto —
`added_by` compreso, che altrimenti un reimport del master trasferirebbe a sé stesso, togliendo in
silenzio al giocatore la modifica delle voci che aveva caricato.

**Materializzazione (§4.4):** leggi-poi-inserisci, con rilettura sul conflitto. Se l'`INSERT` urta il
vincolo `UNIQUE (campaign_id, source_id)` — è il caso di §4.4, «due giocatori preparano le schede la
stessa sera» — la riga vincente si rilegge invece di essere sovrascritta. Nessuna scrittura tocca mai
dati di un altro giocatore.

**Nessun `LIKE` costruito con testo dell'utente.** La rimozione per provenienza riceve un prefisso
digitato: in SQL `LIKE`, `_` vale "un carattere qualsiasi" e `%` "qualunque sequenza". Con
`source_id LIKE 'srd-2024-i_/%'` la guardia «il pacchetto dell'app non è rimovibile» — un confronto di
prefisso esatto — direbbe di sì mentre la `DELETE` colpisce proprio le voci del manuale. Il filtro per
provenienza si fa quindi **in memoria** su righe già lette, e la cancellazione per **elenco di id**.

**Export:** l'`id` del pacchetto esportato è `campagna-<nome normalizzato>`, **mai** `srd-2024-it`:
dare al proprio file l'id del pacchetto dell'app rende le proprie voci di sola lettura al reimport
(§6, «è un autogol»). Le righe che hanno già un `source_id` lo **conservano**: sono davvero quella
voce, e conservarlo è ciò che permette a un reimport di aggiornarle invece di duplicarle.

Con **due eccezioni**, entrambe necessarie perché il file prodotto sia rileggibile dal parser di
Fase 1, che rifiuta l'intero pacchetto — non la singola voce — se trova un identificatore ripetuto:

- le provenienze `srd-2024-it/…` (righe materializzate, §4.4) **degradano** a `<id campagna>/<slug>`.
  Conservarle propagherebbe la sola-lettura del manuale dentro campagne terze, dove quelle voci non
  sarebbero né modificabili (§6) né rimovibili in blocco (§8), senza che nessuno le abbia mai
  importate da un pacchetto ufficiale;
- gli slug che collidono ricevono un **suffisso progressivo**. Nessuna tabella ha un `UNIQUE` sul
  nome — anzi, `SkippedLocalWins` e `Representative` esistono proprio perché gli omonimi capitano:
  «Palla di Fuoco» e «palla di fuoco» normalizzano allo stesso slug, e senza suffisso il file
  esportato sarebbe illeggibile per intero.

## Come verificare

- Test: `dotnet test Tests/DndCompanion.Tests.csproj`
- Build: `dotnet build`
- I test d'integrazione RLS (`Tests.Integration/`) richiedono lo stack Supabase locale e vanno in
  auto-skip se assente. Questa fase non cambia le policy: non ne aggiunge.

---

### Task 1: Nomi di classe italiano↔inglese e filtro degli incantesimi

**File:**
- Crea: `Services/SpellClassNames.cs`
- Modifica: `Pages/Spells.razor` (array `classes` ~245-255, `FilteredSpells` ~312-316, `ToggleClass` ~350)
- Test: `Tests/SpellClassNamesTests.cs`

**Interfacce:**
- Consuma: `CatalogKey.NormalizeName` (Fase 1).
- Produce: `SpellClassNames.Pairs` (`IReadOnlyList<(string Italian, string English)>`) e
  `SpellClassNames.Matches(string? classesField, string italianName)`.

> Oggi `Pages/Spells.razor` filtra per sottostringhe **inglesi** (`Wizard`, `Cleric`). Con un catalogo
> italiano quei filtri non troverebbero mai nulla, senza errore visibile (§4.6). Va per primo perché è
> indipendente da tutto il resto e chiude un difetto già presente.

- [ ] **Step 1: Scrivi i test (falliranno)**

`Tests/SpellClassNamesTests.cs`:

```csharp
using DndCompanion.Services;

namespace DndCompanion.Tests;

public class SpellClassNamesTests
{
    [Theory]
    [InlineData("Wizard, Sorcerer", "Mago", true)]
    [InlineData("Mago, Stregone", "Mago", true)]
    [InlineData("Wizard", "Stregone", false)]
    [InlineData("Chierico", "Chierico", true)]
    [InlineData("Cleric", "Chierico", true)]
    [InlineData("", "Mago", false)]
    [InlineData(null, "Mago", false)]
    public void Matches_RiconosceEntrambeLeLingue(string? campo, string classeItaliana, bool atteso)
        => Assert.Equal(atteso, SpellClassNames.Matches(campo, classeItaliana));

    // Il campo è testo libero digitato a mano: spazi, maiuscole e separatori variano.
    [Theory]
    [InlineData("  wizard ,  bard  ")]
    [InlineData("Wizard;Bard")]
    [InlineData("Wizard/Bard")]
    public void Matches_TolleraSpaziMaiuscoleESeparatoriDiversi(string campo)
        => Assert.True(SpellClassNames.Matches(campo, "Mago"));

    // Il confronto è per TOKEN, non per sottostringa: "Bardo" non deve farsi trovare
    // da chi cerca una classe il cui nome ne è un prefisso, e viceversa.
    [Fact]
    public void Matches_ConfrontoPerTokenNonPerSottostringa()
    {
        Assert.False(SpellClassNames.Matches("Bardolino", "Bardo"));
        Assert.True(SpellClassNames.Matches("Bardo", "Bardo"));
    }

    [Fact]
    public void Matches_ClasseSconosciuta_RestituisceFalso()
        => Assert.False(SpellClassNames.Matches("Mago", "Artefice"));

    [Fact]
    public void Pairs_ContieneLeOttoClassiIncantatrici()
    {
        Assert.Equal(8, SpellClassNames.Pairs.Count);
        Assert.Contains(SpellClassNames.Pairs, p => p.Italian == "Mago" && p.English == "Wizard");
    }
}
```

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter SpellClassNamesTests`
Atteso: FALLIMENTO di compilazione — `SpellClassNames` non esiste.

- [ ] **Step 3: Implementa l'helper**

`Services/SpellClassNames.cs`:

```csharp
namespace DndCompanion.Services;

/// <summary>Le otto classi incantatrici nei due nomi che il catalogo può contenere: quello inglese
/// delle voci digitate finora e quello italiano delle voci di pacchetto (§4.6 dello spec).
/// Logica pura.</summary>
public static class SpellClassNames
{
    /// <summary>Coppie (italiano, inglese) nell'ordine in cui la pagina mostra i filtri.
    /// DEVE restare dichiarata PRIMA di Aliases: l'inizializzazione dei campi statici segue
    /// l'ordine testuale, e invertirli lascerebbe Aliases vuoto senza alcun errore.</summary>
    public static readonly IReadOnlyList<(string Italian, string English)> Pairs = new[]
    {
        ("Bardo",    "Bard"),
        ("Chierico", "Cleric"),
        ("Druido",   "Druid"),
        ("Paladino", "Paladin"),
        ("Ranger",   "Ranger"),
        ("Stregone", "Sorcerer"),
        ("Warlock",  "Warlock"),
        ("Mago",     "Wizard"),
    };

    // Chiave: nome italiano normalizzato. Valore: i due alias normalizzati da cercare nel campo.
    private static readonly Dictionary<string, string[]> Aliases = Pairs.ToDictionary(
        p => CatalogKey.NormalizeName(p.Italian),
        p => new[] { CatalogKey.NormalizeName(p.Italian), CatalogKey.NormalizeName(p.English) },
        StringComparer.Ordinal);

    /// <summary>Vero se il campo "classi" di un incantesimo — testo libero, in italiano o in
    /// inglese — contiene la classe indicata.
    ///
    /// Il confronto è per TOKEN e non per sottostringa: `Contains` su un campo libero farebbe
    /// combaciare qualunque parola che contenga il nome, e il filtro mostrerebbe incantesimi
    /// che non appartengono alla classe.</summary>
    public static bool Matches(string? classesField, string italianName)
    {
        if (string.IsNullOrWhiteSpace(classesField)) return false;
        if (!Aliases.TryGetValue(CatalogKey.NormalizeName(italianName), out var aliases)) return false;

        foreach (var token in classesField.Split(',', ';', '/'))
        {
            var chiave = CatalogKey.NormalizeName(token);
            if (Array.IndexOf(aliases, chiave) >= 0) return true;
        }
        return false;
    }
}
```

- [ ] **Step 4: Esegui i test e verifica che passino**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter SpellClassNamesTests`
Atteso: **13 casi** PASSATI (7 `InlineData` + 3 `InlineData` + 3 `Fact`: ogni `InlineData` conta
come un caso a sé).

- [ ] **Step 5: Collega il filtro della pagina**

In `Pages/Spells.razor`, nel blocco `@code`, **elimina** il record `SpellClass` e l'array `classes`
(righe ~243-255) e sostituiscili con:

```csharp
    // I nomi delle classi vivono in SpellClassNames: la pagina non deve conoscere la mappatura
    // italiano/inglese, che serve anche altrove ed è testata a parte (§4.6).
    private IReadOnlyList<(string Italian, string English)> classes => SpellClassNames.Pairs;
    private string? classFilter;
```

Nel markup (righe ~112-119) il `@foreach` diventa:

```razor
                @foreach (var cls in classes)
                {
                    <button type="button"
                            class="chip @(classFilter == cls.Italian ? "chip-active" : "")"
                            @onclick="() => ToggleClass(cls.Italian)">
                        @cls.Italian
                    </button>
                }
```

In `FilteredSpells` (righe ~312-316) sostituisci il confronto per sottostringa:

```csharp
            if (!string.IsNullOrEmpty(classFilter))
            {
                // Il campo può essere in inglese (voci digitate finora) o in italiano (pacchetto):
                // SpellClassNames riconosce entrambi.
                result = result.Where(s => SpellClassNames.Matches(s.Classes, classFilter));
            }
```

`ToggleClass(string cls)` resta invariato: cambia solo il valore che riceve, ora il nome italiano.

- [ ] **Step 6: Verifica build e suite**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori.

Comando: `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: tutti verdi.

- [ ] **Step 7: Commit**

```bash
git add Services/SpellClassNames.cs Tests/SpellClassNamesTests.cs Pages/Spells.razor
git commit -m "feat(incantesimi): filtro per classe che riconosce nomi italiani e inglesi"
```

---

### Task 2: Il piano di import e la conversione delle righe

**File:**
- Crea: `Services/PackageImportPlan.cs`
- Crea: `Services/PackageRowMerge.cs`
- Modifica: `Models/Packages/CatalogPackage.cs` (`Level` e `ArmorClass` nullable)
- Test: `Tests/PackageImportPlanTests.cs`, `Tests/PackageRowMergeTests.cs`

**Interfacce:**
- Consuma: `CatalogKey.For`/`NormalizeName`, `CatalogMerge.Representative`, `AccessControl.CanEdit`,
  i POCO di `Models/Packages/CatalogPackage.cs`, i Model `Race`/`CharacterClass`/`Spell`/`Monster`/`Background`.
- Produce: `ImportOutcome` (enum), `ImportItem`, `ImportSection`, `ImportPlanResult`,
  `CampaignCatalogs`, `PackageImportPlan.ForSection<TPkg, TRow>(...)`,
  `PackageImportPlan.ForFeats(...)`, `PackageImportPlan.Build(package, existing, isMaster, userId)`;
  e in `PackageRowMerge` i cinque `NuovaX(voce, campaignId, userId)` più i cinque `ApplicaX(voce, riga)`.

> È il cuore della fase: «l'anteprima che l'utente conferma è l'output di `PackageImportPlan`, non una
> stima scritta a parte» (§7). Nessuna rete, nessun database: solo dati in ingresso e un verdetto.
>
> `PackageRowMerge` sta qui e non nel blocco `@code` della schermata perché non è un mapping
> qualunque: codifica l'invariante che rende sicuro un reimport — `Id`, `CampaignId`, `CreatedAt`,
> `AddedBy` e ogni colonna che il formato non trasporta devono **sopravvivere** all'aggiornamento. È
> una regressione che nessuna verifica manuale intercetta (il conteggio delle righe non cambia), e la
> sola rete possibile è xUnit. Il precedente di progetto è `Services/CombatImport.cs`.

- [ ] **Step 1: Scrivi i test (falliranno)**

`Tests/PackageImportPlanTests.cs`:

```csharp
using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;

namespace DndCompanion.Tests;

public class PackageImportPlanTests
{
    private const string Utente = "utente-1";
    private const string Altro = "utente-2";

    private static ImportSection Sezione(
        IEnumerable<PackageBackground> pacchetto,
        IEnumerable<Background> db,
        bool isMaster = false,
        string? userId = Utente)
        => PackageImportPlan.ForSection(
            "Background", pacchetto, p => p.Id, p => p.Name,
            db, r => r.SourceId, r => r.Name, r => r.Id, r => r.AddedBy,
            isMaster, userId);

    private static PackageBackground Voce(string id, string nome) => new() { Id = id, Name = nome };

    private static Background Riga(string id, string? sourceId, string nome, string? addedBy = Utente)
        => new() { Id = id, SourceId = sourceId, Name = nome, AddedBy = addedBy, CampaignId = "c1" };

    [Fact]
    public void ForSection_NessunaCorrispondenza_Crea()
    {
        var sezione = Sezione(
            new[] { Voce("srd-2024-it/soldato", "Soldato") },
            Array.Empty<Background>());

        var voce = Assert.Single(sezione.Items);
        Assert.Equal(ImportOutcome.Create, voce.Outcome);
        Assert.Null(voce.ExistingRowId);
    }

    [Fact]
    public void ForSection_StessaProvenienzaEPermesso_Aggiorna()
    {
        var sezione = Sezione(
            new[] { Voce("srd-2024-it/soldato", "Soldato") },
            new[] { Riga("uuid-1", "srd-2024-it/soldato", "Soldato") });

        var voce = Assert.Single(sezione.Items);
        Assert.Equal(ImportOutcome.Update, voce.Outcome);
        Assert.Equal("uuid-1", voce.ExistingRowId);
    }

    // Il caso di §7: senza questo, l'utente vedrebbe un piano che il server rifiuta riga per riga.
    [Fact]
    public void ForSection_StessaProvenienzaMaRigaAltrui_SaltaPerPermessi()
    {
        var sezione = Sezione(
            new[] { Voce("srd-2024-it/soldato", "Soldato") },
            new[] { Riga("uuid-1", "srd-2024-it/soldato", "Soldato", addedBy: Altro) });

        Assert.Equal(ImportOutcome.SkippedNoPermission, Assert.Single(sezione.Items).Outcome);
    }

    [Fact]
    public void ForSection_RigaAltruiMaChiImportaEMaster_Aggiorna()
    {
        var sezione = Sezione(
            new[] { Voce("srd-2024-it/soldato", "Soldato") },
            new[] { Riga("uuid-1", "srd-2024-it/soldato", "Soldato", addedBy: Altro) },
            isMaster: true);

        Assert.Equal(ImportOutcome.Update, Assert.Single(sezione.Items).Outcome);
    }

    // §6: a parità di chiave vince il database. La riga dell'utente non si tocca, e soprattutto
    // NON si crea un doppione: un source_id nullo non collide con il vincolo di unicità.
    [Fact]
    public void ForSection_CorrispondenzaSoloPerNome_LasciaVincereLaRigaLocale()
    {
        var sezione = Sezione(
            new[] { Voce("srd-2024-it/soldato", "Soldato") },
            new[] { Riga("uuid-1", null, "Soldato") });

        var voce = Assert.Single(sezione.Items);
        Assert.Equal(ImportOutcome.SkippedLocalWins, voce.Outcome);
        Assert.Equal("uuid-1", voce.ExistingRowId);
    }

    [Fact]
    public void ForSection_ConfrontoPerNomeIgnoraAccentiEMaiuscole()
    {
        var sezione = Sezione(
            new[] { Voce("srd-2024-it/eremita", "Eremità") },
            new[] { Riga("uuid-1", null, "  EREMITA  ") });

        Assert.Equal(ImportOutcome.SkippedLocalWins, Assert.Single(sezione.Items).Outcome);
    }

    // Più righe con la stessa chiave: decide il rappresentante di CatalogMerge — prima quella
    // senza provenienza, poi l'id ordinalmente minore.
    [Fact]
    public void ForSection_PiuRigheOmonime_UsaIlRappresentante()
    {
        var sezione = Sezione(
            new[] { Voce("srd-2024-it/soldato", "Soldato") },
            new[]
            {
                Riga("uuid-b", "srd-2024-it/soldato", "Soldato"),
                Riga("uuid-a", "srd-2024-it/soldato", "Soldato"),
            });

        Assert.Equal("uuid-a", Assert.Single(sezione.Items).ExistingRowId);
    }

    [Fact]
    public void ForSection_ContaGliEsitiPerTipo()
    {
        var sezione = Sezione(
            new[]
            {
                Voce("srd-2024-it/soldato", "Soldato"),
                Voce("srd-2024-it/eremita", "Eremita"),
                Voce("srd-2024-it/artigiano", "Artigiano"),
            },
            new[]
            {
                Riga("uuid-1", "srd-2024-it/soldato", "Soldato"),
                Riga("uuid-2", "srd-2024-it/eremita", "Eremita", addedBy: Altro),
            });

        Assert.Equal(1, sezione.CreateCount);
        Assert.Equal(1, sezione.UpdateCount);
        Assert.Equal(1, sezione.SkippedCount);
    }

    // §5 e §9: la dicitura è diversa perché la conseguenza è diversa. Dal pacchetto dell'app i
    // talenti si leggono nella pagina Background; da un file dell'utente non finiscono da nessuna parte.
    [Fact]
    public void ForFeats_DalPacchettoDellApp_DiceCheSonoConsultabili()
    {
        var sezione = PackageImportPlan.ForFeats(
            new[] { new PackageFeat { Id = "srd-2024-it/artefice", Name = "Artefice" } },
            fromAppPackage: true);

        Assert.Equal(ImportOutcome.NotImportable, Assert.Single(sezione.Items).Outcome);
        // Il `!` serve: Note è string? e il progetto di test ha Nullable=enable.
        Assert.Contains("consultazione", sezione.Note!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForFeats_DaUnFileDellUtente_DiceCheRestanoNelFile()
    {
        var sezione = PackageImportPlan.ForFeats(
            new[] { new PackageFeat { Id = "mio/talento", Name = "Talento mio" } },
            fromAppPackage: false);

        Assert.Contains("resta nel tuo file", sezione.Note!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ProduceUnaSezionePerTipoPiuITalenti()
    {
        var pacchetto = new CatalogPackage
        {
            SchemaVersion = 1,
            Id = "mio-pacchetto",
            Species = { new PackageSpecies { Id = "mio-pacchetto/elfo", Name = "Elfo" } },
            Backgrounds = { new PackageBackground { Id = "mio-pacchetto/soldato", Name = "Soldato" } },
            Feats = { new PackageFeat { Id = "mio-pacchetto/schivare", Name = "Schivare" } },
        };

        var piano = PackageImportPlan.Build(pacchetto, new CampaignCatalogs(), isMaster: false, userId: Utente);

        // Cinque cataloghi + talenti, anche quando una sezione è vuota: l'utente deve poter
        // constatare che non c'era nulla, non dedurlo da un'assenza.
        Assert.Equal(6, piano.Sections.Count);
        Assert.Equal(2, piano.TotalWrites);
        Assert.False(piano.IsEmpty);
    }

    [Fact]
    public void Build_PacchettoSenzaVociScrivibili_SegnalaCheNonCEDaScrivere()
    {
        var pacchetto = new CatalogPackage
        {
            SchemaVersion = 1,
            Id = "mio-pacchetto",
            Feats = { new PackageFeat { Id = "mio-pacchetto/schivare", Name = "Schivare" } },
        };

        var piano = PackageImportPlan.Build(pacchetto, new CampaignCatalogs(), isMaster: false, userId: Utente);

        Assert.Equal(0, piano.TotalWrites);
        Assert.True(piano.IsEmpty);
    }
}
```

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter PackageImportPlanTests`
Atteso: FALLIMENTO di compilazione — `PackageImportPlan` non esiste.

- [ ] **Step 3: Implementa l'helper**

`Services/PackageImportPlan.cs`:

```csharp
using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Che cosa accadrà a una voce del pacchetto quando l'import verrà confermato.</summary>
public enum ImportOutcome
{
    /// <summary>Nessuna riga della campagna le corrisponde: sarà creata.</summary>
    Create,

    /// <summary>Esiste una riga con la stessa provenienza e chi importa può modificarla.</summary>
    Update,

    /// <summary>Esiste con la stessa provenienza, ma AccessControl.CanEdit dice no: il server la
    /// rifiuterebbe, quindi non viene nemmeno inviata (§7).</summary>
    SkippedNoPermission,

    /// <summary>La corrispondenza è solo per nome: la riga è dell'utente e vince sul pacchetto
    /// (§6). Non si tocca — e soprattutto non si duplica: marcarla Create farebbe inserire una
    /// riga gemella, perché un source_id nullo non collide con il vincolo di unicità.</summary>
    SkippedLocalWins,

    /// <summary>Sezione senza tabella: i talenti (§5).</summary>
    NotImportable,
}

/// <summary>Una voce del pacchetto e il suo destino.</summary>
public sealed record ImportItem(string SourceId, string Name, ImportOutcome Outcome, string? ExistingRowId);

/// <summary>Una sezione dell'anteprima: un tipo di contenuto e le sue voci.</summary>
public sealed record ImportSection(string Title, IReadOnlyList<ImportItem> Items, string? Note = null)
{
    public int CreateCount => Items.Count(i => i.Outcome == ImportOutcome.Create);
    public int UpdateCount => Items.Count(i => i.Outcome == ImportOutcome.Update);

    /// <summary>Voci che non verranno scritte per una ragione o per l'altra, talenti esclusi:
    /// quelli hanno una sezione e una spiegazione tutta loro.</summary>
    public int SkippedCount => Items.Count(i =>
        i.Outcome is ImportOutcome.SkippedNoPermission or ImportOutcome.SkippedLocalWins);

    /// <summary>Le voci che l'esecuzione invierà davvero al server.</summary>
    public IEnumerable<ImportItem> Writable => Items.Where(i =>
        i.Outcome is ImportOutcome.Create or ImportOutcome.Update);
}

/// <summary>L'anteprima completa che l'utente conferma.</summary>
public sealed record ImportPlanResult(IReadOnlyList<ImportSection> Sections)
{
    public int TotalWrites => Sections.Sum(s => s.CreateCount + s.UpdateCount);
    public int TotalSkipped => Sections.Sum(s => s.SkippedCount);

    /// <summary>Nulla da scrivere: la schermata deve dirlo invece di offrire una conferma che
    /// non farebbe niente.</summary>
    public bool IsEmpty => TotalWrites == 0;
}

/// <summary>I cinque cataloghi di una campagna, come li legge il database. Raccolti in un tipo
/// solo perché sia il piano di import sia l'export ne hanno bisogno tutti insieme.</summary>
public sealed class CampaignCatalogs
{
    public List<Race> Races { get; init; } = new();
    public List<CharacterClass> Classes { get; init; } = new();
    public List<Spell> Spells { get; init; } = new();
    public List<Monster> Monsters { get; init; } = new();
    public List<Background> Backgrounds { get; init; } = new();
}

/// <summary>Calcola, senza scrivere nulla, che cosa un import produrrebbe (§7 dello spec).
/// Logica pura: nessuna rete, nessun database.</summary>
public static class PackageImportPlan
{
    /// <summary>Il destino di ogni voce di una sezione, confrontata con le righe già in campagna.
    ///
    /// I delegati invece dei Model concreti: i cinque cataloghi non condividono un'interfaccia
    /// comune (sono Model Postgrest indipendenti) e introdurne una per questo solo scopo
    /// significherebbe toccarli tutti.</summary>
    public static ImportSection ForSection<TPkg, TRow>(
        string title,
        IEnumerable<TPkg> packageEntries,
        Func<TPkg, string> packageIdOf,
        Func<TPkg, string> packageNameOf,
        IEnumerable<TRow> dbRows,
        Func<TRow, string?> sourceIdOf,
        Func<TRow, string> nameOf,
        Func<TRow, string> rowIdOf,
        Func<TRow, string?> addedByOf,
        bool isMaster,
        string? userId) where TRow : class
    {
        var righe = dbRows.ToList();

        // Due indici distinti perché le due corrispondenze hanno esiti DIVERSI: per provenienza
        // si aggiorna, per solo nome si lascia stare (§6). Fonderli farebbe sparire la differenza.
        var perProvenienza = righe
            .Where(r => !string.IsNullOrWhiteSpace(sourceIdOf(r)))
            .GroupBy(r => sourceIdOf(r)!.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var perNome = righe
            .GroupBy(r => CatalogKey.NormalizeName(nameOf(r)), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var items = new List<ImportItem>();
        foreach (var entry in packageEntries)
        {
            var id = packageIdOf(entry);
            var nome = packageNameOf(entry);

            if (perProvenienza.TryGetValue(id, out var omologhe))
            {
                var rappresentante = CatalogMerge.Representative(omologhe, sourceIdOf, rowIdOf)!;
                var esito = AccessControl.CanEdit(isMaster, addedByOf(rappresentante), userId)
                    ? ImportOutcome.Update
                    : ImportOutcome.SkippedNoPermission;
                items.Add(new ImportItem(id, nome, esito, rowIdOf(rappresentante)));
                continue;
            }

            if (perNome.TryGetValue(CatalogKey.NormalizeName(nome), out var omonime))
            {
                var rappresentante = CatalogMerge.Representative(omonime, sourceIdOf, rowIdOf)!;
                items.Add(new ImportItem(id, nome, ImportOutcome.SkippedLocalWins, rowIdOf(rappresentante)));
                continue;
            }

            items.Add(new ImportItem(id, nome, ImportOutcome.Create, null));
        }

        return new ImportSection(title, items);
    }

    /// <summary>I talenti: mai importati, ma mai scartati in silenzio (§9). La dicitura cambia
    /// con la provenienza, perché cambia la conseguenza: dal pacchetto dell'app si leggono nella
    /// pagina Background, da un file dell'utente non finiscono da nessuna parte.</summary>
    public static ImportSection ForFeats(IEnumerable<PackageFeat> feats, bool fromAppPackage)
    {
        var items = feats
            .Select(f => new ImportItem(f.Id, f.Name, ImportOutcome.NotImportable, null))
            .ToList();

        var nota = fromAppPackage
            ? "Solo consultazione: i talenti si leggono nella pagina Background, accanto al talento d'origine che li richiama."
            : "Non importabile — resta nel tuo file: l'app non ha un catalogo dei talenti dove salvarli.";

        return new ImportSection("Talenti", items, nota);
    }

    /// <summary>L'anteprima completa. Le sezioni ci sono tutte anche quando sono vuote: chi
    /// importa deve poter constatare che il file non conteneva mostri, non dedurlo da un'assenza.</summary>
    public static ImportPlanResult Build(
        CatalogPackage package,
        CampaignCatalogs existing,
        bool isMaster,
        string? userId)
    {
        var sezioni = new List<ImportSection>
        {
            ForSection("Specie", package.Species, p => p.Id, p => p.Name,
                existing.Races, r => r.SourceId, r => r.Name, r => r.Id, r => r.AddedBy,
                isMaster, userId),

            ForSection("Classi", package.Classes, p => p.Id, p => p.Name,
                existing.Classes, r => r.SourceId, r => r.Name, r => r.Id, r => r.AddedBy,
                isMaster, userId),

            ForSection("Background", package.Backgrounds, p => p.Id, p => p.Name,
                existing.Backgrounds, r => r.SourceId, r => r.Name, r => r.Id, r => r.AddedBy,
                isMaster, userId),

            ForSection("Incantesimi", package.Spells, p => p.Id, p => p.Name,
                existing.Spells, r => r.SourceId, r => r.Name, r => r.Id, r => r.AddedBy,
                isMaster, userId),

            ForSection("Mostri", package.Monsters, p => p.Id, p => p.Name,
                existing.Monsters, r => r.SourceId, r => r.Name, r => r.Id, r => r.AddedBy,
                isMaster, userId),

            ForFeats(package.Feats, CatalogKey.IsFromAppPackage(package.Id + "/")),
        };

        return new ImportPlanResult(sezioni);
    }
}
```

> Nota sull'ultima riga: `IsFromAppPackage` confronta il prefisso `"<AppPackageId>/"`, quindi
> riceve `package.Id + "/"` — passargli `package.Id` nudo darebbe sempre falso. È l'unico modo
> ammesso di riconoscere il pacchetto dell'app (vincoli globali), e va usato anche qui.

- [ ] **Step 4: Esegui i test e verifica che passino**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter PackageImportPlanTests`
Atteso: 12 test PASSATI.

- [ ] **Step 5: Rendi nullable i due campi numerici che il formato può omettere**

Il parser di Fase 1 valida **solo** id e nome: una voce minimale come
`{"id":"x/palla-di-fuoco","name":"Palla di Fuoco"}` è legittima. Con `int`, un campo assente arriva
come `0` — e `0` è un valore **valido** sia per il livello (i trucchetti) sia per la classe armatura.
In aggiornamento questo trasformerebbe un incantesimo di livello 3 in un trucchetto e un mostro con
CA 15 in uno con CA 0, senza che nulla lo segnali. Con `int?` l'assenza torna distinguibile.

In `Models/Packages/CatalogPackage.cs`:

```csharp
    // in PackageSpell
    [JsonPropertyName("level")] public int? Level { get; set; }

    // in PackageMonster
    [JsonPropertyName("armorClass")] public int? ArmorClass { get; set; }
```

**Nel repo di oggi non c'è alcun lettore da aggiornare** — nemmeno nei test di Fase 1, che non
asseriscono quei due campi. I lettori li crea questo piano, nei Task 3, 7 e 8: sono **già scritti**
con il coalescing giusto (`?? 0` per il livello, `?? 10` per la classe armatura). Se durante
l'esecuzione ne compare uno senza, è un errore di trascrizione: `Spell.Level` e `Monster.ArmorClass`
sono `int` non nullable, e il compilatore lo segnala con CS0266 o CS1503.

- [ ] **Step 6: Scrivi i test della conversione (falliranno)**

`Tests/PackageRowMergeTests.cs`:

```csharp
using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;

namespace DndCompanion.Tests;

public class PackageRowMergeTests
{
    // ---- Creazione ----

    [Fact]
    public void NuovaSpecie_PortaProvenienzaCampagnaEAutore()
    {
        var voce = new PackageSpecies
        {
            Id = "p/elfo", Name = "Elfo", Traits = "Scurovisione",
            Speed = new PackageSpeed { Value = 9, Unit = "m" },
        };

        var riga = PackageRowMerge.NuovaSpecie(voce, "c1", "utente-1");

        Assert.Equal("p/elfo", riga.SourceId);
        Assert.Equal("c1", riga.CampaignId);
        Assert.Equal("utente-1", riga.AddedBy);
        Assert.Equal(9, riga.Speed);
        Assert.Equal("m", riga.SpeedUnit);
        // L'Id resta vuoto: lo genera il database, e Insert lo esclude dal payload.
        Assert.Equal(string.Empty, riga.Id);
    }

    // races_speed_unit_check ammette SOLO 'm' e 'ft': un file scritto a mano con "metri" o "M" farebbe
    // fallire con 400 l'intero blocco Specie, e l'anteprima aveva promesso il contrario.
    [Theory]
    [InlineData("ft", "ft")]
    [InlineData("FT", "ft")]
    [InlineData(" ft ", "ft")]
    // Le forme estese vanno riconosciute, non lasciate al fallback: "feet" letto come metri
    // trasformerebbe 30 piedi in 30 metri, un dato sbagliato e silenzioso.
    [InlineData("feet", "ft")]
    [InlineData("piedi", "ft")]
    [InlineData("m", "m")]
    [InlineData("metri", "m")]
    [InlineData("", "m")]
    [InlineData(null, "m")]
    public void NuovaSpecie_NormalizzaLUnitaDiVelocita(string? unita, string atteso)
    {
        var voce = new PackageSpecies
        {
            Id = "p/elfo", Name = "Elfo",
            Speed = unita is null ? null : new PackageSpeed { Value = 9, Unit = unita },
        };

        Assert.Equal(atteso, PackageRowMerge.NuovaSpecie(voce, "c1", "u1").SpeedUnit);
    }

    // ---- Aggiornamento: ciò che NON deve cambiare ----

    [Fact]
    public void ApplicaSpecie_NonToccaIdentitaProprietaNeLeColonneFuoriDalFormato()
    {
        var esistente = new Race
        {
            Id = "uuid-1", CampaignId = "c1", AddedBy = "altro-utente",
            CreatedAt = new DateTime(2026, 1, 1),
            Name = "Elfo", Languages = "Comune, Elfico",
            DexBonus = 2, ConBonus = 1, SourceId = "p/elfo",
        };

        PackageRowMerge.ApplicaSpecie(new PackageSpecies { Id = "p/elfo", Name = "Elfo Alto" }, esistente);

        Assert.Equal("Elfo Alto", esistente.Name);
        // Identità e proprietà: un reimport del master non deve appropriarsi delle righe altrui.
        Assert.Equal("uuid-1", esistente.Id);
        Assert.Equal("c1", esistente.CampaignId);
        Assert.Equal("altro-utente", esistente.AddedBy);
        Assert.Equal(new DateTime(2026, 1, 1), esistente.CreatedAt);
        // Colonne che il formato non trasporta: restano.
        Assert.Equal("Comune, Elfico", esistente.Languages);
        Assert.Equal(2, esistente.DexBonus);
        Assert.Equal(1, esistente.ConBonus);
    }

    [Fact]
    public void ApplicaClasse_NonAzzeraLeColonneFuoriDalFormato()
    {
        var esistente = new CharacterClass
        {
            Id = "uuid-1", Name = "Mago", CampaignId = "c1",
            Description = "Studioso dell'arcano",
            Features = "Recupero arcano",
            ArmorProficiencies = "Nessuna",
            WeaponProficiencies = "Bastone",
            SkillChoices = "2 fra: Arcano, Storia",
        };

        PackageRowMerge.ApplicaClasse(new PackageClass { Id = "p/mago", Name = "Mago", HitDie = "d6" }, esistente);

        Assert.Equal("d6", esistente.HitDie);
        Assert.Equal("Studioso dell'arcano", esistente.Description);
        Assert.Equal("Recupero arcano", esistente.Features);
        Assert.Equal("Nessuna", esistente.ArmorProficiencies);
        Assert.Equal("Bastone", esistente.WeaponProficiencies);
        // skillChoices assente nel file: la colonna non si svuota.
        Assert.Equal("2 fra: Arcano, Storia", esistente.SkillChoices);
    }

    [Fact]
    public void ApplicaMostro_NonAzzeraCaratteristicheNeCampiNonTrasportati()
    {
        var esistente = new Monster
        {
            Id = "uuid-1", Name = "Goblin", CampaignId = "c1",
            ArmorClass = 15, Strength = 8, Dexterity = 14,
            Size = "Piccola", Type = "Umanoide", Alignment = "Neutrale malvagio",
            Speed = "9 m", Abilities = "Fuga agile",
        };

        PackageRowMerge.ApplicaMostro(
            new PackageMonster { Id = "p/goblin", Name = "Goblin", ChallengeRating = "1/4" }, esistente);

        Assert.Equal("1/4", esistente.ChallengeRating);
        // armorClass assente: NON diventa 0 (con `int` sarebbe successo in silenzio).
        Assert.Equal(15, esistente.ArmorClass);
        Assert.Equal(8, esistente.Strength);
        Assert.Equal(14, esistente.Dexterity);
        Assert.Equal("Piccola", esistente.Size);
        Assert.Equal("Umanoide", esistente.Type);
        Assert.Equal("Neutrale malvagio", esistente.Alignment);
        Assert.Equal("9 m", esistente.Speed);
        Assert.Equal("Fuga agile", esistente.Abilities);
    }

    // Il caso più dannoso della categoria: un livello che diventa 0 rende l'incantesimo un
    // trucchetto, quindi auto-preparato e senza slot.
    [Fact]
    public void ApplicaIncantesimo_LivelloAssente_NonDiventaTrucchetto()
    {
        var esistente = new Spell { Id = "uuid-1", Name = "Palla di Fuoco", Level = 3, CampaignId = "c1" };

        PackageRowMerge.ApplicaIncantesimo(
            new PackageSpell { Id = "p/palla", Name = "Palla di Fuoco" }, esistente);

        Assert.Equal(3, esistente.Level);
    }

    [Fact]
    public void ApplicaIncantesimo_LivelloZeroEsplicito_LoApplica()
    {
        var esistente = new Spell { Id = "uuid-1", Name = "Luce", Level = 3, CampaignId = "c1" };

        PackageRowMerge.ApplicaIncantesimo(
            new PackageSpell { Id = "p/luce", Name = "Luce", Level = 0 }, esistente);

        Assert.Equal(0, esistente.Level);
    }

    [Fact]
    public void ApplicaIncantesimo_CampiPresenti_VengonoScritti()
    {
        var esistente = new Spell { Id = "uuid-1", Name = "Vecchio", School = "Abiurazione", CampaignId = "c1" };

        PackageRowMerge.ApplicaIncantesimo(new PackageSpell
        {
            Id = "p/palla", Name = "Palla di Fuoco", Level = 3,
            School = "Evocazione", Classes = { "Mago", "Stregone" },
        }, esistente);

        Assert.Equal("Palla di Fuoco", esistente.Name);
        Assert.Equal("Evocazione", esistente.School);
        Assert.Equal("Mago, Stregone", esistente.Classes);
    }

    [Fact]
    public void DescriviScelte_FormulaCondivisaConLePagineDiCatalogo()
    {
        var scelte = new PackageSkillChoices { Count = 2, From = { "Arcano", "Storia" } };

        Assert.Equal("2 fra: Arcano, Storia", PackageRowMerge.DescriviScelte(scelte));
        // null significa "il file non lo dichiara": è ciò che permette ad ApplicaClasse di non
        // svuotare una colonna già compilata.
        Assert.Null(PackageRowMerge.DescriviScelte(null));
    }

    [Fact]
    public void ApplicaBackground_ListeVuote_NonAzzeranoLeColonne()
    {
        var esistente = new Background
        {
            Id = "uuid-1", Name = "Soldato", CampaignId = "c1",
            AbilityScores = "Forza, Costituzione, Carisma",
            SkillProficiencies = "Atletica, Intimidire",
        };

        PackageRowMerge.ApplicaBackground(
            new PackageBackground { Id = "p/soldato", Name = "Soldato" }, esistente);

        Assert.Equal("Forza, Costituzione, Carisma", esistente.AbilityScores);
        Assert.Equal("Atletica, Intimidire", esistente.SkillProficiencies);
    }
}
```

- [ ] **Step 7: Esegui i test e verifica che falliscano**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter PackageRowMergeTests`
Atteso: FALLIMENTO di compilazione — `PackageRowMerge` non esiste.

- [ ] **Step 8: Implementa la conversione**

`Services/PackageRowMerge.cs`:

```csharp
using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Conversione fra le voci del formato di scambio e le righe di catalogo, nelle due
/// direzioni che l'import usa: creare una riga nuova, oppure applicare una voce **sopra** una riga
/// esistente. Logica pura.
///
/// La regola che questa classe custodisce, e che nessuna verifica manuale può controllare — il
/// conteggio delle righe non cambia — è: un aggiornamento **non deve mai** toccare l'identità
/// (<c>Id</c>, <c>CampaignId</c>, <c>CreatedAt</c>), la proprietà (<c>AddedBy</c>) né le colonne che
/// il formato non trasporta. E un campo **assente** nel file non è un campo **vuoto**: il parser
/// valida solo id e nome, quindi le voci minimali sono legittime e non devono svuotare nulla.</summary>
public static class PackageRowMerge
{
    /// <summary>L'unica unità che il database accetta: <c>races_speed_unit_check</c> ammette solo
    /// 'm' e 'ft'. Un file scritto a mano con "metri" o "M" farebbe fallire con 400 l'intera
    /// sezione, e la stessa cosa accadrebbe alla copia creata da "duplica e modifica" — per questo
    /// è <b>public</b>: la usano sia l'import sia le pagine di catalogo, e riscriverla a mano in un
    /// `.razor` la farebbe divergere.
    ///
    /// Le forme estese vanno riconosciute invece che finire nel fallback: "feet" letto come metri
    /// trasformerebbe una velocità di 30 piedi in 30 metri — un dato sbagliato e silenzioso, mentre
    /// prima il CHECK almeno lo respingeva con un errore visibile.</summary>
    public static string UnitaValida(string? unit)
    {
        var u = unit?.Trim() ?? string.Empty;
        return u.Equals("ft", StringComparison.OrdinalIgnoreCase)
            || u.Equals("feet", StringComparison.OrdinalIgnoreCase)
            || u.Equals("foot", StringComparison.OrdinalIgnoreCase)
            || u.Equals("piedi", StringComparison.OrdinalIgnoreCase)
                ? "ft"
                : "m";
    }

    private static string Unisci(List<string> values) => string.Join(", ", values);

    // ---- Creazione: righe nuove, Id lasciato al database ----

    public static Race NuovaSpecie(PackageSpecies p, string campaignId, string? userId) => new()
    {
        Name = p.Name,
        Description = p.Description,
        Speed = p.Speed?.Value ?? 9,
        SpeedUnit = UnitaValida(p.Speed?.Unit),
        Traits = p.Traits,
        SourceId = p.Id,
        CampaignId = campaignId,
        AddedBy = userId,
    };

    public static CharacterClass NuovaClasse(PackageClass p, string campaignId, string? userId) => new()
    {
        Name = p.Name,
        HitDie = p.HitDie,
        PrimaryAbility = p.PrimaryAbility,
        SavingThrows = Unisci(p.SavingThrows),
        SkillChoices = DescriviScelte(p.SkillChoices) ?? string.Empty,
        SourceId = p.Id,
        CampaignId = campaignId,
        AddedBy = userId,
    };

    public static Background NuovoBackground(PackageBackground p, string campaignId, string? userId) => new()
    {
        Name = p.Name,
        Description = p.Description,
        AbilityScores = Unisci(p.AbilityScores),
        OriginFeat = p.OriginFeat,
        SkillProficiencies = Unisci(p.SkillProficiencies),
        ToolProficiency = p.ToolProficiency,
        Equipment = p.Equipment,
        SourceId = p.Id,
        CampaignId = campaignId,
        AddedBy = userId,
    };

    public static Spell NuovoIncantesimo(PackageSpell p, string campaignId, string? userId) => new()
    {
        Name = p.Name,
        Level = p.Level ?? 0,
        School = p.School,
        CastingTime = p.CastingTime,
        Range = p.Range,
        Components = p.Components,
        Duration = p.Duration,
        Description = p.Description,
        Classes = Unisci(p.Classes),
        SourceId = p.Id,
        CampaignId = campaignId,
        AddedBy = userId,
    };

    public static Monster NuovoMostro(PackageMonster p, string campaignId, string? userId) => new()
    {
        Name = p.Name,
        ChallengeRating = p.ChallengeRating,
        ArmorClass = p.ArmorClass ?? 10,
        HitPoints = p.HitPoints,
        Description = p.Description,
        SourceId = p.Id,
        CampaignId = campaignId,
        AddedBy = userId,
    };

    // ---- Aggiornamento: solo i campi che il file porta davvero ----

    public static void ApplicaSpecie(PackageSpecies p, Race r)
    {
        Scrivi(p.Name, v => r.Name = v);
        Scrivi(p.Description, v => r.Description = v);
        Scrivi(p.Traits, v => r.Traits = v);
        if (p.Speed is not null)
        {
            r.Speed = p.Speed.Value;
            r.SpeedUnit = UnitaValida(p.Speed.Unit);
        }
        // r.Languages e i sei bonus di caratteristica restano: il formato non li trasporta.
    }

    public static void ApplicaClasse(PackageClass p, CharacterClass c)
    {
        Scrivi(p.Name, v => c.Name = v);
        Scrivi(p.HitDie, v => c.HitDie = v);
        Scrivi(p.PrimaryAbility, v => c.PrimaryAbility = v);
        Scrivi(Unisci(p.SavingThrows), v => c.SavingThrows = v);
        Scrivi(DescriviScelte(p.SkillChoices), v => c.SkillChoices = v);
        // c.Description, c.Features, c.ArmorProficiencies, c.WeaponProficiencies restano.
    }

    public static void ApplicaBackground(PackageBackground p, Background b)
    {
        Scrivi(p.Name, v => b.Name = v);
        Scrivi(p.Description, v => b.Description = v);
        Scrivi(Unisci(p.AbilityScores), v => b.AbilityScores = v);
        Scrivi(p.OriginFeat, v => b.OriginFeat = v);
        Scrivi(Unisci(p.SkillProficiencies), v => b.SkillProficiencies = v);
        Scrivi(p.ToolProficiency, v => b.ToolProficiency = v);
        Scrivi(p.Equipment, v => b.Equipment = v);
    }

    public static void ApplicaIncantesimo(PackageSpell p, Spell s)
    {
        Scrivi(p.Name, v => s.Name = v);
        // Il livello si applica SOLO se il file lo dichiara: con `int` un campo assente valeva 0,
        // e 0 è un livello legittimo — un incantesimo di livello 3 diventava un trucchetto.
        if (p.Level is not null) s.Level = p.Level.Value;
        Scrivi(p.School, v => s.School = v);
        Scrivi(p.CastingTime, v => s.CastingTime = v);
        Scrivi(p.Range, v => s.Range = v);
        Scrivi(p.Components, v => s.Components = v);
        Scrivi(p.Duration, v => s.Duration = v);
        Scrivi(p.Description, v => s.Description = v);
        Scrivi(Unisci(p.Classes), v => s.Classes = v);
    }

    public static void ApplicaMostro(PackageMonster p, Monster m)
    {
        Scrivi(p.Name, v => m.Name = v);
        Scrivi(p.ChallengeRating, v => m.ChallengeRating = v);
        if (p.ArmorClass is not null) m.ArmorClass = p.ArmorClass.Value;
        Scrivi(p.HitPoints, v => m.HitPoints = v);
        Scrivi(p.Description, v => m.Description = v);
        // Le sei caratteristiche, size, type, alignment, speed e abilities restano: azzerarle
        // riporterebbe a 10 le statistiche di un mostro completato a mano.
    }

    // Un campo vuoto nel file significa "non lo so", non "cancellalo": il parser valida solo id e
    // nome, quindi una voce minimale è legittima e non deve svuotare una riga già compilata.
    private static void Scrivi(string? value, Action<string> assegna)
    {
        if (!string.IsNullOrWhiteSpace(value)) assegna(value);
    }

    /// <summary>Unica formulazione della descrizione delle scelte di abilità: la usano creazione,
    /// aggiornamento e "duplica e modifica" (Task 6), e tenerle separate le farebbe divergere il
    /// giorno in cui il formato cambia. Per questo è <b>public</b>.
    ///
    /// Restituisce <c>null</c> quando il file non dichiara le scelte: è ciò che permette a
    /// <c>ApplicaClasse</c> di non svuotare una colonna già compilata.</summary>
    public static string? DescriviScelte(PackageSkillChoices? choices)
        => choices is null ? null : $"{choices.Count} fra: {Unisci(choices.From)}";
}
```

- [ ] **Step 9: Esegui i test e verifica che passino**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter PackageRowMergeTests`
Atteso: **18 casi PASSATI** (9 `Fact` + 9 `InlineData` della `Theory`).

- [ ] **Step 10: Verifica build**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori.

- [ ] **Step 11: Commit**

```bash
git add Services/PackageImportPlan.cs Services/PackageRowMerge.cs Models/Packages/CatalogPackage.cs \
        Tests/PackageImportPlanTests.cs Tests/PackageRowMergeTests.cs
git commit -m "feat(import): piano di import e conversione delle righe che non azzera i dati esistenti"
```

---

### Task 3: Materializzazione degli incantesimi (decisione pura)

**File:**
- Crea: `Services/SpellMaterialization.cs`
- Test: `Tests/SpellMaterializationTests.cs`

**Interfacce:**
- Consuma: `CatalogKey`, `CatalogMerge.Representative`, `Models/Spell.cs`, `PackageSpell`.
- Produce: `SpellMaterializationResult(Spell? Existing, Spell? ToInsert)` e
  `SpellMaterialization.Resolve(packageSpell, campaignSpells, campaignId, userId)`.

> «Quando un PG aggiunge alla propria lista un incantesimo di pacchetto, l'app inserisce quella singola
> voce in `spells` per la campagna. Se una riga con quel `source_id` esiste già, la riusa» (§4.4). Qui
> sta solo la decisione; la scrittura è del Task 4 e l'aggancio alla scheda del Task 8.

- [ ] **Step 1: Scrivi i test (falliranno)**

`Tests/SpellMaterializationTests.cs`:

```csharp
using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;

namespace DndCompanion.Tests;

public class SpellMaterializationTests
{
    private static PackageSpell Voce() => new()
    {
        Id = "srd-2024-it/palla-di-fuoco",
        Name = "Palla di Fuoco",
        Level = 3,
        School = "Evocazione",
        CastingTime = "Azione",
        Range = "45 metri",
        Components = "V, S, M",
        Duration = "Istantanea",
        Description = "Un lampo di luce…",
        Classes = { "Mago", "Stregone" },
    };

    [Fact]
    public void Resolve_NessunaRigaCorrispondente_ProponeLInserimento()
    {
        var esito = SpellMaterialization.Resolve(Voce(), Array.Empty<Spell>(), "c1", "utente-1");

        Assert.Null(esito.Existing);
        Assert.NotNull(esito.ToInsert);
        Assert.Equal("srd-2024-it/palla-di-fuoco", esito.ToInsert!.SourceId);
        Assert.Equal("Palla di Fuoco", esito.ToInsert.Name);
        Assert.Equal(3, esito.ToInsert.Level);
        Assert.Equal("c1", esito.ToInsert.CampaignId);
        Assert.Equal("utente-1", esito.ToInsert.AddedBy);
        // Le classi del pacchetto sono una lista, la colonna è testo: vanno unite.
        Assert.Equal("Mago, Stregone", esito.ToInsert.Classes);
    }

    [Fact]
    public void Resolve_RigaConLaStessaProvenienza_LaRiusa()
    {
        var esistente = new Spell
        {
            Id = "uuid-1", SourceId = "srd-2024-it/palla-di-fuoco",
            Name = "Palla di Fuoco", CampaignId = "c1"
        };

        var esito = SpellMaterialization.Resolve(Voce(), new[] { esistente }, "c1", "utente-1");

        Assert.Null(esito.ToInsert);
        Assert.Equal("uuid-1", esito.Existing!.Id);
    }

    // Chi ha già digitato a mano "Palla di Fuoco" non deve ritrovarsene due: la sua riga vince (§6).
    [Fact]
    public void Resolve_RigaOmonimaSenzaProvenienza_LaRiusa()
    {
        var esistente = new Spell { Id = "uuid-1", SourceId = null, Name = "palla di fuoco", CampaignId = "c1" };

        var esito = SpellMaterialization.Resolve(Voce(), new[] { esistente }, "c1", "utente-1");

        Assert.Null(esito.ToInsert);
        Assert.Equal("uuid-1", esito.Existing!.Id);
    }

    [Fact]
    public void Resolve_NomiDiversiSoloPerAccento_ContanoComeLaStessaVoce()
    {
        var esistente = new Spell { Id = "uuid-1", Name = "INVISIBILITA", CampaignId = "c1" };
        var voce = new PackageSpell { Id = "srd-2024-it/invisibilita", Name = "Invisibilità" };

        var esito = SpellMaterialization.Resolve(voce, new[] { esistente }, "c1", "utente-1");

        Assert.Equal("uuid-1", esito.Existing!.Id);
    }

    [Fact]
    public void Resolve_PiuRigheOmonime_PrendeIlRappresentante()
    {
        var righe = new[]
        {
            new Spell { Id = "uuid-b", SourceId = "srd-2024-it/palla-di-fuoco", Name = "Palla di Fuoco", CampaignId = "c1" },
            new Spell { Id = "uuid-a", SourceId = null, Name = "Palla di Fuoco", CampaignId = "c1" },
        };

        var esito = SpellMaterialization.Resolve(Voce(), righe, "c1", "utente-1");

        // Il rappresentante è la riga SENZA provenienza: è la voce propria dell'utente.
        Assert.Equal("uuid-a", esito.Existing!.Id);
    }

    // Le righe di un'ALTRA campagna non devono mai entrare nella decisione: se la lista in memoria
    // ne contenesse, si riuserebbe un uuid che la chiave esterna di questa campagna non può puntare.
    [Fact]
    public void Resolve_RigaDiUnAltraCampagna_Ignorata()
    {
        var altrove = new Spell
        {
            Id = "uuid-1", SourceId = "srd-2024-it/palla-di-fuoco",
            Name = "Palla di Fuoco", CampaignId = "c2"
        };

        var esito = SpellMaterialization.Resolve(Voce(), new[] { altrove }, "c1", "utente-1");

        Assert.Null(esito.Existing);
        Assert.NotNull(esito.ToInsert);
    }
}
```

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter SpellMaterializationTests`
Atteso: FALLIMENTO di compilazione — `SpellMaterialization` non esiste.

- [ ] **Step 3: Implementa l'helper**

`Services/SpellMaterialization.cs`:

```csharp
using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Esito della decisione: o una riga già presente da riusare, o una riga nuova da
/// inserire. Mai entrambe, mai nessuna.</summary>
public sealed record SpellMaterializationResult(Spell? Existing, Spell? ToInsert);

/// <summary>Un incantesimo che vive solo nel file non può essere aggiunto alla lista di un PG:
/// `character_spells.spell_id` è una chiave esterna verso `spells(id)` (§4.1). Prima di creare il
/// legame, la voce di pacchetto va materializzata — ma solo se non c'è già (§4.4).
/// Logica pura: decide, non scrive.</summary>
public static class SpellMaterialization
{
    public static SpellMaterializationResult Resolve(
        PackageSpell packageSpell,
        IEnumerable<Spell> campaignSpells,
        string campaignId,
        string? userId)
    {
        // Il filtro sulla campagna non è ridondante: la lista arriva da una pagina che potrebbe
        // averla caricata per un'altra campagna, e riusare l'uuid di una riga che sta altrove
        // creerebbe un legame che la chiave esterna di QUESTA campagna non regge.
        var candidate = campaignSpells
            .Where(s => s.CampaignId == campaignId)
            .Where(s => CatalogKey.For(s.SourceId, s.Name) == packageSpell.Id
                        || CatalogKey.NormalizeName(s.Name) == CatalogKey.NormalizeName(packageSpell.Name))
            .ToList();

        if (candidate.Count > 0)
        {
            var scelta = CatalogMerge.Representative(candidate, s => s.SourceId, s => s.Id)!;
            return new SpellMaterializationResult(scelta, null);
        }

        return new SpellMaterializationResult(null, new Spell
        {
            Name = packageSpell.Name,
            // `?? 0`: PackageSpell.Level è int? (Task 2), perché il parser accetta voci minimali e
            // con `int` un livello assente sarebbe indistinguibile da un trucchetto.
            Level = packageSpell.Level ?? 0,
            School = packageSpell.School,
            CastingTime = packageSpell.CastingTime,
            Range = packageSpell.Range,
            Components = packageSpell.Components,
            Duration = packageSpell.Duration,
            Description = packageSpell.Description,
            // La colonna è testo libero, la voce di pacchetto è una lista: si uniscono con lo
            // stesso separatore che SpellClassNames sa poi spezzare.
            Classes = string.Join(", ", packageSpell.Classes),
            SourceId = packageSpell.Id,
            CampaignId = campaignId,
            AddedBy = userId,
        });
    }
}
```

- [ ] **Step 4: Esegui i test e verifica che passino**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter SpellMaterializationTests`
Atteso: 6 test PASSATI.

- [ ] **Step 5: Commit**

```bash
git add Services/SpellMaterialization.cs Tests/SpellMaterializationTests.cs
git commit -m "feat(incantesimi): decisione di materializzazione delle voci di pacchetto"
```

---

### Task 4: Repository — scrittura e rimozione per provenienza

**File:**
- Modifica: `Services/Repositories/SpellRepository.cs`, `RaceRepository.cs`, `ClassRepository.cs`,
  `MonsterRepository.cs`, `BackgroundRepository.cs`
- Modifica: `Services/Repositories/CharacterSpellRepository.cs`

**Interfacce:**
- Consuma: niente dai task precedenti.
- Produce, su ciascuno dei cinque repository di catalogo:
  `Task<List<T>> CreateManyAsync(List<T> rows)` e
  `Task DeleteByIdsAsync(List<string> ids)`.
  In più, su `ISpellRepository`: `Task<Spell?> GetOneBySourceAsync(string campaignId, string sourceId)`.
  Su `ICharacterSpellRepository`: `Task<List<CharacterSpell>> GetBySpellIdsAsync(List<string> spellIds)`.

> Questi metodi non contengono decisioni: quelle stanno negli helper. Qui c'è solo il modo corretto di
> parlare con PostgREST. **Non c'è un test unitario** per questo task — i repository toccano la rete e
> il progetto non li mocka; li verificano i task che li usano.
>
> ⚠️ **Niente `Upsert`**, per la ragione misurata nelle decisioni in testa al piano: serializza la
> chiave primaria e la manda vuota. Gli aggiornamenti passano dai `Update*Async` che i repository
> hanno **già**, riga per riga; qui si aggiungono solo la creazione in blocco e la cancellazione per
> elenco di id.

- [ ] **Step 1: Aggiungi i metodi a `ISpellRepository`**

In `Services/Repositories/SpellRepository.cs`, estendi l'interfaccia:

```csharp
public interface ISpellRepository
{
    Task<List<Spell>> GetSpellsForCampaignAsync(string campaignId);
    Task<List<Spell>> SearchSpellsAsync(string campaignId, string query);
    Task<Spell?> CreateSpellAsync(Spell spell);
    Task<Spell?> UpdateSpellAsync(Spell spell);
    Task DeleteSpellAsync(string id);

    /// <summary>Creazione in blocco per l'import: una sola richiesta, quindi una sola transazione
    /// (§9). Insert e non Upsert: è l'unico dei due che rispetta [PrimaryKey("id", false)].</summary>
    Task<List<Spell>> CreateManyAsync(List<Spell> rows);

    /// <summary>La riga di questa campagna con quella provenienza, se c'è. Serve alla
    /// materializzazione (§4.4), che legge prima di inserire e rilegge se l'inserimento urta il
    /// vincolo di unicità.</summary>
    Task<Spell?> GetOneBySourceAsync(string campaignId, string sourceId);

    /// <summary>Cancellazione per elenco di id. NON per prefisso: un LIKE costruito con testo
    /// digitato dall'utente colpirebbe righe che l'anteprima non ha mai mostrato.</summary>
    Task DeleteByIdsAsync(List<string> ids);
}
```

- [ ] **Step 2: Implementa i metodi in `SpellRepository`**

Aggiungi in fondo alla classe `SpellRepository`:

```csharp
    public async Task<List<Spell>> CreateManyAsync(List<Spell> rows)
    {
        if (rows.Count == 0) return new List<Spell>();

        var client = await _supabase.GetClientAsync();
        var response = await client.From<Spell>().Insert(rows);
        return response.Models;
    }

    public async Task<Spell?> GetOneBySourceAsync(string campaignId, string sourceId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Spell>()
            .Where(s => s.CampaignId == campaignId)
            .Filter("source_id", Postgrest.Constants.Operator.Equals, sourceId)
            .Get();
        return response.Models.FirstOrDefault();
    }

    // A blocchi: gli id finiscono nella query string come id=in.(…), e un import completo può
    // superarne il limite di lunghezza — proprio il caso per cui la rimozione in blocco esiste.
    // Il Delete di questa libreria non restituisce nulla da controllare (gotcha noto, §3 di
    // DA-FARE): chi chiama riconta dopo, invece di fidarsi.
    public async Task DeleteByIdsAsync(List<string> ids)
    {
        if (ids.Count == 0) return;

        var client = await _supabase.GetClientAsync();
        foreach (var blocco in ids.Chunk(100))
        {
            await client.From<Spell>()
                .Filter("id", Postgrest.Constants.Operator.In, blocco.Cast<object>().ToList())
                .Delete();
        }
    }
```

- [ ] **Step 3: Replica i due metodi sugli altri quattro repository**

Stessa forma, cambiando solo il tipo, il nome del parametro lambda e — dove serve — le `using`.
`GetOneBySourceAsync` **solo** su `SpellRepository`: è l'unico catalogo referenziato da una chiave
esterna, quindi l'unico che ha bisogno della materializzazione (§4.4).

| File | Tipo | Interfaccia da estendere |
|---|---|---|
| `Services/Repositories/RaceRepository.cs` | `Race` | `IRaceRepository` |
| `Services/Repositories/ClassRepository.cs` | `CharacterClass` | `IClassRepository` |
| `Services/Repositories/MonsterRepository.cs` | `Monster` | `IMonsterRepository` |
| `Services/Repositories/BackgroundRepository.cs` | `Background` | `IBackgroundRepository` |

Per esempio, in `RaceRepository`:

```csharp
    public async Task<List<Race>> CreateManyAsync(List<Race> rows)
    {
        if (rows.Count == 0) return new List<Race>();

        var client = await _supabase.GetClientAsync();
        var response = await client.From<Race>().Insert(rows);
        return response.Models;
    }

    public async Task DeleteByIdsAsync(List<string> ids)
    {
        if (ids.Count == 0) return;

        var client = await _supabase.GetClientAsync();
        foreach (var blocco in ids.Chunk(100))
        {
            await client.From<Race>()
                .Filter("id", Postgrest.Constants.Operator.In, blocco.Cast<object>().ToList())
                .Delete();
        }
    }
```

- [ ] **Step 4: Aggiungi la lettura dei legami PG↔incantesimo**

Serve alla rimozione per provenienza, che deve dire **quanti personaggi perderanno un incantesimo**
per via del `ON DELETE CASCADE` (§8). La policy `character_spells_select` consente la lettura a
qualunque **membro** della campagna, non solo al proprietario del PG: il conteggio è quindi completo.

In `Services/Repositories/CharacterSpellRepository.cs`, estendi l'interfaccia e la classe:

```csharp
    /// <summary>I legami che puntano a uno degli incantesimi indicati. Serve a misurare l'impatto
    /// di una cancellazione prima di eseguirla: character_spells_spell_id_fkey è ON DELETE CASCADE,
    /// quindi togliere un incantesimo dal catalogo lo toglie dalle schede che lo conoscono (§8).</summary>
    Task<List<CharacterSpell>> GetBySpellIdsAsync(List<string> spellIds);
```

```csharp
    public async Task<List<CharacterSpell>> GetBySpellIdsAsync(List<string> spellIds)
    {
        if (spellIds.Count == 0) return new List<CharacterSpell>();

        var client = await _supabase.GetClientAsync();
        var risultato = new List<CharacterSpell>();

        // A blocchi come DeleteByIdsAsync, e per la stessa ragione: con un catalogo importato per
        // intero l'elenco di id supererebbe la lunghezza utile della query string, e il 414 si
        // presenterebbe all'utente come un generico errore nel calcolo dell'anteprima.
        foreach (var blocco in spellIds.Chunk(100))
        {
            var response = await client.From<CharacterSpell>()
                .Filter("spell_id", Postgrest.Constants.Operator.In, blocco.Cast<object>().ToList())
                .Get();
            risultato.AddRange(response.Models);
        }
        return risultato;
    }
```

- [ ] **Step 5: Aggiorna il finto repository già presente nei test**

`Tests/CatalogServiceTests.cs` contiene già `FakeBackgroundRepository : IBackgroundRepository`.
Avendo esteso l'interfaccia, **il progetto di test non compila più** (CS0535). Aggiungi al finto i
due metodi nuovi:

```csharp
        public Task<List<Background>> CreateManyAsync(List<Background> rows) => Task.FromResult(rows);
        public Task DeleteByIdsAsync(List<string> ids) => Task.CompletedTask;
```

Senza questo step lo Step 6 fallisce in compilazione e lo Step 7 committerebbe un albero rotto.

- [ ] **Step 6: Verifica build e suite**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori.

Comando: `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: tutti verdi (nessun test nuovo qui, ma la suite deve **compilare**).

- [ ] **Step 7: Commit**

```bash
git add Services/Repositories/ Tests/CatalogServiceTests.cs
git commit -m "feat(dati): creazione in blocco, lettura per provenienza e cancellazione per id"
```

---

### Task 5: `CatalogService` esteso ai cinque cataloghi

**File:**
- Modifica: `Services/CatalogService.cs`
- Test: `Tests/CatalogServiceTests.cs` (casi nuovi)

**Interfacce:**
- Consuma: i cinque repository (Task 4 per i metodi nuovi, quelli di lettura esistevano già),
  `CatalogMerge.HiddenPackageIds`, `CampaignCatalogs` (Task 2).
- Produce: su `ICatalogService` — `GetRacesAsync`, `GetClassesAsync`, `GetSpellsAsync`,
  `GetMonstersAsync` (tutte `Task<CatalogView<TRow, TPkg>>`) e
  `Task<CampaignCatalogs> GetCampaignCatalogsAsync(string campaignId)`.

> Il commento in testa a `CatalogService` lo prevede già: «in Fase 2 gli altri quattro cataloghi
> aggiungeranno il proprio metodo accanto a `GetBackgroundsAsync`, invece di replicare
> l'orchestrazione in cinque `.razor`».

- [ ] **Step 1: Scrivi i test nuovi (falliranno)**

Il file `Tests/CatalogServiceTests.cs` esiste già con il proprio `CountingHandler` e
`FakeBackgroundRepository`. **Non riscriverlo**: aggiungi in coda alla classe i finti repository
mancanti e i casi nuovi, e adegua l'helper `Service(...)` al costruttore a sei dipendenze.

Sostituisci il metodo `Service(...)` esistente con:

```csharp
    private sealed class FakeRaceRepository : IRaceRepository
    {
        private readonly List<Race> _rows;
        public FakeRaceRepository(params Race[] rows) => _rows = rows.ToList();

        public Task<List<Race>> GetRacesForCampaignAsync(string campaignId) => Task.FromResult(_rows);
        public Task<Race?> CreateRaceAsync(Race r) => Task.FromResult<Race?>(r);
        public Task<Race?> UpdateRaceAsync(Race r) => Task.FromResult<Race?>(r);
        public Task DeleteRaceAsync(string id) => Task.CompletedTask;
        public Task<List<Race>> CreateManyAsync(List<Race> rows) => Task.FromResult(rows);
        public Task DeleteByIdsAsync(List<string> ids) => Task.CompletedTask;
    }

    private sealed class FakeClassRepository : IClassRepository
    {
        public Task<List<CharacterClass>> GetClassesForCampaignAsync(string campaignId)
            => Task.FromResult(new List<CharacterClass>());
        public Task<CharacterClass?> CreateClassAsync(CharacterClass c) => Task.FromResult<CharacterClass?>(c);
        public Task<CharacterClass?> UpdateClassAsync(CharacterClass c) => Task.FromResult<CharacterClass?>(c);
        public Task DeleteClassAsync(string id) => Task.CompletedTask;
        public Task<List<CharacterClass>> CreateManyAsync(List<CharacterClass> rows) => Task.FromResult(rows);
        public Task DeleteByIdsAsync(List<string> ids) => Task.CompletedTask;
    }

    private sealed class FakeSpellRepository : ISpellRepository
    {
        private readonly List<Spell> _rows;
        public FakeSpellRepository(params Spell[] rows) => _rows = rows.ToList();

        public Task<List<Spell>> GetSpellsForCampaignAsync(string campaignId) => Task.FromResult(_rows);
        public Task<List<Spell>> SearchSpellsAsync(string c, string q) => Task.FromResult(_rows);
        public Task<Spell?> CreateSpellAsync(Spell s) => Task.FromResult<Spell?>(s);
        public Task<Spell?> UpdateSpellAsync(Spell s) => Task.FromResult<Spell?>(s);
        public Task DeleteSpellAsync(string id) => Task.CompletedTask;
        public Task<List<Spell>> CreateManyAsync(List<Spell> rows) => Task.FromResult(rows);
        public Task<Spell?> GetOneBySourceAsync(string c, string sourceId)
            => Task.FromResult(_rows.FirstOrDefault(s => s.SourceId == sourceId));
        public Task DeleteByIdsAsync(List<string> ids) => Task.CompletedTask;
    }

    private sealed class FakeMonsterRepository : IMonsterRepository
    {
        public Task<List<Monster>> GetMonstersForCampaignAsync(string campaignId)
            => Task.FromResult(new List<Monster>());
        public Task<Monster?> CreateMonsterAsync(Monster m) => Task.FromResult<Monster?>(m);
        public Task<Monster?> UpdateMonsterAsync(Monster m) => Task.FromResult<Monster?>(m);
        public Task DeleteMonsterAsync(string id) => Task.CompletedTask;
        public Task<List<Monster>> CreateManyAsync(List<Monster> rows) => Task.FromResult(rows);
        public Task DeleteByIdsAsync(List<string> ids) => Task.CompletedTask;
    }

    private static CatalogService Service(CountingHandler handler, params Background[] dbRows)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://esempio.test/") },
               new FakeBackgroundRepository(dbRows), new FakeRaceRepository(),
               new FakeClassRepository(), new FakeSpellRepository(), new FakeMonsterRepository());

    private static CatalogService ServiceConRazze(CountingHandler handler, params Race[] righe)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://esempio.test/") },
               new FakeBackgroundRepository(), new FakeRaceRepository(righe),
               new FakeClassRepository(), new FakeSpellRepository(), new FakeMonsterRepository());
```

Il campo `Package` della classe di test contiene solo `feats` e `backgrounds`: aggiungi una `species`,
sostituendo la costante con:

```csharp
    private const string Package = """
    {
      "schemaVersion": 1, "id": "srd-2024-it", "name": "SRD", "edition": "2024",
      "language": "it", "version": "1.0.0",
      "feats": [ { "id": "srd-2024-it/artigiano-talento", "name": "Artefice", "description": "…" } ],
      "backgrounds": [
        { "id": "srd-2024-it/artigiano", "name": "Artigiano" },
        { "id": "srd-2024-it/soldato", "name": "Soldato" }
      ],
      "species": [
        { "id": "srd-2024-it/elfo", "name": "Elfo", "size": "Media",
          "speed": { "value": 9, "unit": "m" }, "traits": "Scurovisione" },
        { "id": "srd-2024-it/nano", "name": "Nano", "size": "Media",
          "speed": { "value": 7, "unit": "m" }, "traits": "Scurovisione" }
      ]
    }
    """;
```

> ⚠️ `PackageSpeed.Value` è `int`: un decimale (il Nano del manuale italiano corre 7,5 m) farebbe
> fallire la deserializzazione dell'intero pacchetto. È un limite reale del formato, da affrontare
> in **Fase 3** quando il contenuto vero verrà tradotto — non da aggirare qui cambiando il tipo.

Poi aggiungi i casi nuovi:

```csharp
    [Fact]
    public async Task GetRacesAsync_SenzaRigheDiDatabase_MostraTutteLeVociDiPacchetto()
    {
        var vista = await ServiceConRazze(new CountingHandler(Package)).GetRacesAsync("campagna-1");

        Assert.Empty(vista.DbRows);
        Assert.Equal(2, vista.PackageEntries.Count);
    }

    [Fact]
    public async Task GetRacesAsync_RigaOmonima_OscuraLaVoceDiPacchetto()
    {
        var riga = new Race { Id = "uuid-1", Name = "Elfo", CampaignId = "campagna-1" };

        var vista = await ServiceConRazze(new CountingHandler(Package), riga).GetRacesAsync("campagna-1");

        Assert.Single(vista.DbRows);
        Assert.Single(vista.PackageEntries);
        Assert.Equal("srd-2024-it/nano", vista.PackageEntries[0].Id);
    }

    [Fact]
    public async Task GetCampaignCatalogsAsync_RestituisceLeCinqueListeDelDatabase()
    {
        var riga = new Race { Id = "uuid-1", Name = "Elfo", CampaignId = "campagna-1" };

        var cataloghi = await ServiceConRazze(new CountingHandler(Package), riga)
            .GetCampaignCatalogsAsync("campagna-1");

        // Solo righe di DATABASE: import ed export ragionano su ciò che esiste davvero,
        // non sull'unione mostrata dalla UI.
        Assert.Single(cataloghi.Races);
        Assert.Empty(cataloghi.Backgrounds);
        Assert.Empty(cataloghi.Spells);
    }
```

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CatalogServiceTests`
Atteso: FALLIMENTO di compilazione — il costruttore di `CatalogService` prende due argomenti.

- [ ] **Step 3: Estendi l'interfaccia**

In `Services/CatalogService.cs`, aggiungi a `ICatalogService`, dopo `GetBackgroundsAsync`:

```csharp
    /// <summary>Specie della campagna unite alle voci di pacchetto non già coperte (§6).</summary>
    Task<CatalogView<Race, PackageSpecies>> GetRacesAsync(string campaignId);

    /// <summary>Classi della campagna unite alle voci di pacchetto non già coperte (§6).</summary>
    Task<CatalogView<CharacterClass, PackageClass>> GetClassesAsync(string campaignId);

    /// <summary>Incantesimi della campagna uniti alle voci di pacchetto non già coperte (§6).</summary>
    Task<CatalogView<Spell, PackageSpell>> GetSpellsAsync(string campaignId);

    /// <summary>Mostri della campagna uniti alle voci di pacchetto non già coperte (§6).</summary>
    Task<CatalogView<Monster, PackageMonster>> GetMonstersAsync(string campaignId);

    /// <summary>Le cinque liste come stanno nel database, senza unione né oscuramenti: import ed
    /// export ragionano su ciò che esiste davvero, non su ciò che la UI mostra.</summary>
    Task<CampaignCatalogs> GetCampaignCatalogsAsync(string campaignId);
```

- [ ] **Step 4: Estendi l'implementazione**

Sostituisci i campi e il costruttore di `CatalogService`:

```csharp
    private readonly HttpClient _http;
    private readonly IBackgroundRepository _backgrounds;
    private readonly IRaceRepository _races;
    private readonly IClassRepository _classes;
    private readonly ISpellRepository _spells;
    private readonly IMonsterRepository _monsters;

    public CatalogService(
        HttpClient http,
        IBackgroundRepository backgrounds,
        IRaceRepository races,
        IClassRepository classes,
        ISpellRepository spells,
        IMonsterRepository monsters)
    {
        _http = http;
        _backgrounds = backgrounds;
        _races = races;
        _classes = classes;
        _spells = spells;
        _monsters = monsters;
    }
```

Aggiungi in fondo alla classe i metodi nuovi, fattorizzando l'unione (che è identica per tutti e
cinque) in un aiutante privato:

```csharp
    public async Task<CatalogView<Race, PackageSpecies>> GetRacesAsync(string campaignId)
        => await MergeAsync(
            await _races.GetRacesForCampaignAsync(campaignId),
            p => p.Species, p => p.Id, p => p.Name,
            r => r.SourceId, r => r.Name);

    public async Task<CatalogView<CharacterClass, PackageClass>> GetClassesAsync(string campaignId)
        => await MergeAsync(
            await _classes.GetClassesForCampaignAsync(campaignId),
            p => p.Classes, p => p.Id, p => p.Name,
            r => r.SourceId, r => r.Name);

    public async Task<CatalogView<Spell, PackageSpell>> GetSpellsAsync(string campaignId)
        => await MergeAsync(
            await _spells.GetSpellsForCampaignAsync(campaignId),
            p => p.Spells, p => p.Id, p => p.Name,
            r => r.SourceId, r => r.Name);

    public async Task<CatalogView<Monster, PackageMonster>> GetMonstersAsync(string campaignId)
        => await MergeAsync(
            await _monsters.GetMonstersForCampaignAsync(campaignId),
            p => p.Monsters, p => p.Id, p => p.Name,
            r => r.SourceId, r => r.Name);

    public async Task<CampaignCatalogs> GetCampaignCatalogsAsync(string campaignId)
    {
        // In parallelo: sono cinque letture indipendenti, e la schermata di import le attende tutte.
        var razze = _races.GetRacesForCampaignAsync(campaignId);
        var classi = _classes.GetClassesForCampaignAsync(campaignId);
        var incantesimi = _spells.GetSpellsForCampaignAsync(campaignId);
        var mostri = _monsters.GetMonstersForCampaignAsync(campaignId);
        var background = _backgrounds.GetBackgroundsForCampaignAsync(campaignId);

        await Task.WhenAll(razze, classi, incantesimi, mostri, background);

        return new CampaignCatalogs
        {
            Races = razze.Result,
            Classes = classi.Result,
            Spells = incantesimi.Result,
            Monsters = mostri.Result,
            Backgrounds = background.Result,
        };
    }

    // L'unione è la stessa per tutti e cinque i cataloghi: le righe di database si mostrano sempre
    // tutte, le voci di pacchetto solo se nessuna riga già le copre (§4.3).
    private async Task<CatalogView<TRow, TPkg>> MergeAsync<TRow, TPkg>(
        List<TRow> dbRows,
        Func<CatalogPackage, List<TPkg>> sectionOf,
        Func<TPkg, string> packageIdOf,
        Func<TPkg, string> packageNameOf,
        Func<TRow, string?> sourceIdOf,
        Func<TRow, string> nameOf)
    {
        var package = await GetPackageAsync();
        if (package is null)
            return new CatalogView<TRow, TPkg>(dbRows, Array.Empty<TPkg>());

        var voci = sectionOf(package);
        var nascoste = CatalogMerge.HiddenPackageIds(
            voci, packageIdOf, packageNameOf, dbRows, sourceIdOf, nameOf);

        var visibili = voci.Where(v => !nascoste.Contains(packageIdOf(v))).ToList();
        return new CatalogView<TRow, TPkg>(dbRows, visibili);
    }
```

Riscrivi anche `GetBackgroundsAsync` usando lo stesso aiutante, così la regola vive in un posto solo:

```csharp
    public async Task<CatalogView<Background, PackageBackground>> GetBackgroundsAsync(string campaignId)
        => await MergeAsync(
            await _backgrounds.GetBackgroundsForCampaignAsync(campaignId),
            p => p.Backgrounds, p => p.Id, p => p.Name,
            r => r.SourceId, r => r.Name);
```

- [ ] **Step 5: Esegui i test e verifica che passino**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CatalogServiceTests`
Atteso: **15 test PASSATI** — i 12 già presenti nel file più i 3 nuovi. Se i test preesistenti sui
background falliscono, l'aiutante `MergeAsync` non replica il comportamento originale: confronta con
la versione in `git show HEAD:Services/CatalogService.cs`.

- [ ] **Step 6: Verifica build**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori. La registrazione in `Program.cs` non cambia: la DI risolve da sé le
dipendenze nuove, che sono tutte già registrate.

- [ ] **Step 7: Commit**

```bash
git add Services/CatalogService.cs Tests/CatalogServiceTests.cs
git commit -m "feat(pacchetti): unione delle due sorgenti su tutti e cinque i cataloghi"
```

---

### Task 6: Marcatura e "duplica e modifica" — Razze e Classi

**File:**
- Modifica: `Pages/Races.razor`, `Pages/Races.razor.css`
- Modifica: `Pages/Classes.razor`, `Pages/Classes.razor.css`

**Interfacce:**
- Consuma: `ICatalogService.GetRacesAsync`/`GetClassesAsync` (Task 5),
  `CatalogKey.IsFromAppPackage`/`NormalizeName`, `AccessControl.CanEdit`.
- Produce: nessuna API — solo comportamento di pagina.

> **`Pages/Backgrounds.razor` è il modello.** Aprilo e replica: il record `Entry` che unifica riga e
> voce, `ToEntry` nelle due versioni, `FilteredEntries`, `PuoModificare`, `DuplicaDaPacchetto`,
> `DuplicaDaRigaImportata`, e soprattutto `ReloadAsync` richiamato dopo **ogni** scrittura.
> **Non inventare pattern nuovi.**

- [ ] **Step 1: Rileggi il modello**

Comando: `cat Pages/Backgrounds.razor`

Nota in particolare **perché** `ReloadAsync` esiste: la lista non è solo righe di database, e quali
voci di pacchetto siano visibili dipende da cosa c'è nel database. Ritoccare `dbRows` in memoria dopo
"duplica e modifica" mostrerebbe la copia **accanto** alla voce da cui deriva, e il doppione
sparirebbe solo cambiando pagina.

- [ ] **Step 2: Converti `Pages/Races.razor`**

1. Aggiungi `@using DndCompanion.Models.Packages` e `@inject ICatalogService Catalog`.
2. Sostituisci il campo `List<Race> allRaces` (`Pages/Races.razor:232` — si chiama così, non `races`)
   con `List<Race> dbRows` + `List<PackageSpecies> packageRows`.
3. Aggiungi il record `Entry` e le due `ToEntry`, sul modello di Backgrounds:

```csharp
    private sealed record Entry(
        string Key,
        string Name,
        string Description,
        int Speed,
        string SpeedUnit,
        string Traits,
        string Languages,
        bool IsPackage,
        bool FromAppPackage,
        Race? Db,
        PackageSpecies? Package);

    private static Entry ToEntry(Race r) => new(
        "db:" + r.Id, r.Name, r.Description, r.Speed, r.SpeedUnit, r.Traits, r.Languages,
        false, CatalogKey.IsFromAppPackage(r.SourceId), r, null);

    // La velocità del pacchetto porta con sé la propria unità: è il motivo per cui speed_unit
    // esiste come colonna e non si deduce dalla sorgente (§4.5). L'unità passa SEMPRE da
    // PackageRowMerge.UnitaValida: races_speed_unit_check accetta solo 'm' e 'ft', e un file
    // scritto a mano può contenere "metri" o "feet".
    private static Entry ToEntry(PackageSpecies p) => new(
        "pkg:" + p.Id, p.Name, p.Description, p.Speed?.Value ?? 0,
        PackageRowMerge.UnitaValida(p.Speed?.Unit),
        p.Traits, string.Empty, true, true, null, p);
```

4. `DuplicaDaPacchetto` deve **portarsi dietro l'unità**, altrimenti 9 metri diventano 9 piedi:

```csharp
    private Race DuplicaDaPacchetto(PackageSpecies p) => new()
    {
        Name = p.Name,
        Description = p.Description,
        Speed = p.Speed?.Value ?? 9,
        // Senza questa riga la copia nasce con il default 'ft' del Model e il numero resta 9:
        // una specie che correva 9 metri si ritrova a 9 piedi (§4.5). E l'unità passa dall'helper,
        // non da un `?? "m"` scritto qui: il valore finisce dritto in una colonna con CHECK, e una
        // copia con "metri" o "FT" farebbe fallire il salvataggio con un 400 opaco.
        SpeedUnit = PackageRowMerge.UnitaValida(p.Speed?.Unit),
        Traits = p.Traits,
        SourceId = null,
        CampaignId = CurrentUser.CampaignId ?? string.Empty,
        AddedBy = CurrentUser.UserId,
    };
```

5. `PuoModificare`, `DuplicaDaRigaImportata`, `ReloadAsync` e `FilteredEntries`: copiali da
   Backgrounds adattando il tipo. Ricorda che `DuplicaDaRigaImportata` mette `AddedBy = CurrentUser.UserId`
   e **non** l'autore originale: `races_insert` richiede `added_by = auth.uid()`.
6. Nel markup, sostituisci il blocco dei comandi con i tre rami di Backgrounds (voce di pacchetto →
   "Duplica e modifica"; riga con provenienza dal pacchetto dell'app → "Duplica e modifica"; altrimenti
   → matita e cestino secondo `PuoModificare`), aggiungi il badge **e la classe sulla card**:

```razor
    <div class="race-card @(isExpanded ? "expanded" : "") @(entry.FromAppPackage ? "package-card" : "")">
```

```razor
                                    @if (entry.FromAppPackage)
                                    {
                                        <span class="package-badge">Dal manuale</span>
                                    }
```

> La classe sulla card non è decorativa: senza, il CSS dello Step 3 non ha nulla da selezionare e il
> task intitolato "marcatura" non marca niente. È così che lo fa `Pages/Backgrounds.razor:109`.

7. Nelle card mostra sempre l'unità, riusando l'unica fonte già dichiarata tale in
   `Services/FormValidation.cs:11-15` e già usata da `Pages/Races.razor:142` — non un confronto
   ordinale scritto qui, che su `"M"` mostrerebbe «ft»:

```razor
   @entry.Speed @(FormValidation.IsMetric(entry.SpeedUnit) ? "m" : "ft")
```
8. Aggiorna l'**empty state**: la condizione oggi è `allRaces.Count == 0` e va sostituita con
   `dbRows.Count == 0 && packageRows.Count == 0`. Senza, con un pacchetto caricato e nessuna riga
   propria una ricerca a vuoto direbbe «Nessuna razza nel database: aggiungetene col +» davanti a un
   elenco pieno. Il modello è `Pages/Backgrounds.razor:217`.

- [ ] **Step 3: Aggiungi il CSS della marcatura**

In `Pages/Races.razor.css`, copia da `Pages/Backgrounds.razor.css` le regole di `.package-card` e
`.package-badge`. **Solo token** (`var(--gold-dim)`, `var(--…)`), mai literal esadecimali.

> ⚠️ **Adatta il selettore.** In Backgrounds la regola è composta e legata alla classe della card di
> quella pagina — `.background-card.package-card { … }`. Copiata alla lettera qui non seleziona
> nulla, perché le card di questa pagina sono `.race-card`. Riscrivila come
> `.race-card.package-card { … }` (e altrettanto per l'eventuale variante `.expanded`).
> `.package-badge` invece è una classe autonoma e si copia tale e quale.

Comando di controllo:
```bash
grep -nE "#[0-9a-fA-F]{3,8}" Pages/Races.razor.css
```
Atteso: nessun risultato **nelle righe che aggiungi**. Se il file ne ha già altrove, non è compito di
questo task ripulirli — non aggiungerne di nuovi.

- [ ] **Step 4: Converti `Pages/Classes.razor` allo stesso modo**

Il record `Entry` per le classi:

```csharp
    private sealed record Entry(
        string Key,
        string Name,
        string Description,
        string HitDie,
        string PrimaryAbility,
        string SavingThrows,
        string SkillChoices,
        string Features,
        bool IsPackage,
        bool FromAppPackage,
        CharacterClass? Db,
        PackageClass? Package);

    private static Entry ToEntry(CharacterClass c) => new(
        "db:" + c.Id, c.Name, c.Description, c.HitDie, c.PrimaryAbility, c.SavingThrows,
        c.SkillChoices, c.Features, false, CatalogKey.IsFromAppPackage(c.SourceId), c, null);

    // Le liste del pacchetto diventano testo, come le colonne del Model. `levels` NON viene
    // riversata in `features`: contiene la progressione completa per livello e la consumerà il
    // motore di derivazione, che è fuori da questo spec (§3). Riversarla qui produrrebbe un muro
    // di testo in una colonna pensata per l'elenco dei privilegi.
    //
    // La descrizione delle scelte di abilità passa da PackageRowMerge.DescriviScelte, non da una
    // formula riscritta qui: è la stessa che usano import e aggiornamento, e tre copie
    // divergerebbero al primo cambio di formato.
    private static Entry ToEntry(PackageClass p) => new(
        "pkg:" + p.Id, p.Name, string.Empty, p.HitDie, p.PrimaryAbility,
        string.Join(", ", p.SavingThrows),
        PackageRowMerge.DescriviScelte(p.SkillChoices) ?? string.Empty,
        string.Empty, true, true, null, p);
```

`DuplicaDaPacchetto` per le classi:

```csharp
    private CharacterClass DuplicaDaPacchetto(PackageClass p) => new()
    {
        Name = p.Name,
        HitDie = p.HitDie,
        PrimaryAbility = p.PrimaryAbility,
        SavingThrows = string.Join(", ", p.SavingThrows),
        SkillChoices = PackageRowMerge.DescriviScelte(p.SkillChoices) ?? string.Empty,
        SourceId = null,
        CampaignId = CurrentUser.CampaignId ?? string.Empty,
        AddedBy = CurrentUser.UserId,
    };
```

Stessi tre rami di comandi, stesso `ReloadAsync`, stesso adeguamento dell'empty state
(`allClasses.Count == 0` → `dbRows.Count == 0 && packageRows.Count == 0`), stessa classe
condizionale sulla card — e nel CSS il selettore adattato a **`.class-card.package-card`**.

- [ ] **Step 5: Verifica a mano**

Comando: `dotnet run`

Verifica su `/races` e `/classes`, con la campagna attiva:
- l'elenco si carica come prima (senza pacchetto, il comportamento è identico a oggi);
- creare, modificare ed eliminare funzionano e la lista si aggiorna;
- il conteggio "N razze trovate" resta coerente.

> Il pacchetto non esiste ancora (arriva in Fase 3): non potrai vedere le voci "Dal manuale" finché
> non importerai un file col Task 10. Verificale allora, non ora.

- [ ] **Step 6: Verifica build e suite**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori.

Comando: `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: tutti verdi.

- [ ] **Step 7: Commit**

```bash
git add Pages/Races.razor Pages/Races.razor.css Pages/Classes.razor Pages/Classes.razor.css
git commit -m "feat(cataloghi): voci di pacchetto marcate e duplicabili in Razze e Classi"
```

---

### Task 7: Marcatura e "duplica e modifica" — Incantesimi e Mostri

**File:**
- Modifica: `Pages/Spells.razor`, `Pages/Spells.razor.css`
- Modifica: `Pages/Monsters.razor`, `Pages/Monsters.razor.css`

**Interfacce:**
- Consuma: `ICatalogService.GetSpellsAsync`/`GetMonstersAsync` (Task 5), `SpellClassNames.Matches`
  (Task 1), `ICharacterSpellRepository.GetBySpellIdsAsync` (Task 4), `CatalogKey`,
  `AccessControl.CanEdit`, `MonsterCatalog.ParseChallengeRating` (esistente).
- Produce: nessuna API.

> Stesso schema del Task 6, ma queste due pagine hanno filtri propri (livello e classe per gli
> incantesimi, grado sfida per i mostri) che devono continuare a funzionare **sull'unione**, non solo
> sulle righe di database.

- [ ] **Step 1: Converti `Pages/Spells.razor`**

1. `@using DndCompanion.Models.Packages`, `@inject ICatalogService Catalog`.
2. `allSpells` → `dbRows` + `packageRows`; `ReloadAsync` che chiama `Catalog.GetSpellsAsync`.
   La lettura dei profili (`GetProfilesAsync`) resta dov'è.
3. Record `Entry`:

```csharp
    private sealed record Entry(
        string Key,
        string Name,
        int Level,
        string School,
        string CastingTime,
        string Range,
        string Components,
        string Duration,
        string Description,
        string Classes,
        string? AddedBy,
        bool IsPackage,
        bool FromAppPackage,
        Spell? Db,
        PackageSpell? Package);

    private static Entry ToEntry(Spell s) => new(
        "db:" + s.Id, s.Name, s.Level, s.School, s.CastingTime, s.Range, s.Components,
        s.Duration, s.Description, s.Classes, s.AddedBy, false,
        CatalogKey.IsFromAppPackage(s.SourceId), s, null);

    // Nessun autore: una voce di pacchetto non l'ha aggiunta nessuno, e mostrare un badge "✎ …"
    // vuoto sarebbe peggio che non mostrarlo.
    // `?? 0` sul livello: la proprietà del pacchetto è int? (Task 2), quella del record è int.
    private static Entry ToEntry(PackageSpell p) => new(
        "pkg:" + p.Id, p.Name, p.Level ?? 0, p.School, p.CastingTime, p.Range, p.Components,
        p.Duration, p.Description, string.Join(", ", p.Classes), null, true, true, null, p);
```

4. `FilteredSpells` diventa `FilteredEntries` e applica i filtri sull'unione:

```csharp
    private IEnumerable<Entry> FilteredEntries
    {
        get
        {
            IEnumerable<Entry> result = dbRows.Select(ToEntry).Concat(packageRows.Select(ToEntry));

            if (!string.IsNullOrEmpty(searchQuery))
            {
                // Normalizzato su entrambi i lati: cercare "invisibilita" deve trovare "Invisibilità".
                var chiave = CatalogKey.NormalizeName(searchQuery);
                result = result.Where(e =>
                    CatalogKey.NormalizeName(e.Name).Contains(chiave, StringComparison.Ordinal));
            }

            if (levelFilters.Count > 0)
                result = result.Where(e => levelFilters.Contains(e.Level));

            if (!string.IsNullOrEmpty(classFilter))
                result = result.Where(e => SpellClassNames.Matches(e.Classes, classFilter));

            return result
                .OrderBy(e => e.Level)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase);
        }
    }
```

5. `DuplicaDaPacchetto`:

```csharp
    private Spell DuplicaDaPacchetto(PackageSpell p) => new()
    {
        Name = p.Name,
        Level = p.Level ?? 0,
        School = p.School,
        CastingTime = p.CastingTime,
        Range = p.Range,
        Components = p.Components,
        Duration = p.Duration,
        Description = p.Description,
        Classes = string.Join(", ", p.Classes),
        SourceId = null,
        CampaignId = CurrentUser.CampaignId ?? string.Empty,
        AddedBy = CurrentUser.UserId,
    };
```

6. Tre rami di comandi, badge "Dal manuale", classe condizionale `package-card` sul div della card,
   CSS dai token con il selettore adattato a **`.spell-card.package-card`** — come nel Task 6.
7. L'empty state va aggiornato: la condizione `allSpells.Count == 0` diventa
   `dbRows.Count == 0 && packageRows.Count == 0`, altrimenti con un pacchetto caricato e nessuna riga
   propria l'utente leggerebbe "Nessun incantesimo nel database" davanti a un elenco pieno.

- [ ] **Step 2: Converti `Pages/Monsters.razor` allo stesso modo**

```csharp
    private sealed record Entry(
        string Key,
        string Name,
        string ChallengeRating,
        int ArmorClass,
        string HitPoints,
        string Description,
        string? AddedBy,
        bool IsPackage,
        bool FromAppPackage,
        Monster? Db,
        PackageMonster? Package);

    private static Entry ToEntry(Monster m) => new(
        "db:" + m.Id, m.Name, m.ChallengeRating, m.ArmorClass, m.HitPoints, m.Description,
        m.AddedBy, false, CatalogKey.IsFromAppPackage(m.SourceId), m, null);

    // `?? 10` sulla CA: la proprietà del pacchetto è int? (Task 2), quella del record è int, e 10
    // è il default del Model per un mostro di cui non si conosce l'armatura.
    private static Entry ToEntry(PackageMonster p) => new(
        "pkg:" + p.Id, p.Name, p.ChallengeRating, p.ArmorClass ?? 10, p.HitPoints, p.Description,
        null, true, true, null, p);
```

`DuplicaDaPacchetto` per i mostri copia nome, grado sfida, CA (`p.ArmorClass ?? 10`, la proprietà è
`int?` dal Task 2), PF e descrizione; le sei caratteristiche del Model restano ai loro default (10),
perché `PackageMonster` non le porta.

Valgono anche qui la classe condizionale sulla card, il selettore **`.monster-card.package-card`** e
l'adeguamento dell'empty state.

Il filtro per grado sfida continua a usare `MonsterCatalog.ParseChallengeRating(entry.ChallengeRating)`:
è già un helper puro e non va toccato.

- [ ] **Step 3: Avverti prima di cancellare un incantesimo dal catalogo**

Lo spec lo chiede esplicitamente in §4.4: «Vale comunque il `ConfirmDialog` di rito, che è il posto
giusto per avvertire che l'incantesimo sparirà dalle schede che lo conoscono». Oggi
`Pages/Spells.razor` chiede solo «Eliminare l'incantesimo "…"?», e con l'import questa fase crea per
la prima volta cataloghi condivisi in cui una cancellazione tocca le schede **di altri**:
`character_spells_spell_id_fkey` è `ON DELETE CASCADE`.

In `DeleteSpellAsync`, **sostituisci** le due righe della conferma esistente
(`Pages/Spells.razor:431-432`, `var confirmed = await Confirm.ShowAsync(…); if (!confirmed) return;`)
con questo blocco. Sostituire, non aggiungere: lasciandole si aprirebbero due dialoghi in fila.

```csharp
        // Quanti PG lo conoscono: la FK è ON DELETE CASCADE, quindi cancellarlo dal catalogo lo
        // toglie dalle loro schede — senza preavviso, se il dialogo non lo dice (§4.4).
        //
        // Il try/catch non è pleonastico: questa è l'unica chiamata di rete FUORI dal try/catch che
        // avvolge la cancellazione, e un errore qui uscirebbe dall'event handler mostrando l'error
        // UI di Blazor — l'app da ricaricare — invece del DbErrorBanner. Se il conteggio non
        // riesce, si avverte comunque e si lascia procedere: negare la cancellazione per un
        // conteggio mancato sarebbe peggio del conteggio mancato.
        var pgColpiti = 0;
        try
        {
            var legami = await CharacterSpellRepository.GetBySpellIdsAsync(new List<string> { spell.Id });
            pgColpiti = legami.Select(l => l.CharacterId).Distinct().Count();
        }
        catch (Exception)
        {
            pgColpiti = -1; // sconosciuto
        }

        var messaggio = $"Eliminare l'incantesimo \"{spell.Name}\"?";
        messaggio += pgColpiti switch
        {
            < 0 => " Sparirà anche dalle schede dei personaggi che lo conoscono.",
            0 => string.Empty,
            1 => " Sparirà anche dalla scheda di 1 personaggio che lo conosce.",
            _ => $" Sparirà anche dalle schede di {pgColpiti} personaggi che lo conoscono.",
        };

        if (!await Confirm.ShowAsync(messaggio)) return;
```

Aggiungi `@inject ICharacterSpellRepository CharacterSpellRepository` in testa alla pagina.

- [ ] **Step 4: Verifica il tracker del combattimento**

`Combat.razor` importa i mostri con `CombatImport.FromMonster`, che li copia **per valore**: nessuna
chiave esterna, quindi nessuna materializzazione (§4.4). Ma il pannello di import legge da
`IMonsterRepository`, quindi vede solo le righe di database.

Per questo task va bene così — il pannello continua a funzionare come oggi. **Annota nel resoconto
finale** che i mostri di pacchetto non compaiono nell'import del combattimento finché non vengono
duplicati in campagna: è una conseguenza nota, non un difetto da correggere qui.

- [ ] **Step 5: Verifica a mano**

Comando: `dotnet run`

Verifica su `/spells` e `/monsters`: ricerca, filtri (livello, classe, grado sfida), creazione,
modifica ed eliminazione. Il filtro per classe ora mostra i nomi **italiani** (Task 1) e deve trovare
gli incantesimi il cui campo è in inglese.

Verifica anche l'avviso dello Step 3: aggiungi un incantesimo alla scheda di un PG, poi prova a
eliminarlo dal catalogo — il dialogo deve dire che sparirà anche da quella scheda. Annulla.

- [ ] **Step 6: Verifica build e suite**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori.

Comando: `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: tutti verdi.

- [ ] **Step 7: Commit**

```bash
git add Pages/Spells.razor Pages/Spells.razor.css Pages/Monsters.razor Pages/Monsters.razor.css
git commit -m "feat(cataloghi): voci di pacchetto marcate e duplicabili in Incantesimi e Mostri"
```

---

### Task 8: Materializzazione degli incantesimi nella scheda del personaggio

**File:**
- Modifica: `Shared/SpellPicker.razor`
- Modifica: `Shared/CharacterTabs/CharacterMagicTab.razor`
- Modifica: `Pages/Characters.razor` (caricamento ~275, passaggio del parametro ~150)

**Interfacce:**
- Consuma: `SpellMaterialization.Resolve` (Task 3), `ISpellRepository.CreateSpellAsync` e
  `GetOneBySourceAsync` (Task 4), `ICatalogService.GetSpellsAsync` (Task 5).
- Produce: parametro `PackageSpells` su `CharacterMagicTab` e su `SpellPicker`.

> È il punto in cui la chiave esterna di §4.1 morde: un incantesimo che vive solo nel file **non può**
> essere aggiunto alla lista di un PG. Il picker deve però mostrarlo, altrimenti il pacchetto è
> invisibile proprio dove serve.

- [ ] **Step 1: Fai accettare al picker anche le voci di pacchetto**

In `Shared/SpellPicker.razor`, sostituisci il blocco `@code` e la lista con una vista comune alle due
sorgenti. Il tipo `Choice` distingue chi ha già un uuid da chi va materializzato:

```csharp
@code {
    /// <summary>Una voce selezionabile: o una riga di catalogo (ha già un id), o una voce di
    /// pacchetto (da materializzare al momento della scelta, §4.4).</summary>
    public sealed record Choice(string Key, string Name, int Level, string School,
                                Spell? Row, PackageSpell? Package);

    [Parameter] public List<Spell> AllSpells { get; set; } = new();
    [Parameter] public List<PackageSpell> PackageSpells { get; set; } = new();
    [Parameter] public HashSet<string> AlreadyAddedIds { get; set; } = new();
    [Parameter] public EventCallback<Choice> OnSpellSelected { get; set; }

    private string searchTerm = "";
    private bool isOpen = false;
    private const int MaxResults = 8;

    private List<Choice> FilteredSpells
    {
        get
        {
            if (string.IsNullOrWhiteSpace(searchTerm)) return new();
            var term = CatalogKey.NormalizeName(searchTerm);

            var righe = AllSpells
                .Where(s => !AlreadyAddedIds.Contains(s.Id))
                .Select(s => new Choice("db:" + s.Id, s.Name, s.Level, s.School, s, null));

            // Le voci già coperte da una riga sono state tolte a monte da CatalogService, e quella
            // materializzata durante la sessione la toglie CharacterMagicTab: qui non si deduplica.
            var voci = PackageSpells
                .Select(p => new Choice("pkg:" + p.Id, p.Name, p.Level ?? 0, p.School, null, p));

            return righe.Concat(voci)
                .Where(c => CatalogKey.NormalizeName(c.Name).Contains(term, StringComparison.Ordinal))
                .OrderBy(c => CatalogKey.NormalizeName(c.Name).StartsWith(term, StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(c => c.Level)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxResults)
                .ToList();
        }
    }

    private async Task HandleSelect(Choice choice)
    {
        await OnSpellSelected.InvokeAsync(choice);
        searchTerm = "";
        isOpen = false;
    }

    private async Task HandleBlur()
    {
        // Piccola attesa: lascia registrare il mousedown sull'opzione prima della chiusura.
        await Task.Delay(150);
        isOpen = false;
    }
}
```

Nel markup, il `@foreach` itera su `Choice` e usa `choice.Key` come chiave, `choice.Name`,
`choice.Level`, `choice.School`. Aggiungi anche `@using DndCompanion.Models.Packages` in testa.

- [ ] **Step 2: Materializza al momento della scelta**

In `Shared/CharacterTabs/CharacterMagicTab.razor`:

1. `@inject ISpellRepository SpellRepository` e `@using DndCompanion.Models.Packages`.
2. Aggiungi il parametro e passalo al picker:

```csharp
    [Parameter] public List<PackageSpell> PackageSpells { get; set; } = new();
```

```razor
        <SpellPicker AllSpells="@AllSpells"
                     PackageSpells="@PackageSpells"
                     AlreadyAddedIds="@selectedCharacterSpells.Select(cs => cs.SpellId).ToHashSet()"
                     OnSpellSelected="@AddSpellToCharacter" />
```

3. Sostituisci `AddSpellToCharacter`:

```csharp
    // La decisione "riusa una riga o creane una da voce di pacchetto" è di SpellMaterialization;
    // qui si eseguono le due scritture, nell'ordine: prima la riga di catalogo (serve il suo uuid),
    // poi il legame con il personaggio.
    private async Task AddSpellToCharacter(SpellPicker.Choice choice)
    {
        if (!CanEdit) return;

        try
        {
            var spell = choice.Row;

            if (spell is null && choice.Package is not null)
            {
                var esito = SpellMaterialization.Resolve(
                    choice.Package, AllSpells, Character.CampaignId, CurrentUser.UserId);

                spell = esito.Existing;
                if (spell is null && esito.ToInsert is not null)
                    spell = await MaterializzaAsync(esito.ToInsert);

                if (spell is not null)
                {
                    // La riga entra nel catalogo in memoria e la voce di pacchetto esce dal picker:
                    // senza la prima, il prossimo incantesimo scelto dallo stesso pacchetto
                    // proverebbe a inserirla di nuovo; senza la seconda, "Palla di Fuoco"
                    // comparirebbe due volte nell'elenco, come riga e come voce.
                    if (AllSpells.All(s => s.Id != spell.Id)) AllSpells.Add(spell);
                    PackageSpells.RemoveAll(p => p.Id == choice.Package.Id);
                }
            }

            if (spell is null)
            {
                await OnError.InvokeAsync("Impossibile aggiungere l'incantesimo al catalogo della campagna.");
                return;
            }

            // Il PG ce l'ha già: può succedere se la voce di pacchetto scelta corrisponde a una
            // riga che il personaggio conosce sotto un altro nome. Meglio non chiamare il server
            // per farsi rifiutare da character_spells_character_id_spell_id_key.
            if (selectedCharacterSpells.Any(cs => cs.SpellId == spell.Id))
            {
                RebuildSpellDisplay();
                return;
            }

            var entry = new CharacterSpell
            {
                CharacterId = Character.Id,
                SpellId = spell.Id,
                IsPrepared = spell.Level == 0 // i trucchetti sono auto-preparati
            };

            var saved = await CharacterSpellRepository.AddSpellToCharacterAsync(entry);
            if (saved is not null)
            {
                selectedCharacterSpells.Add(saved);
                RebuildSpellDisplay();
            }
            else
            {
                await OnError.InvokeAsync("Impossibile aggiungere l'incantesimo.");
            }
        }
        catch (Exception ex)
        {
            await OnError.InvokeAsync($"Errore aggiunta incantesimo: {ex.Message}");
        }
    }

    // Leggi-poi-inserisci, con rilettura sul conflitto. Il vincolo UNIQUE (campaign_id, source_id)
    // è l'arbitro: se fra il controllo e l'inserimento un altro giocatore ha materializzato la
    // stessa voce — il caso di §4.4, due schede preparate la stessa sera — l'INSERT fallisce e si
    // riusa la riga sua, senza mai sovrascriverla.
    private async Task<Spell?> MaterializzaAsync(Spell daInserire)
    {
        var giaPresente = await SpellRepository.GetOneBySourceAsync(
            daInserire.CampaignId, daInserire.SourceId!);
        if (giaPresente is not null) return giaPresente;

        try
        {
            return await SpellRepository.CreateSpellAsync(daInserire);
        }
        catch (Postgrest.Exceptions.PostgrestException)
        {
            // Conflitto sul vincolo di unicità: la riga esiste ora, creata da qualcun altro.
            return await SpellRepository.GetOneBySourceAsync(
                daInserire.CampaignId, daInserire.SourceId!);
        }
    }
```

4. Aggiungi `@inject CurrentUserService CurrentUser` al componente: serve l'`added_by` della riga
   materializzata, che `spells_insert` impone uguale a `auth.uid()`.

5. Correggi anche il suggerimento dell'elenco vuoto (riga ~82), che oggi guarda solo `AllSpells`:

```razor
            @if (CanEdit && AllSpells.Count == 0 && PackageSpells.Count == 0)
```

Senza, con un pacchetto caricato l'utente leggerebbe «il catalogo globale è vuoto» mentre il picker
trova decine di voci.

- [ ] **Step 3: Alimenta il tab dalla vista unita**

In `Pages/Characters.razor`:

1. `@inject ICatalogService Catalog` e `@using DndCompanion.Models.Packages`.
2. Aggiungi il campo `private List<PackageSpell> packageSpells = new();`.
3. Sostituisci la riga ~275:

```csharp
            var vistaIncantesimi = await Catalog.GetSpellsAsync(CurrentUser.CampaignId ?? string.Empty);
            allSpells = vistaIncantesimi.DbRows.ToList();
            packageSpells = vistaIncantesimi.PackageEntries.ToList();
```

> `.ToList()` non è cosmetico: `CharacterMagicTab` **aggiunge** a `AllSpells` la riga appena
> materializzata, e `CatalogView.DbRows` è un `IReadOnlyList` che potrebbe essere la stessa lista
> restituita dal repository.

4. Passa il parametro nuovo al tab (riga ~150):

```razor
                                       AllSpells="@allSpells" PackageSpells="@packageSpells" OnError="@SetError" />
```

- [ ] **Step 4: Verifica a mano**

Comando: `dotnet run`

Senza pacchetto il comportamento deve essere **identico a prima**: apri la scheda di un PG
incantatore, cerca un incantesimo nel picker, aggiungilo, toglilo. Nessun errore, nessuna riga nuova
nel catalogo.

> Il percorso di materializzazione vero si prova col Task 10, importando un file che contenga
> incantesimi. Verifica allora che scegliere una voce di pacchetto crei **una sola** riga in
> `/spells`, marcata "Dal manuale" e senza matita né cestino.

- [ ] **Step 5: Verifica build e suite**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori.

Comando: `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: tutti verdi.

- [ ] **Step 6: Commit**

```bash
git add Shared/SpellPicker.razor Shared/CharacterTabs/CharacterMagicTab.razor Pages/Characters.razor
git commit -m "feat(incantesimi): materializzazione su uso delle voci di pacchetto"
```

---

### Task 9: Export della campagna

**File:**
- Crea: `Services/CampaignExport.cs`
- Modifica: `Services/CatalogPackageParser.cs` (opzioni di serializzazione)
- Modifica: `wwwroot/index.html` (helper di download)
- Test: `Tests/CampaignExportTests.cs`

**Interfacce:**
- Consuma: `CampaignCatalogs` (Task 2), `CatalogKey.NormalizeName`, `CatalogPackageJsonContext`.
- Produce: `CampaignExport.PackageIdFor(string campaignName)`,
  `CampaignExport.Build(CampaignCatalogs, string campaignName)` → `CatalogPackage`,
  `CampaignExport.ToJson(CatalogPackage)` → `string`; e `window.downloadTextFile(nome, contenuto)`.

- [ ] **Step 1: Scrivi i test (falliranno)**

`Tests/CampaignExportTests.cs`:

```csharp
using DndCompanion.Models;
using DndCompanion.Services;

namespace DndCompanion.Tests;

public class CampaignExportTests
{
    private static CampaignCatalogs Cataloghi() => new()
    {
        Races = { new Race { Id = "uuid-1", Name = "Elfo Silvano", Speed = 9, SpeedUnit = "m", CampaignId = "c1" } },
        Spells = {
            new Spell { Id = "uuid-2", Name = "Palla di Fuoco", Level = 3,
                        Classes = "Mago, Stregone", CampaignId = "c1" },
            new Spell { Id = "uuid-3", Name = "Invisibilità", Level = 2,
                        SourceId = "srd-2024-it/invisibilita", CampaignId = "c1" },
        },
    };

    // §6: dare al proprio file l'id del pacchetto dell'app renderebbe le proprie voci di sola
    // lettura al reimport. L'export non deve mai produrlo.
    [Fact]
    public void PackageIdFor_NonProduceMaiLIdDelPacchettoDellApp()
    {
        var id = CampaignExport.PackageIdFor("SRD 2024 IT");

        Assert.NotEqual(CatalogPackageParser.AppPackageId, id);
        Assert.False(CatalogKey.IsFromAppPackage(id + "/qualcosa"));
    }

    [Fact]
    public void PackageIdFor_NormalizzaNomeEAccenti()
        => Assert.Equal("campagna-la-citta-perduta", CampaignExport.PackageIdFor("La Città Perduta"));

    [Fact]
    public void PackageIdFor_NomeVuoto_ProduceUnIdUsabile()
        => Assert.Equal("campagna-senza-nome", CampaignExport.PackageIdFor("   "));

    [Fact]
    public void Build_RigaSenzaProvenienza_RiceveUnIdDerivatoDalNome()
    {
        var pacchetto = CampaignExport.Build(Cataloghi(), "La Città Perduta");

        var elfo = Assert.Single(pacchetto.Species);
        Assert.Equal("campagna-la-citta-perduta/elfo-silvano", elfo.Id);
        Assert.Equal("Elfo Silvano", elfo.Name);
        Assert.Equal(9, elfo.Speed!.Value);
        Assert.Equal("m", elfo.Speed.Unit);
    }

    // Conservare il source_id è ciò che permette a un reimport di AGGIORNARE la voce invece di
    // duplicarla: senza, ogni giro di export/import creerebbe una riga in più.
    [Fact]
    public void Build_RigaConProvenienzaDaUnFileUtente_ConservaLIdOriginale()
    {
        var cataloghi = new CampaignCatalogs
        {
            Spells = { new Spell { Id = "u1", Name = "Dardo", SourceId = "altro-tavolo/dardo", CampaignId = "c1" } },
        };

        var pacchetto = CampaignExport.Build(cataloghi, "La Città Perduta");

        Assert.Contains(pacchetto.Spells, s => s.Id == "altro-tavolo/dardo");
    }

    // Ma NON la provenienza del pacchetto dell'app: propagarla renderebbe quelle voci intoccabili
    // in una campagna terza che non ha mai importato nulla di ufficiale (§6, §8).
    [Fact]
    public void Build_RigaMaterializzataDalManuale_PerdeLaProvenienzaDellApp()
    {
        var pacchetto = CampaignExport.Build(Cataloghi(), "La Città Perduta");

        Assert.DoesNotContain(pacchetto.Spells, s => CatalogKey.IsFromAppPackage(s.Id));
        Assert.Contains(pacchetto.Spells, s => s.Id == "campagna-la-citta-perduta/invisibilita");
    }

    // Nessuna tabella impedisce due righe omonime, e il parser rifiuta l'INTERO pacchetto se un
    // identificatore compare due volte: senza suffisso il file esportato sarebbe illeggibile.
    [Fact]
    public void Build_RigheOmonime_RicevonoIdentificatoriDistinti()
    {
        var cataloghi = new CampaignCatalogs
        {
            Spells =
            {
                new Spell { Id = "u1", Name = "Palla di Fuoco", CampaignId = "c1" },
                new Spell { Id = "u2", Name = "palla di fuoco", CampaignId = "c1" },
                new Spell { Id = "u3", Name = "PALLA DI FUOCO", CampaignId = "c1" },
            },
        };

        var ids = CampaignExport.Build(cataloghi, "Tavolo").Spells.Select(s => s.Id).ToList();

        Assert.Equal(3, ids.Distinct(StringComparer.Ordinal).Count());
    }

    // Il caso che un solo passaggio non copre: una riga SENZA provenienza processata per prima si
    // prenderebbe lo slug che una riga successiva porta già come source_id. Succede davvero — basta
    // un "duplica e modifica" di una voce importata da un file omonimo alla campagna.
    [Fact]
    public void Build_SlugGeneratoCheCollideConUnaProvenienzaConservata_RestaUnico()
    {
        var cataloghi = new CampaignCatalogs
        {
            Spells =
            {
                new Spell { Id = "u1", Name = "Dardo", SourceId = null, CampaignId = "c1" },
                new Spell { Id = "u2", Name = "Dardo", SourceId = "campagna-tavolo/dardo", CampaignId = "c1" },
            },
        };

        var ids = CampaignExport.Build(cataloghi, "Tavolo").Spells.Select(s => s.Id).ToList();

        Assert.Equal(2, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("campagna-tavolo/dardo", ids);
    }

    [Fact]
    public void Build_NomeSenzaLettereNeCifre_ProduceComunqueUnIdValido()
    {
        var cataloghi = new CampaignCatalogs
        {
            Monsters = { new Monster { Id = "u1", Name = "???", CampaignId = "c1" } },
        };

        var mostro = Assert.Single(CampaignExport.Build(cataloghi, "Tavolo").Monsters);
        Assert.False(mostro.Id.EndsWith("/", StringComparison.Ordinal));
    }

    // Una riga senza nome non è esportabile: il parser esige nome e identificatore, e uno slug
    // vuoto non produce né l'uno né l'altro.
    [Fact]
    public void Build_RigaSenzaNome_VieneScartata()
    {
        var cataloghi = new CampaignCatalogs
        {
            Races = { new Race { Id = "u1", Name = "   ", CampaignId = "c1" } },
        };

        Assert.Empty(CampaignExport.Build(cataloghi, "Tavolo").Species);
    }

    [Fact]
    public void Build_ClassiDellIncantesimo_TornanoUnaLista()
    {
        var pacchetto = CampaignExport.Build(Cataloghi(), "La Città Perduta");

        var palla = Assert.Single(pacchetto.Spells, s => s.Name == "Palla di Fuoco");
        Assert.Equal(new[] { "Mago", "Stregone" }, palla.Classes);
    }

    // §5: l'export non produce mai talenti, perché nel database non ce ne sono.
    [Fact]
    public void Build_NonProduceMaiTalenti()
        => Assert.Empty(CampaignExport.Build(Cataloghi(), "La Città Perduta").Feats);

    [Fact]
    public void Build_DichiaraLaVersioneDiSchemaSupportata()
        => Assert.Equal(CatalogPackageParser.SupportedSchemaVersion,
                        CampaignExport.Build(Cataloghi(), "c").SchemaVersion);

    // Il giro completo: ciò che l'export produce, il parser deve saperlo rileggere. Senza questo
    // test un export malformato si scoprirebbe solo al reimport, su un file già distribuito.
    [Fact]
    public void ToJson_IlRisultatoERileggibileDalParser()
    {
        var json = CampaignExport.ToJson(CampaignExport.Build(Cataloghi(), "La Città Perduta"));

        var riletto = CatalogPackageParser.Parse(json);

        Assert.Empty(riletto.Errors);
        Assert.NotNull(riletto.Package);
        Assert.Single(riletto.Package!.Species);
        Assert.Equal(2, riletto.Package.Spells.Count);
    }
}
```

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CampaignExportTests`
Atteso: FALLIMENTO di compilazione — `CampaignExport` non esiste.

- [ ] **Step 3: Abilita la serializzazione nel contesto JSON**

In `Services/CatalogPackageParser.cs`, il contesto oggi serve solo a leggere. Per scrivere serve che
produca un JSON indentato e senza le proprietà nulle. Sostituisci l'attributo:

```csharp
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CatalogPackage))]
internal partial class CatalogPackageJsonContext : JsonSerializerContext { }
```

Cambia anche la visibilità da `internal` a `public`? **No.** `CampaignExport` sta nello stesso
assembly, quindi `internal` basta. Il progetto di test lo raggiunge via `InternalsVisibleTo`, che è
già configurato per `FormValidation`.

- [ ] **Step 4: Implementa l'export**

`Services/CampaignExport.cs`:

```csharp
using System.Text;
using System.Text.Json;
using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Trasforma i cataloghi di una campagna nel formato di scambio (§5 dello spec), così che
/// un gruppo possa portarsi via i propri dati o passarli a un altro tavolo. Logica pura.</summary>
public static class CampaignExport
{
    /// <summary>Prefisso degli id prodotti dall'export. Deliberatamente diverso da
    /// CatalogPackageParser.AppPackageId: un file con quell'id renderebbe di sola lettura le
    /// proprie voci al reimport (§6).</summary>
    private const string Prefix = "campagna-";

    public static string PackageIdFor(string campaignName)
    {
        var slug = Slug(campaignName);
        return Prefix + (string.IsNullOrEmpty(slug) ? "senza-nome" : slug);
    }

    // Nome normalizzato con gli spazi in trattini: la chiave di CatalogKey piega già accenti e
    // maiuscole senza toccare le API di globalizzazione, che qui non funzionerebbero.
    private static string Slug(string? name)
    {
        var normalizzato = CatalogKey.NormalizeName(name);
        var sb = new StringBuilder(normalizzato.Length);
        var trattinoPendente = false;

        foreach (var c in normalizzato)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (trattinoPendente && sb.Length > 0) sb.Append('-');
                trattinoPendente = false;
                sb.Append(c);
            }
            else
            {
                trattinoPendente = true;
            }
        }
        return sb.ToString();
    }

    /// <summary>Assegna gli identificatori di una sezione, garantendone l'unicità.
    ///
    /// Tre regole, in quest'ordine:
    /// 1. una provenienza dal PACCHETTO DELL'APP non si conserva — degraderebbe una campagna terza a
    ///    contenitore di voci che nessuno può modificare né rimuovere (§6, §8);
    /// 2. ogni altra provenienza si conserva: è ciò che permette a un reimport di aggiornare invece
    ///    di duplicare;
    /// 3. gli slug derivati dal nome che collidono ricevono un suffisso progressivo. Non è un caso
    ///    di scuola: nessuna tabella impedisce due righe omonime, e il parser rifiuta l'INTERO
    ///    pacchetto se un identificatore compare due volte.
    /// </summary>
    private static List<string> AssignIds<TRow>(
        string packageId, IReadOnlyList<TRow> rows, Func<TRow, string?> sourceIdOf, Func<TRow, string> nameOf)
    {
        static bool Conservabile(string? sourceId)
            => !string.IsNullOrWhiteSpace(sourceId) && !CatalogKey.IsFromAppPackage(sourceId);

        // DUE passaggi, non uno. Le provenienze conservate vanno prenotate TUTTE prima di generare
        // il primo slug: una riga senza provenienza processata per prima si prenderebbe
        // "<pacchetto>/dardo" e una riga successiva con quel source_id lo riproporrebbe identico —
        // due id uguali, e il parser rifiuta l'intero pacchetto, non la voce.
        var usati = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var sourceId = sourceIdOf(row);
            if (Conservabile(sourceId)) usati.Add(sourceId!.Trim());
        }

        var risultato = new List<string>(rows.Count);
        foreach (var row in rows)
        {
            var sourceId = sourceIdOf(row);
            if (Conservabile(sourceId))
            {
                risultato.Add(sourceId!.Trim());
                continue;
            }

            // Slug vuoto (nome fatto di soli segni di punteggiatura) → "voce", che il suffisso
            // rende comunque unica: meglio un id brutto di un pacchetto irrecuperabile.
            var baseSlug = Slug(nameOf(row));
            if (baseSlug.Length == 0) baseSlug = "voce";

            var candidato = $"{packageId}/{baseSlug}";
            var n = 2;
            while (!usati.Add(candidato))
                candidato = $"{packageId}/{baseSlug}-{n++}";

            risultato.Add(candidato);
        }
        return risultato;
    }

    public static CatalogPackage Build(CampaignCatalogs catalogs, string campaignName)
    {
        var id = PackageIdFor(campaignName);

        // Le righe senza nome non sono esportabili: il parser esige nome E identificatore, e uno
        // slug vuoto non produce né l'uno né l'altro. Si scartano qui, non si aggirano nel test.
        var razze = catalogs.Races.Where(r => !string.IsNullOrWhiteSpace(r.Name)).ToList();
        var classi = catalogs.Classes.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();
        var background = catalogs.Backgrounds.Where(b => !string.IsNullOrWhiteSpace(b.Name)).ToList();
        var incantesimi = catalogs.Spells.Where(s => !string.IsNullOrWhiteSpace(s.Name)).ToList();
        var mostri = catalogs.Monsters.Where(m => !string.IsNullOrWhiteSpace(m.Name)).ToList();

        var idRazze = AssignIds(id, razze, r => r.SourceId, r => r.Name);
        var idClassi = AssignIds(id, classi, c => c.SourceId, c => c.Name);
        var idBackground = AssignIds(id, background, b => b.SourceId, b => b.Name);
        var idIncantesimi = AssignIds(id, incantesimi, s => s.SourceId, s => s.Name);
        var idMostri = AssignIds(id, mostri, m => m.SourceId, m => m.Name);

        return new CatalogPackage
        {
            SchemaVersion = CatalogPackageParser.SupportedSchemaVersion,
            Id = id,
            Name = string.IsNullOrWhiteSpace(campaignName) ? "Campagna" : campaignName.Trim(),
            Edition = "2024",
            Language = "it",
            Version = "1.0.0",

            Species = razze.Select((r, i) => new PackageSpecies
            {
                Id = idRazze[i],
                Name = r.Name,
                Description = r.Description,
                Speed = new PackageSpeed { Value = r.Speed, Unit = r.SpeedUnit },
                Traits = r.Traits,
            }).ToList(),

            Backgrounds = background.Select((b, i) => new PackageBackground
            {
                Id = idBackground[i],
                Name = b.Name,
                Description = b.Description,
                AbilityScores = SplitList(b.AbilityScores),
                OriginFeat = b.OriginFeat,
                SkillProficiencies = SplitList(b.SkillProficiencies),
                ToolProficiency = b.ToolProficiency,
                Equipment = b.Equipment,
            }).ToList(),

            Classes = classi.Select((c, i) => new PackageClass
            {
                Id = idClassi[i],
                Name = c.Name,
                HitDie = c.HitDie,
                PrimaryAbility = c.PrimaryAbility,
                SavingThrows = SplitList(c.SavingThrows),
            }).ToList(),

            Spells = incantesimi.Select((s, i) => new PackageSpell
            {
                Id = idIncantesimi[i],
                Name = s.Name,
                Level = s.Level,
                School = s.School,
                CastingTime = s.CastingTime,
                Range = s.Range,
                Components = s.Components,
                Duration = s.Duration,
                Description = s.Description,
                Classes = SplitList(s.Classes),
            }).ToList(),

            Monsters = mostri.Select((m, i) => new PackageMonster
            {
                Id = idMostri[i],
                Name = m.Name,
                ChallengeRating = m.ChallengeRating,
                ArmorClass = m.ArmorClass,
                HitPoints = m.HitPoints,
                Description = m.Description,
            }).ToList(),

            // Mai talenti: non hanno tabella, quindi nel database non ce ne sono (§5).
            Feats = new List<PackageFeat>(),
        };
    }

    // Le colonne sono testo libero, le sezioni del formato sono liste: si spezza sugli stessi
    // separatori che i form accettano, scartando i vuoti.
    private static List<string> SplitList(string? field)
        => string.IsNullOrWhiteSpace(field)
            ? new List<string>()
            : field.Split(',', ';', '/')
                   .Select(t => t.Trim())
                   .Where(t => t.Length > 0)
                   .ToList();

    /// <summary>Il file da scaricare. Serializzazione col source generator: il progetto pubblica
    /// con TrimMode=full, dove gli overload a reflection producono warning.</summary>
    public static string ToJson(CatalogPackage package)
        => JsonSerializer.Serialize(package, CatalogPackageJsonContext.Default.CatalogPackage);
}
```

- [ ] **Step 5: Esegui i test e verifica che passino**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CampaignExportTests`
Atteso: 14 test PASSATI.

- [ ] **Step 6: Aggiungi l'helper di download**

In `wwwroot/index.html`, accanto a `window.repairApp` (riga ~176), aggiungi:

```javascript
        // Download di un file di testo generato dall'app (export dei cataloghi). Blob + anchor
        // invece di un data: URI perché i pacchetti possono superare di molto il limite pratico
        // degli URI, e l'URL viene revocato subito dopo per non trattenere memoria.
        window.downloadTextFile = function (fileName, content) {
            const blob = new Blob([content], { type: 'application/json' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        };
```

- [ ] **Step 7: Verifica che la CSP non blocchi il download**

La CSP di questa app è `default-src 'self'` **senza** `blob:` (riga 18 di `wwwroot/index.html`).
Un download via `<a download>` non è una navigazione e non dovrebbe essere governato da quella
direttiva, ma va **constatato**, non dedotto.

Comando: `dotnet run`, poi nella console del browser:
```javascript
window.downloadTextFile('prova.json', '{"ok":true}')
```
Atteso: il file viene scaricato e la console **non** riporta violazioni di Content Security Policy.

Se invece compare una violazione, non aggiungere `blob:` a `default-src`: aggiungilo alla sola
direttiva citata dal messaggio d'errore, e annota la modifica nel commit.

- [ ] **Step 8: Verifica build**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori. Se compare un warning di trimming su `JsonSerializer.Serialize`,
la chiamata sta usando l'overload generico invece di `CatalogPackageJsonContext.Default.CatalogPackage`.

- [ ] **Step 9: Commit**

```bash
git add Services/CampaignExport.cs Services/CatalogPackageParser.cs \
        Tests/CampaignExportTests.cs wwwroot/index.html
git commit -m "feat(export): serializzazione dei cataloghi di campagna e download del file"
```

---

### Task 10: Schermata di import ed export

**File:**
- Crea: `Pages/DataPackages.razor`
- Crea: `Pages/DataPackages.razor.css`
- Modifica: `Pages/Home.razor` (card di navigazione, array `cards` ~156-165)

**Interfacce:**
- Consuma: `CatalogPackageParser.Parse`, `PackageImportPlan.Build` (Task 2),
  `ICatalogService.GetCampaignCatalogsAsync` (Task 5), i cinque `CreateManyAsync` (Task 4) e i cinque
  `Update*Async` già esistenti, `CampaignExport` (Task 9), `CurrentUserService`,
  `ICampaignRepository`, `ToastService`, `ConfirmService`.
- Produce: rotta `/dati`.

> «L'anteprima che l'utente conferma è l'output di `PackageImportPlan`, non una stima scritta a
> parte: ciò che legge è ciò che accadrà» (§7). E: «l'import procede a **blocchi**, ciascuno atomico,
> e chiude con un resoconto invece di fingere un'atomicità che non abbiamo» (§9).

- [ ] **Step 1: Scrivi la pagina**

`Pages/DataPackages.razor` deve avere:

- `@page "/dati"`, `@using DndCompanion.Models`, `@using DndCompanion.Models.Packages`
  (**entrambe**: `_Imports.razor` non le contiene, ogni pagina le dichiara per sé),
  e le `@inject` di `ICatalogService`, `IRaceRepository`, `IClassRepository`, `ISpellRepository`,
  `IMonsterRepository`, `IBackgroundRepository`, `ICampaignRepository`, `CurrentUserService`,
  `ToastService`, `ConfirmService`, `NavigationManager`, `IJSRuntime`;
- l'intestazione con "← Home" e `<DbErrorBanner>`, come le altre pagine;
- **una guardia sulla campagna attiva**: se `CurrentUser.CampaignId` è vuoto, la pagina mostra
  l'empty state «Seleziona una campagna per importare o esportare i dati» e **non rende** le sezioni.
  Senza, scegliendo un file si arriverebbe a `GetCampaignCatalogsAsync(null!)` e a cinque query
  `campaign_id=eq.` malformate invece di un messaggio comprensibile;
- tre sezioni: **Importa**, **Esporta**, **Rimuovi un import** (l'ultima è del Task 11: in questo
  task lasciala fuori);
- `<InputFile>` per la scelta del file (`Microsoft.AspNetCore.Components.Forms` è già in
  `_Imports.razor`), con `accept=".json"` e `aria-label="Scegli un file di pacchetto"`;
- `<PageTitle>Dati - D&amp;D Companion</PageTitle>`, come tutte le pagine esistenti;
- l'anteprima come tabella per sezione, con i conteggi e l'elenco delle voci saltate **con il
  motivo**;
- **il rendering di `warnings`** — gli avvisi del parser, per esempio un pacchetto in lingua diversa
  dall'italiano (§5) — in un blocco distinto dagli errori, perché non impediscono l'import;
- **il rendering di `importReport`** dopo l'esecuzione: è il «resoconto di cosa è passato e cosa no»
  che §9 richiede per l'import interrotto a metà, ed è l'analogo di `removalReport` del Task 11;
- `<LoadingSpinner>` durante lettura ed esecuzione, `Toasts.ShowError` per gli errori di
  validazione del file, `DbErrorBanner` per i soli errori di sistema.

> I due penultimi punti non sono rifiniture: `warnings` e `importReport` sono campi assegnati dal
> blocco `@code`, e se nessuno li legge il compilatore emette **CS0414** — due warning, contro il
> gate «0 warning» di questa fase. Oltre, ovviamente, a perdere l'informazione.

- [ ] **Step 2: Usa questo blocco `@code`**

```csharp
@code {
    private bool loading = true;
    private bool isBusy;
    private string? systemError;

    private string campaignName = string.Empty;

    // Il file scelto, letto e validato: pacchetto, anteprima e avvisi restano insieme finché
    // l'utente non conferma o cambia file.
    private CatalogPackage? loadedPackage;
    private ImportPlanResult? plan;
    private IReadOnlyList<string> warnings = Array.Empty<string>();
    private string? importReport;

    // Le righe della campagna come stanno nel database: servono all'anteprima e poi, riga per riga,
    // all'esecuzione degli aggiornamenti, che partono dall'esistente invece di sostituirlo.
    private CampaignCatalogs existing = new();

    /// <summary>Limite di dimensione del file. Il pacchetto SRD completo starà ben sotto; oltre,
    /// è quasi certamente il file sbagliato, e leggerlo in memoria in WebAssembly costa.</summary>
    private const long MaxFileSize = 8 * 1024 * 1024;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await CurrentUser.EnsureLoadedAsync();
            if (string.IsNullOrEmpty(CurrentUser.CampaignId)) return;

            var campagne = await CampaignRepository.GetUserCampaignsAsync(CurrentUser.UserId ?? string.Empty);
            campaignName = campagne.FirstOrDefault(c => c.Id == CurrentUser.CampaignId)?.Name ?? "Campagna";
        }
        catch (Exception ex)
        {
            systemError = $"Errore caricamento: {ex.Message}";
        }
        finally
        {
            loading = false;
        }
    }

    private async Task OnFileSelectedAsync(InputFileChangeEventArgs e)
    {
        loadedPackage = null;
        plan = null;
        warnings = Array.Empty<string>();
        importReport = null;

        var file = e.File;
        if (file.Size > MaxFileSize)
        {
            Toasts.ShowError($"Il file supera {MaxFileSize / (1024 * 1024)} MB: controlla di aver scelto un pacchetto di dati.");
            return;
        }

        isBusy = true;
        try
        {
            using var stream = file.OpenReadStream(MaxFileSize);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var esito = CatalogPackageParser.Parse(json);
            warnings = esito.Warnings;

            if (esito.Package is null)
            {
                // Errori di validazione: toast, non banner. Il banner è per gli errori di sistema.
                foreach (var errore in esito.Errors.Take(3))
                    Toasts.ShowError(errore);
                if (esito.Errors.Count > 3)
                    Toasts.ShowError($"…e altri {esito.Errors.Count - 3} problemi nel file.");
                return;
            }

            loadedPackage = esito.Package;

            // L'anteprima si calcola sullo stato REALE del database, non sull'unione mostrata
            // dalle pagine: è ciò che l'import andrà a toccare.
            existing = await Catalog.GetCampaignCatalogsAsync(CurrentUser.CampaignId!);
            plan = PackageImportPlan.Build(
                loadedPackage, existing, CurrentUser.IsMaster, CurrentUser.UserId);
        }
        catch (Exception ex)
        {
            systemError = $"Errore lettura del file: {ex.Message}";
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task ConfirmImportAsync()
    {
        if (loadedPackage is null || plan is null || string.IsNullOrEmpty(CurrentUser.CampaignId)) return;

        var conferma = await Confirm.ShowAsync(
            $"Importare {plan.TotalWrites} voci nella campagna \"{campaignName}\"? " +
            $"{plan.TotalSkipped} verranno saltate.");
        if (!conferma) return;

        isBusy = true;
        systemError = null;
        var resoconto = new List<string>();
        try
        {
            // Una sezione per tipo, e dentro ciascuna due percorsi distinti: le creazioni in un
            // blocco solo (una richiesta, quindi una transazione), gli aggiornamenti riga per riga
            // partendo dalla riga esistente. PostgREST è transazionale sulla singola richiesta ma
            // non offre atomicità FRA richieste distinte (§9): se qualcosa si ferma a metà, i
            // passaggi già compiuti restano e il resoconto lo dice.
            var campagna = CurrentUser.CampaignId;
            var autore = CurrentUser.UserId;

            await EseguiSezioneAsync(
                "Specie", resoconto, loadedPackage.Species, existing.Races,
                p => p.Id, r => r.Id,
                p => PackageRowMerge.NuovaSpecie(p, campagna, autore), PackageRowMerge.ApplicaSpecie,
                RaceRepository.CreateManyAsync, RaceRepository.UpdateRaceAsync);

            await EseguiSezioneAsync(
                "Classi", resoconto, loadedPackage.Classes, existing.Classes,
                p => p.Id, c => c.Id,
                p => PackageRowMerge.NuovaClasse(p, campagna, autore), PackageRowMerge.ApplicaClasse,
                ClassRepository.CreateManyAsync, ClassRepository.UpdateClassAsync);

            await EseguiSezioneAsync(
                "Background", resoconto, loadedPackage.Backgrounds, existing.Backgrounds,
                p => p.Id, b => b.Id,
                p => PackageRowMerge.NuovoBackground(p, campagna, autore), PackageRowMerge.ApplicaBackground,
                BackgroundRepository.CreateManyAsync, BackgroundRepository.UpdateBackgroundAsync);

            await EseguiSezioneAsync(
                "Incantesimi", resoconto, loadedPackage.Spells, existing.Spells,
                p => p.Id, s => s.Id,
                p => PackageRowMerge.NuovoIncantesimo(p, campagna, autore), PackageRowMerge.ApplicaIncantesimo,
                SpellRepository.CreateManyAsync, SpellRepository.UpdateSpellAsync);

            await EseguiSezioneAsync(
                "Mostri", resoconto, loadedPackage.Monsters, existing.Monsters,
                p => p.Id, m => m.Id,
                p => PackageRowMerge.NuovoMostro(p, campagna, autore), PackageRowMerge.ApplicaMostro,
                MonsterRepository.CreateManyAsync, MonsterRepository.UpdateMonsterAsync);

            importReport = string.Join(" · ", resoconto);
            Toasts.ShowSuccess("Import completato");

            // L'anteprima ora è vecchia: rifarla su dati freschi evita che un secondo clic
            // riproponga di creare ciò che è appena stato creato.
            existing = await Catalog.GetCampaignCatalogsAsync(CurrentUser.CampaignId);
            plan = PackageImportPlan.Build(loadedPackage, existing, CurrentUser.IsMaster, CurrentUser.UserId);
        }
        catch (Exception ex)
        {
            importReport = string.Join(" · ", resoconto);
            systemError = $"Import interrotto: {ex.Message}";
        }
        finally
        {
            isBusy = false;
        }
    }

    // Esegue una sezione del piano e ne registra l'esito.
    //
    // Generico su (voce di pacchetto, riga di database) perché i cinque cataloghi non condividono
    // un'interfaccia comune: sono Model Postgrest indipendenti, e introdurne una per questo solo
    // scopo significherebbe toccarli tutti.
    //
    // L'esito si conta sulle righe RESTITUITE dal server, non si assume: le RLS bloccano gli UPDATE
    // senza sollevare eccezioni — la richiesta riesce e tocca zero righe, e i repository lo
    // rivelano restituendo null (§7).
    private async Task EseguiSezioneAsync<TPkg, TRow>(
        string titoloSezione,
        List<string> resoconto,
        List<TPkg> vociDelPacchetto,
        List<TRow> righeEsistenti,
        Func<TPkg, string> idVoce,
        Func<TRow, string> idRiga,
        Func<TPkg, TRow> nuovaRigaDa,
        Action<TPkg, TRow> applicaSu,
        Func<List<TRow>, Task<List<TRow>>> creaInBlocco,
        Func<TRow, Task<TRow?>> aggiornaUna)
        where TRow : class
    {
        var sezione = plan!.Sections.FirstOrDefault(s => s.Title == titoloSezione);
        if (sezione is null) return;

        var perId = vociDelPacchetto.ToDictionary(idVoce, StringComparer.Ordinal);
        var scritte = 0;
        var attese = 0;

        // 1. Creazioni: un blocco solo.
        var daCreare = sezione.Items
            .Where(i => i.Outcome == ImportOutcome.Create && perId.ContainsKey(i.SourceId))
            .Select(i => nuovaRigaDa(perId[i.SourceId]))
            .ToList();

        attese += daCreare.Count;
        if (daCreare.Count > 0)
            scritte += (await creaInBlocco(daCreare)).Count;

        // 2. Aggiornamenti: uno per riga, PARTENDO dalla riga esistente. Il formato non copre tutte
        // le colonne — un file senza `languages`, senza i bonus di specie, senza le caratteristiche
        // di un mostro le azzererebbe — e non deve toccare né `added_by` né `created_at`, altrimenti
        // un reimport del master si approprierebbe delle voci caricate dai giocatori.
        foreach (var item in sezione.Items.Where(i => i.Outcome == ImportOutcome.Update))
        {
            if (!perId.TryGetValue(item.SourceId, out var voce)) continue;

            var esistente = righeEsistenti.FirstOrDefault(r => idRiga(r) == item.ExistingRowId);
            if (esistente is null) continue;

            attese++;
            applicaSu(voce, esistente);
            if (await aggiornaUna(esistente) is not null) scritte++;
        }

        if (attese == 0) return;

        resoconto.Add(scritte == attese
            ? $"{titoloSezione}: {scritte}"
            : $"{titoloSezione}: {scritte} di {attese} (il server ne ha rifiutate {attese - scritte})");
    }
}
```

> I titoli passati a `EseguiSezioneAsync` devono combaciare **alla lettera** con quelli che
> `PackageImportPlan.Build` assegna alle sezioni («Specie», «Classi», «Background», «Incantesimi»,
> «Mostri»): il confronto è per stringa, e un refuso non produce un errore — produce una sezione che
> non scrive niente in silenzio.

- [ ] **Step 3: Verifica che la conversione arrivi da `PackageRowMerge`**

Non ci sono convertitori da scrivere qui: `NuovaSpecie`/`NuovaClasse`/`NuovoBackground`/
`NuovoIncantesimo`/`NuovoMostro` e i cinque `Applica*` sono helper puri del Task 2, testati con xUnit.
La pagina si limita a passarli a `EseguiSezioneAsync`, come mostrato nello Step 2.

Controlla che il blocco `@code` **non** contenga copie locali di quella logica:

```bash
grep -n "private .* RigaDa\|private .* Applica" Pages/DataPackages.razor
```
Atteso: nessun risultato. Se ne trovi, spostale in `Services/PackageRowMerge.cs` con i loro test —
è la regola di progetto («logica di dominio in helper puri `static`, mai nei `.razor`»), e qui non è
una formalità: l'invariante che quei metodi custodiscono (identità, proprietà e colonne fuori
formato che sopravvivono all'aggiornamento) è verificabile **solo** da xUnit.

- [ ] **Step 4: Aggiungi l'export alla pagina**

```csharp
    private async Task EsportaAsync()
    {
        if (string.IsNullOrEmpty(CurrentUser.CampaignId)) return;

        isBusy = true;
        systemError = null;
        try
        {
            var cataloghi = await Catalog.GetCampaignCatalogsAsync(CurrentUser.CampaignId);
            var pacchetto = CampaignExport.Build(cataloghi, campaignName);
            var json = CampaignExport.ToJson(pacchetto);

            await JS.InvokeVoidAsync("downloadTextFile", pacchetto.Id + ".json", json);
            Toasts.ShowSuccess("Esportato");
        }
        catch (Exception ex)
        {
            systemError = $"Errore esportazione: {ex.Message}";
        }
        finally
        {
            isBusy = false;
        }
    }
```

Il pulsante: `<button type="button" class="primary-btn" @onclick="EsportaAsync" disabled="@isBusy"
aria-label="Esporta i cataloghi della campagna">⬇ Esporta la campagna</button>`.

- [ ] **Step 5: Scrivi il CSS isolato**

**Copia** in `Pages/DataPackages.razor.css` le regole delle classi che servono, prendendole da una
pagina esistente (`Pages/Backgrounds.razor.css` per quasi tutte, `Pages/Home.razor.css` per
`.danger-btn`):

- il **contenitore di pagina** — in Backgrounds è `.backgrounds-container` (gradiente,
  `min-height: 100vh`, font e margini negativi): senza, `/dati` nasce fuori tema. Rinominalo
  `.dati-container`;
- `.page-header`, `.page-title`, `.back-btn`, `.primary-btn`, `.secondary-btn`, `.danger-btn`,
  `.field`, `.input`, `.empty-state`;
- il blocco **`@media (min-width: 641px)`** che regola contenitore e titolo su desktop: le media
  query non sono condivise più delle altre regole, e senza la pagina non è responsive.

Aggiungi poi le classi dell'anteprima (`.plan-section`, `.plan-counts`, `.plan-skipped`).
**Solo token** di `:root`, mai literal esadecimali.

> ⚠️ **Non "riusare": copiare.** Nessuna di quelle classi sta in `wwwroot/css/app.css` — sono tutte
> definite dentro il `.razor.css` di ciascuna pagina, e il CSS isolato di Blazor le lega all'attributo
> di scope di *quella* pagina. Il progetto infatti ridefinisce `.primary-btn` in **undici** file.
> Il gotcha non riguarda solo genitore→figlio: **due `.razor` non condividono nulla**. Darlo per
> scontato produrrebbe una pagina senza stile, e in particolare un pulsante di rimozione che non
> sembra pericoloso.

- [ ] **Step 6: Aggiungi la card in Home**

In `Pages/Home.razor`, in fondo all'array `cards`:

```csharp
        new("📦", "Dati",        "Importa ed esporta cataloghi", "dati"),
```

- [ ] **Step 7: Verifica a mano**

Comando: `dotnet run`

1. Esporta la campagna: il file si scarica, il nome è `campagna-<nome>.json`, e aprendolo si vedono
   le voci dei cataloghi.
2. **Reimporta quel file nella stessa campagna**: le voci nate a mano risultano `SkippedLocalWins`
   (le righe esistono già senza provenienza e vincono sul pacchetto), quelle che avevano un
   `source_id` — che l'export conserva — risultano `Update` o "saltata, non modificabile".
   **In nessun caso `Create`**: è il controllo che scopre i doppioni prima che li veda un utente.
3. Modifica a mano l'`id` del file in `mio-pacchetto` e reimportalo in una campagna **vuota**:
   l'anteprima mostra tutte creazioni; conferma; i cataloghi si popolano; le voci sono modificabili
   (hanno `source_id` ma **non** dal pacchetto dell'app — §6).
4. Reimporta lo stesso file: ora l'anteprima mostra aggiornamenti, non creazioni, e dopo la conferma
   il numero di righe **non cresce**.
5. Fai importare lo stesso file a un secondo account non-master: le voci create dal primo devono
   comparire come "saltate — non modificabile".

- [ ] **Step 8: Verifica build e suite**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori.

Comando: `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: tutti verdi.

- [ ] **Step 9: Commit**

```bash
git add Pages/DataPackages.razor Pages/DataPackages.razor.css Pages/Home.razor
git commit -m "feat(dati): schermata di import con anteprima ed export della campagna"
```

---

### Task 11: Rimozione di un import per provenienza

**File:**
- Modifica: `Pages/DataPackages.razor`, `Pages/DataPackages.razor.css`

**Interfacce:**
- Consuma: `ICatalogService.GetCampaignCatalogsAsync` (Task 5), i cinque `DeleteByIdsAsync` (Task 4),
  `ICharacterSpellRepository.GetBySpellIdsAsync` (Task 4), `CatalogKey.IsFromAppPackage`,
  `AccessControl.CanEdit`, `ConfirmService`.
- Produce: nessuna API.

> «È l'operazione più distruttiva dello spec e va trattata come tale» (§8). Tre protezioni non
> negoziabili: la provenienza **pacchetto dell'app non è rimovibile**; l'anteprima dice **quanti
> personaggi perderanno un incantesimo** per via del `ON DELETE CASCADE`; e la selezione delle righe
> avviene **in memoria**, mai con un `LIKE` costruito col testo digitato.
>
> ⚠️ Il terzo punto non è pedanteria. In SQL `LIKE`, `_` vale "un carattere qualsiasi": con un filtro
> `source_id LIKE '<digitato>/%'`, chi scrive `srd-2024-i_` supera la guardia — che confronta il
> prefisso in modo **esatto** — e cancella proprio le voci del manuale, incluse quelle materializzate
> (create con l'`added_by` del giocatore che le ha usate, quindi `AccessControl.CanEdit` non frena),
> portandosi via per `CASCADE` gli incantesimi dalle schede. Con `%` si arriva a ogni riga di
> provenienza della campagna. Le righe si filtrano quindi in memoria con `StartsWith(…, Ordinal)` e
> si cancellano per **elenco di id**: l'insieme cancellato è esattamente quello mostrato.

- [ ] **Step 1: Aggiungi lo stato e il calcolo dell'anteprima**

Nel blocco `@code` di `Pages/DataPackages.razor`:

```csharp
    private string removalPrefix = string.Empty;
    private RemovalPreview? removalPreview;
    private string? removalReport;

    /// <summary>Che cosa una rimozione toglierebbe, prima di toglierlo. Porta con sé gli **id
    /// esatti** da cancellare, non un criterio da rivalutare: è la garanzia che l'insieme cancellato
    /// sia quello mostrato.
    ///
    /// `BlockedByPermission` non è un dettaglio: la rimozione rispetta AccessControl.CanEdit ed è
    /// quindi quasi sempre PARZIALE (§8).</summary>
    private sealed record RemovalPreview(
        string Prefix,
        List<string> RaceIds, List<string> ClassIds, List<string> BackgroundIds,
        List<string> SpellIds, List<string> MonsterIds,
        int BlockedByPermission, int AffectedCharacters)
    {
        public int Total => RaceIds.Count + ClassIds.Count + BackgroundIds.Count
                            + SpellIds.Count + MonsterIds.Count;
    }

    private async Task CalcolaAnteprimaRimozioneAsync()
    {
        removalPreview = null;
        removalReport = null;

        var prefisso = removalPrefix.Trim();
        if (string.IsNullOrEmpty(prefisso))
        {
            Toasts.ShowError("Indica la provenienza da rimuovere (es. mio-pacchetto).");
            return;
        }

        // §8: la provenienza del pacchetto dell'app NON è rimovibile. Sarebbe il danno della
        // materializzazione moltiplicato per N righe in un colpo solo, e AccessControl.CanEdit non
        // farebbe da freno — quelle righe nascono con l'added_by del giocatore che le ha usate.
        if (CatalogKey.IsFromAppPackage(prefisso + "/"))
        {
            Toasts.ShowError("Le voci del manuale distribuito con l'app non si rimuovono in blocco.");
            return;
        }

        isBusy = true;
        systemError = null;
        try
        {
            removalPreview = await LeggiImpattoAsync(prefisso);
        }
        catch (Exception ex)
        {
            systemError = $"Errore nel calcolo dell'anteprima: {ex.Message}";
        }
        finally
        {
            isBusy = false;
        }
    }

    // Il calcolo vero, senza toccare isBusy né systemError: lo richiama anche RimuoviAsync per
    // ricontare dopo l'operazione, e un finally annidato che spegne isBusy riabiliterebbe i
    // pulsanti a metà di un'operazione ancora in corso.
    private async Task<RemovalPreview> LeggiImpattoAsync(string prefisso)
    {
        var cataloghi = await Catalog.GetCampaignCatalogsAsync(CurrentUser.CampaignId!);
        var conPrefisso = prefisso + "/";

        // Confronto ordinale in memoria, MAI un LIKE: `_` e `%` digitati dall'utente sarebbero
        // wildcard e la DELETE colpirebbe righe che questa anteprima non ha mai contato.
        bool DaQuestaProvenienza(string? sourceId) =>
            sourceId is not null && sourceId.StartsWith(conPrefisso, StringComparison.Ordinal);

        bool Rimovibile(string? addedBy) =>
            AccessControl.CanEdit(CurrentUser.IsMaster, addedBy, CurrentUser.UserId);

        var razze = cataloghi.Races.Where(r => DaQuestaProvenienza(r.SourceId)).ToList();
        var classi = cataloghi.Classes.Where(c => DaQuestaProvenienza(c.SourceId)).ToList();
        var background = cataloghi.Backgrounds.Where(b => DaQuestaProvenienza(b.SourceId)).ToList();
        var incantesimi = cataloghi.Spells.Where(s => DaQuestaProvenienza(s.SourceId)).ToList();
        var mostri = cataloghi.Monsters.Where(m => DaQuestaProvenienza(m.SourceId)).ToList();

        var bloccate =
            razze.Count(r => !Rimovibile(r.AddedBy)) +
            classi.Count(c => !Rimovibile(c.AddedBy)) +
            background.Count(b => !Rimovibile(b.AddedBy)) +
            incantesimi.Count(s => !Rimovibile(s.AddedBy)) +
            mostri.Count(m => !Rimovibile(m.AddedBy));

        // Solo le righe che verranno DAVVERO rimosse finiscono negli elenchi: quelle bloccate dai
        // permessi si contano a parte e non si tenta nemmeno di cancellarle.
        var idIncantesimi = incantesimi.Where(s => Rimovibile(s.AddedBy)).Select(s => s.Id).ToList();

        // Quanti PG perderanno un incantesimo: character_spells_spell_id_fkey è ON DELETE CASCADE,
        // quindi togliere la riga dal catalogo la toglie dalle schede (§8).
        var legami = await CharacterSpellRepository.GetBySpellIdsAsync(idIncantesimi);
        var pgColpiti = legami.Select(l => l.CharacterId).Distinct().Count();

        return new RemovalPreview(
            prefisso,
            razze.Where(r => Rimovibile(r.AddedBy)).Select(r => r.Id).ToList(),
            classi.Where(c => Rimovibile(c.AddedBy)).Select(c => c.Id).ToList(),
            background.Where(b => Rimovibile(b.AddedBy)).Select(b => b.Id).ToList(),
            idIncantesimi,
            mostri.Where(m => Rimovibile(m.AddedBy)).Select(m => m.Id).ToList(),
            bloccate, pgColpiti);
    }
```

Aggiungi `@inject ICharacterSpellRepository CharacterSpellRepository` in testa alla pagina.

- [ ] **Step 2: Esegui la rimozione con conferma numerata**

```csharp
    private async Task RimuoviAsync()
    {
        if (removalPreview is null || string.IsNullOrEmpty(CurrentUser.CampaignId)) return;

        // Il testo del ConfirmDialog riporta i numeri invece di limitarsi a "sei sicuro?" (§8), e
        // nomina la provenienza CONGELATA nell'anteprima, non quella scritta nel campo adesso: chi
        // ricalcola, cambia idea e poi conferma, deve leggere ciò che verrà davvero cancellato.
        var messaggio = $"Rimuovere {removalPreview.Total} voci di provenienza \"{removalPreview.Prefix}\"?";
        if (removalPreview.AffectedCharacters > 0)
            messaggio += $" {removalPreview.AffectedCharacters} personaggi perderanno un incantesimo dalla propria scheda.";
        if (removalPreview.BlockedByPermission > 0)
            messaggio += $" {removalPreview.BlockedByPermission} voci non sono tue e resteranno.";

        if (!await Confirm.ShowAsync(messaggio)) return;

        isBusy = true;
        systemError = null;
        try
        {
            // Si cancellano gli id esatti che l'anteprima ha mostrato — non un criterio rivalutato
            // adesso, che potrebbe raccogliere righe diverse da quelle contate.
            await RaceRepository.DeleteByIdsAsync(removalPreview.RaceIds);
            await ClassRepository.DeleteByIdsAsync(removalPreview.ClassIds);
            await BackgroundRepository.DeleteByIdsAsync(removalPreview.BackgroundIds);
            await SpellRepository.DeleteByIdsAsync(removalPreview.SpellIds);
            await MonsterRepository.DeleteByIdsAsync(removalPreview.MonsterIds);

            // Il Delete di questa libreria non dice quante righe ha tolto, e un Delete bloccato
            // dalla RLS "riesce" a vuoto (§3 di DA-FARE): l'unico resoconto onesto è ricontare.
            var prima = removalPreview.Total;
            removalPreview = await LeggiImpattoAsync(removalPreview.Prefix);
            var rimaste = removalPreview.Total;

            removalReport = rimaste == 0
                ? $"Rimosse {prima} voci."
                : $"Rimosse {prima - rimaste} voci su {prima}; {rimaste} sono rimaste (non tue).";
            Toasts.ShowSuccess("Rimozione completata");
        }
        catch (Exception ex)
        {
            systemError = $"Errore durante la rimozione: {ex.Message}";
        }
        finally
        {
            isBusy = false;
        }
    }
```

- [ ] **Step 3: Aggiungi la sezione al markup**

Una terza sezione "Rimuovi un import" con:
- un `<input class="input">` legato a `removalPrefix`, con `aria-label="Provenienza da rimuovere"`
  e un testo d'aiuto che spiega dov'è scritta (è l'`id` in cima al file, e il prefisso degli id
  delle sue voci). All'`@oninput` **azzera `removalPreview`**: un'anteprima calcolata su un'altra
  provenienza non deve restare a schermo accanto a un campo che ora dice altro;
- un pulsante "Calcola l'impatto" (`@onclick="CalcolaAnteprimaRimozioneAsync"`);
- l'anteprima, mostrata solo se `removalPreview is not null`, con i cinque conteggi, le voci bloccate
  e — evidenziato — il numero di personaggi colpiti;
- il pulsante "🗑 Rimuovi", `class="secondary-btn danger-btn"` (le due classi vanno **copiate** in
  `DataPackages.razor.css`, vedi Task 10 Step 5), abilitato solo se `removalPreview.Total > 0`;
- `removalReport` mostrato dopo l'operazione.

- [ ] **Step 4: Verifica a mano**

Comando: `dotnet run`

1. Importa un file con `id` = `mio-pacchetto` in una campagna di prova (Task 10).
2. Aggiungi uno degli incantesimi importati alla scheda di un PG.
3. In "Rimuovi un import", scrivi `mio-pacchetto` e calcola l'impatto: i conteggi devono corrispondere
   e **"1 personaggio perderà un incantesimo"** deve comparire.
4. Conferma: le voci spariscono dai cataloghi e l'incantesimo sparisce dalla scheda del PG.
5. Prova a scrivere `srd-2024-it`: deve comparire il toast che nega la rimozione, **senza** calcolare
   nulla.
6. Con un secondo account non-master, prova a rimuovere una provenienza le cui righe sono state create
   dal primo: l'anteprima deve dire quante resteranno, e dopo l'operazione il resoconto deve dirlo di
   nuovo con i numeri veri.

- [ ] **Step 5: Verifica build e suite**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori.

Comando: `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: tutti verdi.

- [ ] **Step 6: Commit**

```bash
git add Pages/DataPackages.razor Pages/DataPackages.razor.css
git commit -m "feat(dati): rimozione di un import per provenienza con anteprima dell'impatto"
```

---

## Al termine della fase

- Aggiorna `docs/DA-FARE.md` (§8-bis: Fase 2 completata, Fase 3 unica rimasta) e `docs/DIARIO.md` con
  cosa è stato fatto e **perché**, incluse le decisioni prese qui: `SkippedLocalWins`, l'abbandono di
  `Upsert` (misurato, non congetturato), l'aggiornamento che fonde invece di sostituire, e il divieto
  di `LIKE` su testo digitato.
- Aggiorna anche la voce dei **gotcha di stack** in memoria: `Upsert` di postgrest-csharp 3.5.1
  serializza la chiave primaria anche con `[PrimaryKey(…, false)]`, quindi è inutilizzabile sui Model
  con `id uuid` generato dal database. È esattamente il genere di trappola che quella memoria esiste
  per intercettare.
- **Annota la divergenza dallo spec**, che resta la fonte di verità e va tenuta onesta: §4.4 e la
  tabella di §9 prescrivono un `Upsert` con `on_conflict`, e `DA-FARE` §8-bis lo ripete nella
  descrizione della Fase 2. La misura sul campo dice che quella strada non esiste con questa
  libreria. Correggi le tre occorrenze invece di lasciare uno spec che descrive un'implementazione
  impossibile.
- Annota fra le conseguenze note:
  - i **mostri di pacchetto non compaiono** nel pannello "Importa mostri" del tracker finché non
    vengono duplicati in campagna (Task 7, Step 4);
  - un file **esportato perde la provenienza delle righe materializzate** dal manuale (diventa
    `<id campagna>/<slug>`): reimportandolo altrove quelle voci saranno normali contenuti di
    campagna, non voci di manuale. È voluto — l'alternativa era iniettare righe intoccabili in
    campagne che non hanno mai importato nulla di ufficiale.
- Lancia il gate a due agenti (`critico` e `conformità`) sul diff complessivo, come prescrive
  `CLAUDE.md`.

## Cosa questa fase non chiude

Resta alla **Fase 3**: campione SRD per validare il formato sul campo, traduzione del pacchetto
completo, e il wizard 2024 (bonus dal background con ripartizione, tetto di 20, convivenza con le
specie legacy, §4.7).

Restano inoltre i tre debiti che lo spec crea consapevolmente (§14): la ripartizione salvata ma usata
solo dal wizard; una riga materializzata e poi inutilizzata non rimovibile singolarmente; "duplica e
modifica" che non ritocca le schede esistenti.

**Un limite del formato emerso scrivendo questo piano, da risolvere in Fase 3:** `PackageSpeed.Value`
è un `int`, ma il manuale italiano assegna velocità frazionarie (il Nano corre **7,5 m**). Un decimale
nel JSON fa fallire la deserializzazione dell'**intero** pacchetto, non della singola voce. Le opzioni
sono arrotondare in traduzione, passare a `decimal`, o esprimere la velocità in centimetri: la scelta
va fatta quando il contenuto vero esiste, non prima. Nessuna delle tre tocca il codice di questa fase.

E resta aperta la voce di §5 di `DA-FARE` che questa fase **rimette in gioco davvero**: con un
pacchetto importato i cataloghi superano le ~50 voci su cui poggiava la decisione di scartare la
virtualizzazione. Da rivalutare a pacchetto pieno, non prima.
