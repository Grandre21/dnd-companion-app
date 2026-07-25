# Modello 2024 + import dati — Fase 1: leggere un pacchetto

> **Per chi esegue:** SOTTO-SKILL RICHIESTA: usa `superpowers:subagent-driven-development` (consigliata) o
> `superpowers:executing-plans` per implementare il piano task per task. Gli step usano caselle
> (`- [ ]`) per il tracciamento.

**Obiettivo:** l'app sa leggere un pacchetto di dati JSON, unirlo ai cataloghi della campagna e mostrarlo
in sola lettura, con la nuova pagina Background.

**Architettura:** il pacchetto è un file servito con la PWA, deserializzato da helper puri e unito ai
dati dei repository dentro `CatalogService`, non nelle pagine. Nessuna policy RLS esistente viene
toccata: si aggiungono la tabella `backgrounds`, sei colonne additive e quattro vincoli di unicità
(un quinto nasce dentro la `CREATE TABLE` della tabella nuova, quindi non è una migrazione).

**Stack:** Blazor WebAssembly / .NET 10 · `System.Text.Json` con source generator · xUnit ·
Supabase (PostgREST) via `postgrest-csharp 3.5.1`.

**Spec di riferimento:** [`../specs/2026-07-25-modello-2024-import-dati-design.md`](../specs/2026-07-25-modello-2024-import-dati-design.md)
(commit `76ba5d8`). I riferimenti `§N` qui sotto puntano a quel documento.

## Vincoli globali

- **Tutto in italiano**: stringhe UI, commenti, messaggi di errore, nomi delle voci nei dati.
- **Zero migrazioni di dati.** I personaggi e i cataloghi esistenti non vengono toccati né ricalcolati.
  Le sole migrazioni ammesse sono additive: **1 tabella nuova + 6 colonne additive su 5 tabelle
  esistenti + 4 vincoli `UNIQUE` additivi** (`backgrounds` porta il proprio dentro la `CREATE TABLE`).
- **Build pulita**: `dotnet build` deve restare a **0 warning / 0 errori**. Il progetto pubblica con
  `TrimMode=full`: usare `System.Text.Json` **con source generator** (`JsonSerializerContext`), mai
  l'overload a reflection, che produce warning di trimming.
- **Niente `String.Normalize` né API di globalizzazione.** Il progetto compila con
  `InvariantGlobalization=true`: senza ICU quelle API falliscono **in silenzio** (§4.3).
  `ToLowerInvariant` invece funziona.
- **Logica di dominio in helper puri `static`** in `Services/`, testati con xUnit. Mai nei `.razor`.
- **Dati dietro repository** per aggregato, interfaccia e implementazione nello stesso file in
  `Services/Repositories/`, registrati `AddSingleton` in `Program.cs`.
- **Pattern UI**: toast `.app-toast` (mai `.toast`), `ConfirmDialog` (mai `confirm()`),
  `<LoadingSpinner>`, `DbErrorBanner` per i soli errori di sistema, colori dai token in `:root`.
- **Prefisso del pacchetto dell'app**: `srd-2024-it/`. È la costante che distingue una riga di
  provenienza pacchetto da una caricata dall'utente (§6).

## Come verificare

- Test: `dotnet test Tests/DndCompanion.Tests.csproj`
- Build: `dotnet build`
- I test d'integrazione RLS (`Tests.Integration/`) richiedono lo stack Supabase locale e vanno in
  auto-skip se assente.

---

### Task 1: Modelli del pacchetto e deserializzazione

**File:**
- Crea: `Models/Packages/CatalogPackage.cs`
- Crea: `Services/CatalogPackageParser.cs`
- Test: `Tests/CatalogPackageParserTests.cs`

**Interfacce:**
- Consuma: niente (primo task).
- Produce: `CatalogPackage` e i tipi annidati; `CatalogPackageParser.Parse(string json)` che ritorna
  `ParseResult(CatalogPackage? Package, IReadOnlyList<string> Errors)`.

- [ ] **Step 1: Scrivi i modelli del pacchetto**

`Models/Packages/CatalogPackage.cs`:

```csharp
using System.Text.Json.Serialization;

namespace DndCompanion.Models.Packages;

/// <summary>Pacchetto di dati importabile/esportabile (§5 dello spec). POCO di sola
/// deserializzazione: non sono Model Postgrest e non hanno attributi di tabella.</summary>
public sealed class CatalogPackage
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("edition")] public string Edition { get; set; } = string.Empty;
    [JsonPropertyName("language")] public string Language { get; set; } = string.Empty;
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    [JsonPropertyName("license")] public PackageLicense? License { get; set; }
    [JsonPropertyName("species")] public List<PackageSpecies> Species { get; set; } = new();
    [JsonPropertyName("backgrounds")] public List<PackageBackground> Backgrounds { get; set; } = new();
    [JsonPropertyName("feats")] public List<PackageFeat> Feats { get; set; } = new();
    [JsonPropertyName("classes")] public List<PackageClass> Classes { get; set; } = new();
    [JsonPropertyName("spells")] public List<PackageSpell> Spells { get; set; } = new();
    [JsonPropertyName("monsters")] public List<PackageMonster> Monsters { get; set; } = new();
}

public sealed class PackageLicense
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("attribution")] public string Attribution { get; set; } = string.Empty;
}

public sealed class PackageSpeed
{
    [JsonPropertyName("value")] public int Value { get; set; }
    /// <summary>"m" o "ft" (§4.5). Il pacchetto italiano usa i metri.</summary>
    [JsonPropertyName("unit")] public string Unit { get; set; } = "m";
}

public sealed class PackageSpecies
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("size")] public string Size { get; set; } = string.Empty;
    [JsonPropertyName("speed")] public PackageSpeed? Speed { get; set; }
    [JsonPropertyName("traits")] public string Traits { get; set; } = string.Empty;
}

public sealed class PackageBackground
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    /// <summary>Le TRE caratteristiche su cui il background concede i bonus. La ripartizione
    /// (+2/+1 oppure +1/+1/+1) la sceglie il giocatore, non il background (§4.2).</summary>
    [JsonPropertyName("abilityScores")] public List<string> AbilityScores { get; set; } = new();
    [JsonPropertyName("originFeat")] public string OriginFeat { get; set; } = string.Empty;
    [JsonPropertyName("skillProficiencies")] public List<string> SkillProficiencies { get; set; } = new();
    [JsonPropertyName("toolProficiency")] public string ToolProficiency { get; set; } = string.Empty;
    [JsonPropertyName("equipment")] public string Equipment { get; set; } = string.Empty;
}

/// <summary>Talento. Solo consultazione: non ha tabella e non è importabile (§5).</summary>
public sealed class PackageFeat
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
}

public sealed class PackageSkillChoices
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("from")] public List<string> From { get; set; } = new();
}

public sealed class PackageClassLevel
{
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("features")] public List<string> Features { get; set; } = new();
    /// <summary>Nove slot, dal livello 1 al 9.</summary>
    [JsonPropertyName("spellSlots")] public List<int> SpellSlots { get; set; } = new();
}

public sealed class PackageClass
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("hitDie")] public string HitDie { get; set; } = string.Empty;
    [JsonPropertyName("primaryAbility")] public string PrimaryAbility { get; set; } = string.Empty;
    [JsonPropertyName("savingThrows")] public List<string> SavingThrows { get; set; } = new();
    [JsonPropertyName("skillChoices")] public PackageSkillChoices? SkillChoices { get; set; }
    [JsonPropertyName("levels")] public List<PackageClassLevel> Levels { get; set; } = new();
}

public sealed class PackageSpell
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("school")] public string School { get; set; } = string.Empty;
    [JsonPropertyName("castingTime")] public string CastingTime { get; set; } = string.Empty;
    [JsonPropertyName("range")] public string Range { get; set; } = string.Empty;
    [JsonPropertyName("components")] public string Components { get; set; } = string.Empty;
    [JsonPropertyName("duration")] public string Duration { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("classes")] public List<string> Classes { get; set; } = new();
}

public sealed class PackageMonster
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("challengeRating")] public string ChallengeRating { get; set; } = string.Empty;
    [JsonPropertyName("armorClass")] public int ArmorClass { get; set; }
    [JsonPropertyName("hitPoints")] public string HitPoints { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Scrivi i test del parser (falliranno)**

`Tests/CatalogPackageParserTests.cs`:

```csharp
using DndCompanion.Services;

namespace DndCompanion.Tests;

public class CatalogPackageParserTests
{
    private const string ValidJson = """
    {
      "schemaVersion": 1,
      "id": "srd-2024-it",
      "name": "SRD 5.2 — Italiano",
      "edition": "2024",
      "language": "it",
      "version": "1.0.0",
      "license": { "name": "CC BY 4.0", "attribution": "Wizards of the Coast" },
      "species": [
        { "id": "srd-2024-it/elfo", "name": "Elfo", "size": "Media",
          "speed": { "value": 9, "unit": "m" }, "traits": "Scurovisione" }
      ]
    }
    """;

