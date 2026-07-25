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

    // Regressione (revisione finale Fase 1): NormalizeLists proteggeva solo le sei sezioni di primo
    // livello. Un background con "abilityScores": null superava il parser e poi faceva lanciare
    // ArgumentNullException a string.Join(...) in Pages/Backgrounds.razor, FUORI dal try/catch di
    // OnInitializedAsync (errore fatale di Blazor, non un errore gestito).
    [Fact]
    public void Parse_ListaAnnidataNullaInBackground_NonLanciaEProducePacchettoConListeVuote()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "id": "srd-2024-it",
          "name": "SRD 5.2 — Italiano",
          "edition": "2024",
          "language": "it",
          "version": "1.0.0",
          "backgrounds": [
            { "id": "srd-2024-it/soldato", "name": "Soldato", "abilityScores": null, "skillProficiencies": null }
          ]
        }
        """;

        var result = CatalogPackageParser.Parse(json);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Package);
        Assert.Empty(result.Package!.Backgrounds[0].AbilityScores);
        Assert.Empty(result.Package.Backgrounds[0].SkillProficiencies);
    }

    // Stessa regressione, sulle liste annidate di PackageClass (SavingThrows, Levels, SkillChoices.From)
    // e, dentro ogni livello, PackageClassLevel (Features, SpellSlots).
    [Fact]
    public void Parse_ListeAnnidateNulleInClassiELivelli_NonLanciaEProducePacchettoConListeVuote()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "id": "srd-2024-it",
          "name": "SRD 5.2 — Italiano",
          "edition": "2024",
          "language": "it",
          "version": "1.0.0",
          "classes": [
            { "id": "srd-2024-it/guerriero", "name": "Guerriero", "savingThrows": null,
              "skillChoices": { "count": 2, "from": null },
              "levels": [ { "level": 1, "features": null, "spellSlots": null } ] }
          ]
        }
        """;

        var result = CatalogPackageParser.Parse(json);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Package);
        Assert.Empty(result.Package!.Classes[0].SavingThrows);
        Assert.Empty(result.Package.Classes[0].SkillChoices!.From);
        Assert.Empty(result.Package.Classes[0].Levels[0].Features);
        Assert.Empty(result.Package.Classes[0].Levels[0].SpellSlots);
    }

    // Stessa regressione, ma con "levels": null (invece di una lista di livelli con contenuto nullo).
    [Fact]
    public void Parse_ListaLivelliNullaInClasse_NonLanciaEProducePacchettoConListaVuota()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "id": "srd-2024-it",
          "name": "SRD 5.2 — Italiano",
          "edition": "2024",
          "language": "it",
          "version": "1.0.0",
          "classes": [
            { "id": "srd-2024-it/guerriero", "name": "Guerriero", "levels": null }
          ]
        }
        """;

        var result = CatalogPackageParser.Parse(json);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Package);
        Assert.Empty(result.Package!.Classes[0].Levels);
    }

    // Stessa regressione, sulla lista annidata PackageSpell.Classes.
    [Fact]
    public void Parse_ListaClassiNullaInIncantesimo_NonLanciaEProducePacchettoConListaVuota()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "id": "srd-2024-it",
          "name": "SRD 5.2 — Italiano",
          "edition": "2024",
          "language": "it",
          "version": "1.0.0",
          "spells": [
            { "id": "srd-2024-it/palla-di-fuoco", "name": "Palla di Fuoco", "classes": null }
          ]
        }
        """;

        var result = CatalogPackageParser.Parse(json);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Package);
        Assert.Empty(result.Package!.Spells[0].Classes);
    }

    // Il confine (parser) fa il trim di id e nomi: senza, un pacchetto con spazi accidentali
    // romperebbe l'asimmetria con CatalogKey.For, che fa già il trim del sourceId letto dal database.
    [Fact]
    public void Parse_IdENomeConSpaziAiMargini_VengonoTrimmati()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "id": "  srd-2024-it  ",
          "name": "SRD 5.2 — Italiano",
          "edition": "2024",
          "language": "it",
          "version": "1.0.0",
          "species": [
            { "id": " srd-2024-it/elfo ", "name": " Elfo ", "size": "Media",
              "speed": { "value": 9, "unit": "m" } }
          ]
        }
        """;

        var result = CatalogPackageParser.Parse(json);

        Assert.NotNull(result.Package);
        Assert.Equal("srd-2024-it", result.Package!.Id);
        Assert.Equal("srd-2024-it/elfo", result.Package.Species[0].Id);
        Assert.Equal("Elfo", result.Package.Species[0].Name);
    }

    // Il database impone UNIQUE (campaign_id, source_id): due voci con lo stesso id nella stessa
    // sezione devono essere respinte subito, con l'indicazione della voce colpevole, invece di
    // arrivare in Fase 2 e far fallire l'import a metà.
    [Fact]
    public void Parse_IdentificatoreDuplicatoNellaStessaSezione_RestituisceErroreConLaVoceColpevole()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "id": "srd-2024-it",
          "name": "SRD 5.2 — Italiano",
          "edition": "2024",
          "language": "it",
          "version": "1.0.0",
          "species": [
            { "id": "srd-2024-it/elfo", "name": "Elfo", "size": "Media",
              "speed": { "value": 9, "unit": "m" } },
            { "id": "srd-2024-it/elfo", "name": "Elfo Alto", "size": "Media",
              "speed": { "value": 9, "unit": "m" } }
          ]
        }
        """;

        var result = CatalogPackageParser.Parse(json);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("srd-2024-it/elfo") && e.Contains("Elfo Alto"));
    }
}
