using System.Text.RegularExpressions;
using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>
/// Logica pura per importare i mostri della campagna come combattenti nel tracker iniziativa.
/// Nessuno stato/I/O: il PF di Combatant è int, mentre Monster.HitPoints è testo libero
/// (es. "45 (6d8+18)") → si estrae il primo intero.
/// </summary>
public static class CombatImport
{
    // Primo intero nel testo PF; fallback 1 (mai < 1). Es. "45 (6d8+18)" -> 45, "" -> 1, "n/a" -> 1.
    public static int ParseLeadingHp(string? hitPointsText)
    {
        if (string.IsNullOrWhiteSpace(hitPointsText)) return 1;
        var match = Regex.Match(hitPointsText, @"\d+");
        if (match.Success && int.TryParse(match.Value, out var n) && n >= 1) return n;
        return 1;
    }

    // q copie di un Combatant dal mostro: nome numerato se q>1, Initiative=0, CurrentHp=MaxHp=ParseLeadingHp.
    // q <= 0 -> sequenza vuota.
    public static IEnumerable<Combatant> FromMonster(Monster monster, int quantity)
        => FromNameAndHp(monster.Name, monster.HitPoints, quantity);

    /// <summary>Come <see cref="FromMonster"/>, ma da una voce di manuale. Serve perché il tracker
    /// attinge a entrambe le sorgenti: un mostro che vive solo nel pacchetto non ha una riga di
    /// database, e non gli serve — <c>Combatant</c> è un POCO dentro il jsonb di
    /// <c>combat_state</c>, senza chiave esterna verso <c>monsters</c>.</summary>
    public static IEnumerable<Combatant> FromPackageMonster(PackageMonster monster, int quantity)
        => FromNameAndHp(monster.Name, monster.HitPoints, quantity);

    private static IEnumerable<Combatant> FromNameAndHp(string name, string? hitPointsText, int quantity)
    {
        var hp = ParseLeadingHp(hitPointsText);
        for (var i = 1; i <= quantity; i++)
        {
            yield return new Combatant
            {
                Name = quantity == 1 ? name : $"{name} {i}",
                Initiative = 0,
                CurrentHp = hp,
                MaxHp = hp,
            };
        }
    }
}
