using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

public class CharacterWizardLogicTests
{
    // ===== RaceBonuses =====

    [Fact]
    public void RaceBonuses_with_null_race_are_all_zero()
        => Assert.Equal(new[] { 0, 0, 0, 0, 0, 0 }, CharacterWizardLogic.RaceBonuses(null));

    [Fact]
    public void RaceBonuses_follow_the_canonical_order()
    {
        var race = new Race { StrBonus = 1, DexBonus = 2, ConBonus = 3, IntBonus = 4, WisBonus = 5, ChaBonus = 6 };
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, CharacterWizardLogic.RaceBonuses(race));
    }

    [Fact]
    public void RaceBonuses_keep_negative_values()
    {
        var race = new Race { StrBonus = -2, WisBonus = 1 };
        Assert.Equal(new[] { -2, 0, 0, 0, 1, 0 }, CharacterWizardLogic.RaceBonuses(race));
    }

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

    [Fact]
    public void FinalAbilityScores_null_base_treats_all_as_10()
    {
        // baseScores null → ogni caratteristica parte da 10 (ramo difensivo), poi + bonus razza.
        var race = new Race { StrBonus = 2 };
        var result = CharacterWizardLogic.FinalAbilityScores(null!, race);
        Assert.Equal(new[] { 12, 10, 10, 10, 10, 10 }, result);
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
    [InlineData("d")]        // 'd' senza dimensione dopo
    [InlineData("3d")]       // nessuna cifra dopo la 'd'
    [InlineData("d0")]       // dado 0 non valido
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

    // ===== IsValidStandardArrayAssignment =====

    [Fact]
    public void IsValidStandardArrayAssignment_accepts_any_permutation()
        => Assert.True(CharacterWizardLogic.IsValidStandardArrayAssignment(new[] { 8, 10, 12, 13, 14, 15 }));

    [Fact]
    public void IsValidStandardArrayAssignment_accepts_canonical_order()
        => Assert.True(CharacterWizardLogic.IsValidStandardArrayAssignment(CharacterWizardLogic.StandardArrayScores));

    [Fact]
    public void IsValidStandardArrayAssignment_rejects_wrong_values()
        => Assert.False(CharacterWizardLogic.IsValidStandardArrayAssignment(new[] { 15, 14, 13, 12, 10, 9 }));

    [Fact]
    public void IsValidStandardArrayAssignment_rejects_duplicate_values()
        => Assert.False(CharacterWizardLogic.IsValidStandardArrayAssignment(new[] { 15, 15, 13, 12, 10, 8 }));

    [Fact]
    public void IsValidStandardArrayAssignment_rejects_wrong_length()
        => Assert.False(CharacterWizardLogic.IsValidStandardArrayAssignment(new[] { 15, 14, 13 }));

    [Fact]
    public void IsValidStandardArrayAssignment_rejects_null()
        => Assert.False(CharacterWizardLogic.IsValidStandardArrayAssignment(null));

    // ===== PointBuyCost / PointBuyTotalCost / PointBuyRemaining =====

    [Theory]
    [InlineData(8, 0)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    [InlineData(12, 4)]
    [InlineData(13, 5)]
    [InlineData(14, 7)]
    [InlineData(15, 9)]
    public void PointBuyCost_matches_5e_table(int score, int expectedCost)
        => Assert.Equal(expectedCost, CharacterWizardLogic.PointBuyCost(score));

    [Theory]
    [InlineData(7)]
    [InlineData(16)]
    [InlineData(0)]
    public void PointBuyCost_out_of_range_is_null(int score)
        => Assert.Null(CharacterWizardLogic.PointBuyCost(score));

    [Fact]
    public void PointBuyTotalCost_sums_the_six_costs()
        // 15,14,13,12,10,8 -> 9+7+5+4+2+0 = 27 (l'array standard costa esattamente il budget)
        => Assert.Equal(27, CharacterWizardLogic.PointBuyTotalCost(new[] { 15, 14, 13, 12, 10, 8 }));

    [Fact]
    public void PointBuyTotalCost_all_eights_is_zero()
        => Assert.Equal(0, CharacterWizardLogic.PointBuyTotalCost(new[] { 8, 8, 8, 8, 8, 8 }));

    [Fact]
    public void PointBuyTotalCost_null_if_any_score_out_of_range()
        => Assert.Null(CharacterWizardLogic.PointBuyTotalCost(new[] { 15, 14, 13, 12, 10, 16 }));

    [Fact]
    public void PointBuyTotalCost_null_if_wrong_length()
        => Assert.Null(CharacterWizardLogic.PointBuyTotalCost(new[] { 8, 8, 8 }));

    [Fact]
    public void PointBuyTotalCost_null_for_null_array()
        => Assert.Null(CharacterWizardLogic.PointBuyTotalCost(null));

    [Fact]
    public void PointBuyRemaining_is_budget_minus_cost()
        => Assert.Equal(27, CharacterWizardLogic.PointBuyRemaining(new[] { 8, 8, 8, 8, 8, 8 }));

    [Fact]
    public void PointBuyRemaining_can_go_negative_when_overspent()
        // 15 costa 9 punti ciascuno: 6*9 = 54, 27-54 = -27.
        => Assert.Equal(-27, CharacterWizardLogic.PointBuyRemaining(new[] { 15, 15, 15, 15, 15, 15 }));

    [Fact]
    public void PointBuyRemaining_null_if_score_out_of_range()
        => Assert.Null(CharacterWizardLogic.PointBuyRemaining(new[] { 15, 14, 13, 12, 10, 7 }));

    // ===== RaceGrantsAbilityBonuses =====

    [Fact]
    public void RaceGrantsAbilityBonuses_false_for_null()
        => Assert.False(CharacterWizardLogic.RaceGrantsAbilityBonuses(null));

    [Fact]
    public void RaceGrantsAbilityBonuses_false_when_all_zero()
        => Assert.False(CharacterWizardLogic.RaceGrantsAbilityBonuses(new Race()));

    [Fact]
    public void RaceGrantsAbilityBonuses_true_when_one_nonzero()
        => Assert.True(CharacterWizardLogic.RaceGrantsAbilityBonuses(new Race { DexBonus = 2 }));

    // ===== BackgroundAbilityKeys =====

    [Fact]
    public void BackgroundAbilityKeys_parses_three_abilities()
        => Assert.Equal(new[] { "strength", "wisdom", "charisma" },
            CharacterWizardLogic.BackgroundAbilityKeys("Forza, Saggezza, Carisma"));

    [Fact]
    public void BackgroundAbilityKeys_empty_for_free_text_background()
        => Assert.Empty(CharacterWizardLogic.BackgroundAbilityKeys(""));

    // ===== ShouldApplyBackgroundBonuses =====

    [Fact]
    public void ShouldApplyBackgroundBonuses_true_when_no_race_bonus_and_background_has_keys()
        => Assert.True(CharacterWizardLogic.ShouldApplyBackgroundBonuses(null, new[] { "strength" }));

    [Fact]
    public void ShouldApplyBackgroundBonuses_false_when_no_background_keys()
        => Assert.False(CharacterWizardLogic.ShouldApplyBackgroundBonuses(null, Array.Empty<string>()));

    [Fact]
    public void ShouldApplyBackgroundBonuses_false_when_race_has_legacy_bonuses()
        => Assert.False(CharacterWizardLogic.ShouldApplyBackgroundBonuses(
            new Race { StrBonus = 2 }, new[] { "strength" }));

    // ===== BuildBackgroundBonusMap =====

    [Fact]
    public void BuildBackgroundBonusMap_two_and_one_assigns_2_and_1_in_order()
    {
        var map = CharacterWizardLogic.BuildBackgroundBonusMap(
            CharacterWizardLogic.BackgroundAbilitySplit.TwoAndOne, new[] { "wisdom", "charisma" });
        Assert.Equal(2, map["wisdom"]);
        Assert.Equal(1, map["charisma"]);
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void BuildBackgroundBonusMap_one_each_assigns_1_to_all_three()
    {
        var map = CharacterWizardLogic.BuildBackgroundBonusMap(
            CharacterWizardLogic.BackgroundAbilitySplit.OneEachOfThree, new[] { "strength", "wisdom", "charisma" });
        Assert.Equal(new Dictionary<string, int> { ["strength"] = 1, ["wisdom"] = 1, ["charisma"] = 1 }, map);
    }

    [Fact]
    public void BuildBackgroundBonusMap_two_and_one_with_wrong_count_is_empty()
        => Assert.Empty(CharacterWizardLogic.BuildBackgroundBonusMap(
            CharacterWizardLogic.BackgroundAbilitySplit.TwoAndOne, new[] { "wisdom" }));

    [Fact]
    public void BuildBackgroundBonusMap_one_each_with_zero_keys_is_empty()
        => Assert.Empty(CharacterWizardLogic.BuildBackgroundBonusMap(
            CharacterWizardLogic.BackgroundAbilitySplit.OneEachOfThree, Array.Empty<string>()));

    [Fact]
    public void BuildBackgroundBonusMap_dedupes_duplicate_keys()
        => Assert.Empty(CharacterWizardLogic.BuildBackgroundBonusMap(
            CharacterWizardLogic.BackgroundAbilitySplit.TwoAndOne, new[] { "wisdom", "wisdom" }));

    [Fact]
    public void BuildBackgroundBonusMap_null_list_is_empty()
        => Assert.Empty(CharacterWizardLogic.BuildBackgroundBonusMap(
            CharacterWizardLogic.BackgroundAbilitySplit.OneEachOfThree, null));

    // ===== ApplyBackgroundBonuses =====

    [Fact]
    public void ApplyBackgroundBonuses_adds_bonus_in_ability_order()
    {
        var map = new Dictionary<string, int> { ["strength"] = 2, ["wisdom"] = 1 };
        var result = CharacterWizardLogic.ApplyBackgroundBonuses(new[] { 10, 10, 10, 10, 10, 10 }, map);
        Assert.Equal(new[] { 12, 10, 10, 10, 11, 10 }, result);
    }

    [Fact]
    public void ApplyBackgroundBonuses_caps_at_20()
        => Assert.Equal(20, CharacterWizardLogic.ApplyBackgroundBonuses(
            new[] { 19, 10, 10, 10, 10, 10 }, new Dictionary<string, int> { ["strength"] = 2 })[0]);

    [Fact]
    public void ApplyBackgroundBonuses_untouched_ability_stays_at_base_even_above_20()
        // Il tetto vale solo per il punteggio TOCCATO da questo bonus: uno già oltre 20 da
        // un'altra fonte non viene ritoccato se il background non lo tocca.
        => Assert.Equal(24, CharacterWizardLogic.ApplyBackgroundBonuses(
            new[] { 24, 10, 10, 10, 10, 10 }, new Dictionary<string, int> { ["wisdom"] = 1 })[0]);

    [Fact]
    public void ApplyBackgroundBonuses_null_map_returns_base_unchanged()
        => Assert.Equal(new[] { 10, 11, 12, 13, 14, 15 },
            CharacterWizardLogic.ApplyBackgroundBonuses(new[] { 10, 11, 12, 13, 14, 15 }, null));

    [Fact]
    public void ApplyBackgroundBonuses_null_base_treats_missing_as_10()
        => Assert.Equal(12, CharacterWizardLogic.ApplyBackgroundBonuses(
            null, new Dictionary<string, int> { ["strength"] = 2 })[0]);

    // ===== FormatBackgroundAbilityChoice / ParseBackgroundAbilityChoice (round-trip) =====

    [Fact]
    public void FormatBackgroundAbilityChoice_formats_two_and_one()
        => Assert.Equal("strength:+2,wisdom:+1", CharacterWizardLogic.FormatBackgroundAbilityChoice(
            new Dictionary<string, int> { ["strength"] = 2, ["wisdom"] = 1 }));

    [Fact]
    public void FormatBackgroundAbilityChoice_empty_map_is_empty_string()
        => Assert.Equal(string.Empty, CharacterWizardLogic.FormatBackgroundAbilityChoice(new Dictionary<string, int>()));

    [Fact]
    public void FormatBackgroundAbilityChoice_null_map_is_empty_string()
        => Assert.Equal(string.Empty, CharacterWizardLogic.FormatBackgroundAbilityChoice(null));

    [Fact]
    public void ParseBackgroundAbilityChoice_round_trips_format()
    {
        var original = new Dictionary<string, int> { ["strength"] = 1, ["wisdom"] = 1, ["charisma"] = 1 };
        var text = CharacterWizardLogic.FormatBackgroundAbilityChoice(original);
        var parsed = CharacterWizardLogic.ParseBackgroundAbilityChoice(text);
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void ParseBackgroundAbilityChoice_ignores_unknown_keys()
    {
        var parsed = CharacterWizardLogic.ParseBackgroundAbilityChoice("pippo:+2,wisdom:+1");
        Assert.Equal(new Dictionary<string, int> { ["wisdom"] = 1 }, parsed);
    }

    [Fact]
    public void ParseBackgroundAbilityChoice_empty_or_null_is_empty_map()
    {
        Assert.Empty(CharacterWizardLogic.ParseBackgroundAbilityChoice(null));
        Assert.Empty(CharacterWizardLogic.ParseBackgroundAbilityChoice(""));
        Assert.Empty(CharacterWizardLogic.ParseBackgroundAbilityChoice("   "));
    }

    // ===== BaseArmorClass =====

    [Theory]
    [InlineData(10, 10)]
    [InlineData(14, 12)]  // mod +2
    [InlineData(8, 9)]    // mod -1
    public void BaseArmorClass_is_10_plus_dex_modifier(int dex, int expectedAc)
        => Assert.Equal(expectedAc, CharacterWizardLogic.BaseArmorClass(dex));

    // ===== ValidateSpeciesStep / ValidateClassStep / ValidateBackgroundStep =====

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateSpeciesStep_requires_a_name(string? name)
        => Assert.NotNull(CharacterWizardLogic.ValidateSpeciesStep(name));

    [Fact]
    public void ValidateSpeciesStep_valid_when_named()
        => Assert.Null(CharacterWizardLogic.ValidateSpeciesStep("Elfo"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateClassStep_requires_a_name(string? name)
        => Assert.NotNull(CharacterWizardLogic.ValidateClassStep(name));

    [Fact]
    public void ValidateClassStep_valid_when_named()
        => Assert.Null(CharacterWizardLogic.ValidateClassStep("Guerriero"));

    [Fact]
    public void ValidateBackgroundStep_always_valid()
    {
        Assert.Null(CharacterWizardLogic.ValidateBackgroundStep(null));
        Assert.Null(CharacterWizardLogic.ValidateBackgroundStep(""));
        Assert.Null(CharacterWizardLogic.ValidateBackgroundStep("Soldato"));
    }

    // ===== ValidateAbilitiesStep =====

    [Fact]
    public void ValidateAbilitiesStep_null_scores_is_invalid()
        => Assert.NotNull(CharacterWizardLogic.ValidateAbilitiesStep(CharacterWizardLogic.AbilityScoreMethod.Roll, null));

    [Fact]
    public void ValidateAbilitiesStep_out_of_1_30_range_is_invalid()
        => Assert.NotNull(CharacterWizardLogic.ValidateAbilitiesStep(
            CharacterWizardLogic.AbilityScoreMethod.Roll, new[] { 31, 10, 10, 10, 10, 10 }));

    [Fact]
    public void ValidateAbilitiesStep_roll_accepts_any_valid_range()
        => Assert.Null(CharacterWizardLogic.ValidateAbilitiesStep(
            CharacterWizardLogic.AbilityScoreMethod.Roll, new[] { 3, 18, 7, 12, 9, 16 }));

    [Fact]
    public void ValidateAbilitiesStep_standard_array_requires_exact_permutation()
        => Assert.NotNull(CharacterWizardLogic.ValidateAbilitiesStep(
            CharacterWizardLogic.AbilityScoreMethod.StandardArray, new[] { 15, 14, 13, 12, 10, 9 }));

    [Fact]
    public void ValidateAbilitiesStep_standard_array_valid_permutation()
        => Assert.Null(CharacterWizardLogic.ValidateAbilitiesStep(
            CharacterWizardLogic.AbilityScoreMethod.StandardArray, new[] { 8, 10, 12, 13, 14, 15 }));

    [Fact]
    public void ValidateAbilitiesStep_point_buy_over_budget_is_invalid()
        => Assert.NotNull(CharacterWizardLogic.ValidateAbilitiesStep(
            CharacterWizardLogic.AbilityScoreMethod.PointBuy, new[] { 15, 15, 15, 15, 15, 15 }));

    [Fact]
    public void ValidateAbilitiesStep_point_buy_out_of_8_15_is_invalid()
        => Assert.NotNull(CharacterWizardLogic.ValidateAbilitiesStep(
            CharacterWizardLogic.AbilityScoreMethod.PointBuy, new[] { 16, 10, 10, 10, 10, 10 }));

    [Fact]
    public void ValidateAbilitiesStep_point_buy_within_budget_is_valid()
        => Assert.Null(CharacterWizardLogic.ValidateAbilitiesStep(
            CharacterWizardLogic.AbilityScoreMethod.PointBuy, new[] { 15, 14, 13, 12, 10, 8 }));

    // ===== ValidateDetailsStep =====

    [Fact]
    public void ValidateDetailsStep_null_draft_is_invalid()
        => Assert.NotNull(CharacterWizardLogic.ValidateDetailsStep(null));

    [Fact]
    public void ValidateDetailsStep_requires_name()
        => Assert.NotNull(CharacterWizardLogic.ValidateDetailsStep(new Character { Name = "", MaxHitPoints = 10 }));

    [Fact]
    public void ValidateDetailsStep_requires_positive_max_hp()
        => Assert.NotNull(CharacterWizardLogic.ValidateDetailsStep(new Character { Name = "Aria", MaxHitPoints = 0 }));

    [Fact]
    public void ValidateDetailsStep_rejects_negative_ac()
        => Assert.NotNull(CharacterWizardLogic.ValidateDetailsStep(
            new Character { Name = "Aria", MaxHitPoints = 10, ArmorClass = -1 }));

    [Fact]
    public void ValidateDetailsStep_valid_character_passes()
        => Assert.Null(CharacterWizardLogic.ValidateDetailsStep(
            new Character { Name = "Aria", MaxHitPoints = 10, ArmorClass = 12 }));

    // ===== ValidateSummaryStep =====

    // Punteggi validi con cui isolare i rami non legati alle caratteristiche.
    private static int[] ArrayStandardValido() => (int[])CharacterWizardLogic.StandardArrayScores.Clone();

    private static string? Riepilogo(Character? draft, int[]? scores = null)
        => CharacterWizardLogic.ValidateSummaryStep(
            draft, CharacterWizardLogic.AbilityScoreMethod.StandardArray, scores ?? ArrayStandardValido());

    [Fact]
    public void ValidateSummaryStep_null_draft_is_invalid()
        => Assert.NotNull(Riepilogo(null));

    [Fact]
    public void ValidateSummaryStep_missing_race_is_invalid()
        => Assert.NotNull(Riepilogo(
            new Character { Name = "Aria", Class = "Guerriero", Race = "", MaxHitPoints = 10 }));

    [Fact]
    public void ValidateSummaryStep_missing_class_is_invalid()
        => Assert.NotNull(Riepilogo(
            new Character { Name = "Aria", Class = "", Race = "Elfo", MaxHitPoints = 10 }));

    [Fact]
    public void ValidateSummaryStep_complete_character_is_valid()
        => Assert.Null(Riepilogo(
            new Character { Name = "Aria", Class = "Guerriero", Race = "Elfo", MaxHitPoints = 10, ArmorClass = 12 }));

    [Fact]
    public void ValidateSummaryStep_rejects_ability_scores_that_skipped_step_four()
    {
        // Il difetto che questo test presidia: dai pallini di passo si torna al passo 4, si
        // assegnano sei 15, si risale al 6 senza passare da "Avanti" e si salva. Prima della
        // correzione il riepilogo non guardava le caratteristiche e il personaggio veniva creato.
        var draft = new Character { Name = "Aria", Class = "Guerriero", Race = "Elfo", MaxHitPoints = 10, ArmorClass = 12 };
        var seiQuindici = new[] { 15, 15, 15, 15, 15, 15 };

        Assert.NotNull(Riepilogo(draft, seiQuindici));
    }

    [Fact]
    public void ValidateSummaryStep_rejects_point_buy_over_budget()
    {
        var draft = new Character { Name = "Aria", Class = "Guerriero", Race = "Elfo", MaxHitPoints = 10, ArmorClass = 12 };

        // 6 × 15 = 54 punti spesi su 27 disponibili.
        Assert.NotNull(CharacterWizardLogic.ValidateSummaryStep(
            draft, CharacterWizardLogic.AbilityScoreMethod.PointBuy, new[] { 15, 15, 15, 15, 15, 15 }));
    }

    // ===== SpeedInMeters =====

    [Theory]
    [InlineData(30, "ft", 9)]    // la velocità della quasi totalità delle specie SRD
    [InlineData(35, "ft", 11)]   // Golia: 10,5 m sul manuale, arrotondati perché la colonna è int
    [InlineData(25, "ft", 8)]    // 7,5 m arrotondati
    [InlineData(40, "ft", 12)]
    [InlineData(9, "m", 9)]      // già metrico: nessuna conversione
    [InlineData(12, "M", 12)]    // unità non sensibile alle maiuscole
    public void SpeedInMeters_converts_feet_and_leaves_metres_alone(int valore, string unita, int atteso)
        => Assert.Equal(atteso, CharacterWizardLogic.SpeedInMeters(valore, unita));

    [Theory]
    [InlineData("feet")]
    [InlineData("foot")]
    [InlineData("piedi")]
    [InlineData("FT")]
    public void SpeedInMeters_recognises_every_form_of_feet(string unita)
        // Le forme estese le riconosce PackageRowMerge.UnitaValida, da cui questa funzione dipende:
        // se cadessero nel fallback, 30 piedi diventerebbero 30 metri in silenzio.
        => Assert.Equal(9, CharacterWizardLogic.SpeedInMeters(30, unita));

    [Fact]
    public void SpeedInMeters_agrees_with_the_import_on_unknown_units()
    {
        // Un'unità ignota (o assente) è trattata come metrica, cioè NON convertita: non perché sia
        // più probabile, ma perché è ciò che fa PackageRowMerge.UnitaValida quando la stessa voce
        // viene importata in campagna. Due letture diverse dello stesso dato — importato metrico e
        // convertito come piedi — darebbero al PG velocità diverse a seconda della strada percorsa.
        Assert.Equal(30, CharacterWizardLogic.SpeedInMeters(30, null));
        Assert.Equal(30, CharacterWizardLogic.SpeedInMeters(30, "leghe"));

        // Il legame con la fonte unica, asserito e non solo commentato.
        Assert.Equal("m", PackageRowMerge.UnitaValida(null));
        Assert.Equal("ft", PackageRowMerge.UnitaValida("piedi"));
    }

    // ===== InitialScoresFor / ClampPointBuy =====

    [Fact]
    public void InitialScoresFor_standard_array_is_a_valid_assignment()
        => Assert.True(CharacterWizardLogic.IsValidStandardArrayAssignment(
            CharacterWizardLogic.InitialScoresFor(CharacterWizardLogic.AbilityScoreMethod.StandardArray)));

    [Fact]
    public void InitialScoresFor_point_buy_starts_within_budget()
    {
        var scores = CharacterWizardLogic.InitialScoresFor(CharacterWizardLogic.AbilityScoreMethod.PointBuy);
        Assert.Equal(6, scores.Length);
        Assert.Equal(CharacterWizardLogic.PointBuyBudget, CharacterWizardLogic.PointBuyRemaining(scores));
    }

    [Fact]
    public void InitialScoresFor_returns_a_fresh_array_each_time()
    {
        // Restituire l'array condiviso farebbe modificare StandardArrayScores al primo tocco
        // dell'utente, e da lì ogni personaggio successivo partirebbe da valori alterati.
        var primo = CharacterWizardLogic.InitialScoresFor(CharacterWizardLogic.AbilityScoreMethod.StandardArray);
        primo[0] = 3;

        var secondo = CharacterWizardLogic.InitialScoresFor(CharacterWizardLogic.AbilityScoreMethod.StandardArray);
        Assert.Equal(CharacterWizardLogic.StandardArrayScores[0], secondo[0]);
    }

    [Fact]
    public void ClampPointBuy_respects_the_cost_table_bounds()
    {
        Assert.Equal(CharacterWizardLogic.PointBuyMin, CharacterWizardLogic.ClampPointBuy(3));
        Assert.Equal(CharacterWizardLogic.PointBuyMax, CharacterWizardLogic.ClampPointBuy(20));
        Assert.Equal(12, CharacterWizardLogic.ClampPointBuy(12));

        // I limiti devono restare quelli della tabella dei costi, non due numeri paralleli.
        Assert.NotNull(CharacterWizardLogic.PointBuyCost(CharacterWizardLogic.PointBuyMin));
        Assert.NotNull(CharacterWizardLogic.PointBuyCost(CharacterWizardLogic.PointBuyMax));
    }
}
