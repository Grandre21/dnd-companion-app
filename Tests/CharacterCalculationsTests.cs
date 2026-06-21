using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// Test di caratterizzazione delle formule pure di CharacterCalculations:
// fissano il comportamento attuale come rete di sicurezza per i refactor.
public class CharacterCalculationsTests
{
    private static Character Char(
        int str = 10, int dex = 10, int con = 10,
        int intel = 10, int wis = 10, int cha = 10, int level = 1) => new()
    {
        Name = "Test",
        Strength = str,
        Dexterity = dex,
        Constitution = con,
        Intelligence = intel,
        Wisdom = wis,
        Charisma = cha,
        Level = level,
    };

    // ---- GetModifier: floor((score - 10) / 2) ----
    [Theory]
    [InlineData(1, -5)]
    [InlineData(7, -2)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(12, 1)]
    [InlineData(13, 1)]
    [InlineData(14, 2)]
    [InlineData(20, 5)]
    [InlineData(30, 10)]
    public void GetModifier_returns_floor_of_score_minus_10_over_2(int score, int expected)
        => Assert.Equal(expected, CharacterCalculations.GetModifier(score));

    // ---- GetProficiencyBonus: 2 + (clamp(level,1..20) - 1) / 4 ----
    [Theory]
    [InlineData(1, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(8, 3)]
    [InlineData(9, 4)]
    [InlineData(12, 4)]
    [InlineData(13, 5)]
    [InlineData(16, 5)]
    [InlineData(17, 6)]
    [InlineData(20, 6)]
    [InlineData(0, 2)]    // clamp verso 1
    [InlineData(-3, 2)]   // clamp verso 1
    [InlineData(25, 6)]   // clamp verso 20
    public void GetProficiencyBonus_clamps_level_and_scales_by_4(int level, int expected)
        => Assert.Equal(expected, CharacterCalculations.GetProficiencyBonus(level));

    // ---- GetSaveBonus: mod + (competenza ? pb : 0) ----
    [Fact]
    public void GetSaveBonus_adds_proficiency_when_proficient()
    {
        var c = Char(dex: 16, level: 5);
        c.ProfSaveDexterity = true;
        Assert.Equal(6, CharacterCalculations.GetSaveBonus(c, AbilityType.Dexterity)); // +3 mod +3 pb
    }

    [Fact]
    public void GetSaveBonus_is_only_modifier_when_not_proficient()
    {
        var c = Char(dex: 16, level: 5);
        Assert.Equal(3, CharacterCalculations.GetSaveBonus(c, AbilityType.Dexterity));
    }

    // ---- GetSkillAbility: mapping skill -> caratteristica ----
    [Theory]
    [InlineData(SkillType.Athletics, AbilityType.Strength)]
    [InlineData(SkillType.Acrobatics, AbilityType.Dexterity)]
    [InlineData(SkillType.Stealth, AbilityType.Dexterity)]
    [InlineData(SkillType.Arcana, AbilityType.Intelligence)]
    [InlineData(SkillType.Perception, AbilityType.Wisdom)]
    [InlineData(SkillType.Persuasion, AbilityType.Charisma)]
    [InlineData(SkillType.Intimidation, AbilityType.Charisma)]
    public void GetSkillAbility_maps_skill_to_ability(SkillType skill, AbilityType expected)
        => Assert.Equal(expected, CharacterCalculations.GetSkillAbility(skill));

    // ---- GetSkillBonus: mod + pb(competenza) + pb(expertise) ----
    [Fact]
    public void GetSkillBonus_is_only_modifier_without_proficiency()
    {
        var c = Char(dex: 16, level: 5);
        Assert.Equal(3, CharacterCalculations.GetSkillBonus(c, SkillType.Stealth));
    }

    [Fact]
    public void GetSkillBonus_adds_proficiency()
    {
        var c = Char(dex: 16, level: 5);
        c.ProfStealth = true;
        Assert.Equal(6, CharacterCalculations.GetSkillBonus(c, SkillType.Stealth)); // +3 +3
    }

    [Fact]
    public void GetSkillBonus_doubles_proficiency_with_expertise()
    {
        var c = Char(dex: 16, level: 5);
        c.ProfStealth = true;
        c.ExpStealth = true;
        Assert.Equal(9, CharacterCalculations.GetSkillBonus(c, SkillType.Stealth)); // +3 +3 +3
    }

    // ---- GetInitiative: modificatore di Destrezza ----
    [Theory]
    [InlineData(16, 3)]
    [InlineData(8, -1)]
    [InlineData(10, 0)]
    public void GetInitiative_is_dexterity_modifier(int dex, int expected)
        => Assert.Equal(expected, CharacterCalculations.GetInitiative(Char(dex: dex)));

    // ---- GetPassivePerception: 10 + bonus skill Percezione ----
    [Fact]
    public void GetPassivePerception_is_10_plus_perception_bonus()
    {
        var c = Char(wis: 14); // +2
        Assert.Equal(12, CharacterCalculations.GetPassivePerception(c));
    }

    [Fact]
    public void GetPassivePerception_includes_proficiency()
    {
        var c = Char(wis: 14, level: 5);
        c.ProfPerception = true;
        Assert.Equal(15, CharacterCalculations.GetPassivePerception(c)); // 10 + 2 + 3
    }

    // ---- Spellcasting: null se non incantatore ----
    [Fact]
    public void GetSpellSaveDc_is_8_plus_pb_plus_mod()
    {
        var c = Char(intel: 16, level: 5);
        c.SpellcastingAbility = "intelligence";
        Assert.Equal(14, CharacterCalculations.GetSpellSaveDc(c)); // 8 + 3 + 3
    }

    [Fact]
    public void GetSpellSaveDc_is_null_for_non_caster()
        => Assert.Null(CharacterCalculations.GetSpellSaveDc(Char()));

    [Fact]
    public void GetSpellAttackBonus_is_pb_plus_mod()
    {
        var c = Char(intel: 16, level: 5);
        c.SpellcastingAbility = "intelligence";
        Assert.Equal(6, CharacterCalculations.GetSpellAttackBonus(c)); // 3 + 3
    }

    [Fact]
    public void GetSpellAttackBonus_is_null_for_non_caster()
        => Assert.Null(CharacterCalculations.GetSpellAttackBonus(Char()));

    [Fact]
    public void GetSpellcastingModifier_returns_ability_modifier()
    {
        var c = Char(cha: 20);
        c.SpellcastingAbility = "charisma";
        Assert.Equal(5, CharacterCalculations.GetSpellcastingModifier(c));
    }

    [Fact]
    public void GetSpellcastingModifier_is_null_for_invalid_ability()
    {
        var c = Char(str: 18);
        c.SpellcastingAbility = "strength"; // non valida come incantatore
        Assert.Null(CharacterCalculations.GetSpellcastingModifier(c));
    }

    // ---- GetHitDiceRemaining: somma blocchi NdM meno spesi (0 se non parsabile) ----
    [Fact]
    public void GetHitDiceRemaining_sums_single_block()
    {
        var c = Char();
        c.HitDiceMax = "3d12";
        Assert.Equal(3, CharacterCalculations.GetHitDiceRemaining(c));
    }

    [Fact]
    public void GetHitDiceRemaining_sums_multiclass_blocks()
    {
        var c = Char();
        c.HitDiceMax = "3d12+2d8";
        Assert.Equal(5, CharacterCalculations.GetHitDiceRemaining(c));
    }

    [Fact]
    public void GetHitDiceRemaining_subtracts_spent()
    {
        var c = Char();
        c.HitDiceMax = "5d10";
        c.HitDiceSpent = 2;
        Assert.Equal(3, CharacterCalculations.GetHitDiceRemaining(c));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("non valido")]
    public void GetHitDiceRemaining_is_zero_for_unparseable(string? value)
    {
        var c = Char();
        c.HitDiceMax = value;
        Assert.Equal(0, CharacterCalculations.GetHitDiceRemaining(c));
    }
}