    [Fact]
    public void Parse_PacchettoValido_RestituisceIlPacchettoSenzaErrori()
    {
        var result = CatalogPackageParser.Parse(ValidJson);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Package);
        Assert.Equal("srd-2024-it", result.Package!.Id);
        Assert.Single(result.Package.Species);
        Assert.Equal("Elfo", result.Package.Species[0].Name);
        Assert.Equal(9, result.Package.Species[0].Speed!.Value);
        Assert.Equal("m", result.Package.Species[0].Speed!.Unit);
    }

    [Fact]
    public void Parse_JsonMalformato_RestituisceErroreSenzaLanciare()
    {
        var result = CatalogPackageParser.Parse("{ non è json");

        Assert.Null(result.Package);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_VersioneSchemaFutura_RifiutaIlPacchetto()
    {
        var json = ValidJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");

        var result = CatalogPackageParser.Parse(json);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("99"));
    }

    [Fact]
    public void Parse_IdMancante_SegnalaLaVoceColpevole()
    {
        var json = ValidJson.Replace("\"id\": \"srd-2024-it/elfo\",", "");

        var result = CatalogPackageParser.Parse(json);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("Elfo"));
    }

    [Fact]
    public void Parse_LinguaDiversaDaItaliano_AccettaEAvvisa()
    {
        var json = ValidJson.Replace("\"language\": \"it\"", "\"language\": \"en\"");

        var result = CatalogPackageParser.Parse(json);

        Assert.NotNull(result.Package);
        Assert.Contains(result.Warnings, w => w.Contains("lingua"));
    }

    [Fact]
    public void Parse_StringaVuota_RestituisceErrore()
    {
        var result = CatalogPackageParser.Parse("");

        Assert.Null(result.Package);
        Assert.NotEmpty(result.Errors);
    }
}
```

- [ ] **Step 3: Esegui i test e verifica che falliscano**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CatalogPackageParserTests`
Atteso: FALLIMENTO di compilazione — `CatalogPackageParser` non esiste.

- [ ] **Step 4: Implementa il parser**

`Services/CatalogPackageParser.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Contesto di serializzazione generato a compile-time: il progetto pubblica con
/// TrimMode=full, dove gli overload a reflection di System.Text.Json producono warning.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CatalogPackage))]
internal partial class CatalogPackageJsonContext : JsonSerializerContext { }

/// <summary>Esito della lettura di un pacchetto: o il pacchetto, o gli errori che lo hanno
/// respinto. Gli avvisi non impediscono l'uso.</summary>
public sealed record ParseResult(
    CatalogPackage? Package,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

/// <summary>Lettura e validazione di un pacchetto di dati (§5 dello spec). Logica pura:
/// nessuna rete, nessun accesso al database.</summary>
public static class CatalogPackageParser
{
    /// <summary>Versione di schema che questo codice sa leggere.</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>Prefisso degli identificatori del pacchetto distribuito con l'app (§6).</summary>
    public const string AppPackageId = "srd-2024-it";

    public static ParseResult Parse(string? json)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(json))
            return new ParseResult(null, new[] { "Il file è vuoto." }, warnings);

        CatalogPackage? package;
        try
        {
            package = JsonSerializer.Deserialize(json, CatalogPackageJsonContext.Default.CatalogPackage);
        }
        catch (JsonException ex)
        {
            return new ParseResult(null, new[] { $"Il file non è un JSON valido: {ex.Message}" }, warnings);
        }

        if (package is null)
            return new ParseResult(null, new[] { "Il file non contiene un pacchetto." }, warnings);

        if (package.SchemaVersion != SupportedSchemaVersion)
        {
            errors.Add($"Versione di schema {package.SchemaVersion} non supportata " +
                       $"(questa app legge la versione {SupportedSchemaVersion}).");
            return new ParseResult(null, errors, warnings);
        }

        if (string.IsNullOrWhiteSpace(package.Id))
            errors.Add("Il pacchetto non ha un identificatore ('id').");

        ValidateEntries(package, errors);

        if (!string.Equals(package.Language, "it", StringComparison.OrdinalIgnoreCase))
            warnings.Add($"Il pacchetto è in lingua '{package.Language}': alcune funzioni che " +
                         "dipendono dalla lingua, come il filtro per classe, potrebbero non trovarlo.");

        return errors.Count > 0
            ? new ParseResult(null, errors, warnings)
            : new ParseResult(package, errors, warnings);
    }

    // Ogni voce deve avere id e nome: senza id non sopravvive all'import (§4.3),
    // senza nome non è confrontabile. L'errore cita il nome, o la posizione se manca anche quello.
    private static void ValidateEntries(CatalogPackage p, List<string> errors)
    {
        Check(p.Species.Select(x => (x.Id, x.Name)), "specie", errors);
        Check(p.Backgrounds.Select(x => (x.Id, x.Name)), "background", errors);
        Check(p.Feats.Select(x => (x.Id, x.Name)), "talenti", errors);
        Check(p.Classes.Select(x => (x.Id, x.Name)), "classi", errors);
        Check(p.Spells.Select(x => (x.Id, x.Name)), "incantesimi", errors);
        Check(p.Monsters.Select(x => (x.Id, x.Name)), "mostri", errors);
    }

    private static void Check(IEnumerable<(string Id, string Name)> entries, string section, List<string> errors)
    {
        var index = 0;
        foreach (var (id, name) in entries)
        {
            var etichetta = string.IsNullOrWhiteSpace(name) ? $"posizione {index + 1}" : name;
            if (string.IsNullOrWhiteSpace(id))
                errors.Add($"Sezione '{section}': la voce «{etichetta}» non ha un identificatore.");
            if (string.IsNullOrWhiteSpace(name))
                errors.Add($"Sezione '{section}': la voce in posizione {index + 1} non ha un nome.");
            index++;
        }
    }
}
```

- [ ] **Step 5: Esegui i test e verifica che passino**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CatalogPackageParserTests`
Atteso: 6 test PASSATI.

- [ ] **Step 6: Verifica che la build resti pulita**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori. Se compaiono warning di trimming, il source generator non è stato usato:
controlla che la deserializzazione passi da `CatalogPackageJsonContext.Default.CatalogPackage` e non
dall'overload generico.

- [ ] **Step 7: Commit**

```bash
git add Models/Packages/CatalogPackage.cs Services/CatalogPackageParser.cs Tests/CatalogPackageParserTests.cs
git commit -m "feat(pacchetti): modelli del pacchetto dati e parser con validazione"
```

---

### Task 2: Chiave di confronto e provenienza

**File:**
- Crea: `Services/CatalogKey.cs`
- Test: `Tests/CatalogKeyTests.cs`

**Interfacce:**
- Consuma: `CatalogPackageParser.AppPackageId` (Task 1).
- Produce: `CatalogKey.NormalizeName(string?)`, `CatalogKey.For(string? sourceId, string? name)`,
  `CatalogKey.IsFromAppPackage(string? sourceId)`.

- [ ] **Step 1: Scrivi i test (falliranno)**

`Tests/CatalogKeyTests.cs`:

```csharp
using DndCompanion.Services;

namespace DndCompanion.Tests;

