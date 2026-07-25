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

    [Fact]
    public void Parse_IdPacchettoMancante_RestituisceErrore()
    {
        var json = ValidJson.Replace("\"id\": \"srd-2024-it\",", "");

        var result = CatalogPackageParser.Parse(json);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("identificatore"));
    }

    [Fact]
    public void Parse_JsonLetteraleNull_RestituisceErrore()
    {
        var result = CatalogPackageParser.Parse("null");

        Assert.Null(result.Package);
        Assert.NotEmpty(result.Errors);
    }

    // Regressione: una sezione esplicitamente "null" (es. un esportatore che scrive `null`
    // invece di `[]` per una sezione vuota) sovrascriveva il default `= new()` del modello e
    // faceva lanciare ArgumentNullException a Select(...) invece di restituire un ParseResult.
    [Fact]
    public void Parse_SezioneNulla_NonLanciaEProducePacchettoSenzaQuellaSezione()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "id": "srd-2024-it",
          "name": "SRD 5.2 — Italiano",
          "edition": "2024",
          "language": "it",
          "version": "1.0.0",
          "species": null
        }
        """;

        var result = CatalogPackageParser.Parse(json);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Package);
        Assert.Empty(result.Package!.Species);
    }

    // Regressione: un elemento null dentro un array altrimenti valido (es. "species": [null])
    // faceva lanciare NullReferenceException invece di segnalare la voce come priva di id/nome.
    [Fact]
    public void Parse_ElementoNulloNellaLista_SegnalaErroreSenzaLanciare()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "id": "srd-2024-it",
          "name": "SRD 5.2 — Italiano",
          "edition": "2024",
          "language": "it",
          "version": "1.0.0",
          "species": [ null ]
        }
        """;

        var result = CatalogPackageParser.Parse(json);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("specie"));
    }
}
