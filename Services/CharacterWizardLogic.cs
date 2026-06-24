using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Helper di sole funzioni pure per il wizard di creazione PG: applicazione bonus razza,
/// costruzione dado vita, suggerimento PF e tiri salvezza. Nessuno stato, nessuna I/O.
/// Stesso pattern di <see cref="CharacterCalculations"/>.
/// </summary>
public static class CharacterWizardLogic
{
    /// <summary>Finali = base + bonus razza (ordine FOR,DES,COS,INT,SAG,CAR), clamp 1..30.
    /// race null → base clampati; baseScores più corto → mancanti = 10.</summary>
    public static int[] FinalAbilityScores(int[] baseScores, Race? race)
    {
        var bonuses = race is null
            ? new[] { 0, 0, 0, 0, 0, 0 }
            : new[] { race.StrBonus, race.DexBonus, race.ConBonus, race.IntBonus, race.WisBonus, race.ChaBonus };

        var result = new int[6];
        for (var i = 0; i < 6; i++)
        {
            var b = baseScores is not null && i < baseScores.Length ? baseScores[i] : 10;
            result[i] = Math.Clamp(b + bonuses[i], 1, 30);
        }
        return result;
    }

    /// <summary>"d12" + livello 3 → "3d12". Dado vuoto/non riconosciuto → "". livello &lt; 1 trattato 1.</summary>
    public static string BuildHitDice(string? classHitDie, int level)
    {
        var die = ParseDieSize(classHitDie);
        if (die is null) return string.Empty;
        var lvl = level < 1 ? 1 : level;
        return $"{lvl}d{die.Value}";
    }

    /// <summary>Dimensione del dado dopo la prima 'd'/'D' (es. "d12"/"1d6" → 12/6). null se assente o non parsabile.</summary>
    private static int? ParseDieSize(string? hitDie)
    {
        if (string.IsNullOrWhiteSpace(hitDie)) return null;
        var lower = hitDie.ToLowerInvariant();
        var idx = lower.IndexOf('d');
        if (idx < 0 || idx + 1 >= lower.Length) return null;
        var digits = new string(lower.Skip(idx + 1).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) && n > 0 ? n : (int?)null;
    }
}