public class CatalogKeyTests
{
    [Theory]
    [InlineData("Invisibilità", "invisibilita")]
    [InlineData("  Oscurità  ", "oscurita")]
    [InlineData("Velocità", "velocita")]
    [InlineData("Palla di Fuoco", "palla di fuoco")]
    [InlineData("PERCEZIONE", "percezione")]
    [InlineData("Éclair", "eclair")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeName_PiegaAccentiEMinuscole(string? input, string atteso)
        => Assert.Equal(atteso, CatalogKey.NormalizeName(input));

    // Il progetto compila con InvariantGlobalization=true: String.Normalize non decompone e non
    // solleva eccezioni. Questo test è la rete di sicurezza contro un ritorno a quell'API.
    [Fact]
    public void NormalizeName_NomiAccentatiDiversiSoloPerAccento_CollassanoSullaStessaChiave()
    {
        Assert.Equal(CatalogKey.NormalizeName("Invisibilità"), CatalogKey.NormalizeName("INVISIBILITA"));
    }

    [Fact]
    public void For_ConSourceId_UsaLIdentificatore()
        => Assert.Equal("srd-2024-it/elfo", CatalogKey.For("srd-2024-it/elfo", "Elfo"));

    [Fact]
    public void For_SenzaSourceId_UsaIlNomeNormalizzato()
        => Assert.Equal("elfo", CatalogKey.For(null, "Elfo"));

    [Fact]
    public void For_SourceIdVuoto_TrattatoComeAssente()
        => Assert.Equal("elfo", CatalogKey.For("   ", "Elfo"));

    [Theory]
    [InlineData("srd-2024-it/elfo", true)]
    [InlineData("mio-pacchetto/elfo", false)]
    [InlineData("srd-2024-it-altro/elfo", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsFromAppPackage_RiconosceIlPrefissoDelPacchettoDellApp(string? sourceId, bool atteso)
        => Assert.Equal(atteso, CatalogKey.IsFromAppPackage(sourceId));
}
```

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CatalogKeyTests`
Atteso: FALLIMENTO di compilazione — `CatalogKey` non esiste.

- [ ] **Step 3: Implementa `CatalogKey`**

`Services/CatalogKey.cs`:

```csharp
using System.Text;

namespace DndCompanion.Services;

/// <summary>Chiave di confronto fra voci di catalogo (§4.3 dello spec) e riconoscimento della
/// provenienza (§6). Logica pura.</summary>
public static class CatalogKey
{
    // Piega degli accenti scritta a mano: il progetto compila con InvariantGlobalization=true,
    // quindi non c'è ICU e String.Normalize non decompone nulla — senza sollevare eccezioni.
    // Copre l'insieme latino che serve all'italiano più i casi comuni di altre lingue.
    private static readonly Dictionary<char, char> AccentFolding = new()
    {
        ['à'] = 'a', ['á'] = 'a', ['â'] = 'a', ['ã'] = 'a', ['ä'] = 'a', ['å'] = 'a',
        ['è'] = 'e', ['é'] = 'e', ['ê'] = 'e', ['ë'] = 'e',
        ['ì'] = 'i', ['í'] = 'i', ['î'] = 'i', ['ï'] = 'i',
        ['ò'] = 'o', ['ó'] = 'o', ['ô'] = 'o', ['õ'] = 'o', ['ö'] = 'o',
        ['ù'] = 'u', ['ú'] = 'u', ['û'] = 'u', ['ü'] = 'u',
        ['ç'] = 'c', ['ñ'] = 'n', ['ý'] = 'y', ['ÿ'] = 'y',
    };

    /// <summary>Trim, minuscole e accenti piegati. Null o vuoto → stringa vuota.</summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var lowered = name.Trim().ToLowerInvariant();
        var sb = new StringBuilder(lowered.Length);
        foreach (var c in lowered)
            sb.Append(AccentFolding.TryGetValue(c, out var folded) ? folded : c);
        return sb.ToString();
    }

    /// <summary>Chiave di confronto: l'identificatore di provenienza se c'è, altrimenti il nome
    /// normalizzato.</summary>
    public static string For(string? sourceId, string? name)
        => string.IsNullOrWhiteSpace(sourceId) ? NormalizeName(name) : sourceId.Trim();

    /// <summary>Vero se la voce proviene dal pacchetto distribuito con l'app: è ciò che la rende
    /// di sola lettura (§6). Il confronto è sul prefisso "&lt;id pacchetto&gt;/".</summary>
    public static bool IsFromAppPackage(string? sourceId)
        => !string.IsNullOrWhiteSpace(sourceId)
           && sourceId.StartsWith(CatalogPackageParser.AppPackageId + "/", StringComparison.Ordinal);
}
```

- [ ] **Step 4: Esegui i test e verifica che passino**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CatalogKeyTests`
Atteso: tutti PASSATI (17 casi fra `Theory` e `Fact`).

- [ ] **Step 5: Commit**

```bash
git add Services/CatalogKey.cs Tests/CatalogKeyTests.cs
git commit -m "feat(pacchetti): chiave di confronto con piega accenti e riconoscimento provenienza"
```

---

### Task 3: Unione delle due sorgenti

**File:**
- Crea: `Services/CatalogMerge.cs`
- Test: `Tests/CatalogMergeTests.cs`

**Interfacce:**
- Consuma: `CatalogKey.For`, `CatalogKey.NormalizeName` (Task 2).
- Produce: `CatalogMerge.HiddenPackageIds(...)` e `CatalogMerge.Representative<T>(...)`.

- [ ] **Step 1: Scrivi i test (falliranno)**

`Tests/CatalogMergeTests.cs`:

```csharp
using DndCompanion.Services;

namespace DndCompanion.Tests;

public class CatalogMergeTests
{
    // Riga di database e voce di pacchetto, ridotte a ciò che serve al merge.
    private sealed record Row(string Id, string? SourceId, string Name);
    private sealed record Pkg(string Id, string Name);

    private static HashSet<string> Nascoste(IEnumerable<Pkg> pacchetto, IEnumerable<Row> db)
        => CatalogMerge.HiddenPackageIds(
            pacchetto, p => p.Id, p => p.Name,
            db, r => r.SourceId, r => r.Name);

    [Fact]
    public void HiddenPackageIds_RigaConLoStessoSourceId_OscuraLaVoceDiPacchetto()
    {
        var db = new[] { new Row("uuid-1", "srd-2024-it/elfo", "Elfo") };
        var pacchetto = new[] { new Pkg("srd-2024-it/elfo", "Elfo"), new Pkg("srd-2024-it/nano", "Nano") };

        var nascoste = Nascoste(pacchetto, db);

        Assert.Contains("srd-2024-it/elfo", nascoste);
        Assert.DoesNotContain("srd-2024-it/nano", nascoste);
    }

    [Fact]
    public void HiddenPackageIds_NessunaCorrispondenza_NonNascondeNulla()
    {
        var db = new[] { new Row("uuid-1", null, "Mezzorco") };
        var pacchetto = new[] { new Pkg("srd-2024-it/elfo", "Elfo") };

        Assert.Empty(Nascoste(pacchetto, db));
    }

    // Il caso che la pagina produce davvero: "duplica e modifica" crea una riga SENZA provenienza
    // e con lo stesso nome. Senza il confronto per nome l'utente vedrebbe due "Artigiano".
    [Fact]
    public void HiddenPackageIds_RigaSenzaProvenienzaMaOmonima_OscuraLaVoceDiPacchetto()
    {
        var db = new[] { new Row("uuid-1", null, "Artigiano") };
        var pacchetto = new[] { new Pkg("srd-2024-it/artigiano", "Artigiano") };

        Assert.Contains("srd-2024-it/artigiano", Nascoste(pacchetto, db));
    }

    // Le righe di database sono dati dell'utente: il merge non ne nasconde mai nessuna (§4.3).
    // Il rappresentante serve solo a decidere chi aggiorna un import e chi riusa la materializzazione.
    [Fact]
    public void Representative_PreferisceLaRigaSenzaSourceId()
    {
        var righe = new[]
        {
            new Row("uuid-a", "srd-2024-it/elfo", "Elfo"),
            new Row("uuid-b", null, "Elfo"),
        };

        var scelta = CatalogMerge.Representative(righe, r => r.SourceId, r => r.Id);

        Assert.Equal("uuid-b", scelta!.Id);
    }

    [Fact]
    public void Representative_APariMerito_PrendeLIdOrdinalmenteMinore()
    {
        var righe = new[]
        {
            new Row("uuid-b", null, "Elfo"),
            new Row("uuid-a", null, "Elfo"),
        };

        var scelta = CatalogMerge.Representative(righe, r => r.SourceId, r => r.Id);

        Assert.Equal("uuid-a", scelta!.Id);
    }

    [Fact]
    public void Representative_ListaVuota_RestituisceNull()
        => Assert.Null(CatalogMerge.Representative(
            Array.Empty<Row>(), r => r.SourceId, r => r.Id));

    [Fact]
    public void HiddenPackageIds_ConfrontoPerNomeIgnoraAccentiEMaiuscole()
    {
        var db = new[] { new Row("uuid-1", null, "  INVISIBILITA  ") };
        var pacchetto = new[] { new Pkg("srd-2024-it/invisibilita", "Invisibilità") };

        // Il confronto passa per il nome normalizzato, ma il risultato contiene l'ID della voce:
        // è quello che la pagina usa per filtrare.
        Assert.Contains("srd-2024-it/invisibilita", Nascoste(pacchetto, db));
    }
}
```

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CatalogMergeTests`
Atteso: FALLIMENTO di compilazione — `CatalogMerge` non esiste.

- [ ] **Step 3: Implementa `CatalogMerge`**

`Services/CatalogMerge.cs`:

```csharp
namespace DndCompanion.Services;

/// <summary>Unione fra le voci di un pacchetto e le righe di catalogo della campagna (§4.3, §6).
/// Logica pura.
///
/// Due principi, da non confondere:
/// 1. le righe di database sono dati dell'utente e restano SEMPRE tutte visibili;
/// 2. una voce di pacchetto viene oscurata se il database contiene già qualcosa che le corrisponde.
/// Il "rappresentante" non nasconde nulla: dice quale riga un import aggiorna e quale la
/// materializzazione riusa.</summary>
public static class CatalogMerge
{
    /// <summary>Gli id delle voci di pacchetto che il database già copre e che quindi non vanno
    /// mostrate.
    ///
    /// Una voce di pacchetto ha DUE chiavi — il suo id di provenienza e il suo nome — e va
    /// nascosta se il database ne contiene una qualsiasi delle due. Per questo la firma prende le
    /// voci intere e non un elenco di chiavi già calcolate: con una chiave sola il caso più
    /// frequente (una riga scritta a mano, o creata da "duplica e modifica", omonima di una voce
    /// di pacchetto) sfuggirebbe, e l'utente vedrebbe due volte la stessa cosa.</summary>
    public static HashSet<string> HiddenPackageIds<TPkg, TRow>(
        IEnumerable<TPkg> packageEntries,
        Func<TPkg, string> packageIdOf,
        Func<TPkg, string> packageNameOf,
        IEnumerable<TRow> dbRows,
        Func<TRow, string?> sourceIdOf,
        Func<TRow, string> nameOf)
    {
        var dbKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in dbRows)
        {
            dbKeys.Add(CatalogKey.For(sourceIdOf(row), nameOf(row)));
            // Una riga con provenienza copre anche la voce omonima: chi ha importato "Elfo" non
            // deve vederselo ricomparire perché il pacchetto lo identifica per nome.
            dbKeys.Add(CatalogKey.NormalizeName(nameOf(row)));
        }

        var hidden = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in packageEntries)
        {
            var id = packageIdOf(entry);
            if (dbKeys.Contains(id) || dbKeys.Contains(CatalogKey.NormalizeName(packageNameOf(entry))))
                hidden.Add(id);
        }
        return hidden;
    }

    /// <summary>Fra più righe con la stessa chiave, quella che le rappresenta: prima la riga
    /// senza provenienza (è una voce propria dell'utente, la più specifica), poi l'id
    /// ordinalmente minore — arbitrario, ma deterministico su tutti i cataloghi, perché
    /// `spells` e `monsters` non hanno `created_at`.
    ///
    /// NOTA: in Fase 1 non ha ancora chiamanti — serve a PackageImportPlan e alla
    /// materializzazione, entrambi di Fase 2. Nasce qui perché applica la stessa regola di
    /// precedenza di HiddenPackageIds e conviene fissarla e testarla in un colpo solo.</summary>
    public static T? Representative<T>(
        IEnumerable<T> rows,
        Func<T, string?> sourceIdOf,
        Func<T, string> idOf) where T : class
        => rows
            .OrderBy(r => string.IsNullOrWhiteSpace(sourceIdOf(r)) ? 0 : 1)
            .ThenBy(idOf, StringComparer.Ordinal)
            .FirstOrDefault();
}
```

- [ ] **Step 4: Esegui i test e verifica che passino**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CatalogMergeTests`
Atteso: 7 test PASSATI.

- [ ] **Step 5: Esegui l'intera suite**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: tutti verdi (i 220 preesistenti più quelli dei task 1-3).

- [ ] **Step 6: Commit**

```bash
git add Services/CatalogMerge.cs Tests/CatalogMergeTests.cs
git commit -m "feat(pacchetti): unione fra pacchetto e cataloghi di campagna"
```

---

### Task 4: Migrazione dello schema

**File:**
- Crea: `supabase/migrations/20260726000000_catalog_packages.sql`

**Interfacce:**
- Consuma: niente.
- Produce: tabella `backgrounds`; colonne `source_id` su `races`/`classes`/`spells`/`monsters`;
  `speed_unit` su `races`; `background_ability_choice` su `characters`; quattro vincoli `UNIQUE`.

> Tutto additivo. Nessuna riga esistente viene letta, riscritta o cancellata, e nessuna policy
> esistente viene modificata (§4.1 dello spec).

- [ ] **Step 1: Scrivi la migrazione**

`supabase/migrations/20260726000000_catalog_packages.sql`:

```sql
-- Modello 2024 + import dei dati — migrazioni additive.
-- Spec: docs/superpowers/specs/2026-07-25-modello-2024-import-dati-design.md
-- Nessuna migrazione di dati: personaggi e cataloghi esistenti restano intatti.
--
-- ATTENZIONE: si applica UNA SOLA VOLTA. Le colonne usano IF NOT EXISTS, ma vincoli e policy no
-- (PostgreSQL non lo prevede per ADD CONSTRAINT né per CREATE POLICY): rieseguire questo file su
-- un database dove è già passato fallisce a metà. Applicalo con `supabase db reset`, che riparte
-- da zero, oppure una sola volta a mano.

-- 1. Provenienza delle voci importate (§4.3).
ALTER TABLE "public"."races"    ADD COLUMN IF NOT EXISTS "source_id" text;
ALTER TABLE "public"."classes"  ADD COLUMN IF NOT EXISTS "source_id" text;
ALTER TABLE "public"."spells"   ADD COLUMN IF NOT EXISTS "source_id" text;
ALTER TABLE "public"."monsters" ADD COLUMN IF NOT EXISTS "source_id" text;

-- Una sola riga per provenienza in una campagna. Le righe digitate a mano hanno source_id NULL,
-- e in PostgreSQL più NULL non violano un UNIQUE: le righe esistenti non sono toccate.
ALTER TABLE "public"."races"
    ADD CONSTRAINT "races_campaign_source_key"    UNIQUE ("campaign_id", "source_id");
ALTER TABLE "public"."classes"
    ADD CONSTRAINT "classes_campaign_source_key"  UNIQUE ("campaign_id", "source_id");
ALTER TABLE "public"."spells"
    ADD CONSTRAINT "spells_campaign_source_key"   UNIQUE ("campaign_id", "source_id");
ALTER TABLE "public"."monsters"
    ADD CONSTRAINT "monsters_campaign_source_key" UNIQUE ("campaign_id", "source_id");

-- 2. Unità della velocità (§4.5). Default 'ft': le razze già inserite restano in piedi.
ALTER TABLE "public"."races"
    ADD COLUMN IF NOT EXISTS "speed_unit" text NOT NULL DEFAULT 'ft';

-- 3. Ripartizione dei bonus di background scelta dal giocatore (§4.7).
ALTER TABLE "public"."characters"
    ADD COLUMN IF NOT EXISTS "background_ability_choice" text;

-- 4. Catalogo dei background (§4.2).
CREATE TABLE IF NOT EXISTS "public"."backgrounds" (
    "id" uuid DEFAULT gen_random_uuid() NOT NULL,
    "name" text NOT NULL,
    "description" text,
    "ability_scores" text,
    "origin_feat" text,
    "skill_proficiencies" text,
    "tool_proficiency" text,
    "equipment" text,
    "source_id" text,
    "added_by" uuid,
    "campaign_id" uuid NOT NULL,
    "created_at" timestamp with time zone DEFAULT now(),
    CONSTRAINT "backgrounds_pkey" PRIMARY KEY ("id"),
    CONSTRAINT "backgrounds_campaign_source_key" UNIQUE ("campaign_id", "source_id")
);

ALTER TABLE "public"."backgrounds"
    ADD CONSTRAINT "backgrounds_campaign_id_fkey" FOREIGN KEY ("campaign_id")
    REFERENCES "public"."campaigns"("id") ON DELETE CASCADE;

ALTER TABLE "public"."backgrounds"
    ADD CONSTRAINT "backgrounds_added_by_fkey" FOREIGN KEY ("added_by")
    REFERENCES "auth"."users"("id") ON DELETE SET NULL;

ALTER TABLE "public"."backgrounds" ENABLE ROW LEVEL SECURITY;

-- Policy ricalcate su quelle di races: lettura ai membri della campagna, inserimento a qualunque
-- membro che si dichiari autore, modifica e cancellazione all'autore o al master.
CREATE POLICY "backgrounds_select" ON "public"."backgrounds"
    FOR SELECT USING ("public"."is_campaign_member"("campaign_id"));

CREATE POLICY "backgrounds_insert" ON "public"."backgrounds"
    FOR INSERT WITH CHECK (
        "added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id")
    );

-- La WITH CHECK non è decorativa: la USING dice quali righe si possono aggiornare, la WITH CHECK
-- vincola i valori risultanti. Senza, un membro potrebbe aggiornare una propria riga riscrivendo
-- campaign_id verso una campagna di cui non fa parte. races_update ha entrambe: ricalcarla vuol
-- dire copiarle tutte e due.
CREATE POLICY "backgrounds_update" ON "public"."backgrounds"
    FOR UPDATE USING (
        "added_by" = "auth"."uid"() OR "public"."is_campaign_master"("campaign_id")
    ) WITH CHECK (
        "added_by" = "auth"."uid"() OR "public"."is_campaign_master"("campaign_id")
    );

CREATE POLICY "backgrounds_delete" ON "public"."backgrounds"
    FOR DELETE USING (
        "added_by" = "auth"."uid"() OR "public"."is_campaign_master"("campaign_id")
    );
```

- [ ] **Step 2: Verifica le policy di `races` per confermare la ricalcatura**

Comando:
```bash
grep -n "races_select\|races_insert\|races_update\|races_delete" -A 4 supabase/migrations/20260624225146_remote_schema.sql
```
Atteso: le policy di `backgrounds` sopra hanno la stessa forma. Se differiscono (nomi degli helper,
condizioni), allinea la migrazione a ciò che il file mostra — quello è la verità, non questo piano.

- [ ] **Step 3: Applica la migrazione allo stack locale, se è in piedi**

Comando: `supabase db reset`
Atteso: la migrazione viene applicata senza errori. Se lo stack locale non è attivo
(`supabase start`), salta questo step e annota che la migrazione è da applicare a mano.

- [ ] **Step 4: Aggiungi gli scenari RLS alla suite d'integrazione**

Lo spec lo chiede due volte (§4.2 «le sue policy vanno scritte **e testate**», §10): `backgrounds` è
l'unica tabella nuova dell'intera fase, e la sicurezza server-side è il gate di pubblicazione
(§1 di `DA-FARE`).

In `Tests.Integration/`, ricalca gli scenari già presenti per gli altri cataloghi — apri i file
esistenti per la forma esatta di `SkippableFact` e del setup dei due account — e aggiungi almeno:

1. un membro legge i background della propria campagna;
2. un non-membro non ne vede nessuno;
3. un player non modifica il background creato da un altro membro;
4. il master modifica il background di chiunque nella sua campagna;
5. un membro non può spostare un proprio background in un'altra campagna (è il caso che la
   `WITH CHECK` protegge: senza di essa l'aggiornamento passerebbe).

Comando: `dotnet test Tests.Integration/`
Atteso: verdi con lo stack locale attivo, in **auto-skip** se non lo è.

- [ ] **Step 5: Commit**

```bash
git add supabase/migrations/20260726000000_catalog_packages.sql Tests.Integration/
git commit -m "feat(db): tabella backgrounds, provenienza delle voci e unità di velocità"
```

---

### Task 5: Model e repository dei background

**File:**
- Crea: `Models/Background.cs`
- Crea: `Services/Repositories/BackgroundRepository.cs`
- Modifica: `Models/Race.cs`, `Models/CharacterClass.cs`, `Models/Spell.cs`, `Models/Monster.cs`,
  `Models/Character.cs` (colonne nuove)
- Modifica: `Pages/Races.razor`, `Pages/Spells.razor`, `Pages/Classes.razor`, `Pages/Monsters.razor`,
  `Pages/Characters.razor` (i metodi `Clone*`, Step 3)
- Modifica: `Program.cs` (registrazione DI)

**Interfacce:**
- Consuma: niente dai task precedenti.
- Produce: `Background`; `IBackgroundRepository` con
  `GetBackgroundsForCampaignAsync(string campaignId)`, `CreateBackgroundAsync(Background)`,
  `UpdateBackgroundAsync(Background)`, `DeleteBackgroundAsync(string id)`;
  proprietà `SourceId` su `Race`/`CharacterClass`/`Spell`/`Monster`, `SpeedUnit` su `Race`,
  `BackgroundAbilityChoice` su `Character`.

> Senza le proprietà annotate, `From<T>` non vede le colonne nuove: sono invisibili, non "vuote".

- [ ] **Step 1: Crea il Model `Background`**

`Models/Background.cs`:

```csharp
using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

/// <summary>Background 2024: porta i punteggi di caratteristica, che nel 2014 stavano sulla specie.
/// La colonna elenca le TRE caratteristiche; la ripartizione la sceglie il giocatore (§4.2).</summary>
[Table("backgrounds")]
public class Background : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("ability_scores")]
    public string AbilityScores { get; set; } = string.Empty;

