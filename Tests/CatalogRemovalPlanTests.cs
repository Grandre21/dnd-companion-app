using DndCompanion.Models;
using DndCompanion.Services;

namespace DndCompanion.Tests;

public class CatalogRemovalPlanTests
{
    private const string Utente = "utente-1";
    private const string Altro = "utente-2";

    private static Race Razza(string id, string? sourceId, string? addedBy = Utente)
        => new() { Id = id, SourceId = sourceId, Name = "Riga", AddedBy = addedBy, CampaignId = "c1" };

    private static CampaignCatalogs SoloRazze(params Race[] righe)
        => new() { Races = righe.ToList() };

    // ---- Selezione per provenienza ----

    [Fact]
    public void Build_SelezionaSoloLeRigheDiQuellaProvenienza()
    {
        var cataloghi = SoloRazze(
            Razza("uuid-1", "mio-pacchetto/elfo"),
            Razza("uuid-2", "altro-pacchetto/nano"),
            Razza("uuid-3", "mio-pacchetto/umano"));

        var piano = CatalogRemovalPlan.Build(cataloghi, "mio-pacchetto", isMaster: false, Utente);

        Assert.Equal(new[] { "uuid-1", "uuid-3" }, piano.RaceIds);
        Assert.Equal(2, piano.Total);
    }

    // Il cuore di §8: in SQL `_` vale "un carattere qualsiasi" e `%` "qualunque sequenza". Con un
    // LIKE costruito col testo digitato queste due righe cancellerebbero il manuale.
    [Theory]
    [InlineData("srd-2024-i_")]
    [InlineData("%")]
    [InlineData("srd-2024-it%")]
    public void Build_IMetacaratteriSqlNonSonoWildcard(string digitato)
    {
        var cataloghi = SoloRazze(Razza("uuid-1", "srd-2024-it/elfo"));

        var piano = CatalogRemovalPlan.Build(cataloghi, digitato, isMaster: true, Utente);

        Assert.Equal(0, piano.Total);
    }

    [Fact]
    public void Build_ProvenienzaParzialeNonSelezionaNulla()
    {
        var cataloghi = SoloRazze(Razza("uuid-1", "mio-pacchetto-esteso/elfo"));

        // "mio-pacchetto" non è la provenienza di quella riga: il confronto è sul prefisso
        // "mio-pacchetto/", non sull'inizio del testo.
        Assert.Equal(0, CatalogRemovalPlan.Build(cataloghi, "mio-pacchetto", false, Utente).Total);
    }

    [Fact]
    public void Build_RigheSenzaProvenienzaRestanoFuori()
    {
        var cataloghi = SoloRazze(Razza("uuid-1", null), Razza("uuid-2", "mio-pacchetto/elfo"));

        var piano = CatalogRemovalPlan.Build(cataloghi, "mio-pacchetto", false, Utente);

        Assert.Equal(new[] { "uuid-2" }, piano.RaceIds);
    }

    [Fact]
    public void Build_ProvenienzaDigitataConSlashFinale_SelezionaComunque()
    {
        var cataloghi = SoloRazze(Razza("uuid-1", "mio-pacchetto/elfo"));

        var piano = CatalogRemovalPlan.Build(cataloghi, " mio-pacchetto/ ", false, Utente);

        Assert.Equal(new[] { "uuid-1" }, piano.RaceIds);
        // Il prefisso riportato è quello normalizzato: è ciò che la conferma mostrerà all'utente.
        Assert.Equal("mio-pacchetto", piano.Prefix);
    }

    // ---- Permessi ----

    [Fact]
    public void Build_RigaAltrui_NonFinisceFraGliIdMaSiConta()
    {
        var cataloghi = SoloRazze(
            Razza("uuid-mia", "mio-pacchetto/elfo"),
            Razza("uuid-sua", "mio-pacchetto/nano", addedBy: Altro));

        var piano = CatalogRemovalPlan.Build(cataloghi, "mio-pacchetto", isMaster: false, Utente);

        Assert.Equal(new[] { "uuid-mia" }, piano.RaceIds);
        Assert.Equal(1, piano.BlockedByPermission);
        Assert.Equal(1, piano.Total);
    }

    [Fact]
    public void Build_IlMasterRimuoveAncheLeRigheAltrui()
    {
        var cataloghi = SoloRazze(
            Razza("uuid-mia", "mio-pacchetto/elfo"),
            Razza("uuid-sua", "mio-pacchetto/nano", addedBy: Altro));

        var piano = CatalogRemovalPlan.Build(cataloghi, "mio-pacchetto", isMaster: true, Utente);

        Assert.Equal(2, piano.Total);
        Assert.Equal(0, piano.BlockedByPermission);
    }

