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
}
