using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// Redazione lato player del tracker combattimento (CombatVisibility): funzioni pure.
public class CombatVisibilityTests
{
    private static Combatant Own(string owner, string name = "PG", int init = 0)
        => new() { Name = name, OwnerId = owner, Initiative = init };

    private static Combatant Foreign(string name = "Mostro", int init = 0)
        => new() { Name = name, OwnerId = null, Initiative = init };

    [Fact]
    public void IsOwn_true_quando_owner_combacia()
        => Assert.True(CombatVisibility.IsOwn(Own("u1"), "u1"));

    [Fact]
    public void IsOwn_false_quando_owner_diverso()
        => Assert.False(CombatVisibility.IsOwn(Own("u1"), "u2"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsOwn_false_quando_owner_nullo_o_vuoto(string? owner)
        => Assert.False(CombatVisibility.IsOwn(new Combatant { OwnerId = owner }, "u1"));

    [Fact]
    public void IsOwn_false_quando_userId_nullo()
        => Assert.False(CombatVisibility.IsOwn(Own("u1"), null));

    [Fact]
    public void IsCurrentTurnOwn_true_quando_il_turno_e_del_player()
    {
        var list = new List<Combatant> { Foreign(), Own("u1"), Foreign() };
        Assert.True(CombatVisibility.IsCurrentTurnOwn(list, 1, "u1"));
    }

    [Fact]
    public void IsCurrentTurnOwn_false_quando_il_turno_e_altrui()
    {
        var list = new List<Combatant> { Foreign(), Own("u1"), Foreign() };
        Assert.False(CombatVisibility.IsCurrentTurnOwn(list, 0, "u1"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void IsCurrentTurnOwn_false_quando_indice_fuori_range(int idx)
    {
        var list = new List<Combatant> { Own("u1"), Foreign() };
        Assert.False(CombatVisibility.IsCurrentTurnOwn(list, idx, "u1"));
    }

    [Fact]
    public void OwnForPlayer_solo_le_righe_del_player_in_ordine_originale()
    {
        var a = Own("u1", "Gorik");
        var b = Own("u1", "Alba");
        var list = new List<Combatant> { Foreign(), a, Foreign(), b };
        Assert.Equal(new[] { a, b }, CombatVisibility.OwnForPlayer(list, "u1"));
    }

    [Fact]
    public void OthersForPlayer_esclude_le_mie_e_ordina_per_nome_case_insensitive()
    {
        var list = new List<Combatant>
        {
            Own("u1", "Gorik"),
            Foreign("zombi"),
            Foreign("Goblin"),
            Own("u1", "Alba"),
        };
        var others = CombatVisibility.OthersForPlayer(list, "u1");
        Assert.Equal(new[] { "Goblin", "zombi" }, others.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void OthersForPlayer_include_le_righe_senza_owner()
    {
        var list = new List<Combatant> { Foreign("Goblin"), Own("u1") };
        var others = CombatVisibility.OthersForPlayer(list, "u1");
        Assert.Single(others);
        Assert.Equal("Goblin", others[0].Name);
    }
}
