using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>Le sei caratteristiche di D&D 5e.</summary>
public enum AbilityType { Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma }

/// <summary>Le 18 abilità (skill) di D&D 5e, naming coerente coi campi DB.</summary>
public enum SkillType
{
    Athletics,
    Acrobatics, SleightOfHand, Stealth,
    Arcana, History, Investigation, Nature, Religion,
    AnimalHandling, Insight, Medicine, Perception, Survival,
    Deception, Intimidation, Performance, Persuasion
}

/// <summary>
/// Helper di sole funzioni pure: centralizza tutte le formule derivate
/// della scheda personaggio. Nessuno stato, nessun side effect, nessuna I/O.
/// </summary>
public static class CharacterCalculations
{
    /// <summary>Modificatore di caratteristica: floor((score - 10) / 2).</summary>
    public static int GetModifier(int abilityScore)
        => (int)Math.Floor((abilityScore - 10) / 2.0);

    /// <summary>Bonus di competenza in base al livello (clamp 1..20).</summary>
    public static int GetProficiencyBonus(int level)
    {
        var clamped = Math.Min(Math.Max(level, 1), 20);
        return 2 + ((clamped - 1) / 4);
    }

    /// <summary>Bonus al tiro salvezza: modificatore + competenza se competente.</summary>
    public static int GetSaveBonus(Character c, AbilityType ability)
    {
        var mod = GetModifier(GetAbilityScore(c, ability));
        return mod + (IsProficientInSave(c, ability) ? GetProficiencyBonus(c.Level) : 0);
    }

    /// <summary>Caratteristica di riferimento per una skill.</summary>
    public static AbilityType GetSkillAbility(SkillType skill) => skill switch
    {
        SkillType.Athletics => AbilityType.Strength,

        SkillType.Acrobatics => AbilityType.Dexterity,
        SkillType.SleightOfHand => AbilityType.Dexterity,
        SkillType.Stealth => AbilityType.Dexterity,

        SkillType.Arcana => AbilityType.Intelligence,
        SkillType.History => AbilityType.Intelligence,
        SkillType.Investigation => AbilityType.Intelligence,
        SkillType.Nature => AbilityType.Intelligence,
        SkillType.Religion => AbilityType.Intelligence,

        SkillType.AnimalHandling => AbilityType.Wisdom,
        SkillType.Insight => AbilityType.Wisdom,
        SkillType.Medicine => AbilityType.Wisdom,
        SkillType.Perception => AbilityType.Wisdom,
        SkillType.Survival => AbilityType.Wisdom,

        SkillType.Deception => AbilityType.Charisma,
        SkillType.Intimidation => AbilityType.Charisma,
        SkillType.Performance => AbilityType.Charisma,
        SkillType.Persuasion => AbilityType.Charisma,

        _ => AbilityType.Strength
    };

    /// <summary>Bonus alla skill: modificatore + competenza (raddoppiata se expertise).</summary>
    public static int GetSkillBonus(Character c, SkillType skill)
    {
        var mod = GetModifier(GetAbilityScore(c, GetSkillAbility(skill)));
        var pb = GetProficiencyBonus(c.Level);
        var bonus = mod;
        // Competente e con expertise: pb sommato due volte (raddoppiato).
        // Solo expertise senza competenza (anomalo): pb singolo (defensive).
        if (IsProficientInSkill(c, skill)) bonus += pb;
        if (HasExpertiseInSkill(c, skill)) bonus += pb;
        return bonus;
    }

    /// <summary>Iniziativa: solo modificatore di Destrezza (5e standard).</summary>
    public static int GetInitiative(Character c) => GetModifier(c.Dexterity);

    /// <summary>Percezione passiva: 10 + bonus alla skill Percezione.</summary>
    public static int GetPassivePerception(Character c)
        => 10 + GetSkillBonus(c, SkillType.Perception);

    /// <summary>CD dei tiri salvezza degli incantesimi, o null se non incantatore.</summary>
    public static int? GetSpellSaveDc(Character c)
    {
        var ability = ParseSpellcastingAbility(c.SpellcastingAbility);
        if (ability is null) return null;
        return 8 + GetProficiencyBonus(c.Level) + GetModifier(GetAbilityScore(c, ability.Value));
    }

    /// <summary>Bonus di attacco con incantesimi, o null se non incantatore.</summary>
    public static int? GetSpellAttackBonus(Character c)
    {
        var ability = ParseSpellcastingAbility(c.SpellcastingAbility);
        if (ability is null) return null;
        return GetProficiencyBonus(c.Level) + GetModifier(GetAbilityScore(c, ability.Value));
    }