    [Column("origin_feat")]
    public string OriginFeat { get; set; } = string.Empty;

    [Column("skill_proficiencies")]
    public string SkillProficiencies { get; set; } = string.Empty;

    [Column("tool_proficiency")]
    public string ToolProficiency { get; set; } = string.Empty;

    [Column("equipment")]
    public string Equipment { get; set; } = string.Empty;

    [Column("source_id")]
    public string? SourceId { get; set; }

    [Column("added_by")]
    public string? AddedBy { get; set; }

    [Column("campaign_id")]
    public string CampaignId { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
```

- [ ] **Step 2: Aggiungi le colonne nuove ai Model esistenti**

In `Models/Race.cs`, dopo la proprietà `Languages`:

```csharp
    [Column("source_id")]
    public string? SourceId { get; set; }

    /// <summary>"ft" o "m". Default 'ft' lato database: le razze già inserite sono in piedi (§4.5).</summary>
    [Column("speed_unit")]
    public string SpeedUnit { get; set; } = "ft";
```

In `Models/CharacterClass.cs`, `Models/Spell.cs` e `Models/Monster.cs`, prima di `AddedBy`:

```csharp
    [Column("source_id")]
    public string? SourceId { get; set; }
```

In `Models/Character.cs`, accanto a `Background`:

```csharp
    /// <summary>Ripartizione dei bonus di background scelta dal giocatore (§4.7): serve a modifica
    /// e level-up, perché i punteggi sono salvati già sommati.</summary>
    [Column("background_ability_choice")]
    public string? BackgroundAbilityChoice { get; set; }
```

- [ ] **Step 3: Aggiorna le copie manuali dei Model**

Aggiungere una colonna al Model non basta: dove il codice copia un Model **campo per campo**, le
proprietà nuove restano ai valori di default e il salvataggio le riscrive.

`Pages/Races.razor` ha esattamente questo caso: `CloneRace` (righe ~396-413) alimenta il draft di
modifica, e `UpdateRaceAsync(editDraft)` lo salva. Senza intervento, aprire e salvare una razza
duplicata dal pacchetto la **converte da metri a piedi lasciando il numero invariato** (9 metri
diventano 9 piedi) e le **azzera `source_id`**, facendole perdere sola lettura e deduplica.

Non è un caso isolato: **ogni catalogo ha il suo `Clone*`**, e ognuno perde le colonne nuove.
Vanno corretti tutti e cinque.

| File | Metodo | Riga da aggiungere |
|---|---|---|
| `Pages/Races.razor` (~396) | `CloneRace` | `SourceId = r.SourceId, SpeedUnit = r.SpeedUnit,` |
| `Pages/Spells.razor` (~453) | `CloneSpell` | `SourceId = s.SourceId,` |
| `Pages/Classes.razor` (~410) | `CloneClass` | `SourceId = c.SourceId,` |
| `Pages/Monsters.razor` (~525) | `CloneMonster` | `SourceId = m.SourceId,` |
| `Pages/Characters.razor` (~490) | `CloneCharacter` | `BackgroundAbilityChoice = c.BackgroundAbilityChoice,` |

Adatta il nome del parametro a quello che il metodo usa davvero.

L'ultima riga è la più importante: `BackgroundAbilityChoice` è la colonna che §4.7 introduce
**apposta** perché la ripartizione scelta dal giocatore sopravviva — i punteggi sono salvati già
sommati e senza di essa non è ricostruibile. Se `CloneCharacter` non la copia, il form di modifica
la cancella al primo salvataggio, cioè distrugge il dato nel punto esatto in cui doveva proteggerlo.

Per controllare di non averne saltato nessuno:

```bash
grep -rn "private static .* Clone" Pages/ Shared/
```

Cerca i metodi di clonazione, non le espressioni `new Tipo {`: nel progetto i cloni usano il
`new()` target-typed (`private static Race CloneRace(Race r) => new()`), che una ricerca sul nome
del tipo non troverebbe mai.

- [ ] **Step 4: Crea il repository**

`Services/Repositories/BackgroundRepository.cs`:

```csharp
using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IBackgroundRepository
{
    Task<List<Background>> GetBackgroundsForCampaignAsync(string campaignId);
    Task<Background?> CreateBackgroundAsync(Background background);
    Task<Background?> UpdateBackgroundAsync(Background background);
    Task DeleteBackgroundAsync(string id);
}

/// <summary>Accesso dati per il catalogo background (tabella <c>backgrounds</c>).</summary>
public class BackgroundRepository : IBackgroundRepository
{
    private readonly SupabaseService _supabase;

    public BackgroundRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<Background>> GetBackgroundsForCampaignAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Background>()
            .Where(b => b.CampaignId == campaignId)
            .Get();
        return response.Models;
    }

    public async Task<Background?> CreateBackgroundAsync(Background background)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Background>().Insert(background);
        return response.Models.FirstOrDefault();
    }

    public async Task<Background?> UpdateBackgroundAsync(Background background)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Background>().Update(background);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteBackgroundAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<Background>().Where(b => b.Id == id).Delete();
    }
}
```

- [ ] **Step 5: Registra il repository nella DI**

In `Program.cs`, dopo `builder.Services.AddSingleton<ICampaignRepository, CampaignRepository>();`:

```csharp
builder.Services.AddSingleton<IBackgroundRepository, BackgroundRepository>();
```

- [ ] **Step 6: Verifica build e test**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori.

Comando: `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: tutti verdi.

