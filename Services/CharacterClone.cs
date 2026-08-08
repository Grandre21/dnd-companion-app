using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>Copia profonda di un personaggio. Nata dentro <c>Pages/Characters.razor</c> per il form
/// di modifica — la bozza deve fare un round-trip COMPLETO, perché <c>SaveFormAsync</c> rimanda la
/// bozza intera a <c>UpdateCharacterAsync</c> e postgrest serializza TUTTE le colonne mappate, non
/// solo quelle toccate dall'utente: una proprietà non copiata qui si azzera in database al primo
/// salvataggio (già successo due volte: Subclass, poi ClassResources/ArmorTraining/
/// WeaponProficiencies/ToolProficiencies). Promossa in <c>Services/</c> perché anche
/// <see cref="CreationChain"/> ne ha bisogno per non mutare il baseline che riceve.
///
/// <see cref="Tests.CharacterCloneTests"/> confronta per riflessione TUTTE le proprietà pubbliche
/// dichiarate su <see cref="Character"/> (non quelle eredidate da <c>BaseModel</c>, che questa
/// classe non tocca): il prossimo campo dimenticato fa fallire quel test invece di azzerare dati in
/// produzione.</summary>
public static class CharacterClone
{
    /// <summary>Copia profonda di un personaggio: ogni proprietà pubblica, e le liste per valore.</summary>
    public static Character Clona(Character c) => new()
    {
        Id = c.Id,
        OwnerId = c.OwnerId,
        CampaignId = c.CampaignId,
        Name = c.Name,
        Class = c.Class,
        Race = c.Race,
        Level = c.Level,
        HitPoints = c.HitPoints,
        MaxHitPoints = c.MaxHitPoints,
        ArmorClass = c.ArmorClass,
        Strength = c.Strength,
        Dexterity = c.Dexterity,
        Constitution = c.Constitution,
        Intelligence = c.Intelligence,
        Wisdom = c.Wisdom,
        Charisma = c.Charisma,
        Notes = c.Notes,
        CreatedAt = c.CreatedAt,

        // Identità estesa
        Background = c.Background,
        BackgroundAbilityChoice = c.BackgroundAbilityChoice,
        Subclass = c.Subclass,
        Alignment = c.Alignment,
        ExperiencePoints = c.ExperiencePoints,
        Size = c.Size,
        Speed = c.Speed,
        Appearance = c.Appearance,
        Backstory = c.Backstory,
        Languages = c.Languages,

        // HP avanzati e stato in combattimento
        TempHitPoints = c.TempHitPoints,
        HitDiceMax = c.HitDiceMax,
        HitDiceSpent = c.HitDiceSpent,
        DeathSaveSuccesses = c.DeathSaveSuccesses,
        DeathSaveFailures = c.DeathSaveFailures,
        HeroicInspiration = c.HeroicInspiration,

        // Risorse di classe (jsonb): copia PER VALORE, mai `= c.ClassResources` — altrimenti la
        // bozza e il personaggio aperto condividerebbero la stessa lista, e "Annulla" sul form
        // non annullerebbe più nulla (mutare il clone muterebbe anche l'originale).
        ClassResources = c.ClassResources.Select(r => new ClassResource
        {
            Nome = r.Nome, Max = r.Max, Spesi = r.Spesi, Ricarica = r.Ricarica,
        }).ToList(),

        // Annotazioni sui privilegi (jsonb): copia PER VALORE, mai `= c.CharacterFeatures` —
        // altrimenti la bozza e il personaggio aperto condividerebbero la stessa lista, e "Annulla"
        // sul form non annullerebbe più nulla (mutare il clone muterebbe anche l'originale).
        CharacterFeatures = c.CharacterFeatures.Select(f => new CharacterFeature
        {
            Nome = f.Nome, Nota = f.Nota, Azione = f.Azione, Risorsa = f.Risorsa, Attivabile = f.Attivabile,
        }).ToList(),

        // Competenze tiri salvezza
        ProfSaveStrength = c.ProfSaveStrength,
        ProfSaveDexterity = c.ProfSaveDexterity,
        ProfSaveConstitution = c.ProfSaveConstitution,
        ProfSaveIntelligence = c.ProfSaveIntelligence,
        ProfSaveWisdom = c.ProfSaveWisdom,
        ProfSaveCharisma = c.ProfSaveCharisma,

        // Skill: competenza
        ProfAthletics = c.ProfAthletics,
        ProfAcrobatics = c.ProfAcrobatics,
        ProfSleightOfHand = c.ProfSleightOfHand,
        ProfStealth = c.ProfStealth,
        ProfArcana = c.ProfArcana,
        ProfHistory = c.ProfHistory,
        ProfInvestigation = c.ProfInvestigation,
        ProfNature = c.ProfNature,
        ProfReligion = c.ProfReligion,
        ProfAnimalHandling = c.ProfAnimalHandling,
        ProfInsight = c.ProfInsight,
        ProfMedicine = c.ProfMedicine,
        ProfPerception = c.ProfPerception,
        ProfSurvival = c.ProfSurvival,
        ProfDeception = c.ProfDeception,
        ProfIntimidation = c.ProfIntimidation,
        ProfPerformance = c.ProfPerformance,
        ProfPersuasion = c.ProfPersuasion,

        // Skill: expertise
        ExpAthletics = c.ExpAthletics,
        ExpAcrobatics = c.ExpAcrobatics,
        ExpSleightOfHand = c.ExpSleightOfHand,
        ExpStealth = c.ExpStealth,
        ExpArcana = c.ExpArcana,
        ExpHistory = c.ExpHistory,
        ExpInvestigation = c.ExpInvestigation,
        ExpNature = c.ExpNature,
        ExpReligion = c.ExpReligion,
        ExpAnimalHandling = c.ExpAnimalHandling,
        ExpInsight = c.ExpInsight,
        ExpMedicine = c.ExpMedicine,
        ExpPerception = c.ExpPerception,
        ExpSurvival = c.ExpSurvival,
        ExpDeception = c.ExpDeception,
        ExpIntimidation = c.ExpIntimidation,
        ExpPerformance = c.ExpPerformance,
        ExpPersuasion = c.ExpPersuasion,

        // Tratti, talenti, privilegi
        SpeciesTraits = c.SpeciesTraits,
        ClassFeatures = c.ClassFeatures,
        Feats = c.Feats,

        // Addestramento
        ArmorTraining = c.ArmorTraining,
        WeaponProficiencies = c.WeaponProficiencies,
        ToolProficiencies = c.ToolProficiencies,

        // Denari
        CopperPieces = c.CopperPieces,
        SilverPieces = c.SilverPieces,
        ElectrumPieces = c.ElectrumPieces,
        GoldPieces = c.GoldPieces,
        PlatinumPieces = c.PlatinumPieces,

        // Sintonia oggetti magici
        AttunedItem1 = c.AttunedItem1,
        AttunedItem2 = c.AttunedItem2,
        AttunedItem3 = c.AttunedItem3,

        // Difese
        DamageResistances = c.DamageResistances,
        DamageImmunities = c.DamageImmunities,
        DamageVulnerabilities = c.DamageVulnerabilities,
        ConditionImmunities = c.ConditionImmunities,

        // Incantatore
        SpellcastingAbility = c.SpellcastingAbility,
        SpellSlots1Max = c.SpellSlots1Max, SpellSlots1Used = c.SpellSlots1Used,
        SpellSlots2Max = c.SpellSlots2Max, SpellSlots2Used = c.SpellSlots2Used,
        SpellSlots3Max = c.SpellSlots3Max, SpellSlots3Used = c.SpellSlots3Used,
        SpellSlots4Max = c.SpellSlots4Max, SpellSlots4Used = c.SpellSlots4Used,
        SpellSlots5Max = c.SpellSlots5Max, SpellSlots5Used = c.SpellSlots5Used,
        SpellSlots6Max = c.SpellSlots6Max, SpellSlots6Used = c.SpellSlots6Used,
        SpellSlots7Max = c.SpellSlots7Max, SpellSlots7Used = c.SpellSlots7Used,
        SpellSlots8Max = c.SpellSlots8Max, SpellSlots8Used = c.SpellSlots8Used,
        SpellSlots9Max = c.SpellSlots9Max, SpellSlots9Used = c.SpellSlots9Used
    };
}
