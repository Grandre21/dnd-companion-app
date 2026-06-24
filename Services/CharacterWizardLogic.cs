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
}
