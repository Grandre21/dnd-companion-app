using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("characters")]
public class Character : BaseModel
{
    // ---------------------------------------------------------------
    // Identità base
    // ---------------------------------------------------------------
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("player_id")]
    public string PlayerId { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("class")]
    public string Class { get; set; } = string.Empty;

    [Column("race")]
    public string Race { get; set; } = string.Empty;

    [Column("level")]
    public int Level { get; set; } = 1;

    [Column("hit_points")]
    public int HitPoints { get; set; }

    [Column("max_hit_points")]
    public int MaxHitPoints { get; set; }

    [Column("armor_class")]
    public int ArmorClass { get; set; } = 10;

    [Column("strength")]
    public int Strength { get; set; } = 10;

    [Column("dexterity")]
    public int Dexterity { get; set; } = 10;

    [Column("constitution")]
    public int Constitution { get; set; } = 10;

    [Column("intelligence")]
    public int Intelligence { get; set; } = 10;

    [Column("wisdom")]
    public int Wisdom { get; set; } = 10;

    [Column("charisma")]
    public int Charisma { get; set; } = 10;

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    // ---------------------------------------------------------------
    // Identità estesa
    // ---------------------------------------------------------------
    [Column("background")]
    public string? Background { get; set; }

    [Column("subclass")]
    public string? Subclass { get; set; }

    [Column("alignment")]
    public string? Alignment { get; set; }

    [Column("experience_points")]
    public int ExperiencePoints { get; set; }

    [Column("size")]
    public string Size { get; set; } = "Media";

    [Column("speed")]
    public int Speed { get; set; } = 9;

    [Column("appearance")]
    public string? Appearance { get; set; }

    [Column("backstory")]
    public string? Backstory { get; set; }

    [Column("languages")]
    public string? Languages { get; set; }

    // ---------------------------------------------------------------
    // HP avanzati e stato in combattimento
    // ---------------------------------------------------------------
    [Column("temp_hit_points")]
    public int TempHitPoints { get; set; }

    [Column("hit_dice_max")]
    public string? HitDiceMax { get; set; }

    [Column("hit_dice_spent")]
    public int HitDiceSpent { get; set; }

    [Column("death_save_successes")]
    public int DeathSaveSuccesses { get; set; }

    [Column("death_save_failures")]
    public int DeathSaveFailures { get; set; }

    [Column("heroic_inspiration")]
    public bool HeroicInspiration { get; set; }

    // ---------------------------------------------------------------
    // Competenze tiri salvezza
    // ---------------------------------------------------------------
    [Column("prof_save_strength")]
    public bool ProfSaveStrength { get; set; }

    [Column("prof_save_dexterity")]
    public bool ProfSaveDexterity { get; set; }

    [Column("prof_save_constitution")]
    public bool ProfSaveConstitution { get; set; }

    [Column("prof_save_intelligence")]
    public bool ProfSaveIntelligence { get; set; }

    [Column("prof_save_wisdom")]
    public bool ProfSaveWisdom { get; set; }

    [Column("prof_save_charisma")]
    public bool ProfSaveCharisma { get; set; }

    // ---------------------------------------------------------------
    // Skill (competenza + esperienza)
    // ---------------------------------------------------------------
    [Column("prof_athletics")]
    public bool ProfAthletics { get; set; }

    [Column("exp_athletics")]
    public bool ExpAthletics { get; set; }

    [Column("prof_acrobatics")]
    public bool ProfAcrobatics { get; set; }

    [Column("exp_acrobatics")]
    public bool ExpAcrobatics { get; set; }

    [Column("prof_sleight_of_hand")]
    public bool ProfSleightOfHand { get; set; }

    [Column("exp_sleight_of_hand")]
    public bool ExpSleightOfHand { get; set; }

    [Column("prof_stealth")]
    public bool ProfStealth { get; set; }

    [Column("exp_stealth")]
    public bool ExpStealth { get; set; }

    [Column("prof_arcana")]
    public bool ProfArcana { get; set; }

    [Column("exp_arcana")]
    public bool ExpArcana { get; set; }

    [Column("prof_history")]
    public bool ProfHistory { get; set; }

    [Column("exp_history")]
    public bool ExpHistory { get; set; }

    [Column("prof_investigation")]
    public bool ProfInvestigation { get; set; }

    [Column("exp_investigation")]
    public bool ExpInvestigation { get; set; }

    [Column("prof_nature")]
    public bool ProfNature { get; set; }

    [Column("exp_nature")]
    public bool ExpNature { get; set; }

    [Column("prof_religion")]
    public bool ProfReligion { get; set; }

    [Column("exp_religion")]
    public bool ExpReligion { get; set; }

    [Column("prof_animal_handling")]
    public bool ProfAnimalHandling { get; set; }

    [Column("exp_animal_handling")]
    public bool ExpAnimalHandling { get; set; }

    [Column("prof_insight")]
    public bool ProfInsight { get; set; }

    [Column("exp_insight")]
    public bool ExpInsight { get; set; }

    [Column("prof_medicine")]
    public bool ProfMedicine { get; set; }

    [Column("exp_medicine")]
    public bool ExpMedicine { get; set; }

    [Column("prof_perception")]
    public bool ProfPerception { get; set; }

    [Column("exp_perception")]
    public bool ExpPerception { get; set; }

    [Column("prof_survival")]
    public bool ProfSurvival { get; set; }

    [Column("exp_survival")]
    public bool ExpSurvival { get; set; }

    [Column("prof_deception")]
    public bool ProfDeception { get; set; }

    [Column("exp_deception")]
    public bool ExpDeception { get; set; }

    [Column("prof_intimidation")]
    public bool ProfIntimidation { get; set; }

    [Column("exp_intimidation")]
    public bool ExpIntimidation { get; set; }

    [Column("prof_performance")]
    public bool ProfPerformance { get; set; }

    [Column("exp_performance")]
    public bool ExpPerformance { get; set; }

    [Column("prof_persuasion")]
    public bool ProfPersuasion { get; set; }

    [Column("exp_persuasion")]
    public bool ExpPersuasion { get; set; }

    // ---------------------------------------------------------------
    // Tratti, talenti, privilegi
    // ---------------------------------------------------------------
    [Column("species_traits")]
    public string? SpeciesTraits { get; set; }

    [Column("class_features")]
    public string? ClassFeatures { get; set; }

    [Column("feats")]
    public string? Feats { get; set; }

    // ---------------------------------------------------------------
    // Denari
    // ---------------------------------------------------------------
    [Column("copper_pieces")]
    public int CopperPieces { get; set; }

    [Column("silver_pieces")]
    public int SilverPieces { get; set; }

    [Column("electrum_pieces")]
    public int ElectrumPieces { get; set; }

    [Column("gold_pieces")]
    public int GoldPieces { get; set; }

    [Column("platinum_pieces")]
    public int PlatinumPieces { get; set; }

    // ---------------------------------------------------------------
    // Sintonia oggetti magici (max 3)
    // ---------------------------------------------------------------
    [Column("attuned_item_1")]
    public string? AttunedItem1 { get; set; }

    [Column("attuned_item_2")]
    public string? AttunedItem2 { get; set; }

    [Column("attuned_item_3")]
    public string? AttunedItem3 { get; set; }

    // ---------------------------------------------------------------
    // Resistenze / Immunità / Vulnerabilità
    // ---------------------------------------------------------------
    [Column("damage_resistances")]
    public string? DamageResistances { get; set; }

    [Column("damage_immunities")]
    public string? DamageImmunities { get; set; }

    [Column("damage_vulnerabilities")]
    public string? DamageVulnerabilities { get; set; }

    [Column("condition_immunities")]
    public string? ConditionImmunities { get; set; }

    // ---------------------------------------------------------------
    // Sezione incantatore
    // ---------------------------------------------------------------
    [Column("spellcasting_ability")]
    public string? SpellcastingAbility { get; set; }

    [Column("spell_slots_1_max")]
    public int SpellSlots1Max { get; set; }

    [Column("spell_slots_1_used")]
    public int SpellSlots1Used { get; set; }

    [Column("spell_slots_2_max")]
    public int SpellSlots2Max { get; set; }

    [Column("spell_slots_2_used")]
    public int SpellSlots2Used { get; set; }

    [Column("spell_slots_3_max")]
    public int SpellSlots3Max { get; set; }

    [Column("spell_slots_3_used")]
    public int SpellSlots3Used { get; set; }

    [Column("spell_slots_4_max")]
    public int SpellSlots4Max { get; set; }

    [Column("spell_slots_4_used")]
    public int SpellSlots4Used { get; set; }

    [Column("spell_slots_5_max")]
    public int SpellSlots5Max { get; set; }

    [Column("spell_slots_5_used")]
    public int SpellSlots5Used { get; set; }

    [Column("spell_slots_6_max")]
    public int SpellSlots6Max { get; set; }

    [Column("spell_slots_6_used")]
    public int SpellSlots6Used { get; set; }

    [Column("spell_slots_7_max")]
    public int SpellSlots7Max { get; set; }

    [Column("spell_slots_7_used")]
    public int SpellSlots7Used { get; set; }

    [Column("spell_slots_8_max")]
    public int SpellSlots8Max { get; set; }

    [Column("spell_slots_8_used")]
    public int SpellSlots8Used { get; set; }

    [Column("spell_slots_9_max")]
    public int SpellSlots9Max { get; set; }

    [Column("spell_slots_9_used")]
    public int SpellSlots9Used { get; set; }
}
