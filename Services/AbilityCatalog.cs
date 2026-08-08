using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Fonte unica della corrispondenza <see cref="AbilityType"/> ↔ nome italiano esteso. Helper puro
/// <c>static</c>: nessuno stato, nessuna I/O. Gemello di <see cref="SkillCatalog"/> per lo stesso
/// bisogno: prima di questo helper Forza/Destrezza/Costituzione/Intelligenza/Saggezza/Carisma erano
/// scritte due volte, carattere per carattere, in <c>Pages/Characters.razor</c> (<c>NomeEsteso</c>) e
/// <c>Shared/CharacterTabs/CharacterStatsTab.razor</c> (<c>NomeCompleto</c>), entrambe solo per un
/// <c>aria-label</c> (v. gate 2026-08-08).
/// </summary>
public static class AbilityCatalog
{
    private static readonly Dictionary<AbilityType, string> NomiItaliani = new()
    {
        [AbilityType.Strength] = "Forza",
        [AbilityType.Dexterity] = "Destrezza",
        [AbilityType.Constitution] = "Costituzione",
        [AbilityType.Intelligence] = "Intelligenza",
        [AbilityType.Wisdom] = "Saggezza",
        [AbilityType.Charisma] = "Carisma",
    };

    /// <summary>Nome italiano esteso ("Forza", "Destrezza", ...). <c>"?"</c> se il valore non è fra
    /// le sei caratteristiche note — lo stesso fallback delle due copie sostituite da questo
    /// helper.</summary>
    public static string Nome(AbilityType abilita)
        => NomiItaliani.TryGetValue(abilita, out var nome) ? nome : "?";
}
