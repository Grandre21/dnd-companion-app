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

        NormalizeLists(package);

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

    // System.Text.Json non impone a runtime la non-nullabilità di C#: un JSON con una sezione
    // esplicitamente "null" (es. "species": null) sovrascrive il default `= new()` del modello.
    // Ripristina qui l'invariante "le sei liste non sono mai null" prima di iterarle, così un
    // pacchetto con sezioni assenti/nulle produce un pacchetto senza quelle voci, non un crash.
    private static void NormalizeLists(CatalogPackage p)
    {
        p.Species ??= new();
        p.Backgrounds ??= new();
        p.Feats ??= new();
        p.Classes ??= new();
        p.Spells ??= new();
        p.Monsters ??= new();
    }

    // Ogni voce deve avere id e nome: senza id non sopravvive all'import (§4.3),
    // senza nome non è confrontabile. L'errore cita il nome, o la posizione se manca anche quello.
    // Un elemento null nell'array (es. "species": [null]) è trattato come voce senza id e senza
    // nome, invece di far crashare la lettura del pacchetto.
    private static void ValidateEntries(CatalogPackage p, List<string> errors)
    {
        Check(p.Species.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "specie", errors);
        Check(p.Backgrounds.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "background", errors);
        Check(p.Feats.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "talenti", errors);
        Check(p.Classes.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "classi", errors);
        Check(p.Spells.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "incantesimi", errors);
        Check(p.Monsters.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "mostri", errors);
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