    /// <summary>Modificatore della caratteristica di incantamento, o null se non incantatore.</summary>
    public static int? GetSpellcastingModifier(Character c)
    {
        var ability = ParseSpellcastingAbility(c.SpellcastingAbility);
        if (ability is null) return null;
        return GetModifier(GetAbilityScore(c, ability.Value));
    }

    /// <summary>Totale dadi vita: somma dei blocchi "NdM" di una stringa tipo "3d12+2d8" (0 se non parsabile).</summary>
    public static int GetHitDiceTotal(string? hitDiceMax)
    {
        if (string.IsNullOrWhiteSpace(hitDiceMax)) return 0;

        try
        {
            var total = 0;
            foreach (var block in hitDiceMax.Split('+'))
            {
                var trimmed = block.Trim();
                if (trimmed.Length == 0) continue;
                var countPart = trimmed.Split('d')[0].Trim();
                if (int.TryParse(countPart, out var count))
                    total += count;
            }
            return total;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Dadi vita rimanenti: totale meno HitDiceSpent (0 se HitDiceMax assente).</summary>
    public static int GetHitDiceRemaining(Character c)
    {
        if (string.IsNullOrWhiteSpace(c.HitDiceMax)) return 0;
        return GetHitDiceTotal(c.HitDiceMax) - c.HitDiceSpent;
    }

    // ---------------------------------------------------------------
    // Helper privati
    // ---------------------------------------------------------------

    private static int GetAbilityScore(Character c, AbilityType a) => a switch
    {
        AbilityType.Strength => c.Strength,
        AbilityType.Dexterity => c.Dexterity,
        AbilityType.Constitution => c.Constitution,
        AbilityType.Intelligence => c.Intelligence,
        AbilityType.Wisdom => c.Wisdom,
        AbilityType.Charisma => c.Charisma,
        _ => 10
    };

    private static bool IsProficientInSave(Character c, AbilityType a) => a switch
    {
        AbilityType.Strength => c.ProfSaveStrength,
        AbilityType.Dexterity => c.ProfSaveDexterity,
        AbilityType.Constitution => c.ProfSaveConstitution,
        AbilityType.Intelligence => c.ProfSaveIntelligence,
        AbilityType.Wisdom => c.ProfSaveWisdom,
        AbilityType.Charisma => c.ProfSaveCharisma,
        _ => false
    };

    private static bool IsProficientInSkill(Character c, SkillType skill) => skill switch
    {
        SkillType.Athletics => c.ProfAthletics,
        SkillType.Acrobatics => c.ProfAcrobatics,
        SkillType.SleightOfHand => c.ProfSleightOfHand,
        SkillType.Stealth => c.ProfStealth,
        SkillType.Arcana => c.ProfArcana,
        SkillType.History => c.ProfHistory,
        SkillType.Investigation => c.ProfInvestigation,
        SkillType.Nature => c.ProfNature,
        SkillType.Religion => c.ProfReligion,
        SkillType.AnimalHandling => c.ProfAnimalHandling,
        SkillType.Insight => c.ProfInsight,
        SkillType.Medicine => c.ProfMedicine,
        SkillType.Perception => c.ProfPerception,
        SkillType.Survival => c.ProfSurvival,
        SkillType.Deception => c.ProfDeception,
        SkillType.Intimidation => c.ProfIntimidation,
        SkillType.Performance => c.ProfPerformance,
        SkillType.Persuasion => c.ProfPersuasion,
        _ => false
    };

    private static bool HasExpertiseInSkill(Character c, SkillType skill) => skill switch
    {
        SkillType.Athletics => c.ExpAthletics,
        SkillType.Acrobatics => c.ExpAcrobatics,
        SkillType.SleightOfHand => c.ExpSleightOfHand,
        SkillType.Stealth => c.ExpStealth,
        SkillType.Arcana => c.ExpArcana,
        SkillType.History => c.ExpHistory,
        SkillType.Investigation => c.ExpInvestigation,
        SkillType.Nature => c.ExpNature,
        SkillType.Religion => c.ExpReligion,
        SkillType.AnimalHandling => c.ExpAnimalHandling,
        SkillType.Insight => c.ExpInsight,
        SkillType.Medicine => c.ExpMedicine,
        SkillType.Perception => c.ExpPerception,
        SkillType.Survival => c.ExpSurvival,
        SkillType.Deception => c.ExpDeception,
        SkillType.Intimidation => c.ExpIntimidation,
        SkillType.Performance => c.ExpPerformance,
        SkillType.Persuasion => c.ExpPersuasion,
        _ => false
    };

    private static AbilityType? ParseSpellcastingAbility(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "intelligence" => AbilityType.Intelligence,
            "wisdom" => AbilityType.Wisdom,
            "charisma" => AbilityType.Charisma,
            _ => null
        };
    }
}