    // Seed importato prima che esistesse added_by: AccessControl.CanEdit esclude il match degenere
    // null == null, quindi solo il master può toglierlo.
    [Fact]
    public void Build_RigaSenzaAutore_SoloIlMasterLaRimuove()
    {
        var cataloghi = SoloRazze(Razza("uuid-1", "mio-pacchetto/elfo", addedBy: null));

        Assert.Equal(0, CatalogRemovalPlan.Build(cataloghi, "mio-pacchetto", false, Utente).Total);
        Assert.Equal(1, CatalogRemovalPlan.Build(cataloghi, "mio-pacchetto", true, Utente).Total);
    }

    // ---- I cinque cataloghi ----

    [Fact]
    public void Build_CopreTuttiECinqueICataloghi()
    {
        var cataloghi = new CampaignCatalogs
        {
            Races = { Razza("r1", "p/elfo") },
            Classes = { new CharacterClass { Id = "c1", SourceId = "p/mago", Name = "Mago", AddedBy = Utente } },
            Backgrounds = { new Background { Id = "b1", SourceId = "p/soldato", Name = "Soldato", AddedBy = Utente } },
            Spells = { new Spell { Id = "s1", SourceId = "p/palla", Name = "Palla", AddedBy = Utente } },
            Monsters = { new Monster { Id = "m1", SourceId = "p/goblin", Name = "Goblin", AddedBy = Utente } },
        };

        var piano = CatalogRemovalPlan.Build(cataloghi, "p", false, Utente);

        Assert.Equal(new[] { "r1" }, piano.RaceIds);
        Assert.Equal(new[] { "c1" }, piano.ClassIds);
        Assert.Equal(new[] { "b1" }, piano.BackgroundIds);
        Assert.Equal(new[] { "s1" }, piano.SpellIds);
        Assert.Equal(new[] { "m1" }, piano.MonsterIds);
        Assert.Equal(5, piano.Total);
    }

    // ---- Provenienze rimovibili ----

    [Theory]
    [InlineData("mio-pacchetto", true)]
    [InlineData("srd-2024", true)]          // non è il pacchetto dell'app: nessuna sua voce combacia
    [InlineData("srd-2024-it", false)]
    [InlineData("srd-2024-it/", false)]     // lo slash finale non deve aggirare la guardia
    [InlineData("srd-2024-it/palla", false)]
    [InlineData("  ", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRemovablePrefix_NegaIlPacchettoDellAppEIlVuoto(string? prefisso, bool atteso)
        => Assert.Equal(atteso, CatalogRemovalPlan.IsRemovablePrefix(prefisso));

    [Theory]
    [InlineData("  mio-pacchetto  ", "mio-pacchetto")]
    [InlineData("mio-pacchetto/", "mio-pacchetto")]
    [InlineData("mio-pacchetto//", "mio-pacchetto")]
    [InlineData(null, "")]
    public void NormalizePrefix_TogliSpaziESlashFinali(string? grezzo, string atteso)
        => Assert.Equal(atteso, CatalogRemovalPlan.NormalizePrefix(grezzo));

    // ---- Resoconto dopo l'operazione ----

    [Fact]
    public void StillPresent_TutteCancellate_ZeroRimaste()
    {
        var prima = CatalogRemovalPlan.Build(
            SoloRazze(Razza("uuid-1", "p/elfo"), Razza("uuid-2", "p/nano")), "p", false, Utente);
        var dopo = CatalogRemovalPlan.Build(SoloRazze(), "p", false, Utente);

        Assert.Equal(0, CatalogRemovalPlan.StillPresent(prima, dopo));
    }

    [Fact]
    public void StillPresent_ContaLeRigheSopravvissute()
    {
        var prima = CatalogRemovalPlan.Build(
            SoloRazze(Razza("uuid-1", "p/elfo"), Razza("uuid-2", "p/nano")), "p", true, Utente);
        var dopo = CatalogRemovalPlan.Build(SoloRazze(Razza("uuid-2", "p/nano")), "p", true, Utente);

        Assert.Equal(1, CatalogRemovalPlan.StillPresent(prima, dopo));
    }

    // Fra l'anteprima e il riconteggio possono comparire righe nuove della stessa provenienza (un
    // import rifatto, un altro giocatore che scrive): sono estranee all'operazione e non vanno
    // scambiate per righe sopravvissute — altrimenti il resoconto mente e può andare in negativo.
    [Fact]
    public void StillPresent_IgnoraLeRigheComparseDopoLAnteprima()
    {
        var prima = CatalogRemovalPlan.Build(SoloRazze(Razza("uuid-1", "p/elfo")), "p", false, Utente);
        var dopo = CatalogRemovalPlan.Build(
            SoloRazze(Razza("uuid-nuova-1", "p/elfo"), Razza("uuid-nuova-2", "p/nano")), "p", false, Utente);

        Assert.Equal(0, CatalogRemovalPlan.StillPresent(prima, dopo));
    }
}
