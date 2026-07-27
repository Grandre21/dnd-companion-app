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
