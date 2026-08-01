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
        var result = CatalogPackageParser.Parse(ValidJson, èIlManualeDellApp: true);

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

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("99"));
    }

    [Fact]
    public void Parse_IdMancante_SegnalaLaVoceColpevole()
    {
        var json = ValidJson.Replace("\"id\": \"srd-2024-it/elfo\",", "");

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("Elfo"));
    }

    [Fact]
    public void Parse_LinguaDiversaDaItaliano_AccettaEAvvisa()
    {
        var json = ValidJson.Replace("\"language\": \"it\"", "\"language\": \"en\"");

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

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

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

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

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

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

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

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

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

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

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

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

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

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

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

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

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

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

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("srd-2024-it/elfo") && e.Contains("Elfo Alto"));
    }

    // Le sottoclassi sono una sezione a tutti gli effetti — hanno id e nome, l'export le riporta —
    // e passano dagli stessi controlli delle altre. Il nome, in più, finisce dentro un
    // `<option value>` che il menu della scheda confronta per stringa esatta: senza trim un
    // « Campione » si salverebbe così e poi non combacerebbe più con la propria opzione.
    [Fact]
    public void Parse_SottoclasseConSpaziAiMargini_VieneTrimmata()
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
            { "id": "srd-2024-it/guerriero", "name": "Guerriero",
              "subclasses": [ { "id": "  srd-2024-it/campione  ", "name": "  Campione  " } ] }
          ]
        }
        """;

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

        Assert.NotNull(result.Package);
        var sottoclasse = result.Package!.Classes[0].Subclasses[0];
        Assert.Equal("srd-2024-it/campione", sottoclasse.Id);
        Assert.Equal("Campione", sottoclasse.Name);
    }

    [Fact]
    public void Parse_SottoclasseSenzaIdONome_SegnalaLaClasseColpevole()
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
            { "id": "srd-2024-it/guerriero", "name": "Guerriero",
              "subclasses": [ { "name": "Campione" }, { "id": "srd-2024-it/senza-nome" } ] }
          ]
        }
        """;

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("sottoclassi di Guerriero") && e.Contains("Campione"));
        Assert.Contains(result.Errors, e => e.Contains("sottoclassi di Guerriero") && e.Contains("non ha un nome"));
    }

    /// <summary>L'unicità vale dentro la classe: non c'è tabella dove due sottoclassi di classi
    /// diverse potrebbero collidere.</summary>
    [Fact]
    public void Parse_SottoclassiConLoStessoIdNellaStessaClasse_RestituisceErrore()
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
            { "id": "srd-2024-it/guerriero", "name": "Guerriero",
              "subclasses": [ { "id": "srd-2024-it/campione", "name": "Campione" },
                              { "id": "srd-2024-it/campione", "name": "Campionessa" } ] },
            { "id": "srd-2024-it/ladro", "name": "Ladro",
              "subclasses": [ { "id": "srd-2024-it/campione", "name": "Omonima di un'altra classe" } ] }
          ]
        }
        """;

        var result = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("sottoclassi di Guerriero") && e.Contains("Campionessa"));
        Assert.DoesNotContain(result.Errors, e => e.Contains("sottoclassi di Ladro"));
    }

    // ---- Buco di sicurezza: un file di terze parti non può spacciarsi per il manuale (§6) ----

    [Fact]
    public void Parse_IdDelPacchettoComeIlManuale_RestituisceErrore()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "id": "srd-2024-it",
          "name": "Finto manuale"
        }
        """;

        var result = CatalogPackageParser.Parse(json);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains(CatalogPackageParser.AppPackageId));
    }

    /// <summary>Il controllo copre tutte le sezioni di primo livello: una voce con l'id del manuale
    /// dentro un file altrimenti innocuo (id proprio, "mio-pacchetto") deve comunque far respingere
    /// l'intero file, non solo quella voce.</summary>
    [Theory]
    [InlineData("species")]
    [InlineData("classes")]
    [InlineData("backgrounds")]
    [InlineData("spells")]
    [InlineData("monsters")]
    public void Parse_VoceConPrefissoDelManuale_RestituisceErrore(string sezione)
    {
        const string template = """
        {
          "schemaVersion": 1,
          "id": "mio-pacchetto",
          "__SEZIONE__": [ { "id": "srd-2024-it/voce", "name": "Voce" } ]
        }
        """;
        var json = template.Replace("__SEZIONE__", sezione);

        var result = CatalogPackageParser.Parse(json);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("srd-2024-it/voce"));
    }

    /// <summary>Il difetto che chiude: il divieto sull'id del pacchetto era per uguaglianza, mentre
    /// <c>CatalogKey.IsFromAppPackage</c> confronta il <b>prefisso</b>. Un pacchetto chiamato
    /// «srd-2024-it/mio» passava, e poi <c>PackageImportPlan</c> — che interroga
    /// <c>IsFromAppPackage(package.Id + "/")</c> — lo trattava come il manuale, etichettando le sue
    /// voci «solo consultazione». Le due domande vanno poste nello stesso modo.</summary>
    [Fact]
    public void Parse_IdDelPacchettoConIlPrefissoDelManuale_RestituisceErrore()
    {
        var json = ValidJson.Replace("\"id\": \"srd-2024-it\"", "\"id\": \"srd-2024-it/mio\"")
                            .Replace("srd-2024-it/elfo", "mio/elfo");

        var result = CatalogPackageParser.Parse(json);

        Assert.Null(result.Package);
        Assert.Contains(result.Errors, e => e.Contains("srd-2024-it/"));
    }

    /// <summary>L'asimmetria voluta, e il criterio che la regge: il divieto vale dove l'id
    /// <b>diventa</b> il <c>source_id</c> della riga — è da lì che nasce l'immunità che il controllo
    /// esiste per impedire — e non vale per sottoclassi e talenti, che un <c>source_id</c> non lo
    /// producono mai (la prima vive dentro il testo della colonna <c>subclasses</c>, il secondo non ha
    /// nemmeno una tabella: <c>PackageImportPlan.ForFeats</c> lo marca <c>NotImportable</c>).
    ///
    /// Vietarli sarebbe costato la compatibilità con i file già esportati dal client <b>online</b>,
    /// che porta gli id SRD di sottoclassi e talenti verbatim: sarebbero stati respinti per intero,
    /// con un errore che incolpa il file dell'utente — e il service worker non fa
    /// <c>skipWaiting</c>, quindi quei file continueranno a nascere anche dopo il rilascio. Senza
    /// comprare niente, perché <c>CampaignExport</c> non conserva comunque quel prefisso al
    /// riesporto.</summary>
    [Theory]
    [InlineData("""
        { "schemaVersion": 1, "id": "mio-pacchetto",
          "classes": [ { "id": "mio-pacchetto/guerriero", "name": "Guerriero",
            "subclasses": [ { "id": "srd-2024-it/campione", "name": "Campione" } ] } ] }
        """)]
    [InlineData("""
        { "schemaVersion": 1, "id": "mio-pacchetto",
          "feats": [ { "id": "srd-2024-it/talento/attento", "name": "Attento" } ] }
        """)]
    public void Parse_SottoclasseOTalentoConPrefissoDelManuale_SonoAmmessi(string json)
    {
        var result = CatalogPackageParser.Parse(json);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Package);
    }

    /// <summary>Il solo caricamento legittimo del prefisso: CatalogService lo passa quando legge
    /// wwwroot/data/srd-2024-it.json. Senza questo test, un domani qualcuno potrebbe "risolvere" gli
    /// errori sopra stringendo il controllo fino a rompere il caricamento del manuale vero.</summary>
    [Fact]
    public void Parse_ÈIlManualeDellAppTrue_AccettaIlPrefissoRiservato()
    {
        var result = CatalogPackageParser.Parse(ValidJson, èIlManualeDellApp: true);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Package);
        Assert.Equal("srd-2024-it", result.Package!.Id);
    }
}
