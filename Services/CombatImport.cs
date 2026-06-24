using System.Text.RegularExpressions;
using DndCompanion.Models;

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
    {
        var hp = ParseLeadingHp(monster.HitPoints);
        for (var i = 1; i <= quantity; i++)
        {
            yield return new Combatant
            {
                Name = quantity == 1 ? monster.Name : $"{monster.Name} {i}",
                Initiative = 0,
                CurrentHp = hp,
                MaxHp = hp,
            };
        }
    }
}
