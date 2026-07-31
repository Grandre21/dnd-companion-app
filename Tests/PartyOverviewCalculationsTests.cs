using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// Logica pura della pagina Party (PartyOverviewCalculations): raggruppamento e formattazione PF.
public class PartyOverviewCalculationsTests
{
    private static PartyMember Member(string owner, string name = "PG", int hp = 10, int maxHp = 10)
        => new() { OwnerId = owner, Name = name, HitPoints = hp, MaxHitPoints = maxHp };

    // ----- IsMine -----

    [Fact]
    public void IsMine_true_quando_owner_combacia()
        => Assert.True(PartyOverviewCalculations.IsMine(Member("u1"), "u1"));

    [Fact]
    public void IsMine_false_quando_owner_diverso()
        => Assert.False(PartyOverviewCalculations.IsMine(Member("u1"), "u2"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsMine_false_quando_owner_nullo_o_vuoto(string? owner)
        => Assert.False(PartyOverviewCalculations.IsMine(new PartyMember { OwnerId = owner ?? "" }, "u1"));

    [Fact]
    public void IsMine_false_quando_userId_nullo()
        => Assert.False(PartyOverviewCalculations.IsMine(Member("u1"), null));

    // ----- Mine / Others -----

    [Fact]
    public void Mine_solo_le_righe_dellutente_ordinate_per_nome()
    {
        var list = new List<PartyMember>
        {
            Member("u1", "Zorg"),
            Member("u2", "Altrui"),
            Member("u1", "Alba"),
        };
        var mine = PartyOverviewCalculations.Mine(list, "u1");
        Assert.Equal(new[] { "Alba", "Zorg" }, mine.Select(m => m.Name).ToArray());
    }

    [Fact]
    public void Others_esclude_le_proprie_e_ordina_per_nome_case_insensitive()
    {
        var list = new List<PartyMember>
        {
            Member("u1", "Mio"),
            Member("u2", "zombi"),
            Member("u3", "Goblin"),
        };
        var others = PartyOverviewCalculations.Others(list, "u1");
        Assert.Equal(new[] { "Goblin", "zombi" }, others.Select(m => m.Name).ToArray());
    }

    [Fact]
    public void Others_include_tutto_quando_userId_nullo()
    {
        var list = new List<PartyMember> { Member("u1", "A"), Member("u2", "B") };
        var others = PartyOverviewCalculations.Others(list, null);
        Assert.Equal(2, others.Count);
    }

    // ----- FormatHp -----

    [Theory]
    [InlineData(18, 24, "18 / 24")]
    [InlineData(0, 10, "0 / 10")]
    [InlineData(-3, 10, "-3 / 10")]
    public void FormatHp_restituisce_il_formato_atteso(int current, int max, string expected)
        => Assert.Equal(expected, PartyOverviewCalculations.FormatHp(current, max));

    // ----- HpPercent -----

    [Theory]
    [InlineData(10, 10, 100)]
    [InlineData(5, 10, 50)]
    [InlineData(0, 10, 0)]
    [InlineData(3, 10, 30)]
    public void HpPercent_calcola_la_percentuale(int current, int max, int expected)
        => Assert.Equal(expected, PartyOverviewCalculations.HpPercent(current, max));

    [Fact]
    public void HpPercent_clampa_i_pf_attuali_sopra_il_massimo()
        => Assert.Equal(100, PartyOverviewCalculations.HpPercent(999, 10));

    [Fact]
    public void HpPercent_clampa_i_pf_attuali_negativi()
        => Assert.Equal(0, PartyOverviewCalculations.HpPercent(-5, 10));

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void HpPercent_zero_quando_il_massimo_non_e_positivo(int max)
        => Assert.Equal(0, PartyOverviewCalculations.HpPercent(5, max));

    // ===== HpFillClass =====

    [Theory]
    [InlineData(24, 24, "hp-fill-ok")]     // illeso
    [InlineData(13, 24, "hp-fill-ok")]     // 54%: appena sopra la soglia di attenzione
    [InlineData(12, 24, "hp-fill-warn")]   // 50% esatto: la soglia è inclusiva
    [InlineData(7, 24, "hp-fill-warn")]    // 29%
    [InlineData(6, 24, "hp-fill-danger")]  // 25% esatto: inclusiva anche qui
    [InlineData(1, 24, "hp-fill-danger")]
    [InlineData(0, 24, "hp-fill-danger")]  // a terra
    public void HpFillClass_segue_le_fasce_di_salute(int correnti, int massimi, string atteso)
        => Assert.Equal(atteso, PartyOverviewCalculations.HpFillClass(correnti, massimi));

    [Fact]
    public void HpFillClass_su_dati_sporchi_non_esplode()
    {
        // Massimo non positivo: HpPercent torna 0, quindi la fascia è "pericolo". È il
        // comportamento voluto — meglio segnalare un PG che sembra a terra che colorarlo di verde
        // per una divisione impossibile.
        Assert.Equal("hp-fill-danger", PartyOverviewCalculations.HpFillClass(5, 0));

        // PF attuali oltre il massimo (dato incoerente): clampati, quindi pieno.
        Assert.Equal("hp-fill-ok", PartyOverviewCalculations.HpFillClass(99, 24));
    }
}
