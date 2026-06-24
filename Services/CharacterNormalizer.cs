using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Normalizzazione del draft personaggio prima del salvataggio: trim dei testi (vuoti → null per i
/// campi nullable), default di <c>Size</c> e clamp dei numerici. Muta il <see cref="Character"/>
/// passato. Funzione pura (nessuna I/O), estratta da Characters.razor per essere testabile.
/// </summary>
public static class CharacterNormalizer
{
    // Trim su tutti i testi; vuoti -> null per i campi nullable. Clamp dei numerici.
    public static void Normalize(Character c)
    {
        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        c.Name = c.Name?.Trim() ?? string.Empty;
        c.Class = c.Class?.Trim() ?? string.Empty;
        c.Race = c.Race?.Trim() ?? string.Empty;
        c.Size = string.IsNullOrWhiteSpace(c.Size) ? "Media" : c.Size.Trim();

        c.Subclass = Clean(c.Subclass);
        c.Background = Clean(c.Background);
        c.Alignment = Clean(c.Alignment);
        c.Appearance = Clean(c.Appearance);
        c.Backstory = Clean(c.Backstory);
        c.Languages = Clean(c.Languages);
        c.HitDiceMax = Clean(c.HitDiceMax);
        c.SpeciesTraits = Clean(c.SpeciesTraits);
        c.ClassFeatures = Clean(c.ClassFeatures);
        c.Feats = Clean(c.Feats);
        c.Notes = Clean(c.Notes);
        c.AttunedItem1 = Clean(c.AttunedItem1);
        c.AttunedItem2 = Clean(c.AttunedItem2);
        c.AttunedItem3 = Clean(c.AttunedItem3);
        c.DamageResistances = Clean(c.DamageResistances);
        c.DamageImmunities = Clean(c.DamageImmunities);
        c.DamageVulnerabilities = Clean(c.DamageVulnerabilities);
        c.ConditionImmunities = Clean(c.ConditionImmunities);
        c.SpellcastingAbility = Clean(c.SpellcastingAbility);

        c.Level = Math.Clamp(c.Level, 1, 20);
        c.ArmorClass = Math.Max(0, c.ArmorClass);
        c.HitPoints = Math.Max(0, c.HitPoints);
        c.TempHitPoints = Math.Max(0, c.TempHitPoints);
        c.Speed = Math.Max(0, c.Speed);
        c.HitDiceSpent = Math.Max(0, c.HitDiceSpent);
        c.ExperiencePoints = Math.Max(0, c.ExperiencePoints);
        c.CopperPieces = Math.Max(0, c.CopperPieces);
        c.SilverPieces = Math.Max(0, c.SilverPieces);
        c.ElectrumPieces = Math.Max(0, c.ElectrumPieces);
        c.GoldPieces = Math.Max(0, c.GoldPieces);
        c.PlatinumPieces = Math.Max(0, c.PlatinumPieces);
    }
}
