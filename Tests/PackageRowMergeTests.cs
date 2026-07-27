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
