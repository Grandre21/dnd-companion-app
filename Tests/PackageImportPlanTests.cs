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

    /// <summary>La guida della pagina Dati invita a partire dall'export «manuale incluso», che
    /// contiene le sottoclassi: chi ne aggiunge una propria a quel file la vedrebbe sparire senza un
    /// rigo, perché la tabella `classes` non ha una colonna per portarle. Stessa regola dei talenti:
    /// una sezione non scritta va dichiarata, non taciuta.</summary>
    [Fact]
    public void ForClasses_ConSottoclassiNelFile_DiceCheNonVengonoScritte()
    {
        var pacchetto = new CatalogPackage
        {
            SchemaVersion = 1,
            Id = "mio-pacchetto",
            Classes =
            {
                new PackageClass
                {
                    Id = "mio-pacchetto/guerriero",
                    Name = "Guerriero",
                    Subclasses = { new PackageSubclass { Id = "mio-pacchetto/campione", Name = "Campione" } },
                },
            },
        };

        var sezione = PackageImportPlan.ForClasses(
            pacchetto, new CampaignCatalogs(), isMaster: false, userId: Utente);

        Assert.Equal("Classi", sezione.Title);
        Assert.Equal(1, sezione.CreateCount);
        Assert.Contains("sottoclassi", sezione.Note!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("una classe ne porta", sezione.Note!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Senza sottoclassi nel file la nota non c'è: un'avvertenza che parla di un contenuto
    /// assente si legge come un problema da risolvere.</summary>
    [Fact]
    public void ForClasses_SenzaSottoclassi_NonMetteAlcunaNota()
    {
        var pacchetto = new CatalogPackage
        {
            SchemaVersion = 1,
            Id = "mio-pacchetto",
            Classes = { new PackageClass { Id = "mio-pacchetto/guerriero", Name = "Guerriero" } },
        };

        var sezione = PackageImportPlan.ForClasses(
            pacchetto, new CampaignCatalogs(), isMaster: false, userId: Utente);

        Assert.Null(sezione.Note);
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
