using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Scelta dei mostri per il tracker iniziativa. Il difetto che questo helper chiude non era di
/// calcolo ma di sorgente: il tracker interrogava solo il database, quindi i 331 mostri del manuale
/// non erano utilizzabili al tavolo — con build verde, test verdi e la pagina che diceva
/// «nessun mostro nella campagna».
/// </summary>
public class MonsterPickerTests
{
    private static Monster Riga(string id, string nome, string pf = "12 (2d8+3)")
        => new() { Id = id, Name = nome, HitPoints = pf, CampaignId = "c1" };

    private static PackageMonster Voce(string nome, string pf = "19 (3d10 + 3)")
        => new() { Id = "srd-2024-it/mostro/" + nome.ToLowerInvariant(), Name = nome, HitPoints = pf };

    [Fact]
    public void Senza_ricerca_mostra_solo_le_righe_di_campagna()
    {
        var scelta = MonsterPicker.Scegli(
            new[] { Riga("u1", "Goblin del tavolo") },
            new[] { Voce("Drago rosso") },
            ricerca: null, limite: 40);

        Assert.Equal(new[] { "Goblin del tavolo" }, scelta.Voci.Select(v => v.Nome));
        Assert.Equal(0, scelta.Troncate);
    }

    [Fact]
    public void La_ricerca_pesca_anche_nel_manuale()
    {
        var scelta = MonsterPicker.Scegli(
            Array.Empty<Monster>(),
            new[] { Voce("Drago rosso adulto"), Voce("Goblin") },
            "drago", limite: 40);

        var voce = Assert.Single(scelta.Voci);
        Assert.Equal("Drago rosso adulto", voce.Nome);
        Assert.True(voce.DalManuale);
    }

    /// <summary>Stessa normalizzazione del resto dei cataloghi: cercare «drago» deve trovare
    /// «Drago», e gli accenti non devono contare.</summary>
    [Theory]
    [InlineData("GOBLIN")]
    [InlineData("gob")]
    [InlineData("  Goblin  ")]
    public void La_ricerca_ignora_maiuscole_e_spazi(string ricerca)
        => Assert.Single(MonsterPicker.Scegli(
            Array.Empty<Monster>(), new[] { Voce("Goblin") }, ricerca, 40).Voci);

    [Fact]
    public void Le_righe_di_campagna_precedono_quelle_di_manuale()
    {
        var voci = MonsterPicker.Scegli(
            new[] { Riga("u1", "Zombi") },
            new[] { Voce("Zombi") },
            "zombi", limite: 40).Voci;

        Assert.Equal(2, voci.Count);
        Assert.False(voci[0].DalManuale);
        Assert.True(voci[1].DalManuale);
    }

    [Fact]
    public void Le_due_sorgenti_non_collidono_sulle_chiavi()
    {
        var voci = MonsterPicker.Scegli(
            new[] { Riga("abc", "Zombi") },
            new[] { Voce("Zombi") },
            "zombi", limite: 40).Voci;

        Assert.Equal(voci.Count, voci.Select(v => v.Chiave).Distinct().Count());
        Assert.True(MonsterPicker.DalManuale(voci[1].Chiave));
        Assert.False(MonsterPicker.DalManuale(voci[0].Chiave));
        Assert.Equal("abc", MonsterPicker.IdSenzaPrefisso(voci[0].Chiave));
    }

    [Fact]
    public void Oltre_il_limite_le_voci_si_contano_invece_di_mostrarle()
    {
        var manuale = Enumerable.Range(1, 50).Select(i => Voce($"Drago {i}")).ToList();

        var scelta = MonsterPicker.Scegli(Array.Empty<Monster>(), manuale, "drago", limite: 40);

        Assert.Equal(40, scelta.Voci.Count);
        Assert.Equal(10, scelta.Troncate);
    }

    /// <summary>Il tetto non vale a ricerca vuota: le righe di campagna sono quelle che il master ha
    /// preparato, e troncarle sarebbe una perdita secca rispetto a prima.</summary>
    [Fact]
    public void Senza_ricerca_il_tetto_non_tronca_le_righe_di_campagna()
    {
        var righe = Enumerable.Range(1, 50).Select(i => Riga($"u{i}", $"Goblin {i}")).ToList();

        var scelta = MonsterPicker.Scegli(righe, Array.Empty<PackageMonster>(), null, limite: 40);

        Assert.Equal(50, scelta.Voci.Count);
        Assert.Equal(0, scelta.Troncate);
    }

    [Fact]
    public void Nessuna_corrispondenza_non_e_un_errore()
    {
        var scelta = MonsterPicker.Scegli(
            new[] { Riga("u1", "Goblin") }, new[] { Voce("Orco") }, "tarrasque", 40);

        Assert.Empty(scelta.Voci);
        Assert.Equal(0, scelta.Troncate);
    }

    [Fact]
    public void Le_collezioni_nulle_non_fanno_saltare_nulla()
    {
        Assert.Empty(MonsterPicker.Scegli(null, null, null, 40).Voci);
        Assert.Empty(MonsterPicker.Scegli(null, null, "drago", 40).Voci);
    }

    /// <summary>I punti ferita restano il testo del manuale: la conversione a intero è di
    /// CombatImport, e duplicarla qui produrrebbe due regole che divergono.</summary>
    [Fact]
    public void Il_testo_dei_punti_ferita_arriva_intatto()
    {
        var voce = Assert.Single(MonsterPicker.Scegli(
            Array.Empty<Monster>(), new[] { Voce("Orco", "15 (2d8 + 6)") }, "orco", 40).Voci);

        Assert.Equal("15 (2d8 + 6)", voce.PfTesto);
        Assert.Equal(15, CombatImport.ParseLeadingHp(voce.PfTesto));
    }

    [Fact]
    public void Un_mostro_di_manuale_diventa_combattente_come_uno_di_campagna()
    {
        var combattenti = CombatImport.FromPackageMonster(Voce("Goblin", "7 (2d6)"), 3).ToList();

        Assert.Equal(3, combattenti.Count);
        Assert.Equal(new[] { "Goblin 1", "Goblin 2", "Goblin 3" }, combattenti.Select(c => c.Name));
        Assert.All(combattenti, c => Assert.Equal(7, c.MaxHp));
        Assert.All(combattenti, c => Assert.Equal(c.MaxHp, c.CurrentHp));
    }
}