- [ ] **Step 7: Commit**

```bash
# Percorsi espliciti, non `Pages/`: eseguendo i task in ordine diverso, una cartella intera
# raccoglierebbe anche il lavoro di altri task (per esempio Pages/Backgrounds.razor del Task 8).
git add Models/ Services/Repositories/BackgroundRepository.cs Program.cs \
        Pages/Races.razor Pages/Spells.razor Pages/Classes.razor \
        Pages/Monsters.razor Pages/Characters.razor
git commit -m "feat(background): model, repository e colonne di provenienza sui cataloghi"
```

---

### Task 6: Servizio dei cataloghi

**File:**
- Crea: `Services/CatalogService.cs`
- Modifica: `Program.cs`
- Test: `Tests/CatalogServiceTests.cs`

**Interfacce:**
- Consuma: `CatalogPackageParser.Parse` (Task 1), `CatalogMerge.HiddenPackageIds` (Task 3),
  `IBackgroundRepository` (Task 5).
- Produce: `CatalogView<TRow, TPkg>`; `ICatalogService` con `Task<CatalogPackage?> GetPackageAsync()`,
  `IReadOnlyList<PackageFeat> Feats { get; }` e
  `Task<CatalogView<Background, PackageBackground>> GetBackgroundsAsync(string campaignId)`.

> Il servizio legge il database **solo attraverso i repository**, mai con `From<T>` diretto, e il
> pacchetto via `HttpClient`. I `feats` vengono dal solo pacchetto: non hanno tabella (§6).
> **L'unione sta qui, non nelle pagine**: in Fase 2 gli altri quattro cataloghi aggiungono il proprio
> metodo accanto a `GetBackgroundsAsync`, invece di replicare la composizione in cinque `.razor`.

- [ ] **Step 1: Scrivi il test del caching (fallirà)**

`Tests/CatalogServiceTests.cs`:

```csharp
using System.Net;
using DndCompanion.Models;
using DndCompanion.Services;
using DndCompanion.Services.Repositories;

namespace DndCompanion.Tests;

public class CatalogServiceTests
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        private readonly string _payload;
        private readonly HttpStatusCode _status;

        public CountingHandler(string payload, HttpStatusCode status = HttpStatusCode.OK)
            => (_payload, _status) = (payload, status);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_payload)
            });
        }
    }

    // Repository finto: restituisce le righe che gli si passano, senza toccare la rete.
    private sealed class FakeBackgroundRepository : IBackgroundRepository
    {
        private readonly List<Background> _rows;
        public FakeBackgroundRepository(params Background[] rows) => _rows = rows.ToList();

        public Task<List<Background>> GetBackgroundsForCampaignAsync(string campaignId)
            => Task.FromResult(_rows);
        public Task<Background?> CreateBackgroundAsync(Background b) => Task.FromResult<Background?>(b);
        public Task<Background?> UpdateBackgroundAsync(Background b) => Task.FromResult<Background?>(b);
        public Task DeleteBackgroundAsync(string id) => Task.CompletedTask;
    }

    private const string Package = """
    {
      "schemaVersion": 1, "id": "srd-2024-it", "name": "SRD", "edition": "2024",
      "language": "it", "version": "1.0.0",
      "feats": [ { "id": "srd-2024-it/artigiano-talento", "name": "Artefice", "description": "…" } ],
      "backgrounds": [
        { "id": "srd-2024-it/artigiano", "name": "Artigiano" },
        { "id": "srd-2024-it/soldato", "name": "Soldato" }
      ]
    }
    """;

    private static CatalogService Service(
        CountingHandler handler, params Background[] dbRows)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://esempio.test/") },
               new FakeBackgroundRepository(dbRows));

    [Fact]
    public async Task GetPackageAsync_ScaricaUnaVoltaSola()
    {
        var handler = new CountingHandler(Package);
        var service = Service(handler);

        await service.GetPackageAsync();
        await service.GetPackageAsync();

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetPackageAsync_PacchettoAssente_RestituisceNullSenzaLanciare()
    {
        var service = Service(new CountingHandler("", HttpStatusCode.NotFound));

        Assert.Null(await service.GetPackageAsync());
    }

    // Un fallimento non deve disattivare il pacchetto per l'intera sessione: la rete può tornare.
    [Fact]
    public async Task GetPackageAsync_DopoUnFallimento_Riprova()
    {
        var handler = new CountingHandler("", HttpStatusCode.NotFound);
        var service = Service(handler);

        await service.GetPackageAsync();
        await service.GetPackageAsync();

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Feats_PrimaDelCaricamento_ListaVuota()
    {
        var service = Service(new CountingHandler(Package));

        Assert.Empty(service.Feats);

        await service.GetPackageAsync();

        Assert.Single(service.Feats);
    }

    [Fact]
    public async Task GetBackgroundsAsync_SenzaRigheDiDatabase_MostraTutteLeVociDiPacchetto()
    {
        var vista = await Service(new CountingHandler(Package)).GetBackgroundsAsync("campagna-1");

        Assert.Empty(vista.DbRows);
        Assert.Equal(2, vista.PackageEntries.Count);
    }

    [Fact]
    public async Task GetBackgroundsAsync_RigaOmonimaSenzaProvenienza_OscuraLaVoceDiPacchetto()
    {
        var riga = new Background { Id = "uuid-1", Name = "Artigiano", CampaignId = "campagna-1" };

        var vista = await Service(new CountingHandler(Package), riga).GetBackgroundsAsync("campagna-1");

        Assert.Single(vista.DbRows);
        Assert.Single(vista.PackageEntries);
        Assert.Equal("srd-2024-it/soldato", vista.PackageEntries[0].Id);
    }

    // La ragion d'essere di LastParse: un pacchetto rotto non deve assomigliare a un pacchetto
    // che non c'è. Senza questo test nulla fallirebbe se l'assegnazione sparisse.
    [Fact]
    public async Task LastParse_PacchettoMalformato_ConservaGliErroriENonRiprova()
    {
        var rotto = """{ "schemaVersion": 99, "id": "srd-2024-it", "name": "SRD" }""";
        var handler = new CountingHandler(rotto);
        var service = Service(handler);

        Assert.Null(await service.GetPackageAsync());
        Assert.NotNull(service.LastParse);
        Assert.NotEmpty(service.LastParse!.Errors);

        // Un file rotto non si aggiusta da solo: inutile riscaricarlo a ogni richiesta.
        await service.GetPackageAsync();
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task LastParse_PacchettoAssente_RestaNull()
    {
        var service = Service(new CountingHandler("", HttpStatusCode.NotFound));

        await service.GetPackageAsync();

        Assert.Null(service.LastParse);
    }

    [Fact]
    public async Task GetBackgroundsAsync_PacchettoAssente_RestituisceSoloLeRigheDiCampagna()
    {
        var riga = new Background { Id = "uuid-1", Name = "Mio background", CampaignId = "campagna-1" };
        var service = Service(new CountingHandler("", HttpStatusCode.NotFound), riga);

        var vista = await service.GetBackgroundsAsync("campagna-1");

        Assert.Single(vista.DbRows);
        Assert.Empty(vista.PackageEntries);
    }
}
```

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CatalogServiceTests`
Atteso: FALLIMENTO di compilazione — `CatalogService` non esiste.

- [ ] **Step 3: Implementa il servizio**

`Services/CatalogService.cs`:

```csharp
using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services.Repositories;

namespace DndCompanion.Services;

/// <summary>Un catalogo come lo vede la UI: le righe della campagna (tutte, sempre) più le voci
/// di pacchetto che nessuna di esse già copre.</summary>
public sealed record CatalogView<TRow, TPkg>(
    IReadOnlyList<TRow> DbRows,
    IReadOnlyList<TPkg> PackageEntries);

public interface ICatalogService
{
    /// <summary>Il pacchetto distribuito con l'app, scaricato al primo uso e tenuto in memoria.
    /// Null se assente o illeggibile: l'app funziona lo stesso, con i soli dati di campagna.</summary>
    Task<CatalogPackage?> GetPackageAsync();

    /// <summary>Talenti del solo pacchetto: non hanno tabella, quindi non c'è nulla da unire (§6).
    /// Vuota finché il pacchetto non è stato caricato.</summary>
    IReadOnlyList<PackageFeat> Feats { get; }

    /// <summary>Esito dell'ultima lettura del pacchetto: distingue un pacchetto **malformato**
    /// (valorizzato, con gli errori dentro) da uno **assente** (resta null, perché non c'è stato
    /// nulla da leggere). Null finché una lettura non è andata a buon fine — un fallimento di
    /// rete o un 404 non lo valorizzano.</summary>
    ParseResult? LastParse { get; }

    /// <summary>Background della campagna uniti alle voci di pacchetto non già coperte (§6).</summary>
    Task<CatalogView<Background, PackageBackground>> GetBackgroundsAsync(string campaignId);
}

/// <summary>Unione fra il pacchetto dell'app e i cataloghi di campagna (§6 dello spec).
/// Legge il database SOLO attraverso i repository, mai con From&lt;T&gt; diretto; il pacchetto
/// arriva via HttpClient.
///
/// La composizione sta qui e non nelle pagine: in Fase 2 gli altri quattro cataloghi
/// aggiungeranno il proprio metodo accanto a GetBackgroundsAsync, invece di replicare
/// l'orchestrazione in cinque .razor.</summary>
public class CatalogService : ICatalogService
{
    /// <summary>Percorso relativo alla base dell'app: funziona sia in locale sia sotto il
    /// sottopercorso di GitHub Pages.</summary>
    private const string PackagePath = "data/srd-2024-it.json";

    private readonly HttpClient _http;
    private readonly IBackgroundRepository _backgrounds;
    private CatalogPackage? _package;
    private bool _loaded;

    public CatalogService(HttpClient http, IBackgroundRepository backgrounds)
        => (_http, _backgrounds) = (http, backgrounds);

    public IReadOnlyList<PackageFeat> Feats
        => _package?.Feats ?? (IReadOnlyList<PackageFeat>)Array.Empty<PackageFeat>();

    public ParseResult? LastParse { get; private set; }

    public async Task<CatalogPackage?> GetPackageAsync()
    {
        // Si ricorda solo il successo: un fallimento di rete non deve disattivare il pacchetto
        // per tutta la sessione, perché la rete può tornare.
        if (_loaded) return _package;

        try
        {
            var response = await _http.GetAsync(PackagePath);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();

            // Si conserva l'esito completo, non solo il pacchetto: senza errori e avvisi, un
            // pacchetto MALFORMATO diventa indistinguibile da uno ASSENTE, e in Fase 3 un errore
            // di traduzione si manifesterebbe come "cataloghi senza voci di manuale", senza un
            // appiglio per capire perché. La schermata di import di Fase 2 li mostrerà.
            LastParse = CatalogPackageParser.Parse(json);
            _package = LastParse.Package;
            _loaded = true;
        }
        catch (HttpRequestException)
        {
            // Nessun pacchetto: l'app resta utilizzabile con i soli dati di campagna.
            return null;
        }

        return _package;
    }

