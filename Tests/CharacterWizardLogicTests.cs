using DndCompanion.Models;
using DndCompanion.Models.Packages;
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

    // =====================================================================
    // ApplySpellSlotsLevel1 — stessa fonte del planner (ClassProgression.SlotFinoAl), non un
    // calcolo proprio del wizard.
    // =====================================================================

    private static string TabellaMago()
        // Stesso formato di ClassProgression.Serializza/Leggi: L1 dà 2 slot di 1° cerchio.
        => "L1 — Recupero arcano · Slot 2";

    [Fact]
    public void ApplySpellSlotsLevel1_scrive_gli_slot_del_primo_livello()
    {
        var draft = new Character();
        CharacterWizardLogic.ApplySpellSlotsLevel1(draft, TabellaMago());

        Assert.Equal(2, draft.SpellSlots1Max);
        Assert.Equal(0, draft.SpellSlots2Max);
        Assert.Equal(0, draft.SpellSlots9Max);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("prosa libera senza il formato L<n> — ...")]
    public void ApplySpellSlotsLevel1_senza_tabella_non_scrive_slot(string? tabella)
    {
        var draft = new Character();
        CharacterWizardLogic.ApplySpellSlotsLevel1(draft, tabella);

        Assert.Equal(0, draft.SpellSlots1Max);
        Assert.Equal(0, draft.SpellSlots9Max);
    }

    [Fact]
    public void ApplySpellSlotsLevel1_azzera_gli_slot_di_una_classe_abbandonata()
    {
        // Cambio di classe: da un Mago (2 slot di 1°) a un Guerriero (nessuna tabella). Gli slot
        // del Mago non devono restare addosso al draft — stesso principio idempotente di
        // ApplyClassSaveProficiencies nel wizard.
        var draft = new Character { SpellSlots1Max = 2 };
        CharacterWizardLogic.ApplySpellSlotsLevel1(draft, null);

        Assert.Equal(0, draft.SpellSlots1Max);
    }

    [Fact]
    public void ApplySpellSlotsLevel1_ignora_i_cerchi_oltre_il_nono()
    {
        // Dato malformato (più di nove slot dichiarati): Character conosce solo nove cerchi.
        var tabella = "L1 — Privilegio · Slot 1/1/1/1/1/1/1/1/1/1/1";
        var draft = new Character();

        CharacterWizardLogic.ApplySpellSlotsLevel1(draft, tabella);

        Assert.Equal(1, draft.SpellSlots9Max);
    }

    // =====================================================================
    // ApplySpellcastingAbility — chiave inglese minuscola, MAI il nome italiano (trappola §10).
    // =====================================================================

    [Fact]
    public void ApplySpellcastingAbility_scrive_la_chiave_inglese_per_una_classe_incantatrice()
    {
        var draft = new Character();
        CharacterWizardLogic.ApplySpellcastingAbility(draft, "Mago");

        Assert.Equal("intelligence", draft.SpellcastingAbility);
    }

    [Fact]
    public void ApplySpellcastingAbility_non_scrive_nulla_per_una_classe_non_incantatrice()
    {
        var draft = new Character();
        CharacterWizardLogic.ApplySpellcastingAbility(draft, "Guerriero");

        Assert.Null(draft.SpellcastingAbility);
    }

    [Fact]
    public void ApplySpellcastingAbility_non_sovrascrive_un_valore_gia_presente()
    {
        // "Solo se vuota": chi vuole ricalcolarla (cambio di classe) deve azzerarla prima di
        // chiamare — è il caller, non questa funzione, a decidere quando il campo è da riscrivere.
        var draft = new Character { SpellcastingAbility = "wisdom" };
        CharacterWizardLogic.ApplySpellcastingAbility(draft, "Mago");

        Assert.Equal("wisdom", draft.SpellcastingAbility);
    }

    // =====================================================================
    // RisolviVincoloAbilita / RisolviCompetenzeConcesse — stessa precedenza già in uso nel wizard
    // per dado vita/caratteristica principale: la riga di campagna vince quando esiste.
    // =====================================================================

    [Fact]
    public void RisolviVincoloAbilita_legge_il_testo_libero_della_riga_di_campagna()
    {
        var classeDb = new CharacterClass { SkillChoices = "2 fra: Arcano, Storia" };
        var vincolo = CharacterWizardLogic.RisolviVincoloAbilita(classeDb, null);

        Assert.NotNull(vincolo);
        Assert.Equal(2, vincolo!.Quante);
        Assert.Equal(new[] { SkillType.Arcana, SkillType.History }, vincolo.Fra);
    }

    [Fact]
    public void RisolviVincoloAbilita_la_riga_di_campagna_vince_anche_se_vuota()
    {
        // La riga di campagna esiste (è la classe di QUESTO tavolo): il pacchetto non è un
        // ripiego valido anche se avrebbe un vincolo perfettamente utilizzabile.
        var classeDb = new CharacterClass { SkillChoices = "" };
        var classePacchetto = new PackageClass
        {
            SkillChoices = new PackageSkillChoices { Count = 2, From = new List<string> { "Arcano", "Storia" } }
        };

        Assert.Null(CharacterWizardLogic.RisolviVincoloAbilita(classeDb, classePacchetto));
    }

    [Fact]
    public void RisolviVincoloAbilita_senza_riga_di_campagna_ripiega_sul_pacchetto()
    {
        var classePacchetto = new PackageClass
        {
            SkillChoices = new PackageSkillChoices { Count = 2, From = new List<string> { "Arcano", "Storia" } }
        };

        var vincolo = CharacterWizardLogic.RisolviVincoloAbilita(null, classePacchetto);

        Assert.NotNull(vincolo);
        Assert.Equal(new[] { SkillType.Arcana, SkillType.History }, vincolo!.Fra);
    }

    [Fact]
    public void RisolviVincoloAbilita_senza_nessuna_fonte_torna_null()
        => Assert.Null(CharacterWizardLogic.RisolviVincoloAbilita(null, null));

    [Fact]
    public void RisolviCompetenzeConcesse_legge_il_testo_della_riga_di_campagna()
    {
        var backgroundDb = new Background { SkillProficiencies = "Intuizione, Sopravvivenza" };
        var concesse = CharacterWizardLogic.RisolviCompetenzeConcesse(backgroundDb, null);

        Assert.Equal(new[] { SkillType.Insight, SkillType.Survival }, concesse);
    }

    [Fact]
    public void RisolviCompetenzeConcesse_la_riga_di_campagna_vince_anche_se_vuota()
    {
        var backgroundDb = new Background { SkillProficiencies = "" };
        var backgroundPacchetto = new PackageBackground
        {
            SkillProficiencies = new List<string> { "Intuizione" }
        };

        Assert.Empty(CharacterWizardLogic.RisolviCompetenzeConcesse(backgroundDb, backgroundPacchetto));
    }

    [Fact]
    public void RisolviCompetenzeConcesse_senza_riga_di_campagna_ripiega_sul_pacchetto()
    {
        var backgroundPacchetto = new PackageBackground { SkillProficiencies = new List<string> { "Intuizione" } };
        var concesse = CharacterWizardLogic.RisolviCompetenzeConcesse(null, backgroundPacchetto);

        Assert.Equal(new[] { SkillType.Insight }, concesse);
    }

    // =====================================================================
    // ApplicaCompetenze — riscrive sempre le 18 competenze da capo dalle due fonti indipendenti
    // (§E: i bool di Character non ricordano da dove viene ciascuna spunta).
    // =====================================================================

    [Fact]
    public void ApplicaCompetenze_marca_competenti_le_scelte_e_le_concesse()
    {
        var draft = new Character();
        CharacterWizardLogic.ApplicaCompetenze(
            draft,
            scelte: new[] { SkillType.Athletics },
            concesse: new[] { SkillType.Insight });

        Assert.True(draft.ProfAthletics);
        Assert.True(draft.ProfInsight);
        Assert.False(draft.ProfStealth);
    }

    [Fact]
    public void ApplicaCompetenze_una_competenza_fuori_da_entrambi_gli_insiemi_viene_rimossa()
    {
        // Full recompute, non un delta: un bool preesistente sul draft che non viene più
        // giustificato da nessuna delle due fonti deve tornare falso.
        var draft = new Character { ProfAthletics = true };
        CharacterWizardLogic.ApplicaCompetenze(draft, Array.Empty<SkillType>(), Array.Empty<SkillType>());

        Assert.False(draft.ProfAthletics);
    }

    [Fact]
    public void ApplicaCompetenze_una_scelta_ancora_concessa_dal_background_resta_competente()
    {
        // Il punto più facile da sbagliare (§E): la classe non scelge più questa abilità (le
        // "scelte" non la contengono), ma il background la concede ancora — deve restare vera.
        var draft = new Character();
        CharacterWizardLogic.ApplicaCompetenze(
            draft, scelte: Array.Empty<SkillType>(), concesse: new[] { SkillType.Insight });

        Assert.True(draft.ProfInsight);
    }

    // =====================================================================
    // ValidateClassSkillChoices — D11: blocca "Avanti" solo quando esiste un vincolo.
    // =====================================================================

    [Fact]
    public void ValidateClassSkillChoices_vincolo_assente_non_blocca_mai()
    {
        var esitoIncompleto = new EsitoScelteAbilita(false, Array.Empty<SkillType>(), "irrilevante");
        Assert.Null(CharacterWizardLogic.ValidateClassSkillChoices(null, esitoIncompleto));
    }

    [Fact]
    public void ValidateClassSkillChoices_vincolo_presente_e_incompleto_blocca()
    {
        var vincolo = new VincoloAbilita(2, new[] { SkillType.Arcana, SkillType.History });
        var esito = new EsitoScelteAbilita(false, Array.Empty<SkillType>(), "Scegline ancora 2.");

        Assert.Equal("Scegline ancora 2.", CharacterWizardLogic.ValidateClassSkillChoices(vincolo, esito));
    }

    [Fact]
    public void ValidateClassSkillChoices_vincolo_presente_e_completo_non_blocca()
    {
        var vincolo = new VincoloAbilita(2, new[] { SkillType.Arcana, SkillType.History });
        var esito = new EsitoScelteAbilita(true, Array.Empty<SkillType>(), null);

        Assert.Null(CharacterWizardLogic.ValidateClassSkillChoices(vincolo, esito));
    }

    // =====================================================================
    // Passo Progressione (2026-08-06): funzioni pure che decidono COSA MOSTRARE di una
    // TappaCreazione già calcolata da CreationChain.Deriva — nessun ricalcolo dei suoi numeri qui.
    // =====================================================================

    private static TappaCreazione TappaDiProva(
        bool richiedeScelte = false, bool haScelteFacoltative = false,
        int pfAttuale = 10, int pfProposto = 16,
        int competenzaAttuale = 2, int competenzaProposta = 2,
        string dadoVita = "d10",
        IReadOnlyList<string>? privilegi = null,
        IReadOnlyList<Decisione>? decisioni = null)
    {
        var piano = new LevelUpPlan(
            Classe: "Prova", LivelloDa: 1, LivelloA: 2, DadoVita: dadoVita, MediaDado: 6,
            PuntiFeritaMax: new Proposta<int>(pfAttuale, pfProposto),
            PuntiFeritaCorrenti: new Proposta<int>(pfAttuale, pfProposto),
            DadiVita: new Proposta<string>("1" + dadoVita, "2" + dadoVita),
            SlotMax: new Proposta<IReadOnlyList<int>>(new int[9], new int[9]),
            CaratteristicaIncantatore: new Proposta<string>("", ""),
            BonusCompetenza: new Proposta<int>(competenzaAttuale, competenzaProposta),
            PrivilegiOttenuti: privilegi ?? Array.Empty<string>(),
            Decisioni: decisioni ?? Array.Empty<Decisione>(),
            Avvisi: Array.Empty<string>(),
            CerchioSbloccato: null);
        return new TappaCreazione(2, piano, richiedeScelte, haScelteFacoltative);
    }

    // ----- StatoDiTappa -----

    [Fact]
    public void StatoDiTappa_nessuna_decisione_e_confermata()
        => Assert.Equal(CharacterWizardLogic.StatoTappa.Confermata,
            CharacterWizardLogic.StatoDiTappa(TappaDiProva()));

    [Fact]
    public void StatoDiTappa_richiede_scelte_vince_su_facoltativa()
        => Assert.Equal(CharacterWizardLogic.StatoTappa.RichiedeScelte,
            CharacterWizardLogic.StatoDiTappa(TappaDiProva(richiedeScelte: true, haScelteFacoltative: true)));

    [Fact]
    public void StatoDiTappa_solo_facoltativa()
        => Assert.Equal(CharacterWizardLogic.StatoTappa.Facoltativa,
            CharacterWizardLogic.StatoDiTappa(TappaDiProva(haScelteFacoltative: true)));

    // ----- RiepilogoAutomatico -----

    [Fact]
    public void RiepilogoAutomatico_mostra_il_dado_vita_quando_la_competenza_non_cambia()
    {
        var tappa = TappaDiProva(pfAttuale: 10, pfProposto: 16, competenzaAttuale: 2, competenzaProposta: 2, dadoVita: "d12");
        Assert.Equal("+6 PF, dado vita d12", CharacterWizardLogic.RiepilogoAutomatico(tappa));
    }

    [Fact]
    public void RiepilogoAutomatico_mostra_la_competenza_quando_sale()
    {
        var tappa = TappaDiProva(pfAttuale: 30, pfProposto: 36, competenzaAttuale: 2, competenzaProposta: 3);
        Assert.Equal("+6 PF, competenza +1", CharacterWizardLogic.RiepilogoAutomatico(tappa));
    }

    // ----- RiepilogoScelte -----

    [Fact]
    public void RiepilogoScelte_sottoclasse_ha_etichetta_dedicata()
    {
        var decisione = new DecisioneFraOpzioni("L3:sottoclasse", "Sottoclasse del Barbaro",
            new[] { new OpzioneDecisione("Prova", "Descrizione") }, 1);
        var tappa = TappaDiProva(richiedeScelte: true, decisioni: new Decisione[] { decisione });

        Assert.Equal("Scegli la sottoclasse", CharacterWizardLogic.RiepilogoScelte(tappa));
    }

    [Fact]
    public void RiepilogoScelte_unisce_talento_e_punteggi_distinti()
    {
        var talento = new DecisioneFraOpzioni("L4:talento", "Incremento punteggio caratteristica",
            new[] { new OpzioneDecisione("ASI", "Descrizione") }, 1);
        var punteggi = new DecisionePunteggi("L4:talento/punteggi", "Ripartisci l'incremento",
            new Dictionary<string, int>());
        var tappa = TappaDiProva(richiedeScelte: true, decisioni: new Decisione[] { talento, punteggi });

        // MINORE 8 del gate del 2026-08-06: niente più il ripiego generico "Scegli un talento" — il
        // titolo del privilegio stesso, come già per le decisioni libere.
        Assert.Equal("Incremento punteggio caratteristica · Ripartisci i punteggi", CharacterWizardLogic.RiepilogoScelte(tappa));
    }

    [Fact]
    public void RiepilogoScelte_stile_di_combattimento_non_usa_piu_letichetta_generica_da_talento()
    {
        // Il caso concreto del MINORE 8: LevelUpPlanner assegna la stessa chiave "L{n}:talento" allo
        // stile di combattimento (Ranger, Paladino...) e al dono epico, non solo al talento generico
        // — il ripiego "Scegli un talento" li confondeva tutti nella stessa riga, mentre il pannello
        // sotto diceva "Stile di combattimento".
        var stile = new DecisioneFraOpzioni("L2:talento", "Stile di combattimento",
            new[] { new OpzioneDecisione("Difesa", "Descrizione") }, 1);
        var tappa = TappaDiProva(richiedeScelte: true, decisioni: new Decisione[] { stile });

        Assert.Equal("Stile di combattimento", CharacterWizardLogic.RiepilogoScelte(tappa));
    }

    [Fact]
    public void RiepilogoScelte_decisione_libera_usa_il_proprio_titolo()
    {
        var libera = new DecisioneLibera("L5:libera/dono", "Un privilegio a scelta libera", "Annota qui.");
        var tappa = TappaDiProva(haScelteFacoltative: true, decisioni: new Decisione[] { libera });

        Assert.Equal("Un privilegio a scelta libera", CharacterWizardLogic.RiepilogoScelte(tappa));
    }

    [Fact]
    public void RiepilogoScelte_decisione_gia_risposta_non_si_ripropone_come_aperta()
    {
        // MINORE 3 del secondo giro del gate del 2026-08-06: il talento è già stato scelto (nasce
        // la decisione FIGLIA sulla ripartizione dei punteggi, che tiene la tappa "richiede
        // scelte" finché non è completa) — la riga non deve più dire "Incremento punteggio
        // caratteristica" come se il talento fosse ancora da fare: deve mostrare COSA si è scelto,
        // esattamente come già fa RiepilogoConfermata per le tappe confermate.
        var talento = new DecisioneFraOpzioni("L4:talento", "Incremento punteggio caratteristica",
            new[] { new OpzioneDecisione("Incremento del Punteggio di Caratteristica", "Descrizione") }, 1);
        var punteggi = new DecisionePunteggi("L4:talento/punteggi", "Ripartisci l'incremento",
            new Dictionary<string, int>());
        var tappa = TappaDiProva(richiedeScelte: true, decisioni: new Decisione[] { talento, punteggi });

        var risposte = new Dictionary<string, Risposta>(StringComparer.Ordinal)
        {
            ["L4:talento"] = new Risposta { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
            // "L4:talento/punteggi" resta senza risposta: ancora aperta, deve restare in etichetta breve.
        };

        Assert.Equal("Incremento del Punteggio di Caratteristica · Ripartisci i punteggi",
            CharacterWizardLogic.RiepilogoScelte(tappa, risposte));
    }

    // ----- ScelteRestanti -----

    [Fact]
    public void ScelteRestanti_conta_solo_le_tappe_bloccanti()
    {
        var tappe = new[]
        {
            TappaDiProva(),
            TappaDiProva(richiedeScelte: true),
            TappaDiProva(haScelteFacoltative: true),
            TappaDiProva(richiedeScelte: true),
        };

        Assert.Equal(2, CharacterWizardLogic.ScelteRestanti(tappe));
    }

    // ----- ValidateProgressionStep -----

    [Fact]
    public void ValidateProgressionStep_catena_non_attiva_non_blocca_mai()
        => Assert.Null(CharacterWizardLogic.ValidateProgressionStep(catenaAttiva: false, completa: false, motivo: "irrilevante"));

    [Fact]
    public void ValidateProgressionStep_catena_attiva_e_incompleta_blocca_col_motivo()
        => Assert.Equal("Resta 1 scelta da fare (livello 3).",
            CharacterWizardLogic.ValidateProgressionStep(catenaAttiva: true, completa: false, motivo: "Resta 1 scelta da fare (livello 3)."));

    [Fact]
    public void ValidateProgressionStep_catena_attiva_e_completa_non_blocca()
        => Assert.Null(CharacterWizardLogic.ValidateProgressionStep(catenaAttiva: true, completa: true, motivo: null));

    [Fact]
    public void ValidateProgressionStep_motivo_assente_usa_un_messaggio_di_ripiego()
        => Assert.NotNull(CharacterWizardLogic.ValidateProgressionStep(catenaAttiva: true, completa: false, motivo: null));

    // ----- TroncaDescrizione -----

    [Fact]
    public void TroncaDescrizione_testo_corto_non_si_tocca()
        => Assert.Equal("breve", CharacterWizardLogic.TroncaDescrizione("breve", soglia: 220));

    [Fact]
    public void TroncaDescrizione_testo_lungo_si_tronca_con_ellissi()
    {
        var lungo = new string('x', 300);
        var risultato = CharacterWizardLogic.TroncaDescrizione(lungo, soglia: 220);

        Assert.EndsWith("…", risultato);
        Assert.True(risultato.Length <= 222); // 220 + ellissi, TrimEnd può accorciare ma non allungare
    }

    [Fact]
    public void TroncaDescrizione_null_diventa_stringa_vuota()
        => Assert.Equal(string.Empty, CharacterWizardLogic.TroncaDescrizione(null));

    // =====================================================================
    // TrovaPerNomeNormalizzato — SERIO 1 del gate del 2026-08-06: la causa a monte era la ricerca
    // del dado vita per uguaglianza esatta mentre ClassProgression.Risolvi trova la tabella dei
    // livelli normalizzando maiuscole/accenti. Stessa normalizzazione qui.
    // =====================================================================

    [Fact]
    public void TrovaPerNomeNormalizzato_ignora_maiuscole()
    {
        var classi = new[] { new PackageClass { Name = "Mago", HitDie = "d6" } };

        var trovata = CharacterWizardLogic.TrovaPerNomeNormalizzato(classi, c => c.Name, "mago", c => c.Id);

        Assert.NotNull(trovata);
        Assert.Equal("d6", trovata!.HitDie);
    }

    [Fact]
    public void TrovaPerNomeNormalizzato_ignora_gli_accenti()
    {
        var razze = new[] { new Race { Name = "Invisibilità" } };

        Assert.NotNull(CharacterWizardLogic.TrovaPerNomeNormalizzato(razze, r => r.Name, "INVISIBILITA", r => r.Id));
    }

    [Fact]
    public void TrovaPerNomeNormalizzato_nessuna_voce_torna_null()
        => Assert.Null(CharacterWizardLogic.TrovaPerNomeNormalizzato<PackageClass>(null, c => c.Name, "mago", c => c.Id));

    [Fact]
    public void TrovaPerNomeNormalizzato_nome_assente_torna_null()
    {
        var classi = new[] { new PackageClass { Name = "Mago" } };
        Assert.Null(CharacterWizardLogic.TrovaPerNomeNormalizzato(classi, c => c.Name, null, c => c.Id));
    }

    // Stesso difetto latente per specie e background (CharacterWizard.razor: SelectedRaceDb/
    // SelectedRacePackage/SelectedBackgroundDb/SelectedBackgroundPackage cercavano per uguaglianza
    // esatta del nome mentre la classe già usava TrovaPerNomeNormalizzato). Con l'uguaglianza
    // esatta una specie o un background scritti con un casing o un accento diverso dal manuale
    // perdevano descrizione, tratti, velocità e — per il background — le competenze CONCESSE e le
    // caratteristiche: dati che finiscono davvero sul personaggio salvato, non solo testo mostrato.

    [Fact]
    public void TrovaPerNomeNormalizzato_specie_ignora_maiuscole()
    {
        var razze = new[] { new Race { Name = "Elfo", Speed = 30 } };

        var trovata = CharacterWizardLogic.TrovaPerNomeNormalizzato(razze, r => r.Name, "elfo", r => r.Id);

        Assert.NotNull(trovata);
        Assert.Equal(30, trovata!.Speed);
    }

    [Fact]
    public void TrovaPerNomeNormalizzato_specie_di_pacchetto_ignora_gli_accenti()
    {
        var specie = new[] { new PackageSpecies { Name = "Velocità", Traits = "Resistenza nanica" } };

        var trovata = CharacterWizardLogic.TrovaPerNomeNormalizzato(specie, s => s.Name, "VELOCITA", s => s.Id);

        Assert.NotNull(trovata);
        Assert.Equal("Resistenza nanica", trovata!.Traits);
    }

    [Fact]
    public void TrovaPerNomeNormalizzato_background_ignora_maiuscole()
    {
        var backgrounds = new[] { new Background { Name = "Eremita", SkillProficiencies = "Medicina, Religione" } };

        var trovato = CharacterWizardLogic.TrovaPerNomeNormalizzato(backgrounds, b => b.Name, "eremita", b => b.Id);

        Assert.NotNull(trovato);
        Assert.Equal("Medicina, Religione", trovato!.SkillProficiencies);
    }

    [Fact]
    public void TrovaPerNomeNormalizzato_background_di_pacchetto_ignora_gli_accenti()
    {
        var backgrounds = new[]
        {
            new PackageBackground { Name = "Città natia", SkillProficiencies = new List<string> { "Intuizione" } }
        };

        var trovato = CharacterWizardLogic.TrovaPerNomeNormalizzato(backgrounds, b => b.Name, "CITTA NATIA", b => b.Id);

        Assert.NotNull(trovato);
        Assert.Equal(new List<string> { "Intuizione" }, trovato!.SkillProficiencies);
    }

    // ----- MINORE 2 del secondo giro del gate del 2026-08-06: due righe omonime per
    // casing/accenti condividono la stessa chiave normalizzata, e i repository leggono senza
    // Order — l'ordine con cui PostgREST le restituisce non è garantito. Lo spareggio (match
    // esatto, poi Id ordinale) deve scegliere sempre la stessa riga, indipendentemente
    // dall'ordine di arrivo. -----

    [Fact]
    public void TrovaPerNomeNormalizzato_omonimi_per_casing_preferisce_il_match_esatto()
    {
        // "Città natia" e "Citta natia" normalizzano alla stessa chiave. Si cerca "Citta natia"
        // (senza accento): deve vincere la riga con quel nome ESATTO, non l'altra solo perché
        // condivide la chiave normalizzata.
        var backgrounds = new[]
        {
            new Background { Id = "b2", Name = "Città natia", SkillProficiencies = "Storia" },
            new Background { Id = "b1", Name = "Citta natia", SkillProficiencies = "Intuizione" },
        };

        var trovato = CharacterWizardLogic.TrovaPerNomeNormalizzato(backgrounds, b => b.Name, "Citta natia", b => b.Id);

        Assert.NotNull(trovato);
        Assert.Equal("b1", trovato!.Id);
        Assert.Equal("Intuizione", trovato.SkillProficiencies);
    }

    [Fact]
    public void TrovaPerNomeNormalizzato_omonimi_senza_match_esatto_sceglie_l_id_minore_in_modo_stabile()
    {
        // Nessuna delle due righe combacia col nome esatto cercato ("CITTA NATIA" tutto
        // maiuscolo): lo spareggio ricade sul minore ordinale per Id, e l'esito non cambia
        // scambiando l'ordine delle due righe in ingresso — l'ordine di lettura dal database non
        // è garantito, il risultato deve restare lo stesso.
        var a = new Background { Id = "zeta", Name = "Città natia", SkillProficiencies = "Storia" };
        var b = new Background { Id = "alfa", Name = "Citta natia", SkillProficiencies = "Intuizione" };

        var primo = CharacterWizardLogic.TrovaPerNomeNormalizzato(new[] { a, b }, x => x.Name, "CITTA NATIA", x => x.Id);
        var secondo = CharacterWizardLogic.TrovaPerNomeNormalizzato(new[] { b, a }, x => x.Name, "CITTA NATIA", x => x.Id);

        Assert.Equal("alfa", primo!.Id);
        Assert.Equal("alfa", secondo!.Id);
    }

    // =====================================================================
    // ApplicaTiriSalvezze — promossa dal wizard (SERIO 4 del gate del 2026-08-06).
    // =====================================================================

    [Fact]
    public void ApplicaTiriSalvezze_scrive_le_competenze_dal_testo_e_azzera_le_altre()
    {
        var target = new Character { ProfSaveWisdom = true };
        CharacterWizardLogic.ApplicaTiriSalvezze(target, "Forza, Costituzione");

        Assert.True(target.ProfSaveStrength);
        Assert.True(target.ProfSaveConstitution);
        Assert.False(target.ProfSaveWisdom); // classe abbandonata: non resta addosso
        Assert.False(target.ProfSaveDexterity);
    }

    [Fact]
    public void ApplicaTiriSalvezze_testo_vuoto_azzera_tutto()
    {
        var target = new Character { ProfSaveCharisma = true };
        CharacterWizardLogic.ApplicaTiriSalvezze(target, null);

        Assert.False(target.ProfSaveCharisma);
    }

    // =====================================================================
    // AssemblaBaseline — promossa dal wizard (SERIO 4). SERIO 1: il seed di PF/dadi vita non deve
    // MAI scrivere la sentinella (0, "") di SuggestMaxHp/BuildHitDice quando il dado vita non si
    // risolve, altrimenti il baseline non è più salvabile (ValidateSummaryStep rifiuta PF < 1).
    // =====================================================================

    [Fact]
    public void AssemblaBaseline_riporta_il_livello_a_1_e_sincronizza_i_punteggi_finali()
    {
        var draft = new Character { Level = 5, Strength = 8, MaxHitPoints = 40, HitDiceMax = "5d10" };
        var finalScores = new[] { 16, 12, 14, 10, 10, 10 };

        var baseline = CharacterWizardLogic.AssemblaBaseline(
            draft, finalScores, backgroundBonusMap: null, savingThrowsText: "Forza, Destrezza",
            catenaUtile: true, classHitDie: "d10");

        Assert.Equal(1, baseline.Level);
        Assert.Equal(16, baseline.Strength);
        Assert.Equal(14, baseline.Constitution);
        Assert.True(baseline.ProfSaveStrength);
        Assert.True(baseline.ProfSaveDexterity);
        Assert.False(baseline.ProfSaveConstitution);
        // PF/dado del 1° livello dalla FORMULA (dado pieno 10 + modCOS +2 = 12), non quelli del
        // draft (5d10/40): la catena userà questi come seed.
        Assert.Equal(12, baseline.MaxHitPoints);
        Assert.Equal("1d10", baseline.HitDiceMax);
    }

    [Fact]
    public void AssemblaBaseline_catena_non_utile_lascia_pf_e_dadi_vita_del_draft()
    {
        var draft = new Character { MaxHitPoints = 10, HitDiceMax = "1d8" };
        var baseline = CharacterWizardLogic.AssemblaBaseline(
            draft, new[] { 10, 10, 10, 10, 10, 10 }, backgroundBonusMap: null,
            savingThrowsText: null, catenaUtile: false, classHitDie: "d12");

        Assert.Equal(10, baseline.MaxHitPoints);
        Assert.Equal("1d8", baseline.HitDiceMax);
    }

    [Fact]
    public void AssemblaBaseline_dado_vita_non_risolvibile_usa_la_stima_generica_d8()
    {
        // MINORE 1 del secondo giro del gate del 2026-08-06: il ripiego NON è più il valore del
        // draft (poteva essere quello di un livello più alto, digitato mentre la classe era ancora
        // a testo libero o senza tabella) — è la stessa stima "d8" che il fold userebbe per ogni
        // livello successivo col dado non riconosciuto (LevelUpPlanner.FacceDado). Il dado vita
        // resta invece il ripiego di prima (draft.HitDiceMax): questo finding riguarda solo i PF.
        var draft = new Character { MaxHitPoints = 12, HitDiceMax = "1d10" };
        var baseline = CharacterWizardLogic.AssemblaBaseline(
            draft, new[] { 10, 10, 14, 10, 10, 10 }, backgroundBonusMap: null,
            savingThrowsText: null, catenaUtile: true, classHitDie: "custom");

        Assert.Equal(10, baseline.MaxHitPoints); // d8 pieno (8) + modCOS (+2) = 10, non i 12 del draft
        Assert.Equal("1d10", baseline.HitDiceMax);
    }

    [Fact]
    public void AssemblaBaseline_dado_non_risolvibile_non_eredita_pf_di_un_livello_piu_alto()
    {
        // Il caso concreto del MINORE 1: livello 5 su una classe SENZA tabella (campo PF editabile,
        // l'utente scrive 44) → si cambia in una classe CON tabella ma dado vita vuoto. Col vecchio
        // ripiego (Math.Max(1, draft.MaxHitPoints)) il baseline di 1° livello sarebbe partito da 44
        // — un numero di un altro livello, non di 1°.
        var draft = new Character { MaxHitPoints = 44, Constitution = 10 };
        var baseline = CharacterWizardLogic.AssemblaBaseline(
            draft, new[] { 10, 10, 10, 10, 10, 10 }, backgroundBonusMap: null,
            savingThrowsText: null, catenaUtile: true, classHitDie: null);

        Assert.Equal(8, baseline.MaxHitPoints); // d8 + modCOS 0, non 44
    }

    [Fact]
    public void AssemblaBaseline_dado_non_risolvibile_e_draft_a_zero_usa_comunque_la_stima_d8()
    {
        // Ripiego del ripiego: anche se il draft non ha ancora un valore utile, il baseline non è
        // mai la sentinella 0 (bloccherebbe "Crea personaggio") — ora è la stima d8, non più
        // Math.Max(1, 0).
        var draft = new Character { MaxHitPoints = 0 };
        var baseline = CharacterWizardLogic.AssemblaBaseline(
            draft, new[] { 10, 10, 10, 10, 10, 10 }, backgroundBonusMap: null,
            savingThrowsText: null, catenaUtile: true, classHitDie: null);

        Assert.Equal(8, baseline.MaxHitPoints);
    }

    [Fact]
    public void AssemblaBaseline_override_manuale_vince_solo_quando_il_dado_non_e_riconoscibile()
    {
        // Il campo PF del passo Dettagli torna editabile SOLO quando il dado non si riconosce (§E
        // della spec): l'override manuale deve valere in quel caso...
        var draftNonRiconoscibile = new Character { Constitution = 10 };
        var baselineNonRiconoscibile = CharacterWizardLogic.AssemblaBaseline(
            draftNonRiconoscibile, new[] { 10, 10, 10, 10, 10, 10 }, backgroundBonusMap: null,
            savingThrowsText: null, catenaUtile: true, classHitDie: null, pfBaseline1LivelloManuale: 20);

        Assert.Equal(20, baselineNonRiconoscibile.MaxHitPoints);

        // ...e non deve sopravvivere silenziosamente a un dado che TORNA riconoscibile: la formula
        // vera vince sempre su un override rimasto da uno stato precedente.
        var draftRiconoscibile = new Character { Constitution = 10 };
        var baselineRiconoscibile = CharacterWizardLogic.AssemblaBaseline(
            draftRiconoscibile, new[] { 10, 10, 10, 10, 10, 10 }, backgroundBonusMap: null,
            savingThrowsText: null, catenaUtile: true, classHitDie: "d10", pfBaseline1LivelloManuale: 20);

        Assert.Equal(10, baselineRiconoscibile.MaxHitPoints); // d10 pieno (10) + modCOS 0, non l'override
    }

    // =====================================================================
    // DerivaEsito — promossa dal wizard (SERIO 4). Il branching a tre vie: livello 1, classe senza
    // tabella, fold vero (quest'ultimo verificato dal test D3 più sotto).
    // =====================================================================

    [Fact]
    public void DerivaEsito_livello_1_non_avvia_la_catena()
    {
        var baseline = new Character { Level = 1, Name = "Aria" };
        var esito = CharacterWizardLogic.DerivaEsito(
            baseline, mostraProgressione: false, classeSenzaTabella: false, livelloRichiesto: 1,
            testoProgressione: null, sottoclassi: null, talenti: null,
            rispostePerLivello: null, tiriPerLivello: null, dadoVitaClasse: null);

        Assert.Empty(esito.Tappe);
        Assert.True(esito.Completa);
        Assert.Same(baseline, esito.Personaggio);
    }

    [Fact]
    public void DerivaEsito_classe_senza_tabella_ripiega_al_livello_richiesto_senza_bloccare()
    {
        var baseline = new Character { Level = 1 };
        var esito = CharacterWizardLogic.DerivaEsito(
            baseline, mostraProgressione: true, classeSenzaTabella: true, livelloRichiesto: 5,
            testoProgressione: null, sottoclassi: null, talenti: null,
            rispostePerLivello: null, tiriPerLivello: null, dadoVitaClasse: null);

        Assert.Equal(5, esito.Personaggio.Level);
        Assert.Empty(esito.Tappe);
        Assert.True(esito.Completa);
    }

    // =====================================================================
    // D3 — SERIO 4 del gate del 2026-08-06: "Test che il sync FinalScores preceda il fold: un
    // baseline con ASI al 4° deve conservarlo". La sezione «Verifica» della spec lo chiedeva e non
    // esisteva ancora. La pipeline corretta è assembla (AssemblaBaseline, che sincronizza i punteggi
    // finali) POI deriva (DerivaEsito, il fold): se i due passi si invertissero, il fold partirebbe
    // dai punteggi grezzi del draft e un sync tardivo sovrascriverebbe l'ASI applicato dal fold.
    // =====================================================================

    [Fact]
    public void D3_pipeline_assembla_poi_deriva_conserva_l_ASI_del_4_livello()
    {
        var draft = new Character
        {
            Level = 4,
            Class = "Prova",
            Strength = 8, Dexterity = 10, Constitution = 10, Intelligence = 10, Wisdom = 10, Charisma = 10,
            MaxHitPoints = 10, HitPoints = 10, HitDiceMax = "1d8"
        };
        // Punteggi FINALI (dopo razza/background): Forza 14 — deliberatamente diverso dagli 8 grezzi
        // del draft, così un sync mancato o tardivo produce un numero diverso, non uno che
        // coinciderebbe per caso.
        var finalScores = new[] { 14, 10, 10, 10, 10, 10 };
        var talentoIncremento = new PackageFeat { Name = "Incremento del Punteggio di Caratteristica", Category = "Generale" };
        var tabella = "L4 — Incremento punteggio caratteristica";
        var risposte = new Dictionary<int, IReadOnlyDictionary<string, Risposta>>
        {
            [4] = new Dictionary<string, Risposta>(StringComparer.Ordinal)
            {
                ["L4:talento"] = new Risposta { Scelte = new[] { talentoIncremento.Name } },
                ["L4:talento/punteggi"] = new Risposta { Punteggi = new Dictionary<string, int> { ["strength"] = 2 } },
            }
        };

        var baseline = CharacterWizardLogic.AssemblaBaseline(
            draft, finalScores, backgroundBonusMap: null, savingThrowsText: null,
            catenaUtile: true, classHitDie: "d8");

        var esito = CharacterWizardLogic.DerivaEsito(
            baseline, mostraProgressione: true, classeSenzaTabella: false, livelloRichiesto: 4,
            testoProgressione: tabella, sottoclassi: null, talenti: new[] { talentoIncremento },
            rispostePerLivello: risposte, tiriPerLivello: null, dadoVitaClasse: "d8");

        // 14 (sincronizzato PRIMA del fold da AssemblaBaseline) + 2 (ASI applicato dal fold) = 16.
        // Se il sync girasse DOPO il fold (l'ordine che D3 vieta), il fold partirebbe dagli 8 grezzi
        // del draft e un sync tardivo li sovrascriverebbe con 14 SENZA l'incremento: 16 != 14
        // intercetta esattamente questo.
        Assert.Equal(16, esito.Personaggio.Strength);
    }

    // =====================================================================
    // FacceMax — SERIO 5 del gate del 2026-08-06: promossa da LevelUpDialog.razor, dove viveva
    // duplicata (identica, guardia sul "d1" compresa) nel markup del wizard.
    // =====================================================================

    [Theory]
    [InlineData(7, 12)]   // d12: media 7 → facce 12
    [InlineData(4, 6)]    // d6: media 4 → facce 6
    public void FacceMax_ricava_le_facce_dalla_media(int mediaDado, int facceAttese)
        => Assert.Equal(facceAttese, CharacterWizardLogic.FacceMax(mediaDado));

    [Fact]
    public void FacceMax_dado_d1_non_produce_zero()
        // La ragione della guardia: un HitDie scritto "d1" per errore dà media 1, e (1-1)*2 = 0
        // manderebbe Math.Clamp(valore, 1, 0) in ArgumentException altrove.
        => Assert.Equal(2, CharacterWizardLogic.FacceMax(mediaDado: 1));

    // =====================================================================
    // RiepilogoRisposta — SERIO 2 del gate del 2026-08-06: cosa mostrare di una decisione già
    // risolta, per la riga sintetica di una tappa confermata (altrimenti invisibile finché non si
    // riapre il pannello).
    // =====================================================================

    [Fact]
    public void RiepilogoRisposta_decisione_fra_opzioni_risolta_mostra_il_nome_scelto()
    {
        var decisione = new DecisioneFraOpzioni("L3:sottoclasse", "Sottoclasse del Barbaro",
            new[] { new OpzioneDecisione("Berserker", "Descrizione") }, 1);
        var risposta = new Risposta { Scelte = new[] { "Berserker" } };

        Assert.Equal("Berserker", CharacterWizardLogic.RiepilogoRisposta(decisione, risposta));
    }

    [Fact]
    public void RiepilogoRisposta_senza_risposta_torna_null()
    {
        var decisione = new DecisioneFraOpzioni("L3:sottoclasse", "Sottoclasse del Barbaro",
            new[] { new OpzioneDecisione("Berserker", "Descrizione") }, 1);

        Assert.Null(CharacterWizardLogic.RiepilogoRisposta(decisione, null));
    }

    [Fact]
    public void RiepilogoRisposta_decisione_libera_mostra_il_testo_troncato()
    {
        var decisione = new DecisioneLibera("L5:libera/dono", "Un privilegio a scelta libera", "Annota qui.");
        var risposta = new Risposta { Testo = new string('x', 60) };

        var risultato = CharacterWizardLogic.RiepilogoRisposta(decisione, risposta);

        Assert.NotNull(risultato);
        Assert.EndsWith("…", risultato);
    }

    [Fact]
    public void RiepilogoRisposta_decisione_punteggi_non_mostra_nulla()
    {
        // Il suo effetto è già visibile nei punteggi finali altrove nel wizard: ripeterlo qui
        // allungherebbe la riga senza un'informazione nuova.
        var decisione = new DecisionePunteggi("L4:talento/punteggi", "Ripartisci l'incremento",
            new Dictionary<string, int>());
        var risposta = new Risposta { Punteggi = new Dictionary<string, int> { ["strength"] = 2 } };

        Assert.Null(CharacterWizardLogic.RiepilogoRisposta(decisione, risposta));
    }
}
