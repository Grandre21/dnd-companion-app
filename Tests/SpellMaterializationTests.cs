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
