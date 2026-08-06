using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// Logica pura di import mostri e personaggi nel combattimento (CombatImport).
public class CombatImportTests
{
    private static Monster M(string name, string hp) => new() { Name = name, HitPoints = hp };

    [Theory]
    [InlineData("45 (6d8+18)", 45)]
    [InlineData("7", 7)]
    [InlineData("  12 hp", 12)]
    [InlineData("256", 256)]
    [InlineData("", 1)]
    [InlineData(null, 1)]
    [InlineData("n/a", 1)]
    [InlineData("0", 1)] // clamp a >= 1
    public void ParseLeadingHp_extracts_first_int_or_falls_back_to_1(string? text, int expected)
        => Assert.Equal(expected, CombatImport.ParseLeadingHp(text));

    [Fact]
    public void FromMonster_single_copy_uses_plain_name_and_parsed_hp()
    {
        var result = CombatImport.FromMonster(M("Goblin", "7 (2d6)"), 1).ToList();

        var c = Assert.Single(result);
        Assert.Equal("Goblin", c.Name);
        Assert.Equal(0, c.Initiative);
        Assert.Equal(7, c.CurrentHp);
        Assert.Equal(7, c.MaxHp);
    }

    [Fact]
    public void FromMonster_multiple_copies_are_numbered_with_same_hp()
    {
        var result = CombatImport.FromMonster(M("Orco", "15 (2d8+6)"), 3).ToList();

        Assert.Equal(new[] { "Orco 1", "Orco 2", "Orco 3" }, result.Select(c => c.Name));
        Assert.All(result, c => Assert.Equal(15, c.MaxHp));
        Assert.All(result, c => Assert.Equal(15, c.CurrentHp));
    }

    [Fact]
    public void FromMonster_falls_back_to_hp_1_when_text_has_no_number()
    {
        var c = Assert.Single(CombatImport.FromMonster(M("Slime", "boh"), 1).ToList());
        Assert.Equal(1, c.CurrentHp);
        Assert.Equal(1, c.MaxHp);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FromMonster_returns_empty_for_non_positive_quantity(int qty)
        => Assert.Empty(CombatImport.FromMonster(M("Goblin", "7"), qty));

    [Fact]
    public void FromMonster_gives_each_copy_a_distinct_id()
    {
        var ids = CombatImport.FromMonster(M("Orco", "15"), 3).Select(c => c.Id).ToList();
        Assert.Equal(3, ids.Distinct().Count());
    }

    // ---- FromCharacter: iniziativa precompilata dal bonus di Destrezza ----

    private static Character PG(string name = "Aria", int dex = 10, int hp = 20, int maxHp = 20, string ownerId = "u1")
        => new() { Name = name, Dexterity = dex, HitPoints = hp, MaxHitPoints = maxHp, OwnerId = ownerId };

    [Fact]
    public void FromCharacter_usa_il_bonus_di_destrezza_come_iniziativa_di_partenza()
    {
        var c = CombatImport.FromCharacter(PG(dex: 16)); // modificatore +3

        Assert.Equal(3, c.Initiative);
    }

    [Fact]
    public void FromCharacter_ammette_bonus_negativo_con_destrezza_bassa()
    {
        var c = CombatImport.FromCharacter(PG(dex: 8)); // modificatore -1

        Assert.Equal(-1, c.Initiative);
    }

    [Fact]
    public void FromCharacter_senza_destrezza_valorizzata_ha_iniziativa_zero_come_oggi()
    {
        // Destrezza non valorizzata = default del modello (10) -> modificatore 0: comportamento invariato.
        var c = CombatImport.FromCharacter(new Character { Name = "Bo", HitPoints = 5, MaxHitPoints = 5 });

        Assert.Equal(0, c.Initiative);
    }

    [Fact]
    public void FromCharacter_riporta_nome_owner_e_pf_clampati_come_oggi()
    {
        var c = CombatImport.FromCharacter(PG(name: "Aria", dex: 14, hp: 30, maxHp: 25, ownerId: "u42"));

        Assert.Equal("Aria", c.Name);
        Assert.Equal("u42", c.OwnerId);
        Assert.Equal(25, c.CurrentHp); // clampato a MaxHp come nell'import attuale
        Assert.Equal(25, c.MaxHp);
    }

    [Fact]
    public void FromCharacter_non_tocca_l_importazione_dei_mostri()
    {
        // I mostri restano Initiative=0: la precompilazione riguarda solo i PG (l'app non tira i dadi per i mostri).
        var mostro = Assert.Single(CombatImport.FromMonster(M("Goblin", "7"), 1).ToList());
        var pgConBonus = CombatImport.FromCharacter(PG(dex: 18));

        Assert.Equal(0, mostro.Initiative);
        Assert.Equal(4, pgConBonus.Initiative);
    }
}