    public async Task<CatalogView<Background, PackageBackground>> GetBackgroundsAsync(string campaignId)
    {
        var dbRows = await _backgrounds.GetBackgroundsForCampaignAsync(campaignId);
        var package = await GetPackageAsync();

        if (package is null)
            return new CatalogView<Background, PackageBackground>(dbRows, Array.Empty<PackageBackground>());

        var nascoste = CatalogMerge.HiddenPackageIds(
            package.Backgrounds, p => p.Id, p => p.Name,
            dbRows, r => r.SourceId, r => r.Name);

        var visibili = package.Backgrounds.Where(b => !nascoste.Contains(b.Id)).ToList();
        return new CatalogView<Background, PackageBackground>(dbRows, visibili);
    }
}
```

- [ ] **Step 4: Registra il servizio nella DI**

In `Program.cs`, accanto agli altri servizi:

```csharp
// Scoped, NON Singleton: dipende da HttpClient, che Program.cs registra AddScoped, e un singleton
// che cattura uno scoped lo tiene per sempre — l'accoppiamento sbagliato, oltre a far fallire la
// risoluzione ovunque la validazione degli scope sia attiva. In WebAssembly lo scope è uno solo
// per tutta l'app, quindi la cache in memoria del pacchetto vale comunque per l'intera sessione.
builder.Services.AddScoped<ICatalogService, CatalogService>();
```

- [ ] **Step 5: Esegui i test e verifica che passino**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter CatalogServiceTests`
Atteso: 9 test PASSATI.

- [ ] **Step 6: Commit**

```bash
git add Services/CatalogService.cs Tests/CatalogServiceTests.cs Program.cs
git commit -m "feat(pacchetti): servizio di caricamento del pacchetto dell'app"
```

---

### Task 7: Escludere il pacchetto dal precache

**File:**
- Modifica: `wwwroot/service-worker.published.js:21` (`offlineAssetsExclude`) e `onFetch` (~riga 67)

**Interfacce:**
- Consuma: il percorso `data/srd-2024-it.json` fissato in Task 6.
- Produce: nessuna API.

> `offlineAssetsInclude` cattura ogni `.json` e `onInstall` li scarica tutti con un solo
> `cache.addAll`, che è **atomico**: lasciando il pacchetto nel manifest, ogni utente lo
> scaricherebbe all'installazione e un solo fetch fallito farebbe fallire l'installazione,
> facendo perdere l'offline all'intera app (§6).

- [ ] **Step 1: Aggiungi il pacchetto alle esclusioni**

In `wwwroot/service-worker.published.js`, sostituisci la riga di **`offlineAssetsExclude`** — è la
**21**, non la 20: la 20 è `offlineAssetsInclude`, e cancellarla fa fallire l'installazione del
service worker con `ReferenceError`, facendo perdere all'app sia l'offline sia il banner di
aggiornamento — con queste tre:

```javascript
// I pacchetti dati (data/*.json) restano FUORI dal precache: sono grandi, servono solo a chi apre
// i cataloghi, e cache.addAll è atomico — un loro fetch fallito farebbe fallire l'installazione
// del service worker, con l'app che perde l'offline. Vengono messi in cache alla prima richiesta
// riuscita, dal ramo dedicato in onFetch.
const offlineAssetsExclude = [ /^service-worker\.js$/, /^data\/.*\.json$/ ];
const dataPackagePattern = /\/data\/[^/]+\.json$/;
```

- [ ] **Step 2: Aggiungi il ramo che li mette in cache all'uso**

Senza questo, l'esclusione **toglie** l'offline invece di rimandarlo: `onFetch` oggi legge dalla
cache ma non ci scrive mai, quindi un asset fuori dal precache non finisce in Cache API né alla
prima richiesta né mai.

In `wwwroot/service-worker.published.js`, dentro `onFetch`, subito dopo
`cachedResponse = await cache.match(request);`:

```javascript
        // Cache al primo uso per i pacchetti dati: è ciò che rende il pacchetto consultabile
        // offline dopo averlo aperto una volta (§6 dello spec).
        if (!cachedResponse && dataPackagePattern.test(new URL(event.request.url).pathname)) {
            const response = await fetch(event.request);
            if (response.ok) {
                await cache.put(event.request, response.clone());
            }
            return response;
        }
```

- [ ] **Step 3: Verifica che la pubblicazione non includa il pacchetto nel manifest**

Comando: `dotnet publish -c Release -o /tmp/pub-check`
Poi: `grep -c "srd-2024-it" /tmp/pub-check/wwwroot/service-worker-assets.js || echo "assente"`

Atteso: in questa fase il file `data/srd-2024-it.json` non esiste ancora, quindi il conteggio è
`assente`. Il controllo diventa significativo quando il pacchetto verrà aggiunto (Fase 3): l'esclusione
è già in posizione perché nessuno se ne dimentichi allora.

Verifica anche di non aver toccato la lista sbagliata:

```bash
grep -c "offlineAssetsInclude\|offlineAssetsExclude" wwwroot/service-worker.published.js
```
Atteso: **4** — due dichiarazioni e due usi dentro `onInstall`. Un 2 o un 3 significa che una delle
due costanti è sparita, e il service worker non installerà più.

- [ ] **Step 4: Commit**

```bash
git add wwwroot/service-worker.published.js
git commit -m "fix(pwa): escludi i pacchetti dati dal precache del service worker"
```

---

### Task 8: Pagina Background

**File:**
- Crea: `Pages/Backgrounds.razor`
- Crea: `Pages/Backgrounds.razor.css`
- Modifica: `Pages/Home.razor` (card di navigazione)

**Interfacce:**
- Consuma: `IBackgroundRepository` (Task 5), `ICatalogService` (Task 6), `CatalogKey.IsFromAppPackage`
  (Task 2), `CurrentUserService`, `AccessControl.CanEdit`.
- Produce: rotta `/backgrounds`.

> Senza questa pagina i background esisterebbero solo via import, e chi non carica un pacchetto
> troverebbe un elenco vuoto proprio nel passo del wizard che concede i bonus (§8).

- [ ] **Step 1: Studia una pagina di catalogo esistente**

Comando: `cat Pages/Races.razor`

Osserva e riusa: come si carica lo stato con `CurrentUserService.EnsureLoadedAsync()`, il FAB "+"
con `aria-label`, `<LoadingSpinner>`, `DbErrorBanner`, i toast di validazione, `ConfirmService` per
le cancellazioni, le classi CSS delle card. **Non inventare pattern nuovi.**

- [ ] **Step 2: Scrivi la pagina**

`Pages/Backgrounds.razor` deve avere:

- `@page "/backgrounds"`, le `@using DndCompanion.Models` e `@using DndCompanion.Models.Packages`
  (**servono entrambe**: `_Imports.razor` non le contiene, ogni pagina le dichiara per sé — vedi
  `Pages/Races.razor:2`), e le `@inject` di `IBackgroundRepository`, `ICatalogService`,
  `CurrentUserService`, `ToastService`, `ConfirmService`;
- caricamento in `OnInitializedAsync`: `await CurrentUser.EnsureLoadedAsync()`, poi
  `Catalog.GetBackgroundsAsync(...)`, che restituisce già l'unione — la composizione sta nel
  servizio, non qui;
- ogni voce mostra nome, descrizione, le tre caratteristiche, il talento d'origine e — per le voci
  di pacchetto — il testo del talento preso da `Catalog.Feats` confrontando il nome;
- **comandi**: le voci con `CatalogKey.IsFromAppPackage(b.SourceId) == true` e quelle di pacchetto non
  hanno matita né cestino; al loro posto un pulsante "Duplica e modifica" che crea una copia
  **senza `SourceId`**. Le altre righe seguono `AccessControl.CanEdit(isMaster, b.AddedBy, userId)`;
- form di creazione/modifica con i campi del Model, validazione del solo nome obbligatorio
  (messaggio via `Toasts.ShowError`, non nel banner);
- **dopo ogni creazione, modifica o cancellazione, richiama `Catalog.GetBackgroundsAsync(...)`**
  invece di ritoccare `dbRows` in memoria come fanno le pagine esistenti. Qui la lista non è solo
  di righe: contiene anche le voci di pacchetto, e quali siano visibili dipende da cosa c'è nel
  database. Aggiornando solo `dbRows`, subito dopo "Duplica e modifica" l'utente vedrebbe la propria
  copia **accanto** alla voce da cui deriva, e il doppione sparirebbe solo cambiando pagina;
- `aria-label` su tutti i pulsanti icona-pura e attivazione da tastiera sui controlli interattivi.

- [ ] **Step 3: Usa questa logica nel blocco `@code`**

Il markup ricalca Razze, ma la parte che decide *cosa mostrare* e *cosa è modificabile* è nuova ed è
dove si sbaglia. Scrivila così:

```csharp
@code {
    private List<Background> dbRows = new();
    private List<PackageBackground> packageRows = new();
    private bool loading = true;
    private string? systemError;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await CurrentUser.EnsureLoadedAsync();

            // Stessa guardia di Pages/Races.razor: senza campagna non c'è nulla da caricare.
            if (string.IsNullOrEmpty(CurrentUser.CampaignId)) return;

            var vista = await Catalog.GetBackgroundsAsync(CurrentUser.CampaignId);
            dbRows = vista.DbRows.ToList();
            packageRows = vista.PackageEntries.ToList();
        }
        catch (Exception ex)
        {
            systemError = ex.Message;
        }
        finally
        {
            loading = false;
        }
    }

    // Una riga di database è modificabile se chi guarda ne ha il diritto E non viene dal
    // pacchetto dell'app: quelle restano voci di manuale anche se vivono nel database (§6).
    private bool PuoModificare(Background b)
        => !CatalogKey.IsFromAppPackage(b.SourceId)
           && AccessControl.CanEdit(CurrentUser.IsMaster, b.AddedBy, CurrentUser.UserId);

    // Copia modificabile di una voce di pacchetto: nasce SENZA SourceId, così è una voce
    // propria dell'utente e ha la precedenza nel merge (§4.3).
    private Background DuplicaDaPacchetto(PackageBackground p) => new()
    {
        Name = p.Name,
        Description = p.Description,
        AbilityScores = string.Join(", ", p.AbilityScores),
        OriginFeat = p.OriginFeat,
        SkillProficiencies = string.Join(", ", p.SkillProficiencies),
        ToolProficiency = p.ToolProficiency,
        Equipment = p.Equipment,
        SourceId = null,
        // Il ?? non è pleonastico: CampaignId è string? e il progetto ha Nullable=enable, quindi
        // senza fallback è CS8601 — cioè un warning, contro il vincolo "0 warning" di questa fase.
        CampaignId = CurrentUser.CampaignId ?? string.Empty,
        AddedBy = CurrentUser.UserId,
    };

    // Testo del talento d'origine, se il pacchetto lo contiene: senza, il novizio leggerebbe
    // solo un nome (§5).
    private string? TestoTalento(string? nomeTalento)
        => string.IsNullOrWhiteSpace(nomeTalento)
            ? null
            : Catalog.Feats.FirstOrDefault(f =>
                CatalogKey.NormalizeName(f.Name) == CatalogKey.NormalizeName(nomeTalento))?.Description;
}
```

- [ ] **Step 4: Scrivi il CSS isolato**

`Pages/Backgrounds.razor.css`: replica le classi della pagina Razze. **Usa solo i token di `:root`**
(`var(--...)`), mai literal esadecimali. Aggiungi una classe per la marcatura delle voci di pacchetto,
per esempio un bordo con `var(--gold-dim)` e un'etichetta testuale "dal manuale".

- [ ] **Step 5: Aggiungi la card in Home**

In `Pages/Home.razor`, accanto alle altre card di catalogo, una voce che porta a `/backgrounds` con
la stessa struttura delle esistenti (icona, titolo, descrizione breve).

- [ ] **Step 6: Verifica a mano**

Comando: `dotnet run`
Verifica: `/backgrounds` si apre, l'elenco vuoto mostra l'empty state, si crea un background e
compare, il pulsante di modifica appare solo dove deve, la Home ha la card nuova.

- [ ] **Step 7: Verifica build**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori.

- [ ] **Step 8: Commit**

```bash
git add Pages/Backgrounds.razor Pages/Backgrounds.razor.css Pages/Home.razor
git commit -m "feat(background): pagina di catalogo con voci di pacchetto in sola lettura"
```

---

### Task 9: Unità di velocità visibile nel form Razze

**File:**
- Modifica: `Pages/Races.razor:46-48` (campo velocità)
- Modifica: `Services/FormValidation.cs:27-32` (`ValidateRace`)
- Test: `Tests/FormValidationTests.cs` (casi nuovi)

**Interfacce:**
- Consuma: `Race.SpeedUnit` (Task 5).
- Produce: `FormValidation.ValidateRace` che valida l'intervallo in base all'unità.

> "Duplica e modifica" crea già righe in metri: il form deve saperlo da subito, altrimenti mostra
> "9" con un limite pensato per i piedi (§4.5).

- [ ] **Step 1: Scrivi i test nuovi (falliranno)**

Aggiungi a `Tests/FormValidationTests.cs`:

```csharp
    [Fact]
    public void ValidateRace_VelocitaInMetriDentroIlLimite_Valida()
    {
        var r = new Race { Name = "Elfo", Speed = 9, SpeedUnit = "m" };
        Assert.Null(FormValidation.ValidateRace(r));
    }

    [Fact]
    public void ValidateRace_VelocitaInMetriOltreIlLimite_Rifiutata()
    {
        var r = new Race { Name = "Elfo", Speed = 40, SpeedUnit = "m" };
        Assert.NotNull(FormValidation.ValidateRace(r));
    }

    [Fact]
    public void ValidateRace_VelocitaInPiediDentroIlLimite_Valida()
    {
        var r = new Race { Name = "Elfo", Speed = 30, SpeedUnit = "ft" };
        Assert.Null(FormValidation.ValidateRace(r));
    }

    [Fact]
    public void ValidateRace_MessaggioCitaLUnita()
    {
        var r = new Race { Name = "Elfo", Speed = 999, SpeedUnit = "m" };
        // Il `!` serve: ValidateRace ritorna string? e il progetto di test ha Nullable=enable.
        Assert.Contains("metri", FormValidation.ValidateRace(r)!);
    }
```

- [ ] **Step 2: Aggiorna il test esistente sul messaggio**

Il messaggio cambia, e c'è già un test che lo asserisce **alla lettera**. Trovalo:

```bash
grep -n "La velocità deve essere tra 0 e 120" Tests/FormValidationTests.cs
```

È una `Theory` con `[InlineData(-1)]` e `[InlineData(121)]` che confronta la stringa esatta.
Aggiorna l'atteso a `"La velocità deve essere tra 0 e 120 piedi"`: il default di `SpeedUnit` è
`"ft"`, quindi quei due casi restano sul ramo dei piedi.

Senza questo step la suite va in rosso proprio nel task che chiude la fase, e la tentazione è
ritoccare l'implementazione a caso.

- [ ] **Step 3: Esegui i test e verifica quali falliscono**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter FormValidationTests`
Atteso: **due** dei quattro test nuovi FALLISCONO — `VelocitaInMetriOltreIlLimite_Rifiutata`
(Speed 40 è dentro 0–120 con il limite attuale) e `MessaggioCitaLUnita`. Gli altri due passano già,
perché 9 e 30 sono validi anche col limite fisso: è normale, non un errore.

- [ ] **Step 4: Aggiorna `ValidateRace`**

In `Services/FormValidation.cs`, sostituisci il metodo:

```csharp
    internal static string? ValidateRace(Race r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Il nome è obbligatorio";

        // Il limite dipende dall'unità: 120 piedi ≈ 36 metri (§4.5 dello spec).
        var inMetri = string.Equals(r.SpeedUnit, "m", StringComparison.OrdinalIgnoreCase);
        var max = inMetri ? 36 : 120;
        var unita = inMetri ? "metri" : "piedi";
        if (!InRange(r.Speed, 0, max))
            return $"La velocità deve essere tra 0 e {max} {unita}";

        return null;
    }
```

- [ ] **Step 5: Esegui i test e verifica che passino**

Comando: `dotnet test Tests/DndCompanion.Tests.csproj --filter FormValidationTests`
Atteso: **21 test PASSATI** — i 17 preesistenti (con l'atteso aggiornato allo Step 2; sono 17 e non
11 perché ogni `InlineData` di una `Theory` conta come un caso) più i 4 nuovi.

- [ ] **Step 6: Mostra l'unità nel form**

In `Pages/Races.razor`, accanto al campo velocità aggiungi un `<select>` legato a
**`editDraft.SpeedUnit`** — è così che si chiama il campo nella pagina (righe ~235, 308, 319, 327),
non `draft` — con le due opzioni `m` ("metri") e `ft` ("piedi") e un `aria-label="Unità di velocità"`.

Aggiorna anche il `max` dell'input numerico (riga ~48), oggi fisso a `120`: con l'unità in metri il
browser lascerebbe digitare fino a 120 mentre la validazione rifiuta oltre 36, e l'utente scoprirebbe
il limite solo dal toast.

```razor
max="@(editDraft.SpeedUnit == "m" ? 36 : 120)"
```

Nelle card dell'elenco, mostra l'unità accanto al numero:
`@race.Speed @(race.SpeedUnit == "m" ? "m" : "ft")`.

- [ ] **Step 7: Verifica finale**

Comando: `dotnet build`
Atteso: 0 warning, 0 errori.

Comando: `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: tutti verdi.

- [ ] **Step 8: Commit**

```bash
git add Pages/Races.razor Services/FormValidation.cs Tests/FormValidationTests.cs
git commit -m "feat(razze): unità di velocità esplicita nel form e nella validazione"
```

---

## Al termine della fase

Aggiorna `docs/DA-FARE.md` (§8-bis: fase 1 completata) e `docs/DIARIO.md` con cosa è stato fatto e
perché, poi lancia il gate a due agenti (`critico` e `conformità`) sul diff complessivo, come prescrive
`CLAUDE.md`.

## Fasi successive

- **Fase 2 — import ed export**: `PackageImportPlan` con il gate dei permessi, schermata di import
  con anteprima, export della campagna, rimozione per provenienza, materializzazione degli incantesimi
  su uso (`Upsert` con `on_conflict`), adeguamento del filtro per classe (§4.6).
  Qui rientra anche la **marcatura delle voci di pacchetto nei quattro cataloghi esistenti** (Razze,
  Classi, Incantesimi, Mostri) con il relativo "duplica e modifica", che il §12 punto 3 dello spec
  elenca insieme alla pagina Background: in Fase 1 quelle pagine non hanno ancora nulla da marcare,
  perché senza import né pacchetto pubblicato non esistono righe con provenienza. La logica che serve
  (`CatalogMerge`, `CatalogKey.IsFromAppPackage`) è però già pronta e testata dalla Fase 1, e il
  blocco `@code` del Task 8 è il modello da replicare.
- **Fase 3 — contenuto e wizard 2024**: campione SRD per validare il formato sul campo, traduzione del
  pacchetto completo, wizard che prende i bonus dal background con ripartizione, tetto di 20 e
  convivenza con le specie legacy (§4.7).
