using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

public class CharacterWizardLogicTests
{
    // ===== FinalAbilityScores =====

    [Fact]
    public void FinalAbilityScores_with_null_race_returns_base_unchanged()
    {
        var result = CharacterWizardLogic.FinalAbilityScores(new[] { 10, 12, 14, 8, 15, 13 }, null);
        Assert.Equal(new[] { 10, 12, 14, 8, 15, 13 }, result);
    }

    [Fact]
    public void FinalAbilityScores_adds_race_bonuses_in_order()
    {
        var race = new Race { StrBonus = 2, ConBonus = 1, ChaBonus = 1 };
        var result = CharacterWizardLogic.FinalAbilityScores(new[] { 10, 10, 10, 10, 10, 10 }, race);
        Assert.Equal(new[] { 12, 10, 11, 10, 10, 11 }, result);
    }

    [Fact]
    public void FinalAbilityScores_clamps_to_30()
    {
        var race = new Race { StrBonus = 5 };
        var result = CharacterWizardLogic.FinalAbilityScores(new[] { 29, 10, 10, 10, 10, 10 }, race);
        Assert.Equal(30, result[0]);
    }

    [Fact]
    public void FinalAbilityScores_clamps_to_1()
    {
        var race = new Race { StrBonus = -5 };
        var result = CharacterWizardLogic.FinalAbilityScores(new[] { 3, 10, 10, 10, 10, 10 }, race);
        Assert.Equal(1, result[0]);
    }

    [Fact]
    public void FinalAbilityScores_short_array_treats_missing_as_10()
    {
        var result = CharacterWizardLogic.FinalAbilityScores(new[] { 15 }, null);
        Assert.Equal(new[] { 15, 10, 10, 10, 10, 10 }, result);
    }

    // ===== BuildHitDice =====

    [Theory]
    [InlineData("d12", 3, "3d12")]
    [InlineData("D8", 1, "1d8")]
    [InlineData("1d6", 5, "5d6")]
    [InlineData("d10", 0, "1d10")]   // livello < 1 trattato come 1
    public void BuildHitDice_builds_expected(string die, int level, string expected)
        => Assert.Equal(expected, CharacterWizardLogic.BuildHitDice(die, level));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("custom")]   // niente 'd' → non riconosciuto
    public void BuildHitDice_unrecognized_returns_empty(string? die)
        => Assert.Equal("", CharacterWizardLogic.BuildHitDice(die, 3));

    // ===== SuggestMaxHp =====

    [Fact]
    public void SuggestMaxHp_level1_is_full_die_plus_con()
        => Assert.Equal(14, CharacterWizardLogic.SuggestMaxHp("d12", 2, 1)); // 12 + 2

    [Fact]
    public void SuggestMaxHp_multilevel_uses_rounded_up_average()
        // liv1: 12+1 ; liv2,3: (7)+1 ciascuno → 13 + 8 + 8 = 29
        => Assert.Equal(29, CharacterWizardLogic.SuggestMaxHp("d12", 1, 3));

    [Fact]
    public void SuggestMaxHp_floors_at_1()
        => Assert.Equal(1, CharacterWizardLogic.SuggestMaxHp("d6", -5, 1)); // 6-5=1

    [Fact]
    public void SuggestMaxHp_negative_total_floored_to_1()
        => Assert.Equal(1, CharacterWizardLogic.SuggestMaxHp("d4", -10, 1)); // 4-10 → 1

    [Fact]
    public void SuggestMaxHp_unrecognized_die_returns_0()
        => Assert.Equal(0, CharacterWizardLogic.SuggestMaxHp("custom", 2, 3));

    // ===== ParseSaveProficiencies =====

    [Fact]
    public void ParseSaveProficiencies_maps_two_abilities()
        => Assert.Equal(new[] { "strength", "constitution" },
                        CharacterWizardLogic.ParseSaveProficiencies("Forza, Costituzione"));

    [Fact]
    public void ParseSaveProficiencies_is_case_and_space_insensitive()
        => Assert.Equal(new[] { "strength", "constitution" },
                        CharacterWizardLogic.ParseSaveProficiencies("  FORZA , costituzione "));

    [Fact]
    public void ParseSaveProficiencies_drops_unknown_tokens()
        => Assert.Equal(new[] { "wisdom" },
                        CharacterWizardLogic.ParseSaveProficiencies("Pippo, Saggezza"));

    [Fact]
    public void ParseSaveProficiencies_dedupes()
        => Assert.Equal(new[] { "strength" },
                        CharacterWizardLogic.ParseSaveProficiencies("Forza, Forza"));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ParseSaveProficiencies_empty_returns_empty(string? text)
        => Assert.Empty(CharacterWizardLogic.ParseSaveProficiencies(text));

    [Fact]
    public void ParseSaveProficiencies_maps_all_six()
        => Assert.Equal(
            new[] { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" },
            CharacterWizardLogic.ParseSaveProficiencies("Forza, Destrezza, Costituzione, Intelligenza, Saggezza, Carisma"));
}
